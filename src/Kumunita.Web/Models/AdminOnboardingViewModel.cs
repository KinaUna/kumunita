namespace Kumunita.Web.Models;

/// <summary>
/// M30 (ADR 0153) — the read-only model for the <c>GET /admin/onboarding</c>
/// guided walk-through. <b>Admin-scope</b> only (the signed-in
/// <see cref="Kumunita.Core.Identity.Roles.GlobalAdmin"/> reads the
/// instance-wide completion state); no audience, no decision, no
/// <c>AccessAudit</c> row (the
/// <see cref="Kumunita.Core.AdminOnboarding.IAdminOnboardingService.GetAsync"/>
/// read re-projected, M30·3). The completion-state read is the
/// <see cref="Completed"/> value; the seven step cards are the closed set
/// (M30·7, the design doc §2.3 + the register's §"closed <c>kw-l</c> key
/// set" table).
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
/// **The <see cref="OnboardingStep"/> record is the closed seven-step set** (M30·7):
/// the seven <c>(Key, LabelKey, Route, DescriptionKey)</c> tuples in the
/// README / <c>Milestones.cs</c> order — community name →
/// <c>/admin/communities</c> (the community's base name + description,
/// ADR 0062; distinct from step 2's translations surface), languages →
/// <c>/admin/languages</c>, moderation
/// → <c>/admin/announcements/comments</c>, notifications →
/// <c>/admin/quiet</c>, storage limits → <c>/admin/storage/settings</c>,
/// site content → <c>/admin/site</c>, issue escalation →
/// <c>/admin/error-reports</c> (the M32 surface — the M30
/// <c>/admin/announcements</c> placeholder's known deferral is now resolved:
/// M32 "issue submission & escalation" shipped (ADR 0155) and U06 re-pointed
/// this step's route, M32·11). The walk-through is **links-only** (M30·7):
/// it does **not**
/// inline a control, does **not** add a per-step POST, does **not** persist
/// a step cursor, does **not** gate the admin surface on completion.
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
    /// The closed seven-step set (M30·7, the design doc §2.3 table). The
    /// Razor view iterates this list in order; each <see cref="OnboardingStep"/>
    /// links into the existing admin surface that already owns the setting (the
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
    /// The closed seven-step set (M30·7, the design doc §2.3 table, the
    /// register's §"closed <c>kw-l</c> key set" table) — the seven
    /// <see cref="OnboardingStep"/> records in the README / <c>Milestones.cs</c> order:
    /// community name → <c>/admin/communities</c>, languages →
    /// <c>/admin/languages</c>, moderation → <c>/admin/announcements/comments</c>,
    /// notifications → <c>/admin/quiet</c>, storage limits →
    /// <c>/admin/storage/settings</c>, site content → <c>/admin/site</c>,
    /// issue escalation → <c>/admin/error-reports</c> (the M32 surface —
    /// the M30 <c>/admin/announcements</c> placeholder's known deferral is
    /// now resolved, M32·11, ADR 0155).
    /// **Frozen** — the set is the ceiling (the ADR 0153 D1 pin); a future
    /// lane **adds** steps (additive per ADR 0004 §B.1, if the walk-through
    /// ever grows a per-step state), it does **not** re-shape the existing
    /// seven. The <c>LabelKey</c> + <c>DescriptionKey</c> are the
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
        // 3. Moderation → the announcement-comment moderation toggle (the
        //    ADR 0101 shape — the closest shipped "moderation" surface, the
        //    register's step 3).
        new OnboardingStep(
            Key:            "moderation",
            LabelKey:       "adminonboarding.step_moderation",
            Route:          "/admin/announcements/comments",
            DescriptionKey: "adminonboarding.desc_moderation"),
        // 4. Notifications → the notification-flush cadence the admin sets
        //    for held notification emails (the ADR 0121 M20 surface — the
        //    register's step 4).
        new OnboardingStep(
            Key:            "notifications",
            LabelKey:       "adminonboarding.step_notifications",
            Route:          "/admin/quiet",
            DescriptionKey: "adminonboarding.desc_notifications"),
        // 5. Storage limits → the admin-set per-file size limit + the
        //    per-user total content quota (the ADR 0135 M25 surface — the
        //    register's step 5).
        new OnboardingStep(
            Key:            "storage",
            LabelKey:       "adminonboarding.step_storage",
            Route:          "/admin/storage/settings",
            DescriptionKey: "adminonboarding.desc_storage"),
        // 6. Site content → the landing surfaces' hero text + the show/hide
        //    toggles (the ADR 0150 SITE lane — the register's step 6).
        new OnboardingStep(
            Key:            "sitecontent",
            LabelKey:       "adminonboarding.step_sitecontent",
            Route:          "/admin/site",
            DescriptionKey: "adminonboarding.desc_sitecontent"),
        // 7. Issue escalation → the /admin/error-reports M32 surface
        //    (M32·11 — the M30 placeholder's known deferral, now resolved:
        //    M32 "issue submission & escalation" shipped, ADR 0155, and
        //    U06 re-pointed this step's route from the M30
        //    `/admin/announcements` placeholder to the M32
        //    `/admin/error-reports` surface that owns issue escalation).
        //    The step-7 `Key` / `LabelKey` / `DescriptionKey` are **unchanged**
        //    — the `adminonboarding.step_escalation` keys are **reused**, not
        //    re-authored; the seven-step set is **unchanged** (the M30·7
        //    closed set).
        new OnboardingStep(
            Key:            "escalation",
            LabelKey:       "adminonboarding.step_escalation",
            Route:          "/admin/error-reports",
            DescriptionKey: "adminonboarding.desc_escalation"),
    ];
}
