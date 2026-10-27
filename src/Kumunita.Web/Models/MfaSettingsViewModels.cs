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

    /// <summary>A <c>data:image/png;base64,…</c> URI encoding the
    /// <see cref="OtpAuthUrl"/> as a QR code (ADR 0174), so the resident can
    /// pair their authenticator app by scanning an image. Server-side QR
    /// encode — no client JS required; works with scripts disabled.
    /// Shown **once**, at enrollment — never re-served by a subsequent GET.
    /// <c>null</c> when <see cref="OtpAuthUrl"/> is absent (the steady
    /// state / rotate leg), in which case the view renders no QR image.</summary>
    public string? QrDataUri { get; init; }

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

/// <summary>
/// The <b>locked</b> notice shown to a sample account on a
/// <c>SampleData__Enabled</c> instance that has opted in to the MFA lock
/// (ADR 0194, amending ADR 0138) — the MFA enrollment surface is replaced by
/// this static notice (no form, no write, no TOTP secret minted). The
/// resident can still use every other feature; only the MFA settings surface
/// is denied (so a demo visitor can't mint a TOTP secret or recovery codes
/// that break the shared credential for the everyone else who signs in with
/// the sample account). The <c>GlobalAdmin</c> sample admin and every real
/// (non-sample) account are unaffected (they keep their own MFA lane). The
/// back-to-settings affordance is rendered by the view. The model exists so
/// the view has an explicit <c>@model</c> contract (the repo's Razor
/// convention, the <see cref="ChangePasswordLockedViewModel"/> shape).
/// </summary>
public sealed class MfaLockedViewModel
{
    // No bindable fields: the surface is a notice, not a form. The model
    // exists so the view has an explicit @model contract (the ADR 0138
    // ChangePasswordLockedViewModel shape, re-applied to the MFA surface).
}
