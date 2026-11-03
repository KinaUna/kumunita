---
id: ops-provision
title: "Provision a new neighborhood"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/ops/environments.md
  - docs/ops/configuration-reference.md
  - docs/ops/first-boot.md
  - docs/adr/0002-deployment-topology.md
updated: 2026-11-02
---

> **Abstract:** the seven steps to bring a fresh neighborhood online —
> domain, DNS, a dedicated Coolify app + Postgres, and the first-run admin
> fields. **Who is it for:** the host operator of a new (or demo) instance.
> **When to read the rest:** before provisioning, and when a procedure's
> step says "as Procedure 1".

# Provision a new neighborhood

1. Reserve a domain/subdomain (e.g. `maplewood.example` or `maplewood.kumunita.example`).
2. Point DNS (A or CNAME) at the VPS.
3. In Coolify, create a **new app** from the Kumunita image with its **own** env set:
   `Community__Name`, `Community__SupportEmail`, a **dedicated Postgres**, SMTP, and the
   first-run admin fields.
4. Generate a strong DB password; store it in your secrets manager (not in the runbook).
5. Deploy.
6. Run [first boot & admin setup](first-boot.md).
7. Record the instance in the inventory ([configuration reference](configuration-reference.md), "Instance inventory").

## Related

- [Environments](environments.md) — dev vs prod, the topology facts.
- [Configuration reference](configuration-reference.md) — the env set this procedure configures.
- [First boot & admin setup](first-boot.md) — the procedure that follows this one.
