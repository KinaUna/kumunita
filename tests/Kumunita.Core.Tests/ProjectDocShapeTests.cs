using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Projects;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The <c>PL</c> U01 doc-shape pin (ADR 0086 D2 / D3 / D4, the design doc
/// §9.1 / §9.2 / §9.5): the two new documents
/// <see cref="ProjectGoal"/> + <see cref="Project"/> boot through
/// <see cref="M5DocTypes.Configure"/> and round-trip against a fresh
/// scratch Postgres (the schema + the registration surface are live); the
/// two new adapters implement the frozen 6-member
/// <see cref="IAuditableResource"/> surface with the **exact**
/// <c>TargetKind</c> strings ("goal" / "project" — the
/// <see cref="AccessAudit.TargetKind"/> discriminators, C3); the additive
/// <see cref="TodoItem.ProjectId"/> / <see cref="KanbanBoard.ProjectId"/>
/// fields exist and are nullable (the C-M3·2 feed-filter pin — a feed
/// filter, never a gate). **One** test — this unit is about the **doc
/// shapes** compiling + registering, not behavior (the behavior pins are
/// U02–U09's, the §9.6 pinned names).
/// </summary>
public class ProjectDocShapeTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    /// <summary>
    /// The two PL documents boot through <see cref="M5DocTypes.Configure"/>
    /// without error and round-trip (the delta-detected schema is live); the
    /// two PL adapters present the frozen 6-member
    /// <see cref="IAuditableResource"/> projection with the exact
    /// <c>TargetKind</c> strings; the two additive <c>ProjectId</c> fields
    /// exist and are nullable (a feed filter, never a gate — C-M3·2).
    /// </summary>
    [Fact]
    public async Task ProjectGoal_Project_ResolveAndRegister()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await BootStoreAsync(ct);

        // ── The two new docs round-trip (schema + registration surface live) ──
        await using (var w = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            w.Store(new ProjectGoal
            {
                Id = "pl-goal-1",
                Title = "Green the street",
                Description = "**Optional** Markdown",
                ComponentId = "comp-1",
                AuthorId = "u-pl-author",
                Audience = null, // public
                IsDeleted = false,
                LanguageCode = "en",
                Created = new DateTimeOffset(2026, 9, 25, 9, 0, 0, TimeSpan.Zero),
            });
            w.Store(new Project
            {
                Id = "pl-proj-1",
                Title = "Paint the crosswalk",
                Description = null,
                GoalId = "pl-goal-1",
                Status = "in-progress",
                StartAt = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
                DueAt = null,
                ComponentId = "comp-1",
                AuthorId = "u-pl-author",
                Audience = null, // public
                IsDeleted = false,
                LanguageCode = "en",
                Created = new DateTimeOffset(2026, 9, 25, 9, 30, 0, TimeSpan.Zero),
            });
            w.Store(new TodoItem
            {
                Id = "pl-todo-1",
                Title = "Buy the paint",
                AuthorId = "u-pl-author",
                ProjectId = "pl-proj-1", // the additive ADR 0086 D4 field
                Created = new DateTimeOffset(2026, 9, 25, 9, 45, 0, TimeSpan.Zero),
            });
            w.Store(new KanbanBoard
            {
                Id = "pl-board-1",
                Title = "Crosswalk board",
                AuthorId = "u-pl-author",
                ProjectId = "pl-proj-1", // the additive ADR 0086 D4 field
                Created = new DateTimeOffset(2026, 9, 25, 9, 45, 0, TimeSpan.Zero),
            });
            await w.SaveChangesAsync(ct);
        }

        await using (var q = store.QuerySession())
        {
            var goal = (await q.LoadAsync<ProjectGoal>("pl-goal-1", ct))!;
            Assert.Equal("Green the street", goal.Title);
            Assert.Equal("comp-1", goal.ComponentId);
            Assert.Equal("u-pl-author", goal.AuthorId);
            Assert.Null(goal.Audience);
            Assert.False(goal.IsDeleted);

            var project = (await q.LoadAsync<Project>("pl-proj-1", ct))!;
            Assert.Equal("Paint the crosswalk", project.Title);
            Assert.Equal("pl-goal-1", project.GoalId);
            Assert.Equal("in-progress", project.Status);
            Assert.Null(project.DueAt);

            var todo = (await q.LoadAsync<TodoItem>("pl-todo-1", ct))!;
            Assert.Equal("pl-proj-1", todo.ProjectId);

            var board = (await q.LoadAsync<KanbanBoard>("pl-board-1", ct))!;
            Assert.Equal("pl-proj-1", board.ProjectId);
        }

        // ── The two new adapters: the frozen 6-member surface + exact TargetKind ──
        var goalDoc = new ProjectGoal
        {
            Id = "pl-goal-a",
            Title = "Goal A",
            AuthorId = "u-pl-author",
            ComponentId = "comp-a",
            Audience = null,
        };
        var goalAdapter = new ProjectGoalToAuditableResource(goalDoc);
        Assert.Equal("pl-goal-a", goalAdapter.Id);
        Assert.Equal("Goal A", goalAdapter.Name);
        Assert.Equal("u-pl-author", goalAdapter.OwnerId);
        Assert.Null(goalAdapter.Audience);
        Assert.Equal("comp-a", goalAdapter.ComponentId);
        Assert.Equal("goal", goalAdapter.TargetKind); // the EXACT string (C3)

        var projectDoc = new Project
        {
            Id = "pl-proj-a",
            Title = "Project A",
            AuthorId = "u-pl-author",
            ComponentId = "comp-a",
            Audience = null,
        };
        var projectAdapter = new ProjectToAuditableResource(projectDoc);
        Assert.Equal("pl-proj-a", projectAdapter.Id);
        Assert.Equal("Project A", projectAdapter.Name);
        Assert.Equal("u-pl-author", projectAdapter.OwnerId);
        Assert.Null(projectAdapter.Audience);
        Assert.Equal("comp-a", projectAdapter.ComponentId);
        Assert.Equal("project", projectAdapter.TargetKind); // the EXACT string (C3)

        // ── The two additive ProjectId fields: exist + nullable (C-M3·2) ──
        var todoProjectId = typeof(TodoItem).GetProperty("ProjectId");
        Assert.NotNull(todoProjectId);
        Assert.Equal(typeof(string), todoProjectId!.PropertyType); // `string?` — nullable reference type
        Assert.True(todoProjectId.CanRead && todoProjectId.CanWrite);

        var boardProjectId = typeof(KanbanBoard).GetProperty("ProjectId");
        Assert.NotNull(boardProjectId);
        Assert.Equal(typeof(string), boardProjectId!.PropertyType); // `string?` — nullable reference type
        Assert.True(boardProjectId.CanRead && boardProjectId.CanWrite);
    }

    private async Task<IDocumentStore> BootStoreAsync(CancellationToken ct)
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
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);
        return store;
    }
}
