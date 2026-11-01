# ADR 0281 — Per-session model + per-session revocation (the `sessions` named lane): a `SessionDoc` in a new `Kumunita.Core.Sessions` bounded context, a `SessionService` 5-method surface, the `kumunita.sid` cookie item, the `SessionRevocationMiddleware`, and the `SecuritySettingsController` extension — on top of F5's whole-account stamp rotation (the two mechanisms coexist and are independent)

Status: Accepted
Date: 2026-11-01
Amends: 0006 (the `C3` "audit always on" / `C4` "strong consistency" / `C5`
"moderator default-OFF" pins are **applied at the owner boundary** — the lane
is per-account, not per-audience — and the **`C3` same-transaction deviation**
documented in §Decision.4 is the one recorded relaxation, satisfied within each
service method + at the request level). Amends: 0004 §B.1 (the `SessionDoc` is
Marten-native, registered on the new `SessionDocTypes` parallel surface —
delta-detected, idempotent, **no seeding**, the ADR 0004 §B.1 rule). Amends:
0278 (none — a parallel context, `Kumunita.Core.Sessions`, with no dependency
on the `GroupMessaging` / `Messaging` contexts; the session data is per-account,
not per-resource). Additive on: 0001-B (the resident's choice is absolute — a
resident may revoke **any** of their own sessions, the platform does not auto-
retain sessions), 0171 (the F5 / prod-hardening `session.kill_all` +
`RevocationStampMiddleware` whole-account kill is **kept as-is** — the F5
`SecurityStamp` column and the `RevocationStampMiddleware` are preserved; this
ADR **adds** a per-session record and a per-session enforcement path), and the
F5 `IIdentityService.KillAllSessionsAsync(string subjectId)` seam (unchanged —
the sessions lane is a **caller** of the kill-all path, not a modifier of it).

## Context

F5 (AUTH-04) of the **prod-hardening** lane shipped the **"Sign out
everywhere"** capability: `POST /settings/security/sessions/kill-all` rotates
the account's `SecurityStamp` (`UserManager.UpdateSecurityStampAsync`) and
appends an `AccessAudit` row, and the per-request
`RevocationStampMiddleware` signs out any session whose cookie
`kumunita.stamp` no longer matches the DB stamp. The result is a **whole-
account kill**: every *other* device is revoked, and the current browser is
signed out (the F5 "local sign-out" behavior). F5 deliberately did **not**
build a per-session model — the existing `Sessions.cshtml` view's own comment
says the platform "cannot enumerate another browser's cookies."

The resident's natural follow-up question — *"which of these sessions is the
phone I no longer have?"* — was **unanswerable**. F5's surface shows "this
session" + a single kill-all control; it cannot *list* sessions or revoke
*one* of them. The platform could revoke the *whole account* (F5) but could
not show *which devices were signed in* or revoke a *single* device.

This ADR settles that question and moves the value chain one arrow:
**whole-account → per-session**. It introduces a **new domain document**
(`SessionDoc`), a **new versioned registration surface** (`SessionDocTypes`),
a **new bounded context** (`Kumunita.Core.Sessions`), and **one new read seam**
(`SessionService`) — mirroring the M3 `PostService` / M2 `DirectoryService`
composition pattern. The F5 `kumunita.stamp` mechanism is **kept as-is**; this
ADR **adds** a per-session record and a per-session enforcement path that runs
*after* the stamp gate (the stamp is the higher-priority whole-account kill;
`SessionDoc.RevokedAt` is the per-session kill — the two coexist and are
independent).

This is the **first session-data milestone**: F5 was stamp-rotation-based (no
per-session record). Because this is a new bounded context + a new document +
a new service, the lane opened with a two-part design doc
(`docs/design/sessions-per-session-revocation.md`) that pinned every seam,
invariant, and test name before any implementing unit ran — the guard against
*Distributed fragmentation* (session-validity logic re-implemented in the
controller, the middleware, *and* the service) and *Accidental integration*
(the "current session is never revoked by the per-row action" rule living in a
comment, not in the invariant list).

## Decision

### 1. The per-session model (a new bounded context, one new document)

`Kumunita.Core.Sessions` is a **new bounded context** with a **single new
domain document** — `SessionDoc` (7 fields: `Id` (a conventional `string`, a
GUID — the session id), `UserId`, `UserAgent`, `Ip`, `CreatedAt`, `LastActive`,
`RevokedAt?` — the last is the only nullable field, the active/revoked
discriminator). It is **Marten-native** (ADR 0004 §B.1 — conventional
`string Id`, no `Identity(...)` override, matching `PostDocTypes` /
`CoreDocTypes` / every other document in the repo), registered on the **new
parallel surface** `SessionDocTypes.Configure(StoreOptions)` (one line —
`opts.Schema.For<SessionDoc>();` — **no** business-key index; the
`(UserId, RevokedAt)` index is a named `SL·D` deferral, not in this lane).
There is **no seeding** (sessions are created on sign-in, never seeded —
ADR 0004 §B.1). `SessionService` is the **only** caller of the `SessionDoc`
store (the ADR 0006-D "the authorization path is unique" analog — here the
*session* path is unique).

### 2. The `SessionService` 5-method surface (the single composition point)

`SessionService` (ctor `(IDocumentStore)`) is the **single** composition point
owning the `SessionDoc` store, with a frozen 5-method surface:
`CreateSessionAsync(userId, userAgent, ip)` → `Task<SessionDoc>` (called from
the `OnSigningIn` handler); `GetSessionsForUserAsync(userId)` →
`Task<IReadOnlyList<SessionDoc>>` (active, non-revoked, most-recent-first);
`RevokeSessionAsync(userId, sessionId)` → `Task` (owner-scoped + idempotent);
`RevokeAllButCurrentAsync(userId, currentSessionId)` → `Task` (the kill-all
extension; the current session is kept); `TouchSessionAsync(sessionId)` →
`Task` (the dumb write — the rate-limit is the **middleware's**, not the
service's). The middleware and the controller are **thin callers** of
`SessionService` (the M3 `PostService` pattern — the controller is route +
authz + shape, never a re-derivation). Writes go through the service's own
`LightweightSession()`; reads use `QuerySession()` / `LoadAsync<T>` (the
codebase idiom). The C-S1·1 pin (the current session is never revoked by the
per-row action) is enforced in the **controller** (the 409); the C-S1·3 pin
(owner-scoped; cross-user = 404) is enforced in the **service** (the owner-
scoped + idempotent no-op); the C-S1·5 rate-limit (the `LastActive` touch ≤
once per 60 s, the `Kumunita:Sessions:TouchIntervalSeconds` config key,
default `60`) is enforced in the **middleware**.

### 3. The `kumunita.sid` cookie item + the `SessionRevocationMiddleware`

A new `AuthenticationProperties.Items` key — **`kumunita.sid`** (the session
id, a GUID string) — is set in the `OnSigningIn` handler (`Program.cs`, the
same place F5 sets `kumunita.stamp`) **alongside** the existing stamp. The
`OnSigningIn` handler is the **only** reliable place to set `Items` (the F5
doc-comment). It is a **cookie item, not a claim** (the F5 `kumunita.stamp`
precedent — the stamp is a non-claim property on the cookie's
`AuthenticationProperties`, not a claim in `ClaimShaping.Build`). The
`CreateSessionAsync` call is wrapped in a `try/catch` (C-S1·4 — sign-in never
fails: on a DB outage the handler logs + continues without the `kumunita.sid`
item; the `kumunita.stamp` line runs outside the try block).

`SessionRevocationMiddleware` (the per-session analog of
`RevocationStampMiddleware`) runs on every authenticated request: read
`kumunita.sid`, load the `SessionDoc` by id, sign out the holder (the **same
response** as `RevocationStampMiddleware`'s stale-stamp path —
`SignInManager.SignOutAsync()` + redirect to `/Account/Login?
error=session-revoked`) if `RevokedAt is not null`, and touch `LastActive`
(rate-limited by `Kumunita:Sessions:TouchIntervalSeconds`) if active + due.
Registered **after** `RevocationStampMiddleware` (the stamp gate is the higher
priority — a stamp-rotated session is revoked regardless of its
`SessionDoc.RevokedAt`). A session with no `kumunita.sid` (pre-lane) or an
unknown `SessionDoc` id **passes through** (the backward-compat rule — the
resident's existing sessions are unaffected; the `kumunita.sid` will be
present on their next sign-in).

### 4. The `SecuritySettingsController` extension (the thin HTTP surface)

The existing `GET /settings/security/sessions` action is **extended** to also
return the session list (the new `SessionListViewModel` — `CurrentSessionId`
+ `Sessions` (`IReadOnlyList<SessionItem>`) + `KillAllEnabled`); a **new**
`POST /settings/security/sessions/{id}/revoke` action (the per-row revoke,
`[ValidateAntiForgeryToken]` + `[EnableRateLimiting("sessions")]` — a new
`sessions` fixed-window policy, 10 / 15 min per IP, the F8 `upload` policy
shape); the existing `POST /settings/security/sessions/kill-all` action is
**extended** to also call `SessionService.RevokeAllButCurrentAsync` (after the
F5 `identity.KillAllSessionsAsync` call and before the F5 local sign-out). The
C-S1·1 pin (409 on `id == currentSessionId`) lives in the **controller**; the
C-S1·3 pin (owner-scoped) lives in the **service**. The `views/SecuritySettings/
Sessions.cshtml` renders the sessions table (the "This session" badge on the
current row; the per-row "Revoke" form on every *other* row) **on top of** the
unchanged F5 "Sign out everywhere" kill-all card, using the `data-confirm`
attribute + `client/lib/confirm.ts` (the codebase's own confirm contract —
SECURITY.md §6 no-inline-script). Five new `settings.sessions.*` `kw-l` keys
(en/de/fr/da) were registered in `KnownTranslationKeys.cs` (U9).

**The C3 same-transaction deviation (documented, not a violation).** The kill-
all extension's two commits — `identity.KillAllSessionsAsync` (Identity/EF
stamp rotation + the `session.kill_all` audit row) and
`RevokeAllButCurrentAsync` (Marten `RevokedAt` writes + the
`session.revoke_all_but_current` audit row) — land in **separate transactions
within the same request** (the two modules do not share a transaction). The
`C3` pin is satisfied *within* each service method (the `AccessAudit` row +
the domain write are one `SaveChangesAsync`) and at the *request* level for the
two-mechanism case. This is the **one** recorded relaxation of ADR 0006 `C3`
in this lane; a unit that finds a way to make the two commits share a
transaction (e.g. a shared `IDocumentSession`) is a drift event in the
**positive** direction (the `C3` pin is strengthened, not weakened) and must
record the change here.

### 5. Coexistence with F5 (the two mechanisms are independent)

The F5 stamp rotation is **unchanged**: the `SecurityStamp` column, the
`RevocationStampMiddleware`, and the F5 `IIdentityService.KillAllSessionsAsync`
seam are all preserved. The **stamp is the whole-account kill** (F5), the
**`SessionDoc.RevokedAt` is the per-session kill** (this lane). The two are
independent: a stamp-rotated session is revoked regardless of its
`SessionDoc.RevokedAt`, and a per-session revoke does not touch the stamp. A
password change still invalidates all sessions via the stamp (F3 + F5),
independently of any per-session `RevokedAt` write.

## Consequences

- The resident can **see** their active sessions (`/settings/security/sessions`)
  and **revoke one** at a time (or all-but-current via the extended kill-all) —
  the resident's *"which device is that?"* question is now answerable.
- `SessionDoc` is the **only** new document (ADR 0004 §B.1); `SessionService`
  is the **only** caller of the `SessionDoc` store (ADR 0006-D); the F5 stamp
  rotation is unchanged (the `SecurityStamp` column + the
  `RevocationStampMiddleware` are preserved).
- **Zero** new `AccessAction` / `AccessVia` / `Decide()` branch — the per-
  session revoke is a new `Action` string (`session.revoke` /
  `session.revoke_all_but_current`), not a new `AccessAction` enum member (the
  ADR 0006-B "audit always on" convention — a new row shape, not a new
  authorization surface). No new `IAuthorizationService` call (the C-S1·3 pin
  is a single `userId == currentSubject` check at the owner boundary).
- **Zero migrations** — the `SessionDoc` is delta-detected / idempotent on the
  new `SessionDocTypes` surface (ADR 0004 §B.1). No seeding (sessions are
  created on sign-in).
- The `Milestones.cs` / README Roadmap / `MilestonesTests.cs` triple is
  **byte-identical** (a **named lane**, not a milestone — the ADR 0013 `GP` /
  ADR 0171 `FDW` precedent; the M4/M5/M6 roadmap stays Events/Projects/
  Portability).
- **Tests:** 13 — the 12 pinned `SessionServiceTests` (`tests/Kumunita.Core.
  Tests/SessionServiceTests.cs`) + 1 kill-all extension Web test
  (`tests/Kumunita.Web.Tests/SessionsKillAllExtTests.cs`). **Gate: 13/13
  green, 0 failed** (the `### Run result (sessions acceptance gate —
  2026-11-01)` section of the design doc — the three-test closed-loop /
  handoff / part-vs-whole gate, evidenced at the seam-test layer; the F5 e2e
  is **recorded, not assumed done** — the `STATUS.md` prod-hardening owner-ops
  item "live E2E + F5 sign-out verifications" owns it).

## Not covered (the 5 `SL·D` deferrals, verbatim)

Each is a **named deferral** with a one-line `SL·D` follow-up candidate:

- **Per-session `DeviceLabel`** — a resident-set label (rename "Chrome on
  work laptop" to "My laptop"). A future `SL·D` lane adds the field + the
  rename surface. *`SL·D` candidate: add `DeviceLabel?` to `SessionDoc` + a
  `PATCH /sessions/{id}/label` action.*
- **Per-session `Kiosk` flag** — a kiosk-mode session property. The kiosk is a
  separate surface, not a session property. *`SL·D` candidate: the kiosk
  surface declares its own session scope, not a `SessionDoc` field.*
- **Admin "sign out all residents"** — a new `GlobalAdmin` action (kill every
  resident's sessions platform-wide). Not a per-resident surface. *`SL·D`
  candidate: a `GlobalAdmin` `POST /admin/sessions/kill-all` action over
  `SessionService.*` with an `Admin` audit row (ADR 0006-E named lane).*
- **Per-group / per-event session scoping** — sessions are per-account, not
  per-resource. *`SL·D` candidate: a `SessionScope` document keyed to a
  resource id, only if a M4/M5 surface genuinely needs it.*
- **A session `Trust` / `Remember me` distinction** — the existing cookie
  already carries a `Persistent` flag; no new surface in this lane. *`SL·D`
  candidate: surface `Persistent` in the list if a resident asks.*

## Deviations of record (the two documented deviations)

1. **The U3 `SchemaBootstrap.cs` entry-read slip (a plan-documentation slip,
   not a drift pause).** The plan's U3 "Entry reads" / "Deliverables" list
   named `Kumunita.Core/Bootstrap/SchemaBootstrap.cs` as a place to add
   `SessionDocTypes.Configure(opts);` "next to the existing
   `CoreDocTypes.Configure` + `PostDocTypes.Configure` calls" there. That is
   **factually wrong about this codebase**: `SchemaBootstrap.cs` contains
   **no** `*DocTypes.Configure` calls at all — it only calls
   `ApplyAllConfiguredChangesToDatabaseAsync()`, which picks up whatever
   surfaces are already registered on the DI store's `StoreOptions` from
   `Program.cs`. This codebase has **exactly one `StoreOptions`**, built once
   in `Program.cs`; `SessionDocTypes.Configure` lives there (as it does for
   every other context — `CoreDocTypes` / `PostDocTypes` / `MediaDocTypes` /
   `PersonalNotesDocTypes` / etc.). U3 added the **single `Program.cs`
   registration** (one line, after the existing `PostDocTypes.Configure`
   line) and did **not** edit `SchemaBootstrap.cs` — following the codebase's
   own documented, repeated precedent. This is recorded here so the ADR is the
   source of the boot-wiring truth, not the plan's mis-stated file list.
2. **The C3 same-transaction request-level deviation (see §Decision.4).**
   The kill-all extension's two commits land in separate transactions within
   the same request (the F5 Identity/EF path and the sessions Marten path do
   not share a transaction); the `C3` pin is satisfied within each service
   method + at the request level. The design doc §2.3 documents this deviation
   and freezes it in §2.6 (U2's frozen-counts blockquote).

## Close flip (U12, 2026-11-01)

- `WhatsNew.cs` head = **`0.84.0`** / `2026-11-01` / ADR 0281 (the required
  sixth close-flip member, the M27 lesson; the `WhatsNewTests` head-pin
  bumped from `0.83.0` / ADR 0280 to `0.84.0` / ADR 0281).
- `ARCHITECTURE.md` §2 — the `Sessions/` line flipped to **`sessions` ✓ live**
  with the gate summary (13/13 green).
- `README.md` — the **`Sessions`** named-lane row added (mirroring the
  **Prod-hardening** named-lane row — "a named lane, not a milestone");
  `Milestones.cs` / `MilestonesTests.cs` **byte-identical**.
- `STATUS.md` — the sessions-lane note (mirroring the prod-hardening named-
  lane paragraph shape).
- The design doc's first 15 lines now carry the `> **Abstract:**` blockquote
  (the `improve-check.ps1` gate (g) — the 10-second "is this the right read"
  gate — resolved; the doc is ~740 lines and over the 400-line ceiling).
- `KnownTranslationKeys.cs` re-baselined **14885 → 14905** in
  `improve-check.ps1` gate (a) by U9 (the 5 `settings.sessions.*` keys ×
  en/de/fr/da = 20 entries); the ADR annotation for that re-baseline is this
  ADR (ADR 0281), recorded in U9's handoff note.
