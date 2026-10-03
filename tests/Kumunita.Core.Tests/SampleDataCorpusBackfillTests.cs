using Kumunita.Core;
using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Bootstrap;
using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Media;
using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
using Kumunita.Core.Projects;
using Kumunita.Core.Tags;
using Kumunita.Core.UserInfo;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// ADR 0130 — the **warm-boot full-corpus backfill**
/// (<see cref="SampleDataSeeder.BackfillSampleCorpusAsync"/>) generalized from
/// the ADR 0060 event-translation lane to every sample surface (accounts, tags,
/// groups, announcements, posts, events, pages, goals, projects, to-dos, and
/// every translation / RSVP / membership child row).
/// <para>
/// Mirrors the <see cref="PortabilityRoundTripTests"/> boot shape (full
/// <c>mt</c> doc-type surface + <c>AppDbContext</c> + <c>UserManager</c> /
/// <c>RoleManager</c>) and adds the <see cref="UserInfoService"/> (needed by the
/// component-mandatory write lane). Pins the three invariants that make the lane
/// safe to run on every warm boot:
/// </para>
/// <list type="number">
/// <item><b>Create-if-missing</b> — a fully empty instance gains the pinned
///       sample entities (one per major content type) after the first backfill
///       run.</item>
/// <item><b>Never clobbers</b> — a planted post with a human-edited body is left
///       untouched by the backfill; other corpus entities are still created
///       (the ADR 0042 D1 invariant, carried verbatim from the ADR 0060 lane).</item>
/// <item><b>Idempotent</b> — a second run over the same database adds nothing;
///       the counts and the Ids are unchanged.</item>
/// </list>
/// <para>
/// <b>Prerequisite</b> — the four <c>Component</c> rows
/// (<c>safety</c> / <c>maintenance</c> / <c>social</c> / <c>governance</c>)
/// are seeded before each backfill call;
/// <see cref="UserInfoService.SetCommunityMandatoryAsync"/>
/// throws on a missing component.
/// </para>
/// </summary>
public sealed class SampleDataCorpusBackfillTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── Pinned natural keys (stable strings from the sample corpus) ──────────
    private const string TagSlug        = "cleanup";
    private const string AnnTitle       = "Welcome to the neighborhood board";
    private const string PostTitle      = "New recycling schedule from next month";
    private const string EventTitle     = "Community Cleanup Day";
    private const string PageSlug       = "my-corner";
    private const string GoalTitle      = "A safer, calmer street";
    private const string ProjectTitle   = "Path-lighting upgrade";
    private const string AdminEmail     = "admin@examplium.com";

    // ── Boot ──────────────────────────────────────────────────────────────────
    // The full doc-type surface (the same set Program.cs registers) + the
    // EF Core identity schema + the UserManager / RoleManager (the same
    // UserStore / RoleStore / password-policy shape as
    // PortabilityRoundTripTests.BootFullInstanceAsync) + the UserInfoService
    // (needed by the component-mandatory write lane in the backfill).
    private async Task<(
        IDocumentStore store,
        AppDbContext db,
        UserManager<User> userManager,
        RoleManager<IdentityRole> roleManager,
        UserInfoService userInfo)>
        BootAsync(CancellationToken ct)
    {
        var conn = await fixture.NewDatabaseAsync(ct);

        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
            M3DocTypes.Configure(opts);
            M4DocTypes.Configure(opts);
            M5DocTypes.Configure(opts);
            M6DocTypes.Configure(opts);
            M9DocTypes.Configure(opts);
            M16DocTypes.Configure(opts);
            MediaDocTypes.Configure(opts);
            TagDocTypes.Configure(opts);
            PageDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn).Options);
        await db.Database.MigrateAsync(ct);

        var userStore = new UserStore<User, IdentityRole, AppDbContext, string,
            IdentityUserClaim<string>, IdentityUserRole<string>,
            IdentityUserLogin<string>, IdentityUserToken<string>,
            IdentityRoleClaim<string>>(db);
        var roleStore = new RoleStore<IdentityRole, AppDbContext, string,
            IdentityUserRole<string>, IdentityRoleClaim<string>>(db);

        var identityOpts = Options.Create(new IdentityOptions
        {
            User = { RequireUniqueEmail = true },
            Password = { RequiredLength = 8, RequireNonAlphanumeric = false },
        });
        var userManager = new UserManager<User>(
            userStore, identityOpts,
            new PasswordHasher<User>(),
            new[] { new UserValidator<User>() },
            new[] { new PasswordValidator<User>() },
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new EmptyServiceProvider(),
            NullLogger<UserManager<User>>.Instance);
        var roleManager = new RoleManager<IdentityRole>(
            roleStore,
            new[] { new RoleValidator<IdentityRole>() },
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<IdentityRole>>.Instance);

        var userInfo = new UserInfoService(store);

        // Seed the four Component rows (prerequisite for SetCommunityMandatoryAsync).
        await using (var session = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            foreach (var (id, name) in new[] {
                ("safety", "Safety"), ("maintenance", "Maintenance"),
                ("social", "Social"), ("governance", "Governance") })
            {
                session.Store(new Component { Id = id, Name = name, Enabled = true });
            }
            await session.SaveChangesAsync(ct);
        }

        return (store, db, userManager, roleManager, userInfo);
    }

    // ── Run the backfill ─────────────────────────────────────────────────────
    private static async Task RunBackfill(
        AppDbContext db, IDocumentStore store,
        UserManager<User> userManager, RoleManager<IdentityRole> roleManager,
        UserInfoService userInfo, CancellationToken ct)
    {
        await SampleDataSeeder.BackfillSampleCorpusAsync(
            db, store, userManager, roleManager, userInfo,
            mailer: null, adminEmail: null,
            logger: NullLogger.Instance, ct: ct);
    }

    // ── Query helpers ────────────────────────────────────────────────────────
    private static async Task<int> Count<T>(IDocumentStore store, CancellationToken ct)
    {
        await using var q = store.OpenSession(new Marten.Services.SessionOptions());
        return await Marten.QueryableExtensions.CountAsync(q.Query<T>(), ct);
    }

    private static async Task<bool> Exists<T>(IDocumentStore store, System.Linq.Expressions.Expression<System.Func<T, bool>> filter, CancellationToken ct)
    {
        await using var q = store.OpenSession(new Marten.Services.SessionOptions());
        return await Marten.QueryableExtensions.AnyAsync(q.Query<T>(), filter, ct);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 1 — Create-if-missing: a fully empty instance gains the pinned entities
    // ═══════════════════════════════════════════════════════════════════════
    [Fact(DisplayName = "ADR 0130 create-if-missing: a fully empty instance gains the pinned sample entities after one backfill")]
    public async Task Backfill_CreatesMissingCorpusOnEmptyDb()
    {
        var (store, db, userManager, roleManager, userInfo) =
            await BootAsync(TestContext.Current.CancellationToken);
        var ct = TestContext.Current.CancellationToken;

        // Empty DB: no content entities before the backfill.
        Assert.Equal(0, await Count<Tag>(store, ct));
        Assert.Equal(0, await Count<Announcement>(store, ct));
        Assert.Equal(0, await Count<Post>(store, ct));
        Assert.Equal(0, await Count<Event>(store, ct));
        Assert.Equal(0, await Count<Page>(store, ct));
        Assert.Equal(0, await Count<ProjectGoal>(store, ct));
        Assert.Equal(0, await Count<Project>(store, ct));

        await RunBackfill(db, store, userManager, roleManager, userInfo, ct);

        // Every pinned entity type is present after the backfill.
        Assert.True(await Count<Tag>(store, ct) > 0, "no tags created");
        Assert.True(await Count<Announcement>(store, ct) > 0, "no announcements created");
        Assert.True(await Count<Post>(store, ct) > 0, "no posts created");
        Assert.True(await Count<Event>(store, ct) > 0, "no events created");
        Assert.True(await Count<Page>(store, ct) > 0, "no pages created");
        Assert.True(await Count<ProjectGoal>(store, ct) > 0, "no goals created");
        Assert.True(await Count<Project>(store, ct) > 0, "no projects created");
        Assert.True(await Count<TodoItem>(store, ct) > 0, "no todos created");

        // Spot-check the pinned natural keys exist (not just some other entity).
        Assert.True(await Exists<Tag>(store, t => t.Slug == TagSlug, ct),
            $"tag '{TagSlug}' not found");
        Assert.True(await Exists<Announcement>(store, a => a.Title == AnnTitle, ct),
            $"announcement '{AnnTitle}' not found");
        Assert.True(await Exists<Post>(store, p => p.Title == PostTitle, ct),
            $"post '{PostTitle}' not found");
        Assert.True(await Exists<Event>(store, e => e.Title == EventTitle, ct),
            $"event '{EventTitle}' not found");
        Assert.True(await Exists<Page>(store, p => p.Slug == PageSlug, ct),
            $"page '{PageSlug}' not found");
        Assert.True(await Exists<ProjectGoal>(store, g => g.Title == GoalTitle, ct),
            $"goal '{GoalTitle}' not found");
        Assert.True(await Exists<Project>(store, p => p.Title == ProjectTitle, ct),
            $"project '{ProjectTitle}' not found");

        // The admin account was created on the identity side.
        var admin = await userManager.FindByEmailAsync(AdminEmail);
        Assert.NotNull(admin);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2 — Never clobbers a human edit
    // ═══════════════════════════════════════════════════════════════════════
    [Fact(DisplayName = "ADR 0130 never clobbers: a human-edited post body is preserved; other corpus entities are still created")]
    public async Task Backfill_NeverClobbersHumanEdits()
    {
        var (store, db, userManager, roleManager, userInfo) =
            await BootAsync(TestContext.Current.CancellationToken);
        var ct = TestContext.Current.CancellationToken;

        // Plant the sample post with a human-edited body (a resident tweaked
        // the wording after first boot — the ADR 0042 D1 invariant says the
        // backfill must not overwrite it).
        var humanBody = "I tweaked this post after the first boot.";
        await using (var s = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            s.Store(new Post
            {
                Id = "post-clobber-test",
                ComponentId = "safety",
                AuthorId = "author-0",
                Title = PostTitle,
                Body = humanBody,
                Audience = new Authorization.Audience(Authorization.AudienceMode.Any, Array.Empty<Authorization.AudienceGrant>()) { AllResidents = true },
                Created = DateTimeOffset.UtcNow,
                LanguageCode = "en",
                TagIds = [],
            });
            await s.SaveChangesAsync(ct);
        }

        await RunBackfill(db, store, userManager, roleManager, userInfo, ct);

        // The human edit is preserved verbatim — the backfill skipped this
        // post (matched by title + lane) and did not overwrite the body.
        await using var q = store.OpenSession(new Marten.Services.SessionOptions());
        var post = (await Marten.QueryableExtensions.ToListAsync(q.Query<Post>().Where(p => p.Title == PostTitle), ct)).Single();
        Assert.Equal("post-clobber-test", post.Id);
        Assert.Equal(humanBody, post.Body);

        // Other corpus entities are still created (the backfill is not
        // all-or-nothing; it adds what is missing, skips what is present).
        Assert.True(await Count<Event>(store, ct) > 0, "no events created");
        Assert.True(await Count<Announcement>(store, ct) > 0, "no announcements created");
        Assert.True(await Count<Page>(store, ct) > 0, "no pages created");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2b — Never clobbers the admin's display name on a reboot (ADR 0042 D1)
    // ═══════════════════════════════════════════════════════════════════════
    [Fact(DisplayName = "ADR 0130 never clobbers: the admin's in-app display name survives a warm-boot backfill")]
    public async Task Backfill_NeverClobbersAdminDisplayName()
    {
        var (store, db, userManager, roleManager, userInfo) =
            await BootAsync(TestContext.Current.CancellationToken);
        var ct = TestContext.Current.CancellationToken;

        // First boot: the sample admin comes up named "Alex Admin" from the
        // embedded sample-data document.
        await RunBackfill(db, store, userManager, roleManager, userInfo, ct);

        var admin = await userManager.FindByEmailAsync(AdminEmail);
        Assert.NotNull(admin);

        // The admin renames themselves in-app (the M2 profile editor). This is
        // the exact scenario the ADR 0042 D1 "create-if-missing, never clobber"
        // invariant protects: a reboot must not reset the name to the sample.
        const string residentChosenName = "Per M.";
        await using (var s = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            var updated = await s.LoadAsync<Profile>(admin!.Id, ct)
                ?? throw new InvalidOperationException("seed admin profile is missing");
            updated.DisplayName = residentChosenName;
            s.Store(updated);
            await s.SaveChangesAsync(ct);
        }

        // A reboot (a warm-boot backfill over the same database) runs.
        await RunBackfill(db, store, userManager, roleManager, userInfo, ct);

        // The resident's chosen name survives — the backfill did not reset it to
        // the sample name. (Before the fix, EnsureUserAsync unconditionally
        // re-applied the document's name, clobbering the edit on every reboot.)
        await using var q = store.OpenSession(new Marten.Services.SessionOptions());
        var profile = await q.LoadAsync<Profile>(admin!.Id, ct);
        Assert.NotNull(profile);
        Assert.Equal(residentChosenName, profile!.DisplayName);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3 — Idempotent: a second run adds nothing
    // ═══════════════════════════════════════════════════════════════════════
    [Fact(DisplayName = "ADR 0130 idempotent: a second backfill run adds no new rows and changes no Ids")]
    public async Task Backfill_IsIdempotent()
    {
        var (store, db, userManager, roleManager, userInfo) =
            await BootAsync(TestContext.Current.CancellationToken);
        var ct = TestContext.Current.CancellationToken;

        await RunBackfill(db, store, userManager, roleManager, userInfo, ct);

        var tagsAfter1   = await Count<Tag>(store, ct);
        var annsAfter1   = await Count<Announcement>(store, ct);
        var postsAfter1  = await Count<Post>(store, ct);
        var eventsAfter1 = await Count<Event>(store, ct);
        var pagesAfter1  = await Count<Page>(store, ct);
        var goalsAfter1  = await Count<ProjectGoal>(store, ct);
        var projectsAft1 = await Count<Project>(store, ct);
        var todosAfter1  = await Count<TodoItem>(store, ct);

        // A second warm boot over the same database.
        await RunBackfill(db, store, userManager, roleManager, userInfo, ct);

        var tagsAfter2   = await Count<Tag>(store, ct);
        var annsAfter2   = await Count<Announcement>(store, ct);
        var postsAfter2  = await Count<Post>(store, ct);
        var eventsAfter2 = await Count<Event>(store, ct);
        var pagesAfter2  = await Count<Page>(store, ct);
        var goalsAfter2  = await Count<ProjectGoal>(store, ct);
        var projectsAft2 = await Count<Project>(store, ct);
        var todosAfter2  = await Count<TodoItem>(store, ct);

        Assert.Equal(tagsAfter1,   tagsAfter2);
        Assert.Equal(annsAfter1,   annsAfter2);
        Assert.Equal(postsAfter1,  postsAfter2);
        Assert.Equal(eventsAfter1, eventsAfter2);
        Assert.Equal(pagesAfter1,  pagesAfter2);
        Assert.Equal(goalsAfter1,  goalsAfter2);
        Assert.Equal(projectsAft1, projectsAft2);
        Assert.Equal(todosAfter1,  todosAfter2);
    }

    // ── Private helper (the same shape as PortabilityRoundTripTests) ─────────
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
