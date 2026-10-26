using Kumunita.Core;
using Kumunita.Core.Events;
using Kumunita.Core.Projects;
using Kumunita.Core.Query;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Security.Claims;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// The home "upcoming" section (the resident's <c>Profile.HomeShowUpcoming</c>
/// preference): the <see cref="HomeController.Index"/> view model reflects
/// the preference + the events/todos read seams.
/// </summary>
/// <para>
/// <b>Test model:</b> direct-construction (NSubstitute <c>IUserInfoService</c>
/// + <c>IEventService</c> + <c>IProjectService</c> seams, the
/// <see cref="HomeControllerSiteContentTests"/> shape). The view model's
/// <c>Upcoming</c> field is the assertion target — the Razor view's
/// <c>@if (Model.Upcoming is { } ...)</c> wrap is a pure server-side
/// conditional over that field, so asserting the model is the authoritative
/// pin.
/// </para>
public class HomeControllerUpcomingTests
{
    private static (HomeController controller, IUserInfoService userInfo, IEventService events, IProjectService projects) Build(
        bool homeShowUpcoming,
        bool homeUpcomingMineOnly = false,
        IReadOnlyList<Event> events = default!,
        IReadOnlyList<Event> mineEvents = default!,
        IReadOnlyList<TodoItem> todos = default!)
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync(Arg.Any<string>())
            .Returns(Task.FromResult<Profile?>(new Profile
            {
                SubjectId = "u08-hsu-01",
                HomeShowUpcoming = homeShowUpcoming,
                HomeUpcomingMineOnly = homeUpcomingMineOnly,
            }));

        var eventsService = Substitute.For<IEventService>();
        eventsService.ListUpcomingAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<int>(),
            Arg.Any<CancellationToken>(), Arg.Any<SortSpec?>(), Arg.Any<int?>())
            .Returns(Task.FromResult(new EventPage(events, HasMore: false)));
        // EV-MINE (ADR 0065): the resident's own upcoming events — the
        // mine-scoped read (ADR 0163 D1a).
        eventsService.ListMineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mineEvents));
        eventsService.GetEventTranslationsAsync(Arg.Any<string>())
            .Returns(Task.FromResult<IReadOnlyList<EventTranslation>>([]));

        var projectsService = Substitute.For<IProjectService>();
        projectsService.ListTodosAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<int>(),
            Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<bool>(),
            Arg.Any<CancellationToken>(), Arg.Any<SortSpec?>(), Arg.Any<int?>())
            .Returns(Task.FromResult(new TodoPage(todos, HasMore: false)));
        projectsService.GetTodoTranslationsAsync(Arg.Any<string>())
            .Returns(Task.FromResult<IReadOnlyList<TodoTranslation>>([]));

        var options = Options.Create(new CommunityOptions
        {
            Name = "Maplewood",
            SupportEmail = "maps@example.com",
        });

        var controller = new HomeController(
            NullLogger<HomeController>.Instance,
            options,
            userInfo: userInfo,
            events: eventsService,
            projects: projectsService);

        const string subjectId = "u08-hsu-01";
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[] { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, subjectId) },
                authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return (controller, userInfo, eventsService, projectsService);
    }

    // ── 1 — a fresh profile (HomeShowUpcoming = false) ⇒ Upcoming is null ─
    // A resident who has not opted in sees exactly the pre-feature home page
    // (the section is not in the DOM — the view's @if (Model.Upcoming is { })
    // wrap is the DOM-level pin).

    [Fact(DisplayName = "FreshProfile_HomeShowUpcomingFalse_UpcomingIsNull")]
    public async Task FreshProfile_HomeShowUpcomingFalse_UpcomingIsNull()
    {
        var (controller, _, events, projects) = Build(homeShowUpcoming: false);

        var result = await controller.Index();
        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<HomeViewModel>(view.ViewData.Model);

        Assert.Null(vm.Upcoming); // not in the DOM

        // The seams are not called (no wasted reads — the preference gate
        // runs before the feed reads).
        await events.DidNotReceive().ListUpcomingAsync(
            Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<int>(),
            Arg.Any<CancellationToken>(), Arg.Any<SortSpec?>(), Arg.Any<int?>());
        await projects.DidNotReceive().ListTodosAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<int>(),
            Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<bool>(),
            Arg.Any<CancellationToken>(), Arg.Any<SortSpec?>(), Arg.Any<int?>());
    }

    // ── 2 — HomeShowUpcoming = true ⇒ Upcoming is non-null, both columns ─
    // A resident who has opted in sees the events + todos columns (the view
    // renders them above the feed).

    [Fact(DisplayName = "HomeShowUpcomingTrue_UpcomingPresent_BothColumns")]
    public async Task HomeShowUpcomingTrue_UpcomingPresent_BothColumns()
    {
        var now = DateTimeOffset.UtcNow;
        var events = new List<Event>
        {
            new Event { Id = "ev1", Title = "Cleanup day", Start = now.AddHours(2), End = now.AddHours(5), Location = "The park" },
        };
        var todos = new List<TodoItem>
        {
            new TodoItem { Id = "td1", Title = "Buy compost", DueAt = now.AddDays(1) },
        };

        var (controller, _, evSvc, prSvc) = Build(homeShowUpcoming: true, events: events, todos: todos);

        var result = await controller.Index();
        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<HomeViewModel>(view.ViewData.Model);

        Assert.NotNull(vm.Upcoming);
        Assert.Single(vm.Upcoming!.Events);
        Assert.Single(vm.Upcoming.Todos);

        // The events column carries the event's Start + Location as the Meta.
        Assert.Equal("Cleanup day", vm.Upcoming.Events[0].Title);
        Assert.Equal(now.AddHours(2), vm.Upcoming.Events[0].When);
        Assert.Equal("The park", vm.Upcoming.Events[0].Meta);
        Assert.Equal("/events/ev1", vm.Upcoming.Events[0].Href);

        // The todos column carries the to-do's DueAt (no Status → Meta null).
        Assert.Equal("Buy compost", vm.Upcoming.Todos[0].Title);
        Assert.Equal(now.AddDays(1), vm.Upcoming.Todos[0].When);
        Assert.Null(vm.Upcoming.Todos[0].Meta); // no status set
        Assert.Equal("/projects/todos/td1", vm.Upcoming.Todos[0].Href);

        // Both seams were called (the read ran).
        await evSvc.Received(1).ListUpcomingAsync(
            Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<int>(),
            Arg.Any<CancellationToken>(), Arg.Any<SortSpec?>(), Arg.Any<int?>());
        await prSvc.Received(1).ListTodosAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<int>(),
            Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<bool>(),
            Arg.Any<CancellationToken>(), Arg.Any<SortSpec?>(), Arg.Any<int?>());
    }

    // ── 2b — HomeUpcomingMineOnly = true ⇒ mine-scoped reads ─────────────
    // The events column comes from ListMineAsync (the resident's own RSVP'd /
    // authored events — EV-MINE, ADR 0065), NOT ListUpcomingAsync (the
    // community feed); the to-dos column filters to the resident's
    // assigneeId (the "assigned to me" idiom). The section still renders from
    // the mine-scoped rows.

    [Fact(DisplayName = "HomeUpcomingMineOnlyTrue_MineScopedReads")]
    public async Task HomeUpcomingMineOnlyTrue_MineScopedReads()
    {
        var now = DateTimeOffset.UtcNow;
        const string subjectId = "u08-hsu-01";
        var communityEvents = new List<Event>
        {
            new Event { Id = "ev-comm", Title = "Community cleanup", Start = now.AddHours(2) },
        };
        var mineEvents = new List<Event>
        {
            new Event { Id = "ev-mine", Title = "My event", Start = now.AddHours(2), Location = "My place" },
        };
        var todos = new List<TodoItem>
        {
            new TodoItem { Id = "td-mine", Title = "My to-do", DueAt = now.AddDays(1) },
        };

        var (controller, _, evSvc, prSvc) = Build(
            homeShowUpcoming: true,
            homeUpcomingMineOnly: true,
            events: communityEvents, // should NOT be used
            mineEvents: mineEvents,   // the mine-scoped read
            todos: todos);

        var result = await controller.Index();
        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<HomeViewModel>(view.ViewData.Model);

        // The events column reflects the MINE row, not the community row.
        Assert.NotNull(vm.Upcoming);
        Assert.Single(vm.Upcoming!.Events);
        Assert.Equal("My event", vm.Upcoming.Events[0].Title);
        Assert.Equal("/events/ev-mine", vm.Upcoming.Events[0].Href);

        // The mine-scoped events read ran; the community feed read did not.
        await evSvc.Received(1).ListMineAsync(subjectId, Arg.Any<CancellationToken>());
        await evSvc.DidNotReceive().ListUpcomingAsync(
            Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<int>(),
            Arg.Any<CancellationToken>(), Arg.Any<SortSpec?>(), Arg.Any<int?>());

        // The to-dos read is scoped to the resident's assigneeId (subjectId).
        // Assert the exact call arguments from the captured call (NSubstitute
        // refuses to match a concrete value against an Arg.Any over the same
        // type — ListTodosAsync has four String params — so the captured-args
        // form is the unambiguous pin). ICall exposes GetMethodInfo() +
        // GetArguments().
        var todoCall = prSvc.ReceivedCalls()
            .FirstOrDefault(c => c.GetMethodInfo().Name == nameof(IProjectService.ListTodosAsync));
        Assert.NotNull(todoCall);
        var args = todoCall!.GetArguments();
        Assert.Null(args[0]);                    // componentId
        Assert.Equal(subjectId, args[1]);        // assigneeId = the subject
        Assert.Equal(subjectId, args[2]);        // actorId
        Assert.Null(args[5]);                    // projectId
    }

    // ── 3 — anonymous (no subject) ⇒ Upcoming is null ────────────────────
    // The upcoming section is a signed-in surface (the Profile read is the
    // subject's own); an anonymous visitor never sees their own feed.

    [Fact(DisplayName = "Anonymous_NoSubject_UpcomingIsNull")]
    public async Task Anonymous_NoSubject_UpcomingIsNull()
    {
        var (controller, userInfo, _, _) = Build(homeShowUpcoming: true);

        // Replace the context with an anonymous principal (no subject claim).
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.Index();
        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<HomeViewModel>(view.ViewData.Model);

        Assert.Null(vm.Upcoming); // anonymous → no upcoming section
    }
}
