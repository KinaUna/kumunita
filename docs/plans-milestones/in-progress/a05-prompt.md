---
id: a05-prompt
title: "Prompt: IMPROVE-UX A05 — route new reports to the moderators"
tier: living
kind: plan
status: current
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/adr/0003-roles-and-moderator-scoping.md
  - docs/adr/0006-module-boundary-contracts.md
  - docs/adr/0077-admin-account-signup-verify-notification.md
  - docs/dev/testing-and-ci.md
lane: improve-ux
updated: 2026-11-11
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> **A05** — the fifth unit of the **admin/moderator** block and the unit
> that closes the "new reports reach no moderator" loop (E3, M-severity).
> A01–A04 shipped the **read** lane (feeds honour `PostStatus`) and the
> **write** lane (the resolve form executes `Hidden` / `Removed` for post +
> grouppost targets, plus `UnhidePostAsync`). **A05 is the routing half:**
> when a resident files a report, the component's standing moderators and
> the GlobalAdmins get an inbox row pointing at the report, with **no
> target content in the body** (the E3 "moderators hear only after a
> GlobalAdmin unlocks + assigns" gap this closes). The `report.filed`
> emission to the **author** (the 2026-09-25 no-reason-snippet shape) is
> **unchanged** — A05 adds a *second* emission kind, `report.queued`, to a
> *different* recipient set. **Who is it for:** the next coding agent
> (fresh context). **When to read the rest:** when starting A05 (A01–A04
> are shipped, the read + write lanes exist, no blockers).

# Fresh-context prompt: IMPROVE-UX A05

You are continuing the Kumunita repository on `development` (HEAD
`2c4f475b`, A01 + A02 + A03 + A04 all landed — A01 makes the read paths
honour `PostStatus`; A02 makes the resolve lane execute the outcome for
post targets; A03 adds `UnhidePostAsync` + the "Unhide" button; A04
extends the outcome-execution to grouppost targets). **A05** is the next
unit. The plan card (in `plan-improve-ux.md`, the A05 row):

> A05 — Routing: new kind `report.queued` to the component's standing
> moderators + GlobalAdmins, **no content in body**, link
> `/moderation/{id}` (scoped read still applies); `ReportFiled` to the
> author unchanged.
> Read first: `ReportIntake.cs:121-147,265-275,410-420`,
> `ModeratorAssignment`.
> **Done when: moderators get an inbox row on intake.**

## The state of the code (measured 2026-11-11, `development`)

- **The three filing lanes** (all in
  `src/Kumunita.Core/Moderation/ModerationService.ReportIntake.cs`):
  `FileReportAsync` (`:64`, post + reply), `FileReportTargetAsync`
  (`:200`, the ADR 0173 new-lane shapes: event / project / board / todo /
  grouppost), `FileReplyReportAsync` (`:334`). Each already emits **one**
  `report.filed` notification to the **content's author** (`post.AuthorId`
  / `authorId` / `reply.AuthorId`), skipping the self-report case. That
  emission is **unchanged by A05** — A05 adds a *separate* emission to a
  *different* recipient set (the moderators), in each of the three lanes.
  The three `report.filed` emissions live at `:140`, `:266`, `:412`.
  Each lane already carries a `report.ComponentId` (post lane: the post's
  component; new-lane shapes: the `componentId` parameter, nullable) — that
  is the standing-moderator scope key A05 reads.

- **The standing-moderator read seam** —
  `ModerationService.GetStandingModeratorsAsync(string? componentId)`
  (`src/Kumunita.Core/Moderation/ModerationService.cs:823`) returns an
  `IReadOnlyList<ModerationStandingModerator>` (a `(UserId, DisplayName)`
  record) for the component's `ModeratorAssignment` rows, or `[]` when
  `componentId` is null (the C-M3b·4 "no fabricated component" shape).
  **A05 uses this to resolve the standing-moderator recipient set.** It
  already exists and is report-gated (ADR 0003) — A05 does not need a new
  read seam for the moderators.

- **The ADR 0077 admin-fan-out precedent** —
  `IdentityService.AccountOps.cs:566` (`EmitAccountNotificationAsync`) is
  the closest existing shape for "notify every GlobalAdmin": it resolves
  `userManager.GetUsersInRoleAsync(Roles.GlobalAdmin)`, loops, and emits
  one row per admin with a per-admin idempotency key
  (`notification:{kind}:{accountId}:{adminId}`), best-effort (wrapped in
  `try/catch`, logged, never blocks the domain write). **A05's
  `report.queued` emission to the GlobalAdmins mirrors this fan-out
  shape** — one inbox row per GlobalAdmin, per-admin idempotency key,
  best-effort. **The one design question A05 must settle** (see below):
  `ModerationService` does **not** currently hold `userManager` /
  `IIdentityService` — it has `_notifications`, `_userInfo`,
  `_profileSettings`, `_authz`, `_store`, `_posts`,
  `_translationProvider`. Resolving the GlobalAdmin set is an **Identity
  concern**, so A05 needs a seam it does not have (the `// TODO ❓` below).

- **The notification-kind vocabulary** —
  `src/Kumunita.Core/Notifications/NotificationKinds.cs`. The closed
  `Known` set (`:361`) is the settings-toggle vocabulary (ADR 0076 D2);
  admin-lane kinds (`account.signup` / `account.verified`) trail the
  resident-facing kinds. `report.queued` is an **admin-lane** kind (the
  recipients are the moderators / GlobalAdmins, not the reporter), so it
  belongs **in** `Known` (it is a toggleable lane, unlike
  `instance.full-export` which is deliberately *not* in `Known`).
  **A05 adds the `ReportQueued = "report.queued"` constant + appends it to
  `Known`.**

- **The emitter** — `INotificationEmitter.EmitAsync` (full overload,
  `INotificationEmitter.cs:45`) carries `recipientId`, `kind`,
  `idempotencyKey`, `body`, `targetId`, `linkPath`. **A05 passes
  `linkPath = /moderation/{reportId}`** (the moderation route is
  `[Route("moderation")]` + `[HttpGet("{id}")]`,
  `ModerationController.cs:115,230`) and a **localized, content-free body**
  (see the `// TODO ❓` on body construction).

- **The notification-key JSON** (the en floor + de/fr/da parity):
  `src/Kumunita.Core/Localization/Keys/en/notification.json` — the
  existing `notification.report.filed.subject` / `.body` pair (`:14-15,
  :24-25`) is the template shape. **A05 adds a
  `notification.report.queued.subject` / `.body` pair** (the "no content in
  body" pin: the body names *that a report is pending*, never the target
  title / body / reporter's reason). `notifications.json` (`:29-31,
  :42-44`) carries the kind label
  (`notifications.kind.report.queued`) + the preference label — the
  settings-toggle surface for the new admin-lane kind. All four files
  (en/de/fr/da × both JSON) get the keys; the translation-parity test pins
  the 4-language match.

- **The test harness** — `tests/Kumunita.Core.Tests/ModerationServiceTests.cs`
  (the `PostgresFixture` / `BootStoreAsync` / `ServicesWithPosts` trio; the
  `ReportAudits` / `PostAudits` helpers; the A02/A04 test block is the
  closest precedent for a "resolve emits N rows" assertion). A05's tests
  file into the same class, mirroring the A02 shapes.

## The work

1. **Add the `report.queued` kind** to `NotificationKinds.cs`:
   `public const string ReportQueued = "report.queued";` (doc-comment
   anchored to ADR 0077's admin-lane precedent + the A05 "new reports
   reach the moderators" purpose) and append `ReportQueued` to the `Known`
   list (after `IssueResolved` / `TodoAssign`, in the report-cluster, or at
   the admin-lane tail — match the existing ordering and note the choice in
   the comment). It is a **toggleable** admin-lane kind (in `Known`), not
   a one-time system notice.

2. **Add the emission to the three filing lanes** in
   `ModerationService.ReportIntake.cs` — after each lane's existing
   `report.filed`-to-author emission, emit `report.queued` to the resolved
   recipient set (the component's standing moderators ∪ the GlobalAdmins,
   deduped). Each emission:
   - `kind: NotificationKinds.ReportQueued`
   - `linkPath: "/moderation/{report.Id}"` (the scoped-read route; the
     receiving moderator's `Read` decision on open still applies — A05 does
     not grant standing, it only routes the nudge)
   - `body:` a localized, **content-free** sentence (no target title, no
     target body, no reporter's reason — the A05 pin + the 2026-09-25
     disclosure-fix shape; see the `// TODO ❓` on the body below)
   - `idempotencyKey: $"notification:report.queued:{report.Id}:{recipientId}"`
     (per-recipient, the ADR 0077 admin-fan-out shape — one row per
     moderator / admin, a re-filing of the same report does not
     re-emit)
   - **best-effort** (wrapped in the same `try/catch` posture as
     `EmitAccountNotificationAsync` — a notification-lane failure must not
     block the filing domain write)
   - **skip** a moderator who *is* the reporter (a resident who filed a
     report against their own content is not their own standing
     moderator; the self-report case is already skipped on the author side
     — mirror the guard)

3. **Resolve the recipient set.** The **standing moderators** come from
   the existing `GetStandingModeratorsAsync(report.ComponentId)` seam (or
   the `ModeratorAssignment` read on the lane's own session — prefer the
   session-scoped read over a fresh session, the C3 same-transaction shape;
   `GetStandingModeratorsAsync` opens its own session, so for the
   filing-lane emission a direct `Query<ModeratorAssignment>()` on the
   caller's session is the closer fit — use whichever the lane already uses
   for the `report.assign` lane, `:505-514`). The **GlobalAdmins** need a
   seam `ModerationService` does not currently have (the `// TODO ❓` in
   item 4).

4. **Settle the GlobalAdmin-enumeration seam (the `// TODO ❓`).**
   `ModerationService` holds no `userManager` / `IIdentityService` today.
   The ADR 0077 precedent resolves admins via
   `userManager.GetUsersInRoleAsync(Roles.GlobalAdmin)` inside
   `IdentityService`. A05 needs the GlobalAdmin set *from*
   `ModerationService`. **Options (pick one, do not silently invent a new
   frozen-surface method on `IUserInfoService` without an ADR — ADR 0006
   constrains widening frozen seams):**
   - **Option A** (narrowest, the Identity-seam shape): add a
     read-only seam to `IIdentityService` — e.g.
     `Task<IReadOnlyList<string>> ListGlobalAdminsAsync()` (the
     `GetUsersInRoleAsync(Roles.GlobalAdmin)` body, `userId` projection
     only, no PII) — and wire `IIdentityService` (or the seam) into
     `ModerationService`'s constructor as an **optional** dependency (the
     `_posts` / `_notifications` nullable precedent, CS1736), so existing
     tests that omit it keep compiling. **This is the shape to ship** (it
     reuses the existing role source of truth, keeps `IUserInfoService`
     untouched, and the optional-dependency precedent keeps the test
     harness shapes intact). **Write or amend an ADR in the same commit**
     (the house rule: "A unit that makes a decision writes or amends an
     ADR") naming the new `IIdentityService` seam + the optional
     `ModerationService` dependency.
   - **Option B** (the `IUserInfoService` shape): add
     `ListGlobalAdminsAsync()` to `IUserInfoService` — **rejected** unless
     ADR 0006 is amended, because `IUserInfoService` is the frozen
     profile/community seam and role enumeration is an Identity concern
     (the `DeletionScheduler` / `IdentityService` precedent reads roles
     through `userManager`, not `IUserInfoService`).
   - **The test must pin whichever shape ships:** a filed report produces
     **one `report.queued` inbox row per standing moderator and per
     GlobalAdmin** (deduped), each linking `/moderation/{reportId}`, and
     **none** for the reporter themselves.
   - If no seam can be added cleanly in this unit (the ADR surface is
     genuinely blocked), **file it as a prominent `// TODO ❓`**, ship the
     standing-moderator half (the `GetStandingModeratorsAsync` seam already
     exists), and pin the GlobalAdmin half as a held unit — but **do not**
     silently ship only the moderator half (that is exactly the E3 gap
     A05 exists to close: GlobalAdmins are the fallback recipients when a
     component has no standing moderator).

5. **The body (the `// TODO ❓` pin).** The A05 body must carry **no
   target content** (no title, no body, no reporter's reason). The closest
   precedent is the `report.filed` lane's 2026-09-25 security fix
   (the localized template alone is the signal; the reporter's free-text
   reason is an attacker-controlled disclosure channel — never forwarded).
   A05's `report.queued` body is a plain localized sentence ("A report is
   pending your review" / the `notification.report.queued.body` key) with
   **no inline content**, the `linkPath` carrying the navigation. Keep the
   body `null`-able (the `report.filed` emission passes `body: null` — the
   localized subject is the row's text); match that shape.

6. **The A05 tests** — in `tests/Kumunita.Core.Tests/ModerationServiceTests.cs`,
   mirroring the A02 test shapes:
   - **A05-1 — `FileReport_Queued_StandingModeratorAndGetGlobalAdmin_InboxRows`**
     — file a post-target report for a component with a planted
     `ModeratorAssignment` (a standing moderator) + a planted GlobalAdmin
     (via whichever seam item 4 ships): assert **one `report.queued`
     inbox row for the standing moderator** and **one for the GlobalAdmin**
     (deduped — a standing-moderator who is also the reporter is not
     notified; a standing-moderator who is also a GlobalAdmin gets one
     row, not two), each with `linkPath = /moderation/{reportId}` and the
     `report.queued` kind; the **author's `report.filed` row is unchanged**
     (the author still gets exactly one `report.filed`, not a
     `report.queued`).
   - **A05-2 — `FileReport_Queued_SelfReport_NoQueuedRowForReporter`**
     — the reporter is their own standing moderator (degenerate): no
     `report.queued` row is emitted to the reporter (the self-report skip
     guard, mirrored from the author lane).
   - **A05-3 — `FileReportTarget_Queued_NewLaneShape_RoutesToo`** — a
     `FileReportTargetAsync` filing (the ADR 0173 shape, e.g. a grouppost
     with a nullable `componentId`): when `componentId` is set, the
     standing moderators on that component get a `report.queued` row; when
     `componentId` is null (a group post outside a component), only the
     GlobalAdmins are notified (the "no fabricated component" shape —
     `GetStandingModeratorsAsync(null)` returns `[]`).
   - **A05-4 — `FileReport_Queued_BodyHasNoContent`** — the stored
     `report.queued` row's body is `null` or the localized template only
     (no target title, no target body, no reporter reason) — the A05
     "no content in body" pin.

7. **Verify the gates.** `dotnet build Kumunita.slnx`, run
   `ModerationServiceTests` (all A05 tests + the existing
   `FileReport*` tests to confirm the author's `report.filed` emission is
   unchanged), and `python3 tools/ux/ux-measure.py --check` (A05 adds no
   new Web action, no new view — the emission is a Core change; `bare_status`
   stays at 710, `a11y` / `jargon` / `empty` unchanged; the
   `notify_without_link` count should **shrink** by however many report
   kinds A05 now links, since `report.queued` carries a `linkPath`).

## Scope rules

- **The `report.queued` kind + the three-lane emission + the
  GlobalAdmin-enumeration seam (item 4, with an ADR) + the tests + the
  en/de/fr/da keys.** Do **not** change the `report.filed`-to-author
  emission (the 2026-09-25 shape stands — the author still gets
  `report.filed`, never `report.queued`). Do **not** grant any standing
  (A05 routes a nudge; the moderator's `Read` decision on opening
  `/moderation/{id}` still applies — thin token, fat authorization). Do
  **not** add the nav badge (that is A06's scope — the A05 card's
  "Done when: moderators get an inbox row on intake" is the boundary; the
  badge count query is A06). Do **not** widen `IUserInfoService` without
  an ADR (item 4, Option B is rejected absent an ADR 0006 amendment). Do
  **not** put any target content in the body (item 5, the `// TODO ❓`
  pin).
- **The GlobalAdmin-enumeration seam (item 4)** is the **one** design
  question A05 must settle. Ship Option A (the `IIdentityService`
  `ListGlobalAdminsAsync` read seam + optional `ModerationService`
  dependency) **with an ADR**, or file a prominent `// TODO ❓` and hold
  the GlobalAdmin half — but **do not** silently ship only the
  standing-moderator half (that is the E3 gap A05 exists to close).
- **One commit** (the house rule): `feat(ux): new reports route to the component's standing moderators + GlobalAdmins — report.queued inbox row, no content in body (IMPROVE-UX A05)`.

## Files that should change

- `src/Kumunita.Core/Notifications/NotificationKinds.cs` (the
  `ReportQueued` constant + the `Known` list)
- `src/Kumunita.Core/Moderation/ModerationService.ReportIntake.cs` (the
  three filing lanes — the `report.queued` emission after each
  `report.filed`)
- `src/Kumunita.Core/Identity/IIdentityService.cs` + the
  `IdentityService` impl (the `ListGlobalAdminsAsync` read seam, **only**
  for item 4 Option A)
- `src/Kumunita.Core/Moderation/ModerationService.cs` (the optional
  `IIdentityService` dependency, **only** for item 4 Option A)
- `src/Kumunita.Core/Localization/Keys/{en,de,fr,da}/notification.json` +
  `notifications.json` (the `report.queued` subject/body + kind/preference
  labels)
- `tests/Kumunita.Core.Tests/ModerationServiceTests.cs` (the A05 tests)
- `docs/adr/` (a new ADR for item 4 Option A — number after the current
  highest in `docs/adr`)

(≤ ~6 Core + test + ADR files — the unit-size rule; the 4×2 JSON key files
count as the translation-parity surface, not as separate units.)

## Before committing

```bash
dotnet build Kumunita.slnx -c Debug
python3 tools/ux/ux-measure.py --check   # must stay green (no new Web surface)
dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll \
  -class "Kumunita.Core.Tests.ModerationServiceTests" -method "*A05*"
# + the existing filing tests (confirm the author's report.filed emission is unchanged):
dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll \
  -class "Kumunita.Core.Tests.ModerationServiceTests" -method "*FileReport*"
# + the translation-parity test (the new report.queued keys × en/de/fr/da):
dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll \
  -method "*Translation*"
```

If a test fails, read the error, adjust, continue. Then report:
the emission change (file:line in `ModerationService.ReportIntake.cs`,
each of the three lanes), the GlobalAdmin-seam decision (Option A /
Option B / the `// TODO ❓` hold + the ADR number), the test names +
pass counts, the `ux-measure.py --check` line (note the
`notify_without_link` shrink), and the commit hash. Do not commit
unrelated work.
