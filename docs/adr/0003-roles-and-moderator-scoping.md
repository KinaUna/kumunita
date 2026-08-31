# ADR 0003 — Roles & moderator scoping

Status: Accepted
Date: 2026-08-25

## Context

- A global admin must delegate moderation to other residents so a community can self-moderate.
- Moderation should be *scoped*: a moderator governs a functional component (Safety,
  Maintenance, ...), not necessarily everything.
- Sensitive dimension: moderator access to *audience-restricted* (private) content. Privacy
  must be the default, yet private groups must not become unmoderatable.
- Roles must be operable by a non-technical community volunteer, not a full policy engine.
- Must compose with the audience / delegation / audit model (ADR 0001-B).

## Decision

- **Three roles**: Member, Moderator, GlobalAdmin.
  - **Member** — verified resident; participates within audiences that grant access.
  - **Moderator** — scoped to one or more functional components.
  - **GlobalAdmin** — full control; the only role that can manage roles, set moderator
    scope, toggle scope-level `moderatorAccess`, and read the audit log.
- **Component scoping.** A `ModeratorAssignment { id, userId, componentId, grantedBy, at }`
  maps a moderator to specific components. Moderation actions (hide, pin, remove) are
  permitted only within assigned components. A moderator is not a GlobalAdmin and cannot
  grant roles.
- **Moderator access to audience-restricted content:**
  - **Default OFF.** The author's audience is absolute; a moderator cannot read a post they
    are not in the audience of.
  - **Report-driven unlock.** Filing a report on a resource grants the *assigned* moderator
    audited read access to *that specific resource* (scoped, optionally time-limited). This
    is the safety valve keeping private groups moderatable.
  - **Standing override (optional, admin-set).** A GlobalAdmin may set a scope (component or
    designated sensitive area) to `moderatorAccess = On` for deliberate standing visibility.
  - **Audit always on.** Any moderator read of restricted content (report-driven or standing)
    is appended to `AccessAudit`; the audit log is GlobalAdmin-only.
- **Separation of duties.** Only GlobalAdmin promotes/demotes and assigns scope. Moderators
  act within scope and do not manage moderators. The author always controls their own
  content's audience.
- **GlobalAdmin trust management.** Two GlobalAdmins as the standing state (each can
  demote the other). **Break-glass elevation**: when admin(s) are gone or hostile, the
  host operator grants a time-limited, single-use, audited elevation (`AdminOverride`)
  by direct DB write — *no in-app endpoint can create one*, so a hostile admin cannot
  grant or extend it for themselves. Elevation lapses at `expiresAt` and all actions
  under it are audited (`via: BreakGlass`). See ARCHITECTURE.md §4.5; runbook OPS.md §9.

Rationale:
- Three roles are operable by a volunteer; a per-action claims matrix is overkill and a bug
  magnet at this scale.
- Component scoping matches the product's functional areas and gives "delegate moderation" a
  natural, understandable unit.
- Privacy-default (OFF) + report unlock + optional standing override is the smallest policy
  that is simultaneously trustworthy *and* safe — avoiding both "moderators peek at
  everything" (kills trust) and "private groups are lawless" (safety hole).

## Consequences

Positive
- Delegating moderation is a simple, understandable admin action (promote + pick components).
- Strong, easy-to-explain privacy default.
- Private spaces stay moderatable via an audited, scoped mechanism.
- Clear separation of duties reduces misuse risk.
- Composes with the audience/delegation/audit model without a separate policy engine.

Negative / accepted risks
- Coarse actions: "moderate a component" bundles pin/delete/edit; fine action-level
  permissions (e.g. pin-but-not-delete) aren't directly expressible. Acceptable now; revisit
  if a real need appears.
- Standing `moderatorAccess = On` on a scope means moderators *can* read private content
  there — a deliberate, audited trust tradeoff.
- A single GlobalAdmin is a concentration of trust (mitigations: admin actions are
  audited; handover = promote a resident, which is itself a GlobalAdmin action;
  **two-admin standing practice** and **operator-only break-glass elevation** close the
  departure/hostile-admin cases — see Decision).

Revisit when
- Action-level permissions (pin vs delete vs edit) are needed.
- A moderator hierarchy (per-group vs community) is needed — current scope is per-component.
- Federation adds cross-neighborhood admin (→ a global role layer).
