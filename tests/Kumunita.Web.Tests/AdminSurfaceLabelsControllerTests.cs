using Kumunita.Core.Identity;
using Kumunita.Core.SurfaceLabels;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using System.Security.Claims;
using System.Reflection;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// M29 U09 (ADR 0152) — the <c>/admin/labels</c> write-lane pins
/// (M29·5 / M29·7): the GET seeds the 13-row form with the current
/// singleton's <b>stored</b> label set; the POST saves the full 13-field
/// set and delegates to the service's single audited write lane; a
/// non-<c>GlobalAdmin</c> is denied (the <c>[Authorize(Roles = GlobalAdmin)]</c>
/// gate); and a fresh instance's GET shows all 13 labels blank (the all-null
/// fallback, M29·1 / M29·4 / M29·6).
/// </summary>
/// <para>
/// <b>Test model:</b> direct-construction (NSubstitute
/// <c>ISurfaceLabelsService</c> seam, the <see cref="AdminSiteControllerTests"/>
/// shape). The audit-row assertion lives in
/// <see cref="SurfaceLabelsServiceTests.SaveAsync_WritesOneAccessAuditRow"/>
/// (the service owns it); the controller is tested as the thin seam — the
/// call log is the assertion target (the <c>…_WritesOneAccessAuditRow</c>
/// name asserts the save + delegate-to-write-lane shape). The
/// <c>actorBy</c> is <c>KumunitaPrincipal.SubjectId(User) ?? string.Empty</c>
/// (the <see cref="Kumunita.Web.Security.KumunitaPrincipal"/> idiom).
/// </para>
public class AdminSurfaceLabelsControllerTests
{
    private const string Admin = "admin-labels-u09";

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect / the call log, not the bag
        }
    }

    private static (AdminSurfaceLabelsController controller, ISurfaceLabelsService labels) Build(
        SurfaceLabels? current,
        IReadOnlyList<SurfaceLabelTranslation>? translations = null)
    {
        var labels = Substitute.For<ISurfaceLabelsService>();
        labels.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(current ?? new SurfaceLabels()));
        labels.GetTranslationsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(translations ?? new List<SurfaceLabelTranslation>()));

        var controller = new AdminSurfaceLabelsController(labels);

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Admin),
                    new Claim(Kumunita.Core.Identity.ClaimTypes.Role, Roles.GlobalAdmin),
                },
                authenticationType: "test"));

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());
        return (controller, labels);
    }

    // ── 1 — the GET seeds the form with the current singleton (M29·7) ─────

    [Fact(DisplayName = "U09 AdminLabels GET seeds the 13-row form with the current singleton")]
    public async Task GET_SeesCurrentSingleton()
    {
        var current = new SurfaceLabels
        {
            Home        = "Front",
            Announcements = "News",
            Community   = "Our street",
            Groups      = "Boards",
            Events      = "Happenings",
            Projects    = "Board",
            Inventory   = "Shared things",
            Bookmarks   = "Saved",
            Documents   = "Files",
            Pages       = "Notes",
            Tags        = "Topics",
            Directory   = "Who's here",
            People      = "Neighbours",
        };

        var (controller, labels) = Build(current);

        var action = await controller.Index();
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<AdminSurfaceLabelsController.AdminSurfaceLabelsViewModel>(view.ViewData.Model);

        // All 13 fields are seeded from the stored singleton (M29·7).
        Assert.Equal("Front", vm.Home);
        Assert.Equal("News", vm.Announcements);
        Assert.Equal("Our street", vm.Community);
        Assert.Equal("Boards", vm.Groups);
        Assert.Equal("Happenings", vm.Events);
        Assert.Equal("Board", vm.Projects);
        Assert.Equal("Shared things", vm.Inventory);
        Assert.Equal("Saved", vm.Bookmarks);
        Assert.Equal("Files", vm.Documents);
        Assert.Equal("Notes", vm.Pages);
        Assert.Equal("Topics", vm.Tags);
        Assert.Equal("Who's here", vm.Directory);
        Assert.Equal("Neighbours", vm.People);

        // The service read seam was called (the GET seeds from GetAsync).
        await labels.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    // ── 2 — the POST saves the label set + delegates to the write lane (M29·5) ──

    [Fact(DisplayName = "U09 AdminLabels POST saves the 13-field label set and delegates to the audited write lane")]
    public async Task POST_Save_SavesLabel_WritesOneAccessAuditRow()
    {
        // A fresh current singleton (the admin is changing the labels).
        var (controller, labels) = Build(current: null);

        var model = new AdminSurfaceLabelsController.AdminSurfaceLabelsViewModel
        {
            Home        = "Front",
            Announcements = "News",
            Community   = "Our street",
            Groups      = "Boards",
            Events      = "Happenings",
            Projects    = "Board",
            Inventory   = "Shared things",
            Bookmarks   = "Saved",
            Documents   = "Files",
            Pages       = "Notes",
            Tags        = "Topics",
            Directory   = "Who's here",
            People      = "Neighbours",
        };

        var action = await controller.Save(model);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminSurfaceLabelsController.Index), redirect.ActionName);

        // The service's single audited write lane was called exactly once
        // (M29·5). The audit-row assertion lives in
        // SurfaceLabelsServiceTests.SaveAsync_WritesOneAccessAuditRow
        // (the service owns it) — the controller is the thin seam.
        await labels.Received(1).SaveAsync(
            Arg.Is<SurfaceLabels>(s => s.Announcements == "News" && s.People == "Neighbours"),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    // ── 3 — a non-GlobalAdmin is denied (M29·7) ────────────────────────────
    // The [Authorize(Roles = GlobalAdmin)] attribute is the gate. In a
    // direct-construction harness (no TestServer), the attribute is asserted
    // via reflection — the ASP.NET Core authorization policy denies a
    // non-GlobalAdmin principal at the middleware layer.

    [Fact(DisplayName = "U09 AdminLabels is GlobalAdmin-gated (the [Authorize(Roles = GlobalAdmin)] pin)")]
    public void POST_NonGlobalAdmin_IsDenied()
    {
        var attr = typeof(AdminSurfaceLabelsController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.NotNull(attr.Roles);
        Assert.Contains(Roles.GlobalAdmin, attr.Roles.Split(','));
    }

    // ── 4 — a fresh instance's GET shows all 13 labels blank (M29·1/M29·4/M29·6) ──

    [Fact(DisplayName = "U09 AdminLabels fresh-instance GET shows all 13 labels blank (the all-null fallback)")]
    public async Task GET_FreshInstance_AllLabelsBlank()
    {
        // The substitute returns the in-code all-null fallback (a missing row
        // degrades to all-null — M29·4 / M29·6, the GetAsync contract).
        var (controller, labels) = Build(current: new SurfaceLabels());

        var action = await controller.Index();
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<AdminSurfaceLabelsController.AdminSurfaceLabelsViewModel>(view.ViewData.Model);

        // A fresh instance: every label field is blank (null) — the nav +
        // header fall back to each surface's kw-l key at resolution (M29·1/M29·4).
        Assert.Null(vm.Home);
        Assert.Null(vm.Announcements);
        Assert.Null(vm.Community);
        Assert.Null(vm.Groups);
        Assert.Null(vm.Events);
        Assert.Null(vm.Projects);
        Assert.Null(vm.Inventory);
        Assert.Null(vm.Bookmarks);
        Assert.Null(vm.Documents);
        Assert.Null(vm.Pages);
        Assert.Null(vm.Tags);
        Assert.Null(vm.Directory);
        Assert.Null(vm.People);
    }

    // ── ADR 0158 — the LBL-2 surface-label translation write-lane pins ─────
    // The three dedicated POST actions (add / update / remove) delegate to
    // the service's single audited write lanes
    // (surface_labels_translation.add / .update / .remove); the GlobalAdmin
    // gate is the standing (the ADR 0152 D8 pin — the surface labels have no
    // per-resident owner, so the Translator standing does not qualify, ADR
    // 0021). The service's audit-row shape is pinned in
    // SurfaceLabelsServiceTests; here the controller is the thin seam (the
    // call log is the assertion target). The exact
    // AdminSiteControllerTests translation-pin idiom (ADR 0157) carried over.

    // ── 5 — the POST /translations adds a translation + delegates ──────────

    [Fact(DisplayName = "ADR0158 AdminLabels POST /translations adds a translation and redirects")]
    public async Task POST_AddTranslation_Delegates_WritesRedirect()
    {
        var (controller, labels) = Build(null);

        var form = new AdminSurfaceLabelsController.SurfaceLabelTranslationForm
        {
            LanguageCode = "de",
            Announcements = "Meldungen",
            People        = "Nachbarn",
        };
        var action = await controller.AddTranslation(form);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminSurfaceLabelsController.Index), redirect.ActionName);

        await labels.Received(1).AddTranslationAsync(
            "de",
            Arg.Is<SurfaceLabelTranslation>(t => t.Announcements == "Meldungen" && t.People == "Nachbarn"),
            Admin);
    }

    // ── 6 — the POST /translations validation: all blank ───────────────────

    [Fact(DisplayName = "ADR0158 AdminLabels POST /translations rejects a fully-blank translation")]
    public async Task POST_AddTranslation_AllBlank_Rejected_NoServiceCall()
    {
        var (controller, labels) = Build(null);

        var form = new AdminSurfaceLabelsController.SurfaceLabelTranslationForm
        {
            LanguageCode = "de",
            Home = " ",
        };
        var action = await controller.AddTranslation(form);
        Assert.IsType<RedirectToActionResult>(action);

        // No write lane was called (the validation short-circuits).
        await labels.DidNotReceiveWithAnyArgs().AddTranslationAsync(
            Arg.Any<string>(), Arg.Any<SurfaceLabelTranslation>(), Arg.Any<string>());
    }

    [Fact(DisplayName = "ADR0158 AdminLabels POST /translations rejects a missing language")]
    public async Task POST_AddTranslation_NoLanguage_Rejected_NoServiceCall()
    {
        var (controller, labels) = Build(null);

        var form = new AdminSurfaceLabelsController.SurfaceLabelTranslationForm
        {
            LanguageCode = null,
            Home = "Erste",
        };
        var action = await controller.AddTranslation(form);
        Assert.IsType<RedirectToActionResult>(action);

        await labels.DidNotReceiveWithAnyArgs().AddTranslationAsync(
            Arg.Any<string>(), Arg.Any<SurfaceLabelTranslation>(), Arg.Any<string>());
    }

    // ── 7 — the POST /translations/update edits a translation ──────────────

    [Fact(DisplayName = "ADR0158 AdminLabels POST /translations/update updates a translation and redirects")]
    public async Task POST_UpdateTranslation_Delegates_WritesRedirect()
    {
        var (controller, labels) = Build(null);

        var form = new AdminSurfaceLabelsController.SurfaceLabelTranslationForm
        {
            LanguageCode = "de",
            Home = "Zweite",
            People = "Nachbarn",
        };
        var action = await controller.UpdateTranslation(form);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminSurfaceLabelsController.Index), redirect.ActionName);

        await labels.Received(1).UpdateTranslationAsync(
            "de",
            Arg.Is<SurfaceLabelTranslation>(t => t.Home == "Zweite" && t.People == "Nachbarn"),
            Admin);
    }

    // ── 8 — the POST /translations/update missing-row shape is a flash ─────

    [Fact(DisplayName = "ADR0158 AdminLabels POST /translations/update on a missing row flashes + redirects")]
    public async Task POST_UpdateTranslation_MissingRow_Flashes_Redirects()
    {
        var (controller, labels) = Build(null);

        // A missing-row shape (the service's KeyNotFoundException) is caught by
        // the controller and surfaced as a flash + redirect (not an exception).
        labels.UpdateTranslationAsync(
            Arg.Any<string>(), Arg.Any<SurfaceLabelTranslation>(), Arg.Any<string>())
            .Returns(throwingTask<SurfaceLabelTranslation>());

        var form = new AdminSurfaceLabelsController.SurfaceLabelTranslationForm
        {
            LanguageCode = "xx",
            Home = "eyebrow",
        };
        var action = await controller.UpdateTranslation(form);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminSurfaceLabelsController.Index), redirect.ActionName);
    }

    // ── 9 — the POST /translations/remove removes a translation ────────────

    [Fact(DisplayName = "ADR0158 AdminLabels POST /translations/remove removes a translation and redirects")]
    public async Task POST_RemoveTranslation_Delegates_WritesRedirect()
    {
        var (controller, labels) = Build(null);

        var action = await controller.RemoveTranslation("de");
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminSurfaceLabelsController.Index), redirect.ActionName);

        await labels.Received(1).RemoveTranslationAsync("de", Admin);
    }

    // ── 10 — the POST /translations/remove missing-row shape is a flash ────

    [Fact(DisplayName = "ADR0158 AdminLabels POST /translations/remove on a missing row flashes + redirects")]
    public async Task POST_RemoveTranslation_MissingRow_Flashes_Redirects()
    {
        var (controller, labels) = Build(null);
        labels.RemoveTranslationAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(throwingTask());

        var action = await controller.RemoveTranslation("xx");
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminSurfaceLabelsController.Index), redirect.ActionName);
    }

    /// <summary>A <c>Task</c> that throws on await — the
    /// <see cref="KeyNotFoundException"/> shape the controller catches on the
    /// missing-row path. The generic form returns a <c>Task&lt;T&gt;</c>; the
    /// non-generic form a plain <c>Task</c> (for
    /// <c>RemoveTranslationAsync</c>, which returns <c>Task</c>).</summary>
    private static Task<T> throwingTask<T>()
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        tcs.SetException(new KeyNotFoundException("missing"));
        return tcs.Task;
    }

    private static Task throwingTask()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        tcs.SetException(new KeyNotFoundException("missing"));
        return tcs.Task;
    }
}
