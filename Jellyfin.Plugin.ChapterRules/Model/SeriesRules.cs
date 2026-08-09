using System.Collections.ObjectModel;

namespace Jellyfin.Plugin.ChapterRules.Model;

/// <summary>
/// The set of calibrated rules for one series.
/// </summary>
public class SeriesRules
{
    /// <summary>
    /// Gets or sets the series id.
    /// </summary>
    public Guid SeriesId { get; set; }

    /// <summary>
    /// Gets or sets the series name. Informational only; the id is authoritative.
    /// </summary>
    public string SeriesName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp of the last calibration run for this series.
    /// </summary>
    public DateTime CalibratedAt { get; set; }

    /// <summary>
    /// Gets the rules that passed calibration, at most one per segment type.
    /// </summary>
    public Collection<ChapterRule> Rules { get; } = [];
}
