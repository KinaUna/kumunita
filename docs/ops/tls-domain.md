---
id: ops-tls-domain
title: "TLS & domain"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/COOLIFY.md
  - docs/ops/hardening-checklist.md
updated: 2026-11-02
---

> **Abstract:** the TLS posture for a Kumunita instance — Coolify and Let's
> Encrypt auto-issue and renew, so the operator's share is DNS, ports, and
> an occasional cert check. **Who is it for:** the host operator.
> **When to read the rest:** before changing a domain, or when verifying
> renewal.

# TLS & domain

- Coolify + Let's Encrypt auto-issues and renews. Operator needs only: DNS pointing, ports
  80/443 reachable, and an occasional cert check.
- Verify renewal with `openssl s_client -connect <domain>:443 -servername <domain> 2>/dev/null | openssl x509 -noout -enddate`.
- If a domain changes: update DNS, wait for propagation, reissue the cert, confirm.

The edge TLS here is distinct from the app↔DB TLS posture
(`DbTls__SslMode`, [configuration reference](configuration-reference.md)) —
that one is configured per instance, this one rides on Coolify/Caddy.

## Related

- [COOLIFY.md](../COOLIFY.md) — the deployment guide that sets up the domain.
- [Hardening checklist](hardening-checklist.md) — the "Network" and "App" rows that assume this TLS.
