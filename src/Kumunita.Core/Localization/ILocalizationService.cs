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

    /// <summary>
    /// Adds a language (BCP-47 code + native name) — audited
    /// <c>language.add</c>, TargetKind "language", TargetId = code (M·6).
    /// <para>
    /// <b>Seed-baseline auto-apply (ADR 0044):</b> if the code is a bundled
    /// baseline language (the <see cref="KnownTranslationKeys"/> /
    /// <see cref="Bootstrap.FirstBootSeeder"/> per-language sources) the
    /// bundled UI-string baseline AND the bundled system-page baselines are
    /// materialized into the instance <b>create-if-missing</b> (an admin edit
    /// is never overwritten — the same ADR 0042 D1 ownership invariant the
    /// first-boot seeder holds). This closes the warm-instance gap where a
    /// language added after first boot had an empty translation set, and makes
    /// delete-then-re-add the reset path for a baseline language. A
    /// non-bundled code is added exactly as before (catalog row only).
    /// </para>
    /// </summary>
    Task AddLanguageAsync(string code, string nativeName, string actorId);

    /// <summary>Enables / disables a language — audited <c>language.enable</c> /
    /// <c>language.disable</c>, TargetId = code (M·6).</summary>
    Task SetLanguageEnabledAsync(string code, bool enabled, string actorId);

    /// <summary>Reorders the catalog by <c>sortOrder</c> — audited
    /// <c>language.reorder</c> (M·6).</summary>
    Task ReorderLanguagesAsync(IReadOnlyList<string> codesInOrder, string actorId);

    /// <summary>
    /// Removes a language — audited <c>language.remove</c>, TargetId = code (M·6).
    /// <see cref="TranslationResource"/> rows for a **custom** code are
    /// **retained** (M·7) so re-adding restores them.
    /// <para>
    /// <b>Seed-baseline reset (ADR 0044):</b> for a <b>bundled</b> baseline
    /// language the code's <see cref="TranslationResource"/> rows AND
    /// <see cref="Pages.PageTranslation"/> rows are **deleted** (the catalog row
    /// is removed either way). This is what makes delete-then-re-add the reset
    /// path: <see cref="AddLanguageAsync"/> re-applies the bundled baseline
    /// create-if-missing. Admin edits to a bundled baseline are intentionally
    /// discardable this way — that is the reset the ADR 0042 D1 "never
    /// overwrite" rule is paired with (see ADR 0044).
    /// </para>
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// <paramref name="code"/> is the current
    /// <see cref="LocaleSettings.DefaultLanguageCode"/> — fail-closed, **no audit
    /// row** is written for the blocked attempt (M·7; M11 FACES).</exception>
    Task RemoveLanguageAsync(string code, string actorId);

    /// <summary>Sets the instance default — audited <c>language.set-default</c>,
    /// TargetId = code (M·6; M10 FACES).</summary>
    Task SetDefaultLanguageAsync(string code, string actorId);

    /// <summary>
    /// <b>Read seam (ADR 0044):</b> the bundled seed baseline for a BCP-47
    /// code, or <c>null</c> when the code has no bundled baseline (a custom
    /// language). The baseline is the **code's** per-language source
    /// (<see cref="KnownTranslationKeys.DeValues"/> /
    /// <see cref="KnownTranslationKeys.FrValues"/>) for UI strings and
    /// <see cref="Bootstrap.FirstBootSeeder.DeDefaultPages"/> /
    /// <see cref="Bootstrap.FirstBootSeeder.FrDefaultPages"/>) — the exact text
    /// the first-boot seeder writes on a pristine DB, so re-applying it to a
    /// warm instance is byte-identical to first-boot. No audit row (a read).
    /// </summary>
    Task<BundledLanguageBaseline?> GetBundledBaselineAsync(string code);

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

    // ── Completeness — read ─────────────────────────────────────────
    /// <summary>The per-language completeness view (M·12 FACES) — which UI keys are
    /// present vs. missing for a language.</summary>
    Task<LanguageCompleteness> GetCompletenessAsync(string languageCode);
}

/// <summary>
/// One bundled seed baseline per language (ADR 0044). The closed set of
/// baseline data a language ships with in the code:
/// </summary>
/// <list type="bullet">
/// <item><see cref="UiStrings"/> — one <see cref="TranslationResource"/>-shape
/// text per UI-string key (the <see cref="KnownTranslationKeys.DeValues"/> /
/// <see cref="KnownTranslationKeys.FrValues"/> per-language source).</item>
/// <item><see cref="PageBaselines"/> — one (slug, title, body) per seeded
/// system page (the <c>FirstBootSeeder.DeDefaultPages</c> /
/// <c>FrDefaultPages</c> per-language source in
/// <c>Kumunita.Core.Bootstrap</c>).</item>
/// </list>
/// <para>
/// <b>A closed, code-owned value</b> — not a database row: it is read from the
/// code's per-language sources and written into the instance (create-if-missing)
/// by <see cref="AddLanguageAsync"/> on a warm instance. The
/// <see cref="LanguageCompleteness"/> analog is a *stored* completeness view;
/// this is a *seed* baseline.
/// </para>
public sealed record BundledLanguageBaseline(
    string LanguageCode,
    IReadOnlyDictionary<string, string> UiStrings,
    IReadOnlyList<(string Slug, string Title, string Body)> PageBaselines);
