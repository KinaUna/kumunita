---
id: ops-email-smtp
title: "Email (SMTP)"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/ops/monitoring.md
  - docs/ops/first-boot.md
updated: 2026-11-02
---

> **Abstract:** the SMTP posture for a live instance — provider choice,
> SPF/DKIM/DMARC deliverability, the dev-time Mailpit inbox, and the
> dead-letter runbook (incl. the manual-verify safety valve). **Who is it
> for:** the host operator. **When to read the rest:** before first send,
> or when `/health` reports **degraded**.

# Email (SMTP)

- **Production:** a transactional provider (Resend / Postmark / SES / Mailgun) or the
  community's own mail server. Choose one and record it per instance
  ([configuration reference](configuration-reference.md)).
- **Deliverability (matters for account verification):** on the sending domain, set
  **SPF, DKIM, and DMARC**, and use a stable `From:` address (e.g. `no-reply@<domain>`).
  Without these, verification/reminder emails land in spam and look broken.
- **Dev:** Mailpit — open its web inbox to inspect what would be sent
  ([environments](environments.md)).
- **Reliability (see [ARCHITECTURE](../ARCHITECTURE.md) §6.2):** all email flows through one durable
  handler; a transient SMTP failure never blocks a domain write or an audit row.
  After **6 attempts over ~24 h** the message is dead-lettered to
  `mt.email_dead_letters` and `/health` reports **degraded**.
- **Dead-letter runbook:** check `mt.email_dead_letters` (idempotency key, recipient,
  last error, attempts). Once SMTP is healthy, re-queue the row (reset the key to a
  fresh outbox message) or discard. **Verification emails are load-bearing:** if SMTP
  is down for a long stretch, a new resident is stuck unverified — a **GlobalAdmin
  can verify a resident manually** (in-app, audited) as the safety valve.

## Related

- [Monitoring & health](monitoring.md) — the `/health` degraded signal this runbook acts on.
- [First boot & admin setup](first-boot.md) — where verification email is the acceptance check.
