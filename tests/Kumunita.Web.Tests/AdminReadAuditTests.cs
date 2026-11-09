using System.Security.Claims;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.ErrorReports;
using Kumunita.Core.Identity;
using Kumunita.Core.Pages;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using Claim = System.Security.Claims.Claim;
using ClaimsIdentity = System.Security.Claims.ClaimsIdentity;
using ClaimsPrincipal = System.Security.Claims.ClaimsPrincipal;

namespace Kumunita.Web.Tests;

/// <summary>
/// IS36 (E7, D9) — the three unaudited GlobalAdmin reads of class (c)/PII
/// surfaces (the audit log, error reports, guest list) now each write exactly
/// one aggregate <see cref="AccessAudit"/> row (the ADR 0108 "portability.export"
/// precedent, adapted to a read view — the <see cref="AdminAnalyticsController.Export"/>
/// shape). Each row: <c>TargetKind</c> + <c>Action</c> + <c>Via Admin</c> +
/// <c>Outcome Allow</c> + the acting account's <c>ClaimTypes.Subject</c>,
/// <c>TargetId = null</c>, <c>HiddenCount = null</c>, <c>VisibleCount</c> =
/// the rows the view rendered. Counts only — no PII (the IS08 gate).
/// <para>
/// Test 1 (audit log) uses a real Postgres-backed store: the <c>Audit</c>
/// action's <c>Marten.QueryableExtensions.ToListAsync</c> casts to the internal
/// <c>MartenLinqQueryable</c> and executes a live query, so it cannot be
/// NSubstituted (the <see cref="PostgresFixture"/> pattern, the
/// <see cref="AdminPortabilityScopeTests"/> / <see cref="F11U3_DeadLetterAlertTests"/>
/// precedent). Tests 2–3 (error reports, guests) use NSubstitute stores
/// (the controllers only call <c>store.LightweightSession().Store</c> — no
/// live Marten query).
/// </para>
/// </summary>
public sealed class AdminReadAuditTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Admin = "is36-admin-001";

    // ── 1 — the audit log view is itself audited ─────────────────────────────

    [Fact(DisplayName = "AuditLogView_IsItselfAudited")]
    public async Task AuditLogView_IsItselfAudited()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await BootStoreAsync(ct);
        try
        {
            var controller = BuildAdminController(store);

            // A fresh DB has no AccessAudit rows; the action reads 0, then
            // writes exactly one (VisibleCount = 0).
            var action = await controller.Audit();
            var view = Assert.IsType<ViewResult>(action);
            var model = Assert.IsType<Models.AdminAuditPageViewModel>(view.Model);
            Assert.Empty(model.Rows);

            // Query the real store for the audit row (the IS36 audit write).
            await using var verifySession = store.LightweightSession();
            var auditRows = await Marten.QueryableExtensions.ToListAsync(
                verifySession.Query<AccessAudit>(), ct);

            var row = Assert.Single(auditRows);
            Assert.Equal("audit-log", row.TargetKind);
            Assert.Equal("audit-log.view", row.Action);
            Assert.Equal(Admin, row.ActorId);
            Assert.Equal(AccessVia.Admin, row.Via);
            Assert.Equal(AccessOutcome.Allow, row.Outcome);
            Assert.Null(row.TargetId);
            Assert.Equal(0, row.VisibleCount);
            Assert.Null(row.HiddenCount);
        }
        finally
        {
            await store.DisposeAsync();
        }
    }

    // ── 2 — the error-report view is audited ──────────────────────────────────

    [Fact(DisplayName = "ErrorReportView_IsAudited")]
    public async Task ErrorReportView_IsAudited()
    {
        var store = Substitute.For<IDocumentStore>();
        var session = Substitute.For<IDocumentSession>();
        store.LightweightSession().Returns(session);

        var svc = Substitute.For<IErrorReportService>();
        svc.ListAsync(100, Arg.Any<CancellationToken>()).Returns(new[]
        {
            new ErrorReport { Id = "r1", SubjectId = "user-a", Description = "synthetic", TriageStatus = "new" },
            new ErrorReport { Id = "r2", SubjectId = "user-b", Description = "synthetic", TriageStatus = "new" },
        });

        var controller = new ErrorReportAdminController(svc, store: store);
        SetAdminUser(controller);

        var action = await controller.Index();
        Assert.IsType<ViewResult>(action);

        // Exactly one AccessAudit row — the IS36 aggregate read-view shape.
        session.Received(1).Store(
            Arg.Is<AccessAudit>(a =>
                a.TargetKind == "error-reports"
                && a.Action == "error-reports.view"
                && a.ActorId == Admin
                && a.Via == AccessVia.Admin
                && a.Outcome == AccessOutcome.Allow
                && a.VisibleCount == 2
                && a.TargetId == null
                && a.HiddenCount == null));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        // Negative pin — no stray second row or wrong TargetKind.
        session.DidNotReceive().Store(Arg.Is<AccessAudit>(a =>
            a.TargetKind != "error-reports" || a.Action != "error-reports.view"));
    }

    // ── 3 — the guest list view is audited ───────────────────────────────────

    [Fact(DisplayName = "GuestListView_IsAudited")]
    public async Task GuestListView_IsAudited()
    {
        var store = Substitute.For<IDocumentStore>();
        var session = Substitute.For<IDocumentSession>();
        store.LightweightSession().Returns(session);

        var identity = Substitute.For<IIdentityService>();
        identity.GetGuestAccessAsync("guest-a").Returns(new GuestAccess
        {
            SubjectId  = "guest-a",
            ValidFrom  = DateTimeOffset.UtcNow.AddDays(-7),
            ValidUntil = DateTimeOffset.UtcNow.AddDays(7),
        });

        var controller = new AdminGuestsController(identity, store: store);
        SetAdminUser(controller);

        var action = await controller.Index("guest-a");
        Assert.IsType<ViewResult>(action);

        // Exactly one AccessAudit row (VisibleCount = 1 — one standing rendered).
        session.Received(1).Store(
            Arg.Is<AccessAudit>(a =>
                a.TargetKind == "guests"
                && a.Action == "guests.view"
                && a.ActorId == Admin
                && a.Via == AccessVia.Admin
                && a.Outcome == AccessOutcome.Allow
                && a.VisibleCount == 1
                && a.TargetId == null
                && a.HiddenCount == null));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        // Negative pin — no stray second row or wrong TargetKind.
        session.DidNotReceive().Store(Arg.Is<AccessAudit>(a =>
            a.TargetKind != "guests" || a.Action != "guests.view"));
    }

    // ── Harness ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Boot a real <see cref="DocumentStore"/> over a fresh scratch database
    /// (the <see cref="PostgresFixture"/> seam) with the <c>mt</c> schema +
    /// <see cref="CoreDocTypes"/> (which registers <see cref="AccessAudit"/>),
    /// with the DDL applied. Mirrors <see cref="AdminPortabilityScopeTests"/>
    /// / <see cref="F11U3_DeadLetterAlertTests"/>.
    /// </summary>
    private async Task<DocumentStore> BootStoreAsync(CancellationToken ct)
    {
        var conn = await fixture.NewDatabaseAsync(ct);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            CoreDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);
        return store;
    }

    /// <summary>
    /// Build the <see cref="AdminController"/> with the real <paramref name="store"/>
    /// + a dummy <see cref="AppDbContext"/> (never opened — the Audit action
    /// only reads Marten) + NSubstituted Core seams. Mirrors
    /// <see cref="AdminControllerBlockTests"/> construction.
    /// </summary>
    private AdminController BuildAdminController(IDocumentStore store)
    {
        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=localhost;Database=_test_never_opened;Username=_;Password=_")
                .Options);

        var controller = new AdminController(
            identities: db,
            store:      store,
            identity:   Substitute.For<IIdentityService>(),
            userInfo:   Substitute.For<IUserInfoService>(),
            Substitute.For<ICommunityMembershipService>(),
            Substitute.For<IProfileSettingsService>(),
            pages:      Substitute.For<IPageService>());

        SetAdminUser(controller);
        return controller;
    }

    /// <summary>
    /// Set the <see cref="Controller.HttpContext"/> to a
    /// <see cref="DefaultHttpContext"/> whose <see cref="HttpContext.User"/>
    /// carries an authenticated GlobalAdmin (the <c>ClaimTypes.Subject</c>
    /// claim <see cref="Admin"/>). Mirrors the
    /// <see cref="AdminAnalyticsControllerTests"/> <c>Build()</c> idiom.
    /// </summary>
    private static void SetAdminUser(Controller controller)
    {
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
    }
}
