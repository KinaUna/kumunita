namespace Kumunita.Core.Events;

/// <summary>
/// The M18 recurrence type (D2 — the closed rule shape). The author picks one
/// in the composer (F1); the pure expansion algorithm (C-M18·2) steps by
/// <c>Interval</c> units of this type. The <see cref="None"/> value is the
/// "no recurrence" sentinel (the composer's "None" option) — on such a row
/// <see cref="Event.RecurrenceRule"/> is <c>null</c>.
/// <para>
/// The §drift-guard (ADR 0119) forbids <c>BYDAY</c> / <c>BYMONTHDAY</c> /
/// <c>BYSETPOS</c> / <c>EXRULE</c> and a general RFC 5545 <c>RRULE</c>
/// grammar — this closed enum is the whole rule vocabulary (D11 defers the
/// richer shapes to their own ADR).
/// </para>
/// </summary>
public enum Recurrence
{
    /// <summary>No recurrence — the row is a single, concrete event (the default;
    /// <see cref="Event.RecurrenceRule"/> is <c>null</c> on such a row).</summary>
    None,

    /// <summary>Repeats every <c>Interval</c> day(s).</summary>
    Daily,

    /// <summary>Repeats every <c>Interval</c> week(s), on the same day-of-week as the head.</summary>
    Weekly,

    /// <summary>Repeats every <c>Interval</c> month(s), on the same day-of-month as the
    /// head (a day-of-month beyond the target month's last day clamps to that month's
    /// last day — e.g. head on the 31st, a 30-day month clamps to the 30th; the design
    /// doc §12 cross-month simplification).</summary>
    Monthly,

    /// <summary>Repeats every <c>Interval</c> year(s), on the same month-day as the head
    /// (a Feb-29 head clamps to Feb-28 in a non-leap year — the design doc §12
    /// cross-year simplification).</summary>
    Yearly,
}

/// <summary>
/// The M18 recurrence rule (D2 — the closed rule shape). Carried **only on the
/// head row** (D3 — the head's <see cref="Event.RecurrenceRule"/> is non-null;
/// every non-head occurrence's <c>RecurrenceRule</c> is <c>null</c>). The
/// <see cref="Count"/> / <see cref="Ends"/> fields are mutually exclusive in
/// intent (the composer disables one when the other is set — D7); if both are
/// set, <see cref="Count"/> wins (the design doc §5 expansion's rule).
/// </summary>
public sealed record EventRecurrenceRule
{
    /// <summary>The recurrence type (the <see cref="Recurrence"/> enum). A non-null
    /// rule always carries a non-<see cref="Recurrence.None"/> value — the expansion
    /// is only ever called for a head with a real rule.</summary>
    public Recurrence Recurrence { get; init; }

    /// <summary>The step size (≥ 1, default 1). "Every 2 weeks" is
    /// <see cref="Recurrence.Weekly"/> + <c>Interval: 2</c>. A value &lt; 1 is the
    /// composer's <c>min</c> guard + the service's validation backstop (U02).</summary>
    public int Interval { get; init; } = 1;

    /// <summary>Optional: the total number of occurrences including the head. Wins
    /// over <see cref="Ends"/> if both are set (D2's mutual-exclusivity rule).</summary>
    public int? Count { get; init; }

    /// <summary>Optional: the last occurrence's <c>Start</c> must be ≤ <see cref="Ends"/>
    /// (the expansion emits the longest prefix whose last row's <c>Start</c> is ≤
    /// <see cref="Ends"/>).</summary>
    public DateTimeOffset? Ends { get; init; }
}
