---
id: deploy-coolify-20-dns
title: "Coolify — 2: DNS prerequisite"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/COOLIFY.md
  - docs/ops/tls-domain.md
updated: 2026-11-02
---

> **Abstract:** the DNS prerequisite for Coolify's per-domain TLS — reserving the neighborhood's domain and pointing the A/CNAME at the VPS before the cert check. **Who is it for:** the host operator. **When to read the rest:** before attaching a domain or issuing Let's Encrypt.

# 2 — DNS prerequisite (once per VPS, per domain)
Coolify handles TLS per **domain**, and Let's Encrypt needs the resolver to
already point at the VPS.

1. Reserve the neighborhood's domain/subdomain (e.g.
`maplewood.kumunita.example`).
2. Point **A** (own zone) or **CNAME** (hosted zone) at the VPS.
3. Wait for propagation (`dig` / `nslookup`), then proceed — attaching a domain
before propagation fails the cert check.
