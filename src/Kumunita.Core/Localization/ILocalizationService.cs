using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kumunita.Core.Localization;

/// <summary>
/// The admin-management seam (ADR 0005 D; M·4, M·6, M·7). **HTTP-free**
/// (ADR 0006-D): the Web `LanguagesController` (U5) is a thin GlobalAdmin-gated
/// surface over this. Every **mutating** method appends **exactly one**
/// <c>AccessAudit</c> row (M·6, the UserInfoService admin-action idiom —
/// ARCHITECTURE.md §5): <see cref="Kumunita.Core.Authorization.AccessVia.Admin"/>,
/// <c>Outcome = Allow</c>, <c>ActorId</c> = the GlobalAdmin, and the <c>Action</c>
/// / <c>TargetKind</c> pinned per method below. <see cref="LanguageCompleteness"/>
/// (M·12 FACES) is the read result of the completeness view.
/// </summary>
public interface ILocalizationService
{
    // ── Catalog — TargetKind "language" ─────────────────────────────
    Task<IReadOnlyList<LanguageCatalog>> ListLanguagesAsync();

    /// <summary>Adds a language (BCP-47 code + native name) — audited
    /// <c>language.add</c>, TargetKind "language", TargetId = code (M·6).</summary>
    Task AddLanguageAsync(string code, string nativeName, string actorId);

    /// <summary>Enables / disables a language — audited <c>language.enable</c> /
    /// <c>language.disable</c>, TargetId = code (M·6).</summary>
    Task SetLanguageEnabledAsync(string code, bool enabled, string actorId);

    /// <summary>Reorders the catalog by <c>sortOrder</c> — audited
    /// <c>language.reorder</c> (M·6).</summary>
    Task ReorderLanguagesAsync(IReadOnlyList<string> codesInOrder, string actorId);

    /// <summary>
    /// Removes a language — audited <c>language.remove</c>, TargetId = code (M·6).
    /// <see cref="LocalizedPage"/> rows for the code are **retained** (M·7).
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// <paramref name="code"/> is the current
    /// <see cref="LocaleSettings.DefaultLanguageCode"/> — fail-closed, **no audit
    /// row** is written for the blocked attempt (M·7; M11 FACES).</exception>
    Task RemoveLanguageAsync(string code, string actorId);

    /// <summary>Sets the instance default — audited <c>language.set-default</c>,
    /// TargetId = code (M·6; M10 FACES).</summary>
    Task SetDefaultLanguageAsync(string code, string actorId);

    // ── UI strings — TargetKind "translation" ───────────────────────
    Task<TranslationResource?> GetTranslationAsync(string key, string languageCode);

    /// <summary>
    /// Batch read (M·4 read path — live rows, no projection, no audit: it is a
    /// read). Returns **every** stored <see cref="TranslationResource"/> row for
    /// <paramref name="languageCode"/> as a <c>key → text</c> map — **no
    /// fallback** (the provider's M·2 per-string fallback is the *resident's*
    /// read path, not an editor's). A code with no rows returns an **empty**
    /// map, never null. One query, one round-trip (M·2 "no N round-trips").
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetTranslationsForAsync(string languageCode);

    /// <summary>Upserts one UI string — audited <c>translation.save</c>,
    /// TargetId = key (M·6; M9 FACES). Takes effect on the next request (M·4).</summary>
    Task UpsertTranslationAsync(string key, string languageCode, string text, string actorId);

    // ── Static pages — TargetKind "localized_page" ──────────────────
    Task<LocalizedPage?> GetPageAsync(string slug, string languageCode);

    /// <summary>Upserts one static page — audited <c>page.save</c>, TargetId =
    /// slug (M·6). <see cref="LocalizedPage.Updated"/> is set to server time.</summary>
    Task UpsertPageAsync(string slug, string languageCode, string title, string body, string actorId);

    // ── Completeness — read ─────────────────────────────────────────
    /// <summary>The per-language completeness view (M·12 FACES) — which UI keys and
    /// pages are present vs. missing for a language.</summary>
    Task<LanguageCompleteness> GetCompletenessAsync(string languageCode);
}
