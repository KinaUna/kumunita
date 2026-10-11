namespace Kumunita.Core.Usage;

/// <summary>
/// The D3 aggregation seam (ADR 0114). One instance, registered
/// <c>AddTransient</c> in <c>DependencyInjection.cs</c> (the house
/// "interface + one impl + DI registration" shape). Three methods (M34 adds
/// the 3rd, ADR 0162): the 2 M13 methods over a <b>pinned window</b>
/// (7/30/90; an unknown value throws <see cref="ArgumentOutOfRangeException"/>,
/// not a 0-row query) + the M34 <see cref="GetHistoryAsync"/> over a
/// <b>pinned window</b> (7/30/90/180/365; same unknown-value-throws posture,
/// M34·7). The service touches <b>only</b> <see cref="UsageEvent"/>; the
/// <c>AspNetUsers</c> table is never read (the D3 "NewSignups dropped"
/// deferral — §deferred lanes). <b>Zero new authorization surface</b>
/// (C-M13·6 — the seam is called by the one GlobalAdmin-gated
/// controller; the <c>UsageEvent</c> row is not an auditable resource).
/// </summary>
public interface IUsageAnalyticsService
{
    /// <param name="windowDays">The pinned window — one of 7, 30, 90 (an unknown value throws <see cref="ArgumentOutOfRangeException"/>).</param>
    Task<UsageAnalyticsResult> GetWindowAsync(int windowDays);

    /// <param name="windowDays">The pinned window — one of 7, 30, 90 (an unknown value throws <see cref="ArgumentOutOfRangeException"/>).</param>
    Task<IReadOnlyList<UsageCsvRow>> GetCsvRowsAsync(int windowDays);

    /// <summary>
    /// The M34 history read (ADR 0162, M34·1 / M34·7) — one
    /// <see cref="UsageDailyTotal"/> per **present** UTC day in the trailing
    /// <paramref name="days"/>-day window, <see cref="UsageDailyTotal.Date"/>
    /// **ascending**, **only the days present** (no fabricated zero rows).
    /// <para>
    /// <paramref name="days"/> is a **pinned** value in
    /// <c>{7, 30, 90, 180, 365}</c>; an unknown value throws
    /// <see cref="ArgumentOutOfRangeException"/> (**not** a 0-row query) —
    /// the M34·7 pin, the M13 <c>GetWindowAsync</c> "unknown value throws"
    /// precedent (the M13 7/30/90 set **extended** with 180/365 for the
    /// over-time view).
    /// </para>
    /// <para>
    /// **Zero** <c>AccessAudit</c> rows (M34·4 — the read is not an
    /// auditable resource; the M13 CSV export's **one** row is **untouched**).
    /// The read is over the **existing** <see cref="UsageEvent"/> rows
    /// (M34·1 derived, not captured — no new doc, no new doc-type surface,
    /// no capture tick / handler / boot-seed).
    /// </para>
    /// </summary>
    /// <param name="days">The pinned window — one of 7, 30, 90, 180, 365 (an unknown value throws <see cref="ArgumentOutOfRangeException"/>).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<UsageHistoryResult> GetHistoryAsync(int days, CancellationToken ct = default);
}
