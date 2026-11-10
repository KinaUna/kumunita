---
id: r09-prompt
title: "Prompt: IMPROVE-UX R09 — linkPath on TodoAssign + GroupAdded"
tier: living
kind: plan
status: current
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/r08-prompt.md
  - docs/dev/testing-and-ci.md
lane: improve-ux
updated: 2026-11-10
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> R09. **Who is it for:** the next coding agent. **When to read the rest:**
> when starting R09 (the two remaining "notification has no link back" kinds).

# Fresh-context prompt: IMPROVE-UX R09

You are continuing the Kumunita repository on `development`. Implement
**IMPROVE-UX unit R09 only**: give the two remaining link-less notifications
a `linkPath` that lands the resident on the thing they were told about.
Today two emitters use the **link-less** `EmitAsync` overload (the 6-arg one
with no `targetId`/`linkPath`): the **`TodoAssign`** emitter (a to-do was
assigned to me) and the **`GroupAdded`** emitter (I was added to a group) —
both fire, but the inbox row has no clickable destination (the E14 "7 of 32
notification emits carry no link back" finding; `ux-measure.py`
`notify_without_link` still lists these two). R09 switches both to the
`EmitAsync` overload that carries `targetId`/`linkPath` and passes the
right path:

- **`TodoAssign`** → `linkPath: $"/projects/todos/{todo.Id}"` + `targetId: todo.Id`
- **`GroupAdded`** → `linkPath: $"/groups/{groupId}"` + `targetId: groupId`

That is the whole unit: **two emission-site edits + their tests**. No new
kind, no translation key, no closed-set pin, no ADR — the kinds
(`NotificationKinds.TodoAssign` / `.GroupAdded`) already exist and are in
`Known` (ADR 0077 / 0083), and `EmitAsync` already stores `LinkPath` on the
row (the inbox renders it as a clickable link + the email BaseUrl-prefixed
it — you only supply the same-origin relative path). Do not start `R10` or
any other unit.

Before editing, read [AGENTS.md](../../../AGENTS.md),
[testing-and-ci.md](../../dev/testing-and-ci.md), and the lane rules plus
the R09 card in [plan-improve-ux.md](plan-improve-ux.md). Check `git status`
and the current branch; preserve all existing work.

The two files to touch (plus the test that pins each):

- **`src/Kumunita.Core/Projects/TodoService.Lifecycle.cs`** — the
  `TodoAssign` emission inside `AssignTodoAsync(...)` (the call block ending
  in `await session.SaveChangesAsync(ct)`). The current call is the
  link-less overload:
  ```csharp
  await _notifications.EmitAsync(
      session,
      recipientId: assigneeId,
      kind: NotificationKinds.TodoAssign,
      idempotencyKey: $"notification:todo.assign:{todo.Id}",
      body: body,
      ct: ct).ConfigureAwait(false);
  ```
  Switch it to the overload that carries `targetId`/`linkPath` (add the two
  named args, keep everything else identical):
  ```csharp
  await _notifications.EmitAsync(
      session,
      recipientId: assigneeId,
      kind: NotificationKinds.TodoAssign,
      idempotencyKey: $"notification:todo.assign:{todo.Id}",
      body: body,
      targetId: todo.Id,
      linkPath: $"/projects/todos/{todo.Id}",
      ct: ct).ConfigureAwait(false);
  ```
  Do **not** change the `body` (it is the `BuildTodoAssignBodyAsync`
  localized card-snippet — leave it), the `idempotencyKey`, or the
  `recipientId`.

- **`src/Kumunita.Core/UserInfo/UserInfoService.cs`** — the `GroupAdded`
  emission inside `AddGroupMemberAsync(string groupId, string userId,
  string addedBy)`. The emitter is resolved **lazily** (
  `var ns = services?.GetService<INotificationEmitter>();` — the
  circular-dependency rationale in the class doc-comment; a test harness
  that passes `services = null` gets no emission). The current call is the
  link-less overload:
  ```csharp
  await ns.EmitAsync(
      session,
      userId,
      Notifications.NotificationKinds.GroupAdded,
      $"notification:{Notifications.NotificationKinds.GroupAdded}:{groupId}:{userId}",
      group.Name,
      CancellationToken.None).ConfigureAwait(false);
  ```
  Switch it to the overload that carries `targetId`/`linkPath`. This one is
  called **positionally** — append the two positionals after `group.Name`
  (the `EmitAsync` `targetId` / `linkPath` slots), keeping
  `CancellationToken.None` last:
  ```csharp
  await ns.EmitAsync(
      session,
      userId,
      Notifications.NotificationKinds.GroupAdded,
      $"notification:{Notifications.NotificationKinds.GroupAdded}:{groupId}:{userId}",
      group.Name,
      targetId: groupId,
      linkPath: $"/groups/{groupId}",
      CancellationToken.None).ConfigureAwait(false);
  ```
  Do **not** change the `body` (`group.Name`), the `idempotencyKey`, the
  `recipientId` (`userId`), or the lazy `services?.GetService<...>()`
  resolve.

- **Tests** — pin that the two rows now carry the `LinkPath`. The existing
  harnesses are the copy-paste shapes:
  - `GroupAdded`:
    [`UserInfoServiceGroupNotificationTests.cs`](../../../tests/Kumunita.Core.Tests/UserInfoServiceGroupNotificationTests.cs)
    (already boots the store, calls `AddGroupMemberAsync`, loads the stored
    `Notification` row). Add/extend an assertion that the stored
    `row.LinkPath == $"/groups/{GroupId}"` and `row.TargetId == GroupId`.
  - `TodoAssign`: find the existing `AssignTodoAsync` / `TodoAssign`
    emission test (search `tests/Kumunita.Core.Tests` for
    `AssignTodoAsync` / `NotificationKinds.TodoAssign`); if one asserts the
    stored row, extend it; otherwise add one that calls
    `AssignTodoAsync`, loads the filer's `Notification` row, and asserts
    `row.LinkPath == $"/projects/todos/{todo.Id}"` +
    `row.TargetId == todo.Id`. (The `Emit_TodoAssign_*` test in
    `NotificationServiceTests.cs` is the *NotificationService*-level seam,
    not the `TodoService` emitter — the one you want is the `TodoService`
    emission, which is where this unit's change lives.)
  - Confirm the two **no-op** paths still emit nothing (the pre-existing
    `Guardian` / already-member / already-assigned shapes) — the `LinkPath`
    must not appear on a row that was never created.

Read first: `AssignTodoAsync` + `BuildTodoAssignBodyAsync` in
[`TodoService.Lifecycle.cs`](../../../src/Kumunita.Core/Projects/TodoService.Lifecycle.cs);
`AddGroupMemberAsync` in
[`UserInfoService.cs`](../../../src/Kumunita.Core/UserInfo/UserInfoService.cs)
(the `GroupAdded` emission near its end); the two `EmitAsync` overloads in
[`INotificationEmitter.cs`](../../../src/Kumunita.Core/Notifications/INotificationEmitter.cs)
(pick the one that carries `targetId` / `linkPath`); the stored
`LinkPath` / `TargetId` fields on the `Notification` doc (the
`NotificationService.EmitAsync` writer in
[`NotificationService.cs`](../../../src/Kumunita.Core/Notifications/NotificationService.cs)
documents them); the two `*Tests.cs` harnesses named above; and the
`notify_without_link` heuristic in
[`tools/ux/ux-measure.py`](../../../tools/ux/ux-measure.py)
(`count_notifications_without_link` — it counts **distinct kinds** with no
link, so R09 drops `TodoAssign` + `GroupAdded` from that set).

Do **not**: change the `body`, `idempotencyKey`, or `recipientId` of either
emission; add a new kind, a translation key, or an ADR (there is nothing to
decide — the kinds and the `LinkPath` plumbing already exist); touch the
other four unlinked kinds (`report.filed` / `report.assigned` and any the
`ux-measure.py` table still lists — those are a later unit, or by design);
or re-baseline the `31 → 32` `Known` count pin (that is R08's, and R09 adds
no kind).

Keep this to the two emission sites + their tests (about three to four
files). Run the narrowest relevant tests via the in-process runner (see
testing-and-ci.md for the exact `-class` invocation):
`UserInfoServiceGroupNotificationTests` (your GroupAdded `LinkPath` pin) +
the `TodoAssign` emission test class you touched + `NotificationServiceTests`
(the seam's `LinkPath`/`TargetId` handling). Then verify the script shrank:
`python3 tools/ux/ux-measure.py` — `notify_without_link` should drop by 2
(the two kinds you linked are gone from its set). Then report changed files,
verification results, the commit hash, and any held questions. Make one
commit for R09; do not commit unrelated work.
