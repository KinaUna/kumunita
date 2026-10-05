# ADR 0136 — Platform storage limit: an operator env knob (`Media__MaxPlatformBytes`) capping the total resident content the platform may hold; `/admin/storage`'s "available" capped to the remaining budget; all new uploads blocked once the budget is spent or the volume's free space drops below a fixed 100 MiB floor

Status: Accepted
Date: 2026-10-04
Builds on: 0134 (the M24 storage-metrics lane — its
`IStorageMetricsService.GetSnapshotAsync` **C-SM·2** seam already computes the
`Σ MediaObject.SizeBytes` community total + the two `IMediaFileStore`
volume-stat reads this lane reuses, so the "platform is full" decision and the
admin "available" figure are computed from identical inputs);
0135 (the M25 upload-limits lane — its **single Web `IUploadGate`** (C-UP·3) is
the one `413` producer this lane extends, and its `StorageLimits.Decide`
pure-Core decision (size-first, C-UP·2) is the per-resident half this
platform-wide half runs **before**)
Amends: 0011 (its "`Media__MaxBytes` … the extension point" / "`In-app upload
size enforcement` is the Web boundary's job reading `MediaOptions`" — a
**second** `MediaOptions` knob, `MaxPlatformBytes`, is added; like
`MaxBytes` it is operator-tuned env config read at the Web boundary, but it is a
*platform-wide* cap rather than a per-file cap)

The README roadmap's M24/M25 pair surfaces two storage numbers and enforces two
limits, all **admin-set in-app**: how full the box is (M24), and the per-file +
per-user caps a resident is held to (M25). Neither answers the
**operator's** capacity question: *how much total space may this deployment's
resident content occupy, regardless of who uploaded it and what the admin's
per-user settings say?* A self-hosted operator on a small VPS, or one running
the platform on a shared disk, needs a **hard ceiling over the whole
platform** — a number they set **at deploy time** (in the environment, where
the operator already lives — OPS.md), not a number a `GlobalAdmin` sets in the
browser. ADR 0136 adds that ceiling. It is **not** a renumber or a milestone:
it is a **named lane on the shipped M24/M25 storage surface** (the ADR 0013
GP / 0089 / 0093 / 0109 / 0110 precedent) — the `Milestones.cs` / README
Roadmap / `MilestonesTests.cs` triple is **untouched**.

The one thing the whole lane respects, and the two frozen seams it composes
rather than extends:

- **`Core` stays HTTP-free and the block decision stays pure**
  (ADR 0006-D, the ADR 0135 C-UP·3 posture carried up a level): the
  "platform is full" decision is a **pure** read over `(Σ SizeBytes, free
  space, the configured limit) → { used, free, limit, isFull }`; the `ActionResult`
  (the single `413`) is produced **only** in the Web layer — the **existing**
  `IUploadGate` (C-UP·3), which now runs the platform check **first**, before
  the per-resident `StorageLimits.Decide`. `IFormFile` / `Stream` /
  `IActionResult` still never cross the seam.
- **The frozen ADR 0134 `IStorageMetricsService` seam** — the
  `Σ MediaObject.SizeBytes` community-total read and the two
  `IMediaFileStore` volume-stat reads are **reused**, not re-implemented
  (the same drift-guard the ADR 0134 C-SM·7 / ADR 0135 U4 handoff uses): the
  new `GetPlatformSpaceAsync` is a **fourth method on the same interface**,
  computed from the same inputs, so the admin "available" figure and the
  upload-block decision cannot disagree.

## Context

The platform already answers the two *resident*-facing storage questions, and
holds two *admin-set* limits:

- **How full is the box** (M24, ADR 0134): `GET /admin/storage` renders
  **total used**, **available** (the volume's physical free space —
  `IMediaFileStore.GetFreeSpaceBytesAsync`, a `DriveInfo`/`statvfs` stat),
  **user-content used** (the `Σ MediaObject.SizeBytes` catalog total), and a
  paged per-user table. This is the operator's *diagnostic* surface.
- **What a resident may add** (M25, ADR 0135): a `GlobalAdmin` sets a
  **per-file** size limit + a **per-user** total content quota (in-app, the
  `CommunityStorageSettings` doc), and every upload lane runs the **single**
  Web `IUploadGate` (C-UP·3) before `IMediaStore.PutAsync` — the only `413`
  producer, over the pure `StorageLimits.Decide` (size-first).

What is **not** there: a number that caps the **whole platform's** resident
content independent of per-resident settings. The closest thing is
`Media__MaxBytes` (ADR 0011 / 0135), but that is a **per-file** cap
(`0` = no cap per file) — a resident could still add an unbounded *number* of
files. There is **no** platform-wide ceiling, and **no** guard for the
volume running to its physical edge: the operator watches "available" in
`/admin/storage`, but nothing *stops* uploads when the disk is about to fill,
and nothing caps the platform to a disk budget.

The constraints the choice must honor (all pre-existing, not new):

- **`Core` stays HTTP-free** (ADR 0006-D): the block decision is a pure
  Core read; the `413` is Web (the existing `IUploadGate`).
- **One `413` producer** (ADR 0135 C-UP·3): the platform-block does **not**
  introduce a second rejection shape or a second gate — it is a check the
  existing gate runs first.
- **No new store, doc, context, `AccessAction`, or migration** (ADR 0004 §B /
  0006-D): the cap is **env config** (an operator knob, the `Media__*`
  family in OPS.md) read over the **frozen** ADR 0134 read seam.
- **The admin "available" figure stays honest** (ADR 0134): when a limit is
  set, "available" is the **smaller** of the physical free space and the
  remaining budget — the number the operator should act on.

## Decision

**D1 — The limit is an operator env knob: `Media__MaxPlatformBytes` (a second
`MediaOptions` member), `0` = unlimited.**
`MediaOptions` gains `public long MaxPlatformBytes { get; set; } = 0;` (env
`Media__MaxPlatformBytes`), bound the same way `Media__MaxBytes` is
(`builder.Services.Configure<MediaOptions>(GetSection("Media"))` — already in
`Program.cs`; a new member on the existing section is **zero** new
registration). `0` / absent = **unlimited** — the exact default, so a
deployment that omits it is byte-for-byte the M24/M25 baseline. This is
deliberately an **operator** knob (env, OPS.md), **not** an admin-set-in-app
value: it is the same *class* of number as `Media__MaxBytes` and
`DataProtection__KeysDirectory` (set at deploy time, per-instance), whereas
the M25 per-file/per-user limits are **admin** numbers in the browser.

**D2 — A fixed 100 MiB free-space floor (`MediaOptions.MinFreeSpaceFloor`).**
A new `public const long MinFreeSpaceFloor = 100L * 1024 * 1024;` — the
operator safety margin that keeps the volume from filling to its physical
edge. It is a **constant, not a knob** (a `const`, so it cannot be mis-bound
by env): the floor applies **regardless** of whether a platform limit is set
(it is a property of the disk, not the budget), and it is the second
independent trigger of the block. `100` MiB is chosen as a margin small
enough to be invisible on a dedicated volume and large enough to keep the
operating system + the app from thrashing on a shared one; it is a recorded
non-decision in the sense that it is **not** configurable, by design.

**D3 — One pure Core seam: `GetPlatformSpaceAsync` on the frozen
`IStorageMetricsService` (ADR 0134's interface, a fourth method).**
`IStorageMetricsService` gains
`Task<AvailablePlatformSpace> GetPlatformSpaceAsync(long platformLimitBytes,
CancellationToken ct = default)` returning a new plain DTO
`AvailablePlatformSpace(used, freeVolumeBytes, limitBytes, isFull)`. The
`StorageMetricsService` impl computes `isFull` as
`(limit > 0 ∧ used ≥ limit) ∨ (free < MinFreeSpaceFloor)`, reusing the **same**
`Σ MediaObject.SizeBytes` read + the **same**
`GetFreeSpaceBytesAsync` stat the ADR 0134 C-SM·2 `GetSnapshotAsync` already
runs (so the admin "available" figure and the upload-block decision are
computed from identical inputs — no drift). **Optimization:** when
`platformLimitBytes ≤ 0` the `used ≥ limit` trigger is off by construction, so
the `Σ SizeBytes` catalog read is **skipped entirely** — only the cheap
free-space stat runs (an unlimited deployment pays a single `DriveInfo` read
per upload, not a full catalog scan). No `IFormFile`, no `ActionResult`, no
`HttpContext` (C-SM·3, the ADR 0134 posture carried to the new method).

**D4 — The block runs in the existing single Web `IUploadGate` (ADR 0135
C-UP·3), first and authoritative.**
`IUploadGate.CheckUpload` gains a `long platformLimitBytes` parameter (the
caller passes `mediaOpts.Value.MaxPlatformBytes`). The gate now calls
`IStorageMetricsService.GetPlatformSpaceAsync` **first**; if `IsFull` is
`true` it returns the **same** `413` `StatusCodeResult` (the existing
`Reject413()` — no new rejection shape, no new gate) and **never** reaches the
per-resident `StorageLimits.Decide`. This is **authoritative**: a payload that
would otherwise be `StorageDecision.Allowed` (well under the per-file cap,
well under quota, zero prior usage) is still rejected when the platform is
full. The per-file / per-user decision is **unchanged** and runs only when the
platform is not full. All five upload lanes (avatar / content-image /
attachment / document upload / document re-upload) already resolve the gate
and now pass the one extra env value — the guard-before-write ordering
(ADR 0135 C-UP·2) is preserved: **no byte is written on a platform-block**.

**D5 — `/admin/storage`'s "available" is capped to the remaining budget.**
`AdminStorageMetricsController` gains `IOptions<MediaOptions>`; when
`MaxPlatformBytes > 0` the `AvailableBytes` figure is
`min(physical free space, limit − used)`, **clamped at `0`** once
`used ≥ limit` (a negative budget reads as "0 available", not a negative
number), and the view surfaces the limit (`PlatformLimitBytes`) so the
"available" card is labelled as a **platform-budget** figure rather than raw
physical free space. When the limit is `0` / absent the figure is the physical
free space exactly as before (the M24 baseline, unchanged). This is a
**display** change only — the read-only ADR 0134 discipline (zero writes, zero
`AccessAudit` rows, `GlobalAdmin`-gated) is untouched.

**D6 — Pinned, and documented as an operator knob.**
`StorageMetricsTests` gains **four** Core pins over the four trigger
combinations (unlimited + above floor → not full; unlimited + below floor →
full; limit reached → full; within limit + above floor → not full) — the
last three against a real `mt` catalog (Testcontainers, the ADR 0134 U4
shape). `UploadGateTests` gains **two** Web pins (platform-full blocks an
otherwise-`Allowed` upload; platform-not-full allows one).
`AdminStorageMetricsControllerTests` gains **three** Web pins (available
capped to the remaining budget; clamped to `0` when over; uncapped when
unlimited). The env var is documented in **OPS.md** as an **Optional /
No-secret** row alongside `Media__MaxBytes`.

## Consequences

**C-PS·1 — One new `MediaOptions` member + one new constant (D1/D2).**
`MediaOptions.MaxPlatformBytes` (`0` = unlimited) + `MinFreeSpaceFloor`
(`const`). **No** new config section, **no** new `Configure<T>` line (bound on
the existing `Media` section), **no** schema / doc / context / migration,
**no** new `AccessAction` / `AccessVia` / `Decide()` branch / role / adapter.

**C-PS·2 — One new pure Core seam, on the frozen ADR 0134 interface (D3).**
`IStorageMetricsService.GetPlatformSpaceAsync` + the `AvailablePlatformSpace`
record, HTTP-free (C-SM·3). Reuses the ADR 0134 C-SM·2 `Σ SizeBytes` +
free-space reads (no re-implementation, the drift-guard); skips the catalog
read entirely when the limit is unlimited.

**C-PS·3 — One `413` producer, unchanged shape (D4).** The platform-block is a
check the **existing** `IUploadGate` runs first; it returns the **same**
`Reject413()` the per-file / per-user rejections use (C-UP·3). **No** second
gate, **no** new rejection shape, **no** per-lane inline guard — the ADR 0135
"single `413` producer" and "guards-before-write" (C-UP·2) discipline is
**preserved**; a platform-block writes no byte.

**C-PS·4 — The admin "available" figure stays honest (D5).** When a limit is
set, "available" = `min(physical free, limit − used)`, clamped at `0`;
unlimited ⇒ unchanged. The ADR 0134 read-only / zero-audit / `GlobalAdmin`
discipline is untouched.

**C-PS·5 — A named lane on the shipped M24/M25 surface (D1–D6).**
`Milestones.cs` / the README Roadmap / `MilestonesTests.cs` are **untouched**
(the ADR 0013 GP / 0089 / 0093 / 0109 / 0110 precedent). The platform version
registry (ADR 0110, `WhatsNew.cs`) gains a **`0.29.0`** entry for this
capability + the companion `/admin/storage/settings` MiB-input change.

**FACES (F1–F4) + the named non-decisions.**
F1 — when `Media__MaxPlatformBytes > 0`, `/admin/storage`'s "available" is
capped to the remaining budget (C-PS·4). F2 — all new uploads are blocked
(`413`, no byte written) once `used ≥ limit` **or** `free < 100 MiB`
(C-PS·3). F3 — the platform check runs **before** the per-file / per-user
decision (authoritative) (C-PS·3). F4 — when the limit is `0` / absent, both
the "available" figure and the upload gate are exactly the M24/M25 baseline
(C-PS·1).

**The named non-decisions (each a future lane, not part of 0136):** (1)
**An admin-set (in-app) platform limit** — this lane is env/operator-only by
deliberate design (D1); if the cap ever needs to be a `GlobalAdmin` browser
setting, it is a new `CommunityStorageSettings` field + a `/admin/storage/
settings` lane (own ADR). (2) **Enforcement against the physical volume
independently of resident content** — the block measures **resident
content** (`Σ MediaObject.SizeBytes`) against the limit; non-resident volume
consumption is out of scope (the 100 MiB *floor* covers the disk-edge case).
(3) **A per-resident share of the platform budget** — the platform limit is
all-or-nothing at the ceiling; a per-resident proportional allocation is a
future lane. (4) **An admin surface to *change* the limit without a
redeploy** — the limit is read from env at boot; editing it is an operator
redeploy (consistent with D1).

**Done** (2026-10-04): `MediaOptions.MaxPlatformBytes` +
`MinFreeSpaceFloor`; `IStorageMetricsService.GetPlatformSpaceAsync` +
`AvailablePlatformSpace` + the `StorageMetricsService` impl; the
`IUploadGate`/`UploadGate` platform-first check + the five lane call sites;
`AdminStorageMetricsController` + `AdminStorageMetricsViewModel` +
`Views/AdminStorageMetrics/Index.cshtml` capping the "available" figure; the
`Program.cs` gate DI (the metrics seam injected); **4** Core + **5** Web pins
(`StorageMetricsTests`, `UploadGateTests`,
`AdminStorageMetricsControllerTests`) all green; `Media__MaxPlatformBytes`
documented in OPS.md. `WhatsNew.cs` `0.29.0` added (this lane + the
companion `/admin/storage/settings` MiB-input note).
