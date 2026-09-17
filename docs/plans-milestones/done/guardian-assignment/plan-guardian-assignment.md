# Guardian assignment (`GA`) — a guardian assigns a second guardian to an existing child — sealed unit register

> **The secondary tier** of the GA lane's three-tier contract. The **primary**
> is `docs/design/guardian-assignment-design.md` (U01 authors it fresh, including
> the *pinned contract*: exact C# seam signatures, the invariants G-A·1–G-A·6,
> the FACES, the pinned seam-test names, the acceptance gate, and the
> drift-guard). The **scratch** is
> `docs/plans-milestones/in-progress/guardian-assignment-handoff-notes.md` (one
> `##` section per unit, appended in order). When this register and a unit plan
> disagree, **the unit plan wins for what to do**; this register wins for
> *which files exist and in what order*.
>
> **ADR 0038 does not yet exist** — U01 authors it (a new capability that
> settles a design question, per AGENTS.md's ADR rule). The GU lane (ADR 0028)
> is already shipped and closed (`done/guardian-controls/`); this register and
> unit plans are **additive** — they do not touch any GU file. The GA lane is
> **a named lane (`GA`), not a milestone letter** — the same convention as
> `GP` (ADR 0013), `ML` (ADR 0005), `GU` (ADR 0028), `RE` (ADR 0031), `TR`
> (ADR 0021), `RC` (ADR 0025). **M4/M5/M6 stay Events / Projects /
> Portability.** The roadmap trio (README / `Milestones.cs` /
> `MilestonesTests.cs`) flips in U07 — the loop-closing unit — exactly like
> the GU lane's U11.

## Understanding

The GU lane (ADR 0028) shipped the **account-level supervision** surface for a
child's account: a parent *creates* the child's account (the usual
confirm-email lane), suspends/locks it, curates the child's community and
group memberships, approves a group invitation sent to the child, and dissolves
the link (independence). ADR 0028 §B already records that **"a child may have
one or two guardians (each an active row)"** — the `GuardianLink` *document*
and the `IUserInfoService.CreateGuardianLinkAsync` *seam* both already support
multiple active rows for the same `ChildId` (one row per `(GuardianId, ChildId)`
pair; `GuardActiveLinkAsync` resolves **any** active row over the pair).

What the GU lane did **not** ship is a **surface** for a *second* guardian to
get standing over an **existing** child account. Today the only path to
`CreateGuardianLinkAsync` is `GuardianController.AddChild` (POST `me/children`),
which pairs it with `IIdentityService.RegisterAsync` — the child's account must
be **created** in the same commit (G·4: formation is creation-based). A mother
who created her child's account **cannot** later add her ex-partner (or her
co-parent, or a grandparent) as a second guardian — ADR 0028 §D G·4 explicitly
records "no self-serve 'claim guardianship over an existing account' lane."
This is the lane this register fills.

The GA lane is **small by design**. The Core already supports multi-guardian
standing (ADR 0028 §B); the GU Web surface already reads `GuardianLink` rows
and calls the GU seams. What is new is:

- **One `IIdentityService` ADD** — `FindSubjectByEmailAsync(string email)` —
  the email → subjectId resolution the assign form needs (the email is the only
  external identifier a guardian would type; the identity store is the sole
  source of truth per ADR 0006 §A).
- **One `GuardianController` action** — `POST me/children/{childId}/assign` —
  that (a) gates standing (an *existing active guardian* of the child may
  assign), (b) resolves the typed email to a subjectId (or a
  user-presentable "no account" error), (c) refuses self-assignment and
  duplicate-assignment, and (d) calls the **existing**
  `CreateGuardianLinkAsync(childId, assignedSubjectId)` — one commit, one
  `guardian.create` audit row.
- **One form + one "other guardians" list** on the per-child `Detail` view —
  the "who else holds standing over this child" list (the existing
  `ActiveChildrenAsync` read inverted: the child's active guardian rows, not
  the guardian's child rows) + the assign form (email + submit).
- **One ADR (0038)** — the named decision that the **assign** lane is a
  **deliberate supersession** of ADR 0028 G·4's non-decision for the *existing
  active guardian* case (the standing basis is not "created" but
  "assigned-by-an-existing-guardian"), and that the **remove** path
  (a co-guardian dissolving *another* co-guardian's link, or the child
  dissolving a co-guardian's link) is a **named deferral** to a future
  ADR 0038 amendment.

The whole honesty of the lane is the same as GU's (G·1): the assigned
guardian's standing is **identical in kind** to the creator's standing — the
five GU actions, no content read, no "assigned" tier. The GA lane does not
**change** the GU enforcement path; it adds **one more path to the same
standing**, through the same seam, with the same audit verb. The difference is
**who may open that path**: the creator (GU) or an existing active guardian
(GA).

## Assumptions

- **Scope (per user):** a guardian **assigns another guardian** to a child
  account that already exists. **In:** (1) the
  `IIdentityService.FindSubjectByEmailAsync(string email)` ADD (the email →
  subjectId seam); (2) the `GuardianController.Assign` action (`POST
  me/children/{childId}/assign`) — standing-gate, resolve, refuse
  self/duplicate, call `CreateGuardianLinkAsync`; (3) the
  `AssignGuardianForm` view model + the "other guardians" list + the assign
  form on the existing `Views/Guardian/Detail.cshtml`; (4) ADR 0038 (the named
  decision that supersedes ADR 0028 G·4 for the existing-guardian case); (5)
  the design doc's pinned contract + seam tests + gate; (6) the README /
  `Milestones.cs` / `MilestonesTests.cs` trio flip. **Out (named deferrals,
  ADR 0038 §E "Non-decisions"):** no **remove** path (a co-guardian dissolving
  *another* co-guardian's link, or the child dissolving a co-guardian's link —
  the ADR 0028 G·5 safety valve remains the only "dissolve any active link"
  path); no **acceptance/consent** step on the assigned guardian (the standing
  is conferred by the assigning guardian's act, not the assigned guardian's
  consent — the ADR 0028 "the standing is from the active link, not from an
  acceptance" precedent); no **bulk assign** (one email per form); no
  **self-assignment** (a guardian cannot assign *themselves* as a second
  guardian over a child — that is the GU formation lane's territory, and
  `CreateGuardianLinkAsync` is already idempotent for the pair); no **second
  audit verb** (`guardian.create` is the one verb; the `EffectivePrincipalId`
  is the assigning guardian, the `ActorId` is also the assigning guardian —
  the audit row is "this guardian conferred standing on the child, via
  creation"); no **email notification** to the assigned guardian (the durable
  outbox / M1's verification lane is unchanged; the assigned guardian simply
  has standing on their next read).
- **The seam is one line on `IIdentityService`.** The IdentityModule is the
  sole owner of the identity source (ADR 0006 §A); the Web cannot query the
  identity store directly. `FindSubjectByEmailAsync` is a **read** (no audit
  row, no mutation), returns `string?` (the subjectId, or null if the email
  has no account — the Web's user-presentable error surface), and **does not
  leak** (a non-existent email returns null, not an exception that names the
  email — the ADR 0008 "a non-guardian learns nothing" shape). The
  `IIdentityService` frozen surface gains **one** ADD (the ADR 0006-E
  compatible lane; the M2 `GetProfilesAsync` / GU `CreateGuardianLinkAsync`
  precedent).
- **The Web action is one `GuardianController` method.** The standing gate
  (an **active** `GuardianLink` over `(actorId, childId)` — the same
  `ActiveLinkAsync` helper `Dissolve` already uses) runs first; a non-guardian
  → 404 (the ADR 0012/0013 "a non-guardian learns nothing" shape). The
  resolution (email → subjectId) runs second; null → the form's error surface
  (`ModelState.AddModelError(string.Empty, "No account with that email.")`),
  never a 500. The self-assignment + duplicate-assignment refusals are the
  third step (both `InvalidOperationException` → the form's error surface).
  The `CreateGuardianLinkAsync` call is the fourth — one commit, one
  `guardian.create` audit row (C3), the **existing** GU seam (no new Core
  seam).
- **The Detail view gets one list + one form.** The "other guardians" list is
  the existing `GuardianLink` read **inverted**: the child's active guardian
  rows (not the guardian's child rows), each resolved to a display name via
  the existing `GetProfileAsync` (the `ActiveChildrenAsync` precedent). The
  list is a **read**, not a decision — a non-guardian's page is a 404 (the
  `Detail` route already gates on `ActiveLinkAsync`). The assign form is a
  single email field + submit; the form is **not** bound to the child or the
  guardian (both come from the route + the cookie principal — the existing
  `AddChildForm` pattern).
- **The audit verb is the existing `guardian.create`.** No new verb. The
  `ActorId` and `EffectivePrincipalId` are both the **assigning** guardian
  (the one who called the seam); the `TargetId` is the new `GuardianLink`
  row's id; the `TargetKind` is `"guardian-link"`. The **assigned** guardian's
  identity is on the row's `GuardianId` field, not the audit row — the audit
  row answers "who conferred the standing" (the assigning guardian), not "who
  holds it" (the assigned guardian, on the row). This is the GU lane's
  existing audit shape (ADR 0028 §E), unchanged.
- **The child's content is untouched (G·1, inherited from ADR 0028).** The
  assigned guardian's standing is **identical in kind** to the creator's —
  the five GU actions, no content read. The GA lane does not touch
  `IAuthorizationService`, does not touch `AccessVia`, does not touch the GU
  enforcement path. The `GuardianLink` POCO is **byte-identical** (no new
  field, no new enum value, no renumbering). The `M1DocTypes` registration is
  **byte-identical** (the `GuardianLink` line is already there — U02 of the GU
  lane).
- **`Milestones.cs` + the README + `MilestonesTests.cs` flip together** (the
  AGENTS.md contract) — in U07, the loop-closing unit. The GA lane is a
  **named lane**, not a milestone letter (the `GP`/`ML`/`GU`/`RE`/`TR`/`RC`
  precedent): `new("GA", "Guardian assignment — an existing guardian assigns a second guardian to a child's account (email-driven; one IIdentityService ADD + one GuardianController action + the Detail view's assign form + ADR 0038)", StatusDone)` — appended **after** the `GU` line, **before** `M4`. `MilestonesTests.cs`'s "single-in-progress" + "order" assertions are updated in the **same commit** (the AGENTS.md contract).
- **Test model (unchanged).** Core seam tests in `Kumunita.Core.Tests` against
  the `PostgresFixture` fresh-scratch-DB shape (U03: the
  `FindSubjectByEmailAsync` seam tests). Web controller/VM data-shape tests in
  `Kumunita.Web.Tests` (U06: the `Assign` action's standing gate + the
  resolution + the `CreateGuardianLinkAsync` call). The lane's three-test
  acceptance gate (closed-loop / handoff / part-vs-whole) is recorded in the
  design doc (U07). The runner quirk (AGENTS.md) still applies: run via
  `dotnet exec tests\…\Kumunita.Core.Tests.dll` / `Kumunita.Web.Tests.dll`,
  not `dotnet test`.

## Approach

Three tracks, sequenced. **Track A (Core, U02–U03):** the
`IIdentityService.FindSubjectByEmailAsync(string email)` ADD + its
implementation + the seam tests (the email → subjectId resolution; the null
return on unknown email; the no-leak shape). **Track B (Web, U04–U05):** the
`GuardianController.Assign` action (standing gate + resolution + refuse
self/duplicate + `CreateGuardianLinkAsync`); the `AssignGuardianForm` view
model + the "other guardians" list + the assign form on `Detail.cshtml` + the
localization keys. **Track C (tests + close, U06–U07):** the Web
controller/VM data-shape tests, the acceptance gate recorded, the
`ARCHITECTURE.md` / design-doc flip, the ADR 0038 "Closed (recorded)"
section, and the register/unit-plans/handoff-notes moved to `done/`.

Every unit ends with **build green** (`dotnet build Kumunita.slnx -c Debug`).
U07 appends the final `## GA — Closed (recorded)` section and moves the lane's
files to `done/`.

**U01 is the design + ADR unit** — it authors `docs/design/guardian-assignment-
design.md` (fresh) **and** `docs/adr/0038-guardian-assignment.md` (fresh) in
one pass. This is the lane's "pin the contract" step (the GU lane's U01
analog, which *appended* to an existing design doc; here the design doc does
not exist yet). The ADR is the named decision (the AGENTS.md ADR rule); the
design doc is the machine-pinnable contract (the invariants, the seams, the
FACES, the pinned test names, the gate, the drift-guard).

## Workflow — handoff protocol for fresh-context agents

This lane is executed as a sequence of **sealed units** (U01–U07 below), one
unit per fresh agent with a **~32K context window** (the GU lane's precedent —
keep each unit ≤ ~4 files / ~500 LOC so a 32K window closes it in one pass).

**Shared state (three-tier contract):**
- **Primary —** `docs/design/guardian-assignment-design.md` — the GA
  invariants (G-A·1–G-A·6), the FACES, the **pinned contract** (the exact
  `IIdentityService.FindSubjectByEmailAsync` seam + the
  `GuardianController.Assign` action's exact signature + the
  `AssignGuardianForm` shape), the pinned seam-test names, the acceptance
  gate, and the drift-guard. U01 authors; U02–U06 match it verbatim.
- **Secondary — this file** (`docs/plans-milestones/in-progress/
  plan-guardian-assignment.md`) — the unit registry with each unit's
  deliverables and exit criteria.
- **Scratch —** `docs/plans-milestones/in-progress/guardian-assignment-handoff-
  notes.md`. One section per unit, appended (never rewritten). Each unit writes
  exactly one short section before it exits; the next unit reads only that
  section + its own entry-reads list.

**Per-unit template** (each `U` below follows this): **Goal** (one sentence,
one or two related deliverables); **Entry reads** (the minimal file list, 3–5
files, no full-repo scan; the design-doc section cited is named); **Deliverables**
(a closed set of new/modified files, ≤ ~4 files / ~500 LOC, no misc cleanups);
**Exit** (`dotnet build` green for the touched projects + the named test file
discovers its pinned tests, and a handoff-note entry appended *before* any
follow-up action).

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside a `## U<m> — Drift
pause`; (3) never reshapes `GuardianLink` (the GU lane's POCO) or adds a new
field / enum value / renumbering to it; (4) never opens a seam on
`IIdentityService` / `IUserInfoService` / `IAuthorizationService` beyond what
U01 pinned; (5) **never touches the GU enforcement path** (the GU seams, the
`GuardActiveLinkAsync` helper, the `AccessVia.Guardian` value — all
byte-identical); (6) never introduces a test whose exact name is not in the
design doc's pinned-test section; (7) if entry reads reveal the design doc is
out of date, the unit pauses and records `## U<m> — Drift pause` in the
handoff note.

---

## Units (7 total)

### U01 — Design doc + ADR 0038 (the pinned contract)

- **Goal:** author **two** new files in one pass: (1)
  `docs/design/guardian-assignment-design.md` — the lane's invariants
  (G-A·1–G-A·6), the FACES, the **pinned contract** (the exact
  `IIdentityService.FindSubjectByEmailAsync` seam + the
  `GuardianController.Assign` action's exact signature + the
  `AssignGuardianForm` shape + the `Detail.cshtml` changes), the **pinned
  seam-test names**, the **acceptance gate**, and the **drift-guard**; and
  (2) `docs/adr/0038-guardian-assignment.md` — the named decision (the AGENTS.md
  ADR rule: "a new capability that settles a design question gets an ADR").
  **No code, no build.**
- **Entry reads:** `docs/adr/0028-guardian-controls-account-scope-supervision.md`
  (the §B "one or two guardians" precedent + the §D G·4 non-decision this ADR
  supersedes for the existing-guardian case); `docs/design/guardian-controls-
  design.md` (the GU lane's design doc — the template to emulate, the
  invariants / FACES / pinned-contract shape); `src/Kumunita.Core/Identity/
  IIdentityService.cs` (the frozen surface + the M1 lifecycle ADDs the new
  seam mirrors); `src/Kumunita.Core/Identity/IdentityService.cs` (the
  `userManager.FindByEmailAsync` usage the new seam wraps — the
  `ResendVerificationEmailAsync` precedent); `src/Kumunita.Core/UserInfo/
  IUserInfoService.cs` §`CreateGuardianLinkAsync` (the seam the Web action
  calls — the idempotent upsert + the `guardian.create` audit);
  `src/Kumunita.Web/Controllers/GuardianController.cs` (the existing
  `ActiveLinkAsync` helper the standing gate reuses + the `AddChild` action's
  failure shape the new action mirrors); `src/Kumunita.Web/Models/
  GuardianViewModels.cs` (the `AddChildForm` shape the new `AssignGuardianForm`
  mirrors); `src/Kumunita.Web/Views/Guardian/Detail.cshtml` (the existing
  curation view the new list + form append to); `docs/adr/README.md` (the
  ADR index — U01 appends the `0038` row); `docs/plans-milestones/done/
  guardian-controls/plan-guardian-controls.md` (the GU lane's register — the
  template this register emulates).
- **Deliverables (3 files, all new except the ADR README append):**
  - `docs/design/guardian-assignment-design.md` (new, ~300–450 lines).
    Sections, in order:
    - `## Context` — the GU lane shipped; the `GuardianLink` model already
      supports "one or two guardians" (ADR 0028 §B); the only path to
      `CreateGuardianLinkAsync` today is the GU formation lane (paired with
      `RegisterAsync`); the gap: a second guardian cannot be assigned to an
      *existing* child; ADR 0028 G·4's "no self-serve claim" non-decision is
      the precedent this lane supersedes for the *existing active guardian*
      case. The arrow moved (supervision → co-supervision).
    - `## Scope` — **In:** (1) `IIdentityService.FindSubjectByEmailAsync` ADD
      (the email → subjectId seam); (2) `GuardianController.Assign` action
      (`POST me/children/{childId}/assign`) — standing gate + resolve +
      refuse self/duplicate + `CreateGuardianLinkAsync`; (3)
      `AssignGuardianForm` view model + the "other guardians" list + the
      assign form on `Detail.cshtml`; (4) ADR 0038; (5) the design doc's
      pinned contract + seam tests + gate; (6) the README / `Milestones.cs` /
      `MilestonesTests.cs` trio flip (U07). **Out (named deferrals, ADR 0038
      §E):** no remove path (a co-guardian dissolving *another* co-guardian's
      link, or the child dissolving a co-guardian's link — the ADR 0028 G·5
      safety valve remains the only "dissolve any active link" path); no
      acceptance/consent step on the assigned guardian; no bulk assign; no
      self-assignment (a guardian cannot assign *themselves* — the GU
      formation lane's territory, and `CreateGuardianLinkAsync` is already
      idempotent for the pair); no second audit verb (`guardian.create` is
      the one verb); no email notification to the assigned guardian.
    - `## Invariants (pinned for GA)` — six invariants, each with a one-line
      GA note:
      - **G-A·1** — Standing is from the **assigning** guardian's active
        `GuardianLink` over the child, checked **live** (the GU lane's G·2
        precedent; the `ActiveLinkAsync` helper the `Dissolve` route already
        uses). A non-guardian → 404 (the ADR 0012/0013 "a non-guardian learns
        nothing" shape). (GA-owned.)
      - **G-A·2** — The assigned guardian **must have an account** on the
        platform. The email → subjectId resolution (the new
        `FindSubjectByEmailAsync` seam) returns null for an unknown email; the
        Web surfaces a user-presentable error ("No account with that email."),
        never a 500, never an auto-create (the GU lane's G·4 "formation is
        creation-based" precedent — the assign lane does **not** create
        accounts; it assigns standing over an *existing* one). (GA-owned.)
      - **G-A·3** — The assigned guardian's standing is **identical in kind**
        to the creator's — the five GU actions, no content read, no "assigned"
        tier. The GA lane does **not** touch `IAuthorizationService`, does not
        touch `AccessVia`, does not touch the GU enforcement path. The
        `GuardianLink` POCO is **byte-identical** (no new field, no new enum
        value, no renumbering). (GA-owned; inherits G·1 from ADR 0028.)
      - **G-A·4** — **Idempotency** (the GU lane's G·4 precedent, inherited):
        a duplicate `(GuardianId, ChildId)` active row is a no-op — the row is
        left as-is, no second audit row. The `CreateGuardianLinkAsync` seam
        already enforces this (U04 of the GU lane); the GA lane does not
        re-implement it. (GA-owned; inherited.)
      - **G-A·5** — **Self-assignment is refused** (a new GA invariant): a
        guardian cannot assign *themselves* as a second guardian over a child
        — the `(actorId, childId)` pair is the same as the `(assignedId,
        childId)` pair, which is the GU formation lane's territory, and
        `CreateGuardianLinkAsync` is already idempotent for it (a no-op, not
        a useful act). The Web surfaces a user-presentable error ("You are
        already this child's guardian."), never a 500. (GA-owned.)
      - **G-A·6** — **No remove path** (a named deferral, not a denial): the
        GA lane does **not** add a "remove a co-guardian" surface. The ADR
        0028 G·5 safety valve (a GlobalAdmin dissolving any active link,
        audited `Via: Admin`) remains the **only** "dissolve any active link"
        path. A co-guardian dissolving *another* co-guardian's link, or the
        child dissolving a co-guardian's link, is a **future ADR 0038
        amendment** (the "each re-litigates as an ADR amendment, not a toggle"
        precedent). (GA-owned; the named deferral.)
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
        compatible lane; the M1 lifecycle ADD precedent):
        ```csharp
        /// <summary>
        /// GA (ADR 0038): resolve an email to a subject id (the assign
        /// form's one external identifier). A read — no audit row, no
        /// mutation. Returns the subject id, or null if the email has no
        /// account (the Web's user-presentable error surface — the ADR
        /// 0008 "a non-guardian learns nothing" shape: a null return, not
        /// an exception that names the email). ADR 0006-E compatible
        /// ADD — the M1 lifecycle ADD precedent (the
        /// <c>ResendVerificationEmailAsync</c> shape, a read over
        /// <c>userManager.FindByEmailAsync</c>).
        /// </summary>
        Task<string?> FindSubjectByEmailAsync(string email);
        ```
        Doc-comment anchors G-A·2 (the no-leak shape) + the ADR 0006-E lane.
      - `### GuardianController.Assign (exact C#)` — the **one new action** on
        the existing `GuardianController` (the `AddChild` action's failure
        shape to mirror):
        ```csharp
        /// <summary>
        /// GA (ADR 0038): assign a second guardian to this child. The
        /// standing gate (G-A·1 — the <c>ActiveLinkAsync</c> helper the
        /// <c>Dissolve</c> route already uses) runs first; a non-guardian
        /// → 404. The resolution (G-A·2 — the
        /// <c>FindSubjectByEmailAsync</c> seam) runs second; null → the
        /// form's error surface ("No account with that email.").
        /// Self-assignment (G-A·5) + duplicate-assignment (G-A·4) are the
        /// third step (both <see cref="InvalidOperationException"/> →
        /// the form's error surface). The
        /// <see cref="IUserInfoService.CreateGuardianLinkAsync"/> call is
        /// the fourth — one commit, one <c>guardian.create</c> audit row
        /// (C3), the <b>existing</b> GU seam (no new Core seam).
        /// </summary>
        [HttpPost("{childId}/assign")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(string childId, [FromForm] AssignGuardianForm form);
        ```
      - `### AssignGuardianForm (exact C#)` — the **one new view model** (the
        `AddChildForm` shape to mirror — a single email field, the
        `[Required, EmailAddress]` validation, no other field):
        ```csharp
        /// <summary>
        /// GA (ADR 0038): the assign-a-second-guardian form model, bound
        /// via <c>[FromForm]</c> on <c>GuardianController.Assign</c>.
        /// The <b>assigned guardian</b> is never form-bound beyond the
        /// email (the email is the one external identifier; the
        /// <c>FindSubjectByEmailAsync</c> seam resolves it to a
        /// subject id). The <b>assigning guardian</b> is never form-bound
        /// — it is minted by the Web layer from
        /// <c>KumunitaPrincipal.SubjectId(User)</c> (the single identity
        /// source, the <c>AddChildForm</c> precedent). The <b>child</b>
        /// is the route's <c>{childId}</c>.
        /// </summary>
        public sealed class AssignGuardianForm
        {
            [Required, EmailAddress, MaxLength(255)]
            [Display(Name = "Email of the guardian to assign")]
            public string? Email { get; set; }
        }
        ```
      - `### Detail.cshtml changes (exact markup)` — the **two appends** to the
        existing `Views/Guardian/Detail.cshtml` (the "other guardians" list +
        the assign form; the existing curation sections are **untouched**):
        ```html
        @*
            GA (ADR 0038) — the "other guardians" list: the child's active
            GuardianLink rows (not the guardian's child rows — the
            ActiveChildrenAsync read inverted), each resolved to a
            display name via the existing GetProfileAsync (a read, not a
            decision; G-A·3 — the assigned guardian's standing is
            identical in kind to the creator's). The list is the
            standing itself — a non-guardian's page is a 404 (the
            Detail route already gates on ActiveLinkAsync).
        *@
        <h2>Other guardians</h2>
        @if (Model.GuardianItems.Count == 0)
        {
            <div class="alert alert-info">No other guardians assigned.</div>
        }
        else
        {
            <ul class="list-group mb-3">
                @foreach (var g in Model.GuardianItems)
                {
                    <li class="list-group-item">@g.DisplayName</li>
                }
            </ul>
        }

        @*
            GA (ADR 0038) — the assign form: a single email field +
            submit. The form is NOT bound to the child or the guardian
            (both come from the route + the cookie principal — the
            AddChildForm precedent). The POST is the one-commit write
            (G-A·1 standing gate + G-A·2 resolution + G-A·4 idempotency +
            G-A·5 self-assignment refusal + the CreateGuardianLinkAsync
            call).
        *@
        <h2>Assign a guardian</h2>
        <form method="post" action="@Url.Action("Assign", "Guardian", new { childId = Model.ChildId })">
            @Html.AntiForgeryToken()
            <div asp-validation-summary="All" class="text-danger mb-3"></div>
            <div class="mb-3">
                <label asp-for="Email" class="form-label"></label>
                <input asp-for="Email" class="form-control" />
                <span asp-validation-for="Email" class="text-danger"></span>
            </div>
            <button type="submit" class="btn btn-primary">Assign</button>
        </form>
        ```
        The `MembershipEditorModel` record gains **one** field:
        `IReadOnlyList<GuardianItem> GuardianItems` (the
        `ChildAccountItem` shape to mirror — `record GuardianItem(string
        SubjectId, string DisplayName)`). The `Detail` action's
        `MembershipEditorModel` construction gains the `GuardianItems`
        argument (the `ActiveGuardiansAsync` helper — the
        `ActiveChildrenAsync` helper inverted: the child's active
        `GuardianLink` rows, not the guardian's child rows).
      - `### Pinned seam tests (exact names)` — file
        `tests/Kumunita.Core.Tests/GuardianAssignmentTests.cs` (U03):
        1. `FindSubjectByEmail_ReturnsSubjectIdForKnownEmail` — a verified
           account with a known email → the subject id (the happy path; the
           `ResendVerificationEmailAsync` precedent).
        2. `FindSubjectByEmail_ReturnsNullForUnknownEmail` — an email with
           no account → null (G-A·2's no-leak shape; the
           `ResendVerificationEmailAsync` "no account" precedent).
        3. `FindSubjectByEmail_IsCaseInsensitiveOnEmail` — the email
           comparison is case-insensitive (the ASP.NET Identity
           `FindByEmailAsync` precedent — a read, not a decision).
        And file `tests/Kumunita.Web.Tests/GuardianAssignmentTests.cs` (U06):
        4. `Assign_NonGuardian_Returns404` — a non-guardian POSTs
           `me/children/{childId}/assign` → 404 (G-A·1; the ADR 0012/0013
           "a non-guardian learns nothing" shape).
        5. `Assign_UnknownEmail_ReturnsValidationError` — a guardian POSTs
           with an unknown email → the form re-renders with "No account
           with that email." (G-A·2; the `AddChild` action's failure shape).
        6. `Assign_SelfAssignment_ReturnsValidationError` — a guardian
           POSTs their own email → the form re-renders with "You are already
           this child's guardian." (G-A·5).
        7. `Assign_DuplicateAssignment_IsIdempotentNoOp` — a guardian POSTs
           an email that already has an active link over the child → the
           form re-renders (or the info message) with no second audit row
           (G-A·4; the `CreateGuardianLinkAsync` idempotency).
        8. `Assign_KnownEmail_CallsCreateGuardianLinkAsync` — a guardian
           POSTs a known, non-self, non-duplicate email →
           `CreateGuardianLinkAsync` is called with the resolved subject id
           + the child id (the happy path; the `AddChild` action's
           `RegisterAsync` + `CreateGuardianLinkAsync` pairing precedent,
           minus the `RegisterAsync`).
      - `### Acceptance gate (U07 records)` — the three tests: **closed loop**
        (a guardian assigns a second guardian to a child → the assigned
        guardian's standing is live on their next read — the
        `SuspendChildAsync` / `UnsuspendChildAsync` / membership lanes all
        resolve the new row); **handoff** (the assigned guardian, now a
        full guardian, can suspend / un-suspend / curate / approve over the
        child — the five GU actions, no content read); **part-vs-whole** (the
        8-test list is the whole; closed-loop + handoff are the parts; all
        must pass together).
      - `### Drift-guard (frozen once written)` — the
        `IIdentityService.FindSubjectByEmailAsync` seam + its doc-comment,
        the `GuardianController.Assign` action + its doc-comment, the
        `AssignGuardianForm` shape, the `Detail.cshtml` two appends + the
        `MembershipEditorModel` `GuardianItems` field + the `GuardianItem`
        record, the 8 pinned test names, the G-A·1–G-A·6 invariants, and the
        acceptance gate — all frozen pins; any mismatch is a `## U<m> — Drift
        pause`.
  - `docs/adr/0038-guardian-assignment.md` (new, ~150–250 lines). Sections:
    `## Status: Accepted` + `## Date: 2026-09-16` + `## Amends: 0028 (the
    G·4 "no self-serve claim" non-decision is superseded for the *existing
    active guardian* case — the assign lane is the deliberate exception) +
    0006 (a new `IIdentityService` ADD — a compatible ADD) + 0012/0013 (the
    standing-gate + the "a non-guardian learns nothing" shape inherited)`.
    `## Context` — the GU lane shipped; the `GuardianLink` model already
    supports "one or two guardians" (ADR 0028 §B); the gap: a second
    guardian cannot be assigned to an *existing* child; ADR 0028 G·4's
    "no self-serve claim" non-decision is the precedent this ADR
    supersedes for the *existing active guardian* case. `## Decision` —
    (A) the standing: the **assigning** guardian's active `GuardianLink`
    over the child is the standing basis (G-A·1 — the GU lane's G·2
    precedent, inherited); (B) the seam: the **one** `IIdentityService`
    ADD (`FindSubjectByEmailAsync`) + the **one** `GuardianController`
    action (`Assign`) + the **one** `AssignGuardianForm` view model + the
    **two** `Detail.cshtml` appends (the list + the form) — the rest is
    the GU lane's existing surface (byte-identical); (C) the invariants:
    G-A·1–G-A·6 (the design doc's §Invariants, verbatim); (D) the audit:
    the **existing** `guardian.create` verb (no new verb; the
    `ActorId`/`EffectivePrincipalId` are both the assigning guardian; the
    `TargetId` is the new `GuardianLink` row's id; the `TargetKind` is
    `"guardian-link"`); (E) the non-decisions (the "for now"): no remove
    path; no acceptance/consent step; no bulk assign; no self-assignment
    (refused, not a lane); no second audit verb; no email notification.
    Each is *deferred*, not *denied* — each re-litigates as an ADR 0038
    amendment when it earns its keep. `## Consequences` — the family case
    gains a *co-supervision* lane, not just a private-group one (the ADR
    0028 "the family case gains a supervision lane" precedent, extended);
    the cardinal privacy rule is held and *proven* held (G-A·3 — the
    assigned guardian's standing is identical in kind to the creator's;
    no content read); the `Authority` chain gains a co-supervision
    legibility row (the audit log now answers "this guardian assigned this
    guardian over this child's account" — traceable forward and backward);
    the roadmap trio moves together (AGENTS.md contract) — the `GA` named
    lane (pulled forward ahead of M4, the `GU` precedent); the
    backward-compat note (fully additive — the `GuardianLink` POCO is
    byte-identical, the GU seams are byte-identical, the new seam is an
    ADD, the new action is an ADD, the new view model is new, the
    `Detail.cshtml` appends are additive; re-deploying an older image over
    a forward-migrated database is safe).
  - `docs/adr/README.md` (modify) — append the `0038` row to the ADR
    index table (the `0037` row's precedent): `| 0038 | Guardian
    assignment: an existing guardian assigns a second guardian to a
    child's account (email-driven; one `IIdentityService` ADD + one
    `GuardianController` action + the Detail view's assign form; Amends
    0028's G·4 non-decision for the existing-guardian case) | Accepted |`.
- **Exit:** the design doc exists with all sections + the **8 pinned test
  names**; ADR 0038 exists with all sections; the ADR README index has the
  `0038` row. **No build.** Handoff note: 8–10 lines starting `## U01 —
  design doc + ADR 0038` — (a) the seam name (verbatim), (b) the action
  name (verbatim), (c) the form's field (verbatim), (d) the **8 pinned test
  names**, (e) the G-A·1–G-A·6 invariants (by id), (f) the ADR 0038 §E
  non-decisions (each named), (g) any drift pause.

### U02 — `IIdentityService.FindSubjectByEmailAsync` + the impl

- **Goal:** add the **one** `IIdentityService` ADD (the pinned contract's
  exact seam) + its implementation (the `userManager.FindByEmailAsync`
  precedent, a read, no audit row, null on unknown email). **No Web, no
  tests** (U03 pins the seam tests; U06 pins the Web tests).
- **Entry reads:** `docs/design/guardian-assignment-design.md` §Pinned
  contract §`IIdentityService.FindSubjectByEmailAsync` (the exact seam +
  doc-comment — the *primary* source); `src/Kumunita.Core/Identity/
  IIdentityService.cs` (the frozen surface + the M1 lifecycle ADDs the new
  seam mirrors — the `ResendVerificationEmailAsync` shape);
  `src/Kumunita.Core/Identity/IdentityService.cs` (the
  `userManager.FindByEmailAsync` usage the new seam wraps — the
  `ResendVerificationEmailAsync` precedent, a read over the identity store);
  `docs/adr/0038-guardian-assignment.md` §Decision (A)–(B) (the ADR
  authority for the seam).
- **Deliverables (2 files, modify):**
  - `src/Kumunita.Core/Identity/IIdentityService.cs` — append the **exact**
    `FindSubjectByEmailAsync(string email)` seam from the pinned contract,
    with its doc-comment (the ADR 0006-E lane; the G-A·2 no-leak shape).
    Place it in the M1 lifecycle block (the `ResendVerificationEmailAsync`
    precedent — the `// ── M1 lifecycle (ADR 0006-E compatible lane) ──`
    section, after the M1 lifecycle methods, before the break-glass lane).
  - `src/Kumunita.Core/Identity/IdentityService.cs` — implement
    `FindSubjectByEmailAsync`: `var user = await
    userManager.FindByEmailAsync(email); return user?.Id;` (the
    `ResendVerificationEmailAsync` precedent — a read, no audit row, null
    on unknown email). **No** new `using` needed (the `userManager` field is
    already in the class). **No** `ArgumentException` on null/whitespace
    email (the `ResendVerificationEmailAsync` precedent — the Web's form
    validation is the gate; the seam is a read).
- **Exit:** `dotnet build` green. The seam is on the interface + the impl.
  **No new test** (U03 pins). Handoff note: 4–5 lines starting `## U02 —
  FindSubjectByEmailAsync` — (a) the seam's exact signature (verbatim), (b)
  the impl's one-liner (verbatim), (c) the placement in the interface (the
  M1 lifecycle block, after `ResendVerificationEmailAsync`), (d) a
  confirmation no other `IIdentityService` member changed, (e) any compile
  warnings.

### U03 — Core seam tests (the 3 pinned names)

- **Goal:** implement the **3** Core seam tests from the design doc §Pinned
  contract in `tests/Kumunita.Core.Tests/GuardianAssignmentTests.cs` (the GU
  lane's `GuardianControlsTests.cs` shape — the `PostgresFixture`, the
  seed-and-assert pattern). **This unit does NOT author the Web tests (U06)
  and does NOT run the acceptance gate (U07).**
- **Entry reads:** `docs/design/guardian-assignment-design.md` §Pinned
  contract § Pinned seam tests (the **primary** source — the 3 names, exact);
  `tests/Kumunita.Core.Tests/GuardianControlsTests.cs` (the GU lane's
  seam-test shape to mirror — the `PostgresFixture` usage, the seed-and-
  assert, the audit-row assertions); `tests/Kumunita.Core.Tests/
  PostgresFixture.cs` (the harness); `src/Kumunita.Core/Identity/
  IdentityService.cs` §`FindSubjectByEmailAsync` (U02's impl — the null
  return + the subject id the tests assert); `src/Kumunita.Core/Identity/
  IIdentityService.cs` §`FindSubjectByEmailAsync` (the seam's doc-comment
  — the G-A·2 no-leak shape the tests pin).
- **Deliverables (1 file, new):** `tests/Kumunita.Core.Tests/
  GuardianAssignmentTests.cs` — **3 tests**, one per pinned name:
  1. `FindSubjectByEmail_ReturnsSubjectIdForKnownEmail` — seed a verified
     account with a known email via `RegisterAsync` + `VerifyWithTokenAsync`
     (the M1 lifecycle); assert `FindSubjectByEmailAsync(email)` returns the
     account's subject id (the `ThinPrincipal.SubjectId`).
  2. `FindSubjectByEmail_ReturnsNullForUnknownEmail` — assert
     `FindSubjectByEmailAsync("no-account@example.com")` returns null
     (G-A·2's no-leak shape — a null, not an exception).
  3. `FindSubjectByEmail_IsCaseInsensitiveOnEmail` — seed an account with
     email `User@Example.com`; assert
     `FindSubjectByEmailAsync("user@example.com")` returns the subject id
     (the ASP.NET Identity `FindByEmailAsync` case-insensitive precedent).
- **Exit:** `dotnet build` green. Run via the **reliable path** (AGENTS.md):
  `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
  --filter-method "*FindSubjectByEmail*"` — reports **3 GA tests discovered,
  3 executed**. Record the pass/red status of each (for U07's gate). **No
  gate recorded** (U07). **Clean up Docker:** if the process was killed,
  `docker container prune`. Handoff note: 3 lines starting `## U03 — Core
  seam tests (3)` — (a) the file path, (b) the 3 names (verbatim), (c) the
  pass/red counts (for U07 to consume), (d) any `## U<m> — Drift pause`.

### U04 — Web: `AssignGuardianForm` + the `GuardianItem` record + the
`MembershipEditorModel` `GuardianItems` field

- **Goal:** add the **one** new view model (`AssignGuardianForm`), the **one**
  new record (`GuardianItem`), and the **one** new field on the existing
  `MembershipEditorModel` (`GuardianItems`) — the Web's data-shape surface
  for the GA lane. **No controller, no view** (U05 is the controller +
  view).
- **Entry reads:** `docs/design/guardian-assignment-design.md` §Pinned
  contract §`AssignGuardianForm` + §`Detail.cshtml changes` (the exact
  shapes — the *primary* source); `src/Kumunita.Web/Models/
  GuardianViewModels.cs` (the existing GU VMs — the `ChildAccountItem` shape
  the new `GuardianItem` mirrors; the `MembershipEditorModel` record the new
  field appends to; the `AddChildForm` shape the new `AssignGuardianForm`
  mirrors); `docs/adr/0038-guardian-assignment.md` §Decision (B) (the ADR
  authority for the VMs).
- **Deliverables (1 file, modify):**
  - `src/Kumunita.Web/Models/GuardianViewModels.cs` — append, in order:
    - `public sealed record GuardianItem(string SubjectId, string
      DisplayName);` — the **one** new record (the `ChildAccountItem` shape
      to mirror — `SubjectId` + `DisplayName`; the `SubjectId` is the
      assigned guardian's `Profile.SubjectId`, the `DisplayName` is
      resolved via `GetProfileAsync` (a read, not a decision — G-A·3)).
    - `public sealed class AssignGuardianForm { [Required, EmailAddress,
      MaxLength(255)] [Display(Name = "Email of the guardian to assign")]
      public string? Email { get; set; } }` — the **one** new view model
      (the `AddChildForm` shape to mirror — a single email field, the
      `[Required, EmailAddress]` validation, no other field).
    - **Modify** the existing `MembershipEditorModel` record — append
      **one** field: `IReadOnlyList<GuardianItem> GuardianItems` (the
      pinned contract's exact shape). The record's existing fields
      (`ChildId`, `GroupIds`, `CommunityIds`, `PendingInvitations`) are
      **untouched**; the new field is the last in the record's parameter
      list. The `GuardianController.Detail` action's
      `MembershipEditorModel` construction (U05) gains the `GuardianItems`
      argument (the `ActiveGuardiansAsync` helper — the
      `ActiveChildrenAsync` helper inverted).
- **Exit:** `dotnet build` on `Kumunita.Web` green. The two new types +
  the one new field compile. **No new test** (U06 pins the Web tests).
  Handoff note: 4–5 lines starting `## U04 — VMs` — (a) the `GuardianItem`
  record's fields (verbatim), (b) the `AssignGuardianForm`'s field
  (verbatim), (c) the `MembershipEditorModel`'s new field (verbatim) + its
  position in the record (last), (d) a confirmation the existing
  `MembershipEditorModel` fields are untouched, (e) any compile warnings.

### U05 — Web: `GuardianController.Assign` action + the `Detail` action's
`GuardianItems` + the `ActiveGuardiansAsync` helper

- **Goal:** add the **one** new `GuardianController` action (`Assign`) + the
  **one** new private helper (`ActiveGuardiansAsync`) + the **one** line on
  the existing `Detail` action (the `GuardianItems` argument to the
  `MembershipEditorModel` construction). **No view** (U06 is the view + the
  localization keys).
- **Entry reads:** `docs/design/guardian-assignment-design.md` §Pinned
  contract §`GuardianController.Assign` + §`Detail.cshtml changes` (the
  exact action + the helper — the *primary* source);
  `src/Kumunita.Web/Controllers/GuardianController.cs` (the existing
  `ActiveLinkAsync` helper the standing gate reuses + the `AddChild`
  action's failure shape the new action mirrors + the `Detail` action's
  `MembershipEditorModel` construction the new argument appends to + the
  `ActiveChildrenAsync` helper the new `ActiveGuardiansAsync` helper
  mirrors, inverted); `src/Kumunita.Web/Models/GuardianViewModels.cs`
  (U04's VMs — the `AssignGuardianForm` the action binds + the
  `GuardianItem` record the helper returns + the
  `MembershipEditorModel`'s `GuardianItems` field the `Detail` action
  populates); `src/Kumunita.Core/Identity/IIdentityService.cs`
  §`FindSubjectByEmailAsync` (U02's seam the action calls);
  `src/Kumunita.Core/UserInfo/IUserInfoService.cs` §`CreateGuardianLinkAsync`
  (the seam the action calls — the idempotent upsert + the
  `guardian.create` audit); `docs/adr/0038-guardian-assignment.md`
  §Decision (A)–(B) (the ADR authority for the action).
- **Deliverables (1 file, modify):**
  - `src/Kumunita.Web/Controllers/GuardianController.cs` — append, in
    order:
    - **The `Assign` action** — the pinned contract's exact signature +
      doc-comment. Implementation (the `AddChild` action's failure shape to
      mirror):
      ```csharp
      [HttpPost("{childId}/assign")]
      [ValidateAntiForgeryToken]
      public async Task<IActionResult> Assign(string childId, [FromForm] AssignGuardianForm form)
      {
          if (!ModelState.IsValid)
              return View("Detail", new AssignGuardianForm { Email = form.Email });

          var subject = SubjectId(User);
          if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(childId))
              return NotFound();

          // G-A·1 — the standing gate: the actor must hold an active link
          // over this child (the ActiveLinkAsync helper the Dissolve route
          // already uses). A non-guardian → 404 (the ADR 0012/0013 "a
          // non-guardian learns nothing" shape).
          var link = await ActiveLinkAsync(subject, childId);
          if (link is null)
              return NotFound();

          var email = (form.Email ?? string.Empty).Trim();
          if (string.IsNullOrEmpty(email))
          {
              ModelState.AddModelError(nameof(AssignGuardianForm.Email), "An email is required.");
              return View("Detail", new AssignGuardianForm { Email = email });
          }

          // G-A·2 — the resolution: the email → subject id (the
          // FindSubjectByEmailAsync seam). Null → the form's error
          // surface (a user-presentable error, never a 500, never an
          // auto-create — the GU lane's G·4 "formation is creation-based"
          // precedent).
          var assignedId = await identity.FindSubjectByEmailAsync(email);
          if (assignedId is null)
          {
              ModelState.AddModelError(string.Empty, "No account with that email.");
              return View("Detail", new AssignGuardianForm { Email = email });
          }

          // G-A·5 — self-assignment is refused (the (actorId, childId)
          // pair is the same as the (assignedId, childId) pair — the GU
          // formation lane's territory, and CreateGuardianLinkAsync is
          // already idempotent for it — a no-op, not a useful act).
          if (assignedId == subject)
          {
              ModelState.AddModelError(string.Empty, "You are already this child's guardian.");
              return View("Detail", new AssignGuardianForm { Email = email });
          }

          try
          {
              // G-A·4 — the CreateGuardianLinkAsync seam is idempotent for
              // the (guardianId, childId) pair (the GU lane's G·4
              // precedent, inherited): a duplicate active row is a no-op —
              // the row is left as-is, no second audit row. The happy
              // path is one commit, one guardian.create audit row (C3).
              await userInfo.CreateGuardianLinkAsync(childId, assignedId);
              TempData["info"] = $"Guardian assigned.";
          }
          catch (UnauthorizedAccessException)
          {
              return NotFound();
          }
          catch (InvalidOperationException ex)
          {
              ModelState.AddModelError(string.Empty, ex.Message);
              return View("Detail", new AssignGuardianForm { Email = email });
          }

          return RedirectToAction(nameof(Detail), new { childId });
      }
      ```
    - **The `ActiveGuardiansAsync` helper** — the `ActiveChildrenAsync`
      helper inverted (the child's active `GuardianLink` rows, not the
      guardian's child rows):
      ```csharp
      /// <summary>
      /// GA (ADR 0038) — the child's <b>active</b>
      /// <see cref="GuardianLink"/> rows (a read, not a decision), joined
      /// to each guardian's display name (ids/names only — G-A·3). The
      /// <c>ActiveChildrenAsync</c> helper inverted: the child's active
      /// guardian rows, not the guardian's child rows.
      /// </summary>
      private async Task<IReadOnlyList<GuardianItem>> ActiveGuardiansAsync(string childId)
      {
          await using var session = store.QuerySession();
          var links = await session
              .Query<GuardianLink>()
              .Where(l => l.ChildId == childId && l.Status == GuardianLinkStatus.Active)
              .ToListAsync(System.Threading.CancellationToken.None);

          var rows = new List<GuardianItem>(links.Count);
          foreach (var link in links)
          {
              var profile = await userInfo.GetProfileAsync(link.GuardianId);
              rows.Add(new GuardianItem(
                  link.GuardianId,
                  profile?.DisplayName ?? link.GuardianId));
          }

          return rows;
      }
      ```
    - **The `Detail` action's `MembershipEditorModel` construction** —
      append the **one** new argument (`GuardianItems`):
      ```csharp
      var guardianItems = await ActiveGuardiansAsync(childId);
      return View(new MembershipEditorModel(
          childId,
          groupIds.OrderBy(g => g, StringComparer.OrdinalIgnoreCase).ToList(),
          communityIds.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList(),
          invitations,
          guardianItems));  // ← the new argument (U04's VM)
      ```
      The existing `Detail` action's other lines are **untouched**.
  - **Note:** the `GuardianController`'s constructor already has
    `IIdentityService identity` (the `AddChild` action uses it) — the
    `Assign` action's `identity.FindSubjectByEmailAsync(email)` call is
    valid without a constructor change. **No** new `using` needed (the
    `AssignGuardianForm` + `GuardianItem` are in the `Kumunita.Web.Models`
    namespace, already imported).
- **Exit:** `dotnet build` on `Kumunita.Web` green. The `Assign` action +
  the `ActiveGuardiansAsync` helper + the `Detail` action's new argument
  compile. **No new test** (U06 pins the Web tests). Handoff note: 6–8 lines
  starting `## U05 — Assign action + ActiveGuardiansAsync` — (a) the action's
  exact route (verbatim), (b) the 5 steps (standing gate → resolution →
  self-assignment → `CreateGuardianLinkAsync` → redirect) in order, (c) the
  `ActiveGuardiansAsync` helper's query (the `ChildId` + `Active` filter),
  (d) the `Detail` action's new argument (the `GuardianItems` position —
  last), (e) a confirmation the `AddChild` action + the `ActiveLinkAsync`
  helper + the `ActiveChildrenAsync` helper are **untouched**, (f) any
  compile warnings.

### U06 — Web: `Detail.cshtml` two appends + the localization keys + the Web
tests

- **Goal:** append the **two** `Detail.cshtml` appends (the "other guardians"
  list + the assign form — the pinned contract's exact markup) + the
  **localization keys** (the `guardian.assign.*` prefix, the
  `guardian.otherGuardians.*` prefix) + author the **5** Web
  controller/VM data-shape tests in
  `tests/Kumunita.Web.Tests/GuardianAssignmentTests.cs`. **This unit does
  NOT run the acceptance gate (U07).**
- **Entry reads:** `docs/design/guardian-assignment-design.md` §Pinned
  contract §`Detail.cshtml changes` + § Pinned seam tests (the **primary**
  source — the markup + the 5 Web test names, exact);
  `src/Kumunita.Web/Views/Guardian/Detail.cshtml` (the existing curation
  view the two appends go into — the existing `<h2>` + `<form>` patterns to
  match); `src/Kumunita.Web/Models/GuardianViewModels.cs` (U04's VMs — the
  `AssignGuardianForm` the form binds + the `GuardianItem` record the list
  iterates + the `MembershipEditorModel`'s `GuardianItems` field the list
  reads); `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the
  existing `guardian.*` keys the new `guardian.assign.*` +
  `guardian.otherGuardians.*` keys append to); `tests/Kumunita.Web.Tests/`
  (an existing Web data-shape test to mirror the harness — the GU lane's
  `GuardianViewModelsTests.cs` if it exists, else the M2/M3 Web-test shape);
  `src/Kumunita.Web/Controllers/GuardianController.cs` (U05's `Assign`
  action — the standing gate + the resolution + the refusal shape the tests
  assert).
- **Deliverables (3 files):**
  - `src/Kumunita.Web/Views/Guardian/Detail.cshtml` (modify) — append the
    **two** appends from the pinned contract (the "other guardians" list +
    the assign form) at the **end** of the existing view (after the
    existing curation sections, before the closing `</div>`). The existing
    curation sections (the group membership editor, the community
    membership editor, the pending-invitation approval list, the dissolve
    control) are **untouched**.
  - `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (modify) —
    append the **localization keys** (the existing `guardian.*` block's
    precedent — the `// ── guardian (the /me/children child-accounts
    surface, GU ADR 0028) ──` section):
    ```csharp
    // ── guardian assignment (GA ADR 0038) ──
    ["guardian.otherGuardians.title"] = "Other guardians",
    ["guardian.otherGuardians.empty"] = "No other guardians assigned.",
    ["guardian.assign.title"]        = "Assign a guardian",
    ["guardian.assign.email"]        = "Email of the guardian to assign",
    ["guardian.assign.submit"]       = "Assign",
    ["guardian.assign.noAccount"]    = "No account with that email.",
    ["guardian.assign.self"]         = "You are already this child's guardian.",
    ["guardian.assign.success"]      = "Guardian assigned.",
    ```
    The `Detail.cshtml` appends use the `<kw-l key="...">` TagHelper (the
    existing `guardian.*` keys' precedent) for the user-facing strings; the
    `Assign` action's `ModelState.AddModelError` messages are the **English
    floor** (the `KnownTranslationKeys` values) — the TagHelper's
    resolution is the per-request override (the ML-UI lane's precedent).
  - `tests/Kumunita.Web.Tests/GuardianAssignmentTests.cs` (new) — **5
    tests**, one per pinned name:
    1. `Assign_NonGuardian_Returns404` — a non-guardian POSTs
       `me/children/{childId}/assign` → 404 (G-A·1; the ADR 0012/0013
       "a non-guardian learns nothing" shape). Seed: a child account + a
       non-guardian account (no `GuardianLink` over the child); act: POST
       `me/children/{childId}/assign` with a valid email; assert: 404.
    2. `Assign_UnknownEmail_ReturnsValidationError` — a guardian POSTs with
       an unknown email → the form re-renders with "No account with that
       email." (G-A·2; the `AddChild` action's failure shape). Seed: a
       child account + a guardian account (an active `GuardianLink` over
       the child) + **no** account with the typed email; act: POST
       `me/children/{childId}/assign` with the unknown email; assert: 200
       (the form re-renders) + the `ModelState` has the "No account with
       that email." error.
    3. `Assign_SelfAssignment_ReturnsValidationError` — a guardian POSTs
       their own email → the form re-renders with "You are already this
       child's guardian." (G-A·5). Seed: a child account + a guardian
       account (an active `GuardianLink` over the child); act: POST
       `me/children/{childId}/assign` with the guardian's own email;
       assert: 200 (the form re-renders) + the `ModelState` has the "You
       are already this child's guardian." error.
    4. `Assign_DuplicateAssignment_IsIdempotentNoOp` — a guardian POSTs an
       email that already has an active link over the child → the form
       re-renders (or the info message) with no second audit row (G-A·4;
       the `CreateGuardianLinkAsync` idempotency). Seed: a child account +
       a guardian account (an active `GuardianLink` over the child) + a
       second guardian account (an **existing** active `GuardianLink` over
       the child); act: POST `me/children/{childId}/assign` with the
       second guardian's email (a duplicate); assert: 302 (the redirect to
       `Detail`) + **no** second `guardian.create` audit row for the
       `(secondGuardianId, childId)` pair (the idempotent no-op).
    5. `Assign_KnownEmail_CallsCreateGuardianLinkAsync` — a guardian POSTs
       a known, non-self, non-duplicate email → `CreateGuardianLinkAsync`
       is called with the resolved subject id + the child id (the happy
       path; the `AddChild` action's `RegisterAsync` +
       `CreateGuardianLinkAsync` pairing precedent, minus the
       `RegisterAsync`). Seed: a child account + a guardian account (an
       active `GuardianLink` over the child) + a **new** account with a
       known email (no existing `GuardianLink` over the child); act: POST
       `me/children/{childId}/assign` with the new account's email; assert:
       302 (the redirect to `Detail`) + a **new** `GuardianLink` row for
       the `(newAccountId, childId)` pair (the `Active` status) + **one**
       `guardian.create` audit row (the `ActorId`/`EffectivePrincipalId`
       are the assigning guardian; the `TargetId` is the new
       `GuardianLink` row's id; the `TargetKind` is `"guardian-link"`).
- **Exit:** `dotnet build` green. The two `Detail.cshtml` appends render;
  the localization keys are in `KnownTranslationKeys.cs`; the 5 Web tests
  are in `tests/Kumunita.Web.Tests/GuardianAssignmentTests.cs`. Run via the
  **reliable path** (AGENTS.md): `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  — reports **5 GA Web tests discovered** (the file's others may co-run).
  Record the pass/red status of each (for U07's gate). **No gate recorded**
  (U07). Handoff note: 5–6 lines starting `## U06 — view + l10n + Web
  tests (5)` — (a) the two `Detail.cshtml` append paths (the "other
  guardians" list + the assign form), (b) the localization keys (the
  `guardian.assign.*` + `guardian.otherGuardians.*` prefixes), (c) the 5
  Web test names (verbatim) + the pass/red counts (for U07 to consume), (d)
  a confirmation the existing `Detail.cshtml` curation sections are
  untouched, (e) any `## U<m> — Drift pause`.

### U07 — close: `ARCHITECTURE.md` flip + `## GA — Closed (recorded)` + the
roadmap trio flip + move to `done/`

- **Goal:** flip the `Identity/` + `UserInfo/` lines in
  `docs/ARCHITECTURE.md` to record the GA lane's seams + the
  `GuardianItem` VM, write the **`## GA — Closed (recorded)`** section in
  the design doc, **flip the roadmap trio** (README / `Milestones.cs` /
  `MilestonesTests.cs` — the AGENTS.md contract), and **move the lane's
  files to `done/`** (the register, the unit plans
  `guardian-assignment-uNN-plan.md`, and the handoff notes) — the
  loop-closing step (the GU lane's U11 analog).
- **Entry reads:** `docs/ARCHITECTURE.md` §2 (the `Identity/` line to
  extend + the `UserInfo/` line to extend; the `Posts/` line as the "✓
  live" flip precedent; the `Events/`/`Projects/` lines **untouched**);
  `docs/design/guardian-assignment-design.md` (full — the invariants, the
  FACES, the gate, the drift-guard the close summarizes);
  `docs/plans-milestones/in-progress/plan-guardian-assignment.md` (this
  register, full); `docs/plans-milestones/in-progress/
  guardian-assignment-handoff-notes.md` (all `##` sections U01–U06 wrote);
  `README.md` §Roadmap (the GA line to append — the GU line's precedent);
  `src/Kumunita.Web/Milestones.cs` (the GA line to append — the GU line's
  precedent; the `MilestonesTests.cs`'s "single-in-progress" + "order"
  assertions to update in the **same commit**);
  `tests/Kumunita.Web.Tests/MilestonesTests.cs` (the "single-in-progress" +
  "order" assertions — the GA line's position: **after** the `GU` line,
  **before** `M4`).
- **Deliverables (5 files modify + 9 file moves):**
  - `docs/ARCHITECTURE.md` — the `Identity/` line: add
    `FindSubjectByEmailAsync` (the GA seam, ADR 0038) to the module's seam
    list, marked ✓, with a one-line note (the email → subject id
    resolution; the G-A·2 no-leak shape). The `UserInfo/` line: add
    `GuardianItem` (the GA VM, the Detail view's "other guardians" list)
    + the `MembershipEditorModel.GuardianItems` field, marked ✓, with a
    one-line note (the assigned guardian's display name; the G-A·3
    "identical in kind" pin). `Events/`/`Projects/` lines untouched.
  - `docs/design/guardian-assignment-design.md` — append
    `## GA — Closed (recorded)`: the three tests (U07's record — the
    closed-loop / handoff / part-vs-whole gate, the 3 Core tests from U03 +
    the 5 Web tests from U06), the `ARCHITECTURE.md` flip (this unit), the
    ADR 0038 §E non-decisions (each named: no remove path; no
    acceptance/consent step; no bulk assign; no self-assignment (refused,
    not a lane); no second audit verb; no email notification), and the "GA
    is closed; the three tests are recorded in §Pinned contract; the named
    deferrals are the ADR 0038 §E list" line.
  - `README.md` §Roadmap — append the GA line (the GU line's precedent —
    the "named lane, not a milestone letter" convention): `GA — Guardian
    assignment — an existing guardian assigns a second guardian to a child's
    account (email-driven; one `IIdentityService` ADD + one
    `GuardianController` action + the Detail view's assign form + ADR
    0038)` — the status is **done** (the lane is closed in this unit).
  - `src/Kumunita.Web/Milestones.cs` — append the GA line (the GU line's
    precedent — the "named lane, not a milestone letter" convention):
    `new("GA", "Guardian assignment — an existing guardian assigns a second guardian to a child's account (email-driven; one IIdentityService ADD + one GuardianController action + the Detail view's assign form + ADR 0038)", StatusDone)` —
    appended **after** the `GU` line, **before** `M4`. The `MilestonesTests.cs`'s
    "single-in-progress" + "order" assertions are updated in the **same
    commit** (the AGENTS.md contract) — the GA line is `StatusDone` (not
    `StatusNext`), so the "single-in-progress" assertion is **unchanged**
    (M4 is still the single `StatusNext`); the "order" assertion gains the
    GA line (after GU, before M4).
  - `tests/Kumunita.Web.Tests/MilestonesTests.cs` — update the "order"
    assertion (the GA line's position: after GU, before M4). The
    "single-in-progress" assertion is **unchanged** (M4 is still the
    single `StatusNext` — the GA line is `StatusDone`).
  - **File moves (PowerShell `Move-Item`, one self-contained command):**
    `docs/plans-milestones/in-progress/plan-guardian-assignment.md` →
    `done/guardian-assignment/`; every
    `docs/plans-milestones/in-progress/guardian-assignment-uNN-plan.md` →
    `done/guardian-assignment/`;
    `docs/plans-milestones/in-progress/guardian-assignment-handoff-notes.md`
    → `done/guardian-assignment/`. Confirm `in-progress/` is empty
    afterward.
- **Exit:** `ARCHITECTURE.md`'s `Identity/` + `UserInfo/` lines are
  flipped; the design doc ends with `## GA — Closed (recorded)`; the README
  §Roadmap + `Milestones.cs` + `MilestonesTests.cs` trio is flipped (the GA
  line is `StatusDone`, the "single-in-progress" assertion is unchanged,
  the "order" assertion gains the GA line); the lane's files are in
  `done/guardian-assignment/` and `in-progress/` is empty. The handoff
  note's `## Summary` (this unit) is the sole GA→next handoff artifact.
  **No build** (docs + moves only). Handoff note: the `## Summary` section
  is present — a table of the shipped units (U01–U07) with their one-liner
  goal + test count + any deviations + the ADR 0038 §E deferral list (each
  named).
