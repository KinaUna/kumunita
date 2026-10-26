using Kumunita.Core;
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
/// F9 (AUTH-15: signup bot challenge + disposable-email blocklist) — **U4**, the
/// **Core half** of the pinned test <c>Signup_DisposableEmail_Rejected</c>
/// (design <c>prod-hardening-f9-signup.md</c> §5, assertions 1–3).
/// <para>
/// The blocklist check landed in F9.U2 as the **first statement** of
/// <see cref="IdentityService.RegisterCoreAsync"/> (before
/// <c>FindByEmailAsync</c>, before any EF write, before any Marten session —
/// the actor does not yet exist). This unit drives the **public** seam
/// <see cref="IdentityService.RegisterAsync"/> with an email whose domain is on
/// the committed list (<c>mailinator.com</c>, <c>disposable-emails.txt</c>
/// line 1) and asserts the three Core §5 assertions:
/// </para>
/// <list type="number">
/// <item>the blocklisted domain is **rejected** — a
/// <see cref="DisallowedEmailDomainException"/> is thrown (the **dedicated**
/// type — NOT the <see cref="InvalidOperationException"/> the
/// <c>FindByEmailAsync</c> duplicate check throws, and NOT swallowed by it);</item>
/// <item>**no EF write** — the <c>User</c> table count is unchanged (the
/// rejection is pre-<c>FindByEmailAsync</c>);</item>
/// <item>**no Marten write** — the <c>Profile</c> / <c>IdentityToken</c> /
/// <c>OutboxEmail</c> counts are unchanged (the session was never opened).</item>
/// </list>
/// <para>
/// The **Web half** of <c>Signup_DisposableEmail_Rejected</c> (assertion 4 —
/// the <c>ViewResult</c> + <c>EmailDomainDisallowed</c> re-render) and the
/// separate <c>Signup_BotChallengeEnforced</c> seam test live in
/// <c>Kumunita.Web.Tests/F9U4_SignupTests.cs</c> (the F3.U5 / F5.U4
/// direct-controller idiom). The harness mirrors
/// <see cref="IdentityServiceDeleteLaneGdprTests"/>/<see cref="F5U4_KillAllTests"/>
/// (<see cref="PostgresFixture"/> + a real EF <c>UserManager</c> + a real
/// <see cref="IdentityService"/>). Core stays HTTP-free — the seam is
/// constructed directly, no Web types.
/// </para>
public sealed class F9U4_DisposableEmailSignupTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    /// <summary>A domain **on** the committed blocklist (line 1 of <c>disposable-emails.txt</c>).</summary>
    private const string BlockedDomain = "mailinator.com";

    /// <summary>
    /// <c>Signup_DisposableEmail_Rejected</c> (pin §5, the **Core half** —
    /// assertions 1–3): a blocklisted email is rejected with the **dedicated**
    /// <see cref="DisallowedEmailDomainException"/> type (NOT
    /// <see cref="InvalidOperationException"/>), and **no writes** happened —
    /// the EF <c>User</c> count and the Marten <c>Profile</c> /
    /// <c>IdentityToken</c> / <c>OutboxEmail</c> counts are all **unchanged**
    /// (the rejection is pre-EF, pre-Marten).
    /// </summary>
    [Fact]
    public async Task Signup_DisposableEmail_Rejected()
    {
        var boot = await BootAsync();
        var ct   = TestContext.Current.CancellationToken;

        // Baseline counts on this fresh database (the "unchanged" contract, §5).
        long usersBefore     = await EfUsersCountAsync(boot.DbOptions, ct);
        int  profilesBefore  = await MartenProfileCountAsync(boot.Store, ct);
        int  tokensBefore    = await MartenTokenCountAsync(boot.Store, ct);
        int  outboxBefore    = await MartenOutboxCountAsync(boot.Store, ct);

        // A **new** email (not-yet-registered) that is also blocklisted — the
        // blocklist check fires **before** the FindByEmailAsync duplicate check,
        // so this is the pre-write rejection, not the duplicate path.
        string blockedEmail = "user@" + BlockedDomain;

        // Assertion 1 — the dedicated type is thrown (NOT InvalidOperationException).
        // xunit's ThrowsAsync<T> fails if a different exception type is thrown, so
        // a FindByEmailAsync-style InvalidOperationException would not pass this.
        var ex = await Assert.ThrowsAsync<DisallowedEmailDomainException>(
            () => boot.Identity.RegisterAsync("x", blockedEmail, "Password123!"));

        // The structured payload (Email / Domain) the Web reads to build its
        // localized message — pin §3a.
        Assert.Equal(blockedEmail, ex.Email);
        Assert.Equal(BlockedDomain, ex.Domain);

        // Assertion 2 — no EF write (the User count is unchanged, pre-FindByEmailAsync).
        long usersAfter = await EfUsersCountAsync(boot.DbOptions, ct);
        Assert.Equal(usersBefore, usersAfter);

        // Assertion 3 — no Marten write (the session was never opened).
        Assert.Equal(profilesBefore, await MartenProfileCountAsync(boot.Store, ct));
        Assert.Equal(tokensBefore,   await MartenTokenCountAsync(boot.Store, ct));
        Assert.Equal(outboxBefore,   await MartenOutboxCountAsync(boot.Store, ct));
    }

    // ── Counting helpers (the PortabilityRoundTripTests inline idiom — a concrete
    //    type per call, so `Marten.QueryableExtensions.CountAsync` is a direct,
    //    unambiguous call, not a generic extension-method resolution that would
    //    collide with EF Core's `CountAsync`.) ────────────────────────────────

    private static async Task<long> EfUsersCountAsync(DbContextOptions<AppDbContext> dbOptions, CancellationToken ct)
    {
        await using var db = new AppDbContext(dbOptions);
        // Fully-qualified: with both `Microsoft.EntityFrameworkCore` and `Marten` in
        // scope, an unqualified `CountAsync` on `IQueryable<User>` is ambiguous
        // between the two extension sets.
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .CountAsync(db.Users, ct);
    }

    private static async Task<int> MartenProfileCountAsync(IDocumentStore store, CancellationToken ct)
    {
        await using var q = store.QuerySession();
        return await Marten.QueryableExtensions.CountAsync(q.Query<Profile>(), ct);
    }

    private static async Task<int> MartenTokenCountAsync(IDocumentStore store, CancellationToken ct)
    {
        await using var q = store.QuerySession();
        return await Marten.QueryableExtensions.CountAsync(q.Query<IdentityToken>(), ct);
    }

    private static async Task<int> MartenOutboxCountAsync(IDocumentStore store, CancellationToken ct)
    {
        await using var q = store.QuerySession();
        return await Marten.QueryableExtensions.CountAsync(q.Query<OutboxEmail>(), ct);
    }

    // ── Harness (the F1 IdentityServiceDeleteLaneGdprTests.BootAsync shape) ─

    private sealed record Boot
    {
        public required IdentityService            Identity   { get; init; }
        public required IDocumentStore             Store      { get; init; }
        public required DbContextOptions<AppDbContext> DbOptions { get; init; }
    }

    private async Task<Boot> BootAsync()
    {
        var ct   = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);

        // Real Marten store (the ADR 0006 single-write seam surface — the
        // Profile / IdentityToken / OutboxEmail tables the "no write"
        // assertion counts). No MediaDocTypes needed (the rejection is
        // pre-Marten and never touches a byte store — mediaStore: null is
        // therefore not a vacuous-green risk here; the rejection precedes the
        // store entirely).
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            CoreDocTypes.Configure(opts);
            PostDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        // Real EF store (the Identity schema — the User table the "no write"
        // assertion counts).
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

        // The real Core IdentityService (the ADR 0006 single-write seam — the
        // RegisterAsync → RegisterCoreAsync lane the blocklist check guards).
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
            sampleDataOptions:    Options.Create(new SampleDataOptions { Enabled = false }),
            mediaStore:           null);

        return new Boot { Identity = identity, Store = store, DbOptions = dbOptions };
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
