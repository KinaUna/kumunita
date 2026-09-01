-- Mirrors the production topology (Coolify Postgres addon): the bootstrap superuser
-- `postgres` is reserved for admin work and restores (a restore connects to it while
-- targeting the scratch/app database — docs/OPS.md §5). `kumunita` is the dedicated
-- non-superuser role the app runs as (docs/OPS.md §10) and owns the app database so
-- it can create the `mt` and `identity` schemas at boot (ADR 0004).
--
-- dev-only, deliberately weak credentials (kumunita/kumunita); the prod password
-- lives in the secrets manager, not here.
--
-- Runs exactly once, against an empty data directory (docker-entrypoint-initdb.d).
-- After switching the db service to this layout you must `docker compose down -v`
-- so the container re-bootstraps.
CREATE ROLE kumunita LOGIN PASSWORD 'kumunita';

CREATE DATABASE kumunita OWNER kumunita;
