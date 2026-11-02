---
id: ops
title: "Kumunita — Operations Runbook"
tier: living
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/ops/environments.md
  - docs/ops/configuration-reference.md
  - docs/adr/0002-deployment-topology.md
  - docs/ARCHITECTURE.md
updated: 2026-11-02
---

> **Abstract:** the ops hub — practical, tested procedures for running a
> Kumunita instance. Every procedure lives in its own file under
> [`ops/`](ops/) (split in `D04`); this page is the table of contents with
> one-line "when to use each" reasons. **Who is it for:** the host operator.
> **When to read the rest:** pick the procedure you need from the table;
> read only that file.

# Kumunita — Operations Runbook

Practical, tested procedures for running a Kumunita instance. Companion to
[`ARCHITECTURE.md`](ARCHITECTURE.md) (what it is) and [ADR 0002](adr/0002-deployment-topology.md) (why
one-instance-per-neighborhood).

**Two roles, don't confuse them:**
- **Host operator** — owns the VPS, Coolify, backups, upgrades, TLS, SMTP. (This runbook.)
- **Community GlobalAdmin** — owns in-app roles, moderation, content. (In-product.)

**Rules of the road:** every procedure should be *tested before it's relied on*. Backups
you haven't restored are not backups. Update the "Last tested" date when you run a step.

## Procedures

| Procedure | When to use it |
|---|---|
| [Environments](ops/environments.md) | Dev vs prod topology; the "no shared staging" rule |
| [Configuration reference](ops/configuration-reference.md) | The complete env table, "config is state", the instance inventory |
| [Provision a new neighborhood](ops/provision.md) | Bringing a fresh instance (or demo) online |
| [First boot & admin setup](ops/first-boot.md) | After provision — complete admin setup, the one-time token |
| [Upgrade the application](ops/upgrade.md) | Deploying a new version; the one-step rollback (R1/R2/R3) + drill |
| [Backups](ops/backups.md) | The `pg_dump`, the media volume, PITR/WAL archiving, RPO/cadence |
| [Restore](ops/restore.md) | Same-instance corruption, PITR, disaster recovery (new VPS) |
| [TLS & domain](ops/tls-domain.md) | Let's Encrypt renewal, domain changes |
| [Email (SMTP)](ops/email-smtp.md) | Provider, SPF/DKIM/DMARC, dead-letter runbook |
| [Monitoring & health](ops/monitoring.md) | `/health`, uptime, logs, capacity, audit review |
| [Data & access operations](ops/privacy-operations.md) | Suspend, delete, hand-over, break-glass elevation |
| [Security hardening checklist](ops/hardening-checklist.md) | Network, SSH, secrets, DB roles, app hardening, CSP |
| [Incident response](ops/incident-response.md) | Detect → triage → contain → recover → communicate → record |
| [Deploy a demo site](ops/demo-site.md) | A throwaway `Production` instance with sample data |
| [Review the user guides](ops/guide-review.md) | The quarterly guide-honesty check (GlobalAdmin) |

## Conventions

- Keep steps numbered and imperative; one actor per step.
- Stamp each procedure with **Last tested: <date>** and refresh when you run it.
- When a procedure changes, update it *and* the affected inventory/checklist in the same edit.
- Secrets live in the secrets manager, never in this file. Reference by name (`SMTP__Pass`),
  not by value.
