---
id: deploy-coolify-60-verify
title: "Coolify — 6: Verify"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/COOLIFY.md
  - docs/ops/first-boot.md
  - docs/ops/monitoring.md
  - docs/ops/backups.md
updated: 2026-11-02
---

> **Abstract:** the verify checklist after first deploy — HTTPS + HSTS, `/health`, the first-boot signal, the one-time setup token (and why a 404 on `/admin/setup` is success), the verification link, the inventory record, and the hardening pins. **Who is it for:** the host operator. **When to read the rest:** right after the first deploy, before handover to the runbook.

# 6 — Verify (then switch to the runbook)
1. `https://<domain>/` renders with the correct `Community__Name`, over
HTTPS. **Also check the forwarded-headers + cookie policy** (SEC H3 — the
operator is the only one who can prove this, as it depends on live proxy
config; none of the three checks below is covered by a unit test):
   - a. `curl -sI https://<domain>/` → response must include
     `Strict-Transport-Security: max-age=…` (HSTS) **and** the session
     cookie (`Set-Cookie: kumunita.auth=…; Secure`) must carry the `Secure`
     attribute. If `Secure` is missing the proxy is not forwarding
     `X-Forwarded-Proto: https` to the app, or the non-dev `CookieSecurePolicy.Always`
     block in `Program.cs` is not active.
   - b. From two different source IPs (e.g. two devices, or a VPN vs. a direct
     connection), submit a login attempt with the same wrong password 5+
     times in rapid succession from *each*. If the second IP still accepts
     attempts after the first is locked out, the rate-limit partitioning is
     working (real client IPs are distinct). If both IPs lock out
     simultaneously, the app sees a single shared IP — the proxy is not
     forwarding `X-Forwarded-For`, or `ReverseProxy:KnownNetworks` is unset
     and the proxy's source IP is not in the loopback+RFC1918 defaults.
   - c. If (a) or (b) fails: set `ReverseProxy:KnownNetworks` to the CIDR of
     the proxy's source address (e.g. `172.18.0.0/16` for the Docker network)
     in the app's env or `appsettings.Production.json`, and redeploy.
2. `GET /health` → `{"status":"ok","database":"ok","build":"<sha>",…}` and
`build` matches the deployed commit (`release` tip).
3. **First boot** (fresh database): the `mt` schema initializes with no
operator step (ADR [0004](../../adr/0004-data-persistence-and-schema-evolution.md)) — the log shows **First boot** exactly once;
subsequent boots are a no-op.
4. Seeded admin exists; log in **once** with the setup token → set password;
the token invalidates on use. Then **remove `SeedAdmin__*` from env**
(nothing reusable remains to leak — the [first-boot runbook](../../ops/first-boot.md), step 4).

   **How to tell the setup POST worked** — the successful POST signs the
   operator in and redirects to `/profile/edit`; the token is consumed and
   the lane is **permanently closed**. Every *subsequent* GET or POST to
   `/admin/setup` returns **404 by design** (the controller's `NotFound()`
   is the closed-lane signal, uniform for "already used", "wrong email",
   and "wrong token" so an anonymous caller cannot distinguish them — see
   `AdminSetupController.cs`). So:

   - A **404 on re-submit is success, not failure.** If you see
     "not found" after submitting, first verify the account actually has a
     password: `SELECT "Email" FROM identity."AspNetUsers"` (run after
     `\c kumunita`) and try to sign in at `/Account/Login` with the
     password you just set. If that works, the setup is done — do not
     re-submit the form, and do not try to "re-open" the lane.
   - **Gate the submit on `/health` = 200.** The app needs ~8 s to finish
     booting (Marten schema + Wolverine leadership); Coolify's health check
     will restart the container if it sees a non-200 at the moment of your
     POST, which kills the in-flight response *after* the DB transaction
     has already committed. The password is set, the token is consumed,
     but the browser never gets the redirect and instead sees the next
     boot's 404. Wait for `curl -fsS https://<domain>/health` to return
     `200` before opening `/admin/setup` and submitting the form.
   - If you are *genuinely* stuck (no password set, token still unconsumed,
     and `/admin/setup` 404s on a *first* attempt), the token is either
     expired or the email you typed doesn't match the seeded one — probe
     `mt.mt_doc_identitytoken` (run after `\c kumunita`):
     `SELECT data->>'ConsumedAt', data->>'ExpiresAt', data->>'UserId'
      FROM mt.mt_doc_identitytoken WHERE data->>'Kind' = 'setup';`
     A non-null `ConsumedAt` means it was already used (sign in instead);
     a past `ExpiresAt` means the TTL (`Verification__TtlDays`, default
     14 days from first boot) elapsed — re-seed a fresh token per the
     [first-boot runbook](../../ops/first-boot.md).
5. `/health` reports `ok` (email dead-letter count 0); a test email arrives
at the destination inbox (the [first-boot runbook](../../ops/first-boot.md) step 5).
6. **Confirm the verification link is absolute** (requires `Verification__BaseUrl`,
[§5](step-5/00-overview.md)/[§5.3](step-5/53-domains.md)): the first-boot *setup* email carries no link — its proof is the
setup token + a `/admin/setup` path. The resident *verification* link only
appears in a **signup** email (`RegisterAsync`, `VerificationBody`). So to
check it: create a throwaway resident account, read the email's link, and
confirm it is `https://<domain>/account/verify?id=…` — an absolute URL to
**this** instance's domain. A bare `/account/verify?id=…` (relative) means
`Verification__BaseUrl` is unset or wrong. Delete the throwaway account
after the check.
7. Record the instance in the **OPS inventory** — the [configuration reference](../../ops/configuration-reference.md) (domain, VPS, DB name, commit
SHA, admin contact, dates).
8. Backups enabled / scheduled per the [backups runbook](../../ops/backups.md).
9. **Host + security-header hardening** (SEC U26, L6/L7) — the in-repo defaults
keep local dev working, so the operator must pin the production values in the
app's env or `appsettings.Production.json`:
   - **`AllowedHosts`** — the in-repo `appsettings.json` ships `"*"` (dev only,
     so local host/port iteration keeps working). For production, set it to the
     community's public host (e.g. `maplewood.example.com`), comma-separated if
     there is a secondary name. Leave it `*` only for local dev.
   - **`WellKnown__BaseUrl`** *(optional)* — the instance's public base URL
     (no trailing slash, e.g. `https://maplewood.example.com`). When set, the
     RFC 9116 `security.txt` `Policy:` field is built from this instead of the
     incoming `Request.Host`, so a spoofed `Host` header (S-3) can't corrupt it.
     Unset → falls back to the request origin (the previous behaviour).
   - **`Strict-Transport-Security`** is now emitted with `max-age=31536000`
     (one year; `includeSubDomains` is deliberately **off**). Verify with
     `curl -sI https://<domain>/` (step 1a already asserts the HSTS header is
     present — confirm the `max-age` value is `31536000`).
   - **`Permissions-Policy`** is now emitted on every response with a
     restrictive default (camera, microphone, geolocation, payment, usb,
     picture-in-picture, web-share all disabled). `fullscreen` is opted in as
     `(self)` because the kiosk, events calendar, and projects board use the
     Fullscreen API. No env value needed; if a future feature legitimately
     needs one, opt it in here.
