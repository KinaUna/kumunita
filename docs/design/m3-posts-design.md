# Design Doc — M3: Posts, component lists (moderation deferred to M3b)

> Part 1 of 2 (U1). Part 2 (U2) will append "Seams & contracts (Part 2)" with the
> exact C# shapes U3–U11 must match, the **seeded-invariants test list**, the
> **three-test acceptance gate**, and the drift-guard — mirroring
> [`design/m2-directory-profiles-groups.md`](m2-directory-profiles-groups.md)
> §2 (§2.1 frozen seam list, §2.2 new Core types, §2.3 candidate-filter rule,
> §2.4 reply-inherits rule, §2.5 seam-test names, §2.6 gate, §2.7
> drift-guard). Both parts pin the invariant / FACES numbers that every M3
> unit (U3–U12) must match.

## Context

M1 shipped the access model and stored the audience / group / delegation /
`AccessAudit` / `Component` documents (with `Component.ModeratorAccess`
reserved for the M3b report-driven unlock). M2 shipped the first three surfaces
over those stored documents — **directory**, **profile editor**, **groups** —
with the `DirectoryService` / `ProfileToAuditableResource` composition and
M2's `GetProfilesAsync` ADD on `IUserInfoService`. M2 moved the value chain
one arrow: from stored documents to *shared awareness* (a resident can find
other residents and see what they've chosen to share).

`how-it-works.md`'s next promise — *"Post and discuss. Announcements and
conversations, organized into topics like Safety, Maintenance, Social, and
Governance"* — is still prose, not product. **M3 delivers that arrow
(signals → shared awareness) as product:** a resident can post, a resident
can reply one level deep, and both surfaces are organized by the frozen
`Component` buckets that M1 seeded.

M3 also registers — but **does not implement** — the `Report` document. The
*table* lands in M3's storage step for forward compatibility (per the Q1↔Q3
resolution: the table in M3, the flow in M3b); the workflow (file / assign /
unlock / resolve), the report-driven moderator unlock, the `Via = Report`
read branch, and the moderator surfaces (queue, resolve UI) all **carry to
M3b**. M3 ships **no moderation actions**, does **not** touch `/admin`, and
does **not add** a `Moderate`-on-post audit branch. M3's *M1-style* "out of
scope" close is the M3b deferral list below.

M3 is the **first content milestone**: M1 was identity + access over the
seeded four components; M2 was surfacing over M1's stored documents; M3 is
the first write surface beyond `UpsertProfileAsync`. That is why M3 — like
M2 — opens with a two-part design doc that pins every seam, invariant, and
test name before any implementing unit runs: the guard against *Distributed
fragmentation* (access logic re-implemented per list) and *Accidental
integration* (the reply-visibility rule living in a comment, not in the
invariant list).

## Scope

**In scope**

- `PostService` (Kumunita.Core, bounded context `Kumunita.Core.Posts`) — the
  first M3-owned feature-service, composing **only** `IUserInfoService` (read
  lane) + `IAuthorizationService` + its own Marten session. Never reads
  `GroupMembership` or `DelegationGrant` for access purposes (ADR 0006-D
  lane, mirrors M2 §3 C-M2·2).
- The `Post` / `PostReply` / `Report` documents (Poco, Marten-native,
  conventional `string Id`; the `Report` table is **registered but dormant**
  — no surface, no tests, no workflow in M3).
- The `PostToAuditableResource : IAuditableResource` adapter (single `Id`,
  `Name`, `OwnerId`, `Audience`, `ComponentId`, `TargetKind = "post"`).
- `IUserInfoService.GetComponentsAsync(bool enabledOnly)` — **the single
  M3 ADD** on a frozen interface (ADR 0006-E compatible-lane, precedent
  `GetProfilesAsync`; doc-comment says *candidate* set, no audit row, never
  a visible set).
- `M3DocTypes.Configure(StoreOptions)` — the new parallel document surface
  (analogous to `M1DocTypes`). U3 wires it into both boot paths
  (`Kumunita.Core/Bootstrap/SchemaBootstrap.cs` + `Kumunita.Web/Program.cs`).
- `Kumunita.Web` surface:
  - `/community/{componentId}` — component feed (one aggregate
    `AccessAudit` row per render).
  - `/posts/{id}` — post detail + one-level replies (one decision row per
    render; reply visibility inherits the parent's `Read`).
  - `/posts/new` — composer (component picker via `GetComponentsAsync`;
    audience selector **reuses** M2's `_AudienceEditor` partial verbatim —
    no new seam).
  - View models + Razor views under `Kumunita.Web/Views/Posts/`.
  - A nav item on `/community` (the seed components) + the existing
    "Profile" / "Directory" / "Groups" items — **no new nav for moderation;
    that is M3b's surface**.
- Seam tests (`Kumunita.Core.Tests/PostServiceTests.cs`) and e2e Playwright
  specs (`Kumunita.Web.Tests/`) — test **names** are pinned by Part 2 (U2);
  this part pins the invariants / FACES they must anchor to.

**Out of scope — M3b deferral (this section is M3's M1-style close)**

- Report workflow: **file** (submit a report from a post / reply), **assign**
  (a GlobalAdmin names a moderator), the report-driven **moderator
  `ModeratorAccess` unlock**, and **resolve** (clear the report, flip the
  flag back). The `Report` *table* is registered in M3 for forward
  compatibility (the Q1↔Q3 resolution: the table in M3, the flow in M3b);
  M3 ships no workflow over it.
- The `Via = Report` read branch on a post (a moderator sees a previously-
  invisible post *through* a filed report — the C5 carve-out the M3b surface
  will exercise). M3 does **not** exercise `Moderate`-on-post; the reserved
  `AccessAction` case stays dormant. **M3 tests assert the absence** (F3 /
  F8).
- Moderator surfaces: the queue, the resolve UI, the "assign to a moderator"
  form. `/admin` (M1's admin surface) is **unchanged** in M3 — M3b owns any
  `/admin` surface that adds a report queue.
- The post *status* field (hidden / removed) and the M3b removal path. M3's
  post has no `Status` column.
- Export, iCal, federation, MCP/API (as in M1 / M2).

**Surfaces (resident-visible)**

1. Component feed (`/community/{componentId}`) — the posts in a given
   component the viewer may see, with the hidden count in the aggregate row;
   no hidden-field rendering.
2. Post detail (`/posts/{id}`) — the post body (if visible), one level of
   replies, an inline reply composer.
3. Composer (`/posts/new`) — `title` + `body` + component picker + the
   audience editor (M2's `_AudienceEditor` partial, reused verbatim).

## Invariants (pinned for M3)

M3 is a *caller* of the ADR 0006 invariants, not an owner (mirrors M2's
positioning). M3 *owns* three new invariants (C-M3·1/2/3 — the three
behavioral rules M3 is the first milestone to need: reply inheritance,
component-candidate isolation, feed/detail audit-shape). The following ADR
0006 + two companion ADRs **must keep holding for the M3 surfaces**, and the
seam tests for M3 (pinned by Part 2 §2.5) will name each by id.

> **Plan-count note (U1):** the plan § U1 headline says "12 invariants" but
> its bullet list enumerates **11**. This doc pins the 11 from the body (the
> body is authoritative). The handoff note (U1's entry) carries this
> discrepancy so U2 — who owns the test list — can confirm or correct.

| # | ADR 0006 / M3 pin | How M3 uses it |
|---|---|---|
| **C-M3·1** (M3-owned) | A `PostReply` has **no separate `Audience` field**; its visibility is **exactly** the parent `Post`'s `Read` decision. No second `IAuthorizationService` call for a reply; the reply is *not evaluated* (and emits no `AccessAudit` row) when the parent is denied. | Reply-visibility inherits the parent's `Read` (the C6 core, `MatchGroups`, called **once** on the post and then *applied* to the reply list). A reply list is always a *subset* of a visible parent. This is what keeps the "two `CanSeeAsync` calls" bug out of the detail render — and is the reason the detail page is one decision row, not two. |
| **C-M3·2** (M3-owned) | A `Component` is a **candidate filter** (a feed organizer — which list does this post land in?), **never** an access decision. `GetComponentsAsync(enabledOnly)` returns a *candidate set*; its output never renders without passing the post's own `Read`. The component filter emits **no `AccessAudit` row** of its own (it is a precondition, not a decision). | The composer's component picker, the feed grouping, the `/community/{id}` route: all read `Component` documents via the frozen `IUserInfoService` — never via `CanSeeAsync`. A post in "Safety" is visible **exactly** per its own `Audience`; no "Safety component is open" rule exists. M3 tests assert the absence of an audit row for the candidate query (F9). |
| **C-M3·3** (M3-owned) | **Audit shape:** feed render = **one aggregate row** (`targetKind = "post"`, `visibleCount` / `hiddenCount`, same `IDocumentSession` overload M1 used for single + bulk). Detail render = **one decision row** (Allow *or* Deny) per post. Reply visibility inherits the parent decision (C-M3·1) — **no separate reply row in either shape**. All rows commit in the *same transaction* as their render (or, for create / reply writes, in the same transaction as the domain write). | M3's two render shapes are *frozen*: a feed page does not fan out into one row per post; a detail page does not emit a row per reply. The `IDocumentSession` overloads (the frozen no-session methods retain their own-commit semantics, per M1's §E lane) are the *only* way M3's reads and writes get their `AccessAudit` rows in-transaction. |
| **C1** (ADR 0006) | Empty audience denies (both `Any` and `All`; explicit guard). | `Post.Audience` is **non-null** (an M3 document-level rule: it is a required field, not optional). Explicit `Any` + empty grant, or `All` + empty grant ⇒ `CanSeeAsync` returns deny for everyone *except* the author (C1's owner branch is the *only* exception — F4). A draft the author is composing also sees itself via that same owner branch. |
| **C2** (ADR 0006) | Delegation is action-scoped. | A delegate with `Read` in scope sees the author's post on any surface that renders it (feed, detail). A delegate without `Read` in scope sees nothing of it — the post is hidden, no partial fields, no "hidden" placeholder. `Via = Delegation` records the acting identity in the `AccessAudit` row (carried through M1's `AccessVia` vocabulary). |
| **C3** (ADR 0006) | Audit always on — Allow *and* Deny. | Per C-M3·3: a feed render commits one aggregate Allow+Deny row in the same transaction; a detail render commits one decision row (Allow *or* Deny); a `Post` / `PostReply` create commits the domain write and its `AccessAudit` (Allow) in the same transaction. A reply create on a denied parent is **not executed at all** (C-M3·1 is the guard). |
| **C4** (ADR 0006) | Strong-consistency membership resolution against live documents. | A membership change (add a group member, add a resident to `Post.Audience`) takes effect on the **very next** render — no projection in the access path. M3's e2e pins: post created in group G while viewer V is not in G (V sees nothing); add V to G; V's next render shows the post (F5). |
| **C5** (ADR 0006) | Moderator default-OFF on audience-restricted content. | **M3's hold:** M3 exercises **no** `Moderate`-on-post call. A moderator (any scope) cannot peek at a post the author has not shared with them — `CanSeeAsync` denies the moderator exactly as it denies any other non-audience viewer, and the `AccessAudit` row records Deny. `Via = Report` stays dormant (M3b). M3 tests assert the absence (F3 / F8). |
| **C6** (ADR 0006) | One matching pass (`MatchGroups`) shared by `CanAsync` and `CanSeeAsync`. | The feed (bulk), the detail (single), and the reply-visibility (inherited from the parent's single `Read` decision — C-M3·1) all reduce to the **same** `MatchGroups` core; they cannot drift. A post visible to a group member in the feed is visible on the detail page, and a reply under it is visible on the detail, in one pass. |
| **ADR 0001-B** | Author's choice is absolute. | The composer writes the chosen `Audience` verbatim into `Post.Audience`. M3 does **not** auto-add group grants, does **not** second-guess a "Safety only" choice with a "but the author is friendly" branch, and does **not** inject a community-wide audience. The C1 owner branch is the only "extra" the author gets. |
| **ADR 0004 §B.1** | Marten-native document registration (POCOs, conventional `string Id`), delta-detected, idempotent, no seeding. | M3's three documents are registered in **`Kumunita.Core/Bootstrap/M3DocTypes.Configure(StoreOptions)`** — a new parallel surface analogous to `M1DocTypes`. **No seeding** (the four components are already seeded by M1's `FirstBootSeeder`; M3 consumes them, does not re-seed). Delta-detected + idempotent is inherited from `ApplyAllConfiguredChangesToDatabaseAsync`. *Open veto:* the plan notes U1 may flip to a hand-rolled `FeatureSchemaBase` subclass before U2 pins §2.2 — this doc records the choice; U2's pin decides. |

**ADRs M3 must respect (not pinned as invariants, but the code must not
violate):**

- **ADR 0003 §Separation of duties** — M3's post create/reply surface is
  *owner-scoped to the author* (a resident posts in their name; a delegate
  posts with `Via = Delegation`). M3 **does not** add a moderator scope, does
  **not** add a GlobalAdmin surface for posts, and leaves the M3b report /
  resolve flow to M3b. A `GlobalAdmin` reading a post in M3 has the **same**
  `Read` standing as any resident (C5).
- **ADR 0006-D** (dependency direction) — `PostService` (Core) depends only on
  `IUserInfoService` + `IAuthorizationService` + Marten; it never reads
  `GroupMembership` / `DelegationGrant` for its own access decisions, and the
  `Kumunita.Web` layer is a thin controller (mirroring M2's
  `DirectoryController` shape).
- **ADR 0006-E** (change management) —
  `IUserInfoService.GetComponentsAsync(bool enabledOnly)` is the **single
  M3 ADD**, named in the doc-comment as a compatible-lane addition (the ADR's
  *named here* list grows by exactly one line in M3's close — U11, U12).

## FACES (pinned, 10)

Each row is a *resident-visible outcome* (or a *moderator-absence* outcome)
that the M3 seam tests (Part 2 §2.5) and the M3 e2e (U9's unit) must cover.
"Pinned" means the invariant in the right column is the single authority for
the outcome; the test names Part 2 pins must reference that pin by id.

| # | Outcome (what a resident sees / can do) | Pinned by |
|---|---|---|
| F1 | Component feed (`/community/{id}`) shows **only** the posts the viewer may read; the aggregate row reports the hidden count; hidden fields do not render (no hidden-author name, no hidden title, no hidden body). | C6 (one matching pass: feed bulk ≡ detail single) + C3 (aggregate row) + C-M3·3 (feed shape) |
| F2 | A non-member (a viewer outside the `Post.Audience`) sees **no** trace of a post in the feed — no title, no author, no "hidden" placeholder; the hidden count in the aggregate row is the only evidence the post exists. | C6 + C1 (in-audience deny) + C-M3·2 (the component is not the gate that decides "I can't see anything") |
| F3 | Empty-audience post (explicit `Any` + zero grants, or `All` + empty) denies from **everyone**, moderators included; a `GlobalAdmin` is **not** auto-allowed. | C1 + C5 (moderator default-OFF, via M1's `Via = Admin` / `Moderator` vocabulary that M3 does *not* invoke) |
| F4 | Empty-audience post: the **author** (or their `Read`-scoped delegate) still sees it via the owner branch; the owner-branch `AccessAudit` row records `Via = Owner`. | C1's owner branch (the single exception to C1) |
| F5 | A group member added to a post's audience **after** the post is created sees it on the **very next** render — no refresh, no stale projection. | C4 (strong-consistency, live documents) |
| F6 | A delegate with `Read` in scope sees the author's post in feed and detail; the `AccessAudit` row records `Via = Delegation` with the **acting** identity (the delegate, not the author). | C2 (delegation action-scoped) + C3 (audit via row) |
| F7 | A delegate without `Read` in scope sees **nothing** of the author's post — feed hides it, detail denies it; the `AccessAudit` row records Deny with `Via = Delegation`. | C2 (out-of-scope is Deny) + C3 (Deny audited) |
| F8 | `Component` is a **feed organizer, not a gate**: a post in the "Safety" feed is visible **exactly** per its own `Audience`; no moderator "peek" is available on a component page; the `/community/{id}` route does not add a moderator bypass. | C-M3·2 (component filter is a candidate set, not an access rule) + C5 (no moderator exception) |
| F9 | The component candidate-filter query (`GetComponentsAsync`, `/community/{id}` grouping) emits **no** `AccessAudit` row — the audit trail contains only the feed's aggregate row and the detail's decision row; a resident's audit query on their post returns the row(s) for their post / replies, never a "component candidate query" row. | C-M3·2 (precondition, not decision) + C-M3·3 (row shape fixed to feed/detail) |
| F10 | A reply under a post is visible **iff** the parent post is visible on the detail render; a reply under a denied parent is **not evaluated** (no `AccessAudit` row for the reply, no separate `CanSeeAsync` call, no "hidden" reply in the UI). One level only — no nested reply. | C-M3·1 (reply inherits parent's single `Read`) + C-M3·3 (audit shape = one decision row for the post, not a row per reply) |

**FACES count: 10.** This count (and the invariant-pin per row) is the input
the next unit (U2) needs to name the seam-test list and the acceptance gate
without re-deriving them.

## Drift-guard & change policy (Part 1)

- If a later unit (U3–U12) finds a mismatch between an implemented signature
  and the pin in this Part, **this doc wins**. The unit updates this file in
  the same commit and appends a one-line drift note to
  `docs/plans-milestones/m3-handoff-notes.md`.
- The invariant *numbers* — ADR 0006's **C1, C2, C3, C4, C5, C6**; the three
  M3-owned **C-M3·1, C-M3·2, C-M3·3**; ADR **0001-B**; ADR **0004 §B.1** — are
  stable for the rest of M3. Adding a new M3-owned invariant (C-M3·4+)
  requires an ADR amendment plus a design-doc edit in the same commit;
  renaming or renumbering an existing one is a breaking change and is not
  allowed mid-M3.
- A new FACES row (F11+) is added only by a unit that ships the outcome it
  pins, in the same commit as the feature. The FACES count is a **handoff
  field** (U1 → U2, and forward): every unit that touches FACES updates the
  count in the handoff note.
- **The plan § U1 "12 invariants" headline** vs. the 11-item body list is a
  plan-documentation slip, not a pinned-invariant drift. The handoff note
  (U1's entry) records it so U2 — who owns the test list — confirms 11
  against the body and pins test names accordingly. U2 owns the final count
  in §2.6.
- The "## M3 — Closed (recorded)" section at the end of this file is a
  **placeholder**. U10 (the gate record) and U11 / U12 (the close) will
  append the final entry; until then that section is empty and must not be
  interpreted as closed.
- **Seam/test names are not pinned in Part 1.** Every test name and the
  three-test acceptance gate (closed-loop / handoff / part-vs-whole) land in
  Part 2 (U2) at §2.5 / §2.6 — mirroring M2's structure. The FACES table
  above is the *input* to Part 2; Part 2's test names are the *output*.

## Seams & contracts (Part 2, written by U2)

### 2.0 Preambles — what this section pins, and what wins on conflict

Every C# fragment below is **exact**: parameter lists, return types, and
namespaces are the contract U3–U11 must implement against. If a later unit
discovers an implemented signature that does not exist verbatim here, the
drift-guard (§2.7) applies: **this file wins**; the unit updates this file in
the same commit and appends a drift note to
`docs/plans-milestones/m3-handoff-notes.md`.

Namespace conventions:

- Frozen / new-Core seams: `Kumunita.Core.Authorization` (M1 surface,
  unchanged by M3) and `Kumunita.Core.UserInfo` (module-owned; M3's **one**
  ADD is on this surface).
- M3's new bounded context: `Kumunita.Core.Posts`
  (`Post`, `PostReply`, `Report`, `PostToAuditableResource`, `PostService`,
  `FeedResult`, `PostDetailResult`, `PostDraft`).
- Web-side composition: `Kumunita.Web.Controllers` / `Kumunita.Web.Models`
  (never in `Kumunita.Core`).

**Count reconciliation (U1 → U2):** U1's body pins **11 invariants** (the
plan's "12" headline is a documentation slip recorded in U1's handoff). U2
confirms 11 and pins the §2.5 test list accordingly — 18 named tests, each
anchored to an invariant id and/or FACES row from Part 1. No C-M3·4 is
introduced by this section.

### 2.1 Frozen seam list (exact C#)

Seams that exist as of M1/M2. M3 *calls* them; M3 does not modify.

`Kumunita.Core.Authorization.IAuthorizationService` (frozen, ADR 0006 §A):

```csharp
public interface IAuthorizationService
{
    Task<Decision>   CanAsync(string actorId, AccessAction action,
                              IAuditableResource target);
    Task<Decision>   CanAsync(string actorId, AccessAction action,
                              IAuditableResource target,
                              Marten.IDocumentSession session);
    Task<VisibleSet> CanSeeAsync(string actorId, AccessAction action,
                                  IEnumerable<IAuditableResource> candidates);
    Task<VisibleSet> CanSeeAsync(string actorId, AccessAction action,
                                  IEnumerable<IAuditableResource> candidates,
                                  Marten.IDocumentSession session);
}
```

`Kumunita.Core.Authorization` frozen types (as they stand in M1; quoted
verbatim from `Audience.cs`, `Decision.cs`, `AccessAction.cs`):

```csharp
public enum AudienceMode { Any, All }
public enum GrantKind    { User, Group }
public sealed record AudienceGrant(GrantKind Kind, string Id);

public sealed class Audience
{
    public AudienceMode Mode { get; set; } = AudienceMode.Any;
    public System.Collections.Generic.List<AudienceGrant> Grants { get; set; } = new();
    public Audience();
    public Audience(AudienceMode mode,
                    System.Collections.Generic.IReadOnlyList<AudienceGrant> grants);
    public bool IsEmpty => Grants.Count == 0;
}

public enum AccessVia     { Owner, Audience, Delegation, Moderator,
                            Report, BreakGlass, Admin }
public enum AccessOutcome { Allow, Deny }

public sealed record Decision(bool Allowed, AccessVia Via,
                              string EffectivePrincipalId);

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
    Audience? Audience    { get; }    // `null` = not audience-restricted
    string?   ComponentId { get; }
    string    TargetKind  { get; }
}
```

> **M3 exercises `Read` only.** `Moderate` is reserved for M3b (C5 — OFF by
> default; M3 tests assert the *absence*, `F3_FeedDeniesModeratorOnAudiencePost`
> / `F8_ComponentIsFilterNotAccessGate`, F3/F8).

`Kumunita.Core.UserInfo.IUserInfoService` — M3's frozen M1/M2 surface plus
M3's **one** ADD (ADR 0006-E compatible lane, mirroring M2's
`GetProfilesAsync(bool)`):

```csharp
public interface IUserInfoService
{
    // ── M1 frozen surface (unchanged by M3) ──
    Task<Profile?> GetProfileAsync(string subjectId);
    Task<HashSet<string>> GetGroupIdsAsync(string userId);
    Task<DelegationGrant?> GetActiveGrantAsync(string delegateId);
    Task<Group> CreateGroupAsync(string ownerId, string name, string? description);
    Task AddGroupMemberAsync(string groupId, string userId, string addedBy);
    Task RemoveGroupMemberAsync(string groupId, string userId, string removedBy);
    Task<DelegationGrant> GrantDelegationAsync(string ownerId, string delegateId,
                              System.Collections.Generic.IReadOnlyList<string> scope,
                              DateTimeOffset from, DateTimeOffset? to);
    Task RevokeDelegationAsync(string grantId, string revokedBy);
    Task UpsertProfileAsync(Profile profile, ProfileUpdate patch);
    Task<System.Collections.Generic.IReadOnlyList<Component>> SeedComponentsAsync();
    Task SetComponentModeratorAccessAsync(string componentId, bool on, string actorId);
    Task<System.Collections.Generic.IReadOnlyList<ModeratorAssignment>> GetAssignmentsAsync(string userId);

    // ── M2 ADD (frozen for M3; reused by the composer's audience selector) ──
    Task<System.Collections.Generic.IReadOnlyList<Profile>> GetProfilesAsync(bool verifiedOnly);
    Task<System.Collections.Generic.IReadOnlyList<Group>> GetGroupsForUserAsync(string userId);
    Task<System.Collections.Generic.IReadOnlyList<GroupMembership>> GetGroupMembersAsync(string groupId);

    // ── M3 ADD — the single M3 new method on a frozen interface
    //    (ADR 0006-E compatible lane, precedent: M2 `GetProfilesAsync`, U3) ──

    /// <summary>
    /// The composer's *component picker* / the <c>/community/{id}</c>
    /// *grouping* / the feed's *candidate filter* (M3 design §2.3). A
    /// *candidate set*, NOT a visible set (C-M3·2): the caller must pass every
    /// post through <c>IAuthorizationService</c> before rendering, and this
    /// read produces **no** <c>Authorization.AccessAudit</c> row itself
    /// (C-M3·2; pinned by the §2.4 seam test
    /// <c>F9_CandidateFilterEmitsNoAuditRow</c> at the service level and by
    /// U4's <c>UserInfoServiceTests.GetComponentsAsync_CandidateFilterEmitsNoAuditRow</c>
    /// at the unit level — same C-M3·2 pin, two test files). Strong-consistency
    /// live rows (C4): a component enable/disable flip in the same commit is
    /// live on the very next call.
    /// </summary>
    Task<System.Collections.Generic.IReadOnlyList<Component>> GetComponentsAsync(bool enabledOnly);
}
```

> **Drift note (U2):** U1's handoff pinned the ADD's doc-comment as
> "returns a *candidate* set; C-M3·2 says never a visible set; C-M3·2 says no
> own `AccessAudit` row; C-M3·3 says the row shape is fixed to feed aggregate
> + detail decision" — the pinned prose, not a verbatim C# body, and that is
> honored here (candidate-set language, no-audit-row, C-M3·2/3 references) and
> the shape frozen.

### 2.2 New M3-owned Core types (exact C#)

Namespace `Kumunita.Core.Posts`. All three documents are **Marten-native**
POCOs with the conventional `string Id` identity (ADR 0004 §B.1; the carve-out
for a hand-rolled `FeatureSchemaBase` is *not* used in M3 — that pin is
reserved for operator-written tables like `AdminOverride`, mirroring
`M1DocTypes`).

```csharp
namespace Kumunita.Core.Posts;

/// <summary>
/// A post (M3). `Audience` is **non-null** (invariant C1 — empty audience
/// denies; the author's bootstrap default is an *empty* audience, so the owner
/// branch is the *only* lane that lets the author see their own draft).
/// `ComponentId` is a **feed organizer**, never an access boundary (C-M3·2).
/// </summary>
public sealed class Post
{
    public string Id { get; set; } = string.Empty;
    public string ComponentId { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string Body { get; set; } = string.Empty;
    public Authorization.Audience Audience { get; set; } = null!;
    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Modified { get; set; }
    // No Status — the hidden/removed surface is M3b (the M3b deferral close,
    // §Scope "Out of scope — M3b deferral"): M3's post has no Status column.
}

/// <summary>
/// A one-level reply to a post (M3). **No `Audience` field** (invariant
/// C-M3·1): a reply's visibility inherits its parent post's single `Read`
/// decision — there is no second authorization evaluation for the reply and
/// the reply produces **no** `Authorization.AccessAudit` row of its own.
/// </summary>
public sealed class PostReply
{
    public string Id { get; set; } = string.Empty;
    public string PostId { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset Created { get; set; }
}

/// <summary>
/// A *dormant* report row (M3b workflow). The **table** is registered in M3
/// for forward compatibility (the Q1↔Q3 resolution: the table in M3, the
/// flow in M3b); M3's surface ships **no** workflow, **no** tests, and **no**
/// `Status` writes against it. `Status` is nullable until M3b lands a write
/// lane that sets it.
/// </summary>
public sealed class Report
{
    public string Id { get; set; } = string.Empty;
    public string PostId { get; set; } = string.Empty;
    public string ReporterId { get; set; } = string.Empty;
    public string? ComponentId { get; set; }
    public string? Reason { get; set; }
    public string? Status { get; set; }   // null until M3b's write lane sets it
    public DateTimeOffset At { get; set; }
}

/// <summary>
/// The single `IAuthorizableResource` projection of a `Post` (M3 U4's
/// composition unit — the *only* path through which a post reaches
/// `IAuthorizationService`, ADR 0006-D). Frozen 6-member shape (mirrors M2's
/// `ProfileToAuditableResource` pin), `TargetKind = "post"` for the aggregate
/// audit rows (C-M3·3).
/// </summary>
public sealed class PostToAuditableResource : Authorization.IAuditableResource
{
    public Post Post { get; }
    public PostToAuditableResource(Post post) { Post = post; }

    public string Id           => Post.Id;
    public string Name         => Post.Title ?? (Post.Body.Length > 60 ? Post.Body[..60] : Post.Body);
    public string? OwnerId     => Post.AuthorId;
    public Authorization.Audience? Audience => Post.Audience;   // non-null by invariant C1
    public string? ComponentId => Post.ComponentId;
    public string TargetKind   => "post";
}

/// <summary>
/// The list/read composition service for M3 posts (bounded context
/// `Kumunita.Core.Posts`, ADR 0006-D lane). Composes **only** the two
/// frozen modules — `IUserInfoService` (candidate set + single-row read) and
/// `IAuthorizationService` (the single decision path) — plus its own
/// `IDocumentStore`; it never reads `GroupMembership` / `DelegationGrant`
/// for its own access decisions (the same "feature modules never re-derive
/// access" boundary that pins M1/M2). Owns M3's two product rules: the §2.3
/// candidate filter (invariant C-M3·2) and the §2.4 reply-inherits rule
/// (invariant C-M3·1).
/// </summary>
public sealed class PostService
{
    public PostService(UserInfo.IUserInfoService userInfo,
                       Authorization.IAuthorizationService authz,
                       Marten.IDocumentStore store);

    /// <summary>
    /// The §2.3 candidate filter applied *here* (C-M3·2 names this service as
    /// the filter's owner): `GetComponentsAsync(true)` → the candidate posts
    /// for <paramref name="componentId"/> → one `CanSeeAsync(..., "read")`
    /// over the survivor set. `HiddenCount` counts only the candidates
    /// `CanSeeAsync` actually evaluated. One aggregate `AccessAudit` row
    /// (`TargetKind = "post"`), the C-M3·3 pin.
    /// </summary>
    Task<FeedResult> ListFeedAsync(string componentId, string actorId, int page);

    /// <summary>
    /// A post + its one-level replies, the replies evaluated **only** under
    /// the parent's `Read` decision (C-M3·1): parent Deny ⇒ reply *not
    /// evaluated*, no reply of its own produces an audit row, and the
    /// aggregate row for the *post* is a **single decision row** (not an
    /// aggregate), per C-M3·3.
    /// </summary>
    Task<PostDetailResult> GetPostAsync(string postId, string actorId);

    /// <summary>
    /// Creates a post in the caller's in-flight session (the C3 same-transaction
    /// lane, ADR 0006-E `IDocumentSession` overload). The author's chosen
    /// `Audience` is written **verbatim** (ADR 0001-B); `AuthorId = actorId`,
    /// `ComponentId = draft.ComponentId`.
    /// </summary>
    Task<Post> CreatePostAsync(PostDraft draft, string actorId,
                               Marten.IDocumentSession session);

    /// <summary>
    /// Creates a one-level reply in the caller's session (C3). No `Audience`
    /// (C-M3·1): the parent's `Read` decision has already been made by the
    /// caller — this method does not re-check.
    /// </summary>
    Task<PostReply> CreateReplyAsync(string postId, string actorId,
                                     string body, Marten.IDocumentSession session);
}

/// <summary>`ListFeedAsync`'s result — the C-M3·3 aggregate shape.</summary>
public sealed record FeedResult(
    System.Collections.Generic.IReadOnlyList<Post> Visible,
    int HiddenCount,
    int Page,
    int Total);

/// <summary>`GetPostAsync`'s result — the C-M3·3 single-decision-row shape,
/// with the one-level reply list evaluated under the parent's decision.</summary>
public sealed record PostDetailResult(Post Post,
                                      System.Collections.Generic.IReadOnlyList<PostReply> Replies);

/// <summary>The `CreatePostAsync` input — `Audience` is non-null (C1).</summary>
public sealed record PostDraft(string ComponentId, string? Title,
                               string Body, Authorization.Audience Audience);
```

**M3 document registration surface** (U3 lands this, mirroring `M1DocTypes`):

```csharp
namespace Kumunita.Core;

/// M3's Marten-native document registration surface (ADR 0004 §B.1). Mirrors
/// `M1DocTypes` exactly: all three POCOs use the conventional `string Id`
/// identity and need no `Identity(...)` or `UniqueIndex(...)` call — Marten's
/// defaults apply.
public static class M3DocTypes
{
    public static void Configure(Marten.StoreOptions opts)
    {
        opts.Schema.For<Posts.Post>();
        opts.Schema.For<Posts.PostReply>();
        opts.Schema.For<Posts.Report>();
    }
}
```

Wired by U3 into both boot paths:
`Kumunita.Core/Bootstrap/SchemaBootstrap.cs` (called from
`ApplyAllConfiguredChangesToDatabaseAsync`) and `Kumunita.Web/Program.cs`
(the dev-loop + all-env paths, adjacent to `M1DocTypes.Configure`).

### 2.3 Candidate-filter + reply-inherits rule (M3-owned invariants)

**Candidate-filter rule (C-M3·2) — the §4.3 analog for M3.** The *component
feed's product query* is fixed here; it is a **product rule**, not an
`AccessAudit` subject.

| Caller state | Candidate set / outcome (before `CanSeeAsync`) |
|---|---|
| Unauthenticated (no principal) | **401 at the Web layer** (U7's `PostsController` `[Authorize]`). Core never sees an empty actor — the Core service requires a non-empty `actorId`. No candidate set is loaded, no audit row. |
| Authenticated, verified, component **missing or disabled** | **404 at the Web layer** (`GetComponentsAsync(enabledOnly: true)` returns the component set; the component is absent). No candidate posts are loaded, no audit row. |
| Authenticated, verified, present enabled component | `PostListComponent(componentId)` — *candidate posts* for that component (never a visible set; C-M3·2). The candidate posts are then passed to **one** `CanSeeAsync("read")` over the adapter projections — one aggregate `AccessAudit` row (C-M3·3). |
| Authenticated, unverified, present enabled component | Same verified row — the *candidate filter* is the component's posts, not the viewer's verification state (M3's §2.3 differs from M2's here on purpose: M2's §2.3 is a *viewer-side* filter because the candidate set is "every profile"; M3's candidate set is "this component's posts", whose audience is the *author's* choice and the viewer's standing is evaluated by `CanSeeAsync`). |
| Moderator standing on this component | **Same candidate set as verified**, and the same `CanSeeAsync` run — the *moderator* branch, if one ever applies, is *inside* `CanAsync` / `CanSeeAsync` (M1), never in the candidate filter. Today (C5) no moderator branch on a post is exercised; §2.5's `F3_FeedDeniesModeratorOnAudiencePost` asserts the absence. |

The filter is **never** logged as an `AccessAudit` row — a violation is
C-M3·2; §2.5's `F9_CandidateFilterEmitsNoAuditRow` pins it.

**Reply-inherits rule (C-M3·1) — the 4-shape table.** Given a parent post
`p` and a reply `r`, the reply is **rendered iff** `p`'s `Read` decision is
`Allow` for the viewer. The reply is *not* independently evaluated.

| Shape | What happens to the reply |
|---|---|
| Parent Allow (via Owner / Audience / Delegation) | Reply **rendered** — no separate `AccessAudit` row for the reply (C-M3·1 + C-M3·3: the row for the visit is the parent's single-decision row). |
| Parent Deny | Reply **not evaluated** (short-circuits at the parent). No *reply* row at all; the parent's row is `Deny`. |
| Empty-audience parent, viewer == author | Parent Allow via Owner branch (C1 exception); reply rendered as in row 1. |
| Empty-audience parent, viewer ≠ author, or `Mode == All && Grants.Count == 0` (explicit "deny everyone") | Parent Deny ⇒ reply **not** rendered, not evaluated — row 2. The *explicit* `All + empty` case (F3) has the same outcome as the *implicit* empty case. |

**All four rows reduce to the parent's *one* `Read` decision (C6):** there is
exactly one decision call for a visit (feed: `CanSeeAsync`; detail:
`CanAsync`), and the reply's visibility follows from *that* decision's
`Allowed` flag. A second `Can*` call on the reply is a C-M3·1 violation and §2.5's
`F10_ReplyNotEvaluatedOnParentDeny` / `F10_ReplyVisibleIffParentVisible` pin its
absence.

### 2.4 Pinned seam tests (exact names)

File: `tests/Kumunita.Core.Tests/PostServiceTests.cs`. All 18 names below are
**pinned** by this section (U9 is responsible for the file per the plan
register; U5 / U6 / U7 may add their own *named* tests to the same file but
must not rename these). Each name carries the FACES row and/or invariant
anchor from Part 1:

1. `F1_FeedVisibleToAudienceMember`            — F1, C-M3·3 (aggregate row).
2. `F2_FeedHiddenFromNonMember`                 — F2, C-M3·3, C1.
3. `F3_FeedDeniesModeratorOnAudiencePost`       — F3, C5 (absence), C1.
4. `F4_EmptyAudiencePostAuthorSeesOwnDraft`     — F4, C1 (owner-branch exception).
5. `F4_EmptyAudiencePostDeniesNonAuthor`        — F4, C1.
6. `F5_MembershipChangeReScopesNextRequest`     — F5, C4 (strong consistency).
7. `F6_DelegateWithReadInScopeSeesAuthorPost`   — F6, C2 (delegation scoped).
8. `F7_DelegateWithoutReadDenies`               — F7, C2.
9. `F8_ComponentIsFilterNotAccessGate`          — F8, C-M3·2, C5 (absence).
10. `F9_CandidateFilterEmitsNoAuditRow`          — F9, C-M3·2.
11. `F10_ReplyVisibleIffParentVisible`           — F10, C-M3·1.
12. `F10_ReplyNotEvaluatedOnParentDeny`          — F10, C-M3·1, C3 (no own row).
13. `Feed_AggregateAuditRowShape`                — C-M3·3 (`VisibleCount`/`HiddenCount`
    set, `Action="read"`, `TargetKind="post"`).
14. `Detail_DecisionAuditRowShape_ViaOwner`      — C-M3·3, C1 (single-row, `Via=Owner`).
15. `Detail_DecisionAuditRowShape_ViaAudience`   — C-M3·3 (single-row, `Via=Audience`).
16. `Detail_DecisionAuditRowShape_ViaDelegation` — C-M3·3, C2 (single-row, `Via=Delegation`).
17. `AuthorAudienceWrittenVerbatim`              — ADR 0001-B (the composer's choice
    is absolute; the DB row's `Audience` is bit-identical to the draft's).
18. `PostService_MakesNoModerateCall`            — C5 (absence; `Moderate` is
    never invoked on a post in M3; the call is asserted to be `Read` only).

> **Note to U6 → U9 (unit numbers from the plan register):** U6 lands
> `FeedResult` + `PostDetailResult` + `PostDraft` + the 4 public methods +
> the C-M3·1 reply-inherits rule + the C-M3·2 candidate filter. U9 owns the
> file `tests/Kumunita.Core.Tests/PostServiceTests.cs` and its 18 `[Fact]`s
> (the plan names U9 "seam tests only (the 18 pinned names)"); U10 records
> the three-test gate. §2.6's gate names reference this list — **renaming
> any of the 18 after U9 owns them is a drift event** (§2.7).

### 2.5 Acceptance gate (U10 records)

The three-test shape (mirroring M2 §2.6 verbatim; M3's *handoff* lane is the
audience's author→group-recipient arrow, not a second `Can*` call):

| # | Test | Shape (M3's reading of the M2 lane) |
|---|------|-------------------------------------|
| 1 | **Closed-loop** | Author creates a post → it appears in their own feed on the next request; the aggregate `AccessAudit` row for the feed visit is present with `VisibleCount ≥ 1` and `TargetKind = "post"` (C-M3·3, F1). |
| 2 | **Handoff** | A group member is **added after the post was created** and sees the post on the **next** feed render — strong consistency (C4, F5); the *delegate* branch is the "handoff to a delegate" case for the same pin (F6, C2). |
| 3 | **Part-vs-whole** | The 18-test list in §2.4 is the **whole**; the closed-loop + handoff tests are the **parts**; all three — plus the M1-inherited (C1–C6) and M2-inherited (C-M2·1..3) anchors re-run unchanged — must pass together for M3's gate to record. U10's record cites the *actual* landed test names (drift lane: if a U2-pinned test name did not land verbatim, U10 renames the *test file / `[Fact]`* in the same commit and records a one-line drift in the handoff note — mirroring M2 §2.5's own handling). |

**The gate is recorded by U10** (per the plan register: U9 is "seam tests
only"; U10 is "run + record the M3 acceptance gate"). U11 (the
`ARCHITECTURE.md` flip + M3→M3b deferral note) and U12 (the final
`## M3 — Closed (recorded)` append) are the close units, the M2 U12 / U15
analogs re-indexed to M3.

### 2.6 Drift-guard (frozen once written)

The following pins are **frozen** in this doc, and any violation is a `## U<m>
— Drift pause` per unit-series rule §6 (unit-series rule in the plan):

- The **11** invariants from Part 1 (C-M3·1/2/3, ADR 0006 C1–C6, ADR 0001-B,
  ADR 0003 §SoD, ADR 0004 §B.1, ADR 0006-D, ADR 0006-E) — the *numbers*
  (not the prose) are stable for the rest of M3; rename / renumber is a
  breaking change (Part 1 already pins this; it repeats here because
  Part 2's own §2.5 / §2.4 names hang off them).
- The **10** FACES rows (F1–F10) in Part 1 — FACES *count* is a handoff field
  (U1 → U2, and forward); a new FACES row (F11+) is added **only** by the
  unit that ships the outcome it pins, in the same commit as the feature
  (Part 1 already pins this).
- **`IUserInfoService`** frozen surface (§2.1) plus M3's **one** ADD
  (`GetComponentsAsync(bool enabledOnly)`) — both signature and "candidate
  set, no audit row, C-M3·2" doc-comment are **frozen** once U4 lands them
  (U4 owns the `IUserInfoService` ADD per the plan), mirroring M2's
  `GetProfilesAsync(bool)` pin. Adding, removing, or re-scoping the ADD is a
  drift event **before** U4; a drift event **after** too (this file wins).
- The **`Post`** 8-field shape (Id, ComponentId, AuthorId, Title?, Body,
  Audience, Created, Modified?) and the **absence of `Status`** on a post —
  frozen once U3 lands `Post`. A `Status` column is a **M3b drift event** if
  it appears in M3.
- The **`PostReply`** 5-field shape (Id, PostId, AuthorId, Body, Created) and
  the **absence of `Audience`** — frozen once U3 lands `PostReply`.
- The **`Report`** 7-field shape (Id, PostId, ReporterId, ComponentId?,
  Reason?, Status?, At) and the **`Status?` nullable, M3b-owned** pin —
  frozen once U3 lands `Report`.
- The **`PostToAuditableResource`** 6-member shape (Id, Name, OwnerId,
  Audience, ComponentId, TargetKind) and the **`TargetKind = "post"`** pin —
  frozen once U5 lands the adapter (U5 is "PostToAuditableResource adapter" in
  the plan register; mirrors M2's `ProfileToAuditableResource` pin).
- The **`PostService`** ctor `(IUserInfoService, IAuthorizationService,
  IDocumentStore)` and the 4 public methods (`ListFeedAsync`,
  `GetPostAsync`, `CreatePostAsync`, `CreateReplyAsync`) + their 3 records
  (`FeedResult`, `PostDetailResult`, `PostDraft`) — the **frozen Core
  surface**, named + signatures + record shapes, frozen once U6 (the defining
  unit, per the plan register) lands them. Renaming a public method or
  reshaping a record is a drift event (U6 owns the shapes; U7 / U8 consume
  them, no re-shaping; U9 tests against them).
- The **§2.3** two tables (the 5-row candidate-filter table + the 4-shape
  reply-inherits table) — frozen once written (U2's commit); a new row is a
  drift event **or** a new FACES row (F11+), whichever the unit's change
  actually pins, in the same commit + the same drift note.
- The **18 test names** in §2.4 — frozen once U9 owns the file (the unit
  named in §2.4 "Note to U6 → U9"). Renaming or re-scoping a name is a
  drift event; the unit updates §2.4 in the same commit and appends a drift
  note to the handoff.

> **U2 records, here, the `12 vs 11` plan-documentation slip as closed:**
> the 11 invariants (not 12) are the pin, confirmed against U1's body.
> The 18 test names (not a smaller or larger set) are the pin. The 2 tables
> (5 + 4 rows) are the pin. The 11 + 10 + 18 + 5 + 4 (and the 3 records,
> 4 methods, 6-member adapter, 3 POCOs) are the *frozen counts* of Part 2.

### Run result (M3 acceptance gate — 2026-09-04)

Command: VS Test Explorer `run_tests` (filter `Project=Kumunita.Core.Tests`,
`Project=Kumunita.Web.Tests`).
Testcontainers `postgres:18`; `PostgresFixture` fresh scratch DB per class.
M2's U11 precedent still applies: CLI `dotnet test` returns exit-code 5 "Zero
tests ran" in this workspace, the VS Test Explorer is the working runner.

**`Kumunita.Core.Tests` 105/105 passed, 0 failed** (18 M3-pinned
`PostServiceTests` per U9 + 87 inherited M1/M2 — `AuthorizationServiceTests`,
`ClaimShapingInvariantBTests`, `AdminOverrideDdlTests`, `KumunitaFeatureDdlTests`,
`DbBootstrapIsPristineTests`, `SideEffectHarnessTests`, `DirectoryServiceTests`,
`DirectoryServiceTests_U6`, `ProfileToAuditableResourceTests`, `UserInfoServiceTests`,
`UserInfoServiceGroupsU9Tests`). **`Kumunita.Web.Tests` 37/37 passed, 0 failed**
(`HealthControllerTests`, `HomeControllerTests`, `DirectoryIndexViewModelTests`,
`DirectoryDetailViewModelTests`, `ProfileEditViewModelTests`, `GroupsViewModelTests`,
`GroupsDetailViewModelTests`, `MilestonesTests`, `RepositoryInfoTests`).
**Total: 142/142 passed, 0 failed.** No reds.

Record (shape mirrored from M2 — `#` | `Test` | `Evidence (actual test names)`):

| # | Test | Evidence (actual test names — all passed) |
|---|------|-------------------------------------------|
| 1 | **Closed-loop** (author creates a post → it appears in their own feed on the next request; the aggregate `AccessAudit` row for the feed visit is present with `VisibleCount ≥ 1` and `TargetKind = "post"`) | `PostServiceTests.F1_FeedVisibleToAudienceMember` (F1, C6, C3 — the audience member's feed contains the post and the decision row is `Allowed`; the author's own feed is the closed-loop instance of the same `F1` pin, where `Owner` is the `Via` the feed's `CanSeeAsync` resolves), `PostServiceTests.F4_EmptyAudiencePostAuthorSeesOwnDraft` (F4, C1 — the owner-branch exception when `Audience` is `Any+empty`: the author's feed still contains the draft; the non-author's does not), `PostServiceTests.Feed_AggregateAuditRowShape` (C-M3·3 — the feed's *single* `AccessAudit` row has `TargetKind = "post"`, `Action = "read"`, `VisibleCount`/`HiddenCount` set as pinned; the closed-loop's `VisibleCount ≥ 1` is the observable in this test). |
| 2 | **Handoff** (a group member is added **after the post was created** and sees the post on the **next** feed render — strong consistency (C4, F5); the *delegate* branch is the "handoff to a delegate" case for the same pin (F6, C2)) | `PostServiceTests.F5_MembershipChangeReScopesNextRequest` (F5, C4 — a `GroupMembership` row added *after* `CreatePostAsync` re-scopes the *next* `ListFeedAsync` call: the new member's feed now contains the post on the subsequent render; M1's `AuthorizationService` is unchanged — M3's pin *is* that the next call sees the live row; no caching in M3), `PostServiceTests.F6_DelegateWithReadInScopeSeesAuthorPost` (F6, C2 — the *delegate* branch: a `DelegationGrant` with `Read` in scope lets the delegate see the owner's post on the feed; the same *strong-consistency* lane as F5, through `CanSeeAsync`'s delegate pass), `PostServiceTests.F7_DelegateWithoutReadDenies` (F7, C2 — the boundary: a delegate without `Read` in scope sees *nothing*; the handoff is *action-scoped*, not blanket). |
| 3 | **Part-vs-whole** (the 18-test list is the **whole**; the closed-loop + handoff tests are the **parts**; all three — plus the M1-inherited (C1–C6) and M2-inherited (C-M2·1..3) anchors re-run unchanged — must pass together) | U9's 18 pinned `[Fact]`s in `tests/Kumunita.Core.Tests/PostServiceTests.cs` (`F1_FeedVisibleToAudienceMember` · `F2_FeedHiddenFromNonMember` · `F3_FeedDeniesModeratorOnAudiencePost` · `F4_EmptyAudiencePostAuthorSeesOwnDraft` · `F4_EmptyAudiencePostDeniesNonAuthor` · `F5_MembershipChangeReScopesNextRequest` · `F6_DelegateWithReadInScopeSeesAuthorPost` · `F7_DelegateWithoutReadDenies` · `F8_ComponentIsFilterNotAccessGate` · `F9_CandidateFilterEmitsNoAuditRow` · `F10_ReplyVisibleIffParentVisible` · `F10_ReplyNotEvaluatedOnParentDeny` · `Feed_AggregateAuditRowShape` · `Detail_DecisionAuditRowShape_ViaOwner` · `Detail_DecisionAuditRowShape_ViaAudience` · `Detail_DecisionAuditRowShape_ViaDelegation` · `AuthorAudienceWrittenVerbatim` · `PostService_MakesNoModerateCall`) — all 18 green — **plus** the M1-inherited anchors (`AuthorizationServiceTests`, `ClaimShapingInvariantBTests`, `AdminOverrideDdlTests`, `KumunitaFeatureDdlTests`, `DbBootstrapIsPristineTests`, `SideEffectHarnessTests`) and the M2-inherited anchors (`DirectoryServiceTests`, `DirectoryServiceTests_U6`, `ProfileToAuditableResourceTests`, `UserInfoServiceTests`, `UserInfoServiceGroupsU9Tests`) — all re-run unchanged in the same execution, still passing. **`Kumunita.Core.Tests` 105/105 passed, 0 failed** in this run. |

**E2E status.** The Playwright scaffolding (M2's U13 spec +
`tests/Kumunita.Web.Tests/package.json` + `playwright.config.ts`) is
**present** in the repo and the M2 spec is **enumerable** (`npx playwright
test --list` reports 3 M2 specs at `e2e-m2.spec.ts:156/202/258`). Two
observations block M3's e2e from running in this unit:

1. **No `e2e-m3.spec.ts` exists** in `tests/Kumunita.Web.Tests/`. The M3
   plan register does not name any unit that authors the M3 e2e spec;
   U9's handoff exit says "no e2e authored (U11)", but U11's own spec is
   the `ARCHITECTURE.md` flip + `## Summary` handoff and does not author
   an e2e either — that attribution is a **plan-documentation slip**,
   recorded here per U1's unit-series convention (flag slips in the
   handoff, body/spec authoritative; not a §2.6 drift-pause since no
   frozen pin is touched). **No M3 unit lands the Playwright runtime
   implementation** (`kumunita.signup / login / lastCreatedGroupId`
   helpers) the M2 spec — and thus any M3 spec — would need.
2. **M2's D2 deviation** (`m2-directory-profiles-groups.md` § Deviation
   register — "the `kumunita` fixture is a documented *throw*, pending an
   M3-landed runtime") is **still open** in this workspace; U10's check
   (`npx playwright test --list` enumerates cleanly but executing the
   spec would fail on the `throw`) confirms the runtime has not been
   landed by any M3 unit.

Per U10's fallback path (the runtime is **not present**, and U10's
Deliverables are the design doc only — "No code, no build"), the e2e is
therefore **neither authored nor run** in this unit, and the gap is
recorded here + in the handoff note. **The gate is evidenced at the
seam-test layer** by U9's 18 `PostServiceTests`: per §2.5 the three
gate tests (closed-loop row 1 / handoff row 2 / part-vs-whole row 3) are
*defined by* the 18 (rows 1 and 2 are the F1/F4/F5/F6/F7 + `Feed_...`
pins; row 3 is the 18 itself). **This is not a gate fail** — 18/18 pin
+ 142/142 inherited + M1/M2 anchors re-run unchanged in this unit, 0
failed.

The *unit that lands* the Playwright runtime (`kumunita` fixture
implementation + M3-authored `e2e-m3.spec.ts`) — a **future milestone**
(M3b or M4, whichever owns the moderation surface the runtime would end
to end) — records the M3 e2e pass count in a subsequent `### Run result
(M3 e2e — <date>)` section of this doc.

**Drift status.** No `## U<m> — Drift pause` sections exist in the handoff note. The plan-documentation slips recorded by U1, U3, U4, and U9 (invariant count 12 vs 11, `IUserInfoService` method count 15 vs 16, `site.js` `JSON.stringify` vs `FormData`, F3/F8 C5-reading interpretation) are all flagged as **not §2.6 drift-pauses** and **do not affect the frozen pins**; they are carried into U11's close for the M3b-reconcile pass. No still-open §2.6 drift.

---

## M3 — Closed (recorded)

M3 is closed. The three-test gate is recorded above in § `Run result (M3
acceptance gate — 2026-09-04)` (all PASS: closed-loop · handoff ·
part-vs-whole, `Kumunita.Core.Tests` 105/105 + `Kumunita.Web.Tests` 37/37 =
142/142, 0 failed, M1 + M2 anchors re-run unchanged). The `ARCHITECTURE.md`
§2 `Posts/` line is flipped to **M3 ✓ live** with the gate summary (the
`Moderation/` line is re-marked **M3b — not yet created**; the `Events/` and
`Projects/` lines are untouched). **This design-doc close is the *record*
close; the sole M3→M3b *handoff* artifact is `docs/plans-milestones/
m3-handoff-notes.md` § `## Summary` (U11's close)** — read that section first
when starting M3b's U1 (it holds the 11-unit table + the reconciled count
drift + the 6-item deferral list in a single artifact).

### M3b deferral list (each named, each with a one-line M3b candidate)

The Part 1 "Out of scope — M3b deferral" block is M3's M1-style "out-of-scope"
close; the six items below are the reconciled, named list U11's `## Summary`
carries forward — the same six, in the same order, with the next-owner cue
each one carries. M3b's U1 (the design-doc author for the `Moderation`
module) reads this list on entry; the *sole* M3→M3b handoff artifact (U11's
`## Summary`) holds the table + the reconciled count drift + the same six
items.

1. **Report workflow — file / assign / unlock / resolve.** The `Report`
   *table* is registered (U3, 7 fields, `Status` nullable, no index, no
   surface, no tests); the *workflow* (the `FileReportAsync` /
   `AssignReportAsync` / `UnlockAsync` / `ResolveReportAsync` command surface
   + the assignment UI + the resolve-UI) is M3b's. **M3b candidate:** the
   M3b U1 design-doc unit for a new `Kumunita.Core/Moderation/` module that
   lands the C5 carve-out (moderator default-OFF at the component's
   `ModeratorAccess` flag — M1 branch #2, the pin F3/F8's "absence" tests
   name).
2. **The `Via = Report` read branch on a post** (a moderator sees a
   previously-invisible post *through* a filed report). **M3b candidate:**
   the M3b U1 design-doc unit — a read-lane pin on a (new)
   `IReportService.CanReadWithReportAsync`-shaped seam, or a direct branch
   on `AuthorizationService.Decide` (the thinner lane is decided in that
   unit; ADR 0006-E compatible-lane applies, mirroring the M3 lane U4 just
   landed).
3. **Moderator surfaces — the moderator queue, the resolve UI, the "assign
   to a moderator" form.** `/admin` (M1's admin surface) is **unchanged** in
   M3 (the ADR 0003-SoD pin names this). **M3b candidate:** a new
   `/moderation` surface — its own controller + Razor views; the queue is a
   read over `Report` ordered `At` desc + `Status` desc; the resolve form is
   a `Report` write lane that sets `Status`.
4. **The post `Status` field (hidden / removed) and the M3b removal path.**
   M3's `Post` POCO has **no** `Status` column (U3 registered the field
   *absent*, not nullable-not-set). **M3b candidate:** the M3b design-doc
   unit owns the `Post.Status` enum (`active` / `hidden` / `removed`) + the
   two write lanes (`HidePostAsync` / `RemovePostAsync`) + the C5 `Moderate`
   action id it will exercise (the pin U1's C5 carve-out says M3b owns).
5. **The reply `POST /posts/{id}/replies` route** — U7's 4-route set
   (3 GET + `POST /posts/new`) does not include it; U8's `Detail.cshtml`
   links to it and it currently 404s. The Core write lane
   (`PostService.CreateReplyAsync`, U6) is present and is the *only* write
   seam (C3's single-write pin) — a ~10-line action closes the route (no new
   seam, no new seam-test name, the design-doc §2.2 `CreateReplyAsync` pin
   already exists). **M3b candidate:** M3b's U1 registers it as a small
   "close-the-loop" micro-unit *before* the `Moderation` design doc (or
   the M3b U1 design-doc unit owns it inline — the register's U9
   "the 18-test file" ordering in M3 does not pre-empt this, so M3b decides).
6. **The E2E spec (`e2e-m3.spec.ts`).** The Playwright scaffolding is
   present-and-enumerable but no M3 unit authors the spec; M2's D2
   deviation (`kumunita` fixture is a documented *throw*) is still open.
   **M3b candidate:** whichever future milestone lands the runtime (M3b or
   M4, whichever owns the surface the runtime ends-to-end) authors + runs
   the spec and records the pass count in a subsequent `### Run result (M3
   e2e — <date>)` section of this doc (above this close, not replacing it).

---

*M3 is closed. M3b opens from U11's `## Summary` in the handoff note —
that section is the sole M3→M3b handoff artifact (the 11-unit table + the
reconciled count drift + the six items above, in one place).*
