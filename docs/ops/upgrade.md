---
id: ops-upgrade
title: "Upgrade the application"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/ops/backups.md
  - docs/ops/restore.md
  - docs/adr/0004-data-persistence-and-schema-evolution.md
  - docs/design/prod-hardening-f20-rollback.md
updated: 2026-11-02
---

> **Abstract:** the upgrade procedure — deploy from the `release` branch, the
> forward-only schema rule, and the **one-step rollback** (the R1
> schema-compat guard, the R2 app-only leg, the R3 DB-restore leg, and the
> dated drill). **Who is it for:** the host operator. **When to read the
> rest:** before every upgrade, and top-to-bottom when rolling back.

# Upgrade the application

Production deploys from the **`release` branch only** — `main` never deploys. Coolify
builds the image from the selected commit, so the git state *is* the image version; there
is no registry, and the inventory's "Version (Commit)" comes from the deployed app's
`/health` → `build` (the `SOURCE_COMMIT` env var).

1. Announce a maintenance window to residents.
2. **Take a fresh backup** ([backups](backups.md)) and note the current **commit SHA** (from
   `/health` → `build`, or Coolify's deploy history) — this is the rollback target.
3. Merge the tested `main` tip into **`release`** (which Coolify deploys from). Deploy.
4. On boot, pending **versioned migrations** are applied — the Marten storage features
   (`FeatureSchemaBase` / doc-type changes) are **delta-detected against the live `mt`
   catalog** and applied idempotently (Weasel-feature pattern: no `mt.migrations` ledger —
   ADR 0004 §B) and Identity migrations run against `identity.__EFMigrationsHistory`;
   check the logs confirm the steps ran. Schema evolution is forward-only and lives in the
   image, so the new image must always carry a superset of the running schema's steps
   (ADR 0004).
5. Smoke test: home page renders, `/health` OK; the `build` SHA matches the deployed
   commit. (The full smoke set — login, create a post, open a component — exists from M1/M3.)
6. **Rollback (if needed):** reselect the **previous commit SHA** recorded in step 2 in
   Coolify and deploy it. App rollback is easy; **DB
   rollback requires a restore** ([restore](restore.md)) because schema changes are forward-only.
7. Record the new **commit SHA** + date in the inventory.

**One-step rollback (DEP-04 — this block is the procedure):**
the app reselect (step 6 above) and the DB restore ([restore](restore.md)) are **two halves of
one procedure**, not two separate ones. Read this block top-to-bottom when you
need to roll back — it is the "one-step" the 2026-10-22 audit found missing:
*one runbook the owner reads and executes*, not one command. The mechanism
(Coolify reselect + PITR restore) already exists; what was absent is this
ordered, guarded, drilled, dated shape around it.

**R1 — the schema-compat guard (run BEFORE reselecting the SHA):**
the forward-only rule (ADR 0004 §B, step 4 above) constrains the *forward*
direction (the new image must be a superset). This guard constrains the
*rollback* direction — does the **old** image tolerate the **current**
schema?

1. Enumerate the commits in the rolled-back window:
   `git --no-pager log --oneline <rollback-target-sha>..HEAD`.
2. For each, check the **Marten surface** (`Kumunita.Core`
   `FeatureSchemaBase` / doc-type registration — `CoreDocTypes` /
   `PostDocTypes`, `DependencyInjection.cs`) **and** the **EF surface**
   (`Kumunita.Web/Migrations/*`) for a schema change.
3. **Classify** each change: **additive** (new column with a default, new
   table, new doc type, additive-only EF migration) vs **breaking**
   (dropped/renamed column, column type change, reshaped doc the old image
   reads).
4. **Decision gate:** *all* additive → **R2** (app-only rollback).
   *Any* breaking → **R3 first** (DB-restore leg), **then** R2.

> **Recorded risk (if the guard is skipped):** rolling an old image over a
> breaking schema = a boot failure (the old image can't read the reshaped
> `mt`/`identity` surface) — the app is **down**, not merely degraded. This
> guard converts that surprise outage into a deliberate "app + DB" rollback
> with a known restore target.

**R2 — the app-only rollback (the one-step app leg):**
1. **Name the rollback target SHA** — the "Version (Commit)" from step 2
   (the pre-upgrade SHA, from `/health` → `build` / the `SOURCE_COMMIT`
   env) and/or the Instance inventory row.
2. **Prod (Coolify, primary — the only prod path, §3 intro: no registry):**
   reselect that commit SHA in Coolify and deploy. Coolify rebuilds the image
   from that commit (the commit *is* the version). On boot, the old image's
   Marten delta-detection runs against the current `mt` catalog; because the
   schema is forward-only and the old image's feature set is a subset, it is
   a **no-op** — provided the R1 guard held (all additive).
3. **Local / compose (equivalent):**
   ```bash
   git checkout <rollback-target-sha> && docker compose build app && docker compose up -d app
   ```
   (the AGENTS.md rebuild idiom applied to the previous commit; wait ~8 s for
   the Marten schema + Wolverine leadership boot.)
4. **Verify:** `/health` OK, `build` SHA == the target SHA, login works, and
   one representative page renders (step 5 smoke set — home / one post / one
   component, the M1/M3 smoke set).

**R3 — the DB-restore leg (when R1 found a breaking change):**
this leg **reuses the backups page's PITR** ([backups](backups.md) PITR block / [restore](restore.md) PITR restore) — it is not a
new procedure, it *calls* the existing one. The exact sequence:
1. Stop the app (Coolify).
2. Restore the Postgres surface — one of two targets, chosen by the backups page's state:
   - **PITR point (preferred; the backups page's `archive_command` live):**
     `pgbackrest --stanza=<instance> restore --type=time --target="<T>"`
     where `<T>` is the instant **before** the bad deploy/write (e.g.
     `--target="2026-10-23 14:50:00+00"`), **or** `--latest` for the newest
     restorable point. **WAL-G fallback** (the backups page's fallback tool): the `wal-g`
     restore-to-timestamp equivalent.
   - **Last `pg_dump` (fallback; WAL archiving not yet live):** the
     [restore](restore.md) same-instance path — `pg_restore … < dump.dump` **as `postgres`**
     (superuser; the `kumunita` role cannot drop/recreate the DB it is
     connected *as*). The backups page's PITR only narrows the RPO; this leg works **with or
     without** it.
   **The restore connects as the `postgres` superuser** — the ADR 0004
   "app role owns the DB" consequence (the `kumunita` role cannot
   drop/recreate the DB it is connected *as*).
3. **Restore the media volume too, if bracketed** (ADR 0011, [backups](backups.md) "Second
   restore surface"): the DB restore does **not** rewind the media volume
   (`Media__RootPath`). If the media snapshot brackets the dump, restore it
   as **step 2.5** — between the DB restore and app start. A half-restore
   (Postgres at `T`, media volume at `T'`) degrades safely (a missing
   `MediaObject` byte is a **re-hydratable avatar `404`**; the content hash
   is the integrity key). This leg **reuses the existing** media-volume
   procedure; the one-step rollback does not change it.
4. Restart Postgres, then the app (now at the old SHA from R2). **Verify**
   (the R2 verify set).

**The drill procedure (dated, repeatable, owner-run — the "Last drilled" record
the audit found absent):**
the owner runs this on a **throwaway prod-style instance** (the [environments](environments.md)
"No shared staging cluster" idiom — a throwaway prod-style instance is enough to verify
an upgrade), *not* the live instance:

- **Precondition:** the throwaway instance is at a **known current** SHA with
  a representative schema; a **previous** SHA's image is available (Coolify
  deploy history / a `git checkout` target); and a **fresh backup / PITR
  anchor** is present (the backups page's PITR stanza + first full, or a `pg_dump`).
- **A — app-only rollback (R2):** reselect the previous SHA, deploy, boot.
  **Verify:** `/health` OK, `build` SHA == the previous SHA, **login**
  works, and **one representative page** renders (the step 5 smoke set).
- **B1 (additive):** apply a **deliberate additive** schema change (a new
  column / doc type), confirm the old image **tolerates** the superset and
  boots clean (proves the additive path).
- **B2 (breaking):** in a fresh pass, a **deliberate breaking** change (a
  dropped/reshaped column the old image reads), confirm the R1 guard
  **fires** and routes to the R3 DB-restore leg (proves the gate is not a
  no-op).
- **C — the full "app + DB" rollback (R3):** introduce a **known-bad write
  past the restore point**, then run the R3 DB-restore command (PITR point or
  last-`pg_dump`) **as `postgres`**, restart, and re-run the A verify set.
- **Verify leg (all steps):** `/health`, login, a representative page (the
  step 5 smoke set) — **the rendered page is the evidence** (step 5; the
  AGENTS.md "rendered HTML is the evidence" doctrine).
- **Stamp (the dated record):** record the **date + the target SHA + the
  passes (A/B1/B2/C) + the operator** in the Instance inventory "Last drilled
  (rollback)" column. This **dated record** is the concrete close of the
  FAIL.

> **Last drilled (rollback):** _open item — the owner stamps the date + SHA +
> passes when the drill is run on a throwaway prod-style instance;
> **never assumed**._ (Mirrors the [backups](backups.md) "Last tested (PITR)" idiom and "Rules
> of the road": *update the date when you run a step*.)

**Repeatable:** the sequence is a **fixed order** (A → B1 → B2 → C → verify
→ stamp); each pass is **independently re-runnable**, and the **"Last
drilled"** stamp is the **only state** the drill writes — making it a
**dated, repeatable, auditable** record rather than a one-off.

## Related

- [Backups](backups.md) — step 2's fresh backup, and the PITR machinery R3 reuses.
- [Restore](restore.md) — the same-instance and PITR restore paths R3 calls.
- [ADR 0004](../adr/0004-data-persistence-and-schema-evolution.md) — the forward-only schema rule this procedure is built on.
- [Design: F20 rollback](../design/prod-hardening-f20-rollback.md) — the design record behind the one-step rollback shape.
