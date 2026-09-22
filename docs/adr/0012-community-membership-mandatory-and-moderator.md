# ADR 0012 — Community membership: mandatory communities + moderator-managed optional membership

Status: Accepted
Date: 2026-09-11
Amends: M3 (the "posts in components" lane — component membership gains the
mandatory/optional axis and a moderator write surface); ADR 0004 §B (the new
`Component.Mandatory` column rides the existing `M1DocTypes` migration
surface, no new schema surface); ADR 0006-E (all four new seams are additive)

## Context

Components ("communities") were introduced in M3 as the *audience unit* for
posts: the per-account `ComponentMembership` rows (the "posting right") had a
single admin lane on `/admin` (check/uncheck a set per account). Three product
gaps were settled by this request:

1. **Some communities must be universal.** A neighborhood always has the one
   or two places where *everyone* belongs (the main board, public
   notices). Today that requires an administrator to hand-add every verified
   resident to the explicit rows — and every sign-up after. What was wanted:
   mark a community **mandatory** and it is instantly *and* permanently
   "everyone is a member", with no one able to be removed from it or leave
   it. Product decision on who may make that call: the toggle is the
   **GlobalAdmin's** — the community's own moderator manages its *members*,
   but whether the community is mandatory at all is an admin-level call,
   not delegated down.
2. **Optional communities need their owner's managing hand.** For ordinary
   communities the *moderator* (the `moderator:{id}` scope claim of ADR 0003)
   should add and remove members — today only the GlobalAdmin can, via the
   `/admin` per-account set form.
3. **Members should be able to leave.** A membership someone else granted is
   not a one-way ticket; a non-moderator member of an *optional* community
   can remove themselves (the exact shape of ADR 0008's group self-leave).

The invariants to keep: the thin-token rule (decision in Core, HTTP shape in
Web — ADR 0006-D); audit-by-default (every state change of restricted
content is logged, SECURITY.md); strong consistency C4 (a membership change
is live on the very next gate read — no projection, no cache); the frozen
`/admin` surface stays usable (ADR 0006-A).

## Decision

Mandatory is a property of the **community**, not of each membership row —
one flag, one definition of membership, one gate.

### Core (`Kumunita.Core.UserInfo`)

- **`Component.Mandatory`** (added to `M1DocTypes`; default `false`).
  ADR 0004 §B unchanged otherwise.
- **The single "who is a member" definition** — `GetCommunityIdsAsync(userId)`
  returns the explicit `ComponentMembership` rows **∪ (Enabled ∧ Mandatory)**
  components. Disabled∧mandatory grants nothing (a disabled component is
  down, full stop). The posting gate, the composer's picker, the feed, and
  the `/admin` index all read through this one seam, so mandatory-ness is
  C4-live on the very next read. No new read path, no new projection.
- **`SetCommunityMandatoryAsync(componentId, mandatory, actorId, actorRoles)`**
   flips the flag — **GlobalAdmin-only** (product decision above; the
   community's own moderator gets `UnauthorizedAccessException`). Audits
   `community.set-mandatory` (on) /
   `community.set-optional` (off), via `Via: Admin`, same transaction (C3).
- **`AddCommunityMemberAsync(componentId, userId, actorId, actorRoles)`** —
  the same idempotent upsert as the frozen admin lane; a row on a mandatory
  community is a harmless no-op (the union read already includes them; kept
  so forms round-trip without special cases). Audits
  `community.add-member` (C3).
- **`RemoveCommunityMemberAsync(componentId, userId, actorId, actorRoles)`** —
   deletes the row; no-op when there is no row (the set-form shape).
   **Refuses on a mandatory community** — `InvalidOperationException`, the
   message the Web lane surfaces — membership there is implicit and cannot be
   *removed* (only the flag can be turned off — and that is the admin's
   toggle). Audits `community.remove-member` (C3).
- **Standing gates** (thin token, fail-closed) — two lanes, deliberately
   different (the product decision above):
   - `SetCommunityMandatoryAsync` is **GlobalAdmin-only**; a scoped
     community moderator (or anyone else) gets
     `UnauthorizedAccessException`, and the audit row is always `Via: Admin`.
   - `AddCommunityMemberAsync` / `RemoveCommunityMemberAsync` admit the
     actor's role set carrying the `moderator:{componentId}` scope claim
     **∪ GlobalAdmin** — `UnauthorizedAccessException` otherwise. The audit
     row records the **narrower** standing: `Via: Moderator` when the claim
     is present, else `Via: Admin`.
- **`GetCommunityMembersAsync(componentId)`** — the manage-page member list and
   add-picker: explicit rows only (a mandatory community's members are *all*
   residents, which no list enumerates), candidate read, no audit — the
   `GetGroupMembersAsync` analog on the component axis.
- **The frozen admin lane `ClearCommunityMembershipAsync` is amended, not
  replaced**: on a mandatory pair it is a no-op skip (no delete, no audit —
  nothing changed). The `/admin` per-account set form must keep working on
  any account, and the self-leave route below reuses the seam; the refusal
  with a *surfed message* lives on the dedicated lane
  (`RemoveCommunityMemberAsync`), the skip lives on the frozen one. No
  break on the ADR 0006-A surface.

### Web (`Kumunita.Web`)

- **New `CommunityController`** on the community feed — the standing's own
  surface, ADR 0003-style per-component:
  - `GET /community/manage/{id}` — mandatory state (the toggle form is
    offered to the **GlobalAdmin only**; a community moderator sees member
    management without it), member
    list (per-row remove, hidden on mandatory — nothing to hide behind the
    "Everyone" badge), add-picker of non-member verified profiles.
  - `POST /community/manage/{id}/mandatory` (the bool form field — this lane
    is GlobalAdmin-gated, the moderator gets the same fail-closed 404),
    `POST /community/manage/{id}/add`, `POST /community/manage/{id}/remove`
    (the `UserId` form field). `manage` is a literal path segment (not the
    `{id}` slot) so it can't collide with the `{id}`-shaped component routes.
  - `POST /community/{id}/leave` — **the self-lane, ADR 0008 verbatim on the
    component axis**: no target anywhere, the actor's own subject is both
    `userId` and `actorId` on the frozen
    `ClearCommunityMembershipAsync` — self by construction. The Web route
    pre-checks the flag (mandatory → refusal message + redirect to the
    community; the Core skip is the second line). Audit reads
    `community.remove-member, ActorId: <the member>, Via: Admin` — the
    ADR 0008 convention (no third `Via`, no new verb for the self lane).
  - **The gate's failure shape is 404, not 403** (ADR 0008 U10 / the posts
    lane): no standing on this community's surface → `NotFound()`. The
    service's `UnauthorizedAccessException` maps to the same shape on the
    write lanes (defense in depth — the claims are the token, the Core gate
    is the rule).
- **The feed** (`Posts/Index` single-community view): an "Everyone" badge on
  mandatory communities, a Manage-members link for the standing, and the
  Leave form for a member who is neither standing nor implicit (the button
  tree mirrors exactly what the routes would accept — ADR 0008's
  presentation rule).
- **`/admin`**: the communities table shows the mandatory badge backed by a
  **GlobalAdmin-only toggle** — the same `SetCommunityMandatoryAsync` lane the
  community's own manage page uses, so the standing rule is unchanged (the
  admin's decision, never the moderator's); only the surface is duplicated,
  the manage page staying the primary one. The "add a community" form accepts
  the mandatory flag (create via the frozen `CreateCommunityAsync`, then the
  same `set-mandatory` lane flips it on — a second audited row, correct
  because it *is* a distinct action). The per-account set form still renders
  mandatory communities as *checked, disabled* rows, and its remove diff
  filters them out — the counts stay honest because `GetCommunityIdsAsync`
  still returns them (union read).

### Presentation of "mandatory" vs "members"

A mandatory community's membership is *implicit* — the manage page shows the
flag (and its toggle), not a list of all residents; `GetCommunityMembersAsync`
deliberately returns explicit rows only. The "You" badge on members' own rows
is view-only.

## Consequences

- The community's own moderator gains a managing surface on their community
   (add/remove members — but *not* the mandatory/optional toggle: that view
   is offered to the GlobalAdmin only, and the route 404s the moderator). Removing
   *themself* through the
   manage form is refused there (the message points at the feed's Leave
   button) — self-departure is the `/leave` lane, audited as the member
   acting, exactly like a non-moderator's; re-posting standing survives on
   the scope claim alone either way (there is no owner whose departure is a
   transfer question, because the mandatory-state call is the admin's).
  from membership by the standing; this is deliberate: standing is not
  built on membership, so removal never orphans anything (unlike the ADR
  0008 group-owner exception, which exists because the owner row *is* the
  anchor — communities have no such anchor).
- A mandatory community is genuinely universal while enabled: no per-account
  exceptions (a disabled account loses posting rights from that account, not
  from the community — membership is about the community, posting is about
  the account; `Mandatory` does not grant posting on a block), the
  self-leave route refuses, the moderator remove lane refuses with a message,
  the `/admin` set form can't remove. Turning it off is one toggle on the
  **GlobalAdmin** standing (the moderator cannot), audited
  (`community.set-optional`), and C4-live everywhere.
- Audit vocabulary gains four verbs (the two toggles + two manage-lane
  verbs), all `targetKind: component`, all C3, all Allow; the self-leave
  reuses `community.remove-member` (actor = the member). No new `Via` state.
- Existing deployments are untouched: `Mandatory` defaults to `false` under
  the `M1DocTypes` migration, and no existing row changes meaning.
- `SetCommunityEnabledAsync(false)` on a mandatory community still hides it
  from every reader (union = enabled∧mandatory) — a mandatory but *disabled*
  community grants nothing; re-enabling restores it implicitly.
- ADR 0008's "one recorded exception for the actor's own row" stays exactly
  that: this ADR adds a second such lane (components' self-leave), same
  construction, same audit reading, same 404-on-no-standing shape; the group
  side is untouched.
- The README Roadmap / `Milestones.cs` pair is not bumped: this lands inside
  M3's already-shipped "posts in components" lane (a membership-management
  extension, not a new milestone), and the sync contract only changes on a
  status flip.
