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

    /// <summary>
    /// The closed resident step set (D4/D5, C-M22·5) — the walk-through's
    /// steps in order, each an <see cref="OnboardingStep"/> pointing into the
    /// existing surface that already owns the setting (the M22 D3 "rides
    /// frozen lanes" pin — the walk-through <b>links into</b> the surface, it
    /// never re-implements a write, C-M22·4). The modal rework renders each
    /// step as a button that opens the step's <see cref="OnboardingStep.Route"/>
    /// in a shared <c>_OnboardingModal</c> (the M35 kiosk "frame the surface's
    /// own route" idiom — the surface owns its form + anti-forgery token + save
    /// lane), with previous / next navigation walking this list by index.
    /// <para>
    /// The per-step "set / not set" hint stays on the <c>Has*</c> members
    /// above (resident-scope, off the owner-scope read the step links into —
    /// the design-doc §6 table); it is not carried on the shared
    /// <see cref="OnboardingStep"/> record (the admin walk has no such hint).
    /// The <see cref="OnboardingStep.DescriptionKey"/> is empty for every
    /// resident step — the resident walk has never carried a per-step
    /// description (unlike the M30 admin walk), and the view renders the label
    /// only when the description is blank.
    /// </para>
    /// </summary>
    public IReadOnlyList<OnboardingStep> Steps { get; init; } = ClosedSteps;

    /// <summary>
    /// The closed resident step set (D4, the design-doc §6 table) — the
    /// <see cref="OnboardingStep"/> records in the walk-through's order, each
    /// linking into the surface that already owns the setting (the M22 D3
    /// "rides frozen lanes" pin; the <c>LabelKey</c> values are the closed
    /// <c>onboarding.step_*</c> kw-l set — the 16-key set the
    /// <c>Onboarding_KwL_Set_Is_Parity_Pinned_In_Four_Languages</c> pin names;
    /// the per-step "set" hint is the matching <c>Has*</c> member).
    /// <para>
    /// **Order (M9 amendment, ADR 0139):** the messaging step is the
    /// <b>last</b> step (added after the original seven). The view's previous /
    /// next navigation walks this list in order: display name → avatar →
    /// language → time zone → date format → email → contact → messaging.
    /// **Routes:** display name + contact → <c>/profile/edit</c>, avatar →
    /// <c>/profile/edit</c> (the avatar upload form lives on the editor —
    /// <c>/profile</c> is the read-only preview, which has no save lane to
    /// drive inside a modal), language + email → <c>/settings/language</c>,
    /// time zone → <c>/settings/timezone</c>, date format →
    /// <c>/settings/dateformat</c>, messaging → <c>/settings/messaging</c>.
    /// </para>
    /// </summary>
    public static IReadOnlyList<OnboardingStep> ClosedSteps { get; } =
    [
        // display name + contact details ride the /profile/edit editor
        // (IUserInfoService.UpsertProfileAsync, the F13 single-write surface).
        // The "set" hint is HasDisplayName (the owner-scope read).
        new OnboardingStep("displayname", "onboarding.step_displayname", "/profile/edit", ""),
        // avatar rides the avatar upload on the editor (POST /profile/avatar,
        // IUserInfoService.SetProfileAvatarAsync). The "set" hint is HasAvatar.
        // The route is /profile/edit (the page that carries the avatar upload
        // form — /profile is the read-only preview, with no save lane).
        new OnboardingStep("avatar", "onboarding.step_avatar", "/profile/edit", ""),
        // UI language rides the LocaleController language-cookie lane (a
        // per-request string, not a Profile field — the thin-token rule,
        // ADR 0001-B). The "set" hint is UiLanguageCode (non-blank).
        new OnboardingStep("language", "onboarding.step_language", "/settings/language", ""),
        // time zone rides IUserInfoService.SetProfileTimezoneAsync.
        // The "set" hint is HasTimezone.
        new OnboardingStep("timezone", "onboarding.step_timezone", "/settings/timezone", ""),
        // date & time format rides IUserInfoService.SetProfileDateFormatAsync.
        // The "set" hint is HasDateFormat.
        new OnboardingStep("dateformat", "onboarding.step_dateformat", "/settings/dateformat", ""),
        // email & notification language rides
        // IUserInfoService.SetProfileEmailLanguageAsync (the /settings/language
        // email section). The "set" hint is HasEmailLanguage.
        new OnboardingStep("email", "onboarding.step_email", "/settings/language", ""),
        // contact details + visibility ride the /profile/edit editor too
        // (UpsertProfileAsync owns contact + the audience visibility).
        // The "set" hint is HasDisplayName (contact lives on the same editor).
        new OnboardingStep("contact", "onboarding.step_contact", "/profile/edit", ""),
        // M9 amendment (ADR 0139) — the messaging opt-in step: links into
        // /settings/messaging (the page that owns Profile.MessagingOptIn).
        // The "set" hint is HasMessaging (the owner-scope opt-in read).
        new OnboardingStep("messaging", "onboarding.step_messaging", "/settings/messaging", ""),
    ];
}
