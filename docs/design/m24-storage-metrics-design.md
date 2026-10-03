# M24 — Storage metrics — design

> **Milestone M24.** A **read-only admin feedback lane** over the *existing*
> media byte store + catalog (ADR 0011). M24 surfaces **storage metrics** —
> the admin's view of the byte store: **total used space**, **available
> space**, **user-content used space**, and **space used per user** — closing
> the operator's storage feedback loop locally. It is the *metrics* half;
> M25 (Upload limits) is the *enforcement* half (admin-set per-file limit +
> per-user quota + the resident self-usage view) and builds on this lane's
> `GetPerUserUsageBytesAsync` seam.
>
> **The one thing every unit must respect:** M24 is **read-only,
> zero-writes, HTTP-free, and `GlobalAdmin`-gated** (C-SM·1 / C-SM·2 /
> C-SM·3 / C-SM·6) — it opens a `QuerySession` (a read) and calls two
> volume-stat methods (reads); it emits **zero** writes, **zero**
> `AccessAudit` rows, and **zero** new documents; and it **does not reshape**
> `IMediaStore` or `MediaObject` — it only *reads* them (C-SM·1).

## Context

M13 (Logging and analytics) surfaces **usage analytics** — *how the platform
is used*: a per-request `UsageEvent` row aggregated over a pinned window,
rendered on the GlobalAdmin-gated `/admin/analytics` surface (the
ARCHITECTURE.md value-chain row: "**M13** logging and analytics —
**feedback** — the operator sees how the platform is used"). It answers the
*traffic* question.

M24 answers a **different** operator question that the platform today cannot
answer at all: **how full is the byte store, and who is using it?** The media
byte store (ADR 0011) is a **dedicated local volume** (`LocalVolumeFileStore`)
holding the content-addressed payloads, with a **`mt` catalog**
(`MediaObject`, Marten-owned, ADR 0004 §B) recording each unique file's
`SizeBytes` and `CreatedById` (who first stored it). Two restore surfaces,
one story (C-MED·7): the catalog rides the `pg_dump`; the payload rides the
volume snapshot (OPS.md procedures 4/5). The operator can read either surface
in `psql` or `du`, but the platform gives them **no first-class admin view**
that answers:

- **Total used space** — how many bytes of payloads are on the volume.
- **Available space** — how much the volume has left (the capacity ceiling
  an operator must watch before the store fills).
- **User-content used space** — the total bytes of *resident-authored*
  content (the `Σ MediaObject.SizeBytes` over the catalog), as opposed to
  other volume occupancy.
- **Space used per user** — the per-`CreatedById` breakdown (who is storing
  the most).

M24 is a **read-only feedback lane for the operator**, closed locally: it
reads the existing `MediaObject` catalog (Postgres) + the volume
(filesystem stat) and renders the four metrics on one new GlobalAdmin-gated
admin section (`/admin/storage`). It adds **no** new store, catalog doc,
`AccessAction`, bounded context, or write surface — it **reads** `MediaObject`
(content-addressed, `SizeBytes`, `CreatedById`) + the volume
(`LocalVolumeFileStore`) for the four metrics the milestone name names. The
service lives in the **existing `Kumunita.Core.Usage`** context (created for
M13's `UsageAnalyticsService`) — the "a bounded context per concern" house
rule already points usage-shaped reads here; M24 adds a second seam to the
same context rather than opening a new one.

**What M24 does *not* do (the named non-decisions, the M25 boundary):**

- **The admin settings surface + enforcement** — the admin-set per-file size
  limit and the per-user total content quota, and the upload-time enforcement
  of both, are **M25** (Upload limits). M24 *reads* the `MaxBytes` boundary
  (the `MediaOptions` env default, ADR 0011) as context; it does not change
  or enforce it.
- **The resident self-usage view** — the resident's own view of "how much of
  my quota remains" is **M25**. M24 is **admin-only**; it renders **no**
  resident-facing surface and does not touch the M13 "no per-account rendered
  analytics" boundary (C-M13·3) because it is a different audience (the
  operator) and a different question (storage, not traffic).
- **Per-resident quota overrides** — M25's named non-decision, carried
  forward; M24 does not open the surface for it either.
- **Per-file size-limit setting** — M25's named non-decision (the
  admin-settable `MaxBytes`); M24 reads the env default, it does not
  expose a setter.
- **Any `IMediaStore` / `MediaObject` reshape** — C-SM·1: M24 only *reads*
  them; the two `IMediaFileStore` read-only ADDs (C-SM·2) are the *only*
  interface change.
- **Any new bounded context** — M24 lives in the existing
  `Kumunita.Core.Usage` context.
- **Any new `AccessAction` / `AccessVia` / `AccessAudit` row** — C-SM·6: the
  read is not an auditable action (the M13 "read = no row; export = one row"
  discipline; M24 has no export lane in v1, so **zero** rows).
- **Charts / trend lines / time series / per-day breakdowns** — M24 renders a
  point-in-time snapshot + a per-user table; time series are a deferred lane
  (own ADR, the M13 charts precedent).
- **The CSV export lane** — M13's export is an audited admin action
  (one `AccessAudit` row); M24 has no export in v1, so **zero** rows
  (C-SM·6 / F8).
- **The M13 "per-account rendered analytics" boundary (M24 is admin-only).**
  M24 renders a per-user *storage* table for the *operator* (an admin
  surface, GlobalAdmin-gated); it does not render a *resident-facing*
  analytics view, so it does not touch C-M13·3.

**The arrow moved:** the operator's storage feedback loop, closed locally —
they can now see, in the admin console, how full the volume is, how much is
resident content, and who is storing the most, without leaving the box for
`psql` / `du` / a volume snapshot listing.

## Scope

**In (shipped by M24):**

- The **`IStorageMetricsService`** + **`StorageMetricsService`** in
  `Kumunita.Core.Usage` (the existing context that hosts M13's
  `IUsageAnalyticsService`), with three read-only seams:
  - `Task<StorageMetricsSnapshot> GetSnapshotAsync(CancellationToken ct = default)`
    — the four headline metrics (total used, available, user-content used) +
    the per-user table for the default page.
  - `Task<PerUserStoragePage> GetPerUserListAsync(int page, int pageSize, CancellationToken ct = default)`
    — the paged per-user table (F3; the M7 `HasMore` discipline).
  - `Task<long> GetPerUserUsageBytesAsync(string subjectId, CancellationToken ct = default)`
    — the per-resident `Σ MediaObject.SizeBytes WHERE CreatedById == subjectId`
    seam (F10) — **reusable by name** by M25's `StorageSettingsService`
    (the M25 handoff is a **closed-loop** artifact).
- The **two `IMediaFileStore` read-only ADDs** (C-SM·2 / F7) + the
  `LocalVolumeFileStore` impl:
  - `Task<long> GetTotalSpaceBytesAsync(CancellationToken ct = default)`
    — the volume's total capacity (a filesystem stat, *not* a Postgres query).
  - `Task<long> GetFreeSpaceBytesAsync(CancellationToken ct = default)`
    — the volume's free space (a filesystem stat, *not* a Postgres query).
  - The `LocalVolumeFileStore` implements both against the
    `DriveInfo`/`Directory` BCL for the configured `RootPath`; a test
    double in `Kumunita.Core.Tests` implements both against a temp dir.
- The **`/admin/storage` admin view** + **nav row** + **view model** in
  `Kumunita.Web`:
  - `StorageMetricsController` (`src/Kumunita.Web/Controllers/`) — the
    `[Authorize(Roles = Roles.GlobalAdmin)]` attribute shape (the M13
    `AdminAnalyticsController` precedent, C-SM·6 / F2).
  - `GET /admin/storage` → `StorageMetricsViewModel` (the
    `TotalUsedBytes`, `AvailableBytes`, `UserContentUsedBytes`, the
    `PerUserList` + `HasMore` for the default page, the
    `PerUserTotalBytes` aggregate) → `Views/Admin/Storage.cshtml` (the
    four-metric header + the per-user table — the `Admin/Audit.cshtml`
    table shape, the `kw-l` labels × en/de/fr, the `_AdminNav.cshtml`
    "Storage" tab added to the strip).
  - `GET /admin/storage/users?page=N` → the paged per-user table (F3).
- The **pinned Core + Web tests** (U4/U6 per the M13 template — the
  exact names are authored by U2 in Part 2).
- The **ADR + OPS/README/Milestones sync** (the C-SM·7 close unit, U8,
  updates `Milestones.cs` + `README.md` Roadmap + `docs/STATUS.md` +
  `docs/ARCHITECTURE.md` in the same unit; `MilestonesTests.cs` is re-pinned
  to keep the single-in-progress contract passing — M24 → `StatusDone`,
  M25 → `StatusNext`).

**Out (named non-decisions):**

- The admin settings surface (M25).
- Enforcement (M25).
- The resident self-usage view (M25).
- Per-resident quota overrides.
- Per-file size-limit setting (M25).
- Any `IMediaStore` / `MediaObject` reshape.
- Any new bounded context.
- Any new `AccessAction` / `AccessVia` / `AccessAudit` row.
- Charts / trend lines / time series.
- Per-day breakdowns.
- The CSV export lane.
- The M13 "per-account rendered analytics" boundary (M24 is admin-only).

## Invariants (pinned for M24)

The 7 invariants below are the **frozen** ids/names for M24 — once written,
they are the cross-reference for every subsequent unit (U2–U8). The FACES
(F1–F10) in the next section are each **bound** to one or more of these
invariants; the drift-guard (authored by U2) re-pins them in the design doc
Part 2 and in the close unit (U8).

- **C-SM·1 — frozen interfaces.** `IMediaStore` + `MediaObject` are
  **not reshaped** by M24; M24 only *reads* them. The two `IMediaFileStore`
  ADDs (`GetTotalSpaceBytesAsync` / `GetFreeSpaceBytesAsync`) are the **only**
  interface change, and they are **read-only**. Pinned by F9 (the
  `IMediaStore` + `MediaObject` not-reshaped test) + the U2 seam-shape pin
  (the two ADDs are the only delta on `IMediaFileStore`).
- **C-SM·2 — read-only, zero writes.** M24 opens a `QuerySession` (a read) +
  calls two volume-stat methods (reads); it emits **zero** writes, **zero**
  `AccessAudit` rows, **zero** new documents. Pinned by F8 (the zero
  `AccessAudit` row test) + the U2 seam-shape pin (the `IStorageMetricsService`
  methods return DTOs; there is no `SaveChangesAsync` in the impl).
- **C-SM·3 — `Core` stays HTTP-free (ADR 0006-D).** The seam returns plain
  DTOs (`StorageMetricsSnapshot`, `PerUserStorageRow`,
  `PerUserStoragePage`); no `IFormFile`, no `ActionResult`. The
  `/admin/storage` view model (`StorageMetricsViewModel`) is Web-only.
  Pinned by the U2 seam-shape pin (the `IStorageMetricsService` interface
  has no `IActionResult` / `IFormFile` / `HttpContext` parameter or return).
- **C-SM·4 — single `QuerySession`, two catalog queries.**
  `GetSnapshotAsync` runs **one** `QuerySession` + **two** `MediaObject`
  queries (the community total + the per-user map); the two volume-stat reads
  (`GetTotalSpaceBytesAsync` / `GetFreeSpaceBytesAsync`) are **not** in the
  session (a local-volume filesystem stat, not a Postgres query). Pinned by
  F3 (the paging test), F4 (the ordering test), F7 (the not-in-session test),
  and the U2 seam-shape pin (the impl opens exactly one `QuerySession`).
- **C-SM·5 — sentinel semantics.** `CreatedById` of `null` / empty string
  lands in a single **"unknown / not captured"** bucket on the per-user
  table; `SizeBytes == 0` rows are **excluded** from the per-user list.
  Pinned by F5 (the unknown-bucket test) + F6 (the zero-byte-exclusion test).
- **C-SM·6 — `GlobalAdmin`-gated, no audit row.** The `/admin/storage`
  surface is `[Authorize(Roles = GlobalAdmin)]` (the M13
  `AdminAnalyticsController` precedent); it emits **no** `AccessAudit` row on
  a read (the M13 "read = no row; export = one row" discipline; M24 has no
  export lane in v1, so **zero** rows). Pinned by F2 (the 403 test) + F8 (the
  zero-row test).
- **C-SM·7 — single-in-progress milestone contract.** M24 begins with M24
  `StatusNext` (the current state — **U1 enforces this**; confirmed in the
  Step 0 check recorded in `m24-handoff-notes.md`); U8 closes M24
  (`StatusDone`) and promotes M25 (`StatusNext`), updating
  `MilestonesTests.cs`. Closing M24 unblocks M25 U1/U2 (which are currently
  `BLOCKED` on the C-UP·6 precondition). Pinned by the U1 Step 0 check (this
  unit) + the U8 close-unit test (`MilestonesTests` green after the flip).

## FACES (pinned, 10)

The 10 FACES below are the **frozen** observable behaviors for M24 — each is
bound to one or more of the invariants above, and each is the contract a
pinned test (authored by U2 in Part 2) re-asserts. The names are frozen once
written; the drift-guard (U2 + U8) re-pins them.

- **F1** — the `/admin/storage` page renders the **four metrics** (total
  used, available, user-content used, per-user table) — **C-SM·2, C-SM·6**
  (the read-only, GlobalAdmin-gated surface; the M13 `/admin/analytics`
  precedent for the header-row shape).
- **F2** — **non-GlobalAdmins are 403** from `/admin/storage` — **C-SM·6**
  (the `[Authorize(Roles = GlobalAdmin)]` gate; the M13
  `AdminAnalytics_Route_Exists_And_GlobalAdmin_Only` pin shape).
- **F3** — the per-user table is **paged** (the M7 `HasMore` discipline) —
  **C-SM·4** (the `GetPerUserListAsync(page, pageSize)` seam; the
  `PerUserStoragePage { Rows, HasMore }` DTO).
- **F4** — the per-user table defaults to **descending by bytes used** (the
  admin's natural first question) — **C-SM·4** (the deterministic ordering
  the M13 `Aggregation_SurfaceRanking_Descending_Then_Alphabetical` pin
  shape, applied to bytes).
- **F5** — `CreatedById` of `null` / empty string lands in a single
  **"unknown / not captured"** bucket — **C-SM·5** (the sentinel bucket; one
  row, never a crash, never N rows for N nulls).
- **F6** — `SizeBytes == 0` rows are **excluded** from the per-user table —
  **C-SM·5** (the zero-byte exclusion; a catalog row with no payload
  contributes nothing to the per-user table).
- **F7** — the two volume-stat reads (`GetTotalSpaceBytesAsync` /
  `GetFreeSpaceBytesAsync`) are **not** in the `QuerySession` (a local-volume
  filesystem stat, not a Postgres query) — **C-SM·4** (the not-in-session
  pin; the `LocalVolumeFileStore` impl is a BCL `DriveInfo`/`Directory`
  call, not an `IDocumentStore` query).
- **F8** — the `/admin/storage` surface emits **zero** `AccessAudit` rows (no
  export lane in v1) — **C-SM·6** (the M13 "read = no row" discipline; M24
  has no `/admin/storage/export` route, so the zero-row pin holds).
- **F9** — `IMediaStore` + `MediaObject` are **not reshaped** by M24 —
  **C-SM·1** (the not-reshaped pin; a reflection test over the
  `IMediaStore` interface + the `MediaObject` POCO asserting the exact
  member set is unchanged from the ADR 0011 shape).
- **F10** — the `GetPerUserUsageBytesAsync` seam is **reusable by name** by
  M25 (the M25 handoff is a **closed-loop** artifact) — **C-SM·7** (the
  seam-shape pin; the method name + signature are frozen so M25's
  `StorageSettingsService` can call it without re-deriving the per-user
  usage computation — the M25 handoff is a closed-loop artifact, not a
  re-implementation).

---

*Part 1 — Context, Scope, Invariants, FACES. Authored by U1 (this unit),
2026-10-03. Step 0 (C-SM·7 precondition) **passed**: M24 `StatusNext` (the
single in-progress milestone), M25 `StatusPlanned` — confirmed against
`src/Kumunita.Web/Milestones.cs` at U1 entry. Part 2 (Seams & contracts —
the exact C# shapes, the two `IMediaFileStore` ADDs, the pinned test names,
the three-test acceptance gate, the drift-guard) is authored by U2
(`m24-u02.md`).*

---

## Seams & contracts (Part 2, written by U2)

Part 1 froze *what/why* (the 7 invariants C-SM·1–7, the 10 FACES F1–F10,
scope). This section pins *how*: the exact C# seams each later unit
implements (U3–U6), the two `IMediaFileStore` read-only ADDs (C-SM·2) + their
`LocalVolumeFileStore` impl, the single-`QuerySession` shapes (C-SM·4), the
10 pinned Core test names + the 5 pinned Web test names, the three-test
acceptance gate (U7), and the drift-guard (frozen once written). The design
doc is the primary tier, so these exact shapes live here, not only in the
register (`docs/plans-milestones/plan-m24-storage-metrics.md`).

### 2.1 New Core-owned types (exact C#) — `Kumunita.Core.Usage`

The service + DTOs live in the **existing** `Kumunita.Core.Usage` context
(the M13 `IUsageAnalyticsService` home — mirror its `IDocumentStore` ctor +
its `QuerySession` read shape), so C-SM·3 holds: plain DTOs, no `IFormFile`
/ `ActionResult` / `HttpContext` anywhere on the seam.

`IStorageMetricsService` — the **three** read-only seams:

```csharp
namespace Kumunita.Core.Usage;

/// <summary>
/// The M24 storage-metrics read seam (C-SM·2/3/4). Read-only, zero writes
/// (C-SM·2): every method opens at most one <c>QuerySession</c> and calls the
/// two <c>IMediaFileStore</c> volume-stat reads; it emits zero
/// <c>AccessAudit</c> rows and zero new documents. Returns plain DTOs (C-SM·3).
/// </summary>
public interface IStorageMetricsService
{
    /// <summary>
    /// The four headline metrics (total used, available, user-content used) +
    /// the per-user aggregate counts (C-SM·2/4). **One** <c>QuerySession</c> +
    /// **two** <c>MediaObject</c> catalog queries + **two** volume reads
    /// (<c>GetTotalSpaceBytesAsync</c> / <c>GetFreeSpaceBytesAsync</c>), the
    /// latter two **not** in the session (C-SM·4).
    /// </summary>
    Task<StorageMetricsSnapshot> GetSnapshotAsync(CancellationToken ct = default);

    /// <summary>
    /// The **paged** per-user table (the M24 title's "space used per user",
    /// F3/F4). Default sort **descending by bytes used** (C-SM·4); the M7
    /// <c>HasMore</c> discipline (C-SM·4/7). <paramref name="pageSize"/>
    /// defaults to 25.
    /// </summary>
    Task<PerUserStoragePage> GetPerUserListAsync(int page, int pageSize = 25,
        CancellationToken ct = default);

    /// <summary>
    /// The **C-SM·7 handoff seam** M25 reuses (or re-points to):
    /// <c>Σ SizeBytes WHERE CreatedById == subjectId</c>. This is the exact
    /// shape M25's U4 (<c>IStorageSettingsService.GetPerUserUsageBytesAsync</c>)
    /// delegates to or duplicates — the drift-guard (§2.7) names it.
    /// </summary>
    Task<long> GetPerUserUsageBytesAsync(string subjectId,
        CancellationToken ct = default);
}
```

The impl:

```csharp
/// <summary>The <see cref="IStorageMetricsService"/> impl (U4).</summary>
public sealed class StorageMetricsService : IStorageMetricsService
{
    private readonly IDocumentStore _store;
    private readonly IMediaFileStore _volume;

    public StorageMetricsService(IDocumentStore store, IMediaFileStore volume)
    {
        _store  = store  ?? throw new ArgumentNullException(nameof(store));
        _volume = volume ?? throw new ArgumentNullException(nameof(volume));
    }
    // the three seams: §2.3
}
```

The three DTOs (plain records — C-SM·3):

```csharp
/// <summary>
/// The four headline metrics (F1). <see cref="UserContentUsedBytes"/> ==
/// <see cref="TotalUsedBytes"/> **by design** (C-SM·2/4); the "orphan file"
/// caveat is a named non-decision.
/// </summary>
public sealed record StorageMetricsSnapshot(
    long TotalUsedBytes,
    long TotalVolumeBytes,
    long FreeVolumeBytes,
    long UserContentUsedBytes,
    int TotalUniqueFiles,
    int TotalDistinctUsers,
    DateTimeOffset AsOf);

/// <summary>
/// One per-user row (F5/F6). <see cref="CreatedById"/> == <c>null</c> /
/// <c>""</c> is the single **"unknown / not captured"** bucket (C-SM·5).
/// </summary>
public sealed record PerUserStorageRow(string? CreatedById, long Bytes, int FileCount);

/// <summary>
/// The paged per-user table (F3) — the M7 <see cref="HasMore"/> discipline
/// (C-SM·4). <see cref="TotalUsers"/> counts distinct rows **including** the
/// one "unknown" bucket row when present (C-SM·5).
/// </summary>
public sealed record PerUserStoragePage(
    IReadOnlyList<PerUserStorageRow> Items,
    int TotalUsers,
    int Page,
    bool HasMore);
```

`IMediaFileStore` — **two read-only ADDs** (C-SM·2; the ADR 0006-E lane).
These are the **only** interface change (C-SM·1) — **no** other
`IMediaFileStore` member is touched:

```csharp
// IMediaFileStore (the existing interface, unchanged except the two ADDs):
/// <summary>The disk-partition total (a <c>DriveInfo</c>/<c>statvfs</c> read; **not** a <c>QuerySession</c>) — C-SM·2/4.</summary>
Task<long> GetTotalSpaceBytesAsync(CancellationToken ct = default);

/// <summary>The disk-partition free (a <c>DriveInfo</c>/<c>statvfs</c> read; **not** a <c>QuerySession</c>) — C-SM·2/4.</summary>
Task<long> GetFreeSpaceBytesAsync(CancellationToken ct = default);
```

### 2.2 The two `IMediaFileStore` ADDs — the exact impl (U3)

`LocalVolumeFileStore` (the `IMediaFileStore` impl, `Kumunita.Core.Media`)
extends with exactly these two read-only methods. The impl is **read-only +
no I/O side-effect beyond the `statvfs` / `DriveInfo` call** (C-SM·2):

- `GetTotalSpaceBytesAsync` — on **Windows**:
  `DriveInfo.GetDriveFromPath(RootPath).TotalSize`; on **Linux** (containers):
  `statvfs(RootPath).f_blocks * statvfs(RootPath).f_frsize`. The unit that
  lands this (U3) documents the platform fallback + the test that pins it —
  **`GetTotalSpaceBytes_ReturnsPositive`**.
- `GetFreeSpaceBytesAsync` — on **Windows**:
  `DriveInfo.GetDriveFromPath(RootPath).AvailableFreeSpace`; on **Linux**:
  `statvfs(RootPath).f_bavail * statvfs(RootPath).f_frsize`.

Both read the configured `RootPath`'s volume only; neither opens a
`QuerySession` (F7) and neither writes (C-SM·2).

### 2.3 The single `QuerySession` shape (C-SM·4) (U4)

`GetSnapshotAsync` — **one** `QuerySession`, two `MediaObject` catalog
queries, two volume reads **outside** the session (C-SM·4):

```csharp
await using var session = _store.QuerySession();
var totalUsed = await session.Query<MediaObject>().Sum(o => o.SizeBytes);
var totalFiles = await session.Query<MediaObject>().Count();
var perUser = await session.Query<MediaObject>()
    .Where(o => o.SizeBytes > 0 && (o.CreatedById != null && o.CreatedById != ""))
    .GroupBy(o => o.CreatedById)
    .Select(g => (CreatedById: g.Key, Bytes: g.Sum(x => x.SizeBytes), Files: g.Count()))
    .ToListAsync();
var unknown = await session.Query<MediaObject>()
    .Where(o => o.SizeBytes > 0 && (o.CreatedById == null || o.CreatedById == ""))
    .Sum(o => o.SizeBytes);
var totalVolume = await _volume.GetTotalSpaceBytesAsync(ct);
var freeVolume = await _volume.GetFreeSpaceBytesAsync(ct);
return new StorageMetricsSnapshot(totalUsed, totalVolume, freeVolume,
    totalUsed, totalFiles, perUser.Count + (unknown > 0 ? 1 : 0),
    DateTimeOffset.UtcNow);
```

The **M25 handoff** is `GetPerUserUsageBytesAsync` — the exact shape (C-SM·7):

```csharp
await using var session = _store.QuerySession();
return await session.Query<MediaObject>()
    .Where(o => o.CreatedById == subjectId)
    .Sum(o => o.SizeBytes);
```

**This is the seam M25's U4 (`IStorageSettingsService.GetPerUserUsageBytesAsync`)
will delegate to or duplicate** — the drift-guard (§2.7) names it.

`GetPerUserListAsync` — the same `GroupBy` + `Where`, with **paging** +
**sort** (the M7 `HasMore` discipline):

```csharp
var all = await session.Query<MediaObject>()
    .Where(o => o.SizeBytes > 0)
    .GroupBy(o => o.CreatedById)
    .Select(g => new PerUserStorageRow(g.Key, g.Sum(x => x.SizeBytes), g.Count()))
    .ToListAsync();
// The "unknown" bucket (C-SM·5):
var unknownBytes = all.Where(r => r.CreatedById == null || r.CreatedById == "").Sum(r => r.Bytes);
var unknownFiles = all.Where(r => r.CreatedById == null || r.CreatedById == "").Sum(r => r.FileCount);
var distinct = all.Where(r => r.CreatedById != null && r.CreatedById != "").ToList();
if (unknownBytes > 0)
    distinct.Add(new PerUserStorageRow(null, unknownBytes, unknownFiles)); // the "unknown" bucket
distinct = distinct.OrderByDescending(r => r.Bytes).ToList(); // the default sort (C-SM·4)
var pageItems = distinct.Skip((page - 1) * pageSize).Take(pageSize).ToList();
return new PerUserStoragePage(pageItems, distinct.Count, page,
    distinct.Count > page * pageSize);
```

The **"unknown" bucket** (C-SM·5) is **one row** on the per-user table (the
`CreatedById == null` / `""` bucket); the **`SizeBytes == 0` rows** are
**excluded** (the `Where(o => o.SizeBytes > 0)` clause; C-SM·5).

### 2.4 Pinned Core tests (exact names) — `tests/Kumunita.Core.Tests/Usage/StorageMetricsTests.cs`

The 10 pinned Core tests (U4), each bound to its invariant:

1. `GetSnapshot_TotalUsed_SumsAllMediaObjects` (C-SM·2/4)
2. `GetSnapshot_UserContentUsed_Equals_TotalUsed` (C-SM·2/4)
3. `GetSnapshot_VolumeTotals_AreNotInQuerySession` (C-SM·4/7)
4. `GetPerUserList_PagesAndSortsByBytesDesc` (C-SM·4)
5. `PerUser_UnknownBucketGrouped` (C-SM·5)
6. `PerUser_ZeroSizeRowsExcluded` (C-SM·5)
7. `GetPerUserUsage_ReturnsOnlySubjectBytes` (C-SM·7 — the M25 handoff seam)
8. `GetPerUserUsage_ReturnsZeroForUnknownSubject` (C-SM·7)
9. `GetSnapshot_ZeroRows_ReturnsZeroMetrics` (C-SM·2)
10. `GetPerUserList_UnknownBucketIsOneRow` (C-SM·5)

### 2.5 Pinned Web tests (exact names) — `tests/Kumunita.Web.Tests/AdminStorageMetricsControllerTests.cs`

The 5 pinned Web tests (U6), the `/admin/storage` surface bound to its
invariant:

11. `AdminStorage_GlobalAdmin_Allowed` (C-SM·6)
12. `AdminStorage_NonGlobalAdmin_Forbidden` (C-SM·6)
13. `AdminStorage_NoAccessAuditRow` (C-SM·6)
14. `AdminStorage_PerUserTable_RendersWithHasMore` (C-SM·4)
15. `AdminStorage_FourMetrics_Render` (C-SM·2/4)

### 2.6 Acceptance gate (U7 records) — the three-test shape

The gate is three shapes, recorded by U7 (the register's close-adjacent unit);
all must pass together:

- **closed loop:** a GlobalAdmin signs in → `/admin/storage` renders the four
  metrics (total used, available, user-content used, per-user table) → the
  per-user table is paged + sorted by bytes desc.
- **handoff:** the `/admin` hub shows the new "Storage" nav row → a
  non-GlobalAdmin is **403** from `/admin/storage` (the C-SM·6 gate).
- **part-vs-whole:** the 15-test list (10 Core + 5 Web) is the whole;
  closed-loop + handoff are the parts; all must pass together.

### 2.7 Drift-guard (frozen once written)

The following are **frozen** by this unit and re-pinned by the close unit
(U8). Any mismatch — a renamed invariant or FACES id, a reshaped seam
signature or DTO, a dropped or renamed pinned test, or a re-pointed handoff
seam — is a `## U<m> — Drift pause` (unit-series rule §6), not a silent fix:

- the **7 invariants** (C-SM·1–7) and the **10 FACES** (F1–F10);
- the **`IStorageMetricsService` 3-method surface** (the exact C# shapes in
  §2.1);
- the **`StorageMetricsSnapshot` / `PerUserStorageRow` / `PerUserStoragePage`**
  DTO shapes;
- the **two `IMediaFileStore` ADDs** (`GetTotalSpaceBytesAsync` +
  `GetFreeSpaceBytesAsync`);
- the **`GetPerUserUsageBytesAsync` seam** (the M25 handoff — **C-SM·7**);
- the **`SizeBytes == 0` exclusion** + the **`CreatedById == null` / `""`
  "unknown" bucket** (C-SM·5);
- the **10 Core test names** + the **5 Web test names**;
- the **C-SM·7 milestone contract** (M24 `StatusNext` at start; M24
  `StatusDone` + M25 `StatusNext` at close).

The canonical example the drift-guard pins: M25's U4
`IStorageSettingsService.GetPerUserUsageBytesAsync` **should** delegate to
M24's `IStorageMetricsService.GetPerUserUsageBytesAsync` — the drift-guard
names the exact seam M24 ships; a re-implementation that diverges from the
`Σ SizeBytes WHERE CreatedById == subjectId` shape is a Drift pause.

---

*Part 2 — Seams & contracts (the exact C# shapes, the two `IMediaFileStore`
ADDs, the single-`QuerySession` shapes, the 15 pinned test names, the
three-test acceptance gate, the drift-guard). Authored by U2 (this unit),
2026-10-03, per `m24-u02.md` (the sealed-unit instruction; the authority for
this content). Part 1 remains the primary Context/Scope/Invariants/FACES
tier; Part 2 pins the how. Next: U3 (`m24-u03.md`) implements the two
`IMediaFileStore` ADDs + the `LocalVolumeFileStore` fallback.*

### Run result (M24 acceptance gate — 2026-10-03)

**Executed by U7 (record-only; no new test code, no build).** The U6/U4
guardrail held at entry: after a clean `dotnet build Kumunita.slnx -c Debug`
(0 errors), the 15 pinned tests — the 10 Core
(`Kumunita.Core.Tests.Usage.StorageMetricsTests`) + the 5 Web
(`Kumunita.Web.Tests.AdminStorageMetricsControllerTests`) — were re-run in
the in-process xunit.v3 runner (the reliable path per AGENTS.md) and all
green:

| Assembly | Class | Total | Errors | Failed | Skipped | Not Run |
|---|---|---|---|---|---|---|
| `Kumunita.Core.Tests` | `Usage.StorageMetricsTests` | 10 | 0 | 0 | 0 | 0 |
| `Kumunita.Web.Tests` | `AdminStorageMetricsControllerTests` | 5 | 0 | 0 | 0 | 0 |

The **15-test list** (§2.4 + §2.5) is the **whole** of the gate; the two
behavior shapes below are its **parts**, and all must pass together.

- **(a) Closed loop — PASS.** A `GlobalAdmin` signs in → `/admin/storage`
  renders the four metrics (total used, available, user-content used, as-of)
  + the per-user table, paged + sorted by bytes desc. Satisfied verbatim by
  the green U6 pins `AdminStorage_GlobalAdmin_Allowed` (the
  `[Authorize(Roles = GlobalAdmin)]` gate + a 200 `ViewResult`),
  `AdminStorage_FourMetrics_Render` (the four headline metrics + the 2-row
  per-user table), and `AdminStorage_PerUserTable_RendersWithHasMore` (the
  M7 `HasMore`-gated pager over a 30-row/3-user page at size 25).
- **(b) Handoff — PASS.** The `/admin` hub shows the new "Storage" nav row
  (U5, `Views/Admin/Index.cshtml`) → a non-`GlobalAdmin` is **403** from
  `/admin/storage` (the C-SM·6 gate). Satisfied verbatim by the green U6 pin
  `AdminStorage_NonGlobalAdmin_Forbidden` (the role set does not contain
  `Member`).
- **(c) Part-vs-whole — PASS.** The 15-test list (10 Core + 5 Web) passes
  **together** — both classes green in the same in-process run above
  (`10 + 5 = 15`, Errors: 0, Failed: 0). Every part green ⇒ the whole green.

**e2e (M13 three-test shape) — AUTHORED-SPEC, NOT RUN.** The browser-level
e2e (a `GlobalAdmin` signs in through the form → `/admin/storage` renders
the four metrics + per-user table; a non-`GlobalAdmin` is 403; the 15-test
list passes together) requires the Playwright runtime (a `webServer` /
`globalSetup` booting Postgres + the token-channel sign-in helper) that the
Web test suite does not yet carry — the 5 U6 Web tests are
`DefaultHttpContext` + NSubstitute **direct-construction**, not a full
`WebApplication` host (the M2 U13 precedent: the spec is authored but the
bounded runtime deferred). Per this unit's no-code rule the e2e spec is
therefore **recorded, not run** here; the next unit who lands the runtime
(Playwright package + Postgres-boot + token-channel) runs it and records the
pass count. The gate above is nonetheless fully satisfied by the 15 green
C# tests — the e2e re-verifies the same behaviors through the browser.

**Drift-guard (§2.7) — no still-open drift.** There is **no**
`## U<m> — Drift pause` section in the handoff note. The two `⚠ Drift note`
items are explicitly *not* Drift pauses (internal-impl corrections to
non-frozen lines only; the frozen seams/DTOs/invariants/FACES/15-test-names
are unchanged) — each **resolved**:
- **U3** — §2.2 wrote the Windows volume call as
  `DriveInfo.GetDriveFromPath(RootPath)`; `System.IO.DriveInfo` has no such
  static in .NET 10 (build CS0117 proved it). U3 used the faithful BCL
  equivalent (the `DriveInfo` root ctor over the path root) — same
  `TotalSize`/`AvailableFreeSpace`, same `RootPath` volume. The two frozen
  ADD signatures (C-SM·1/2) are unchanged.
- **U4** — §2.3 wrote the per-user aggregation as a server-side
  `GroupBy`/`Select` LINQ; U4 used the codebase's proven Marten fallback
  (one `ToListAsync()` over the row set, then client-side group/sum) —
  identical deterministic results, and the pins assert the results, not the
  SQL emitted. The frozen seam signatures + DTO shapes (§2.1) are unchanged.

**Next:** U8 (`m24-u08.md`) closes M24 — the `Milestones.cs` +
`MilestonesTests.cs` status flip + the ADR + the OPS/README sync + the
design-doc close section + the handoff close + the move to `done/m24/`.
