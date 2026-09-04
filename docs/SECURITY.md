# Kumunita — Security & Privacy

Security and privacy are the **highest-priority requirements** of this product, not
features to add. This document is the single source for *what we protect, from whom,
and why*. It complements `ARCHITECTURE.md` (how the mechanisms work) and `OPS.md`
§10 (the operational checklist). Rationale for identity/authorization decisions lives
in `adr/0001` and `adr/0003`.

Status: pre-M0, design-level. Update this document when a control changes, a threat is
accepted, or an open item below is decided — and record non-obvious decisions as ADRs.

## 1. Why this is the top priority

- A Kumunita instance holds **real, named neighbors** and their private conversations.
  A leak isn't a dataset — it's the person down the street.
- Scale is tiny (dozens to hundreds of users), so **sophisticated attack economics
  don't apply; the realistic threats are simple ones** (stuffing, scraping, a bad
  actor with an account, an operator mistake). Simple threats need simple, correct
  controls — not a security framework.
- One deployment per neighborhood (ADR 0002) means **isolation is total but also
  total-loss**: there is no shared-infrastructure blast radius, and no shared defense
  in depth either. Each instance stands on its own.

**Deployment assumption (confirmed):** each neighborhood gets its **own VPS and its own
domain** (a dedicated instance per ADR 0002, not co-hosted neighborhoods on one box).
Per-instance rate limiting is therefore effective, and one community's traffic or abuse
cannot exhaust or implicate another's. If this ever changes (shared hosting), revisit
§6 (rate limiting) and the CAPTCHA decision (OPS.md §10).

**Two operating rules shape everything below:**

- **Privacy by design, not by promise.** Access rules are enforced in code on every
  request (ARCHITECTURE.md §4.4); the audit trail exists to *prove* the design held,
  not to substitute for it. A control that depends on someone remembering to check is
  not a control.
- **Security is non-negotiable scope.** A security or privacy issue found at any
  milestone stops feature work until it is resolved or explicitly accepted in writing
  (here, or in an ADR). Schedule pressure is not an acceptable reason to defer one.

**Design rules, in priority order:**

1. **Deny by default.** No audience = invisible to everyone (the empty-audience-denies
   invariant, ARCHITECTURE.md §4.4).
2. **Least standing.** Moderator access to restricted content is off by default and
   unlocked only by a report or an explicit admin toggle (ADR 0003).
3. **Audit by default.** Restricted-content access is always logged; admin and
   moderation actions are always logged (ARCHITECTURE.md §5).
4. **Fail closed.** Authorization errors, missing grants, and concurrency failures
   resolve to denial / rollback — never to a guess.
5. **Minimize.** No event-sourced data lake, no third-party analytics, no telemetry.
   What we don't store, we can't leak.

## 2. Trust boundaries

Data crosses these boundaries; each one is a point where protection is delegated to
someone else.

| # | Boundary                       | Who controls it                    | Protection / notes                                                                 |
|---|--------------------------------|------------------------------------|------------------------------------------------------------------------------------|
| B1 | Browser ↔ app (TLS)           | Coolify / Let's Encrypt            | HTTPS-only + HSTS (OPS.md §10); cookies `Secure`/`HttpOnly`/`SameSite=Lax`         |
| B2 | Public internet → VPS         | VPS host (operator)                | Only 80/443 + SSH (key-only) exposed; Postgres never public (OPS.md §10)            |
| B3 | App ↔ Postgres                | operator                           | Dedicated non-superuser DB account after first run; internal network only           |
| B4 | App ↔ SMTP provider           | **provider (third party)**         | SPF/DKIM/DMARC (OPS.md §7); provider can read email bodies — see §3 class (a)      |
| B5 | Backups → offsite storage     | **object store (third party)**     | Encrypted at rest by the store; **also encrypt backups at the source**; access scoped to a key we rotate (OPS.md §4) |
| B6 | Secrets: env / secrets manager| operator + Coolify                 | One-time seed token pattern; secrets never in image, logs, or this repo (OPS.md §10) |

**Operator = trusted-but-not-invulnerable.** The host operator can read everything on
a VPS. The break-glass design (ADR 0003, ARCHITECTURE.md §4.5) assumes the operator
may be the *only* honest party left, and constrains what an app-admin can do to the
operator's authority.

## 3. Data classes

How each class is stored and what its loss means.

| Class | Contents | Stored where | Sensitivity if leaked |
|-------|----------|--------------|------------------------|
| **(a) PII** | names, emails, phones (opt-in), profiles, households | `mt` (Profile…), `identity` | High — identifies real neighbors; phone/email is contact-targetable |
| **(b) Private content** | audience-restricted posts, replies, RSVPs, project members | `mt` | High — private conversations; disclosure breaks community trust permanently |
| **(c) Audit & moderation** | `AccessAudit`, `Report`, `AdminOverride` | `mt` | **High, asymmetric** — reveals *who accessed what*, incl. *denied* items (see §3.1) |
| **(d) Secrets** | DB/SMTP credentials, seed token, backup keys | env / secrets manager / object store | Critical — full instance compromise |

### 3.1 The audit log is a disclosure surface (accepted by design)

`AccessAudit` records **denials with `targetId`**, and list views record `hiddenCount`.
A GlobalAdmin reading the log can therefore enumerate *which* restricted items exist and
*who was kept out of them*. That is a deliberate property — it is what makes the audit
log an accountability mechanism ("prove what happened", OPS.md §9) — but it means:

- The **audit log itself must be treated as class (c)**: GlobalAdmin-only, retained per
  the tiered policy (ARCHITECTURE.md §5), pseudonymized on account deletion.
- Do not "fix" it by dropping `targetId` or denials — that removes the accountability
  the community is promised.

## 4. Assumed adversaries

Ranked by realism at this scale. Each maps to the controls that answer it.

| # | Adversary | Can do | Mitigated by |
|---|-----------|--------|--------------|
| A1 | **Curious outsider** (no account) | scrape public pages, probe endpoints, attempt signup | nothing sensitive is public; opt-in contact details; rate limiting (OPS.md §10) |
| A2 | **Credential-stuffing / signup bots** | brute login, flood register & report endpoints | rate limiting on register/login/report (§6, OPS.md §10), Identity lockout (OPS.md §10); CAPTCHA deferred by decision (OPS.md §10) |
| A3 | **Compromised or malicious resident** (has an account) | try to read beyond their audience, abuse delegation, spam reports | deny-by-default audiences (ADR 0001-B); action-scoped delegation (ARCHITECTURE.md §4.4); report-filing rate-limited; security-stamp invalidation on privilege change (OPS.md §10); audit trail for after-the-fact |
| A4 | **Malicious / hostile GlobalAdmin** | read everything, suppress others, extend their power | two-admin standing practice; no in-app `AdminOverride` creation; operator-only break-glass; `BreakGlass`-tagged audit (ADR 0003, ARCHITECTURE.md §4.5) |
| A5 | **Third parties & operator path** (SMTP provider, object store, VPS host, operator mistake) | leak data at B4/B5, misconfigure | encryption at rest + at source, scoped keys, SPF/DKIM/DMARC, least-privilege DB user, backups-verified (OPS.md §4, §7, §10) |
| A6 | **Supply chain** (base image, NuGet/npm) | malicious or stale dependency | pinned image + package versions; rebuild on base updates (OPS.md §10); two-project solution with no heavy runtime deps (ADR 0001) |

**Not assumed (documented, not designed for):** nation-state actors; VPS-level physical
access; a compromised Coolify platform. If the deployment context changes, add them.

## 5. Control map — every threat must be covered

Quick self-check when adding a feature: does it introduce a new data class or a new
privilege surface? If so, add a row here and in the relevant checklist.

| Control | Answers | Owner / detail |
|---------|---------|----------------|
| Deny-by-default audiences + empty-audience-denies | A1, A3 | ARCHITECTURE.md §4.3–4.4 |
| Thin token, fat authorization (no privilege in cookie) | A3 | ADR 0001-B, ARCHITECTURE.md §4.1 |
| Security-stamp rotation on privilege change | A3 (revoked grants) | OPS.md §10 |
| Optimistic concurrency + atomic audit commit | A3 (lost-update abuse) | ARCHITECTURE.md §5 |
| Rate limiting (register / login / report), per-IP | A2 | §6 (mechanism decided); OPS.md §10 (endpoints + XFF) |
| Identity account lockout, per-account (defaults) | A2 | OPS.md §10 — pairs with rate limiting; see division of labor there |
| **2FA (TOTP): required for GlobalAdmin**, recommended for Moderator | A3, A4 | §6 (decided); OPS.md §10; break-glass step OPS.md §9 |
| Content-Security-Policy `default-src 'self'` + no-inline-script rule | A3 (XSS) | §6 (decided); OPS.md §10 |
| PBKDF2 password hashing (Identity default; iteration count documented) | A2, A3 | §6 (decided) |
| CAPTCHA (deferred, documented decision) | A2 | OPS.md §10 |
| Report-gated, audited moderator access | A4 (and protects A3's privacy from mods) | ADR 0003 |
| Two-admin rule + operator-only break-glass | A4 | ADR 0003, ARCHITECTURE.md §4.5, OPS.md §9 |
| One-time seed token, removed from env after first boot | A5 | OPS.md §2 |
| Non-superuser DB, internal-only | A1, A5 | OPS.md §10 |
| Encrypted offsite backups + verified restores | A5, disaster | OPS.md §4–5 |
| Pinned image + packages | A6 | OPS.md §10 |
| SPF / DKIM / DMARC, stable From: | phishing-as-us (A3 impersonation) | OPS.md §7 |
| Audit log (always-on, tiered retention) | accountability for A3, A4 | ARCHITECTURE.md §5 |

## 6. Decisions & open items

### Decisions

**Rate limiting (mechanism, decided 2026-08-27; protects A2):**

- **Mechanism:** ASP.NET Core's built-in rate-limiting middleware
  (`AddRateLimiter`), with **per-endpoint policies** and **in-memory partitioning by
  client IP** (plus user identity where the actor is authenticated). At one instance
  per neighborhood with dozens of users, a process-local counter is the correct weight
  — no Postgres-backed or distributed rate limiter, no queue.
- **Covered endpoints (minimum):** `register`, `login` (failed attempts), and
  `report-filing` (an authorization-escalation surface — OPS.md §10). Add policies for
  other mutating surfaces as they ship (M1+).
- **Real client IP is a hard requirement.** Behind the reverse proxy the app sees the
  proxy's address unless the proxy forwards `X-Forwarded-For` *and* the app honors it
  (trust the proxy, e.g. `ForwardedHeaders` / `UseForwardedHeaders` configured for the
  proxy only). **Without this, per-IP partitioning collapses into one shared bucket** —
  every user shares one limit and a single client can exhaust it for everyone. This is
  verified in the M0/M1 smoke test, not assumed.
- **Revisit trigger:** shared hosting of multiple neighborhoods on one VPS/IP (see
  deployment assumption, §1) — per-instance limits stop protecting the others, and the
  CAPTCHA decision (OPS.md §10) is revisited in that case.

**2FA (decided 2026-08-27; protects A3, A4):** TOTP via ASP.NET Identity's built-in
support, plus recovery codes.

- **GlobalAdmin: required.** An admin account is total instance compromise (roles,
  standing moderator visibility, audit log); 2FA is the control, not optional.
- **Moderator: recommended, not required** — standing is component-scoped and their
  restricted reads are already audited.
- **Member: none** — overkill at this scale; their content is protected by
  deny-by-default audiences.
- **Break-glass interaction:** the OPS.md §9 break-glass procedure requires the
  elevated account to have 2FA enabled (or enables it as part of elevation) — an
  account that just became an admin must meet the admin bar before it keeps the
  standing.
- **Recovery codes are mandatory:** they are the self-serve path if a TOTP device is
  lost; the two-admin rule (ADR 0003) is the fallback if the codes are gone too.

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
    `client/*.ts` modules with `addEventListener` (the documented pattern, ARCHITECTURE.md §7).
  - Any future third-party script gets a *scoped* `script-src` addition **plus
    Subresource Integrity**, decided per case — never by loosening `default-src`.

**Password storage (decided 2026-08-27; protects A2/A3):** ASP.NET Identity hashes
passwords with PBKDF2 (HMAC-SHA256); the iteration count is configurable via
`IdentityOptions` and we document the default rather than silently depending on it.
No custom password scheme — Identity's format, reset flow, and lockout are the ones
we test and operate (OPS.md §10).

**Translation of user-generated content (decided 2026-08-27; protects A3 privacy):**
off by default. UGC is always rendered as authored — nothing is translated silently,
and platform texts (UI, terms, about, help) are translated *in-instance* from data
and never leave the box. If a machine-translation feature is ever enabled: the
provider is a third-party boundary like B4 — **audience-restricted content is never
sent to it** — translation is per-item, user-initiated, and always labeled
"machine translation", and this decision is re-recorded here before shipping
(ADR 0005 C).

### Open items (decide before shipping the surface they protect)

| Item | Protects | Decision needed |
|------|----------|-----------------|
| Log hygiene: sanitize user-supplied strings (newlines/CR) before logging so logs can't be forged | A3 (log integrity) | M1 |
| Named invariant tests (empty-audience-denies, delegation action-scope) referenced from ARCHITECTURE.md §4.4 | A3 | M1 |
| **Sign-up is currently open, self-service** (verification email + admin manual-verify valve, M1) — an acceptable interim state because the user base is the development team. The default must become **invitation-only**: an admin invites a resident, and they self-serve their own password from an invitation link rather than registering on their own. Decide the mechanism (token lifecycle, expiry, admin UX) before the community is open to residents beyond that team — this is the control that answers A2 better than rate-limiting alone. | A2 (signup bots) | After the M2–M3 development circle; before wider rollout |

Decisions taken here should link back into OPS.md §10 (checklist) and, where they

Decisions taken here should link back into OPS.md §10 (checklist) and, where they
change a design decision, into an ADR.
