# M35 — Kiosk mode — rolling handoff note

> **Milestone open (U00).** This is the **scratch tier** (rolling handoff
> note) of M35's three-tier contract. One `##` section per unit, appended
> (never rewritten), in order (U00, U01, … U10). Each unit writes exactly one
> short section before it exits; the next unit reads **only** that section +
> its own entry-reads list.
>
> **M35 is *new context + reused read seams* — the one thing that separates
> M35 from the M34 "derived, not captured" precedent it otherwise mirrors.**
> M35 **creates** a new bounded context `Kumunita.Core.Kiosk` (the M31
> `ErrorReports` / M11 `Portability` / M13 `Usage` precedent) with **one
> singleton doc** (`KioskPlaylist`, the M35·2 pin), **one doc-type surface**
> (`KioskDocTypes`, the ADR 0004 §B.1 shape), and **one service**
> (`IKioskService`, the 2-method seam — M35·1). It **reuses** the **frozen**
> board / calendar / todos read seams (the M5 `IProjectService` board-detail
> + todos-list + the M4 `IEventService` calendar-range seam — the M35·3 pin)
> and the **existing render surface** (the board / calendar / todos Razor
> partials — the M35·6 pin). The kiosk's per-view content rides the
> **frozen** read seams — the same `Read` decision, re-run on every fetch — so
> a slide never reveals more than that viewer could already see on that
> surface's own feed (the M12 iCal "authorization rides the cookie, re-run on
> every fetch" precedent). M35 adds **no** new `AccessAction` / `AccessVia` /
> `Decide()` branch / `IAuditableResource` adapter (the M35·1 / M35·3 pins).
>
> - **Register** — `docs/plans-milestones/plan-m35-kiosk-mode.md`
> - **Design doc (primary)** — `docs/design/m35-kiosk-mode-design.md`
>   (U01/U02 author)
> - **ADR** — **ADR 0164** (the next free number after 0163 — the ADR index
>   in `docs/adr/README.md` confirms 0163 is the current highest: 0163 = the
>   home-upcoming-events-and-todos lane, 0162 = the analytics-history lane
>   `Accepted`, 0161 = the page-translation lane `Accepted`, 0160 = the
>   UG nested-help lane `Accepted`. **0164 is free**; U00 verified this at
>   kickoff.)
> - **Scope** — a **new bounded context + display surface** (M35·1): M35
>   **creates** the one new `Kiosk` context — the `KioskPlaylist` singleton
>   doc (the M35·2 4-member ceiling: `Id` sentinel `"kiosk"` + `Views`
>   `IReadOnlyList<KioskView>` + `ViewIntervalSeconds` +
>   `RefreshIntervalSeconds`) + the `KioskView` entry (the 5-member M35·2
>   ceiling: `Kind` + `BoardId?` + `CalendarView?` + `ComponentId?` +
>   `Label?`) + the `KioskDocTypes` registration surface (the ADR 0004 §B.1
>   shape — one `.Schema.For<KioskPlaylist>`) + the boot wiring (the
>   `Program.cs` dev-loop + the `SchemaBootstrap` `KioskDocTypes.Configure`
>   call) + the `IKioskService` 2-method seam (M35·1 — `GetPlaylistAsync` +
>   `SavePlaylistAsync`) + the `KioskService` impl (the singleton read + the
>   GlobalAdmin write with one `kiosk.save` audit row, M35·5) — and **reuses**
>   the frozen M5 `IProjectService` board-detail + todos-list read seams
>   (M35·3 / M35·9) + the frozen M4 `IEventService.ListInRangeAsync`
>   calendar-range seam (M35·3 / M35·9) + the existing board / calendar /
>   todos Razor partials (M35·6) + the ADR 0104 `body.kmb-page-expanded`
>   chrome-hide idiom (M35·6) + the `kw-l` registry (M35·10). M35 adds the
>   `KioskController` (`GET /kiosk` + `GET /kiosk/slide` + `GET/POST
>   /admin/kiosk`), the `KioskDisplayViewModel`, the
>   `Views/Kiosk/Index.cshtml` chrome-hidden display, the
>   `client/lib/kiosk.ts` auto-cycle / auto-refresh state machine (tsc-only,
>   M35·7), the `site.css` kiosk rules, the closed `kiosk.*` `kw-l` key set ×
>   en/de/fr/da (~8 keys, M35·10), and the pinned test pins (12 tests, M35·12).
>   **No new dependency, no new npm package, no build step** (M35·7). **Core
>   stays HTTP-free + Wolverine-free** (M35·8, ADR 0006-D). **No new
>   `AccessAction` / `AccessVia` / `Decide()` branch / `IAuditableResource`
>   adapter** (M35·1 / M35·3).
> - **Out of scope (named deferrals)** — the **per-resident playlist** (v1 is
>   a single GlobalAdmin-set community playlist) · the **kiosk "lock"** (a
>   PIN / admin-token exit gate — the browser's back button still works in v1)
>   · **audio / ambient sound** · **per-slide dwell weighting** (v1 advances at
>   a uniform `ViewIntervalSeconds`) · the **board / calendar / todos content**
>   itself (M35 reuses the M5 / M4 surfaces — it does not add any board / event
>   / todo capability) · the **iCal / portability / search** surfaces · and the
>   `Milestones.cs` / README / `MilestonesTests` trio until the milestone
>   *ships* (U10 owns the six-member close flip).
> - **Frozen base (reused, unchanged)** — the M5 `Kumunita.Core.Projects`
>   context (`src/Kumunita.Core/Projects/` — M35 **reuses** the
>   `IProjectService` board-detail + todos-list read seams, it does not add a
>   context to it, M35·3 / M35·9) · the M5 `IProjectService` **board-detail +
>   todos-list read seams** (`src/Kumunita.Core/Projects/IProjectService.cs`
>   — `GetBoardAsync` + `ListTodosAsync`; M35 **reuses** them verbatim, it does
>   not re-shape them, M35·3) · the M4 `Kumunita.Core.Events` context
>   (`src/Kumunita.Core/Events/` — M35 **reuses** the `IEventService`
>   calendar-range seam, M35·3 / M35·9) · the M4 `IEventService`
>   **calendar-range seam** (`src/Kumunita.Core/Events/IEventService.cs` —
>   `ListInRangeAsync`; M35 **reuses** it verbatim, M35·3) · the board /
>   calendar / todos **Razor partials** (`src/Kumunita.Web/Views/Projects/
>   BoardDetail.cshtml` + `src/Kumunita.Web/Views/Projects/TodosIndex.cshtml`
>   + `src/Kumunita.Web/Views/Event/Calendar.cshtml` — M35 `@await`s them
>   verbatim, it does not re-author them, M35·6) · the **ADR 0104 chrome-hide
>   idiom** (`src/Kumunita.Web/client/lib/expand-page.ts` — the
>   `body.kmb-page-expanded` class + the `.page-expand-toggle` /
>   `.page-expand-close` selectors + the Escape-key exit; M35's `kiosk.ts`
>   reuses this idiom, M35·6) · the **`SiteContent` / `LocaleSettings`
>   singleton shape** (`src/Kumunita.Core/SiteContent/SiteContent.cs` +
>   `src/Kumunita.Core/SiteContent/SiteContentService.cs` — the singleton doc
>   + service shape the `KioskPlaylist` / `KioskService` mirror; the
>   `SiteContent.SingletonId = "singleton"` sentinel idiom is the structural
>   template; the `KioskPlaylist` proposed sentinel is `"kiosk"`) · the M5 /
>   M4 `kw-l` key sets (`src/Kumunita.Core/Localization/KnownTranslationKeys.cs`
>   — **untouched**, M35·6 additive-only pin; M35 **adds** the `kiosk.*` set,
>   it does not re-author the M5 / M4 set).
> - **M35 is the last milestone** (M35·11) — the close flip (U10) promotes
>   **no** successor. The `M35` row in `Milestones.cs` is the **last** row,
>   currently `StatusNext`.
> - **New invariants (locked in ADR 0164, U00):** M35·1 a new bounded
>   context `Kumunita.Core.Kiosk`, not a re-shape of an existing one · M35·2
>   the playlist is a GlobalAdmin-set singleton (the `KioskPlaylist` 4-member
>   + `KioskView` 5-member ceilings) · M35·3 the display is signed-in and
>   rides the frozen read seams (a denied slide renders
>   `kiosk.slide_denied`, never a 404, never a content leak) · M35·4 the two
>   intervals are display-only, never an access input · M35·5 one
>   `kiosk.save` audit row per write; the display reads emit zero `kiosk.*`
>   rows (the content rides the underlying surface's own audit) · M35·6 the
>   existing render surface is reused, not re-authored · M35·7 zero new
>   dependency, zero new npm package, zero build step (the `kiosk.ts` is
>   tsc-only, ADR 0031) · M35·8 Core stays HTTP-free + Wolverine-free (ADR
>   0006-D) · M35·9 no new bounded context beyond `Kiosk` (the M5 / M4
>   contexts are reused, not re-added) · M35·10 the closed `kiosk.*` `kw-l`
>   key set is four-language (en/de/fr/da) · M35·11 the six-member close flip
>   is U10's responsibility (M35 is the **last** milestone — no successor to
>   promote).

<!-- U00 appends its section below this line. One ## section per unit, in
order (U00, U01, … U10). Never rewrite a prior section. -->

## U00 — Kickoff verified

- **Verified surface (the frozen base M35 reuses):**
  - `IProjectService` — **board-detail seam**:
    `Task<BoardDetailResult> GetBoardAsync(string boardId, string actorId,
    CancellationToken ct = default)` (line 225 of
    `src/Kumunita.Core/Projects/IProjectService.cs`) — the kiosk's board slide
    rides this. **Todos-list seam**:
    `Task<TodoPage> ListTodosAsync(string? componentId, string? assigneeId,
    string actorId, int page, bool unassignedOnly = false, string? projectId
    = null, bool blockedOnly = false, CancellationToken ct = default,
    SortSpec? sort = null, int? pageSize = null)` (line 69) — the kiosk's
    todos slide rides this. **Board-list seam** (for the settings board
    picker):
    `Task<BoardPage> ListBoardsAsync(string? componentId, string actorId, int
    page, string? projectId = null, CancellationToken ct = default, SortSpec?
    sort = null, int? pageSize = null)` (line 190). All three are
    **reused verbatim** — M35 does not re-shape them (M35·3 / M35·9).
  - `IEventService` — **calendar-range seam**:
    `Task<IReadOnlyList<Event>> ListInRangeAsync(DateTimeOffset
    windowStartUtc, DateTimeOffset windowEndUtc, string? componentId, string
    actorId, CancellationToken ct = default)` (in
    `src/Kumunita.Core/Events/IEventService.cs`) — the kiosk's calendar slide
    rides this. **Reused verbatim** (M35·3 / M35·9).
  - **Board / calendar / todos Razor partials** (reused, M35·6):
    - `src/Kumunita.Web/Views/Projects/BoardDetail.cshtml`
    - `src/Kumunita.Web/Views/Projects/TodosIndex.cshtml`
    - `src/Kumunita.Web/Views/Event/Calendar.cshtml` (note: the folder is
      singular `Event`, not `Events` — the register's `Views/Events/Calendar.cshtml`
      path is a minor typo; the actual path is `Views/Event/Calendar.cshtml`)
  - **ADR 0104 chrome-hide idiom** (reused, M35·6):
    `src/Kumunita.Web/client/lib/expand-page.ts` — the
    `body.kmb-page-expanded` class (hides the top navbar, footer, flash toast,
    pinned-announcement banner; stretches `<main>` to fill the viewport); the
    `.page-expand-toggle` / `.page-expand-close` selectors; the Escape-key
    exit. tsc-only, no-dep, no-`any` IIFE shape (the ADR 0031 pin). M35's
    `kiosk.ts` reuses this idiom.
  - **`SiteContent` / `LocaleSettings` singleton shape** (structural
    template): `SiteContent.SingletonId = "singleton"` (the singleton sentinel
    idiom). `SiteContentService.GetAsync` — `QuerySession` +
    `LoadAsync<SiteContent>(SiteContent.SingletonId)` → returns the row or
    `new SiteContent()` (never null, zero audit rows).
    `SiteContentService.SaveAsync` — `LoadAsync` + set fields +
    `session.Store` + **one** `AccessAudit` row (Action=`"site.save"`,
    TargetKind=`"site"`, Via=`Admin`) + `SaveChangesAsync`. The
    `KioskPlaylist` proposed `Id` sentinel is **`"kiosk"`** (mirroring the
    `SiteContent.SingletonId` idiom; the register's M35·2 pin).
  - **`Milestones.cs` M35 row** (the M35·11 pin): line 81 —
    `new("M35", "Kiosk mode — …", StatusNext)` — confirmed `StatusNext` +
    **last** row (no M36; no successor to promote).
  - **ADR number**: **0164** is free (0163 =
    `home-upcoming-events-and-todos`, 0162 = `analytics-history`, 0161 =
    `page-translation-standing-author-branch`, 0160 =
    `nested-help-guides-tabbed-surface-subpages` — all the existing
    `docs/adr/016x-*.md` files; no 0164 file exists).
  - **Precedent ADR list**: 0004 §B.1 (the singleton doc-type registration
    idiom — `KioskDocTypes` mirrors this) · 0031 (the tsc-only, no-dep,
    no-`any` JS pin — `kiosk.ts` mirrors this) · 0104 (the chrome-hide
    idiom — `body.kmb-page-expanded` + the `.page-expand-toggle` /
    `.page-expand-close` selectors) · 0015 (the `kw-l` registry
    discipline — the `kiosk.*` key set is pinned by
    `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests`) ·
    0006-D (Core HTTP-free — the `KioskService` is a plain store-composing
    service, no `HttpClient` in Core).

## U01 — design doc Part 1

- **Authored** `docs/design/m35-kiosk-mode-design.md` **Part 1** (Context,
  Scope in/out + named deferrals, Invariants, FACES). **No code, no build.**
- **11 invariants pinned** (contiguous, M35·1–M35·11 — U02 pin these by id):
  M35·1 new bounded context `Kumunita.Core.Kiosk`, not a re-shape · M35·2 the
  playlist is a GlobalAdmin-set singleton (the `KioskPlaylist` 4-member +
  `KioskView` 5-member ceilings) · M35·3 the display is signed-in and rides
  the frozen read seams (a denied slide renders `kiosk.slide_denied`, never a
  404, never a content leak) · M35·4 the two intervals are display-only, never
  an access input · M35·5 one `kiosk.save` audit row per write; the display
  reads emit zero `kiosk.*` rows · M35·6 the existing render surface is
  reused, not re-authored · M35·7 zero new dependency / npm package / build
  step (`kiosk.ts` tsc-only, ADR 0031) · M35·8 Core stays HTTP-free +
  Wolverine-free (ADR 0006-D) · M35·9 no new bounded context beyond `Kiosk`
  (the M5 / M4 contexts are reused, not re-added) · M35·10 the closed
  `kiosk.*` `kw-l` key set is four-language (en/de/fr/da) · M35·11 the
  six-member close flip is U10's (M35 is the **last** milestone — no
  successor to promote).
- **10 FACES pinned** (M35-1–M35-10 — U02 pin these by id): M35-1 GlobalAdmin
  sets playlist → saves (one `kiosk.save`) + `/kiosk` renders slide 1 (M35·2,
  M35·5) · M35-2 signed-in resident opens `/kiosk` → slide 1 renders (M35·3,
  M35·6) · M35-3 auto-advance after `ViewIntervalSeconds` (M35·4, M35·7) ·
  M35-4 content auto-refresh after `RefreshIntervalSeconds` (M35·4, M35·3) ·
  M35-5 a denied slide → `kiosk.slide_denied` (M35·3) · M35-6 anonymous →
  sign-in redirect (M35·3) · M35-7 non-GlobalAdmin settings → 403 (M35·2) ·
  M35-8 empty playlist → `kiosk.empty` (M35·10) · M35-9 read + display reads
  emit zero `kiosk.*` audit rows (M35·5) · M35-10 board / calendar / todos
  surfaces unchanged (M35·6).
- **Note for U02 (impl-shape to reconcile in Part 2):** the register's M35·2
  assumption has `SavePlaylistAsync` **upsert** the `KioskPlaylist` singleton
  (a missing row is created). The `SiteContentService.SaveAsync` structural
  template it mirrors is actually a **no-op on a missing row** (it "never
  load-or-creates", SITE·6 — see
  `src/Kumunita.Core/SiteContent/SiteContentService.cs`). The **register is
  the source of truth** and pins the upsert; U02 should pin the exact
  `KioskService` impl shape (upsert vs. no-op) in Part 2 §2.2 and note the
  `SiteContent` divergence there. Not a Part 1 surface drift.

## U02 — design doc Part 2 + ADR 0164 Draft

- **Appended** `## Seams & contracts (Part 2, written by U02)` to
  `docs/design/m35-kiosk-mode-design.md` (§2.1 frozen seam list, §2.2 new
  Core types, §2.3 display + settings surface, §2.4 closed `kiosk.*` key set,
  §2.5 the 12 pinned test names, §2.6 the three-test acceptance gate, §2.7 the
  drift-guard). **Drafted** `docs/adr/0164-kiosk-mode.md` (Status: **Draft** —
  U10 flips it to Accepted). **No code, no build.**
- **(a) Reused frozen seams (verbatim, §2.1):** M5
  `IProjectService.GetBoardAsync(boardId, actorId, ct)` (board slide) +
  `ListTodosAsync(componentId, assigneeId, actorId, page, …, ct, sort, pageSize)`
  (todos slide) + `ListBoardsAsync(componentId, actorId, page, projectId, ct, sort, pageSize)`
  (settings board picker); M4
  `IEventService.ListInRangeAsync(windowStartUtc, windowEndUtc, componentId, actorId, ct)`
  (calendar slide). All **reused verbatim** — M35 adds no seam to them
  (M35·3 / M35·9).
- **(b) New `IKioskService` 2-method seam (§2.1):**
  `Task<KioskPlaylist?> GetPlaylistAsync(CancellationToken ct = default);`
  (read — zero audit rows, null on missing) + `Task<KioskPlaylist>
  SavePlaylistAsync(IReadOnlyList<KioskView> views, int viewIntervalSeconds, int
  refreshIntervalSeconds, string actorId, IDocumentSession session, CancellationToken
  ct = default);` (write — one `kiosk.save` row, in the caller's session).
- **(c) Ceilings + upsert-vs-no-op resolution (§2.2):** `KioskPlaylist`
  **4 members** (`Id` sentinel `"kiosk"`, `Views`, `ViewIntervalSeconds`=30,
  `RefreshIntervalSeconds`=10) + `KioskView` **5 members** (`Kind`, `BoardId?`,
  `CalendarView?`="week", `ComponentId?`, `Label?`) + `KioskViewKind` **3
  members** (`Board`/`Calendar`/`Todos`) + `KioskDocTypes` **one**
  `.Schema.For`. **Chose the upsert** (create-on-missing) — the register is the
  source of truth; the `SiteContentService.SaveAsync` template is a no-op on a
  missing row (SITE·6), so M35 **intentionally departs** (U08 test
  `M35_2_SavePlaylist_NoRow_CreatesSingleton` pins it). Also pinned the
  **caller's-session** shape (vs. the template's own session) + the **nullable
  read** (vs. the template's never-null fallback) as intentional divergences.
- **(d) 12 test names (§2.5):** Core `KioskServiceTests` —
  `M35_2_GetPlaylist_NoRow_ReturnsNull` · `M35_2_GetPlaylist_Configured_ReturnsRow`
  · `M35_5_GetPlaylist_WritesNoAuditRow` · `M35_5_SavePlaylist_OneKioskSaveAuditRow`
  · `M35_2_SavePlaylist_NoRow_CreatesSingleton` · `M35_SavePlaylist_AtomicInCallerSession`;
  Web `KioskControllerTests` — `M35_3_SignedIn_Sees_ChromeHidden_FirstSlide` ·
  `M35_3_Anonymous_RedirectedToSignIn` · `M35_5_SlideDenied_SignsInViewer_SeesDeniedShape`
  · `M35_7_NonGlobalAdmin_AdminKiosk_403` · `M35_5_GlobalAdmin_SavePlaylist_OneAuditRow_Persists`
  · `M35_6_ExistingSurfaces_RenderUnchanged`.
- **(e) Three-test gate (§2.6):** **Closed loop** (GlobalAdmin sets playlist →
  `/kiosk` renders the first slide) · **Handoff** (a slide resolves via the
  frozen seam — same `Read` decision as the surface's own feed) · **Part-vs-whole**
  (the 12-test list is the whole; all must pass together). U09 records it.
- **(f) ADR 0164 Draft:** `docs/adr/0164-kiosk-mode.md` — the **"new context +
  reused read seams"** framing (M35·1 / M35·3) that inverts M34's "derived, not
  captured"; 10 decisions (D1–D10); Status **Draft**.
- **(g) Closed `kiosk.*` key set (§2.4):** **8 keys** — `kiosk.title` /
  `kiosk.empty` / `kiosk.slide_denied` / `kiosk.next_slide` / `kiosk.refreshed` /
  `kiosk.settings.title` / `kiosk.settings.view_interval` /
  `kiosk.settings.refresh_interval` — four-language (en values are the source
  text; de/fr/da are U06's to author). Board / calendar / todos `kw-l` sets
  **untouched** (M35·6).

## U03 — Kiosk context + IKioskService

- **(a) `IKioskService` public-method count: 2** — `GetPlaylistAsync` (the
  nullable singleton read — zero audit rows, M35·5) + `SavePlaylistAsync`
  (the GlobalAdmin write — one `kiosk.save` audit row, `TargetKind="kiosk"`,
  `Via=Admin`, within the caller's session, M35·5). The design doc §2.1
  pinned signatures matched exactly.
- **(b) Ceilings:** `KioskPlaylist` **4 members** (`Id` sentinel `"kiosk"`,
  `Views` `IReadOnlyList<KioskView>`, `ViewIntervalSeconds` int=30,
  `RefreshIntervalSeconds` int=10) + `KioskView` **5 members** (`Kind`,
  `BoardId?`, `CalendarView?`="week", `ComponentId?`, `Label?`) +
  `KioskViewKind` **3 members** (`Board`/`Calendar`/`Todos`). All three
  pinned ceilings from the design doc §2.2 matched exactly.
- **(c) `KioskDocTypes` line count: 1 `.Schema.For`** —
  `opts.Schema.For<KioskPlaylist>()`. The `KioskView` entry is a value
  object nested in the singleton (not a standalone doc — M35·9: the kiosk
  adds no new doc type beyond `KioskPlaylist`). The `SiteContent` /
  `LocaleSettings` singleton shape mirrored (the structural template).
- **(d) Boot-path line added (1 location — `Program.cs` only):**
  `src/Kumunita.Web/Program.cs` line 287 — `KioskDocTypes.Configure(opts);`
  (added next to the existing `EscalationDocTypes.Configure(opts);` at
  line 276, in the `AddMarten` lambda). **Note:** `SchemaBootstrap.cs`
  contains **zero** `*DocTypes.Configure` calls (verified by grep — all 20
  existing registrations live in `Program.cs`'s `AddMarten` lambda); the
  plan's mention of `SchemaBootstrap.cs` is a minor inaccuracy. The
  `KioskDocTypes.Configure` call in `Program.cs` is the sole boot wiring
  (the M31 `ErrorReportDocTypes` / M13 `UsageDocTypes` / SITE / M29 / M30
  / ESC precedent — all registered in `Program.cs` only).
- **(e) DI registration:** `src/Kumunita.Core/DependencyInjection.cs` —
  `services.AddTransient<Kiosk.IKioskService>(sp => new Kiosk.KioskService(
  sp.GetRequiredService<Marten.IDocumentStore>(),
  sp.GetRequiredService<IAuthorizationService>()));` — the M31/M13
  `AddTransient` shape. The `KioskService` ctor is
  `(IDocumentStore store, IAuthorizationService authz)` — the
  `IAuthorizationService` is carried for composition-root symmetry (the M23
  `ProfileFindService` / M5 `PostService` precedent) but is **not invoked**
  (M35·1 pins "no `Decide()` branch"; the controller
  `[Authorize(Roles = GlobalAdmin)]` enforces the standing). The unused-
  field is avoided via `_ = authz ?? throw` validation in the ctor.
- **(f) Confirmation of the M35·1 / M35·3 / M35·8 pins:** **No** new
  `AccessAction` / `AccessVia` / `Decide()` branch / `IAuditableResource`
  adapter was added (the kiosk adds no authorization surface of its own —
  the per-view content rides the **frozen** board / calendar / todos read
  seams). **No** `HttpClient` in Core (the `KioskService` is a plain
  store-composing service, ADR 0006-D). The M5 `Projects` + M4 `Events`
  contexts are **untouched** (M35·9).
- **(g) Compile warnings on the new types:** **None.** The 3 warnings in
  the build output are in `HomeControllerUpcomingTests.cs` (lines 37–39,
  CS8625 `Cannot convert null literal to non-nullable reference type`) — a
  file I did **not** touch (pre-existing, M4/M13 related). The 6 new Kiosk
  files + the 2 wire-in edits produced **zero** warnings. Build green
  (27.6 s, 4 projects, 3 pre-existing warnings in `Kumunita.Web.Tests`).

## U04 — Drift pause (the "partials" the register assumes are actually full MVC views)

> **U04 did not ship.** Per **unit-series rule §13** I verified the frozen
> base before writing code, found a mismatch between what the register /
> design doc §2.3 / my unit plan assume and what is actually on disk, and am
> **stopping** rather than improvising. **No code was written, no files
> created/modified, the plan was NOT `git mv`'d to `done/`, and nothing was
> committed.** The working tree is clean (only this handoff-note append is
> new). The decision below is for the lane owner / the next fresh agent with
> a mandate to amend the register + design doc.

**What U04 is specified to build.** `KioskController` (`GET /kiosk` +
`GET /kiosk/slide`), `Models/KioskDisplayViewModel.cs`, and
`Views/Kiosk/Index.cshtml` (chrome-hidden stage) — where each slide
`@await Html.PartialAsync`s the **same** board / calendar / todos **partial**
the surface's own view uses, and the controller builds the three slide view
models via the frozen read seams (M35·6 "reuse, not re-author"; the design
doc §2.3 shows `object? FirstSlideContent`; Deliverables ≤ 4 files).

**What the frozen base actually is (verified on disk, U04):**

- `src/Kumunita.Web/Views/Projects/BoardDetail.cshtml` is a **full MVC view**,
  not a partial: `@model BoardDetailViewModel` (line 1),
  `ViewData["Title"] = …` (line 10), `@section Scripts` (line 828 — a
  **layout-only** construct that **cannot render standalone**), and
  `@inject IDocumentStore Store` (line 6) whose body runs an **unscoped**
  `Store.QuerySession().Query<KanbanBoard>()` (all non-deleted boards — an
  actor-unscoped read, not the frozen actor-scoped `IProjectService` seam).
- `src/Kumunita.Web/Views/Projects/TodosIndex.cshtml` is a **full MVC view**:
  `@model TodoIndexViewModel` (line 1), `ViewData["Title"]` (line 10), and
  reads four **controller-seeded** bag entries —
  `ViewData["Audience_Users"]` (line 400), `ViewData["Assign_Users"]` /
  `ViewData["Audience_Groups"]` / `ViewData["Assign_Communities"]` (lines
  442–444) — populated only by `ProjectsController.TodosIndex`'s
  `SeedGrantPickerOptionsAsync()` + community seed (lines 672–683).
- `src/Kumunita.Web/Views/Event/Calendar.cshtml` is a **full MVC view**:
  `@model EventCalendarViewModel` (line 1), `ViewData["Title"]` (line 13),
  `@section Scripts` (line 456), and depends on `events-calendar.js` +
  `events-calendar-time.js` + `rich-editor.js` (loaded via that `@section`).
- **Their view models are built by ~200–400 lines of
  `ProjectsController` / `EventController` private-helper logic** —
  `BoardDetail` (`ProjectsController.cs:2009–2208`) + `TodosIndex`
  (`:539–705`): `ResolveDisplayNameAsync`, `ResolveComponentNamesAsync`,
  `ResolveGroupNamesAsync`, `SeedComponentPickerAsync`, `SeedProjectPickerAsync`,
  `SeedLanguagePickerAsync`, `SeedGrantPickerOptionsAsync`,
  `ActorMembershipAsync`, `ApplyTodoTranslationAsync`, `ProjectRow`,
  `LanguageOptionsFrom`, `FeedPaging.PageSizeAsync`,
  `SortViewModel.ForRoute`, plus the `ViewData` seeds above; `Calendar`
  (`EventController.cs:485–…`): its own ~150-line anchor/window +
  `EventCalendarViewModel` ctor (line 691).
- **No `@await Html.PartialAsync` of any of these three views exists
  anywhere in the codebase** (grep-verified); there are no board/calendar/
  todos "partial" files to `@await` (the only `_`-prefixed partials under
  `Views/` are `_ProjectsTabs`, `_EventsTabs`, `_GrantPickers`, `_Pager`,
  `_BoardCard`, etc. — none is a board/calendar/todos *detail* surface).

**Why "U04 as specified" cannot be implemented without violating a frozen pin:**

- **M35·6 (reuse, don't re-author) is broken by construction.** The three
  surfaces are full views whose models require the controller-private helper
  fan-out above. There is no `@await`-able partial to reuse. The only ways to
  render a slide are (a) **rebuild each `*ViewModel` in `KioskController`**
  (re-authoring the surface + a **second display/decision path**, which also
  touches the **M35·1 / M35·3 / ADR 0006-D** "single decision path" spine), or
  (b) **refactor the three surface views into real partials** — which
  **modifies** the frozen M5/M4 surface files (violates M35·6 / M35·9 and
  **Deliverables ≤ 4 files** — that path needs ≥ 8 files including the two
  existing controllers) — or (c) **client-side `fetch` + iframe the existing
  routes** (`/projects/boards/{id}`, `/events/calendar?view=…`,
  `/projects/todos`) which **re-renders each surface's full layout + chrome**
  (contradicts the chrome-hidden single-stage design) and needs a JS change
  the register forbids (M35·7).
- **The §2.3 `object? FirstSlideContent` shape is incoherent as written**: a
  board slide is a `BoardDetailViewModel`, a calendar slide an
  `EventCalendarViewModel`, a todos slide a `TodoIndexViewModel` — three
  unrelated types, so the `KioskDisplayViewModel` cannot carry one content
  member per slide without the same re-authoring.

**Two candidate forward paths (decision needed before any code):**

1. **Kiosk renders each slide by *embedding the live surface route*
   (iframe/`fetch` of the existing full pages), chrome hidden by CSS only.**
   Faithful to "the same `Read` decision, re-run on every fetch" (M35·3) and
   to M35·6 (zero re-authoring) and M35·9 (surfaces untouched), but it
   re-renders each surface's own `_Layout` (navbar/footer), which conflicts
   with the single chrome-hidden `#kiosk-stage` model and the `kiosk.ts`
   innerHTML-swap design. Needs a design-doc + register amendment on the
   "chrome-hide a full rendered page inside a stage" mechanic.
2. **Introduce true read-only partials for the three surfaces (a small M5/M4
   refactor) and have `KioskController` build the three VMs** (delegating to
   the existing controller actions rather than duplicating the helpers, to
   avoid a second decision path). This matches the §2.3 mental model best but
   is **> 4 files, touches frozen surface files (M35·6 / M35·9), and re-opens
   the ADR 0006-D single-decision-path question** — it is a larger unit than
   U04 was scoped to and arguably belongs to its own lane.

**What I did NOT do:** no `KioskController` / `KioskDisplayViewModel` /
`Views/Kiosk/Index.cshtml`; no `KioskSettingsViewModel` (that is U05's, and it
inherits the same surface assumption); no `site.css` / `_Layout` /
`kiosk.ts` edits (U07's); no `git mv` of `m35-u04.md`; **no commit**.
The register (plan-m35-kiosk-mode.md §U04 / §U05 / §2.3), the design doc
(§2.3 + §2.7 drift-guard), and the handoff note now agree that the frozen
base is **three full MVC views, not three partials** — that is the fact the
decision must turn on.

## U04 — /kiosk display (resumed)

> **U04-resume resolved the pause and shipped.** The lane owner (reachable)
> was asked the Path 1 vs. Path 2 decision and chose **Path 1 — iframe the
> live surface routes**. U04 then implemented the `/kiosk` display per Path 1
> and exited with **build green**. **No M5/M4 surface file was touched**
> (M35·6 / M35·9 held); **no** new `AccessAction` / `AccessVia` / `Decide()`
> branch / `IAuditableResource` adapter was added (M35·1 / M35·3 held); **no**
> new npm package / build step (M35·7 held); Core stays HTTP-free /
> Wolverine-free (M35·8 — the `KioskController` is a Web-layer class, the
> `KioskService` seam is untouched).

**Decision — Path 1 (iframe the live surface routes), rationale:**

- The frozen base is **three full MVC views** (`BoardDetail.cshtml` /
  `TodosIndex.cshtml` / `Event/Calendar.cshtml`), not partials — the U04
  drift pause. `@await`-ing them is impossible (`@section Scripts` is
  layout-only; `@inject` / `ViewData` bags are controller-seeded; the VMs
  need ~200–400 lines of controller helper fan-out). Path 2 (extract partials
  + KioskController builds the VMs) would **touch the frozen M5/M4 surface
  files** (violates M35·6 / M35·9 + the ≤ 4-file constraint) and re-open the
  ADR 0006-D single-decision-path question — the drift note's own verdict is
  that it "arguably belongs to its own lane." **Path 1 honors M35·3 most
  strongly** (the iframe's navigation **re-runs the surface's own `Read`
  decision** — the frozen board / calendar / todos read seam — on every load,
  the M12 iCal precedent), **honors M35·6 more strongly** (the kiosk adds no
  slide view at all — it frames the surface's own view), **holds M35·9** (no
  M5/M4 file edited), and **stays ≤ 4 files**.
- Verified enabling facts on disk: the app's own CSP sets
  `frame-ancestors 'self'` (`src/Kumunita.Web/Program.cs:721`) — same-origin
  iframing is explicitly permitted; `script-src 'self'` (no inline
  `<script>` — U04 uses none; U07's `kiosk.ts` must be a module file);
  `style-src 'self' 'unsafe-inline'` (the view's inline `<style>` chrome-hide
  is permitted).
- **How the M35·6 / M35·3 pins are still held:** the kiosk renders **no**
  board / calendar / todos view of its own (it iframes the surface's own
  route, so `@section Scripts` / `@inject` / `ViewData` all work and the
  surface's own `Read` decision re-runs on the iframe's navigation); the
  board / calendar / todos Razor views + their `kw-l` key sets are
  **untouched** (M35·6 additive-only). The §2.3 `object? FirstSlideContent`
  shape is **superseded** (recorded in the design doc §2.3 drift note + the
  register §U04) because the three unrelated surface VMs cannot share one
  content member — replaced by `IReadOnlyList<KioskSlideViewModel> Slides`
  (each slide carrying `Href` + `Denied` + `Label`).

**Secondary drift (mine, discovered in this unit) — the `kiosk.*` kw-l set
is inseparably four-language:** the register / design doc §2.4 assigned the
`kiosk.*` key set to **U06** ("en values are the source text; de/fr/da are
U06's to author"), and U04's brief assumed the keys "exist as en-only floor
until U06." On disk the **`kiosk.*` keys are absent from the registry** (not
an en-only floor — the `kw-l` TagHelper would render the raw key), **and**
the Core.Tests parity tests
(`DeValues_Keys_Match_AllKeys_Exactly_NoEmptyValues`, Fr, Da in
`KnownTranslationKeys_ParityTests`) enforce **key-for-key set equality**
between en/de/fr/da — so there is **no valid en-only intermediate state**: the
moment a `kiosk.*` key is added to `EnValues`, all three go red until de/fr/da
match. Combined with the Web.Tests view-scan test
(`KwLRegistryConsistencyTests.Every_KwL_Key_In_A_View_Is_Registered`, which
requires every static `kw-l key="…"` in a view to be in `EnValues`), U04's
view **cannot** reference `kiosk.*` keys unless all four languages are
present. **Resolution:** U04 co-ships the closed 8-key `kiosk.*` set in all
four languages (en/de/fr/da), **absorbing U06's deliverable**; **U06 is
re-scoped to a verification unit** (confirm the set is present + non-empty in
all four + the board/calendar/todos sets are untouched, then exit). The
register §U06 + design doc §2.4 are amended to record this supersession.

**Deliverables (U04's, 4 code files + 1 registry + 1 design-doc amendment):**

- **`KioskController`** (`src/Kumunita.Web/Controllers/KioskController.cs`) —
  **2 actions** (`Index` + `Slide`). `Index` = `GET /kiosk` `[Authorize]`
  (reads the playlist via `IKioskService.GetPlaylistAsync`, classifies each
  slide via the frozen read seam for the signed-in viewer, renders
  `Views/Kiosk/Index.cshtml`; a null/empty playlist → the `kiosk.empty`
  message, M35-8 FACE). `Slide` = `GET /kiosk/slide?slide=N&t=<ts>`
  `[Authorize]` (the single-slide refresh fetch — CSRF-free GET, the M12 iCal
  precedent; returns a JSON `KioskSlideRefreshViewModel`). The classification
  **catches the frozen seams' deny signals** (`GetBoardAsync`'s
  `KeyNotFoundException` / `UnauthorizedAccessException`;
  `ListTodosAsync` / `ListInRangeAsync`'s `UnauthorizedAccessException`) and
  renders the `kiosk.slide_denied` shape (M35-5 FACE: no content leak, no
  404). **No** new `AccessAction` / `AccessVia` / `Decide()` branch (M35·1 /
  M35·3); the per-view decision is the frozen seam's, re-run by the surface's
  own controller on the iframe's navigation.
- **`KioskDisplayViewModel`** (`src/Kumunita.Web/Models/KioskDisplayViewModel.cs`)
  — `IReadOnlyList<KioskSlideViewModel> Slides` + `int ViewIntervalSeconds`
  (=30) + `int RefreshIntervalSeconds` (=10) + `bool HasSlides` +
  `KioskSlideViewModel? FirstSlide`. The **`KioskSlideViewModel`** record
  (per slide): `int Index` + `KioskViewKind Kind` + `string? Label` +
  `string? Href` (the surface's own route) + `bool Denied` (the
  `kiosk.slide_denied` shape). The **`KioskSlideRefreshViewModel`** (the
  `/kiosk/slide` JSON): `int Index` + `bool Denied` + `string? Href` +
  `string? DeniedKey`. These match U03's Core types exactly (`KioskPlaylist`
  4 members, `KioskView` 5 members, `KioskViewKind` 3 — `Board`/`Calendar`/
  `Todos`, via `IKioskService.GetPlaylistAsync` → `KioskPlaylist?`).
- **`Views/Kiosk/Index.cshtml`** — the chrome-hidden display. The
  `#kiosk-stage` element carries the **`data-*` attributes U07's
  `client/lib/kiosk.ts` reads**: `data-view-interval`,
  `data-refresh-interval`, `data-slide-count`; plus one hidden
  `.kmb-kiosk-slide-meta` div per slide with `data-index` / `data-href` /
  `data-denied` / `data-label` (simple values — no JSON-in-attribute, so
  Razor's attribute encoding is a no-op for URLs/labels — sidestepping the
  Razor attribute-value trap in `AGENTS.md`). The first slide renders an
  `<iframe src="…">` (the surface's own route) or the `kiosk.slide_denied`
  shape. The chrome-hide (navbar / footer / flash / pinned-announcement) is
  applied via the view's inline `<style>` reusing the ADR 0104
  `body.kmb-page-expanded` selector set (the M35·6 chrome-hide idiom **reused,
  not re-authored** — no `_Layout.cshtml` touch, so the ≤ 4-file footprint
  holds). A `kmb-kiosk-exit` link (→ `/`) is the ADR 0104 `.page-expand-close`
  exit affordance. **No inline `<script>`** (CSP `script-src 'self'`), **no**
  new JS / npm package (M35·7 — `kiosk.ts` + `site.css` are U07's).
- **`KnownTranslationKeys.cs`** — the **8 `kiosk.*` keys added to all four
  dicts** (en/de/fr/da): `kiosk.title` / `kiosk.empty` /
  `kiosk.slide_denied` / `kiosk.next_slide` / `kiosk.refreshed` /
  `kiosk.settings.title` / `kiosk.settings.view_interval` /
  `kiosk.settings.refresh_interval`. The board / calendar / todos `kw-l` key
  sets are **untouched** (M35·6 additive-only). (U06's deliverable, absorbed
  per the secondary drift above.)
- **`m35-kiosk-mode-design.md` §2.3** — the **Path-1 supersession note**
  appended (the `object? FirstSlideContent` → `Slides` / `Href` / `Denied`
  change, the `/kiosk/slide` JSON refresh, the inline-`<style>` chrome-hide,
  the deny-signal catch, the `kiosk.ts` iframe-advance model). The
  `SettingsGet` / `SettingsPost` + `KioskSettingsViewModel` rows (U05) are
  **unaffected**.

**Exit:** `dotnet build Kumunita.slnx -c Debug` **green** (0 errors, 0
warnings on the new code — the 3 build warnings are the pre-existing
`HomeControllerUpcomingTests.cs` CS8625 ones noted in U03, in a file I did not
touch). `KwLRegistryConsistencyTests` (Web.Tests) **green** (1/1 — the
`kiosk.*` keys in the view are registered). `KnownTranslationKeys_ParityTests`
(Core.Tests) **green** (7/7 — en/de/fr/da set-equality holds). **Full
Web.Tests green** (1033 total, 0 errors, 0 failed, 1 skipped — the
`ImproveCheck_Gate_Passes` pwsh test, unrelated). **No test name outside
§pinned-test-names was introduced** (the 12 U08 pins are untouched — U04
introduces no tests; U08 authors `KioskServiceTests` + `KioskControllerTests`).
**U07's `kiosk.ts` contract (the `#kiosk-stage` data-* names it reads):**
`data-view-interval` / `data-refresh-interval` / `data-slide-count` on
`#kiosk-stage`, and per-slide `data-index` / `data-href` / `data-denied` /
`data-label` on `.kmb-kiosk-slide-meta`. The two `setInterval`s U07 drives:
(1) advance the visible slide every `data-view-interval` (wrap to first),
(2) re-point the iframe every `data-refresh-interval` (re-read the next
`.kmb-kiosk-slide-meta`, set the iframe `src`, or swap the
`kiosk.slide_denied` shape when `data-denied === "true"`). `GET
/kiosk/slide?slide=N&t=<ts>` is the cache-bust refresh fetch (U07 may call it,
or simply re-point the iframe — both re-run the frozen seam).

## U05 — /admin/kiosk settings

- **(a) `KioskController` action count: 4** — `Index` (U04) + `Slide` (U04) +
  `SettingsGet` (`GET /admin/kiosk`, U05) + `SettingsPost` (`POST
  /admin/kiosk`, U05). Both settings actions are
  `[Authorize(Roles = GlobalAdmin)]` (the M35-7 FACE — a non-GlobalAdmin gets
  403). `SettingsPost` is `[ValidateAntiForgeryToken]` (the admin-surface
  write-lane idiom, the `AdminSurfaceLabelsController.Save` /
  `AdminStorageController.Save` precedent).
- **(b) `KioskSettingsViewModel` member set:** `IReadOnlyList<KioskView>
  Views` (the ordered slide list — M35·2) + `int ViewIntervalSeconds`
  (display-only, M35·4) + `int RefreshIntervalSeconds` (display-only, M35·4)
  + `IReadOnlyList<(string Id, string Title)> Boards` (the board picker seed —
  M35·3 / M35·9) + `bool IsValid` (server-side validation flag — the view
  does not render it; the admin view's local convention is non-kw-l English,
  so the flag is for the controller's own use only). **5 members.**
- **(c) Board picker seed seam:** the **frozen** M5
  `IProjectService.ListBoardsAsync(componentId: null, actorId, page: 1,
  pageSize: 200, ct: ct)` (the M35·3 / M35·9 pin — reused, not re-shape).
  The GlobalAdmin's own `Read` decision runs on the seam, so the picker
  offers only the boards the GlobalAdmin can already see (a leak-prevention
  guard, not a new access branch). A read failure degrades to an empty
  picker (the form still renders; no board id is surfaced — no leak).
- **(d) Pin confirmation:** **No** new `AccessAction` / `AccessVia` /
  `Decide()` branch / `IAuditableResource` adapter was added (the
  `[Authorize(Roles = GlobalAdmin)]` class gate is the standing — the
  admin-surface precedent; the service
  (`KioskService.SavePlaylistAsync`) owns the single `kiosk.save` audit row
  + the upsert, M35·5 — the controller opens the
  `IDocumentStore.LightweightSession` and passes it in, the M3
  same-transaction precedent). **No** new JS / npm package (M35·7 — the view
  is a plain `<form method="post">` + a static `<table>`; no
  `<script>` tag, no `data-*` JS hooks, no drag-and-drop lib). **No**
  `KnownTranslationKeys.cs` change (U04 already co-shipped the 8
  `kiosk.*` keys in all four languages — U05 only *references* the existing
  `kiosk.settings.title` / `.view_interval` / `.refresh_interval` keys,
  which are confirmed present in en/de/fr/da at `KnownTranslationKeys.cs`
  lines 2745–2747 / 5349–5351 / 7868–7870 / 10362–10364).
- **(e) Compile warnings:** **None.** Build green (0 errors, 0 warnings on
  the new code). Web.Tests **1033/0/0** (1 skipped — the pre-existing
  `ImproveCheck_Gate_Passes` pwsh gate, unrelated to U05).
- **View design note (a deviation from the register's ≤ 4-file constraint,
  within its intent):** the register's U05 §Deliverables names
  `_AdminNav.cshtml` as the nav entry. On disk, `_AdminNav.cshtml` is the
  **nav-tabs strip** (the `AdminController`'s 5 GET actions: Overview /
  Accounts / Communities / Platform / Security / Analytics — the
  `AdminController`'s own section). The established convention for
  **separate-controller** GlobalAdmin settings surfaces (`/admin/site`,
  `/admin/labels`, `/admin/timezone`, `/admin/storage` — all of which are on
  their own controller, exactly like `/admin/kiosk`) is a **row in the
  Platform list-group** (`Views/Admin/Platform.cshtml`), the exact precedent
  `AdminSurfaceLabelsController` (ADR 0152, M29·7) explicitly mirrors. U05
  added the Kiosk row there (after "Surface labels"), not to
  `_AdminNav.cshtml`. This is **not a new surface** — it is the same
  `/admin/kiosk` route, and it gives a visible, discoverable entry to a
  GlobalAdmin on the Platform hub. The M35·2 / M35·6 / M35·7 pins are
  unchanged (the Platform list-group is an **additive** row — no existing
  surface's file is re-authored, no new JS, no new npm package). **Total
  U05 files: 4** (the controller edit, the new VM, the new view, the
  Platform row) — within the register's ≤ 4-file constraint.

## U06 — kiosk.* kw-l key set (verification)

> **U06 is a verification unit — it authored nothing** (the set was co-shipped
> by U04, per the U04-resume drift note: the parity tests make the set
> inseparably four-language, so there was no valid en-only intermediate state).

- **(a) Key count: 8** — `kiosk.title` / `kiosk.empty` / `kiosk.slide_denied`
  / `kiosk.next_slide` / `kiosk.refreshed` / `kiosk.settings.title` /
  `kiosk.settings.view_interval` / `kiosk.settings.refresh_interval` — the
  closed `kiosk.*` set, matching design doc §2.4 + the register §U06.
- **(b) Four-language present + non-empty: CONFIRMED.** All 8 keys are present
  and non-empty in **all four** dicts: `en` (`KnownTranslationKeys.cs`
  ~2740), `de` (~5344), `fr` (~7863), `da` (~10357). `en` values are the
  source text (ADR 0015 D1); `de`/`fr`/`da` are U04's authored translations
  (the `du`/`tu`/`dig` familiar registers, sentence case — consistent with the
  ADR 0042 idiom blocks adjacent to each block).
- **(c) board / calendar / todos `kw-l` key sets: UNTOUCHED (M35·6 additive-only).**
  The U04 commit (`b967bc58`) `KnownTranslationKeys.cs` diff is
  `kiosk.*`-only: the only non-`kiosk` added lines are section-header comments
  + blank lines (the M35·10 closed-set comment + a `fr` surface banner); **no**
  M5 board / M4 calendar / todos key line was added, removed, or re-authored.
- **(d) Two closure tests GREEN:**
  - `KwLRegistryConsistencyTests` (Web.Tests) — **1/1** passed, 0 failed
    (the `kiosk.*` keys referenced in `Views/Kiosk/*` are registered).
  - `KnownTranslationKeys_ParityTests` (Core.Tests, namespace
    `Kumunita.Core.Tests`) — **7/7** passed, 0 failed (en/de/fr/da
    key-for-key set-equality holds).
- **Build:** `dotnet build Kumunita.slnx -c Debug` **green** (0 warnings,
  0 errors). **No `KnownTranslationKeys.cs` change** (no key missing/empty —
  no drift pause). **Total U06 code files touched: 0** (verification only).

## U07 — kiosk.ts + site.css

> **Path-1 model (U04-resume), NOT the register's stale innerHTML-swap.**
> U07 drives the **iframe `src`**, it does not swap innerHTML over fetched
> slide HTML. The register §U07 "re-fetches from `GET /kiosk/slide` + innerHTML
> swap" language is **superseded**: each slide iframes the surface's OWN route
> and `renderSlide` re-points `iframe.src` (re-navigating re-runs the surface's
> own `Read` decision on the frozen seam — M35·3). No `document.write`, no
> inline `<script>`, no fetch/JSON — the iframe IS the content (M35·7).

- **(a) `kiosk.ts` — 123 lines, two `setInterval`s.**
  `src/Kumunita.Web/client/lib/kiosk.ts` (new) — a plain-tsc, no-dep, no-`any`
  IIFE (the `expand-page.ts` shape, ADR 0031). Self-guards: a clean no-op when
  there is no `#kiosk-stage` (so loading globally from `_Layout.cshtml` is
  safe) and a no-op when `slideCount === 0` (the `kiosk.empty` page shows;
  nothing to cycle). Reads the two intervals from `#kiosk-stage`'s
  `data-view-interval` / `data-refresh-interval` and the ordered slides from
  the `.kmb-kiosk-slide-meta` divs (`data-index` / `data-href` / `data-denied`
  / `data-label`, the U04 contract). **Two independent `setInterval`s
  (display-only, M35·4):** (1) **ADVANCE** — every `viewIntervalSec`, move
  `current = (current + 1) % slideCount` and render it; (2) **REFRESH** — every
  `refreshIntervalSec`, re-render the current slide. Both call `renderSlide(i)`,
  which reads the meta, removes any existing `:scope > .kmb-kiosk-iframe` /
  `:scope > .kmb-kiosk-denied` (so the server-rendered first slide is cleanly
  replaced on the first tick, no duplication), and appends either an
  `<iframe class="kmb-kiosk-iframe" src=…>` (re-pointing `src` re-runs the
  surface's own `Read` on the frozen seam — M35·3) or the localized
  `kiosk.slide_denied` shape when `data-denied === "true"` (M35-5 FACE: no
  leak, no 404). The denied text is read from the **P0-6 `#kumunita-strings`
  bundle** via the `L(key, fallback)` idiom the other client modules
  (notifications-bell / rich-editor) already use, so it is localized in the
  effective language; the en floor is the kw-l view text. A quiet cross-fade is
  applied by toggling the `.kmb-kiosk-fading` class around the swap (built on
  the `.3s opacity` transition the view's inline `<style>` already declares —
  M35·6 idiom reused, M35·7 no animation lib). **No `AccessAction` / `AccessVia`
  / `Decide()` branch** added (M35·1 / M35·3 — the JS is display-only); **no**
  board / calendar / todos view or `kw-l` key touched (M35·6 additive-only).
  One tsc quirk resolved: TS does not carry the `stage` guard-narrowing into
  the `renderSlide` function expression (TS18047), so a definite
  `const stageEl: HTMLElement = stage` is bound after the guard and used in the
  closure.
- **(b) `site.css` rules added — 2 blocks (24 lines appended).**
  `src/Kumunita.Web/wwwroot/css/site.css` (append, after the
  `.notifications-dropdown .dropdown-item` block). **Does NOT re-declare the
  chrome-hide** (that + the stage/title/iframe/empty/denied base sizing already
  live in `Views/Kiosk/Index.cshtml`'s inline `<style>` — the ADR 0104
  `body.kmb-page-expanded` idiom, view-scoped). It adds only: (1)
  `#kiosk-stage .kmb-kiosk-iframe { width:100%; height:100%; border:0; }` —
  a robust fill for the JS-built iframe (belt-and-suspenders for the inline
  flex rule), and (2) `#kiosk-stage .kmb-kiosk-fading { opacity:0; transition:
  opacity .3s ease; }` — the fade state `kiosk.ts` toggles around each slide
  swap (M35·6 chrome-hide idiom reused; M35·7 — plain CSS, no animation lib).
- **(c) `_Layout.cshtml` script-tag append.**
  `src/Kumunita.Web/Views/Shared/_Layout.cshtml` (2 additive edits, 11 lines).
  Appended `<script type="module" src="~/js/lib/kiosk.js"></script>` immediately
  after the `expand-page.js` tag (the global script-tag idiom — self-guarding,
  no-op on non-kiosk pages, M35·7). **Also** added `"kiosk.slide_denied"` to the
  closed `ClientJsStringKeys` array (line 60) so the P0-6 `#kumunita-strings`
  bundle carries it — this is what lets `kiosk.ts` render a localized denied
  shape via the `L(key, fallback)` idiom instead of hard-coding the string.
  No test pins the bundle's exact key set (grep-verified `tests/**` clean), so
  the additive key is safe.
- **(d) `package.json` unchanged — CONFIRMED (M35·7).** `git status` shows only
  `_Layout.cshtml` + `site.css` (modified) + `kiosk.ts` (new); `package.json`
  is **not** in the diff. The tsc build (`npm run build` → `tsc`,
  `outDir: wwwroot/js`) emitted `wwwroot/js/lib/kiosk.js` (5989 bytes), which is
  gitignored (`.gitignore:31` `src/Kumunita.Web/wwwroot/js/`) so it is not
  committed — the tsc-only build path (ADR 0031) is preserved.
- **(e) `tsc` warnings:** **None** (one TS18047 "possibly null" on the
  closure-captured `stage` resolved by binding a definite `const stageEl` after
  the guard; the final `tsc` run is clean, 0 warnings).
- **Builds:** `npm run build` (tsc) **green**; `dotnet build Kumunita.slnx -c
  Debug` **green** (0 warnings, 0 errors). **No `AccessAction` / `AccessVia` /
  `Decide()` branch added** (M35·1 / M35·3); **no** board / calendar / todos
  view or `kw-l` key touched (M35·6 additive-only); **no** new npm package /
  build step (M35·7). **Total U07 code files touched: 3** (the `kiosk.ts` new
  + the `site.css` + `_Layout.cshtml` additive edits) — within the ≤ 3-file
  constraint.
