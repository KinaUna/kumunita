using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Kumunita.Core.Projects;
using Kumunita.Core.Query;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Marten;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// C12 — the <see cref="ProjectsBaseController"/> shared static seams (C08,
/// ADR 0287). The base controller is <c>abstract</c>; its protected static
/// helpers (<c>ProjectRow</c>, <c>ParseSort</c>, <c>HasSortParam</c>,
/// <c>BuildBoardFilterParams</c>, <c>BuildLandingFilterParams</c>) are the pure
/// decision surface shared by <c>BoardsController</c> / <c>TodosController</c> /
/// <c>ProjectsController</c>. This file exposes them via a minimal test subclass
/// so each is unit-tested at its seam.
/// <para>
/// **Part-vs-whole (D1/D6):** the test subclass (<see cref="TestProjectBase"/>)
/// is a test double with a real consumer (this test class) and its own tests —
/// it is not a production part, so the rule is satisfied by the tests it enables.
/// </para>
/// </summary>
public sealed class ProjectsBaseControllerTests
{
    /// <summary>
    /// The minimal concrete subclass: the constructor's required args are
    /// satisfied with nulls (only <c>groupService</c> / <c>profileSettings</c>
    /// are null-checked, and none of the static helpers touch the instance
    /// state), exposing the <c>protected static</c> seams as <c>public static</c>.
    /// </summary>
    private sealed class TestProjectBase : ProjectsBaseController
    {
        public TestProjectBase() : base(
            projects: null!,
            userInfo: null!,
            groupService: NSubstitute.Substitute.For<Kumunita.Core.UserInfo.IGroupService>(),
            profileSettings: NSubstitute.Substitute.For<Kumunita.Core.UserInfo.IProfileSettingsService>(),
            localization: null!,
            store: null!,
            boards: null!,
            todos: null!)
        {
        }

        public new static TodoRow ProjectRow(
            TodoItem todo, TodoItem? parent,
            IReadOnlyDictionary<string, string> authorNames,
            IReadOnlyDictionary<string, string> componentNames,
            IReadOnlyDictionary<string, string> groupNames,
            IReadOnlySet<string> actorGroupIds,
            IReadOnlyCollection<string> actorCommunityIds)
            => ProjectsBaseController.ProjectRow(todo, parent, authorNames, componentNames, groupNames, actorGroupIds, actorCommunityIds);

        public static SortSpec? ParseTodoSort(string? sort, string? dir)
            => ProjectsBaseController.ParseSort(sort, dir, ProjectsBaseController.TodoFeedAllowedKeys);

        public new static bool HasSortParam(string? sort) => ProjectsBaseController.HasSortParam(sort);

        public static IReadOnlyDictionary<string, string>? BoardFilter(string? componentId, string? sort, string? dir)
            => ProjectsBaseController.BuildBoardFilterParams(componentId, sort, dir);

        public static IReadOnlyDictionary<string, string>? LandingFilter(string? componentId, string? sort, string? dir)
            => ProjectsBaseController.BuildLandingFilterParams(componentId, sort, dir);
    }

    private static TodoItem NewTodo(string id, string? assignee = null, string? componentId = null, string? audienceGroupId = null)
    {
        var todo = new TodoItem
        {
            Id = id,
            Title = "To-do " + id,
            AuthorId = "author-1",
            AssigneeId = assignee,
            ComponentId = componentId,
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Modified = new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.Zero)
        };
        if (audienceGroupId is not null)
        {
            todo.Audience = new Audience { Grants = { new AudienceGrant(GrantKind.Group, audienceGroupId) } };
        }
        return todo;
    }

    [Fact]
    public void ProjectRow_Unassigned_GroupMember_CanClaim()
    {
        var todo = NewTodo("todo-1", assignee: null, audienceGroupId: "group-9");
        var row = TestProjectBase.ProjectRow(
            todo, null,
            authorNames: new Dictionary<string, string> { ["author-1"] = "Ada" },
            componentNames: new Dictionary<string, string>(),
            groupNames: new Dictionary<string, string> { ["group-9"] = "Builders" },
            actorGroupIds: new HashSet<string> { "group-9" },
            actorCommunityIds: new List<string>());

        Assert.True(row.CanClaim);
        Assert.Equal("Ada", row.AuthorDisplayName);
        Assert.Contains("Builders", row.GroupNames);
    }

    [Fact]
    public void ProjectRow_Assigned_CannotClaim()
    {
        var todo = NewTodo("todo-2", assignee: "someone", audienceGroupId: "group-9");
        var row = TestProjectBase.ProjectRow(
            todo, null,
            authorNames: new Dictionary<string, string>(),
            componentNames: new Dictionary<string, string>(),
            groupNames: new Dictionary<string, string>(),
            actorGroupIds: new HashSet<string> { "group-9" },
            actorCommunityIds: new List<string>());

        Assert.False(row.CanClaim);   // assigned → no claim affordance
    }

    [Fact]
    public void ProjectRow_CommunityTodo_CommunityMember_CanClaim()
    {
        var todo = NewTodo("todo-3", assignee: null, componentId: "comm-7");
        var row = TestProjectBase.ProjectRow(
            todo, null,
            authorNames: new Dictionary<string, string>(),
            componentNames: new Dictionary<string, string> { ["comm-7"] = "Main St" },
            groupNames: new Dictionary<string, string>(),
            actorGroupIds: new HashSet<string>(),
            actorCommunityIds: new List<string> { "comm-7" });

        Assert.True(row.CanClaim);
        Assert.Equal("Main St", row.ComponentDisplayName);
    }

    [Fact]
    public void ParseTodoSort_AllowsClosedKeySet_RejectsUnknown()
    {
        // "created" is in the todo allowlist → parses.
        var ok = TestProjectBase.ParseTodoSort("created", "desc");
        Assert.NotNull(ok);
        Assert.Equal("created", ok!.Key);
        Assert.True(ok.Descending);

        // "priority" is NOT in the todo allowlist → falls back to the default key.
        var unknown = TestProjectBase.ParseTodoSort("priority", "asc");
        Assert.NotNull(unknown);
        Assert.Equal("created", unknown!.Key);   // the surface's default
        Assert.False(unknown.Descending);        // "asc" honored
    }

    [Fact]
    public void HasSortParam_BlankIsFalse()
    {
        Assert.False(TestProjectBase.HasSortParam(null));
        Assert.False(TestProjectBase.HasSortParam(""));
        Assert.False(TestProjectBase.HasSortParam("   "));
        Assert.True(TestProjectBase.HasSortParam("created"));
    }

    [Fact]
    public void BoardFilter_CarriesComponentAndSort()
    {
        var p = TestProjectBase.BoardFilter("comm-1", "created", "desc");
        Assert.NotNull(p);
        Assert.Equal("comm-1", p!["componentId"]);
        Assert.Equal("created", p["sort"]);
        Assert.Equal("desc", p["dir"]);
    }

    [Fact]
    public void LandingFilter_NoParams_ReturnsNull()
    {
        // No component, no sort → the pager carries nothing (null, not an empty bag).
        Assert.Null(TestProjectBase.LandingFilter(null, null, null));
    }
}
