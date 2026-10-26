using System.Net;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Kumunita.Web.TagHelpers;

/// <summary>
/// <c>&lt;kw-avatar subject="..." mono="..." /&gt;</c> — renders the one
/// avatar slot for a resident, deciding <b>server-side</b> whether to emit
/// the avatar <c>&lt;img&gt;</c> at all.
/// <para>
/// This is the single home for "does this resident have an avatar?" —
/// previously every avatar slot shipped an <c>&lt;img src="/profile/avatar/…"&gt;</c>
/// unconditionally and leaned on <c>client/lib/avatar.ts</c> to hide the
/// broken image after the browser had already made a request that the
/// serving lane
/// (<see cref="Kumunita.Web.Controllers.ProfileController.Avatar"/>, ADR
/// 0011) answers with a 404 when the resident has no avatar. The console was
/// full of those doomed fetches for every account without an avatar.
/// </para>
/// <para>
/// The helper asks <see cref="IAvatarPresenceChecker"/> (a read, never a
/// decision — the avatar lane is "all platform users", ADR 0057) before
/// writing any markup:
/// <list type="bullet">
/// <item>avatar <b>present</b> → the same <c>&lt;img&gt;</c> + hidden
/// monogram sibling the app always used (the client-side fallback stays as a
/// belt-and-suspenders for the rare render-then-delete race), inside the
/// standard flex wrapper;</item>
/// <item>avatar <b>absent</b> → just the (visible) monogram, or nothing when
/// no monogram initial is available — <b>no <c>&lt;img&gt;</c>, no request</b>.
/// </item>
/// </list>
/// The element re-tags itself to a <c>&lt;span&gt;</c> (the
/// <c>&lt;kw-upload-limit /&gt;</c> precedent, <see cref="UploadLimitTagHelper"/>),
/// so the rendered DOM is exactly the wrapper the views already had — no
/// extra <c>&lt;kw-avatar&gt;</c> element leaks into the page.
/// </para>
/// </summary>
[HtmlTargetElement("kw-avatar", Attributes = "subject")]
public sealed class AvatarTagHelper(IAvatarPresenceChecker presence) : TagHelper
{
    /// <summary>The resident's subject id (the value the serving lane
    /// <c>GET /profile/avatar/{subjectId}</c> keys on). Required.</summary>
    [HtmlAttributeName("subject")]
    public string Subject { get; set; } = string.Empty;

    /// <summary>The monogram initial to render when the avatar is absent (and,
    /// as the hidden fallback, when present).
    /// <para>
    /// Omit it entirely for a slot with no monogram channel — an absent
    /// avatar then emits <b>nothing at all</b> (the comment-avatar shape,
    /// which previously rendered a blank 404 slot). Pass it — even as
    /// <c>""</c> — for the standard monogram circle (the profile preview's
    /// "empty circle" placeholder, ADR 0011). The value is HTML-encoded
    /// before it is written, so a display name with a special character can
    /// never break out of the markup.
    /// </para></summary>
    [HtmlAttributeName("mono")]
    public string? Mono { get; set; }

    /// <summary>
    /// A stable per-resident monogram hue in <c>[0, 6)</c> (ADR 0175,
    /// <see cref="Security.AvatarHue"/>) — emitted as a <c>data-hue</c>
    /// attribute on the <c>.avatar-mono</c> circle so site.css can colour it.
    /// Presentation only; omit it (the default) for the uncoloured monogram.
    /// </summary>
    [HtmlAttributeName("mono-hue")]
    public int? MonoHue { get; set; }

    /// <summary>The size variant: <c>"lg"</c>, <c>"sm"</c>, or empty (the
    /// default). Maps to the existing <c>avatar-lg</c> /
    /// <c>avatar-sm</c> / <c>avatar-mono-lg</c> / <c>avatar-mono-sm</c>
    /// classes so the rendered avatar is visually identical to the
    /// pre-<c>kw-avatar</c> markup.</summary>
    [HtmlAttributeName("size")]
    public string Size { get; set; } = string.Empty;

    /// <summary>The wrapper <c>&lt;span&gt;</c>'s classes. Defaults to the
    /// app's standard avatar wrapper <c>d-inline-flex align-items-center</c>;
    /// override where a slot needs an extra modifier (e.g. the board card's
    /// <c>mt-1</c> or the announcement comment's <c>me-2</c>).</summary>
    [HtmlAttributeName("wrapper-class")]
    public string WrapperClass { get; set; } = "d-inline-flex align-items-center";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (string.IsNullOrEmpty(Subject))
        {
            // No subject to resolve → nothing to render (never a doomed fetch).
            output.SuppressOutput();
            return;
        }

        var present = await presence.HasAvatarAsync(Subject).ConfigureAwait(false);
        var sizeSuffix = string.IsNullOrEmpty(Size) ? string.Empty : $" avatar-{Size}";
        // `Mono` is nullable by design: a `null` Mono means "no monogram
        // channel" (an absent avatar then emits nothing); a non-null Mono
        // (even "") means "render the monogram circle" — the profile
        // preview's empty-circle placeholder. Trim the value for rendering.
        var mono = Mono is null ? null : Mono.Trim();

        if (present)
        {
            // Same shape the app always shipped for a set avatar: the <img>
            // (with the client-side data-avatar-fallback) plus the hidden
            // monogram sibling that avatar.ts swaps in on the rare
            // render-then-delete race. The presence check just guarantees the
            // normal no-avatar case never reaches this branch.
            var subjectUrl = WebUtility.HtmlEncode(Subject);
            var img = $"<img class=\"avatar{sizeSuffix}\" src=\"/profile/avatar/{subjectUrl}\" alt=\"\" data-avatar-fallback>";
            var hiddenMono = mono is null
                ? string.Empty
                : $"<span class=\"avatar-mono{sizeSuffix}\"{HueAttr} style=\"display:none;\">{WebUtility.HtmlEncode(mono)}</span>";
            RenderSlot(output, img + hiddenMono);
            return;
        }

        // Absent. A `null` Mono (no monogram channel) → emit nothing at all.
        if (mono is null)
        {
            output.SuppressOutput();
            return;
        }
        // A non-null Mono → the monogram circle stands in directly — no <img>,
        // no request. (This is the profile-preview empty-circle case, where
        // Mono is "" and the circle is the placeholder.)
        RenderSlot(output, $"<span class=\"avatar-mono{sizeSuffix}\"{HueAttr}>{WebUtility.HtmlEncode(mono)}</span>");
    }

    /// <summary>The <c>data-hue</c> attribute (ADR 0175) when a
    /// <see cref="MonoHue"/> is set and in range; empty otherwise.</summary>
    private string HueAttr =>
        MonoHue is >= 0 and < 6 ? $" data-hue=\"{MonoHue}\"" : string.Empty;

    /// <summary>Rename the element to the app's avatar wrapper
    /// <c>&lt;span&gt;</c> (honoring <see cref="WrapperClass"/>) and set its
    /// inner content as raw (pre-built) markup — mirroring how
    /// <see cref="UploadLimitTagHelper"/> re-tags <c>kw-upload-limit</c> to
    /// <c>&lt;div&gt;</c>.</summary>
    private void RenderSlot(TagHelperOutput output, string innerHtml)
    {
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", string.IsNullOrWhiteSpace(WrapperClass)
            ? "d-inline-flex align-items-center"
            : WrapperClass.Trim());
        output.Content.SetHtmlContent(innerHtml);
    }
}
