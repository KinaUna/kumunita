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
    // RC R·3/R·7 (ADR 0025) — the content-image ids, **server-side** (the Web
    // layer parses the body's /content-image/{id} links via
    // ContentImageIds.ExtractContentImageIds before calling the service — Core
    // stays body-parse-free, R·5). Written onto the Event doc with a
    // null-coalesce to the POCO's non-null empty list (`= []`). Nullable
    // default: a positional/initialiser that omits it ⇒ null ⇒ [].
    public IReadOnlyList<string>? ImageIds { get; init; }
    // ATT (ADR 0034) — the file-attachment ids, **server-side** (the Web layer
    // parses the body's /attachment/{id} links via AttachmentIds.ExtractAttachmentIds
    // before calling the service). Written onto the Event doc with a
    // null-coalesce to the POCO's non-null empty list. **Separate from**
    // ImageIds (C-ATT·5 — an event's images stay in ImageIds, its files in
    // AttachmentIds). Nullable default: omitted ⇒ null ⇒ [].
    public IReadOnlyList<string>? AttachmentIds { get; init; }
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
    // RC R·3/R·7 (ADR 0025) — the re-parsed content-image ids from the
    // (re-submitted) body; the re-parse is authoritative (replace-style, the
    // Announcement edit lane's shape). Server-side; the client never sends them
    // (a form field would be spoofable). Nullable default: omitted ⇒ null ⇒ [].
    public IReadOnlyList<string>? ImageIds { get; init; }
    // ATT (ADR 0034) — the re-parsed attachment ids from the (re-submitted)
    // body (replace-style). Server-side; separate from ImageIds (C-ATT·5).
    // Nullable default: omitted ⇒ null ⇒ [].
    public IReadOnlyList<string>? AttachmentIds { get; init; }
}
