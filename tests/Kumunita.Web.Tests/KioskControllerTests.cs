using System.Collections.Concurrent;
using System.Security.Claims;
using Kumunita.Core.Events;
using Kumunita.Core.Kiosk;
using Kumunita.Core.Projects;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
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
/// <see cref="IKioskService"/> / <see cref="IProjectService"/> /
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

    // ── Pin 7 ─────────────────────────────────────────────────────────────
    // M35·3 / M35·6 (FACES M35-2): a SIGNED-IN viewer visits /kiosk → the
    // chrome-hidden layout + the first slide render. The playlist's Board
    // slide resolves to the surface's OWN route (/projects/boards/{id}) via
    // the frozen board-detail read seam; the stage renders the iframe (the
    // M35·6 reuse — the kiosk frames the surface, it does not re-author it).

    [Fact]
    public async Task M35_3_SignedIn_Sees_ChromeHidden_FirstSlide()
    {
        var (controller, kiosk, projects, _, _) = Build(role: Member);

        // A configured playlist (one Board slide) the signed-in viewer can see:
        kiosk.GetPlaylistAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new KioskPlaylist
        {
            Id = Subject,
            Views = [new KioskView { Kind = KioskViewKind.Board, BoardId = "board-1" }],
            ViewIntervalSeconds = 40,
            RefreshIntervalSeconds = 12,
        });
        // The frozen M5 board-detail read seam resolves the board for the viewer:
        projects.GetBoardAsync("board-1", Subject, Arg.Any<CancellationToken>())
            .Returns(new BoardDetailResult
            {
                Board = new KanbanBoard { Id = "board-1", Title = "Board A" },
            });

        var action = await controller.Index(TestContext.Current.CancellationToken);
        var model = Assert.IsType<KioskDisplayViewModel>(Assert.IsType<ViewResult>(action).Model);

        // The first slide resolves to the surface's own route (the iframe src, M35·6):
        Assert.True(model.HasSlides);
        Assert.NotNull(model.FirstSlide);
        Assert.False(model.FirstSlide!.Denied);
        Assert.Equal("/projects/boards/board-1", model.FirstSlide.Href);
        // The two intervals project through (M35·4 display-only):
        Assert.Equal(40, model.ViewIntervalSeconds);
        Assert.Equal(12, model.RefreshIntervalSeconds);
        await kiosk.Received(1).GetPlaylistAsync(Subject, Arg.Any<CancellationToken>());
        await projects.Received(1).GetBoardAsync("board-1", Subject, Arg.Any<CancellationToken>());

        // The chrome-hidden layout renders (the ADR 0104 body.kmb-page-expanded
        // idiom — the top-level chrome is hidden, the stage + iframe fill the
        // viewport — the M35·6 pin): the string-pin idiom.
        var html = ViewSource("Kiosk", "Index.cshtml");
        Assert.Contains("#kiosk-stage", html);              // the stage the TS reads
        Assert.Contains("kmb-kiosk-iframe", html);          // the iframe (the surface's own route)
        Assert.Contains(".navbar,", html);                  // the chrome-hide selector set
        Assert.Contains("key=\"kiosk.title\"", html);       // the display heading
    }

    // ── Pin 8 ─────────────────────────────────────────────────────────────
    // M35·3 / M35-6 (FACES M35-6): an ANONYMOUS visitor to /kiosk is
    // redirected to sign-in — the gate is the class-level [Authorize] (no role
    // restriction: any authenticated resident). Asserted via the attribute
    // (the house "no TestServer" idiom — the M33 M33_7_NonGlobalAdmin_Denied
    // shape: the redirect is the attribute's effect, not an action-level
    // re-check).

    [Fact]
    public void M35_3_Anonymous_RedirectedToSignIn()
    {
        // The gate (M35-6): [Authorize] on the controller type — an anonymous
        // request is rejected by the auth middleware → the sign-in redirect.
        var attr = typeof(KioskController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.True(
            string.IsNullOrEmpty(attr!.Roles),
            "GET /kiosk is signed-in (any authenticated resident) — the [Authorize] gate has NO role restriction (M35·3 / M35-6).");

        // The /kiosk route is the Index action (the display surface):
        var httpGet = typeof(KioskController).GetMethod(nameof(KioskController.Index))!
            .GetCustomAttribute<HttpGetAttribute>();
        Assert.NotNull(httpGet);
        Assert.Contains("/kiosk", httpGet!.Template ?? string.Empty);
    }

    // ── Pin 9 ─────────────────────────────────────────────────────────────
    // M35·5 / M35-5 (FACES M35-5): GET /kiosk/slide?slide=N over a slide the
    // viewer CANNOT see → the kiosk.slide_denied shape (no content, no 404 —
    // the frozen seam's deny signal is caught, the denied shape returned).

    [Fact]
    public async Task M35_5_SlideDenied_SignsInViewer_SeesDeniedShape()
    {
        var (controller, kiosk, projects, _, _) = Build(role: Member);

        kiosk.GetPlaylistAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new KioskPlaylist
        {
            Id = Subject,
            Views = [new KioskView { Kind = KioskViewKind.Board, BoardId = "board-x" }],
        });
        // The viewer may NOT read this board — the frozen seam's deny signal
        // (a faulted Task the controller's catch converts to the denied shape):
        projects.GetBoardAsync("board-x", Subject, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<BoardDetailResult>(new UnauthorizedAccessException()));

        var action = await controller.Slide(slide: 0, t: 1, ct: TestContext.Current.CancellationToken);
        var vm = Assert.IsType<KioskSlideRefreshViewModel>(Assert.IsType<JsonResult>(action).Value);

        // The denied shape (M35-5 FACE): no content (Href null), the
        // kiosk.slide_denied key named, and **not** a 404 (a JSON 200 with the
        // denied flag):
        Assert.True(vm.Denied);
        Assert.Null(vm.Href);
        Assert.Equal("kiosk.slide_denied", vm.DeniedKey);
        Assert.Equal(0, vm.Index);
    }

    // ── Pin 10 ────────────────────────────────────────────────────────────
    // M35·2 (ADR 0164 amendment — per-resident): the settings surface is
    // [Authorize]d (any signed-in user edits their OWN playlist) with **no
    // role restriction** — the GlobalAdmin gate from the original M35-7 pin is
    // gone (the playlist is no longer a shared GlobalAdmin singleton). The
    // sign-in gate is the class-level [Authorize] (pinned in the M35-6
    // M35_3_Anonymous test); the settings methods carry **no** method-level
    // [Authorize(Roles = ...)] gate (a Member may save their own kiosk).

    [Fact]
    public void M35_7_Settings_Authorized_NoRoleGate()
    {
        // The sign-in gate (ADR 0164 amendment): the class-level [Authorize]
        // (any signed-in resident edits their own playlist) carries NO role
        // set — the M35_3_Anonymous test pins its presence; here we pin that
        // the settings surface adds no method-level role restriction (the
        // original M35-7 GlobalAdmin gate is removed).
        var getMethod = typeof(KioskController).GetMethod(nameof(KioskController.SettingsGet))!;
        Assert.True(
            getMethod.GetCustomAttribute<AuthorizeAttribute>() is null,
            "GET /settings/kiosk is any signed-in user (per-resident, ADR 0164 amendment) — no method-level [Authorize(Roles = ...)] gate; the class-level [Authorize] (no roles) is the sign-in gate.");

        // The same: the POST lane has no method-level role gate either —
        // any signed-in user saves their own playlist.
        var postMethod = typeof(KioskController).GetMethod(nameof(KioskController.SettingsPost))!;
        Assert.True(
            postMethod.GetCustomAttribute<AuthorizeAttribute>() is null,
            "POST /settings/kiosk is any signed-in user (per-resident, ADR 0164 amendment) — no method-level [Authorize(Roles = ...)] gate; the class-level [Authorize] (no roles) is the sign-in gate.");

        // And the class gate itself (the sign-in requirement) has NO roles:
        var classAttr = typeof(KioskController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(classAttr);
        Assert.True(
            string.IsNullOrEmpty(classAttr!.Roles),
            "The KioskController [Authorize] gate is role-free — a Member may configure their own kiosk (ADR 0164 amendment).");
    }

    // ── Pin 11 ────────────────────────────────────────────────────────────
    // M35·5 / M35-1 (ADR 0164 amendment — per-resident): a signed-in resident
    // POSTs /settings/kiosk → the
    // controller parses the form, opens the caller's session, and delegates to
    // SavePlaylistAsync (the service owns the ONE kiosk.save audit row + the
    // upsert — the M35·5 pin, pinned in the Core KioskServiceTests) and
    // redirects on success (the M3 same-transaction save-lane shape).

    [Fact]
    public async Task M35_5_GlobalAdmin_SavePlaylist_OneAuditRow_Persists()
    {
        var (controller, kiosk, _, _, store) = Build(role: GlobalAdmin);

        // The caller's session (the M3 same-transaction lane — the controller
        // opens it, the service commits into it):
        var session = Substitute.For<IDocumentSession>();
        store.LightweightSession().Returns(session);
        // The service save (the one kiosk.save audit row + upsert — the Core
        // pin) returns the saved row:
        kiosk.SavePlaylistAsync(
                Arg.Any<IReadOnlyList<KioskView>>(), 60, 30,
                Arg.Any<KioskDisplayMode>(),
                Arg.Any<string>(), Arg.Any<IDocumentSession>(), Arg.Any<CancellationToken>())
            .Returns(new KioskPlaylist
            {
                Id = Subject,
                Views = [new KioskView { Kind = KioskViewKind.Board, BoardId = "board-1" }],
                ViewIntervalSeconds = 60,
                RefreshIntervalSeconds = 30,
            });

        // The form (a single Board slide + the two intervals):
        controller.HttpContext.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["ViewIntervalSeconds"]    = new StringValues("60"),
            ["RefreshIntervalSeconds"] = new StringValues("30"),
            ["DisplayMode"]            = new StringValues("FullScreen"),
            ["Views[0].Kind"]          = new StringValues("Board"),
            ["Views[0].BoardId"]       = new StringValues("board-1"),
        });

        var action = await controller.SettingsPost(TestContext.Current.CancellationToken);

        // The success path (not the sub-floor redirect-back): the redirect +
        // the saved confirmation. The redirect MUST be the action-name overload
        // resolving SettingsGet's /settings/kiosk attribute route — NOT a string
        // URL passed to RedirectToAction(string), which ASP.NET Core treats as an
        // *action name* and renders to /Kiosk/%2Fsettings%2Fkiosk (a 404). Pinning
        // the action name is what keeps that regression caught.
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(KioskController.SettingsGet), redirect.ActionName);
        Assert.NotNull(controller.TempData["info"]);

        // The controller delegated the single write to the service with the
        // parsed form (one Board view + the two intervals + the parsed
        // DisplayMode + the caller's session) — the service commits the one
        // kiosk.save audit row + the upsert (M35·5, pinned in KioskServiceTests).
        // The DisplayMode is pinned to FullScreen (the form value) to verify
        // the amendment's display-mode parse (M35·4 display-only):
        await kiosk.Received(1).SavePlaylistAsync(
            Arg.Any<IReadOnlyList<KioskView>>(), 60, 30,
            KioskDisplayMode.FullScreen,
            Arg.Any<string>(), Arg.Any<IDocumentSession>(), Arg.Any<CancellationToken>());
    }

    // ── Pin 12 ────────────────────────────────────────────────────────────
    // M35·6 (the additive-only pin): the EXISTING board / calendar / todos
    // surfaces render UNCHANGED — each still declares its OWN @model and
    // carries NO kiosk coupling (the kiosk frames them via iframe, it does not
    // re-author them — the M35·6 / M35·9 "reused, not re-shape" pin).

    [Fact]
    public void M35_6_ExistingSurfaces_RenderUnchanged()
    {
        // Each of the three framed surfaces keeps its own model + has no
        // kiosk coupling (the string-pin idiom — the M35·6 additive-only pin):
        var surfaces = new[]
        {
            (Folder: "Projects", View: "BoardDetail.cshtml", Model: "BoardDetailViewModel"),
            (Folder: "Projects", View: "TodosIndex.cshtml",  Model: "TodoIndexViewModel"),
            (Folder: "Event",    View: "Calendar.cshtml",    Model: "EventCalendarViewModel"),
        };
        foreach (var (folder, view, model) in surfaces)
        {
            var html = ViewSource(folder, view);
            Assert.Contains(model, html, StringComparison.Ordinal);                        // the surface's own @model (untouched)
            Assert.DoesNotContain("kiosk", html, StringComparison.OrdinalIgnoreCase);        // no kiosk coupling (M35·6 / M35·9)
        }
    }

    // ── Harness ──────────────────────────────────────────────────────────────

    /// <summary>Build a <see cref="KioskController"/> over a
    /// <see cref="DefaultHttpContext"/> whose principal carries
    /// <paramref name="role"/> (the <see cref="AdminStorageMetricsHistoryTests"/>
    /// M33 <c>Build</c> shape) + NSubstituted <see cref="IKioskService"/> /
    /// <see cref="IProjectService"/> / <see cref="IEventService"/> /
    /// <see cref="IDocumentStore"/> seams the test stubs. <paramref name="role"/>
    /// is <c>null</c> for the anonymous (no-principal) case. No Postgres (the
    /// Core seam behaviour is pinned in <c>Kiosk/KioskServiceTests.cs</c>).</summary>
    private static (KioskController controller, IKioskService kiosk, IProjectService projects,
        IEventService events, IDocumentStore store) Build(string? role)
    {
        var kiosk    = Substitute.For<IKioskService>();
        var projects = Substitute.For<IProjectService>();
        var events   = Substitute.For<IEventService>();
        var store    = Substitute.For<IDocumentStore>();

        var controller = new KioskController(kiosk, projects, events, null, store);

        // The direct-construction harness must expose the MVC services
        // Controller.ViewData / Controller.TempData / Controller.View resolve
        // from HttpContext.RequestServices (a real deployment carries them via
        // AddControllers()). The KioskController touches BOTH: the GET actions
        // write ViewData["FullBleed"] / ["Title"] (the Index action, line ~108)
        // and the POST lane writes TempData["info"] on success (line ~313). The
        // M33 AdminStorageMetricsHistoryTests precedent never hit either — it
        // only drove a GET action that returned View(vm) without writing
        // ViewData / TempData. Both Controller.View (the View(vm) result the GET
        // actions build) AND Controller.TempData resolve
        // ITempDataDictionaryFactory + IModelMetadataProvider from DI
        // (AddControllers normally wires them), so register the real MVC
        // service set here. The success path's
        // RedirectToAction("/settings/kiosk") merely CONSTRUCTS a
        // RedirectToActionResult (Url is only touched when the result is
        // executed, which this test never does), so no Url helper is needed:
        var services = new ServiceCollection();
        // AddHttpContextAccessor + AddControllers wire the full MVC service
        // set (ITempDataDictionaryFactory, IModelMetadataProvider,
        // ValidationProblemDetailsService, IHttpContextAccessor, etc.) that
        // Controller.ViewData / Controller.TempData / Controller.View resolve
        // from HttpContext.RequestServices (a real deployment always carries
        // these). The M33 AdminStorageMetricsHistoryTests precedent never hit
        // this — it only drove a GET action that returned View(vm) without
        // writing ViewData / TempData. Register the 4 KioskController
        // dependencies + the in-memory ITempDataProvider before
        // AddControllers (the real TransientsDataDictionaryFactory resolves
        // ITempDataProvider from DI):
        services.AddHttpContextAccessor();
        services.AddSingleton<ITempDataProvider, InMemoryTempDataProvider>();
        // The concrete TempDataDictionaryFactory (Microsoft.AspNetCore.Mvc.
        // ViewFeatures) resolves ITempDataProvider from DI (registered above).
        // Controller.View / Controller.TempData both resolve
        // ITempDataDictionaryFactory from RequestServices:
        services.AddTransient<ITempDataDictionaryFactory, TempDataDictionaryFactory>();
        services.AddSingleton(Substitute.For<IModelMetadataProvider>());
        // The success-path RedirectToAction("/settings/kiosk") resolves
        // IUrlHelperFactory from RequestServices (ControllerBase.get_Url).
        // .NET 10's IUrlHelper (Microsoft.AspNetCore.Mvc.IUrlHelper) methods
        // return System.String (the older UrlValue type is gone), so a plain
        // stub implementation is the cleanest — the compiler enumerates the
        // exact required members (Action(UrlActionContext) / Content /
        // IsLocalUrl / RouteUrl(UrlRouteContext) / Link / the ActionContext
        // property):
        services.AddSingleton<IUrlHelperFactory, StubUrlHelperFactory>();
        services.AddSingleton(kiosk);
        services.AddSingleton(projects);
        services.AddSingleton(events);
        services.AddSingleton(store);
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

        return (controller, kiosk, projects, events, store);
    }

    /// <summary>Read <c>Views/{folder}/{view}</c> as a string (the house
    /// "string pin, no TestServer" idiom — the <see cref="PwaManifestTests"/>
    /// <c>RepoRoot</c> shape), stripping Razor comments (author documentation,
    /// not markup) so the pin matches the rendered markup, not a comment.</summary>
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
        private readonly ConcurrentDictionary<string, object?> _values = new();
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
            foreach (var (key, value) in values) _values[key] = value;
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
        public StubUrlHelper(ActionContext? actionContext)
        {
            ActionContext = actionContext;
        }

        public ActionContext? ActionContext { get; }
        public string Action(UrlActionContext actionContext) => "stub-action";
        public string Content(string content) => content ?? string.Empty;
        public bool IsLocalUrl(string url) => false;
        public string RouteUrl(UrlRouteContext routeContext) => "stub-route";
        public string Link(string linkName, object values) => "stub-link";
    }
}
