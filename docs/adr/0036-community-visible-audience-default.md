# ADR 0036 — Community-visible audience: the default for new community posts

Status: Accepted
Date: 2026-09-17
Amends: none (additive). Builds on the frozen base of **0001-B**
(audience written **verbatim** by the author), **0006** (the decision
order, the empty-audience-denies invariant C1, the `CanAsync` /
`CanSeeAsync` frozen signatures), and **0012** (the
`GetCommunityIdsAsync` read seam — explicit `ComponentMembership` rows ∪
enabled ∩ mandatory components).

## Context

The post composer's bootstrap default audience was **owner-only**: a new
post with no grants was visible to its author and no one else (the
empty-audience-denies invariant, ADR 0006-C1, applied to an empty grant
list). That was the right bootstrap *default* at M1 (a self-only shape),
but it is the **wrong default** for the *post* lane. In a single-
neighborhood community platform, the overwhelming majority of posts a
resident writes are intended for their community — "the new bread recipe"
or "the road repair is on Tuesday". Requiring the poster to
individually pick a set of users or groups for the *common* case, when
they usually just want "everyone in my community", inverts the
granularity: the picker is the *coarse* control forced on for the
*fine* default.

The audience model (ADR 0001-B) already has the right primitives: an
`Audience` is a grant list + a combine mode, and a post's audience is
written **verbatim** by the author. What it lacked was a **first-class
"all members of the target component" grant** — a way to say "my
community" without enumerating its members (which is both impossible at
write time — the membership changes over time — and the wrong shape —
audience grants are *specific* user/group ids, not a set). Adding
"all community members" as a *mode of the grant list* (a synthetic
grant) would collide with the C1 invariant (empty-list semantics) and
with ADR 0006's "the grant list is the explicit set" contract. It needed
to be a **distinct grant**, independent of the list.

## Decision

- **`Audience.Community` is a new first-class flag on the `Audience`
  document, a *distinct grant* — not a mode of the `Grants` list.**
  `public bool Community { get; set; }` (default `false`, so existing
  posts keep their owner-only behavior verbatim — ADR 0001-B's verbatim-
  write contract is untouched; the flag simply round-trips). A
  `true` flag with an **empty** `Grants` list is the "all community
  members" shape. The flag and the grant list are **independent**: a post
  can be community-visible *and* carry explicit user/group grants (the
  union is the visible set). It is *not* an `AudienceMode` value — the
  mode (Any / All) still governs how the *grant list* combines, exactly
  as before.

- **The empty-audience-denies invariant (C1) does not apply to the
  Community branch.** C1 is about the *grant list* being empty (vacuous
  truth over `All` mode would make an empty-`All` resource world-
  readable). The Community flag is a *separate* grant that is checked by
  its own branch, so a `Community: true` + empty-grants post is **not**
  world-readable — it is visible to exactly the target component's
  members (a *smaller* set than the world), and denied to everyone
  else. The C1 guard in `EvaluateAudience` (the public static test seam)
  is therefore deliberately **unchanged**: it only looks at the grant
  list, and the Community branch is a *different* branch it never sees.

- **`Decide()` gains a 4th branch (owner → moderation → break-glass →
  **community** → public → grant-match → deny), before the public /
  grant-match branches.** It fires when **all three** hold:
  1. `target.Audience` is non-null **and** `target.Audience.Community`
     is `true`;
  2. `target.ComponentId` is **non-null** (the branch is inert for a
     component-agnostic target — see group posts below);
  3. the actor's live `communityIds` contain `target.ComponentId`.
  On a match it allows via `AccessVia.Community` (or
  `AccessVia.Delegation` when acting under an in-scope grant). It is
  actor-scoped (the *actor's* own membership, like break-glass /
  moderation), not effective-principal-scoped — a delegate does not
  inherit the delegator's community standing for this branch.

- **`AccessVia.Community` is a new additive enum value**, the 10th in
  `AccessVia` (after `Owner` / `Audience` / `Delegation` / `Moderator` /
  `Report` / `BreakGlass` / `Admin` / `Group` / `Guardian`), following
  the M1 `Admin` / ADR 0013 `Group` / ADR 0028 `Guardian` **append**
  precedent: an additive value, the nine frozen values untouched. It is
  the audit tag for "this decision allowed because the actor is a
  member of the target community and the post is community-visible" —
  the "who did this, by what right" query the audit log exists to answer
  now has a distinct answer for the common case.

- **The community set is loaded per `AuthorizationModule` call** (same
  D4 / C4 contract as the group set):
  `IUserInfoService.GetCommunityIdsAsync(actorId)` → the live
  `IReadOnlyCollection<string>` (explicit `ComponentMembership` rows ∪
  enabled ∩ mandatory components, ADR 0012), materialized into a
  `HashSet<string>` (`StringComparer.Ordinal`) on the `ActorContext`
  alongside the existing `GroupIds`. The read seam and the write lanes
  (`SetCommunityMembershipAsync` / `ClearCommunityMembershipAsync`) are
  **unchanged** — this ADR only adds a *reader* of that seam on the
  authorization path.

- **Group posts are structurally unaffected.** A group post is written
  `ComponentId = string.Empty` (G·2 lane exclusivity) and
  `Audience = new Audience()` (G·8 — non-null, **empty**, `Community`
  defaults `false`). The Community branch requires **both** a `true`
  flag **and** a non-null `ComponentId`, so a group post — which has the
  flag `false` *and* an empty component id — never reaches it. The group
  lane's single-decision standing (ADR 0013 / ADR 0035 membership lane)
  is untouched. (The branch is also inert for any hypothetical
  component-agnostic target with `Community: true` and a null/empty
  `ComponentId` — the flag alone does nothing without a component.)

- **The post composer's default is now "all community members".** The
  Web `New` action seeds the reusable `AudienceEditorModel` with
  `CommunityVisible = true` (the flag), `Mode = "Any"`, `Grants = "[]"`
  (the "all community members" shape) — so a no-change submit writes a
  community-visible post. The **edit** lane round-trips the flag verbatim
  (`FromAudience` reads `audience.Community` → `CommunityVisible`;
  `BuildAudience` writes it back), so an edit save never silently flips a
  community-visible post to owner-only.

- **The form flag is `AudienceEditorModel.CommunityVisible` (a simple
  `bool` checkbox).** It is **not** a second audience object — the
  single-source pin (U11 / F13) holds: `Grants` is still the only form-
  bound *grants* field, `BuildAudience()` is still the only
  deserialization site, and `CommunityVisible` is a *scalar* flag the
  editor carries verbatim onto the document's `Community`. A simple bool
  model-binds from a checkbox's absence (`false`) and presence (the
  posted value); an unchecked box is a well-formed shape, never a
  malformed post.

- **The UI is checkbox-first, granular-by-default-off.** The post
  create/edit forms lead with an "Everyone in this community" switch
  (checked by default on create, seeded from the stored flag on edit).
  The granular user/group picker (the `_GrantPickers` partial, the mode
  radios, the combine hint) sits in a block that is **hidden by default**
  and revealed when the poster opts into a narrower audience. A small
  inline script toggles the block's visibility with the checkbox. The
  hidden `Grants` textarea remains the *only* form-bound grant field (no
  `name=` added to the picker checkboxes) — the single-source pin is
  visually enforced, not just structurally.

## Consequences

- **The common case is a no-effort default.** A resident who just wants
  "everyone in my community" gets it by default; they only touch the
  picker when they want *narrower* (a subset) or *wider* (an explicit
  grant list on top) visibility. This is a UX simplification with no
  authorization-model change beyond the new branch.

- **Existing posts are unchanged** (the flag defaults `false`; the
  verbatim-write contract is untouched). A post written before this ADR
  is still owner-only unless its author (or, on the edit lane, an
  authorized editor) explicitly opts it into community visibility. No
  data migration.

- **The grant list and the community flag compose.** A post can be
  community-visible *and* carry explicit user/group grants; the visible
  set is the union (a community member *or* an explicitly granted user).
  This is the "wider than the community" case, and it is the author's
  verbatim choice (ADR 0001-B).

- **The C1 invariant's *scope* is now explicit.** C1 guards the *grant
  list* (and the `All`-mode vacuous-truth trap). The Community branch is
  a *separate* grant that C1 does not govern. The public static
  `EvaluateAudience` test seam is deliberately **unchanged** and still
  pins C1 for the grant list; the new Community branch lives only in the
  private `Decide()` and is exercised by the DB-backed
  `AuthorizationServiceTests` (the `A0036_*` family) — the two surfaces
  cannot drift because the branch is structurally invisible to the seam.

- **Audit granularity improves.** The `via` column on the audit row can
  now say `Community` for the common case, instead of conflating it with
  `Owner` (wrong — the author isn't the viewer) or `Audience` (wrong —
  there is no grant list). The "who did this, by what right" query the
  log exists to answer now has the correct, distinct answer.

- **The authorization read path now loads *two* membership sets** (group +
  community) per call. Both use the existing strong-consistency read
  seams (C4); the added cost is one extra `GetCommunityIdsAsync` per
  `AuthorizationModule` call — the same shape as the group load, so no new
  consistency class is introduced.

- **Inert on component-agnostic targets.** Any target with a null/empty
  `ComponentId` (group posts, and any future component-agnostic resource)
  never reaches the branch, so the flag is a no-op there. This is the
  safe default: a stray `Community: true` on a component-agnostic target
  cannot accidentally broaden visibility.

## Tests

- **Core (DB-backed, `AuthorizationServiceTests`)** — the `A0036_*`
  family pins the branch end-to-end against the live `Decide()` path:
  - `A0036_CommunityFlagAndMember_Allows_ViaCommunity` — flag on, member
    of the component → **Allow**, `via Community` (not `Audience` — the
    empty grant list would have denied; the branch short-circuits first).
  - `A0036_CommunityFlagButNotMember_Denies` — flag on, **not** a member
    → **Deny** (grant list empty → no fallback).
  - `A0036_CommunityFlagButNullComponent_Inert` — flag on, `ComponentId`
    null → **Deny** (the branch is inert without a component).
  - `A0036_CommunityFlagFalse_EmptyGrants_OwnerOnly` — the pre-ADR-0036
    shape (flag off, empty grants): a member who isn't the owner is
    denied, the owner is allowed via `Owner` (the old behavior, pinned).
  - `A0036_CommunityFlagPlusExplicitGrant_BothVisible` — flag on **and**
    an explicit user grant: the community member sees it via `Community`,
    the granted non-member sees it via `Audience` (the two grants
    compose).
- **Web (pure, `AudienceEditorModelCommunityTests`)** — the form-bound
  `CommunityVisible` ↔ document `Community` round-trip, both directions:
  - `BuildAudience_CommunityVisible_RoundTripsVerbatim` — checked /
    unchecked both carry verbatim onto `Audience.Community`, with
    `Mode` + `Grants` untouched.
  - `BuildAudience_CommunityVisible_WithGrants_BothPreserved` — the flag
    and the grant list are independent; both survive.
  - `FromAudience_Community_RoundTripsVerbatim` — the edit-lane read
    direction seeds `CommunityVisible` from `Audience.Community`; the
    full build → from → build round-trip is stable (guards against a
    regression that silently flips a community-visible post to
    owner-only on the next edit save).
