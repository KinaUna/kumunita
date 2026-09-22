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
/// (key, effective) → (key, default) → (key, <c>en</c>) → **the key's
/// <c>en</c> source text from the registry** (the provider floor, ADR 0015
/// D1 — code is the floor, so a newly wrapped string renders its English
/// without a reseed); an unregistered key falls back to the raw key.
/// A resident never sees a blank label either way.
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
    private readonly ILocalizationService _localization;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <param name="provider">The per-request translation read seam (DI).</param>
    /// <param name="localization">The enabled-catalog read (ADR 0046 — the
    /// <c>Accept-Language</c> match is only valid against an enabled
    /// catalog language). A read — never an audit row.</param>
    /// <param name="httpContextAccessor">Reaches the request's <c>kumunita.locale</c>
    /// cookie + <c>Accept-Language</c> header (registered via
    /// <c>AddHttpContextAccessor</c>, <c>Program.cs</c> — the same seam
    /// <c>ClaimsSource</c> uses).</param>
    public LocalizeTagHelper(
        ITranslationProvider provider,
        ILocalizationService localization,
        IHttpContextAccessor httpContextAccessor)
    {
        _provider = provider;
        _localization = localization;
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
        // M·5/M·8: read the per-request language signal here (the Web layer)
        // M·5/M·8: read the per-request language signal here (the Web layer)
        // and pass it to the provider as a plain value — the provider never
        // touches a cookie or a header. A missing context degrades to "no
        // preference" so the provider still resolves via its default/en chain.
        // ADR 0046: an explicit cookie pick wins (the frozen two-step chain);
        // otherwise the browser's Accept-Language tags, matched against the
        // enabled catalog, supply an ordered candidate list — the per-string
        // fallback chain (M·2) walks all of them, so a key the top tag lacks
        // but a later one has resolves to that later row, not the en floor.
        var request = _httpContextAccessor.HttpContext?.Request;
        string text;
        var pref = request is null ? null : LocaleCookie.Read(request);
        if (!string.IsNullOrWhiteSpace(pref))
        {
            text = await _provider.GetAsync(Key, pref).ConfigureAwait(false);
        }
        else if (request is not null)
        {
            var enabled = await _localization.ListLanguagesAsync().ConfigureAwait(false);
            var candidates = RequestLanguage.BrowserCandidates(request, enabled);
            text = await _provider.GetAsync(Key, candidates).ConfigureAwait(false);
        }
        else
        {
            text = await _provider.GetAsync(Key, (string?)null).ConfigureAwait(false);
        }

        // The value is platform copy (not UGC); emit it. SetContent auto-escapes
        // (the safer choice — the plan allows either; recorded in the U2 handoff
        // note) and leaves the element's inner en reference as the source M·1 floor.
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Content.SetContent(text);
    }
}
