# M35 — Kiosk mode (design doc)

> **Abstract:** This design settles the **shared physical display** question
> M1–M34 never answered. Every surface shipped so far is something a resident
> reads **at their own desk** — the feed, the groups, the boards, the calendar,
> the to-dos. But a neighborhood's life is not only what each resident reads
> privately; a **shared physical space** (the community hall, the library, the
> café) is where the neighborhood *sees itself*. Today, putting Kumunita on a
> wall display means a human standing at the TV, clicking from one feed to the
> next, and refreshing by hand. M35 adds the **kiosk**: a **full-page /
> full-screen display** (`GET /kiosk`) that **automatically cycles** a
> **GlobalAdmin-set** playlist of the resident's own surfaces (a Kanban board,
> the calendar, a second board, the calendar day, the to-dos) and **refreshes
> each view's content on a separate, faster interval**. The playlist + the two
> intervals are a **GlobalAdmin-set singleton** (the `SiteContent` /
> `LocaleSettings` config idiom), the display is **signed-in** (a kiosk account
> on the TV), and each slide's content rides the **frozen** board / calendar /
> todos read seams — **the same `Read` decision, re-run on every fetch**, so a
> slide never reveals more than that viewer could already see on that surface's
> own feed (the M12 iCal "authorization rides the cookie, re-run on every
> fetch" precedent). Out of scope: the per-resident playlist, the kiosk "lock",
> audio, per-slide dwell weighting, the board / calendar / todos **content**
> itself (reused, not re-added), the iCal / portability / search surfaces, and
> the six-member close flip (U10's). **M35 is the last milestone** — the close
> promotes **no** successor.
>
> **Milestone M35 — Kiosk mode.** The README / `Milestones.cs` line, verbatim:
> "**Kiosk mode** — a full-page / full-screen display that automatically cycles
> a preset list of views (e.g. Kanban board A, calendar week, Kanban board B,
> calendar today, todos), changing view on a set interval and refreshing each
> view's content on a separate, faster interval." M35 is a **new bounded
> context** (`Kumunita.Core.Kiosk`, M35·1), **not** a capability on an existing
> one: it **creates** the one new `Kiosk` context (the M31 `ErrorReports` / M11
> `Portability` / M13 `Usage` precedent) with **one singleton doc**
> (`KioskPlaylist`, the M35·2 ceiling), **one doc-type surface**
> (`KioskDocTypes`, the ADR 0004 §B.1 shape), and **one service**
> (`IKioskService`, the 2-method seam), and **reuses** the **frozen** board /
> calendar / todos read seams (the M5 `IProjectService` board-detail +
> todos-list + the M4 `IEventService` calendar-range seam — M35·3) and the
> **existing render surface** (the board / calendar / todos Razor partials +
> the ADR 0104 chrome-hide idiom — M35·6). **M35 is the last milestone on the
> roadmap** — there is **no** `M36` to promote at the close (M35·11).
>
> **The one thing that separates M35 from the M34 "derived, not captured"
> precedent it otherwise mirrors — new context + reused read seams (M35·1).**
> M34 added **no** new bounded context — it extended the existing M13 `Usage`
> surface with a derived read. M35 is the opposite shape on the *display*
> surface: it **creates** a new `Kumunita.Core.Kiosk` context (one
> `KioskPlaylist` doc + one `KioskDocTypes` surface + one `IKioskService`
> seam) to hold the **operator's kiosk configuration**, and then **reuses —
> without re-shaping** — the frozen M5 / M4 read seams to resolve each slide's
> content for the signed-in viewer. The kiosk's per-view content is **not**
> stored in the `Kiosk` context: the playlist doc carries only the **ordered
> view list + the two intervals**; the content is always resolved live through
> the underlying surface's own read seam (M35·3), so the kiosk adds **no** new
> `AccessAction` / `AccessVia` / `Decide()` branch / `IAuditableResource`
> adapter (M35·1 / M35·3) and the existing board / calendar / todos render
> partials are `@await`'d verbatim (M35·6).
>
> **Three-tier contract.** This file is the **primary** tier of M35's
> contract: it pins the **invariants (the 11: M35·1–M35·11)**, the **FACES
> (M35-1–M35-10)**, and (in Part 2, U02) the `KioskPlaylist` / `KioskView` doc
> shapes, the `KioskDocTypes` surface, the `IKioskService` seam, the
> `KioskService` impl shape, the `/kiosk` display + the GlobalAdmin settings
> surface, the `client/lib/kiosk.ts` state machine, the closed `kiosk.*`
> `kw-l` key set, the pinned test names, the acceptance gate, and the drift
> guard. The register
> (`docs/plans-milestones/plan-m35-kiosk-mode.md`) is the **secondary** tier
> (unit-level deliverables + exit criteria).
> `docs/plans-milestones/in-progress/m35-handoff-notes.md` is the **scratch**
> tier (one short section per unit, appended, never rewritten). When the three
> disagree, **this file wins for the pinned shapes**; the register wins for
> *which files exist* and *what each unit does*.
>
> **Part 1 (this file, U01):** the context, the scope (In / Out, incl. the
> named deferrals), the **eleven invariants** (M35·1–M35·11), and the **ten
> FACES** (M35-1–M35-10), plus the frozen-base assumptions.
> **Part 2 (U02):** the seams & contracts (the exact `KioskPlaylist` /
> `KioskView` doc shapes, the `KioskDocTypes` surface, the `IKioskService`
> 2-method seam, the `KioskService` impl shape, the `KioskController` actions
> + view models, the `client/lib/kiosk.ts` state machine, the closed `kiosk.*`
> `kw-l` key set, the 12 pinned test names, the three-test acceptance gate, the
> drift guard) + **ADR 0164** (the next free number after 0163 — U00 verified).
>
> **The frozen base (reused, unchanged).** M35 is built **on top of** the
> existing board / calendar / todos surfaces (the M5 `Kumunita.Core.Projects`
> context + the M4 `Kumunita.Core.Events` context): the M5 `IProjectService`
> **board-detail + todos-list read seams** (`GetBoardAsync` +
> `ListTodosAsync`), the M5 **board-list seam** (`ListBoardsAsync`, seeding the
> settings board picker), and the M4 `IEventService` **calendar-range seam**
> (`ListInRangeAsync`) — all **reused verbatim** (M35·3 / M35·9); the board /
> calendar / todos **Razor partials**
> (`Views/Projects/BoardDetail.cshtml` + `Views/Projects/TodosIndex.cshtml` +
> `Views/Event/Calendar.cshtml`) — `@await`'d verbatim (M35·6); the **ADR 0104
> chrome-hide idiom** (`client/lib/expand-page.ts` — the
> `body.kmb-page-expanded` class + the `.page-expand-toggle` /
> `.page-expand-close` selectors + the Escape-key exit) — `kiosk.ts` reuses it
> (M35·6); and the **`SiteContent` / `LocaleSettings` singleton shape**
> (`SiteContent.SingletonId = "singleton"`, the singleton sentinel idiom — the
> `KioskPlaylist` proposed `Id` sentinel is `"kiosk"`). It also rides the
> ADR 0006-D dependency-direction rule (**`Kumunita.Core` references no ASP.NET
> HTTP types** — the `KioskService` is a plain store-composing service, no
> `HttpClient` in Core, M35·8), the ADR 0031 tsc-only discipline (the
> `kiosk.ts` is a plain-tsc, no-dep, no-`any` module, M35·7), and the ADR 0015
> D1 `kw-l` provider-floor discipline (the `en` value is the source text,
> M35·10). All of these still bind **unchanged**. M35 adds **one new bounded
> context** (`Kiosk`, M35·1), **one new singleton doc** (`KioskPlaylist`, the
> M35·2 ceiling), **one new doc-type surface** (`KioskDocTypes`, M35·1), **one
> new service** (`IKioskService`, the 2-method seam, M35·1), **one display
> route** (`GET /kiosk` + `GET /kiosk/slide`, M35·3), **one settings surface**
> (`GET/POST /admin/kiosk`, M35·2), **one tsc-only state machine**
> (`client/lib/kiosk.ts`, M35·7), the **`site.css` kiosk rules**, and the
> **closed `kiosk.*` `kw-l` key set** × en/de/fr/da (M35·10) — but it adds
> **no** new `AccessAction` / `AccessVia` / `Decide()` branch /
> `IAuditableResource` adapter (M35·1 / M35·3), **no** second `kiosk.*`
> `AccessAudit` row beyond the one `kiosk.save` write row (M35·5), **no** new
> doc **to an existing surface** and **no** new index to an **existing** doc
> (the M5 / M4 surfaces are **untouched**, M35·9), **no** new npm package /
> build step (M35·7), and **no** EF migration (the new doc registers through
> the new `KioskDocTypes` surface, not an EF table — ADR 0004 §B). It is
> **additive**.
>
> **The one thing every unit must respect:** M35 is **new context + reused
> read seams** (M35·1 / M35·3) on the display surface. The playlist is a
> **GlobalAdmin-set singleton** (the `KioskPlaylist` 4-member + `KioskView`
> 5-member ceilings, M35·2); the display is **signed-in** and each slide's
> content is resolved by the **frozen** board / calendar / todos read seam for
> the **signed-in viewer** — **re-run on every fetch** (M35·3); a slide the
> viewer cannot see renders the `kiosk.slide_denied` empty state — **no content
> leak, no 404 crash** (M35·3 / M35-5). The two intervals are **display-only,
> never an access input** (M35·4). One `kiosk.save` audit row per write; the
> display reads emit **zero** `kiosk.*` rows (the content rides the underlying
> surface's own audit, M35·5). The existing render partials are **reused, not
> re-authored** (M35·6). Zero new dependency / npm package / build step
> (`kiosk.ts` is tsc-only, M35·7). Core stays **HTTP-free + Wolverine-free**
> (M35·8, ADR 0006-D). No new bounded context beyond `Kiosk` (the M5 / M4
> contexts are reused, not re-added, M35·9). The closed `kiosk.*` `kw-l` key
> set is **four-language** (en/de/fr/da, M35·10). The six-member close flip is
> **U10's** — and **M35 is the last milestone**, so the close promotes **no**
> successor (M35·11). After M35, a kiosk account signs in on a wall display
> and the neighborhood's own surfaces — the boards, the calendar, the to-dos —
> cycle themselves, fresh.

## Context

M1–M34 shipped the full **resident** surface: a directory, posts, events,
projects, boards, to-dos, a calendar, messaging, and every admin control the
operator needs. Every one of those surfaces is something a resident reads
**at their own desk**. But a neighborhood's shared life happens in a **shared
physical space** — the community hall, the library, the café — where the
neighborhood *sees itself*. Putting Kumunita on that display today is a
manual chore: a human stands at the TV, clicks from one feed to the next, and
refreshes by hand.

The platform already has the pieces a kiosk needs; it lacks only the
**display surface** that ties them into a self-driving slideshow:

- **The content** is all there and is the **frozen** M5 / M4 surface: the M5
  `IProjectService` **board-detail seam** (`GetBoardAsync`) + **todos-list
  seam** (`ListTodosAsync`) + **board-list seam** (`ListBoardsAsync`, for the
  settings board picker), and the M4 `IEventService` **calendar-range seam**
  (`ListInRangeAsync`). M35 **reuses** these verbatim — it does not add any
  board / event / todo capability (M35·3 / M35·9).
- **The render** is all there: the board / calendar / todos **Razor partials**
  (`Views/Projects/BoardDetail.cshtml`, `Views/Projects/TodosIndex.cshtml`,
  `Views/Event/Calendar.cshtml`). M35 `@await`s them verbatim (M35·6).
- **The chrome-hide** is all there: the ADR 0104 `body.kmb-page-expanded`
  idiom (`client/lib/expand-page.ts`) already hides the navbar / footer /
  flash / pinned-announcement and stretches `<main>` to fill the viewport.
  M35's `kiosk.ts` reuses this idiom for the full-screen stage (M35·6).
- **The authorization** is all there and **correct by construction**: each of
  those read seams already resolves the **signed-in viewer's** `Read`
  decision. M35 rides that decision — **re-run on every fetch** — so a slide
  never reveals more than the viewer could already see on that surface's own
  feed (the M12 iCal "authorization rides the cookie" precedent, M35·3).
- **The config idiom** is all there: the `SiteContent` / `LocaleSettings`
  **singleton** doc shape (one row per instance, `Id` fixed to a sentinel, a
  plain `LoadAsync` read, one audited `Admin` write). M35's
  `KioskPlaylist` mirrors this (M35·2).

What is missing is the **glue**: a signed-in route that **holds** an ordered
playlist + two intervals, **advances** the visible slide on one cadence, and
**re-fetches** the current slide's content on a faster cadence. M35 **is**
that glue. It adds **one new bounded context** (`Kumunita.Core.Kiosk`) to hold
the operator's kiosk configuration, **one display route** (`GET /kiosk`) that
renders the chrome-hidden stage, **one settings surface**
(`GET/POST /admin/kiosk`) to set the playlist + intervals, and **one
tsc-only state machine** (`client/lib/kiosk.ts`) to drive the cycle +
refresh — and **reuses** everything else.

**Critically, M35 stores no content.** This is the **one invariant that
separates M35 from the M34 "derived, not captured" precedent** it otherwise
mirrors. M35 **creates** a new context to hold the *configuration*
(the ordered view list + the two intervals) — it does **not** store a
snapshot of any board / event / todo. Every slide's content is resolved
**live** through the underlying surface's own frozen read seam for the
**signed-in viewer** (M35·3). So the kiosk adds **no** new `AccessAction` /
`AccessVia` / `Decide()` branch / `IAuditableResource` adapter (M35·1 /
M35·3), the existing render partials are `@await`'d verbatim (M35·6), and the
audit story is unchanged: the content rides the **underlying surface's own**
`Read` audit rows, and the only `kiosk.*` row is the one `kiosk.save` write
row on the playlist (M35·5).

The boundary with M34 is explicit and pinned: **M34 is derived, not
captured** on the M13 `Usage` surface (one read over existing rows; no new
context); **M35 is new context + reused read seams** on the display surface
(one new `Kiosk` context holding the configuration; the content is always
resolved through the frozen M5 / M4 seams). M35 **reuses** the M5 `Projects`
+ M4 `Events` contexts (**untouched**, M35·9), the board / calendar / todos
render partials (M35·6), the ADR 0104 chrome-hide idiom (M35·6), and the
`SiteContent` / `LocaleSettings` singleton shape (the structural template,
M35·2); it **adds** the one new `Kiosk` context, the display route, the
settings surface, the tsc-only state machine, and the closed `kiosk.*` `kw-l`
key set. M35 does **not** re-shape any M5 / M4 seam, any board / calendar /
todos partial, or any existing `kw-l` key; it does **not** add any board /
event / todo capability, a per-resident playlist, a kiosk lock, audio,
per-slide dwell weighting, or an iCal / portability / search surface (the
named deferrals, §Scope).

## Scope

**In (M35's closed surface):**

- the **`KioskPlaylist` singleton doc** — the **4-member M35·2 ceiling** (the
  `Id` — the sentinel `"kiosk"`, the ADR 0004 §B.1 singleton idiom, the
  `SiteContent.SingletonId = "singleton"` precedent — + the `Views` — the
  **ordered** `IReadOnlyList<KioskView>` slide list — + the
  `ViewIntervalSeconds` — the slide-advance cadence, the M35·4 display-only
  pin — + the `RefreshIntervalSeconds` — the content-refresh cadence,
  **faster** than the view cadence, the M35·4 pin);
- the **`KioskView` entry** — the **5-member M35·2 ceiling** (the `Kind` —
  the `KioskViewKind` closed enum `Board` | `Calendar` | `Todos` — + the
  `BoardId?` — only when `Kind == Board` — + the `CalendarView?` —
  `day` | `week` | `month`, only when `Kind == Calendar`, default `week` — +
  the `ComponentId?` — the community filter, only when `Kind == Todos`,
  `null` = all the viewer's visible todos — + the `Label?` — an optional
  per-slide display label);
- the **`KioskViewKind` enum** — the closed 3-member set (`Board` |
  `Calendar` | `Todos`), the M35·2 pin;
- the **`KioskDocTypes` registration surface** — the ADR 0004 §B.1 shape (one
  `.Schema.For<KioskPlaylist>` — the `SiteContentDocTypes` /
  `LocaleSettings` `*DocTypes` precedent, M35·1) + the **boot wiring** (the
  `Program.cs` dev-loop + the all-env `SchemaBootstrap`
  `KioskDocTypes.Configure` call, the M3 `M3DocTypes` precedent);
- the **`IKioskService` 2-method seam** (M35·1) — `GetPlaylistAsync`
  (the singleton read — **zero** `AccessAudit` rows, a `null`/empty return =
  no row configured — M35·5) + `SavePlaylistAsync` (the GlobalAdmin write —
  **one** `kiosk.save` `AccessAudit` row, `TargetKind = "kiosk"`,
  `Via = Admin` — M35·5);
- the **`KioskService` impl** (the `SiteContentService` structural template)
  — the singleton read + the GlobalAdmin write with one `kiosk.save` audit
  row (M35·5); the actor-standing gate (GlobalAdmin) is enforced by the
  **controller**, not the service (the admin-surface precedent);
- the **`KioskController`** — `GET /kiosk` (the signed-in chrome-hidden
  display, M35·3) + `GET /kiosk/slide?slide=N&t=<ts>` (the single-slide
  refresh fetch — a CSRF-free GET, the M12 iCal "the cookie is the
  authorization" precedent) + `GET/POST /admin/kiosk` (the GlobalAdmin
  settings surface, M35·2);
- the **`KioskDisplayViewModel`** (the playlist's `Views` + the two intervals
  + the first slide's resolved content + `HasSlides` + `DeniedLabel`) + the
  **`KioskSettingsViewModel`** (the playlist editor binding + the interval
  pickers + the board picker seed);
- the **`Views/Kiosk/Index.cshtml`** chrome-hidden display (the ADR 0104
  `body.kmb-page-expanded` idiom — the navbar / footer / flash /
  pinned-announcement hidden, the slide fills the viewport; the board /
  calendar / todos partials `@await`'d verbatim, M35·6) + the
  **`Views/Kiosk/Settings.cshtml`** playlist editor;
- the **`client/lib/kiosk.ts`** auto-cycle / auto-refresh state machine
  (a plain-tsc, no-dep, no-`any` IIFE — the ADR 0031 pin — the
  `expand-page.ts` / `calendar-fullscreen.ts` precedent — two `setInterval`s:
  the view-advance + the content-refresh, M35·7);
- the **`site.css` kiosk rules** (the chrome-hide + the slide-advance
  transition, M35·6 / M35·7);
- the **closed `kiosk.*` `kw-l` key set** × en/de/fr/da (~8 keys —
  `kiosk.title` / `kiosk.empty` / `kiosk.slide_denied` / `kiosk.next_slide` /
  `kiosk.refreshed` / `kiosk.settings.title` /
  `kiosk.settings.view_interval` / `kiosk.settings.refresh_interval` — the
  M35·10 pin); and
- the **pinned test pins** (the `KioskServiceTests` Core class — 6 names +
  the `KioskControllerTests` Web class — 6 names, the 12-test model, M35·12).

**Out (named deferrals):**

- **The per-resident playlist** — v1 is a **single** GlobalAdmin-set
  community playlist (the `SiteContent` / `LocaleSettings` singleton
  precedent); a per-resident override is a **future lane**.
- **The kiosk "lock"** — a kiosk-mode "exit only with a PIN / admin-token"
  gate to prevent a viewer leaving the kiosk surface. In v1 the browser's
  own back button still works; a true kiosk lock (kiosk browser profile + a
  PIN gate) is a **future lane**.
- **Audio / ambient sound** — the kiosk is silent; ambient audio is a
  **future lane**.
- **Per-slide dwell weighting** — v1 advances at a **uniform**
  `ViewIntervalSeconds` across every slide; a per-slide weighted dwell is a
  **future lane**.
- **The board / calendar / todos content itself** — M35 **reuses** the M5 /
  M4 surfaces; it does **not** add any board / event / todo capability
  (M35·3 / M35·9). A board / event / todo change lands on its **own** lane.
- **The iCal / portability / search surfaces** — out of kiosk scope; the
  kiosk cycles the **five** named views (boards, calendar, todos), not the
  export / search / portability lanes.
- **The `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip** until the
  milestone *ships* (U10 owns it — the `WhatsNew.cs` `0.57.0` entry is
  appended by U10, not by an earlier unit, M35·11). And **M35 is the last
  milestone** — the close flips `M35` from `StatusNext` to `StatusDone` and
  promotes **no** successor (M35·11).

## Invariants (pinned for M35)

The **11 invariants** below are the **frozen** ids for M35 — once written,
they are the cross-reference for every subsequent unit (U02–U10). The FACES
(M35-1–M35-10) in the next section are each **bound** to one or more of these
invariants; the drift-guard (authored by U02 in Part 2) re-pins them, and the
close unit (U10) re-pins them again. M35 carries **11 invariants** — the ids
run **M35·1 through M35·11** (contiguous; unlike M34, no deliberate gaps).

- **M35·1 — M35 is a new bounded context `Kumunita.Core.Kiosk`, not a
  re-shape of an existing one.** M35 **creates** a new context (the M31
  `ErrorReports` / M11 `Portability` / M13 `Usage` precedent) with **one
  singleton doc** (`KioskPlaylist`, the M35·2 pin), **one doc-type surface**
  (`KioskDocTypes`, the ADR 0004 §B.1 shape), and **one service**
  (`IKioskService`, the 2-method seam). It **reuses** the **frozen** board /
  calendar / todos read seams (the M5 `IProjectService` board-detail +
  todos-list + the M4 `IEventService` calendar-range seam — the M35·3 pin) and
  the **existing render surface** (the board / calendar / todos Razor
  partials — the M35·6 pin). M35 adds **no** new `AccessAction`, **no** new
  `AccessVia`, **no** `Decide()` branch, and **no** `IAuditableResource`
  adapter (the kiosk's per-view content rides the **frozen** read seams).
- **M35·2 — The playlist is a GlobalAdmin-set singleton.** The
  `KioskPlaylist` doc is a **singleton** (the `SiteContent` /
  `LocaleSettings` / `SurfaceLabels` shape — the admin-surface config idiom,
  ADR 0004 §B.1). It carries the **ordered** list of `KioskView` entries +
  `ViewIntervalSeconds` + `RefreshIntervalSeconds`. The **standing to write
  it is GlobalAdmin** (the admin-surface precedent — M35-7 FACE). **No
  per-resident playlist in v1** (a named deferral, §Scope). The `KioskPlaylist`
  carries exactly **4 members** and the `KioskView` entry exactly **5 members**
  (the M35·2 ceilings — no field outside the set, §Scope).
- **M35·3 — The display is signed-in and rides the frozen read seams.**
  `GET /kiosk` is `[Authorize]`. Each slide's content is resolved by the
  **frozen** board / calendar / todos read seam for the **signed-in viewer**
  (the same `Read` decision as that surface's own feed — **re-run on every
  fetch**, the M12 iCal precedent). A slide the viewer **cannot see** (a board
  the viewer is not a member of) renders the `kiosk.slide_denied` empty state
  — **no content leak, no 404 crash** (M35-5 FACE). **No break-glass, no new
  standing, no new authorization branch.**
- **M35·4 — The two intervals are display-only, never an access input.**
  `ViewIntervalSeconds` (the slide-advance cadence) + `RefreshIntervalSeconds`
  (the content-refresh cadence, **faster** than the view cadence) are
  **config** on the `KioskPlaylist` singleton — **never** a DB access column,
  **never** an authorization input (the M34·7 / M13 `windowDays`
  "display-only, never an access input" precedent). Both are **GlobalAdmin-set**
  (M35·2).
- **M35·5 — One `kiosk.save` audit row per write; the display reads emit zero
  `kiosk.*` rows.** The GlobalAdmin playlist write commits **one**
  `kiosk.save` `AccessAudit` row (`TargetKind = "kiosk"`, `Via = Admin` — the
  admin-surface write-lane convention). The `/kiosk` display's per-view content
  reads emit **zero** `kiosk.*` `AccessAudit` rows — the content rides the
  **underlying surface's own** audit rows (the board's / calendar's / todos'
  `Read` audit — the M34·4 / M13 D4 "the audit row is on the underlying
  surface" precedent). The `KioskPlaylist` **read** (config) emits **zero**
  rows.
- **M35·6 — The existing render surface is reused, not re-authored.** The
  kiosk's per-slide section renders the **same** board / calendar / todos
  Razor partials the board detail / calendar / todos index views use
  (the "integration over features" spine, `docs/philosophy/`). The
  `MarkdownRenderer` / `kw-dt` / audience / tag / draft machinery is
  **untouched**. The kiosk adds **only** a **chrome-hidden layout** (reusing
  the ADR 0104 `body.kmb-page-expanded` chrome-hide idiom — the navbar /
  footer / flash / pinned-announcement are hidden, the slide fills the
  viewport) + the `client/lib/kiosk.ts` auto-cycle / auto-refresh state
  machine + the `kiosk.*` `kw-l` keys.
- **M35·7 — Zero new dependency, zero new npm package, zero build step.**
  The `client/lib/kiosk.ts` is **tsc-only** (the ADR 0031 pin — the
  `expand-page.ts` / `calendar-fullscreen.ts` precedent). The auto-cycle +
  auto-refresh is a plain `setInterval` state machine (no carousel lib, no
  timer framework, no chart lib, no bundler, no `npm install`).
  `package.json` stays `typescript`-only.
- **M35·8 — Core stays HTTP-free + Wolverine-free.** The `KioskService` is a
  plain store-composing service (the `AddTransient<IKioskService>` shape, the
  M31 `IErrorReportService` / M13 `IUsageAnalyticsService` precedent). The
  playlist read + the per-view content read are **Wolverine-free reads** over
  local Postgres documents. There is **no** `HttpClient` in Core (ADR
  0006-D) and **no** outbound channel.
- **M35·9 — No new bounded context beyond `Kiosk`.** M35 **creates** the one
  new `Kiosk` context (M35·1); the board / calendar / todos contexts (M5
  `Projects` + M4 `Events`) are **reused, not re-added** (the `M5DocTypes` /
  `M4DocTypes` surfaces are **untouched**, M35·1 / M35·9). M35 adds **no** new
  doc **to an existing surface** and **no** new index to an existing doc.
- **M35·10 — The closed `kiosk.*` `kw-l` key set is four-language.** Every
  new user-visible string M35 introduces is a `KnownTranslationKeys` entry
  present, **non-empty, in all four** languages (en/de/fr/da), pinned by
  `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` (the
  M34·11 / M33·11 closed-set precedent). The board / calendar / todos `kw-l`
  key sets are **untouched** (the M35·6 additive-only pin).
- **M35·11 — The six-member close flip is U10's responsibility.** The
  `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip lands in U10 (the
  M34·12 / M34-U08 precedent). The `WhatsNew.cs` registry gains one new entry
  (newest-first, the `0.57.0` row) naming M35 + ADR 0164. **M35 is the last
  milestone** — the close flips `M35` from `StatusNext` to `StatusDone` and
  promotes **no** successor (the `Single_InProgress_Milestone_Is_M35` pin is
  **removed** and replaced with a "no in-progress milestone" assertion — the
  M34-U08 precedent).

## FACES (pinned, 10)

The 10 FACES below are the **frozen** observable behaviors for M35 — each is
bound to one or more of the invariants above, and each is the contract a
pinned test (authored by U08) re-asserts. The names are frozen once written;
the drift-guard (U02 + U10) re-pins them.

- **M35-1 — A GlobalAdmin sets the kiosk playlist** (a board + a calendar
  week + todos, with view + refresh intervals) → it saves (**one**
  `kiosk.save` audit row) + `/kiosk` renders the first slide (M35·2, M35·5).
- **M35-2 — A signed-in resident opens `/kiosk`** → slide 1 renders (the
  first playlist entry's content, resolved via the viewer's **own** read
  decision) (M35·3, M35·6).
- **M35-3 — The kiosk auto-advances to the next slide after
  `ViewIntervalSeconds`** (wrapping to the first) (M35·4, M35·7).
- **M35-4 — The current slide's content auto-refreshes after
  `RefreshIntervalSeconds`** (re-fetched via the viewer's **own** read
  decision — the frozen seam re-run) (M35·4, M35·3).
- **M35-5 — A slide the viewer **cannot see** → renders the
  `kiosk.slide_denied` empty state** (no content leak, no 404 crash) (M35·3).
- **M35-6 — An **anonymous** visitor opens `/kiosk`** → redirected to
  sign-in (M35·3).
- **M35-7 — A non-GlobalAdmin (signed-in resident) opens the settings
  surface** → 403 (M35·2).
- **M35-8 — The playlist is **empty** (no slides configured) → `/kiosk`
  renders the `kiosk.empty` message** (not a blank, not an error) (M35·10).
- **M35-9 — The `KioskPlaylist` read + the `/kiosk` display reads emit
  **zero** `kiosk.*` `AccessAudit` rows** (the content rides the underlying
  surface's own audit) (M35·5).
- **M35-10 — The existing board / calendar / todos render surfaces are
  **unchanged** (a board detail / calendar / todos index visit is
  byte-identical)** (the M35·6 additive-only pin) (M35·6).

---

*Part 1 — Context, Scope, Invariants, FACES. Authored by U01, 2026-10-12.*
