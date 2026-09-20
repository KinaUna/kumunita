namespace Kumunita.Core.Events;

/// <summary>
/// A resident's RSVP status for an <see cref="Event"/> (ADR 0054 §3.2). The
/// <c>Going</c> set is the reminder recipient set (§3.6); <c>Maybe</c> / <c>No</c>
/// are not reminded.
/// </summary>
public enum RsvpStatus
{
    /// <summary>The resident is attending (a reminder recipient).</summary>
    Going,
    /// <summary>The resident is considering (not a reminder recipient).</summary>
    Maybe,
    /// <summary>The resident is not attending (not a reminder recipient).</summary>
    No
}

/// <summary>
/// A resident's RSVP to an <see cref="Event"/> (bounded context
/// <c>Kumunita.Core.Events</c>, ADR 0054 §3.2). **Last-write-wins, keyed per
/// user** — the <c>docs/ARCHITECTURE.md</c> §5 concurrency-token exception, keyed
/// per <c>(EventId, UserId)</c>: a conflicting RSVP write is a no-op or
/// self-converging; the resident's latest status is simply the truth. The
/// <see cref="M4DocTypes.Configure"/> surface registers a **unique**
/// <c>(EventId, UserId)</c> index (upsert semantics — exactly one row per
/// <c>(EventId, UserId)</c>).
/// <para>
/// **No <c>AccessAudit</c> row on an RSVP** (ADR 0054 §3.2 / §3.4): a routine
/// resident action, not an access decision — the same posture as a profile edit.
/// The <c>RsvpAsync</c> write lane (U04) stores no audit row.
/// </para>
/// <para>
/// **Reads:** the RSVP **list** is owner-only (the author sees their own event's
/// RSVPs); a non-author sees only their **own** RSVP (the <c>GetRsvpsAsync</c> /
/// <c>GetMyRsvpAsync</c> split, U03).
/// </para>
/// </summary>
public sealed class EventRsvp
{
    public string Id { get; set; } = string.Empty;

    /// <summary>The <see cref="Event"/> id this RSVP is for. Part of the unique
    /// <c>(EventId, UserId)</c> index (the last-write-wins pin).</summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>The RSVPing resident's <c>SubjectId</c>. Part of the unique
    /// <c>(EventId, UserId)</c> index (the last-write-wins pin).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The resident's latest status — the truth under last-write-wins
    /// (§3.2). Defaults to <see cref="RsvpStatus.Going"/> (the common affirmative).</summary>
    public RsvpStatus Status { get; set; } = RsvpStatus.Going;

    /// <summary>The last write's instant (UTC) — the resident's latest RSVP
    /// timestamp.</summary>
    public DateTimeOffset At { get; set; }
}
