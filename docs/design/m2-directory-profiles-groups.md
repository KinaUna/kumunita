# Design Doc — M2: Directory, profiles, groups

> Part 1 of 2 (U1). Part 2 (U2) will append "Seams & contracts" and the test list.
> Part 1 is scope, invariants, and FACES. Both parts pin the invariant numbers
> that every M2 unit (U3–U14) must match.

## Context

M1 shipped the **access model** — identity, groups, delegation,
`Any`/`All` audience evaluation with the empty-audience guard, effective
principal through delegation, always-on `AccessAudit` (single + bulk), and the
Wolverine side effects (`OutboxEmail`, `AuditPurge`). M1 *stored* the M2
surfaces: `Profile.Visibility: Audience?`, `Profile.ContactVisibility:
Audience?`, `Group` / `GroupMembership`, `DelegationGrant`. M1 shipped no
resident-facing surface that reads them: `how-it-works.md`'s first promise —
*"A directory of residents. Each person decides what they share"* — is still
prose, not product.

M2 delivers three surfaces:

- **Directory** (`/directory`): a listing of residents the viewer may see; a
  detail page with profile + optional contact block.
- **Profile editor** (`/profile/edit`): the write surface for
  `Visibility` (who sees my profile) and `ContactVisibility` (who sees my
  email/phone), plus a read-only "view-as" preview.
- **Group management** (`/groups`): list, create, per-group add/remove
  member — so an audience can point at a named group, not only individuals.

Per `ARCHITECTURE.md` §4.3, **M2 introduces no new authorization rule.** Every
decision routes through the frozen `IAuthorizationService` (ADR 0006). M2 adds
exactly one new read method on `IUserInfoService`
(`GetProfilesAsync(verifiedOnly)`, a *new* method on a frozen interface — the
compatible lane per ADR 0006-E, precedent the `IDocumentSession` overloads on
`IAuthorizationService`) and one new adapter (`Profile` → `IAuditableResource`,
`TargetKind = "directory"`, `Audience = Visibility`).

## Scope

**In scope**

- `DirectoryService` (Kumunita.Core) — composes `IUserInfoService` +
  `IAuthorizationService` for: directory listing, profile detail, contact-block
  evaluation, "view-as" preview. Never reads `GroupMembership` or
  `DelegationGrant` for its own access decisions.
- `Profile` → `IAuditableResource` adapter (single `Id = SubjectId`,
  `Audience = Visibility`, `TargetKind = "directory"`).
- `IUserInfoService.GetProfilesAsync(bool verifiedOnly)` — the one new read
  method, returning `IReadOnlyList<Profile>` documents; used only as the
  **candidate** set for `CanSeeAsync`, never as a visible set.
- `DirectoryController`: `/directory` (list) + `/directory/{subjectId}`
  (detail, incl. contact block).
- `ProfileController`: `/profile/edit` (name + `Visibility` editor +
  `ContactVisibility` opt-in + read-only "view as [resident]" preview).
  M1's `AccountController.Profile` actions migrate here (redirect stubs
  preserved in U14).
- `GroupsController`: `/groups` (list), `/groups/new` (create),
  `/groups/{id}` (view + add/remove member).
- View models + Razor views under
  `src/Kumunita.Web/Views/{Directory,Profile,Groups}/`.
- Seam tests (`tests/Kumunita.Core.Tests/DirectoryServiceTests.cs`) and e2e
  Playwright specs (`tests/Kumunita.Web.Tests/`) — test *names* are pinned
  by Part 2 (U2); this part pins the invariants they must anchor to.

**Out of scope (close M1's OOS line here)**

- M1's "out of scope: **profile editing UI and directory visibility rules
  (M2)**" — **closed by M2**. This section is M2's explicit close.
- Report-driven moderator unlock on a *profile* (M3 — the `moderatorAccess`
  mechanism is M1, the trigger on a `Profile` is M3+).
- Directory-level group browsing (UI for "groups I'm in" at the directory
  level) — M2 is group-per-owner; directory listing of *groups* is M3 if
  ever.
- "You now appear in the directory" email / notifications — **no new
  Wolverine side effect in M2**; the only outbound channel is the one M1
  opened (verification email); M6 owns notification surfaces.
- Export, iCal, federation, MCP/API.

**Surfaces (resident-visible)**

1. Directory listing — verified residents the viewer may see (their names /
   display names); hidden count in an aggregate row; no hidden-field
   rendering.
2. Directory detail — one resident; if the profile's `Visibility` allows, the
   profile fields appear; if `ContactVisibility` also allows, the email /
   phone block appears.
3. Profile editor — my own `Visibility` and `ContactVisibility`, both
   audited writes through M1's `UpsertProfileAsync`.
4. View-as preview — read-only; evaluates my saved audience as if a chosen
   resident (or I) were the viewer.
5. Group list / create / member add-remove — per owner.

## Invariants (pinned for M2)

M2 is a *caller* of the ADR 0006 invariants, not an owner. The following M1
invariants **must keep holding for the M2 surfaces**, and the seam tests for
M2 (pinned by Part 2) will name each.

| # | ADR 0006 invariant | How M2 uses it |
|---|---|---|
| **C1** | Empty audience denies (both `Any` and `All`; explicit guard) | `Profile.Visibility = null` ⇒ profile hidden from everyone (owner branch is the only exception, see §Contact gating). `ContactVisibility` `null` ⇒ contact block hidden. Explicit `Any` + empty grant ⇒ `CanSeeAsync` returns deny. |
| **C3** | Audit always on — Allow *and* Deny; one aggregate row per bulk; rows commit in the same transaction | Directory listing = one aggregate `AccessAudit` row (single + bulk, `targetKind = "directory"`, `visibleCount` / `hiddenCount`). Detail view (incl. contact block) = one `AccessAudit` row per profile decision. Profile/group writes ride on M1's `UpsertProfileAsync` / group-mutation transactions. No new audit shape. |
| **C4** | Group visibility is strong-consistency (live documents, no projection in the access path) | The directory reflects a membership change on the *very next* request (U10 e2e pin). Group add/remove member is a live change, not a projection update. |
| **C6** | One matching pass (`MatchGroups`) shared by `CanAsync` and `CanSeeAsync` | The contact block and the profile both reduce to the same `MatchGroups` call; the *two* decisions (`Visibility` first, then `ContactVisibility`) cannot drift (e.g., a profile visible with `All` + non-empty contact `Any` + non-empty). |

**ADRs M2 must respect (not pin as invariants, but the code must not
violate):**

- **ADR 0001-B** — audience is user-defined; the *author's* choice is
  absolute. M2's editor writes the author's choice verbatim; M2 does not
  second-guess, does not auto-add group grants.
- **ADR 0003 §Separation of duties** — group list/create/membership is the
  *group owner's* surface; only GlobalAdmin can demote/assign roles, no
  moderator scope is involved in M2's group surface, and a moderator
  **cannot** manage groups. A GroupOwner may add/remove only members *of
  their own group*. M2 does not touch `/admin` (M1's admin surface is
  unchanged).
- **ADR 0006-D** (dependency direction) — `DirectoryService` (Core) depends
  only on `IUserInfoService` + `IAuthorizationService`; the Web controllers
  (Web) depend on `DirectoryService`. No Web → Marten path.
- **ADR 0006-E** (change lanes) — the *one* new method on `IUserInfoService`
  is a **compatible** addition (predecessor the `IDocumentSession`
  overloads). No breaking change to an existing signature.

**M2 invariants (new, M2-owned, pinned for U3–U14)**

- **C-M2·1 — Contact-gating (§9, ARCHITECTURE.md / M1 design doc).**
  `ContactVisibility` is evaluated *only after* `Visibility` has allowed the
  profile. A viewer not allowed by `Visibility` never reaches the contact
  check — no row, no render of contact fields. This is a *composition* rule
  over ADR 0006-C1/C6; the pin is the **ordering**, not a new deny guard.
- **C-M2·2 — Candidate filter vs. authorization separation (ARCHITECTURE.md
  §4.3).** The *product query* of the directory ("verified residents;
  unverified viewer sees only themselves; unauthenticated viewer sees no
  one") is a **filter the `DirectoryService` applies to the candidate set
  before invoking `CanSeeAsync`**, not an access rule owned by the
  `AuthorizationModule`. It must never appear as an `AccessAction`, and
  must not be logged as an access decision. If it leaks into
  `IAuthorizationService` it will be mis-audited and mis-attributed to the
  audience.
- **C-M2·3 — Group surface SoD (ADR 0003).** Add/remove member on a `Group`
  is permitted only to the group's owner or a GlobalAdmin. No other path.

## FACES (user-visible outcome → invariant pin)

Each row is a *resident-visible outcome* that the M2 seam tests (Part 2)
must cover. A row is *pinned* when the invariant in the right column is the
single authority for the outcome; the test names Part 2 will name must
reference that pin.

| # | Outcome (what a resident sees / can do) | Pinned by |
|---|---|---|
| F1 | Directory listing shows **only** the residents I may see; the others do not render (names, emails, no partial fields). Aggregate row shows hidden count. | C1 (empty / out-of-audience denies) + C3 (one aggregate row) |
| F2 | Directory listing reflects a membership change on my group the moment it happens — no refresh, no delay, no stale cache. | C4 |
| F3 | Detail page: my contact block (email/phone) appears **only** when my `Visibility` allows the profile *and* my `ContactVisibility` allows the contact. | C-M2·1 + C1 |
| F4 | Detail page: a viewer who is not allowed by `Visibility` never reaches the contact check (no email / phone rendered, no "hidden" placeholder). | C-M2·1 (ordering) |
| F5 | Profile editor: `Visibility` and `ContactVisibility` each accept explicit `Any` / `All` with named users + groups; the author's choice is stored verbatim (ADR 0001-B). | ADR 0001-B + C1 (empty ⇒ deny) |
| F6 | Profile editor: "View as [resident]" is a read-only preview of the same saved audience; it does not change state and is not a write path. | C-M2·1 (composition over the same audience) + ADR 0006-D (no new surface) |
| F7 | Group owner can create a group, add a member, remove a member; each action is live on the next directory render (F2) and audited (C3). A non-owner cannot manage the group. | C-M2·3 (SoD) + C3 + C4 |
| F8 | Unverified viewer: sees **only themselves** in the directory (C-M2·2 candidate filter — *not* an access decision; no `AccessAudit` row for the filter itself). | C-M2·2 |
| F9 | Delegation: a delegate acting for me inherits my standing on my profile's `Visibility` / `ContactVisibility` only for actions inside the grant's scope; out-of-scope is Deny with `Via = Delegation`. | ADR 0006-C2 (delegation action-scoped) — carried through the `Via` path from M1 |
| F10 | Every directory *list* decision, every *detail* decision (profile *and* contact block), and every group mutation (create / add / remove) commits an `AccessAudit` row in the same transaction as the domain write; Allow and Deny are both recorded. | C3 |
| F11 | Bulk directory ≡ per-item directory: the aggregate `visibleCount` / `hiddenCount` equals the per-resident `CanSeeAsync` result computed one at a time over the same candidate set. | C6 (one matching pass) |
| F12 | Moderator (any scope) cannot read a profile I have not shared with them, cannot manage a group I do not own, and cannot use the profile editor to peek at my `ContactVisibility`. Default OFF (ADR 0003) holds in M2 as it held in M1. | ADR 0003 + ADR 0006-C5 (carried through M1) |
| F13 | The profile editor is the *single write surface* for `Visibility` / `ContactVisibility`; no other route in M2 mutates those fields. | ADR 0001-B + ADR 0006-D (single seam) |
| F14 | Groups surface is owner-scoped: my group list shows only groups I own (plus, if I'm a member, groups I belong to — read-only membership list is optional per scope note). Directory-level "groups I'm in" is **not** in M2. | C-M2·3 (SoD) + scope note above |
| F15 | The *one* new `IUserInfoService` method (`GetProfilesAsync(verifiedOnly)`) is used *only* to produce the candidate set for F1 / F2 / F8 / F11 — never as a direct visible set. Its output never renders without passing F1's `CanSeeAsync` gate. | C-M2·2 + C1 + C6 |

**FACES count: 15.** This count (and the invariant-pin per row) is the input
the next unit (U2) needs to name the seam-test list and the acceptance gate
without re-deriving them.

## Drift-guard & change policy (Part 1)

- If a later unit (U3–U14) finds a mismatch between an implemented signature
  and the pin in this Part, **this doc wins**. The unit updates this file in
  the same commit and appends a one-line drift note to the handoff.
- The invariant *numbers* (C1…C6 from ADR 0006; C-M2·1..3) are stable for the
  rest of M2. Adding a new invariant requires an ADR amendment; renaming or
  renumbering an existing one is a breaking change and is not allowed mid-M2.
- A new FACES row (F16+) is added only by a unit that ships the outcome it
  pins, in the same commit as the feature. The FACES count is a **handoff
  field** (U1 → U2, and forward): every unit that touches FACES updates the
  count in the handoff note.

## Seams & contracts (Part 2, written by U2)

### 2.0 Preambles — what this section pins, and what wins on conflict

Every C# fragment below is **exact**: parameter lists, return types, and
namespaces are the contract U3–U14 must implement against. If a later
unit (U3–U14) discovers an implemented signature or a required M1 seam
that does not exist verbatim here, the drift-guard (§2.7) applies:
**this file wins**; the unit updates this file in the same commit and
appends a one-line drift note to `docs/plans-milestones/m2-handoff-notes.md`.

Namespace conventions (matching M1):

- Frozen / new-Core seams: `Kumunita.Core.UserInfo` (module-owned) or
  `Kumunita.Core.Authorization` (M1 surface, unchanged by M2 except
  where noted).
- Web-side composition: `Kumunita.Web.Controllers` /
  `Kumunita.Web.Models` (never in `Kumunita.Core`).

The **one** frozen-interface addition M2 makes (ADR 0006-E compatible
lane; precedent: the `IDocumentSession` overloads on
`IAuthorizationService` in M1): `IUserInfoService.GetProfilesAsync(bool)`.
M2 introduces **one** new type on the frozen surface: the
`IAuditableResource` adapter for `Profile` (a *new* type — not a change
to `Profile`). Plus, contingent on a verification in U9 (§2.2):
`IUserInfoService.GetGroupsForUserAsync(string)`, *flagged*, not
assumed.

### 2.1 Frozen seam list (exact C#)

Seams that exist as of M1. M2 *calls* them; M2 does not modify.

`Kumunita.Core.Authorization.IAuthorizationService` (frozen, ADR 0006 §A):

```csharp
public interface IAuthorizationService
{
    Task<Decision>  CanAsync(string actorId, AccessAction action,
                             IAuditableResource target);
    Task<Decision>  CanAsync(string actorId, AccessAction action,
                             IAuditableResource target,
                             Marten.IDocumentSession session);
    Task<VisibleSet> CanSeeAsync(string actorId, AccessAction action,
                                 IEnumerable<IAuditableResource> candidates);
    Task<VisibleSet> CanSeeAsync(string actorId, AccessAction action,
                                 IEnumerable<IAuditableResource> candidates,
                                 Marten.IDocumentSession session);
}
```

`Kumunita.Core.Authorization` frozen types (as they stand in M1):

```csharp
public enum AudienceMode { Any, All }
public enum GrantKind    { User, Group }
public sealed record AudienceGrant(GrantKind Kind, string Id);

public sealed class Audience
{
    public AudienceMode Mode { get; set; }
    public System.Collections.Generic.List<AudienceGrant> Grants { get; set; }
    public bool IsEmpty => Grants.Count == 0;
}

public enum AccessVia     { Owner, Audience, Delegation, Moderator, Report, BreakGlass, Admin }
public enum AccessOutcome { Allow, Deny }

public sealed record Decision(bool Allowed, AccessVia Via, string EffectivePrincipalId);

public sealed record VisibleSet(
    System.Collections.Generic.IReadOnlyList<(string Id, AccessVia Via)> Visible,
    int HiddenCount);

public sealed record AccessAction(string Id)
{
    public static readonly AccessAction Read     = new("read");
    public static readonly AccessAction Moderate = new("moderate");
}

public interface IAuditableResource
{
    string    Id          { get; }
    string    Name        { get; }
    string?   OwnerId     { get; }
    Audience? Audience    { get; }    // `null` = public
    string?   ComponentId { get; }    // `Profile` maps to `null`
    string    TargetKind  { get; }    // `Profile` maps to "directory"
}

public sealed class AccessAudit
{
    public string          Id       { get; set; }
    public DateTimeOffset  At       { get; set; }
    public string          ActorId  { get; set; }
    public string?         EffectivePrincipalId { get; set; }
    public string          Action   { get; set; }
    public string          TargetKind { get; set; }
    public string?         TargetId   { get; set; }        // single-target rows
    public int?            VisibleCount { get; set; }      // aggregate rows only
    public int?            HiddenCount  { get; set; }      // aggregate rows only
    public AccessVia       Via      { get; set; }
    public AccessOutcome   Outcome  { get; set; }
}
```

`Kumunita.Core.UserInfo.IUserInfoService` (frozen; `// M2 ADD` is the one
new method, landed in U3):

```csharp
public interface IUserInfoService
{
    Task<Profile?>     GetProfileAsync(string subjectId);
    Task<HashSet<string>> GetGroupIdsAsync(string userId);
    Task<DelegationGrant?> GetActiveGrantAsync(string delegateId);

    Task<Group>        CreateGroupAsync(string ownerId, string name, string? description);
    Task               AddGroupMemberAsync(string groupId, string userId, string addedBy);
    Task               RemoveGroupMemberAsync(string groupId, string userId, string removedBy);

    Task<DelegationGrant> GrantDelegationAsync(string ownerId, string delegateId,
                              IReadOnlyList<string> scope,
                              DateTimeOffset from, DateTimeOffset? to);
    Task               RevokeDelegationAsync(string grantId, string revokedBy);

    Task               UpsertProfileAsync(Profile profile, ProfileUpdate patch);
    Task<IReadOnlyList<Component>> SeedComponentsAsync();
    Task               SetComponentModeratorAccessAsync(string componentId, bool on, string actorId);
    Task<IReadOnlyList<ModeratorAssignment>> GetAssignmentsAsync(string userId);

    // M2 ADD — the *one* new method on a frozen interface (ADR 0006-E compatible
    // lane; precedent: the `IDocumentSession` overloads on `IAuthorizationService`,
    // M1). Returns the *candidate* set for the directory, NOT a visible set; the
    // caller must pass every element through `CanAsync` / `CanSeeAsync` before
    // rendering (F15 pin). Produces no audit row itself (C-M2·2).
    Task<IReadOnlyList<Profile>> GetProfilesAsync(bool verifiedOnly);
}
```

`Kumunita.Core.UserInfo.Profile` (frozen; M2 reads, never re-shapes):

```csharp
public sealed class Profile
{
    public string   SubjectId       { get; set; }   // doc id (= actor subject for self-sight)
    public string?  ExternalId      { get; set; }
    public string?  HouseholdId     { get; set; }   // display-only; the authz path never reads it
    public string   DisplayName     { get; set; }
    public bool     Verified        { get; set; }
    public Audience       Visibility        { get; set; }   // non-null; bootstrap default = empty (C1 ⇒ self-only)
    public Audience?    ContactVisibility   { get; set; }   // null ⇒ contact block hidden (C-M2·1)
    public string?      Email                 { get; set; }
    public string?      Phone                 { get; set; }
}

public sealed record ProfileUpdate(
    string?    DisplayName,
    string?    Email,
    string?    Phone,
    Audience?  Visibility,
    Audience?  ContactVisibility);
```

`Kumunita.Core.UserInfo.Group` / `GroupMembership` (frozen; M2 reads `Group`
for rendering; reads membership *only* through the frozen
`CreateGroupAsync` / `AddGroupMemberAsync` / `RemoveGroupMemberAsync`
seams — never directly on the access path, per ADR 0006-D):

```csharp
public sealed class Group
{
    public string          Id          { get; set; }
    public string          Name        { get; set; }
    public string?         Description { get; set; }
    public string          OwnerId     { get; set; }
    public DateTimeOffset  Created     { get; set; }
}

public sealed class GroupMembership
{
    public string          Id        { get; set; }
    public string          GroupId   { get; set; }
    public string          UserId    { get; set; }
    public string          AddedBy   { get; set; }
    public DateTimeOffset  At        { get; set; }
}
```

### 2.2 New Core types (M2-owned)

**`ProfileToAuditableResource`** (namespace `Kumunita.Core.UserInfo`;
the one adapter, F15 pin). A single instance per `Profile` is safe to
pass into either `IAuthorizationService` overload. It does not *own*
the profile: the same `Profile` may be wrapped by multiple adapters in
the same request (once for the bulk `CanSeeAsync`, once per `CanAsync`
in the "view-as" preview).

```csharp
namespace Kumunita.Core.UserInfo;

/// <summary>
/// Adapter (Part 2 pin): presents a <see cref="Profile"/> to the frozen
/// <see cref="Kumunita.Core.Authorization.IAuthorizationService"/>.
/// Id = SubjectId; OwnerId = SubjectId (owner branch); Audience =
/// Visibility (ContactVisibility is the *caller's* second decision,
/// §2.4); ComponentId = null; TargetKind = "directory" (matches the
/// aggregate-row shape in <see cref="Kumunita.Core.Authorization.AccessAudit"/>).
/// </summary>
public sealed class ProfileToAuditableResource : Kumunita.Core.Authorization.IAuditableResource
{
    public ProfileToAuditableResource(Profile profile) => Profile = profile;
    public Profile Profile { get; }

    public string    Id          => Profile.SubjectId;
    public string    Name        => Profile.DisplayName;
    public string?   OwnerId     => Profile.SubjectId;
    public Audience? Audience    => Profile.Visibility;
    public string?   ComponentId => null;
    public string    TargetKind  => "directory";
}
```

**`DirectoryService`** (namespace `Kumunita.Core.UserInfo`; pure caller
of the two frozen modules — never reads `GroupMembership` /
`DelegationGrant` directly):

```csharp
namespace Kumunita.Core.UserInfo;

/// <summary>
/// Directory-side composition (M2). Caller of the two frozen modules:
/// <see cref="IUserInfoService"/> (candidate set + single-row read) and
/// <see cref="Kumunita.Core.Authorization.IAuthorizationService"/>
/// (the single decision path, ADR 0006-D). Applies the §4.3 candidate
/// filter *before* calling `CanSeeAsync` — invariant C-M2·2 (candidate
/// ≠ access decision); the §2.4 two-gate (Visibility → ContactVisibility)
/// ordering — invariant C-M2·1 — is the same two methods, same shared
/// matching pass (C6), in order, as two separate decisions.
/// </summary>
public sealed class DirectoryService
{
    public DirectoryService(IUserInfoService userInfo,
                            Kumunita.Core.Authorization.IAuthorizationService authz);

    /// <summary>The §2.3 candidate filter applied *here* (C-M2·2 names this
    /// service as the filter's owner — see §2.2's Note, revised U5): the
    /// caller-state table maps onto the two arguments, then one
    /// `CanSeeAsync` over the survivor set. `HiddenCount` counts only
    /// candidates `CanSeeAsync` actually evaluated — a profile dropped by the
    /// filter is not "hidden" here, it was excluded before any decision ran.</summary>
    Task<DirectoryList>   ListAsync(string viewerSubjectId, bool viewerVerified);

    /// <summary>F3/F4: Visibility first; ContactVisibility *only if* Visibility
    /// allowed (C-M2·1). Missing target profile is fail-closed (no decision,
    /// no audit row). `Profile` is the source `<see cref="Profile"/>` document
    /// (null only in the missing-profile case).</summary>
    Task<DirectoryDetail> DetailAsync(string viewerSubjectId, string targetSubjectId);

    /// <summary>F6: same two gates as `DetailAsync`, with `asSubjectId` standing
    /// in as the viewer over `authorSubjectId`'s saved profile. Read-only —
    /// no write path (M2's scope pin); the two decisions still commit their own
    /// `<see cref="Kumunita.Core.Authorization.AccessAudit"/>` rows (C3).</summary>
    Task<PreviewRow>      PreviewAsAsync(string authorSubjectId, string asSubjectId);
    // The three public methods above are the *only* public method surface; the
    // three records below are the *only* public return-type surface — both
    // shipped U5 (the defining unit), frozen going forward like the rest of §2.2.
}

/// <summary>`ListAsync`'s result — the §2.3-filtered, §"Profile enumeration vs
/// privacy" risk-line-safe projection of the `CanSeeAsync` visible set.</summary>
public sealed record DirectoryList(IReadOnlyList<Profile> Visible, int HiddenCount);

/// <summary>`DetailAsync`'s result — the two-gate (Visibility, then ContactVisibility)
/// shape, plus the source `<see cref="Profile"/>` document (nullable: the missing-
/// profile fail-closed case).</summary>
public sealed record DirectoryDetail(bool IsVisible, bool ShowContactBlock, Profile? Profile);

/// <summary>`PreviewAsAsync`'s result — the same two-gate shape as
/// `<see cref="DirectoryDetail"/>`, applied to the *author's* saved profile with a
/// chosen resident standing in as the viewer (F6).</summary>
public sealed record PreviewRow(bool IsVisible, bool ShowContactBlock, Profile? Profile);
```

> *Note to U5 (revised U5 — U5 is the defining unit, so this note ships in the same
> commit as the code, per the Drift-guard's own "the unit updates this file in the same
> commit and appends a one-line drift note to the handoff" policy):* the
> `ListAsync` / `DetailAsync` / `PreviewAsAsync` names and the
> `(IUserInfoService, IAuthorizationService)` ctor were already pinned in U2's §2.2;
> U5 confirms and ships them **unchanged** as the method names, and adds the **return
> type** (the 3 records above — the plan's own U5 spec names the shapes
> `(Visible, HiddenCount)` / `(IsVisible, ShowContactBlock, Profile?)` and the §2.3
> "unverified viewer sees only themselves" rule is *only* testable — U5's own
> self-check test, U6's `F8_UnverifiedViewer_SeesOnlyThemselves_NoAccessDecisionAuditRow`,
> and F8 all require it — as `ListAsync` *itself* applying the §2.3 table, given the
> `(viewerSubjectId, viewerVerified)` arguments it already receives. The plan-U5 spec,
> C-M2·2 (the Part 1 Invariants block names `DirectoryService` as the filter's owner —
> not the Web controller), and the §2.3 table's `viewerVerified`-dependent rows all
> agree on this; the older inline `#`-comment block + the pre-revision "Note to U5"
> (Web controller applies §2.3) are the stale half. **Frozen surface, post-revision:**
> the 3 named methods + their 3 records + the ctor. This is the *same* drift-guard lane
> §2.7 already uses for `ProfileToAuditableResource` — "frozen once U4 lands
> [`Id`, `Name`, `OwnerId`, `Audience`, `ComponentId`, `TargetKind`]" — with U5 as the
> defining unit: U2 froze the 3 *names* + ctor (already inside its stated 12-seam
> count, `DirectoryService` counting as one entry, ctor + 3 methods — U2's handoff
> note says "and the `DirectoryService` ctor + 3 public methods"); U5 lands the 3
> records as that same entry's *return-type shape*, exactly as U4 landed the 6
> member shape on the same already-frozen *name* `ProfileToAuditableResource`. One
> line U6
> should read before writing §2.4's "separate call" tests: §2.4's prose says
> "a distinct `CanSeeAsync` call" for the second (contact) decision, but U5
> deliberately uses the single-resource form — `IAuthorizationService.CanAsync` —
> for *both* `DetailAsync`'s decisions, since each decision is one resource
> (not a list), the standalone overloads commit their own audit row (correct
> C3 lane — no in-flight caller transaction here), and both `CanAsync` and
> `CanSeeAsync` reduce to the *exact same* pure `Decide` call inside
> `AuthorizationService` (the C6 shared-pass property holds structurally, since
> `Decide` is the one matching core and `EvaluateAudience` the one matcher — the
> §2.4/C6 "separate call, shared matching core" pin is honored; the only
> difference is the audit row *shape*, and `CanAsync`'s single-row shape is
> exactly the more precise lane for a one-resource decision — the same reason
> `I`/`AuthorizationService.CanAsync`'s own `/// <summary>` names it "detail
> views"). U6's tests should assert the decision's `Via`/`Outcome` and the audit
> lane's row *existence* / `TargetKind` / `TargetId`, not the overload's *name*
> (which is a Core-internal implementation detail, invisible to the test's
> `store.QuerySession()` read of `AccessAudit`). `DetailAsync`/`PreviewAsAsync`
> deliberately **share** one private two-gate path (C6's no-drift property, applied
> to this service's own two public methods — they cannot disagree on the ordering or
> shape of the two decisions, since they are the same calls in the same order) and
> the §2.4 `null`-ContactVisibility row is a documented *short-circuit with no call
> and no audit row* (the literal "not evaluated" wording) — not a fourth method and
> not a hidden fourth decision.

**`IUserInfoService.GetGroupOwnersAsync(string userId)` — M1-seam
assumption — needs confirmation (U9/U10/U11 to verify).** F14 ("my
group list shows only groups I own plus groups I belong to") requires
*reading* `Group` documents keyed by `userId`. M1's frozen interface
exposes `GetGroupIdsAsync` (a `HashSet<string>` of ids — no `Group`
documents), but not a "groups for this user" read. For the
`GroupsController` (U9/U10) to render F14, one of the following must
hold:

1. M1 already exposes `Task<IReadOnlyList<Group>> GetGroupsForUserAsync(string userId)` —
   **not in the frozen surface as of M1, so this is not the case** —
   *or*
2. M2 opens the drift-guard in the *same* commit that U9's first Web
   consumer ships, adding the following to `IUserInfoService` in the
   ADR 0006-E compatible-lane style used for `GetProfilesAsync`:

```csharp
// ── M2 ADD (contingent on U9 finding M1 lacks this — FLAGGED, NOT ASSUMED) ──
// Returns: groups where (OwnerId == userId)  ∪  (∃ GroupMembership g
//         where g.GroupId = <group>.Id ∧ g.UserId == userId),
//          deduped, sorted by Group.Created descending.
// No audit row (a read).
Task<IReadOnlyList<Group>> GetGroupsForUserAsync(string userId);
```

> *Drift decision for U9:* **verify M1 first**. If the method is
> missing (this doc's expectation), **open the drift-guard in the same
> commit as U9's Web consumer** — a *new* method on the frozen
> interface, exactly mirroring §2.1's `GetProfilesAsync` ADD. If the
> method *is* present (drift against M1's own surface), update §2.1 in
> the same commit and log the drift note. The *GroupsController* cannot
> ship without this read.

### 2.3 The §4.3 candidate-filter rule (invariant C-M2·2)

The *directory's product query* — what "candidate set" means at the Web
layer — is fixed here. It is a **product rule**, not a `CanSeeAsync`
argument or an `AccessAudit` subject.

| Caller state | Candidate set (before `CanSeeAsync`) |
|---|---|
| Unauthenticated (no principal) | **empty** — short-circuit; return "nobody"; no `CanSeeAsync` call, no aggregate row (F8 boundary) |
| Authenticated, **verified** resident | `GetProfilesAsync(verifiedOnly: true)` |
| Authenticated, **unverified** resident | `GetProfilesAsync(verifiedOnly: false)` *filtered by `p.SubjectId == viewer.SubjectId`* — exactly one row (the viewer themselves) (F8) |
| Authenticated, verified, moderate-scope (any) | Same row as the verified row — the *moderator* branch is inside `CanSeeAsync` (M1), not in the candidate filter |
| Authenticated, but the viewer's profile row is missing (edge) | **empty** — fail closed |

The filter is *never* logged as an `AccessAudit` row. Any code path
that records an audit row for the candidate set itself is a C-M2·2
violation; the corresponding test in §2.5 fails.

The filter is a *precondition* on the *input set* to `CanSeeAsync`,
not a *check* inside it.

### 2.4 The `ContactVisibility` gating rule (invariant C-M2·1)

Given a `Profile p` and an actor `a`, **after** the `Visibility` check
has *allowed* the profile (F3/F4 gating):

| Shape of `p.ContactVisibility` | Contact block (email + phone) rendered |
|---|---|
| `null` | **No** — short-circuit; not evaluated (F3, F4) |
| Non-null, `Mode == Any`, `Grants.Count == 0` | **No** (empty-audience guard, C1 — same as `Visibility`) |
| Non-null, `Mode == Any`, `Grants.Count > 0` | **Evaluate** through `CanSeeAsync` on the contact audience alone (a *separate* call; see pin below), render iff allowed |
| Non-null, `Mode == All`, `Grants.Count == 0` | **No** (empty-audience guard, C1 — `All` + empty denies) |
| Non-null, `Mode == All`, `Grants.Count > 0` | **Evaluate** through `CanSeeAsync` on the contact audience, render iff allowed |

**Evaluation order (the *one* rule):** the contact check runs **only
after** the `Visibility` check has allowed the profile. A profile that
fails `Visibility` never reaches the contact check; the contact row is
*never* appended to `AccessAudit` for a hidden profile (F4 pin;
`ARCHITECTURE.md` §9 "contact block never on a hidden profile").

**The *separate*-call pin (C6 compliance):** each of the two decisions
(`Visibility` → profile; `ContactVisibility` → contact block) is a
*distinct* `CanSeeAsync` call on the same shared `MatchGroups` core,
over the same `[p]` candidate. They are not merged into one compound
audience — ADR 0006-C6 says the *matching pass* is shared, not the
*audience object*. Drift-guard: if U11's editor writes a combined
"profile + contact" audience in one shot, the contact-block tests in
§2.5 fail.

### 2.5 M2 seam-test list (names are pinned)

File `tests/Kumunita.Core.Tests/DirectoryServiceTests.cs` (new). Each
*name* carries the invariant / FACES row it anchors to (M1 convention).
U5 / U6 / U11 own their rows; the list below is the acceptance-gate
input for U12.

**Directory listing (F1, F2, F11)**

- `F1_Directory_OnlyAllowedRowsRendered_OtherFieldsNotLeaked`
- `F2_MembershipChange_IsLiveOnTheNextListing_C4`
- `F11_BulkMatches_PerCanSeeAsync_AggregateOverSameCandidates`

**Candidate filter (F8, C-M2·2)**

- `F8_Unauthenticated_EmptySet_NoCanSeeAsyncCall_NoAuditRow`
- `F8_UnverifiedViewer_SeesOnlyThemselves_NoAccessDecisionAuditRow`

**Detail + contact gating (F3, F4, C-M2·1)**

- `F4_VisibilityDenies_ContactCheckNeverRuns_NoContactAuditRow`
- `F3_ContactVisibility_Null_Hidden_EvenWhenVisibilityAllows`
- `F3_ContactVisibility_AnyEmpty_Denies_EvenWhenVisibilityAllows`
- `F3_ContactVisibility_AnyNonEmpty_EvaluatesThroughMatchGroups`
- `F3_ContactVisibility_AllEmpty_Denies_EvenWhenVisibilityAllows`
- `F3_ContactVisibility_AllNonEmpty_IntersectionEvaluatesThroughMatchGroups`

**Delegation on a profile (F9, ADR 0006-C2)**

- `F9_Delegate_InScope_BorrowsOwnersStanding_OnProfileVisibility`
- `F9_Delegate_OutOfScope_Denies_OnProfileVisibility_ViaDelegation`

**Group add / remove audit lane (F7, C3, C-M2·3)**

- `F7_GroupAddMember_CreatesAuditRow_InSameTransaction_C3_SoD_C_M2_3`
- `F7_GroupRemoveMember_CreatesAuditRow_InSameTransaction_C3_SoD_C_M2_3`
- `F7_NonOwnerCannotManageGroup_DeniedByService_WithAuditRow`

**Moderator default-OFF (F12, ADR 0003)**

- `F12_Moderator_CannotReadAPrivateProfile_DefaultOff_ADR0003`
- `F12_Moderator_CannotUseProfileEditorToPeekContactVisibility`

**Editor round-trip (F5, F6, F13, ADR 0001-B)**

- `F5_EditorWritesAuthorsChoice_Verbatim_Visibility_ADR0001B`
- `F6_ViewAs_PreviewIsReadOnly_NoStateChange_C_M2_1`
- `F13_SingleWriteSurface_VisibilityAndContact_UpsertProfileAsyncOnly`

**Count: 22 seam tests.**

### 2.6 Acceptance gate (U12 records)

Mirrors the M1 step 9 shape. U12 runs the full suite (Core + Web,
Testcontainers Postgres) and records a run result against these three
tests:

1. **Closed-loop:** a fresh unverified account signs up, verifies,
   signs in, visits `/directory`, sees only themselves (F8); a verified
   account with one private + one shared resident sees exactly the
   shared profile (F1); an owner's contact block appears on their own
   detail page (F3); a group the owner adds a member to reflects on
   the *next* listing (F2). Every step is on-platform.
2. **Handoff:** M1's *one* designed handoff (the verification email)
   is the **only** cross-seam in M2. The profile editor's POST →
   `UpsertProfileAsync` → `AccessAudit` row is on-platform. The
   directory's `CanSeeAsync` is a decision, not a handoff (no
   outbound).
3. **Part-vs-whole:** the full **22-test** seam list in §2.5, *all
   passing* against Postgres, is the whole's insurance — the
   invariant-anchored coverage M1's own step-9 run produced, now
   extended by the M2-anchored C-M2·1 / C-M2·2 / C-M2·3 rows.

The record is written into *this* design doc under a new heading
`### Run result (M2 acceptance gate — <date>)` in the M1 step 9 table
shape (`#` | `Test` | `Evidence (test names)` | `Result`), with
evidence citing the §2.5 names verbatim.

### 2.7 Drift-guard & change policy (Part 2)

Carried forward from Part 1. Additions:

- The **22-test** name list in §2.5 is frozen once U5 / U6 / U11 own
  them. Renaming or re-scoping a name is a drift event: the unit
  updates §2.5 in the same commit and appends a drift note.
- The **`IUserInfoService`** frozen surface (§2.1) and the
  `GetProfilesAsync` ADD, once U3 lands them, are **frozen**. The
  *contingent* ADD `GetGroupsForUserAsync` (§2.2) is resolved in the
  U9 commit that first consumes it, via the drift-guard above.
- The **`ProfileToAuditableResource`** shape (§2.2: `Id`, `Name`,
  `OwnerId`, `Audience`, `ComponentId`, `TargetKind`) is frozen once
  U4 lands it. A change to any of the six members is a drift event.
- The **§2.4 gating rule** (the four-shape table) is frozen once U6
  (detail + contact block) lands. Renaming or re-scoping the four
  shapes is a drift event.
- The **FACES count** in Part 1 (15) is the *U1 → U2* handoff field.
  U2 does *not* add a new FACES row (the seams are the same 15 rows
  F1–F15). A unit that ships a *new* outcome updates the FACES *and*
  this count in Part 1, same commit, same drift note.
