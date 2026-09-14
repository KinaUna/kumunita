# GU U02 — `GuardianLink` POCO + `M1DocTypes` registration

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained: it names every entry read, every deliverable, and the exit
> criteria. The register (`docs/plans-milestones/in-progress/
> plan-guardian-controls.md`) is the cross-reference; when the two disagree,
> **this file wins for what to do** and the register wins for *which files
> exist and in what order*.

## Goal

Create the `GuardianLink` relationship document (the `DelegationGrant`/
`GroupInvitation` shape) + the `GuardianLinkStatus` enum, and register them on
the **existing** `M1DocTypes` surface — an additive line (no new `*DocTypes`,
no re-seed, ADR 0004 §B.1). **No seam logic, no tests.**

## Context you need (read these first, in this order)

1. `docs/design/guardian-controls-design.md` § **`## Pinned contract` →
   `### GuardianLink POCO (exact C#)`** (U01 pinned it) — the *primary*
   source for this unit. Match it verbatim.
2. `docs/adr/0028-guardian-controls-account-scope-supervision.md` §B (the
   relationship: one row per (guardian, child) pair, `Status` two-state,
   dissolve one-way) + §C (the `GuardianLink` rides the existing `M1DocTypes`
   surface, ADR 0004 §B.1, additive).
3. `src/Kumunita.Core/UserInfo/Group.cs` §`DelegationGrant` (~line 79) — the
   **shape precedent**: `Id`, `OwnerId`, `DelegateId`, `Scope`, `From`, `To?`,
   `RevokedBy?`, and the `IsActiveAt` helper's comment ("the service (not the
   document) is the resolver"). U02's `GuardianLink` mirrors this: **no**
   doc-side `IsActive` boolean (G·2 — the service resolves "active").
4. `src/Kumunita.Core/UserInfo/GroupInvitation.cs` — the `InvitationStatus`
   enum placement (file-scoped, top of the file) + the `ResolvedAt?` /
   `ResolvedBy?` "set when the row leaves Pending, null while live" pattern to
   mirror for `DissolvedAt?` / `DissolvedBy?`.
5. `src/Kumunita.Core/M1DocTypes.cs` §`Configure` (~line 48) — where
   `GuardianLink` registers: a neighbor of `opts.Schema.For<DelegationGrant>();`
   (~line 61), and the **business-key convention** to mirror:
   `opts.Schema.For<GroupInvitation>().UniqueIndex(i => i.GroupId, i =>
   i.UserId);` (surrogate `Id` is the Marten identity; the pair is the business
   key).

## Deliverables (2 files)

### 1. `src/Kumunita.Core/UserInfo/GuardianLink.cs` (new)

- A `namespace Kumunita.Core.UserInfo;` header (matching the file's neighbors).
- `public enum GuardianLinkStatus { Active, Dissolved }` (the two-state
  machine — file-scoped, like `InvitationStatus` in `GroupInvitation.cs`).
- `public sealed class GuardianLink` with **exactly** the pinned-contract
  fields: `public string Id { get; set; } = string.Empty;` (surrogate PK),
  `public string GuardianId { get; set; } = string.Empty;`, `public string
  ChildId { get; set; } = string.Empty;`, `public GuardianLinkStatus Status
  { get; set; }`, `public DateTimeOffset CreatedAt { get; set; }`, `public
  DateTimeOffset? DissolvedAt { get; set; }`, `public string? DissolvedBy
  { get; set; }`.
- A class doc-comment (mirror `GroupInvitation`'s voice): one row per
  (guardian, child) pair — a child may have one **or two** guardians; standing
  exists iff **any** active row (G·2, resolved by the **service**, not this
  doc); the standing basis is creation (G·4 — `GuardianId` is the creator);
  dissolve is effectively one-way in practice (re-attach is a fresh `Active`
  row, a deliberate new act).
- **Do not** add an `IsActive` boolean or a `Scope` field (those are
  `DelegationGrant`'s, not this doc's — the guardian does not act *as* the
  child, ADR 0028 §C / the design doc's "opposite direction" note).

### 2. `src/Kumunita.Core/M1DocTypes.cs` (modify)

- Add **one** line immediately after `opts.Schema.For<DelegationGrant>();`:
  `opts.Schema.For<GuardianLink>().UniqueIndex(g => g.GuardianId, g => g.ChildId);`
  with a one-line comment: `// GU (ADR 0028): one row per (guardian, child); the pair is the business key (GroupInvitation convention)`.
- **No** other line in `Configure` changes. **No** new `*DocTypes` file.

## Exit

`dotnet build Kumunita.slnx -c Debug` green. `GuardianLink.cs` compiles; the
`M1DocTypes` line is additive (no other line changed). **No new test** (U09
pins the lane's tests). Handoff note (append): 4–5 lines starting
`## U02 — GuardianLink + M1DocTypes` — (a) the POCO's fields (verbatim),
(b) the exact `M1DocTypes` line + its placement (after `DelegationGrant`),
(c) the unique-index fields (`GuardianId`, `ChildId`), (d) a confirmation there
is **no** doc-side `IsActive` boolean and **no** `Scope` field, (e) any compile
warnings.
