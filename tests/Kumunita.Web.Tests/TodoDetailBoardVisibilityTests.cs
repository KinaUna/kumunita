using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Kumunita.Core.Projects;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Marten;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// IMPROVE-SEC IS10 (E4, the one confirmed S-severity leak in the 52
/// decision-free reads) — <c>GET /projects/todos/{id}</c> must **not**
/// disclose the title (or a lane chip) of a board the actor may not read.
/// <para>
/// The fix (the <c>TodosController.TodoDetail</c> F2 block) routes the
/// board-title lookup through the <c>CanSeeAsync</c>-filtered seam
/// <see cref="IBoardService.ListBoardsForTodoAsync"/> — the decision the
/// raw <c>session.Query&lt;KanbanBoard&gt;()</c> title lookup lacked — and
/// keeps placements + lanes only for boards in the visible set (a lane's
/// visibility is its board's, C-M5·3). The seam itself is the frozen
/// <see cref="Kumunita.Core.Projects.BoardService"/> (its
/// <c>CanSeeAsync(Read)</c> filter is U08-pinned); here it is
/// **NSubstituted** to drive the two visibility outcomes, while the
/// placements/lanes reads run over a **real** scratch-Postgres store (Marten
/// 9's <c>ToListAsync()</c> casts to the internal
/// <c>MartenLinqQueryable</c> and cannot be NSubstituted — the
/// <see cref="ProjectsControllerTests"/> / <see cref="TagControllerTests"/>
/// precedent).
/// </para>
/// </summary>
public sealed class TodoDetailBoardVisibilityTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // A board the actor **may** read (the visible board + its lane).
    private const string Actor = "subj-is10-actor";
    private const string TodoId = "todo-is10";
    private const string VisibleBoardId = "board-is10-visible";
    private const string VisibleLaneId = "lane-is10-visible";
    private const string VisibleBoardTitle = "Visible board";
    private const string VisibleLaneTitle = "Visible lane";

    // A board the actor may **not** read (audience-restricted to another
    // group) — its title must never reach the view.
    private const string RestrictedBoardId = "board-is10-restricted";
    private const string RestrictedLaneId = "lane-is10-restricted";
    private const string RestrictedBoardTitle = "Restricted board";

    /// <summary>
    /// Boots a real Marten <see cref="IDocumentStore"/> over a fresh scratch
    /// Postgres database (the <see cref="ProjectsControllerTests"/>
    /// <c>BuildRealStoreAsync</c> idiom) and seeds: the to-do, both boards,
    /// both lanes, and the to-do's two placements (one per board) — the F2
    /// surface the detail view renders (now decided).
    /// </summary>
    private async Task<IDocumentStore> BuildStoreAsync()
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
            EventDocTypes.Configure(opts);
            ProjectDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        await using (var w = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            w.Store(new TodoItem
            {
                Id = TodoId, Title = "IS10 to-do", AuthorId = Actor,
                Created = new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero),
            });
            w.Store(new KanbanBoard
            {
                Id = VisibleBoardId, Title = VisibleBoardTitle, AuthorId = Actor,
                Created = new DateTimeOffset(2026, 11, 7, 8, 0, 0, TimeSpan.Zero),
            });
            w.Store(new KanbanBoard
            {
                Id = RestrictedBoardId, Title = RestrictedBoardTitle,
                AuthorId = "subj-is10-other",
                // The board's Audience is what the (substituted) seam uses to
                // keep it out of the visible set; the value here is not the
                // decision driver — the seam's CanSeeAsync is — but it makes
                // the fixture self-describing (an audience-restricted board).
                Audience = new Audience
                {
                    Mode = AudienceMode.Any,
                    Grants = [new AudienceGrant(GrantKind.Group, "grp-is10-other")],
                },
                Created = new DateTimeOffset(2026, 11, 7, 8, 0, 0, TimeSpan.Zero),
            });
            w.Store(new KanbanLane
            {
                Id = VisibleLaneId, BoardId = VisibleBoardId, Title = VisibleLaneTitle, Order = 0,
                Created = new DateTimeOffset(2026, 11, 7, 8, 0, 0, TimeSpan.Zero),
            });
            w.Store(new KanbanLane
            {
                Id = RestrictedLaneId, BoardId = RestrictedBoardId, Title = "Restricted lane", Order = 0,
                Created = new DateTimeOffset(2026, 11, 7, 8, 0, 0, TimeSpan.Zero),
            });
            w.Store(new BoardItemPlacement
            {
                Id = "place-is10-visible", TodoItemId = TodoId,
                BoardId = VisibleBoardId, LaneId = VisibleLaneId, Order = 0,
                Created = new DateTimeOffset(2026, 11, 7, 8, 30, 0, TimeSpan.Zero),
            });
            w.Store(new BoardItemPlacement
            {
                Id = "place-is10-restricted", TodoItemId = TodoId,
                BoardId = RestrictedBoardId, LaneId = RestrictedLaneId, Order = 1,
                Created = new DateTimeOffset(2026, 11, 7, 8, 30, 0, TimeSpan.Zero),
            });
            await w.SaveChangesAsync(ct);
        }
        return store;
    }

    /// <summary>
    /// Builds a <see cref="TodosController"/> over NSubstitute seams (the
    /// <see cref="ProjectsControllerTests"/> <c>BuildTodos</c> idiom) with a
    /// fresh principal, and pins the two seam outcomes the test drives:
    /// <see cref="ITodoService.GetTodoAsync"/> (the to-do + empty subtasks)
    /// and <see cref="IBoardService.ListBoardsForTodoAsync"/> (the visible
    /// set the F2 block filters to).
    /// </summary>
    private static TodosController BuildController(
        IDocumentStore store,
        IBoardService boards,
        ITodoService todos)
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync(Arg.Any<string>()).Returns((Profile?)null);
        var profileSettings = Substitute.For<IProfileSettingsService>();
        var groupService = Substitute.For<IGroupService>();

        var localization = Substitute.For<ILocalizationService>();
        localization.ListLanguagesAsync().Returns(new List<LanguageCatalog>
        {
            new() { Id = "en", NativeName = "English", Enabled = true, SortOrder = 1 },
        });
        localization.GetDefaultLanguageCodeAsync().Returns("en");

        var controller = new TodosController(
            Substitute.For<IProjectService>(), userInfo, groupService, profileSettings,
            localization, store, boards, todos);
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(
                    new System.Security.Claims.ClaimsIdentity(
                        [new System.Security.Claims.Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Actor)],
                        authenticationType: "test")),
            },
        };
        controller.TempData = new TempDataDictionary(
            new Microsoft.AspNetCore.Http.DefaultHttpContext(), new NoOpTempDataProvider());
        return controller;
    }

    private static ITodoService TodoSeam()
    {
        var todos = Substitute.For<ITodoService>();
        todos.GetTodoAsync(TodoId, Actor, Arg.Any<CancellationToken>())
            .Returns(new TodoDetailResult
            {
                Todo = new TodoItem
                {
                    Id = TodoId, Title = "IS10 to-do", AuthorId = Actor,
                    Created = new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero),
                },
                Subtasks = [],
            });
        return todos;
    }

    /// <summary>
    /// The leak, closed: a board the actor may **not** read (the seam returns
    /// it as not visible) must **not** appear in the detail view's
    /// <c>Placements</c> — no placement row for it (hence no
    /// <see cref="TodoPlacementRow.BoardTitle"/> /
    /// <see cref="TodoPlacementRow.LaneTitle"/> for it either). The pre-IS10
    /// code rendered its title via the raw <c>KanbanBoard</c> query; this
    /// pins that it does not.
    /// </summary>
    [Fact]
    public async Task TodoDetail_RestrictedBoardTitle_NotShown()
    {
        var store = await BuildStoreAsync();

        // The seam's decision (substituted here, U08-pinned in production):
        // the restricted board is **not** in the actor's visible set.
        var boards = Substitute.For<IBoardService>();
        boards.ListBoardsForTodoAsync(TodoId, Actor, Arg.Any<CancellationToken>())
            .Returns(new List<KanbanBoard>());

        var controller = BuildController(store, boards, TodoSeam());
        var vm = Assert.IsType<TodoDetailViewModel>(
            Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(await controller.TodoDetail(TodoId)).ViewData.Model);

        // No placement row for the restricted board at all (so no title, no
        // lane chip): the leak is closed.
        Assert.DoesNotContain(vm.Placements, p => p.BoardId == RestrictedBoardId);
        // And the view carries the restricted title nowhere.
        Assert.DoesNotContain(vm.Placements, p => p.BoardTitle == RestrictedBoardTitle);
    }

    /// <summary>
    /// The fix does not over-suppress: a board the actor **may** read still
    /// renders its title + its lane chip (the F2 display surface, now
    /// *decided*).
    /// </summary>
    [Fact]
    public async Task TodoDetail_VisibleBoardTitle_Shown()
    {
        var store = await BuildStoreAsync();

        // The seam returns exactly the visible board (the production
        // BoardService's CanSeeAsync filter outcome, substituted here).
        var boards = Substitute.For<IBoardService>();
        boards.ListBoardsForTodoAsync(TodoId, Actor, Arg.Any<CancellationToken>())
            .Returns(new List<KanbanBoard>
            {
                new()
                {
                    Id = VisibleBoardId, Title = VisibleBoardTitle, AuthorId = Actor,
                    Created = new DateTimeOffset(2026, 11, 7, 8, 0, 0, TimeSpan.Zero),
                },
            });

        var controller = BuildController(store, boards, TodoSeam());
        var vm = Assert.IsType<TodoDetailViewModel>(
            Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(await controller.TodoDetail(TodoId)).ViewData.Model);

        var visible = vm.Placements.Where(p => p.BoardId == VisibleBoardId).ToList();
        // Exactly one placement row (the visible board's) — the restricted
        // board's row is absent.
        Assert.Single(visible);
        var row = visible[0];
        Assert.Equal(VisibleBoardTitle, row.BoardTitle);
        Assert.Equal(VisibleLaneId, row.LaneId);
        Assert.Equal(VisibleLaneTitle, row.LaneTitle);
        // The decision seam was consulted (the F2 block's new shape).
        await boards.Received(1).ListBoardsForTodoAsync(TodoId, Actor, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The lane chip on a board the actor may not read is dropped with the
    /// placement (a lane's visibility is its board's, C-M5·3): no lane on the
    /// restricted board reaches the view.
    /// </summary>
    [Fact]
    public async Task TodoDetail_RestrictedBoardPlacement_NoLaneChip()
    {
        var store = await BuildStoreAsync();

        // The seam returns the visible board only — the restricted board's
        // lane is in the restricted board's set, which is not visible.
        var boards = Substitute.For<IBoardService>();
        boards.ListBoardsForTodoAsync(TodoId, Actor, Arg.Any<CancellationToken>())
            .Returns(new List<KanbanBoard>
            {
                new()
                {
                    Id = VisibleBoardId, Title = VisibleBoardTitle, AuthorId = Actor,
                    Created = new DateTimeOffset(2026, 11, 7, 8, 0, 0, TimeSpan.Zero),
                },
            });

        var controller = BuildController(store, boards, TodoSeam());
        var vm = Assert.IsType<TodoDetailViewModel>(
            Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(await controller.TodoDetail(TodoId)).ViewData.Model);

        // The restricted board's lane id never appears on any placement row.
        Assert.DoesNotContain(vm.Placements, p => p.LaneId == RestrictedLaneId);
        // The visible lane chip is intact (the drop is scoped to the
        // restricted board's lane, not all lanes).
        Assert.Contains(vm.Placements, p => p.BoardId == VisibleBoardId && p.LaneId == VisibleLaneId);
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(Microsoft.AspNetCore.Http.HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(Microsoft.AspNetCore.Http.HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the view model, not the bag
        }
    }
}
