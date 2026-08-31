# ADR 0001 — Core stack & identity model

Status: Accepted
Date: 2026-08-25

## Context

Kumunita serves one neighborhood per deployment, at a scale of a few dozen to a few hundred
users. Requirements that shape the core:

- Privacy-first, user-controlled access: authors decide exactly who sees what, down to
  individual users and groups. Access rules will get complex.
- Delegation: owners grant family/caretakers scoped access.
- Audit: access to restricted content must always be logged.
- Lean: no premature services or ceremony; small team; a VPS + Coolify.
- Future: cross-neighborhood federation (one person, many neighborhoods) and MCP/API.

We want a stack that handles complex authorization and side-effect-heavy flows (email,
notifications, audit, reminders) well, without the weight of a distributed system.

## Decision

### A. Data + messaging: Marten + Wolverine (CQRS-lite)

- **Marten** as the Postgres document store (in place of EF Core). Documents + projections
  fit the profile/group/audience model and the relationship queries.
- **Wolverine** for in-process messaging, side-effect handlers, and scheduled jobs.
- **CQRS-lite**: writes via commands + handlers; reads via Marten queries/projections.
- **No event sourcing.** An immutable event log is not needed; documents + projections are
  the right weight.
- Rationale: the hard parts are (1) relationship-based authorization and (2) side effects.
  Both fit "documents + handlers + projections + jobs" naturally. At this scale an
  in-process monolith beats the operational cost of microservices.

### B. Identity & authorization: thin token, fat authorization service

- **Authentication stays lean.** ASP.NET Core Identity + cookie now. It issues only a small
  principal (subjectId, isVerifiedResident, base roles); no relational data in the token.
- **Authorization is a separate concern** (AuthorizationModule) that resolves "can actor X do
  Y to Z?" per request. This is where complexity lives and is allowed to grow.
- **Access is user-defined**: an audience is explicit grants to users and/or groups, combined
  Any (default) or All. No built-in relationship semantics (neighbor/family) in the core —
  groups are the reuse unit; relationship helpers arrive later as group conveniences.
- **Delegation** resolves an effective principal, bounded by the grant's action scope.
- **Audience `All` (intersection) mode** exists because a concrete use case was stated
  (content visible only to the intersection of two groups, e.g. "board + verified
  neighbor"). It is the mode that forces the empty-audience-denies invariant (vacuous
  truth over `All`). Revisit after M3 whether `Any` alone suffices for the real
  workloads — if it does, dropping `All` removes that edge case entirely.
- **Moderator access to audience-restricted content is off by default**; a report grants the
  assigned moderator audited access to that item; admins can enable standing visibility per scope.
- **Audit is always on.**
- **Federation is deferred.** `Profile.externalId` is reserved now; because IdentityModule is
  the only component that knows the identity source, adding OpenIddict later is additive.
  Global identity, local authorization.

## Consequences

Positive
- Complex, relationship-based authorization is a first-class, well-factored capability.
- Side effects and scheduled jobs are uniform and testable (handlers + TestWidgets).
- Identity stays trivial and swappable; authorization rules can grow freely.
- Small operational surface: one app + one Postgres per neighborhood.

Negative / accepted risks
- Steeper learning curve than EF Core (Marten API, projections, handler conventions).
- Email delivery is made reliable with **one** Wolverine durable handler (Postgres-backed
  invocation state), a 6-attempt/~24 h retry cap, and a small `EmailDeadLetter` table —
  no new service, no queue broker (ARCHITECTURE.md §6.2).
- Smaller ecosystem / fewer ready-made answers than EF.
- No EF migration tooling — instead Marten's own **versioned migrations** (ordered
  `IMigration` steps, recorded in `mt.migrations`) give reviewable, forward-only schema
  evolution. Auto-upgrade is dev-only and never run against production.
- First feature costs more time while learning; later features cost the same or less.

Revisit when
- True multi-tenant shared hosting is required.
- Federation becomes a real requirement (stand up an OpenIddict IdP).
- Scale-out is needed (revisit the in-process monolith).
