# GA U04 — Web: `AssignGuardianForm` + the `GuardianItem` record + the
`MembershipEditorModel` `GuardianItems` field

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained: it names every entry read, every deliverable, and the exit
> criteria. The register (`docs/plans-milestones/in-progress/
> plan-guardian-assignment.md`) is the cross-reference; when the two disagree,
> **this file wins for what to do** and the register wins for *which files
> exist and in what order*.

## Goal

Add the **one** new view model (`AssignGuardianForm`), the **one** new
record (`GuardianItem`), and the **one** new field on the existing
`MembershipEditorModel` (`GuardianItems`) — the Web's data-shape surface
for the GA lane. **No controller, no view** (U05 is the controller; U06 is
the view + the localization keys).

## Context you need (read these first, in this order)

1. `docs/design/guardian-assignment-design.md` § **`## Pinned contract`
   → `### AssignGuardianForm (exact C#)`** + **`### GuardianItem (exact
   C#)`** + **`### MembershipEditorModel.GuardianItems (exact C#)`** (U01
   pinned them) — the *primary* source for this unit. Match them verbatim.
2. `src/Kumunita.Web/Models/GuardianViewModels.cs` — the existing GU VMs.
   Read the `ChildAccountItem` record (the shape the new `GuardianItem`
   mirrors), the `MembershipEditorModel` record (the shape the new
   `GuardianItems` field appends to), and the `AddChildForm` class (the
   shape the new `AssignGuardianForm` mirrors — a single email field, the
   `[Required, EmailAddress]` validation).
3. `docs/adr/0038-guardian-assignment.md` §Decision (B) (the ADR authority
   for the VMs — the "one `AssignGuardianForm` view model + the one
   `GuardianItem` record + the one `MembershipEditorModel.GuardianItems`
   field" line).
4. `docs/design/guardian-assignment-design.md` § Invariants G-A·3 (the
   "identical in kind" pin the `GuardianItem` record's doc-comment anchors).

## Deliverables (1 file, modify)

### `src/Kumunita.Web/Models/GuardianViewModels.cs` (modify)

Append, in order, **after** the existing `AddChildForm` class (at the end of
the file):

1. **The `GuardianItem` record** — the **one** new record (the
   `ChildAccountItem` shape to mirror):

   ```csharp
   /// <summary>
   /// GA (ADR 0038): one <b>guardian row</b> on the child's <c>Detail</c>
   /// "other guardians" list. <see cref="SubjectId"/> is the assigned
   /// guardian's <see cref="Kumunita.Core.UserInfo.Profile.SubjectId"/>;
   /// <see cref="DisplayName"/> is resolved via the existing
   /// <see cref="Kumunita.Core.UserInfo.IUserInfoService.GetProfileAsync"/>
   /// (a read, not a decision — G-A·3: the assigned guardian's standing is
   /// identical in kind to the creator's). The <c>GuardianLink</c> row's
   /// <c>Status</c> / resolution stamps never reach the model (the list
   /// shows only <em>active</em> rows; G·2 — the service is the resolver).
   /// </summary>
   public sealed record GuardianItem(string SubjectId, string DisplayName);
   ```

2. **The `AssignGuardianForm` class** — the **one** new view model (the
   `AddChildForm` shape to mirror):

   ```csharp
   /// <summary>
   /// GA (ADR 0038): the assign-a-second-guardian form model, bound via
   /// <c>[FromForm]</c> on <c>GuardianController.Assign</c>. The
   /// <b>assigned guardian</b> is identified by email (the one external
   /// identifier; the <see cref="Kumunita.Core.Identity.IIdentityService
   /// .FindSubjectByEmailAsync"/> seam resolves it to a subject id). The
   /// <b>assigning guardian</b> is never form-bound — it is minted by the
   /// Web layer from <c>KumunitaPrincipal.SubjectId(User)</c> (the single
   /// identity source, the <c>AddChildForm</c> precedent). The <b>child</b>
   /// is the route's <c>{childId}</c>.
   /// </summary>
   public sealed class AssignGuardianForm
   {
       [Required, EmailAddress, MaxLength(255)]
       [Display(Name = "Email of the guardian to assign")]
       public string? Email { get; set; }
   }
   ```

3. **Modify the existing `MembershipEditorModel` record** — append **one**
   field: `IReadOnlyList<GuardianItem> GuardianItems` as the **last**
   parameter in the record's parameter list. The record's existing fields
   (`ChildId`, `GroupIds`, `CommunityIds`, `PendingInvitations`) are
   **untouched**. The exact modified record:

   ```csharp
   public sealed record MembershipEditorModel(
       string ChildId,
       IReadOnlyList<string> GroupIds,
       IReadOnlyList<string> CommunityIds,
       IReadOnlyList<PendingInvitationItem> PendingInvitations,
       IReadOnlyList<GuardianItem> GuardianItems);
   ```

   **Note:** this changes the record's parameter list (adds one parameter).
   Any existing construction site (the `GuardianController.Detail` action,
   U05 modifies) must be updated in the **same commit** — U05 does this.
   **No** other file in this unit.

## Exit

`dotnet build` on `Kumunita.Web` green (the two new types + the one new
field compile; the `MembershipEditorModel` construction in
`GuardianController.Detail` will **break** until U05 adds the `GuardianItems`
argument — this is expected and is U05's job). **No new test** (U06 pins the
Web tests). Handoff note: 4–5 lines starting `## U04 — VMs` — (a) the
`GuardianItem` record's fields (verbatim), (b) the `AssignGuardianForm`'s
field (verbatim), (c) the `MembershipEditorModel`'s new field (verbatim) +
its position in the record (last), (d) a confirmation the existing
`MembershipEditorModel` fields are untouched, (e) a note that
`GuardianController.Detail` will not compile until U05 adds the
`GuardianItems` argument, (f) any compile warnings.
