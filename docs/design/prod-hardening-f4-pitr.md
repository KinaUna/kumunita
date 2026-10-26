# F4 — BKP-02: PITR / WAL archiving — Part-0 design pin

> **Fix:** F4 (report row **BKP-02**) — kind **`code+ops`**.
> **Status:** this is the **Part-0 design pin** (F4.U1). It is **design-only**:
> no code, no build, no `.cs` touched. F4.U2 records "no code needed" (this is a
> pure-ops tool — see §7); F4.U3 lands the `docs/OPS.md` §4/§5 update. The
> owner-ops steps in §6 are **open items** — they are *pinned* here so the owner
> executes them verbatim, and are **never assumed done**.
> **Why it's a FAIL today:** the only DB backup is a scheduled `pg_dump -Fc`
> logical dump (OPS §4; hourly via the Coolify backup task on the examplium
> instance, daily the documented floor for a new instance). A logical dump is a
> **point-in-time snapshot**, not a continuous stream — the recovery point
> objective is therefore the **dump interval** (up to **24 h** for a new
> instance). There is no WAL archiving, so there is no point-in-time recovery
> (PITR) between dumps.

---

## 1. Current state (grounded, not assumed)

- **What exists (OPS §4/§5):** a scheduled `pg_dump -Fc` of the app database
  `kumunita`, offsite to object storage (S3 / Backblaze B2 / rclone), with a
  retention example ("7 daily, 4 weekly, 6 monthly") and a quarterly restore
  verification. A **second restore surface** — the media volume (`Media__RootPath`)
  — is snapshotted beside its dump (ADR 0011).
- **What's absent:** any **WAL archiving** and therefore any **PITR**. The RPO is
  the dump cadence, not near-continuous. This is exactly the audit's "RPO up to
  24 h."
- **Topology constraint (drives the tool choice):** in **dev** the Postgres is a
  compose `postgres:18` container (a local volume, not a Coolify addon). In **prod**
  the Postgres is a **Coolify-managed `postgres` addon** on the VPS — the app and
  DB are separate instances, the app's `ConnectionStrings__Kumunita` points at the
  addon's host. The backup is today a **Coolify backup task** against that addon
  (OPS §4 example cron). The ops steps below therefore target the **prod VPS +
  the Coolify Postgres addon**, and must be expressible as addon config + host
  commands, not as a compose-file change to the dev container.
- **Invariants this pin holds:** ADR 0004 (Marten owns the domain documents;
  Postgres is the crown jewels); ADR 0011 (the media volume is a *separate*
  restore surface and is **not** covered by WAL/PITR — see §5). PITR hardens
  **only the Postgres surface**; it does not change the schema-evolution model,
  the module boundaries (ADR 0006), or the "Core stays HTTP-free" rule. It adds
  **no app code path, no new bounded context, no new surface to provision** — it
  is host/DBA infrastructure, which is why F4 is `code+ops` with the code side
  expected to be empty (U2).

## 2. Chosen tool — **pgBackRest** (WAL-G recorded as fallback)

**Pin: pgBackRest.** Rationale, in priority order:

1. **One tool covers the full surface the audit needs** — full + differential
   backups, **WAL archiving**, **point-in-time restore**, built-in **retention
   policy**, and **offsite to S3 / S3-compatible (B2) / SFTP** in a single
   config. The current OPS §4 retention model ("7 daily, 4 weekly, 6 monthly")
   maps directly onto pgBackRest's `repo-retention-full` / `repo-retention-archive`
   knobs with no bespoke cron bookkeeping.
2. **Built-in `pgbackrest check`** validates the backup *chain* end-to-end
   (full → WAL → restorable), which is the concrete, automated form of OPS §4's
   "restore a recent dump to a scratch DB at least quarterly … *a backup you can't
   restore is not a backup*." WAL-G has no single equivalent self-test verb.
3. **Offsite + retention are first-class**, not assembled from `rclone` + `rm`
   crons (which is the shape of the current `pg_dump | rclone rcat` pipeline).
   This removes a class of "the offsite copy is stale / retention ran and deleted
   the wrong segment" failures.

**Recorded fallback — WAL-G.** If the owner prefers the lighter single-binary
footprint (one static binary, no separate `pgbackrest` daemon/package), **WAL-G**
is an acceptable substitute and every step below maps over:
`pgbackrest archive-push` → `wal-g wal-push`; `pgbackrest backup --type=full|incr`
→ `wal-g backup-full|backup-incremental`; `pgbackrest restore --target=…` →
`wal-g time-begin/time-target/time-end` + `restore` (or `wal-g restore` to a
timestamp); retention → `wal-g delete retain 4`. The **decision to record is
pgBackRest as primary**; the owner may choose WAL-G at exec time without
re-opening this pin, as long as §3 (retention/RPO) and §6 (alert-on-failure +
verify) are still met. This pin does **not** depend on which of the two is chosen —
both satisfy the same §3/§6 contract.

## 3. Retention window + RPO target (pinned)

- **RPO target: ≤ 5 minutes** (bounded only by WAL archiving latency — effectively
  continuous once `archive_command` is live). This closes the audit's "RPO up to
  24 h" FAIL: the recovery point is no longer the dump interval.
- **RTO (documented, not a hard number):** bounded by the restore path in §5 —
  a **same-instance** PITR restore (stop app → `pgbackrest restore --target="…"` →
  start) is minutes for a single-neighborhood DB; disaster recovery (new VPS) is
  the existing §5 DR path. No RTO SLA is promised by this pin.
- **Retention (pinned defaults; owner tunes to the same instance):**
  - **Full backups: 4 retained** (`repo-retention-full-type=full`,
    `repo-retention-full=4`) — roughly 4 weekly fulls, matching the OPS §4
    "…4 weekly…" intent.
  - **WAL segments: retained to bridge the gap to the oldest retained full**
    (`repo-retention-archive-type=time`, `repo-retention-archive=14d` as a floor).
    This guarantees you can PITR to **any point within the last full-backup cycle
    plus 14 days** — i.e. PITR coverage ⊇ the full-retention window, so there is
    always at least one restorable full whose WAL chain is intact.
  - **Offsite: the same repo lives in object storage** (S3 / Backblaze B2) — not
    only on the VPS (the current OPS §4 offsite rule, preserved).
- **Cadence (pinned):** one **full** per week + **incremental** (`--type=incr`)
  daily; **WAL archiving continuous** (via `archive_command`, not a cron). The
  daily incremental is optional once continuous WAL is the real recovery source —
  it is kept as a cheap "known-good restorable anchor."

## 4. Why this is a `code+ops` fix with an empty code side

The recovery mechanism is **entirely Postgres + host-side** (archive command,
backup repo, retention, offsite). The Kumunita **application** has no code path
that participates in WAL archiving or PITR — the app neither reads nor writes the
WAL stream; Postgres and pgBackRest do. Therefore:

- **F4.U2 = "no code needed."** There is no app-side knob, health-probe field, or
  DI seam that PITR requires. (Contrast F10–F12, which add an app-side alert
  sink; F4's alert-on-failure in §6 is a **host cron** calling `pgbackrest check`
  and forwarding the result — it reuses the **F12 human alert channel**, which is
  itself a `code+ops` fix; F4 does not build the channel.)
- **F4.U3 = the `docs/OPS.md` update** (the code-visible artifact of F4): add the
  PITR procedure, the retention/RPO numbers from §3, the "last verified" line
  (OPS's "Rules of the road"), and the alert-on-failure cron from §6 — into
  **§4 (Backups)** and **§5 (Restore)** — and record the owner-ops step as an
  **open item**.

## 5. Scope boundary — the media volume is NOT part of PITR

**Explicit, so U3's OPS edit doesn't blur it:** PITR/WAL archiving covers the
**Postgres surface only**. The **media volume** (`Media__RootPath`, ADR 0011) is a
separate byte store whose recovery is the existing **volume snapshot beside the
dump** (OPS §4 "Second restore surface"). pgBackRest does **not** snapshot that
volume, and PITR-ing Postgres to a point in time does **not** rewind the media
volume — a half-restore (Postgres at `T`, media volume at `T'`) still degrades
safely exactly as OPS §4/§5 already document (a missing `MediaObject` byte is a
re-hydratable avatar `404`, the content hash is the integrity key). **U3 must
preserve, not replace, the existing media-volume snapshot procedure** and add the
PITR procedure **alongside** it.

## 6. Owner ops steps (pinned verbatim — the `ops` half of F4)

> These are **open items** — recorded, **not assumed done**. The owner (host
> operator role, OPS header) executes these against the **prod VPS** + the
> **Coolify Postgres addon**. Steps assume pgBackRest (the §2 primary); the WAL-G
> equivalents are in §2's fallback note. Placeholder `<instance>` / `<repo>` match
> the existing OPS §4 `backup:kumunita/<instance>/…` naming.

**6.1 — Install the tool on the VPS (once).**
```bash
# pgBackRest (Debian/Ubuntu VPS example):
sudo apt-get install -y pgbackrest
pgbackrest --version
```
> If the owner chooses the §2 fallback, install the `wal-g` static binary instead
> and skip to the WAL-G equivalents.

**6.2 — Configure the backup repo (offsite to object storage).**
Create `/etc/pgbackrest/pgbackrest.conf` (or the owner's equivalent), `standby`
unnecessary (single node), targeting the **app database `kumunita`** and the
existing offsite object storage the `pg_dump` already ships to:
```ini
[<instance>]
db-host=127.0.0.1
db-port=5432
db-user=kumunita
db-database=kumunita
repo1-path=/var/pgbackrest/<instance>            # local staging repo
repo1-s3-bucket=kumunita-backups                  # offsite (S3 / B2)
repo1-s3-endpoint=<b2 or s3 endpoint>             # B2: https://s3.<region>.backblazeb2.com
repo1-s3-key=<access key>
repo1-s3-key-secret=<secret>
repo1-type=s3                                       # keep the canonical copy offsite
repo1-retention-full-type=full
repo1-retention-full=4
repo1-retention-archive-type=time
repo1-retention-archive=14d
process-max=2
```
> Credentials in the config are secret — store them per the existing env-secret
> discipline (OPS "Config is state too"), not in the repo.

**6.3 — Enable WAL archiving on the Postgres (the core BKP-02 fix).**
On the **Coolify Postgres addon**, set `archive_command` so every completed WAL
segment is pushed the moment it's written (this is what collapses RPO from 24 h
to ≤ 5 min). Via the addon's config (Coolify "Postgres config" / `postgresql.conf`)
or `ALTER SYSTEM`:
```ini
archive_mode = on
archive_command = 'pgbackrest --stanza=<instance> archive-push %p'
```
Restart/reload the addon so the command takes effect. **Verify it is live:**
```bash
pgbackrest --stanza=<instance> archive-push --dry-run   # or: inspect recent pgbackrest log for archive-push lines
```
> **This is the single most important step** — until `archive_command` is live,
> the RPO is *still* the dump interval and the FAIL is not closed.

**6.4 — Create the stanza + take the first full backup.**
```bash
pgbackrest --stanza=<instance> stanza-create
pgbackrest --stanza=<instance> backup --type=full
pgbackrest --stanza=<instance> info
```

**6.5 — Set the cadence (cron on the VPS; mirrors the existing Coolify task).**
```cron
# Full backup weekly (Sunday 03:30):
30 3 * * 0  pgbackrest --stanza=<instance> backup --type=full
# Incremental anchor daily (03:30) — cheap known-good restorable point:
30 3 * * *  pgbackrest --stanza=<instance> backup --type=incr
# Retention enforcement (pgBackRest also enforces at backup time; this is belt-and-suspenders):
0 4 * * 0   pgbackrest --stanza=<instance> backup --type=full --prune
```
> Alternatively keep the existing **Coolify backup task** as the cadence driver and
> point it at `pgbackrest backup` — the pin does not mandate cron vs. Coolify task,
> only that a full + the continuous WAL archiving both happen.

**6.6 — Verify (automated, quarterly at least; the OPS "last tested" contract).**
```bash
pgbackrest --stanza=<instance> check          # validates full→WAL chain is restorable
```
Update OPS §4/§5 "Last backup verified" / "Last tested" on every run (OPS "Rules
of the road": *update the date when you run a step*).

**6.7 — Alert-on-failure (route to a human — reuses the F12 channel).**
Wrap the cadence commands so a **non-zero exit is escalated**, not silently
logged. Pin the shape (the actual channel is F12's `code+ops` human sink):
```cron
# On any failure, emit an alert to the F12-configured human channel (e.g. mail/SMSEmail/Sentry alert rule):
30 3 * * 0  pgbackrest --stanza=<instance> backup --type=full || <F12 alert-sink: "pgbackrest full backup FAILED on <instance>"
0  4 * * *  pgbackrest --stanza=<instance> check        || <F12 alert-sink: "pgbackrest check FAILED on <instance>"
```
> **Open item (owner):** point the failure handler at the **F12** human alert
> channel (F12 is the lane that *builds* that channel; F4 only reuses it). Do
> **not** assume the channel exists — it is F12's deliverable.

**6.8 — PITR restore (the procedure F4.U3 lands into OPS §5).**
The owner's one-step "recover to a point in time" path (same instance):
```bash
# 1. Stop the app (Coolify).
# 2. Pick the target instant (e.g. 10 minutes before the bad write):
pgbackrest --stanza=<instance> restore --type=time --target="2026-10-23 14:50:00+00"
#    (or --latest to take the newest restorable point)
# 3. Restart Postgres, then the app; verify /health + login + a representative page (OPS §5 step 3).
```
> **Media volume note (§5):** a PITR restore does **not** rewind the media
> volume. If the media volume snapshot is from a different instant, the half-
> restore degrades safely per the existing OPS §4/§5 (missing bytes = re-
> hydratable `404`). U3 must keep the existing media-volume restore steps and
> add this PITR block alongside them.

## 7. F4.U2 — code support (expected: **none**)

**Record for the next unit:** F4 is a pure-ops tool for this codebase. There is
**no app-side knob, health-probe field, or DI seam** that WAL archiving / PITR
requires — the app neither reads nor writes the WAL stream. F4.U2 therefore
records **"no code needed"** (per the plan's own allowance: *"If pure-ops, U2 =
'no code needed' recorded."*). The only code-visible artifact of F4 is the
**`docs/OPS.md` update** in F4.U3. Do **not** invent an app-side config knob to
"make it code" — that would be a drive-by outside F4's pinned scope.

## 8. Explicit non-goals / out of scope for F4

- **Media-volume lifecycle** — that is **F15** (UPL-04) and the existing OPS §4
  snapshot; F4 does not touch it (see §5).
- **The human alert channel itself** — that is **F12** (OBS-06); F4 only reuses it
  in §6.7 and records it as an **open item**, never assuming it exists.
- **Rollback-as-restore** — the *app* rollback runbook is **F20** (DEP-04), which
  depends on F4; F4 provides the PITR primitive F20's "one-step rollback" restore
  step will call.
- **A new bounded context / surface / schema** — none. F4 adds none (ADR 0006
  preserved).

## 9. Exit contract for this pin (F4.U1)

- This doc exists and pins the **3 required items**: (1) the tool — **pgBackRest**
  (WAL-G fallback, §2); (2) the retention window / RPO — **RPO ≤ 5 min**,
  **4 fulls + 14 d WAL floor**, offsite (§3); (3) the **exact owner ops steps**
  (§6.1–§6.8, verbatim).
- The F4.U2 expectation (**"no code needed"**, §7) is recorded so the next unit
  does not invent code.
- The **open items** (§6.7 F12 channel, §6 all steps as owner-executed) are
  **recorded, not assumed done**.
- **No `src/` edit, no `<PackageReference>`, no migration, no `.slnx` change,
  no build run** for this unit.
