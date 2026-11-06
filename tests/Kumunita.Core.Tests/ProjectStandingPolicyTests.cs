using Kumunita.Core.Identity;
using Kumunita.Core.Projects;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The C04 <see cref="ProjectStandingPolicy"/> truth table — the M5 (Projects)
/// **project** mutation-standing rule (creator ∪ GlobalAdmin; the assignee
/// branch does not apply), lifted from <c>ProjectServiceTests</c>'s standing
/// cases and asserted against the policy directly. A **pure** unit — no
/// <c>PostgresFixture</>, no <see cref="Marten.IDocumentStore"/>: just the
/// <see cref="Project"/> POCO + the <see cref="Roles"/> constants. Behaviour is
/// byte-identical to the pre-C04
/// <see cref="ProjectService.CheckProjectStanding"/> static.
/// </summary>
public class ProjectStandingPolicyTests
{
    private static readonly IReadOnlySet<string> MemberRoles = new HashSet<string> { Roles.Member };
    private static readonly IReadOnlySet<string> GlobalAdminRoles = new HashSet<string> { Roles.GlobalAdmin };

    private static void ThrowsUnauthorized(Action action)
        => Assert.Throws<UnauthorizedAccessException>(action);

    [Fact]
    public void CheckProject_Creator_Allows()
    {
        var project = new Project { Id = "p-1", AuthorId = "p-creator" };
        ProjectStandingPolicy.CheckProject("p-creator", MemberRoles, project);   // no exception
    }

    [Fact]
    public void CheckProject_GlobalAdmin_Allows()
    {
        var project = new Project { Id = "p-2", AuthorId = "p-other" };
        ProjectStandingPolicy.CheckProject("p-admin", GlobalAdminRoles, project); // ADR 0017
    }

    [Fact]
    public void CheckProject_Stranger_Denies()
    {
        var project = new Project { Id = "p-3", AuthorId = "p-other" };
        ThrowsUnauthorized(() => ProjectStandingPolicy.CheckProject("p-stranger", MemberRoles, project));
    }

    [Fact]
    public void CheckProject_NoActor_Denies()
    {
        var project = new Project { Id = "p-4", AuthorId = "p-creator" };
        ThrowsUnauthorized(() => ProjectStandingPolicy.CheckProject("", GlobalAdminRoles, project));
    }

    [Fact]
    public void CheckProject_NullProject_404()
    {
        Assert.Throws<KeyNotFoundException>(
            () => ProjectStandingPolicy.CheckProject("p-admin", GlobalAdminRoles, null));
    }
}
