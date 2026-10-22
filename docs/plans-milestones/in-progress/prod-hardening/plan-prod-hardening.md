# Prod-hardening — high-level fix plan for the 2026-10-22 full audit

This is the **high-level plan** for fixing the **FAIL** rows from the
2026-10-22 full production-readiness audit (Tier T1 self-hosted community
platform). It is the *source* that drives a per-fix individual plan for each
fix, and each individual plan is split into **atomic sub-units** sized for an
independent **clean-slate agent with a ~32K context window** — one unit at a
time, each unit finishing (build green + handoff entry) before the next agent
picks up with fresh context.

> The 22 FAIL rows, verbatim, live in the 2026-10-22 prod-check report
> (delivered in-session). This file references them by **ID** (AUTH-02, etc.)
> and does not re-quote the report. The UNKNOWN/ATTESTED rows are **not** in
> this plan — most are blocked on production access or owner attestation and
> are tracked separately (see "Out of scope here").

## Understanding

The audit separated cleanly into three buckets:

- **Code-fixable in-repo, testable now** — the majority. Each is a small,
  isolated change to an existing seam (identity tokens, the delete lane,
  middleware, a controller, a service). These are where this plan's atomic
  units live.
- **Code + owner-ops (mixed)** — the code unit lands in the repo and is
  tested; a companion **ops unit** is executed by the owner against the
  production VPS/Coolify/SMTP provider (WAL archiving, rollback drill, alert
  routing, provider alarms). The code unit is written so the ops unit has
  something to point at.
- **Owner-ops only / access-blocked** — the production firewall, registrar
  ownership, the live SMTP-domain DNS. These are the UNKNOWN rows; they are
  *out of scope* for a code agent and are not unitized here.

Every fix preserves the repo's invariants (ADR 0006 module boundaries, C3
single-write-lane, the "Core stays HTTP-free" rule, the frozen-seam ADD
discipline, and the server-rendered stack). None of the fixes are new
features: they harden existing surfaces. The one exception (a new `security.txt`
route) is a tiny additive surface with no bounded context.

## Classification + recommended build order

Ordering is by **severity (data exposure → data loss → money → availability →
rest)**, matching the report's "top 5 fixes" contract so the *next* audit run
can diff against this one. Dependencies are noted per fix. `code` = a code agent
can close it end-to-end in-repo with tests; `code+ops` = a code unit here plus
an owner-executed ops step; `ops` = owner-only (listed for completeness, not
unitized for a code agent).

| # | Fix (report ID) | Kind | Severity bucket | Deps |
|---|---|---|---|---|
| F1 | DB-13 — complete GDPR deletion | code | data loss/privacy | — |
| F2 | AUTH-02 — hash tokens at rest | code | data exposure | — |
| F3 | AUTH-03 — MFA for `GlobalAdmin` | code | data exposure | — |
| F4 | BKP-02 — PITR / WAL archiving | code+ops | data loss | — |
| F5 | AUTH-04 — session kill-all + invalidation | code | data exposure/avail | F3 (reuses the TOTP token store) |
| F6 | AUTH-09 — argon2id/bcrypt password hash | code | data exposure | — |
| F7 | WEB-07 — global `nosniff` + `Referrer-Policy` | code | everything else | — |
| F8 | WEB-10 — rate-limit upload lanes | code | everything else | — |
| F9 | AUTH-15 — signup bot challenge + disposable-email blocklist | code | everything else | — |
| F10 | OBS-02 — error tracker (Sentry) | code+ops | everything else | — |
| F11 | JOB-02 — alert on dead-letter exhaustion | code+ops | everything else | F10 (reuses its alert channel) |
| F12 | OBS-06 — route alerts to a human | code+ops | everything else | F10/F11 |
| F13 | OPS-05 — `security.txt` + VDP | code | everything else | — |
| F14 | DEP-03 — CI test gate on `release` | code+ops | everything else | — |
| F15 | UPL-04 — byte-store lifecycle/retention | code | everything else | F1 (shares the byte-delete seam) |
| F16 | UPL-02 — scan uploads before serving | code+ops | everything else | — |
| F17 | MAIL-03 — bounce/complaint suppression | code+ops | everything else | — |
| F18 | MAIL-06 — bounce/complaint/volume alarms | code+ops | everything else | F12 (alert channel) |
| F19 | TEST-03 — E2E suite (signup/verify/login/write) | code | everything else | — |
| F20 | DEP-04 — one-step rollback + drilled | code+ops | availability | F4 (rollback is a restore) |
| F21 | SRV-04 — app↔DB TLS (`SslMode`) | code+ops | everything else | — |
| F22 | DB-18 — demote app role (least-privilege) | code+ops | everything else | — |

**Suggested first wave (highest value, no external deps, all `code`):** F1, F2,
F6, F7, F8, F13. These six close six of the FAIL rows entirely and set up the
reusable seams (byte-delete for F15, the TOTP store for F3/F5, the alert
channel for F10–F12/F18) that the rest build on.

---

## Workflow — three-tier handoff protocol for fresh-context ~32K agents

Adapted from `docs/plans-milestones/done/m3/plan-m3-posts-components.md`, but
re-sized for **~32K** windows (M3 assumed ~64K, so units here are *smaller*):

**Shared state (three tiers):**
- **Tier 1 — the fix plan's Part-0 design pin** (written by the fix's U1): the
  exact C# seam signatures, the invariants, and the **pinned test names**.
  Small enough for one agent to author and re-read.
- **Tier 2 — this file** (`plan-prod-hardening.md`): the master register —
  each fix, its units, each unit's deliverables + exit criteria.
- **Tier 3 — the rolling handoff note**
  (`docs/plans-milestones/in-progress/prod-hardening/hardening-handoff-notes.md`):
  one short section per unit, **appended, never rewritten**. Each unit writes
  exactly one section before it exits; the next agent reads only that section +
  its own unit's entry reads.

**Per-unit template** (each `U` below follows it, sized for ~32K):
- **Goal** — one sentence, **one** deliverable (a single new/modified file, or
  a single method, or a single test).
- **Entry reads** — ≤ **3** files, each < ~250 lines; name the design-pin
  section to read. **No full-repo scan.**
- **Deliverables** — ≤ **2** files / ≤ **~300 LOC**; no drive-by cleanups.
- **Exit** — `dotnet build Kumunita.slnx -c Debug` green (code units), or the
  test dll run green; **handoff-note entry appended before any follow-up.**

**Unit-series rules (same as M3):**
1. A unit never touches a file not in its own `Deliverables`.
2. A unit never opens a *new* frozen seam beyond what the fix's Part-0 pinned.
3. A unit never re-shapes a frozen type outside its Part-0 pin.
4. A unit that finds the codebase diverged from the pin records
   `## <fix>.U<n> — Drift pause` in the handoff note and stops.
5. **Ops units** (kind `code+ops`) end at "code lands + test green"; the
   owner-executed step is recorded as an **open item**, not silently assumed
   done.
6. **Build/test command** (from repo memory — `dotnet test` discovery is
   broken on this machine): `dotnet build Kumunita.slnx -c Debug`, then
   `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll`
   and `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll`
   (Core needs Docker/Testcontainers and leaves containers behind if killed —
   `docker container prune -f` after).

**Marten note (from repo memory):** this Marten version rejects the
`For<T>(o => o.Index(...))` lambda form — use the chained
`For<T>().Index(...)` form; use `OrderBy*`, never `.OrderDescending(...)`.

---

## Individual plans (one per fix, each split into atomic units)

> Each fix below is an **individual plan**. Its units are the **sub-units** a
> clean-slate agent executes one at a time. A fix's **U1 is always its Part-0
> design pin** (Tier 1), so every later agent in that fix reads the same pinned
> seams. Where a fix is `code+ops`, the last unit is the owner-executed ops step
> (recorded as an open item, never assumed).

### F1 — DB-13: complete GDPR deletion (data loss/privacy) — `code`
**Context (from audit):** the delete lane
(`src/Kumunita.Core/Identity/IdentityService.cs`, `DeleteAccount...` ~L900–1090)
removes Profile + memberships + Identity row + pseudonymizes audit, but **not**
the resident's authored content docs, **not** their media bytes on the volume,
**not** the `MediaObject` catalog rows.
**Pin (U1) targets:** the exact list of content doc types a resident can own
(owned-by `AuthorId`/`CreatedById`/`OwnerSubjectId`), the byte-store delete
seam on `LocalVolumeFileStore` (`src/Kumunita.Core/Media/LocalVolumeFileStore.cs`),
and the `MediaObject` catalog shape.

- **U1 — Part-0 design pin.** Author `docs/design/prod-hardening-f1-gdpr-deletion.md`
  Part 0: the **closed list** of content docs a deletion must remove per resident
  (grep `AuthorId`/`CreatedById`/`OwnerSubjectId` across `Kumunita.Core`), the
  byte-store delete seam to call, the `MediaObject` row shape, and the pinned
  test names (e.g. `DeleteAccount_RemovesResidentAuthoredDocs`,
  `DeleteAccount_RemovesResidentMediaBytes`,
  `DeleteAccount_RemovesMediaObjectCatalogRows`,
  `DeleteAccount_KeepsOthersContentIntact`). No code, no build.
- **U2 — byte-store delete seam.** Add a **single** delete method to
  `IMediaFileStore` + `LocalVolumeFileStore` that deletes one content-addressed
  byte by id (idempotent; missing file is a no-op). ≤2 files, ≤~120 LOC.
- **U3 — content-doc deletion in the delete lane.** In the existing
  `DeleteAccount...` session, before the Identity delete, delete the target's
  authored docs (the U1 closed list) — one `session.Query<T>().Where(...)` per
  doc type, `Delete` each. Touch only `IdentityService.cs`.
- **U4 — media byte + catalog cleanup.** In the same session, delete the
  target's `MediaObject` rows and call the U2 byte-delete for each. Touch only
  `IdentityService.cs`.
- **U5 — tests.** Add the U1-pinned tests to
  `tests/Kumunita.Core.Tests/` (mirror the `PostgresFixture` seam-test shape).
  Exit: build green + the new tests green. Handoff note: which doc types + the
  byte-delete count.

### F2 — AUTH-02: hash tokens at rest (data exposure) — `code`
**Context (from audit):** `IdentityService.NewVerifyToken` (~L1362) stores
`Token = NewSecret()` (base64) and `FindTokenAsync` (~L235) compares
`t.Token == tokenValue` — cleartext at rest.
**Pin (U1):** the hash function (SHA-256, hex), the exact `FindTokenAsync`
rewrite, and the migration for existing rows.

- **U1 — Part-0 design pin.** Author `docs/design/prod-hardening-f2-token-hash.md`:
  the hash (SHA-256 hex of the token), where the hash is stored (reuse
  `IdentityToken.Token` — no new column), the `FindTokenAsync` comparison
  change, and the **backward-compat rule** for already-minted un-consumed
  tokens (re-hash-or-expire). Pinned tests: `VerifyToken_StoredHashedNotCleartext`,
  `VerifyToken_ConsumedOrExpiredStillRejected`, `VerifyToken_ExistingCleartextRowHandled`.
- **U2 — hash on write + compare on read.** In `IdentityService.cs`: `NewVerifyToken`
  (and any `KindSetup` mint) stores `Sha256Hex(NewSecret())`; `FindTokenAsync`
  compares against `Sha256Hex(tokenValue)`. Add the `Sha256Hex` helper (one
  static, ≤~10 LOC). Only `IdentityService.cs`.
- **U3 — migration/compat for live cleartext rows.** Decide the U1 rule and
  implement it (e.g., on read, if a stored value is not a 64-char hex, treat as
  stale/invalid and reject; no silent accept). Only `IdentityService.cs` or a
  small `IdentityService`-adjacent helper.
- **U4 — tests.** Add the U1-pinned tests. Exit: build green + tests green.
  Handoff note: the hash (algorithm + encoding) + the compat rule chosen.

### F3 — AUTH-03: MFA for `GlobalAdmin` (data exposure) — `code`
**Context:** no MFA; only the EF `TwoFactorEnabled` default column
(`Migrations/20260905190113_InitialIdentity.cs:50`). Enforce for `GlobalAdmin`,
with a recovery path.
**Pin (U1):** the TOTP provider registration, the enroll/verify routes, the
recovery-code store, and the enforcement point (claim/middleware).

- **U1 — Part-0 design pin.** Author `docs/design/prod-hardening-f3-mfa.md`:
  the exact routes (`/settings/security/mfa` GET/POST enable + verify,
  `/settings/security/mfa/recovery`), the storage (reuse `User` EF columns or a
  new small doc — pin the choice), the enforcement point (a middleware after
  `PrivilegedStampMiddleware` for the `GlobalAdmin` role), and pinned tests
  (`GlobalAdminLogin_RequiresMfaChallenge`, `GlobalAdminRecoveryCode_WorksOnce`,
  `NonAdmin_MfaOptional`).
- **U2 — TOTP provider + token store.** Register ASP.NET Identity TOTP
  (`.AddTokenProvider<TotpAuthenticatorTokenProvider>()` in `Program.cs`) + the
  recovery-code store. ≤2 files.
- **U3 — enroll/verify surface.** `/settings/security/mfa` controller actions +
  view model (recovery codes shown once). ≤3 files.
- **U4 — enforcement.** Middleware: `GlobalAdmin` sign-in requires a valid MFA
  challenge (or recovery code); other roles unaffected. ≤1–2 files.
- **U5 — tests.** Add the U1-pinned tests (Web harness per repo memory's
  direct-controller-construction note). Exit: build green + tests green.

### F4 — BKP-02: PITR / WAL archiving (data loss) — `code+ops`
**Context (from audit):** only daily `pg_dump -Fc` (OPS §4); no WAL archiving →
RPO up to 24h.
- **U1 — Part-0 design pin.** Author
  `docs/design/prod-hardening-f4-pitr.md`: the chosen tool (pgBackRest **or**
  WAL-G — pin one), the retention window, and the exact **owner ops steps**
  (this is where the ops part is pinned so the owner executes it verbatim).
- **U2 — code support (if any).** If the chosen tool needs an app-side config
  knob or a health probe field, add it (≤1 file). If pure-ops, U2 = "no code
  needed" recorded.
- **U3 — OPS.md update.** Add the PITR procedure + "last verified" line to
  `docs/OPS.md` §4/§5, and mark the owner-ops step as an **open item** in the
  handoff note (owner runs it on the prod box; not assumed).

### F5 — AUTH-04: session kill-all + invalidation (data exposure/avail) — `code` (dep: F3)
**Context (from audit):** `Program.cs` raises `SecurityStampValidatorOptions`
to 14 days so the validator "can never fire inside a live session"; no
kill-all path; password change doesn't invalidate other sessions.
**Pin (U1):** the kill-all seam (delete-by-subject on a session store, or a
per-user `SecurityStamp` bump), the enforcement, and the recovery interplay with F3.
- **U1 — Part-0 design pin.** Author `docs/design/prod-hardening-f5-sessions.md`:
  the invalidation mechanism (revert the 14-day `ValidationInterval` so
  `UpdateSecurityStampAsync` fires **and** add a `/settings/security/sessions`
  kill-all that stamps + signs out all of the user's active sessions), pinned
  tests (`PasswordChange_InvalidatesOtherSessions`, `KillAllSessions_SignsOutAll`).
- **U2 — lower `ValidationInterval` + kill-all service.** Adjust `Program.cs`
  (one line) + add a `SessionService`-style kill-all (≤2 files).
- **U3 — the kill-all surface.** `/settings/security/sessions` list + "sign out
  everywhere" action (≤3 files).
- **U4 — tests.** Add the U1-pinned tests. Exit: build green + tests green.

### F6 — AUTH-09: argon2id/bcrypt password hash (data exposure) — `code`
**Context (from audit):** ASP.NET Identity default = PBKDF2-SHA256 (strong but
not the three listed). Swap to argon2id/bcrypt at a sane cost.
**Pin (U1):** the chosen hasher (pin argon2id), the package, the registration
line, and the existing-hash compatibility (PBKDF2 rows keep working on read via
the Identity `PasswordHasher` detection — pin this).
- **U1 — Part-0 design pin.** Author `docs/design/prod-hardening-f6-pwhash.md`:
  hasher + cost params, the package (e.g. `BCrypt.Net-Next` or
  `Argon2`/`Microsoft.IdentityModel` — pin one), the `.AddPasswordHasher<T>()`
  registration, the read-compat rule, pinned tests
  (`NewPassword_HashedWithArgon2id`, `LegacyPbkdf2Row_StillVerifies`).
- **U2 — custom `PasswordHasher` + registration.** Add the hasher class +
  `.AddPasswordHasher<...>()` in `Program.cs` (≤2 files).
- **U3 — tests.** Add the U1-pinned tests (round-trip new hash + legacy
  PBKDF2 verify). Exit: build green + tests green.

### F7 — WEB-07: global `nosniff` + `Referrer-Policy` (everything else) — `code`
**Context (from audit):** CSP + `X-Frame-Options` present; missing global
`X-Content-Type-Options` and `Referrer-Policy`.
- **U1 — Part-0 design pin + impl (tiny).** Add both headers to the existing
  CSP middleware in `Program.cs` (the `app.Use(async (context, next) => { … })`
  block that sets `Content-Security-Policy`). ≤1 file, ≤~5 LOC. Pinned test:
  `Response_HasGlobalNosniffAndReferrerPolicy`.
- **U2 — test.** Assert both headers on an anonymous route. Exit: build green +
  test green + `curl -sI` shows both (record the curl output in the handoff note).

### F8 — WEB-10: rate-limit upload lanes (everything else) — `code`
**Context (from audit):** limits exist on login/signup/resend/report/setup/
message/write; the four upload lanes (avatar/content-image/attachment/document)
have no `[EnableRateLimiting]`.
**Pin (U1):** the new policy name (`upload`), its limit/window, and the exact
actions to annotate.
- **U1 — Part-0 design pin + policy.** Add the `upload` window to the
  `AddRateLimiter` block in `Program.cs` (≤~5 LOC), pinned test name
  (`UploadLane_IsRateLimited`).
- **U2 — annotate the upload actions.** Add `[EnableRateLimiting("upload")]` to
  the four upload POST actions (the `UploadGate`-guarding actions). ≤1–2 files.
- **U3 — test.** Assert a 429 after N rapid upload POSTs from one IP.
  Exit: build green + test green.

### F9 — AUTH-15: signup bot challenge + disposable-email blocklist (everything else) — `code`
**Context (from audit):** per-IP signup limit exists (5/hr); no bot challenge,
no disposable-email blocklist.
**Pin (U1):** the blocklist source (a maintained package or a committed list —
pin), the enforcement point in `RegisterCoreAsync`, and the bot-challenge
choice (pin a Turnstile-style seam or record it as owner-ops if a provider key
is required).
- **U1 — Part-0 design pin.** Author `docs/design/prod-hardening-f9-signup.md`:
  the blocklist mechanism, the enforcement point, the pinned test
  (`Signup_DisposableEmail_Rejected`).
- **U2 — disposable-email blocklist.** Add the blocklist check to
  `RegisterCoreAsync` (reject the address). ≤2 files.
- **U3 — bot challenge seam.** Add the challenge hook on the signup form +
  server check (or record as owner-ops if it needs a live provider key — open
  item). ≤2 files.
- **U4 — tests.** Add the U1-pinned test. Exit: build green + tests green.

### F10 — OBS-02: error tracker (Sentry) (everything else) — `code+ops`
**Context (from audit):** no error tracker; only the M31/M32 issue form + logs.
- **U1 — Part-0 design pin.** Author `docs/design/prod-hardening-f10-errtrack.md`:
  the package (`Sentry.AspNetCore` — pin), the DSN env var name
  (`SENTRY_DSN`), the scrubber (no PII/token capture), pinned test
  (`Error_TrackerRegisteredWhenDsnSet`).
- **U2 — Sentry registration + scrubber.** Add the package + `UseSentry` in
  `Program.cs` + a PII-scrubbing filter (≤2 files).
- **U3 — test.** Assert registration is conditional on the DSN env var.
  Exit: build green + test green. **Open item (owner):** set `SENTRY_DSN` on
  the prod app (recorded, not assumed).

### F11 — JOB-02: alert on dead-letter exhaustion (everything else) — `code+ops` (dep: F10)
**Context (from audit):** backoff + dead-letter exist; no alert fires on exhaustion.
- **U1 — Part-0 design pin.** Author
  `docs/design/prod-hardening-f11-dlq-alert.md`: the alert hook (reuse F10's
  Sentry alert channel, or a dedicated `IAlertSink` seam — pin), the trigger
  point (the dead-letter path / `PublishFaultEvents`), pinned test
  (`DeadLetter_FiresAlert`).
- **U2 — the alert seam.** Add an `IAlertSink` (or wire the Wolverine fault path)
  so a dead-lettered message emits one alert. ≤2 files.
- **U3 — test.** Simulate an exhausted handler; assert the alert sink was called.
  Exit: build green + test green. **Open item (owner):** point the sink at the
  F12 human channel.

### F12 — OBS-06: route alerts to a human (everything else) — `code+ops` (dep: F10/F11)
**Context (from audit):** dead-letter/degraded only surfaced on `/health` poll.
- **U1 — Part-0 design pin.** Author `docs/design/prod-hardening-f12-alerts.md`:
  the channel (pin — e.g. the community `SupportEmail` SMTP for critical alerts,
  or the F10 Sentry alert rule), the pinned test (`Alert_RoutedToConfiguredChannel`).
- **U2 — the channel sink.** Implement the `IAlertSink` chosen in F11 against
  this channel (≤2 files).
- **U3 — test + open item.** Assert routing; **owner op** = confirm a real
  recipient is configured on prod (recorded, not assumed).

### F13 — OPS-05: `security.txt` + VDP (everything else) — `code`
**Context (from audit):** `/.well-known/security.txt` → 404 (verified live); no VDP.
- **U1 — Part-0 design pin + route.** Author the VDP text
  (`docs/SECURITY-VDP.md`) + add a `GET /.well-known/security.txt` route that
  returns the RFC 9116 fields (Contact, Policy, Expires — a future date,
  Preferred-Languages, Hints). ≤2 files. Pinned test
  (`SecurityTxt_ReturnsValidFields`).
- **U2 — test.** Assert the route returns 200 with `Contact` + a future `Expires`.
  Exit: build green + test green + `curl` shows the body (record in handoff note).

### F14 — DEP-03: CI test gate on `release` (everything else) — `code+ops`
**Context (from audit):** no `.github/workflows`; tests run locally, not a gate.
- **U1 — Part-0 design pin.** Author `docs/design/prod-hardening-f14-ci.md`:
  the workflow steps (build → run both dlls in-process per the repo test quirk),
  the branch (`release`), the pin (a failing test blocks the merge/deploy).
- **U2 — the workflow file.** Create `.github/workflows/ci.yml`: `dotnet build
  Kumunita.slnx -c Debug`, then `dotnet exec` both test dlls (the working
  path, not `dotnet test`). ≤1 file.
- **U3 — open item (owner).** Wire the deploy (Coolify) to require the green CI
  check on `release` (recorded, not assumed).

### F15 — UPL-04: byte-store lifecycle/retention (everything else) — `code` (dep: F1)
**Context (from audit):** quota/platform-limit gate exists; no lifecycle/auto-delete
of old uploads.
- **U1 — Part-0 design pin.** Author
  `docs/design/prod-hardening-f15-lifecycle.md`: the retention rule, the
  idempotent lock-protected cron shape (JOB-04 precedent: a Postgres advisory
  lock + safe re-run), the re-use of F1's byte-delete seam, pinned test
  (`Lifecycle_DeletesExpiredBytes`).
- **U2 — the retention job.** Add a `MediaLifecycleTick` (Wolverine recurring,
  idempotent, lock-protected) that deletes bytes past the retention rule. ≤2 files.
- **U3 — test.** Plant an expired row + byte; run the tick; assert gone + no
  double-run. Exit: build green + test green.

### F16 — UPL-02: scan uploads before serving (everything else) — `code+ops`
**Context (from audit):** no scan step; files served `attachment` + `nosniff`
(mitigation only).
- **U1 — Part-0 design pin.** Author `docs/design/prod-hardening-f16-scan.md`:
  the scanner (ClamAV via the store, or a provider — pin), the quarantine
  state, the enforcement point (serve lane), pinned test
  (`InfectedUpload_NotServed`).
- **U2 — scan seam.** Add an `IVirusScanner` seam + the quarantine flag on the
  `MediaObject` doc + the serve-lane check. ≤3 files.
- **U3 — test.** A stub scanner returns infected; assert the serve lane 404s.
  Exit: build green + test green. **Open item (owner):** run ClamAV on the prod
  volume (recorded, not assumed).

### F17 — MAIL-03: bounce/complaint suppression (everything else) — `code+ops`
**Context (from audit):** only per-recipient opt-out + sample suppression
(`NotificationService.cs:121`); no bounce/complaint list.
- **U1 — Part-0 design pin.** Author `docs/design/prod-hardening-f17-bounce.md`:
  the suppression doc (`SuppressedAddress`), the gate in the mail-out path
  (before `IMailerStage.StageAsync`), the provider-callback ingest, pinned test
  (`SuppressedAddress_NotMailed`).
- **U2 — the suppression gate + doc.** Add `SuppressedAddress` + the gate in the
  notification/mail-out path (≤3 files).
- **U3 — ingest + test.** Add the callback/ingest + the U1-pinned test.
  Exit: build green + test green. **Open item (owner):** wire the provider's
  bounce/complaint callbacks to the ingest (recorded, not assumed).

### F18 — MAIL-06: bounce/complaint/volume alarms (everything else) — `code+ops` (dep: F12)
**Context (from audit):** no provider metrics/alarms.
- **U1 — Part-0 design pin.** Author `docs/design/prod-hardening-f18-mailalarms.md`:
  the metrics source (provider API/console), the thresholds, the F12 alert
  channel. **Mostly owner-ops** — pin the exact provider steps.
- **U2 — code support (if any).** If a threshold needs an app knob or the
  F12 sink, add it (≤1 file); otherwise record "no code needed."
- **U3 — open item (owner).** Configure the provider alarms (recorded, not
  assumed).

### F19 — TEST-03: E2E suite (signup/verify/login/write) (everything else) — `code`
**Context (from audit):** no E2E; 2211 unit + Testcontainers integration only.
- **U1 — Part-0 design pin.** Author `docs/design/prod-hardening-f19-e2e.md`:
  the runner (Playwright against the compose app), the flows
  (signup → verify (read the token link from Mailpit) → login → one write),
  the pinned spec names.
- **U2 — the E2E harness.** Add the Playwright project + a `signup_login.e2e.ts`
  spec driving the compose app on :5080 (Mailpit for the verify link). ≤3 files.
- **U3 — the write-flow spec.** A `post_write.e2e.ts` spec (create a post, assert
  it appears). ≤1 file.
- **U4 — open item.** Wire the specs into F14's CI (recorded; runs in CI with
  the compose stack).

### F20 — DEP-04: one-step rollback + drilled (availability) — `code+ops` (dep: F4)
**Context (from audit):** rollback is a DB restore (OPS §5); forward-only schema;
no dated rollback record.
- **U1 — Part-0 design pin.** Author `docs/design/prod-hardening-f20-rollback.md`:
  the one-step app rollback (Coolify previous-image), the schema-compat guard,
  the exact owner drill steps + the "last drilled" line.
- **U2 — runbook update.** Add the one-step rollback + drill procedure to
  `docs/OPS.md` (≤1 file). **Open item (owner):** run the drill once on the prod
  box and stamp the date (recorded, not assumed).

### F21 — SRV-04: app↔DB TLS (everything else) — `code+ops`
**Context (from audit):** Npgsql connection string has no `SslMode`.
- **U1 — Part-0 design pin.** Author `docs/design/prod-hardening-f21-db-tls.md`:
  the `SslMode` value (pin `Verify CA` vs `Require`), where the CA is provided,
  the pinned test (a connection-string shape test).
- **U2 — code support.** Add an env knob for `SslMode` + the CA path (read from
  config) so the same image works with/without TLS (≤1 file).
- **U3 — open item (owner).** Enable Postgres TLS on the prod addon + set the
  env + CA (recorded, not assumed).

### F22 — DB-18: demote app role (least-privilege) (everything else) — `code+ops`
**Context (from audit):** app role is the **database owner** (`CREATE DATABASE
kumunita OWNER kumunita`) — deliberate (ADR 0004) but not least-privilege.
**Note:** this conflicts with the ADR 0004 "app role owns the DB so it can
create the `mt`/`identity` schemas at boot" invariant. **U1 must record this
tension and propose the split (migrations under a separate owner role; app
role demoted to DML grants) + the ADR amendment** before any unit proceeds.
- **U1 — Part-0 design pin + ADR amendment.** Author
  `docs/design/prod-hardening-f22-role.md` + propose `docs/adr/<next>-least-privilege-db-role.md`:
  the two-role split, the boot-ownership migration, pinned test
  (`AppRole_CannotDropSchema`).
- **U2 — migration + role split.** Implement the split in the dev-db-init SQL +
  the boot ownership path (≤2 files). **High risk — gate on U1's ADR being
  accepted by the owner first.**
- **U3 — test + open item.** Assert the app role lacks `DROP`/`CREATE SCHEMA`;
  **owner op** = apply the split on the prod Postgres (recorded, not assumed).

---

## Out of scope here (UNKNOWN / owner-attested — tracked separately)
These are **not** in this plan because they are blocked on production access or
owner attestation, not on code:
- **Access-blocked (need the prod box / DNS / provider console):** SRV-01
  (prod DB port), SRV-03 (SSH/auto-upgrades), SRV-09 (disk-fill/log rotation),
  SEC-01 (full-history secret scan — run `gitleaks`), MAIL-02 (sending-domain
  DKIM/SPF/DMARC), OPS-12 (ownership + root MFA), DEP-08 (boot env validation
  is code-doable — **promote to a small code fix if wanted**).
- **Owner-attested (need the owner's word + an artifact):** OPS-04, OPS-16,
  OBS-09, OPS-17, OPS-18.
- **Promotion candidates** (currently N/A at T1, become in-scope when the gate
  turns on): none of the multi-tenant / payments / AI / public-API rows.

## Close-flip contract (when this lane ships)
Per repo memory `shipped-lane-close-flip`, when the close unit runs the SIX
members must flip together: (1) README Roadmap/named-lane row → Done + ADR;
(2) `src/Kumunita.Web/WhatsNew.cs` newest-first entry **+ bump the head-pin in
`tests/Kumunita.Web.Tests/WhatsNewTests.cs`**; (3) `docs/STATUS.md`; (4)
`docs/ARCHITECTURE.md`; (5) `MilestonesTests.cs` only if roadmap order changed
(a hardening lane reuses no milestone letter — do **not** touch it); (6)
`docs/adr/` new ADR(s) + `docs/adr/README.md` index.

**The last unit of the lane** records a `## Summary` table (fix → units → tests
→ open items) in the handoff note, so the owner knows exactly which **open
items** (owner-ops steps) remain before the FAIL rows can be re-verified as PASS.
