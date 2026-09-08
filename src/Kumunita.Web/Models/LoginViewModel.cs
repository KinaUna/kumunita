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

    [Display(Name = "Remember me")]
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
}
