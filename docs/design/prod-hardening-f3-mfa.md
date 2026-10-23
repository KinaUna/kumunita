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
> pattern from memory." That applies to **ASP.NET Identity's two-factor API** as much as to
> Marten/Wolverine. The .NET ≤8 "stock `UserManager` TOTP API" that the original pin assumed
> is **largely absent** in `Microsoft.AspNetCore.Identity 10.0.13` — the .NET 10 surface is a
> **provider-based two-factor API on `SignInManager`** plus **`IdentityUser` properties**
> (`IsTwoFactorEnabled` / `RecoveryCodesLeft` / `ResetRecoveryCodes`). This **re-pinned**
> version therefore names the **actual 10.0.13 surface, annotated by verification tier**
> (① compiler-verified / ② F3.U2-verified / ③ U2-build-verifies — see §2.1), so U2/U4 know
> exactly which names are guaranteed and which must be confirmed at build.

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

## 2. Storage — **the EF-owned Identity row + a registered provider's token store; ADR 0004 preserved; NO new Marten doc**

> **RE-PINNED against the actual `Microsoft.AspNetCore.Identity 10.0.13`** (the exact
> ref assembly the repo's compiler resolves: `…/packs/Microsoft.AspNetCore.App.Ref/10.0.13/
> ref/net10.0/Microsoft.AspNetCore.Identity.dll`). The original §2 assumed the **.NET ≤8
> `UserManager` TOTP API**; **per the F3.U2 drift-pause note that set does not match the
> 10.0.13 surface** (the F3.U2 note states: *"those names are the .NET ≤8 shape, not
> 10.0.13"*), and this unit's `strings` sweep of the 10.0.13 ref dll is **consistent** (zero
> hits for `GenerateTwoFactorTokenAsync`, `ValidateTwoFactorTokenAsync`,
> `Get/Replace/ReuseTwoFactorRecoveryCodesAsync`, `EnsureTwoFactorAsync`,
> `SetTwoFactorEnabledAsync`, `Get/SetAuthenticatorKeyAsync`, `Get/SetAuthenticationTokenAsync`).
> **Caution:** `strings` is an unreliable *negative* test for methods (it also missed the
> present `PasswordSignInAsync`), so the F3.U2 note is the **authoritative** absence basis,
> with the strings sweep as corroboration. **U2 confirms at build** that each is truly absent
> before removing any code that depends on it. The .NET 10 surface is a **provider-based
> two-factor API on `SignInManager`** plus **`IdentityUser` properties** for the enable flag
> and recovery codes. This section names members **with a verification tier** (①/②/③, §2.1) so
> U2/U4 know which are guaranteed and which must be confirmed at build.

### 2.1 What is present in 10.0.13 (verified, load-bearing)

> **Verification tiers (honest about what I actually verified against the live 10.0.13 assembly):**
> - **① compiler-verified** = the **current repo code compiles** against 10.0.13 using it (ground truth — cannot be absent).
> - **② F3.U2-verified** = named in the **F3.U2 drift-pause note** (the F3.U2 agent ran `strings` on the same 10.0.13 dll and listed it as present).
> - **③ U2-build-verifies** = I found it via `strings` on the 10.0.13 ref dll (NOT in the F3.U2 note). `strings` is a **reliable positive** test for **property accessors** (`get_`/`set_`) + **type names** (if the token is there, the name is there), but an **unreliable negative** test for **methods** (method names live in iterator state-machine strings and may not appear as bare tokens — e.g. `PasswordSignInAsync` does not appear as a bare token yet exists). So: ③ **property accessors / type names** are reliably present (U2 confirms the **EF-mapping / shape** at build); ③ **methods** are *likely* present but **U2 must confirm the exact signature at build** before relying on them. **Do NOT treat a ③ *method* as a guarantee.**

| Concern | **10.0.13 member** | Verification tier |
|---|---|---|
| Enable flag (replaces the .NET ≤8 `TwoFactorEnabled` property) | `IdentityUser.IsTwoFactorEnabled` (`bool`, get/set) + `SignInManager.IsTwoFactorEnabledAsync(user)` | ③ this-unit `strings` sweep (property accessors — reliable present; U2 confirms the EF-mapping at build) |
| Recovery codes — count | `IdentityUser.RecoveryCodesLeft` (`int`, get/set) | ③ this-unit `strings` sweep (property accessors — reliable present; U2 confirms the EF-mapping at build) |
| Recovery codes — (re)issue set | `IdentityUser.ResetRecoveryCodes` (get/set) | ③ this-unit `strings` sweep (property accessors — reliable present; U2 confirms the EF-mapping at build) |
| Challenge leg (TOTP) — **generate** | `SignInManager.CanGenerateTwoFactorTokenAsync(user)` | ② (in the F3.U2 note) |
| Challenge leg (TOTP) — **sign in** | `SignInManager.TwoFactorAuthenticatorSignInAsync(user, code)` | ② (in the F3.U2 note) |
| Challenge leg (recovery) — **sign in** | `SignInManager.TwoFactorRecoveryCodeSignInAsync(user, code)` | ② (in the F3.U2 note) |
| Security-stamp re-check at challenge | `SignInManager.ValidateTwoFactorSecurityStampAsync(user)` | ② (in the F3.U2 note) |
| TOTP token provider (interface) | `IUserTwoFactorTokenProvider<TUser>` | ② (in the F3.U2 note) |
| Default provider registration | `IdentityBuilder.AddDefaultTokenProviders()` | ② (in the F3.U2 note) |
| Default token provider impl | `DataProtectorTokenProvider<TUser>` (data-protection based) | ② (in the F3.U2 note) |
| **Sign-in leg 1** (password check, `isPersistent:false` to withhold cookie) | `SignInManager.PasswordSignInAsync(user, password, isPersistent, lockoutOnFailure)` | **① compiler-verified** — the current `AccountController.cs:716` calls it and the repo builds green |
| Sign-in-result two-factor gate | `SignInResult.RequiresTwoFactor` (bool) | ③ U2-build-verifies (the ① `PasswordSignInAsync` returns a `SignInResult`; this gate is the .NET 10 idiomatic two-factor check) |
| Two-factor user cookie (pending sign-in) | `SignInManager.GetTwoFactorAuthenticationUserAsync()` | ③ U2-build-verifies |
| Pending user + metadata | `TwoFactorAuthenticationInfo` (has `User`, `LoginProvider`, `RememberMe`) | ③ U2-build-verifies |
| Combined challenge entry (optional) | `SignInManager.SignInOrTwoFactorAsync(user, code)` | ③ U2-build-verifies (the ② sign-in methods are sufficient; this is a convenience dispatch) |
| Two-factor cookie-mint (if the ② sign-in methods do NOT auto-mint) | `SignInManager.HandleSignInAsync(user, rememberMe)` | ③ U2-build-verifies (U4 confirms at build whether the ② two-factor sign-in methods mint the cookie themselves) |

> **What is ABSENT in 10.0.13 (verified — both by the F3.U2 note's `strings` sweep and by this unit's `strings` sweep of the 10.0.13 ref dll):**
> - `TotpAuthenticatorTokenProvider` — **absent** (F3.U2: zero `totp` strings; this unit: zero `totp` strings). The .NET ≤8 authenticator-app provider is gone from the public API surface.
> - The parameterless `AddTokenProvider<TProvider>()` overload — **absent** (F3.U2's CS7036 error names the real one as `AddTokenProvider<TProvider>(string providerName)`; the named overload exists, the parameterless one does not).
> - The .NET ≤8 **`UserManager`** two-factor method set — **absent** (**authoritative basis: the F3.U2 note** states *"those names are the .NET ≤8 shape, not 10.0.13"*). This unit's `strings` sweep is **consistent** (zero hits in the 10.0.13 ref dll for `EnsureTwoFactorAsync`, `SetTwoFactorEnabledAsync`, `GenerateTwoFactorTokenAsync`, `ValidateTwoFactorTokenAsync`, `GetTwoFactorRecoveryCodesAsync`, `ReplaceTwoFactorRecoveryCodesAsync`, `ReuseTwoFactorRecoveryCodeAsync`, `GetAuthenticationTokenAsync`, `SetAuthenticationTokenAsync`, `GetAuthenticatorKeyAsync`, `SetAuthenticatorKeyAsync`) but `strings` is an unreliable *negative* test for methods, so **U2 confirms at build** before removing any code that depends on one. The .NET 10 surface is provider-based + `IdentityUser` properties.
> - **What is NOT absent (correcting the F3.U2 note's implication):** `PasswordSignInAsync` **is present** — the current `AccountController.cs:716` calls it and the repo builds green (① compiler-verified). The F3.U2 drift pause did not test `PasswordSignInAsync`; only `AddTokenProvider` and `TotpAuthenticatorTokenProvider`. **Do not remove `PasswordSignInAsync` from the sign-in flow** — it is the leg-1 method that already works.
>
> **`user.TwoFactorEnabled` → `user.IsTwoFactorEnabled` (③).** The bare `TwoFactorEnabled` **property** is **absent** in 10.0.13 (this unit's `strings` sweep: zero `get_TwoFactorEnabled` / `set_TwoFactorEnabled` tokens; only `get_/set_IsTwoFactorEnabled` + `IsTwoFactorEnabledAsync` are present). Every §5/§7 reference that read `user.TwoFactorEnabled` must read `user.IsTwoFactorEnabled` (or `signInManager.IsTwoFactorEnabledAsync(user)`). **The DB *column* on `AspNetUsers` is still spelled `TwoFactorEnabled`** in the repo's initial migration (`InitialIdentity.cs:50`) — EF maps the 10.0.13 `IsTwoFactorEnabled` property onto that existing column, so **no new column and no new migration are required for the flag**.

### 2.2 Storage mechanism under the corrected API (grounded)

- **The two-factor *state* lives on the EF-owned `User` row** (`identity` schema, ADR 0004):
  `IsTwoFactorEnabled` (the `AspNetUsers.TwoFactorEnabled` column, already present —
  `InitialIdentity.cs:50`) + `RecoveryCodesLeft` + `ResetRecoveryCodes`. These are
  **`IdentityUser` properties in 10.0.13**, persisted through `UserManager`/EF exactly as the
  other `IdentityUser` columns already are. **No new bounded context, no new doc.**
- **The TOTP *secret* is provider-owned token material.** In .NET 10 the secret is read/written
  **through the registered `IUserTwoFactorTokenProvider<TUser>`** (its token storage — for the
  default provider this is the data-protection-backed token store, **not** a bespoke base32
  column the app hand-mints). **U2 must verify at build time** whether the default
  `AddDefaultTokenProviders` provider persists the secret in the `AspNetUserTokens` table
  (`IdentityUserToken<string>`, already present in the schema — `InitialIdentity.cs:156`,
  `AppDbContextModelSnapshot:206/267`) or in a column-backed store, and pin whichever it is.
  **Either way it is EF/Identity-owned** — ADR 0004 holds and **no Marten doc is added**.
- **Recovery codes are `IdentityUser` properties** in 10.0.13 (`RecoveryCodesLeft` count +
  `ResetRecoveryCodes` set), **not** the old `UserManager` recovery-code *methods*. U2 must
  verify the EF mapping of these two properties (which column / table backs them in 10.0.13's
  `IdentityDbContext`) before pinning the exact persistence; the **count** is what a steady
  GET shows (`RecoveryCodesLeft`), and **(re)issuance** goes through `ResetRecoveryCodes`
  (§6). The "one-time use, then decrement" behavior is enforced by the sign-in leg
  (`TwoFactorRecoveryCodeSignInAsync`), not by an app-side consume call.

### 2.3 Rejected options (the rejection is pinned, not just the choice)

1. **A new `MfaSecret` column on `AspNetUsers`** — *rejected.* The provider owns token
   material in .NET 10; a bespoke column would bypass `IUserTwoFactorTokenProvider` and the
   `IsTwoFactorEnabled` flag, and is a new migration for zero benefit.
2. **A new small `IdentityMfa` *Marten* doc** (in the `mt` schema) — *rejected on ADR 0004
   grounds.* ADR 0004 is explicit: "**Marten** owns the domain documents … **EF Core is used
   only for ASP.NET Identity tables**." A TOTP secret + recovery codes + the enable flag are
   **Identity credential material** — they belong with the Identity row in the `identity`
   schema, not as a domain doc in `mt`. Same ADR 0004 split that keeps the password hash in
   EF (see F6, `docs/design/prod-hardening-f6-pwhash.md`). **Pin: this is why the Marten-doc
   option in the plan is not chosen — unchanged by the re-pin.**

### 2.4 Consequence for U2 (scope note, §8.4)

U2's "token store" is **not a new file** — it is (a) registering the default token provider
(§3) and (b) the `IsTwoFactorEnabled` / `RecoveryCodesLeft` / `ResetRecoveryCodes` property
access on the `User` row via `UserManager`/EF. **U2's real deliverables are:** the
`AddDefaultTokenProviders()` registration (§3) + confirming the two-factor state's EF
persistence resolves at build (§2.2/§8.3) + the provider's token round-trip. The .NET ≤8
"stock API calls" framing from the original §2 **does not apply** and is removed.

---

## 3. Registration — **`AddDefaultTokenProviders()` on the existing `AddIdentity` chain (10.0.13)**

> **RE-PINNED against the actual `Microsoft.AspNetCore.Identity 10.0.13`.** The original §3
> pinned `.AddTokenProvider<TotpAuthenticatorTokenProvider>()` — **both parts are ABSENT in
> 10.0.13**: `TotpAuthenticatorTokenProvider` (zero `totp` strings in the assembly) and the
> **parameterless** `AddTokenProvider<T>()` overload (the real one is
> `AddTokenProvider<TProvider>(string providerName)` — but it takes a name, and the type
> still doesn't exist). The F3.U2 drift pause (handoff note) caught exactly this. The
> **correct** registration for the 10.0.13 surface is below.

**Pin: add `.AddDefaultTokenProviders()` to the `AddIdentity<User, IdentityRole>(…)` chain in
`Program.cs`.**

- **Location:** `src/Kumunita.Web/Program.cs:396-403` (the chain is
  `AddIdentity<User, IdentityRole>(opts => { … }) → .AddEntityFrameworkStores<AppDbContext>()
  → .AddClaimsPrincipalFactory<KumunitaClaimsPrincipalFactory>()`). Insert the provider
  registration so the chain reads
  `… .AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders()
  .AddClaimsPrincipalFactory<KumunitaClaimsPrincipalFactory>()`. (`AddDefaultTokenProviders`
  is an `IdentityBuilder` method — **verified present** in the 10.0.13 assembly; it registers
  the default token providers, including `DataProtectorTokenProvider<T>`, which is the
  provider `SignInManager.CanGenerateTwoFactorTokenAsync` / `TwoFactorAuthenticatorSignInAsync`
  resolve when generating/validating a two-factor token.)
- **Why it is load-bearing (corrected):** the two-factor *sign-in* leg
  (`SignInManager.CanGenerateTwoFactorTokenAsync` / `TwoFactorAuthenticatorSignInAsync` /
  `TwoFactorRecoveryCodeSignInAsync`) and the `IUserTwoFactorTokenProvider<T>` token
  generate/validate all resolve a registered provider from DI. Without a provider
  registration, the first two-factor call throws ("no token provider registered").
  **This registration does not exist today** (grep `AddTokenProvider|AddDefaultTokenProviders`
  → zero hits in `src/**`). U2 **must** add it.
- **TOTP-specific provider — pin the decision, not a guess:** .NET 10's
  `AddDefaultTokenProviders()` registers a **data-protection-backed** provider
  (`DataProtectorTokenProvider<T>`, verified present), **not** a base32-TOTP
  authenticator-app provider (the .NET ≤8 `TotpAuthenticatorTokenProvider` is gone). For a
  **GlobalAdmin MFA** gate the data-protection token round-trip is sufficient for the
  sign-in challenge (`CanGenerateTwoFactorTokenAsync` → `TwoFactorAuthenticatorSignInAsync`).
  **Pin: U2 uses `AddDefaultTokenProviders()` and verifies at build time that
  `CanGenerateTwoFactorTokenAsync` returns `true` for a seeded `User` and that
  `TwoFactorAuthenticatorSignInAsync` accepts the generated token** — that is the contract.
  **If the owner wants a genuine authenticator-app (otpauth:// QR) TOTP experience instead,**
  U2 implements a custom `IUserTwoFactorTokenProvider<User>` and registers it with the
  **named** overload `AddTokenProvider<TProvider>("Default")` (the 10.0.13 signature) — this
  is an **open decision for the owner** (see §8.8), not a default. The default path is the
  minimal, build-verifiable one and is what the three pinned tests can assert against.
- **Period / digits — DO NOT pin defaults in code:** the .NET ≤8
  "30-second / 6-digit `TotpAuthenticatorTokenProvider` defaults" grounding is **removed** —
  that type does not exist in 10.0.13. Token parameters, if any, come from the registered
  provider's own configuration (or a custom `IUserTwoFactorTokenProvider` implementation).
  **Do not** add a `Configure<IdentityOptions>(o => o.Tokens…)` override speculating at
  10.0.13 behavior; pin only what U2's build + the two sign-in-leg calls confirm.
- **No new package (grounded, corrected):** `AddDefaultTokenProviders`,
  `IUserTwoFactorTokenProvider<T>`, `DataProtectorTokenProvider<T>`, and the
  `SignInManager` two-factor methods all live in `Microsoft.AspNetCore.Identity` **and are
  already reachable** — `Kumunita.Core.csproj` references
  `Microsoft.AspNetCore.Identity.EntityFrameworkCore 10.0.13` (which depends on the Identity
  core package), and `Program.cs` already imports the namespace (`AddIdentity`,
  `SecurityStampValidatorOptions`). **The original "add
  `<PackageReference Microsoft.AspNetCore.Identity 10.0.13>`" contingency is MOOT** — the
  F3.U2 drift pause already showed the missing type is absent *in* `10.0.13` itself, so a
  package reference cannot supply it; the fix is to use the **present** 10.0.13 surface, not
  to import the version that lacks the type. **Do not add the package.**

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
| `/settings/security/mfa` | GET | `Mfa()` | Enrollment state. **If not yet enabled:** show the one-time **setup key** (the provider-generated TOTP secret) + **otpauth:// URI** (QR, only if the TOTP-specific provider in §3 is chosen) + the **10 recovery codes** (shown **once**). **If already enabled:** show status + **count only** of remaining recovery codes (never the values again). |
| `/settings/security/mfa` | POST | `MfaEnable(MfaEnableViewModel)` | **Verify** the first TOTP code the resident typed (proving the authenticator works — `signInManager.CanGenerateTwoFactorTokenAsync` + the provider's token validate, §2.2), **then** store the TOTP secret through the registered `IUserTwoFactorTokenProvider` (§2.2), issue the 10 recovery codes (`user.ResetRecoveryCodes = new set` + `SaveChangesAsync`, §6), and flip `user.IsTwoFactorEnabled = true` + `SaveChangesAsync` (the 10.0.13 property — the .NET ≤8 `EnsureTwoFactorAsync` is **absent**). Returns the recovery codes **once**. |
| `/settings/security/mfa/recovery` | GET | `MfaRecovery()` | Show **count only** of remaining recovery codes (`user.RecoveryCodesLeft`, never the values — they were shown once at enable/rotate). |
| `/settings/security/mfa/recovery` | POST | `MfaRotateRecovery()` | **Rotate**: regenerate 10 codes (`user.ResetRecoveryCodes = new set` + `SaveChangesAsync`, §6.3), return them **once**. |
| `/settings/security/mfa` | POST | `MfaDisable(MfaDisableViewModel)` | (Optional, recommended) Re-verify a TOTP code, then `user.IsTwoFactorEnabled = false` + clear the secret (the 10.0.13 property — the .NET ≤8 `SetTwoFactorEnabledAsync(user,false)` is **absent**). **For a `GlobalAdmin`, this is the one action that must carry the enforcement caution** (§8.6): a lone GlobalAdmin disabling MFA removes their own second factor — gate it behind a TOTP or recovery proof, and record an audit row. |
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
    public bool Enabled { get; init; }                 // user.IsTwoFactorEnabled (10.0.13 property)
    // Shown ONCE, at enable/rotate — never re-served by a GET afterward.
    public string? SetupKey { get; init; }             // provider-generated TOTP secret (enable leg only)
    public string? OtpAuthUrl { get; init; }           // otpauth:// URI (enable leg only, TOTP-provider path)
    public string[]? RecoveryCodes { get; init; }      // 10 codes (enable/rotate leg only)
    public int RemainingRecoveryCodes { get; init; }   // user.RecoveryCodesLeft — the only thing a steady GET shows
}
```
`MfaEnableViewModel` / `MfaDisableViewModel` / `MfaChallengeViewModel` each carry the
single `string Code` field (+ `ValidateAntiForgeryToken`). The **"shown once, then never
stored again"** rule = the values are returned **only** on the enabling/rotating response;
every GET reads `user.RecoveryCodesLeft` **solely as the count** (the 10.0.13 property —
the .NET ≤8 `GetTwoFactorRecoveryCodesAsync` is **absent**) and never emits the values.

---

## 5. Enforcement point — **pin: the `AccountController.Login` POST seam (the block-gate precedent), NOT `PrivilegedStampMiddleware`**

> **RE-PINNED against the actual `Microsoft.AspNetCore.Identity 10.0.13` sign-in surface.**
> The original §5 relied on `ValidateTwoFactorTokenAsync` + `ReuseTwoFactorRecoveryCodeAsync` +
> `user.TwoFactorEnabled` — **all three are ABSENT in 10.0.13** (the .NET ≤8 `UserManager` TOTP
> API + the `TwoFactorEnabled` property; see §2.1). The .NET 10 two-leg sign-in flow is instead:
> **leg 1** `PasswordSignInAsync(user, password, isPersistent:false, lockoutOnFailure:true)`
> (**① compiler-verified** — the current `AccountController.cs:716` calls it and the repo builds
> green) → **gate** on `result.RequiresTwoFactor` (**③ U4-build-verifies** the property on
> `SignInResult`) → **leg 2** `TwoFactorAuthenticatorSignInAsync(user, code)` /
> `TwoFactorRecoveryCodeSignInAsync(user, code)` (**② F3.U2-verified** — both named in the F3.U2
> drift-pause note). **Correction to the F3.U2 note's implication:** `PasswordSignInAsync` is
> **NOT absent** in 10.0.13 (the F3.U2 agent only tested `AddTokenProvider` +
> `TotpAuthenticatorTokenProvider`, both of which ARE absent; it did not test the sign-in leg).
> **Do NOT replace leg-1 with `CheckPasswordSignInAsync`** — that method is ③ (U2-build-verifies)
> and `PasswordSignInAsync` is the one that already compiles, so it is the safe leg-1.

**Pin: enforce MFA in the `AccountController.Login` POST (`src/Kumunita.Web/Controllers/
AccountController.cs`, the action at ~`:698-755`), immediately after the existing
`profile.Blocked` gate — i.e. at the exact seam the repo already uses for its one other
post-credential gate.** A `GlobalAdmin` (or any user with `IsTwoFactorEnabled == true`) cannot
complete sign-in without a valid TOTP or recovery code. **Other roles are unaffected** (their
`IsTwoFactorEnabled` is optional).

**The pinned flow (U4 implements against the 10.0.13 surface):**

1. **Leg 1 — password check, NO persistent cookie minted.**
   `var result = await signInManager.PasswordSignInAsync(user, model.Password,
   isPersistent: false, lockoutOnFailure: true);` — **① compiler-verified** (the current
   `AccountController.cs:716` uses exactly this shape; the **only** change from today is
   `isPersistent: true` → `isPersistent: false` so a not-yet-MFA'd sign-in does not mint the
   long-lived cookie). If `result.Succeeded` is false → fail as today (existing
   `AccountController` error path). If `Succeeded`, continue.
2. **Leg 1.5 — MFA gate (the idiomatic .NET 10 two-factor check).**
   `if (result.RequiresTwoFactor)` → **the account is MFA-protected** (regardless of role).
   **③ U4-build-verifies:** `result` is the `SignInResult` returned by `PasswordSignInAsync`;
   `RequiresTwoFactor` is the .NET 10 gate. (Fallback if `RequiresTwoFactor` does not resolve:
   `if (await signInManager.IsTwoFactorEnabledAsync(user))`, **② F3.U2-verified**.) On the
   gate:
   - **Return the MFA challenge** (the `/account/login` view with the code field, or a
     redirect to `/account/login/mfa`); do **not** yet redirect to `ReturnUrl`. The user is
     already in a **temporary** (non-persistent) sign-in from leg 1, so `User` is populated
     for the challenge view — **no separate pending-user store is required**.
   - On a correct code at the challenge leg, **Leg 2 — complete sign-in** (both methods are
     **② F3.U2-verified**):
     - **TOTP code:** `await signInManager.TwoFactorAuthenticatorSignInAsync(user,
       code, model.RememberMe);`
     - **Recovery code:** `await signInManager.TwoFactorRecoveryCodeSignInAsync(user,
       code, model.RememberMe);` (this method also enforces one-time-use + the
       `RecoveryCodesLeft` decrement, §6).
     - These two methods **complete the sign-in and mint the auth cookie** on success (the
       .NET 10 two-factor sign-in overloads issue the cookie — **③ U4-build-verifies** this;
       if a build shows they do **not** auto-mint, U4 adds a `HandleSignInAsync` follow-up and
       records it in the handoff note — **do not** pre-commit to `HandleSignInAsync`).
   - On a wrong code → fail (no persistent cookie minted); the temp sign-in is discarded by
     the next request / sign-out.
3. **Leg 1.5b — the "Enforce for GlobalAdmin" requirement.**
   `else if (IsGlobalAdmin(user) && !await signInManager.IsTwoFactorEnabledAsync(user))` → a
   `GlobalAdmin` who has **not** enrolled yet is **redirected to
   `/settings/security/mfa`** (enrollment) instead of proceeding — they cannot reach the
   admin surface with password-only. (This is the "enforce" half of AUTH-03; the *challenge*
   half is Leg 1.5.) **U4 must complete the sign-in for this user only after they enroll**
   (the enrollment POST flips `IsTwoFactorEnabled` and issues `ResetRecoveryCodes`, §4/§6).
4. **Leg 1.5c — the existing path for non-MFA users is UNCHANGED from today:** the
   `profile.Blocked` check (kept verbatim), then the persistent sign-in
   (`await signInManager.PasswordSignInAsync(user, model.Password, isPersistent: true,
   lockoutOnFailure: true)` — **① compiler-verified**, the current code's exact shape) and the
   persistent redirect. This is the **`NonAdmin_MfaOptional`** guarantee — a non-`GlobalAdmin`
   with `IsTwoFactorEnabled == false` signs in **exactly as today** (no change at all for this
   branch).

**Why this seam (pin the rationale, it is the deciding argument):**
- The repo's own precedent for "a gate that must be able to *fail* sign-in" is the
  **block enforcement at the login seam** (`AccountController.cs:~726`): *"Kept here (not in
  the factory) because the factory cannot fail sign-in; the controller is the one place that
  can inspect the account after credentials are verified."* MFA is exactly such a gate — it
  must be able to **withhold** the auth cookie — so it belongs here for the same reason.
- The controller **already injects** `SignInManager<User>` **and** `UserManager<User>`
  (`AccountController.cs:31-35`) — **zero new DI**, zero new service.
- It is **per-role-agnostic** to write (the check is `IsTwoFactorEnabledAsync(user)` +
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
  about the **login**, not an endpoint). **Optional hardening (open item, §9):**
  *additionally* gate the `/admin/*` controllers with a `[MfaRequired]` handler so a stale
  MFA-less cookie can't reach admin pages — but the controller login gate is the contract.

**`IsGlobalAdmin(user)`** = `(await userManager.GetRolesAsync(user)).Contains(Roles.GlobalAdmin)`
(`Roles.GlobalAdmin`, `ThinPrincipal.cs:47`). Same assertion shape as `IdentityService.cs:902`.

---

## 6. Recovery path (pin the full shape)

### 6.1 Generation
- **Count: 10** — there is **no .NET 10.0.13 stock recovery-code method** (the .NET ≤8
  `GetTwoFactorRecoveryCodesAsync` is **absent**). The 10 codes are the set **the app mints**
  and stores on the `User` row via `user.ResetRecoveryCodes` (10.0.13 property, **verified
  present**); `user.RecoveryCodesLeft` (10.0.13 property) is the count a steady GET shows. Pin
  10; do not invent a different number. (If U2's `10.0.13` check shows the provider expects a
  different shape for `ResetRecoveryCodes` — e.g. a delimited string vs. a JSON array — record
  it in the handoff note and keep the test asserting the **count** dynamically, not
  hardcoding 10.)
- **Issued at:** (a) the **enable** POST (§4 `MfaEnable`) — first 10 codes, returned once;
  (b) the **rotate** POST (§4 `MfaRotateRecovery`) — replaces the current set, returned once.

### 6.2 One-time use
- A recovery code presented at the challenge leg is consumed by the **sign-in leg**
  `signInManager.TwoFactorRecoveryCodeSignInAsync(user, code, rememberMe)` — the **10.0.13
  method that enforces one-time use and decrements `user.RecoveryCodesLeft`** (the .NET ≤8
  `ReuseTwoFactorRecoveryCodeAsync` is **absent**). A second presentation of the same code is
  rejected. This is the `GlobalAdminRecoveryCode_WorksOnce` guarantee. **U2 must verify at
  build** that `TwoFactorRecoveryCodeSignInAsync` performs the consume + decrement (if the
  10.0.13 surface requires an explicit `RecoveryCodesLeft` decrement in the controller, pin
  that in the handoff note — do not guess).

### 6.3 Rotate
- `user.ResetRecoveryCodes = <new set of 10>` + `SaveChangesAsync` **atomically swaps** the
  whole set (the .NET ≤8 `ReplaceTwoFactorRecoveryCodesAsync` is **absent**; the 10.0.13
  property **is** present). The new 10 are shown **once**; the old 10 are dead. The
  steady-state GET (§4 `MfaRecovery()`) shows only the **count** (`user.RecoveryCodesLeft`).

### 6.4 The "all recovery codes used" edge case (pin the escape — no backdoor)
- If **all 10 recovery codes are consumed** *and* the TOTP authenticator is lost, the
  `GlobalAdmin` **cannot sign in** (both second factors are gone). **Do NOT add a
  backdoor** (no "skip MFA" flag, no admin override that bypasses the second factor).
- **The escape is the existing operator lane** (reuse, don't reinvent):
  - **Break-glass:** the operator invokes the existing **break-glass** lane
    (`IdentityService.ConsumeBreakGlassAsync`, `IdentityService.cs:~1319`) to re-enable
    access, then a GlobalAdmin peer (or the operator) resets MFA via
    `user.IsTwoFactorEnabled = false` + `SaveChangesAsync` + clearing the secret (the §4
    `MfaDisable` action — the .NET ≤8 `SetTwoFactorEnabledAsync(user, false)` is **absent**; the
    10.0.13 property **is** present), and re-enrolls.
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
   `IsTwoFactorEnabled == true` posts a correct password to `Login`. Assert the response is the
   **challenge** (not a redirect to `ReturnUrl`), i.e. sign-in is **not** complete, and a
   subsequent correct TOTP code (`TwoFactorAuthenticatorSignInAsync`) completes the **persistent**
   sign-in. Proves §5 Leg 1.5.
2. **`GlobalAdminRecoveryCode_WorksOnce`** — a `GlobalAdmin` at the challenge leg presents
   a **recovery code**. Assert (a) the first presentation **succeeds** via
   `TwoFactorRecoveryCodeSignInAsync` and completes sign-in; (b) the **same** code presented
   again is **rejected** (consumed + `RecoveryCodesLeft` decremented, §6.2). Proves §6.
3. **`NonAdmin_MfaOptional`** — a non-`GlobalAdmin` (e.g. `Member`) with
   `IsTwoFactorEnabled == false` posts a correct password. Assert sign-in completes
   **directly** (persistent redirect, no challenge) — MFA is **optional** for them. Proves
   §5 Leg 1.5c.

**Driving note for U5:** the enforcement logic lives in `AccountController` (Web) and the
sign-in leg is on `SignInManager` (10.0.13 two-factor surface, §2.1). The tests drive the
**public controller actions** (`Login`, `LoginMfa`) — do **not** make the §5 private helpers
public to test them. A `GlobalAdmin` role is seeded via `RoleManager`/`userManager.AddToRoleAsync`
(the same setup the F1 `IdentityServiceDeleteLaneGdprTests` harness uses for a GlobalAdmin
actor). **Recovery codes are seeded by setting `user.ResetRecoveryCodes` +
`user.IsTwoFactorEnabled = true` + `SaveChangesAsync`** (the 10.0.13 properties — the .NET ≤8
`GetTwoFactorRecoveryCodesAsync`/`EnsureTwoFactorAsync` are **absent**).

---

## 8. Drift / scope notes (record for the owner + U2–U5 — do not silently resolve)

> **Re-pinned against `Microsoft.AspNetCore.Identity 10.0.13`** (verified by `strings` on
> `…/packs/Microsoft.AspNetCore.App.Ref/10.0.13/ref/net10.0/Microsoft.AspNetCore.Identity.dll`).
> Items 2, 3, 4, 7 below were rewritten by this re-pin; the rest are preserved.

1. **The plan's named hook, `PrivilegedStampMiddleware`, is the wrong layer for MFA.** It is
   a per-request **role-revocation** middleware (`Program.cs:773`,
   `Security/PrivilegedStampMiddleware.cs:43`) that only knows how to `SignOutAsync` +
   `Redirect`; it cannot present a challenge or complete a two-leg sign-in. The grounded
   enforcement seam is **`AccountController.Login` POST** (the block-gate precedent, §5).
   **Recorded, not resolved against the plan** — the plan's F3 U4 said "a middleware after
   `PrivilegedStampMiddleware`"; the pin supersedes that with the controller seam.
2. **The token provider is not registered today — U2 adds `.AddDefaultTokenProviders()`.**
   Grep `AddTokenProvider|AddDefaultTokenProviders` → zero hits in `src/**`. The .NET ≤8 pin
   line `.AddTokenProvider<TotpAuthenticatorTokenProvider>()` is **not a valid 10.0.13 call**:
   `TotpAuthenticatorTokenProvider` is **absent** from the 10.0.13 assembly (zero `totp`
   strings) and the parameterless `AddTokenProvider<T>()` overload is **absent** (the real one
   is `AddTokenProvider<TProvider>(string providerName)`). The **correct** 10.0.13 registration
   is **`.AddDefaultTokenProviders()`** (verified present; registers `DataProtectorTokenProvider<T>`).
   Without it, the first `CanGenerateTwoFactorTokenAsync` / `TwoFactorAuthenticatorSignInAsync`
   call throws. **This is the single most likely "it compiles but throws at runtime" trap —
   U2 must add it.**
3. **`IsTwoFactorEnabled` (10.0.13 property) maps onto the existing `AspNetUsers.TwoFactorEnabled` column.**
   The bare `TwoFactorEnabled` **property** is **absent** in 10.0.13 (`strings` shows only
   `get_/set_IsTwoFactorEnabled` + `IsTwoFactorEnabledAsync`). The DB column is still
   `TwoFactorEnabled` (`InitialIdentity.cs:50`); EF maps the 10.0.13 `IsTwoFactorEnabled`
   property onto it, so **no new column / no new migration for the flag**. **But** the
   `AspNetUsers` table has **no `RecoveryCodesLeft` / `ResetRecoveryCodes` column** in the repo's
   initial migration — these are **new 10.0.13 `IdentityUser` properties** whose EF mapping
   (column/table) **U2 must verify at build** (§2.2). If 10.0.13's `IdentityDbContext` maps
   them to new columns, **U2 adds an EF migration for them** — this is **not** the Marten-doc
   path (ADR 0004 still holds: they are Identity credential material). **Record the migration
   in the handoff note if needed; do NOT add a Marten doc.**
4. **Recovery codes moved from `UserManager` methods to `IdentityUser` properties (10.0.13).**
   The .NET ≤8 `Get/Replace/ReuseTwoFactorRecoveryCodesAsync` are **absent**. The 10.0.13
   surface is `user.RecoveryCodesLeft` (count) + `user.ResetRecoveryCodes` (set), and one-time
   use is enforced by `SignInManager.TwoFactorRecoveryCodeSignInAsync` (verified present). U2's
   "recovery-code store" is therefore **property access on the `User` row + EF save**, not
   three `UserManager` calls. **This refines the plan's F3 U2** ("TOTP provider + token store,
   ≤2 files").
5. **`Login` POST currently does a single `PasswordSignInAsync` — and that method IS PRESENT in 10.0.13 (① compiler-verified).**
   **Correction to the F3.U2 note's implication:** the F3.U2 agent tested `AddTokenProvider` +
   `TotpAuthenticatorTokenProvider` (both **absent**) but did **not** test `PasswordSignInAsync`.
   The current `AccountController.cs:716` calls `signInManager.PasswordSignInAsync(...)` and
   the repo builds green — **`PasswordSignInAsync` exists in 10.0.13**. The leg-1 change is
   minimal: `isPersistent: true` → `isPersistent: false` (the one behavior change to the
   existing login path, so a not-yet-MFA'd sign-in does not mint the long-lived cookie). The
   gate is `result.RequiresTwoFactor` (**③ U4-build-verifies**) or
   `IsTwoFactorEnabledAsync(user)` (**②**). Leg 2 is the **② F3.U2-verified**
   `TwoFactorAuthenticatorSignInAsync` / `TwoFactorRecoveryCodeSignInAsync`. **Do NOT replace
   leg-1 with `CheckPasswordSignInAsync`** (③ U2-build-verifies; unnecessary when
   `PasswordSignInAsync` already compiles). The `NonAdmin_MfaOptional` test still guards the
   non-MFA branch (leg 1.5c is unchanged from today).
6. **A lone `GlobalAdmin` disabling their own MFA is a self-lockout-of-security** (§4
   `MfaDisable`). Pin: that action must require a TOTP/recovery proof and write an audit row
   (`Authorization.AccessAudit`, the `seed-admin.setup` shape, `IdentityService.cs:~1275`).
   Record it; do not let a password-only POST remove the second factor.
7. **Identity-API version — RESOLVED by this re-pin (was the §8.7 "verify signatures" caution).**
   The original pin's "stock `Microsoft.AspNetCore.Identity` surface" assumed the .NET ≤8 API.
   **What is confirmed ABSENT in 10.0.13 (both by the F3.U2 note's `strings` sweep AND this
   unit's `strings` sweep of the 10.0.13 ref dll):** `TotpAuthenticatorTokenProvider`, the
   parameterless `AddTokenProvider<T>()` overload, and the entire .NET ≤8 `UserManager` TOTP
   method set (`EnsureTwoFactorAsync`, `SetTwoFactorEnabledAsync`, `GenerateTwoFactorTokenAsync`,
   `ValidateTwoFactorTokenAsync`, `Get/Replace/ReuseTwoFactorRecoveryCodesAsync`,
   `Get/SetAuthenticationTokenAsync`, `Get/SetAuthenticatorKeyAsync`). **What is present**
   (by tier, §2.1): ① `PasswordSignInAsync` (compiler-verified); ② **F3.U2-verified** (the
   F3.U2 note's finding-3 list, exactly 7 names + the named overload from its CS7036 error):
   `CanGenerateTwoFactorTokenAsync`, `TwoFactorAuthenticatorSignInAsync`,
   `TwoFactorRecoveryCodeSignInAsync`, `ValidateTwoFactorSecurityStampAsync`,
   `AddDefaultTokenProviders`, `DataProtectorTokenProvider<T>`, `IUserTwoFactorTokenProvider<T>`,
   and the named `AddTokenProvider<TProvider>(string)`; ③ **this-unit `strings` sweep** (NOT in
   the F3.U2 note): `IsTwoFactorEnabled`, `RecoveryCodesLeft`, `ResetRecoveryCodes`,
   `IsTwoFactorEnabledAsync`, `CheckPasswordSignInAsync`, `HandleSignInAsync`,
   `RefreshSignInAsync`, `SignInOrTwoFactorAsync`, `StoreTwoFactorInfo`,
   `GetTwoFactorAuthenticationUserAsync`, `SignInResult.RequiresTwoFactor`,
   `TwoFactorAuthenticationInfo`. **U2/U4 must confirm the ③ names' exact signatures + the
   property EF-mapping at build before relying on them** — the re-pin is honest about which
   names are ①/② (guaranteed) vs ③ (to-be-confirmed), which is what the F3.U2 drift pause could
   not guarantee (it only tested the registration line, not the sign-in surface).
8. **Old→new API mapping (for the owner's diff — the re-derivation):**

   | Concern | **.NET ≤8 (original pin)** | **10.0.13 (re-pinned)** |
   |---|---|---|
   | Enable flag | `user.TwoFactorEnabled` (property) | `user.IsTwoFactorEnabled` (property, ③) |
   | Enable async | `UserManager.EnsureTwoFactorAsync` (**absent**) | `user.IsTwoFactorEnabled = true` + `SaveChangesAsync` (③) |
   | Disable async | `UserManager.SetTwoFactorEnabledAsync(user, false)` (**absent**) | `user.IsTwoFactorEnabled = false` + `SaveChangesAsync` (③) |
   | Enable-check | `user.TwoFactorEnabled` | `SignInManager.IsTwoFactorEnabledAsync(user)` (③) |
   | Generate TOTP (enrollment) | `UserManager.GenerateTwoFactorTokenAsync(user, prov, "Token")` (**absent**) | `SignInManager.CanGenerateTwoFactorTokenAsync(user)` (②) + the provider's token `GenerateAsync` |
   | Validate TOTP (challenge) | `UserManager.ValidateTwoFactorTokenAsync(user, prov, code)` (**absent**) | `SignInManager.TwoFactorAuthenticatorSignInAsync(user, code)` (②) |
   | **Sign-in leg 1** | `SignInManager.PasswordSignInAsync(user, pw, isPersistent, lockout)` | **UNCHANGED — `PasswordSignInAsync` is ① compiler-verified (present in 10.0.13)**; only `isPersistent` flips `true`→`false` for the MFA branch |
   | **Two-factor gate** (new) | (n/a in .NET ≤8 — `user.TwoFactorEnabled` read) | `SignInResult.RequiresTwoFactor` (③) or `IsTwoFactorEnabledAsync(user)` (③) |
   | **Sign-in leg 2** (new) | (n/a in .NET ≤8 — re-ran `PasswordSignInAsync(isPersistent:true)`) | `TwoFactorAuthenticatorSignInAsync(user, code)` (②) / `TwoFactorRecoveryCodeSignInAsync(user, code)` (②) |
   | Sign-in cookies | (minted by `PasswordSignInAsync`) | minted by the ② leg-2 methods (③ U4-build-verifies; fall back to `HandleSignInAsync` (③) if needed) |
   | Recovery read | `UserManager.GetTwoFactorRecoveryCodesAsync(user)` (**absent**) | `user.RecoveryCodesLeft` (count, ③) |
   | Recovery rotate | `UserManager.ReplaceTwoFactorRecoveryCodesAsync(user, codes)` (**absent**) | `user.ResetRecoveryCodes = <set>` + `SaveChangesAsync` (③) |
   | Recovery consume | `UserManager.ReuseTwoFactorRecoveryCodeAsync(user, code)` (**absent**) | `SignInManager.TwoFactorRecoveryCodeSignInAsync(user, code)` (②, enforces one-time use) |
   | Provider type | `TotpAuthenticatorTokenProvider` (**absent**) | `IUserTwoFactorTokenProvider<T>` (②) (+ `DataProtectorTokenProvider<T>` default, ②) |
   | Registration | `.AddTokenProvider<TotpAuthenticatorTokenProvider>()` (**absent**) | `.AddDefaultTokenProviders()` (②) — or the **named** `AddTokenProvider<TProvider>("Default")` (②, from F3.U2's CS7036) for a custom provider |
   | Secret storage | `Get/SetAuthenticationTokenAsync(user, prov, "Token")` (**absent**) | provider-owned token store (`IUserTwoFactorTokenProvider` (②) — ③ U2 verifies column vs. `AspNetUserTokens`) |

---

## 9. Exit (what must hold before U2 is considered ready)

- This file exists and pins: the **storage choice + the rejection of the Marten-doc option**
  (§2), the **TOTP registration** line + the no-new-package grounding (§3), the **routes +
  view-model shape** (§4), the **enforcement seam** (the `AccountController.Login` POST, with
  the `PrivilegedStampMiddleware` rejection grounded) (§5), the **recovery path** incl. the
  "all codes used" escape (§6), and the **three test names** (§7).
- **No `.cs` file is modified by this unit** (design-only). **No build is run.**
- The `## F3.U1 — Part-0 design pin (re-pinned)` section is **appended** to
  `hardening-handoff-notes.md` (the §2 old→new, §3 old→new, the §5/§6/§7 adjustments, the
  assembly names verified, and "Next unit = **F3.U2 (re-issue)**") — **written before
  stopping.**
- **F3.U2 (re-issue) is NOT started** — it is the next clean-slate agent.

**Open items (owner, NOT assumed done):**
- **(a)** the §6.4 "all codes used → break-glass / second-GlobalAdmin" runbook is an
  **owner-attested** procedure (operator lane, not this unit);
- **(b)** the **recovery-code EF mapping** (§8.3): whether 10.0.13's `IdentityDbContext`
  maps `RecoveryCodesLeft` / `ResetRecoveryCodes` to **new `AspNetUsers` columns** (→ U2
  adds an EF migration — record it) or reuses existing storage. This is the **one possible
  migration** F3 introduces (still EF/Identity-owned, ADR 0004 intact); U2 confirms at build
  and records the outcome.
- **(c)** the **TOTP provider choice** (§3): the default `AddDefaultTokenProviders()`
  (data-protection token) is the minimal build-verifiable path; if the owner wants a genuine
  authenticator-app (otpauth:// QR) TOTP, U2 implements a custom `IUserTwoFactorTokenProvider`.
  **Owner decision, not a default.**
