# ADR 0102 — "Everyone in this community" is exclusive + the "all communities" scope ⇒ all residents

Status: Accepted
Date: 2026-10-01
Amends: **0036** (refines its `Decide()` branch 4 — the "inert on a null/empty
`ComponentId`" consequence is **reversed**; the flag now implies
"all residents" in that shape). Builds on the frozen base of **0001-B**
(audience written **verbatim**), **0006** (the decision order, the
empty-audience-denies invariant C1, the `CanAsync` / `CanSeeAsync` frozen
signatures), **0041** (the `Audience.AllResidents` flag + the branch-4.5
"signed-in resident" standing this ADR reuses), and the ADR 0036
checkbox-first UI contract.

## Context

ADR 0036 made "Everyone in this community" the default audience for new
community posts: a first-class `Audience.Community` flag (a *distinct grant*,
not a mode of the grant list), a 4th `Decide()` branch, and a checkbox-first
UI in which the granular picker is hidden while the flag is on. Three
follow-ups sharpen that contract so the UI and the authorization agree:

1. **Exclusivity.** When "Everyone in this community" is on, the granular
   options (the Any/All mode radios and the "Who to grant to" picker) no
   longer apply — they are *additional* grants on top of an audience that
   already reaches everyone. ADR 0036's UI hid the block while the checkbox
   was on *on some* forms, but the Event-create and the whole Projects lane
   (todo create/edit, goal, project, board create/edit) rendered the mode
   radios + picker unconditionally. The contract should be uniform: **where
   the option is available, it is exclusive while on.**

2. **Default-on.** ADR 0036 seeded the *create* lane `CommunityVisible =
   true` but the *edit* lane's `Posts/Edit` form carried a `?? false`
   default. For the contract "it should default to enabled on every form
   where the option is available" to hold, the fallback default is `true`
   everywhere.

3. **The "all communities" scope.** Events and Projects forms let the author
   pick **"All communities"** (a `ComponentId` of `""`, normalized to `null`).
   ADR 0036's branch 4 was **inert** in that shape (it required a *non-null*
   `ComponentId`, so `Community: true` + null component fell through to the
   grant-list branch and denied a non-grantee). That made "Everyone in this
   community" + "all communities" a confusing no-op for a non-member
   resident. The intended meaning of *community-visible* + *all
   communities* is "visible to the whole neighborhood's signed-in
   residents" — the same standing ADR 0041 already grants via
   `Audience.AllResidents`. ADR 0102 makes the flag itself carry that
   meaning, so an Event/Project the author scoped to "all communities" and
   marked community-visible is seen by any signed-in resident (anonymous
   still denied — the public branch is the world-readable shape).

## Decision

- **`Decide()` branch 4 is reworked** (the owner → moderation → break-glass
  → **community** → public → grant-match → deny order is otherwise
  untouched). When `target.Audience.Community` is `true`:
  - **the target names a specific community** (a non-empty
    `ComponentId`): allow **iff** the actor's live `communityIds` contain
    that component — *unchanged from ADR 0036*;
  - **the target's community scope is "all communities"** (a null/empty
    `ComponentId`): allow **iff** the actor is signed in (a non-empty
    `actorId`) — the resident-only standing ADR 0041's branch 4.5 grants,
    now applied to the `Community` flag in this shape. Anonymous (an empty
    `actorId`) is denied (the public branch, 5, is the world-readable
    shape).
  On a match it allows via `AccessVia.Community` (or
  `AccessVia.Delegation` under an in-scope grant), as before. This is the
  **one** branch that changes; branch 4.5 (`AllResidents`) is untouched, and
  a `Community: true` + null-component target now resolves at branch 4
  (more specific) rather than falling to the grant list.

- **The empty-string component id is the "all communities" shape.** The
  check is `string.IsNullOrEmpty(target.ComponentId)`, not `is not null`,
  so both the null shape (the Projects controller's
  `IsNullOrWhiteSpace → null` normalization) and the empty-string shape
  (group posts store `ComponentId = string.Empty`) are covered. **Group
  posts are structurally unaffected**: a group post is written
  `Community = false` (ADR 0013 / ADR 0035 membership lane), so the reworked
  branch never fires for it; a stray `Community: true` on a group post
  would now broaden it to all signed-in residents — but no write lane sets
  that, so it is a no-op in practice.

- **The "Everyone" checkbox is the default on every form where the option
  is available.** `Posts/Edit`'s `?? false` fallback becomes `?? true`,
  matching the create lane and every other form (Event create/edit, the six
  Projects forms, and the calendar quick-create, which already posts
  `CommunityVisible = true`).

- **The granular block is hidden while "Everyone" is on, on every form
  where the option is available.** The seven forms that rendered the mode
  radios + `_GrantPickers` unconditionally (Event create; Projects
  todo-create/edit, goal, project, board-create/edit) now wrap that block
  in a `data-audience-panel` element — the exact ADR 0036 pattern already
  on Posts create/edit and Event edit — driven by the checkbox via the
  globally-loaded `client/lib/audience-toggle.js`. The panel's **initial**
  state keys off the checkbox state (`Model.Audience?.CommunityVisible ??
  true` ⇒ hidden), not the grant list, so a stored audience that carries
  *both* a grant list and `Community = true` still renders hidden while
  "Everyone" is on (the granular options are inapplicable). The hidden
  `Grants` textarea inside `_GrantPickers` remains the **only** form-bound
  grants field (the U11 / F13 single-source pin) regardless of visibility —
  hiding is via the Bootstrap `d-none` class (CSS), not `disabled`, so the
  form still posts a well-formed shape and `AudienceEditorModel.IsValid`
  still holds (the `required` mode radio keeps posting its value while
  hidden).

- **The `Audience.Community` doc-comment** is updated to record the new
  two-shape semantics (specific community ⇒ member; "all communities" ⇒
  any signed-in resident), pointing at this ADR.

## Consequences

- **"Everyone in this community" is a clean, exclusive, default-on control.**
  A resident who wants the whole neighborhood gets it by default; the
  granular picker only appears when they actively opt into a narrower
  audience. There is no longer a form where the option is on *and* the
  "How the picks combine" radios are visible and confusingly required.

- **The "all communities" + community-visible combination is now
  meaningful.** An Event or Project scoped to "all communities" and marked
  community-visible is visible to any signed-in resident (anonymous
  denied) — the ADR 0041 resident standing, reached through the `Community`
  flag rather than the separate `AllResidents` flag. The two flags now
  agree: a resident sees both shapes.

- **The ADR 0036 "inert on a null/empty ComponentId" consequence is
  reversed.** The old `A0036_CommunityFlagButNullComponent_Inert` test
  (deny) is replaced by three pins: a signed-in member (allow, `via
  Community`), a signed-in non-member (allow — membership is irrelevant in
  this shape), and an anonymous actor (deny). The member-of-a-specific
  community shape is **unchanged** (still `via Community` for a member, deny
  for a non-member), so the ADR 0036 pins that use a non-null component
  still hold.

- **Group posts are unaffected.** They are `Community: false` by
  construction; the reworked branch 4 only broadens visibility when the
  flag is `true`.

- **No schema / migration / seam change.** `Audience.Community` already
  exists (ADR 0036); only the branch-4 *interpretation* of a null/empty
  component changes, plus the form markup. `EvaluateAudience` (the public
  test seam) is deliberately untouched — the community branch is
  structurally invisible to it, as it was under ADR 0036.

- **Existing data is re-interpreted, not rewritten.** Any Event/Project
  already stored with `Community: true` + null/empty `ComponentId` that a
  signed-in non-member could not see before is now visible to them. That is
  the intended correction of the "all communities" scope; posts that named
  a specific community are unchanged.

## Tests

- **Core (DB-backed, `AuthorizationServiceTests`)** — the `A0036_*` family is
  updated to pin the new branch-4 two-shape behavior:
  - `A0036_CommunityFlagAndMember_Allows_ViaCommunity` — flag on, member of
    the named component → **Allow**, `via Community` (unchanged).
  - `A0036_CommunityFlagButNotMember_Denies` — flag on, **not** a member of
    the *named* component → **Deny** (unchanged — the specific-community
    shape is intact).
  - `A0036_CommunityFlagNullComponent_SignedInActor_Allows_ViaCommunity` —
    flag on, `ComponentId` null, signed-in → **Allow**, `via Community`
    (the ADR 0102 "all communities ⇒ all residents" shape; replaces the old
    `..._Inert` deny pin).
  - `A0036_CommunityFlagNullComponent_SignedInNonMember_Allows` — flag on,
    `ComponentId` null, a signed-in actor with **no** community membership →
    **Allow**, `via Community` (membership is irrelevant in this shape).
  - `A0036_CommunityFlagNullComponent_AnonymousActor_Denies` — flag on,
    `ComponentId` null, **anonymous** → **Deny** (the resident-only
    boundary; the public branch is the world-readable shape).
  - `A0036_CommunityFlagFalse_EmptyGrants_OwnerOnly` — the pre-ADR-0036
    shape (flag off, empty grants) → owner-only (unchanged).
  - `A0036_CommunityFlagPlusExplicitGrant_BothVisible` — flag on + an
    explicit grant → member via `Community`, grantee via `Audience`
    (unchanged).

- **The service-lane tests** (`EventServiceTests.M4_CommunityAudience*`,
  `PageServiceTests.PG_CommunityFlag*`, `ProjectServiceTests`) that pin the
  *specific-community* shape are unchanged — they all use a non-null
  `ComponentId` and a member/non-member split, which the reworked branch
  preserves verbatim.
