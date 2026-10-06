using System.Reflection;
using System.Security.Claims;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Projects;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Localization;
using Kumunita.Web.Models;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// M14 (ADR 0115) — the Events ↔ Projects interlock, the **three acceptance
/// tests** from the design doc's §gate (their names are the pinned contract,
/// verbatim) + the missing Web-side cross-surface read-shape pins. The Core
/// service-side pins (U01's four <c>ListTodosForEventAsync</c>, U02's three
/// <c>SetTodoEventAsync</c>, U05's four <c>TodoIcsWriter</c>) already live in
/// <c>Kumunita.Core.Tests</c> and are deliberately **not** duplicated here —
/// this file only pins the **Web boundary** the interlock lands on:
/// <list type="number">
/// <item><b>Closed loop</b> — the <c>set-event</c> write lane + both
///       directions of the read resolve (the to-do detail's event chip and the
///       event detail's "linked to-dos" section).</item>
/// <item><b>Handoff</b> — the write + read compose with **no new
///       authorization surface** (C-M14·4): the frozen <see cref="AccessAction"/>
///       surface is unchanged (only <c>Read</c> + <c>Moderate</c>), and
///       <c>EventId</c> is never a gate (C-M14·1) — it is a display / filter
///       association, resolved through the frozen read seams.</item>
/// <item><b>Part vs. whole</b> — an unreadable / absent / soft-deleted event
///       **omits** the chip (never a 404/403 for the to-do itself, and the
///       target's title / id are not leaked — C-M14·2).</item>
/// </list>
/// The two read lanes' detail actions open a real Marten session (the
/// <c>BoardItemPlacement</c> / <c>KanbanBoard</c> / <c>KanbanLane</c> read on
/// the to-do detail — Marten 9's <c>ToListAsync()</c> casts to the internal
/// <c>MartenLinqQueryable</c> and cannot be NSubstituted), so those directions
/// run a **real** scratch-Postgres <see cref="IDocumentStore"/> (the
/// <see cref="ProjectsControllerTests"/> / <see cref="PostgresFixture"/>
/// precedent). The event-detail + write-lane directions are pure NSubstitute.
/// </summary>
public class M14InterlockTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── The three §gate acceptance tests (the pinned names, verbatim) ────────

    /// <summary>
    /// **U07 acceptance 1 — Closed loop.** A to-do linked (via the
    /// <c>set-event</c> lane) to a visible event: the to-do detail shows the
    /// event chip **and** the event detail shows the to-do in its "linked
    /// to-dos" section — the linkage is symmetric and both directions resolve
    /// through the frozen read seams (the ADR 0115 D2 interlock).
    /// </summary>
    [Fact]
    public async Task Interlock_ClosedLoop_TodoAndEventSeeEachOther()
    {
        const string actor = "subj-m14-loop";
        const string todoId = "todo-m14";
        const string eventId = "event-m14";

        // ── Direction A: the write lane maps through (C-M14·3 shape) ─────────
        // The set-event POST hands the posted eventId verbatim to the frozen
        // seam and redirects back to the to-do (the write side of the loop).
        var writeProjects = Substitute.For<IProjectService>();
        writeProjects.SetTodoEventAsync(todoId, actor, Arg.Any<IReadOnlySet<string>>(), eventId, Arg.Any<CancellationToken>())
            .Returns(new TodoItem { Id = todoId, Title = "Water the tomatoes", AuthorId = actor, EventId = eventId });
        var writeEvents = Substitute.For<IEventService>();
        var writeController = BuildProjects(writeProjects, writeEvents, Substitute.For<IDocumentStore>(), actor);

        var writeResult = await writeController.TodoSetEvent(todoId, eventId);
        var redirect = Assert.IsType<RedirectResult>(writeResult);
        Assert.Equal($"/projects/todos/{todoId}", redirect.Url);
        await writeProjects.Received(1).SetTodoEventAsync(
            todoId, actor, Arg.Any<IReadOnlySet<string>>(), eventId, Arg.Any<CancellationToken>());

        // ── Direction B: the to-do detail's event chip (real store) ───────────
        var todo = new TodoItem
        {
            Id = todoId, Title = "Water the tomatoes", AuthorId = actor,
            EventId = eventId,
            Created = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero),
        };
        var projects = Substitute.For<IProjectService>();
        projects.GetTodoAsync(todoId, actor, Arg.Any<CancellationToken>())
            .Returns(new TodoDetailResult { Todo = todo, Subtasks = [] });
        projects.GetTodoTranslationsAsync(todoId).Returns(new List<TodoTranslation>());
        var ev = SampleEvent(eventId, actor);
        var events = Substitute.For<IEventService>();
        events.GetAsync(eventId, actor, Arg.Any<CancellationToken>()).Returns(ev);
        events.ListMineAsync(actor, Arg.Any<CancellationToken>()).Returns(new List<Event> { ev });

        var todoController = BuildProjects(projects, events, await BuildRealStoreAsync(), actor);
        var todoView = Assert.IsType<ViewResult>(await todoController.TodoDetail(todoId));
        var todoVm = Assert.IsType<TodoDetailViewModel>(todoView.ViewData.Model);
        Assert.Equal(eventId, todoVm.EventId);
        Assert.Equal(ev.Title, todoVm.EventTitle);
        Assert.Equal($"/events/{eventId}", todoVm.EventLinkPath);
        // The picker is seeded with the actor's own event and prefilled to it
        // (the set-event lane's affordance — the display surface of the chip).
        Assert.NotNull(todoVm.EventPicker);
        Assert.Equal(eventId, todoVm.EventPicker.CurrentEventId);

        // ── Direction C: the event detail's linked-to-dos section ─────────────
        var detailProjects = Substitute.For<IProjectService>();
        detailProjects.ListTodosForEventAsync(eventId, actor, 0, Arg.Any<CancellationToken>(), Arg.Any<int?>())
            .Returns(new TodoPage(new List<TodoItem> { todo }, false));
        var detailEvents = Substitute.For<IEventService>();
        detailEvents.GetAsync(eventId, actor, Arg.Any<CancellationToken>()).Returns(ev);
        detailEvents.GetEventTranslationsAsync(eventId).Returns(new List<EventTranslation>());
        var eventController = BuildEvent(detailEvents, detailProjects, actor);

        var eventView = Assert.IsType<ViewResult>(await eventController.Detail(eventId));
        var eventVm = Assert.IsType<EventDetailViewModel>(eventView.ViewData.Model);
        var row = Assert.Single(eventVm.LinkedTodos!);
        Assert.Equal(todoId, row.TodoId);
        Assert.Equal(todo.Title, row.Title);
        Assert.Equal($"/projects/todos/{todoId}", row.LinkPath);
        await detailProjects.Received(1).ListTodosForEventAsync(eventId, actor, 0, Arg.Any<CancellationToken>(), Arg.Any<int?>());
    }

    /// <summary>
    /// **U07 acceptance 2 — Handoff.** The <c>set-event</c> write + the
    /// both-direction read compose with **no new authorization surface**
    /// (C-M14·4) — the frozen <see cref="AccessAction"/> surface is unchanged
    /// (exactly <c>Read</c> + <c>Moderate</c>), and <c>EventId</c> is never a
    /// gate (C-M14·1): it rides the frozen read seams, not a decision branch.
    /// </summary>
    [Fact]
    public async Task Interlock_Handoff_WriteAndReadComposeWithoutNewAuthorizationSurface()
    {
        const string actor = "subj-m14-handoff";
        const string todoId = "todo-m14";
        const string eventId = "event-m14";

        // (1) The frozen authorization surface is the closed two-member set —
        // C-M14·4 ("no new authorization surface") pins that M14 added **no**
        // new <see cref="AccessAction"/> gate. The <c>AccessVia</c> enum
        // (the audit-row "via" tag, why a decision came out the way it did)
        // pre-dates M14 (the M3 / M5 audit contract, <c>Decision.cs</c>) and is
        // not a gate — M14 adds no value to it either; the load-bearing pin is
        // that the **decision-action** set is exactly the frozen Read + Moderate.
        var actionFields = typeof(AccessAction)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => f.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(new[] { "Moderate", "Read" }, actionFields);
        Assert.Equal("read", AccessAction.Read.Id);
        Assert.Equal("moderate", AccessAction.Moderate.Id);

        // (2) The write lane: a blank post clears (posts null), a value sets —
        // the seam is the standing authority; the controller does no standing
        // math (C-M14·4). The write resolves through the seam, not a gate.
        var projects = Substitute.For<IProjectService>();
        projects.SetTodoEventAsync(todoId, actor, Arg.Any<IReadOnlySet<string>>(), eventId, Arg.Any<CancellationToken>())
            .Returns(new TodoItem { Id = todoId, Title = "Set up the bench", AuthorId = actor, EventId = eventId });
        var events = Substitute.For<IEventService>();
        var setController = BuildProjects(projects, events, Substitute.For<IDocumentStore>(), actor);
        Assert.IsType<RedirectResult>(await setController.TodoSetEvent(todoId, eventId));
        await projects.Received(1).SetTodoEventAsync(
            todoId, actor, Arg.Any<IReadOnlySet<string>>(), eventId, Arg.Any<CancellationToken>());

        // A blank post is a **clear** (null) — the same seam, the inverse write.
        projects.ClearReceivedCalls();
        projects.SetTodoEventAsync(todoId, actor, Arg.Any<IReadOnlySet<string>>(), null, Arg.Any<CancellationToken>())
            .Returns(new TodoItem { Id = todoId, Title = "Set up the bench", AuthorId = actor, EventId = null });
        Assert.IsType<RedirectResult>(await setController.TodoSetEvent(todoId, null));
        await projects.Received(1).SetTodoEventAsync(
            todoId, actor, Arg.Any<IReadOnlySet<string>>(), null, Arg.Any<CancellationToken>());

        // (3) The both-direction read composes over the same frozen seams —
        // EventId is carried as a **field** and resolved through the read
        // lanes; it is never consulted as an authorization input.
        var ev = SampleEvent(eventId, actor);
        var readProjects = Substitute.For<IProjectService>();
        readProjects.GetTodoAsync(todoId, actor, Arg.Any<CancellationToken>())
            .Returns(new TodoDetailResult
            {
                Todo = new TodoItem { Id = todoId, Title = "Set up the bench", AuthorId = actor, EventId = eventId,
                    Created = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero) },
                Subtasks = [],
            });
        readProjects.GetTodoTranslationsAsync(todoId).Returns(new List<TodoTranslation>());
        var readEvents = Substitute.For<IEventService>();
        readEvents.GetAsync(eventId, actor, Arg.Any<CancellationToken>()).Returns(ev);
        readEvents.ListMineAsync(actor, Arg.Any<CancellationToken>()).Returns(new List<Event> { ev });
        var readController = BuildProjects(readProjects, readEvents, await BuildRealStoreAsync(), actor);
        var view = Assert.IsType<ViewResult>(await readController.TodoDetail(todoId));
        var vm = Assert.IsType<TodoDetailViewModel>(view.ViewData.Model);
        Assert.Equal(eventId, vm.EventId); // the association rides the read, not a decision
    }

    /// <summary>
    /// **U07 acceptance 3 — Part vs. whole.** The interlock degrades gracefully
    /// (F5): an event the actor cannot read (denied), an absent event
    /// (soft-deleted), or a soft-deleted row **omits** the chip — never a
    /// 404/403 for the to-do itself, and the target's title / id are not
    /// leaked (C-M14·2). The to-do detail is still a clean 200 in every case.
    /// </summary>
    [Fact]
    public async Task Interlock_PartVsWhole_UnreadableEventOmitsTheChip()
    {
        const string actor = "subj-m14-part";
        const string todoId = "todo-m14";
        const string eventId = "event-m14";

        TodoDetailResult Detail(string? storedEventId) => new()
        {
            Todo = new TodoItem
            {
                Id = todoId, Title = "Paint the fence", AuthorId = actor,
                EventId = storedEventId,
                Created = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero),
            },
            Subtasks = [],
        };

        // Case 1 — denied: the seam 403s the event, but the to-do still renders.
        {
            var projects = Substitute.For<IProjectService>();
            projects.GetTodoAsync(todoId, actor, Arg.Any<CancellationToken>()).Returns(Detail(eventId));
            projects.GetTodoTranslationsAsync(todoId).Returns(new List<TodoTranslation>());
            var events = Substitute.For<IEventService>();
            events.GetAsync(eventId, actor, Arg.Any<CancellationToken>())
                .Returns(Task.FromException<Event>(new UnauthorizedAccessException("denied")));
            var controller = BuildProjects(projects, events, await BuildRealStoreAsync(), actor);

            var view = Assert.IsType<ViewResult>(await controller.TodoDetail(todoId));
            var vm = Assert.IsType<TodoDetailViewModel>(view.ViewData.Model);
            Assert.Null(vm.EventId);
            Assert.Null(vm.EventTitle);
            Assert.Null(vm.EventLinkPath);
        }

        // Case 2 — absent: the seam 404s the event, but the to-do still renders.
        {
            var projects = Substitute.For<IProjectService>();
            projects.GetTodoAsync(todoId, actor, Arg.Any<CancellationToken>()).Returns(Detail(eventId));
            projects.GetTodoTranslationsAsync(todoId).Returns(new List<TodoTranslation>());
            var events = Substitute.For<IEventService>();
            events.GetAsync(eventId, actor, Arg.Any<CancellationToken>())
                .Returns(Task.FromException<Event>(new KeyNotFoundException("gone")));
            var controller = BuildProjects(projects, events, await BuildRealStoreAsync(), actor);

            var view = Assert.IsType<ViewResult>(await controller.TodoDetail(todoId));
            var vm = Assert.IsType<TodoDetailViewModel>(view.ViewData.Model);
            Assert.Null(vm.EventId);
            Assert.Null(vm.EventTitle);
            Assert.Null(vm.EventLinkPath);
        }

        // Case 3 — soft-deleted: the seam returns the row with IsDeleted set;
        // the chip is omitted and the title / id are not leaked.
        {
            var ev = SampleEvent(eventId, actor);
            ev.IsDeleted = true;
            var projects = Substitute.For<IProjectService>();
            projects.GetTodoAsync(todoId, actor, Arg.Any<CancellationToken>()).Returns(Detail(eventId));
            projects.GetTodoTranslationsAsync(todoId).Returns(new List<TodoTranslation>());
            var events = Substitute.For<IEventService>();
            events.GetAsync(eventId, actor, Arg.Any<CancellationToken>()).Returns(ev);
            var controller = BuildProjects(projects, events, await BuildRealStoreAsync(), actor);

            var view = Assert.IsType<ViewResult>(await controller.TodoDetail(todoId));
            var vm = Assert.IsType<TodoDetailViewModel>(view.ViewData.Model);
            Assert.Null(vm.EventId);
            Assert.Null(vm.EventTitle);
            Assert.Null(vm.EventLinkPath);
        }

        // Case 4 — no association at all: the seam is never consulted.
        {
            var projects = Substitute.For<IProjectService>();
            projects.GetTodoAsync(todoId, actor, Arg.Any<CancellationToken>()).Returns(Detail(null));
            projects.GetTodoTranslationsAsync(todoId).Returns(new List<TodoTranslation>());
            var events = Substitute.For<IEventService>();
            var controller = BuildProjects(projects, events, await BuildRealStoreAsync(), actor);

            var view = Assert.IsType<ViewResult>(await controller.TodoDetail(todoId));
            var vm = Assert.IsType<TodoDetailViewModel>(view.ViewData.Model);
            Assert.Null(vm.EventId);
            Assert.Null(vm.EventTitle);
            Assert.Null(vm.EventLinkPath);
            await events.DidNotReceiveWithAnyArgs().GetAsync(default!, default!, default!);
        }
    }

    // ── Scaffolding ──────────────────────────────────────────────────────────

    /// <summary>
    /// A minimal <see cref="Event"/> for the interlock's read seams (the
    /// <see cref="EventControllerTests.SampleEvent"/> shape) — a readable,
    /// non-draft event owned by the actor so the chip + linked-to-dos resolve.
    /// </summary>
    private static Event SampleEvent(string id, string authorId, string title = "Cleanup day")
    {
        var now = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        return new Event
        {
            Id = id,
            Title = title,
            Body = "Bring gloves.",
            AuthorId = authorId,
            Start = now.AddHours(48),
            End = now.AddHours(52),
            Location = "Common shed",
            IsDraft = false,
            IsDeleted = false,
            Created = now,
        };
    }

    /// <summary>
    /// Boots a real Marten <see cref="IDocumentStore"/> over a fresh scratch
    /// Postgres database (the <see cref="ProjectsControllerTests"/>
    /// precedent) so the to-do detail read lane's
    /// <c>BoardItemPlacement</c> / <c>KanbanBoard</c> / <c>KanbanLane</c>
    /// queries have their schema.
    /// </summary>
    private async Task<IDocumentStore> BuildRealStoreAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);

        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
            M3DocTypes.Configure(opts);
            M4DocTypes.Configure(opts);
            M5DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);
        return store;
    }

    /// <summary>
    /// Builds a <see cref="ProjectsController"/> over NSubstitute seams with the
    /// M14 **optional event seam** injected (the shared
    /// <c>ProjectsControllerTests.Build</c> does not pass it, so the chip block
    /// would be skipped — this one supplies it to exercise the interlock). The
    /// <c>store</c> is either a real scratch-Postgres store (the detail read
    /// lane) or a plain substitute (the write lane, which never opens a session).
    /// </summary>
    private static ProjectsController BuildProjects(
        IProjectService projects,
        IEventService events,
        IDocumentStore store,
        string subjectId)
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync(Arg.Any<string>()).Returns((Profile?)null);
        userInfo.GetComponentsAsync(true).Returns(new List<Component>());
        userInfo.GetProfilesAsync(true).Returns(new List<Profile>());
        userInfo.GetPublicGroupsAsync().Returns(new List<Group>());
        userInfo.GetGroupIdsAsync(Arg.Any<string>()).Returns(new HashSet<string>(StringComparer.Ordinal));
        userInfo.GetCommunityIdsAsync(Arg.Any<string>()).Returns(Array.Empty<string>());

        var localization = Substitute.For<ILocalizationService>();
        localization.ListLanguagesAsync().Returns(new List<LanguageCatalog>
        {
            new() { Id = "en", NativeName = "English", Enabled = true, SortOrder = 1 },
        });
        localization.GetDefaultLanguageCodeAsync().Returns("en");

        var controller = new ProjectsController(
            projects, userInfo, localization, store, events: events);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var claims = new List<Claim> { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, subjectId) };
        controller.ControllerContext.HttpContext.User =
            new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));

        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());
        return controller;
    }

    /// <summary>
    /// Builds an <see cref="EventController"/> over NSubstitute seams with the
    /// M14 **optional project seam** injected (the shared
    /// <c>EventControllerTests.Build</c> does not pass it, so the
    /// linked-to-dos section would be skipped — this one supplies it). The
    /// event-detail read lane never opens a store session, so a plain
    /// substitute suffices.
    /// </summary>
    private static EventController BuildEvent(
        IEventService events,
        IProjectService projects,
        string subjectId)
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync(Arg.Any<string>()).Returns((Profile?)null);
        userInfo.GetComponentsAsync(true).Returns(new List<Component>());
        userInfo.GetProfilesAsync(true).Returns(new List<Profile>());
        userInfo.GetPublicGroupsAsync().Returns(new List<Group>());

        var localization = Substitute.For<ILocalizationService>();
        localization.ListLanguagesAsync().Returns(new List<LanguageCatalog>());
        localization.GetDefaultLanguageCodeAsync().Returns("en");
        var timezone = new EffectiveTimezoneResolver(userInfo, localization, new HttpContextAccessor());

        var controller = new EventController(
            events, userInfo, localization, Substitute.For<IDocumentStore>(), timezone,
            projects: projects);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var claims = new List<Claim> { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, subjectId) };
        controller.ControllerContext.HttpContext.User =
            new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));

        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());
        return controller;
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect / the chip shape, not the bag
        }
    }
}
