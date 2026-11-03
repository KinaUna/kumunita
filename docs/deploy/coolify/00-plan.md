---
id: deploy-coolify-00-plan
title: "Coolify — 0: Plan"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/COOLIFY.md
updated: 2026-11-02
---

> **Abstract:** the step map for bringing one Kumunita neighborhood online with Coolify — the ordered list of the six steps and what each is for. **Who is it for:** the host operator, before provisioning. **When to read the rest:** as the overview before you start step 1.

# 0 — Plan
| What | How often | Where |
|---|---|---|
| Install Coolify on the VPS | once per VPS | [§1](10-install-coolify.md) |
| DNS + ports | once per VPS | [§2](20-dns.md) |
| Project + environment | once per neighborhood | [§3](30-project-environment.md) |
| Postgres (addon) | once per neighborhood | [§4](40-postgres.md) |
| App (Coolify app) | once per neighborhood | [§5](step-5/00-overview.md) |
| Verify + hand over to runbook | once per neighborhood | [§6](60-verify.md) |
