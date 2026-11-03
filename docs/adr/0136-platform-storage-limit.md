---
id: adr-0136
title: "Platform storage limit: an operator env knob (`Media__MaxPlatformBytes`) capping the total resident content the platform may hold; `/admin/storage`'s \"available\" capped to the remaining budget; all new uploads blocked once the budget is spent or the volume's free space drops below a fixed 100 MiB floor"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: []
supersedes: [adr-0011]
superseded_by: []
updated: 2026-11-02
---

# ADR [0136](0136-platform-storage-limit.md) — Platform storage limit: an operator env knob (`Media__MaxPlatformBytes`) capping the total resident content the platform may hold; `/admin/storage`'s "available" capped to the remaining budget; all new uploads blocked once the budget is spent or the volume's free space drops below a fixed 100 MiB floor

Status: Accepted
Date: 2026-10-04
Builds on: 0134 (the M24 storage-metrics lane — its `IStorageMetricsService`
**C-SM·2** seam already computes the `Σ MediaObject.SizeBytes` community total
+ the two `IMediaFileStore` volume-stat reads this lane reuses),
0135 (the M25 upload-limits lane — its **single Web `IUploadGate`** (C-UP·3)
is the one `413` producer this lane extends, and its `StorageLimits.Decide`
pure-Core decision is the per-resident half this platform-wide half runs
**before**)
Amends: 0011 (a **second** `MediaOptions` knob, `MaxPlatformBytes`, is added;
like `MaxBytes` it is operator-tuned env config read at the Web boundary, but
it is a *platform-wide* cap rather than a per-file cap)

**The platform storage limit** — a hard ceiling over the whole platform's
resident content, set **at deploy time** (env, OPS.md) by the operator rather
than by a `GlobalAdmin` in the browser. It is a **named lane on the shipped
M24/M25 storage surface** (the ADR [0013](0013-group-posts-membership-lane.md) GP / 0089 / 0093 / 0109 / 0110
precedent), not a renumber or a milestone — the `Milestones.cs` / README
Roadmap / `MilestonesTests.cs` triple is **untouched**. The moved detail (the
`AvailablePlatformSpace` record shape, the unlimited-skip optimization, the
C-PS·1–5 consequence ids, F1–F4, the four named non-decisions, the Done list,
the `WhatsNew.cs` `0.29.0` entry) lives in the [handoff
note](../design/adr-handoffs.md#adr-0136--platform-storage-limit-moved-detail).
**No design doc exists for this ADR** — the handoff note is the moved-detail
home.

The one thing the whole lane respects, and the two frozen seams it composes:

- **`Core` stays HTTP-free and the block decision stays pure** (ADR [0006](0006-module-boundary-contracts.md)-D,
  the ADR [0135](0135-upload-limits.md) C-UP·3 posture carried up a level): the "platform is full"
  decision is a **pure** read over `(Σ SizeBytes, free space, the configured
  limit) → { used, free, limit, isFull }`; the `ActionResult` (the single
  `413`) is produced **only** in the Web layer — the **existing**
  `IUploadGate` (C-UP·3), which now runs the platform check **first**.
- **The frozen ADR [0134](0134-storage-metrics.md) `IStorageMetricsService` seam** — the
  `Σ MediaObject.SizeBytes` community-total read and the two
  `IMediaFileStore` volume-stat reads are **reused**, not re-implemented; the
  new `GetPlatformSpaceAsync` is a **fourth method on the same interface**,
  computed from the same inputs, so the admin "available" figure and the
  upload-block decision cannot disagree.

## Context

The platform already answers the two *resident*-facing storage questions:
**how full the box is** (M24, ADR [0134](0134-storage-metrics.md) — `GET /admin/storage` renders total
used, available, user-content used, a per-user table) and **what a resident
may add** (M25, ADR [0135](0135-upload-limits.md) — a `GlobalAdmin` sets a per-file limit + per-user
quota, and every upload lane runs the **single** Web `IUploadGate` before
`PutAsync`). What is **not** there: a number that caps the **whole
platform's** resident content independent of per-resident settings. The
closest thing is `Media__MaxBytes` (ADR [0011](0011-media-and-file-storage.md) / 0135), but that is a **per-
file** cap — a resident could still add an unbounded *number* of files. There
is **no** platform-wide ceiling, and **no** guard for the volume running to
its physical edge. The constraints (all pre-existing): **`Core` stays
HTTP-free** (the block is a pure Core read; the `413` is the existing
`IUploadGate`); **one `413` producer** (the platform-block is a check the
existing gate runs first, not a second rejection shape); **no new store, doc,
context, `AccessAction`, or migration** (the cap is env config read over the
frozen ADR [0134](0134-storage-metrics.md) read seam); and the **admin "available" figure stays honest**
(when a limit is set, "available" is the smaller of the physical free space
and the remaining budget).

## Decision

**D1 — The limit is an operator env knob: `Media__MaxPlatformBytes` (a second
`MediaOptions` member), `0` = unlimited.** `MediaOptions` gains
`public long MaxPlatformBytes { get; set; } = 0;`, bound the same way
`Media__MaxBytes` is (a new member on the existing section is **zero** new
registration). `0` / absent = **unlimited** — the exact M24/M25 baseline.
Deliberately an **operator** knob (env, OPS.md), **not** an admin-set-in-app
value. *See the handoff note for the full detail.*

**D2 — A fixed 100 MiB free-space floor (`MediaOptions.MinFreeSpaceFloor`).**
A new `public const long MinFreeSpaceFloor = 100L * 1024 * 1024;` — a
**constant, not a knob** (cannot be mis-bound by env). The floor applies
**regardless** of whether a platform limit is set (a property of the disk,
not the budget) and is the second independent trigger of the block.

**D3 — One pure Core seam: `GetPlatformSpaceAsync` on the frozen
`IStorageMetricsService` (a fourth method).** Returns a new plain DTO
`AvailablePlatformSpace(used, freeVolumeBytes, limitBytes, isFull)`. The impl
computes `isFull` as `(limit > 0 ∧ used ≥ limit) ∨ (free < MinFreeSpaceFloor)`,
reusing the **same** `Σ SizeBytes` + `GetFreeSpaceBytesAsync` reads the ADR
0134 C-SM·2 `GetSnapshotAsync` already runs (no drift). **Optimization:** when
the limit is `≤ 0` the catalog read is **skipped entirely**. No `IFormFile`,
no `ActionResult`, no `HttpContext` (C-SM·3). *See the handoff note.*

**D4 — The block runs in the existing single Web `IUploadGate` (ADR [0135](0135-upload-limits.md)
C-UP·3), first and authoritative.** `IUploadGate.CheckUpload` gains a
`long platformLimitBytes` parameter. The gate calls
`GetPlatformSpaceAsync` **first**; if `IsFull` it returns the **same** `413`
(the existing `Reject413()` — no new rejection shape, no new gate) and
**never** reaches the per-resident `StorageLimits.Decide`. **Authoritative**:
a payload that would otherwise be `Allowed` is still rejected when the
platform is full. All five upload lanes pass the one extra env value; the
guard-before-write ordering (C-UP·2) is preserved — **no byte is written on a
platform-block**. *See the handoff note.*

**D5 — `/admin/storage`'s "available" is capped to the remaining budget.**
`AdminStorageMetricsController` gains `IOptions<MediaOptions>`; when
`MaxPlatformBytes > 0` the `AvailableBytes` figure is
`min(physical free space, limit − used)`, **clamped at `0`** once
`used ≥ limit`, and the view surfaces the limit (`PlatformLimitBytes`). When
the limit is `0` / absent the figure is the physical free space exactly as
before. A **display** change only — the ADR [0134](0134-storage-metrics.md) read-only / zero-audit /
`GlobalAdmin` discipline is untouched. *See the handoff note.*

**D6 — Pinned, and documented as an operator knob.** `StorageMetricsTests`
gains **four** Core pins over the four trigger combinations; `UploadGateTests`
gains **two** Web pins; `AdminStorageMetricsControllerTests` gains **three**
Web pins. The env var is documented in **OPS.md** as an **Optional /
No-secret** row alongside `Media__MaxBytes`. *See the handoff note for the
full pin list.*

## Consequences

- **C-PS·1** — One new `MediaOptions` member (`MaxPlatformBytes`, `0` =
  unlimited) + one new constant (`MinFreeSpaceFloor`). **No** new config
  section, **no** new `Configure<T>` line, **no** schema / doc / context /
  migration, **no** new `AccessAction` / `AccessVia` / `Decide()` branch /
  role / adapter.
- **C-PS·2** — One new pure Core seam, on the frozen ADR [0134](0134-storage-metrics.md) interface
  (`GetPlatformSpaceAsync` + `AvailablePlatformSpace`), HTTP-free (C-SM·3);
  reuses the C-SM·2 reads (no re-implementation).
- **C-PS·3** — One `413` producer, unchanged shape: the platform-block is a
  check the **existing** `IUploadGate` runs first, returning the **same**
  `Reject413()`. The ADR [0135](0135-upload-limits.md) "single `413` producer" + "guards-before-write"
  (C-UP·2) discipline is **preserved**; a platform-block writes no byte.
- **C-PS·4** — The admin "available" figure stays honest: when a limit is set,
  "available" = `min(physical free, limit − used)`, clamped at `0`; unlimited
  ⇒ unchanged.
- **C-PS·5** — A named lane on the shipped M24/M25 surface: `Milestones.cs` /
  the README Roadmap / `MilestonesTests.cs` are **untouched**. The
  `WhatsNew.cs` registry (ADR [0110](0110-platform-versioning-and-whats-new.md)) gains a **`0.29.0`** entry.
- **FACES F1–F4 + the four named non-decisions** (an admin-set in-app limit;
  enforcement against the physical volume independently of resident content;
  a per-resident share of the budget; an admin surface to change the limit
  without a redeploy) and the **Done (2026-10-04)** list. *See the handoff
  note.*
