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
    IUserInfoService userInfo,
    IDocumentStore store) : Controller
{
    private static string? SubjectId(System.Security.Claims.ClaimsPrincipal user) =>
        user.FindFirst(Kumunita.Core.Identity.ClaimTypes.Subject)?.Value;

    // ── Signup ──────────────────────────────────────────────────────────────────────────

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Signup() =>
        User.Identity?.IsAuthenticated == true
            ? RedirectToAction(nameof(Profile))
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
            return RedirectToAction(nameof(Profile));

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

            return RedirectToAction(nameof(Profile));
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
            ? RedirectToAction(nameof(Profile))
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
                : RedirectToAction(nameof(Profile));
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

    // ── Profile bootstrap (M1: name + email; visibility editing is M2) ─────────────────

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var subject = SubjectId(User) ?? string.Empty;
        var model = new ProfileViewModel { Verified = false };

        if (await userInfo.GetProfileAsync(subject) is { } profile)
        {
            model.DisplayName = profile.DisplayName;
            model.Email = profile.Email;
            model.Verified = profile.Verified;
        }
        else if (await userManager.FindByIdAsync(subject) is { } user)
        {
            // No Profile doc yet (pre-bootstrap edge) — seed the page from the account.
            model.DisplayName = user.UserName;
            model.Email = user.Email;
        }

        return View(model);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var subject = SubjectId(User) ?? string.Empty;

        // UpsertProfileAsync (Core) owns the session + patch semantics: non-null patch
        // fields win, nulls leave the current row untouched (so Verified, Visibility etc.
        // survive — only name/email change from this page). Fully qualify the type because
        // the bare `Profile` collides with this controller's Profile() methods, which the
        // compiler otherwise prefers over the type.
        var baseProfile = new Kumunita.Core.UserInfo.Profile { SubjectId = subject };
        var patch = new Kumunita.Core.UserInfo.ProfileUpdate(
            model.DisplayName, model.Email, null, null, null);
        await userInfo.UpsertProfileAsync(baseProfile, patch);

        TempData["info"] = "Profile updated.";
        return RedirectToAction(nameof(Profile));
    }
}
