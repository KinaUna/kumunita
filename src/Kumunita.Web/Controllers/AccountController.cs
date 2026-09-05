using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Marten;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The resident-facing identity surface (M1 step 8): signup → verify → login, sign-out,
/// the <c>AccessDeniedPath</c> target, and the M1 profile-bootstrap page.
/// <para>
/// Cookie auth is already wired in <c>Program.cs</c>; <see cref="KumunitaClaimsPrincipalFactory"/>
/// (step 6) is the only place that mints the admissible claim set (<see cref="ClaimTypes.All"/>),
/// and <see cref="IIdentityService"/> (Core, step 6) owns the signup/verify semantics. This
/// controller only shapes HTTP and calls the frozen seams.
/// </para>
/// <para>
/// Unverified accounts cannot sign in: <see cref="KumunitaClaimsPrincipalFactory"/> omits the
/// <see cref="Roles.Member"/> role while <c>Profile.Verified</c> is false, and
/// <see cref="SignInManager{TUser}"/>'s lockout/verification checks hold the login itself —
/// no extra middleware needed.
/// </para>
/// </summary>
public sealed class AccountController(
    SignInManager<User> signInManager,
    UserManager<User> userManager,
    IIdentityService identity,
    IDocumentStore store) : Controller
{
    private static string? SubjectId(System.Security.Claims.ClaimsPrincipal user) =>
        user.FindFirst(Kumunita.Core.Identity.ClaimTypes.Subject)?.Value;

    // ── Signup ──────────────────────────────────────────────────────────────────────────

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Signup() =>
        User.Identity?.IsAuthenticated == true
            ? Redirect("/profile/edit")
            : View();

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Signup(SignupViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            // RegisterAsync: creates the unverified account, bootstraps the Profile
            // (visibility self-only, Core-defaulted), mints the verify token, stages the
            // verification email into the durable outbox — all in one Core transaction.
            await identity.RegisterAsync(model.DisplayName, model.Email, model.Password);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        TempData["info"] = "Account created. Check your inbox for the verification link.";
        return RedirectToAction(nameof(Login));
    }

    // ── Verify (the one designed handoff) ───────────────────────────────────────────────

    [AllowAnonymous]
    public async Task<IActionResult> Verify([FromQuery] string id)
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect("/profile/edit");

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var token = await session.LoadAsync<IdentityToken>(id);

        if (token is null
            || token.Kind != IdentityToken.KindVerify
            || !token.IsUsableAt(DateTimeOffset.UtcNow))
        {
            return View(new VerifyViewModel
            {
                Error = "This verification link is invalid, expired, or already used. " +
                        "Sign up again or ask an admin to verify your account."
            });
        }

        try
        {
            // VerifyWithTokenAsync flips Profile.Verified and consumes the token in one
            // Core transaction (audit row via:Owner).
            var profile = await identity.VerifyWithTokenAsync(token.Token);
            var user = await userManager.FindByIdAsync(profile.SubjectId)
                ?? throw new InvalidOperationException("Account not found.");

            // The verification link is the handoff end — the resident should land signed-in,
            // so mint the cookie through the same factory step 6 uses at sign-in (the claim
            // set is the whole principal; no extra DB read on later requests).
            var factory = HttpContext.RequestServices.GetRequiredService<KumunitaClaimsPrincipalFactory>();
            var identityPrinciple = await factory.CreateAsync(user);

            // The verification link's purpose is to end the handoff with the resident
            // signed-in — this branch bypasses the password check (the link IS the proof
            // the user owns the account) while keeping the same admissible claim shape
            // the rest of the request pipeline expects.
            await HttpContext.SignInAsync(
                scheme: CookieAuthenticationDefaults.AuthenticationScheme,
                principal: identityPrinciple);

            return Redirect("/profile/edit");
        }
        catch (InvalidOperationException ex)
        {
            return View(new VerifyViewModel { Error = ex.Message });
        }
    }

    // ── Login ───────────────────────────────────────────────────────────────────────────

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login([FromQuery] string? returnUrl = null) =>
        User.Identity?.IsAuthenticated == true
            ? Redirect("/profile/edit")
            : View(new LoginViewModel { ReturnUrl = returnUrl });

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await userManager.FindByNameAsync(model.Email)
                 ?? await userManager.FindByEmailAsync(model.Email);

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Email or password is incorrect.");
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(
            user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return Url.IsLocalUrl(model.ReturnUrl)
                ? Redirect(model.ReturnUrl)
                : Redirect("/profile/edit");
        }

        ModelState.AddModelError(string.Empty,
            result.IsLockedOut
                ? "Account is temporarily locked. Try again in a few minutes."
                : "Email or password is incorrect.");
        return View(model);
    }

    // ── Sign-out ────────────────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    // ── Access denied (the cookie's AccessDeniedPath target, Program.cs) ────────────────

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    // ── Profile (U14: M1 bootstrap page redirected to M2's editor) ─────────────────────
    //
    // U14 (M2) closed M1's name/email bootstrap page. The M1 action's
    // read + write lanes are both superseded by /profile/edit (U11's write
    // lane, the single profile-editor surface M2 promised). The M2 plan's
    // line 184 ("the /Account/Profile route returns a 301 redirect to
    // /profile/edit, or 404 if removed") chose the 301 stub path: a
    // permanent redirect so any M1 bookmarks / mail links that still
    // reference the old address land on the live editor. The M1
    // <c>ProfileViewModel</c> (name + email + Verified) is now dead
    // surface-level code; M2's <c>ProfileEditViewModel</c> (U11) carries
    // the two audience fields (Visibility, ContactVisibility) + the two
    // M1 bootstrap fields. M3 may delete the M1 VM; U14 does not (the M2
    // plan line 183 limits U14 to the controller / nav / view).
    [Authorize]
    [HttpGet]
    public IActionResult Profile() =>
        RedirectPermanent("/profile/edit");
}
