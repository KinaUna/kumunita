-- Coolify PostgreSQL addon — one-shot init script (the operator-facing shape).
--
-- Paste this into the Postgres addon's **Initialization scripts** field
-- (General → Initialization scripts), replacing ONLY the two
-- `<strong-…-password>` placeholders.
--
-- The script is self-contained: it creates the `kumunita` database itself
-- (cluster-level), then `\c`s into it to run the per-database statements
-- (`CREATE SCHEMA mt`, grants, `ALTER DATABASE`). The addon's **Initial
-- database** field can stay at the default (`postgres`) — if it *is* set to
-- `kumunita`, the `CREATE DATABASE` below is suppressed harmlessly and the
-- rest of the script still runs inside the `kumunita` database.
--
-- This is the two-role split (**ADR 0171** / F22), in the exact shape the
-- runtime needs (mirrors `01-app-role.sql` + `02-app-schemas.sh`, the
-- dev-compose equivalent — see `docs/COOLIFY.md` §4 step 5):
--
--   kumunita_migrate  — the MIGRATION role. Owner of the `kumunita` database
--                       (transferred below from the superuser the image
--                       auto-created it as) and of the `mt` schema. The only
--                       role with DDL standing at boot (EF migrations, Marten
--                       schema-apply, the identity DML grants).
--   kumunita          — the RUNTIME APP role. DML only, with one **forced
--                       exception**: USAGE + CREATE on the `mt` schema, so
--                       Wolverine + Marten can create their `mt.wolverine_*` /
--                       `mt.mt_doc_*` tables **as the app role** during
--                       `app.StartAsync()`. It is NOT the DB owner, owns no
--                       schema, and is refused `CREATE SCHEMA` / `DROP SCHEMA`
--                       / `DROP DATABASE` (SQLSTATE 42501).
--
-- `identity` is deliberately NOT pre-created here: the app's fresh-boot signal
-- (`DbBootstrap.IsPristineAsync` = "identity schema absent") must stay true;
-- EF's `EnsureSchema("identity")` runs as `kumunita_migrate` (the DB owner) at
-- first boot.
--
-- Runs exactly once (the postgres image executes /docker-entrypoint-initdb.d/
-- only against a fresh, empty data directory). After first boot the script is
-- inert — do not rely on re-running it to change role passwords (use
-- `ALTER ROLE … PASSWORD` on the running instance, then update the app's
-- connection strings).
CREATE ROLE kumunita_migrate LOGIN PASSWORD '<strong-migrate-password>';
CREATE ROLE kumunita        LOGIN PASSWORD '<strong-app-password>';
-- Create the app database owned by the migration role. Cluster-level, so it
-- works from any connection. If the addon already created `kumunita` (because
-- "Initial database" = kumunita), this fails harmlessly — we suppress the
-- error and continue; the ownership transfer below then fixes the owner:
\set ON_ERROR_STOP 0
CREATE DATABASE kumunita OWNER kumunita_migrate;
\set ON_ERROR_STOP 1
-- Database-level statements (valid from any connection, idempotent):
ALTER DATABASE kumunita OWNER TO kumunita_migrate;
GRANT CONNECT ON DATABASE kumunita TO kumunita;
-- Per-database statements must run *inside* the app database — switch into it:
\c kumunita
-- Pre-create the `mt` schema, owned by the migration role (the app role is
-- granted CREATE on it below so it can create the wolverine_* / mt_doc_* tables
-- it owns at boot; it does NOT own the schema):
CREATE SCHEMA mt AUTHORIZATION kumunita_migrate;
GRANT USAGE, CREATE ON SCHEMA mt TO kumunita;
