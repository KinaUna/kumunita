using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// M15 U04 (ADR 0116, D2/D3/D4/D8) — the three pinned Web-surface tests for
/// the translation-bulk **file half** (the <c>bundle.csv</c> download route +
/// the <c>import</c> upload route + the view affordances + the three
/// file-facing <c>kw-l</c> keys). Written against the **shipped** code (U00
/// design lock + U01/U02/U03 Core seams) on the project's direct-construction
/// harness (NSubstitute + <see cref="DefaultHttpContext"/>, no
/// <c>WebApplicationFactory</c> — the M11 D8-3 / ML-UI U8 D8-3 idiom).
///
/// <para>
/// The pins (names **verbatim** from the design doc §pinned-tests):
/// </para>
/// <list type="bullet">
/// <item><see cref="Bulk_Export_Route_ServesCsv_WithAttachmentHeaders"/> —
///   the <c>bundle.csv</c> serve shape (200 + <c>text/csv; charset=utf-8</c>
///   + <c>Content-Disposition: attachment</c> + <c>Cache-Control:
///   no-store</c> + <c>X-Content-Type-Options: nosniff</c> + the body's
///   marker row) and the **one** <c>translation.export</c> audit seam call
///   (the service emits the row — the controller adds none, the ADR 0021
///   idiom).</item>
/// <item><see cref="Bulk_Import_Route_RefusalIs422_And_NoAuditRow"/> — a
///   malformed upload (a wrong marker) → <c>StatusCode(422)</c> + the
///   refusal named in <c>TempData["error"]</c> + <c>UpsertManyTranslationsAsync</c>
///   **not called** (zero writes, no audit row — C-M15·3).</item>
/// <item><see cref="Bulk_Import_Route_Upsert_Saves_ThePresentRows"/> — a
///   well-formed bundle (two rows, one language) → the
///   <c>UpsertManyTranslationsAsync</c> seam is called **once** with exactly
///   those two rows + a redirect (the U02 seam through the route).</item>
/// </list>
///
/// <para>
/// The three file-facing <c>kw-l</c> keys (<c>translations.bulk.export</c> /
/// <c>.import</c> / <c>.import_hint</c>) are pinned present + non-empty in all
/// four languages (D8). The <c>KwLRegistryConsistencyTests</c> /
/// <c>KnownTranslationKeys_ParityTests</c> pins extend automatically with the
/// three new keys (they iterate the registry, not a hardcoded list).
/// </para>
/// </summary>
public class BulkTranslationRouteTests
{
    private const string Actor = "pt-bulk-actor-001";
    private const string Code = "de";

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the result type / headers / the call log
        }
    }

    /// <summary>A minimal <see cref="IFormFile"/> carrier backed by a byte array
    /// (the <see cref="AdminPortabilityControllerTests"/> idiom).</summary>
    private sealed class TestFormFile : IFormFile
    {
        private readonly byte[] _content;

        public TestFormFile(string fileName, string? contentType, byte[] content)
        {
            _content = content ?? Array.Empty<byte>();
            FileName = fileName;
            Name = "bundle";
            ContentType = contentType ?? "text/csv";
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
            => startingOffset switch
            {
                0 => new MemoryStream(_content, false),
                _ => new MemoryStream(_content[(int)startingOffset..], false),
            };

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

    // ── Pin 1 — the bundle.csv serve shape ───────────────────────────────

    [Fact]
    public async Task Bulk_Export_Route_ServesCsv_WithAttachmentHeaders()
    {
        var (controller, localization) = Build(new[]
        {
            new LanguageCatalog { Id = "en", NativeName = "English", Enabled = true, SortOrder = 0 },
            new LanguageCatalog { Id = "de", NativeName = "Deutsch", Enabled = true, SortOrder = 1 },
        });

        // The matrix read (U01 seam, a read, no audit) — one row for a real
        // AllKeys key. KnownTranslationKeys.EnValues is the source floor.
        var key = KnownTranslationKeys.AllKeys.First();
        localization.GetBulkTranslationMatrixAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TranslationBulkRow>>(new[]
            {
                new TranslationBulkRow
                {
                    Key = key,
                    SourceText = KnownTranslationKeys.EnValues[key],
                    Stored = new Dictionary<string, string?>
                    {
                        ["en"] = KnownTranslationKeys.EnValues[key],
                        ["de"] = "Hallo, Nachbar",
                    },
                },
            }));

        var result = await controller.BulkExport(Code);

        // The serve shape (the M12 ADR 0112 precedent — the EventController
        // .ics idiom, the design doc D2/D4 pin).
        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv; charset=utf-8", file.ContentType);

        // The body carries the §bundle marker on line 1 (C-M15·2 round-trip
        // pin — the marker is the format authority, D2 §bundle).
        var body = System.Text.Encoding.UTF8.GetString(file.FileContents);
        Assert.StartsWith(TranslationBulkExporter.BundleMarker, body);

        // The serve headers (ADR 0034 / ADR 0112 — nosniff + no-store +
        // Content-Disposition attachment).
        Assert.Equal("nosniff",
            controller.ControllerContext!.HttpContext.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.Equal("no-store",
            controller.ControllerContext!.HttpContext.Response.Headers["Cache-Control"].ToString());
        Assert.Contains("attachment",
            controller.ControllerContext!.HttpContext.Response.Headers["Content-Disposition"].ToString(),
            StringComparison.OrdinalIgnoreCase);

        // The **one** translation.export audit row (D4 — the M13 analytics-CSV
        // precedent, ADR 0114). The service emits the row; the controller
        // adds none (the ADR 0021 idiom).
        await localization.Received(1).RecordTranslationExportAsync(
            Arg.Any<IReadOnlyList<string>>(), Actor, Arg.Any<CancellationToken>());
    }

    // ── Pin 2 — the refusal is 422 + the audit seam is not called ─────────

    [Fact]
    public async Task Bulk_Import_Route_RefusalIs422_And_NoAuditRow()
    {
        var (controller, localization) = Build(new[]
        {
            new LanguageCatalog { Id = "en", NativeName = "English", Enabled = true, SortOrder = 0 },
            new LanguageCatalog { Id = "de", NativeName = "Deutsch", Enabled = true, SortOrder = 1 },
        });

        // A malformed bundle — a wrong marker (the C-M15·3 fail-closed
        // refusal). The importer names the offending row (the 422 shape).
        const string badBundle =
            "# WRONG-MARKER\r\n" +
            "key,source,en,de\r\n" +
            "nav.home,Home,Home,Startseite\r\n";
        var file = new TestFormFile("bad.csv", "text/csv",
            System.Text.Encoding.UTF8.GetBytes(badBundle));

        var result = await controller.BulkImport(Code, file);

        // 422 + the refusal named (C-M15·3 — the M11 D4 posture).
        Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(422, ((StatusCodeResult)result).StatusCode);

        // The first offending row is named (the design doc §bundle
        // "the 422 refusal names the first offending row" pin).
        var error = Assert.IsType<string>(controller.TempData["error"]);
        Assert.Contains("marker", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("# WRONG-MARKER", error);

        // Zero writes, no audit row (C-M15·3 — the M11 D4 "no audit for the
        // blocked attempt" pin). The UpsertMany seam is **not called**.
        await localization.DidNotReceiveWithAnyArgs().UpsertManyTranslationsAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── Pin 3 — the well-formed upsert saves the present rows ─────────────

    [Fact]
    public async Task Bulk_Import_Route_Upsert_Saves_ThePresentRows()
    {
        var (controller, localization) = Build(new[]
        {
            new LanguageCatalog { Id = "en", NativeName = "English", Enabled = true, SortOrder = 0 },
            new LanguageCatalog { Id = "de", NativeName = "Deutsch", Enabled = true, SortOrder = 1 },
        });

        // A well-formed bundle: the marker, the header (en + de), and two
        // body rows. Only the de column carries non-blank cells (the en
        // column is blank — a no-op, C-M15·4), so the importer returns
        // exactly { "de" → { two rows } }.
        var k1 = KnownTranslationKeys.AllKeys.First();
        var k2 = KnownTranslationKeys.AllKeys.Skip(1).First();
        var bundle =
            TranslationBulkExporter.BundleMarker + "\r\n" +
            "key,source,en,de\r\n" +
            $"{k1},{KnownTranslationKeys.EnValues[k1]},,Hallo\r\n" +
            $"{k2},{KnownTranslationKeys.EnValues[k2]},,Welt\r\n";
        var file = new TestFormFile("ok.csv", "text/csv",
            System.Text.Encoding.UTF8.GetBytes(bundle));

        // The seam returns the upsert count (U02 — exactly one row).
        localization.UpsertManyTranslationsAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(2));

        var result = await controller.BulkImport(Code, file);

        // A redirect (the success shape — the U02 seam through the route).
        Assert.IsType<RedirectToActionResult>(result);

        // The UpsertMany seam is called **once** (C-M15·6 — one audit row
        // per bulk action, the batch lane's shape) with exactly the two
        // present rows for the de language.
        await localization.Received(1).UpsertManyTranslationsAsync(
            "de",
            Arg.Is<IReadOnlyDictionary<string, string>>(rows =>
                rows.Count == 2 &&
                rows[k1] == "Hallo" &&
                rows[k2] == "Welt"),
            Actor,
            Arg.Any<CancellationToken>());

        // The success flash (the _FlashToast surface).
        Assert.NotNull(controller.TempData["info"]);
    }

    // ── D8 — the three file-facing kw-l keys, four-language parity ────────

    [Theory]
    [InlineData("translations.bulk.export")]
    [InlineData("translations.bulk.import")]
    [InlineData("translations.bulk.import_hint")]
    public void KwL_BulkFileFacing_KeysPresent_En(string key)
        => Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.EnValues[key]));

    [Theory]
    [InlineData("translations.bulk.export")]
    [InlineData("translations.bulk.import")]
    [InlineData("translations.bulk.import_hint")]
    public void KwL_BulkFileFacing_KeysPresent_De(string key)
        => Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.DeValues[key]));

    [Theory]
    [InlineData("translations.bulk.export")]
    [InlineData("translations.bulk.import")]
    [InlineData("translations.bulk.import_hint")]
    public void KwL_BulkFileFacing_KeysPresent_Fr(string key)
        => Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.FrValues[key]));

    [Theory]
    [InlineData("translations.bulk.export")]
    [InlineData("translations.bulk.import")]
    [InlineData("translations.bulk.import_hint")]
    public void KwL_BulkFileFacing_KeysPresent_Da(string key)
        => Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.DaValues[key]));
}
