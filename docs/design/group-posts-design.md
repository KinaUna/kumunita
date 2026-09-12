# Design Doc — Group posts (membership-scoped group channel)

> Part 1 of 2 (U1). Part 2 (U2) will append "Seams & contracts (Part 2,
> written by U2)" with the **exact C#** U4–U9 must match, the **19 pinned
> seam-test names**, the **three-test acceptance gate**, and the drift-guard —
> mirroring [`m3-posts-design.md`](m3-posts-design.md) §2 (§2.1 frozen seam
> list, §2.2 new/changed Core types, §2.3 lane-exclusivity + reply-inherits
> rules, §2.4 gate, §2.5 test names, §2.6 module-boundary notes, §2.7
> drift-guard). Both parts pin the invariant (**G·1–G·8**) and FACES (**G1–G13**)
> numbers that every group-posts unit (U2–U12) must match.

## Context

M2 shipped groups as a **membership / privacy / organizing unit**: membership and
ownership, invitations, the description lane, and ADR 0010's `IsPrivate` flag (a
*back-office* organizing unit — a family, a circle — hidden from the audience /
grant pickers). A group is a **list of users**, resolvable on the live
membership read (`IUserInfoService.GetGroupIdsAsync`), strong-consistent (C4).

M3 shipped **component posts**: an author *addresses* a post to a group through an
**audience grant** (ADR 0010 — a private group can *never* be an audience; a
group is a reuse unit, not a recipient), one-level replies, and the component
feeds over the frozen `Component` buckets. M3b added **component-scoped
moderation** over a post's *audience* (`Moderate`-gated lane, report-driven
unlock).

Across all of it, a group **remains just a collection of users.** The group page
has **no post channel of its own**: a member cannot post *to the group* as the
group, and no one sees a stream *of the group's* posts. The only group-shaped
surface a post touches today is the **audience lane** (an author *picks* a group;
its current members can *read* that author's post). The group itself is an
access-reuse unit (ARCHITECTURE.md §4.3/§5), not a room.

`how-it-works.md` has already made the promise residents will check us against:

> "You can post to a group — and when membership changes, all your past posts
> reach exactly the right people — and when membership changes, all your past
> posts follow automatically."

Today that sentence is only true for **audience grants** (an author picked a
group as a recipient). It is **not** true of a group *having a channel* its
members post *in*. This milestone closes that gap. It moves the value chain one
arrow: from *"a group is a list I address a post **to**"* to **"*a group is a
**channel** its members post **in**."** Membership becomes the **access unit**:
a group post is visible to exactly the group's *current* members and to nobody
else.

That is a **new authorization lane beyond the audience lane** — standing,
auditable, and deliberately **stricter** than the audience lane (no moderator
peek, no break-glass; G·4). A change of that standing earns its own ADR —
**ADR 0013** — and a two-part design doc that pins every invariant, seam, and
test name before any implementing unit runs, for the same reason M3 opened that
way: the guard against *distributed fragmentation* (membership access logic
re-implemented per surface) and *accidental integration* (the "single decision,
no audience evaluation" rule living in a comment, not in the invariant list).

**Naming.** Group posts are a **named capability** (a "named group channel"),
not a milestone letter — the **media** milestone is the precedent. The roadmap
letters **M4/M5/M6 stay Events / Projects / Portability**; this milestone
consumes no letter.

## Scope

**In scope**

- `Post.GroupId` **additive** field (`string`, default `string.Empty`.
  non-empty ⇒ the post is a group-channel post). ADR 0004 §B.1 additive —
  delta-detected, idempotent, no re-seed, **no `M3DocTypes` change** (Marten's
  `Schema.For<Post>()` delta picks up the new property); `PostReply` is
  **unchanged** (lane-neutral). **[U4]**
- The `IAuthorizationService` **group lane**: `AccessVia.Group` (the **8th**
  value, the M1 `Admin`-enum-ADD precedent) + two `CanSeeGroupAsync` overloads
  (plain + `IDocumentSession`). ADR 0006-E **compatible ADD** — frozen
  signatures untouched (M2 `GetProfilesAsync` / M3b `Moderate` ADD precedent).
  **[U3: ADR 0013 · U5: the lane impl · U2: the frozen C#]**
- The `PostService` **group surface**: `ListGroupFeedAsync` /
  `GetGroupPostAsync` / `CreateGroupPostAsync` + the `GroupPostDraft` record.
  Replies **reuse** the existing lane-neutral `CreateReplyAsync`.
  **[U6 · U2: the frozen C#]**
- The **Web channel surface** (thin `GroupsController`): group-post actions +
  view models **[U7]** and Razor under `Views/Groups/` — feed at
  `/groups/{id}/posts` (members see a composer), detail + reply at
  `/groups/{id}/posts/{postId}`, a plain form post (the M3 reply-form pattern —
  **no** new TS) — plus the group-detail entry point into the channel **[U8]**.
  Non-member: **404** (the M2 "plain member's POST 404s" precedent; G·3).
- The **19 pinned seam tests** in `Kumunita.Core.Tests/GroupPostServiceTests.cs`
  **[U9 implements · U2 pins the names]** + the recorded **three-test acceptance
  gate** (closed loop / handoff / part-vs-whole) **[U10 records]**.
- **ADR 0013** — group posts are a membership lane. **[U3]**

**Out of scope — deferral list (each named; carried forward)**

- **Moderation / report flow on group posts.** M3b's component-scoped moderator
  standing does not reach a membership lane; there is no `Report` on a group
  post, no queue, no resolve UI for them.
- **Notifications on new group posts** (a named M6-scope item).
- **Search over group posts** (a named M6-scope item).
- **Cross-posting a group post into a component.** The lanes stay exclusive
  (G·2) — this is a standing rule, not a deferral in practice.
- **Pagination UI beyond the existing feed paging** (no new paging controls).

**Explicitly not available (privacy-first — a standing rule, not a deferral)**

- **Break-glass on group posts: none.** A group post is visible to members only,
  full stop (G·4). There is no `Via = BreakGlass` branch on the group lane.
- **Moderator peek: none.** A non-member moderator sees a group post *iff* they
  are a member (G·4, invariant C5, ADR 0003 default-OFF).
- **Non-member authoring: none.** Only current members (owner included) may
  create a group post (G·3). A non-member create denies.

## Invariants (pinned for group posts)

Group posts are a **caller** of the ADR 0006 invariants (**C1–C6**) and the M3
owned invariants (C-M3·1 reply-inherits, C-M3·3 feed/detail audit shape), not an
owner. This milestone **owns** eight group-posts invariants, **G·1–G·8**. U2
(Part 2) freezes the exact C# and the 19 seam-test names that anchor these; each
row names the unit that lands the pin. The statements below are **verbatim**
from the group-posts master register (authoritative on wording).

| # | Invariant (verbatim) | Pinned where (unit) |
|---|---|---|
| **G·1** | group-post visibility = **current** membership (strong consistency C4); the membership lane is the **only** access decision; the audience lane is **never** evaluated (G·8). | **U5** (lane impl) · **U2** (decision algorithm) · **U9** (G1–G4, G7, G8) |
| **G·2** | lane **exclusivity**: a post is either a group-channel post **or** a component feed entry; `GroupId` non-empty ⇒ `ComponentId` empty, excluded from `ListFeedAsync` / `ListAllFeedAsync`. | **U4** (`Post.GroupId`) · **U6** (feed/detail exclusion) · **U9** (G12, G13) |
| **G·3** | **members-only** authoring: the create gate **is** the group-lane decision; non-member create denies (`UnauthorizedAccessException`; Web **404**). | **U5** (gate = lane) · **U6** (`CreateGroupPostAsync`) · **U9** (G5, G6) |
| **G·4** | nobody peeks: **no moderator lane, no break-glass**; a moderator / GlobalAdmin sees group posts **iff** they are a member (C5, ADR 0003, how-it-works.md). | **U5** (those branches absent) · **U2** (absent by contract) · **U9** (G7, G8; test #19) |
| **G·5** | audit **per lane**: feed = **one aggregate** row; detail = **one decision** row; Allow **and** Deny audited; **in-transaction** via the session overload (C3). | **U6** (row writes) · **U2** (row shape) · **U9** (aggregate/detail shape tests) |
| **G·6** | delegation is **action-scoped** (C2): an **in-scope `read`** delegate acts with the owner's standing; **out-of-scope denies**. | **U5** (delegation branch) · **U2** · **U9** (G9, G10) |
| **G·7** | replies **inherit** the parent's single group-lane decision (no second evaluation, no second row — C-M3·1 analog). | **U6** (replies reuse `CreateReplyAsync`) · **U2** · **U9** (G11) |
| **G·8** | a group post's `Audience` is written **non-null and empty**; audience grants never apply to group posts. | **U6** (`CreateGroupPostAsync` writes empty) · **U2** · **U9** (G5 empty-audience) |

**ADRs the group lane must respect (not pinned as invariants, but the code must
not violate them):**

- **ADR 0003 §Separation of duties + moderator default-OFF** — the group lane
  adds **no** moderator branch and **no** break-glass branch; a moderator /
  GlobalAdmin stands exactly as a non-member (G·4).
- **ADR 0006-D (dependency direction)** — `PostService` (Core) depends only on
  `IUserInfoService` (the live `GetGroupIdsAsync` read) + `IAuthorizationService`
  (the two new seams) + its own Marten session. It never reads
  `GroupMembership` / `DelegationGrant` for its own access decision, and
  `Kumunita.Web` is a thin `GroupsController` (the M2 group-lane surface).
- **ADR 0006-E (change management)** — the two `CanSeeGroupAsync` overloads +
  `AccessVia.Group` are the group lane's **ADDs** on `IAuthorizationService`
  (frozen signatures untouched); `Post.GroupId` is the **single additive** on the
  `Post` POCO. ADR 0006-E's *named here* list grows by exactly these lines in
  the close (U11 / U12).
- **ADR 0013 (this milestone)** — "group posts are a membership lane," authored
  by U3 and cross-referenced throughout Part 2.

## FACES (pinned, 13)

Each row is a *resident-visible outcome* (a visibility or authoring outcome)
that the group-posts seam tests (Part 2 §2.5) must cover, pinned to the invariant
that is the single authority for it. The table is **verbatim** from the
group-posts master register (authoritative on wording).

| # | Outcome (what a resident sees / can do) | Pinned by |
|---|---|---|
| **G1** | a member sees the group feed (one aggregate `AccessAudit` row). | G·1, G·5 |
| **G2** | a non-member gets the empty feed + a Deny row. | G·1, G·5 |
| **G3** | a member added **after** a post sees it on the **next** feed (C4). | G·1, C4 |
| **G4** | a member removed **after** a post loses it on the **next** detail (C4). | G·1, C4 |
| **G5** | a member creates a group post and sees it; audience written **empty**. | G·3, G·8 |
| **G6** | a non-member's create is **denied** (service exception + a deny row). | G·3 |
| **G7** | a non-member **moderator** cannot see group posts. | G·4 |
| **G8** | a non-member **GlobalAdmin** cannot see group posts (break-glass N/A). | G·4 |
| **G9** | a delegate with `read` **in scope** sees the owner's group posts (via `Delegation`). | G·6, C2 |
| **G10** | a delegate without `read` in scope sees **nothing**. | G·6, C2 |
| **G11** | a reply is visible **iff** the parent group post is visible. | G·7, C-M3·1 |
| **G12** | a group post **never** appears in the component feed (`ListFeedAsync`). | G·2 |
| **G13** | a group post **never** appears in `ListAllFeedAsync` ("all sections"). | G·2 |

**FACES count: 13.** This count (and the invariant-pin per row) is the input
U2 — who owns the seam-test list — needs to name the **19** seam-test names
(Part 2 §2.5) and the three-test acceptance gate (§2.4) without re-deriving
them.

## Drift-guard & change policy (Part 1)

- If a later unit (U2–U12) finds a mismatch between an implemented signature and
  a Part 1 pin, **this doc wins**. The unit updates this file in the same commit
  and appends a one-line drift note to the group-posts handoff note.
- The invariant *numbers* — **G·1–G·8** — and the ADR 0006 **C1–C6** and M3
  owned **C-M3·1 / C-M3·3** references are stable for the rest of the milestone.
  Adding a new group-posts owned invariant (**G·9+**) requires an ADR 0013
  amendment **plus** a design-doc edit in the same commit; renaming or renumbering
  an existing one is a breaking change and is not allowed mid-milestone.
- A new FACES row (**G14+**) is added only by a unit that ships the outcome it
  pins, in the same commit as the feature. The **FACES count is a handoff
  field** (U1 → U2, and forward): every unit that touches FACES updates the
  count in the group-posts handoff note.
- **Break-glass is not a deferral.** It is an explicit, standing **not
  available** rule (G·4; Scope §"Explicitly not available"). U3 (ADR 0013) and
  U5 (the lane impl) must **not re-litigate** it — the answer is *no by design*,
  per how-it-works.md's trust pitch, ADR 0003's moderator default-OFF, and
  invariant C5. (The **deferral** list is separate: moderation/report,
  notifications, search, cross-posting, pagination UI.)
- The **"## Group posts — Closed (recorded)"** section at the end of this file
  is a **placeholder**. U10 (the gate record) and U11 / U12 (the close) will
  append the final entry; until then that section is empty and must not be read
  as closed.

## Seams & contracts (Part 2, written by U2)

> **What this section is.** It freezes the **exact C#** that U3–U12 implement
> against: the seam list (§2.1), the new / changed Core types (§2.2), the
> lane-exclusivity + reply-inheritance rules (§2.3), the three-test
> acceptance gate (§2.4), the 19 pinned test names (§2.5), the
> module-boundary impact notes (§2.6), and the drift guard (§2.7). Every
> C# fragment is **exact**: parameter names and positions, return types, and
> the row shapes are the contract. If an implemented signature does not match
> this section verbatim, §2.7 applies: **this file wins**; the unit updates
> this file in the same commit and appends a one-line drift note to
> `docs/plans-milestones/in-progress/group-posts-handoff-notes.md`.
>
> **Counts reconciled against the register + Part 1:** **8** invariants
> (G·1–G·8) + **13** FACES (G1–G13) — U1's pass-forward count 13 ✓, master
> 13 ✓; **19** test names — verified 1:1, none renamed (§2.5). The lane's
> interface ADDs are **4 methods** (two single-target + two feed) + one
> `AccessVia` value + one additive POCO property (the two adjustments
> A1/A2, below, recorded in the `## U2` handoff section).

### Contract adjustments (two — the only deviations from the register's *directional* draft)

**U2-A1 — the feed pair.** The register's draft sketches two
`CanSeeGroupAsync` overloads, single-target only. `AccessAudit` has exactly
**two** row shapes (`TargetId` set, counts null — *decision*; or `TargetId`
null, `VisibleCount`/`HiddenCount` set — *aggregate*; `AccessAudit.cs`), and
the register's own pins require a feed row in the **aggregate** shape (G1
FACES "one aggregate `AccessAudit` row"; test #16
`Feed_AggregateAuditRowShape_GroupPost`) while detail/gate rows must stay the
**decision** shape (tests #17/#18). One single-target seam cannot write both.
Pinned: a `CanSeeGroupFeedAsync(actorId, groupId, candidateCount[, session])`
pair — the M1 single↔bulk pair precedent (`CanAsync` ↔ `CanSeeAsync`).

**U2-A2 — `targetPostId`.** The detail row's `TargetId` must be **the post
id** (test #17 `Detail_DecisionAuditRowShape_ViaGroup`), and the draft's two
parameters carry no post id. Pinned: the single-target pair takes
`string? targetPostId`; row `TargetId = targetPostId ?? groupId` — detail ⇒
post id; create-gate ⇒ group id (the channel is the target of the gate);
feed ⇒ `TargetId = null` (aggregate, A1).

Everything else in the draft is frozen verbatim: the three `PostService`
group method signatures (ctor unchanged),
`GroupPostDraft(string GroupId, string? Title, string Body)`,
`Post.GroupId : string, default string.Empty`, `AccessVia.Group` as the
8th value, and the lane's decision algorithm (in-scope `read` ⇒ the
**owner's** standing ⇒ live `GetGroupIdsAsync`; **no** Moderate, **no**
BreakGlass, G·4). The deny row's `Via` is **pinned, not adjusted**: plain
non-member ⇒ `Group`; a grant case ⇒ `Delegation` (M1's `denyVia =
isDelegated ? Delegation : Audience` pattern — `AuthorizationService.Decide`
/ `ResolveActorAsync` — with the group lane's own-standing value `Group`
where M1's lane uses `Audience`).

The group lane's decision shape (frozen; U5 implements exactly this):

```
// ── The lane's core (in both lane methods; reads come from IUserInfoService
//    — the lane never reads GroupMembership/DelegationGrant rows directly).
grant = GetActiveGrantAsync(actorId)          // scope = grants per action (C2)

if (grant is not null and grant.Scope contains AccessAction.Read.Id):
    principal  = grant.OwnerId                // in-scope read ⇒ the OWNER's standing (G·6)
    isDelegated = true
    groups = GetGroupIdsAsync(principal)      // the **owner's** live membership (C4)
else:
    principal  = actorId
    isDelegated = grant is not null           // M1 C2 "acting" identity:
                                              //   out-of-scope acts as the delegate themself
    groups = GetGroupIdsAsync(actorId)        // live membership (C4)

allowed = groups.Contains(groupId)
via     = isDelegated ? AccessVia.Delegation : AccessVia.Group   // Allow **and** Deny rows
return new Decision(allowed, via, principal)   // + the row for the call site (§2.1 shapes)

// Absent **by contract** (G·4): no owner-skip branch (an author is allowed iff
// they are still a member — G4 FACES); no HasBreakGlassAsync (AdminOverride)
// read; no ModeratorAssignment / Component.ModeratorAccess path; no
// EvaluateAudience call (G·1/G·8 — Post.Audience is empty anyway). The action
// on every group-lane row is "read" (the lane never takes AccessAction.Moderate
// input — test #19).
```

### 2.1 Frozen seam list (exact C#)

**Pre-existing seams (frozen as of M1/M2/M3b; the group posts *call and
reuse* these; none changes):**

```csharp
// ── IAuthorizationService — the four frozen methods (ADR 0006-A) stay
//    byte-identity; the group lane's ADDs go *in* (ADR 0006-E compatibility
//    lane — M3b's Moderate-ADD is the precedent; M2's GetProfilesAsync ADD
//    the interface precedent):
public interface IAuthorizationService
{
    Task<Decision>   CanAsync(string actorId, AccessAction action, IAuditableResource target);
    Task<Decision>   CanAsync(string actorId, AccessAction action, IAuditableResource target, IDocumentSession session);
    Task<VisibleSet> CanSeeAsync(string actorId, AccessAction action, IEnumerable<IAuditableResource> candidates);
    Task<VisibleSet> CanSeeAsync(string actorId, AccessAction action, IEnumerable<IAuditableResource> candidates, IDocumentSession session);
}

// ── Decision (Authorization/Decision.cs) — unchanged; the lane reuses the shape:
public sealed record Decision(bool Allowed, AccessVia Via, string EffectivePrincipalId);

// ── AccessAudit (Authorization/AccessAudit.cs) — the only **two** row
//    shapes that exist; a group-lane row must use one of them verbatim:
//    * decision shape:    TargetId set, VisibleCount/HiddenCount null
//    * aggregate shape:   VisibleCount + HiddenCount set, TargetId null
public class AccessAudit
{
    public string  Id                   { get; set; }
    public DateTimeOffset At            { get; set; }   // server time
    public string  ActorId              { get; set; }
    public string  EffectivePrincipalId { get; set; }
    public string  Action               { get; set; }   // group rows: always "read"
    public string  TargetKind           { get; set; }   // group rows: always "grouppost"
    public string? TargetId             { get; set; }   // null iff aggregate row
    public int?    VisibleCount         { get; set; }   // set iff aggregate row
    public int?    HiddenCount          { get; set; }   // set iff aggregate row
    public AccessVia Via                { get; set; }
    public AccessOutcome Outcome        { get; set; }
}

// ── IUserInfoService — the lane's **only** reads (M1/M2 seams, unchanged — §2.6):
Task<HashSet<string>>  GetGroupIdsAsync(string userId);        // live membership (C4)
Task<DelegationGrant?> GetActiveGrantAsync(string delegateId); // Scope: action-id grants (C2)

// ── DelegationGrant (UserInfo/Group.cs) — unchanged; the lane's branch reads:
//    Scope is IReadOnlyList<string> of action ids (AccessAction.Id), and the
//    IsActiveAt(delegateId, now) helper resolves "active" — neither changes.

// ── PostService (Posts/PostService.cs) — reused **as-is** (group posts
//    structurally never reach them — §2.3(a)); return types reused, no new
//    result records:
public Task<FeedResult>        ListFeedAsync(string componentId, string actorId, int page)
public Task<FeedResult>        ListAllFeedAsync(IReadOnlyCollection<string> componentIds, string actorId, int page)
public Task<PostDetailResult>  GetPostAsync(string postId, string actorId)
public Task<Post>              CreateReplyAsync(string postId, string actorId, string body, IDocumentSession session)

public sealed record FeedResult(IReadOnlyList<Post> Visible, int HiddenCount, int Page, int Total);
public sealed record PostDetailResult(Post? Post, IReadOnlyList<PostReply> Replies);

// PostReply (Posts/PostReply.cs) — unchanged: Id, PostId, AuthorId, Body,
// Created. No Audience field — replies inherit their parent's decision.
```

**The group-lane ADDs (exact C# — the doc-comments are the contract U5
implements against):**

```csharp
/**
 * Group lane — ADR 0013's ADDs on IAuthorizationService (ADR 0006-E
 * compatibility lane: the four frozen signatures above untouched).
 *
 * The decision is **membership only** (G·1): the effective principal's
 * **live** membership in `groupId` (IUserInfoService.GetGroupIdsAsync;
 * strong consistency — C4). **No** owner-skip (an author is allowed iff a
 * member — G4 FACES), **no** AccessVia.Moderator branch, **no** break-glass
 * read (HasBreakGlassAsync / AdminOverride / ModeratorAssignment are never
 * touched on this lane — G·4), **no** audience evaluation (G·1/G·8).
 *
 * Delegation (G·6, C2): an in-scope `read` grant (grant.Scope contains
 * "read") acts with the **owner's** membership; an out-of-scope grant acts
 * as the delegate themself (M1's acting-identity rule) and still audits
 * Via Delegation.
 *
 * Rows (C3): **every** call — Allow **and** Deny — writes exactly one
 * AccessAudit row: TargetKind "grouppost", Action "read". Standalone
 * methods commit their own row; the IDocumentSession overloads store the
 * row into the caller's transaction (same lane as CanAsync(..., session)).
 */

/// <summary>
/// Single-target group-lane decision. Row: the **decision** shape —
/// TargetKind "grouppost", **TargetId = targetPostId ?? groupId**
/// (detail ⇒ the post id; create-gate ⇒ the group id), counts null,
/// Via Group / Delegation, Outcome per membership.
/// </summary>
Task<Decision> CanSeeGroupAsync(string actorId, string groupId, string? targetPostId);

/// <summary>
/// Same decision with the row **in the caller's transaction** — the
/// G·3 create-gate lane: CreateGroupPostAsync commits the Deny row via its
/// own SaveChangesAsync **before** throwing (the row must survive — G6
/// FACES) and commits Allow-row + post in one SaveChangesAsync (atomic, C3).
/// </summary>
Task<Decision> CanSeeGroupAsync(string actorId, string groupId, string? targetPostId, IDocumentSession session);

/// <summary>
/// **Feed (whole-channel)** decision: the channel is all-or-nothing for a
/// principal (membership — G·1), so one call per visit covers the page and
/// writes that visit's **aggregate** row (G·5, the C-M3·3 analog):
/// TargetKind "grouppost", **TargetId null**, VisibleCount/HiddenCount =
/// (candidateCount, 0) on Allow, (0, candidateCount) on Deny.
/// `candidateCount` = the page's candidate count this call evaluated
/// (ListGroupFeedAsync passes the count it loaded; 0 candidates ⇒ no call,
/// no row — the M3 feed 0-candidate shape).
/// </summary>
Task<Decision> CanSeeGroupFeedAsync(string actorId, string groupId, int candidateCount);

/// <summary>
/// Same feed row in the caller's transaction (the C3 lane — kept for the
/// interface's two-form pattern).
/// </summary>
Task<Decision> CanSeeGroupFeedAsync(string actorId, string groupId, int candidateCount, IDocumentSession session);
```

### 2.2 New / changed Core types (exact C#)

```csharp
// ── AccessVia (Authorization/Decision.cs) — the group lane's **add**: the 8th
//    value, appended **after Admin** (never inserted mid-enum — stored audit
//    rows would re-map; M1's Admin-APPEND is the precedent; ADR 0004 §B.1
//    additive lane):
public enum AccessVia
{
    Owner, Audience, Delegation, Moderator, Report, BreakGlass, Admin,

    /// <summary>
    /// Group channel access (ADR 0013, G·1): the post's visibility is
    /// membership in `Post.GroupId`. Appears on group-lane AccessAudit
    /// rows — Allow **and** Deny (a grant case audits AccessVia.Delegation
    /// instead — the shape in §2.1).
    /// </summary>
    Group
}
```

```csharp
// ── GroupPostDraft (Posts) — the new input record. **No Audience member**
//    (G·8): the service writes the audience non-null **empty**; the
//    author's choice is *which group* (the lane), not an audience.
/// <summary>
/// A group-post create input (ADR 0013, G·2/G·8). <see cref="GroupId"/> is
/// the non-empty channel (enforced: null/empty ⇒ ArgumentException **before**
/// any decision — no audit row is written for such input);
/// <see cref="Title"/> optional (M3's PostDraft nullable-Title handling);
/// <see cref="Body"/> non-null (the Post.Body non-null invariant).
/// </summary>
public sealed record GroupPostDraft(string GroupId, string? Title, string Body);
```

```csharp
// ── Post (Posts/Post.cs) — the **single additive** (ADR 0004 §B.1; the M3b
//    `Status` ADD precedent — Marten's delta picks up the property: no
//    M3DocTypes change, no re-seed). All other POCO members (Id,
//    ComponentId, AuthorId, Title, Body, Audience, Created, Modified,
//    Status) + PostStatus + PostReply are **untouched**.
public sealed class Post
{
    // … (M1/M2/M3/M3b members unchanged) …

    /// <summary>
    /// The group channel this post belongs to (ADR 0013). **Non-empty ⇒
    /// group-lane post** (G·2): membership is the **sole** access decision
    /// (G·1 — the audience lane is never evaluated; G·8 — the audience is
    /// written non-null **empty**), <see cref="ComponentId"/> is **empty**
    /// (lane exclusivity — the post is structurally absent from
    /// <see cref="PostService.ListFeedAsync"/> / <see cref="PostService.ListAllFeedAsync"/>,
    /// §2.3(a)), and only members may create or see it (G·3/G·4). Empty
    /// (the default) ⇒ component post (M3/M3b), unchanged. Written **only**
    /// via <see cref="PostService.CreateGroupPostAsync"/>.
    /// </summary>
    public string GroupId { get; set; } = string.Empty;
}
```

```csharp
// ── PostService (Posts/PostService.cs) — **three** new public methods; the
//    ctor is **unchanged** (IUserInfoService, IAuthorizationService,
//    IDocumentStore — the lane owns every membership read; no new dependency,
//    ADR 0006-D). M3/M3b methods (ListFeedAsync, ListAllFeedAsync,
//    GetPostAsync, CreatePostAsync, CreateReplyAsync, HidePostAsync,
//    RemovePostAsync) are **untouched** — no group-post branch is added to
//    any of them (G·2's exclusion is structural, §2.3(a)).
/// <summary>
/// A group's channel feed (G1–G4 FACES, §2.3(a)): the candidate set is the
/// group's posts — `Post.GroupId == groupId`, `Created desc`, paged with the
/// class's existing `PageSize` (the 30-per-page shape is M3's, unchanged).
/// Exactly one <see cref="IAuthorizationService.CanSeeGroupFeedAsync(string, string, int)"/>
/// (the **standalone** form — a plain read with no in-flight caller
/// transaction, the ListFeedAsync precedent) writes the visit's **single
/// aggregate** <c>AccessAudit</c> row (G·5: TargetKind "grouppost",
/// TargetId null, counts). Allow ⇒ the paged candidates (G1); Deny ⇒
/// <see cref="FeedResult"/> with an **empty** visible list and
/// <see cref="FeedResult.HiddenCount"/> = the candidate count (G2).
/// **No audience evaluation of any kind** (G·1/G·8).
/// </summary>
public Task<FeedResult> ListGroupFeedAsync(string groupId, string actorId, int page);

/// <summary>
/// A group post's detail + its one-level replies (G11 FACES, §2.3(b) — the
/// C-M3·1 analog, G·7): the post is loaded first (the M3 fail-closed shape:
/// missing ⇒ <c>Post = null</c>, no decision, **no** row); a post with an
/// **empty** <c>GroupId</c> (not a group post) or <c>GroupId != groupId</c>
/// (route/lane mismatch) ⇒ <c>Post = null</c>, no row (fail-closed).
/// Otherwise **exactly one**
/// <see cref="IAuthorizationService.CanSeeGroupAsync(string, string, string?)"/>
/// (standalone) — the detail decision row (TargetKind "grouppost",
/// **TargetId = postId**, G·5). Allow ⇒ the post + its
/// <see cref="PostReply"/> list **as-is** — replies inherit the parent's
/// single group-lane decision: no second evaluation, no per-reply row (G·7).
/// Deny ⇒ <c>Post = null</c> with **no** replies (Web 404 — G·3/G·4) — the
/// decision's row **was** written (C3).
/// </summary>
public Task<PostDetailResult> GetGroupPostAsync(string groupId, string postId, string actorId);

/// <summary>
/// Creates a group post, in the **caller's** in-flight session (C3). The
/// **create gate is the group-lane decision** (G·3): one
/// <see cref="IAuthorizationService.CanSeeGroupAsync(string, string, string?, IDocumentSession)"/>
/// with <c>targetPostId: null</c>, in the caller's transaction — **deny**:
/// the row is committed by a <c>SaveChangesAsync()</c> **before**
/// <see cref="UnauthorizedAccessException"/> throws (the gate row must
/// survive — G6 FACES; Web maps it to 404); **allow**: the gate row + the new
/// post commit in **one** <c>SaveChangesAsync()</c> (atomic with the write,
/// C3). The gate is the **sole** decision (G·3): **no** <c>actorRoles</c>
/// parameter (contrast CreatePostAsync's GlobalAdmin/moderator skip — a
/// non-member GlobalAdmin is denied, G8 FACES/G·4), **no** break-glass, and
/// **no** membership read here (the lane owns its reads — ADR 0006-D).
/// The write pins G·2/G·8: <c>ComponentId = string.Empty</c>,
/// <c>Audience = new Audience()</c> (non-null, **empty**). One
/// <c>SaveChangesAsync()</c>.
/// </summary>
/// <exception cref="UnauthorizedAccessException">The actor (or, under an
/// in-scope `read` grant, the owner) is not a member of
/// <c>draft.GroupId</c> — thrown **after** the gate row is persisted.</exception>
public Task<Post> CreateGroupPostAsync(GroupPostDraft draft, string actorId, IDocumentSession session);
```

**Shapes (U6 implements exactly these):**

- `ListGroupFeedAsync`: argument guards exactly as the siblings (null
  `groupId`/`actorId` ⇒ throw; `page < 1` ⇒ 1). `QuerySession()` →
  `Query<Post>().Where(p => p.GroupId == groupId).OrderByDescending(p => p.Created)
  .Skip((page - 1) * PageSize).Take(PageSize)`. **0 candidates ⇒** empty
  `FeedResult`, no decision, **no row** (the M3 `ListFeedAsync` 0-candidate
  shape). Else the standalone feed-lane call with `candidateCount =
  candidates.Count`; map to `FeedResult(Visible: allow ? candidates : empty,
  HiddenCount: deny ? count : 0, Page: page, Total: candidates.Count)`.
- `GetGroupPostAsync`: guards as the siblings; `QuerySession()` → load the
  post → fail-closed shapes as doc-commented (missing / empty `GroupId` /
  route mismatch ⇒ `(null, [])` + no row) → the standalone single-target
  call → Allow: `Query<PostReply>().Where(r => r.PostId == postId)
  .OrderBy(r => r.Created)` returned as-is (the M3 replies shape); Deny:
  `(null, [])`.
- `CreateGroupPostAsync`: guards as `CreatePostAsync` (draft/`actorId`/
  `session` null-checked, `draft.GroupId` non-empty enforced — §2.2 above) →
  the session-variant gate call → deny: `SaveChangesAsync()` (persist the
  row) **then** `throw new UnauthorizedAccessException(…)` → allow: build the
  `Post` (Id = new; `ComponentId = string.Empty`; `GroupId = draft.GroupId`;
  `AuthorId = actorId`; `Title`/`Body` from the draft; `Audience = new
  Audience()`; `Created = now`) → store → `SaveChangesAsync()` (row + post,
  one commit) → return the post.

### 2.3 Lane exclusivity + reply inheritance (rule)

**(a) Group post vs. component post — the lane split (G·1/G·2; M3's
owner/moderation/break-glass branches do not exist on this lane):**

| Aspect | Component post (M3/M3b — **unchanged**) | Group post (ADR 0013) |
|---|---|---|
| Access decision | Owner → Moderate → BreakGlass → MatchGroups (one pass, C6) | **membership only** — the audience lane is **never** evaluated (G·1/G·8) |
| author-is-owner standing | always allowed (owner branch first) | allowed **iff still a member** (G4 FACES — no owner-skip on this lane) |
| moderation / break-glass | Moderate-gated hide/remove; `AdminOverride` peek | **none available** (G·4 — *unavailable*, not deferred) |
| delegation (C2) | in-scope grant borrows the owner's standing | the **same rule** on **membership**: in-scope `read` ⇒ `GetGroupIdsAsync(owner)` (G·6) |
| `ComponentId` | non-empty (the feed organizer) | **empty** (G·2) |
| `GroupId` | empty | **non-empty** (G·2) |
| `Audience` | author's choice, frozen (ADR 0001-B) | non-null, **empty**, written by the service (G·8) |
| create gate | ComponentMembership + role skips; no service audit row | the group-lane decision; the Deny row is **persisted** (G·3, G·5) |
| feeds | `ListFeedAsync` / `ListAllFeedAsync` (both filter on `ComponentId`) | `ListGroupFeedAsync` only — never in a component feed (**structurally**: the two M3 queries filter on `ComponentId` and group posts write it empty — U6 adds no filter; G12/G13 pin the outcome) |
| replies | inherit the parent (C-M3·1) | inherit the parent (G·7) — the same rule on the group decision |
| audit rows | TargetKind "post" — aggregate feed + decision detail | TargetKind **"grouppost"** — same two shapes (§2.1) |

**(b) Reply inheritance (G·7 = C-M3·1, applied to the group decision) — the
four shapes:**

| Shape | What happens to the replies |
|---|---|
| parent **Allow** (via Group / Delegation) | the replies render — **no** reply row (G·7) |
| parent **Deny** | the replies are **not evaluated**; the row is the parent's Deny (G·7) |
| detail missing / lane mismatch (post missing, `GroupId` empty, or `≠ groupId`) | fail-closed, **no** decision, **no** row (the M3 shape) |
| reply **create** (`CreateReplyAsync` — unchanged) | stored with no decision, no row — the method stays **lane-neutral**; the Web group reply route runs the detail's Allow first (§2.6, Web) |

All rows reduce to the parent's one decision (C6's one-decision-per-visit,
applied to the group lane).

### 2.4 The acceptance gate (U10 records the run)

| # | Test | Shape (what it proves) |
|---|------|------------------------|
| 1 | **closed loop** | a member creates a group post → it appears in **their** group feed (the visible list contains the post); the feed's aggregate row exists: `TargetKind = "grouppost"`, `TargetId = null`, `VisibleCount ≥ 1`, `HiddenCount = 0`, `Outcome = Allow` (G5, G·5) |
| 2 | **handoff** | a member added **after** the post was created sees it on the **next** feed — the membership change takes effect immediately (C4, G3 FACES); the **delegated `read` (in-scope)** branch is the handoff case onto a delegate — the *same* pin (G9, G·6/C2: in-scope ⇒ the **owner's** membership) |
| 3 | **parts vs. whole** | the 19 names in §2.5 are the whole; tests 1–2 are the parts; all must pass **together**, in the **same** `Kumunita.Core.Tests` run as the inherited M1/M2/M3/M3b/media anchors (no per-name isolation) |

**Runner note (AGENTS.md test-runner quirk, this machine):** the reliable
path is build then in-process execution — **not** `dotnet test` / VS Test
Explorer (xunit.v3 discovery goes wrong: "No tests found / exit code 5" is a
**runner** bug, not a failure):

```
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
```

U10 records the actual pass counts from that run (the M3 §2.5 drift lane
applies: if a pinned name did not land verbatim, rename it in the same
commit + one-line drift note).

### 2.5 The pinned seam tests (exact names — 19, the master is authoritative)

File: `tests/Kumunita.Core.Tests/GroupPostServiceTests.cs`. U2 verified the
register's 19 **1:1 — none renamed, none added, none removed**; each name is
the pin hereafter (U6 lands the seam, U9 owns this file and its 19
`[Fact]`s; a rename or re-scope of a name after U2's freeze = a drift
event, §2.7):

| # | test name (exact) | anchored to |
|---|-------------------|-------------|
| 1 | `G1_MemberSeesGroupFeed` | G1 FACES; G·1, G·5 (aggregate Allow shape) |
| 2 | `G2_NonMemberFeedEmptyWithDenyRow` | G2 FACES; G·1, G·5 (aggregate Deny row, `Via = Group`) |
| 3 | `G3_MembershipAddReScopesNextFeed` | G3 FACES; C4, G·1 |
| 4 | `G4_MembershipRemoveRevokesNextDetail` | G4 FACES; C4, G·1 (an author who is removed is denied — no owner-skip) |
| 5 | `G5_MemberCreatesGroupPostSeesIt` | G5 FACES; G·3 (gate Allow), G·5 (gate Allow row) |
| 6 | `G5_GroupPostAudienceWrittenEmpty` | G5 FACES; G·8 (the audience written non-null, **empty**) |
| 7 | `G6_NonMemberCreateDenied` | G6 FACES; G·3 (`UnauthorizedAccessException` **and** the gate Deny row persisted) |
| 8 | `G7_ModeratorNonMemberDenied` | G7 FACES; G·4 (*fixture*: a `ModeratorAssignment` present — still Deny) |
| 9 | `G8_BreakGlassDoesNotApplyToGroupPosts` | G8 FACES; G·4 (*fixture*: a consumed `AdminOverride` present — still Deny) |
| 10 | `G9_DelegateWithReadInScopeSeesOwnerGroupPosts` | G9 FACES; G·6, C2 (in-scope ⇒ `GetGroupIdsAsync(owner)`) |
| 11 | `G10_DelegateWithoutReadDenied` | G10 FACES; G·6, C2 (out-of-scope acts as the delegate themself; the pinned case: the delegate is **not** a member themself ⇒ Deny, row `Via = Delegation`) |
| 12 | `G11_ReplyInheritsParentGroupLane` | G11 FACES; G·7 (parent's Allow ⇒ the replies as-is; **no** reply row) |
| 13 | `G11_ReplyNotEvaluatedOnParentDeny` | G11 FACES; G·7 (parent's Deny ⇒ the replies not evaluated; **no** reply row) |
| 14 | `G12_GroupPostExcludedFromComponentFeed` | G12 FACES; G·2 (structural exclusion — §2.3(a)) |
| 15 | `G13_GroupPostExcludedFromAllFeed` | G13 FACES; G·2 (structural exclusion — §2.3(a)) |
| 16 | `Feed_AggregateAuditRowShape_GroupPost` | G·5; the aggregate shape (§2.1): TargetKind "grouppost", `TargetId` null, counts set, Action "read" |
| 17 | `Detail_DecisionAuditRowShape_ViaGroup` | G·5; the decision shape: `TargetId = postId`, counts null, `Via = Group` |
| 18 | `Detail_DecisionAuditRowShape_ViaDelegation` | G·5, C2; the decision shape: `Via = Delegation`, `EffectivePrincipalId = owner` |
| 19 | `PostService_MakesNoModerateOrBreakGlassCallOnGroupPosts` | G·4 (absence at the **seam** level: the group surface issues only the group-lane authz calls — no audience-lane `CanAsync`/`CanSeeAsync`, no `Moderate`; G7/G8 assert the same at the decision level with fixtures) |

### 2.6 Module-boundary impact notes

- **UserInfo — unchanged.** The lane's only reads are the two frozen M1/M2
  seams: `GetGroupIdsAsync` (live membership, C4) + `GetActiveGrantAsync`
  (grant scope, C2). **No** UserInfo ADD this milestone (contrast M3's
  `GetComponentsAsync` ADD). Membership add/remove stays M2's write lane —
  the G3/G4 strong-consistency tests drive it through the existing surface.
- **Authorization — the lane ADDs only.** The four group-lane methods (§2.1)
  + the `AccessVia.Group` value (§2.2) are the **entire** interface delta of
  the milestone; the four frozen signatures stay byte-identity (ADR 0006-E;
  the M3b Moderate-ADD precedent). The lane implementation (U5) reads
  `IUserInfoService` + its own `IDocumentStore` only — **never**
  `Group`/`GroupMembership` for its own access decisions (ADR 0006-D).
- **Posts — composes, does not derive.** `PostService`'s group surface (U6)
  calls the frozen lane; the lane owns every membership read (U6 never reads
  `GroupMembership`/`DelegationGrant` itself — M1's lane-separation,
  unchanged). `CreateReplyAsync` / `HidePostAsync` / `RemovePostAsync` /
  `GetPostAsync` and the two M3 feeds are **untouched** — G·2's exclusion is
  structural (§2.3(a)); `PostReply` unchanged (G·7), `Post` gains exactly the
  one additive (§2.2).
- **Web — a thin controller over `PostService` (U7/U8).** The group-post
  actions call `PostService` **only** (not `IAuthorizationService` directly —
  the M3 PostsController rule): feed/detail for a non-member ⇒ **404** (G·3/
  G·4 — never a 403-audited peek; the audit row is the decision's);
  create-deny ⇒ **404** (Web already maps `UnauthorizedAccessException`);
  the group reply route runs the detail's **Allow first** (§2.3(b), row 4),
  then `CreateReplyAsync`; the composer's write lands in the Web's in-flight
  session (C3) — the M3 composer precedent.
- **ADR 0013 (U3)** — "group posts are the membership lane": Part 2
  (§2.1–§2.3) *is* that contract; the ADR points at it rather than
  re-deriving it, and keeps break-glass **out of scope** (the "unavailable"
  flag — the deferral list is separate: moderation/report, notifications,
  search, cross-posting, pagination UI — Part 1).
- **ADR 0006-E's "named here" list grows** by: the four group-lane methods +
  the `AccessVia.Group` value + the `Post.GroupId` additive (a doc property,
  not an interface method) + the `GroupPostDraft` record (a type, not a
  seam). The ADR's rule itself is unchanged.

### 2.7 Drift guard (frozen once written)

- Part 1's **8 invariants** (G·1–G·8) and **13 FACES** (G1–G13) — the
  numbers are stable; a rename/renumber is a **break** (Part 1 already
  pinned that; §2.4/§2.5's names depend on it).
- **The lane seam** (§2.1): the four **exact** signatures (including the
  `targetPostId` / `candidateCount` parameters — A1/A2, part of the freeze),
  the decision's branch order and via-pin, and the three row shapes
  (decision / aggregate / gate — all `TargetKind "grouppost"`, Action
  "read") — freeze when **U5** lands it; the lane **owns** it; later units
  consume, never re-scope it.
- **`AccessVia.Group`** — 8th value, **appended after `Admin`** (no
  mid-enum insert — stored audit rows would re-map; the ADR 0004 §B.1
  additive lane, M1's Admin-APPEND precedent).
- **`Post.GroupId`** — the **one** additive property on `Post` (empty
  default = a component post; non-empty = a group-lane post, G·2);
  `PostReply` / `PostStatus` unchanged; **zero** `M3DocTypes` changes
  (Marten's delta detects the property — "no re-seed") — freeze when
  **U4** lands it.
- **`GroupPostDraft(GroupId, Title?, Body)`** — no `Audience` member (G·8) —
  freeze when **U6** lands it.
- **`PostService`** — the ctor unchanged; the three group-method signatures +
  their lane/audit pins (§2.2) — freeze when **U6** lands it; Web consumes;
  U9 tests it.
- The **two tables** in §2.3 (a)+(b) — a new row is a drift event *or* a new
  FACES row, same commit (never silent).
- **The 19 names in §2.5** — freeze when U9 owns
  `GroupPostServiceTests.cs`; a rename/re-scope of a name afterwards = a
  drift event.
- **U4–U12's allowed ADDs** (unit-series rule 4, made exact): **only**
  §2.1–§2.2 — the four group-lane methods, `AccessVia.Group`, the three
  `PostService` group methods, `GroupPostDraft`, `Post.GroupId` — plus
  U7/U8's Web surface in `Views/Groups/` + `GroupsController` + view models.
  **Any other Core ADD** = a `## U<m> — drift pause`.
- **The contract-adjustment record:** U2-A1 / U2-A2 (above + in the `## U2`
  handoff section) are the **only** deviations from the master register's
  directional draft, and they are **part of the freeze** — a later unit
  "correcting them back to the draft" is a drift pause, not a fix.
- **Counts (the freeze at a glance):**

| Pin | Count | Owner |
|---|---:|---|
| Invariants (Part 1) | 8 (G·1–G·8) | U1 |
| FACES (Part 1) | 13 (G1–G13) | U1 |
| lane interface ADDs (§2.1) | 4 methods | U5 |
| `AccessVia` add (§2.2) | 1 (Group, 8th) | U5 |
| POCO add (§2.2) | 1 (`Post.GroupId`) | U4 |
| new records (§2.2) | 1 (`GroupPostDraft`) | U6 |
| `PostService` public ADDs (§2.2) | 3 | U6 |
| test names (§2.5) | 19 | U9 |
| gate tests (§2.4) | 3 | U10 |
| `M3DocTypes` changes | **0** | — |

## Group posts — Closed

*Placeholder — U10's gate record lands here; U11/U12 append their close
entries. Empty until the gate has run. (U2 added this heading: Part 1's
drift guard references it, but the file never had the section.)*
