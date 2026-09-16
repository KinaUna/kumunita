# GA U01 — Design doc + ADR 0038 (the pinned contract)

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained: it names every entry read, every deliverable, and the exit
> criteria. The register (`docs/plans-milestones/in-progress/
> plan-guardian-assignment.md`) is the cross-reference; when the two disagree,
> **this file wins for what to do** and the register wins for *which files
> exist and in what order*.

## Goal

Author **two** new files in one pass: (1)
`docs/design/guardian-assignment-design.md` — the lane's invariants
(G-A·1–G-A·6), the FACES, the **pinned contract** (the exact
`IIdentityService.FindSubjectByEmailAsync` seam + the
`GuardianController.Assign` action's exact signature + the
`AssignGuardianForm` shape + the `Detail.cshtml` changes), the **pinned
seam-test names**, the **acceptance gate**, and the **drift-guard**; and
(2) `docs/adr/0038-guardian-assignment.md` — the named decision (the
AGENTS.md ADR rule: "a new capability that settles a design question gets
an ADR"). Also append the `0038` row to `docs/adr/README.md`. **No code, no
build.**

## Context you need (read these first, in this order)

1. `docs/adr/0028-guardian-controls-account-scope-supervision.md` — the
   **primary** authority. Read the **§B** (the relationship: "a child may
   have one or two guardians (each an active row)" — the precedent this
   lane's scope relies on), the **§D G·4** (the non-decision "no self-serve
   claim guardianship over an existing account" — the precedent this ADR
   **supersedes** for the *existing active guardian* case), and the **§E**
   (the audit verbs + the non-decisions the GA lane inherits). This is the
   authority U01's ADR 0038 amends.
2. `docs/design/guardian-controls-design.md` — the GU lane's design doc.
   Read the **`## Context`** (the GU lane's framing), the **`## Invariants`**
   (G·1–G·5 — the GA lane inherits G·1 + G·2 + G·4), the **`## Pinned
   contract`** (the GU lane's seam shapes — the `CreateGuardianLinkAsync`
   idempotency + the `guardian.create` audit shape the GA lane reuses), and
   the **`## Acceptance gate`** (the three-test shape U01's design doc
   mirrors). This is the **template** U01's design doc emulates.
3. `src/Kumunita.Core/Identity/IIdentityService.cs` — the **frozen surface**
   + the M1 lifecycle ADDs (the `ResendVerificationEmailAsync` shape the new
   `FindSubjectByEmailAsync` seam mirrors — a read over
   `userManager.FindByEmailAsync`). Note the `// ── M1 lifecycle (ADR 0006-E
   compatible lane) ──` section header where the new seam goes.
4. `src/Kumunita.Core/Identity/IdentityService.cs` — the
   `userManager.FindByEmailAsync` usage (the `ResendVerificationEmailAsync`
   precedent at ~line 137: `var user = await
   userManager.FindByEmailAsync(email);`). The new seam wraps this one call.
5. `src/Kumunita.Core/UserInfo/IUserInfoService.cs` §`CreateGuardianLinkAsync`
   (~line 788) — the seam the Web action calls. Read the doc-comment (the
   idempotent upsert + the `guardian.create` audit + the G·4 creation-based
   standing). The GA lane **reuses** this seam unchanged.
6. `src/Kumunita.Web/Controllers/GuardianController.cs` — the existing
   `ActiveLinkAsync` helper (~line 490) the standing gate reuses; the
   `AddChild` action (~line 320) the new `Assign` action's failure shape
   mirrors (the `ModelState.AddModelError` + the `View(form)` re-render + the
   `TempData["info"]` on success); the `ActiveChildrenAsync` helper (~line
   460) the new `ActiveGuardiansAsync` helper mirrors (inverted).
7. `src/Kumunita.Web/Models/GuardianViewModels.cs` — the existing GU VMs.
   Read the `ChildAccountItem` record (the shape the new `GuardianItem`
   mirrors), the `MembershipEditorModel` record (the shape the new
   `GuardianItems` field appends to), and the `AddChildForm` class (the
   shape the new `AssignGuardianForm` mirrors — a single email field, the
   `[Required, EmailAddress]` validation).
8. `src/Kumunita.Web/Views/Guardian/Detail.cshtml` — the existing curation
   view. Note the existing `<h2>` + `<form>` + `<ul class="list-group">`
   patterns the two appends match.
9. `docs/adr/README.md` — the ADR index table. Note the `0037` row (the
   precedent for the `0038` row U01 appends).
10. `docs/plans-milestones/done/guardian-controls/plan-guardian-controls.md`
    — the GU lane's register. Read the **`## Workflow`** (the three-tier
    contract + the unit-series rules) and the **`### U01`** section (the
    "design doc + ADR" unit shape U01 mirrors, adapted for the fact that
    here the design doc does not yet exist).

## Deliverables (3 files)

### 1. `docs/design/guardian-assignment-design.md` (new, ~300–450 lines)

The **primary** tier. Sections, in order:

- `## Context` — the GU lane shipped (ADR 0028); the `GuardianLink` model
  already supports "one or two guardians" (ADR 0028 §B); the only path to
  `CreateGuardianLinkAsync` today is the GU formation lane (paired with
  `RegisterAsync` in `GuardianController.AddChild`); the gap: a second
  guardian cannot be assigned to an *existing* child; ADR 0028 G·4's
  "no self-serve claim" non-decision is the precedent this lane supersedes
  for the *existing active guardian* case. The arrow moved (supervision →
  co-supervision). The platform is invitation-only, one neighborhood,
  privacy-first — the assigned guardian's standing is **identical in kind**
  to the creator's (the five GU actions, no content read).
- `## Scope` — **In:** (1)
  `IIdentityService.FindSubjectByEmailAsync` ADD (the email → subjectId
  seam); (2) `GuardianController.Assign` action (`POST
  me/children/{childId}/assign`) — standing gate + resolve + refuse
  self/duplicate + `CreateGuardianLinkAsync`; (3)
  `AssignGuardianForm` view model + the `GuardianItem` record + the
  `MembershipEditorModel.GuardianItems` field; (4) the `Detail.cshtml` two
  appends (the "other guardians" list + the assign form) + the
  localization keys; (5) ADR 0038; (6) the design doc's pinned contract +
  seam tests + gate; (7) the README / `Milestones.cs` / `MilestonesTests.cs`
  trio flip (U07). **Out (named deferrals, ADR 0038 §E):** no remove path
  (a co-guardian dissolving *another* co-guardian's link, or the child
  dissolving a co-guardian's link — the ADR 0028 G·5 safety valve remains
  the only "dissolve any active link" path); no acceptance/consent step on
  the assigned guardian; no bulk assign (one email per form); no
  self-assignment (refused — G-A·5); no second audit verb (`guardian.create`
  is the one verb); no email notification to the assigned guardian.
- `## Invariants (pinned for GA)` — six invariants, each with a one-line
  GA note:
  - **G-A·1** — Standing is from the **assigning** guardian's active
    `GuardianLink` over the child, checked **live** (the GU lane's G·2
    precedent; the `ActiveLinkAsync` helper the `Dissolve` route already
    uses). A non-guardian → 404 (the ADR 0012/0013 "a non-guardian learns
    nothing" shape).
  - **G-A·2** — The assigned guardian **must have an account** on the
    platform. The email → subjectId resolution returns null for an unknown
    email; the Web surfaces a user-presentable error, never a 500, never an
    auto-create (the GU lane's G·4 "formation is creation-based" precedent —
    the assign lane does **not** create accounts; it assigns standing over
    an *existing* one).
  - **G-A·3** — The assigned guardian's standing is **identical in kind**
    to the creator's — the five GU actions, no content read, no "assigned"
    tier. The GA lane does **not** touch `IAuthorizationService`, does not
    touch `AccessVia`, does not touch the GU enforcement path. The
    `GuardianLink` POCO is **byte-identical** (no new field, no new enum
    value, no renumbering). (Inherits G·1 from ADR 0028.)
  - **G-A·4** — **Idempotency** (the GU lane's G·4 precedent, inherited):
    a duplicate `(GuardianId, ChildId)` active row is a no-op — the row is
    left as-is, no second audit row. The `CreateGuardianLinkAsync` seam
    already enforces this; the GA lane does not re-implement it.
  - **G-A·5** — **Self-assignment is refused**: a guardian cannot assign
    *themselves* as a second guardian over a child — the `(actorId, childId)`
    pair is the same as the `(assignedId, childId)` pair, which is the GU
    formation lane's territory, and `CreateGuardianLinkAsync` is already
    idempotent for it (a no-op, not a useful act). The Web surfaces a
    user-presentable error, never a 500.
  - **G-A·6** — **No remove path** (a named deferral, not a denial): the
    GA lane does **not** add a "remove a co-guardian" surface. The ADR
    0028 G·5 safety valve (a GlobalAdmin dissolving any active link,
    audited `Via: Admin`) remains the **only** "dissolve any active link"
    path. A co-guardian dissolving *another* co-guardian's link, or the
    child dissolving a co-guardian's link, is a **future ADR 0038
    amendment**.
- `## FACES (pinned, 6)` — F1–F6, each bound to an invariant:
  - **F1** a non-guardian cannot assign (404) — G-A·1
  - **F2** an unknown email is refused (user-presentable error, no
    auto-create) — G-A·2
  - **F3** the assigned guardian gets the five GU actions on their next
    read — G-A·3
  - **F4** a duplicate assignment is a no-op (no second audit row) —
    G-A·4
  - **F5** self-assignment is refused (user-presentable error) — G-A·5
  - **F6** no remove path (the ADR 0028 G·5 safety valve is the only
    dissolve path) — G-A·6
- `## Pinned contract (U01 — finalizes for U02–U06)` — the exact C# U02–U06
  must match, verbatim:
  - `### IIdentityService.FindSubjectByEmailAsync (exact C#)` — the **one
    new ADD** on the frozen `IIdentityService` surface (the ADR 0006-E
    compatible lane; the M1 lifecycle ADD precedent). The exact seam:
    `Task<string?> FindSubjectByEmailAsync(string email);` with a
    doc-comment: "GA (ADR 0038): resolve an email to a subject id (the
    assign form's one external identifier). A read — no audit row, no
    mutation. Returns the subject id, or null if the email has no account
    (the Web's user-presentable error surface — the ADR 0008 'a non-guardian
    learns nothing' shape: a null return, not an exception that names the
    email). ADR 0006-E compatible ADD — the M1 lifecycle ADD precedent
    (the `ResendVerificationEmailAsync` shape, a read over
    `userManager.FindByEmailAsync`)."
  - `### GuardianController.Assign (exact C#)` — the **one new action** on
    the existing `GuardianController`. The exact signature: `[HttpPost(
    "{childId}/assign")] [ValidateAntiForgeryToken] public async
    Task<IActionResult> Assign(string childId, [FromForm]
    AssignGuardianForm form);` with a doc-comment: "GA (ADR 0038): assign a
    second guardian to this child. The standing gate (G-A·1 — the
    `ActiveLinkAsync` helper the `Dissolve` route already uses) runs first;
    a non-guardian → 404. The resolution (G-A·2 — the
    `FindSubjectByEmailAsync` seam) runs second; null → the form's error
    surface. Self-assignment (G-A·5) + duplicate-assignment (G-A·4) are the
    third step. The `CreateGuardianLinkAsync` call is the fourth — one
    commit, one `guardian.create` audit row (C3), the **existing** GU seam
    (no new Core seam)."
  - `### AssignGuardianForm (exact C#)` — the **one new view model** (the
    `AddChildForm` shape to mirror). `public sealed class
    AssignGuardianForm { [Required, EmailAddress, MaxLength(255)]
    [Display(Name = "Email of the guardian to assign")] public string?
    Email { get; set; } }`.
  - `### GuardianItem (exact C#)` — the **one new record** (the
    `ChildAccountItem` shape to mirror). `public sealed record
    GuardianItem(string SubjectId, string DisplayName);`.
  - `### MembershipEditorModel.GuardianItems (exact C#)` — the **one new
    field** on the existing `MembershipEditorModel` record.
    `IReadOnlyList<GuardianItem> GuardianItems` appended as the **last**
    parameter in the record's parameter list.
  - `### Detail.cshtml changes (exact markup)` — the **two appends** to the
    existing `Views/Guardian/Detail.cshtml` (the "other guardians" list +
    the assign form; the existing curation sections are **untouched**). The
    exact markup is in the register's U01 §Pinned contract (the `<h2>` +
    `<ul>` + `<form>` shapes, the `<kw-l>` TagHelper usage, the
    `Url.Action` routes).
  - `### Pinned seam tests (exact names)` — the file
    `tests/Kumunita.Core.Tests/GuardianAssignmentTests.cs` (U03 authors)
    with exactly these **3**:
    1. `FindSubjectByEmail_ReturnsSubjectIdForKnownEmail`
    2. `FindSubjectByEmail_ReturnsNullForUnknownEmail`
    3. `FindSubjectByEmail_IsCaseInsensitiveOnEmail`
    And the file `tests/Kumunita.Web.Tests/GuardianAssignmentTests.cs`
    (U06 authors) with exactly these **5**:
    4. `Assign_NonGuardian_Returns404`
    5. `Assign_UnknownEmail_ReturnsValidationError`
    6. `Assign_SelfAssignment_ReturnsValidationError`
    7. `Assign_DuplicateAssignment_IsIdempotentNoOp`
    8. `Assign_KnownEmail_CallsCreateGuardianLinkAsync`
  - `### Acceptance gate (U07 records)` — the three tests: **closed loop**
    (a guardian assigns a second guardian to a child → the assigned
    guardian's standing is live on their next read); **handoff** (the
    assigned guardian, now a full guardian, can suspend / un-suspend /
    curate / approve over the child — the five GU actions, no content
    read); **part-vs-whole** (the 8-test list is the whole; closed-loop +
    handoff are the parts; all must pass together).
  - `### Drift-guard (frozen once written)` — the
    `IIdentityService.FindSubjectByEmailAsync` seam + its doc-comment, the
    `GuardianController.Assign` action + its doc-comment, the
    `AssignGuardianForm` shape, the `GuardianItem` record, the
    `MembershipEditorModel.GuardianItems` field, the `Detail.cshtml` two
    appends, the 8 pinned test names, the G-A·1–G-A·6 invariants, and the
    acceptance gate — all frozen pins; any mismatch is a `## U<m> — Drift
    pause`.

### 2. `docs/adr/0038-guardian-assignment.md` (new, ~150–250 lines)

The **named decision** (the AGENTS.md ADR rule). Sections:

- `Status: Accepted` + `Date: 2026-09-16` + `Amends: 0028 (the G·4
  "no self-serve claim" non-decision is superseded for the *existing active
  guardian* case — the assign lane is the deliberate exception) + 0006 (a
  new `IIdentityService` ADD — a compatible ADD) + 0012/0013 (the
  standing-gate + the "a non-guardian learns nothing" shape inherited)`.
- `## Context` — the GU lane shipped; the `GuardianLink` model already
  supports "one or two guardians" (ADR 0028 §B); the gap: a second guardian
  cannot be assigned to an *existing* child; ADR 0028 G·4's "no self-serve
  claim" non-decision is the precedent this ADR supersedes for the *existing
  active guardian* case.
- `## Decision` —
  - **A. The standing:** the **assigning** guardian's active
    `GuardianLink` over the child is the standing basis (G-A·1 — the GU
    lane's G·2 precedent, inherited). No new `AccessVia` value; the
    `AccessVia.Guardian` value (the GU lane's 9th value) is **reused** as
    the audit `Via` for the `guardian.create` row the assign lane emits.
  - **B. The seams:** the **one** `IIdentityService` ADD
    (`FindSubjectByEmailAsync`) + the **one** `GuardianController` action
    (`Assign`) + the **one** `AssignGuardianForm` view model + the
    **one** `GuardianItem` record + the **one** `MembershipEditorModel.
    GuardianItems` field + the **two** `Detail.cshtml` appends (the list +
    the form) + the **localization keys**. The rest is the GU lane's
    existing surface (byte-identical).
  - **C. The invariants:** G-A·1–G-A·6 (the design doc's §Invariants,
    verbatim).
  - **D. The audit:** the **existing** `guardian.create` verb (no new verb;
    the `ActorId`/`EffectivePrincipalId` are both the assigning guardian;
    the `TargetId` is the new `GuardianLink` row's id; the `TargetKind` is
    `"guardian-link"`).
  - **E. The non-decisions (the "for now"):** no remove path; no
    acceptance/consent step; no bulk assign; no self-assignment (refused,
    not a lane); no second audit verb; no email notification. Each is
    *deferred*, not *denied* — each re-litigates as an ADR 0038 amendment
    when it earns its keep.
- `## Consequences` — the family case gains a *co-supervision* lane, not
  just a private-group one (the ADR 0028 "the family case gains a
  supervision lane" precedent, extended); the cardinal privacy rule is held
  and *proven* held (G-A·3 — the assigned guardian's standing is identical
  in kind to the creator's; no content read); the `Authority` chain gains a
  co-supervision legibility row (the audit log now answers "this guardian
  assigned this guardian over this child's account" — traceable forward and
  backward); the roadmap trio moves together (AGENTS.md contract) — the
  `GA` named lane (pulled forward ahead of M4, the `GU` precedent); the
  backward-compat note (fully additive — the `GuardianLink` POCO is
  byte-identical, the GU seams are byte-identical, the new seam is an ADD,
  the new action is an ADD, the new view model is new, the `Detail.cshtml`
  appends are additive; re-deploying an older image over a forward-migrated
  database is safe).

### 3. `docs/adr/README.md` (modify)

Append the `0038` row to the ADR index table (after the `0037` row):
`| 0038 | Guardian assignment: an existing guardian assigns a second guardian to a child's account (email-driven; one `IIdentityService` ADD + one `GuardianController` action + the Detail view's assign form; Amends 0028's G·4 non-decision for the existing-guardian case) | Accepted |`

## Exit

The design doc exists with all sections (the invariants, the FACES, the
pinned contract, the 8 pinned test names, the acceptance gate, the
drift-guard). ADR 0038 exists with all sections (the Status/Date/Amends,
the Context, the Decision A–E, the Consequences). The ADR README index has
the `0038` row. **No build.** Handoff note: 8–10 lines starting `## U01 —
design doc + ADR 0038` — (a) the seam name (verbatim), (b) the action name
(verbatim), (c) the form's field (verbatim), (d) the **8 pinned test names**,
(e) the G-A·1–G-A·6 invariants (by id), (f) the ADR 0038 §E non-decisions
(each named), (g) any drift pause.
