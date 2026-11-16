using Kumunita.Core.Identity;
using Kumunita.Core.Posts;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The C04 <see cref="PostTranslationPolicy"/> truth table — the ADR 0022
/// post/reply translation-variant rule, lifted from
/// <c>PostTranslationTests.CanAddTranslation_MatchesStandingMatrix</c> and the
/// ADR 0048 lanes, and asserted against the policy directly. A **pure** unit
/// — no <c>PostgresFixture</>, no <see cref="Marten.IDocumentStore"/>.
/// Behaviour is byte-identical to the pre-C04
/// <see cref="PostService.CanAddTranslation"/> static.
/// </summary>
public class PostTranslationPolicyTests
{
    private const string ComponentId = "c-a022-comp";
    private const string Author = "a022-author";

    private static IReadOnlySet<string> RoleSet(params string[] roles) => new HashSet<string>(roles);

    [Fact]
    public void CanAddTranslation_MatchesStandingMatrix()
    {
        // Community lane — author (Owner), community-moderator (Moderator),
        // GlobalAdmin (Admin) all allow.
        Assert.True(PostTranslationPolicy.CanAddTranslation(false, ComponentId, Author, Author, RoleSet()));
        Assert.True(PostTranslationPolicy.CanAddTranslation(
            false, ComponentId, Author, "a022-moderator", RoleSet(Roles.Moderator, Roles.ModeratorComponent(ComponentId))));
        Assert.True(PostTranslationPolicy.CanAddTranslation(false, ComponentId, Author, "a022-admin", RoleSet(Roles.GlobalAdmin)));

        // Community lane — a moderator of a *different* component is denied,
        // as is a plain Member (no standing at all).
        Assert.False(PostTranslationPolicy.CanAddTranslation(
            false, ComponentId, Author, "a022-moderator-other", RoleSet(Roles.Moderator, Roles.ModeratorComponent("c-other"))));
        Assert.False(PostTranslationPolicy.CanAddTranslation(false, ComponentId, Author, "a022-member", RoleSet(Roles.Member)));

        // Group lane — author and GlobalAdmin allow; the component-moderator
        // branch is excluded (ADR 0007 — the group lane has no
        // component-moderator standing).
        Assert.True(PostTranslationPolicy.CanAddTranslation(true, ComponentId, Author, Author, RoleSet()));
        Assert.True(PostTranslationPolicy.CanAddTranslation(true, ComponentId, Author, "a022-admin", RoleSet(Roles.GlobalAdmin)));
        Assert.False(PostTranslationPolicy.CanAddTranslation(
            true, ComponentId, Author, "a022-moderator", RoleSet(Roles.Moderator, Roles.ModeratorComponent(ComponentId))));
    }

    [Fact]
    public void ResolveTranslationStanding_DerivesAccessVia()
    {
        // Author → Owner (the narrowest right).
        Assert.Equal(Authorization.AccessVia.Owner,
            PostTranslationPolicy.ResolveTranslationStanding(false, ComponentId, Author, Author, RoleSet()));
        // GlobalAdmin → Admin.
        Assert.Equal(Authorization.AccessVia.Admin,
            PostTranslationPolicy.ResolveTranslationStanding(false, ComponentId, Author, "a022-admin", RoleSet(Roles.GlobalAdmin)));
        // Community-lane component-moderator → Moderator.
        Assert.Equal(Authorization.AccessVia.Moderator,
            PostTranslationPolicy.ResolveTranslationStanding(
                false, ComponentId, Author, "a022-mod", RoleSet(Roles.Moderator, Roles.ModeratorComponent(ComponentId))));
        // Group lane — no Moderator branch (ADR 0007): a member moderator is denied.
        Assert.Null(
            PostTranslationPolicy.ResolveTranslationStanding(
                true, ComponentId, Author, "a022-mod", RoleSet(Roles.Moderator, Roles.ModeratorComponent(ComponentId))));
    }
}
