namespace Kumunita.Core.UserInfo;

/// <summary>
/// M28 (ADR 0151, D3) — the pure "is the child allowed now?" evaluator. A
/// **pure function** (no session, no HTTP, no IO — the <c>IcsWriter</c> /
/// <c>UsageCapturePolicy</c> "pure, closed, testable in isolation" discipline):
/// it takes the resolved (schedule, instant, effective zone) and returns a
/// <c>bool</c> — <c>true</c> = the child MAY use the platform now. It converts
/// <paramref name="now"/> to <paramref name="effectiveZone"/>'s **wall clock**
/// (ADR 0019 — the instant is converted to the zone's wall clock *first*, then
/// the hour/day are read, exactly the <c>kw-dt</c> TagHelper +
/// <c>EventReminderService</c> resolution order), then evaluates the
/// schedule's <c>Mode</c>/<c>Hours</c>/<c>DaysOfWeek</c> against that wall
/// clock. <c>null</c> / <c>Enabled == false</c> → <c>true</c> (allowed, the
/// floor, C-M28·3). An empty <c>Hours</c> means "all hours"; an empty
/// <c>DaysOfWeek</c> means "all days". A **separate** pure function from M20's
/// <c>QuietScheduleEvaluator</c> (so the <c>UserInfo</c> context does not
/// depend on <c>Notifications</c>), sharing the exact evaluation discipline.
/// </summary>
public static class GuardianTimeLimitEvaluator
{
    /// <summary>
    /// D3 — pure, timezone-aware "is the child allowed now?" A <c>null</c>
    /// schedule is always allowed (the floor, C-M28·3); a disabled schedule is
    /// always allowed (the D2 master on/off). Otherwise the instant is
    /// converted to <paramref name="effectiveZone"/>'s wall clock FIRST (ADR
    /// 0019), then: <see cref="TimeLimitMode.Blocked"/> — restricted DURING the
    /// window, so allowed iff the wall clock is OUTSIDE the window
    /// (<c>!windowMatches</c>). <see cref="TimeLimitMode.Allowed"/> — restricted
    /// EXCEPT the window, so allowed iff the wall clock is INSIDE the window
    /// (<c>windowMatches</c>). (A <c>Blocked</c> schedule with both axes empty
    /// is the "all hours × all days" window → always restricted, the "total
    /// block" reading; a <c>Allowed</c> schedule with both axes empty is the
    /// "all hours × all days" window → always allowed, the floor.)
    /// </summary>
    public static bool IsAllowedNow(
        GuardianTimeLimitSchedule? schedule,
        DateTimeOffset now,
        TimeZoneInfo effectiveZone)
    {
        ArgumentNullException.ThrowIfNull(effectiveZone);

        // The floor: no schedule, or the master off → always allowed (C-M28·3).
        if (schedule is null || !schedule.Enabled)
            return true;

        // ADR 0019 — wall-clock-first: convert the instant to the zone's local
        // time, THEN read the hour + day (the kw-dt TagHelper shape).
        var wall = TimeZoneInfo.ConvertTime(now, effectiveZone);

        // An empty axis = "all" (the D2 "empty = all hours / all days" reading).
        var hourMatches   = schedule.Hours.Length     == 0 || schedule.Hours.Contains(wall.Hour);
        var dayMatches    = schedule.DaysOfWeek.Length == 0 || schedule.DaysOfWeek.Contains((int)wall.DayOfWeek);
        var windowMatches = hourMatches && dayMatches;   // the schedule's window contains the instant

        // Blocked = restricted DURING the window (the lean default) → allowed iff
        // OUTSIDE. Allowed = restricted EXCEPT the window (the allow-list) →
        // allowed iff INSIDE. (The mirror of M20's IsQuietNow — the polarity is
        // inverted because here *allowed* is the permission, not the restriction.)
        return schedule.Mode == TimeLimitMode.Blocked
            ? !windowMatches
            : windowMatches;
    }
}
