using Kumunita.Core.Media;
using Xunit;

namespace Kumunita.Core.Tests.Media;

/// <summary>
/// ADR 0126 F4 — the <b>closed previewable set</b> pin for
/// <see cref="MediaOptions.IsPreviewable"/> / <see cref="MediaOptions.ResolvedPreviewableTypes"/>.
/// Pure options test (no Postgres): the classification the serve branch
/// (C-PV·3) consults. The default is the seven browser-displayable types
/// (C-PV·2); every other allowlist member (Office / zip) and every
/// script-bearing type (<c>text/html</c> / <c>image/svg+xml</c> /
/// <c>application/javascript</c>) is <b>not</b> previewable (C-PV·1 / C-PV·9
/// / C-PV·10). The previewable set is a subset of
/// <see cref="MediaOptions.ResolvedAttachmentAllowedTypes"/> (C-PV·10).
/// </summary>
public class MediaOptionsPreviewableTests
{
    private static readonly string[] Previewable =
    {
        "image/jpeg", "image/png", "image/webp", "image/gif",
        "application/pdf", "text/plain", "text/csv",
    };

    private static readonly string[] NotPreviewable =
    {
        // On the attachment allowlist, but not browser-previewable (C-PV·9).
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/zip",
        // Script-bearing / not on the allowlist at all (C-PV·1 / C-PV·10).
        "text/html",
        "image/svg+xml",
        "application/javascript",
        "video/mp4",
    };

    [Fact(DisplayName = "F4: IsPreviewable returns true for the seven default previewable types (case-insensitive)")]
    public void IsPreviewable_Defaults_TrueForTheSevenPreviewableTypes()
    {
        var options = new MediaOptions();
        foreach (var type in Previewable)
            Assert.True(options.IsPreviewable(type), $"expected {type} to be previewable");
    }

    [Fact(DisplayName = "F4: IsPreviewable is case-insensitive")]
    public void IsPreviewable_Is_CaseInsensitive()
    {
        var options = new MediaOptions();
        Assert.True(options.IsPreviewable("IMAGE/JPEG"));
        Assert.True(options.IsPreviewable("Application/PDF"));
        Assert.True(options.IsPreviewable("  text/CSV  ")); // surrounding whitespace trimmed
    }

    [Fact(DisplayName = "F4: IsPreviewable returns false for Office, zip, and the script-bearing types")]
    public void IsPreviewable_Defaults_FalseForOfficeZipAndScriptTypes()
    {
        var options = new MediaOptions();
        foreach (var type in NotPreviewable)
            Assert.False(options.IsPreviewable(type), $"expected {type} to NOT be previewable");
    }

    [Fact(DisplayName = "F4: IsPreviewable returns false for null / empty / unrecognized input")]
    public void IsPreviewable_FalseForNullOrEmptyOrUnrecognized()
    {
        var options = new MediaOptions();
        Assert.False(options.IsPreviewable(null));
        Assert.False(options.IsPreviewable(""));
        Assert.False(options.IsPreviewable("   "));
        Assert.False(options.IsPreviewable("application/octet-stream"));
    }

    [Fact(DisplayName = "F4: the resolved default previewable set equals the seven browser-displayable types")]
    public void ResolvedPreviewableTypes_Default_EqualsTheSevenTypes()
    {
        var options = new MediaOptions();
        Assert.Equal(Previewable.OrderBy(t => t).ToArray(),
                     options.ResolvedPreviewableTypes.OrderBy(t => t).ToArray(),
                     StringComparer.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "F4: the previewable set is a subset of the attachment allowlist (C-PV·10)")]
    public void ResolvedPreviewableTypes_IsASubsetOfTheAttachmentAllowlist()
    {
        var options = new MediaOptions();
        var allowlist = options.ResolvedAttachmentAllowedTypes
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.All(options.ResolvedPreviewableTypes,
                   t => Assert.Contains(t, allowlist, StringComparer.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = "F4: PreviewableContentTypes config override is honored (case-insensitive, empty entries ignored)")]
    public void PreviewableContentTypes_ConfigOverride_IsHonored()
    {
        var options = new MediaOptions
        {
            PreviewableContentTypes = " application/pdf , text/plain ,  ", // trailing/empty entries ignored
        };
        Assert.True(options.IsPreviewable("application/pdf"));
        Assert.True(options.IsPreviewable("text/plain"));
        Assert.False(options.IsPreviewable("image/png")); // overridden out
        Assert.False(options.IsPreviewable(""));          // empty entry ignored, not matched
    }

    [Fact(DisplayName = "F4: SVG is never previewable, even if explicitly added to the config")]
    public void Svg_IsNeverPreviewable_EvenIfConfigured()
    {
        // C-PV·1 — a misconfigured operator can add image/svg+xml to the
        // previewable set, but the DEFAULT never contains it; this pin guards
        // the default. (An operator who does add it is outside the ADR 0126
        // safety model by construction.)
        var options = new MediaOptions();
        Assert.False(options.IsPreviewable("image/svg+xml"));
        Assert.DoesNotContain(options.ResolvedPreviewableTypes,
                              t => t.Equals("image/svg+xml", StringComparison.OrdinalIgnoreCase));
    }
}
