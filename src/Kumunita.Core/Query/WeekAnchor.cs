namespace Kumunita.Core.Query;

/// <summary>
/// The one shared <b>first-day-of-week</b> resolution + day-ordering (the
/// resident's per-user preference, <see cref="UserInfo.Profile.FirstDayOfWeek"/>,
/// ADR 0169). One constant + two pure helpers so no surface duplicates the
/// default or the reordering.
/// <para>
/// <see cref="Default"/> (<see cref="System.DayOfWeek.Monday"/> = 1) is the
/// platform default: a resident who has not set a preference
/// (<c>Profile.FirstDayOfWeek == null</c>) — or any surface with no actor
/// context — anchors the week on Monday (the calendar's existing ISO
/// Monday-start shape). The two helpers are pure (no I/O, no actor): the
/// caller resolves the preference and passes the value; this only normalizes
/// it and orders the day-of-week set. A stored value outside 0…6 (a
/// pre-existing row written by an older build, or a direct write) resolves to
/// <see cref="Default"/> — the ADR 0019 "resolve a bad value to the floor"
/// posture, not a fail-closed throw (a display preference is never a gate).
/// </para>
/// </summary>
public static class WeekAnchor
{
    /// <summary>The platform default first day of the week (Monday,
    /// <see cref="System.DayOfWeek.Monday"/> = 1) — the user preference is
    /// additive on top of this (<c>null</c> ⇒ <see cref="Default"/>).</summary>
    public const int Default = 1;

    /// <summary>
    /// Resolve a resident's <see cref="UserInfo.Profile.FirstDayOfWeek"/>
    /// preference (or <c>null</c> = unset) to a concrete <see
    /// cref="System.DayOfWeek"/> ordinal (0 = Sunday … 6 = Saturday):
    /// <c>null</c> ⇒ <see cref="Default"/> (Monday); a value in 0…6 ⇒ the
    /// value as-is; anything else ⇒ <see cref="Default"/>. Pure: no I/O, no
    /// actor — the caller resolves the preference and passes the value.
    /// </summary>
    public static int Resolve(int? preferred)
        => preferred is >= 0 and <= 6 ? preferred.Value : Default;

    /// <summary>
    /// The seven day-of-week ordinals (0 = Sunday … 6 = Saturday) ordered to
    /// start at <paramref name="firstDay"/> and wrap around — the option
    /// list every day-of-week selector (the Quiet-hours / Guardian
    /// time-limit pickers) renders so the resident's preferred first day is
    /// on top. <c>firstDay = 0</c> ⇒ the natural Sunday…Saturday order (the
    /// pre-preference order); <c>firstDay = 1</c> ⇒ Monday…Sunday. Pure.
    /// </summary>
    public static int[] OrderDays(int firstDay)
    {
        var f = Resolve(firstDay);
        var result = new int[7];
        for (var i = 0; i < 7; i++)
            result[i] = (f + i) % 7;
        return result;
    }
}
