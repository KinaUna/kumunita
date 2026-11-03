using Kumunita.Core.Identity;
using Kumunita.Core.Projects;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The C04 <see cref="GoalStandingPolicy"/> truth table — the M5 (Projects)
/// **goal** mutation-standing rule (creator ∪ GlobalAdmin; the assignee branch
/// does not apply), lifted from <c>ProjectServiceTests</c>'s standing cases and
/// asserted against the policy directly. A **pure** unit — no
/// <c>PostgresFixture</>, no <see cref="Marten.IDocumentStore"/>: just the
/// <see cref="ProjectGoal"/> POCO + the <see cref="Roles"/> constants.
/// Behaviour is byte-identical to the pre-C04
/// <see cref="ProjectService.CheckGoalStanding"/> static.
/// </summary>
public class GoalStandingPolicyTests
{
    private static readonly IReadOnlySet<string> MemberRoles = new HashSet<string> { Roles.Member };
    private static readonly IReadOnlySet<string> GlobalAdminRoles = new HashSet<string> { Roles.GlobalAdmin };

    private static void ThrowsUnauthorized(Action action)
        => Assert.Throws<UnauthorizedAccessException>(action);

    [Fact]
    public void CheckGoal_Creator_Allows()
    {
        var goal = new ProjectGoal { Id = "g-1", AuthorId = "g-creator" };
        GoalStandingPolicy.CheckGoal("g-creator", MemberRoles, goal);            // no exception
    }

    [Fact]
    public void CheckGoal_GlobalAdmin_Allows()
    {
        var goal = new ProjectGoal { Id = "g-2", AuthorId = "g-other" };
        GoalStandingPolicy.CheckGoal("g-admin", GlobalAdminRoles, goal);         // ADR 0017
    }

    [Fact]
    public void CheckGoal_Stranger_Denies()
    {
        var goal = new ProjectGoal { Id = "g-3", AuthorId = "g-other" };
        ThrowsUnauthorized(() => GoalStandingPolicy.CheckGoal("g-stranger", MemberRoles, goal));
    }

    [Fact]
    public void CheckGoal_NoActor_Denies()
    {
        var goal = new ProjectGoal { Id = "g-4", AuthorId = "g-creator" };
        ThrowsUnauthorized(() => GoalStandingPolicy.CheckGoal("", GlobalAdminRoles, goal));
    }

    [Fact]
    public void CheckGoal_NullGoal_404()
    {
        Assert.Throws<KeyNotFoundException>(
            () => GoalStandingPolicy.CheckGoal("g-admin", GlobalAdminRoles, null));
    }
}
