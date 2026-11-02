---
id: ops-configuration-reference
title: "Configuration reference"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/ops/environments.md
  - docs/adr/0011-media-and-file-storage.md
  - docs/adr/0171-least-privilege-db-role.md
updated: 2026-11-02
---

> **Abstract:** the complete per-instance env reference (identity, mail,
> media, health, audit, DB-TLS, bot-check and every other knob), the
> "config is state" rule, and the instance inventory table. **Who is it
> for:** the host operator, configuring a new instance or auditing an
> existing one. **When to read the rest:** before provisioning, or when a
> behavior you can't explain turns out to be an env value.

# Configuration reference

All per-instance identity and integration is env. The *image* is identical everywhere.

| Variable                    | Req       | Secret | Description                                    |
|-----------------------------|-----------|--------|------------------------------------------------|
| `ASPNETCORE_ENVIRONMENT`    | Yes       | No     | `Production`                                   |
| `Community__Name`           | Yes       | No     | Display name shown to residents                |
| `Community__SupportEmail`   | Yes       | No     | Contact shown in footer / reports              |
| `ConnectionStrings__Kumunita` | Yes      | Yes    | Postgres connection string                     |
| `SMTP__Host`                | Yes       | No     | SMTP server                                    |
| `SMTP__Port`                | Yes       | No     | The relay's STARTTLS port (conventionally 587; the BCL `SmtpClient` does not support implicit TLS / 465) |
| `SMTP__Secure`              | Optional  | No     | `Tls` (STARTTLS — default; the BCL's only TLS mode) \| `None` (plain, local-only)                      |
| `SMTP__User` / `SMTP__Pass` | If auth   | — / Yes| SMTP credentials                               |
| `SeedAdmin__Email`          | First-run | No     | Initial GlobalAdmin address                    |
| `SeedAdmin__Token`          | First-run | Yes    | **One-time** setup token, consumed on first login — never a reusable password |
| `DataProtection__KeysDirectory` | Recommended | No | Persistent directory holding the data-protection keyring — keeps sign-in + antiforgery state across redeploys (deploy/coolify/step-5/52-data-protection.md). Omit = in-memory (sessions die on restart) |
| `Media__RootPath` | Recommended | No | The dedicated media volume mount (content-addressed byte store, ADR 0011). The in-code default (`{appDir}/media`) sits **inside the image layer** — in prod this must point at an operator-provided attached volume, or uploads are lost on redeploy, same discipline as `DataProtection__KeysDirectory` |
| `Media__MaxBytes` | Optional | No | **Fallback** per-file cap in bytes (default `10485760` = 10 MiB; `0` = no cap). **ADR 0135 (M25):** the per-file limit is now primarily **admin-set in-app** (`GET/POST /admin/storage/settings`), and this env value is the **fallback** in force when the admin has not set an override — a resident over it is still rejected `413` before any byte is written. Under an admin override, its `0` = no-cap sentinel reads as *unlimited file size*. The **per-user total content quota** is **not** an env knob — it is admin-set in-app only (quota `0` = unlimited) |
| `Media__MaxPlatformBytes` | Optional | No | **Platform-wide** storage-space limit in bytes (default **absent/`0` = unlimited**). When set to a value `> 0`, the platform may hold at most this many bytes of resident content: the `GlobalAdmin`'s `/admin/storage` "available" figure is capped to `min(physical free space, limit − used)` (clamped at `0` once used ≥ limit), and **all** new file uploads are blocked with `413` the moment used space reaches **or** exceeds the limit — *or* the volume's physical free space drops below the fixed **100 MiB** floor, whichever comes first. This is the operator's capacity knob (an env value, **not** an admin-set in-app limit): a value of `0` or unset leaves both the "available" figure and the upload gate exactly as they are without the limit. The 100 MiB floor is a fixed safety margin (not a separate knob) that stops the volume filling to its physical edge |
| `Media__AllowedContentTypes` | Optional | No | Comma-sep Content-Type allowlist, case-insensitive (default `image/jpeg,image/png,image/webp,image/gif` — SVG deliberately excluded, SECURITY.md §3(e)). The image lane's raster-only gate; the extension point this row was sized to leave open for follow-on lanes |
| `Media__AttachmentAllowedContentTypes` | Optional | No | Comma-sep Content-Type allowlist, case-insensitive, for the **attachment (download) lane** (ADR 0034, lane `ATT`) — **distinct** from the image lane's `Media__AllowedContentTypes`. Default: `application/pdf, application/msword, application/vnd.openxmlformats-officedocument.wordprocessingml.document, application/vnd.ms-excel, application/vnd.openxmlformats-officedocument.spreadsheetml.sheet, text/plain, text/csv, application/zip, image/jpeg, image/png, image/webp, image/gif` (SVG excluded; the four raster types included so a photo can be attached *as a download*). Same `Media__MaxBytes` cap. Guards-before-write: empty → 400, oversize → 413, disallowed → 415 (SECURITY.md §3(e)) |
| `SampleData__Enabled` | Optional | No | First-boot mock-neighborhood seeder (ADR 0055 / 0056) **plus** the warm-boot sample-event translation backfill (ADR 0060). Default **absent/`false`** — a real deployment omits it, so neither lane is reachable by construction. Set `true` only for a **deployed demo site** (`Production` + fresh DB): the seed admin keeps its `SeedAdmin__` setup-token lane, the other demo accounts get random high-entropy passwords, and a single credentials summary is emailed to the seed admin's address through the durable outbox. In `Development` it instead uses the documented weak demo credentials (the [Running-locally](../dev/running-locally.md) table). The content seed is first-boot only (pristine-DB gate); on a warm boot the flag also triggers the create-if-missing, idempotent, never-clobbering backfill of the sample events' missing `de`/`fr`/`da` `EventTranslation` rows (ADR 0060) |
| `Health__Token` | Optional | Yes | Shared-secret for the full `/health` diagnostic payload (SECURITY.md §6 / M3). When set, the detailed fields (`mail`, `mailDetail`, `emailDeadLetters`, `build`) require either the `X-Health-Token` header to match this value, or an authenticated GlobalAdmin session. The minimal liveness probe (`status`, `database`, `app`, `elapsedMs`) stays anonymous (Coolify / edge-proxy health check relies on it). **Default absent/empty** — the full payload is always visible (back-compat). Recommended for production: `openssl rand -hex 24` |
| `AuditPurge__RoutineDays` | Optional | No | §6.4 scheduled-job config: routine access-audit rows are purged once older than this many days (default `90`). Omit to keep the default |
| `AuditPurge__UnresolvedReportDays` | Optional | No | §6.4 scheduled-job config: report-attached + standing audit rows are kept until the report resolves, then a further this many days (default `365`). Omit to keep the default |
| `EventReminder__WindowHours` | Optional | No | §6.4 scheduled-job config: the "remind the day before" window for event reminders, in hours (default `24`). Omit to keep the default |
| `ConnectionStrings__KumunitaMigrate` | F22 (ADR 0171) | Yes | The **migrate role** connection string — the role that owns the DB + `mt` / `identity` schemas and runs DDL at boot (EF migrations + Marten schema-apply + the grant step). When **unset**, the boot path degrades to the single-role baseline (a not-yet-migrated instance is unchanged). See [hardening checklist](hardening-checklist.md) "DB user" |
| `DbTls__SslMode` | F21 (SRV-04) | No | Postgres TLS posture for the app↔DB connection (`Prefer` default → byte-identical to baseline; `Require` / `Verify CA`). **`Verify CA` against a non-TLS server is a hard connection failure** — enable server-side TLS before flipping to it. (This is app↔DB TLS, distinct from the [TLS & domain](tls-domain.md) edge/domain TLS; see [design: F21 DB-TLS](../design/prod-hardening-f21-db-tls.md)) |
| `DbTls__CaPath` | F21 (SRV-04) | No | Path to the CA bundle for `DbTls__SslMode=Verify CA`. Must be re-provisioned on CA rotation or the handshake fails |
| `SENTRY_DSN` | F10 (OBS-02) | Yes | Sentry error-tracker DSN (F10). **OFF by default** — unset = a no-op, the pipeline is byte-identical to before the seam (no PII leaves the box, the PII-scrubbing filter applies only once set). Set it to enable capture on an instance |
| `VirusScan__Host` / `VirusScan__Port` | F16 (UPL-02) | No | Point the serve-lane scan at a local `clamd` daemon (`127.0.0.1` / `3310` defaults). **Fail-open by default** — a missing/errored daemon degrades to `Clean`, so until ClamAV is live on the volume the active control stays the `nosniff` + `attachment` serve mitigation. `VirusScan__MaxScanBytes` caps the bytes scanned per file (default 100 MiB) |
| `MailSuppression__IngestToken` | F17 (MAIL-03) | Yes | Shared secret for `POST /api/mail/suppress` — the provider's bounce/complaint webhook sends it as the `X-Suppression-Token` header. A missing/mismatched token → 403. Until the webhook is pointed at this route the suppression list stays empty and the mail-out gate is a no-op |
| `KUMUNITA__SIGNUP__BOT_CHECK` / `...__SITE_KEY` | F9 (AUTH-15) | No / Yes | The Turnstile bot-challenge seam (see [hardening checklist](hardening-checklist.md) CAPTCHA row). **OFF by default** — the `SITE_KEY` is the owner's key; until it is set a flag-on instance **fails closed** (a bot is not proven human) |

Connection string example:
`Host=db;Port=5432;Database=kumunita;Username=kumunita;Password=____;Include Error Detail=true`

**Config is state too.** The env set defines an instance's identity — back it up (encrypted)
alongside the database ([backups](backups.md)), or a rebuilt VPS won't know who it is.

**Storage metrics (M24, ADR 0134).** A `GlobalAdmin` can read the media byte
store at **`GET /admin/storage`** — total used space, available space,
user-content used space, and a paged per-user table. It adds **no env var** and
**no new surface to provision**: it reads `Media__RootPath` (the configured
volume) via two read-only `statvfs`/`DriveInfo` calls and the `MediaObject`
catalog (`Σ SizeBytes` per `CreatedById`). It is a **read** — a page view emits
**no `AccessAudit` row** (the M13 "read = no row" discipline; there is no
`/admin/storage/export` route in v1), so there is nothing to audit, back up, or
rotate for it. Non-`GlobalAdmin`s get **403**.

**Sorting (M26, ADR 0145).** A paged list/search surface's order is a
**per-request query param** (`?sort=<key>` + `?dir=asc|desc`, parsed against
the surface's closed allowlist) — **not** an operator config knob. It adds
**no env var** and **no new surface to provision**: the `?sort=`/`?dir=`
pair is read Web-only from the request, resolved to a pure `SortSpec`,
threaded into the paged seam, and carried on the pager links (the ADR 0090
D7 `FilterParams` mechanism). An absent/unknown pair preserves the surface's
current order (C-SORT·2). There is nothing to audit, back up, or rotate
for it — a sorted read emits **no `AccessAudit` row** (C-SORT·4).

## Instance inventory

Keep one row per neighborhood. This is your map.

| Neighborhood | Domain | VPS | DB name | Version (Commit) | Admin contact | Created (Y/M/D) | Last backup verified (Y/M/D) | Last drilled (rollback) | Notes |
|--------------|--------|-----|---------|---------------|---------------|---------|----------------------|------------------------|-------|
|Examplium Residents|example.kumunita.com|Hetzner|kumunita|f8b8432ce7b5b2a6c4eaca26fd363029dde104f0|admin@examplium.com|2026-09-01|2026-09-01 15:20|_not yet drilled (open item — F20.U3)_|First instance|

## Related

- [Environments](environments.md) — the dev/prod table this reference describes per instance.
- [Hardening checklist](hardening-checklist.md) — the "DB user" and CAPTCHA rows that reference this table.
- [ADR 0171](../adr/0171-least-privilege-db-role.md) — the two-role split behind `ConnectionStrings__KumunitaMigrate`.
