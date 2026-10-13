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
  `KioskDocTypes.Configure` call, the M3 `PostDocTypes` precedent);
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
  `Projects` + M4 `Events`) are **reused, not re-added** (the `ProjectDocTypes` /
  `EventDocTypes` surfaces are **untouched**, M35·1 / M35·9). M35 adds **no** new
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

## Seams & contracts (Part 2, written by U02)

This part freezes the **exact shapes** U03–U08 must match, the closed `kiosk.*`
`kw-l` key set, the **12 pinned test names**, the **three-test acceptance
gate**, and the **drift-guard**. U03 (Core context + service), U04 (the
`/kiosk` display), U05 (the GlobalAdmin settings surface), U06 (the `kiosk.*`
`kw-l` keys), and U08 (the test pins) each read **their** sub-section of this
part as the primary source; the register
(`docs/plans-milestones/plan-m35-kiosk-mode.md`) wins for *which files exist*
and *what each unit does*. **No code, no build** in U02.

### 2.1 frozen seam list (exact C#)

**The M35 *reused* frozen seams** (verbatim — M35 adds **no** seam to them,
M35·3 / M35·9 "reused, not re-shape"):

```csharp
// src/Kumunita.Core/Projects/IProjectService.cs (M5, FROZEN — reused verbatim)
// board-detail seam (the kiosk's Board slide rides this):
Task<BoardDetailResult> GetBoardAsync(string boardId, string actorId,
    CancellationToken ct = default);
// todos-list seam (the kiosk's Todos slide rides this):
Task<TodoPage> ListTodosAsync(string? componentId, string? assigneeId, string actorId,
    int page, bool unassignedOnly = false, string? projectId = null,
    bool blockedOnly = false, CancellationToken ct = default,
    SortSpec? sort = null, int? pageSize = null);
// board-list seam (seeds the /admin/kiosk settings board picker, U05):
Task<BoardPage> ListBoardsAsync(string? componentId, string actorId, int page,
    string? projectId = null, CancellationToken ct = default,
    SortSpec? sort = null, int? pageSize = null);

// src/Kumunita.Core/Events/IEventService.cs (M4, FROZEN — reused verbatim)
// calendar-range seam (the kiosk's Calendar slide rides this):
Task<IReadOnlyList<Event>> ListInRangeAsync(
    DateTimeOffset windowStartUtc,
    DateTimeOffset windowEndUtc,
    string? componentId,
    string actorId,
    CancellationToken ct = default);
```

- Each of these already resolves the **signed-in viewer's** `Read` decision
  (the same `CanSeeAsync` / `CanAsync` pass that surface's own feed runs) and
  emits its **own** surface's `AccessAudit` row. M35 rides that decision —
  **re-run on every fetch** (M35·3, the M12 iCal "authorization rides the
  cookie" precedent) — and adds **zero** `kiosk.*` `AccessAudit` rows on those
  reads (M35·5). M35 adds **no** `AccessAction` / `AccessVia` / `Decide()`
  branch / `IAuditableResource` adapter over any of them (M35·1 / M35·3).

**The M35 *new* seam** — a **2-method** surface on the new `Kiosk` context
(the M35·1 / M35·5 pin):

```csharp
// src/Kumunita.Core/Kiosk/IKioskService.cs (new — M35·1 additive seam)
public interface IKioskService
{
    /// The singleton read (M35·5 — zero AccessAudit rows). Returns the row,
    /// or **null** when no `KioskPlaylist` row is configured (the M35-8
    /// floor: the display renders `kiosk.empty`).
    Task<KioskPlaylist?> GetPlaylistAsync(CancellationToken ct = default);

    /// The GlobalAdmin write (M35·5 — exactly one `kiosk.save` AccessAudit
    /// row, `TargetKind = "kiosk"`, `Via = Admin`). **Within the caller's
    /// session** so the write is in-transaction with any surrounding work (the
    /// M3 `CreatePostAsync` same-transaction precedent). **Upsert**: a missing
    /// row is created (the register's M35·2 pin — see §2.2 divergence note).
    /// Returns the saved row.
    Task<KioskPlaylist> SavePlaylistAsync(
        IReadOnlyList<KioskView> views,
        int viewIntervalSeconds,
        int refreshIntervalSeconds,
        string actorId,
        IDocumentSession session,
        CancellationToken ct = default);
}
```

### 2.2 new M35-owned Core types (exact C#)

**The closed enum** (the M35·2 pin):

```csharp
// src/Kumunita.Core/Kiosk/KioskViewKind.cs (new) — the closed 3-member set
public enum KioskViewKind
{
    Board,
    Calendar,
    Todos,
}
```

**The `KioskView` entry** — the **5-member M35·2 ceiling** (no field outside
the set may appear):

```csharp
// src/Kumunita.Core/Kiosk/KioskView.cs (new)
public sealed class KioskView
{
    /// <summary>Which surface this slide shows (the closed 3-member set).</summary>
    public KioskViewKind Kind { get; set; }
    /// <summary>The board id — **only when** <see cref="Kind"/> is <see cref="KioskViewKind.Board"/>.</summary>
    public string?   BoardId      { get; set; }
    /// <summary>`"day"` | `"week"` | `"month"` — **only when** <see cref="Kind"/> is <see cref="KioskViewKind.Calendar"/>; default `week`.</summary>
    public string?   CalendarView { get; set; } = "week";
    /// <summary>The community filter — **only when** <see cref="Kind"/> is <see cref="KioskViewKind.Todos"/>; `null` = all the viewer's visible todos.</summary>
    public string?   ComponentId  { get; set; }
    /// <summary>An optional per-slide display label; `null` = the surface's own header.</summary>
    public string?   Label        { get; set; }
}
```

**The `KioskPlaylist` singleton** — the **4-member M35·2 ceiling** (no field
outside the set may appear):

```csharp
// src/Kumunita.Core/Kiosk/KioskPlaylist.cs (new) — the M10 LocaleSettings /
// ADR 0004 §B.1 singleton idiom (the SiteContent.SingletonId shape).
public sealed class KioskPlaylist
{
    /// <summary>The singleton sentinel id (the SiteContent "singleton" /
    /// LocaleSettings idiom). One row per instance, keyed on this id.</summary>
    public const string SingletonId = "kiosk";

    public string Id { get; set; } = SingletonId;

    /// <summary>The **ordered** slide list (list position = the slide order,
    /// not a stored field).</summary>
    public IReadOnlyList<KioskView> Views { get; set; } = [];

    /// <summary>The slide-advance cadence (seconds). **Display-only, never an
    /// access input** (M35·4). `>= 5`; default 30.</summary>
    public int ViewIntervalSeconds    { get; set; } = 30;

    /// <summary>The content-refresh cadence (seconds), **faster** than the
    /// view cadence. **Display-only, never an access input** (M35·4).
    /// `>= 2` and `< ViewIntervalSeconds`; default 10.</summary>
    public int RefreshIntervalSeconds { get; set; } = 10;
}
```

**The `KioskDocTypes` registration surface** (the ADR 0004 §B.1 shape — one
`.Schema.For`, the M35·1 / M35·9 pin):

```csharp
// src/Kumunita.Core/Kiosk/KioskDocTypes.cs (new)
public static class KioskDocTypes
{
    public static void Configure(StoreOptions opts)
    {
        // The singleton has a conventional string Id (fixed to "kiosk"), so no
        // non-default convention or business-key index is needed — only the
        // explicit Schema.For makes the doc visible to Marten (the
        // SiteContentDocTypes / LocaleSettings *DocTypes precedent).
        opts.Schema.For<KioskPlaylist>();
    }
}
```

**The `KioskService` impl shape** (the `SiteContentService` structural
template, with **two** divergences pinned against the register below):

```csharp
// src/Kumunita.Core/Kiosk/KioskService.cs (new)
public sealed class KioskService : IKioskService
{
    private readonly IDocumentStore _store;
    public KioskService(IDocumentStore store, IAuthorizationService authz)
    {
        _store = store;
        // authz is carried for composition-root symmetry (the M23
        // ProfileFindService / M5 PostService precedent) but is **not** invoked
        // for standing — M35·1 pins "no Decide() branch"; the controller
        // ([Authorize(Roles = GlobalAdmin)]) enforces the standing (the
        // admin-surface precedent).
        _authz = authz;
    }

    // (1) GetPlaylistAsync — one QuerySession + LoadAsync<KioskPlaylist>("kiosk").
    //     Returns the row, or **null** when absent (the M35-8 floor). ZERO
    //     AccessAudit rows (M35·5).

    // (2) SavePlaylistAsync — **within the caller's `session`**:
    //     var row = await session.LoadAsync<KioskPlaylist>("kiosk", ct)
    //                   ?? new KioskPlaylist { Id = KioskPlaylist.SingletonId };
    //     row.Views                  = views;
    //     row.ViewIntervalSeconds    = viewIntervalSeconds;
    //     row.RefreshIntervalSeconds = refreshIntervalSeconds;
    //     session.Store(row);
    //     session.Store(new AccessAudit { ... Action = "kiosk.save",
    //                                    TargetKind = "kiosk",
    //                                    TargetId   = "kiosk",
    //                                    Via = AccessVia.Admin,
    //                                    Outcome = AccessOutcome.Allow });
    //     await session.SaveChangesAsync(ct);
    //     return row;
    //     Exactly ONE kiosk.save AccessAudit row (M35·5).
}
```

**Divergence notes (register is the source of truth — pinned here):**

- **Upsert vs. no-op.** The register's M35·2 assumption pins
  `SavePlaylistAsync` as an **upsert** — a missing `KioskPlaylist` row is
  **created**. The `SiteContentService.SaveAsync` structural template it
  mirrors is instead a **no-op on a missing row** (it "never load-or-creates",
  SITE·6 — the `SiteContent` seeder is the only writer that creates the row).
  **M35 pins the upsert** (the register wins): `SavePlaylistAsync` load-or-
  creates the singleton so a GlobalAdmin can configure the playlist on a
  fresh instance **without** a seeder. This is the one intentional departure
  from the `SiteContent` template. (Consequence: the U08 test
  `M35_2_SavePlaylist_NoRow_CreatesSingleton` asserts the create-on-missing
  behavior — the `SiteContent` template has no such test.)
- **Caller's session vs. own session.** `SavePlaylistAsync` takes the
  **caller's** `IDocumentSession session` (in-transaction with surrounding
  work — the M3 `CreatePostAsync` same-transaction precedent), whereas
  `SiteContentService.SaveAsync` opens its **own** session. **M35 pins the
  caller's-session shape** (the register's `IKioskService` signature).
- **Nullable read vs. never-null read.** `GetPlaylistAsync` returns
  `KioskPlaylist?` — **null** on a missing row (the M35-8 floor renders
  `kiosk.empty`), whereas `SiteContentService.GetAsync` returns a never-null
  in-code fallback. **M35 pins the nullable read** (the register's seam
  signature; the kiosk has no in-code fallback content — an empty playlist is
  a legitimate state, not a degraded one).

**No** new `AccessAction` / `AccessVia` / `Decide()` branch /
`IAuditableResource` adapter (the M35·1 / M35·3 pin). **No** `HttpClient` in
Core (the M35·8 pin, ADR 0006-D). The M5 `Projects` + M4 `Events` contexts
are **untouched** (M35·9).

### 2.3 the `/kiosk` display + settings surface (exact Web shapes)

> **U04-resume drift resolution (2026-10-12) — path 1 supersedes the partial
> rendering below.** The register's U04/U05 and this §2.3 assume the kiosk
> `@await Html.PartialAsync`s the board / calendar / todos **partials**. On
> disk those three surfaces are **full MVC views** (`BoardDetail.cshtml` /
> `TodosIndex.cshtml` / `Event/Calendar.cshtml` — each with `@model`,
> `ViewData["Title"]`, `@section Scripts`, `@inject`, and controller-seeded
> `ViewData` bags), and there are **no** board/calendar/todos partials to
> `@await` (the U04 drift pause). The lane owner chose **path 1**: each slide
> iframes the surface's **own route** (`/projects/boards/{id}` /
> `/events/calendar?view=…` / `/projects/todos`) inside the chrome-hidden
> stage. The iframe's navigation **re-runs the surface's own `Read` decision**
> (the frozen board / calendar / todos read seam — M35·3), so the M35·6
> "reuse, not re-author" pin is honored **more strongly** (the kiosk adds no
> slide view at all) and M35·9 (surfaces untouched) holds (no M5/M4 file
> edited). Consequences that supersede the text below:
> - The `object? FirstSlideContent` member of `KioskDisplayViewModel` is
>   **replaced** by `IReadOnlyList<KioskSlideViewModel> Slides` (each slide
>   carrying `Href` + `Denied` + `Label`) + a `FirstSlide` convenience — the
>   three unrelated surface view models cannot share one content member.
> - The `/kiosk/slide` refresh fetch returns a small JSON
>   `KioskSlideRefreshViewModel` (`Index` / `Denied` / `Href` / `DeniedKey`)
>   that `client/lib/kiosk.ts` (U07) uses to **re-point the iframe**, not to
>   swap innerHTML.
> - The `body.kmb-page-expanded` chrome-hide is applied via the view's inline
>   `<style>` (the ADR 0104 selector set, `style-src 'self' 'unsafe-inline'`;
>   no `_Layout.cshtml` touch, so the footprint stays ≤ 4 files — the "no
>   surface-file edits" constraint is **preserved**, not waived).
> - The M35-5 denied shape is honored by the controller **catching the frozen
>   seam's deny signals** (`GetBoardAsync`'s `KeyNotFoundException` /
>   `UnauthorizedAccessException`; `ListTodosAsync` / `ListInRangeAsync`'s
>   `UnauthorizedAccessException`) and rendering the `kiosk.slide_denied`
>   shape (no 404, no leak).
>
> The `SettingsGet` / `SettingsPost` rows and the `KioskSettingsViewModel`
> below are **unaffected** (U05's surface is unchanged). The two
> `setInterval`s in `kiosk.ts` (U07) now drive **iframe advance** + **iframe
> re-point**, not innerHTML swaps.

**The `KioskController`** (namespace `Kumunita.Web`, `src/Kumunita.Web/
Controllers/KioskController.cs`) — four actions:

| Action | Route | Gate | Behavior |
| --- | --- | --- | --- |
| `Index` | `GET /kiosk` | `[Authorize]` | Read the `KioskPlaylist` via `GetPlaylistAsync`; resolve the **first** slide's content through the **frozen** read seam for the **signed-in viewer** (a denied slide → the `kiosk.slide_denied` shape, M35-5); render `Views/Kiosk/Index.cshtml` (the chrome-hidden stage + the first slide). |
| `Slide` | `GET /kiosk/slide?slide=N&t=<ts>` | `[Authorize]` | The single-slide refresh fetch (the `t` timestamp busts any cache; a CSRF-free GET — the M12 iCal "the cookie is the authorization" precedent). Return the **Nth** slide's content via the **frozen** read seam; a slide the viewer cannot see returns the **denied-shape** view model (the `kiosk.slide_denied` message — not a 404). |
| `SettingsGet` | `GET /admin/kiosk` | `[Authorize(Roles = GlobalAdmin)]` | Render `Views/Kiosk/Settings.cshtml` (the playlist editor + the two interval pickers; a non-GlobalAdmin → **403**, M35-7). |
| `SettingsPost` | `POST /admin/kiosk` | `[Authorize(Roles = GlobalAdmin)]` | Bind `KioskSettingsViewModel`; on valid, commit `SavePlaylistAsync` (**one** `kiosk.save` audit row, M35·5) + redirect; on invalid, re-render with the error. |

**The `KioskDisplayViewModel`** (`src/Kumunita.Web/Models/
KioskDisplayViewModel.cs`):

```csharp
public sealed class KioskDisplayViewModel
{
    public IReadOnlyList<KioskView> Views { get; init; } = [];
    public int  ViewIntervalSeconds    { get; init; } = 30;
    public int  RefreshIntervalSeconds { get; init; } = 10;
    /// True when at least one slide is configured (false → render kiosk.empty, M35-8).
    public bool HasSlides  { get; init; }
    /// The first slide's resolved content (the board / calendar / todos view
    /// model the slide renders), or the kiosk.slide_denied shape (M35-5).
    public object? FirstSlideContent { get; init; }
    /// The kiosk.slide_denied label (present when a slide is denied, M35-5).
    public string? DeniedLabel { get; init; }
    /// The `#kiosk-stage` data-* attributes the client/lib/kiosk.ts reads
    /// (U07): the two intervals + the slide count.
    public int SlideCount { get; init; }
}
```

**The `KioskSettingsViewModel`** (`src/Kumunita.Web/Models/
KioskSettingsViewModel.cs`): the playlist editor binding + the interval
pickers + the board-picker seed:

```csharp
public sealed class KioskSettingsViewModel
{
    public IReadOnlyList<KioskView> Views { get; set; } = [];
    public int  ViewIntervalSeconds    { get; set; } = 30;
    public int  RefreshIntervalSeconds { get; set; } = 10;
    /// The board picker's options — seeded from the **frozen** M5
    /// IProjectService.ListBoardsAsync (M35·3 / M35·9 — reused, not re-shape).
    public IReadOnlyList<(string Id, string Title)> Boards { get; set; } = [];
    public bool IsValid { get; set; }
}
```

**The `Views/Kiosk/Index.cshtml` chrome-hidden layout** (the M35·6 pin): the
`body.kmb-page-expanded` class (the ADR 0104 chrome-hide idiom — the navbar /
footer / flash / pinned-announcement hidden, the slide fills the viewport) +
a `#kiosk-stage` element carrying the `data-*` attributes the `kiosk.ts`
reads (`data-view-interval`, `data-refresh-interval`, `data-slide-count`). The
board / calendar / todos **Razor partials are `@await Html.PartialAsync`'d
verbatim** (the M35·6 pin — the kiosk does **not** re-author them):
`Views/Projects/BoardDetail.cshtml` + `Views/Projects/TodosIndex.cshtml` +
`Views/Event/Calendar.cshtml` (note: the calendar partial's folder is
singular `Event`, U00 verified).

**The `client/lib/kiosk.ts` state machine** (the M35·7 pin — a plain-tsc,
no-dep, no-`any` IIFE, the `expand-page.ts` / `calendar-fullscreen.ts`
precedent): two `setInterval`s — (1) **advance** the visible slide to the
next (wrapping to the first) every `ViewIntervalSeconds`; (2) **re-fetch** the
current slide's content from `GET /kiosk/slide?slide=N&t=<ts>` every
`RefreshIntervalSeconds` (a same-origin `fetch` + innerHTML swap over the
server-rendered slide HTML). **Zero** new npm package, **zero** build step.

**The `site.css` kiosk rules** (the M35·6 / M35·7 pin): the `#kiosk-stage`
chrome-hide (reusing the `body.kmb-page-expanded` rules) + the slide-advance
transition (a CSS opacity/transform transition — no JS animation lib).

### 2.4 the closed `kiosk.*` `kw-l` key set

> **U04-resume (2026-10-12) — the set is inseparably four-language.** The
> register's assumption that "the `de` / `fr` / `da` values are U06's to
> author" (i.e. an en-only floor exists until U06) is **superseded**: on disk
> the `kiosk.*` keys were **absent** (not an en-only floor — the `kw-l`
> TagHelper would render the raw key), **and** the Core.Tests parity tests
> (`De/Fr/DaValues_Keys_Match_AllKeys_Exactly_NoEmptyValues`) enforce
> **key-for-key set equality** between en/de/fr/da, so there is **no valid
> en-only intermediate state**. Combined with the Web.Tests view-scan test
> (`KwLRegistryConsistencyTests.Every_KwL_Key_In_A_View_Is_Registered`), U04's
> view **cannot** reference `kiosk.*` keys unless all four languages are
> present. **U04 therefore co-shipped the 8 keys in en/de/fr/da** (absorbing
> U06's deliverable), and **U06 is re-scoped to a verification unit**. The
> `en` values below remain the **source text** (the ADR 0015 D1
> provider-floor discipline); the `de` / `fr` / `da` values are now authored
> (by U04), present and non-empty in all four (verified green). The board /
> calendar / todos `kw-l` key sets are **untouched** (the M35·6 additive-only
> pin — M35 **adds** this set, it does not re-author the M5 / M4 set).

**8 keys**, each present, **non-empty, in all four** languages (en/de/fr/da),
pinned by `KwLRegistryConsistencyTests` (Web.Tests) +
`KnownTranslationKeys_ParityTests` (Core.Tests) (the M35·10 pin, the M34·11
closed-set precedent). The `en` values below are the **source text** (the
ADR 0015 D1 `kw-l` provider-floor discipline). The board / calendar /
todos `kw-l` key sets are **untouched** (the M35·6 additive-only pin — M35
**adds** this set, it does not re-author the M5 / M4 set):

| Key | en value (source text) | Consumed by |
| --- | --- | --- |
| `kiosk.title` | Neighbourhood board | the display heading (U04) |
| `kiosk.empty` | The kiosk has no slides configured yet. | the empty playlist (U04, M35-8) |
| `kiosk.slide_denied` | You don't have access to this view. | a denied slide (U04, M35-5) |
| `kiosk.next_slide` | Next view | the advance control (U04 / U07) |
| `kiosk.refreshed` | Updated just now | the refresh stamp (U07) |
| `kiosk.settings.title` | Kiosk display | the settings heading (U05) |
| `kiosk.settings.view_interval` | Change view every (seconds) | the view-interval picker (U05) |
| `kiosk.settings.refresh_interval` | Refresh content every (seconds) | the refresh-interval picker (U05) |

### 2.5 pinned seam tests (exact names)

**12 pinned tests** — no unit may introduce a test whose exact name is not in
this list (unit-series rule §3). **6 Core** in
`tests/Kumunita.Core.Tests/Kiosk/KioskServiceTests.cs` (the M33
`StorageMetricsHistoryTests` `PostgresFixture` seed-and-assert shape), **6
Web** in `tests/Kumunita.Web.Tests/KioskControllerTests.cs` (the M33
`AdminStorageMetricsHistoryTests` NSubstitute + minimal-`WebApplication`
shape):

`tests/Kumunita.Core.Tests/Kiosk/KioskServiceTests.cs`:

1. `M35_2_GetPlaylist_NoRow_ReturnsNull` — `GetPlaylistAsync` over a **no
   row** store returns `null` (the M35·2 / M35-8 floor).
2. `M35_2_GetPlaylist_Configured_ReturnsRow` — `GetPlaylistAsync` over a
   **configured** singleton returns the row (the M35·2 pin).
3. `M35_5_GetPlaylist_WritesNoAuditRow` — `GetPlaylistAsync` writes **zero**
   `AccessAudit` rows (the M35·5 pin).
4. `M35_5_SavePlaylist_OneKioskSaveAuditRow` — `SavePlaylistAsync` commits
   **exactly one** `kiosk.save` `AccessAudit` row (`TargetKind = "kiosk"`,
   `Via = Admin`) (the M35·5 pin).
5. `M35_2_SavePlaylist_NoRow_CreatesSingleton` — `SavePlaylistAsync` over a
   **no row** store **creates** the singleton (the upsert — the M35·2 pin;
   the §2.2 divergence from the `SiteContent` no-op template).
6. `M35_SavePlaylist_AtomicInCallerSession` — `SavePlaylistAsync`
   **within the caller's session** commits atomically (the M3
   same-transaction pin).

`tests/Kumunita.Web.Tests/KioskControllerTests.cs`:

7. `M35_3_SignedIn_Sees_ChromeHidden_FirstSlide` — a **signed-in** viewer
   visits `/kiosk` → the chrome-hidden layout + the first slide render (the
   M35·3 / M35·6 pin).
8. `M35_3_Anonymous_RedirectedToSignIn` — an **anonymous** visitor visits
   `/kiosk` → redirected to sign-in (the M35·3 / M35-6 pin).
9. `M35_5_SlideDenied_SignsInViewer_SeesDeniedShape` — `GET
   /kiosk/slide?slide=N` over a slide the viewer **cannot see** → the
   `kiosk.slide_denied` shape (no content, no 404 — the M35·5 / M35-5 pin).
10. `M35_7_NonGlobalAdmin_AdminKiosk_403` — a **non-GlobalAdmin** visits
    `/admin/kiosk` → 403 (the M35·2 / M35-7 pin).
11. `M35_5_GlobalAdmin_SavePlaylist_OneAuditRow_Persists` — a
    **GlobalAdmin** POSTs `/admin/kiosk` → one `kiosk.save` audit row + the
    playlist persists (the M35·5 / M35-1 pin).
12. `M35_6_ExistingSurfaces_RenderUnchanged` — the **existing** board /
    calendar / todos surfaces render **unchanged** (a board detail / calendar
    / todos index visit is byte-identical — the M35·6 additive-only pin).

### 2.6 acceptance gate (U09 records)

The **three-test shape** (U09 executes and records it in §"Run result";
*using* the 12 §2.5 tests as the part-vs-whole evidence):

- **Closed loop** — a `GlobalAdmin` sets the playlist (a board + a calendar
  week + todos, with view + refresh intervals) → `/kiosk` renders the first
  slide for a signed-in viewer (the full request → read → render loop
  end-to-end, M35-1 / M35-2).
- **Handoff** — a signed-in viewer's `/kiosk` slide resolves via the
  **frozen** board / calendar / todos read seam — the **same `Read` decision**
  as that surface's own feed — so the slide is a "handoff" from the
  underlying surface to the display (the frozen-seam → slide contract, M35·3).
- **Part-vs-whole** — the **12-test pin list is the whole**; the closed-loop
  + handoff are the parts; **all must pass together** (a green 12-test run
  *is* the gate evidence).

### 2.7 drift-guard (frozen once written)

The following are **frozen pins**. Any unit that finds a mismatch between
these and the register, the handoff note, or the code is in drift and must
record a `## U<m> — Drift pause` in the handoff note and stop (unit-series
rule §13):

- **The 11-invariant set (U01's table, Part 1)** — **M35·1 through M35·11**
  (contiguous; no deliberate gaps, unlike M34). Any text that renames, skips,
  or re-splits an id is a drift.
- **The 10 FACES (U01's list)** — M35-1 through M35-10, verbatim, each bound
  to the invariant ids stated in Part 1.
- **The frozen seam list (§2.1)** — the M5 `GetBoardAsync` /
  `ListTodosAsync` / `ListBoardsAsync` + the M4 `ListInRangeAsync`
  **reused verbatim** (no re-shape); the **new** `IKioskService` **2-method**
  surface (`GetPlaylistAsync` + `SavePlaylistAsync`).
- **The new Core types (§2.2)** — `KioskPlaylist` **4 members** (`Id`
  sentinel `"kiosk"`, `Views`, `ViewIntervalSeconds` default 30,
  `RefreshIntervalSeconds` default 10) + `KioskView` **5 members**
  (`Kind`, `BoardId?`, `CalendarView?` default `"week"`, `ComponentId?`,
  `Label?`) + `KioskViewKind` **3 members** (`Board` / `Calendar` / `Todos`)
  + `KioskDocTypes` **one** `.Schema.For`; no field outside any of these sets.
  The **upsert** (create-on-missing) + **caller's-session** + **nullable
  read** divergences are pinned and are **not** drift — they are the
  intentional departures from the `SiteContent` template.
- **The `/kiosk` display + settings surface (§2.3)** — the 4
  `KioskController` actions (`Index` / `Slide` / `SettingsGet` /
  `SettingsPost`), the `KioskDisplayViewModel` + `KioskSettingsViewModel`
  member sets, the `body.kmb-page-expanded` chrome-hide, the board / calendar
  / todos partials `@await`'d **verbatim**, the `kiosk.ts` two-`setInterval`
  state machine, and the `site.css` kiosk rules.
- **The closed `kiosk.*` key set (§2.4)** — exactly **8 keys**, four-language,
  the board / calendar / todos `kw-l` sets **untouched**.
- **The 12 test names (§2.5)** — verbatim, in the two named files/classes;
  no test name outside the list.

---

*Part 2 — Seams & contracts (the reused frozen seams, the new `IKioskService`
2-method seam, the `KioskPlaylist` / `KioskView` / `KioskViewKind` shapes +
the `KioskDocTypes` surface + the `KioskService` impl shape [upsert /
caller's-session / nullable-read divergence notes], the `/kiosk` display +
settings surface, the closed `kiosk.*` `kw-l` key set, the 12 pinned test
names, the three-test acceptance gate, the drift-guard). Authored by U02,
2026-10-12. Companion ADR: `docs/adr/0164-kiosk-mode.md` (Draft).*

### Run result (M35 acceptance gate — 2026-10-12)

**(a) Three-test gate (§2.6)** — **Closed loop PASS** · **Handoff PASS** ·
**Part-vs-whole PASS** (the 12-test list is the whole; all 12 green).
Evidence: `M35_5_GlobalAdmin_SavePlaylist_OneAuditRow_Persists` (a
`GlobalAdmin` POSTs the playlist to `/admin/kiosk` → one `kiosk.save` audit
row + the playlist persists) → `M35_3_SignedIn_Sees_ChromeHidden_FirstSlide`
(a signed-in viewer's `GET /kiosk` renders the chrome-hidden stage + the first
slide — the full request → read → render loop, M35-1 / M35-2) · the
frozen-seam → slide contract (M35·3): `M35_5_SlideDenied_SignsInViewer_SeesDeniedShape`
(a slide the viewer cannot see resolves via the **frozen** board / calendar /
todos read seam into the `kiosk.slide_denied` shape — no content leak, no 404)
+ `M35_6_ExistingSurfaces_RenderUnchanged` (the board / calendar / todos
surfaces render **unchanged** — the kiosk frames them, it does not re-author
them) · the 12-test run green together.

**(b) 12-test part-vs-whole (verbatim §2.5)** — **12 / 12 GREEN**:
- **Core (6), `Kumunita.Core.Tests.KioskServiceTests`:**
  `M35_2_GetPlaylist_NoRow_ReturnsNull` ·
  `M35_2_GetPlaylist_Configured_ReturnsRow` ·
  `M35_5_GetPlaylist_WritesNoAuditRow` ·
  `M35_5_SavePlaylist_OneKioskSaveAuditRow` ·
  `M35_2_SavePlaylist_NoRow_CreatesSingleton` ·
  `M35_SavePlaylist_AtomicInCallerSession`.
  Run: `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll`
  → **6 / 6 green** (suite **1465 total, 0 failed, 0 errors**, `RUN_EXIT=0`;
  Testcontainers `postgres:18`).
- **Web (6), `Kumunita.Web.Tests.KioskControllerTests`:**
  `M35_3_SignedIn_Sees_ChromeHidden_FirstSlide` ·
  `M35_3_Anonymous_RedirectedToSignIn` ·
  `M35_5_SlideDenied_SignsInViewer_SeesDeniedShape` ·
  `M35_7_NonGlobalAdmin_AdminKiosk_403` ·
  `M35_5_GlobalAdmin_SavePlaylist_OneAuditRow_Persists` ·
  `M35_6_ExistingSurfaces_RenderUnchanged`.
  Run: `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll`
  → **6 / 6 green** (suite **1039 total, 0 failed, 0 errors**, the 1 skip
  is pre-existing, `RUN_EXIT=0`).

**(c) Still-open drift (per the handoff note)** —
- **U04 drift pause — RESOLVED.** U04's kickoff verification found the three
  slide surfaces are **full MVC views**, not partials
  (`BoardDetail.cshtml` / `TodosIndex.cshtml` / `Event/Calendar.cshtml`), so
  the register's `@await Html.PartialAsync` premise could not hold. The
  **U04-resume** resolved it via **path 1: iframe the live surface routes** —
  each slide resolves to the surface's **own route** (`/projects/boards/{id}`
  / `/events/calendar?view=…` / `/projects/todos`) inside the chrome-hidden
  stage (`frame-ancestors 'self'`). This **supersedes** the §2.3
  `object? FirstSlideContent` shape with `IReadOnlyList<KioskSlideViewModel>
  Slides`, honors the M35·6 "reuse, not re-author" pin **more strongly** (no
  slide view authored), and keeps M35·9 **held** (no M5/M4 file edited).
  Recorded in the design doc §2.3 drift note + the register's `§U04-resume`.
  No red pin remains — all 12 §2.5 tests green.
- No other `## U<m> — Drift pause` section is recorded (U00 / U01 / U02 / U03
  / U05 / U06 / U07 / U08 shipped without a drift pause).
