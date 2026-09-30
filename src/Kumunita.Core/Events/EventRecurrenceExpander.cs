namespace Kumunita.Core.Events;

/// <summary>
/// M18 (ADR 0119, C-M18·2) — the **pure** occurrence-expansion algorithm.
/// A stateless static method: no I/O, no <c>DateTime.Now</c> outside the
/// single <paramref name="now"/> parameter, no randomness.
/// <para>
/// **Purity (C-M18·2):** the function is a **pure function** of
/// (<paramref name="head"/>, <paramref name="now"/>). The §5 pinned tests
/// (GATE-1) call it with a fixed <paramref name="now"/> and assert the exact
/// row set.
/// </para>
/// <para>
/// U02's head-edit cascade calls this to re-materialize the series on a rule /
/// <c>Start</c> / <c>End</c> change (D4); on a re-materialization the
/// <paramref name="now"/> floor drops occurrences whose <c>Start</c> is in the
/// past (a past occurrence is not re-created — the author's intent is "future
/// repeats"; the past rows already exist and were soft-deleted by the cascade).
/// On a fresh create the head's <c>Start</c> is in the future, so the floor is
/// a no-op.
/// </para>
/// </summary>
public static class EventRecurrenceExpander
{
    /// <summary>
    /// Expand the head's rule into a **concrete, ordered list of occurrence
    /// rows** (the head first, then the non-head occurrences in
    /// <c>Start</c> ascending order).
    /// <para>
    /// **Rules (design doc §5, D2 / D4):**
    /// <list type="number">
    /// <item>The head is **always** row 0 of the result (its <c>Start</c> is
    /// the head's <c>Start</c>; its <c>RecurrenceHeadId</c> is <c>null</c>;
    /// its <c>RecurrenceRule</c> is the head's rule — preserved verbatim).</item>
    /// <item>The non-head occurrences step by <c>Interval</c> units of the
    /// <c>Recurrence</c> type — <see cref="Recurrence.Daily"/> steps days,
    /// <see cref="Recurrence.Weekly"/> steps weeks,
    /// <see cref="Recurrence.Monthly"/> steps months (keeping the head's
    /// day-of-month, clamping to the target month's last day if the day is
    /// beyond it), <see cref="Recurrence.Yearly"/> steps years (keeping the
    /// head's month + day-of-month, clamping Feb 29 → Feb 28 in non-leap
    /// years).</item>
    /// <item>Expansion **stops** when **either** <c>Count</c> / <c>Ends</c> is
    /// satisfied (whichever comes first): if <c>Count != null</c>, at most
    /// <c>Count</c> rows are generated (head + <c>Count - 1</c> non-head); if
    /// <c>Count</c> is null and <c>Ends != null</c>, the result is the longest
    /// prefix whose last row's <c>Start ≤ Ends</c>; if **both** are set,
    /// <c>Count</c> wins.</item>
    /// <item>Every non-head occurrence is a **new** <c>Event</c> row with a
    /// placeholder <c>Id = string.Empty</c> (the caller — U02 / U04 — assigns
    /// the real id), <c>RecurrenceHeadId = head.Id</c>, <c>RecurrenceRule =
    /// null</c>, and the head's display fields
    /// (<c>AuthorId</c> / <c>ComponentId</c> / <c>GroupId</c> / <c>Title</c> /
    /// <c>Body</c> / <c>Location</c> / <c>Capacity</c> / <c>Color</c> /
    /// <c>Audience</c> / <c>ReminderEnabled</c> / <c>LanguageCode</c> /
    /// <c>IsDraft</c> / <c>TagIds</c> / <c>ImageIds</c> / <c>AttachmentIds</c>)
    /// copied verbatim (a non-head occurrence is "the same event, a different
    /// date").</item>
    /// <item>The <c>End</c> of a non-head row = the head's
    /// <c>(End - Start)</c> duration added to the non-head's <c>Start</c>
    /// (a 2-hour event stays a 2-hour event across the series).</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="head">
    /// The head row. Must have <c>head.RecurrenceRule != null</c> and
    /// <c>head.RecurrenceRule.Recurrence != None</c> (a non-null
    /// <see cref="EventRecurrenceRule"/> always carries a non-<c>None</c>
    /// <c>Recurrence</c>). A head with a <c>null</c> rule or
    /// <see cref="Recurrence.None"/> returns a single-element list containing
    /// only the head (the non-recurring M4 shape, unchanged).
    /// </param>
    /// <param name="now">
    /// The current UTC instant (the caller passes it — never
    /// <c>DateTime.Now</c> inside this pure function). **Used for one purpose
    /// only:** non-head occurrences whose <c>Start</c> is in the past
    /// (<c>Start &lt; now</c>) are **skipped** (the re-materialization floor,
    /// D4). The head is **always** emitted regardless of <paramref name="now"/>.
    /// </param>
    /// <returns>
    /// A concrete, ordered list of occurrence rows — the head first, then the
    /// non-head occurrences in <c>Start</c> ascending order. The head row is
    /// returned with its own <c>Id</c> / <c>RecurrenceRule</c> /
    /// <c>RecurrenceHeadId = null</c>; every non-head row has
    /// <c>Id = string.Empty</c> (placeholder), <c>RecurrenceHeadId =
    /// head.Id</c>, and <c>RecurrenceRule = null</c>.
    /// </returns>
    public static IReadOnlyList<Event> ExpandRecurrence(Event head, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(head);

        var rule = head.RecurrenceRule;
        if (rule is null || rule.Recurrence == Recurrence.None)
            return [head];

        var interval = Math.Max(1, rule.Interval);
        var results = new List<Event> { head };

        // How many non-head occurrences to generate (before the now-floor /
        // Ends filters). Count wins when both are set (D2's rule).
        var maxNonHead = rule.Count is int count ? count - 1 : int.MaxValue;

        var start = head.Start;
        for (var i = 0; i < maxNonHead; i++)
        {
            var nextStart = StepOnce(start, rule.Recurrence, interval);

            // Ends branch (only when Count is null — Count wins when both are set).
            if (rule.Count is null && rule.Ends is { } ends && nextStart > ends)
                break;

            start = nextStart;

            // Re-materialization floor: a past occurrence is not re-created
            // (D4). The head is exempt — it is always row 0.
            if (nextStart < now)
                continue;

            results.Add(CloneForOccurrence(head, nextStart));
        }

        return results;
    }

    /// <summary>
    /// Step a single occurrence forward by <paramref name="interval"/> units of
    /// the <paramref name="recurrence"/> type from <paramref name="current"/>.
    /// The monthly / yearly day-clamping (the design doc §12 cross-month /
    /// cross-year simplification) lives here.
    /// </summary>
    private static DateTimeOffset StepOnce(DateTimeOffset current, Recurrence recurrence, int interval)
    {
        return recurrence switch
        {
            Recurrence.Daily => current.AddDays(interval),
            Recurrence.Weekly => current.AddDays(7 * interval),
            Recurrence.Monthly => AddMonthsClamped(current, interval),
            Recurrence.Yearly => AddYearsClamped(current, interval),
            _ => throw new ArgumentOutOfRangeException(nameof(recurrence), recurrence, "M18 supports Daily / Weekly / Monthly / Yearly only (D2)."),
        };
    }

    /// <summary>
    /// Advance <paramref name="dt"/> by <paramref name="months"/> calendar
    /// months, keeping the day-of-month but clamping it to the target month's
    /// last day when the day-of-month is beyond it (the design doc §12
    /// cross-month simplification).
    /// </summary>
    private static DateTimeOffset AddMonthsClamped(DateTimeOffset dt, int months)
    {
        var year = dt.Year;
        var monthIndex = dt.Month - 1 + months;
        year += monthIndex / 12;
        monthIndex %= 12;
        if (monthIndex < 0)
        {
            monthIndex += 12;
            year--;
        }

        var month = monthIndex + 1;
        var day = Math.Min(dt.Day, DateTime.DaysInMonth(year, month));
        return new DateTimeOffset(year, month, day, dt.Hour, dt.Minute, dt.Second, dt.Offset);
    }

    /// <summary>
    /// Advance <paramref name="dt"/> by <paramref name="years"/> calendar
    /// years, keeping month + day-of-month but clamping a Feb-29 head to
    /// Feb-28 in non-leap years (the design doc §12 cross-year simplification).
    /// </summary>
    private static DateTimeOffset AddYearsClamped(DateTimeOffset dt, int years)
    {
        var year = dt.Year + years;
        var day = Math.Min(dt.Day, DateTime.DaysInMonth(year, dt.Month));
        return new DateTimeOffset(year, dt.Month, day, dt.Hour, dt.Minute, dt.Second, dt.Offset);
    }

    /// <summary>
    /// Build a non-head occurrence row from the head: the head's display fields
    /// copied verbatim, a fresh placeholder <c>Id</c>, the series link to the
    /// head, a <c>null</c> rule, and the head's <c>(End - Start)</c> duration
    /// applied at the new <c>Start</c>.
    /// </summary>
    private static Event CloneForOccurrence(Event head, DateTimeOffset start)
    {
        var duration = head.End - head.Start;
        return new Event
        {
            Id = string.Empty,
            Title = head.Title,
            Body = head.Body,
            ComponentId = head.ComponentId,
            GroupId = head.GroupId,
            AuthorId = head.AuthorId,
            Start = start,
            End = start + duration,
            Location = head.Location,
            Capacity = head.Capacity,
            Color = head.Color,
            Audience = head.Audience,
            ReminderEnabled = head.ReminderEnabled,
            IsDraft = head.IsDraft,
            IsDeleted = false,
            LanguageCode = head.LanguageCode,
            TagIds = head.TagIds,
            ImageIds = head.ImageIds,
            AttachmentIds = head.AttachmentIds,
            RecurrenceHeadId = head.Id,
            RecurrenceRule = null,
            Created = head.Created,
            Modified = null,
        };
    }
}
