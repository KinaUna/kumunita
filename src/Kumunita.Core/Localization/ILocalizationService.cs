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

    /// <summary>
    /// The instance default authored-in language (ADR 0018 read seam) — the
    /// <see cref="LocaleSettings.DefaultLanguageCode"/> with the <c>en</c>
    /// floor (a missing singleton or blank code yields <c>en</c>). A read:
    /// **no audit row** (it is not a mutating call, matching
    /// <see cref="ListLanguagesAsync"/>). Used to pre-select a compose form's
    /// language picker so the "no change" submit is a concrete BCP-47 code,
    /// never an empty row.
    /// </summary>
    Task<string> GetDefaultLanguageCodeAsync();

    /// <summary>
    /// The instance's <b>default</b> IANA time zone id (ADR 0019 read seam) —
    /// the <see cref="LocaleSettings.DefaultTimezone"/> with the <c>UTC</c>
    /// floor (a missing singleton or blank id yields <c>UTC</c>). A read:
    /// **no audit row** (matching <see cref="GetDefaultLanguageCodeAsync"/>).
    /// The <b>fallback</b> a resident's timestamps render in when they have
    /// set no <c>Profile.TimeZone</c> override; the Web resolution helper (the
    /// <c>kw-dt</c> TagHelper) prefers the signed-in actor's
    /// <c>Profile.TimeZone</c> and falls through to this value, then to
    /// <c>UTC</c>.
    /// </summary>
    Task<string> GetDefaultTimezoneAsync();

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

    // ── Timezone — the instance default (ADR 0019; the admin platform-default
    // write lane, mirroring SetDefaultLanguageAsync's audited shape) ──────

    /// <summary>
    /// Sets the instance's <b>default</b> time zone (the fallback a resident's
    /// timestamps render in when they have set no personal override — ADR
    /// 0019). <paramref name="timezoneId"/> is an IANA id (e.g.
    /// <c>Europe/Warsaw</c>); it is validated against
    /// <see cref="System.TimeZoneInfo"/> and a missing/unknown id throws
    /// <see cref="System.InvalidOperationException"/> **before** any write
    /// (fail-closed, the <see cref="RemoveLanguageAsync"/> pin — **no audit
    /// row** for the blocked attempt). On success appends **exactly one**
    /// <c>AccessAudit</c> row — action <c>timezone.set-default</c>,
    /// <c>TargetKind</c> "timezone", <c>TargetId</c> = the id,
    /// <see cref="Kumunita.Core.Authorization.AccessVia.Admin"/>,
    /// <c>Outcome = Allow</c> — in the same session as the singleton write
    /// (invariant C3). Live on the very next
    /// <see cref="GetDefaultTimezoneAsync"/> call (M·4: data, not config).
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// <paramref name="timezoneId"/> is not a valid IANA time zone id.</exception>
    Task SetDefaultTimezoneAsync(string timezoneId, string actorId);

    // ── Date format — the instance default (ADR 0020; the admin platform-
    // default write lane + read seam, mirroring the timezone pair exactly) ─

    /// <summary>
    /// The instance's <b>default</b> date-time format string (ADR 0020 read
    /// seam) — the <see cref="LocaleSettings.DefaultDateFormat"/> with the
    /// <see cref="DateFormat.FloorFormat"/> floor (a missing singleton or blank
    /// stored value yields the floor). A read: **no audit row** (matching
    /// <see cref="GetDefaultTimezoneAsync"/>). The <b>fallback</b> a resident's
    /// timestamps are formatted in when they have set no
    /// <c>Profile.DateFormat</c> override; the Web resolution helper (the
    /// <c>kw-dt</c> TagHelper) prefers the signed-in actor's
    /// <c>Profile.DateFormat</c> and falls through to this value, then to the
    /// floor.
    /// </summary>
    Task<string> GetDefaultDateFormatAsync();

    /// <summary>
    /// Sets the instance's <b>default</b> date-time format string (the fallback
    /// a resident's timestamps are formatted in when they have set no personal
    /// override — ADR 0020). <paramref name="formatString"/> is a .NET custom
    /// datetime format string (e.g. <c>yyyy-MM-dd HH:mm</c>); it is validated
    /// (via <see cref="DateFormat.IsValid"/>) and a blank or unusable string
    /// throws <see cref="System.InvalidOperationException"/> **before** any
    /// write (fail-closed, the <see cref="RemoveLanguageAsync"/> pin — **no
    /// audit row** for the blocked attempt). On success appends **exactly one**
    /// <c>AccessAudit</c> row — action <c>dateformat.set-default</c>,
    /// <c>TargetKind</c> "dateformat", <c>TargetId</c> = the format string,
    /// <see cref="Kumunita.Core.Authorization.AccessVia.Admin"/>,
    /// <c>Outcome = Allow</c> — in the same session as the singleton write
    /// (invariant C3). Live on the very next
    /// <see cref="GetDefaultDateFormatAsync"/> call (M·4: data, not config).
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// <paramref name="formatString"/> is blank or not a usable .NET custom
    /// datetime format string.</exception>
    Task SetDefaultDateFormatAsync(string formatString, string actorId);

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
    /// slug (M·6). <see cref="LocalizedPage.Updated"/> is set to server time.
    /// <para>
    /// <b>RC U05 (R·3):</b> <paramref name="imageIds"/> is the content-image ids
    /// parsed server-side by the Web save action from the page body's
    /// <c>/content-image/{id}</c> links (the body is the source of truth — the
    /// client never sends the ids). A <c>null</c> value is coalesced to the
    /// doc's non-null empty list, so the pre-RC positional call sites keep
    /// compiling unchanged (source-compatible trailing parameter).
    /// </para>
    /// </summary>
    Task UpsertPageAsync(string slug, string languageCode, string title, string body, string actorId, IReadOnlyList<string>? imageIds = null);

    // ── Completeness — read ─────────────────────────────────────────
    /// <summary>The per-language completeness view (M·12 FACES) — which UI keys and
    /// pages are present vs. missing for a language.</summary>
    Task<LanguageCompleteness> GetCompletenessAsync(string languageCode);
}
