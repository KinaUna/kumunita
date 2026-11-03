---
id: ops-monitoring
title: "Monitoring & health"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/ops/email-smtp.md
  - docs/ops/privacy-operations.md
updated: 2026-11-02
---

> **Abstract:** the standing watch on a live instance — `/health`, external
> uptime, logs, capacity, and the periodic access-audit review. **Who is it
> for:** the host operator. **When to read the rest:** when setting up (or
> auditing) the watch on a new instance.

# Monitoring & health

- **Liveness:** `/health` endpoint (Coolify restarts on failure). Also reports
  **degraded** (not failed) when `mt.email_dead_letters` is non-empty — the app is up
  but email is not going out; act per the [email (SMTP) runbook](email-smtp.md) dead-letter section.
- **External uptime:** a cheap checker on the public URL → alert on failure (Uptime Kuma or
  similar). This is your "is it even up" signal.
- **Logs:** structured logs to stdout; Coolify captures. Grep for `error`/`unhandled` after
  incidents.
- **Capacity:** watch disk (backups grow) and Postgres size; alert on thresholds.
- **Review:** the access audit log (GlobalAdmin, in-app) periodically — see [data & access operations](privacy-operations.md).

## Related

- [Email (SMTP)](email-smtp.md) — the dead-letter runbook this page points at.
- [Incident response](incident-response.md) — what to do when the watch fires.
