# Kumunita

A self-hosted community platform for a single neighborhood. Residents can find and
get to know one another, share announcements and events, discuss topics in functional
areas, and work on projects together — with privacy-first, user-controlled access.

Each deployment serves **one neighborhood**. The same codebase is deployed
independently per community (its own container + its own Postgres), so there is no
multi-tenant data model: scaling to many neighborhoods is a *configuration* concern,
not a code concern.

**Naming.** *Kumunita* is the platform (this repository). Each deployed instance shows its own community name (e.g. "Maplewood Residents"), set via `Community__Name`.

## Status

**M0 is in place.** Deployable scaffold: `Kumunita.slnx` (`Kumunita.Core`,
`Kumunita.Web`, `Kumunita.Core.Tests`), multi-stage Docker image, the first
versioned schema change (`mt.community`, `KumunitaFeature`), a `/health`
liveness probe that requires a reachable Postgres, and a home page that renders
`Community__Name`. First Coolify deployment is the remaining M0 item. **M1** next:
Identity, groups, delegation, and the authorization model above.

## Principles

- **Lean.** Smallest stack that meets the requirements. No premature services, no ceremony.
- **Privacy-first.** The author's choice of audience is absolute by default.
- **Audit by default.** Access to restricted content is always logged.
- **Boring where it can be.** Server-rendered pages, plain TypeScript, one database.
- **Integration over features.** A neighborhood's life is fragmented — people,
  knowledge, trust, problems. Kumunita's value is the *linkage* that turns those
  parts into a whole, not the count of parts. This is the spine of our
  development philosophy: see [`docs/philosophy/`](docs/philosophy/).

## Features

- Resident directory (profiles, opt-in contact details)
- Announcements & discussions, organized by **functional components** (Safety, Maintenance, Social, Governance, …)
- Events with RSVP and reminders
- Collaborative projects (goals, tasks, contributors)
- **Groups** for reusable access lists
- **Delegation** — owners grant family/caretakers scoped access
- Moderation with component-scoped moderators and full audit
- **Multilingual** — UI and platform texts (terms, about, help) are translatable;
  admins add/remove supported languages and set the default in-app (ADR 0005)

## Tech stack

- **ASP.NET Core 10** — MVC + Razor, server-rendered
- **Plain TypeScript** — compiled with `tsc` only (no bundler, no dev server)
- **Marten** — Postgres document store (domain); **EF Core** strictly for ASP.NET Identity tables
- **Wolverine** — in-process messaging, scheduled jobs, CQRS-lite
- **No event sourcing** — documents + projections instead
- **ASP.NET Core Identity** (cookie) now; **OpenIddict** (OIDC) later, for cross-neighborhood federation

## Architecture

A **modular monolith**. Identity and access are split into three in-process bounded
contexts behind interfaces — extractable later, not separate services today:

- **IdentityModule** — lean authentication; issues a thin principal (`subjectId`,
  `isVerifiedResident`, base roles). Nothing relational in the token.
- **UserInfoModule** — who people are: profiles, groups, delegation grants.
- **AuthorizationModule** — what they may do: audience evaluation + policy. Always audits.

The guiding rule: **thin token, fat authorization service.** "Can this person see that
post?" is never a claim — it's a query resolved per request — so the identity story
stays trivial and the authorization rules can grow freely.

### Access model

- An **audience** is a set of grants to **users** and/or **groups**, combined with
  **Any** (union, default) or **All** (intersection).
- **Groups** are the reuse unit — grant a post to a group once; membership changes ripple everywhere.
- **Delegation** lets an owner grant another person scoped access; the system resolves an *effective principal* for that actor.
- **Moderator access** to audience-restricted content is **off by default**. A filed **report** grants the assigned moderator audited access to that item; an admin can enable standing moderator visibility per scope.
- **Audit** of access decisions is always on.

### Roles

- **GlobalAdmin** — full control; manages moderators and their component scope.
- **Moderator** — scoped to one or more functional components.
- **Member** — verified resident.

## Deployment

- One instance per neighborhood, on a **VPS via Coolify**.
- Docker multi-stage build (compile TS with `tsc`, publish, runtime) + **Postgres**.
- Config via environment: community name, SMTP, seeded admin.
- TLS via Coolify / Let's Encrypt; `/health` endpoint; scheduled Postgres backups.

## Roadmap

- **M0** — Deployable scaffold: solution, Docker, Coolify deploys "hello" with a live DB.
- **M1** — Identity, groups, delegation, and the authorization model above.
- **M2** — Directory & profiles with visibility rules.
- **M3** — Posts/announcements in components; moderation + reports.
- **M4** — Events, RSVPs, reminders.
- **M5** — Projects (goals, tasks, contributors).
- **M6** — Portability (export/import), iCal, notifications, search, multilingual
  support (ADR 0005), responsive pass.

## Deferred (future, by design)

- **Machine translation of user-generated content** — optional and opt-in if it
  ever ships: per-item, clearly labeled, and the MT provider becomes a third-party
  boundary (ADR 0005 C, SECURITY.md §6). Until then UGC is rendered as authored.
- **Geographic zones** as metadata + display filtering (not part of the core access model).
- **Cross-neighborhood federation** — a standalone OpenIddict IdP; global identity, local authorization.
- **Group helpers** — suggest/populate groups (neighbors from addresses, family from household).
- **MCP**, calendar integration, cross-neighborhood data migration.

## Running

*Dev (Docker):* `docker compose up -d --build` — Postgres on
`localhost:5433`, the app on `http://localhost:5080` (built from `Dockerfile`,
running `Development` so the `mt` schema auto-applies on a fresh database —
ADR 0004), and Mailpit for SMTP (`localhost:1025`, UI at `http://localhost:8025`).
Smoke test: `GET /health` → `{"status":"ok","database":"ok"}` and `/` renders the
configured `Community__Name`.

*Dev (no app container):* `docker compose up -d db`,
`npm run build` in `src/Kumunita.Web/`, then
`dotnet run --project src/Kumunita.Web` (settings from
`appsettings.Development.json`, DB on `localhost:5433`).

*Prod:* one neighborhood per instance — identical image + dedicated Postgres +
env per the [OPS.md configuration reference](docs/OPS.md), TLS via
Coolify/Let's Encrypt, `/health` monitored, scheduled Postgres backups.

## Documentation

- `docs/philosophy/how-it-works.md` — **how the platform works, in plain
  language** — for residents who aren't technical collaborators: what it does,
  how privacy and moderation actually behave, and how to give feedback that
  helps. No code background needed.
- `docs/philosophy/` — **our development philosophy**: why the platform exists (it links a
  neighborhood's fragmented life into a whole) and how we build accordingly. Start at
  [`docs/philosophy/START-HERE.md`](docs/philosophy/START-HERE.md)
- `docs/SECURITY.md` — **security & privacy: the top priority** — threat model, data classes, control map
- `docs/ARCHITECTURE.md` — detailed stack, data model, module boundaries
- `docs/OPS.md` — operations runbook: provisioning, upgrades, backups, restore, security
- `docs/adr/` — architecture decision records (0001–0005)
