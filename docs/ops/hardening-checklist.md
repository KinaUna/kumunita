---
id: ops-hardening-checklist
title: "Security hardening checklist"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/SECURITY.md
  - docs/SECURITY-AUDIT.md
  - docs/ops/configuration-reference.md
  - docs/adr/0171-least-privilege-db-role.md
updated: 2026-11-02
---

> **Abstract:** the operational slice of the security control map — network,
> SSH, secrets, the two-role DB split, app-level hardening (cookies, rate
> limiting, 2FA, CSP), and the deferred CAPTCHA. The *verification* cadence
> and checklists C/D/P/R live in
> [`SECURITY-AUDIT.md`](../SECURITY-AUDIT.md). **Who is it for:** the host
> operator. **When to read the rest:** before first prod deployment, or
> after any suspected exposure.

# Security hardening checklist

Security and privacy are the product's top priority — the threat model, data
classes, and the full control map live in [`SECURITY.md`](../SECURITY.md).
This checklist is the operational
slice of that map. The *verification* of these controls — cadence, owners,
and the run-anywhere checklists (including deployment checks D-1…D-8) — lives
in [`SECURITY-AUDIT.md`](../SECURITY-AUDIT.md); this page says *what* must
be true, that document says
*when and by whom we check it, and where we record that we did*.

- **Network:** expose only 80/443 (and SSH, key-only). **Postgres must not be public** —
  bind to the internal/localhost or Coolify network.
- **SSH:** key-based only; consider fail2ban; disable password login.
- **Secrets:** never baked into the image. Env / secrets manager only. Rotate SMTP, DB, and
  admin credentials on a schedule and after any suspected exposure.
- **DB user (two-role split, ADR [0171](../adr/0171-least-privilege-db-role.md) / F22):** the database is owned by a dedicated
  **non-superuser migration role `kumunita_migrate`** (`CREATE DATABASE kumunita OWNER
  kumunita_migrate`); `kumunita_migrate` owns the `mt` + `identity` schemas and is the
  only role that may run DDL (schema/table creation, `CREATE EXTENSION`). The runtime **
  app role `kumunita` is demoted to DML** (CONNECT / USAGE / SELECT / INSERT / UPDATE /
  DELETE) plus one **forced exception** — schema-level `CREATE` on `mt` only, because
  Wolverine + Marten create their `mt.wolverine_*` / `mt.mt_*` tables **as the app role**
  during `app.StartAsync()`; it is refused `CREATE SCHEMA` / `DROP SCHEMA` /
  `DROP DATABASE` (SQLSTATE `42501`, the `AppRole_CannotDropSchema` witness). The boot
  path reads `ConnectionStrings:KumunitaMigrate` (the migrate role) and falls back to
  the single-role baseline when it is unset (a not-yet-migrated instance is unchanged).
  The `postgres` superuser is admin/restore-only and is **never** the app connection.
  (Supersedes the old single-owner bullet `CREATE DATABASE kumunita OWNER kumunita`.)
  See [ADR 0171](../adr/0171-least-privilege-db-role.md).
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
    where relevant). Covered endpoints (each a named policy in `Program.cs`):
    **signup, resend, login (failed attempts), report-filing, setup (first-boot
    token), message, write (post/reply), and the four upload lanes** (`upload`
    policy, F8 / WEB-10 — `/attachment`, `/content-image`, `/documents`,
    `/profile/avatar`; `IUploadGate` is the size/quota guard, this is the *rate*
    guard). Report filing is an authorization-escalation surface (it grants the
    assigned moderator access), so it is not just a spam concern; the upload lanes
    write a volume byte each and are the byte-flood surface.
    **Proxy requirement:** the reverse proxy must forward `X-Forwarded-For` and the app
    must honor it (trusted-proxy config); otherwise every request shares one bucket.
    Verify in the M0/M1 smoke test. Rationale & revisit triggers:
    [the rate-limiting & proxy-trust decisions](../security/decisions-open-items.md#decisions).
  - **Account lockout (decided):** ASP.NET Identity's built-in per-account lockout,
    enabled with the default thresholds (5 failed attempts → 30-minute lockout).
    **Division of labor:** lockout stops one attacker hammering *one known account*;
    per-IP rate limiting stops an attacker *sweeping many accounts* (each below the
    lockout threshold). Do not disable one because the other "covers" it — they cover
    different attack shapes.
  - **2FA (decided):** TOTP (ASP.NET Identity built-in) + recovery codes — **required
    for GlobalAdmin**, recommended for Moderator, not offered for Members. A
    break-glass-elevated account must have 2FA before it keeps the standing
    ([privacy operations](privacy-operations.md) step 3).
  - **Content-Security-Policy (shipped, strict — L1 + strict flip):** the header is
    set in code (`Program.cs`, before `UseRouting`) so every response in every
    environment carries it — it no longer depends on a Caddy edge that may be
    misconfigured or absent. The directive set enforces the full strict rule from
    the original 2026-08-27 decision: `script-src 'self'` (no inline script, no
    `unsafe-inline` — every interactive behavior lives in `client/lib/*.ts`
    modules, self-wiring ES modules loaded from `_Layout.cshtml`), no external
    resource origin (`default-src 'self'`), no external form/connector target
    (`form-action`/`connect-src 'self'`), no `<base>` injection (`base-uri
    'self'`), clickjacking (`frame-ancestors 'self'`), `object-src 'none'`, and
    the inline style the views rely on (`style-src 'self' 'unsafe-inline'` —
    inline `style=` attributes are Bootstrap utility patterns, not a script
    vector). `img-src` adds `data:`/`blob:` (the WYSIWYG local-preview pane).
    Any future third-party script: scoped `script-src` entry + SRI, decided per
    case ([the CSP decision](../security/decisions-open-items.md#decisions)). **Two more global headers ride the same block (F7 /
    WEB-07):** `X-Content-Type-Options: nosniff` (every response — the global
    floor under the per-controller `nosniff` on the serve/download lanes) and
    `Referrer-Policy: no-referrer` (no cross-origin referrer leakage). Set in
    code, so every environment carries them; verify with `curl -sI <domain>`.
- **CAPTCHA — deferred by default (decision):** signup is email-verification-gated and
  rate-limited, so a bot that can't verify the email can't join — no CAPTCHA is needed
  today. Revisit and add one (e.g. Turnstile, self-hosted or cloud, per-instance site
  key) on **signup** and **failed login** (after N failures) if either: (a) abuse is
  observed, or (b) several instances share a VPS/public IP — a bot probing one
  neighborhood can burn the shared IP's reputation, and a per-instance key also avoids
  one third party seeing all instances' traffic. **The seam is in place** (F9, AUTH-15,
  [design: F9 signup](../design/prod-hardening-f9-signup.md) §4): set `KUMUNITA__SIGNUP__BOT_CHECK=true`
  + `KUMUNITA__SIGNUP__BOT_CHECK__SITE_KEY=<key>` and rebuild. The deferral
  conditions (a) abuse observed / (b) shared VPS still apply — the seam does not force
  the challenge on.
- **Supply chain:** pin the base image + package versions; rebuild on base-image updates.

## Related

- [`SECURITY.md`](../SECURITY.md) — the threat model, data classes, and full control map this checklist operationalizes.
- [`SECURITY-AUDIT.md`](../SECURITY-AUDIT.md) — the cadence and checklists (C/D/P/R) that verify this checklist.
- [Configuration reference](configuration-reference.md) — the env fields the hardening rows depend on.
- [Owner-ops ledger](owner-ops-ledger.md) — the single record of the operator steps behind these rows, each with its "last verified" cell.
