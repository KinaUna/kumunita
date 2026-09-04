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

## M3 — Closed (recorded)

> **Placeholder — this section is empty until U10 (gate record) and U11 / U12
> (close) have appended the final entry.** U1 (this unit) does not close M3;
> it opens the record. The three-test gate table, the `ARCHITECTURE.md` §2
> note update, and the M3b deferral list (the "Out of scope" block above,
> each with a one-line M3b candidate) will be appended here by U12 in its
> close.
