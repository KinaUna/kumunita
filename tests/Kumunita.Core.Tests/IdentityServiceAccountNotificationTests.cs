// ADR 0077 — admin-lane account notifications: when NotifyAdminsOnSignup is
// on, RegisterAsync and VerifyWithTokenAsync fan out one inbox row (and, for
// admins whose planted Profile has an email, one staged email) to EVERY
// GlobalAdmin, with a per-admin IdempotencyKey (the M6 dedup is by key alone
// — a shared key would collapse the fan-out to the first admin).
//
// Harness: the GuardianAssignmentTests two-store boot (the `mt` document
// schema — including M6DocTypes so the Notification rows have a table — plus
// the EF-migrated `identity` schema + the real EF UserStore/UserManager so
// GetUsersInRoleAsync works), wired with a real NotificationService over the
// M6-registered store (recording translator + mailer doubles, the
// NotificationServiceTests shape).
using System.Security.Claims;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Notifications;
using Kumunita.Core.UserInfo;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

public sealed class IdentityServiceAccountNotificationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Admin1 = "admin-001";
    private const string Admin2 = "admin-002";

    // ── ADR 0077 D3: RegisterAsync emits one row per GlobalAdmin ─────────────

    [Fact]
    public async Task Register_WithAdmins_EmitsOneRowPerAdmin_AndStagesEmails()
    {
        var boot = await BootAsync();
        await SeedGlobalAdminAsync(boot);

        var principal = await boot.Identity.RegisterAsync("Rina Resident", "rina@ex.net", "Sup3rSecret!");
        var rows = await NotificationsFor(boot.Store, principal.SubjectId, NotificationKinds.AccountSignup);

        var row = Assert.Single(rows);
        Assert.Equal(Admin1, row.RecipientId);
        Assert.Equal(NotificationKinds.AccountSignup, row.Kind);
        Assert.Contains("rina@ex.net", row.Body);                 // the UGC snippet is appended to the body
        Assert.Equal($"notification:{NotificationKinds.AccountSignup}:{principal.SubjectId}:{Admin1}", row.IdempotencyKey);

        // The admin has a planted profile email, so the M6 conditional stage ran
        // for this one row (the recording mailer captured exactly one envelope).
        Assert.Single(boot.Staged);
        Assert.Equal(Admin1 + "@ex.net", boot.Staged[0].Recipient);
    }

    [Fact]
    public async Task Register_WithTwoAdmins_FansOutToBothWithDistinctKeys()
    {
        var boot = await BootAsync();
        await SeedGlobalAdminAsync(boot, Admin1);
        await SeedGlobalAdminAsync(boot, Admin2);

        var principal = await boot.Identity.RegisterAsync("Rina Resident", "rina@ex.net", "Sup3rSecret!");
        var rows = await NotificationsFor(boot.Store, principal.SubjectId, NotificationKinds.AccountSignup);

        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { Admin1, Admin2 }, rows.Select(r => r.RecipientId).OrderBy(x => x).ToArray());
        // ADR 0077 D3: the per-admin key is what keeps the M6 key-alone dedup
        // from collapsing the fan-out to the first admin.
        Assert.Equal(
            new[]
            {
                $"notification:{NotificationKinds.AccountSignup}:{principal.SubjectId}:{Admin1}",
                $"notification:{NotificationKinds.AccountSignup}:{principal.SubjectId}:{Admin2}",
            },
            rows.Select(r => r.IdempotencyKey).OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task Register_WithoutAnyAdmin_StillSucceeds_EmitsNothing()
    {
        var boot = await BootAsync();   // no GlobalAdmin seeded

        var principal = await boot.Identity.RegisterAsync("Rina Resident", "rina@ex.net", "Sup3rSecret!");
        Assert.False(string.IsNullOrWhiteSpace(principal.SubjectId));   // best-effort: the signup is not blocked
        Assert.Empty(await NotificationsFor(boot.Store, principal.SubjectId, null));
        Assert.Empty(boot.Staged);
    }

    // ── ADR 0077 D3: VerifyWithTokenAsync emits the same shape on verify ─────

    [Fact]
    public async Task Verify_WithAdmins_EmitsAccountVerifiedRow()
    {
        var boot = await BootAsync();
        await SeedGlobalAdminAsync(boot, Admin1);

        var principal = await boot.Identity.RegisterAsync("Rina Resident", "rina@ex.net", "Sup3rSecret!");
        await VerifySeededAccountAsync(boot.Identity, boot.Store);

        var rows = await NotificationsFor(boot.Store, principal.SubjectId, NotificationKinds.AccountVerified);
        var row = Assert.Single(rows);
        Assert.Equal(Admin1, row.RecipientId);
        Assert.Equal(NotificationKinds.AccountVerified, row.Kind);
        Assert.Contains("rina@ex.net", row.Body);
        Assert.Equal($"notification:{NotificationKinds.AccountVerified}:{principal.SubjectId}:{Admin1}", row.IdempotencyKey);

        // Both moments fanned out to the same admin — the staged email count is
        // the signup envelope + the verify envelope.
        Assert.Equal(2, boot.Staged.Count);
    }

    // ── ADR 0077 D1: the gate — off stops BOTH emission points ────────────────

    [Fact]
    public async Task GateOff_RegisterAndVerify_EmitNothing_AndAuditTheFlip()
    {
        var boot = await BootAsync();
        await SeedGlobalAdminAsync(boot, Admin1);

        Assert.True(await boot.Identity.IsNotifyAdminsOnSignupAsync());   // default true (the M6 floor)
        await boot.Identity.SetNotifyAdminsOnSignupAsync(false, Admin1);
        Assert.False(await boot.Identity.IsNotifyAdminsOnSignupAsync());

        var principal = await boot.Identity.RegisterAsync("Rina Resident", "rina@ex.net", "Sup3rSecret!");
        Assert.Empty(await NotificationsFor(boot.Store, principal.SubjectId, NotificationKinds.AccountSignup));
        await VerifySeededAccountAsync(boot.Identity, boot.Store);
        Assert.Empty(await NotificationsFor(boot.Store, principal.SubjectId, NotificationKinds.AccountVerified));
        Assert.Empty(boot.Staged);

        // The flip is a durable audit trail (the ADR 0019/0020 read-modify-write shape).
        await using var session = boot.Store.QuerySession();
        var audit = await Marten.QueryableExtensions
            .FirstAsync(session.Query<AccessAudit>()
                .Where(a => a.Via == AccessVia.Admin && a.Action == "signup.set-notify"),
                TestContext.Current.CancellationToken);
        Assert.Equal(AccessOutcome.Allow, audit.Outcome);
        Assert.Equal("signup", audit.TargetKind);
        Assert.Equal("signup", audit.TargetId);
    }

    // ── Harness (GuardianAssignmentTests two-store boot + the M6 store shape) ─

    private sealed record Boot
    {
        public required IdentityService Identity { get; init; }
        public required IDocumentStore Store { get; init; }
        public required DbContextOptions<AppDbContext> DbOptions { get; init; }
        public required List<(string Key, string Recipient, string Subject, string Body)> Staged { get; init; }
    }

    /// <summary>
    /// Boots the two-store shape (the `mt` document schema — including
    /// <see cref="M6DocTypes"/> so the <see cref="Notification"/> rows have a
    /// table — plus the EF-migrated `identity` schema and the real EF
    /// UserStore/UserManager so <c>GetUsersInRoleAsync</c> works), and wires
    /// the real <see cref="IdentityService"/> with a real
    /// <see cref="NotificationService"/> (recording translator + mailer doubles).
    /// </summary>
    private async Task<Boot> BootAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);

        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
            M6DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        // The identity schema — the only EF Core in the tree (ADR 0004), migrated
        // exactly as SchemaBootstrap.ApplyAsync does at boot.
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(conn).Options;
        await using (var db = new AppDbContext(dbOptions))
        {
            await db.Database.MigrateAsync(ct);
        }

        var userStore = new UserStore<User, IdentityRole, AppDbContext, string,
            IdentityUserClaim<string>, IdentityUserRole<string>,
            IdentityUserLogin<string>, IdentityUserToken<string>,
            IdentityRoleClaim<string>>(new AppDbContext(dbOptions));

        var userManager = new UserManager<User>(
            userStore,
            Options.Create(new IdentityOptions { User = { RequireUniqueEmail = true } }),
            new PasswordHasher<User>(),
            new[] { new UserValidator<User>() },
            new[] { new PasswordValidator<User>() },
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            EmptyServiceProvider.Instance,
            NullLogger<UserManager<User>>.Instance);

        var userInfo = new UserInfoService(store);

        var staged = new List<(string Key, string Recipient, string Subject, string Body)>();
        var notificationMailer = Substitute.For<IMailerStage>();
        notificationMailer.StageAsync(
                Arg.Any<IDocumentSession>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                staged.Add(((string)callInfo[1], (string)callInfo[2], (string)callInfo[3], (string)callInfo[4]));
                return Task.CompletedTask;
            });

        var translator = Substitute.For<ITranslationProvider>();
        translator.GetAsync(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(ci => Task.FromResult((string)ci[0]));       // the key is a fine stand-in for the localized text here

        var notifications = new NotificationService(store, userInfo, translator, notificationMailer);

        var identity = new IdentityService(
            userManager,
            store,
            userInfo,
            NoClaimsSource.Instance,
            NoMail.Instance,
            Options.Create(new VerificationOptions()),
            NullLogger<IdentityService>.Instance,
            null,                 // ITranslationProvider — these emission points never read one
            notifications);

        return new Boot { Identity = identity, Store = store, DbOptions = dbOptions, Staged = staged };
    }

    /// <summary>
    /// Seeds one GlobalAdmin: the EF account + the <see cref="IdentityRole"/> row +
    /// the user-role link (exactly as FirstBootSeeder would for an invited admin),
    /// plus a planted, verified <see cref="Profile"/> with an email — so the M6
    /// conditional email stage has a recipient address to resolve.
    /// </summary>
    private async Task SeedGlobalAdminAsync(Boot boot, string adminId = Admin1)
    {
        var ct = TestContext.Current.CancellationToken;
        var email = adminId + "@ex.net";

        // The EF side (the identity schema): the account, the role row, the link —
        // the same shape FirstBootSeeder creates for an invited GlobalAdmin.
        // (EF's AnyAsync — both Marten and EF ship an AnyAsync; fully-qualified
        // for the EF DbSet.)
        //
        // `NormalizedName` is set explicitly: Identity's `GetUsersInRoleAsync`
        // queries on `Role.NormalizedName`, and the `UpperInvariantLookupNormalizer`
        // uppercases it — a direct `db.Roles.Add` (bypassing RoleManager /
        // AddNormalizedData) leaves it null, so the query would match nothing.
        await using var db = new AppDbContext(boot.DbOptions);
        if (!await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .AnyAsync(db.Roles.Where(r => r.Name == Roles.GlobalAdmin), ct))
        {
            db.Roles.Add(new IdentityRole
            {
                Id = Roles.GlobalAdmin,
                Name = Roles.GlobalAdmin,
                NormalizedName = Roles.GlobalAdmin.ToUpperInvariant()
            });
        }
        if (await db.Users.FindAsync(new object[] { adminId }, ct) is null)
        {
            db.Users.Add(new User { Id = adminId, UserName = adminId, Email = email });
        }
        if (!await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .AnyAsync(db.UserRoles.Where(ur => ur.UserId == adminId && ur.RoleId == Roles.GlobalAdmin), ct))
        {
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = adminId, RoleId = Roles.GlobalAdmin });
        }
        await db.SaveChangesAsync(ct);

        // The `mt` side: a verified profile with an email (the M6 email-stage input
        // — EmitAsync resolves the recipient's address from Profile.Email).
        await using var session = boot.Store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(new Profile
        {
            SubjectId = adminId,
            DisplayName = adminId,
            Verified = true,
            Email = email
        });
        await session.SaveChangesAsync(ct);
    }

    // ── Small read-back helpers ───────────────────────────────────────────────

    private static async Task<List<Notification>> NotificationsFor(IDocumentStore store, string accountId, string? kind)
    {
        await using var session = store.QuerySession();
        var q = session.Query<Notification>().Where(n => n.IdempotencyKey.Contains(accountId));
        if (kind is not null) q = q.Where(n => n.Kind == kind);
        var rows = await Marten.QueryableExtensions
            .ToListAsync(q.OrderBy(n => n.Created), TestContext.Current.CancellationToken);
        return rows.ToList();
    }

    /// <summary>
    /// Completes the M1 verification handoff exactly as the Web's
    /// <c>/account/verify</c> action would (the GuardianAssignmentTests helper):
    /// read back the single-use token row and consume it.
    /// </summary>
    private static async Task VerifySeededAccountAsync(IIdentityService identity, IDocumentStore store)
    {
        await using var session = store.QuerySession();
        var token = await Marten.QueryableExtensions
            .FirstAsync(
                session.Query<IdentityToken>()
                    .Where(t => t.Kind == IdentityToken.KindVerify)
                    .OrderByDescending(t => t.CreatedAt),
                TestContext.Current.CancellationToken);
        await identity.VerifyWithTokenAsync(token.Token);
    }

    // ── Test doubles ──────────────────────────────────────────────────────────

    private sealed class NoClaimsSource : IClaimsSource
    {
        public static readonly NoClaimsSource Instance = new();
        public ClaimsPrincipal? Current => null;   // not request-driven in a Core test
    }

    private sealed class NoMail : IMailerStage
    {
        public static readonly NoMail Instance = new();
        public Task StageAsync(
            IDocumentSession session,
            string idempotencyKey,
            string recipient,
            string subject,
            string body,
            CancellationToken ct = default) => Task.CompletedTask;   // the Identity outbox stage — no-op here
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static readonly EmptyServiceProvider Instance = new();
        public object? GetService(Type serviceType) => null;   // unregistered → null
    }
}
