namespace Kumunita.Core.Notifications;

/// <summary>
/// M20 (ADR 0121, D3) — the pure "is it quiet now?" evaluator. A **pure
/// function** (no session, no HTTP, no IO — the <c>IcsWriter</c> /
/// <c>UsageCapturePolicy</c> "pure, closed, testable in isolation" discipline):
/// it takes the resolved (schedule, instant, effective zone) and returns a
/// <c>bool</c>. It converts <paramref name="now"/> to
/// <paramref name="effectiveZone"/>'s **wall clock** (ADR 0019 — the instant is
/// converted to the zone's wall clock *first*, then the hour/day are read,
/// exactly the <c>kw-dt</c> TagHelper + <c>EventReminderService</c> resolution
/// order), then evaluates the schedule's <c>Mode</c>/<c>Hours</c>/
/// <c>DaysOfWeek</c> against that wall clock. <c>null</c> / <c>Enabled ==
/// false</c> → <c>false</c> (never quiet, C-M20·3). An empty <c>Hours</c> means
/// "all hours"; an empty <c>DaysOfWeek</c> means "all days".
/// </summary>
public static class QuietScheduleEvaluator
{
    /// <summary>
    /// D3 — pure, timezone-aware "is it quiet now?" A <c>null</c> schedule is
    /// never quiet (the floor, C-M20·3); a disabled schedule is never quiet
    /// (the D2 master on/off). Otherwise the instant is converted to
    /// <paramref name="effectiveZone"/>'s wall clock FIRST (ADR 0019), then:
    /// <see cref="QuietScheduleMode.Blocked"/> — quiet iff (Hours empty OR
    /// wallHour in Hours) AND (DaysOfWeek empty OR wallDow in DaysOfWeek).
    /// <see cref="QuietScheduleMode.Allowed"/> — the INVERSE: quiet iff NOT
    /// (wallHour in Hours OR wallDow in DaysOfWeek) — the email flows only
    /// during a listed hour/day, held otherwise (an allowed schedule with both
    /// lists empty is "always quiet").
    /// </summary>
    public static bool IsQuietNow(
        NotificationQuietSchedule? schedule,
        DateTimeOffset now,
        TimeZoneInfo effectiveZone)
    {
        ArgumentNullException.ThrowIfNull(effectiveZone);

        // The floor: no schedule, or the master off → never quiet (C-M20·3).
        if (schedule is null || !schedule.Enabled)
            return false;

        // ADR 0019 — wall-clock-first: convert the instant to the zone's local
        // time, THEN read the hour + day (the kw-dt TagHelper shape).
        var wall = TimeZoneInfo.ConvertTime(now, effectiveZone);

        // An empty axis = "all" (the D2 "empty = all hours / all days" reading).
        var hourMatches   = schedule.Hours.Length     == 0 || schedule.Hours.Contains(wall.Hour);
        var dayMatches    = schedule.DaysOfWeek.Length == 0 || schedule.DaysOfWeek.Contains((int)wall.DayOfWeek);
        var windowMatches = hourMatches && dayMatches;   // the schedule's window contains the instant

        // Blocked = quiet DURING the window (the lean default); Allowed = quiet
        // EXCEPT the window (the allow-list).
        return schedule.Mode == QuietScheduleMode.Blocked
            ? windowMatches
            : !windowMatches;
    }
}
