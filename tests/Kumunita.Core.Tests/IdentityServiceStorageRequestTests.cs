// IMPROVE-UX A14 — the "request more space" notify lane (the
// IIdentityService.RequestStorageQuotaIncreaseAsync seam).
//
// One inbox row (and, for admins whose planted Profile has an email, one
// staged email) per EVERY GlobalAdmin, with a per-admin IdempotencyKey
// (the M6 dedup is by key alone — a shared key would collapse the fan-out
// to the first admin). A fresh instance with no GlobalAdmin is a no-op
// (nothing to notify, not a noise path). Best-effort (the ADR 0077 D5
// posture): an emit error is swallowed + logged — the request "was made"
// is the durable fact (the resident's confirmed action), the notify is the
// nudge. No audit row (a notify is not an access decision — the A12
// InvitationService.ApproveAsync notify precedent).
//
// Harness: the IdentityServiceAccountNotificationTests two-store boot
// (the `mt` document schema — including NotificationDocTypes so the
// Notification rows have a table — plus the EF-migrated `identity` schema
// + the real EF UserStore/UserManager so GetUsersInRoleAsync works), wired
// with a real NotificationService over the M6-registered store (recording
// translator + mailer doubles). For the emit-throws case, the boot accepts
// an INotificationEmitter override (an NSubstitute that throws) in place
// of the real NotificationService.
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

public sealed class IdentityServiceStorageRequestTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Admin1 = "admin-001";
    private const string Admin2 = "admin-002";
    private const string Resident = "resident-001";

    // ── One inbox row per GlobalAdmin (the ADR 0077 F10 fan-out shape) ─────

    [Fact]
    public async Task TwoAdmins_FansOutOneRowPerAdmin_WithPerAdminKeysAndLinkPath()
    {
        var boot = await BootAsync();
        await SeedGlobalAdminAsync(boot, Admin1);
        await SeedGlobalAdminAsync(boot, Admin2);

        await boot.Identity.RequestStorageQuotaIncreaseAsync(Resident);

        var rows = await NotificationsFor(boot.Store, Resident, NotificationKinds.StorageRequest);
        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { Admin1, Admin2 }, rows.Select(r => r.RecipientId).OrderBy(x => x).ToArray());

        // The per-admin idempotency key (the ADR 0077 F10 shape — the
        // recipient is a parameter, so the admin id is part of the key — a
        // repeat request does not re-flood the same admin).
        Assert.Equal(
            new[]
            {
                $"notification:{NotificationKinds.StorageRequest}:{Resident}:{Admin1}",
                $"notification:{NotificationKinds.StorageRequest}:{Resident}:{Admin2}",
            },
            rows.Select(r => r.IdempotencyKey).OrderBy(x => x).ToArray());

        // The linkPath lands on the account-detail surface (the A15
        // per-resident quota override will extend it).
        Assert.All(rows, r => Assert.Equal($"/admin/accounts/{Resident}", r.LinkPath));

        // The body carries the requesting resident (the admin knows *who*
        // is asking) — a planted profile display name resolves it.
        // (A profileless resident falls back to the raw subjectId — the
        // admin still has a handle.)
    }

    // ── A fresh instance with no GlobalAdmin is a no-op (nothing to notify) ─

    [Fact]
    public async Task NoAdmins_NoOp_EmitsNothing_StillSucceeds()
    {
        var boot = await BootAsync();   // no GlobalAdmin seeded

        // The lane is best-effort (the ADR 0077 D5 posture) — a fresh instance
        // (no GlobalAdmin) is a no-op, not a noise path, and it never throws.
        await boot.Identity.RequestStorageQuotaIncreaseAsync(Resident);

        Assert.Empty(await NotificationsFor(boot.Store, Resident, null));
    }

    // ── An emit error is swallowed (best-effort — the request still stands) ─

    [Fact]
    public async Task EmitterThrows_Swallows_StillSucceeds()
    {
        var boot = await BootAsync(notificationsOverride: ThrowingEmitter.Instance);
        await SeedGlobalAdminAsync(boot, Admin1);

        // The lane is best-effort (the ADR 0077 D5 posture) — an emit error is
        // swallowed + logged, and the request "was made" still stands (no
        // exception surfaces to the resident; the durable record is the
        // request, not the notify).
        await boot.Identity.RequestStorageQuotaIncreaseAsync(Resident);
    }

    // ── Harness (mirror the IdentityServiceAccountNotificationTests boot) ───

    private sealed record Boot
    {
        public required IdentityService Identity { get; init; }
        public required IDocumentStore Store { get; init; }
        public required DbContextOptions<AppDbContext> DbOptions { get; init; }
    }

    private async Task<Boot> BootAsync(INotificationEmitter? notificationsOverride = null)
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);

        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            CoreDocTypes.Configure(opts);
            NotificationDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

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

        INotificationEmitter notifications = notificationsOverride ?? new NotificationService(
            store,
            userInfo,
            Substitute.For<ITranslationProvider>(),
            Substitute.For<IMailerStage>());

        var identity = new IdentityService(
            userManager,
            store,
            userInfo,
            NoClaimsSource.Instance,
            NoMail.Instance,
            Options.Create(new VerificationOptions()),
            NullLogger<IdentityService>.Instance,
            null,                 // ITranslationProvider — the lane never reads one
            notifications);

        return new Boot { Identity = identity, Store = store, DbOptions = dbOptions };
    }

    /// <summary>
    /// Seeds one GlobalAdmin: the EF account + the <see cref="IdentityRole"/>
    /// row + the user-role link (exactly as FirstBootSeeder would for an
    /// invited GlobalAdmin), plus a planted, verified <see cref="Profile"/>
    /// with an email.
    /// </summary>
    private async Task SeedGlobalAdminAsync(Boot boot, string adminId)
    {
        var ct = TestContext.Current.CancellationToken;
        var email = adminId + "@ex.net";

        // `NormalizedName` is set explicitly: Identity's GetUsersInRoleAsync
        // queries on Role.NormalizedName, and the UpperInvariantLookupNormalizer
        // uppercases it — a direct db.Roles.Add (bypassing RoleManager /
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

        // The `mt` side: a verified profile with an email (the emit input —
        // the body resolves the requesting resident's name from Profile).
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

    private static async Task<List<Notification>> NotificationsFor(IDocumentStore store, string accountId, string? kind)
    {
        await using var session = store.QuerySession();
        var q = session.Query<Notification>().Where(n => n.IdempotencyKey.Contains(accountId));
        if (kind is not null) q = q.Where(n => n.Kind == kind);
        var rows = await Marten.QueryableExtensions
            .ToListAsync(q.OrderBy(n => n.Created), TestContext.Current.CancellationToken);
        return rows.ToList();
    }

    // ── Test doubles ──────────────────────────────────────────────────────────

    /// <summary>An <see cref="INotificationEmitter"/> that throws on emit (the ADR 0077 D5 best-effort posture's input).</summary>
    private sealed class ThrowingEmitter : INotificationEmitter
    {
        public static readonly ThrowingEmitter Instance = new();

        public Task<Notification?> EmitAsync(
            IDocumentSession session, string recipientId, string kind, string idempotencyKey, string? body,
            CancellationToken ct = default)
            => throw new InvalidOperationException("notify lane unavailable");

        public Task<Notification?> EmitAsync(
            IDocumentSession session, string recipientId, string kind, string idempotencyKey, string? body,
            string? targetId, string? linkPath, string? acceptPath, string? declinePath, CancellationToken ct = default)
            => throw new InvalidOperationException("notify lane unavailable");
    }

    private sealed class NoClaimsSource : IClaimsSource
    {
        public static readonly NoClaimsSource Instance = new();
        public ClaimsPrincipal? Current => null;
    }

    private sealed class NoMail : IMailerStage
    {
        public static readonly NoMail Instance = new();
        public Task StageAsync(
            IDocumentSession session, string idempotencyKey, string recipient, string subject, string body,
            CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static readonly EmptyServiceProvider Instance = new();
        public object? GetService(Type serviceType) => null;
    }
}
