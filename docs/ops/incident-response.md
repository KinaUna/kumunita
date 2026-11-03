---
id: ops-incident-response
title: "Incident response (basic)"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/ops/restore.md
  - docs/ops/privacy-operations.md
  - docs/SECURITY.md
updated: 2026-11-02
---

> **Abstract:** the six-step incident response (detect → triage → contain →
> recover → communicate → record) plus the incident log table the owner
> keeps. **Who is it for:** the host operator, and whoever is on duty when
> something goes wrong. **When to read the rest:** before an incident, so
> the steps are memorized; during one, for the exact order.

# Incident response (basic)

1. **Detect** — uptime alert, resident report, or logs ([monitoring](monitoring.md)).
2. **Triage** — is it *down* or is *data at risk*? Data risk takes priority.
3. **Contain** — stop the app; block the offending user; **rotate credentials** if
   compromise is suspected.
4. **Recover** — restore from backup ([restore](restore.md)) if data is affected.
5. **Communicate** — if residents' data was exposed, tell them (a privacy duty, not optional —
   the privacy model in [`SECURITY.md`](../SECURITY.md)).
6. **Record** — append to the incident log below.

**Incident log**

| Date | What happened | Impact | Action taken | Follow-up | Who |
|------|---------------|--------|--------------|-----------|-----|
|      |               |        |              |           |     |

## Related

- [Restore](restore.md) — the recovery procedure this step 4 calls.
- [Data & access operations](privacy-operations.md) — the resident-facing actions (suspend, delete, hand over) that follow a triage.
