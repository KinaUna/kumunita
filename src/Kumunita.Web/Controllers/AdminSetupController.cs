using Kumunita.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The /admin/setup handoff — the one-time lane where the seed GlobalAdmin
/// account receives its real password (from the setup token) and is signed in.
/// <para>
/// The class has no <see cref="AuthorizeAttribute"/> at the controller level:
/// every action in this controller must be reachable before the account has a
/// working password. After <see cref="Setup(SetupViewModel)"/>
/// completes, the account already has a password hash, and it can be signed
/// in through <c>/Login</c> using those credentials.
/// </para>
/// <para>
/// The URL is <c>/admin/setup</c>; the seeder's first-boot email
/// (see <c>FirstBootSeeder.SeedAdminBody</c>) links to exactly this path, and
/// <c>/Account/Login</c> surfaces a secondary link to it, so the route is
/// pinned explicitly below.
/// </para>
/// <para>
/// This controller is deliberately tiny — the actual credential swap is
/// owned by <see cref="Kumunita.Core.Identity.IIdentityService.CompleteSeedAdminSetupAsync"/>;
/// this file only shapes the HTTP (GET form, POST form) and then signs the
/// user in through the same claims-factory path that <c>AccountController.Verify</c>
/// uses. That keeps the claim minting surface in one place (the factory),
/// not scattered across token lanes.
/// </para>
/// </summary>
[Route("/admin/setup")]
public sealed class AdminSetupController(
    Kumunita.Core.Identity.IIdentityService identity,
    Microsoft.AspNetCore.Identity.UserManager<Kumunita.Core.Identity.User> userManager,
    Kumunita.Web.KumunitaClaimsPrincipalFactory claimsFactory) : Controller
{
    // ── GET /admin/setup — the form ──────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Setup()
    {
        // Already signed in — bounce to the profile editor.
        if (User.Identity?.IsAuthenticated == true)
            return Redirect("/profile/edit");

        // M2 — the first-boot setup lane is closed once the seed-admin account
        // has a working password (CompleteSeedAdminSetupAsync consumed the token
        // and set Profile.Verified). After that there is no live setup surface,
        // and the endpoint should 404 (not 200-with-a-form) so an anonymous
        // caller cannot probe the account's existence or keep a stale form in
        // the wild. The login page (AccountController.Login) already surfaces a
        // secondary link to /admin/setup only while the lane is open (the
        // ShowSetupLink flag), so this is the one authoritative gate.
        if (await identity.IsFirstBootSetupCompleteAsync())
            return NotFound();

        return View(new SetupViewModel());
    }

    // ── POST /admin/setup — the swap + sign-in ───────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("setup")]
    public async Task<IActionResult> Setup(SetupViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // M2 — setup lane already complete: the token is consumed (or never
        // existed for this email). Return 404 (the lane is closed) rather than
        // surfacing the Core's InvalidOperationException (which would reveal
        // "account not found" vs "token invalid" vs "already used" — three
        // distinguishable signals to an attacker). A 404 is uniform for all
        // three cases and matches the GET's closed-lane response.
        if (await identity.IsFirstBootSetupCompleteAsync())
            return NotFound();

        // The token is the credential; the password the user just chose becomes
        // the long-term credential. If anything fails (bad token, already used,
        // expired, no seed-admin account at that email), Core throws
        // InvalidOperationException and this surfaces it in the form. Do NOT
        // swallow other exceptions — they mask a programming error.
        Kumunita.Core.Identity.ThinPrincipal principal;
        try
        {
            principal = await identity.CompleteSeedAdminSetupAsync(
                email: model.Email,
                setupTokenValue: model.SetupToken,
                newPassword: model.NewPassword);
        }
        catch (InvalidOperationException)
        {
            // M2 — the lane is open (the GET check above would have 404'd if it
            // were closed) but the token is invalid / expired / not for this
            // email. Return 404 (uniform, no account-existence signal) rather
            // than surfacing the exception message.
            return NotFound();
        }

        // Sign the user in through the SAME factory the rest of the app uses.
        // This is the only place the admissible claim set is minted (per ADR
        // 0006-B); loading the user by Id (the principal.SubjectId — returned
        // from CompleteSeedAdminSetupAsync) is the only read we need.
        var user = await userManager.FindByIdAsync(principal.SubjectId)
            ?? throw new InvalidOperationException("Seed-admin account missing after setup completed.");

        var claimsPrincipal = await claimsFactory.CreateAsync(user);

        // Mint the same persistent, 14-day cookie every other sign-in lane issues so
        // the first-boot setup handoff leaves the operator on a long-lived cookie
        // rather than a session cookie that dies when the browser closes.
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.Add(TimeSpan.FromDays(14)),
        };
        await HttpContext.SignInAsync(
            scheme: CookieAuthenticationDefaults.AuthenticationScheme,
            principal: claimsPrincipal,
            properties: authProperties);

        // The setup token is now consumed (Core marked IdentityToken.ConsumedAt),
        // the account has a password hash, and the audit row is written. The
        // operator should remove SeedAdmin__* from env (OPS Procedure 2, step 4)
        // — this page does NOT do that on their behalf (it's a Coolify env-var
        // change, not an application state change).
        return Redirect("/profile/edit");
    }
}
