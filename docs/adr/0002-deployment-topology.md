# ADR 0002 — Deployment topology: one instance per neighborhood

Status: Accepted
Date: 2026-08-25

## Context

- Each neighborhood is an independent community: its own data, moderators, admin, and
  display name.
- Scale is a few dozen to a few hundred users per neighborhood.
- There is no v1 requirement for shared hosting or cross-neighborhood data.
- Cross-neighborhood federation (one person, many neighborhoods) is a *possibility*, not a
  requirement.
- Hosting is a VPS via Coolify; the team is small and the ethos is lean.

## Decision

- **One deployment (instance) per neighborhood.** Each instance = one app container + one
  Postgres (+ Mailpit in dev). No shared multi-tenant database, no tenant column, no
  row-level tenancy.
- **Modular monolith, in-process bounded contexts.** Identity / UserInfo / Authorization +
  feature modules live in one deployable and communicate in-process via interfaces. Not
  microservices, not one Coolify app per module.
- **Configuration-driven per instance.** `Community__Name`, SMTP, seeded admin, and the
  connection string are env vars, so the *same image* is deployed N times with different
  config.
- **Single image, many runs.** Coolify runs the one image once per neighborhood, each with
  its own env and its own Postgres volume.

Rationale:
- Multi-tenancy is a cross-cutting tax on every table, query, cache key, and background
  job — for a requirement we don't have. It is also the hardest thing to retrofit and the
  hardest to get right (leaks). Not worth it at this scale.
- Per-instance isolation is the *strongest* privacy guarantee (a community's data is
  physically separate) — which matters for a privacy-first product.
- In-process modules avoid network hops, distributed failure modes, and token passing; the
  interface boundaries preserve the option to split later.
- "Same image, different config" is the cheapest multi-deployment story and is exactly what
  Coolify is good at.

Deliberately NOT doing (yet): a shared control-plane/"meta" DB managing all neighborhoods;
a service mesh, API gateway, or separate auth service.

## Consequences

Positive
- Simplest possible data isolation; strongest privacy story.
- Tiny operational surface per community (one container + one DB).
- Each neighborhood upgrades/rollbacks independently.
- No tenant-leak bug class.
- The federation seam is preserved (externalId, interface boundaries) at no current cost.

Negative / accepted risks
- N neighborhoods = N containers + N DBs to operate (backup, update, monitor). Fine at
  "a few dozen"; grows linearly. (Mitigation: Coolify + a small ops runbook; a lightweight
  control plane only if it ever becomes painful.)
- No cross-neighborhood features in v1 (by design).
- Small per-instance config duplication (admin seed, etc.) — acceptable.
- Shared identity, when needed, is added via an external IdP rather than retrofitting
  tenancy (cleaner).

Revisit when
- A single login across neighborhoods is required (→ OpenIddict IdP; seam reserved in 0001-B).
- Instance count makes per-instance ops painful (→ consider a control-plane/management service).
- A hard requirement for cross-neighborhood data sharing emerges (→ export/migration or federation).
