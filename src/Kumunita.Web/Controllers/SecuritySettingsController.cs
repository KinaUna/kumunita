using Kumunita.Core.Identity;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The per-resident <b>MFA enrollment</b> surface (F3, AUTH-03 — the MFA for
/// <c>GlobalAdmin</c> lane; the enforcement/challenge leg is F3.U4, the tests
/// are F3.U5). Lives at <c>/settings/security</c> — the same <c>[Authorize]</c>
/// settings-tab shape as <see cref="MessagingSettingsController"/> at
/// <c>/settings/messaging</c> (the <see cref="LocaleController"/> /
/// <see cref="KioskController"/> idiom, ADR 0080).
/// <para>
/// This is the <b>enrollment surface only</b> (F3.U3): the enable/disable
/// flag flip, the one-time authenticator setup key + <c>otpauth://</c> URI,
/// and the one-time 10 recovery codes, all wired to the repo's
/// <c>UserManager&lt;User&gt;</c> two-factor members. It does <b>not</b> touch
/// the <c>AccountController.Login</c> sign-in challenge leg (that is F3.U4).
/// </para>
/// <para>
/// The two-factor API used here is the <b>build-verified round-3 surface</b>
/// (F3.U2 — <c>tests/Kumunita.Web.Tests/F3U2_TokenStoreApiCompileCheckTests.cs</c>
/// proved these names compile against <c>Microsoft.AspNetCore.Identity 10.0.13</c>).
/// Every <c>UserManager</c> member below is referenced verbatim from that
/// green compile-check — no re-guessed names (the repo compiler is the
/// authority, per the pin's convention). <c>Program.cs:403</c> already has
/// <c>.AddDefaultTokenProviders()</c>; it is <b>not</b> re-added here.
/// </para>
/// <para>
/// <b>Recovery-codes-shown-once rule:</b> the 10 codes from
/// <c>UserManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10)</c> are
/// returned in the model <b>only</b> on the enabling/rotating response; every
/// steady GET reads <c>UserManager.CountRecoveryCodesAsync(user)</c> <b>solely
/// as the count</b> and never emits the values.
/// </para>
/// <para>
/// <b>Route note (F3.U3 resolution of a pin §4 table collision):</b> the pin's
/// §4 table lists <b>both</b> <c>MfaEnable</c> and <c>MfaDisable</c> as
/// <c>POST /settings/security/mfa</c> — impossible under attribute routing
/// (two actions, same verb + path). The primary enable action keeps the
/// pin's exact URL (<c>POST /settings/security/mfa</c>); the <b>optional</b>
/// disable leg is relocated to <c>POST /settings/security/mfa/disable</c>.
/// The three plan-pinned URLs (<c>/settings/security/mfa</c>,
/// <c>/settings/security/mfa/recovery</c> GET) are unchanged.
/// </para>
/// <para>
/// <b>Ordering note (F3.U3 refinement of pin §4):</b> the pin prescribes
/// "verify the TOTP code, <i>then</i> flip the flag". In 10.0.13,
/// <c>UserManager.VerifyTwoFactorTokenAsync</c> is gated on the flag already
/// being enabled (it routes through
/// <c>CanGenerateTwoFactorTokenAsync → IsTwoFactorEnabledAsync</c>, which
/// returns <c>false</c> when the flag is off, so the token is never
/// validated). At enrollment the flag is still <c>false</c>, so that order
/// would be runtime-broken. The enable action therefore orders the
/// <b>same build-verified members</b> as <c>SetTwoFactorEnabledAsync(user,
/// true)</c> → <c>VerifyTwoFactorTokenAsync(user, "Authenticator", code)</c>
/// → <c>SetTwoFactorEnabledAsync(user, false)</c> <b>(rollback)</b> on a
/// failed verify. No member name is substituted; only the order is the one
/// the 10.0.13 runtime honors.
/// </para>
/// </summary>
[Authorize]
public sealed class SecuritySettingsController(
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    IIdentityService identity,
    IOptions<TokenOptions> tokenOptions) : Controller
{
    private static readonly string AuthenticatorProvider = "Authenticator";
    private const int RecoveryCodeCount = 10;

    // ---- helpers ---------------------------------------------------------

    /// <summary>
    /// Resolve the signed-in resident's Identity row (the repo idiom —
    /// <c>AccountController</c> uses <c>userManager.FindByIdAsync(subject)</c>
    /// where <c>subject</c> is the <c>Kumunita.Sub</c> claim, which is the
    /// <c>User.Id</c>). <c>null</c> is impossible for a <c>[Authorize]</c>
    /// action but is handled fail-closed anyway.
    /// </summary>
    private async Task<User?> ResolveUserAsync()
    {
        var subject = KumunitaPrincipal.SubjectId(User);
        if (string.IsNullOrEmpty(subject))
        {
            return null;
        }

        return await userManager.FindByIdAsync(subject);
    }

    /// <summary>
    /// Build the <c>otpauth://</c> URI from the one-time setup key + the
    /// configured authenticator issuer (the pin §4 enrollment leg — the app
    /// builds the URI; the key comes from
    /// <c>UserManager.GenerateNewAuthenticatorKey()</c>).
    /// <para>
    /// <b>Shape note (F3.U3 compiler-verified):</b> in 10.0.13,
    /// <c>TokenOptions.AuthenticatorTokenProvider</c> is a <c>string</c>
    /// (the provider name, <c>"Authenticator"</c>), not a nested object.
    /// The issuer is <c>TokenOptions.AuthenticatorIssuer</c> (a direct
    /// <c>string</c> property). The compiler confirmed this (CS1061 on
    /// <c>.AuthenticatorTokenProvider.AuthenticatorIssuer</c>);
    /// <c>AuthenticatorIssuer</c> on <c>TokenOptions</c> is the correct
    /// path.
    /// </para>
    /// </summary>
    private string BuildOtpAuthUrl(User user, string key)
    {
        var issuer = string.IsNullOrWhiteSpace(tokenOptions.Value.AuthenticatorIssuer)
            ? "Kumunita"
            : tokenOptions.Value.AuthenticatorIssuer;
        var account = user.Email ?? user.UserName ?? "account";

        return "otpauth://totp/"
            + Uri.EscapeDataString(issuer) + ":" + Uri.EscapeDataString(account)
            + "?secret=" + key
            + "&issuer=" + Uri.EscapeDataString(issuer)
            + "&algorithm=SHA1&digits=6&period=30";
    }

    /// <summary>
    /// Encode the <c>otpauth://</c> URI as a QR <c>data:image/png</c> URI
    /// (ADR 0174). A server-side QR encode (<see cref="QRCoder.PngByteQRCode"/>)
    /// means the enrollment page can render a scannable image without any
    /// client JavaScript — the authenticator-app pairing leg works with
    /// scripts disabled, and there is no new asset to ship. The PNG is
    /// embedded as a data URI so the image carries no cache/identity
    /// metadata and the page stays a single round-trip.
    /// <para>
    /// <b>Fallback note:</b> the <c>data:</c> URI is only useful when the
    /// <c>otpauth://</c> URI is present (the enrollment leg). On a steady
    /// state / rotate (no setup key) the view does not render the QR at all,
    /// so a <c>null</c> URI simply produces no image. A QR encoder failure is
    /// not expected for the short, fixed-shape <c>otpauth://</c> payload — if
    /// it ever throws, we return <c>null</c> and the view degrades to the
    /// setup-key + URI text (both already rendered), which is the resident's
    /// other pairing path.
    /// </para>
    /// </summary>
    private static string? BuildQrDataUri(string otpAuthUrl)
    {
        try
        {
            // QRCoder 1.8.1 shape (build-verified against the 1.8.1 assembly):
            // CreateQrCode(string, ECCLevel, bool, bool, EciMode, int) — the
            // single-string overload was dropped in 1.8.x; the 6-arg form is
            // the raw-payload entry point. ECCLevel.M (the QR standard's
            // default, ~15% error tolerance) is the right balance for a QR
            // scanned by a phone camera; EciMode.Default + the default
            // 40-version cap cover the short otpauth:// payload comfortably.
            // The last int (version cap 40) is the QR max; the payload is
            // far below it, so the encoder picks the smallest version that
            // fits. pixelsPerModule = 5 gives a crisp, scannable image
            // (large enough for a phone held at arm's length, small enough
            // to stay a light data URI).
            var qr = new QRCoder.QRCodeGenerator().CreateQrCode(
                otpAuthUrl,
                QRCoder.QRCodeGenerator.ECCLevel.M,
                false,
                false,
                QRCoder.QRCodeGenerator.EciMode.Default,
                40);
            var png = new QRCoder.PngByteQRCode(qr).GetGraphic(5);
            return "data:image/png;base64," + Convert.ToBase64String(png);
        }
        catch (Exception)
        {
            return null; // degrade to the setup-key + URI text leg
        }
    }

    // ---- actions ---------------------------------------------------------

    /// <summary>
    /// <c>GET /settings/security/mfa</c> — enrollment state.
    /// <list type="bullet">
    /// <item><b>Not enabled:</b> mint the one-time setup key
    /// (<c>GenerateNewAuthenticatorKey()</c>, which persists the key as a
    /// side effect so a later verify has a key to check against) + the
    /// <c>otpauth://</c> URI + the 10 recovery codes
    /// (<c>GenerateNewTwoFactorRecoveryCodesAsync(user, 10)</c>, shown
    /// <b>once</b>).</item>
    /// <item><b>Already enabled:</b> status + <b>count only</b>
    /// (<c>CountRecoveryCodesAsync</c>, never the values).</item>
    /// </list>
    /// </summary>
    [HttpGet("/settings/security/mfa")]
    public async Task<IActionResult> Mfa()
    {
        var user = await ResolveUserAsync();
        if (user is null)
        {
            return View(new MfaSettingsViewModel());
        }

        var enabled = await userManager.GetTwoFactorEnabledAsync(user);

        if (enabled)
        {
            // Steady state — count only, never the values.
            var remaining = await userManager.CountRecoveryCodesAsync(user);
            return View(new MfaSettingsViewModel
            {
                Enabled = true,
                RemainingRecoveryCodes = remaining,
            });
        }

        // Enrollment leg — the one-time secrets (shown once on this response).
        // GenerateNewAuthenticatorKey() persists the key as a side effect
        // (standard Identity behavior: it calls IUserAuthenticatorKeyStore.
        // SetAuthenticatorKeyAsync internally), so the enable POST's
        // VerifyTwoFactorTokenAsync has a key to validate against. The
        // public UserManager surface does NOT expose SetAuthenticatorKeyAsync
        // (it is on the store interface, not promoted to UserManager in
        // 10.0.13 — CS1061 confirmed by the repo compiler); the pin's
        // §2.1 build-verified surface lists only
        // GetAuthenticatorKeyAsync / ResetAuthenticatorKeyAsync / 
        // GenerateNewAuthenticatorKey on UserManager.
        var key = userManager.GenerateNewAuthenticatorKey();
        // Shape note (F3.U3 compiler-verified): returns IEnumerable<string>?,
        // not string[] (CS0266) — .ToArray() materializes it; the null-forgiving
        // operator handles the nullable return (CS8604).
        var codes = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount))!.ToArray();

        // ADR 0174 — the pairing QR: encode the otpauth URI once here (the
        // enrollment leg is the only leg that carries a setup key), and serve
        // the resulting data: URI to the view alongside the URI + setup key.
        var otpAuthUrl = BuildOtpAuthUrl(user, key);

        return View(new MfaSettingsViewModel
        {
            Enabled = false,
            SetupKey = key,
            OtpAuthUrl = otpAuthUrl,
            QrDataUri = BuildQrDataUri(otpAuthUrl),
            RecoveryCodes = codes,
            RemainingRecoveryCodes = codes.Length,
        });
    }

    /// <summary>
    /// <c>POST /settings/security/mfa</c> — enable. Verify the TOTP code the
    /// resident typed, <b>then</b> flip the flag on. The 10 recovery codes
    /// were already minted by the GET and shown <b>once</b>; this POST does
    /// not re-serve them.
    /// <para>
    /// Ordering (see the type-level note): <c>SetTwoFactorEnabledAsync(true)</c>
    /// → <c>VerifyTwoFactorTokenAsync</c> → rollback to <c>false</c> on a
    /// failed verify. A wrong code leaves the flag <c>false</c> (no partial
    /// state) and the resident returns to the enrollment page.
    /// </para>
    /// </summary>
    [HttpPost("/settings/security/mfa")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MfaEnable(MfaEnableViewModel model)
    {
        var user = await ResolveUserAsync();
        if (user is null)
        {
            return RedirectToAction(nameof(Mfa));
        }

        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Mfa));
        }

        // Enable first so 10.0.13's VerifyTwoFactorTokenAsync is un-gated, then
        // verify, then roll back on a failed verify (see the type-level note).
        // Shape note (F3.U3 compiler-verified): VerifyTwoFactorTokenAsync
        // returns bool (not IdentityResult) in 10.0.13 — the round-2 pin
        // assumed .Succeeded on an IdentityResult (CS1061 on 'bool'); the
        // real shape is a bare bool (true = valid, false = invalid).
        await userManager.SetTwoFactorEnabledAsync(user, true);
        var verified = await userManager.VerifyTwoFactorTokenAsync(user, AuthenticatorProvider, model.Code);
        if (!verified)
        {
            await userManager.SetTwoFactorEnabledAsync(user, false); // rollback
            TempData["error"] = "The authenticator code was not valid. Enable two-factor authentication again.";
            return RedirectToAction(nameof(Mfa));
        }

        TempData["info"] = "Two-factor authentication is now enabled. Sign in with your authenticator app from now on.";
        // The recovery codes were shown once at enrollment; redirect to the
        // count-only recovery page (never re-served here).
        return RedirectToAction(nameof(MfaRecovery));
    }

    /// <summary>
    /// <c>GET /settings/security/mfa/recovery</c> — show <b>count only</b>
    /// (<c>CountRecoveryCodesAsync</c>, never the values — they were shown
    /// once at enable/rotate).
    /// </summary>
    [HttpGet("/settings/security/mfa/recovery")]
    public async Task<IActionResult> MfaRecovery()
    {
        var user = await ResolveUserAsync();
        if (user is null)
        {
            return View(new MfaSettingsViewModel());
        }

        var enabled = await userManager.GetTwoFactorEnabledAsync(user);
        var remaining = await userManager.CountRecoveryCodesAsync(user);
        // Render the single Mfa view (one .cshtml for the whole enrollment
        // surface — the ≤3-file unit budget).
        return View("Mfa", new MfaSettingsViewModel
        {
            Enabled = enabled,
            RemainingRecoveryCodes = remaining,
        });
    }

    /// <summary>
    /// <c>POST /settings/security/mfa/recovery</c> — rotate: re-mint the 10
    /// codes, shown <b>once</b>; the old 10 are dead (replaced in the token
    /// store). Only reachable when MFA is enabled.
    /// </summary>
    [HttpPost("/settings/security/mfa/recovery")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MfaRotateRecovery()
    {
        var user = await ResolveUserAsync();
        if (user is null)
        {
            return RedirectToAction(nameof(Mfa));
        }

        var enabled = await userManager.GetTwoFactorEnabledAsync(user);
        if (!enabled)
        {
            // Nothing to rotate — back to the enrollment page.
            return RedirectToAction(nameof(Mfa));
        }

        // Shape note: returns IEnumerable<string>? — .ToArray() + null-forgiving.
        var codes = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount))!.ToArray();
        TempData["info"] = "Your recovery codes have been rotated. Copy them now — they will not be shown again.";
        // Render the single Mfa view (the rotated codes shown once).
        return View("Mfa", new MfaSettingsViewModel
        {
            Enabled = true,
            RecoveryCodes = codes,
            RemainingRecoveryCodes = codes.Length,
        });
    }

    /// <summary>
    /// <c>POST /settings/security/mfa/disable</c> — (optional leg) re-verify a
    /// TOTP code, then flip the flag off + clear the key. <b>For a
    /// <c>GlobalAdmin</c>, this is the one action that must carry the
    /// enforcement caution (pin §8.6):</b> a lone GlobalAdmin disabling MFA
    /// removes their own second factor — the TOTP code (a
    /// <see cref="MfaDisableViewModel.Code"/> <c>[Required]</c> field) is the
    /// proof, and the disable is refused if it does not validate.
    /// </summary>
    [HttpPost("/settings/security/mfa/disable")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MfaDisable(MfaDisableViewModel model)
    {
        var user = await ResolveUserAsync();
        if (user is null)
        {
            return RedirectToAction(nameof(Mfa));
        }

        var enabled = await userManager.GetTwoFactorEnabledAsync(user);
        if (!enabled)
        {
            // Nothing to disable — the flag is already off.
            return RedirectToAction(nameof(Mfa));
        }

        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Mfa));
        }

        // The §8.6 proof: the TOTP code must validate before the flag is
        // turned off (the flag is currently on, so 10.0.13's gate is already
        // satisfied — no rollback needed on the verify leg).
        // Shape note: VerifyTwoFactorTokenAsync returns bool in 10.0.13.
        var verified = await userManager.VerifyTwoFactorTokenAsync(user, AuthenticatorProvider, model.Code);
        if (!verified)
        {
            TempData["error"] = "The authenticator code was not valid. Two-factor authentication is still enabled.";
            return RedirectToAction(nameof(Mfa));
        }

        // Flag off + clear the key (the pin §4 optional-disable leg).
        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);
        TempData["info"] = "Two-factor authentication has been disabled.";
        return RedirectToAction(nameof(Mfa));
    }

    // ── F5 (AUTH-04) — session management (kill-all + invalidation) ──────────
    //
    // F5.U3 — the /settings/security/sessions surface (the pin §3 shape):
    // a sibling action set on this controller (same [Authorize], same
    // ResolveUserAsync, same anti-forgery + rate-limit discipline as the
    // F3 MFA actions above). The kill-all calls the Core seam
    // (IIdentityService.KillAllSessionsAsync — F5.U2, Core stays HTTP-free,
    // ADR 0006) then signs out the LOCAL browser (the AccountController.
    // Logout shape — SignOutAsync clears both the kumunita.auth and the
    // auxiliary .AspNetCore.Identity.Application schemes) for the instant
    // local effect. The "everywhere" effect is F5.U2's server-side stamp
    // (UpdateSecurityStampAsync rotation) + the per-request gate
    // (RevocationStampMiddleware) + the F3 leg-2 gate — this surface does
    // NOT re-implement any of that (F5.U2 owns it). The list is
    // server-side state (the revocation stamp), NOT a per-browser cookie
    // read (a server cannot enumerate another browser's cookies — pin §3).

    /// <summary>
    /// <c>GET /settings/security/sessions</c> — the session-management list.
    /// Minimum viable (pin §3): "this session" + "other active sessions
    /// (revoked if a kill-all has run)." This is **server-side state** (the
    /// <c>User.SecurityStamp</c>, rotated by
    /// <see cref="IIdentityService.KillAllSessionsAsync"/> / 
    /// <see cref="IIdentityService.ChangePasswordAsync"/>), **not** a
    /// per-browser cookie read. The <c>POST …/kill-all</c> action is the
    /// only write; there is no form to bind (no <c>ModelState</c> check).
    /// </summary>
    [HttpGet("/settings/security/sessions")]
    public IActionResult Sessions()
    {
        // Fail-closed: the [Authorize] attribute makes a null subject
        // impossible for a real request, but the pin §3 discipline is to
        // handle it anyway (redirect to login rather than render a list
        // for an unauthenticated principal).
        if (string.IsNullOrEmpty(KumunitaPrincipal.SubjectId(User)))
        {
            return RedirectToAction("Login", "Account");
        }

        return View(new SessionSettingsViewModel());
    }

    /// <summary>
    /// <c>POST /settings/security/sessions/kill-all</c> — the kill-all.
    /// <para>
    /// Calls the **Core** seam
    /// (<see cref="IIdentityService.KillAllSessionsAsync"/>) to rotate the
    /// account's security stamp (the F5.U2 seam — reuses the existing
    /// <c>UpdateSecurityStampAsync</c> rotation at the password-change lane,
    /// one <c>AccessAudit</c> row in the same Marten session; Core stays
    /// HTTP-free, ADR 0006). The "everywhere" effect is then produced by
    /// F5.U2's per-request gate (<c>RevocationStampMiddleware</c>) + the
    /// F3 leg-2 gate — every session whose cookie's <c>kumunita.stamp</c>
    /// carrier predates the rotation is signed out on its next request, and
    /// a pending F3 leg-1 <c>TwoFactorUserIdScheme</c> cookie is rejected at
    /// leg-2 completion (the §1d half-signed-in window is closed).
    /// </para>
    /// <para>
    /// The **local** effect is the <c>SignOutAsync</c> below (the
    /// <c>AccountController.Logout</c> shape — clears BOTH the
    /// <c>kumunita.auth</c> and the auxiliary
    /// <c>.AspNetCore.Identity.Application</c> schemes) for the instant
    /// local half, then a redirect to the login page.
    /// </para>
    /// </summary>
    [HttpPost("/settings/security/sessions/kill-all")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> SessionsKillAll()
    {
        var subject = KumunitaPrincipal.SubjectId(User);
        if (string.IsNullOrEmpty(subject))
        {
            // Fail-closed: no subject → no kill-all; redirect to login.
            return RedirectToAction("Login", "Account");
        }

        // The Core seam (F5.U2) — rotates the User.SecurityStamp + appends
        // one AccessAudit row in the same Marten session. Core stays
        // HTTP-free (ADR 0006); this surface does NOT re-implement the
        // rotation or the gate (F5.U2 owns both).
        await identity.KillAllSessionsAsync(subject);

        // The local effect — SignOutAsync (not a bare SignOutAsync on the
        // auth scheme) clears BOTH of the app's cookies: AddIdentity
        // registers an auxiliary .AspNetCore.Identity.Application cookie
        // alongside the kumunita.auth cookie, and only this path signs out
        // the ApplicationScheme too (the AccountController.Logout shape).
        await signInManager.SignOutAsync();

        return RedirectToAction("Login", "Account");
    }
}
