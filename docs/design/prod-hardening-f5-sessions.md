# F5 — AUTH-04: session kill-all + invalidation — Part-0 design pin

> **Fix:** F5 (report row **AUTH-04**) — kind **`code`**, **dep F3 (complete)**.
> **Status:** this is the **Part-0 design pin** (F5.U1). It is **design-only**:
> **no code, no build, no `.cs` touched, no `<PackageReference>`, no migration,
> no `.slnx` change.** F5.U2 lands the kill-all service, F5.U3 the
> `/settings/security/sessions` surface, F5.U4 the two pinned tests.
> **Why it's a FAIL today:** a resident who changes their password (or who is
> told to "sign out everywhere") is **not** actually signed out of their other
> live sessions — the security-stamp invalidation that `UpdateSecurityStampAsync`
> is *supposed* to trigger is neutered in this codebase (see §1), and there is
> **no kill-all surface** at all.

> **Repo doctrine applied (from `AGENTS.md`):** *"Thin token, fat authorization:
> 'may this actor see that resource?' is resolved by the `Authorization` service
> per request, not encoded in an identity claim."* F5's invalidation mechanism
> must honor that: **server-side state + per-request resolution**, not a cookie
> claim the resident controls. Every claim about the two-factor / session API
> below is grounded in the **build-verified `Microsoft.AspNetCore.Identity
> 10.0.13` surface the F3.U2 build-verification recorded**
> (`hardening-handoff-notes.md` → `## F3.U2`) or in an in-repo call site the green
> build proves compiles — **not from memory**.

---

## 1. Current state (grounded, not assumed)

### 1a. The 14-day `ValidationInterval` — and why it exists

- **The value:** `Program.cs:478-481` —
  `builder.Services.Configure<SecurityStampValidatorOptions>(o => { o.ValidationInterval = TimeSpan.FromDays(14); });`.
- **Why it was raised (the in-repo rationale, quoted):** `Program.cs:458-477`
  documents that ASP.NET Identity "automatically wires a `SecurityStampValidator`
  into the `.AspNetCore.Identity.Application` cookie, with a default
  `ValidationInterval` of 30 minutes. Every 30 min … the validator fires and
  re-reads the user from the store by the `NameIdentifier` claim … The custom
  `KumunitaClaimsPrincipalFactory` … mints the identity with a **name-type of
  null** (not `NameIdentifier`, by design — ADR 0006-B no-relational-data
  invariant), so `userManager.FindByIdAsync(...)` **returns null** on every
  validation pass, and the validator then calls
  `context.RejectPrincipal()` + `SignInManager.SignOutAsync()` … That is the
  **'I keep getting logged out after a while' bug.** The right remedy here is to
  raise `ValidationInterval` past the cookie's own `ExpireTimeSpan` (14 days) so
  the validator can never fire inside a live session, rather than to disable it
  outright (disable = every cookie is trusted forever, even after a real
  `UpdateSecurityStampAsync` admin-action that we still want to honor)."
- **The root cause (quoted from the code):** `ClaimShaping.cs:57` —
  `var identity = new ClaimsIdentity(subjectId, null, ClaimTypes.Role);` — the
  second positional arg (`nameType`) is **`null`**, and the subject is a
  **Kumunita custom** claim type, `ClaimShaping.cs:59` —
  `identity.AddClaim(new Claim(ClaimTypes.Subject, subjectId));` (where
  `ClaimTypes.Subject` is `Kumunita.Core.Identity.ClaimTypes.Subject`, **not**
  the BCL `System.Security.Claims.ClaimTypes.NameIdentifier`). The admissible
  claim set is **exactly** Subject / ExternalId / Verified / Role
  (`ClaimShaping.cs:57-66`); there is **no `SecurityStamp` claim** in the cookie
  and **no `NameIdentifier`**.
- **The factory:** `KumunitaClaimsPrincipalFactory.cs:62-68` —
  `return ClaimShaping.Build(subjectId: user.Id ?? string.Empty, …);` — confirms
  the sign-in principal is the `ClaimShaping.Build` output above, i.e. null
  name-type + a custom Subject claim + **no stamp claim**.

> **Consequence (the crux of F5):** the `SecurityStampValidator` **cannot**
> resolve this principal by id (it reads the default `NameIdentifier` claim type,
> which is absent → `FindByIdAsync` → null), **and** the cookie carries no stamp
> to compare. So the two standard levers the plan names — "revert the interval
> so `UpdateSecurityStampAsync` fires" and "a per-user security-stamp bump" —
> are **both inert in this codebase today** unless we either (a) re-mint a
> standard `NameIdentifier` + a `SecurityStamp` claim (an **ADR 0006-B conflict**),
> or (b) switch to the repo's *actual* invalidation pattern (per-request
> server-side resolution — §2, Option B). This is the design decision this pin
> resolves.

### 1b. `UpdateSecurityStampAsync` is already called — but is a no-op for other sessions

- **The password-change lane** (the F5 FAIL's "password change doesn't invalidate
  other sessions") already rotates the stamp: `IdentityService.cs:1472-1477` —
  `var user = await userManager.FindByIdAsync(subjectId) ?? throw …;` then
  `await userManager.UpdateSecurityStampAsync(user);      // invalidate every existing session`.
  The comment asserts the intent, but **§1a proves the effect is neutered**: the
  validator never fires (14-day interval) *and* the cookie has no stamp claim to
  reject. So the comment's promise is **aspirational, not operative**, in this
  codebase.
- **Other in-repo call sites** (all `UserManager.UpdateSecurityStampAsync`, all
  compiling in the green build — the build is the evidence):
  `IdentityService.cs:358`, `IdentityService.cs:437`, `IdentityService.cs:1412`,
  `Kumunita.Core/Portability/PortabilityApplyIdentity.cs:122`. These are the
  **exact seam F5 reuses** — the stamp-rotation write already lives in
  `Kumunita.Core/Identity/IdentityService.cs` (Core stays HTTP-free), not in a
  controller.
- **Security-stamp / two-factor members grounded in the F3.U2 build-verified
  surface** (`hardening-handoff-notes.md` → `## F3.U2` "Kept unchanged" list, all
  ①-verified against 10.0.13): `ValidateTwoFactorSecurityStampAsync`,
  `GetTwoFactorAuthenticationUserAsync` (the leg-2 pending-user read-back),
  `PasswordSignInAsync`, `IsTwoFactorEnabledAsync`, `Result.RequiresTwoFactor`,
  `SignInOrTwoFactorAsync`. And the leg-2 completion members (③ U2-build-verified):
  `SignInManager.TwoFactorAuthenticatorSignInAsync(code, isPersistent,
  rememberClient)` (code-first, **no user** — the user comes from the pending
  `TwoFactorUserIdScheme` cookie) and
  `SignInManager.TwoFactorRecoveryCodeSignInAsync(recoveryCode)` (**1-arg, no
  user**). `SignInManager.SignOutAsync` clears **both** the
  `.AspNetCore.Identity.Application` **and** the `kumunita.auth` schemes
  (`AccountController.cs:978-982` `Logout` — "clears BOTH of the app's cookies").

### 1c. The repo's *actual* live-session invalidation pattern (the pattern F5 should follow)

- **`PrivilegedStampMiddleware`** (`src/Kumunita.Web/Security/PrivilegedStampMiddleware.cs`)
  is the in-repo precedent for "invalidate a live session by server-side
  re-read": its doc (lines 1-50) states "the `SecurityStampValidator` is
  **intentionally disabled** within live sessions (its `ValidationInterval`
  equals the 14-day cookie lifetime, `Program.cs`)." On every request from a
  privileged principal it re-reads the user's current role set **from the
  database** and, if it differs from the cookie's role claims,
  "the middleware **signs the user out** and redirects to the login page."
  It resolves the user by the **custom** Subject claim
  (`PrivilegedStampMiddleware.cs:78` — `context.User.FindFirst(ClaimTypes.Subject)?.Value`)
  and `FindByIdAsync` (`:91`). This is the "fat authorization, per-request
  resolution" pattern (AGENTS.md) — **the correct home for a kill-all in this
  codebase**, not the disabled `SecurityStampValidator`.
- **`BlockedAccountMiddleware`** (`src/Kumunita.Web/Security/BlockedAccountMiddleware.cs:88`)
  is the sibling precedent: a per-request `Profile.Blocked` check that signs out
  (clearing "the `kumunita.auth` cookie and the auxiliary
  `.AspNetCore.Identity.Application`" cookie) for **all** users. A kill-all gate
  for *all* users is the same shape, one step broader.

### 1d. The F3 MFA leg flow (F5 must interact with it, not change it)

Grounded in `src/Kumunita.Web/Controllers/AccountController.cs` (the F3.U4 seam,
the `## F3.U4` handoff section):

- **Leg 1** — `AccountController.cs:721-723`:
  `signInManager.PasswordSignInAsync(user, model.Password, isPersistent: false, lockoutOnFailure: true)`.
  For an MFA account + correct password this returns `SignInResult.TwoFactorRequired`
  (`Succeeded == false`, `RequiresTwoFactor == true`) and **mints the pending
  `TwoFactorUserIdScheme` cookie** (the temp, non-app sign-in) — **no app cookie yet**.
- **Leg 1.5** — `AccountController.cs:742-775`: `if (result.RequiresTwoFactor)` →
  render the MFA challenge view (`model.MfaPending = true`). The resident now holds
  the **pending `TwoFactorUserIdScheme` cookie** but is **not** signed in to the app.
  This is the **half-signed-in window** F5 must not let a kill-all leave dangling.
- **Leg 1.5b** — `AccountController.cs:799-812`: `GlobalAdmin` + MFA off →
  `RedirectToAction("Mfa", "SecuritySettings")` (enrollment; the resident IS signed
  in here, so no `SignOutAsync`).
- **Leg 1.5c** — `AccountController.cs:826-828`: the **persistent** sign-in for
  non-MFA users (re-mints the app cookie).
- **Leg 2** (`LoginMfa`) — `AccountController.cs:908-956`: completes the sign-in via
  `TwoFactorRecoveryCodeSignInAsync(…)` (`:933`) **or**
  `TwoFactorAuthenticatorSignInAsync(…)` (`:943`) — **both read the pending user back
  from the `TwoFactorUserIdScheme` cookie** (no `user` arg, F3.U2-verified); on a
  wrong code they `SignOutAsync` (`:956`) and re-render the challenge.
- **The F3 interplay risk F5 pins:** a kill-all that only bumps a server-side stamp
  for *completed* sessions does **not** clear a resident's **pending**
  `TwoFactorUserIdScheme` cookie. If that resident then completes leg 2 (presents a
  valid TOTP / recovery code), they get a **fresh app cookie** *after* being "killed"
  — a half-signed-in state surviving the kill. F5's gate **must** reject a leg-2
  completion whose pending sign-in predates the kill-all (§2, Option B, leg-2 gating).

### 1e. The F3 `SecuritySettingsController` shape F5 reuses

- **The controller** — `src/Kumunita.Web/Controllers/SecuritySettingsController.cs`:
  `[Authorize]` at `/settings/security` (the `MessagingSettingsController`
  settings-tab shape, `LocaleController` / `KioskController` idiom, ADR 0080).
  The resolve-user seam is `ResolveUserAsync()` (`:85-94`):
  `var subject = KumunitaPrincipal.SubjectId(User); … return await userManager.FindByIdAsync(subject);`.
  The enrollment actions are `Mfa()` `GET /settings/security/mfa` (`:150-190`) and
  `MfaEnable` `POST /settings/security/mfa` (with `[ValidateAntiForgeryToken]`).
  **F5's `/settings/security/sessions` surface is a sibling of these actions** (same
  controller, same `[Authorize]`, same `ResolveUserAsync`, same anti-forgery +
  rate-limit discipline) — see §3.

### 1f. The two pinned tests (from the plan, restated as the exit contract's names)

- **`PasswordChange_InvalidatesOtherSessions`** — a password change (the
  `IdentityService` lane at `IdentityService.cs:1455-1490`, which calls
  `UpdateSecurityStampAsync` at `:1477`) invalidates the user's **other** live
  sessions (a second, still-signed-in session is rejected on its next request).
- **`KillAllSessions_SignsOutAll`** — the `/settings/security/sessions` kill-all
  invalidates **all** of the user's sessions, **including** a pending F3 leg-1
  `TwoFactorUserIdScheme` cookie that is then **rejected at leg 2** (the §1d
  interplay).

---

## 2. The invalidation mechanism — **both options + a recommendation** (the design decision this pin resolves)

The plan's literal premise was: *"revert the 14-day `ValidationInterval` so
`UpdateSecurityStampAsync` / security-stamp rotation fires inside live sessions."*
**§1a/§1c prove that revert is not viable in this codebase as-is.** Per the
unit's explicit instruction ("*… or record with evidence why it can't be*"; and
"*If you find the `ValidationInterval` revert would break the F3 two-leg sign-in
(the pending-cookie window), that is a design decision to pin with both options +
a recommendation, not a silent choice*"), this pin records **both** options and a
**recommendation**, and does **not** silently choose. **F5.U2 must wait for the
owner to pick before it lands code** (§8 drift note).

### Option A — revert the `ValidationInterval` (the plan's literal premise)

**What it would take to actually work here** (not just set the interval):
1. Re-mint a standard `NameIdentifier` claim (or set
   `IdentityOptions.ClaimsIdentity.UserIdClaimType` to Kumunita's custom
   `Kumunita.Sub`) so the validator's `FindByIdAsync` resolves the user.
2. Add a `SecurityStamp` claim to the sign-in principal so the validator has
   something to compare (`ClaimShaping.Build` currently mints only
   Subject/ExternalId/Verified/Role — `ClaimShaping.cs:57-66`).
3. Lower `ValidationInterval` (`Program.cs:478-481`) below the cookie lifetime so
   the validator fires inside live sessions.
4. Gate leg-2 (§1d) so a pending `TwoFactorUserIdScheme` cookie held across a
   kill-all is rejected at completion.

**Why it is NOT recommended (grounded evidence):**
- **ADR 0006-B conflict.** Adding a `SecurityStamp` claim to the admissible claim
  set breaks the "exactly `ClaimTypes.All` (subject, externalId, verified, role)"
  invariant that `ClaimShaping` (`:6-20`) and the invariant-set-B tests assert.
  `ClaimShaping.cs:57`'s null name-type is a **deliberate** choice (the comment at
  `:44-56` explains it is what makes `IsAuthenticated == true` without a
  relational claim). Re-minting standard claims re-opens exactly the seam the
  factory was written to close.
- **It does not cover the F3 pending-cookie window.** The validator operates on
  the app scheme; a pending `TwoFactorUserIdScheme` cookie is a different scheme.
  A stamp bump would not clear it, and leg 2 would still complete (§1d) — the
  half-signed-in state F5 must eliminate **remains** unless a separate leg-2 gate
  is added, at which point Option A has already needed the same leg-2 work as
  Option B.
- **It re-introduces the documented bug if done partially.** If only the interval
  is lowered (steps 1–2 not done), `FindByIdAsync` returns null →
  `RejectPrincipal` + `SignOutAsync` for **every** authenticated user every
  interval — the "I keep getting logged out after a while" bug the 14-day
  remediation exists to prevent (`Program.cs:465-477`).

**Verdict: not recommended.** It is *recorded* here so the owner can choose it;
if chosen, it is a **new ADR** (an ADR 0006-B amendment), not a silent change
(§7 non-goal).

### Option B — keep the 14-day interval; kill-all via a server-side revocation stamp + per-request gate (repo-consistent) — **RECOMMENDED**

**Mechanism (pinned):**
- **Server-side revocation stamp.** A per-user invalidation mark in `Kumunita.Core`
  (the `User` Identity row, or a small token-store row — **F5.U2 pins which**). The
  password-change lane **already** rotates the stamp
  (`IdentityService.cs:1477` `UpdateSecurityStampAsync`); F5 makes that rotation
  **operative** by having a per-request gate honor it, instead of adding a new
  validator.
- **Per-request gate** in the **`PrivilegedStampMiddleware` / `BlockedAccountMiddleware`
  pattern** (§1c): on every request from an authenticated principal, re-read the
  user's current revocation state and, if a session-start marker predates it,
  `SignOutAsync` (clearing **both** the `kumunita.auth` **and**
  `.AspNetCore.Identity.Application` schemes — the `AccountController.cs:978-982`
  `Logout` shape) and redirect to login. This is the repo's *actual*
  invalidation idiom (AGENTS.md "fat authorization, per request"), and it is
  **already proven in this codebase** for privileged users.
- **Leg-2 gating (the F3 interplay).** Because a pending `TwoFactorUserIdScheme`
  cookie is a *separate* scheme the gate's app-scheme sign-out does not touch, the
  F3 leg-2 completion (`AccountController.cs:908-956`,
  `TwoFactorAuthenticatorSignInAsync` / `TwoFactorRecoveryCodeSignInAsync`) must
  reject a completion whose pending sign-in predates the revocation mark
  (§1d). Concretely: the leg-2 path checks the same revocation stamp before
  minting the app cookie, and on a hit, `SignOutAsync` (clearing the pending
  scheme) + re-render the challenge — **the resident cannot complete leg 2 after
  a kill-all**. This is the single thing that closes the half-signed-in window.

**What it costs (recorded for the owner, NOT assumed):**
- A **`User`-row revocation mark = a schema migration** (unlike F3, which had
  **no** migration). This touches a **frozen seam** (the `User` model / the
  `IdentityService` write path) — **F5.U2 must get owner approval before it
  lands code** (§8).
- A **per-request DB read for all authenticated users** (today
  `PrivilegedStampMiddleware` does this only for *privileged* users; Option B
  generalizes it to all). At this platform's scale (one neighborhood) that is a
  handful of extra reads — but the owner should accept it knowingly.
- A **leg-2 gate** on the F3 `LoginMfa` path — this touches a **F3-landed file**
  (`AccountController.cs`), so F5.U2/U3 must do it as a *narrow additive check*,
  not a re-shape of the F3 leg flow (§7 non-goal).

**Verdict: recommended.** It is consistent with the repo's existing
invalidation pattern (§1c), needs **no ADR 0006-B amendment** (the revocation mark
is a DB column, not a new claim type), and is the **only** option that cleanly
covers the F3 pending-cookie window with one consistent gate.

> **Recommendation to the owner:** **Option B.** Option A is recorded with its
> evidence for the owner's choice; if the owner prefers A, that is a new ADR
> (0006-B amendment) + a `ClaimShaping` change + the leg-2 gate, and F5.U2 must
> re-scope accordingly. **Either way, F5.U2 does not start until the owner picks.**

---

## 3. The `/settings/security/sessions` seam (reuses the F3 `SecuritySettingsController` shape)

- **Location:** a sibling action set on `SecuritySettingsController`
  (`/settings/security`), **not** a new controller — the same `[Authorize]`, the
  same `ResolveUserAsync()` (`SecuritySettingsController.cs:85-94`), the same
  anti-forgery + rate-limit discipline as the F3 MFA actions.
- **`GET /settings/security/sessions`** — list the signed-in user's sessions. The
  "current sessions" the list shows is **server-side state** (the revocation mark
  + any session registry F5.U2 pins), **not** a per-browser cookie read (a server
  cannot read another browser's cookies). Minimum viable: show "this session" +
  "other active sessions (revoked if a kill-all has run)."
- **`POST /settings/security/sessions/kill-all`** — `[ValidateAntiForgeryToken]` +
  `[EnableRateLimiting("write")]` (the repo's authenticated-write policy,
  `Program.cs:440-447`). Calls the **Core** revocation seam
  (`Kumunita.Core/Identity/IdentityService.cs` — the `UpdateSecurityStampAsync`
  precedent at `:1477` is the exact seam to extend/reuse, so **Core stays
  HTTP-free**), then `SignOutAsync()` the **local** browser (the
  `AccountController.cs:978-982` shape) for the immediate local effect. The
  "everywhere" effect is the server-side stamp + the per-request gate (§2 Option B);
  the local `SignOutAsync` is just the instant local half.
- **Route convention note (F3 precedent):** the F3.U3 handoff section records that
  a pin-table URL collision was resolved by keeping the primary URL and relocating
  the optional leg. F5 follows the same discipline: the primary kill-all keeps the
  pin's URL; any optional companion leg is relocated, and the plan-pinned URLs
  (`/settings/security/sessions`, `/settings/security/sessions/kill-all`) are
  unchanged.

---

## 4. Enforcement point (pinned)

- **The per-request gate** (§2 Option B) is the enforcement point — the
  `PrivilegedStampMiddleware` / `BlockedAccountMiddleware` idiom (§1c), registered
  in `Program.cs` between `UseAuthentication` and `UseAuthorization` (the
  `PrivilegedStampMiddleware` registration note, `PrivilegedStampMiddleware.cs`
  doc). It is what makes **both** pinned tests pass:
  - `PasswordChange_InvalidatesOtherSessions` — the password change
    (`IdentityService.cs:1477`) rotates the stamp; the **next request** from the
    user's other session hits the gate → sign-out.
  - `KillAllSessions_SignsOutAll` — the kill-all rotates the stamp; **all** of the
    user's sessions (and a pending leg-1 cookie, via the §2 leg-2 gate) are
    rejected.
- **Not the `SecurityStampValidator`.** §1a/§2 (Option A) is why the disabled
  validator is **not** the enforcement point in this codebase. The 14-day value
  at `Program.cs:478-481` is **kept** under the recommendation (Option B); it is
  **reverted** only if the owner picks Option A.

---

## 5. Pinned tests (the exit contract's names)

- **`PasswordChange_InvalidatesOtherSessions`** — a password change invalidates the
  user's other sessions (the second session is rejected on its next request).
- **`KillAllSessions_SignsOutAll`** — the kill-all invalidates all of the user's
  sessions **including** a pending F3 leg-1 `TwoFactorUserIdScheme` cookie that is
  rejected at leg 2 (the §1d interplay).
- **Grounding (F3.U2 build-verified surface, `hardening-handoff-notes.md` →
  `## F3.U2`):** the leg-2 completion members
  (`TwoFactorAuthenticatorSignInAsync(code, isPersistent, rememberClient)`,
  `TwoFactorRecoveryCodeSignInAsync(recoveryCode)` — both **no-user**, reading the
  pending `TwoFactorUserIdScheme` cookie via `GetTwoFactorAuthenticationUserAsync`),
  `SignOutAsync` (clears both schemes), `PasswordSignInAsync`,
  `Result.RequiresTwoFactor`, `ValidateTwoFactorSecurityStampAsync`. The tests
  drive the **public** `AccountController.Login` / `LoginMfa` + the
  `SecuritySettingsController` kill-all actions (the F3.U5 harness idiom,
  `tests/Kumunita.Web.Tests/F3U5_MfaLoginTests.cs`) — they do **not** make any
  private helper public.

---

## 6. Scope (what F5 owns)

- The **kill-all service** (F5.U2) + the **`/settings/security/sessions` surface**
  (F5.U3) + the **leg-2 gate** (F5.U2/U3, the §2 Option B interplay) + the two
  **pinned tests** (F5.U4).
- **Reuses** (does not re-invent): the F3 `SecuritySettingsController` shape
  (§1e/§3), the F3 `Login`/`LoginMfa` leg flow (§1d), the `UpdateSecurityStampAsync`
  Core seam (`IdentityService.cs:1477`), the `PrivilegedStampMiddleware` /
  `BlockedAccountMiddleware` per-request idiom (§1c), the F3.U5 test harness
  (§5).

---

## 7. Explicit non-goals / out of scope for F5

- **F5 is not the F20 rollback lane.** It **must not invent an app-side ops knob**
  to "make it code" (the F4.U2 discipline: "do not invent an app-side config knob
  to make it code" — `hardening-handoff-notes.md` → `## F4.U2`). F5's kill-all is a
  resident-facing security surface, not an ops/rollback primitive.
- **F5 does NOT change the F3 MFA enrollment surface** (F3.U3, landed) **or re-shape
  the F3 `Login`/`LoginMfa` leg flow** (F3.U4, landed). It only **adds** the §2
  Option B leg-2 gate (a narrow additive check on the F3 `LoginMfa` path) and
  reuses the leg flow's pending-cookie semantics. It does **not** touch the F3
  `SecuritySettingsController` MFA actions.
- **F5 does NOT open ADR 0006-B** under the recommendation (Option B). It opens a
  **new ADR** only if the owner picks Option A (the `SecurityStamp`-claim /
  name-type change) — recorded, not assumed.
- **F5 adds no new bounded context** (ADR 0006 preserved), **no new Marten doc
  that changes the C3 single-write-lane** (the kill-all write is in
  `Kumunita.Core/Identity/IdentityService.cs`, the existing single-write seam),
  and **no new `<PackageReference>`**.
- **F5 does NOT touch the F3-landed files or the F4 `OPS.md` blocks** (per the
  unit's rule). The only F3-landed file F5 may *add to* is `AccountController.cs`
  (the leg-2 gate), and only as a narrow additive check the owner approves (§8).

---

## 8. Exit contract for this pin (F5.U1)

- **This doc pins the 5 required items:** (1) the **mechanism decision — both
  options + a recommendation** (Option B recommended, Option A recorded as
  not-recommended-with-evidence, §2); (2) the **`/settings/security/sessions`
  kill-all seam** reusing the F3 `SecuritySettingsController` shape (§3); (3) the
  **F3-leg interplay** — a kill-all must reject a pending `TwoFactorUserIdScheme`
  cookie at leg 2 so no half-signed-in state survives (§2 leg-2 gate, §1d); (4) the
  **two pinned test names** — `PasswordChange_InvalidatesOtherSessions`,
  `KillAllSessions_SignsOutAll` (§5); (5) the **enforcement point** — the
  per-request gate in the `PrivilegedStampMiddleware` / `BlockedAccountMiddleware`
  idiom, **not** the disabled `SecurityStampValidator` (§4).
- **Drift note (recorded, NOT resolved inline — per the unit's rule):** the plan's
  literal premise — *"revert the 14-day `ValidationInterval` so
  `UpdateSecurityStampAsync` fires inside live sessions"* — is **not viable in this
  codebase as-is** (grounded: `Program.cs:458-481`, `ClaimShaping.cs:57-66`,
  `KumunitaClaimsPrincipalFactory.cs:62-68`, §1a/§2 Option A). This is the
  **design decision this pin resolves with both options + a recommendation** (per
  the unit's explicit instruction), **not a silent choice**. **F5.U2 must re-scope
  its step 1 ("lower `ValidationInterval` + kill-all service") to the Option B
  mechanism before it proceeds**, and the **owner must approve** the two
  frozen-seam touches Option B implies — (a) a `User`-row revocation mark (a
  **schema migration**, unlike F3), and (b) the per-request gate + the F3
  `AccountController.cs` leg-2 gate — **before F5.U2 lands code**.
- **No `src/` edit, no `tests/` edit, no `<PackageReference>`, no migration,
  no `.slnx` change, no build run** for this unit.
