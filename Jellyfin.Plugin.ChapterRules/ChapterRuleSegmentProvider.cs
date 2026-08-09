using Jellyfin.Plugin.ChapterRules.Calibration;
using Jellyfin.Plugin.ChapterRules.Model;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.MediaSegments;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.MediaSegments;

namespace Jellyfin.Plugin.ChapterRules;

/// <summary>
/// Serves segments derived from chapter positions using the rules stored by the
/// calibration task. Deriving a segment is a lookup plus arithmetic, so unlike
/// fingerprint based providers there is nothing to precompute or cache per episode.
/// </summary>
public class ChapterRuleSegmentProvider : IMediaSegmentProvider
{
    private static readonly IReadOnlyList<MediaSegmentDto> _none = [];

    /// <summary>
    /// Options used when checking what another provider already supplied. This plugin's own
    /// provider is excluded so a segment it wrote in an earlier pass does not read as
    /// "someone else already covered this" and suppress the rule forever.
    /// </summary>
    private static readonly LibraryOptions _foreignOnly = new()
    {
        DisabledMediaSegmentProviders = ["Chapter Rules"],
    };

    private readonly IItemRepository _itemRepository;
    private readonly IChapterRepository _chapterRepository;
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChapterRuleSegmentProvider"/> class.
    /// </summary>
    /// <param name="itemRepository">The item repository.</param>
    /// <param name="chapterRepository">The chapter repository.</param>
    /// <param name="services">
    /// Service provider, used to resolve <see cref="IMediaSegmentManager"/> lazily.
    /// It must not be taken as a constructor dependency: the manager resolves the registered
    /// segment providers, so asking for it here would close a dependency cycle and the server
    /// would fail to start.
    /// </param>
    public ChapterRuleSegmentProvider(
        IItemRepository itemRepository,
        IChapterRepository chapterRepository,
        IServiceProvider services)
    {
        _itemRepository = itemRepository;
        _chapterRepository = chapterRepository;
        _services = services;
    }

    /// <inheritdoc />
    public string Name => Plugin.Instance?.Name ?? "Chapter Rules";

    /// <inheritdoc />
    public ValueTask<bool> Supports(BaseItem item) => ValueTask.FromResult(item is Episode);

    /// <inheritdoc />
    public async Task<IReadOnlyList<MediaSegmentDto>> GetMediaSegments(
        MediaSegmentGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return _none;
        }

        if (_itemRepository.RetrieveItem(request.ItemId) is not Episode episode)
        {
            return _none;
        }

        var seriesRules = config.CalibratedSeries.FirstOrDefault(s => s.SeriesId == episode.SeriesId);
        if (seriesRules is null || seriesRules.Rules.Count == 0)
        {
            return _none;
        }

        var chapters = _chapterRepository.GetChapters(episode.Id);
        if (chapters.Count < 2)
        {
            return _none;
        }

        var covered = new HashSet<Jellyfin.Database.Implementations.Enums.MediaSegmentType>();
        if (config.OnlyFillGaps)
        {
            // Resolved here rather than injected; see the constructor for why.
            // By the time this runs the manager already exists, since it is what asks
            // providers for segments in the first place.
            if (_services.GetService(typeof(IMediaSegmentManager)) is not IMediaSegmentManager manager)
            {
                // Without it there is no way to tell whether another provider already covered
                // this episode. Staying silent risks a missing skip button; emitting anyway
                // risks a duplicate that overrides a better boundary. Prefer silence.
                return _none;
            }

            var foreign = await manager
                .GetSegmentsAsync(episode, null, _foreignOnly, false)
                .ConfigureAwait(false);

            foreach (var segment in foreign)
            {
                covered.Add(segment.Type);
            }
        }

        var sample = new EpisodeSample
        {
            Id = episode.Id,
            Duration = (episode.RunTimeTicks ?? 0) / (double)TimeSpan.TicksPerSecond,
            Chapters = [.. chapters.Select(c => c.StartPositionTicks / (double)TimeSpan.TicksPerSecond)],
        };

        if (sample.Duration <= 0)
        {
            return _none;
        }

        var segments = new List<MediaSegmentDto>();
        foreach (var rule in seriesRules.Rules)
        {
            if (!config.IsTypeEnabled(rule.Type) || covered.Contains(rule.Type))
            {
                continue;
            }

            var derived = RuleCalibrator.Apply(rule.Type, rule.Anchor, sample, config);
            if (derived is null)
            {
                continue;
            }

            segments.Add(new MediaSegmentDto
            {
                ItemId = episode.Id,
                Type = rule.Type,
                StartTicks = (long)(derived.Value.Start * TimeSpan.TicksPerSecond),
                EndTicks = (long)(derived.Value.End * TimeSpan.TicksPerSecond),
            });
        }

        return segments;
    }
}
