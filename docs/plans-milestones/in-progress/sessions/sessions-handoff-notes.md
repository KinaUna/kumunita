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
