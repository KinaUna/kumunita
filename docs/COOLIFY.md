# Kumunita — Coolify Setup Guide

Practical instructions for what a host operator does on a VPS: installing
Coolify, a project + environment per neighborhood, a neighborhood's
Postgres, and the Kumunita app. A `how-to` reference only — this file has
*no* per-instance values. Where this guide and the runbook overlap, the
runbook wins:

- **`docs/OPS.md`** — the runbook: procedures, configuration reference,
  instance inventory, hardening checklist.
- **`docs/adr/0002`** — why one instance per neighborhood.

**Convention (OPS):** every step is numbered and imperative. Stamp the
sections below with **Last tested: <date>** when you run them on a live box.

## 0. Plan

| What | How often | Where |
|---|---|---|
| Install Coolify on the VPS | once per VPS | §1 |
| DNS + ports | once per VPS | §2 |
| Project + environment | once per neighborhood | §3 |
| Postgres (addon) | once per neighborhood | §4 |
| App (Coolify app) | once per neighborhood | §5 |
| Verify + hand over to runbook | once per neighborhood | §6 |

## 1. Installing Coolify (once per VPS)

Fresh Ubuntu/Debian VPS, as a user with `sudo`, key-based SSH already
configured (OPS §10):

1. Run the official one-click installer:
`curl -fsSL https://cdn.coollabs.io/coolify/install.sh | bash`
This provisions Coolify and its components behind Caddy (reverse proxy;
issues and renews Let's Encrypt automatically). For more information and
up to date instructions, see [Coolify Installation docs](https://coolify.io/docs/get-started/installation).

2. Open the wizard in the browser at `http://<vps-ip>`, choose the
installation type (Cloud — default; no external services needed for this
repo's usage), and generate the **admin login token**. Store the token in
the secrets manager; log in and create a human Coolify account.

3. Firewall: open **80/443** (Caddy; needed for domain routing and cert
issuance) and **22** (SSH — key only, source-restricted if your provider
supports it). Everything else stays closed (OPS §10).

## 2. DNS prerequisite (once per VPS, per domain)

Coolify handles TLS per **domain**, and Let's Encrypt needs the resolver to
already point at the VPS.

1. Reserve the neighborhood's domain/subdomain (e.g.
`maplewood.kumunita.example`).
2. Point **A** (own zone) or **CNAME** (hosted zone) at the VPS.
3. Wait for propagation (`dig` / `nslookup`), then proceed — attaching a domain
before propagation fails the cert check.

## 3. Project & environment — once per neighborhood

In Coolify, **resources** (apps, Postgres addons, services) live under a
**project**, and each project holds one or more **environments** (production,
staging, A/B tests, …). Every environment is a complete, independently
deployable set of resources with its own env variables and secrets. The
neighborhood's Postgres and app must be attached to the *same environment* —
that is what makes them network-adjacent and shares one scoped env-var set.

1. **Projects → Add new** (from the Coolify home). Name it after the
   neighborhood (`maplewood`). One project per neighborhood (ADR 0002).
2. Open the project → **Add a new environment** → name it `production`.
   A project must have at least one environment.
3. `production` is the only environment for now. A future staging or A/B
   instance of this neighborhood is a *second environment in the same
   project* — never a second project (one neighborhood = one project).

## 4. Postgres 18 — once per neighborhood

One **dedicated** Postgres per neighborhood (never shared — ADR 0002).

For more information see Coolify docs: [PostgreSQL](https://coolify.io/docs/databases/postgresql).

1. Inside the neighborhood's environment (`production`, §3): **Add a resource
   → Postgres**. Name it after the neighborhood (`maplewood-postgres`).
2. **Version: 18** — image parity with dev (`docker-compose.yml`) and the test
fixtures (`PostgresFixture` runs `postgres:18`).
3. **Expose no public port.** The app reaches it on the Coolify internal
network; operators reach it via the addon's psql. A public Postgres is a
hardening violation (OPS §10).
4. **Bootstrap superuser `postgres`**: set a **strong unique** password on
first creation (`POSTGRES_PASSWORD`). `postgres` is **admin/restore-only**
(OPS §5) and **never** the app connection.
5. **Create the app role + database so the app role is the database *owner***
(Open console on the addon). The app's boot block creates the `mt` and
`identity` schemas itself (ADR 0004) — schema creation needs ownership, so
   exactly this shape, mirroring `dev-db-init/01-app-role.sql`:
```
CREATE ROLE kumunita LOGIN PASSWORD '<strong-password>';
CREATE DATABASE kumunita OWNER kumunita;
```

(Coolify's "username/password" fields provision the non-superuser role;
verify the database is **owned** by it — this is the one step the addon
doesn't do out of the box. Ownership means schema creation at boot needs no
elevated/rights dance, and none was required on the live instance — OPS §10.)

6. Store `<strong-password>` in the secrets manager (OPS Procedures 1–2).
7. Verify both paths: `postgres` → `kumunita` connection, and
`kumunita` → `kumunita` (pings the console / an admin login, then the app
smoke below).

8. Backing up the database is **procedure 4** (OPS) — the addon's backup task
writes to object storage, not only this VPS.

## 5. The app — once per neighborhood

For more information see Coolify docs: [Deploy Public Repository](https://coolify.io/docs/applications/ci-cd/github/public-repository).

1. Inside the same `production` environment (same one the Postgres addon
   lives in): **Add a resource → Application** → **GitHub source** →
   `KinaUna/kumunita`. **Branch: `release`** — production deploys from
   `release` only; `main` never deploys (OPS §3). The deployed commit is the
   image version — there is no registry.
2. **Buildpack:** Dockerfile; file `Dockerfile` at the repo root; default
context. (The multi-stage build compiles the TS client, publishes the app,
and runs on the ASP.NET 10 slim image.)
3. **Ports:** app listens on **8080** (the `Dockerfile` ENV). Keep the Coolify
port **internal-only** — reach the app through the domain, not an exposed
port (OPS §10).
4. **Environment variables & secrets** (full reference: OPS *Configuration
   reference*): set these at the **environment** level so they apply to both
   the app and the Postgres addon — the default scope here (app-level works
   equally well; pick one and stay consistent):

| Variable | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Community__Name` | the neighborhood's display name |
| `Community__SupportEmail` | support contact from the inventory |
| `ConnectionStrings__Kumunita` | `Host=<addon-service-name>;Port=5432;Database=kumunita;Username=kumunita;Password=<from §4>;Include Error Detail=true` — **secret** |
| `SMTP__Host` / `SMTP__Port` | the relay for verification + seed-admin mail — see **§5.1** (and OPS §7) |
| `SMTP__User` / `SMTP__Pass` | required by most real relays — see **§5.1** |
| `SMTP__Secure` | optional; `Tls` (STARTTLS, default) or `None` (plain, local-only) — **see BCL constraint in §5.1** |
| `SeedAdmin__Email` / `SeedAdmin__Token` | one-time setup token — **created per OPS Procedure 2, removed from env after first login** |

The `Host` value is the addon's internal service name (visible on the
addon's page / psql connection block).

### 5.1 SMTP — concrete env values

The `SmtpSender` reads six options from the `SMTP` section: `Host`, `Port`,
`User`, `Pass`, `Secure`, and `From` (`SmtpOptions` in
`src/Kumunita.Core/Identity/SmtpSender.cs`); all are bound through
`Configure<SmtpOptions>` in `Program.cs`. The BCL `SmtpClient` is used as-is,
and the BCL **only supports STARTTLS** (`EnableSsl = true`) — there is no
implicit-TLS / SMTPS mode (the .NET API reference for `SmtpClient.EnableSsl`
is explicit that an "SSL session established up front," i.e. port 465, is
**not currently supported**). Practical consequence: **pick a relay that
exposes a STARTTLS port** (conventionally 587). Virtually every SaaS relay —
Mailgun, Resend, MailerSend, Postmark, SendGrid, MS 365 — does; if yours is
465-only, it needs a relay swap or a `SmtpSender` implementation change before
deploy (see note in §5.1B).

**A. Local dev loop (compose / Mailpit):** run via `docker-compose.yml` at the
repo root, which brings up `Mailpit` (SMTP on port 1025, web UI on 8025) plus
Postgres 18 in the same network. Only reachable from inside that network — this
is the `ASPNETCORE_ENVIRONMENT=Development` path, not a Coolify deploy:

| Variable | Value |
|---|---|
| `SMTP__Host` | `mailpit` (the compose service name) |
| `SMTP__Port` | `1025` (Mailpit's SMTP port; no AUTH, no TLS needed — Mailpit is a local relay) |
| `SMTP__Secure` | `None` (plain SMTP; the honest shape for a loopback-only relay) |
| `SMTP__User` / `SMTP__Pass` | leave unset (Mailpit doesn't require auth) |
| `SMTP__From` | optional; the dev loop has no strict relay to reject a missing `From` |

**B. A real relay (test server or production):** most relays require `User` +
`Pass` **and** a STARTTLS port. The shape to use:

| Variable | Value |
|---|---|
| `SMTP__Host` | e.g. `smtp.mailgun.org`, `smtp.resend.com`, `relay.mailprovider.com` — your provider's host |
| `SMTP__Port` | the relay's **STARTTLS** port — conventionally **587**; use the port your provider documents for STARTTLS (some are 2525 or 5870 — check the provider's SMTP-settings page, not a "webmail login" port) |
| `SMTP__Secure` | `Tls` (the default; `None` would talk plain SMTP, which leaks credentials in transit and is not a production shape) |
| `SMTP__User` | the relay's username (often the sender's email or an API-issued account) — **secret** |
| `SMTP__Pass` | the relay's password / API key — **secret**; store in the secrets manager, inject via env, never in the runbook |
| `SMTP__From` | the resident-facing address shown in verification emails (often the same as `Community__SupportEmail`) |

> **465 / implicit TLS (SMTPS) is not supported by the BCL `SmtpClient`.** If
> the only relay available to the instance is 465-only, your options are
> (a) pick a different relay that also exposes a STARTTLS port (most do), or
> (b) replace `SmtpSender`'s BCL `SmtpClient` with a hand-rolled `Sockets`
> client that wraps the stream in `SslStream.AuthenticateAsClient(...)` before
> speaking SMTP — i.e. a real code change, not an env value. Do **not** set
> `SMTP__Secure=Ssl`: the value is rejected with an actionable error message
> that says exactly this.
>
> **`SMTP__User` and `SMTP__Pass` are a pair.** Setting only one (or an empty
> string for one and a real value for the other) is a configuration error:
> `SmtpSender.SendAsync` throws an `InvalidOperationException` before opening
> the client rather than failing opaquely at the `AUTH` handshake once on the
> wire. See the `SmtpOptions.User` doc comment in `SmtpSender.cs` for the
> invariant.

Whichever shape you pick, **one strong `SeedAdmin__Email` / one strong token**
is required for the first-boot admin lane (OPS Procedure 2, step 1). Generate it
with `openssl rand -hex 24`, store it in your secrets manager, and set it before
the first boot. The token is **one-time** — the app invalidates it on the admin's
first login (OPS Procedure 2, step 4: then remove `SeedAdmin__*` from env so
nothing reusable remains).

> **Why this matters:** verification + admin handoff both depend on a *delivered*
> email. An unconfigured SMTP host **throws** on first send (SmtpSender is
deliberate about not "sending to nowhere") — the durable handler then
retries/dead-letters per §6.2 and `/health` flips to `degraded` so it is visible.
Verify the mail landed on the real inbox (OPS Procedure 2, step 5) before
considering the test "working".

5. **Domains:** attach the neighborhood's domain (§2).
(Coolify + Let's Encrypt) — nothing else to do (OPS §6).
6. **Health check:** path `GET /health`, expected `200`.
7. **Deploy.**

## 6. Verify (then switch to the runbook)

1. `https://<domain>/` renders with the correct `Community__Name`, over
HTTPS.
2. `GET /health` → `{"status":"ok","database":"ok","build":"<sha>",…}` and
`build` matches the deployed commit (`release` tip).
3. **First boot** (fresh database): the `mt` schema initializes with no
operator step (ADR 0004) — the log shows **First boot** exactly once;
subsequent boots are a no-op.
4. Seeded admin exists; log in **once** with the setup token → set password;
the token invalidates on use. Then **remove `SeedAdmin__*` from env**
(nothing reusable remains to leak — OPS Procedure 2, step 4).
5. `/health` reports `ok` (email dead-letter count 0); a test email arrives
at the destination inbox (OPS Procedure 2 step 5).
6. Record the instance in the **OPS inventory** (domain, VPS, DB name, commit
SHA, admin contact, dates).
7. Backups enabled / scheduled per OPS Procedure 4.

## Troubleshooting (quick)

| Symptom | First look |
|---|---|
| Health check `database` failed on boot | `Host` in the connection string (should be the internal addon name, not `localhost`) |
| Boot loops, "permission denied: schema" | the `kumunita` role isn't the **owner** of the `kumunita` database — re-run §4 step 5 |
| TLS not issued | DNS not pointing at the VPS yet (§2), or port 80/443 closed |
| Nothing renders, port 8080 open | you are (a) not on the `release` branch (OPS §3) or (b) hitting the exposed internal port directly instead of the domain |
