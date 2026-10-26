using Kumunita.Core.Identity;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// F3 · U2 — build-verification (round-3). Compile-check of the round-3 re-pin's
/// §2.1 two-factor surface against the real <c>Microsoft.AspNetCore.Identity 10.0.13</c>
/// <c>UserManager&lt;User&gt;</c> / <c>SignInManager&lt;User&gt;</c> of the repo.
///
/// The private method <see cref="ReferenceRound3Surface"/> references **every**
/// §2.1 name the pin (docs/design/prod-hardening-f3-mfa.md §2.1/§3) pins — the
/// **four round-3-corrected shapes** (each FAILED in the round-2 build) plus the
/// **round-2-confirmed regression set**. The body is **compile-checked even though
/// it is never invoked**: a green <c>dotnet build Kumunita.slnx -c Debug</c> is the
/// proof that the names resolve (the repo compiler is the authority — the pin's
/// convention). If a round-3 shape still fails to resolve, the build goes red and
/// that is a drift pause back to F3.U1 (not a guess — the name is not substituted).
///
/// Kept as a valid API-surface guard: no stray files, no <c>&lt;PackageReference&gt;</c>,
/// no <c>.slnx</c> change, no <c>.cs</c> feature change, <c>Program.cs:403</c> untouched.
/// F3.U3 (enroll/verify surface) is NOT started by this unit.
/// </summary>
public class F3U2_TokenStoreApiCompileCheckTests
{
    /// <summary>Pinned-name surface guard (F3.U2 build-verification, round-3).</summary>
    [Fact]
    public void F3_U2_Round3_TokenStoreApi_Resolves()
    {
        // The compile-check lives in ReferenceRound3Surface (intentionally never
        // invoked): a green build of THIS file proves every §2.1 name resolves
        // against the repo's UserManager&lt;User&gt; / SignInManager&lt;User&gt; on
        // Microsoft.AspNetCore.Identity 10.0.13.
        Assert.True(true);
    }

    /// <summary>
    /// The round-3 compile-check body. Every member is referenced against the real
    /// generic managers so the compiler resolves each one. The four
    /// round-3-corrected shapes are marked <c>// (R3)</c>; the rest are the
    /// round-2-confirmed regression set kept in the same check as a guard.
    /// </summary>
    private static void ReferenceRound3Surface(UserManager<User> um, SignInManager<User> sm)
    {
        var u = new User();

        // ---- The FOUR round-3-corrected shapes (each FAILED in the round-2 build) ----
        // (R3) #1 — TOTP validate. Round-2's `ValidateTwoFactorTokenAsync` was
        //          CS1061 / absent; the real 10.0.13 member is this one.
        _ = um.VerifyTwoFactorTokenAsync(u, "Authenticator", "123456");

        // (R3) #2 — recovery codes (re)issue. Round-2's 1-arg call was CS7036;
        //          the real shape is (user, number).
        _ = um.GenerateNewTwoFactorRecoveryCodesAsync(u, 10);

        // (R3) #3 — leg-2 TOTP sign-in. Round-2's (user, code) was wrong-shaped;
        //          the real shape is code-first, NO user (user comes from the
        //          pending temp sign-in).
        _ = sm.TwoFactorAuthenticatorSignInAsync("123456", isPersistent: true, rememberClient: false);

        // (R3) #4 — leg-2 recovery sign-in. Round-2's (user, code) was wrong-shaped;
        //          the real shape is 1-arg, NO user.
        _ = sm.TwoFactorRecoveryCodeSignInAsync("abcdef0123");

        // ---- Round-2-confirmed regression set (keep in the same check as a guard) ----
        _ = um.GetTwoFactorEnabledAsync(u);
        _ = um.SetTwoFactorEnabledAsync(u, true);
        _ = um.GenerateNewAuthenticatorKey();
        _ = um.GenerateTwoFactorTokenAsync(u, "Authenticator");
        _ = um.RedeemTwoFactorRecoveryCodeAsync(u, "abcdef0123");
        _ = um.CountRecoveryCodesAsync(u);
        _ = um.GetAuthenticatorKeyAsync(u);
        _ = um.ResetAuthenticatorKeyAsync(u);
        _ = um.GetValidTwoFactorProvidersAsync(u);
        _ = sm.PasswordSignInAsync(u, "hunter22", isPersistent: true, lockoutOnFailure: true);
        // (R3-regression) IsTwoFactorEnabledAsync — the repo compiler (the authority)
        // proved the real 10.0.13 shape is IsTwoFactorEnabledAsync(User): my round-1
        // scratch form called it with 0 args (CS7036). Supply the compiler-named
        // required 'user' arg; the NAME is unchanged.
        _ = sm.IsTwoFactorEnabledAsync(u);
        // (R3-regression) SignInResult.RequiresTwoFactor (tier ①) — compile-checked via
        // nameof (no construction: SignInResult has NO public ctor in 10.0.13 and
        // SignInStatus is internal — my round-1 scratch form fabricated that ctor,
        // CS1729/CS0103). nameof resolves the member at compile time: red if absent.
        _ = nameof(SignInResult.RequiresTwoFactor);
    }
}
