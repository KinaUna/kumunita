---
id: r05-prompt
title: "Prompt: IMPROVE-UX R05 My-reports read seams"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/r04-prompt.md
  - docs/dev/testing-and-ci.md
lane: improve-ux
updated: 2026-11-09
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> R05. **Who is it for:** the next coding agent. **When to read the rest:**
> when starting R05 after R04.

# Fresh-context prompt: IMPROVE-UX R05

You are continuing the Kumunita repository on `development`. Implement
**IMPROVE-UX unit R05 only**: the two Core read seams that back the
"My reports" page (R06) — the reporter's content reports and the
reporter's platform-error issues. R01 (the status page), R02 (the
re-execute gate), R03 (the `Report` resolution shape + the optional
`outcome` / `resolutionNote` parameters on `ResolveReportAsync`), and
R04 (the resolve form) have already landed — build on them, don't
re-open them. Do not start R06 or any later unit.

Before editing, read [AGENTS.md](../../../../AGENTS.md),
[testing-and-ci.md](../../../dev/testing-and-ci.md), and the lane rules
plus the R05 card in [plan-improve-ux.md](plan-improve-ux.md). Check
`git status` and the current branch; preserve all existing work.

R05 is **Core-side** (`Kumunita.Core` read seams + tests) — no Web
controller, no view, no translation keys (R06 adds those). Two read
seams, one on each report surface:

- `IModerationService.ListMyReportsAsync(reporterId, ct)` — the
  reporter's content reports (where `Report.ReporterId == reporterId`),
  newest-first, each row carrying `Status`, `Outcome` (the R03 enum,
  `null` until resolved), `ResolutionNote` (≤500, `null` until
  resolved), `ResolvedAt` / `ResolvedBy`, `TargetKind`, `TargetTitle`
  (the target's title, **only if the reporter can still read it** —
  see below), and `At` / `Reason` for the row's context. The target
  title is resolved the same way `GetDetailAsync` does (post /
  event / project / board / todo / grouppost, the ADR 0173
  `TargetKind` switch), **but gated on the reporter's visibility**:
  for a **post** target (the original M3b shape, `TargetKind ==
  null`), call `Posts.PostService.GetPostAsync(postId, reporterId)`
  — a `null` `Post` in the `PostDetailResult` (denied, hidden,
  removed, or deleted) means the reporter can no longer read it →
  **no title** (the R05 DoD test "target no longer visible → no
  title"). For the other target kinds (event / project / board /
  todo / grouppost), resolve the title directly (the same
  `GetDetailAsync` logic) — their visibility rules are out of scope
  for R05 (the plan is posts-first, A02).

  **Type note (checked 2026-11-09):** `Posts.PostService` is a
  **concrete** class (there is **no** `IPostService`); it is
  registered `Transient` in
  `src/Kumunita.Core/Posts/DependencyInjection.cs` with a factory.
  Inject the concrete `Posts.PostService` into `ModerationService` —
  add it to the constructor as an **optional** parameter (the same
  `INotificationEmitter?` / `IUserInfoService?` optional-injection
  idiom the constructor already uses) so the existing positional
  `Services(store)` test call sites keep compiling, and the DI wiring
  passes the registered instance. `GetGroupPostAsync` lives on
  `IGroupPostService` (a separate service), **not** on `PostService`
  — do not look for it there; group-post visibility is out of R05's
  scope anyway.
- `IErrorReportService.ListMineAsync(subjectId, ct)` — the
  filer's platform-error issues (where `ErrorReport.SubjectId ==
  subjectId`), newest-first, each row carrying `Id`, `TriageStatus`,
  `Description`, `Created`, `Origin`, and the M32 resolution fields
  (`ResolvedAt` / `ResolvedBy` / `ResolutionNote`, all `null` until
  resolved). No visibility gate — the filer is the subject, and the
  fields are their own. Return the raw `ErrorReport` documents
  (they already carry every field; no read-model record needed).

Read model: add a `MyReportRow` record to
`ModerationReadModels.cs` (the same `required` + `string?` pattern as
the existing `ModerationQueueRow` / `ModerationDetailReadModel`).
Fields: `ReportId`, `PostId`, `ComponentId`, `Reason`, `Status`,
`At`, `TargetKind`, `TargetTitle` (nullable — `null` when the
reporter can no longer read the target), `Outcome` (`ReportOutcome?`),
`ResolutionNote` (`string?`), `ResolvedAt` (`DateTimeOffset?`),
`ResolvedBy` (`string?`). The `ErrorReport` doc is returned directly
— no new record.

R05 scope:

- **`IModerationService`**: add the `ListMyReportsAsync` method
  signature (the existing interface's read-seam pattern — a `Task<
  IReadOnlyList<MyReportRow>>` return, `reporterId` + `ct` params, a
  `<summary>` that names the "My reports" D1 shape and the target
  title's visibility gate).
- **`ModerationReadModels.cs`**: add the `MyReportRow` record.
- **`ModerationService`** (the partial in `ModerationService.cs`):
  implement `ListMyReportsAsync` — query `Report` where
  `ReporterId == reporterId` order `At` desc, resolve each row's
  target title (the `GetDetailAsync` target switch), gate the post
  title on `Posts.PostService.GetPostAsync` (a `null` `Post` in the
  `PostDetailResult` → null title), and return the rows. A **read,
  not a decision** — no `IAuthorizationService` call, no
  `AccessAudit` row (the `GetQueueAsync` / `GetDetailAsync`
  precedent: the report is the reporter's own; the read is a list,
  not a scoped-read gate).
- **`IErrorReportService`**: add the `ListMineAsync` method signature.
- **`ErrorReportService`**: implement `ListMineAsync` — query
  `ErrorReport` where `SubjectId == subjectId` order `Created` desc,
  return the rows. No visibility gate (the filer is the subject).
- **Tests** — two existing files:
  [`ModerationServiceTests.cs`](../../../../tests/Kumunita.Core.Tests/ModerationServiceTests.cs)
  for `ListMyReportsAsync`, and
  [`ErrorReportServiceTests.cs`](../../../../tests/Kumunita.Core.Tests/ErrorReportServiceTests.cs)
  (the `ListAsync` / `MarkResolvedAsync` harness lives there;
  [`ErrorReportResolveTests.cs`](../../../../tests/Kumunita.Core.Tests/ErrorReportResolveTests.cs)
  is the M32 resolution-shape companion) for `ListMineAsync`. The
  R05 DoD pins —
  (a) `ListMyReportsAsync` returns the reporter's rows with the
  resolution shape (status, outcome, note, resolved-by) populated
  after a resolve (re-use the R03 test's planted report shape, ≈
  line 776 of `ModerationServiceTests.cs`);
  (b) **target no longer visible → no title**: a post target that
  the reporter can no longer read (hidden / removed / deleted)
  yields `TargetTitle == null` (the R05 DoD test);
  (c) `ListMineAsync` returns the filer's issues with the M32
  resolution fields (status, resolution note) populated after a
  `MarkResolvedAsync`; (d) an empty list when the reporter / filer
  has no reports / issues (the `GetQueueAsync` / `ListAsync`
  empty-list precedent). Use each file's existing harness
  (`ModerationServiceTests` uses `BootStoreAsync` / `Services` /
  `Plant` / `RunInSession` over the shared `PostgresFixture`;
  `ErrorReportServiceTests` wires its own `DocumentStore.For`
  lambda over the same `postgres:18` — follow whichever the file
  already uses; do not introduce a new fixture).

Read first: [`IModerationService.cs`](../../../../src/Kumunita.Core/Moderation/IModerationService.cs)
(the `GetQueueAsync` / `GetDetailAsync` read-seam signatures + their
`<summary>` style), [`ModerationReadModels.cs`](../../../../src/Kumunita.Core/Moderation/ModerationReadModels.cs)
(the `required` record pattern), [`ModerationService.cs`](../../../../src/Kumunita.Core/Moderation/ModerationService.cs)
around `GetDetailAsync` (≈ line 465, the target-title switch to
re-use) and the class's constructor / field declarations (to see what
is already injected), [`Report.cs`](../../../../src/Kumunita.Core/Posts/Report.cs)
(the R03 resolution fields: `Outcome`, `ResolutionNote`,
`ResolvedAt`, `ResolvedBy`), [`IErrorReportService.cs`](../../../../src/Kumunita.Core/ErrorReports/IErrorReportService.cs)
(the existing read-seam signatures), [`ErrorReportService.cs`](../../../../src/Kumunita.Core/ErrorReports/ErrorReportService.cs)
(the implementation pattern), [`ErrorReport.cs`](../../../../src/Kumunita.Core/ErrorReports/ErrorReport.cs)
(the doc's field set, incl. the M32 resolution fields),
[`PostService.GetPostAsync`](../../../../src/Kumunita.Core/Posts/PostService.cs)
(≈ line 362, the visibility check to gate the post title on — the
concrete class, no interface),
[`Posts/DependencyInjection.cs`](../../../../src/Kumunita.Core/Posts/DependencyInjection.cs)
(the `Transient` `PostService` factory registration — the DI line to
mirror when you add the optional constructor param),
[`ModerationServiceTests.cs`](../../../../tests/Kumunita.Core.Tests/ModerationServiceTests.cs)
the R03 tests (≈ line 776, the `BootStoreAsync` / `Plant` /
`RunInSession` harness + the resolution-shape test as the template),
and [`ErrorReportServiceTests.cs`](../../../../tests/Kumunita.Core.Tests/ErrorReportServiceTests.cs)
(the `ListAsync` / `MarkResolvedAsync` harness for `ListMineAsync`).

Do **not**: add the "My reports" Web page or controller (that is
R06), add any notification (that is R07), add a link to the account
menu (that is R06), change the `Report` doc shape or the
`ResolveReportAsync` seam (R03 is frozen), or add a hide/remove action
on the target (that is A02). No new `DataClass` annotation or
`CoreDocTypes` registration is needed — R05 is a read over the
existing `Report` and `ErrorReport` documents.

Keep this to the two read seams and their read model (the two
interfaces, the `MyReportRow` record, the two implementations, and
the two existing test files — about six to seven files). Follow the
repository's existing read-seam, record, and test patterns.

Run the narrowest relevant tests (the `ModerationServiceTests` class
and the `ErrorReportServiceTests` class, both via the in-process
xunit.v3 runner — see testing-and-ci.md for the exact invocation),
then report changed files,
verification results, the commit hash, and any held questions. Make
one commit for R05; do not commit unrelated work.
