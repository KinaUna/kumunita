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
  - docs/adr/0298-data-classes-in-code.md
  - docs/adr/0299-instance-export-operator-gated.md
updated: 2026-11-08
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
- **Full instance export** (whole-neighborhood migration, or a legal hold where
  the private content must move) → the content-free export is the everyday
  default (a GlobalAdmin, one audit row, nothing residents see); the **full**
  scope is an operator act and is the right tool only when the private content
  (messaging threads, audience-restricted rows, profile contact fields) has to
  leave with the archive. It is a three-part break-glass, **operator at the DB
  first**, per [ADR 0299](../adr/0299-instance-export-operator-gated.md):
  1. **Grant + consume the override** — the operator writes the DB-level
     `AdminOverride` row above (no in-app endpoint creates one;
     [ADR 0003](../adr/0003-roles-and-moderator-scoping.md)), and the
     GlobalAdmin who will run the export **consumes** it by entering the token
     at `/admin/break-glass` (break-glass step 3). The gate
     (`BreakGlassProbe.HasActiveAsync`) is satisfied only while that row is
     **consumed and unexpired** at the moment the export runs — the full scope
     is refused (403) otherwise.
  2. **Run the export** — the GlobalAdmin opens the portability surface
     (`/admin/portability`) and chooses the **full** scope (the second button;
     the content-free button is the default and needs no override). The audit row
     records `Action = portability.export.full`, `Via = BreakGlass`.
  3. **Confirm it landed** — a transparency notification
     (`kind: instance.full-export`, the D10 mechanism in
     [ADR 0299](../adr/0299-instance-export-operator-gated.md) §4) reaches
     every non-guest resident's inbox, and the `portability.export.full` /
     `Via: BreakGlass` row is in the audit log. **Record the reason** in the
     incident log as in step 4 above.

  This is distinct from the **resident's own** export above (that is the
  resident's data out; this is the whole instance out) — do not conflate them.

## Related

- [`SECURITY.md`](../SECURITY.md) — the privacy model and the access-control rules these operations enforce.
- [Hardening checklist](hardening-checklist.md) — the 2FA bar this page's step 3 enforces.
- [Incident response](incident-response.md) — where the break-glass reason is recorded.
