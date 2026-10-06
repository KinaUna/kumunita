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
/// ADR 0138 — the sample-data change-password lock. The lock is an additive
/// field on the <see cref="Kumunita.Core.Localization.LocaleSettings"/>
/// singleton (the same doc the ADR 0050 / ADR 0077 / ADR 0019 / ADR 0020
/// lanes use), read via
/// <see cref="IdentityService.IsSamplePasswordChangeLockedAsync"/> (the
/// <c>false</c> floor — a fresh or real instance never blocks a password
/// change) and written via
/// <see cref="IdentityService.SetSamplePasswordChangeLockedAsync"/> (exactly
/// one <see cref="AccessAudit"/> row, <c>Via = Admin</c>, action
/// <c>sample.set-password-lock</c>, <c>TargetKind</c> "sample").
/// <para>
/// The <see cref="IdentityService.IsChangePasswordLockedForAsync"/> decision
/// combines four conditions into one place: (1) the
/// <c>SampleData__Enabled</c> flag, (2) the admin lock gate, (3) the subject
/// is a member of the closed
/// <see cref="Bootstrap.SampleDataSeeder.SampleAccountEmails"/> set (by its
/// profile e-mail), and (4) the subject is NOT a
/// <see cref="Roles.GlobalAdmin"/> (the sample admin is exempt). Every real
/// (non-sample) account and the sample admin are never locked.
/// <para>
/// Pinned the same way the ADR 0050 / ADR 0077 lanes are (via
/// <c>AccessAudit</c> shape + the live-on-next-read invariant + the four-
/// conjunct decision), over a fresh scratch Postgres (the
/// <see cref="PostgresFixture"/> harness). <c>IdentityService</c> is
/// constructed with a real <c>UserManager</c> (the role membership + the
/// account lookup need the EF side) + a real <c>UserInfoService</c> (the
/// profile e-mail read) + substitutes for the seams the gate does not touch.
/// </para>
/// </summary>
public sealed class IdentityServiceSamplePasswordLockTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // A sample account's closed-set e-mail (a member of
    // SampleDataSeeder.SampleAccountEmails, the seeder's embedded document).
    private const string SampleEmail = Bootstrap.SampleDataSeeder.ModeratorEmail;

    // A non-sample e-mail (NOT a member of the closed set).
    private const string RealEmail = "real-resident@not-a-sample.org";

    // ── The `false` floor — a fresh instance never locks a password change ─

    [Fact]
    public async Task IsSamplePasswordChangeLocked_FreshInstance_FloorsToFalse_NoAuditRow()
    {
        var boot = await BootAsync(sampleDataEnabled: false);

        // A fresh instance has no LocaleSettings row at all (the seeder has
        // not run in this scratch DB); the floor is `false` — a real instance
        // never blocks a password change (the same `false`-floor posture as the
        // IsSignupOpenAsync floor, ADR 0050 as amended 2026-10-05).
        Assert.False(await boot.Identity.IsSamplePasswordChangeLockedAsync());

        // A read is a read — no audit row is committed for the floor probe.
        Assert.Empty(await AuditRows(boot.Store));
    }

    // ── The lock on — the admin flips the gate (the ADR 0050 singleton-toggle shape) ──

    [Fact]
    public async Task SetSamplePasswordChangeLocked_On_AuditRowShape_ViaAdmin_AndLiveOnNextRead()
    {
        var boot = await BootAsync(sampleDataEnabled: false);

        const string actor = "admin-s50";
        await boot.Identity.SetSamplePasswordChangeLockedAsync(true, actor);

        // The value is live on the very next read (data, not config).
        Assert.True(await boot.Identity.IsSamplePasswordChangeLockedAsync());

        // Exactly one audit row (the single write), the ADR 0019 / ADR 0020
        // shape (Via Admin, Outcome Allow), the singleton-toggle target
        // (TargetKind "sample", TargetId "sample", action
        // "sample.set-password-lock").
        var audits = await AuditRows(boot.Store, action: "sample.set-password-lock");
        Assert.Single(audits);
        var row = audits[0];
        Assert.Equal("sample.set-password-lock", row.Action);
        Assert.Equal("sample", row.TargetKind);
        Assert.Equal("sample", row.TargetId);
        Assert.Equal(AccessVia.Admin, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
        Assert.Equal(actor, row.ActorId);
        // A singleton toggle is not an access change — no VisibilityCount
        // (the single-target shape, ADR 0006 §B).
        Assert.Null(row.VisibleCount);
        Assert.Null(row.HiddenCount);
    }

    // ── Re-unlock — the admin flips it back (the lane is additive — one row per flip) ──

    [Fact]
    public async Task SetSamplePasswordChangeLocked_Off_SetsFalse_AndSecondAuditRow()
    {
        var boot = await BootAsync(sampleDataEnabled: false);

        const string actor = "admin-s50";
        await boot.Identity.SetSamplePasswordChangeLockedAsync(true, actor);
        Assert.True(await boot.Identity.IsSamplePasswordChangeLockedAsync());

        await boot.Identity.SetSamplePasswordChangeLockedAsync(false, actor);
        Assert.False(await boot.Identity.IsSamplePasswordChangeLockedAsync());

        var audits = await AuditRows(boot.Store, action: "sample.set-password-lock");
        Assert.Equal(2, audits.Count);
        Assert.All(audits, r =>
        {
            Assert.Equal(AccessVia.Admin, r.Via);
            Assert.Equal(actor, r.ActorId);
        });
    }

    // ── The four-conjunct decision (IsChangePasswordLockedForAsync) ───────

    [Fact]
    public async Task IsChangePasswordLockedFor_SampleDataDisabled_ReturnsFalse()
    {
        // Conjunct (1) fails: the instance is not a sample-data instance, so
        // the lock is unreachable by construction (ADR 0056) — even with the
        // gate flipped on and a sample e-mail + non-admin role.
        var boot = await BootAsync(sampleDataEnabled: false);
        await boot.Identity.SetSamplePasswordChangeLockedAsync(true, "admin-x");

        var subject = await SeedAccountAsync(boot, email: SampleEmail, globalAdmin: false);
        Assert.False(await boot.Identity.IsChangePasswordLockedForAsync(subject));
    }

    [Fact]
    public async Task IsChangePasswordLockedFor_GateOff_ReturnsFalse()
    {
        // Conjunct (2) fails: the admin has not opted in — even with the
        // SampleData__Enabled flag, a sample e-mail, and a non-admin role.
        var boot = await BootAsync(sampleDataEnabled: true);
        // The gate is `false` (the floor) — never flipped on.

        var subject = await SeedAccountAsync(boot, email: SampleEmail, globalAdmin: false);
        Assert.False(await boot.Identity.IsChangePasswordLockedForAsync(subject));
    }

    [Fact]
    public async Task IsChangePasswordLockedFor_RealAccount_ReturnsFalse()
    {
        // Conjunct (3) fails: the account is not a member of the closed
        // SampleAccountEmails set (a real resident) — even with the flag +
        // the gate on and a non-admin role.
        var boot = await BootAsync(sampleDataEnabled: true);
        await boot.Identity.SetSamplePasswordChangeLockedAsync(true, "admin-x");

        var subject = await SeedAccountAsync(boot, email: RealEmail, globalAdmin: false);
        Assert.False(await boot.Identity.IsChangePasswordLockedForAsync(subject));
    }

    [Fact]
    public async Task IsChangePasswordLockedFor_SampleAdmin_IsExempt()
    {
        // Conjunct (4) fails: the account IS a sample e-mail, but it holds
        // GlobalAdmin (the seed admin) — the sample admin keeps its own
        // password lane, by construction.
        var boot = await BootAsync(sampleDataEnabled: true);
        await boot.Identity.SetSamplePasswordChangeLockedAsync(true, "admin-x");

        var subject = await SeedAccountAsync(boot, email: Bootstrap.SampleDataSeeder.AdminEmail, globalAdmin: true);
        Assert.False(await boot.Identity.IsChangePasswordLockedForAsync(subject));
    }

    [Fact]
    public async Task IsChangePasswordLockedFor_LockedSampleNonAdmin_ReturnsTrue()
    {
        // All four conjuncts hold: flag + gate on + sample e-mail + non-admin
        // role → the self-serve password change is locked.
        var boot = await BootAsync(sampleDataEnabled: true);
        await boot.Identity.SetSamplePasswordChangeLockedAsync(true, "admin-x");

        var subject = await SeedAccountAsync(boot, email: SampleEmail, globalAdmin: false);
        Assert.True(await boot.Identity.IsChangePasswordLockedForAsync(subject));
    }

    // ── The enforcement guard (ChangePasswordAsync, self-serve lane only) ──

    [Fact]
    public async Task ChangePasswordAsync_LockedSampleAccount_ThrowsAndDoesNotWrite()
    {
        var boot = await BootAsync(sampleDataEnabled: true);
        await boot.Identity.SetSamplePasswordChangeLockedAsync(true, "admin-x");
        var subject = await SeedAccountAsync(boot, email: SampleEmail, globalAdmin: false);

        // The self-serve lane (byAdmin: false) is denied by the guard, before
        // any write — no audit row for the blocked attempt (the fail-closed pin).
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => boot.Identity.ChangePasswordAsync(subject, "NewSup3rSecret!", byAdmin: false));
        Assert.Empty(await AuditRows(boot.Store, action: "password.change"));
    }

    [Fact]
    public async Task ChangePasswordAsync_AdminResetOfLockedSampleAccount_IsAllowed()
    {
        // The byAdmin lane (an admin resetting a demo credential they set) is
        // always allowed — the guard is a no-op for byAdmin.
        var boot = await BootAsync(sampleDataEnabled: true);
        await boot.Identity.SetSamplePasswordChangeLockedAsync(true, "admin-x");
        var subject = await SeedAccountAsync(boot, email: SampleEmail, globalAdmin: false);

        await boot.Identity.ChangePasswordAsync(subject, "NewSup3rSecret!", byAdmin: true);
        var audits = await AuditRows(boot.Store, action: "password.change");
        Assert.Single(audits);
        Assert.Equal(AccessVia.Admin, audits[0].Via);
    }

    // ── Harness ───────────────────────────────────────────────────────────

    private sealed record Boot
    {
        public required IdentityService Identity { get; init; }
        public required UserManager<User> UserManager { get; init; }
        public required IDocumentStore Store { get; init; }
        public required DbContextOptions<AppDbContext> DbOptions { get; init; }
    }

    /// <summary>
    /// A real Marten store over a fresh scratch Postgres (the
    /// <see cref="PostgresFixture"/> harness) + a real EF
    /// <see cref="UserManager{TUser}"/> (the role membership + account lookup
    /// the decision needs) + a real <see cref="UserInfoService"/> (the profile
    /// e-mail read) + an <see cref="IdentityService"/> wired with substitutes
    /// for the seams the gate does not touch. <paramref
    /// name="sampleDataEnabled"/> controls the <c>SampleData__Enabled</c> flag
    /// (conjunct 1 of the decision).
    /// </summary>
    private async Task<Boot> BootAsync(bool sampleDataEnabled)
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
            M3DocTypes.Configure(opts);
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
            sampleDataOptions:    Options.Create(new SampleDataOptions { Enabled = sampleDataEnabled }));

        return new Boot { Identity = identity, UserManager = userManager, Store = store, DbOptions = dbOptions };
    }

    /// <summary>
    /// Seeds one account: the EF account (with a password) + a planted,
    /// verified <see cref="Profile"/> carrying the given <paramref name="email"/>
    /// (the closed-set membership read) + the <see cref="Roles.GlobalAdmin"/>
    /// role when <paramref name="globalAdmin"/> is set. Returns the account's
    /// SubjectId.
    /// </summary>
    private async Task<string> SeedAccountAsync(Boot boot, string email, bool globalAdmin)
    {
        var ct = TestContext.Current.CancellationToken;
        var mgr = boot.UserManager;

        var user = new User { Id = Guid.NewGuid().ToString("N"), Email = email, UserName = email };
        await mgr.CreateAsync(user, "Sup3rSecret!");

        if (globalAdmin)
        {
            // The IdentityRole row (the GetUsersInRoleAsync query queries on
            // Role.NormalizedName, which the UpperInvariantLookupNormalizer
            // uppercases — so it must be the upper-invariant form; a direct
            // db.Roles.Add with the raw name would leave the normalized lookup
            // matching nothing — the harness precedent).
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

        // The profile (the e-mail read) — a verified resident with the given
        // e-mail (the closed-set membership input).
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

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
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
}
