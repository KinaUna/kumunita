using System.ComponentModel.DataAnnotations;

namespace Kumunita.Web.Models;

public sealed class SignupViewModel
{
    [Required, MaxLength(100)]
    [Display(Name = "Display name")]
    public string DisplayName { get; set; } = string.Empty;

    [Required, EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), MinLength(8)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password), Compare(nameof(Password))]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>
    /// True when signup failed because an account with this email already exists
    /// (unactivated) — the view shows the "resend confirmation email" affordance.
    /// </summary>
    public bool EmailAlreadyExists { get; set; }

    /// <summary>
    /// True when signup failed because the email's domain is in the instance's
    /// disposable-email blocklist (F9, AUTH-15) — the view shows the "please use a
    /// permanent email" notice (the <c>EmailAlreadyExists</c> precedent, :28).
    /// </summary>
    public bool EmailDomainDisallowed { get; set; }

    /// <summary>
    /// The F9 (AUTH-15) bot-challenge token (pin §4b) — the value the
    /// challenge widget (only rendered when the owner's
    /// <c>KUMUNITA__SIGNUP__BOT_CHECK</c> flag is on) writes into a hidden
    /// input this property binds. The <see cref="AccountController.Signup(SignupViewModel)"/>
    /// POST resolves <c>IBotChallengeVerifier</c> and calls
    /// <c>IsVerifiedAsync(BotChallengeToken)</c> before
    /// <see cref="Kumunita.Core.Identity.IIdentityService.RegisterAsync"/>.
    /// When the flag is off (the <c>NoopBotChallengeVerifier</c>) this is
    /// absent from the form and ignored — the form posts through unchanged
    /// (the standing owner deferral, OPS.md §10). No client binding
    /// validation: the value is server-verified against the provider
    /// (fail-closed), not shape-checked by a DataAnnotation.
    /// </summary>
    public string? BotChallengeToken { get; set; }

    /// <summary>
    /// True when signup failed because the bot-challenge token was missing or
    /// invalid **and the <c>KUMUNITA__SIGNUP__BOT_CHECK</c> flag is on** (F9,
    /// AUTH-15) — the view re-renders the form with the challenge again (the
    /// <c>EmailDomainDisallowed</c> precedent, :33; the <c>EmailAlreadyExists</c>
    /// notice shape, :28). Set by the <see cref="AccountController.Signup(SignupViewModel)"/>
    /// POST on a <c>false</c> from <c>IBotChallengeVerifier.IsVerifiedAsync</c>.
    /// Never set when the flag is off (the <c>NoopBotChallengeVerifier</c> always
    /// verifies — the deferral stands).
    /// </summary>
    public bool BotChallengeFailed { get; set; }

    /// <summary>
    /// The F9 (AUTH-15) flag for the **view**: <c>true</c> when the owner's
    /// <c>KUMUNITA__SIGNUP__BOT_CHECK</c> flag is on — the view then renders the
    /// challenge widget + the hidden <see cref="BotChallengeToken"/> input (and
    /// only then; when off the signup form is byte-identical to before this seam,
    /// pin §4b / the standing owner deferral, OPS.md §10). Set by the
    /// <see cref="AccountController.Signup()"/> GET from the flag.
    /// </summary>
    public bool BotChallengeEnabled { get; set; }

    /// <summary>
    /// The F9 (AUTH-15) owner's provider **site key** (the
    /// <c>KUMUNITA__SIGNUP__BOT_CHECK__SITE_KEY</c> value, an owner-ops open item
    /// — pin §4b), rendered as the widget's <c>data-sitekey</c> when
    /// <see cref="BotChallengeEnabled"/> is <c>true</c>. Blank when unset (the
    /// challenge then fails closed on the server — a bot is not proven human).
    /// </summary>
    public string? BotChallengeSiteKey { get; set; }

    /// <summary>
    /// The invitation token (IMPROVE-UX A08, ADR 0304 D4) — the value the
    /// <c>?invite=</c> query param (the GET) or a hidden form field (the POST)
    /// carries, set by the <see cref="AccountController.Signup()"/> GET and
    /// echoed by the <c>Signup.cshtml</c> hidden input. The token is a
    /// credential (ADR 0304 D2 — the 0159 / ADR 0302 hash-at-rest shape), so it
    /// is **not** a visible form field — only the <see cref="Email"/> is
    /// prefilled from it (the <c>AccountController.Signup</c> GET resolves the
    /// invitation via <c>IInvitationService.ValidatePresentedAsync</c> and
    /// sets <see cref="Email"/> from <c>Invitation.Email</c>). The POST
    /// verifies the signing-up email matches <c>Invitation.Email</c> (A07's
    /// <c>IInvitationService.AcceptAsync</c> single-address pin, ADR 0304 D4)
    /// and runs <c>IInvitationService.AcceptPresentedAsync</c> instead of
    /// <c>IIdentityService.RegisterAsync</c>. <see cref="InvitationActive"/>
    /// tells the view whether a valid token is attached (so the
    /// <c>SignupClosed</c> notice is not rendered and the prefilled email is
    /// shown).
    /// </summary>
    public string? InviteToken { get; set; }

    /// <summary>
    /// True when the <c>?invite=</c> token (IMPROVE-UX A08, ADR 0304 D4)
    /// resolved to a live <c>Invitation</c> row — the signup form renders
    /// (with <see cref="Email"/> prefilled to <c>Invitation.Email</c>)
    /// **instead of** the <c>SignupClosed</c> notice, and the POST runs
    /// <c>IInvitationService.AcceptPresentedAsync</c> (the
    /// <c>RegisterAsync</c> + <c>Accepted</c> mark + the
    /// <c>invitation.accept</c> audit row in one lane). Set by the
    /// <see cref="AccountController.Signup()"/> GET (and the
    /// <see cref="AccountController.Signup(SignupViewModel)"/> POST re-render
    /// path on a mismatch refusal — the token stays live for the retry) from
    /// a non-null <c>IInvitationService.ValidatePresentedAsync</c> result.
    /// </summary>
    public bool InvitationActive { get; set; }

    /// <summary>
    /// True when the <c>?invite=</c> token (IMPROVE-UX A08, ADR 0304 D4)
    /// resolved to a live <c>Invitation</c> row **but the signing-up
    /// <see cref="Email"/> does not match <c>Invitation.Email</c>** (A07's
    /// single-address pin, ADR 0304 D4) — the POST re-renders the signup form
    /// with the invited email prefilled again (so the resident can correct it)
    /// and a friendly notice, instead of a raw exception. Set by the
    /// <see cref="AccountController.Signup(SignupViewModel)"/> POST on the
    /// <c>AcceptPresentedAsync</c> email-match refusal (the
    /// <c>EmailDomainDisallowed</c> / <c>BotChallengeFailed</c> precedent —
    /// the re-render-with-flag shape). <see cref="InvitationActive"/> stays
    /// <c>true</c> (the token is still live — only the email was wrong).
    /// </summary>
    public bool InvitationMismatch { get; set; }
}

public sealed class ResendVerificationViewModel
{
    [Required, EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// The invitation-only notice shown when the admin sign-up gate (ADR 0050) is
/// closed — the self-service signup surface is replaced by this static notice
/// (no form, no write). The <c>LinkToLogin</c> affordance is rendered by the view.
/// </summary>
public sealed class SignupClosedViewModel
{
    // No bindable fields: the surface is a notice, not a form. The model exists
    // so the view has an explicit @model contract (the repo's Razor convention).
}
