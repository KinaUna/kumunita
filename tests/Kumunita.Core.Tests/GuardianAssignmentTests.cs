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
/// GA (ADR 0038 — guardian assignment: an existing guardian assigns a second
/// guardian to a child's account) — the lane's <b>3 pinned Core seam tests</b> for
/// the <see cref="IIdentityService.FindSubjectByEmailAsync"/> ADD (the email →
/// subject id resolution; invariant G-A·2's no-leak shape: an unknown email
/// returns <c>null</c>, never an exception that names the email). These are the
/// <c>Pinned contract → Pinned seam tests (exact names)</c> frozen in
/// <c>docs/design/guardian-assignment-design.md</c> (U01), asserting U02's seam +
/// impl.
/// <para>
/// The account is seeded through the <b>real M1 lifecycle</b> —
/// <see cref="IIdentityService.RegisterAsync"/> +
/// <see cref="IIdentityService.VerifyWithTokenAsync"/> — driven by the real EF
/// <see cref="UserStore{TUser,TRole,TContext,TKey,TUserClaim,TUserRole,TUserLogin,TUserToken,TRoleClaim}"/>
/// over a migrated <c>identity</c> schema (the same <c>identity</c>-schema store
/// production uses) and the real <see cref="PasswordHasher{TUser}"/> /
/// <see cref="EmailAddressNormalizer"/>. A no-op <see cref="IMailerStage"/> stands
/// in for the outbox stage (the durable email lane is not under test here); the
/// verification token is read back from the seeded <see cref="IdentityToken"/> row
/// and consumed exactly as the Web's <c>/account/verify</c> handoff would. Each
/// test hands itself a fresh scratch Postgres DB (the
/// <see cref="GuardianControlsTests"/> harness shape — the
/// <see cref="PostgresFixture"/> plus the <c>BootStoreAsync</c> boot the GU lane
/// established in this assembly).
/// <para>
/// These are Core tests: no <c>Kumunita.Web</c> type anywhere; the 5 Web
/// controller tests are U06's, and the acceptance gate is U07's. A test whose
/// exact name is not in the design doc's pinned list is a drift pause, not a
/// silent add.
/// </para>
/// </summary>
public class GuardianAssignmentTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── The known-email resolution — the seam's happy path ───────────────────
    // Seed a verified account through the M1 lifecycle (RegisterAsync +
    // VerifyWithTokenAsync); FindSubjectByEmailAsync resolves the typed email to
    // the account's subject id (the same value RegisterAsync's principal carried).

    [Fact]
    public async Task FindSubjectByEmail_ReturnsSubjectIdForKnownEmail()
    {
        var (identity, _, store) = await BootIdentityAsync();

        const string email = "ga-u03-known@example.com";
        var principal = await identity.RegisterAsync("GA U03 Known", email, "Passw0rd!u03known");
        await VerifySeededAccountAsync(identity, store);

        Assert.Equal(principal.SubjectId, await identity.FindSubjectByEmailAsync(email));
    }

    // ── G-A·2 — no-leak shape: an unknown email returns null, not an exception ─
    // The Web's Assign action turns the null into the form's "No account with
    // that email." surface; a 500/exception naming the email would leak account
    // existence the way the ADR 0008 "a non-guardian learns nothing" shape forbids.

    [Fact]
    public async Task FindSubjectByEmail_ReturnsNullForUnknownEmail()
    {
        var (identity, _, _) = await BootIdentityAsync();

        Assert.Null(await identity.FindSubjectByEmailAsync("no-account@example.com"));
    }

    // ── The ASP.NET Identity FindByEmailAsync case-insensitive precedent ───────
    // Seed with a mixed-case email; the lowercase lookup still resolves to the
    // same subject id (the store's NormalizedEmail column — set by the real
    // UpperInvariantLookupNormalizer on Create — is what FindByEmailAsync compares).

    [Fact]
    public async Task FindSubjectByEmail_IsCaseInsensitiveOnEmail()
    {
        var (identity, _, store) = await BootIdentityAsync();

        const string email = "User@Example.com";
        var principal = await identity.RegisterAsync("GA U03 Case", email, "Passw0rd!u03case");
        await VerifySeededAccountAsync(identity, store);

        Assert.Equal(principal.SubjectId, await identity.FindSubjectByEmailAsync("user@example.com"));
    }

    // ── Shared harness ────────────────────────────────────────────────────────

    /// <summary>
    /// Boots the two-store shape the seam reads from: the <c>mt</c> document
    /// schema (the <see cref="GuardianControlsTests"/> boot, byte-identical)
    /// plus the <c>identity</c> schema (EF Core migrations applied — the same
    /// call <c>SchemaBootstrap.ApplyAsync</c> makes at boot), and hands back the
    /// real <see cref="IdentityService"/> wired with the real EF
    /// <see cref="UserStore{TUser,TRole,TContext,TKey,TUserClaim,TUserRole,TUserLogin,TUserToken,TRoleClaim}"/>
    /// (the <c>identity</c>-schema store production uses), the real
    /// <see cref="PasswordHasher{TUser}"/> + <see cref="EmailKeyNormalizer"/>,
    /// and a no-op <see cref="IMailerStage"/> (the outbox stage
    /// <c>RegisterAsync</c> touches; the seam under test never reads one).
    /// </summary>
    private async Task<(IdentityService identity, UserInfoService userInfo, IDocumentStore store)> BootIdentityAsync()
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
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn)
            .Options);
        await db.Database.MigrateAsync(ct);

        // The real EF identity store over the migrated `identity` schema — the same
        // UserStore<AppUser, IdentityRole, AppDbContext, …> the Web host's
        // AddEntityFrameworkStores<AppDbContext>() registers in production.
        var userStore = new UserStore<User, IdentityRole, AppDbContext, string,
            IdentityUserClaim<string>, IdentityUserRole<string>,
            IdentityUserLogin<string>, IdentityUserToken<string>,
            IdentityRoleClaim<string>>(db);

        var options = new IdentityOptions { User = { RequireUniqueEmail = true } };
        var userManager = new UserManager<User>(
            userStore,
            Options.Create(options),
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
            NullLogger<IdentityService>.Instance);

        return (identity, userInfo, store);
    }

    /// <summary>
    /// Completes the M1 verification handoff exactly as the Web's
    /// <c>/account/verify</c> action would: <c>RegisterAsync</c> staged the
    /// single-use token (row Id = the URL's <c>?id=</c> value; the high-entropy
    /// secret is what <c>VerifyWithTokenAsync</c> compares). Each test owns a
    /// fresh scratch DB, so the single <see cref="IdentityToken"/> KindVerify
    /// row IS the just-registered account's token — read it back and consume it.
    /// </summary>
    private static async Task VerifySeededAccountAsync(IIdentityService identity, IDocumentStore store)
    {
        await using var session = store.QuerySession();
        // Fully-qualified: both Marten and EF Core expose a FirstAsync(IQueryable<T>)
        // extension here; this is the Marten (mt-schema) query.
        var token = await Marten.QueryableExtensions
            .FirstAsync(
                session.Query<IdentityToken>()
                    .Where(t => t.Kind == IdentityToken.KindVerify)
                    .OrderByDescending(t => t.CreatedAt),
                TestContext.Current.CancellationToken);
        await identity.VerifyWithTokenAsync(token.Token);
    }

    // ── Test doubles (the two Web-side seams the IdentityService takes; the
    //    Identity shared framework ships no public Null* test doubles in .NET 10,
    //    and the mailer's durable-envelope path needs a started Wolverine host —
    //    neither is under test here, so minimal substitutes are honest) ──

    private sealed class NoClaimsSource : IClaimsSource
    {
        public static readonly NoClaimsSource Instance = new();
        public ClaimsPrincipal? Current => null;   // not request-driven in a Core test
    }

    private sealed class NoMail : IMailerStage
    {
        public static readonly NoMail Instance = new();
        public Task StageAsync(
            Marten.IDocumentSession session,
            string idempotencyKey,
            string recipient,
            string subject,
            string body,
            CancellationToken ct = default) => Task.CompletedTask;   // no outbox row — the seam under test never reads one
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static readonly EmptyServiceProvider Instance = new();
        public object? GetService(Type serviceType) => null;   // unregistered → null (the IServiceProvider contract); ProtectPersonalData is off, so no manager path resolves anything
    }
}
