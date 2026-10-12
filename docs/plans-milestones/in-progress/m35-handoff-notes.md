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
