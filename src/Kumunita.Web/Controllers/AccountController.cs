using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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

    // P1-5 (translation audit) — localize the four Account forms' DataAnnotations
    // messages. The platform's closed translation surface is the KnownTranslationKeys
    // registry (ADR 0015); the four account.err.* keys live there (en/de/fr/da).
    // Resolved from the request scope (the Storage() idiom) so the constructor is
    // unchanged for the existing harnesses. A no-op on a non-HTTP render (null
    // provider) or an unmapped attribute.
    private async Task LocalizeValidationAsync(object model)
    {
        // In unit tests (and any non-HTTP host) RequestServices is absent —
        // skip localization rather than throw on a null provider.
        var services = HttpContext?.RequestServices;
        var request = HttpContext?.Request;
        if (services is null)
        {
            return;
        }
        var localization = services.GetService<Kumunita.Core.Localization.ILocalizationService>();
        var provider = services.GetService<Kumunita.Core.Localization.ITranslationProvider>();
        if (provider is null)
        {
            return;
        }
        await Kumunita.Web.Localization.AccountValidationLocalizer.ApplyAsync(
            model, ModelState, request, localization, provider);
    }

    // ── My storage (M25 U7 — the self-only resident usage read) ────────────────────────

    /// <summary>
    /// <c>GET /account/storage</c> — the resident's **own** storage usage (M25
    /// U7, C-UP·4/F7): their own usage bytes, the community per-user quota (or
    /// "unlimited"), and how much of it remains. **Self-only** — the subject is
    /// the signed-in principal (<see cref="SubjectId(System.Security.Claims.ClaimsPrincipal)"/>),
    /// minted server-side and never taken from a route param, so a resident can
    /// never read another resident's numbers.
    /// <para>
    /// <b>Read-only</b> — no write lane here (the admin settings surface, U5/U6,
    /// is the only writer), and it emits **no** <c>AccessAudit</c> row
    /// (C-UP·7: a resident's own usage is not an audience-restricted read).
    /// The seam (<see cref="Kumunita.Core.Usage.IStorageSettingsService"/>) is
    /// resolved from the request scope — the <see cref="Verify(string)"/> idiom —
    /// so the controller constructor is unchanged. The usage read
    /// (<c>GetPerUserUsageBytesAsync</c>) reuses the M24 C-SM·7
    /// <c>Σ SizeBytes WHERE CreatedById</c> seam (U4); the quota comes from
    /// <c>GetOrCreateAsync</c> (the create-if-missing sentinel). Sentinel
    /// (C-UP·5): a quota of <c>0</c> ⇒ **unlimited** — rendered as "Unlimited",
    /// not <c>0</c> remaining and not an error.
    /// </para>
    /// </summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Storage()
    {
        // Self-only (C-UP·4/F7): the subject is the signed-in principal —
        // never a path param.
        var subject = SubjectId(User);
        if (subject is null)
            return Challenge();

        // Resolve the read seam from the request scope (the Verify idiom) — keeps
        // the controller constructor untouched for the existing harnesses.
        var settings = HttpContext.RequestServices
            .GetRequiredService<Kumunita.Core.Usage.IStorageSettingsService>();

        // Two independent reads (quota doc + own usage): start both, then await
        // each for its value (the AdminStorageController.Index shape).
        var quotaTask  = settings.GetOrCreateAsync(CancellationToken.None);
        var usageTask  = settings.GetPerUserUsageBytesAsync(subject, CancellationToken.None);
        var quota      = await quotaTask;
        long usage      = await usageTask;

        bool unlimited = quota.PerUserQuotaBytes == 0;      // C-UP·5 sentinel
        var remaining  = unlimited ? (long?)null
                                   : Math.Max(0, quota.PerUserQuotaBytes - usage);

        return View(new ResidentStorageViewModel
        {
            MyUsageBytes      = usage,
            PerUserQuotaBytes = quota.PerUserQuotaBytes,
            RemainingBytes    = remaining,                  // null ⇒ unlimited
            QuotaUnlimited    = unlimited,
            MyUsageHuman      = ResidentStorageViewModel.FormatBytes(usage),
            QuotaHuman        = unlimited ? "Unlimited" : ResidentStorageViewModel.FormatBytes(quota.PerUserQuotaBytes),
            RemainingHuman    = unlimited ? "Unlimited" : ResidentStorageViewModel.FormatBytes(remaining!.Value)
        });
    }

    // ── Change password (self-serve; ADR 0138) ───────────────────────────────

    /// <summary>
    /// <c>GET /account/password</c> — the resident's self-serve change-password
    /// form (ADR 0138). The subject is the signed-in principal (never a path
    /// param). When the instance has opted in to the sample-account lock
    /// (<see cref="IIdentityService.IsChangePasswordLockedForAsync"/> — a
    /// <c>SampleData__Enabled</c> instance, the admin lock on, and this account
    /// a non-admin sample account), the form is replaced by the static
    /// <c>ChangePasswordLocked</c> notice (the same "surface replaced by a
    /// notice" shape as the ADR 0050 <c>SignupClosed</c> lane). Otherwise the
    /// change-password form is returned.
    /// </summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> ChangePassword()
    {
        var subject = SubjectId(User);
        if (subject is null)
            return Challenge();

        // ADR 0138 — the lock (the single decision seam, so the GET and the
        // POST agree): a locked, non-admin sample account gets the static
        // notice instead of the form.
        if (await identity.IsChangePasswordLockedForAsync(subject))
            return View("ChangePasswordLocked", new ChangePasswordLockedViewModel());

        return View(new ChangePasswordViewModel());
    }

    /// <summary>
    /// <c>POST /account/password</c> — the self-serve write lane (ADR 0138).
    /// <b>The guard is authoritative on the write path too</b> (the ADR 0050
    /// gate shape): a locked account is denied before any write, even if the
    /// form were crafted by hand. Otherwise the <b>current</b> password is
    /// verified against the account (the self-serve lane confirms it is really
    /// this resident), and the new password is written through
    /// <see cref="IIdentityService.ChangePasswordAsync"/> (the single audited
    /// write lane, <c>via: Owner</c>; it rotates the security stamp). On
    /// success the resident is signed out (the credential just changed —
    /// confirm the new one on the next sign-in) and returned to the login
    /// surface with a <c>info</c> flash.
    /// </summary>
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        var subject = SubjectId(User);
        if (subject is null)
            return Challenge();

        // ADR 0138 — the lock, authoritative on the write path (defense in
        // depth over the Core guard inside ChangePasswordAsync): a locked,
        // non-admin sample account is denied with the static notice — no
        // current-password check, no write.
        if (await identity.IsChangePasswordLockedForAsync(subject))
            return View("ChangePasswordLocked", new ChangePasswordLockedViewModel());

        if (!ModelState.IsValid)
        {
            await LocalizeValidationAsync(model);
            return View(model);
        }

        // Verify the current password (the self-serve lane proves it is really
        // this resident changing their own credential — not a blind set). A
        // wrong current password is a form error (the account is untouched).
        var user = await userManager.FindByIdAsync(subject);
        if (user is null)
        {
            ModelState.AddModelError(nameof(model.CurrentPassword),
                "We could not find that account — sign in again.");
            return View(model);
        }

        var currentOk = await userManager.CheckPasswordAsync(user, model.CurrentPassword);
        if (!currentOk)
        {
            ModelState.AddModelError(nameof(model.CurrentPassword),
                "Your current password is incorrect.");
            return View(model);
        }

        try
        {
            // The single audited write lane (via: Owner; rotates the security
            // stamp, so this account's existing sessions are invalidated).
            await identity.ChangePasswordAsync(subject, model.NewPassword, byAdmin: false);
        }
        catch (UnauthorizedAccessException)
        {
            // The Core guard (IsChangePasswordLockedForAsync) refused the write
            // — surface the locked notice (defense in depth: the GET/POST lock
            // check above normally catches this first).
            return View("ChangePasswordLocked", new ChangePasswordLockedViewModel());
        }

        // The credential just changed: sign this session out so the new
        // password is confirmed on the next sign-in (the ChangePasswordAsync
        // security-stamp rotation already invalidates this account's other
        // sessions).
        await signInManager.SignOutAsync();
        TempData["info"] = "Your password was changed. Sign in again with the new password.";
        return RedirectToAction(nameof(Login));
    }

    // ── Signup ──────────────────────────────────────────────────────────────────────────

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Signup()
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect("/profile/edit");

        // ADR 0050 — the sign-up gate (the admin-settled instance value, the
        // `true` floor). Closed → the invitation-only notice instead of the form;
        // the gate is authoritative (an authenticated admin still creates accounts
        // through the Guardian / admin lanes, not this self-service surface).
        if (!await identity.IsSignupOpenAsync())
            return View("SignupClosed", new SignupClosedViewModel());

        return View(new SignupViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Signup(SignupViewModel model)
    {
        // ADR 0050 — the gate is authoritative on the write path too (the GET hides
        // the form, but the POST is the actual account-creation surface): a closed
        // gate denies the self-service write. A resident who is *invited* is added
        // by an administrator, not through this endpoint.
        if (!await identity.IsSignupOpenAsync())
            return View("SignupClosed", new SignupClosedViewModel());

        if (!ModelState.IsValid)
        {
            await LocalizeValidationAsync(model);
            return View(model);
        }

        try
        {
            // RegisterAsync: creates the unverified account, bootstraps the Profile
            // (visibility self-only, Core-defaulted), mints the verify token, stages the
            // verification email into the durable outbox — all in one Core transaction.
            await identity.RegisterAsync(model.DisplayName, model.Email, model.Password);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.StartsWith("An account with email", StringComparison.OrdinalIgnoreCase))
                model.EmailAlreadyExists = true;
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        TempData["info"] = "Account created. Check your inbox for the verification link.";
        return RedirectToAction(nameof(Login));
    }

    // ── Resend confirmation email (an unactivated account already exists for the email) ──

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResendVerification([FromQuery] string? email) =>
        View(new ResendVerificationViewModel { Email = email ?? string.Empty });

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("resend")]
    public async Task<IActionResult> ResendVerification(ResendVerificationViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await LocalizeValidationAsync(model);
            return View(model);
        }

        await identity.ResendVerificationEmailAsync(model.Email);
        // M1 — uniform response: whether or not an account exists for this email
        // (and whether the per-account attempt bound is exhausted), the response is
        // identical. The Core service returns different Reason strings for the two
        // failure modes; both must produce the SAME response so an attacker probing
        // which emails are registered cannot distinguish them. The uniform message
        // is "if an unverified account exists, a link is on its way" — true for the
        // fresh-account path, false for the no-account / exhausted paths, but the
        // resident cannot tell which is which.
        TempData["info"] =
            $"If there is an unverified account with email '{model.Email}', a new " +
            "verification link is on its way. Check your inbox (and your spam folder).";
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
            // the rest of the request pipeline expects. Mint the same persistent,
            // 14-day cookie every sign-in lane issues: the handler honors the explicit
            // ExpiresUtc and, with sliding expiration on, refreshes the ticket as the
            // resident keeps the tab open. Without this, the handler falls back to a
            // session cookie that dies when the browser closes.
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,  // persistent cookie — survives a browser close
                ExpiresUtc = DateTimeOffset.UtcNow.Add(TimeSpan.FromDays(14)),
            };
            await HttpContext.SignInAsync(
                scheme: CookieAuthenticationDefaults.AuthenticationScheme,
                principal: identityPrinciple,
                properties: authProperties);

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
    public async Task<IActionResult> Login([FromQuery] string? returnUrl = null, [FromQuery] string? error = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect("/profile/edit");

        // `error` arrives as a short code (not the message text) — the query string is
        // user-visible and may be bookmarked/shared, so keep it token-like. The
        // code→message table lives in the Login view (D6 — "the mapping is a 3-row
        // table in the view"). The three known codes are:
        //   "blocked"          — BlockedAccountMiddleware (suspension mid-session)
        //                        + the POST-Login block-enforcement guard.
        //   "account-removed"  — PrivilegedStampMiddleware (user deleted).
        //   "role-changed"     — PrivilegedStampMiddleware (role-set mismatch).
        // An unknown code is passed through verbatim — the view falls back to
        // `Model.Error` (forward-compatible: a future code still shows something).

        // Hide the "Received a first-boot setup token?" hint once the seed-admin
        // setup has already been completed (or its token has since expired) — after
        // that there is no live setup lane left to complete, and the account signs in
        // with a real password.
        var showSetupLink = !await identity.IsFirstBootSetupCompleteAsync();

        // ADR 0050 — the sign-up gate (the `true` floor): when closed, the view
        // suppresses the "No account yet? Sign up." affordance (a closed gate has
        // no self-service signup surface to point at).
        var signupOpen = await identity.IsSignupOpenAsync();

        return View(new LoginViewModel
        {
            ReturnUrl = returnUrl,
            Error = error,   // ← the code, not a resolved message (D6: the view does the mapping)
            ShowSetupLink = showSetupLink,
            SignupOpen = signupOpen,
        });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await LocalizeValidationAsync(model);
            return View(model);
        }

        var user = await userManager.FindByNameAsync(model.Email)
                 ?? await userManager.FindByEmailAsync(model.Email);

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Email or password is incorrect.");
            return View(model);
        }

        // Always mint a persistent, long-lived cookie. PasswordSignInAsync's
        // `isPersistent` flag sets AuthenticationProperties.IsPersistent — the
        // app wants long-lived logins (not a browser-session cookie that dies
        // when the tab closes), so pass true unconditionally: the RememberMe
        // checkbox stays for the form but the cookie is persistent either way.
        // The ticket (and hence the cookie's) lifetime is set by the cookie
        // handler's ExpireTimeSpan (14 days, Program.cs) and its sliding
        // expiration refreshes it while the resident keeps the tab active.
        var result = await signInManager.PasswordSignInAsync(
            user, model.Password, isPersistent: true, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            // Block enforcement at the login seam: a GlobalAdmin's suspension (Profile.Blocked)
            // must not end in a signed-in resident, even if the cookie minted below somehow
            // carried standing (it does not — the ClaimsPrincipalFactory strips it at mint,
            // but the explicit sign-out + message here is what the feature promises the
            // resident: a login attempt that ends in rejection, not a silent bounce to a
            // page they then cannot open). Kept here (not in the factory) because the
            // factory cannot fail sign-in; the controller is the one place that can
            // inspect the account after credentials are verified.
            var profile = await userInfo.GetProfileAsync(user.Id ?? string.Empty);
            if (profile is not null && profile.Blocked)
            {
                await signInManager.SignOutAsync();
                // The GET-Login action maps the "blocked" code to the resident-facing
                // message (see Login GET) — keep it as a code here too, consistent with
                // the BlockedAccountMiddleware path and non-informative in URLs.
                return RedirectToAction(nameof(Login), new { error = "blocked" });
            }

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
        // SignOutAsync (not a bare SignOutAsync on the auth scheme) clears BOTH
        // of the app's cookies: AddIdentity registers an auxiliary anonymous
        // .AspNetCore.Identity.Application cookie alongside the kumunita.auth
        // cookie, and only this path signs out the ApplicationScheme too.
        await signInManager.SignOutAsync();
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
