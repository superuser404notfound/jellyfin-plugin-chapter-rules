using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.ChapterRules.Calibration;
using Jellyfin.Plugin.ChapterRules.Configuration;
using Jellyfin.Plugin.ChapterRules.Model;
using Xunit;

namespace Jellyfin.Plugin.ChapterRules.Tests;

/// <summary>
/// The calibrator is the only part of this plugin that makes a judgement call, so it is the
/// part worth pinning down. The cases below are modelled on real series: the numbers in
/// <see cref="HighAgreementButSystematicallyOffIsRejected"/> and
/// <see cref="SlightlyLowerAgreementButCloseMissesIsAccepted"/> are what made the deviation
/// guard necessary in the first place.
/// </summary>
public class RuleCalibratorTests
{
    private static PluginConfiguration Config() => new();

    /// <summary>
    /// Builds an episode whose last chapter sits <paramref name="creditsFromEnd"/> before the end,
    /// and which optionally carries a known outro starting <paramref name="knownOffset"/> away
    /// from that chapter.
    /// </summary>
    private static EpisodeSample Episode(
        double duration = 1400,
        double creditsFromEnd = 30,
        double? knownOffset = 0)
    {
        var lastChapter = duration - creditsFromEnd;
        var known = new Dictionary<MediaSegmentType, (double Start, double End)>();
        if (knownOffset is not null)
        {
            known[MediaSegmentType.Outro] = (lastChapter - knownOffset.Value, duration);
        }

        return new EpisodeSample
        {
            Id = Guid.NewGuid(),
            Duration = duration,
            Chapters = [0, 60, 400, 800, lastChapter],
            Known = known,
        };
    }

    [Fact]
    public void PerfectAgreementIsAccepted()
    {
        var episodes = Enumerable.Range(0, 20).Select(_ => Episode()).ToList();

        var rules = RuleCalibrator.Calibrate(episodes, Config());

        var outro = Assert.Single(rules, r => r.Type == MediaSegmentType.Outro);
        Assert.Equal(-1, outro.Anchor);
        Assert.Equal(1.0, outro.Confidence);
        Assert.Equal(20, outro.Samples);
        Assert.Equal(0, outro.P90DeviationSeconds);
    }

    [Fact]
    public void HighAgreementButSystematicallyOffIsRejected()
    {
        // How I Met Your Mother: 83 % of episodes agree, but where the rule misses it misses
        // by roughly 47 seconds every time — the anchor is on the wrong chapter, not fuzzy.
        var episodes = Enumerable.Range(0, 100)
            .Select(i => Episode(knownOffset: i < 83 ? 0 : 47))
            .ToList();

        var rules = RuleCalibrator.Calibrate(episodes, Config());

        Assert.DoesNotContain(rules, r => r.Type == MediaSegmentType.Outro);
    }

    [Fact]
    public void SlightlyLowerAgreementButCloseMissesIsAccepted()
    {
        // Vampire Diaries: fewer episodes agree exactly, but the misses are ~13 seconds,
        // which is the reference being imprecise rather than the rule being wrong.
        var episodes = Enumerable.Range(0, 100)
            .Select(i => Episode(knownOffset: i < 80 ? 0 : 13))
            .ToList();

        var rules = RuleCalibrator.Calibrate(episodes, Config());

        var outro = Assert.Single(rules, r => r.Type == MediaSegmentType.Outro);
        Assert.InRange(outro.Confidence, 0.75, 0.85);
        Assert.InRange(outro.P90DeviationSeconds, 0, 20);
    }

    [Fact]
    public void TooFewSamplesIsRejectedEvenWhenPerfect()
    {
        var episodes = Enumerable.Range(0, 3).Select(_ => Episode()).ToList();

        var rules = RuleCalibrator.Calibrate(episodes, Config());

        Assert.DoesNotContain(rules, r => r.Type == MediaSegmentType.Outro);
    }

    [Fact]
    public void EpisodesWithoutAKnownSegmentAreCountedAsGapsNotEvidence()
    {
        var episodes = Enumerable.Range(0, 20).Select(_ => Episode()).ToList();
        episodes.AddRange(Enumerable.Range(0, 7).Select(_ => Episode(knownOffset: null)));

        var rules = RuleCalibrator.Calibrate(episodes, Config());

        var outro = Assert.Single(rules, r => r.Type == MediaSegmentType.Outro);
        Assert.Equal(20, outro.Samples);
        Assert.Equal(7, outro.Gaps);
    }

    [Fact]
    public void ImplausiblyLongOutroIsNotDerived()
    {
        // Last chapter eight minutes before the end: this is the act break that started the
        // whole exercise, and the plausibility window is what keeps it out.
        var episode = Episode(creditsFromEnd: 480);

        var derived = RuleCalibrator.Apply(MediaSegmentType.Outro, -1, episode, Config());

        Assert.Null(derived);
    }

    [Fact]
    public void NegativeAnchorsCountFromTheEnd()
    {
        var episode = Episode(duration: 1400, creditsFromEnd: 30, knownOffset: null);

        var last = RuleCalibrator.Apply(MediaSegmentType.Outro, -1, episode, Config());

        Assert.NotNull(last);
        Assert.Equal(1370, last!.Value.Start);
        Assert.Equal(1400, last.Value.End);
    }

    [Fact]
    public void RecapRunsFromTheStartToItsAnchor()
    {
        var episode = new EpisodeSample
        {
            Duration = 2400,
            Chapters = [0, 50, 300, 2380],
            Known = new Dictionary<MediaSegmentType, (double, double)>(),
        };

        var recap = RuleCalibrator.Apply(MediaSegmentType.Recap, 1, episode, Config());

        Assert.NotNull(recap);
        Assert.Equal(0, recap!.Value.Start);
        Assert.Equal(50, recap.Value.End);
    }

    [Theory]
    [InlineData(new double[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 100 }, 0.9, 0)]
    [InlineData(new double[] { 0, 0, 0, 0, 0, 0, 0, 0, 100, 100 }, 0.9, 100)]
    [InlineData(new double[] { 5 }, 0.9, 5)]
    public void PercentileUsesNearestRank(double[] values, double fraction, double expected)
    {
        Assert.Equal(expected, RuleCalibrator.Percentile(values, fraction));
    }

    [Fact]
    public void PercentileOfNothingIsZero()
    {
        Assert.Equal(0, RuleCalibrator.Percentile([], 0.9));
    }
}
