---
id: security-audit
title: "Security audit program"
tier: reference
kind: security
status: current
audiences: [developer, platform-maintainer, community-admin, agent]
related:
  - docs/security/audit-checklists.md
  - docs/security/audit-annual-trigger.md
  - docs/security/audit-log.md
  - docs/SECURITY.md
  - docs/ops/hardening-checklist.md
  - docs/adr/0002-deployment-topology.md
  - docs/adr/0003-roles-and-moderator-scoping.md
updated: 2026-11-02
---

> **Abstract:** the audit program — *how often the controls actually hold is
> verified, who runs each check, and where each run is recorded.* Rules,
> cadence, and the four checklists (C/D/P/R), the annual & trigger audits,
> and the audit log, split into one file each. **Who is it for:** the
> developer (C), the GlobalAdmin (P), the operator (D + R). **When to read the
> rest:** at the cadence listed below; after an incident; when a change ships
> a new boundary or data class.

Security and privacy are the product's top priority. The *what, whom, and why*
live in [the security model](SECURITY.md) (threat model, data classes, control
map) and the hardening state lives in [the hardening checklist](ops/hardening-checklist.md).
This document is the missing third piece: **how often we verify the controls
actually hold, who runs each check, and where the result is recorded.**

A control that is only *decided* is a promise. A control that is *verified on
a schedule and the verification is recorded* is a control. Every checklist
below is written so one person can run it start-to-finish without a meeting.

The program is split into one file per concern (this is the hub; each file is
self-contained and links back to the others):

| Concern | File |
|---------|------|
| **Rules + cadence** (this hub) | `SECURITY-AUDIT.md` (you are here) |
| **Checklists C / D / P / R** — code, deployment, platform-admin, recurring review | [`audit-checklists.md`](security/audit-checklists.md) |
| **Annual & trigger-based audits** (A-1…A-3 + the trigger table) | [`audit-annual-trigger.md`](security/audit-annual-trigger.md) |
| **The audit log** — the append-only record of every run + the SEC remediation lane's per-finding rows | [`audit-log.md`](security/audit-log.md) |

## 1. Rules

1. **Audits are cheap by design.** Dozens to hundreds of users, one instance per
   neighborhood ([ADR 0002](adr/0002-deployment-topology.md)): checklists, not a security framework. If a check
   takes a day, split it or drop it.
2. **A failed check is a defect.** A security or privacy issue found at any
   point stops feature work until resolved or explicitly accepted in writing
   ([why-first](security/why-first.md)). Record the finding in [the audit log](security/audit-log.md) and track it as a lane.
3. **Two eyes where it matters.** The platform-admin audit ([checklist P](security/audit-checklists.md#checklist-p-platform-admin)) is run by a
   GlobalAdmin under the two-admin standing practice ([ADR 0003](adr/0003-roles-and-moderator-scoping.md)) — one admin
   runs it, the other can see it happened in the audit log and this table.
4. **Record every run.** Every completed audit appends one row to [the audit log](security/audit-log.md) and
   updates the `Last run` date of the section it belongs to. An unrecorded
   audit did not happen.
5. **No PII in these logs.** Findings reference checklist IDs (C-3, D-1, …)
   and a one-line summary — never account names, emails, or content. The
   platform's own `AccessAudit` log remains the authoritative record of *who
   accessed what*; this program records *what we checked*.

## 2. Cadence

| Cadence        | Audit                                | Who                | Checklist        |
|----------------|--------------------------------------|--------------------|------------------|
| Per PR         | Code (short form) — security surfaces only | author + reviewer | [C short](security/audit-checklists.md#checklist-c-code) (C-1…C-6) |
| Per release    | Code (full) + deployment             | dev + operator     | [C full](security/audit-checklists.md#checklist-c-code) (C-7…C-11) + [D](security/audit-checklists.md#checklist-d-deployment) |
| Monthly        | Platform-admin review                | GlobalAdmin        | [P](security/audit-checklists.md#checklist-p-platform-admin) |
| Quarterly      | Recurring review + restore drill     | GlobalAdmin + operator | [R](security/audit-checklists.md#checklist-r-recurring-review) |
| Annual / trigger | Deep audits ([A](security/audit-annual-trigger.md)) | dev + operator     | [annual & trigger](security/audit-annual-trigger.md) |

"Per release" means every deployment to a neighborhood instance
([upgrade](ops/upgrade.md)).
A release that skips C-full + D is not done — same bar as a feature that
ships without its guide ([`CONVENTIONS.md`](guides/CONVENTIONS.md) consistency loop).
