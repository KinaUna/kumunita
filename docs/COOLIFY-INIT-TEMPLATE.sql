-- Coolify PostgreSQL addon — one-shot init script (the operator-facing shape).
--
-- Paste this into the Postgres addon's **Initialization scripts** field
-- (General → Initialization scripts), replacing ONLY the two
-- `<strong-…-password>` placeholders. Prerequisite: set the addon's
-- **Initial database** field to `kumunita` (not the default `postgres`) so
-- the postgres image auto-creates the `kumunita` database and runs this
-- script *inside it* — `CREATE SCHEMA` is per-database and is invalid
-- against the default `postgres` database.
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
-- Transfer ownership of the auto-created `kumunita` database to the migration role:
ALTER DATABASE kumunita OWNER TO kumunita_migrate;
GRANT CONNECT ON DATABASE kumunita TO kumunita;
-- Pre-create the `mt` schema, owned by the migration role (the app role is
-- granted CREATE on it below so it can create the wolverine_* / mt_doc_* tables
-- it owns at boot; it does NOT own the schema):
CREATE SCHEMA mt AUTHORIZATION kumunita_migrate;
GRANT USAGE, CREATE ON SCHEMA mt TO kumunita;
