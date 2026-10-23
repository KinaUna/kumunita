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
> Marten/Wolverine. **Round-2 (this re-pin):** the 10.0.13 two-factor surface is the
> **classic `UserManager` TOTP + recovery-code API** (`Get/SetTwoFactorEnabledAsync`,
> `Generate/ValidateTwoFactorTokenAsync`, `GenerateNewAuthenticatorKey`,
> `GenerateNewTwoFactorRecoveryCodesAsync` / `RedeemTwoFactorRecoveryCodeAsync` /
> `CountRecoveryCodesAsync`) plus the **TOTP `AuthenticatorTokenProvider<T>`** (RFC 6238) and
> the **`SignInManager` two-leg sign-in** (`PasswordSignInAsync` → `TwoFactorAuthenticatorSignInAsync`
> / `TwoFactorRecoveryCodeSignInAsync`). The `IdentityUser` *properties*
> `IsTwoFactorEnabled` / `RecoveryCodesLeft` / `ResetRecoveryCodes` claimed by the round-1 pin
> **DO NOT EXIST in 10.0.13** (F3.U2 CS1061 ×3; round-2 reflection confirms `IdentityUser` has
> none of them). Every member named in §2.1 is **① round-2 reflection-verified** against the
> real 10.0.13 PE files, so U2/U4 know exactly which names are guaranteed.

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

## 2. Storage — **EF-owned Identity row + the token store; ADR 0004 preserved; NO new Marten doc; NO new migration**

> **RE-PINNED AGAINST THE REAL `Microsoft.AspNetCore.Identity 10.0.13` — round 2.**
> This unit re-derived §2 from **direct reflection of the actual 10.0.13 PE files**
> (`System.Reflection.Metadata` over `Microsoft.Extensions.Identity.Core.dll`,
> `Microsoft.Extensions.Identity.Stores.dll`, and
> `Microsoft.AspNetCore.Identity.EntityFrameworkCore.dll` 10.0.13). The live metadata is the
> authority — not the round-1 pin, not the F3.U2 note, and not `strings`.
>
> **The round-1 §2 model is DISPROVEN.** Round 1 claimed the recovery codes live on
> `IdentityUser` as properties (`IsTwoFactorEnabled` / `RecoveryCodesLeft` /
> `ResetRecoveryCodes`). F3.U2's build proved all three are **ABSENT** from the repo's `User`
> (CS1061 ×3). This unit's reflection confirms `IdentityUser` has **none** of them — in 10.0.13
> those three names exist **only** on the `Microsoft.AspNetCore.Identity.Data.TwoFactorResponse`
> **DTO** (the Identity Endpoints API model), which is not a user row. The real model is the
> **classic `UserManager` two-factor API** + the **TOTP `AuthenticatorTokenProvider<T>`**, with
> the secret and recovery codes persisted in the **existing `AspNetUserTokens`** table. Every
> member named in §2.1 is **① verified present** by this unit's direct reflection.

### 2.1 What is present in 10.0.13 (verified — the live assembly is the authority)

> **Verification tiers (round-2 honesty):**
> - **① direct-reflection-verified** = this unit read the member out of the **real 10.0.13
>   PE files** via `System.Reflection.Metadata` (no assembly loading, no `strings`, no prior
>   note). If it is in the table, it is in 10.0.13. This is the strongest tier and the one that
>   governs the pin.
> - **①' compiler-verified** = the current repo code compiles using it against 10.0.13
>   (`PasswordSignInAsync` at `AccountController.cs:716` builds green; `AddDefaultTokenProviders()`
>   at `Program.cs:403` builds green per F3.U2).
> - **⊗ build-verified absent** = F3.U2's repo compiler emitted CS1061 for it against `User`
>   (`IsTwoFactorEnabled`, `RecoveryCodesLeft`, `ResetRecoveryCodes`).
>
> **Correcting the record (important for U2/U3):** the F3.U2 note's bulk-assembly "ABSENT"
> verdict for `CanGenerateTwoFactorTokenAsync`, `IUserTwoFactorTokenProvider`,
> `DataProtectorTokenProvider`, `TwoFactorAuthenticationInfo` was a **FALSE NEGATIVE** — F3.U2
> itself documented that its scan wrongly reported `AddDefaultTokenProviders` (build-proven
> present) as absent. This unit's direct reflection confirms **all four ARE present**. Likewise
> the `UserManager` TOTP + recovery-code method set that round-1/F3.U2 called "absent" is
> **PRESENT** in 10.0.13 (see table). **U2 must not delete any code on the basis of the
> round-1/F3.U2 "absent" lists.**

| Concern | **10.0.13 member (real)** | Verified in | Tier |
|---|---|---|---|
| Enable flag (property) | `IdentityUser.TwoFactorEnabled` (`bool`) | `IdentityUser` (Stores) | ① |
| Enable flag (read) | `UserManager.GetTwoFactorEnabledAsync` | `UserManager<T>` (Core) | ① |
| Enable flag (write) | `UserManager.SetTwoFactorEnabledAsync` | `UserManager<T>` (Core) | ① |
| Enable flag (sign-in path) | `SignInManager.IsTwoFactorEnabledAsync` | `SignInManager<T>` (Identity) | ① |
| Two-factor gate (login result) | `SignInResult.RequiresTwoFactor` (`bool`) + `TwoFactorRequired` | `SignInResult` (Core) | ① |
| Valid 2FA providers | `UserManager.GetValidTwoFactorProvidersAsync` | `UserManager<T>` (Core) | ① |
| TOTP — generate | `UserManager.GenerateTwoFactorTokenAsync` | `UserManager<T>` (Core) | ① |
| TOTP — validate | `UserManager.ValidateTwoFactorTokenAsync` | `UserManager<T>` (Core) | ① |
| **TOTP provider (real)** | `AuthenticatorTokenProvider<T>` + `Rfc6238AuthenticationService.ComputeTotp` | Identity / Core | ① |
| TOTP — "can generate?" | `IUserTwoFactorTokenProvider<T>.CanGenerateTwoFactorTokenAsync` (+ on `AuthenticatorTokenProvider<T>`, `DataProtectorTokenProvider<T>`) | provider iface + impls | ① |
| **TOTP secret (authenticator key)** | `UserManager.GenerateNewAuthenticatorKey` / `GetAuthenticatorKeyAsync` / `ResetAuthenticatorKeyAsync`; store `IUserAuthenticatorKeyStore<T>` (`Get/SetAuthenticatorKeyAsync`) | Core | ① |
| **Recovery codes — (re)issue** | `UserManager.GenerateNewTwoFactorRecoveryCodesAsync` | `UserManager<T>` (Core) | ① |
| **Recovery codes — redeem** | `UserManager.RedeemTwoFactorRecoveryCodeAsync` | `UserManager<T>` (Core) | ① |
| **Recovery codes — count** | `UserManager.CountRecoveryCodesAsync` | `UserManager<T>` (Core) | ① |
| Recovery-code store iface | `IUserTwoFactorRecoveryCodeStore<T>`: `ReplaceCodesAsync` / `RedeemCodeAsync` / `CountCodesAsync` | Core | ① |
| Two-factor store iface | `IUserTwoFactorStore<T>`: `SetTwoFactorEnabledAsync` / `GetTwoFactorEnabledAsync` | Core | ① |
| Token store (key+codes backing) | `UserManager.GetAuthenticationTokenAsync` / `SetAuthenticationTokenAsync` / `RemoveAuthenticationTokenAsync` (raw token access); EF backing `UserStore<10>.UserTokens` + `AddUserTokenAsync` / `FindTokenAsync` / `RemoveUserTokenAsync` | Core / EFCore | ① |
| Pending user (2FA) | `SignInManager.GetTwoFactorAuthenticationUserAsync` | `SignInManager<T>` | ① |
| Pending user + metadata DTO | `TwoFactorAuthenticationInfo` (`User`, `LoginProvider`) | Identity | ① |
| Leg-2 sign-in (TOTP) | `SignInManager.TwoFactorAuthenticatorSignInAsync` | `SignInManager<T>` | ① |
| Leg-2 sign-in (recovery) | `SignInManager.TwoFactorRecoveryCodeSignInAsync` | `SignInManager<T>` | ① |
| Leg-2 combined / entry | `SignInManager.TwoFactorSignInAsync`, `SignInOrTwoFactorAsync` | `SignInManager<T>` | ① |
| Security-stamp re-check | `SignInManager.ValidateTwoFactorSecurityStampAsync` | `SignInManager<T>` | ① |
| Leg-1 (password) | `SignInManager.PasswordSignInAsync` | `SignInManager<T>` | ①' (`AccountController`) |
| EF store (tokens) | `UserStore<10>.UserTokens` + `AddUserTokenAsync` / `FindTokenAsync` / `RemoveUserTokenAsync` | EFCore `UserStore<10>` | ① |
| Provider registration (ext.) | `IdentityBuilderExtensions.AddDefaultTokenProviders` | Identity | ①' (`Program.cs:403`) |
| Provider registration (named) | `IdentityBuilder.AddTokenProvider` (×2 overloads) | `IdentityBuilder` (Core) | ① |
| Token options (issuer) | `TokenOptions.AuthenticatorTokenProvider` / `AuthenticatorIssuer` | `TokenOptions` (Core) | ① |

> **What is ABSENT in 10.0.13 (the honest list):**
> - **`TotpAuthenticatorTokenProvider`** — **ABSENT** (no such type in 10.0.13; the TOTP
>   provider is `AuthenticatorTokenProvider<T>` + `Rfc6238AuthenticationService`). Round-1 and
>   F3.U2 both flagged this; it is genuinely absent.
> - **`IdentityUser.IsTwoFactorEnabled`** — **ABSENT** (F3.U2 CS1061; the property is
>   `TwoFactorEnabled`).
> - **`IdentityUser.RecoveryCodesLeft`** / **`IdentityUser.ResetRecoveryCodes`** — **ABSENT**
>   (F3.U2 CS1061; `IdentityUser` has no recovery-code members — those names live only on the
>   `Data.TwoFactorResponse` DTO, not the user row).
> - **`CanGenerateTwoFactorTokenAsync` on `SignInManager`** — **ABSENT** on `SignInManager` (it
>   is a **provider** method — on `IUserTwoFactorTokenProvider<T>` / `AuthenticatorTokenProvider<T>`
>   / `DataProtectorTokenProvider<T>`). Round-1 misattributed it to `SignInManager`.
>
> **What round-1/F3.U2 got WRONG (corrected by round-2):**
> - `CanGenerateTwoFactorTokenAsync`, `IUserTwoFactorTokenProvider<T>`, `DataProtectorTokenProvider<T>`,
>   `TwoFactorAuthenticationInfo` — all **PRESENT** (round-2 reflection) despite F3.U2's
>   false-negative "absent/UNKNOWN" verdicts.
> - The `UserManager` TOTP + recovery-code method set (`Generate/ValidateTwoFactorTokenAsync`,
>   `GetValidTwoFactorProvidersAsync`, `Get/SetTwoFactorEnabledAsync`,
>   `GenerateNewTwoFactorRecoveryCodesAsync`, `RedeemTwoFactorRecoveryCodeAsync`,
>   `CountRecoveryCodesAsync`, `Get/SetAuthenticatorKeyAsync`) — all **PRESENT** (round-2
>   reflection) despite round-1/F3.U2 "absent" claims.
>
> **Enable-flag naming (corrected):** the property is **`TwoFactorEnabled`** (NOT
> `IsTwoFactorEnabled`). The `AspNetUsers.TwoFactorEnabled` **column** is unchanged
> (`InitialIdentity.cs:50`). **No new column, no new migration for the flag.**

### 2.2 Storage mechanism (grounded — the real 10.0.13 path)

- **Enable flag:** the `AspNetUsers.TwoFactorEnabled` column (already present,
  `InitialIdentity.cs:50`), read/written via `UserManager.Get/SetTwoFactorEnabledAsync`
  (→ `IUserTwoFactorStore<T>` → the `AspNetUsers` column). **No new column, no new migration.**
- **TOTP secret (authenticator key):** the base32 key is the provider's `AuthenticatorKey`.
  Generated once at enrollment via `UserManager.GenerateNewAuthenticatorKey()` (returns the key;
  the app builds the `otpauth://` URL from it + `TokenOptions.AuthenticatorIssuer`). Stored via
  the **token store** as `AspNetUserTokens(LoginProvider="Authenticator", Name="Key",
  Value=<base32 key>)`. Validated at the challenge leg via
  `UserManager.ValidateTwoFactorTokenAsync(user, "Authenticator", code)` →
  `AuthenticatorTokenProvider<T>.ValidateAsync` (`Rfc6238AuthenticationService.ComputeTotp`).
  **Reuses `AspNetUserTokens`; no new column.**
- **Recovery codes:** 10 codes, generated once at enrollment via
  `UserManager.GenerateNewTwoFactorRecoveryCodesAsync(user)` (returns the set; shown **once**),
  stored as rows in **`AspNetUserTokens`** (via `IUserTwoFactorRecoveryCodeStore<T>.
  ReplaceCodesAsync`, backed by the token store). **Redeemed** (one-time) via
  `UserManager.RedeemTwoFactorRecoveryCodeAsync(user, code)` (or leg-2
  `SignInManager.TwoFactorRecoveryCodeSignInAsync`). **Counted** (steady GET) via
  `UserManager.CountRecoveryCodesAsync(user)`. **Rotated** by calling
  `GenerateNewTwoFactorRecoveryCodesAsync` again. **Reuses `AspNetUserTokens`; no new column.**
- **The `AspNetUserTokens` table already exists** in the schema (`InitialIdentity.cs:156`,
  `AppDbContextModelSnapshot:206/267`) and the EF `UserStore<10>` already implements the token
  store (`AddUserTokenAsync` / `FindTokenAsync` / `RemoveUserTokenAsync`, verified ①).

### 2.3 Storage decision (PIN) + rejected options

- **PIN: NO new EF migration is required.** The enable flag is a pre-existing column; the TOTP
  secret and recovery codes are rows in the pre-existing `AspNetUserTokens` table. **The round-1
  open item (b) — "U2 confirms at build whether a new EF migration is needed for
  `RecoveryCodesLeft`/`ResetRecoveryCodes`" — is MOOT: the round-1 model was wrong, and the real
  mechanism needs no new storage.**
- **A new `MfaSecret` column on `AspNetUsers`** — *rejected.* The provider/`UserManager` owns the
  key; a bespoke column would bypass `AuthenticatorTokenProvider<T>` + the token store and is a
  new migration for zero benefit.
- **A new `IdentityMfa` *Marten* doc** — *rejected on ADR 0004 grounds.* ADR 0004: "**Marten**
  owns the domain documents … **EF Core is used only for ASP.NET Identity tables**." A TOTP secret
  + recovery codes + the enable flag are **Identity credential material** — they belong in the
  `identity` schema (the existing `AspNetUsers` / `AspNetUserTokens` tables), not as a `mt` doc.
  **Pin: the Marten-doc option is not chosen — unchanged by the re-pin.**

### 2.4 Consequence for U2/U3 (scope)

U2/U3's "token store" is **not a new file and not a new migration** — it is the **existing
`AspNetUserTokens` table** reached through the **`UserManager`** two-factor API (the flag, the
authenticator key, the recovery codes). U2's real deliverables: (a) the provider registration
(§3 — `AddDefaultTokenProviders()` is **already present** at `Program.cs:403` per F3.U2 — do not
re-add it), (b) confirming at build that `UserManager`'s `GenerateNewTwoFactorRecoveryCodesAsync` /
`RedeemTwoFactorRecoveryCodeAsync` / `CountRecoveryCodesAsync` / `GenerateNewAuthenticatorKey`
resolve (they do — ①) and that `ValidateTwoFactorTokenAsync` accepts a generated code, (c) the
`AuthenticatorTokenProvider<T>` TOTP round-trip (generate → validate). **No new migration, no new
`<PackageReference>`, no Marten doc.**

---

## 3. Registration — **`AddDefaultTokenProviders()` already present (10.0.13); TOTP via `AuthenticatorTokenProvider<T>`**

> **RE-PINNED round-2.** `AddDefaultTokenProviders()` (extension, verified ①' present on
> `IdentityBuilderExtensions`) **is already present** in the repo at `Program.cs:403` (F3.U2
> landed it, build green). **U2 does NOT re-add it.** It registers the default token providers,
> including **`DataProtectorTokenProvider<T>`** (verified ① present) and the TOTP
> **`AuthenticatorTokenProvider<T>`** (verified ① present) — the provider
> `UserManager.GenerateTwoFactorTokenAsync` / `ValidateTwoFactorTokenAsync` and the
> `SignInManager` leg-2 sign-in resolve for the `"Authenticator"` login provider.

**Pin (state):** `.AddDefaultTokenProviders()` is already on the `AddIdentity<User,
IdentityRole>(…)` chain (`src/Kumunita.Web/Program.cs:396-403`):
`… .AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders()
.AddClaimsPrincipalFactory<KumunitaClaimsPrincipalFactory>()`. **Do not re-add it.**

- **Why it is load-bearing (corrected):** `UserManager.GenerateTwoFactorTokenAsync` /
  `ValidateTwoFactorTokenAsync` and `SignInManager.TwoFactorAuthenticatorSignInAsync` resolve a
  registered `IUserTwoFactorTokenProvider<User>` for the `"Authenticator"` login provider.
  `AddDefaultTokenProviders()` supplies it. Without it the first two-factor call throws
  ("no token provider registered" — the `Resources.NoTokenProvider` string, verified ① present).
- **TOTP provider — RESOLVED (round-2):** the authenticator-app TOTP provider in 10.0.13 is
  **`AuthenticatorTokenProvider<T>`** (RFC 6238, `Rfc6238AuthenticationService.ComputeTotp`,
  verified ① present), registered by `AddDefaultTokenProviders()`. **No custom provider is
  needed** for a genuine otpauth:// QR experience — the TOTP key comes from
  `UserManager.GenerateNewAuthenticatorKey()` and the `otpauth://` URL is built by the app from
  that key + `TokenOptions.AuthenticatorIssuer`. The round-1 open item (c) "custom
  `IUserTwoFactorTokenProvider` for a genuine TOTP experience" is therefore **unneeded** — the
  stock provider already gives it.
- **`CanGenerateTwoFactorTokenAsync` is a provider method, NOT on `SignInManager`** (round-1
  misattribution, corrected). To ask "can this user generate a 2FA token?" call it on the
  provider (e.g. the `AuthenticatorTokenProvider<User>` from DI), or rely on
  `UserManager.GetValidTwoFactorProvidersAsync(user)` (verified ① present) to enumerate the
  valid providers for the user.
- **Named registration (if ever needed):** `IdentityBuilder.AddTokenProvider` has **two
  overloads** (verified ① present) — the named form. Use only if adding a custom provider; the
  default path already covers F3.
- **No new package:** all of the above live in `Microsoft.AspNetCore.Identity` /
  `Microsoft.Extensions.Identity.Core` / `Microsoft.Extensions.Identity.Stores`, already
  reachable via `Kumunita.Core.csproj`'s `Microsoft.AspNetCore.Identity.EntityFrameworkCore
  10.0.13`. **Do not add a `<PackageReference>`.** The round-1 "add the package" contingency is
  **MOOT**.

---

## 4. Routes — **a new `settings/security` controller (the plan's exact URLs)**

There is **no existing `/settings/security/*` controller** (grep `settings/security` →
zero hits). The settings-tab precedent is `MessagingSettingsController` at
`/settings/messaging` (`src/Kumunita.Web/Controllers/MessagingSettingsController.cs:43-47`,
a `[Authorize]` controller in `Kumunita.Web.Controllers`). **Pin the same shape for F3:**
a new `[Authorize]` controller (name it `SecuritySettingsController`, action namespace
`Kumunita.Web.Controllers`), `[Route("settings/security")]`, with these actions. **The repo's
`AccountController` already injects `UserManager<User>` and `SignInManager<User>`
(`AccountController.cs:31-35`)** — the enrollment actions call `UserManager` (the ①-verified
two-factor API, §2.1); the challenge leg calls `SignInManager` (leg-2, §5).

| URL | HTTP | Action | Purpose (round-2, real API) |
|---|---|---|---|
| `/settings/security/mfa` | GET | `Mfa()` | Enrollment state. **If not enabled:** show the one-time **setup key** (`userManager.GenerateNewAuthenticatorKey()` → base32) + **otpauth:// URI** (QR, built from that key + `TokenOptions.AuthenticatorIssuer`) + the **10 recovery codes** (`userManager.GenerateNewTwoFactorRecoveryCodesAsync`, shown **once**). **If already enabled:** status + **count only** (`userManager.CountRecoveryCodesAsync`, never the values). |
| `/settings/security/mfa` | POST | `MfaEnable(MfaEnableViewModel)` | **Verify** the first TOTP code the resident typed (`userManager.ValidateTwoFactorTokenAsync(user, "Authenticator", code)`), **then** flip the flag `userManager.SetTwoFactorEnabledAsync(user, true)` (the .NET ≤8 `EnsureTwoFactorAsync` does not exist — this is the real call). The 10 recovery codes were already minted by the GET and shown **once**; this POST does not re-serve them. |
| `/settings/security/mfa/recovery` | GET | `MfaRecovery()` | Show **count only** (`userManager.CountRecoveryCodesAsync`, never the values — they were shown once at enable/rotate). |
| `/settings/security/mfa/recovery` | POST | `MfaRotateRecovery()` | **Rotate**: `userManager.GenerateNewTwoFactorRecoveryCodesAsync(user)` again → the new 10 are shown **once**; the old 10 are dead. |
| `/settings/security/mfa` | POST | `MfaDisable(MfaDisableViewModel)` | (Optional, recommended) Re-verify a TOTP code, then `userManager.SetTwoFactorEnabledAsync(user, false)` + clear the key (`userManager.ResetAuthenticatorKeyAsync(user)` / `RemoveAuthenticationTokenAsync`). **For a `GlobalAdmin`, this is the one action that must carry the enforcement caution** (§8.6): a lone GlobalAdmin disabling MFA removes their own second factor — gate it behind a TOTP or recovery proof, and record an audit row. |
| `/account/login/mfa` | POST | (U4) `LoginMfa(MfaChallengeViewModel)` | The **challenge leg** of sign-in (§5): accept a TOTP **or** a recovery code for the pending (temporarily signed-in) user, then complete the **persistent** sign-in via `SignInManager.TwoFactorAuthenticatorSignInAsync` / `TwoFactorRecoveryCodeSignInAsync`. |

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
    public bool Enabled { get; init; }                 // userManager.GetTwoFactorEnabledAsync(user) — the 10.0.13 flag
    // Shown ONCE, at enable/rotate — never re-served by a GET afterward.
    public string? SetupKey { get; init; }             // userManager.GenerateNewAuthenticatorKey() — base32 (enrollment leg only)
    public string? OtpAuthUrl { get; init; }           // otpauth:// URI built from SetupKey + TokenOptions.AuthenticatorIssuer (enrollment leg only)
    public string[]? RecoveryCodes { get; init; }      // 10 codes from GenerateNewTwoFactorRecoveryCodesAsync (enrollment/rotate leg only)
    public int RemainingRecoveryCodes { get; init; }   // userManager.CountRecoveryCodesAsync(user) — the only thing a steady GET shows
}
```
`MfaEnableViewModel` / `MfaDisableViewModel` / `MfaChallengeViewModel` each carry the
single `string Code` field (+ `ValidateAntiForgeryToken`). The **"shown once, then never
stored again"** rule = the values are returned **only** on the enabling/rotating response;
every GET reads `userManager.CountRecoveryCodesAsync(user)` **solely as the count** and never
emits the values.

---

## 5. Enforcement point — **pin: the `AccountController.Login` POST seam (the block-gate precedent), NOT `PrivilegedStampMiddleware`**

> **RE-PINNED against the actual `Microsoft.AspNetCore.Identity 10.0.13` sign-in surface.**
> The original §5 relied on the .NET ≤8 `UserManager` TOTP API + the `TwoFactorEnabled` property.
> **Round-2 correction:** the .NET 10.0.13 `UserManager` two-factor API **IS present** (see §2.1) —
> `ValidateTwoFactorTokenAsync` / `RedeemTwoFactorRecoveryCodeAsync` / `SetTwoFactorEnabledAsync` all
> exist. The `IdentityUser` *properties* `IsTwoFactorEnabled` / `RecoveryCodesLeft` /
> `ResetRecoveryCodes` are **ABSENT** (CS1061) — round-1's model was invented. The .NET 10 two-leg sign-in flow is instead:
> **leg 1** `PasswordSignInAsync(user, password, isPersistent:false, lockoutOnFailure:true)`
> (**① compiler-verified** — the current `AccountController.cs:716` calls it and the repo builds
> green) → **gate** on `result.RequiresTwoFactor` (**① round-2 reflection-verified** — the property
> on `SignInResult` is present in 10.0.13) → **leg 2** `TwoFactorAuthenticatorSignInAsync(user, code)` /
> `TwoFactorRecoveryCodeSignInAsync(user, code)` (**① round-2 reflection-verified** — both present
> on `SignInManager<T>` in 10.0.13). **Correction to the F3.U2 note's implication:** `PasswordSignInAsync` is
> **NOT absent** in 10.0.13 (the F3.U2 agent only tested `AddTokenProvider` +
> `TotpAuthenticatorTokenProvider`, both of which ARE absent; it did not test the sign-in leg).
> **Do NOT replace leg-1 with `CheckPasswordSignInAsync`** — that method is ③ (U2-build-verifies)
> and `PasswordSignInAsync` is the one that already compiles, so it is the safe leg-1.

**Pin: enforce MFA in the `AccountController.Login` POST (`src/Kumunita.Web/Controllers/
AccountController.cs`, the action at ~`:698-755`), immediately after the existing
`profile.Blocked` gate — i.e. at the exact seam the repo already uses for its one other
post-credential gate.** A `GlobalAdmin` (or any user with `TwoFactorEnabled == true`) cannot
complete sign-in without a valid TOTP or recovery code. **Other roles are unaffected** (their
`TwoFactorEnabled` flag is optional).

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
   `if (await signInManager.IsTwoFactorEnabledAsync(user))`, **① round-2 reflection-verified**.) On the
   gate:
   - **Return the MFA challenge** (the `/account/login` view with the code field, or a
     redirect to `/account/login/mfa`); do **not** yet redirect to `ReturnUrl`. The user is
     already in a **temporary** (non-persistent) sign-in from leg 1, so `User` is populated
     for the challenge view — **no separate pending-user store is required**.
   - On a correct code at the challenge leg, **Leg 2 — complete sign-in** (both methods are
     **① round-2 reflection-verified**):
     - **TOTP code:** `await signInManager.TwoFactorAuthenticatorSignInAsync(user,
       code, model.RememberMe);`
     - **Recovery code:** `await signInManager.TwoFactorRecoveryCodeSignInAsync(user,
       code, model.RememberMe);` (this method also enforces one-time-use via
       `UserManager.RedeemTwoFactorRecoveryCodeAsync` under the hood, §6).
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
   (the enrollment POST flips `SetTwoFactorEnabledAsync(user, true)` and issues recovery codes
   via `GenerateNewTwoFactorRecoveryCodesAsync`, §4/§6).
4. **Leg 1.5c — the existing path for non-MFA users is UNCHANGED from today:** the
   `profile.Blocked` check (kept verbatim), then the persistent sign-in
   (`await signInManager.PasswordSignInAsync(user, model.Password, isPersistent: true,
   lockoutOnFailure: true)` — **① compiler-verified**, the current code's exact shape) and the
   persistent redirect. This is the **`NonAdmin_MfaOptional`** guarantee — a non-`GlobalAdmin`
   with `TwoFactorEnabled == false` signs in **exactly as today** (no change at all for this
   branch).

**Why this seam (pin the rationale, it is the deciding argument):**
- The repo's own precedent for "a gate that must be able to *fail* sign-in" is the
  **block enforcement at the login seam** (`AccountController.cs:~726`): *"Kept here (not in
  the factory) because the factory cannot fail sign-in; the controller is the one place that
  can inspect the account after credentials are verified."* MFA is exactly such a gate — it
  must be able to **withhold** the auth cookie — so it belongs here for the same reason.
- The controller **already injects** `SignInManager<User>` **and** `UserManager<User>`
  (`AccountController.cs:31-35`) — **zero new DI**, zero new service.
- It is **per-role-agnostic** to write (the check is `signInManager.IsTwoFactorEnabledAsync(user)` +
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
- **Count: 10** — the 10 codes are minted by `UserManager.GenerateNewTwoFactorRecoveryCodesAsync(user)`
  (① round-2 reflection-verified) and persisted as rows in the existing `AspNetUserTokens` table
  (via `IUserTwoFactorRecoveryCodeStore<T>.ReplaceCodesAsync`). **No new column, no new migration.**
  The count a steady GET shows is `UserManager.CountRecoveryCodesAsync(user)` (①). Pin 10; do not
  invent a different number. (If U2's build shows a different count, record it in the handoff note
  and keep the test asserting the count via `CountRecoveryCodesAsync`, not hardcoding 10.)
- **Issued at:** (a) the **enable** POST (§4 `MfaEnable`) — first 10 codes, returned once;
  (b) the **rotate** POST (§4 `MfaRotateRecovery`) — replaces the current set, returned once.

### 6.2 One-time use
- A recovery code presented at the challenge leg is consumed by the **sign-in leg**
  `signInManager.TwoFactorRecoveryCodeSignInAsync(user, code, rememberMe)` (① round-2
  reflection-verified), which internally calls `UserManager.RedeemTwoFactorRecoveryCodeAsync`
  (①) to consume the code from `AspNetUserTokens`. A second presentation of the same code is
  rejected (the code row is gone). This is the `GlobalAdminRecoveryCode_WorksOnce` guarantee.
  The remaining count is `UserManager.CountRecoveryCodesAsync(user)` (①). **U2 must verify at
  build** that `TwoFactorRecoveryCodeSignInAsync` performs the consume (if the 10.0.13 surface
  requires an explicit `RedeemTwoFactorRecoveryCodeAsync` call in the controller, pin that in
  the handoff note — do not guess).

### 6.3 Rotate
- `UserManager.GenerateNewTwoFactorRecoveryCodesAsync(user)` (① round-2 reflection-verified)
  **atomically swaps** the whole set (via `IUserTwoFactorRecoveryCodeStore<T>.ReplaceCodesAsync`).
  The new 10 are shown **once**; the old 10 are dead. The steady-state GET (§4 `MfaRecovery()`)
  shows only the **count** (`UserManager.CountRecoveryCodesAsync(user)`, ①).

### 6.4 The "all recovery codes used" edge case (pin the escape — no backdoor)
- If **all 10 recovery codes are consumed** *and* the TOTP authenticator is lost, the
  `GlobalAdmin` **cannot sign in** (both second factors are gone). **Do NOT add a
  backdoor** (no "skip MFA" flag, no admin override that bypasses the second factor).
- **The escape is the existing operator lane** (reuse, don't reinvent):
  - **Break-glass:** the operator invokes the existing **break-glass** lane
    (`IdentityService.ConsumeBreakGlassAsync`, `IdentityService.cs:~1319`) to re-enable
    access, then a GlobalAdmin peer (or the operator) resets MFA via
    `userManager.SetTwoFactorEnabledAsync(user, false)` (① round-2 reflection-verified) + clearing
    the secret (`userManager.ResetAuthenticatorKeyAsync(user)`, ①) (the §4 `MfaDisable` action),
    and re-enrolls.
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
   `TwoFactorEnabled == true` (seeded via `userManager.SetTwoFactorEnabledAsync(user, true)`, ①)
   posts a correct password to `Login`. Assert the response is the
   **challenge** (not a redirect to `ReturnUrl`), i.e. sign-in is **not** complete, and a
   subsequent correct TOTP code (`TwoFactorAuthenticatorSignInAsync`) completes the **persistent**
   sign-in. Proves §5 Leg 1.5.
2. **`GlobalAdminRecoveryCode_WorksOnce`** — a `GlobalAdmin` at the challenge leg presents
   a **recovery code**. Assert (a) the first presentation **succeeds** via
   `TwoFactorRecoveryCodeSignInAsync` and completes sign-in; (b) the **same** code presented
   again is **rejected** (consumed via `UserManager.RedeemTwoFactorRecoveryCodeAsync` under the
   hood, §6.2; count drops by 1 via `CountRecoveryCodesAsync`). Proves §6.
3. **`NonAdmin_MfaOptional`** — a non-`GlobalAdmin` (e.g. `Member`) with
   `TwoFactorEnabled == false` (the default; no seeding needed) posts a correct password. Assert sign-in completes
   **directly** (persistent redirect, no challenge) — MFA is **optional** for them. Proves
   §5 Leg 1.5c.

**Driving note for U5 (round-2):** the enforcement logic lives in `AccountController` (Web) and
the sign-in leg is on `SignInManager` (10.0.13 two-factor surface, §2.1). The tests drive the
**public controller actions** (`Login`, `LoginMfa`) — do **not** make the §5 private helpers
public to test them. A `GlobalAdmin` role is seeded via `userManager.AddToRoleAsync` (the same
setup the F1 `IdentityServiceDeleteLaneGdprTests` harness uses for a GlobalAdmin actor).
**Seeding the two-factor state (round-2, real API):** enable via
`userManager.SetTwoFactorEnabledAsync(user, true)` (①) + mint the TOTP key via
`userManager.GenerateNewAuthenticatorKey()` (①, stored as `AspNetUserTokens` row) + generate the
10 recovery codes via `userManager.GenerateNewTwoFactorRecoveryCodesAsync(user)` (①, stored as
`AspNetUserTokens` rows). **Do NOT use the round-1 `user.IsTwoFactorEnabled` /
`user.ResetRecoveryCodes` / `user.RecoveryCodesLeft` properties — they do not exist on
`IdentityUser` in 10.0.13 (CS1061).**

---

## 8. Drift / scope notes (record for the owner + U2–U5 — do not silently resolve)

> **Re-pinned against the REAL `Microsoft.AspNetCore.Identity 10.0.13` — round 2.** Items 2, 3, 4,
> 7, 8 below were rewritten by round-2 (they carry the round-1 errors); items 1, 5, 6 are preserved
> (still correct). **Round-2 verification is by direct reflection of the 10.0.13 PE files, not by
> `strings` and not by the F3.U2 note** (both of which contained false negatives — see item 7).

1. **The plan's named hook, `PrivilegedStampMiddleware`, is the wrong layer for MFA.** It is
   a per-request **role-revocation** middleware (`Program.cs:773`,
   `Security/PrivilegedStampMiddleware.cs:43`) that only knows how to `SignOutAsync` +
   `Redirect`; it cannot present a challenge or complete a two-leg sign-in. The grounded
   enforcement seam is **`AccountController.Login` POST** (the block-gate precedent, §5).
   **Recorded, not resolved against the plan** — the plan's F3 U4 said "a middleware after
   `PrivilegedStampMiddleware`"; the pin supersedes that with the controller seam.
2. **The token provider registration — `.AddDefaultTokenProviders()` — is ALREADY PRESENT
   (round-2 correction).** It is on the `AddIdentity<User, IdentityRole>(…)` chain at
   `src/Kumunita.Web/Program.cs:403` (F3.U2 landed it, build green — **do not re-add it**).
   **`TotpAuthenticatorTokenProvider` is genuinely ABSENT** in 10.0.13 (round-2 reflection: no
   such type) — the TOTP provider is **`AuthenticatorTokenProvider<T>`** (RFC 6238, verified ①
   present), registered by `AddDefaultTokenProviders()`. Without a provider registration the
   first two-factor call throws (`Resources.NoTokenProvider`, verified ① present) — but that
   registration **already exists**, so this is no longer an open trap for U2. **U2 must not
   re-add it, and must not assume a custom `IUserTwoFactorTokenProvider` is required** — the
   stock `AuthenticatorTokenProvider<T>` already provides genuine otpauth:// TOTP.
3. **The enable flag is `IdentityUser.TwoFactorEnabled` (the REAL property), on the existing
   `AspNetUsers.TwoFactorEnabled` column — NO new column, NO new migration (round-2 correction).**
   The round-1 claim that the property is `IsTwoFactorEnabled` (and that `TwoFactorEnabled` is
   absent) is **backwards** — round-2 reflection confirms `IdentityUser` has `TwoFactorEnabled`
   (bool) and **not** `IsTwoFactorEnabled` (F3.U2 CS1061). The column is unchanged
   (`InitialIdentity.cs:50`). `IdentityUser` has **no** `RecoveryCodesLeft` / `ResetRecoveryCodes`
   members — those live only on the `Data.TwoFactorResponse` DTO. **No EF migration is required
   for the flag.**
4. **Recovery codes are the `UserManager` recovery-code API, persisted in `AspNetUserTokens`
   (round-2 correction).** The round-1 model ("recovery codes are `IdentityUser` properties
   `RecoveryCodesLeft` / `ResetRecoveryCodes`") is **build-disproven** (F3.U2 CS1061 ×3) and
   **invented**. The REAL 10.0.13 API (verified ①): **`GenerateNewTwoFactorRecoveryCodesAsync`**
   (issue 10, shown once), **`RedeemTwoFactorRecoveryCodeAsync`** (one-time consume),
   **`CountRecoveryCodesAsync`** (steady-GET count) — backed by
   **`IUserTwoFactorRecoveryCodeStore<T>`** (`ReplaceCodesAsync` / `RedeemCodeAsync` /
   `CountCodesAsync`, verified ①) → the token store → the existing **`AspNetUserTokens`** table.
   One-time use is enforced by the store (and surfaced at sign-in via
   `SignInManager.TwoFactorRecoveryCodeSignInAsync`, ①). U2's "recovery-code store" is therefore
   **the `UserManager` recovery-code API over the existing `AspNetUserTokens` table** — not three
   invented `IdentityUser` properties. **No new column, no new migration.**
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
7. **Identity-API version — RESOLVED by round-2 via direct reflection of the 10.0.13 PE files.**
   **Correcting the record (the round-1 and F3.U2 "absent" lists were partly wrong):**
   - **GENUINELY ABSENT in 10.0.13** (round-2 reflection): `TotpAuthenticatorTokenProvider`
     (the TOTP provider is `AuthenticatorTokenProvider<T>` + `Rfc6238AuthenticationService`),
     `IdentityUser.IsTwoFactorEnabled` (the property is `TwoFactorEnabled`; F3.U2 CS1061),
     `IdentityUser.RecoveryCodesLeft` / `IdentityUser.ResetRecoveryCodes` (F3.U2 CS1061; `IdentityUser`
     has no recovery-code members), and `CanGenerateTwoFactorTokenAsync` **on `SignInManager`**
     (it is a **provider** method — on `IUserTwoFactorTokenProvider<T>` /
     `AuthenticatorTokenProvider<T>` / `DataProtectorTokenProvider<T>`).
   - **PRESENT** (round-2 reflection, ①) despite round-1/F3.U2 "absent" claims: the full
     `UserManager` two-factor set (`Get/SetTwoFactorEnabledAsync`, `GetValidTwoFactorProvidersAsync`,
     `Generate/ValidateTwoFactorTokenAsync`, `Get/SetAuthenticationTokenAsync`,
     `GetAuthenticatorKeyAsync` / `ResetAuthenticatorKeyAsync` / `GenerateNewAuthenticatorKey`,
     `GenerateNewTwoFactorRecoveryCodesAsync` / `RedeemTwoFactorRecoveryCodeAsync` /
     `CountRecoveryCodesAsync`), the `IUserTwoFactorRecoveryCodeStore<T>` / `IUserTwoFactorStore<T>` /
     `IUserAuthenticatorKeyStore<T>` interfaces, `AuthenticatorTokenProvider<T>`,
     `DataProtectorTokenProvider<T>`, `IUserTwoFactorTokenProvider<T>`,
     `TwoFactorAuthenticationInfo`, `SignInResult.RequiresTwoFactor`, and all the `SignInManager`
     leg-1/leg-2 methods. **F3.U2's note had a documented FALSE NEGATIVE** (its bulk scan wrongly
     reported the build-proven-present `AddDefaultTokenProviders` as absent) — so **U2 must not
     delete any code based on the round-1/F3.U2 "absent" lists**; round-2's §2.1 table is the
     authoritative present/absent basis.
   - **U2's build-verify is now narrow:** confirm at build that `UserManager`'s two-factor API
     resolves for the repo's `User` (it should — the repo already `AddIdentity`s against `User`)
     and that `ValidateTwoFactorTokenAsync` accepts a `GenerateTwoFactorTokenAsync` output. No
     "confirm the ③ names" step is needed — §2.1 is reflection-verified.
8. **Old→new API mapping (for the owner's diff — the re-derivation):**

   | Concern | **round-1 pin (WRONG)** | **10.0.13 (round-2, real — ① reflection-verified)** |
|---|---|---|
| Enable flag | `user.IsTwoFactorEnabled` (**absent**) | `user.TwoFactorEnabled` (property, real) |
| Enable (write) | `user.IsTwoFactorEnabled = true` + `SaveChangesAsync` | `userManager.SetTwoFactorEnabledAsync(user, true)` (①) |
| Disable (write) | `user.IsTwoFactorEnabled = false` + `SaveChangesAsync` | `userManager.SetTwoFactorEnabledAsync(user, false)` (①) |
| Enable-check | `signInManager.IsTwoFactorEnabledAsync(user)` | **same** — `IsTwoFactorEnabledAsync` IS on `SignInManager` (①); or `userManager.GetTwoFactorEnabledAsync(user)` (①) |
| TOTP — generate | `signInManager.CanGenerateTwoFactorTokenAsync(user)` (**wrong home**) | `userManager.GenerateTwoFactorTokenAsync(user, "Authenticator")` (①) or the provider's `GenerateAsync` (①) |
| TOTP — validate | `signInManager.TwoFactorAuthenticatorSignInAsync(user, code)` | **enrollment:** `userManager.ValidateTwoFactorTokenAsync(user, "Authenticator", code)` (①); **sign-in:** `signInManager.TwoFactorAuthenticatorSignInAsync(user, code)` (①) |
| TOTP secret | (invented) | `userManager.GenerateNewAuthenticatorKey()` (①) + `otpauth://` from `TokenOptions.AuthenticatorIssuer` (①); stored in `AspNetUserTokens` |
| **Sign-in leg 1** | (n/a) | `signInManager.PasswordSignInAsync(user, pw, isPersistent:false, lockout)` — ①' compiler-verified; only `isPersistent` flips `true`→`false` |
| **Two-factor gate** | `signInManager.IsTwoFactorEnabledAsync(user)` | **same — `IsTwoFactorEnabledAsync` IS present** (①) OR `result.RequiresTwoFactor` (①) |
| **Sign-in leg 2** | (n/a) | `signInManager.TwoFactorAuthenticatorSignInAsync(user, code)` / `TwoFactorRecoveryCodeSignInAsync(user, code)` (both ①) |
| Recovery — read | `user.RecoveryCodesLeft` (**absent**) | `userManager.CountRecoveryCodesAsync(user)` (①) |
| Recovery — issue/rotate | `user.ResetRecoveryCodes = <set>` (**absent**) | `userManager.GenerateNewTwoFactorRecoveryCodesAsync(user)` (①) → `AspNetUserTokens` |
| Recovery — consume | `signInManager.TwoFactorRecoveryCodeSignInAsync(user, code)` | **same** (①) + `userManager.RedeemTwoFactorRecoveryCodeAsync(user, code)` (①) under the hood |
| Provider type | `IUserTwoFactorTokenProvider<T>` (②) | **same** (①) + `AuthenticatorTokenProvider<T>` TOTP (①) + `DataProtectorTokenProvider<T>` (①) |
| Registration | `.AddDefaultTokenProviders()` (②) | **same** — already at `Program.cs:403` (①') — do not re-add |
| Secret + codes storage | (invented `IdentityUser` properties) | **`AspNetUserTokens` table** (already in schema) via the token store (①); **NO new migration** |

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
- **(b) RESOLVED (round-2):** the recovery-code EF-mapping question is **moot** — the round-1
  model (`IdentityUser.RecoveryCodesLeft` / `ResetRecoveryCodes`) was wrong. The real mechanism
  persists the TOTP key + recovery codes in the **existing `AspNetUserTokens`** table (via the
  token store; `AspNetUserTokens` is already in the schema, `InitialIdentity.cs:156`). **NO new
  EF migration is required.**
- **(c) RESOLVED (round-2):** the genuine authenticator-app (otpauth:// QR) TOTP experience is
  provided by the stock **`AuthenticatorTokenProvider<T>`** (RFC 6238, ①) registered by
  `AddDefaultTokenProviders()` (already at `Program.cs:403`). **No custom `IUserTwoFactorTokenProvider`
  is needed.**
