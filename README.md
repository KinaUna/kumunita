# Kumunita

A self-hosted community platform for a single neighborhood. Residents can find
and get to know one another, share announcements and events, discuss topics in
functional areas, and work on projects together — with privacy-first,
user-controlled access.

Each deployment serves **one neighborhood**. The same codebase is deployed
independently per community (its own container + its own Postgres), so there is
no multi-tenant data model: scaling to many neighborhoods is a *configuration*
concern, not a code concern.

**Naming.** *Kumunita* is the platform (this repository). Each deployed
instance shows its own community name (e.g. "Maplewood Residents"), set via
`Community__Name`.

## Status

M1–M37 and every named lane are **done** — identity, groups & delegation,
directory & profiles, posts + moderation, multilingual, rich content, guardian
controls, pages, events + calendar, projects, notifications, messaging, PWA,
portability, storage, analytics, kiosk, @mentions, personal notes, and the
`IMPROVE` + prod-hardening integration lanes — on one server-rendered stack
over a single Postgres. The detailed, milestone-by-milestone report lives in
[`docs/STATUS.md`](docs/STATUS.md); the roadmap's source of truth is
[`src/Kumunita.Web/Milestones.cs`](src/Kumunita.Web/Milestones.cs) (narrated at
[`docs/product/roadmap.md`](docs/product/roadmap.md)).

## Principles

- **Lean.** Smallest stack that meets the requirements. No premature services, no ceremony.
- **Privacy-first.** The author's choice of audience is absolute by default.
- **Audit by default.** Access to restricted content is always logged.
- **Boring where it can be.** Server-rendered pages, plain TypeScript, one database.
- **Integration over features.** A neighborhood's life is fragmented — Kumunita's
  value is the *linkage* that turns those parts into a whole. The full argument
  is in [`docs/philosophy/`](docs/philosophy/).

## What it does

The current feature surface — resident directory, announcements & discussions,
groups & delegation, guardian controls, multilingual, rich content, events,
projects, messaging, and more — is listed in
[`docs/product/features.md`](docs/product/features.md).

## Tech stack

- **ASP.NET Core 10** — MVC + Razor, server-rendered
- **Plain TypeScript** — compiled with `tsc` only (no bundler, no dev server)
- **Marten** — Postgres document store (domain); **EF Core** strictly for ASP.NET Identity tables
- **Wolverine** — in-process messaging, scheduled jobs, CQRS-lite
- **No event sourcing** — documents + projections instead
- **ASP.NET Core Identity** (cookie) now; **OpenIddict** (OIDC) later, for cross-neighborhood federation

## Architecture

A **modular monolith**. Identity and access are split into three in-process
bounded contexts — **Identity** (lean authentication), **UserInfo** (who people
are: profiles, groups, delegation), **Authorization** (what they may do:
audience + policy, always auditing) — behind interfaces, extractable later.
The guiding rule: **thin token, fat authorization** — "may this actor see that?"
is a query resolved per request, never a claim. The full map (data model,
module boundaries, the CQRS/Wolverine convention, the extension seams) is in
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

## Deployment

One instance per neighborhood, on a **VPS via Coolify**: a Docker multi-stage
build (compile TS with `tsc`, publish, runtime) + a dedicated Postgres, config
via environment (community name, SMTP, seeded admin), TLS via Let's Encrypt, a
`/health` endpoint, and scheduled backups. The runbooks are in
[`docs/ops/`](docs/ops/) and the deployment guide in
[`docs/deploy/coolify/`](docs/deploy/coolify/00-plan.md).

## Quickstart

*Dev (Docker):* `docker compose up -d --build` — Postgres on `localhost:5433`,
the app on `http://localhost:5080` (the `mt` schema auto-applies on a fresh
database). Smoke test: `GET /health` → `{"status":"ok","database":"ok",…}`.
The full local loop, sample data + demo accounts, and the prod topology are in
[`docs/dev/running-locally.md`](docs/dev/running-locally.md).

## Where to go next

Start from the entry page for *you* — links and one-line reasons only, so they
cannot drift:

- **Resident** (a person in the neighborhood) → [`docs/start/resident.md`](docs/start/resident.md)
- **Community admin** (a moderator / global admin) → [`docs/start/community-admin.md`](docs/start/community-admin.md)
- **Platform maintainer** (operates the deployment) → [`docs/start/platform-maintainer.md`](docs/start/platform-maintainer.md)
- **Developer** (writes code in this repo) → [`docs/start/developer.md`](docs/start/developer.md)
- **Agent** (an AI assistant working in this repo) → [`docs/start/agent.md`](docs/start/agent.md)

The full docs map — tiers, conventions, and every folder — is in
[`docs/README.md`](docs/README.md).
