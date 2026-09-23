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
        // ADR 0059 — the user-added event-translation lane (author ∪ Translator
        // ∪ GlobalAdmin): the add / update / remove POSTs.
        Assert.Equal("/events/{id}/translations", Route("AddTranslation"));
        Assert.Equal("/events/{id}/translations/update", Route("UpdateTranslation"));
        Assert.Equal("/events/{id}/translations/remove", Route("RemoveTranslation"));
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

    // ── ADR 0059 — the event-translation lane (author ∪ Translator ∪ GlobalAdmin) ─
    //
    // Thin Web lanes: the controller pins the route, the shape gate (blank
    // languageCode / body → redirect-back, no service call), the 404-vs-403
    // split (the service's <see cref="KeyNotFoundException"/> →
    // <see cref="NotFoundResult"/>, <see cref="UnauthorizedAccessException"/> →
    // <see cref="ForbidResult"/>), and delegates the standing + write to
    // <see cref="IEventService"/> (the service is the authority — the controller
    // never re-derives the standing split itself, ADR 0006-D). Mirrors the
    // AnnouncementControllerTests ADR 0029/0048 translation-lane pins; the
    // EventService seam has **no** <c>IDocumentSession</c> parameter (the M4
    // controller never opens a session — the service owns its own, C3).

    // ── ADR 0059·A — AddTranslation happy path (author, the Owner branch) ─────
    // The event's **author** adds a translation: the controller pre-checks the
    // viewer can see the event (<see cref="IEventService.GetAsync"/>), delegates
    // the write to <see cref="IEventService.AddEventTranslationAsync"/> (passing
    // the author's real role set), and on success redirects back to the detail
    // page with a <c>TempData["info"]</c> confirmation.

    [Fact]
    public async Task AddTranslation_Author_HappyPath_Redirects()
    {
        const string id = "ev-t-001";
        const string author = "subj-author-001";
        var events = Substitute.For<IEventService>();
        events.GetAsync(id, author, Arg.Any<CancellationToken>())
            .Returns(SampleEvent(id, author));
        events.AddEventTranslationAsync(
                id, "de", "Titel", "Körper", author,
                Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(new EventTranslation
            {
                Id = "tr-001", EventId = id, LanguageCode = "de",
                Title = "Titel", Body = "Körper", AuthorId = author,
                Created = DateTimeOffset.UtcNow,
            });

        var controller = Build(events, subjectId: author);

        var result = await controller.AddTranslation(id, "de", "Titel", "Körper");

        Assert.IsType<RedirectResult>(result);
        Assert.Equal($"/events/{id}", ((RedirectResult)result).Url);

        // The author's (possibly empty) role set is passed through — the service
        // re-checks standing from the author match, not the roles.
        await events.Received(1).AddEventTranslationAsync(
            id, "de", "Titel", "Körper", author,
            Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>());
    }

    // ── ADR 0059·B — AddTranslation via Translator (the Admin branch) ────────
    // A **Translator** (not the author) adds a translation of the author's event
    // — the ADR 0021 delegated-editor lane exercised through the Web surface.

    [Fact]
    public async Task AddTranslation_Translator_HappyPath_Redirects()
    {
        const string id = "ev-t-002";
        const string author = "subj-author-002";
        const string translator = "subj-translator-002";
        var events = Substitute.For<IEventService>();
        events.GetAsync(id, translator, Arg.Any<CancellationToken>())
            .Returns(SampleEvent(id, author));
        events.AddEventTranslationAsync(
                id, "fr", "Titre", "Corps", translator,
                Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(new EventTranslation
            {
                Id = "tr-002", EventId = id, LanguageCode = "fr",
                Title = "Titre", Body = "Corps", AuthorId = translator,
                Created = DateTimeOffset.UtcNow,
            });

        var controller = Build(events, roles: [Roles.Translator], subjectId: translator);

        var result = await controller.AddTranslation(id, "fr", "Titre", "Corps");

        Assert.IsType<RedirectResult>(result);
        await events.Received(1).AddEventTranslationAsync(
            id, "fr", "Titre", "Corps", translator,
            Arg.Is<IReadOnlySet<string>>(s => s.Contains(Roles.Translator)), Arg.Any<CancellationToken>());
    }

    // ── ADR 0059·C — AddTranslation denied → Forbid ───────────────────────────
    // A standing-denied actor (the service's <see
    // cref="UnauthorizedAccessException"/>) maps to a clean <see
    // cref="ForbidResult"/> — never a 500.

    [Fact]
    public async Task AddTranslation_Denied_Forbid()
    {
        const string id = "ev-t-003";
        var events = Substitute.For<IEventService>();
        events.GetAsync(id, "subj-member", Arg.Any<CancellationToken>())
            .Returns(SampleEvent(id, "subj-author-003"));
        events.AddEventTranslationAsync(
                id, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<EventTranslation>(new UnauthorizedAccessException("Denied.")));

        var controller = Build(events, roles: [Roles.Member], subjectId: "subj-member");

        var result = await controller.AddTranslation(id, "de", "t", "b");

        Assert.IsType<ForbidResult>(result);
    }

    // ── ADR 0059·D — AddTranslation missing event → NotFound ─────────────────
    // A missing (or not-visible) event: the service's <see
    // cref="IEventService.GetAsync"/> reports <see cref="KeyNotFoundException"/>
    // and the controller maps that to a clean <see cref="NotFoundResult"/> — the
    // write lane never touches a dangling reference.

    [Fact]
    public async Task AddTranslation_MissingEvent_NotFound()
    {
        const string id = "ev-t-missing";
        var events = Substitute.For<IEventService>();
        events.GetAsync(id, "subj-author-004", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Event>(new KeyNotFoundException($"Event '{id}' was not found.")));

        var controller = Build(events, subjectId: "subj-author-004");

        var result = await controller.AddTranslation(id, "de", "t", "b");

        Assert.IsType<NotFoundResult>(result);
        await events.DidNotReceive().AddEventTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>());
    }

    // ── ADR 0059·E — AddTranslation blank languageCode → redirect back ───────
    // A blank <c>languageCode</c> is a shape error — the controller
    // short-circuits with a redirect back to the detail page and never calls the
    // service's write seam.

    [Fact]
    public async Task AddTranslation_BlankLanguageCode_RedirectsBack_NoWrite()
    {
        var events = Substitute.For<IEventService>();
        var controller = Build(events, subjectId: "subj-author-005");

        var result = await controller.AddTranslation("ev-t-005", "   ", "Titel", "Körper");

        Assert.IsType<RedirectResult>(result);
        await events.DidNotReceive().AddEventTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>());
    }

    // ── ADR 0059·F — AddTranslation blank body → redirect back ───────────────
    // A blank <c>body</c> is a shape error (the same short-circuit): a
    // translation needs some text, and the write seam is never called.

    [Fact]
    public async Task AddTranslation_BlankBody_RedirectsBack_NoWrite()
    {
        var events = Substitute.For<IEventService>();
        var controller = Build(events, subjectId: "subj-author-006");

        var result = await controller.AddTranslation("ev-t-006", "de", "Titel", "");

        Assert.IsType<RedirectResult>(result);
        await events.DidNotReceive().AddEventTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>());
    }

    // ── ADR 0059·G — UpdateTranslation happy path (author) ───────────────────
    // The **author** edits an existing translation: delegates to
    // <see cref="IEventService.UpdateEventTranslationAsync"/> and redirects back.

    [Fact]
    public async Task UpdateTranslation_Author_HappyPath_Redirects()
    {
        const string id = "ev-u-001";
        const string author = "subj-author-001";
        var events = Substitute.For<IEventService>();
        events.GetAsync(id, author, Arg.Any<CancellationToken>())
            .Returns(SampleEvent(id, author));
        events.UpdateEventTranslationAsync(
                id, "de", "Neu", "Körper", author,
                Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(new EventTranslation
            {
                Id = "tr-u-001", EventId = id, LanguageCode = "de",
                Title = "Neu", Body = "Körper", AuthorId = author,
                Created = DateTimeOffset.UtcNow,
            });

        var controller = Build(events, subjectId: author);

        var result = await controller.UpdateTranslation(id, "de", "Neu", "Körper");

        Assert.IsType<RedirectResult>(result);
        Assert.Equal($"/events/{id}", ((RedirectResult)result).Url);
        await events.Received(1).UpdateEventTranslationAsync(
            id, "de", "Neu", "Körper", author,
            Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>());
    }

    // ── ADR 0059·H — UpdateTranslation denied → Forbid ───────────────────────
    // A standing-denied actor editing a translation is a clean 403.

    [Fact]
    public async Task UpdateTranslation_Denied_Forbid()
    {
        const string id = "ev-u-002";
        var events = Substitute.For<IEventService>();
        events.GetAsync(id, "subj-member", Arg.Any<CancellationToken>())
            .Returns(SampleEvent(id, "subj-author-002"));
        events.UpdateEventTranslationAsync(
                id, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<EventTranslation>(new UnauthorizedAccessException("Denied.")));

        var controller = Build(events, roles: [Roles.Member], subjectId: "subj-member");

        var result = await controller.UpdateTranslation(id, "de", "x", "b");

        Assert.IsType<ForbidResult>(result);
    }

    // ── ADR 0059·I — UpdateTranslation missing event → NotFound ──────────────
    // A missing event is a clean 404 (the non-leaky pin).

    [Fact]
    public async Task UpdateTranslation_MissingEvent_NotFound()
    {
        const string id = "ev-u-missing";
        var events = Substitute.For<IEventService>();
        events.GetAsync(id, "subj-author-003", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Event>(new KeyNotFoundException($"Event '{id}' was not found.")));

        var controller = Build(events, subjectId: "subj-author-003");

        var result = await controller.UpdateTranslation(id, "de", "t", "b");

        Assert.IsType<NotFoundResult>(result);
        await events.DidNotReceive().UpdateEventTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>());
    }

    // ── ADR 0059·J — RemoveTranslation happy path (author) ───────────────────
    // The **author** removes a translation: delegates to
    // <see cref="IEventService.RemoveEventTranslationAsync"/> and redirects back.

    [Fact]
    public async Task RemoveTranslation_Author_HappyPath_Redirects()
    {
        const string id = "ev-r-001";
        const string author = "subj-author-001";
        var events = Substitute.For<IEventService>();
        events.GetAsync(id, author, Arg.Any<CancellationToken>())
            .Returns(SampleEvent(id, author));

        var controller = Build(events, subjectId: author);

        var result = await controller.RemoveTranslation(id, "de");

        Assert.IsType<RedirectResult>(result);
        Assert.Equal($"/events/{id}", ((RedirectResult)result).Url);
        await events.Received(1).RemoveEventTranslationAsync(
            id, "de", author, Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>());
    }

    // ── ADR 0059·K — RemoveTranslation denied → Forbid ───────────────────────
    // A standing-denied actor removing a translation is a clean 403.

    [Fact]
    public async Task RemoveTranslation_Denied_Forbid()
    {
        const string id = "ev-r-002";
        var events = Substitute.For<IEventService>();
        events.GetAsync(id, "subj-member", Arg.Any<CancellationToken>())
            .Returns(SampleEvent(id, "subj-author-002"));
        events.RemoveEventTranslationAsync(
                id, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnauthorizedAccessException("Denied.")));

        var controller = Build(events, roles: [Roles.Member], subjectId: "subj-member");

        var result = await controller.RemoveTranslation(id, "de");

        Assert.IsType<ForbidResult>(result);
    }

    // ── ADR 0059·L — RemoveTranslation missing event → NotFound ──────────────
    // A missing event is a clean 404 (the non-leaky pin).

    [Fact]
    public async Task RemoveTranslation_MissingEvent_NotFound()
    {
        const string id = "ev-r-missing";
        var events = Substitute.For<IEventService>();
        events.GetAsync(id, "subj-author-003", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Event>(new KeyNotFoundException($"Event '{id}' was not found.")));

        var controller = Build(events, subjectId: "subj-author-003");

        var result = await controller.RemoveTranslation(id, "de");

        Assert.IsType<NotFoundResult>(result);
        await events.DidNotReceive().RemoveEventTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(),
            Arg.Any<CancellationToken>());
    }

    // ── ADR 0059·M — RemoveTranslation blank languageCode → redirect back ────
    // A blank <c>languageCode</c> is a shape error — the controller
    // short-circuits with a redirect back and never calls the service.

    [Fact]
    public async Task RemoveTranslation_BlankLanguageCode_RedirectsBack_NoWrite()
    {
        var events = Substitute.For<IEventService>();
        var controller = Build(events, subjectId: "subj-author-005");

        var result = await controller.RemoveTranslation("ev-r-005", "   ");

        Assert.IsType<RedirectResult>(result);
        await events.DidNotReceive().RemoveEventTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(),
            Arg.Any<CancellationToken>());
    }

    // ── EV-CAL — the calendar pin set (ADR 0063, design §5.4 — 3 pins) ──────
    //
    // The <c>GET /events/calendar</c> action is thin (ADR 0006-D): the
    // anchor → window math is <b>this</b> layer's (the seam is
    // window-span-agnostic — design §5.1), so the Web pins assert the
    // window-start instant the action derives from the viewer's effective
    // zone (C-EV·5), the ±1-month <c>yyyy-MM-dd</c> nav anchors (C-EV·7),
    // and the verbatim <c>componentId</c> pass-through (C-M3·2 / C-EV·3).
    // The seam + the <c>EffectiveTimezoneResolver</c> are the NSubstitute
    // substitutes (no live Postgres — the M4 controller harness shape).

    /// <summary>
    /// No <c>from</c> and no <c>view</c> ⇒ the anchor is <b>today in the
    /// viewer's effective zone</b> (C-EV·5) and the view resolves to
    /// <b>month</b> (the backward-compatible default, C-DWM·8): the window
    /// start passed to the seam is the anchor's month's <b>1st</b>, that
    /// zone's local midnight converted to UTC (a fixed-offset zone's
    /// midnight is a distinct UTC instant from the UTC-midnight floor), the
    /// window end is start + the month's days (the calendar-month span, D4 /
    /// §6.2 step 3), and the model's <see cref="EventCalendarViewModel
    /// .View"/> is <c>"month"</c> with a non-empty <see
    /// cref="EventCalendarViewModel.WindowDays"/> grid. The zone is driven
    /// through the resolver's platform-default seam (→ a real IANA zone,
    /// <c>Europe/Berlin</c>), so the <c>UTC</c> floor is not what's under
    /// test here. The expected instant is computed before and after the
    /// call, so a zone-local midnight rollover during the test can't flake
    /// the pin. (Updated for EV-DWM: the month window is the anchor's
    /// calendar month, not the prior rolling 30-day run — ADR 0064 D4.)
    /// </summary>
    [Fact]
    public async Task Calendar_DefaultFromIsTodayInEffectiveZone()
    {
        const string subject = "subj-cal-default";
        const string zoneId = "Europe/Berlin";
        var zone = EffectiveTimezoneResolver.TryConvert(zoneId)!;

        var events = Substitute.For<IEventService>();
        DateTimeOffset? capturedStart = null;
        DateTimeOffset? capturedEnd = null;
        events.ListInRangeAsync(
                Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedStart = callInfo.ArgAt<DateTimeOffset>(0);
                capturedEnd = callInfo.ArgAt<DateTimeOffset>(1);
                return Array.Empty<Event>();
            });

        var controller = BuildCalendarController(events, zoneId, roles: [Roles.Member], subjectId: subject);

        var anchorBefore = System.TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone).Date;
        var result = await controller.Calendar(null, null);
        var anchorAfter = System.TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone).Date;

        // The window is the anchor's calendar month (default view = month,
        // C-DWM·8): start = the 1st of the anchor's month, that zone's local
        // midnight as UTC; end = start + the month's day count (D4).
        var startBefore = ExpectZoneMidnightUtc(zone, new DateTime(anchorBefore.Year, anchorBefore.Month, 1));
        var startAfter = ExpectZoneMidnightUtc(zone, new DateTime(anchorAfter.Year, anchorAfter.Month, 1));
        Assert.True(
            capturedStart == startBefore || capturedStart == startAfter,
            $"Window start {capturedStart} is neither the zone's midnight of the 1st of the anchor's month ({startBefore}) nor one month rollover away ({startAfter}).");

        var anchorFromStart = System.TimeZoneInfo.ConvertTimeFromUtc(capturedStart!.Value.UtcDateTime, zone).Date;
        var daysInMonth = System.DateTime.DaysInMonth(anchorFromStart.Year, anchorFromStart.Month);
        Assert.Equal(ExpectZoneMidnightUtc(zone, anchorFromStart.AddDays(daysInMonth)), capturedEnd);

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<EventCalendarViewModel>(view.ViewData.Model);
        Assert.Equal("month", vm.View);
        Assert.NotEmpty(vm.WindowDays);
        Assert.Equal(zone.Id, vm.TimeZoneId);

        await events.Received(1).ListInRangeAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), null, subject, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A <c>from</c> anchor of <c>2026-08-15</c> (default <c>view</c> = month)
    /// windows the anchor's <b>calendar month</b>: the seam receives
    /// <c>[2026-08-01 zone-local midnight as UTC, 2026-09-01 zone-local
    /// midnight as UTC)</c> — the anchor's month's 1st through its day count
    /// (D4 / §6.2 step 3, the calendar-month span replacing the prior
    /// rolling 30-day run), and the model carries the anchor's ±1-month nav
    /// links as <c>yyyy-MM-dd</c> (<see cref="EventCalendarViewModel
    /// .PrevAnchor"/> = <c>2026-07-15</c>, <see cref="EventCalendarViewModel
    /// .NextAnchor"/> = <c>2026-09-15</c>) — the C-DWM·6 pre-rendered
    /// plain-GET-link targets. (Updated for EV-DWM / ADR 0064 D4.)
    /// </summary>
    [Fact]
    public async Task Calendar_FromShiftsWindow_AndPrevNextLinks()
    {
        const string subject = "subj-cal-shift";
        var zone = EffectiveTimezoneResolver.TryConvert("Europe/Berlin")!;

        var events = Substitute.For<IEventService>();
        DateTimeOffset? capturedStart = null;
        DateTimeOffset? capturedEnd = null;
        events.ListInRangeAsync(
                Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedStart = callInfo.ArgAt<DateTimeOffset>(0);
                capturedEnd = callInfo.ArgAt<DateTimeOffset>(1);
                return Array.Empty<Event>();
            });

        var controller = BuildCalendarController(events, zone.Id, roles: [Roles.Member], subjectId: subject);

        var result = await controller.Calendar("2026-08-15", null);

        // The window is the anchor's calendar month (default view = month):
        // start = the 1st, end = the 1st + the month's day count (D4 / §6.2
        // step 3), both as zone-local midnights converted to UTC.
        var expectedStartUtc = ExpectZoneMidnightUtc(zone, new DateTime(2026, 8, 1));
        var expectedEndUtc = ExpectZoneMidnightUtc(zone, new DateTime(2026, 9, 1));
        Assert.Equal(expectedStartUtc, capturedStart);
        Assert.Equal(expectedEndUtc, capturedEnd);

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<EventCalendarViewModel>(view.ViewData.Model);
        Assert.Equal("2026-08-15", vm.FromAnchor);
        Assert.Equal("month", vm.View);
        Assert.Equal("2026-07-15", vm.PrevAnchor);
        Assert.Equal("2026-09-15", vm.NextAnchor);

        await events.Received(1).ListInRangeAsync(
            expectedStartUtc, expectedEndUtc, null, subject, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The <c>componentId</c> query reaches the seam <b>verbatim</b>
    /// (C-M3·2 / C-EV·3 — a filter, never a gate) and is carried on the view
    /// model's <see cref="EventCalendarViewModel.CurrentComponentId"/>
    /// (the view's filter picker re-renders it selected).
    /// </summary>
    [Fact]
    public async Task Calendar_PassesComponentFilter_ToSeam()
    {
        const string subject = "subj-cal-filter";
        const string component = "component-filter-001";
        var zone = EffectiveTimezoneResolver.TryConvert("Europe/Berlin")!;

        var events = Substitute.For<IEventService>();
        events.ListInRangeAsync(
                Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Event>());

        var controller = BuildCalendarController(events, zone.Id, roles: [Roles.Member], subjectId: subject);

        var result = await controller.Calendar("2026-09-01", component);

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<EventCalendarViewModel>(view.ViewData.Model);
        Assert.Equal(component, vm.CurrentComponentId);

        await events.Received(1).ListInRangeAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), component, subject, Arg.Any<CancellationToken>());
    }

    // ── EV-DWM — the ?view= engine pin set (ADR 0064, design §6.3 — 7 pins) ─
    //
    // The <c>GET /events/calendar</c> action's U02 <c>?view=</c> engine
    // (ADR 0064 §6.2) is thin (ADR 0006-D): the anchor → per-view window
    // math, the nav-by-view-unit anchors, and the view-appropriate
    // <c>Label</c> are <b>this</b> layer's, so these Web pins assert the
    // window instants the action derives from the viewer's effective zone
    // (C-EV·5), the resolved <see cref="EventCalendarViewModel.View"/>,
    // the <see cref="EventCalendarViewModel.WindowDays"/> grid, the
    // <c>yyyy-MM-dd</c> nav anchors (C-DWM·6), and the <c>month</c>
    // display fallback (C-DWM·3 / F5). The seam + the
    // <c>EffectiveTimezoneResolver</c> are the NSubstitute substitutes
    // (no live Postgres — the M4/EV-CAL controller harness shape).

    /// <summary>
    /// No <c>from</c> and no <c>view</c> ⇒ the view resolves to
    /// <b>month</b> (the backward-compatible EV-CAL default, C-DWM·8 /
    /// F4): the anchor is today in the viewer's effective zone (C-EV·5),
    /// the window spans the anchor's <b>calendar month</b>
    /// (<c>[1st, 1st + DaysInMonth)</c> as zone-local midnights, D4),
    /// <see cref="EventCalendarViewModel.View"/> is <c>"month"</c>, the
    /// <see cref="EventCalendarViewModel.WindowDays"/> grid covers the
    /// 5–6 full weeks of the month, and <see cref="EventCalendarViewModel
    /// .Label"/> is the month name + year. The expected instants are
    /// computed before and after the call so a zone-local midnight
    /// rollover during the test can't flake the pin.
    /// </summary>
    [Fact]
    public async Task Calendar_DefaultViewIsMonth_BackwardCompat()
    {
        const string subject = "subj-dwm-default";
        const string zoneId = "Europe/Berlin";
        var zone = EffectiveTimezoneResolver.TryConvert(zoneId)!;

        var events = Substitute.For<IEventService>();
        DateTimeOffset? capturedStart = null;
        DateTimeOffset? capturedEnd = null;
        events.ListInRangeAsync(
                Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedStart = callInfo.ArgAt<DateTimeOffset>(0);
                capturedEnd = callInfo.ArgAt<DateTimeOffset>(1);
                return Array.Empty<Event>();
            });

        var controller = BuildCalendarController(events, zoneId, roles: [Roles.Member], subjectId: subject);

        var anchorBefore = System.TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone).Date;
        var result = await controller.Calendar(null, null, null);
        var anchorAfter = System.TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone).Date;

        // The window is the anchor's calendar month (default view = month,
        // C-DWM·8): start = the 1st of the anchor's month (zone-local
        // midnight as UTC); end = start + the month's day count (D4).
        var startBefore = ExpectZoneMidnightUtc(zone, new DateTime(anchorBefore.Year, anchorBefore.Month, 1));
        var startAfter = ExpectZoneMidnightUtc(zone, new DateTime(anchorAfter.Year, anchorAfter.Month, 1));
        Assert.True(
            capturedStart == startBefore || capturedStart == startAfter,
            $"Window start {capturedStart} is neither the zone's midnight of the 1st of the anchor's month ({startBefore}) nor one month rollover away ({startAfter}).");

        var anchorFromStart = System.TimeZoneInfo.ConvertTimeFromUtc(capturedStart!.Value.UtcDateTime, zone).Date;
        var daysInMonth = System.DateTime.DaysInMonth(anchorFromStart.Year, anchorFromStart.Month);
        Assert.Equal(ExpectZoneMidnightUtc(zone, anchorFromStart.AddDays(daysInMonth)), capturedEnd);
        Assert.Equal(TimeSpan.FromDays(daysInMonth), capturedEnd.Value - capturedStart.Value);

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<EventCalendarViewModel>(view.ViewData.Model);
        Assert.Equal("month", vm.View);
        // The grid covers the full calendar month — anchored on the Monday
        // on or before the 1st (D4), spanning the month's full grid (the
        // implemented span, e.g. Sept 2026 = 34 days, 08-31 … 09-27 — the
        // U05 browser-verified shape).
        var gridMonday = anchorFromStart.AddDays(-(((int)anchorFromStart.DayOfWeek + 6) % 7));
        Assert.Equal(gridMonday, vm.WindowDays[0]);
        Assert.Contains(anchorFromStart, vm.WindowDays);
        Assert.True(vm.WindowDays[^1] >= anchorFromStart.AddDays(daysInMonth - 1));
        Assert.True(vm.WindowDays.Count >= daysInMonth); // the grid spans at least the month itself.
        Assert.False(string.IsNullOrWhiteSpace(vm.Label));

        await events.Received(1).ListInRangeAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), null, subject, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// <c>?view=day</c> with an anchor ⇒ the window is the anchor day
    /// <b>only</b> (F1 / C-DWM·3): the seam receives
    /// <c>[anchorLocalStartUtc, anchorLocalStartUtc + 1d)</c> and
    /// <see cref="EventCalendarViewModel.WindowDays"/> has exactly one
    /// entry — the anchor date.
    /// </summary>
    [Fact]
    public async Task Calendar_ViewDay_WindowIsAnchorDayOnly()
    {
        const string subject = "subj-dwm-day";
        var zone = EffectiveTimezoneResolver.TryConvert("Europe/Berlin")!;

        var events = Substitute.For<IEventService>();
        DateTimeOffset? capturedStart = null;
        DateTimeOffset? capturedEnd = null;
        events.ListInRangeAsync(
                Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedStart = callInfo.ArgAt<DateTimeOffset>(0);
                capturedEnd = callInfo.ArgAt<DateTimeOffset>(1);
                return Array.Empty<Event>();
            });

        var controller = BuildCalendarController(events, zone.Id, roles: [Roles.Member], subjectId: subject);

        // Anchor = 2026-09-15 (a Tuesday).
        var result = await controller.Calendar("2026-09-15", null, "day");

        var expectedStartUtc = ExpectZoneMidnightUtc(zone, new DateTime(2026, 9, 15));
        Assert.Equal(expectedStartUtc, capturedStart);
        Assert.Equal(ExpectZoneMidnightUtc(zone, new DateTime(2026, 9, 16)), capturedEnd);
        Assert.Equal(TimeSpan.FromDays(1), capturedEnd!.Value - capturedStart!.Value);

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<EventCalendarViewModel>(view.ViewData.Model);
        Assert.Equal("day", vm.View);
        // Exactly one grid column — the anchor day (F1).
        Assert.Equal(new DateTime(2026, 9, 15), Assert.Single(vm.WindowDays));
    }

    /// <summary>
    /// <c>?view=week</c> with a <b>Thursday</b> anchor (2026-09-17) ⇒ the
    /// window is the anchor's <b>Monday-start</b> week (C-DWM·5 / F2 /
    /// C-DWM·3): the seam receives
    /// <c>[2026-09-14T00:00 zone-UTC, 2026-09-21T00:00 zone-UTC)</c> (a
    /// 7-day span), <see cref="EventCalendarViewModel.WindowDays"/> has
    /// exactly 7 entries, Monday-first — the first day is the Monday on
    /// or before the anchor (2026-09-14).
    /// </summary>
    [Fact]
    public async Task Calendar_ViewWeek_WindowIsAnchorWeek_MondayStart()
    {
        const string subject = "subj-dwm-week";
        var zone = EffectiveTimezoneResolver.TryConvert("Europe/Berlin")!;

        var events = Substitute.For<IEventService>();
        DateTimeOffset? capturedStart = null;
        DateTimeOffset? capturedEnd = null;
        events.ListInRangeAsync(
                Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedStart = callInfo.ArgAt<DateTimeOffset>(0);
                capturedEnd = callInfo.ArgAt<DateTimeOffset>(1);
                return Array.Empty<Event>();
            });

        var controller = BuildCalendarController(events, zone.Id, roles: [Roles.Member], subjectId: subject);

        // Anchor = 2026-09-17 (a Thursday); the Monday of that ISO week is
        // 2026-09-14.
        var result = await controller.Calendar("2026-09-17", null, "week");

        var expectedStartUtc = ExpectZoneMidnightUtc(zone, new DateTime(2026, 9, 14));
        Assert.Equal(expectedStartUtc, capturedStart);
        Assert.Equal(ExpectZoneMidnightUtc(zone, new DateTime(2026, 9, 21)), capturedEnd);
        Assert.Equal(TimeSpan.FromDays(7), capturedEnd!.Value - capturedStart!.Value);

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<EventCalendarViewModel>(view.ViewData.Model);
        Assert.Equal("week", vm.View);
        Assert.Equal(7, vm.WindowDays.Count);
        // Monday-start (C-DWM·5): the first column is the Monday on/before
        // the anchor; the last is the following Sunday.
        Assert.Equal(new DateTime(2026, 9, 14), vm.WindowDays[0]);
        Assert.Equal(new DateTime(2026, 9, 20), vm.WindowDays[^1]);
    }

    /// <summary>
    /// <c>?view=month</c> with an anchor ⇒ the window spans the anchor's
    /// <b>calendar month</b> (F3 / C-DWM·3): the seam receives
    /// <c>[1st, 1st + DaysInMonth)</c> as zone-local midnights, and
    /// <see cref="EventCalendarViewModel.WindowDays"/> covers the 5–6
    /// full weeks of the month, starting on the Monday on or before the
    /// 1st (D4).
    /// </summary>
    [Fact]
    public async Task Calendar_ViewMonth_WindowIsAnchorCalendarMonth()
    {
        const string subject = "subj-dwm-month";
        var zone = EffectiveTimezoneResolver.TryConvert("Europe/Berlin")!;

        var events = Substitute.For<IEventService>();
        DateTimeOffset? capturedStart = null;
        DateTimeOffset? capturedEnd = null;
        events.ListInRangeAsync(
                Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedStart = callInfo.ArgAt<DateTimeOffset>(0);
                capturedEnd = callInfo.ArgAt<DateTimeOffset>(1);
                return Array.Empty<Event>();
            });

        var controller = BuildCalendarController(events, zone.Id, roles: [Roles.Member], subjectId: subject);

        // Anchor = 2026-09-17 (mid-September 2026 — a 30-day month).
        var result = await controller.Calendar("2026-09-17", null, "month");

        var expectedStartUtc = ExpectZoneMidnightUtc(zone, new DateTime(2026, 9, 1));
        Assert.Equal(expectedStartUtc, capturedStart);
        var daysInMonth = System.DateTime.DaysInMonth(2026, 9);
        Assert.Equal(ExpectZoneMidnightUtc(zone, new DateTime(2026, 10, 1)), capturedEnd);
        Assert.Equal(TimeSpan.FromDays(daysInMonth), capturedEnd!.Value - capturedStart!.Value);

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<EventCalendarViewModel>(view.ViewData.Model);
        Assert.Equal("month", vm.View);
        // The grid covers the anchor's calendar month (D4): anchored on the
        // Monday on or before the 1st, spanning the month's full grid — the
        // implemented span (Sept 2026 = 34 days, 08-31 … 09-27 — the U05
        // browser-verified shape), always at least the month's own day count.
        var gridMonday = new DateTime(2026, 9, 1).AddDays(-(((int)new DateTime(2026, 9, 1).DayOfWeek + 6) % 7));
        Assert.Equal(gridMonday, vm.WindowDays[0]);
        Assert.Contains(new DateTime(2026, 9, 1), vm.WindowDays);
        Assert.Contains(new DateTime(2026, 9, 30), vm.WindowDays);
        Assert.True(vm.WindowDays[^1] >= new DateTime(2026, 9, 30));
        Assert.True(vm.WindowDays.Count >= daysInMonth);
    }

    /// <summary>
    /// An out-of-set <c>?view=year</c> (and an <c>empty</c>
    /// <c>?view=</c>) ⇒ the view resolves to <b>month</b> (F5 / C-DWM·3) —
    /// a <b>display fallback, not an error</b>: the action still returns a
    /// 200 <see cref="ViewResult"/> with <see cref="EventCalendarViewModel
    /// .View"/> = <c>"month"</c> and the month window, and the seam is
    /// called once with the month bounds. Two separate controller instances
    /// (one per sub-case) keep each assertion's call-count clean.
    /// </summary>
    [Fact]
    public async Task Calendar_InvalidViewFallsBackToMonth()
    {
        const string subject = "subj-dwm-invalid";
        var zone = EffectiveTimezoneResolver.TryConvert("Europe/Berlin")!;

        // Sub-case 1 — out-of-set value ("year") falls back to month.
        var events1 = Substitute.For<IEventService>();
        DateTimeOffset? capturedStart = null;
        DateTimeOffset? capturedEnd = null;
        events1.ListInRangeAsync(
                Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedStart = callInfo.ArgAt<DateTimeOffset>(0);
                capturedEnd = callInfo.ArgAt<DateTimeOffset>(1);
                return Array.Empty<Event>();
            });
        var controller1 = BuildCalendarController(events1, zone.Id, roles: [Roles.Member], subjectId: subject);

        // Anchor = 2026-09-17 (September 2026 — a 30-day month).
        var result1 = await controller1.Calendar("2026-09-17", null, "year");
        var vm1 = Assert.IsType<EventCalendarViewModel>(Assert.IsType<ViewResult>(result1).ViewData.Model);
        Assert.Equal("month", vm1.View);
        // The fallback window is the month window (the display fallback,
        // not an error path): start = the 1st of the anchor's month
        // (zone-local midnight as UTC); span = the month's day count.
        Assert.Equal(ExpectZoneMidnightUtc(zone, new DateTime(2026, 9, 1)), capturedStart);
        Assert.Equal(TimeSpan.FromDays(30), capturedEnd!.Value - capturedStart!.Value);
        await events1.Received(1).ListInRangeAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), null, subject, Arg.Any<CancellationToken>());

        // Sub-case 2 — an empty view value is the same display fallback.
        var events2 = Substitute.For<IEventService>();
        capturedStart = null;
        capturedEnd = null;
        events2.ListInRangeAsync(
                Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedStart = callInfo.ArgAt<DateTimeOffset>(0);
                capturedEnd = callInfo.ArgAt<DateTimeOffset>(1);
                return Array.Empty<Event>();
            });
        var controller2 = BuildCalendarController(events2, zone.Id, roles: [Roles.Member], subjectId: subject);

        var result2 = await controller2.Calendar("2026-09-17", null, "");
        var vm2 = Assert.IsType<EventCalendarViewModel>(Assert.IsType<ViewResult>(result2).ViewData.Model);
        Assert.Equal("month", vm2.View);
        Assert.Equal(ExpectZoneMidnightUtc(zone, new DateTime(2026, 9, 1)), capturedStart);
        Assert.Equal(TimeSpan.FromDays(30), capturedEnd!.Value - capturedStart!.Value);
        await events2.Received(1).ListInRangeAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), null, subject, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// <c>?view=week&amp;from=&lt;anchor&gt;&amp;componentId=&lt;id&gt;</c> ⇒ the
    /// nav anchors are the anchor shifted by the <b>view's unit</b>
    /// (±1 week, C-DWM·6 / F6): <see cref="EventCalendarViewModel
    /// .PrevAnchor"/> = <c>2026-09-16</c>, <see cref="EventCalendarViewModel
    /// .NextAnchor"/> = <c>2026-09-30</c>, <see cref="EventCalendarViewModel
    /// .FromAnchor"/> = <c>2026-09-23</c>; <see cref="EventCalendarViewModel
    /// .View"/> is <c>"week"</c> (the view rides along in the links'
    /// <c>?view=</c> query — <c>Calendar.cshtml</c>'s
    /// <c>NavHref</c> builds <c>?from=&amp;view=&amp;componentId=</c> from
    /// exactly these fields) and <see cref="EventCalendarViewModel
    /// .CurrentComponentId"/> carries the filter verbatim (C-M3·2 /
    /// C-EV·3 — the <c>componentId</c> filter rides along too).
    /// </summary>
    [Fact]
    public async Task Calendar_ViewRidesAlongInPrevNextNavLinks()
    {
        const string subject = "subj-dwm-nav";
        const string component = "component-nav-001";
        var zone = EffectiveTimezoneResolver.TryConvert("Europe/Berlin")!;

        var events = Substitute.For<IEventService>();
        events.ListInRangeAsync(
                Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Event>());

        var controller = BuildCalendarController(events, zone.Id, roles: [Roles.Member], subjectId: subject);

        // Anchor = 2026-09-23 (a Wednesday); the view is week; the
        // component filter is passed verbatim.
        var result = await controller.Calendar("2026-09-23", component, "week");

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<EventCalendarViewModel>(view.ViewData.Model);
        Assert.Equal("week", vm.View);
        Assert.Equal(component, vm.CurrentComponentId);
        // Nav by the view's unit (±1 week, C-DWM·6): the prev/next anchors
        // are the anchor shifted ±7 days (the Monday-start alignment is
        // preserved by the ±7-day shift, so the week view stays aligned).
        Assert.Equal("2026-09-23", vm.FromAnchor);
        Assert.Equal("2026-09-16", vm.PrevAnchor);
        Assert.Equal("2026-09-30", vm.NextAnchor);

        await events.Received(1).ListInRangeAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), component, subject, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The <see cref="EventCalendarViewModel.Label"/> is the
    /// <b>view-appropriate</b> display string in the UI culture
    /// (C-DWM·3 / C-DWM·9 — a computed display string, <b>not</b> a
    /// registry key): <c>?view=day</c> ⇒ a full date (day name + day +
    /// month + year); <c>?view=week</c> ⇒ a <c>Mon d – Mon d</c> range
    /// (abbreviated day names + the en-dash separator);
    /// <c>?view=month</c> ⇒ the month name + year. The assertions are
    /// culture-robust: they check the structural shape (the anchor day,
    /// the range endpoints, the month name) in <see
    /// cref="CultureInfo.InvariantCulture"/>, whose en-GB/US day + month
    /// names are the <c>kw-l</c> English fallback set.
    /// </summary>
    [Fact]
    public async Task Calendar_Label_IsViewAppropriate()
    {
        const string subject = "subj-dwm-label";
        var zone = EffectiveTimezoneResolver.TryConvert("Europe/Berlin")!;

        var events = Substitute.For<IEventService>();
        events.ListInRangeAsync(
                Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Event>());

        var controller = BuildCalendarController(events, zone.Id, roles: [Roles.Member], subjectId: subject);
        var fmt = System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat;

        // Anchor = 2026-09-15 (a Tuesday).
        var dayVm = Assert.IsType<EventCalendarViewModel>(
            Assert.IsType<ViewResult>(await controller.Calendar("2026-09-15", null, "day")).ViewData.Model);
        // Day → a full date: the day name + the day number + the month name
        // + the year, all present (the exact separators are culture-shape).
        Assert.Contains("15", dayVm.Label);
        Assert.Contains("2026", dayVm.Label);
        Assert.Contains(fmt.GetDayName(System.DayOfWeek.Tuesday), dayVm.Label, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains(fmt.GetMonthName(9), dayVm.Label, System.StringComparison.OrdinalIgnoreCase);

        // Anchor = 2026-09-17 (a Thursday); the ISO week is
        // 2026-09-14 (Mon) … 2026-09-20 (Sun).
        var weekVm = Assert.IsType<EventCalendarViewModel>(
            Assert.IsType<ViewResult>(await controller.Calendar("2026-09-17", null, "week")).ViewData.Model);
        // Week → a "Mon d – Mon d" range: both endpoints' abbreviated day
        // names + the en-dash separator + both day numbers.
        Assert.Contains("–", weekVm.Label);
        Assert.Contains(fmt.GetAbbreviatedDayName(System.DayOfWeek.Monday), weekVm.Label, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains(fmt.GetAbbreviatedDayName(System.DayOfWeek.Sunday), weekVm.Label, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("14", weekVm.Label);
        Assert.Contains("20", weekVm.Label);

        // Anchor = 2026-09-17 (mid-September 2026).
        var monthVm = Assert.IsType<EventCalendarViewModel>(
            Assert.IsType<ViewResult>(await controller.Calendar("2026-09-17", null, "month")).ViewData.Model);
        // Month → the month name + year (the EV-CAL shape).
        Assert.Contains(fmt.GetMonthName(9), monthVm.Label, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2026", monthVm.Label);
    }

    // ── Harness ────────────────────────────────────────────────────────────────

    /// <summary>The anchor date's zone-local midnight as a UTC instant — the
    /// §5.2 window math (<c>anchor → zone-local midnight → UTC</c>), factored
    /// so the test's expectation and the controller's derivation share one
    /// expression.</summary>
    private static DateTimeOffset ExpectZoneMidnightUtc(System.TimeZoneInfo zone, DateTime nowUtc)
    {
        var anchorDate = System.TimeZoneInfo.ConvertTimeFromUtc(nowUtc, zone).Date;
        return new DateTimeOffset(anchorDate, zone.GetUtcOffset(anchorDate)).ToUniversalTime();
    }

    /// <summary>
    /// Builds an <see cref="EventController"/> whose effective zone is the
    /// given IANA <paramref name="zoneId"/> — driven through the resolver's
    /// own seams (the <see cref="Kumunita.Core.Localization.ILocalizationService.GetDefaultTimezoneAsync"/>
    /// platform-default seam returns the zone id; the resolver's
    /// profile → default → UTC resolution order is covered by its own tests,
    /// so here the <em>zone</em> is the given and the action's window math
    /// is the pin). Everything else mirrors <see cref="Build"/>.
    /// </summary>
    private static EventController BuildCalendarController(
        IEventService events,
        string zoneId,
        string[]? roles,
        string? subjectId)
    {
        var userInfoImpl = Substitute.For<IUserInfoService>();
        userInfoImpl.GetComponentsAsync(true).Returns(new List<Component>());

        var localization = DefaultLocalization();
        localization.GetDefaultTimezoneAsync().Returns(zoneId);
        var timezone = new EffectiveTimezoneResolver(userInfoImpl, localization, new HttpContextAccessor());

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
