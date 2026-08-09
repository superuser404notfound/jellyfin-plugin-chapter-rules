namespace Jellyfin.Plugin.ChapterRules.Model;

/// <summary>
/// One episode reduced to what calibration needs. Deliberately free of Jellyfin types
/// so the calibration logic can be exercised without a running server.
/// </summary>
public class EpisodeSample
{
    /// <summary>
    /// Gets or sets the episode id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the runtime in seconds.
    /// </summary>
    public double Duration { get; set; }

    /// <summary>
    /// Gets or sets the chapter start positions in seconds, ascending.
    /// </summary>
    public IReadOnlyList<double> Chapters { get; set; } = [];

    /// <summary>
    /// Gets or sets the already known segment boundaries per type, in seconds.
    /// These come from other providers and are treated as the reference to calibrate against.
    /// </summary>
    public IReadOnlyDictionary<Jellyfin.Database.Implementations.Enums.MediaSegmentType, (double Start, double End)> Known { get; set; }
        = new Dictionary<Jellyfin.Database.Implementations.Enums.MediaSegmentType, (double, double)>();
}
