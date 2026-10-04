using System.ComponentModel.DataAnnotations;

namespace Kumunita.Web.Models;

/// <summary>
/// The resident's self-serve <b>change password</b> form (ADR 0138) — the
/// new / confirm pair the <c>POST /account/password</c> lane binds. The
/// <b>current</b> password is verified server-side against the account before
/// the write (the self-serve lane confirms it is really this resident changing
/// their own credential); it is not persisted. The subject is always the
/// signed-in principal minted server-side (never a path param).
/// </summary>
public sealed class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password)]
    [Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), MinLength(8)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password), Compare(nameof(NewPassword))]
    [Display(Name = "Confirm new password")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

/// <summary>
/// The <b>locked</b> notice shown to a sample account on a
/// <c>SampleData__Enabled</c> instance that has opted in to the lock (ADR
/// 0138) — the change-password form is replaced by this static notice (no
/// form, no write). The resident can still use every other feature; only the
/// self-serve password change is denied (so a demo visitor can't break the
/// shared credentials for the everyone else who signs in with the sample
/// account). The <c>LinkToLogin</c> / back-to-profile affordance is rendered
/// by the view.
/// </summary>
public sealed class ChangePasswordLockedViewModel
{
    // No bindable fields: the surface is a notice, not a form. The model exists
    // so the view has an explicit @model contract (the repo's Razor convention,
    // the SignupClosedViewModel shape).
}

/// <summary>
/// The GlobalAdmin's <b>/admin/sample</b> toggle (ADR 0138) — whether the
/// instance's sample (demo) accounts are locked out of changing their own
/// password (except the sample <c>GlobalAdmin</c>). The page is only reachable
/// on a <c>SampleData__Enabled</c> instance (the controller 404s otherwise —
/// a real deployment never carries the flag, so the surface is unreachable by
/// construction, the ADR 0056 shape). Mirrors the
/// <see cref="Kumunita.Web.Controllers.AdminSignupController.SignupAdminViewModel"/>
/// singleton-toggle shape (one admin-settled instance value, one audited write
/// lane).
/// </summary>
public sealed class SampleAdminViewModel
{
    /// <summary>Whether the lock is currently on
    /// (<see cref="Kumunita.Core.Identity.IIdentityService.IsSamplePasswordChangeLockedAsync"/>,
    /// the <c>false</c> floor).</summary>
    public bool PasswordChangeLocked { get; init; }
}
