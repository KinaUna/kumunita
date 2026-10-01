namespace Kumunita.Core.Notifications;

/// <summary>
/// M20 (ADR 0121, D2) — the quiet-schedule mode: the *polarity* of the
/// schedule. <see cref="Blocked"/> (the lean default) = "quiet DURING the
/// listed hours/days" (a block-list — the intuitive "quiet at night"
/// reading). <see cref="Allowed"/> = "quiet EXCEPT the listed hours/days"
/// (an allow-list — the email flows during the listed windows, held
/// otherwise). A closed two-value enum: a typo or a wrong value is impossible
/// (the ADR 0120 D4 "closed, enumerated set" idiom), and a new mode is a new
/// enum value (an additive append, never a free-form string).
/// </summary>
public enum QuietScheduleMode
{
    /// <summary>Held DURING the listed hours/days (the lean default).</summary>
    Blocked = 0,
    /// <summary>Held EXCEPT the listed hours/days (the allow-list reading).</summary>
    Allowed = 1,
}

/// <summary>
/// M20 (ADR 0121, D2) — the per-resident notification-quiet schedule. One
/// document per recipient (id = the recipient's <see cref="RecipientId"/> —
/// the <see cref="NotificationPreference"/> "one row per recipient" shape;
/// identity pinned to <c>RecipientId</c> in <c>M6DocTypes</c>).
/// <see cref="Enabled"/> is the master on/off: <c>false</c> = "never quiet"
/// (the floor — every notification's email flows, exactly as pre-M20,
/// C-M20·3). When <see cref="Enabled"/> is <c>true</c>, the quiet verdict is
/// the pure <see cref="QuietScheduleEvaluator.IsQuietNow"/> over
/// <see cref="Mode"/> / <see cref="Hours"/> / <see cref="DaysOfWeek"/> in the
/// recipient's own effective zone (D3, C-M20·2). <see cref="Hours"/> (0–23)
/// and <see cref="DaysOfWeek"/> (0=Sunday…6=Saturday) are independent axes; an
/// empty array means "all" (a schedule with only hours set is quiet on every
/// day during those hours).
/// </summary>
public sealed class NotificationQuietSchedule
{
    /// <summary>Document identity — the recipient's subject id (one schedule
    /// per recipient; the same id as <see cref="Notification.RecipientId"/>).</summary>
    public string RecipientId { get; set; } = string.Empty;

    /// <summary>
    /// The master on/off (D2, C-M20·3). <c>false</c> = "never quiet" — the
    /// floor: every notification's email flows, exactly as pre-M20, regardless
    /// of <see cref="Mode"/> / <see cref="Hours"/> / <see cref="DaysOfWeek"/>.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// The schedule's mode (D2): <see cref="QuietScheduleMode.Blocked"/>
    /// (held DURING the listed hours/days, the lean default) vs
    /// <see cref="QuietScheduleMode.Allowed"/> (held EXCEPT the listed
    /// hours/days, the allow-list).
    /// </summary>
    public QuietScheduleMode Mode { get; set; } = QuietScheduleMode.Blocked;

    /// <summary>
    /// The hours of day (0–23) the mode applies to. **Empty = the mode applies
    /// to ALL hours** (the "all hours" reading — a schedule with only
    /// days-of-week set is quiet on those days all day).
    /// </summary>
    public int[] Hours { get; set; } = [];

    /// <summary>
    /// The days of week (0=Sunday…6=Saturday) the mode applies to. **Empty =
    /// the mode applies to ALL days** (a schedule with only hours set is quiet
    /// on every day during those hours).
    /// </summary>
    public int[] DaysOfWeek { get; set; } = [];

    /// <summary>The instant the resident last saved/cleared this schedule (for
    /// display; not a gate).</summary>
    public DateTimeOffset? Updated { get; set; }
}
