using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kumunita.Core.Localization;

/// <summary>
/// The per-request read seam (ADR 0005 B; M·1–M·3, M·8, M·9). **HTTP-free**
/// (ADR 0006-D): the Web layer reads the <c>kumunita.locale</c> cookie (M·5) and passes
/// the preferred code in as a plain string — the provider never touches a cookie or
/// a claim. It resolves **platform text only** (M·3): the two content documents
/// above; it never reads a Post / PostReply / Group body.
/// </summary>
public interface ITranslationProvider
{
    /// <summary>
    /// Resolve the effective language for a request (M·1, M·9). Order:
    /// <paramref name="preferredLanguageCode"/> if present **and** enabled in
    /// <see cref="LanguageCatalog"/> → <see cref="LocaleSettings.DefaultLanguageCode"/>
    /// if enabled → <c>"en"</c> (always — the source language is seeded). Never
    /// returns null or empty.
    /// </summary>
    Task<string> ResolveEffectiveLanguageAsync(string? preferredLanguageCode);

    /// <summary>
    /// One UI string with **per-string** fallback (M·2, M·1): (key, effective) →
    /// (key, default) → (key, "en") → **the key itself** (the last-resort — a
    /// resident never sees a blank label).
    /// </summary>
    Task<string> GetAsync(string key, string? preferredLanguageCode);

    /// <summary>
    /// Batch UI strings in **one** query (the view path — no N round-trips, M·2).
    /// Each key falls back **independently** (per-string, M·2). Returns a map from
    /// each requested key to its resolved text.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetManyAsync(
        IReadOnlyCollection<string> keys, string? preferredLanguageCode);

    /// <summary>
    /// One static page with **per-page** fallback (M·2): (slug, effective) →
    /// (slug, default) → (slug, "en") → <c>null</c>. A <c>null</c> result means the
    /// page truly does not exist in **any** language (the Web renders a 404) —
    /// contrast the string path, whose floor is the key itself (M·1).
    /// </summary>
    Task<LocalizedPage?> GetPageAsync(string slug, string? preferredLanguageCode);
}
