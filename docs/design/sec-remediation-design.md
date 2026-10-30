# SEC — Security-audit remediation — design doc

> Part 1 (this): Context, Scope, Invariants, Findings register. Written by U1.
> Part 2 (fix shapes, config keys, signatures, test names, acceptance gate,
> drift-guard) is appended by U2. Do not edit Part 1 outside the drift-guard.

## Context

A **static** security review of `main` @ `bdcaff7` (2026-10-30) found **3 High,
5 Medium, 8 Low** issues. The review read the tree at that revision; **nothing was
executed** — no live server, no exploit, no dependency scan (the scan is deferred
to U3). The authorization core, the CSP/XSS posture, SQL parameterization,
file-path handling and the MFA primitives held up. The gaps are places where
code drifted from `docs/SECURITY.md` §5–§6 (the control map and the open-items
register).

This lane's job is to close each finding with **one named regression test**, in
units small enough for a fresh agent with a 32K–64K token window. The source of
the findings is **Appendix A** of `docs/plans-milestones/in-progress/sec/plan-sec-remediation.md`
(the 2026-10-30 register). Its file:line references are from `bdcaff7` and are
marked *re-verify in U2* until U2 re-anchors them against the current tree.

## Scope

- **In:** all of **H1–H3** (High), **M1–M5** (Medium), **L1–L8** (Low).
- **Parked (kept as written units, not on the critical path — owner decision required before they start):**
  - **L1** (U28) — uniform signup/login responses. A privacy-vs-UX call; the
    owner must record `## Decision — L1` in the handoff note before U28 proceeds.
  - **L8c** (within U27's finding, but the log-forging sub-item is *parked*) —
    sanitize user-supplied strings before logging so logs can't be forged. This
    is the `SECURITY.md` §6 open item "Log hygiene … so logs can't be forged";
    it stays open and is named as deferred, not fixed by this lane.
- **Out (explicitly not part of this lane):**
  - New features or capability additions.
  - Dependency upgrades / advisory remediation (U3 records the scan result;
    fixing advisories is a separate decision).
  - Anything not named in the findings register above.

## Invariants

Every fix in this lane must preserve or establish the following. A fix that
violates one is a regression, not a fix.

- **S-1** — An import **never creates or changes a row the actor does not own**.
  (H1: `UserPortabilityService`)
- **S-2** — A GlobalAdmin session **cannot reach any route but enrolment/logout
  without 2FA** enrolled (and not a sample-data account). (H2)
- **S-3** — Client IP and scheme come **only from a configured trusted proxy**;
  unset/empty config never means "trust everyone". (H3)
- **S-4** — Every state-changing request **validates an anti-forgery token**
  unless explicitly token-authenticated (the two inbound/suppress endpoints). (M1)
- **S-5** — Every anonymous write has a **rate limit and a size cap**. (M2, M3)
- **S-6** — A session whose **stamp is missing or stale is rejected after the
  cut-off** (`Security:RevocationStamp:RequiredAfter`). (M5)
- **S-7** — User-controlled content **never produces an off-site link or an
  unvalidated file type**. (L2, L4)
- **S-8** — **Every fix has a named regression test** (pinned in Part 2).

## Findings register

Source: Appendix A of the lane plan (static audit of `main` @ `bdcaff7`,
2026-10-30). **Every file:line is from that revision — *re-verify in U2*.**

| ID | Sev | Finding | Where (audit-time — *re-verify in U2*) | Invariant | Unit(s) |
|----|-----|---------|----------------------------------------|-----------|---------|
| H1 | High | Resident import stores archive rows with no ownership or `Id`-collision check (forge authorship, overwrite documents, join private groups); tests only cover export | `UserPortabilityService.cs:714` (classify), `:1100`, `:1141` (`session.Store(row)`); `UserScopeInventory.cs` | S-1 | U4–U6 |
| H2 | High | GlobalAdmin 2FA is a redirect, not a gate; the admin cookie already exists; nothing checks `TwoFactorEnabled` | `AccountController.cs:802`, `:893-907` | S-2 | U7–U8 |
| H3 | High | `UseForwardedHeaders()` has no options (no-op): one shared rate-limit bucket, HSTS not emitted, cookie not `Secure` (depends on live proxy config) | `Program.cs:948`, `:951`, `:565`, `:599`, `:586` | S-3 | U9–U10 |
| M1 | Med | Seven state-changing POSTs lack anti-forgery validation; no global filter | `CommunityController` `SetMandatory` (114), `AddMember` (136), `RemoveMember` (158), `Leave` (198); `MessagesController` `Open` (162), `Send` (333); `PostsController` `Replies` (1280) | S-4 | U11–U12 |
| M2 | Med | Zip bomb (no entry-count / decompressed-size cap) and imported media bypasses upload gate and quota; import actions unthrottled | `KumunitaArchive.cs:273-300`; `UserPortabilityService.cs:1169` | S-5 | U13–U14 |
| M3 | Med | Anonymous writes without rate limit; error-report text has no length cap | `HomeController.cs` (`/Home/Error/Report`), `IssueController.cs` (`/issues/new`), `/escalations/inbound`, `/api/mail/suppress`, `ErrorReportFormModel.cs:29` | S-5 | U15 |
| M4 | Med | Anonymous `/health` does a DB query + SMTP handshake per request; full detail public unless `Health__Token` set; non-constant-time compare | `HealthController.cs:67-109` | S-5 | U16 |
| M5 | Med | Session revocation fails open when a cookie has no stamp; verify-link sign-in is unstamped; built-in stamp validator interval is 14 days | `RevocationStampMiddleware.cs:130`; `AccountController.cs:719`; `Program.cs:663` | S-6 | U17–U18 |
| L1 | Low | Account enumeration: signup message, lockout message, login timing (**parked — U28, owner decision**) | `AccountController.cs:522`, `:783`, `:855` | (parked) | U28 |
| L2 | Low | Upload type trusted from client; dedup keeps first uploader's declared type; ClamAV off by default | `AttachmentController.cs:100`, `ContentImageController.cs:190`, `ProfileController.cs:463`, `DocumentController.cs:413,646` | S-7 | U20–U22 |
| L3 | Low | Verification token "hash at rest" is accepted as a credential; GET link signs the browser in | `IdentityService.cs:1698`; `AccountController.cs` Verify | S-4 | U23–U24 |
| L4 | Low | Markdown link filter lets `/\host` through; `mailto:` never accepted | `MarkdownRenderer.cs:397` | S-7 | U19 |
| L5 | Low | Container runs as root; floating image tags; CI actions pinned by tag, no `permissions:` | `Dockerfile`, `.github/workflows/ci.yml` | (hardening) | U25 |
| L6 | Low | Dev compose publishes Postgres with default credentials, Development mode | `docker-compose.yml` | (dev hygiene) | U26 |
| L7 | Low | HSTS scope, `AllowedHosts: *`, `security.txt` URL from `Request.Host`, no `Permissions-Policy` | `Program.cs:951`, `appsettings.json:12`, `WellKnownController.cs` | S-3 | U26 |
| L8 | Low | Sample-account limiter exemption and form-body read inside limiter; non-constant-time break-glass compare; **log-forging open item (parked)** | `Program.cs:586-595`, `IdentityService.cs`, `SECURITY.md` | S-6 | U27 (L8c parked) |

**Parked sub-items (counted in the register, deferred by owner decision):**

1. **L1** (U28) — account enumeration / uniform responses. Privacy-vs-UX; needs
   `## Decision — L1` before it starts.
2. **L8c** (within U27) — log-forging hygiene (sanitize user-supplied strings
   before `ILogger`). Named as deferred in the close-out; not fixed by this lane.

**What held up (do not "fix"):** parameterized SQL at the three raw sites; strict
CSP and Markdown escaping; service-level authorization on Announcement actions;
hex-only media paths and safe download headers; lockout, TOTP code checks,
rate-limited second leg; anti-forgery on all other state-changing POSTs.
