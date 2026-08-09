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
    public double MinimumConfidence { get; set; } = 0.9;

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
