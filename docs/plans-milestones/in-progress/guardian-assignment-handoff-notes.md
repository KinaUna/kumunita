# Guardian assignment (`GA`) — rolling handoff notes

> **The scratch tier** of the GA lane's three-tier contract (the design doc
> is primary, the register is secondary, this file is scratch). One section
> per unit, **appended, never rewritten**. Each unit writes exactly one short
> section before it exits; the next unit reads only that section + its own
> entry-reads list. A `## U<m> — Drift pause` section is a **blocker**: the
> next unit reads it first and either resolves it (recording the resolution
> in its own section) or carries it forward (naming it in its exit criteria).
>
> The skeleton below is the **only** pre-written content — every `##` section
> from here on is authored by a unit, in order.

## Lane open

- **Date:** 2026-09-16
- **Register:** `docs/plans-milestones/in-progress/plan-guardian-assignment.md`
  (U01–U07)
- **Design doc (primary):** `docs/design/guardian-assignment-design.md`
  (U01 authors it fresh — the invariants G-A·1–G-A·6, the FACES, the pinned
  contract, the 8 pinned test names, the acceptance gate, the drift-guard)
- **ADR:** `docs/adr/0038-guardian-assignment.md` (U01 authors it fresh —
  Amends 0028's G·4 non-decision for the existing-guardian case; 0006 one
  `IIdentityService` ADD; 0012/0013 the standing-gate shape inherited)
- **Scope:** an existing guardian assigns a second guardian to a child's
  account (email-driven). The GU lane (ADR 0028) already shipped the
  `GuardianLink` model (one row per pair, "one or two guardians") + the
  `CreateGuardianLinkAsync` seam (idempotent upsert) + the five supervisory
  actions. What is **new** in the GA lane: (1) the
  `IIdentityService.FindSubjectByEmailAsync` ADD (the email → subjectId
  resolution); (2) the `GuardianController.Assign` action (standing gate +
  resolve + refuse self + `CreateGuardianLinkAsync`); (3) the
  `AssignGuardianForm` + `GuardianItem` + `MembershipEditorModel.
  GuardianItems` VMs; (4) the `Detail.cshtml` two appends (the "other
  guardians" list + the assign form) + the localization keys; (5) ADR 0038;
  (6) the README / `Milestones.cs` / `MilestonesTests.cs` trio flip (U07).
- **Out of scope (the named deferrals, ADR 0038 §E):** no remove path
  (a co-guardian dissolving *another* co-guardian's link, or the child
  dissolving a co-guardian's link — the ADR 0028 G·5 safety valve remains
  the only "dissolve any active link" path); no acceptance/consent step on
  the assigned guardian; no bulk assign (one email per form); no
  self-assignment (refused — G-A·5); no second audit verb (`guardian.create`
  is the one verb); no email notification to the assigned guardian. Each
  re-litigates as an ADR 0038 amendment.
- **Test model:** Core seam tests in `Kumunita.Core.Tests` against
  `PostgresFixture` (U03 — the **3** pinned seam tests:
  `FindSubjectByEmail_ReturnsSubjectIdForKnownEmail`,
  `FindSubjectByEmail_ReturnsNullForUnknownEmail`,
  `FindSubjectByEmail_IsCaseInsensitiveOnEmail`). Web controller/VM
  data-shape tests in `Kumunita.Web.Tests` (U06 — the **5** pinned tests:
  `Assign_NonGuardian_Returns404`,
  `Assign_UnknownEmail_ReturnsValidationError`,
  `Assign_SelfAssignment_ReturnsValidationError`,
  `Assign_DuplicateAssignment_IsIdempotentNoOp`,
  `Assign_KnownEmail_CallsCreateGuardianLinkAsync`). The lane's
  **acceptance gate** (U01 pins it in the design doc `### Acceptance gate`;
  U07 records the run result) = the 3 Core tests + the 5 Web tests + the
  build line. Runner quirk (AGENTS.md) applies: run via `dotnet exec
  tests\…\.dll`, not `dotnet test`.
