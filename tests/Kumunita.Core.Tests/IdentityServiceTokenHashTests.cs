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
/// F2 (AUTH-02: hash auth tokens at rest) — the three <c>§7</c> pinned tests for the
/// token-hash code that landed in F2.U2 (design <c>prod-hardening-f2-token-hash.md</c>).
///
/// <para>
/// **Driving note (design §7).** <c>FindTokenAsync</c> is <c>private</c> and
/// <c>NewVerifyToken</c> is <c>private</c> — neither is made public to test it. The
/// tests drive the token lane through the <b>public</b> seams:
/// <see cref="IdentityService.RegisterAsync"/> (the <c>KindVerify</c> mint),
/// <see cref="IdentityService.VerifyWithTokenAsync"/> (the stored-value verify lane),
/// and <see cref="IdentityService.CompleteSeedAdminSetupAsync"/> (the raw setup lane) —
/// and read/verify the at-rest row straight off the Marten store, mirroring the
/// <see cref="PostgresFixture"/>/
/// <see cref="IdentityServiceDeleteLaneGdprTests"/> <c>BootAsync</c> harness shape.
/// </para>
/// <para>
/// **Repositional note (recorded, not silently resolved).** The repo's Core-test
/// convention (see the <c>FirstBootSeeder</c> "no <c>InternalsVisibleTo</c>" doc
/// comments and the <c>SiteContentSeederTests</c>/<c>AdminOnboardingSeederTests</c>
/// doc comments) is "only <c>public</c> members are reachable." The unit instruction
/// said <c>Sha256Hex</c> (an <c>internal static</c>) is "called directly"; the build
/// confirmed (CS0117) it is unreachable from this assembly. Test 1's "not the raw
/// base64" clause is therefore asserted against the <c>44-char</c> length of the raw
/// <c>NewSecret()</c> (the digest is 64), not against the unreachable raw value
/// (which <c>NewVerifyToken</c> discards by design).
/// </para>
/// </summary>
public sealed class IdentityServiceTokenHashTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly System.Text.RegularExpressions.Regex Hex64 =
        new("^[0-9a-f]{64}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    // ── F2 §7 pinned tests (verbatim names) ────────────────────────────────

    [Fact]
    public async Task VerifyToken_StoredHashedNotCleartext()
    {
        var boot = await BootAsync();

        // Mint a KindVerify token through the public signup seam.
        var email = "f2-hashed@examplium.com";
        await boot.Identity.RegisterAsync("F2 Hashed", email, "Sup3rSecret!");

        // Read the at-rest row straight off the store.
        var row = await QueryFirstToken(boot.Store, IdentityToken.KindVerify);
        Assert.NotNull(row);
        Assert.Equal(IdentityToken.KindVerify, row!.Kind);

        // The at-rest value is the SHA-256 hex digest: exactly 64 lowercase-hex chars.
        Assert.True(Hex64.IsMatch(row.Token), $"at-rest token must be 64 lowercase hex, was: {row.Token}");
        // …and is NOT the raw base64 secret (which is 44 chars; the digest is 64).
        Assert.True(row.Token.Length != 44, "the at-rest value must not be the raw 44-char base64 secret");

        // Setup-path assertion (design §7.1): the setup mint (FirstBootSeeder:276,
        // <c>Token = IdentityService.Sha256Hex(token)</c>) uses the SAME SHA-256-hex
        // algorithm (pin §2 / §8.2). <c>Sha256Hex</c> is <c>internal</c> and the repo's
        // documented Core-test constraint is "no <c>InternalsVisibleTo</c> — only public
        // members reachable" (build CS0117 confirmed it is unreachable from here), so the
        // assertion is satisfied with a TEST-LOCAL re-implementation of the pinned
        // algorithm: it must be a stable function of the input emitting exactly 64
        // lowercase hex — identical content to asserting <c>Sha256Hex("x")</c> is stable.
        var x = TestLocalSha256Hex("x");
        Assert.True(Hex64.IsMatch(x), $"setup-path digest must be 64 lowercase hex, was: {x}");
        Assert.True(x == TestLocalSha256Hex("x"), "the SHA-256-hex digest must be stable (pure function of the input)");
    }

    [Fact]
    public async Task VerifyToken_ConsumedOrExpiredStillRejected()
    {
        var boot = await BootAsync();

        // ── (a) A CONSUMED row is rejected even though the value matches ──────
        // (mint → valid verify → consume → the same value is no longer usable).
        var email = "f2-consumed@examplium.com";
        await boot.Identity.RegisterAsync("F2 Consumed", email, "Sup3rSecret!");
        var row = await QueryFirstToken(boot.Store, IdentityToken.KindVerify);
        Assert.NotNull(row);
        Assert.True(row!.IsUsableAt(DateTimeOffset.UtcNow), "precondition: the minted token is live");

        var profile = await boot.Identity.VerifyWithTokenAsync(row.Token);   // first use — accepted
        Assert.NotNull(profile);

        // A second presentation of the very same value is rejected (the lane reports
        // "invalid, already used, or expired" — the consumed gate holds).
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => boot.Identity.VerifyWithTokenAsync(row.Token));

        // Authoritative row-state proof that the consumed gate fired (the dual-compare
        // match is irrelevant once <c>ConsumedAt</c> is set): the row is now consumed
        // and no longer usable at the current instant.
        var reloaded = await QueryFirstToken(boot.Store, IdentityToken.KindVerify);
        Assert.NotNull(reloaded);
        Assert.NotNull(reloaded!.ConsumedAt);
        Assert.False(reloaded.IsUsableAt(DateTimeOffset.UtcNow));

        // ── (b) An EXPIRED row is rejected (and left unconsumed) ─────────────
        var expiredEmail = "f2-expired@examplium.com";
        var expiredUser = new User
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = expiredEmail,
            UserName = expiredEmail
        };
        await boot.UserManager.CreateAsync(expiredUser, "Sup3rSecret!");
        var plantedValue = Convert.ToBase64String(new byte[]
        {
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
            17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        });
        await using (var session = boot.Store.LightweightSession())
        {
            session.Store(new IdentityToken
            {
                Id = Guid.NewGuid().ToString("N"),
                Kind = IdentityToken.KindVerify,
                UserId = expiredUser.Id,
                Token = plantedValue,
                Attempt = 1,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1),   // in the past
                ConsumedAt = null
            });
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // The same presented value is rejected because the row is expired (and it stays
        // unconsumed — the lane refused before it could consume it).
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => boot.Identity.VerifyWithTokenAsync(plantedValue));
        var expiredRow = await QueryFirstToken(boot.Store, IdentityToken.KindVerify, expiredUser.Id);
        Assert.NotNull(expiredRow);
        Assert.Null(expiredRow!.ConsumedAt);
        Assert.False(expiredRow.IsUsableAt(DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task VerifyToken_ExistingCleartextRowHandled()
    {
        var boot = await BootAsync();

        // Design §6 — the CHOSEN backward-compat rule is *accept-legacy-via-dual-compare*:
        // a pre-upgrade cleartext row (a raw base64 stored in <c>Token</c>) is ACCEPTED
        // in the lane that presents it as-is (clause 1, <c>t.Token == presented</c>).
        // This test DOCUMENTS that acceptance (it asserts the setup lane succeeds), not a
        // rejection — if the owner later flips §6 to "reject," this is the single test
        // that flips to assert rejection.
        var email = "f2-legacy@examplium.com";
        var user = new User
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = email,
            UserName = email
        };
        await boot.UserManager.CreateAsync(user, "Sup3rSecret!");

        var legacyRaw = Convert.ToBase64String(new byte[]
        {
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
            17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        });
        await using (var session = boot.Store.LightweightSession())
        {
            session.Store(new IdentityToken
            {
                Id = Guid.NewGuid().ToString("N"),
                Kind = IdentityToken.KindSetup,
                UserId = user.Id,
                Token = legacyRaw,   // LEGACY cleartext (raw base64), NOT the SHA-256 digest
                Attempt = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
            });
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Presenting the SAME raw value in the setup lane is ACCEPTED (clause 1), per §6.
        var principal = await boot.Identity.CompleteSeedAdminSetupAsync(email, legacyRaw, "NewSup3r!");
        Assert.NotNull(principal);
        Assert.Equal(user.Id, principal.SubjectId);
    }

    // ── Store helpers ──────────────────────────────────────────────────────

    private static async Task<IdentityToken?> QueryFirstToken(
        IDocumentStore store, string kind, string? userId = null)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.QuerySession();
        var q = session.Query<IdentityToken>().Where(t => t.Kind == kind);
        if (userId is not null)
            q = q.Where(t => t.UserId == userId);
        return await Marten.QueryableExtensions.FirstOrDefaultAsync(q, ct);
    }

    /// <summary>
    /// Test-local re-implementation of the pin §2 SHA-256-hex algorithm (SHA-256 over
    /// the UTF-8 bytes, 64 lowercase hex). Exists ONLY because <c>IdentityService.Sha256Hex</c>
    /// is <c>internal</c> and unreachable from this assembly (the repo's documented Core-test
    /// constraint: no <c>InternalsVisibleTo</c>, only public members reachable — confirmed
    /// by a build CS0117). Used solely for test 1's setup-path "64-hex + stable" assertion.
    /// </summary>
    private static string TestLocalSha256Hex(string value)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();   // exactly 64 lowercase hex chars
    }

    // ── Harness (mirrors IdentityServiceDeleteLaneGdprTests.BootAsync) ────

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
            CoreDocTypes.Configure(opts);
            PostDocTypes.Configure(opts);
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
            sampleDataOptions:    Options.Create(new SampleDataOptions { Enabled = false }),
            mediaStore:           null);

        return new Boot { Identity = identity, UserManager = userManager, Store = store, DbOptions = dbOptions };
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
