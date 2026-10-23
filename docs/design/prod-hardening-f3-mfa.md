# F3 — AUTH-03: MFA for `GlobalAdmin` (Part-0 design pin)

**Fix:** F3 (report row **AUTH-03** — "MFA for `GlobalAdmin`"), severity bucket **data
exposure**. **Kind:** `code`.
**Source plan:** `docs/plans-milestones/in-progress/prod-hardening/plan-prod-hardening.md` § F3.
**Author unit:** **F3.U1** (design-only — this doc pins the design; **no code, no build**).
**Implementing units:** **F3.U2** (TOTP provider + token store), **F3.U3** (enroll/verify
surface), **F3.U4** (enforcement), **F3.U5** (tests). See §8 for the drift notes that
refine U4's seam.

This is **Tier 1** (a design pin a later ~32K clean-slate agent re-reads verbatim). It is
self-contained: F3.U2–U5 must not need to re-derive any of these decisions.

> **Repo doctrine applied (from `AGENTS.md`):** "Before writing … code, check the actual
> APIs in the current dependency versions in the `.csproj` files — do not copy a 'common'
> pattern from memory." That applies to **ASP.NET Identity's `UserManager` TOTP API** as
> much as to Marten/Wolverine. This pin therefore names the **stock method set** to use
> (the stable, well-known Identity surface) and instructs U2/U4 to **verify each signature
> against the pinned `Microsoft.AspNetCore.Identity 10.0.13`** before relying on it — the
> exact call shapes below are the *contract*, not a promise that every overload name is
> byte-for-byte correct in this major version.

---

## 1. The problem (grounded)

There is **no second factor** anywhere in the sign-in path. A compromised
`GlobalAdmin` password (or a stolen credential) yields full platform control.

- **The only MFA-shaped artifact in the schema** is the **`User.TwoFactorEnabled`**
  column (inherited from `IdentityUser`), present since the initial migration:
  `src/Kumunita.Core/Migrations/20260905190113_InitialIdentity.cs:50`
  (`TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false)`). It is **never
  set to `true`** anywhere (grep for `TwoFactorEnabled` in `src/**/*.cs` returns only the
  migration + model-snapshot lines). **No `AddTokenProvider`, no `GenerateTwoFactorToken`,
  no recovery-code code exists** (grep `TotpAuthenticator|AddTokenProvider|
  GenerateTwoFactorToken|GetTwoFactorRecoveryCodes` → zero hits in `src/**`).
- **The sign-in seam** is `AccountController.Login` POST —
  `src/Kumunita.Web/Controllers/AccountController.cs:~716`,
  `signInManager.PasswordSignInAsync(user, model.Password, isPersistent: true,
  lockoutOnFailure: true)`. **One step, persistent cookie, done.** There is no
  challenge leg. The controller already has **both** `SignInManager<User>` **and**
  `UserManager<User>` in its constructor (`AccountController.cs:31-35`) — so the MFA gate
  needs **no new DI**.
- **The closest existing "post-credential gate" precedent** — the **block
  enforcement at the login seam** — is the template for where the MFA gate goes. See
  `AccountController.cs:~726` (the comment block right after `PasswordSignInAsync`
  succeeds): *"Kept here (not in the factory) because the factory cannot fail sign-in;
  the controller is the one place that can inspect the account after credentials are
  verified."* The MFA gate slots in at **exactly this seam, for the same reason** (§5).
- **The `GlobalAdmin` role constant** is `Roles.GlobalAdmin == "GlobalAdmin"` —
  `src/Kumunita.Core/Identity/ThinPrincipal.cs:47` (in `public static class Roles`,
  `ThinPrincipal.cs:43`). Role assertions elsewhere use
  `roles.Contains(Roles.GlobalAdmin)` (e.g. `IdentityService.cs:902`, `:1387`) and
  `RequireGlobalAdminAsync` (`IdentityService.cs:1491`).
- **The recovery escape precedent** is the **last-GlobalAdmin guard** + **break-glass**:
  `IdentityService.cs:895-910` (the "This is the last GlobalAdmin on the instance" lockout
  + "recovery path is to promote a second GlobalAdmin first") and
  `ConsumeBreakGlassAsync` (`IdentityService.cs:~1319`, the operator break-glass consume).
  The MFA lockout escape (§6.4) reuses this precedent, not a new backdoor.

---

## 2. Storage — **reuse the existing `identity`-schema token table; NO new column, NO new doc, NO new migration** (the load-bearing choice)

**Pin: use ASP.NET Identity's stock token API against the `AspNetUserTokens` table that is
*already in the schema*, and flip the *already-present* `TwoFactorEnabled` column.**

Grounding:

- The token table **already exists.** `AppDbContext` derives from
  `IdentityDbContext<User, IdentityRole, string>` (`src/Kumunita.Core/Identity/AppDbContext.cs:29`),
  and the base entity set includes `IdentityUserToken<string>` — confirmed present in the
  initial migration's model (`20260905190113_InitialIdentity.cs` Designer, entity
  `Microsoft.AspNetCore.Identity.IdentityUserToken<string>` at lines 209/270; also in
  `AppDbContextModelSnapshot.cs:206/267`). **No `MfaSecret` column, no new EF migration.**
- `TwoFactorEnabled` **already exists** on `User : IdentityUser`
  (`src/Kumunita.Core/Identity/User.cs:15`) and on the table (`InitialIdentity.cs:50`).
  `UserManager.EnsureTwoFactorAsync(user)` flips it to `true`; `SetTwoFactorEnabledAsync(user, false)`
  clears it. **No schema change.**
- The `UserManager<User>` token API (the **stock** set — verify signatures vs `10.0.13`,
  §7 caveat) is the storage mechanism:

  | Purpose | Stock `UserManager<TUser>` method (namespace `Microsoft.AspNetCore.Identity`) |
  |---|---|
  | Read the TOTP secret (base32) | `GetAuthenticationTokenAsync(user, provider, "Token")` |
  | Store the TOTP secret (base32) | `SetAuthenticationTokenAsync(user, provider, "Token", secret)` |
  | Generate a TOTP code (30s/6) | `GenerateTwoFactorTokenAsync(user, provider, user.Email)` |
  | Validate a TOTP code | `ValidateTwoFactorTokenAsync(user, provider, code)` |
  | Read recovery codes | `GetTwoFactorRecoveryCodesAsync(user)` |
  | Replace recovery codes (rotate) | `ReplaceTwoFactorRecoveryCodesAsync(user, newCodes)` |
  | Consume one recovery code | `ReuseTwoFactorRecoveryCodeAsync(user, code)` |
  | Flip `TwoFactorEnabled` | `EnsureTwoFactorAsync(user)` / `SetTwoFactorEnabledAsync(user, bool)` |

  - **`provider`** = `TokenOptions.DefaultProvider` (the constant, value `"Default"`).
    `TotpAuthenticatorTokenProvider.Name` returns this same constant, so using
    `TokenOptions.DefaultProvider` is robust across versions (do not hardcode `"Kumunita"`
    unless U2's version check says the provider's `Name` differs — record it if it does).
  - **The TOTP secret is generated as base32** (`Base32Encoding.Encode` of 20 random
    bytes), then stored via `SetAuthenticationTokenAsync`. The **otpauth:// URI** (for the
    authenticator-app QR) is derived from the base32 + `user.Email` + the issuer.
  - **Recovery codes** (10, §6.1) are stored via `ReplaceTwoFactorRecoveryCodesAsync` —
    the stock API keeps them in the **same `AspNetUserTokens` table**, so no separate
    "recovery-code store" file is needed (the plan's U2 "recovery-code store" = this
    stock call, not a new artifact).

**Why the two *other* options are rejected (pin the rejection, not just the choice):**

1. **A new `MfaSecret` column** on `AspNetUsers` — *rejected.* The token table already
   exists and the stock API already reads/writes it; a bespoke column is a new migration
   for zero benefit and bypasses `EnsureTwoFactorAsync` / the recovery-code API.
2. **A new small `IdentityMfa` *Marten* doc** (in the `mt` schema) — *rejected on ADR 0004
   grounds.* ADR 0004 is explicit: "**Marten** owns the domain documents … **EF Core is used
   only for ASP.NET Identity tables**." A TOTP secret + recovery codes are **Identity
   credential material** — they belong with the Identity row in the `identity` schema, not
   as a domain doc in `mt`. This is the same ADR 0004 split that keeps the password hash in
   EF (see F6, `docs/design/prod-hardening-f6-pwhash.md`). **Pin: this is why the Marten-doc
   option in the plan is not chosen.**

**Consequence for U2 (scope note, §8.4):** U2's "recovery-code store" is **not a new
file** — it is the two stock calls above. U2's real work is the `.AddTokenProvider<...>()`
registration (§3) + confirming the token table is present on a live DB (§7).

---

## 3. TOTP registration — **one line on the existing `AddIdentity` chain**

**Pin: add `.AddTokenProvider<TotpAuthenticatorTokenProvider>()` to the `AddIdentity<User,
IdentityRole>(…)` chain in `Program.cs`, immediately after the existing
`.AddClaimsPrincipalFactory<KumunitaClaimsPrincipalFactory>()` (currently
`Program.cs:403`).**

- **Location:** `src/Kumunita.Web/Program.cs:396-403` (the chain is
  `AddIdentity<User, IdentityRole>(opts => { … }) → .AddEntityFrameworkStores<AppDbContext>()
  → .AddClaimsPrincipalFactory<KumunitaClaimsPrincipalFactory>()`). Insert the provider
  registration so the chain reads
  `… .AddEntityFrameworkStores<AppDbContext>().AddTokenProvider<TotpAuthenticatorTokenProvider>()
  .AddClaimsPrincipalFactory<KumunitaClaimsPrincipalFactory>()`.
- **Why it is load-bearing:** `GenerateTwoFactorTokenAsync` / `ValidateTwoFactorTokenAsync`
  resolve the token provider **by name** from DI. Without a registered provider, the first
  TOTP call throws ("no `ITokenProvider` registered"). **This registration does not exist
  today** (grep `AddTokenProvider` → zero hits). U2 **must** add it.
- **Period / digits — PIN the defaults, do NOT override:** **30-second period, 6 digits**
  (the `TotpAuthenticatorTokenProvider` defaults). Do **not** add a
  `Configure<IdentityOptions>(o => o.Tokens…) / SetTokenLifespan` override; the defaults are
  the standard and the pinned test expectations assume them.
- **No new package (grounded):** `TotpAuthenticatorTokenProvider` lives in
  `Microsoft.AspNetCore.Identity`. It is **already reachable** — `Program.cs` already
  imports that namespace (`SecurityStampValidatorOptions`, `AddIdentity`,
  `AccountController.cs` uses `SignInManager`/`UserManager`), and
  `Kumunita.Core.csproj` references `Microsoft.AspNetCore.Identity.EntityFrameworkCore
  10.0.13` (which depends on the Identity core package). **Contingency (record, not assumed):**
  if U2's build cannot resolve the type from the Web project, add an explicit
  `<PackageReference Include="Microsoft.AspNetCore.Identity" Version="10.0.13" />` to
  `Kumunita.Web.csproj`. Flag it in the handoff note if it was needed.

---

## 4. Routes — **a new `settings/security` controller (the plan's exact URLs)**

There is **no existing `/settings/security/*` controller** (grep `settings/security` →
zero hits). The settings-tab precedent is `MessagingSettingsController` at
`/settings/messaging` (`src/Kumunita.Web/Controllers/MessagingSettingsController.cs:43-47`,
a `[Authorize]` controller in `Kumunita.Web.Controllers`). **Pin the same shape for F3:**
a new `[Authorize]` controller (name it `SecuritySettingsController`, action namespace
`Kumunita.Web.Controllers`), `[Route("settings/security")]`, with these actions.

| URL | HTTP | Action | Purpose |
|---|---|---|---|
| `/settings/security/mfa` | GET | `Mfa()` | Enrollment state. **If not yet enabled:** show the one-time **setup key** (base32) + **otpauth:// URI** (QR) + the **10 recovery codes** (shown **once**). **If already enabled:** show status + **count only** of remaining recovery codes (never the values again). |
| `/settings/security/mfa` | POST | `MfaEnable(MfaEnableViewModel)` | **Verify** the first TOTP code the resident typed (proving the authenticator works), **then** store the secret (`SetAuthenticationTokenAsync`), issue the 10 recovery codes (`ReplaceTwoFactorRecoveryCodesAsync`), and flip `EnsureTwoFactorAsync`. Returns the recovery codes **once**. |
| `/settings/security/mfa/recovery` | GET | `MfaRecovery()` | Show **count only** of remaining recovery codes (never the values — they were shown once at enable/rotate). |
| `/settings/security/mfa/recovery` | POST | `MfaRotateRecovery()` | **Rotate**: regenerate 10 codes (`ReplaceTwoFactorRecoveryCodesAsync`), return them **once**. |
| `/settings/security/mfa` | POST | `MfaDisable(MfaDisableViewModel)` | (Optional, recommended) Re-verify a TOTP code, then `SetTwoFactorEnabledAsync(user,false)` + clear the secret. **For a `GlobalAdmin`, this is the one action that must carry the enforcement caution** (§8.6): a lone GlobalAdmin disabling MFA removes their own second factor — gate it behind a TOTP or recovery proof, and record an audit row. |
| `/account/login/mfa` | POST | (U4) `LoginMfa(MfaChallengeViewModel)` | The **challenge leg** of sign-in (§5): accept a TOTP **or** a recovery code for the pending (temporarily signed-in) user, then complete the **persistent** sign-in. |

> **URL-shape pin:** the plan names `/settings/security/mfa` (GET/POST) and
> `/settings/security/mfa/recovery`. This table adds two additive actions
> (`/settings/security/mfa` POST `MfaDisable`, `/account/login/mfa` POST `LoginMfa`) that
> the plan's three test names require (enforcement + the recovery-code path). They are
> **additive**, not a re-shape of the pinned URLs — U3/U4 own them. The **three** plan-pinned
> URLs are unchanged.

### View model shape (pin — `MfaSettingsViewModel`, `Kumunita.Web.Models`)

```csharp
public sealed class MfaSettingsViewModel
{
    public bool Enabled { get; init; }                 // user.TwoFactorEnabled
    // Shown ONCE, at enable/rotate — never re-served by a GET afterward.
    public string? SetupKey { get; init; }             // base32 TOTP secret (enable leg only)
    public string? OtpAuthUrl { get; init; }           // otpauth:// URI (enable leg only)
    public string[]? RecoveryCodes { get; init; }      // 10 codes (enable/rotate leg only)
    public int RemainingRecoveryCodes { get; init; }   // int — the only thing a steady GET shows
}
```
`MfaEnableViewModel` / `MfaDisableViewModel` / `MfaChallengeViewModel` each carry the
single `string Code` field (+ `ValidateAntiForgeryToken`). The **"shown once, then never
stored again"** rule = the values are returned **only** on the enabling/rotating response;
every GET reads `GetTwoFactorRecoveryCodesAsync` **solely to count** (`.Length`) and never
emits the values.

---

## 5. Enforcement point — **pin: the `AccountController.Login` POST seam (the block-gate precedent), NOT `PrivilegedStampMiddleware`**

**Pin: enforce MFA in the `AccountController.Login` POST (`src/Kumunita.Web/Controllers/
AccountController.cs`, the action at ~`:698-755`), immediately after
`PasswordSignInAsync` succeeds and the existing `profile.Blocked` gate — i.e. at the exact
seam the repo already uses for its one other post-credential gate.** A GlobalAdmin
(presented above as a `user.TwoFactorEnabled` + role check) cannot complete sign-in
without a valid TOTP or recovery code. **Other roles are unaffected** (their `TwoFactorEnabled`
is optional).

**The pinned flow (U4 implements):**

1. `var preResult = await signInManager.PasswordSignInAsync(user, model.Password,
   isPersistent: false, lockoutOnFailure: true);` — note the change to
   **`isPersistent: false`** (temporary) so a not-yet-MFA'd sign-in does not mint the
   long-lived cookie.
2. `if (preResult.Succeeded && user.TwoFactorEnabled)` → **the account is
   MFA-protected** (regardless of role):
   - keep the **temporary** sign-in (so `User` is populated for the challenge view);
   - return the **MFA challenge** (the `/account/login` view with the code field, or a
     redirect to `/account/login/mfa`); do **not** yet redirect to `ReturnUrl`.
   - on a correct code (`ValidateTwoFactorTokenAsync` **or** a consumed
     `ReuseTwoFactorRecoveryCodeAsync`), re-run `PasswordSignInAsync(user, …,
     isPersistent: true)` and redirect — **persistent** sign-in is now earned.
   - on a wrong code, fail (do not mint the persistent cookie); the temp sign-in is
     discarded by the next request / sign-out.
3. `else if (preResult.Succeeded && IsGlobalAdmin(user) && !user.TwoFactorEnabled)` →
   **the "Enforce for GlobalAdmin" requirement**: a GlobalAdmin who has **not** enrolled
   yet is **redirected to `/settings/security/mfa`** (enrollment) instead of proceeding —
   they cannot reach the admin surface with password-only. (This is the "enforce" half of
   AUTH-03; the *challenge* half is step 2.)
4. `else if (preResult.Succeeded)` → **the existing path is unchanged**: the
   `profile.Blocked` check (kept verbatim), then the persistent redirect. This is the
   **`NonAdmin_MfaOptional`** guarantee — a non-GlobalAdmin with `TwoFactorEnabled == false`
   signs in exactly as today.

**Why this seam (pin the rationale, it is the deciding argument):**
- The repo's own precedent for "a gate that must be able to *fail* sign-in" is the
  **block enforcement at the login seam** (`AccountController.cs:~726`): *"Kept here (not in
  the factory) because the factory cannot fail sign-in; the controller is the one place that
  can inspect the account after credentials are verified."* MFA is exactly such a gate — it
  must be able to **withhold** the persistent cookie — so it belongs here for the same
  reason.
- The controller **already injects** `SignInManager<User>` **and** `UserManager<User>`
  (`AccountController.cs:31-35`) — **zero new DI**, zero new service.
- It is **per-role-agnostic** to write (the check is `user.TwoFactorEnabled` +
  `IsGlobalAdmin(user)`) and trivially testable through the controller (the repo's
  documented Web-test convention is **direct controller construction** — see the F2/F1
  handoff notes' "direct-controller-construction note").

**Why the other two plan options are rejected (pin the rejection):**
- **`PrivilegedStampMiddleware` (the plan's named hook, `Program.cs:773`,
  `src/Kumunita.Web/Security/PrivilegedStampMiddleware.cs:43`)** — *wrong layer.* It is a
  **per-request role-revocation** middleware: it re-reads roles and signs out when the set
  changes. It **cannot present a challenge** (it has no view/model, only
  `SignOutAsync` + `Redirect`). Driving a sign-in second factor through a per-request
  middleware would be an anti-pattern and would add a DB hit to every privileged request.
  **Recorded as Drift note §8.1** — the plan named it; the grounded seam is the controller.
- **A `[MfaRequired]` `IAuthorizationHandler` attribute** — *rejected for the sign-in gate.*
  An authorization handler produces a 401/403 on a *protected endpoint*; it cannot
  "complete a two-leg sign-in." It is fine as an optional **defense-in-depth** on the
  `/admin/*` endpoints, but it is the **wrong primary** for "a GlobalAdmin must pass a
  second factor to *sign in*" (the pinned test `GlobalAdminLogin_RequiresMfaChallenge` is
  about the **login**, not an endpoint). **Optional hardening (open item, §9):** *additionally*
  gate the `/admin/*` controllers with a `[MfaRequired]` handler so a stale MFA-less cookie
  can't reach admin pages — but the controller login gate is the contract.

**`IsGlobalAdmin(user)`** = `(await userManager.GetRolesAsync(user)).Contains(Roles.GlobalAdmin)`
(`Roles.GlobalAdmin`, `ThinPrincipal.cs:47`). Same assertion shape as `IdentityService.cs:902`.

---

## 6. Recovery path (pin the full shape)

### 6.1 Generation
- **Count: 10** — the ASP.NET Identity `GetTwoFactorRecoveryCodesAsync` default. Pin 10;
  do not invent a different number. (If U2's `10.0.13` check shows a different count, record
  it in the handoff note and keep the test counting dynamically, not hardcoding 10.)
- **Issued at:** (a) the **enable** POST (§4 `MfaEnable`) — first 10 codes, returned once;
  (b) the **rotate** POST (§4 `MfaRotateRecovery`) — replaces the current set, returned once.

### 6.2 One-time use
- A recovery code presented at the challenge leg is consumed via
  `ReuseTwoFactorRecoveryCodeAsync(user, code)` — the stock API **deletes the used code**
  from the stored set, so each code works **exactly once**. A second presentation of the
  same code is rejected. This is the `GlobalAdminRecoveryCode_WorksOnce` guarantee.

### 6.3 Rotate
- `ReplaceTwoFactorRecoveryCodesAsync(user, newCodes)` **atomically swaps** the whole set.
  The new 10 are shown **once**; the old 10 are dead. The steady-state GET (§4
  `MfaRecovery()`) shows only the **count**.

### 6.4 The "all recovery codes used" edge case (pin the escape — no backdoor)
- If **all 10 recovery codes are consumed** *and* the TOTP authenticator is lost, the
  `GlobalAdmin` **cannot sign in** (both second factors are gone). **Do NOT add a
  backdoor** (no "skip MFA" flag, no admin override that bypasses the second factor).
- **The escape is the existing operator lane** (reuse, don't reinvent):
  - **Break-glass:** the operator invokes the existing **break-glass** lane
    (`IdentityService.ConsumeBreakGlassAsync`, `IdentityService.cs:~1319`) to re-enable
    access, then a GlobalAdmin peer (or the operator) resets MFA via
    `SetTwoFactorEnabledAsync(user, false)` + clearing the secret (the §4 `MfaDisable`
    action), and re-enrolls.
  - **Second-GlobalAdmin handover:** the same "promote a second GlobalAdmin first"
    precedent as the **last-GlobalAdmin guard** (`IdentityService.cs:895-910`) — a peer
    with MFA resets the stuck account's MFA.
- **Pin:** the recovery path is the **stock one-time codes + the existing break-glass /
  second-GlobalAdmin escape**. Record the "all codes used → break-glass" behavior as an
  **owner-attested procedure** (it touches operator runbooks, not this code unit).

---

## 7. Pinned test names (**F3.U5 implements**)

These three are the contract (verbatim from the plan §F3 U5 and the task). Each asserts
against a real `UserManager`/`Identity` state (the repo's Web-test convention is
**direct-controller construction** — construct `AccountController` with a real
`SignInManager`/`UserManager` over an in-memory/EF `AppDbContext` and a fake
`IIdentityService`/`IUserInfoService`, per the F1/F2 handoff notes).

1. **`GlobalAdminLogin_RequiresMfaChallenge`** — a `GlobalAdmin` with
   `TwoFactorEnabled == true` posts a correct password to `Login`. Assert the response is the
   **challenge** (not a redirect to `ReturnUrl`), i.e. sign-in is **not** complete, and a
   subsequent correct TOTP code completes the **persistent** sign-in. Proves §5 step 2.
2. **`GlobalAdminRecoveryCode_WorksOnce`** — a `GlobalAdmin` at the challenge leg presents
   a **recovery code**. Assert (a) the first presentation **succeeds** and completes sign-in;
   (b) the **same** code presented again is **rejected** (consumed, §6.2). Proves §6.
3. **`NonAdmin_MfaOptional`** — a non-GlobalAdmin (e.g. `Member`) with
   `TwoFactorEnabled == false` posts a correct password. Assert sign-in completes
   **directly** (persistent redirect, no challenge) — MFA is **optional** for them. Proves
   §5 step 4.

**Driving note for U5:** the enforcement logic lives in `AccountController` (Web) and the
token API is on `UserManager` (Identity/EF). The tests drive the **public controller
actions** (`Login`, `LoginMfa`) — do **not** make the §5 private helpers public to test
them. A `GlobalAdmin` role is seeded via `RoleManager`/`userManager.AddToRoleAsync` (the
same setup the F1 `IdentityServiceDeleteLaneGdprTests` harness uses for a GlobalAdmin actor).

---

## 8. Drift / scope notes (record for the owner + U2–U5 — do not silently resolve)

1. **The plan's named hook, `PrivilegedStampMiddleware`, is the wrong layer for MFA.** It is
   a per-request **role-revocation** middleware (`Program.cs:773`,
   `Security/PrivilegedStampMiddleware.cs:43`) that only knows how to `SignOutAsync` +
   `Redirect`; it cannot present a challenge or complete a two-leg sign-in. The grounded
   enforcement seam is **`AccountController.Login` POST** (the block-gate precedent, §5).
   **Recorded, not resolved against the plan** — the plan's F3 U4 said "a middleware after
   `PrivilegedStampMiddleware`"; the pin supersedes that with the controller seam.
2. **`AddTokenProvider` is not registered today.** Grep `AddTokenProvider` → zero hits in
   `src/**`. The TOTP API will throw on first use until U2 adds
   `.AddTokenProvider<TotpAuthenticatorTokenProvider>()` (§3). This is the single most
   likely "it compiles but throws at runtime" trap — U2 must add it.
3. **`TwoFactorEnabled` exists but is never set; the `AspNetUserTokens` table already
   exists.** So "reuse `IdentityUserToken`" (§2) is not just convenient — it is
   **migration-free**. If U2 nonetheless finds the token table absent on a live DB (it should
   be, per the initial migration model), the fallback is a new EF migration adding
   `AspNetUserTokens` — **record it**, do not silently add a Marten doc (ADR 0004, §2).
4. **U2's "recovery-code store" is not a new file.** It is the stock
   `GetTwoFactorRecoveryCodesAsync` / `ReplaceTwoFactorRecoveryCodesAsync` /
   `ReuseTwoFactorRecoveryCodeAsync` calls (§2, §6) against the existing table. U2's real
   deliverables are: the `.AddTokenProvider` registration (§3) + confirming the table is
   present (§8.3) + the TOTP-secret read/write helpers (§2 table). **This refines the plan's
   F3 U2** ("TOTP provider + token store, ≤2 files") — the "token store" is API calls, not a
   new artifact.
5. **`Login` POST currently does a single persistent `PasswordSignInAsync`**
   (`AccountController.cs:~716`, `isPersistent: true`). The MFA gate requires splitting that
   into a **two-leg** flow (§5: temporary → challenge → persistent). U4 must change that one
   line from `isPersistent: true` to `isPersistent: false` (then re-sign-in persistently on a
   good code). **This is the one behavior change to the existing login path** — it is
   intentional (a not-yet-verified MFA sign-in must not mint the long-lived cookie) and is
   why the `NonAdmin_MfaOptional` test must still pass (the else branch keeps the persistent
   sign-in for non-MFA users).
6. **A lone `GlobalAdmin` disabling their own MFA is a self-lockout-of-security** (§4
   `MfaDisable`). Pin: that action must require a TOTP/recovery proof and write an audit row
   (`Authorization.AccessAudit`, the `seed-admin.setup` shape, `IdentityService.cs:~1275`).
   Record it; do not let a password-only POST remove the second factor.
7. **Identity-API version caution (repo doctrine).** The §2 method names are the **stock
   `Microsoft.AspNetCore.Identity` surface**; per the repo's "don't copy from memory" rule
   (and the Marten note in the plan), U2/U4 **must verify each signature against
   `Microsoft.AspNetCore.Identity 10.0.13`** (via the compiler) before relying on it —
   especially `GenerateTwoFactorTokenAsync` / `ValidateTwoFactorTokenAsync` /
   `ReuseTwoFactorRecoveryCodeAsync` overload shapes, which are the ones most likely to
   differ by version.

---

## 9. Exit (what must hold before U2 is considered ready)

- This file exists and pins: the **storage choice + the rejection of the Marten-doc option**
  (§2), the **TOTP registration** line + the no-new-package grounding (§3), the **routes +
  view-model shape** (§4), the **enforcement seam** (the `AccountController.Login` POST, with
  the `PrivilegedStampMiddleware` rejection grounded) (§5), the **recovery path** incl. the
  "all codes used" escape (§6), and the **three test names** (§7).
- **No `.cs` file is modified by this unit** (design-only). **No build is run.**
- The `## F3.U1 — Part-0 design pin` section is **appended** to
  `hardening-handoff-notes.md` (storage choice, enforcement choice, the three test names, the
  §8 drift notes, and "Next unit = **F3.U2**") — **written before stopping.**
- **F3.U2 is NOT started** — it is the next clean-slate agent.

**Open items (owner, NOT assumed done):** (a) the §6.4 "all codes used → break-glass /
second-GlobalAdmin" runbook is an **owner-attested** procedure (operator lane, not this unit);
(b) the §8.2/§8.3 table-presence + §8.7 API-signature verifications are **U2's** to confirm
against the live `10.0.13`, recorded here as the risk surface, not assumed.
