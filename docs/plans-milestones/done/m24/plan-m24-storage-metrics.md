# M24 — Storage metrics — sealed unit register

> **In progress.** This is the **plan** for M24 (Storage metrics), split into
> **sealed units** sized for a **~32K-context fresh agent one at a time**,
> exactly like `done/m3/plan-m3-posts-components.md`,
> `done/m13/plan-m13-logging-analytics.md`, and the in-progress
> `plan-m25-upload-limits.md`. The **primary** reference tier — the exact C#
> seams every unit codes against — is the design doc
> `docs/design/m24-storage-metrics-design.md` (authored in U1/U2, **not yet
> implemented**). The **secondary** tier is this file (unit registry +
> deliverables + exit criteria). The **scratch** tier is
> `docs/plans-milestones/in-progress/m24-handoff-notes.md` (one appended
> `## U#` section per unit, never rewritten; created by U1, moved to
> `done/m24/` by the close unit U8).
>
> **What this is:** the README/Milestones M24 promise — **"an admin view of
> storage: total used space, available space, user-content used space, and
> space used per user."** It is a **read-only admin feedback lane** over the
> *existing* media byte store + catalog (ADR 0011): it does **not** add a new
> store, a new catalog doc, an `AccessAction`, a bounded context, or any
> write surface. It **reuses** `MediaObject` (content-addressed, `SizeBytes`,
> `CreatedById`) for the metrics and `LocalVolumeFileStore` (the volume) for
> the available/total-space read — and it lives in the existing
> **`Kumunita.Core.Usage`** context (created for M13's
> `UsageAnalyticsService`; the natural home of M24's "storage metrics" and the
> per-user usage read M25 will enforce against — **no new bounded context**,
> ADR 0006-D).
>
> **What changes the world:** (1) a **pure read seam**
> (`IStorageMetricsService`) that answers the four questions the M24 title
> names — total used, available, user-content used, per-user used — in **one
> call** (`GetSnapshotAsync`) plus a **paged per-user list**
> (`GetPerUserListAsync`) for the admin's table; (2) a **GlobalAdmin-gated**
> `/admin/storage` **view** (read-only; no form, no write, no settings —
> M25's territory) with one nav row on the `/admin` hub; (3) **no
> authorization surface** (C-SM·6) and **no byte write** (C-SM·2) — M24 is a
> *metric*, not a *gate*.
>
> **The one thing every unit must respect:** `Core` stays **HTTP-free**
> (ADR 0006-D, C-SM·3) — the seam is a **pure read** on
> `(IDocumentStore, IMediaFileStore)`; the `ActionResult` mapping is
> **Web-only**. And the **single-in-progress milestone** contract (C-SM·7):
> M24 is currently `StatusNext` (the single in-progress milestone, pinned by
> `MilestonesTests.cs`); M24's **close** (U8) flips M24 → `StatusDone`,
> **promotes M25 → `StatusNext`**, and updates the two `MilestonesTests.cs`
> tests to match. Closing M24 **unblocks M25 U1/U2**, which are currently
> `BLOCKED` (see `in-progress/m25-handoff-notes.md` — read verbatim at close
> time).

## Understanding

The platform already has **three** relevant surfaces, and M24 sits *on top* of
all of them — it **reads** them, never reshapes them:

- **The byte store + catalog** (ADR 0011): `IMediaStore.PutAsync` (idempotent,
  content-addressed by SHA-256) + the `MediaObject` doc
  (`SizeBytes`, `CreatedById` = the subject who **first** stored that unique
  payload). Every upload lane (avatar / content-image / attachment / document)
  writes through the *same* `IMediaStore` + volume. **M24 reads
  `MediaObject.SizeBytes` / `MediaObject.CreatedById` — the per-user usage
  metric is exactly `Σ MediaObject.SizeBytes WHERE CreatedById == subjectId`,
  and the community total is `Σ MediaObject.SizeBytes`.**
- **The volume** (`LocalVolumeFileStore`, the `IMediaFileStore` impl): a local
  directory at `MediaOptions.RootPath` holding the payloads at
  `{RootPath}/{Id[0..2]}/{Id}`. It already exposes `ExistsAsync` /
  `OpenReadAsync` / `DeleteFileAsync` / `PutAsync`. **M24 adds** *two*
  **read-only** volume metrics to `IMediaFileStore` —
  `Task<long> GetTotalSpaceBytesAsync(CancellationToken)` (the
  disk-partition total) and `Task<long> GetFreeSpaceBytesAsync(CancellationToken)`
  (the disk-partition free) — using `System.IO.DriveInfo` (or a POSIX-safe
  `statvfs` fallback for Linux containers; the unit that lands this
  documents the fallback in the design doc §2.1). **This is the only
  interface change M24 makes**; it is a **read-only ADD** on the store seam,
  mirroring ADR 0006-E's "additive read seam on a frozen interface" lane
  (the same lane M2's `GetProfilesAsync` and M3's `GetComponentsAsync`
  used). No other member is touched.
- **The usage context** (M13, ADR 0114): `IUsageAnalyticsService` +
  `UsageAnalyticsService` + the `UsageDocTypes` doc surface + the
  `AdminAnalyticsController` admin surface. **M24 reuses this home** (the
  `Kumunita.Core.Usage` context) — it adds *two* types to the same
  namespace: `IStorageMetricsService` (the seam) + `StorageMetricsService`
  (the impl). It adds **no new document** (the `MediaObject` doc is
  M24's read source, not its own doc — M24 is *over* the catalog, not
  *beside* it). It registers **no new doc surface** (M24 has no doc to
  register — the `MediaObject` is already registered by M13-era media
  doc-types on the `MediaDocTypes` surface; the `CommunityStorageSettings`
  doc that M25 needs is **M25's** registration, not M24's).

**M24 is a *metric*, not a *gate*.** It answers: *how much space is used, how
much is available, how much is user-content, and who uses how much?* It does
**not** answer: *is this upload allowed?* That is **M25** (Upload limits) —
M25 **reuses** M24's per-user read shape as its enforcement input, and M25
adds the admin **settings** (the `CommunityStorageSettings` doc +
`IStorageSettingsService`) + the **gate** (`IUploadGate`) + the resident
**self** usage view. M24's drift-guard pins the exact seam shapes M25
depends on, so the two milestones' seams are not two competing versions of
"per-user usage bytes."

**What is *not* M24 (named non-decisions, pinned in the design doc):**
- **The admin *settings* surface** (the per-file size limit + per-user quota)
  — that is **M25** (Upload limits). M24 is read-only; M25 adds the write.
- **Enforcement** (the size/quota gate) — that is **M25**'s `IUploadGate`.
- **The resident self-usage view** (a resident sees their own usage / quota /
  remaining) — that is **M25** (U7). M24's per-user metric is **admin-only**;
  no resident-facing surface in M24.
- **Per-*resident* individual quota overrides** — a future lane (M25's
  non-decision).
- **Per-file size-limit setting** — M25's `CommunityStorageSettings`
  surface (the env `Media__MaxBytes` is the current default; M25 makes it
  admin-settable in-app).
- **Any `IMediaStore` / `MediaObject` reshape** (C-SM·1/2/4) — M24 reads
  them, never reshapes them.
- **Any new bounded context** (C-SM·3) — M24 lives in the existing
  `Kumunita.Core.Usage` context (M13's home).
- **Any new `AccessAction` / `AccessVia` / `AccessAudit` row** (C-SM·6) —
  M24's surface is `GlobalAdmin`-gated (the M13 `AdminAnalyticsController`
  precedent); it emits **no** `AccessAudit` row on a read (the M13 "read =
  no row; export = one row" discipline applies: M24 has no export lane in
  v1, so it emits **zero** audit rows — the export is a **future lane**,
  named in the ADR's Consequences).
- **Charts / trend lines / per-day breakdowns** (the M13 deferred-lane
  precedent — M24 ships a *snapshot*, not a *time series*; a time series over
  the storage surface is a **future lane**, named in the ADR's
  Consequences).
- **The M13 "per-account rendered analytics" boundary** (C-M13·3) applies by
  analogy: M24's per-user table **is** an admin-visible rendering of
  per-account data (which is **in-scope** for M24 — the M13 boundary was
  about *resident-facing* per-account data, which M24 does not render).
  M24's per-user metric is **admin-only** (C-SM·6); a resident's view of
  their own usage is **M25**'s U7.

**The one thing to internalize before writing code:** the **single-in-progress
milestone** contract. `MilestonesTests.cs` pins that **exactly one**
milestone is `StatusNext` (in-progress) and names it (currently **`M24`**).
So **M24's U1 enforces the precondition** (M24 is `StatusNext`, M25 is
`StatusPlanned` — the current state, confirmed by reading `Milestones.cs`)
and **M24's U8 (close)** flips M24 → `StatusDone` and promotes M25 →
`StatusNext`, updating the two `MilestonesTests.cs` tests to match. Closing
M24 **unblocks M25 U1/U2**, which are currently `BLOCKED` on exactly this
precondition.

**The M25 handoff is a **closed-loop** artifact:** M25's U5 (admin storage
view) is explicitly written to "reuse M24's metrics seam if present" (see
`in-progress/m25-u05.md` line 37) and M25's U12 (close) records "whether M25's
per-user usage seam was reconciled with M24's." The drift-guard (design doc
§2.6) pins the exact seam names + shapes so M25 can either **reuse** them
directly or **re-point** its `GetPerUserUsageBytesAsync` to M24's seam — both
are valid, but the **drift-guard** must name the exact seam M24 ships, so a
M25 agent can find it by name, not by guess.

## Assumptions

- **Scope (per the README/Milestones M24 title):** In: a **pure read**
  `IStorageMetricsService` + `StorageMetricsService` in `Kumunita.Core.Usage`
  with a single `GetSnapshotAsync` (total used / available / user-content
  used / per-user top-N) and a **paged per-user list** (`GetPerUserListAsync`
  — the admin's per-user table; the M24 title's "space used per user"); a
  **GlobalAdmin-gated** `/admin/storage` **view** (read-only, no form, no
  write, no settings — M25's territory); **two** **read-only ADDs** on
  `IMediaFileStore` (`GetTotalSpaceBytesAsync` +
  `GetFreeSpaceBytesAsync`); the pinned Core + Web tests; the ADR +
  OPS/README/Milestones docs sync. **Out (named non-decisions above):**
  the admin settings surface (M25), enforcement (M25), the resident self-usage
  view (M25), per-resident quota overrides, per-file size-limit setting (M25),
  any `IMediaStore` / `MediaObject` reshape, any new bounded context, any
  new `AccessAction` / `AccessVia` / `AccessAudit` row, charts / trend lines
  / time series, per-day breakdowns, the CSV export lane (a future lane,
  named in the ADR's Consequences), and the M13 "per-account rendered
  analytics" boundary (which is about *resident-facing* data, and M24 does
  not render any).
- **`Core` stays HTTP-free** (ADR 0006-D; C-SM·3):
  `IStorageMetricsService` returns **plain DTOs** (`StorageMetricsSnapshot`,
  `PerUserStorageRow`, `PerUserStoragePage`), no `IFormFile`, no `ActionResult`,
  no HTTP. The `/admin/storage` view model + the `ActionResult` mapping are
  **Web-only** (`Kumunita.Web`).
- **The two `IMediaFileStore` ADDs are read-only and additive** (ADR 0006-E
  lane; C-SM·2): the `IMediaStore` interface is **not touched** at all; the
  `MediaObject` doc is **not touched** at all; M24 adds **two** read-only
  methods to `IMediaFileStore` (the *volume* seam, not the *catalog* seam)
  and implements them in `LocalVolumeFileStore`. Both use
  `System.IO.DriveInfo` on the root-path's volume (or a `statvfs` fallback
  on Linux; the unit that lands this documents the fallback in the design
  doc §2.1 + a one-line handoff note). No other `IMediaFileStore` member is
  touched; the `IMediaStore` interface is **frozen** for M24 (the C-SM·1 pin).
- **`Kumunita.Core.Usage` is the home** (C-SM·3): `IStorageMetricsService` +
  `StorageMetricsService` live in the **existing** `Kumunita.Core.Usage`
  namespace, next to `IUsageAnalyticsService` + `UsageAnalyticsService`. The
  DI registration follows the `UsageAnalyticsService` shape
  (`AddTransient<IStorageMetricsService>(sp => new StorageMetricsService(sp.GetRequiredService<IDocumentStore>(), sp.GetRequiredService<IMediaFileStore>()))`).
  **No new doc surface** (M24 has no doc to register); **no new bounded
  context** (the C-SM·3 pin).
- **The read is a single `QuerySession`** (C-SM·2/4):
  `StorageMetricsService.GetSnapshotAsync` opens **one** `IDocumentStore.QuerySession()`
  and runs **two** queries over the `MediaObject` doc: (1)
  `Query<MediaObject>().Sum(o => o.SizeBytes)` (the community total used —
  the "user-content used" in the M24 title; the *total* used on the volume
  may be *slightly* larger if there are orphan files, but M24's "total used"
  is the **catalog** total, matching "user-content used" — the two are
  **equal by design** and the design doc §2.3 pins this equivalence + the
  "orphan file" caveat as a **named non-decision**); (2)
  `Query<MediaObject>().GroupBy(o => o.CreatedById).Select(g => (CreatedById: g.Key, Bytes: g.Sum(x => x.SizeBytes)))`
  (the per-user map; M24's `GetPerUserListAsync` re-queries the same shape
  with paging + sort). The `GetTotalSpaceBytesAsync` +
  `GetFreeSpaceBytesAsync` volume reads are **not** in the session — they
  call `IMediaFileStore` directly (a local-volume filesystem stat, not a
  Postgres query). **Zero writes**, **zero audit rows** (C-SM·2/6).
- **The admin surface is `GlobalAdmin`-gated** (C-SM·6; the M13
  `AdminAnalyticsController` precedent): `[Authorize(Roles =
  Kumunita.Core.Identity.Roles.GlobalAdmin)]` on the controller; the
  `/admin` hub gains one nav row ("Storage"); the view is **read-only** (no
  form, no submit, no POST action — M25's U5/U6 own the set-lane). The view
  model is `AdminStorageMetricsViewModel` (the Web projection of
  `StorageMetricsSnapshot` + `PerUserStoragePage`).
- **The per-user table is paged** (C-SM·4/7): `GetPerUserListAsync(int page,
  int pageSize = 25)` returns `PerUserStoragePage { Items, TotalUsers, Page,
  HasMore }` — the M7 `HasMore` discipline (the house pagination shape).
  **No sort-by-column in M24 v1** (M26's "sorting" lane owns it; the default
  sort is **descending by bytes used**, which is the admin's natural first
  question: "who uses the most space?"). The M26 sort-lane will add the
  `?sort=` + `?dir=` params to `/admin/storage` — **named in the ADR's
  Consequences** as the M26 hook.
- **Sentinel semantics are explicit (C-SM·5):** a **`CreatedById` of `null`
  or empty string** (the pre-M13-era uploads where the actor was not
  captured) lands in a **single "unknown" bucket** on the per-user table —
  the design doc §2.3 pins this + the exact bucket label ("unknown / not
  captured") + the test name that asserts it (`PerUser_UnknownBucketGrouped`).
  **Zero-size** rows (a `MediaObject` with `SizeBytes == 0`) are
  **excluded** from the per-user list (a `SizeBytes == 0` doc is a
  zero-byte upload, not a usage event — the design doc §2.3 pins this +
  the test name `PerUser_ZeroSizeRowsExcluded`).
- **The four metrics are a **single** `GetSnapshotAsync` call** (C-SM·2/4):
  `Task<StorageMetricsSnapshot> GetSnapshotAsync(CancellationToken ct)` —
  **one** call, **one** `QuerySession`, **two** catalog queries + **two**
  volume reads. The DTO is:
  `public sealed record StorageMetricsSnapshot(long TotalUsedBytes, long
  TotalVolumeBytes, long FreeVolumeBytes, long UserContentUsedBytes,
  int TotalUniqueFiles, int TotalDistinctUsers, DateTimeOffset AsOf);`
  where `UserContentUsedBytes == TotalUsedBytes` **by design** (the
  "user-content used" and "total used" are the same number in M24 v1 —
  the design doc §2.3 pins this + the caveat that the *volume* total may
  be slightly larger if orphan files exist, which is a **named
  non-decision**). The M24 title's four questions are: (1) "total used
  space" = `TotalUsedBytes`; (2) "available space" = `FreeVolumeBytes`;
  (3) "user-content used space" = `UserContentUsedBytes` (= `TotalUsedBytes`
  in v1); (4) "space used per user" = the `GetPerUserListAsync` table.
- **The M25 handoff is **reusable by name** (C-SM·7):** the design doc
  §2.1 pins the **exact** C# shape of the per-user read seam M25 will reuse
  — `Task<long> GetPerUserUsageBytesAsync(string subjectId,
  CancellationToken ct)` (a **new** method on `IStorageMetricsService`,
  distinct from the paged `GetPerUserListAsync`) — so that M25's U4
  (`IStorageSettingsService.GetPerUserUsageBytesAsync`) can either be
  **re-pointed** to M24's seam (M25's `IStorageSettingsService` **delegates**
  to `IStorageMetricsService.GetPerUserUsageBytesAsync`) or can be a
  **duplicate** (M25 ships its own `GetPerUserUsageBytesAsync` on
  `IStorageSettingsService`, the two are **equivalent** and M25's U12
  close reconciles them). The **drift-guard** (design doc §2.6) names the
  exact seam M24 ships so a M25 agent can find it by name.
- **Test model (unchanged).** The invariant-anchored seam-test list pinned
  in the design doc Part 2 + the **three-test acceptance gate**
  (closed-loop / handoff / part-vs-whole), recorded in the rolling handoff
  note. The Core tests are the seam tests (the pure `Decide`-shape tests
  are M25's, not M24's); M24's Core tests are the **read-shape** tests
  (the `Sum` / `GroupBy` / `Filter` / `Page` assertions). The Web tests are
  the **admin-surface** tests (the `GlobalAdmin` gate + the non-GlobalAdmin
  403 + the per-user table rendering).
- **Milestone contract (C-SM·7):** M24 begins with M24 `StatusNext` (the
  current state — **U1 enforces this**); U8 closes M24 (`StatusDone`) and
  promotes M25 (`StatusNext`), updating `MilestonesTests.cs`
  (`Shipped_Milestones_Are_Marked_Done` gains M24; the
  `M24_Is_The_Single_InProgress_Milestone` test is **re-pointed** to M25).
  U8's close **unblocks M25 U1/U2** (which are currently `BLOCKED` on this
  exact precondition — see `in-progress/m25-handoff-notes.md`).

## Approach

Two tracks, sequenced — **Track A (Core + docs)** then **Track B (Web + tests
+ close)**, each unit **build-green** before handoff. **Track A (U1–U4):**
the design doc (U1/U2), the two `IMediaFileStore` read-only ADDs + the
`LocalVolumeFileStore` impl (U3), and `IStorageMetricsService` +
`StorageMetricsService` + the Core read-shape tests (U4). **Track B
(U5–U8):** the `/admin/storage` admin view + nav row + view model (U5), the
Web admin-surface tests (U6), the acceptance gate + FACES tests (U7), and
the close: `Milestones.cs` + `MilestonesTests.cs` status flip + ADR +
OPS/README docs + design-doc close section + handoff close + move to
`done/m24/` (U8).

Every unit ends with **build green** (and, for test-bearing units, the pinned
tests passing via the repo's reliable runner — see the *Running the tests*
section). **U1** appends the first handoff section and enforces the
milestone-start precondition; **U8** appends the final handoff section and
the "M24 — Closed (recorded)" design-doc section and moves the plan + handoff
note to `done/m24/`.

## Workflow — handoff protocol for fresh-context agents

Executed as a sequence of **sealed units** (U1–U8 below), one unit per fresh
agent with a ~32K context window.

**Shared state (three-tier contract):**
- **Primary — the design doc**
  (`docs/design/m24-storage-metrics-design.md`, authored in U1/U2) pins the
  exact C# signatures of every seam U3–U7 must match.
- **Secondary — this file**
  (`docs/plans-milestones/plan-m24-storage-metrics.md`) — the unit registry
  with each unit's deliverables and exit criteria.
- **Scratch — the rolling handoff note**
  (`docs/plans-milestones/in-progress/m24-handoff-notes.md`) — one
  `## U#` section per unit, appended (never rewritten). Created by U1, moved
  to `done/m24/` by U8.

**Per-unit template** (each `U` below follows this): **Goal** (one sentence,
one or two related deliverables); **Entry reads** (the minimal file list,
3–5 files <~300 lines each, no full-repo scan; the design-doc section cited
is named); **Deliverables** (a closed set of new/modified files, ≤ ~5 files
/ ~600 LOC, no misc cleanups); **Exit** (`run_build` green for the touched
projects; handoff-note entry appended *before* any follow-up action).

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside the §2.6
drift-guard; (3) never introduces a test whose exact name is not in the
§2.4 seam list; (4) never opens a *new* seam on `IMediaStore` (frozen for
M24 — C-SM·1) beyond the two `IMediaFileStore` ADDs pinned in §2.1;
(5) never re-shapes `MediaObject` (frozen — C-SM·1); (6) if entry reads
reveal the design doc is out of date, the unit pauses and records
`## U<m> — Drift pause` in the handoff note.

**The M25 handoff is a **closed-loop** artifact:** M25's U5 (admin storage
view) is explicitly written to "reuse M24's metrics seam if present" (see
`in-progress/m25-u05.md` line 37); M25's U12 (close) records "whether M25's
per-user usage seam was reconciled with M24's." The drift-guard (design doc
§2.6) pins the exact seam names + shapes so M25 can either **reuse** them
directly or **re-point** its `GetPerUserUsageBytesAsync` to M24's seam — both
are valid, but the **drift-guard** must name the exact seam M24 ships, so a
M25 agent can find it by name, not by guess.

**Running the tests (test-runner quirk — per AGENTS.md):**

Both test projects use **xunit.v3** (`Microsoft.Testing.Platform`), surfaced
to VS Test Explorer via `Microsoft.Testing.Extensions.VSTestBridge`. On this
machine the discovery path reliably goes wrong: VS Test Explorer (the
`run_tests` tool) shows "Discovered: N Tests found … No tests found to run",
and `dotnet test` fails with `Zero tests ran / Exit code: 5`. The reliable
path is to build, then run each test assembly in-process through xunit.v3's
own runner:

```powershell
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
```

`Kumunita.Web.Tests` runs in well under a second; `Kumunita.Core.Tests`
takes ~20 s because it starts `postgres:18` via Testcontainers (and leaves
Docker containers behind if the process is killed — clean up with
`docker container prune`).

---

## Units (8 total)

### U1 — Design doc Part 1
- **Goal:** author `docs/design/m24-storage-metrics-design.md` Part 1 —
  **Context, Scope (in/out incl. the named non-decisions), Invariants pinned
  for M24, FACES (10)**. Mirrors M25's design doc §1 (in-progress) + M13's
  design doc (the closest analog — the admin analytics surface). **No code,
  no build.**
- **Entry reads (3–5 files, no full-repo scan):**
  - `docs/plans-milestones/in-progress/m25-u01.md` — the M25 Part 1 to
    emulate (the FACES/invariant template; the M24↔M25 boundary).
  - `docs/plans-milestones/done/m13/plan-m13-logging-analytics.md` — the
    closest analog (the admin analytics surface; the "operator sees the
    platform" value-chain row).
  - `docs/adr/0011-media-and-file-storage.md` — C-MED·1–7; the
    content-addressed store + the `MediaObject` doc + the volume + the
    `MaxBytes` boundary M24 reads (not enforces).
  - `src/Kumunita.Core/Media/MediaObject.cs` — the `SizeBytes` /
    `CreatedById` fields the per-user usage metric keys off.
  - `src/Kumunita.Core/Media/IMediaFileStore.cs` — the volume seam U3 will
    add **two** read-only ADDs to (C-SM·2).
  - `src/Kumunita.Web/Milestones.cs` — the M24/M25 status (the C-SM·7
    precondition).
- **Deliverables (1 file, new):** `docs/design/m24-storage-metrics-design.md`
  (~220 lines). Sections:
  - `## Context` — M13 surfaces *usage analytics*; M24 surfaces *storage
    metrics* (the admin view of the byte store: total used, available,
    user-content used, per-user used); what M24 does *not* do (the
    non-decisions); the arrow moved (the operator's storage feedback loop,
    closed locally).
  - `## Scope` — **In:** the `IStorageMetricsService` +
    `StorageMetricsService` in `Kumunita.Core.Usage` (the
    `GetSnapshotAsync` + `GetPerUserListAsync` + `GetPerUserUsageBytesAsync`
    seams); the two `IMediaFileStore` read-only ADDs
    (`GetTotalSpaceBytesAsync` + `GetFreeSpaceBytesAsync`) + the
    `LocalVolumeFileStore` impl; the `/admin/storage` admin view + nav row +
    view model; the pinned Core + Web tests; the ADR + OPS/README/Milestones
    sync. **Out (named non-decisions):** the admin settings surface (M25),
    enforcement (M25), the resident self-usage view (M25), per-resident quota
    overrides, per-file size-limit setting (M25), any `IMediaStore` /
    `MediaObject` reshape, any new bounded context, any new `AccessAction` /
    `AccessVia` / `AccessAudit` row, charts / trend lines / time series,
    per-day breakdowns, the CSV export lane, the M13 "per-account rendered
    analytics" boundary (M24 is admin-only).
  - `## Invariants (pinned for M24)` — the **7 invariants** (ids/names
    **frozen** once written):
    - **C-SM·1** — **frozen interfaces:** `IMediaStore` + `MediaObject` are
      **not reshaped** by M24; M24 only *reads* them. The two
      `IMediaFileStore` ADDs are the only interface change, and they are
      read-only.
    - **C-SM·2** — **read-only, zero writes:** M24 opens a `QuerySession`
      (a read) + calls two volume-stat methods (reads); it emits **zero**
      writes, **zero** `AccessAudit` rows, **zero** new documents.
    - **C-SM·3** — **`Core` stays HTTP-free** (ADR 0006-D): the seam returns
      plain DTOs (`StorageMetricsSnapshot`, `PerUserStorageRow`,
      `PerUserStoragePage`); no `IFormFile`, no `ActionResult`. The
      `/admin/storage` view model is Web-only.
    - **C-SM·4** — **single `QuerySession`, two catalog queries:**
      `GetSnapshotAsync` runs one `QuerySession` + two `MediaObject` queries
      (the community total + the per-user map); the two volume-stat reads
      are **not** in the session (a local-volume filesystem stat, not a
      Postgres query).
    - **C-SM·5** — **sentinel semantics:** `CreatedById` of `null` /
      empty string lands in a single "unknown / not captured" bucket on the
      per-user table; `SizeBytes == 0` rows are excluded from the per-user
      list.
    - **C-SM·6** — **`GlobalAdmin`-gated, no audit row:** the
      `/admin/storage` surface is `[Authorize(Roles = GlobalAdmin)]`
      (the M13 `AdminAnalyticsController` precedent); it emits **no**
      `AccessAudit` row on a read (the M13 "read = no row; export = one
      row" discipline; M24 has no export lane in v1, so zero rows).
    - **C-SM·7** — **single-in-progress milestone contract:** M24 begins
      with M24 `StatusNext` (the current state — U1 enforces); U8 closes M24
      (`StatusDone`) and promotes M25 (`StatusNext`), updating
      `MilestonesTests.cs`. Closing M24 unblocks M25 U1/U2 (which are
      currently `BLOCKED`).
  - `## FACES (pinned, 10)` — F1–F10, each bound to an invariant (names
    **frozen** once written):
    - **F1** the `/admin/storage` page renders the four metrics (total
      used, available, user-content used, per-user table) — C-SM·2, C-SM·6
    - **F2** non-GlobalAdmins are **403** from `/admin/storage` — C-SM·6
    - **F3** the per-user table is paged (the M7 `HasMore` discipline) —
      C-SM·4
    - **F4** the per-user table defaults to **descending by bytes used**
      (the admin's natural first question) — C-SM·4
    - **F5** `CreatedById` of `null` / empty string lands in a single
      "unknown / not captured" bucket — C-SM·5
    - **F6** `SizeBytes == 0` rows are excluded from the per-user table —
      C-SM·5
    - **F7** the two volume-stat reads are **not** in the `QuerySession`
      (a local-volume filesystem stat, not a Postgres query) — C-SM·4
    - **F8** the `/admin/storage` surface emits **zero** `AccessAudit` rows
      (no export lane in v1) — C-SM·6
    - **F9** `IMediaStore` + `MediaObject` are **not reshaped** by M24 —
      C-SM·1
    - **F10** the `GetPerUserUsageBytesAsync` seam is **reusable by name**
      by M25 (the M25 handoff is a **closed-loop** artifact) — C-SM·7

2. **`docs/plans-milestones/in-progress/m24-handoff-notes.md`** — the first
   section `## U1 — design doc Part 1` (5–7 lines: the **7 invariants** by
   id + the **10 FACES** F1–F10 + the **C-SM·7 precondition outcome**
   (M24 `StatusNext` confirmed — or the BLOCKED state + the exact status)).

- **Exit:** design doc Part 1 exists with all sections (Context / Scope /
  Invariants / FACES). Handoff note exists with the U1 section. **No code,
  no build.**

### U2 — Design doc Part 2 (seams, contracts, test list, gate, drift-guard)
- **Goal:** append `## Seams & contracts (Part 2, written by U2)` to the
  design doc — the exact C# shapes U3–U7 must match, the **pinned Core test
  names** (U4), the **pinned Web test names** (U6), the **U7 acceptance
  gate**, and the **drift-guard**. **No code, no build.**
- **Entry reads (3–5 files):**
  - `docs/design/m24-storage-metrics-design.md` (U1's Part 1 — the primary
    source).
  - `src/Kumunita.Core/Usage/UsageAnalyticsService.cs` — the
    `Kumunita.Core.Usage` context home M24's types live in; mirror its
    registration + its `QuerySession` read shape.
  - `src/Kumunita.Core/Media/IMediaFileStore.cs` — the volume seam U3 will
    add two read-only ADDs to (C-SM·2).
  - `src/Kumunita.Core/Media/LocalVolumeFileStore.cs` — the `IMediaFileStore`
    impl U3 will extend (the `DriveInfo` / `statvfs` fallback).
  - `src/Kumunita.Core/DependencyInjection.cs` — the `AddTransient` + factory
    registration shape (U4 adds `IStorageMetricsService`).
  - `src/Kumunita.Web/Controllers/AdminAnalyticsController.cs` — the M13
    admin analytics surface U5 will mirror (the `GlobalAdmin` gate + the
    `QuerySession` read + the "no audit row on a read" discipline).
- **Deliverables (1 append, same file):** `docs/design/m24-storage-metrics-design.md`.
  Sub-sections:
  - `### 2.1 New Core-owned types (exact C#)` — namespace
    `Kumunita.Core.Usage`:
    - `IStorageMetricsService`:
      - `Task<StorageMetricsSnapshot> GetSnapshotAsync(CancellationToken ct)`
        — the **four** metrics (total used, available, user-content used,
        per-user top-N). **One** `QuerySession` + **two** catalog queries +
        **two** volume reads (C-SM·4).
      - `Task<PerUserStoragePage> GetPerUserListAsync(int page, int pageSize
        = 25, CancellationToken ct)` — the **paged** per-user table (the M24
        title's "space used per user"). Default sort: **descending by bytes
        used**. The M7 `HasMore` discipline (C-SM·4/7).
      - `Task<long> GetPerUserUsageBytesAsync(string subjectId,
        CancellationToken ct)` — the **C-SM·7** handoff seam M25 will reuse
        (or re-point to): `Σ SizeBytes WHERE CreatedById == subjectId`.
        **This is the exact shape M25's U4
        (`IStorageSettingsService.GetPerUserUsageBytesAsync`) will
        delegate to or duplicate** — the drift-guard names it.
    - `StorageMetricsService(IDocumentStore store, IMediaFileStore volume)`
      — the impl.
    - `StorageMetricsSnapshot` (the DTO):
      `public sealed record StorageMetricsSnapshot(long TotalUsedBytes, long
      TotalVolumeBytes, long FreeVolumeBytes, long UserContentUsedBytes,
      int TotalUniqueFiles, int TotalDistinctUsers, DateTimeOffset AsOf);`
      where `UserContentUsedBytes == TotalUsedBytes` **by design** (C-SM·2/4;
      the "orphan file" caveat is a named non-decision).
    - `PerUserStorageRow` (the per-user row):
      `public sealed record PerUserStorageRow(string? CreatedById, long
      Bytes, int FileCount);` where `CreatedById == null` / `""` means the
      "unknown / not captured" bucket (C-SM·5).
    - `PerUserStoragePage` (the paged DTO):
      `public sealed record PerUserStoragePage(IReadOnlyList<PerUserStorageRow>
      Items, int TotalUsers, int Page, bool HasMore);`
    - `IMediaFileStore` — **two** **read-only ADDs** (C-SM·2; ADR 0006-E
      lane):
      - `Task<long> GetTotalSpaceBytesAsync(CancellationToken ct = default)`
        — the disk-partition total (a `DriveInfo` / `statvfs` read;
        **not** a `QuerySession`).
      - `Task<long> GetFreeSpaceBytesAsync(CancellationToken ct = default)`
        — the disk-partition free (a `DriveInfo` / `statvfs` read; **not** a
        `QuerySession`).
      - **No other `IMediaFileStore` member is touched** (C-SM·1).
  - `### 2.2 The two `IMediaFileStore` ADDs — the exact impl** (U3):
    - `LocalVolumeFileStore.GetTotalSpaceBytesAsync` — on Windows:
      `DriveInfo.GetDriveFromPath(RootPath).TotalSize`; on Linux
      (containers): `statvfs(RootPath).f_blocks * statvfs(RootPath).f_frsize`
      (the unit that lands this documents the fallback + the test that pins
      it — `GetTotalSpaceBytes_ReturnsPositive`).
    - `LocalVolumeFileStore.GetFreeSpaceBytesAsync` — on Windows:
      `DriveInfo.GetDriveFromPath(RootPath).AvailableFreeSpace`; on Linux:
      `statvfs(RootPath).f_bavail * statvfs(RootPath).f_frsize`.
    - **The impl is **read-only** + **no I/O side-effect** beyond the
      statvfs / DriveInfo call** (C-SM·2).
  - `### 2.3 The **single** `QuerySession` shape (C-SM·4)** (U4):
    - `GetSnapshotAsync`:
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
      **The M25 handoff is `GetPerUserUsageBytesAsync` — the **exact** shape
      (C-SM·7):**
      ```csharp
      await using var session = _store.QuerySession();
      return await session.Query<MediaObject>()
        .Where(o => o.CreatedById == subjectId)
        .Sum(o => o.SizeBytes);
      ```
      **This is the seam M25's U4
      (`IStorageSettingsService.GetPerUserUsageBytesAsync`) will delegate to
      or duplicate** — the drift-guard names it.
    - `GetPerUserListAsync`: the same `GroupBy` + `Where` as above, but with
      **paging** + **sort** (the M7 `HasMore` discipline):
      ```csharp
      var all = await session.Query<MediaObject>()
        .Where(o => o.SizeBytes > 0)
        .GroupBy(o => o.CreatedById)
        .Select(g => new PerUserStorageRow(g.Key, g.Sum(x => x.SizeBytes),
          g.Count()))
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
      **The "unknown" bucket** (C-SM·5) is **one row** on the per-user table
      (the `CreatedById == null` / `""` bucket); the **`SizeBytes == 0`
      rows** are **excluded** (the `Where(o => o.SizeBytes > 0)` clause;
      C-SM·5).
  - `### 2.4 Pinned Core tests (exact names)` —
    `tests/Kumunita.Core.Tests/Usage/StorageMetricsTests.cs`:
    1. `GetSnapshot_TotalUsed_SumsAllMediaObjects` (C-SM·2/4)
    2. `GetSnapshot_UserContentUsed_Equals_TotalUsed` (C-SM·2/4)
    3. `GetSnapshot_VolumeTotals_AreNotInQuerySession` (C-SM·4/7)
    4. `GetPerUserList_PagesAndSortsByBytesDesc` (C-SM·4)
    5. `PerUser_UnknownBucketGrouped` (C-SM·5)
    6. `PerUser_ZeroSizeRowsExcluded` (C-SM·5)
    7. `GetPerUserUsage_ReturnsOnlySubjectBytes` (C-SM·7 — the M25
       handoff seam)
    8. `GetPerUserUsage_ReturnsZeroForUnknownSubject` (C-SM·7)
    9. `GetSnapshot_ZeroRows_ReturnsZeroMetrics` (C-SM·2)
    10. `GetPerUserList_UnknownBucketIsOneRow` (C-SM·5)
  - `### 2.5 Pinned Web tests (exact names)` —
    `tests/Kumunita.Web.Tests/AdminStorageMetricsControllerTests.cs`:
    11. `AdminStorage_GlobalAdmin_Allowed` (C-SM·6)
    12. `AdminStorage_NonGlobalAdmin_Forbidden` (C-SM·6)
    13. `AdminStorage_NoAccessAuditRow` (C-SM·6)
    14. `AdminStorage_PerUserTable_RendersWithHasMore` (C-SM·4)
    15. `AdminStorage_FourMetrics_Render` (C-SM·2/4)
  - `### 2.6 Acceptance gate (U7 records)` — the three-test shape:
    - **closed loop:** a GlobalAdmin signs in → `/admin/storage` renders the
      four metrics (total used, available, user-content used, per-user
      table) → the per-user table is paged + sorted by bytes desc.
    - **handoff:** the `/admin` hub shows the new "Storage" nav row → a
      non-GlobalAdmin is **403** from `/admin/storage` (the C-SM·6 gate).
    - **part-vs-whole:** the 15-test list (10 Core + 5 Web) is the whole;
      closed-loop + handoff are the parts; all must pass together.
  - `### 2.7 Drift-guard (frozen once written)` — the 7 invariants
    (C-SM·1–7), the 10 FACES (F1–F10), the `IStorageMetricsService`
    3-method surface (the **exact** C# shapes in §2.1), the
    `StorageMetricsSnapshot` / `PerUserStorageRow` / `PerUserStoragePage`
    DTO shapes, the two `IMediaFileStore` ADDs (`GetTotalSpaceBytesAsync` +
    `GetFreeSpaceBytesAsync`), the **`GetPerUserUsageBytesAsync` seam**
    (the M25 handoff — **C-SM·7**), the **`SizeBytes == 0` exclusion** +
    the **`CreatedById == null` / `""` "unknown" bucket** (C-SM·5), the
    10 Core test names + the 5 Web test names, and the **C-SM·7 milestone
    contract** (M24 `StatusNext` at start; M24 `StatusDone` + M25
    `StatusNext` at close). Any mismatch (e.g. M25's U4
    `IStorageSettingsService.GetPerUserUsageBytesAsync` **should** delegate
    to M24's `IStorageMetricsService.GetPerUserUsageBytesAsync` — the
    drift-guard names the exact seam M24 ships) is a `## U<m> — Drift pause`
    (unit-series rule §6).

- **Exit:** file exists with all Part 2 sub-sections. **No build.** Handoff
  note: 6–8 lines starting `## U2 — design doc Part 2`, listing (a) the
  sealed seam signatures (the 3 `IStorageMetricsService` methods + the two
  `IMediaFileStore` ADDs), (b) the 15 test names by id, (c) the three-test
  gate (by name), (d) the **`GetPerUserUsageBytesAsync`** seam (the M25
  handoff — **C-SM·7**).

### U3 — The two `IMediaFileStore` read-only ADDs + the `LocalVolumeFileStore` impl
- **Goal:** add the two **read-only** `IMediaFileStore` ADDs
  (`GetTotalSpaceBytesAsync` + `GetFreeSpaceBytesAsync`) + implement them
  in `LocalVolumeFileStore` (the `DriveInfo` / `statvfs` fallback). **No
  other `IMediaFileStore` member is touched** (C-SM·1). **No Core service,
  no Web, no test** (the Core tests are U4; the Web is U5+).
- **Entry reads (3–5 files):**
  - `docs/design/m24-storage-metrics-design.md` §2.1 (the two ADDs) + §2.2
    (the exact impl).
  - `src/Kumunita.Core/Media/IMediaFileStore.cs` — the volume seam U3 will
    add two read-only ADDs to (C-SM·2).
  - `src/Kumunita.Core/Media/LocalVolumeFileStore.cs` — the `IMediaFileStore`
    impl U3 will extend (the `DriveInfo` / `statvfs` fallback).
  - `src/Kumunita.Core/Media/MediaOptions.cs` — the `RootPath` the volume
    stats key off.
- **Deliverables (2 files, modified):**
  1. **`src/Kumunita.Core/Media/IMediaFileStore.cs`** — append the two
     **read-only** ADDs (C-SM·2; ADR 0006-E lane):
     ```csharp
     /// <summary>The disk-partition total bytes (a `DriveInfo` / `statvfs` read; not a `QuerySession`). M24, ADR 0134 C-SM·2.</summary>
     Task<long> GetTotalSpaceBytesAsync(CancellationToken ct = default);
     /// <summary>The disk-partition free bytes (a `DriveInfo` / `statvfs` read; not a `QuerySession`). M24, ADR 0134 C-SM·2.</summary>
     Task<long> GetFreeSpaceBytesAsync(CancellationToken ct = default);
     ```
  2. **`src/Kumunita.Core/Media/LocalVolumeFileStore.cs`** — implement the
     two ADDs (the `DriveInfo` / `statvfs` fallback; C-SM·2):
     ```csharp
     public Task<long> GetTotalSpaceBytesAsync(CancellationToken ct = default)
     {
         // Windows: DriveInfo; Linux (containers): statvfs.
         // The unit that lands this documents the fallback in the design doc §2.2 + the handoff note.
         return Task.FromResult(
             System.OperatingSystem.IsWindows()
                 ? System.IO.DriveInfo.GetDriveFromPath(RootPath).TotalSize
                 : statvfs(RootPath).f_blocks * statvfs(RootPath).f_frsize);
     }
     public Task<long> GetFreeSpaceBytesAsync(CancellationToken ct = default)
     {
         return Task.FromResult(
             System.OperatingSystem.IsWindows()
                 ? System.IO.DriveInfo.GetDriveFromPath(RootPath).AvailableFreeSpace
                 : statvfs(RootPath).f_bavail * statvfs(RootPath).f_frsize);
     }
     ```
     **The `statvfs` fallback** (Linux) is a **P/Invoke** to
     `System.IO.MemoryMappedFiles` or a **`DllImport`** to `libc` — the unit
     that lands this documents the exact fallback + the test that pins it
     (`GetTotalSpaceBytes_ReturnsPositive` — U4's Core test). **The impl is
     read-only + no I/O side-effect beyond the statvfs / DriveInfo call**
     (C-SM·2).
- **Exit:** `run_build` on `Kumunita.Core` green. **No new test** (U4's Core
  tests are the first M24 Core tests). Handoff note (4–5 lines,
  `## U3 — volume-stat ADDs`): (a) the two ADDs (the exact C#), (b) the
  `statvfs` fallback (the P/Invoke / `DllImport` shape, or the
  `DriveInfo`-only shape if Linux is out of scope for M24 v1), (c) the
  **`IMediaStore` interface is untouched** (C-SM·1 confirmed), (d) any
  compile warnings.

### U4 — `IStorageMetricsService` + `StorageMetricsService` + the Core read-shape tests
- **Goal:** implement the **Core** of M24 — the service seam
  (`IStorageMetricsService`: `GetSnapshotAsync` / `GetPerUserListAsync` /
  `GetPerUserUsageBytesAsync`) + the `StorageMetricsService` impl + the
  **10 pinned Core tests** (design doc §2.4). It registers the service in
  `AddKumunitaCore`. The service is **read-only** (C-SM·2): a `QuerySession`
  + two catalog queries + two volume reads; **no writes**, **no audit rows**,
  **no new documents**.
- **Entry reads (3–5 files):**
  - `docs/design/m24-storage-metrics-design.md` §2.1 (the exact
    `IStorageMetricsService` + DTO shapes) + §2.3 (the `QuerySession` shape)
    + §2.4 (the 10 Core test names).
  - `src/Kumunita.Core/Media/MediaObject.cs` — the `SizeBytes` /
    `CreatedById` fields the per-user usage metric keys off.
  - `src/Kumunita.Core/Usage/UsageAnalyticsService.cs` — the
    `Kumunita.Core.Usage` context home M24's types live in; mirror its
    registration + its `QuerySession` read shape.
  - `src/Kumunita.Core/DependencyInjection.cs` — the `AddTransient` + factory
    registration shape (U4 adds `IStorageMetricsService`).
  - `tests/Kumunita.Core.Tests/PostgresFixture.cs` (the Testcontainers
    harness the service tests need) + `tests/Kumunita.Core.Tests/Usage/`
    (the existing usage-context test harness to mirror).
  - `docs/plans-milestones/plan-m24-storage-metrics.md` §*Running the tests*
    (the xunit.v3 runner quirk — **build, then `dotnet exec` the test dlls**;
    do not use `dotnet test`).
- **Deliverables (≤ 5 files):**
  1. **`src/Kumunita.Core/Usage/IStorageMetricsService.cs`** — the 3-method
     seam per §2.1:
     ```csharp
     public interface IStorageMetricsService
     {
         Task<StorageMetricsSnapshot> GetSnapshotAsync(CancellationToken ct = default);
         Task<PerUserStoragePage> GetPerUserListAsync(int page, int pageSize = 25, CancellationToken ct = default);
         Task<long> GetPerUserUsageBytesAsync(string subjectId, CancellationToken ct = default);
     }
     ```
  2. **`src/Kumunita.Core/Usage/StorageMetricsService.cs`** — the impl (the
     three methods per §2.3; the **single** `QuerySession` + two catalog
     queries + two volume reads; the `SizeBytes == 0` exclusion + the
     `CreatedById == null` / `""` "unknown" bucket; the **default sort**
     descending by bytes used; the **M25 handoff** `GetPerUserUsageBytesAsync`
     — the exact shape in §2.3).
  3. **`src/Kumunita.Core/Usage/StorageMetricsSnapshot.cs`** — the DTO:
     ```csharp
     public sealed record StorageMetricsSnapshot(
       long TotalUsedBytes,
       long TotalVolumeBytes,
       long FreeVolumeBytes,
       long UserContentUsedBytes,
       int TotalUniqueFiles,
       int TotalDistinctUsers,
       DateTimeOffset AsOf);
     ```
  4. **`src/Kumunita.Core/Usage/PerUserStorageRow.cs`** +
     **`src/Kumunita.Core/Usage/PerUserStoragePage.cs`** — the per-user row
     + the paged DTO (per §2.1).
  5. **`tests/Kumunita.Core.Tests/Usage/StorageMetricsTests.cs`** — the 10
     pinned Core tests (§2.4, items 1–10):
     `GetSnapshot_TotalUsed_SumsAllMediaObjects`,
     `GetSnapshot_UserContentUsed_Equals_TotalUsed`,
     `GetSnapshot_VolumeTotals_AreNotInQuerySession`,
     `GetPerUserList_PagesAndSortsByBytesDesc`,
     `PerUser_UnknownBucketGrouped`,
     `PerUser_ZeroSizeRowsExcluded`,
     `GetPerUserUsage_ReturnsOnlySubjectBytes`,
     `GetPerUserUsage_ReturnsZeroForUnknownSubject`,
     `GetSnapshot_ZeroRows_ReturnsZeroMetrics`,
     `GetPerUserList_UnknownBucketIsOneRow`.
     (The **`GetSnapshot_VolumeTotals_AreNotInQuerySession`** test asserts
     the two volume reads are **not** in the `QuerySession` — a **named
     assertion** in the test body, not a separate test. The
     **`PerUser_UnknownBucketGrouped`** test asserts the
     `CreatedById == null` / `""` rows land in a **single** "unknown" bucket
     (C-SM·5). The **`PerUser_ZeroSizeRowsExcluded`** test asserts the
     `SizeBytes == 0` rows are **excluded** (C-SM·5). The
     **`GetPerUserUsage_ReturnsOnlySubjectBytes`** +
     **`GetPerUserUsage_ReturnsZeroForUnknownSubject`** tests pin the
     **M25 handoff** seam — the **`GetPerUserUsageBytesAsync`** shape
     (C-SM·7).)
  6. **`src/Kumunita.Core/DependencyInjection.cs`** — add
     `services.AddTransient<IStorageMetricsService>(sp => new
     StorageMetricsService(sp.GetRequiredService<IDocumentStore>(),
     sp.GetRequiredService<IMediaFileStore>()));` (the
     `UsageAnalyticsService` registration shape).

- **Rules:**
  - **`Core` stays HTTP-free** (C-SM·3; ADR 0006-D): no `IFormFile` /
    `Stream` / `ActionResult` in any new file.
  - **Read-only, zero writes** (C-SM·2): the service opens a `QuerySession`
    (a read) + calls two volume-stat methods (reads); it emits **zero**
    writes, **zero** `AccessAudit` rows, **zero** new documents.
  - **Do not touch `IMediaStore` / `MediaObject`** (C-SM·1).
  - **Do not add a Web surface or a Web test** in this unit (U5+).
- **Exit (the reliable test path — per the register's "Running the tests"):**

```powershell
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
```

Build green; the 10 new Core tests pass (the `dotnet exec` path reports
pass/fail — **not** `dotnet test`). Handoff note (5–6 lines,
`## U4 — service + Core tests`): (a) the 10 Core test names (by id) that
passed, (b) the `GetSnapshotAsync` read shape (the two catalog queries +
the two volume reads), (c) the **`GetPerUserUsageBytesAsync`** seam shape
(the M25 handoff — C-SM·7), (d) the `SizeBytes == 0` exclusion + the
`CreatedById == null` / `""` "unknown" bucket (C-SM·5), (e) the DI
registration line.

### U5 — Web: `AdminStorageMetricsController` + view model + view + nav row
- **Goal:** the **GlobalAdmin-gated** `/admin/storage` surface — a
  **read-only** view of the four metrics (total used, available,
  user-content used, per-user table) + the per-user table (paged, sorted
  by bytes desc) + one nav row on the `/admin` hub. Mirrors the M13
  `AdminAnalyticsController` (the `GlobalAdmin` gate + the `QuerySession`
  read + the "no audit row on a read" discipline). **No form, no write, no
  settings** — M25's U5/U6 own the set-lane.
- **Entry reads (3–5 files):**
  - `src/Kumunita.Web/Controllers/AdminAnalyticsController.cs` — the M13
    admin analytics surface to mirror (the `GlobalAdmin` gate + the
    `QuerySession` read + the "no audit row on a read" discipline).
  - `src/Kumunita.Web/Models/AnalyticsViewModel.cs` (or the equivalent) —
    the M13 view-model shape to mirror (the `AnalyticsViewModel` projection
    of the `UsageAnalyticsResult`).
  - `src/Kumunita.Web/Views/Admin/` — the existing admin view + layout
    style.
  - `src/Kumunita.Core/Usage/IStorageMetricsService.cs` (U4's seam).
  - `docs/design/m24-storage-metrics-design.md` §2.1 (the
    `IStorageMetricsService` read seam + the DTO shapes).
- **Deliverables (≤ 4 files):**
  1. **`src/Kumunita.Web/Controllers/AdminStorageMetricsController.cs`** —
     `[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]`;
     `GET /admin/storage?page=1` → loads the snapshot
     (`GetSnapshotAsync()`) + the per-user list
     (`GetPerUserListAsync(page, 25)`) + renders the view. **Read-only**
     (no form, no write, no POST action). **Zero** `AccessAudit` rows (C-SM·6).
  2. **`src/Kumunita.Web/Models/AdminStorageMetricsViewModel.cs`** — the
     view model: the `StorageMetricsSnapshot` projection (total used,
     available, user-content used, total unique files, total distinct
     users, as-of) + the `PerUserStoragePage` projection (items, total
     users, page, has-more).
  3. **`src/Kumunita.Web/Views/Admin/StorageMetrics.cshtml`** — the four
     metrics (total used, available, user-content used, as-of) + the
     per-user table (paged, sorted by bytes desc) + the "unknown / not
     captured" bucket (C-SM·5) + the `HasMore` / `Prev` / `Next` pager
     (the M7 `HasMore` discipline).
  4. **`src/Kumunita.Web/Views/Admin/Index.cshtml`** (or the `/admin` hub)
     — add the "Storage" nav row (mirror the existing section links).
- **Rules:**
  - **`GlobalAdmin`-gated** (C-SM·6). Non-GlobalAdmins must not reach
    `/admin/storage` (U6's `AdminStorage_NonGlobalAdmin_Forbidden` test
    pins this at the Web level; U5 just applies the attribute).
  - The snapshot + per-user-list reads are **read-only** (a
    `QuerySession`) and emit **no** `AccessAudit` row (C-SM·6 — the admin
    surface is not an audience-restricted read; M24 has no export lane in
    v1, so zero rows).
  - **No form, no write, no POST action** in this unit (M25's U5/U6 own the
    set-lane).
  - **No new test** in this unit (U6's `AdminStorageMetricsControllerTests`
    are the first M24 Web tests).
- **Exit:** `run_build` on `Kumunita.Web` green; `GET /admin/storage`
  renders (GlobalAdmin session). **No new test** (U6's Web tests are the
  first M24 Web tests). Handoff note (4–5 lines, `## U5 — admin GET
  surface`): (a) the `AdminStorageMetricsController` route + the view model
  shape, (b) the nav row added (file + line), (c) the four metrics + the
  per-user table + the "unknown" bucket (C-SM·5) + the `HasMore` / `Prev`
  / `Next` pager (C-SM·4), (d) the **zero** `AccessAudit` rows (C-SM·6
  confirmed).

### U6 — Web: the 5 pinned Web tests
- **Goal:** implement the 5 Web tests from the design doc §2.5 (the
  `AdminStorageMetricsControllerTests`) in
  `tests/Kumunita.Web.Tests/AdminStorageMetricsControllerTests.cs`. **This
  unit does NOT run the acceptance gate** (that is U7). **No design-doc
  edits** (U7).
- **Entry reads (3–5 files):**
  - `docs/design/m24-storage-metrics-design.md` §2.5 (the 5 Web test
    names, exact — the *primary* source for this unit).
  - `tests/Kumunita.Web.Tests/AdminAnalyticsControllerTests.cs` (or the
    equivalent M13 admin test file) — the M13 admin test shape to mirror
    (the test harness, the fixture, the assertion style).
  - `src/Kumunita.Web/Controllers/AdminStorageMetricsController.cs`
    (U5's controller).
  - `src/Kumunita.Web/Models/AdminStorageMetricsViewModel.cs` (U5's view
    model).
- **Deliverables (1 file, new):**
  **`tests/Kumunita.Web.Tests/AdminStorageMetricsControllerTests.cs`** —
  the 5 pinned Web tests (§2.5, items 11–15):
  11. `AdminStorage_GlobalAdmin_Allowed` — a GlobalAdmin signs in →
      `GET /admin/storage` → **200** + the four metrics render.
  12. `AdminStorage_NonGlobalAdmin_Forbidden` — a non-GlobalAdmin signs
      in → `GET /admin/storage` → **403**.
  13. `AdminStorage_NoAccessAuditRow` — a GlobalAdmin signs in →
      `GET /admin/storage` → **zero** `AccessAudit` rows (C-SM·6).
  14. `AdminStorage_PerUserTable_RendersWithHasMore` — plant 30
      `MediaObject` rows across 3 users → `GET /admin/storage?page=1` →
      the per-user table renders + the `HasMore` is **true** + the
      `Next` link is present (C-SM·4).
  15. `AdminStorage_FourMetrics_Render` — plant 5 `MediaObject` rows
      across 2 users → `GET /admin/storage` → the four metrics (total
      used, available, user-content used, as-of) render + the
      per-user table has 2 rows + the "unknown" bucket is absent
      (C-SM·2/4/5).
- **Rules:**
  - **`GlobalAdmin`-gated** (C-SM·6) — the `AdminStorage_GlobalAdmin_Allowed`
    + `AdminStorage_NonGlobalAdmin_Forbidden` tests pin the gate at the
    Web level.
  - The `AdminStorage_NoAccessAuditRow` test asserts **zero** `AccessAudit`
    rows (C-SM·6 — M24 has no export lane in v1, so zero rows).
  - The `AdminStorage_PerUserTable_RendersWithHasMore` test pins the
    M7 `HasMore` discipline (C-SM·4).
  - The `AdminStorage_FourMetrics_Render` test pins the four metrics (C-SM·2/4)
    + the "unknown" bucket absence (C-SM·5).
- **Exit:** `run_build` green. `run_test(s)` on
  `AdminStorageMetricsControllerTests` reports **5 tests discovered, 5
  executed** — the pass/red status of each is recorded (for U7's gate).
  **No gate recorded** (U7), **no design-doc edits** (U7). Handoff note
  (3 lines, `## U6 — Web tests (5)`): (a) the test file path, (b) the 5
  names (verbatim), (c) the 5 pass/red counts (for U7 to consume).

### U7 — Run + record the M24 acceptance gate
- **Goal:** execute and **record** the three-test acceptance gate
  (closed-loop / handoff / part-vs-whole) from the design doc §2.6,
  *using* U6's 5 Web tests as the part-vs-whole evidence. The **e2e** is
  also executed in this unit (the M13 three-test shape: (a) closed loop —
  a GlobalAdmin signs in, `/admin/storage` renders the four metrics +
  the per-user table; (b) handoff — a non-GlobalAdmin is 403 from
  `/admin/storage`; (c) part-vs-whole — the 15-test list (10 Core + 5
  Web) passes together) and recorded alongside the gate. **If the e2e
  runtime (Postgres-boot + token-channel) is not yet present, the spec is
  authored (mirroring M13's U13) and *not run* — the gap is recorded in the
  design doc and the next unit who lands the runtime records the pass
  count.**
- **Entry reads (3–5 files):**
  - `docs/design/m24-storage-metrics-design.md` §2.6 (the gate's three test
    names and their definitions) + §2.7 (the drift-guard).
  - `docs/plans-milestones/in-progress/m24-handoff-notes.md` (U6's section
    — the 5 Web test + the 10 Core test results that the gate *references*).
  - `tests/Kumunita.Web.Tests/AdminStorageMetricsControllerTests.cs` (U6's
    5 Web tests the gate's part-vs-whole invokes).
  - `tests/Kumunita.Core.Tests/Usage/StorageMetricsTests.cs` (U4's 10 Core
    tests the gate's part-vs-whole invokes).
- **Deliverables (1 file, modify):**
  **`docs/design/m24-storage-metrics-design.md`** — append
  `### Run result (M24 acceptance gate — <date>)`: the three test names,
  their pass/red status, the 15-test count (10 Core + 5 Web), the e2e
  status (from U6/U7), and one line per any `## U<m> — Drift pause` section
  in the handoff note (each resolved or still open). **No code, no build.**
- **Exit:** the gate section is present and consistent with U6's results.
  Handoff note: 4–5 lines starting `## U7 — gate recorded` — the three test
  names + pass counts + the date + any still-open drift.

### U8 — Close: `Milestones.cs` + `MilestonesTests.cs` status flip + ADR + OPS/README docs + design-doc close section + handoff close + move to `done/m24/`
- **Goal:** the **final** M24 unit. (1) Flip `Milestones.cs` — M24
  `StatusNext` → `StatusDone`; M25 `StatusPlanned` → `StatusNext`. (2)
  Update `MilestonesTests.cs` — `Shipped_Milestones_Are_Marked_Done` gains
  M24; `M24_Is_The_Single_InProgress_Milestone` is **re-pointed** to M25
  (the single in-progress milestone is now M25, not M24). (3) Author the
  ADR (the next free number — **0134**, after 0133 dark theme) —
  `docs/adr/0134-storage-metrics.md`. (4) Update `README.md` Roadmap +
  `docs/STATUS.md` + `docs/OPS.md` (the storage-metrics row). (5) Append
  the `## M24 — Closed (recorded)` section to the design doc. (6) Append
  the `## Summary` section to the handoff note. (7) Move the plan +
  handoff note to `done/m24/`. **Closing M24 unblocks M25 U1/U2** (which
  are currently `BLOCKED` on exactly this precondition — see
  `in-progress/m25-handoff-notes.md`).
- **Entry reads (3–5 files):**
  - `src/Kumunita.Web/Milestones.cs` — the M24/M25 status (the C-SM·7
    precondition at close time).
  - `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the two tests to update
    (`Shipped_Milestones_Are_Marked_Done` +
    `M24_Is_The_Single_InProgress_Milestone`).
  - `docs/adr/0133-dark-theme-forest.md` — the current highest ADR number
    (0133) — M24's ADR is **0134** (the next free).
  - `docs/README.md` + `docs/STATUS.md` + `docs/OPS.md` — the
    Roadmap / STATUS / OPS rows to update.
  - `docs/design/m24-storage-metrics-design.md` (the full design doc — the
    `## M24 — Closed (recorded)` section appends here).
  - `docs/plans-milestones/in-progress/m24-handoff-notes.md` (the full
    handoff note — the `## Summary` section appends here).
  - `docs/plans-milestones/in-progress/m25-handoff-notes.md` — the M25 U1
    Drift pause (the **BLOCKED** state this unit **unblocks**).
- **Deliverables (≤ 7 files, modified + 1 new + 2 moved):**
  1. **`src/Kumunita.Web/Milestones.cs`** — flip M24 `StatusNext` →
     `StatusDone`; M25 `StatusPlanned` → `StatusNext`. The order is
     **unchanged** (`…"M23","M22","M24","M25","M26","M27","M28"` — the
     "named lane, not a renumber" precedent; M24 is a milestone, M25 stays
     M25).
  2. **`tests/Kumunita.Web.Tests/MilestonesTests.cs`** — update
     `Shipped_Milestones_Are_Marked_Done` (gains M24); update
     `M24_Is_The_Single_InProgress_Milestone` (re-point to M25 — the
     single in-progress milestone is now M25, not M24; the
     `M24` / `M25` / `M26` / `M27` / `M28` assertions are updated to
     match).
  3. **`docs/adr/0134-storage-metrics.md`** — the ADR (the next free
     number — **0134**, after 0133 dark theme). Sections: **Context**
     (M13 surfaces usage analytics; M24 surfaces storage metrics; the
     four questions the M24 title names), **Decision** (the
     `IStorageMetricsService` seam + the two `IMediaFileStore` ADDs +
     the `/admin/storage` surface + the `GlobalAdmin` gate + the "no
     audit row on a read" discipline + the **M25 handoff**
     `GetPerUserUsageBytesAsync` seam), **Consequences** (the **M25
     handoff** is a **closed-loop** artifact — M25's U4
     `IStorageSettingsService.GetPerUserUsageBytesAsync` **delegates** to
     M24's `IStorageMetricsService.GetPerUserUsageBytesAsync`; the
     **`SizeBytes == 0` exclusion** + the **`CreatedById == null` /
     `""` "unknown" bucket**; the **`IMediaStore` + `MediaObject` are
     frozen** (C-SM·1); the **CSV export lane** + the **time-series
     lane** are **named non-decisions** (future lanes); the **M26
     sort-lane** hook — `?sort=` + `?dir=` params on `/admin/storage`).
  4. **`README.md`** — the Roadmap: M24 → **Done**, M25 → **In progress**
     (the two milestones' statuses flip; the order is unchanged).
  5. **`docs/STATUS.md`** — the M24 line flips to done + M25 is named the
     single in-progress (the ADR 0132/0133 close precedent: "M24 → done,
     M25 the single in-progress").
  6. **`docs/OPS.md`** — the storage-metrics row (the `/admin/storage`
     surface + the two volume-stat reads + the "no audit row on a read"
     discipline).
  7. **`docs/design/m24-storage-metrics-design.md`** — append
     `## M24 — Closed (recorded)` — the three tests (from U7's record),
     the `Milestones.cs` flip (from U8), the ADR 0134 + the M25 handoff
     (the `GetPerUserUsageBytesAsync` seam — **C-SM·7**). **No code, no
     build.**
  8. **`docs/plans-milestones/in-progress/m24-handoff-notes.md`** — append
     `## Summary` — a table of the shipped units (U1–U8), with their
     one-liner goal + test count + any deviations + the **M25 handoff**
     note (the `GetPerUserUsageBytesAsync` seam — **C-SM·7**).
  9. **Move** `docs/plans-milestones/plan-m24-storage-metrics.md` →
     `docs/plans-milestones/done/m24/plan-m24-storage-metrics.md` +
     `docs/plans-milestones/in-progress/m24-handoff-notes.md` →
     `docs/plans-milestones/done/m24/m24-handoff-notes.md`. **Also move**
     the 8 unit plan files (`m24-u01.md` … `m24-u08.md`) from
     `in-progress/` → `done/m24/`.
- **Exit:** `run_build` green; `run_test(s)` on `MilestonesTests` reports
  **4 tests discovered, 4 executed** (the two updated tests + the
  unchanged `Roadmap_Covers_M0_Through_M28_Plus_Named_Lanes_In_Order` +
  `No_Milestone_Has_Blank_Title`). **The M25 U1 Drift pause is
  unblocked** (the `Milestones.cs` flip + the `MilestonesTests.cs` update
  are the precondition M25 U1's Step 0 checks). Handoff note: the `##
  Summary` section is present. **The last handoff note U8 writes is for
  the M25 U1 agent** (the M25 U1 Drift pause is **resolved** by this
  unit's close).

> The reliable test path (xunit.v3 runner quirk) is documented once, mid-file,
> in the **Running the tests** section above — U4/U6/U8 each quote it.
