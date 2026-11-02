---
id: dev-live-server-debugging
title: "Live-server debugging conventions"
tier: living
kind: guide
status: current
audiences: [developer, agent, platform-maintainer]
related:
  - AGENTS.md
  - docs/dev/README.md
  - docs/plans-milestones/improve-docs/plan-improve-docs.md
lane: improve-docs
updated: 2026-11-02
---

> **Abstract:** the conventions that trip you up when debugging against the
> live docker-compose Postgres — the `mt_doc_` table-naming convention, the
> `kumunita` vs `postgres` database gotcha, `pgcrypto`/`digest`, and why a
> 404 on `/admin/setup` submit is success, not failure. **Who is it for:** a
> developer, agent, or maintainer psql-ing the live server. **When to read
> the rest:** the moment a Postgres query against the app DB returns a
> "relation does not exist" or a function error, or the setup flow
> "fails" with a 404.

# Live-server debugging conventions

When debugging against the live docker-compose Postgres, three specific
errors recur because the conventions are non-obvious:

- **`ERROR: relation "mt.IdentityToken" does not exist`** (or similar) —
  the table name is wrong. The Marten convention in this repo is
  **`mt_doc_` + lowercase class name** (e.g. `mt_doc_identitytoken`,
  `mt_doc_bookmark`, `mt_doc_personalnote`). The class name is
  lowercased and the `mt_doc_` prefix is required. Querying
  `mt.IdentityToken` (Pascal case, no `mt_doc_` prefix) is the failure
  mode.
- **`ERROR: relation "..." does not exist`** on the *correct* table
  name — you're connected to the **`postgres`** database, not
  **`kumunita`**. The docker-compose stack creates the `kumunita` DB
  (see `dev-db-init/` + `docker-compose.yml`); the `postgres` database
  is the default and has none of the app's schemas. The `postgres=#`
  prompt in the psql banner is the tell — re-run with
  `psql -h localhost -U kumunita -d kumunita`.
- **`function digest(unknown, unknown) does not exist`** — Postgres's
  built-in `digest()` is in the `pgcrypto` extension, which is *not*
  enabled on the app DB. For hashing a password to verify against
  `identity."AspNetUsers".PasswordHash`, use `crypt()` from `pgcrypto`
  (if available) or just `SELECT "PasswordHash"` and compare in the app
  — don't try to re-derive the hash in SQL.

Also: the **`/admin/setup`** flow returns a uniform **`404 NotFound`**
on submit by design (the controller deliberately collapses several
distinct causes into one response so an anonymous caller can't
distinguish "already set up" from "wrong token"). **A 404 on submit of
a valid token + password is success, not failure** — the setup lane is
permanently closed after a successful POST. Do not chase this as a bug;
the report "I set the password and got a 404" is the expected outcome.
