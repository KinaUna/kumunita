---
id: audit-checklists
title: "Checklists C / D / P / R"
tier: reference
kind: security
status: current
audiences: [developer, platform-maintainer, community-admin, agent]
related:
  - docs/SECURITY-AUDIT.md
  - docs/security/audit-log.md
  - docs/security/audit-annual-trigger.md
  - docs/security/why-first.md
  - docs/security/control-map.md
  - docs/security/trust-boundaries.md
  - docs/security/data-classes.md
updated: 2026-11-02
---

> **Abstract:** the four run-anywhere checklists — C (code, per PR + release),
> D (deployment, per release), P (platform admin, monthly), R (recurring
> review, quarterly) — written so one person can run each start-to-finish
> without a meeting. **Who is it for:** the developer (C, D), the
> GlobalAdmin (P), the GlobalAdmin + operator (R). **When to read the rest:**
> at the cadence listed in the [hub](../SECURITY-AUDIT.md); after an
> incident (P); before a release (C-full + D).

<a id="checklist-c-code"></a>
## Checklist C — code

**Short form (every PR touching a security surface**: audiences, roles,
delegation, identity, media/attachments, admin, moderation, audit):

- **C-1** New reads of audience-restricted content go through
  `IAuthorizationService.CanAsync(Read)` — no ad-hoc audience checks, no
  privilege sent by the client ([why-first](why-first.md) rule 1,
  [ADR 0006](../adr/0006-module-boundary-contracts.md)).
- **C-2** New restricted reads, admin actions, or moderator unlocks are
  logged — `AccessAudit` / `Deny` rows per the lane's ADR. No silent new
  privilege surface ([why-first](why-first.md) rule 3).
- **C-3** No inline `on*` attributes or inline `<script>` in new Razor views;
  client behavior lives in `client/*.ts` (the CSP discipline,
  [decisions](decisions-open-items.md#decisions)).
- **C-4** No secrets in code, config, or log output; any new env var is
  documented in the [configuration reference](../ops/configuration-reference.md).
- **C-5** Any new file handling: type/size allowlist, auth-gated endpoint,
  never a static path ([ADR 0011](../adr/0011-media-and-file-storage.md) /
  [ADR 0034](../adr/0034-file-attachments-posts-replies-announcements.md) shape).
- **C-6** If the PR introduces a **new data class or privilege surface**, a row
  was added to the [control map](control-map.md) *before* merge. If it
  settles a design question, it got an ADR (repo convention).

**Full form (every release, in addition to the short form):**

- **C-7** Full test suite green, including the named invariant tests
  (empty-audience-denies, delegation action-scope). These are the executable
  spec of the authorization core — a green suite without them is not a green
  suite. Run per [`AGENTS.md`](../../AGENTS.md):

  ```powershell
  dotnet build Kumunita.slnx -c Debug
  dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
  dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
  ```

- **C-8** Walk **one** authorization path end-to-end on the dev instance as
  three personas (Member / Moderator / GlobalAdmin) and confirm the audit
  rows (Allow **and** Deny) land exactly as the lane's ADR specifies — e.g.
  attachments: one `Deny` row on a UGC deny, zero on an announcement 404
  ([ADR 0034](../adr/0034-file-attachments-posts-replies-announcements.md) C-ATT·7).
- **C-9** The diff added no endpoint that is unauthenticated *by accident*;
  every state-changing form still posts an anti-forgery token.
- **C-10** New endpoints that are escalation or flooding surfaces (register,
  login, report-filing, any new mutating admin surface) have a rate-limiting
  policy ([hardening-checklist](../ops/hardening-checklist.md)).
- **C-11** Grep the diff for user-supplied strings reaching `ILogger` or
  exception messages un-sanitized (log-forging hygiene —
  [open items](decisions-open-items.md#open-items)),
  and for secrets/tokens/PII in error output.

*Last run: (record date + release tag in [the audit log](audit-log.md)).*

<a id="checklist-d-deployment"></a>
## Checklist D — deployment (per release / per new instance)

Verify on the **live** instance, in a browser or with `Invoke-WebRequest` —
not from the config file. A header that is configured but not served is not
served.

- **D-1** Public surface is 80/443 + SSH (key-only) only. Postgres is
  unreachable from outside ([hardening-checklist](../ops/hardening-checklist.md)).
- **D-2** HTTPS-only + HSTS present; `Content-Security-Policy: default-src
  'self'` present; `X-Content-Type-Options: nosniff` + `Referrer-Policy`
  present (the F7 global floor — every response carries them); session
  cookie is `Secure`/`HttpOnly`/`SameSite=Lax` (browser devtools → response
  headers + cookie attributes).
- **D-3** Rate limiting is active **and** partitioning by real client IP —
  the reverse proxy forwards `X-Forwarded-For` and the app trusts it; a burst
  test from one IP is throttled without another client feeling it
  ([hardening-checklist](../ops/hardening-checklist.md) proxy requirement —
  "verified, not assumed").
- **D-4** Identity lockout at defaults (5 failures → 30 min) and 2FA enabled
  on **every** GlobalAdmin on this instance ([hardening-checklist](../ops/hardening-checklist.md)).
- **D-5** Supply chain: base image and package versions are the pinned ones
  from the release; the release diff introduced **no** new third-party
  boundary (SMTP, object store, scripts, fonts — [trust boundaries](trust-boundaries.md) B1–B6).
- **D-6** Backups: latest backup is recent, encrypted at source, present
  offsite; the last restore drill (R-1) is inside the current quarter
  ([backups](../ops/backups.md) + [restore](../ops/restore.md)).
- **D-7** Sign-up gate state is *deliberate* — open or invitation-only
  ([ADR 0050](../adr/0050-admin-managed-signup-gate-open-or-invitation-only.md)), matching the community's rollout stage; if closed, the
  invitation mechanism is the only door in.
- **D-8** Post-upgrade: warm-boot backfills ran (seeders, create-if-missing),
  no unexpected Marten migrations, health/monitoring endpoints green
  ([upgrade](../ops/upgrade.md), [monitoring](../ops/monitoring.md)).
- **D-9** SEC live-config checks (2026-10-31, SEC lane H3/H5/M4/L7 — none of
  these is provable by a unit test, verify on the live instance):
  - (a) **Forwarded headers:** `curl -sI https://<domain>/` shows
    `Strict-Transport-Security: max-age=31536000` **and** a
    `Set-Cookie: kumunita.auth=…; Secure` attribute; from two source IPs a
    wrong-password burst locks out only one IP (real-IP partitioning —
    otherwise set `ReverseProxy:KnownNetworks` to the proxy's source CIDR).
  - (b) **`/health`:** anonymous `GET /health` returns only the minimal
    liveness payload; the full payload requires the `X-Health-Token` header
    matching `Health__Token` (or a GlobalAdmin session). Set `Health__Token`
    ([`60-verify`](../deploy/coolify/60-verify.md)).
  - (c) **`AllowedHosts`:** set to the community's public host (in-repo stays
    `*` for dev); optionally set `WellKnown__BaseUrl` so `security.txt`
    carries the public origin.
  - (d) **ClamAV `Scan`:** `VirusScan__Host`/`VirusScan__Port` are set only
    if the operator has a ClamAV daemon; the default (unset ⇒ fail-open seam
    absent) is a deliberate choice and must be a *documented* one on this
    instance.
  - (e) **Revocation cut-off:** `Security:RevocationStamp:RequiredAfter` set
    to a UTC date ≈ 14 days out (veto 5 — deploy date + 14 days) so existing
    unstamped sessions are not logged out at once, and are rejected after.

*Last run: (record date + release tag in [the audit log](audit-log.md)).*

<a id="checklist-p-platform-admin"></a>
## Checklist P — platform admin (monthly, by a GlobalAdmin)

Run once a month (or after any incident), against the instance's own UI and
audit log — this is a review of *the community*, not the code.

- **P-1** Every GlobalAdmin (and any standing break-glass account) has TOTP
  2FA + recovery codes; no stale GlobalAdmin accounts (departed contributors,
  test accounts that outlived their purpose).
- **P-2** Recent audit log: any spike of `Deny` rows from one source
  (probing), any `Via: Admin` / `BreakGlass` row without a remembered
  justification, any admin action that predates its actor's account being
  admin (impossible → investigate).
- **P-3** Guardian links: every link is over an account the community knows to
  be a child's ([ADR 0028](../adr/0028-guardian-controls-account-scope-supervision.md) G·4); dissolve any disputed or orphaned link (G·5);
  no standing suspension that should have been lifted.
- **P-4** Report queue: no stale or unhandled reports; no moderator
  unlock/restricted view that is still "on" after its report was resolved —
  least standing means the toggle goes back off ([why-first](why-first.md) rule 2).
- **P-5** Credential rotation is on schedule (SMTP, DB password, backup key)
  and immediately after any suspected exposure ([hardening-checklist](../ops/hardening-checklist.md)).
- **P-6** CAPTCHA revisit triggers ([hardening-checklist](../ops/hardening-checklist.md)): has
  signup or failed-login abuse been observed? Do several neighborhoods now
  share a VPS/public IP? Either answer yes → the deferred decision becomes a
  lane.
- **P-7** Instance inventory ([configuration-reference](../ops/configuration-reference.md)) still matches reality:
  domain, TLS certificate expiry (renewal actually happens), env/secrets
  current, monitoring alert reaches a human.

*Last run: (record date + who in [the audit log](audit-log.md)).*

<a id="checklist-r-recurring-review"></a>
## Checklist R — recurring review (quarterly, GlobalAdmin + operator)

- **R-1** **Restore drill:** restore the latest backup to a scratch
  database, boot the app against it, spot-check that data is complete (a
  post, a profile, a media object, an audit row), then destroy the scratch
  copy ([restore](../ops/restore.md)). A backup that has never been restored is a hope.
- **R-2** **Dependency audit:** NuGet and npm vulnerability + currency scan
  (the solution is two projects with a small dependency set — this is a
  minutes-scale job); pin updates in a lane, rebuild on base-image updates
  ([adversaries](adversaries.md) A6).
- **R-3** **Control-map verification:** walk the [control map](control-map.md) table and
  confirm each row's control is present in the *running* instance — headers,
  lockout, rate limiting, audit rows, media/attachment endpoints — the
  difference between "we decided it" and "we saw it work".
- **R-4** **Open items:** every row in [decisions & open items](decisions-open-items.md) is
  re-decided — closed by a decision + ADR, or explicitly re-opened with a
  reason. The invitation mechanism is the standing one.
- **R-5** **Doc ↔ code consistency:** the [hardening-checklist](../ops/hardening-checklist.md),
  the [control map](control-map.md), and the shipped code still agree; the
  [incident log](../ops/incident-response.md) has no
  un-followed-up rows; README roadmap / `Milestones.cs` are in step (repo
  convention).
- **R-6** **Threat-model sanity:** has the deployment shape changed — shared
  hosting, a new third-party boundary, a new data class, a new privilege
  surface (e.g. a new role like Guardian/Translator)? If yes: [trust
  boundaries](trust-boundaries.md), [adversaries](adversaries.md), [control
  map](control-map.md) and the [cadence](../SECURITY-AUDIT.md#cadence) all get a row before the
  change ships.

*Last run: (record date in [the audit log](audit-log.md)).*
