---
id: security-trust-boundaries
title: "Trust boundaries"
tier: reference
kind: security
status: current
audiences: [community-admin, platform-maintainer, developer, agent]
related:
  - docs/SECURITY.md
  - docs/security/data-classes.md
  - docs/adr/0003-roles-and-moderator-scoping.md
updated: 2026-11-07
---

> **Abstract:** the seven boundaries (B1–B7) where data crosses into someone else's
> control — browser↔app, internet→VPS, app↔Postgres, app↔SMTP, backups→offsite,
> secrets, and a third-party error tracker — and who controls each one. **Who is it for:** the operator and
> anyone adding a new integration. **When to read the rest:** when a change
> introduces or touches a third-party boundary — add a row here and in the
> [adversaries](adversaries.md) + [control map](control-map.md) before it ships.

## 2. Trust boundaries

Data crosses these boundaries; each one is a point where protection is delegated to
someone else.

| # | Boundary                       | Who controls it                    | Protection / notes                                                                 |
|---|--------------------------------|------------------------------------|------------------------------------------------------------------------------------|
| B1 | Browser ↔ app (TLS)           | Coolify / Let's Encrypt            | HTTPS-only + HSTS ([`hardening-checklist`](../ops/hardening-checklist.md)); cookies `Secure`/`HttpOnly`/`SameSite=Lax`         |
| B2 | Public internet → VPS         | VPS host (operator)                | Only 80/443 + SSH (key-only) exposed; Postgres never public ([`hardening-checklist`](../ops/hardening-checklist.md))            |
| B3 | App ↔ Postgres                | operator                           | Dedicated non-superuser DB account after first run; internal network only           |
| B4 | App ↔ SMTP provider           | **provider (third party)**         | SPF/DKIM/DMARC ([`email-smtp`](../ops/email-smtp.md)); provider can read email bodies ([class (a)](data-classes.md)) — and notification / DM / mention emails also carry a ≤ 200-char UGC/DM snippet ([`UgcSnippets.Truncate`](../../src/Kumunita.Core/Notifications/UgcSnippets.cs)), so **class (b)** crosses B4 too. The stored outbox copy each of these was sent from is retained per the D6 policy (body nulled after 7 days, [`RetentionOptions`](../../src/Kumunita.Core/Privacy/RetentionOptions.cs))      |
| B5 | Backups → offsite storage     | **object store (third party)**     | Encrypted at rest by the store; **also encrypt backups at the source**; access scoped to a key we rotate ([`backups`](../ops/backups.md)) |
| B6 | Secrets: env / secrets manager| operator + Coolify                 | One-time seed token pattern; secrets never in image, logs, or this repo ([`hardening-checklist`](../ops/hardening-checklist.md)) |
| B7 | App ↔ Sentry (error tracker)  | **Sentry SaaS (third party)**      | **DSN-gated** — `SENTRY_DSN` unset means the `UseSentry` seam is not executed and the instance is byte-identical to before. When enabled: `MinimumBreadcrumbLevel = LogLevel.Warning` (nothing below Warning reaches Sentry, so no PII template does), `SendDefaultPii = false`, the request scrubber redacts headers/cookies/query, and log templates carry no PII (the [IS08 gate](../../tests/Kumunita.Web.Tests/SecGate/LogTemplatePiiGateTests.cs)) |

**Operator = trusted-but-not-invulnerable.** The host operator can read everything on
a VPS. The break-glass design ([ADR 0003](../adr/0003-roles-and-moderator-scoping.md), [`ARCHITECTURE.md`](../ARCHITECTURE.md) §4.5) assumes the operator
may be the *only* honest party left, and constrains what an app-admin can do to the
operator's authority.
