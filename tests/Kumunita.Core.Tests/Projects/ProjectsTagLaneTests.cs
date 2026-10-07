using System.Collections.Generic;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Projects;
using Kumunita.Core.Tags;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests.Projects;

/// <summary>
/// The ADR 0147 "tags on to-dos and boards" lane — the Core-level write-path
/// tests. Mirrors the <see cref="DocumentsOrganizationTests"/> harness shape
/// (fresh scratch Postgres per test, the <c>*DocTypes.Configure</c> cluster,
/// the <see cref="PostgresFixture"/> shared <c>postgres:18</c> container)
/// with the <c>ProjectService</c> write lanes driving the
/// <see cref="TagService"/> attach lanes for the two new target kinds.
/// <para>
/// The lanes under test:
/// </para>
/// <list type="number">
///   <item><b>Tags on a to-do</b> — <see cref="ProjectService.CreateTodoAsync"/>
///   / <see cref="ProjectService.UpdateTodoAsync"/> resolve the author's
///   typed <c>TagSlugs</c> to <c>Tag</c> ids via
///   <see cref="ITagService.AttachToTodoAsync"/> (the
///   <see cref="DocumentService"/> tag-attach precedent).</item>
///   <item><b>Tags on a board</b> — <see cref="ProjectService.CreateBoardAsync"/>
///   / <see cref="ProjectService.UpdateBoardAsync"/> resolve the author's
///   typed <c>TagSlugs</c> to <c>Tag</c> ids via
///   <see cref="ITagService.AttachToBoardAsync"/>.</item>
///   <item><b>Standing</b> — the attach lanes re-check the target's own edit
///   standing (todo: creator ∪ assignee ∪ GlobalAdmin; board: creator ∪
///   GlobalAdmin), refusing a non-standing actor at the Core level.</item>
/// </list>
/// </summary>
public class ProjectsTagLaneTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly IReadOnlySet<string> MemberRoles =
        new HashSet<string> { Roles.Member };
    private static readonly IReadOnlySet<string> GlobalAdminRoles =
        new HashSet<string> { Roles.GlobalAdmin };

    // ── 1 — to-do: CreateTodoAsync resolves TagSlugs → TagIds ─────────────

    [Fact(DisplayName = "CreateTodoAsync resolves typed tag slugs to TagIds (the TG attach lane)")]
    public async Task CreateTodo_ResolvesTagSlugs_ToTagIds()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        await SeedDefaultLanguage(store, "en");

        var (_, _, projects, tags) = Services(store);
        const string author = "u-ptag-todo-author";

        var todo = await projects.CreateTodoAsync(
            author, MemberRoles,
            new CreateTodoRequest
            {
                Title = "Sanitation round",
                Audience = null, // public
                TagSlugs = new[] { "sanitation", "budget" },
            }, ct);

        Assert.True(todo.TagIds.Count == 2);
        Assert.Contains(todo.TagIds, id => !string.IsNullOrEmpty(id));

        // The two typed slugs resolved to two Tag docs (the C-TG·4
        // create-or-reuse lane: both slugs are new, so both are created).
        await using (var q = store.QuerySession())
        {
            var sanitation = await q.Query<Tag>().FirstOrDefaultAsync(t => t.Slug == "sanitation");
            var budget = await q.Query<Tag>().FirstOrDefaultAsync(t => t.Slug == "budget");
            Assert.NotNull(sanitation);
            Assert.NotNull(budget);
            Assert.Equal(author, sanitation!.CreatedBy);
            Assert.Equal(author, budget.CreatedBy);
        }

        // The audit rows: one tag.attach (TargetKind "todo") + two tag.create.
        await using (var q = store.QuerySession())
        {
            var attachRows = await q.Query<AccessAudit>()
                .Where(a => a.Action == "tag.attach" && a.TargetKind == "todo")
                .ToListAsync(ct);
            Assert.Single(attachRows);
            Assert.Equal(author, attachRows[0].ActorId);

            var createRows = await q.Query<AccessAudit>()
                .Where(a => a.Action == "tag.create" && a.TargetKind == "tag")
                .ToListAsync(ct);
            Assert.True(createRows.Count == 2);
        }
    }

    // ── 2 — to-do: UpdateTodoAsync empty slugs detaches all ───────────────

    [Fact(DisplayName = "UpdateTodoAsync with an empty slug list detaches all (the U8b detach semantics)")]
    public async Task UpdateTodo_EmptySlugList_DetachesAll()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        await SeedDefaultLanguage(store, "en");

        var (_, _, projects, tags) = Services(store);
        const string author = "u-ptag-todo-detach";

        var seeded = await projects.CreateTodoAsync(
            author, MemberRoles,
            new CreateTodoRequest
            {
                Title = "T",
                Audience = null,
                TagSlugs = new[] { "alpha", "beta" },
            }, ct);
        Assert.True(seeded.TagIds.Count == 2);

        var updated = await projects.UpdateTodoAsync(
            seeded.Id, author, MemberRoles,
            new UpdateTodoRequest
            {
                Title = "T (updated)",
                TagSlugs = Array.Empty<string>(),
            }, ct);
        Assert.Empty(updated.TagIds);
    }

    // ── 3 — to-do: UpdateTodoAsync null slugs leaves existing ─────────────

    [Fact(DisplayName = "UpdateTodoAsync with null slugs leaves the to-do's existing tags (the 'leave existing' shape)")]
    public async Task UpdateTodo_NullSlugs_LeavesExisting()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        await SeedDefaultLanguage(store, "en");

        var (_, _, projects, tags) = Services(store);
        const string author = "u-ptag-todo-leave";

        var seeded = await projects.CreateTodoAsync(
            author, MemberRoles,
            new CreateTodoRequest
            {
                Title = "T",
                Audience = null,
                TagSlugs = new[] { "alpha" },
            }, ct);
        var existingTagIds = seeded.TagIds;
        Assert.Single(existingTagIds);

        var updated = await projects.UpdateTodoAsync(
            seeded.Id, author, MemberRoles,
            new UpdateTodoRequest
            {
                Title = "T (updated)",
                TagSlugs = null,
            }, ct);
        Assert.Equal(existingTagIds, updated.TagIds);
    }

    // ── 4 — board: CreateBoardAsync resolves TagSlugs → TagIds ────────────

    [Fact(DisplayName = "CreateBoardAsync resolves typed tag slugs to TagIds (the TG attach lane)")]
    public async Task CreateBoard_ResolvesTagSlugs_ToTagIds()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        await SeedDefaultLanguage(store, "en");

        var (_, _, projects, tags) = Services(store);
        const string author = "u-ptag-board-author";

        var board = await projects.CreateBoardAsync(
            author, MemberRoles,
            new CreateBoardRequest
            {
                Title = "Cleanup board",
                Lanes = new[] { new CreateLaneRequest { Title = "Planned" } },
                TagSlugs = new[] { "sanitation", "garden" },
            }, ct);

        Assert.True(board.TagIds.Count == 2);
        Assert.Contains(board.TagIds, id => !string.IsNullOrEmpty(id));

        await using (var q = store.QuerySession())
        {
            var sanitation = await q.Query<Tag>().FirstOrDefaultAsync(t => t.Slug == "sanitation");
            var garden = await q.Query<Tag>().FirstOrDefaultAsync(t => t.Slug == "garden");
            Assert.NotNull(sanitation);
            Assert.NotNull(garden);
            Assert.Equal(author, sanitation!.CreatedBy);
        }

        // The audit rows: one tag.attach (TargetKind "board") + two tag.create.
        await using (var q = store.QuerySession())
        {
            var attachRows = await q.Query<AccessAudit>()
                .Where(a => a.Action == "tag.attach" && a.TargetKind == "board")
                .ToListAsync(ct);
            Assert.Single(attachRows);
            Assert.Equal(author, attachRows[0].ActorId);

            var createRows = await q.Query<AccessAudit>()
                .Where(a => a.Action == "tag.create" && a.TargetKind == "tag")
                .ToListAsync(ct);
            Assert.True(createRows.Count == 2);
        }
    }

    // ── 5 — board: UpdateBoardAsync empty slugs detaches all ──────────────

    [Fact(DisplayName = "UpdateBoardAsync with an empty slug list detaches all (the U8b detach semantics)")]
    public async Task UpdateBoard_EmptySlugList_DetachesAll()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        await SeedDefaultLanguage(store, "en");

        var (_, _, projects, tags) = Services(store);
        const string author = "u-ptag-board-detach";

        var seeded = await projects.CreateBoardAsync(
            author, MemberRoles,
            new CreateBoardRequest
            {
                Title = "B",
                Lanes = new[] { new CreateLaneRequest { Title = "Planned" } },
                TagSlugs = new[] { "alpha", "beta" },
            }, ct);
        Assert.True(seeded.TagIds.Count == 2);

        var updated = await projects.UpdateBoardAsync(
            seeded.Id, author, MemberRoles,
            new UpdateBoardRequest
            {
                Title = "B (updated)",
                TagSlugs = Array.Empty<string>(),
            }, ct);
        Assert.Empty(updated.TagIds);
    }

    // ── 6 — board: UpdateBoardAsync null slugs leaves existing ────────────

    [Fact(DisplayName = "UpdateBoardAsync with null slugs leaves the board's existing tags (the 'leave existing' shape)")]
    public async Task UpdateBoard_NullSlugs_LeavesExisting()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        await SeedDefaultLanguage(store, "en");

        var (_, _, projects, tags) = Services(store);
        const string author = "u-ptag-board-leave";

        var seeded = await projects.CreateBoardAsync(
            author, MemberRoles,
            new CreateBoardRequest
            {
                Title = "B",
                Lanes = new[] { new CreateLaneRequest { Title = "Planned" } },
                TagSlugs = new[] { "alpha" },
            }, ct);
        var existingTagIds = seeded.TagIds;
        Assert.Single(existingTagIds);

        var updated = await projects.UpdateBoardAsync(
            seeded.Id, author, MemberRoles,
            new UpdateBoardRequest
            {
                Title = "B (updated)",
                TagSlugs = null,
            }, ct);
        Assert.Equal(existingTagIds, updated.TagIds);
    }

    // ── 7 — standing: AttachToBoardAsync refuses a non-creator non-admin ─

    [Fact(DisplayName = "AttachToBoardAsync refuses a non-creator non-GlobalAdmin (the standing gate)")]
    public async Task Board_Attach_NonStanding_Throws()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        await SeedDefaultLanguage(store, "en");

        var (_, _, projects, tags) = Services(store);
        const string creator = "u-ptag-board-creator";
        const string other   = "u-ptag-board-other";

        var board = await projects.CreateBoardAsync(
            creator, MemberRoles,
            new CreateBoardRequest
            {
                Title = "B",
                Lanes = new[] { new CreateLaneRequest { Title = "Planned" } },
                TagSlugs = Array.Empty<string>(),
            }, ct);

        // A non-creator (a plain Member, not a GlobalAdmin) is refused at the
        // Core level (a hard gate — the board's edit standing: creator ∪
        // GlobalAdmin).
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => RunInSession(store, s => tags.AttachToBoardAsync(
                board.Id, new[] { "intruder" }, other, MemberRoles, s)));

        // No Tag was created (the lane threw before anything was stored).
        await using (var q = store.QuerySession())
        Assert.Equal(0, await q.Query<Tag>().CountAsync(t => t.Slug == "intruder", ct));
    }

    // ── 8 — standing: AttachToBoardAsync allows a GlobalAdmin ─────────────

    [Fact(DisplayName = "AttachToBoardAsync allows a GlobalAdmin (the elevated standing)")]
    public async Task Board_Attach_GlobalAdmin_Allowed()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        await SeedDefaultLanguage(store, "en");

        var (_, _, projects, tags) = Services(store);
        const string creator = "u-ptag-board-admin-creator";
        const string admin   = "u-ptag-board-admin";

        var board = await projects.CreateBoardAsync(
            creator, MemberRoles,
            new CreateBoardRequest
            {
                Title = "B",
                Lanes = new[] { new CreateLaneRequest { Title = "Planned" } },
                TagSlugs = Array.Empty<string>(),
            }, ct);

        // A GlobalAdmin (a non-creator elevated standing) is allowed.
        var resolved = await RunInSession(store, s => tags.AttachToBoardAsync(
            board.Id, new[] { "admin-tag" }, admin, GlobalAdminRoles, s));
        Assert.Single(resolved);
        Assert.Equal("admin-tag", resolved[0].Slug);
    }

    // ── 9 — standing: AttachToTodoAsync refuses a non-standing actor ──────

    [Fact(DisplayName = "AttachToTodoAsync refuses a non-creator non-assignee (the standing gate)")]
    public async Task Todo_Attach_NonStanding_Throws()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        await SeedDefaultLanguage(store, "en");

        var (_, _, projects, tags) = Services(store);
        const string author = "u-ptag-todo-creator";
        const string other  = "u-ptag-todo-other";

        var todo = await projects.CreateTodoAsync(
            author, MemberRoles,
            new CreateTodoRequest
            {
                Title = "T",
                Audience = null,
                TagSlugs = Array.Empty<string>(),
            }, ct);

        // A non-creator, non-assignee (a plain Member, not a GlobalAdmin) is
        // refused at the Core level (a hard gate — the to-do's edit standing:
        // creator ∪ assignee ∪ GlobalAdmin).
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => RunInSession(store, s => tags.AttachToTodoAsync(
                todo.Id, new[] { "intruder" }, other, MemberRoles, s)));

        // No Tag was created (the lane threw before anything was stored).
        await using (var q = store.QuerySession())
        Assert.Equal(0, await q.Query<Tag>().CountAsync(t => t.Slug == "intruder", ct));
    }

    // ── Shared helpers (the DocumentsOrganizationTests shape, re-pointed) ──

    private async Task<IDocumentStore> BootStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
            M3DocTypes.Configure(opts);
            M4DocTypes.Configure(opts);
            M5DocTypes.Configure(opts);        // the Projects surfaces (todo + board)
            TagDocTypes.Configure(opts);       // the Tag + TagTranslation surfaces (the TG attach lane)
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    private static (UserInfoService User, AuthorizationService Authz, ProjectService Projects, TagService Tags)
        Services(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz    = new AuthorizationService(store, userInfo);
        var tags     = new TagService(store, authz, new TranslationProvider(store));
        // The ProjectService's tag seam is the last ctor param (the
        // PostService._tags optional-ctor-param idiom) — pass the live
        // TagService so the write lanes drive the attach lane (the DI
        // registration shape, mirrored directly against the scratch store).
        var projects = new ProjectService(store, authz, userInfo, null, null, null, tags);
        return (userInfo, authz, projects, tags);
    }

    private static async Task SeedDefaultLanguage(IDocumentStore store, string code)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.OpenSession(new Marten.Services.SessionOptions());
        s.Store(new LanguageCatalog
        {
            Id = code,
            NativeName = code,
            Enabled = true,
            SortOrder = 0,
        });
        s.Store(new LocaleSettings
        {
            Id = LocaleSettings.SingletonId,
            DefaultLanguageCode = code,
        });
        await s.SaveChangesAsync(ct);
    }

    private static async Task RunInSession(IDocumentStore store, Func<IDocumentSession, Task> action)
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        await action(session);
    }

    private static async Task<T> RunInSession<T>(IDocumentStore store, Func<IDocumentSession, Task<T>> action)
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        return await action(session);
    }
}
