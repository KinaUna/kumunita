using System.Security.Claims;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Media;
using Kumunita.Core.Portability;
using Kumunita.Web.Controllers;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Npgsql;
using NpgsqlTypes;
using Xunit;
using Claim = System.Security.Claims.Claim;
using ClaimsIdentity = System.Security.Claims.ClaimsIdentity;
using ClaimsPrincipal = System.Security.Claims.ClaimsPrincipal;

namespace Kumunita.Web.Tests;

/// <summary>
/// IS28 (ADR 0299) — the operator-gated instance export, at the controller
/// seam. The content-free export is the default (no gate); a <c>Full</c>
/// export (<c>?scope=full</c>) requires an active operator-granted
/// <c>AdminOverride</c> (checked via <see cref="BreakGlassProbe.HasActiveAsync"/>
/// on the injected <see cref="IDocumentStore"/>) — otherwise <c>403 Forbid</c>,
/// with no service call and no audit row. The service emits exactly one
/// scope-aware audit row (C-M11·6):
/// <c>portability.export.content-free</c> / <c>Via: Admin</c> or
/// <c>portability.export.full</c> / <c>Via: BreakGlass</c>.
/// <para>
/// Tests 1–2 drive the controller against an <c>NSubstitute</c>
/// <see cref="IPortabilityService"/> (the <c>Build()</c> shape from
/// <c>AdminPortabilityControllerTests</c>, which passes <c>null</c> for the
/// store — the test floor). Test 3 boots a real Postgres-backed store (the
/// <see cref="PostgresFixture"/> scratch DB + a hand-written
/// <c>AdminOverride</c> row, the IS26 <c>BreakGlassProbeTests</c> pattern)
/// with an active override and asserts the full-scope audit row lands
/// <c>Via: BreakGlass</c>.
/// </para>
/// </summary>
public sealed class AdminPortabilityScopeTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Admin = "pt-scope-admin-001";

    // ── 1 — the default (absent scope) is the content-free export ─────────

    [Fact(DisplayName = "Export_Default_IsContentFree")]
    public async Task Export_Default_IsContentFree()
    {
        var (controller, portability) = Build(store: null);
        var payload = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00 };
        portability.ExportAsync(Admin, PortabilityExportScope.ContentFree,
                Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(payload));

        var action = await controller.Export();

        // The content-free path streams the archive (a 200 FileResult).
        var file = Assert.IsType<FileStreamResult>(action);
        Assert.Equal("application/octet-stream", file.ContentType);
        using var ms = new MemoryStream();
        file.FileStream?.CopyTo(ms);
        Assert.Equal(payload, ms.ToArray());

        // The service is called exactly once, with the ContentFree scope —
        // the store (break-glass probe) is never touched on this path.
        await portability.Received(1).ExportAsync(
            Admin, PortabilityExportScope.ContentFree, Arg.Any<CancellationToken>());
    }

    // ── 2 — full scope without an active override → 403, no service call ──

    [Fact(DisplayName = "FullExport_WithoutOverride_Forbidden")]
    public async Task FullExport_WithoutOverride_Forbidden()
    {
        // The test floor (store == null) is a fail-closed no-override: the
        // full branch is refused before any service call is made.
        var (controller, portability) = Build(store: null);

        var action = await controller.Export(scope: "full");

        Assert.IsType<ForbidResult>(action);
        // No service call — the refused export emits no audit row (C-M11·6).
        await portability.DidNotReceive()
            .ExportAsync(Arg.Any<string>(), Arg.Any<PortabilityExportScope>(),
                Arg.Any<CancellationToken>());
    }

    // ── 3 — full scope WITH an active override → 200, audited BreakGlass ───

    [Fact(DisplayName = "FullExport_WithOverride_AuditedBreakGlass")]
    public async Task FullExport_WithOverride_AuditedBreakGlass()
    {
        var (store, connString, service) =
            await BootRealServiceAsync(TestContext.Current.CancellationToken);

        // An active operator-granted AdminOverride for the test actor — the
        // same hand-written row the operator writes through psql (OPS §9),
        // seeded the way the IS26 BreakGlassProbeTests do (a consumed,
        // non-expired row → HasActiveAsync is true), through a fresh write
        // connection (a query session is read-only, so the INSERT runs on its
        // own connection).
        await SeedAdminOverrideAsync(connString, Admin);

        var (controller, portability) = Build(store: store, realService: service);

        // The real service streams a real archive (an empty instance is a
        // valid, non-zero-byte archive — the manifest head is always written).
        var action = await controller.Export(scope: "full");
        var file = Assert.IsType<FileStreamResult>(action);
        using var ms = new MemoryStream();
        file.FileStream?.CopyTo(ms);
        Assert.NotEmpty(ms.ToArray());

        // The full-scope audit row (exactly one) is audited Via: BreakGlass
        // with the scope-aware Action (ADR 0299 §3).
        await using var session = store.QuerySession();
        var rows = (await Marten.QueryableExtensions.ToListAsync(
            session.Query<AccessAudit>()
                .Where(a => a.Action.StartsWith("portability.export.", StringComparison.Ordinal)),
            TestContext.Current.CancellationToken)).ToList();

        var full = Assert.Single(rows,
            a => a.Action == "portability.export.full");
        Assert.Equal(AccessVia.BreakGlass, full.Via);
        Assert.Equal(Admin, full.ActorId);
        Assert.Equal("portability", full.TargetKind);
    }

    // ── Harnesses ──────────────────────────────────────────────────────────

    /// <summary>
    /// The controller harness (the <c>Build()</c> shape from
    /// <c>AdminPortabilityControllerTests</c>). When <paramref name="realService"/>
    /// is null, the <see cref="IPortabilityService"/> is an
    /// <c>NSubstitute</c>; otherwise the real service is wired in and the
    /// substitute is unused.
    /// </summary>
    private static (AdminPortabilityController controller, IPortabilityService portability)
        Build(IDocumentStore? store, IPortabilityService? realService = null)
    {
        var portability = realService ?? Substitute.For<IPortabilityService>();
        var localization = Substitute.For<ILocalizationService>();
        // translationProvider: null — the test floor (the T() helper falls
        // back to returning the key raw).
        var controller = new AdminPortabilityController(
            portability, localization, null, store);

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Admin),
                new Claim(Kumunita.Core.Identity.ClaimTypes.Role, Roles.GlobalAdmin),
            },
            authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());

        return (controller, portability);
    }

    /// <summary>
    /// Boots a real Postgres-backed store + a real <see cref="PortabilityService"/>
    /// (the same boot shape as <c>PortabilityRoundTripTests.BootFullInstanceAsync</c>,
    /// trimmed to the doc-type surfaces the export loop queries). Returns the
    /// store (for the controller gate + the audit-row read), the connection string
    /// (for the <c>AdminOverride</c> seed — a fresh write connection, the IS26
    /// <c>BreakGlassProbeTests</c> pattern), and the service (for the controller
    /// to call).
    /// </summary>
    private async Task<(IDocumentStore store, string connString, PortabilityService service)>
        BootRealServiceAsync(CancellationToken ct)
    {
        var conn = await fixture.NewDatabaseAsync(ct);

        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            // Every doc-type surface the §2.6 Full export queries (the exact
            // set Program.cs registers) + the Media catalog + the Escalation
            // outbound config ConfigExport reads.
            CoreDocTypes.Configure(opts);
            PostDocTypes.Configure(opts);
            EventDocTypes.Configure(opts);
            ProjectDocTypes.Configure(opts);
            NotificationDocTypes.Configure(opts);
            MessagingDocTypes.Configure(opts);
            MediaDocTypes.Configure(opts);
            TagDocTypes.Configure(opts);
            PageDocTypes.Configure(opts);
            EscalationDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        // The identity schema (the only EF Core in the tree, ADR 0004) — the
        // PrincipalExport reads the AspNetUsers rows through it.
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn).Options);
        await db.Database.MigrateAsync(ct);

        var userStore = new UserStore<User, IdentityRole, AppDbContext, string,
            IdentityUserClaim<string>, IdentityUserRole<string>,
            IdentityUserLogin<string>, IdentityUserToken<string>,
            IdentityRoleClaim<string>>(db);
        var roleStore = new RoleStore<IdentityRole, AppDbContext, string,
            IdentityUserRole<string>, IdentityRoleClaim<string>>(db);

        var identityOpts = Options.Create(new IdentityOptions
        {
            User = { RequireUniqueEmail = true },
            Password = { RequiredLength = 8, RequireNonAlphanumeric = false },
        });
        var userManager = new UserManager<User>(
            userStore, identityOpts, new PasswordHasher<User>(),
            new[] { new UserValidator<User>() }, new[] { new PasswordValidator<User>() },
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(),
            new EmptyServiceProvider(), NullLogger<UserManager<User>>.Instance);
        var roleManager = new RoleManager<IdentityRole>(
            roleStore, new[] { new RoleValidator<IdentityRole>() },
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(),
            NullLogger<RoleManager<IdentityRole>>.Instance);

        var mediaRoot = Path.Combine(Path.GetTempPath(),
            "kumunita-is28-" + Guid.NewGuid().ToString("n")[..10]);
        var fileStore = new LocalVolumeFileStore(Options.Create(new MediaOptions { RootPath = mediaRoot }));
        var mediaStore = new LocalVolumeMediaStore(fileStore, store);

        var options = Options.Create(new CommunityOptions { Name = "Kumunita" });

        var service = new PortabilityService(
            store, new IdentityPrincipalStore(db), userManager, roleManager,
            options, mediaStore, fileStore);

        return (store, conn, service);
    }

    /// <summary>
    /// Seeds an active (consumed, non-expired) <c>AdminOverride</c> row for
    /// <paramref name="userId"/> — the hand-written operator row (ADR 0003 §4.5),
    /// inserted through the same raw-SQL path <see cref="BreakGlassProbe.HasActiveAsync"/>
    /// reads, the way the IS26 <c>BreakGlassProbeTests</c> do it (a fresh write
    /// connection, not a read-only query session).
    /// </summary>
    private static async Task SeedAdminOverrideAsync(string connString, string userId)
    {
        var now = DateTimeOffset.UtcNow;
        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO \"mt\".\"AdminOverride\" " +
            "  (\"id\", \"userId\", \"token\", \"grantedAt\", \"expiresAt\", \"consumedAt\") " +
            "VALUES (@id, @userId, @token, @grantedAt, @expiresAt, @consumedAt)";

        static void Add(NpgsqlCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }

        Add(cmd, "@id", Guid.NewGuid().ToString("N"));
        Add(cmd, "@userId", userId);
        Add(cmd, "@token", "tok-is28-full-export");
        Add(cmd, "@grantedAt", now.AddHours(-2));
        Add(cmd, "@expiresAt", now.AddHours(+2));
        Add(cmd, "@consumedAt", now.AddHours(-1));

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>An empty <see cref="IServiceProvider"/> for the Identity managers (the <c>PortabilityRoundTripTests</c> helper).</summary>
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    /// <summary>A no-op <see cref="ITempDataProvider"/> (the <c>AdminPortabilityControllerTests</c> helper).</summary>
    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect / the call log, not the bag
        }
    }
}
