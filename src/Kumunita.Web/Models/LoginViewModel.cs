using System.ComponentModel.DataAnnotations;

namespace Kumunita.Web.Models;

public sealed class LoginViewModel
{
    [Required, EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    // Label is rendered by the view via the account.login_remember key (ADR 0015
    // mechanism) — no [Display(Name)] to mirror.
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }

    /// <summary>Pre-populated error to display when arriving at the login page with a
    /// known reason (e.g. the blocked-account sign-out landing).</summary>
    public string? Error { get; set; }

    /// <summary>
    /// Whether to show the "Received a first-boot setup token? Complete setup." hint.
    /// Default true (safe fallback — the hint only appears if there happens to be no
    /// setup lane), set to false by the controller when
    /// <c>IIdentityService.IsFirstBootSetupCompleteAsync</c> reports the seed-admin
    /// setup token has already been consumed/expired.
    /// </summary>
    public bool ShowSetupLink { get; set; } = true;

    /// <summary>
    /// Whether self-service sign-up is open on this instance (ADR 0050). Drives the
    /// "No account yet? Sign up." affordance in the login view — when the admin gate
    /// is closed (invitation-only) there is no self-service signup surface to link
    /// to, so the view suppresses it. Default true (the safe floor — a fresh
    /// instance ships with sign-up open; the controller reads the authoritative
    /// gate).
    /// </summary>
    public bool SignupOpen { get; set; } = true;

    // ── F3.U4 (AUTH-03) — the MFA challenge leg (the pin §5 leg 1.5/leg 2) ──────────
    // When the password leg succeeds but the account is MFA-protected, the login
    // view renders the **challenge** form (a TOTP or recovery-code field) instead
    // of the password form. The pin §5 leg 1.5: "Return the MFA challenge (a
    // code/recovery-code field on the login view)". These three fields are the
    // minimal shape the pin §4 names (a single TOTP `Code` + an optional
    // `RecoveryCode`); the resident completes sign-in by POSTing the code to the
    // `LoginMfa` action (leg 2). `MfaPending` is `false` for the non-MFA path
    // (the `NonAdmin_MfaOptional` guarantee — the password form renders exactly
    // as today).
    /// <summary>
    /// Whether the account is MFA-protected and has passed the password leg but
    /// not yet the second-factor leg (F3.U4, the pin §5 leg 1.5). When
    /// <c>true</c>, <c>Login.cshtml</c> suppresses the password form and renders
    /// the MFA challenge form (a TOTP or recovery-code field) instead.
    /// Default <c>false</c> — the non-MFA path renders the password form exactly
    /// as today.
    /// </summary>
    public bool MfaPending { get; set; }

    /// <summary>
    /// The TOTP code the resident typed into their authenticator app (the pin §4
    /// `Code` field — RFC 6238). Optional when a <see cref="MfaRecoveryCode"/>
    /// is supplied. Bound by the challenge form in <c>Login.cshtml</c> and read
    /// by the <c>LoginMfa</c> action (the pin §5 leg 2).
    /// </summary>
    public string? MfaCode { get; set; }

    /// <summary>
    /// An optional one-time recovery code (the pin §6 escape — presented
    /// <b>once</b>; <c>SignInManager.TwoFactorRecoveryCodeSignInAsync</c> redeems
    /// it under the hood via <c>UserManager.RedeemTwoFactorRecoveryCodeAsync</c>,
    /// dropping the count by 1). Optional when a <see cref="MfaCode"/> is
    /// supplied.
    /// </summary>
    public string? MfaRecoveryCode { get; set; }
}
