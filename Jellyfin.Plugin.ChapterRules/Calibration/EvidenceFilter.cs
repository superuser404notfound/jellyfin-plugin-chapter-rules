using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.MediaSegments;
using MediaBrowser.Model.Configuration;

namespace Jellyfin.Plugin.ChapterRules.Calibration;

/// <summary>
/// Builds the query options used whenever this plugin looks at what other providers produced.
/// </summary>
public static class EvidenceFilter
{
    /// <summary>
    /// Returns options that exclude this plugin's own segments.
    /// </summary>
    /// <param name="manager">The media segment manager.</param>
    /// <param name="item">Any item; used only to enumerate the registered providers.</param>
    /// <param name="ownName">This plugin's provider name.</param>
    /// <returns>Options to pass to <c>GetSegmentsAsync</c> with <c>filterByProvider: true</c>.</returns>
    /// <remarks>
    /// <para>
    /// Two things make this less obvious than it looks. Provider ids are not names — Jellyfin
    /// derives them by hashing the lowercased name — so the exclusion list has to hold the id,
    /// which is why it is asked for rather than reconstructed. And the filter is only applied
    /// when <c>filterByProvider</c> is true; passing false ignores the list entirely and returns
    /// everything, including our own output.
    /// </para>
    /// <para>
    /// Getting this wrong is not a harmless detail: our segments would count as evidence for
    /// our own rules, so a rule that drifted once would keep confirming itself.
    /// </para>
    /// </remarks>
    public static LibraryOptions ExcludingSelf(IMediaSegmentManager manager, BaseItem item, string ownName)
    {
        ArgumentNullException.ThrowIfNull(manager);

        var options = new LibraryOptions();
        foreach (var provider in manager.GetSupportedProviders(item))
        {
            if (string.Equals(provider.Name, ownName, StringComparison.OrdinalIgnoreCase))
            {
                options.DisabledMediaSegmentProviders = [provider.Id];
                break;
            }
        }

        return options;
    }
}
