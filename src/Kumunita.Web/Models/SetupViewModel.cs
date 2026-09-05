using System.ComponentModel.DataAnnotations;

namespace Kumunita.Web.Models;

/// <summary>
/// The /admin/setup form (the one-time seed-admin handoff).
/// <para>
/// On first boot the seeder creates the GlobalAdmin account with no password hash
/// (the setup token is a <see cref="Kumunita.Core.Identity.IdentityToken.KindSetup"/>
/// row in the <c>mt</c> schema, not an ASP.NET Identity hash) and emails the operator
/// instructions to bring it to this page. Presenting the token + email here is the
/// only way to swap the token for a real password — from then on the account signs
/// in through the normal <c>/Login</c> form.
/// </para>
/// </summary>
public sealed class SetupViewModel
{
    [Required, EmailAddress]
    [Display(Name = "Seed-admin email")]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Text)]
    [Display(Name = "Setup token")]
    public string SetupToken { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), MinLength(8)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password), Compare(nameof(NewPassword))]
    [Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>
    /// Rendered only on a failed POST to explain <em>why</em> without leaking
    /// "the token is fine, your password is wrong" vs. "the token isn't valid".
    /// </summary>
    public string? Error { get; set; }
}
