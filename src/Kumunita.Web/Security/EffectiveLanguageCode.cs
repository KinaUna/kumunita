using Kumunita.Core.Localization;
using Microsoft.AspNetCore.Http;

namespace Kumunita.Web.Security;

/// <summary>
/// The per-request **effective language code** a resident is rendering in
/// (ADR 0049) — the *code* half of the exact chain the <c>&lt;kw-l&gt;</c>
/// TagHelper (<see cref="Kumunita.Web.TagHelpers.LocalizeTagHelper"/>) resolves
/// UI strings through (M·5/M·8, ADR 0046):
/// <list type="number">
/// <item>the <c>kumunita.locale</c> preference cookie (an explicit pick —
/// <see cref="RequestLanguage.Preference"/>);</item>
/// <item>the first enabled <c>Accept-Language</c> match (a suggestion, never
/// persisted — ADR 0046);</item>
/// <item>the instance default → <c>en</c> floor (the provider's frozen chain,
/// M·1/M·9 — <see cref="ITranslationProvider.ResolveEffectiveLanguageAsync(string?)"/>
/// with a <c>null</c> preference).</item>
/// </list>
/// </summary>
/// <remarks>
/// **Why a helper (and not the provider's string methods).** The TagHelper and
/// the provider return *resolved strings*; ADR 0049 needs the *code* so a
/// content surface can pick which of an item's pre-rendered language
/// <em>variants</em> (ADR 0027) is default-visible. The three-step chain is the
/// same read the platform already performs for UI text, so the "current
/// language" a resident is reading the platform in is the "current language"
/// used to auto-select their post/announcement translation variant — one
/// notion of "the resident's language", no second, divergent signal.
/// <para>
/// **HTTP-free Core (M·8) holds:** this lives in the Web layer and passes plain
/// BCP-47 values to the Core <see cref="ITranslationProvider"/> /
/// <see cref="ILocalizationService"/> reads — Core never reads a header or a
/// cookie (the same seam as <see cref="RequestLanguage"/>).
/// </para>
/// <para>
/// **Fail-open to a concrete code.** The provider's floor is the instance
/// default → <c>en</c> (M·1: never null/empty), so this method always returns
/// a usable BCP-47 code even for a request with no cookie, no
/// <c>Accept-Language</c>, and an unseeded instance. A code that no enabled
/// catalog row has is harmless: the caller only ever compares it (case-
/// insensitively) against the codes an item *actually* has a translation in,
/// so a non-matching code simply leaves the authored-in variant default-visible.
/// </para>
/// </remarks>
public static class EffectiveLanguageCode
{
    /// <summary>
    /// Resolves the per-request effective language **code** (ADR 0049) — the
    /// cookie preference if present, else the first enabled
    /// <c>Accept-Language</c> match, else the provider's instance-default →
    /// <c>en</c> floor. Always returns a concrete, **enabled** BCP-47 code —
    /// the same code the <c>&lt;kw-l&gt;</c> TagHelper resolves UI strings in
    /// for this request, so the "current language" used to auto-select a
    /// post/announcement variant is exactly the language the resident is
    /// reading the platform in.
    /// </summary>
    /// <param name="request">The in-flight <see cref="HttpRequest"/> (the
    /// <c>kumunita.locale</c> cookie + <c>Accept-Language</c> source). May be
    /// <c>null</c> (no context — e.g. a non-HTTP call site); the cookie and
    /// <c>Accept-Language</c> branches are then skipped and the resolution
    /// falls to the instance-default → <c>en</c> floor, exactly as the
    /// <c>&lt;kw-l&gt;</c> TagHelper does for a missing context.</param>
    /// <param name="localization">The enabled-catalog read (ADR 0046 — the
    /// <c>Accept-Language</c> match is only valid against an enabled catalog
    /// language). A read — never an audit row.</param>
    /// <param name="provider">The per-request translation read seam (resolves
    /// each branch to an **enabled** code, falling to the instance default →
    /// <c>en</c> floor — M·1/M·9).</param>
    public static async Task<string> ResolveAsync(
        HttpRequest? request,
        ILocalizationService localization,
        ITranslationProvider provider)
    {
        // Mirror the <c>&lt;kw-l&gt;</c> TagHelper's three-branch chain exactly
        // (TagHelpers/LocalizeTagHelper.cs), but return the resolved **code**
        // rather than a string, so a content surface can pick which of an item's
        // pre-rendered variants (ADR 0027) is default-visible.
        var pref = request is null ? null : LocaleCookie.Read(request);
        if (!string.IsNullOrWhiteSpace(pref))
            return await provider.ResolveEffectiveLanguageAsync(pref).ConfigureAwait(false);

        if (request is not null)
        {
            var catalog = await localization.ListLanguagesAsync().ConfigureAwait(false);
            var candidates = RequestLanguage.BrowserCandidates(request, catalog);
            if (candidates.Count > 0)
                return await provider.ResolveEffectiveLanguageAsync(candidates).ConfigureAwait(false);
        }

        return await provider.ResolveEffectiveLanguageAsync((string?)null).ConfigureAwait(false);
    }
}
