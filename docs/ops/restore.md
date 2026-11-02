---
id: ops-restore
title: "Restore"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/ops/backups.md
  - docs/ops/provision.md
  - docs/adr/0011-media-and-file-storage.md
updated: 2026-11-02
---

> **Abstract:** the three restore paths — same-instance corruption, point-in-time
> (once [backups](backups.md)' `archive_command` is live), and disaster recovery
> on a fresh VPS — plus the media-volume leg and the `postgres`-superuser
> rule. **Who is it for:** the host operator, mid-incident. **When to read the
> rest:** when data is affected and you're choosing which path.

# Restore

**Same instance (corruption / bad data):**
1. Stop the app (Coolify).
2. Restore: `pg_restore … < dump.dump` — **connect as `postgres`** (superuser) while
   targeting the app database; the `kumunita` role cannot drop/recreate a database it is
   itself connected *as* (the first live restore — both roles `kumunita` — failed on
   exactly this).
3. Start the app; verify `/health`, login, and a representative page.

**Point-in-time (PITR, [backups](backups.md) — recover to a point in time, same instance):** the
one-step path, available once the backups page's `archive_command` is live — restores
the **Postgres surface only** (the media volume is a separate surface and is not
rewound — restore it from its own snapshot per the rule below):
1. Stop the app (Coolify).
2. Pick the target instant (e.g. 10 minutes before the bad write):
   `pgbackrest --stanza=<instance> restore --type=time --target="2026-10-23 14:50:00+00"`
   (or `--latest` for the newest restorable point).
3. Restart Postgres, then the app; verify `/health`, login, and a representative
   page (step 3 above).

   **Last tested (PITR restore):** _open item — owner stamps the date when run;
   never assumed._

**Disaster recovery (new VPS):**
1. Provision a fresh instance ([provision](provision.md)) with the **same** `Community__Name` and config.
2. Restore the DB (and re-apply the backed-up env set, per the [configuration reference](configuration-reference.md)) into it.
3. **Version compatibility:** restore a dump into a compatible app version — check the image
   tag the backup was taken under before proceeding (the forward-only rule in
   [upgrade](upgrade.md)).
4. Re-point DNS; verify email + TLS + `/health`
   ([TLS & domain](tls-domain.md), [email (SMTP)](email-smtp.md)).

**The media volume too (ADR 0011, second surface):** if the backup set
includes the media snapshot, it lands at `Media__RootPath` **before the app
starts** — same instance: restore the tarball as step 2.5 (between `pg_restore`
and start); disaster recovery: attach a volume to the new instance (procedure
1) and extract the tarball into its `Media__RootPath`. A half-restore (surviving
on one surface only) presents as avatar `404`s and is re-hydratable by the
owner re-uploading (the hash is the integrity key — [backups](backups.md)).

## Related

- [Backups](backups.md) — the backup set this page restores.
- [Upgrade](upgrade.md) — the R3 leg that reuses this page's PITR path.
- [Incident response](incident-response.md) — the triage that routes here.
