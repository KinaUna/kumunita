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
  M4 `Events` contexts are **reused, not re-added** (the `M5DocTypes` /
  `M4DocTypes` surfaces are **untouched**). M35 adds **no** new doc **to an
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
