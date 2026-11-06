using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;

namespace Kumunita.Core.UserInfo;

/// <summary>
/// The UserInfoModule's profile-settings surface (ADR 0286): timezone, page size,
/// first-day-of-week, home prefs, date format, email language, onboarding,
/// messaging opt-in, and directory/component reads.
/// Split from <see cref="IUserInfoService"/> (C07, IMPROVE-CODE E4).
/// </summary>

public interface IProfileSettingsService
{
    /// <summary>
    /// Point a profile's avatar at a media object (or clear it when `avatarId`
    /// is null; C-MED·8). The owner-scope check happens at the Web boundary;
    /// this lane writes `Profile.AvatarId` only.
    /// </summary>
    Task SetProfileAvatarAsync(string subjectId, string? avatarId, string actorBy);

    /// <summary>
    /// Set (or clear, with null) the resident's <see cref="Profile.TimeZone"/>
    /// IANA id — the user's <b>override</b> of the platform default (ADR 0019;
    /// the fallback is <see cref="Localization.LocaleSettings.DefaultTimezone"/>
    /// / the <c>UTC</c> floor). Mirrors <see cref="SetProfileAvatarAsync"/>
    /// exactly (the C-MED·8 single write-lane shape): the self-scope check
    /// happens at the Web boundary (the owner is the actor); this lane writes
    /// <c>Profile.TimeZone</c> only. One session, one <c>SaveChangesAsync</c>;
    /// no <see cref="Authorization.AccessAudit"/> row (a profile field write —
    /// the <see cref="UpsertProfileAsync"/> shape, "not an access decision").
    /// Strong consistency (invariant C4): the new value is live on the very
    /// next <see cref="GetProfileAsync"/> call.
    /// </summary>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">
    /// No profile with that <c>subjectId</c> exists (fail closed — the lane
    /// never load-or-creates, the <see cref="SetProfileAvatarAsync"/> pin).</exception>
    Task SetProfileTimezoneAsync(string subjectId, string? timezone, string actorBy);

    /// <summary>
    /// Set (or clear, with null) the resident's
    /// <see cref="Profile.PageSize"/> items-per-page preference — the user's
    /// <b>override</b> of the platform default
    /// (<see cref="Kumunita.Core.Query.PageSizer.Default"/> = 10; a <c>null</c> value means
    /// "reset to the platform default"). Mirrors
    /// <see cref="SetProfileTimezoneAsync"/> exactly (the C-MED·8 single
    /// write-lane shape): the self-scope check happens at the Web boundary
    /// (the owner is the actor); this lane writes <c>Profile.PageSize</c>
    /// only. One session, one <c>SaveChangesAsync</c>; no
    /// <see cref="Authorization.AccessAudit"/> row (a profile field write —
    /// the <see cref="UpsertProfileAsync"/> shape, "not an access decision").
    /// The value is clamped to <see cref="Query.PageSizer.Min"/>…
    /// <see cref="Query.PageSizer.Max"/> on the read path
    /// (<see cref="Query.PageSizer.Resolve"/>); this lane stores what it's
    /// given (the ADR 0019 "store, resolve on read" posture — a page size is
    /// never a fail-closed gate). Strong consistency (invariant C4): the new
    /// value is live on the very next <see cref="GetProfileAsync"/> call.
    /// </summary>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">
    /// No profile with that <c>subjectId</c> exists (fail closed — the lane
    /// never load-or-creates, the <see cref="SetProfileTimezoneAsync"/> pin).</exception>
    Task SetProfilePageSizeAsync(string subjectId, int? pageSize, string actorBy);

    /// <summary>
    /// Set (or clear, with null) the resident's
    /// <see cref="Profile.FirstDayOfWeek"/> first-day-of-week preference
    /// (ADR 0169) — the day (the <see cref="System.DayOfWeek"/> ordinal,
    /// 0 = Sunday … 6 = Saturday) that weeks anchor on across the day-of-week
    /// selectors (the Quiet-hours / Guardian time-limit pickers) and the
    /// events calendar's week + month columns. Mirrors
    /// <see cref="SetProfilePageSizeAsync"/> exactly (the C-MED·8 single
    /// write-lane shape): the self-scope check happens at the Web boundary
    /// (the owner is the actor); this lane writes <c>Profile.FirstDayOfWeek</c>
    /// only. One session, one <c>SaveChangesAsync</c>; no
    /// <see cref="Authorization.AccessAudit"/> row (a profile field write —
    /// the <see cref="UpsertProfileAsync"/> shape, "not an access decision").
    /// The value is resolved on the read path
    /// (<see cref="Query.WeekAnchor.Resolve"/>): a <c>null</c> / out-of-range
    /// value resolves to the platform default, Monday. Strong consistency
    /// (invariant C4): the new value is live on the very next
    /// <see cref="GetProfileAsync"/> call.
    /// </summary>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">
    /// No profile with that <c>subjectId</c> exists (fail closed — the lane
    /// never load-or-creates, the <see cref="SetProfileTimezoneAsync"/> pin).</exception>
    Task SetProfileFirstDayOfWeekAsync(string subjectId, int? firstDayOfWeek, string actorBy);

    /// <summary>
    /// Set the resident's <see cref="Profile.HideHomeIntro"/> flag (ADR 0149,
    /// D1) — whether the home page hides the two top intro sections ("Intro:
    /// what Kumunita is and does" + "What Kumunita does") so a signed-in
    /// resident lands straight on the "What's new" feed. Mirrors
    /// <see cref="SetProfilePageSizeAsync"/> exactly (the C-MED·8 single
    /// write-lane shape): the self-scope check happens at the Web boundary
    /// (the owner is the actor); this lane writes
    /// <c>Profile.HideHomeIntro</c> only. One session, one
    /// <c>SaveChangesAsync</c>; no <see cref="Authorization.AccessAudit"/> row
    /// (a profile field write — the <see cref="UpsertProfileAsync"/> shape,
    /// "not an access decision"). Strong consistency (invariant C4): the new
    /// value is live on the very next <see cref="GetProfileAsync"/> call.
    /// </summary>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">
    /// No profile with that <c>subjectId</c> exists (fail closed — the lane
    /// never load-or-creates, the <see cref="SetProfileTimezoneAsync"/> pin).</exception>
    Task SetProfileHideHomeIntroAsync(string subjectId, bool hideHomeIntro, string actorBy);

    /// <summary>
    /// Set the resident's <see cref="Profile.HomeShowUpcoming"/> flag — the
    /// home-page "upcoming" display preference: whether the home page shows
    /// the "Upcoming" section (the next upcoming events + the top to-dos,
    /// rendered just above the "What's new" feed). Mirrors
    /// <see cref="SetProfileHideHomeIntroAsync"/> exactly (the C-MED·8 single
    /// write-lane shape): the self-scope check happens at the Web boundary
    /// (the owner is the actor); this lane writes
    /// <c>Profile.HomeShowUpcoming</c> only. One session, one
    /// <c>SaveChangesAsync</c>; no <see cref="Authorization.AccessAudit"/> row
    /// (a profile field write — the <see cref="UpsertProfileAsync"/> shape,
    /// "not an access decision"). Strong consistency (invariant C4): the new
    /// value is live on the very next <see cref="GetProfileAsync"/> call.
    /// </summary>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">
    /// No profile with that <c>subjectId</c> exists (fail closed — the lane
    /// never load-or-creates, the <see cref="SetProfileTimezoneAsync"/> pin).</exception>
    Task SetProfileHomeShowUpcomingAsync(string subjectId, bool showUpcoming, string actorBy);

    /// <summary>
    /// Set the resident's <see cref="Profile.HomeUpcomingMineOnly"/> flag —
    /// the home-page "upcoming" scope: whether the home page's Upcoming
    /// section shows the resident's own events + to-dos (<c>true</c>) or the
    /// community-wide lists (<c>false</c>, the floor). Mirrors
    /// <see cref="SetProfileHomeShowUpcomingAsync"/> exactly (the C-MED·8
    /// single write-lane shape): the self-scope check happens at the Web
    /// boundary (the owner is the actor); this lane writes
    /// <c>Profile.HomeUpcomingMineOnly</c> only. One session, one
    /// <c>SaveChangesAsync</c>; no <see cref="Authorization.AccessAudit"/>
    /// row (a profile field write). Strong consistency (invariant C4): the
    /// new value is live on the very next <see cref="GetProfileAsync"/> call.
    /// </summary>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">
    /// No profile with that <c>subjectId</c> exists (fail closed — the lane
    /// never load-or-creates, the <see cref="SetProfileTimezoneAsync"/> pin).
    /// </exception>
    Task SetProfileHomeUpcomingMineOnlyAsync(string subjectId, bool mineOnly, string actorBy);

    /// <summary>
    /// Set (or clear, with null) the resident's <see cref="Profile.DateFormat"/>
    /// .NET custom datetime format string — the user's <b>override</b> of the
    /// platform default (ADR 0020; the fallback is <see
    /// cref="Localization.LocaleSettings.DefaultDateFormat"/> / the
    /// <see cref="Localization.DateFormat.FloorFormat"/> floor). Mirrors
    /// <see cref="SetProfileTimezoneAsync"/> exactly (the C-MED·8 single
    /// write-lane shape): the self-scope check happens at the Web boundary (the
    /// owner is the actor); this lane writes <c>Profile.DateFormat</c> only. One
    /// session, one <c>SaveChangesAsync</c>; no
    /// <see cref="Authorization.AccessAudit"/> row (a profile field write —
    /// the <see cref="UpsertProfileAsync"/> shape, "not an access decision").
    /// Strong consistency (invariant C4): the new value is live on the very
    /// next <see cref="GetProfileAsync"/> call.
    /// </summary>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">
    /// No profile with that <c>subjectId</c> exists (fail closed — the lane
    /// never load-or-creates, the <see cref="SetProfileTimezoneAsync"/> pin).</exception>
    Task SetProfileDateFormatAsync(string subjectId, string? formatString, string actorBy);

    /// <summary>
    /// Set (or clear, with null) the resident's
    /// <see cref="Profile.EmailLanguage"/> BCP-47 code — the user's
    /// <b>preference</b> for the language the platform writes to them in
    /// (outbound account emails and event reminders). Unlike the other
    /// overrides, this does not change the resident's own UI or rendering —
    /// it is strictly the *outbound channel's* language. The read seam
    /// (the email staging path) resolves the recipient's
    /// <c>Profile.EmailLanguage</c> first, then the instance default
    /// (<see cref="Localization.LocaleSettings.DefaultLanguageCode"/>), then
    /// the <c>en</c> registry floor. Mirrors
    /// <see cref="SetProfileDateFormatAsync"/> exactly (the C-MED·8 single
    /// write-lane shape): the self-scope check happens at the Web boundary (the
    /// owner is the actor); this lane writes <c>Profile.EmailLanguage</c> only.
    /// One session, one <c>SaveChangesAsync</c>; no
    /// <see cref="Authorization.AccessAudit"/> row (a profile field write —
    /// the <see cref="UpsertProfileAsync"/> shape, "not an access decision").
    /// Strong consistency (invariant C4): the new value is live on the very
    /// next <see cref="GetProfileAsync"/> call.
    /// </summary>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">
    /// No profile with that <c>subjectId</c> exists (fail closed — the lane
    /// never load-or-creates, the <see cref="SetProfileDateFormatAsync"/> pin).</exception>
    Task SetProfileEmailLanguageAsync(string subjectId, string? emailLanguage, string actorBy);

    /// <summary>
    /// M22 (ADR 0132, D2) — stamp the onboarding-completed state. Owner-scope
    /// single write lane (the <see cref="SetProfileTimezoneAsync"/> shape
    /// verbatim): stamps <see cref="Profile.OnboardingCompletedAt"/> = now, one
    /// <c>SaveChangesAsync</c>, **no <see cref="Authorization.AccessAudit"/>
    /// row** (a profile-field write — the <see cref="UpsertProfileAsync"/>
    /// shape, "not an access decision"). Strong consistency (invariant C4): the
    /// stamp is live on the very next <see cref="GetProfileAsync"/> call. The
    /// self-scope check happens at the Web boundary (the owner is the actor);
    /// this lane writes <c>Profile.OnboardingCompletedAt</c> only.
    /// </summary>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">
    /// No profile with that <c>subjectId</c> exists (fail closed — the lane
    /// never load-or-creates, the <see cref="SetProfileTimezoneAsync"/> pin).</exception>
    Task CompleteOnboardingAsync(string subjectId, string actorBy);

    /// <summary>
    /// M22 (ADR 0132, D2) — owner-scope read of the completion stamp
    /// (<see cref="Profile.OnboardingCompletedAt"/>; <c>null</c> = not
    /// completed, the floor). The <see cref="GetProfileAsync"/> read re-
    /// projected: owner-scope, **no audit row**, **never load-or-creates**
    /// (returns <c>null</c> when no <see cref="Profile"/> exists — the read is
    /// the floor, not a write). Mirrors <see cref="GetProfileAsync"/>.
    /// </summary>
    Task<DateTimeOffset?> GetOnboardingCompletedAsync(string subjectId);

    /// <summary>
    /// M9 amendment — the resident's own <b>messaging opt-in</b> write lane
    /// (the <see cref="SetProfileTimezoneAsync"/> / <see
    /// cref="CompleteOnboardingAsync"/> owner-scope single-write shape
    /// verbatim): flips <see cref="Profile.MessagingOptIn"/> to
    /// <paramref name="optIn"/>. One <c>SaveChangesAsync</c>; **no**
    /// <see cref="Authorization.AccessAudit"/> row (a profile-field write — the
    /// <see cref="UpsertProfileAsync"/> shape, "not an access decision"; the
    /// self-scope check happens at the Web boundary, the owner is the actor).
    /// Strong consistency (invariant C4): the value is live on the very next
    /// <see cref="GetProfileAsync"/> call. Written by the profile editor's
    /// "Allow me to message / messaging" toggle; read by the
    /// <see cref="Kumunita.Core.Messaging.IMessagingService"/> per-actor gate
    /// as the resident's own half of the decision (the instance toggle is the
    /// master gate; a guardian's restriction is the ceiling).
    /// </summary>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">No
    /// profile with that <c>subjectId</c> exists (fail closed — the lane never
    /// load-or-creates, the <see cref="CompleteOnboardingAsync"/> pin).</exception>
    Task SetMessagingOptInAsync(string subjectId, bool optIn, string actorBy);

    /// <summary>
    /// The composer's *component picker* / the <c>/community/{id}</c>
    /// *grouping* / the feed's *candidate filter* (M3 design §2.3). A
    /// *candidate set*, not a visible set (C-M3·2): the caller must pass every
    /// post through <c>IAuthorizationService</c> before rendering, and this
    /// read produces **no** <see cref="Authorization.AccessAudit"/> row itself
    /// (C-M3·2; pinned by the §2.4 seam test
    /// <c>F9_CandidateFilterEmitsNoAuditRow</c> at the service level and by
    /// <c>UserInfoServiceTests.GetComponentsAsync_CandidateFilterEmitsNoAuditRow</c>
    /// at the unit level — same C-M3·2 pin, two test files). Strong-consistency
    /// live rows (C4): a component enable/disable flip in the same commit is
    /// live on the very next call.
    /// </summary>
    Task<IReadOnlyList<Component>> GetComponentsAsync(bool enabledOnly);

    /// <summary>
    /// The directory's *candidate* set (M2 design doc §2.1, F15; invariants C3/C4/C6):
    /// <paramref name="verifiedOnly"/> true — every verified resident's profile
    /// document; false — every profile (only the §4.3 unverified-self case needs
    /// this). This is a *candidate filter*, not an access decision (C-M2·2): the
    /// result is never a visible set — the caller must pass each element through
    /// <c>IAuthorizationService</c> before rendering — and it produces no
    /// <see cref="Authorization.AccessAudit"/> row itself. Strong-consistency live
    /// rows (C4); no projection, no cache.
    /// </summary>
    Task<IReadOnlyList<Profile>> GetProfilesAsync(bool verifiedOnly);

    /// <summary>Bootstrap the profile (name, email, phone, <see cref="Profile.Visibility"/>
    /// default per ADR 0001-B — the author's choice, absolute by default — i.e.
    /// *self-only* visibility on bootstrap). Called by the IdentityModule's
    /// lifecycle (signup, seed-admin setup) and by the M1 profile-bootstrap
    /// surface (the M2 editing UI is out of scope, M1 design §"Out of scope").
    /// <para>
    /// M23 (ADR 0123 D3) extends this single write lane — <b>no new method</b>:
    /// <see cref="ProfileUpdate.Bio"/> is written <b>verbatim</b> (the author's
    /// choice, ADR 0001-B), and <see cref="ProfileUpdate.TagIds"/> is resolved
    /// to <see cref="Tags.Tag"/> ids — a missing <c>Slug</c> <b>creates</b> a
    /// <c>Tag</c> (+ one <c>tag.create</c> audit row, C-TG·9), a present
    /// <c>Slug</c> is <b>reused</b> (no row, C-TG·4). The profile write itself
    /// emits <b>no</b> audit row (a Profile field write, not an access decision
    /// — the existing shape). A <c>null</c> <c>Bio</c> / <c>TagIds</c> leaves
    /// the current value untouched (the "null ⇒ don't touch" patch rule every
    /// other field follows).
    /// </para>
    /// </summary>
    /// <param name="actorBy">
    /// M23 (D3): the acting resident's subject id — the <c>CreatedBy</c> on any
    /// newly created <see cref="Tags.Tag"/>. The caller (the self-edit Web
    /// boundary) passes the owner's id; a <c>null</c> falls back to
    /// <c>profile.SubjectId</c> (the only standing is the author's own profile,
    /// D3). Appended as an optional trailing param (the
    /// <see cref="SetProfileTimezoneAsync"/> <c>actorBy</c> idiom — the ADR
    /// 0006-E compatible-addition lane); existing two-param call sites compile
    /// unchanged.
    /// </param>
    Task UpsertProfileAsync(Profile profile, ProfileUpdate patch, string? actorBy = null);

    /// <summary>The four seeded components (Safety, Maintenance, Social,
    /// Governance) at first boot — idempotent (upsert by <c>key</c> /
    /// <see cref="Component.Id"/>). <see cref="Component.ModeratorAccess"/> defaults
    /// to <c>false</c> (invariant C5).</summary>
    Task<IReadOnlyList<Component>> SeedComponentsAsync();

    /// <summary>Set a component's <see cref="Component.ModeratorAccess"/> flag (the
    /// standing-moderator-scope path, ADR 0003; invariant C5 — OFF by default,
    /// ON by a GlobalAdmin). Appends an audit row (<c>via: Admin</c>,
    /// action "moderator-access", <see cref="AccessAudit.TargetKind"/> "component").</summary>
    Task SetComponentModeratorAccessAsync(string componentId, bool on, string actorId);

    /// <summary>The named scope's ModeratorAssignments (for the <c>/admin</c> surface's
    /// roles/scope assignment — ADR 0003's "delegating moderation
    /// is… promote + pick components").</summary>
    Task<IReadOnlyList<ModeratorAssignment>> GetAssignmentsAsync(string userId);

}
