namespace Kumunita.Core.Usage;

/// <summary>
/// The D3 aggregation seam (ADR 0114). One instance, registered
/// <c>AddTransient</c> in <c>DependencyInjection.cs</c> (the house
/// "interface + one impl + DI registration" shape). Two methods, both
/// over a <b>pinned window</b> (7/30/90; an unknown value throws
/// <see cref="ArgumentOutOfRangeException"/>, not a 0-row query). The
/// service touches <b>only</b> <see cref="UsageEvent"/>; the
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
}
