namespace Kumunita.Core.Notifications;

/// <summary>
/// The closed, code-owned notification kind vocabulary (ADR 0076 D2).
/// The stored <see cref="Notification.Kind"/> remains a plain <c>string</c> —
/// these constants define the closed set the emitters and the settings page
/// use (the M5 <c>KanbanStatuses</c> shape; a resident cannot choose their own
/// kind string — only the code's emitters can).
/// </summary>
public static class NotificationKinds
{
    /// <summary>A reply was added to a post the resident authored (wired, U04).</summary>
    public const string PostReply = "post.reply";

    /// <summary>
    /// A reply mentions the resident (RESERVED — the constant + the <c>kw-l</c>
    /// keys exist; no M6 emitter calls it — mention detection is a follow-on
    /// lane, ADR 0076 D2).
    /// </summary>
    public const string PostMention = "post.mention";

    /// <summary>A new post was added to a group the resident is a member of (wired, U04).</summary>
    public const string GroupPost = "group.post";

    /// <summary>
    /// A group owner (or GlobalAdmin) added the resident directly to a group
    /// the resident was not already a member of (wired, ADR 0083 — the
    /// <c>UserInfoService.AddGroupMemberAsync</c> emitter; the recipient is the
    /// newly added resident).
    /// </summary>
    public const string GroupAdded = "group.added";

    /// <summary>
    /// A group owner (or GlobalAdmin) invited the resident to join a group
    /// (wired, ADR 0083 — the <c>UserInfoService.InviteGroupMemberAsync</c>
    /// emitter; the recipient is the invited resident).
    /// </summary>
    public const string GroupInvite = "group.invite";

    /// <summary>A resident RSVP'd to an event the resident authored (wired, U04).</summary>
    public const string EventRsvp = "event.rsvp";

    /// <summary>The M4 §6.4 day-before reminder (wired, U04 — M6 adds the inbox row; the email is M4's).</summary>
    public const string EventReminder = "event.reminder";

    /// <summary>A report was filed against content the resident authored (wired, U04).</summary>
    public const string ReportFiled = "report.filed";

    /// <summary>A report was assigned to the resident (a moderator) (wired, U04).</summary>
    public const string ReportAssigned = "report.assigned";

    /// <summary>A report the resident filed (or was assigned to) was resolved (wired, U04).</summary>
    public const string ReportResolved = "report.resolved";

    /// <summary>A to-do was assigned to the resident (person-only; wired, U04).</summary>
    public const string TodoAssign = "todo.assign";

    /// <summary>
    /// A new resident signed up (an <b>admin-lane</b> kind — the recipient is a
    /// <c>GlobalAdmin</c>, not the signing-up resident; wired by ADR 0077's
    /// <c>IdentityService.RegisterAsync</c> emitter, gated by the instance
    /// <c>NotifyAdminsOnSignup</c> flag). One of the thirteen kinds; the other
    /// twelve are the resident-facing wired kinds (including ADR 0083's
    /// <c>group.added</c> / <c>group.invite</c>) + the reserved
    /// <c>post.mention</c> + <c>account.verified</c>.
    /// </summary>
    public const string AccountSignup = "account.signup";

    /// <summary>
    /// A resident verified their account (an <b>admin-lane</b> kind — the
    /// recipient is a <c>GlobalAdmin</c>, not the verifying resident; wired by
    /// ADR 0077's <c>IdentityService.VerifyWithTokenAsync</c> emitter, gated by
    /// the instance <c>NotifyAdminsOnSignup</c> flag). One of the thirteen kinds.
    /// </summary>
    public const string AccountVerified = "account.verified";

    // ── ADR 0084 — per-target subscription kinds ─────────────────────────

    /// <summary>
    /// ADR 0084 — a new announcement was published. Recipient universe: the
    /// target community's members (for a community-scoped announcement) or,
    /// for a flat/public announcement, every verified resident. The emitter
    /// consults <see cref="NotificationService.IsSubscriptionEnabledAsync"/>
    /// to resolve the recipient's effective choice (opt-IN default: the kind
    /// is disabled until the resident stores an explicit
    /// <c>NotificationSubscription.Enabled = true</c> row for the target).
    /// </summary>
    public const string Announcement = "announcement";

    /// <summary>
    /// ADR 0084 — a new post appeared in a community the resident is a member
    /// of. Recipient universe: the community's members, excluding the author.
    /// Opt-OUT default (same shape as <see cref="GroupPost"/>): enabled until
    /// the resident stores an explicit <c>Enabled = false</c> row for the
    /// target community id.
    /// </summary>
    public const string CommunityPost = "community.post";

    /// <summary>
    /// ADR 0084 — a new page was added under a parent page. Recipient universe:
    /// the parent page's subscribers (the subscription target is the parent
    /// page's id). Opt-IN default: disabled until the resident stores an
    /// explicit <c>Enabled = true</c> row for the target parent-page id.
    /// </summary>
    public const string PageChild = "page.child";

    /// <summary>
    /// ADR 0084 — the set of kinds that default to <b>disabled</b> (opt-IN):
    /// the kind's default is off until the resident explicitly enables it with
    /// an <c>Enabled = true</c> subscription row. The complement of this set
    /// (the kinds not in <c>OptInKinds</c>) default to <b>enabled</b> (opt-OUT):
    /// the kind is on until the resident explicitly disables it with an
    /// <c>Enabled = false</c> row. <see cref="NotificationService
    /// .IsSubscriptionEnabledAsync"/> consults this table to resolve the
    /// effective default for a given kind.
    /// </summary>
    // ── M9 — direct 1:1 messaging (ADR 0105) ─────────────────────────

    /// <summary>
    /// M9 (ADR 0105, U03) — the recipient of a direct 1:1 message gets a
    /// nudge through the M6 lane (D6). Opt-OUT default (not in
    /// <see cref="OptInKinds"/>): the kind is enabled until the resident
    /// stores an explicit <c>Enabled = false</c> row (the resident-facing
    /// posture — the same shape as <c>group.post</c> / <c>post.reply</c>).
    /// The emitter is <see cref="Messaging.MessagingService".SendAsync}/>
    /// (the ADR 0105 D6 shape: idempotency key
    /// <c>notification:message.new:{messageId}</c>, <c>LinkPath =
    /// /messages/{conversationId}</c>, <c>targetId = null</c>).
    /// </summary>
    public const string MessageNew = "message.new";

    // ── GU community-approval lane — the guardian-facing pair ─────────────

    /// <summary>
    /// GU community-approval lane — a group owner (or GlobalAdmin) invited a
    /// **supervised child** to join a group. The recipient is the child's
    /// <b>guardian</b> (one row per active <c>GuardianLink</c>), not the
    /// child. The notification's <see cref="Kumunita.Core.Notifications
    /// .Notification.LinkPath"/> points at the child's manage-child page
    /// (where the guardian sees the pending list + the approve/reject
    /// buttons); <c>AcceptPath</c> / <c>DeclinePath</c> point at the same
    /// page (the inbox button + the email link both deep-link there).
    /// Opt-OUT default (the resident-facing posture, like
    /// <see cref="GroupInvite"/>).
    /// </summary>
    public const string GuardianGroupInvite = "guardian.group_invite";

    /// <summary>
    /// GU community-approval lane — an admin (or community moderator) added
    /// a **supervised child** to a community. The recipient is the child's
    /// <b>guardian</b> (one row per active <c>GuardianLink</c>), not the
    /// child. The notification's <see cref="Kumunita.Core.Notifications
    /// .Notification.LinkPath"/> points at the child's manage-child page
    /// (where the guardian sees the pending community-membership request +
    /// the approve/reject buttons); <c>AcceptPath</c> / <c>DeclinePath</c>
    /// point at the same page (the inbox button + the email link both
    /// deep-link there). Opt-OUT default (the resident-facing posture, like
    /// <see cref="GroupInvite"/>).
    /// </summary>
    public const string GuardianCommunityInvite = "guardian.community_invite";

    /// <summary>
    /// GA (ADR 0038 §F) — the acceptance lane: an existing guardian has
    /// <b>assigned</b> the resident as a co-guardian for one of their
    /// supervised children. The recipient is the <b>assigned</b> guardian
    /// (the assignee), not the child. The notification's
    /// <see cref="Kumunita.Core.Notifications.Notification.LinkPath"/>
    /// points at the assignee's <c>/me/children</c> Index page (where the
    /// pending-requests card shows the Accept / Decline actions); no
    /// <c>AcceptPath</c> / <c>DeclinePath</c> (the actions are form-POST
    /// with a consent block, not link-clickable — the ADR 0038 §F
    /// acceptance step is deliberate, not a one-click). Opt-OUT default
    /// (the resident-facing posture, like <see cref="GroupInvite"/>).
    /// Emitter: <see cref="Kumunita.Core.UserInfo.UserInfoService"/> via
    /// <see cref="Kumunita.Core.UserInfo.IUserInfoService
    /// .AssignGuardianLinkAsync"/> (the ADR 0038 §F supersession of the
    /// "no email notification" deferral).
    /// </summary>
    public const string GuardianAssign = "guardian.assign";

    // ── The lane — the guardian's event-attendance lanes ────────────────────

    /// <summary>
    /// The lane (<see cref="Kumunita.Core.UserInfo.EventRsvpMode
    /// .GuardianApproves"/> posture) — a supervised child asked to attend an
    /// event (their own RSVP self-lane was refused and a
    /// <see cref="Kumunita.Core.Events.GuardianEventRequest"/> row was stored
    /// instead). The recipient is the child's <b>guardian</b> (one row per
    /// active <c>GuardianLink</c>), not the child. The notification's
    /// <see cref="Kumunita.Core.Notifications.Notification.LinkPath"/> points
    /// at the child's manage-child page (where the pending event-attendance
    /// list + the approve/deny buttons live); <c>AcceptPath</c> /
    /// <c>DeclinePath</c> point at the same page (the inbox button + the email
    /// link both deep-link there, the <see cref="GuardianGroupInvite"/>
    /// shape). Opt-OUT default (the resident-facing posture, like
    /// <see cref="GuardianGroupInvite"/>).
    /// </summary>
    public const string GuardianEventRequest = "guardian.event_request";

    /// <summary>
    /// The lane (<see cref="Kumunita.Core.UserInfo.EventRsvpMode
    /// .GuardianNotifies"/> posture) — a supervised child attended / changed
    /// their attendance on an event through their own (allowed) RSVP
    /// self-lane, and every active guardian is told (the auto-approve + veto
    /// posture: the child's attendance already stands; the notification is
    /// the guardian's window to <see
    /// cref="Kumunita.Core.Events.IEventService.GuardianVetoEventRsvpAsync" />
    /// it). The recipient is the child's <b>guardian</b> (one row per active
    /// <c>GuardianLink</c>), not the child. The notification's
    /// <see cref="Kumunita.Core.Notifications.Notification.LinkPath"/> points
    /// at the event detail page (where the child's current RSVP is shown);
    /// no <c>AcceptPath</c> / <c>DeclinePath</c> (the veto is a form-POST on
    /// the manage-child page, not a one-click — the ADR 0038 §F shape).
    /// Opt-OUT default (the resident-facing posture, like
    /// <see cref="GuardianGroupInvite"/>).
    /// </summary>
    public const string GuardianEventRsvp = "guardian.event_rsvp";

    public static readonly IReadOnlySet<string> OptInKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        Announcement, PageChild,
    };

    /// <summary>
    /// The ordered, closed kind set (for the settings toggles + the <c>kw-l</c>
    /// key table). Order is the settings page's canonical display order.
    /// The two admin-lane kinds (<c>account.signup</c> / <c>account.verified</c>,
    /// ADR 0077) are appended last — they are a separate lane (the recipient is a
    /// GlobalAdmin, not the resident whose inbox the resident-facing kinds serve),
    /// so they trail the resident-facing kinds.
    /// </summary>
    public static IReadOnlyList<string> Known { get; } =
    [
        PostReply, PostMention, GroupPost, GroupAdded, GroupInvite,
        EventRsvp, EventReminder,
        ReportFiled, ReportAssigned, ReportResolved, TodoAssign,
        AccountSignup, AccountVerified,
        Announcement, CommunityPost, PageChild, MessageNew,
        GuardianGroupInvite, GuardianCommunityInvite, GuardianAssign,
        GuardianEventRequest, GuardianEventRsvp,
    ];
}
