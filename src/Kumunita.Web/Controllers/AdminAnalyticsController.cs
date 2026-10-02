using Kumunita.Core.Authorization;
using Kumunita.Core.Usage;
using Kumunita.Web.Models;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/admin/analytics</c> surface (M13, ADR 0114 D4) — the GlobalAdmin's
/// local feedback lane over the <see cref="UsageEvent"/> capture (the
/// ARCHITECTURE.md value-chain row: "the operator sees how the platform is
/// used"). The ADR 0062 section-split precedent: a section-specific
/// controller, not a new action on <see cref="AdminController"/>.
/// <para>
/// <b>Two routes, both reads:</b>
/// <list type="bullet">
/// <item><c>GET /admin/analytics?window=7|30|90</c> (default 30) — the
///       surface-rank table + the distinct-account count over the window
///       (the <see cref="AnalyticsViewModel"/> projection of the
///       <see cref="Kumunita.Core.Usage.UsageAnalyticsResult"/>;
///       <see cref="Index"/>).</item>
/// <item><c>GET /admin/analytics/export?window=7|30|90</c> — the CSV export
///       for the operator's local analysis (<see cref="Export"/>) +
///       <b>exactly one</b> <see cref="AccessAudit"/> row
///       (<c>TargetKind "analytics"</c> + <c>Action "analytics.export"</c> +
///       <c>Via Admin</c> + <c>Outcome Allow</c> — the ADR 0108
///       "portability.export" precedent verbatim: the operator's export is an
///       audited admin action).</item>
/// </list>
/// <para>
/// <b>Zero new authorization surface (C-M13·6):</b> no <c>AccessAction</c> /
/// <c>AccessVia</c> / <c>Decide()</c> branch / <c>IAuditableResource</c> —
/// the gate is the <see cref="Kumunita.Core.Identity.Roles.GlobalAdmin"/>
/// role; the <see cref="UsageEvent"/> row is not an auditable resource.
/// <b>No per-account data is rendered (C-M13·3):</b> the surface renders
/// aggregates over the window only — the <c>ActorId</c> column is used for
/// the <c>DistinctActors</c> count and never shown.
/// </para>
/// </summary>
[Route("admin/analytics")]
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class AdminAnalyticsController(
    IDocumentStore store,
    IUsageAnalyticsService analytics) : Controller
{
    private static string? AdminSubjectId(System.Security.Claims.ClaimsPrincipal user) =>
        user.FindFirst(Kumunita.Core.Identity.ClaimTypes.Subject)?.Value;

    /// <summary>
    /// <c>GET /admin/analytics?window=7|30|90</c> (default 30) — the local
    /// usage summary (the <see cref="AnalyticsViewModel"/> — the
    /// <see cref="Kumunita.Core.Usage.UsageAnalyticsResult"/> projection; the
    /// surface-rank table + the distinct-account count over the pinned
    /// window). A read — no audit row (the D4 surface contract: the audit row
    /// is on the <em>export</em> only). A <c>window</c> value outside 7/30/90
    /// throws <see cref="ArgumentOutOfRangeException"/> from the
    /// <see cref="IUsageAnalyticsService"/> seam (the D3 pin) — it propagates
    /// as a 400.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int window = 30)
    {
        var result = await analytics.GetWindowAsync(window);
        return View(new AnalyticsViewModel
        {
            WindowDays         = result.WindowDays,
            Total              = result.Total,
            AuthenticatedTotal = result.AuthenticatedTotal,
            AnonymousTotal     = result.AnonymousTotal,
            DistinctActors     = result.DistinctActors,
            SurfaceRanking     = result.SurfaceRanking
        });
    }

    /// <summary>
    /// <c>GET /admin/analytics/export?window=7|30|90</c> (default 30) — the
    /// CSV export (the ADR 0034 / ADR 0112 serve shape, the
    /// <see cref="AdminPortabilityController"/> precedent):
    /// <c>Content-Type: text/csv; charset=utf-8</c> +
    /// <c>Content-Disposition: attachment; filename="kumunita-usage-{window}d
    /// .csv"</c> + <c>Cache-Control: no-store</c>. Commits
    /// <b>exactly one</b> <see cref="AccessAudit"/> row
    /// (<c>TargetKind "analytics"</c>, <c>Action "analytics.export"</c>,
    /// <c>Via Admin</c>, <c>Outcome Allow</c>, the acting account's
    /// <c>ClaimTypes.Subject</c>) via <c>LightweightSession</c> — the ADR 0108
    /// "portability.export" precedent verbatim.
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] int window = 30)
    {
        var rows = await analytics.GetCsvRowsAsync(window);

        // The one AccessAudit row (D4 — the ADR 0108 "portability.export"
        // precedent verbatim): the operator's export is an audited admin
        // action.
        await using (var session = store.LightweightSession())
        {
            session.Store(new AccessAudit
            {
                At         = DateTimeOffset.UtcNow,
                ActorId    = AdminSubjectId(User) ?? string.Empty,
                Action     = "analytics.export",
                TargetKind = "analytics",
                Via        = AccessVia.Admin,
                Outcome    = AccessOutcome.Allow
            });
            await session.SaveChangesAsync();
        }

        var sb = new System.Text.StringBuilder();
        sb.Append("date,surface,total\n");
        foreach (var r in rows)
            sb.Append(r.Date.ToString("yyyy-MM-dd")).Append(',')
              .Append(r.Surface).Append(',')
              .Append(r.Total).Append('\n');

        var csv = sb.ToString();
        Response.Headers["Content-Type"] = "text/csv; charset=utf-8";
        Response.Headers["Content-Disposition"] =
            $"attachment; filename=\"kumunita-usage-{window}d.csv\"";
        Response.Headers["Cache-Control"] = "no-store";
        return Content(csv, "text/csv; charset=utf-8");
    }
}
