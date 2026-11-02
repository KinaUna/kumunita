---
id: start-platform-maintainer
title: "Platform maintainer entry"
tier: living
kind: entry
status: current
audiences: [platform-maintainer]
related:
  - docs/README.md
  - docs/OPS.md
  - docs/COOLIFY.md
  - docs/SECURITY.md
  - docs/philosophy/START-HERE.md
updated: 2026-11-02
---

> **Abstract:** the links a platform maintainer (the person who operates a
> deployment) is most likely to want, each with a one-line reason. Links
> only, no facts — this page cannot drift. **Who is it for:** the person
> who hosts this deployment (not the community's own admins).
> **When to read the rest:** when you're running the platform —
> provisioning, upgrading, backing up, recovering.

# Platform maintainer entry

You operate this deployment — you provision, upgrade, back up, and recover
the platform for one community. The docs below are the ones that matter for
*you*.

| Link | Why |
|---|---|
| [`docs/OPS.md`](../OPS.md) | The ops runbook — the hub: a "when to use each" table linking into [`docs/ops/`](../ops/) (one file per procedure, split in `D04`). |
| [`docs/COOLIFY.md`](../COOLIFY.md) | The Coolify deployment guide — the hub: an ordered checklist (0 → 6) linking into [`docs/deploy/coolify/`](../deploy/coolify/00-plan.md) (one file per step, split in `D05`). |
| [`SECURITY.md`](../SECURITY.md) | The privacy model and threat model — the authoritative constraint on the controls you operate (the model hub, split into the concern files under [`docs/security/`](../security/why-first.md) in `D08`). |
| [`SECURITY-AUDIT.md`](../SECURITY-AUDIT.md) | The audit cadence and the checklists (C / D / P / R) — the "did we do the thing" loop (the audit hub, split into [`audit-checklists`](../security/audit-checklists.md) / [`audit-annual-trigger`](../security/audit-annual-trigger.md) / [`audit-log`](../security/audit-log.md) in `D08`). |
| [`SECURITY.md`](../../SECURITY.md) | The vulnerability disclosure policy — how to report and how reports are handled (moved to repo-root `SECURITY.md` in `D08`; the machine-readable companion is served at `GET /.well-known/security.txt`). |
| [`docs/philosophy/START-HERE.md`](../philosophy/START-HERE.md) | The platform's philosophy — the trust model and the integration argument that the controls are built to hold. |
| [`docs/ARCHITECTURE.md`](../ARCHITECTURE.md) | The architecture map — the stack, the bounded contexts, the CQRS convention, the extension seams. |

The community's own admins (the moderators and global admins of the
neighborhood you're hosting) read the
[community-admin entry](community-admin.md).
