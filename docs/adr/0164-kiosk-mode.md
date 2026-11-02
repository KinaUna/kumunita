---
id: adr-0164
title: "Kiosk mode (M35) — a full-page / full-screen display that automatically cycles a GlobalAdmin-set playlist of the community's own surfaces (boards, calendar, to-dos), changing view on a set interval and refreshing each view's content on a separate, faster interval"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/m35-kiosk-mode-design.md]
supersedes: []
superseded_by: [adr-0285]
updated: 2026-11-02
---

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
