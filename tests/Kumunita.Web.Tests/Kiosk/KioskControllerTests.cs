using System.Collections.Concurrent;
using System.Security.Claims;
using Kumunita.Core.Events;
using Kumunita.Core.Kiosk;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Kumunita.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Marten;
using NSubstitute;
using System.Reflection;
using Xunit;
using Claim = System.Security.Claims.Claim;
using ClaimsIdentity = System.Security.Claims.ClaimsIdentity;
using ClaimsPrincipal = System.Security.Claims.ClaimsPrincipal;

namespace Kumunita.Web.Tests;

/// <summary>
/// The 6 pinned M35 Web tests (design doc §2.5, verbatim names) — the
/// <see cref="KioskController"/> surface: the <c>GET /kiosk</c> chrome-hidden
/// display (the M35·3 / M35·6 signed-in first-slide render + the M35-6
/// anonymous→sign-in gate), the <c>GET /kiosk/slide</c> denied shape (the
/// M35-5 no-leak / no-404 FACE), the <c>GET /settings/kiosk</c> per-resident
/// settings gate (the ADR 0164 amendment — any signed-in user, no role
/// restriction), the <c>POST /settings/kiosk</c> save-lane (the M35·5
/// delegation to the service that owns the single <c>kiosk.save</c> audit row
/// (Via = Owner) + the per-resident upsert), and the M35·6 additive-only pin
/// (the board / calendar /
/// todos surfaces render **unchanged** — the kiosk frames them, it does not
/// re-author them).
/// <para>
/// Harness: the project's standing <b>direct-construction</b> idiom (NSubstitute
/// <see cref="IKioskService"/> / <see cref="IKioskItemCatalog"/> (ADR 0285) /
/// <see cref="IEventService"/> / <see cref="IDocumentStore"/> seams +
/// <see cref="DefaultHttpContext"/> — the <see cref="AdminStorageMetricsHistoryTests"/>
/// M33 shape). The frozen read-seam Postgres behaviour is pinned by the 6 M35
/// Core tests in <c>Kiosk/KioskServiceTests.cs</c>; these 6 pin the Web
/// surface. View <c>kw-l</c> / chrome-hide / additive-only pins use the house
/// <b>"string pin, no TestServer"</b> idiom (the <see cref="PwaManifestTests"/>
/// / <see cref="AdminStorageMetricsHistoryTests"/> shape — read the view as a
/// string; the rendered contract is the view source).
/// </para>
/// </summary>
public class KioskControllerTests
{
    private const string Subject = "m35-user-001";
    private const string Member  = Kumunita.Core.Identity.Roles.Member;
    private const string GlobalAdmin = Kumunita.Core.Identity.Roles.GlobalAdmin;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ── Pin 7 ─────────────────────────────────────────────────────────────
    // M35·3 / M35·6 (FACES M35-2): a SIGNED-IN viewer visits /kiosk → the
    // chrome-hidden layout + the first slide render. The entry resolves (ADR
    // 0285: through the catalog, i.e. the item's own read seam) to the item's
    // OWN route; the stage renders the iframe (the M35·6 reuse). The slide
    // carries the entry's duration / scroll / comments (ADR 0285).

    [Fact]
    public async Task M35_3_SignedIn_Sees_ChromeHidden_FirstSlide()
    {
        var (controller, kiosk, catalog, _, _) = Build(role: Member);

        var entry = new KioskEntry
        {
            Type = KioskItemType.Board, ItemId = "board-1", DurationSeconds = 25,
            Scroll = KioskScrollBehavior.Auto,
        };
        kiosk.GetPlaylistAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new KioskPlaylist
        {
            Id = Subject,
            Entries = [entry],
            DefaultDurationSeconds = 40,
            RefreshIntervalSeconds = 12,
            ScrollSpeed = KioskScrollSpeed.Slow,
        });
        catalog.ResolveAsync(entry, Subject, Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(new KioskResolvedItem(true, "Board A", "/projects/boards/board-1", "B|lane"));

        var action = await controller.Index(Ct);
        var model = Assert.IsType<KioskDisplayViewModel>(Assert.IsType<ViewResult>(action).Model);

        Assert.True(model.HasSlides);
        var first = model.FirstSlide!;
        Assert.False(first.Denied);
        // D7: the slide href carries ?kiosk=1 — the authoritative kiosk signal
        // the layout turns into the kmb-kiosk body class (survives no-referrer).
        Assert.Equal("/projects/boards/board-1?kiosk=1", first.Href);
        Assert.Equal("Board A", first.Title);
        Assert.Equal(25, first.DurationSeconds);
        Assert.Equal(KioskScrollBehavior.Auto, first.Scroll);
        Assert.Equal(KioskFingerprint.Of("B|lane"), first.Fingerprint);
        Assert.Equal(40, model.DefaultDurationSeconds);
        Assert.Equal(12, model.RefreshIntervalSeconds);
        Assert.Equal(KioskScrollSpeed.Slow, model.ScrollSpeed);
        await kiosk.Received(1).GetPlaylistAsync(Subject, Arg.Any<CancellationToken>());

        var html = ViewSource("Kiosk", "Index.cshtml");
        Assert.Contains("#kiosk-stage", html);              // the stage the TS reads
        Assert.Contains("kmb-kiosk-iframe", html);          // the iframe (the item's own route)
        Assert.Contains(".navbar,", html);                  // the chrome-hide selector set
        Assert.Contains("key=\"kiosk.title\"", html);       // the display heading
    }


    [Fact]
    public void M35_3_Anonymous_RedirectedToSignIn()
    {
        var attr = typeof(KioskController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.True(
            string.IsNullOrEmpty(attr!.Roles),
            "GET /kiosk is signed-in (any authenticated resident) — the [Authorize] gate has NO role restriction (M35·3 / M35-6).");

        var httpGet = typeof(KioskController).GetMethod(nameof(KioskController.Index))!
            .GetCustomAttribute<HttpGetAttribute>();
        Assert.NotNull(httpGet);
        Assert.Contains("/kiosk", httpGet!.Template ?? string.Empty);
    }


    [Fact]
    public async Task M35_5_SlideDenied_SignsInViewer_SeesDeniedShape()
    {
        var (controller, kiosk, catalog, _, _) = Build(role: Member);

        var entry = new KioskEntry { Type = KioskItemType.Announcement, ItemId = "ann-x" };
        kiosk.GetPlaylistAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new KioskPlaylist
        {
            Id = Subject,
            Entries = [entry],
        });
        catalog.ResolveAsync(entry, Subject, Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(KioskResolvedItem.Hidden);

        var action = await controller.Slide(slide: 0, t: 1, ct: Ct);
        var vm = Assert.IsType<KioskSlideRefreshViewModel>(Assert.IsType<JsonResult>(action).Value);

        Assert.True(vm.Denied);
        Assert.Null(vm.Href);
        Assert.Equal("kiosk.slide_denied", vm.DeniedKey);
        Assert.Equal(KioskFingerprint.Of("denied"), vm.Fingerprint);
        Assert.Equal(0, vm.Index);
    }

    // ADR 0285 D1 — a calendar entry: the catalog gives the route (with the
    // component filter); the controller fingerprints the events in the
    // viewer's window, read for that component.

    [Fact]
    public async Task ADR0285_CalendarSlide_ReadsTheComponentsWindow()
    {
        var (controller, kiosk, catalog, events, _) = Build(role: Member);

        var entry = new KioskEntry { Type = KioskItemType.Calendar, ItemId = "comp-1", CalendarView = "day" };
        kiosk.GetPlaylistAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new KioskPlaylist
        {
            Id = Subject,
            Entries = [entry],
        });
        catalog.ResolveAsync(entry, Subject, Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(new KioskResolvedItem(true, "Main street", "/events/calendar?view=day&componentId=comp-1", null));
        events.ListInRangeAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), "comp-1", Subject, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Event>>([]));

        var action = await controller.Slide(slide: 0, t: 1, ct: Ct);
        var vm = Assert.IsType<KioskSlideRefreshViewModel>(Assert.IsType<JsonResult>(action).Value);

        Assert.False(vm.Denied);
        // D7: the base href already has a query, so ?kiosk=1 rides as &kiosk=1.
        Assert.Equal("/events/calendar?view=day&componentId=comp-1&kiosk=1", vm.Href);
        await events.Received(1).ListInRangeAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), "comp-1", Subject, Arg.Any<CancellationToken>());
    }


    [Fact]
    public void M35_7_Settings_Authorized_NoRoleGate()
    {
        var getMethod = typeof(KioskController).GetMethod(nameof(KioskController.SettingsGet))!;
        Assert.True(
            getMethod.GetCustomAttribute<AuthorizeAttribute>() is null,
            "GET /settings/kiosk is any signed-in user (per-resident, ADR 0164 amendment) — no method-level [Authorize(Roles = ...)] gate; the class-level [Authorize] (no roles) is the sign-in gate.");

        var postMethod = typeof(KioskController).GetMethod(nameof(KioskController.SettingsPost))!;
        Assert.True(
            postMethod.GetCustomAttribute<AuthorizeAttribute>() is null,
            "POST /settings/kiosk is any signed-in user (per-resident, ADR 0164 amendment) — no method-level [Authorize(Roles = ...)] gate; the class-level [Authorize] (no roles) is the sign-in gate.");

        var classAttr = typeof(KioskController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(classAttr);
        Assert.True(
            string.IsNullOrEmpty(classAttr!.Roles),
            "The KioskController [Authorize] gate is role-free — a Member may configure their own kiosk (ADR 0164 amendment).");
    }

    // ADR 0285 — the editor shows each stored entry with its item's current
    // title (resolved for the viewer); a gone / hidden item is flagged.

    [Fact]
    public async Task ADR0285_SettingsGet_ResolvesEntryTitles()
    {
        var (controller, kiosk, catalog, _, _) = Build(role: Member);

        var visible = new KioskEntry { Type = KioskItemType.Page, ItemId = "page-1" };
        var gone = new KioskEntry { Type = KioskItemType.CommunityPost, ItemId = "post-gone" };
        kiosk.GetPlaylistAsync(Subject, Arg.Any<CancellationToken>()).Returns(new KioskPlaylist
        {
            Id = Subject,
            Entries = [visible, gone],
            DefaultDurationSeconds = 20,
            ScrollSpeed = KioskScrollSpeed.Fast,
        });
        catalog.ResolveAsync(visible, Subject, Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(new KioskResolvedItem(true, "Bin days", "/pages/bins", "v"));
        catalog.ResolveAsync(gone, Subject, Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(KioskResolvedItem.Hidden);

        var action = await controller.SettingsGet(Ct);
        var vm = Assert.IsType<KioskSettingsViewModel>(Assert.IsType<ViewResult>(action).Model);

        Assert.Equal(2, vm.Entries.Count);
        Assert.Equal("Bin days", vm.Entries[0].ItemTitle);
        Assert.True(vm.Entries[0].ItemVisible);
        Assert.Null(vm.Entries[1].ItemTitle);
        Assert.False(vm.Entries[1].ItemVisible);
        Assert.Equal(20, vm.DefaultDurationSeconds);
        Assert.Equal(KioskScrollSpeed.Fast, vm.ScrollSpeed);
    }


    [Fact]
    public async Task M35_5_GlobalAdmin_SavePlaylist_OneAuditRow_Persists()
    {
        var (controller, kiosk, _, _, _) = Build(role: GlobalAdmin);

        KioskPlaylistDraft? saved = null;
        // IMPROVE-CODE C09 — the committed write lane (3-arg overload, no
        // IDocumentSession) is what the controller now calls.
        kiosk.SavePlaylistAsync(Arg.Do<KioskPlaylistDraft>(d => saved = d),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new KioskPlaylist { Id = Subject });

        // ADR 0285 — rows arrive in index order (gaps are fine); a row with no
        // type is left out; the group id rides only on group-routed types; the
        // checkbox + hidden-false pair reads as checked.
        controller.HttpContext.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["DefaultDurationSeconds"]    = new StringValues("60"),
            ["RefreshIntervalSeconds"]    = new StringValues("30"),
            ["DisplayMode"]               = new StringValues("FullScreen"),
            ["ScrollSpeed"]               = new StringValues("Fast"),
            ["Entries[3].Type"]           = new StringValues("GroupPost"),
            ["Entries[3].ItemId"]         = new StringValues("post-1"),
            ["Entries[3].GroupId"]        = new StringValues("group-1"),
            ["Entries[3].DurationSeconds"] = new StringValues("45"),
            ["Entries[3].Scroll"]         = new StringValues("Auto"),
            ["Entries[3].ShowComments"]   = new StringValues(["false", "true"]),
            ["Entries[3].Label"]          = new StringValues("Choir news"),
            ["Entries[1].Type"]           = new StringValues("Calendar"),
            ["Entries[1].ItemId"]         = new StringValues(""),
            ["Entries[1].GroupId"]        = new StringValues("ignored"),
            ["Entries[1].CalendarView"]   = new StringValues("month"),
            ["Entries[1].ShowComments"]   = new StringValues("false"),
            ["Entries[2].Type"]           = new StringValues(""),
            ["Entries[2].ItemId"]         = new StringValues("left-out"),
        });

        var action = await controller.SettingsPost(Ct);

        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(KioskController.SettingsGet), redirect.ActionName);
        Assert.NotNull(controller.TempData["info"]);

        await kiosk.Received(1).SavePlaylistAsync(
            Arg.Any<KioskPlaylistDraft>(), Subject, Arg.Any<CancellationToken>());
        Assert.NotNull(saved);
        Assert.Equal(60, saved!.DefaultDurationSeconds);
        Assert.Equal(30, saved.RefreshIntervalSeconds);
        Assert.Equal(KioskDisplayMode.FullScreen, saved.DisplayMode);
        Assert.Equal(KioskScrollSpeed.Fast, saved.ScrollSpeed);
        Assert.Equal(2, saved.Entries.Count);

        var calendar = saved.Entries[0];
        Assert.Equal(KioskItemType.Calendar, calendar.Type);
        Assert.Null(calendar.ItemId);
        Assert.Null(calendar.GroupId);
        Assert.Equal("month", calendar.CalendarView);
        Assert.Equal(60, calendar.DurationSeconds);   // no duration → the default
        Assert.False(calendar.ShowComments);

        var post = saved.Entries[1];
        Assert.Equal(KioskItemType.GroupPost, post.Type);
        Assert.Equal("post-1", post.ItemId);
        Assert.Equal("group-1", post.GroupId);
        Assert.Equal(45, post.DurationSeconds);
        Assert.Equal(KioskScrollBehavior.Auto, post.Scroll);
        Assert.True(post.ShowComments);
        Assert.Equal("Choir news", post.Label);
    }

    [Theory]
    [InlineData("Todo", "", "30", "Pick an item")]                  // no item picked
    [InlineData("GroupPost", "post-1", "30", "Pick an item")]       // no group
    [InlineData("Todo", "todo-1", "3", "between 5 and 3600")]       // under 5 s
    [InlineData("Todo", "todo-1", "abc", "between 5 and 3600")]     // not a number
    public async Task ADR0285_SavePlaylist_RefusesAnInvalidEntry(string type, string itemId, string duration, string message)
    {
        var (controller, kiosk, _, _, _) = Build(role: Member);

        controller.HttpContext.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["DefaultDurationSeconds"]     = new StringValues("30"),
            ["RefreshIntervalSeconds"]     = new StringValues("10"),
            ["Entries[0].Type"]            = new StringValues(type),
            ["Entries[0].ItemId"]          = new StringValues(itemId),
            ["Entries[0].DurationSeconds"] = new StringValues(duration),
        });

        var action = await controller.SettingsPost(Ct);

        Assert.IsType<RedirectToActionResult>(action);
        Assert.Contains(message, controller.TempData["error"] as string ?? string.Empty);
        await kiosk.DidNotReceiveWithAnyArgs().SavePlaylistAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("4", "10")]   // default duration under 5
    [InlineData("30", "1")]   // refresh under 2
    public async Task ADR0285_SavePlaylist_RefusesInvalidDisplaySettings(string defaultDuration, string refresh)
    {
        var (controller, kiosk, _, _, _) = Build(role: Member);

        controller.HttpContext.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["DefaultDurationSeconds"] = new StringValues(defaultDuration),
            ["RefreshIntervalSeconds"] = new StringValues(refresh),
        });

        var action = await controller.SettingsPost(Ct);

        Assert.IsType<RedirectToActionResult>(action);
        Assert.NotNull(controller.TempData["error"]);
        await kiosk.DidNotReceiveWithAnyArgs().SavePlaylistAsync(default!, default!, TestContext.Current.CancellationToken);
    }


    [Fact]
    public void M35_6_ExistingSurfaces_RenderUnchanged()
    {
        var surfaces = new[]
        {
            // BoardDetail was moved from Projects/ → Boards/ (view-path reorg).
            (Folder: "Boards", View: "BoardDetail.cshtml", Model: "BoardDetailViewModel"),
            (Folder: "Todos",  View: "TodosIndex.cshtml",  Model: "TodoIndexViewModel"),
            (Folder: "Event",  View: "Calendar.cshtml",    Model: "EventCalendarViewModel"),
        };
        foreach (var (folder, view, model) in surfaces)
        {
            var html = ViewSource(folder, view);
            Assert.Contains(model, html, StringComparison.Ordinal);                        // the surface's own @model (untouched)
            Assert.DoesNotContain("kiosk", html, StringComparison.OrdinalIgnoreCase);        // no kiosk coupling (M35·6 / M35·9)
        }
    }

    // ── ADR 0285 D4 / D5 — playback attributes and the comments toggle ─────

    [Theory]
    // D7: every slide href carries ?kiosk=1 (the authoritative kiosk signal,
    // which _Layout turns into the kmb-kiosk body class — it survives the
    // global Referrer-Policy: no-referrer that a referrer-based test would
    // have failed under). D5: where the entry shows comments on a discussion
    // surface, &kioskComments=1 rides after it.
    [InlineData(KioskItemType.Announcement, true, "/announcements/a-1", "/announcements/a-1?kiosk=1&kioskComments=1")]
    [InlineData(KioskItemType.Announcement, false, "/announcements/a-1", "/announcements/a-1?kiosk=1")]
    [InlineData(KioskItemType.Page, true, "/pages/bins", "/pages/bins?kiosk=1")]          // no discussion on a page
    [InlineData(KioskItemType.Todo, true, "/projects/todos/t-1", "/projects/todos/t-1?kiosk=1&kioskComments=1")]
    public async Task ADR0285_ShowComments_AddsTheQueryFlag_OnlyWhereThereIsADiscussion(
        KioskItemType type, bool showComments, string resolvedHref, string expectedHref)
    {
        var (controller, kiosk, catalog, _, _) = Build(role: Member);
        var entry = new KioskEntry { Type = type, ItemId = "x", ShowComments = showComments };
        kiosk.GetPlaylistAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new KioskPlaylist
        {
            Id = Subject,
            Entries = [entry],
        });
        catalog.ResolveAsync(entry, Subject, Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(new KioskResolvedItem(true, "Item", resolvedHref, "v"));

        var action = await controller.Slide(slide: 0, t: 1, ct: Ct);
        var vm = Assert.IsType<KioskSlideRefreshViewModel>(Assert.IsType<JsonResult>(action).Value);

        Assert.Equal(expectedHref, vm.Href);
    }

    [Fact]
    public void ADR0285_PlaybackAndCommentsWiring_IsInPlace()
    {
        // The stage + slide meta carry what kiosk.ts plays.
        var index = ViewSource("Kiosk", "Index.cshtml");
        Assert.Contains("data-default-duration=", index);
        Assert.Contains("data-scroll-speed=", index);
        Assert.Contains("data-duration=", index);
        Assert.Contains("data-scroll=", index);

        // The layout turns ?kioskComments=1 into the body class …
        var layout = ViewSource("Shared", "_Layout.cshtml");
        Assert.Contains("kmb-kiosk-comments", layout);
        Assert.Contains("kioskComments", layout);

        // D7 — the authoritative kiosk signal: the kiosk appends ?kiosk=1 to
        // EVERY slide href (KioskController always does), and _Layout turns it
        // into the kmb-kiosk body class. This is what actually activates the
        // kiosk CSS in the browser — it survives the global
        // Referrer-Policy: no-referrer (F7) that suppresses the Referer header
        // a referrer-based test relied on (the bug that left kiosk controls
        // visible). Pinned on both ends: the controller emits it on every slide,
        // and the layout reads it from the query string.
        var controllerSrc = File.ReadAllText(Path.Combine(RepoRoot, "src", "Kumunita.Web", "Controllers", "KioskController.cs"));
        Assert.Contains("KioskQueryKey = \"kiosk\"", controllerSrc);
        Assert.Contains("AppendQuery(baseHref, KioskQueryKey, \"1\")", controllerSrc);
        Assert.Contains("Query[\"kiosk\"]", layout);
        Assert.Contains("kmb-kiosk", layout);

        // … site.css hides the discussion sections without it …
        var css = File.ReadAllText(Path.Combine(RepoRoot, "src", "Kumunita.Web", "wwwroot", "css", "site.css"));
        Assert.Contains("body.kmb-kiosk:not(.kmb-kiosk-comments) .kmb-discussion", css);
        // The control-only shell-hide rules (the "Add a to-do" composer card,
        // the RSVP card, the "Translations:" chip row).
        Assert.Contains("body.kmb-kiosk .kmb-detail-card:has(> .card-body > form)", css);
        Assert.Contains("body.kmb-kiosk .kmb-detail-card:has(form.kmb-rsvp)", css);
        Assert.Contains("body.kmb-kiosk .d-flex:has(> [data-translation-chip])", css);
        // The calendar cluster-hide rules (the Full page / Full screen
        // toggle row, the view-toggle + New event + iCal feed row, the
        // Prev/Today/Next/Filter nav row, the Events/Calendar tab strip).
        Assert.Contains("body.kmb-kiosk .lede-row .d-flex", css);
        Assert.Contains("body.kmb-kiosk .head-row > .d-flex", css);
        Assert.Contains("body.kmb-kiosk .d-flex.mb-3", css);
        Assert.Contains("body.kmb-kiosk .kmb-tab-under-navbar", css);

        // … and the discussion partials that detail pages render mark it.
        // The kmb-discussion markup was extracted from the detail views into
        // the shared partials (the served markup is unchanged) — pin those.
        foreach (var (folder, view) in new[]
        {
            ("Posts", "Detail.cshtml"), ("Announcement", "Detail.cshtml"),
            ("Event", "Detail.cshtml"), ("Groups", "EventDetail.cshtml"),
            // The discussion sections that formerly lived inline in TodoDetail / PostDetail:
            ("Todos", "_TodoComments.cshtml"), ("Groups", "_GroupPostReplies.cshtml"),
        })
        {
            Assert.Contains("kmb-discussion", ViewSource(folder, view));
        }
    }

    // ── ADR 0285 — the playlist editor's item picker ──────────────────────

    [Fact]
    public async Task ADR0285_Items_UnknownType_Is400()
    {
        var (controller, _, _, _, _) = Build(role: Member);

        var action = await controller.Items("Inventory", null, Ct);
        var numeric = await controller.Items("3", null, Ct);

        Assert.IsType<BadRequestResult>(action);
        Assert.IsType<BadRequestResult>(numeric);
    }

    [Fact]
    public async Task ADR0285_Items_Calendar_All_First_Then_Components()
    {
        var (controller, _, catalog, _, _) = Build(role: Member);
        catalog.SearchAsync(KioskItemType.Calendar, null, Subject, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<KioskItemOption>>([new KioskItemOption("c-1", "Main street", null, null)]));

        var action = await controller.Items("calendar", null, Ct);

        var items = ItemsOf(action);
        Assert.Equal(2, items.Count);
        Assert.Equal("", items[0].GetProperty("id").GetString());
        Assert.Equal("All communities", items[0].GetProperty("title").GetString());
        Assert.Equal("c-1", items[1].GetProperty("id").GetString());
    }

    [Fact]
    public async Task ADR0285_Items_Board_PassesQueryAndActor()
    {
        var (controller, _, catalog, _, _) = Build(role: Member);
        catalog.SearchAsync(KioskItemType.Board, "garden", Subject, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<KioskItemOption>>([new KioskItemOption("b-1", "Garden board", null, null)]));

        var action = await controller.Items("Board", "garden", Ct);

        var item = Assert.Single(ItemsOf(action));
        Assert.Equal("b-1", item.GetProperty("id").GetString());
        Assert.Equal("Garden board", item.GetProperty("title").GetString());
    }

    private static List<System.Text.Json.JsonElement> ItemsOf(IActionResult action)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(Assert.IsType<JsonResult>(action).Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("items").EnumerateArray().Select(e => e.Clone()).ToList();
    }


    private static (KioskController controller, IKioskService kiosk, IKioskItemCatalog catalog,
        IEventService events, IDocumentStore store) Build(string? role)
    {
        var kiosk    = Substitute.For<IKioskService>();
        var catalog  = Substitute.For<IKioskItemCatalog>();
        var events   = Substitute.For<IEventService>();
        IDocumentStore store = default!; // IMPROVE-CODE C09 — the controller no longer takes a store; kept in the tuple for the other deconstruction sites.

        var controller = new KioskController(kiosk, events, null, catalog);

        var services = new ServiceCollection();
        services.AddHttpContextAccessor();
        services.AddSingleton<ITempDataProvider, InMemoryTempDataProvider>();
        services.AddTransient<ITempDataDictionaryFactory, TempDataDictionaryFactory>();
        services.AddSingleton(Substitute.For<IModelMetadataProvider>());
        services.AddSingleton<IUrlHelperFactory, StubUrlHelperFactory>();
        services.AddSingleton(kiosk);
        services.AddSingleton(catalog);
        services.AddSingleton(events);
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            Request =
            {
                Scheme = "http",
                Host   = new HostString("localhost"),
            },
            RequestServices = provider,
            User = role is null
                ? new ClaimsPrincipal()
                : new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Subject),
                        new Claim(Kumunita.Core.Identity.ClaimTypes.Role, role),
                    },
                    authenticationType: "test")),
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return (controller, kiosk, catalog, events, store);
    }

    private static string ViewSource(string folder, string view)
    {
        var path = Path.Combine(RepoRoot, "src", "Kumunita.Web", "Views", folder, view);
        Assert.True(File.Exists(path), $"Views/{folder}/{view} not found at {path}.");

        var html = File.ReadAllText(path);
        return System.Text.RegularExpressions.Regex.Replace(
            html,
            @"\@\*.*?\*\@",
            "",
            System.Text.RegularExpressions.RegexOptions.Singleline);
    }

    /// <summary>Locate the repo root (the dir that holds <c>Kumunita.slnx</c>)
    /// by walking up from the test output dir (the <see cref="PwaManifestTests"/>
    /// shape — the pin is correct regardless of the test output depth).</summary>
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Kumunita.slnx")))
                dir = dir.Parent;
            Assert.True(dir is not null, "Could not locate the repo root (Kumunita.slnx).");
            return dir!.FullName;
        }
    }

    /// <summary>A minimal in-memory <see cref="ITempDataProvider"/> so the
    /// <see cref="Microsoft.AspNetCore.Mvc.Controller.TempData"/> property
    /// (which lazily resolves <c>ITempDataProvider</c> from
    /// <see cref="HttpContext.RequestServices"/>) works in the direct-construction
    /// harness. The M35 <c>POST /settings/kiosk</c> lane writes
    /// <c>TempData["info"]</c> on success; without a registered provider the
    /// property's <c>GetRequiredService</c> would throw (a test-harness gap, not
    /// a production concern — a real deployment always carries the provider via
    /// <c>AddControllers()</c>). The M33 <c>AdminStorageMetricsHistoryTests</c>
    /// precedent never hit a <c>TempData</c>-writing POST, so this is new.</summary>
    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        // Retain saved values across Load/Save so a redirect-after-POST
        // (the R18 LinkCreate → SettingsGet copy-once token round-trip, the
        // ADR 0302 D2 reveal) resolves the token the POST wrote. The existing
        // M35 SettingsPost test reads its own TempData within the same request
        // (the controller's provider round-trip), so this retention does not
        // change its behavior.
        private readonly IDictionary<string, object?> _stored =
            new ConcurrentDictionary<string, object?>();
        public IDictionary<string, object> LoadTempData(HttpContext context)
        {
            var result = new Dictionary<string, object>();
            foreach (var (key, value) in _stored)
            {
                if (value is not null) result[key] = value;
            }
            return result;
        }
        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
            _stored.Clear();
            foreach (var (key, value) in values) _stored[key] = value;
        }
    }

    /// <summary>A minimal <see cref="IUrlHelperFactory"/> / <see cref="IUrlHelper"/>
    /// pair so <see cref="Microsoft.AspNetCore.Mvc.ControllerBase.Url"/> (and hence
    /// the success-path <c>RedirectToAction("/admin/kiosk")</c>) resolves in the
    /// direct-construction harness. The test never executes the
    /// <c>RedirectToActionResult</c> (no UrlHelperExecutor), so <c>Url.Action</c>
    /// only needs to return a non-null string — the framework constructs the
    /// <c>UrlActionContext</c> / <c>UrlRouteContext</c> arguments and hands them
    /// in, so the stub just returns a stub path. .NET 10's <c>IUrlHelper</c>
    /// methods return <see cref="string"/> (the older <c>UrlValue</c> type is
    /// gone). The compiler enumerates the exact required members against the
    /// real interface.</summary>
    private sealed class StubUrlHelperFactory : IUrlHelperFactory
    {
        public IUrlHelper GetUrlHelper(ActionContext actionContext) => new StubUrlHelper(actionContext);
    }

    private sealed class StubUrlHelper : IUrlHelper
    {
        public StubUrlHelper(ActionContext actionContext)
        {
            ActionContext = actionContext;
        }

        public ActionContext ActionContext { get; }
        public string Action(UrlActionContext actionContext) => "stub-action";
        public string Content(string? content) => content ?? string.Empty;
        public bool IsLocalUrl(string? url) => false;
        public string RouteUrl(UrlRouteContext routeContext) => "stub-route";
        public string Link(string? linkName, object? values) => "stub-link";
    }
}

// ─────────────────────────────────────────────────────────────────────────
// ADR 0302 (IMPROVE-UX R18) — the /settings/kiosk shared-link lifecycle.
//
// The DoD tests (plan-improve-ux.md, R18 row): create link (copy-once
// token, the row stores the hash not the raw token), list with last used
// (a revoked link is out of the list), revoke (terminal, a second revoke is
// a no-op), the owner-scope pin (another owner's link is a no-op), and the
// ADR 0302 D5 personal-kiosk-untouched pin.
//
// Harness: the project's standing direct-construction idiom (the
// KioskControllerTests.Build shape) + the ADR 0302 IKioskLinkService seam
// and the KioskLinkListReader read seam (both substituted). The
// InMemoryTempDataProvider (retaining saved values) pins the copy-once
// token round-trip.
// ─────────────────────────────────────────────────────────────────────────

public class KioskLinkSettingsTests
{
    private const string Owner = "r18-owner-001";
    private const string Member = Kumunita.Core.Identity.Roles.Member;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ── 1 — Create link — the happy path (the copy-once token + the row) ──

    [Fact]
    public async Task R18_CreateLink_ReturnsTokenOnce_AndRedirectsToSettings()
    {
        var (controller, _, _, linkService, _) = BuildR18(role: Member);
        var newLink = new KioskLink
        {
            Id = "link-abc123",
            PlaylistId = Owner,
            OwnerSubjectId = Owner,
            TokenHash = "fake-hash-for-test",
            CreatedAt = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero),
        };
        linkService.CreateAsync(Owner, Owner, Arg.Any<CancellationToken>())
            .Returns((token: "kskl_test-token", link: newLink));

        var action = await controller.LinkCreate(Ct);

        // The redirect-after-POST (the SettingsPost precedent) — the token
        // rides TempData for the single round-trip, never the URL (ADR 0302 D2).
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(KioskController.SettingsGet), redirect.ActionName);
        Assert.Equal("kskl_test-token", controller.TempData["kiosk.link.token"] as string);
        Assert.Equal("link-abc123", controller.TempData["kiosk.link.id"] as string);
        await linkService.Received(1).CreateAsync(Owner, Owner, Arg.Any<CancellationToken>());
    }

    // ── 2 — The token is shown once, then gone (no "show again") ──────────

    [Fact]
    public async Task R18_CreateLink_TokenShownOnce_NotRecoverableOnRefresh()
    {
        var (controller, _, _, _, links) = BuildR18(role: Member);
        links.ListActiveAsync(Owner, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<KioskLink>>([
                new KioskLink
                {
                    Id = "link-xyz", PlaylistId = Owner, OwnerSubjectId = Owner,
                    TokenHash = "hash-xyz",
                    CreatedAt = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero),
                    LastUsedAt = new DateTimeOffset(2026, 11, 10, 14, 30, 0, TimeSpan.Zero),
                },
            ]));

        // First GET (the reveal): the token LinkCreate wrote to TempData is
        // read once and handed to the view for the copy-once input.
        controller.TempData["kiosk.link.token"] = "kskl_secret";
        var firstVm = Assert.IsType<KioskSettingsViewModel>(
            Assert.IsType<ViewResult>(await controller.SettingsGet(Ct)).Model);
        Assert.Equal("kskl_secret", firstVm.SharedLinkToken);

        // Second GET (a refresh): the token is cleared (the ADR 0302 D2
        // "no show again" floor) — the list row carries metadata only, and
        // the raw token is nowhere in the view model.
        var secondVm = Assert.IsType<KioskSettingsViewModel>(
            Assert.IsType<ViewResult>(await controller.SettingsGet(Ct)).Model);
        Assert.Null(secondVm.SharedLinkToken);
        Assert.Single(secondVm.SharedLinks);
        Assert.Equal("link-xyz", secondVm.SharedLinks[0].Id);
        Assert.NotNull(secondVm.SharedLinks[0].LastUsedAt);
    }

    // ── 3 — List with last used (newest first, active only) ────────────────

    [Fact]
    public async Task R18_SettingsGet_ListsActiveLinksWithLastUsed()
    {
        var (controller, _, _, _, links) = BuildR18(role: Member);
        var older = new DateTimeOffset(2026, 11, 9, 9, 0, 0, TimeSpan.Zero);
        var now = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
        links.ListActiveAsync(Owner, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<KioskLink>>([
                new KioskLink { Id = "link-2", PlaylistId = Owner, OwnerSubjectId = Owner,
                    TokenHash = "h2", CreatedAt = now, LastUsedAt = null },
                new KioskLink { Id = "link-1", PlaylistId = Owner, OwnerSubjectId = Owner,
                    TokenHash = "h1", CreatedAt = older, LastUsedAt = older.AddHours(2) },
            ]));

        var vm = Assert.IsType<KioskSettingsViewModel>(
            Assert.IsType<ViewResult>(await controller.SettingsGet(Ct)).Model);
        Assert.Equal(2, vm.SharedLinks.Count);
        Assert.Equal("link-2", vm.SharedLinks[0].Id);
        Assert.Equal("link-1", vm.SharedLinks[1].Id);
        Assert.Null(vm.SharedLinks[0].LastUsedAt);
        Assert.NotNull(vm.SharedLinks[1].LastUsedAt);
        await links.Received(1).ListActiveAsync(Owner, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task R18_SettingsGet_NoActiveLinks_EmptyList()
    {
        var (controller, _, _, _, links) = BuildR18(role: Member);
        links.ListActiveAsync(Owner, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<KioskLink>>([]));

        var vm = Assert.IsType<KioskSettingsViewModel>(
            Assert.IsType<ViewResult>(await controller.SettingsGet(Ct)).Model);
        Assert.Empty(vm.SharedLinks);
        Assert.Null(vm.SharedLinkToken);
    }

    // ── 4 — Revoke — happy path + the idempotent no-op ─────────────────────

    [Fact]
    public async Task R18_RevokeLink_CallsTheSeam_ReturnsTrue()
    {
        var (controller, _, _, linkService, _) = BuildR18(role: Member);
        linkService.RevokeAsync("link-1", Owner, Arg.Any<CancellationToken>()).Returns(true);

        var action = await controller.LinkRevoke("link-1", Ct);
        var json = Assert.IsType<JsonResult>(action);
        Assert.True(BoolOf(json!, "revoked"));
        await linkService.Received(1).RevokeAsync("link-1", Owner, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task R18_RevokeLink_SecondCall_IsANoop()
    {
        var (controller, _, _, linkService, _) = BuildR18(role: Member);
        // The R15 ADR 0302 D3 idempotent shape: an already-revoked link
        // returns false (no audit row, no state change) — the controller
        // still returns JSON 200 (revoked: false; the row is already out of
        // the owner's list).
        linkService.RevokeAsync("link-1", Owner, Arg.Any<CancellationToken>()).Returns(false);

        var action = await controller.LinkRevoke("link-1", Ct);
        Assert.False(BoolOf(Assert.IsType<JsonResult>(action)!, "revoked"));
    }

    // ── 5 — Revoke — the owner-scope pin (another owner's link is a no-op) ─

    [Fact]
    public async Task R18_RevokeLink_AnotherOwnersLink_IsANoop()
    {
        var (controller, _, _, linkService, _) = BuildR18(role: Member);
        // The R15 seam's owner-scope check (ADR 0302 D3): a link the actor
        // does not own is a no-op returning false (not a 404/403 — the
        // idempotent precedent; the owner's own list never contains another
        // owner's link, so the UI shows absence, not an error).
        linkService.RevokeAsync("someone-elses-link", Owner, Arg.Any<CancellationToken>()).Returns(false);

        var action = await controller.LinkRevoke("someone-elses-link", Ct);
        Assert.False(BoolOf(Assert.IsType<JsonResult>(action)!, "revoked"));
        await linkService.Received(1).RevokeAsync("someone-elses-link", Owner, Arg.Any<CancellationToken>());
    }

    // ── 6 — Personal mode untouched (the ADR 0302 D5 pin) ─────────────────

    [Fact]
    public void R18_PersonalKioskActions_RoutesUnchanged()
    {
        // R18 is additive: the personal-kiosk routes (SettingsGet /
        // SettingsPost) are re-pointed to none; the shared-link routes
        // (/settings/kiosk/link, /settings/kiosk/link/revoke) are NEW. The
        // existing KioskControllerTests (M35_7, ADR0285_SettingsGet, the
        // save lane) still pass unchanged (the D5 pin).
        var getAttr = typeof(KioskController).GetMethod(nameof(KioskController.SettingsGet))!
            .GetCustomAttribute<HttpGetAttribute>();
        Assert.Contains("/settings/kiosk", getAttr!.Template ?? string.Empty);

        var postAttr = typeof(KioskController).GetMethod(nameof(KioskController.SettingsPost))!
            .GetCustomAttribute<HttpPostAttribute>();
        Assert.Contains("/settings/kiosk", postAttr!.Template ?? string.Empty);

        var createAttr = typeof(KioskController).GetMethod(nameof(KioskController.LinkCreate))!
            .GetCustomAttribute<HttpPostAttribute>();
        Assert.Contains("/settings/kiosk/link", createAttr!.Template ?? string.Empty);
    }

    // ── 7 — The shared-link actions are owner-guarded (a blank actor 403s) ─

    [Fact]
    public async Task R18_CreateLink_NoActorId_Is403()
    {
        var (controller, _, _, linkService, _) = BuildR18(role: null);
        var action = await controller.LinkCreate(Ct);
        Assert.IsType<ForbidResult>(action);
        await linkService.DidNotReceiveWithAnyArgs().CreateAsync(default!, default!, Ct);
    }

    [Fact]
    public async Task R18_RevokeLink_NoActorId_Is403()
    {
        var (controller, _, _, linkService, _) = BuildR18(role: null);
        var action = await controller.LinkRevoke("link-1", Ct);
        Assert.IsType<ForbidResult>(action);
        await linkService.DidNotReceiveWithAnyArgs().RevokeAsync(default!, default!, Ct);
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private static bool BoolOf(JsonResult json, string prop) =>
        (bool)(json!.Value!.GetType().GetProperty(prop)!.GetValue(json.Value)!);

    /// <summary>A retaining TempData provider for the R18 copy-once token
    /// round-trip (LinkCreate → redirect → SettingsGet reads the token the
    /// POST wrote, then clears it). The KioskControllerTests'
    /// InMemoryTempDataProvider is nested and inaccessible here, so this is a
    /// local copy with the same retention semantics.</summary>
    private sealed class R18TempData : ITempDataProvider
    {
        private readonly IDictionary<string, object?> _stored =
            new ConcurrentDictionary<string, object?>();
        public IDictionary<string, object> LoadTempData(HttpContext context)
        {
            var result = new Dictionary<string, object>();
            foreach (var (key, value) in _stored)
            {
                if (value is not null) result[key] = value;
            }
            return result;
        }
        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
            _stored.Clear();
            foreach (var (key, value) in values) _stored[key] = value;
        }
    }

    /// <summary>A minimal <see cref="IUrlHelperFactory"/> so
    /// <c>RedirectToAction</c> (the R18 LinkCreate success path) resolves in
    /// the direct-construction harness (the KioskControllerTests
    /// StubUrlHelperFactory shape).</summary>
    private sealed class R18UrlFactory : IUrlHelperFactory
    {
        public IUrlHelper GetUrlHelper(ActionContext actionContext) => new R18UrlHelper(actionContext);
    }

    private sealed class R18UrlHelper : IUrlHelper
    {
        public R18UrlHelper(ActionContext actionContext) { ActionContext = actionContext; }
        public ActionContext ActionContext { get; }
        public string Action(UrlActionContext actionContext) => "stub-action";
        public string Content(string? content) => content ?? string.Empty;
        public bool IsLocalUrl(string? url) => false;
        public string RouteUrl(UrlRouteContext routeContext) => "stub-route";
        public string Link(string? linkName, object? values) => "stub-link";
    }

    private static (KioskController controller, IKioskService kiosk,
        IKioskItemCatalog catalog, IKioskLinkService linkService,
        Kumunita.Web.Services.IKioskLinkListReader links) BuildR18(string? role)
    {
        var kiosk = Substitute.For<IKioskService>();
        var catalog = Substitute.For<IKioskItemCatalog>();
        var events = Substitute.For<IEventService>();
        var linkService = Substitute.For<IKioskLinkService>();
        var links = Substitute.For<Kumunita.Web.Services.IKioskLinkListReader>();

        var controller = new KioskController(kiosk, events, null, catalog, linkService, links);

        var services = new ServiceCollection();
        services.AddHttpContextAccessor();
        services.AddSingleton<ITempDataProvider, R18TempData>();
        services.AddTransient<ITempDataDictionaryFactory, TempDataDictionaryFactory>();
        services.AddSingleton(Substitute.For<IModelMetadataProvider>());
        services.AddSingleton<IUrlHelperFactory, R18UrlFactory>();
        services.AddSingleton(kiosk);
        services.AddSingleton(catalog);
        services.AddSingleton(events);
        services.AddSingleton(linkService);
        services.AddSingleton(links);
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            Request = { Scheme = "http", Host = new HostString("localhost") },
            RequestServices = provider,
            User = role is null
                ? new ClaimsPrincipal()
                : new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Owner),
                        new Claim(Kumunita.Core.Identity.ClaimTypes.Role, role),
                    },
                    authenticationType: "test")),
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return (controller, kiosk, catalog, linkService, links);
    }
}
