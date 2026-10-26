-- F22 two-role split (least-privilege, DB-18 — ADR 0171, accepted 2026-10-25).
--
-- Mirrors the production topology (Coolify Postgres addon): the bootstrap superuser
-- `postgres` is reserved for admin work and restores (a restore connects to it while
-- targeting the scratch/app database — docs/OPS.md §5).
--
-- Replaces the old single-owner layout (`CREATE DATABASE kumunita OWNER kumunita`,
-- where the *runtime app* role was the database owner — the prod-hardening audit's
-- DB-18 FAIL). ADR 0171 splits the DDL-creation capability across two named roles:
--
--   kumunita_migrate  — the MIGRATION role. Owns the `kumunita` database (and the
--                       `mt` / `identity` schemas, created in 02-app-schemas.sh).
--                       This is the operator's role for restore / admin work and the
--                       only role with database-level DDL standing.
--   kumunita          — the RUNTIME APP role (demoted, docs/OPS.md §10). No longer
--                       the database owner. Holds CONNECT + its DML + (forced, see
--                       below) schema-level CREATE on `mt` so the Wolverine
--                       transactional-outbox message store it creates at
--                       StartAsync still works. It CANNOT create/drop schemas or
--                       the database, and owns no schema.
--
-- The forced exception (recorded in ADR 0171): the app role keeps schema-level
-- CREATE on `mt` only. Wolverine's `wolverine_*` tables + the Marten `mt_doc_*`
-- tables are created by the app-role Marten store during `app.StartAsync()` (the
-- transactional outbox requires the envelope store in the caller's own store/
-- transaction, so they cannot be re-routed to a separate role). Creating *tables*
-- in `mt` is schema-level DDL — it does NOT let the app role create/own/drop the
-- `mt`/`identity` SCHEMAS or the database, which is what DB-18 forbids.
--
-- dev-only, deliberately weak credentials (kumunita/kumunita,
-- kumunita_migrate/kumunita_migrate); the prod passwords live in the secrets
-- manager, not here.
--
-- Prod (Coolify) equivalent of this layout: `docs/COOLIFY-INIT-TEMPLATE.sql`
-- — the same shape as a one-shot init script for the Coolify Postgres addon
-- (see docs/COOLIFY.md §4 step 5).
--
-- Runs exactly once, against an empty data directory (docker-entrypoint-initdb.d).
-- After switching the db service to this layout you must `docker compose down -v`
-- so the container re-bootstraps. The per-schema grants + the pre-created schemas
-- land in 02-app-schemas.sh (a separate script: CREATE SCHEMA targets a specific
-- database, and this .sql script runs against the default `postgres` database).
CREATE ROLE kumunita_migrate LOGIN PASSWORD 'kumunita_migrate';

CREATE ROLE kumunita LOGIN PASSWORD 'kumunita';

CREATE DATABASE kumunita OWNER kumunita_migrate;

GRANT CONNECT ON DATABASE kumunita TO kumunita;
