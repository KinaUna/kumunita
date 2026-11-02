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

## U3 — volume-stat ADDs

**Outcome: build green — the two `IMediaFileStore` read-only ADDs + the
`LocalVolumeFileStore` impl landed.** **(a) The two ADDs (exact C#, unchanged
from §2.1):** on `IMediaFileStore`, `Task<long> GetTotalSpaceBytesAsync(
CancellationToken ct = default)` + `Task<long> GetFreeSpaceBytesAsync(
CancellationToken ct = default)` — read-only, zero writes, `RootPath`-scoped;
**`IMediaStore` is untouched** (C-SM·1 confirmed — `LocalVolumeFileStore` is
the only `IMediaFileStore` implementer, so no test double needed).
**(b) Impl:** branches on `OperatingSystem.IsWindows()` — Windows uses the BCL
`new DriveInfo(Path.GetPathRoot(RootPath)!).TotalSize` / `.AvailableFreeSpace`;
Linux uses a single `statvfs` P/Invoke (`[DllImport("libc", SetLastError = true)]`
`int statvfs(string, out statvfs_t)`, 10 × 8-byte `ulong` struct) →
`(long)(buf.f_blocks * buf.f_frsize)` / `(long)(buf.f_bavail * buf.f_frsize)`.
**No `QuerySession`, no write, no side-effect beyond the stat** (C-SM·2/4/F7).
**(c) The `IMediaStore` interface is untouched** (C-SM·1 confirmed).
**(d) Compile warnings:** none introduced — the two initial `CS8604`
(nullability) on `Path.GetPathRoot` were silenced with the `!` operator (the
existing `PutAsync` `Path.GetDirectoryName(final)!` style); all 9 remaining
warnings are pre-existing.
**⚠ Drift note (for the drift-guard, not a Drift pause):** the design doc §2.2
wrote the Windows call as `DriveInfo.GetDriveFromPath(RootPath)` — but
`System.IO.DriveInfo` has **no** `GetDriveFromPath` static method in .NET 10
(build CS0117 proved it). U3 used the faithful BCL equivalent (`DriveInfo`
root ctor over the path root) — same `TotalSize`/`AvailableFreeSpace`, same
`RootPath` volume, same read-only semantics. The two **frozen ADD signatures
(C-SM·1/2) are unchanged**; only the non-compiling internal impl line was
corrected.

**Next unit:** `m24-u04.md` (the `IStorageMetricsService` + `StorageMetricsService`
+ the 10 pinned Core tests) — **do not start it**.

## U4 — service + Core tests

**Outcome: build green + the 10 Core tests pass.** The `IStorageMetricsService`
3-method seam + `StorageMetricsService` impl + the 3 DTOs + the DI registration
landed in `Kumunita.Core.Usage`; the U3 two-ADD guardrail held (both
`IMediaFileStore` ADDs present + `LocalVolumeFileStore` implements both, build
green at entry). **(a) The 10 Core test names (by id, all passed):**
`GetSnapshot_TotalUsed_SumsAllMediaObjects`,
`GetSnapshot_UserContentUsed_Equals_TotalUsed`,
`GetSnapshot_VolumeTotals_AreNotInQuerySession`,
`GetPerUserList_PagesAndSortsByBytesDesc`, `PerUser_UnknownBucketGrouped`,
`PerUser_ZeroSizeRowsExcluded`, `GetPerUserUsage_ReturnsOnlySubjectBytes`,
`GetPerUserUsage_ReturnsZeroForUnknownSubject`,
`GetSnapshot_ZeroRows_ReturnsZeroMetrics`,
`GetPerUserList_UnknownBucketIsOneRow` — `Total: 10, Failed: 0` (and the full
Core suite `Total: 1140, Failed: 0`). **(b) `GetSnapshotAsync` read shape:**
**one** `QuerySession` (C-SM·4) over the `MediaObject` catalog — community total
(`Σ SizeBytes`, the "two catalog queries" of §2.3 derived from the one row set:
total + per-user map) — plus **two volume reads**
(`GetTotalSpaceBytesAsync` / `GetFreeSpaceBytesAsync`) **outside** the session
(a `DriveInfo`/`statvfs` BCL stat, not a Postgres query — C-SM·4/F7);
`UserContentUsedBytes == TotalUsedBytes` by design (C-SM·2/4); zero writes,
zero `AccessAudit` rows, zero new docs (C-SM·2/6).
**(c) `GetPerUserUsageBytesAsync` seam shape (the C-SM·7 M25 handoff, F10):**
`Σ SizeBytes WHERE CreatedById == subjectId` — the exact shape M25's U4
(`IStorageSettingsService.GetPerUserUsageBytesAsync`) delegates to or duplicates.
**(d) Sentinel semantics (C-SM·5):** `SizeBytes == 0` rows excluded (the
`Where(o => o.SizeBytes > 0)` clause); `CreatedById == null` / `""` fold into a
**single** "unknown / not captured" bucket row (never one row per null); default
sort descending by bytes used (C-SM·4/F4).
**(e) DI registration line** (in `DependencyInjection.cs`, after
`IUsageAnalyticsService`):
`services.AddTransient<Usage.IStorageMetricsService>(sp => new
Usage.StorageMetricsService(sp.GetRequiredService<Marten.IDocumentStore>(),
sp.GetRequiredService<IMediaFileStore>()));`
**⚠ Drift note (not a Drift pause):** the design doc §2.3 wrote the per-user
aggregation as a server-side `GroupBy`/`Select` LINQ. The impl instead uses the
codebase's established, proven Marten fallback (the documented
`UsageAnalyticsService` pattern: one `ToListAsync()` over the row set, then
client-side group/sum) — the deterministic **results** are identical to §2.3,
and the pins assert the results, not the SQL emitted (D7). The **frozen seam
signatures + DTO shapes (§2.1) are unchanged**; only the non-portable
server-side-grouping query shape was realized via the in-repo fallback.

**Next unit:** `m24-u05.md` (the `AdminStorageMetricsController` + view model +
view + nav row, GlobalAdmin-gated, read-only).

## U5 — admin GET surface

**Outcome: build green; `Kumunita.Web` compiles (the Razor view is precompiled into `Kumunita.Web.dll`, so a green build also validates the `.cshtml`).** The U4 guardrail held at entry (all three `IStorageMetricsService` seams + `StorageMetricsService` + the 3 DTOs + the DI line present; frozen §2.1 seam signatures + DTO shapes used as canonical — not re-derived). **(a) Route + view model:** `AdminStorageMetricsController` (`[Route("admin/storage")]` + `[Authorize(Roles = Roles.GlobalAdmin)]`) — one `GET` action, `Index([FromQuery] int page = 1)` (clamped `Math.Max(1, page)`), loads `GetSnapshotAsync()` + `GetPerUserListAsync(page, 25)` (two independent reads, started then awaited — the `Task.WhenAll` `Task[]`-overload deconstruction trap avoided) and renders `Admin/StorageMetrics`. The view model `AdminStorageMetricsViewModel` is the `StorageMetricsSnapshot` projection (`TotalUsedBytes` / `AvailableBytes`←`FreeVolumeBytes` / `UserContentUsedBytes` / `TotalUniqueFiles` / `TotalDistinctUsers` / `AsOf`) + the `PerUserStoragePage` projection (`Items` / `TotalUsers` / `Page` / `HasMore`) — a plain Web POCO, mirrors the M13 `AnalyticsViewModel`. **(b) Nav row:** added to the `/admin` hub `src/Kumunita.Web/Views/Admin/Index.cshtml` — the "Storage" card (href `/admin/storage`) inserted right after the Security card (mirroring the section-link cards; the hub is itself `[Authorize(Roles = GlobalAdmin)]`, so the card renders unconditionally like the core sections). **(c) The view** (`src/Kumunita.Web/Views/Admin/StorageMetrics.cshtml`): the four metric cards (total used / available / user content / as-of) + the aggregate counts line + the per-user table (paged, sorted desc by bytes from the service; a `null`/empty `CreatedById` row renders the italic **"unknown / not captured"** bucket — C-SM·5) + the `HasMore`/`Prev`/`Next` pager (C-SM·4 — `Prev` when `page>1`, `Next` when `HasMore`, plain interpolated `?page=` hrefs). **(d) Zero `AccessAudit` rows** (C-SM·6): the action is a read only — no form, no write, no POST action, no export lane (M25's U5/U6 own the set-lane), so **zero** rows. **Note:** the view lives at the deliverable path `Views/Admin/StorageMetrics.cshtml` (not a `Views/AdminStorageMetrics/` folder), so the controller renders it explicitly via `View("Admin/StorageMetrics", …)` — the repo's per-controller-folder convention (e.g. `Views/AdminAnalytics/`) is deliberately not used here because the U5 deliverable names this exact path.

**Next unit:** `m24-u06.md` (the 5 pinned Web tests, `AdminStorageMetricsControllerTests`) — **do not start it**.

## U6 — Web tests (5)

**(a) Test file:** `tests/Kumunita.Web.Tests/AdminStorageMetricsControllerTests.cs` — the 5 pinned Web tests from design doc §2.5, mirroring the M13 `AdminAnalyticsControllerTests` harness (direct-construction, NSubstituted `IStorageMetricsService` + `DefaultHttpContext`, the `Controller_Carries_GlobalAdmin_Role_Authorize` attribute idiom for the gate pins). **(b) The 5 names (verbatim):** `AdminStorage_GlobalAdmin_Allowed`, `AdminStorage_NonGlobalAdmin_Forbidden`, `AdminStorage_NoAccessAuditRow`, `AdminStorage_PerUserTable_RendersWithHasMore`, `AdminStorage_FourMetrics_Render`. **(c) Pass/red counts:** 5/5 pass, 0 red (`-class` filtered run: `Total: 5, Errors: 0, Failed: 0`); the full Web suite is green (`Total: 768, Errors: 0, Failed: 0` — delta +5 vs U5's 763). **U5 guardrail held at entry** (controller + `[Authorize(Roles = GlobalAdmin)]` + `View("Admin/StorageMetrics", …)` + view model + `Views/Admin/StorageMetrics.cshtml` + the "Storage" nav card in `Views/Admin/Index.cshtml` all present; `dotnet build Kumunita.slnx -c Debug` green). The U5 quirks are preserved as-is — the explicit `View("Admin/StorageMetrics", …)` path and the two-read held-as-`Task<T>`-locals-then-awaited-sequentially shape are not "fixed" or "refactored" in U6 (only the test harness exercises them). **Next unit:** `m24-u07.md` (the M24 acceptance gate, per §2.6) — **do not start it**.

## U7 — gate recorded (2026-10-03)

**(a) The three gate tests — all PASS** (record-only, no code, no build; the 15 U6+U4 tests re-run green in the in-process xunit.v3 runner after a clean `dotnet build Kumunita.slnx -c Debug`): **closed loop** (via the green U6 `AdminStorage_GlobalAdmin_Allowed` + `AdminStorage_FourMetrics_Render` + `AdminStorage_PerUserTable_RendersWithHasMore`) · **handoff** (via the green U6 `AdminStorage_NonGlobalAdmin_Forbidden`) · **part-vs-whole** (the 15-test list — Core `Total: 10, Failed: 0` + Web `Total: 5, Failed: 0` — passes together). **(b) e2e (M13 three-test shape) — AUTHORED-SPEC, NOT RUN:** the browser e2e needs the Playwright runtime (Postgres-boot + token-channel) the Web suite does not yet carry (the 5 U6 Web tests are `DefaultHttpContext` + NSubstitute direct-construction, not a full `WebApplication` host — the M2 U13 precedent); the next unit who lands the runtime records the pass count. **(c) Drift-guard (§2.7):** no still-open `## U<m> — Drift pause`; the two `⚠ Drift note` items (U3 `DriveInfo` internal impl; U4 Marten client-side aggregation) are both **resolved** (explicitly *not* Drift pauses — the frozen seams/DTOs/invariants/FACES/15-test-names are unchanged). **Gate section appended to `docs/design/m24-storage-metrics-design.md`.** **Next unit:** `m24-u08.md` (close M24) — **do not start it**.


---

## Summary

M24 is **closed** (U8, 2026-10-03). The table below is the shipped-unit log;
the last `## U#` section this note gets is this one.

| Unit | Goal (one line) | Tests | Deviations |
|------|-----------------|-------|-----------|
| U1 | design doc Part 1 — Context / Scope / Invariants C-SM·1–7 / FACES F1–F10 (Step 0 C-SM·7 precondition passed) | — (record) | — |
| U2 | design doc Part 2 — the exact C# seams + the two `IMediaFileStore` ADDs + the 15 pinned test names + the 3-test gate + the drift-guard | — (record) | — |
| U3 | the two `IMediaFileStore` read-only ADDs (`GetTotalSpaceBytesAsync` / `GetFreeSpaceBytesAsync`) + the `LocalVolumeFileStore` impl | (in Core suite) | ⚠ Drift note (not a pause): §2.2's `DriveInfo.GetDriveFromPath` doesn't exist in .NET 10 — used the faithful `DriveInfo` root-ctor equivalent; frozen ADD signatures unchanged |
| U4 | `IStorageMetricsService` + `StorageMetricsService` + the 3 DTOs + DI; Core tests | 10 Core green (full Core 1140) | ⚠ Drift note (not a pause): server-side `GroupBy` realized as the in-repo Marten client-side fallback; frozen seams/DTOs unchanged |
| U5 | `AdminStorageMetricsController` + view model + `Views/Admin/StorageMetrics.cshtml` + the "Storage" nav card; build green | (build green) | — |
| U6 | the 5 pinned Web tests (`AdminStorageMetricsControllerTests`) | 5/5 green (full Web 768) | — |
| U7 | the M24 acceptance gate recorded (2026-10-03) | 15 green (10 Core + 5 Web) | e2e authored-spec, not run (no Playwright runtime yet); no Drift pauses |
| U8 | close M24 — `Milestones.cs` + `MilestonesTests.cs` flip + ADR 0134 + README/STATUS/OPS sync + design-doc close section + this `## Summary` + move to `done/m24/` | build green + `MilestonesTests` 4/4 + the 15 still green (Web 768/768, Core 1140/1140) | — |

**The M25 handoff (C-SM·7) — a closed-loop artifact.** The **`GetPerUserUsageBytesAsync`**
seam (`Task<long> GetPerUserUsageBytesAsync(string subjectId, CancellationToken ct =
default)` — `Σ MediaObject.SizeBytes WHERE CreatedById == subjectId`) is the frozen by-name
artifact **M25's** U4 (`IStorageSettingsService.GetPerUserUsageBytesAsync`) **delegates** to
(or re-points to) rather than re-deriving. A M25 agent finds it by this name in ADR 0134 (D6 /
F10), the design doc's `## M24 — Closed (recorded)` section, and this `## Summary`.

**This is the last handoff entry the M24 note gets.** Closing M24 (M24 `StatusDone`, M25
`StatusNext`) **resolves the M25 U1 Drift pause** — `in-progress/m25-u01.md` may now be
re-run (its Step 0 C-UP·6 precondition — M24 `StatusDone` / M25 `StatusNext` — now holds).
**Do not start M25 here**; it is its own milestone with its own register
(`docs/plans-milestones/in-progress/m25-*.md`).
