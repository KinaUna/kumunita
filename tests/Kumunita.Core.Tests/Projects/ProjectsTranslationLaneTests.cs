using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Projects;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests.Projects;

/// <summary>
/// ADR 0288 (C2-02, IMPROVE-CODE-2 DoD #5) — the **Projects family** collapsed
/// onto the shared <see cref="Translations.TranslationLane{TParent,TVar}"/>. The
/// three per-surface write forwarders (Add/Update/Remove *Project|Board|
/// Todo*TranslationAsync) retired onto the per-surface
/// <see cref="Translations.TranslationDescriptor{TParent,TVar}"/>; this is the
/// seam test for each migrated surface: **add** a translation, **the read
/// seam resolves** it, **remove** it — exercising the standing gate (the
/// parent's creator <see cref="Authorization.AccessVia.Owner"/> or a
/// <see cref="Roles.Translator"/>/<see cref="Roles.GlobalAdmin"/>
/// <see cref="Authorization.AccessVia.Admin"/>) + the hand-written
/// <c>AccessAudit</c> row + the C3 single-<c>SaveChangesAsync</c>. Same
/// <see cref="PostgresFixture"/> / <c>BootStoreAsync</c> / <c>Plant</c> /
/// <c>RunInSession</c> / <c>AuditsFor</c> harness as
/// <see cref="PostTranslationTests"/> (the C2-01 reference surface).
/// </summary>
public class ProjectsTranslationLaneTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static IReadOnlySet<string> RoleSet(params string[] roles) =>
        new HashSet<string>(roles);

    private async Task<IDocumentStore> BootStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            CoreDocTypes.Configure(opts);
            ProjectDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    private static (UserInfoService User, AuthorizationService Authz, ProjectService Projects, TodoService Todos, BoardService Boards)
        Services(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var projects = new ProjectService(store, authz, userInfo);
        var todos = new TodoService(store, authz, userInfo);
        var boards = new BoardService(store, authz, userInfo);
        return (userInfo, authz, projects, todos, boards);
    }

    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }

    private static async Task<T> RunInSession<T>(IDocumentStore store, Func<IDocumentSession, Task<T>> action)
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        return await action(session);
    }

    private static async Task RunInSession(IDocumentStore store, Func<IDocumentSession, Task> action)
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        await action(session);
    }

    private static async Task<IReadOnlyList<AccessAudit>> AuditsFor(IDocumentStore store, string action)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<AccessAudit>().Where(a => a.Action == action).ToListAsync(ct);
    }

    private static Project Project(string id, string author) => new()
    {
        Id = id,
        Title = "Project " + id,
        AuthorId = author,
        Created = DateTimeOffset.UtcNow,
    };

    private static TodoItem Todo(string id, string author, string? assignee = null) => new()
    {
        Id = id,
        Title = "Todo " + id,
        AuthorId = author,
        AssigneeId = assignee,
        Created = DateTimeOffset.UtcNow,
    };

    private static KanbanBoard Board(string id, string author) => new()
    {
        Id = id,
        Title = "Board " + id,
        AuthorId = author,
        Created = DateTimeOffset.UtcNow,
    };

    // ── Project surface: add → variant resolves → remove ─────────────────────

    [Fact]
    public async Task Project_AddThenResolvesThenRemove()
    {
        var store = await BootStoreAsync();
        var (_, _, projects, _, _) = Services(store);
        const string author = "u-a288-project-author";

        await Plant(store, Project("a288-proj", author));

        var created = await RunInSession(store, s =>
            projects.ProjectTranslationLane.AddAsync(s, "a288-proj", "pl", "Tyt", "ciało", author, RoleSet()));
        Assert.Equal("a288-proj", created.ProjectId);

        var rows = await projects.GetProjectTranslationsAsync("a288-proj");
        Assert.Single(rows);
        Assert.Equal("pl", rows[0].LanguageCode);

        var add = Assert.Single(await AuditsFor(store, "projecttranslation.add"));
        Assert.Equal(AccessVia.Owner, add.Via);
        Assert.Equal("project", add.TargetKind);
        Assert.Equal("a288-proj", add.TargetId);

        await RunInSession(store, s =>
            projects.ProjectTranslationLane.RemoveAsync(s, "a288-proj", "pl", author, RoleSet()));
        Assert.Empty(await projects.GetProjectTranslationsAsync("a288-proj"));
        Assert.Single(await AuditsFor(store, "projecttranslation.remove"));
    }

    [Fact]
    public async Task Project_Translator_Allows_ViaAdmin()
    {
        var store = await BootStoreAsync();
        var (_, _, projects, _, _) = Services(store);
        const string author = "u-a288-project-author";
        const string translator = "u-a288-project-translator";

        await Plant(store, Project("a288-proj", author));

        await RunInSession(store, s =>
            projects.ProjectTranslationLane.AddAsync(s, "a288-proj", "fr", null, "corps", translator, RoleSet(Roles.Translator)));

        var audit = Assert.Single(await AuditsFor(store, "projecttranslation.add"));
        Assert.Equal(AccessVia.Admin, audit.Via);
        Assert.Equal(translator, audit.ActorId);
    }

    [Fact]
    public async Task Project_OrdinaryMember_Denied_NoRow()
    {
        var store = await BootStoreAsync();
        var (_, _, projects, _, _) = Services(store);
        const string author = "u-a288-project-author";
        const string stranger = "u-a288-project-stranger";

        await Plant(store, Project("a288-proj", author));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => RunInSession(store, s =>
            projects.ProjectTranslationLane.AddAsync(s, "a288-proj", "pl", null, "ciało", stranger, RoleSet(Roles.Member))));

        Assert.Empty(await projects.GetProjectTranslationsAsync("a288-proj"));
        Assert.Empty(await AuditsFor(store, "projecttranslation.add"));
    }

    // ── Board surface: add → variant resolves → remove ───────────────────────

    [Fact]
    public async Task Board_AddThenResolvesThenRemove()
    {
        var store = await BootStoreAsync();
        var (_, _, _, _, boards) = Services(store);
        const string author = "u-a288-board-author";

        await Plant(store, Board("a288-board", author));

        var created = await RunInSession(store, s =>
            boards.BoardTranslationLane.AddAsync(s, "a288-board", "de", "Titel", "Körper", author, RoleSet()));
        Assert.Equal("a288-board", created.BoardId);

        var rows = await boards.GetBoardTranslationsAsync("a288-board");
        Assert.Single(rows);
        Assert.Equal("de", rows[0].LanguageCode);

        var add = Assert.Single(await AuditsFor(store, "boardtranslation.add"));
        Assert.Equal(AccessVia.Owner, add.Via);
        Assert.Equal("board", add.TargetKind);
        Assert.Equal("a288-board", add.TargetId);

        await RunInSession(store, s =>
            boards.BoardTranslationLane.RemoveAsync(s, "a288-board", "de", author, RoleSet()));
        Assert.Empty(await boards.GetBoardTranslationsAsync("a288-board"));
        Assert.Single(await AuditsFor(store, "boardtranslation.remove"));
    }

    [Fact]
    public async Task Board_GlobalAdmin_Allows_ViaAdmin()
    {
        var store = await BootStoreAsync();
        var (_, _, _, _, boards) = Services(store);
        const string author = "u-a288-board-author";
        const string admin = "u-a288-board-admin";

        await Plant(store, Board("a288-board", author));

        await RunInSession(store, s =>
            boards.BoardTranslationLane.AddAsync(s, "a288-board", "es", null, "cuerpo", admin, RoleSet(Roles.GlobalAdmin)));

        var audit = Assert.Single(await AuditsFor(store, "boardtranslation.add"));
        Assert.Equal(AccessVia.Admin, audit.Via);
        Assert.Equal(admin, audit.ActorId);
    }

    // ── Todo surface: add → variant resolves → remove ────────────────────────

    [Fact]
    public async Task Todo_AddThenResolvesThenRemove()
    {
        var store = await BootStoreAsync();
        var (_, _, _, todos, _) = Services(store);
        const string author = "u-a288-todo-author";

        await Plant(store, Todo("a288-todo", author));

        var created = await RunInSession(store, s =>
            todos.TodoTranslationLane.AddAsync(s, "a288-todo", "pl", null, "odpowiedź", author, RoleSet()));
        Assert.Equal("a288-todo", created.TodoItemId);

        var rows = await todos.GetTodoTranslationsAsync("a288-todo");
        Assert.Single(rows);
        Assert.Equal("pl", rows[0].LanguageCode);

        var add = Assert.Single(await AuditsFor(store, "todotranslation.add"));
        Assert.Equal(AccessVia.Owner, add.Via);
        Assert.Equal("todo", add.TargetKind);
        Assert.Equal("a288-todo", add.TargetId);

        await RunInSession(store, s =>
            todos.TodoTranslationLane.RemoveAsync(s, "a288-todo", "pl", author, RoleSet()));
        Assert.Empty(await todos.GetTodoTranslationsAsync("a288-todo"));
        Assert.Single(await AuditsFor(store, "todotranslation.remove"));
    }

    [Fact]
    public async Task Todo_Assignee_Allows_ViaAdmin()
    {
        var store = await BootStoreAsync();
        var (_, _, _, todos, _) = Services(store);
        const string author = "u-a288-todo-author";
        const string assignee = "u-a288-todo-assignee";

        await Plant(store, Todo("a288-todo", author, assignee: assignee));

        await RunInSession(store, s =>
            todos.TodoTranslationLane.AddAsync(s, "a288-todo", "fr", null, "corps", assignee, RoleSet()));

        var audit = Assert.Single(await AuditsFor(store, "todotranslation.add"));
        Assert.Equal(AccessVia.Admin, audit.Via);
        Assert.Equal(assignee, audit.ActorId);
    }

    [Fact]
    public async Task Todo_OrdinaryMember_Denied_NoRow()
    {
        var store = await BootStoreAsync();
        var (_, _, _, todos, _) = Services(store);
        const string author = "u-a288-todo-author";
        const string stranger = "u-a288-todo-stranger";

        await Plant(store, Todo("a288-todo", author));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => RunInSession(store, s =>
            todos.TodoTranslationLane.AddAsync(s, "a288-todo", "pl", null, "ciało", stranger, RoleSet(Roles.Member))));

        Assert.Empty(await todos.GetTodoTranslationsAsync("a288-todo"));
        Assert.Empty(await AuditsFor(store, "todotranslation.add"));
    }
}
