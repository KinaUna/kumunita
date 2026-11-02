---
id: design-analytics-subsystem
title: "Analytics subsystem (M13 + history)"
tier: reference
kind: design
status: current
audiences: [developer, agent, platform-maintainer]
related:
  - docs/adr/0114-logging-and-usage-analytics.md
  - docs/adr/0162-analytics-history.md
  - docs/design/m13-logging-analytics-design.md
  - docs/design/m34-analytics-history-design.md
updated: 2026-11-02
---

> **Abstract:** the **one entry point into the analytics subsystem** — the
> capability chain built on the frozen **M13** operator-feedback-loop surface:
> M13 is the **logging half** (dated files, daily rotation + a pinned
> retention) + the **analytics half** (one minimal `UsageEvent` row, aggregated
> over a pinned 7/30/90-day window, on a GlobalAdmin-gated `/admin/analytics`),
> and the **M34 history extension** adds the **trend** (a derived read over the
> existing `UsageEvent` rows + a Trend section over a pinned window). **What
> this is:** the map from the base (M13) to the one extension that rides on it,
> with each layer's ADR, design doc, and archived lane. **Who is it for:** the
> platform-maintainer reading the analytics admin surface; a developer touching
> the `UsageEvent` doc or `IUsageAnalyticsService`; an agent that needs the
> local + minimal-row invariant. **When to read the rest:** before touching the
> file sink, the `UsageEvent` doc, the `GetHistoryAsync` seam, the
> `UsageDailyTotal` DTO, or the `/admin/analytics` surface.

# Analytics subsystem

The analytics surface is a **capability chain**: M13 is the base (the
operator's feedback loop — logging + the minimal `UsageEvent` analytics row);
M34 is the *extension on top* — the over-time trend (a derived read over the
existing rows + a Trend section). This hub is the single place that names the
chain; each row links the layer's own design doc (the current truth), its ADR,
and its archived lane.

| Layer | What it adds | Design doc (current truth) | ADR | Archived lane |
|---|---|---|---|---|
| **M13 — base** | the **logging half** (dated files, daily rotation + a pinned retention, console sink untouched) + the **analytics half** (one minimal `UsageEvent` row, a pinned 7/30/90-day window, `/admin/analytics` + CSV export); the local + minimal-row invariants | [`m13-logging-analytics-design.md`](m13-logging-analytics-design.md) | [ADR 0114](../adr/0114-logging-and-usage-analytics.md) | [`archive/lanes/m13/`](../archive/lanes/m13/plan-m13-logging-analytics.md) |
| **M34 — history** | the **over-time trend**: a **derived read** (`GetHistoryAsync`, one `UsageDailyTotal` per UTC day over a pinned 7/30/90/180/365-day window, re-derived from the existing `UsageEvent` rows) + a Trend section (per-day table + sparkline); additive on the M13 `Usage` surface | [`m34-analytics-history-design.md`](m34-analytics-history-design.md) | [ADR 0162](../adr/0162-analytics-history.md) | [`archive/lanes/m34/`](../archive/lanes/m34/plan-m34-analytics-history.md) |

**The one fact to keep straight:** both layers honor **the feedback is local
and the row is minimal** (C-M13·1/2) — a `UsageEvent` carries exactly
`Id`/`At`/`ActorId`/`RouteTemplate`, and nothing M13 emits/stores/renders
leaves the instance. M34 is *additive on* M13's `Usage` surface (one new read
seam, two new DTOs, the closed `analytics.trend.*` key set), not a re-shape of
it.


---

## ADR 0114 — moved content (D11 trim, 2026-11-02)

## Amendments

### 2026-10-02 — D4 view path correction

**View location.** The U05 deliverable placed the analytics view at
`Views/Admin/Analytics.cshtml`, but the `AdminAnalyticsController` is
its own controller class (the ADR 0062 section-split precedent, matching
every other `Admin*Controller` in this codebase: `AdminDateFormat`,
`AdminQuiet`, `AdminGuests`, `AdminSignup`, …). ASP.NET Core MVC's view
lookup for `AdminAnalyticsController.Index` searches
`Views/AdminAnalytics/Index.cshtml` then `Views/Shared/Index.cshtml`
(the `/Views/{Controller}/{Action}.cshtml` + shared format) — neither of
which existed at `Views/Admin/Analytics.cshtml`, so every request to
`/admin/analytics` 500'd with `InvalidOperationException: The view
'Index' was not found`.

The view is relocated to `Views/AdminAnalytics/Index.cshtml` (the path
MVC searches, and the convention every other section-split controller
already follows), and the controller action uses plain `View(model)`
as all its sibling controllers do. The `_AdminNav` partial — still
housed in `Views/Admin/` — is referenced from the relocated view via
the site-root-absolute form `~/Views/Admin/_AdminNav.cshtml` (the same
idiom `Guardian/Index.cshtml` already uses for its cross-folder
`~/Views/Locale/_SettingsTabs.cshtml` reference). All other decisions
in this ADR are unchanged.
