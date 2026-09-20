namespace Kumunita.Core.Events;

/// <summary>
/// The request payload for <see cref="IEventService.CreateAsync"/> (U04). The
/// author's choices are written verbatim (ADR 0001-B); the <c>ImageIds</c> /
/// <c>AttachmentIds</c> lists are **not** sent by the client — the owning write lane
/// parses the body server-side (the RC U04 / U05 + ATT U5 idiom). The
/// <c>AuthorId</c> / <c>Created</c> / <c>Modified</c> fields are service-side (the
/// caller's identity + the current instant), not request fields.
/// </summary>
public sealed record CreateEventRequest
{
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string? ComponentId { get; init; }
    public DateTimeOffset Start { get; init; }
    public DateTimeOffset End { get; init; }
    public string? Location { get; init; }
    public int? Capacity { get; init; }
    public Authorization.Audience? Audience { get; init; }
    public bool ReminderEnabled { get; init; } = true;
    public bool IsDraft { get; init; } = true;
    public string LanguageCode { get; init; } = string.Empty;
    public IReadOnlyList<string> TagIds { get; init; } = [];
}

/// <summary>
/// The request payload for <see cref="IEventService.UpdateAsync"/> (U04). Same
/// shape as <see cref="CreateEventRequest"/> (the edit lane round-trips the same
/// field set, including the <c>Audience</c> verbatim — the ADR 0036
/// <c>FromAudience</c> / <c>BuildAudience</c> shape). <c>AuthorId</c> / <c>Created</c>
/// are **preserved untouched** on edit (the ADR 0014 / 0016 / 0017 precedent);
/// <c>Modified</c> is stamped by the service on a real change.
/// </summary>
public sealed record UpdateEventRequest
{
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string? ComponentId { get; init; }
    public DateTimeOffset Start { get; init; }
    public DateTimeOffset End { get; init; }
    public string? Location { get; init; }
    public int? Capacity { get; init; }
    public Authorization.Audience? Audience { get; init; }
    public bool ReminderEnabled { get; init; } = true;
    public string LanguageCode { get; init; } = string.Empty;
    public IReadOnlyList<string> TagIds { get; init; } = [];
}
