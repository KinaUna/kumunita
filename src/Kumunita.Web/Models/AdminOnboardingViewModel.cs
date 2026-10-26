namespace Kumunita.Web.Models;

/// <summary>
/// M30 (ADR 0153) — the read-only model for the <c>GET /admin/onboarding</c>
/// guided walk-through. <b>Admin-scope</b> only (the signed-in
/// <see cref="Kumunita.Core.Identity.Roles.GlobalAdmin"/> reads the
/// instance-wide completion state); no audience, no decision, no
/// <c>AccessAudit</c> row (the
/// <see cref="Kumunita.Core.AdminOnboarding.IAdminOnboardingService.GetAsync"/>
/// read re-projected, M30·3). The completion-state read is the
/// <see cref="Completed"/> value; the ten step cards are the closed set
/// (M30·7, the design doc §2.3 + the register's §"closed <c>kw-l</c> key
/// set" table — the original seven M30 steps, extended by the additive
/// time-zone / date-format / sign-up steps per ADR 0153 D1).
/// <para>
/// **The model never writes** (M30·1, M30·6): the only write is the
/// controller's <see cref="Kumunita.Core.AdminOnboarding.IAdminOnboardingService.CompleteAsync"/>
/// (the M22 / SITE single-write-lane shape, admin-scope). The
/// <see cref="Completed"/> property is the
/// <see cref="Kumunita.Core.AdminOnboarding.IAdminOnboardingService.GetAsync"/>
/// read's inverse (<c>CompletedAt != null</c>) — the same seam the U05
/// banner partial reads (M30·5 — the banner + the view resolve to the same
/// value).
/// </para>
/// <para>
/// **The <see cref="OnboardingStep"/> record is the closed ten-step set** (M30·7,
/// extended additively per ADR 0153 D1): the ten <c>(Key, LabelKey, Route,
/// DescriptionKey)</c> tuples in the walk order — community name →
/// <c>/admin/communities</c> (the community's base name + description,
/// ADR 0062; distinct from step 2's translations surface), languages →
/// <c>/admin/languages</c>, time zone → <c>/admin/timezone</c> (ADR 0019,
/// additive), date &amp; time format → <c>/admin/dateformat</c> (ADR 0020,
/// additive), moderation → <c>/admin/announcements/comments</c>,
/// notifications → <c>/admin/quiet</c>, sign-up → <c>/admin/signup</c>
/// (ADR 0050, additive), storage limits → <c>/admin/storage/settings</c>,
/// site content → <c>/admin/site</c>, issue escalation →
/// <c>/admin/escalation</c> (the ESC outbound-config surface, ADR 0159 —
/// the M30 placeholder's known deferral was first re-pointed by M32·11 to
/// <c>/admin/error-reports</c> (the M32 "issue submission & escalation"
/// surface, ADR 0155), then re-pointed again to <c>/admin/escalation</c>
/// (the ADR 0159 ESC surface, which is the outbound config a new admin
/// actually sets up first). The walk-through is **links-only** (M30·7):
/// it does **not** inline a control, does **not** add a per-step POST,
/// does **not** persist a step cursor, does **not** gate the admin surface
/// on completion.
/// </para>
/// </summary>
public sealed class AdminOnboardingViewModel
{
    /// <summary>
    /// The completion stamp read (M30·3, the ADR 0153 D2 pin):
    /// <c>true</c> = <see cref="Kumunita.Core.AdminOnboarding.AdminOnboarding.CompletedAt"/>
    /// is non-null (the banner clears, the walk-through is still reachable);
    /// <c>false</c> = <see cref="Kumunita.Core.AdminOnboarding.AdminOnboarding.CompletedAt"/>
    /// is <c>null</c> (the banner shows, the floor). Computed from the
    /// <see cref="Kumunita.Core.AdminOnboarding.IAdminOnboardingService.GetAsync"/>
    /// read's inverse (<c>CompletedAt != null</c>) — the single read-seam the
    /// U05 banner partial + the U05 view call, so they resolve to the same
    /// value (M30·5). Never a claim (the thin-token rule, ADR 0001-B).
    /// </summary>
    public bool Completed { get; init; }

    /// <summary>
    /// The closed ten-step set (M30·7, the design doc §2.3 table, extended
    /// additively per ADR 0153 D1). The Razor view iterates this list in
    /// order; each <see cref="OnboardingStep"/> links into the existing admin
    /// surface that already owns the setting (the
    /// <see cref="OnboardingStep.Route"/> — the M22 D3 "rides frozen lanes" pin,
    /// admin-scope). **No inline control, no per-step POST, no persisted step
    /// cursor, no completion gate** (the M30·7 pin — the walk-through's
    /// *only* write is the controller's
    /// <see cref="Kumunita.Core.AdminOnboarding.IAdminOnboardingService.CompleteAsync"/>).
    /// <para>
    /// <see cref="OnboardingStep"/> is the <b>shared</b> record the M22 resident
    /// walk and this M30 admin walk both surface their steps through (the modal
    /// rework renders each step as a button that opens the step's Route in a
    /// modal, with previous / next navigation walking the step list by index).
    /// </para>
    /// </summary>
    public IReadOnlyList<OnboardingStep> Steps { get; init; } = ClosedSteps;

    /// <summary>
    /// The closed ten-step set (M30·7, the design doc §2.3 table, the
    /// register's §"closed <c>kw-l</c> key set" table — extended additively
    /// per ADR 0153 D1) — the ten <see cref="OnboardingStep"/> records in the
    /// walk order: community name → <c>/admin/communities</c>, languages →
    /// <c>/admin/languages</c>, time zone → <c>/admin/timezone</c>, date &amp;
    /// time format → <c>/admin/dateformat</c>, moderation →
    /// <c>/admin/announcements/comments</c>, notifications → <c>/admin/quiet</c>,
    /// sign-up → <c>/admin/signup</c>, storage limits →
    /// <c>/admin/storage/settings</c>, site content → <c>/admin/site</c>,
    /// issue escalation → <c>/admin/escalation</c> (the ESC outbound-config
    /// surface, ADR 0159 — the M30 placeholder's known deferral was first
    /// re-pointed by M32·11 to <c>/admin/error-reports</c> (the M32 "issue
    /// submission & escalation" surface, ADR 0155), then re-pointed again to
    /// <c>/admin/escalation</c> (the ADR 0159 ESC surface).
    /// **Frozen** — the set is the ceiling (the ADR 0153 D1 pin); a future
    /// lane **adds** steps (additive per ADR 0004 §B.1, if the walk-through
    /// ever grows a per-step state), it does **not** re-shape the existing
    /// ten. The <c>LabelKey</c> + <c>DescriptionKey</c> are the
    /// <c>adminonboarding.*</c> <c>kw-l</c> keys authored by U05 (M30·6 —
    /// the closed set parity-pinned in four languages, the M22 D7 "closed
    /// set" shape, admin-scope).
    /// </summary>
    public static IReadOnlyList<OnboardingStep> ClosedSteps { get; } =
    [
        // 1. Community name → the community's base name + description, edited
        //    on the /admin/communities surface (the AddCommunity / UpdateCommunity
        //    form, ADR 0062 — the register's step 1). Distinct from step 2,
        //    the languages surface (ADR 0026 community *translations*):
        //    the M30 route pointed at /admin/languages by mistake — that
        //    surface only owns the language catalog + UI-string translations,
        //    not the community's name/description, so the link was re-pointed
        //    to the surface that actually owns it.
        new OnboardingStep(
            Key:            "communityname",
            LabelKey:       "adminonboarding.step_communityname",
            Route:          "/admin/communities",
            DescriptionKey: "adminonboarding.desc_communityname"),
        // 2. Languages → the language catalog + the translated UI strings +
        //    the static pages (the ADR 0005 B / ADR 0044 surface — the
        //    register's step 2).
        new OnboardingStep(
            Key:            "languages",
            LabelKey:       "adminonboarding.step_languages",
            Route:          "/admin/languages",
            DescriptionKey: "adminonboarding.desc_languages"),
        // 3. Time zone → the platform-default time zone (ADR 0019, the
        //    /admin/timezone surface — the register's step 3; the M30 closed
        //    seven set is **extended** by this additive step per ADR 0153 D1:
        //    a future lane **adds** steps, it does **not** re-shape the
        //    existing seven). Grouped with step 2 (languages) + step 4
        //    (date & time format) as the platform-localization defaults a new
        //    admin settles before content.
        new OnboardingStep(
            Key:            "timezone",
            LabelKey:       "adminonboarding.step_timezone",
            Route:          "/admin/timezone",
            DescriptionKey: "adminonboarding.desc_timezone"),
        // 4. Date & time format → the platform-default date-time format
        //    (ADR 0020, the /admin/dateformat surface — the register's step
        //    4; additive per ADR 0153 D1, the ADR 0019 time zone's exact
        //    analog). Grouped with steps 2 + 3 as the platform-localization
        //    defaults.
        new OnboardingStep(
            Key:            "dateformat",
            LabelKey:       "adminonboarding.step_dateformat",
            Route:          "/admin/dateformat",
            DescriptionKey: "adminonboarding.desc_dateformat"),
        // 5. Moderation → the announcement-comment moderation toggle (the
        //    ADR 0101 shape — the closest shipped "moderation" surface, the
        //    register's step 3, re-numbered step 5 after the additive
        //    timezone + date-format steps above).
        new OnboardingStep(
            Key:            "moderation",
            LabelKey:       "adminonboarding.step_moderation",
            Route:          "/admin/announcements/comments",
            DescriptionKey: "adminonboarding.desc_moderation"),
        // 6. Notifications → the notification-flush cadence the admin sets
        //    for held notification emails (the ADR 0121 M20 surface — the
        //    register's step 4, re-numbered step 6).
        new OnboardingStep(
            Key:            "notifications",
            LabelKey:       "adminonboarding.step_notifications",
            Route:          "/admin/quiet",
            DescriptionKey: "adminonboarding.desc_notifications"),
        // 7. Sign-up → the open / invitation-only self-service sign-up gate
        //    (ADR 0050, the /admin/signup surface — the register's new step
        //    7; additive per ADR 0153 D1). A fresh instance ships
        //    invitation-only (the ADR 0050 amended <c>false</c> floor), so a
        //    new admin settles this gate before opening the community to new
        //    residents — placed between the notification cadence and the
        //    storage limits (the membership / gate settings before the
        //    content-quota settings).
        new OnboardingStep(
            Key:            "signup",
            LabelKey:       "adminonboarding.step_signup",
            Route:          "/admin/signup",
            DescriptionKey: "adminonboarding.desc_signup"),
        // 8. Storage limits → the admin-set per-file size limit + the
        //    per-user total content quota (the ADR 0135 M25 surface — the
        //    register's step 5, re-numbered step 8).
        new OnboardingStep(
            Key:            "storage",
            LabelKey:       "adminonboarding.step_storage",
            Route:          "/admin/storage/settings",
            DescriptionKey: "adminonboarding.desc_storage"),
        // 9. Site content → the landing surfaces' hero text + the show/hide
        //    toggles (the ADR 0150 SITE lane — the register's step 6,
        //    re-numbered step 9).
        new OnboardingStep(
            Key:            "sitecontent",
            LabelKey:       "adminonboarding.step_sitecontent",
            Route:          "/admin/site",
            DescriptionKey: "adminonboarding.desc_sitecontent"),
        // 10. Issue escalation → the /admin/escalation ESC outbound-config
        //    surface (ADR 0159, ESC·4 — the origin GlobalAdmin sets their
        //    receiving endpoint + token + enabled switch, the surface a new
        //    admin actually configures first). The M30 placeholder's known
        //    deferral was first re-pointed by M32·11 to /admin/error-reports
        //    (the M32 "issue submission & escalation" surface, ADR 0155), but
        //    that page is the *inbound* read-only triage list of resident
        //    reports — not the *outbound* escalation setup. ADR 0159 (ESC)
        //    added /admin/escalation (the outbound endpoint + token + enabled
        //    form, the ADR 0155 E-surface), which is the surface this step's
        //    "Issue escalation" label + "Where residents' issues are
        //    surfaced and escalated" description point at. The step's `Key`
        //    / `LabelKey` / `DescriptionKey` are **unchanged** — the
        //    `adminonboarding.step_escalation` keys are **reused**, not
        //    re-authored.
        new OnboardingStep(
            Key:            "escalation",
            LabelKey:       "adminonboarding.step_escalation",
            Route:          "/admin/escalation",
            DescriptionKey: "adminonboarding.desc_escalation"),
    ];
}
