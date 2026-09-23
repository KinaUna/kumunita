namespace Kumunita.Core.Events;

/// <summary>
/// A community event (bounded context <c>Kumunita.Core.Events</c>, ADR 0054 / M4).
/// The first **content-with-time** document: a resident sees that a cleanup day is
/// happening, RSVPs, and gets a reminder the day before.
/// <para>
/// Every field reuses an existing mechanism — none invents one (ADR 0054 §3.1, the
/// design doc's field-by-field provenance table):
/// </para>
/// <list type="bullet">
/// <item><see cref="Title"/> / <see cref="Body"/> / <see cref="LanguageCode"/> —
/// <c>Post</c> / <c>Announcement</c> (ADR 0018): authored-in tag + Markdown body;
/// the one <c>MarkdownRenderer</c>, the one <c>bindRichEditor</c>.</item>
/// <item><see cref="ComponentId"/> — <c>Post.ComponentId</c>: a feed filter,
/// **never** a gate (C-M3·2).</item>
/// <item><see cref="AuthorId"/> — <c>Post.AuthorId</c>: the owner branch; the
/// standing matrix's standing owner.</item>
/// <item><see cref="Start"/> / <see cref="End"/> — *new, this milestone*: the time,
/// rendered by the one <c>kw-dt</c> TagHelper; <see cref="Start"/> drives feed ordering
/// (the <c>(ComponentId, Start)</c> index, <see cref="M4DocTypes.Configure"/>) and the
/// reminder window (§3.6).</item>
/// <item><see cref="Location"/> / <see cref="Capacity"/> — *new, this milestone*:
/// display metadata — <see cref="Capacity"/> is **not** a gate (no admission queue in
/// M4; <c>Going</c> RSVPs are the truth, §5).</item>
/// <item><see cref="Color"/> — display metadata (the <see cref="Location"/> /
/// <see cref="Capacity"/> shape): the author's picked chip color for the calendar;
/// <c>null</c> = the theme default. Never a gate.</item>
/// <item><see cref="Audience"/> — <c>Post.Audience</c> (ADR 0001-B / 0036): <c>null</c>
/// = public (the frozen <c>Decide()</c> branch 5); the <c>AudienceEditorModel</c> form
/// surface verbatim. **No new audience mechanism.**</item>
/// <item><see cref="ReminderEnabled"/> — *new, this milestone*: the §6.4 job's
/// per-event opt-out; <c>true</c> floor.</item>
/// <item><see cref="IsDraft"/> — <c>Announcement.IsDraft</c> (ADR 0037): the author-only
/// draft pin + the <c>/my/drafts</c> lane.</item>
/// <item><see cref="IsDeleted"/> — <c>Post</c> soft-delete (ADR 0024): filtered in the
/// read lanes. (The <c>Event</c> uses the bool <c>IsDeleted</c> flag shape, the same
/// shape as the other UGC surfaces — distinct from <c>Post.DeletedAt</c>'s nullable
/// timestamp; the flag is the ADR 0054 §3.1 pin.)</item>
/// <item><see cref="TagIds"/> / <see cref="ImageIds"/> / <see cref="AttachmentIds"/> —
/// <c>Post</c> (ADR 0044 / 0025 / 0034): the server-side body parse populates
/// <see cref="ImageIds"/> / <see cref="AttachmentIds"/> (the client never sends them).</item>
/// </list>
/// <para>
/// <b>Translation lane:</b> an <c>Event</c> may also carry **user-added
/// translations** (the <see cref="EventTranslation"/> row) — added / edited /
/// removed by the event's author or a Translator / GlobalAdmin (ADR 0059, the
/// ADR 0022/0029 lane carried to the M4 surface). <c>LanguageCode</c> below is
/// the authored-in language, *not* the translation mechanism.
/// </para>
/// </summary>
public sealed class Event
{
    public string Id { get; set; } = string.Empty;            // surrogate (Marten default)

    public string Title { get; set; } = string.Empty;         // display label (adapter Name)

    /// <summary>
    /// The event's rich body (ADR 0025 / 0031): authored in Markdown, rendered by the
    /// one <c>MarkdownRenderer</c> and edited by the one <c>bindRichEditor</c>. The
    /// RC content-image (<c>/content-image/{id}</c>) + ATT attachment
    /// (<c>/attachment/{id}</c>) body idioms live here; their id lists are
    /// <see cref="ImageIds"/> / <see cref="AttachmentIds"/> (server-side parse).
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// The feed organizer (a <c>Component</c> id) — a **feed filter, never an access
    /// boundary** (C-M3·2, the <c>Post.ComponentId</c> shape). <c>null</c> ⇒ not scoped
    /// to a component. Drives the <c>(ComponentId, Start)</c> index for feed ordering.
    /// </summary>
    public string? ComponentId { get; set; }

    /// <summary>The event author's <c>SubjectId</c> — the owner branch and the standing
    /// matrix's standing owner (ADR 0054 §3.4).</summary>
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>The event's start instant (UTC). The feed ordering key and the
    /// reminder window's anchor (§3.6).</summary>
    public DateTimeOffset Start { get; set; }

    /// <summary>The event's end instant (UTC).</summary>
    public DateTimeOffset End { get; set; }

    /// <summary>A free-text location (display metadata, not a gate).</summary>
    public string? Location { get; set; }

    /// <summary>A display capacity hint (ADR 0054 §3.1). **Not a gate** — there is no
    /// admission queue / waitlist in M4 (the design doc §5 non-decision); <c>Going</c>
    /// RSVPs are the truth.</summary>
    public int? Capacity { get; set; }

    /// <summary>
    /// An optional display color (a CSS color, typically a normalized <c>#RRGGBB</c> hex
    /// value) the author picked in the composer — a pure **display** choice, not a gate
    /// (the <see cref="Location"/> / <see cref="Capacity"/> shape). The calendar renders
    /// it as the event chip/block background; <c>null</c> = the theme default.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// The **exact** <c>Post</c> <see cref="Kumunita.Core.Authorization.Audience"/>
    /// (ADR 0001-B / 0036) — *reused*, not extended. <c>null</c> = public (the frozen
    /// <c>Decide()</c> branch 5); a non-null audience restricts read access through the
    /// frozen <c>IAuthorizationService.CanAsync(Read)</c> path. The composer seeds it
    /// **community-visible by default** (ADR 0036). **No new audience mechanism.**
    /// </summary>
    public Authorization.Audience? Audience { get; set; }

    /// <summary>
    /// The §6.4 job's per-event opt-out (ADR 0054 §3.6): when <c>false</c>, the
    /// <c>EventReminders</c> job skips this event's recipients. <c>true</c> floor — the
    /// single 24-hour-before reminder is the M4 surface.
    /// </summary>
    public bool ReminderEnabled { get; set; } = true;

    /// <summary>
    /// True while this event is an unsaved draft (ADR 0037, the <c>Announcement.IsDraft</c>
    /// shape). A draft is **invisible to everyone except its author** — the authorization
    /// algorithm is never consulted for a draft (a pure <c>AuthorId == actorId</c> check in
    /// the service layer). Feeds exclude drafts unconditionally. Set to <c>false</c> by
    /// <c>EventService.PublishAsync</c> (author-only, ADR 0037 pin). Default <c>true</c> —
    /// a newly created event is a draft until its author publishes it.
    /// </summary>
    public bool IsDraft { get; set; } = true;

    /// <summary>
    /// Set when the **author** soft-deletes this event (ADR 0024, the author-lane shape):
    /// <c>true</c> while deleted. The record is kept (never hard-deleted) and the read
    /// lanes (<c>ListUpcomingAsync</c> / <c>GetAsync</c>) filter it out. Default
    /// <c>false</c> — a live event.
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// The BCP-47 code of the language this event was **authored in** (ADR 0018, the
    /// <c>Post.LanguageCode</c> shape). Written at create time and editable on the edit
    /// lane. Materialized from the instance default (<c>en</c> floor) when the author
    /// leaves it unchosen, so no stored row is empty. **Not a translation mechanism** —
    /// user-added translations of this event live on the separate
    /// <see cref="EventTranslation"/> row (ADR 0059), keyed by their own
    /// <c>LanguageCode</c>.
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// The tag ids referenced by the event (ADR 0044, the <c>Post</c> shape). Written
    /// server-side by the owning write lane; the client never sends them.
    /// </summary>
    public IReadOnlyList<string> TagIds { get; set; } = [];

    /// <summary>
    /// The content images referenced by <see cref="Body"/> — the <c>MediaObject</c> ids
    /// appearing as <c>/content-image/{id}</c> links in the rendered body (ADR 0025, the
    /// <c>Post.ImageIds</c> shape). Populated server-side by the owning write lane (U04);
    /// the client never sends them.
    /// </summary>
    public IReadOnlyList<string> ImageIds { get; set; } = [];

    /// <summary>
    /// The attachment file ids referenced by <see cref="Body"/> — the <c>MediaObject</c>
    /// ids appearing as <c>/attachment/{id}</c> links in the rendered body (ADR 0034, the
    /// <c>Post.AttachmentIds</c> shape). Populated server-side by the owning write lane
    /// (U04); the client never sends them. **Separate from** <see cref="ImageIds"/>.
    /// </summary>
    public IReadOnlyList<string> AttachmentIds { get; set; } = [];

    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Modified { get; set; }
}
