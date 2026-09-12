# Design Doc — Multilingual (UI & platform texts)

> **Primary reference tier** for the multilingual lane (`ML`, ADR 0005). This doc
> is **authored in full up front** (unlike group posts, whose Part 2 was written by
> U2) — the unit register `docs/plans-milestones/done/plan-multilingual.md` is the
> **secondary** tier, and
> `docs/plans-milestones/done/multilingual-handoff-notes.md` is the
> **scratch** tier. This file pins the invariant numbers (**M·1–M·9**), the FACES
> numbers (**M1–M13**), the **exact C#** of every seam, the **19 pinned seam-test
> names**, the **three-test acceptance gate**, and the **drift guard**. Every
> multilingual unit (U1–U9) must match this file verbatim; a deviation is a drift
> pause (§Drift guard). The roadmap (README / `Milestones.cs` / `MilestonesTests.cs`)
> is already bumped (`ML` = *next*) — this lane ships it; U9 (the close) then moves
> `ML` → *done* and `M4` → *next*.

## Context

M1 shipped the **seed surface** of ADR 0005: the `LanguageCatalog` and
`LocaleSettings` POCOs (in `src/Kumunita.Core/Localization/`), registered on
`M1DocTypes`, and materialized by the first-run seeder (`FirstBootSeeder` step 4) —
the source-language `en` row (enabled, sort 0) and the instance default (`en`).
`how-it-works.md` and `ARCHITECTURE.md` §8/§9 already name the two *unshipped*
documents the ADR's module surface promises:

> `TranslationResource { key, languageCode, text }` — UI strings
> `LocalizedPage       { slug, languageCode, title, body: Markdown, updated }` — terms, about, help

This lane builds **on top of that seed**: the two content documents, the
per-request read path (a `TranslationResource`-backed string provider + a single
`LocalizedPage` page engine), the user-preference cookie, and the audited
`/admin/languages` admin surface. It moves the platform from "English only, by
construction" to **"a non-English-speaking neighborhood can run its entire
platform in its own language without a code change or a deploy"** (ADR 0005,
positive consequences).

**Naming.** Multilingual is a **named lane** (`ML`), not a milestone letter —
the **media** (`M4-adjacent`, ADR 0011) and **group posts** (`GP`, ADR 0013)
lanes are the precedent. The roadmap letters **M4/M5/M6 stay
Events / Projects / Portability**; this lane consumes no letter.

## Scope

**In scope (the work to plan) — verbatim from ADR 0005 A–D:**

- **UI strings** via `TranslationResource` (`key`, `languageCode`, `text`),
  resolved per request (user preference → instance default → `en`), with
  **per-string fallback**.
- **Static pages** via `LocalizedPage` (`slug`, `languageCode`, `title`, body
  Markdown, `updated`) — **terms, about, help** — rendered by a single page
  engine.
- **Admin surface at `/admin/languages`** (GlobalAdmin): add/remove languages,
  set default, edit/preview static-page translations, per-language completeness
  view. **Audited.**
- **User preference as a cookie** (not a claim, per the thin-token rule) + a
  settings page.
- The **source language (`en`) ships in the image**; its catalog row and UI
  strings are embedded and materialized by the first-run seeder — **the
  `LanguageCatalog` / `LocaleSettings` seed is already shipped in M1; this plan
  covers what comes after that** (the two content documents + the read path + the
  admin surface).

**Out of scope (do NOT plan):**

- **Machine translation of UGC** (ADR 0005 C, README → *Deferred*) — if it ever
  ships, per-item opt-in with a third-party-boundary review (a new trust
  boundary in `SECURITY.md`, not a feature flag).
- **Federation** (ADR 0001-B) — may move the platform language catalog with the
  IdP, but **per-instance languages stay local**.
- **Any renumbering of M4/M5/M6** — multilingual is `ML`, a named lane; the
  M-letters stay Events / Projects / Portability.

## Invariants (pinned for the multilingual lane)

Nine invariants, **M·1–M·9**. Each is one idea, pinned so every unit and every
FACES row references a stable number. Adding a new invariant (**M·10+**) requires
a design-doc edit in the same commit as the feature that earns it; renaming or
renumbering an existing one is a breaking change and is not allowed mid-lane.

| # | Invariant | Pinned where |
|---|-----------|--------------|
| **M·1** | **Resolution order is total and deterministic:** per request, effective language = user preference (cookie) if enabled → instance default (`LocaleSettings.DefaultLanguageCode`) if enabled → `en` (always seeded); a resident **never sees a blank label** — the last-resort floor is the string's **key itself**. | **U2** (provider) · **U7** (M3/M4 tests) |
| **M·2** | **Fallback is per-string / per-page:** a partially translated language degrades gracefully per key (UI strings) and per slug (static pages) — never as a whole view flipping to `en`. | **U2** (provider) · **U7** (M2/M5 tests) |
| **M·3** | **UGC is never translated:** the provider resolves **platform** text only (UI keys + `LocalizedPage` slugs); it never touches a `Post` / `PostReply` / `Group` body (ADR 0005 C — machine translation is deferred). | **U2** (provider reads only the two content docs) · **U7** (M6 test) |
| **M·4** | **Languages are data, not config** (ADR 0005 B): `LanguageCatalog` / `LocaleSettings` / `TranslationResource` / `LocalizedPage` are Marten docs in `mt`; an admin edit takes effect on the **next request** (no env var, no rebuild, no satellite assemblies). | **U1** (docs + registration) · **U3** (admin seam) |
| **M·5** | **The preference is a cookie, not a claim** (thin-token rule, ADR 0001-B): the per-request preferred language comes from the `kumunita.locale` cookie (Web layer) and is passed to the provider as a plain BCP-47 string — **never** an identity claim, **never** part of the authorization decision. | **U4** (cookie helper) · **U7** (M8 test) |
| **M·6** | **The admin surface is GlobalAdmin-gated and audited:** every `/admin/languages` mutation (add / remove / enable / reorder / set-default / save-string / save-page) appends **exactly one** `AccessAudit` row (`Via = Admin`, `Outcome = Allow`, `TargetKind` = `"language"` / `"translation"` / `"localized_page"`) — the `UserInfoService` admin-action idiom (ARCHITECTURE.md §5). | **U3** (audit rows) · **U7** (the six audit-row-shape tests) |
| **M·7** | **Removing the default is blocked; removed-language rows are retained:** `RemoveLanguageAsync` on the current default **throws** (fail-closed — no audit row on the blocked attempt); a preference pointing at a removed language **falls back to the default** (M·1); `LocalizedPage` rows for a removed language are **retained** so re-adding restores them. | **U3** (guard + retain) · **U7** (M11/M13 tests) |
| **M·8** | **Core stays HTTP-free** (ADR 0006-D): `ITranslationProvider` / `ILocalizationService` reference no ASP.NET/HTTP types; the cookie is read/written only in the Web layer and passed down as a BCP-47 string. | **U2/U3** (no `System.Web`/`HttpRequest` in Core) · **U4** (the one Web cookie seam) |
| **M·9** | **The source language `en` is the guaranteed floor:** its catalog row and every UI string are materialized by the first-run seeder (M1, already shipped); `en` is **always resolvable** and is the last fallback in M·1. | **U2** (the `en` floor) · **U7** (M4 test) |

## FACES (pinned, 13)

Thirteen resident- / admin-facing scenarios, **M1–M13**, each exercising one or
more invariants. The seam tests (§Pinned seam tests) cover these 1:1, plus the
six audit-row-shape tests.

| # | Outcome (what a resident / admin sees / can do) | Pinned by |
|---|---|---|
| **M1** | a resident with a `pl` preference cookie sees a UI string in Polish (the `pl` `TranslationResource` row renders). | M·1, M·2 |
| **M2** | a string missing in the preferred language falls back **per string** — that one label degrades to the default / `en`, the rest of the view stays in the preferred language. | M·2 |
| **M3** | a resident with **no** preference cookie sees the platform in the instance default language. | M·1 |
| **M4** | a fresh instance (default `en`, no cookie) renders entirely in English — the `en` floor is always present. | M·1, M·9 |
| **M5** | a static page (e.g. `/terms`) renders in the preferred language per page; if the preferred version is absent, the `en` page renders (per-page fallback). | M·2, M·7 |
| **M6** | a `pl`-preferring resident reads an `en` **post** → the body is the `en` text as authored — **never** translated. | M·3 |
| **M7** | a resident switches their preference `en → pl` on the settings page (the cookie is written) → the **next** request renders in Polish. | M·1, M·5 |
| **M8** | a resident whose preference points at a **removed** language silently falls back to the instance default — no error, no blank label. | M·1, M·7 |
| **M9** | the GlobalAdmin saves a UI string in `/admin/languages` → it is visible on the **next** request **and** one `AccessAudit` row (`Via = Admin`) is written. | M·4, M·6 |
| **M10** | the GlobalAdmin sets the default language to `pl` → every resident without a preference now sees `pl` on the next request **and** one audit row is written. | M·1, M·6 |
| **M11** | the GlobalAdmin attempts to **remove the current default** language → the action is **blocked** (the service throws; the catalog is unchanged). | M·7, M·6 |
| **M12** | the GlobalAdmin opens the **per-language completeness view** for `pl` → it lists the UI keys and pages present vs. missing (the gap a resident would hit via M·2's fallback). | M·2, M·6 |
| **M13** | the GlobalAdmin **removes** a language, re-adds it, and opens its page editor → the previously-saved `LocalizedPage` rows are **restored** (retained, not deleted). | M·7 |

**FACES count: 13.** This count (and the invariant-pin per row) is the input the
pinned seam-test list needs to name the **19** test names (13 FACES + 6
audit-row-shape) without re-deriving them.

## Pinned contract (exact C# — every unit matches verbatim)

> Every fragment below is **exact**: member names and order, types, defaults, and
> the doc-comments that state the invariants they anchor are the contract. If an
> implemented shape does not match this section verbatim, the **Drift guard**
> applies: **this file wins**; the unit updates this file in the same commit and
> appends a one-line drift note to the handoff note.

### 1. Content documents (the two ADR 0005 B shapes, on top of the M1 seed)

`src/Kumunita.Core/Localization/TranslationResource.cs`:
```csharp
namespace Kumunita.Core.Localization;

/// <summary>
/// One UI-string translation (ADR 0005 B; M·2/M·4). The business key is
/// (Key, LanguageCode) — the unique index in M1DocTypes enforces one text per key
/// per language (the GroupMembership / ComponentMembership pair idiom). A resident
/// never sees a blank label (M·1): a missing (Key, preferred) falls back to
/// (Key, default) → (Key, "en") → the Key itself (M·9's floor is "en", always
/// seeded by the first-run seeder).
/// </summary>
public sealed class TranslationResource
{
    public string Id { get; set; } = string.Empty;            // surrogate (Marten default) — the pair idiom
    public string Key { get; set; } = string.Empty;           // the code key (e.g. "nav.home", "feed.reply")
    public string LanguageCode { get; set; } = string.Empty;  // BCP-47 — a LanguageCatalog.Id
    public string Text { get; set; } = string.Empty;          // the rendered string
}
```

`src/Kumunita.Core/Localization/LocalizedPage.cs`:
```csharp
namespace Kumunita.Core.Localization;

/// <summary>
/// One static-page translation (ADR 0005 B; M·2/M·4). The business key is
/// (Slug, LanguageCode) — one page body per slug per language (terms/about/help),
/// rendered by the single page engine. Markdown body; per-page fallback (M·2).
/// Rows are **retained** when a language is removed (M·7) so re-adding restores
/// them.
/// </summary>
public sealed class LocalizedPage
{
    public string Id { get; set; } = string.Empty;            // surrogate (Marten default) — the pair idiom
    public string Slug { get; set; } = string.Empty;          // "terms" | "about" | "help"
    public string LanguageCode { get; set; } = string.Empty;  // BCP-47 — a LanguageCatalog.Id
    public string Title { get; set; } = string.Empty;         // the page's rendered title
    public string Body { get; set; } = string.Empty;          // Markdown (the single page engine)
    public DateTimeOffset Updated { get; set; }               // last admin edit
}
```

**The `M1DocTypes` registration** (the additive on the existing surface — the
`GroupMembership` unique-index idiom, ADR 0004 §B.1):
```csharp
// M1DocTypes.Configure(StoreOptions) — appended after the existing
// `opts.Schema.For<LanguageCatalog>(); opts.Schema.For<LocaleSettings>();` lines:
opts.Schema.For<TranslationResource>()
       .UniqueIndex(t => t.Key, t => t.LanguageCode);   // business key (one text per key per language)
opts.Schema.For<LocalizedPage>()
       .UniqueIndex(p => p.Slug, p => p.LanguageCode);  // business key (one page per slug per language)
```

### 2. The per-request read seam (`ITranslationProvider`)

`src/Kumunita.Core/Localization/ITranslationProvider.cs` (impl: `TranslationProvider`):
```csharp
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kumunita.Core.Localization;

/// <summary>
/// The per-request read seam (ADR 0005 B; M·1–M·3, M·8, M·9). **HTTP-free**
/// (ADR 0006-D): the Web layer reads the `kumunita.locale` cookie (M·5) and passes
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
```

### 3. The admin-management seam (`ILocalizationService`)

`src/Kumunita.Core/Localization/ILocalizationService.cs` (impl: `LocalizationService`):
```csharp
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
```

`src/Kumunita.Core/Localization/LanguageCompleteness.cs`:
```csharp
using System.Collections.Generic;

namespace Kumunita.Core.Localization;

/// <summary>
/// The per-language completeness view (ADR 0005 D; M·12 FACES). Which UI keys and
/// static pages are present vs. missing for a language — the admin sees the gap a
/// resident would hit via the per-string fallback (M·2) **before** they hit it.
/// The "known" universe of keys/slugs is the instance's seeded `en` set (M·9): a
/// key is *missing* for a language iff it has an `en` row but no row in this
/// language.
/// </summary>
public sealed record LanguageCompleteness(
    string LanguageCode,
    IReadOnlyList<string> PresentKeys,
    IReadOnlyList<string> MissingKeys,
    IReadOnlyList<string> PresentPageSlugs,
    IReadOnlyList<string> MissingPageSlugs);
```

**The audit-row shape (pinned for the six audit-row-shape tests, M·6):**

Every mutating method commits, **in its own transaction**, exactly one
`AccessAudit` row with:

| Field | Value |
|---|---|
| `ActorId` | the GlobalAdmin's subject id |
| `Action` | the per-method id: `language.add` / `language.enable` / `language.disable` / `language.reorder` / `language.remove` / `language.set-default` / `translation.save` / `page.save` |
| `TargetKind` | `language` (catalog) · `translation` (UI strings) · `localized_page` (pages) |
| `TargetId` | the `code` / `key` / `slug` of the affected row (reorder: the first code) |
| `Via` | `AccessVia.Admin` |
| `Outcome` | `AccessOutcome.Allow` |

The **blocked** default-removal (M·7) writes **no** row (it throws before the
commit) — `Admin_RemoveDefaultLanguage_Blocked_NoAuditRow` pins that absence.

### 4. The user-preference cookie helper (Web — the **one** HTTP seam)

`src/Kumunita.Web/Security/LocaleCookie.cs`:
```csharp
using Microsoft.AspNetCore.Http;

namespace Kumunita.Web.Security;

/// <summary>
/// The <c>kumunita.locale</c> preference cookie (ADR 0005 B; M·5). The value is a
/// BCP-47 code (a <see cref="Kumunita.Core.Localization.LanguageCatalog.Id"/>).
/// **Not** a claim (thin-token rule — ADR 0001-B): read here, passed to
/// <see cref="Kumunita.Core.Localization.ITranslationProvider"/> as a plain string;
/// it is never part of the identity or the authorization decision. This is the
/// **only** place the cookie is read or written (M·8 — Core stays HTTP-free).
/// </summary>
public static class LocaleCookie
{
    public const string Name = "kumunita.locale";
    public const int MaxAgeDays = 365;

    /// <summary>Reads the preferred code from the request, or <c>null</c> (no
    /// preference — resolve to the instance default, M·1).</summary>
    public static string? Read(HttpRequest request);

    /// <summary>Writes the preference (the settings-page save, M7 FACES). Value = a
    /// BCP-47 code; <c>HttpOnly</c>, <c>SameSite=Lax</c>, <see cref="MaxAgeDays"/>.</summary>
    public static void Write(HttpResponse response, string languageCode);

    /// <summary>Clears the preference ("reset to default" on the settings page).</summary>
    public static void Clear(HttpResponse response);
}
```

### 5. The admin-surface actions (Web — thin over `ILocalizationService`)

`src/Kumunita.Web/Controllers/LanguagesController.cs` — **GlobalAdmin-gated**
(the `AdminController` `[Authorize(Roles = GlobalAdmin)]` precedent), a thin
surface: each action calls the **one** matching `ILocalizationService` method and
maps a service exception to the Web status (M·7's blocked removal ⇒ **400/409** —
the optimistic-concurrency story, ARCHITECTURE.md §5; the audit row is the
service's, M·6). The actions:

| Action | Method | Route | Delegates to |
|---|---|---|---|
| `Index` | GET | `/admin/languages` | `ListLanguagesAsync` + `GetCompletenessAsync` (the per-language completeness view) |
| `Add` | POST | `/admin/languages` | `AddLanguageAsync(code, nativeName, actorId)` |
| `Enable` | POST | `/admin/languages/{code}/enable` | `SetLanguageEnabledAsync(code, true, actorId)` |
| `Disable` | POST | `/admin/languages/{code}/disable` | `SetLanguageEnabledAsync(code, false, actorId)` |
| `Reorder` | POST | `/admin/languages/reorder` | `ReorderLanguagesAsync(codesInOrder, actorId)` |
| `Remove` | POST | `/admin/languages/{code}` | `RemoveLanguageAsync(code, actorId)` (M·7: default ⇒ 409) |
| `SetDefault` | POST | `/admin/languages/{code}/default` | `SetDefaultLanguageAsync(code, actorId)` |
| `SaveTranslation` | POST | `/admin/languages/{code}/translations` | `UpsertTranslationAsync(key, code, text, actorId)` |
| `SavePage` | POST | `/admin/languages/{code}/pages/{slug}` | `UpsertPageAsync(slug, code, title, body, actorId)` |
| `PreviewPage` | GET | `/admin/languages/{code}/pages/{slug}` | `GetPageAsync(slug, code)` (the editor/preview pane) |

The **user settings page** (the cookie write, M5/M7 FACES) and the **static-page
routes** (`/terms`, `/about`, `/help` over `ITranslationProvider.GetPageAsync`) are
the Web track's U4/U6 surface — thin, both `HTTP`-only, both delegating to the two
Core seams above.

**The provider / service are registered in `src/Kumunita.Core/DependencyInjection.cs`**
(mirroring the media lane's `IMediaStore` registration): `AddTransient<
ITranslationProvider, TranslationProvider>()` + `AddTransient<ILocalizationService,
LocalizationService>()`, both receiving the host `IDocumentStore`.

## Pinned seam tests (19 exact names — the master is authoritative)

File: `tests/Kumunita.Core.Tests/LocalizationServiceTests.cs` (the `PostgresFixture`
harness is the model; U7 owns this file and its 19 `[Fact]`s). **13 FACES tests
(one per M1–M13) + 6 audit-row-shape tests = 19.** A rename or re-scope of a name
after the freeze = a drift event (Drift guard).

| # | test name (exact) | anchored to |
|---|-------------------|-------------|
| 1 | `M1_PreferenceCookie_ResolvesPolishUIString` | M1 FACES; M·1, M·2 |
| 2 | `M2_MissingStringInPreferredFallsBackPerString` | M2 FACES; M·2 (per-string, not per-view) |
| 3 | `M3_NoPreference_UsesInstanceDefault` | M3 FACES; M·1 |
| 4 | `M4_FreshInstance_DefaultIsEnglish` | M4 FACES; M·1, M·9 (the `en` floor) |
| 5 | `M5_StaticPageLocalizesPerPage` | M5 FACES; M·2, M·7 (per-page fallback) |
| 6 | `M6_UgcRendersAsAuthored_NotTranslated` | M6 FACES; M·3 (the provider never reads a Post body) |
| 7 | `M7_PreferenceChange_TakesEffectNextRequest` | M7 FACES; M·1, M·5 (cookie → provider) |
| 8 | `M8_PreferenceAtRemovedLanguage_FallsBackToDefault` | M8 FACES; M·1, M·7 (removed ⇒ disabled ⇒ default) |
| 9 | `M9_AdminSavesTranslation_VisibleNextRequest` | M9 FACES; M·4, M·6 |
| 10 | `M10_AdminSetsDefault_ResidentSeesNewDefault` | M10 FACES; M·1, M·6 |
| 11 | `M11_RemoveDefaultLanguage_Blocked` | M11 FACES; M·7 (throws, catalog unchanged) |
| 12 | `M12_CompletenessView_ShowsMissingKeys` | M12 FACES; M·2, M·6 |
| 13 | `M13_RemovedLanguagePageRows_Retained` | M13 FACES; M·7 (rows retained, not deleted) |
| 14 | `Admin_AddLanguage_AuditRowShape_ViaAdmin` | M·6; `Action "language.add"`, `TargetKind "language"`, `TargetId = code`, `Via Admin`, `Outcome Allow` |
| 15 | `Admin_RemoveLanguage_AuditRowShape_ViaAdmin` | M·6; `Action "language.remove"`, `TargetKind "language"`, `TargetId = code`, `Via Admin`, `Outcome Allow` |
| 16 | `Admin_SetDefaultLanguage_AuditRowShape_ViaAdmin` | M·6; `Action "language.set-default"`, `TargetKind "language"`, `TargetId = code`, `Via Admin`, `Outcome Allow` |
| 17 | `Admin_SaveTranslation_AuditRowShape_ViaAdmin` | M·6; `Action "translation.save"`, `TargetKind "translation"`, `TargetId = key`, `Via Admin`, `Outcome Allow` |
| 18 | `Admin_SavePage_AuditRowShape_ViaAdmin` | M·6; `Action "page.save"`, `TargetKind "localized_page"`, `TargetId = slug`, `Via Admin`, `Outcome Allow` |
| 19 | `Admin_RemoveDefaultLanguage_Blocked_NoAuditRow` | M·7; the blocked removal **throws** and writes **no** `AccessAudit` row (fail-closed absence) |

## Acceptance gate (the three-test shape — U8 records the run)

| # | Test | Shape (what it proves) |
|---|------|------------------------|
| 1 | **closed loop** | the GlobalAdmin saves a `TranslationResource` (`key`, `pl`, text) via the admin seam → the **next** request with a `pl` preference resolves that key to the saved Polish text; the `AccessAudit` row for `translation.save` exists: `Action = "translation.save"`, `TargetKind = "translation"`, `TargetId = key`, `Via = Admin`, `Outcome = Allow` (M9, M·4, M·6) |
| 2 | **handoff** | the GlobalAdmin sets the default language to `pl` → a resident with **no** preference cookie now sees the platform in Polish on the **next** request — the `LocaleSettings` change is picked up **live** (no projection lag, M·4); the `language.set-default` audit row is present (M10, M·1, M·6) |
| 3 | **part-vs-whole** | the **19** names above are the **whole**; tests 1–2 are the **parts**; all must pass **together**, in the **same** `Kumunita.Core.Tests` run as the inherited M1/M2/M3/M3b/media/group-posts anchors (no per-name isolation) |

**Runner note (AGENTS.md test-runner quirk, this machine):** the reliable path is
build then in-process execution — **not** `dotnet test` / VS Test Explorer (the
xunit.v3 discovery quirk — "No tests found / exit code 5" is a **runner** bug, not
a failure):

```
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
```

U8 records the actual pass counts from that run (if a pinned name did not land
verbatim, rename it in the same commit + one-line drift note).

## Drift guard (unit-series rules — frozen once written)

- **The nine invariants (M·1–M·9) and the thirteen FACES (M1–M13)** — the numbers
  are stable for the rest of the lane; a rename/renumber is a **break** (the §Pinned
  seam tests and the gate depend on it).
- **The two content documents** (`TranslationResource` / `LocalizedPage`) — the
  **exact** member list, order, and defaults above; `Post`-style POCOs with the
  pair-idiom business key (surrogate `Id` + `UniqueIndex` on the pair). A re-shape
  = a drift event.
- **The two interfaces** (`ITranslationProvider` / `ILocalizationService`) +
  `LanguageCompleteness` — the **exact** signatures and the audit-row shape table
  freeze when **U3** lands them; the provider's four-method shape freezes when
  **U2** lands it; Web consumes, never re-scopes.
- **The `M1DocTypes` unique indexes** — freeze when **U1** lands them (Marten's
  delta detects the new docs — no re-seed, ADR 0004 §B.1).
- **`LocaleCookie`** — the **one** Web HTTP seam (M·5/M·8); the cookie name
  `kumunita.locale` and the read/write/clear trio freeze when **U4** lands it.
- **The 19 names in §Pinned seam tests** — freeze when **U7** owns
  `LocalizationServiceTests.cs`; a rename/re-scope afterwards = a drift event.
- **The unit-series rules (U1–U9 must respect):**
  1. a unit **never modifies a file not in its own `Deliverables`**;
  2. **never rewrites this design doc** outside this drift guard;
  3. **never introduces a test** whose exact name is not in the §Pinned seam tests
     list (a rename only with a note — no silent drift);
  4. **never opens a new seam** beyond the pin (the two content docs, the two
     interfaces, `LanguageCompleteness`, `LocaleCookie`, the `M1DocTypes` indexes)
     — **any other Core/Web ADD is a `## U<m> — Drift pause`**; and **never
     re-shapes** the two documents / the two interfaces outside this pin;
  5. **if entry reads reveal this design doc is out of date**, the unit **pauses**
     and records `## U<m> — Drift pause` in the handoff note.
- **Counts (the freeze at a glance):**

| Pin | Count | Owner |
|---|---:|---|
| Invariants | 9 (M·1–M·9) | this doc |
| FACES | 13 (M1–M13) | this doc |
| content docs | 2 (`TranslationResource`, `LocalizedPage`) | U1 |
| `M1DocTypes` unique indexes | 2 | U1 |
| Core interfaces | 2 (`ITranslationProvider`, `ILocalizationService`) + 1 record (`LanguageCompleteness`) | U2/U3 |
| Web HTTP seams | 1 (`LocaleCookie`) + 1 controller (`LanguagesController`) + settings/static-page routes | U4–U6 |
| test names | 19 (13 FACES + 6 audit-row-shape) | U7 |
| gate tests | 3 (closed loop / handoff / part-vs-whole) | U8 |
| `M1DocTypes` changes | **2 additive** (the two new docs — no re-seed) | U1 |

## Multilingual — Gate (recorded by U8)

**Date:** 2026-09-12 · **Machine:** Windows (PowerShell terminal) ·
**Runner:** the AGENTS.md reliable path — `dotnet build Kumunita.slnx
-c Debug` (green, 3.1 s) then in-process execution, **not** `dotnet
test` / VS Test Explorer (the xunit.v3 discovery quirk on this machine).
Testcontainers `postgres:18` (Docker Desktop, WSL2 backend); `PostgresFixture`
fresh scratch DB per class.

**`Kumunita.Core.Tests` 280/280 passed, 0 failed, 0 skipped** (27.6 s) —
including the 19 multilingual pinned `[Fact]`s (`LocalizationServiceTests`)
**and** the inherited M1/M2/M3/M3b/group-posts suites, all re-run
unchanged in the same execution. **`Kumunita.Web.Tests` 90/90 passed,
0 failed** (0.6 s), including the `ML`-lane settings/static-page and
`LanguagesController` coverage. **Total: 370/370 passed, 0 failed.** No reds.
No drift: all 19 `§Pinned seam tests` names landed verbatim in
`tests/Kumunita.Core.Tests/LocalizationServiceTests.cs` (U7's freeze held —
no `## U8 — Drift pause`, so no rename lane was exercised).

Record (shape mirrored from the group-posts `§Gate (recorded by U10)` —
`#` | `Test` | `Evidence (actual test names)`):

| # | Test | Evidence (actual test names — all passed) |
|---|------|-------------------------------------------|
| 1 | **Closed loop** (the GlobalAdmin saves a `TranslationResource` (`key`, `pl`, text) via the admin seam → the **next** `pl`-preference request resolves that key to the saved Polish text; the `translation.save` audit row exists: `Action = "translation.save"`, `TargetKind = "translation"`, `TargetId = key`, `Via = Admin`, `Outcome = Allow` — M9, M·4, M·6) | `LocalizationServiceTests.M9_AdminSavesTranslation_VisibleNextRequest` (M9 FACES; the save-then-resolve round-trip — `UpsertTranslationAsync` then the provider's `GetAsync` returns the stored `pl` text on the next read) and `LocalizationServiceTests.Admin_SaveTranslation_AuditRowShape_ViaAdmin` (M·6; the *single* committed `AccessAudit` row — `Action = "translation.save"`, `TargetKind = "translation"`, `TargetId = key`, `Via = Admin`, `Outcome = Allow`, the closed-loop's observable audit shape). |
| 2 | **Handoff** (the GlobalAdmin sets the default language to `pl` → a resident with **no** preference cookie now sees the platform in Polish on the **next** request — the `LocaleSettings` change is picked up **live**, no projection lag, M·4; the `language.set-default` audit row is present — M10, M·1, M·6) | `LocalizationServiceTests.M10_AdminSetsDefault_ResidentSeesNewDefault` (M10 FACES; `SetDefaultLanguageAsync` writes the `LocaleSettings` singleton, then a no-preference `GetAsync` resolves the `pl` row on the very next read — the live-singleton handoff) and `LocalizationServiceTests.Admin_SetDefaultLanguage_AuditRowShape_ViaAdmin` (M·6; the *single* committed `AccessAudit` row — `Action = "language.set-default"`, `TargetKind = "language"`, `TargetId = code`, `Via = Admin`, `Outcome = Allow`). |
| 3 | **Part-vs-whole** (the 19 `§Pinned seam tests` names are the **whole**; gates 1–2 are the **parts**; all must pass **together** in the same `Kumunita.Core.Tests` run as the inherited anchors) | all 19 `LocalizationServiceTests` `[Fact]`s — `M1_PreferenceCookie_ResolvesPolishUIString` · `M2_MissingStringInPreferredFallsBackPerString` · `M3_NoPreference_UsesInstanceDefault` · `M4_FreshInstance_DefaultIsEnglish` · `M5_StaticPageLocalizesPerPage` · `M6_UgcRendersAsAuthored_NotTranslated` · `M7_PreferenceChange_TakesEffectNextRequest` · `M8_PreferenceAtRemovedLanguage_FallsBackToDefault` · `M9_AdminSavesTranslation_VisibleNextRequest` · `M10_AdminSetsDefault_ResidentSeesNewDefault` · `M11_RemoveDefaultLanguage_Blocked` · `M12_CompletenessView_ShowsMissingKeys` · `M13_RemovedLanguagePageRows_Retained` · `Admin_AddLanguage_AuditRowShape_ViaAdmin` · `Admin_RemoveLanguage_AuditRowShape_ViaAdmin` · `Admin_SetDefaultLanguage_AuditRowShape_ViaAdmin` · `Admin_SaveTranslation_AuditRowShape_ViaAdmin` · `Admin_SavePage_AuditRowShape_ViaAdmin` · `Admin_RemoveDefaultLanguage_Blocked_NoAuditRow` — green **in the same execution** as the inherited M1/M2/M3/M3b/group-posts suites (`AuthorizationServiceTests`, `ClaimShapingInvariantBTests`, `AdminOverrideDdlTests`, `KumunitaFeatureDdlTests`, `DbBootstrapIsPristineTests`, `SideEffectHarnessTests`, `DirectoryServiceTests`, `DirectoryServiceTests_U6`, `ProfileToAuditableResourceTests`, `UserInfoServiceTests`, `UserInfoServiceGroupsU9Tests`, `UserInfoServiceGroupPrivacyTests`, `UserInfoServiceGroupInvitationsM2bTests`, `UserInfoServiceGroupDescriptionTests`, `GroupIsPrivateUpgradePathTests`, `PostServiceTests`, `GroupPostServiceTests`, `ModerationServiceTests`, `AnnouncementServiceTests`, `EmailDeadLetterCounterTests`, `SmtpHealthCheckTests`, `CommunityOptionsTests`) — **`Kumunita.Core.Tests` 280/280 passed, 0 failed** in this run; `Kumunita.Web.Tests` 90/90 in its own run. |

## Multilingual — Closed (recorded by U9)

**Date:** 2026-09-12 · **Machine:** Windows (PowerShell terminal) ·
**Runner (re-verified at close):** the AGENTS.md reliable path — `dotnet build
Kumunita.slnx -c Debug` (green) then `dotnet exec` on each test assembly (**not**
`dotnet test` / VS Test Explorer — the xunit.v3 discovery quirk on this machine).
U9 is a **doc + roadmap close**: it changes **no production code**, adds **no**
test, and re-verifies the two assemblies stay green after the roadmap bump.

**Consistency checklist (all confirmed):**
- **Seams present (frozen when landed):** the 2 content docs
  (`TranslationResource` / `LocalizedPage`, U1) + the 2 `M1DocTypes` unique
  indexes (U1) · `ITranslationProvider` + `TranslationProvider` (U2) ·
  `ILocalizationService` + `LocalizationService` + `LanguageCompleteness` (U3) ·
  `LocaleCookie` (the **one** Web HTTP seam, U4) + the two `AddTransient` DI
  registrations in `DependencyInjection.cs` (U4) · `LanguagesController` (U5) +
  `LocaleController` (settings page) + `StaticPagesController` (`/terms`,
  `/help`) + `MarkdownRenderer` + the admin/ locale/ static-page Razor views
  (U6). Every seam matches this doc's §Pinned contract verbatim (no re-shape).
- **19 §Pinned-seam-test names verbatim:** all 19 are present character-for-
  character in `tests/Kumunita.Core.Tests/LocalizationServiceTests.cs` (U7's
  freeze held — no rename lane was exercised), and re-ran green at close
  (re-verified below).
- **Roadmap bump:** `src/Kumunita.Web/Milestones.cs` — `ML` → `StatusDone`,
  `M4` → `StatusNext` (order unchanged; `ML` keeps index 5).
  `tests/Kumunita.Web.Tests/MilestonesTests.cs` kept in step — the single
  in-progress milestone is now `M4` (the `M0_Through_M3_Are_Marked_Done` pin was
  extended to `GP` + `ML` and the single-in-progress test renamed to
  `Events_Is_The_Single_InProgress_Milestone`, asserting `M4`).
- **README Roadmap + status + feature bullet:** `Multilingual` restated as
  **shipped** (roadmap row now **Done.**, `M4` now **Next.**) and the feature
  bullet restated as the live admin + resident surface.
- **`ARCHITECTURE.md` §8/§9:** §9 "Current state" restated to **`ML` lane
  shipped**; the `M1DocTypes` / `Localization/` tree note restated from "lands
  with M6" to **shipped (ML)**; the value-chain milestone table de-`ML`-folds
  (multilingual is a *named lane*, like `GP`/media, not an M-letter row) — and
  M6's row no longer lists "multilingual".
- **Drift-pause count = 0:** no `## U<m> — Drift pause` section in
  `multilingual-handoff-notes.md` across U0–U9; every unit exited on its own
  green gate (U7: 280/280 + 90/90; U8: 370/370 recorded).

**Follow-on recorded (deliberately NOT shipped in this lane):** the `LocalizedPage`
static-page engine (ADR 0005 A) names **terms, about, help**. U6 shipped the
`/terms` + `/help` routes (`StaticPagesController`, slug-guarded to
`{ terms, help }`) but **not** `/about` — the existing `/about` route is
`HomeController.About` (the product-story landing page). Making `/about` render a
`LocalizedPage` body is a **new Web ADD** (a route + a `HomeController` change)
outside this lane's sealed scope (unit-series rule 4: any other Web ADD is a drift
pause), so it is **recorded, not shipped**. The exact change for the M4 lane (or a
small follow-up unit) is: add `"about"` to `StaticPagesController.Slugs` **or**
point `HomeController.About` at `ITranslationProvider.GetPageAsync("about", …)`
when a row exists (fall back to the current product-story view), reusing the
existing `Views/StaticPages/Page.cshtml` + `MarkdownRenderer`. The seam is ready —
only the route/wiring remains.

**Close re-verification (this unit):** `dotnet build Kumunita.slnx -c Debug`
green; `Kumunita.Web.Tests` **90/90** (including the re-pinned `MilestonesTests`
with `M4` as the single in-progress); `Kumunita.Core.Tests` **280/280** (unchanged
— U9 adds no Core test). **Total: 370/370 passed, 0 failed.** No drift.
