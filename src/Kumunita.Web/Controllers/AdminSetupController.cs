using Kumunita.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

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
    public IActionResult Setup() =>
        User.Identity?.IsAuthenticated == true
            ? Redirect("/profile/edit")
            : View(new SetupViewModel());

    // ── POST /admin/setup — the swap + sign-in ───────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Setup(SetupViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

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
        catch (InvalidOperationException ex)
        {
            model.Error = ex.Message;
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        // Sign the user in through the SAME factory the rest of the app uses.
        // This is the only place the admissible claim set is minted (per ADR
        // 0006-B); loading the user by Id (the principal.SubjectId — returned
        // from CompleteSeedAdminSetupAsync) is the only read we need.
        var user = await userManager.FindByIdAsync(principal.SubjectId)
            ?? throw new InvalidOperationException("Seed-admin account missing after setup completed.");

        var claimsPrincipal = await claimsFactory.CreateAsync(user);
        await HttpContext.SignInAsync(
            scheme: CookieAuthenticationDefaults.AuthenticationScheme,
            principal: claimsPrincipal);

        // The setup token is now consumed (Core marked IdentityToken.ConsumedAt),
        // the account has a password hash, and the audit row is written. The
        // operator should remove SeedAdmin__* from env (OPS Procedure 2, step 4)
        // — this page does NOT do that on their behalf (it's a Coolify env-var
        // change, not an application state change).
        return Redirect("/profile/edit");
    }
}
