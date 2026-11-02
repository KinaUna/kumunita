---
id: arch-identity-access
title: "Identity & access"
tier: reference
kind: architecture
status: current
audiences: [developer, agent, platform-maintainer]
related:
  - docs/ARCHITECTURE.md
  - docs/adr/0001-core-stack-and-identity-model.md
  - docs/adr/0003-roles-and-moderator-scoping.md
  - docs/adr/0006-module-boundary-contracts.md
updated: 2026-11-02
---

# Identity & access

> **Abstract:** the access model — thin token / fat authorization, the `IUserInfoService` + `IAuthorizationService` seam, the access model (audiences, groups, delegation, moderator, audit), the decision algorithm (single + bulk, the `MatchGroups` core), and the role set (GlobalAdmin / Moderator / Member + the break-glass trust-management rules). **Who is it for:** a developer changing authorization behavior, or an agent tracing why a given actor may or may not see a given resource. **When to read the rest:** before any change to `IUserInfoService`, `IAuthorizationService`, the `Audience` doc, or the audit row shape.


## 4.1 Thin token, fat authorization

Authentication issues a small principal and nothing else:

    principal = { subjectId, isVerifiedResident, roles: [ "moderator:maintenance" ] }

"Can this person see that post?" is never a claim — it is resolved per request by the
AuthorizationModule. This keeps the cookie small and the identity story trivial, and lets
authorization rules grow without touching the token. It also keeps the later OpenIddict
swap mechanical (the cookie simply becomes an OIDC `sub`).

## 4.2 Services

    // UserInfoModule
    interface IUserInfoService {
      Task<Profile> GetProfileAsync(string subjectId);
      // Reads GroupMembership documents directly — strong consistency. Membership
      // changes take effect on the very next request, with no projection lag.
      Task<HashSet<string>> GetGroupIdsAsync(string userId);   // loaded once per request
      Task<DelegationGrant?> GetActiveGrantAsync(string delegateId);
      // Returns the delegate's active grant (if any): { ownerId, scope, from, to? }.
      // Noun note: "delegate" is the actor; "owner" is the principal they act as.
      // Create / Read / membership-management lanes, plus the group
      // description and privacy seams (ADR 0009 / ADR 0010):
      // SetDescription, SetPrivacy, GetPublicGroups.
    }

    // AuthorizationModule
    interface IAuthorizationService {
      // Single-target — detail views ("may I read this post?")
      Task<Decision> CanAsync(string actorId, Action action, IAuditableResource target);
      // Decision = { Allowed, Via, EffectivePrincipalId }
      // Bulk — list views (feeds, directory, boards): one group-load, one matching
      // pass over all candidates, one aggregate audit row (see [data model](data-model.md))
      Task<VisibleSet> CanSeeAsync(string actorId, Action action,
                                   IEnumerable<IAuditableResource> candidates);
      // VisibleSet = { visible: [ { id, via: Owner|Audience|Delegation } ], hiddenCount }
      // Group lane (ADR 0013, group posts milestone) — membership is the SOLE
      // decision: CanSeeGroupAsync(actorId, groupId, targetPostId?) for
      // detail/create-gate (one decision audit row) and
      // CanSeeGroupFeedAsync(actorId, groupId, candidateCount) for the feed
      // (one aggregate row). Via = Group, or Delegation when an in-scope
      // `read` grant acts with the owner's membership. There is NO moderator
      // and NO break-glass branch (G·4 — "nobody peeks", standing not
      // available), and the audience lane is never evaluated.
    }

## 4.3 Access model

- **Audience** = grants to users and/or groups; combine **Any** (union, default) | **All** (intersection).
- **Groups** = the reuse unit (ADR 0010: **private** groups are a membership/organizing unit, hidden from the audience pickers).
- **Delegation** = scoped acting; resolves an effective principal.
- **Moderator access** to audience-restricted content = **off by default**; a report grants
  the assigned moderator audited access to that item; an admin can enable standing
  visibility per scope.
- **Audit** of access decisions = always on.
- **Candidate filters are not authorization** (§4.4). A rule about *who is in the
  candidate set* (e.g. a post feed only includes published posts; a list only includes
  non-archived items) is a product query, applied before `CanSeeAsync`, and is never
  audited as an access decision.
- **Show-everyone directory.** The platform is invitation-only and limited to one
  neighborhood's residents: the directory lists every non-blocked resident to every
  signed-in viewer (no verified/unverified filter, no `Profile.Visibility` gate), and
  is a pure catalog read — no `CanSeeAsync`, no `AccessAudit` row. Only the detail
  (and preview) surfaces run the single `ContactVisibility` audience decision.

## 4.4 Decision algorithm

Shared by both entry points below is the group-matching core, written once so the two
paths cannot drift:

    MatchGroups(target, mode, groups, effPrin) =
        if target.audience is empty: return False             // deny-by-default
        match   = g => (g.kind == User  && g.id == effPrin)
                    || (g.kind == Group && groups.Contains(g.id))
        return (mode == Any) ? target.audience.grants.Any(match)
                             : target.audience.grants.All(match)

**Single target — `CanAsync`** (detail views):

    groups   = userInfo.GetGroupIdsAsync(actor)   // source docs (strong consistency),
                                                  // once per request, cached in-request
    grant    = activeGrant(actor)
    effPrin  = grant ? grant.ownerId : actor                     // delegation

    if target.authorId in { actor, effPrin }:                   // owner branch
        if actor == effPrin                                      -> Allow (Via: Owner)
        if action in grant.scope                                 -> Allow (Via: Delegation)
        // out-of-scope action as a delegate: fall through to Deny (audited)
    if action is moderation:
        if scope.moderatorAccess == On and actor moderates scope   -> Allow (Via: Moderator)
        if actor holds an active Report grant on target            -> Allow (Via: Report)
    if actor has a consumed, unexpired AdminOverride:             // break-glass (§4.5)
        -> Allow (Via: BreakGlass)
    allowed = MatchGroups(target, target.audience.mode, groups, effPrin)
              ? Allow (Via: Audience) : Deny

    append AccessAudit { actor, effPrin, action, target, via, outcome }   // always

**Bulk — `CanSeeAsync`** (feeds, directory, boards):

    same group-load + grant resolution, once
    for each candidate: owner branch, then moderation branches, then MatchGroups
    -> VisibleSet { visible: [ { id, via } ], hiddenCount }

    // audit (see [data model](data-model.md)): ONE aggregate row for the view, PLUS one row per visible
    // audience-restricted item; per-item denials are NOT logged

Both paths (single and bulk) share these invariants, enforced inside `MatchGroups` so
they cannot drift:
- **Empty audience denies.** In mode `All`, `grants.All(...)` over an empty grant list is
  vacuously true — without the empty check above, an `All`-mode resource with no grants
  would be readable by *everyone*. An empty audience always denies.
- **Delegation is action-scoped.** A delegated actor gets the owner's standing only for
  actions in the grant's `scope`; an out-of-scope action is a Deny (audited), even though
  the effective principal is the owner. `Via: Delegation` records the acting identity in
  the audit row.

Every decision on audience-restricted content is audited, Allow or Deny.

## 4.5 Roles

- **GlobalAdmin** — full control; manages moderators + their component scope; sets
  scope-level `moderatorAccess`; reads the audit log.
- **Moderator** — scoped to one or more functional components.
- **Member** — verified resident.

**GlobalAdmin trust management** (mitigates the single-admin concentration, ADR 0003):

- **Two admins as standing practice.** Promoting a second GlobalAdmin is normal, not an
  exception — each can demote the other, so no single account is a hard point of trust.
- **Break-glass elevation.** When the admin(s) are gone, locked out, or hostile, the
  *host operator* can grant a **time-limited, single-use, audited** GlobalAdmin
  elevation to an existing account via a direct DB write (`AdminOverride`, see [data model](data-model.md)) —
  consumed in-app at `/admin/break-glass`. **No in-app endpoint can create an
  `AdminOverride`**, so a hostile admin cannot grant or extend one for themselves.
  The elevation lapses at `expiresAt` (checked inline at authorization — no job).
  Runbook: OPS.md §9.

