using System.Text.Json;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Media;
using Kumunita.Core.Portability;
using Kumunita.Core.Posts;
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
/// C3-03 (ADR 0293) — the <see cref="ImportOwnershipGuardService"/> seam's own
/// tests. The **SEC-lane H1 ownership-guard** derivation (the three guard
/// methods) was extracted out of <see cref="UserPortabilityService"/> as a real
/// part-vs-whole surface (the <see cref="ImportOwnershipGuardService"/> class +
/// <see cref="IImportOwnershipGuardService"/> interface — the C3-00
/// <c>EventRsvpGuardianService</c> pattern; the shared
/// <see cref="ClassifyPrepass"/> / <see cref="OwnershipGuardInputs"/> shapes are
/// top-level records both sides compile against). These tests construct the seam
/// **directly** (not through the <see cref="UserPortabilityService"/> forwarders
/// the existing <c>UserPortabilityClassifyTests</c> / <c>UserPortabilityResolveTests</c>
/// exercise) to prove the seam is a standalone surface that works on its own:
/// the same <see cref="PostgresFixture"/> (fresh scratch Postgres), the same
/// <c>UserManager</c> principal set, but the guard calls go through
/// <see cref="ImportOwnershipGuardService"/> (the named seam) rather than the
/// god-service it came out of.
/// <para>
/// This is the DoD's "each extracted seam has its own unit test" line for the
/// ownership-guard seam, and it satisfies the gate's seam-test Rule 2
/// (<see cref="IImportOwnershipGuardService"/> named by a test file).
/// </para>
/// </summary>
public sealed class ImportOwnershipGuardServiceSeamTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private sealed record Booted(IDocumentStore store, UserManager<User> userManager, ImportOwnershipGuardService guard);

    private async Task<Booted> BootAsync(CancellationToken ct)
    {
        var conn = await fixture.NewDatabaseAsync(ct);

        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            CoreDocTypes.Configure(opts);
            PostDocTypes.Configure(opts);
            EventDocTypes.Configure(opts);
            ProjectDocTypes.Configure(opts);
            MessagingDocTypes.Configure(opts);
            MediaDocTypes.Configure(opts);
            TagDocTypes.Configure(opts);
            PageDocTypes.Configure(opts);
            InventoryDocTypes.Configure(opts);
            DocumentDocTypes.Configure(opts);
            BookmarkDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(conn).Options);
        await db.Database.MigrateAsync(ct);

        var userStore = new UserStore<User, IdentityRole, AppDbContext, string,
            IdentityUserClaim<string>, IdentityUserRole<string>,
            IdentityUserLogin<string>, IdentityUserToken<string>,
            IdentityRoleClaim<string>>(db);
        var identityOpts = Options.Create(new IdentityOptions
        {
            User = { RequireUniqueEmail = true },
            Password = { RequiredLength = 8, RequireNonAlphanumeric = false },
        });
        var userManager = new UserManager<User>(
            userStore, identityOpts, new PasswordHasher<User>(),
            [new UserValidator<User>()], [new PasswordValidator<User>()],
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(),
            new EmptyServiceProvider(),
            NullLogger<UserManager<User>>.Instance);

        var guard = new ImportOwnershipGuardService(store, userManager);
        return new Booted(store, userManager, guard);
    }

    // ── 1 — the classify prepass: an empty target yields an empty prepass ──
    //
    // With no target rows and no Identity users, the <see
    // cref="ImportOwnershipGuardService"/> seam's classify prepass is well-formed:
    // every in-scope kind has an (empty) target-row set, the referenced doc
    // types have (empty) id sets, and the principal set is empty. This proves
    // the seam's own target-store read (<c>QueryAllRowsAsync</c> over the
    // in-scope + referenced doc types) runs standalone.

    [Fact]
    public async Task Seam_ClassifyTargetPrepass_EmptyTargetIsEmptyPrepass()
    {
        var boot = await BootAsync(TestContext.Current.CancellationToken);
        var data = new KumunitaArchiveData();
        var jsonOpts = new JsonSerializerOptions();

        var prepass = await boot.guard.ClassifyTargetPrepassAsync(data, jsonOpts, "u-seam-prepass", TestContext.Current.CancellationToken);

        // Every in-scope kind is present with an empty target-row set.
        foreach (var (type, _) in UserScopeInventory.Entries)
            Assert.Empty(prepass.TargetRowsByType[type]);
        // The principal set is empty (no Identity users in the scratch DB).
        Assert.Empty(prepass.PrincipalIds);
    }

    // ── 2 — the guard derivation over a prepass: empty inputs → empty guard ─
    //
    // Over an empty prepass + an empty archive, the ownership-guard inputs are
    // well-formed: every in-scope kind has an (empty) owner probe and the
    // fail-closed conversation-seat union is empty. This proves the seam's own
    // OwnershipGuardInputs derivation runs standalone (no god-service state).

    [Fact]
    public void Seam_DeriveOwnershipGuardInputs_EmptyInputsAreEmptyGuard()
    {
        var boot = SyncBoot();
        var data = new KumunitaArchiveData();
        var jsonOpts = new JsonSerializerOptions();
        var prepass = new ClassifyPrepass(
            TargetRowsByType: new Dictionary<string, IReadOnlyList<object>>(StringComparer.Ordinal),
            TypeSetById: new Dictionary<string, HashSet<string>>(StringComparer.Ordinal),
            PrincipalIds: new HashSet<string>(StringComparer.Ordinal),
            LanguageCodes: new HashSet<string>(StringComparer.Ordinal));

        var guard = boot.guard.DeriveOwnershipGuardInputs(prepass, data, jsonOpts, "u-seam-derive");

        foreach (var (type, _) in UserScopeInventory.Entries)
            Assert.True(guard.OwnerByIdByType.ContainsKey(type));
        Assert.Empty(guard.ActorConversationIds);
    }

    // ── 3 — the resolve guard: an empty target yields an empty guard ────────
    //
    // Over an empty target store + an empty archive, the resolve-path guard is
    // well-formed: every in-scope kind has an (empty) owner probe and the
    // fail-closed conversation-seat union is empty. This proves the seam's own
    // <see cref="ImportOwnershipGuardService.ResolveGuardInputsAsync"/> target
    // read runs standalone.

    [Fact]
    public async Task Seam_ResolveGuardInputs_EmptyTargetIsEmptyGuard()
    {
        var boot = await BootAsync(TestContext.Current.CancellationToken);
        var data = new KumunitaArchiveData();
        // The name→type map (the guard reads rows for every in-scope kind) —
        // the same shape UserPortabilityService.BuildNameToTypeMap builds, from
        // the public registry + the 4 non-M11 kinds (all public types).
        var nameToType = UserPortabilityService.BuildNameToTypeMap();

        var guard = await boot.guard.ResolveGuardInputsAsync(
            data, nameToType, "u-seam-resolve", TestContext.Current.CancellationToken);

        foreach (var (type, _) in UserScopeInventory.Entries)
            Assert.True(guard.OwnerByIdByType.ContainsKey(type));
        Assert.Empty(guard.ActorConversationIds);
    }

    // A sync boot for the pure (no-store) test — the seam is constructible with
    // the same store + userManager; the derivation over an empty prepass reads
    // no rows, so the scratch DB is unused here (the seam is constructed the
    // same way as the other two facts).
    private Booted SyncBoot()
    {
        var ct = TestContext.Current.CancellationToken;
        // Reuse the async boot on the sync path (a scratch DB is spun up; the
        // derivation itself reads nothing).
        return BootAsync(ct).GetAwaiter().GetResult();
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}