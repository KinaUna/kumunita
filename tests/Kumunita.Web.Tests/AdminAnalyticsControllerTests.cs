using System.Security.Claims;
using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Kumunita.Core.Usage;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using System.Reflection;
using Xunit;
using Claim = System.Security.Claims.Claim;
using ClaimsIdentity = System.Security.Claims.ClaimsIdentity;
using ClaimsPrincipal = System.Security.Claims.ClaimsPrincipal;

namespace Kumunita.Web.Tests;

/// <summary>
/// The 3 D4/D7 admin-surface pins for <see cref="AdminAnalyticsController"/>
/// (M13, ADR 0114) — the design doc §pinned tests "Web —
/// <c>AdminAnalyticsControllerTests</c>" list, verbatim.
/// <para>
/// Harness: the project's standing <b>direct-construction</b> idiom
/// (NSubstitute seams + <see cref="DefaultHttpContext"/> — the shape
/// <see cref="AdminPortabilityControllerTests"/> and
/// <see cref="UsageCaptureMiddlewareTests"/> use), <b>not</b> a full host
/// boot. <see cref="IDocumentStore"/> and <see cref="IUsageAnalyticsService"/>
/// are NSubstituted (both are interfaces, so they proxy cleanly);
/// <b>no Postgres</b> (the U05 constraint — unlike U04's aggregation pins).
/// </para>
/// <para>
/// <b>Pin coverage (D7, verbatim):</b>
/// <see cref="AdminAnalytics_Route_Exists_And_GlobalAdmin_Only"/>
/// (a GlobalAdmin gets 200 on <c>GET /admin/analytics?window=30</c>; a
/// <c>Member</c> gets the sign-in challenge — the D4 gate pin, F3),
/// <see cref="AdminAnalytics_Csv_Shape"/> (the <c>Content-Type</c> is
/// <c>text/csv; charset=utf-8</c>, the <c>Content-Disposition</c> filename
/// carries the window (<c>kumunita-usage-{window}d.csv</c>), the
/// <c>Cache-Control</c> is <c>no-store</c>, and <b>exactly one</b>
/// <see cref="AccessAudit"/> row with <c>TargetKind == "analytics"</c> +
/// <c>Action == "analytics.export"</c> — the D4 audit-row pin),
/// <see cref="KnownTranslationKeys_Parity_Extended_With_Analytics_Keys"/>
/// (the ten <c>admin.analytics_*</c> keys, §kw-l, exist × en/de/fr/da with
/// non-empty values — the D4 <c>kw-l</c> parity pin, the
/// <c>KnownTranslationKeys_ParityTests</c> extension).
/// </para>
/// </summary>
public class AdminAnalyticsControllerTests
{
    private const string Admin = "m13-admin-001";

    // ── The 3 D4/D7 pins ────────────────────────────────────────────────────

    [Fact]
    public async Task AdminAnalytics_Route_Exists_And_GlobalAdmin_Only()
    {
        var (controller, analytics, store, _) = Build();

        // The gate (F3, the ADR 0105 "the gate is the role" posture): the
        // controller carries [Authorize(Roles = GlobalAdmin)] — a Member never
        // reaches the action (the sign-in challenge is the platform's
        // authz-failure shape, asserted by the attribute's presence, the
        // AdminPortabilityControllerTests Controller_Carries_GlobalAdmin_Role_Authorize
        // idiom).
        var attr = typeof(AdminAnalyticsController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(Kumunita.Core.Identity.Roles.GlobalAdmin, attr!.Roles);

        // A GlobalAdmin gets 200 on GET /admin/analytics?window=30 (the Index
        // action resolves to a View over the projected AnalyticsViewModel).
        analytics
            .GetWindowAsync(30)
            .Returns(new UsageAnalyticsResult
            {
                WindowDays         = 30,
                Total              = 5,
                AuthenticatedTotal = 3,
                AnonymousTotal     = 2,
                DistinctActors     = 2,
                SurfaceRanking     = new[] { new SurfaceRow { Surface = "posts", Total = 5 } }
            });

        var action = await controller.Index(window: 30);
        var view = Assert.IsType<ViewResult>(action);
        var model = Assert.IsType<AnalyticsViewModel>(view.Model);
        Assert.Equal(30, model.WindowDays);
        Assert.Equal(5, model.Total);
        Assert.Equal(3, model.AuthenticatedTotal);
        Assert.Equal(2, model.AnonymousTotal);
        Assert.Equal(2, model.DistinctActors);
        var rankRow = Assert.Single(model.SurfaceRanking);
        Assert.Equal("posts", rankRow.Surface);
        Assert.Equal(5, rankRow.Total);

        await analytics.Received(1).GetWindowAsync(30);

        // The index read is un-audited (the D4 surface contract: the audit row
        // is on the export only) — the store is never even opened.
        store.DidNotReceive().LightweightSession();
    }

    [Fact]
    public async Task AdminAnalytics_Csv_Shape()
    {
        var (controller, analytics, store, session) = Build();

        analytics
            .GetCsvRowsAsync(90)
            .Returns(new[]
            {
                new UsageCsvRow { Date = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), Surface = "posts",  Total = 3 },
                new UsageCsvRow { Date = new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero), Surface = "events", Total = 2 },
            });

        var action = await controller.Export(window: 90);
        var content = Assert.IsType<ContentResult>(action);

        // The serve shape (the ADR 0034 / ADR 0112 serve idiom, the
        // AdminPortabilityController Export precedent): the three headers the
        // D4 contract pins.
        Assert.Equal("text/csv; charset=utf-8", content.ContentType);
        Assert.Contains("kumunita-usage-90d.csv",
            controller.HttpContext!.Response.Headers["Content-Disposition"].ToString());
        Assert.Equal("no-store",
            controller.HttpContext.Response.Headers["Cache-Control"].ToString());

        // The CSV body: header row + the per-day-per-surface rows.
        Assert.StartsWith("date,surface,total\n", content.Content);
        Assert.Contains("2026-09-01,posts,3", content.Content);
        Assert.Contains("2026-09-02,events,2", content.Content);

        await analytics.Received(1).GetCsvRowsAsync(90);

        // Exactly one AccessAudit row, the D4 audit-row shape (the ADR 0108
        // "portability.export" precedent verbatim): TargetKind "analytics",
        // Action "analytics.export", Via Admin, Outcome Allow, the acting
        // account's ClaimTypes.Subject.
        session.Received(1).Store(
            Arg.Is<AccessAudit>(a =>
                a.TargetKind == "analytics"
                && a.Action == "analytics.export"
                && a.ActorId == Admin
                && a.Via == AccessVia.Admin
                && a.Outcome == AccessOutcome.Allow));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        session.DidNotReceive().Store(Arg.Is<AccessAudit>(a =>
            a.TargetKind != "analytics" || a.Action != "analytics.export"));
    }

    [Fact]
    public void KnownTranslationKeys_Parity_Extended_With_Analytics_Keys()
    {
        // The ten admin.analytics_* keys (design doc §kw-l) exist ×
        // en/de/fr/da with non-empty values (the D4 kw-l parity pin — the
        // KnownTranslationKeys_ParityTests extension; the AllKeys/DeValues/
        // FrValues/DaValues key-set parity is already pinned by that family).
        var keys = new[]
        {
            "admin.analytics_title",
            "admin.analytics_lede",
            "admin.analytics_window",
            "admin.analytics_total",
            "admin.analytics_authenticated",
            "admin.analytics_anonymous",
            "admin.analytics_distinct",
            "admin.analytics_surface",
            "admin.analytics_count",
            "admin.analytics_export",
        };

        foreach (var key in keys)
        {
            Assert.Contains(key, KnownTranslationKeys.EnValues.Keys,
                StringComparer.Ordinal);
            Assert.Contains(key, KnownTranslationKeys.DeValues.Keys,
                StringComparer.Ordinal);
            Assert.Contains(key, KnownTranslationKeys.FrValues.Keys,
                StringComparer.Ordinal);
            Assert.Contains(key, KnownTranslationKeys.DaValues.Keys,
                StringComparer.Ordinal);

            Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.EnValues[key]),
                $"registry key '{key}' has an empty/whitespace en value");
            Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.DeValues[key]),
                $"registry key '{key}' has an empty/whitespace de value");
            Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.FrValues[key]),
                $"registry key '{key}' has an empty/whitespace fr value");
            Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.DaValues[key]),
                $"registry key '{key}' has an empty/whitespace da value");
        }
    }

    // ── Harness ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Build the controller with NSubstituted seams over a
    /// <see cref="DefaultHttpContext"/> whose <see cref="HttpContext.User"/>
    /// carries an authenticated GlobalAdmin (the <c>ClaimTypes.Subject</c>
    /// claim <see cref="Admin"/>). The <see cref="IDocumentStore"/>
    /// <c>LightweightSession()</c> returns the shared session substitute so
    /// the export's <c>Store</c>/<c>SaveChangesAsync</c> calls are assertable.
    /// </summary>
    private static (AdminAnalyticsController controller, IUsageAnalyticsService analytics,
        IDocumentStore store, IDocumentSession session) Build()
    {
        var store = Substitute.For<IDocumentStore>();
        var session = Substitute.For<IDocumentSession>();
        store.LightweightSession().Returns(session);

        var analytics = Substitute.For<IUsageAnalyticsService>();

        var controller = new AdminAnalyticsController(store, analytics);

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Admin),
                    new Claim(Kumunita.Core.Identity.ClaimTypes.Role,
                        Kumunita.Core.Identity.Roles.GlobalAdmin),
                },
                authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return (controller, analytics, store, session);
    }
}
