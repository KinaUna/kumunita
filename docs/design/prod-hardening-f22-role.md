# F22 — DB-18: demote the app role (least-privilege) — Part-0 design pin

> **Fix:** F22 (report row **DB-18**) — kind **`code+ops`**, **dep: —** (no code
> dependency, but **high risk**). **F22 is the lane's last fix** — its **U3** is
> the unit at which the **6-member close-flip** runs.
> **Status:** this is the **Part-0 design pin** (F22.U1). It is **design-only**:
> no code, no build, no `src/**` / `tests/**` / `dev-db-init/**` change, no
> `<PackageReference>`, no migration, no `.slnx` change, no test run. The pin
> (this file) + the **proposed ADR 0171** (the ADR amendment that resolves the
> ADR 0004 "app role owns the DB" tension) are the **two** deliverables.
> **F22.U2** lands the split (the `dev-db-init/01-app-role.sql` two-role DDL + the
> **boot-ownership path** in `Program.cs`) — **high risk, and it is gated on
> owner acceptance of ADR 0171 first** (U1 records the tension; the owner must
> accept the reconciliation before the risky migration proceeds). **F22.U3** is
> the **owner-ops step** (apply the split on the prod Postgres) — recorded as an
> **open item, never assumed done**, and is the lane's **close-flip** unit.
> **Why it's a FAIL today:** the app role is the **database owner** —
> `dev-db-init/01-app-role.sql` runs `CREATE DATABASE kumunita OWNER kumunita`.
> A Postgres database owner can `ALTER DATABASE` / `DROP` its own database,
> create and drop **any** schema in it, and create roles in many setups. That is
> not the standing a *runtime* application role should hold: the audit's FAIL is
> that the role the app uses **every** request is the most-privileged role in the
> database (least-privilege violated). F21 only changed *how* the app connects
> (TLS) — **F22 is the unit that touches *ownership*.**

---

## 1. The exact ADR 0004 tension (grounded, not assumed)

The two constraints collide on **one role** — and the collision is the whole fix:

**ADR 0004 needs a role that *can* create schemas at boot.** ADR 0004 §A splits
one Postgres into two schemas with a hard ownership boundary (`mt` — Marten;
`identity` — EF Core Identity). ADR 0004 §B pins the applied-state contract:
*"Applied at boot in all environments (incl. production): the versioned steps
only — a pristine database gets its initial state with no operator step"* and
*"Re-running an already-applied step is a no-op."* The mechanism that makes a
**pristine** boot work is that the connecting role can `CREATE SCHEMA` /
`CREATE TABLE` in `mt` and `identity`. `dev-db-init/01-app-role.sql` states the
reason verbatim:

> `kumunita` is the dedicated non-superuser role the app runs as (docs/OPS.md
> §10) and **owns the app database so it can create the `mt` and `identity`
> schemas at boot (ADR 0004).**

**The DB-18 least-privilege FAIL forbids that same role from being the owner.**
The role in `ConnectionStrings:Kumunita` (the one both Marten **DML** at
`Program.cs:95` and EF Core **DML** at `Program.cs:474` connect as) is the
database **owner** — the broadest standing short of a superuser.

**The tension, precisely:** ADR 0004's boot-ownership invariant *requires*
**some** role to carry the DDL-creation capability (`CREATE SCHEMA` /
`CREATE TABLE` in `mt` and `identity`) at boot; DB-18's least-privilege
requirement *forbids* the **runtime app** role from being the database owner.
The reconciliation is **not** to drop the boot-ownership invariant (that would
break ADR 0004 §B's "pristine database gets its initial state with no operator
step") — it is to **split the capability across two roles**: a dedicated
**migration** role keeps the DDL-creation capability (and the boot contract),
and the **runtime app** role is **demoted to DML**. ADR 0004's *forward-only +
delta-detected idempotent boot* is **preserved**; only **which role** carries
the DDL capability changes. This is a **proposal** (ADR 0171), not an accepted
change — U2 must not proceed until the owner accepts it.

---

## 2. The two-role split (pin item 1) — the role / privilege matrix

Two **named** roles, replacing the single owner-role today. The **name** of the
migration role is pinned as `kumunita_migrate` (U2 may keep it or choose an
equivalent — but the *shape* below is the contract):

| Role | Standing (pinned) | What it may do | What it may **not** do |
|---|---|---|---|
| **`kumunita_migrate`** (the **migration** role — *new*) | **Database owner** + **owner of the `mt` and `identity` schemas** | `CREATE SCHEMA` / `ALTER` / `DROP` in those schemas; `CREATE` / `ALTER` / `DROP` on the tables; run the boot schema-apply (`ApplyAllConfiguredChangesToDatabaseAsync` + `Database.MigrateAsync`); operator restore / admin work | — (this is the one role allowed DDL) |
| **`kumunita`** (the **runtime app** role — *demoted*, already exists) | `CONNECT` on the DB + `USAGE` on schemas `mt`, `identity` + `SELECT` / `INSERT` / `UPDATE` / `DELETE` on the tables in those schemas | all **DML** the app does today (Marten reads/writes, EF Identity reads/writes) | **`CREATE SCHEMA`, `CREATE` on schema, `DROP` (table/schema/DB), `TRUNCATE`, `CREATE DATABASE`, `ALTER` on schema/table** — no DDL at all; **not** the database owner |

**The invariants the split holds (and why it is safe):**

- **The runtime app role keeps every DML right it uses today** — `SELECT` /
  `INSERT` / `UPDATE` / `DELETE` on every `mt` and `identity` table. So a
  running deployment behaves **identically** after the split; nothing the app
  *does* changes, only the rights it *doesn't have* (the DDL rights it never
  needs at runtime).
- **The DDL-creation capability is not removed — it is moved** to
  `kumunita_migrate`, which is the role that actually performs the boot
  schema-apply. ADR 0004 §B's "pristine database gets its initial state with no
  operator step" is therefore **preserved** (§3).
- **The split demotes *privileges*, not the forward-only path.** The
  schema-evolution mechanism (Weasel `FeatureSchemaBase`, delta-detected,
  idempotent) is **untouched**; the forward-only guarantee is **untouched**.
  What changes is *who* holds the right to run it.

---

## 3. The boot-ownership migration (pin item 2) — how a pristine boot still creates `mt` / `identity`

This is the piece that makes the split **reconcile** with ADR 0004 §B rather
than break it. A pristine boot proceeds in **four ordered steps**; the key is
that the **DDL** step and the **DML** step run as **different roles**:

1. **Provisioning** (owner / Coolify / `01-app-role.sql`, runs once against an
   empty data directory — `docker-entrypoint-initdb.d`): create **both** roles
   and the **database owned by the migration role**, and grant the app role
   `CONNECT`:
   `CREATE ROLE kumunita_migrate LOGIN …; CREATE ROLE kumunita LOGIN …;`
   `CREATE DATABASE kumunita OWNER kumunita_migrate;`
   `GRANT CONNECT ON DATABASE kumunita TO kumunita;`
   (The per-schema and per-table grants **cannot** run here — the schemas do
   not exist yet on a pristine data directory. They land in step 3.)
2. **Boot schema-apply** (the app process, connecting as **`kumunita_migrate`**):
   `SchemaBootstrap.ApplyAsync` → Marten `ApplyAllConfiguredChangesToDatabaseAsync()`
   (creates the `mt` schema + tables, **owned by `kumunita_migrate`**) + EF
   `Database.MigrateAsync()` (creates the `identity` schema + `AspNet*` tables,
   **owned by `kumunita_migrate`**). This is **exactly** the ADR 0004 §B boot
   path, unchanged in shape — the only difference is *which role* it connects
   as. Delta-detected ⇒ a re-run is a no-op.
3. **Grant step** (the app process, still connecting as **`kumunita_migrate`**,
   immediately after step 2, and idempotent on every subsequent boot): issue the
   app role's standing now that the schemas + tables exist — the per-schema
   `USAGE`, the per-table `SELECT` / `INSERT` / `UPDATE` / `DELETE`, and the
   `ALTER DEFAULT PRIVILEGES` that auto-grants DML to the app role on **every
   future table** the migration role creates (the §4 DDL). This is the piece
   that keeps "a brand-new table is usable by the app" **without** giving the
   app role any `CREATE`.
4. **Runtime DML** (the app process, connecting as **`kumunita`**): all reads
   and writes, `Program.cs:95` (Marten) and `Program.cs:474` (EF) unchanged.
   The role is DML-only — it has no `CREATE` / `DROP`.

**Net effect on the ADR 0004 invariants:** the "pristine database gets its
initial state with no operator step" contract is **preserved** (steps 2–3 run
at boot in all environments, incl. production); the "re-run is a no-op" contract
is **preserved** (step 2 is delta-detected, step 3 is idempotent grants); the
*only* change is that the **DDL-creation capability** lives in a dedicated
migration role instead of the runtime app role.

**Why this is a `Program.cs` + `01-app-role.sql` change (U2), not a config
flip:** step 2 (the schema-apply) and step 3 (the grants) need a **second
connection** (the migration role) distinct from the runtime DML connection. U2
introduces that as a small **env-knob pair** (the F21 `Program.cs` knob shape,
re-applied: a `DbMigrate` user/password, absent ⇒ fall back to the single-role
baseline so the dev `docker-compose` and any not-yet-migrated prod instance keep
working), so **the same image works before and after the split**. That
backward-compat fallback is deliberate: it means a deployment that has **not**
yet run the prod ops step (U3) is unchanged, and the split activates only when
the owner provisions the second role.

---

## 4. The grant DDL shape (the concrete pin for U2)

The exact DDL the split settles on. **Step 1** (in `01-app-role.sql`, the
`dev-db-init` bootstrap that runs once against an empty data directory):

```sql
-- F22 two-role split (least-privilege — ADR 0171).
-- The migration role owns the DB (so it can CREATE SCHEMA / CREATE TABLE at
-- boot); the app role is demoted to DML (CONNECT + USAGE + SELECT/INSERT/
-- UPDATE/DELETE). Replaces the old `CREATE DATABASE kumunita OWNER kumunita`.
CREATE ROLE kumunita_migrate LOGIN PASSWORD '<migrate-secret>';
CREATE ROLE kumunita        LOGIN PASSWORD '<app-secret>';
CREATE DATABASE kumunita OWNER kumunita_migrate;
GRANT CONNECT ON DATABASE kumunita TO kumunita;
```

**Step 3** (the grant path, run at boot as `kumunita_migrate` **after** the
schemas + tables exist; idempotent on every re-run):

```sql
GRANT USAGE ON SCHEMA mt TO kumunita;
GRANT USAGE ON SCHEMA identity TO kumunita;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA mt TO kumunita;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA identity TO kumunita;
-- Auto-grant DML to the app role on every FUTURE table the migration role
-- creates (this is what keeps new tables usable without giving the app role
-- any CREATE). Issued by kumunita_migrate, which owns the schemas.
ALTER DEFAULT PRIVILEGES FOR ROLE kumunita_migrate IN SCHEMA mt
  GRANT SELECT, INSERT, UPDATE, DELETE TO kumunita;
ALTER DEFAULT PRIVILEGES FOR ROLE kumunita_migrate IN SCHEMA identity
  GRANT SELECT, INSERT, UPDATE, DELETE TO kumunita;
```

**What the app role does *not* get (the FAIL the split closes):** it is **not**
the database owner, and it holds **no** `CREATE` / `DROP` / `TRUNCATE` /
`CREATE SCHEMA` / `ALTER` on `mt` or `identity`. A compromised or buggy app
cannot drop a schema, add a table, or run DDL — it can only do the DML the
platform was designed to do.

**The `ALTER DEFAULT PRIVILEGES` line is the load-bearing piece.** Without it,
the first boot after adding a new Marten document type would create a table the
app role could not touch. With it, the migration role creates the table and the
app role gets DML automatically, on every future table, forever — the app role
never needs `CREATE`.

---

## 5. The pinned test name (pin item 3) — `AppRole_CannotDropSchema`

**Exact pinned test name (do not rename): `AppRole_CannotDropSchema`.** A
Testcontainers integration test (the `PostgresFixture` seam-test shape in
`tests/Kumunita.Core.Tests/`, the F1 precedent) that stands up Postgres with the
**two-role layout** and asserts the split held at the Postgres privilege
boundary:

- **Connect as the app role (`kumunita`)** — the demoted, DML-only role.
- **Assert the app role *cannot* run schema DDL:** `DROP SCHEMA` (and,
  symmetrically, `CREATE SCHEMA`) is refused by Postgres with SQLSTATE **`42501`**
  (`insufficient_privilege`) — the named assertion, and the stronger statement
  that the app role holds **no** schema-level DDL right.
- **Assert the app role *can* still do DML:** `SELECT` / `INSERT` / `UPDATE` /
  `DELETE` on an existing `mt` table **succeed** — the split demoted DDL, not
  the app's working rights (this is the "the split didn't break the app" half).
- **(Witness) the capability was moved, not removed:** the migration role
  (`kumunita_migrate`) *can* create a schema — the boot-ownership capability
  still exists, on the role that needs it.

The test is the **enforcement** witness: it proves the least-privilege property
is real at the Postgres layer, not just asserted in a doc.

---

## 6. Invariants held (this pin + the unit it drives)

- **ADR 0004 forward-only + delta-detected idempotent boot — PRESERVED, not
  dropped.** The split demotes *privileges* (which role holds `CREATE`), it does
  **not** change the schema-evolution mechanism, the forward-only path, or the
  "pristine database gets its initial state with no operator step" contract
  (§3). The "app role owns the DB so it can create the `mt` / `identity`
  schemas at boot" invariant is **reconciled by proposal** (a dedicated
  migration role now carries the capability), **not silently dropped** — the
  reconciliation is **ADR 0171**, which the owner must accept before U2
  proceeds.
- **ADR 0006 module boundaries + Core stays HTTP-free — HELD.** F22 adds **no**
  bounded context, **no** Core service seam, **no** cross-context call. The
  change is at the **Postgres role / privilege** layer (the `dev-db-init` DDL +
  the boot connection shape in `Program.cs`), below the bounded contexts. Core
  is not given any new type or seam.
- **No new bounded context / no new Core seam / no `<PackageReference>` / no EF
  migration.** The app role loses DDL rights; it gains none it needs. The only
  new surface is a second **Postgres role** and the **env-knob pair** for its
  credentials.
- **C3 single-write-lane / server-rendered stack — UNTOUCHED.** No write lane,
  no route, no view, no `AccessAudit` row shape changes.
- **The single `ConnectionStrings:Kumunita` runtime-DML consumer — UNCHANGED in
  shape.** The app role is still the DML role at `Program.cs:95` (Marten) and
  `Program.cs:474` (EF); only the **boot schema-apply/grant path** gains the
  migration-role connection (the F21-knob-shaped `Program.cs` addition, U2).
- **Backward compatibility — HELD.** The `Program.cs` env-knob falls back to the
  single-role baseline when the migration-role credentials are unset, so a
  deployment that has not yet run the prod ops step (U3) is byte-for-byte
  unchanged. The split activates only when the owner provisions `kumunita_migrate`.

---

## 7. Open items (owner — recorded, NOT assumed done)

F22 is `code+ops`; these are the owner-executed steps. **None is assumed done**;
the code agent does **not** run them. They are the substance of **F22.U3** (the
lane's close-flip unit):

1. **Provision the `kumunita_migrate` role on the prod Postgres** and make it
   the owner of the `kumunita` database (and of the `mt` / `identity` schemas —
   on an **existing** database this is an `ALTER DATABASE kumunita OWNER
   kumunita_migrate` + `ALTER SCHEMA mt / identity OWNER TO kumunita_migrate`,
   the existing-DB analogue of step 1). **This must precede** the app role being
   demoted, or the running app loses its ability to apply any further schema
   change.
2. **Demote the prod app role to DML** — apply the §4 step-3 grants to the app
   role, then **revoke the owner / DDL standing** (it must end as non-owner,
   no `CREATE` / `DROP`). Verify with the §5 assertion (`42501` on
   `DROP SCHEMA`, DML still green) **before** revoking anything the running app
   depends on.
3. **Set the `DbMigrate` env** (the §3 / U2 knob pair) on the prod app so the
   boot schema-apply connects as the migration role. If the owner cannot
   provision a second role on the managed addon, **record the justification** —
   **do not** leave the app role as the database owner while claiming least
   privilege.

---

## 8. Exit contract + forward pointer

**F22.U1 is design-only:** no code, no build, no `src/**` / `tests/**` /
`dev-db-init/**` change, no `<PackageReference>`, no migration, no `.slnx`
change, no test run. The pin (this file) + **ADR 0171** (the proposed ADR
amendment) exist and pin the **three** items: (1) the **two-role split** —
`kumunita_migrate` (DB owner + schema owner, the DDL role) vs. `kumunita`
(demoted to `CONNECT` + `USAGE` + `SELECT` / `INSERT` / `UPDATE` / `DELETE`, the
DML role) (§2); (2) the **boot-ownership migration** — the 4-step pristine-boot
ordering where the DDL step and the DML step run as **different roles**,
preserving ADR 0004 §B's no-operator-step boot (§3 + §4 the exact DDL); (3) the
**pinned test name** `AppRole_CannotDropSchema` (§5). The **ADR 0004 tension**
is recorded, and the reconciliation is a **proposal** (ADR 0171), **not an
accepted change** (§1 + §6). The **owner-ops open items** are recorded, **not
assumed done** (§7). The invariants are held (§6).

**F22 is now at U2.** The prod-hardening lane stays **open** — **F22.U2 + F22.U3
remain**. **No 6-member close-flip yet** (that happens at **F22.U3**, the lane's
last unit). Next unit = **F22.U2** — **the high-risk migration + role split**:
implement the §4 two-role DDL in `dev-db-init/01-app-role.sql` + the §3
boot-ownership path (the `Program.cs` `DbMigrate` env-knob pair, F21-knob
shape), **gated on owner acceptance of ADR 0171 first**. A fresh clean-slate
agent reads this entry + **§2 / §3 / §4** (the role matrix, the boot ordering,
the exact DDL) + **§5** (the test shape) + **§6** (the invariants) + **§7** (the
owner ops) + **ADR 0171** (the acceptance gate) + `dev-db-init/01-app-role.sql`
(the current single-owner DDL to replace) + `Program.cs` **~92–104** (the F21
`DbTls` knob, the shape the new `DbMigrate` knob follows).
