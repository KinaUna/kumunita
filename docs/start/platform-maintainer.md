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
| [`docs/OPS.md`](../OPS.md) | The ops runbook — every procedure (provision, upgrade, backup, restore, TLS, SMTP, monitoring, incident response). The hub; `D04` splits it into one file per procedure. |
| [`docs/COOLIFY.md`](../COOLIFY.md) | The Coolify deployment guide, step by step. The hub; `D05` splits it into one file per step. |
| [`SECURITY.md`](../../SECURITY.md) | The privacy model and threat model — the authoritative constraint on the controls you operate. `D08` splits this into `docs/security/`. |
| [`SECURITY-AUDIT.md`](../../SECURITY-AUDIT.md) | The audit cadence and the checklists (C / D / P / R) — the "did we do the thing" loop. `D08` splits this too. |
| [`SECURITY-VDP.md`](../../SECURITY-VDP.md) | The vulnerability disclosure policy — how to report and how reports are handled. `D08` moves this to repo-root `SECURITY.md`. |
| [`docs/philosophy/START-HERE.md`](../philosophy/START-HERE.md) | The platform's philosophy — the trust model and the integration argument that the controls are built to hold. |
| [`docs/ARCHITECTURE.md`](../ARCHITECTURE.md) | The architecture map — the stack, the bounded contexts, the CQRS convention, the extension seams. |

The community's own admins (the moderators and global admins of the
neighborhood you're hosting) read the
[community-admin entry](community-admin.md).
