using Kumunita.Core.Identity;
using Kumunita.Core.Projects;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The C04 <see cref="TodoStandingPolicy"/> truth table — the M5 (Projects)
/// **to-do** mutation-standing rule (creator ∪ assignee ∪ GlobalAdmin), lifted
/// from <c>ProjectServiceTests</c>'s standing cases and asserted against the
/// policy directly. A **pure** unit — no <c>PostgresFixture</>, no
/// <see cref="Marten.IDocumentStore"/>: just the <see cref="TodoItem"/> POCO +
/// the <see cref="Roles"/> constants. Behaviour is byte-identical to the
/// pre-C04 <see cref="ProjectService.CheckTodoStanding"/> static.
/// </summary>
public class TodoStandingPolicyTests
{
    private static readonly IReadOnlySet<string> MemberRoles = new HashSet<string> { Roles.Member };
    private static readonly IReadOnlySet<string> GlobalAdminRoles = new HashSet<string> { Roles.GlobalAdmin };

    private static void ThrowsUnauthorized(Action action)
        => Assert.Throws<UnauthorizedAccessException>(action);

    [Fact]
    public void CheckTodo_Creator_Allows()
    {
        var todo = new TodoItem { Id = "t-1", AuthorId = "t-creator" };
        TodoStandingPolicy.CheckTodo("t-creator", MemberRoles, todo);            // no exception
    }

    [Fact]
    public void CheckTodo_Assignee_Allows()
    {
        var todo = new TodoItem { Id = "t-2", AuthorId = "t-other", AssigneeId = "t-assignee" };
        TodoStandingPolicy.CheckTodo("t-assignee", MemberRoles, todo);           // ADR 0067
    }

    [Fact]
    public void CheckTodo_GlobalAdmin_Allows()
    {
        var todo = new TodoItem { Id = "t-3", AuthorId = "t-other", AssigneeId = "t-other" };
        TodoStandingPolicy.CheckTodo("t-admin", GlobalAdminRoles, todo);         // ADR 0017
    }

    [Fact]
    public void CheckTodo_Stranger_Denies()
    {
        var todo = new TodoItem { Id = "t-4", AuthorId = "t-other", AssigneeId = "t-other" };
        ThrowsUnauthorized(() => TodoStandingPolicy.CheckTodo("t-stranger", MemberRoles, todo));
    }

    [Fact]
    public void CheckTodo_NoActor_Denies()
    {
        var todo = new TodoItem { Id = "t-5", AuthorId = "t-creator" };
        ThrowsUnauthorized(() => TodoStandingPolicy.CheckTodo("", GlobalAdminRoles, todo));
    }

    [Fact]
    public void CheckTodo_NullTodo_404()
    {
        Assert.Throws<KeyNotFoundException>(
            () => TodoStandingPolicy.CheckTodo("t-admin", GlobalAdminRoles, null));
    }
}
