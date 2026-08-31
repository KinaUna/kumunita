# Kumunita — Operations Runbook

Practical, tested procedures for running a Kumunita instance. Companion to
`ARCHITECTURE.md` (what it is) and `docs/adr/0002` (why one-instance-per-neighborhood).

**Two roles, don't confuse them:**
- **Host operator** — owns the VPS, Coolify, backups, upgrades, TLS, SMTP. (This runbook.)
- **Community GlobalAdmin** — owns in-app roles, moderation, content. (In-product.)

**Rules of the road:** every procedure should be *tested before it's relied on*. Backups
you haven't restored are not backups. Update the "Last tested" date when you run a step.

## Environments

| Env  | Where            | DB          | Email     | Purpose                 |
|------|------------------|-------------|-----------|-------------------------|
| dev  | local (compose)  | postgres:18 | Mailpit   | development, tests      |
| prod | VPS via Coolify  | per instance| real SMTP | a live neighborhood     |

No shared "staging cluster" — a throwaway prod-style instance is enough to verify an upgrade.

## Configuration reference

All per-instance identity and integration is env. The *image* is identical everywhere.

| Variable                    | Req       | Secret | Description                                    |
|-----------------------------|-----------|--------|------------------------------------------------|
| `ASPNETCORE_ENVIRONMENT`    | Yes       | No     | `Production`                                   |
| `Community__Name`           | Yes       | No     | Display name shown to residents                |
| `Community__SupportEmail`   | Yes       | No     | Contact shown in footer / reports              |
| `ConnectionStrings__Kumunita` | Yes      | Yes    | Postgres connection string                     |
| `SMTP__Host`                | Yes       | No     | SMTP server                                    |
| `SMTP__Port`                | Yes       | No     | 587 (STARTTLS) or 465 (implicit TLS)           |
| `SMTP__Secure`              | Optional  | No     | `Ssl` \| `Tls` \| `None`                       |
| `SMTP__User` / `SMTP__Pass` | If auth   | — / Yes| SMTP credentials                               |
| `SeedAdmin__Email`          | First-run | No     | Initial GlobalAdmin address                    |
| `SeedAdmin__Token`          | First-run | Yes    | **One-time** setup token, consumed on first login — never a reusable password |

Connection string example:
`Host=db;Port=5432;Database=kumunita;Username=kumunita;Password=____;Include Error Detail=true`

**Config is state too.** The env set defines an instance's identity — back it up (encrypted)
alongside the database, or a rebuilt VPS won't know who it is.

## Instance inventory

Keep one row per neighborhood. This is your map.

| Neighborhood | Domain | VPS | DB name | Image version | Admin contact | Created | Last backup verified | Notes |
|--------------|--------|-----|---------|---------------|---------------|---------|----------------------|-------|
|              |        |     |         |               |               |         |                      |       |

---

## Procedures

### 1. Provision a new neighborhood

1. Reserve a domain/subdomain (e.g. `maplewood.example` or `maplewood.kumunita.example`).
2. Point DNS (A or CNAME) at the VPS.
3. In Coolify, create a **new app** from the Kumunita image with its **own** env set:
   `Community__Name`, `Community__SupportEmail`, a **dedicated Postgres**, SMTP, and the
   first-run admin fields.
4. Generate a strong DB password; store it in your secrets manager (not in the runbook).
5. Deploy.
6. Run **Procedure 2** (first boot).
7. Record the instance in the inventory.

### 2. First boot & admin setup

What happens automatically on first run: Marten applies the schema → Identity schema is
created → the GlobalAdmin is created for `SeedAdmin__Email` with `SeedAdmin__Token` as
a **one-time setup credential** (invalidated on first use) → a verification email is
sent → default components (Safety, Maintenance, Social, Governance) are seeded → the
language catalog is seeded (source language `en` enabled and set as default).
Re-running the seeder is a no-op once the admin exists.

**Languages are in-app data, not config.** Supported languages, the default language,
and all translations live in the DB (`mt` schema; admin-managed under
`/admin/languages`, ADR 0005) — not env, not image config. They ride the existing
DB backup; nothing extra to provision.

Operator steps:
1. Generate a strong one-time token (e.g. `openssl rand -hex 24`); store it in the
   secrets manager, then set `SeedAdmin__Email` + `SeedAdmin__Token` in Coolify.
2. Deploy; open `https://<domain>`; confirm it renders with the correct `Community__Name`.
3. Log in as the seeded admin **using the token** — first login is "set your password";
   the token is invalidated by the app on this first use.
4. **Remove `SeedAdmin__*` from env** in Coolify (nothing reusable remains to leak).
5. Verify: `/health` returns OK, a test email arrives (check the provider, not just "sent"),
   and the default components appear.
6. Update the inventory (image version, admin contact).

Why a token, not a password: env is a config store — visible to ops tooling, shell
history, and container env dumps. A one-time token in env is acceptable; a long-lived
admin password in env is not. The app invalidating it on first use removes the
change-then-remove race entirely.

### 3. Upgrade the application

1. Announce a maintenance window to residents.
2. **Take a fresh backup** (Procedure 4) and note the current image tag.
3. In Coolify, upgrade the app to the new image tag and redeploy.
4. On boot, pending **versioned migrations** are applied — Marten steps (tracked in
   `mt.migrations`) and Identity migrations (`identity.__EFMigrationsHistory`); check the
   logs confirm each step ran. Migrations are forward-only and live in the image, so the
   new image must always carry a superset of the running schema's steps (ADR 0004).
5. Smoke test: login, create a post, open a component, check `/health`.
6. **Rollback (if needed):** redeploy the previous image tag. App rollback is easy; **DB
   rollback requires a restore** (Procedure 5) because schema changes are forward-only.
7. Record the new version + date in the inventory.

### 4. Backups

**What:** the Postgres DB is the crown jewels (all Marten documents + Identity). Back it up.

- **Format:** `pg_dump -Fc` (custom format — fast restore, compression).
- **Schedule:** at least daily. Example host cron:
  `0 3 * * * pg_dump -Fc -h 127.0.0.1 -U kumunita kumunita | rclone rcat backup:kumunita/<instance>/$(date +\%F).dump`
- **Offsite:** to object storage (S3 / Backblaze B2 / rclone) — **not** only on the same VPS.
- **Retention:** e.g. 7 daily, 4 weekly, 6 monthly (tune to need).
- **Config:** include the instance's env set (encrypted) in the same backup set.
- **Verify:** restore a recent dump to a scratch DB at least quarterly. *A backup you can't
  restore is not a backup.* Update "Last backup verified" in the inventory.

### 5. Restore

**Same instance (corruption / bad data):**
1. Stop the app (Coolify).
2. Restore: `pg_restore -h 127.0.0.1 -U kumunita -d kumunita --clean < dump.dump`
3. Start the app; verify `/health`, login, and a representative page.

**Disaster recovery (new VPS):**
1. Provision a fresh instance (Procedure 1) with the **same** `Community__Name` and config.
2. Restore the DB (and re-apply the backed-up env set) into it.
3. **Version compatibility:** restore a dump into a compatible app version — check the image
   tag the backup was taken under before proceeding.
4. Re-point DNS; verify email + TLS + `/health`.

### 6. TLS & domain

- Coolify + Let's Encrypt auto-issues and renews. Operator needs only: DNS pointing, ports
  80/443 reachable, and an occasional cert check.
- Verify renewal with `openssl s_client -connect <domain>:443 -servername <domain> 2>/dev/null | openssl x509 -noout -enddate`.
- If a domain changes: update DNS, wait for propagation, reissue the cert, confirm.

### 7. Email (SMTP)

- **Production:** a transactional provider (Resend / Postmark / SES / Mailgun) or the
  community's own mail server. Choose one and record it per instance.
- **Deliverability (matters for account verification):** on the sending domain, set
  **SPF, DKIM, and DMARC**, and use a stable `From:` address (e.g. `no-reply@<domain>`).
  Without these, verification/reminder emails land in spam and look broken.
- **Dev:** Mailpit — open its web inbox to inspect what would be sent.
- **Reliability (see ARCHITECTURE.md §6.2):** all email flows through one durable
  handler; a transient SMTP failure never blocks a domain write or an audit row.
  After **6 attempts over ~24 h** the message is dead-lettered to
  `mt.email_dead_letters` and `/health` reports **degraded**.
- **Dead-letter runbook:** check `mt.email_dead_letters` (idempotency key, recipient,
  last error, attempts). Once SMTP is healthy, re-queue the row (reset the key to a
  fresh outbox message) or discard. **Verification emails are load-bearing:** if SMTP
  is down for a long stretch, a new resident is stuck unverified — a **GlobalAdmin
  can verify a resident manually** (in-app, audited) as the safety valve.

### 8. Monitoring & health

- **Liveness:** `/health` endpoint (Coolify restarts on failure). Also reports
  **degraded** (not failed) when `mt.email_dead_letters` is non-empty — the app is up
  but email is not going out; act per §7 dead-letter runbook.
- **External uptime:** a cheap checker on the public URL → alert on failure (Uptime Kuma or
  similar). This is your "is it even up" signal.
- **Logs:** structured logs to stdout; Coolify captures. Grep for `error`/`unhandled` after
  incidents.
- **Capacity:** watch disk (backups grow) and Postgres size; alert on thresholds.
- **Review:** the access audit log (GlobalAdmin, in-app) periodically — see Procedure 9.

### 9. Data & access operations (privacy)

Most of this is an **in-app GlobalAdmin** action; a few need the operator at the DB.

- **View who accessed private content** → GlobalAdmin opens the audit log (always-on by
  design; see ADR 0003).
- **Audit retention:** `AccessAudit` is purged on a schedule (see `ARCHITECTURE.md` §5):
  routine restricted-content Allow/Deny rows after ~90 days; report- and
  moderator/admin-access rows kept until the report resolves (+90 days). The cutoff is
  per-instance config. The purge job writes an `AuditPurge` summary row (count, cutoff,
  at) — spot-check after a run, and note that a deleted account's rows are
  **pseudonymized, not purged** (below).
- **Suspend a resident** (abuse) → GlobalAdmin disables the account in-app (immediate).
- **Revoke a moderator** → GlobalAdmin removes the role / component scope in-app.
- **Resident requests data export** → self-serve in-app once the export service ships
  (deferred). **Until then (manual):** operator extracts that user's records to a file and
  delivers it securely; log the action.
- **Resident requests account deletion** →
  1. Disable the account in-app immediately.
  2. Operator hard-deletes the user's personal data (profile, groups they own, grants).
  3. **Audit retention nuance:** keep `AccessAudit`/`Report` rows but **pseudonymize** the
     subject (replace the user id with a tombstone) rather than deleting — you may need to
     prove what happened without retaining personal data. Confirm the policy before acting.
- **Hand over admin** → promote a resident to GlobalAdmin, then demote the outgoing one.
  **Two GlobalAdmins should be the standing state** (each can demote the other), so a
  single departure is never a lockout.
- **Break-glass elevation** (admin gone, locked out, or hostile) → **operator-only, DB
  level**, since the app trusts its own admins:
  1. Choose the account to elevate (an existing verified resident, ideally the backup
     admin). Note: a hostile admin cannot do this *to themselves* — no in-app endpoint
     creates an `AdminOverride` (ARCHITECTURE.md §4.5).
  2. Insert directly into Postgres (as the DB operator user, not the app user):
     `INSERT INTO mt."AdminOverride" (id, userId, token, grantedAt, expiresAt)
      VALUES (gen_random_uuid(), '<target-user-id>', '<strong-one-time-token>',
              now(), now() + interval '6 hours');`
  3. Tell the target: log in, go to `/admin/break-glass`, enter the token. Consumption
     elevates them to GlobalAdmin until `expiresAt`; the app appends
     `AccessAudit (via: BreakGlass)` rows for the elevation and every subsequent
     privileged action. **Before or as part of elevation, ensure the account has 2FA**
     (GlobalAdmins must, per §10) — an elevated account that lacks 2FA is a standing
     admin that does not meet the admin bar.
  4. When done, verify the override has expired (or delete the row) and review the
     `BreakGlass` audit rows. **Record the reason** in the incident log (§11) — the DB
     write itself is invisible to the app's audit trail.

### 10. Security hardening checklist

Security and privacy are the product's top priority — the threat model, data classes,
and the full control map live in `docs/SECURITY.md`. This checklist is the operational
slice of that map.

- **Network:** expose only 80/443 (and SSH, key-only). **Postgres must not be public** —
  bind to the internal/localhost or Coolify network.
- **SSH:** key-based only; consider fail2ban; disable password login.
- **Secrets:** never baked into the image. Env / secrets manager only. Rotate SMTP, DB, and
  admin credentials on a schedule and after any suspected exposure.
- **DB user:** dedicated, non-superuser for normal operation. Note: Marten needs to create
  extensions on **first** run — the setup user may need elevated rights initially, then
  drop to least privilege.
- **App:** HTTPS-only + HSTS, secure cookies, anti-forgery tokens, rate limiting (all
  planned in the app):
  - **Cookies:** `Secure`, `HttpOnly`, `SameSite=Lax`; sliding expiry with an absolute
    session cap.
  - **Privilege-change invalidation:** issuing/revoking a delegation grant or a
    role/moderator change rotates the user's **security stamp** (ASP.NET Identity
    support), which invalidates existing sessions and forces re-authentication — a
    delegated person never keeps stale access after the grant is revoked.
  - **Rate limiting (decided):** ASP.NET Core built-in middleware (`AddRateLimiter`),
    per-endpoint policies, in-memory partitioning by client IP (+ authenticated user
    where relevant). Covered endpoints: register, login (failed attempts), **and
    report-filing** — report filing is an authorization-escalation surface (it grants
    the assigned moderator access), so it is not just a spam concern.
    **Proxy requirement:** the reverse proxy must forward `X-Forwarded-For` and the app
    must honor it (trusted-proxy config); otherwise every request shares one bucket.
    Verify in the M0/M1 smoke test. Rationale & revisit triggers: SECURITY.md §6.
  - **Account lockout (decided):** ASP.NET Identity's built-in per-account lockout,
    enabled with the default thresholds (5 failed attempts → 30-minute lockout).
    **Division of labor:** lockout stops one attacker hammering *one known account*;
    per-IP rate limiting stops an attacker *sweeping many accounts* (each below the
    lockout threshold). Do not disable one because the other "covers" it — they cover
    different attack shapes.
  - **2FA (decided):** TOTP (ASP.NET Identity built-in) + recovery codes — **required
    for GlobalAdmin**, recommended for Moderator, not offered for Members. A
    break-glass-elevated account must have 2FA before it keeps the standing (§9 step 3).
  - **Content-Security-Policy (decided):** `default-src 'self'`,
    `style-src 'self' 'unsafe-inline'`; **no inline `on*` attributes or inline
    `<script>` in Razor views** (use the `client/*.ts` modules). The header is a
    one-line change — the cost of relaxing it later is hunting down inline handlers,
    so the code discipline is what's pinned. Any future third-party script: scoped
    `script-src` entry + SRI, decided per case (SECURITY.md §6).
- **CAPTCHA — deferred by default (decision):** signup is email-verification-gated and
  rate-limited, so a bot that can't verify the email can't join — no CAPTCHA is needed
  today. Revisit and add one (e.g. Turnstile, self-hosted or cloud, per-instance site
  key) on **signup** and **failed login** (after N failures) if either: (a) abuse is
  observed, or (b) several instances share a VPS/public IP — a bot probing one
  neighborhood can burn the shared IP's reputation, and a per-instance key also avoids
  one third party seeing all instances' traffic.
- **Supply chain:** pin the base image + package versions; rebuild on base-image updates.

### 11. Incident response (basic)

1. **Detect** — uptime alert, resident report, or logs.
2. **Triage** — is it *down* or is *data at risk*? Data risk takes priority.
3. **Contain** — stop the app; block the offending user; **rotate credentials** if
   compromise is suspected.
4. **Recover** — restore from backup (Procedure 5) if data is affected.
5. **Communicate** — if residents' data was exposed, tell them (a privacy duty, not optional).
6. **Record** — append to the incident log below.

**Incident log**

| Date | What happened | Impact | Action taken | Follow-up | Who |
|------|---------------|--------|--------------|-----------|-----|
|      |               |        |              |           |     |

---

## Conventions

- Keep steps numbered and imperative; one actor per step.
- Stamp each procedure with **Last tested: <date>** and refresh when you run it.
- When a procedure changes, update it *and* the affected inventory/checklist in the same edit.
- Secrets live in the secrets manager, never in this file. Reference by name (`SMTP__Pass`),
  not by value.
