using System.Security.Claims;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Xunit;
using Kumunita.Core.Events;

namespace Kumunita.Web.Tests;

/// <summary>
/// ADR 0089 (the <b>GE</b> group-events lane) — the Web-side 404 fail-closed
/// shapes for the <see cref="GroupsController"/> group-event actions (design
/// doc <c>group-events-design.md</c>: "The Web 404 tests live in
/// <c>Kumunita.Web.Tests</c> (the <c>GroupsController</c> group actions)").
/// <para>
/// Every denial (a non-member read, a non-author edit/publish, a missing
/// event, a lane mismatch) is a <b>404 <see cref="NotFoundResult"/></b> —
/// never a 403 (a 403 would advertise a gate the group lane does not expose;
/// the G·3/G·4 register pin, mirrored by <see cref="GroupsControllerTranslationTests"/>).
/// The group-lane membership decision and the author-only edit/publish gates
/// live in Core (<see cref="IEventService"/>); these tests pin the Web mapping
/// of that decision + the exception contract to the fail-closed shape, with
/// no write leaking through the deny path.
/// </para>
/// <para>
/// Harness mirrors <see cref="GroupsControllerTranslationTests"/>: NSubstitute
/// for the seams (<see cref="IUserInfoService"/> / <see cref="IEventService"/> /
/// <see cref="ILocalizationService"/> / <see cref="IDocumentStore"/>), a real
/// (sealed) <see cref="Kumunita.Core.Posts.PostService"/> for the constructor
/// shape (never invoked here), and a no-op <see cref="ITempDataProvider"/> to
/// close the write lane's TempData touch.
/// </para>
/// </summary>
public class GroupsControllerGroupEventTests
{
    private const string GroupId = "grp-ge";
    private const string EventId = "ev-ge";
    private const string Author = "subj-author";

    private static readonly Group GeGroup = new() { Id = GroupId, Name = "GE family", OwnerId = Author };

    [Fact]
    public async Task GroupEventDetail_NonMember_404()
    {
        var (controller, events, userInfo) = Build(subjectId: "subj-nonmember");
        userInfo.GetGroupAsync(GroupId).Returns(GeGroup);
        events.GetGroupEventAsync(GroupId, EventId, "subj-nonmember").Returns((Kumunita.Core.Events.Event?)null);

        var result = await controller.GroupEventDetail(GroupId, EventId);

        Assert.IsType<NotFoundResult>(result);
        // A non-member must not leak the author profile (the detail's read).
        await userInfo.DidNotReceive().GetProfileAsync(Author);
    }

    [Fact]
    public async Task GroupEventDetail_MissingGroup_404()
    {
        var (controller, events, userInfo) = Build(subjectId: Author);
        userInfo.GetGroupAsync(GroupId).Returns((Group?)null);

        var result = await controller.GroupEventDetail(GroupId, EventId);

        Assert.IsType<NotFoundResult>(result);
        await events.DidNotReceive().GetGroupEventAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task GroupEventDetail_Author_HappyPath_RendersEventDetail()
    {
        var (controller, events, userInfo) = Build(subjectId: Author);
        userInfo.GetGroupAsync(GroupId).Returns(GeGroup);
        var ev = new Event
        {
            Id = EventId, AuthorId = Author, GroupId = GroupId,
            ComponentId = string.Empty, Audience = new Kumunita.Core.Authorization.Audience(),
            Title = "T", Body = "b", IsDraft = false,
            Start = DateTimeOffset.UtcNow, End = DateTimeOffset.UtcNow.AddHours(1),
        };
        events.GetGroupEventAsync(GroupId, EventId, Author).Returns(ev);
        userInfo.GetProfileAsync(Author).Returns((Kumunita.Core.UserInfo.Profile?)null);
        events.GetMyRsvpAsync(EventId, Author, Arg.Any<CancellationToken>()).Returns((EventRsvp?)null);
        events.GetRsvpsAsync(EventId, Arg.Any<CancellationToken>()).Returns(new List<EventRsvp>());

        var result = await controller.GroupEventDetail(GroupId, EventId);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("EventDetail", view.ViewName);
        var vm = Assert.IsType<GroupEventDetailViewModel>(view.ViewData.Model);
        Assert.True(vm.IsAuthor);
        Assert.Equal(EventId, vm.Event.Id);
    }

    [Fact]
    public async Task CreateGroupEvent_NonMember_404_NoWrite()
    {
        var (controller, events, userInfo) = Build(subjectId: "subj-nonmember");
        userInfo.GetGroupAsync(GroupId).Returns(GeGroup);
        events.CreateGroupEventAsync(
            Arg.Any<GroupEventDraft>(), Arg.Any<string>(), Arg.Any<IDocumentSession>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Event>(new UnauthorizedAccessException("Denied.")));

        var model = ValidCompose();

        var result = await controller.CreateGroupEvent(GroupId, model);

        Assert.IsType<NotFoundResult>(result);
        // The gate's Deny row was persisted Core-side (GE6 FACES); the Web
        // simply maps the throw to a 404 and never redirects (no leak).
        await events.Received(1).CreateGroupEventAsync(
            Arg.Any<GroupEventDraft>(), Arg.Any<string>(), Arg.Any<IDocumentSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateGroupEvent_MissingGroup_404_NoWrite()
    {
        var (controller, events, userInfo) = Build(subjectId: Author);
        userInfo.GetGroupAsync(GroupId).Returns((Group?)null);

        var result = await controller.CreateGroupEvent(GroupId, ValidCompose());

        Assert.IsType<NotFoundResult>(result);
        await events.DidNotReceive().CreateGroupEventAsync(
            Arg.Any<GroupEventDraft>(), Arg.Any<string>(), Arg.Any<IDocumentSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateGroupEvent_Author_HappyPath_Redirects()
    {
        var (controller, events, userInfo) = Build(subjectId: Author);
        userInfo.GetGroupAsync(GroupId).Returns(GeGroup);
        var created = new Event
        {
            Id = "ev-created", AuthorId = Author, GroupId = GroupId,
            ComponentId = string.Empty, Audience = new Kumunita.Core.Authorization.Audience(),
            Title = "New", Body = "body", IsDraft = true,
            Start = DateTimeOffset.UtcNow, End = DateTimeOffset.UtcNow.AddHours(1),
        };
        events.CreateGroupEventAsync(
            Arg.Any<GroupEventDraft>(), Arg.Any<string>(), Arg.Any<IDocumentSession>(), Arg.Any<CancellationToken>())
            .Returns(created);

        var result = await controller.CreateGroupEvent(GroupId, ValidCompose());

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal($"/groups/{GroupId}/events/ev-created", redirect.Url);
    }

    [Fact]
    public async Task EditGroupEvent_NonAuthor_404_NoWrite()
    {
        // The actor is a member (so GetGroupEventAsync returns the event) but
        // not the author → the controller's author gate is a 404 (GE·4).
        var (controller, events, userInfo) = Build(subjectId: "subj-member");
        userInfo.GetGroupAsync(GroupId).Returns(GeGroup);
        var ev = new Event
        {
            Id = EventId, AuthorId = Author, GroupId = GroupId,
            Title = "T", Body = "b",
            Start = DateTimeOffset.UtcNow, End = DateTimeOffset.UtcNow.AddHours(1),
        };
        events.GetGroupEventAsync(GroupId, EventId, "subj-member").Returns(ev);

        var result = await controller.EditGroupEvent(GroupId, EventId);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EditGroupEvent_Post_NonAuthor_404_NoWrite()
    {
        var (controller, events, userInfo) = Build(subjectId: "subj-member");
        userInfo.GetGroupAsync(GroupId).Returns(GeGroup);
        events.UpdateGroupEventAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<GroupEventUpdate>(),
            Arg.Any<IDocumentSession>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Event>(new UnauthorizedAccessException("Denied.")));

        var result = await controller.EditGroupEvent(GroupId, EventId, ValidCompose());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EditGroupEvent_Post_MissingOrNonGroup_404()
    {
        var (controller, events, userInfo) = Build(subjectId: Author);
        userInfo.GetGroupAsync(GroupId).Returns(GeGroup);
        events.UpdateGroupEventAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<GroupEventUpdate>(),
            Arg.Any<IDocumentSession>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Event>(new KeyNotFoundException("No row.")));

        var result = await controller.EditGroupEvent(GroupId, EventId, ValidCompose());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task PublishGroupEvent_NonMember_404_NoPublish()
    {
        var (controller, events, userInfo) = Build(subjectId: "subj-nonmember");
        userInfo.GetGroupAsync(GroupId).Returns(GeGroup);
        events.GetGroupEventAsync(GroupId, EventId, "subj-nonmember").Returns((Kumunita.Core.Events.Event?)null);

        var result = await controller.PublishGroupEvent(GroupId, EventId);

        Assert.IsType<NotFoundResult>(result);
        await events.DidNotReceive().PublishAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishGroupEvent_NonAuthor_404_NoPublish()
    {
        // A member who is not the author: the decision returns the event, but
        // the author-only publish lane throws → 404 (GE·4, no GlobalAdmin skip).
        var (controller, events, userInfo) = Build(subjectId: "subj-member");
        userInfo.GetGroupAsync(GroupId).Returns(GeGroup);
        events.GetGroupEventAsync(GroupId, EventId, "subj-member").Returns(new Event
        {
            Id = EventId, AuthorId = Author, GroupId = GroupId,
            Title = "T", Body = "b", IsDraft = true,
            Start = DateTimeOffset.UtcNow, End = DateTimeOffset.UtcNow.AddHours(1),
        });
        events.PublishAsync(EventId, "subj-member", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Event>(new UnauthorizedAccessException("Non-author.")));

        var result = await controller.PublishGroupEvent(GroupId, EventId);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GroupEventRsvp_NonMember_404_NoRsvp()
    {
        var (controller, events, userInfo) = Build(subjectId: "subj-nonmember");
        userInfo.GetGroupAsync(GroupId).Returns(GeGroup);
        events.GetGroupEventAsync(GroupId, EventId, "subj-nonmember").Returns((Kumunita.Core.Events.Event?)null);

        var result = await controller.GroupEventRsvp(GroupId, EventId, RsvpStatus.Going);

        Assert.IsType<NotFoundResult>(result);
        await events.DidNotReceive().RsvpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RsvpStatus>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GroupEventRsvp_Member_HappyPath_Redirects()
    {
        var (controller, events, userInfo) = Build(subjectId: "subj-member");
        userInfo.GetGroupAsync(GroupId).Returns(GeGroup);
        events.GetGroupEventAsync(GroupId, EventId, "subj-member").Returns(new Event
        {
            Id = EventId, AuthorId = Author, GroupId = GroupId,
            Title = "T", Body = "b", IsDraft = false,
            Start = DateTimeOffset.UtcNow, End = DateTimeOffset.UtcNow.AddHours(1),
        });

        var result = await controller.GroupEventRsvp(GroupId, EventId, RsvpStatus.Maybe);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal($"/groups/{GroupId}/events/{EventId}", redirect.Url);
        await events.Received(1).RsvpAsync(EventId, "subj-member", RsvpStatus.Maybe, Arg.Any<CancellationToken>());
    }

    // ── Harness ─────────────────────────────────────────────────────────────

    private static (GroupsController controller, IEventService events, IUserInfoService userInfo)
        Build(string? subjectId = null)
    {
        var userInfo = Substitute.For<IUserInfoService>();
        // The sealed PostService is for the constructor shape only (the group-
        // name/post translation lanes are out of scope here).
        var posts = new Kumunita.Core.Posts.PostService(
            userInfo, Substitute.For<Kumunita.Core.Authorization.IAuthorizationService>(), Substitute.For<IDocumentStore>());

        var localization = Substitute.For<ILocalizationService>();
        localization.ListLanguagesAsync().Returns(new List<LanguageCatalog>());
        localization.GetDefaultLanguageCodeAsync().Returns("en");

        var store = Substitute.For<IDocumentStore>();
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        var events = Substitute.For<IEventService>();

        var controller = new GroupsController(userInfo, posts, localization, store, events);
        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        if (subjectId is not null)
        {
            var claims = new List<Claim>
            {
                new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, subjectId),
                new Claim(Kumunita.Core.Identity.ClaimTypes.Role, Roles.Member),
            };
            controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(claims, authenticationType: "test"));
        }

        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());
        return (controller, events, userInfo);
    }

    private static GroupEventComposeViewModel ValidCompose() => new()
    {
        Title = "A group event",
        Body = "body ge",
        Start = DateTimeOffset.UtcNow,
        End = DateTimeOffset.UtcNow.AddHours(2),
    };

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }
}
