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
