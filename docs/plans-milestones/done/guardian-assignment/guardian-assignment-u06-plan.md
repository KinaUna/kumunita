# GA U06 — Web: `Detail.cshtml` two appends + the localization keys + the Web
tests

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained: it names every entry read, every deliverable, and the exit
> criteria. The register (`docs/plans-milestones/in-progress/
> plan-guardian-assignment.md`) is the cross-reference; when the two disagree,
> **this file wins for what to do** and the register wins for *which files
> exist and in what order*.

## Goal

Append the **two** `Detail.cshtml` appends (the "other guardians" list + the
assign form — the pinned contract's exact markup) + the **localization
keys** (the `guardian.assign.*` + `guardian.otherGuardians.*` prefixes) +
author the **5** Web controller/VM data-shape tests in
`tests/Kumunita.Web.Tests/GuardianAssignmentTests.cs`. **This unit does NOT
run the acceptance gate (U07).**

## Context you need (read these first, in this order)

1. `docs/design/guardian-assignment-design.md` § **`## Pinned contract`
   → `### Detail.cshtml changes (exact markup)`** + **`### Pinned seam
   tests (exact names)`** (U01 pinned the markup + the 5 Web test names) —
   the *primary* source for this unit. Match them verbatim.
2. `src/Kumunita.Web/Views/Guardian/Detail.cshtml` — the existing curation
   view. Note the existing `<h2>` + `<form>` + `<ul class="list-group">`
   patterns the two appends match; the `<kw-l>` TagHelper usage (the
   `guardian.*` keys' precedent); the `Url.Action` routes.
3. `src/Kumunita.Web/Models/GuardianViewModels.cs` — U04's VMs: the
   `AssignGuardianForm` the form binds, the `GuardianItem` record the list
   iterates, the `MembershipEditorModel`'s `GuardianItems` field the list
   reads.
4. `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the existing
   `guardian.*` keys (the `// ── guardian (the /me/children child-accounts
   surface, GU ADR 0028) ──` section). The new `guardian.assign.*` +
   `guardian.otherGuardians.*` keys append to this block.
5. `tests/Kumunita.Web.Tests/` — an existing Web data-shape test to mirror
   the harness. Check for `GuardianViewModelsTests.cs` (the GU lane's U10
   tests) or the M2/M3 Web-test shape.
6. `src/Kumunita.Web/Controllers/GuardianController.cs` — U05's `Assign`
   action (the standing gate + the resolution + the refusal shape the tests
   assert) + U05's `ActiveGuardiansAsync` helper (the `GuardianItems` the
   `Detail` view's list reads).
7. `docs/adr/0038-guardian-assignment.md` §Decision (B) (the ADR authority
   for the view + the localization keys).
8. `docs/design/guardian-assignment-design.md` § Invariants G-A·1 (the
   standing gate) + G-A·2 (the resolution) + G-A·4 (the idempotency) +
   G-A·5 (the self-assignment refusal).

## Deliverables (3 files)

### 1. `src/Kumunita.Web/Views/Guardian/Detail.cshtml` (modify)

Append the **two** appends from the pinned contract at the **end** of the
existing view (after the existing curation sections, before the closing
`</div>`). The existing curation sections (the group membership editor, the
community membership editor, the pending-invitation approval list, the
dissolve control) are **untouched**.

The exact markup (from the pinned contract):

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
    <div class="alert alert-info">
        <kw-l key="guardian.otherGuardians.empty">No other guardians assigned.</kw-l>
    </div>
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

**Note:** the `<label asp-for="Email">` + `<input asp-for="Email">` +
`<span asp-validation-for="Email">` helpers bind to the `AssignGuardianForm`
model. Since the `Detail` view's `@model` is the `MembershipEditorModel`
(not the `AssignGuardianForm`), the `asp-for` helpers will **not** work
directly. Instead, use **plain HTML** with the `name` attribute matching
the `AssignGuardianForm.Email` property name:

```html
<div class="mb-3">
    <label for="Email" class="form-label"><kw-l key="guardian.assign.email">Email of the guardian to assign</kw-l></label>
    <input type="email" name="Email" id="Email" class="form-control" value="@ViewData["Email"]" />
    <span class="text-danger"></span>
</div>
```

And the `Assign` action's error path sets `ViewData["Email"] = form.Email`
so the form re-binds the typed email. **Update U05's `Assign` action** to
set `ViewData["Email"] = email` in each error-path `View("Detail", …)` call
(if U05's action doesn't already do this). The `ModelState` errors are
rendered by the `<div asp-validation-summary="All">` helper.

### 2. `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (modify)

Append the **localization keys** (the existing `guardian.*` block's
precedent — the `// ── guardian (the /me/children child-accounts surface,
GU ADR 0028) ──` section):

```csharp
// ── guardian assignment (GA ADR 0038) ──
["guardian.otherGuardians.title"] = "Other guardians",
["guardian.otherGuardians.empty"] = "No other guardians assigned.",
["guardian.assign.title"]         = "Assign a guardian",
["guardian.assign.email"]         = "Email of the guardian to assign",
["guardian.assign.submit"]        = "Assign",
["guardian.assign.noAccount"]     = "No account with that email.",
["guardian.assign.self"]          = "You are already this child's guardian.",
["guardian.assign.success"]       = "Guardian assigned.",
```

The `Detail.cshtml` appends use the `<kw-l key="...">` TagHelper (the
existing `guardian.*` keys' precedent) for the user-facing strings. The
`Assign` action's `ModelState.AddModelError` messages are the **English
floor** (the `KnownTranslationKeys` values) — the TagHelper's resolution is
the per-request override (the ML-UI lane's precedent).

### 3. `tests/Kumunita.Web.Tests/GuardianAssignmentTests.cs` (new)

**5 tests**, one per pinned name. Mirror the existing Web-test harness
(check `tests/Kumunita.Web.Tests/` for the GU lane's `GuardianViewModelsTests.cs`
or the M2/M3 Web-test shape — the `GuardianController` injection, the
`ClaimsPrincipal` setup, the `IActionResult` assertions):

1. `Assign_NonGuardian_Returns404` — a non-guardian POSTs
   `me/children/{childId}/assign` → 404 (G-A·1; the ADR 0012/0013 "a
   non-guardian learns nothing" shape). Seed: a child account + a
   non-guardian account (no `GuardianLink` over the child); act: POST
   `me/children/{childId}/assign` with a valid email; assert: 404.
2. `Assign_UnknownEmail_ReturnsValidationError` — a guardian POSTs with
   an unknown email → the form re-renders with "No account with that
   email." (G-A·2; the `AddChild` action's failure shape). Seed: a child
   account + a guardian account (an active `GuardianLink` over the child) +
   **no** account with the typed email; act: POST
   `me/children/{childId}/assign` with the unknown email; assert: 200 (the
   form re-renders) + the `ModelState` has the "No account with that email."
   error.
3. `Assign_SelfAssignment_ReturnsValidationError` — a guardian POSTs their
   own email → the form re-renders with "You are already this child's
   guardian." (G-A·5). Seed: a child account + a guardian account (an
   active `GuardianLink` over the child); act: POST
   `me/children/{childId}/assign` with the guardian's own email; assert:
   200 (the form re-renders) + the `ModelState` has the "You are already
   this child's guardian." error.
4. `Assign_DuplicateAssignment_IsIdempotentNoOp` — a guardian POSTs an
   email that already has an active link over the child → the redirect
   (302) with no second audit row (G-A·4; the `CreateGuardianLinkAsync`
   idempotency). Seed: a child account + a guardian account (an active
   `GuardianLink` over the child) + a **second** guardian account (an
   **existing** active `GuardianLink` over the child); act: POST
   `me/children/{childId}/assign` with the second guardian's email (a
   duplicate); assert: 302 (the redirect to `Detail`) + **no** second
   `guardian.create` audit row for the `(secondGuardianId, childId)` pair
   (the idempotent no-op).
5. `Assign_KnownEmail_CallsCreateGuardianLinkAsync` — a guardian POSTs a
   known, non-self, non-duplicate email → the redirect (302) + a **new**
   `GuardianLink` row for the `(newAccountId, childId)` pair (the `Active`
   status) + **one** `guardian.create` audit row (the `ActorId`/
   `EffectivePrincipalId` are the assigning guardian; the `TargetId` is the
   new `GuardianLink` row's id; the `TargetKind` is `"guardian-link"`).
   Seed: a child account + a guardian account (an active `GuardianLink`
   over the child) + a **new** account with a known email (no existing
   `GuardianLink` over the child); act: POST `me/children/{childId}/assign`
   with the new account's email; assert: 302 (the redirect to `Detail`) +
   the new `GuardianLink` row + the audit row.

The file's doc-comment anchors the GA lane (ADR 0038) + the G-A·1–G-A·5
invariants the tests pin.

## Exit

`dotnet build` green. The two `Detail.cshtml` appends render; the
localization keys are in `KnownTranslationKeys.cs`; the 5 Web tests are in
`tests/Kumunita.Web.Tests/GuardianAssignmentTests.cs`. Run via the
**reliable path** (AGENTS.md): `dotnet build Kumunita.slnx -c Debug` then
`dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
— reports **5 GA Web tests discovered** (the file's others may co-run).
Record the pass/red status of each (for U07's gate). **No gate recorded**
(U07). Handoff note: 5–6 lines starting `## U06 — view + l10n + Web
tests (5)` — (a) the two `Detail.cshtml` append paths (the "other
guardians" list + the assign form), (b) the localization keys (the
`guardian.assign.*` + `guardian.otherGuardians.*` prefixes), (c) the 5 Web
test names (verbatim) + the pass/red counts (for U07 to consume), (d) a
confirmation the existing `Detail.cshtml` curation sections are untouched,
(e) any `## U<m> — Drift pause`.
