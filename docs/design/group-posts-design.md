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
