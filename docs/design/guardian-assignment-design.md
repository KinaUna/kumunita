# Design Doc — Guardian assignment (`GA`)

> The "Seams & contracts" section is mandatory. The three tests
> (closed-loop? handoff? part-vs-whole?) are run and recorded at the end.
>
> **This is a named lane (`GA`), not an M-letter** — the same convention as
> `GP`, `ML`, `GU`, `RE`, `TR`, `RC` (ADR 0013's "named capability, not a
> milestone letter"). It is **pulled forward ahead of M4** (Events),
> mirroring how `GU` was pulled forward in 2026-09-14. M4/M5/M6 stay
> Events / Projects / Portability.

## Context

The GU lane (ADR 0028) shipped the **account-level supervision** surface for
a child's account: a parent *creates* the child's account (the usual
confirm-email lane), suspends/locks it, curates the child's community and
group memberships, approves a group invitation sent to the child, and
dissolves the link (independence). ADR 0028 §B already records that **"a
child may have one or two guardians (each an active row)"** — the
`GuardianLink` *document* and the `IUserInfoService.CreateGuardianLinkAsync`
*seam* both already support multiple active rows for the same `ChildId` (one
row per `(GuardianId, ChildId)` pair; `GuardActiveLinkAsync` resolves **any**
active row over the pair).

What the GU lane did **not** ship is a **surface** for a *second* guardian
to get standing over an **existing** child account. Today the only path to
`CreateGuardianLinkAsync` is `GuardianController.AddChild` (POST
`me/children`), which pairs it with `IIdentityService.RegisterAsync` — the
child's account must be **created** in the same commit (G·4: formation is
creation-based). A mother who created her child's account **cannot** later
add her ex-partner (or her co-parent, or a grandparent) as a second
guardian — ADR 0028 §D G·4 explicitly records "no self-serve 'claim
guardianship over an existing account' lane."

This lane fills exactly that gap: **an existing active guardian assigns a
second guardian** to a child's account. The arrow moved from *supervision*
(the GU lane) to *co-supervision* — but the standing conferred is the same
standing. The platform is invitation-only, one neighborhood, privacy-first
(`SECURITY.md` §1, ADR 0002): the assigned guardian's standing is
**identical in kind** to the creator's (the five GU actions, no content
read, no "assigned" tier). The lane is small by design — the Core already
supports multi-guardian standing (ADR 0028 §B); this lane adds **one
`IIdentityService` ADD**, **one `GuardianController` action**, **one view
model + one record + one record field**, and **two `Detail.cshtml`
appends**. Nothing else moves.

**Naming.** `GA` is a **named capability** (guardian assignment), not a
milestone letter. The roadmap letters **M4/M5/M6 stay Events / Projects /
Portability**.

## Scope

**In:**

1. `IIdentityService.FindSubjectByEmailAsync(string email)` — the **one
   new ADD** on the frozen `IIdentityService` surface (the ADR 0006-E
   compatible lane; the M1 lifecycle ADD precedent). The email → subjectId
   resolution the assign form needs; a read (no audit row, no mutation);
   null on unknown email (the Web's user-presentable error surface).
2. `GuardianController.Assign` — the **one new action** (`POST
   me/children/{childId}/assign`): the standing gate (G-A·1) → the
   resolution (G-A·2) → the self-assignment refusal (G-A·5) → the
   `CreateGuardianLinkAsync` call (G-A·4 idempotency; the **existing** GU
   seam, no new Core seam).
3. `AssignGuardianForm` (the one new view model) + `GuardianItem` (the one
   new record) + the `MembershipEditorModel.GuardianItems` field (the one
   new field on the existing record).
4. The `Detail.cshtml` two appends (the "other guardians" list + the assign
   form; the existing curation sections are untouched) + the
   `guardian.assign.*` / `guardian.otherGuardians.*` localization keys.
5. ADR 0038 (the named decision that supersedes ADR 0028 G·4's
   non-decision for the *existing active guardian* case).
6. This design doc's pinned contract + the seam tests (8 pinned names) +
   the acceptance gate.
7. The README / `Milestones.cs` / `MilestonesTests.cs` trio flip (U07, the
   AGENTS.md contract).

**Out (named deferrals, ADR 0038 §E):**

- **No remove path** — a co-guardian dissolving *another* co-guardian's
  link, or the child dissolving a co-guardian's link. The ADR 0028 G·5
  safety valve (a GlobalAdmin dissolving any active link, audited `Via:
  Admin`) remains the **only** "dissolve any active link" path.
- **No acceptance/consent step** on the assigned guardian — the standing
  is conferred by the assigning guardian's act, not the assigned
  guardian's consent (the ADR 0028 "the standing is from the active link,
  not from an acceptance" precedent).
- **No bulk assign** — one email per form.
- **No self-assignment** — a guardian cannot assign *themselves* as a
  second guardian over a child (refused, G-A·5 — the GU formation lane's
  territory, and `CreateGuardianLinkAsync` is already idempotent for the
  pair).
- **No second audit verb** — `guardian.create` is the one verb (its
  `ActorId` records the **assigned** guardian as standing-holder, not the
  assigning guardian — the GU seam's shape, §D).
- **No email notification** to the assigned guardian — the durable outbox
  / M1's verification lane is unchanged; the assigned guardian simply has
  standing on their next read.

Each is *deferred*, not *denied* — each re-litigates as an ADR 0038
amendment when it earns its keep.

## Invariants (pinned for GA)

- **G-A·1 — Standing is from the *assigning* guardian's active
  `GuardianLink` over the child, checked live.** The `ActiveLinkAsync`
  helper the `Dissolve` route already uses runs first; a non-guardian →
  404 (the ADR 0012/0013 "a non-guardian learns nothing" shape). The GU
  lane's G·2 precedent, inherited.
- **G-A·2 — The assigned guardian must have an account.** The email →
  subjectId resolution (the new `FindSubjectByEmailAsync` seam) returns
  null for an unknown email; the Web surfaces a user-presentable error
  ("No account with that email."), never a 500, **never an auto-create**
  (the GU lane's G·4 "formation is creation-based" precedent — the assign
  lane does **not** create accounts; it assigns standing over an *existing*
  one). A null return, not an exception that names the email (the ADR
  0008 shape).
- **G-A·3 — The assigned guardian's standing is identical in kind to the
  creator's.** The five GU actions, no content read, no "assigned" tier.
  The GA lane does **not** touch `IAuthorizationService`, does not touch
  `AccessVia`, does not touch the GU enforcement path. The `GuardianLink`
  POCO is **byte-identical** (no new field, no new enum value, no
  renumbering). Inherits G·1 from ADR 0028.
- **G-A·4 — Idempotency.** A duplicate `(GuardianId, ChildId)` active row
  is a no-op — the row is left as-is, no second audit row. The
  `CreateGuardianLinkAsync` seam already enforces this (the GU lane's G·4
  precedent, inherited); the GA lane does not re-implement it.
- **G-A·5 — Self-assignment is refused.** A guardian cannot assign
  *themselves* as a second guardian over a child — the `(actorId, childId)`
  pair is the same as the `(assignedId, childId)` pair, which is the GU
  formation lane's territory, and `CreateGuardianLinkAsync` is already
  idempotent for it (a no-op, not a useful act). The Web surfaces a
  user-presentable error ("You are already this child's guardian."), never
  a 500.
- **G-A·6 — No remove path (a named deferral, not a denial).** The GA lane
  does **not** add a "remove a co-guardian" surface. The ADR 0028 G·5
  safety valve (a GlobalAdmin dissolving any active link, audited `Via:
  Admin`) remains the **only** "dissolve any active link" path. A
  co-guardian dissolving *another* co-guardian's link, or the child
  dissolving a co-guardian's link, is a **future ADR 0038 amendment** (the
  "each re-litigates as an ADR amendment, not a toggle" precedent).

## FACES (pinned, 6)

- **F1** a non-guardian cannot assign (404) — G-A·1
- **F2** an unknown email is refused (user-presentable error, no
  auto-create) — G-A·2
- **F3** the assigned guardian gets the five GU actions on their next
  read — G-A·3
- **F4** a duplicate assignment is a no-op (no second audit row) — G-A·4
- **F5** self-assignment is refused (user-presentable error) — G-A·5
- **F6** no remove path (the ADR 0028 G·5 safety valve is the only
  dissolve path) — G-A·6

## Pinned contract (U01 — finalizes for U02–U06)

The exact C# U02–U06 must match, verbatim. Any mismatch against this
section is a `## U<m> — Drift pause` in the handoff notes.

### IIdentityService.FindSubjectByEmailAsync (exact C#)

The **one new ADD** on the frozen `IIdentityService` surface (the ADR
0006-E compatible lane; the M1 lifecycle ADD precedent — the
`ResendVerificationEmailAsync` shape, a read over
`userManager.FindByEmailAsync`). Placed in the M1 lifecycle block (the
`// ── M1 lifecycle (ADR 0006-E compatible lane) ──` section, after
`ResendVerificationEmailAsync`, before the break-glass lane):

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

The implementation is one line on `IdentityService` (the
`ResendVerificationEmailAsync` precedent — `var user = await
userManager.FindByEmailAsync(email); return user?.Id;`). No audit row, no
exception on unknown email (a null return).

### GuardianController.Assign (exact C#)

The **one new action** on the existing `GuardianController` (the
`AddChild` action's failure shape to mirror). The exact signature:

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

The implementation order (pinned, U05 fills it in): (1) the
`ActiveLinkAsync` standing gate → 404; (2) trim the form's email; (3)
`identity.FindSubjectByEmailAsync(email)` → null ⇒
`ModelState.AddModelError(string.Empty, "No account with that email.")` +
re-render; (4) `assignedId == subject` ⇒
`ModelState.AddModelError(string.Empty, "You are already this child's
guardian.")` + re-render; (5) `userInfo.CreateGuardianLinkAsync(childId,
assignedId)` in a try/catch (`UnauthorizedAccessException` → 404;
`InvalidOperationException` → the form's error surface); on success
`TempData["info"] = "Guardian assigned."` + redirect to `Detail`. One
commit, one `guardian.create` audit row (C3).

### AssignGuardianForm (exact C#)

The **one new view model** (the `AddChildForm` shape to mirror — a single
email field):

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

### GuardianItem (exact C#)

The **one new record** (the `ChildAccountItem` shape to mirror —
`SubjectId` + `DisplayName` only; the assigned guardian's `Profile.
SubjectId` and display name, resolved via `GetProfileAsync` — a read, not
a decision, G-A·3):

```csharp
public sealed record GuardianItem(string SubjectId, string DisplayName);
```

### MembershipEditorModel.GuardianItems (exact C#)

The **one new field** on the existing `MembershipEditorModel` record,
appended as the **last** parameter in the record's parameter list (the
existing fields `ChildId`, `GroupIds`, `CommunityIds`,
`PendingInvitations` are untouched):

```csharp
public sealed record MembershipEditorModel(
    string ChildId,
    IReadOnlyList<string> GroupIds,
    IReadOnlyList<string> CommunityIds,
    IReadOnlyList<PendingInvitationItem> PendingInvitations,
    IReadOnlyList<GuardianItem> GuardianItems);
```

### Detail.cshtml changes (exact markup)

The **two appends** to the existing `Views/Guardian/Detail.cshtml` (the
"other guardians" list + the assign form; the existing curation sections —
group memberships, community memberships, the pending-invitation list, the
dissolve control — are **untouched**):

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
<h2><kw-l key="guardian.otherGuardians.title">Other guardians</kw-l></h2>
@if (Model.GuardianItems.Count == 0)
{
    <div class="alert alert-info"><kw-l key="guardian.otherGuardians.empty">No other guardians assigned.</kw-l></div>
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
<h2><kw-l key="guardian.assign.title">Assign a guardian</kw-l></h2>
<form method="post" action="@Url.Action("Assign", "Guardian", new { childId = Model.ChildId })">
    @Html.AntiForgeryToken()
    <div asp-validation-summary="All" class="text-danger mb-3"></div>
    <div class="mb-3">
        <label asp-for="Email" class="form-label"><kw-l key="guardian.assign.email">Email of the guardian to assign</kw-l></label>
        <input asp-for="Email" class="form-control" />
        <span asp-validation-for="Email" class="text-danger"></span>
    </div>
    <button type="submit" class="btn btn-primary"><kw-l key="guardian.assign.submit">Assign</kw-l></button>
</form>
```

The view's `@model` stays `Kumunita.Web.Models.MembershipEditorModel`
(U04 adds the `GuardianItems` field; the form binds `AssignGuardianForm`
by its own property name on the POST).

### Localization keys (exact set)

Appended to `src/Kumunita.Core/Localization/KnownTranslationKeys.cs`
after the existing `guardian.*` block (the English floor for the TagHelper
resolution; the `ModelState.AddModelError` messages are the same strings):

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

### Pinned seam tests (exact names)

The file `tests/Kumunita.Core.Tests/GuardianAssignmentTests.cs` (U03
authors) with exactly these **3**:

1. `FindSubjectByEmail_ReturnsSubjectIdForKnownEmail`
2. `FindSubjectByEmail_ReturnsNullForUnknownEmail`
3. `FindSubjectByEmail_IsCaseInsensitiveOnEmail`

And the file `tests/Kumunita.Web.Tests/GuardianAssignmentTests.cs` (U06
authors) with exactly these **5**:

4. `Assign_NonGuardian_Returns404`
5. `Assign_UnknownEmail_ReturnsValidationError`
6. `Assign_SelfAssignment_ReturnsValidationError`
7. `Assign_DuplicateAssignment_IsIdempotentNoOp`
8. `Assign_KnownEmail_CallsAssignGuardianLinkAsync` (GA-AR rename,
   2026-09-17 — the action now calls the new seam)

And the **9th** (the acceptance gate's **handoff** leg, promoted from
inference to a test on 2026-09-17 — see `## GA — Closed (recorded)`;
ADR 0038 §Amendment): `tests/Kumunita.Core.Tests/` (the same Core file)

9. `Handoff_AssignedGuardian_CanSuspendAndUnsuspendChild` — after the GA
   lane confers standing (a second active `GuardianLink` over the child,
   via the same `CreateGuardianLinkAsync` seam `Assign` calls), the
   assigned guardian drives `SuspendChildAsync` / `UnsuspendChildAsync`
   over the child (G-A·3 — identical in kind to the creator's, no content
   read). The suspension row's `ActorId` is the **assigned** guardian.

**GA-AR (2026-09-17):** the pinned-test list is extended from 9 to
**11** names (5 Core + 6 Web) by the GA-AR lane (the second-audit-row
amendment, ADR 0038 §Amendment (2026-09-17, second)). The GA lane's 9
original names are unchanged (one renamed — #8, above); the GA-AR lane
adds:

10. `AssignGuardianLink_WritesBothAuditRows` — `tests/Kumunita.Core.Tests/`
    (Core) — the seam's both-audit-rows + idempotency shape (U01).
11. `Assign_KnownEmail_WritesAssignAuditRow` — `tests/Kumunita.Web.Tests/`
    (Web) — the `guardian.assign` row's `ActorId` = the assigning
    guardian (U02).

> **Count reconciliation (U02, 2026-09-17):** the register's prose pinned
> "10 (4 Core + 6 Web)"; the measured truth is **11** (5 Core + 6 Web) —
> the register undercounted U01's new Core test. The test NAMES above are
> authoritative; the 5/5 Core + 6/6 Web run results confirm the count.

A test whose exact name is not in this list is a `## U<m> — Drift pause`.
(A test added under this list — #9 — is an ADR 0038 amendment, not a
drift pause; the lane's own rule.

### Acceptance gate (U07 records)

The three tests:

- **closed loop** — a guardian assigns a second guardian to a child → the
  assigned guardian's standing is live on their next read (the
  `SuspendChildAsync` / `UnsuspendChildAsync` / membership lanes all
  resolve the new row).
- **handoff** — the assigned guardian, now a full guardian, can suspend /
  un-suspend / curate / approve over the child — the five GU actions, no
  content read. **Proven** by the pinned test #9
  (`Handoff_AssignedGuardian_CanSuspendAndUnsuspendChild`), not inferred
  (2026-09-17, ADR 0038 §Amendment).
- **part-vs-whole** — the **11-test** list (5 Core + 6 Web, after the
  GA-AR rename + add) is the whole; closed-loop + handoff are the parts;
  all must pass together.
- **audit legibility (GA-AR, 2026-09-17)** — the `guardian.assign`
  row's `ActorId` is the **assigning** guardian (the conferrer) — the
  §D named legibility limitation (the 2026-09-17 first amendment) is
  **resolved**; proven by `AssignGuardianLink_WritesBothAuditRows`
  (Core) + `Assign_KnownEmail_WritesAssignAuditRow` (Web).

### Drift-guard (frozen once written)

The `IIdentityService.FindSubjectByEmailAsync` seam + its doc-comment, the
`GuardianController.Assign` action + its doc-comment, the
`AssignGuardianForm` shape, the `GuardianItem` record, the
`MembershipEditorModel.GuardianItems` field, the `Detail.cshtml` two
appends, the localization key set, the 9 pinned test names (#1–#9, #9
added 2026-09-17), the
G-A·1–G-A·6 invariants, and the acceptance gate — all frozen pins; any
mismatch is a `## U<m> — Drift pause`.

**GA-AR (2026-09-17) extends the drift-guard** (the GA lane's existing
pins are unchanged): the `IUserInfoService.AssignGuardianLinkAsync` seam
+ its doc-comment, the `GuardianController.Assign` one changed line
(`CreateGuardianLinkAsync` → `AssignGuardianLinkAsync(childId, assignedId,
subject)`), the S·1–S·6 invariants, the FACES F1–F4, and the
3 new/renamed test names (`AssignGuardianLink_WritesBothAuditRows`,
`Assign_KnownEmail_CallsAssignGuardianLinkAsync`,
`Assign_KnownEmail_WritesAssignAuditRow`) — all frozen pins; any mismatch
is a `## U<m> — Drift pause`.

## Verification + reconciliation (2026-09-17)

A full code-vs-docs pass (plus the Fractal-Integration lens) confirmed the
lane is complete and green and reconciled two drifts (ADR 0038 §Amendment
(2026-09-17)):

- **Gate (re-run 2026-09-17): 9/9 PASS** — 4 Core (`FindSubjectByEmail_*` ×3
  + **`Handoff_AssignedGuardian_CanSuspendAndUnsuspendChild`**, the handoff
  leg now proven) + 5 Web (`Assign_*`); the full `Kumunita.Web.Tests` suite
  is 200/200. The **handoff** leg is now a test, not an inference.
- **Reconciled (docs corrected to the shipped code, which G-A·3 keeps
  authoritative):** the `guardian.create` row's `ActorId` /
  `EffectivePrincipalId` record the **assigned** guardian (the
  standing-holder) — the GU byte-identical seam's shape. The earlier prose
  ("both the assigning guardian") in this doc's §Scope / §D and in ADR 0038
  §D/§E did not match the code and is superseded; the **assigning**
  guardian's identity is **not persisted** on the row (named legibility
  limitation).
- **Superseded (GA-AR, 2026-09-17):** the named legibility limitation
  (the assigning guardian not persisted) is **resolved** by the GA-AR
  lane — the `guardian.assign` audit row's `ActorId` is the assigning
  guardian (ADR 0038 §Amendment (2026-09-17, second)). The
  `GuardianLink` POCO is still byte-identical (S·3) — the conferrer is
  in the **audit trail**, not the relationship document.
- **Carried (unchanged):** the `name="Email"` (not `asp-for`) note and the
  `Kumunita.Web.Tests` own-`PostgresFixture` note from the U06 drift pauses
  — both still hold.

## GA — Closed (recorded) (2026-09-17)

The GA lane (guardian assignment, ADR 0038) is **shipped**. The
`IIdentityService.FindSubjectByEmailAsync` ADD + the `GuardianController.
Assign` action + the `AssignGuardianForm` / `GuardianItem` / `MembershipEditorModel.GuardianItems` VMs + the `Detail.cshtml` two appends + the `guardian.assign.*` / `guardian.otherGuardians.*` localization keys are live. Standing is **identical in kind** to the creator's — invariant **G-A·3** held: the assigned guardian's standing is the same five GU actions, **no content read**, no "assigned" tier; the `GuardianLink` POCO and the GU enforcement path are byte-identical.

- **Decision record:** ADR 0038 (guardian assignment; amends 0028's G·4 non-decision for the existing-guardian case, 0006 one `IIdentityService` ADD, 0012/0013 the standing-gate + "a non-guardian learns nothing" 404 shape inherited).
- **Gate (U07's record — 2026-09-17):** the three tests (closed loop / handoff / part-vs-whole) are the 8 pinned seam tests below, all **PASS**:
  - **3 Core tests** (`tests/Kumunita.Core.Tests/GuardianAssignmentTests.cs`, U03 — `PostgresFixture`, the real M1 lifecycle `RegisterAsync` + `VerifyWithTokenAsync`, both EF `identity` + Marten `mt` stores over one fresh scratch Postgres DB):
    1. `FindSubjectByEmail_ReturnsSubjectIdForKnownEmail` — **PASS**
    2. `FindSubjectByEmail_ReturnsNullForUnknownEmail` — **PASS**
    3. `FindSubjectByEmail_IsCaseInsensitiveOnEmail` — **PASS**
  - **5 Web tests** (`tests/Kumunita.Web.Tests/GuardianAssignmentTests.cs`, U06 — integration tests via `PostgresFixture` (postgres:18), asserting real `GuardianLink` + `guardian.create` audit rows):
    4. `Assign_NonGuardian_Returns404` — **PASS**
    5. `Assign_UnknownEmail_ReturnsValidationError` — **PASS**
    6. `Assign_SelfAssignment_ReturnsValidationError` — **PASS**
    7. `Assign_DuplicateAssignment_IsIdempotentNoOp` — **PASS**
    8. `Assign_KnownEmail_CallsCreateGuardianLinkAsync` — **PASS**
  - **Total:** 8/8 passed, 0 failed. **Closed loop** (the assigned guardian's standing is live on their next read) + **handoff** (the assigned guardian, now a full guardian, can suspend / un-suspend / curate / approve over the child) + **part-vs-whole** (all 8 pass together) — the gate holds.
- **Drift pauses carried from U06 (named, not silently resolved):** (i) `Detail.cshtml` uses plain `name="Email"` (not `asp-for="Email"`) — the pinned `@model` is `MembershipEditorModel` (no `Email` property), so the `asp-for` TagHelper is uncompilable; the `Assign` action binds `AssignGuardianForm.Email` by its own property name on the POST, matching the existing curation-forms convention; (ii) the `guardian.create` audit row's `ActorId` is the **assigned** guardian (the `CreateGuardianLinkAsync(childId, guardianId)` seam sets `ActorId = guardianId`, and `guardianId` is the `assignedId` the `Assign` action passes) — the pinned contract §D prose says "assigning guardian", but the byte-identical GU seam (G-A·3 forbids a new seam) sets it to the assigned guardian; the tests assert the real behavior; (iii) `Kumunita.Web.Tests` now carries its own `PostgresFixture` (a byte-for-byte copy of `Kumunita.Core.Tests`'s) — the repo's documented convention ("Web.Tests is NSubstitute-only, Testcontainers lives in Core.Tests") is overridden by the explicit U06 requirement to run the 5 pinned tests against a real store.
- **Cross-lane red surfaced (NOT in GA's deliverables, not fixed by GA):** `GuardianViewModelsTests.MembershipEditorModel_Is_Exact_Four_Field_Projection` (GU lane, pre-existing) asserts exactly `{ "ChildId", "CommunityIds", "GroupIds", "PendingInvitations" }`, but U04 legitimately added `GuardianItems` as the 5th field → that test is **red** in the full `Kumunita.Web.Tests` suite (1 failure out of 200 total). This is a cross-lane breakage outside GA's sealed scope — surfaced to the user as a decision (update the GU projection test to expect 5 fields vs. leave it) rather than silently fixed or ignored.
- **Layout:** the `IIdentityService.FindSubjectByEmailAsync` ADD + the `GuardianController.Assign` action + the `AssignGuardianForm` / `GuardianItem` / `MembershipEditorModel.GuardianItems` VMs + the `Detail.cshtml` two appends + the `guardian.assign.*` / `guardian.otherGuardians.*` localization keys ride the existing surface (ADR 0004 §B.1 additive; `M1DocTypes` byte-identical — the `GuardianLink` line is already there, GU U02); `docs/ARCHITECTURE.md`'s `Identity/` + `UserInfo/` lines carry the surface (U07).
- **Non-decisions (ADR 0038 §E, each named, carried forward):** no remove path (a co-guardian dissolving *another* co-guardian's link, or the child dissolving a co-guardian's link — the ADR 0028 G·5 safety valve remains the only "dissolve any active link" path); no acceptance/consent step on the assigned guardian; no bulk assign (one email per form); no self-assignment (refused, G-A·5, not a lane); no second audit verb (`guardian.create` is the one verb); no email notification to the assigned guardian. Each re-litigates as an ADR 0038 amendment, not a toggle.
- **M4/M5/M6 untouched** (Events / Projects / Portability — the named-lane discipline: GA is not a renumber).

## GA-AR — Second audit row (implemented) (2026-09-17)

The GA-AR lane (guardian assignment, second audit row; ADR 0038
§Amendment (2026-09-17, second)) is **shipped**. It resolves the §D
named legibility limitation the GA lane's first amendment recorded
(the assigning guardian not persisted) by adding a **second audit row**,
`guardian.assign` (`ActorId` = the assigning guardian / conferrer),
written in the **same commit** as the `GuardianLink` row + the
`guardian.create` row (`ActorId` = the assigned guardian). The conferrer
is now on the **audit trail**, not the relationship document — the
`GuardianLink` POCO stays byte-identical.

- **Pinned contract (the register, primary):** the additive seam
  `IUserInfoService.AssignGuardianLinkAsync(string childId, string
  guardianId, string assignedById)` (the conferral-based standing basis,
  vs. `CreateGuardianLinkAsync`'s creation-based basis) + the
  `GuardianController.Assign` **one changed line**
  (`CreateGuardianLinkAsync(childId, assignedId)` →
  `AssignGuardianLinkAsync(childId, assignedId, subject)`). The
  standing gate (G-A·1), the resolution (G-A·2), the self-assignment
  refusal (G-A·5), the `try/catch` shape, the `TempData["info"]` line,
  and the redirect are **untouched**.
- **Invariants S·1–S·6** (each named):
  - **S·1 — C3 atomicity:** the link + the `guardian.create` row + the
    `guardian.assign` row are in **one** `SaveChangesAsync`; a partial
    write is a bug, not a state.
  - **S·2 — GU seam byte-identical:** `CreateGuardianLinkAsync(childId,
    guardianId)` unchanged in signature, behavior, and audit shape; the
    GU lane's `AddChild` call site unchanged.
  - **S·3 — POCO byte-identical:** `GuardianLink` has no new field / enum
    value; `M1DocTypes` unchanged.
  - **S·4 — Standing unchanged:** the assigned guardian's standing is
    identical in kind to the creator's; no new GU action / content read /
    "assigned" tier; the `IAuthorizationService` frozen surface unchanged.
  - **S·5 — Audit shape:** `guardian.assign`'s `ActorId` /
    `EffectivePrincipalId` = the **assigning** guardian;
    `guardian.create`'s `ActorId` / `EffectivePrincipalId` = the
    **assigned** guardian; both `TargetId`s = the link's id, both
    `Via = Guardian`, `Outcome = Allow`, `TargetKind = "guardian-link"`.
  - **S·6 — Idempotency (G-A·4, inherited):** a duplicate
    `(guardianId, childId)` active row is a no-op — no second row, no
    second pair of audit rows.
- **FACES F1–F4** (each named):
  - **F1** a non-guardian cannot assign (404) — G-A·1 (inherited).
  - **F2** the `guardian.assign` row's `ActorId` is the assigning
    guardian — S·5.
  - **F3** a duplicate assignment is a no-op (no second pair of audit
    rows) — S·6.
  - **F4** the GU seam + POCO are byte-identical — S·2, S·3.
- **Gate (U02's record — 2026-09-17):** the 11 pinned seam tests (5 Core
  + 6 Web) all **PASS** — 5/5 Core (`Kumunita.Core.Tests`, incl. the new
  `AssignGuardianLink_WritesBothAuditRows`), 6/6 Web
  (`Kumunita.Web.Tests`, incl. the renamed
  `Assign_KnownEmail_CallsAssignGuardianLinkAsync` + the new
  `Assign_KnownEmail_WritesAssignAuditRow`), full `Kumunita.Web.Tests`
  suite 201/201. **Closed loop** + **handoff** (inherited) +
  **part-vs-whole** (all 11 pass together) + **audit legibility** (the
  `guardian.assign` row's `ActorId` = the assigning guardian) — the gate
  holds. (The register's prose said "10 (4 Core + 6 Web)"; the measured
  truth is 11 (5 Core + 6 Web) — see the § Pinned seam tests
  reconciliation note.)
- **GU lane byte-identity (confirmed by reading the file):**
  `CreateGuardianLinkAsync`, the `GuardianLink` POCO, `M1DocTypes`, the
  five GU actions, `AccessVia`, and the GU enforcement path are all
  byte-identical (S·2, S·3, S·4) — the new seam is additive.
- **Drift-guard (frozen):** the `AssignGuardianLinkAsync` seam + its
  doc-comment, the `GuardianController.Assign` one changed line, the
  S·1–S·6 invariants, the FACES F1–F4, and the 3 new/renamed test names —
  all frozen pins (see § Drift-guard, GA-AR extension).
- **Roadmap trio untouched** (README / `Milestones.cs` /
  `MilestonesTests.cs`) — the GA-AR lane is an audit-legibility
  refinement of a shipped lane, not a new roadmap item (the GU/GA lane
  discipline). M4/M5/M6 stay Events / Projects / Portability.
