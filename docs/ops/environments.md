---
id: ops-environments
title: "Environments"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer, developer]
related:
  - docs/ops/configuration-reference.md
  - docs/COOLIFY.md
  - docs/adr/0002-deployment-topology.md
updated: 2026-11-02
---

> **Abstract:** the two environments (dev compose, prod VPS/Coolify), their
> databases and mail, and the "no shared staging cluster" rule. **Who is it
> for:** the host operator and anyone running the app locally. **When to
> read the rest:** before choosing where a change gets verified, or before
> an upgrade drill.

# Environments

| Env  | Where            | DB          | Email     | Purpose                 |
|------|------------------|-------------|-----------|-------------------------|
| dev  | local (compose)  | postgres:18 | Mailpit   | development, tests      |
| prod | VPS via Coolify  | per instance| real SMTP | a live neighborhood     |

No shared "staging cluster" — a throwaway prod-style instance is enough to verify an
upgrade (the [upgrade](upgrade.md) drill and the [demo site](demo-site.md) are the two
concrete forms of that throwaway instance).

## Related

- [Configuration reference](configuration-reference.md) — the per-instance identity this table's rows carry.
- [COOLIFY.md](../COOLIFY.md) — how the prod environment is built.
- [ADR 0002](../adr/0002-deployment-topology.md) — why one instance per neighborhood.
