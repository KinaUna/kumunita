---
id: a04-prompt
title: "Prompt: IMPROVE-UX A04 — hide for the next target kind"
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
updated: 2026-11-11
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> **A04** — the fourth unit of the **admin/moderator** block and the unit
> that extends the A02 outcome-execution lane to the **next target kind**.
> A02 made the resolve lane execute the outcome, but **post-targets only**
> (the plan card's explicit "post targets only" guard): a report on an
> event / project / board / todo / grouppost records the outcome on the
> `Report` row and executes **nothing** — the A02 tests pin that (the
> `A02_…_EventTarget_RecordsOutcomeOnly` decoy-post shape). **A04 flips
> that pin for the next kind — group posts (the closest copy: a `Post` doc,
> the same `HidePostAsync` / `RemovePostAsync` write lanes, the same
> `PostToAuditableResource` gate).** Before A04, a moderator who resolved
> a group-post report with `Hidden` saw nothing happen — the outcome was
> recorded, the post stayed visible. **Who is it for:** the next coding
> agent (fresh context). **When to read the rest:** when starting A04
> (A01 + A02 + A03 are shipped, the read paths honour `PostStatus`, the
> resolve lane executes the outcome for post targets, no blockers).

# Fresh-context prompt: IMPROVE-UX A04

You are continuing the Kumunita repository on `development` (HEAD
`89c3843c`, A01 + A02 + A03 all landed — A01 makes the read paths honour
`PostStatus`; A02 makes the resolve lane execute the outcome for
**post-targets only**; A03 adds the `UnhidePostAsync` reversible lever +
the "Unhide" button on the resolved report page). **A04** is the next
unit. The plan card (in `plan-improve-ux.md`, the A04 row):

> A04 — Hide for the next target kind — `// TODO ❓` which first (group
> posts are the closest copy). Read first: `ReportTargetsController.cs`,
> `ReportIntake.cs` `LoadReportTargetResourceAsync`.
> **Done when: one more kind hideable; the rest listed as follow-ups.**

## The state of the code (measured 2026-11-11, `development`)

- **The A02 outcome-execution block** —
  `src/Kumunita.Core/Moderation/ModerationService.ReportIntake.cs:767-781`.
  The resolve lane (`ResolveReportAsync`) already:
  1. runs the `CanAsync(Moderate)` decision (the lane's own
     `report.resolve` audit row);
  2. records the `outcome` + `resolutionNote` + `ResolvedAt` /
     `ResolvedBy` onto the `Report` row (the R03 shape);
  3. **executes the outcome** — but only for **post-targets**:
     ```csharp
     if (outcome is (ReportOutcome.Hidden or ReportOutcome.Removed)
         && _posts is not null
         && !string.IsNullOrEmpty(report.PostId))      // ← the A04 trap
     {
         if (outcome == ReportOutcome.Hidden)
             await _posts.HidePostAsync(report.PostId, globalAdminId, session);
         else
             await _posts.RemovePostAsync(report.PostId, globalAdminId, session);
     }
     ```
     The `!string.IsNullOrEmpty(report.PostId)` guard is the **A04 trap**:
     for the ADR 0173 new-lane shapes (event / project / board / todo /
     grouppost), `Report.PostId` is `string.Empty` and the target's id
     lives in `Report.TargetId` (the `FileReportTargetAsync` lane sets it
     that way). So a grouppost-targeted report with `Hidden` records the
     outcome but **executes nothing** — that is A04's job to flip, for
     **grouppost only** (the other kinds are follow-ups, the plan card's
     "the rest listed as follow-ups" line).

- **`HidePostAsync` / `RemovePostAsync`** —
  `src/Kumunita.Core/Posts/PostService.cs:1252` (hide) and `:1290` (remove)
  (A03 added `UnhidePostAsync` at `:1296`). Both are
  `Moderate`-gated write lanes: they call
  `CanAsync(actorId, AccessAction.Moderate, new PostToAuditableResource(post),
  session)` and, if allowed, write `Post.Status` + `Post.Modified` + one
  `SaveChangesAsync` (C3 same-transaction). **They take a `postId`** —
  for a group post, that is the **`Report.TargetId`** (a group post is a
  `Post` doc with a `GroupId` set, not a `ComponentId` — the ADR 0013
  group-lane shape). The write lanes are **target-kind-agnostic**: they
  operate on any `Post` doc by id, so no change to `PostService` is
  needed — A04 only changes **which id** the resolve lane passes.

- **`PostToAuditableResource.ComponentId`** —
  `src/Kumunita.Core/Posts/PostToAuditableResource.cs` (a `sealed`
  adapter; `ComponentId => Post.ComponentId`, `TargetKind => "post"`).
  The `Moderate` decision gates on the resource's `ComponentId` (the
  C-M3·2 "moderation scope" key) + the standing-moderator
  `ModeratorAssignment` rows on that component. **A group post's
  `Post.ComponentId` may be `string.Empty`** (a group post belongs to a
  `GroupId`, not a community component) — see the `// TODO ❓` below
  (the group-scope moderation gate is a real design question A04 must
  resolve, not assume).

- **`LoadReportTargetResourceAsync`** —
  `src/Kumunita.Core/Moderation/ModerationService.cs:231` — the
  resolve-lane's target switch (event / project / board / todo /
  grouppost). The `grouppost` case loads the `Post` by `report.TargetId`
  and returns `new PostToAuditableResource(gp)` — **the same adapter as a
  community post** (the A04 "closest copy" pin: the gate shape is
  already correct for a grouppost, the only missing piece is the
  execution).

- **The A02 test pins** —
  `tests/Kumunita.Core.Tests/ModerationServiceTests.cs`:
  - `A02_ResolveReport_OutcomeHidden_PostTarget_FlipsPostHidden`
    (`:1170`) — the post-target execution A04 does **not** change.
  - `A02_ResolveReport_OutcomeHidden_EventTarget_RecordsOutcomeOnly`
    (`:1368`) — the **non-post** trap: an `event`-targeted report with
    `Hidden` records the outcome but the decoy post stays `Active` and
    **no** `moderate` audit row commits. **A04 must NOT break this pin**
    (an `event` is still not a hideable kind — the plan card's "the rest
    listed as follow-ups"). There is **no** grouppost-target A02 pin
    today — the grouppost case was not separately pinned because the
    A02 guard (`!string.IsNullOrEmpty(report.PostId)`) blocks **all**
    non-post targets the same way. A04 adds the first grouppost pin.

- **The group-post intake** — `ReportTargetsController.cs:284`
  (`POST /groups/{groupId}/posts/{postId}/report`) calls
  `FileReportTargetAsync(targetKind: "grouppost", targetId: postId, …)` —
  the grouppost's `PostId` on the `Report` row is `string.Empty` and the
  post's id is in `TargetId` (the A02 trap shape, confirmed).

## The work

1. **Flip the A02 guard for `grouppost` targets** in the
   `ModerationService.ReportIntake.cs:767-781` execution block. The
   current guard is `!string.IsNullOrEmpty(report.PostId)` — extend it so
   that when `report.TargetKind == "grouppost"`, the target's id is
   `report.TargetId` (not `report.PostId`) and the same
   `HidePostAsync` / `RemovePostAsync` write lanes run on that id. The
   post-target shape (the `PostId` branch) is **unchanged**; the
   `event` / `project` / `board` / `todo` shapes **stay records-only**
   (the plan card's follow-ups). Concretely, the block becomes a
   target-kind-aware branch:
   ```csharp
   // A02 (post-targets) + A04 (grouppost-targets) — the outcome-execution
   // lane. Post targets use Report.PostId; grouppost targets use
   // Report.TargetId (the ADR 0173 new-lane shape). The other new-lane
   // kinds (event / project / board / todo) remain records-only (A04's
   // "the rest listed as follow-ups").
   string? executionTargetId = null;
   if (!string.IsNullOrEmpty(report.PostId))
       executionTargetId = report.PostId;                                  // A02 post / reply shape
   else if (report.TargetKind == "grouppost" && !string.IsNullOrEmpty(report.TargetId))
       executionTargetId = report.TargetId;                                // A04 grouppost shape
   if (outcome is (ReportOutcome.Hidden or ReportOutcome.Removed)
       && _posts is not null
       && executionTargetId is not null)
   {
       if (outcome == ReportOutcome.Hidden)
           await _posts.HidePostAsync(executionTargetId, globalAdminId, session);
       else
           await _posts.RemovePostAsync(executionTargetId, globalAdminId, session);
   }
   ```
   Update the block's `// IMPROVE-UX A02 — …` doc comment to note the
   A04 grouppost branch (the A02 comment already names A04's scope; keep
   the comment honest about which kinds execute vs. records-only).

2. **Resolve the `// TODO ❓` group-scope moderation gate.** The
   `HidePostAsync` / `RemovePostAsync` write lanes gate on
   `CanAsync(actorId, AccessAction.Moderate, new PostToAuditableResource(post),
   session)` — which reads the post's `ComponentId` (the C-M3·2
   moderation-scope key). A **group post's** `Post.ComponentId` may be
   `string.Empty` (it belongs to a `GroupId`, not a community component).
   **Check before building:** does the `Moderate` decision for a
   `ComponentId = string.Empty` resource resolve to "denied" (the
   C-M3·4 "no fabricated component" shape — a GlobalAdmin's
   `ModeratorAssignment` is on a specific component id, not
   `string.Empty`)? If so, a GlobalAdmin resolving a grouppost report
   with `Hidden` would be **denied** at the Core's `CanAsync` gate even
   though the Web layer's `[Authorize(Roles = GlobalAdmin)]` already
   passed — the A04 execution would be a silent no-op. This is the
   **real design question A04 must settle**, not assume:
   - **Option A** (simplest, the GlobalAdmin-only shape): a GlobalAdmin
     is the only actor who resolves a report (the Web gate is
     GlobalAdmin-only), so the Core's `CanAsync(Moderate)` gate on a
     group-post resource must return `Allowed` for a GlobalAdmin —
     verify the `IAuthorizationService` `Moderate` branch for a
     `ComponentId = string.Empty` resource (read the `Moderate` branch
     in the authorization service; it may key on `ComponentId` and need
     a `GlobalAdmin` short-circuit, or the `PostToAuditableResource`
     may need to surface a `GroupId`-aware scope).
   - **Option B** (the standing-moderator shape, the ADR 0003 SoD): a
     standing-moderator on the group's **own component** (if the group
     is scoped to a component) can also hide the group post — but a
     group post's `ComponentId` is the group's, not the component's,
     so the standing-moderator row must be read on the group's scope,
     not the community's. This is the **stricter** reading; A04 may
     **file it as a `// TODO ❓`** and ship Option A (the
     GlobalAdmin-only shape) first, the A03 "the reversible lever"
     precedent (A03 shipped the GlobalAdmin-only shape; the
     standing-moderator shape is a follow-up).
   - **The test must pin whichever shape ships:** if Option A, a
     GlobalAdmin resolving a grouppost report with `Hidden` **hides the
     group post** (the `PostStatus.Hidden` write commits); if the
     decision is denied for a `ComponentId = string.Empty` resource, the
     test **must** assert the denial (the `PostStatus` unchanged + the
     `Deny` audit row) and the `// TODO ❓` must be **prominent** (the
     A02 test's `RecordsOutcomeOnly` comment shape).

3. **The A04 tests** — in
   `tests/Kumunita.Core.Tests/ModerationServiceTests.cs`, mirroring the
   A02 test shapes (the `A02_ResolveReport_OutcomeHidden_PostTarget_FlipsPostHidden`
   `:1170` and the `A02_ResolveReport_OutcomeHidden_EventTarget_RecordsOutcomeOnly`
   `:1368` pins):
   - **A04-1 — `A04_ResolveReport_OutcomeHidden_GroupPostTarget_FlipsGroupPostHidden`**
     — a grouppost-targeted report (`Report.PostId = string.Empty`,
     `Report.TargetKind = "grouppost"`, `Report.TargetId = the group
     post's id`) resolved with `ReportOutcome.Hidden` by a GlobalAdmin:
     the **group post's** `Post.Status` flips to `PostStatus.Hidden`
     (the A04 DoD "one more kind hideable" pin); the group post's
     `moderate` audit row commits (`Action = AccessAction.Moderate.Id`,
     `TargetId = the group post's id`, `Outcome = Allow`); the
     `report.resolve` row also commits (the lane's own row, the A02 pin
     unchanged).
   - **A04-2 — `A04_ResolveReport_OutcomeHidden_EventTarget_StillRecordsOnly`**
     — a **regression** pin for the A02 `event`-target trap (the A04
     "the rest listed as follow-ups" line): an `event`-targeted report
     with `Hidden` still records the outcome but **executes nothing**
     (the decoy post stays `Active`, no `moderate` row for the decoy).
     This test **must stay green after A04** — it is the proof A04
     flipped the pin for `grouppost` only, not for all non-post
     targets. (If the A02 `A02_…_EventTarget_RecordsOutcomeOnly` test
     already covers this, A04 does **not** re-pin it — the A02 test
     stays the pin. A04-2 is only needed if A04 changes the event
     branch.)
   - **(optional, the `Removed` symmetry) A04-3 —
     `A04_ResolveReport_OutcomeRemoved_GroupPostTarget_FlipsGroupPostRemoved`**
     — the `Removed` outcome on a grouppost target flips the group
     post's `Post.Status` to `PostStatus.Removed` (the A04 "hideable"
     pin's `Removed` sibling; the F4 hard lane is the same shape).

4. **The follow-up list** — the plan card's "the rest listed as
   follow-ups" line. At the bottom of the A04 commit message (or as a
   `// TODO ❓` comment in the execution block), list the **remaining
   target kinds** that A04 does **not** extend to (event / project /
   board / todo), each with a one-line note on what its "hide" would
   mean (event: an event has no `PostStatus`; its "hide" is a different
   doc shape, likely a `Visibility` flag, not a `PostStatus` flip —
   **filed, not built**):
   - `event` — no `PostStatus` field; the ADR 0173 lane's event doc
     shape is out of scope (a new doc surface, not a `Post` flip).
   - `project` — same shape as event (a `Project` doc, not a `Post`).
   - `board` — a `KanbanBoard` doc; no `PostStatus` field.
   - `todo` — a `TodoItem` doc; no `PostStatus` field.

5. **Verify the gates.** `dotnet build Kumunita.slnx`, run
   `ModerationServiceTests` (all A02 tests + the new A04 tests), and
   `python3 tools/ux/ux-measure.py --check` (A04 adds no new Web action,
   no new view, no new translation key — the execution block is a Core
   change; `bare_status` stays at 710, `a11y` / `jargon` / `empty`
   unchanged).

## Scope rules

- **The A02 execution-block change (the `grouppost` branch) + the tests +
  the follow-up list.** Do **not** touch `HidePostAsync` /
  `RemovePostAsync` / `UnhidePostAsync` (the A03 lane — the write
  lanes are target-kind-agnostic, A04 only changes which id the
  resolve lane passes). Do **not** extend the execution to `event` /
  `project` / `board` / `todo` (the plan card's follow-ups, filed not
  built). Do **not** add a new `PostStatus` field or a new doc surface
  (the A04 "closest copy" pin — a grouppost is a `Post` doc, the same
  write lanes apply). Do **not** touch the `Report` POCO / the R03
  resolution shape / the A03 `UnhidePostAsync` seam / the Web
  `Unhide` action (the A03 surface — A04 is a Core change, the Web
  layer's `ResolvePost` action is unchanged).
- **The `// TODO ❓` group-scope moderation gate** (item 2) is the
  **one** design question A04 must settle. If the `Moderate` decision
  for a `ComponentId = string.Empty` resource is denied, A04 ships
  Option A (the GlobalAdmin-only shape) **or** files the denial as a
  prominent `// TODO ❓` and the test pins the denial (the A02
  `RecordsOutcomeOnly` comment shape). Do **not** silently ship a
  no-op execution (the A04 trap — the outcome recorded, nothing
  happens, the A02 pin's exact shape).
- **One commit** (the house rule): `feat(ux): the resolve lane executes
  the outcome for grouppost targets — Hidden hides the group post,
  Removed removes it (IMPROVE-UX A04)`.

## Files that should change

- `src/Kumunita.Core/Moderation/ModerationService.ReportIntake.cs`
  (the A02 execution block — the `grouppost` branch)
- `tests/Kumunita.Core.Tests/ModerationServiceTests.cs` (the A04 tests)
- (possibly) `src/Kumunita.Core/Posts/PostToAuditableResource.cs` or
  the authorization service's `Moderate` branch — **only** if item 2
  (the group-scope gate) requires a `ComponentId = string.Empty`
  short-circuit or a `GroupId`-aware scope. **Verify first** (read the
  `Moderate` branch in the `IAuthorizationService` impl); do not
  change it unless the test (item 3, A04-1) demands it.

(≤ ~5 Core + test files — the unit-size rule.)

## Before committing

```bash
dotnet build Kumunita.slnx -c Debug
python3 tools/ux/ux-measure.py --check   # must stay green (no new Web surface)
dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll \
  -class "Kumunita.Core.Tests.ModerationServiceTests" -method "*A04*"
# + the A02 test pins (confirm no regression):
dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll \
  -class "Kumunita.Core.Tests.ModerationServiceTests" -method "*A02*"
```

If a test fails, read the error, adjust, continue. Then report:
the execution-block change (file:line in
`ModerationService.ReportIntake.cs`), the group-scope gate decision
(Option A / Option B / the `// TODO ❓` denial pin), the test names +
pass counts, the follow-up list (the remaining target kinds), the
`ux-measure.py --check` line, and the commit hash. Do not commit
unrelated work.
