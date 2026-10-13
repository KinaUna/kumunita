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
    // derived, not captured). **Option-B seam (ADR 0162 close resolution):**
    // the Trend section carries its **own** window label,
    // <see cref="TrendWindowDays"/> (the <c>?trendwindow=</c> param, pinned
    // {7, 30, 90, 180, 365}), **independent** of the M13
    // <see cref="WindowDays"/> member (the <c>?window=</c> param, pinned
    // {7, 30, 90}). The two windows no longer share one param, so
    // <c>?trendwindow=180</c> / <c>365</c> render the Trend end-to-end
    // without tripping the M13 <c>GetWindowAsync</c> guard (the red pin the
    // U06/U07 gate recorded is resolved by this decoupling, M34·8).
    public int TrendWindowDays { get; init; } = 30;

    // The per-day usage totals in the chosen **trend** window
    // (<see cref="TrendWindowDays"/>), **ascending** by Date, **only the days
    // present** (no fabricated zero rows — the M34·7 / M33-9 FACE pin).
    // <see cref="History"/> is empty on a fresh instance (no <c>UsageEvent</c>
    // rows in the window yet) — the view then renders the
    // analytics.trend.empty message (M34-3 FACE).
    public IReadOnlyList<UsageDailyTotal> History { get; init; } =
        System.Array.Empty<UsageDailyTotal>();
}
