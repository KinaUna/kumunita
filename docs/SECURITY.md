---
id: security-model
title: "Security & privacy model"
tier: reference
kind: security
status: current
audiences: [resident, community-admin, platform-maintainer, developer, agent]
related:
  - docs/security/why-first.md
  - docs/security/trust-boundaries.md
  - docs/security/data-classes.md
  - docs/security/adversaries.md
  - docs/security/control-map.md
  - docs/security/decisions-open-items.md
  - docs/SECURITY-AUDIT.md
  - SECURITY.md
updated: 2026-11-02
---

> **Abstract:** the *what, whom, and why* of Kumunita's security and privacy —
> the single source for what we protect, from whom, and why, split into six
> concern files. **Who is it for:** everyone — residents read the
> [why](security/why-first.md) and [data classes](security/data-classes.md);
> admins and developers read the [adversaries](security/adversaries.md) and
> [control map](security/control-map.md); operators read the
> [decisions](security/decisions-open-items.md). **When to read the rest:**
> when a change touches a data class, a privilege surface, or a third-party
> boundary — the matching concern file, then the [control map].

Security and privacy are the **highest-priority requirements** of this product, not
features to add. This document is the single source for *what we protect, from whom,
and why*. It complements [`ARCHITECTURE.md`](ARCHITECTURE.md) (how the mechanisms work),
[`hardening-checklist`](ops/hardening-checklist.md) (the operational hardening
checklist), and [`SECURITY-AUDIT.md`](SECURITY-AUDIT.md) (the audit program — the
cadence, owners, and run-anywhere checklists that verify the controls here actually
hold, and where each run is recorded). Rationale for identity/authorization
decisions lives in [ADR 0001](adr/0001-core-stack-and-identity-model.md) and [ADR 0003](adr/0003-roles-and-moderator-scoping.md).

Status: pre-M0, design-level. Update this document when a control changes, a threat
is accepted, or an open item below is decided — and record non-obvious decisions as
ADRs.

The model is split into one file per concern (this is the hub; each concern is
self-contained and links to the others):

| Concern | Read it when | File |
|---------|--------------|------|
| **Why security & privacy is the top priority** — the two operating rules and the five design rules (deny-by-default, least standing, audit-by-default, fail-closed, minimize) | you need the *why* behind a rule, or are deciding whether a change fits the posture | [`why-first.md`](security/why-first.md) |
| **Trust boundaries** (B1–B6) — where data crosses into someone else's control | you are adding or touching a third-party integration | [`trust-boundaries.md`](security/trust-boundaries.md) |
| **Data classes** (PII, private content, audit, secrets, media/attachments) — what we store and what its loss means | you are deciding what may be stored, served, or logged | [`data-classes.md`](security/data-classes.md) |
| **Assumed adversaries** (A1–A7) — ranked by realism at this scale | you are asking "what can a bad actor do now?" | [`adversaries.md`](security/adversaries.md) |
| **Control map** — every control, the adversary it answers, the code seam, the ADR | you are adding a feature and must cover the threat, or verifying a control holds | [`control-map.md`](security/control-map.md) |
| **Decisions & open items** — the decided controls + the items still undecided | you are changing a security surface, or deciding an open item | [`decisions-open-items.md`](security/decisions-open-items.md) |

The related surfaces:

- **The audit program** — how often the controls are verified, by whom, and where
  each run is recorded: [`SECURITY-AUDIT.md`](SECURITY-AUDIT.md) (the hub) →
  `security/audit-*.md` (rules + cadence, checklists C / D / P / R, annual &
  trigger audits, the audit log).
- **The vulnerability disclosure policy** — how to report a security issue and how
  reports are handled: [`SECURITY.md`](../SECURITY.md) at the repo root (GitHub's
  recognized location), with the machine-readable companion served at
  `GET /.well-known/security.txt`.
- **The operational hardening checklist** — the operator's "what must be true"
  slice: [`ops/hardening-checklist.md`](ops/hardening-checklist.md).
- **The owner-ops ledger** — the single record of the operator steps that must
  run against a live instance, each with a "last verified" cell:
  [`ops/owner-ops-ledger.md`](ops/owner-ops-ledger.md).
