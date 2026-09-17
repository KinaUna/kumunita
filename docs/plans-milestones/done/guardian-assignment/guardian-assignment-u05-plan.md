# GA U05 — Web: `GuardianController.Assign` action + the
`ActiveGuardiansAsync` helper + the `Detail` action's `GuardianItems`

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained: it names every entry read, every deliverable, and the exit
> criteria. The register (`docs/plans-milestones/in-progress/
> plan-guardian-assignment.md`) is the cross-reference; when the two disagree,
> **this file wins for what to do** and the register wins for *which files
> exist and in what order*.

## Goal

Add the **one** new `GuardianController` action (`Assign`) + the **one** new
private helper (`ActiveGuardiansAsync`) + the **one** line on the existing
`Detail` action (the `GuardianItems` argument to the `MembershipEditorModel`
construction). **No view** (U06 is the view + the localization keys).

## Context you need (read these first, in this order)

1. `docs/design/guardian-assignment-design.md` § **`## Pinned contract`
   → `### GuardianController.Assign (exact C#)`** + **`###
   Detail.cshtml changes (exact markup)`** (U01 pinned the action + the
   helper) — the *primary* source for this unit. Match them verbatim.
2. `src/Kumunita.Web/Controllers/GuardianController.cs` — the **existing**
   controller. Read the `ActiveLinkAsync` helper (~line 490) the standing
   gate reuses; the `AddChild` action (~line 320) the new `Assign` action's
   failure shape mirrors (the `ModelState.AddModelError` + the `View(form)`
   re-render + the `TempData["info"]` on success); the `ActiveChildrenAsync`
   helper (~line 460) the new `ActiveGuardiansAsync` helper mirrors
   (inverted); the `Detail` action (~line 88) the `GuardianItems` argument
   appends to.
3. `src/Kumunita.Web/Models/GuardianViewModels.cs` — U04's VMs: the
   `AssignGuardianForm` the action binds, the `GuardianItem` record the
   helper returns, the `MembershipEditorModel`'s `GuardianItems` field the
   `Detail` action populates.
4. `src/Kumunita.Core/Identity/IUserInfoService.cs` §`FindSubjectByEmailAsync`
   (U02's seam the action calls).
5. `src/Kumunita.Core/UserInfo/IUserInfoService.cs` §`CreateGuardianLinkAsync`
   (~line 788) — the seam the action calls (the idempotent upsert + the
   `guardian.create` audit).
6. `docs/adr/0038-guardian-assignment.md` §Decision (A)–(B) (the ADR
   authority for the action).
7. `docs/design/guardian-assignment-design.md` § Invariants G-A·1 (the
   standing gate) + G-A·2 (the resolution) + G-A·4 (the idempotency) +
   G-A·5 (the self-assignment refusal).

## Deliverables (1 file, modify)

### `src/Kumunita.Web/Controllers/GuardianController.cs` (modify)

Append, in order, **after** the existing `AddChild` action and **before**
the `// ── Read helpers ──` section:

1. **The `Assign` action** — the pinned contract's exact signature +
   doc-comment. Implementation:

   ```csharp
   /// <summary>
   /// GA (ADR 0038): assign a second guardian to this child. The standing
   /// gate (G-A·1 — the <c>ActiveLinkAsync</c> helper the <c>Dissolve</c>
   /// route already uses) runs first; a non-guardian → 404. The resolution
   /// (G-A·2 — the <c>FindSubjectByEmailAsync</c> seam) runs second; null
   /// → the form's error surface ("No account with that email.").
   /// Self-assignment (G-A·5) is the third step (a user-presentable error,
   /// never a 500). The <see cref="IUserInfoService
   /// .CreateGuardianLinkAsync"/> call is the fourth — one commit, one
   /// <c>guardian.create</c> audit row (C3), the <b>existing</b> GU seam
   /// (no new Core seam; G-A·4 — the idempotent upsert means a duplicate
   /// assignment is a no-op, not an error).
   /// </summary>
   [HttpPost("{childId}/assign")]
   [ValidateAntiForgeryToken]
   public async Task<IActionResult> Assign(string childId, [FromForm] AssignGuardianForm form)
   {
       if (!ModelState.IsValid)
           return View("Detail", new MembershipEditorModel(
               childId, new List<string>(), new List<string>(),
               new List<PendingInvitationItem>(), new List<GuardianItem>()));

       var subject = SubjectId(User);
       if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(childId))
           return NotFound();

       // G-A·1 — the standing gate.
       var link = await ActiveLinkAsync(subject, childId);
       if (link is null)
           return NotFound();

       var email = (form.Email ?? string.Empty).Trim();
       if (string.IsNullOrEmpty(email))
       {
           ModelState.AddModelError(nameof(AssignGuardianForm.Email), "An email is required.");
           return View("Detail", new MembershipEditorModel(
               childId, new List<string>(), new List<string>(),
               new List<PendingInvitationItem>(), new List<GuardianItem>()));
       }

       // G-A·2 — the resolution.
       var assignedId = await identity.FindSubjectByEmailAsync(email);
       if (assignedId is null)
       {
           ModelState.AddModelError(string.Empty, "No account with that email.");
           return View("Detail", new MembershipEditorModel(
               childId, new List<string>(), new List<string>(),
               new List<PendingInvitationItem>(), new List<GuardianItem>()));
       }

       // G-A·5 — self-assignment is refused.
       if (assignedId == subject)
       {
           ModelState.AddModelError(string.Empty, "You are already this child's guardian.");
           return View("Detail", new MembershipEditorModel(
               childId, new List<string>(), new List<string>(),
               new List<PendingInvitationItem>(), new List<GuardianItem>()));
       }

       try
       {
           // G-A·4 — the CreateGuardianLinkAsync seam is idempotent for the
           // (guardianId, childId) pair: a duplicate active row is a no-op.
           await userInfo.CreateGuardianLinkAsync(childId, assignedId);
           TempData["info"] = "Guardian assigned.";
       }
       catch (UnauthorizedAccessException)
       {
           return NotFound();
       }
       catch (InvalidOperationException ex)
       {
           ModelState.AddModelError(string.Empty, ex.Message);
           return View("Detail", new MembershipEditorModel(
               childId, new List<string>(), new List<string>(),
               new List<PendingInvitationItem>(), new List<GuardianItem>()));
       }

       return RedirectToAction(nameof(Detail), new { childId });
   }
   ```

   **Note on the `View("Detail", …)` calls:** the `Assign` action's error
   path re-renders the `Detail` view with a minimal `MembershipEditorModel`
   (empty collections) so the form's validation errors are visible. This is
   the `AddChild` action's `View(form)` precedent, adapted for the
   `MembershipEditorModel` shape (the `AssignGuardianForm` is not the
   `@model` of the `Detail` view — the `MembershipEditorModel` is). The
   `AssignGuardianForm.Email` value is preserved in the `ModelState` so the
   form re-binds. **Alternative (simpler):** if the `Detail` view's `@model`
   is the `MembershipEditorModel` and the `Assign` form is a **separate**
   `<form>` element within the `Detail` view (the pinned contract's
   `Detail.cshtml changes` section), then the `View("Detail", …)` calls
   should pass the **full** `MembershipEditorModel` (the same construction
   the `Detail` action uses). U06's view markup will clarify which shape is
   used; the above is the conservative shape (the `AddChild` precedent).

2. **The `ActiveGuardiansAsync` helper** — the `ActiveChildrenAsync` helper
   inverted (the child's active `GuardianLink` rows, not the guardian's
   child rows). Append it **after** the existing `ActiveChildrenAsync`
   helper (in the `// ── Read helpers ──` section):

   ```csharp
   /// <summary>
   /// GA (ADR 0038) — the child's <b>active</b>
   /// <see cref="GuardianLink"/> rows (a read, not a decision), joined to
   /// each guardian's display name (ids/names only — G-A·3). The
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

3. **The `Detail` action's `MembershipEditorModel` construction** — append
   the **one** new argument (`GuardianItems`). Find the existing
   `return View(new MembershipEditorModel(...))` line in the `Detail`
   action and add the `guardianItems` argument as the **last** parameter:

   ```csharp
   // Before the existing `return View(new MembershipEditorModel(...))`:
   var guardianItems = await ActiveGuardiansAsync(childId);

   // The existing return, with the new last argument:
   return View(new MembershipEditorModel(
       childId,
       groupIds.OrderBy(g => g, StringComparer.OrdinalIgnoreCase).ToList(),
       communityIds.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList(),
       invitations,
       guardianItems));  // ← the new argument (U04's VM)
   ```

   The existing `Detail` action's other lines are **untouched**.

**Note:** the `GuardianController`'s constructor already has
`IIdentityService identity` (the `AddChild` action uses it) — the
`Assign` action's `identity.FindSubjectByEmailAsync(email)` call is valid
without a constructor change. **No** new `using` needed (the
`AssignGuardianForm` + `GuardianItem` are in the `Kumunita.Web.Models`
namespace, already imported; the `GuardianLink` + `GuardianLinkStatus` are
in the `Kumunita.Core.UserInfo` namespace, already imported).

## Exit

`dotnet build` on `Kumunita.Web` green. The `Assign` action + the
`ActiveGuardiansAsync` helper + the `Detail` action's new argument compile.
**No new test** (U06 pins the Web tests). Handoff note: 6–8 lines starting
`## U05 — Assign action + ActiveGuardiansAsync` — (a) the action's exact
route (verbatim), (b) the 4 steps (standing gate → resolution →
self-assignment → `CreateGuardianLinkAsync`) in order, (c) the
`ActiveGuardiansAsync` helper's query (the `ChildId` + `Active` filter),
(d) the `Detail` action's new argument (the `GuardianItems` position —
last), (e) a confirmation the `AddChild` action + the `ActiveLinkAsync`
helper + the `ActiveChildrenAsync` helper are **untouched**, (f) any
compile warnings.
