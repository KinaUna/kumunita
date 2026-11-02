---
id: design-storage-metrics-subsystem
title: "Storage metrics subsystem (M24 + history)"
tier: reference
kind: design
status: current
audiences: [developer, agent, platform-maintainer]
related:
  - docs/adr/0134-storage-metrics.md
  - docs/adr/0156-storage-metrics-history.md
  - docs/design/m24-storage-metrics-design.md
  - docs/design/m33-storage-metrics-history-design.md
updated: 2026-11-02
---

> **Abstract:** the **one entry point into the storage-metrics subsystem** —
> the capability chain built on the frozen **M24** read-only admin view of the
> byte store: M24 answers "how full is the volume **right now**" (a
> point-in-time snapshot + a per-user table, `/admin/storage`), and the **M33
> history extension** adds the **trend** (a daily `StorageMetricsSample` + a
> retention tick + a Trend section over a pinned window). **What this is:** the
> map from the base (M24) to the one extension that rides on it, with each
> layer's ADR, design doc, and archived lane. **Who is it for:** the
> platform-maintainer reading the storage admin surface; a developer touching
> `IStorageMetricsService`; an agent that needs the read-only + additive-on-M24
> invariants. **When to read the rest:** before touching the storage-metrics
> service, the two `IMediaFileStore` read ADDs, the `StorageMetricsSample` doc,
> the capture tick, or the `/admin/storage` surface.

# Storage metrics subsystem

The storage-metrics surface is a **capability chain**: M24 is the base (the
read-only, GlobalAdmin-gated, zero-write admin view of the byte store); M33 is
the *extension on top* — the over-time trend (a daily capture + a retention
tick + a Trend section). This hub is the single place that names the chain;
each row links the layer's own design doc (the current truth), its ADR, and its
archived lane.

| Layer | What it adds | Design doc (current truth) | ADR | Archived lane |
|---|---|---|---|---|
| **M24 — base** | the **read-only, zero-write, HTTP-free** admin view of the byte store: total used, available, user-content used, per-user table; the two `IMediaFileStore` read ADDs; the `/admin/storage` surface | [`m24-storage-metrics-design.md`](m24-storage-metrics-design.md) | [ADR 0134](../adr/0134-storage-metrics.md) | [`archive/lanes/m24/`](../archive/lanes/m24/plan-m24-storage-metrics.md) |
| **M33 — history** | the **over-time trend**: a daily `StorageMetricsSample` + a 365-day retention tick + a Trend section (per-day table + sparkline) over a pinned 30/90/180/365-day window; additive on the M24 `Usage` surface | [`m33-storage-metrics-history-design.md`](m33-storage-metrics-history-design.md) | [ADR 0156](../adr/0156-storage-metrics-history.md) | [`archive/lanes/m33/`](../archive/lanes/m33/plan-m33-storage-metrics-history.md) |

**The one fact to keep straight:** both layers are **read-only, zero-write**
(M24's C-SM·6; M33's additive-on-M24 contract) — they **read** `MediaObject` +
the volume, add **no** new store/context/write. M33 is *additive on* M24's
`Usage` surface (one new doc, one new read seam, one new doc-type surface), not
a re-shape of it.
