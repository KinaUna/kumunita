using System.ComponentModel.DataAnnotations;

namespace Kumunita.Web.Models;

/// <summary>
/// The MFA enrollment surface view model (F3, AUTH-03). The pin's §4 shape:
/// the enable flag, the one-time setup key, the <c>otpauth://</c> URI, the
/// 10 recovery codes (shown **once**), and the remaining-code count (the
/// only thing a steady GET shows).
/// <para>
/// The **"shown once, then never stored again"** rule:
/// <see cref="SetupKey"/>, <see cref="OtpAuthUrl"/>, and
/// <see cref="RecoveryCodes"/> are returned only on the enabling /
/// rotating response; every steady GET reads
/// <see cref="UserManager{User}.CountRecoveryCodesAsync"/>
/// (<c>Microsoft.AspNetCore.Identity</c>) **solely as the count** and
/// never emits the values.
/// </para>
/// </summary>
public sealed class MfaSettingsViewModel
{
    /// <summary>Whether MFA is enabled
    /// (<c>UserManager.GetTwoFactorEnabledAsync(user)</c>).</summary>
    public bool Enabled { get; init; }

    /// <summary>The one-time setup key (the base32/Base64 key from
    /// <c>UserManager.GenerateNewAuthenticatorKey()</c>).
    /// Shown **once**, at enrollment — never re-served by a subsequent GET.</summary>
    public string? SetupKey { get; init; }

    /// <summary>The <c>otpauth://</c> URI built from <see cref="SetupKey"/>
    /// + <c>TokenOptions.AuthenticatorTokenProvider.AuthenticatorIssuer</c>.
    /// Shown **once**, at enrollment — never re-served by a subsequent GET.</summary>
    public string? OtpAuthUrl { get; init; }

    /// <summary>The 10 recovery codes from
    /// <c>UserManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10)</c>.
    /// Shown **once**, at enrollment/rotate — never re-served by a
    /// subsequent GET.</summary>
    public string[]? RecoveryCodes { get; init; }

    /// <summary>The count of remaining recovery codes
    /// (<c>UserManager.CountRecoveryCodesAsync(user)</c>).
    /// The only thing a steady GET shows.</summary>
    public int RemainingRecoveryCodes { get; init; }
}

/// <summary>
/// The MFA enable form model (F3, AUTH-03). Carries the single
/// <see cref="Code"/> field (the TOTP code the resident typed) +
/// anti-forgery (enforced by <c>[ValidateAntiForgeryToken]</c> on the
/// action, not by a model field).
/// </summary>
public sealed class MfaEnableViewModel
{
    /// <summary>The TOTP code the resident typed into their authenticator
    /// app.</summary>
    [Required, StringLength(10, MinimumLength = 6)]
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// The MFA disable form model (F3, AUTH-03). Carries the single
/// <see cref="Code"/> field (the TOTP code that serves as the §8.6
/// proof for a <c>GlobalAdmin</c> disabling their own second factor) +
/// anti-forgery.
/// </summary>
public sealed class MfaDisableViewModel
{
    /// <summary>The TOTP code the resident typed (the §8.6 proof).</summary>
    [Required, StringLength(10, MinimumLength = 6)]
    public string Code { get; set; } = string.Empty;
}
