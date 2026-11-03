using Kumunita.Core.Identity;
using Kumunita.Core.Projects;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The C04 <see cref="BoardStandingPolicy"/> truth table — the M5 (Projects)
/// **board / lane** mutation-standing rule (creator ∪ GlobalAdmin; the assignee
/// branch does not apply), lifted from <c>ProjectServiceTests</c>'s standing
/// cases and asserted against the policy directly. A **pure** unit — no
/// <c>PostgresFixture</>, no <see cref="Marten.IDocumentStore"/>: just the
/// <see cref="KanbanBoard"/> POCO + the <see cref="Roles"/> constants.
/// Behaviour is byte-identical to the pre-C04
/// <see cref="ProjectService.CheckBoardStanding"/> static.
/// </summary>
public class BoardStandingPolicyTests
{
    private static readonly IReadOnlySet<string> MemberRoles = new HashSet<string> { Roles.Member };
    private static readonly IReadOnlySet<string> GlobalAdminRoles = new HashSet<string> { Roles.GlobalAdmin };

    private static void ThrowsUnauthorized(Action action)
        => Assert.Throws<UnauthorizedAccessException>(action);

    [Fact]
    public void CheckBoard_Creator_Allows()
    {
        var board = new KanbanBoard { Id = "b-1", AuthorId = "b-creator" };
        BoardStandingPolicy.CheckBoard("b-creator", MemberRoles, board);         // no exception
    }

    [Fact]
    public void CheckBoard_GlobalAdmin_Allows()
    {
        var board = new KanbanBoard { Id = "b-2", AuthorId = "b-other" };
        BoardStandingPolicy.CheckBoard("b-admin", GlobalAdminRoles, board);      // ADR 0017
    }

    [Fact]
    public void CheckBoard_Stranger_Denies()
    {
        var board = new KanbanBoard { Id = "b-3", AuthorId = "b-other" };
        ThrowsUnauthorized(() => BoardStandingPolicy.CheckBoard("b-stranger", MemberRoles, board));
    }

    [Fact]
    public void CheckBoard_NoActor_Denies()
    {
        var board = new KanbanBoard { Id = "b-4", AuthorId = "b-creator" };
        ThrowsUnauthorized(() => BoardStandingPolicy.CheckBoard("", GlobalAdminRoles, board));
    }

    [Fact]
    public void CheckBoard_NullBoard_404()
    {
        Assert.Throws<KeyNotFoundException>(
            () => BoardStandingPolicy.CheckBoard("b-admin", GlobalAdminRoles, null));
    }
}
