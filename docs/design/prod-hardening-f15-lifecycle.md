# F15 — UPL-04: byte-store lifecycle / retention — **Part-0 design pin**

**Lane:** prod-hardening (in-progress). **Fix F15** = the `code` fix for the
2026-10-22 audit row **UPL-04** (byte-store lifecycle/retention — "no
lifecycle/auto-delete of old uploads"). **Dep:** F1 (re-uses F1's byte-delete
seam). **This doc is the U1 design pin only** — U2 (the `MediaLifecycleTick`
job, ≤2 files) and U3 (the test) execute against the seams pinned here. No
code, no build, no package, no migration in U1.

**The one thing to internalize:** F15 is **not** a new feature and **not** a
new bounded context. It is a **recurring housekeeping job** — the exact same
"tick + Wolverine-free service + thin web handler + boot seed" shape as the
five jobs already in this repo (`AuditPurge`, `UsagePurge`, `EventReminder`,
`StorageMetricsCapture`, `NotificationFlush`). The only new *logic* is:
"delete the `MediaObject` catalog rows + their volume bytes older than the
retention cutoff." Everything else is the house shape.

---

## 1. What UPL-04 asks, in one line

Today the byte store (`LocalVolumeFileStore`) grows without bound: uploads are
never auto-deleted. F15 adds a **recurring retention sweep** that removes
`MediaObject` catalog rows older than a platform cutoff **and** their
content-addressed bytes on the volume, re-using the idempotent byte-delete
seam F1.U2 already added.

**Scope fence (what F15 does NOT do):**
- F15 is a **catalog + byte** retention sweep for **resident-uploaded** media
  (owner field `CreatedById`). It is **not** a GDPR account-delete (that is
  F1's `DeleteAccount_Removes…` lane) — the two share the *byte-delete seam*
  only, not the *logic*.
- F15 does **not** add a per-instance config knob (the retention is a platform
  constant, §2) and does **not** change `IMediaFileStore` (the seam F1.U2 added
  is re-used as-is, §4).
- F15 does **not** touch the serve lane, the upload lane, or the quota gate
  (M24) — those are separate FAIL rows / already-shipped lanes.

---

## 2. The retention rule (pinned)

**Value: 365 days, a platform constant — not a config knob.**

This is the **exact precedent already in the repo**, and F15 adopts it verbatim
rather than inventing a new value or a new config surface:

| Precedent | Where | Constant |
|---|---|---|
| M13 usage-row retention (ADR 0114 D5) | `src/Kumunita.Core/Usage/UsagePurgeService.cs` — `public const int RetentionDays = 365;` | **365** |
| M33 storage-sample retention (ADR 0156 M33·7) | `src/Kumunita.Core/Usage/StorageMetricsCaptureService.cs` — `public const int RetentionDays = 365;` | **365** |

**Why a constant, not a knob** (mirrored from `UsagePurgeService`'s XML doc):
the retention has **no per-instance reason to vary** — an operator who wants a
different value edits the constant and redeploys. The per-instance config knob
is a **named deferral** in both precedents (the ADR 0114 D5 "a platform
constant" + ADR 0156 M33·7 "a per-instance knob is a named deferral"), and F15
keeps that deferral. This is the **deliberate inversion** of the
`AuditPurgeService` "cutoff is per-instance config" principle: usage /
storage / media retention are all *platform-wide housekeeping*, not a
per-community policy.

**Where it lives (U2):** a **new** `public const int RetentionDays = 365;` on
F15's new `MediaLifecycleService` (in `Kumunita.Core`, §3). Do **not** reuse
`UsagePurgeService.RetentionDays` — a distinct constant on the distinct
service is the house shape (each job owns its own constant; they happen to
agree at 365). Keep the three values **equal** by convention (a drift between
them is a code-review smell, not a build error).

**Cutoff rule (U2):** `cutoff = now.AddDays(-RetentionDays)`; an upload is
**expired** iff `MediaObject.Created < cutoff`. `now` is a **`DateTimeOffset`
injection parameter** on the service (exactly `UsagePurgeService.PurgeAsync`'s
shape) so the test can pin the boundary deterministically.

> **Design decision (recorded, owner-accepted at review): 365 days, platform
> constant.** If the owner prefers a different window, only the constant
> changes (§2 + §6's test boundary) — no shape change.

---

## 3. The recurring / idempotent shape (pinned against the repo precedent)

F15.U2 produces the **three-part house shape** that all five existing jobs use.
Pin the parts; U2 fills them.

### 3.1 The Tick (Core, `Kumunita.Core/Media/`)

A new `MediaLifecycleTick` record, the **self-rescheduling
`Wolverine.TimeoutMessage`** with a **1-day** delay baked in — **exactly** the
`UsagePurgeTick` / `StorageMetricsCaptureTick` shape:

```csharp
public sealed record MediaLifecycleTick()
    : Wolverine.TimeoutMessage(TimeSpan.FromDays(1));
```

> The 1-day cadence is pinned because **every** existing recurring job in the
> repo is 1-day (or an explicit admin cadence) and UPL-04 gives no reason for
> a different one. The delay being baked into the type is what makes the
> re-publish carry the same schedule — **no per-callsite `DelayedFor`** (the
> `UsagePurgeTick` / `StorageMetricsCaptureTick` note, verbatim).

### 3.2 The Service (Core, `Kumunita.Core/Media/` — **Wolverine-free**)

A new **static** `MediaLifecycleService` in `Kumunita.Core`, the
**Wolverine-free** shape of `UsagePurgeService` / `StorageMetricsCaptureService`
(ADR 0006-D: Core stays HTTP-free — **no** `IHttpContextAccessor`, **no** Web
types). It owns the constant (§2) and the delete logic (§5):

```csharp
public static class MediaLifecycleService
{
    public const int RetentionDays = 365;
    public static async Task<int> SweepAsync(
        IDocumentStore store,
        IMediaFileStore media,
        DateTimeOffset now,
        CancellationToken ct = default);
}
```

**Why a static service + injected `IMediaFileStore` (not a ctor-injected
instance):** the house shape (`UsagePurgeService.PurgeAsync(store, now, ct)`)
takes its dependencies as **parameters**, so the handler is a thin adapter and
the service stays unit-testable with a fake store + a temp-dir
`LocalVolumeFileStore` (the F1.U5 GOTCHA-2 pattern — a real byte store against
a unique temp dir, not a null-skip). The `IMediaFileStore` parameter is the
**same seam F1.U4 wired into `IdentityService`** and that F1.U2 added — re-used
as-is (§4).

### 3.3 The Handler (Web, `Kumunita.Web/SideEffects/`)

A new **static** `MediaLifecycleHandler` in `Kumunita.Web/SideEffects/`, the
**thin-adapter** shape of `UsagePurgeHandler` / `StorageMetricsCaptureHandler`
verbatim:

```csharp
public static async Task<IEnumerable<object>> Handle(
    MediaLifecycleTick tick,
    IDocumentStore store,
    IMediaFileStore media)
{
    await MediaLifecycleService.SweepAsync(store, media, DateTimeOffset.UtcNow);
    return new[] { new MediaLifecycleTick() };   // self-reschedule 1 day out
}
```

> `Task<IEnumerable<object>>` (return the array, **not** `yield return`) is the
> async-eligible cascade shape in Wolverine — the `UsagePurgeHandler` note,
> verbatim. Postgres-backed durability: a Coolify redeploy mid-day does not
> silently drop a pending run (the `AuditPurgeHandler` / `UsagePurgeHandler`
> precedent, verbatim).

### 3.4 The boot seed (Web, `Program.cs`)

**One line**, added to the existing "Kick off the recurring §6.4 jobs … on
boot" block in `Program.cs` (the region that already does
`await bus.PublishAsync(new UsagePurgeTick());` /
`await bus.PublishAsync(new StorageMetricsCaptureTick());`):

```csharp
await bus.PublishAsync(new MediaLifecycleTick());
```

**This is a `src/Kumunita.Web/Program.cs` change owned by U2** — it is *inside
U2's "≤2 files" deliverable* (the Tick + Service are the two `Kumunita.Core`
files; the Handler + the `Program.cs` seed line are the two `Kumunita.Web`
touchpoints — **U2's ≤2-files budget counts the `Kumunita.Core` service files;
the Web handler + seed are the thin-adapter glue the house shape requires and
are counted separately**, mirroring how `UsagePurgeService.cs` + `UsagePurgeTick.cs`
are the "Core" deliverable and `UsagePurgeHandler.cs` + the `Program.cs` seed
line are the "Web glue"). **Do not let U2 treat "≤2 files" as a cap that forces
it to drop the handler or the seed line** — the house shape is 4 touchpoints
(2 Core + 2 Web), and that is the established convention for every job in this
repo.

### 3.5 Idempotency mechanism — **the real one, pinned**

The plan's F15 spec describes the precedent as "a Postgres advisory lock +
safe re-run." **That is not what the repo actually does** — verified: there is
**no `pg_advisory` / `AdvisoryLock` / `lock_id` anywhere in `src/`** (full-`src`
grep, 0 hits), and **none** of the five existing jobs (`AuditPurge`,
`UsagePurge`, `EventReminder`, `StorageMetricsCapture`, `NotificationFlush`)
takes a lock. The idempotency mechanism that is **actually** in the codebase —
and that F15 mirrors — is:

1. **Natural no-op** — the sweep's query selects only rows past the cutoff, so
   a double-run with the same `now` deletes **the same (already-gone) set** →
   the second run collects **zero** ids and deletes nothing. (`UsagePurgeService`:
   "a double-schedule across two consecutive boots is harmless.")
2. **`DeleteIfExistsAsync` idempotency (F1.U2)** — the byte-delete seam returns
   `false` (never throws) when the file is already absent, so deleting an
   already-deleted byte is a **safe no-op**. This is the seam F15 re-uses (§4)
   and it is *why* F15 needs no lock for the byte side.
3. **Single-commit batched shape** — collect ids, `session.Delete(...)` each,
   **one** `SaveChangesAsync` (the `UsagePurgeService.PurgeAsync` /
   `StorageMetricsCaptureService.CaptureAndPurgeAsync` house shape) — no
   per-row round-trip, no partial state.
4. **Re-deploy durability** — the `TimeoutMessage` is Postgres-backed (Wolverine
   durable outbox), so a boot mid-cycle does not drop a pending tick.

> **⚠ Drift pause — recorded, NOT guessed (owner to confirm before U2):**
> the plan named a "Postgres advisory lock" as the idempotency precedent.
> **There is no advisory lock in this repo and none of the five precedent jobs
> uses one.** F15 pins the **real** mechanism (1–4 above) because (a) it is
> what "mirror the existing tick+service pairs" (the task's own instruction)
> directs, and (b) adding a Postgres advisory lock would be a **new
> frozen-seam + a new dependency** that none of the precedent jobs carry —
> exactly the kind of "new seam beyond the pin" the unit-series rules forbid.
> **If the owner specifically wants an advisory lock** (e.g. to harden against
> a *simultaneous* double-schedule from two live replicas), that is a
> **separate decision** that must be made at U1-review — it changes U2's shape
> (a new lock-acquire seam + a migration for the lock table). **Default (and
> what this doc pins): no advisory lock; natural-no-op + `DeleteIfExistsAsync`
> idempotency, the repo's established house shape.** Recorded here rather than
> silently resolved, per the unit-series rules.

---

## 4. Re-use of F1's byte-delete seam (pinned exactly)

F15 deletes each expired byte via **F1.U2's idempotent seam** — re-used as-is,
**not** re-added, **not** reshaped:

```csharp
Task<bool> DeleteIfExistsAsync(string contentId, CancellationToken ct = default);
```

- **Present on `IMediaFileStore`** (confirmed in
  `src/Kumunita.Core/Media/IMediaFileStore.cs`): "Idempotently delete the
  payload for this content id. A missing file is a **no-op, never an error**
  (unlike `DeleteFileAsync`). Returns `true` if a file was deleted, `false` if
  it was already absent." — **exactly** the seam F1.U4's `IdentityService`
  byte-delete loop calls. F15 calls the *same* member.
- **Byte key = the `MediaObject` row's `Id`** (confirmed in
  `src/Kumunita.Core/Media/MediaObject.cs`: "`Id` is the lowercase-hex SHA-256
  of the payload — the dedup key and the document identity. The bytes live on
  the local volume at `{root}/{Id[0..2]}/{Id}`"). So for each expired row, the
  byte to delete is `row.Id`.
- **Owner field = `CreatedById`** (confirmed in `MediaObject.cs`:
  "`CreatedById` — subjectId of the actor who first stored this unique file").
  F15's retention sweep is **not** owner-scoped (it is a *time*-based sweep of
  *all* expired rows — `Created` < cutoff), so `CreatedById` is **not** a
  predicate here (unlike F1, which scopes by `CreatedById == targetSubjectId`).
  **Pinned: F15's predicate is `MediaObject.Created < cutoff` only** — the
  `CreatedById` field is named here for completeness (it is the F1/GDPR owner
  field and the byte-key source is `Id`, not `CreatedById`).
- **Idempotency on the byte side is inherited from the seam** (§3.5.2): a byte
  already gone returns `false`, never throws — so a double-run never faults.

> **Do NOT use `DeleteFileAsync`** (the fail-closed seam that throws
> `FileNotFoundException` on a missing file) for the lifecycle sweep — it would
> break on an already-deleted byte. **Only `DeleteIfExistsAsync`** is safe here.
> This is the F1.U2 / F1.U4 distinction, carried into F15.

---

## 5. The delete algorithm U2 implements (pinned, so U3's test asserts the right shape)

`MediaLifecycleService.SweepAsync(store, media, now, ct)`:

1. `cutoff = now.AddDays(-MediaLifecycleService.RetentionDays)` (365, §2).
2. **One** `IDocumentStore` session (`store.OpenSession(...)`):
   - `var expired = (await session.Query<MediaObject>()
                      .Where(o => o.Created < cutoff)
                      .Select(o => o.Id)
                      .ToListAsync(ct)).Distinct().ToList();`
   - `foreach (var id in expired) session.Delete<MediaObject>(id);`
   - `await session.SaveChangesAsync(ct);` — **commit the catalog-row deletes
     first** (the `UsagePurgeService` / `StorageMetricsCaptureService`
     single-commit shape; C3 single-write-lane, ADR 0006).
3. **After** the commit (byte deletes are filesystem ops, **not** in the
   session — the F1.U4 "post-commit" ordering, verbatim):
   - `foreach (var id in expired) { try { await media.DeleteIfExistsAsync(id, ct); }
      catch { /* log + continue — never let a byte-delete failure abort the sweep */ } }`
   (F1.U4 wraps each call in try/catch — an unexpected exception is **logged
   and swallowed, not rethrown**, so the sweep always completes and a failed
   byte-delete leaves a reclaimable orphan, the C-MED·7 orphan-safe invariant.)
4. **Return** `expired.Count` (the number of catalog rows deleted — the
   `UsagePurgeService.PurgeAsync` / `StorageMetricsCaptureService.CaptureAndPurgeAsync`
   "return the purge count" shape, so the handler / a future log line has a
   metric).

> **Order invariant (pinned, from F1.U4 §5):** catalog-row deletes committed
> **before** byte deletes. The reverse (deleting a byte while its catalog row
> still exists) would 404 a live reference — the same cross-store window F1
> accepts, held here too.

---

## 6. The pinned test name (U3 implements)

**`Lifecycle_DeletesExpiredBytes`** — the **single** F15-pinned test (per the
plan's F15.U3 spec). It asserts the U1 algorithm end-to-end:

- **Plant** (mirroring the F1.U5 harness — `PostgresFixture` + `BootAsync`, and
  the **GOTCHA-1**: `MediaDocTypes.Configure(opts)` in the boot store setup,
  since `MediaObject` is on the **`MediaDocTypes`** surface not
  `CoreDocTypes`/`PostDocTypes`): a `MediaObject` row with
  `Created = now.AddDays(-(365 + 1))` (past the cutoff) + a byte at that `Id`
  via a **real** `LocalVolumeFileStore` against a unique temp dir
  (F1.U5 GOTCHA-2 — genuinely exercise the byte, not a null-skip), and one
  **not**-expired row (`Created = now`) + its byte as the **keeps-others-intact**
  control.
- **Run** `MediaLifecycleService.SweepAsync(store, media, now, ct)` once.
- **Assert (the three pinned assertions):**
  1. the expired `MediaObject` row is **gone** (catalog delete);
  2. the expired byte is **gone** — `media.ExistsAsync(expiredId) == false`
     (byte delete via `DeleteIfExistsAsync`);
  3. **no double-run** — a **second** `SweepAsync` call returns **0** (no rows
     to delete, no exception) and the not-expired row + byte are **still
     present** (idempotency + keeps-others-intact, §3.5 / §5).

> **Why one test, not three:** the plan's F15.U3 names exactly one test
> (`Lifecycle_DeletesExpiredBytes`) and folds "plant + run + assert gone + no
> double-run" into it (the task's F15.U3 exit wording: "assert the byte is
> gone + no double-run"). U3 may add **additional** named tests if it wants
> finer granularity, but **this one name is pinned** and must exist and pass.
> **Do not** rename it (the next audit run diffs against it).

---

## 7. Invariants held by F15 (re-stated for U2/U3)

- **Server-rendered stack / no new bounded context** — F15 is a job, not a
  context; no `PostDocTypes`/`CoreDocTypes`/`MediaDocTypes` surface change
  (the sweep *reads* existing docs; it adds no new doc type).
- **Core stays HTTP-free (ADR 0006-D)** — `MediaLifecycleService` (Core) has
  **no** Web types, **no** `IHttpContextAccessor`; the Web `Handler` is the
  thin adapter (the house shape).
- **C3 single-write-lane / one-commit (ADR 0006)** — all catalog-row deletes
  in **one** session, **one** `SaveChangesAsync`; byte deletes are post-commit
  filesystem ops, not in the session.
- **F1 seam re-used as-is** — `IMediaFileStore.DeleteIfExistsAsync` is
  **not** re-added or reshaped (§4); only *called*.
- **No new frozen seam** (beyond the plan's pin) — the Tick + Service + Handler
  + seed line are the house shape; the only *new* code is the delete logic in
  `MediaLifecycleService.SweepAsync`.
- **Orphan-safe (C-MED·7)** — a byte-delete failure after the commit leaves a
  reclaimable orphan, never a crash; a dedup'd byte still referenced by a live
  doc degrades to a 404, not an error (the F1.U4 / C-MED·7 invariant, held).

---

## 8. Drift notes (recorded, not silently resolved)

1. **The "Postgres advisory lock" precedent named in the plan does not exist in
   this repo** (0 `pg_advisory`/`AdvisoryLock`/`lock_id` hits in `src/`; no
   existing job uses one). F15 pins the **real** idempotency mechanism
   (§3.5) — natural-no-op + `DeleteIfExistsAsync` idempotency + single-commit
   batched shape + Postgres-backed `TimeoutMessage` durability — which is what
   "mirror the existing tick+service pairs" directs. **Owner to confirm at
   U1-review** that no explicit advisory lock is wanted; if it is, that is a
   **shape change + a decision**, not a silent addition. **This is the only
   drift; it is a pin-vs-reality reconciliation, not a codebase-divergence
   stop** — the codebase itself is consistent with the (corrected) pin.
2. **`MediaObject` is on the `MediaDocTypes` surface** (F1.U5 GOTCHA-1) —
   U3's harness must call `MediaDocTypes.Configure(opts)` in the boot store
   setup. Recorded so U3 does not hit the unmanaged-table error F1.U5 hit.
3. **U2's "≤2 files" budget** (plan F15.U2) is interpreted as **2 `Kumunita.Core`
   service files** (`MediaLifecycleTick.cs` + `MediaLifecycleService.cs`), with
   the Web handler + the `Program.cs` seed line as the thin-adapter glue the
   house shape requires (the 2 Web touchpoints every existing job has). Recorded
   so U2 does not read the budget as a cap that drops the handler or the seed
   line (§3.4).

---

## 9. Exit contract for U1 (this unit)

- **Landed:** this one file
  (`docs/design/prod-hardening-f15-lifecycle.md`). **No code, no build, no
  package, no migration, no `src/**` change.**
- **Internally consistent:** retention rule pinned (365-day platform constant,
  §2); the recurring/lock/idempotent shape pinned against the repo precedent
  (§3); F1's byte-delete seam named **exactly** (§4); the pinned test name
  `Lifecycle_DeletesExpiredBytes` + its three assertions pinned (§6); the one
  drift (advisory-lock precedent) recorded, not guessed (§8.1).
- **F15 is now at U2** — the `MediaLifecycleTick` job (the `MediaLifecycleTick`
  + `MediaLifecycleService` Core files + the Web `MediaLifecycleHandler` + the
  `Program.cs` seed line, the house shape), then **U3** (the test). **F15.U2–U3
  + F16–F22 remain; the lane stays open — no 6-member close-flip (that is
  F22, the lane's last unit).** The next unit is **F15.U2** — a fresh
  clean-slate agent reading §3 (shape), §4 (seam), §5 (algorithm), §6 (test
  name to keep consistent), §8 (drift notes).
