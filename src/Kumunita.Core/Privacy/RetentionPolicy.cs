namespace Kumunita.Core.Privacy;

/// <summary>
/// The retention **decision** surface (IMPROVE-SEC D6 / E5), extracted as a
/// pure, store-free <c>*Policy</c> static (the
/// <see cref="Kumunita.Core.Usage.UsagePurgePolicy"/> precedent — a pure
/// static over <c>now</c> + the retention constant that a plain unit test
/// names without a store).
/// <para>
/// The <b>decision</b>: given <c>now</c> + a retention key's day-count
/// (<see cref="RetentionOptions"/>'s config-bound value), the retention
/// <b>cutoff</b> instant (<c>now − days</c>) — or **no cutoff at all** when the
/// key is <c>0</c> or absent (keep: the safe side; a <c>0</c>-valued or
/// unknown key never purges a row). The I/O leg — the
/// <see cref="Kumunita.Core.Usage.UsagePurgeTick"/>-shaped
/// <c>RetentionTick</c> record, the <c>RetentionService</c> delete/null
/// commits, the Wolverine handler and the <c>Program.cs</c> seed — is IS34a /
/// IS34b; it must read the days from <see cref="RetentionOptions"/> and call
/// <see cref="CutoffFor"/> here, and must **never** re-derive the cutoff.
/// </para>
/// Pure — no store, no I/O, no <c>Marten</c>, no <c>Wolverine</c>.
/// </summary>
public static class RetentionPolicy
{
    /// <summary>
    /// The retention cutoff for a key (D6): <paramref name="now"/> minus the
    /// key's day-count (<c>RetentionOptions.DaysFor</c> — the single source of
    /// the key → value mapping). A key set to <c>0</c> (or an unknown key)
    /// yields **no cutoff** — the caller keeps every row. Pure — no store, no
    /// I/O.
    /// </summary>
    /// <param name="options">The config-bound day-counts (the <c>Retention__*</c> surface).</param>
    /// <param name="key">The retention key (e.g. <c>"Sessions"</c>).</param>
    /// <param name="now">The "now" instant (the I/O method's injection point for a deterministic boundary).</param>
    /// <returns>The cutoff instant (<c>now − days</c>); rows with <c>At &lt; cutoff</c> are purged — or <c>null</c> when the key is <c>0</c> / unknown (keep).</returns>
    public static DateTimeOffset? CutoffFor(
        RetentionOptions options,
        string key,
        DateTimeOffset now)
    {
        int days = options.DaysFor(key);
        return days > 0 ? now.AddDays(-days) : null;
    }

    /// <summary>
    /// The keep predicate: a key's rows are **kept** (never purged) when its
    /// day-count is <c>0</c> or the key is unknown — the fail-safe side. The
    /// inverse of a non-null <see cref="CutoffFor"/>. Pure — no store, no I/O.
    /// </summary>
    /// <param name="options">The config-bound day-counts (the <c>Retention__*</c> surface).</param>
    /// <param name="key">The retention key (e.g. <c>"Sessions"</c>).</param>
    /// <returns><c>true</c> when the key is <c>0</c> / unknown (keep); <c>false</c> when a positive cutoff applies.</returns>
    public static bool Keep(RetentionOptions options, string key)
        => options.DaysFor(key) <= 0;
}
