# Multilingual (UI & platform texts) — handoff notes (ML)

> **Scratch tier.** One section per unit, **appended** (never rewritten). Each
> unit writes exactly one short `## U#` section before it exits; the next unit
> reads only that section + its own entry-read list (the three-tier contract,
> `docs/plans-milestones/plan-multilingual.md`). **U0** is this session's
> kickoff — the plan-authoring unit that produced the two reference-tier
> artifacts, before any code unit runs.

## U0 — Plan kickoff (this session)

**Date:** 2026-09-12 · **Machine:** Windows (PowerShell terminal) · **Role:**
plan authoring only — **no code, no per-unit plan files** in this session (U1
onward create their own `multilingual-uNN-plan.md` as they start).

**Scope (verbatim from ADR 0005 A–D; see the design doc `## Scope`):**
- **In scope:** UI strings via `TranslationResource` (per-request, preference →
  default → `en`, per-string fallback) · static pages via `LocalizedPage`
  (terms / about / help, one page engine) · the `/admin/languages` admin surface
  (add/remove languages, set default, edit/preview static-page translations,
  per-language completeness — audited) · the user preference as a **cookie**
  (+ a settings page) · the `en` source-language floor (already seeded in M1).
- **Out of scope:** machine translation of UGC (ADR 0005 C, *Deferred*) ·
  federation (ADR 0001-B) · **any renumbering of M4/M5/M6** (multilingual is
  `ML`, a named lane — the M-letters stay Events / Projects / Portability).

**What's already shipped (the M1 seed surface this lane builds on):**
- `LanguageCatalog` + `LocaleSettings` — `src/Kumunita.Core/Localization/LanguageCatalog.cs`.
- Both registered on **`M1DocTypes`** (the existing surface — the design doc
  appends the two new docs there, not a new `DocTypes` surface):
  `opts.Schema.For<LanguageCatalog>(); opts.Schema.For<LocaleSettings>();`.
- Materialized by the first-run seeder — `FirstBootSeeder` step 4
  (`SeedLanguageCatalogAsync`): the source-language `en` row (enabled, sort 0)
  + the instance default (`LocaleSettings.DefaultLanguageCode`) set to `en`
  (ADR 0005 B — "the source language ships with the code"). **Already shipped —
  do not re-do.** This lane ships the **two content documents + the read path +
  the admin surface** that the ADR's module surface promises on top of that seed.

**Roadmap (already bumped — do not re-bump; U9 moves `ML` → done / `M4` → next):**
- `src/Kumunita.Web/Milestones.cs` — `new("ML", "Multilingual — … (ADR 0005)", StatusNext)`.
- `tests/Kumunita.Web.Tests/MilestonesTests.cs` — pins the order
  `M0, M1, M2, M3, GP, ML, M4, M5, M6` + `ML` as the single in-progress.
- `README.md` / `docs/ARCHITECTURE.md` §8/§9 / `how-it-works.md` — already name
  the lane. This session **verified, did not edit** them.

**Reference-tier artifacts produced this session (the two deliverables):**
- **Primary** — `docs/design/multilingual-design.md`: Context · Scope (verbatim
  from ADR 0005 A–D) · **Invariants M·1–M·9** · **FACES M1–M13** · **Pinned
  contract** (the exact C# of the two content docs, `ITranslationProvider`,
  `ILocalizationService` + `LanguageCompleteness`, `LocaleCookie`, the
  admin-surface actions) · **Pinned seam tests (19)** · **Acceptance gate
  (three-test shape)** · **Drift guard**.
- **Secondary** — `docs/plans-milestones/plan-multilingual.md`: the three-tier
  contract · Understanding · Assumptions · the invariant / FACES / pinned-seam-
  test tables (cross-referenced to the design doc) · the **sealed-unit table
  (U1–U9)** · the unit-series rules.

**Unit count: 9** — U1 (content docs + `M1DocTypes` indexes) · U2
(`ITranslationProvider`) · U3 (`ILocalizationService` + `LanguageCompleteness`)
· U4 (`LocaleCookie` + DI) · U5 (`LanguagesController`) · U6 (settings page +
static-page routes) · U7 (the 19 seam tests) · U8 (run + record the gate) ·
U9 (close: `Milestones.cs` / README / ARCHITECTURE sync + folder moves).

**Pinned counts (the freeze at a glance — see the design doc §Drift guard):**
9 invariants (M·1–M·9) · 13 FACES (M1–M13) · 2 content docs · 2 `M1DocTypes`
unique indexes · 2 Core interfaces + 1 record · 1 Web HTTP seam (`LocaleCookie`)
+ 1 controller + settings/static-page routes · **19** test names (13 FACES + 6
audit-row-shape) · 3 gate tests.

**Definition of done (this session, all met):** the design doc + the unit
register exist with every required section; this handoff note has the U0
kickoff; `dotnet build Kumunita.slnx -c Debug` is **green** (doc-only — no code
touched); the `Kumunita.Web.Tests` assembly still passes (verified via the
AGENTS.md `dotnet exec` path, **not** `dotnet test`); the plan is consistent
with the already-bumped roadmap (verified, not re-edited). **Next: U1** (the
content documents + the `M1DocTypes` indexes), per the unit register.

## U1 — Content documents + M1DocTypes indexes

**Date:** 2026-09-12 · **Role:** code unit · **Exit met:** build green +
section written + plan file moved to `done/`.

**What shipped (3 files, matching the design doc §Pinned contract §1 verbatim):**
- **New** `src/Kumunita.Core/Localization/TranslationResource.cs` — the UI-string
  doc: surrogate `Id` + the business-key pair (`Key`, `LanguageCode`) + `Text`
  (sealed, doc-comment anchors M·1/M·2/M·4/M·9 — the key-itself floor).
- **New** `src/Kumunita.Core/Localization/LocalizedPage.cs` — the static-page
  doc: `Id` + (`Slug`, `LanguageCode`) + `Title`, `Body` (Markdown), `Updated`
  (sealed; M·7's row-retention note is here, the *behavior* is U3's).
- **Modified** `src/Kumunita.Core/M1DocTypes.cs` — appended the two
  registrations on the **existing** surface (no new `DocTypes` class, per
  ADR 0004 §B.1 additive — Marten's delta picks them up, **no re-seed**):
  `UniqueIndex(t => t.Key, t => t.LanguageCode)` and
  `UniqueIndex(p => p.Slug, p => p.LanguageCode)` — the pair idiom
  (`GroupMembership` / `ComponentMembership` precedent). Also corrected the
  stale Localization comment that said the surface "lands with M6" — it lands
  **now** (`ML` lane); retention is called out as U3's job, not a delete here.

**State for U2 (the `ITranslationProvider` read seam):** both content docs exist
in `Kumunita.Core.Localization` with exactly the pinned member names — the
provider reads `TranslationResource` / `LocalizedPage` + the M1 seed
(`LanguageCatalog` / `LocaleSettings`), never UGC (M·3). `en` floor (M·9) is the
seeder's row, already shipped — the provider's last fallback is the **key
itself**, not a blank. Nothing else changed: no seeder edit (M1's `en` row is
untouched), no interface yet, no Web change. **Next: U2** —
`ITranslationProvider` + `TranslationProvider`, per the design doc
§Pinned contract §2.

## U2 — `ITranslationProvider` read seam + `TranslationProvider` impl

**Date:** 2026-09-12 · **Role:** code unit · **Exit met:** build green +
section written + plan file moved to `done/`.

**What shipped (2 files, matching the design doc §Pinned contract §2 verbatim):**
- **New** `src/Kumunita.Core/Localization/ITranslationProvider.cs` — the
  per-request read seam: 4 methods (`ResolveEffectiveLanguageAsync`, `GetAsync`,
  `GetManyAsync`, `GetPageAsync`), doc-comments anchoring M·1/M·2/M·3/M·8/M·9.
  **HTTP-free** (M·8): `preferredLanguageCode` is a plain `string?` — no cookie,
  no claim, no `HttpRequest`.
- **New** `src/Kumunita.Core/Localization/TranslationProvider.cs` — the
  implementation (takes `Marten.IDocumentStore`):
  - `ResolveChainAsync` (private helper) resolves the effective language
    (M·1: preferred-if-enabled → default-if-enabled → `"en"`) and builds the
    deduplicated fallback chain (M·2).
  - `GetAsync` / `GetManyAsync`: **one** query for all candidate rows, then
    per-string fallback (M·2); the floor is the **key itself** (M·1 — a resident
    never sees a blank label).
  - `GetPageAsync`: **one** query, per-page fallback (M·2); `null` = truly
    absent (the Web renders a 404).
  - Reads **only** `TranslationResource` / `LocalizedPage` + `LanguageCatalog`
    / `LocaleSettings` — **never** a `Post` / `PostReply` / `Group` body (M·3).
  - Uses `IQuerySession` (the `EmailDeadLetterCounter` read idiom).

**State for U3 (the `ILocalizationService` admin seam):** the read path is
complete. U3 ships the admin seam + `LanguageCompleteness` — it needs `AccessAudit`
(the audit-row shape in the design doc §Pinned contract §3) and the M1 seed
catalog. The provider and the service are **independent** (the provider reads,
the service writes) — no coupling between them. `DependencyInjection.cs`
registration is **not** in U2's deliverables (the provider is registered by U4
or the host; U2 only ships the two files). **Next: U3** —
`ILocalizationService` + `LocalizationService` + `LanguageCompleteness`, per
the design doc §Pinned contract §3.

## U3 — `ILocalizationService` admin seam + `LocalizationService` impl + `LanguageCompleteness`

**Date:** 2026-09-12 · **Role:** code unit · **Exit met:** build green +
section written + plan file moved to `done/`.

**What shipped (3 files, matching the design doc §Pinned contract §3 verbatim):**
- **New** `src/Kumunita.Core/Localization/ILocalizationService.cs` — the
  admin-management seam: 12 methods across three groups (catalog / UI strings /
  static pages) + the completeness read. Every mutating method's doc-comment
  pins the exact `Action` string, `TargetKind`, and `TargetId` (M·6).
  `RemoveLanguageAsync` carries the `<exception>` tag for M·7's fail-closed
  throw. **HTTP-free** (M·8): `actorId` is a plain `string` — no cookie, no
  claim, no `HttpRequest`.
- **New** `src/Kumunita.Core/Localization/LanguageCompleteness.cs` — the
  completeness record: 5 positional parameters (`LanguageCode`, `PresentKeys`,
  `MissingKeys`, `PresentPageSlugs`, `MissingPageSlugs`). The doc-comment
  anchors M·9 (the `en` universe) and M·12 FACES.
- **New** `src/Kumunita.Core/Localization/LocalizationService.cs` — the
  implementation (takes `Marten.IDocumentStore`):
  - **Read paths** (`ListLanguagesAsync`, `GetTranslationAsync`,
    `GetPageAsync`, `GetCompletenessAsync`): `QuerySession`, live rows (M·4 —
    no projection, no cache). `GetCompletenessAsync` computes present/missing
    against the `en` universe (M·9) — one session, two live-row reads (strings
    + pages).
  - **Catalog mutations** (`AddLanguageAsync`, `SetLanguageEnabledAsync`,
    `ReorderLanguagesAsync`, `RemoveLanguageAsync`,
    `SetDefaultLanguageAsync`): one `OpenSession` + one `SaveChangesAsync`;
    each stores exactly one `AccessAudit` row (`Via = Admin`, `Outcome = Allow`,
    `EffectivePrincipalId = actorId`) — the `UserInfoService` admin-action
    idiom (M·6, ARCHITECTURE.md §5).
  - **M·7 (fail-closed):** `RemoveLanguageAsync` checks the default in the
    same transaction before any write — throws `InvalidOperationException` if
    the code is the current `LocaleSettings.DefaultLanguageCode`; no audit row
    is committed for the blocked attempt. Content rows
    (`TranslationResource` / `LocalizedPage`) are **retained** (only the
    catalog row is deleted) — re-adding the language restores them.
  - **Translation / page upserts** (`UpsertTranslationAsync`,
    `UpsertPageAsync`): upsert by business key (pair idiom — the unique index
    enforces one row per pair), one `AccessAudit` row, one `SaveChangesAsync`.
    `UpsertPageAsync` sets `Updated` to server time.
  - Uses `IQuerySession` for reads and `OpenSession` + `SaveChangesAsync` for
    writes (the `UserInfoService` / `TranslationProvider` idiom).

**State for U4 (the `LocaleCookie` + DI registrations):** the read path (U2)
and the admin seam (U3) are **independent** — the provider reads, the service
writes, no coupling between them. U4 ships `LocaleCookie` (the one Web HTTP
seam, M·5/M·8) + the `DependencyInjection.cs` registrations for both
`ITranslationProvider` → `TranslationProvider` and `ILocalizationService` →
`LocalizationService` (the design doc §Pinned contract §5 names this
registration: both `AddTransient`, both receiving the host `IDocumentStore`).
**Next: U4** — `LocaleCookie` + DI, per the design doc §Pinned contract §4 +
§5.

## U4 — `LocaleCookie` Web seam + the Core DI registrations

**Date:** 2026-09-12 · **Role:** code unit · **Exit met:** build green +
section written + plan file moved to `done/`.

**What shipped (2 files, matching the design doc §Pinned contract §4 verbatim):**
- **New** `src/Kumunita.Web/Security/LocaleCookie.cs` — the **one** Web HTTP
  seam of the lane (M·5/M·8): `Name = "kumunita.locale"`, `MaxAgeDays = 365`,
  and the read/write/clear trio. `Read(HttpRequest)` returns the BCP-47 code or
  `null` (a blank value is treated as no preference → M·1's instance default);
  `Write(HttpResponse, code)` appends `HttpOnly` + `SameSite=Lax` + the 365-day
  max-age (M7 FACES — the settings-page save); `Clear(HttpResponse)` deletes with
  the same attributes (the "reset to default" action). The cookie is **never**
  a claim, **never** part of the authorization decision (thin-token rule) — it
  is passed to the provider as a plain string. Lives in `Kumunita.Web.Security`
  next to `KumunitaPrincipal` (matching that folder's style).
- **Modified** `src/Kumunita.Core/DependencyInjection.cs` — inside
  `AddKumunitaCore`, appended the two lane registrations after the media block:
  `AddTransient<Localization.ITranslationProvider,
  Localization.TranslationProvider>()` (U2's seam) and
  `AddTransient<Localization.ILocalizationService,
  Localization.LocalizationService>()` (U3's seam). Both constructors take the
  host-registered `Marten.IDocumentStore` (verified against U2/U3's shipped
  constructors — both are `ctor(IDocumentStore)`), so the plain `AddTransient`
  form resolves cleanly; the host already calls `AddKumunitaCore()`
  (`Program.cs`, after the `AddMarten` block). A lane comment names ML/U4 and
  the M·4/M·5/M·8 anchors. No other line touched.

**State for U5 (the `LanguagesController` admin surface):** **every** Core seam
the controller needs is now registered in the host container —
`ILocalizationService` (all 12 methods, U3) and `ITranslationProvider` (the 4
read methods, U2, for the completeness view / preview path). U5 ships the
GlobalAdmin-gated `LanguagesController` (the design doc §Pinned contract §5
action table — 10 actions, one per `ILocalizationService` method), maps M·7's
blocked `RemoveLanguageAsync` throw to **409**, and writes the audit rows
**only** via the service (M·6 — the controller itself never touches an
`AccessAudit` row). `LocaleCookie` is available for U6's settings page
(its `Read`/`Write`/`Clear` are the cookie's only touch points). **Next: U5** —
`LanguagesController`, per the design doc §Pinned contract §5.

## U5 — `LanguagesController` admin surface (thin, GlobalAdmin-gated)

**Date:** 2026-09-12 · **Role:** code unit · **Exit met:** build green +
section written + plan file moved to `done/`.

**What shipped (1 file, matching the design doc §Pinned contract §5 verbatim):**
- **New** `src/Kumunita.Web/Controllers/LanguagesController.cs` — the
  GlobalAdmin-gated thin surface (the `AdminController`
  `[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]` precedent).
  **One** constructor parameter: `ILocalizationService` — the **only** Core
  seam this controller consumes (both GET actions and all 7 POST actions are
  `ILocalizationService` methods; no `IDocumentStore`, no `ITranslationProvider`).
  The 10 actions, exactly the §5 table: `Index` (GET `/admin/languages`) →
  `ListLanguagesAsync` + `GetCompletenessAsync` (the M·12 completeness view);
  `Add` / `Enable` / `Disable` / `Reorder` / `Remove` / `SetDefault` /
  `SaveTranslation` / `SavePage` (POST) → the matching catalog / string / page
  method; `PreviewPage` (GET `/admin/languages/{code}/pages/{slug}`) →
  `GetPageAsync(slug, code)` (the editor/preview pane).
  - **Thin-token rule (ADR 0001-B):** the actor is read per action via
    `KumunitaPrincipal.SubjectId(User)` (the `Kumunita.Sub` claim — **not** the
    BCL `Identity.Name`) and passed to the service as `actorId`.
  - **M·7 → 409:** `Remove` catches the service's `InvalidOperationException`
    (blocked removal of the current default) and returns
    `StatusCode(409)` — the optimistic-concurrency story §5 pins; the other
    mutations map `ArgumentException` to a `TempData` error + redirect (the
    `AddCommunity` idiom).
  - **No audit writes, no `IDocumentStore`** — M·6's one-`AccessAudit`-row
    guarantee is the service's (U3). The view models
    (`LanguageRowViewModel` / `PageEditorViewModel`) are **public** nested types
    so U6's Razor views can bind to them as
    `LanguagesController.LanguageRowViewModel` (a private nested type is
    invisible to the separately-compiled view class).

**Scope note (for U6):** the design doc §5 table is the **only** pin for U5;
U6 owns the resident-facing surface (the `LocaleCookie` write on the settings
page + the `/terms` `/about` `/help` routes over
`ITranslationProvider.GetPageAsync`) **and** the `Views/Admin/Languages/*.cshtml`
views that bind to this controller. U5 ships the stable HTTP surface; the
`Index` view's `DefaultCode` (which language is the instance default) is **not**
exposed by `ILocalizationService` (no `LocaleSettings` read on the seam) — U6
must source it from its own read path (or the shell may render the default via
a U6-added read; not a U5 concern). **Next: U6** — the settings page +
static-page routes + the admin Razor views, per the design doc §Pinned
contract §5 tail + §4.

## U6 — resident Web surface (locale settings + static-page routes) + admin Razor views

**Date:** 2026-09-12 · **Role:** code unit · **Exit met:** build green +
section written + plan file moved to `done/`.

**What shipped (8 files, matching the design doc §4 + §5 tail verbatim):**
- **New** `src/Kumunita.Web/Controllers/LocaleController.cs` — the M7 FACES
  settings page. `GET /settings/language` reads the enabled catalog (the
  picker) + the `LocaleSettings` singleton (the "default" marker) + the
  current cookie value (`LocaleCookie.Read`). `POST /settings/language`
  calls `LocaleCookie.Write` (M·5: the cookie write, 365-day, HttpOnly,
  SameSite=Lax) or `LocaleCookie.Clear` (the "reset to default" action).
  One `[Authorize]`-gated action pair; `ILocalizationService` in the ctor
  (the **only** Core seam — no `IDocumentStore` read for the catalog, just
  the `LocaleSettings` singleton read for the default marker). A nested
  **public** `LocaleSettingsViewModel` so the view binds cleanly.
- **New** `src/Kumunita.Web/Controllers/StaticPagesController.cs` — the
  M5 FACES static-page routes. `GET /terms` + `GET /help` over
  `ITranslationProvider.GetPageAsync(slug, LocaleCookie.Read(Request))`
  (M·2 per-page fallback lives in U2's provider — this controller is a thin
  route). `null` → 404 (M·5's "truly absent"). Slug guard: only
  `terms` / `help` are served (no route-spoofing for other slugs).
- **New** `src/Kumunita.Web/Security/MarkdownRenderer.cs` — the **single
  page engine** for `LocalizedPage.Body` (ADR 0005 A). Minimal, escape-first
  Markdown→HTML: headings 1–6, paragraphs, ordered/unordered lists,
  `**bold**`, `*italic*`, `` `code` ``, `[label](url)`, fenced code blocks.
  **XSS-safe by construction:** links are extracted from the raw text before
  any escaping (so URLs with `&` in query strings survive the scheme check);
  all non-link text is HTML-escaped before inline rules run; URLs are
  re-escaped for the attribute value (`&` → `&amp;`, `"` → `&quot;`).
  Scheme whitelist: `http` / `https` / `mailto` / relative only —
  `javascript:`, `data:`, and any other scheme render as plain text.
- **Modified** `src/Kumunita.Web/Views/Shared/_AccountNav.cshtml` — added
  the **Settings** nav link (signed-in only, next to Profile) — the
  discovery point for the locale page.
- **New** `src/Kumunita.Web/Views/Locale/Index.cshtml` — the settings-page
  body: the enabled-language `<select>` (with the "instance default" marker),
  the current-preference echo, a "Save" button (POST), and a "Reset to
  instance default" button (POST with `clear=1`). TempData flash.
- **New** `src/Kumunita.Web/Views/StaticPages/Page.cshtml` — the shared
  static-page view (binds to `LocalizedPage`): renders `Title` as an `<h1>`
  and `Body` via `MarkdownRenderer.RenderHtml` (the single page engine).
  Used by both `/terms` and `/help`.
- **New** `src/Kumunita.Web/Views/Admin/Languages/Index.cshtml` — the
  GlobalAdmin catalog shell (binds to
  `IReadOnlyList<LanguagesController.LanguageRowViewModel>`): the
  add-language form, per-language enable/disable/set-default/remove buttons,
  a reorder form, and the M·12 completeness summary (present/missing keys +
  page slugs). TempData flash, `@Html.AntiForgeryToken()` on every form.
- **New** `src/Kumunita.Web/Views/Admin/Languages/PreviewPage.cshtml` —
  the editor/preview pane (binds to
  `LanguagesController.PageEditorViewModel`): the title + body form
  (`SavePage`), and a translation-key editor (key + text,
  `SaveTranslation`). The "no saved translation yet" state
  (`HasSaved == false`) renders an empty form.

**About-page note (U9 follow-on):** the existing `/about` route is owned by
`HomeController.About` (the product-story landing page). U6's deliverables do
**not** touch `HomeController.cs` (unit rule 1). The about-page localization
is reachable through `ITranslationProvider.GetPageAsync("about", …)` — U7's
seam tests exercise that path. If U9 (the close) wants `/about` to render
the `LocalizedPage` body when one exists, that is a **U9** follow-on (a
one-line change to `HomeController.About` + a small localized view), not a U6
concern.

**State for U7 (the 19 pinned seam tests):** every Core seam U7's tests
consume is registered in the host container (`ITranslationProvider` → U2,
`ILocalizationService` → U3, both `AddTransient` via U4's DI block). The Web
surface is now complete: the resident can switch their language (M7 FACES),
read a static page (M5 FACES), and an admin can manage the full catalog
(M9/M10/M11/M12/M13 FACES). U7 ships `LocalizationServiceTests.cs` — the
19 names from the design doc §Pinned seam tests. **Next: U7** — the 19
seam tests, per the design doc §Pinned seam tests table.

## U7 — the 19 pinned seam tests (`LocalizationServiceTests.cs`)

**Date:** 2026-09-12 · **Role:** test unit · **Exit met:** build green +
section written + plan file moved to `done/`.

**What shipped (1 file, all 19 names verbatim from the design doc §Pinned
seam tests):**
- **New** `tests/Kumunita.Core.Tests/LocalizationServiceTests.cs` — the
  19 pinned `[Fact]`s (13 FACES M1–M13 + 6 audit-row-shape), class
  `public class LocalizationServiceTests(PostgresFixture fixture) :
  IClassFixture<PostgresFixture>`. Each test drives the **shipped** Core
  seams (`TranslationProvider` / `LocalizationService`) over a fresh scratch
  Postgres DB (the `PostgresFixture` harness — the `PostServiceTests`
  template, extended with `M3DocTypes.Configure` for M6's `Post` plant).
  - **FACES M1–M13:** each test seeds the M1 row (`en` catalog row +
    `LocaleSettings` singleton) via a private `SeedM1RowAsync` helper (mirrors
    `FirstBootSeeder.SeedLanguageCatalogAsync` exactly — idempotent
    load-then-store), then drives the provider / service per the pinned
    FACES row. M6 plants an `en` `Post` directly (the provider **never**
    reads a `Post` body — the negative pin is the test's assertion that the
    `Post`'s `Body` is unchanged and the provider's platform-text returns
    are independent). M8 removes `pl` via the service's `RemoveLanguageAsync`
    (not the default — allowed) and asserts the provider falls back to `en`.
    M13 removes `pl`, re-adds it, and asserts the `pl` `LocalizedPage` row
    is **restored** (M·7 retention).
  - **Audit-row-shape (6):** each test drives the matching
    `LocalizationService` mutation and asserts the **single** committed
    `AccessAudit` row with the pinned shape (`Action`, `TargetKind`,
    `TargetId`, `Via = Admin`, `Outcome = Allow`, `ActorId = actor`) — the
    design doc §Pinned contract §3 table.
  - **M·7 fail-closed (M11 + test 19):** `RemoveLanguageAsync("en", …)`
    (the default) throws `InvalidOperationException`; the `en` catalog row
    is unchanged; **no `language.remove` audit row** is committed (M11
    filters for `action = "language.remove"` — the setup's `language.add`
    from `AddLanguage` is a separate audit row; test 19 has no setup writes,
    so the full-table-empty assertion pins the fail-closed absence).

**Test-harness conventions (matching `PostServiceTests`):** fresh scratch
DB per test (`PostgresFixture.NewDatabaseAsync`) → `DocumentStore.For` with
`KumunitaFeature` + `AuthorizationFeature` + `M1DocTypes.Configure` +
`M3DocTypes.Configure` → `ApplyAllConfiguredChangesToDatabaseAsync`. The
`Plant` / `UpsertTranslation` / `UpsertPage` / `SetDefault` helpers are
direct write-session seeds (test fixture seeding, **not** service write
seams) — the audit-row-shape tests drive the service's own methods for the
M·6 assertions. `TestContext.Current.CancellationToken` throughout.

**Run (AGENTS.md `dotnet exec` path — not `dotnet test`):**
- `dotnet build Kumunita.slnx -c Debug` — **green** (all 4 projects).
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  — **Total: 280, Errors: 0, Failed: 0, Skipped: 0** (the 19 new
  `LocalizationServiceTests` `[Fact]`s + the inherited M1/M2/M3/M3b/media/
  group-posts anchors).
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  — **Total: 90, Errors: 0, Failed: 0, Skipped: 0** (unchanged, verified).

**State for U8 (the acceptance gate run + record):** the 19 seam tests are
green and in the same assembly as the inherited anchors — the
**part-vs-whole** gate input is ready. U8 owns the *recording* of the
three-test gate (closed loop / handoff / part-vs-whole) into the design
doc's `## Multilingual — Gate` placeholder. The closed-loop and handoff
gate tests are **not** new `[Fact]`s — they are the acceptance-gate run
(U8's), composed from the already-green seam tests + the M9/M10 FACES.
**Next: U8** — run + record the multilingual acceptance gate, per the
design doc §Acceptance gate table.

## U8 — gate run + record

- **Gate: ALL THREE PASS, 2026-09-12, this machine (Windows/PowerShell).**
  Runner per AGENTS.md: `dotnet build Kumunita.slnx -c Debug` (green,
  3.1 s) → `dotnet exec
  tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` →
  **90/90, 0 failed** (0.6 s) → `dotnet exec
  tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll` →
  **280/280, 0 failed** (27.6 s; Testcontainers `postgres:18`, Docker
  Desktop / WSL2 backend — containers stopped and deleted by the run itself,
  no `docker container prune` needed). **Total: 370/370 passed, 0 failed.**
- **Part-vs-whole evidence:** all 19 §Pinned-seam-test names are present
  character-for-character in
  `tests/Kumunita.Core.Tests/LocalizationServiceTests.cs` (19 `[Fact]`s
  counted) and are **inside** the 280-passing run alongside the inherited
  M1/M2/M3/M3b/group-posts suites (the same-run requirement of the
  `## Acceptance gate` row 3). **No drift:** U7's freeze held, so the
  rename lane was not exercised — no `## U8 — Drift pause`.
- **Record:** filled `## Multilingual — Gate (recorded by U8)` in
  `docs/design/multilingual-design.md` (the designated placeholder, kept its
  heading). Closed loop = `M9_AdminSavesTranslation_VisibleNextRequest` +
  `Admin_SaveTranslation_AuditRowShape_ViaAdmin`; handoff =
  `M10_AdminSetsDefault_ResidentSeesNewDefault` +
  `Admin_SetDefaultLanguage_AuditRowShape_ViaAdmin`; part-vs-whole = the
  19-name line above.
- **Handing to U9 (the close):** U8 changed **no** production code and
  added **no** tests — the only repo delta this unit is the design-doc
  gate section + this handoff entry + the plan-file move. U9 owns the
  close: `Milestones.cs` `ML` → done / `M4` → next + README Roadmap +
  `ARCHITECTURE.md` §8/§9 sync + the folder moves (several U1–U7 plan
  files are already in `done/` — U1–U7 each moved their own at exit; U8's
  is the one moved by this note). **Nothing is staged or committed —
  the user reviews first.**
