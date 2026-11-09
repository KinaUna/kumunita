---
id: r04-prompt
title: "Prompt: IMPROVE-UX R04 resolve form"
tier: living
kind: plan
status: current
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/r03-prompt.md
  - docs/dev/testing-and-ci.md
  - docs/dev/razor-verification.md
lane: improve-ux
updated: 2026-11-09
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> R04. **Who is it for:** the next coding agent. **When to read the rest:**
> when starting R04 after R03.

# Fresh-context prompt: IMPROVE-UX R04

You are continuing the Kumunita repository on `development`. Implement
**IMPROVE-UX unit R04 only**: the resolve form — the outcome radio group
and the optional note textarea on `POST /moderation/{id}/resolve`, wired
through the controller to the R03 seam. R01 (the status page), R02 (the
re-execute gate), and R03 (the `Report` resolution shape + the optional
`outcome` / `resolutionNote` parameters on `ResolveReportAsync`) have
already landed — build on them, don't re-open them. Do not start R05 or any
later unit.

Before editing, read [AGENTS.md](../../../AGENTS.md),
[testing-and-ci.md](../../dev/testing-and-ci.md),
[razor-verification.md](../../dev/razor-verification.md), and the lane rules
plus the R04 card in [plan-improve-ux.md](plan-improve-ux.md). Check
`git status` and the current branch; preserve all existing work.

R04 is **Web-side** (`Kumunita.Web` controller + view + translation keys) —
the Core seam is already done (R03, commit `6aeecb20`). The R03 seam
signature is:

```csharp
// IModerationService / ModerationService (committed-lane overload):
Task ResolveReportAsync(
    string reportId,
    string globalAdminId,
    ReportOutcome? outcome = null,
    string? resolutionNote = null,
    CancellationToken ct = default);
```

`ReportOutcome` is `Kumunita.Core.Posts.ReportOutcome` — a four-value enum
(`NoAction` / `Hidden` / `Removed` / `Other`) already defined in
`src/Kumunita.Core/Posts/Report.cs`. The current
`ModerationController.ResolvePost` action calls the seam with two args
(`reportId`, `admin`) — that shape still compiles and is the pre-R04
"no outcome recorded" path. R04 changes that call to pass the form's
outcome + note when the user supplied them.

R04 scope:

- **View** (`Views/Moderation/Resolve.cshtml`, the `@if (Model.IsResolvable)`
  block): add an outcome radio group (four options: NoAction / Hidden /
  Removed / Other — the R03 enum values, rendered as `<input type="radio"
  name="outcome" value="NoAction">` etc.) and an optional note textarea
  (`name="resolutionNote"`, `maxlength="500"`). Labels must be
  **programmatic** (`<label for="...">` with matching `id`, or a wrapping
  `<label>` — the E15 a11y pin). The radio group and the textarea go
  **inside** the existing `<form method="post" action="…/resolve">` block,
  before the submit button. Do not change the assign or unlock forms.
- **Controller** (`ModerationController.ResolvePost`): read
  `Request.Form["outcome"]` and `Request.Form["resolutionNote"]` (both
  optional — empty/absent means the user left them blank, the pre-R04
  "no outcome recorded" shape). Parse the outcome with
  `Enum.TryParse<ReportOutcome>(value, ignoreCase: true, out var outcome)`
  (the `AdminController` precedent at line 513). Pass both to the seam
  call. The lane's own `ResolveReportNote` helper (R03) enforces the ≤500
  cap and throws `ArgumentException` on over-cap — the form's `maxlength`
  is the first line, the lane is the second. Do **not** add your own
  validation or error handling in the controller for the note length;
  the lane's exception is the contract.
- **View-model** (`ModerationResolveViewModel`): no new fields needed for
  R04 (the form is a blank form on a not-yet-resolved report; the
  "echo back the stored values on re-render" shape is R05's "My reports"
  page, not R04's). The existing `IsResolvable` predicate already gates
  which reports show the form (filed / assigned / unlocked, not resolved)
  — that gate is correct as-is.
- **Translation keys**: add the new label strings to
  `src/Kumunita.Core/Localization/Keys/{en,de,fr,da}/moderation.json`
  (the `moderation.resolve_*` family). New keys (en):
  `moderation.resolve_outcome_label` ("Outcome"),
  `moderation.resolve_outcome_noaction` ("No action"),
  `moderation.resolve_outcome_hidden` ("Hide the post"),
  `moderation.resolve_outcome_removed` ("Remove the post"),
  `moderation.resolve_outcome_other` ("Other (see note)"),
  `moderation.resolve_note_label` ("Note to the reporter (optional)"),
  `moderation.resolve_note_placeholder` ("What happened, in one or two sentences").
  de/fr/da are machine-written (flagged for the parked native review).
- **Tests**: a `ModerationControllerTests` case (the existing
  `Substitute.For<IModerationService>` seam-test harness, no
  Testcontainers) pinning that the `ResolvePost` action, when the form
  carries `outcome=Hidden` + `resolutionNote=…`, calls the seam with
  `outcome: ReportOutcome.Hidden` and the note verbatim; and that an
  empty form (no outcome, no note) calls the seam with both `null`
  (the pre-R04 shape). The Core-level R03 tests already pin what the
  seam does with those values — R04's test pins the controller's
  pass-through, not the seam's behavior.

Read first: [`ModerationController.cs`](../../../src/Kumunita.Web/Controllers/ModerationController.cs)
around the `Resolve` GET (≈ line 228) and `ResolvePost` POST (≈ line 477),
[`Resolve.cshtml`](../../../src/Kumunita.Web/Views/Moderation/Resolve.cshtml)
(the `@if (Model.IsResolvable)` block),
[`ModerationResolveViewModel.cs`](../../../src/Kumunita.Web/Models/ModerationResolveViewModel.cs),
[`ModerationControllerTests.cs`](../../../tests/Kumunita.Web.Tests/ModerationControllerTests.cs)
(the `Build` harness + the two existing 404 tests as the template), and
[`moderation.json`](../../../src/Kumunita.Core/Localization/Keys/en/moderation.json)
(the existing `moderation.resolve_*` keys).

Do **not**: execute any hide/remove action on the target (that is A02),
emit or change any notification (that is R07), add the "My reports" page
(that is R05–R06), or change the `ReportFiled` / `ReportResolved` emission
paths. No new Core document or service method is needed — R03's seam is
the only Core change.

Keep this to one seam and no more than about five files (the view, the
controller, the view-model if needed, the four `moderation.json` files,
and the test file). Follow the repository's existing Razor, controller,
and translation-key patterns.

Run the narrowest relevant tests/checks (the
`ModerationControllerTests` class at minimum; the translation-parity test
if the repo has one), then report changed files, verification results,
the commit hash, and any held questions. Make one commit for R04; do not
commit unrelated work.
