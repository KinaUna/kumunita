---
id: deploy-coolify-53-domains
title: "Coolify — 5.3: Domains"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/deploy/coolify/step-5/00-overview.md
  - docs/ops/tls-domain.md
updated: 2026-11-02
---

> **Abstract:** attaching the neighborhood's domain in Coolify (Let's Encrypt) and keeping `Verification__BaseUrl` in sync with it. **Who is it for:** the host operator. **When to read the rest:** at first deploy and whenever the domain changes.

# 5.3 — Domains
Attach the neighborhood's domain ([§2](../20-dns.md)). Coolify + Let's Encrypt issue it —
see the [TLS & domain runbook](../../../ops/tls-domain.md).

The domain is also the value of `Verification__BaseUrl` ([§5](00-overview.md) table) — set
both together, from the same value, so the confirmation email's link resolves
at the same host Caddy routes and signs for. If the domain changes later,
update `Verification__BaseUrl` in the same change.
