using System.ComponentModel.DataAnnotations;

namespace Kumunita.Web.Models;

/// <summary>
/// One <b>child account row</b> on the <c>/me/children</c> list (GU, ADR 0028;
/// M28 ADR 0151 §2.5 re-pin to 4 fields).
/// <para>
/// **Exact 4-field projection (M28 U06 pin)** — <see cref="ChildId"/>,
/// <see cref="DisplayName"/>, <see cref="Blocked"/>, <see
/// cref="HasTimeLimits"/>. <see cref="ChildId"/> is the
/// route's <c>{childId}</c> (the supervised child's
/// <see cref="Kumunita.Core.UserInfo.Profile.SubjectId"/>);
/// <see cref="DisplayName"/> is resolved from the child's
/// <see cref="Kumunita.Core.UserInfo.Profile.DisplayName"/> (the raw id as
/// fail-safe); <see cref="Blocked"/> drives the suspend/un-suspend button state —
/// the <see cref="Kumunita.Core.UserInfo.Profile.Blocked"/> flag the existing
/// <c>BlockedAccountMiddleware</c> + directory already read (enforcement parity;
/// U04's suspension lane sets the same flag); <see cref="HasTimeLimits"/>
/// (M28, ADR 0151 D6 — the <c>Blocked</c> flag precedent) drives the
/// "time-limits set?" badge on the GU Index card: <c>true</c> when the
/// child's <see cref="Kumunita.Core.UserInfo.GuardianTimeLimitSchedule"/>
/// row exists with <c>Enabled == true</c> (resolved via
/// <see cref="Kumunita.Core.UserInfo.IUserInfoService.GetActiveTimeLimitAsync"/>),
/// <c>false</c> otherwise (the floor, C-M28·3). No other field: the child's
/// posts, profile body, or any audience-restricted content never reach this
/// row (G·1).
/// </para>
/// </summary>
public sealed record ChildAccountItem(string ChildId, string DisplayName, bool Blocked, bool HasTimeLimits);

/// <summary>
/// M28 (ADR 0151 §2.5, D6) — the <b>"Time limits" section</b> view-model
/// for the child's <c>Detail</c> page. The <c>LocaleSettingsViewModel.Quiet</c>
/// shape re-expressed on the GU surface (the M20 quiet-section form fields,
/// flattened so the Razor view binds directly): <see cref="Enabled"/> (the
/// schedule's master on/off — <c>false</c> when there is no schedule or the
/// schedule is disabled, the floor C-M28·3), <see cref="Mode"/> (the form
/// posts <c>"blocked"</c> or <c>"allowed"</c>), <see cref="Hours"/> (0–23,
/// empty = all), <see cref="DaysOfWeek"/> (0=Sun…6=Sat, empty = all).
/// Exposed on <c>ViewData["TimeLimitsSection"]</c> (the
/// <c>MessagingRestricted</c> / <c>EventRsvpMode</c> ViewData precedent —
/// the <c>MembershipEditorModel</c> is a pinned record; the U07
/// exact-projection pin forbids adding a field).
/// </summary>
public sealed record TimeLimitsSection(
    bool Enabled,
    string Mode,
    IReadOnlyList<int> Hours,
    IReadOnlyList<int> DaysOfWeek);

/// <summary>
/// One <b>assigned guardian row</b> on the child's <c>Detail</c> "other
/// guardians" list (GA, ADR 0038 §F). The <see cref="ChildAccountItem"/>
/// shape mirrored: <see cref="SubjectId"/> is the assigned guardian's
/// <see cref="Kumunita.Core.UserInfo.Profile.SubjectId"/>; <see
/// cref="DisplayName"/> is resolved via <see
/// cref="Kumunita.Core.UserInfo.IUserInfoService.GetProfileAsync"/> (a read,
/// not a decision; G-A·3 — the assigned guardian's standing is identical in
/// kind to the creator's, no content read); <see cref="IsPending"/> is the
/// ADR 0038 §F acceptance-lane state — <c>true</c> when the row is
/// <see cref="Kumunita.Core.UserInfo.GuardianLinkStatus.Pending"/> (the
/// assignee has not yet accepted; the conferrer sees a "pending" badge),
/// <c>false</c> when the row is <see cref="Kumunita.Core.UserInfo
/// .GuardianLinkStatus.Active"/> (the assignee accepted — the conferrer sees
/// no badge).
/// </summary>
public sealed record GuardianItem(string SubjectId, string DisplayName, bool IsPending);

/// <summary>
/// One <b>pending event-attendance request</b> on the child's <c>Detail</c>
/// curation page (the lane's <see
/// cref="Kumunita.Core.Events.GuardianEventRequest" />
/// <see cref="Kumunita.Core.Events.GuardianEventRequestStatus.Pending" />
/// row, the <see cref="PendingInvitationItem" /> group-invitation
/// precedent re-expressed over events): the event id the child asked about,
/// the child's display name (the curation fact the guardian already sees —
/// the <see cref="ChildAccountItem" /> precedent, not a G·1 content read),
/// the attendance the child asked for (<see cref="Kumunita.Core.Events
/// .RsvpStatus"/>), and the request's creation instant. The approve / deny
/// buttons POST to the <c>ApproveEventRsvp</c> / <c>DenyEventRsvp</c> lanes
/// keyed by (childId, EventId).
/// </summary>
public sealed record EventRsvpRequestItem(
    string EventId,
    string ChildName,
    Kumunita.Core.Events.RsvpStatus DesiredStatus,
    string RequestedAt);

/// <summary>
/// One <b>existing child event-attendance row</b> on the child's
/// <c>Detail</c> curation page (the lane's
/// <see cref="Kumunita.Core.Events.EventRsvp" /> row, the
/// <see cref="Kumunita.Core.UserInfo.EventRsvpMode.GuardianNotifies" />
/// posture's veto list): the event id, the child's current
/// <see cref="Kumunita.Core.Events.RsvpStatus"/>, and the last write's
/// instant. The veto button POSTs to the <c>VetoEventRsvp</c> lane keyed by
/// (childId, EventId) — a hard ceiling that removes the child's RSVP
/// (the <see cref="Kumunita.Core.UserInfo.Profile
/// .MessagingRestricted" /> veto shape).
/// </summary>
public sealed record EventRsvpVetoItem(
    string EventId,
    Kumunita.Core.Events.RsvpStatus Status,
    string At);

/// <summary>
/// One <b>pending group invitation row</b> on the child's <c>Detail</c> curation
/// view (GU, ADR 0028). <see cref="GroupId"/> is the route's <c>{groupId}</c> the
/// approve POST posts to; <see cref="GroupName"/> is the display label (resolved
/// through the single-group read — a curation fact, not content); <see
/// cref="InvitedAt"/> is the row's
/// <see cref="Kumunita.Core.UserInfo.GroupInvitation.InvitedAt"/> (ISO-8601). The
/// row's <c>Status</c> / resolution stamps never reach the model — the list shows
/// only <em>pending</em> rows, and "who resolved it" is an
/// <see cref="Kumunita.Core.Authorization.AccessAudit"/> lane fact, not a UI fact
/// (G·1).
/// </summary>
public sealed record PendingInvitationItem(string GroupId, string GroupName, string InvitedAt);

/// <summary>
/// One <b>pending community-membership request row</b> on the child's
/// <c>Detail</c> curation view (the GU community-approval lane — the sibling
/// of <see cref="PendingInvitationItem"/>, for a supervised child's pending
/// <c>CommunityMembershipRequest</c>). <see cref="CommunityId"/> is the
/// route's <c>{communityId}</c> the approve / reject POSTs post to;
/// <see cref="CommunityName"/> is the display label (resolved through the
/// single-component read — a curation fact, not content); <see
/// cref="RequestedAt"/> is the row's
/// <see cref="Kumunita.Core.UserInfo.CommunityMembershipRequest.RequestedAt"/>
/// (ISO-8601). The row's <c>Status</c> / resolution stamps never reach the
/// model — the list shows only <em>pending</em> rows (G·1).
/// </summary>
public sealed record PendingCommunityRequestItem(
    string CommunityId, string CommunityName, string RequestedAt);

/// <summary>
/// The <b>per-child curation</b> view model for <c>/me/children/{childId}</c> (GU,
/// ADR 0028). The three curation sets — <see cref="GroupIds"/> (the child's group
/// membership ids), <see cref="CommunityIds"/> (the child's community membership
/// ids), and <see cref="PendingInvitations"/> (the child's pending group
/// invitations) — are **ids/names only** (G·1): a curation read, never the child's
/// posts, profile body, or any audience-restricted content. <see cref="ChildId"/>
/// is the route's <c>{childId}</c>.
/// </summary>
public sealed record MembershipEditorModel(
    string ChildId,
    IReadOnlyList<string> GroupIds,
    IReadOnlyList<string> CommunityIds,
    IReadOnlyList<PendingInvitationItem> PendingInvitations,
    IReadOnlyList<GuardianItem> GuardianItems,
    IReadOnlyList<PendingCommunityRequestItem> PendingCommunityRequests);

/// <summary>
/// The <b>add-a-child</b> form model (GU, ADR 0028; ADR 0146) bound via
/// <c>[FromForm]</c> on <c>GuardianController.AddChild</c>. The <b>guardian</b> is
/// never form-bound — it is minted by the Web layer from
/// <c>KumunitaPrincipal.SubjectId(User)</c> (the single identity source) and
/// passed as the <c>guardianId</c> argument to the Core seam
/// <see cref="Kumunita.Core.UserInfo.IUserInfoService.CreateGuardianLinkAsync"/>
/// (one commit, G·4). <see cref="DisplayName"/> / <see cref="Email"/> feed the
/// <see cref="Kumunita.Core.Identity.IIdentityService.RegisterChildAccountAsync"/>
/// child-formation lane — **no password field** (ADR 0146: the guardian does not
/// set the child's credential; the child sets their own at the confirmation
/// link, the ADR 0028 "supervision rides the link, not the password" shape).
/// <see cref="GuardianConsent"/> is the guardian's consent to the child-account
/// terms (guardian confirmation + data-processing terms), bound from the
/// "I consent" checkbox on the add-a-child form; creation is refused until it
/// is checked.
/// </summary>
public sealed class AddChildForm
{
    [Required, MaxLength(100)]
    [Display(Name = "Display name")]
    public string? DisplayName { get; set; }

    [Required, EmailAddress, MaxLength(255)]
    [Display(Name = "Email address")]
    public string? Email { get; set; }

    [Required]
    [Display(Name = "Guardian consent")]
    public bool GuardianConsent { get; set; }
}

/// <summary>
/// ADR 0143: the guardian delete-child form model, bound via <c>[FromForm]</c>
/// on <c>GuardianController.DeleteChild</c>. The <b>guardian</b> is never
/// form-bound — it is minted by the Web layer from
/// <c>KumunitaPrincipal.SubjectId(User)</c> (the single identity source, the
/// <c>AddChildForm</c> precedent); the <b>child</b> is the route's
/// <c>{childId}</c>. <see cref="Confirmed"/> is the dangerous-action
/// acknowledgment (the ADR 0142 <c>DeleteAccountViewModel.Confirmed</c>
/// precedent, verbatim): a bare <c>[Required]</c> on a bool would accept
/// <c>false</c> (the bound value is non-null), so the controller checks it
/// explicitly. The <c>data-confirm</c> client dialog (the repo's existing
/// idiom — the Dissolve form's precedent) is a second, client-side layer;
/// this checkbox is the first and is enforced server-side.
/// </summary>
public sealed class GuardianDeleteChildForm
{
    [Display(Name = "I understand the child account will be permanently deleted")]
    public bool Confirmed { get; set; }
}

/// <summary>
/// GA (ADR 0038): the assign-a-second-guardian form model, bound
/// via <c>[FromForm]</c> on <c>GuardianController.Assign</c>.
/// The <b>assigned guardian</b> is never form-bound beyond the
/// email (the email is the one external identifier; the
/// <c>FindSubjectByEmailAsync</c> seam resolves it to a
/// subject id). The <b>assigning guardian</b> is never form-bound
/// — it is minted by the Web layer from
/// <c>KumunitaPrincipal.SubjectId(User)</c> (the single identity
/// source, the <c>AddChildForm</c> precedent). The <b>child</b>
/// is the route's <c>{childId}</c>.
/// </summary>
public sealed class AssignGuardianForm
{
    [Required, EmailAddress, MaxLength(255)]
    [Display(Name = "Email of the guardian to assign")]
    public string? Email { get; set; }
}

/// <summary>
/// GA (ADR 0038 §F): the <b>accept-a-guardian-assignment</b> form model,
/// bound via <c>[FromForm]</c> on <c>GuardianController.Accept</c>.
/// <see cref="GuardianConsent"/> is the assignee's consent to the
/// child-account terms — the same confirmation + data-processing
/// obligations the creating guardian accepts on the
/// <c>AddChildForm</c> before creation (the "like when a guardian creates a
/// new child account" the ADR 0038 §F acceptance lane mirrors): the accept
/// is refused until the assignee checks the box. The assignee's own
/// <c>SubjectId</c> is minted by the controller from
/// <c>KumunitaPrincipal.SubjectId(User)</c> (the <c>AddChildForm</c>
/// precedent — the guardian is never form-bound); the <b>child</b> is the
/// route's <c>{childId}</c>.
/// </summary>
public sealed class AcceptGuardianForm
{
    [Required]
    [Display(Name = "Guardian consent")]
    public bool GuardianConsent { get; set; }
}

/// <summary>
/// One <b>pending guardian-assignment request row</b> on the assignee's
/// <c>/me/children</c> Index page (GA, ADR 0038 §F). <see cref
/// "ChildId"/> is the supervised child's
/// <see cref="Kumunita.Core.UserInfo.Profile.SubjectId"/> (the route's
/// <c>{childId}</c> the accept / decline POSTs post to); <see
/// cref="ChildDisplayName"/> is the child's display name (a curation fact,
/// resolved through the same profile read the <see cref
/// "ChildAccountItem"/> list uses — G·1 held: no content); <see
/// cref="ConferrerDisplayName"/> is the assigning guardian's display name
/// (the UGC snippet the <c>guardian.assign</c> notification carries — the
/// assignee already knows the name from the notification, the page repeats
/// it so the pending list is legible without the inbox); <see cref
/// "RequestedAt"/> is the row's <see cref="Kumunita.Core.UserInfo
/// .GuardianLink.CreatedAt"/> (ISO-8601) — the row's Status / resolution
/// stamps never reach the model (the list shows only <em>pending</em>
/// rows, the <see cref="PendingInvitationItem"/> precedent).
/// </summary>
public sealed record PendingGuardianRequestItem(
    string ChildId, string ChildDisplayName, string ConferrerDisplayName, string RequestedAt);

/// <summary>
/// The <b>index</b> view model for <c>/me/children</c> (GU ADR 0028,
/// extended by GA ADR 0038 §F): <see cref="Children"/> — the
/// guardian's active <c>ChildAccountItem</c> list (the GU pin,
/// unchanged); <see cref="PendingRequests"/> — the guardian's own
/// <see cref="PendingGuardianRequestItem"/> rows (the rows where
/// <see cref="Kumunita.Core.UserInfo.GuardianLink.GuardianId"/> = the
/// assignee, <see cref="Kumunita.Core.UserInfo.GuardianLinkStatus
/// .Pending"/> = the state, the assignee has not yet acted). A
/// non-assignee sees an empty <see cref="PendingRequests"/> list — the
/// same as the GU list (a non-guardian sees an empty card, nothing else).
/// </summary>
public sealed record GuardianIndexModel(
    IReadOnlyList<ChildAccountItem> Children,
    IReadOnlyList<PendingGuardianRequestItem> PendingRequests);
