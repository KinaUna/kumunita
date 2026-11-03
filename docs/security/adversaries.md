---
id: security-adversaries
title: "Assumed adversaries"
tier: reference
kind: security
status: current
audiences: [community-admin, platform-maintainer, developer, agent]
related:
  - docs/SECURITY.md
  - docs/security/why-first.md
  - docs/security/control-map.md
  - docs/adr/0001-core-stack-and-identity-model.md
  - docs/adr/0003-roles-and-moderator-scoping.md
  - docs/adr/0028-guardian-controls-account-scope-supervision.md
updated: 2026-11-02
---

> **Abstract:** the seven assumed adversaries (A1–A7) ranked by realism at this
> scale — the outsider, the bot, the compromised resident, the hostile admin,
> the third-party/operator path, the supply chain, and the over-reaching
> guardian — each mapped to the controls that answer it. **Who is it for:**
> anyone adding a feature and asking "what can a bad actor do now?". **When to
> read the rest:** when a change introduces a new privilege surface — add a row
> here and in the [control map](control-map.md) before it ships.

## 4. Assumed adversaries

Ranked by realism at this scale. Each maps to the controls that answer it.

| # | Adversary | Can do | Mitigated by |
|---|-----------|--------|--------------|
| A1 | **Curious outsider** (no account) | scrape public pages, probe endpoints, attempt signup | nothing sensitive is public; opt-in contact details; rate limiting ([`hardening-checklist`](../ops/hardening-checklist.md)) |
| A2 | **Credential-stuffing / signup bots** | brute login, flood register & report endpoints | rate limiting on the eight covered lanes ([decisions](decisions-open-items.md#decisions), [`hardening-checklist`](../ops/hardening-checklist.md)), Identity lockout ([`hardening-checklist`](../ops/hardening-checklist.md)); CAPTCHA deferred by decision ([`hardening-checklist`](../ops/hardening-checklist.md)) — a **bot-challenge seam** (F9, AUTH-15) is in place: a `KUMUNITA__SIGNUP__BOT_CHECK` flag, **off by default** (the owner's site key is an owner-ops step). The deferral stands until the owner turns the flag on |
| A3 | **Compromised or malicious resident** (has an account) | try to read beyond their audience, abuse delegation, spam reports | deny-by-default audiences ([ADR 0001](../adr/0001-core-stack-and-identity-model.md)-B); action-scoped delegation ([`ARCHITECTURE.md`](../ARCHITECTURE.md) §4.4); report-filing rate-limited; security-stamp invalidation on privilege change ([`hardening-checklist`](../ops/hardening-checklist.md)); audit trail for after-the-fact |
| A4 | **Malicious / hostile GlobalAdmin** | read everything, suppress others, extend their power | two-admin standing practice; no in-app `AdminOverride` creation; operator-only break-glass; `BreakGlass`-tagged audit ([ADR 0003](../adr/0003-roles-and-moderator-scoping.md), [`ARCHITECTURE.md`](../ARCHITECTURE.md) §4.5) |
| A5 | **Third parties & operator path** (SMTP provider, object store, VPS host, operator mistake) | leak data at B4/B5, misconfigure | encryption at rest + at source, scoped keys, SPF/DKIM/DMARC, least-privilege DB user, backups-verified ([`backups`](../ops/backups.md), [`email-smtp`](../ops/email-smtp.md), [`hardening-checklist`](../ops/hardening-checklist.md)) |
| A6 | **Supply chain** (base image, NuGet/npm) | malicious or stale dependency | pinned image + package versions; rebuild on base updates ([`hardening-checklist`](../ops/hardening-checklist.md)); two-project solution with no heavy runtime deps ([ADR 0001](../adr/0001-core-stack-and-identity-model.md)) |
| A7 | **The hostile or over-reaching guardian** (a parent supervising a child's account) | over a minor who may not contest it out-of-platform, use the supervision lane to silence the child (suspend without cause), or try to grow the lane beyond account-level control into reading the child's private content | standing comes from *creating* the account, no self-serve claim over a stranger ([ADR 0028](../adr/0028-guardian-controls-account-scope-supervision.md) G·4); the lane is **account-scope only** — the content path has no guardian branch, so the exposure is never the child's private life ([ADR 0028](../adr/0028-guardian-controls-account-scope-supervision.md) G·1); the **safety valve**: a GlobalAdmin dissolves any link or un-suspends any account, audited `Via: Admin` ([ADR 0028](../adr/0028-guardian-controls-account-scope-supervision.md) G·5) |

**Not assumed (documented, not designed for):** nation-state actors; VPS-level physical
access; a compromised Coolify platform. If the deployment context changes, add them.
