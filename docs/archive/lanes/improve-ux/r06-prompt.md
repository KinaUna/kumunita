---
id: r06-prompt
title: "Prompt: IMPROVE-UX R06 My-reports page"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/r05-prompt.md
  - docs/dev/testing-and-ci.md
  - docs/dev/razor-verification.md
lane: improve-ux
updated: 2026-11-09
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> R06. **Who is it for:** the next coding agent. **When to read the rest:**
> when starting R06 after R05.

# Fresh-context prompt: IMPROVE-UX R06

You are continuing the Kumunita repository on `development`. Implement
**IMPROVE-UX unit R06 only**: the resident's **`GET /reports/mine`** page —
the "My reports" surface (the D1 shape from the lane plan) with two
sections (content reports, platform issues), a friendly empty state, and a
link in the account menu. R01 (the status page), R02 (the re-execute gate),
R03 (the `Report` resolution shape), R04 (the resolve form), and R05 (the two
Core read seams) have already landed — build on them, don't re-open them. Do
not start R07 or any later unit.

Before editing, read [AGENTS.md](../../../../AGENTS.md),
[testing-and-ci.md](../../../dev/testing-and-ci.md),
[razor-verification.md](../../../dev/razor-verification.md), and the lane rules
plus the R06 card in [plan-improve-ux.md](plan-improve-ux.md). Check
`git status` and the current branch; preserve all existing work.

R06 is **Web-side** (`Kumunita.Web` controller + view + translation keys +
the account-menu link) — the Core read seams are already done (R05, commit
`aaffc78b`). The two seams to call:

```csharp
// IModerationService (Kumunita.Core.Moderation) — the reporter's content reports:
Task<IReadOnlyList<MyReportRow>> ListMyReportsAsync(string reporterId, CancellationToken ct = default);

// IErrorReportService (Kumunita.Core.ErrorReports) — the filer's platform issues
// (returns the raw ErrorReport docs — no read-model record):
Task<IReadOnlyList<ErrorReport>> ListMineAsync(string subjectId, CancellationToken ct = default);
```

`MyReportRow` (`Kumunita.Core.Moderation.ModerationReadModels`) fields:
`ReportId`, `PostId`, `ComponentId`, `Reason`, `Status` (a closed string —
`filed` / `assigned` / `unlocked` / `resolved`), `At`, `TargetKind` (null =
the original post shape), `TargetTitle` (**nullable** — null when the
reporter can no longer read the target), `Outcome` (`Kumunita.Core.Posts.
ReportOutcome?`, the four-value enum `NoAction`/`Hidden`/`Removed`/`Other`),
`ResolutionNote`, `ResolvedAt`, `ResolvedBy`. `ErrorReport`
(`Kumunita.Core.ErrorReports`) fields the page shows: `Id`, `Description`,
`Created`, `Origin`, `TriageStatus` (a closed string — `new` / `triaged` /
`resolved`), `ResolvedAt`, `ResolvedBy`, `ResolutionNote`.

The reporter's / filer's own id is the signed-in subject — resolve it the
house way (`KumunitaPrincipal.SubjectId(User)`, see
`BookmarksController` / `IssueController`) and pass it to **both** seams.
The page is a **personal, signed-in** surface: `[Authorize]`, and a caller
with no `SubjectId` gets a non-leaky 404 (the `BookmarksController` /
`ErrorReportAdminController` "operator has no read standing" shape) —
**never** a 500, never the list for another resident.

R06 scope:

- **Controller** — a new `ReportsMineController` (route `[Authorize]`,
  `[HttpGet("/reports/mine")]`), or a single `GET /reports/mine` action on a
  suitably-named controller — your call, but one action, one route. Inject
  `IModerationService` + `IErrorReportService` (both already registered in
  Core DI), plus the optional `ILocalizationService?` /
  `ITranslationProvider?` pair (the house kw-l floor, the
  `BookmarksController` / `ErrorReportAdminController` idiom). Resolve the
  subject id; if blank, `return NotFound()`. Otherwise call
  `ListMyReportsAsync(subjectId)` and `ListMineAsync(subjectId)`, and pass
  both lists to the view (a small view-model holding the two lists + a
  derived `HasAny` is fine). A **read, not a decision** — no
  `IAuthorizationService` call, no `AccessAudit` row (R05's seams already
  pin that).
- **View** — `Views/ReportsMine/Index.cshtml` (or a folder that matches the
  controller name). Two stacked sections, each a simple table/list:
  1. **Content reports** — one row per `MyReportRow`: the
     `TargetTitle` (fall back to a localized "(unavailable)" when null —
     the R05 "no longer visible" shape, **not** a link), a localized `Status`
     label, a localized `Outcome` label (map the enum; when `Outcome` is
     null render the localized "no outcome recorded" shape), the
     `ResolutionNote` (verbatim, when present), and the filed `At`.
  2. **Issues** — one row per `ErrorReport`: the `Description`, a localized
     `TriageStatus` label, the `ResolutionNote` (verbatim, when present),
     and the `Created` instant.
  - **Anchor ids for R07/R08:** give each content-report row
    `id="r-{ReportId}"` and each issue row `id="i-{Id}"` — R07's
    `ReportResolved` link (`/reports/mine#r-{id}`) and R08's `issue.resolved`
    link (`/reports/mine#i-{id}`) will land on these. Do it now so the
    follow-on units don't have to refactor.
  - **Friendly empty state:** when the subject has no reports *and* no
    issues, render one localized friendly line (not a blank page, not an
    error) — the E16 "one next action" shape. A section that's empty but the
    other isn't just hides that section's table.
  - **Labels must be programmatic** (`<label for>` / wrapping `<label>` —
    the E15 a11y pin); **no inline `on*` handler or `<script>` block**
    (the SEC gate in `tests/Kumunita.Web.Tests/SecGate/`).
- **Account-menu link** — add one `<li>` to the signed-in resident's
  account dropdown in [`_AccountNav.cshtml`](../../../../src/Kumunita.Web/Views/Shared/_AccountNav.cshtml)
  (the resident section, beside "My drafts" / "Settings" — **not** in the
  GlobalAdmin / moderator "Management" section, this is a resident's own
  surface). A static anchor `href="/reports/mine"` + a `<kw-l key="…">` label
  + the existing `<svg class="kmb-nav-icon kmb-icon" …><use href="#kmb-i-…"/></svg>`
  icon pattern (reuse an existing `#kmb-i-*` glyph; do not add a new icon).
  The link is **always** rendered for a signed-in resident (unlike the
  message-allowance / messaging gates).
- **Translation keys** — add a `reports.*` family to
  `src/Kumunita.Core/Localization/Keys/{en,de,fr,da}/reports.json` (a new
  file — no `reports.json` exists yet; `errorreport.json` and
  `moderation.json` are the neighbors). New en keys (the exact wording is
  yours, keep it in plain words — the R21 jargon pin): `reports.title`
  ("My reports"), `reports.reports_section` ("Content reports"),
  `reports.issues_section` ("Issues"), `reports.empty` ("No reports yet —
  when you report a post or an issue, the outcome lands here."), the
  column/field labels you render (status, target, reason/description,
  filed, outcome, note), the outcome labels (you may reuse the existing
  `moderation.resolve_outcome_*` values or add `reports.outcome_*` — your
  call, but pick **one** and use it consistently), and the
  `reports.no_outcome` + `reports.target_unavailable` shapes. de/fr/da are
  machine-written (flagged for the parked native review).
- **Tests** — a `ReportsMineControllerTests` class in
  `tests/Kumunita.Web.Tests/` using the house seam-test harness
  (`Substitute.For<IModerationService>()` +
  `Substitute.For<IErrorReportService>()`, no Testcontainers — the
  `AdminErrorReportDetailTests.cs` / `ModerationControllerTests.cs` shape).
  Pins: (a) signed-in subject → both seams are called with that subject id
  and the view renders (assert the view name / model carries both lists);
  (b) a blank subject id (no `SubjectId`) → a 404, and **neither** seam is
  called (the non-leaky gate); (c) both seams return empty → the friendly
  empty state renders (assert the model / a marker). Assert the seam call
  arguments, not the HTML body.

Read first: the R05 seams — [`IModerationService.cs`](../../../../src/Kumunita.Core/Moderation/IModerationService.cs)
(`ListMyReportsAsync` + the `MyReportRow` row shape) and
[`ModerationReadModels.cs`](../../../../src/Kumunita.Core/Moderation/ModerationReadModels.cs),
[`IErrorReportService.cs`](../../../../src/Kumunita.Core/ErrorReports/IErrorReportService.cs)
(`ListMineAsync`) + [`ErrorReport.cs`](../../../../src/Kumunita.Core/ErrorReports/ErrorReport.cs)
(the doc's field set) — then the Web pattern to mirror:
[`BookmarksController.cs`](../../../../src/Kumunita.Web/Controllers/BookmarksController.cs)
(the `[Authorize]` + `KumunitaPrincipal.SubjectId(User)` + optional kw-l floor
+ `T(key)` helper) and [`Bookmarks/Index.cshtml`](../../../../src/Kumunita.Web/Views/Bookmarks/Index.cshtml)
(a simple personal list view), [`_AccountNav.cshtml`](../../../../src/Kumunita.Web/Views/Shared/_AccountNav.cshtml)
(the resident account dropdown where the link goes),
[`moderation.json`](../../../../src/Kumunita.Core/Localization/Keys/en/moderation.json)
+ [`errorreport.json`](../../../../src/Kumunita.Core/Localization/Keys/en/errorreport.json)
(the existing `moderation.resolve_outcome_*` / `errorreport.list.*` keys to
reuse or echo), and a Web seam-test to copy the harness from:
[`AdminErrorReportDetailTests.cs`](../../../../tests/Kumunita.Web.Tests/AdminErrorReportDetailTests.cs)
(the `Substitute.For<IErrorReportService>` shape) or
[`ModerationControllerTests.cs`](../../../../tests/Kumunita.Web.Tests/ModerationControllerTests.cs)
(the `Substitute.For<IModerationService>` shape).

Do **not**: emit or change any notification (that is R07 — the
`ReportResolved` body + link — and R08 — the new `issue.resolved` kind),
add any hide/remove action on the target (that is A02), change the two
R05 read seams or the `Report` / `ErrorReport` doc shapes (R03/R05 are
frozen), or add a second route. The only Core surface R06 touches is the
**translation-key JSON** — no new Core service method, no new document.

Keep this to one seam and no more than about six to seven files (the
controller, the view, the account-nav partial, the four `reports.json`
files, and the one test class — the view-model is optional inline). Follow
the repository's existing Razor, controller, and translation-key patterns.

**Known trip (translation keys):** a subset of tests pin an *exact closed
set* of keys in certain namespaces (see the lane's testing doctrine + the
`improve-check.ps1` / closed-set pins). A **new** `reports.*` namespace is
additive and should be fine, but if any `…Parity…` / closed-set test fails
with a "stray key" or "set changed" after you add keys, re-baseline the
pinned list + length guard (and annotate the driving ADR) — do **not**
loosen the assertion. New keys also need `en` + `de` + `fr` + `da` parity
(the translation-parity test), all four in the same commit.

Run the narrowest relevant tests/checks (the new
`ReportsMineControllerTests` class + the translation-parity test, both via
the in-process runner — see testing-and-ci.md for the exact invocation),
then report changed files, verification results, the commit hash, and any
held questions. Make one commit for R06; do not commit unrelated work.
