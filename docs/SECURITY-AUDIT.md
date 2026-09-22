# Kumunita — Security Audit Program

Security and privacy are the product's top priority. The *what, whom, and why*
live in `docs/SECURITY.md` (threat model, data classes, control map) and the
hardening state lives in `docs/OPS.md` §10. This document is the missing
third piece: **how often we verify the controls actually hold, who runs each
check, and where the result is recorded.**

A control that is only *decided* is a promise. A control that is *verified on
a schedule and the verification is recorded* is a control. Every checklist
below is written so one person can run it start-to-finish without a meeting.

## 1. Rules

1. **Audits are cheap by design.** Dozens to hundreds of users, one instance per
   neighborhood (ADR 0002): checklists, not a security framework. If a check
   takes a day, split it or drop it.
2. **A failed check is a defect.** A security or privacy issue found at any
   point stops feature work until resolved or explicitly accepted in writing
   (SECURITY.md §1). Record the finding in §8 below and track it as a lane.
3. **Two eyes where it matters.** The platform-admin audit (§5) is run by a
   GlobalAdmin under the two-admin standing practice (ADR 0003) — one admin
   runs it, the other can see it happened in the audit log and this table.
4. **Record every run.** Every completed audit appends one row to §8 and
   updates the `Last run` date of the section it belongs to. An unrecorded
   audit did not happen.
5. **No PII in these logs.** Findings reference checklist IDs (C-3, D-1, …)
   and a one-line summary — never account names, emails, or content. The
   platform's own `AccessAudit` log remains the authoritative record of *who
   accessed what*; this program records *what we checked*.

## 2. Cadence

| Cadence        | Audit                                | Who                | Checklist        |
|----------------|--------------------------------------|--------------------|------------------|
| Per PR         | Code (short form) — security surfaces only | author + reviewer | §3 (C-1…C-6)     |
| Per release    | Code (full) + deployment             | dev + operator     | §3 (C-7…C-11) + §4 |
| Monthly        | Platform-admin review                | GlobalAdmin        | §5               |
| Quarterly      | Recurring review + restore drill     | GlobalAdmin + operator | §6          |
| Annual / trigger | Deep audits (§7)                    | dev + operator     | §7               |

"Per release" means every deployment to a neighborhood instance (OPS.md §3).
A release that skips §3-full + §4 is not done — same bar as a feature that
ships without its guide (CONVENTIONS.md consistency loop).

## 3. Checklist C — code

**Short form (every PR touching a security surface**: audiences, roles,
delegation, identity, media/attachments, admin, moderation, audit):

- **C-1** New reads of audience-restricted content go through
  `IAuthorizationService.CanAsync(Read)` — no ad-hoc audience checks, no
  privilege sent by the client (SECURITY.md rule 1, ADR 0006).
- **C-2** New restricted reads, admin actions, or moderator unlocks are
  logged — `AccessAudit` / `Deny` rows per the lane's ADR. No silent new
  privilege surface (SECURITY.md rule 3).
- **C-3** No inline `on*` attributes or inline `<script>` in new Razor views;
  client behavior lives in `client/*.ts` (the CSP discipline, SECURITY.md §6).
- **C-4** No secrets in code, config, or log output; any new env var is
  documented in the OPS.md configuration reference.
- **C-5** Any new file handling: type/size allowlist, auth-gated endpoint,
  never a static path (ADR 0011 / ADR 0034 shape).
- **C-6** If the PR introduces a **new data class or privilege surface**, a row
  was added to the SECURITY.md §5 control map *before* merge. If it settles a
  design question, it got an ADR (repo convention).

**Full form (every release, in addition to the short form):**

- **C-7** Full test suite green, including the named invariant tests
  (empty-audience-denies, delegation action-scope). These are the executable
  spec of the authorization core — a green suite without them is not a green
  suite. Run per `AGENTS.md`:

  ```powershell
  dotnet build Kumunita.slnx -c Debug
  dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
  dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
  ```

- **C-8** Walk **one** authorization path end-to-end on the dev instance as
  three personas (Member / Moderator / GlobalAdmin) and confirm the audit
  rows (Allow **and** Deny) land exactly as the lane's ADR specifies — e.g.
  attachments: one `Deny` row on a UGC deny, zero on an announcement 404
  (ADR 0034 C-ATT·7).
- **C-9** The diff added no endpoint that is unauthenticated *by accident*;
  every state-changing form still posts an anti-forgery token.
- **C-10** New endpoints that are escalation or flooding surfaces (register,
  login, report-filing, any new mutating admin surface) have a rate-limiting
  policy (OPS.md §10).
- **C-11** Grep the diff for user-supplied strings reaching `ILogger` or
  exception messages un-sanitized (log-forging hygiene — SECURITY.md §6 open
  item), and for secrets/tokens/PII in error output.

*Last run: (record date + release tag in §8).*

## 4. Checklist D — deployment (per release / per new instance)

Verify on the **live** instance, in a browser or with `Invoke-WebRequest` —
not from the config file. A header that is configured but not served is not
served.

- **D-1** Public surface is 80/443 + SSH (key-only) only. Postgres is
  unreachable from outside (OPS.md §10).
- **D-2** HTTPS-only + HSTS present; `Content-Security-Policy: default-src
  'self'` present; session cookie is `Secure`/`HttpOnly`/`SameSite=Lax`
  (browser devtools → response headers + cookie attributes).
- **D-3** Rate limiting is active **and** partitioning by real client IP —
  the reverse proxy forwards `X-Forwarded-For` and the app trusts it; a burst
  test from one IP is throttled without another client feeling it (OPS.md §10
  proxy requirement — "verified, not assumed").
- **D-4** Identity lockout at defaults (5 failures → 30 min) and 2FA enabled
  on **every** GlobalAdmin on this instance (OPS.md §10).
- **D-5** Supply chain: base image and package versions are the pinned ones
  from the release; the release diff introduced **no** new third-party
  boundary (SMTP, object store, scripts, fonts — SECURITY.md §2 B1–B6).
- **D-6** Backups: latest backup is recent, encrypted at source, present
  offsite; the last restore drill (R-1) is inside the current quarter
  (OPS.md §4–5).
- **D-7** Sign-up gate state is *deliberate* — open or invitation-only
  (ADR 0050), matching the community's rollout stage; if closed, the
  invitation mechanism is the only door in.
- **D-8** Post-upgrade: warm-boot backfills ran (seeders, create-if-missing),
  no unexpected Marten migrations, health/monitoring endpoints green
  (OPS.md §3, §8).

*Last run: (record date + release tag in §8).*

## 5. Checklist P — platform admin (monthly, by a GlobalAdmin)

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
  be a child's (ADR 0028 G·4); dissolve any disputed or orphaned link (G·5);
  no standing suspension that should have been lifted.
- **P-4** Report queue: no stale or unhandled reports; no moderator
  unlock/restricted view that is still "on" after its report was resolved —
  least standing means the toggle goes back off (SECURITY.md rule 2).
- **P-5** Credential rotation is on schedule (SMTP, DB password, backup key)
  and immediately after any suspected exposure (OPS.md §10).
- **P-6** CAPTCHA revisit triggers (OPS.md §10): has signup or failed-login
  abuse been observed? Do several neighborhoods now share a VPS/public IP?
  Either answer yes → the deferred decision becomes a lane.
- **P-7** Instance inventory (OPS.md) still matches reality: domain, TLS
  certificate expiry (renewal actually happens), env/secrets current,
  monitoring alert reaches a human.

*Last run: (record date + who in §8).*

## 6. Checklist R — recurring review (quarterly, GlobalAdmin + operator)

- **R-1** **Restore drill:** restore the latest backup to a scratch
  database, boot the app against it, spot-check that data is complete (a
  post, a profile, a media object, an audit row), then destroy the scratch
  copy (OPS.md §5). A backup that has never been restored is a hope.
- **R-2** **Dependency audit:** NuGet and npm vulnerability + currency scan
  (the solution is two projects with a small dependency set — this is a
  minutes-scale job); pin updates in a lane, rebuild on base-image updates
  (SECURITY.md A6).
- **R-3** **Control-map verification:** walk the SECURITY.md §5 table and
  confirm each row's control is present in the *running* instance — headers,
  lockout, rate limiting, audit rows, media/attachment endpoints — the
  difference between "we decided it" and "we saw it work".
- **R-4** **Open items:** every row in SECURITY.md §6 "open items" is
  re-decided — closed by a decision + ADR, or explicitly re-opened with a
  reason. The invitation mechanism is the standing one.
- **R-5** **Doc ↔ code consistency:** OPS.md §10 checklist, the §5 control
  map, and the shipped code still agree; the OPS.md §11 incident log has no
  un-followed-up rows; README roadmap / `Milestones.cs` are in step (repo
  convention).
- **R-6** **Threat-model sanity:** has the deployment shape changed — shared
  hosting, a new third-party boundary, a new data class, a new privilege
  surface (e.g. a new role like Guardian/Translator)? If yes: SECURITY.md §2
  (boundaries), §4 (adversaries), §5 (control map) and this document's
  cadence all get a row before the change ships.

*Last run: (record date in §8).*

## 7. Annual and trigger-based audits

- **A-1** **Full walk-through as a fresh resident and as an admin** on a
  throwaway instance: sign up (or receive an invitation), log in, set up 2FA,
  post with each audience type, attach a file, report a post, watch a
  moderator resolve it, watch an admin audit the log. The point is to catch
  the *security UX* that unit tests can't — a 2FA setup that dead-ends, an
  audience picker that defaults wrong, a recovery-code path that's lost.
- **A-2** **Break-glass drill (dry run on a dev instance only, never live
  data):** OPS.md §9 end-to-end — the elevated account meets the 2FA bar,
  the action is `BreakGlass`-tagged in the audit log, and the two-admin
  rule was observable (ADR 0003, ARCHITECTURE.md §4.5). A procedure that
  has never been run is a rumor.
- **A-3** **TLS/domain hygiene:** certificate chain valid and renewal
  automated, no `http://` URL anywhere in the served app or email, HSTS
  preload candidacy if the domain is stable.

**Trigger-based (run §3 short form plus the relevant section, whenever):**

| Trigger | Run |
|---------|-----|
| New third-party boundary added (any B-boundary in SECURITY.md §2) | D-5 + R-6 + a SECURITY.md §2 row |
| Shared hosting of multiple neighborhoods (one VPS/IP) | §6 rate-limiting decision + CAPTCHA decision (SECURITY.md §1) |
| Suspected compromise / breach | OPS.md §11 incident flow **first**, then P-1, P-2, D-6 |
| New privilege surface ships (new role, new admin action, new delegation) | C-1…C-6 + one P-2 + one R-3 |
| New data class stored | SECURITY.md §3 row + §5 row + R-4 |

## 8. Audit log

Append-only, one row per completed audit (or per failed check that became a
lane). No PII — checklist IDs and a one-line summary only.

| Date       | Scope (section + cadence)          | Who        | Result (pass / findings)        | Follow-up (lane/ADR) |
|------------|------------------------------------|------------|---------------------------------|----------------------|
|            | e.g. `C+D — release 2026-09-XX`    |            | pass / `C-8: Deny row missing`  |                      |

**Ownership summary:** dev owns C and D (with the operator for D-1/D-6/D-8),
the GlobalAdmin owns P, the operator owns D-1/D-6/D-8 and R-1. When in doubt
about who runs a check, the two-admin rule settles it: whichever admin runs
it must be able to show the other that it happened.
