using System.Security.Claims;
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
using Npgsql;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// ADR 0146 — the child-account formation + password-at-the-link lane.
///
/// <para>
/// Two Core seams, one behavior: a child account is created **without a
/// password** (the guardian does not hold the child's credential — the ADR 0028
/// "supervision rides the link, not the password" shape), and the child sets
/// <b>their own</b> password when they click the confirmation link
/// (<see cref="IIdentityService.VerifyAndSetPasswordAsync"/>). The tests pin
/// the two Core contracts the Web surface depends on:
/// </para>
/// <list type="number">
/// <item><b>Formation.</b> <see cref="IIdentityService.RegisterChildAccountAsync"/>
/// creates an unverified account whose <c>PasswordHash</c> is empty (a
/// password-less account — the credential the guardian never holds). The
/// <see cref="IdentityToken"/> is staged as usual, so the child can click the
/// link.</item>
/// <item><b>Activation.</b> <see cref="IIdentityService.VerifyAndSetPasswordAsync"/>
/// sets the child's own password, flips <c>Profile.Verified</c>, and consumes
/// the token — one commit. After the call, the account signs in with the
/// password the child chose (the <see cref="UserManager{TUser}.CheckPasswordAsync"/>
/// seam succeeds, the <c>Verified</c> flag is live, and the token is consumed).</item>
/// </list>
///
/// <para>
/// <b>Harness:</b> the GuardianAssignmentTests two-store boot (the <c>mt</c>
/// document schema + the EF-migrated <c>identity</c> schema + the real EF
/// UserStore/UserManager), wired with a real <see cref="IdentityService"/>
/// (the mailer + translation doubles are minimal — the Identity outbox stage
/// and the localization fallback both no-op for these assertions).
/// </para>
/// </summary>
public sealed class ChildAccountPasswordTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── 1 — Formation: the child account is password-less ────────────────

    [Fact]
    public async Task RegisterChildAccount_CreatesAnUnverifiedAccountWithNoPassword()
    {
        var boot = await BootAsync();
        var email = "child-formation@example.com";

        var principal = await boot.Identity.RegisterChildAccountAsync("Child One", email);
        Assert.False(string.IsNullOrWhiteSpace(principal.SubjectId));
        Assert.False(principal.IsVerifiedResident);   // unverified at formation

        // The credential the guardian never held: the account's PasswordHash is
        // empty (a password-less account, the ADR 0146 child-lane shape). A
        // CheckPasswordAsync against any candidate password fails.
        var user = await boot.UserManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.True(string.IsNullOrEmpty(user!.PasswordHash));
        Assert.False(await boot.UserManager.CheckPasswordAsync(user, "WhateverTheChildMightTry1"));
    }

    // ── 2 — Activation: the child sets their own password at the link ─────

    [Fact]
    public async Task VerifyAndSetPassword_SetsTheChildsOwnCredential_AndSignsIn()
    {
        var boot = await BootAsync();
        var email = "child-activation@example.com";
        const string ChildPassword = "ChildOwnsThisCredential1!";

        // Formation (the ADR 0146 child lane).
        var principal = await boot.Identity.RegisterChildAccountAsync("Child Two", email);
        var subjectId = principal.SubjectId;

        // The single-use token row is the just-registered account's (a fresh
        // scratch DB — the same assumption the GuardianAssignmentTests helper
        // makes). Read it back the way the Web's /account/verify action would.
        var token = await ReadLatestVerifyTokenAsync(boot.Store, subjectId);
        Assert.NotNull(token);

        // Before activation: the account is unverified and password-less.
        var profileBefore = await boot.UserInfo.GetProfileAsync(subjectId);
        Assert.False(profileBefore!.Verified);
        var user = await boot.UserManager.FindByIdAsync(subjectId);
        Assert.NotNull(user);
        Assert.True(string.IsNullOrEmpty(user!.PasswordHash));

        // Activation: the child sets their own password at the confirmation link.
        var profileAfter = await boot.Identity.VerifyAndSetPasswordAsync(token!.Token, ChildPassword);
        Assert.True(profileAfter.Verified);

        // Re-fetch the user: CheckPasswordAsync reads the in-memory PasswordHash,
        // which the pre-activation capture still shows empty. The freshly loaded
        // row carries the credential the child just set.
        var userAfter = await boot.UserManager.FindByIdAsync(subjectId);
        Assert.NotNull(userAfter);
        Assert.False(string.IsNullOrEmpty(userAfter!.PasswordHash));

        // The credential the child set is live: CheckPasswordAsync succeeds (the
        // sign-in lane will accept it) and the token is consumed (the link is
        // single-use — a second presentation is refused).
        Assert.True(await boot.UserManager.CheckPasswordAsync(userAfter, ChildPassword));
        Assert.False(await boot.UserManager.CheckPasswordAsync(userAfter, "GuardianSetThisInstead!"));

        // The token is consumed: a second VerifyAndSetPasswordAsync with the same
        // token is refused (the FindTokenAsync null path).
        await Assert.ThrowsAnyAsync<InvalidOperationException>(
            () => boot.Identity.VerifyAndSetPasswordAsync(token.Token, "AnotherPassword!"));
    }

    // ── Harness ───────────────────────────────────────────────────────────

    private sealed record Boot
    {
        public required IdentityService Identity { get; init; }
        public required IDocumentStore Store { get; init; }
        public required UserManager<User> UserManager { get; init; }
        public required IUserInfoService UserInfo { get; init; }
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
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
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

        var identity = new IdentityService(
            userManager,
            store,
            userInfo,
            NoClaimsSource.Instance,
            NoMail.Instance,
            Options.Create(new VerificationOptions()),
            NullLogger<IdentityService>.Instance,
            null,                 // ITranslationProvider — the fallback literals are fine
            null);                // NotificationService — not under test here

        return new Boot
        {
            Identity = identity,
            Store = store,
            UserManager = userManager,
            UserInfo = userInfo,
            DbOptions = dbOptions
        };
    }

    private static async Task<IdentityToken?> ReadLatestVerifyTokenAsync(
        IDocumentStore store, string subjectId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.QuerySession();
        var token = await Marten.QueryableExtensions
            .FirstOrDefaultAsync(
                session.Query<IdentityToken>()
                    .Where(t => t.Kind == IdentityToken.KindVerify && t.UserId == subjectId)
                    .OrderByDescending(t => t.CreatedAt),
                ct);
        return token;
    }

    // ── Test doubles (the minimal seams the IdentityService takes; the
    //    Identity shared framework ships no public Null* test doubles in .NET
    //    10, and the mailer's durable-envelope path needs a started Wolverine
    //    host — neither is under test here, so minimal substitutes are honest) ──

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
        public object? GetService(Type serviceType) => null;
    }
}
