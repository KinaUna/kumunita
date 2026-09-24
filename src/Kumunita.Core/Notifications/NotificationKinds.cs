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
    /// The ordered, closed kind set (for the settings toggles + the <c>kw-l</c>
    /// key table). Order is the settings page's canonical display order.
    /// </summary>
    public static IReadOnlyList<string> Known { get; } =
    [
        PostReply, PostMention, GroupPost, EventRsvp, EventReminder,
        ReportFiled, ReportAssigned, ReportResolved, TodoAssign,
    ];
}
