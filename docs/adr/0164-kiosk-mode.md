# ADR 0164 — Kiosk mode (M35) — a full-page / full-screen display that automatically cycles a GlobalAdmin-set playlist of the community's own surfaces (boards, calendar, to-dos), changing view on a set interval and refreshing each view's content on a separate, faster interval

Status: Accepted
Date: 2026-10-12

The README roadmap names M35 exactly: "**Kiosk mode** — a full-page /
full-screen display that automatically cycles a preset list of views (e.g.
Kanban board A, calendar week, Kanban board B, calendar today, todos),
changing view on a set interval and refreshing each view's content on a
separate, faster interval." M35 settles the **shared physical display**
question M1–M34 never answered: every surface shipped so far is something a
resident reads **at their own desk**; a neighborhood's shared life happens on
a **wall display** (the community hall, the library, the café). Today, putting
Kumunita on that display is a manual chore — a human stands at the TV, clicks
from one feed to the next, and refreshes by hand. M35 adds the **kiosk**: a
**full-page / full-screen display** (`GET /kiosk`) that **automatically
cycles** a **GlobalAdmin-set** playlist of the resident's own surfaces and
**refreshes each view's content on a separate, faster interval**.

**The one thing that separates ADR 0164 from the M34 precedent it otherwise
mirrors — new context + reused read seams (M35·1).** M34 added **no** new
bounded context — it extended the existing M13 `Usage` surface with a derived
read ("derived, not captured"). M35 is the **opposite shape** on the *display*
surface: it **creates** a new bounded context `Kumunita.Core.Kiosk` (the M31
`ErrorReports` / M11 `Portability` / M13 `Usage` precedent) to hold the
**operator's kiosk configuration** — one singleton doc (`KioskPlaylist`), one
doc-type surface (`KioskDocTypes`), one service (`IKioskService`, the 2-method
seam) — and then **reuses, without re-shaping**, the **frozen** M5 / M4 read
seams to resolve each slide's content for the signed-in viewer. The kiosk's
per-view content is **not** stored in the `Kiosk` context: the playlist doc
carries only the **ordered view list + the two intervals**; the content is
always resolved **live** through the underlying surface's own read seam
(M35·3). So the kiosk adds **no** new `AccessAction` / `AccessVia` /
`Decide()` branch / `IAuditableResource` adapter (M35·1 / M35·3), the existing
board / calendar / todos render partials are `@await`'d verbatim (M35·6), and
the only `kiosk.*` `AccessAudit` row is the one `kiosk.save` write row on the
playlist (M35·5).

The one thing the whole lane respects: M35 is **new context + reused read
seams** (M35·1 / M35·3) on the display surface. It **creates** the one new
`Kiosk` context — the `KioskPlaylist` singleton (the **4-member M35·2
ceiling**: `Id` sentinel `"kiosk"` + `Views` `IReadOnlyList<KioskView>` +
`ViewIntervalSeconds` + `RefreshIntervalSeconds`) + the `KioskView` entry
(the **5-member M35·2 ceiling**: `Kind` + `BoardId?` + `CalendarView?` +
`ComponentId?` + `Label?`) + the `KioskViewKind` enum (`Board` | `Calendar` |
`Todos`) + the `KioskDocTypes` registration surface (one `.Schema.For`, the
ADR 0004 §B.1 shape) + the `IKioskService` 2-method seam (`GetPlaylistAsync`
+ `SavePlaylistAsync`) + the `KioskService` impl (the singleton read + the
GlobalAdmin write with one `kiosk.save` audit row) — and **reuses** the
**frozen** M5 `IProjectService` board-detail + todos-list + board-list read
seams + the M4 `IEventService.ListInRangeAsync` calendar-range seam (M35·3 /
M35·9) + the existing board / calendar / todos **Razor partials** (M35·6) +
the ADR 0104 `body.kmb-page-expanded` chrome-hide idiom (M35·6). It **adds**
the `KioskController` (`GET /kiosk` + `GET /kiosk/slide` + `GET/POST
/admin/kiosk`), the `KioskDisplayViewModel` + `KioskSettingsViewModel`, the
`Views/Kiosk/Index.cshtml` chrome-hidden display + `Views/Kiosk/Settings.cshtml`
playlist editor, the `client/lib/kiosk.ts` auto-cycle / auto-refresh state
machine (tsc-only, M35·7), the `site.css` kiosk rules, and the closed
`kiosk.*` `kw-l` key set × en/de/fr/da (8 keys, M35·10). It adds **no** new
bounded context beyond `Kiosk` (the M5 / M4 contexts are **reused, not
re-added**, M35·9), **no** new `AccessAction` / `AccessVia` / `Decide()`
branch / `IAuditableResource` adapter (M35·1 / M35·3), **no** second
`kiosk.*` `AccessAudit` row beyond the one `kiosk.save` write row (M35·5),
**no** new dependency / npm package / build step (M35·7), and **no** EF
migration (the new doc registers through the new `KioskDocTypes` surface, not
an EF table — ADR 0004 §B). Core stays **HTTP-free + Wolverine-free** (M35·8,
ADR 0006-D). **M35 is the last milestone on the roadmap** — the close flips
M35 → done and promotes **no** successor (M35·11).

M35 rides **frozen** seams, adding a small, additive delta:

- **The M5 `Kumunita.Core.Projects` context** (`src/Kumunita.Core/Projects/`)
  — the "a bounded context per concern" house rule already points
  board / todo-shaped reads at the `Projects` context; M35 **reuses** the
  `IProjectService` board-detail + todos-list + board-list read seams
  (**untouched**, M35·9).
- **The M4 `Kumunita.Core.Events` context** (`src/Kumunita.Core/Events/`) —
  the **frozen** `IEventService.ListInRangeAsync` calendar-range seam the
  kiosk's calendar slide rides (**untouched**, M35·9).
- **The board / calendar / todos Razor partials**
  (`Views/Projects/BoardDetail.cshtml` + `Views/Projects/TodosIndex.cshtml` +
  `Views/Event/Calendar.cshtml`) — the **frozen** render surface the kiosk
  `@await`s verbatim (M35·6).
- **The ADR 0104 chrome-hide idiom** (`client/lib/expand-page.ts` — the
  `body.kmb-page-expanded` class + the `.page-expand-toggle` /
  `.page-expand-close` selectors + the Escape-key exit) — the **frozen**
  fullscreen shape the `kiosk.ts` reuses (M35·6).
- **The `SiteContent` / `LocaleSettings` singleton shape**
  (`SiteContent.SingletonId = "singleton"`, the singleton sentinel idiom) —
  the **structural template** the `KioskPlaylist` / `KioskService` mirror,
  with the two pinned divergences (upsert vs. no-op; caller's-session vs. own
  session) resolved in favor of the register (the design doc §2.2).

## Decision

- **D1 — M35 is a new bounded context `Kumunita.Core.Kiosk`, not a re-shape
  of an existing one (M35·1).** M35 **creates** the one new `Kiosk` context
  (the M31 `ErrorReports` / M11 `Portability` / M13 `Usage` precedent) with
  one singleton doc (`KioskPlaylist`), one doc-type surface (`KioskDocTypes`,
  the ADR 0004 §B.1 shape), and one service (`IKioskService`, the 2-method
  seam). It **reuses** the frozen M5 / M4 read seams (M35·3) and the existing
  board / calendar / todos render partials (M35·6). It adds **no** new
  `AccessAction`, **no** new `AccessVia`, **no** `Decide()` branch, and **no**
  `IAuditableResource` adapter (M35·1 / M35·3).
- **D2 — The playlist is a GlobalAdmin-set singleton (M35·2).** The
  `KioskPlaylist` doc is a **singleton** (the `SiteContent` / `LocaleSettings`
  shape, ADR 0004 §B.1) — the 4-member ceiling (`Id` sentinel `"kiosk"`,
  `Views`, `ViewIntervalSeconds`, `RefreshIntervalSeconds`) + the `KioskView`
  entry's 5-member ceiling. The **standing to write it is GlobalAdmin**
  (enforced by the `[Authorize(Roles = GlobalAdmin)]` controller gate, not the
  service — the admin-surface precedent). **No per-resident playlist in v1**
  (a named deferral).
- **D3 — The display is signed-in and rides the frozen read seams (M35·3).**
  `GET /kiosk` is `[Authorize]`. Each slide's content is resolved by the
  **frozen** board / calendar / todos read seam for the **signed-in viewer**
  (the same `Read` decision as that surface's own feed — **re-run on every
  fetch**, the M12 iCal precedent). A slide the viewer **cannot see** renders
  the `kiosk.slide_denied` empty state — **no content leak, no 404 crash**
  (M35-5). **No break-glass, no new standing, no new authorization branch.**
- **D4 — The two intervals are display-only, never an access input
  (M35·4).** `ViewIntervalSeconds` (the slide-advance cadence) +
  `RefreshIntervalSeconds` (the content-refresh cadence, **faster**) are
  **config** on the `KioskPlaylist` singleton — **never** a DB access column,
  **never** an authorization input (the M34·7 / M13 `windowDays` "display-only,
  never an access input" precedent). Both are **GlobalAdmin-set** (M35·2).
- **D5 — One `kiosk.save` audit row per write; the display reads emit zero
  `kiosk.*` rows (M35·5).** The GlobalAdmin playlist write commits **one**
  `kiosk.save` `AccessAudit` row (`TargetKind = "kiosk"`, `Via = Admin` — the
  admin-surface write-lane convention). The `/kiosk` display's per-view content
  reads emit **zero** `kiosk.*` `AccessAudit` rows — the content rides the
  **underlying surface's own** audit rows (the board's / calendar's / todos'
  `Read` audit). The `KioskPlaylist` **read** (config) emits **zero** rows.
- **D6 — The existing render surface is reused, not re-authored (M35·6).** The
  kiosk's per-slide section renders the **same** board / calendar / todos Razor
  partials (the "integration over features" spine). The kiosk adds **only** a
  chrome-hidden layout (the ADR 0104 `body.kmb-page-expanded` idiom) + the
  `client/lib/kiosk.ts` state machine + the `kiosk.*` `kw-l` keys.
- **D7 — Zero new dependency, zero new npm package, zero build step
  (M35·7).** The `client/lib/kiosk.ts` is **tsc-only** (the ADR 0031 pin). The
  auto-cycle + auto-refresh is a plain `setInterval` state machine.
  `package.json` stays `typescript`-only.
- **D8 — Core stays HTTP-free + Wolverine-free (M35·8).** The `KioskService`
  is a plain store-composing service (the `AddTransient<IKioskService>` shape,
  the M31 / M13 precedent). There is **no** `HttpClient` in Core (ADR 0006-D)
  and **no** outbound channel.
- **D9 — No new bounded context beyond `Kiosk` (M35·9).** The M5 `Projects` +
  M4 `Events` contexts are **reused, not re-added** (the `ProjectDocTypes` /
  `EventDocTypes` surfaces are **untouched**). M35 adds **no** new doc **to an
  existing surface** and **no** new index to an existing doc.
- **D10 — The closed `kiosk.*` `kw-l` key set is four-language (M35·10).** 8
  keys, each present, non-empty, in all four languages (en/de/fr/da), pinned
  by `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests`. The
  board / calendar / todos `kw-l` key sets are **untouched** (M35·6).

## Status

**Draft.** M35 has not yet shipped — the six-member close flip (M35·11) is
U10's responsibility, and **M35 is the last milestone** (the close promotes no
successor). U10 flips this to `Accepted` at the close.

## Consequences

- **A new bounded context `Kumunita.Core.Kiosk`** (one `KioskPlaylist`
  singleton + one `KioskView` entry + one `KioskViewKind` enum + one
  `KioskDocTypes` surface + one `IKioskService` 2-method seam + one
  `KioskService` impl) — registered through the new `KioskDocTypes` surface,
  not an EF table (ADR 0004 §B).
- **A new signed-in display route** (`GET /kiosk` + `GET /kiosk/slide`) + a
  new GlobalAdmin settings surface (`GET/POST /admin/kiosk`).
- **A new tsc-only state machine** (`client/lib/kiosk.ts`) + the `site.css`
  kiosk rules + the closed `kiosk.*` `kw-l` key set × en/de/fr/da (8 keys).
- **No re-seed** (a missing `KioskPlaylist` row reads as **empty** — the
  M10 `LocaleSettings` "a missing row reads as a sensible floor" precedent —
  and `SavePlaylistAsync` **upserts** the singleton, the design doc §2.2
  divergence from the `SiteContent` no-op template).
- **No new `AccessAudit` row on the read** (a config read — zero rows; the
  display's per-view content rides the underlying surface's own audit rows).
- **The board / calendar / todos surfaces are unchanged** (a board detail /
  calendar / todos index visit is byte-identical — the M35·6 additive-only
  pin).
- **The `WhatsNew` registry gains a `0.57.0` entry** (the required sixth
  close-flip member per AGENTS.md — U10's).

## Tests

- `KioskServiceTests` (6 tests, `Kumunita.Core.Tests`) — the seam's
  invariants (no-row read → null, configured read → the row, read → zero audit
  rows, write → one `kiosk.save` row, no-row write → upsert creates the
  singleton, write → atomic in the caller's session), the M33
  `StorageMetricsHistoryTests` `PostgresFixture` shape.
- `KioskControllerTests` (6 tests, `Kumunita.Web.Tests`) — the display +
  settings surface's behavior (signed-in sees the chrome-hidden first slide,
  anonymous → sign-in redirect, denied slide → `kiosk.slide_denied` shape,
  non-GlobalAdmin → 403, GlobalAdmin save → one audit row + persists, existing
  surfaces render unchanged), the M33 `AdminStorageMetricsHistoryTests` shape.
- `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` (the
  closed `kiosk.*` key set's four-language parity — U06 adds the 8 keys).

## References

- ADR 0004 — persistence & schema evolution (the §B.1 additive singleton +
  doc-type-surface idiom).
- ADR 0006 — module boundary contracts (the "thin token, fat authorization"
  posture + the Core HTTP-free D rule).
- ADR 0015 — UI view-localization mechanics (the `kw-l` provider-floor
  discipline + the closed key set).
- ADR 0031 — the WYSIWYG editor + toolbar (the tsc-only, no-dep, no-`any`
  JS pin the `kiosk.ts` mirrors).
- ADR 0104 — the full-page + calendar-fullscreen modes (the
  `body.kmb-page-expanded` chrome-hide idiom the kiosk reuses).
- ADR 0110 — the `WhatsNew` registry (the required sixth close-flip member).
- ADR 0114 — the M13 analytics surface (the `windowDays` "display-only, never
  an access input" precedent the M35·4 intervals mirror).
- ADR 0162 — analytics history (M34 — the "derived, not captured" precedent
  M35 inverts to "new context + reused read seams").
- ADR 0163 — home upcoming events & to-dos (the "display, not data" precedent
  M35 shares for its signed-in display surface).

## Amendment (2026-10-15) — per-resident playlist, moved out of admin

The original decision was a **single GlobalAdmin-set** `KioskPlaylist`
singleton (`Id = "kiosk"`) for one **shared** wall display, configured only at
`/admin/kiosk` (the `[Authorize(Roles = GlobalAdmin)]` gate, `Via = Admin`
audit row). A product decision has since moved the kiosk into the per-resident
settings surface — a signed-in user should be able to configure **their own**
`/kiosk` display. The following parts of this ADR are amended; the rest of the
decision (the `Kiosk` bounded context, the `KioskPlaylist` / `KioskView` doc
shapes, the `IKioskService` 2-method seam, the `GET /kiosk` +
`GET /kiosk/slide` display, the `kiosk.ts` state machine, and the closed
`kiosk.*` `kw-l` key set) is unchanged.

- **Per-resident, not a shared singleton.** `KioskPlaylist.Id` is now the
  **acting resident's subject id** (one row per user — the `LocaleSettings`
  per-user preference shape, ADR 0004 §B.1), not the fixed `"kiosk"` sentinel.
  The `SingletonId` constant is removed. The `/kiosk` display resolves the
  **viewer's** playlist, and `/kiosk/slide` resolves the viewer's row. A
  resident who has not configured one sees the `kiosk.empty` shape (the
  M35-8 floor, unchanged).
- **`IKioskService` keyed by actor.** `GetPlaylistAsync` now takes an
  `actorId` argument (loads that resident's row). `SavePlaylistAsync` upserts
  the row **keyed on `actorId`**.
- **Settings surface: `/settings/kiosk`, any signed-in user.** The GlobalAdmin
  gate is dropped — `KioskController.SettingsGet` / `SettingsPost` are now
  `[Authorize]`d (class-level, no role), matching the `/settings/*` resident
  convention. The M35-7 403 pin is replaced by a "no role gate" pin.
- **Audit row: `Via = Owner`.** `KioskService.SavePlaylistAsync` now writes the
  `kiosk.save` row with `Via = AccessVia.Owner` (the acting resident writes
  their own playlist — the `timezone.set-default` per-resident standing, not
  the `Admin` surface) and `TargetId = actorId`.
- **Settings tabs + admin pointer.** The kiosk editor gains a tab in the
  `/settings` strip (`_SettingsTabs.cshtml`), and the `/admin/platform` kiosk
  row now points at `/settings/kiosk` (every resident configures their own).
- **Retired address.** `GET /admin/kiosk` is kept as a redirect to
  `/settings/kiosk` (the `/settings/email-language` → tab redirect precedent,
  ADR 0080) so a saved link / bookmark / already-open old tab lands the user on
  the new page rather than 404ing.
- **Help.** A new resident guide `Kiosk` (nested under `settings`,
  `GuideParentMap`) documents the per-resident `/settings/kiosk` + `/kiosk`
  surface.
- **Docs sync.** README roadmap + `STATUS.md` + the `WhatsNew` registry (a new
  version row) reflect the per-resident move (the ADR 0110 sixth close-flip
  member).

## Amendment (2026-10-15) — display mode: full page vs. full screen

The original M35 display hid the page chrome and let the stage fill the
viewport, but in practice (a) some of the chrome the layout renders — the
onboarding banner, the what's-new toast, and in the variant-C nav the icon-rail
+ topbar — was **not** in the chrome-hide list, so the `/kiosk` display still
showed chrome above the stage; and (b) a resident operating a **wall-display
TV** wanted the kiosk to own the **whole monitor** (the browser's tabs and
address bar hidden too), not just the browser's window. This amendment makes
the display **true full-page** and adds a **per-resident display-mode choice**.

The bounded context, the `KioskPlaylist` / `KioskView` doc shapes (the
5-member `KioskPlaylist` ceiling now includes `DisplayMode` — the original
4-member ceiling is extended additively), the 2-method `IKioskService` seam
(`SavePlaylistAsync` gains a `displayMode` parameter), the `GET /kiosk` +
`GET /kiosk/slide` display, the `kiosk.ts` state machine, and the one
`kiosk.save` audit row are **unchanged in shape** — this is an **additive
delta**, the per-resident shape preserved.

- **True full-page.** The `/kiosk` display's chrome-hide list is extended to
  **every** chrome element the layout renders (both nav variants: the `.navbar`
  row and the `.kmb-rail` + `.kmb-topbar` pair; plus the onboarding banner,
  the what's-new toast, the flash toast, the pinned-announcement, the guest
  notice, and the footer), and the stage is stretched to fill the browser
  viewport edge-to-edge (the ADR 0104 `body.kmb-page-expanded` chrome-hide
  idiom, applied in the view's inline `<style>`). The per-slide chrome (inside
  the iframe) is already hidden by the layout's `body.kmb-embed` rules (the
  `Sec-Fetch-Dest: iframe` signal) — unchanged.
- **`KioskDisplayMode` enum + `KioskPlaylist.DisplayMode`.** A new
  `KioskDisplayMode` enum (`FullPage` | `FullScreen`) and a new
  `KioskPlaylist.DisplayMode` member (the 5-member ceiling), **display-only,
  never an access input** (M35·4 — a display preference, not an authorization
  input). Default `FullPage` (a fresh row is the in-browser full-page shape).
- **`IKioskService.SavePlaylistAsync` gains `displayMode`.** The
  `SavePlaylistAsync` seam now takes the `displayMode` and persists it with the
  row (the upsert shape is unchanged; the read returns it).
- **Settings surface: the display-mode picker.** `GET/POST /settings/kiosk`
  gains a **Display mode** picker (`kiosk.settings.display_mode` +
  `.display_mode.full_page` + `.display_mode.full_screen` +
  `.display_mode.hint` kw-l key set × en/de/fr/da — the closed `kiosk.*` key
  set is extended from 8 to 12). The controller parses the form value and, on a
  blank / invalid value, falls back to `FullPage` (a display preference is not
  a validation gate — the M35·4 "display-only" precedent: the two interval
  floors remain the only hard validations). The one `kiosk.save` audit row is
  unchanged (still `Via = Owner`, keyed on the resident).
- **`/kiosk` display: `data-display-mode` + the FullScreen buttons.** The
  `#kiosk-stage` declares `data-display-mode` (`fullpage` / `fullscreen`). For
  `Full screen`, the view renders an **enter-fullscreen button** (the board's
  `.kanban-fullscreen-toggle` / calendar's `requestFullscreen` precedent — a
  **user gesture** is required to enter the browser's Fullscreen API, so the
  display degrades to `Full page` where the gesture is unavailable — Safari iOS
  / the API's absence) + an **exit-fullscreen close** (shown only while
  full-screen) + the `:fullscreen` stage rules (the stage owns the whole
  monitor, edge to edge). The `✕` exit link (top-right, always) still exits the
  kiosk itself; the browser's `Esc` also exits full screen.
- **Tests.** The `KioskServiceTests` upsert pin now saves + reads back a
  non-default `DisplayMode` (the round-trip pin); the `KioskControllerTests`
  save-lane pin adds the `DisplayMode` form field + the `Arg.Any` / pinned
  `KioskDisplayMode.FullScreen` delegation; the `KioskControllerTests`
  chrome-hide string-pin is extended to the new chrome-hide selectors +
  `data-display-mode`.
- **Docs sync.** README roadmap + the `WhatsNew` registry (a new `0.64.0`
  version row) + this ADR reflect the display-mode lane (the ADR 0110 sixth
  close-flip member). The `Milestones.cs` / `MilestonesTests.cs` roadmap
  order + shape are unchanged (M35 is still `Done` — the display-mode is a
  refinement of the already-shipped M35 surface, not a new milestone).
