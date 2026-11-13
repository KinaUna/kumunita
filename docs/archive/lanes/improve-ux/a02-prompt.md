---
id: a02-prompt
title: "Prompt: IMPROVE-UX A02 — the resolve lane executes the outcome"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/adr/0024-author-soft-delete-lane.md
  - docs/adr/0006-module-boundary-contracts.md
  - docs/adr/0004-data-persistence-and-schema-evolution.md
  - docs/dev/testing-and-ci.md
lane: improve-ux
updated: 2026-11-10
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> **A02** — the second unit of the **admin/moderator** block and the
> heart of the moderation loop (A01–A06, the plan's "one story"). A01 made
> the reads honour `PostStatus` (the E1/E2 leak fix); **A02 makes the
> resolve lane actually execute the outcome** — `ReportOutcome.Hidden` →
> `HidePostAsync`, `ReportOutcome.Removed` → `RemovePostAsync`, in the
> **same session** as the resolve (one transaction, the existing audit
> rows unchanged, the M3b write-lane audit rows added). Before A02,
> resolving a report with `Hidden` recorded the outcome on the
> `Report` row but **changed nothing visible** — the post was still
> live in the feed and reachable by URL (the E1 finding: "the outcome
> picker performs the action" was the promise, not the reality).
> **Who is it for:** the next coding agent (fresh context). **When to
> read the rest:** when starting A02 (the moderation loop's core
> execution unit — A01 + R03 + R04 are all shipped, no blockers).

# Fresh-context prompt: IMPROVE-UX A02

You are continuing the Kumunita repository on `development` (HEAD
`980d73c8`, A01 landed — the reads honour `PostStatus`: feeds + search
exclude Hidden/Removed; the detail lanes serve the post to the author
+ standing moderator / break-glass, and return `Post = null` to any
other reader). The **entire resident block (R01–R21) is shipped**; the
**admin/moderator** block is now in progress, and **A02** is the next
unit. The plan card (in `plan-improve-ux.md`, the A02 row):

> A02 — Outcome executes the action: `Hidden` → `HidePostAsync`,
> `Removed` → `RemovePostAsync`, in the same session as resolve (one
> transaction, audit rows as today); post targets only; tests.
> **`// TODO ❓`** a **reply** report: hide the parent post (the
> existing comment's lever) or add a reply-level hide?
> Read first: `ReportIntake.cs:648-720`, `PostService.cs:1209-1290`.
> **Done when: resolving with Hidden hides the post.**

This is the **S**-severity loop closer. A01 made the reads honour
`PostStatus`; A02 makes the **write** lane fire — the outcome picker
on the resolve form (R04, already shipped) calls `HidePostAsync` /
`RemovePostAsync` from the resolve session so the post actually
disappears from the feed + detail (A01's read branch) and the
kiosk catalog. **Post targets only** (community posts + group posts);
event / project / board / todo targets record the outcome but do not
execute a hide/remove (those lanes are A04's scope — a `// TODO ❓`
per the plan).

## The state of the code (measured 2026-11-10, `development`)

- **`ReportOutcome`** (`src/Kumunita.Core/Posts/Report.cs:16`) — the
  D1 closed vocabulary: `NoAction`, `Hidden`, `Removed`, `Other`.
  Written by the R03 lane (`ResolveReportAsync`) when the caller
  supplies an outcome; read by the R05/R06/R07 surfaces.
- **`Report`** (`src/Kumunita.Core/Posts/Report.cs:50+`) — the target
  shape: `PostId` is set for **both** community-post and group-post
  reports (a community post has `TargetKind` null; a group post has
  `TargetKind = "grouppost"`). `ReplyId` is set when the report is
  against a reply (the parent post's `PostId` is still set). For
  event / project / board / todo targets, `TargetKind` is set to
  `"event"` / `"project"` / `"board"` / `"todo"` and `PostId` is
  **empty** (the target is `TargetId`).
- **`HidePostAsync` / `RemovePostAsync`**
  (`src/Kumunita.Core/Posts/PostService.cs:1253` / `:1290`) — the
  M3b `Moderate`-gated write lanes (F3/F4). Both take
  `(postId, actorId, IDocumentSession session)`, run their own
  `CanAsync(Moderate)` decision (the audit row), and write
  `Post.Status = Hidden` / `Removed` in the **caller's** session
  (one `SaveChangesAsync` — the C3 same-transaction lane). Both are
  already unit-tested (`PostServiceTests.cs` M3b·9–M3b·12).
  **Do not change their signatures or their write shape** — A02
  calls them, it does not modify them.
- **The frozen `ResolveReportAsync`**
  (`src/Kumunita.Core/Moderation/ModerationService.ReportIntake.cs:673`)
  — the R03 lane. In the `decision.Allowed` branch it writes
  `report.Status = "resolved"`, and (when the caller supplied an
  outcome or note) `report.Outcome` / `report.ResolutionNote` /
  `report.ResolvedAt` / `report.ResolvedBy`. It runs one
  `CanAsync(Moderate)` decision and stores one `AccessAudit` row —
  the R03 pin (the unit tests assert the audit row's field set is
  byte-identical to the pre-R03 lane). **A02 hooks in here**: after
  the `report.Outcome = outcome` write, add the hide/remove call in
  the **same session** (the `session` parameter — the C3
  same-transaction lane).
- **The `ModerationService`** already has `_postService` (or
  `IPostsService` / `IPostService`) injected — confirm the exact
  field name in `ModerationService.ReportIntake.cs` (the A02 hook
  calls the service instance it already holds). If it does not have
  the `PostService` / `HidePostAsync` / `RemovePostAsync` surface
  available, add the injection in the same commit (the
  `DependencyInjection.cs` registration + the constructor parameter
  + the `IModerationService` interface — the C3-03 extraction
  pattern, the `GroupPostService` precedent).

## The trap that will bite you — the "execute on the wrong target" leak

The `Report` row's `PostId` is set for **post and group-post**
targets (both are `Post` documents), but **not** for event / project
/ board / todo targets (those have `TargetId` set, `PostId` empty).
The `// TODO ❓` in the plan card says "post targets only" — so the
A02 hook must **check that `report.PostId` is non-empty** before
calling `HidePostAsync` / `RemovePostAsync`. An event report with
`outcome = Hidden` should record the outcome (the `Report.Outcome`
write) but **not** call `HidePostAsync` (there is no post to hide).
The A04 unit will add the per-target-kind execution for the other
surfaces; A02 is posts only.

Also: **do not add a new `UnhidePostAsync` in A02** — that is A03's
scope (the reversible lever). A02 is `Hidden` + `Removed` only. And
**do not change the R03 audit row shape** (the R03 pin: the
`AccessAudit` row's field set is byte-identical to the pre-R03
lane). The `HidePostAsync` / `RemovePostAsync` lanes write their
**own** audit rows (the M3b write-lane rows, `Action = Moderate`,
`TargetId = the post id`) — those are **additional** rows in the
same transaction, not a change to the resolve row.

## Before writing, read (in this order)

- **`AGENTS.md`** (repo root) and **`.github/copilot-instructions.md`**
  — the doctrine (the comment rule: what/when/throws + ADR number;
  the ratchet; the don't-pause-mid-task rule).
- **`plan-improve-ux.md`** — the Decisions table (D6 moderation), the
  A02 row, and the E1/E2 evidence rows (A02 closes the "the outcome
  picker performs the action" promise).
- **`src/Kumunita.Core/Posts/Report.cs:1-140`** — the
  `ReportOutcome` enum + the `Report` doc's `PostId` / `TargetKind`
  / `ReplyId` fields (the "post targets only" scope).
- **`src/Kumunita.Core/Moderation/ModerationService.ReportIntake.cs:648-740`**
  — the frozen `ResolveReportAsync` body (the A02 hook point: the
  `decision.Allowed` branch, after the `report.Outcome = outcome`
  write).
- **`src/Kumunita.Core/Posts/PostService.cs:1240-1300`** —
  `HidePostAsync` + `RemovePostAsync` (the A02 call targets —
  read-only, to confirm the signature + the C3 same-transaction
  lane).
- **`src/Kumunita.Core/Moderation/IModerationService.cs`** — the
  `ResolveReportAsync` surface (the committed overload at
  `:204-225` — the one the Web controller calls; A02 hooks in the
  **session-based** overload the committed one delegates to).
- **`tests/Kumunita.Core.Tests/ModerationServiceTests.cs`** — the
  existing R03 resolve-lane tests (the fixture, the `PostgresFixture`
  shape, the actor setup) — your new tests go here (or in
  `PostServiceTests.cs` if you find it more natural — the A01
  precedent put the read-lane tests in the `PostServiceTests` /
  `GroupPostServiceTests` files; the A02 write-lane tests can go
  in either, but the `ModerationServiceTests` file is the
  moderation-lane home).
- **`docs/dev/testing-and-ci.md`** — the xunit.v3 in-process
  `dotnet exec` invocation (the reliable runner).
- **`git status`** (A01's commit must be intact; you are on
  `development`).

## The work

1. **Hook the outcome execution into the frozen `ResolveReportAsync`.**
   In `ModerationService.ReportIntake.cs:673` (the session-based
   overload), inside the `decision.Allowed` branch, **after** the
   `report.Outcome = outcome` / `report.ResolvedAt` /
   `report.ResolvedBy` writes (so the outcome is recorded **before**
   the action runs — a denied action should not leave a phantom
   outcome without the post state):
   - `if (outcome == ReportOutcome.Hidden && !string.IsNullOrEmpty(report.PostId))
        await _postsService.HidePostAsync(report.PostId, globalAdminId, session);`
   - `else if (outcome == ReportOutcome.Removed && !string.IsNullOrEmpty(report.PostId))
        await _postsService.RemovePostAsync(report.PostId, globalAdminId, session);`
   - `NoAction` and `Other` → no execution (the outcome is recorded,
     the note is the explanation).
   - The `!string.IsNullOrEmpty(report.PostId)` guard is the
     "post targets only" scope (events / projects / boards / todos
     have empty `PostId` — A04's scope).
2. **Confirm the `PostService` injection.** If
   `ModerationService.ReportIntake.cs` does not already have the
   `PostService` (or `IPostsService`) injected, add it: the
   constructor parameter + the `DependencyInjection.cs` registration
   + the `IModerationService` interface (the C3-03 extraction
   pattern, the `GroupPostService` precedent). Check the existing
   field names first — the `ModerationService` may already hold a
   reference (the ADR 0173 `LoadReportTargetResourceAsync` helper
   loads `Post` documents, so it may have the `PostService` or the
   `IDocumentStore` already).
3. **The `// TODO ❓` (ask Per if you reach it):** the plan card says
   "post targets only" and flags the reply-report question. I read
   that as: **A02 hides/removes the parent post for a reply report
   too** (the `Report.PostId` is set for a reply report — the
   existing comment's lever, the ADR 0023 reply-report-target shape).
   If Per intends a **reply-level** hide (a new `PostReply.Status`
   field + `HideReplyAsync` lane), that is A04's scope — flag it
   before writing, don't guess. **Do not add a reply-level hide in
   A02.**
4. **Tests (the DoD: "resolving with Hidden hides the post").** In
   `ModerationServiceTests.cs` (or `PostServiceTests.cs` — pick the
   moderation-lane home):
   - resolving a **post-target** report with `Hidden` → the post's
     `Status` flips to `PostStatus.Hidden` (the feed excludes it,
     the detail lane serves the author/moderator, the stranger gets
     `Post = null` — the A01 read branch is the enforcement);
   - resolving a **post-target** report with `Removed` → the post's
     `Status` flips to `PostStatus.Removed` (same read branches);
   - resolving a **post-target** report with `NoAction` → the post's
     `Status` is **unchanged** (still `Active`);
   - resolving an **event-target** report with `Hidden` → the
     `Report.Outcome` is recorded (`Hidden`) but **no** `Post` is
     touched (the `PostId` is empty — the "post targets only" guard);
   - the **audit rows**: the resolve `AccessAudit` row is
     byte-identical to the R03 shape (the R03 pin — the unit tests
     assert this); **plus** the `HidePostAsync` / `RemovePostAsync`
     write-lane audit rows (`Action = Moderate`, `TargetId = the
     post id`) — both in the same transaction.
5. **Verify the gates.** `python3 tools/ux/ux-measure.py`
   (report-only) and `--check` must stay green — A02 is Core C# and
   does not touch the tracked UX counts. The
   `ux-measure.py` `bare_status` count is **708** (A01 did not
   change it); A02 should keep it there.

**`// TODO ❓` (ask Per if you reach it):** the reply-report
question (the plan card's flag). I read it as: **A02 hides/removes
the parent post for a reply report** (the `PostId` is set, the
existing comment's lever). If Per wants a reply-level hide, that is
A04's scope. Do not guess — flag it.

## Scope rules

- **The A02 hook in `ResolveReportAsync` + the tests.** Do not
  touch the write lanes (`HidePostAsync` / `RemovePostAsync`), the
  `ReportOutcome` enum, the `Report` doc, the resolve form (R04),
  or the R03 audit row shape. Do not add `UnhidePostAsync` (A03's
  scope) or a reply-level hide (A04's scope). Do not touch
  `admin.json`. The only files that should change:
  `ModerationService.ReportIntake.cs` (the hook),
  `DependencyInjection.cs` + `IModerationService.cs` (if the
  `PostService` injection is missing), and the test file(s).
- **One commit** (the house rule): the outcome-execution hook +
  the tests land in **one** `feat(ux):` commit
  (`feat(ux): the resolve lane executes the outcome — Hidden hides, Removed removes (IMPROVE-UX A02)`).
- **The `ux-measure.py --check` gate** stays green (A02 lowers
  nothing tracked; it adds no new Web surface).

**Before committing** (see `testing-and-ci.md` for the in-process
runner — the reliable path; `dotnet test` discovery is broken here):

```bash
dotnet build Kumunita.slnx -c Debug
python3 tools/ux/ux-measure.py --check     # must stay green (bare_status stays 708)
dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll \
  -class "Kumunita.Core.Tests.ModerationServiceTests"
# (or the class you chose for the A02 tests — confirm the name)
# + the A01 read-lane tests (the A02 hook + the A01 read branch
#   are the two halves of the moderation loop; run both to confirm
#   the loop closes end-to-end):
dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll \
  -class "Kumunita.Core.Tests.PostServiceTests" -method "*A01*"
dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll \
  -class "Kumunita.Core.Tests.GroupPostServiceTests" -method "*A01*"
```

If a test fails, read the error, adjust, continue. Note:
`Kumunita.Web.Tests` is expected to fail **3** known-reds (the two
`SecGate.DataClassCoverageTests` M2 gates +
`ImproveHarnessTests.CodeCheck_Gate_Passes`) — recorded drift (the
SEC-lane M2 + the ratchet), **not** regressions; don't "fix" them
(a different unit's scope).

Then report: the hook point (file:line in
`ModerationService.ReportIntake.cs`), the injection you added (if
any — the field name + the `DependencyInjection.cs` registration
line), the `// TODO ❓` (did you resolve it, and with whom — the
reply-report question), the test class name(s) + pass counts, the
`ux-measure.py --check` line, and the commit hash. Do not commit
unrelated work.
