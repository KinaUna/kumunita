---
id: r08-prompt
title: "Prompt: IMPROVE-UX R08 — IssueResolved emission + new kind"
tier: living
kind: plan
status: current
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/r07-prompt.md
  - docs/dev/testing-and-ci.md
lane: improve-ux
updated: 2026-11-10
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> R08. **Who is it for:** the next coding agent. **When to read the rest:**
> when starting R08 after R07.

# Fresh-context prompt: IMPROVE-UX R08

You are continuing the Kumunita repository on `development`. Implement
**IMPROVE-UX unit R08 only**: close the **platform-issue** loop the R07
unit closed for content reports — make a *resolved* issue come back to its
**signed-in filer** as an inbox row. Today `ErrorReportService
.MarkResolvedAsync` stamps the doc + audit row and sends **nothing**: the
filer never hears back (the E5 finding). R08 adds one emission — a **new**
kind `issue.resolved`, recipient = the filer, a short body (the moderator's
note verbatim, when present), and a `linkPath` that lands on the filer's
row on the "My reports" page (`/reports/mine#i-{id}`). R06 (the
`GET /reports/mine` page, whose **issue** rows already carry
`id="i-{Id}"`) and R07 (the `ReportResolved` body + link for *content*
reports) have already landed — build on them, don't re-open them. Do not
start `issue.triaged` or any later unit.

Before editing, read [AGENTS.md](../../../AGENTS.md),
[testing-and-ci.md](../../dev/testing-and-ci.md), and the lane rules plus
the R08 card in [plan-improve-ux.md](plan-improve-ux.md). Check `git
status` and the current branch; preserve all existing work.

R08 is **Core-side** (`Kumunita.Core`) — one emission, one new kind, an
optional ctor seam, a DI wire, and a test. **This is the one unit in the
lane that *adds* a notification kind** (R07 *reused* `report.resolved`; the
other report kinds are R03's). Adding a kind is **not additive** — it trips
the closed-set pins (see Known trips), so read those before you touch
`NotificationKinds`. The only files to touch:

- **`src/Kumunita.Core/ErrorReports/ErrorReportService.cs`** — the
  emission site + the ctor seam. The method to change is
  `MarkResolvedAsync(string reportId, string actorId, string?
  resolutionNote, CancellationToken ct)`: it loads the `ErrorReport`,
  no-ops (returns `null`) when missing or already `resolved`, otherwise
  stamps `TriageStatus = "resolved"` + `ResolvedAt`/`ResolvedBy`/
  `ResolutionNote`, stores the doc + one `AccessAudit` row, and
  `await session.SaveChangesAsync(ct)` — **then `return stored;`**. Add the
  emission **between `SaveChangesAsync` and the `return`**, in the same
  write session (the R07 `ModerationService.ReportIntake` session-owning
  precedent — pass the existing `session`). The shape, mirroring R07:
  ```csharp
  // R08 — the filer hears back (E5). Only a signed-in filer gets an inbox
  // row (an anonymous filer has no recipient — the // TODO ❓ below).
  if (!string.IsNullOrWhiteSpace(stored.SubjectId)
      && _notifications is not null)
  {
      await _notifications.EmitAsync(
          session,
          recipientId: stored.SubjectId,
          kind: NotificationKinds.IssueResolved,
          idempotencyKey: $"notification:issue.resolved:{stored.Id}",
          body: BuildIssueResolvedBody(stored.ResolutionNote),
          targetId: stored.Id,
          linkPath: $"/reports/mine#i-{stored.Id}",
          ct: ct).ConfigureAwait(false);
  }
  ```
  - **`body`** — a short plain-words "your issue report was resolved"
    sentence; **append `stored.ResolutionNote` verbatim** when it is
    non-blank (the moderator's free text — do **not** translate it, the
    R07 `ResolutionNote` rule). There is no `ReportOutcome` here (that is
    the content-report shape, R03) — the issue doc only carries
    `TriageStatus` + `ResolutionNote`. **Localize is a `may`**: if you want
    the R07 house way (localize to the filer's `EmailLanguage` via
    `IUserInfoService` + `ITranslationProvider`, the `EventReminderService
    .BuildLocalizedReminder` precedent), add those optional seams too — but
    a plain string that carries the note verbatim is in scope and cheaper.
    Degrade to the plain string when a seam is `null` — never throw.
  - **`linkPath`** — use the `EmitAsync` overload that carries it
    (`INotificationEmitter.cs`, the `EmitAsync[1]` one with `targetId` /
    `linkPath` / `acceptPath` / `declinePath`) and pass
    `linkPath: $"/reports/mine#i-{stored.Id}"` + `targetId: stored.Id`. The
    `#i-{id}` fragment matches the anchor R06 already put on each
    **issue** row (`id="i-{Id}"`), so the inbox row lands on the right
    row. The inbox renders `LinkPath` as a clickable link and the email
    carries it BaseUrl-prefixed — you only supply the same-origin relative
    path.
  - **The ctor** is currently
    `public ErrorReportService(IDocumentStore store, IEscalationTokenService?
    tokenService = null)`. Add `INotificationEmitter? notifications = null`
    as a **trailing optional** parameter (the same `CS1736` trailing-param
    idiom the other services use, so the existing
    `new ErrorReportService(store)` test-construction call sites keep
    compiling) + a `_notifications` field. `ErrorReport` is the doc:
    `Id` (string), `SubjectId` (`string`, blank = anonymous — **not**
    nullable), `ResolutionNote` (`string?`), `TriageStatus` (a closed
    string). The `#i-{id}` is the **issue** anchor (R06), distinct from
    R07's `#r-{id}` **report** anchor — don't mix them up.

- **`src/Kumunita.Core/Notifications/NotificationKinds.cs`** — add the new
  constant **`public const string IssueResolved = "issue.resolved";`**
  (a sibling of the existing `ReportFiled` / `ReportAssigned` /
  `ReportResolved`) **and** add `IssueResolved` to the closed
  `Known` set (the `IReadOnlyList<string> Known { get; }` array — group it
  near the other report/issue kinds). Do **not** add `issue.triaged` —
  that is the card's optional `❓` and is out of scope for this unit.

- **`src/Kumunita.Core/ErrorReports/DependencyInjection.cs`** — the
  `AddErrorReports` factory currently constructs
  `new ErrorReportService(IDocumentStore, IEscalationTokenService)`
  (two args, no emitter). Wire the **optional** emitter so the emission
  actually fires in the app: append
  `sp.GetService<Core.Notifications.INotificationEmitter>()` as the third
  arg (use `GetService` — the seam is optional and must degrade to
  `null`, the `EventReminderService` / `ModerationService` idiom — not
  `GetRequiredService`). Without this the field stays `null` in
  production and R08 ships a dead seam.

- **Translation keys** — adding a kind to `Known` means the
  `/notifications/preferences` toggle page renders a label for it
  (`notifications.kind.{kind}`, the U07 family). Add
  **`notifications.kind.issue.resolved`** to
  `src/Kumunita.Core/Localization/Keys/{en,de,fr,da}/notifications.json`
  (all four in the same commit; `de` / `fr` / `da` machine-written, flagged
  for the parked native review). The en label is a short plain-words label
  (the `report.resolved` → "Report resolved" precedent) — your call on the
  exact wording. If you localize the R08 body you may also add a small
  `notifications.issue.resolved_body` key in the same surface — optional,
  and only in all four languages.

- **Tests** — add to the existing
  [`ErrorReportResolveTests.cs`](../../../tests/Kumunita.Core.Tests/ErrorReportResolveTests.cs)
  (it already boots the store + `ErrorReportDocTypes` + `CoreDocTypes` and
  constructs `new ErrorReportService(store)` — the harness to copy). Wire a
  **NSubstitute `INotificationEmitter`** and construct
  `new ErrorReportService(store, tokenService: null, notifications:
  substitute)`. Capture the emission on the `EmitAsync[1]` (linkPath)
  overload with `Arg.Do` / `Arg.Is` and assert:
  (a) `kind == NotificationKinds.IssueResolved` and
  `recipientId == <the filer's SubjectId>`;
  (b) **`linkPath` is exactly `/reports/mine#i-{reportId}`** and
  `targetId == reportId`;
  (c) the body carries the `ResolutionNote` verbatim when one was passed.
  One test for a **signed-in filer** (non-blank `SubjectId`) → the emission
  fires; one for an **anonymous filer** (blank `SubjectId`) → **no**
  emission (the `if (!IsNullOrWhiteSpace(SubjectId) && _notifications is
  not null)` guard); and confirm the two existing no-op paths
  (already-`resolved`, missing) still emit **nothing** (idempotency).

Read first: `MarkResolvedAsync` + the ctor in
[`ErrorReportService.cs`](../../../src/Kumunita.Core/ErrorReports/ErrorReportService.cs);
the doc's fields (`Id` / `SubjectId` / `ResolutionNote` / `TriageStatus`)
in [`ErrorReport.cs`](../../../src/Kumunita.Core/ErrorReports/ErrorReport.cs);
the kind constants + the `Known` set in
[`NotificationKinds.cs`](../../../src/Kumunita.Core/Notifications/NotificationKinds.cs);
the emitter seam + the two `EmitAsync` overloads in
[`INotificationEmitter.cs`](../../../src/Kumunita.Core/Notifications/INotificationEmitter.cs)
(pick `EmitAsync[1]`, the one that carries `linkPath`); the `AddErrorReports`
factory in [`DependencyInjection.cs`](../../../src/Kumunita.Core/ErrorReports/DependencyInjection.cs);
the R07 emission you're mirroring in
[`ModerationService.ReportIntake.cs`](../../../src/Kumunita.Core/Moderation/ModerationService.ReportIntake.cs)
+ the `BuildReportResolvedBody` helper in
[`ModerationService.cs`](../../../src/Kumunita.Core/Moderation/ModerationService.cs);
the `notifications.kind.*` family in
`src/Kumunita.Core/Localization/Keys/en/notifications.json`; the closed-set
pins in
[`NotificationsControllerTests.cs`](../../../tests/Kumunita.Web.Tests/NotificationsControllerTests.cs)
(the `31` count + the `Known` equality); and the harness in
[`ErrorReportResolveTests.cs`](../../../tests/Kumunita.Core.Tests/ErrorReportResolveTests.cs).

Do **not**: emit to an **anonymous** filer (blank `SubjectId` → no
recipient — leave the held question as `// TODO ❓` and do **not** mail the
`ContactEmail`, SEC: it is "never mailed" by design); add the
`issue.triaged` kind (the card's optional `❓`, a later unit); touch the
`ReportResolved` emission or the "My reports" view (R07/R06 are frozen);
loosen the `Known` count pin (re-baseline + annotate instead, see below);
translate the `ResolutionNote` (append verbatim); or change
`MarkResolvedAsync`'s audit row / `Via` / no-op behaviour.

Keep this to one seam and about five to six files (the service, the kinds
file, the DI factory, the one test class) + the four `notifications.json`
label keys (+ one ADR). A unit that makes a decision writes or amends an
ADR in the same commit (the lane rule) — adding a new closed-set kind + the
anonymous-filer hold is one. The next free ADR number is **0300**; a short
ADR naming the `issue.resolved` kind + the held `// TODO ❓` (anonymous
mailing is a SEC decision, not made here) is the right shape.

**Known trips** (these differ from R07 — adding a *kind* trips closed-set
pins; R07 *reused* one so it hit none):

- **The `Known` count pin** —
  `NotificationsControllerTests
  .GET_Notifications_Preferences_Returns_200_And_Toggles` asserts
  `Assert.Equal(31, vm.AllKinds.Count)` (and
  `Assert.Equal(NotificationKinds.Known, vm.AllKinds)`, which stays
  self-consistent). Adding `IssueResolved` to `Known` bumps the count to
  **32** — **re-baseline `31` → `32`** and append the running comment
  (the `30 → 31 (the group-messaging invitation flow, ADR 0278…)` precedent)
  naming the driving ADR. Do **not** loosen the assertion or drop the
  `Known` entry.
- **The kind-label key** — the preferences toggle page renders
  `notifications.kind.{kind}` for each known kind. A new known kind with no
  `notifications.kind.issue.resolved` label degrades to the raw key
  (the `KwLRegistryConsistencyTests` / `KnownTranslationKeys` pins watch
  this). Add the label to **all four** languages in the same commit
  (`C02_DataAsEmbeddedResourcesTests` / the parity pins auto-check the
  four-carry-the-same-set rule).
- **Pre-existing reds (NOT yours):** `ImproveHarnessTests
  .CodeCheck_Gate_Passes` and three `SecGate.DataClassCoverageTests` are
  already failing on `development` from R05 (`ListMyReportsAsync` over the
  CC ceiling + boundary edges). They are **not** caused by R08 — don't
  re-baseline the ratchet for R08 and don't "fix" them here. Run the
  narrow tests below, not the whole suite, so you don't chase them.

Run the narrowest relevant tests via the in-process runner (see
testing-and-ci.md for the exact invocation — the `-class` flag):
`ErrorReportResolveTests` (your new R08 tests) +
`NotificationsControllerTests` (the re-baselined count) +
`C02_DataAsEmbeddedResourcesTests`. Then report changed files,
verification results, the commit hash, and any held questions. Make one
commit for R08; do not commit unrelated work.
