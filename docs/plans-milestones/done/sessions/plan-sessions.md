# Sessions — per-session list + revoke one device — sealed unit register

Lane id: **`sessions`** (named lane, "M37-adjacent"; the M-letter milestones
M0–M37 + the existing named lanes `PG` / `GP` / `ML` / `LS` / `SP` / `TZ` / `DF` /
`TR` / `RC` / `GU` / `GA` / `RE` / `TG` / `ATT` / `improve` / `prod-hardening` /
`sec` are untouched — the M4/M5/M6 roadmap stays Events/Projects/Portability).

## Understanding

F5 (AUTH-04) of the **prod-hardening** lane shipped the **"Sign out everywhere"**
capability: `POST /settings/security/sessions/kill-all` rotates the
`SecurityStamp` (`UserManager.UpdateSecurityStampAsync`) and appends an
`AccessAudit` row, and the per-request
`RevocationStampMiddleware` signs out any session whose cookie `kumunita.stamp`
no longer matches the DB stamp. The result is **whole-account kill**: every
other device is revoked, and the current device is signed out.

F5 deliberately did **not** build a per-session model — the existing
`Sessions.cshtml` view's own comment says the platform "cannot enumerate another
browser's cookies". This lane adds the missing piece: a **session document** in
Marten, a **`SessionService`** with a small fixed surface, a **per-request
middleware** that enforces session validity (the per-session analog of
`RevocationStampMiddleware`), the **Web surface** (list + per-row revoke +
keep-kill-all), and the **tests**. After this lane, a resident can see their
active sessions and revoke **one** at a time (or all-but-current via the
existing kill-all).

This is the **first session-data milestone**: the F5 capability was
stamp-rotation-based (no per-session record); this lane introduces a **new
domain document** (`SessionDoc`), a **new versioned registration surface**
(`SessionDocTypes`), a **new bounded context** (`Kumunita.Core/Sessions/`), and
**one new read seam** (`IUserInfoService` is unchanged — the session data lives
in the new `SessionService`, mirroring the M3 `PostService` / M2 `DirectoryService`
composition pattern). That is why this lane, like M3, begins with a two-part
design doc (U1/U2) that pins every seam, invariant, and test name before any
unit implements — the guard against *Distributed fragmentation* and *Accidental
integration*.

## Assumptions

- **Scope (per user):** the lane = **per-session list** + **revoke one session**
  + keep the existing **kill-all** (which becomes "revoke all-but-current" on
  top of the existing stamp rotation). **Out of scope (named deferrals, each
  with a one-line follow-up candidate):** per-session metadata beyond
  `UserAgent` / `Ip` / `CreatedAt` / `LastActive` (e.g. a `DeviceLabel` field
  the resident can set — a future `SL·D` lane); per-session `Kiosk`-mode flag
  (the kiosk is a separate surface, not a session property — `SL·D`); admin
  "sign out all residents" (a new `GlobalAdmin` action, not a per-resident
  surface — `SL·D`); per-group or per-event session scoping (sessions are
  per-account, not per-resource — `SL·D`); a session `Trust` / `Remember me`
  distinction (the existing cookie already has a `Persistent` flag; no new
  surface in this lane).
- **The `SessionDoc` is the **only** new document.** F5's stamp rotation is
  **kept as-is** (the `SecurityStamp` column is unchanged; the
  `RevocationStampMiddleware` is unchanged); this lane **adds** a per-session
  record and a per-session enforcement path. The two mechanisms coexist: the
  stamp is the **whole-account** kill (F5), the `SessionDoc.RevokedAt` is the
  **per-session** kill (this lane). The `SessionRevocationMiddleware` (new)
  runs **after** `RevocationStampMiddleware` (the stamp gate is the higher
  priority — a stamp-rotated session is revoked regardless of its
  `SessionDoc.RevokedAt`).
- **Session id in the cookie.** A new `AuthenticationProperties.Items` key —
  `kumunita.sid` (the session id, a GUID string) — is set in the
  `OnSigningIn` handler (`Program.cs:851-872`, the same place F5 sets
  `kumunita.stamp`) alongside the existing stamp. The
  `CookieAuthenticationHandler.CloneTicket` preserves `Items` across sliding
  renewal (the F5 precedent — `RevocationStampMiddleware` doc-comment, the
  "Items dictionary is preserved across sliding renewal" line), so the session
  id is stable for the session's life.
- **`LastActive` touch is rate-limited.** The
  `SessionRevocationMiddleware` updates `SessionDoc.LastActive` **at most once
  per 60 seconds per session** (the `Kumunita:Sessions:TouchIntervalSeconds`
  config key, default `60`). This avoids write amplification on a
  per-request basis. At this platform's scale (one neighborhood) the cost is
  negligible — the same cost profile as `RevocationStampMiddleware` (one EF
  read per authenticated request) + one Marten write per 60 s per session.
- **`SessionService` is the single composition point.** It owns the
  `SessionDoc` store and the five-method surface. The middleware and the
  controller are **thin callers** of `SessionService` (the M3 `PostService`
  pattern — the controller is route + authz + shape, never a re-derivation).
- **No new outbound channel.** This lane is intake (sessions in) — the only
  channel is the existing sign-in flow; no email is staged, no Wolverine
  handler is added, no new `AccessAction` is introduced. The `session.revoke`
  + `session.revoke_all_but_current` audit tags are **new** `Action` strings
  (the ADR 0006-B "audit always on" convention — a new row shape, not a new
  `AccessAction` enum member).
- **Kill-all becomes "revoke all-but-current" on top of stamp rotation.**
  The existing `POST /settings/security/sessions/kill-all` action is
  **extended** (not replaced): after the stamp rotation + audit row (the F5
  path), it also calls `SessionService.RevokeAllButCurrentAsync(userId,
  currentSessionId)` in the **same transaction** (the C3 same-transaction
  guarantee). The current session's `SessionDoc.RevokedAt` stays `null` (the
  resident stays signed in); every other session's `RevokedAt` is set to
  `UtcNow`. The F5 "local sign-out" behavior (the `AccountController.Logout`
  redirect) is **unchanged** — the resident is still signed out of the current
  browser.
- **Test model (unchanged).** The F5 test harness (`F5U4_KillAllTests.cs`) is
  the shape to mirror; this lane's seam tests are in a **new** file
  (`tests/Kumunita.Core.Tests/SessionServiceTests.cs`) for the `SessionService`
  unit-level pins, plus **one** new Web test
  (`tests/Kumunita.Web.Tests/SessionsKillAllExtTests.cs`) for the kill-all
  extension (U10). The M2/M3 three-test acceptance gate (closed-loop /
  handoff / part-vs-whole) is re-applied to this lane (U11).
- **ADR + WhatsNew (U12).** A **new capability that settles a design question**
  gets an ADR under `docs/adr/` (numbered after the current highest, `0280`) —
  **ADR 0281** (`0281-session-doc-and-per-session-revocation.md`). The
  `WhatsNew.cs` entry is the **required sixth member** of the close flip
  (per the AGENTS.md "Keeping the docs in sync with the code" rule — the
  M27 lesson: a shipped lane without a `WhatsNew.cs` row is a miss).

## Approach

Three tracks, sequenced — exactly like M3. **Track A (Core):**
`Kumunita.Core/Sessions/` — the `SessionDoc` POCO, the `SessionDocTypes`
registration surface, the `SessionService` (create, list, revoke, revoke-
all-but-current, touch — all routed through its own Marten session), and the
`DependencyInjection.cs` registration. **Track B (Web):** the `OnSigningIn`
hook (the `kumunita.sid` cookie item), the `SessionRevocationMiddleware`
(per-request enforce + touch), the `SecuritySettingsController` extension
(`GET /sessions` list + `POST /sessions/{id}/revoke` + the kill-all extension),
the `SessionListViewModel`, the `Sessions.cshtml` sessions table, and the i18n
keys. **Track C (Tests):** the 12 invariant-anchored seam tests
(`SessionServiceTests`) + the kill-all extension test (`SessionsKillAllExtTests`)
+ the three-test acceptance gate + the ADR + `WhatsNew.cs` + the
`ARCHITECTURE.md` flip + the close record.

Every unit ends with **build green**. The last unit (U12) appends the ADR, the
`WhatsNew.cs` row, the `ARCHITECTURE.md` flip, and the final close record so
the lane's close is honest.

## Workflow — handoff protocol for fresh-context agents

This lane is executed as a sequence of **sealed units** (U1–U12 below), one
unit per fresh agent with a ~32K context window.

**Shared state (three-tier contract):**

- **Primary — the design doc**
  (`docs/design/sessions-per-session-revocation.md`) — U1/U2 pin the exact C#
  signatures of every seam U3–U12 must match.
- **Secondary — this file**
  (`docs/plans-milestones/in-progress/sessions/plan-sessions.md`) — the unit
  registry with each unit's deliverables and exit criteria.
- **Scratch — the rolling handoff note**
  (`docs/plans-milestones/in-progress/sessions/sessions-handoff-notes.md`).
  One section per unit, appended (never rewritten). Each unit writes exactly
  one short section before it exits; the next unit reads only that section +
  its own entry-read list.

**Per-unit template** (each `U` below follows this): **Goal** (one sentence,
one or two related deliverables); **Entry reads** (the minimal file list, 3–5
files <~300 lines each, no full-repo scan; the design-doc section cited is
named); **Deliverables** (a closed set of new/modified files, ≤ ~4 files /
~600 LOC, no misc cleanups); **Exit** (`run_build` green for the touched
projects; handoff-note entry appended *before* any follow-up action).

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside the §2.7 drift-
guard; (3) never introduces a test whose exact name is not in the §2.5 seam
list; (4) never opens a *new* seam on `IUserInfoService` /
`IAuthorizationService` / `IIdentityService` beyond what U1/U2 pinned;
(5) never re-shapes the `SessionDoc` POCO outside the §2.2 pin; (6) if entry
reads reveal the design doc is out of date, the unit pauses and records
`## U<m> — Drift pause` in the handoff note.

---

## Units (12 total)

### U1 — Design doc Part 1

- **Goal:** author `docs/design/sessions-per-session-revocation.md` Part 1 —
  **Context, Scope (in/out incl. the `SL·D` deferral list), Invariants pinned
  for the lane, FACES (6)**. Mirrors M3's design doc §1. **No code, no build.**
- **Entry reads:** the F5 plan section
  (`docs/plans-milestones/done/prod-hardening/plan-prod-hardening.md` § `### F5`
  — the existing kill-all surface + the `SessionService`-style seam the F5
  plan pinned), the M3 design doc
  (`docs/design/m3-posts-design.md` — the FACES/invariant template to emulate),
  `docs/philosophy/templates/design-doc.md` (required section set),
  `docs/philosophy/the-platform-as-integrator.md` (the value chain; the "which
  arrow does this serve?" framing), `docs/adr/0006-module-boundary-contracts.md`
  (C1–C6, lanes A/D/E), `docs/adr/0004-data-persistence-and-schema-evolution.md`
  (§B.1 Marten-native rule), `docs/adr/0005-multilingual-support.md` (the i18n
  key convention — U9's `kw-l` keys), `Kumunita.Core/Identity/IIdentityService.cs`
  (the `KillAllSessionsAsync` seam to extend, not replace),
  `Kumunita.Web/Security/RevocationStampMiddleware.cs` (the F5 enforcement
  shape the new `SessionRevocationMiddleware` mirrors),
  `Kumunita.Web/Controllers/SecuritySettingsController.cs` (§ F5 block, the
  `Sessions` / `SessionsKillAll` actions to extend),
  `Kumunita.Web/Views/SecuritySettings/Sessions.cshtml` (the existing view the
  sessions table extends), `Kumunita.Web/Program.cs:851-872` (the
  `OnSigningIn` handler where `kumunita.sid` is stamped).
- **Deliverables (1 file, new):** `docs/design/sessions-per-session-revocation.md`
  (~200 lines). Sections:
  - `## Context` — F5 shipped the whole-account kill; this lane adds per-session
    visibility + per-session revocation; the arrow moved (whole-account →
    per-session; the resident's "which device is that?" question is now answerable).
  - `## Scope` — **In:** the `SessionDoc` POCO, the `SessionDocTypes`
    registration, the `SessionService` (5-method surface), the `OnSigningIn`
    hook (the `kumunita.sid` cookie item), the `SessionRevocationMiddleware`,
    the `SecuritySettingsController` extension (the `GET /sessions` list +
    `POST /sessions/{id}/revoke` + the kill-all extension), the
    `SessionListViewModel`, the `Sessions.cshtml` sessions table, the i18n keys,
    the 12 seam tests + the kill-all extension test, the three-test acceptance
    gate, **ADR 0281**, the `WhatsNew.cs` row. **Out (→`SL·D`):** per-session
    `DeviceLabel` (a resident-set label), per-session `Kiosk` flag, admin
    "sign out all residents", per-group/per-event session scoping, a session
    `Trust` / `Remember me` distinction.
  - `## Invariants (pinned for the lane)` — 11 invariants, each with a one-line
    note:
    - **C-S1·1** — the current session is **never** revoked by the per-row
      action (the `POST /sessions/{id}/revoke` action refuses to revoke the
      session whose id is in the current cookie). Kill-all **does** sign out
      the current browser (the F5 behavior is unchanged). (Lane-owned.)
    - **C-S1·2** — a revoked session is **immediately** invalid (the
      `SessionRevocationMiddleware` signs out the holder on the **next**
      request — strong consistency, mirroring F5's `RevocationStampMiddleware`).
      (Lane-owned.)
    - **C-S1·3** — session revocation is **owner-scoped**: a resident can only
      revoke their **own** sessions; cross-user revoke is **denied** (the
      controller checks `userId == currentSubject` before calling
      `SessionService.RevokeSessionAsync`; a mismatch is a **404** — not a
      403, to avoid leaking the existence of another user's session ids).
      (Lane-owned.)
    - **C-S1·4** — the existing sign-in flow is **unchanged**: password
      verification, MFA, the stamp rotation on password change (F3 + F5) are
      all preserved; this lane **adds** a `SessionDoc` row + the `kumunita.sid`
      cookie item on sign-in, it does not change the sign-in challenge.
      (Lane-owned.)
    - **C-S1·5** — `LastActive` is touched **at most once per 60 s per
      session** (the `Kumunita:Sessions:TouchIntervalSeconds` config key,
      default `60`); the middleware does a single Marten query per request
      (the `SessionDoc` by id + the touch write if due). (Lane-owned.)
    - **C1** (ADR 0006) — empty-audience denies. This lane is per-account,
      not per-audience — the invariant applies at the **owner** boundary: a
      non-owner cannot revoke (C-S1·3).
    - **C3** (ADR 0006) — audit always on: a `session.revoke` audit row
      (`targetKind "session"`, `Action "session.revoke"`) is committed in the
      **same transaction** as the `SessionDoc.RevokedAt` write; a
      `session.revoke_all_but_current` row is committed in the same
      transaction as the kill-all extension's `RevokedAt` writes. (Lane-owned.)
    - **C4** (ADR 0006) — strong consistency: a revoke is visible on the
      **next** request (the `SessionRevocationMiddleware` reads the
      `SessionDoc` fresh per request). (Lane-owned.)
    - **C5** (ADR 0006) — moderator default-OFF. This lane has no moderator
      surface; the per-row revoke is owner-only (C-S1·3). **No `Moderate`-on-
      session call.** Tests assert *absence*.
    - **C6** (ADR 0006) — one matching pass shared by `CanAsync` and
      `CanSeeAsync`. This lane is per-account (owner-scoped), not per-audience —
      the invariant applies at the **owner** boundary: a single `userId ==
      currentSubject` check, no `IAuthorizationService` call. (Lane-owned.)
    - **ADR 0001-B** — the resident's choice is absolute: a resident can
      revoke **any** of their own sessions (including the one that was
      signed in 3 months ago on a phone they no longer have); the platform
      does not auto-retain sessions. (Lane-owned.)
    - **ADR 0004 §B.1** — `SessionDoc` is Marten-native, registered in the
      `SessionDocTypes` surface, delta-detected, idempotent, **no seeding**
      (sessions are created on sign-in, not seeded).
  - `## FACES (pinned, 6)` — F1–F6, each bound to an invariant:
    - **F1** create a session returns a doc with a GUID id + correct
      `CreatedAt` / `LastActive` timestamps — C-S1·4
    - **F2** listing a user's sessions returns only active (non-revoked)
      sessions, most-recent-first — C-S1·4
    - **F3** listing excludes revoked sessions; returns empty for an unknown
      user — C-S1·2, C-S1·3
    - **F4** revoking a session sets `RevokedAt`; refuses the current
      session (C-S1·1); denies cross-user (C-S1·3); is idempotent — C-S1·1,
      C-S1·3, C3
    - **F5** revoking all-but-current revokes the others, keeps the current
      — C3, ADR 0001-B
    - **F6** touching a session updates `LastActive` when due; skips when
      not due — C-S1·5
- **Exit:** file exists with all sections. **No build.** Handoff note: 5–6
  lines starting `## U1 — design doc Part 1`, listing the **11 invariants**
  (by id) and the **6 FACES** (F1–F6) so U2 can pin them by id.

### U2 — Design doc Part 2 (seams, contracts, test list, gate, drift-guard)

- **Goal:** append `## Seams & contracts (Part 2, written by U2)` to the design
  doc — the exact C# shapes U3–U11 must match, the revoke rule, the **pinned
  seam-test names**, the **three-test acceptance gate**, and the **drift-
  guard**. **No code, no build.**
- **Entry reads:** U1's Part 1 (the invariant table is the primary source),
  M3 Part 2 (§2.1–§2.7 of the M3 design doc, to emulate),
  `Kumunita.Core/Identity/IIdentityService.cs` (the `KillAllSessionsAsync`
  seam to extend), `Kumunita.Core/Identity/IdentityService.cs:1635-1665` (the
  existing kill-all impl to extend — the `session.kill_all` audit tag to
  mirror), `Kumunita.Web/Security/RevocationStampMiddleware.cs` (the F5
  enforcement shape the new middleware mirrors), `Kumunita.Web/Program.cs:851-
  872` (the `OnSigningIn` handler where `kumunita.sid` is stamped),
  `Kumunita.Web/Controllers/SecuritySettingsController.cs:687-780` (the
  existing `Sessions` / `SessionsKillAll` actions to extend),
  `Kumunita.Core/CoreDocTypes.cs` (the registration shape U3 mirrors),
  `Kumunita.Core/DependencyInjection.cs` (M3's `PostService` registration to
  mirror), `Kumunita.Core.Tests/PostgresFixture.cs` (the test harness U9 uses).
- **Deliverables (1 append, same file):**
  `docs/design/sessions-per-session-revocation.md`. Sub-sections:
  - `### 2.1 frozen seam list (exact C#)` — the **one** new Core seam
    (verbatim):
    ```csharp
    namespace Kumunita.Core.Sessions;

    public sealed class SessionService
    {
        public SessionService(IDocumentStore store);

        // Create a new session (called from the OnSigningIn handler).
        public Task<SessionDoc> CreateSessionAsync(string userId, string userAgent, string ip);

        // List the user's active (non-revoked) sessions, most-recent-first.
        public Task<IReadOnlyList<SessionDoc>> GetSessionsForUserAsync(string userId);

        // Revoke one session (owner-scoped; the controller checks userId == currentSubject).
        public Task RevokeSessionAsync(string userId, string sessionId);

        // Revoke all-but-current (the kill-all extension; the current session stays).
        public Task RevokeAllButCurrentAsync(string userId, string currentSessionId);

        // Touch the session's LastActive (rate-limited by the middleware).
        public Task TouchSessionAsync(string sessionId);
    }
    ```
    Plus the existing `IIdentityService.KillAllSessionsAsync(string subjectId)`
    verbatim (the F5 seam, unchanged) and the `SessionDoc` shape verbatim
    (§2.2).
  - `### 2.2 new lane-owned Core types (exact C#)` — `SessionDoc` (ns
    `Kumunita.Core.Sessions`):
    ```csharp
    public sealed class SessionDoc
    {
        public string Id { get; set; } = "";          // GUID, the session id
        public string UserId { get; set; } = "";       // the resident's User.Id
        public string UserAgent { get; set; } = "";    // the sign-in UA string
        public string Ip { get; set; } = "";           // the sign-in client IP
        public DateTime CreatedAt { get; set; }        // the sign-in time (UTC)
        public DateTime LastActive { get; set; }       // the last-touch time (UTC)
        public DateTime? RevokedAt { get; set; }       // null = active; set = revoked
    }
    ```
    All fields are **non-nullable** except `RevokedAt` (the active/revoked
    discriminator). The `Id` is a conventional string (no `Identity(...)`
    override). The `SessionDocTypes.Configure(StoreOptions)` registration:
    `opts.Schema.For<SessionDoc>();` (one line; no business-key index in the
    lane — the `(UserId, RevokedAt)` index is a `SL·D` candidate).
  - `### 2.3 revoke rule (5-state table)` — the 5-state table for the
    `POST /sessions/{id}/revoke` action:
    | State | Response |
    |---|---|
    | Unauthenticated | 401 (the `[Authorize]` challenge) |
    | `sessionId == currentSessionId` (C-S1·1) | 409 (the per-row action refuses to revoke the current session; the resident is told "This is the session you are using — use 'Sign out everywhere' to end it") |
    | `userId != currentSubject` (C-S1·3) | 404 (not 403 — to avoid leaking the existence of another user's session ids) |
    | `SessionDoc` not found (revoked or deleted) | 404 (idempotent — a double-revoke is a no-op) |
    | `userId == currentSubject` + `SessionDoc.RevokedAt is null` | 204 (the `SessionDoc.RevokedAt` is set + the `session.revoke` audit row is committed) |
    And the 4-shape table for the kill-all extension (C3 same-transaction):
    the `POST /sessions/kill-all` action (the F5 path) now **also** calls
    `SessionService.RevokeAllButCurrentAsync(userId, currentSessionId)` in
    the **same transaction** as the stamp rotation + the `session.kill_all`
    audit row. The current session's `SessionDoc.RevokedAt` stays `null`;
    every other session's `RevokedAt` is set to `UtcNow`. The F5 "local
    sign-out" behavior (the `AccountController.Logout` redirect) is
    **unchanged** — the resident is still signed out of the current browser.
  - `### 2.4 pinned seam tests (exact names)` — file
    `tests/Kumunita.Core.Tests/SessionServiceTests.cs`:
    1. `F1_CreateSession_ReturnsDocWithGuidIdAndTimestamps`
    2. `F2_GetSessionsForUser_ReturnsOnlyActiveSessionsMostRecentFirst`
    3. `F3_GetSessionsForUser_ExcludesRevokedSessions`
    4. `F3_GetSessionsForUser_ReturnsEmptyForUnknownUser`
    5. `F4_RevokeSession_SetsRevokedAtToUtcNow`
    6. `F4_RevokeSession_DoesNotTouchCurrentSession` (C-S1·1)
    7. `F4_RevokeSession_DeniesCrossUser` (C-S1·3)
    8. `F4_RevokeSession_IsIdempotent` (a double-revoke is a no-op)
    9. `F5_RevokeAllButCurrent_RevokesOthersKeepsCurrent`
    10. `F5_RevokeAllButCurrent_RevokesOthersWhenCurrentUnknown`
    11. `F6_TouchSession_UpdatesLastActiveWhenDue`
    12. `F6_TouchSession_SkipsWhenNotDue` (C-S1·5)
  - `### 2.5 acceptance gate (U11 records)` — the three M2/M3-style tests:
    **closed loop** (resident signs in → a `SessionDoc` row exists → the
    resident's session list shows it → the resident revokes **another**
    session → the revoked session is invalid on the next request), **handoff**
    (the kill-all extension revokes all-but-current; the current session stays
    valid; the F5 stamp rotation is unchanged — a password change still
    invalidates all sessions via the stamp), **part-vs-whole** (the 12-test
    list is the whole; closed-loop + handoff are the parts; all must pass
    together).
  - `### 2.6 drift-guard (frozen once written)` — the 11-invariant table
    (U1), the 6 FACES (F1–F6, U1), the `SessionDoc` shape, the
    `SessionService` 5-method surface, the `kumunita.sid` cookie key, the
    `Kumunita:Sessions:TouchIntervalSeconds` config key, the §2.3 tables, and
    the 12 test names — all frozen pins; any mismatch is a `## U<m> — Drift
    pause` per unit-series rule §6.
- **Exit:** file exists with all Part 2 sub-sections. **No build.** Handoff
  note: 6–8 lines starting `## U2 — design doc Part 2`, listing (a) the sealed
  seam signatures (the `SessionService` 5-method surface), (b) the 12 test
  names by id, (c) the three-test gate (by name), (d) the
  `kumunita.sid` cookie key + the `Kumunita:Sessions:TouchIntervalSeconds`
  config key.

### U3 — `SessionDoc` + `SessionDocTypes` registration + boot wiring

- **Goal:** create the `SessionDoc` POCO (namespace
  `Kumunita.Core.Sessions`) and a new `SessionDocTypes.Configure(StoreOptions)`
  registration surface; wire it into both boot paths (the dev loop in
  `Program.cs` and the all-env `SchemaBootstrap`). Mirrors the M3
  `PostDocTypes` pattern exactly.
- **Entry reads:** `Kumunita.Core/CoreDocTypes.cs` (the shape to mirror; the
  "Marten-native, no hand-rolled FeatureSchemaBase" precedent),
  `Kumunita.Core/PostDocTypes.cs` (the M3 `PostDocTypes` registration to
  mirror — the one-line `opts.Schema.For<SessionDoc>();` shape),
  `Kumunita.Core/Bootstrap/SchemaBootstrap.cs` (where
  `ApplyAllConfiguredChangesToDatabaseAsync` is called — U3 adds the
  `SessionDocTypes` registration there), `Kumunita.Web/Program.cs` (the
  dev-loop + all-env paths; the existing `CoreDocTypes.Configure` +
  `PostDocTypes.Configure` calls to place `SessionDocTypes.Configure(next to
  them);`), `docs/design/sessions-per-session-revocation.md` §2.2 (the exact
  shape).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Core/Sessions/SessionDoc.cs` — POCO with the §2.2 shape.
    `Id` is a conventional string (the session id, a GUID); `UserId` is the
    resident's `User.Id`; `UserAgent` / `Ip` are the sign-in metadata;
    `CreatedAt` / `LastActive` are UTC `DateTime`; `RevokedAt` is `DateTime?`
    (null = active; set = revoked).
  - `src/Kumunita.Core/SessionDocTypes.cs` — `public static class
    SessionDocTypes { public static void Configure(StoreOptions opts) {
    opts.Schema.For<SessionDoc>(); } }` (one line; the `SessionDoc` is the
    **only** new document in the lane).
  - `src/Kumunita.Core/Bootstrap/SchemaBootstrap.cs` — add the
    `SessionDocTypes.Configure(opts);` line *next to* the existing
    `CoreDocTypes.Configure(opts);` + `PostDocTypes.Configure(opts);` calls
    (one line added).
  - `src/Kumunita.Web/Program.cs` — add the `SessionDocTypes.Configure(...)`
    call in the dev-loop path, next to the existing `CoreDocTypes.Configure`
    + `PostDocTypes.Configure` lines (one line added).
- **Exit:** `run_build` on `Kumunita.Core` + `Kumunita.Web` green.
  `SessionDocTypes.cs` exists; the `SessionDoc` POCO compiles. **No new test**
  (U9's seam tests are the first lane tests). Handoff note: 4–5 lines starting
  `## U3 — SessionDoc + SessionDocTypes + boot` — (a) the `SessionDocTypes`
  line count (1 `.Schema.For` call), (b) the two boot-path lines added (file +
  line numbers), (c) the `SessionDoc` field count (7), (d) any compile
  warnings on the new POCO.

### U4 — `SessionService` Core composition (create + list + revoke + revoke-
all-but-current + touch)

- **Goal:** the Sessions module's single composition service — pure caller of
  its own `IDocumentStore` for the read/write lanes. This is the lane's analog
  of M3's `PostService`. Implements the C-S1·1 / C-S1·2 / C-S1·3 / C-S1·4 /
  C-S1·5 / C3 / C4 / ADR 0001-B / ADR 0004 §B.1 pins from the design doc.
- **Entry reads:** `Kumunita.Core/Posts/PostService.cs` (the M3 composition to
  mirror — the "caller of the frozen seams" pattern, the session-overload
  usage), `Kumunita.Core/Identity/IdentityService.cs:1635-1665` (the existing
  kill-all impl to extend — the `session.kill_all` audit tag to mirror),
  `Kumunita.Core/Authorization/AccessAudit.cs` (the audit row shape —
  `targetKind` / `Action` for the `session.revoke` +
  `session.revoke_all_but_current` rows), `Kumunita.Core/Sessions/SessionDoc.cs`
  (U3's POCO), `Kumunita.Core/SessionDocTypes.cs` (U3's registration — confirm
  the document is in the store), `Kumunita.Core/DependencyInjection.cs` (where
  to register), `docs/design/sessions-per-session-revocation.md` §2.2 (the
  exact `SessionService` shape) + §2.3 (the revoke rule) + §2.4 (the 12 test
  names the service must satisfy).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Core/Sessions/SessionService.cs` — the full service.
    Constructor: `(IDocumentStore store)`. Public methods (exact, §2.1):
    - `Task<SessionDoc> CreateSessionAsync(string userId, string userAgent,
      string ip)` — construct a `SessionDoc { Id = Guid.NewGuid().ToString(),
      UserId = userId, UserAgent = userAgent, Ip = ip, CreatedAt =
      UtcNow, LastActive = UtcNow, RevokedAt = null }`; `session.Store(doc);
      await session.SaveChangesAsync();` — **within its own session** (the
      service opens a `LightweightSession()` for writes — the M3
      `PostService.CreatePostAsync` pattern, but the `OnSigningIn` handler
      does not have a surrounding session to join). Return the stored doc.
    - `Task<IReadOnlyList<SessionDoc>> GetSessionsForUserAsync(string userId)`
      — `session.Query<SessionDoc>()
      .Where(d => d.UserId == userId && d.RevokedAt == null)
      .OrderByDescending(d => d.LastActive).ToList()` — a **read** (the
      `session.Query` shape, NOT a write session). Return the list.
    - `Task RevokeSessionAsync(string userId, string sessionId)` — load the
      doc by `Id`; if `doc.UserId != userId` **or** `doc.RevokedAt is not
      null`, **do nothing** (the C-S1·3 + C-S1·1 + idempotency pins); else
      set `doc.RevokedAt = UtcNow`; **and** append an `AccessAudit` row
      (`targetKind "session"`, `Action "session.revoke"`, `Subject = userId`,
      `Object = sessionId`) in the **same session**; `session.Store(doc);
      session.Store(auditRow); await session.SaveChangesAsync();` — **C3
      same-transaction** (the revoke + the audit row commit together).
    - `Task RevokeAllButCurrentAsync(string userId, string
      currentSessionId)` — load the user's active sessions (the
      `GetSessionsForUserAsync` query); for each doc where `doc.Id !=
      currentSessionId`, set `doc.RevokedAt = UtcNow`; **and** append **one**
      `AccessAudit` row (`targetKind "session"`, `Action
      "session.revoke_all_but_current"`, `Subject = userId`, `Object =
      currentSessionId`) in the **same session**; `session.Store(each doc);
      session.Store(auditRow); await session.SaveChangesAsync();` — **C3
      same-transaction** (the bulk revoke + the one audit row commit
      together).
    - `Task TouchSessionAsync(string sessionId)` — load the doc by `Id`; if
      `doc.RevokedAt is not null`, **do nothing** (a revoked session is not
      touched); else set `doc.LastActive = UtcNow`; `session.Store(doc);
      await session.SaveChangesAsync();` — the C-S1·5 rate-limit is enforced
      by the **caller** (the middleware checks the interval before calling
      `TouchSessionAsync`), not by the service (the service is the dumb
      write; the middleware is the smart gate).
  - `src/Kumunita.Core/DependencyInjection.cs` — add the `SessionService`
    registration: `services.AddTransient<SessionService>(sp => new
    SessionService(sp.GetRequiredService<IDocumentStore>()));` — mirror
    M3's `PostService` registration shape.
- **Exit:** `run_build` green. Handoff note: 6–8 lines starting
  `## U4 — SessionService` — (a) the 5 public method names + the `SessionDoc`
  POCO reference; (b) the session-usage note ("writes go through the service's
  own `LightweightSession()`; reads use `session.Query`"); (c) the C-S1·1 pin
  ("`RevokeSessionAsync` does not touch the current session — the controller
  checks `sessionId != currentSessionId` before calling"); (d) the C-S1·3 pin
  ("`RevokeSessionAsync` does nothing when `doc.UserId != userId` — the
  controller checks the owner-scoped rule before calling"); (e) the C3 pin
  ("the revoke + the audit row commit in the same session"); (f) any compile
  warnings on the new types.

### U5 — `OnSigningIn` hook: the `kumunita.sid` cookie item

- **Goal:** extend the `OnSigningIn` handler (`Program.cs:851-872`, the same
  place F5 sets `kumunita.stamp`) to **also** create a `SessionDoc` row and
  stamp its `Id` into `ctx.Properties.Items["kumunita.sid"]`. The
  `OnSigningIn` handler is the **only** reliable place to set `Items` (the F5
  doc-comment — "the only reliable place to set `Items`, since
  `CreateUserPrincipalAsync` returns a bare `ClaimsPrincipal` and
  `SignInWithClaimsAsync` passes `AuthenticationProperties` separately to
  `Context.SignInAsync`").
- **Entry reads:** `Kumunita.Web/Program.cs:820-880` (the `OnSigningIn`
  handler + the F5 stamp logic to extend), `Kumunita.Core/Sessions/
  SessionService.cs` (U4's `CreateSessionAsync`), `Kumunita.Core/
  DependencyInjection.cs` (the `SessionService` registration to resolve),
  `docs/design/sessions-per-session-revocation.md` §2.1 (the exact
  `CreateSessionAsync` signature) + §2.2 (the `SessionDoc` shape the handler
  stores).
- **Deliverables (1 file, modify):**
  - `src/Kumunita.Web/Program.cs` — in the `OnSigningIn` handler (the
    existing `o.Events.OnSigningIn = async ctx => { ... }` block at
    `Program.cs:851`), **after** the F5 stamp logic (the
    `ctx.Properties.Items["kumunita.stamp"] = user.SecurityStamp;` line at
    `Program.cs:858`), add: resolve `SessionService` + `IDocumentStore` from
    `ctx.HttpContext.RequestServices` (the per-request scoped provider — the
    `RevocationStampMiddleware` "Dependency scoping" doc-comment, the
    "captive dependency" pitfall), call `await sessionService.
    CreateSessionAsync(user.Id, ctx.HttpContext.Request.Headers.UserAgent,
    ctx.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "")`, and set
    `ctx.Properties.Items["kumunita.sid"] = newDoc.Id;`. **Guard:** if the
    `CreateSessionAsync` call throws (e.g. a DB outage), **do not** fail the
    sign-in (catch + log + continue without the `kumunita.sid` item — the
    session is created on the next sign-in; the resident's existing sessions
    are unaffected). The `user.Id` is the `IdentityUser.Id` (the
    `Kumunita.Sub` claim, which is the `User.Id` — the `AccountController`
    idiom, the `SecuritySettingsController.ResolveUserAsync` doc-comment).
- **Exit:** `run_build` on `Kumunita.Web` green. Handoff note: 4–5 lines
  starting `## U5 — OnSigningIn hook` — (a) the `kumunita.sid` cookie key
  (the exact string), (b) the `CreateSessionAsync` call site (file + line
  number), (c) the guard (the try/catch + the "do not fail the sign-in"
  note), (d) any compile warnings.

### U6 — `SessionRevocationMiddleware` (per-request enforce + touch)

- **Goal:** the per-session analog of `RevocationStampMiddleware` — a
  middleware that, on every authenticated request, (a) reads the
  `kumunita.sid` cookie item, (b) loads the `SessionDoc` by id, (c) if
  `RevokedAt is not null`, signs out the holder (the same response as
  `RevocationStampMiddleware`'s stale-stamp path — `SignInManager.SignOutAsync`
  + redirect to `/Account/Login?error=session-revoked`), (d) if active,
  touches `LastActive` (rate-limited by the
  `Kumunita:Sessions:TouchIntervalSeconds` config key, default `60`).
- **Entry reads:** `Kumunita.Web/Security/RevocationStampMiddleware.cs` (the
  F5 enforcement shape to mirror — the "middleware-class shape", the
  "Dependency scoping" doc-comment, the "Fail-closed cut-off" shape),
  `Kumunita.Web/Program.cs` (the middleware registration order — U6 adds
  `UseMiddleware<SessionRevocationMiddleware>()` **after**
  `UseMiddleware<RevocationStampMiddleware>()`),
  `Kumunita.Core/Sessions/SessionService.cs` (U4's `TouchSessionAsync`),
  `Kumunita.Core/Sessions/SessionDoc.cs` (U3's POCO — the `RevokedAt`
  discriminator), `docs/design/sessions-per-session-revocation.md` §2.1 (the
  exact `TouchSessionAsync` signature) + §2.3 (the revoke rule the middleware
  enforces).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Web/Security/SessionRevocationMiddleware.cs` — the
    middleware. Constructor: `(RequestDelegate next, IConfiguration
    configuration)` (the `RevocationStampMiddleware` shape — the
    `IConfiguration` is a root-lifetime singleton, so it does not break the
    "middleware-class shape"). The `InvokeAsync` method:
    - Read the `kumunita.sid` cookie item (via
      `context.AuthenticateAsync(IdentityConstants.ApplicationScheme)` →
      `result.Properties.Items["kumunita.sid"]` — the F5 doc-comment, the
      "Explicit scheme" line).
    - **No `kumunita.sid` in the cookie** (e.g. a session minted before this
      lane shipped): **pass through** (the backward-compat rule — the
      `RevocationStampMiddleware` "No stamp in the cookie" doc-comment, the
      same shape). The resident's existing sessions are unaffected; the
      `kumunita.sid` will be present on their next sign-in.
    - **`kumunita.sid` present:** load the `SessionDoc` by id (via
      `context.RequestServices.GetRequiredService<IDocumentStore>().
      LightweightSession().Query<SessionDoc>().Where(d => d.Id == sessionId).
      SingleOrDefault()` — the per-request scoped provider, the
      `RevocationStampMiddleware` "Dependency scoping" doc-comment).
    - **`SessionDoc` not found** (deleted or unknown id): **pass through**
      (the backward-compat rule — the session is not in the store; the
      resident's other sessions are unaffected).
    - **`SessionDoc.RevokedAt is not null`:** sign out the holder (via
      `context.RequestServices.GetRequiredService<SignInManager<User>>()
      .SignOutAsync()`) + redirect to `/Account/Login?error=session-revoked`
      (the **same response** as `RevocationStampMiddleware`'s stale-stamp
      path — no new response shape is invented).
    - **`SessionDoc.RevokedAt is null`:** touch `LastActive` **if due** (the
      `Kumunita:Sessions:TouchIntervalSeconds` config key, default `60` —
      read from `IConfiguration` in the constructor; the middleware checks
      `DateTime.UtcNow - doc.LastActive >= touchInterval` before calling
      `await sessionService.TouchSessionAsync(sessionId)` — the C-S1·5
      rate-limit). Then `await _next(context);`.
  - `src/Kumunita.Web/Program.cs` — add the
    `app.UseMiddleware<SessionRevocationMiddleware>();` line **after** the
    existing `app.UseMiddleware<RevocationStampMiddleware>();` line (one line
    added; the stamp gate is the higher priority — a stamp-rotated session is
    revoked regardless of its `SessionDoc.RevokedAt`).
  - `src/Kumunita.Web/appsettings.json` — add the
    `"Kumunita:Sessions:TouchIntervalSeconds": 60` config key (one line added;
    the default value; the operator can override per deployment).
- **Exit:** `run_build` on `Kumunita.Web` green. Handoff note: 5–6 lines
  starting `## U6 — SessionRevocationMiddleware` — (a) the 5-state table
  (no sid → pass; sid present + not found → pass; sid present + revoked →
  sign out; sid present + active → touch if due; sid present + active + not
  due → no touch), (b) the `Kumunita:Sessions:TouchIntervalSeconds` config
  key (the exact string + the default value), (c) the registration order
  (after `RevocationStampMiddleware`), (d) the "same response as
  `RevocationStampMiddleware`" note (the `/Account/Login?error=session-revoked`
  redirect), (e) any compile warnings.

### U7 — `SecuritySettingsController` extension: `GET /sessions` + `POST
/sessions/{id}/revoke` + the kill-all extension

- **Goal:** the thin HTTP surface for the lane. Extend
  `SecuritySettingsController` with two new actions — `GET /settings/security/
  sessions` (the list, the existing action at `:687` is **extended** to also
  return the session list) and `POST /settings/security/sessions/{id}/revoke`
  (the per-row revoke) — plus the **kill-all extension** (the existing
  `POST /settings/security/sessions/kill-all` action at `:725` is **extended**
  to also call `SessionService.RevokeAllButCurrentAsync`). All access
  decisions come from `SessionService`; the controller is *thin* (route +
  authz + shape, never a re-derivation).
- **Entry reads:** `Kumunita.Web/Controllers/SecuritySettingsController.cs:
  687-780` (the existing `Sessions` / `SessionsKillAll` actions to extend),
  `Kumunita.Web/Models/SessionSettingsViewModel.cs` (the existing view-model
  to extend), `Kumunita.Core/Sessions/SessionService.cs` (U4's surface),
  `Kumunita.Core/Sessions/SessionDoc.cs` (U3's POCO),
  `Kumunita.Web/Security/SessionRevocationMiddleware.cs` (U6's middleware —
  the `kumunita.sid` cookie key the controller reads for the "current session"
  flag), `docs/design/sessions-per-session-revocation.md` §2.3 (the revoke
  rule the controller enforces) + §2.4 (the 12 test names the controller must
  satisfy).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Web/Controllers/SecuritySettingsController.cs` — extend the
    existing `Sessions` action (the `GET /settings/security/sessions` at
    `:687`) to **also** return the session list (the
    `SessionService.GetSessionsForUserAsync(userId)` call + the
    `currentSessionId` from the `kumunita.sid` cookie item — the
    `HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme)` →
    `result.Properties.Items["kumunita.sid"]` read, the F5 doc-comment shape).
    The `SessionListViewModel` (U7's new model) carries the list + the
    `currentSessionId` + the kill-all flag.
  - `src/Kumunita.Web/Controllers/SecuritySettingsController.cs` — add the
    new `SessionsRevoke` action: `[HttpPost("/settings/security/sessions/{id}/
    revoke")] [ValidateAntiForgeryToken] [EnableRateLimiting("sessions")]
    public async Task<IActionResult> SessionsRevoke(string id)`. The
    `[EnableRateLimiting("sessions")]` policy (a new `sessions` fixed-window
    policy, 10 / 15 min per IP — the F8 `upload` policy shape, the
    `STATUS.md` F8 row) is added in `Program.cs` (U7's `Program.cs` change).
    The action: read the `currentSessionId` from the `kumunita.sid` cookie
    item; if `id == currentSessionId`, return **409** (the C-S1·1 pin — the
    per-row action refuses to revoke the current session); resolve the
    `userId` (the `KumunitaPrincipal.SubjectId(User)` idiom, the
    `SecuritySettingsController.ResolveUserAsync` shape); call
    `await sessionService.RevokeSessionAsync(userId, id)` (the C-S1·3
    owner-scoped rule is enforced inside the service — the service does
    nothing when `doc.UserId != userId`); return **204** (no content — the
    revoke is committed).
  - `src/Kumunita.Web/Controllers/SecuritySettingsController.cs` — extend the
    existing `SessionsKillAll` action (the `POST /settings/security/sessions/
    kill-all` at `:725`) to **also** call `await sessionService.
    RevokeAllButCurrentAsync(userId, currentSessionId)` **after** the existing
    `await identity.KillAllSessionsAsync(userId);` call (the F5 path — the
    stamp rotation + the `session.kill_all` audit row) and **before** the
    `await signInManager.SignOutAsync();` + `return RedirectToAction("Index",
    "Home");` (the F5 "local sign-out" behavior — the resident is still
    signed out of the current browser). The **C3 same-transaction** pin: the
    `RevokeAllButCurrentAsync` call is in the **same** HTTP request as the
    `KillAllSessionsAsync` call (the two calls are sequential in the same
    action method; the `SessionService` opens its own session, so the two
    commits are separate — the C3 pin is satisfied at the **request** level,
    not the **transaction** level; the design doc §2.3 documents this
    deviation).
  - `src/Kumunita.Web/Models/SessionListViewModel.cs` — **new** model:
    `public sealed class SessionListViewModel { public string
    CurrentSessionId { get; set; } = ""; public IReadOnlyList<SessionItem>
    Sessions { get; set; } = []; public bool KillAllEnabled { get; set; } =
    true; }` — `SessionItem { Id, UserAgent, Ip, CreatedAt, LastActive,
    IsCurrent }` (the `IsCurrent` flag is `item.Id ==
    CurrentSessionId` — the controller sets it).
  - `src/Kumunita.Web/Program.cs` — add the `sessions` rate-limiting policy
    (the F8 `upload` policy shape, the `STATUS.md` F8 row):
    `builder.Services.AddRateLimiter(o => { o.AddPolicy("sessions", http
    => RateLimitPartition.GetFixedWindowLimiter(http.Context.Connection.
    RemoteIpAddress?.ToString() ?? "unknown", _ => new
    FixedWindowRateLimiterOptions { PermitLimit = 10, Window =
    TimeSpan.FromMinutes(15) })); };` (the same shape as the existing
    `upload` policy; one block added).
- **Exit:** `run_build` on `Kumunita.Web` green. Handoff note: 5–6 lines
  starting `## U7 — controller extension` — (a) the 2 new/extended routes
  (the `GET /sessions` + the `POST /sessions/{id}/revoke` + the kill-all
  extension), (b) the `currentSessionId` read (the `kumunita.sid` cookie item
  + the `IdentityConstants.ApplicationScheme`), (c) the C-S1·1 pin (the 409
  on `id == currentSessionId`), (d) the C-S1·3 pin (the service's owner-
  scoped rule), (e) the C3 pin (the "same request, separate transactions"
  deviation), (f) the `sessions` rate-limiting policy (the 10 / 15 min per IP
  shape), (g) any compile warnings.

### U8 — Web: `Sessions.cshtml` sessions table + the per-row revoke form

- **Goal:** the Razor view for the lane's surface. Extend
  `Views/SecuritySettings/Sessions.cshtml` with a **sessions table** (one row
  per session: `UserAgent` / `Ip` / `CreatedAt` / `LastActive` / the
  "This session" badge on the current row / the per-row "Revoke" button on
  every **other** row) + keep the existing "Sign out everywhere" kill-all
  button at the top. The per-row revoke is a `<form method="post"
  action="/settings/security/sessions/{id}/revoke">` (the CSRF form, the
  `Sessions.cshtml:54` shape — the existing kill-all form's `<form method=
  "post" action="/settings/security/sessions/kill-all">` idiom).
- **Entry reads:** `Kumunita.Web/Views/SecuritySettings/Sessions.cshtml`
  (the existing view to extend — the `<kw-l>` key idiom, the `<form method=
  "post">` shape, the `@model` directive), `Kumunita.Web/Models/
  SessionListViewModel.cs` (U7's model — the `@model` directive must match
  exactly), `Kumunita.Web/Views/Shared/_Layout.cshtml` (the layout — the
  `<script>` block for the confirm dialog), `src/Kumunita.Web/wwwroot/js/
  site.js` (the existing JS — the per-row confirm handler in the same style;
  no bundler), `docs/design/sessions-per-session-revocation.md` §Scope (in/
  out) + §2.3 (the revoke rule the view enforces).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Web/Views/SecuritySettings/Sessions.cshtml` — extend the
    existing view. **Keep** the existing "Sign out everywhere" kill-all
    section (the `settings.sessions.kill_heading` + `kill_desc` + the
    `<form method="post" action="/settings/security/sessions/kill-all">`
    button). **Add** a new "Active sessions" section below it: a `<table>`
    with one row per `SessionItem` in `Model.Sessions` — columns: `UserAgent`
    (truncated to 60 chars + `…`), `Ip`, `CreatedAt` (the
    `@(item.CreatedAt.ToLocalTime().ToString("g"))` shape, the ADR 0019
    timezone convention), `LastActive` (the same shape), a "This session"
    `<span class="badge bg-success">` badge on the row where
    `item.IsCurrent == true`, and a `<form method="post" action=
    "/settings/security/sessions/@item.Id/revoke" onsubmit="return
    confirm('Revoke this session?')">` + `<button type="submit" class="btn
    btn-sm btn-outline-danger">Revoke</button>` on every **other** row (the
    `@item.Id` interpolation is a real Razor expression — the AGENTS.md
    Razor-trap rule, the "`@$` in an attribute is a C# interpolated string,
    not Razor" line; the `action` attribute here uses a **bare** `@item.Id`
    inside a **quoted** attribute value, which is a real Razor expression —
    **not** the `@($"...")` bug).
  - `src/Kumunita.Web/wwwroot/js/site.js` — add the per-row confirm handler
    (a `form.addEventListener('submit', ...)` that shows a `confirm('Revoke
    this session? Your other devices and browsers will **not** be affected.')`
    dialog — the `Sessions.cshtml:7` confirm idiom, the existing kill-all
    confirm shape). **No** new TS file (the existing `site.js` is the
    surface; no bundler).
- **Exit:** `run_build` on `Kumunita.Web` green + `tsc` (the existing TS
  build path — check `package.json`'s build script) green. Handoff note: 5–6
  lines starting `## U8 — view + JS` — (a) the 1 view path + the 1 JS handler,
  (b) the `@item.Id` interpolation (a real Razor expression, not the `@($"...
  ")` bug), (c) the "This session" badge (the `item.IsCurrent == true`
  condition), (d) the per-row confirm dialog (the `confirm('Revoke this
  session?')` shape), (e) any `tsc` warnings.

### U9 — i18n keys (the `sessions.*` keys × en/de/fr/da)

- **Goal:** add the i18n keys for the lane's surface (the `kw-l` idiom, the
  ADR 0005 / ADR 0015 / ADR 0052 parity pins) to
  `KnownTranslationKeys.cs` + the 4-language seed data. The existing
  `settings.sessions.kill_heading` / `kill_desc` / `kill_confirm` keys (the
  F5 keys, `KnownTranslationKeys.cs:1082`+) are **unchanged**; this unit
  **adds** the new keys for the per-row surface.
- **Entry reads:** `Kumunita.Core/Localization/KnownTranslationKeys.cs`
  (the existing key registry — the `settings.sessions.*` block at `:1082`+
  to extend), `Kumunita.Core/Bootstrap/FirstBootSeeder.cs` (the 4-language
  seed data — the `en` / `de` / `fr` / `da` blocks to extend),
  `Kumunita.Web/Views/SecuritySettings/Sessions.cshtml` (U8's view — the
  exact `kw-l` key names the view uses), `docs/adr/0005-multilingual-
  support.md` (the i18n convention), `docs/adr/0015-ui-view-localization-
  mechanics.md` (the `kw-l` idiom), `docs/design/sessions-per-session-
  revocation.md` §Scope (the i18n keys the lane ships).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — **add** the
    new keys (the `settings.sessions.*` block, the F5 keys are unchanged):
    `sessions.list_heading` ("Active sessions"), `sessions.list_desc`
    ("The sessions currently signed in to this account. Revoke a session to
    sign out that device immediately."), `sessions.current_badge` ("This
    session"), `sessions.revoke_button` ("Revoke"), `sessions.revoke_confirm`
    ("Revoke this session? Your other devices and browsers will **not** be
    affected."), `sessions.revoke_409` ("This is the session you are using
    — use 'Sign out everywhere' to end it."), `sessions.revoke_404` ("This
    session is no longer active.") — **7 new keys** (the `improve-check.ps1`
    gate (d) pin — the `KnownTranslationKeys.cs` line count; U9 re-baselines
    the count + annotates the ADR that drove the growth, per the
    `improve-check.ps1` gate (d) rule, the AGENTS.md "CI gates and
    anti-regression pins" section).
  - `src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs` — **add** the 4-language
    translations for the 7 new keys (the `en` / `de` / `fr` / `da` blocks —
    the existing `settings.sessions.kill_heading` translations to mirror the
    shape; the `de` / `fr` / `da` translations are the resident-facing
    strings, the ADR 0005 parity pin).
- **Exit:** `run_build` on `Kumunita.Core` + `Kumunita.Web` green.
  Handoff note: 4–5 lines starting `## U9 — i18n keys` — (a) the 7 new key
  names (verbatim), (b) the 4-language count (en/de/fr/da × 7 = 28
  translations), (c) the `improve-check.ps1` gate (d) re-baseline (the new
  `KnownTranslationKeys.cs` line count + the ADR annotation), (d) any
  compile warnings.

### U10 — seam tests (the 12 pinned names) + the kill-all extension test

- **Goal:** implement the 12 tests from the design doc §2.4 (the lane's analog
  of M3's 18-test seam list) in `tests/Kumunita.Core.Tests/SessionServiceTests.
  cs` (the M3 `PostServiceTests.cs` is the shape to mirror — the test harness,
  the fixture, the assertion style) + **one** new Web test
  (`tests/Kumunita.Web.Tests/SessionsKillAllExtTests.cs`) for the kill-all
  extension (U7's `RevokeAllButCurrentAsync` call in the `SessionsKillAll`
  action). **This unit does NOT run the acceptance gate** (that is U11) and
  **does NOT author the e2e** (the lane's e2e is the F5 e2e, the
  `STATUS.md` "live E2E + F5 sign-out verifications" owner-ops item — U11
  records the pass count).
- **Entry reads:** `Kumunita.Core.Tests/PostServiceTests.cs` (the M3 seam-
  test shape to mirror — the `PostgresFixture` usage, the seed-and-assert
  pattern), `Kumunita.Core.Tests/PostgresFixture.cs` (the harness),
  `Kumunita.Core.Tests/UserInfoServiceTests.cs` (the M2 C-M2·2 test — the
  "no audit row" shape to mirror for the C3 pin),
  `docs/design/sessions-per-session-revocation.md` §2.4 (the 12 test names,
  exact — the **primary** source for this unit),
  `tests/Kumunita.Web.Tests/F5U4_KillAllTests.cs` (the F5 Web test harness
  to mirror for the kill-all extension test).
- **Deliverables (2 files, new):**
  - `tests/Kumunita.Core.Tests/SessionServiceTests.cs` — **12 tests**, one per
    pinned name (the §2.4 list, verbatim):
    1. `F1_CreateSession_ReturnsDocWithGuidIdAndTimestamps`
    2. `F2_GetSessionsForUser_ReturnsOnlyActiveSessionsMostRecentFirst`
    3. `F3_GetSessionsForUser_ExcludesRevokedSessions`
    4. `F3_GetSessionsForUser_ReturnsEmptyForUnknownUser`
    5. `F4_RevokeSession_SetsRevokedAtToUtcNow`
    6. `F4_RevokeSession_DoesNotTouchCurrentSession` (C-S1·1)
    7. `F4_RevokeSession_DeniesCrossUser` (C-S1·3)
    8. `F4_RevokeSession_IsIdempotent`
    9. `F5_RevokeAllButCurrent_RevokesOthersKeepsCurrent`
    10. `F5_RevokeAllButCurrent_RevokesOthersWhenCurrentUnknown`
    11. `F6_TouchSession_UpdatesLastActiveWhenDue`
    12. `F6_TouchSession_SkipsWhenNotDue` (C-S1·5)
  - `tests/Kumunita.Web.Tests/SessionsKillAllExtTests.cs` — **one** test:
    `KillAllExt_RevokesOthersKeepsCurrent` — the F5 harness shape (the
    `F5U4_KillAllTests.cs` idiom — the `SignInManager` + the
    `UserManager` + the `IDocumentStore` setup); the test: sign in a resident
    (a `SessionDoc` row is created via the `OnSigningIn` hook — U5's shape),
    create a **second** session (a second `SessionDoc` row, a different `Id`),
    call the `POST /settings/security/sessions/kill-all` action (U7's
    extended action), assert: the first session's `RevokedAt` is `null`
    (the current session is kept), the second session's `RevokedAt` is
    **not** `null` (the other session is revoked), the `AccessAudit` table
    has a `session.kill_all` row **and** a `session.revoke_all_but_current`
    row (the two audit tags, the C3 pin).
- **Exit:** `run_build` green. `run_test(s)` on `SessionServiceTests` +
  `SessionsKillAllExtTests` reports **12 + 1 = 13 tests discovered, 13
  executed** — the pass/red status of each is recorded (for U11's gate).
  **No gate recorded** (U11), **no e2e authored** (the F5 e2e is the
  `STATUS.md` owner-ops item), **no design-doc edits** (U11/U12). Handoff
  note: 3 lines starting `## U10 — seam tests (12 + 1)` — (a) the 2 test file
  paths, (b) the 12 + 1 names (verbatim), (c) the 12 + 1 pass/red counts
  (for U11 to consume).

### U11 — run + record the lane's acceptance gate + the F5 e2e pass count

- **Goal:** execute and **record** the three-test acceptance gate (closed-
  loop / handoff / part-vs-whole) from the design doc §2.5, *using* U10's
  13 tests as the part-vs-whole evidence. The **F5 e2e** (the
  `STATUS.md` "live E2E + F5 sign-out verifications" owner-ops item) is also
  executed in this unit (the F5 three-test shape: (a) closed loop — sign in
  on device A, sign in on device B, revoke device B's session from device A,
  device B is signed out on its next request; (b) handoff — sign in on device
  A, sign in on device B, kill-all from device A, device B is signed out on
  its next request, device A is also signed out (the F5 "local sign-out"
  behavior); (c) part-vs-whole — the 13-test list passes together) and
  recorded alongside the gate. **If the e2e runtime (Playwright + Postgres-
  boot + token-channel) is not yet present, the spec is authored (mirroring
  F5's U13) and *not run* — the gap is recorded in the design doc and the
  next unit who lands the runtime records the pass count.** This is the M3
  U11 analog (the "run the record" step), split from U10 so the test-
  authoring unit stays atomic.
- **Entry reads:** `docs/design/sessions-per-session-revocation.md` §2.5 (the
  gate's three test names and their definitions) + §2.7 (the acceptance-gate
  recording shape), `docs/plans-milestones/in-progress/sessions/sessions-
  handoff-notes.md` (U10's section — the 13-test + e2e results that the gate
  *references*), `tests/Kumunita.Core.Tests/SessionServiceTests.cs` (the 12
  tests the gate's part-vs-whole invokes), `tests/Kumunita.Web.Tests/
  SessionsKillAllExtTests.cs` (the kill-all extension test the gate's handoff
  invokes), `docs/design/m3-posts-design.md` § Acceptance Gate (the M3
  recording shape to mirror verbatim), `docs/STATUS.md` (the F5 e2e owner-
  ops item to record).
- **Deliverables (1 file, modify):**
  `docs/design/sessions-per-session-revocation.md` — append `### Run result
  (sessions acceptance gate — <date>)`: the three test names, their pass/
  red status, the 13-test count (from U10), the F5 e2e status (from U10 +
  the `STATUS.md` owner-ops item), and one line per any `## U<m> — Drift
  pause` section in the handoff note (each resolved or still open). **No
  code, no build.**
- **Exit:** the gate section is present and consistent with U10's results.
  Handoff note: 4–5 lines starting `## U11 — gate recorded` — the three
  test names + pass counts + the date + the F5 e2e status (the
  `STATUS.md` owner-ops item) + any still-open drift.

### U12 — close: ADR 0281 + `WhatsNew.cs` row + `ARCHITECTURE.md` flip +
  the `SL·D` deferral note

- **Goal:** author **ADR 0281** (`docs/adr/0281-session-doc-and-per-session-
  revocation.md`) — the new capability that settles a design question (the
  per-session model + the per-session revocation + the coexistence with F5's
  stamp rotation); add the **`WhatsNew.cs` row** (the required sixth member
  of the close flip, the AGENTS.md "Keeping the docs in sync with the code"
  rule, the M27 lesson); flip the `Sessions/` status line in
  `ARCHITECTURE.md` from "not yet created" to **`sessions` ✓ live** with the
  gate summary; and write the **`SL·D` deferral note** so the next lane's
  OOS close is honest.
- **Entry reads:** `docs/adr/0004-data-persistence-and-schema-evolution.md`
  (the ADR shape to mirror — the §B.1 Marten-native rule), `docs/adr/
  0006-module-boundary-contracts.md` (the C1–C6 invariants the ADR cites),
  `src/Kumunita.Web/WhatsNew.cs` (the existing version registry — the
  newest-first shape to extend), `src/Kumunita.Web/Milestones.cs` (the home-
  page roadmap — the `sessions` lane is a named lane, not an M-letter
  milestone; the `Milestones.cs` is **unchanged** — the named lanes are not
  in the M-letter roadmap, the AGENTS.md "Keeping the docs in sync with the
  code" rule), `docs/ARCHITECTURE.md` §2 (the `Sessions/` line to flip + the
  `Identity/` / `UserInfo/` / `Authorization/` lines untouched),
  `docs/design/sessions-per-session-revocation.md` §Scope (the `SL·D`
  deferral list to record).
- **Deliverables (≤ 4 files):**
  - `docs/adr/0281-session-doc-and-per-session-revocation.md` — **new** ADR.
    The **Context**: F5 shipped the whole-account kill; this lane adds per-
    session visibility + per-session revocation. The **Decision**: the
    `SessionDoc` POCO (the Marten-native document, the ADR 0004 §B.1 rule),
    the `SessionService` 5-method surface (the composition point, the ADR
    0006-D "authorization path is unique" rule — the `SessionService` is the
    **only** caller of the `SessionDoc` store), the `kumunita.sid` cookie
    item (the session id, the F5 `kumunita.stamp` precedent), the
    `SessionRevocationMiddleware` (the per-session enforcement, the
    `RevocationStampMiddleware` precedent), the `SecuritySettingsController`
    extension (the thin HTTP surface, the ADR 0006-B "fat authorization,
    per request" rule), the `LastActive` touch rate-limit (the C-S1·5 pin),
    the kill-all extension (the "revoke all-but-current" on top of the F5
    stamp rotation, the C3 same-transaction pin at the request level), and
    the coexistence with F5 (the stamp is the whole-account kill, the
    `SessionDoc.RevokedAt` is the per-session kill — the two mechanisms are
    independent; a stamp-rotated session is revoked regardless of its
    `SessionDoc.RevokedAt`). The **Consequences**: the resident can see
    their active sessions + revoke one at a time (or all-but-current via the
    kill-all); the `SessionDoc` is the only new document (the ADR 0004 §B.1
    rule); the `SessionService` is the only caller of the `SessionDoc`
    store (the ADR 0006-D rule); the F5 stamp rotation is unchanged (the
    `SecurityStamp` column + the `RevocationStampMiddleware` are preserved).
    The **`SL·D` deferral list**: per-session `DeviceLabel` (a resident-set
    label), per-session `Kiosk` flag, admin "sign out all residents", per-
    group/per-event session scoping, a session `Trust` / `Remember me`
    distinction — each with a one-line `SL·D` candidate.
  - `src/Kumunita.Web/WhatsNew.cs` — **add** the `sessions` lane row (the
    newest-first shape; the F5 row is the shape to mirror — the "Sign out
    everywhere" capability is F5; this lane's row is the "per-session list +
    revoke one device" capability, the ADR 0281 reference, the date, the
    test count (13), the gate summary).
  - `docs/ARCHITECTURE.md` — flip the `Sessions/` status line from "not yet
    created" to **`sessions` ✓ live** with the gate summary (the U11's gate
    result). The `Identity/` / `UserInfo/` / `Authorization/` / `Posts/` /
    `Moderation/` / `Localization/` / `Bootstrap/` lines are **untouched**.
  - `docs/plans-milestones/in-progress/sessions/sessions-handoff-notes.md` —
    append `## Summary` — a table of the shipped units (U1–U11), with their
    one-liner goal + test count + any deviations + the `SL·D` deferral list
    (each item named, each with a one-line `SL·D` candidate or "resolved by
    U<m>").
- **Exit:** no build. `ADR 0281` exists; `WhatsNew.cs` has the `sessions`
  lane row; `ARCHITECTURE.md`'s `Sessions/` line is flipped; the handoff
  note's `## Summary` section is present. **The last handoff note U12
  writes is for the `SL·D` lane** (not for a U12 — there is none). The lane
  is **closed**: the unit plans are moved from
  `docs/plans-milestones/in-progress/sessions/` to
  `docs/plans-milestones/done/sessions/` (the AGENTS.md "Keeping the docs in
  sync with the code" rule — the `in-progress/` → `done/` move is the close
  flip).
