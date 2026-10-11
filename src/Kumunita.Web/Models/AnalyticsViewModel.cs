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

    // The M34 analytics-history Trend section (M34·8 additive-only, M34·1
    // derived, not captured): the per-day usage totals in the chosen window,
    // **ascending** by Date, **only the days present** (no fabricated zero
    // rows — the M34·7 / M33-9 FACE pin). <see cref="History"/> is empty on a
    // fresh instance (no <c>UsageEvent</c> rows in the window yet) — the view
    // then renders the analytics.trend.empty message (M34-3 FACE).
    // <see cref="WindowDays"/> (above) is **reused** for the Trend section's
    // window label — M34 does **not** add a second window member (M34·8).
    public IReadOnlyList<UsageDailyTotal> History { get; init; } =
        System.Array.Empty<UsageDailyTotal>();
}
