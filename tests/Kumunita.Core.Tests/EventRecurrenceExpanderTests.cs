using Kumunita.Core.Events;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// GATE-1 (ADR 0119, U01) — the **pure** occurrence-expansion algorithm
/// (<see cref="EventRecurrenceExpander"/>). C-M18·2 pins that the expansion is
/// a **pure function** of (the head, the fixed <c>now</c> floor): no I/O, no
/// <c>DateTime.Now</c> inside the function, no randomness. These tests call
/// <see cref="EventRecurrenceExpander.ExpandRecurrence"/> with a fixed head
/// <see cref="Event.Start"/> / <see cref="Event.End"/> and a fixed
/// <c>now</c> well before the head's <see cref="Event.Start"/>, and assert the
/// **exact** row set (each with its own <c>Start</c> / <c>End</c>, the head's
/// series link, and the head's <see cref="Event.RecurrenceRule"/> preserved on
/// the head row only).
/// <para>
/// These tests are **pure** (no <see cref="PostgresFixture"/> / no
/// <see cref="Kumunita.Core"/> store) — they exercise the static expander
/// directly, so they run without Testcontainers. The two names below are the
/// §10 GATE-1 pin; a unit that does not land them is not done.
/// </para>
/// </summary>
public class EventRecurrenceExpanderTests
{
    private const string HeadId = "m18-gate1-head";
    private const string Author = "m18-gate1-author";

    /// <summary>
    /// Build the GATE-1 head row: a weekly event starting
    /// 2026-10-01T18:00Z and ending 2026-10-01T20:00Z (a 2-hour event), with
    /// the recurrence rule applied (the rule shape is set per-test —
    /// <see cref="EventRecurrenceRule.Count"/> / <see
    /// cref="EventRecurrenceRule.Ends"/> differ between the two pins).
    /// </summary>
    private static Event Head(EventRecurrenceRule? rule) => new()
    {
        Id = HeadId,
        Title = "Weekly cleanup day",
        Body = "body",
        AuthorId = Author,
        Start = new DateTimeOffset(2026, 10, 1, 18, 0, 0, TimeSpan.Zero),
        End = new DateTimeOffset(2026, 10, 1, 20, 0, 0, TimeSpan.Zero),
        Location = "Community hall",
        RecurrenceHeadId = null,
        RecurrenceRule = rule,
    };

    private static readonly DateTimeOffset Now_Well_Before_Head = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    // ── GATE-1 (1) — the Count branch: exactly 7 weekly rows, stepping 7 days ──

    [Fact]
    public void Expansion_Produces_Expected_Rows_For_A_Weekly_Series()
    {
        var head = Head(new EventRecurrenceRule
        {
            Recurrence = Recurrence.Weekly,
            Interval = 1,
            Count = 7,
        });

        var rows = EventRecurrenceExpander.ExpandRecurrence(head, Now_Well_Before_Head);

        Assert.Equal(7, rows.Count);

        // Row 0 is the head (same Id, Start, End, rule preserved, no series link).
        var row0 = rows[0];
        Assert.Equal(HeadId, row0.Id);
        Assert.Equal(head.Start, row0.Start);
        Assert.Equal(head.End, row0.End);
        Assert.NotNull(row0.RecurrenceRule);
        Assert.Null(row0.RecurrenceHeadId);

        // Rows 1..6 step exactly 7 days, in lockstep End (2-hour duration).
        var expectedStarts = new[]
        {
            new DateTimeOffset(2026, 10, 8, 18, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 15, 18, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 22, 18, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 29, 18, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 5, 18, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 12, 18, 0, 0, TimeSpan.Zero),
        };

        for (var i = 1; i <= 6; i++)
        {
            var row = rows[i];
            Assert.Equal(expectedStarts[i - 1], row.Start);
            // End = Start + (head.End - head.Start) — the 2-hour duration preserved.
            Assert.Equal(expectedStarts[i - 1].AddHours(2), row.End);
            Assert.Equal(HeadId, row.RecurrenceHeadId);
            Assert.Null(row.RecurrenceRule);
            // Placeholder id — the caller (U02 / U04) assigns the real id.
            Assert.Equal(string.Empty, row.Id);
        }
    }

    // ── GATE-1 (2) — the Ends branch: Count null, stop at Starts ≤ Ends ──────

    [Fact]
    public void Expansion_Stops_At_Ends_When_Count_Is_Null()
    {
        var head = Head(new EventRecurrenceRule
        {
            Recurrence = Recurrence.Weekly,
            Interval = 1,
            Count = null,
            Ends = new DateTimeOffset(2026, 10, 22, 23, 59, 59, TimeSpan.Zero),
        });

        var rows = EventRecurrenceExpander.ExpandRecurrence(head, Now_Well_Before_Head);

        // Exactly 4 rows: the head + 3 non-heads (2026-10-08, -15, -22).
        // The 2026-10-29 occurrence's Start is > Ends, so it is not emitted.
        Assert.Equal(4, rows.Count);

        var expectedStarts = new[]
        {
            new DateTimeOffset(2026, 10, 1, 18, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 8, 18, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 15, 18, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 22, 18, 0, 0, TimeSpan.Zero),
        };

        for (var i = 0; i < 4; i++)
            Assert.Equal(expectedStarts[i], rows[i].Start);

        // The 2026-10-29 row is **not** present.
        Assert.DoesNotContain(rows, r => r.Start == new DateTimeOffset(2026, 10, 29, 18, 0, 0, TimeSpan.Zero));

        // Non-head rows carry the series link + a null rule.
        for (var i = 1; i < 4; i++)
        {
            Assert.Equal(HeadId, rows[i].RecurrenceHeadId);
            Assert.Null(rows[i].RecurrenceRule);
        }
    }
}
