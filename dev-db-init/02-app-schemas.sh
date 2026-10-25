#!/usr/bin/env sh
# F22 two-role split (ADR 0171) — step 2 + step 3 (schema pre-creation + grants).
#
# Prod (Coolify) equivalent of this whole 01+02 layout: `00-coolify-init-template.sql`
# (the same shape as a one-shot init script for the Coolify Postgres addon —
# docs/COOLIFY.md §4 step 5).
#
# Runs in /docker-entrypoint-initdb.d on a fresh data directory, AFTER 01-app-role.sql
# (which created the roles + the `kumunita` database owned by `kumunita_migrate`).
# This is a .sh (not .sql) because it must target the *app* database (`kumunita`),
# not the default init database (`postgres`): CREATE SCHEMA is per-database.
#
# Two roles (ADR 0171):
#   kumunita_migrate  — MIGRATION role: DB owner + owner of the `mt` / `identity`
#                       schemas. Runs the EF MigrateAsync (identity schema + AspNet*
#                       tables) and the operator's restore/admin work.
#   kumunita          — RUNTIME APP role: DML + (forced) schema-level CREATE on `mt`
#                       ONLY. It is NOT the DB owner, owns NO schema, and cannot
#                       CREATE SCHEMA / DROP SCHEMA / DROP DATABASE.
#
# Why `mt` is pre-created here (the forced exception, recorded in ADR 0171):
#   Wolverine's `wolverine_*` tables + Marten's `mt_doc_*` tables are created by the
#   app-role Marten store during `app.StartAsync()` (the transactional outbox needs
#   the envelope store in the caller's own store/transaction). Creating *tables* in
#   `mt` is schema-level DDL, so the app role needs CREATE on `mt`. But creating the
#   `mt` SCHEMA itself (or `identity`) is database-level DDL — the app role does NOT
#   have that (Design A / strict). So:
#     - `mt` is pre-created here, owned by kumunita_migrate; Weasel 9.45.1 checks
#       information_schema.schemata and only creates MISSING schemas, so the app-role
#       store never emits `CREATE SCHEMA mt` (it would need DB-level CREATE).
#     - `identity` is NOT pre-created: the EF boot signal (DbBootstrap.IsPristineAsync
#       = "identity schema absent") must stay a fresh-boot signal, and EF's
#       EnsureSchema("identity") runs as kumunita_migrate (the DB owner) at boot.
#     - the app role is granted SELECT/INSERT/UPDATE/DELETE on the identity tables
#       (created by kumunita_migrate) in SchemaBootstrap after MigrateAsync.
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname kumunita <<'EOSQL'
    -- Pre-create the `mt` schema, owned by the migration role (the app role is
    -- granted CREATE on it below so it can create the wolverine_* / mt_doc_* tables
    -- it owns at boot; it does NOT own the schema).
    CREATE SCHEMA mt AUTHORIZATION kumunita_migrate;

    -- App role: USAGE (to see mt objects) + CREATE (to create the wolverine_* and
    -- mt_doc_* tables it owns at StartAsync — the forced exception in ADR 0171).
    -- NOTE: no CREATE on `identity` — the app role never creates identity objects;
    -- their DML grants are issued in SchemaBootstrap after MigrateAsync.
    GRANT USAGE, CREATE ON SCHEMA mt TO kumunita;
EOSQL
