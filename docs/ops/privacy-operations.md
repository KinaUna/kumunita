---
id: ops-privacy-operations
title: "Data & access operations (privacy)"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer, community-admin]
related:
  - docs/SECURITY.md
  - docs/adr/0003-roles-and-moderator-scoping.md
  - docs/adr/0142-delete-account.md
updated: 2026-11-02
---

> **Abstract:** the resident-facing data operations — viewing the audit log,
> retention, suspend / revoke, export, deletion (with the
> pseudonymization nuance), hand-over-admin, and the DB-level
> break-glass elevation. **Who is it for:** the community **GlobalAdmin**
> for most steps, the host operator for the DB-level ones. **When to read
> the rest:** when a resident asks for their data, or when an admin is gone.

# Data & access operations (privacy)

Most of this is an **in-app GlobalAdmin** action; a few need the operator at the DB.

- **View who accessed private content** → GlobalAdmin opens the audit log (always-on by
  design; see [ADR 0003](../adr/0003-roles-and-moderator-scoping.md)).
- **Audit retention:** `AccessAudit` is purged on a schedule (see [ARCHITECTURE](../ARCHITECTURE.md) §5):
  routine restricted-content Allow/Deny rows after ~90 days; report- and
  moderator/admin-access rows kept until the report resolves (+90 days). The cutoff is
  per-instance config. The purge job writes an `AuditPurge` summary row (count, cutoff,
  at) — spot-check after a run, and note that a deleted account's rows are
  **pseudonymized, not purged** (below).
- **Suspend a resident** (abuse) → GlobalAdmin disables the account in-app (immediate).
- **Revoke a moderator** → GlobalAdmin removes the role / component scope in-app.
- **Resident requests data export** → self-serve in-app once the export service ships
  (deferred). **Until then (manual):** operator extracts that user's records to a file and
  delivers it securely; log the action.
- **Resident requests account deletion** →
  1. Disable the account in-app immediately.
  2. Operator hard-deletes the user's personal data (profile, groups they own, grants).
  3. **Audit retention nuance:** keep `AccessAudit`/`Report` rows but **pseudonymize** the
     subject (replace the user id with a tombstone) rather than deleting — you may need to
     prove what happened without retaining personal data. Confirm the policy before acting.
     See [ADR 0142](../adr/0142-delete-account.md) (the design record behind this procedure).
- **Hand over admin** → promote a resident to GlobalAdmin, then demote the outgoing one.
  **Two GlobalAdmins should be the standing state** (each can demote the other), so a
  single departure is never a lockout.
- **Break-glass elevation** (admin gone, locked out, or hostile) → **operator-only, DB
  level**, since the app trusts its own admins:
  1. Choose the account to elevate (an existing verified resident, ideally the backup
     admin). Note: a hostile admin cannot do this *to themselves* — no in-app endpoint
     creates an `AdminOverride` ([ARCHITECTURE](../ARCHITECTURE.md) §4.5).
  2. Insert directly into Postgres (as the DB operator user, not the app user):
     `INSERT INTO mt."AdminOverride" (id, userId, token, grantedAt, expiresAt)
      VALUES (gen_random_uuid(), '<target-user-id>', '<strong-one-time-token>',
              now(), now() + interval '6 hours');`
  3. Tell the target: log in, go to `/admin/break-glass`, enter the token. Consumption
     elevates them to GlobalAdmin until `expiresAt`; the app appends
     `AccessAudit (via: BreakGlass)` rows for the elevation and every subsequent
     privileged action. **Before or as part of elevation, ensure the account has 2FA**
     ([hardening checklist](hardening-checklist.md)) — an elevated account that lacks 2FA is a standing
     admin that does not meet the admin bar.
  4. When done, verify the override has expired (or delete the row) and review the
     `BreakGlass` audit rows. **Record the reason** in the incident log
     ([incident response](incident-response.md)) — the DB
     write itself is invisible to the app's audit trail.

## Related

- [`SECURITY.md`](../SECURITY.md) — the privacy model and the access-control rules these operations enforce.
- [Hardening checklist](hardening-checklist.md) — the 2FA bar this page's step 3 enforces.
- [Incident response](incident-response.md) — where the break-glass reason is recorded.
