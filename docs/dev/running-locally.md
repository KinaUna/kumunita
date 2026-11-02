---
id: dev-running-locally
title: "Running locally"
tier: living
kind: guide
status: current
audiences: [developer, agent]
related:
  - docs/ops/demo-site.md
  - docs/ops/configuration-reference.md
  - docs/dev/README.md
lane: improve-docs
updated: 2026-11-02
---

> **Abstract:** how to run Kumunita locally — the dev container, the no-app-
> container loop, the sample-data / demo accounts, and the prod topology.
> This was the `## Running` section of the front-page
> [`README.md`](../../README.md); it now lives here under the developer
> doctrine so the front page stays a ≤120-line door. **Who is it for:** a
> developer or agent who wants a working local instance (or the demo-site
> recipe). **When to read the rest:** when you're about to boot the app, seed
> sample data, or reason about the local vs prod topology.

# Running locally
*Dev (Docker):* `docker compose up -d --build` — Postgres on
`localhost:5433`, the app on `http://localhost:5080` (built from `Dockerfile`,
running `Development` so the `mt` schema auto-applies on a fresh database —
ADR 0004), and Mailpit for SMTP (`localhost:1025`, UI at `http://localhost:8025`).
Logins survive container rebuilds: the data-protection keyring persists in the
`kumunita_dpkeys` named volume (same `/data/dataprotection-keys` container
path as prod — COOLIFY §5.2), so `--build` recreations don't wipe session/
antiforgery state.
Smoke test: `GET /health` → `{"status":"ok","database":"ok","build":"<sha>",…}` and `/`
renders the configured `Community__Name`. On a **fresh** database the versioned boot block
initializes the `mt` schema with no operator step (ADR 0004) — the log shows **First boot**
exactly once, and subsequent boots are a no-op.

**Sample data & demo accounts.** On a **fresh** database, the first-boot lane can seed a
mock neighborhood so an instance is immediately exercisable — residents, groups, community
+ group posts with replies, events with RSVPs, tags, a resident blog, and de/fr/da event
translations (ADR 0055, extended by ADR 0056). It is **opt-in via `SampleData__Enabled=true`**
and first-boot only, so a real deployment that never sets the flag can never see it. A
`SampleData__Enabled` instance that was already booted also gets the **missing** sample
event `de` / `fr` / `da` translations backfilled on every warm boot — create-if-missing,
idempotent, never overwriting a Translator's in-app edit (ADR 0060). Two postures, one
seeder (ADR 0056):

- **Development** (local dev loop, `appsettings.Development.json` / `docker-compose.yml`
  already set the flag) — the documented weak demo credentials below, printed to the log;
  the seed admin keeps its `SeedAdmin__` setup-token lane *plus* a weak demo password.
- **Deployed demo site** (a Coolify `Production` instance with `SampleData__Enabled=true`)
  — the seed admin **keeps only its `SeedAdmin__` setup-token lane** (no weak password),
  the other demo accounts get **random high-entropy passwords**, and a single credentials
  summary is **emailed to the seed admin's address** through the durable outbox. No weak
  credential is stored on the public instance; the operator's admin inbox is the only place
  the random passwords appear.

The Development-posture demo logins (all `examplium.com`, a deliberately-fictional domain
kept distinct from the real `kumunita.com`):

| Account | E-mail | Password | Standing |
|---|---|---|---|
| Admin | `admin@examplium.com` | `Admin123!` | `GlobalAdmin` (the seeded first-boot admin, now with a password) |
| Maria | `moderator@examplium.com` | `Mod1234!` | `Moderator` scoped to Safety + Social |
| Sophie | `translator@examplium.com` | `Trans123!` | `Translator` (authors the de/fr/da translation rows) |
| Anna | `anna@examplium.com` | `Resident123!` | resident; contact-opted-in; Warsaw timezone |
| Ben | `ben@examplium.com` | `Resident123!` | resident |
| Carla | `carla@examplium.com` | `Resident123!` | resident; contact-opted-in; owns the public "Street Green" group |
| David | `david@examplium.com` | `Resident123!` | resident; Prague timezone |

**Deployed demo site recipe (ADR 0056).** A Coolify `Production` app + dedicated Postgres,
with `SampleData__Enabled=true`, the `examplium.com` `Community__*` / `SeedAdmin__*` names,
and a **fresh** DB → first boot seeds the full neighborhood. The seed admin signs in with
its one-time `SeedAdmin__` setup token (as on any real deployment), and the demo
credentials (random per account) arrive as a single e-mail to that admin's address —
the table above is the **Development** credential set, not the deployed one. The full
step-by-step (including the disposable-DB caveat) is [the demo-site runbook](../ops/demo-site.md).

Wipe everything with `docker compose down -v` and rebuild — the seeder re-seeds on the
next first boot (it is idempotent: keyed by e-mail, and the pristine gate keeps it from
ever re-running its content on a warm database — the only warm-boot write is the
create-if-missing sample-event translation backfill, ADR 0060).

**Locking sample-account password changes (ADR 0138) and two-factor (MFA) (ADR 0194).** A
demo site's sample accounts are *shared* credentials — anyone who signs in as "Anna" signs in
as the *same* account — so a visitor testing the demo could change one of those passwords (or
enable two-factor, minting a TOTP secret + recovery codes that break the shared credential)
and break the credential for the everyone else who uses it. On a `SampleData__Enabled`
instance (and **only** there — a real deployment never carries the flag, so this surface is
unreachable by construction), a `GlobalAdmin` can turn on a **change-password lock** (ADR
0138) and a **two-factor (MFA) lock** (ADR 0194) at `/admin/platform` → **Sample data** →
`/admin/sample`: the demo accounts (the closed sample set) are then locked out of the
resident self-serve "Change password" surface and the MFA settings surface
(`/settings/security/mfa` — enrollment / recovery-rotation / disable), **except the sample
`GlobalAdmin`**, whose own credential and MFA lane the admin must still be able to keep
(ADR 0138 / 0194 sample-admin exemption). Real (non-sample) accounts are never affected. The
two locks are independent `GlobalAdmin`-settled instance values (audited,
`sample.set-password-lock` + `sample.set-mfa-lock`) and plain read decisions
(`IsChangePasswordLockedForAsync` + `IsMfaLockedForAsync`), so the resident form, the locked
notice, and the write-path guards agree by construction. The sample `GlobalAdmin` is also
exempt from the login leg 1.5b forced-MFA enrollment redirect (the
`IsSampleDataAccountAsync` seam) so a locked instance still lets the demo admin sign in with
the shared password (a real, non-sample `GlobalAdmin` is still forced to enroll MFA).

*Dev (no app container):* `docker compose up -d db`,
`npm run build` in `src/Kumunita.Web/`, copy
`src/Kumunita.Web/appsettings.Development.Local.json.example` to
`appsettings.Development.Local.json` (git-ignored — carries the dev DB
password; default `kumunita`), then
`dotnet run --project src/Kumunita.Web` (settings from
`appsettings.Development.json` + `appsettings.Development.Local.json`,
DB on `localhost:5433`).

*Prod:* one neighborhood per instance — identical image + dedicated Postgres +
env per the [configuration reference](../ops/configuration-reference.md), TLS via
Coolify/Let's Encrypt, `/health` monitored, scheduled Postgres backups.
