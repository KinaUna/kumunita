using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Kumunita.Core.Localization;
using Kumunita.Web.Security;

namespace Kumunita.Web.TagHelpers;

/// <summary>
/// The &lt;kw-l&gt; view-localization TagHelper (the <c>ML-UI</c> U2 deliverable —
/// D1, the TagHelper decision, settled in the lane plan and recorded in ADR 0015
/// at U9).
/// <para>
/// Emits one platform UI string resolved through
/// <see cref="ITranslationProvider"/> (the frozen <c>ML</c> read seam) for the
/// request's preferred language. The preference is read from the
/// <c>kumunita.locale</c> cookie via <see cref="LocaleCookie.Read"/> (M·5) and
/// passed to the provider as a plain BCP-47 string — Core stays HTTP-free
/// (M·8). Resolution order is the provider's per-string chain (M·1/M·2):
/// (key, effective) → (key, default) → (key, <c>en</c>) → **the key itself** —
/// a resident never sees a blank label.
/// </para>
/// <para>
/// <b>Platform text only (M·3).</b> The TagHelper is used in the in-scope
/// layout/page views (U2–U5) for the curated <see cref="KnownTranslationKeys"/>
/// set. It must <b>never</b> wrap a UGC value (a post/reply body, a group
/// description, an announcement body, or an author's display name) — those are
/// rendered as authored.
/// </para>
/// <para>
/// <b>Graceful floor.</b> A blank <see cref="Key"/> resolves to
/// <see cref="ITranslationProvider.GetAsync"/>'s last-resort — the key itself
/// (here, the empty string) — rather than throwing (M·1).
/// </para>
/// </summary>
[HtmlTargetElement("kw-l", Attributes = "key")]
public sealed class LocalizeTagHelper : TagHelper
{
    private readonly ITranslationProvider _provider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <param name="provider">The per-request translation read seam (DI).</param>
    /// <param name="httpContextAccessor">Reaches the request's <c>kumunita.locale</c>
    /// cookie (registered via <c>AddHttpContextAccessor</c>, <c>Program.cs</c> — the
    /// same seam <c>ClaimsSource</c> uses).</param>
    public LocalizeTagHelper(ITranslationProvider provider, IHttpContextAccessor httpContextAccessor)
    {
        _provider = provider;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>The translation key to resolve (a <see cref="KnownTranslationKeys"/>
    /// key — e.g. <c>nav.home</c>). The element's inner text is the human-readable
    /// <c>en</c> reference (the M·1 floor if the provider is ever absent), but the
    /// resolved value emitted here is what the resident sees.</summary>
    [HtmlAttributeName]
    public string Key { get; set; } = "";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        // M·5/M·8: read the preference cookie here (the Web layer) and pass it as a
        // plain string to the provider — the provider never touches a cookie. A
        // missing context degrades to "no preference" (null) so the provider still
        // resolves via its default/en chain — never a throw.
        var request = _httpContextAccessor.HttpContext?.Request;
        var pref = request is null ? null : LocaleCookie.Read(request);

        // M·1/M·2: per-string fallback chain, last-resort the key itself (a blank
        // Key yields the empty string — never a blank label, never a throw).
        var text = await _provider.GetAsync(Key, pref);

        // The value is platform copy (not UGC); emit it. SetContent auto-escapes
        // (the safer choice — the plan allows either; recorded in the U2 handoff
        // note) and leaves the element's inner en reference as the source M·1 floor.
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Content.SetContent(text);
    }
}
