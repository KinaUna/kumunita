# ADR 0006 — Module boundary contracts (Identity, UserInfo, Authorization)

Status: Accepted
Date: 2026-09-01
Amends: 0001 (B — thin token, fat authorization), 0003 (break-glass elevation)

## Context

ADR 0001 decided thin token / fat authorization service; ARCHITECTURE.md §3–4.2
sketch the three in-process modules behind interfaces, and §4.2 gives draft
signatures. M1 now implements them and first makes the access model live.

The philosophy (`docs/philosophy/in-code.md`) treats module interfaces as
*more precious than their implementation* — they outlast it, and changes to
audience semantics, group resolution, or delegation resolution are breaking
changes that belong in an ADR. Today the contract exists only as prose in
ARCHITECTURE.md. Left unfrozen, the first implementation would define the
interface through whatever details the first code review settles on — the
*Accidental integration* anti-pattern (undocumented assumptions across a
boundary).

This ADR freezes the M1 contract surface so implementations can move freely
underneath and every seam change becomes a deliberate, reviewed decision.

## Decision

### A. The three module boundaries (frozen)

In-process and extractable later, with a strict dependency direction.

| Module | Owns | Public interface |
|---|---|---|
| **IdentityModule** | authentication (ASP.NET Identity, `identity` schema); only issuer of the thin principal | `IIdentityService` |
| **UserInfoModule** | profiles, groups, group membership, delegation grants (`mt` documents) | `IUserInfoService` |
| **AuthorizationModule** | audience evaluation, effective principal, role resolution (incl. break-glass), `AccessAudit` writes | `IAuthorizationService` |

Signatures (semantic freeze; C# identifiers may deviate for conflict
avoidance — e.g. `AccessAction` for the docs' `Action` to avoid
`System.Action`):

	// IdentityModule
	IIdentityService {
	  Task<ThinPrincipal> GetCurrentAsync();
	  Task<ThinPrincipal?> GetBySubjectAsync(string subjectId);
	}

	// UserInfoModule
	IUserInfoService {
	  Task<Profile?> GetProfileAsync(string subjectId);
	  // Strong consistency: reads GroupMembership documents. A membership
	  // change takes effect on the very next request, no projection lag.
	  // Loaded once per request (D4).
	  Task<HashSet<string>> GetGroupIdsAsync(string userId);
	  Task<DelegationGrant?> GetActiveGrantAsync(string delegateId);
	  Task<Group> CreateGroupAsync(…);
	  Task AddGroupMemberAsync(string groupId, string userId);
	  Task RemoveGroupMemberAsync(string groupId, string userId);
	  Task<DelegationGrant> GrantDelegationAsync(…);
	  Task RevokeDelegationAsync(string grantId);
	}

	// AuthorizationModule
	IAuthorizationService {
	  // Single-target — detail views ("may I read this post?")
	  Task<Decision> CanAsync(string actorId, AccessAction action, IAuditableResource target);
	  // Bulk — list views (feeds, directory, boards): one group-load, one
	  // matching pass, one aggregate audit row.
	  Task<VisibleSet> CanSeeAsync(string actorId, AccessAction action,
								   IEnumerable<IAuditableResource> candidates);
	}

with `Decision = { Allowed, Via, EffectivePrincipalId }` and
`VisibleSet = { visible: [(id, Via)], hiddenCount }` (ARCHITECTURE.md §4.2,
§5).

### B. The thin principal (frozen)

	ThinPrincipal = {
	  SubjectId,       // stable across the instance; later: OIDC `sub`
	  ExternalId?,     // reserved for federation (ADR 0001)
	  IsVerifiedResident,
	  Roles,           // simple claim strings: "moderator:<component>"
	}

- **Only the IdentityModule knows the identity source** and only it issues
  `ThinPrincipal` (cookie claims now; the later OIDC `sub` swap is
  mechanical and confined to this module).
- **No audience, group, delegation, or content data appears in the
  principal or the cookie.** "Can X see Y" is *always* a per-request query to
  the AuthorizationModule (ADR 0001-B). A test asserts the principal's claim
  set carries no relational data (D5).
- Break-glass elevation (`AdminOverride`, ADR 0003) is **not** minted into
  the principal: the AuthorizationModule reads the consumed `AdminOverride`
  row inline at decision time (`expiresAt` past ⇒ no elevation). The
  operator-only, direct-DB-write path stays unchanged and the principal
  stays lean.

### C. Invariants (and who owns them)

All six are owned by the **AuthorizationModule**. Every other module invokes
the interfaces; none may re-derive access on its own.

1. **Empty audience denies.** In mode `All`, `grants.All(...)` over an
   empty grant list is vacuously true — the guard is explicit. An empty
   audience always denies, in either mode. This is the invariant that keeps
   an empty `All` resource from being world-readable.
2. **Delegation is action-scoped.** A delegated actor gets the owner's
   standing only for actions inside the grant's `scope`; out-of-scope
   actions are a Deny even though the effective principal is the owner.
   `Via = Delegation` records the *acting* identity in the audit row.
3. **Audit is always on.** Audience-restricted content is audited for
   *both* Allow and Deny: one row per single-target decision; one aggregate
   row per bulk decision (`targetKind` `component`/`directory`,
   `visibleCount`/`hiddenCount`, §5). Audit rows commit in the *same*
   transaction as the domain write — no silent, unaudited access.
4. **Group visibility is strong-consistency membership resolution** against
   live documents — a membership change takes effect on the very next
   request. Projections serve ordering/trimming only; no access decision
   ever reads a projection (a lagging projection must not be able to leak or
   hide access).
5. **Moderator access to audience-restricted content is off by default.**
   The component-scoped `moderatorAccess` flag is `false` unless a
   GlobalAdmin sets it (§4.5, ADR 0003); report-driven unlock arrives in
   M3.
6. **One matching pass.** `CanAsync` and `CanSeeAsync` both reduce to the
   same single match function (`MatchGroups`) so the paths cannot drift.

### D. Dependency direction (frozen)

	Web ──▶ feature modules ──▶ { Identity, UserInfo, Authorization } ──▶ Marten / Wolverine

- Feature modules (M2+) depend on the three access modules and Marten; never
  the reverse.
- AuthorizationModule may call `IUserInfoService` (group/delegation
  resolution); it never depends on feature modules.
- `Kumunita.Core` references no ASP.NET HTTP types (testable; future
  API/MCP layer stays open — ARCHITECTURE.md §2).
- **The authorization path is unique.** All feature-module access checks
  go through `IAuthorizationService` (`CanAsync` for detail, `CanSeeAsync`
  for lists). Feature modules do *not* read group membership for access
  purposes — that is the *Distributed fragmentation* anti-pattern (access
  logic scattered across components, group rules re-implemented per
  feature, §4.2's "list authorization is one platform primitive, not
  per-feature logic"). Non-access data reads may use `IUserInfoService`.
- **Request shape:** `IIdentityService.GetCurrentAsync()` → thin
  principal → `GetGroupIdsAsync` *once* per request → `CanAsync` /
  `CanSeeAsync` per check/list.

### E. Change management

- **Breaking** — requires a new ADR amending this one, with a migration
  story for the audit log (which records `Via`):
  - adding/removing/renaming any method on the three interfaces;
  - changing any `ThinPrincipal` field's meaning;
  - changing `Any`/`All` or empty-grant semantics;
  - changing delegation-resolution rules;
  - changing the `AccessAudit` row shape or the always-on guarantee.
- **Compatible** — no ADR, but named in the change:
  - adding a method to the *owning* module's public surface;
  - adding an `AccessAction` case — new actions **deny by default** on
	audience-restricted resources until a policy opts in;
  - adding `Profile` document fields.
- Per `in-code.md`, public surface is permanent integration cost: every
  public method is paid for by every other part. Add few, add stable.

## Consequences

**Positive**
- Implementations move freely under a frozen contract; "the contract is
  small and stable" (in-code.md) becomes checkable instead of aspirational.
- The Authority-domain chain (identity → permission → audit) is explicitly
  owned and owned in one place; trust you can show doesn't depend on tribal
  knowledge (domains-of-integration.md).
- M1's seam tests (docs/design/m1-identity-access.md) can reference the
  frozen contract, making in-code.md's "test the access model at its seams"
  concrete and repeatable.

**Negative / accepted risk**
- Freezing at M1 risks over-constraining before implementation feedback.
  Mitigated by the compatible lane (E) and the revisit conditions below;
  the `All`-mode edge is already flagged for post-M3 revisit (ADR 0001-B).
- The frozen surface is slightly larger than M1's minimum (e.g. group
  management methods) — accepted: it *is* M1's work.

Revisit when
- `All` mode is dropped after M3 per ADR 0001-B → re-specify invariant 1
  here.
- Federation (OpenIddict) lands → `ExternalId` becomes the cross-instance
  identifier; only IdentityModule changes (global identity, local
  authorization).
- A second consumer of the authorization decision appears (API/MCP layer) →
  these interfaces *are* that boundary; extraction is the design from
  the start.
