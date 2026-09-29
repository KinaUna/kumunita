using Marten;

namespace Kumunita.Core.Usage;

/// <summary>
/// The D3 aggregation seam impl (ADR 0114; design doc §aggregation,
/// verbatim). Touches <b>only</b> <see cref="UsageEvent"/> (the
/// <c>AspNetUsers</c> table is never read — the "NewSignups dropped"
/// deferral). <b>Zero new authorization surface</b> (C-M13·6 — the seam
/// is called by the one GlobalAdmin-gated controller; the row is not an
/// auditable resource).
/// <para>
/// <b>Marten parser fallback (recorded per the §aggregation contract):</b>
/// the three aggregation shapes whose SQL translation is non-trivial for
/// the provider — <see cref="SurfaceKey.Map"/> inside a LINQ
/// <c>GroupBy</c>, the <c>Distinct()</c> before <c>Count()</c>, and the
/// anonymous-type <c>GroupBy</c> over <c>(Date, Surface)</c> — are
/// evaluated <b>Linq-to-objects over the window's row set</b> (one
/// server-side <c>Where(e =&gt; e.At &gt;= cutoff)</c> +
/// <c>ToListAsync</c>, then client-side group/count/sort). The
/// deterministic results are identical to the design doc's Linq
/// shapes, and the pins assert the <em>results</em> (D7:
/// "the pin still asserts the result, not the query shape"), not the
/// SQL emitted — so the fallback is invisible to the contract.
/// </para>
/// </summary>
public sealed class UsageAnalyticsService : IUsageAnalyticsService
{
    private readonly IDocumentStore _store;

    public UsageAnalyticsService(IDocumentStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc/>
    public async Task<UsageAnalyticsResult> GetWindowAsync(int windowDays)
    {
        // The D3 pin: the window is 7/30/90; an unknown value throws
        // ArgumentOutOfRangeException (not a 0-row query).
        _ = NormalizeWindow(windowDays);

        // The cutoff is computed at the seam call (not at the query — the
        // Aggregation_Window_Excludes_Older_Rows pin plants a row at
        // now − 91d and asserts it is excluded from the 90-day window).
        var cutoff = DateTimeOffset.UtcNow.AddDays(-windowDays);

        // One server-side query: the window's rows (the only clause the
        // provider must translate — a plain Where over a column compare).
        await using var session = _store.QuerySession();
        var rows = await session.Query<UsageEvent>()
            .Where(e => e.At >= cutoff)
            .ToListAsync();

        var total = rows.Count;
        var authenticatedTotal = rows.Count(e => !string.IsNullOrEmpty(e.ActorId));
        var anonymousTotal = rows.Count(e => string.IsNullOrEmpty(e.ActorId));

        // DistinctActors = CountDistinct over non-empty ActorId. Recorded
        // fallback (see class doc): the projection list is collected and
        // deduped client-side — the ToHashSet().Count() shape, the same
        // result as the design doc's Distinct().Count() Linq shape.
        var distinctActors = rows
            .Where(e => !string.IsNullOrEmpty(e.ActorId))
            .Select(e => e.ActorId)
            .ToHashSet()
            .Count;

        // SurfaceRanking = GroupBy(SurfaceKey.Map(e.RouteTemplate)) →
        // SurfaceRow, OrderByDescending(Total).ThenBy(Surface) — the D3
        // ordering pin (descending by Total, ties ascending by Surface).
        var surfaceRanking = rows
            .GroupBy(e => SurfaceKey.Map(e.RouteTemplate))
            .Select(g => new SurfaceRow
            {
                Surface = g.Key,
                Total = g.Count()
            })
            .OrderByDescending(r => r.Total)
            .ThenBy(r => r.Surface)
            .ToList();

        return new UsageAnalyticsResult
        {
            WindowDays = windowDays,
            Total = total,
            AuthenticatedTotal = authenticatedTotal,
            AnonymousTotal = anonymousTotal,
            DistinctActors = distinctActors,
            SurfaceRanking = surfaceRanking
        };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UsageCsvRow>> GetCsvRowsAsync(int windowDays)
    {
        // The same switch pin (unknown → ArgumentOutOfRangeException, not a
        // 0-row query).
        _ = NormalizeWindow(windowDays);

        var cutoff = DateTimeOffset.UtcNow.AddDays(-windowDays);

        await using var session = _store.QuerySession();
        var rows = await session.Query<UsageEvent>()
            .Where(e => e.At >= cutoff)
            .ToListAsync();

        // The per-day-per-surface breakdown the CSV exports (recorded
        // fallback, see class doc): GroupBy over (Date, Surface), sorted
        // Date ascending then Surface ascending.
        return rows
            .GroupBy(e => new { Date = e.At.Date, Surface = SurfaceKey.Map(e.RouteTemplate) })
            .Select(g => new UsageCsvRow
            {
                Date = new DateTimeOffset(g.Key.Date, TimeSpan.Zero),
                Surface = g.Key.Surface,
                Total = g.Count()
            })
            .OrderBy(r => r.Date)
            .ThenBy(r => r.Surface)
            .ToList();
    }

    /// <summary>
    /// The D3 window pin: one of 7/30/90; an unknown value throws
    /// <see cref="ArgumentOutOfRangeException"/> (not a 0-row query).
    /// </summary>
    private static int NormalizeWindow(int windowDays)
    {
        switch (windowDays)
        {
            case 7:
            case 30:
            case 90:
                return windowDays;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(windowDays),
                    windowDays,
                    "The usage window is pinned to 7, 30, or 90 days (ADR 0114 D3).");
        }
    }
}
