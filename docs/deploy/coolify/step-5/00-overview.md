---
id: deploy-coolify-50-app
title: "Coolify — 5: The app (overview + env vars)"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/COOLIFY.md
  - docs/ops/configuration-reference.md
  - docs/ops/environments.md
updated: 2026-11-02
---

> **Abstract:** step 5's overview — how to attach the Kumunita app in Coolify (source, buildpack, ports) and the **complete** environment-variable table, each variable marked required/recommended/optional/secret. **Who is it for:** the host operator. **When to read the rest:** when configuring the app's env vars, before first deploy.

# 5 — The app (once per neighborhood)
For more information see Coolify docs: [Deploy Public Repository](https://coolify.io/docs/applications/ci-cd/github/public-repository).

1. Inside the same `production` environment (same one the Postgres addon
   lives in): **Add a resource → Application** → **GitHub source** →
   `KinaUna/kumunita`. **Branch: `release`** — production deploys from
   `release` only; `main` never deploys (the [environments runbook](../../../ops/environments.md)). The deployed commit is the
   image version — there is no registry.
2. **Buildpack:** Dockerfile; file `Dockerfile` at the repo root; default
context. (The multi-stage build compiles the TS client, publishes the app,
and runs on the ASP.NET 10 slim image.)
3. **Ports:** app listens on **8080** (the `Dockerfile` ENV). Keep the Coolify
port **internal-only** — reach the app through the domain, not an exposed
port (the [hardening checklist](../../../ops/hardening-checklist.md)).
4. **Environment variables & secrets** (this table is the **complete**
   configurable surface; full detail: the [configuration reference](../../../ops/configuration-reference.md)): set
   these at the **environment** level so they apply to both the app and the
   Postgres addon — the default scope here (app-level works equally well;
   pick one and stay consistent). Every variable the app reads from the
   environment is listed below, marked **required** / **recommended** /
   **optional** (and **secret** where it is a credential). Omitting an
   **optional** variable keeps its in-code default.

| Variable | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | **required** — `Production` |
| `Community__Name` | **required** — the neighborhood's display name |
| `Community__SupportEmail` | **required** — support contact from the inventory |
| `ConnectionStrings__Kumunita` | **required, secret** — `Host=<addon-service-name>;Port=5432;Database=kumunita;Username=kumunita;Password=<from [§4](../40-postgres.md)>;Include Error Detail=true`. The `Host` value is the addon's internal service name (from the addon's page / psql connection block), not `localhost`. |
| `ConnectionStrings__KumunitaMigrate` | **required, secret (F22 / ADR 0171)** — the **migration role** connection string, same shape as `Kumunita` but `Username=kumunita_migrate` + `Password=<migrate-password from [§4](../40-postgres.md)>`. The boot path (`SchemaBootstrap`) runs EF migrations + the Marten schema-apply + the `identity` DML grants as this role; when **unset** the app degrades to the single-role baseline (a not-yet-migrated instance). See the [configuration reference](../../../ops/configuration-reference.md) |
| `SMTP__Host` | **required for real mail** — the relay for verification + seed-admin mail — see **[§5.1](51-smtp.md)** (and the [email (SMTP) runbook](../../../ops/email-smtp.md)) |
| `SMTP__Port` | optional — the relay's STARTTLS port, conventionally **587** (default `587`); see **[§5.1](51-smtp.md)** |
| `SMTP__Secure` | optional — `Tls` (STARTTLS, default) or `None` (plain, local-only) — **see BCL constraint in [§5.1](51-smtp.md)** |
| `SMTP__User` | **required by most real relays, secret** — see **[§5.1](51-smtp.md)** |
| `SMTP__Pass` | **required by most real relays, secret** — see **[§5.1](51-smtp.md)** |
| `SMTP__From` | optional — the resident-facing `From` address shown in verification emails (often `Community__SupportEmail`); empty = the relay's default — see **[§5.1](51-smtp.md)** |
| `SMTP__UseDefaultCredentials` | optional, **a documented no-op** under the MailKit transport (ADR 0131) — leave unset |
| `SeedAdmin__Email` | **one-time, first boot only** — the seed-admin bootstrap lane — **created per the [first-boot runbook](../../../ops/first-boot.md), removed from env after first login** |
| `SeedAdmin__Token` | **one-time, first boot only, secret** — the setup token, consumed + invalidated on the admin's first login — **removed from env after first login** |
| `Verification__BaseUrl` | **recommended — set for correct mail links** — this instance's public base URL: the [§2](../20-dns.md) domain, scheme included, **no trailing slash** (e.g. `https://maplewood.kumunita.example`). The verification email's link is built from it; a recipient's mail client cannot resolve a bare `/account/verify` path, so an unset value degrades the email to a relative path. See **[§5.3](53-domains.md)**. |
| `Verification__TtlDays` | optional — how long a verification link stays valid (default `14`) |
| `Verification__MaxVerifyAttempts` | optional — max clicks on a verification link before it expires (default `3`) |
| `Health__Token` | optional, **secret** — shared-secret granting the full `/health` diagnostic payload. **Minimal-by-default (SEC U16 / M4):** an anonymous caller *always* gets only the liveness probe (`status`/`database`/`app`/`elapsedMs`); the full diagnostic (mail detail, dead-letter count, build SHA) requires this token as the `X-Health-Token` header, or an authenticated GlobalAdmin session. The token compare is constant-time and the SMTP relay handshake is cached ~60 s. The Coolify / edge-proxy health check works with the probe alone — set this token only to read the operator diagnostics. Recommended for production: `openssl rand -hex 24` |
| `DataProtection__KeysDirectory` | **recommended — required in production** — a persistent directory (Coolify's `/data` volume, e.g. `/data/keys`) holding the data-protection keyring. See **[§5.2](52-data-protection.md)** below. Omit to keep the in-memory default (sessions die on every redeploy). |
| `Media__RootPath` | **recommended — required in production** — the path of the dedicated media volume holding the *bytes* (ADR 0011). See **[§5.2A](52a-media-volume.md)** below. Omit to keep the in-image default (`/app/media`) — in production, uploads are then lost on every redeploy. |
| `Media__MaxBytes` | optional — **fallback** per-file cap in bytes (default `10485760` = 10 MiB; `0` = no cap). Primarily admin-set in-app (`/admin/storage/settings`); this env value is the fallback when no override is set — the [configuration reference](../../../ops/configuration-reference.md) (ADR 0135) |
| `Media__MaxPlatformBytes` | optional — **platform-wide** storage-space cap in bytes (default absent/`0` = unlimited). When `> 0`, caps how much resident content the platform may hold: `/admin/storage`'s "available" is capped to the remaining budget, and all new uploads are blocked `413` once spent or the volume's free space drops below `100` MiB (ADR 0136) |
| `Media__AllowedContentTypes` | optional — comma-sep Content-Type allowlist for the **image** lane (default `image/jpeg,image/png,image/webp,image/gif`, SVG excluded) |
| `Media__AttachmentAllowedContentTypes` | optional — comma-sep allowlist for the **attachment (download)** lane, distinct from the image lane (ADR 0034); same `Media__MaxBytes` cap — the [configuration reference](../../../ops/configuration-reference.md) |
| `SampleData__Enabled` | **demo instances only (ADR 0055 / 0056 / 0060)** — set `true` only for a **deployed demo site** (`Production` + a fresh DB), so first boot seeds the mock neighborhood (including the sample events' `de`/`fr`/`da` translations). Omit on a real neighborhood (the seeder **and** the warm-boot sample-event translation backfill are then unreachable by construction). In the deploy posture the seed admin keeps its `SeedAdmin__` setup-token lane, the other demo accounts get random high-entropy passwords, and a single credentials summary is emailed to the seed admin's address — no weak credential is stored on the public instance. See the [configuration reference](../../../ops/configuration-reference.md) + the [README](../../../../README.md) §Running. |
| `AuditPurge__RoutineDays` | optional — routine audit-row retention in days before the scheduled purge (default `90`); the [monitoring runbook](../../../ops/monitoring.md) job |
| `AuditPurge__UnresolvedReportDays` | optional — extra days report-attached audit rows are kept after the report resolves (default `365`) |
| `EventReminder__WindowHours` | optional — the "remind the day before" window for event reminders, in hours (default `24`); the [monitoring runbook](../../../ops/monitoring.md) job |
| `Logging__File__Directory` | optional — where the dated `app-*.log` files are written (default `logs`) |
| `Logging__File__RetentionDays` | optional — log-file retention before the at-boot delete pass (default `14`) |
| `KUMUNITA_SMTP_HEALTH_TIMEOUT_MS` | optional — a timeout (ms) override for the `/health` SMTP relay probe (diagnostic tuning; read directly from the environment) |
| `SOURCE_COMMIT` | optional — the commit SHA surfaced as `/health`'s `build` field (usually injected by the build/CI; defaults to `local`) |
| `DbTls__SslMode` | optional (F21 / SRV-04) — Postgres TLS posture for the app↔DB connection (`Prefer` default → byte-identical to baseline; `Require` / `Verify CA`). **`Verify CA` against a non-TLS server is a hard connection failure** — enable server-side TLS before flipping to it. Distinct from the [§5.3](53-domains.md) domain TLS — the [configuration reference](../../../ops/configuration-reference.md) |
| `DbTls__CaPath` | optional (F21) — path to the CA bundle for `DbTls__SslMode=Verify CA`; re-provision on CA rotation |
| `SENTRY_DSN` | optional, **secret** (F10 / OBS-02) — the Sentry error-tracker DSN. **OFF by default** — unset = a no-op (no PII leaves the box); set it to enable capture on an instance. the [configuration reference](../../../ops/configuration-reference.md) |
| `VirusScan__Host` / `VirusScan__Port` | optional (F16 / UPL-02) — point the serve-lane scan at a local `clamd` daemon (`127.0.0.1` / `3310`). **Fail-open by default** — a missing/errored daemon degrades to `Clean`, so until ClamAV is live on the volume the active control stays the `nosniff` + `attachment` serve mitigation. `VirusScan__MaxScanBytes` caps the bytes scanned per file (default 100 MiB) |
| `MailSuppression__IngestToken` | optional, **secret** (F17 / MAIL-03) — shared secret for `POST /api/mail/suppress`; the provider's bounce/complaint webhook sends it as the `X-Suppression-Token` header (mismatch → 403). Until the webhook is pointed at this route the suppression list stays empty and the mail-out gate is a no-op |
| `KUMUNITA__SIGNUP__BOT_CHECK` / `...__SITE_KEY` | optional (F9 / AUTH-15) — the Turnstile bot-challenge seam; **OFF by default** (the `SITE_KEY` is the owner's key; until set, a flag-on instance **fails closed**). the [hardening checklist](../../../ops/hardening-checklist.md) |
