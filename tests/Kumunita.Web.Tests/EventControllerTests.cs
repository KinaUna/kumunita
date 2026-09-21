using System.Security.Claims;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Localization;
using Kumunita.Web.Models;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// Unit tests for <see cref="EventController"/> (M4, ADR 0054). Mirrors the
/// <see cref="AnnouncementControllerTests"/> harness shape: the **frozen**
/// <see cref="IEventService"/> seam is substituted with NSubstitute (no live
/// Postgres, no <see cref="IDocumentStore"/> session — the M4 controller never
/// opens one itself; the service owns its own sessions, C3); <see
/// cref="IUserInfoService"/> + <see cref="ILocalizationService"/> are plain
/// substitutes for the read-lookup / picker-seeding lanes.
/// <para>
/// Pins this harness owns (the Web-side contract — the service-side decisions
/// are the U09 seam tests' job):
/// <list type="number">
/// <item><b>Route map</b> — the <c>/events</c> surface's documented routes
///       (feed / detail / new / edit / publish / delete / rsvp) are the exact
///       attribute-route strings (a route drift is a Web-contract break).</item>
/// <item><b>404-vs-403 split</b> (<c>KeyNotFoundException</c> →
///       <see cref="NotFoundResult"/>, <c>UnauthorizedAccessException</c> →
///       <see cref="ForbidResult"/>) — on the detail, the edit-lane shape
///       gates, and the publish / delete / rsvp POSTs: a missing event is a
///       clean 404, a denied actor is a clean 403 (never a 500, never a
///       swapped code).</item>
/// <item><b>Feed filter</b> — the <c>componentId</c> query is a *filter, never
///       a gate* (C-M3·2): passed to
///       <see cref="IEventService.ListUpcomingAsync"/> verbatim and carried on
///       the view model; the page number likewise.</item>
/// <item><b>Composer / edit audience round-trip</b> — the create lane seeds
///       the community-visible-by-default <see cref="AudienceEditorModel"/>
///       (ADR 0036) + the ADR 0037 save-as-draft floor; the create POST maps the
///       editor through the **one** <c>BuildAudience()</c> deserialization site
///       (+ the draft flag) into <see cref="CreateEventRequest"/>; the edit GET
///       round-trips the stored audience through the **one**
///       <c>FromAudience()</c> inverse; a malformed audience is a shape error
///       (re-render, no service call); the edit standing gates (author ∪
///       GlobalAdmin) 404 / 403 split.</item>
/// <item><b>RSVP surface</b> — the owner-only RSVP list is loaded for the
///       author only; the viewer's own RSVP is the <c>MyRsvp</c> read (null
///       when absent); the <c>POST /events/{id}/rsvp</c> lane passes the posted
///       <see cref="RsvpStatus"/> to the service verbatim.</item>
/// </list>
/// </summary>
public class EventControllerTests
{
    // ── Route map ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The <c>/events</c> surface's documented attribute routes are the exact
    /// strings (a route drift — e.g. <c>/event</c> or <c>/events/detail</c> — is
    /// a Web-contract break: the nav entry, the view links, and the
    /// <c>.http</c> fixtures all target these paths).
    /// </summary>
    [Fact]
    public void RouteMap_MatchesDocumentedSurface()
    {
        static string? Route(string actionName)
            => typeof(EventController)
                .GetMethod(actionName)!
                .GetCustomAttributes(typeof(HttpGetAttribute), inherit: true)
                .OfType<HttpGetAttribute>()
                .FirstOrDefault()?.Template
              ?? typeof(EventController)
                .GetMethod(actionName)!
                .GetCustomAttributes(typeof(HttpPostAttribute), inherit: true)
                .OfType<HttpPostAttribute>()
                .FirstOrDefault()?.Template;

        Assert.Equal("/events", Route("Index"));
        Assert.Equal("/events/{id}", Route("Detail"));
        Assert.Equal("/events/new", Route("CreateGet"));
        Assert.Equal("/events/new", Route("CreatePost"));
        Assert.Equal("/events/{id}/edit", Route("EditGet"));
        Assert.Equal("/events/{id}/edit", Route("EditPost"));
        Assert.Equal("/events/{id}/publish", Route("Publish"));
        Assert.Equal("/events/{id}/delete", Route("Delete"));
        Assert.Equal("/events/{id}/rsvp", Route("Rsvp"));
    }

    // ── 404-vs-403 split (detail + edit + write POSTs) ─────────────────────────

    /// <summary>
    /// <c>GET /events/{id}</c>: the service's
    /// <see cref="IEventService.GetAsync"/> reports the event as **absent**
    /// (<see cref="KeyNotFoundException"/>). The Web layer must map that to a
    /// clean <c>404</c> (<see cref="NotFoundResult"/>), not swallow it into a
    /// 500, and not report it as a denial (the 404-vs-403 split, C3).
    /// </summary>
    [Fact]
    public async Task Detail_When_ServiceReportsAbsent_Returns_404()
    {
        const string id = "ev-does-not-exist";
        var events = Substitute.For<IEventService>();
        events.GetAsync(id, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Event>(new KeyNotFoundException($"Event '{id}' was not found.")));
        var controller = Build(events, subjectId: "subj-resident-001");

        var result = await controller.Detail(id);

        Assert.IsType<NotFoundResult>(result);
        await events.Received(1).GetAsync(id, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// <c>GET /events/{id}</c>: the service's
    /// <see cref="IEventService.GetAsync"/> reports the event as **denied**
    /// (<see cref="UnauthorizedAccessException"/>). The Web layer must map that
    /// to a clean <c>403</c> (<see cref="ForbidResult"/>), not a 404 (the event
    /// exists — the caller simply may not read it) and not a 500. The
    /// announcement 404-vs-403 split, pinned at the Web boundary.
    /// </summary>
    [Fact]
    public async Task Detail_When_ServiceReportsDenied_Returns_403()
    {
        const string id = "ev-denied";
        var events = Substitute.For<IEventService>();
        events.GetAsync(id, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Event>(new UnauthorizedAccessException($"Actor may not read event '{id}'.")));
        var controller = Build(events, subjectId: "subj-resident-001");

        var result = await controller.Detail(id);

        Assert.IsType<ForbidResult>(result);
        await events.Received(1).GetAsync(id, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// <c>GET /events/{id}/edit</c>: the event is **absent** — the Web layer's
    /// shape gate maps the service's <see cref="KeyNotFoundException"/> to a
    /// clean <c>404</c> before any standing check (a missing event is a
    /// not-found, not a denial).
    /// </summary>
    [Fact]
    public async Task EditGet_When_ServiceReportsAbsent_Returns_404()
    {
        const string id = "ev-edit-missing";
        var events = Substitute.For<IEventService>();
        events.GetAsync(id, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Event>(new KeyNotFoundException($"Event '{id}' was not found.")));
        var controller = Build(events, subjectId: "subj-resident-001");

        var result = await controller.EditGet(id);

        Assert.IsType<NotFoundResult>(result);
    }

    /// <summary>
    /// <c>GET /events/{id}/edit</c>: the event exists but the viewer holds only
    /// the Member role and is **not** the author — the §3.4 standing matrix
    /// (author ∪ GlobalAdmin) refuses up front (the <see
    /// cref="EventService.CheckEditStanding"/> shape gate) with a clean
    /// <c>403</c> (<see cref="ForbidResult"/>), not a rendered form (a form a
    /// user cannot submit is not rendered in the first place) and not a 404
    /// (the event exists).
    /// </summary>
    [Fact]
    public async Task EditGet_When_NonAuthorNonAdmin_Returns_403()
    {
        const string id = "ev-edit-denied";
        var existing = SampleEvent(id, authorId: "subj-author-001", isDraft: true);
        var events = Substitute.For<IEventService>();
        events.GetAsync(id, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(existing);
        var controller = Build(events, roles: new[] { Roles.Member }, subjectId: "viewer-001");

        var result = await controller.EditGet(id);

        Assert.IsType<ForbidResult>(result);
    }

    /// <summary>
    /// <c>POST /events/{id}/publish</c>: the ADR 0037 author-only lane denies —
    /// the service's <see cref="IEventService.PublishAsync"/>
    /// <see cref="UnauthorizedAccessException"/> maps to a clean <c>403</c>
    /// (a non-author, even a GlobalAdmin, is denied the publish).
    /// </summary>
    [Fact]
    public async Task Publish_When_ServiceDenies_Returns_403()
    {
        const string id = "ev-pub-denied";
        var events = Substitute.For<IEventService>();
        events.PublishAsync(id, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Event>(new UnauthorizedAccessException("Only the author may publish an event.")));
        var controller = Build(events, roles: new[] { Roles.GlobalAdmin }, subjectId: "subj-admin-001");

        var result = await controller.Publish(id);

        Assert.IsType<ForbidResult>(result);
        await events.Received(1).PublishAsync(id, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// <c>POST /events/{id}/delete</c>: the standing check (the event's author
    /// is someone else, the viewer holds only Member) refuses the soft-delete
    /// up front — a clean <c>403</c>, and the <see
    /// cref="IEventService.DeleteAsync"/> write lane is **never** reached (the
    /// shape gate is before the service write, the C3 defense-in-depth pin).
    /// </summary>
    [Fact]
    public async Task Delete_When_NonAuthorNonAdmin_Returns_403_And_NeverCallsDeleteAsync()
    {
        const string id = "ev-del-denied";
        var existing = SampleEvent(id, authorId: "subj-author-001");
        var events = Substitute.For<IEventService>();
        events.GetAsync(id, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(existing);
        var controller = Build(events, roles: new[] { Roles.Member }, subjectId: "viewer-001");

        var result = await controller.Delete(id);

        Assert.IsType<ForbidResult>(result);
        await events.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// <c>POST /events/{id}/rsvp</c>: the service denies — a clean
    /// <c>403</c> (the RSVP is a routine resident action, not an access
    /// decision; the denial is the service's to raise and the Web's to map).
    /// </summary>
    [Fact]
    public async Task Rsvp_When_ServiceDenies_Returns_403()
    {
        const string id = "ev-rsvp-denied";
        var events = Substitute.For<IEventService>();
        events.RsvpAsync(id, Arg.Any<string>(), Arg.Any<RsvpStatus>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<EventRsvp>(new UnauthorizedAccessException($"Actor may not RSVP to event '{id}'.")));
        var controller = Build(events, subjectId: "subj-resident-001");

        var result = await controller.Rsvp(id, RsvpStatus.Going);

        Assert.IsType<ForbidResult>(result);
        await events.Received(1).RsvpAsync(id, Arg.Any<string>(), Arg.Any<RsvpStatus>(), Arg.Any<CancellationToken>());
    }

    // ── Feed (GET /events) ─────────────────────────────────────────────────────

    /// <summary>
    /// The <c>componentId</c> query is a *filter, never a gate* (C-M3·2): the
    /// controller passes it to
    /// <see cref="IEventService.ListUpcomingAsync"/> verbatim (plus the page
    /// number + the caller's subject id — the only Web-side read decisions) and
    /// carries it on the view model's <c>CurrentComponentId</c>. A filter
    /// regression (a dropped component id, a swapped gate) would change *which*
    /// events the feed shows without changing any audience row.
    /// </summary>
    [Fact]
    public async Task Index_PassesComponentFilterAndPage_Verbatim_ToService()
    {
        var events = Substitute.For<IEventService>();
        events.ListUpcomingAsync("component-A", "subj-resident-001", 2, Arg.Any<CancellationToken>())
            .Returns(new List<Event> { SampleEvent("ev-1", authorId: "subj-author-001", componentId: "component-A") });

        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync("subj-author-001").Returns((Profile?)new Profile { SubjectId = "subj-author-001", DisplayName = "Ada" });
        userInfo.GetComponentsAsync(true).Returns(new List<Component> { new() { Id = "component-A", Name = "Community A" } });

        var controller = Build(events, userInfo: userInfo, subjectId: "subj-resident-001");

        var result = (await controller.Index(componentId: "component-A", page: 2)) as ViewResult;

        Assert.NotNull(result);
        var model = Assert.IsType<EventIndexViewModel>(result!.ViewData.Model);
        Assert.Equal("component-A", model.CurrentComponentId);
        Assert.Equal(2, model.CurrentPage);
        Assert.Single(model.Events);
        Assert.Equal("Community A", model.Events[0].ComponentDisplayName);
        Assert.Equal("Ada", model.Events[0].AuthorDisplayName);
        await events.Received(1).ListUpcomingAsync("component-A", "subj-resident-001", 2, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The feed's author display-name read is null-safe: a **missing** profile
    /// row (a profile deletion / seed-lane drift is normal operational state)
    /// falls back to the raw subject id on the
    /// <see cref="EventRow.AuthorDisplayName"/> cell — not a crash, not an
    /// empty string. The audience gate already ran in the service; the
    /// display-name lookup is never an access decision.
    /// </summary>
    [Fact]
    public async Task Index_When_AuthorProfileMissing_FallsBackToSubjectId()
    {
        var events = Substitute.For<IEventService>();
        events.ListUpcomingAsync(null, "subj-resident-001", 1, Arg.Any<CancellationToken>())
            .Returns(new List<Event> { SampleEvent("ev-1", authorId: "subj-ghost-001") });

        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync("subj-ghost-001").Returns((Profile?)null);
        userInfo.GetComponentsAsync(true).Returns(new List<Component>());

        var controller = Build(events, userInfo: userInfo, subjectId: "subj-resident-001");

        var result = (await controller.Index(componentId: null, page: 1)) as ViewResult;

        Assert.NotNull(result);
        var model = Assert.IsType<EventIndexViewModel>(result!.ViewData.Model);
        Assert.Equal("subj-ghost-001", model.Events[0].AuthorDisplayName);
    }

    // ── Composer (GET/POST /events/new) ────────────────────────────────────────

    /// <summary>
    /// <c>GET /events/new</c> seeds the composer's <see cref="AudienceEditorModel"/>
    /// with the ADR 0036 **community-visible-by-default** shape (Mode "Any",
    /// empty grants, <c>CommunityVisible = true</c>) — the sole access boundary
    /// on the form (the component picker is a filter, never a gate) — and the
    /// ADR 0037 **save-as-draft** floor (<c>SaveAsDraft = true</c>: a new event
    /// is a draft until its author publishes it) + the reminder opt-out floor
    /// (<c>ReminderEnabled = true</c>).
    /// </summary>
    [Fact]
    public async Task CreateGet_SeesCommunityVisibleDefault_Audience_AndDraftFloor()
    {
        var controller = Build(Substitute.For<IEventService>(), subjectId: "subj-resident-001");

        var result = (await controller.CreateGet()) as ViewResult;

        Assert.NotNull(result);
        var model = Assert.IsType<EventEditorModel>(result!.ViewData.Model);
        Assert.Equal("Any", model.Audience.Mode);
        Assert.True(model.Audience.CommunityVisible);
        Assert.True(model.Audience.IsValid);
        Assert.True(model.SaveAsDraft);     // ADR 0037 — a new event is a draft.
        Assert.True(model.ReminderEnabled); // §6.4 job opt-out floor.
    }


    /// <summary>
    /// <c>POST /events/new</c>, happy path: the form's <see
    /// cref="AudienceEditorModel"/> is mapped through the **one**
    /// <c>BuildAudience()</c> deserialization site (ADR 0001-B) into
    /// <see cref="CreateEventRequest"/> — the mode, the grant list, and the
    /// ADR 0036 community flag all land on the request verbatim — and the ADR
    /// 0037 <c>IsDraft</c> pin passes the composer's save-as-draft toggle
    /// through. A request-shape drift (a dropped grant, a flipped mode, a lost
    /// community flag) is a single-source break: the composer is the *only*
    /// audience write surface.
    /// </summary>
    [Fact]
    public async Task CreatePost_MapsAudience_Verbatim_ViaBuildAudience_AndDraftFlag()
    {
        var created = SampleEvent("ev-created", authorId: "subj-resident-001");
        CreateEventRequest? capturedCreate = null;
        var events = Substitute.For<IEventService>();
        events.CreateAsync(Arg.Any<string>(), Arg.Any<CreateEventRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => { capturedCreate = call.ArgAt<CreateEventRequest>(1); return created; });

        var model = new EventEditorModel
        {
            Title = "Cleanup day",
            Body = "Bring gloves. Meet at the common shed.",
            Start = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 1, 13, 0, 0, TimeSpan.Zero),
            Location = "Common shed",
            SaveAsDraft = true, // ADR 0037 — the composer posts "save as draft".
            ReminderEnabled = true,
            Audience = new AudienceEditorModel
            {
                Mode = "All",
                Grants = "[{\"Kind\":\"User\",\"Id\":\"u-grant-1\"},{\"Kind\":\"Group\",\"Id\":\"g-grant-1\"}]",
                CommunityVisible = true,
            },
        };
        var controller = Build(events, subjectId: "subj-resident-001");

        var result = await controller.CreatePost(model);

        Assert.IsType<RedirectResult>(result);
        var redirect = (RedirectResult)result;
        Assert.Equal("/events/ev-created", redirect.Url);

        await events.Received(1).CreateAsync("subj-resident-001", Arg.Any<CreateEventRequest>(), Arg.Any<CancellationToken>());
        Assert.NotNull(capturedCreate);
        var sent = capturedCreate!;

        // The single deserialization site: the editor's shape lands verbatim.
        Assert.Equal(AudienceMode.All, sent.Audience!.Mode);
        Assert.Equal(2, sent.Audience.Grants.Count);
        Assert.Contains("u-grant-1", sent.Audience.Grants.Select(g => g.Id));
        Assert.Contains("g-grant-1", sent.Audience.Grants.Select(g => g.Id));
        Assert.True(sent.Audience.Community);
        // ADR 0037 — the draft pin.
        Assert.True(sent.IsDraft);
    }

    /// <summary>
    /// <c>POST /events/new</c>, malformed shape: an empty body fails
    /// <see cref="EventEditorModel.IsValid"/> — the action re-renders the
    /// composer (a <see cref="ViewResult"/>) with a model error and **never
    /// reaches the service** (a shape error is not a write; the guard runs
    /// before <see cref="IEventService.CreateAsync"/>).
    /// </summary>
    [Fact]
    public async Task CreatePost_When_BodyMissing_RendersView_And_NeverCreates()
    {
        var events = Substitute.For<IEventService>();
        var model = new EventEditorModel
        {
            Title = "Cleanup day",
            Body = "   ", // malformed — empty body.
            Start = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 1, 13, 0, 0, TimeSpan.Zero),
            Audience = new AudienceEditorModel { Mode = "Any", Grants = "[]" },
        };
        var controller = Build(events, subjectId: "subj-resident-001");

        var result = await controller.CreatePost(model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.True(view.ViewData.ModelState.ErrorCount > 0, "expected the shape guard to add at least one model error");
        await events.DidNotReceive().CreateAsync(Arg.Any<string>(), Arg.Any<CreateEventRequest>(), Arg.Any<CancellationToken>());
    }

    // ── Edit lane (GET/POST /events/{id}/edit) — audience round-trip ──────────

    /// <summary>
    /// <c>GET /events/{id}/edit</c> (the author): the form is seeded through
    /// the **one** <see cref="AudienceEditorModel.FromAudience"/> inverse of
    /// <c>BuildAudience()</c> — the stored audience's mode, grant list, and
    /// ADR 0036 community flag round-trip onto the editor verbatim — and the
    /// §3.4 standing affordances (<see
    /// cref="Kumunita.Web.Models.EventDetailViewModel.CanEdit"/>'s shape
    /// twin: author or GlobalAdmin) are honored. A round-trip drift (a lost
    /// grant, a flipped mode, a dropped community flag) is a single-source
    /// break on the edit lane.
    /// </summary>
    [Fact]
    public async Task EditGet_When_Author_RoundTripsStoredAudience_Verbatim()
    {
        const string id = "ev-edit-author";
        const string author = "subj-author-001";
        var storedAudience = new Audience(
            AudienceMode.All,
            new[] { new AudienceGrant(GrantKind.User, "u-round-1") });
        storedAudience.Community = true;
        var existing = SampleEvent(id, authorId: author, isDraft: true);
        existing.Audience = storedAudience;
        existing.Title = "Old title";
        existing.Body = "Old body";

        var events = Substitute.For<IEventService>();
        events.GetAsync(id, author, Arg.Any<CancellationToken>()).Returns(existing);
        var controller = Build(events, subjectId: author);

        var result = (await controller.EditGet(id)) as ViewResult;

        Assert.NotNull(result);
        var model = Assert.IsType<EventEditorModel>(result!.ViewData.Model);
        Assert.Equal(id, model.Id);
        Assert.Equal("Old title", model.Title);
        Assert.Equal("Old body", model.Body);
        // The round-trip: FromAudience(seed) → the form → BuildAudience() == stored.
        Assert.Equal("All", model.Audience.Mode);
        Assert.True(model.Audience.CommunityVisible);
        Assert.Single(model.Audience.ParsedGrants);
        Assert.Equal("u-round-1", model.Audience.ParsedGrants[0].Id);
        // <see cref="Audience"/> is a plain class (no value equality) — compare
        // the round-tripped audience property-by-property against the stored one.
        var rebuilt = model.Audience.BuildAudience();
        Assert.Equal(storedAudience.Mode, rebuilt.Mode);
        Assert.Equal(storedAudience.Community, rebuilt.Community);
        Assert.Equal(storedAudience.AllResidents, rebuilt.AllResidents);
        Assert.Equal(storedAudience.Grants, rebuilt.Grants);
        // The standing gate (author ∪ GlobalAdmin) passed — a denied actor would
        // have gotten a ForbidResult before any form was seeded (the sibling
        // EditGet_When_NonAuthorNonAdmin_Returns_403 pins that branch).
    }

    /// <summary>
    /// <c>POST /events/{id}/edit</c>, happy path (a GlobalAdmin override,
    /// ADR 0017): the <see cref="IEventService.UpdateAsync"/> write lane is
    /// reached with the **same** request shape as the create lane (Title /
    /// Body / Audience / ReminderEnabled), and the controller redirects back to
    /// the detail (the M2 "redirect after write" precedent). The <c>AuthorId</c>
    /// / <c>Created</c> preservation is the service's pin (U04), not the Web's.
    /// </summary>
    [Fact]
    public async Task EditPost_When_GlobalAdmin_Updates_And_Redirects()
    {
        const string id = "ev-edit-admin";
        var existing = SampleEvent(id, authorId: "subj-author-001", isDraft: false);
        UpdateEventRequest? capturedUpdate = null;
        var events = Substitute.For<IEventService>();
        events.GetAsync(id, "subj-admin-001", Arg.Any<CancellationToken>()).Returns(existing);
        events.UpdateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<UpdateEventRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => { capturedUpdate = call.ArgAt<UpdateEventRequest>(3); return existing; });

        var model = new EventEditorModel
        {
            Id = id,
            Title = "New title",
            Body = "New body",
            Start = new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 2, 13, 0, 0, TimeSpan.Zero),
            Audience = new AudienceEditorModel { Mode = "Any", Grants = "[]", CommunityVisible = true },
        };
        var controller = Build(events, roles: new[] { Roles.GlobalAdmin }, subjectId: "subj-admin-001");

        var result = await controller.EditPost(id, model);

        Assert.IsType<RedirectResult>(result);
        Assert.Equal($"/events/{id}", ((RedirectResult)result).Url);
        await events.Received(1).UpdateAsync(id, "subj-admin-001", Arg.Any<IReadOnlySet<string>>(), Arg.Any<UpdateEventRequest>(), Arg.Any<CancellationToken>());
        Assert.NotNull(capturedUpdate);
        var sent = capturedUpdate!;
        Assert.Equal("New title", sent.Title);
        Assert.Equal("New body", sent.Body);
        Assert.Equal(AudienceMode.Any, sent.Audience!.Mode);
        Assert.True(sent.Audience.Community);
    }

    /// <summary>
    /// <c>POST /events/{id}/edit</c>: the event is **absent** — the Web layer's
    /// shape gate maps the service's <see cref="KeyNotFoundException"/> to a
    /// clean <c>404</c> before the standing check (the same "missing = 404,
    /// not 500" contract as the GET edit lane).
    /// </summary>
    [Fact]
    public async Task EditPost_When_ServiceReportsAbsent_Returns_404()
    {
        const string id = "ev-editpost-missing";
        var events = Substitute.For<IEventService>();
        events.GetAsync(id, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Event>(new KeyNotFoundException($"Event '{id}' was not found.")));
        var controller = Build(events, roles: new[] { Roles.GlobalAdmin }, subjectId: "subj-admin-001");

        var result = await controller.EditPost(id, new EventEditorModel
        {
            Title = "t", Body = "b",
            Start = new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 2, 13, 0, 0, TimeSpan.Zero),
            Audience = new AudienceEditorModel { Mode = "Any", Grants = "[]" },
        });

        Assert.IsType<NotFoundResult>(result);
        await events.DidNotReceive().UpdateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<UpdateEventRequest>(), Arg.Any<CancellationToken>());
    }

    // ── RSVP surface (GET /events/{id} + POST /events/{id}/rsvp) ───────────────

    /// <summary>
    /// The detail's **owner-only** RSVP list: for the author, <see
    /// cref="IEventService.GetRsvpsAsync"/> is loaded onto
    /// <see cref="EventDetailViewModel.Rsvps"/> (the RSVP form is
    /// owner-only-visible — the author sees their own event's RSVPs), and the
    /// viewer's own RSVP is on <c>MyRsvp</c>. The two reads are the §3.2
    /// <c>GetRsvpsAsync</c> / <c>GetMyRsvpAsync</c> split.
    /// </summary>
    [Fact]
    public async Task Detail_When_Author_RsvpListAndMyRsvp_Loaded()
    {
        const string id = "ev-rsvp-author";
        const string author = "subj-author-001";
        var existing = SampleEvent(id, authorId: author, isDraft: false);
        var events = Substitute.For<IEventService>();
        events.GetAsync(id, author, Arg.Any<CancellationToken>()).Returns(existing);
        events.GetMyRsvpAsync(id, author, Arg.Any<CancellationToken>()).Returns(new EventRsvp
        {
            Id = "rsvp-mine", EventId = id, UserId = author,
            Status = RsvpStatus.Going, At = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
        });
        events.GetRsvpsAsync(id, Arg.Any<CancellationToken>()).Returns(new List<EventRsvp>
        {
            new() { Id = "rsvp-1", EventId = id, UserId = "u-a", Status = RsvpStatus.Going, At = DateTimeOffset.UnixEpoch },
            new() { Id = "rsvp-2", EventId = id, UserId = "u-b", Status = RsvpStatus.Maybe, At = DateTimeOffset.UnixEpoch },
            new() { Id = "rsvp-3", EventId = id, UserId = author, Status = RsvpStatus.Going, At = DateTimeOffset.UnixEpoch },
        });

        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync(author).Returns((Profile?)new Profile { SubjectId = author, DisplayName = "Ada" });
        // RSVP-list display names: the author's row resolves to the profile's
        // name; the other residents' profiles are absent (NSubstitute null →
        // the raw subject-id fallback, the null-safe display lookup).
        userInfo.GetProfileAsync("u-a").Returns((Profile?)null);
        userInfo.GetProfileAsync("u-b").Returns((Profile?)new Profile { SubjectId = "u-b", DisplayName = "Ben" });

        var controller = Build(events, userInfo: userInfo, subjectId: author);

        var result = (await controller.Detail(id)) as ViewResult;

        Assert.NotNull(result);
        var model = Assert.IsType<EventDetailViewModel>(result!.ViewData.Model);
        Assert.Equal(3, model.Rsvps.Count);
        Assert.Equal("u-a", model.Rsvps[0].DisplayName);       // no profile → subject-id fallback.
        Assert.Equal("Ben", model.Rsvps[1].DisplayName);        // profile's display name.
        Assert.Equal("Ada", model.Rsvps[2].DisplayName);        // the author's own row.
        Assert.Equal(RsvpStatus.Going, model.Rsvps[0].Rsvp.Status); // the row still carries its RSVP.
        Assert.Equal(RsvpStatus.Going, model.MyRsvp!.Status);
        Assert.True(model.CanEdit);   // the author's standing affordance.
        Assert.True(model.CanDelete); // identical row (author ∪ GlobalAdmin).
        await events.Received(1).GetRsvpsAsync(id, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The detail's **non-author** RSVP surface: the owner-only list is
    /// **not** loaded (<see cref="IEventService.GetRsvpsAsync"/> is never
    /// called — the non-author sees only their own RSVP), <c>MyRsvp</c> is the
    /// <c>GetMyRsvpAsync</c> read (absent here → <c>null</c>), and the standing
    /// affordances are off (a Member non-author has no edit / delete / publish
    /// button).
    /// </summary>
    [Fact]
    public async Task Detail_When_NonAuthor_RsvpListNotLoaded_AffordancesOff()
    {
        const string id = "ev-rsvp-nonauthor";
        var existing = SampleEvent(id, authorId: "subj-author-001", isDraft: true);
        var events = Substitute.For<IEventService>();
        events.GetAsync(id, "viewer-001", Arg.Any<CancellationToken>()).Returns(existing);
        events.GetMyRsvpAsync(id, "viewer-001", Arg.Any<CancellationToken>()).Returns((EventRsvp?)null);

        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync("subj-author-001").Returns((Profile?)new Profile { SubjectId = "subj-author-001", DisplayName = "Ada" });

        var controller = Build(events, userInfo: userInfo, roles: new[] { Roles.Member }, subjectId: "viewer-001");

        var result = (await controller.Detail(id)) as ViewResult;

        Assert.NotNull(result);
        var model = Assert.IsType<EventDetailViewModel>(result!.ViewData.Model);
        Assert.Empty(model.Rsvps);
        await userInfo.DidNotReceive().GetProfileAsync("u-a"); // list not loaded → no per-rsvp name lookups.
        Assert.Null(model.MyRsvp);
        Assert.False(model.CanEdit);
        Assert.False(model.CanDelete);
        Assert.False(model.CanPublish); // non-author: the ADR 0037 author-only publish is off.
        await events.DidNotReceive().GetRsvpsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// <c>POST /events/{id}/rsvp</c>, happy path: the posted <see
    /// cref="RsvpStatus"/> passes to <see cref="IEventService.RsvpAsync"/>
    /// verbatim (the last-write-wins write — a conflicting earlier row is the
    /// service's / the unique index's to converge, not the Web's) and the
    /// controller redirects back to the detail.
    /// </summary>
    [Fact]
    public async Task Rsvp_PassesPostedStatus_Verbatim_And_Redirects()
    {
        const string id = "ev-rsvp-post";
        var events = Substitute.For<IEventService>();
        events.RsvpAsync(id, "subj-resident-001", RsvpStatus.Maybe, Arg.Any<CancellationToken>())
            .Returns(new EventRsvp { Id = "rsvp-new", EventId = id, UserId = "subj-resident-001", Status = RsvpStatus.Maybe, At = DateTimeOffset.UnixEpoch });
        var controller = Build(events, subjectId: "subj-resident-001");

        var result = await controller.Rsvp(id, RsvpStatus.Maybe);

        Assert.IsType<RedirectResult>(result);
        Assert.Equal($"/events/{id}", ((RedirectResult)result).Url);
        await events.Received(1).RsvpAsync(id, "subj-resident-001", RsvpStatus.Maybe, Arg.Any<CancellationToken>());
    }

    // ── Harness ────────────────────────────────────────────────────────────────

    private static Event SampleEvent(
        string id,
        string authorId,
        string? componentId = null,
        bool isDraft = false,
        string title = "Cleanup day",
        string body = "Bring gloves.")
    {
        var now = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        return new Event
        {
            Id = id,
            Title = title,
            Body = body,
            AuthorId = authorId,
            ComponentId = componentId,
            Start = now.AddHours(48),
            End = now.AddHours(52),
            Location = "Common shed",
            IsDraft = isDraft,
            Created = now,
        };
    }

    private static ILocalizationService DefaultLocalization()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.ListLanguagesAsync().Returns(new List<LanguageCatalog>());
        localization.GetDefaultLanguageCodeAsync().Returns("en");
        return localization;
    }

    /// <summary>
    /// Builds an <see cref="EventController"/> over NSubstitute seams (the
    /// <see cref="AnnouncementControllerTests.Build"/> shape): the
    /// <see cref="IDocumentStore"/> is a plain substitute (the M4 controller
    /// never opens a session — the service owns its own sessions, C3), and a
    /// no-op <see cref="ITempDataProvider"/> closes the <c>TempData</c> bag so
    /// the write lanes' success branches don't NRE (<c>DefaultHttpContext</c>
    /// leaves <c>Session</c> null by default).
    /// </summary>
    private static EventController Build(
        IEventService events,
        IUserInfoService? userInfo = null,
        string[]? roles = null,
        string? subjectId = null)
    {
        var userInfoImpl = userInfo ?? Substitute.For<IUserInfoService>();
        // The picker / display-name seeding lanes default to empty candidate
        // sets (a valid shape); a caller-provided substitute's own setup wins.
        if (userInfo is null)
        {
            userInfoImpl.GetComponentsAsync(true).Returns(new List<Component>());
            userInfoImpl.GetProfilesAsync(true).Returns(new List<Profile>());
            userInfoImpl.GetPublicGroupsAsync().Returns(new List<Group>());
        }

        var localization = DefaultLocalization();
        // The composer's time-range default resolves the actor's effective time
        // zone (resident override → platform default → UTC floor). An anonymous
        // HttpContextAccessor (no request principal) drives it to the UTC floor —
        // a shape test only needs a non-throwing, non-null zone.
        var timezone = new EffectiveTimezoneResolver(
            userInfoImpl, localization, new HttpContextAccessor());
        var controller = new EventController(events, userInfoImpl, localization, Substitute.For<IDocumentStore>(), timezone);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var claims = new List<Claim>();
        if (subjectId is not null)
            claims.Add(new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, subjectId));
        if (roles is { Length: > 0 })
            claims.AddRange(roles.Select(r => new Claim(Kumunita.Core.Identity.ClaimTypes.Role, r)));

        if (claims.Count > 0)
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
            // no-op — the assertion target is the redirect / the call log, not the bag
        }
    }
}
