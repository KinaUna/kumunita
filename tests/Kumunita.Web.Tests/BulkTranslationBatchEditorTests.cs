using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Primitives;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// M15 U05 (ADR 0116, D6/D8) — the two pinned Web-surface tests for the
/// translation-bulk **batch half** (the <c>?batch=true</c> mode on the
/// existing <c>Translations</c> view + the <c>save-all</c> route + the three
/// editor-facing <c>kw-l</c> keys). Written against the **shipped** code
/// (U00 design lock + U01/U02/U03 Core seams + U04 file half) on the project's
/// direct-construction harness (NSubstitute + <see cref="DefaultHttpContext"/>,
/// no <c>WebApplicationFactory</c> — the M11 D8-3 / ML-UI U8 D8-3 idiom, the
/// <see cref="BulkTranslationRouteTests"/> U04 shape).
///
/// <para>
/// The pins (names **verbatim** from the design doc §pinned-tests):
/// </para>
/// <list type="bullet">
/// <item><see cref="Bulk_SaveAll_Route_SavesThePresentRows_OneAuditRow"/> —
///   a batch POST of three rows → <see cref="ILocalizationService.SaveAllTranslationsAsync"/>
///   called **exactly once** with exactly those three rows + the language
///   code as the <c>TargetId</c> carrier + the actor (the one
///   <c>translation.save_all</c> audit row is the service's — C-M15·6, the
///   ADR 0021 idiom: the controller adds none) + a redirect + the success
///   flash.</item>
/// <item><see cref="Bulk_BatchMode_Toggle_Renders_TheClosedKeyList"/> — the
///   <c>?batch=true</c> view model is <c>Mode = Batch</c> and lists exactly
///   <see cref="KnownTranslationKeys.AllKeys.Count"/> rows (one input per
///   closed key) + the view carries the mode toggle (two kw-l links) and the
///   batch form; the default view model is <c>Mode = Single</c> with the
///   same closed row list (the per-row lane intact — C-M15·7).</item>
/// </list>
///
/// <para>
/// The three editor-facing <c>kw-l</c> keys (<c>translations.bulk.
/// save_all</c> / <c>.mode_batch</c> / <c>.mode_single</c>) are pinned
/// present + non-empty in all four languages (D8). The
/// <c>KwLRegistryConsistencyTests</c> / <c>KnownTranslationKeys_ParityTests</c>
/// pins extend automatically with the three new keys (they iterate the
/// registry, not a hardcoded list).
/// </para>
/// </summary>
public class BulkTranslationBatchEditorTests
{
    private const string Actor = "pt-bulk-actor-002";
    private const string Code = "pl";

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the result type / the call log
        }
    }

    private static (LanguagesController controller, ILocalizationService localization) Build(
        IEnumerable<LanguageCatalog> catalog)
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.ListLanguagesAsync()
            .Returns(Task.FromResult<IReadOnlyList<LanguageCatalog>>(catalog.ToList()));

        var controller = new LanguagesController(localization);
        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[]
                {
                    new System.Security.Claims.Claim(ClaimTypes.Subject, Actor),
                    new System.Security.Claims.Claim(ClaimTypes.Role, Roles.Translator),
                },
                authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(
            new DefaultHttpContext(), new NoOpTempDataProvider());
        return (controller, localization);
    }

    // ── Pin 1 — the save-all route: three rows, one seam call, one audit row ──

    [Fact]
    public async Task Bulk_SaveAll_Route_SavesThePresentRows_OneAuditRow()
    {
        var (controller, localization) = Build(new[]
        {
            new LanguageCatalog { Id = "en", NativeName = "English", Enabled = true, SortOrder = 0 },
            new LanguageCatalog { Id = "pl", NativeName = "Polski", Enabled = true, SortOrder = 1 },
        });

        // The batch form posts one text input per closed key (the input's
        // name is the key). Three keys carry non-blank values (the "present
        // rows" the pin names); every other key posts blank (a no-op for the
        // service — C-M15·4).
        var keys = KnownTranslationKeys.AllKeys;
        var k1 = keys.First();
        var k2 = keys.Skip(1).First();
        var k3 = keys.Skip(2).First();

        var formFields = new Dictionary<string, StringValues>(System.StringComparer.Ordinal);
        foreach (var key in keys)
            formFields[key] = string.Empty;
        formFields[k1] = "Cześć";
        formFields[k2] = "Witaj";
        formFields[k3] = "Dzień dobry";
        controller.ControllerContext!.HttpContext.Request.Form =
            new FormCollection(formFields, null);
        // The seam returns the count of rows upserted (U03 — the audit row's
        // count is the service's; the route's flash uses the return value).
        localization.SaveAllTranslationsAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(3));

        var result = await controller.SaveAllTranslations(Code);

        // A redirect (the success shape — the U03 seam through the route).
        Assert.IsType<RedirectToActionResult>(result);

        // The SaveAll seam is called **exactly once** (C-M15·6 — one
        // translation.save_all audit row per bulk action, the batch lane's
        // shape: the service emits the row, the controller adds none — the
        // ADR 0021 idiom). The call's language code is the audit row's
        // TargetId carrier (the U03 seam contract — the language code,
        // unlike translation.import's count).
        await localization.Received(1).SaveAllTranslationsAsync(
            Code,
            Arg.Is<IReadOnlyDictionary<string, string>>(rows =>
                rows.Count == KnownTranslationKeys.AllKeys.Count &&
                rows[k1] == "Cześć" &&
                rows[k2] == "Witaj" &&
                rows[k3] == "Dzień dobry"),
            Actor,
            Arg.Any<CancellationToken>());

        // The frozen per-row lane is **not called** — the batch save does
        // not loop the one-row seam (C-M15·6 — the batch is one action, not
        // N per-row actions; C-M15·7 — the frozen seam stays the per-row
        // lane, untouched).
        await localization.DidNotReceiveWithAnyArgs().UpsertTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());

        // The success flash (the _FlashToast surface).
        Assert.NotNull(controller.TempData["info"]);
    }

    // ── Pin 2 — the batch-mode toggle + the closed key list, both modes ────

    [Fact]
    public async Task Bulk_BatchMode_Toggle_Renders_TheClosedKeyList()
    {
        // The D6 read (missing key → empty string, never null — the
        // M·12 floor the per-row editor already pins): the seam returns an
        // empty map, so every row's CurrentValue is the empty string — the
        // pin asserts the **shape** (the closed row list + the mode flag),
        // not the values.
        var emptyValues = new Dictionary<string, string>(System.StringComparer.Ordinal);
        IReadOnlyDictionary<string, string> values = emptyValues;

        var (controller, localization) = Build(new[]
        {
            new LanguageCatalog { Id = "en", NativeName = "English", Enabled = true, SortOrder = 0 },
            new LanguageCatalog { Id = "pl", NativeName = "Polski", Enabled = true, SortOrder = 1 },
        });
        localization.GetTranslationsForAsync(Arg.Any<string>())
            .Returns(Task.FromResult(values));

        // Default mode — the flag absent: the per-row editor renders
        // byte-identical (C-M15·7). The view model carries the same closed
        // row list + Mode = Single.
        var singleResult = Assert.IsType<ViewResult>(
            await controller.Translations(Code, batch: false));
        var singleVm = Assert.IsType<LanguagesController.TranslationEditorViewModel>(
            singleResult.ViewData.Model);
        Assert.Equal(LanguagesController.TranslationEditorMode.Single, singleVm.Mode);
        Assert.Equal(KnownTranslationKeys.AllKeys.Count, singleVm.Rows.Count);
        Assert.Equal(KnownTranslationKeys.AllKeys, singleVm.Rows.Select(r => r.Key));

        // Batch mode — ?batch=true: the same closed key list (D1 — one view,
        // two modes), one input per row in the batch form + the toggle.
        var batchResult = Assert.IsType<ViewResult>(
            await controller.Translations(Code, batch: true));
        var batchVm = Assert.IsType<LanguagesController.TranslationEditorViewModel>(
            batchResult.ViewData.Model);
        Assert.Equal(LanguagesController.TranslationEditorMode.Batch, batchVm.Mode);
        Assert.Equal(KnownTranslationKeys.AllKeys.Count, batchVm.Rows.Count);
        Assert.Equal(KnownTranslationKeys.AllKeys, batchVm.Rows.Select(r => r.Key));

        // The view file itself carries the U05 surface (the Razor layer —
        // the house harness never executes views, so the view's contract is
        // pinned by its source: the two mode-toggle kw-l links, the
        // batch-mode gate on Model.Mode, the batch form posting to the
        // save-all route, and the byte-identical per-row editor lane
        // (C-M15·7) — the per-row form's action + its one input per row).
        var viewPath = Path.Combine(
            FindProjectDir(), "src", "Kumunita.Web", "Views", "Languages", "Translations.cshtml");
        var viewSource = await File.ReadAllTextAsync(
            viewPath, TestContext.Current.CancellationToken);

        // The quiet top toggle — two kw-l links, plain <a>, no JS (D6).
        Assert.Contains("translations.bulk.mode_single", viewSource);
        Assert.Contains("translations.bulk.mode_batch", viewSource);
        // The ?batch=true link (the batch side of the toggle).
        Assert.Contains("?batch=true", viewSource);

        // The batch form — one text input per row (name = the closed key)
        // + the one "Save all" button + the save-all route (D6).
        Assert.Contains("translations/save-all", viewSource);
        Assert.Contains("translations.bulk.save_all", viewSource);
        Assert.Contains("name=\"@row.Key\"", viewSource);

        // The batch block is gated on the mode flag (the per-row editor
        // renders byte-identical when the flag is absent — C-M15·7).
        Assert.Contains("Model.Mode", viewSource);
        Assert.Contains("TranslationEditorMode.Batch", viewSource);

        // The per-row lane intact (C-M15·7) — the frozen SaveTranslation
        // form posts to the per-row route (unchanged by U05).
        Assert.Contains("/admin/languages/@Model.Code/translations\" class=\"d-flex gap-2\"", viewSource);
    }

    // ── D8 — the three editor-facing kw-l keys, four-language parity ───────

    [Theory]
    [InlineData("translations.bulk.save_all")]
    [InlineData("translations.bulk.mode_batch")]
    [InlineData("translations.bulk.mode_single")]
    public void KwL_BulkEditorFacing_KeysPresent_En(string key)
        => Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.EnValues[key]));

    [Theory]
    [InlineData("translations.bulk.save_all")]
    [InlineData("translations.bulk.mode_batch")]
    [InlineData("translations.bulk.mode_single")]
    public void KwL_BulkEditorFacing_KeysPresent_De(string key)
        => Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.DeValues[key]));

    [Theory]
    [InlineData("translations.bulk.save_all")]
    [InlineData("translations.bulk.mode_batch")]
    [InlineData("translations.bulk.mode_single")]
    public void KwL_BulkEditorFacing_KeysPresent_Fr(string key)
        => Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.FrValues[key]));

    [Theory]
    [InlineData("translations.bulk.save_all")]
    [InlineData("translations.bulk.mode_batch")]
    [InlineData("translations.bulk.mode_single")]
    public void KwL_BulkEditorFacing_KeysPresent_Da(string key)
        => Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.DaValues[key]));

    private static string FindProjectDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.EnumerateFiles("Kumunita.slnx").Any())
            dir = dir.Parent;
        if (dir is null)
            throw new DirectoryNotFoundException("Kumunita.slnx not found above the test bin dir.");
        return dir.FullName;
    }
}
