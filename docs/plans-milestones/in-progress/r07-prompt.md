---
id: r07-prompt
title: "Prompt: IMPROVE-UX R07 — ReportResolved body + link"
tier: living
kind: plan
status: current
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/r06-prompt.md
  - docs/dev/testing-and-ci.md
lane: improve-ux
updated: 2026-11-09
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> R07. **Who is it for:** the next coding agent. **When to read the rest:**
> when starting R07 after R06.

# Fresh-context prompt: IMPROVE-UX R07

You are continuing the Kumunita repository on `development`. Implement
**IMPROVE-UX unit R07 only**: make the `ReportResolved` notification come
back **useful** — its body is currently the reporter's *own* reason
(`Truncate(report.Reason)`) with no link; R07 changes the body to the
localized **outcome** (plus the moderator's note, when present) and gives
it a `linkPath` that lands on the reporter's row on the "My reports" page
(`/reports/mine#r-{id}`). R03 (the `Report` resolution shape: `Outcome` /
`ResolutionNote` / `ResolvedAt` / `ResolvedBy`), R04 (the resolve form),
R05 (the two read seams), and R06 (the `GET /reports/mine` page, whose rows
already carry `id="r-{ReportId}"`) have already landed — build on them,
don't re-open them. Do not start R08 or any later unit.

Before editing, read [AGENTS.md](../../../AGENTS.md),
[testing-and-ci.md](../../dev/testing-and-ci.md), and the lane rules plus
the R07 card in [plan-improve-ux.md](plan-improve-ux.md). Check `git
status` and the current branch; preserve all existing work.

R07 is **Core-side** (`Kumunita.Core`) — one emission change, an optional
constructor param, and a test. The kind `report.resolved`
(`NotificationKinds.ReportResolved`) **already exists** and is in
`NotificationKinds.Known` — reuse it, do not add a new kind (that is R08's
`issue.resolved`). The only files to touch:

- **`src/Kumunita.Core/Moderation/ModerationService.ReportIntake.cs`** —
  the `ReportResolved` emission inside the session-owning
  `ResolveReportAsync(string, string, IDocumentSession, ReportOutcome?,
  string?)` overload (the one that ends with
  `await session.SaveChangesAsync()`). The current emission is:
  ```csharp
  await _notifications.EmitAsync(
      session,
      recipientId: report.ReporterId,
      kind: NotificationKinds.ReportResolved,
      idempotencyKey: $"notification:report.resolved:{report.Id}:filed",
      body: Truncate(report.Reason),
      ct: default).ConfigureAwait(false);
  ```
  R07 changes **two** things:
  1. **`body`** — stop echoing `report.Reason`. Instead build a localized
     sentence from `report.Outcome` (the four-value `ReportOutcome?` the
     R03 lane wrote) + append `report.ResolutionNote` verbatim when it is
     non-blank (it is the moderator's free text — do **not** translate
     it). When `Outcome` is `null` (a pre-R03 / no-outcome resolve) keep
     a plain "resolved" shape. The recipient is the reporter
     (`report.ReporterId`), so localize to **their** language the house
     way the emitters already do — `IUserInfoService.GetProfileAsync(
     reporterId)` → `Profile.EmailLanguage` + `ITranslationProvider.
     GetAsync(key, lang)` (the `EventReminderService.BuildLocalizedReminder`
     precedent; `ModerationService` already injects
     `IUserInfoService? userInfo` — see below).
  2. **`linkPath`** — switch to the `EmitAsync` overload that carries it
     (`INotificationEmitter.cs`, the one with `targetId` / `linkPath` /
     `acceptPath` / `declinePath`) and pass
     `linkPath: $"/reports/mine#r-{report.Id}"` + `targetId: report.Id`.
     The `#r-{id}` fragment matches the anchor R06 already put on each
     content-report row (`id="r-{ReportId}"`), so the inbox row lands on
     the right row. The inbox renders `LinkPath` as a clickable link and
     the email carries it BaseUrl-prefixed (the existing
     `VerificationOptions.BaseUrl` precedent) — you only supply the
     same-origin relative path.

  **Leave the rest alone:** `recipientId`, `kind`, and the
  `idempotencyKey` stay exactly as they are (the key is the dedup anchor —
  changing it breaks `F10` re-emit idempotency). The `report.ReporterId`
  blank-guard stays.

- **`src/Kumunita.Core/Moderation/ModerationService.cs`** — the
  constructor currently is:
  ```csharp
  public ModerationService(
      IProfileSettingsService profileSettings,
      IAuthorizationService authz,
      IDocumentStore store,
      INotificationEmitter? notifications = null,
      IUserInfoService? userInfo = null,
      Posts.PostService? posts = null)
  ```
  If localizing the body at emit time needs a translation seam, add
  `ITranslationProvider? translationProvider = null` as a **trailing
  optional** parameter (the same `CS1736` trailing-param idiom the other
  optional seams use, so the existing positional test-construction call
  sites keep compiling) + a `_translationProvider` field. Resolve the
  reporter's language from the already-injected `_userInfo` (the
  `Profile.EmailLanguage` read) and call `_translationProvider.GetAsync`.
  When either seam is `null` (a test harness that omits them) degrade to
  a plain non-localized body (the raw outcome word) — never throw. If the
  outcome label you want is already a registered key you can reuse it
  directly; otherwise register new keys (below).

- **Translation keys** — R06 added a `reports.*` surface that already
  carries four terse outcome labels (`reports.outcome_noaction` /
  `_hidden` / `_removed` / `_other`, all four languages). For the
  **notification body** a full sentence reads better than the terse label,
  so you may add a small set of body keys — your call on the exact
  wording, but keep it in plain words (the R21 jargon pin) and in the
  `reports.*` surface so the `reports` file stays the single owner.
  Whichever keys you use must be present in **all four** languages
  (`en` / `de` / `fr` / `da`) in the same commit — `de` / `fr` / `da`
  are machine-written, flagged for the parked native review. (The
  `ResolutionNote` itself is never translated — it is appended verbatim.)

- **Tests** — add to the existing moderation service test file
  (`tests/Kumunita.Core.Tests/ModerationServiceTests.cs`, the file that
  already hosts the R05 `ListMyReportsAsync` seam tests). The house shape
  is the NSubstitute `INotificationEmitter` seam: capture the emission
  with `Arg.Do`/`Arg.Is` on the `EmitAsync` overload and assert:
  (a) `kind == NotificationKinds.ReportResolved` and
  `recipientId == report.ReporterId`; (b) **`linkPath` is exactly
  `/reports/mine#r-{reportId}`** and `targetId == reportId`; (c) the body
  reflects the **outcome** (not the reporter's own reason — the E4 "the
  notification repeats the reporter's own reason" finding this unit fixes)
  and, when a `ResolutionNote` was passed, carries it. One test per
  outcome value (or a theory over the four `ReportOutcome` values) + one
  for the `Outcome == null` no-outcome shape. The existing
  `ResolveReportAsync` seam tests in that file are the harness to copy
  (the `_notifications` substitute + the `IDocumentSession` /
  `IAuthorizationService` seams it already stands up).

Read first: the emission + the two `ResolveReportAsync` overloads in
[`ModerationService.ReportIntake.cs`](../../../src/Kumunita.Core/Moderation/ModerationService.ReportIntake.cs)
(the session-owning overload is the one to change) +
[`ModerationService.cs`](../../../src/Kumunita.Core/Moderation/ModerationService.cs)
(the constructor to extend); the emitter seam + the two `EmitAsync`
overloads in [`INotificationEmitter.cs`](../../../src/Kumunita.Core/Notifications/INotificationEmitter.cs)
(pick the overload that carries `linkPath`) + the `LinkPath` field's
contract in [`Notification.cs`](../../../src/Kumunita.Core/Notifications/Notification.cs);
the kind constant in [`NotificationKinds.cs`](../../../src/Kumunita.Core/Notifications/NotificationKinds.cs)
(`ReportResolved = "report.resolved"` — reuse, don't add); the
localization-at-emit precedent in
[`EventReminderService.cs`](../../../src/Kumunita.Core/Events/EventReminderService.cs)
(`BuildLocalizedReminder` — `IUserInfoService` + `ITranslationProvider` →
recipient's `EmailLanguage`); the `Report` doc's R03 resolution fields
(`Outcome` / `ResolutionNote`) in [`Report.cs`](../../../src/Kumunita.Core/Posts/Report.cs);
the `reports.*` keys R06 added (to reuse or extend) in
`src/Kumunita.Core/Localization/Keys/{en,de,fr,da}/reports.json`; and the
harness in
[`ModerationServiceTests.cs`](../../../tests/Kumunita.Core.Tests/ModerationServiceTests.cs).

Do **not**: change the `recipientId`, `kind`, or `idempotencyKey` of the
existing emission (only `body` + the new `linkPath` / `targetId`); add a
new notification kind (that is R08); translate the `ResolutionNote`
(free text, append verbatim); touch the "My reports" view or the two R05
read seams (R05/R06 are frozen); add hide/remove action on the target
(that is A02); or touch a second route.

Keep this to one seam and about four to five files (the two
`ModerationService` partials, the four `reports.json` files only **if**
you add new body keys, and the one test class).

**Known trips:**

- **Translation parity** — any new key needs `en` + `de` + `fr` + `da` in
  the same commit (the `C02_DataAsEmbeddedResourcesTests` /
  `KnownTranslationKeys_ParityTests` pins auto-check this; all four must
  carry the same set). A *new* `reports.*` key is additive (the surface
  already exists from R06), so no closed-set pin re-baseline is needed —
  but if a `…Parity…` test still fails with "stray key", re-baseline the
  pinned list + length guard and annotate the driving ADR; do **not**
  loosen the assertion.
- **Pre-existing reds (NOT yours):** `ImproveHarnessTests.
  CodeCheck_Gate_Passes` and three `SecGate.DataClassCoverageTests`
  are already failing on `development` from R05 (`ListMyReportsAsync`
  over the CC ceiling + boundary edges). They are **not** caused by R07 —
  don't re-baseline the ratchet for R07 and don't "fix" them here; they
  are R05's close-out debt. Run the narrow tests below, not the whole
  suite, so you don't chase them.

Run the narrowest relevant tests via the in-process runner (see
testing-and-ci.md for the exact invocation — the `-class` flag): the
`ModerationServiceTests` class (or just your new R07 tests) +
`KnownTranslationKeys_ParityTests` + `C02_DataAsEmbeddedResourcesTests`.
Then report changed files, verification results, the commit hash, and any
held questions. Make one commit for R07; do not commit unrelated work.
