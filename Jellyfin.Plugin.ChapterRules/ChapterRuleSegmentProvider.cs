using Jellyfin.Plugin.ChapterRules.Calibration;
using Jellyfin.Plugin.ChapterRules.Model;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.MediaSegments;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model;
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

    private readonly IItemRepository _itemRepository;
    private readonly IChapterRepository _chapterRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChapterRuleSegmentProvider"/> class.
    /// </summary>
    /// <param name="itemRepository">The item repository.</param>
    /// <param name="chapterRepository">The chapter repository.</param>
    public ChapterRuleSegmentProvider(IItemRepository itemRepository, IChapterRepository chapterRepository)
    {
        _itemRepository = itemRepository;
        _chapterRepository = chapterRepository;
    }

    /// <inheritdoc />
    public string Name => Plugin.Instance?.Name ?? "Chapter Rules";

    /// <inheritdoc />
    public ValueTask<bool> Supports(BaseItem item) => ValueTask.FromResult(item is Episode);

    /// <inheritdoc />
    public Task<IReadOnlyList<MediaSegmentDto>> GetMediaSegments(
        MediaSegmentGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return Task.FromResult(_none);
        }

        if (_itemRepository.RetrieveItem(request.ItemId) is not Episode episode)
        {
            return Task.FromResult(_none);
        }

        var seriesRules = config.CalibratedSeries.FirstOrDefault(s => s.SeriesId == episode.SeriesId);
        if (seriesRules is null || seriesRules.Rules.Count == 0)
        {
            return Task.FromResult(_none);
        }

        var chapters = _chapterRepository.GetChapters(episode.Id);
        if (chapters.Count < 2)
        {
            return Task.FromResult(_none);
        }

        var sample = new EpisodeSample
        {
            Id = episode.Id,
            Duration = (episode.RunTimeTicks ?? 0) / (double)TimeSpan.TicksPerSecond,
            Chapters = [.. chapters.Select(c => c.StartPositionTicks / (double)TimeSpan.TicksPerSecond)],
        };

        if (sample.Duration <= 0)
        {
            return Task.FromResult(_none);
        }

        var segments = new List<MediaSegmentDto>();
        foreach (var rule in seriesRules.Rules)
        {
            if (!config.IsTypeEnabled(rule.Type))
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

        return Task.FromResult<IReadOnlyList<MediaSegmentDto>>(segments);
    }
}
