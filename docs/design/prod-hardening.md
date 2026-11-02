---
id: design-prod-hardening
title: "Prod-hardening (the 2026-10-22 audit remediation)"
tier: reference
kind: design
status: current
audiences: [developer, agent, platform-maintainer]
related:
  - docs/adr/0171-least-privilege-db-role.md
  - docs/design/prod-hardening-f1-gdpr-deletion.md
  - docs/design/prod-hardening-f22-role.md
updated: 2026-11-02
---

> **Abstract:** the **one entry point into the prod-hardening lane** — the
> remediation of the **2026-10-22 static security audit** (the 18 `F#` fixes,
> one Part-0 design pin each, F22.U3 the lane's close-flip). **What this is:**
> the map of the 18 findings (by audit row), each to its design pin, so the
> findings and their fixes live in one place. **Who is it for:** the
> platform-maintainer executing the owner-ops legs; a developer executing an
> `F#` U2/U3; an agent that needs the audit-row → fix → design-pin lookup.
> **When to read the rest:** before touching a finding's code seam, an
> owner-ops step, or the ADR 0171 reconciliation (F22).

# Prod-hardening (the 2026-10-22 audit remediation)

The **prod-hardening** lane remediates the 2026-10-22 static security audit.
Each finding is one **Part-0 design pin** (design-only: the exact shape, the
invariants, the pinned test names) that the later units execute against. The
fixes are **independent** (each pin is self-contained) — unlike the other five
subsystem hubs, this lane has no single "base → extension" chain; it is a
**closed set of 18 findings**, each tied to the audit row it closes. F22 is
the lane's last fix (its U3 runs the six-member close-flip) and the one that
resolves the ADR 0004 "app role owns the DB" tension via [ADR 0171](../adr/0171-least-privilege-db-role.md).

| Fix | Audit row | What it closes | Design pin (current truth) |
|---|---|---|---|
| **F1** | DB-13 | complete **GDPR deletion** (no resident-authored content / media bytes / `MediaObject` rows on delete) | [`prod-hardening-f1-gdpr-deletion.md`](prod-hardening-f1-gdpr-deletion.md) |
| **F2** | AUTH-02 | **hash auth tokens at rest** (verify + setup tokens) | [`prod-hardening-f2-token-hash.md`](prod-hardening-f2-token-hash.md) |
| **F3** | AUTH-03 | **MFA for `GlobalAdmin`** (Identity 10.0.13 TOTP + recovery codes) | [`prod-hardening-f3-mfa.md`](prod-hardening-f3-mfa.md) |
| **F4** | BKP-02 | **PITR / WAL archiving** (the RPO is today the dump interval) | [`prod-hardening-f4-pitr.md`](prod-hardening-f4-pitr.md) |
| **F5** | AUTH-04 | **session kill-all + invalidation** (security-stamp neutered today) | [`prod-hardening-f5-sessions.md`](prod-hardening-f5-sessions.md) |
| **F9** | AUTH-15 | **signup bot challenge + disposable-email blocklist** | [`prod-hardening-f9-signup.md`](prod-hardening-f9-signup.md) |
| **F10** | OBS-02 | **error tracker** (Sentry, DSN-gated, PII-scrubbed) | [`prod-hardening-f10-errtrack.md`](prod-hardening-f10-errtrack.md) |
| **F11** | JOB-02 | **alert on dead-letter exhaustion** (the `IAlertSink` seam) | [`prod-hardening-f11-dlq-alert.md`](prod-hardening-f11-dlq-alert.md) |
| **F12** | OBS-06 | **route alerts to a human** (a real `IAlertSink` binding) | [`prod-hardening-f12-alerts.md`](prod-hardening-f12-alerts.md) |
| **F14** | DEP-03 | **CI test gate on `release`** (one workflow file) | [`prod-hardening-f14-ci.md`](prod-hardening-f14-ci.md) |
| **F15** | UPL-04 | **byte-store lifecycle / retention** (the recurring housekeeping job) | [`prod-hardening-f15-lifecycle.md`](prod-hardening-f15-lifecycle.md) |
| **F16** | UPL-02 | **scan uploads before serving** (the `IVirusScanner` seam + quarantine) | [`prod-hardening-f16-scan.md`](prod-hardening-f16-scan.md) |
| **F17** | MAIL-03 | **bounce/complaint suppression** (the `SuppressedAddress` doc) | [`prod-hardening-f17-bounce.md`](prod-hardening-f17-bounce.md) |
| **F18** | MAIL-06 | **bounce/complaint/volume alarms** (mostly owner-ops) | [`prod-hardening-f18-mailalarms.md`](prod-hardening-f18-mailalarms.md) |
| **F19** | TEST-03 | **E2E suite** (signup → verify → login → write, Playwright) | [`prod-hardening-f19-e2e.md`](prod-hardening-f19-e2e.md) |
| **F20** | DEP-04 | **one-step rollback + drilled** (a single runbook) | [`prod-hardening-f20-rollback.md`](prod-hardening-f20-rollback.md) |
| **F21** | SRV-04 | **app↔DB TLS** (a single Web-host env knob) | [`prod-hardening-f21-db-tls.md`](prod-hardening-f21-db-tls.md) |
| **F22** | DB-18 | **demote the app role** (least-privilege; the lane's last fix + close-flip) | [`prod-hardening-f22-role.md`](prod-hardening-f22-role.md) |

**The one fact to keep straight:** each `F#` is **independent** (a self-
contained pin the later units execute against) — the lane's order is the audit
row order, not a base→extension chain. The owner-ops legs (F4, F18, F19, F20,
F21, F22) are **open items, never assumed done** — the pin records the verbatim
checklist; the owner executes it.
