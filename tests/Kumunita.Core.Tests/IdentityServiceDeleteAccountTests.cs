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
/// ADR 0142 — the delete-account lane (<see cref="IdentityService
/// .DeleteAccountAsync"/>). Two branches, one seam:
/// </summary>
/// <list type="bullet">
/// <item><b>Self-deletion</b> (actor == target): the resident deletes their
/// own account. ADR 0142 D5 gate — only a <c>GlobalAdmin</c> may self-delete
/// (a non-GlobalAdmin's invocation throws
/// <see cref="UnauthorizedAccessException"/> — the fail-closed pin). The audit
/// row is <c>Via: Owner</c>.</item>
/// <item><b>Admin-initiated</b> (actor ≠ target): a GlobalAdmin removes
/// another resident. The GlobalAdmin gate applies (a non-GlobalAdmin's
/// invocation throws <see cref="UnauthorizedAccessException"/>). The audit row
/// is <c>Via: Admin</c>.</item>
/// </list>
/// <para>
/// **Both branches:** the last-GlobalAdmin guard refuses before any write (a
/// lone GlobalAdmin cannot be deleted — the OPS.md §9 "Hand over admin"
/// precedent). On success: the target's <see cref="AccessAudit"/> rows are
/// <b>pseudonymized</b> (actor id → <c>deleted:{subjectId}</c> tombstone, the
/// ARCHITECTURE.md §5 / OPS.md §9 "Deletion-of-account interaction" — rows
/// remain, identity is replaced), their
/// <see cref="GroupMembership"/> / <see cref="ComponentMembership"/> /
/// <see cref="ModeratorAssignment"/> rows are removed, their
/// <see cref="Profile"/> is deleted, the Identity account is deleted (the
/// password hash and roles are gone), and exactly one
/// <c>"account.delete"</c> audit row is written (the
/// <c>AuditPurgeSummary</c> "one summary, many rows" shape).
/// </para>
/// <para>
/// Pinned the same way the ADR 0138 lane is (via <c>AccessAudit</c> shape +
/// the fail-closed pin + the pseudonymization invariant), over a fresh
/// scratch Postgres (the <see cref="PostgresFixture"/> harness).
/// </para>
/// </summary>
public sealed class IdentityServiceDeleteAccountTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── Self-deletion (actor == target) ────────────────────────────────────

    [Fact]
    public async Task DeleteAccountAsync_SelfDeletion_NonGlobalAdmin_ThrowsUnauthorized()
    {
        var boot = await BootAsync();
        // A plain resident (no GlobalAdmin role) attempting self-deletion —
        // the ADR 0142 D5 gate refuses before any write.
        var self = await SeedAccountAsync(boot, "self-nonadmin@examplium.com", globalAdmin: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => boot.Identity.DeleteAccountAsync(self, adminSubjectId: self));

        // No write happened: no "account.delete" audit row, the account still
        // exists, the profile row is intact (the fail-closed pin).
        Assert.Empty(await AuditRows(boot.Store, action: "account.delete"));
        Assert.NotNull(await boot.UserManager.FindByIdAsync(self));
        Assert.NotNull(await ProfileAsync(boot.Store, self));
    }

    [Fact]
    public async Task DeleteAccountAsync_SelfDeletion_GA_Successful_DeletesAndPseudonymizes()
    {
        var boot = await BootAsync();
        // A GlobalAdmin self-deleting (they hold the standing the D5 gate
        // requires), plus a second GlobalAdmin (the last-GA guard is
        // satisfied — the target is not the *last* GA).
        var self = await SeedAccountAsync(boot, "self-ga@examplium.com", globalAdmin: true);
        await SeedAccountAsync(boot, "other-ga@examplium.com", globalAdmin: true);

        // Pre-plant an audit row *by* the target (a past action) + a group
        // membership (the target belongs to a group) — both should be
        // pseudonymized / removed on delete.
        await PlantAuditRowAsync(boot.Store, actorId: self, action: "post.read");
        var groupId = await PlantGroupMembershipAsync(boot.Store, userId: self);

        await boot.Identity.DeleteAccountAsync(self, adminSubjectId: self);

        // The Identity account is gone (the password hash + roles are deleted
        // — the FindByIdAsync is null).
        Assert.Null(await boot.UserManager.FindByIdAsync(self));

        // The Profile row is deleted (the directory would list no one; the
        // author-name resolution falls back to the raw subject id).
        Assert.Null(await ProfileAsync(boot.Store, self));

        // The group membership is removed (the strong-consistency C4
        // invariant — the next GetGroupIdsAsync misses it).
        Assert.Null(await GroupMembershipAsync(boot.Store, groupId));

        // The target's pre-planted audit row is pseudonymized: the row
        // *remains* (the trail is preserved, the OPS.md §9 policy) but the
        // actor id is the tombstone.
        var byAction = await AuditRows(boot.Store, action: "post.read");
        Assert.Single(byAction);
        Assert.Equal($"deleted:{self}", byAction[0].ActorId);

        // Exactly one "account.delete" summary row — the self-serve branch
        // is Via: Owner (the resident acted on their own account).
        var deletes = await AuditRows(boot.Store, action: "account.delete");
        Assert.Single(deletes);
        Assert.Equal(AccessVia.Owner, deletes[0].Via);
        Assert.Equal(self, deletes[0].ActorId);
        Assert.Equal("account", deletes[0].TargetKind);
        Assert.Equal(self, deletes[0].TargetId);
        Assert.Equal(AccessOutcome.Allow, deletes[0].Outcome);
    }

    [Fact]
    public async Task DeleteAccountAsync_SelfDeletion_LastGlobalAdmin_ThrowsAndDoesNotWrite()
    {
        var boot = await BootAsync();
        // A lone GlobalAdmin self-deleting — the last-GlobalAdmin guard
        // refuses before any write (the OPS.md §9 "Hand over admin"
        // precedent: the recovery path is to promote a second GA first).
        var self = await SeedAccountAsync(boot, "last-ga@examplium.com", globalAdmin: true);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => boot.Identity.DeleteAccountAsync(self, adminSubjectId: self));

        // No write happened: no "account.delete" audit row, the account
        // still exists (the fail-closed pin).
        Assert.Empty(await AuditRows(boot.Store, action: "account.delete"));
        Assert.NotNull(await boot.UserManager.FindByIdAsync(self));
        Assert.NotNull(await ProfileAsync(boot.Store, self));
    }

    [Fact]
    public async Task DeleteAccountAsync_SelfDeletion_MissingAccount_Throws()
    {
        var boot = await BootAsync();
        const string unknown = "no-such-account-id";
        // The idempotency pin: a missing account is an error, not a silent
        // no-op (the ADR 0138 "a non-GlobalAdmin learns nothing" shape,
        // adapted — the actor is the missing subject themselves).
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => boot.Identity.DeleteAccountAsync(unknown, adminSubjectId: unknown));
    }

    // ── Admin-initiated (actor ≠ target) ────────────────────────────────────

    [Fact]
    public async Task DeleteAccountAsync_AdminInitiated_NonGlobalAdminActor_ThrowsUnauthorized()
    {
        var boot = await BootAsync();
        var actor = await SeedAccountAsync(boot, "actor-nonadmin@examplium.com", globalAdmin: false);
        var target = await SeedAccountAsync(boot, "target@examplium.com", globalAdmin: false);

        // A non-GlobalAdmin cannot delete another account (the
        // BlockAsync / UnblockAsync precedent — the fail-closed pin).
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => boot.Identity.DeleteAccountAsync(target, adminSubjectId: actor));

        Assert.Empty(await AuditRows(boot.Store, action: "account.delete"));
        Assert.NotNull(await boot.UserManager.FindByIdAsync(target));
    }

    [Fact]
    public async Task DeleteAccountAsync_AdminInitiated_GA_Successful_ViaAdmin()
    {
        var boot = await BootAsync();
        var actor = await SeedAccountAsync(boot, "admin-ga@examplium.com", globalAdmin: true);
        var target = await SeedAccountAsync(boot, "target-ga@examplium.com", globalAdmin: true);

        // Pre-plant an audit row *by* the target + a component membership.
        await PlantAuditRowAsync(boot.Store, actorId: target, action: "post.read");
        var cmId = await PlantComponentMembershipAsync(boot.Store, userId: target);

        await boot.Identity.DeleteAccountAsync(target, adminSubjectId: actor);

        // The target is gone (Identity + Profile + memberships), the actor
        // is untouched (a different account).
        Assert.Null(await boot.UserManager.FindByIdAsync(target));
        Assert.NotNull(await boot.UserManager.FindByIdAsync(actor));
        Assert.Null(await ProfileAsync(boot.Store, target));
        Assert.NotNull(await ProfileAsync(boot.Store, actor));
        Assert.Null(await ComponentMembershipAsync(boot.Store, cmId));

        // The target's audit row is pseudonymized (the actor id → tombstone).
        var byAction = await AuditRows(boot.Store, action: "post.read");
        Assert.Single(byAction);
        Assert.Equal($"deleted:{target}", byAction[0].ActorId);

        // Exactly one "account.delete" summary row — the admin-initiated
        // branch is Via: Admin (a GlobalAdmin removed another resident).
        var deletes = await AuditRows(boot.Store, action: "account.delete");
        Assert.Single(deletes);
        Assert.Equal(AccessVia.Admin, deletes[0].Via);
        Assert.Equal(actor, deletes[0].ActorId);
        Assert.Equal(target, deletes[0].TargetId);
    }

    [Fact]
    public async Task DeleteAccountAsync_AdminInitiated_TargetIsGlobalAdmin_GuardPasses_BecauseActorIsAlsoGA()
    {
        // A design consequence worth pinning (the ADR 0142 last-GA guard
        // is only reachable on the *self* branch): on the admin-initiated
        // branch, the actor must be a GlobalAdmin to pass the admin gate —
        // so when the target is also a GlobalAdmin, there are at least
        // two GAs on the instance (actor + target), and the last-GlobalAdmin
        // guard never fires. The guard is a no-op on this branch; it
        // *can* fire on the self branch (a lone GA deleting themselves),
        // which DeleteAccountAsync_SelfDeletion_LastGlobalAdmin_Throws
        // covers.
        //
        // This test pins that the admin branch *can* delete a GlobalAdmin
        // target (when the actor is a different GA) — i.e. the guard does
        // NOT block a legitimate "second admin removes a departing
        // GlobalAdmin" scenario.
        var boot = await BootAsync();
        var actor  = await SeedAccountAsync(boot, "admin-ga@examplium.com", globalAdmin: true);
        var target = await SeedAccountAsync(boot, "departing-ga@examplium.com", globalAdmin: true);

        // Two GAs exist (actor + target) — the guard passes.
        await boot.Identity.DeleteAccountAsync(target, adminSubjectId: actor);
        Assert.Null(await boot.UserManager.FindByIdAsync(target));
        Assert.NotNull(await boot.UserManager.FindByIdAsync(actor));
    }

    [Fact]
    public async Task DeleteAccountAsync_AdminInitiated_SecondCall_Throws()
    {
        var boot = await BootAsync();
        var actor = await SeedAccountAsync(boot, "admin-ga@examplium.com", globalAdmin: true);
        var target = await SeedAccountAsync(boot, "target@examplium.com", globalAdmin: false);
        await boot.Identity.DeleteAccountAsync(target, adminSubjectId: actor);

        // A second call for the same subject (the account is already gone)
        // throws — the lane is not a silent no-op (the idempotency pin).
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => boot.Identity.DeleteAccountAsync(target, adminSubjectId: actor));
    }

    // ── Harness ───────────────────────────────────────────────────────────

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

    private static async Task<string> PlantComponentMembershipAsync(IDocumentStore store, string userId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.LightweightSession();
        var componentId = Guid.NewGuid().ToString("N");
        var membershipId = Guid.NewGuid().ToString("N");
        session.Store(new Component
        {
            Id = componentId,
            Name = "Test Community",
            Enabled = true
        });
        session.Store(new ComponentMembership
        {
            Id = membershipId,
            ComponentId = componentId,
            UserId = userId,
            AddedBy = userId,
            At = DateTimeOffset.UtcNow
        });
        await session.SaveChangesAsync(ct);
        return membershipId;
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

    private static async Task<ComponentMembership?> ComponentMembershipAsync(IDocumentStore store, string membershipId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.QuerySession();
        return await session.LoadAsync<ComponentMembership>(membershipId, ct);
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
