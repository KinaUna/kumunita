# F20 — DEP-04: one-step rollback + drilled — Part-0 design pin

> **Fix:** F20 (report row **DEP-04**) — kind **`code+ops`**, **dep: F4** (BKP-02
> PITR / WAL archiving). F20's **DB-restore leg *is* a restore from F4's PITR** —
> F20 builds on F4's **design** (the `pgbackrest restore --type=time` primitive,
> F4 pin §6.8 / OPS §5), not on F4's code (which is empty — F4.U2 is "no code
> needed," F4 pin §7).
> **Status:** this is the **Part-0 design pin** (F20.U1). It is **design-only**:
> no code, no build, no `.cs` touched. F20.U2 lands the **`docs/OPS.md` runbook
> update** (the one-step rollback + the schema-compat guard + the drill procedure
> + the "last drilled" line) — that is the **code side**, and it is a *runbook*,
> not C# (see §7). F20.U3 is the **owner-ops drill** — recorded as an **open
> item**, *never assumed done*.
> **Why it's a FAIL today:** OPS §3 step 6 has an **app rollback** (reselect the
> previous commit SHA in Coolify) and OPS §5 has a **DB restore** — but they are
> **two separate procedures**, not a *single one-step runbook*; there is **no
> explicit schema-compat guard** (the forward-only rule is stated in §3 step 4 as
> a *constraint on the new image*, not as a *gate on the rollback direction*);
> and there is **no dated "last drilled" record** (OPS "Rules of the road" —
> *tested before it's relied on* — is honored for backups, but the rollback leg
> has never been drilled **as one step** and stamped). The audit's FAIL is the
> absence of that **one-step, guarded, drilled, dated** procedure — a *procedural*
> gap, not a *mechanistic* one (the mechanism already exists; see §7).

---

## 1. Current state (grounded, not assumed)

**What exists (from the repo's own runbook + design docs):**

- **App rollback (OPS §3 step 6):** "reselect the **previous commit SHA**
  recorded in step 2 in Coolify and deploy it." The prod path deploys from the
  **`release` branch only** (OPS §3 intro); **Coolify builds the image from the
  selected commit — there is no registry** (the git state *is* the image
  version). So the rollback is a **commit reselect**, not an image-tag swap.
- **DB restore (OPS §5):** a same-instance `pg_restore` — **connect as
  `postgres`** (superuser) because "the `kumunita` role cannot drop/recreate a
  database it is itself connected *as*"; **plus** the **PITR path**
  (`pgbackrest --stanza=<instance> restore --type=time --target=…` / `--latest`)
  — the continuous-WAL restore that **F4** (pin §6.8) provides once `archive_command`
  is live.
- **Forward-only schema (OPS §3 step 4, ADR 0004 §B):** "the new image must
  always carry a **superset** of the running schema's steps." Marten delta-
  detection is **idempotent, no `mt.migrations` ledger** (Weasel-feature pattern);
  EF Identity migrations run against `identity.__EFMigrationsHistory`.
- **The "last tested / last drilled" idiom (OPS "Rules of the road"; F4 pin §6.6):**
  "Update the 'Last tested' date when you run a step." F4's pin already carries a
  "Last tested (PITR)" line; this pin adds the rollback's **"Last drilled"** line.

**What's absent (the FAIL):**

- A **single one-step runbook** that names, in order: (1) the app rollback, (2)
  the schema-compat decision, (3) the DB restore (**only when the guard fires**),
  (4) the verify, (5) the dated stamp.
- An **explicit schema-compat guard** — today it is a *constraint on the forward
  direction* (the new image must be a superset), **not** a *gate on the rollback
  direction* (does the **old** image tolerate the **current** schema?).
- A **dated "last drilled" record** — the rollback has never been executed as one
  step and stamped with a date + SHA + passes.

---

## 2. The one-step app rollback mechanism (pinned)

The app-side half of the one-step rollback is the **commit reselect**. Two
equivalent forms — **primary (prod, Coolify)** and **equivalent (local / compose)**:

**Primary — prod, Coolify (the only prod path; OPS §3, no registry):**
1. **Name the rollback target SHA** — the "Version (Commit)" recorded in OPS §3
   step 2 (the pre-upgrade SHA, from `/health` → `build` / the `SOURCE_COMMIT`
   env) and/or the **Instance inventory** row (`Version (Commit)`, OPS §2). This
   is the SHA the app *was* running before the bad deploy.
2. **In Coolify, reselect that commit SHA** for the app and **deploy**. Coolify
   rebuilds the image from that commit (§3 intro: no registry — the commit *is*
   the version).
3. **On boot**, the **old** image's Marten delta-detection runs against the
   **current** `mt` catalog. Because the schema is forward-only (ADR 0004 §B) and
   the old image's feature set is a **subset** of what is applied, it tolerates
   the superset and is a **no-op** — **provided** the changes in the rolled-back
   window are additive-only (the §3 guard decides; a breaking change routes to
   the §4 DB-restore leg first).
4. **Verify** (§5 verify leg).

**Equivalent — local / `docker compose` (dev, builds from the working tree):**
```bash
git checkout <rollback-target-sha> && docker compose build app && docker compose up -d app
```
(the AGENTS.md rebuild idiom — `docker compose build app && docker compose up -d
app` — applied to the **previous commit**; wait ~8 s for the Marten schema +
Wolverine leadership boot). A **registry-based** deployment (not this repo's
prod shape) would instead be `docker compose pull <prev-tag> && docker compose
up -d app`; the **Coolify reselect-previous-SHA path above is the authoritative
prod idiom** (OPS §3) because prod has no registry.

**The "one-step" claim (pinned):** "one-step" means **one runbook the owner reads
top-to-bottom and executes** — it does **not** mean one command. The mechanism is
the §2 app reselect **plus, when the §3 guard fires**, the §4 DB-restore leg; the
*one-step* is the **documented, ordered, guarded, drilled** shape around those
existing primitives (the gap the audit found, §1).

---

## 3. The schema-compat guard (pinned)

**The rule:** forward-only (ADR 0004 §B) means the **old** image may be rolled
back over the **new** schema **iff** the schema changes in the rolled-back window
are **additive-only**.

- **Additive = safe on app-only rollback (§2):** a **new column with a default**,
  a **new table**, a **new doc type**, or an **additive-only EF migration** — the
  old image **ignores** the columns/tables/doc-types it doesn't know and reads its
  own subset. Rollback is **app-only**; the old image's boot delta-detection is a
  no-op against the superset.
- **Breaking = NOT safe on app-only rollback → the §4 DB-restore leg is
  mandatory:** a **dropped/renamed column**, a **column type change**, or a
  **reshaped doc the old image reads** means the old image **fails** against the
  current schema. The rollback then becomes **"app + DB"**, not "app only" —
  restore the Postgres surface to a point whose schema the old image understands
  (§4), **then** roll the app (§2).

**The pre-rollback checklist (the guard — the owner runs this, in order, *before*
reselecting the SHA):**
1. Enumerate the commits in the rolled-back window:
   `git --no-pager log --oneline <rollback-target-sha>..HEAD`.
2. For each, check the **Marten surface** (`Kumunita.Core` `FeatureSchemaBase` /
   doc-type registration — `CoreDocTypes` / `PostDocTypes`, `DependencyInjection.cs`)
   **and** the **EF surface** (`Kumunita.Web/Migrations/*`) for a schema change.
3. **Classify** each change **additive vs breaking** (additive = new column /
   table / doc type / additive-only migration; breaking = drop / rename / type-
   change / doc reshape).
4. **Decision gate:** *all* additive → app-only rollback (§2). *Any* breaking →
   **DB-restore leg first** (§4), **then** the app rollback (§2).

**Recorded risk (if the guard is skipped):** rolling an old image over a
**breaking** schema = a boot failure (the old image can't read the reshaped
`mt`/`identity` surface) — the app is **down**, not merely degraded. The guard
exists to convert that surprise outage into a **deliberate "app + DB" rollback**
with a known restore target.

---

## 4. The DB-restore leg (reuses F4's PITR — the exact owner command)

This leg is **F4's PITR, verbatim** (F4 pin §6.8 / OPS §5 PITR block) — F20 does
**not** re-invent it, it *calls* it (F4 pin §8: "F4 provides the PITR primitive
F20's 'one-step rollback' restore step will call").

**Exact owner sequence (the `postgres`-superuser restore, OPS §5 step 2):**
1. **Stop the app** (Coolify).
2. **Restore the Postgres surface** — one of two targets, chosen by F4's state:
   - **PITR point (preferred; F4's `archive_command` live, F4 pin §6.3):**
     `pgbackrest --stanza=<instance> restore --type=time --target="<T>"` where
     `<T>` is the instant **before** the bad deploy/write (e.g.
     `--target="2026-10-23 14:50:00+00"`), **or** `--latest` for the newest
     restorable point. **WAL-G fallback** (F4 pin §2): the `wal-g` restore-to-
     timestamp equivalent.
   - **Last `pg_dump` (fallback; F4's WAL archiving not yet live):** the OPS §5
     same-instance path — `pg_restore … < dump.dump` **as `postgres`** (the F4
     dependency only *narrows the RPO*; this leg works **with or without** F4).
   - **The restore connects as the `postgres` superuser** — the `kumunita` role
     cannot drop/recreate the DB it is connected *as* (OPS §5 step 2; the
     **ADR 0004 "app role owns the DB"** consequence, §6).
3. **Restore the media volume too, if bracketed** (F4 §5 boundary, re-stated):
   the DB restore does **not** rewind the media volume (`Media__RootPath`,
   ADR 0011). If the media snapshot brackets the dump (OPS §4 "Second restore
   surface"), restore it as **step 2.5** — between the DB restore and app start.
   A half-restore (Postgres at `T`, media volume at `T'`) degrades safely (a
   missing `MediaObject` byte is a **re-hydratable avatar `404`**; the content
   hash is the integrity key). This leg **reuses the existing** media-volume
   procedure; F20 does **not** change it (F4 §5).
4. **Restart Postgres, then the app** (now at the old SHA from §2). **Verify**
   (§5 verify leg).

**Prerequisite note (F4's open item, re-stated):** until F4's `archive_command`
is live (F4 pin §6.3, an **open item**), the restore target is the **last
`pg_dump`**, not a point-in-time. The pin records **both** targets so the §4 leg
is executable in **either** F4 state; F4 only narrows the RPO.

---

## 5. The drill procedure (the dated, repeatable, owner-run sequence)

**The drill** — the owner runs this on a **throwaway prod-style instance** (OPS
"No shared staging cluster — a throwaway prod-style instance is enough to verify
an upgrade"), *not* the live instance:

- **Precondition:** the throwaway instance is at a **known current** SHA with a
  representative schema; a **previous** SHA's image is available (Coolify deploy
  history / a `git checkout` target); and a **fresh backup / PITR anchor** is
  present (F4 pin §6.4 stanza + first full, **or** a `pg_dump`).
- **Step A — app-only rollback (§2 primary path):** reselect the previous SHA,
  deploy, boot. **Verify:** `/health` OK, `build` SHA == the previous SHA,
  **login** works, and **one representative page** renders (the OPS §5 step-3 /
  §3 step-5 smoke idiom — home / one post / one component, the M1/M3 smoke set).
- **Step B — the schema-compat guard (§3 checklist), two passes:**
  - **B1 (additive):** apply a **deliberate additive** schema change (a new
    column / doc type), confirm the old image **tolerates** the superset and
    boots clean (proves the additive path).
  - **B2 (breaking):** in a fresh pass, a **deliberate breaking** change (a
    dropped/reshaped column the old image reads), confirm the §3 guard **fires**
    and routes to the **§4 DB-restore leg** (proves the gate is not a no-op).
- **Step C — the full "app + DB" rollback (§4 leg):** introduce a **known-bad
  write past the restore point**, then run the §4 DB-restore command (PITR point
  or last-`pg_dump`) **as `postgres`**, restart, and re-run Step A's verify set.
- **Verify leg (all steps):** `/health`, login, a representative page (the §3
  step-5 smoke set) — **the rendered page is the evidence** (OPS §3 step 5; the
  AGENTS.md "rendered HTML is the evidence" doctrine).
- **Stamp (the dated record the audit found absent):** record the **date + the
  target SHA + the passes (A/B1/B2/C) + the operator** in the **Instance
  inventory** "Last drilled (rollback)" column. This **dated record** is the
  concrete close of the FAIL.

> **Last drilled (rollback):** _open item — the owner stamps the date + SHA +
> passes when the drill is run on a throwaway prod-style instance; **never
> assumed**._ (Mirrors the F4 pin §6.6 "Last tested (PITR)" idiom and OPS "Rules
> of the road": *update the date when you run a step*.)

**Repeatable:** the sequence is a **fixed order** (A → B1 → B2 → C → verify →
stamp); each pass is **independently re-runnable**, and the **"Last drilled"**
stamp is the **only state** the drill writes — making it a **dated, repeatable,
auditable** record rather than a one-off.

---

## 6. The ADR 0004 "app role owns the DB" tension (recorded, not resolved here)

- The **DB-restore leg requires the `postgres` superuser** (OPS §5 step 2)
  **precisely because** the app role owns the DB (ADR 0004) — the `kumunita` role
  cannot drop/recreate the DB it is connected *as*. This is the **non-blocking**
  reading for F20: the restore path is the **superuser** path, which does **not**
  depend on the app role's privileges — so the ADR 0004 ownership does **not**
  prevent the rollback.
- The **forward-only tolerance** in §3 also leans on the app role's schema
  ownership: the old image, on boot, runs its Marten delta-detection **as the
  `kumunita` role** against the (possibly-older) `mt` catalog — if the app role
  still owns `mt`, that is a no-op on an older catalog and a forward-apply on a
  newer one; **both are tolerated**.
- **F22's intersection (recorded, deferred to F22's ADR amendment):** F22 (DB-18)
  will **demote the app role** from DB-owner to DML-grants. When it lands, the
  **restore-as-`postgres` path in §4 remains canonical** (it already uses the
  superuser, *not* the app role — so F22 does **not** break F20's §4 restore
  leg), **but** the old-image **boot delta-detection** in §3/§6 must be
  **re-verified** against the demoted role's grants (the old image must still be
  able to **read** `mt` at boot). F20 **records the tension and defers the
  resolution to F22** — F20's §4 restore command is **unchanged** by F22.

---

## 7. Why this is `code+ops`, and what the code side is

- **The code side (F20.U2) is the `docs/OPS.md` runbook update** — **NOT a C#
  change.** There is **no app-side knob, no health-probe field, no DI seam** a
  one-step rollback needs: the app neither initiates nor participates in its own
  image rollback or its own DB restore — the **owner** does. This mirrors F4's
  §7 "no code needed" pattern, **with one difference:** F4's code side is
  **empty** (pure host/DBA infra), whereas F20's code side is the **runbook** —
  because the audit's FAIL is **procedural**, not mechanistic. The mechanism
  (Coolify reselect + PITR restore) **already exists** (OPS §3 step 6 + OPS §5 /
  F4 pin §6.8); what is **absent** is the **one-step, guarded, drilled, dated**
  shape around it. U2 is the artifact that closes that gap.
- **F20.U2 (≤ 1 file, the runbook):** add to `docs/OPS.md` (§3 **Upgrade** and/or
  §5 **Restore**) the §2 one-step rollback sequence, the §3 schema-compat guard +
  checklist, the §5 drill procedure, and the "Last drilled (rollback)" line — and
  record the owner-ops drill as an **open item**. **No C#, no
  `<PackageReference>`, no `.slnx` change, no build.**
- **F20.U3 (the owner-ops drill, §5):** run the A→B1→B2→C→verify→stamp sequence
  on a throwaway prod-style instance and **stamp "Last drilled (rollback)."**
  **Recorded, not assumed done** (the F4.U3 / F18.U3 / F12.U3 open-item pattern).

---

## 8. Invariants held + open items

**Invariants (this unit, and the lane):**
- **ADR 0006** module boundaries — **untouched** (no new bounded context / surface
  / schema; the rollback is owner-ops around existing primitives).
- **C3 single-write-lane** — **untouched** (the rollback is owner-ops, not an app
  write lane; no new write path).
- **Core stays HTTP-free** — **untouched** (no app-side rollback code; the §2/§4
  legs are host/owner commands, not C#).
- **ADR 0004 forward-only** — **enforced, not changed** (the §3 guard *applies*
  the forward-only invariant in the rollback direction; it does not relax it).
- **ADR 0011 media-volume boundary** — **preserved, not replaced** (the §4 leg
  reuses the existing media snapshot procedure; F20 does not change it).
- **F4 dependency honored** — the §4 leg **reuses** F4's PITR primitive (F4 pin
  §6.8), never re-invents it.

**Open items (owner — recorded, **NOT assumed done**):**
1. **Run the §5 drill on a throwaway prod-style instance** and **stamp "Last
   drilled (rollback)"** (date + SHA + passes) — the dated record the audit found
   absent. **(F20.U3.)**
2. **F4's PITR prerequisite** (`archive_command` live — F4 pin §6.3) — until it
   is, the §4 DB-restore leg falls back to the **last `pg_dump`** target (OPS §5
   same-instance path). F20's §4 leg works **either way**; F4 only narrows the
   RPO.
3. **F22's role demotion** (§6) — when F22 lands, **re-verify** the old-image
   boot delta-detection against the demoted app role's grants. F20's §4 restore
   command is **unaffected**.

---

## 9. Exit contract for this pin (F20.U1)

- This doc exists and pins the **5 required items**:
  1. **The one-step app rollback mechanism** — **Coolify reselect-previous-SHA**
     (primary, prod, no registry — OPS §3) + the
     `git checkout <prev-sha> && docker compose build app && docker compose up -d
     app` equivalent (local, the AGENTS.md rebuild idiom), **§2**.
  2. **The schema-compat guard** — additive-tolerant vs breaking→DB-restore, the
     **§3 pre-rollback checklist** (the gate the owner runs before reselecting
     the SHA).
  3. **The DB-restore leg** — F4's `pgbackrest restore --type=time` (or
     `--latest`) **as the `postgres` superuser**, the last-`pg_dump` fallback,
     the media-volume step-2.5 boundary, **§4**.
  4. **The drill procedure** — the **§5** dated/repeatable **A → B1 → B2 → C →
     verify → stamp** sequence + the **"Last drilled (rollback)"** line.
  5. **The verification shape** — the **§5 verify leg** (`/health`, login, a
     representative page — the rendered page is the evidence) + the **§7 "code
     side = the OPS runbook, no C#"** record (a **pure-runbook dry-run**, not a
     C# test — there is no C# to test).
- The **open items** (§8) are **recorded, not assumed done**.
- **No `src/` edit, no `<PackageReference>`, no migration, no `.slnx` change,
  no build run** for this unit.
- **Forward pointer:** **F20 is now at U2** — a fresh clean-slate agent reads
  this pin **§2 / §3 / §5** (the exact runbook content to land), **§7** (the U2
  shape — `docs/OPS.md`, ≤ 1 file, no C#), **OPS §3 / §5** (where U2 lands it),
  and the **F20.U1 handoff entry** below.
