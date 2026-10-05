# ADR 0134 — Storage metrics (M24) — a read-only, `GlobalAdmin`-gated admin view of the media byte store: total used space, available space, user-content used space, and space used per user

Status: Accepted
Date: 2026-10-03

The README roadmap names M24 exactly: "**Storage metrics** — an admin view of
storage: total used space, available space, user-content used space, and
space used per user." M24 surfaces a **read-only** operator feedback lane over
the *existing* media byte store + catalog (ADR 0011) that answers the four
questions the milestone name names — **how full is the volume**, **how much is
left**, **how much is resident content**, and **who is storing the most** —
without the operator leaving the box for `psql` / `du` / a volume-snapshot
listing. It is the *metrics* half; **M25** (Upload limits) is the *enforcement*
half and builds on this lane's `GetPerUserUsageBytesAsync` seam.

The one thing the whole lane respects: M24 is **read-only, zero-writes,
HTTP-free, and `GlobalAdmin`-gated** (C-SM·1 / C-SM·2 / C-SM·3 / C-SM·6). It
opens a `QuerySession` (a read) and calls two volume-stat reads; it emits
**zero** writes, **zero** `AccessAudit` rows, and **zero** new documents; and
it **does not reshape** `IMediaStore` or `MediaObject` — it only *reads* them
(C-SM·1).

M24 rides **frozen** seams, adding a small, read-only delta:

- **The frozen ADR 0011 `IMediaStore` / `MediaObject` catalog** (Marten-owned,
  ADR 0004 §B) — each unique file's `SizeBytes` + `CreatedById` (who first
  stored it). M24 reads these; it adds **no** field, **no** document, **no**
  `*DocTypes` surface, **no** migration.
- **The existing `Kumunita.Core.Usage` context** (created for M13's
  `IUsageAnalyticsService`) — the "a bounded context per concern" house rule
  already points usage-shaped reads here; M24 adds a second seam to the same
  context rather than opening a new one.
- **The M13 `AdminAnalyticsController` `/admin/analytics` precedent** — the
  `[Authorize(Roles = GlobalAdmin)]` admin surface + the "read = no `AccessAudit`
  row; export = one row" audit discipline (the M13 `UsageEvent` lane) that
  M24's `/admin/storage` surface mirrors.

## Context

M13 (Logging and analytics) surfaces **usage analytics** — *how the platform
is used*: a per-request `UsageEvent` row aggregated over a pinned window,
rendered on the `GlobalAdmin`-gated `/admin/analytics` surface. It answers the
*traffic* question.

M24 answers a **different** operator question the platform today cannot answer
at all: **how full is the byte store, and who is using it?** The media byte
store (ADR 0011) is a **dedicated local volume** (`LocalVolumeFileStore`)
holding the content-addressed payloads, with a **`mt` catalog**
(`MediaObject`) recording each unique file's `SizeBytes` and `CreatedById`.
The operator can read either surface in `psql` or `du`, but the platform gives
them **no first-class admin view** that answers the four questions the M24
title names:

- **Total used space** — how many bytes of payloads are on the volume.
- **Available space** — how much the volume has left (the capacity ceiling an
  operator must watch before the store fills).
- **User-content used space** — the total bytes of *resident-authored* content
  (the `Σ MediaObject.SizeBytes` over the catalog).
- **Space used per user** — the per-`CreatedById` breakdown (who is storing
  the most).

The constraints the choice must honor (all pre-existing, not new):

- **`Core` stays HTTP-free** (ADR 0006-D): the new seam returns plain DTOs;
  the `/admin/storage` view model is Web-only (C-SM·3).
- **Audit-by-default, but a read is not an auditable action** (ADR 0006-C,
  the M13 "read = no row; export = one row" discipline): M24's read emits
  **zero** `AccessAudit` rows; it has no export lane in v1, so zero rows
  (C-SM·6).
- **Marten owns the domain documents** (ADR 0004 §B): M24 reads the existing
  `MediaObject` catalog — **no** new document, **no** new DocTypes surface,
  **no** migration (C-SM·1).
- **Lean + Boring, one database** (README principles): no new context, no new
  store, no new `AccessAction`, no new write surface (C-SM·1/2/6).

## Decision

**D1 — The seam is one `IStorageMetricsService` in the existing
`Kumunita.Core.Usage` context, with three read-only methods.**
`Kumunita.Core.Usage` gains `IStorageMetricsService` + a
`StorageMetricsService` impl (a `IDocumentStore` + `IMediaFileStore` ctor):
`Task<StorageMetricsSnapshot> GetSnapshotAsync(CancellationToken ct = default)`
(the four headline metrics + the per-user aggregate),
`Task<PerUserStoragePage> GetPerUserListAsync(int page, int pageSize = 25,
CancellationToken ct = default)` (the paged per-user table, the M7 `HasMore`
discipline), and
`Task<long> GetPerUserUsageBytesAsync(string subjectId, CancellationToken ct =
default)` (the per-resident `Σ MediaObject.SizeBytes WHERE CreatedById ==
subjectId` seam — the **M25 handoff**, D6). The three return **plain DTOs**
(`StorageMetricsSnapshot`, `PerUserStorageRow`, `PerUserStoragePage`) — no
`IFormFile`, no `ActionResult`, no `HttpContext` (C-SM·3). `GetSnapshotAsync`
runs **one** `QuerySession` + **two** `MediaObject` catalog queries (the
community total + the per-user map); the two volume-stat reads are **not** in
the session (C-SM·4).

**D2 — Two read-only `IMediaFileStore` ADDs (the *only* interface change).**
`IMediaFileStore` gains exactly two members:
`Task<long> GetTotalSpaceBytesAsync(CancellationToken ct = default)` (the
volume's total capacity) and
`Task<long> GetFreeSpaceBytesAsync(CancellationToken ct = default)` (the
volume's free space) — both a **filesystem stat** (`DriveInfo`/`statvfs`),
**not** a Postgres query. `LocalVolumeFileStore` implements both against the
configured `RootPath`. This is the *only* delta on the interface —
`IMediaStore` + `MediaObject` are **not reshaped** (C-SM·1).

**D3 — The surface is a `GlobalAdmin`-gated `/admin/storage` admin view.**
`AdminStorageMetricsController` (`[Route("admin/storage")]`,
`[Authorize(Roles = Roles.GlobalAdmin)]`, the M13
`AdminAnalyticsController` precedent): `GET /admin/storage?page=N` renders
`Views/Admin/StorageMetrics.cshtml` — the four-metric header (total used,
available, user-content used) + the paged per-user table (the
`Admin/Audit.cshtml` table shape, the `kw-l` labels) + a "Storage" card in
`Views/Admin/Index.cshtml`. The view model
(`AdminStorageMetricsViewModel`) is **Web-only** (C-SM·3).

**D4 — Non-`GlobalAdmin`s are `403`; a read emits **zero** `AccessAudit`
rows.** The surface is `[Authorize(Roles = GlobalAdmin)]` (F2); it is a
**read** with **no** export lane in v1, so it emits **zero** `AccessAudit`
rows (F8) — the M13 "read = no row; export = one row" discipline.

**D5 — Sentinel semantics on the per-user table.** A `CreatedById` of `null`
/ empty string lands in a **single "unknown / not captured"** bucket (one row,
never N rows for N nulls, never a crash) (F5, C-SM·5); `SizeBytes == 0` rows
are **excluded** from the per-user table (a catalog row with no payload
contributes nothing) (F6, C-SM·5).

**D6 — The `GetPerUserUsageBytesAsync` seam is the M25 handoff, frozen by
name.** The seam's method name + signature are frozen so **M25's**
`IStorageSettingsService.GetPerUserUsageBytesAsync` (M25's U4) can **delegate
to** it without re-deriving the per-user usage computation — the M25 handoff
is a **closed-loop** artifact (C-SM·7), not a re-implementation.

## Consequences

**C-SM·1 — `IMediaStore` + `MediaObject` are frozen; the two
`IMediaFileStore` ADDs are the only interface change (D2).** M24 adds **no**
field, **no** document, **no** `*DocTypes` surface, **no** migration; it only
*reads* the ADR 0011 catalog + volume.

**C-SM·2 — Read-only, zero writes (D1).** M24 opens a `QuerySession` (a read)
+ calls two volume-stat reads (reads); it emits **zero** writes, **zero**
`AccessAudit` rows, **zero** new documents.

**C-SM·3 — `Core` stays HTTP-free (D1/D3).** The seam returns plain DTOs; the
`/admin/storage` view model is Web-only.

**C-SM·4 — Single `QuerySession`, two catalog queries (D1).**
`GetSnapshotAsync` runs one `QuerySession` + two `MediaObject` queries; the
two volume-stat reads are not in the session (a filesystem stat, not a Postgres
query).

**C-SM·5 — Sentinel semantics (D5).** `null` / empty `CreatedById` → one
"unknown / not captured" bucket; `SizeBytes == 0` excluded from the per-user
table.

**C-SM·6 — `GlobalAdmin`-gated, no audit row (D3/D4).** `[Authorize(Roles =
GlobalAdmin)]`; **zero** `AccessAudit` rows on a read (no export lane in v1).

**C-SM·7 — Single-in-progress milestone contract + the M25 handoff is a
closed-loop artifact (D6).** M24 closes by flipping `Milestones.cs` (M24
`StatusDone`, M25 `StatusNext`, `MilestonesTests.cs` re-pointed), and the
`GetPerUserUsageBytesAsync` seam is the frozen by-name artifact M25's U4
**delegates** to (or re-points to). Closing M24 unblocks **M25 U1/U2** (which
were `BLOCKED` on the C-UP·6 precondition — the single-in-progress contract
M24's close satisfies).

**FACES (F1–F10) + the named non-decisions.** F1 — the four metrics render on
`/admin/storage` (C-SM·2, C-SM·6). F2 — non-`GlobalAdmin`s are `403` (C-SM·6).
F3 — the per-user table is paged (the M7 `HasMore` discipline) (C-SM·4). F4 —
the per-user table defaults to **descending by bytes used** (C-SM·4). F5 — the
`null` / empty `CreatedById` bucket (C-SM·5). F6 — the `SizeBytes == 0`
exclusion (C-SM·5). F7 — the two volume-stat reads are not in the
`QuerySession` (C-SM·4). F8 — the surface emits **zero** `AccessAudit` rows
(C-SM·6). F9 — `IMediaStore` + `MediaObject` are not reshaped (C-SM·1). F10 —
the `GetPerUserUsageBytesAsync` seam is reusable by name by M25 (C-SM·7).

**The named non-decisions (each a future lane, not part of M24):** (1) **The
M25 surface itself** — the admin-set per-file size limit, the per-user total
content quota, the upload-time enforcement, and the resident self-usage view
(M25; M24 *reads* the `MaxBytes` env default as context, it does not change or
enforce it). (2) **The CSV export lane** — M13's export is an audited admin
action (one `AccessAudit` row); M24 has no export in v1, so zero rows; an
export is a future lane (own ADR). (3) **The time-series lane** — charts /
trend lines / per-day breakdowns; M24 renders a point-in-time snapshot + a
per-user table; time series is a future lane (own ADR, the M13 charts
precedent). (4) **The M26 sort-lane hook** — M26 (Sorting) will add
`?sort=` + `?dir=` params to `/admin/storage` (the M7 sibling that pagination
shipped without); M24's default is descending-by-bytes (F4), the hook is the
named extension point, not shipped here.

**Done** (2026-10-03): `IStorageMetricsService` + `StorageMetricsService` + the
three DTOs in `Kumunita.Core.Usage`; the two `IMediaFileStore` read-only ADDs
+ the `LocalVolumeFileStore` impl; `AdminStorageMetricsController` +
`AdminStorageMetricsViewModel` + `Views/Admin/StorageMetrics.cshtml` + the
"Storage" nav card; **10** pinned Core tests (`StorageMetricsTests`) + **5**
pinned Web tests (`AdminStorageMetricsControllerTests`) all green; the M24
acceptance gate recorded (2026-10-03) in the design doc. M24 → `StatusDone`,
M25 → `StatusNext`; **M25 U1/U2 unblocked**.
