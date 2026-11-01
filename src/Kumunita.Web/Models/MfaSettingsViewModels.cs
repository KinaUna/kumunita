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

    /// <summary>Whether the resident's self-serve <b>change-password</b> form
    /// (ADR 0138) is locked for this account — a non-admin sample account on an
    /// opted-in demo instance (<see cref="Kumunita.Core.Identity.IIdentityService.IsChangePasswordLockedForAsync"/>).
    /// The change-password surface now lives on this Security page (co-located
    /// with MFA): <c>true</c> renders the "password changes are locked" notice
    /// in place of the form, <c>false</c> (the floor, and every real /
    /// non-sample account) renders the form. The <c>_PasswordChange</c> partial
    /// reads this flag (typed <c>bool</c>) to decide which to render.</summary>
    public bool PasswordChangeLocked { get; init; }
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
/// convention). It also carries <see cref="PasswordChangeLocked"/> so the
/// co-located change-password section (the ADR 0138 surface, now on this
/// Security page) can render its form or "password changes are locked" notice.
/// </summary>
public sealed class MfaLockedViewModel
{
    // The change-password section is rendered on this locked-MFA page too (so a
    // resident who is MFA-locked but not password-locked keeps their change-
    // password lane — the ADR 0138 surface, now co-located with MFA on the
    // Security tab). The _PasswordChange partial reads this flag (the same
    // ADR 0138 decision as the MfaSettingsViewModel one) to decide whether to
    // render the form or the "password changes are locked" notice.
    public bool PasswordChangeLocked { get; init; }
}
