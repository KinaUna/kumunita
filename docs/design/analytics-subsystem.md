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
