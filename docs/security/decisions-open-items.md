---
id: security-decisions-open-items
title: "Decisions & open items"
tier: reference
kind: security
status: current
audiences: [community-admin, platform-maintainer, developer, agent]
related:
  - docs/SECURITY.md
  - docs/security/control-map.md
  - docs/adr/0003-roles-and-moderator-scoping.md
  - docs/adr/0005-multilingual-support.md
  - docs/adr/0018-ugc-authored-in-language-tag.md
  - docs/adr/0050-admin-managed-signup-gate-open-or-invitation-only.md
  - docs/adr/0172-password-hash-posture-pbkdf2-identity-default.md
updated: 2026-11-07
---

> **Abstract:** the *decided* controls — supply-chain pinning, rate limiting,
> proxy trust, 2FA, CSP, password storage, UGC translation — each with its date
> and the adversary it protects — plus the open items that are still
> undecided (log hygiene, the invitation mechanism). **Who is it for:** the
> developer changing a security surface and the auditor checking a decision
> still holds. **When to read the rest:** before touching rate limiting, 2FA,
> CSP, proxy trust, or a third-party boundary; before deciding an open item.

## 6. Decisions & open items

### Decisions

<a id="decisions"></a>

**Supply-chain pinning (SEC L5, decided 2026-10-31; protects A6):** what is
pinned and what is intentionally left floating.

- **Pinned (by commit / digest, not just tag):**
  - **CI actions** (`.github/workflows/ci.yml`): `actions/checkout@fbc6f39…b6c09`
    (`# v5`), `actions/setup-dotnet@26b0ec1…c95bf1` (`# v5`),
    `actions/setup-node@a0853c2…a1444` (`# v5`). SHA = the tag's resolved commit
    (GitHub API, 2026-10-31).
  - **Container base images** (`Dockerfile`): `mcr.microsoft.com/dotnet/sdk:10.0`
    and `mcr.microsoft.com/dotnet/aspnet:10.0` are SHA-pinned to their
    multi-arch index digests (MCR registry, 2026-10-31).
  - **NuGet + npm packages:** already version-pinned via `PackageReference`
    / `package-lock.json`; `dotnet list package --vulnerable` + `npm audit` are
    clean (U3 baseline).
- **Intentionally not pinned ("to pin" list):**
  - `node:22` (Dockerfile stage 1, the TS build) — Docker Hub blocks anonymous
    digest resolution, so it stays a floating tag. **To pin:** resolve via a
    token-authenticated pull or `docker pull node:22 && docker inspect` and add
    the digest.
  - `postgres:18`, `axllent/mailpit:latest` (dev `docker-compose.yml`) — dev-only,
    not the deploy path, so left floating. **To pin:** same approach if a dev box
    needs reproducible builds.
  - **Workflow `permissions:`** is now scoped to `contents: read` (least
    privilege — the workflow only reads the checked-out source).
- **Non-root container runtime:** the app's *running process* is `app` (UID 1654,
  the user the MCR aspnet base ships), achieved via `docker/entrypoint.sh`:
  the entrypoint starts as the image's default USER (root) just long enough to
  `chown -R app:app /app /data` (re-claiming any root-owned files in the mounted
  volumes), then drops to `app` via `setpriv --reuid app --regid app
  --clear-groups` and `exec`s `dotnet Kumunita.Web.dll`. The dotnet process
  is therefore non-root (PID 1, UID 1654) — the U25 invariant — but the
  entrypoint's root phase repairs the ownership of *any* stale pre-U25 volume
  files at every boot (the U25-era `RUN chown` only fixed the image layer, not
  a mounted named volume first populated when the app ran as root). `curl`/`wget`
  are kept (external health probe, no built-in substitute — see the U25 handoff
  note). The MCR base ships `setpriv` (util-linux) but not `gosu`/`su-exec`;
  `setpriv` is the setuid-free, PAM-free, env-preserving privilege drop.

**Rate limiting (mechanism, decided 2026-08-27; protects A2):**

- **Mechanism:** ASP.NET Core's built-in rate-limiting middleware
  (`AddRateLimiter`), with **per-endpoint policies** and **in-memory partitioning by
  client IP** (plus user identity where the actor is authenticated). At one instance
  per neighborhood with dozens of users, a process-local counter is the correct weight
  — no Postgres-backed or distributed rate limiter, no queue.
- **Covered endpoints (shipped):** `login` (5/15 min), `signup` (5/1 hr), `resend` (5/1 hr), `report-filing` (10/1 hr — an authorization-escalation surface), `setup` (5/15 min), `message` (20/15 min), `write` (30/15 min), and `upload` (10/15 min — F8, the 2026-10-25 fix pass, attached to the four upload POSTs: `/attachment`, `/content-image`, `/documents`, `/profile/avatar`). [`hardening-checklist`](../ops/hardening-checklist.md) is the canonical runbook list; the `upload` leg is pinned by `F7F8_HardeningGateTests`.
- **Real client IP is a hard requirement.** Behind the reverse proxy the app sees the
  proxy's address unless the proxy forwards `X-Forwarded-For` *and* the app honors it
  (trust the proxy, e.g. `ForwardedHeaders` / `UseForwardedHeaders` configured for the
  proxy only). **Without this, per-IP partitioning collapses into one shared bucket** —
  every user shares one limit and a single client can exhaust it for everyone. This is
  verified in the M0/M1 smoke test, not assumed.
- **Revisit trigger:** shared hosting of multiple neighborhoods on one VPS/IP (see
  the [deployment assumption](why-first.md)) — per-instance limits stop protecting the
  others, and the CAPTCHA decision ([`hardening-checklist`](../ops/hardening-checklist.md)) is revisited in that case.

**Proxy trust (SEC H3, decided 2026-10-31; protects A2 — the real-client-IP
requirement above):** the trusted-proxy list is **configuration-driven, never
trust-all.** [`ForwardedHeadersSetup`](../../src/Kumunita.Web/Security/ForwardedHeadersSetup.cs) (U9) reads `ReverseProxy:KnownNetworks`
(CIDR ranges) and `ReverseProxy:KnownProxies` (IPs) from the app's config;
when both are unset or empty it falls back to a safe default — loopback
(`127.0.0.0/8`, `::1/128`) and the RFC1918 ranges (`10.0.0.0/8`,
`172.16.0.0/12`, `192.168.0.0/16`), which covers the Docker network — and
**never** to "trust everyone." An out-of-range peer's `X-Forwarded-For` /
`X-Forwarded-Proto` headers are ignored, so a spoofed client cannot inject a
false source IP or scheme. An unparseable CIDR or IP value throws
`InvalidOperationException` at `builder.Services` time (the boot fails, the
door does not open). In production, set `ReverseProxy:KnownNetworks` to the
CIDR of the edge proxy's source address (e.g. `172.18.0.0/16`) so the
`X-Forwarded-For` header is honored and the `login` rate-limit partitioning
sees real client IPs. The cookie `Secure` attribute is set explicitly via
`CookieSecurePolicy.Always` outside Development (U10), so the session cookie
is never issued over plain HTTP even on a non-TLS hop.

**2FA (decided 2026-08-27; protects A3, A4):** TOTP via ASP.NET Identity's built-in
support, plus recovery codes.

- **GlobalAdmin: required.** An admin account is total instance compromise (roles,
  standing moderator visibility, audit log); 2FA is the control, not optional.
  Enforced server-side by [`AdminTwoFactorMiddleware`](../../src/Kumunita.Web/Security/AdminTwoFactorMiddleware.cs) (registered in `Program.cs`
  after `UseAuthentication`, before `UseAuthorization`): an authenticated
  GlobalAdmin without enrolled 2FA (and not a sample-data account) is limited
  to the enrolment, logout and static routes; every other route is redirected
  to enrolment. The login redirect in `AccountController` is a convenience only
  — the middleware is the gate.
- **Moderator: recommended, not required** — standing is component-scoped and their
  restricted reads are already audited.
- **Member: none** — overkill at this scale; their content is protected by
  deny-by-default audiences.
- **Break-glass interaction:** the [`incident-response`](../ops/incident-response.md) break-glass procedure requires the
  elevated account to have 2FA enabled (or enables it as part of elevation) — an
  account that just became an admin must meet the admin bar before it keeps the
  standing.
- **Recovery codes are mandatory:** they are the self-serve path if a TOTP device is
  lost; the two-admin rule ([ADR 0003](../adr/0003-roles-and-moderator-scoping.md)) is the fallback if the codes are gone too.

**Content-Security-Policy (decided 2026-08-27; XSS hardening, protects A3):**

- **Policy:** `default-src 'self'` (all assets are first-party — `tsc`-built JS, no
  CDN), `style-src 'self' 'unsafe-inline'` (inline styles in Razor are fine), no
  `script-src` extension.
- **The constraint is on the code, not the header.** The header itself is a
  one-line server-side change — loosening is nearly free (add a scoped `script-src`
  entry with SRI, or switch to nonces via `UseCsp`), but *tightening after the fact*
  means hunting down every inline handler the browser now refuses. So the discipline
  is pinned now, at zero lines of front-end code:
  - Razor views: **no inline `on*` attributes, no inline `<script>`** — use the
    `client/*.ts` modules with `addEventListener` (the documented pattern, [`ARCHITECTURE.md`](../ARCHITECTURE.md) §7).
  - Any future third-party script gets a *scoped* `script-src` addition **plus
    Subresource Integrity**, decided per case — never by loosening `default-src`.

**Password storage (decided 2026-08-27; protects A2/A3):** ASP.NET Identity hashes
passwords with PBKDF2 (HMAC-SHA256); the iteration count is configurable via
`IdentityOptions` and we document the default rather than silently depending on it.
No custom password scheme — Identity's format, reset flow, and lockout are the ones
we test and operate ([`hardening-checklist`](../ops/hardening-checklist.md)). The posture is recorded in [ADR 0172](../adr/0172-password-hash-posture-pbkdf2-identity-default.md).

**Translation of user-generated content (decided 2026-08-27; protects A3 privacy):**
off by default. UGC is always rendered as authored — nothing is translated silently,
and platform texts (UI, terms, about, help) are translated *in-instance* from data
and never leave the box. If a machine-translation feature is ever enabled: the
provider is a third-party boundary like B4 — **audience-restricted content is never
sent to it** — translation is per-item, user-initiated, and always labeled
"machine translation", and this decision is re-recorded here before shipping
([ADR 0005](../adr/0005-multilingual-support.md) C). The **authored-in language tag** on `Post` / `PostReply` /
`Announcement` ([ADR 0018](../adr/0018-ugc-authored-in-language-tag.md)) is **not** a translation step — it is a BCP-47
metadata field (for future search and reader-added language versions), carries
no content to a third party, and changes none of this posture.

**Verify-lane link credential (decided 2026-10-31; owner decision U23, protects A3):**
the email-verification / reset link's credential is the `IdentityToken` **row
`Id`** — the row itself is the credential (single-use, TTL-bounded), so the
verify lane needs **no separate at-rest token hash**. This supersedes the
historical "tokens hashed at rest" phrasing for the *verify* lane
([`WhatsNew.cs`](../../src/Kumunita.Web/WhatsNew.cs) 0.68.0, F2;
[`STATUS.md`](../STATUS.md) — both historical, not rewritten here); the F2
SHA-256-hex write applies to the **setup** mint, which is a different lane.

**Retention policy (decided 2026-11-06; owner D6 "Moderate", protects A3/A6):**
the previously unbounded types now carry a retention key with a day-count
default in [`RetentionOptions`](../../src/Kumunita.Core/Privacy/RetentionOptions.cs)
(keys `Retention__*`; `0` = keep): sessions 30 d, outbox body nulled 7 d (row
90 d), dead letters 30 d, notifications read 90 / unread 365, resolved error
reports 180 d, consumed/expired tokens 7 d. One `RetentionTick` enforces them
(IS33 / IS34a / IS35).

### Open items (decide before shipping the surface they protect)

<a id="open-items"></a>

SEC lane (2026-10-31, `6dafb783`) **closed:** the proxy-trust / forwarded-headers
item (H3 — see the "Proxy trust" decision above: config-driven, never trust-all,
`Secure` cookie policy, HSTS `max-age=31536000` + `Permissions-Policy`) and the
GlobalAdmin 2FA item (H2 — see the "2FA" decision above: `AdminTwoFactorMiddleware`
is the server-side gate, the login redirect a convenience). The SEC remediation
register and its per-finding test rows are in [the audit log](audit-log.md).

| Item | Protects | Decision needed |
|------|----------|-----------------|
| Log hygiene — **PII half (closed):** no PII in log templates and nothing below Warning reaches a third party — **decided and closed by [IS08](../plans-milestones/plan-improve-sec.md)** (`LogTemplatePiiGateTests.NoLogTemplate_CarriesAPiiPlaceholder` gate + the [IS05](../plans-milestones/plan-improve-sec.md) `MinimumBreadcrumbLevel = Warning` floor) | A3 (PII in logs) | **Closed** — IM-SEC IS08 (2026-11-07) |
| Log hygiene — **log-forging half (still open):** sanitize user-supplied strings (newlines/CR) before logging so logs can't be forged — **SEC L8c, parked 2026-10-31, no owner decision (the open-ended scope is why it parked)** | A3 (log integrity) | **Still open** — L8c, no owner decision |
| Named invariant tests (empty-audience-denies, delegation action-scope) referenced from [`ARCHITECTURE.md`](../ARCHITECTURE.md) §4.4 | A3 | **Closed** — both tests exist and are pinned by the IS04 deny-matrix gate: `EvaluateAudience_EmptyAudience_AllMode_Denies` and `C2_Delegate_OutOfScope_Denies_WithDelegationViaRecorded` ([`AccessViaDenyMatrixTests.EmptyAudienceDenies_AndDelegationScope_ArePinned`](../../tests/Kumunita.Web.Tests/SecGate/AccessViaDenyMatrixTests.cs), IM-SEC [IS04](../plans-milestones/plan-improve-sec.md)) |
| **Sign-up gate ([ADR 0050](../adr/0050-admin-managed-signup-gate-open-or-invitation-only.md)) + the invitation mechanism it closes to.** The gate is landed: a GlobalAdmin flips the instance open / invitation-only (`/admin/signup`; `LocaleSettings.IsSignupOpen`, `false` floor as amended 2026-10-05 — a fresh instance ships **invitation-only** so the admin can set up the platform first), and when closed the `/account/signup` write lane and the nav/login sign-up affordances are authoritative — new self-service accounts are refused (existing residents unaffected). That is the control that answers A2 better than rate-limiting alone. **Still open:** the *invitation mechanism* a closed instance closes the door *to* — an invited resident self-serves their password from an admin-sent link rather than registering (token lifecycle, expiry, admin UX). Decide it before the community is open to residents beyond the development team. | A2 (signup bots) | Gate: landed ([ADR 0050](../adr/0050-admin-managed-signup-gate-open-or-invitation-only.md)); invitation mechanism: before wider rollout |

Decisions taken here should link back into [`hardening-checklist`](../ops/hardening-checklist.md) (checklist) and, where they
change a design decision, into an ADR.
