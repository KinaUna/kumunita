namespace Kumunita.Core.Events;

/// <summary>
/// <see cref="IEventService.ListGroupEventsAsync"/>'s result (the
/// <see cref="Kumunita.Core.Posts.FeedResult"/> group-lane analog). <see cref="Visible"/>
/// holds the source <see cref="Event"/> documents the single
/// <c>CanSeeGroupFeedAsync</c> call over the group's candidate set actually
/// allowed — never a hidden event's fields (GE1/GE2). <see cref="HiddenCount"/>
/// counts the candidates the call evaluated. One aggregate <c>AccessAudit</c>
/// row (TargetKind <c>"grouppost"</c> — the frozen seam's existing
/// discriminator, reused — TargetId null, the counts) is the row for this
/// visit (GE·5, C3).
/// </summary>
public sealed record GroupEventFeedResult(
    IReadOnlyList<Event> Visible,
    int HiddenCount,
    int Page,
    int Total);

/// <summary>
/// A group-event create input (ADR 0089, GE·2/GE·8) — the
/// <see cref="Kumunita.Core.Posts.GroupPostDraft"/> group-lane analog, the input to
/// <see cref="IEventService.CreateGroupEventAsync"/>. <see cref="GroupId"/> is the
/// non-empty channel (the service pins the write shape from it, GE·8);
/// <see cref="Title"/> optional (the M4 <see cref="Event.Title"/> nullable handling);
/// <see cref="Body"/> non-null (the <see cref="Event.Body"/> non-null invariant);
/// <see cref="StartUtc"/> / <see cref="EndUtc"/> the event's time (UTC — materialized to
/// the <see cref="Event.Start"/> / <see cref="Event.End"/> <c>DateTimeOffset</c>s).
/// <para>
/// **No <c>Audience</c> / <c>ComponentId</c> member** (GE·8): the service writes
/// <c>ComponentId = string.Empty</c> and <c>Audience = new Audience()</c> — the author's
/// choice is *which group* (the lane), not an audience, so an <c>Audience</c> is
/// deliberately not on this record (contrast the M4 community create, which seeds a
/// community-visible audience).
/// </para>
/// </summary>
public sealed record GroupEventDraft(
    string GroupId,
    string Title,
    string Body,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string? Location = null,
    int? Capacity = null,
    string? Color = null,
    string? LanguageCode = null,
    bool? ReminderEnabled = null);

/// <summary>
/// An author-only edit on a group event (ADR 0089 GE·4) — the input to
/// <see cref="IEventService.UpdateGroupEventAsync"/>. The <c>GroupId</c> /
/// <c>ComponentId</c> / <c>Audience</c> / <c>AuthorId</c> / <c>Created</c> /
/// <c>IsDraft</c> / <c>IsDeleted</c> lane fields are **never** edited here — the
/// service stamps only <see cref="Title"/> / <see cref="Body"/> /
/// <see cref="StartUtc"/> / <see cref="EndUtc"/> / <see cref="Location"/> /
/// <see cref="Capacity"/> / <see cref="Color"/> / <see cref="LanguageCode"/> /
/// <see cref="ReminderEnabled"/> (the <see cref="Event"/> mutable surface) and leaves
/// the lane markers untouched (the ADR 0016 author-lane precedent, no audit row).
/// </summary>
public sealed record GroupEventUpdate(
    string Title,
    string Body,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string? Location = null,
    int? Capacity = null,
    string? Color = null,
    string? LanguageCode = null,
    bool? ReminderEnabled = null);
