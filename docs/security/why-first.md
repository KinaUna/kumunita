---
id: security-why-first
title: "Why security & privacy is the top priority"
tier: reference
kind: security
status: current
audiences: [resident, community-admin, platform-maintainer, developer, agent]
related:
  - docs/SECURITY.md
  - docs/adr/0002-deployment-topology.md
updated: 2026-11-02
---

> **Abstract:** the *why* — the two operating rules and the five design rules
> (deny-by-default, least standing, audit-by-default, fail-closed, minimize)
> that every control in this model serves. **Who is it for:** anyone deciding
> whether a proposed change is consistent with the platform's privacy posture.
> **When to read the rest:** before adding a data class, a privilege surface,
> or a new third-party boundary — read the [adversaries](adversaries.md) and
> the [control map](control-map.md) next.

## 1. Why this is the top priority

- A Kumunita instance holds **real, named neighbors** and their private conversations.
  A leak isn't a dataset — it's the person down the street.
- Scale is tiny (dozens to hundreds of users), so **sophisticated attack economics
  don't apply; the realistic threats are simple ones** (stuffing, scraping, a bad
  actor with an account, an operator mistake). Simple threats need simple, correct
  controls — not a security framework.
- One deployment per neighborhood ([ADR 0002](../adr/0002-deployment-topology.md)) means **isolation is total but also
  total-loss**: there is no shared-infrastructure blast radius, and no shared defense
  in depth either. Each instance stands on its own.

**Deployment assumption (confirmed):** each neighborhood gets its **own VPS and its own
domain** (a dedicated instance per [ADR 0002](../adr/0002-deployment-topology.md), not co-hosted neighborhoods on one box).
Per-instance rate limiting is therefore effective, and one community's traffic or abuse
cannot exhaust or implicate another's. If this ever changes (shared hosting), revisit
the [rate-limiting decision](decisions-open-items.md#decisions) and the CAPTCHA decision ([`hardening-checklist`](../ops/hardening-checklist.md)).

**Two operating rules shape everything below:**

- **Privacy by design, not by promise.** Access rules are enforced in code on every
  request ([`ARCHITECTURE.md`](../ARCHITECTURE.md) §4.4); the audit trail exists to *prove* the design held,
  not to substitute for it. A control that depends on someone remembering to check is
  not a control.
- **Security is non-negotiable scope.** A security or privacy issue found at any
  milestone stops feature work until it is resolved or explicitly accepted in writing
  (here, or in an ADR). Schedule pressure is not an acceptable reason to defer one.

**Design rules, in priority order:**

1. **Deny by default.** No audience = invisible to everyone (the empty-audience-denies
   invariant, [`ARCHITECTURE.md`](../ARCHITECTURE.md) §4.4).
2. **Least standing.** Moderator access to restricted content is off by default and
   unlocked only by a report or an explicit admin toggle ([ADR 0003](../adr/0003-roles-and-moderator-scoping.md)).
3. **Audit by default.** Restricted-content access is always logged; admin and
   moderation actions are always logged ([`ARCHITECTURE.md`](../ARCHITECTURE.md) §5).
4. **Fail closed.** Authorization errors, missing grants, and concurrency failures
   resolve to denial / rollback — never to a guess.
5. **Minimize.** No event-sourced data lake, no third-party analytics, no telemetry.
   What we don't store, we can't leak.
