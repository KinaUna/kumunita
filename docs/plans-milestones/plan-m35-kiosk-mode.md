# M35 — Kiosk mode — sealed unit register

> **In progress.** This is the **lane register** (secondary tier of the
> milestone's three-tier contract) for **M35** — the README / `Milestones.cs`
> line, verbatim:
> "**Kiosk mode — a full-page / full-screen display that automatically cycles
> a preset list of views (e.g. Kanban board A, calendar week, Kanban board B,
> calendar today, todos), changing view on a set interval and refreshing each
> view's content on a separate, faster interval.**"
>
> M35 is a **milestone** (it takes the `M35` letter; it is the **last**
> milestone on the roadmap — there is **no** `M36` to promote), **not a named
> lane**. It is a **new capability on the display surface** (M35·1): it
> **creates** a new bounded context `Kumunita.Core.Kiosk` (the M31
> `ErrorReports` / M11 `Portability` / M13 `Usage` precedent — one singleton
> doc `KioskPlaylist` + one service `IKioskService` + one `KioskDocTypes`
> surface) and **reuses** the **frozen** board / calendar / todos read seams
> (the M5 `IProjectService` board-detail + todos-list seams + the M4
> `IEventService` calendar-range seam) and the **existing render surface**
> (the board / calendar / todos Razor partials + the ADR 0104 chrome-hide
> idiom). It adds a **new signed-in display route** (`GET /kiosk`), a **new
> GlobalAdmin settings surface** (the playlist + intervals), a **new
> `client/lib/kiosk.ts`** auto-cycle / auto-refresh state machine (tsc-only),
> and a **closed `kiosk.*` `kw-l` key set** × en/de/fr/da. It adds **no new
> `AccessAction` / `AccessVia` / `Decide()` branch / `IAuditableResource`
> adapter** (M35·1 — the kiosk's per-view content rides the **frozen** read
> seams; the kiosk adds **no** authorization surface of its own).
>
> **U00** verifies the surface (the M5 `IProjectService` board-detail +
> todos-list read seams + the M4 `IEventService` calendar-range seam + the
> board / calendar / todos render partials + the ADR 0104
> `body.kmb-page-expanded` chrome-hide idiom + the `SiteContent` /
> `LocaleSettings` singleton shape as the config template + the ADR index —
> confirm **0164** is free) and authors the handoff-note skeleton. **U01/U02**
> author the primary-tier design doc (the 11 invariants M35·1–M35·11 + FACES
> M35-1–M35-10 + the `KioskPlaylist` / `KioskView` doc shapes + the
> `IKioskService` seam + the `/kiosk` display + the GlobalAdmin settings
> surface + the `client/lib/kiosk.ts` state machine + the closed `kiosk.*`
> `kw-l` key set + the pinned test names + the acceptance gate + the drift
> guard) and draft **ADR 0164**. **U03** implements the Core context
> (the `KioskPlaylist` doc + the `KioskDocTypes` registration + the boot
> wiring + the `IKioskService` seam + the `KioskService` impl). **U04**
> implements the `/kiosk` signed-in display (the controller + the view model +
> the chrome-hidden view). **U05** implements the GlobalAdmin settings surface
> (the playlist editor + the interval pickers). **U06** authors the **closed
> `kiosk.*` `kw-l` key set** × en/de/fr/da. **U07** implements the
> `client/lib/kiosk.ts` auto-cycle / auto-refresh state machine (tsc-only) +
> the `site.css` kiosk rules. **U08** implements the **pinned test pins**
> (the `Core.Tests` seam pins + the `Web.Tests` controller pins). **U09** runs
> + records the acceptance gate. **U10** flips the close (the six-member close
> flip; M35 is the **last** milestone — there is **no** next to promote).
>
> **Sizing:** units are sized for a **~32K-context fresh agent one at a
> time**, each with its own exit criteria, in the M33 / M34 style (≤ ~5 files
> / ~500 LOC, 4–8 entry reads, one build). **One new bounded context**
> (M35·1 — `Kumunita.Core.Kiosk`; the board / calendar / todos contexts are
> **reused**, not re-added). **One new doc** (`KioskPlaylist`, a singleton —
> the M35·2 pin) + **one new doc-type surface** (`KioskDocTypes`, the ADR
> 0004 §B.1 shape) + **one new service** (`IKioskService`, the M35·1 additive
> seam). **Zero new `AccessAction` / `AccessVia` / `Decide()` branch /
> `IAuditableResource` adapter** (M35·1 / M35·3 — the kiosk's per-view content
> rides the **frozen** board / calendar / todos read seams; the kiosk adds
> **no** authorization surface of its own). **The existing render surface is
> reused, not re-authored** (M35·6 — the kiosk renders the **same** board /
> calendar / todos Razor partials; the `MarkdownRenderer` / `kw-dt` / audience
> machinery is **untouched**). **Zero new dependency, zero new npm package,
> zero build step** (M35·7 — the `client/lib/kiosk.ts` is tsc-only, the ADR
> 0031 pin — the `expand-page.ts` / `calendar-fullscreen.ts` precedent;
> `setInterval` is the mechanism). **Core stays HTTP-free + Wolverine-free**
> (M35·8 — the `KioskService` is a plain store-composing service; the playlist
> read + the per-view content read are Wolverine-free reads; no `HttpClient` in
> Core, ADR 0006-D). **The two intervals are display-only, never an access
> input** (M35·4 — the M34·7 / M13 `windowDays` "display-only, never an access
> input" precedent). **M35 is the last milestone** — the close flip (U10)
> promotes **no** successor (M35·11).

## Understanding (one paragraph)

M1–M34 shipped the full resident surface: a directory, posts, events,
projects, boards, todos, a calendar, messaging, and every admin control the
operator needs. But a neighborhood's life is not only what each resident
reads at their own desk — a shared physical space (the community hall, the
library, the café) is where the neighborhood *sees itself*. Today, putting
Kumunita on a wall display means a human standing at the TV, clicking from one
feed to the next, and refreshing by hand. **M35 is the kiosk lane**: a
**full-page / full-screen display** (`GET /kiosk`) that **automatically cycles**
a **preset list of views** (a Kanban board, the calendar week, a second board,
the calendar day, the to-dos — the resident's own community surfaces) and
**refreshes each view's content on a separate, faster interval**. The playlist
+ the two intervals are a **GlobalAdmin-set singleton** (the `SiteContent` /
`LocaleSettings` config idiom), the display is **signed-in** (a kiosk account
on the TV), and each slide's content rides the **frozen** board / calendar /
todos read seams — **the same `Read` decision, re-run on every fetch**, so a
slide never reveals more than that viewer could already see on that surface's
own feed (the M12 iCal "authorization rides the cookie, re-run on every fetch"
precedent). M35 **creates** one new bounded context (`Kumunita.Core.Kiosk` —
one `KioskPlaylist` doc + one `KioskDocTypes` surface + one `IKioskService`)
and **reuses** everything else: the board / calendar / todos read seams, the
board / calendar / todos render partials, the ADR 0104 chrome-hide idiom, and
the `kw-l` registry. After M35, a kiosk account signs in on a wall display and
the neighborhood's own surfaces — the boards, the calendar, the to-dos — cycle
themselves, fresh.

## The one thing every unit must respect

**Kiosk-mode semantics (locked in ADR 0164, U00):**

- **M35 is a new bounded context `Kumunita.Core.Kiosk`, not a re-shape of an
  existing one (M35·1).** M35 **creates** a new context (the M31 `ErrorReports`
  / M11 `Portability` / M13 `Usage` precedent) with **one singleton doc**
  (`KioskPlaylist`, the M35·2 pin), **one doc-type surface** (`KioskDocTypes`,
  the ADR 0004 §B.1 shape), and **one service** (`IKioskService`, the M35·1
  additive seam). It **reuses** the **frozen** board / calendar / todos read
  seams (the M5 `IProjectService` board-detail + todos-list + the M4
  `IEventService` calendar-range seam — the M35·3 pin) and the **existing
  render surface** (the board / calendar / todos Razor partials — the M35·6
  pin). M35 adds **no** new `AccessAction`, **no** new `AccessVia`, **no**
  `Decide()` branch, and **no** `IAuditableResource` adapter (the M35·1 /
  M35·3 pins — the kiosk's per-view content rides the **frozen** read seams).
- **The playlist is a GlobalAdmin-set singleton (M35·2).** The
  `KioskPlaylist` doc is a **singleton** (the `SiteContent` / `LocaleSettings`
  / `SurfaceLabels` shape — the admin-surface config idiom, ADR 0004 §B.1). It
  carries the **ordered** list of `KioskView` entries + `ViewIntervalSeconds`
  + `RefreshIntervalSeconds`. The **standing to write it is GlobalAdmin**
  (the admin-surface precedent — M35-7 FACE). **No per-resident playlist in
  v1** (a named deferral, §Scope). A **missing** `KioskPlaylist` row reads as
  **empty** (the M10 `LocaleSettings` "a missing row reads as a sensible
  floor" precedent — M35-8 FACE renders the `kiosk.empty` message).
- **The display is signed-in and rides the frozen read seams (M35·3).**
  `GET /kiosk` is `[Authorize]`. Each slide's content is resolved by the
  **frozen** board / calendar / todos read seam for the **signed-in viewer**
  (the same `Read` decision as that surface's own feed — **re-run on every
  fetch**, the M12 iCal precedent). A slide the viewer **cannot see** (a board
  the viewer is not a member of) renders the `kiosk.slide_denied` empty state
  — **no content leak, no 404 crash** (M35-5 FACE). **No break-glass, no new
  standing, no new authorization branch.**
- **The two intervals are display-only, never an access input (M35·4).**
  `ViewIntervalSeconds` (the slide-advance cadence) + `RefreshIntervalSeconds`
  (the content-refresh cadence, **faster** than the view cadence) are
  **config** on the `KioskPlaylist` singleton — **never** a DB access column,
  **never** an authorization input (the M34·7 / M13 `windowDays`
  "display-only, never an access input" precedent, C-M7·2). Both are
  **GlobalAdmin-set** (M35·2).
- **One `kiosk.save` audit row per write; the display reads emit zero
  `kiosk.*` rows (M35·5).** The GlobalAdmin playlist write commits **one**
  `kiosk.save` `AccessAudit` row (`TargetKind = "kiosk"`, `Via = Admin` — the
  admin-surface write-lane convention). The `/kiosk` display's per-view
  content reads emit **zero** `kiosk.*` `AccessAudit` rows — the content
  rides the **underlying surface's own** audit rows (the board's / calendar's
  / todos' `Read` audit — the M34·4 / M13 D4 "the audit row is on the
  underlying surface" precedent). The `KioskPlaylist` **read** (config) emits
  **zero** rows (the M34·4 pin).
- **The existing render surface is reused, not re-authored (M35·6).** The
  kiosk's per-slide section renders the **same** board / calendar / todos
  Razor partials the board detail / calendar / todos index views use (the
  "integration over features" spine, `docs/philosophy/`). The `MarkdownRenderer`
  / `kw-dt` / audience / tag / draft machinery is **untouched**. The kiosk
  adds **only** a **chrome-hidden layout** (reusing the ADR 0104
  `body.kmb-page-expanded` chrome-hide idiom — the navbar / footer / flash /
  pinned-announcement are hidden, the slide fills the viewport) + the
  `client/lib/kiosk.ts` auto-cycle / auto-refresh state machine + the `kiosk.*`
  `kw-l` keys.
- **Zero new dependency, zero new npm package, zero build step (M35·7).** The
  `client/lib/kiosk.ts` is **tsc-only** (the ADR 0031 pin — the
  `expand-page.ts` / `calendar-fullscreen.ts` precedent). The auto-cycle +
  auto-refresh is a plain `setInterval` state machine (no carousel lib, no
  timer framework, no chart lib, no bundler, no `npm install`). `package.json`
  stays `typescript`-only.
- **Core stays HTTP-free + Wolverine-free (M35·8).** The `KioskService` is a
  plain store-composing service (the `AddTransient<IKioskService>` shape, the
  M31 `IErrorReportService` / M13 `IUsageAnalyticsService` precedent). The
  playlist read + the per-view content read are **Wolverine-free reads** over
  local Postgres documents. There is **no** `HttpClient` in Core (ADR
  0006-D) and **no** outbound channel.
- **No new bounded context beyond `Kiosk` (M35·9).** M35 **creates** the one
  new `Kiosk` context (M35·1); the board / calendar / todos contexts (M5
  `Projects` + M4 `Events`) are **reused, not re-added** (the `M5DocTypes` /
  `M4DocTypes` surfaces are **untouched**, M35·1 / M35·9). M35 adds **no** new
  doc **to an existing surface** and **no** new index to an existing doc.
- **The closed `kiosk.*` `kw-l` key set is four-language (M35·10).** Every new
  user-visible string M35 introduces is a `KnownTranslationKeys` entry present,
  **non-empty, in all four** languages (en/de/fr/da), pinned by
  `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` (the
  M34·11 / M33·11 closed-set precedent). The board / calendar / todos `kw-l`
  key sets are **untouched** (the M35·6 additive-only pin).
- **The six-member close flip is U10's responsibility (M35·11).** The
  `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip lands in U10 (the
  M34·12 / M34-U08 precedent). The `WhatsNew.cs` registry gains one new entry
  (newest-first, the `0.57.0` row) naming M35 + ADR 0164. **M35 is the last
  milestone** — the close flips `M35` from `StatusNext` to `StatusDone` and
  promotes **no** successor (the `Single_InProgress_Milestone_Is_M35` pin
  is **removed** and replaced with a "no in-progress milestone" assertion — the
  M34-U08 precedent).

## Assumptions

- **Scope (in):** the **`KioskPlaylist` singleton doc** (the ordered
  `IReadOnlyList<KioskView>` + `ViewIntervalSeconds` + `RefreshIntervalSeconds`
  — the M35·2 pin) + the **`KioskView` entry** (the closed 5-member shape,
  below) + the **`KioskDocTypes` registration surface** (the ADR 0004 §B.1
  shape — the M35·1 pin) + the **boot wiring** (the `Program.cs` dev-loop +
  the all-env `SchemaBootstrap` `KioskDocTypes.Configure` call, the M3
  `M3DocTypes` precedent) + the **`IKioskService` seam** (the 2-method
  surface — `GetPlaylistAsync` + `SavePlaylistAsync`) + the
  **`KioskService` impl** (the singleton read + the GlobalAdmin write with one
  `kiosk.save` audit row) + the **`KioskController`** (`GET /kiosk` the signed
  -in display + `GET /kiosk/slide` the single-slide refresh fetch) + the
  **`KioskDisplayViewModel`** + the **`Views/Kiosk/Index.cshtml`**
  chrome-hidden display + the **GlobalAdmin settings surface** (the playlist
  editor + the interval pickers, the `/admin/kiosk` section) + the **closed
  `kiosk.*` `kw-l` key set** × en/de/fr/da (the M35·10 pin) + the
  **`client/lib/kiosk.ts`** auto-cycle / auto-refresh state machine (tsc-only,
  the M35·7 pin) + the **`site.css` kiosk rules** (the chrome-hide + the
  slide-advance transition) + the **pinned test pins** (the M35·12 test model).
  **Out (named deferrals):** the **per-resident playlist** (v1 is a single
  GlobalAdmin-set community playlist — a per-resident override is a future
  lane), the **kiosk "lock"** (a kiosk-mode "exit only with a PIN /
  admin-token" to prevent a viewer leaving the kiosk surface — the browser's
  own back button still works in v1; a true kiosk lock is a future lane),
  **audio / ambient sound**, **per-slide dwell weighting** (v1 advances at a
  uniform `ViewIntervalSeconds` — a weighted dwell is a future lane), the
  **board / calendar / todos content** itself (M35 **reuses** the M5 / M4
  surfaces — it does **not** add any board / event / todo capability), the
  **iCal / portability / search** surfaces (out of kiosk scope — the kiosk
  cycles the **five** named views: boards, calendar, todos), and the
  `Milestones.cs` / README / `MilestonesTests` trio until the milestone *ships*
  (U10 owns the six-member close flip).
- **The `KioskPlaylist` + `KioskView` doc field set (locked by the design doc
  §2.2 pin).** The `KioskPlaylist` singleton carries exactly these members (the
  **M35·2 ceiling** — no field outside this set):
  - `Id` (string — **the singleton sentinel** `"kiosk"` — the ADR 0004 §B.1
    singleton idiom, the `SiteContent` / `LocaleSettings` `Id` precedent)
  - `Views` (`IReadOnlyList<KioskView>` — the **ordered** slide list; `Order`
    is the list position, not a stored field)
  - `ViewIntervalSeconds` (int — the slide-advance cadence, ≥ 5; the M35·4
    display-only pin)
  - `RefreshIntervalSeconds` (int — the content-refresh cadence, ≥ 2, and
    **< `ViewIntervalSeconds`** by the M35·4 pin; a blank / missing row reads
    as `30` / `10` defaults — the M10 `LocaleSettings` floor precedent)
  The `KioskView` entry carries exactly these **5** members (the **M35·2
  ceiling**):
  - `Kind` (`KioskViewKind` enum — `Board` | `Calendar` | `Todos` — the closed
    set; the "five named views" = boards (any), calendar (any view), todos)
  - `BoardId?` (string? — the board's id, **only when** `Kind == Board`)
  - `CalendarView?` (`string?` — `day` | `week` | `month`, **only when**
    `Kind == Calendar`; default `week`)
  - `ComponentId?` (string? — the community filter, **only when**
    `Kind == Todos`; `null` = all the viewer's visible todos)
  - `Label?` (string? — an optional per-slide display label; `null` = the
    surface's own header)
  Namespace `Kumunita.Core.Kiosk` (the new context's namespace).
- **The `IKioskService` seam (locked by the design doc §2.1 pin).**
  `Task<KioskPlaylist?> IKioskService.GetPlaylistAsync(CancellationToken
  ct = default);` (the singleton read — **zero** `AccessAudit` rows, M35·5 —
  a `null` return = no row configured, the M35-8 FACE floor) and
  `Task<KioskPlaylist> IKioskService.SavePlaylistAsync(IReadOnlyList<KioskView>
  views, int viewIntervalSeconds, int refreshIntervalSeconds, string actorId,
  IDocumentSession session, CancellationToken ct = default);` (the GlobalAdmin
  write — **one** `kiosk.save` `AccessAudit` row, `TargetKind = "kiosk"`,
  `Via = Admin` — the M35·5 pin; **within the caller's session** so the write
  is in-transaction with any surrounding work, the M3 `CreatePostAsync`
  same-transaction precedent). The `IKioskService` surface is **2 methods** in
  M35 (`GetPlaylistAsync` + `SavePlaylistAsync`).
- **The `KioskService` impl shape (locked by the design doc §2.2 pin).**
  `KioskService(IDocumentStore store, IAuthorizationService authz)`: (1)
  `GetPlaylistAsync` — one `QuerySession` + a `LoadAsync<KioskPlaylist>("kiosk")`
  (the ADR 0004 §B.1 singleton-read idiom, the `SiteContentService.GetAsync`
  shape); returns the row or `null`. **Zero** `AccessAudit` rows (M35·5).
  (2) `SavePlaylistAsync` — **within the caller's session**: a
  `session.LoadAsync<KioskPlaylist>("kiosk")` (the singleton upsert — a
  missing row is created), set the three members, `session.Store(...)`,
  **one** `kiosk.save` `AccessAudit` row (the write-lane convention, the M29
  `SurfaceLabels` save precedent), `await session.SaveChangesAsync()`. The
  **actor-standing gate** (GlobalAdmin) is enforced by the **controller**
  (`[Authorize(Roles = GlobalAdmin)]`), **not** the service (the admin-surface
  precedent — the service assumes an authorized actor, the controller enforces
  the role). **Zero** new `AccessAction` / `AccessVia` / `Decide()` branch
  (M35·1). **No** `HttpClient` in Core (M35·8, ADR 0006-D).
- **The `/kiosk` display + settings surface (locked by the design doc §2.2
  pin).** `KioskController` (namespace `Kumunita.Web`): (a)
  `GET /kiosk` `[Authorize]` — the signed-in display; builds a
  `KioskDisplayViewModel` (the playlist's `Views` + `ViewIntervalSeconds` +
  `RefreshIntervalSeconds` + the **first slide's** resolved content — the
  first `KioskView`'s board / calendar / todos content via the **frozen**
  read seam for the **signed-in viewer**); renders `Views/Kiosk/Index.cshtml`
  (the chrome-hidden layout + the first slide). (b)
  `GET /kiosk/slide?slide=N&t=<ts>` `[Authorize]` — the single-slide refresh
  fetch (the `t` timestamp busts any cache); returns the **Nth** slide's
  content (the `KioskView` at index `N`) via the **frozen** read seam; a
  slide the viewer cannot see returns a **denied-shape** view model (the
  `kiosk.slide_denied` message — M35-5 FACE — not a 404). (c)
  `GET /admin/kiosk` `[Authorize(Roles = GlobalAdmin)]` + `POST /admin/kiosk`
  — the GlobalAdmin settings surface (the playlist editor + the two interval
  pickers); a non-GlobalAdmin gets **403** (M35-7 FACE). The
  `KioskDisplayViewModel` carries the `Views` + the two intervals + the
  resolved first-slide content (the board / calendar / todos view model the
  slide renders). The **board / calendar / todos render partials are
  `@await Html.PartialAsync`'d verbatim** (the M35·6 pin — the kiosk does not
  re-author them).
- **The `client/lib/kiosk.ts` state machine (locked by the design doc §2.2
  pin).** A plain-tsc, no-dep, no-`any` (the ADR 0031 pin — the
  `expand-page.ts` / `calendar-fullscreen.ts` precedent) IIFE that: (1) reads
  the playlist + the two intervals from the server-rendered `data-*`
  attributes on `#kiosk-stage`; (2) **advances** the visible slide to the next
  (wrapping to the first) every `ViewIntervalSeconds` (a `setInterval`); (3)
  **re-fetches** the current slide's content from `GET /kiosk/slide?slide=N&
  t=<ts>` every `RefreshIntervalSeconds` (a second `setInterval`, a CSRF-free
  GET — no token needed, the M12 iCal "the cookie is the authorization"
  precedent); (4) swaps the `#kiosk-stage` inner content to the fetched
  slide (a same-origin `fetch` + `document.write`-free innerHTML swap over the
  **server-rendered** slide HTML — the response is a full slide partial, not
  JSON). **Zero** new npm package, **zero** build step (the M35·7 pin).
- **The `kw-l` key set is closed and four-language (~8 keys).** The
  `kiosk.*` keys are authored by U06 and consumed by U04's display + U05's
  settings surface + U07's TS. The closed set: `kiosk.title` /
  `kiosk.empty` / `kiosk.slide_denied` / `kiosk.next_slide` /
  `kiosk.refreshed` / `kiosk.settings.title` /
  `kiosk.settings.view_interval` / `kiosk.settings.refresh_interval`. Every key
  is present, non-empty, in **all four** languages (en/de/fr/da); the
  `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` closure
  pins it (the M35·10 pin). The `en` values are the source text (the ADR
  0015 D1 `kw-l` provider-floor discipline); the `de` / `fr` / `da` values are
  U06's to author (the M35·10 four-language pin). The board / calendar / todos
  `kw-l` key sets are **untouched** (the M35·6 additive-only pin).
- **The test model (pinned in the design doc §2.4).** The `Core.Tests` pins
  (the `KioskServiceTests` class, in `tests/Kumunita.Core.Tests/Kiosk/`):
  (a) `GetPlaylistAsync` over a **no row** store returns `null` (the M35·2 /
  M35-8 floor), (b) `GetPlaylistAsync` over a **configured** singleton returns
  the row (the M35·2 pin), (c) `GetPlaylistAsync` writes **zero**
  `AccessAudit` rows (the M35·5 pin), (d) `SavePlaylistAsync` commits **exactly
  one** `kiosk.save` `AccessAudit` row (`TargetKind = "kiosk"`, `Via = Admin`)
  — the M35·5 pin, (e) `SavePlaylistAsync` over a **no row** store creates the
  singleton (the upsert — the M35·2 pin), (f) `SavePlaylistAsync` **within the
  caller's session** commits atomically (the M3 same-transaction pin). The
  `Web.Tests` pins (the `KioskControllerTests` class, in
  `tests/Kumunita.Web.Tests/`): (a) a **signed-in** viewer visits `/kiosk` →
  the chrome-hidden layout + the first slide render (the M35·3 / M35·6 pin),
  (b) an **anonymous** visitor visits `/kiosk` → redirected to sign-in (the
  M35·3 / M35-6 pin), (c) `GET /kiosk/slide?slide=N` over a slide the viewer
  **cannot see** → the `kiosk.slide_denied` shape (no content, no 404 — the
  M35·5 / M35-5 pin), (d) a **non-GlobalAdmin** visits `/admin/kiosk` → 403
  (the M35·2 / M35-7 pin), (e) a **GlobalAdmin** POSTs `/admin/kiosk` → one
  `kiosk.save` audit row + the playlist persists (the M35·5 / M35-1 pin),
  (f) the **existing** board / calendar / todos surfaces render **unchanged**
  (a board detail / calendar / todos index visit is byte-identical — the
  M35·6 additive-only pin).
- **The terminal constraints in `AGENTS.md` and `copilot-instructions.md`
  bind** — no here-strings, no multi-line terminal commands, `$`-variables
  don't survive between commands, the `dotnet test` discovery bug on this
  machine (use the in-process `dotnet exec tests/…/bin/Debug/net10.0/*.dll`
  path).

## Approach

One track, **Core context + display + settings + kw-l + TS + test pins +
close**, sequenced. **U00** verifies the surface (the M5 `IProjectService`
board-detail + todos-list read seams + the M4 `IEventService` calendar-range
seam + the board / calendar / todos render partials + the ADR 0104
`body.kmb-page-expanded` chrome-hide idiom + the `SiteContent` singleton shape
as the config template + the ADR index — confirm **0164** is free) + authors
the handoff-note skeleton. **U01/U02** author the primary-tier design doc
(the 11 invariants M35·1–M35·11 + FACES M35-1–M35-10 + the `KioskPlaylist` /
`KioskView` doc shapes + the `IKioskService` seam + the `/kiosk` display + the
GlobalAdmin settings surface + the `client/lib/kiosk.ts` state machine + the
closed `kiosk.*` key set + the pinned test names + the acceptance gate + the
drift guard) and draft **ADR 0164**. **U03** implements the Core context
(the `KioskPlaylist` doc + the `KioskDocTypes` registration + the boot wiring
+ the `IKioskService` seam + the `KioskService` impl). **U04** implements the
`/kiosk` signed-in display (the controller + the view model + the
chrome-hidden view). **U05** implements the GlobalAdmin settings surface (the
playlist editor + the interval pickers). **U06** authors the **closed
`kiosk.*` `kw-l` key set** × en/de/fr/da. **U07** implements the
`client/lib/kiosk.ts` auto-cycle / auto-refresh state machine (tsc-only) + the
`site.css` kiosk rules. **U08** implements the **pinned test pins** (the
`Core.Tests` seam pins + the `Web.Tests` controller pins). **U09** runs +
records the acceptance gate. **U10** flips the close (the six-member close
flip; M35 is the **last** milestone — no successor to promote).

Every code unit ends with **build green** (`dotnet build Kumunita.slnx -c
Debug`). The last unit (U10) appends the final handoff section so the
milestone is honest.

## Workflow — handoff protocol for fresh-context agents

This milestone is executed as a sequence of **sealed units** (U00–U10
below), one unit per fresh agent with a ~32K context window.

**Shared state (three-tier contract):**
- **Primary — the design doc**
  (`docs/design/m35-kiosk-mode-design.md`, U01/U02 author) — pins
  the invariants (the 11: M35·1–M35·11), the FACES
  (M35-1–M35-10), the `KioskPlaylist` / `KioskView` doc shapes (the M35·2
  ceilings), the `IKioskService` seam, the `/kiosk` display + the GlobalAdmin
  settings surface, the `client/lib/kiosk.ts` state machine, the closed
  `kiosk.*` key set, the pinned test names, the acceptance gate, and the
  drift guard.
- **Secondary — this file**
  (`docs/plans-milestones/plan-m35-kiosk-mode.md`) — the unit registry with
  each unit's deliverables and exit criteria. (The flat-lane convention —
  the main plan sits at the top of `docs/plans-milestones/`, the unit plans
  sit in `docs/plans-milestones/in-progress/` as `m35-u00.md` …
  `m35-u10.md`, and move to `docs/plans-milestones/done/m35/` as each unit
  completes.)
- **Scratch — the rolling handoff note**
  (`docs/plans-milestones/in-progress/m35-handoff-notes.md`) — one section
  per unit, appended (never rewritten). Each unit writes exactly one short
  section before it exits; the next unit reads only that section + its own
  entry-reads list.

**Per-unit template** (each `U` below follows this): **Goal** (one
sentence, one or two related deliverables); **Entry reads** (the minimal
file list, 4–8 files < ~300 lines each, no full-repo scan; the design-doc
section cited is named); **Deliverables** (a closed set of new/modified
files, ≤ ~5 files / ~500 LOC, no misc cleanups); **Exit** (`dotnet build
Kumunita.slnx -c Debug` green for the touched projects; handoff-note entry
appended *before* any follow-up action).

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside the §drift-guard;
(3) never introduces a test whose exact name is not in the
§pinned-test-names list; (4) never re-shapes the `KioskPlaylist` / `KioskView`
doc field set outside the design doc §2.2 pin (the M35·2 ceilings — no field
outside the set); (5) never re-shapes the **frozen** board / calendar / todos
read seams (the M5 `IProjectService` board-detail + todos-list + the M4
`IEventService` calendar-range seam — the M35·3 / M35·9 "reused, not
re-shape" pin); (6) never adds a new `AccessAction`, `AccessVia`, `Decide()`
branch, `IAuditableResource` adapter, or a **second** `kiosk.*` `AccessAudit`
row (the M35·1 / M35·3 / M35·5 pins — the kiosk adds **no** authorization
surface of its own; the one write row is `kiosk.save`); (7) never puts a
Wolverine **handler** or an outbound `HttpClient` in `Kumunita.Core` (the
M35·8 pin — the `KioskService` is a plain store-composing service; no
`HttpClient` in Core, ADR 0006-D); (8) never adds a **new** doc **to an
existing** surface or a **new** index to an **existing** doc (the M35·9
pin — the M5 `Projects` + M4 `Events` contexts are **reused**, not re-added);
(9) never add a JS chart / carousel / timer library / a new npm package / a
build step (the M35·7 pin — the `client/lib/kiosk.ts` is tsc-only, the ADR
0031 pin); (10) never re-author the board / calendar / todos **Razor
partials** or the board / calendar / todos `kw-l` key set in a unit other than
the one that consumes them (the M35·6 additive-only pin — the kiosk renders
the **existing** partials, it does not re-author them); (11) never render a
slide's content that the **signed-in viewer** cannot already see on that
surface's own feed (the M35·3 / M35-5 pin — the frozen read seam is the
authority; a denied slide renders the `kiosk.slide_denied` shape, never a
404 crash, never a content leak); (12) never make the two intervals an
**access input** (the M35·4 pin — they are config, display-only); (13) if
entry reads reveal the design doc is out of date, the unit pauses and
records `## U<m> — Drift pause` in the handoff note.

---

## Units (11 total: U00–U10)

### U00 — Kickoff verification + handoff-note skeleton

- **Goal:** verify the surface (the M5 `IProjectService` board-detail +
  todos-list read seams + the M4 `IEventService` calendar-range seam + the
  board / calendar / todos render partials + the ADR 0104
  `body.kmb-page-expanded` chrome-hide idiom + the `SiteContent` /
  `LocaleSettings` singleton shape as the config template + the ADR index —
  confirm **0164** is free) and author the handoff-note skeleton (the
  "Milestone open" section). **No code, no build, no test.**
- **Entry reads:**
  `src/Kumunita.Core/Projects/IProjectService.cs` (the M5 seam M35 reuses —
  the board-detail + todos-list read methods the kiosk's per-slide content
  rides);
  `src/Kumunita.Core/Events/IEventService.cs` (the M4 seam M35 reuses — the
  `ListInRangeAsync` calendar-range read the kiosk's calendar slide rides);
  `src/Kumunita.Web/Controllers/ProjectsController.cs` (the `BoardDetail` +
  `TodosIndex` actions — the view-model shapes the kiosk's slides mirror);
  `src/Kumunita.Web/Controllers/EventsController.cs` (the calendar action —
  the view-model shape the kiosk's calendar slide mirrors);
  `src/Kumunita.Core/SiteContent/SiteContent.cs` +
  `src/Kumunita.Core/SiteContent/SiteContentService.cs` (the singleton doc +
  service shape the `KioskPlaylist` / `KioskService` mirror — the ADR
  0004 §B.1 singleton idiom);
  `src/Kumunita.Web/client/lib/expand-page.ts` +
  `src/Kumunita.Web/client/lib/calendar-fullscreen.ts` (the ADR 0104
  chrome-hide + fullscreen idiom the `kiosk.ts` reuses — the tsc-only,
  no-dep, no-`any` pin);
  `src/Kumunita.Core/Bootstrap/SchemaBootstrap.cs` +
  `src/Kumunita.Web/Program.cs` (the two boot paths U03 wires
  `KioskDocTypes.Configure` into — the M3 `M3DocTypes` precedent);
  `src/Kumunita.Web/Milestones.cs` (the `M35` row — confirm it is
  `StatusNext` + the **last** row — the M35·11 pin);
  `docs/adr/README.md` (the ADR index — confirm **0164** is free after the
  0163 row);
  `docs/plans-milestones/done/m34/plan-m34-analytics-history.md` (the M34
  register — the structural template for this register + the "last
  milestone" framing M35 shares).
- **Deliverables (1 file, new):**
  `docs/plans-milestones/in-progress/m35-handoff-notes.md` — the **skeleton
  only** (the header + the "Milestone open" section + the
  `<!-- U00 appends its section below this line. One ## section per unit, in
  order (U00, U01, … U10). Never rewrite a prior section. -->` marker). The
  skeleton mirrors the `m34-handoff-notes.md` shape (the "Milestone open"
  section names the register, the design doc, the ADR, the scope, the
  out-of-scope deferrals, and the frozen base — the M5 `IProjectService`
  board-detail + todos-list read seams are **reused** (M35·3 / M35·9), the M4
  `IEventService` calendar-range seam is **reused** (M35·3 / M35·9), the board
  / calendar / todos **Razor partials** are **reused** (M35·6), the ADR 0104
  chrome-hide idiom is **reused** (M35·6), the `SiteContent` /
  `LocaleSettings` singleton shape is **the structural template** the
  `KioskPlaylist` / `KioskService` mirror, and M35 **creates** the one new
  `Kiosk` context (M35·1)).
- **Exit:** the handoff-note skeleton is present. The `## Milestone open`
  section names (a) the M5 `IProjectService` board-detail + todos-list read
  seams (the M35·3 / M35·9 reuse), (b) the M4 `IEventService` calendar-range
  seam (the M35·3 / M35·9 reuse), (c) the board / calendar / todos Razor
  partials (the M35·6 reuse), (d) the ADR 0104 chrome-hide idiom (the M35·6
  reuse), (e) the `SiteContent` / `LocaleSettings` singleton shape (the
  structural template), (f) the `M35` row in `Milestones.cs` (the
  `StatusNext` **last** row — the M35·11 pin), (g) the **ADR 0164** (the next
  free number after 0163 — the ADR index in `docs/adr/README.md` confirms 0163
  is the current highest).
  Handoff note: a `## U00 — Kickoff verified` section with the current surface
  shape (the M5 `IProjectService` board-detail + todos-list method names, the
  M4 `IEventService.ListInRangeAsync` signature, the board / calendar / todos
  partial file paths, the ADR 0104 `body.kmb-page-expanded` class name, the
  `SiteContent` singleton `Id` value + the `KioskPlaylist` proposed `Id`
  sentinel `"kiosk"`, the ADR number [0164] + the precedent ADR list [0004 §B.1,
  0031 [the tsc-only pin], 0104 [the chrome-hide idiom], 0015 [the `kw-l`
  registry], 0006-D [Core HTTP-free]]). Move this unit plan
  `in-progress/m35-u00.md` → `done/m35/` (move **last**). `git status`
  clean except the one new handoff-note file.

### U01 — Design doc Part 1 (context, scope, invariants, FACES)

- **Goal:** author `docs/design/m35-kiosk-mode-design.md` Part 1 —
  **Context, Scope (in/out incl. the named deferral list), Invariants pinned
  for M35 (the 11: M35·1–M35·11), FACES (10)**. **No code, no build.**
- **Entry reads:**
  `docs/plans-milestones/plan-m35-kiosk-mode.md` (this register — the
  Understanding, the "one thing" section, the Assumptions);
  `docs/plans-milestones/done/m34/plan-m34-analytics-history.md` (the M34
  register — the structural template + the "last milestone" framing);
  `docs/design/m13-logging-analytics-design.md` (the M13 design doc — the
  FACES/invariant template to emulate);
  `docs/design/m34-analytics-history-design.md` (the M34 design doc — the
  "reused, not re-shape" + "display-only, never an access input" framing);
  `src/Kumunita.Core/SiteContent/SiteContent.cs` +
  `src/Kumunita.Core/SiteContent/SiteContentService.cs` (the singleton doc +
  service shape the `KioskPlaylist` / `KioskService` mirror);
  `src/Kumunita.Core/Projects/IProjectService.cs` +
  `src/Kumunita.Core/Events/IEventService.cs` (the frozen read seams the
  kiosk rides);
  `src/Kumunita.Web/client/lib/expand-page.ts` (the ADR 0104 chrome-hide
  idiom the `kiosk.ts` reuses);
  `docs/adr/0104-full-page-and-calendar-fullscreen-modes.md` (the ADR 0104 —
  the full-page expand + calendar fullscreen the kiosk's chrome-hide reuses);
  `docs/adr/0162-analytics-history.md` +
  `docs/adr/0163-home-upcoming-events-and-todos.md` (the two newest ADRs —
  the format to mirror for ADR 0164 + the "display-only, per-resident
  override" framing).
- **Deliverables (1 file, new):**
  `docs/design/m35-kiosk-mode-design.md` (~220 lines). Sections:
  - `## Context` — M1–M34 shipped the full resident surface; the kiosk is the
    **shared physical display** the neighborhood sees itself on. M35 adds a
    **full-page / full-screen display** (`GET /kiosk`) that **auto-cycles** a
    **GlobalAdmin-set** playlist of the resident's own surfaces (boards,
    calendar, todos) and **auto-refreshes** each slide's content. M35
    **creates** the one new `Kiosk` context (M35·1) and **reuses** the frozen
    board / calendar / todos read seams + render partials + the ADR 0104
    chrome-hide idiom (M35·3 / M35·6). M35 is the **last** milestone (M35·11).
  - `## Scope` — **In:** the `KioskPlaylist` singleton doc (the M35·2
    ceiling), the `KioskView` entry (the 5-member M35·2 ceiling), the
    `KioskDocTypes` registration surface (M35·1), the boot wiring, the
    `IKioskService` 2-method seam (M35·1), the `KioskService` impl (the
    singleton read + the GlobalAdmin write with one `kiosk.save` audit row,
    M35·5), the `KioskController` (`GET /kiosk` + `GET /kiosk/slide` +
    `GET/POST /admin/kiosk`), the `KioskDisplayViewModel`, the
    `Views/Kiosk/Index.cshtml` chrome-hidden display, the `client/lib/
    kiosk.ts` state machine (tsc-only, M35·7), the `site.css` kiosk rules,
    the closed `kiosk.*` `kw-l` key set × en/de/fr/da (M35·10), the pinned
    test pins (M35·12). **Out (named deferrals):** the per-resident playlist,
    the kiosk "lock", audio, per-slide dwell weighting, the board / calendar /
    todos content itself (reused, not re-added), the iCal / portability /
    search surfaces, and the `Milestones.cs` / README / `MilestonesTests` trio
    until the milestone *ships* (U10 owns it).
  - `## Invariants (pinned for M35)` — the **11 invariants**
    (M35·1–M35·11), each with a one-line M35 note (verbatim from the
    register's "one thing" section).
  - `## FACES (pinned, 10)` — M35-1 through M35-10, each bound to
    invariants:
    - **M35-1** a GlobalAdmin sets the kiosk playlist (a board + a calendar
      week + todos, with view + refresh intervals) → it saves (one
      `kiosk.save` audit row) + `/kiosk` renders the first slide — M35·2,
      M35·5
    - **M35-2** a signed-in resident opens `/kiosk` → slide 1 renders (the
      first playlist entry's content, via the viewer's own read decision) —
      M35·3, M35·6
    - **M35-3** the kiosk auto-advances to the next slide after
      `ViewIntervalSeconds` (wrapping to the first) — M35·4, M35·7
    - **M35-4** the current slide's content auto-refreshes after
      `RefreshIntervalSeconds` (re-fetched via the viewer's own read
      decision) — M35·4, M35·3
    - **M35-5** a slide the viewer **cannot see** → renders the
      `kiosk.slide_denied` empty state (no content leak, no 404 crash) —
      M35·3
    - **M35-6** an **anonymous** visitor opens `/kiosk` → redirected to
      sign-in — M35·3
    - **M35-7** a non-GlobalAdmin (signed-in resident) opens the settings
      surface → 403 — M35·2
    - **M35-8** the playlist is **empty** (no slides configured) → `/kiosk`
      renders the `kiosk.empty` message — M35·10
    - **M35-9** the `KioskPlaylist` read + the `/kiosk` display reads emit
      **zero** `kiosk.*` `AccessAudit` rows (the content rides the underlying
      surface's audit) — M35·5
    - **M35-10** the existing board / calendar / todos render surfaces are
      **unchanged** (the M35·6 additive-only pin) — M35·6
- **Exit:** file exists with all sections. **No build.** Handoff note: 5–6
  lines starting `## U01 — design doc Part 1`, listing the **11 invariants**
  (by id) and the **10 FACES** (M35-1–M35-10) so U02 can pin them by id.

### U02 — Design doc Part 2 (seams, contracts, test names, gate, drift-guard) + ADR 0164 Draft

- **Goal:** append `## Seams & contracts (Part 2, written by U02)` to the
  design doc — the exact C# shapes U03–U08 must match, the closed `kiosk.*`
  `kw-l` key set, the **pinned seam-test names**, the **three-test acceptance
  gate**, and the **drift-guard**. Draft **ADR 0164**. **No code, no build.**
- **Entry reads:**
  U01's Part 1 (the invariant table is the primary source);
  `src/Kumunita.Core/SiteContent/SiteContent.cs` +
  `src/Kumunita.Core/SiteContent/SiteContentService.cs` +
  `src/Kumunita.Core/SiteContent/SiteContentDocTypes.cs` (the singleton doc +
  service + doc-type-surface shape the `KioskPlaylist` / `KioskService` /
  `KioskDocTypes` mirror — all three);
  `src/Kumunita.Core/Projects/IProjectService.cs` +
  `src/Kumunita.Core/Events/IEventService.cs` (the frozen read seams the
  kiosk rides — the board-detail + todos-list + calendar-range signatures);
  `src/Kumunita.Web/Controllers/ProjectsController.cs` +
  `src/Kumunita.Web/Controllers/EventsController.cs` (the `BoardDetail` +
  `TodosIndex` + calendar actions — the view-model shapes the kiosk's slides
  mirror);
  `src/Kumunita.Core/DependencyInjection.cs` (where the
  `IKioskService` registration goes — the M31 `IErrorReportService` / M13
  `IUsageAnalyticsService` `AddTransient` precedent);
  `src/Kumunita.Web/Program.cs` +
  `src/Kumunita.Core/Bootstrap/SchemaBootstrap.cs` (the two boot paths U03
  wires `KioskDocTypes.Configure` into);
  `docs/adr/0162-analytics-history.md` +
  `docs/adr/0163-home-upcoming-events-and-todos.md` (the two newest ADRs —
  the format to mirror for ADR 0164);
  `docs/adr/README.md` (the ADR index — the 0164 row to add);
  `docs/plans-milestones/done/m34/plan-m34-analytics-history.md` §U02 (the
  M34 Part 2 — the structural template).
- **Deliverables (2 files: 1 append + 1 new):**
  - `docs/design/m35-kiosk-mode-design.md` (append). Sub-sections:
    - `### 2.1 frozen seam list (exact C#)` — the M35 **reused** frozen seams
      (verbatim): the M5 `IProjectService` board-detail + todos-list read
      methods + the M4 `IEventService.ListInRangeAsync` signature (the M35·3 /
      M35·9 "reused, not re-shape" pin — M35 adds **no** seam to them).
      **Plus** the M35 **new** `IKioskService` seam (the 2-method surface):
      `Task<KioskPlaylist?> IKioskService.GetPlaylistAsync(CancellationToken
      ct = default);` + `Task<KioskPlaylist> IKioskService.SavePlaylistAsync
      (IReadOnlyList<KioskView> views, int viewIntervalSeconds, int
      refreshIntervalSeconds, string actorId, IDocumentSession session,
      CancellationToken ct = default);` (the M35·1 / M35·5 pin — the write
      commits one `kiosk.save` audit row).
    - `### 2.2 new M35-owned Core types (exact C#)` — the `KioskPlaylist`
      singleton (the 4-member M35·2 ceiling — `Id` (the sentinel `"kiosk"`) +
      `Views` (`IReadOnlyList<KioskView>`) + `ViewIntervalSeconds` (int, ≥ 5)
      + `RefreshIntervalSeconds` (int, ≥ 2, < `ViewIntervalSeconds`)). The
      `KioskView` entry (the 5-member M35·2 ceiling — `Kind`
      (`KioskViewKind` enum: `Board` | `Calendar` | `Todos`) + `BoardId?` +
      `CalendarView?` (`day`/`week`/`month`, default `week`) + `ComponentId?`
      + `Label?`). The `KioskViewKind` enum. The `KioskDocTypes.Configure`
      registration surface (the ADR 0004 §B.1 shape — one
      `.Schema.For<KioskPlaylist>()`). The `KioskService(IDocumentStore,
      IAuthorizationService)` impl (the `GetPlaylistAsync` singleton read +
      the `SavePlaylistAsync` upsert-with-one-audit-row — the M35·5 pin).
      **No** new `AccessAction` / `AccessVia` / `Decide()` branch /
      `IAuditableResource` adapter (the M35·1 / M35·3 pin). **No** `HttpClient`
      in Core (the M35·8 pin, ADR 0006-D).
    - `### 2.3 the `/kiosk` display + settings surface (exact Web shapes)` —
      the `KioskController` actions (the `GET /kiosk` + `GET /kiosk/slide` +
      `GET/POST /admin/kiosk` — the M35·3 / M35·2 / M35·7 pins) + the
      `KioskDisplayViewModel` (the `Views` + the two intervals + the resolved
      first-slide content) + the `Views/Kiosk/Index.cshtml` chrome-hidden
      layout (the ADR 0104 `body.kmb-page-expanded` idiom — the M35·6 pin) +
      the `client/lib/kiosk.ts` state machine (the two `setInterval`s — the
      M35·7 pin) + the `site.css` kiosk rules. The board / calendar / todos
      **Razor partials are `@await Html.PartialAsync`'d verbatim** (the M35·6
      pin — the kiosk does not re-author them).
    - `### 2.4 the closed `kiosk.*` `kw-l` key set` — the ~8 keys (verbatim
      from the register's Assumptions), each with the en value (the de/fr/da
      values are U06's to author).
    - `### 2.5 pinned seam tests (exact names)` — file
      `tests/Kumunita.Core.Tests/Kiosk/KioskServiceTests.cs`:
      1. `M35_2_GetPlaylist_NoRow_ReturnsNull`
      2. `M35_2_GetPlaylist_Configured_ReturnsRow`
      3. `M35_5_GetPlaylist_WritesNoAuditRow`
      4. `M35_5_SavePlaylist_OneKioskSaveAuditRow`
      5. `M35_2_SavePlaylist_NoRow_CreatesSingleton`
      6. `M35_SavePlaylist_AtomicInCallerSession`
      File `tests/Kumunita.Web.Tests/KioskControllerTests.cs`:
      7. `M35_3_SignedIn_Sees_ChromeHidden_FirstSlide`
      8. `M35_3_Anonymous_RedirectedToSignIn`
      9. `M35_5_SlideDenied_SignsInViewer_SeesDeniedShape`
      10. `M35_7_NonGlobalAdmin_AdminKiosk_403`
      11. `M35_5_GlobalAdmin_SavePlaylist_OneAuditRow_Persists`
      12. `M35_6_ExistingSurfaces_RenderUnchanged`
    - `### 2.6 acceptance gate (U09 records)` — the three-test shape:
      **closed loop** (a `GlobalAdmin` sets the playlist → `/kiosk` renders
      the first slide for a signed-in viewer), **handoff** (a signed-in
      viewer's `/kiosk` slide resolves via the **frozen** board / calendar /
      todos read seam — the same `Read` decision as that surface's own feed —
      so the slide is a "handoff" from the underlying surface to the display),
      **part-vs-whole** (the 12-test pin list is the whole; closed-loop +
      handoff are the parts; all must pass together).
    - `### 2.7 drift-guard (frozen once written)` — the 11-invariant table
      (U01), the 10 FACES (U01), the `IKioskService` 2-method seam, the
      `KioskPlaylist` / `KioskView` shapes, the `/kiosk` display + settings
      surface, the `client/lib/kiosk.ts` state machine, the closed
      `kiosk.*` key set, and the 12 test names — all frozen pins; any
      mismatch is a `## U<m> — Drift pause` per unit-series rule §13.
  - `docs/adr/0164-kiosk-mode.md` (new, **Draft**) — the ADR 0164, mirroring
    the ADR 0162 / 0163 shape but with the **"new context + reused read seams
    + chrome-hidden display"** framing (the M35·1 / M35·3 / M35·6 pins).
    Status: **Draft** (U10 flips it to `Accepted` at the close).
- **Exit:** file exists with all Part 2 sub-sections + the ADR 0164 Draft is
  present. **No build.** Handoff note: 6–8 lines starting
  `## U02 — design doc Part 2 + ADR 0164 Draft`, listing (a) the reused frozen
  seam signatures (the M5 board-detail + todos-list + the M4
  `ListInRangeAsync`), (b) the new `IKioskService` 2-method seam (the two
  lines), (c) the `KioskPlaylist` 4-member + `KioskView` 5-member ceilings,
  (d) the 12 test names by id, (e) the three-test gate (by name), (f) the
  ADR 0164 Draft path + the "new context + reused read seams" framing (the
  M35·1 / M35·3 pins), (g) the closed `kiosk.*` key set count (~8).

### U03 — Core: the `KioskPlaylist` doc + `KioskDocTypes` + boot wiring + `IKioskService` seam + `KioskService` impl

- **Goal:** create the new `Kumunita.Core.Kiosk` context — the
  `KioskPlaylist` + `KioskView` + `KioskViewKind` docs (namespace
  `Kumunita.Core.Kiosk`), the `KioskDocTypes` registration surface, the boot
  wiring (the `Program.cs` dev-loop + the all-env `SchemaBootstrap`
  `KioskDocTypes.Configure` call), the `IKioskService` 2-method seam, and the
  `KioskService` impl (the singleton read + the GlobalAdmin write with one
  `kiosk.save` audit row). **No** new `AccessAction` / `AccessVia` /
  `Decide()` branch / `IAuditableResource` adapter (the M35·1 / M35·3 pin).
  **No** `HttpClient` in Core (the M35·8 pin). Mirrors the `SiteContent` /
  `LocaleSettings` singleton shape (the structural template).
- **Entry reads:**
  `docs/design/m35-kiosk-mode-design.md` §2.1 + §2.2 (the exact seam + doc
  shapes + the impl shape);
  `src/Kumunita.Core/SiteContent/SiteContent.cs` +
  `src/Kumunita.Core/SiteContent/SiteContentService.cs` +
  `src/Kumunita.Core/SiteContent/SiteContentDocTypes.cs` (the singleton doc +
  service + doc-type-surface shape the M35 docs mirror — all three);
  `src/Kumunita.Core/SiteContent/ISiteContentService.cs` (the service-seam
  shape the `IKioskService` mirrors);
  `src/Kumunita.Core/Bootstrap/SchemaBootstrap.cs` +
  `src/Kumunita.Web/Program.cs` (the two boot paths U03 wires
  `KioskDocTypes.Configure` into — the M3 `M3DocTypes` precedent);
  `src/Kumunita.Core/DependencyInjection.cs` (where the
  `IKioskService` registration goes — the M31 `IErrorReportService` / M13
  `IUsageAnalyticsService` `AddTransient` precedent);
  `src/Kumunita.Core/Authorization/IAuthorizationService.cs` (the audit-row
  write convention the `SavePlaylistAsync` one-row uses — the `AccessVia.
  Admin` + the `TargetKind` string).
- **Deliverables (≤ 6 files):**
  - `src/Kumunita.Core/Kiosk/KioskViewKind.cs` — the closed enum
    (`Board` | `Calendar` | `Todos`), the M35·2 pin.
  - `src/Kumunita.Core/Kiosk/KioskView.cs` — the 5-member entry (the M35·2
    ceiling): `Kind` + `BoardId?` + `CalendarView?` + `ComponentId?` +
    `Label?`. A doc-comment pins the M35·2 ceiling + the M35·3 "reused, not
    re-shape" pin.
  - `src/Kumunita.Core/Kiosk/KioskPlaylist.cs` — the 4-member singleton (the
    M35·2 ceiling): `Id` (the sentinel `"kiosk"`) + `Views`
    (`IReadOnlyList<KioskView>`) + `ViewIntervalSeconds` (int, ≥ 5) +
    `RefreshIntervalSeconds` (int, ≥ 2). A doc-comment pins the M35·2 /
    M35·4 pins.
  - `src/Kumunita.Core/Kiosk/KioskDocTypes.cs` — the ADR 0004 §B.1
    registration surface: `public static class KioskDocTypes { public static
    void Configure(StoreOptions opts) { opts.Schema.For<KioskPlaylist>(); } }`
    (one `.Schema.For` — the M35·1 / M35·9 pin).
  - `src/Kumunita.Core/Kiosk/IKioskService.cs` — the 2-method seam (the
    M35·1 / M35·5 pin): `GetPlaylistAsync` + `SavePlaylistAsync` (exact
    signatures from the design doc §2.1).
  - `src/Kumunita.Core/Kiosk/KioskService.cs` — the impl (the
    `GetPlaylistAsync` singleton read — zero audit rows, M35·5 — + the
    `SavePlaylistAsync` upsert-with-one-`kiosk.save`-audit-row — the M35·5
    pin, within the caller's session). Constructor:
    `(IDocumentStore store, IAuthorizationService authz)`. **No** `HttpClient`
    (the M35·8 pin). **No** `AccessAction` / `AccessVia` / `Decide()` branch /
    `IAuditableResource` adapter (the M35·1 / M35·3 pin).
  - `src/Kumunita.Core/Bootstrap/SchemaBootstrap.cs` — add the `using` + the
    `KioskDocTypes.Configure(opts);` line *next to* the existing doc-type
    `Configure` calls (one line added).
  - `src/Kumunita.Web/Program.cs` — add the `KioskDocTypes.Configure(...)`
    call in the dev-loop path, next to the existing doc-type `Configure` calls
    (one line added).
  - `src/Kumunita.Core/DependencyInjection.cs` — add the `IKioskService`
    registration: `services.AddTransient<IKioskService>(sp => new
    KioskService(sp.GetRequiredService<IDocumentStore>(),
    sp.GetRequiredService<IAuthorizationService>()));` — the M31 / M13
    `AddTransient` shape.
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The `Kiosk` context
  compiles; the `IKioskService` seam + `KioskService` impl are present; the
  `KioskDocTypes` + boot wiring + DI registration are wired. **No new test**
  (U08's test pins are the first M35 tests). **No** new `AccessAction` /
  `AccessVia` / `Decide()` branch / `IAuditableResource` adapter (the M35·1 /
  M35·3 pin). **No** `HttpClient` in Core (the M35·8 pin). Handoff note: 5–6
  lines starting `## U03 — Kiosk context + IKioskService` — (a) the
  `IKioskService` public-method count (**2**), (b) the `KioskPlaylist` 4-member
  + `KioskView` 5-member ceilings, (c) the `KioskDocTypes` line count (1
  `.Schema.For`), (d) the two boot-path lines added (file + line numbers),
  (e) the DI registration line, (f) the confirmation that **no**
  `AccessAction` / `AccessVia` / `Decide()` branch / `IAuditableResource`
  adapter was added (the M35·1 / M35·3 pin) + **no** `HttpClient` in Core (the
  M35·8 pin), (g) any compile warnings on the new types.

### U04 — Web: the `KioskController` + `KioskDisplayViewModel` + `Views/Kiosk/Index.cshtml` chrome-hidden display

- **Goal:** implement the `/kiosk` signed-in display — the
  `KioskController` (`GET /kiosk` + `GET /kiosk/slide`), the
  `KioskDisplayViewModel`, and the `Views/Kiosk/Index.cshtml` chrome-hidden
  layout (the ADR 0104 `body.kmb-page-expanded` idiom — the navbar / footer /
  flash / pinned-announcement hidden, the slide fills the viewport). The
  per-slide section renders the **same** board / calendar / todos Razor
  partials the board detail / calendar / todos index views use (the M35·6
  pin — the kiosk does not re-author them). **No** new `AccessAction` /
  `AccessVia` / `Decide()` branch (the M35·1 / M35·3 pin). **No** new JS /
  npm package (the M35·7 pin).
- **Entry reads:**
  `docs/design/m35-kiosk-mode-design.md` §2.3 (the display + settings surface
  pin);
  `src/Kumunita.Core/Kiosk/IKioskService.cs` +
  `src/Kumunita.Core/Kiosk/KioskPlaylist.cs` +
  `src/Kumunita.Core/Kiosk/KioskView.cs` (U03's seam + docs — the `@model`
  directive must match exactly);
  `src/Kumunita.Web/Controllers/ProjectsController.cs` (the `BoardDetail` +
  `TodosIndex` actions — the view-model shapes the kiosk's board + todos
  slides mirror + the partials the kiosk renders);
  `src/Kumunita.Web/Controllers/EventsController.cs` (the calendar action —
  the view-model shape the kiosk's calendar slide mirrors + the partial the
  kiosk renders);
  `src/Kumunita.Web/Views/Projects/BoardDetail.cshtml` +
  `src/Kumunita.Web/Views/Projects/TodosIndex.cshtml` +
  `src/Kumunita.Web/Views/Events/Calendar.cshtml` (the three render partials
  the kiosk `@await Html.PartialAsync`s — the M35·6 pin);
  `src/Kumunita.Web/client/lib/expand-page.ts` (the ADR 0104 chrome-hide
  idiom the `kiosk.ts` reuses — the `body.kmb-page-expanded` class + the
  `.page-expand-toggle` / `.page-expand-close` selectors);
  `src/Kumunita.Web/Views/Shared/_Layout.cshtml` (the chrome the kiosk
  hides — the navbar / footer / flash / pinned-announcement).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Web/Controllers/KioskController.cs` — the thin controller.
    (a) `GET /kiosk` `[Authorize]` — reads the `KioskPlaylist` via
    `IKioskService.GetPlaylistAsync` (a `null` / empty row → the
    `kiosk.empty` message, the M35-8 FACE); builds the
    `KioskDisplayViewModel` (the `Views` + the two intervals + the **first
    slide's** resolved content — the first `KioskView`'s board / calendar /
    todos content via the **frozen** read seam for the **signed-in viewer**);
    renders `Views/Kiosk/Index.cshtml`. (b) `GET /kiosk/slide?slide=N&t=
    <ts>` `[Authorize]` — returns the **Nth** slide's content (the
    `KioskView` at index `N`) via the **frozen** read seam; a slide the
    viewer cannot see returns a **denied-shape** view model (the
    `kiosk.slide_denied` message — M35-5 FACE — **not** a 404). **No** new
    `AccessAction` / `AccessVia` / `Decide()` branch (the M35·1 / M35·3
    pin — the frozen read seam is the authority).
  - `src/Kumunita.Web/Models/KioskDisplayViewModel.cs` — the display model:
    `IReadOnlyList<KioskView> Views` + `int ViewIntervalSeconds` + `int
    RefreshIntervalSeconds` + the resolved first-slide content (the board /
    calendar / todos view model the slide renders). A `bool HasSlides`
    member (the `kiosk.empty` gate). A `string? DeniedLabel` member (the
    `kiosk.slide_denied` shape).
  - `src/Kumunita.Web/Views/Kiosk/Index.cshtml` — the chrome-hidden display.
    A `body.kmb-page-expanded` class (the ADR 0104 chrome-hide idiom — the
    M35·6 pin) + a `#kiosk-stage` element (the `data-slides` /
    `data-view-interval` / `data-refresh-interval` `data-*` attributes the
    `kiosk.ts` reads) + the first slide's content (the board / calendar /
    todos partial `@await Html.PartialAsync`'d verbatim — the M35·6 pin) +
    the `kiosk.*` `kw-l` keys (the `kiosk.title` / `kiosk.empty` /
    `kiosk.slide_denied` / `kiosk.next_slide` / `kiosk.refreshed`) + a
    `.page-expand-close`-style exit affordance (the M35·6 pin — the ADR 0104
    idiom). **No** new JS, **no** new npm package (the M35·7 pin).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The
  `KioskController` + `KioskDisplayViewModel` + `Views/Kiosk/Index.cshtml`
  are present. The board / calendar / todos partials are **untouched** (the
  M35·6 additive-only pin). Handoff note: 5–6 lines starting
  `## U04 — /kiosk display` — (a) the `KioskController` action count (2:
  `Index` + `Slide`), (b) the `KioskDisplayViewModel` member count (the
  `Views` + the 2 intervals + the first-slide content + `HasSlides` +
  `DeniedLabel`), (c) the `#kiosk-stage` `data-*` attribute names (the
  `kiosk.ts` reads them), (d) the board / calendar / todos partial paths the
  kiosk `@await`s (the M35·6 pin), (e) the confirmation that **no**
  `AccessAction` / `AccessVia` / `Decide()` branch was added (the M35·1 /
  M35·3 pin) + **no** JS / npm package was added (the M35·7 pin), (f) any
  compile warnings.

### U05 — Web: the GlobalAdmin settings surface (the playlist editor + the interval pickers)

- **Goal:** implement the GlobalAdmin settings surface — `GET /admin/kiosk`
  + `POST /admin/kiosk` on the `KioskController` (the playlist editor + the
  two interval pickers). A non-GlobalAdmin gets **403** (the M35-7 FACE).
  The write commits **one** `kiosk.save` audit row (the M35·5 pin). **No**
  new `AccessAction` / `AccessVia` / `Decide()` branch (the M35·1 / M35·3
  pin). **No** new JS / npm package (the M35·7 pin).
- **Entry reads:**
  `docs/design/m35-kiosk-mode-design.md` §2.3 (the settings-surface pin);
  `src/Kumunita.Web/Controllers/KioskController.cs` (U04's controller — the
  `KioskDisplayViewModel` shape + the `IKioskService` injection the settings
  surface reuses);
  `src/Kumunita.Web/Controllers/AdminController.cs` +
  `src/Kumunita.Web/Views/Admin/SiteContent.cshtml` (the GlobalAdmin
  settings-surface idiom — the `[Authorize(Roles = GlobalAdmin)]` + the
  form-POST shape the `/admin/kiosk` mirrors);
  `src/Kumunita.Web/Views/Shared/_AdminNav.cshtml` (the admin nav the
  `/admin/kiosk` entry goes into);
  `src/Kumunita.Core/Kiosk/IKioskService.cs` +
  `src/Kumunita.Core/Kiosk/KioskView.cs` (U03's seam + the `KioskView`
  shape the editor binds);
  `src/Kumunita.Core/Projects/IProjectService.cs` (the board-list read the
  editor's board picker seeds — the frozen seam, the M35·3 / M35·9 pin).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Web/Controllers/KioskController.cs` — **append** the
    settings actions (the M35·2 / M35·7 pin): (a) `GET /admin/kiosk`
    `[Authorize(Roles = GlobalAdmin)]` — reads the `KioskPlaylist` via
    `GetPlaylistAsync` + seeds the board picker (the M5 `IProjectService`
    board-list read — the frozen seam); builds the
    `KioskSettingsViewModel`; renders `Views/Kiosk/Settings.cshtml`.
    (b) `POST /admin/kiosk` `[Authorize(Roles = GlobalAdmin)]` — binds the
    `KioskSettingsViewModel` + validates (the two intervals ≥ their floors,
    the `Views` list non-empty-or-empty-is-legal); calls
    `IKioskService.SavePlaylistAsync` within
    `IDocumentStore.LightweightSession()` (the one `kiosk.save` audit row —
    the M35·5 pin); redirects to `GET /admin/kiosk` with a flash. A
    non-GlobalAdmin gets **403** (the `[Authorize(Roles = GlobalAdmin)]` —
    the M35-7 FACE). **No** new `AccessAction` / `AccessVia` / `Decide()`
    branch (the M35·1 / M35·3 pin).
  - `src/Kumunita.Web/Models/KioskSettingsViewModel.cs` — the settings
    model: `IReadOnlyList<KioskView> Views` + `int ViewIntervalSeconds` +
    `int RefreshIntervalSeconds` + `IReadOnlyList<(string Id, string Name)>
    Boards` (the board picker) + `bool IsValid`.
  - `src/Kumunita.Web/Views/Kiosk/Settings.cshtml` — the playlist editor
    (an ordered list of `KioskView` entries — a `Kind` select
    (`Board`/`Calendar`/`Todos`) + a `BoardId` select (for `Board`) + a
    `CalendarView` select (for `Calendar`) + a `ComponentId` select (for
    `Todos`) + a `Label` input) + the two interval pickers (number inputs,
    `ViewIntervalSeconds` ≥ 5 + `RefreshIntervalSeconds` ≥ 2) + the
    `kiosk.settings.*` `kw-l` keys + an add / remove / reorder affordance
    (a simple ordered list — the "boring where it can be" pin, the M35·7
    pin — no drag-and-drop lib). **No** new JS, **no** new npm package (the
    M35·7 pin).
  - `src/Kumunita.Web/Views/Shared/_AdminNav.cshtml` — add the
    `/admin/kiosk` nav entry (one line, the admin-nav idiom — the M35·2 pin).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The
  `/admin/kiosk` GET + POST are present. The board / calendar / todos
  surfaces are **untouched** (the M35·6 additive-only pin). Handoff note:
  5 lines starting `## U05 — /admin/kiosk settings` — (a) the
  `KioskController` action count (4: `Index` + `Slide` + `SettingsGet` +
  `SettingsPost`), (b) the `KioskSettingsViewModel` member count, (c) the
  board picker seed seam (the frozen M5 `IProjectService` board-list read —
  the M35·3 / M35·9 pin), (d) the confirmation that **no**
  `AccessAction` / `AccessVia` / `Decide()` branch was added (the M35·1 /
  M35·3 pin) + **no** JS / npm package was added (the M35·7 pin), (e) any
  compile warnings.

### U06 — The closed `kiosk.*` `kw-l` key set × en/de/fr/da

- **Goal:** author the closed `kiosk.*` `kw-l` key set (~8 keys) in **all
  four** languages (en/de/fr/da) in the `KnownTranslationKeys` registry —
  present, non-empty, in all four — so U04's display + U05's settings
  surface + U07's TS resolve their `kw-l` references. The board / calendar /
  todos `kw-l` key sets are **untouched** (the M35·6 additive-only pin). The
  closure is pinned by `KwLRegistryConsistencyTests` +
  `KnownTranslationKeys_ParityTests` (the M35·10 pin).
- **Entry reads:**
  `docs/design/m35-kiosk-mode-design.md` §2.4 (the closed key set — the exact
  keys + the en values);
  `src/Kumunita.Web/Views/Kiosk/Index.cshtml` +
  `src/Kumunita.Web/Views/Kiosk/Settings.cshtml` (U04's display + U05's
  settings — the `kw-l` references to satisfy);
  `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the closed-set
  shape to extend + the board / calendar / todos `kw-l` keys to **leave
  untouched**);
  `tests/Kumunita.Web.Tests/KwLRegistryConsistencyTests.cs` +
  `tests/Kumunita.Core.Tests/KnownTranslationKeys_ParityTests.cs` (the
  closure pins — the four-language parity test M35 must satisfy; the parity
  test lives in **Core.Tests**, the registry-consistency test in
  **Web.Tests**);
  `docs/adr/0015-ui-view-localization-mechanics.md` (the `kw-l`
  provider-floor discipline — the `en` values are the source text).
- **Deliverables (1 file, modify):**
  `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — add the closed
  `kiosk.*` key set (~8 keys) in **all four** languages (en/de/fr/da):
  `kiosk.title` / `kiosk.empty` / `kiosk.slide_denied` /
  `kiosk.next_slide` / `kiosk.refreshed` / `kiosk.settings.title` /
  `kiosk.settings.view_interval` / `kiosk.settings.refresh_interval`. The
  `en` values are the source text (the ADR 0015 D1 pin); the `de` / `fr` /
  `da` values are U06's to author (the M35·10 four-language pin). The board /
  calendar / todos `kw-l` keys are **untouched** (the M35·6 additive-only
  pin).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The
  `KnownTranslationKeys_ParityTests` + `KwLRegistryConsistencyTests` are
  green (the closure is held). The board / calendar / todos `kw-l` keys are
  **untouched**. Handoff note: 4 lines starting `## U06 — kiosk.* kw-l key
  set` — (a) the key count (~8), (b) the four-language parity confirmation
  (en/de/fr/da all present + non-empty), (c) the board / calendar / todos
  `kw-l` keys untouched confirmation, (d) the two test files green.

### U07 — The `client/lib/kiosk.ts` auto-cycle / auto-refresh state machine (tsc-only) + the `site.css` kiosk rules

- **Goal:** implement the `client/lib/kiosk.ts` auto-cycle / auto-refresh
  state machine (a plain-tsc, no-dep, no-`any` IIFE — the ADR 0031 pin — the
  `expand-page.ts` / `calendar-fullscreen.ts` precedent) + the `site.css`
  kiosk rules (the chrome-hide + the slide-advance transition) + the global
  `<script>` tag in `_Layout.cshtml` (the `expand-page.js` /
  `calendar-fullscreen.js` idiom — self-guarding, no-op on non-kiosk pages).
  **Zero** new npm package, **zero** build step (the M35·7 pin).
- **Entry reads:**
  `docs/design/m35-kiosk-mode-design.md` §2.3 (the `kiosk.ts` state-machine
  pin);
  `src/Kumunita.Web/Views/Kiosk/Index.cshtml` (U04's display — the
  `#kiosk-stage` element + the `data-*` attributes the `kiosk.ts` reads);
  `src/Kumunita.Web/client/lib/expand-page.ts` +
  `src/Kumunita.Web/client/lib/calendar-fullscreen.ts` (the ADR 0104
  chrome-hide + fullscreen idiom the `kiosk.ts` reuses — the tsc-only,
  no-dep, no-`any` pin + the IIFE shape);
  `src/Kumunita.Web/wwwroot/css/site.css` (the `body.kmb-page-expanded`
  rules the kiosk's chrome-hide reuses + the slide-advance transition to add);
  `src/Kumunita.Web/Views/Shared/_Layout.cshtml` (the script-tag idiom — the
  `expand-page.js` / `calendar-fullscreen.js` / `mine-more.js` global
  `<script type="module" src="~/js/lib/…">` tags U07 appends `kiosk.js` to);
  `src/Kumunita.Web/package.json` +
  `src/Kumunita.Web/tsconfig.json` (the build script — confirm M35 adds
  **no** new package, the M35·7 pin — + the `outDir: wwwroot/js`).
- **Deliverables (≤ 3 files):**
  - `src/Kumunita.Web/client/lib/kiosk.ts` — the state machine (the M35·7
    pin): a plain-tsc, no-dep, no-`any` IIFE (the `expand-page.ts` shape).
    (1) reads the `#kiosk-stage` `data-slides` (a JSON array of `KioskView`
    entries) + `data-view-interval` + `data-refresh-interval` attributes;
    (2) **advances** the visible slide to the next (wrapping to the first)
    every `ViewIntervalSeconds` (a `setInterval`); (3) **re-fetches** the
    current slide's content from `GET /kiosk/slide?slide=N&t=<ts>` every
    `RefreshIntervalSeconds` (a second `setInterval`, a CSRF-free GET — no
    token needed, the M12 iCal "the cookie is the authorization" precedent);
    (4) swaps the `#kiosk-stage` inner content to the fetched slide (a
    same-origin `fetch` + innerHTML swap over the **server-rendered** slide
    HTML — the response is a full slide partial, not JSON). **Zero** new npm
    package, **zero** build step (the M35·7 pin).
  - `src/Kumunita.Web/wwwroot/css/site.css` — the kiosk rules (the M35·6 /
    M35·7 pin): the `#kiosk-stage` chrome-hide (reusing the
    `body.kmb-page-expanded` idiom — the navbar / footer / flash /
    pinned-announcement hidden, the slide fills the viewport) + a
    slide-advance transition (a CSS opacity fade — the "boring where it can
    be" pin — no JS animation lib). **No** new dependency (the M35·7 pin).
  - `src/Kumunita.Web/Views/Shared/_Layout.cshtml` — **append** the
    `<script type="module" src="~/js/lib/kiosk.js"></script>` tag *next to*
    the existing `expand-page.js` / `calendar-fullscreen.js` script tags
    (the global script-tag idiom — self-guarding, no-op on non-kiosk pages —
    the M35·7 pin). **No** new JS, **no** new npm package (the M35·7 pin).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green + the `npm run build`
  (the M34 / M33 `tsc` build path — check `package.json`'s build script)
  green (the `kiosk.ts` compiles to `wwwroot/js/lib/kiosk.js`). The
  `package.json` is **unchanged** (no new package — the M35·7 pin). Handoff
  note: 5 lines starting `## U07 — kiosk.ts + site.css` — (a) the
  `kiosk.ts` line count + the two `setInterval`s (the view-advance + the
  content-refresh), (b) the `site.css` kiosk rules (the chrome-hide + the
  slide-advance transition), (c) the script-tag append in `_Layout.cshtml`
  (the `expand-page.js` / `calendar-fullscreen.js` idiom — self-guarding),
  (d) the `package.json` unchanged confirmation (the M35·7 pin), (e) any
  `tsc` warnings.

### U08 — The pinned test pins (Core + Web)

- **Goal:** implement the **12 pinned test pins** from the design doc §2.5 —
  the 6 `Core.Tests` seam pins (the `KioskServiceTests` class) + the 6
  `Web.Tests` controller pins (the `KioskControllerTests` class). Mirrors the
  M33 `StorageMetricsHistoryTests` + `AdminStorageMetricsHistoryTests` shapes
  (the structural template).
- **Entry reads:**
  `docs/design/m35-kiosk-mode-design.md` §2.5 (the 12 test names, exact — the
  *primary* source for this unit);
  `tests/Kumunita.Core.Tests/Usage/StorageMetricsHistoryTests.cs` (the M33
  Core seam-test shape to mirror — the `PostgresFixture` usage, the
  seed-and-assert pattern);
  `tests/Kumunita.Web.Tests/AdminStorageMetricsHistoryTests.cs` (the M33 Web
  controller-test shape to mirror — the NSubstitute + minimal
  `WebApplication` host);
  `src/Kumunita.Core/Kiosk/KioskService.cs` +
  `src/Kumunita.Core/Kiosk/KioskPlaylist.cs` (U03's impl + doc — the methods
  to test);
  `src/Kumunita.Web/Controllers/KioskController.cs` (U04's + U05's controller
  — the actions to test);
  `tests/Kumunita.Core.Tests/PostgresFixture.cs` (the test harness).
- **Deliverables (2 files, new):**
  - `tests/Kumunita.Core.Tests/Kiosk/KioskServiceTests.cs` — **6 tests**, one
    per pinned name:
      1. `M35_2_GetPlaylist_NoRow_ReturnsNull`
      2. `M35_2_GetPlaylist_Configured_ReturnsRow`
      3. `M35_5_GetPlaylist_WritesNoAuditRow`
      4. `M35_5_SavePlaylist_OneKioskSaveAuditRow`
      5. `M35_2_SavePlaylist_NoRow_CreatesSingleton`
      6. `M35_SavePlaylist_AtomicInCallerSession`
  - `tests/Kumunita.Web.Tests/KioskControllerTests.cs` — **6 tests**, one per
    pinned name:
      7. `M35_3_SignedIn_Sees_ChromeHidden_FirstSlide`
      8. `M35_3_Anonymous_RedirectedToSignIn`
      9. `M35_5_SlideDenied_SignsInViewer_SeesDeniedShape`
      10. `M35_7_NonGlobalAdmin_AdminKiosk_403`
      11. `M35_5_GlobalAdmin_SavePlaylist_OneAuditRow_Persists`
      12. `M35_6_ExistingSurfaces_RenderUnchanged`
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The 12 tests are
  present. `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/
  Kumunita.Core.Tests.dll` reports **6 tests discovered** in
  `KioskServiceTests` (pass/red status recorded for U09's gate). `dotnet
  exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll`
  reports **6 tests discovered** in `KioskControllerTests` (pass/red status
  recorded for U09's gate). **No gate recorded** (U09). Handoff note: 3 lines
  starting `## U08 — pinned test pins (12)` — (a) the two test file paths,
  (b) the 12 names (verbatim), (c) the 6 + 6 pass/red counts (for U09 to
  consume).

### U09 — Run + record the M35 acceptance gate

- **Goal:** execute and **record** the three-test acceptance gate (closed
  loop / handoff / part-vs-whole) from the design doc §2.6, *using* U08's 12
  tests as the part-vs-whole evidence. Record the gate in the design doc.
  **If the runtime is not yet present, the spec is recorded (mirroring the
  M34 / M33 precedent) and *not* run — the gap is recorded in the design doc
  and the next unit who lands the runtime records the pass count.**
- **Entry reads:**
  `docs/design/m35-kiosk-mode-design.md` §2.6 (the gate's three test names
  and their definitions) + §2.7 (the drift-guard);
  `docs/plans-milestones/in-progress/m35-handoff-notes.md` (U08's section —
  the 12-test + pass/red results that the gate *references*);
  `tests/Kumunita.Core.Tests/Kiosk/KioskServiceTests.cs` +
  `tests/Kumunita.Web.Tests/KioskControllerTests.cs` (the 12 tests the gate's
  part-vs-whole invokes);
  `docs/design/m34-analytics-history-design.md` § Acceptance Gate (the M34
  recording shape to mirror verbatim).
- **Deliverables (1 file, modify):**
  `docs/design/m35-kiosk-mode-design.md` — append `### Run result (M35
  acceptance gate — <date>)`: the three test names, their pass/red status,
  the 12-test count (from U08), and one line per any `## U<m> — Drift pause`
  section in the handoff note (each resolved or still open). **No code, no
  build.**
- **Exit:** the gate section is present and consistent with U08's results.
  Handoff note: 4–5 lines starting `## U09 — gate recorded` — the three test
  names + pass counts + the date + any still-open drift.

### U10 — Close: the six-member close flip (M35 is the **last** milestone)

- **Goal:** flip `M35` from `StatusNext` to `StatusDone` in `Milestones.cs`
  (**no** successor to promote — M35 is the last milestone), removes
  `MilestonesTests.Single_InProgress_Milestone_Is_M35` (the current pin) +
  adds a new `No_Milestone_Is_InProgress` test (asserting **no** in-progress
  milestone) + appends `"M35"` to the
  `Shipped_Milestones_Are_Marked_Done` shipped-set list, append the README Roadmap `M35` line (the `**Done.** (ADR 0164)`
  tail), append the `STATUS.md` `M35` line, append the `ARCHITECTURE.md`
  `Kiosk/` line (the M35 new context), append the `WhatsNew.cs` `0.57.0`
  entry (newest-first, naming M35 + ADR 0164), tag the ADR 0164 index row
  `**Done** (M35)`, flip ADR 0164 → `Accepted`, and move all M35 artifacts to
  `done/m35/`. **No code change.** **Exit: `dotnet build` clean +
  `Kumunita.Web.Tests` green (the `MilestonesTests` + `WhatsNewTests` pins
  green).**
- **Entry reads:**
  `docs/plans-milestones/plan-m35-kiosk-mode.md` — the register (the §gate,
  the §drift-guard, the M35·11 pin);
  U09's handoff-note `## U09 — gate recorded` section (the 12 pass/red counts
  + the gate status);
  `src/Kumunita.Web/Milestones.cs` — the `M35` row to flip (the **last**
  milestone — **no** successor to promote);
  `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the
  `Single_InProgress_Milestone_Is_M35` pin to **remove** + the
  `Shipped_Milestones_Are_Marked_Done` shipped-set list to append
  `"M35"` to + a new `No_Milestone_Is_InProgress` test to **add**;
  `src/Kumunita.Web/WhatsNew.cs` — the `0.57.0` entry to append,
  newest-first;
  `README.md` — the `M35` Roadmap line to append the `**Done.** (ADR 0164)`
  tail;
  `docs/STATUS.md` — the `M35` line to append;
  `docs/ARCHITECTURE.md` — the `Kiosk/` line to append (the M35 new context);
  `docs/adr/0164-kiosk-mode.md` — the ADR 0164 to flip to `Accepted` + the
  index row to tag `**Done** (M35)`;
  `docs/adr/README.md` — the ADR 0164 index row to tag `**Done** (M35)`.
- **Deliverables (7 files, modify + 1 move):**
  1. **`src/Kumunita.Web/Milestones.cs`** (modify) — the `M35` row:
     `new("M35", "Kiosk mode — …", StatusDone)` (was `StatusNext`).
     **No** successor row to promote (M35 is the last milestone). The order
     is **unchanged** (`…"M33","M34","M35"`).
  2. **`tests/Kumunita.Web.Tests/MilestonesTests.cs`** (modify) — append
     `"M35"` to the `Shipped_Milestones_Are_Marked_Done` shipped-set list +
     **remove** `Single_InProgress_Milestone_Is_M35` + **add** a new
     `No_Milestone_Is_InProgress` test (asserting
     `Milestones.All.Where(m => m.Status == Milestones.StatusNext)` is
     **empty**). The `Roadmap_Covers_M0_Through_M35_Plus_Named_Lanes_In_`
     `Order` pin is **untouched** (M35 is already in the pinned order; only
     the status flips).
  3. **`src/Kumunita.Web/WhatsNew.cs`** (modify) — append the `0.57.0`
     entry (newest-first, naming M35 + ADR 0164):
     `new("0.57.0", "2026-10-12", new List<string> { "Kiosk mode — a
     full-page / full-screen display at /kiosk that automatically cycles a
     GlobalAdmin-set preset list of views (boards, calendar, todos) on a set
     interval and refreshes each view's content on a separate, faster
     interval: a new bounded context Kumunita.Core.Kiosk (the KioskPlaylist
     singleton doc + the KioskDocTypes surface + the IKioskService seam +
     the KioskService impl — one kiosk.save audit row per GlobalAdmin write)
     + the /kiosk signed-in display (the chrome-hidden layout + the first
     slide) + the GET /kiosk/slide refresh fetch + the /admin/kiosk settings
     surface (the playlist editor + the interval pickers) + the
     client/lib/kiosk.ts auto-cycle/auto-refresh state machine (tsc-only) +
     the closed kiosk.* kw-l key set × en/de/fr/da; the frozen board /
     calendar / todos read seams + render partials are reused, not re-shape)
     (ADR 0164)." })`.
  4. **`README.md`** (modify) — the `M35` Roadmap line: append the
     `**Done.** (ADR 0164)` tail (the M34 `**Done.** (ADR 0162)` shape).
  5. **`docs/STATUS.md`** (modify) — the `M35` line: append the `**M35 is
     done** — kiosk mode (the /kiosk full-page / full-screen display that
     auto-cycles a GlobalAdmin-set preset list of views — boards, calendar,
     todos — on a set interval and refreshes each view's content on a
     separate, faster interval; a new bounded context Kumunita.Core.Kiosk —
     the KioskPlaylist singleton doc + the KioskDocTypes surface + the
     IKioskService seam + the KioskService impl — + the /kiosk display + the
     /kiosk/slide refresh fetch + the /admin/kiosk settings surface + the
     client/lib/kiosk.ts state machine (tsc-only) + the closed kiosk.*
     kw-l key set × en/de/fr/da; the frozen board / calendar / todos read
     seams + render partials are reused; ADR 0164)` line (the M34 shape).
  6. **`docs/ARCHITECTURE.md`** (modify) — the `Kiosk/` line: append the
     `**Kiosk/** (M35) — the M35 kiosk-mode lane (ADR 0164): the
     KioskPlaylist singleton doc + the KioskView entry + the KioskDocTypes
     surface + the IKioskService seam (GetPlaylistAsync + SavePlaylistAsync)
     + the KioskService impl (one kiosk.save audit row per GlobalAdmin
     write) — a new bounded context; the frozen board / calendar / todos
     read seams (IProjectService + IEventService) + render partials are
     **reused**, not re-shape; zero new AccessAction / AccessVia / Decide()
     branch / IAuditableResource adapter; Core stays HTTP-free +
     Wolverine-free` line (the M31 `ErrorReports/` shape).
  7. **`docs/adr/0164-kiosk-mode.md`** (modify) — the ADR 0164: flip
     `Status: Draft` → `Status: Accepted` + the `docs/adr/README.md` index
     row: tag the `0164` row `**Done** (M35)` (the M34 `0162` row shape).
     **Plus** the `done/m35/` move:
     `git mv docs/plans-milestones/plan-m35-kiosk-mode.md
     docs/plans-milestones/done/m35/` + `git mv
     docs/plans-milestones/in-progress/m35-uNN.md
     docs/plans-milestones/done/m35/m35-uNN.md` (for each U00–U09 unit plan)
     + `git mv docs/plans-milestones/in-progress/m35-handoff-notes.md
     docs/plans-milestones/done/m35/m35-handoff-notes.md` (the `done/m34/`
     subfolder convention).
- **Exit**
  - `dotnet build Kumunita.slnx -c Debug` green.
  - `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.
    dll` green (the `MilestonesTests` + `WhatsNewTests` pins green — the
    order pin is intact, the `No_Milestone_Is_InProgress` assertion is green,
    the new `0.57.0` entry is present, newest-first).
  - The `Milestones.cs` `M35` row is `StatusDone` (**no** successor row).
    The README / `STATUS.md` / `ARCHITECTURE.md` parity is held. The ADR
    0164 is `Accepted` + the index row is tagged `**Done** (M35)`. The
    `done/m35/` subfolder is present (the register + the unit plans + the
    handoff notes).
  - Handoff note: a `## U10 — close` section — (a) the `Milestones.cs` flip
    (the `M35` row `StatusDone`, **no** successor), (b) the
    `MilestonesTests` re-pin (the `Single_InProgress_Milestone_Is_M35`
    removed + the `No_Milestone_Is_InProgress` added + the
    `Shipped_Milestones_Are_Marked_Done` shipped-set appended with `"M35"`),
    (c) the `WhatsNew.cs` `0.57.0` entry (newest-first), (d) the README /
    `STATUS.md` / `ARCHITECTURE.md` parity (the three line appends), (e) the
    ADR 0164 `Accepted` + the index row `**Done** (M35)`, (f) the `done/m35/`
    move (the `git mv` commands).
