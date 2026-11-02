---
id: deploy-coolify-40-postgres
title: "Coolify — 4: Postgres 18"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/COOLIFY.md
  - docs/adr/0171-least-privilege-db-role.md
  - docs/ops/backups.md
  - docs/ops/restore.md
updated: 2026-11-02
---

> **Abstract:** the dedicated Postgres 18 addon for one neighborhood — the two-role split (`kumunita_migrate` + `kumunita`), the one-shot init script, and the ownership/permission shape. **Who is it for:** the host operator. **When to read the rest:** before first boot, once per neighborhood.

# 4 — Postgres 18 (once per neighborhood)
One **dedicated** Postgres per neighborhood (never shared — ADR [0002](../../adr/0002-deployment-topology.md)).

For more information see Coolify docs: [PostgreSQL](https://coolify.io/docs/databases/postgresql).

1. Inside the neighborhood's environment (`production`, [§3](30-project-environment.md)): **Add a resource
   → Postgres**. Name it after the neighborhood (`maplewood-postgres`).
2. **Version: 18** — image parity with dev (`docker-compose.yml`) and the test
fixtures (`PostgresFixture` runs `postgres:18`).
3. **Expose no public port.** The app reaches it on the Coolify internal
network; operators reach it via the addon's psql. A public Postgres is a
hardening violation (the [hardening checklist](../../ops/hardening-checklist.md)).
4. **Bootstrap superuser `postgres`**: set a **strong unique** password on
first creation (`POSTGRES_PASSWORD`). `postgres` is **admin/restore-only**
(the [restore runbook](../../ops/restore.md)) and **never** the app connection.
5. **Create the migration role + app role + database ownership with one init script.** Paste the template below into the addon's **Initialization scripts** field (General → Initialization scripts), replacing only the two `<strong-…-password>` placeholders. The script is **self-contained**: it creates the `kumunita` database itself, then switches into it for the per-database statements — so the addon's **Initial database** field can stay at the default (`postgres`). (If you *do* set Initial database = `kumunita`, the script still works: its `CREATE DATABASE` is suppressed harmlessly and the rest runs inside `kumunita` either way.)
```sql
CREATE ROLE kumunita_migrate LOGIN PASSWORD '<strong-migrate-password>';
CREATE ROLE kumunita        LOGIN PASSWORD '<strong-app-password>';
-- Create the app database owned by the migration role (cluster-level). Suppressed
-- harmlessly if the entrypoint already created it (Initial database = kumunita):
\set ON_ERROR_STOP 0
CREATE DATABASE kumunita OWNER kumunita_migrate;
\set ON_ERROR_STOP 1
-- Database-level statements (valid from any connection, idempotent):
ALTER DATABASE kumunita OWNER TO kumunita_migrate;
GRANT CONNECT ON DATABASE kumunita TO kumunita;
-- Per-database statements must run *inside* the app database — switch into it:
\c kumunita
-- Pre-create the `mt` schema, owned by the migration role (the forced exception —
-- the app role gets CREATE on it so Wolverine + Marten can create their tables at boot):
CREATE SCHEMA mt AUTHORIZATION kumunita_migrate;
GRANT USAGE, CREATE ON SCHEMA mt TO kumunita;
```
This is the two-role split (**ADR [0171](../../adr/0171-least-privilege-db-role.md)** / F22) in its entirety: `kumunita_migrate` owns the DB + the `mt` schema and is the only role that runs DDL; the app role `kumunita` is DML, with one **forced exception** — `CREATE` on the `mt` schema only (so Wolverine + Marten can create their `mt.wolverine_*` / `mt.mt_*` tables **as the app role** during `app.StartAsync()`). The `identity` schema is deliberately **not** pre-created here — the app's fresh-boot signal ("identity schema absent") must stay true; EF creates it at first boot as `kumunita_migrate` (the DB owner). The shape mirrors `dev-db-init/01-app-role.sql` + `dev-db-init/02-app-schemas.sh` (the dev-compose equivalent).

(The init script runs **once, on first boot only** — the postgres image executes its `/docker-entrypoint-initdb.d/` files only against a fresh, empty data directory. Ownership-by-migrate-role means DDL at boot needs no elevated/rights dance, and the app role is refused `CREATE SCHEMA` / `DROP SCHEMA` / `DROP DATABASE` (SQLSTATE `42501`). See the [hardening checklist](../../ops/hardening-checklist.md) "DB user".)

6. Store **both** `<strong-migrate-password>` and `<strong-app-password>` in the secrets manager (the [provision runbook](../../ops/provision.md) + [first-boot runbook](../../ops/first-boot.md)).
7. Verify both paths: `kumunita_migrate` → `kumunita` (owns the DB; `mt` owned by it — and, after the app's first boot, `identity` too, which EF creates as this role), and `kumunita` → `kumunita` (can `CREATE TABLE` in `mt` + `INSERT`/`SELECT`, but is **refused** `CREATE SCHEMA` / `DROP SCHEMA` / `DROP DATABASE`).

8. Backing up the database is **procedure 4** ([backups runbook](../../ops/backups.md)) — the addon's backup task
writes to object storage, not only this VPS.
