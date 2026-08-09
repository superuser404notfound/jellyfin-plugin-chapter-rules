using Jellyfin.Database.Implementations.Enums;

namespace Jellyfin.Plugin.ChapterRules.Model;

/// <summary>
/// A rule that derives one segment type from the position of a chapter marker.
/// </summary>
/// <remarks>
/// <para>
/// The anchor selects a chapter by index. Non-negative values count from the start of the
/// episode (<c>0</c> is the first chapter), negative values count from the end
/// (<c>-1</c> is the last chapter). How the anchor becomes a segment depends on the type:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="MediaSegmentType.Outro"/>: anchor to end of file.</description></item>
/// <item><description><see cref="MediaSegmentType.Recap"/>: start of file to anchor.</description></item>
/// <item><description><see cref="MediaSegmentType.Intro"/>: anchor to the following chapter.</description></item>
/// </list>
/// </remarks>
public class ChapterRule
{
    /// <summary>
    /// Gets or sets the segment type this rule produces.
    /// </summary>
    public MediaSegmentType Type { get; set; }

    /// <summary>
    /// Gets or sets the chapter index the rule anchors on. Negative values count from the end.
    /// </summary>
    public int Anchor { get; set; }

    /// <summary>
    /// Gets or sets the share of comparison samples the rule reproduced, between 0 and 1.
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Gets or sets the number of episodes the confidence was measured against.
    /// </summary>
    public int Samples { get; set; }

    /// <summary>
    /// Gets or sets the 90th-percentile deviation from the reference boundary, in seconds.
    /// Low values mean the rule agrees closely even where it does not agree exactly.
    /// </summary>
    public double P90DeviationSeconds { get; set; }

    /// <summary>
    /// Gets or sets the number of episodes where the rule applies but no segment exists yet.
    /// </summary>
    public int Gaps { get; set; }
}
