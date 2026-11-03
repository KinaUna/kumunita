---
id: adr-0162
title: "Analytics history (M34) — the M13 `/admin/analytics` admin surface gains a trend view over time so an operator can see usage patterns evolve"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/analytics-subsystem.md]
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR [0162](0162-analytics-history.md) — Analytics history (M34) — the M13 `/admin/analytics` admin surface gains a trend view over time so an operator can see usage patterns evolve

Status: Accepted
Date: 2026-10-11

> **Abstract:** M34 settles M13's **named deferral** (ADR [0114](0114-logging-and-usage-analytics.md) §"deferred
> lanes" item 1 — "Charts … the admin page is a table + a count for M13.
> **Own lane**, own ADR.") on the M13 analytics surface: it adds a
> **derived read** (one `UsageDailyTotal` per UTC day over a pinned window,
> re-derived from the **existing** `UsageEvent` rows) and a **trend view**
> on the existing `/admin/analytics` surface (a per-day table + an inline
> `<svg>` sparkline over a pinned 7/30/90/180/365-day window). **The one
> thing that separates M34 from the M33 precedent it otherwise mirrors —
> M34 is *derived, not captured* (M34·1):** M33 added a **capture lane**
> because the M24 storage metrics are **point-in-time and non-derivable**;
> M13 already stores a `UsageEvent` row **per recognized request** with an
> `At` timestamp and already prunes them past a **365-day** platform
> constant, so M34's over-time half is **purely derived** — a new read that
> `GROUP`s the existing `UsageEvent` rows by UTC day. **M34 adds no new
> doc, no new capture tick, no new retention constant, and no new doc-type
> surface.** It is **additive on the M13 `Usage` surface** (M34·1) — it
> **reuses** the M13 `Kumunita.Core.Usage` context + the
> `IUsageAnalyticsService` seam + the `UsageEvent` doc + the M13
> `UsagePurgeService.RetentionDays = 365` retention + the M13
> `UsageDocTypes` surface, and **adds** two new DTOs (`UsageDailyTotal`,
> `UsageHistoryResult`), one new read seam (`GetHistoryAsync`, the **3rd**
> method), the M13 surface's **Trend** section, and the closed
> `analytics.trend.*` `kw-l` key set × en/de/fr/da. **No** new bounded
> context, **no** re-shape of any M13 method or DTO member, **no** new doc,
> **no** JS chart library, **no** `AccessAudit` rows on the read, **no** EF
> migration. **For** a developer touching `Kumunita.Core.Usage`, the
> `IUsageAnalyticsService` seam, the `/admin/analytics` surface, or the
> `tests/*` analytics families. The "derived, not captured" distinction,
> the DTO member lists, and the named deferrals are in the [handoff
> note](../design/adr-handoffs.md#adr-0162--analytics-history-m34-moved-detail).

## Context

M13 (ADR [0114](0114-logging-and-usage-analytics.md)) closed the **point-in-time** analytics question: the
GlobalAdmin can see, at `/admin/analytics`, how the platform was used over a
pinned 7/30/90-day window (the per-surface ranking, the
authenticated/anonymous split, the distinct-actor count, and a CSV export
of the per-day-per-surface rows). A single window's count cannot answer the
operator's **pattern** question: **is usage growing, flat, or seasonal?
which surface is the *trend*, not just the count?** The operator today
would have to pull the CSV by hand, bucket the `Date` column into days, and
plot it in a spreadsheet to see the shape. ADR [0114](0114-logging-and-usage-analytics.md) §"deferred lanes" item
1 named this exact gap as a deferral ("Charts … **Own lane**, own ADR.").
M34 **is** that future lane, on the M13 analytics surface. It adds the
**over-time** half in **one piece — a derived read + a trend view**,
**reuses** the M13 `Usage` context + the `IUsageAnalyticsService` seam +
the `UsageEvent` doc + the `UsagePurgeService` retention + the M13
`UsageDocTypes` surface, and **mirrors** the M33 (ADR [0156](0156-storage-metrics-history.md)) trend-view
shape (table + sparkline + pinned window + closed `kw-l` set) with the
**derived, not captured** inversion.

## Decision

- **D1 — M34 is a capability on the M13 `Kumunita.Core.Usage` surface, not
  a new context — the history is *derived, not captured* (M34·1).** M34
  **reuses** the M13 `Usage` context + the `IUsageAnalyticsService` seam +
  the `UsageEvent` doc + the `UsagePurgeService` retention + the
  `UsageDocTypes` surface and **extends them additively** (ADR [0004](0004-data-persistence-and-schema-evolution.md) §B.1).
  **No new bounded context**. **No re-shape** of the 2 M13 methods (they
  are the **frozen by-name** seam M34 extends). **No new doc, no new
  doc-type surface, no capture tick / handler / boot-seed, no new retention
  constant** — the M13 capture + retention already exist and are **reused**,
  not re-added (the one facet that separates M34 from M33). **No EF
  migration** (there is no new doc to register).
- **D2 — Two new DTOs (the 2-member M34·1 ceilings).** The
  `UsageDailyTotal` POCO (namespace `Kumunita.Core.Usage`) carries
  **exactly** 2 members: `Date` (DateTimeOffset, **UTC**) + `Total` (int —
  the count of `UsageEvent` rows whose `At` falls on that UTC day). The
  `UsageHistoryResult` record carries **exactly** 2 members: `WindowDays`
  (int) + `Points` (`IReadOnlyList<UsageDailyTotal>` — **ascending** by
  `Date`, **only the days present** in the window). **Not** an
  `IAuditableResource`; **not** a Marten document. See the handoff note for
  the full member lists.
- **D3 — One new read seam: `IUsageAnalyticsService.GetHistoryAsync`
  (M34·1 / M34·7).** `Task<UsageHistoryResult> GetHistoryAsync(int days,
  CancellationToken ct = default)` — the **3rd** method on the existing
  seam (the M25 `GetPlatformSpaceAsync` / M33 `GetHistoryAsync` ADD
  precedent). `days` is a **pinned** value in `{7, 30, 90, 180, 365}`; an
  unknown value throws `ArgumentOutOfRangeException`. The **default**
  rendered window is **30 days**. The impl is on the **existing**
  `UsageAnalyticsService` (one `QuerySession` over the `UsageEvent` rows
  with `At >= now − days`, grouped by `At`'s UTC date → one
  `UsageDailyTotal` per **present** day, ordered **ascending** by `Date`,
  wrapped in `UsageHistoryResult`). **Zero** `AccessAudit` rows (M34·4).
  The 2 M13 methods are **unchanged**.
- **D4 — The M13 `/admin/analytics` surface is extended additively
  (M34·8).** `AdminAnalyticsController.Index` gains a
  `GetHistoryAsync(trendWindow)` read (a second read alongside the M13
  `GetWindowAsync` read — the M13 read is **unchanged**). **Option-B seam
  (the close resolution):** the Trend section reads its **own** window over
  a separate `?trendwindow=` param (pinned {7, 30, 90, 180, 365}),
  **independent of** the M13 `?window=` read (pinned {7, 30, 90}) — the two
  reads no longer share a param. `Views/AdminAnalytics/Index.cshtml` gains
  a **Trend** section (a `<table>` — date · total, ascending — + an inline
  `<svg>` sparkline — **no** JS, **no** chart lib — the M34·9 pin — + the
  `analytics.trend.*` `kw-l` keys + a window selector — five links:
  7/30/90/180/365 over `?trendwindow=`). The M13 surface-rank table + the
  distinct-actor header markup are **unchanged**; the M13 `GlobalAdmin`
  gate is **unchanged**; the M13 CSV export lane and its **one**
  `AccessAudit` row are **untouched**.
- **D5 — The closed `analytics.trend.*` `kw-l` key set is parity-pinned in
  four languages (M34·11).** **10 keys**, each present, **non-empty, in all
  four** languages (en/de/fr/da), pinned by
  `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests`. The
  M13 `admin.analytics_*` key set is the **floor** — **untouched**.

## Consequences

- **The operator can now see the trend** — the M13 surface-rank table +
  distinct-actor header are **unchanged** + a **Trend** section (a per-day
  table + an inline `<svg>` sparkline over a pinned 7/30/90/180/365-day
  window, the 30-day default) renders on `/admin/analytics` — the **shape
  of usage over time** so they can see a growth trend, a weekend dip, or a
  feature taking off.
- **The history is server-rendered, no JS chart library, no new dependency
  (M34·9)** — zero npm change, zero new package, zero build step.
- **The whole delta (all additive on the M13 `Usage` surface, M34·1):** two
  new DTOs (`UsageDailyTotal`, `UsageHistoryResult`), one new read seam
  (`GetHistoryAsync`, the 3rd method), the M13 surface's Trend section, and
  the closed `analytics.trend.*` `kw-l` key set × en/de/fr/da (M34·11).
- **The 2 M13 methods are **untouched** (M34·1)** — the
  `IUsageAnalyticsService` surface is **3 methods** in M34. The M13
  `UsageEvent` doc + `UsageDocTypes` surface + `UsagePurgeService` retention
  are **untouched** — the 365-day M13 constant bounds the M34 window. The
  M13 `admin.analytics_*` `kw-l` key set is **untouched** (M34·11).
- **Zero `AccessAudit` rows on the read (M34·4)** — the
  `GetHistoryAsync` read is a read. The M13 CSV export lane and its **one**
  `AccessAudit` row are **untouched**; M34 does **not** add an export of
  the history (a named deferral).
- **`Kumunita.Core` stays HTTP-free + Wolverine-free (M34·6)** — the
  `GetHistoryAsync` impl is on the existing Wolverine-free read service;
  **no** new tick / handler / boot-seed; **no** `HttpClient` in Core
  (ADR [0006](0006-module-boundary-contracts.md)-D); **no** outbound channel (M34·5).
- **The data never leaves the instance (M34·5)** — the `UsageEvent` rows
  are local Postgres documents; nothing M34 emits, stores, or renders leaves
  the box (the M13 C-M13·1 + SECURITY.md §5 no-third-party-telemetry
  precedent).
- **No new bounded context, no new doc, no new dependency, no new
  `AccessAction`, no new `Decide()` branch, no new
  `IAuthorizationService` surface, no new `AccessAudit` row, no EF
  migration** (M34·1 / M34·4 / M34·8).
- **No roadmap letter moves** — M34 is the **last** milestone on the
  roadmap; the close flips `M34` from `StatusNext` to `StatusDone` and
  promotes **no** successor (M34·12).
- **Named deferrals (a future lane, if it comes):** the per-surface over-
  time chart, the CSV export of the history, the capacity / growth
  projection, the resident-facing history, the multi-week / month
  aggregation, and the six-member close flip until the milestone *ships*
  (U08 owns it — M34·12). See the handoff note for the full catalog.
