using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// ADR 0143 — the guardian delete-child lane
/// (<see cref="IdentityService.DeleteChildAccountAsync"/>). One branch, one
/// seam:
/// <para>
/// **Standing gate (G·2/G·3, live):** before any write, an <b>active</b>
/// <see cref="GuardianLink"/> for this exact (guardian, child) pair must
/// exist, else <see cref="UnauthorizedAccessException"/> (the Web's 404).
/// A non-guardian's invocation is refused (deny-by-default, C·2).
/// </para>
/// <para>
/// **On success:** the child's audit rows are pseudonymized to
/// <c>deleted:{childId}</c> (C·4), their memberships +
/// <see cref="Profile"/> are removed, every <see cref="GuardianLink"/>
/// row for the child (any status) is dissolved (C·5 — no dangling
/// standing; a co-guardian's row ends here), the Identity account is
/// deleted, and exactly one <c>"account.delete"</c> summary audit row is
/// written with <see cref="AccessVia.Guardian"/> (the ADR 0028 "narrower
/// standing" rule).
/// </para>
/// <para>
/// **Not a silent no-op (the ADR 0142 idempotency pin):** a second call
/// for the same child after a successful first throws
/// <see cref="InvalidOperationException"/> (the Identity row is gone).
/// </para>
/// <para>
/// Pinned the same way the ADR 0142 lane is (via <see cref="AccessAudit"/>
/// shape + the fail-closed pin + the pseudonymization invariant), over a
/// fresh scratch Postgres (the <see cref="PostgresFixture"/> harness).
/// </para>
/// </summary>
public sealed class IdentityServiceDeleteChildAccountTests(
    PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── C·2 — a non-guardian cannot delete (deny-by-default, 404) ─────────

    [Fact]
    public async Task DeleteChildAccountAsync_NonGuardian_ThrowsUnauthorized()
    {
        var boot = await BootAsync();
        // The actor is NOT the guardian (no active link over the child).
        var nonGuardian = await SeedAccountAsync(boot, "non-guardian@examplium.com", globalAdmin: false);
        var child = await SeedAccountAsync(boot, "child@examplium.com", globalAdmin: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => boot.Identity.DeleteChildAccountAsync(child, nonGuardian));

        // No write happened: no "account.delete" audit row, the account
        // still exists, the profile row is intact (the fail-closed pin).
        Assert.Empty(await AuditRows(boot.Store, action: "account.delete"));
        Assert.NotNull(await boot.UserManager.FindByIdAsync(child));
        Assert.NotNull(await ProfileAsync(boot.Store, child));
    }

    // ── C·2 — a guardian with no active link (dissolved) cannot delete ────

    [Fact]
    public async Task DeleteChildAccountAsync_DissolvedLink_ThrowsUnauthorized()
    {
        var boot = await BootAsync();
        var guardian = await SeedAccountAsync(boot, "guardian@examplium.com", globalAdmin: false);
        var child = await SeedAccountAsync(boot, "child@examplium.com", globalAdmin: false);

        // Create the link, then dissolve it (the independence lane) — the
        // guardian no longer holds standing.
        var svc = new UserInfoService(boot.Store);
        var link = await svc.CreateGuardianLinkAsync(child, guardian);
        Assert.Equal(GuardianLinkStatus.Active, link.Status);
        await svc.DissolveGuardianLinkAsync(link.Id, guardian, viaAdmin: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => boot.Identity.DeleteChildAccountAsync(child, guardian));

        // The account still exists (the dissolve did not delete it — the
        // independence lane preserves the account; the delete lane is a
        // separate, gated act).
        Assert.NotNull(await boot.UserManager.FindByIdAsync(child));
    }

    // ── Happy path — the account is deleted, pseudonymized, links dissolved ──

    [Fact]
    public async Task DeleteChildAccountAsync_ActiveLink_Successful_DeletesAndPseudonymizesAndDissolvesLinks()
    {
        var boot = await BootAsync();
        var guardian = await SeedAccountAsync(boot, "guardian@examplium.com", globalAdmin: false);
        var child = await SeedAccountAsync(boot, "child@examplium.com", globalAdmin: false);

        // Seed the active link.
        var svc = new UserInfoService(boot.Store);
        var link = await svc.CreateGuardianLinkAsync(child, guardian);
        Assert.Equal(GuardianLinkStatus.Active, link.Status);

        // Pre-plant an audit row *by* the child (a past action) + a group
        // membership — both should be pseudonymized / removed on delete.
        await PlantAuditRowAsync(boot.Store, actorId: child, action: "post.read");
        var groupId = await PlantGroupMembershipAsync(boot.Store, userId: child);

        // A second guardian (co-guardian) over the same child — their row
        // should also be dissolved (C·5 — no dangling standing).
        var coGuardian = await SeedAccountAsync(boot, "co-guardian@examplium.com", globalAdmin: false);
        var coLink = await svc.CreateGuardianLinkAsync(child, coGuardian);
        Assert.Equal(GuardianLinkStatus.Active, coLink.Status);

        // The delete.
        await boot.Identity.DeleteChildAccountAsync(child, guardian);

        // The child is gone (Identity + Profile + memberships).
        Assert.Null(await boot.UserManager.FindByIdAsync(child));
        Assert.Null(await ProfileAsync(boot.Store, child));
        Assert.Null(await GroupMembershipAsync(boot.Store, groupId));

        // The guardian and the co-guardian are both untouched (different
        // accounts — the delete is scoped to the child only).
        Assert.NotNull(await boot.UserManager.FindByIdAsync(guardian));
        Assert.NotNull(await boot.UserManager.FindByIdAsync(coGuardian));

        // The child's audit row is pseudonymized (actor id → tombstone).
        var byAction = await AuditRows(boot.Store, action: "post.read");
        Assert.Single(byAction);
        Assert.Equal($"deleted:{child}", byAction[0].ActorId);

        // Exactly one "account.delete" summary row — the guardian branch
        // is Via: Guardian (the ADR 0028 "narrower standing" rule).
        var deletes = await AuditRows(boot.Store, action: "account.delete");
        Assert.Single(deletes);
        Assert.Equal(AccessVia.Guardian, deletes[0].Via);
        Assert.Equal(guardian, deletes[0].ActorId);
        Assert.Equal(child, deletes[0].TargetId);

        // C·5 — no dangling standing: every GuardianLink row for the child
        // is dissolved (no active or pending row remains).
        await using (var session = boot.Store.QuerySession())
        {
            var ct = TestContext.Current.CancellationToken;
            var remainingActive = await Marten.QueryableExtensions.CountAsync(
                session.Query<GuardianLink>()
                    .Where(l => l.ChildId == child && l.Status == GuardianLinkStatus.Active), ct);
            Assert.Equal(0, remainingActive);

            // Both links (the guardian's + the co-guardian's) are Dissolved,
            // stamped with the deleting guardian as DissolvedBy.
            var guardianLink = await session.LoadAsync<GuardianLink>(link.Id, ct);
            Assert.Equal(GuardianLinkStatus.Dissolved, guardianLink!.Status);
            Assert.Equal(guardian, guardianLink.DissolvedBy);

            var coLinkRow = await session.LoadAsync<GuardianLink>(coLink.Id, ct);
            Assert.Equal(GuardianLinkStatus.Dissolved, coLinkRow!.Status);
            Assert.Equal(guardian, coLinkRow.DissolvedBy);
        }
    }

    // ── Idempotency — a second call is refused (not a silent no-op) ────────
    // The standing gate runs first (the house convention); after a
    // successful delete the link is dissolved (C·5), so a second call is
    // refused by the standing gate with UnauthorizedAccessException (the
    // Web's 404). Either way the lane is not a silent no-op: a second
    // attempt cannot re-delete, and it is an error, not a success.

    [Fact]
    public async Task DeleteChildAccountAsync_SecondCall_Refused()
    {
        var boot = await BootAsync();
        var guardian = await SeedAccountAsync(boot, "guardian@examplium.com", globalAdmin: false);
        var child = await SeedAccountAsync(boot, "child@examplium.com", globalAdmin: false);

        var svc = new UserInfoService(boot.Store);
        await svc.CreateGuardianLinkAsync(child, guardian);

        await boot.Identity.DeleteChildAccountAsync(child, guardian);

        // The link was dissolved by the successful delete (C·5), so the
        // standing gate refuses the second call — not a silent no-op.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => boot.Identity.DeleteChildAccountAsync(child, guardian));
    }

    // ── Null/whitespace guard (the ArgumentException pin) ────────────────────

    [Fact]
    public async Task DeleteChildAccountAsync_NullChildId_ThrowsArgumentException()
    {
        var boot = await BootAsync();
        var guardian = await SeedAccountAsync(boot, "guardian@examplium.com", globalAdmin: false);
        await Assert.ThrowsAsync<ArgumentException>(
            () => boot.Identity.DeleteChildAccountAsync(null!, guardian));
    }

    // ── Harness (the IdentityServiceDeleteAccountTests Boot shape, verbatim) ──

    private sealed record Boot
    {
        public required IdentityService Identity { get; init; }
        public required UserManager<User> UserManager { get; init; }
        public required IDocumentStore Store { get; init; }
        public required DbContextOptions<AppDbContext> DbOptions { get; init; }
    }

    private async Task<Boot> BootAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);

        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            M1DocTypes.Configure(opts);
            M3DocTypes.Configure(opts);
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
            new EmptyServiceProvider(),
            NullLogger<UserManager<User>>.Instance);

        var userInfo = new UserInfoService(store);

        var identity = new IdentityService(
            userManager:          userManager,
            documentStore:        store,
            userInfo:             userInfo,
            claimsSource:         Substitute.For<IClaimsSource>(),
            mailer:               Substitute.For<IMailerStage>(),
            verificationOptions:  Options.Create(new VerificationOptions()),
            logger:               NullLogger<IdentityService>.Instance,
            translationProvider:  null,
            notifications:        null,
            sampleDataOptions:    Options.Create(new SampleDataOptions { Enabled = false }));

        return new Boot { Identity = identity, UserManager = userManager, Store = store, DbOptions = dbOptions };
    }

    private async Task<string> SeedAccountAsync(Boot boot, string email, bool globalAdmin)
    {
        var ct = TestContext.Current.CancellationToken;
        var mgr = boot.UserManager;

        var user = new User { Id = Guid.NewGuid().ToString("N"), Email = email, UserName = email };
        await mgr.CreateAsync(user, "Sup3rSecret!");

        if (globalAdmin)
        {
            await using var db = new AppDbContext(boot.DbOptions);
            if (!await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                    .AnyAsync(db.Roles.Where(r => r.Name == Roles.GlobalAdmin), ct))
            {
                db.Roles.Add(new IdentityRole
                {
                    Name = Roles.GlobalAdmin,
                    NormalizedName = Roles.GlobalAdmin.ToUpperInvariant()
                });
                await db.SaveChangesAsync(ct);
            }
            await mgr.AddToRoleAsync(user, Roles.GlobalAdmin);
        }

        await using var session = boot.Store.LightweightSession();
        session.Store(new Profile
        {
            SubjectId = user.Id,
            DisplayName = "Test Resident",
            Verified = true,
            Email = email
        });
        await session.SaveChangesAsync(ct);

        return user.Id ?? string.Empty;
    }

    private static async Task PlantAuditRowAsync(IDocumentStore store, string actorId, string action)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.LightweightSession();
        session.Store(new AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = action,
            TargetKind = "post",
            TargetId = "post-plant-1",
            Via = AccessVia.Owner,
            Outcome = AccessOutcome.Allow
        });
        await session.SaveChangesAsync(ct);
    }

    private static async Task<string> PlantGroupMembershipAsync(IDocumentStore store, string userId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.LightweightSession();
        var groupId = Guid.NewGuid().ToString("N");
        var membershipId = Guid.NewGuid().ToString("N");
        session.Store(new Group
        {
            Id = groupId,
            Name = "Test Group",
            OwnerId = userId,
            Created = DateTimeOffset.UtcNow
        });
        session.Store(new GroupMembership
        {
            Id = membershipId,
            GroupId = groupId,
            UserId = userId,
            AddedBy = userId,
            At = DateTimeOffset.UtcNow
        });
        await session.SaveChangesAsync(ct);
        return groupId;
    }

    private static async Task<Profile?> ProfileAsync(IDocumentStore store, string subjectId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.QuerySession();
        return await session.LoadAsync<Profile>(subjectId, ct);
    }

    private static async Task<GroupMembership?> GroupMembershipAsync(IDocumentStore store, string groupId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.QuerySession();
        return await Marten.QueryableExtensions
            .FirstOrDefaultAsync(session.Query<GroupMembership>().Where(m => m.GroupId == groupId), ct);
    }

    private static async Task<IReadOnlyList<AccessAudit>> AuditRows(
        IDocumentStore store, string? action = null)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.QuerySession();
        System.Linq.IQueryable<AccessAudit> query = session.Query<AccessAudit>();
        if (action is not null)
            query = query.Where(a => a.Action == action);
        return await Marten.QueryableExtensions.ToListAsync(query.OrderBy(a => a.At), ct);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
