using Kumunita.Core.Announcements;
using Kumunita.Core.Identity;
using Kumunita.Core.Pages;
using Kumunita.Core.Projects;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The C2-03 truth tables for the five new <c>*TranslationPolicy</c> statics
/// (the C04 decision-extraction shape applied to the five
/// <c>Can*Translation</c> service statics). Each test is a pure unit — no
/// <see cref="Marten.IDocumentStore"/>, no <c>PostgresFixture</c> — pinning
/// the exact rule the service delegates to.
/// </summary>
public class TranslationPolicyTests
{
    private const string Actor = "actor-1";
    private const string Other = "other-1";
    private const string CommunityId = "c-trans-1";

    private static IReadOnlySet<string> RoleSet(params string[] roles) => new HashSet<string>(roles);

    // ── AnnouncementTranslationPolicy (ADR 0029) ──────────────────────────

    [Fact]
    public void Announcement_CanTranslate_Translator_Allows_AnyScope()
        => Assert.True(AnnouncementTranslationPolicy.CanTranslateAnnouncement(
            AnnouncementScope.Community, CommunityId, Actor, RoleSet(Roles.Translator)));

    [Fact]
    public void Announcement_CanTranslate_GlobalAdmin_Allows_AnyScope()
        => Assert.True(AnnouncementTranslationPolicy.CanTranslateAnnouncement(
            AnnouncementScope.Public, null, Actor, RoleSet(Roles.GlobalAdmin)));

    [Fact]
    public void Announcement_CanTranslate_ModeratorComponent_Allows_OnlyTargetedCommunity()
    {
        Assert.True(AnnouncementTranslationPolicy.CanTranslateAnnouncement(
            AnnouncementScope.Community, CommunityId, "mod-1",
            RoleSet(Roles.Moderator, Roles.ModeratorComponent(CommunityId))));

        Assert.False(AnnouncementTranslationPolicy.CanTranslateAnnouncement(
            AnnouncementScope.Public, null, "mod-2",
            RoleSet(Roles.Moderator, Roles.ModeratorComponent(CommunityId))));

        Assert.False(AnnouncementTranslationPolicy.CanTranslateAnnouncement(
            AnnouncementScope.Community, "c-other", "mod-3",
            RoleSet(Roles.Moderator, Roles.ModeratorComponent(CommunityId))));
    }

    [Fact]
    public void Announcement_CanTranslate_PlainMember_Denies()
        => Assert.False(AnnouncementTranslationPolicy.CanTranslateAnnouncement(
            AnnouncementScope.Community, CommunityId, Actor, RoleSet(Roles.Member)));

    [Fact]
    public void Announcement_ResolveTranslationStanding_DerivesAccessVia()
    {
        Assert.Equal(Authorization.AccessVia.Admin,
            AnnouncementTranslationPolicy.ResolveTranslationStanding(
                AnnouncementScope.Community, CommunityId, Actor, RoleSet(Roles.Translator)));
        Assert.Equal(Authorization.AccessVia.Moderator,
            AnnouncementTranslationPolicy.ResolveTranslationStanding(
                AnnouncementScope.Community, CommunityId, "mod-1",
                RoleSet(Roles.Moderator, Roles.ModeratorComponent(CommunityId))));
        Assert.Null(AnnouncementTranslationPolicy.ResolveTranslationStanding(
            AnnouncementScope.Community, CommunityId, Actor, RoleSet(Roles.Member)));
    }

    // ── BoardTranslationPolicy (ADR 0088) ─────────────────────────────────

    [Fact]
    public void Board_CanAddTranslation_Author_Allows()
        => Assert.True(BoardTranslationPolicy.CanAddBoardTranslation(Actor, Actor, RoleSet(Roles.Member)));

    [Fact]
    public void Board_CanAddTranslation_Translator_Allows()
        => Assert.True(BoardTranslationPolicy.CanAddBoardTranslation(Other, Actor, RoleSet(Roles.Translator)));

    [Fact]
    public void Board_CanAddTranslation_GlobalAdmin_Allows()
        => Assert.True(BoardTranslationPolicy.CanAddBoardTranslation(Other, Actor, RoleSet(Roles.GlobalAdmin)));

    [Fact]
    public void Board_CanAddTranslation_PlainMemberOfOtherBoard_Denies()
        => Assert.False(BoardTranslationPolicy.CanAddBoardTranslation(Other, Actor, RoleSet(Roles.Member)));

    [Fact]
    public void Board_ResolveTranslationStanding_DerivesAccessVia()
    {
        Assert.Equal(Authorization.AccessVia.Owner,
            BoardTranslationPolicy.ResolveTranslationStanding(Actor, Actor, RoleSet(Roles.Member)));
        Assert.Equal(Authorization.AccessVia.Admin,
            BoardTranslationPolicy.ResolveTranslationStanding(Other, Actor, RoleSet(Roles.Translator)));
        Assert.Null(BoardTranslationPolicy.ResolveTranslationStanding(Other, Actor, RoleSet(Roles.Member)));
    }

    // ── PageTranslationPolicy (ADR 0040 / ADR 0161) ───────────────────────

    private static Pages.Page MakePage(Pages.PageKind kind, string authorId)
        => new Pages.Page { Kind = kind, AuthorId = authorId, Title = "t", Body = "b" };

    [Fact]
    public void Page_CanTranslate_AuthorOfBlogPage_Allows()
        => Assert.True(PageTranslationPolicy.CanTranslatePage(
            Actor, RoleSet(Roles.Member), MakePage(Pages.PageKind.User, Actor)));

    [Fact]
    public void Page_CanTranslate_AuthorOfSystemPage_Denies()
        => Assert.False(PageTranslationPolicy.CanTranslatePage(
            Actor, RoleSet(Roles.Member), MakePage(Pages.PageKind.System, Actor)));

    [Fact]
    public void Page_CanTranslate_Translator_Allows_AnyKind()
    {
        Assert.True(PageTranslationPolicy.CanTranslatePage(
            Actor, RoleSet(Roles.Translator), MakePage(Pages.PageKind.System, Other)));
        Assert.True(PageTranslationPolicy.CanTranslatePage(
            Actor, RoleSet(Roles.Translator), MakePage(Pages.PageKind.User, Other)));
    }

    [Fact]
    public void Page_CanTranslate_GlobalAdmin_Allows_AnyKind()
    {
        Assert.True(PageTranslationPolicy.CanTranslatePage(
            Actor, RoleSet(Roles.GlobalAdmin), MakePage(Pages.PageKind.System, Other)));
        Assert.True(PageTranslationPolicy.CanTranslatePage(
            Actor, RoleSet(Roles.GlobalAdmin), MakePage(Pages.PageKind.User, Other)));
    }

    [Fact]
    public void Page_CanTranslate_PlainMember_Denies()
        => Assert.False(PageTranslationPolicy.CanTranslatePage(
            Actor, RoleSet(Roles.Member), MakePage(Pages.PageKind.System, Other)));

    [Fact]
    public void Page_CanTranslate_NullPageOrBlankActor_Denies()
    {
        Assert.False(PageTranslationPolicy.CanTranslatePage(Actor, RoleSet(Roles.GlobalAdmin), null));
        Assert.False(PageTranslationPolicy.CanTranslatePage("", RoleSet(Roles.GlobalAdmin),
            MakePage(Pages.PageKind.System, Other)));
    }

    // ── ProjectTranslationPolicy (ADR 0088) ───────────────────────────────

    [Fact]
    public void Project_CanAddTranslation_Author_Allows()
        => Assert.True(ProjectTranslationPolicy.CanAddProjectTranslation(Actor, Actor, RoleSet(Roles.Member)));

    [Fact]
    public void Project_CanAddTranslation_Translator_Allows()
        => Assert.True(ProjectTranslationPolicy.CanAddProjectTranslation(Other, Actor, RoleSet(Roles.Translator)));

    [Fact]
    public void Project_CanAddTranslation_GlobalAdmin_Allows()
        => Assert.True(ProjectTranslationPolicy.CanAddProjectTranslation(Other, Actor, RoleSet(Roles.GlobalAdmin)));

    [Fact]
    public void Project_CanAddTranslation_PlainMemberOfOtherProject_Denies()
        => Assert.False(ProjectTranslationPolicy.CanAddProjectTranslation(Other, Actor, RoleSet(Roles.Member)));

    [Fact]
    public void Project_ResolveTranslationStanding_DerivesAccessVia()
    {
        Assert.Equal(Authorization.AccessVia.Owner,
            ProjectTranslationPolicy.ResolveTranslationStanding(Actor, Actor, RoleSet(Roles.Member)));
        Assert.Equal(Authorization.AccessVia.Admin,
            ProjectTranslationPolicy.ResolveTranslationStanding(Other, Actor, RoleSet(Roles.Translator)));
        Assert.Null(ProjectTranslationPolicy.ResolveTranslationStanding(Other, Actor, RoleSet(Roles.Member)));
    }

    // ── TodoTranslationPolicy (ADR 0088) ─────────────────────────────────

    [Fact]
    public void Todo_CanAddTranslation_Author_Allows()
        => Assert.True(TodoTranslationPolicy.CanAddTodoTranslation(Actor, null, Actor, RoleSet(Roles.Member)));

    [Fact]
    public void Todo_CanAddTranslation_Assignee_Allows()
        => Assert.True(TodoTranslationPolicy.CanAddTodoTranslation(Other, Actor, Actor, RoleSet(Roles.Member)));

    [Fact]
    public void Todo_CanAddTranslation_Translator_Allows()
        => Assert.True(TodoTranslationPolicy.CanAddTodoTranslation(Other, null, Actor, RoleSet(Roles.Translator)));

    [Fact]
    public void Todo_CanAddTranslation_GlobalAdmin_Allows()
        => Assert.True(TodoTranslationPolicy.CanAddTodoTranslation(Other, null, Actor, RoleSet(Roles.GlobalAdmin)));

    [Fact]
    public void Todo_CanAddTranslation_PlainMember_Denies()
        => Assert.False(TodoTranslationPolicy.CanAddTodoTranslation(Other, null, Actor, RoleSet(Roles.Member)));

    [Fact]
    public void Todo_ResolveTranslationStanding_DerivesAccessVia()
    {
        Assert.Equal(Authorization.AccessVia.Owner,
            TodoTranslationPolicy.ResolveTranslationStanding(Actor, null, Actor, RoleSet(Roles.Member)));
        Assert.Equal(Authorization.AccessVia.Admin,
            TodoTranslationPolicy.ResolveTranslationStanding(Other, Actor, Actor, RoleSet(Roles.Member)));
        Assert.Equal(Authorization.AccessVia.Admin,
            TodoTranslationPolicy.ResolveTranslationStanding(Other, null, Actor, RoleSet(Roles.Translator)));
        Assert.Null(TodoTranslationPolicy.ResolveTranslationStanding(Other, null, Actor, RoleSet(Roles.Member)));
    }
}
