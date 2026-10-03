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
