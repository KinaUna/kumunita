# GU U05 — Core: membership curation admits guardian standing

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained: it names every entry read, every deliverable, and the exit
> criteria. The register (`docs/plans-milestones/in-progress/
> plan-guardian-controls.md`) is the cross-reference; when the two disagree,
> **this file wins for what to do** and the register wins for *which files
> exist and in what order*.

## Goal

Admit the **guardian standing** (`Via: Guardian`) into the four
membership-curation lanes — `AddGroupMemberAsync` / `RemoveGroupMemberAsync` /
`AddCommunityMemberAsync` / `RemoveCommunityMemberAsync` — so a guardian can
curate the **child's** community & group memberships (ADR 0028 §C, invariant
G·3: action-scoped, deny-by-default). The four lanes' **signatures are
unchanged**; the change is an additive `Via: Guardian` branch in their standing
derivation, **recorded as the narrower standing** (the ADR 0013 / "one
additive lane" discipline). **No new seam, no tests, no Web surface.**

## Context you need (read these first, in this order)

1. `docs/design/guardian-controls-design.md` § **`## Pinned contract`** → the
   membership-curation row (U01 pinned which lanes admit the guardian
   standing + the `Via: Guardian` record) + the "narrower standing" note — the
   *primary* source.
2. `docs/adr/0028-...md` §C (membership curation is one of the five actions;
   the guardian acts **for the child**, recorded `Via: Guardian`) + §A (the
   standing is **narrower** than the lane's existing bases — Owner/Moderator/
   Admin — and is recorded as such, the "M3B Via: Moderator when scoped"
   shape).
3. `src/Kumunita.Core/UserInfo/UserInfoService.cs` — the four lanes'
   **current** standing derivation (this is the shape U05 extends):
   - **Group lanes** (`AddGroupMemberAsync` ~line 268, `RemoveGroupMemberAsync`
     ~line 329): `var via = addedBy == group.OwnerId ? Owner : Admin;` (and the
     same for `removedBy`).
   - **Community lanes** (`AddCommunityMemberAsync` ~line 1591,
     `RemoveCommunityMemberAsync` ~line 1655): `var via =
     GateCommunityStanding(componentId, actorId, actorRoles);` (the private
     static gate ~line 1742, which throws `UnauthorizedAccessException` when
     the actor holds neither GlobalAdmin nor the community's Moderator scope).
4. `src/Kumunita.Core/UserInfo/UserInfoService.cs` § `GateCommunityStanding`
   (the **narrower-standing record** precedent the doc-comment already names:
   "a claim-holder acting through their community scope records via
   `Moderator` … a pure GlobalAdmin via `Admin`"). U05's guardian branch is the
   *third* base in this same "narrower wins" ladder.
5. `src/Kumunita.Core/UserInfo/GuardianLink.cs` (U02) — the doc U05 queries:
   an **active** link where `GuardianId == <the lane's actor>` **and**
   `ChildId == <the lane's `userId`>` (the child being curated) is the
   guardian-standing basis.

## The standing rule U05 implements (pin this)

In each of the four lanes, the actor curates a membership **for the `userId`
argument** (the child). The `Via` resolution becomes a three-base ladder,
**narrowest first**:

1. **Guardian** (new): the actor holds an **active** `GuardianLink` with
   `GuardianId == <actor>` and `ChildId == <userId>`. → record `Via: Guardian`,
   `EffectivePrincipalId = <actor>`. This is the **narrowest** base — it wins
   even when the actor also holds Owner / Moderator / Admin (a guardian acting
   for their child records the guardian standing, not their admin standing —
   the "narrower wins" rule the community gate already states).
2. **Owner** (group lanes only, unchanged): `actor == group.OwnerId`.
3. **Moderator / Admin** (community lanes, unchanged; `GateCommunityStanding`).

The guardian branch is **deny-by-default** (G·3): it fires **only** when the
active-link precondition holds; it does not widen any lane. The mandatory-
community exception (ADR 0012) and the owner-row exception are **preserved
verbatim** — U05 must not relax either.

## Deliverables (≤2 files, modify)

### 1. `src/Kumunita.Core/UserInfo/UserInfoService.cs` (modify)

- Add **one** private helper (next to `GateCommunityStanding`) that resolves
  the guardian standing:

```
    /// <summary>
    /// The GU (ADR 0028) standing basis for the membership-curation lanes:
    /// the actor holds an **active** <see cref="GuardianLink"/> with
    /// <c>GuardianId == actor</c> and <c>ChildId == child</c> — i.e. the
    /// actor is curating their own child's membership. Returns
    /// <see cref="Authorization.AccessVia.Guardian"/> when that holds, else
    /// <c>null</c> (the lane's existing base — Owner / Moderator / Admin —
    /// then applies). Narrower-standing record (G·3): a guardian acting for
    /// their child records <c>Guardian</c>, not a broader role they also hold.
    /// Read-only — no session mutation.
    /// </summary>
    private async Task<Authorization.AccessVia?> GateGuardianStandingAsync(
        string actorId, string childId)
```

  (implement it as a `store.QuerySession()` read of the active link — the
  read-session shape `GetPendingInvitationsForUserAsync` already uses; return
  `Guardian` or `null`.)

- In **each** of the four lanes, resolve the guardian base **first**, then fall
  through to the existing base:
  - **Group lanes**: before the existing `var via = … ? Owner : Admin;`, do
    `via = await GateGuardianStandingAsync(<actor>, userId)
    ?? (<existing Owner/Admin expression>);`. The `userId` argument is the
    child. (For `RemoveGroupMemberAsync` the actor is `removedBy`.)
  - **Community lanes**: before `var via = GateCommunityStanding(…)`, do
    `via = await GateGuardianStandingAsync(actorId, userId)
    ?? GateCommunityStanding(componentId, actorId, actorRoles);`. **Crucially**:
    the `GateCommunityStanding` throw (no GlobalAdmin / Moderator scope) must be
    **bypassed** when the guardian base fires — a guardian holds neither of
    those roles by definition, so resolve the guardian base *before* calling
    the gate (the `??` short-circuit does exactly this). The mandatory-
    community refusal in `RemoveCommunityMemberAsync` is **unchanged** (it
    throws before the `Via` line — preserve its order).

### 2. `src/Kumunita.Core/UserInfo/IUserInfoService.cs` (modify — **doc-comment only**)

- On **each** of the four lanes, extend the existing XML doc-comment with one
  sentence: the lane now admits the **guardian standing** (ADR 0028) — a
  guardian curating their child's membership records `Via: Guardian` (the
  narrower standing). **No signature change, no new parameter.**

## Exit

`dotnet build Kumunita.slnx -c Debug` green. The four lanes record
`Via: Guardian` when the active-link precondition holds, and fall through to
their existing base otherwise. **No new test** (U09's
`Membership_AddRemoveChild_ViaGuardian` pins this — U05 is the *impl* that test
asserts). **The mandatory-community + owner-row exceptions are unchanged.**
Handoff note (append): 5–7 lines starting `## U05 — membership curation
admits guardian standing` — (a) the helper name + its return contract
(`Guardian` or `null`), (b) how each lane's `Via` expression changed (the
`??` short-circuit, one line per lane), (c) confirmation the mandatory-
community + owner-row exceptions are **unchanged**, (d) confirmation the
interface signatures are **unchanged** (doc-comment only), (e) any compile
warnings.
