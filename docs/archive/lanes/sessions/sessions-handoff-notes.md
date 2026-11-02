---
id: sessions-sessions-handoff-notes
title: "Sessions Handoff Notes"
tier: archive
kind: handoff
status: archived
audiences: [developer, agent]
lane: sessions
updated: 2026-11-02
---

> **Abstract:** the running handoff note for this lane — the commit-by-commit
> log of what the lane shipped, the seams it created, and the decisions a
> future reader must not re-litigate. See the lane register for the what/why.

# sessions — rolling handoff notes

(Lane: **`sessions`** — per-session list + revoke one device. This file is
the **scratch** tier of the three-tier contract — one section per unit,
appended (never rewritten). Each unit writes exactly one short section
before it exits; the next unit reads only that section + its own entry-
read list. See `plan-sessions.md` for the sealed unit register.)

## U1 — design doc Part 1

Authored `docs/design/sessions-per-session-revocation.md` Part 1 (Context,
Scope incl. the 5 `SL·D` deferrals, the invariants table, the 6 FACES, the
Part-1 drift-guard, and the Part-2 placeholder). **No code, no build.**
Pinned for U2 to consume:

- **12 invariants** (the body is authoritative; the plan § U1 headline says
  "11" — a plan-documentation slip, the *reversed* direction of M3's U1 slip
  [M3 headline "12", body 11]. Resolved identically: body wins, carried to
  U2, **not** a drift pause). Five lane-owned: **C-S1·1** (current session
  never revoked by the per-row action — 409), **C-S1·2** (revoked session
  immediately invalid on the next request), **C-S1·3** (owner-scoped;
  cross-user = 404), **C-S1·4** (sign-in flow unchanged; `OnSigningIn` is the
  only `SessionDoc` create path; the `CreateSessionAsync` guard),
  **C-S1·5** (`LastActive` touched ≤ once per 60 s; the middleware, not the
  service, enforces the rate-limit). Five ADR 0006 caller-pins:
  **C1** (empty-audience — applies at the owner boundary; no audience field),
  **C3** (audit always on — `session.revoke` + `session.revoke_all_but_current`
  rows in the same transaction as their writes), **C4** (strong consistency —
  the revoke is enforced on the next request), **C5** (no moderator surface;
  no `Moderate`-on-session; tests assert absence), **C6** (one matching pass —
  here a single `userId == currentSubject` check, no `IAuthorizationService`
  call). Plus **ADR 0001-B** (resident's choice is absolute — can revoke any
  of their own sessions) and **ADR 0004 §B.1** (`SessionDoc` Marten-native,
  `SessionDocTypes` surface, no seeding).
- **6 FACES (F1–F6)**: **F1** sign-in creates a `SessionDoc` + `kumunita.sid`
  (C-S1·4, ADR 0001-B); **F2** list shows active sessions most-recent-first +
  the "This session" badge (C-S1·4, C-S1·3); **F3** list excludes revoked +
  empty for unknown user (C-S1·2, C-S1·3); **F4** revoke one — sets
  `RevokedAt` + audit, refuses current (409, C-S1·1), denies cross-user (404,
  C-S1·3), idempotent; **F5** kill-all extension — revokes all-but-current,
  keeps the current `SessionDoc` but signs out the current browser, degrades
  to all when `kumunita.sid` is unknown (C3, ADR 0001-B, C-S1·4); **F6**
  `LastActive` touch when due, skip when not due, never touch a revoked
  session (C-S1·5, C-S1·2).
- **Open item for U2 (owner of §2.4 test list):** confirm the invariant count
  is **12** (not the plan's "11") and pin the 12 test names + the
  three-test acceptance gate to the 6 FACES rows above. The FACES table is
  the input; §2.4's test names are the output.
- **Entry reads honored** (no full-repo scan): `plan-prod-hardening.md` § F5
  (the F5 kill-all baseline — `KillAllSessionsAsync` seam + the
  `RevocationStampMiddleware` shape), M3's design doc (the FACES/invariant
  template emulated verbatim), `the-platform-as-integrator.md` (the value
  chain — this lane moves the arrow whole-account → per-session),
  ADR 0006 (C1–C6 + lanes D/E), ADR 0004 §B.1 (Marten-native, no seeding),
  `IIdentityService.cs` (the `KillAllSessionsAsync` seam to *extend*, not
  replace), `RevocationStampMiddleware.cs` (the F5 enforcement shape the new
  middleware mirrors), `Sessions.cshtml` (the existing kill-all surface the
  sessions table extends), the design-doc template + ADR 0005/0015 i18n
  convention. **No drift** — entry reads are consistent with the plan.

## U2 — design doc Part 2

Appended `## Seams & contracts (Part 2, written by U2)` to
`docs/design/sessions-per-session-revocation.md` (§2.0 preambles, §2.1 frozen
seam list, §2.2 new Core types, §2.3 revoke rule, §2.4 pinned test names,
§2.5 acceptance gate, §2.6 drift-guard). **No code, no build.** **No drift**
— entry reads are consistent with the plan and Part 1.

- **Confirmed the invariant count is 12** (not the plan's "11" — the
  *reversed* direction of M3's U1 slip; body wins, recorded as a
  plan-documentation slip, **not** a drift pause). The 12 are: C-S1·1/2/3/4/5
  (lane-owned) + C1/C3/C4/C5/C6 (ADR 0006) + ADR 0001-B + ADR 0004 §B.1.
- **Sealed seam signatures (§2.1):** the `SessionService` 5-method surface —
  `CreateSessionAsync(userId, userAgent, ip)` → `Task<SessionDoc>`,
  `GetSessionsForUserAsync(userId)` → `Task<IReadOnlyList<SessionDoc>>`,
  `RevokeSessionAsync(userId, sessionId)` → `Task`,
  `RevokeAllButCurrentAsync(userId, currentSessionId)` → `Task`,
  `TouchSessionAsync(sessionId)` → `Task`; ctor `(IDocumentStore)`. The F5
  `IIdentityService.KillAllSessionsAsync(string subjectId)` is **unchanged**
  (the sessions lane is a caller, not a modifier — quoted verbatim).
- **New Core types (§2.2):** `SessionDoc` 7-field shape (Id, UserId, UserAgent,
  Ip, CreatedAt, LastActive, `RevokedAt?` — the active/revoked discriminator);
  `SessionDocTypes.Configure` one-line (`opts.Schema.For<SessionDoc>();`, no
  business-key index — `(UserId, RevokedAt)` is an `SL·D` candidate).
- **12 test names by id (§2.4)** — anchored to the 6 FACES: F1 →
  `F1_CreateSession_ReturnsDocWithGuidIdAndTimestamps`; F2 →
  `F2_GetSessionsForUser_ReturnsOnlyActiveSessionsMostRecentFirst`; F3 →
  `F3_GetSessionsForUser_ExcludesRevokedSessions` +
  `F3_GetSessionsForUser_ReturnsEmptyForUnknownUser`; F4 →
  `F4_RevokeSession_SetsRevokedAtToUtcNow`,
  `F4_RevokeSession_DoesNotTouchCurrentSession`,
  `F4_RevokeSession_DeniesCrossUser`, `F4_RevokeSession_IsIdempotent`; F5 →
  `F5_RevokeAllButCurrent_RevokesOthersKeepsCurrent`,
  `F5_RevokeAllButCurrent_RevokesOthersWhenCurrentUnknown`; F6 →
  `F6_TouchSession_UpdatesLastActiveWhenDue`,
  `F6_TouchSession_SkipsWhenNotDue`.
- **Three-test acceptance gate by name (§2.5):** **closed-loop** (sign in →
  `SessionDoc` row → list shows it → revoke another → invalid on next
  request), **handoff** (kill-all revokes all-but-current, keeps the current
  `SessionDoc` but signs out the current browser; F5 stamp rotation unchanged;
  the two mechanisms coexist + are independent), **part-vs-whole** (the
  12-test list + the kill-all extension test must pass together).
- **Two frozen config pins (§2.6):** the `kumunita.sid` cookie item key
  (the exact string — the session id, a GUID string, set in `OnSigningIn`
  alongside `kumunita.stamp`; **not** a claim) + the
  `Kumunita:Sessions:TouchIntervalSeconds` config key (the exact string,
  default `60`; the rate-limit is the middleware's, not the service's).
- **C3 same-transaction deviation (§2.3, documented, not a violation):** the
  kill-all extension's `KillAllSessionsAsync` (Identity/EF) +
  `RevokeAllButCurrentAsync` (Marten) commits are in **separate transactions
  within the same request** (the two modules do not share a transaction); the
  C3 pin is satisfied *within* each service method + at the *request* level
  for the two-mechanism case. ADR 0281 (U12) records this.

## U3 — SessionDoc + SessionDocTypes + boot

Created `SessionDoc` (ns `Kumunita.Core.Sessions`) + `SessionDocTypes` (ns
`Kumunita.Core`), wired into `Program.cs`'s `AddMarten` block. **Build
green: 0 warnings, 0 errors** (`Kumunita.Core` + `Kumunita.Web` + both test
projects, `dotnet build Kumunita.slnx -c Debug`, `EXIT=0`). No new test (U10's
seam tests are the first lane tests, per the plan).

- **`SessionDoc` field count (7)** — `Id`, `UserId`, `UserAgent`, `Ip`,
  `CreatedAt`, `LastActive`, `RevokedAt?` (the last is the only nullable
  field, the active/revoked discriminator; matches Part 2 §2.2 verbatim).
  `Id` is a conventional `string` — **no** `Identity(...)` override (the
  `PostDocTypes` convention, `CoreDocTypes.cs`/`PostDocTypes.cs` precedent —
  every other document in this repo, M3 `Post`/`PostReply` through M37
  `PersonalNote`/`NoteFolder`, uses the conventional `string Id`; only a
  handful like `Profile`/`GuestAccess`/`GuardianTimeLimitSchedule` pin a
  non-default identity, and `SessionDoc` is not one of them).
- **`SessionDocTypes.Configure` line count (1)** — exactly
  `opts.Schema.For<SessionDoc>();` — no business-key index (the
  `(UserId, RevokedAt)` index is a named `SL·D` deferral, not a U3
  deliverable, per the plan's U3 + Part 2 §2.2 pin).
- **Boot wiring — ONE line added, in `Program.cs` only (not in
  `SchemaBootstrap.cs` — see the drift note below).** The
  `SessionDocTypes.Configure(opts);` call was added **immediately after** the
  existing `PostDocTypes.Configure(opts);` line (the plan's "next to
  `CoreDocTypes.Configure` + `PostDocTypes.Configure`" instruction), with a
  block comment mirroring the M3/M37/M31/M35 precedent comments already in
  that block (the `SessionDoc` rationale + the ADR 0004 §B.1 parallel-surface
  note + the "SchemaBootstrap.cs does not call any `*DocTypes` surface — the
  `Program.cs` registration is the only boot wiring" line, quoted directly
  from the existing M37/`PersonalNotesDocTypes` comment two blocks below it
  — which is the codebase's own documented statement of this exact fact).
  `Program.cs` is the **only** file in this repo's codebase that calls any
  `*DocTypes.Configure` on the `AddMarten` block's `StoreOptions` (verified
  by grep across `src/` — the only matches for `PostDocTypes.Configure` /
  `CoreDocTypes.Configure` outside `tests/` and `docs/` are the definitions
  themselves and the single `Program.cs` call site).
- **Drift note — the plan's U3 "Entry reads" / "Deliverables" list names
  `Kumunita.Core/Bootstrap/SchemaBootstrap.cs` as a place to add
  `SessionDocTypes.Configure(opts);` "next to the existing
  `CoreDocTypes.Configure` + `PostDocTypes.Configure` calls" there. That is
  **factually wrong about this codebase**: `SchemaBootstrap.cs` contains
  **no** `*DocTypes.Configure` calls at all (confirmed by a targeted grep —
  zero matches for `DocTypes` in that file; it only ever calls
  `store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync()`, which
  picks up whatever surfaces are already registered on the DI store's
  `StoreOptions` from `Program.cs`). This is not a `## U3 — Drift pause`
  blocker (the plan's intent — "register `SessionDocTypes` so the
  `SessionDoc` doc is visible to Marten on both the dev-loop and all-env boot
  paths" — is fully satisfied by the single `Program.cs` registration, since
  **both** the dev-only `ApplyAllDatabaseChangesOnStartup` loop and the
  `SchemaBootstrap` versioned boot read from the **same** DI-registered
  `IDocumentStore`/`StoreOptions`; there is only one `StoreOptions` in this
  app, built once in `Program.cs`). I followed the codebase's own
  documented, repeated precedent (M37's `PersonalNotesDocTypes`, M31's
  `ErrorReportDocTypes`, M35's `KioskDocTypes`, and every other bounded
  context in that block) — one `*DocTypes.Configure` call in `Program.cs` —
  rather than inventing a second, `SchemaBootstrap.cs`-local registration
  call that no other context in this repo uses and that would require
  `SchemaBootstrap` to hold its own separate `StoreOptions` (it does not).
  This is recorded here so U12 (the ADR 0281 author) can note the plan's
  mis-stated file list, not as a design decision U3 made — the design
  decision (one `Program.cs` registration) was already settled for every
  prior context and is not U3's to change.
- **Compile warnings:** none (build output: "0 Warning(s) / 0 Error(s)").
- **Files touched (exactly the U3 Deliverables' intended surface):**
  `src/Kumunita.Core/Sessions/SessionDoc.cs` (new),
  `src/Kumunita.Core/SessionDocTypes.cs` (new),
  `src/Kumunita.Web/Program.cs` (one `*DocTypes.Configure` call + a block
  comment added, immediately after the existing `PostDocTypes.Configure(opts);`
  line, ~`Program.cs:140`). No other file touched; no `SchemaBootstrap.cs`
  edit (per the drift note above — not needed, and inconsistent with the
  codebase's actual architecture); no test file (U10's lane).

## U4 — SessionService

Created `src/Kumunita.Core/Sessions/SessionService.cs` (ns
`Kumunita.Core.Sessions`) and registered it in `src/Kumunita.Core/
DependencyInjection.cs`. **Build green: 0 errors** (`Kumunita.Core`:
4 pre-existing warnings in `UserPortabilityService.cs`, none from new code;
`Kumunita.Web`: 0 warnings, 0 errors). No new test (U10's seam tests are
the first lane tests, per the plan).

- **5 public methods (Part 2 §2.1, verbatim):** `CreateSessionAsync(userId,
  userAgent, ip)` → `Task<SessionDoc>`; `GetSessionsForUserAsync(userId)` →
  `Task<IReadOnlyList<SessionDoc>>`; `RevokeSessionAsync(userId, sessionId)`
  → `Task`; `RevokeAllButCurrentAsync(userId, currentSessionId)` → `Task`;
  `TouchSessionAsync(sessionId)` → `Task`. Ctor: `(IDocumentStore store)`.
- **Session-usage note:** reads open `QuerySession()` (no write session);
  writes open `LightweightSession()` + `SaveChangesAsync()`. Single-doc load
  uses `session.LoadAsync<SessionDoc>(id)` (the codebase idiom — `PostService`,
  `AnnouncementService`, `BookmarkService` all use `LoadAsync`; `FindAsync`
  does not exist on this Marten version — caught at build, fixed before exit).
- **C-S1·1 pin (current session never revoked by the per-row action):**
  enforced in the **controller** (U7) — the controller returns 409 when
  `id == currentSessionId` before calling the service. The service itself
  does not know the current session id; its job is the owner-scoped +
  idempotent rule.
- **C-S1·3 pin (owner-scoped; cross-user = 404):** enforced in the **service**
  — `RevokeSessionAsync` returns without writing when
  `doc.UserId != userId` (the controller's 404 response is produced by U7's
  thin HTTP layer; the service's no-op is the domain-level guard).
- **C3 pin (audit + domain write in the same session):**
  `RevokeSessionAsync` appends `AccessAudit { Action = "session.revoke",
  TargetKind = "session", TargetId = sessionId, ActorId = userId,
  Via = Owner, Outcome = Allow }` in the same `LightweightSession` as the
  `RevokedAt` write — one `SaveChangesAsync`. `RevokeAllButCurrentAsync`
  appends **one** `AccessAudit { Action = "session.revoke_all_but_current",
  TargetKind = "session", TargetId = currentSessionId }` after setting
  `RevokedAt` on all-but-current docs — same session, one `SaveChangesAsync`.
- **C-S1·5 pin (rate-limit is the middleware's, not the service's):**
  `TouchSessionAsync` is the dumb write — it updates `LastActive` when
  called; it does **not** check the 60-second interval. The
  `SessionRevocationMiddleware` (U6) reads the interval from
  `Kumunita:Sessions:TouchIntervalSeconds` and only calls
  `TouchSessionAsync` when `UtcNow - doc.LastActive >= interval`.
  `TouchSessionAsync` does nothing if the doc is not found or already revoked
  (C-S1·2: a revoked session is not touched).
- **Marten API note for U10/U11 test authors:** single-doc load is
  `session.LoadAsync<T>(id)` (not `FindAsync`); list query is
  `session.Query<T>().Where(…).ToListAsync()` — both on
  `LightweightSession` / `QuerySession` as appropriate.
- **Compile warnings (new code):** none (all 4 warnings are pre-existing in
  `UserPortabilityService.cs`).
- **Files touched (exactly the U4 Deliverables):**
  `src/Kumunita.Core/Sessions/SessionService.cs` (new),
  `src/Kumunita.Core/DependencyInjection.cs` (one `AddTransient` registration
  block added, immediately before `return services;`). No other file touched;
  no test file (U10's lane).

## U5 — OnSigningIn hook

Extended the `o.Events.OnSigningIn` handler (the app-cookie `IdentityConstants.ApplicationScheme` block in `Program.cs`, the same place F5 sets `kumunita.stamp`) to also create a `SessionDoc` row and stamp its id into the cookie `Items`. **Build green: 0 warnings, 0 errors** (`dotnet build Kumunita.slnx -c Debug`, `EXIT=0`). No new test (U10's seam tests are the first lane tests, per the plan).

- **`kumunita.sid` cookie key (exact string):** `kumunita.sid` — set at `Program.cs:884` (`ctx.Properties.Items["kumunita.sid"] = newDoc.Id;`), immediately after the existing `kumunita.stamp` line. A **cookie item, not a claim** (invariant set B preserved — no new claim type).
- **`CreateSessionAsync` call site:** `Program.cs:883` — `var newDoc = await sessionService.CreateSessionAsync(user.Id, userAgent, ip);` where `userAgent = ctx.HttpContext.Request.Headers.UserAgent.ToString()` and `ip = ctx.HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""`. `user.Id` is the `IdentityUser.Id` (the `Kumunita.Sub` claim value, the `SecuritySettingsController.ResolveUserAsync` idiom — same `user` object resolved via `UserManager.FindByIdAsync(subject)` already in the handler).
- **Guard shape (C-S1·4 — sign-in never fails):** the create + stamp are wrapped in a `try/catch` block. On any `Exception`, the handler logs a `LogWarning` (via `ILoggerFactory.CreateLogger("Kumunita.Sessions")`, resolved from `ctx.HttpContext.RequestServices` — the per-request scoped provider, the `RevocationStampMiddleware` "Dependency scoping" idiom) with the exception + `user.Id`, then **continues without the `kumunita.sid` item**. The `kumunita.stamp` line is **outside** the try/catch (it ran before the try block) so a DB outage on `CreateSessionAsync` does not affect the F5 stamp path.
- **`SessionService` resolution:** `ctx.HttpContext!.RequestServices.GetRequiredService<Kumunita.Core.Sessions.SessionService>()` — full-qualified namespace, no new `using` added to `Program.cs` (the file's `using` block already imports `Kumunita.Core` but not `Kumunita.Core.Sessions`; the full-qualified reference avoids a `using` change, which is outside U5's one-file deliverable).
- **The `TwoFactorUserIdScheme` `OnSigningIn` handler is **unchanged**:** the pending leg-1 cookie is not a full session (no app cookie is minted yet); the `SessionDoc` is created only on the full sign-in (the `ApplicationScheme` handler), matching the design doc's "the `OnSigningIn` handler is the **only** reliable place to set `Items`" note (F5's doc-comment, which names the app-cookie handler specifically).
- **Compile warnings (new code):** none.
- **Files touched (exactly the U5 Deliverables):** `src/Kumunita.Web/Program.cs` (the `OnSigningIn` handler for `IdentityConstants.ApplicationScheme` extended — one `try/catch` block added after the `kumunita.stamp` line, ~`Program.cs:874-892`). No other file touched; no new `using`; no test file (U10's lane).

## U6 — SessionRevocationMiddleware

Created `src/Kumunita.Web/Security/SessionRevocationMiddleware.cs` (the per-session enforcement middleware), registered it in `Program.cs` after `RevocationStampMiddleware`, and added the `Kumunita:Sessions:TouchIntervalSeconds` config key to `appsettings.json`. **Build green: 0 warnings, 0 errors** (`dotnet build Kumunita.slnx -c Debug`, `EXIT=0`). No new test (U10's seam tests are the first lane tests, per the plan).

- **5-state table:** (1) anonymous → pass; (2) authenticated + no `kumunita.sid` in cookie → pass (backward-compat, the `RevocationStampMiddleware` "No stamp in the cookie" rule); (3) `kumunita.sid` present + `SessionDoc` not found → pass (unknown id, not a denial); (4) `kumunita.sid` present + `SessionDoc.RevokedAt is not null` → sign out via `SignInManager<User>.SignOutAsync()` + redirect to `/Account/Login?error=session-revoked` (the **same response shape** as `RevocationStampMiddleware`'s stale-stamp path — no new shape); (5) `kumunita.sid` present + active (`RevokedAt is null`) → if `UtcNow - doc.LastActive >= _touchInterval`, call `SessionService.TouchSessionAsync(sessionId)` (wrapped in try/catch — C-M13·5 pattern, `UsageCaptureMiddleware` precedent — a touch never fails the request); then `_next(context)`.
- **`Kumunita:Sessions:TouchIntervalSeconds` config key (exact string + default):** `"Kumunita:Sessions:TouchIntervalSeconds": 60` in `appsettings.json`. The constructor reads it via `configuration["Kumunita:Sessions:TouchIntervalSeconds"]`; an unparseable or non-positive value falls back to 60 s (the rate-limit degrades to the safe default rather than disabling itself).
- **Registration order (relative to `RevocationStampMiddleware`):** `app.UseMiddleware<SessionRevocationMiddleware>();` is registered **immediately after** `app.UseMiddleware<RevocationStampMiddleware>();` (the stamp gate is the higher priority — a stamp-rotated session is revoked regardless of its `SessionDoc.RevokedAt`; the two mechanisms coexist and are independent). It is **before** `AdminTwoFactorMiddleware`, `TimeLimitMiddleware`, `PrivilegedStampMiddleware`, and `UsageCaptureMiddleware`.
- **The "same response as `RevocationStampMiddleware`" redirect shape:** `context.Response.Redirect("/Account/Login?error=session-revoked"); await context.Response.CompleteAsync(); return;` — the exact same three-line pattern as `RevocationStampMiddleware`'s stale-stamp and fail-closed paths (the `SignInManager.SignOutAsync()` call resolves from `context.RequestServices` — the per-request scoped provider, the "Dependency scoping" idiom).
- **`SessionDoc` load (read, no write session):** `await using var store = requestServices.GetRequiredService<IDocumentStore>(); await using var querySession = store.QuerySession(); var doc = await querySession.LoadAsync<SessionDoc>(sessionId);` — the codebase idiom (the `SessionService.GetSessionsForUserAsync` read shape; `LoadAsync` is the single-doc idiom, confirmed by U4's Marten API note).
- **`SessionService` resolution (transient, per-request):** `requestServices.GetRequiredService<SessionService>().TouchSessionAsync(sessionId)` — resolved from `context.RequestServices` (the per-request scoped provider), not constructor-injected (the `RevocationStampMiddleware` "Dependency scoping" idiom; the "captive dependency" pitfall).
- **Compile warnings (new code):** none.
- **Files touched (exactly the U6 Deliverables):** `src/Kumunita.Web/Security/SessionRevocationMiddleware.cs` (new, ~165 lines), `src/Kumunita.Web/Program.cs` (one `app.UseMiddleware<SessionRevocationMiddleware>();` line + a block comment added, immediately after the existing `app.UseMiddleware<RevocationStampMiddleware>();` line, ~`Program.cs:1108-1121`), `src/Kumunita.Web/appsettings.json` (one `"Kumunita": { "Sessions": { "TouchIntervalSeconds": 60 } }` block added). No other file touched; no new test (U10's lane).

## U7 — controller extension

Extended `SecuritySettingsController` with the thin HTTP surface for the lane (list + per-row revoke + kill-all extension) + the `sessions` rate-limit policy + the new `SessionListViewModel`. **Build green: 0 warnings, 0 errors** (`Kumunita.Core` + `Kumunita.Web`, `dotnet build src/Kumunita.Web/Kumunita.Web.csproj -c Debug`, `Build succeeded in 4.0s`). No new test (U10's seam tests are the first lane tests, per the plan).

- **Routes (2 extended + 1 new):** (a) **extended** `GET /settings/security/sessions` (the F5 list action, now `async`) — resolves `currentSessionId` from the `kumunita.sid` cookie item, calls `SessionService.GetSessionsForUserAsync(subject)`, and returns the **new** `SessionListViewModel { CurrentSessionId; Sessions (IReadOnlyList<SessionItem>); KillAllEnabled }` with each `SessionItem.IsCurrent = item.Id == currentSessionId`; (b) **new** `POST /settings/security/sessions/{id}/revoke` → `SessionsRevoke(string id)` (`[ValidateAntiForgeryToken]` + `[EnableRateLimiting("sessions")]`) — C-S1·1 409 when `id == currentSessionId`, else `SessionService.RevokeSessionAsync(subject, id)` + 204; (c) **extended** `POST /settings/security/sessions/kill-all` (the F5 action) — after the existing `identity.KillAllSessionsAsync(subject)` and **before** `signInManager.SignOutAsync()` + redirect, added `await sessionService.RevokeAllButCurrentAsync(subject, currentSessionId)`.
- **`currentSessionId` read shape:** a new private helper `CurrentSessionIdAsync()` — `await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme)` → `result.Properties.Items.TryGetValue("kumunita.sid", out var sid)` (the **`TryGetValue`** form, not the indexer — a pre-lane session / a sign-in whose `CreateSessionAsync` guard tripped has no key, so the missing key is a clean `null`, not a `KeyNotFoundException`; the `ApplicationScheme` is the app cookie the sid is stamped into, the F5 `RevocationStampMiddleware` "Explicit scheme" idiom). Returns `null` when absent; callers coalesce to `""`.
- **C-S1·1 pin (409 on the current session):** `SessionsRevoke` compares `id == currentSessionId` **before** calling the service and returns `Conflict()` (409). The comparison is null-safe — a missing sid on this browser means no session is "current" here, so the revoke proceeds for any other id. The service itself does not know the current-session id (U4's note) — the guard lives in the controller.
- **C-S1·3 pin (owner-scoped):** enforced **inside** `SessionService.RevokeSessionAsync` (the owner-scoped + idempotent no-op); the controller is route + authz + shape, never a re-derivation (the M3 `PostService` pattern). `SessionsRevoke` resolves `subject` via `KumunitaPrincipal.SubjectId(User)` (the existing idiom) and passes it straight through.
- **C3 pin deviation (documented, not a violation):** the kill-all extension's two commits — `identity.KillAllSessionsAsync` (Identity/EF stamp rotation) and `RevokeAllButCurrentAsync` (Marten `RevokedAt` writes + the `session.revoke_all_but_current` audit row) — land in **separate transactions within the same request** (the two modules do not share a transaction). The C3 pin is satisfied *within* each service method (the U4 `AccessAudit` + domain write are one `SaveChangesAsync`) and at the *request* level for the two-mechanism case — matching the design doc §2.3 documented deviation (U2's note + U4's note). The F5 "local sign-out" behavior is **unchanged**: `SignOutAsync()` + the redirect to `/Account/Login` still run (the resident is signed out of the current browser).
- **`sessions` rate-limit policy (10/15 min per IP):** added in `Program.cs`'s `AddRateLimiter` block via the codebase's existing `AddWindow(opts, "sessions", limit: 10, window: TimeSpan.FromMinutes(15))` helper — the **exact** shape of the F8 `upload` policy two lines above (a fixed-window limiter partitioned by the resolved client IP, `context.Connection.RemoteIpAddress?.ToString() ?? "unknown"`). The plan's inline `RateLimitPartition.GetFixedWindowLimiter(…)` block is satisfied by this helper (it wraps that exact call) — no new policy-registration mechanism, one `AddWindow` line + a comment.
- **New model file:** `src/Kumunita.Web/Models/SessionListViewModel.cs` — `SessionListViewModel { CurrentSessionId; Sessions; KillAllEnabled }` + `SessionItem { Id, UserAgent, Ip, CreatedAt, LastActive, IsCurrent }`. The existing `SessionSettingsViewModel` is **untouched** (the F5 kill-all block shape; U8's view is the unit that renders the table).
- **`using` added to the controller:** `using Kumunita.Core.Sessions;` (for the `SessionService` ctor param + the `SessionService` type; `IdentityConstants` is already imported via `Microsoft.AspNetCore.Authentication`).
- **Compile warnings (new code):** none.
- **Files touched (exactly the U7 Deliverables):** `src/Kumunita.Web/Controllers/SecuritySettingsController.cs` (modified — the `SessionService` ctor param + the `CurrentSessionIdAsync` helper + the extended `Sessions` action + the new `SessionsRevoke` action + the kill-all extension), `src/Kumunita.Web/Models/SessionListViewModel.cs` (new), `src/Kumunita.Web/Program.cs` (one `AddWindow(opts, "sessions", …)` line + a comment), and this handoff note. No other file touched; no test (U10's lane); no `SessionSettingsViewModel` edit (U8's surface).

## U8 — view

Rewrote `src/Kumunita.Web/Views/SecuritySettings/Sessions.cshtml` to render the per-session table, on top of the **unchanged** F5 "Sign out everywhere" kill-all card. **Build green: 0 errors; 0 warnings in `Sessions.cshtml`** (`dotnet build src/Kumunita.Web/Kumunita.Web.csproj -c Debug` → "Build succeeded. 15 Warning(s) 0 Error(s)" — the 15 are pre-existing CS8602 null-derefs in *other* views; none reference `Sessions.cshtml`). No new test (U10's lane). No `site.js` change (see the drift note below).

- **`@model` repoint (required):** `@model Kumunita.Web.Models.SessionSettingsViewModel` → `@model Kumunita.Web.Models.SessionListViewModel` (U7's `GET /settings/security/sessions` action now returns this; the F5 `SessionSettingsViewModel` is no longer bound). The `@inject ITranslationProvider` / `ILocalizationService` + `EffectiveLanguageCode.ResolveAsync` pattern is unchanged.
- **New `kw-l` keys shipped (5, verbatim for U9 to register):** `settings.sessions.list_heading` (table heading "Active sessions"), `settings.sessions.list_desc` (table description), `settings.sessions.current_badge` (the "This session" badge), `settings.sessions.revoke_button` (the per-row "Revoke" button), `settings.sessions.revoke_confirm` (the per-row `data-confirm` string — used via `Translation.GetAsync`, **not** a `kw-l` element). **All 5 are currently UNREGISTERED** in `KnownTranslationKeys.cs` (the pre-existing 6 — `intro` / `this_heading` / `this_desc` / `kill_heading` / `kill_desc` / `kill_confirm` — are registered and are reused unchanged). U9 registers these 5 (en/de/fr/da × 5 = 20 translations).
- **KEY CORRECTION for U9 — the enforcement is a TEST, not the build.** The plan's U8 premise ("a kw-l key used-but-unregistered fails the build") is **wrong for this codebase**: the `LocalizeTagHelper` (`src/Kumunita.Web/TagHelpers/LocalizeTagHelper.cs`) resolves a missing key via `ITranslationProvider.GetAsync` and **gracefully falls back to the raw key** at runtime (no build error, no throw) — which is why my build is green. The real guard is the **existing test** `KwLRegistryConsistencyTests.Every_KwL_Key_In_A_View_Is_Registered` (`tests/Kumunita.Web.Tests/KwLRegistryConsistencyTests.cs`) — a static scan of every `kw-l key="…"` in `Views/**/*.cshtml` against `KnownTranslationKeys.EnValues`. Until U9 registers the 4 `kw-l`-element keys above, that test will **fail the test run** (not the build) listing `Views/SecuritySettings/Sessions.cshtml: settings.sessions.list_heading` / `.list_desc` / `.current_badge` / `.revoke_button`. The 5th key, `settings.sessions.revoke_confirm`, is **not** caught by that scan (it's read via `Translation.GetAsync`, not a `kw-l` attribute) — but U9 should still register it + its 4-language translations for the `en` floor + ADR 0005 parity, mirroring how `settings.sessions.kill_confirm` is registered and read the same way.
- **`data-confirm` idiom (the codebase's own confirm contract):** the per-row revoke `<form>` carries `data-confirm="@_sessRevokeConfirm"` + `@Html.AntiForgeryToken()` — exactly the existing kill-all form's shape (the `PostDetail.cshtml` / `AdminSite/Index.cshtml` idiom). The confirm is handled by the shared `client/lib/confirm.ts` (loaded once in `_Layout.cshtml`; reads `form[data-confirm]` on submit, `window.confirm`s). **NOT** `onsubmit="return confirm(…)"` and **NOT** a new `site.js` handler.
- **`@item.Id` interpolation (real Razor expression):** the per-row form's `action="/settings/security/sessions/@item.Id/revoke"` uses a **bare** `@item.Id` inside a **quoted** attribute value — a genuine Razor expression. It is **NOT** the `@($"…")` C#-interpolation bug named in AGENTS.md's Razor doctrine (I did not wrap it in `@$`).
- **Table columns (one `<tr>` per `Model.Sessions` item):** `Device` (UserAgent, truncated to 60 chars + `…`, full UA in `title="…"`), `IP address` (`item.Ip`), `Signed in` (`item.CreatedAt.ToLocalTime().ToString("g")` — ADR 0019 timezone convention), `Last active` (`item.LastActive.ToLocalTime().ToString("g")`), and the last cell — the "This session" `badge bg-success` when `item.IsCurrent`, else the per-row revoke `<form>` + "Revoke" button.
- **Razor gotcha caught + fixed:** an `@{ var _ua = … }` inside the `@foreach (…){…}` code block is illegal (`error RZ1010: Unexpected "{" after "@" character`). Fixed by declaring `var _ua = item.UserAgent;` directly in the foreach body (no `@{ }` wrapper).
- **Drift note (a correction, NOT a `## U8 — Drift pause`):** the plan's U8 "Deliverables" list a `site.js` handler + the "Exit" names a `tsc` build; the codebase's actual confirm contract is the declarative `data-confirm` attribute + `client/lib/confirm.ts` (SECURITY.md §6 no-inline-script), which the existing kill-all form in this same view already uses. I followed the codebase: the per-row form uses `data-confirm`, **no `site.js` change, no `tsc` step**. The plan's intent (a confirm-before-submit on the per-row form) is fully satisfied by the existing idiom, so this is a stale-plan-form correction, not a design mismatch.
- **`revoke_409` / `revoke_404` (the plan's U9 also lists these):** the view emits **neither** — the 409/404 responses are produced by U7's `SessionsRevoke` controller action as `StatusCode` / `NotFound`, with no view. So they are not part of U9's view-key closure; U9 registers the 5 keys above (registering the extra 2 is harmless, but they'd be dead keys until a view renders them).
- **Files touched (exactly the U8 Deliverables):** `src/Kumunita.Web/Views/SecuritySettings/Sessions.cshtml` (modified), and this handoff note. No other file touched; no test (U10's lane); no `site.js` (see drift note); no `KnownTranslationKeys.cs` / `FirstBootSeeder.cs` (U9's lane).

## U9 — i18n keys

Registered the 5 `settings.sessions.*` keys U8 shipped unregistered, × en/de/fr/da = **20 entries**, each inserted immediately after the existing `settings.sessions.kill_confirm` line in its language block. **`Kumunita.Core` build: 0 Warnings, 0 Errors.** `KwLRegistryConsistencyTests.Every_KwL_Key_In_A_View_Is_Registered` **would pass** (verified by mirroring its static scan: 1584 static kw-l view keys, **0 missing** from the registry — all 5 new keys registered; no closed-set test pins the `settings.sessions.*` namespace, confirmed by grep across `tests/`).

- **(a) 5 key names (verbatim, per U8's correction — the view's `settings.sessions.*` names, NOT the plan's stale `sessions.*` form):** `settings.sessions.list_heading`, `settings.sessions.list_desc`, `settings.sessions.current_badge`, `settings.sessions.revoke_button`, `settings.sessions.revoke_confirm`. `revoke_409` / `revoke_404` were **NOT** registered (U8 confirmed no view emits them; the controller returns 409/404 as status codes).
- **(b) translation count:** 5 × 4 = **20** entries (en/de/fr/da). en values match the view's verbatim inner-text / `revoke_confirm` floor exactly; de/fr/da mirror the existing `settings.sessions.kill_*` block style.
- **(c) `improve-check.ps1` re-baseline:** the `KnownTranslationKeys.cs` line-count pin actually lives in **gate (a)** (`$aBaselineSizes`), not gate (d) as the plan labels it (gate (d) is the handoff-notes TL;DR gate) — a plan mislabel, the intent is the same re-baseline-and-annotate mechanism. Re-baselined `KnownTranslationKeys.cs` **14885 → 14905** in `docs/archive/lanes/improve/improve-check.ps1` (gate (a)'s `$aBaselineSizes` hash) + added the dated re-baseline comment. **ADR annotation status: PENDING at U12** — ADR 0281 does not exist yet (lands in U12); the re-baseline comment records "ADR 0281 does not yet exist — it lands in the sessions lane's U12 close; the ADR that drives this re-baseline will be annotated there." Gate (a) now **[OK]** (verified by running the gate).
- **(d) compile warnings (new code):** **none** — `Kumunita.Core` built 0 Warning(s) / 0 Error(s). No `FirstBootSeeder.cs` change needed (it holds **no** `settings.sessions.*` keys — the `KnownTranslationKeys.cs` registry is the single 4-language source for these keys, confirmed by grep).
- **BLOCKER for U10/U11/U12 (pre-existing, NOT introduced by U9, outside U9's Deliverables):** `dotnet build Kumunita.slnx -c Debug` reports **Build FAILED — 4 Error(s)**, all `CS7036` "no argument given that corresponds to the required parameter 'sessionService' of SecuritySettingsController" in four **pre-existing** test files: `AccountControllerChangePasswordTests.cs(74)`, `F5U4_KillAllTests.cs(172)`, `MfaQrTests.cs(258)`, `SecuritySettingsMfaLockTests.cs(281)`. Root cause: **U7 added a `SessionService` parameter to the `SecuritySettingsController` constructor** but did not update these four call sites — U7's handoff notes show it only built `Kumunita.Web.csproj`, never the test project, so it never saw the break. This blocks `Kumunita.Web.Tests` from compiling (and therefore U10's `SessionsKillAllExtTests` from running). Fix = pass a `SessionService` (or a stub) to each of the four `new SecuritySettingsController(...)` calls — a U10-adjacent repair, but it edits test files, which U9's unit-series rule (1) forbids me from touching. **Do not attribute these errors to U9.**
- **BLOCKER for U12 (pre-existing, outside U9's Deliverables):** `improve-check.ps1` **gate (g) [FAIL]** — `docs/design/sessions-per-session-revocation.md` (692 lines, no Abstract in first 15 lines; new beyond the U07 baseline). Authored by U1/U2; adding an Abstract (or the design-doc gate re-baseline) is a U12-close task (U12 owns the design-doc/ADR/ARCHITECTURE flip). Not a U9 file.
- **Files touched (exactly the U9 Deliverables):** `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (20 entries added), `docs/archive/lanes/improve/improve-check.ps1` (gate (a) re-baseline 14885 → 14905 + comment), and this handoff note. No other file touched; no test (U10's lane); no `FirstBootSeeder.cs` (no separate seed for these keys); no view / controller edit (U8's / U7's lanes).

## U10 — seam tests (12 + 1)

Authored the 12 pinned `SessionServiceTests` (§2.4, verbatim) + 1 kill-all
extension Web test. **Build green: 0 errors** (`dotnet build Kumunita.slnx -c
Debug`, `EXIT=0`). **13 discovered, 13 executed, 0 failed** — the pass counts U11
consumes (Core 12/12; Web 1/1).

- **(a) 2 test file paths:** `tests/Kumunita.Core.Tests/SessionServiceTests.cs`
  (new, 12 `[Fact]`s) + `tests/Kumunita.Web.Tests/SessionsKillAllExtTests.cs`
  (new, 1 `[Fact]`).
- **(b) 13 names (verbatim, §2.4 + §2.5):**
  - `F1_CreateSession_ReturnsDocWithGuidIdAndTimestamps`
  - `F2_GetSessionsForUser_ReturnsOnlyActiveSessionsMostRecentFirst`
  - `F3_GetSessionsForUser_ExcludesRevokedSessions`
  - `F3_GetSessionsForUser_ReturnsEmptyForUnknownUser`
  - `F4_RevokeSession_SetsRevokedAtToUtcNow`
  - `F4_RevokeSession_DoesNotTouchCurrentSession`
  - `F4_RevokeSession_DeniesCrossUser`
  - `F4_RevokeSession_IsIdempotent`
  - `F5_RevokeAllButCurrent_RevokesOthersKeepsCurrent`
  - `F5_RevokeAllButCurrent_RevokesOthersWhenCurrentUnknown`
  - `F6_TouchSession_UpdatesLastActiveWhenDue`
  - `F6_TouchSession_SkipsWhenNotDue`
  - `KillAllExt_RevokesOthersKeepsCurrent` (the Web kill-all extension test)
- **(c) 13 pass/red counts (for U11):** **12/12 Core green** (`Kumunita.Core.
  Tests` `Total: 12, Errors: 0, Failed: 0`); **1/1 Web green**
  (`Kumunita.Web.Tests` `Total: 1, Errors: 0, Failed: 0`). **13 discovered, 13
  executed, 0 failed.** The F5U4 pre-existing tests I repaired also re-verified
  green (2/2 `F5U4_KillAllTests`, and the 3 other repaired files 14/14).
- **(d) PREREQUISITE REPAIR (a U7-defect fix, NOT U10's authored test — named
  explicitly per the unit-series rule):** U7 added a `sessionService:` param to
  the `SecuritySettingsController` ctor but never updated 4 pre-existing test
  call sites (U7 built only `Kumunita.Web.csproj`, never the test project), so
  `Kumunita.Web.Tests` did not compile (4 `CS7036`). Repaired all 4:
  - `AccountControllerChangePasswordTests.cs`, `MfaQrTests.cs`,
    `SecuritySettingsMfaLockTests.cs` — added `sessionService: null!` (these
    tests never touch the session actions; `SessionService` is **sealed** so
    NSubstitute cannot proxy it — `Substitute.For<SessionService>()` threw
    `TypeLoadException: parent type is sealed` at runtime — and these files have
    no store, so `null!` is the safe, minimal repair). All 14 tests in these 3
    files pass.
  - `F5U4_KillAllTests.cs` — the one to think about: `SessionsKillAll` now also
    calls `sessionService.RevokeAllButCurrentAsync(...)`. I wired a **real**
    `IDocumentStore`-backed `SessionService` (registered `SessionDocTypes` on the
    test store + exposed the store on the `Boot` record + seeded one `SessionDoc`
    for the "current session"). The action's `CurrentSessionIdAsync()` (added by
    U7) calls `HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme)`,
    which in the direct-construction harness needs an `IAuthenticationService` in
    `RequestServices` — without it the test throws `ArgumentNullException
    (provider)` **and F5U4's `KillAllSessions_SignsOutAll` breaks too** (a second
    U7 defect the U9 blocker note did not anticipate). I added a
    `SidAuthenticationService` stub returning a success `AuthenticateResult`
    whose `AuthenticationProperties.Items["kumunita.sid"]` is the current-session
    id (the `Program.cs:890` `OnSigningIn` shape), so `CurrentSessionIdAsync`
    resolves and `RevokeAllButCurrentAsync` preserves the current session
    (the §2.3 shape 2, not the "sid unknown" degrade-to-all). F5U4 now passes 2/2.
- **(e) compile warnings (new code):** **none that matter** — `SessionsKillAll
  ExtTests.cs` had 2 `xUnit1051` (pass `TestContext.Current.CancellationToken`
  to `LoadAsync`), fixed; the remaining warnings in the build are pre-existing
  (CS8602 in other views, `xUnit1051` in `DocumentControllerTests` /
  `HealthControllerTests` / `VerifyConfirmStepTests` — none in U10's files).
  **One note for U11/U12:** the F5 `session.kill_all` audit row uses
  `TargetKind = "account"` (the `IdentityService.AccountKind` const,
  `IdentityService.cs:62`) — **not** `"session"` — so any assertion on that row
  must filter on the `Action` tag, not `TargetKind`. The per-session lane's
  `session.revoke` / `session.revoke_all_but_current` rows DO use
  `TargetKind = "session"` (U4's `SessionService`).
- **Drift check (unit-series rule 6):** no drift — the 12 §2.4 test names landed
  verbatim; the `SessionService` 5-method surface, the `SessionDoc` 7-field
  shape, the `kumunita.sid` cookie key, and the `session.revoke` /
  `session.revoke_all_but_current` audit `Action` tags all match U4's landed
  implementation. **No `## U10 — Drift pause`.**
- **Files touched (U10 Deliverables + the named prerequisite repair):**
  `tests/Kumunita.Core.Tests/SessionServiceTests.cs` (new, U10-authored),
  `tests/Kumunita.Web.Tests/SessionsKillAllExtTests.cs` (new, U10-authored), and
  the 4-file prerequisite repair (`AccountControllerChangePasswordTests.cs`,
  `MfaQrTests.cs`, `SecuritySettingsMfaLockTests.cs`, `F5U4_KillAllTests.cs`).
  No controller / view / `KnownTranslationKeys.cs` / design-doc edits (those lanes
  are closed); no e2e authored (U11's lane); no gate run (U11's lane).

## U11 — gate recorded

Recorded the sessions lane's acceptance gate (the §2.5 three-test shape:
**closed-loop** / **handoff** / **part-vs-whole**) in
`docs/design/sessions-per-session-revocation.md` as
`### Run result (sessions acceptance gate — 2026-11-01)` (appended after §2.6's
U2 frozen-counts blockquote). **No code, no build** (the build was re-run to
confirm the test assemblies were current — `dotnet build Kumunita.slnx -c
Debug`, 0 errors — and the 13 tests were re-executed to confirm they still
pass, mirroring U10's "re-run them if needed" instruction).

- **Three test names + pass counts + date (2026-11-01):**
  1. **Closed-loop** — evidenced by `F1_CreateSession_ReturnsDocWithGuidIdAnd
     Timestamps` · `F2_GetSessionsForUser_ReturnsOnlyActiveSessionsMostRecentFirst`
     · `F4_RevokeSession_SetsRevokedAtToUtcNow` ·
     `F3_GetSessionsForUser_ExcludesRevokedSessions` — **green**.
  2. **Handoff** — evidenced by
     `F5_RevokeAllButCurrent_RevokesOthersKeepsCurrent` ·
     `F5_RevokeAllButCurrent_RevokesOthersWhenCurrentUnknown` ·
     `SessionsKillAllExtTests.KillAllExt_RevokesOthersKeepsCurrent` (the Web
     kill-all extension test — the two mechanisms coexist, the C3 same-
     transaction pin at the request level per the §2.3 documented deviation) —
     **green**.
  3. **Part-vs-whole** — all **12** `SessionServiceTests` + the **1**
     `SessionsKillAllExtTests` must pass together — **13/13 green, 0 failed**.
- **13-test count (from U10):** **12 Core / 12 green** (`Kumunita.Core.Tests`
  `Total: 12, Errors: 0, Failed: 0`) + **1 Web / 1 green** (`Kumunita.Web.Tests`
  `Total: 1, Errors: 0, Failed: 0`) = **13/13 passed, 0 failed** — re-confirmed
  in this unit (Core via `dotnet exec …/Kumunita.Core.Tests.dll -class
  'Kumunita.Core.Tests.SessionServiceTests'`, Web via `dotnet exec
  …/Kumunita.Web.Tests.dll -class 'Kumunita.Web.Tests.SessionsKillAllExtTests'`,
  the AGENTS.md "Running the tests (test-runner quirk)" reliable path; Testcontainers
  `postgres:18`, fresh container per class, cleaned up on completion).
- **F5 e2e status (present / gap / deferred to U12):** **gap — deferred to
  the owner-ops scope, not a U12 unit deliverable.** The `STATUS.md`
  prod-hardening owner-ops list carries the "live E2E + F5 sign-out
  verifications" line, explicitly "recorded, not assumed done" (the same shape
  as the other owner-ops items in `STATUS.md`). The `e2e/` Playwright project
  **is present** (F19 — `e2e/package.json` + `playwright.config.ts` + the two
  F19 specs `signup_verify_login.e2e.ts` / `post_write.e2e.ts`), but **no
  sessions spec is authored** in `e2e/specs/` (U10's handoff note confirms "no
  e2e authored (U11's lane)"; my unit-series rule (1) forbids me from authoring
  outside my Deliverables), and the owner-ops list **owns** the live F5 sign-out
  verification. Per the plan's U11 fallback path, the **gate is evidenced at
  the seam-test layer** by the 12 `SessionServiceTests` + the 1
  `SessionsKillAllExtTests` above (13/13 green) — **this is not a gate fail**.
  The owner-ops item that lands the live F5 + sessions sign-out verification
  records the pass count in a subsequent `### Run result (sessions e2e —
  <date>)` section of the design doc.
- **`improve-check.ps1` gate (g) (the design-doc Abstract blocker from U9):
  STILL OPEN FOR U12.** U11 did **not** add an Abstract to the design doc (the
  `### Run result` append is the only change I made to that file, per the
  unit-series rule that the design-doc close-flip — ADR 0281 + the Abstract +
  the `improve-check.ps1` gate (g) re-baseline — is U12's close-flip file, not
  U11's). Gate (g) requires a `> **Abstract:**` blockquote in the first 15
  lines of any `docs/design/*.md` over 400 lines (the design doc is now
  ~740 lines). U12's close-flip (ADR 0281 + `WhatsNew.cs` + `ARCHITECTURE.md`
  + the `SL·D` deferral note) must add the Abstract blockquote to the design
  doc's first 15 lines in the same commit as the `improve-check.ps1` gate (g)
  re-baseline (the `KnownTranslationKeys.cs` re-baseline U9 already did was
  gate (a), a separate gate — the Abstract is gate (g), a distinct gate).
- **Still-open drift:** **No `## U<m> — Drift pause` sections exist in the
  handoff note** (confirmed by a direct scan of the handoff note — the three
  "Drift note (a correction, NOT a `## U<m> — Drift pause`)" mentions in U3,
  U8, and U10's sections are explicitly *not* drift pauses). The two
  pre-existing blockers U9 inherited (the 4 `CS7036` `SecuritySettingsController`
  ctor call-site breaks in pre-existing test files — **repaired by U10**,
  green; the `improve-check.ps1` gate (g) design-doc-Abstract blocker —
  **still open for U12**) are likewise **not §2.6 drift-pauses** (gate (g) is
  a close-flip task, not a frozen-pin violation). **No still-open §2.6 drift.**
  The plan-documentation slips recorded by U3 (the `SchemaBootstrap.cs` entry-
  read file list — the codebase has exactly one `StoreOptions`, built in
  `Program.cs`, and `SessionDocTypes.Configure` lives there for every context,
  including `SessionDoc`'s), U8 (the `site.js` handler + `tsc` step — the
  codebase's actual confirm contract is the `data-confirm` attribute +
  `client/lib/confirm.ts`, which the existing kill-all form in the same view
  already uses), and U9 (the `improve-check.ps1` gate (a) vs (d) label — the
  `KnownTranslationKeys.cs` line-count pin actually lives in gate (a)'s
  `$aBaselineSizes`, not gate (d) as the plan labels it) are all **not §2.6
  drift-pauses** and **do not affect the frozen pins**; they are carried into
  U12's close (the ADR 0281 author) for the record.
- **Files touched (exactly the U11 Deliverables):**
  `docs/design/sessions-per-session-revocation.md` (one `### Run result
  (sessions acceptance gate — 2026-11-01)` section appended after §2.6's U2
  frozen-counts blockquote — the three-test gate table + the E2E status + the
  drift status), and this handoff note. **No** ADR, **no** `WhatsNew.cs`,
  **no** `ARCHITECTURE.md`, **no** plan-file move (those are U12's close
  flip), **no** controller / view / `KnownTranslationKeys.cs` / test-file
  edits (those lanes are closed).

## Summary

The `sessions` lane (per-session list + revoke one device) is **closed** at
U12 (2026-11-01). This is the **first session-data milestone**: F5
(prod-hardening) shipped the whole-account stamp-rotation kill; this lane adds
the per-session model on top — the two mechanisms coexist and are independent
(the stamp is the whole-account kill, `SessionDoc.RevokedAt` is the per-session
kill). **Gate: 13/13 green, 0 failed** (U11; the F5 e2e is *recorded, not
assumed done* — the prod-hardening owner-ops item). **ADR 0281 Accepted**;
v0.84.0 in `WhatsNew.cs`. `Milestones.cs` / README Roadmap /
`MilestonesTests.cs` **byte-identical** (a **named lane**, not a milestone —
the ADR 0013 `GP` / ADR 0171 `FDW` precedent).

### Shipped units (U1–U11)

| Unit | Goal (one-liner) | Tests | Deviations |
|---|---|---|---|
| **U1** | design doc Part 1 (Context, Scope incl. the 5 `SL·D` deferrals, the invariants table, the 6 FACES, the Part-1 drift-guard) | — | Plan-documentation slip: plan headline said "11" invariants, body is **12** (body wins; the *reversed* direction of M3's U1 slip). |
| **U2** | design doc Part 2 (frozen seam list, new Core types, revoke rule, 12 pinned test names, the 3-test gate, the drift-guard + U2 frozen-counts blockquote) | — | None — the 12 invariants + 12 test names + 2 tables + 3-test gate are the frozen pins; the C3 same-transaction deviation is documented (not a violation). |
| **U3** | `SessionDoc` (7 fields) + `SessionDocTypes` (1 `.Schema.For` line) + boot wiring (one `Program.cs` registration line) | — | **Plan-documentation slip (U12 record):** the plan's U3 "Entry reads / Deliverables" named `SchemaBootstrap.cs` as a registration site; that file has **no** `*DocTypes.Configure` calls — this codebase has exactly one `StoreOptions` (built in `Program.cs`), so the single `Program.cs` registration is the whole boot wiring (every other context, incl. `SessionDoc`'s, lives there). Not a §2.6 drift pause. |
| **U4** | `SessionService` (the frozen 5-method surface: create / list / revoke / revoke-all-but-current / touch) + the `DependencyInjection.cs` registration | — | None — C-S1·1 (controller), C-S1·3 (service), C3 (same-session audit + write), C-S1·5 (middleware, not service) pins all landed as pinned. |
| **U5** | `OnSigningIn` hook — the `kumunita.sid` cookie item + the `CreateSessionAsync` guard (sign-in never fails) | — | None — the `TwoFactorUserIdScheme` handler is unchanged (leg-1 is not a full session). |
| **U6** | `SessionRevocationMiddleware` (per-request enforce + the rate-limited touch) + the registration order (after `RevocationStampMiddleware`) + the `Kumunita:Sessions:TouchIntervalSeconds` config key (default 60) | — | None — the 5-state table (no sid → pass; sid + not found → pass; sid + revoked → sign out; sid + active + due → touch; sid + active + not due → no touch) all landed as pinned. |
| **U7** | `SecuritySettingsController` extension (the `GET /sessions` list + `POST /sessions/{id}/revoke` + the kill-all extension) + the `sessions` rate-limit policy (10/15 min per IP) + the `SessionListViewModel` | — | **Deviation (U12 record):** the `SecuritySettingsController` ctor gained a `sessionService:` param, which **broke 4 pre-existing test call sites** (U7 built only `Kumunita.Web.csproj`, never the test project) — repaired by U10 (see below). The C3 same-transaction pin is satisfied at the *request* level (the F5 Identity/EF path and the sessions Marten path do not share a transaction) — the §2.3 documented deviation, the one `C3` relaxation in this lane. |
| **U8** | `Sessions.cshtml` sessions table (the "This session" badge + the per-row "Revoke" form on every *other* row) on top of the **unchanged** F5 kill-all card | — | **Plan-documentation slip (U12 record):** the plan's U8 "Deliverables" listed a `site.js` handler + a `tsc` step; the codebase's actual confirm contract is the `data-confirm` attribute + `client/lib/confirm.ts` (which the existing kill-all form in the same view already uses) — no `site.js` change, no `tsc` step. Not a §2.6 drift pause. |
| **U9** | 5 `settings.sessions.*` `kw-l` keys × en/de/fr/da = 20 entries in `KnownTranslationKeys.cs` + the `improve-check.ps1` gate (a) re-baseline (14885 → 14905) | — | **Plan-documentation slip (U12 record):** the plan labeled the `KnownTranslationKeys.cs` line-count pin as "gate (d)"; it actually lives in **gate (a)**'s `$aBaselineSizes` (gate (d) is the handoff-notes TL;DR gate) — the intent is the same re-baseline-and-annotate mechanism. **ADR annotation status:** the re-baseline comment at U9 recorded "ADR 0281 does not yet exist — it lands in the sessions lane's U12 close"; the ADR that drives this re-baseline is **ADR 0281** (this close). |
| **U10** | 12 pinned `SessionServiceTests` (§2.4, verbatim) + 1 kill-all extension Web test (`SessionsKillAllExtTests`) | **12 + 1 = 13 discovered, 13 executed, 0 failed** | **Prerequisite repair (a U7-defect fix, NOT U10's authored test — named per the unit-series rule):** U7 added a `sessionService:` param to the `SecuritySettingsController` ctor but never updated 4 pre-existing test call sites, so `Kumunita.Web.Tests` did not compile (4 `CS7036`). Repaired all 4: `AccountControllerChangePasswordTests.cs` / `MfaQrTests.cs` / `SecuritySettingsMfaLockTests.cs` (added `sessionService: null!` — `SessionService` is **sealed**, NSubstitute cannot proxy it, and these files have no store) + `F5U4_KillAllTests.cs` (wired a real `IDocumentStore`-backed `SessionService` + a `SidAuthenticationService` stub for the `CurrentSessionIdAsync()` `AuthenticateAsync` call — a second U7 defect the U9 blocker note did not anticipate). All 16 tests in the 4 repaired files pass (U10's 13 + the F5U4 2/2 + the 3 other files 14/14). |
| **U11** | run + record the 3-test acceptance gate (closed-loop / handoff / part-vs-whole) + the F5 e2e status | **13/13 green, 0 failed** (Core 12/12 + Web 1/1, re-confirmed) | The F5 e2e (the `STATUS.md` prod-hardening owner-ops item "live E2E + F5 sign-out verifications") is **recorded, not assumed done** — the `e2e/` Playwright project is present (F19) but no sessions spec is authored (U11's own unit-series rule (1) forbids authoring outside its Deliverables); the owner-ops item that lands it records the pass count in a subsequent `### Run result (sessions e2e — <date>)` section of the design doc. **Gate (g) (the design-doc Abstract blocker from U9) is inherited, currently the open blocker — U12 resolves it.** |
| **U12** | close: ADR 0281 + `WhatsNew.cs` 0.84.0 head + `ARCHITECTURE.md` flip + `README.md` row + `STATUS.md` note + gate (g) resolved + plan → `done/` | (Web suite re-run to confirm the `WhatsNewTests` head-pin is green after the bump — the AGENTS.md "Running the tests" reliable path) | **Gate (g) resolved:** the design doc's first 15 lines now carry the `> **Abstract:**` blockquote (the `improve-check.ps1` gate (g) — the 10-second "is this the right read" gate — was the open blocker U9 + U11 carried to U12; the doc is ~740 lines, over the 400-line ceiling, and was not in the U07 baseline, so a *new* file without an Abstract fails the close — the Abstract is the actual fix, not a re-baseline). **Deviation record (per the unit-series rule):** the close-flip convention requires 7 files (`docs/adr/0281-…md`, `docs/adr/README.md`, `src/Kumunita.Web/WhatsNew.cs`, `tests/Kumunita.Web.Tests/WhatsNewTests.cs`, `docs/ARCHITECTURE.md`, `README.md`, `docs/STATUS.md`) + the design-doc Abstract + this `## Summary` + the plan-folder move — **10 files + the move**, exceeding U12's "≤ 4 files" cap. The repo's doc↔code parity (AGENTS.md) + the M27 `WhatsNew.cs` lesson are the authority: a missed member = a red test (`WhatsNewTests` head-pin) or a stale doc. The two documented deviations of record (the U3 `SchemaBootstrap.cs` entry-read slip; the C3 same-transaction request-level deviation from §2.3) are recorded in **ADR 0281** (§ "Deviations of record"). |

### The 5 `SL·D` deferrals (verbatim, with the one-line candidate each)

Each is a **named deferral** with a one-line `SL·D` follow-up candidate
(resolved by U12's ADR 0281 § "Not covered"):

1. **Per-session `DeviceLabel`** — a resident-set label (rename "Chrome on
   work laptop" to "My laptop"). A future `SL·D` lane adds the field + the
   rename surface. *`SL·D` candidate: add `DeviceLabel?` to `SessionDoc` + a
   `PATCH /sessions/{id}/label` action.*
2. **Per-session `Kiosk` flag** — a kiosk-mode session property. The kiosk is a
   separate surface, not a session property. *`SL·D` candidate: the kiosk
   surface declares its own session scope, not a `SessionDoc` field.*
3. **Admin "sign out all residents"** — a new `GlobalAdmin` action (kill every
   resident's sessions platform-wide). Not a per-resident surface. *`SL·D`
   candidate: a `GlobalAdmin` `POST /admin/sessions/kill-all` action over
   `SessionService.*` with an `Admin` audit row (ADR 0006-E named lane).*
4. **Per-group / per-event session scoping** — sessions are per-account, not
   per-resource. *`SL·D` candidate: a `SessionScope` document keyed to a
   resource id, only if a M4/M5 surface genuinely needs it.*
5. **A session `Trust` / `Remember me` distinction** — the existing cookie
   already carries a `Persistent` flag; no new surface in this lane. *`SL·D`
   candidate: surface `Persistent` in the list if a resident asks.*

### Close-flip record (U12, 2026-11-01)

- `docs/design/sessions-per-session-revocation.md` — `> **Abstract:**`
  blockquote added to the first 15 lines (gate (g) resolved — the doc is
  ~740 lines, over the 400-line ceiling, not in the U07 baseline).
- `docs/adr/0281-session-doc-and-per-session-revocation.md` — **new** ADR
  (Context / Decision / Consequences / Not covered (the 5 `SL·D` deferrals,
  verbatim) / **Deviations of record** (the U3 `SchemaBootstrap.cs` entry-read
  slip + the C3 same-transaction request-level deviation) / Close flip).
- `docs/adr/README.md` — the **0281** index row added (after 0280).
- `src/Kumunita.Web/WhatsNew.cs` — the **0.84.0** / `2026-11-01` / ADR 0281
  head row (the **required sixth close-flip member**, the M27 lesson).
- `tests/Kumunita.Web.Tests/WhatsNewTests.cs` — the head-pin **bumped**:
  `0.84.0` / `2026-11-01` / ADR 0281 is now the head (the old `0.83.0` /
  ADR 0280 assertion shifts down one row; the rest of the positional
  assertions shift down one row each; a `sessionsLane` version-anchored
  assertion added for `0.84.0`).
- `docs/ARCHITECTURE.md` — the `Sessions/` bounded-context line added to §2
  (the `Identity/` / `UserInfo/` / `Authorization/` / `Posts/` /
  `Moderation/` / `Localization/` / `Bootstrap/` lines are **untouched**).
- `README.md` — the **`Sessions`** named-lane row added (mirroring the
  **Prod-hardening** named-lane row — "a named lane, not a milestone").
- `docs/STATUS.md` — the **Sessions is done** note added (mirroring the
  prod-hardening named-lane paragraph shape).
- `docs/archive/lanes/improve/improve-check.ps1` — **unchanged by U12**
  (the gate (a) re-baseline 14885 → 14905 was done by U9; the gate (g) fix is
  the design-doc Abstract above, not a re-baseline).
- **Plan folder moved:** `docs/plans-milestones/in-progress/sessions/` →
  `docs/archive/lanes/sessions/` (the close flip — the `in-progress/`
  → `done/` move is the lane-closed marker).
- `Milestones.cs` / `MilestonesTests.cs` — **byte-identical** (a named lane
  reuses no milestone letter; the M4/M5/M6 roadmap stays Events/Projects/
  Portability).
