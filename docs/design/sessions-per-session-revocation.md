# Design Doc — sessions: per-session list + revoke one device

> Part 1 of 2 (U1). Part 2 (U2) will append "Seams & contracts (Part 2)" with the
> exact C# shapes U3–U11 must match, the **pinned seam-test names**, the
> **three-test acceptance gate**, and the **drift-guard** — mirroring
> [`design/m3-posts-design.md`](m3-posts-design.md) Part 2 (§2.1 frozen seam
> list, §2.2 new Core types, §2.3 revoke rule, §2.4 pinned test names, §2.5
> gate, §2.6 drift-guard). Both parts pin the invariant / FACES numbers that
> every sessions unit (U3–U12) must match.

> **Lane:** **`sessions`** (named lane, "M37-adjacent") — per-session list +
> revoke one device, built on F5's shipped "Sign out everywhere"
> (stamp-rotation kill-all). The M-letter milestones M0–M37 and the existing
> named lanes (`PG`/`GP`/`ML`/`LS`/`SP`/`TZ`/`DF`/`TR`/`RC`/`GU`/`GA`/`RE`/
> `TG`/`ATT`/`improve`/`prod-hardening`/`sec`) are untouched; the M4/M5/M6
> roadmap stays Events/Projects/Portability.

## Context

F5 (AUTH-04) of the **prod-hardening** lane shipped the **"Sign out
everywhere"** capability: `POST /settings/security/sessions/kill-all` rotates
the account's `SecurityStamp` (`UserManager.UpdateSecurityStampAsync`) and
appends an `AccessAudit` row, and the per-request
`RevocationStampMiddleware` signs out any session whose cookie
`kumunita.stamp` no longer matches the DB stamp. The result is a
**whole-account kill**: every *other* device is revoked, and the current
browser is signed out (the F5 "local sign-out" behavior).

F5 deliberately did **not** build a per-session model — the existing
`Sessions.cshtml` view's own comment says the platform "cannot enumerate
another browser's cookies." The resident's natural follow-up question —
*"which of these sessions is the phone I no longer have?"* — was
**unanswerable**. F5's surface shows "this session" + a single kill-all
control; it cannot *list* sessions or revoke *one* of them.

This lane adds the missing piece and moves the value chain one arrow:
**whole-account → per-session**. After this lane the resident can *see* their
active sessions (a `UserAgent` / `Ip` / `CreatedAt` / `LastActive` row each)
and revoke **one** at a time (or all-but-current via the existing kill-all,
which becomes "revoke all-but-current" on top of the F5 stamp rotation).
That is the integration this lane serves — not a new feature bolted onto F5,
but the *linkage* between "the platform revokes a device" (F5) and "the
resident *chose* that device" (this lane).

This is the **first session-data milestone**: F5 was stamp-rotation-based (no
per-session record). This lane introduces a **new domain document**
(`SessionDoc`), a **new versioned registration surface** (`SessionDocTypes`),
a **new bounded context** (`Kumunita.Core/Sessions/`), and **one new read
seam** (`SessionService`) — mirroring the M3 `PostService` / M2
`DirectoryService` composition pattern. The F5 `kumunita.stamp` mechanism is
**kept as-is**; this lane **adds** a per-session record and a per-session
enforcement path that runs *after* the stamp gate (the stamp is the
higher-priority whole-account kill; `SessionDoc.RevokedAt` is the per-session
kill — the two coexist and are independent).

Because this is a new bounded context + a new document + a new service, this
lane — like M3 and M2 — opens with a two-part design doc that pins every
seam, invariant, and test name before any implementing unit runs: the guard
against *Distributed fragmentation* (session-validity logic re-implemented in
the controller, the middleware, *and* the service) and *Accidental
integration* (the "current session is never revoked by the per-row action"
rule living in a comment, not in the invariant list).

## Scope

**In scope**

- `SessionDoc` (Kumunita.Core, new bounded context
  `Kumunita.Core.Sessions`) — the **only** new domain document in the lane.
  Marten-native, conventional `string Id` (a GUID); fields: `UserId`,
  `UserAgent`, `Ip`, `CreatedAt`, `LastActive`, `RevokedAt` (`DateTime?` —
  the active/revoked discriminator). Part 2 §2.2 pins the exact shape.
- `SessionDocTypes.Configure(StoreOptions)` — the new parallel document
  surface (analogous to M3's `PostDocTypes`). U3 wires it into both boot
  paths (`Kumunita.Core/Bootstrap/SchemaBootstrap.cs` +
  `Kumunita.Web/Program.cs`). **No seeding** (sessions are created on
  sign-in, never seeded — ADR 0004 §B.1).
- `SessionService` (Kumunita.Core) — the **single** composition point owning
  the `SessionDoc` store. Five-method surface (Part 2 §2.1 pins the exact
  C#): `CreateSessionAsync`, `GetSessionsForUserAsync`,
  `RevokeSessionAsync`, `RevokeAllButCurrentAsync`, `TouchSessionAsync`. The
  middleware and the controller are **thin callers** of `SessionService`
  (the M3 `PostService` pattern — the controller is route + authz + shape,
  never a re-derivation). `SessionService` is the **only** caller of the
  `SessionDoc` store (ADR 0006-D "the authorization path is unique" analog —
  here the *session* path is unique).
- The `OnSigningIn` hook (`Kumunita.Web/Program.cs`, the same place F5 sets
  `kumunita.stamp`) — **adds** a `SessionDoc` row + stamps its `Id` into
  `AuthenticationProperties.Items["kumunita.sid"]`, alongside the existing
  `kumunita.stamp`. The sign-in challenge itself (password, MFA, F3/F5 stamp
  rotation on password change) is **unchanged**.
- `SessionRevocationMiddleware` (Kumunita.Web.Security) — the per-session
  analog of `RevocationStampMiddleware`: per-request enforce (read
  `kumunita.sid`, load the `SessionDoc`, sign out if `RevokedAt is not
  null`) + touch `LastActive` when due (rate-limited by the
  `Kumunita:Sessions:TouchIntervalSeconds` config key, default `60`).
  Registered **after** `RevocationStampMiddleware` (the stamp gate is the
  higher priority).
- `SecuritySettingsController` extension: the `GET /settings/security/sessions`
  action **extended** to also return the session list; a **new**
  `POST /settings/security/sessions/{id}/revoke` action (the per-row
  revoke); the existing `POST /settings/security/sessions/kill-all` action
  **extended** to also call `SessionService.RevokeAllButCurrentAsync`.
- `SessionListViewModel` (Kumunita.Web.Models) — the list + the
  `currentSessionId` + the per-row `IsCurrent` flag.
- `Views/SecuritySettings/Sessions.cshtml` — the sessions table (one row per
  session; the "This session" badge on the current row; the per-row "Revoke"
  button on every *other* row) + the existing "Sign out everywhere" kill-all
  control kept at the top.
- i18n keys (the `settings.sessions.*` block) in
  `KnownTranslationKeys.cs` + the 4-language seed data (en/de/fr/da).
- Seam tests (`Kumunita.Core.Tests/SessionServiceTests.cs`, 12 tests) +
  **one** Web test (`Kumunita.Web.Tests/SessionsKillAllExtTests.cs`) — test
  **names** are pinned by Part 2 §2.4; this part pins the invariants / FACES
  they must anchor to.
- **ADR 0281** (`docs/adr/0281-session-doc-and-per-session-revocation.md`),
  the `WhatsNew.cs` row, the `ARCHITECTURE.md` flip (U12's close).

**Out of scope — `SL·D` deferral (this section is the lane's M1-style close)**

Each is a **named deferral** with a one-line `SL·D` follow-up candidate:

- **Per-session `DeviceLabel`** — a resident-set label (rename "Chrome on
  work laptop" to "My laptop"). A future `SL·D` lane adds the field + the
  rename surface. *`SL·D` candidate: add `DeviceLabel?` to `SessionDoc` + a
  `PATCH /sessions/{id}/label` action.*
- **Per-session `Kiosk` flag** — a kiosk-mode session property. The kiosk is
  a separate surface, not a session property. *`SL·D` candidate: the kiosk
  surface declares its own session scope, not a `SessionDoc` field.*
- **Admin "sign out all residents"** — a new `GlobalAdmin` action (kill
  every resident's sessions platform-wide). Not a per-resident surface.
  *`SL·D` candidate: a `GlobalAdmin` `POST /admin/sessions/kill-all` action
  over `SessionService.*` with an `Admin` audit row (ADR 0006-E named lane).*
- **Per-group / per-event session scoping** — sessions are per-account, not
  per-resource. *`SL·D` candidate: a `SessionScope` document keyed to a
  resource id, only if a M4/M5 surface genuinely needs it.*
- **A session `Trust` / `Remember me` distinction** — the existing cookie
  already carries a `Persistent` flag; no new surface in this lane. *`SL·D`
  candidate: surface `Persistent` in the list if a resident asks.*

**Surfaces (resident-visible)**

1. **Active-sessions list** (`/settings/security/sessions`) — the sessions
   currently signed in to this account, most-recent-first, each row a
   `UserAgent` / `Ip` / `CreatedAt` / `LastActive`, the "This session" badge
   on the current row.
2. **Per-row Revoke** (`POST /settings/security/sessions/{id}/revoke`) —
   revoke *one* session (a device the resident may no longer have). The
   current session is **never** revocable by this action (C-S1·1) — the
   resident is told to use "Sign out everywhere."
3. **Sign out everywhere** (`POST /settings/security/sessions/kill-all`) —
   the existing F5 surface, **extended**: it still rotates the stamp and
   signs out the current browser (F5 unchanged), **and now also** revokes
   every *other* session's `SessionDoc.RevokedAt` (all-but-current).

## Invariants (pinned for the lane)

The lane is a **caller** of the ADR 0006 invariants at the *owner* boundary
(per-account, not per-audience), and **owns** five new invariants
(C-S1·1/2/3/4/5 — the five behavioral rules this lane is the first surface to
need: current-session protection, immediate invalidation, owner-scoping,
sign-in-unchanged, touch-rate-limit). The ADR 0006 invariants + two companion
ADRs **must keep holding for the lane's surfaces**, and the seam tests (Part
2 §2.4) will name each by id.

> **Plan-count note (U1):** the plan § U1 headline says **"11 invariants"**
> but its bullet list enumerates **12** (five lane-owned C-S1·1–5, five ADR
> 0006 caller-pins C1/C3/C4/C5/C6, ADR 0001-B, ADR 0004 §B.1). This doc pins
> **all 12 from the body** (the body is authoritative) — the **exact** slip
> M3's U1 hit (M3's headline said "12", body had 11; M3 pinned the 11 from
> the body and flagged it as a plan-documentation slip, *not* a drift
> pause). The handoff note (U1's entry) carries this discrepancy so U2 — who
> owns the test list — can confirm 12 against the body and pin test names
> accordingly. U2 owns the final count in §2.6.

| # | Lane / ADR pin | How this lane uses it |
|---|---|---|
| **C-S1·1** (lane-owned) | The **current session is never revoked by the per-row action.** `POST /sessions/{id}/revoke` **refuses** (409) to revoke the session whose id is in the current `kumunita.sid` cookie item. Kill-all **does** still sign out the current browser (the F5 behavior is unchanged — kill-all is "all-but-current" for the *other* sessions, but the current browser is signed out). | The per-row action's single guard: the controller reads `currentSessionId` from the `kumunita.sid` cookie item and compares to the `id` route value before calling `SessionService.RevokeSessionAsync`. A mismatch is **not** a 404 (that would leak the current session's id) — it is a **409** with the "This is the session you are using — use 'Sign out everywhere' to end it" message. This is what keeps the "I revoked my own device and got locked out" bug out of the surface — and is the reason the per-row button is **absent** on the current row in the view (F4). |
| **C-S1·2** (lane-owned) | A revoked session is **immediately** invalid. `SessionRevocationMiddleware` signs out the holder on the **next** request — strong consistency, mirroring F5's `RevocationStampMiddleware` (which reads the DB stamp fresh per request, not a projection). | Per-request enforcement: the middleware reads `kumunita.sid` from the cookie, loads the `SessionDoc` by id from the live store, and if `RevokedAt is not null` signs out + redirects to `/Account/Login?error=session-revoked` (the **same response** as `RevocationStampMiddleware`'s stale-stamp path — no new response shape is invented). A revoke committed in one request is enforced on the *very next* request from that session — no refresh, no stale projection. |
| **C-S1·3** (lane-owned) | Session revocation is **owner-scoped**: a resident can only revoke their **own** sessions; cross-user revoke is **denied**. The controller checks `userId == currentSubject` before calling `SessionService.RevokeSessionAsync`; a mismatch is a **404** (not a 403 — to avoid leaking the existence of another user's session ids). The service enforces the same rule: `RevokeSessionAsync` does **nothing** when `doc.UserId != userId`. | Two layers, both cheap: the controller (the HTTP boundary) returns 404 before the service is even called for a cross-user id; the service (the Core boundary) re-checks `doc.UserId != userId` and no-ops if it disagrees. A non-owner probing `/sessions/{someOtherSessionId}/revoke` gets a 404 — indistinguishable from "no such session," so the resident cannot enumerate other users' session ids by 403-vs-404. |
| **C-S1·4** (lane-owned) | The existing sign-in flow is **unchanged**: password verification, MFA, the F3/F5 stamp rotation on password change are all preserved. This lane **adds** a `SessionDoc` row + the `kumunita.sid` cookie item on sign-in; it does **not** change the sign-in challenge. The `OnSigningIn` hook (the only reliable place to set `AuthenticationProperties.Items`, per the F5 doc-comment) is the **only** place the `SessionDoc` row is created. If `CreateSessionAsync` throws (a DB outage), the sign-in **must not fail** (catch + log + continue without the `kumunita.sid` item — the session is created on the next sign-in). | The `OnSigningIn` handler is the single write path for `SessionDoc` creation — the middleware, the controller, and the service all **read** (or touch / revoke) an existing doc, but only `OnSigningIn` **creates** one. This is what makes "a session exists iff a sign-in happened" the invariant: no background seeder, no migration backfill, no `FirstBootSeeder` row (ADR 0004 §B.1 "no seeding"). |
| **C-S1·5** (lane-owned) | `LastActive` is touched **at most once per 60 s per session** (the `Kumunita:Sessions:TouchIntervalSeconds` config key, default `60`). The middleware does a single Marten read per request (the `SessionDoc` by id) + a touch write **only when due** (`UtcNow - doc.LastActive >= touchInterval`). The rate-limit is enforced by the **middleware** (the smart gate), not the service (the service is the dumb write — `TouchSessionAsync` does not check the interval). | One read per authenticated request (the same cost profile as `RevocationStampMiddleware`'s one EF read per authenticated request) + one write per 60 s per session. At this platform's scale (one neighborhood) the cost is negligible — the write-amplification this guards against is a *correctness* concern (a 1000 rps loop must not rewrite `LastActive` 1000 times a second) as much as a cost one. |
| **C1** (ADR 0006) | Empty audience denies. | This lane is **per-account, not per-audience** — there is no `Audience` field on `SessionDoc` and no `CanSeeAsync` call in the session path. C1 applies at the **owner** boundary: a non-owner cannot revoke (C-S1·3). The ADR 0006 "empty audience denies" rule is satisfied trivially — there is no audience to be empty. |
| **C3** (ADR 0006) | Audit always on — Allow *and* Deny; the audit row commits in the **same transaction** as the domain write. | Two new `AccessAudit` `Action` strings: `session.revoke` (targetKind `"session"`, `Subject = userId`, `Object = sessionId`) committed in the **same** `IDocumentSession` as the `SessionDoc.RevokedAt` write; `session.revoke_all_but_current` (targetKind `"session"`, `Subject = userId`, `Object = currentSessionId`) committed in the **same** session as the bulk `RevokedAt` writes. **No silent, unaudited revoke.** The `session.kill_all` row (F5, unchanged) is committed in the same *request* as the kill-all extension (the C3 pin is satisfied at the **request** level for the two-mechanism case — see the design doc §2.3 deviation note; the F5 row is the whole-account kill's audit, the `session.revoke_all_but_current` row is the per-session kill's audit — the two rows together answer "who did this, by what right, to which session"). |
| **C4** (ADR 0006) | Strong-consistency resolution against live documents — a change takes effect on the **very next** request. | A revoke (or a kill-all) is visible on the **next** request from the affected session — `SessionRevocationMiddleware` reads the `SessionDoc` fresh per request (no projection in the enforcement path). The `LastActive` touch is a *write* that is itself strong-consistent (the next read sees the new value), but it is not an *access* decision — C4's access-decision rule is about the `RevokedAt` read, not the `LastActive` write. |
| **C5** (ADR 0006) | Moderator default-OFF on audience-restricted content. | This lane has **no moderator surface**: the per-row revoke is owner-only (C-S1·3); there is no `Moderate`-on-session action, no `/admin` session surface, no moderator "peek" at another resident's sessions. **No `Moderate`-on-session call.** Tests assert the **absence** (a GlobalAdmin probing another resident's session id gets the same 404 as any non-owner — C-S1·3 + C5 together). |
| **C6** (ADR 0006) | One matching pass shared by `CanAsync` and `CanSeeAsync` — the paths cannot drift. | This lane is per-account (owner-scoped), not per-audience — the "matching pass" is a single `userId == currentSubject` check at the controller + a single `doc.UserId != userId` check in the service. **No `IAuthorizationService` call** in the session path (the session is not an `IAuditableResource`; it is the *actor's own* record). The two checks cannot drift because they check the *same* field (`UserId`) against the *same* value (`currentSubject`); a refactor that splits them is a drift event (§2.6). |
| **ADR 0001-B** | The resident's choice is absolute. | A resident can revoke **any** of their own sessions (including the one signed in 3 months ago on a phone they no longer have); the platform does **not** auto-retain sessions, does **not** require a "reason" for a revoke, and does **not** second-guess a resident's choice to revoke a session that still looks active. The C-S1·1 guard (current-session protection) is the **only** "extra" the platform imposes, and it is there to prevent the resident from locking themselves out, not to limit their choice. |
| **ADR 0004 §B.1** | Marten-native document registration (POCOs, conventional `string Id`), delta-detected, idempotent, **no seeding**. | `SessionDoc` is registered in **`Kumunita.Core/SessionDocTypes.Configure(StoreOptions)`** — a new parallel surface analogous to M3's `PostDocTypes` (one line: `opts.Schema.For<SessionDoc>();`). **No seeding** (sessions are created on sign-in, never seeded — C-S1·4). Delta-detected + idempotent is inherited from `ApplyAllConfiguredChangesToDatabaseAsync`. *Open veto:* the plan does not name a hand-rolled `FeatureSchemaBase` for this lane (unlike M3's `PostDocTypes`, which shipped Marten-native); if U3 finds a reason to flip, it records a `## U3 — Drift pause` and the decision lands in ADR 0281. |

**ADRs the lane must respect (not pinned as invariants, but the code must not
violate):**

- **ADR 0003 §Separation of duties** — the per-row revoke is *owner-scoped to
  the resident*; a `GlobalAdmin` revoking another resident's session is
  **not** in scope (the `SL·D` "admin sign out all residents" deferral owns
  that surface). A `GlobalAdmin` reading *their own* sessions in this lane
  has the **same** owner standing as any resident (C-S1·3 + C5 together).
- **ADR 0006-D** (dependency direction) — `SessionService` (Core) depends only
  on `IDocumentStore` (Marten); it never reads `GroupMembership` /
  `DelegationGrant` / `Profile` for its own decisions, and the
  `Kumunita.Web` layer is a thin controller (mirroring M3's `PostController`
  shape). The `OnSigningIn` hook (Web) resolves `SessionService` from
  `ctx.HttpContext.RequestServices` (the per-request scoped provider — the
  `RevocationStampMiddleware` "Dependency scoping" doc-comment, the "captive
  dependency" pitfall).
- **ADR 0006-E** (change management) — `SessionService` is a **new** public
  surface on `Kumunita.Core` (a compatible addition to the owning module —
  the ADR's *named here* list grows by exactly one line in the lane's close
  — U12). The `session.revoke` + `session.revoke_all_but_current` `Action`
  strings are **new** audit-row shapes (a compatible addition to the
  `AccessAudit` vocabulary — ADR 0006-C "adding an `AccessAction` case — new
  actions deny by default on audience-restricted resources" does **not**
  apply here because sessions are not audience-restricted resources; the
  two new strings are *owner-scoped* actions, the same position as F5's
  `session.kill_all`).
- **ADR 0005 / ADR 0015 / ADR 0052** (i18n parity) — the new
  `settings.sessions.*` keys (the 7 per-row keys U9 adds) must be registered
  in `KnownTranslationKeys.cs` *and* seeded in all four languages
  (en/de/fr/da) in `FirstBootSeeder.cs`. The existing F5 keys
  (`settings.sessions.kill_heading` / `kill_desc` / `kill_confirm`) are
  **unchanged**. The `improve-check.ps1` gate (d) re-baselines the
  `KnownTranslationKeys.cs` line count + annotates ADR 0281 in the same
  commit (the AGENTS.md "CI gates and anti-regression pins" rule).

## FACES (pinned, 6)

Each row is a *resident-visible outcome* (or a *resident-absence* outcome)
that the lane's seam tests (Part 2 §2.4) and the lane's e2e (the F5 e2e
shape, the `STATUS.md` owner-ops item) must cover. "Pinned" means the
invariant in the right column is the single authority for the outcome; the
test names Part 2 pins must reference that pin by id.

| # | Outcome (what a resident sees / can do) | Pinned by |
|---|---|---|
| F1 | A resident **signs in** (password + MFA, unchanged) and a `SessionDoc` row is created with a GUID `Id` + correct `CreatedAt` / `LastActive` timestamps + `RevokedAt = null`; the `kumunita.sid` cookie item is set to that `Id`. The sign-in **succeeds** even if the `SessionDoc` write fails (the guard — the resident is not locked out by a DB outage during the session-creation step). | C-S1·4 (sign-in unchanged; `OnSigningIn` is the only create path; the guard on `CreateSessionAsync` failure) + ADR 0001-B (the resident's sign-in is not gated by the lane) |
| F2 | A resident opens `/settings/security/sessions` and sees their **active** sessions, **most-recent-first** (ordered by `LastActive` descending), each row a `UserAgent` / `Ip` / `CreatedAt` / `LastActive`; the row where `id == kumunita.sid` carries the "This session" badge. Revoked sessions are **not** in the list (F3). | C-S1·4 (the list is the user's own sessions; no audience / moderator surface) + C-S1·3 (owner-scoped — the list is *this* resident's sessions, not a shared surface) |
| F3 | A resident's session list **excludes** revoked sessions (a `SessionDoc` with `RevokedAt != null` is not in the list); an **unknown user** (a `userId` with no `SessionDoc` rows) sees an **empty** list (not an error). A resident who has revoked session X on device A does not see session X in the list on device B on the next render (strong consistency — F5's `RevocationStampMiddleware` precedent). | C-S1·2 (revoked = immediately invalid, visible on the next request) + C-S1·3 (owner-scoped — a non-owner probing the list for another user's `userId` is not a surface; the list is keyed by the current subject) |
| F4 | A resident revokes **one** of their sessions (a device they may no longer have) from the list: the `SessionDoc.RevokedAt` is set, a `session.revoke` audit row is committed in the **same transaction**, and the revoked session is **invalid on its next request** (C-S1·2). The per-row action **refuses** (409) to revoke the **current** session (`id == kumunita.sid` — C-S1·1) with the "This is the session you are using — use 'Sign out everywhere' to end it" message. A **cross-user** revoke (`userId != currentSubject`) is a **404** (C-S1·3). A **double-revoke** (revoking the same session twice) is a **no-op** (idempotent — the second call sees `RevokedAt != null` and does nothing). The current row in the list has **no** "Revoke" button (the C-S1·1 guard is visible in the view, not just in the controller). | C-S1·1 (current-session protection) + C-S1·3 (owner-scoped; cross-user = 404) + C3 (audit in the same transaction) + ADR 0001-B (the resident's choice to revoke *another* of their own sessions is absolute) |
| F5 | A resident clicks **"Sign out everywhere"** (the F5 surface, extended): the F5 stamp rotation + `session.kill_all` audit row are committed (F5 unchanged), **and now also** every **other** session's `SessionDoc.RevokedAt` is set + one `session.revoke_all_but_current` audit row is committed; the **current** session's `SessionDoc.RevokedAt` stays `null` (the resident's *current* `SessionDoc` is not revoked by the per-session mechanism), but the current **browser is still signed out** (the F5 "local sign-out" behavior — the `SignInManager.SignOutAsync` + redirect). If the `kumunita.sid` cookie item is **unknown** (a session minted before this lane shipped, or a sign-in path that never hit the `OnSigningIn` hook), **all** of the resident's sessions are revoked (the "current" is not identifiable, so the all-but-current degrades to all — the same position as F5's "no stamp in the cookie" backward-compat rule, which lets the session through *until the cut-off*; here the all-but-current has no current to preserve). | C3 (the two audit rows — `session.kill_all` for the F5 whole-account kill + `session.revoke_all_but_current` for the per-session kill — are both committed, the C3 pin at the **request** level) + ADR 0001-B (the resident's choice to end *every* session is absolute) + C-S1·4 (the sign-in flow is unchanged — the F5 stamp rotation is the higher-priority gate, the per-session kill is the lower-priority one) |
| F6 | A resident's `LastActive` is touched **at most once per 60 s** (the `Kumunita:Sessions:TouchIntervalSeconds` config key, default `60`): on a request within the 60 s window since the last touch, **no** write occurs (the middleware checks the interval before calling `TouchSessionAsync`); on a request at or past the window, the `LastActive` is updated to `UtcNow`. A **revoked** session is **never** touched (the middleware / service short-circuit on `RevokedAt != null` — a revoked session's `LastActive` is frozen at its last active moment, which is exactly what the list's "most-recent-first" ordering needs to keep honest). | C-S1·5 (the rate-limit is the middleware's, not the service's) + C-S1·2 (a revoked session is not touched — the `RevokedAt != null` short-circuit) |

**FACES count: 6.** This count (and the invariant-pin per row) is the input
the next unit (U2) needs to name the seam-test list (Part 2 §2.4, 12 tests)
and the three-test acceptance gate (Part 2 §2.5) without re-deriving them.
The 12 tests in §2.4 anchor to the 6 FACES rows (multiple tests per FACE —
e.g. F4's 5 outcomes: `SetsRevokedAt`, `DoesNotTouchCurrentSession`,
`DeniesCrossUser`, `IsIdempotent`, plus the F5 `RevokesOthersKeepsCurrent`
and `RevokesOthersWhenCurrentUnknown` that straddle F4/F5).

## Drift-guard & change policy (Part 1)

- If a later unit (U3–U12) finds a mismatch between an implemented signature
  and the pin in this Part, **this doc wins**. The unit updates this file in
  the same commit and appends a one-line drift note to
  `docs/plans-milestones/in-progress/sessions/sessions-handoff-notes.md`
  (the `## U<m> — Drift pause` form, per the unit-series rule §6).
- The invariant *numbers* — ADR 0006's **C1, C3, C4, C5, C6**; the five
  lane-owned **C-S1·1, C-S1·2, C-S1·3, C-S1·4, C-S1·5**; ADR **0001-B**;
  ADR **0004 §B.1** — are stable for the rest of the lane. Adding a new
  lane-owned invariant (C-S1·6+) requires an ADR amendment plus a
  design-doc edit in the same commit; renaming or renumbering an existing
  one is a breaking change and is not allowed mid-lane.
- A new FACES row (F7+) is added only by a unit that ships the outcome it
  pins, in the same commit as the feature. The FACES count is a **handoff
  field** (U1 → U2, and forward): every unit that touches FACES updates the
  count in the handoff note.
- **The plan § U1 "11 invariants" headline** vs. the 12-item body list is a
  plan-documentation slip, not a pinned-invariant drift. The handoff note
  (U1's entry) records it so U2 — who owns the test list — confirms 12
  against the body and pins test names accordingly. U2 owns the final count
  in §2.6. (This is the **exact** slip M3's U1 hit — M3's headline said "12",
  body had 11, M3 pinned the 11 from the body; here the headline says "11",
  body has 12, this doc pins the 12 from the body. The *direction* of the
  slip is reversed, the *resolution* is identical: the body is authoritative.)
- **Seam/test names are not pinned in Part 1.** Every test name and the
  three-test acceptance gate (closed-loop / handoff / part-vs-whole) land in
  Part 2 (U2) at §2.4 / §2.5 — mirroring M3's structure. The FACES table
  above is the *input* to Part 2; Part 2's test names are the *output*.
- The **`SL·D` deferral list** in §Scope is a **frozen** set for the rest of
  the lane: a unit that lands a `SL·D` item (e.g. the `DeviceLabel` field)
  is **out of scope** for this lane and must open its own `SL·D` lane with its
  own design doc. The five deferrals are the lane's **close boundary** —
  U12's ADR 0281 records them verbatim.

## Seams & contracts (Part 2, written by U2)

### 2.0 Preambles — what this section pins, and what wins on conflict

Every C# fragment below is **exact**: parameter lists, return types, and
namespaces are the contract U3–U11 must implement against. If a later unit
discovers an implemented signature that does not exist verbatim here, the
drift-guard (§2.6) applies: **this file wins**; the unit updates this file in
the same commit and appends a drift note to
`docs/plans-milestones/in-progress/sessions/sessions-handoff-notes.md`.

Namespace conventions:

- Frozen / unchanged seam: `Kumunita.Core.Identity`
  (`IIdentityService.KillAllSessionsAsync` — the F5 seam, **unchanged** by
  this lane).
- The lane's new bounded context: `Kumunita.Core.Sessions`
  (`SessionDoc`, `SessionService`).
- The lane's registration surface: `Kumunita.Core`
  (`SessionDocTypes` — mirroring `CoreDocTypes` / `PostDocTypes` at the root).
- Web-side composition: `Kumunita.Web.Controllers` /
  `Kumunita.Web.Security` / `Kumunita.Web.Models` (never in `Kumunita.Core`).

**Count reconciliation (U1 → U2):** U1's body pins **12 invariants** (the
plan's U1 headline says "11" — a plan-documentation slip in the *reversed*
direction of M3's U1 slip [M3 headline "12", body 11]. U1 resolved it: body
wins, recorded in the handoff note, **not** a drift pause). U2 **confirms 12**
against U1's body and pins the §2.4 test list accordingly — **12 named tests**,
each anchored to an invariant id and/or FACES row from Part 1. No C-S1·6 is
introduced by this section. The 6 FACES rows (F1–F6) are the *input*; §2.4's
12 test names are the *output* (multiple tests per FACE — F4 carries 4,
F5 carries 2, F3 carries 2, F1/F2/F6 carry 1 each).

### 2.1 Frozen seam list (exact C#)

**F5 seam (frozen, unchanged by this lane).** `Kumunita.Core.Identity`
(quoted verbatim from `IIdentityService.cs`):

```csharp
// Kumunita.Core.Identity.IIdentityService — the F5 kill-all seam (AUTH-04).
// This lane does NOT modify this method. The kill-all extension (U7) calls
// KillAllSessionsAsync (the F5 path: stamp rotation + session.kill_all audit
// row) and THEN calls SessionService.RevokeAllButCurrentAsync (the per-session
// path: the SessionDoc.RevokedAt writes + the session.revoke_all_but_current
// audit row). The two are sequential in the same HTTP request; the C3
// same-transaction pin is satisfied at the request level, not the transaction
// level (see the §2.3 deviation note).
Task KillAllSessionsAsync(string subjectId);
```

**The one new Core seam (this lane's composition point).**
`Kumunita.Core.Sessions`:

```csharp
namespace Kumunita.Core.Sessions;

/// <summary>
/// The <b>sessions</b> module's single composition service (bounded context
/// <c>Kumunita.Core.Sessions</c>, ADR 0006-D lane). Composes **only**
/// <see cref="Marten.IDocumentStore"/> (the <see cref="SessionDoc"/> store);
/// it never reads <c>GroupMembership</c> / <c>DelegationGrant</c> /
/// <c>Profile</c> for its own decisions, and the <c>Kumunita.Web</c> layer
/// (the <c>SecuritySettingsController</c> + the
/// <c>SessionRevocationMiddleware</c>) is a **thin caller** of this service
/// (the M3 <c>PostService</c> pattern — the controller is route + authz +
/// shape, never a re-derivation). <see cref="SessionService"/> is the
/// **only** caller of the <see cref="SessionDoc"/> store (ADR 0006-D
/// "the authorization path is unique" analog — here the *session* path is
/// unique).
/// <para>
/// The service opens its own <c>LightweightSession()</c> for writes
/// (the <c>OnSigningIn</c> handler does not have a surrounding session to
/// join — the M3 <c>PostService.CreatePostAsync</c> pattern, but without the
/// caller-supplied <c>IDocumentSession</c> parameter). Reads use
/// <c>session.Query</c>. The C3 same-transaction pin (the revoke + the
/// audit row commit together) is satisfied **within** each service method —
/// a single <c>IDocumentSession</c> commit per method call.
/// </para>
/// </summary>
public sealed class SessionService
{
    public SessionService(Marten.IDocumentStore store);

    /// <summary>
    /// Create a new session (called from the <c>OnSigningIn</c> handler —
    /// U5). Constructs a <see cref="SessionDoc"/> with a GUID <c>Id</c>,
    /// the given <paramref name="userId"/> / <paramref name="userAgent"/> /
    /// <paramref name="ip"/>, <c>CreatedAt = UtcNow</c>,
    /// <c>LastActive = UtcNow</c>, <c>RevokedAt = null</c>; stores it in its
    /// own <c>LightweightSession()</c>; returns the stored doc. C-S1·4
    /// (the <c>OnSigningIn</c> hook is the **only** create path; if this
    /// throws, the caller — the <c>OnSigningIn</c> handler — catches + logs
    /// + continues without the <c>kumunita.sid</c> item, so the sign-in does
    /// not fail).
    /// </summary>
    public Task<SessionDoc> CreateSessionAsync(string userId, string userAgent, string ip);

    /// <summary>
    /// List the user's **active** (non-revoked) sessions, most-recent-first
    /// (ordered by <c>LastActive</c> descending). A **read** (the
    /// <c>session.Query</c> shape, not a write session). Excludes docs with
    /// <c>RevokedAt is not null</c> (C-S1·2, F3). Returns an empty list for
    /// an unknown <paramref name="userId"/> (F3).
    /// </summary>
    public Task<IReadOnlyList<SessionDoc>> GetSessionsForUserAsync(string userId);

    /// <summary>
    /// Revoke one session (owner-scoped; the controller checks
    /// <c>userId == currentSubject</c> **before** calling — C-S1·3). Loads
    /// the doc by <c>Id</c>; if <c>doc.UserId != userId</c> **or**
    /// <c>doc.RevokedAt is not null</c>, **does nothing** (the C-S1·3
    /// owner-scoped rule + the idempotency pin — a double-revoke is a no-op).
    /// Else sets <c>doc.RevokedAt = UtcNow</c> **and** appends an
    /// <c>AccessAudit</c> row (<c>targetKind "session"</c>,
    /// <c>Action "session.revoke"</c>, <c>Subject = userId</c>,
    /// <c>Object = sessionId</c>) in the **same** <c>IDocumentSession</c>
    /// (C3 same-transaction). The C-S1·1 pin (the current session is never
    /// revoked by the per-row action) is enforced by the **controller**
    /// (the 409 guard), not by this method — this method is the dumb write;
    /// the controller is the smart gate.
    /// </summary>
    public Task RevokeSessionAsync(string userId, string sessionId);

    /// <summary>
    /// Revoke all-but-current (the kill-all extension; the current session's
    /// <c>SessionDoc.RevokedAt</c> stays <c>null</c>). Loads the user's
    /// active sessions (the <c>GetSessionsForUserAsync</c> query); for each
    /// doc where <c>doc.Id != currentSessionId</c>, sets
    /// <c>doc.RevokedAt = UtcNow</c>; **and** appends **one**
    /// <c>AccessAudit</c> row (<c>targetKind "session"</c>,
    /// <c>Action "session.revoke_all_but_current"</c>,
    /// <c>Subject = userId</c>, <c>Object = currentSessionId</c>) in the
    /// **same** <c>IDocumentSession</c> as the bulk writes (C3
    /// same-transaction). If <paramref name="currentSessionId"/> is
    /// <c>string.Empty</c> (the <c>kumunita.sid</c> cookie item was not
    /// present — a session minted before this lane shipped), **all** active
    /// sessions are revoked (the all-but-current degrades to all — the
    /// "no stamp in the cookie" backward-compat rule, the
    /// <c>RevocationStampMiddleware</c> precedent).
    /// </summary>
    public Task RevokeAllButCurrentAsync(string userId, string currentSessionId);

    /// <summary>
    /// Touch the session's <c>LastActive</c> to <c>UtcNow</c>. If
    /// <c>doc.RevokedAt is not null</c>, **does nothing** (a revoked session
    /// is not touched — C-S1·2, F6). The C-S1·5 rate-limit (at most once per
    /// 60 s per session) is enforced by the **caller** (the
    /// <c>SessionRevocationMiddleware</c> checks the interval before calling
    /// this method), not by the service (the service is the dumb write; the
    /// middleware is the smart gate).
    /// </summary>
    public Task TouchSessionAsync(string sessionId);
}
```

### 2.2 New lane-owned Core types (exact C#)

Namespace `Kumunita.Core.Sessions`. The `SessionDoc` is a **Marten-native**
POCO with the conventional `string Id` identity (ADR 0004 §B.1; the carve-out
for a hand-rolled `FeatureSchemaBase` is **not** used in this lane — the
`SessionDoc` is the **only** new document, registered in the new
`SessionDocTypes` surface, mirroring `CoreDocTypes` / `PostDocTypes`).

```csharp
namespace Kumunita.Core.Sessions;

/// <summary>
/// A resident's sign-in session (the <b>sessions</b> lane). <c>RevokedAt</c>
/// is the **active/revoked** discriminator: <c>null</c> = active, set =
/// revoked (C-S1·2 — a revoked session is immediately invalid on the next
/// request; C-S1·5 — a revoked session's <c>LastActive</c> is frozen).
/// <c>UserId</c> is the owner-scoping field (C-S1·3 — a non-owner cannot
/// revoke; the controller + the service both check
/// <c>doc.UserId == currentSubject</c>). The <c>Id</c> is a conventional
/// <c>string</c> (a GUID, minted by <c>SessionService.CreateSessionAsync</c>)
/// — no <c>Identity(...)</c> override (ADR 0004 §B.1 Marten-native rule;
/// <c>SessionDocTypes</c> registers <c>opts.Schema.For&lt;SessionDoc&gt;()</c>
/// with no business-key index — the <c>(UserId, RevokedAt)</c> index is a
/// <c>SL·D</c> candidate, not in this lane).
/// </summary>
public sealed class SessionDoc
{
    public string    Id         { get; set; } = "";    // GUID, the session id
    public string    UserId     { get; set; } = "";    // the resident's User.Id
    public string    UserAgent  { get; set; } = "";    // the sign-in UA string
    public string    Ip         { get; set; } = "";    // the sign-in client IP
    public DateTime  CreatedAt  { get; set; }          // the sign-in time (UTC)
    public DateTime  LastActive { get; set; }          // the last-touch time (UTC)
    public DateTime? RevokedAt  { get; set; }          // null = active; set = revoked
}
```

**All fields are non-nullable except `RevokedAt`** (the active/revoked
discriminator). The `Id` is a conventional `string` (no `Identity(...)`
override). The `SessionDocTypes.Configure(StoreOptions)` registration
(U3 lands this, mirroring `CoreDocTypes` / `PostDocTypes` at the
`Kumunita.Core` root):

```csharp
namespace Kumunita.Core;

/// <summary>
/// The <b>sessions</b> lane's Marten-native document registration surface
/// (ADR 0004 §B.1). Mirrors <c>CoreDocTypes</c> / <c>PostDocTypes</c>
/// exactly: the <see cref="Sessions.SessionDoc"/> POCO uses the conventional
/// <c>string Id</c> identity and needs no <c>Identity(...)</c> or
/// <c>UniqueIndex(...)</c> call — Marten's defaults apply. The
/// <c>(UserId, RevokedAt)</c> index is a <c>SL·D</c> candidate, **not** in
/// this lane.
/// </summary>
public static class SessionDocTypes
{
    public static void Configure(Marten.StoreOptions opts)
    {
        opts.Schema.For<Sessions.SessionDoc>();
    }
}
```

Wired by U3 into both boot paths:
`Kumunita.Core/Bootstrap/SchemaBootstrap.cs` (called from
`ApplyAllConfiguredChangesToDatabaseAsync`) and `Kumunita.Web/Program.cs`
(the dev-loop + all-env paths, adjacent to `CoreDocTypes.Configure` +
`PostDocTypes.Configure`).

### 2.3 Revoke rule (the two tables)

**Per-row revoke — the 5-state table for
`POST /settings/security/sessions/{id}/revoke`.** The controller (U7) reads
`currentSessionId` from the `kumunita.sid` cookie item
(`HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme)` →
`result.Properties.Items["kumunita.sid"]`), resolves `userId`
(`KumunitaPrincipal.SubjectId`), and dispatches:

| # | Caller state | Response | Pin |
|---|---|---|---|
| 1 | Unauthenticated (no principal) | **401** (the `[Authorize]` challenge — Core never sees an empty subject) | — |
| 2 | `id == currentSessionId` | **409** — the per-row action **refuses** to revoke the current session. The resident is told: "This is the session you are using — use 'Sign out everywhere' to end it." (the C-S1·1 pin — the per-row button is **absent** on the current row in the view, F4) | C-S1·1, F4 |
| 3 | `id != currentSessionId` but the `SessionDoc` belongs to a **different** user (`doc.UserId != currentSubject`) | **404** — not a 403, to avoid leaking the existence of another user's session ids. The service (`RevokeSessionAsync`) re-checks `doc.UserId != userId` and no-ops (the C-S1·3 two-layer rule) | C-S1·3, C5, F4 |
| 4 | `id` is not a known `SessionDoc` (revoked, deleted, or never existed) | **404** — idempotent: a double-revoke is a no-op (the service sees `RevokedAt is not null` and does nothing) | F4, idempotency |
| 5 | `id != currentSessionId` + `doc.UserId == currentSubject` + `doc.RevokedAt is null` | **204** — `SessionDoc.RevokedAt` is set to `UtcNow` + the `session.revoke` audit row is committed in the **same** `IDocumentSession` (C3). The revoked session is **invalid on its next request** (C-S1·2, C4) | C3, C-S1·2, C4, F4 |

**Kill-all extension — the 4-shape table for
`POST /settings/security/sessions/kill-all`.** The existing F5 action (U7
**extends** it — not replaces) now calls
`SessionService.RevokeAllButCurrentAsync(userId, currentSessionId)` **after**
the existing `await identity.KillAllSessionsAsync(userId);` call (the F5
path) and **before** the `await signInManager.SignOutAsync();` + redirect (the
F5 "local sign-out" behavior):

| # | Shape | What happens | Pin |
|---|---|---|---|
| 1 | **Current session** (`doc.Id == currentSessionId`) | `SessionDoc.RevokedAt` **stays `null`** — the per-session mechanism does **not** revoke it. The F5 stamp rotation + `SignInManager.SignOutAsync` still signs out the current browser (the F5 "local sign-out" behavior, **unchanged**). The resident is signed out of the current browser but their `SessionDoc` row is preserved (the list on their next sign-in will show the new session, not this one). | F5, C-S1·4 |
| 2 | **Every other session** (`doc.Id != currentSessionId`, `RevokedAt is null`) | `RevokedAt` set to `UtcNow`; **one** `AccessAudit` row (`Action = "session.revoke_all_but_current"`, `targetKind = "session"`, `Subject = userId`, `Object = currentSessionId`) committed in the **same** `IDocumentSession` as the bulk writes (C3 same-transaction — **within** the `RevokeAllButCurrentAsync` call). | C3, F5 |
| 3 | **`kumunita.sid` unknown** (session minted before this lane shipped, or a sign-in path that never hit `OnSigningIn`) | The all-but-current **degrades to all**: no `currentSessionId` to preserve (the `RevokeAllButCurrentAsync` method treats `string.Empty` / missing as "no current"), so **every** active session's `RevokedAt` is set + the same `session.revoke_all_but_current` audit row. The F5 stamp rotation still signs out the current browser. The backward-compat rule mirrors the `RevocationStampMiddleware` "No stamp in the cookie" path (the session passes through until the next sign-in mints a `kumunita.sid`; here the all-but-current has no current to preserve, so it revokes everything). | F5, C-S1·4 |
| 4 | **F5 `session.kill_all` audit row** (the existing F5 path, **unchanged**) | The F5 `KillAllSessionsAsync` call (the stamp rotation + the `session.kill_all` audit row, `via: Owner`) is committed in the **same HTTP request** as the kill-all extension. The two rows together — `session.kill_all` (the F5 whole-account kill) + `session.revoke_all_but_current` (the per-session kill) — answer "who did this, by what right, to which sessions." | C3 (request-level), F5 |

> **C3 same-transaction deviation note:** the `KillAllSessionsAsync` call
> (Identity/EF path — the stamp rotation + the `session.kill_all` audit row)
> and the `RevokeAllButCurrentAsync` call (Marten path — the
> `SessionDoc.RevokedAt` writes + the `session.revoke_all_but_current` audit
> row) are **sequential in the same action method**, but the
> `SessionService` opens its **own** `LightweightSession()` (it is not given
> a caller-supplied `IDocumentSession` to join — the `OnSigningIn` handler
> does not have a surrounding session to join, and the kill-all extension
> runs in the controller's request scope). The two commits are therefore in
> **separate transactions** within the **same request**. The C3 pin
> ("the audit row commits in the same transaction as the domain write") is
> satisfied **within** each service method (the `RevokeSessionAsync` and
> `RevokeAllButCurrentAsync` methods each commit their write + their audit
> row in a single `IDocumentSession`), and at the **request** level for the
> two-mechanism kill-all case (the F5 row + the per-session row are both
> committed before the response is sent). This is a **documented deviation**
> from the strict "one transaction" reading — the F5 stamp rotation is in the
> Identity/EF module, not the Marten module, and the two modules do not share
> a transaction. The ADR 0281 (U12) records this.

### 2.4 Pinned seam tests (exact names)

File: `tests/Kumunita.Core.Tests/SessionServiceTests.cs`. All **12** names
below are **pinned** by this section (U10 is responsible for the file per the
plan register; U10 may add their own *named* tests to the same file but must
not rename these). Each name carries the FACES row and/or invariant anchor
from Part 1. The **12 tests anchor to the 6 FACES rows** (multiple tests per
FACE — F4 carries 4, F5 carries 2, F3 carries 2, F1/F2/F6 carry 1 each):

| # | Test name | FACES | Invariant pin |
|---|---|---|---|
| 1 | `F1_CreateSession_ReturnsDocWithGuidIdAndTimestamps` | F1 | C-S1·4 (the `OnSigningIn` hook is the only create path; the guard on `CreateSessionAsync` failure) + ADR 0001-B (the resident's sign-in is not gated by the lane) |
| 2 | `F2_GetSessionsForUser_ReturnsOnlyActiveSessionsMostRecentFirst` | F2 | C-S1·4 (the list is the user's own sessions) + C-S1·3 (owner-scoped) |
| 3 | `F3_GetSessionsForUser_ExcludesRevokedSessions` | F3 | C-S1·2 (revoked = immediately invalid, excluded from the list) + C-S1·3 (owner-scoped) |
| 4 | `F3_GetSessionsForUser_ReturnsEmptyForUnknownUser` | F3 | C-S1·3 (owner-scoped — a non-owner probing the list for another user's `userId` is not a surface; the list is keyed by the current subject) |
| 5 | `F4_RevokeSession_SetsRevokedAtToUtcNow` | F4 | C3 (the `session.revoke` audit row is committed in the same transaction) + C-S1·2 (the revoked session is invalid on the next request) |
| 6 | `F4_RevokeSession_DoesNotTouchCurrentSession` | F4 | C-S1·1 (the current session is never revoked by the per-row action — the controller's 409 guard; the service does not enforce this, the controller does) |
| 7 | `F4_RevokeSession_DeniesCrossUser` | F4 | C-S1·3 (owner-scoped; cross-user = 404 at the controller; the service no-ops when `doc.UserId != userId`) + C5 (no moderator surface — a GlobalAdmin probing another resident's session id gets the same 404) |
| 8 | `F4_RevokeSession_IsIdempotent` | F4 | C-S1·2 (a double-revoke is a no-op — the second call sees `RevokedAt is not null` and does nothing) |
| 9 | `F5_RevokeAllButCurrent_RevokesOthersKeepsCurrent` | F5 | C3 (the `session.revoke_all_but_current` audit row + the bulk writes in the same transaction) + ADR 0001-B (the resident's choice to end every *other* session is absolute) + C-S1·4 (the current `SessionDoc` is preserved) |
| 10 | `F5_RevokeAllButCurrent_RevokesOthersWhenCurrentUnknown` | F5 | C-S1·4 (the all-but-current degrades to all when `kumunita.sid` is unknown — the backward-compat rule, the `RevocationStampMiddleware` "No stamp in the cookie" precedent) + C3 (the audit row is still committed) |
| 11 | `F6_TouchSession_UpdatesLastActiveWhenDue` | F6 | C-S1·5 (the rate-limit is the middleware's; the service is the dumb write — `TouchSessionAsync` sets `LastActive = UtcNow` when called) |
| 12 | `F6_TouchSession_SkipsWhenNotDue` | F6 | C-S1·5 (the middleware checks the interval before calling `TouchSessionAsync`) + C-S1·2 (a revoked session is **never** touched — the `RevokedAt is not null` short-circuit) |

> **Note to U10 (unit number from the plan register):** U10 owns the file
> `tests/Kumunita.Core.Tests/SessionServiceTests.cs` and its 12 `[Fact]`s
> (the plan names U10 "seam tests (the 12 pinned names) + the kill-all
> extension test"); U11 records the three-test gate (§2.5). §2.6's gate names
> reference this list — **renaming any of the 12 after U10 owns them is a
> drift event** (§2.6).

### 2.5 Acceptance gate (U11 records)

The three-test shape (mirroring M3 §2.5 verbatim; the sessions lane's
*handoff* lane is the per-session revoke → next-request invalidation arrow,
not a second `Can*` call):

| # | Test | Shape (sessions lane's reading) |
|---|---|---|
| 1 | **Closed-loop** | A resident **signs in** → a `SessionDoc` row exists (F1) → the resident's session list shows it (F2) → the resident revokes **another** session (F4) → the revoked session is **invalid on its next request** (C-S1·2, C4 — the `SessionRevocationMiddleware` signs out the holder; the F5 `RevocationStampMiddleware` is the precedent for the same next-request enforcement). |
| 2 | **Handoff** | The kill-all extension (F5) revokes **all-but-current**: the current session's `SessionDoc.RevokedAt` stays `null` (the per-session mechanism does not revoke it), but the F5 stamp rotation + `SignInManager.SignOutAsync` signs out the current browser (the F5 "local sign-out" behavior, unchanged). A **password change** still invalidates all sessions via the F5 stamp rotation (the higher-priority gate — a stamp-rotated session is revoked regardless of its `SessionDoc.RevokedAt`). The handoff is: the two mechanisms (F5 stamp + this lane's `SessionDoc.RevokedAt`) **coexist** and are **independent**; a resident can revoke one session (this lane) or all (F5 + this lane), and the current session is protected by the C-S1·1 guard in the per-row path and by the "all-but-current" rule in the kill-all path. |
| 3 | **Part-vs-whole** | The 12-test list in §2.4 is the **whole**; the closed-loop + handoff tests are the **parts**; all 12 — plus the kill-all extension test (`SessionsKillAllExtTests.KillAllExt_RevokesOthersKeepsCurrent`) — must pass together for the sessions lane's gate to record. U11's record cites the *actual* landed test names (drift lane: if a U2-pinned test name did not land verbatim, U11 renames the *test file / `[Fact]`* in the same commit and records a one-line drift in the handoff note — mirroring M3 §2.5's own handling). |

**The gate is recorded by U11** (per the plan register: U10 is "seam tests
(12 + 1)"; U11 is "run + record the lane's acceptance gate + the F5 e2e pass
count"). U12 (the ADR 0281 + `WhatsNew.cs` row + `ARCHITECTURE.md` flip +
the `SL·D` deferral note) is the close unit, the M3 U12 analog.

### 2.6 Drift-guard (frozen once written)

The following pins are **frozen** in this doc, and any violation is a `## U<m>
— Drift pause` per unit-series rule §6 (unit-series rule in the plan):

- The **12** invariants from Part 1 (C-S1·1/2/3/4/5, ADR 0006 C1/C3/C4/C5/C6,
  ADR 0001-B, ADR 0004 §B.1) — the *numbers* (not the prose) are stable for
  the rest of the sessions lane; rename / renumber is a breaking change
  (Part 1 already pins this; it repeats here because Part 2's own §2.4 /
  §2.5 names hang off them). The plan's U1 "11 invariants" headline is a
  plan-documentation slip (the body has 12); U2 **confirms 12** — the
  *reversed* direction of M3's U1 slip (M3 headline "12", body 11; M3 pinned
  11 from the body). The body is authoritative in both cases.
- The **6** FACES rows (F1–F6) in Part 1 — FACES *count* is a handoff field
  (U1 → U2, and forward); a new FACES row (F7+) is added **only** by the
  unit that ships the outcome it pins, in the same commit as the feature
  (Part 1 already pins this).
- **`IIdentityService.KillAllSessionsAsync(string subjectId)`** — the F5
  seam, **unchanged** by this lane. This lane **calls** it (the kill-all
  extension, U7); it does not modify it. Adding, removing, or re-scoping this
  method is a drift event in **both** lanes (the F5 lane owns the seam; the
  sessions lane is a caller).
- The **`SessionDoc`** 7-field shape (Id, UserId, UserAgent, Ip, CreatedAt,
  LastActive, RevokedAt?) — frozen once U3 lands `SessionDoc`. The
  `RevokedAt?` nullable (the active/revoked discriminator) and the
  **absence** of a `DeviceLabel` / `Kiosk` / `Trust` field (the `SL·D`
  deferrals — Part 1 §Scope) are frozen; adding any of them is an `SL·D`
  drift event (a new lane, not this one).
- The **`SessionDocTypes.Configure`** one-line shape
  (`opts.Schema.For<SessionDoc>();`) — frozen once U3 lands it. A
  business-key index (`(UserId, RevokedAt)`) is a `SL·D` candidate, **not**
  in this lane; adding one is a drift event.
- The **`SessionService`** ctor `(IDocumentStore)` and the 5 public methods
  (`CreateSessionAsync`, `GetSessionsForUserAsync`, `RevokeSessionAsync`,
  `RevokeAllButCurrentAsync`, `TouchSessionAsync`) — the **frozen Core
  surface**, named + signatures, frozen once U4 (the defining unit, per the
  plan register) lands them. Renaming a public method or reshaping a
  signature is a drift event (U4 owns the shapes; U5/U6/U7 consume them, no
  re-shaping; U10 tests against them).
- The **`kumunita.sid`** cookie item key (the exact string — the session id,
  a GUID string, set in the `OnSigningIn` handler alongside the existing
  `kumunita.stamp`) — frozen once U5 lands it. The key is **not** a claim
  type (the F5 `kumunita.stamp` precedent — the stamp is a non-claim
  property on the cookie's `AuthenticationProperties`, not a claim in
  `ClaimShaping.Build`). Renaming the key or moving it to a claim is a drift
  event.
- The **`Kumunita:Sessions:TouchIntervalSeconds`** config key (the exact
  string; default `60`) — frozen once U6 lands it in `appsettings.json`. The
  rate-limit is enforced by the **middleware** (the smart gate), not the
  service (the dumb write). Renaming the key or moving the rate-limit into
  the service is a drift event (C-S1·5 pin).
- The **§2.3** two tables (the 5-row per-row-revoke table + the 4-shape
  kill-all table) — frozen once written (U2's commit); a new row is a
  drift event **or** a new FACES row (F7+), whichever the unit's change
  actually pins, in the same commit + the same drift note.
- The **12 test names** in §2.4 — frozen once U10 owns the file (the unit
  named in §2.4 "Note to U10"). Renaming or re-scoping a name is a
  drift event; the unit updates §2.4 in the same commit and appends a drift
  note to the handoff.
- The **three-test acceptance gate** in §2.5 (closed-loop / handoff /
  part-vs-whole) — frozen once written (U2's commit); the gate names
  reference the 12 test names + the kill-all extension test. U11 records the
  pass/red status; the gate *names* are frozen.
- The **C3 same-transaction deviation note** in §2.3 (the kill-all extension's
  two commits are in separate transactions within the same request — the F5
  Identity/EF path and the sessions Marten path do not share a transaction)
  — frozen once written. A unit that finds a way to make the two commits
  share a transaction (e.g. a shared `IDocumentSession`) is a drift event in
  the **positive** direction (the C3 pin is strengthened, not weakened) and
  must record the change in ADR 0281 (U12).

> **U2 records, here, the `11 vs 12` plan-documentation slip as closed:**
> the **12** invariants (not 11) are the pin, confirmed against U1's body.
> The **12** test names (not a smaller or larger set) are the pin. The **2**
> tables (5 + 4 rows) are the pin. The **12 + 6 + 12 + 5 + 4** (and the
> 7-field `SessionDoc`, 5-method `SessionService`, 1-line
> `SessionDocTypes`, `kumunita.sid` cookie key,
> `Kumunita:Sessions:TouchIntervalSeconds` config key, 3-test gate) are the
> *frozen counts* of Part 2. The F5 `KillAllSessionsAsync` seam is **unchanged**
> (the sessions lane is a caller, not a modifier). The C3 same-transaction
> deviation (the kill-all extension's two-mechanism case) is **documented**,
> not a violation.
