using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.Kiosk;
using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
using Kumunita.Core.Projects;
using Kumunita.Core.Search;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Services;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// ADR 0302 D4 (IMPROVE-UX R17) — the shared-kiosk-link community-visible
/// filter in <see cref="KioskItemCatalog.ResolveAsync"/>. The R16
/// restricted principal carries the "kiosk-link" role value (and no
/// other), so the role's presence is the mode signal: when present, the
/// filter enforces "community-visible items only" for the wall viewer —
/// the owner's own audience grants and group memberships do NOT apply.
/// <para>
/// The DoD tests (plan-improve-ux.md, R17 row):
/// <list type="number">
/// <item><b>Group-private item → denied slide</b> (the plan card's
/// "Done when" verbatim): a <see cref="KioskItemType.GroupPost"/> in a
/// private group, resolved in kiosk-link mode → <see cref="KioskResolvedItem.Hidden"/>
/// → the existing <c>kiosk.slide_denied</c> shape.
/// <item><b>Community-visible item → visible</b>: a
/// <see cref="KioskItemType.Project"/> with community audience, resolved
/// in kiosk-link mode → <c>Visible = true</c>, <c>Href</c> set. (The
/// <see cref="KioskItemType.CommunityPost"/> analogue exercises the same
/// <c>KioskLinkAudienceIsCommunity</c> guard; the <c>PostService</c> is a
/// concrete class with non-virtual methods and is not substitutable, so
/// the <c>ProjectService</c> (<see cref="IProjectService"/>, an
/// interface) stands in — the audience guard code path is identical for
/// all Audience-bearing types.)
/// <item><b>Personal-kiosk unaffected</b>: the same group-private item,
/// resolved in personal-kiosk mode (the owner's <c>Member</c> role
/// present) → <c>Visible = true</c>.
/// <item><b>No-role item → denied</b>: a <see cref="KioskItemType.Todo"/>
/// with a grant-restricted audience (no community flag), resolved in
/// kiosk-link mode → denied.
/// <item><b>Announcement → visible</b>: an
/// <see cref="KioskItemType.Announcement"/> (community-wide by design —
/// <c>CommunityId = null</c>), resolved in kiosk-link mode → visible.
/// </list>
/// </para>
/// </summary>
public class KioskLinkCommunityFilterTests
{
    private const string Owner = "r17-owner";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IReadOnlySet<string> KioskLinkRoles() =>
        new HashSet<string> { Roles.KioskLink };

    private static IReadOnlySet<string> MemberRoles() =>
        new HashSet<string> { Roles.Member };

    // ── DoD 1: Group-private item → denied slide (the plan card verbatim) ──

    [Fact]
    public async Task GroupPrivate_GroupPost_KioskLinkMode_Hidden()
    {
        var h = H();
        h.groupPosts.GetGroupPostAsync("g-private", "gp-1", Owner)
            .Returns(new PostDetailResult(Post("gp-1", "g-private"), []));
        h.groupService.GetGroupAsync("g-private")
            .Returns(new Group { Id = "g-private", Name = "Private choir", IsPrivate = true });

        var result = await h.catalog.ResolveAsync(
            Entry(KioskItemType.GroupPost, "gp-1", "g-private"),
            Owner, KioskLinkRoles(), Ct);

        Assert.Same(KioskResolvedItem.Hidden, result);
    }

    // ── DoD 2: Community-visible item → visible ──

    [Fact]
    public async Task Project_CommunityAudience_KioskLinkMode_Visible()
    {
        var h = H();
        h.projects.GetProjectAsync("pr-1", Owner, Arg.Any<CancellationToken>())
            .Returns(new Project { Id = "pr-1", Title = "Community garden", Audience = CommunityAudience() });

        var result = await h.catalog.ResolveAsync(
            Entry(KioskItemType.Project, "pr-1"),
            Owner, KioskLinkRoles(), Ct);

        Assert.True(result.Visible);
        Assert.Equal("/projects/projects/pr-1", result.Href);
    }

    [Fact]
    public async Task Project_GrantRestrictedAudience_KioskLinkMode_Hidden()
    {
        var h = H();
        h.projects.GetProjectAsync("pr-2", Owner, Arg.Any<CancellationToken>())
            .Returns(new Project { Id = "pr-2", Title = "Restricted", Audience = GrantRestrictedAudience() });

        var result = await h.catalog.ResolveAsync(
            Entry(KioskItemType.Project, "pr-2"),
            Owner, KioskLinkRoles(), Ct);

        Assert.Same(KioskResolvedItem.Hidden, result);
    }

    // ── DoD 3: Personal-kiosk unaffected (ADR 0302 D5) ──

    [Fact]
    public async Task GroupPrivate_GroupPost_PersonalKiosk_Visible()
    {
        var h = H();
        h.groupPosts.GetGroupPostAsync("g-private", "gp-1", Owner)
            .Returns(new PostDetailResult(Post("gp-1", "g-private"), []));
        h.groupService.GetGroupAsync("g-private")
            .Returns(new Group { Id = "g-private", Name = "Private choir", IsPrivate = true });

        var result = await h.catalog.ResolveAsync(
            Entry(KioskItemType.GroupPost, "gp-1", "g-private"),
            Owner, MemberRoles(), Ct);

        Assert.True(result.Visible);
        Assert.NotNull(result.Href);
    }

    // ── DoD 4: No-role item → denied (grant-restricted audience) ──

    [Fact]
    public async Task Todo_GrantRestrictedAudience_KioskLinkMode_Hidden()
    {
        var h = H();
        h.todos.GetTodoAsync("t-1", Owner, Arg.Any<CancellationToken>())
            .Returns(new TodoDetailResult { Todo = new TodoItem { Id = "t-1", Title = "Bins", Audience = GrantRestrictedAudience() } });

        var result = await h.catalog.ResolveAsync(
            Entry(KioskItemType.Todo, "t-1"),
            Owner, KioskLinkRoles(), Ct);

        Assert.Same(KioskResolvedItem.Hidden, result);
    }

    [Fact]
    public async Task Board_CommunityAudience_KioskLinkMode_Visible()
    {
        var h = H();
        h.boards.GetBoardAsync("b-1", Owner, Arg.Any<CancellationToken>())
            .Returns(new BoardDetailResult { Board = new KanbanBoard { Id = "b-1", Title = "Main", Audience = CommunityAudience() } });

        var result = await h.catalog.ResolveAsync(
            Entry(KioskItemType.Board, "b-1"),
            Owner, KioskLinkRoles(), Ct);

        Assert.True(result.Visible);
        Assert.NotNull(result.Href);
    }

    // ── DoD 5: Announcement → visible (community-wide by design) ──

    [Fact]
    public async Task Announcement_CommunityWide_KioskLinkMode_Visible()
    {
        var h = H();
        h.announcements.GetAsync("a-1", Owner, Arg.Any<IReadOnlySet<string>>())
            .Returns(new Announcement
            {
                Id = "a-1", Title = "Street fair", Body = "Saturday",
                Scope = AnnouncementScope.Community, CommunityId = null,
            });

        var result = await h.catalog.ResolveAsync(
            Entry(KioskItemType.Announcement, "a-1"),
            Owner, KioskLinkRoles(), Ct);

        Assert.True(result.Visible);
        Assert.NotNull(result.Href);
    }

    [Fact]
    public async Task Announcement_TargetedCommunity_KioskLinkMode_Hidden()
    {
        var h = H();
        h.announcements.GetAsync("a-targeted", Owner, Arg.Any<IReadOnlySet<string>>())
            .Returns(new Announcement
            {
                Id = "a-targeted", Title = "Targeted", Body = "For Main street",
                Scope = AnnouncementScope.Community, CommunityId = "main-street",
            });

        var result = await h.catalog.ResolveAsync(
            Entry(KioskItemType.Announcement, "a-targeted"),
            Owner, KioskLinkRoles(), Ct);

        Assert.Same(KioskResolvedItem.Hidden, result);
    }

    // ── Group-event pin (the DoD 4 analogue on the Event type) ──

    [Fact]
    public async Task GroupEvent_PrivateGroup_KioskLinkMode_Hidden()
    {
        var h = H();
        h.events.GetGroupEventAsync("g-private", "e-1", Owner, Arg.Any<CancellationToken>())
            .Returns(new Event { Id = "e-1", Title = "Choir", GroupId = "g-private", IsDraft = false });
        h.groupService.GetGroupAsync("g-private")
            .Returns(new Group { Id = "g-private", Name = "Private choir", IsPrivate = true });

        var result = await h.catalog.ResolveAsync(
            Entry(KioskItemType.Event, "e-1", "g-private"),
            Owner, KioskLinkRoles(), Ct);

        Assert.Same(KioskResolvedItem.Hidden, result);
    }

    [Fact]
    public async Task CommunityEvent_GrantRestricted_KioskLinkMode_Hidden()
    {
        var h = H();
        h.events.GetAsync("e-restricted", Owner, Arg.Any<CancellationToken>())
            .Returns(new Event
            {
                Id = "e-restricted", Title = "Restricted", IsDraft = false,
                Audience = GrantRestrictedAudience(),
            });

        var result = await h.catalog.ResolveAsync(
            Entry(KioskItemType.Event, "e-restricted"),
            Owner, KioskLinkRoles(), Ct);

        Assert.Same(KioskResolvedItem.Hidden, result);
    }

    // ── The pure audience predicate (the shared guard's decision) ──

    [Fact]
    public void KioskLinkAudienceIsCommunity_Null_IsPublic()
        => Assert.True(KioskItemCatalog.KioskLinkAudienceIsCommunity(null));

    [Fact]
    public void KioskLinkAudienceIsCommunity_CommunityFlag_IsCommunity()
        => Assert.True(KioskItemCatalog.KioskLinkAudienceIsCommunity(new Audience { Community = true }));

    [Fact]
    public void KioskLinkAudienceIsCommunity_AllResidentsFlag_IsCommunity()
        => Assert.True(KioskItemCatalog.KioskLinkAudienceIsCommunity(new Audience { AllResidents = true }));

    [Fact]
    public void KioskLinkAudienceIsCommunity_GrantRestricted_IsDenied()
        => Assert.False(KioskItemCatalog.KioskLinkAudienceIsCommunity(
            new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, "somebody-else")])));

    // ── Harness ──

    /// <summary>
    /// The direct-construction harness (the <see cref="KioskItemCatalogTests"/>
    /// shape): every seam an NSubstitute mock; the concrete
    /// <see cref="PostService"/> a real instance over a no-op store (the
    /// <c>MyDraftsControllerTests</c> idiom — the <c>PostService</c> is a
    /// concrete class, not substitutable; the DoD's community-visible
    /// analogue uses the <see cref="IProjectService"/> interface instead).
    /// </summary>
    private sealed class Harness
    {
        public KioskItemCatalog catalog { get; }
        public IGroupPostService groupPosts { get; }
        public IGroupService groupService { get; }
        public IProjectService projects { get; }
        public ITodoService todos { get; }
        public IBoardService boards { get; }
        public IAnnouncementService announcements { get; }
        public IEventService events { get; }

        private Harness(
            KioskItemCatalog catalog, IGroupPostService groupPosts, IGroupService groupService,
            IProjectService projects, ITodoService todos, IBoardService boards,
            IAnnouncementService announcements, IEventService events)
        {
            this.catalog = catalog;
            this.groupPosts = groupPosts;
            this.groupService = groupService;
            this.projects = projects;
            this.todos = todos;
            this.boards = boards;
            this.announcements = announcements;
            this.events = events;
        }

        public static Harness Make()
        {
            var search         = Substitute.For<ISearchService>();
            var userInfo       = Substitute.For<IUserInfoService>();
            var groupService   = Substitute.For<IGroupService>();
            var profileSettings = Substitute.For<IProfileSettingsService>();
            var events         = Substitute.For<IEventService>();
            var todos          = Substitute.For<ITodoService>();
            var boards         = Substitute.For<IBoardService>();
            var projects       = Substitute.For<IProjectService>();
            var pages          = Substitute.For<IPageService>();
            var announcements  = Substitute.For<IAnnouncementService>();
            var groupPosts     = Substitute.For<IGroupPostService>();
            var authz          = Substitute.For<IAuthorizationService>();
            var posts          = new PostService(
                Substitute.For<IUserInfoService>(),
                Substitute.For<IAuthorizationService>(),
                Substitute.For<Marten.IDocumentStore>());

            var catalog = new KioskItemCatalog(
                search, userInfo, groupService, profileSettings,
                projects: projects, boards: boards, todos: todos, events: events,
                pages: pages, announcements: announcements, posts: posts,
                authz: authz, groupPosts: groupPosts);

            return new Harness(catalog, groupPosts, groupService, projects, todos, boards, announcements, events);
        }
    }

    private static Harness H() => Harness.Make();

    // ── Fixtures ──

    private static KioskEntry Entry(KioskItemType type, string id, string? group = null)
        => new() { Type = type, ItemId = id, GroupId = group };

    private static Post Post(string id, string groupId)
        => new() { Id = id, Title = "T", GroupId = groupId };

    private static Audience CommunityAudience() => new() { Community = true };

    private static Audience GrantRestrictedAudience()
        => new(AudienceMode.Any, [new AudienceGrant(GrantKind.User, "somebody-else")]);
}
