using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.ChapterRules.Calibration;
using Jellyfin.Plugin.ChapterRules.Model;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaSegments;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ChapterRules.ScheduledTasks;

/// <summary>
/// Works out, for every series, which chapter position corresponds to which segment type,
/// and stores the rules that survive the confidence threshold.
/// </summary>
public class CalibrateRulesTask : IScheduledTask
{
    private LibraryOptions? _evidenceOptions;

    private readonly ILibraryManager _libraryManager;
    private readonly IChapterRepository _chapterRepository;
    private readonly IMediaSegmentManager _mediaSegmentManager;
    private readonly ILogger<CalibrateRulesTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalibrateRulesTask"/> class.
    /// </summary>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="chapterRepository">Chapter repository.</param>
    /// <param name="mediaSegmentManager">Media segment manager.</param>
    /// <param name="logger">Logger.</param>
    public CalibrateRulesTask(
        ILibraryManager libraryManager,
        IChapterRepository chapterRepository,
        IMediaSegmentManager mediaSegmentManager,
        ILogger<CalibrateRulesTask> logger)
    {
        _libraryManager = libraryManager;
        _chapterRepository = chapterRepository;
        _mediaSegmentManager = mediaSegmentManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Calibrate chapter rules";

    /// <inheritdoc />
    public string Key => "ChapterRulesCalibrate";

    /// <inheritdoc />
    public string Description =>
        "Compares chapter positions against segments that are already known to be correct and "
        + "stores, per series, which position reliably marks an intro, recap or outro.";

    /// <inheritdoc />
    public string Category => "Chapter Rules";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() =>
    [
        new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.DailyTrigger,
            TimeOfDayTicks = TimeSpan.FromHours(3).Ticks,
        }
    ];

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);

        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return;
        }

        var config = plugin.Configuration;

        var allSeries = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Series],
            IsVirtualItem = false,
            Recursive = true,
        });

        _logger.LogInformation("Calibrating chapter rules across {Count} series", allSeries.Count);

        config.CalibratedSeries.Clear();
        var done = 0;

        foreach (var series in allSeries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var samples = await BuildSamplesAsync(series, cancellationToken).ConfigureAwait(false);
            if (samples.Count > 0)
            {
                var rules = RuleCalibrator.Calibrate(samples, config);
                if (rules.Count > 0)
                {
                    var entry = new SeriesRules
                    {
                        SeriesId = series.Id,
                        SeriesName = series.Name,
                        CalibratedAt = DateTime.UtcNow,
                    };

                    foreach (var rule in rules)
                    {
                        entry.Rules.Add(rule);
                        _logger.LogInformation(
                            "{Series}: {Type} anchored on chapter {Anchor} — {Confidence:P0} of {Samples} samples, p90 {P90:F0}s, {Gaps} gaps",
                            series.Name, rule.Type, rule.Anchor, rule.Confidence, rule.Samples,
                            rule.P90DeviationSeconds, rule.Gaps);
                    }

                    config.CalibratedSeries.Add(entry);
                }
                else
                {
                    _logger.LogInformation(
                        "{Series}: no rule met the threshold ({Samples} episodes with chapters)",
                        series.Name, samples.Count);
                }
            }

            progress.Report(100.0 * ++done / allSeries.Count);
        }

        plugin.SaveConfiguration();
        _logger.LogInformation(
            "Calibration finished, {Count} series have at least one rule", config.CalibratedSeries.Count);
    }

    /// <summary>
    /// Reduces a series to the per-episode data calibration needs.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Episodes that have at least two chapters.</returns>
    private async Task<IReadOnlyList<EpisodeSample>> BuildSamplesAsync(
        BaseItem series,
        CancellationToken cancellationToken)
    {
        var episodes = _libraryManager.GetItemList(new InternalItemsQuery
        {
            AncestorIds = [series.Id],
            IncludeItemTypes = [BaseItemKind.Episode],
            IsVirtualItem = false,
            Recursive = true,
        });

        var samples = new List<EpisodeSample>();

        foreach (var item in episodes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (item is not Episode episode || (episode.RunTimeTicks ?? 0) <= 0)
            {
                continue;
            }

            var chapters = _chapterRepository.GetChapters(episode.Id);
            if (chapters.Count < 2)
            {
                continue;
            }

            var known = new Dictionary<MediaSegmentType, (double Start, double End)>();
            // Every segment another provider supplied counts as evidence, whichever plugin
            // produced it. Our own is filtered out; see EvidenceFilter for why that needs the
            // provider id and filterByProvider: true rather than the obvious spelling.
            _evidenceOptions ??= EvidenceFilter.ExcludingSelf(
                _mediaSegmentManager, episode, Plugin.Instance?.Name ?? "Chapter Rules");

            var existing = await _mediaSegmentManager
                .GetSegmentsAsync(episode, null, _evidenceOptions, true)
                .ConfigureAwait(false);

            foreach (var segment in existing)
            {
                known[segment.Type] = (
                    segment.StartTicks / (double)TimeSpan.TicksPerSecond,
                    segment.EndTicks / (double)TimeSpan.TicksPerSecond);
            }

            samples.Add(new EpisodeSample
            {
                Id = episode.Id,
                Duration = (episode.RunTimeTicks ?? 0) / (double)TimeSpan.TicksPerSecond,
                Chapters = [.. chapters.Select(c => c.StartPositionTicks / (double)TimeSpan.TicksPerSecond)],
                Known = known,
            });
        }

        return samples;
    }
}
