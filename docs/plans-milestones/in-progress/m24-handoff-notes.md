# M24 — Storage metrics · rolling handoff notes

> Every unit appends a short `## U#` section before it moves its own plan to
> `done/`. This is the cross-unit memory: read the `## U#` sections before
> yours, append yours after. Do not re-derive what an earlier unit already
> settled (the invariant ids/names, the FACES F1–F10, the seam signatures, the
> pinned test names). This file is created by U1 and moved to `done/m24/` by
> the close unit (U8).

## U1 — design doc Part 1

**Outcome: Step 0 (C-SM·7 precondition) PASSED — design doc Part 1 authored.**
M24 = **`StatusNext`** (the single in-progress milestone), M25 =
**`StatusPlanned`** — confirmed verbatim against `src/Kumunita.Web/Milestones.cs`
at U1 entry. The C-SM·7 contract holds; U1 proceeded to author
`docs/design/m24-storage-metrics-design.md` (Context / Scope / Invariants /
FACES).

**7 invariants (frozen ids/names):** C-SM·1 (frozen interfaces — `IMediaStore`
+ `MediaObject` not reshaped; the two `IMediaFileStore` ADDs are the only
change, read-only) · C-SM·2 (read-only, zero writes — one `QuerySession` + two
volume-stat reads, zero `AccessAudit` rows, zero new docs) · C-SM·3 (`Core`
stays HTTP-free — plain DTOs, no `IFormFile`/`ActionResult`; view model
Web-only) · C-SM·4 (single `QuerySession`, two catalog queries — the two
volume-stat reads are **not** in the session) · C-SM·5 (sentinel semantics —
null/empty `CreatedById` → one "unknown" bucket; `SizeBytes == 0` excluded) ·
C-SM·6 (`GlobalAdmin`-gated, no audit row — no export lane in v1 → zero rows) ·
C-SM·7 (single-in-progress contract — M24 begins `StatusNext`, U8 closes it →
`StatusDone` + promotes M25).

**10 FACES (frozen):** F1 four metrics render on `/admin/storage` (C-SM·2,
C-SM·6) · F2 non-GlobalAdmins 403 (C-SM·6) · F3 per-user table paged, M7
`HasMore` (C-SM·4) · F4 per-user table defaults descending by bytes (C-SM·4) ·
F5 null/empty `CreatedById` → single "unknown" bucket (C-SM·5) · F6
`SizeBytes == 0` excluded (C-SM·5) · F7 two volume-stat reads not in the
`QuerySession` (C-SM·4) · F8 zero `AccessAudit` rows (C-SM·6) · F9 `IMediaStore`
+ `MediaObject` not reshaped (C-SM·1) · F10 `GetPerUserUsageBytesAsync` seam
reusable by name by M25 (C-SM·7).

**Step 0 (C-SM·7) outcome:** M24 `StatusNext` **confirmed** (not
`StatusDone`, not `StatusPlanned`) — the precondition passes and this unit was
not stale. No BLOCKED state.

**Next unit:** `m24-u02.md` (Seams & contracts, design doc Part 2) — **do not
start it**; it appends to `docs/design/m24-storage-metrics-design.md` and
depends on U1's Part 1 existing.

## U2 — design doc Part 2 (seams, contracts, test list, gate, drift-guard)

**Outcome: design doc Part 2 authored.** Appended
`## Seams & contracts (Part 2, written by U2)` to
`docs/design/m24-storage-metrics-design.md` — the exact C# shapes transcribed
from `m24-u02.md` into the house style (the design doc is the primary tier, so
the shapes live here, not only in the register). **No code, no build** in this
unit.

**(a) Sealed seam signatures:** `IStorageMetricsService` = 3 methods —
`GetSnapshotAsync(CancellationToken)` → `StorageMetricsSnapshot`,
`GetPerUserListAsync(int page, int pageSize = 25, CancellationToken)` →
`PerUserStoragePage`, `GetPerUserUsageBytesAsync(string subjectId,
CancellationToken)` → `long`; DTOs `StorageMetricsSnapshot` /
`PerUserStorageRow` / `PerUserStoragePage`; impl
`StorageMetricsService(IDocumentStore, IMediaFileStore)`. The **two
`IMediaFileStore` read-only ADDs** (the only interface delta, C-SM·1/2):
`GetTotalSpaceBytesAsync(CancellationToken)` +
`GetFreeSpaceBytesAsync(CancellationToken)` → `LocalVolumeFileStore` impl (U3,
`DriveInfo` / `statvfs` fallback).

**(b) 15 test names by id:** Core (U4,
`tests/Kumunita.Core.Tests/Usage/StorageMetricsTests.cs`) 1–10:
`GetSnapshot_TotalUsed_SumsAllMediaObjects`,
`GetSnapshot_UserContentUsed_Equals_TotalUsed`,
`GetSnapshot_VolumeTotals_AreNotInQuerySession`,
`GetPerUserList_PagesAndSortsByBytesDesc`, `PerUser_UnknownBucketGrouped`,
`PerUser_ZeroSizeRowsExcluded`, `GetPerUserUsage_ReturnsOnlySubjectBytes`,
`GetPerUserUsage_ReturnsZeroForUnknownSubject`,
`GetSnapshot_ZeroRows_ReturnsZeroMetrics`,
`GetPerUserList_UnknownBucketIsOneRow`. Web (U6,
`tests/Kumunita.Web.Tests/AdminStorageMetricsControllerTests.cs`) 11–15:
`AdminStorage_GlobalAdmin_Allowed`, `AdminStorage_NonGlobalAdmin_Forbidden`,
`AdminStorage_NoAccessAuditRow`, `AdminStorage_PerUserTable_RendersWithHasMore`,
`AdminStorage_FourMetrics_Render`.

**(c) Three-test acceptance gate (by name):** **closed loop** (GlobalAdmin →
`/admin/storage` four metrics → paged per-user table, bytes desc) · **handoff**
(`/admin` hub "Storage" nav row → non-GlobalAdmin 403) · **part-vs-whole** (the
15-test list is the whole; closed-loop + handoff are the parts; all pass
together).

**(d) M25 handoff seam (C-SM·7):**
`IStorageMetricsService.GetPerUserUsageBytesAsync(string subjectId, …)` —
`Σ SizeBytes WHERE CreatedById == subjectId` — the exact shape M25's U4
(`IStorageSettingsService.GetPerUserUsageBytesAsync`) delegates to or
duplicates; the drift-guard (§2.7) names it.

**Drift-guard (frozen):** the 7 invariants (C-SM·1–7), the 10 FACES (F1–F10),
the 3-method seam surface + the 3 DTO shapes, the two ADDs, the
`GetPerUserUsageBytesAsync` seam, the `SizeBytes == 0` exclusion + the
null/empty `CreatedById` "unknown" bucket (C-SM·5), the 15 test names, and the
C-SM·7 milestone contract (M24 `StatusNext` at start → `StatusDone` + M25
`StatusNext` at close). Any mismatch is a `## U<m> — Drift pause` (unit-series
rule §6).

**Next unit:** `m24-u03.md` (the two `IMediaFileStore` read-only ADDs + the
`LocalVolumeFileStore` `DriveInfo` / `statvfs` fallback) — **do not start
it**.
