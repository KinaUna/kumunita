using System.ComponentModel.DataAnnotations;

namespace Kumunita.Web.Models;

/// <summary>
/// The <b>verification handoff</b> surface (M1, extended by ADR 0146). One model
/// serves both the self-serve lane (the link confirms the account, no password
/// field — the credential was set at signup) and the <b>child-account lane</b>
/// (the guardian created the account without holding the child's credential —
/// ADR 0028 "supervision rides the link, not the password" — so the child sets
/// <b>their own</b> password here, at the confirmation link).
/// <para>
/// <see cref="RequiresPassword"/> is the lane flag: <c>true</c> ⇒ the view
/// renders the <see cref="Password"/> / <see cref="ConfirmPassword"/> form and
/// the POST activates via <see cref="Kumunita.Core.Identity.IIdentityService
/// .VerifyAndSetPasswordAsync"/>; <c>false</c> ⇒ the classic confirm-only
/// surface (the POST activates via <see cref="Kumunita.Core.Identity
/// .IIdentityService.VerifyWithTokenAsync"/>). <see cref="Error"/> is non-null
/// when the link could not be used (invalid/expired/consumed).
/// </para>
/// </summary>
public sealed class VerifyViewModel
{
    /// <summary>
    /// The verification token row id (the <c>?id=</c> query value) — round-tripped
    /// so the set-password form can POST back to the same link (ADR 0146).
    /// </summary>
    public string? Id { get; set; }

    /// <summary>Non-null when the link could not be used (invalid/expired/consumed).</summary>
    public string? Error { get; set; }

    /// <summary>
    /// ADR 0146 — true when this account was created without a password (the
    /// child lane); the confirmation surface then collects the child's own
    /// password before activating the account.
    /// </summary>
    public bool RequiresPassword { get; set; }

    [Required, DataType(DataType.Password), MinLength(8)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password), Compare(nameof(Password))]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
