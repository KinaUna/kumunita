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
        Announcement, CommunityPost, PageChild,
    ];
}
