namespace Kumunita.Core.Localization;

/// <summary>
/// The date-time <i>format</i> surface (ADR 0020) — a small, shared source for
/// the curated <b>presets</b> a resident (or an admin, for the platform
/// default) can pick, the <b>floor</b> the resolution order falls through to,
/// and the <b>validation</b> both write lanes run <b>before</b> they write
/// (fail-closed, the <see cref="LocalizationService.SetDefaultTimezoneAsync"/>
/// pin).
/// <para>
/// <b>Presets are just well-known format strings.</b> The platform stores the
/// .NET custom datetime format string <i>itself</i> on
/// <see cref="LocaleSettings.DefaultDateFormat"/> and
/// <see cref="UserInfo.Profile.DateFormat"/> — <b>not</b> a preset id — so a
/// custom format (one not in <see cref="Presets"/>) is simply another value.
/// "Custom" is therefore not a special case in storage; it is the picker's
/// "none of the presets match the current value" state. The presets are the
/// curated short list the picker offers; adding a preset later is a one-line
/// change to <see cref="Presets"/> and no migration.
/// </para>
/// <para>
/// <b>Time only, no seconds.</b> The presets carry a 24-hour clock
/// (<c>HH:mm</c>) so a time reads the same on every resident's screen; the
/// <i>date</i> portion is what the presets differ on (the "is <c>1/2/2026</c>
/// Jan 2 or Feb 1" ambiguity the feature exists to remove). The presets and the
/// floor are all <c>24-hour</c>; a custom format may use any .NET custom
/// datetime format string the author intends.
/// </para>
/// <para>
/// <b>Core-only, HTTP-free.</b> This is a pure <see cref="System"/> helper
/// (ADR 0006-D holds: no <c>IHttpContext</c>, no cookie, no claim). The Web
/// layer (the <c>kw-dt</c> TagHelper, the <c>/admin/dateformat</c> +
/// <c>/settings/dateformat</c> surfaces) reads <see cref="Presets"/> /
/// <see cref="FloorFormat"/> and calls <see cref="IsValid"/> without the Core
/// services touching HTTP.
/// </para>
/// </summary>
public static class DateFormat
{
    /// <summary>
    /// The floor — the format string the resolution order falls through to when
    /// neither the resident's <see cref="UserInfo.Profile.DateFormat"/> override
    /// nor the instance default is present (ADR 0020, the <c>UTC</c> floor of
    /// ADR 0019 and the <c>en</c> floor of ADR 0005). The "Long" preset
    /// (<see cref="LongFormat"/>): <c>January 2, 2026 15:04</c> — the least
    /// ambiguous of the four (the month is fully spelled out, the day and year
    /// can never be confused with each other).
    /// </summary>
    public const string FloorFormat = LongFormat;

    /// <summary>The "Long" preset — <c>MMMM d, yyyy HH:mm</c> →
    /// <c>January 2, 2026 15:04</c>.</summary>
    public const string LongFormat = "MMMM d, yyyy HH:mm";

    /// <summary>The "Short" preset — <c>MMM d, yyyy HH:mm</c> →
    /// <c>Jan 2, 2026 15:04</c>.</summary>
    public const string ShortFormat = "MMM d, yyyy HH:mm";

    /// <summary>The "ISO" preset — <c>yyyy-MM-dd HH:mm</c> →
    /// <c>2026-01-02 15:04</c> (sorts and reads the same in any locale).</summary>
    public const string IsoFormat = "yyyy-MM-dd HH:mm";

    /// <summary>The "Day-first" preset — <c>dd/MM/yyyy HH:mm</c> →
    /// <c>02/01/2026 15:04</c> (the common European convention).</summary>
    public const string DayFirstFormat = "dd/MM/yyyy HH:mm";

    /// <summary>
    /// A (display-label, format-string) pair — the one unit the picker renders
    /// and the value stored. The label is the resident-facing name
    /// (<c>"Long"</c>, <c>"Short"</c>, …); the format is the .NET custom
    /// datetime format string that is actually stored + rendered. The label is
    /// <b>not</b> stored — only the format — so a custom format that isn't one
    /// of these still round-trips (the picker just renders it as "Custom…").
    /// </summary>
    public sealed record Preset(string Label, string Format);

    /// <summary>
    /// The curated preset list, in picker order. The first preset (the "Long"
    /// format) is also the floor (<see cref="FloorFormat"/>). Adding a preset
    /// here is the only change needed to offer it — the picker, the write
    /// lanes, and the resolver all read from this list, and storage carries the
    /// format string (not an id) so nothing downstream changes.
    /// </summary>
    public static IReadOnlyList<Preset> Presets { get; } = new List<Preset>
    {
        new("Long", LongFormat),
        new("Short", ShortFormat),
        new("ISO", IsoFormat),
        new("Day-first", DayFirstFormat),
    };

    /// <summary>
    /// The preset whose <see cref="Preset.Format"/> matches
    /// <paramref name="formatString"/> (ordinal, case-sensitive — .NET format
    /// tokens are case-sensitive: <c>H</c> ≠ <c>h</c>, <c>M</c> ≠ <c>m</c>), or
    /// <c>null</c> when the value is a custom format (not one of the presets).
    /// The picker uses this to mark the selected option "Custom…" when no preset
    /// matches.
    /// </summary>
    public static Preset? Match(string? formatString)
    {
        if (string.IsNullOrWhiteSpace(formatString))
            return null;

        foreach (var p in Presets)
        {
            if (string.Equals(p.Format, formatString, StringComparison.Ordinal))
                return p;
        }

        return null;
    }

    /// <summary>
    /// Whether <paramref name="formatString"/> is a usable .NET custom datetime
    /// format string — i.e. applying it to a real instant does not throw. Both
    /// write lanes call this <b>before</b> any write (fail-closed, the
    /// <c>SetDefaultTimezoneAsync</c> pin): a blank string or a string that
    /// .NET cannot apply (a stray <c>FormatException</c> from an unpaired
    /// format token) throws at the Web boundary and is never stored.
    /// <para>
    /// The sample instant is fixed so the check is deterministic (no DST /
    /// locale dependence): <c>2026-01-02 15:04:00</c> UTC.
    /// </para>
    /// </summary>
    public static bool IsValid(string? formatString)
    {
        if (string.IsNullOrWhiteSpace(formatString))
            return false;

        try
        {
            // Applying the format to a real instant is the validation: a
            // malformed custom format (e.g. a stray `K` with no `d`, or an
            // unpaired token) throws FormatException here — caught and reported
            // as "not a usable format", never stored.
            _ = new System.DateTime(2026, 1, 2, 15, 4, 0, System.DateTimeKind.Utc)
                .ToString(formatString, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            // FormatException / ArgumentException — not a format .NET can apply.
            return false;
        }
    }
}
