namespace Kumunita.Web.Models;

/// <summary>
/// M22 (ADR 0132, D4/D5) — the read-only model for the <c>GET /onboarding</c>
/// guided walk-through. Owner-scope only (the signed-in resident reads their
/// own state); no audience, no decision, no <c>AccessAudit</c> row (the
/// <see cref="Kumunita.Core.UserInfo.IUserInfoService.GetProfileAsync"/> read
/// re-projected, D2). The completion-state read is the
/// <see cref="OnboardingCompletedAt"/> null/non-null value; the step "set /
/// not set" hints are off the frozen read seams (the design-doc §6 table, D3).
/// The model never writes — the only write is the controller's
/// <c>CompleteOnboardingAsync</c> (D2/D3).
/// </summary>
public sealed class OnboardingViewModel
{
    /// <summary>
    /// The completion stamp read (<c>null</c> = not completed; the floor: the
    /// banner shows, the nav entry is present). Written only by the
    /// owner-scope <see cref="Kumunita.Core.UserInfo.IUserInfoService.CompleteOnboardingAsync"/>
    /// lane; read through the owner-scope profile read (never a claim, D6).
    /// D5, C-M22·5.
    /// </summary>
    public DateTimeOffset? OnboardingCompletedAt { get; init; }

    /// <summary>
    /// The banner-eligibility read (D5, C-M22·5): <c>true</c> = the banner
    /// renders (the signed-in resident's <c>OnboardingCompletedAt</c> is
    /// <c>null</c>); <c>false</c> = the banner clears (non-null). Computed from
    /// the owner-scope read; never a claim (D6). Non-blocking — sign-in is
    /// never gated (D5).
    /// </summary>
    public bool BannerEligible => OnboardingCompletedAt is null;

    // ── Step "set / not set" hints (D4/D5, C-M22·5) — each is the non-empty
    // check on the frozen read seam the step links into (design-doc §6 table).
    // D8·2 defers the *recommendation engine*; M22 shows a derived hint + a
    // link into the owning surface. ──────────────────────────────────────────

    /// <summary>Step hint: a display name is set (links to <c>/profile/edit</c>).</summary>
    public bool HasDisplayName { get; init; }

    /// <summary>Step hint: an avatar is set (links to <c>/profile</c>).</summary>
    public bool HasAvatar { get; init; }

    /// <summary>Step hint: a time-zone override is set (links to <c>/settings/timezone</c>).</summary>
    public bool HasTimezone { get; init; }

    /// <summary>Step hint: a date-format override is set (links to <c>/settings/dateformat</c>).</summary>
    public bool HasDateFormat { get; init; }

    /// <summary>Step hint: an email/notification language is set (links to <c>/settings/language</c>).</summary>
    public bool HasEmailLanguage { get; init; }

    /// <summary>
    /// M9 amendment (ADR 0139) — step hint: the resident has opted in to direct
    /// messaging (<see cref="Kumunita.Core.UserInfo.Profile.MessagingOptIn"/>, the
    /// per-resident control). <c>true</c> ⇒ the "✓" hint (messaging is on for them);
    /// <c>false</c> (the default, the ADR 0105 privacy-sensitive default-<c>false</c>
    /// convention) ⇒ the neutral "–" hint. Links into <c>/settings/messaging</c>,
    /// which owns the <c>MessagingOptIn</c> write.
    /// </summary>
    public bool HasMessaging { get; init; }

    /// <summary>
    /// Step hint: the UI-language preference (the per-request
    /// <see cref="Kumunita.Web.Security.LocaleCookie"/> read — a plain BCP-47
    /// string, <b>not</b> a <c>Profile</c> field, the thin-token rule
    /// ADR 0001-B). <c>null</c> = no preference (the instance-default floor).
    /// </summary>
    public string? UiLanguageCode { get; init; }
}
