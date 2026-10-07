namespace Kumunita.Core.UserInfo;

/// <summary>
/// M28 (ADR 0151, D2) — the time-limit mode: the *polarity* of the schedule.
/// <see cref="Blocked"/> (the lean default) = "restricted DURING the listed
/// hours/days" (a block-list — the intuitive "no platform at night" reading).
/// <see cref="Allowed"/> = "restricted EXCEPT the listed hours/days" (an
/// allow-list — the child may use the platform only during the listed windows).
/// A closed two-value enum: a typo or a wrong value is impossible (the ADR
/// 0120 D4 "closed, enumerated set" idiom), and a new mode is a new enum value
/// (an additive append, never a free-form string).
/// </summary>
public enum TimeLimitMode
{
    /// <summary>Restricted DURING the listed hours/days (the lean default).</summary>
    Blocked = 0,
    /// <summary>Restricted EXCEPT the listed hours/days (the allow-list reading).</summary>
    Allowed = 1,
}

/// <summary>
/// M28 (ADR 0151, D2) — the per-child guardian time-limit schedule. One
/// document per child (id = the child's <see cref="ChildId"/> — the
/// <see cref="Notifications.NotificationQuietSchedule"/> "one row per subject"
/// shape, ADR 0121 D2, with <c>ChildId</c> in place of <c>RecipientId</c>;
/// identity pinned to <c>ChildId</c> in <c>M1DocTypes</c>, next to
/// <see cref="GuardianLink"/>). A child with two guardians shares **one**
/// schedule (the <see cref="GuardianLink"/> multi-guardian shape).
/// <see cref="Enabled"/> is the master on/off: <c>false</c> = "never
/// restricted" (the floor — the child is always allowed, exactly as pre-M28,
/// C-M28·3). When <see cref="Enabled"/> is <c>true</c>, the allow/restrict
/// verdict is the pure <c>GuardianTimeLimitEvaluator.IsAllowedNow</c> (U02)
/// over <see cref="Mode"/> / <see cref="Hours"/> / <see cref="DaysOfWeek"/> in
/// the **child's** effective zone (D3, C-M28·6). <see cref="Hours"/> (0–23)
/// and <see cref="DaysOfWeek"/> (0=Sunday…6=Saturday) are independent axes; an
/// empty array means "all" (a schedule with only hours set is restricted on
/// every day during those hours, in <see cref="TimeLimitMode.Blocked"/> mode).
/// </summary>
public sealed class GuardianTimeLimitSchedule
{
    /// <summary>Document identity — the child's subject id (one schedule per
    /// child; the same id as the child row's <see cref="GuardianLink.ChildId"/>).</summary>
    public string ChildId { get; set; } = string.Empty;

    /// <summary>
    /// The master on/off (D2, C-M28·3). <c>false</c> = "never restricted" —
    /// the floor: the child is always allowed, exactly as pre-M28, regardless
    /// of <see cref="Mode"/> / <see cref="Hours"/> / <see cref="DaysOfWeek"/>.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// The schedule's mode (D2): <see cref="TimeLimitMode.Blocked"/>
    /// (restricted DURING the listed hours/days, the lean default) vs
    /// <see cref="TimeLimitMode.Allowed"/> (restricted EXCEPT the listed
    /// hours/days, the allow-list).
    /// </summary>
    public TimeLimitMode Mode { get; set; } = TimeLimitMode.Blocked;

    /// <summary>
    /// The hours of day (0–23) the mode applies to. **Empty = the mode applies
    /// to ALL hours** (the "all hours" reading — a <see cref="TimeLimitMode.Blocked"/>
    /// schedule with only days-of-week set is restricted on those days all day).
    /// </summary>
    public int[] Hours { get; set; } = [];

    /// <summary>
    /// The days of week (0=Sunday…6=Saturday) the mode applies to. **Empty =
    /// the mode applies to ALL days** (a schedule with only hours set is
    /// restricted on every day during those hours).
    /// </summary>
    public int[] DaysOfWeek { get; set; } = [];

    /// <summary>The instant the guardian last saved/cleared this schedule (for
    /// display; not a gate).</summary>
    public DateTimeOffset? Updated { get; set; }
}
