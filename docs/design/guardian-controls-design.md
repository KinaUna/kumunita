# Design Doc — Guardian controls (account-scope supervision of a child's account)

> The "Seams & contracts" section is mandatory. The three tests
> (closed-loop? handoff? part-vs-whole?) are run and recorded at the end.
>
> **This is a named lane (`GU`), not an M-letter** — the same convention as
> `GP`, `ML`, `ML-UI`, `TR`, `RC`, `TD` (ADR 0013's "named capability, not a
> milestone letter"). It is **pulled forward ahead of M4** (Events), mirroring
> how `ML` was pulled forward in 2026-09-12 so the platform could be exercised
> in more than one language before the circle widened. M4/M5/M6 stay
> Events / Projects / Portability.

## Context

The platform is invitation-only and limited to one neighborhood's residents
(`SECURITY.md` §1, ADR 0002). The realistic population is not only adults:
**there will be kids on the platform**, and integrating them well is part of
what makes the place a *community* (`the-platform-as-integrator.md`). But
online activity is also a real burden and a real risk for parents, and the
platform is privacy-first — a child's *audience choices* are as absolute as an
adult's (`SECURITY.md` §1, the "author's choice of audience is absolute by
default" rule; the `part-vs-whole` test in `START-HERE.md`).

Today the platform has **no notion of a minor and no notion of a guardian**.
The nearest primitives are all *adult-to-adult* or *owner-to-self*:

- **`DelegationGrant`** (`UserInfo/Group.cs`, `IUserInfoService`) — the closest
  shape, but it is the **opposite direction**: a delegate borrows the *owner's*
  standing to act *as* the owner (action-scoped, invariant C2). A guardian does
  **not** act *as* the child; the guardian holds a defined set of *controls over*
  the child's account. So this is a **new lane**, not a reuse — exactly the
  pattern ADR 0013 used for group posts (a new `AccessVia` value, action-scoped
  standing, one additive seam on a frozen surface).
- **`Profile.Blocked`** (`UserInfo/Profile.cs`, the `IIdentityService.BlockAsync`
  / `UnblockAsync` GlobalAdmin suspension lane, enforced by
  `BlockedAccountMiddleware`) — a reversible account suspension that strips all
  standing. A "parent locks the child's account" is the **child-scoped analog**
  of this well-precedented lane.
- **ADR 0012 community membership** (`AddCommunityMemberAsync` /
  `RemoveCommunityMemberAsync`, gated on `moderator:{id}` ∪ GlobalAdmin) —
  membership is the child's **posting/feed gate** on a component, so curating
  it *is* "allow/block access to communities."
- **The m2b `GroupInvitation` state machine** (`AcceptGroupInvitationAsync` /
  `DeclineGroupInvitationAsync`, C-M2b·2 self-lane, C-M2b·3) — the invitation
  a child receives, and the shape a "parent approves it" gate rides on.
- **`AccessVia`** (`Authorization/Decision.cs`) — the audit "by what right"
  vocabulary; the last two values (`Admin`, `Group`) are **named-lane appends**
  (the least-distortion slot, ADR 0006-E). A guardian standing fits the same slot.

The product want (2026-09-14), in the words it was given: **keep it simple,
controls only, no invasion of privacy for now.** Concretely: a parent can add
an account for a child (the usual confirm-email process); then suspend/lock the
account, allow/block the child's access to communities and groups, and approve a
group invitation sent to the child. And, when the child is old enough, a way to
make the child's account a normal, independent one.

The one thing this lane **must not** do — and the design that decides this
outright — is give a parent standing to **read the child's private content**.
That would invert the cardinal rule; the whole lane is scoped so it never does.

**Naming.** `GU` is a **named capability** (guardian controls), not a milestone
letter. The roadmap letters **M4/M5/M6 stay Events / Projects / Portability**.

## Goals / Non-goals

**In scope (the four controls + formation + come-of-age)**

1. **Formation** — a parent adds an account for a child. The account goes
   through the **usual verification lane** (`IIdentityService.RegisterAsync`
   → unverified account + `Profile` + `IdentityToken` + one verification email
   → child clicks the link, `VerifyWithTokenAsync`). A **`GuardianLink`**
   document (`GuardianId` → `ChildId`, `Active`) is created in the same commit
   (C3). The trust basis is **creation, not claim**: the parent *created* the
   account, so the link's `CreatedById` is the parent's own subject — a strong,
   self-evident basis that a stranger has no standing to contest.
2. **Suspend / lock** — the guardian suspends the child's account
   (`Profile.Blocked = true`, the same flag `BlockedAccountMiddleware` and the
   directory already read) and can un-suspend it. A child-scoped version of the
   GlobalAdmin `BlockAsync` lane, audited `Via: Guardian`.
3. **Allow / block communities & groups** — the guardian curates the child's
   **memberships**: add or remove the child from a component (ADR 0012 lanes)
   or a group (`AddGroupMemberAsync` / `RemoveGroupMemberAsync`). Membership is
   the access gate (ADR 0012: "the posting gate, the composer's picker, the
   feed … all read through this one seam"), so curating it *is* allowing or
   blocking the child's access — and it is **participation** control, not content
   reading.
4. **Approve a group invitation** — when a group owner invites the child, the
   child's **self-accept** lane is gated while a `GuardianLink` is active; the
   **guardian** resolves the child's `Pending` invitation (a `Via: Guardian`
   accept). The child's **self-decline** stays open (a child may always say no).
5. **Come of age (independence)** — the guardian **dissolves** the
   `GuardianLink` (the `DelegationGrant.RevokedBy` precedent). The child's
   self-lanes (self-accept, self-membership) **restore**; the child's **existing
   memberships are preserved** (the account is handed over, not stripped); the
   guardian loses all `Via: Guardian` standing.

**Out of scope — deliberate non-decisions (the "for now")**

- **No content reading.** The guardian gains **no** standing to read the
  child's audience-restricted posts, replies, or group posts; the child's
  `CanAsync` / `CanSeeAsync` are evaluated as if the child were a normal
  account. This is the cardinal rule **held, not deferred** — re-litigating it
  later requires an ADR 0028 amendment (the ADR 0013 "deliberate not-available"
  precedent).
- **No reading who contacted / replied to the child** (a content-level read).
- **No delegation of authorship** — the guardian does **not** post *as* the
  child (no `DelegationGrant`-style effective principal). The child's posts are
  the child's, authored in the child's language tag (ADR 0018), unattributed to
  the guardian.
- **No blanket "block all communities" toggle** — participation control is
  per-member (add/remove), the same weight as the adult lanes; a dedicated
  blanket toggle is a follow-on if it earns its keep.
- **Age / birthdate is not stored.** Standing comes from the **link itself**,
  not from a minor-PII field. (Storing a child's age would add a sensitive
  class to `SECURITY.md` for no control that the link does not already give.)

**Surfaces**

- A **"family / my children"** parent-side surface (`/me/children` or under
  `/admin`'s neighbor) listing the parent's child accounts, each with the
  suspend/un-suspend control, the community & group membership editor, and the
  pending-invitation approval list.
- The **add-a-child-account** form (drives `RegisterAsync` + the link, one
  commit; the verification email goes out on the usual lane).
- The **independence** control (dissolve the link; a confirm-guarded, reversible
  *only* by re-creating the link — a fresh act, not an undo).

## Human cost

This gives the **parent's** time and attention back: one place to keep a child's
account in check, instead of a parallel out-of-platform phone conversation and
supervision. It takes a **little** from the **child**: while the link is active,
the child's account autonomy (self-joining groups, self-managing memberships,
staying un-suspended) is subordinated to the guardian's — a real cost, paid
deliberately for the child's online safety. The **safety valve** is the escape:
a GlobalAdmin can dissolve any link or un-suspend any child account
("the community stands behind the child"), so a guardian cannot hold a child's
account hostage with no recourse. Team cost is moderate: it is a standing + a
doc + a handful of gates on **existing** lanes — invariant-light compared to
M1, heavier than a single-lane ADR.

## Parts affected

- **`Kumunita.Core`:**
  - `UserInfo/GuardianLink.cs` — the new relationship document (the
    `DelegationGrant` shape: `GuardianId`, `ChildId`, `Status` Active|Dissolved,
    `CreatedAt`, `DissolvedAt?`, `DissolvedBy?`). Registered on the **existing
    `M1DocTypes`** surface (ADR 0004 §B.1 — it rides `Schema.For<DelegationGrant>()`'s
    neighbor; no new `*DocTypes`, no re-seed).
  - `UserInfo/IUserInfoService.cs` — the guardian-management seams (below).
  - `Authorization/Decision.cs` — `AccessVia.Guardian`, the **9th** value
    appended after `Group` (the `Admin`/`Group` append precedent, ADR 0006-E).
  - `Identity` — **unchanged** as a frozen surface: the child-scoped
    suspend/un-suspend is a **new lane** that sets `Profile.Blocked` (a
    `UserInfo` doc) and audits `Via: Guardian`; the GlobalAdmin
    `BlockAsync`/`UnblockAsync` stay byte-identical.
  - ADR 0012's `AddCommunityMemberAsync` / `RemoveCommunityMemberAsync` and the
    group `AddGroupMemberAsync` / `RemoveGroupMemberAsync` — **admit the
    guardian standing** for a child target (a `Via: Guardian` branch); the
    m2b `AcceptGroupInvitationAsync` **gates on an active link** (refused for a
    child with a guardian) and a **new `ApproveGroupInvitationAsync`** resolves
    the child's invitation as the guardian.
- **`Kumunita.Web`:** the parent-side surface (a `GuardianController` or a
  section of the profile surface), the add-a-child form, the membership editor,
  the pending-invitation approval list, the independence (dissolve) control.
  Thin over the Core seams; the failure shape is the existing lane's (404 on no
  standing, the ADR 0008/0012 precedent — a non-guardian learns nothing).
- **`tests/Kumunita.Core.Tests`:** the seam tests (below) against the
  `PostgresFixture` fresh-scratch-DB shape.
- **Docs (this lane's records):** this design doc; **ADR 0028**; the
  `SECURITY.md` additions (a protected-class note for minors, adversary **A7**
  — a hostile guardian over a minor — and the control-map row); the `README`
  Features + Roadmap and `Milestones.cs` `GU` entry (the README / `Milestones.cs`
  / `MilestonesTests.cs` trio moves together).

## Seams & contracts (mandatory)

- **New standing: `AccessVia.Guardian` (the 9th value).** An additive enum
  value appended after `Group` — the **exact** precedent of the M1 `Admin`
  (7th) and ADR 0013 `Group` (8th) appends: a value-addition, never a
  renumbering; stored audit rows are untouched. It is **action-scoped** to the
  five supervisory actions above and **never** to a content read (invariant
  G·3 below).
- **New relationship doc: `GuardianLink`.** One row per (guardian, child) pair —
  a child may have one or two guardians (each an active row); the child has
  guardian standing iff **any** active row exists (the multi-group membership
  shape, not a single-owner). `Status` is a two-state machine
  (`Active → Dissolved`), the `DelegationGrant.RevokedBy` / the `GroupInvitation`
  state precedent. **Dissolve is effectively one-way in practice** (re-attaching
  is a fresh `Active` row — a deliberate new act, not an undo).
- **Access model touched — a *new standing on existing gates*, no new
  authorization lane on the frozen `IAuthorizationService`.** The content
  authorization path (`CanAsync` / `CanSeeAsync`, ADR 0006-A) is **untouched**:
  a child's content is read exactly as a normal account's. The guardian's
  standing is exercised on the **management** lanes (suspend, membership,
  invitation), which live on `IUserInfoService` — so the ADR 0006 frozen
  signatures stay byte-identical (the ADR 0013 "one additive lane, nothing else
  moves on the frozen surface" discipline). This is the lane's most important
  non-decision: **the guardian standing does not enter the content decision
  path at all.**
- **Seams created / changed, each with an owner:**
  - `IUserInfoService.CreateGuardianLinkAsync(childId, guardianId)` — the
    **formation lane**: upserts the `(GuardianId, ChildId)` `Active` row, audits
    `guardian.create` (`Via: Guardian`, target the child account), C3. The Web's
    add-a-child form pairs it with `IIdentityService.RegisterAsync` (the account
    + verification email) so the account, the link, and the audit rows commit
    together (C3). The trust basis is the link's `CreatedById = guardianId`.
  - `IUserInfoService.SuspendChildAsync(childId, guardianId)` /
    `IUserInfoService.UnsuspendChildAsync(childId, guardianId)` — the
    **suspension lane**: verifies the active link (else
    `UnauthorizedAccessException` → the Web's 404), sets
    `Profile.Blocked` true/false (the same flag the existing suspension lane and
    `BlockedAccountMiddleware` already read — so enforcement is identical),
    audits `guardian.suspend` / `guardian.unsuspend` (`Via: Guardian`, target
    the child account), C3. The GlobalAdmin `BlockAsync`/`UnblockAsync` are
    **unchanged**.
  - **Community & group membership curation** — the ADR 0012
    `AddCommunityMemberAsync` / `RemoveCommunityMemberAsync` and the group
    `AddGroupMemberAsync` / `RemoveGroupMemberAsync` lanes **admit the guardian
    standing** for a child target: the existing standing gate (moderator ∪
    GlobalAdmin) gains a `guardian of the target child` branch, and the audit
    row records the **narrower** standing — `Via: Guardian` (the ADR 0012
    "record the narrower standing" rule). No new method; a new branch on the
    existing seams.
  - **Group-invitation approval** — `IUserInfoService.ApproveGroupInvitationAsync
    (groupId, childId, guardianId)` resolves the child's `Pending` invitation as
    Accepted (the `AcceptGroupInvitationAsync` lane's membership write +
    `ResolvedAt`/`ResolvedBy` stamps), auditing `group.invite.approve`
    (`Via: Guardian`, target the group). Simultaneously,
    `AcceptGroupInvitationAsync` **gates**: if the invitee (the child) has an
    active `GuardianLink`, the self-accept is refused
    (`InvalidOperationException` → the Web's error, never a 500); the child's
    **decline** stays open. This is the C-M2b·2 self-lane's "one recorded
    exception for a supervised account" (the ADR 0008 owner-row-exception shape,
    carried to the supervised-child row).
  - `IUserInfoService.DissolveGuardianLinkAsync(linkId, actorId)` — the
    **independence lane**: moves the row `Active → Dissolved`
    (`DissolvedAt`/`DissolvedBy` stamped, the `RevokedBy` precedent), audits
    `guardian.dissolve` (`Via: Guardian` when the guardian acts; `Via: Admin` on
    the **safety-valve** branch where a GlobalAdmin dissolves a link — the
    "community stands behind the child" escape). The child's memberships are
    **preserved** (dissolving the link writes nothing to membership); the child's
    self-lanes restore on the very next read (C4).
- **Audit vocabulary — additive, all `Allow`, all C3.** The audit rows are
  *management actions* (like the role-change / manual-verify lanes), not access
  decisions on restricted content, so they do not engage the
  audience-restricted Allow/Deny machinery. New verbs: `guardian.create`,
  `guardian.suspend`, `guardian.unsuspend`, `guardian.dissolve` (target the
  child account, `Via: Guardian` — or `Via: Admin` on the dissolve safety valve);
  `group.invite.approve` (target the group, `Via: Guardian`); the existing
  `community.add-member` / `community.remove-member` / `group.add-member` /
  `group.remove-member` verbs now additionally carry `Via: Guardian` when a
  guardian curates a child's membership. No verb is repurposed; no stored row's
  meaning changes.
- **Invariants (named, so the seam tests can anchor to them):**
  - **G·1 — The guardian standing never reads the child's content.** The
    `AccessVia.Guardian` value appears on **no** `CanAsync` / `CanSeeAsync`
    decision; the child's content is authorized as a normal account. A test
    asserts a guardian cannot read a child's audience-restricted post.
  - **G·2 — Standing is from the active link, checked live (C4).** Every
    guardian lane resolves the child's active `GuardianLink` in the same read;
    a dissolve is live on the very next lane read (no projection, no cache).
  - **G·3 — Action-scoped, deny-by-default.** A guardian action outside the five
    supervisory actions (or on a target with no active link) is a Deny —
    including, explicitly, any content read. A scope unknown to old code denies
    on new actions by default (ADR 0006-E).
  - **G·4 — Formation is creation-based.** The `GuardianLink`'s standing basis is
    that the guardian *created* the child's account (`CreatedById`); there is no
    self-serve "claim guardianship over an existing account" lane.
  - **G·5 — The safety valve always exists.** A GlobalAdmin may dissolve any
    active link and un-suspend any account, audited `Via: Admin`; the child (or
    an adult on the child's behalf) can always reach it. No guardian may hold a
    child's account with no recourse.

## Pinned contract (U01 — finalizes for U02–U11)

> **U01 finalizes this section; U02–U11 match it verbatim.** It turns the
> `## Seams & contracts (mandatory)` prose above into machine-pinnable exact
> C#: the `GuardianLink` POCO, the `AccessVia.Guardian` value, the five
> `IUserInfoService` guardian seams (three new methods, two branches on
> existing lanes) + the invitation gate, the pinned seam-test names, and the
> acceptance gate. ADR 0028 (already accepted) is the authority every pin
> traces back to; this section is where the *exact* shape is frozen. **No code
> is authored here** — the C# below is the contract U02–U06 will write; it is
> pinned so the shape cannot drift between units.

### GuardianLink POCO (exact C#)

New file `src/Kumunita.Core/UserInfo/GuardianLink.cs` (U02 authors). One row
per (guardian, child) pair — the `DelegationGrant` / `GroupInvitation` shape;
the `GroupMembership` business-key convention (surrogate `Id` is the Marten
identity; `(GuardianId, ChildId)` is the business key, unique-indexed on the
`M1DocTypes` surface in U02):

```csharp
namespace Kumunita.Core.UserInfo;

/// <summary>
/// One row per (guardian, child) pair — the GU standing's relationship document
/// (ADR 0028 §B). A child may have one or two guardians (each an active row);
/// the child has guardian standing iff <b>any</b> active row exists.
/// <see cref="GuardianId"/> is the creator: G·4 (formation is creation-based)
/// is anchored here — the standing basis is that this guardian <i>created</i>
/// the child's account, so there is no self-serve "claim guardianship" lane.
/// <para>
/// G·2 — the service is the resolver, not this document. Whether a row is
/// "active" is decided by <c>IUserInfoService</c> off <see cref="Status"/> at
/// decision time, exactly like <c>DelegationGrant.IsActiveAt</c> (the "service
/// is the resolver" rule): this POCO carries the state, it does not carry an
/// <c>IsActive</c> boolean. A dissolve is live on the very next lane read (C4).
/// </para>
/// <para>
/// G·5 — the safety valve is anchored by <see cref="DissolvedBy"/> (the
/// GlobalAdmin who dissolved, on the <c>viaAdmin</c> branch) and the
/// <c>DissolveGuardianLinkAsync(viaAdmin: true)</c> audit row — a GlobalAdmin
/// may always dissolve an active link or un-suspend an account, audited
/// <c>Via: Admin</c>.
/// </para>
/// </summary>
public sealed class GuardianLink
{
    /// <summary>Surrogate PK; (GuardianId, ChildId) remains the business key.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The guardian account (the creator — G·4).</summary>
    public string GuardianId { get; set; } = string.Empty;

    /// <summary>The supervised child account (the target of every GU action).</summary>
    public string ChildId { get; set; } = string.Empty;

    /// <summary>The two-state machine (ADR 0028 §B): Active → Dissolved.</summary>
    public GuardianLinkStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Set when the row moves Active → Dissolved (the independence lane);
    /// null while Active.</summary>
    public DateTimeOffset? DissolvedAt { get; set; }

    /// <summary>The account that dissolved the row (the guardian, or a GlobalAdmin
    /// on the G·5 safety valve); null while Active. The <c>DelegationGrant.
    /// RevokedBy</c> / <c>GroupInvitation.ResolvedBy</c> precedent.</summary>
    public string? DissolvedBy { get; set; }
}

/// <summary>
/// The <see cref="GuardianLink"/> state machine (ADR 0028 §B).
/// <c>Active → Dissolved</c>; dissolve is effectively one-way in practice
/// (re-attaching is a fresh <c>Active</c> row — a deliberate new act, not an
/// undo).
/// </summary>
public enum GuardianLinkStatus
{
    Active,
    Dissolved
}
```

- **No** doc-side `IsActive` boolean (the `DelegationGrant.IsActiveAt` "service
  is the resolver" rule — G·2).
- Registered (U02) on the **existing `M1DocTypes`** surface, a neighbor of
  `opts.Schema.For<DelegationGrant>();`:
  `opts.Schema.For<GuardianLink>().UniqueIndex(g => g.GuardianId, g => g.ChildId);`
  (the `GroupInvitation` business-key convention; the surrogate `Id` is the
  Marten identity).

### AccessVia.Guardian (exact C#)

The **9th** value, appended **after `Group`** in
`src/Kumunita.Core/Authorization/Decision.cs` (U03 authors). A value-addition,
never a renumbering (the M1 `Admin` 7th / ADR 0013 `Group` 8th append
precedent, ADR 0006-E):

```csharp
public enum AccessVia
{
    Owner,
    Audience,
    Delegation,
    Moderator,
    Report,
    BreakGlass,
    Admin,
    Group,
    /// <summary>
    /// The GU standing (ADR 0028): action-scoped to the five supervisory
    /// actions (G·3); **never** on a <c>CanAsync</c> / <c>CanSeeAsync</c>
    /// content decision (G·1). The M1 <see cref="Admin"/> / ADR 0013
    /// <see cref="Group"/> append precedent.
    /// </summary>
    Guardian
}
```

**G·1 pin (load-bearing):** `AccessVia.Guardian` appears on **no** `CanAsync` /
`CanSeeAsync` decision. A unit that adds it to the content path is a drift
pause, not a deviation (unit-series rule §5).

### IUserInfoService guardian seams (exact C#)

The **five** supervisory actions (ADR 0028 §C) — **three add new methods, two
are branches on existing lanes** — each pinned to its exact signature, the
standing gate (= active link), the audit verb + `Via`, the invariant, and the
exceptions. **No new method is added for the two branch actions; the
signatures below for them are unchanged.**

**1. Formation — `CreateGuardianLinkAsync`** (new method, U04). Upserts the
`(GuardianId, ChildId)` `Active` row (idempotent no-op on re-attach). Audit
`guardian.create`, `Via: Guardian`, target the child account. G·4 (creation
basis). The Web's add-a-child form pairs it with
`IIdentityService.RegisterAsync` so account + link + audit commit together (C3)
— a created-but-unlinked account can never exist.

```csharp
Task<GuardianLink> CreateGuardianLinkAsync(string childId, string guardianId);
```

**2. Suspend / un-suspend — `SuspendChildAsync` / `UnsuspendChildAsync`** (new
methods, U04). Verify the active link (else `UnauthorizedAccessException` → the
Web's 404). Set `Profile.Blocked` true/false — the **same flag**
`BlockedAccountMiddleware` + the directory already read, so enforcement is
identical. Audit `guardian.suspend` / `guardian.unsuspend`, `Via: Guardian`,
target the child account. G·2 (live on the next read). The GlobalAdmin
`BlockAsync` / `UnblockAsync` are **unchanged**.

```csharp
Task SuspendChildAsync(string childId, string guardianId);
Task UnsuspendChildAsync(string childId, string guardianId);
```

**3. Membership curation — a branch on the existing four lanes** (U05). The
existing `AddCommunityMemberAsync(componentId, userId, actorId, actorRoles)` /
`RemoveCommunityMemberAsync(…)` (ADR 0012) **and**
`AddGroupMemberAsync(groupId, userId, addedBy)` /
`RemoveGroupMemberAsync(groupId, userId, removedBy)` **grow a `Via: Guardian`
branch**: when the actor has an **active link over the target `userId`** (the
child), the standing gate is satisfied and the audit row records the
**narrower** standing `Via: Guardian` (the ADR 0012 "record the narrower
standing" rule). **No new method; a branch. The four signatures are unchanged.**
The ADR 0012 mandatory-community `InvalidOperationException` and the ADR 0008
group-owner-row exception are **preserved** (the branch adds a path, never
removes one).

```csharp
// unchanged signatures — a Via: Guardian branch is added to each standing gate:
Task AddCommunityMemberAsync(string componentId, string userId, string actorId, IReadOnlySet<string> actorRoles);
Task RemoveCommunityMemberAsync(string componentId, string userId, string actorId, IReadOnlySet<string> actorRoles);
Task AddGroupMemberAsync(string groupId, string userId, string addedBy);
Task RemoveGroupMemberAsync(string groupId, string userId, string removedBy);
```

**4. Invitation approval — `ApproveGroupInvitationAsync`** (new method, U06).
Resolves the child's `Pending` row as `Accepted` (the
`AcceptGroupInvitationAsync` membership write + `ResolvedAt`/`ResolvedBy =
guardianId`). Audit `group.invite.approve`, `Via: Guardian`, targetKind
"group".

```csharp
Task<GroupInvitation> ApproveGroupInvitationAsync(string groupId, string childId, string guardianId);
```

**5. Independence — `DissolveGuardianLinkAsync`** (new method, U04). Moves the
row `Active → Dissolved` (`DissolvedAt`/`DissolvedBy = actorId`). Audit
`guardian.dissolve`, `Via: Admin` when `viaAdmin` else `Via: Guardian` (G·5
safety valve). The child's memberships are **preserved**; the self-lanes
restore on the next read (G·2/C4).

```csharp
Task DissolveGuardianLinkAsync(string linkId, string actorId, bool viaAdmin);
```

**6. The invitation gate** (U06) — `AcceptGroupInvitationAsync(groupId, actorId)`
**gates**: if the invitee (the child) has an active `GuardianLink`, the
self-accept is refused (`InvalidOperationException` → the Web's error, never a
500). `DeclineGroupInvitationAsync` **stays open** (a child may always say
no). This is the C-M2b·2 self-lane's one recorded exception (the ADR 0008
owner-row-exception shape, carried to the supervised-child row).

### Pinned seam tests (exact names)

File `tests/Kumunita.Core.Tests/GuardianControlsTests.cs` (U09 authors) —
exactly these **11**, the load-bearing one first:

1. `G1_GuardianCannotReadChildContent` — **the lane's honesty**: a guardian,
   given the child's id, is **denied** a post the child authored for a
   non-guardian audience; the `CanAsync` decision carries no `Via: Guardian`
   branch.
2. `G2_SuspendIsLiveAndBlocksStanding` — suspend → the account is standing-less
   + directory-excluded (`Profile.Blocked`); un-suspend → standing restored on
   the next read.
3. `G2_DissolveRestoresSelfLanesOnNextRead` — dissolve → the child's self-accept
   lane is live on the very next attempt (C4).
4. `G3_NonChildTargetIsRefused` — a guardian acting on a *non-child* target is
   refused (`UnauthorizedAccessException`).
5. `G3_ContentReadIsNeverGuardian` — a guardian attempting a content read is
   refused; no `Via: Guardian` on the path (the G·1 unit-level twin).
6. `G4_FormationCommitsAccountLinkAndAuditTogether` — account + link + audit
   land in one commit; a duplicate `(guardian, child)` is an idempotent no-op.
7. `G5_GlobalAdminDissolvesAndUnSuspends` — the safety valve: GlobalAdmin
   dissolves an active link (`Via: Admin`) and un-suspends; the child's
   self-lanes restore.
8. `Invitation_GatedForSupervisedChild` — a child with an active link
   **cannot** self-accept a group invitation (refused) but **can** self-decline.
9. `Invitation_GuardianApproveLandsMembership_ViaGuardian` —
   `ApproveGroupInvitationAsync` lands the membership, audit
   `group.invite.approve`, `Via: Guardian`.
10. `Membership_AddRemoveChild_ViaGuardian` — the guardian adds/removes the
    child from a component **and** a group; the audit rows record `Via:
    Guardian`; the ADR 0012 posting/feed gate reflects it on the next read.
11. `SuspendSetsProfileBlocked_EnforcementIdentical` — the flag the existing
    `BlockedAccountMiddleware` / directory already read is the one set
    (enforcement parity).

### Acceptance gate (U10 records)

The lane's three tests (the `## Three tests (run before "ready")` shape, pinned
here so U10 records the run against these):

- **closed loop** — a parent forms a child account, suspends it, curates a
  membership, approves an invitation, then dissolves; each lands its audit row +
  effect on the next read.
- **handoff** — dissolve hands the account to the child: the child's self-lanes
  restore live, memberships preserved — the "come of age" handoff.
- **part-vs-whole** — the 11-test list is the **whole**; closed-loop + handoff
  are the **parts**; all must pass together (the child's private content is
  structurally never paid for — G·1).

### Drift-guard (frozen once written)

Frozen pins — any mismatch is a `## U<m> — Drift pause` (unit-series rules
§2/§8), never a silent change: the `GuardianLink` POCO (fields +
`GuardianLinkStatus` enum), the `AccessVia.Guardian` value + its position (9th,
after `Group`), the five seam signatures + the two membership-lane branches +
the invitation gate, the 11 pinned test names, the G·1–G·5 invariants, and the
acceptance gate. A unit that wants to change any of these pauses and records
the drift; it does not reshape the pin.

### Run result (GU acceptance gate — 2026-09-14)

- **Core (11 pinned seam tests):** all **PASS** — `G1_GuardianCannotReadChildContent` ✅ · `G2_SuspendIsLiveAndBlocksStanding` ✅ · `G2_DissolveRestoresSelfLanesOnNextRead` ✅ · `G3_NonChildTargetIsRefused` ✅ · `G3_ContentReadIsNeverGuardian` ✅ · `G4_FormationCommitsAccountLinkAndAuditTogether` ✅ · `G5_GlobalAdminDissolvesAndUnSuspends` ✅ · `Invitation_GatedForSupervisedChild` ✅ · `Invitation_GuardianApproveLandsMembership_ViaGuardian` ✅ · `Membership_AddRemoveChild_ViaGuardian` ✅ · `SuspendSetsProfileBlocked_EnforcementIdentical` ✅ (11/11, 0 errors, 0 failed).
- **Web (4 VM data-shape tests):** all **PASS** — `ChildAccountItem_Is_Exact_Three_Field_Projection` ✅ · `MembershipEditorModel_Is_Exact_Four_Field_Projection` ✅ · `PendingInvitationItem_Is_Exact_Three_Field_Projection` ✅ · `AddChildForm_Is_Form_Model_With_Three_Required_Fields` ✅ (4/4, 0 errors, 0 failed).
- **Build:** `dotnet build Kumunita.slnx -c Debug` — **green** (0 warnings, 0 errors).
- **Gate verdict:** **GREEN — GU is accepted.** Closed-loop + handoff + part-vs-whole all hold: the 11 Core seam tests (the part-vs-whole *whole*) and the 4 Web VM projection pins (the part-vs-whole *parts*) are green together; the load-bearing G·1 test confirms no `Via: Guardian` branch on the content path. No drift pause; no seam implicated.

## Feedback loops

- **Seam tests** (each cites the invariant above):
  - **G·1 (no content read):** a guardian, given the child's id, is **denied**
    an audience-restricted post the child authored for a non-guardian audience —
    the `CanAsync` decision carries no `Via: Guardian` branch (the load-bearing
    test of the whole lane).
  - **G·2 (live standing):** suspend → the account is immediately standing-less
    and excluded from the directory (the existing `Profile.Blocked` effect);
    un-suspend → standing restored on the next read; dissolve → the child's
    self-accept lane is live on the very next attempt (C4).
  - **G·3 (action-scope, deny-by-default):** a guardian on a *non-child* target
    is refused; a guardian attempting a content read is refused; the
    self-serve "claim" lane does not exist.
  - **G·4 (creation basis):** the formation lane creates account + link + audit
    in one commit; the verification email is staged (the M1 world-seam shape);
    a duplicate link for the same (guardian, child) is an idempotent no-op.
  - **G·5 (safety valve):** a GlobalAdmin dissolves an active link and the
    child's self-lanes restore; the admin un-suspends a suspended child; both
    are audited `Via: Admin`.
  - **Invitation gate:** a child with an active link **cannot** self-accept a
    group invitation (refused); **can** self-decline; the guardian's
    `ApproveGroupInvitationAsync` lands the membership audited `Via: Guardian`;
    after a dissolve the child's self-accept is restored.
  - **Membership curation:** the guardian adds/removes the child from a
    component and a group; the audit rows record `Via: Guardian`; the child's
    posting/feed gate (ADR 0012's single seam) reflects it on the next read.
- **Production signals:** the audit log (`/admin/audit`) now answers "a parent
  suspended their child, by what right, when" (`guardian.suspend`,
  `Via: Guardian`) — a new, legible row class. No new `/health` signal (no new
  side-effect surface; the verification email rides the existing
  `OutboxEmail` / dead-letter / degraded path, M1 §6.2).

## Emergent impact

- **The *Authority* chain gains a legibility row for the family case.** The
  audit log (the "prove what happened" mechanism, `SECURITY.md` §3.1) now
  records *who supervised a child's account, by what right, and when* —
  traceable forward (action → audit row) and backward (audit row → link →
  guardian), exactly the `domains-of-integration.md` chain, extended one step.
- **A protected class is named without a protected data field.** "Minor" is
  carried by the **link**, not by a stored age — so the privacy-first rule
  ("what we don't store, we can't leak") is kept even as we add the control.
- **The cardinal privacy rule is held, and *proven* held.** G·1 is a test, not
  a promise: the design's most dangerous temptation (a parent reading their
  kid's posts) is *structurally* impossible on the content path and pinned by a
  named test. That is the difference between "we decided not to" and "it cannot
  happen" — and at this scale, at this product, the latter is the only
  acceptable form.

## Local-optimization check

Optimizes the **whole** (a child who can participate safely; a parent who can
keep that child safe; the neighborhood's trust that the platform handles
families honestly), not a part. The explicit cost is the **child's account
autonomy** while the link is active — paid for the child's safety, and
capped by G·5 (the safety valve). There is no engagement number to over-fit:
the feature's measure is a child who is included *and* a parent who is not
carrying the load alone, and the whole paying nothing in the child's private
content (G·1).

## FACES check

- **Stable — strengthened:** the family case (the ADR 0010 "a family is an
  organizing unit" want) now has a *supervision* lane, not just a private-group
  one; a child's account can be suspended, curated, and handed over without a
  GlobalAdmin being in the loop for routine care — the place holds for families,
  across a child's growth (link → dissolve → independence).
- **Coherent — strengthened:** one `Via: Guardian` standing, one `GuardianLink`
  doc, one audit verb family; a GlobalAdmin reading `/admin/audit` can narrate
  "this parent supervised this child's account for that period, by creation,
  and dissolved it when the child came of age" in one breath.
- **Adaptive — strengthened:** the safety valve (G·5) is a closed response path
  — a mistreated child reaches a GlobalAdmin and the link dissolves; the audit
  row is the record that it did.
- **Flexible — consumed (the named trade):** the guardian standing is frozen
  action-scoped at five supervisory actions and *cannot* grow into a content
  read without an ADR 0028 amendment. That is slower than ad-hoc — deliberately.
  A family that later wants "my parent can see what I posted" is told that is a
  new, separate, ADR-gated decision, not a toggle.
- **Energizing — protected:** the parent is given one place to do the
  supervision (less out-of-platform nagging); the child's *participation* is
  preserved (the lane touches memberships and the account, never the child's
  voice, authorship, or private content).

## Rollout & rollback

- **Schema:** the `GuardianLink` document rides the existing `M1DocTypes`
  surface (ADR 0004 §B.1 — additive, delta-detected by Marten, idempotent, no
  re-seed, no new `*DocTypes` file, no DDL change under Marten 9's JSONB
  `data` column — the ADR 0010 `IsPrivate` upgrade-path precedent). The
  `AccessVia.Guardian` value is an append (no stored row's meaning changes).
  Both are **additive** → re-deploying an older image over a forward-migrated
  database is safe (no `GuardianLink` rows exist until the lane ships; no audit
  row ever carried `Via: Guardian` before this).
- **`/health`** is unchanged (no new side-effect surface; the verification email
  reuses M1's durable outbox).
- **Rollback:** dissolve all links (a support action) to return every affected
  child to a normal account; the schema is additive, so the older image remains
  deployable. Restore: `pg_dump` of the single Postgres captures the
  `GuardianLink` rows with the rest (OPS.md).
- **The trio moves together** (AGENTS.md contract): `README.md` Features +
  Roadmap gains the `GU` named lane; `Milestones.cs` gains the `GU` entry
  (`StatusNext`, with `M4` moving to `StatusPlanned` — the single-in-progress
  invariant preserved); `MilestonesTests.cs` is updated in the same commit so
  the build catches a forgotten sync.

## Risks

- **A hostile or over-reaching guardian is a new adversary (A7).** Mitigated by
  G·4 (standing comes from *creating* the account — no self-serve claim over a
  stranger), G·1 (no content read, so the exposure is account-level, not the
  child's private life), and G·5 (the safety valve: a GlobalAdmin dissolves the
  link or un-suspends the account, audited). Recorded in `SECURITY.md` as a
  named adversary + control row, not assumed away.
- **The suspend lane could be used to silence a child.** The same control that
  keeps a child safe can lock a child out; G·5 is the counterweight, and the
  `guardian.suspend` audit row (always-on, `Via: Guardian`) is the record a
  GlobalAdmin reads when a child (or another adult) reports it.
- **G·1 is the whole lane's honesty.** If the content path ever gains a
  `Via: Guardian` branch, the lane silently inverts the product's cardinal
  rule. Pinned by the G·1 seam test and by the ADR 0028 "deliberate
  not-available" record (a re-litigation requires an amendment).
- **Formation + link in two Core calls.** The account (`IIdentityService`) and
  the link (`IUserInfoService`) are different modules; the Web must commit them
  together (C3) so a created-but-unlinked account (or a link with no account)
  can never exist. The design pins this as a single Web transaction; the seam
  test asserts both land or neither does.

## Integration step served

Moves **coordination → outcome** for the family case: the value chain
(`the-platform-as-integrator.md`) already links a *signal* to its audience; this
lane links a **child** to a **safe, supervised participation** — a parent takes
"I have a child who should be part of the neighborhood, but I need to keep them
safe and in check" in and gets a working, supervised, eventually-independent
account out, without leaving the platform. It is the *linkage* (parent ↔ child
↔ community) the platform exists to build — not a feature bolted onto the child.

## World seams

- **The one outbound seam is the verification email** (the child clicks the
  link to activate the account) — the M1 designed handoff, not a leak; the
  parent does not need the child's credentials to supervise (supervision rides
  the link, not the password).
- **No out-of-platform handoff for the controls** — suspend, curate, approve,
  hand-over are all on-platform; a parent does not re-key a child's password
  into a phone to "lock it out." The independence transition is a single on-platform
  act, not a re-signup.
- **The safety valve is the boundary to the community's trust** — when the
  guardian lane fails a child, the seam is a *human* one (the child reaches a
  GlobalAdmin), which is the correct and honest exit for a product that stands
  behind its youngest residents.

## Three tests (run before "ready")

- **Closed-loop?** A parent takes the problem in (a child who needs a safe,
  supervised place in the community) and gets the outcome out (a working
  account the parent can lock, curate, and eventually hand over) — entirely
  on-platform. **Yes.** The only exit is the one designed email handoff, and
  the safety-valve exit is a human handoff the product owns.
- **Handoff?** Every manual transfer is accounted for: the verification email is
  a designed seam; supervision and hand-over are on-platform; there is no
  "re-key the password into a phone" step. **No un-integrated seam.**
- **Part-vs-whole?** The part optimized is *parent convenience* and *child
  safety*; the whole (the child's private content, the neighborhood's trust in
  the platform) is **not** paid for — it is *structurally protected* by G·1
  (the content path has no guardian branch) and capped by G·5 (the safety
  valve). The only cost is the child's temporary account autonomy, which is the
  deliberate, named, bounded trade. **Whole not traded for a part.**

## GU — Closed (recorded) (2026-09-14)

The GU lane (guardian controls, ADR 0028) is **shipped**. The five actions
(suspend/unsuspend, community + group membership curation, invitation
approval) + formation (add-a-child, the usual verify-email flow) + the
independence handover (dissolve, the GlobalAdmin safety valve) are live.
Standing is **account-scope only** — invariant **G·1** held: the guardian
standing (`AccessVia.Guardian`, the 9th value) **never** resolves a content
read; it is exercised only on the `IUserInfoService` management lanes.

- **Decision record:** ADR 0028 (account-scope supervision; amends 0006,
  0003, 0012, m2b).
- **Gate:** the 11 pinned seam tests + the 4 Web VM tests — see the
  `### Run result (GU acceptance gate — 2026-09-14)` section above (U10).
- **Layout:** `GuardianLink` rides the existing `M1DocTypes` surface (ADR
  0004 §B.1, additive); `docs/ARCHITECTURE.md`'s `UserInfo/` line + doc-map
  carry the surface (U11).
- **Non-decision (carried forward):** the GlobalAdmin `viaAdmin: true`
  dissolve shell (the admin surface) is **out of scope** here — it is a
  separate admin-shell lane, not a GU one (ADR 0028 §C, the §D G·5 valve).
- **M4/M5/M6 untouched** (Events / Projects / Portability — the named-lane
  discipline: GU is not a renumber).
