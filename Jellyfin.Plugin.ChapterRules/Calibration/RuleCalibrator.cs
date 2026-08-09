using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.ChapterRules.Configuration;
using Jellyfin.Plugin.ChapterRules.Model;

namespace Jellyfin.Plugin.ChapterRules.Calibration;

/// <summary>
/// Works out which chapter position corresponds to which segment type for a given series,
/// by replaying candidate rules against segments that are already known to be correct.
/// </summary>
/// <remarks>
/// A rule is only ever accepted on evidence. If a series has too few known segments to
/// compare against, or the best candidate disagrees too often, no rule is emitted for that
/// type — the plugin stays silent rather than guessing.
/// </remarks>
public static class RuleCalibrator
{
    /// <summary>
    /// Candidate anchors per segment type, in the order they are tried.
    /// </summary>
    private static readonly IReadOnlyDictionary<MediaSegmentType, int[]> _candidates =
        new Dictionary<MediaSegmentType, int[]>
        {
            [MediaSegmentType.Outro] = [-1, -2],
            [MediaSegmentType.Recap] = [1, 2],
            [MediaSegmentType.Intro] = [0, 1, 2, 3, 4],
        };

    /// <summary>
    /// Resolves an anchor index against a chapter list.
    /// </summary>
    /// <param name="anchor">The anchor; negative values count from the end.</param>
    /// <param name="count">The number of chapters.</param>
    /// <returns>The resolved index, or -1 when the anchor falls outside the list.</returns>
    private static int Resolve(int anchor, int count)
    {
        var index = anchor >= 0 ? anchor : count + anchor;
        return index >= 0 && index < count ? index : -1;
    }

    /// <summary>
    /// Applies a rule to one episode.
    /// </summary>
    /// <param name="type">The segment type.</param>
    /// <param name="anchor">The chapter anchor.</param>
    /// <param name="episode">The episode.</param>
    /// <param name="config">Plugin configuration supplying the plausibility windows.</param>
    /// <returns>The derived segment, or <see langword="null"/> when the rule does not apply.</returns>
    public static (double Start, double End)? Apply(
        MediaSegmentType type,
        int anchor,
        EpisodeSample episode,
        PluginConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentNullException.ThrowIfNull(config);

        var chapters = episode.Chapters;
        var index = Resolve(anchor, chapters.Count);
        if (index < 0)
        {
            return null;
        }

        double start, end;
        switch (type)
        {
            case MediaSegmentType.Outro:
                start = chapters[index];
                end = episode.Duration;
                break;

            case MediaSegmentType.Recap:
                start = 0;
                end = chapters[index];
                break;

            case MediaSegmentType.Intro:
                if (index + 1 >= chapters.Count)
                {
                    return null;
                }

                start = chapters[index];
                end = chapters[index + 1];
                break;

            default:
                return null;
        }

        var length = end - start;
        var (min, max) = config.WindowFor(type);
        if (length < min || length > max || end > episode.Duration + 1)
        {
            return null;
        }

        return (start, end);
    }

    /// <summary>
    /// Calibrates every supported segment type for one series.
    /// </summary>
    /// <param name="episodes">The episodes of the series.</param>
    /// <param name="config">Plugin configuration.</param>
    /// <returns>The rules that met the confidence and sample thresholds.</returns>
    public static IReadOnlyList<ChapterRule> Calibrate(
        IReadOnlyList<EpisodeSample> episodes,
        PluginConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(episodes);
        ArgumentNullException.ThrowIfNull(config);

        var accepted = new List<ChapterRule>();

        foreach (var (type, anchors) in _candidates)
        {
            if (!config.IsTypeEnabled(type))
            {
                continue;
            }

            ChapterRule? best = null;

            foreach (var anchor in anchors)
            {
                int hits = 0, samples = 0, gaps = 0;

                foreach (var episode in episodes)
                {
                    var derived = Apply(type, anchor, episode, config);
                    if (derived is null)
                    {
                        continue;
                    }

                    if (episode.Known.TryGetValue(type, out var known))
                    {
                        samples++;

                        // Compare on the boundary the rule actually predicts: an outro rule
                        // predicts where the credits start, a recap rule where they end.
                        var predicted = type == MediaSegmentType.Recap ? derived.Value.End : derived.Value.Start;
                        var actual = type == MediaSegmentType.Recap ? known.End : known.Start;

                        if (Math.Abs(predicted - actual) <= config.ToleranceSeconds)
                        {
                            hits++;
                        }
                    }
                    else
                    {
                        gaps++;
                    }
                }

                if (samples == 0)
                {
                    continue;
                }

                var confidence = (double)hits / samples;
                if (best is null || confidence > best.Confidence)
                {
                    best = new ChapterRule
                    {
                        Type = type,
                        Anchor = anchor,
                        Confidence = confidence,
                        Samples = samples,
                        Gaps = gaps,
                    };
                }
            }

            if (best is not null
                && best.Samples >= config.MinimumSamples
                && best.Confidence >= config.MinimumConfidence)
            {
                accepted.Add(best);
            }
        }

        return accepted;
    }
}
