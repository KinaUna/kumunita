# ADR 0171 — Least-privilege DB role: the two-role split (migrations under a separate owner role; app role demoted to DML)

Status: **Accepted** (owner accepted 2026-10-25 — the **F22.U2** gate is now
**clear**; the high-risk split may proceed)
Date: 2026-10-25
Accepted: 2026-10-25
Amends **ADR 0004** (§A — "Two schemas, one Postgres" + §B — the
boot-ownership invariant "the app role owns the DB so it can create the `mt`
and `identity` schemas at boot"). This ADR reconciles that invariant with the
**DB-18 least-privilege** requirement (the prod-hardening audit's FAIL) by
**splitting the DDL-creation capability across two roles** rather than by
dropping the invariant.

> **History (was Proposed, now Accepted):** F22 is `code+ops` and its **U2
> (the migration + role split) is high risk** — it changes *which role* owns the
> database and the schemas on a running system, and a wrong ordering revokes the
> app's ability to apply further schema changes. Per the prod-hardening protocol,
> a high-risk unit is **gated on owner acceptance of the proposal first**. U1
> (this ADR + the design pin) recorded the tension and the proposal; the owner
> accepted it on 2026-10-25; only then does U2 land the code. This ADR's
> **index row is added as *Accepted***.

## Context

ADR 0004 §A splits one Postgres into two schemas with a hard ownership boundary
(`mt` — Marten domain documents; `identity` — EF Core Identity tables). ADR 0004
§B pins the applied-state contract: *"Applied at boot in all environments (incl.
production): the versioned steps only — a pristine database gets its initial
state with no operator step"* and *"Re-running an already-applied step is a
no-op."* The mechanism that makes a **pristine** boot work is that the role the
app connects as can `CREATE SCHEMA` / `CREATE TABLE` in both schemas.

The current deployment encodes that invariant as **database ownership by the app
role.** `dev-db-init/01-app-role.sql` runs `CREATE DATABASE kumunita OWNER
kumunita`, and comments the reason verbatim:

> `kumunita` is the dedicated non-superuser role the app runs as (docs/OPS.md
> §10) and **owns the app database so it can create the `mt` and `identity`
> schemas at boot (ADR 0004).**

The **DB-18 least-privilege** FAIL (the prod-hardening audit) objects to exactly
this: the role the app uses **every** request is the **database owner** — the
broadest standing short of a superuser, able to `DROP` its own database and
create/drop any schema in it. A *runtime* application role should not hold that.

The tension is therefore precise: **ADR 0004's boot contract requires *some*
role to hold the DDL-creation capability; DB-18 forbids the *runtime app* role
from being the database owner.** These are not contradictory — they can both be
true if the capability is **moved to a different role.** That is this ADR.

## Decision

**Split the capability across two named roles. The runtime app role is demoted
to DML; a dedicated migration role carries the DDL-creation capability and the
boot-ownership contract.**

1. **Two roles.**
   - **`kumunita_migrate`** — the **migration** role (new). Owns the `kumunita`
     database **and** the `mt` and `identity` schemas. This is the **only** role
     that may run DDL (`CREATE` / `ALTER` / `DROP` on schemas/tables) in the
     database. It is the role the **boot schema-apply** (ADR 0004 §B:
     `ApplyAllConfiguredChangesToDatabaseAsync` + `Database.MigrateAsync`)
     connects as, and it is the operator's role for restore / admin work.
   - **`kumunita`** — the **runtime app** role (existing, **demoted**). Holds
     `CONNECT` on the database, `USAGE` on `mt` and `identity`, and
     `SELECT` / `INSERT` / `UPDATE` / `DELETE` on the tables in those schemas.
     It is **not** the database owner and holds **no** `CREATE` / `DROP` /
     `TRUNCATE` / `CREATE SCHEMA` / `ALTER` DDL right.

2. **The DML surface is unchanged.** The app role keeps **every** DML right the
   platform uses today (Marten reads/writes at `Program.cs:95`, EF Identity
   reads/writes at `Program.cs:474`). A running deployment behaves
   **identically** after the split; only the DDL rights it never needed at
   runtime are removed.

3. **New tables stay usable without giving the app role `CREATE`.** The
   migration role's `ALTER DEFAULT PRIVILEGES … GRANT SELECT, INSERT, UPDATE,
   DELETE TO kumunita` (per schema) auto-grants DML to the app role on **every
   future table** the migration role creates. This is the load-bearing piece that
   keeps "a brand-new Marten/EF table is usable by the app" true on every
   subsequent boot **without** the app role ever needing `CREATE`.

4. **The boot-ownership migration.** A pristine boot proceeds as:
   (1) provisioning creates both roles and the database **owned by the migration
   role** (`01-app-role.sql`, once, on an empty data directory);
   (2) the boot schema-apply connects as **`kumunita_migrate`** and creates the
   `mt` + `identity` schemas and tables (ADR 0004 §B, unchanged in shape);
   (3) the grant path connects as **`kumunita_migrate`** and issues the
   step-3 grants + `ALTER DEFAULT PRIVILEGES` (idempotent on every re-run);
   (4) the runtime DML connects as **`kumunita`**. ADR 0004 §B's "no operator
   step" and "re-run is a no-op" contracts are **preserved** — only *which role*
   runs steps 2–3 changes.

5. **Backward compatibility.** The `Program.cs` change is an **env-knob pair**
   (the F21 `DbTls` knob shape) carrying the migration role's credentials. When
   the knob is **unset**, the boot path falls back to the **single-role
   baseline** (the current behavior), so a deployment that has not yet run the
   prod ops step is byte-for-byte unchanged. The split activates only when the
   owner provisions `kumunita_migrate`.

## Consequences

- **Least-privilege is restored at the Postgres layer.** A compromised or buggy
  app can no longer drop a schema, create a table, or run DDL — it can only do
  the DML the platform was designed to do. DB-18 is closed **when the owner
  applies the split on the prod Postgres** (the `code+ops` ops half, recorded as
  an open item, not assumed).
- **ADR 0004 is *amended*, not overturned.** §A (two schemas) and the forward-only
  / delta-detected idempotent schema-evolution path are **unchanged**. §B's
  boot-ownership invariant is **reconciled**: the capability it relied on now
  lives in the dedicated migration role. Any future schema change is still
  applied at boot with no operator step.
- **The enforcement witness is a test.** `AppRole_CannotDropSchema`
  (Testcontainers, the `PostgresFixture` shape) asserts the app role is refused
  `DROP SCHEMA` / `CREATE SCHEMA` (SQLSTATE `42501`) while its DML
  (`SELECT` / `INSERT` / `UPDATE` / `DELETE`) still succeeds — the split demoted
  DDL, not the app's working rights.
- **The risk is in the ordering, and it is owner-executed.** On an **existing**
  production database the split is an `ALTER DATABASE kumunita OWNER
  kumunita_migrate` + `ALTER SCHEMA mt / identity OWNER TO kumunita_migrate`
  **before** the app role is demoted — the running app must never lose its
  ability to apply a further schema change. The prod ops steps are the
  **open items** (the design pin §7), **recorded, not assumed done**.
- **No new bounded context, no Core service seam, no `<PackageReference>`, no EF
  migration.** The change is at the Postgres role/privilege layer + the boot
  connection shape. Core stays HTTP-free; ADR 0006 module boundaries are
  untouched.

## Implementation (F22.U2, 2026-10-25)

Decisions the empirical boot-DDL resolution settled, recorded here so the ADR
matches what actually ships (the pin `docs/design/prod-hardening-f22-role.md`
is the F22.U1 source; this addendum is the F22.U2 implementation record):

- **The one forced exception.** The app role is **not the owner** of `mt`
  (`kumunita_migrate` owns it) and owns no schema and not the database — but it
  keeps **schema-level `CREATE` on `mt`**, because Wolverine's
  `IntegrateWithWolverine()` and Marten create their runtime tables
  (`mt.wolverine_*`, `mt.mt_*`) **as the app role** during `app.StartAsync()`.
  Schema-level CREATE is table-DDL *within* a pre-created schema, not schema/DB
  DDL: the app is still refused `CREATE SCHEMA` / `DROP SCHEMA` / `DROP DATABASE`
  (all SQLSTATE `42501`). This is "owns no schema" held precisely — no
  *ownership*, a bounded *table-creation* grant on `mt` only.
- **`mt` is pre-created; `identity` is not.** `mt` is created in init owned by
  `kumunita_migrate` with `GRANT USAGE, CREATE … TO kumunita`; `identity` is
  **deliberately absent** after init — (a) its absence is the pristine signal in
  `DbBootstrap.IsPristineAsync` (ADR 0004 §B), and (b) the EF initial migration
  `EnsureSchema("identity")` emits `CREATE SCHEMA IF NOT EXISTS identity`, which
  needs **database-level** CREATE — so it must run as `kumunita_migrate`. The
  app then gets `identity` USAGE + table DML **at boot** (below), not in init.
- **EF migrations run as the migrate role.** `SchemaBootstrap.ApplyAsync`
  applies the Marten/Weasel store as the app role (the app already has `mt`),
  then runs `identity.Database.MigrateAsync` **as `kumunita_migrate`** and
  grants the app identity DML. The migrate connection is read from
  **`ConnectionStrings:KumunitaMigrate`** (the actual env key — the pin's
  "DbMigrate" placeholder became this). When the key is **unset**, the path
  degrades to the single-role baseline (`identity` on the app role) — the
  backward-compat knob.
- **`02-app-schemas.sh`.** The `mt` pre-create + grant lives in a **`.sh`**
  init script (not `.sql`) because Postgres' entrypoint runs `.sql` files
  against the `postgres` database, not `kumunita` — the script `psql`s the app
  DB explicitly.
- **No live-DB migration path.** Per the owner (2026-10-25), current production
  deployments are install-from-scratch demos, so the split is simply what
  provisioning (`01-app-role.sql` + `02-app-schemas.sh`) produces — there is no
  `ALTER … OWNER` demotion of a running DB to author. The reorder-ops risk above
  applies only to a hypothetical existing-DB demotion and remains recorded, not
  executed.
- **Verified at a pristine boot** (`docker compose down -v && up`): `mt` /
  `identity` owner = `kumunita_migrate`, DB owner = `kumunita_migrate`; the app
  role's `CREATE TABLE mt.x` / `INSERT` succeed while `DROP SCHEMA mt` ("must be
  owner of schema") and `CREATE SCHEMA` ("permission denied for database") are
  refused; `identity."AspNetUsers"` is DML-able by the app role and the
  GlobalAdmin is seeded. The witness test `AppRole_CannotDropSchema` encodes the
  same invariants against a Testcontainers DB.
