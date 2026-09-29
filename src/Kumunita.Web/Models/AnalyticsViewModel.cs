using Kumunita.Core.Usage;

namespace Kumunita.Web.Models;

/// <summary>
/// The view model for <c>/admin/analytics</c> (ADR 0114 D4). The six fields
/// mirror the <see cref="UsageAnalyticsResult"/> (the D3 contract) — the
/// projection is the controller's job; this POCO carries nothing the
/// <see cref="UsageAnalyticsResult"/> does not already carry (no per-account
/// data — C-M13·3: the rendered surface shows aggregates over the window,
/// never a row per <c>ActorId</c>).
/// </summary>
public class AnalyticsViewModel
{
    public int                          WindowDays         { get; init; }
    public int                          Total              { get; init; }
    public int                          AuthenticatedTotal { get; init; }
    public int                          AnonymousTotal     { get; init; }
    public int                          DistinctActors     { get; init; }
    public IReadOnlyList<SurfaceRow>    SurfaceRanking     { get; init; } =
        System.Array.Empty<SurfaceRow>();
}
