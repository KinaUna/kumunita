---
id: is36-prompt
title: "IS36 prompt — audit the unaudited admin reads (audit log, error reports, guests) (E7, D9)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS36 — Audit the unaudited admin reads (audit log, error reports, guests)

## What you are doing

You are the **thirty-seventh working agent** on the **IMPROVE-SEC** lane
(plan: `docs/plans-milestones/plan-improve-sec.md`). IS00–IS35 are committed.

**Finding E7 (severity M):** three `GlobalAdmin` reads of class (c) / PII write
**no `AccessAudit` row**. The plan's **IS36** section
(`docs/plans-milestones/plan-improve-sec.md`, see `#### IS36`) and the **D9**
decision row are the spec:

> `AdminController.Audit` (≈ :500), `ErrorReportAdminController`,
> `AdminGuestsController` — each writing **one `AccessAudit` row per view**
> (`Via: Admin`, `TargetKind` `audit-log` / `error-reports` / `guests`,
> `VisibleCount`).

D9's chosen shape is **one aggregate `AccessAudit` row per view** (not
per-row — noisy; not "accept unaudited"). The three views read data that is a
disclosure surface (the audit log itself, the resident-filed error reports,
the guest list) and currently leave no trail.

## The one thing to get right

**Each of the three view actions writes exactly ONE aggregate `AccessAudit`
row** — the `AdminAnalyticsController.Export` precedent (the ADR 0108
"portability.export" shape) verbatim, adapted to a *read* view rather than an
export:

- `At = DateTimeOffset.UtcNow`
- `ActorId = <the acting GlobalAdmin's ClaimTypes.Subject>`
- `Action = "<surface>.view"` (`audit-log.view` / `error-reports.view` /
  `guests.view`) — a *read* verb, not `.export`; keep the three `TargetKind`
  strings from D9 exact: `audit-log` / `error-reports` / `guests`.
- `TargetKind = <the D9 string>`; `TargetId = null` (an aggregate row — no
  single target, the `AccessAudit` aggregate shape: counts carry, id stays
  null).
- `VisibleCount = <the number of rows this view returned>` (the `rows.Count`
  / `model` row count the action is about to render); `HiddenCount = null`.
- `Via = AccessVia.Admin`; `Outcome = AccessOutcome.Allow` (a successful
  GlobalAdmin view — not a `Deny`; mirror `AdminAnalyticsController.Export`).

Write it through the same seam as the precedent:
`await using var session = store.LightweightSession(); session.Store(new
AccessAudit { … }); await session.SaveChangesAsync();` — no new `AccessVia`,
no new `TargetKind` concept, no new `IAuthorizationService` method (the
`AccessAuditFactory` "no new seam" shape). The audit write is a **side
effect** of the view action; the view's response is unchanged.

## The design decision to resolve (the one real call this unit makes)

The three controllers do **not** all have a store available, and that is the
seam to close cleanly:

- **`AdminController`** — **already** carries `IDocumentStore store` in its
  constructor (line ≈ 41) and has the `AdminSubjectId(ClaimsPrincipal)`
  helper (line ≈ 114). The `Audit` action (line ≈ 500) can write the row
  directly, mirroring the precedent. **No constructor change.**
- **`ErrorReportAdminController`** — built with services (`IErrorReportService`
  + the optional `ILocalizationService?` / `ITranslationProvider?` /
  `IEscalationForwarder?` seams) and has its own `ActorId(ClaimsPrincipal)`
  helper (line ≈ 80). It does **not** have a store.
- **`AdminGuestsController`** — built with `(IIdentityService identity)` only
  (line ≈ 34) and has its own `ActorId(ClaimsPrincipal)` helper (line ≈ 36).
  It does **not** have a store.

The clean path (the prompt's recommended option): **add an optional
`IDocumentStore? store = null` constructor parameter to the two controllers
that lack it**, following the **exact ADR 0015 D1 "kw-l floor" optional-
ctor-param idiom** the `ErrorReportAdminController` already uses for its
localization / forwarder seams — "optional (default null) so any test-
construction site that builds this controller without the seam keeps
compiling; DI always supplies the live store in the app." When `store` is
`null` (a test harness that doesn't supply it), the audit write is skipped
(a read is a no-op without a store, the "no disclosure" shape); when it is
supplied (the app, and the new tests), exactly one row is written.

Whichever you pick: **no new `AccessVia` value, no new `TargetKind` concept,
no new `IAuthorizationService` method** (the `AccessAuditFactory`
"single-target / aggregate row" shape). `AccessAudit` is already registered
via `CoreDocTypes` (verify it resolves in the test store before relying on
it). `VisibleCount` is a count, never a PII field (the IS08 gate applies to
anything logged).

## Constraints that keep existing tests green

- **No new `AccessVia`, `TargetKind` string, or `IAuthorizationService`
  method.** `AccessAudit` / `AccessAuditFactory` are frozen (IS16 / C3).
- **`VisibleCount` / `HiddenCount` are `int?`** on `AccessAudit` — set
  `VisibleCount` to the returned-row count, `HiddenCount = null`,
  `TargetId = null` (the aggregate shape, the `AccessAudit` class doc's
  "counts carry, target id stays null" rule).
- **Optional-ctor-param idiom (ADR 0015 D1):** the two new constructor
  parameters on `ErrorReportAdminController` and `AdminGuestsController` must
  default to `null` so the existing test-construction sites
  (`AdminErrorReportTests`, `AdminErrorReportDetailTests`,
  `AdminGuestsControllerTests`, and any other `new <Controller>(…)` in
  `tests/`) keep compiling **without** a store. The app (DI) supplies the
  live store.
- **`Outcome = Allow`** (a successful view), **not** `Deny` — mirror the
  precedent. A `Deny` row means the actor was refused, which is not this
  case.
- **PII rule:** the audit row carries only counts + the acting subject id —
  never a resident email, body, or report content (the IS08 gate;
  `ErrorReport.ContactEmail` must never leak into the row or a log).
  Tests use synthetic fixture ids (`user-a`), `a@example.test`-style
  addresses, and no real content.
- **`PortabilityDocTypes` (M11) untouched** — no doc-type surface added.
- **No re-audit of the U08 `AccessVia` deny matrix** — this unit only adds
  the *audit rows for three admin reads*; it does not change the decision
  matrix or add a new `AccessVia` value.

## Deliverables (≈ 3–4 files)

1. **`src/Kumunita.Web/Controllers/AdminController.cs`** — the `Audit`
   action (line ≈ 500) writes one `AccessAudit` row (`TargetKind
   "audit-log"`, `Action "audit-log.view"`, `VisibleCount = rows.Count`,
   `Via: Admin`, `Outcome: Allow`) before `return View(…)`. The store +
   `AdminSubjectId` are already on the class.
2. **`src/Kumunita.Web/Controllers/ErrorReportAdminController.cs`** — add
   `IDocumentStore? store = null` to the constructor (the ADR 0015 D1
   optional-param idiom); the `Index` (list, line ≈ 135) and/or `Detail`
   (line ≈ 222) view action(s) — **pick the action(s) that read the
   resident-filed reports; the prompt's D9 row says the view; the minimum is
   `Index`** — write one `AccessAudit` row (`TargetKind "error-reports"`,
   `Action "error-reports.view"`, `VisibleCount = <rows returned>`,
   `Via: Admin`, `Outcome: Allow`). Guard: only write when `store is not
   null`.
3. **`src/Kumunita.Web/Controllers/AdminGuestsController.cs`** — add
   `IDocumentStore? store = null` to the constructor (the same idiom); the
   `Index` action (line ≈ 47) writes one `AccessAudit` row (`TargetKind
   "guests"`, `Action "guests.view"`, `VisibleCount = <rows returned>`,
   `Via: Admin`, `Outcome: Allow`). Guard: only write when `store is not
   null`.
4. **`tests/Kumunita.Web.Tests/AdminReadAuditTests.cs`** (new) — the three
   tests below. The setup idiom mirrors `AdminAnalyticsControllerTests`
   (NSubstitute `IDocumentStore` → shared `IDocumentSession` for
   `LightweightSession()`, `Arg.Is<AccessAudit>(…)` assertions on the exact
   row shape, a `DefaultHttpContext` whose `User` carries the GlobalAdmin
   claim).

## Entry reads (only these — no whole-file reads)

- `sed -n '100,145p' src/Kumunita.Web/Controllers/AdminAnalyticsController.cs`
  (the **precedent** — the `Export` action + the one `AccessAudit` row via
  `store.LightweightSession()`, `TargetKind "analytics"`, `Action
  "analytics.export"`, `Via: Admin`, `Outcome: Allow`, the
  `AdminSubjectId(User)` actor read). **This is the shape to mirror.**
- `sed -n '495,545p' src/Kumunita.Web/Controllers/AdminController.cs` (the
  `Audit` action you extend — note it opens `store.OpenSession(new
  Marten.Services.SessionOptions())` for the *read*; the audit write is a
  second `store.LightweightSession()` block, the precedent's shape; the
  `rows.Count` is your `VisibleCount`) and `sed -n '38,50p'` +
  `sed -n '110,120p'` (the ctor's `IDocumentStore store` param + the
  `AdminSubjectId` helper).
- `sed -n '55,90p' src/Kumunita.Web/Controllers/ErrorReportAdminController.cs`
  (the constructor you extend with the optional `IDocumentStore?` + the
  `ActorId` helper) and `sed -n '130,150p'` + `sed -n '218,232p'` (the
  `Index` / `Detail` actions — pick the one(s) D9 names).
- `sed -n '30,65p' src/Kumunita.Web/Controllers/AdminGuestsController.cs`
  (the `(IIdentityService identity)` constructor you extend + the `Index`
  action).
- `sed -n '1,70p' src/Kumunita.Core/Authorization/AccessAudit.cs` (the doc's
  fields: `At`, `ActorId`, `Action`, `TargetKind`, `TargetId`,
  `VisibleCount` / `HiddenCount` (both `int?`), `Via`, `Outcome` — and the
  "aggregate row: counts carry, target id stays null" rule).
- `sed -n '1,30p' src/Kumunita.Core/Authorization/Decision.cs` (the
  `AccessVia` enum — confirm `Admin` is a member; you use it, you do **not**
  add a new value).
- `sed -n '1,60p' tests/Kumunita.Web.Tests/AdminAnalyticsControllerTests.cs`
  (the **test setup idiom** to mirror — the `Build()` returning
  `(controller, analytics, store, session)`, the NSubstitute
  `store.LightweightSession().Returns(session)`, the `Arg.Is<AccessAudit>(a
  => a.TargetKind == "analytics" && a.Action == "analytics.export" …)`
  assertion, the `DefaultHttpContext.User = new ClaimsPrincipal(…)` actor).
- `sed -n '1,40p' tests/Kumunita.Web.Tests/AdminGuestsControllerTests.cs` +
  `sed -n '1,40p' tests/Kumunita.Web.Tests/AdminErrorReportTests.cs` (the
  existing constructor calls you must **not break** — confirm they build the
  controller with **no** store, so your optional param must default to
  `null`).
- Doc rules + the per-unit contract (D8): `docs/plans-milestones/
  plan-improve-sec.md` (the **IS36** section + the **D9** decision row) +
  `docs/DOCS-CONVENTIONS.md`.
- Test-runner form: `docs/archive/lanes/sec/sec-handoff-notes.md` (SEC U3 —
  xunit.v3 in-process `-class`, **not** `dotnet test --filter`).

## Tests (new file `AdminReadAuditTests.cs`)

- `AuditLogView_IsItselfAudited` — build `AdminController` with a
  substitute `IDocumentStore` (the `AdminAnalyticsControllerTests` idiom) +
  a `DefaultHttpContext` whose `User` carries the GlobalAdmin claim; call
  the `Audit` action; assert **exactly one** `AccessAudit` was stored with
  `TargetKind == "audit-log"`, `Action == "audit-log.view"`, `Via ==
  AccessVia.Admin`, `Outcome == AccessOutcome.Allow`, and `VisibleCount`
  equal to the page size the action rendered (the `rows.Count`, `const int
  page = 50` in the action).
- `ErrorReportView_IsAudited` — build `ErrorReportAdminController` with the
  optional `store` param set (a substitute) + the required
  `IErrorReportService` (NSubstitute) + the actor `HttpContext`; call
  `Index`; assert exactly one `AccessAudit` with `TargetKind ==
  "error-reports"`, `Action == "error-reports.view"`, `Via: Admin`,
  `Outcome: Allow`, `VisibleCount == <rows>`.
- `GuestListView_IsAudited` — build `AdminGuestsController` with the
  optional `store` param set + `IIdentityService` (NSubstitute) + the actor
  `HttpContext`; call `Index`; assert exactly one `AccessAudit` with
  `TargetKind == "guests"`, `Action == "guests.view"`, `Via: Admin`,
  `Outcome: Allow`, `VisibleCount == <rows>`.

Each test should also assert the **negative** shape (the `AdminAnalyticsControllerTests`
`session.DidNotReceive().Store(Arg.Is<AccessAudit>(a => a.TargetKind != …))`
line) so a stray second row or a wrong `TargetKind` fails the pin.

## Verification

- [ ] Build green (`dotnet build`).
- [ ] The 3 new tests green — run the new class: `dotnet exec
      tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll -class
      "Kumunita.Web.Tests.AdminReadAuditTests"` → **Total: 3, Failed: 0,
      Errors: 0**.
- [ ] **No regression** on the existing constructor-call sites you may have
      touched — re-run the three existing test classes: `dotnet exec
      tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll -class
      "Kumunita.Web.Tests.AdminGuestsControllerTests" -class
      "Kumunita.Web.Tests.AdminErrorReportTests" -class
      "Kumunita.Web.Tests.AdminAnalyticsControllerTests"` → all still green
      (they build the controllers with **no** store, so your optional param
      must default to `null` and the audit write must be skipped cleanly).
- [ ] The three view actions each write **exactly one** `AccessAudit` row
      (`TargetId = null`, `HiddenCount = null`, `VisibleCount = <count>`,
      `Via: Admin`, `Outcome: Allow`) — no second row, no per-row rows.
- [ ] No new `AccessVia` value, no new `TargetKind` string, no new
      `IAuthorizationService` method. `AccessAudit` / `AccessAuditFactory`
      unchanged. `PortabilityDocTypes` (M11) unchanged.
- [ ] `VisibleCount` is a count, never a PII field; no resident email, body,
      or report content in the row or a log (the IS08 gate).
- [ ] One `## IS36` section appended to
      `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`
      (files, the exact `Action` / `TargetKind` strings, the optional-ctor-
      param seam decision + which actions you wired, the test + pass count,
      next-unit notes).

## Definition of done (IS36)

- [ ] `AdminController.Audit`, `ErrorReportAdminController` (the D9-named
      view), and `AdminGuestsController.Index` each write **exactly one**
      aggregate `AccessAudit` row (`Via: Admin`, `TargetKind`
      `audit-log` / `error-reports` / `guests`, `VisibleCount = <rows>`,
      `Outcome: Allow`, `TargetId = null`) — the `AdminAnalyticsController.
      Export` precedent adapted to a read view.
- [ ] The two controllers that lack a store gain an **optional**
      `IDocumentStore?` constructor param (the ADR 0015 D1 idiom) so the
      existing test sites keep compiling; the app (DI) supplies the live
      store. No other constructor change.
- [ ] The 3 new tests green + the three existing constructor-call-site test
      classes still green (no regression); build green.
- [ ] No new `AccessVia` / `TargetKind` / `IAuthorizationService` seam.
      `AccessAudit` / `AccessAuditFactory` / `PortabilityDocTypes` (M11)
      unchanged.
- [ ] No PII in the row or a log (counts only). One commit, one handoff
      section.
- [ ] **Next unit: IS37** (the destructive-confirm views move to the existing
      `form[data-confirm]` module — `Notes/Index` (2), `Bookmarks/Index` (1),
      `AdminSurfaceLabels/Index` (1), `AdminHelp/Index` (2) — the 8 inline
      `on*` handlers E9 found that `script-src 'self'` silently blocks).
