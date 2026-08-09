using System.Collections.ObjectModel;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.ChapterRules.Model;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ChapterRules.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether intro segments may be derived from chapters.
    /// </summary>
    public bool EnableIntro { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether recap segments may be derived from chapters.
    /// </summary>
    public bool EnableRecap { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether outro segments may be derived from chapters.
    /// </summary>
    public bool EnableOutro { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether a segment type is skipped for an episode when
    /// another provider already supplies it.
    /// </summary>
    /// <remarks>
    /// On by default. Detection based providers usually place a boundary a few seconds earlier
    /// than the chapter marker does, at the start of the fade rather than where the credits
    /// card appears, which is the nicer place to skip from. Overwriting that would be a
    /// regression, so the rule only fills what is missing.
    /// </remarks>
    public bool OnlyFillGaps { get; set; } = true;

    /// <summary>
    /// Gets or sets how far a derived boundary may sit from a known one and still count as
    /// agreement during calibration, in seconds.
    /// </summary>
    public double ToleranceSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets the share of comparison samples a rule must reproduce before it is used.
    /// </summary>
    /// <remarks>
    /// This alone is a poor gate, which is why <see cref="MaximumDeviationSeconds"/> exists.
    /// Two rules can agree on a near-identical share of samples and be worlds apart: one
    /// missing by seconds because the reference itself is fuzzy, the other missing by the
    /// better part of a minute because it is anchored on the wrong chapter.
    /// </remarks>
    public double MinimumConfidence { get; set; } = 0.75;

    /// <summary>
    /// Gets or sets the largest 90th-percentile deviation from the reference a rule may have,
    /// in seconds. Hits count as zero deviation, so this is a statement about the whole
    /// distribution: at least nine in ten episodes must land within this of where the existing
    /// segment says the boundary is.
    /// </summary>
    /// <remarks>
    /// The default is twice <see cref="ToleranceSeconds"/>. A rule that is correct but compared
    /// against imprecise references sits just above the tolerance; a rule anchored on the wrong
    /// chapter sits far above it, and no confidence threshold separates the two.
    /// </remarks>
    public double MaximumDeviationSeconds { get; set; } = 20;

    /// <summary>
    /// Gets or sets how many known segments a series must have before a rule is trusted.
    /// Series below this threshold are reported but never written to.
    /// </summary>
    public int MinimumSamples { get; set; } = 5;

    /// <summary>Gets or sets the shortest plausible intro, in seconds.</summary>
    public double MinIntroSeconds { get; set; } = 15;

    /// <summary>Gets or sets the longest plausible intro, in seconds.</summary>
    public double MaxIntroSeconds { get; set; } = 180;

    /// <summary>Gets or sets the shortest plausible recap, in seconds.</summary>
    public double MinRecapSeconds { get; set; } = 15;

    /// <summary>Gets or sets the longest plausible recap, in seconds.</summary>
    public double MaxRecapSeconds { get; set; } = 120;

    /// <summary>Gets or sets the shortest plausible outro, in seconds.</summary>
    public double MinOutroSeconds { get; set; } = 10;

    /// <summary>Gets or sets the longest plausible outro, in seconds.</summary>
    public double MaxOutroSeconds { get; set; } = 300;

    /// <summary>
    /// Gets the rules accepted by the most recent calibration run, one entry per series.
    /// </summary>
    public Collection<SeriesRules> CalibratedSeries { get; } = [];

    /// <summary>
    /// Returns whether the given segment type may be produced.
    /// </summary>
    /// <param name="type">The segment type.</param>
    /// <returns><see langword="true"/> when enabled.</returns>
    public bool IsTypeEnabled(MediaSegmentType type) => type switch
    {
        MediaSegmentType.Intro => EnableIntro,
        MediaSegmentType.Recap => EnableRecap,
        MediaSegmentType.Outro => EnableOutro,
        _ => false,
    };

    /// <summary>
    /// Returns the plausible length window for the given segment type.
    /// </summary>
    /// <param name="type">The segment type.</param>
    /// <returns>Minimum and maximum length in seconds.</returns>
    public (double Min, double Max) WindowFor(MediaSegmentType type) => type switch
    {
        MediaSegmentType.Intro => (MinIntroSeconds, MaxIntroSeconds),
        MediaSegmentType.Recap => (MinRecapSeconds, MaxRecapSeconds),
        MediaSegmentType.Outro => (MinOutroSeconds, MaxOutroSeconds),
        _ => (0, double.MaxValue),
    };
}
