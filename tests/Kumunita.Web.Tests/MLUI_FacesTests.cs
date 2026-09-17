using Kumunita.Core;
using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Kumunita.Web.Controllers;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// ML-UI U8 — the FACES gate (the plan's FACES table L1–L9), the Web/controller
/// half. Written against the **shipped** code (U1–U7), on the project's existing
/// direct-construction harness (NSubstitute + <see cref="DefaultHttpContext"/>,
/// no TestServer / no <c>WebApplicationFactory</c> — D8-3).
///
/// <para>
/// <b>L3 (M·1, M·11) — the cookie write (U7's recorded deferral, now closed, D8-4):</b>
/// a signed-out visitor's picker save (<c>POST /language</c>) writes
/// <c>kumunita.locale</c>. U7 deferred this because the direct-construction harness
/// has **no <see cref="ITempDataProvider"/></>, so <c>PublicLocaleController.Save</c>
/// NREs on <c>TempData["info"]</c> before the cookie can be observed. This test
/// supplies a fake <see cref="ITempDataProvider"/> (no-op load/save) on the
/// controller's <c>TempData</c> so the cookie write completes and the
/// <c>Set-Cookie</c> header (the frozen <see cref="LocaleCookie.Write"/> shape) is
/// asserted. The "next request" resolving the <c>pl</c> row is the Core seam's job
/// (L1's <c>provider.GetAsync</c>, in <c>Core.Tests/MLUI_FacesTests.cs</c>).
/// </para>
///
/// <para>
/// <b>L5 (M·4, M·6) — the web half:</b> <see cref="LanguagesController.Translations"/>
/// returns a closed per-key list: exactly <c>KnownTranslationKeys.AllKeys.Count</c>
/// rows, in registry order, each <c>EnReference == EnValues[key]</c>,
/// <c>CurrentValue</c> from the batch read (missing → <c>""</c>), and the model
/// shape has **no free-form key input** (asserted on the types).
/// </para>
///
/// <para>
/// <b>L9 (M·2, M·7) — <c>/about</c>, three branches:</b> (a) no <c>about</c> row in
/// any language → the product-story view (<c>~/Views/StaticPages/About</c> + a
/// <c>HomeViewModel</c>); (b) an <c>en</c> <c>about</c> row present, <c>pl</c>
/// preference, no <c>pl</c> row → the <c>Page</c> view with the <b>en</b> page
/// (per-page fallback, the provider's job); (c) a <c>pl</c> row present → the
/// <c>pl</c> page wins.
/// </para>
///
/// <para>
/// <b>No <c>src</c> file is modified and no package added</b> (D8-3); the
/// <c>Set-Cookie</c> shape asserted here is the frozen <see cref="LocaleCookie"/>
/// (read-only).
/// </para>
/// </summary>
public class MLUI_FacesTests
{
    // ── L3 — a signed-out visitor saves a preference → the cookie is written ──
    // (M·1, M·11.) D8-4: a fake ITempDataProvider lets Save complete so the
    // Set-Cookie header (the frozen LocaleCookie.Write shape) is observable.

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the Set-Cookie header, not the bag
        }
    }

    private static PublicLocaleController BuildPicker(
        IEnumerable<LanguageCatalog> catalog, LocaleSettings? settings)
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.ListLanguagesAsync()
            .Returns(Task.FromResult<IReadOnlyList<LanguageCatalog>>(catalog.ToList()));

        var store = Substitute.For<IDocumentStore>();
        var readSession = Substitute.For<IQuerySession>();
        readSession.LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(settings));
        store.QuerySession().Returns(readSession);

        var controller = new PublicLocaleController(localization, store);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        // D8-4: the U7 deferral — the harness has no ITempDataProvider, so
        // Save's TempData["info"] write NREs before the cookie is observable.
        // A no-op fake (on the Controller's TempData) closes it.
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());
        return controller;
    }

    [Fact(DisplayName = "L3 a signed-out visitor saves a pl preference → the Set-Cookie header is written (next request renders pl)")]
    public void MLUI_U8_L3_SavePreference_WritesLocaleCookie()
    {
        var controller = BuildPicker(
            new[]
            {
                new LanguageCatalog { Id = "en", NativeName = "English", Enabled = true, SortOrder = 0 },
                new LanguageCatalog { Id = "pl", NativeName = "Polski",  Enabled = true, SortOrder = 1 },
            },
            settings: new LocaleSettings { DefaultLanguageCode = "en" });

        // POST /language — a signed-out visitor (no authz; the action is public).
        var result = controller.Save(code: "pl", clear: null);
        Assert.IsType<RedirectToActionResult>(result);

        // The frozen LocaleCookie.Write shape: kumunita.locale=pl, HttpOnly,
        // SameSite=Lax (the L3 assertion target — M·11).
        var setCookie = controller.ControllerContext!.HttpContext.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains("kumunita.locale=pl", setCookie);
        Assert.Contains("HttpOnly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SameSite=Lax", setCookie, StringComparison.OrdinalIgnoreCase);
        // The "next request" resolving the pl row is the Core seam's job
        // (provider.GetAsync over the seeded registry — L1 in Core.Tests/MLUI_FacesTests.cs);
        // D8-2 exercises it at the cookie→provider seam rather than rendering Razor.
    }

    // ── L3 branch: clear=1 deletes the preference ──────────────────────────

    [Fact(DisplayName = "L3 clear=1 branch — the locale cookie is deleted (reset to default)")]
    public void MLUI_U8_L3_ClearPreference_DeletesCookie()
    {
        var controller = BuildPicker(
            new[]
            {
                new LanguageCatalog { Id = "en", NativeName = "English", Enabled = true, SortOrder = 0 },
            },
            settings: new LocaleSettings { DefaultLanguageCode = "en" });

        var result = controller.Save(code: null, clear: "1");
        Assert.IsType<RedirectToActionResult>(result);

        // LocaleCookie.Clear deletes the cookie. The frozen shape signals the
        // deletion via an epoch Expires (the CookieOptions Delete idiom) — the
        // preference is reset to the instance default.
        var setCookie = controller.ControllerContext!.HttpContext.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains("kumunita.locale", setCookie);
        Assert.Contains("expires=Thu, 01 Jan 1970", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    // ── L5 (web half) — the closed per-key editor list, no hand-typed key ──
    // (M·4, M·6.) D8-6: drive LanguagesController.Translations with a
    // substituted ILocalizationService; assert the closed model shape.

    [Fact(DisplayName = "L5 the editor list is the closed registry — AllKeys in order, en reference, current value, no free-form key")]
    public async Task MLUI_U8_L5_EditorListIsClosedRegistry()
    {
        var localization = Substitute.For<ILocalizationService>();
        // The batch read: exactly two registry keys have a pl value (the rest → "").
        localization.GetTranslationsForAsync("pl").Returns(Task.FromResult<IReadOnlyDictionary<string, string>>(
            new Dictionary<string, string>
            {
                ["nav.home"] = "Strona główna",
                ["nav.groups"] = "Grupy",
            }));

        var controller = new LanguagesController(localization);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await controller.Translations("pl");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LanguagesController.TranslationEditorViewModel>(view.ViewData.Model);
        Assert.Equal("pl", model.Code);

        var allKeys = KnownTranslationKeys.AllKeys.ToList();
        // Exactly the closed registry — one row per key, in registry order.
        Assert.Equal(allKeys.Count, model.Rows.Count);
        Assert.Equal(allKeys, model.Rows.Select(r => r.Key).ToList());

        foreach (var row in model.Rows)
        {
            // The en reference is the registry's source text (by reference).
            Assert.Equal(KnownTranslationKeys.EnValues[row.Key], row.EnReference);
        }
        // CurrentValue from the batch read (present → value; missing → "").
        Assert.Equal("Strona główna", model.Rows.Single(r => r.Key == "nav.home").CurrentValue);
        Assert.Equal("Grupy", model.Rows.Single(r => r.Key == "nav.groups").CurrentValue);
        Assert.Equal(string.Empty, model.Rows.Single(r => r.Key == "nav.directory").CurrentValue);

        // No free-form key input in the model shape (L5's "no hand-typed key"): the
        // editor view model is exactly the closed list (Code + Rows) — there is no
        // additional string-input property for an admin to hand-type a key into.
        var editorProps = typeof(LanguagesController.TranslationEditorViewModel)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new[] { "Code", "Rows" }, editorProps);
    }

    // ── L9 — /about (absent → product-story; present → the shared Page view) ─
    // (M·2, M·7.) PG U07: the controller is tree-only — the Page tree is the
    // single store. The two branches here: absent (the U05 drift pin — `about`
    // is not seeded) degrades to the product-story view; present renders the
    // shared Page view. (The per-page language fallback is the Page tree's
    // job, covered in the ML-UI U7 Core surface, not the controller.)

    private static (StaticPagesController controller, IPageService pages) BuildAbout(Page? page)
    {
        var pages = Substitute.For<IPageService>();

        if (page is null)
        {
            pages.GetByPathAsync(Arg.Any<string>())
                .Returns(Task.FromException<Page>(new KeyNotFoundException()));
        }
        else
        {
            pages.GetByPathAsync("about").Returns(page);
        }

        var controller = new StaticPagesController(
            pages,
            Options.Create(new CommunityOptions { Name = "Maplewood", SupportEmail = "maps@example.com" }));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        return (controller, pages);
    }

    [Fact(DisplayName = "L9a no about page in the tree → the product-story view (Home/About + HomeViewModel)")]
    public async Task MLUI_U8_L9_AboutAbsent_ProductStoryView()
    {
        var (controller, _) = BuildAbout(page: null);

        var result = await controller.About();

        // L9's "truly absent" branch: a fresh instance's /about is the product pitch.
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("About", view.ViewName);
        var vm = Assert.IsType<Models.HomeViewModel>(view.ViewData.Model);
        Assert.Equal("Maplewood", vm.CommunityName);
    }

    [Fact(DisplayName = "L9b an about page present in the tree → the shared Page view")]
    public async Task MLUI_U8_L9_AboutPagePresent_RendersPageView()
    {
        var (controller, _) = BuildAbout(page: new Page
        {
            Id = "page-about-001",
            ParentId = null,
            Slug = "about",
            Title = "O nas",
            Body = "Jesteśmy Twoim sąsiedztwem.",
            LanguageCode = "pl",
            Audience = null,
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow,
        });

        var result = await controller.About();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Page", view.ViewName);
        var model = Assert.IsType<StaticPagesController.StaticPageViewModel>(view.ViewData.Model);
        Assert.Equal("about", model.Slug);
        Assert.Equal("O nas", model.Title);
    }
}
