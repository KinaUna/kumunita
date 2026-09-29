using System.Reflection;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Web.Controllers;
using Marten;
using Marten.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// M15 · U06 — the **gate** (design doc
/// <c>m15-translation-bulk-design.md</c> §gate, names **verbatim**) + the
/// **cross-surface pins** U06 adds only because U04/U05's pins do not
/// consolidate them (C-M15·7 byte-identical confirmation + the six
/// <c>translations.bulk.*</c> keys' four-language parity as a single
/// assertion).
/// <para>
/// All three acceptance tests are **full-stack** (the shipped
/// <see cref="LanguagesController"/> routes + the **real**
/// <see cref="LocalizationService"/> + a live Marten store, the
/// <see cref="PostgresFixture"/> shape the GA lane established in this
/// assembly — the controller and the service are the **shipped** U04/U05
/// code, not substitutes, so the closed loop / handoff / part-vs-whole
/// assertions exercise the actual round-trip pair
/// <see cref="TranslationBulkExporter"/> / <see cref="TranslationBulkImporter"/>
/// (C-M15·2)).
/// </para>
/// <para>
/// **Frozen-surface discipline (C-M15·7):** the per-row editor, the
/// <c>SaveTranslation</c> route, <c>UpsertTranslationAsync</c>, and the two
/// parity test classes stay byte-identical — U06's cross-surface pin
/// <see cref="M15_CrossSurface_FrozenSeamsAndRoute_ShapeIntact"/>
/// **confirms** that shape (a reflection + attribute check), it does not
/// reshape it.
/// </para>
/// </summary>
public sealed class M15AcceptanceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Translator = "m15-acceptance-translator-001";
    private const string GlobalAdmin = "m15-acceptance-admin-001";

    // ── 1 — the closed loop ─────────────────────────────────────────────────
    // Ten strings, one <c>save-all</c> (one <c>translation.save_all</c> audit
    // row) → the export bundle carries all ten → a re-import of that bundle
    // leaves the stored matrix **byte-identical** (the round-trip, C-M15·2)
    // — the "check, update, extend as a batch" the roadmap row names,
    // end-to-end (§gate 1).
    [Fact]
    public async Task M15_Acceptance_BatchClosedLoop_TenStringsOneSaveRoundTrips()
    {
        var (controller, svc, store) = await BuildRealAsync();
        var ct = TestContext.Current.CancellationToken;

        // The translator saves **ten** strings in the batch editor: the
        // batch form posts one text input per closed key; ten keys carry
        // values, the rest post blank (a no-op — C-M15·4).
        var tenKeys = KnownTranslationKeys.AllKeys.Take(10).ToList();
        Assert.Equal(10, tenKeys.Count);
        var formFields = new Dictionary<string, StringValues>(System.StringComparer.Ordinal);
        foreach (var key in KnownTranslationKeys.AllKeys)
            formFields[key] = string.Empty;
        for (var i = 0; i < tenKeys.Count; i++)
            formFields[tenKeys[i]] = $"BatchTen::{i}";
        controller.ControllerContext!.HttpContext.Request.Form =
            new FormCollection(formFields, null);

        var saveResult = await controller.SaveAllTranslations("de");
        Assert.IsType<RedirectToActionResult>(saveResult);

        // Exactly **one** translation-surface audit row — the
        // <c>translation.save_all</c> row (one save, C-M15·6 — never ten
        // <c>translation.save</c> rows).
        var saveAllRows = await TranslationAuditRowsAsync(store);
        Assert.Single(saveAllRows);
        var saveAllRow = saveAllRows[0];
        Assert.Equal("translation.save_all", saveAllRow.Action);
        Assert.Equal("de", saveAllRow.TargetId);

        // The export (the same <c>bundle.csv</c> route a resident would
        // click) carries all ten.
        var exportResult = await controller.BulkExport("de");
        var file = Assert.IsType<FileContentResult>(exportResult);
        var bundle = System.Text.Encoding.UTF8.GetString(file.FileContents);
        for (var i = 0; i < tenKeys.Count; i++)
        {
            Assert.Contains(tenKeys[i] + ",", bundle, System.StringComparison.Ordinal);
            Assert.Contains($"BatchTen::{i}", bundle, System.StringComparison.Ordinal);
        }

        // A re-import of that **exact** bundle (the file half, through the
        // shipped route) → the stored matrix is **byte-identical**
        // (C-M15·2: export → import unchanged → the matrix is identical;
        // the ten values are byte-identical and the blank cells stay
        // blank — never a synthetic row, never a deletion).
        var importFile = new BulkTestFormFile("roundtrip.csv", bundle);
        var importResult = await controller.BulkImport("de", importFile);
        Assert.IsType<RedirectToActionResult>(importResult);

        var after = await svc.GetTranslationsForAsync("de");
        for (var i = 0; i < tenKeys.Count; i++)
            Assert.Equal($"BatchTen::{i}", after[tenKeys[i]]);
        foreach (var key in KnownTranslationKeys.AllKeys.Skip(10))
            Assert.False(after.TryGetValue(key, out var v) && v is { Length: > 0 },
                "a blank cell's key must stay row-less (never a synthetic row — C-M15·2)");
    }

    // ── 2 — the handoff (D7: catalog lane → export → fill → import) ─────────
    // A GlobalAdmin adds a custom language (the **existing** catalog lane —
    // no new surface) → the next export shows its column (blank) → a fill of
    // the column imports → the <see cref="ITranslationProvider"/>'s
    // per-request read returns the filled text (the ADR 0015 M·2 fallback
    // intact) — "extend with new languages" is composition of the existing
    // seams, no new lane (§gate 2).
    [Fact]
    public async Task M15_Acceptance_NewLanguageExportFillImport()
    {
        var (controller, svc, store) = await BuildRealAsync();
        var ct = TestContext.Current.CancellationToken;

        // The existing catalog lane (the GlobalAdmin's <c>Add</c> route —
        // M15 adds **zero** catalog surface, D7): a **custom** code (no
        // bundled baseline) starts with an empty translation set.
        var addResult = await controller.Add("xx", "Kumunita");
        Assert.IsType<RedirectToActionResult>(addResult);

        // The next export shows the new column — **blank** (no stored rows
        // for the custom code, D1's empty-cell rule; the catalog's row
        // exists, the bundle carries its column).
        var exportResult = await controller.BulkExport("de");
        var file = Assert.IsType<FileContentResult>(exportResult);
        var bundle = System.Text.Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains(",xx", bundle, System.StringComparison.Ordinal);

        var matrix = await svc.GetBulkTranslationMatrixAsync(ct);
        Assert.All(matrix, row =>
            Assert.True(row.Stored.TryGetValue("xx", out var v) && v is null,
                "the new language's column is a blank cell (no stored row — D1)"));

        // The fill: the bundle's new column carries two values (the
        // translator's file), every other cell blank (a no-op — C-M15·4).
        var k1 = KnownTranslationKeys.AllKeys.First();
        var k2 = KnownTranslationKeys.AllKeys.Skip(1).First();
        var filled =
            TranslationBulkExporter.BundleMarker + "\r\n" +
            "key,source,en,xx\r\n" +
            $"{k1},{KnownTranslationKeys.EnValues[k1]},,XX::{k1}\r\n" +
            $"{k2},{KnownTranslationKeys.EnValues[k2]},,XX::{k2}\r\n";

        var importResult = await controller.BulkImport("de",
            new BulkTestFormFile("filled.csv", filled));
        Assert.IsType<RedirectToActionResult>(importResult);

        // The import landed exactly the two present rows — and the
        // provider's per-request read returns the **filled** text (the
        // ADR 0015 M·2 floor intact: a resident preferring the new
        // language sees the filled string, the en floor would be the
        // fallback otherwise).
        var xxTexts = await svc.GetTranslationsForAsync("xx");
        Assert.Equal($"XX::{k1}", xxTexts[k1]);
        Assert.Equal($"XX::{k2}", xxTexts[k2]);

        var provider = new TranslationProvider(store);
        Assert.Equal($"XX::{k1}",
            await provider.GetAsync(k1, "xx"));
        Assert.Equal($"XX::{k2}",
            await provider.GetAsync(k2, "xx"));
    }

    // ── 3 — part vs. whole (C-M15·3/4) ──────────────────────────────────────
    // A bundle with **one unknown key** is **refused** (422, the key named,
    // **zero** rows touched, **no** audit row) and a **blank cell** in a
    // valid bundle is a **no-op** (the stored row unchanged) — the whole
    // set survives a malformed file, and a file never erases a row
    // (§gate 3).
    [Fact]
    public async Task M15_Acceptance_RefusalLeavesStoreUntouched()
    {
        var (controller, svc, store) = await BuildRealAsync();
        var ct = TestContext.Current.CancellationToken;

        // Pre-seed the <c>de</c> store so "zero rows touched" is
        // assertable: the refusal must leave the stored set
        // byte-identical.
        var k1 = KnownTranslationKeys.AllKeys.First();
        var k2 = KnownTranslationKeys.AllKeys.Skip(1).First();
        await UpsertRowAsync(store, k1, "de", "de-survives");
        await UpsertRowAsync(store, k2, "de", "de-blank-must-survive");

        // (a) The **refusal**: one unknown key in an otherwise valid bundle.
        const string unknownKey = "m15.acceptance.unknown_key";
        var badBundle =
            TranslationBulkExporter.BundleMarker + "\r\n" +
            "key,source,en,de\r\n" +
            $"{k1},{KnownTranslationKeys.EnValues[k1]},,Hallo\r\n" +
            $"{unknownKey},{KnownTranslationKeys.EnValues[k1]},,Nein\r\n";

        var badResult = await controller.BulkImport("de",
            new BulkTestFormFile("bad.csv", badBundle));
        var status = Assert.IsType<StatusCodeResult>(badResult);
        Assert.Equal(422, status.StatusCode);

        // The first offending row is **named** (the key is visible to the
        // resident).
        var error = Assert.IsType<string>(controller.TempData["error"]);
        Assert.Contains(unknownKey, error, System.StringComparison.Ordinal);

        // **Zero** rows touched: the two stored rows are byte-identical.
        var afterRefusal = await svc.GetTranslationsForAsync("de");
        Assert.Equal("de-survives", afterRefusal[k1]);
        Assert.Equal("de-blank-must-survive", afterRefusal[k2]);

        // **No** audit row for the blocked attempt (the M11 D4 pin —
        // C-M15·3).
        Assert.Empty(await TranslationAuditRowsAsync(store));

        // (b) The **no-op**: a blank cell in a *valid* bundle is skipped,
        // never a deletion (C-M15·4). <c>k1</c> is filled (updated),
        // <c>k2</c> is blank (its stored row must survive byte-identical).
        var goodBundle =
            TranslationBulkExporter.BundleMarker + "\r\n" +
            "key,source,en,de\r\n" +
            $"{k1},{KnownTranslationKeys.EnValues[k1]},,Hallo-updated\r\n" +
            $"{k2},{KnownTranslationKeys.EnValues[k2]},,\r\n";

        var goodResult = await controller.BulkImport("de",
            new BulkTestFormFile("good.csv", goodBundle));
        Assert.IsType<RedirectToActionResult>(goodResult);

        var afterNoOp = await svc.GetTranslationsForAsync("de");
        Assert.Equal("Hallo-updated", afterNoOp[k1]);
        Assert.Equal("de-blank-must-survive", afterNoOp[k2]);

        // Exactly **one** <c>translation.import</c> audit row — for the
        // successful import only (the refusal wrote none; C-M15·6).
        var auditRows = await TranslationAuditRowsAsync(store);
        Assert.Single(auditRows);
        Assert.Equal("translation.import", auditRows[0].Action);
        Assert.Equal("1", auditRows[0].TargetId);
    }

    // ── Cross-surface pin — the frozen seams + the frozen route stay the
    //      locked shape (C-M15·7 confirmation, not a reshape) ───────────────
    [Fact]
    public void M15_CrossSurface_FrozenSeamsAndRoute_ShapeIntact()
    {
        // The frozen one-row seam (drift-guard frozen pin #1) — the
        // signature is the locked shape: four strings, a task.
        var upsert = typeof(ILocalizationService).GetMethod(nameof(ILocalizationService.UpsertTranslationAsync))
            ?? throw new MissingMethodException(nameof(ILocalizationService.UpsertTranslationAsync));
        var upsertParams = upsert.GetParameters();
        Assert.Equal(4, upsertParams.Length);
        Assert.Equal(typeof(string), upsertParams[0].ParameterType);
        Assert.Equal(typeof(string), upsertParams[1].ParameterType);
        Assert.Equal(typeof(string), upsertParams[2].ParameterType);
        Assert.Equal(typeof(string), upsertParams[3].ParameterType);

        // The frozen per-row route (drift-guard frozen pin #4) — the
        // POST <c>{code}/translations</c> action with its three form
        // parameters + the anti-forgery gate, byte-identical beside the
        // additive M15 lanes.
        var save = typeof(LanguagesController).GetMethod(nameof(LanguagesController.SaveTranslation))
            ?? throw new MissingMethodException(nameof(LanguagesController.SaveTranslation));
        var saveParams = save.GetParameters();
        Assert.Equal(3, saveParams.Length);
        Assert.Equal(typeof(string), saveParams[0].ParameterType);
        Assert.Equal(typeof(string), saveParams[1].ParameterType);
        Assert.Equal(typeof(string), saveParams[2].ParameterType);
        Assert.NotNull(save.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(save.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    // ── Cross-surface pin — the six <c>translations.bulk.*</c> keys'
    //      four-language parity, as a single consolidated assertion (D8)
    //      (U04/U05 pin their three keys each — the consolidated shape of
    //      the **whole** closed six-key set across **all** four dicts is
    //      not covered by either, so U06 adds it — §gate).
    [Theory]
    [InlineData("translations.bulk.export")]
    [InlineData("translations.bulk.import")]
    [InlineData("translations.bulk.import_hint")]
    [InlineData("translations.bulk.save_all")]
    [InlineData("translations.bulk.mode_batch")]
    [InlineData("translations.bulk.mode_single")]
    public void M15_CrossSurface_BulkKeysFourLanguageParity(string key)
    {
        Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.EnValues[key]),
            $"{key} missing/blank in EnValues");
        Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.DeValues[key]),
            $"{key} missing/blank in DeValues");
        Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.FrValues[key]),
            $"{key} missing/blank in FrValues");
        Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.DaValues[key]),
            $"{key} missing/blank in DaValues");
    }

    // ── Shared harness ──────────────────────────────────────────────────────

    /// <summary>
    /// Boots a real Marten store (the <see cref="PostgresFixture"/> scratch
    /// DB, the GA lane's <c>BootStoreAsync</c> shape in this assembly) + the
    /// **real** <see cref="LocalizationService"/> + the **shipped**
    /// <see cref="LanguagesController"/> (the U04/U05 code under test — the
    /// acceptance tests drive the routes, not substitutes). The actor is a
    /// Translator (the ADR 0021 standing the bulk routes ride) with a
    /// <c>Kumunita.Sub</c> claim.
    /// </summary>
    private async Task<(LanguagesController controller, LocalizationService svc, IDocumentStore store)> BuildRealAsync()
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
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        await using var session = store.OpenSession(new SessionOptions());
        session.Store(new LanguageCatalog
        {
            Id = "en",
            NativeName = "English",
            Enabled = true,
            SortOrder = 0,
        });
        session.Store(new LanguageCatalog
        {
            Id = "de",
            NativeName = "Deutsch",
            Enabled = true,
            SortOrder = 1,
        });
        session.Store(new LocaleSettings
        {
            Id = LocaleSettings.SingletonId,
            DefaultLanguageCode = "en",
        });
        await session.SaveChangesAsync(ct);

        var svc = new LocalizationService(store);
        var controller = new LanguagesController(svc);

        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[]
                {
                    new System.Security.Claims.Claim(ClaimTypes.Subject, Translator),
                    new System.Security.Claims.Claim(ClaimTypes.Role, Roles.Translator),
                },
                authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());

        return (controller, svc, store);
    }

    private static async Task UpsertRowAsync(IDocumentStore store, string key, string languageCode, string text)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.OpenSession(new SessionOptions());
        var existing = await session.Query<TranslationResource>()
            .Where(t => t.Key == key && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync(ct);
        if (existing is null)
        {
            session.Store(new TranslationResource
            {
                Id = Guid.NewGuid().ToString("N"),
                Key = key,
                LanguageCode = languageCode,
                Text = text,
            });
        }
        else
        {
            existing.Text = text;
            session.Store(existing);
        }
        await session.SaveChangesAsync(ct);
    }

    private static async Task<IReadOnlyList<AccessAudit>> TranslationAuditRowsAsync(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.QuerySession();
        return await session.Query<AccessAudit>()
            .Where(a => a.TargetKind == "translation")
            .OrderBy(a => a.At)
            .ToListAsync(ct);
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertions target the result types + the store
        }
    }

    /// <summary>A minimal <see cref="IFormFile"/> carrier backed by a string
    /// (the <see cref="BulkTranslationRouteTests"/> U04 idiom).</summary>
    private sealed class BulkTestFormFile : IFormFile
    {
        private readonly byte[] _content;

        public BulkTestFormFile(string fileName, string text)
        {
            _content = System.Text.Encoding.UTF8.GetBytes(text);
            FileName = fileName;
            Name = "bundle";
            ContentType = "text/csv";
            ContentDisposition = string.Empty;
            Length = _content.Length;
        }

        public long Length { get; }
        public string FileName { get; }
        public string Name { get; }
        public string ContentType { get; set; }
        public string ContentDisposition { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

        public Stream Open() => new MemoryStream(_content, false);
        public Stream OpenReadStream() => new MemoryStream(_content, false);
        public Stream OpenReadStream(long startingOffset)
            => new MemoryStream(_content, false);
        public void CopyTo(Stream target) => target.Write(_content, 0, _content.Length);
        public void CopyTo(Stream target, CancellationToken cancellationToken)
            => target.Write(_content, 0, _content.Length);
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
        {
            target.Write(_content, 0, _content.Length);
            return Task.CompletedTask;
        }
        public void Dispose() { }
    }
}
