---
id: ops-backups
title: "Backups"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/ops/restore.md
  - docs/ops/upgrade.md
  - docs/adr/0011-media-and-file-storage.md
  - docs/adr/0171-least-privilege-db-role.md
updated: 2026-11-02
---

> **Abstract:** the backup story for a live instance — the hourly `pg_dump`
> (the `kumunita_migrate` role), the second restore surface (the media
> volume), and the PITR/WAL-archiving procedure with its RPO, cadence,
> retention and self-test contracts. **Who is it for:** the host operator.
> **When to read the rest:** before your first backup, at the quarterly
> restore test, and before any upgrade.

# Backups

**What:** the Postgres DB is the crown jewels (all Marten documents + Identity). Back it up.

- **Format:** `pg_dump -Fc` (custom format — fast restore, compression).
- **Schedule:** the `postgres` addon on the VPS is backed up **hourly via the Coolify
  backup task** (examplium instance; daily is the documented floor for a new instance).
  The dump target is the app database `kumunita` — owned by the **`kumunita_migrate`**
  role (the two-role split, **ADR [0171](../adr/0171-least-privilege-db-role.md)** / F22: `kumunita_migrate` owns the DB + the
  `mt` / `identity` schemas and carries DDL; the demoted `kumunita` app role is DML),
  so it can be taken by `kumunita_migrate` or `postgres`. (Example host cron, if operating
  without Coolify's task: `0 3 * * * pg_dump -Fc -h 127.0.0.1 -U kumunita_migrate kumunita
  | rclone rcat backup:kumunita/<instance>/$(date +%F).dump`.)
- **Offsite:** to object storage (S3 / Backblaze B2 / rclone) — **not** only on the same VPS.
- **Retention:** e.g. 7 daily, 4 weekly, 6 monthly (tune to need).
- **Config:** include the instance's env set (encrypted) in the same backup set — see the
  [configuration reference](configuration-reference.md) ("Config is state too").
- **Verify:** restore a recent dump to a scratch DB at least quarterly. *A backup
  you can't restore is not a backup.* Update "Last backup verified" in the inventory.

**Second restore surface — the media volume (ADR [0011](../adr/0011-media-and-file-storage.md)):** the *byte payload* of
avatars lives on the dedicated volume at `Media__RootPath`, not in Postgres; the
dump carries only the catalog (the `MediaObject` rows). The **attachment
(payload bytes of the ADR [0034](../adr/0034-file-attachments-posts-replies-announcements.md) download lane) live on this same volume** — one
volume, two route families (`/content-image/{id}` + `/attachment/{id}`), one
catalog, one snapshot (C-ATT·1; C-MED·7 unchanged — no new restore surface).
So:

- **Snapshot the volume in the same backup set as its dump** — a
  `media-<date>.tgz` of `Media__RootPath`, taken just before that `pg_dump`
  (so the snapshot brackets the dump); offsite + retention exactly like the
  dump.
- **Verify:** at the quarterly restore test, restore the media tarball too
  (extract to a scratch dir and confirm the `{hash[0..2]}/{hash}` trees
  round-trip) — a volume snapshot without its matching Postgres snapshot (or
  vice versa) is an **inconsistent restore**.
- **A half-restore still degrades safely** (the content hash is the integrity
  key): a `MediaObject` with no file manifests as an avatar `404` and is
  **re-hydratable** — the owner re-uploads the same bytes, the hash matches,
  and the existing catalog row is reused (nothing duplicated); an orphan file
  (file with no doc) is **inert** — nothing references a bare hash. The
  failure mode is a missing picture, never cross-contamination.

**PITR — WAL archiving + point-in-time recovery (BKP-02 fix):** a logical
`pg_dump` is a point-in-time *snapshot*, so the recovery point of the procedure
above is the **dump interval** (up to 24 h). PITR closes that gap: the recovery
point is no longer the dump cadence, it is the WAL stream.

- **Tool:** **pgBackRest** (full + differential backups, WAL archiving, PITR,
  built-in retention, offsite to S3/B2 in one config). **WAL-G is the recorded
  fallback** (single static binary; every step maps over: `archive-push` →
  `wal-g wal-push`, `backup` → `backup-full|backup-incremental`, `restore
  --type=time` → `restore` to a timestamp, retention → `wal-g delete retain 4`)
  — either may be chosen at exec time, provided the RPO/retention contract below
  and the alert-on-failure contract still hold.
- **RPO: ≤ 5 minutes** (bounded only by WAL-archiving latency — effectively
  continuous once `archive_command` is live). RTO: minutes for a same-instance
  PITR restore; disaster recovery is the [restore](restore.md) DR path.
- **Cadence:** one **full** per week + an **incremental** daily (a cheap
  known-good restorable anchor) + **continuous** WAL archiving (via
  `archive_command`, not a cron).
- **Retention (defaults; tune to need):** **4 fulls retained**; **WAL segments
  retained to bridge the gap to the oldest retained full** (14-day floor) — so
  PITR coverage always spans the full-retention window. Offsite to the **same
  object storage** the `pg_dump` ships to — not only on the VPS.
- **Self-test (automated, at least quarterly):**
  `pgbackrest --stanza=<instance> check` — validates the full → WAL chain is
  restorable; this is the concrete form of the "a backup you can't restore is
  not a backup" rule above. Update the **"Last tested"** date below on every run.
- **Alert on failure:** wrap the cadence + `check` commands so a **non-zero exit
  is escalated to a human**, not just logged — the failure handler routes to the
  **F12 human alert channel** (F12 is the lane that *builds* that channel; this
  step **reuses** it and does not build it).
- **Scope boundary — Postgres surface only:** PITR/WAL archiving covers the
  **Postgres surface only**. The **media volume** (`Media__RootPath`, ADR [0011](../adr/0011-media-and-file-storage.md))
  is a **separate** restore surface — pgBackRest does not snapshot it, and
  PITR-ing Postgres to a point in time does **not** rewind it. The media-volume
  snapshot/restore procedure above is **preserved unchanged**; a half-restore
  (Postgres at `T`, media volume at `T'`) degrades safely per the rule above
  (a missing `MediaObject` byte is a re-hydratable avatar `404`).
- **Owner ops (open items — recorded, not assumed done):** install the tool on
  the VPS; configure the backup repo (offsite); enable `archive_mode=on` +
  `archive_command='pgbackrest --stanza=<instance> archive-push %p'` on the
  Postgres (the single most important step — until it is live the RPO is still
  the dump interval); `stanza-create` + first full; set the cadence (cron or
  the existing Coolify backup task); run the `check` self-test; wire the
  alert-on-failure handler to the F12 channel. The PITR restore path in
  [restore](restore.md) is the procedure these steps make available.

**Last tested (PITR):** _open item — owner stamps the date when a PITR restore
or `pgbackrest check` is run on a live instance; never assumed._

## Related

- [Restore](restore.md) — the procedures these backups make available.
- [Upgrade](upgrade.md) — the procedure that takes a fresh backup in step 2 and may call the PITR restore leg.
- [Configuration reference](configuration-reference.md) — the env set that ships in the same backup set.
