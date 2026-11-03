---
id: audit-annual-trigger
title: "Annual & trigger-based audits"
tier: reference
kind: security
status: current
audiences: [developer, platform-maintainer, agent]
related:
  - docs/SECURITY-AUDIT.md
  - docs/security/audit-checklists.md
  - docs/security/audit-log.md
  - docs/security/trust-boundaries.md
  - docs/security/data-classes.md
  - docs/security/control-map.md
  - docs/security/decisions-open-items.md
  - docs/security/why-first.md
  - docs/adr/0003-roles-and-moderator-scoping.md
updated: 2026-11-02
---

> **Abstract:** the deep audits (A-1…A-3) run annually or on a throwaway
> instance, and the trigger-based table — what to run when a boundary,
> privilege surface, or data class changes. **Who is it for:** dev + the
> operator. **When to read the rest:** at the annual review, or the moment a
> trigger fires (new boundary, shared hosting, suspected breach, new role,
> new stored data class).

## Annual and trigger-based audits

- **A-1** **Full walk-through as a fresh resident and as an admin** on a
  throwaway instance: sign up (or receive an invitation), log in, set up 2FA,
  post with each audience type, attach a file, report a post, watch a
  moderator resolve it, watch an admin audit the log. The point is to catch
  the *security UX* that unit tests can't — a 2FA setup that dead-ends, an
  audience picker that defaults wrong, a recovery-code path that's lost.
- **A-2** **Break-glass drill (dry run on a dev instance only, never live
  data):** [`incident-response`](../ops/incident-response.md) end-to-end — the elevated account meets
  the 2FA bar, the action is `BreakGlass`-tagged in the audit log, and the
  two-admin rule was observable ([ADR 0003](../adr/0003-roles-and-moderator-scoping.md), [`ARCHITECTURE.md`](../ARCHITECTURE.md) §4.5). A procedure that
  has never been run is a rumor.
- **A-3** **TLS/domain hygiene:** certificate chain valid and renewal
  automated, no `http://` URL anywhere in the served app or email, HSTS
  preload candidacy if the domain is stable ([tls-domain](../ops/tls-domain.md)).

**Trigger-based (run the [checklist C short form](audit-checklists.md#checklist-c-code) plus the
relevant section, whenever):**

| Trigger | Run |
|---------|-----|
| New third-party boundary added (any B-boundary in [trust boundaries](trust-boundaries.md)) | D-5 + R-6 + a [trust boundaries](trust-boundaries.md) row |
| Shared hosting of multiple neighborhoods (one VPS/IP) | [rate-limiting](decisions-open-items.md#decisions) decision + CAPTCHA decision ([why-first](why-first.md)) |
| Suspected compromise / breach | the [incident flow](../ops/incident-response.md) **first**, then P-1, P-2, D-6 |
| New privilege surface ships (new role, new admin action, new delegation) | C-1…C-6 + one P-2 + one R-3 |
| New data class stored | a [data classes](data-classes.md) row + a [control map](control-map.md) row + R-4 |
