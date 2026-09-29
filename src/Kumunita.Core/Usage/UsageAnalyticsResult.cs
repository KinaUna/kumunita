namespace Kumunita.Core.Usage;

/// <summary>
/// One surface row in the <see cref="UsageAnalyticsResult.SurfaceRanking"/>
/// (D3). <see cref="Surface"/> is the <see cref="SurfaceKey"/> map result
/// (the §surface-key closed list); <see cref="Total"/> is the count of
/// <see cref="UsageEvent"/> rows in the window mapping to that surface.
/// </summary>
public sealed class SurfaceRow
{
    public string Surface { get; init; } = string.Empty;
    public int    Total   { get; init; }
}

/// <summary>
/// One per-day-per-surface row in the CSV export (D3 / D4).
/// <see cref="Date"/> is the <see cref="UsageEvent.At"/>'s UTC date;
/// <see cref="Surface"/> is the <see cref="SurfaceKey"/> map result;
/// <see cref="Total"/> is the count for that (date, surface) pair.
/// </summary>
public sealed class UsageCsvRow
{
    public DateTimeOffset Date    { get; init; }
    public string         Surface { get; init; } = string.Empty;
    public int            Total   { get; init; }
}

/// <summary>
/// The D3 aggregation result (ADR 0114). The <see cref="SurfaceRanking"/>
/// is sorted by <see cref="SurfaceRow.Total"/> <b>descending</b>, ties
/// broken by <see cref="SurfaceRow.Surface"/> <b>ascending</b> (the
/// deterministic ordering the pin test asserts, D7). <b>
/// <c>NewSignups</c> is deliberately absent</b> (the D3 deferral — the
/// <c>User : IdentityUser</c> table has no created-at column).
/// </summary>
public sealed class UsageAnalyticsResult
{
    public int                          WindowDays         { get; init; }
    public int                          Total              { get; init; }
    public int                          AuthenticatedTotal { get; init; }
    public int                          AnonymousTotal     { get; init; }
    public int                          DistinctActors     { get; init; }
    public IReadOnlyList<SurfaceRow>    SurfaceRanking     { get; init; } =
        System.Array.Empty<SurfaceRow>();
}
