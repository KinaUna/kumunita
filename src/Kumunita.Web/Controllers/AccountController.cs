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
    // This controller uses conventional routing ({controller}/{action}), which
    // would map the `ChangePassword` action to /account/changepassword. The
    // documented URL (ADR 0138, the settings-tab nav link, the form's
    // asp-action) is /account/password, so pin it explicitly.
    [Route("account/password")]
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
    // Explicit route so the POST targets /account/password (matching the GET
    // and the form's action) rather than the conventional /account/changepassword.
    [Route("account/password")]
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

    // ── Delete account (self-serve; ADR 0142) ─────────────────────────────

    /// <summary>
    /// <c>GET /account/delete</c> — the resident's self-serve delete-account
    /// form (ADR 0142). The subject is the signed-in principal (never a path
    /// param). The form asks for the resident's current password (verified
    /// server-side on the POST) and an explicit acknowledgment checkbox (the
    /// dangerous-action guard). The lede text explains the consequences:
    /// the account is removed, the resident's audit trail is pseudonymized
    /// (their identity replaced by a tombstone), and their group/community
    /// memberships are removed — the privacy lane OPS.md §9 / ARCHITECTURE.md
    /// §5 prescribe.
    /// <para>
    /// **ADR 0142 D5 gate (the ADR 0138 "surface-replaced-by-notice"
    /// shape, generalized):** the self-serve lane is reachable only by a
    /// <c>GlobalAdmin</c>. A non-GlobalAdmin resident sees the same form
    /// with the <see cref="DeleteAccountViewModel.SelfDeletionRefused"/>
    /// flag set, so the view renders a notice that the self-serve lane is
    /// unavailable for them and that they should contact an administrator
    /// instead — rather than letting the resident discover the gate only
    /// on submit. A crafted POST is refused server-side with the same
    /// message (defense in depth).
    /// </para>
    /// </summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Delete()
    {
        // The subject must be the signed-in principal — the [Authorize]
        // attribute above guarantees that (an unauthenticated request is
        // redirected to /Account/Login by the cookie handler's
        // AccessDeniedPath), so this method only runs for an authenticated
        // resident. A defensive null-check mirrors the Storage() /
        // ChangePassword() shape.
        var subject = SubjectId(User);
        if (subject is null)
            return Challenge();

        // ADR 0142 D5 gate — read the standing (a read, no audit row) and
        // set the notice flag when the resident is not a GlobalAdmin.
        // The form is still rendered (so the surface is discoverable), but
        // the page makes the gate visible (the ADR 0138
        // ChangePasswordLockedViewModel "notice" shape, generalized).
        var user = await userManager.FindByIdAsync(subject);
        var roles = user is null
            ? new List<string>()
            : (await userManager.GetRolesAsync(user)).ToList();
        var refused = !roles.Contains(Kumunita.Core.Identity.Roles.GlobalAdmin);

        return View(new DeleteAccountViewModel { SelfDeletionRefused = refused });
    }

    /// <summary>
    /// <c>POST /account/delete</c> — the self-serve write lane (ADR 0142).
    /// <b>The guard is authoritative on the write path too</b> (the ADR 0138
    /// shape): the resident must have checked the acknowledgment box AND
    /// provided the correct current password — otherwise the form re-renders
    /// with the error (the account is untouched). When both pass, the Core
    /// lane (<see cref="Kumunita.Core.Identity.IIdentityService
    /// .DeleteAccountAsync"/>) is invoked with the resident as the actor —
    /// the Core lane still enforces the GlobalAdmin gate (the fail-closed
    /// pin), so a non-admin self-deletion is refused server-side.
    /// <para>
    /// On success the resident is signed out (their credential is gone) and
    /// redirected to the login page with a confirmation message. The
    /// login page's "account-removed" error code is NOT used here — that
    /// code is for the PrivilegedStampMiddleware's "user deleted while
    /// signed in" path; this is the *intentional* self-deletion lane, so
    /// the message is the positive "your account was deleted" form.
    /// </para>
    /// </summary>
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(DeleteAccountViewModel model)
    {
        // Self-only (the C-UP·4/F7 shape from Storage()): the subject is
        // the signed-in principal — never a path param.
        var subject = SubjectId(User);
        if (subject is null)
            return Challenge();

        // The dangerous-action guard: the resident must have checked the
        // acknowledgment box. A bare [Required] on a bool would accept
        // false (the bound value is non-null), so the check is explicit.
        if (!model.Confirmed)
        {
            ModelState.AddModelError(
                nameof(model.Confirmed),
                "You must confirm that you understand your account will be permanently deleted.");
        }

        // Verify the current password (the self-serve lane proves it is really
        // this resident deleting their own account — not a crafted request).
        // A wrong password is a form error (the account is untouched).
        var user = await userManager.FindByIdAsync(subject);
        if (user is null)
        {
            ModelState.AddModelError(nameof(model.Password),
                "We could not find that account — sign in again.");
        }
        else
        {
            var currentOk = await userManager.CheckPasswordAsync(user, model.Password);
            if (!currentOk)
            {
                ModelState.AddModelError(nameof(model.Password),
                    "Your password is incorrect.");
            }
        }

        if (!ModelState.IsValid)
        {
            await LocalizeValidationAsync(model);
            return View(model);
        }

        try
        {
            // The single audited write lane (via: Owner on the self-serve
            // branch — actor == target). ADR 0142 D5 gate: the Core seam
            // enforces the GlobalAdmin standing on this branch (a
            // non-GlobalAdmin's crafted POST is refused server-side, the
            // fail-closed pin). Pseudonymizes the resident's audit rows,
            // removes their memberships + profile, and deletes the Identity
            // account.
            await identity.DeleteAccountAsync(subject, adminSubjectId: subject);
        }
        catch (UnauthorizedAccessException)
        {
            // ADR 0142 D5 gate (defense in depth — the GET's
            // SelfDeletionRefused notice is the primary surface, but a
            // crafted POST reaches the Core seam directly). The resident is
            // not a GlobalAdmin, so the self-serve lane is unavailable for
            // them. Surface the notice (the ADR 0138 "locked" shape).
            ModelState.AddModelError(string.Empty,
                "The self-serve delete-account lane is only available to a " +
                "GlobalAdmin. A non-GlobalAdmin resident cannot delete their " +
                "own account — contact an administrator to remove the account.");
            return View(new DeleteAccountViewModel { SelfDeletionRefused = true });
        }
        catch (InvalidOperationException ex)
        {
            // The Core lane refused the write (the last-GlobalAdmin guard —
            // a lone GlobalAdmin cannot self-delete — or the account no
            // longer exists, the idempotency pin). Surface the message.
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        // The account is gone: sign this session out (their credential is
        // deleted — the cookie is now stale) and redirect to the login page
        // with a positive confirmation.
        await signInManager.SignOutAsync();
        TempData["info"] = "Your account has been deleted. Your audit trail is preserved (pseudonymized) per the platform's privacy policy.";
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
        // `false` floor — a fresh instance ships invitation-only). Closed → the
        // invitation-only notice instead of the form;
        // the gate is authoritative (an authenticated admin still creates accounts
        // through the Guardian / admin lanes, not this self-service surface).
        if (!await identity.IsSignupOpenAsync())
            return View("SignupClosed", new SignupClosedViewModel());

        var model = new SignupViewModel();

        // F9 (AUTH-15, U3) — surface the flag + site key to the view (pin §4b): the
        // challenge widget + the hidden BotChallengeToken input render ONLY when the
        // owner's KUMUNITA__SIGNUP__BOT_CHECK flag is on (the registered verifier is
        // NOT the Noop default). Resolved from the request scope (the
        // LocalizeValidationAsync idiom — the constructor is unchanged). Off (the
        // NoopBotChallengeVerifier, the default) → BotChallengeEnabled stays false and
        // the signup form is byte-identical to before this seam (the standing owner
        // deferral, OPS.md §10; the frozen CSP invariant — no new origin exposed).
        var botChallengeVerifier = HttpContext?.RequestServices
            ?.GetService<Kumunita.Core.Identity.IBotChallengeVerifier>();
        if (botChallengeVerifier is not Kumunita.Core.Identity.NoopBotChallengeVerifier)
        {
            model.BotChallengeEnabled = true;
            model.BotChallengeSiteKey = HttpContext?.RequestServices
                ?.GetService<Microsoft.Extensions.Options.IOptions<Kumunita.Core.Identity.SignupBotChallengeOptions>>()
                ?.Value.SiteKey;
        }

        return View(model);
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("signup")]
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

        // F9 (AUTH-15, U3) — the bot-challenge gate (pin §4b): when the owner's
        // KUMUNITA__SIGNUP__BOT_CHECK flag is on, the signup POST must carry a
        // valid challenge token (the widget wrote it into BotChallengeToken).
        // Resolved from the request scope (the LocalizeValidationAsync idiom — the
        // constructor is unchanged for the existing direct-construction harnesses)
        // so a harness with no HTTP host / no registered IBotChallengeVerifier is a
        // no-op (null → skipped), exactly like the F3 leg-2 gate. The Core lane
        // (RegisterCoreAsync / IIdentityService) does NOT call the verifier — the
        // verification is a Web-enforced I/O gate (the IMailerStage "designed
        // handoff" precedent, pin §4b). On false: re-render with the
        // BotChallengeFailed flag (the EmailDomainDisallowed precedent, §3b) — a
        // bot is not proven human, and the reason is NOT leaked (the
        // uniform-rejection shape, the F3 leg-2 precedent).
        var botChallengeVerifier = HttpContext?.RequestServices
            ?.GetService<Kumunita.Core.Identity.IBotChallengeVerifier>();
        if (botChallengeVerifier is not null &&
            !await botChallengeVerifier.IsVerifiedAsync(model.BotChallengeToken))
        {
            model.BotChallengeFailed = true;
            ModelState.AddModelError(string.Empty,
                "The security check did not pass. Please try again.");
            return View(model);
        }

        try
        {
            // RegisterAsync: creates the unverified account, bootstraps the Profile
            // (visibility self-only, Core-defaulted), mints the verify token, stages the
            // verification email into the durable outbox — all in one Core transaction.
            await identity.RegisterAsync(model.DisplayName, model.Email, model.Password);
        }
        catch (DisallowedEmailDomainException ex)
        {
            // F9 (AUTH-15) — the disposable-email blocklist rejection. Caught BEFORE the
            // `InvalidOperationException` catch (the dedicated type must not be swallowed
            // by it) and re-rendered with the `EmailDomainDisallowed` flag (the
            // `EmailAlreadyExists` precedent) — pin §3b.
            model.EmailDomainDisallowed = true;
            ModelState.AddModelError(string.Empty,
                $"The email domain '{ex.Domain}' is not accepted for new accounts.");
            return View(model);
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
    // ADR 0146 — the handoff is now two-shaped: the self-serve lane (the credential was
    // set at signup) confirms the account on the link click, while the **child lane**
    // (the guardian created the account without holding the child's credential — ADR
    // 0028 "supervision rides the link, not the password") collects the child's own
    // password on the confirmation page and activates the account in one commit. The
    // lane is decided by whether the account has a password (the single-source
    // `UserManager.HasPasswordAsync` read), so a resident who exhausts the resend
    // attempts and is manually verified still gets the confirm-only surface, and a
    // child who re-requests the link still gets the set-password surface.

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Verify([FromQuery] string id)
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect("/profile/edit");

        var (token, errorModel) = await LoadUsableVerifyTokenAsync(id);
        if (token is null)
            return View(errorModel);

        if (await AccountHasPasswordAsync(token.UserId))
        {
            // Self-serve lane — the credential exists; confirm and sign in directly.
            return await ConfirmAndSignInAsync(token);
        }

        // Child lane — the account has no password; render the set-password form so
        // the child sets their own credential at the confirmation link.
        return View(new VerifyViewModel { RequiresPassword = true, Id = id });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify(VerifyViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect("/profile/edit");

        // The POST carries the token row id back in the model (the form's hidden
        // input) — the child lane re-submits to the same link.
        var id = model.Id ?? string.Empty;
        var (token, errorModel) = await LoadUsableVerifyTokenAsync(id);
        if (token is null)
            return View(errorModel ?? new VerifyViewModel { Id = id });

        if (!await AccountHasPasswordAsync(token.UserId))
        {
            // Child lane — the account has no password yet.
            if (!ModelState.IsValid)
            {
                await LocalizeValidationAsync(model);
                return View(new VerifyViewModel { RequiresPassword = true, Error = model.Error, Id = id });
            }

            // Set the child's own password + flip Verified + consume the token, one
            // Core commit (the ADR 0146 seam), then sign the resident in.
            try
            {
                var profile = await identity.VerifyAndSetPasswordAsync(token.Token, model.Password);
                return await SignInAfterVerifyAsync(
                    await userManager.FindByIdAsync(profile.SubjectId)
                        ?? throw new InvalidOperationException("Account not found."));
            }
            catch (InvalidOperationException ex)
            {
                return View(new VerifyViewModel { RequiresPassword = true, Error = ex.Message, Id = id });
            }
        }

        // Self-serve lane — the credential exists; confirm and sign in.
        return await ConfirmAndSignInAsync(token);
    }

    /// <summary>
    /// Whether the account for a subject id has a password set (the ADR 0146
    /// lane-decision read: true ⇒ self-serve lane / confirm-only; false ⇒
    /// child lane / collect the password at the link). A user lookup is
    /// required (the <see cref="UserManager{TUser}.HasPasswordAsync"/> seam
    /// takes a <c>User</c>, not an id), so this is a single EF read — cheap,
    /// and the same read the confirm / set-password branches will do.
    /// </summary>
    private async Task<bool> AccountHasPasswordAsync(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        return user is not null && await userManager.HasPasswordAsync(user);
    }

    /// <summary>
    /// Resolves the verification link to a usable single-use token, or (null, the
    /// error model) when the link is invalid/expired/consumed (the M1 error shape,
    /// unchanged). Shared by the GET and POST lanes so the two agree on the
    /// usable-token definition.
    /// </summary>
    private async Task<(IdentityToken? token, VerifyViewModel? errorModel)> LoadUsableVerifyTokenAsync(string id)
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var token = await session.LoadAsync<IdentityToken>(id);
        if (token is not null
            && token.Kind == IdentityToken.KindVerify
            && token.IsUsableAt(DateTimeOffset.UtcNow))
        {
            return (token, null);
        }

        return (null, new VerifyViewModel
        {
            Error = "This verification link is invalid, expired, or already used. " +
                    "Sign up again or ask an admin to verify your account."
        });
    }

    /// <summary>
    /// Confirms a verification token (self-serve lane, no credential write) and signs
    /// the resident in — the M1 handoff end, factored out of the former monolithic
    /// <c>Verify</c> action. The child lane calls the <c>VerifyAndSetPasswordAsync</c>
    /// seam + <see cref="SignInAfterVerifyAsync"/> itself instead.
    /// </summary>
    private async Task<IActionResult> ConfirmAndSignInAsync(IdentityToken token)
    {
        // VerifyWithTokenAsync flips Profile.Verified and consumes the token in one
        // Core transaction (audit row via:Owner).
        var profile = await identity.VerifyWithTokenAsync(token.Token);
        var user = await userManager.FindByIdAsync(profile.SubjectId)
            ?? throw new InvalidOperationException("Account not found.");

        return await SignInAfterVerifyAsync(user);
    }

    /// <summary>
    /// Mints the admissible claim set and signs the resident in with the persistent
    /// 14-day cookie every sign-in lane issues (the M1 "land signed-in" handoff end).
    /// Factored out so both the confirm-only and set-password lanes end the handoff
    /// the same way.
    /// </summary>
    private async Task<IActionResult> SignInAfterVerifyAsync(User user)
    {
        // The verification link is the handoff end — the resident should land signed-in,
        // so mint the cookie through the same factory step 6 uses at sign-in (the claim
        // set is the whole principal; no extra DB read on later requests).
        var factory = HttpContext.RequestServices.GetRequiredService<KumunitaClaimsPrincipalFactory>();
        var identityPrinciple = await factory.CreateAsync(user);

        // Mint the same persistent, 14-day cookie every sign-in lane issues: the handler
        // honors the explicit ExpiresUtc and, with sliding expiration on, refreshes the
        // ticket as the resident keeps the tab open. Without this, the handler falls
        // back to a session cookie that dies when the browser closes.
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

        // ADR 0050 — the sign-up gate (the `false` floor — a fresh instance ships
        // invitation-only): when closed, the view suppresses the "No account yet?
        // Sign up." affordance (a closed gate has no self-service signup surface
        // to point at).
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

        // Leg 1 — password check (the pin §5 leg 1). `isPersistent: false` so a
        // not-yet-MFA'd sign-in does not mint the long-lived cookie (the pin §5
        // leg 1: "the only change from today is `isPersistent: true` →
        // `isPersistent: false`"). The .NET 10.0.13 `PasswordSignInAsync` calls
        // `SignInOrTwoFactorAsync` after a successful password check, which
        // returns `SignInResult.TwoFactorRequired` (Succeeded == false,
        // RequiresTwoFactor == true) when MFA is required — so we must check
        // `RequiresTwoFactor` BEFORE `Succeeded` (a `TwoFactorRequired` result
        // is NOT a failure). For the non-MFA path (leg 1.5c, the
        // NonAdmin_MfaOptional guarantee), the sign-in still completes — the
        // cookie just won't be long-lived, which is functionally equivalent
        // (the cookie's lifetime is set by the handler's ExpireTimeSpan, not
        // by the IsPersistent flag, for the non-MFA case).
        var result = await signInManager.PasswordSignInAsync(
            user, model.Password, isPersistent: false, lockoutOnFailure: true);

        // Leg 1.5 — MFA gate (the pin §5 leg 1.5). `result.RequiresTwoFactor`
        // is the .NET 10 two-factor gate (① round-2 reflection-verified — the
        // property on `SignInResult` is present in 10.0.13). The .NET 10.0.13
        // `SignInResult` states are:
        //   · wrong password (any MFA state)        → Succeeded=false, RequiresTwoFactor=false
        //   · correct password + MFA enabled        → Succeeded=false, RequiresTwoFactor=true  (TwoFactorRequired)
        //   · correct password + MFA disabled       → Succeeded=true,  RequiresTwoFactor=false  (Success)
        // so `RequiresTwoFactor` is the authoritative challenge gate and is
        // ONLY set when the password was correct AND MFA is enabled
        // (`CheckPasswordSignInCoreAsync` returns `SignInResult.Failed` for a
        // wrong password before `SignInOrTwoFactorAsync` runs, so a wrong
        // password on an MFA account has `RequiresTwoFactor == false` and
        // falls through to the `Succeeded` check below — it does NOT get the
        // challenge). The pin §5's "fallback" (`IsTwoFactorEnabledAsync`) is a
        // compile-time alternative for if `RequiresTwoFactor` did not resolve;
        // it resolves (F3U2's `nameof(SignInResult.RequiresTwoFactor)` is
        // green), so it is not OR'd in here — OR-ing it would wrongly show the
        // challenge for a wrong password on an MFA account.
        if (result.RequiresTwoFactor)
        {
            // The account is MFA-protected and the password was correct.
            // Return the MFA challenge (a TOTP/recovery-code field on the
            // login view) — do NOT yet redirect to `ReturnUrl` (the pin §5
            // leg 1.5). The user is already in a temporary (non-persistent)
            // sign-in from leg 1 (the `TwoFactorUserIdScheme` cookie, which
            // `SignInOrTwoFactorAsync` mints), so the leg-2 methods can read
            // the pending user back (the pin §5 leg 1.5: "no separate
            // pending-user store is required"). Complete the sign-in at the
            // challenge leg (leg 2, `LoginMfa`).
            // Preserve the resident's email + RememberMe flag in the model so
            // the view can render the challenge form (the `MfaPending` flag
            // drives the view's branch; the `MfaCode` / `MfaRecoveryCode`
            // fields are the challenge inputs, empty here — the resident
            // fills them in).
            model.MfaPending = true;
            model.Email = user.Email ?? user.UserName ?? model.Email;
            model.MfaCode = null;
            model.MfaRecoveryCode = null;
            return View(model);
        }

        // A real sign-in failure (wrong password, lockout, not-allowed). The
        // `TwoFactorRequired` case was handled above; if `Succeeded` is
        // `false` here, it is a genuine failure — fail as today (the existing
        // `AccountController` error path, the pin §5 leg 1 "If Succeeded is
        // false → fail as today").
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty,
                result.IsLockedOut
                    ? "Account is temporarily locked. Try again in a few minutes."
                    : "Email or password is incorrect.");
            return View(model);
        }

        // Leg 1.5b — the "Enforce for GlobalAdmin" requirement (the pin §5
        // leg 1.5b). Reached only on a successful password check (
        // `Succeeded == true`) for a non-MFA account. A `GlobalAdmin` who has
        // NOT enrolled yet (i.e. `TwoFactorEnabled == false`) is redirected to
        // the MFA enrollment surface instead of proceeding — they cannot reach
        // the admin surface with password-only (the pin §5 leg 1.5b: "a
        // GlobalAdmin with TwoFactorEnabled == false is redirected to
        // /settings/security/mfa (enroll) instead of proceeding"). "U4 must
        // complete the sign-in for this user only after they enroll" — after
        // enrolling (the enrollment POST flips `SetTwoFactorEnabledAsync
        // (user, true)` + issues recovery codes via F3.U3's `MfaEnable`
        // action) the resident re-signs-in and leg 1.5 now returns
        // `TwoFactorRequired`, completing via the leg-2 challenge.
        // ADR 0194 — the sample-admin exemption: a sample account (a member of
        // the closed SampleAccountEmails set, on a SampleData__Enabled
        // instance) is EXEMPT from this forced enrollment. Without this
        // exemption, a locked demo instance (the admin has locked MFA for the
        // sample accounts so a visitor can't break the shared credential)
        // would redirect the sample GlobalAdmin to /settings/security/mfa on
        // every sign-in — but that page is itself locked for the sample
        // accounts (IsMfaLockedForAsync), so the admin could never sign in
        // with the shared password. The exemption reads the two-conjunct
        // IsSampleDataAccountAsync seam (conjuncts 1+3 only — the admin-toggle
        // gate is NOT consulted, so a locked instance still lets its admin
        // through; the GlobalAdmin check is NOT applied either, since the
        // exemption the login path needs is *precisely for* the sample
        // GlobalAdmin). A real (non-sample) account and the non-sample
        // GlobalAdmin are unaffected — this leg still forces them to enroll.
        // Use `UserManager.GetRolesAsync` (the repo's verified pattern —
        // `AccountController.cs:281`) + `Kumunita.Core.Identity.Roles.GlobalAdmin`
        // (NOT `ClaimsPrincipal.IsInRole`, which does not see the custom
        // `Kumunita.Role` claim type).
        var userRoles = await userManager.GetRolesAsync(user);
        if (userRoles.Contains(Kumunita.Core.Identity.Roles.GlobalAdmin)
            && !await signInManager.IsTwoFactorEnabledAsync(user)
            && !(user.Id is not null && await identity.IsSampleDataAccountAsync(user.Id)))
        {
            // **Do NOT sign out here.** The `SecuritySettingsController`
            // (F3.U3) is `[Authorize]`, so the resident must be signed in to
            // reach the enrollment page. This branch is only reached when the
            // password was correct (`Succeeded == true`, above), and an
            // unenrolled account's leg-1 `PasswordSignInAsync` mints the app
            // cookie (MFA is off, so `SignInOrTwoFactorAsync` takes the
            // non-MFA branch → `SignInWithClaimsAsync`), so the resident IS
            // signed in — the redirect to enrollment works. Signing out would
            // break the `[Authorize]` (auth challenge → redirect back to
            // /Account/Login → loop).
            return RedirectToAction("Mfa", "SecuritySettings");
        }

        // Leg 1.5c — the existing path for non-MFA users, UNCHANGED from today
        // (the pin §5 leg 1.5c, the NonAdmin_MfaOptional guarantee). The
        // **persistent** sign-in — `PasswordSignInAsync(user, model.Password,
        // isPersistent: true, lockoutOnFailure: true)` (the pin §5 leg 1.5c:
        // "the persistent sign-in … the current code's exact shape"; the app
        // "Always mint[s] a persistent, long-lived cookie") — is called here
        // (NOT in leg 1, which used `isPersistent: false` so the not-yet-MFA'd
        // sign-in does not mint the long-lived cookie). This re-verifies the
        // password (already verified at leg 1, so this succeeds) and re-mints
        // the app cookie as **persistent** (overwriting the non-persistent
        // cookie leg 1 minted), preserving the pre-F3.U4 behavior for
        // non-MFA users exactly. Then the `profile.Blocked` check + the
        // `ReturnUrl` redirect stay exactly as today.
        await signInManager.PasswordSignInAsync(
            user, model.Password, isPersistent: true, lockoutOnFailure: true);

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

    // ── MFA challenge leg (F3.U4, the pin §5 leg 2) ──────────────────────────────────
    //
    // The pin §4 routes table: `POST /account/login/mfa` → `LoginMfa
    // (MfaChallengeViewModel)`. The action accepts a TOTP **or** a recovery
    // code for the pending (temporarily signed-in) user, then completes the
    // **persistent** sign-in via `SignInManager.TwoFactorAuthenticatorSignIn
    // Async(code, isPersistent, rememberClient)` / `TwoFactorRecoveryCode
    // SignInAsync(recoveryCode)` (both ③ U2-build-verifies against the
    // round-3 shape — F3U2_TokenStoreApiCompileCheckTests.cs proved these
    // names compile against `Microsoft.AspNetCore.Identity 10.0.13`).
    //
    // **Leg-2 return types (the pin §5 leg 2: "if a build shows they do NOT
    // auto-mint, U4 adds a `HandleSignInAsync` follow-up and records it"):**
    // the compiler resolves both methods to `Task<SignInResult>` (the
    // `result.Succeeded` branch below compiles green — `SignInResult` is the
    // return type, NOT `bool` or `IdentityResult`). The .NET 10.0.13 source
    // confirms both call `DoTwoFactorSignInAsync` → `SignInWithClaimsAsync`
    // (which mints the app cookie) on success; no `HandleSignInAsync`
    // follow-up is needed (recorded in the handoff note).
    //
    // **Model note:** the action binds to `LoginViewModel` (the same model the
    // `Login` action uses, the pin §5 leg 1.5 "a code/recovery-code field on
    // the login view") — NOT a separate `MfaChallengeViewModel`. The
    // `MfaCode` / `MfaRecoveryCode` fields (added to `LoginViewModel` by this
    // unit) carry the TOTP / recovery-code inputs; `MfaPending` + `Email` +
    // `ReturnUrl` + `RememberMe` are carried through for the error re-render.
    //
    // **URL note (F3.U4 refinement of the pin §4 table):** the pin's §4 table
    // names `POST /account/login/mfa`, but `AccountController` is routed by
    // the conventional `{controller=Home}/{action=Index}/{id?}` route
    // (Program.cs:789) — under that route this action is reachable at
    // `POST /Account/LoginMfa` (NOT `/account/login/mfa`). This is consistent
    // with the repo's other `AccountController` actions (`/Account/Login`,
    // `/Account/Signup`, `/Account/Logout`) and the pin §5's "the enforcement
    // seam is `AccountController.Login` POST" — the challenge leg lives in
    // the same controller and is reachable by the same convention. The view
    // form posts to `asp-action="LoginMfa"` (the conventional URL), not the
    // pin's literal `/account/login/mfa` path. The pin's URL is the
    // *semantic* name for the leg; the actual URL is the conventional one.
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> LoginMfa(LoginViewModel model)
    {
        // F5 (AUTH-04, Option B) — the leg-2 revocation gate (pin §1d / §2
        // "leg-2 gating"). A kill-all (or password change) that rotated the
        // security stamp AFTER leg-1 minted the pending
        // `Identity.TwoFactorUserId` cookie leaves that cookie's stamp
        // (stamped at leg-1 by the `OnSigningIn` handler in `Program.cs`)
        // no longer matching the DB's current stamp. Without this gate the
        // resident would complete leg-2 with a valid TOTP / recovery code and
        // mint a FRESH app cookie after the kill — the half-signed-in state
        // the F5 pin explicitly forbids (§1d: "the half-signed-in window F5
        // must not let a kill-all leave dangling").
        //
        // The check reads the pending cookie's `Properties.Items["kumunita.stamp"]`
        // (the per-session carrier, NOT a claim type — invariant-set-B intact)
        // and compares it to the DB's current `SecurityStamp` (via
        // `UserManager.GetSecurityStampAsync` — the framework's own
        // `ValidateSecurityStampAsync` comparison, inlined here so the gate
        // can sign out and re-render the challenge rather than returning a
        // bare `SignInResult.Failed`). A mismatch → sign out (clears BOTH the
        // pending `Identity.TwoFactorUserId` cookie AND the app scheme, the
        // same `SignInManager.SignOutAsync` shape the `!result.Succeeded`
        // branch below uses) + re-render the challenge with a revocation
        // message. The resident must re-do leg-1 (password) to get a fresh
        // pending cookie with the current stamp.
        //
        // If the pending cookie carries no stamp (e.g. leg-1 was minted
        // before F5 shipped) the gate is a no-op — the resident's legitimate
        // leg-2 completion is not blocked (backward compat, the same rule as
        // `RevocationStampMiddleware`).
        // Resolve IAuthenticationService explicitly (not via the AuthenticateAsync
        // extension, which calls GetRequiredService and throws in test harnesses
        // that use a DefaultHttpContext without authentication services registered).
        // In production this is always available (AddAuthentication registers it).
        // If it's absent (unit-test harness), the gate is a no-op — the test does
        // not exercise the authentication pipeline.
        if (HttpContext.RequestServices is not null
            && HttpContext.RequestServices.GetService<IAuthenticationService>() is not null)
        {
            var pendingAuth = await HttpContext.AuthenticateAsync(IdentityConstants.TwoFactorUserIdScheme);
            if (pendingAuth.Succeeded
                && pendingAuth.Principal?.Identity?.Name is string pendingUserId
                && pendingAuth.Properties?.Items.TryGetValue("kumunita.stamp", out var pendingStampObj) == true
                && pendingStampObj is string pendingStamp)
            {
                var pendingUser = await userManager.FindByIdAsync(pendingUserId);
                if (pendingUser is not null)
                {
                    var currentStamp = await userManager.GetSecurityStampAsync(pendingUser);
                    if (pendingStamp != currentStamp)
                    {
                        // A kill-all / password change rotated the stamp after leg-1.
                        // Reject the pending cookie and force the resident back to
                        // leg-1 (password) so a fresh pending cookie is minted with
                        // the current stamp.
                        await signInManager.SignOutAsync();
                        ModelState.AddModelError(string.Empty,
                            "This sign-in was revoked. Please enter your password again.");
                        model.MfaPending = true;
                        return View("Login", model);
                    }
                }
            }
        }

        // The pin §5 leg 2: at least one of `MfaCode` (TOTP) or
        // `MfaRecoveryCode` (the pin §6 escape) must be present.
        if (string.IsNullOrWhiteSpace(model.MfaCode)
            && string.IsNullOrWhiteSpace(model.MfaRecoveryCode))
        {
            ModelState.AddModelError(string.Empty,
                "Enter a code from your authenticator app, or a recovery code.");
            model.MfaPending = true;
            return View("Login", model);
        }

        // Leg 2 — complete the sign-in. The user comes from the pending temp
        // sign-in (leg 1, `isPersistent: false`), stored in the
        // `TwoFactorUserIdScheme` cookie by `SignInOrTwoFactorAsync` (the
        // .NET 10.0.13 source at `SignInOrTwoFactorAsync` — `
        // Context.SignInAsync(IdentityConstants.TwoFactorUserIdScheme,
        // StoreTwoFactorInfo(userId, loginProvider))`), so the leg-2 methods
        // take NO `user` arg (they call `RetrieveTwoFactorInfoAsync` to read
        // it back — the pin §5 leg 2, both ③ round-3 verified).
        // `TwoFactorAuthenticatorSignInAsync` (TOTP) and
        // `TwoFactorRecoveryCodeSignInAsync` (recovery code) each return
        // `Microsoft.AspNetCore.Identity.SignInResult` (the
        // compiler-verified shape — the `result.Succeeded` branch below is the
        // proof; fully-qualified to disambiguate from
        // `Microsoft.AspNetCore.Mvc.SignInResult`, which is also in scope in
        // the Web project). The .NET 10.0.13 source confirms both call
        // `DoTwoFactorSignInAsync` → `SignInWithClaimsAsync` (which mints the
        // app cookie) on success; no `HandleSignInAsync` follow-up is needed
        // (recorded in the handoff note — the pin §5 leg 2 "do not pre-commit
        // to `HandleSignInAsync`").
        Microsoft.AspNetCore.Identity.SignInResult result;
        if (!string.IsNullOrWhiteSpace(model.MfaRecoveryCode))
        {
            // Recovery code (the pin §6 escape — presented **once**;
            // `TwoFactorRecoveryCodeSignInAsync` redeems it under the hood via
            // `UserManager.RedeemTwoFactorRecoveryCodeAsync`, dropping the
            // count by 1 — the F3.U5 test `GlobalAdminRecoveryCode_WorksOnce`
            // asserts this). 1-arg, NO user (③ round-3 verified).
            // Note: the .NET 10.0.13 source at
            // `TwoFactorRecoveryCodeSignInCoreAsync` calls
            // `DoTwoFactorSignInAsync(user, info, isPersistent: false,
            // rememberClient: false)` — the recovery path is
            // always non-persistent + no remember-client (the `RememberMe`
            // flag does not apply to the recovery-code path).
            result = await signInManager.TwoFactorRecoveryCodeSignInAsync(
                model.MfaRecoveryCode.Trim());
        }
        else
        {
            // TOTP code (RFC 6238). Code-first, NO user (③ round-3 verified).
            // `isPersistent` + `rememberClient` take the `RememberMe` flag the
            // resident chose on the password form (the pin §5 leg 2:
            // `TwoFactorAuthenticatorSignInAsync(code, isPersistent,
            // rememberClient)`).
            result = await signInManager.TwoFactorAuthenticatorSignInAsync(
                model.MfaCode!.Trim(),
                isPersistent: model.RememberMe,
                rememberClient: model.RememberMe);
        }

        if (!result.Succeeded)
        {
            // ADR 0214 — a wrong code used to SignOutAsync here, which discards the
            // pending `TwoFactorUserId` cookie: the challenge re-rendered with
            // "Try again", but the retry could never succeed (no pending sign-in
            // left), so every typo forced the resident back to the password leg.
            // Identity already counts each failed TOTP / recovery code towards
            // lockout (AccessFailedAsync inside the leg-2 methods), so the pending
            // cookie is kept for another try and only a lockout ends the challenge.
            if (result.IsLockedOut)
            {
                await signInManager.SignOutAsync();
                ModelState.AddModelError(string.Empty,
                    "Too many incorrect codes. This account is temporarily locked; try again later.");
                model.MfaPending = false;
                model.MfaCode = null;
                model.MfaRecoveryCode = null;
                ModelState.Remove(nameof(model.MfaCode));
                ModelState.Remove(nameof(model.MfaRecoveryCode));
                return View("Login", model);
            }

            ModelState.AddModelError(string.Empty,
                "The code was not valid. Try again, or use a recovery code.");
            // Re-render with the wrong code cleared (the tag helper prefers ModelState
            // over the model, so the ModelState entries are removed too).
            model.MfaCode = null;
            model.MfaRecoveryCode = null;
            ModelState.Remove(nameof(model.MfaCode));
            ModelState.Remove(nameof(model.MfaRecoveryCode));
            model.MfaPending = true;
            return View("Login", model);
        }

        // Complete the sign-in and redirect to `ReturnUrl` (the pin §5 leg 2:
        // "complete the sign-in and mint the auth cookie on success"). The
        // cookie was minted by the leg-2 sign-in method above (via
        // `DoTwoFactorSignInAsync` → `SignInWithClaimsAsync`).
        return Url.IsLocalUrl(model.ReturnUrl)
            ? Redirect(model.ReturnUrl)
            : Redirect("/profile/edit");
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
