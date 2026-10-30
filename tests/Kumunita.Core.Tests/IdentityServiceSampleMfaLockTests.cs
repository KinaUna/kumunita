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
/// ADR 0194 — the sample-data MFA (two-factor) lock. The lock is an additive
/// field on the <see cref="Kumunita.Core.Localization.LocaleSettings"/>
/// singleton (<c>SampleMfaLocked</c>), read via
/// <see cref="IdentityService.IsSampleMfaLockedAsync"/> (the
/// <c>false</c> floor — a fresh or real instance never blocks MFA) and written
/// via <see cref="IdentityService.SetSampleMfaLockedAsync"/> (exactly one
/// <see cref="AccessAudit"/> row, <c>Via = Admin</c>, action
/// <c>sample.set-mfa-lock</c>, <c>TargetKind</c> "sample").
/// <para>
/// The <see cref="IdentityService.IsMfaLockedForAsync"/> decision combines
/// the same four conditions as the ADR 0138
/// <see cref="IdentityService.IsChangePasswordLockedForAsync"/>:
/// (1) the <c>SampleData__Enabled</c> flag, (2) the admin MFA-lock gate,
/// (3) the subject is a member of the closed
/// <see cref="Bootstrap.SampleDataSeeder.SampleAccountEmails"/> set (by its
/// profile e-mail), and (4) the subject is NOT a
/// <see cref="Roles.GlobalAdmin"/> (the sample admin is exempt — it keeps its
/// own MFA lane, exactly as it keeps its own password lane under ADR 0138).
/// Every real (non-sample) account and the sample admin are never locked.
/// <para>
/// The <see cref="IdentityService.IsSampleDataAccountAsync"/> decision is
/// the two-conjunct read the login path (leg 1.5b) needs to exempt the
/// sample admin from the forced-MFA enrollment redirect: conjunct (1) + (3)
/// only (no admin-toggle gate, no GlobalAdmin check — the exemption the
/// login path needs is *precisely for* the sample GlobalAdmin). A real
/// (non-sample) account and a non-sample-data instance are never exempt.
/// <para>
/// Pinned the same way the ADR 0138 password lock is (via
/// <c>AccessAudit</c> shape + the live-on-next-read invariant + the four-
/// conjunct decision + the two-conjunct login-exemption decision), over a
/// fresh scratch Postgres (the <see cref="PostgresFixture"/> harness).
/// <c>IdentityService</c> is constructed with a real <c>UserManager</c>
/// (the role membership + the account lookup need the EF side) + a real
/// <c>UserInfoService</c> (the profile e-mail read) + substitutes for the
/// seams the gate does not touch.
/// </summary>
public sealed class IdentityServiceSampleMfaLockTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // A sample account's closed-set e-mail (a member of
    // SampleDataSeeder.SampleAccountEmails, the seeder's embedded document).
    private const string SampleEmail = Bootstrap.SampleDataSeeder.ModeratorEmail;

    // A non-sample e-mail (NOT a member of the closed set).
    private const string RealEmail = "real-resident@not-a-sample.org";

    // ── The `false` floor — a fresh instance never locks MFA ──────────────

    [Fact]
    public async Task IsSampleMfaLocked_FreshInstance_FloorsToFalse_NoAuditRow()
    {
        var boot = await BootAsync(sampleDataEnabled: false);

        // A fresh instance has no LocaleSettings row at all (the seeder has
        // not run in this scratch DB); the floor is `false` — a real instance
        // never blocks MFA (the same `false`-floor posture as
        // IsSamplePasswordChangeLockedAsync, ADR 0138 as re-applied to the
        // MFA surface).
        Assert.False(await boot.Identity.IsSampleMfaLockedAsync());

        // A read is a read — no audit row is committed for the floor probe.
        Assert.Empty(await AuditRows(boot.Store));
    }

    // ── The MFA lock on — the admin flips the gate (the singleton-toggle shape) ──

    [Fact]
    public async Task SetSampleMfaLocked_On_AuditRowShape_ViaAdmin_AndLiveOnNextRead()
    {
        var boot = await BootAsync(sampleDataEnabled: false);

        const string actor = "admin-mfa-01";
        await boot.Identity.SetSampleMfaLockedAsync(true, actor);

        // The value is live on the very next read (data, not config).
        Assert.True(await boot.Identity.IsSampleMfaLockedAsync());

        // Exactly one audit row (the single write), the ADR 0138 shape
        // (Via Admin, Outcome Allow), the singleton-toggle target
        // (TargetKind "sample", TargetId "sample", action
        // "sample.set-mfa-lock").
        var audits = await AuditRows(boot.Store, action: "sample.set-mfa-lock");
        Assert.Single(audits);
        var row = audits[0];
        Assert.Equal("sample.set-mfa-lock", row.Action);
        Assert.Equal("sample", row.TargetKind);
        Assert.Equal("sample", row.TargetId);
        Assert.Equal(AccessVia.Admin, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
        Assert.Equal(actor, row.ActorId);
        // A singleton toggle is not an access change — no VisibilityCount.
        Assert.Null(row.VisibleCount);
        Assert.Null(row.HiddenCount);
    }

    // ── Re-unlock — the admin flips it back (one row per flip) ────────────

    [Fact]
    public async Task SetSampleMfaLocked_Off_SetsFalse_AndSecondAuditRow()
    {
        var boot = await BootAsync(sampleDataEnabled: false);

        const string actor = "admin-mfa-02";
        await boot.Identity.SetSampleMfaLockedAsync(true, actor);
        Assert.True(await boot.Identity.IsSampleMfaLockedAsync());

        await boot.Identity.SetSampleMfaLockedAsync(false, actor);
        Assert.False(await boot.Identity.IsSampleMfaLockedAsync());

        var audits = await AuditRows(boot.Store, action: "sample.set-mfa-lock");
        Assert.Equal(2, audits.Count);
        Assert.All(audits, r =>
        {
            Assert.Equal(AccessVia.Admin, r.Via);
            Assert.Equal(actor, r.ActorId);
        });
    }

    // ── The two locks are independent (the ADR 0138 + ADR 0194 conjunct
    //    (2) gates are separate fields on the same singleton) ─────────────

    [Fact]
    public async Task SetSamplePasswordChangeLocked_DoesNotTouchMfaGate_AndViceVersa()
    {
        var boot = await BootAsync(sampleDataEnabled: false);

        // Flip the password lock on — the MFA gate stays `false` (the floor).
        await boot.Identity.SetSamplePasswordChangeLockedAsync(true, "admin-a");
        Assert.True(await boot.Identity.IsSamplePasswordChangeLockedAsync());
        Assert.False(await boot.Identity.IsSampleMfaLockedAsync());

        // Flip the MFA lock on — the password gate is unchanged (still `true`).
        await boot.Identity.SetSampleMfaLockedAsync(true, "admin-b");
        Assert.True(await boot.Identity.IsSampleMfaLockedAsync());
        Assert.True(await boot.Identity.IsSamplePasswordChangeLockedAsync());

        // Flip the MFA lock off — the password gate is still `true`.
        await boot.Identity.SetSampleMfaLockedAsync(false, "admin-c");
        Assert.False(await boot.Identity.IsSampleMfaLockedAsync());
        Assert.True(await boot.Identity.IsSamplePasswordChangeLockedAsync());
    }

    // ── The four-conjunct decision (IsMfaLockedForAsync) ─────────────────

    [Fact]
    public async Task IsMfaLockedFor_SampleDataDisabled_ReturnsFalse()
    {
        // Conjunct (1) fails: the instance is not a sample-data instance, so
        // the MFA lock is unreachable by construction (ADR 0056) — even with
        // the gate flipped on and a sample e-mail + non-admin role.
        var boot = await BootAsync(sampleDataEnabled: false);
        await boot.Identity.SetSampleMfaLockedAsync(true, "admin-x");

        var subject = await SeedAccountAsync(boot, email: SampleEmail, globalAdmin: false);
        Assert.False(await boot.Identity.IsMfaLockedForAsync(subject));
    }

    [Fact]
    public async Task IsMfaLockedFor_GateOff_ReturnsFalse()
    {
        // Conjunct (2) fails: the admin has not opted in to the MFA lock —
        // even with the SampleData__Enabled flag, a sample e-mail, and a
        // non-admin role.
        var boot = await BootAsync(sampleDataEnabled: true);
        // The gate is `false` (the floor) — never flipped on.

        var subject = await SeedAccountAsync(boot, email: SampleEmail, globalAdmin: false);
        Assert.False(await boot.Identity.IsMfaLockedForAsync(subject));
    }

    [Fact]
    public async Task IsMfaLockedFor_RealAccount_ReturnsFalse()
    {
        // Conjunct (3) fails: the account is not a member of the closed
        // SampleAccountEmails set (a real resident) — even with the flag +
        // the gate on and a non-admin role.
        var boot = await BootAsync(sampleDataEnabled: true);
        await boot.Identity.SetSampleMfaLockedAsync(true, "admin-x");

        var subject = await SeedAccountAsync(boot, email: RealEmail, globalAdmin: false);
        Assert.False(await boot.Identity.IsMfaLockedForAsync(subject));
    }

    [Fact]
    public async Task IsMfaLockedFor_SampleAdmin_IsExempt()
    {
        // Conjunct (4) fails: the account IS a sample e-mail, but it holds
        // GlobalAdmin (the seed admin) — the sample admin keeps its own MFA
        // lane, by construction (exactly as it keeps its own password lane
        // under ADR 0138).
        var boot = await BootAsync(sampleDataEnabled: true);
        await boot.Identity.SetSampleMfaLockedAsync(true, "admin-x");

        var subject = await SeedAccountAsync(boot, email: Bootstrap.SampleDataSeeder.AdminEmail, globalAdmin: true);
        Assert.False(await boot.Identity.IsMfaLockedForAsync(subject));
    }

    [Fact]
    public async Task IsMfaLockedFor_LockedSampleNonAdmin_ReturnsTrue()
    {
        // All four conjuncts hold: flag + gate on + sample e-mail + non-admin
        // role → the MFA settings surface is locked.
        var boot = await BootAsync(sampleDataEnabled: true);
        await boot.Identity.SetSampleMfaLockedAsync(true, "admin-x");

        var subject = await SeedAccountAsync(boot, email: SampleEmail, globalAdmin: false);
        Assert.True(await boot.Identity.IsMfaLockedForAsync(subject));
    }

    // ── The two-conjunct login-exemption decision (IsSampleDataAccountAsync) ──

    [Fact]
    public async Task IsSampleDataAccount_SampleDataDisabled_ReturnsFalse()
    {
        // Conjunct (1) fails: the instance is not a sample-data instance.
        // Even with a sample e-mail, the login exemption does not fire (the
        // ADR 0056 "unreachable by construction" shape).
        var boot = await BootAsync(sampleDataEnabled: false);
        var subject = await SeedAccountAsync(boot, email: SampleEmail, globalAdmin: true);
        Assert.False(await boot.Identity.IsSampleDataAccountAsync(subject));
    }

    [Fact]
    public async Task IsSampleDataAccount_RealAccount_ReturnsFalse()
    {
        // Conjunct (2) fails: the account is not a member of the closed
        // SampleAccountEmails set (a real resident).
        var boot = await BootAsync(sampleDataEnabled: true);
        var subject = await SeedAccountAsync(boot, email: RealEmail, globalAdmin: true);
        Assert.False(await boot.Identity.IsSampleDataAccountAsync(subject));
    }

    [Fact]
    public async Task IsSampleDataAccount_SampleAdmin_ReturnsTrue()
    {
        // Both conjuncts hold: the instance carries the flag AND the account
        // is a member of the closed set. The GlobalAdmin role does NOT
        // negate the exemption (the login path needs this *precisely for*
        // the sample GlobalAdmin — leg 1.5b is GlobalAdmin-only, so a
        // non-admin sample account never reaches it).
        var boot = await BootAsync(sampleDataEnabled: true);
        var subject = await SeedAccountAsync(boot, email: Bootstrap.SampleDataSeeder.AdminEmail, globalAdmin: true);
        Assert.True(await boot.Identity.IsSampleDataAccountAsync(subject));
    }

    [Fact]
    public async Task IsSampleDataAccount_SampleNonAdmin_ReturnsTrue()
    {
        // Both conjuncts hold for a non-admin sample account too (the
        // IsSampleDataAccountAsync seam does NOT apply the GlobalAdmin
        // exemption — that is the IsMfaLockedForAsync /
        // IsChangePasswordLockedForAsync job).
        var boot = await BootAsync(sampleDataEnabled: true);
        var subject = await SeedAccountAsync(boot, email: SampleEmail, globalAdmin: false);
        Assert.True(await boot.Identity.IsSampleDataAccountAsync(subject));
    }

    // ── The login-exemption decision is independent of the admin-toggle gate ──

    [Fact]
    public async Task IsSampleDataAccount_DoesNotConsultAdminToggleGate()
    {
        // The IsSampleDataAccountAsync seam is conjunct (1)+(3) only — it
        // does NOT consult the admin-toggle gate (SampleMfaLocked). A locked
        // instance still lets its admin sign in (that is the point of the
        // exemption: the admin needs to be able to sign in even when MFA
        // is locked for the sample accounts).
        var boot = await BootAsync(sampleDataEnabled: true);
        // Do NOT flip the gate on — it stays `false`.
        Assert.False(await boot.Identity.IsSampleMfaLockedAsync());

        var subject = await SeedAccountAsync(boot, email: Bootstrap.SampleDataSeeder.AdminEmail, globalAdmin: true);
        Assert.True(await boot.Identity.IsSampleDataAccountAsync(subject));

        // Flip the gate on — the exemption is still live (the gate is NOT
        // a conjunct of IsSampleDataAccountAsync).
        await boot.Identity.SetSampleMfaLockedAsync(true, "admin-y");
        Assert.True(await boot.Identity.IsSampleMfaLockedAsync());
        Assert.True(await boot.Identity.IsSampleDataAccountAsync(subject));
    }

    // ── The password lock and MFA lock are independent decisions ──────────

    [Fact]
    public async Task PasswordLockOn_MfaLockOff_LockedSample_MfaNotLocked_AndViceVersa()
    {
        var boot = await BootAsync(sampleDataEnabled: true);
        var subject = await SeedAccountAsync(boot, email: SampleEmail, globalAdmin: false);

        // Only the password gate is on → MFA is NOT locked.
        await boot.Identity.SetSamplePasswordChangeLockedAsync(true, "admin-p");
        Assert.True(await boot.Identity.IsChangePasswordLockedForAsync(subject));
        Assert.False(await boot.Identity.IsMfaLockedForAsync(subject));

        // Now the MFA gate is on too → both are locked.
        await boot.Identity.SetSampleMfaLockedAsync(true, "admin-m");
        Assert.True(await boot.Identity.IsChangePasswordLockedForAsync(subject));
        Assert.True(await boot.Identity.IsMfaLockedForAsync(subject));

        // Turn the MFA gate off → password still locked, MFA unlocked.
        await boot.Identity.SetSampleMfaLockedAsync(false, "admin-m2");
        Assert.True(await boot.Identity.IsChangePasswordLockedForAsync(subject));
        Assert.False(await boot.Identity.IsMfaLockedForAsync(subject));
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
            CoreDocTypes.Configure(opts);
            PostDocTypes.Configure(opts);
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
