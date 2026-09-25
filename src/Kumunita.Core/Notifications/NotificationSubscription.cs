namespace Kumunita.Core.Notifications;

/// <summary>
/// ADR 0084 — a per-target notification <b>subscription</b> (the user's
/// explicit "tell me when this target fires" choice, as distinct from the
/// per-kind <see cref="NotificationPreference"/> "tell me about this kind of
/// event at all" choice).
/// <para>
/// One row per (recipient, kind, target) triple; <see cref="Id"/> is a
/// surrogate PK for clean Marten identity (the <c>GroupMembership</c> /
/// <c>ComponentMembership</c> business-key convention — a unique index on
/// (<c>Kind</c>, <c>TargetId</c>) per recipient enforces the pair at the DB
/// layer). The <c>(RecipientId, Kind, TargetId)</c> triple is the business
/// key; <c>TargetId</c> is the stable id of the target the subscription
/// applies to (an announcement's community id, a community's id, a group's
/// id, or a page's id). <c>TargetId</c> is a sentinel (see the
/// <see cref="NotificationKinds"/> kind's documentation) for kinds that
/// have a single global target (e.g. "new announcements").
/// </para>
/// <para>
/// **Opt-in vs opt-out semantics (ADR 0084):** the same shape serves both
/// — <c>Enabled = true</c> is an explicit opt-IN, <c>Enabled = false</c> is
/// an explicit opt-OUT. The <see cref="NotificationService.IsSubscriptionEnabledAsync"/>
/// gate consults the row's <c>Enabled</c> value and the *kind's default*
/// (the <see cref="NotificationKinds.OptInKinds"/> table):
/// <list type="bullet">
/// <item>an **opt-OUT** kind (not in <see cref="NotificationKinds.OptInKinds"/>,
///   e.g. <see cref="NotificationKinds.GroupPost"/> or
///   <see cref="NotificationKinds.CommunityPost"/>) defaults to enabled — an
///   absent row is treated as enabled, a stored <c>Enabled = false</c> row
///   disables.</item>
/// <item>an **opt-IN** kind (in <see cref="NotificationKinds.OptInKinds"/>,
///   e.g. <see cref="NotificationKinds.Announcement"/> or
///   <see cref="NotificationKinds.PageChild"/>) defaults to disabled — an
///   absent row is treated as disabled, a stored <c>Enabled = true</c> row
///   enables.</item>
/// </list>
/// This keeps the feature additive: a fresh install with no subscription
/// rows behaves exactly like before (opt-out kinds still fire, opt-in kinds
/// stay quiet), and each resident's explicit toggle overrides the default
/// for the target they chose.
/// </para>
/// <para>
/// **Personal write, not an <c>AccessAction</c> decision** (D3 / F11
/// carried): no <c>IAuthorizationService</c> call, no audit row — the
/// <c>RecipientId</c> is the whole access story, the same as
/// <see cref="NotificationPreference"/>.
/// </para>
/// </summary>
public sealed class NotificationSubscription
{
    /// <summary>Surrogate PK; (RecipientId, Kind, TargetId) is the business key.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The resident the subscription applies to (the recipient).</summary>
    public string RecipientId { get; set; } = string.Empty;

    /// <summary>The <see cref="NotificationKinds"/> kind the target applies to.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// The stable id of the target (community / group / parent-page id, or a
    /// sentinel for kinds that have a single global target).
    /// </summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>The resident's explicit choice (see the class docs above).</summary>
    public bool Enabled { get; set; }

    /// <summary>Last time the resident changed their choice (audit-by-default: the
    /// *what* is the row, the *when* is this stamp — no separate audit log).</summary>
    public DateTimeOffset Updated { get; set; }
}
