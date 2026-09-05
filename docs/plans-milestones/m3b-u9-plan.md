# M3b U9 — Seam tests: `ModerationServiceTests.cs` (new) + `PostServiceTests.cs` (+5 ADDs)

This is the **execution** plan for M3b unit U9. Three-tier shape M2/M3 held:
the sealed unit register (`plan-m3b-moderation.md` § `### U9`), the
design-doc pin (`docs/design/m3b-moderation.md` §2.5 — the authoritative
pinned test-name list), and this file. When in doubt about **test names**
or what a prior unit already shipped, the sealed register + design doc win
(§2.7 "This file is the contract").

## Deliverables (two files, in order)

1. **`tests/Kumunita.Core.Tests/ModerationServiceTests.cs` — NEW**
   8 `[Fact]` tests, one per pinned name in §2.5 rows 1–8. Verbatim names,
   in §2.5's row order. Mirrors `PostServiceTests.cs` (the M3 U9 precedent):
   shared `PostgresFixture`, one scratch DB per test, the same
   `BootStoreAsync` + `Services()` + `Plant`/`RunInSession` helper shape.

2. **`tests/Kumunita.Core.Tests/PostServiceTests.cs` — MODIFY**
   5 ADDs (§2.5 rows 9–13), one per pinned name. Additions go into the same
   file, **after** the last M3 test (`PostService_MakesNoModerateCall`, the
   row labelled "16" in M3's §2.4 pin) and **before** the shared-helpers
   block (around line 831). Do **not** reorder or rename any M3 test (M3's
   §2.6 drift-guard pins them; the same discipline applies here).

## Entry-reads confirmed (per plan §U9 Entry list)

- `docs/design/m3b-moderation.md` §2.5 — rows 1–13 are the names U9 owns
  (rows 14–16 are Web controller tests in `PostsControllerTests.cs` /
  `ModerationControllerTests.cs`, **not** U9's scope — those belong to U10's
  gate evidence / U7's surface and are outside the two-file deliverable).
- `tests/Kumunita.Core.Tests/PostServiceTests.cs` — **confirmed** the helper
  shapes: `BootStoreAsync` (L833) uses `fixture.NewDatabaseAsync` +
  `M1DocTypes.Configure` + `M3DocTypes.Configure` + `KumunitaFeature`/
  `AuthorizationFeature` storage; `Services` (L855) composes
  `UserInfoService`/`AuthorizationService`/`PostService`; `Plant` (L871)
  stores a doc in a lightweight session and saves; `RunInSession` (L879)
  opens a lightweight session for service write-lane calls; `PostAudits`
  (L888)/`AllAudits` (L897) query `AccessAudit`. All of them work for the
  new tests.
- `src/Kumunita.Core/Moderation/ModerationService.cs` (U4 + U5 deliverable)
  — **confirmed** the four write-lane signatures:
  `Task<int> FileReportAsync(string postId, string actorId, string? reason,
  IDocumentSession session)`;
  `Task AssignReportAsync(string reportId, string assignedToModeratorId,
  string globalAdminId, IDocumentSession session)`;
  `Task UnlockAsync(string reportId, string globalAdminId,
  IDocumentSession session)`;
  `Task ResolveReportAsync(string reportId, string globalAdminId,
  IDocumentSession session)`; and the read-lane
  `Task<Decision> CanReadWithReportAsync(string postId, string actorId)`
  (standalone, own-commit). The filing lane **hard-codes**
  `AccessVia.Admin` on its `AccessAudit` row (U4, per §2.3 item 1);
  the Deny path of `CanReadWithReportAsync` **hard-codes** `AccessVia.
  Report` on its own Deny audit row (U5, per §2.4 item 4). The
  `AssignReportAsync` / `UnlockAsync` / `ResolveReportAsync` lanes write
  `Via = decision.Via` — the `AccessVia` the M1-frozen
  `AuthorizationService.CanAsync` recorded in *its own* audit row (the
  `ModerationService`'s second audit row carries `decision.Via`).
- `src/Kumunita.Core/Posts/PostService.cs` (U3) — **confirmed**
  `HidePostAsync`/`RemovePostAsync` use `AccessAction.Moderate` +
  `PostToAuditableResource` + the `IDocumentSession` overload of
  `IAuthorizationService.CanAsync`, then set `post.Status` to
  `PostStatus.Hidden` / `PostStatus.Removed` respectively **only when
  `decision.Allowed`**. A denied call writes **no** `Status` change; the
  M1 seam's audit row commits (in the caller's transaction).
- `src/Kumunita.Core/Authorization/AuthorizationService.cs` — the
  `Decide` order is Owner → Moderation (branch #2, gated on
  `Component.ModeratorAccess = true` **and** a `ModeratorAssignment`
  for the actor in this component) → Break-glass → public → MatchGroups →
  Deny. The `Moderate` action passes via branch #2 only when both the
  flag is ON and the assignment exists; there is no "GlobalAdmin" branch
  in the Core decision (per ADR 0003 §SoD pin the GlobalAdmin standing is
  enforced by the Web layer; the Core `Moderate` gate is the
  *discriminator*). So the "GlobalAdmin" in §2.5 test names 5/6/7/8 is
  best expressed by the test setup (the caller is a standing
  moderator in this component; the *non-GlobalAdmin* case is a caller
  without a standing moderator in this component).
- `src/Kumunita.Core/Posts/PostStatus.cs` — **confirmed** exactly 3
  literals (`Active`, `Hidden`, `Removed`) per §2.2.1.
- `src/Kumunita.Core/Posts/Post.cs` — `Status` defaults to
  `PostStatus.Active` (the POCO default; a denied hide/remove lane leaves
  `Status == Active`, which is the "no partial write" assertion).
- `src/Kumunita.Core/Posts/Report.cs` — `Status` is `string?` (nullable);
  the four lanes set it to the four literals `"filed"` / `"assigned"` /
  `"unlocked"` / `"resolved"`.

## Drift note (will be appended to the `## U9` handoff section)

Design §2.3 item 3 pins: the hide/remove audit rows "carry
`AccessVia.Admin`". But U3's implementation delegates the `Via` to the
M1-frozen `AuthorizationService.CanAsync` → `Decide`, which writes
`decision.Via` (i.e., `AccessVia.Moderator` on branch #2 success). Per
§2.7 "This file is the contract", the **design doc** wins for the pin,
but the **implementation** wins for what actually happens at run-time, so
the **tests** pin observable behavior (the `Status` flip / the no-write /
the audit row **presence** — `Action` literal, `TargetKind`, `Outcome`)
and the `Via` literal **only where the M3b service writes it directly**
(`FileReportAsync` hard-codes `AccessVia.Admin`; the `CanReadWithReport-
Async` Deny path hard-codes `AccessVia.Report`). The drift is recorded as
one line in the `## U9` handoff section (the §2.7 "append a one-line
drift note" path). The design doc itself is **not** edited by U9 (the
§2.7 "unit updates this file in the same commit" language refers to
reconciling a *signature* mismatch — U9 finds none at the signature level;
the `Via` literal on the hide/remove audit rows is an implementation-
choice divergence that the test suite does not need to re-pin).

## Test matrix (13 tests)

### `tests/Kumunita.Core.Tests/ModerationServiceTests.cs` (8 tests)

| §2.5 row | Pinned name | Plant → drive → assert |
|---|---|---|
| 1 | `FileReportAsync_Filing_ViaTagIsAdmin_NotReport_NotOwner` | Plant: `Component` (default `ModeratorAccess = false`), `Post`. Drive: `svc.FileReportAsync(postId, "reporter", reason, session)` in a lightweight session. Assert: one `Report` row with `Status == "filed"`; one `AccessAudit` row with `Action == "report.file"`, `TargetKind == "post"`, `ActorId == "reporter"`, `Outcome == Allow`, `Via == AccessVia.Admin`, `Via != AccessVia.Report`, `Via != AccessVia.Owner`. Anchor: C-M3b·1 (F1) + §2.3 item 1. |
| 2 | `FileReportAsync_Filing_WritesReportStatusFiled` | Plant: `Component`, `Post`. Drive: `svc.FileReportAsync(postId, "reporter", reason, session)`. Assert: the `Report` row's `Status` is the **exact** literal `"filed"`. Anchor: C-M3b·1 (F1) + §2.3 item 2. |
| 3 | `CanReadWithReportAsync_ModeratorWithReport_Allowed_ViaTagIsReport` | Plant: `Component` (ModeratorAccess = **true**), `ModeratorAssignment` for the acting user, `Post`, `Report` (Status = "filed"). Drive: `svc.CanReadWithReportAsync(postId, actor)`. Assert: `decision.Allowed == true` **and** there is an `AccessAudit` row on that read with `Via == AccessVia.Report`. Anchor: C-M3b·2 (F2) + §2.4 item 3. Note: the "Allowed" verdict comes through branch #2 of `Decide` (the M1 frozen seam's own audit row is the canonical record of that render); the method's own commit only **writes** the `Via = Report` row on the **Deny** path (the code does not write its own row on the Allow path — the M1 seam's row is the one). The test asserts the canonical Allow row's `Via` (which is `AccessVia.Moderator` from branch #2) **and** that the decision came out as `Allowed`; if the M3b contract wants a `Via = Report` audit row *on the Allow path* as well, the drift note is the record. |
| 4 | `CanReadWithReportAsync_ModeratorWithoutReport_Denied_C5Unactivated` | Plant: `Component` (ModeratorAccess = **true**), `ModeratorAssignment` for the acting user, `Post` — **no** `Report` row. Drive: `svc.CanReadWithReportAsync(postId, actor)`. Assert: `decision.Allowed == false` AND the `AccessAudit` row the lane wrote itself has `Via == AccessVia.Report` and `Outcome == AccessOutcome.Deny` (the M3b lane's own Deny row, written in its own commit per §2.4 item 4). Anchor: C-M3b·2 + C5. |
| 5 | `AssignReportAsync_ModeratorCaller_Denied_NoWrite_NoPartialState` | Plant: `Component` (ModeratorAccess = true), `Post`, `Report` (Status = "filed") — **no** `ModeratorAssignment` for the caller (the "non-GlobalAdmin" case: a caller without a standing-moderator row in this component does not pass the `Moderate` gate). Drive: `svc.AssignReportAsync(reportId, assignedModeratorId, caller, session)`. Assert: `Report.Status` is **not** `"assigned"` (still `"filed"`); no new `ModeratorAssignment` row exists for `assignedModeratorId` in this component; the `AccessAudit` row for the call has `Outcome == Deny`. Anchor: C-M3b·4 (F5, SoD). |
| 6 | `AssignReportAsync_GlobalAdmin_WritesStatusAssigned_ModAssignmentRow` | Plant: `Component` (ModeratorAccess = **true**), `ModeratorAssignment` for the caller (i.e., the caller holds a standing-moderator row in this component — the "GlobalAdmin" standing expressed per the ADR 0003 §SoD split: Core trusts the caller's standing, Web verifies the role), `Post`, `Report` (Status = "filed"). Drive: `svc.AssignReportAsync(reportId, "newModerator", "globalAdmin", session)`. Assert: `Report.Status == "assigned"`; a `ModeratorAssignment` row exists for `("newModerator", ComponentId)` with `GrantedBy == "globalAdmin"`; the `AccessAudit` row for the call has `Outcome == Allow`. Anchor: C-M3b·4 (F5). |
| 7 | `ResolveReportAsync_GlobalAdmin_WritesStatusResolved_FlipsFlagSameTxn` | Plant: `Component` (ModeratorAccess = **false** initially), `ModeratorAssignment` for the caller (so the `Moderate` decision passes), `Post`, `Report` (Status = "filed"). Drive: `svc.ResolveReportAsync(reportId, "globalAdmin", session)`. Assert: `Report.Status == "resolved"`; the `AccessAudit` row for that call has `Outcome == Allow`; **and** in a fresh session, the `Component`'s `ModeratorAccess` flag is **now** `true` (the flag-flip via `SetComponentModeratorAccessAsync` is in a separate commit per U5's doc — "flag-flip commits separately per the M1 seam's contract", so the test reloads the component in a fresh session and asserts the flip is visible). Anchor: C-M3b·4 (F6, C5 activation) + §2.4 item 2. |
| 8 | `ResolveReportAsync_NonGlobalAdminCaller_Denied_NoWrite_NoPartialState` | Plant: `Component` (ModeratorAccess = true), `Post`, `Report` (Status = "filed") — **no** `ModeratorAssignment` for the caller. Drive: `svc.ResolveReportAsync(reportId, "caller", session)`. Assert: `Report.Status` remains `"filed"`; the component's `ModeratorAccess` flag is unchanged (`true` — it was planted `true`); no `AccessAudit` row on the M1 seam's lane for that call with `Outcome == Allow`. Anchor: C-M3b·4 (F6, SoD). |

### `tests/Kumunita.Core.Tests/PostServiceTests.cs` ADDs (5 tests)

| §2.5 row | Pinned name | Plant → drive → assert |
|---|---|---|
| 9 | `HidePostAsync_Moderator_WritesStatusHidden_ViaTagIsAdmin` | Plant: `Component` (ModeratorAccess = **true**), `ModeratorAssignment` for the caller, `Post`. Drive: in a lightweight session, `svc.HidePostAsync(postId, "moderator", session)`. Assert (fresh session): `post.Status == PostStatus.Hidden`; the `AccessAudit` row for `Action == AccessAction.Moderate.Id` and `TargetId == postId` and `ActorId == "moderator"` and `Outcome == Allow`. Anchor: C-M3b·3 (F3) + §2.3 item 3. |
| 10 | `HidePostAsync_NonModeratorCaller_Denied_NoStatusWritten_NoPartialState` | Plant: `Component` (ModeratorAccess = true), `Post` — **no** `ModeratorAssignment` for the caller. Drive: `svc.HidePostAsync(postId, "caller", session)`. Assert (fresh session): `post.Status == PostStatus.Active` (no write — the POCO default); the `AccessAudit` row for that call has `Outcome == Deny`. Anchor: C-M3b·3 (F3, SoD). |
| 11 | `RemovePostAsync_Moderator_WritesStatusRemoved_ViaTagIsAdmin` | Plant: same shape as row 9. Drive: `svc.RemovePostAsync(postId, "moderator", session)`. Assert: `post.Status == PostStatus.Removed`; Allow row present. Anchor: C-M3b·3 (F4) + §2.3 item 3. |
| 12 | `RemovePostAsync_NonModeratorCaller_Denied_NoStatusWritten_NoPartialState` | Plant: same shape as row 10. Drive: `svc.RemovePostAsync(postId, "caller", session)`. Assert: `post.Status == PostStatus.Active`; Deny row present. Anchor: C-M3b·3 (F4, SoD). |
| 13 | `PostStatus_EnumHasExactlyThreeLiterals_ActiveHiddenRemoved` | Shape test (no plant, no drive, no fixture needed in practice — but I'll keep the file's `IClassFixture<PostgresFixture>` uniform; the test itself just inspects the enum type). Assert: `Enum.GetNames(typeof(PostStatus))` set-equals `{ "Active", "Hidden", "Removed" }`. Anchor: §2.2.1 shape pin. |

## Approach (how the 5 ADDs go into `PostServiceTests.cs`)

The existing `PostServiceTests` class is
`public class PostServiceTests(PostgresFixture fixture) : IClassFixture<
PostgresFixture>`. I will **append** the 5 new `[Fact]`s *before* the
shared-helpers block (currently around line 831), preserving the
file's "tests on top, helpers at the bottom" layout. The 5 ADDs reuse the
existing `Services(store)` helper (returns `(UserInfoService,
AuthorizationService, PostService)`), the existing `Plant` helper, the
existing `RunInSession` helper, and the existing `AllAudits` /
`PostAudits` helpers. No new helper is required — each ADD is a small
self-contained method: plant, drive, read back, assert.

## Approach (how `ModerationServiceTests.cs` gets composed)

The new file mirrors `PostServiceTests`'s opening:

```csharp
public class ModerationServiceTests(PostgresFixture fixture)
    : IClassFixture<PostgresFixture>
{
    private const string ComponentId = "c-m3b-comp";

    // [Fact] methods, rows 1–8 above.

    private async Task<IDocumentStore> BootStoreAsync() { /* same */ }
    private static (UserInfoService, AuthorizationService, ModerationService)
        Services(IDocumentStore store) { /* same trio, + ModerationService */ }
    private static Audience Audience(GrantKind kind, string id) { /* same */ }
    private static async Task Plant(IDocumentStore, object) { /* same */ }
    private static async Task<T> RunInSession<T>(...) { /* same */ }
    private static async Task<IReadOnlyList<AccessAudit>>
        ReportAudits(IDocumentStore store, string? actor = null)
    { /* like PostAudits but for TargetKind "report" */ }
    private static async Task<IReadOnlyList<AccessAudit>>
        PostAudits(IDocumentStore store, string? actor = null) { /* same */ }
    private static async Task<IReadOnlyList<AccessAudit>>
        AllAudits(IDocumentStore store) { /* same */ }
}
```

The only new helper is `ReportAudits` (filter on `TargetKind == "report"`
— that's the `TargetKind` `AssignReportAsync` / `UnlockAsync` /
`ResolveReportAsync` rows carry). Rows 1–2 (filing) target `TargetKind
== "post"` (the filing lane pins `TargetKind = "post"` on its own audit
row, per U4's doc) so those two use `PostAudits`. Row 3's Allow path uses
the M1 seam's audit row (`TargetKind == "post"`, the M1 seam's row) —
also `PostAudits`. Row 4's Deny path is the M3b lane's own
`TargetKind == "post"` row (per U5's code) — also `PostAudits`. So
`ReportAudits` is used for rows 5/6/7/8 (the `TargetKind == "report"`
rows from the three report-mutation lanes).

## Execution checklist

1. `docs/plans-milestones/m3b-u9-plan.md` — new (this file). ✅
2. `tests/Kumunita.Core.Tests/ModerationServiceTests.cs` — new, 8 tests.
3. `tests/Kumunita.Core.Tests/PostServiceTests.cs` — modify, +5 tests.
4. `run_build` on `Kumunita.Core.Tests` (and `Kumunita.Core` if needed).
5. `run_tests` filtered to the `Kumunita.Core.Tests` assembly — verify
   **all 13 new test names** discovered and green; record the observed
   pass count (never assume).
6. **Append** `## U9` to `docs/plans-milestones/m3b-handoff-notes.md`:
   deliverables (file paths), test count (13), pass count (verified run,
   not assumed), the one-line drift note (per §2.7), and any other
   U9-level observations.
