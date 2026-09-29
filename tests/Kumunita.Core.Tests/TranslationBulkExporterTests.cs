using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Marten;
using Marten.Services;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M15 · U01 — the 4 pinned <see cref="LocalizationService"/> /
/// <see cref="TranslationBulkExporter"/> facts (design doc
/// <c>m15-translation-bulk-design.md</c> §pinned tests, U01 group, names
/// verbatim; unit plan <c>m15-u01.md</c>). Each test drives the shipped Core
/// seams over a fresh scratch Postgres database (the <see cref
/// "PostgresFixture"/> harness — the same template <see cref
/// "LocalizationServiceTests"/> uses), plus the pure
/// <see cref="TranslationBulkExporter"/> directly (it has no store of its
/// own — C-M15·8, the <c>IcsWriter</c> posture).
/// <para>
/// No new store, no new schema (C-M15·1/8): the matrix is a projection over
/// the frozen <c>TranslationResource</c> rows + the catalog; the frozen read
/// seams (<c>GetTranslationsForAsync</c>, <c>ListLanguagesAsync</c>) are
/// untouched (C-M15·7).
/// </para>
/// </summary>
public class TranslationBulkExporterTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── Bulk_Export_RoundTrips_TheClosedSetMatrix ───────────────────────────
    // The full closed-set matrix (registry keys × catalog languages) exports
    // every non-blank stored text and an empty cell where a row is missing —
    // the matrix is lossless for the closed set (D1/C-M15·2, the §bundle
    // shape: marker, header, body in declaration order).
    [Fact]
    public async Task Bulk_Export_RoundTrips_TheClosedSetMatrix()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        // A stored row for every closed key, in two languages — the full
        // closed-set matrix the §bundle "round-trips" over.
        foreach (var key in KnownTranslationKeys.AllKeys)
        {
            await UpsertTranslation(store, key, "en", $"EN::{key}");
            await UpsertTranslation(store, key, "pl", $"PL::{key}");
        }

        var svc = new LocalizationService(store);
        var matrix = await svc.GetBulkTranslationMatrixAsync();
        var codes = (await svc.ListLanguagesAsync())
            .OrderBy(c => c.SortOrder)
            .Select(c => c.Id)
            .ToList();

        var bundle = TranslationBulkExporter.Build(matrix, codes);
        var lines = bundle.Split("\r\n");

        // Marker + header, verbatim (the §bundle table).
        Assert.Equal(TranslationBulkExporter.BundleMarker, lines[0]);
        Assert.StartsWith("key,source,en,", lines[1]);

        // One body row per closed key, in declaration order.
        var bodyLines = lines[2..^1]; // the trailing CRLF yields a final empty entry
        Assert.Equal(KnownTranslationKeys.AllKeys.Count, bodyLines.Length);

        var keys = KnownTranslationKeys.AllKeys.ToList();
        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            var cells = bodyLines[i].Split(',');
            Assert.Equal(key, cells[0]);
            // Every stored (non-blank) text survives verbatim in its column.
            Assert.Contains($"EN::{key}", cells);
            Assert.Contains($"PL::{key}", cells);
        }
    }

    // ── Bulk_Export_MissingRowIsEmptyCell_NeverNull ──────────────────────────
    // A key with no stored row for a language exports an **empty cell** —
    // never a null in the row's Key/SourceText, never a synthetic row
    // (D1; the M·12 floor).
    [Fact]
    public async Task Bulk_Export_MissingRowIsEmptyCell_NeverNull()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        // Only ONE key has a stored row, and only in `en` — the rest of the
        // closed set has no row in either language.
        var key0 = KnownTranslationKeys.AllKeys.First();
        var key1 = KnownTranslationKeys.AllKeys.Skip(1).First();
        await UpsertTranslation(store, key0, "en", "present-en");

        var svc = new LocalizationService(store);
        var matrix = await svc.GetBulkTranslationMatrixAsync();
        var codes = (await svc.ListLanguagesAsync()).OrderBy(c => c.SortOrder).Select(c => c.Id).ToList();

        // The M·12 floor: the row is present, its Key/SourceText non-null.
        var row1 = matrix.Single(r => r.Key == key1);
        Assert.NotNull(row1.Key);
        Assert.Equal(KnownTranslationKeys.EnValues[key1], row1.SourceText);

        // A missing row → a missing cell (null), never an absent entry and
        // never a synthetic text.
        Assert.Equal((string?)null, row1.Stored["en"]);
        Assert.Equal((string?)null, row1.Stored["pl"]);

        // The §bundle output: the missing cell renders **empty** — the row's
        // cells are `key,<source>,,` (en + pl both blank), never a placeholder.
        var bundle = TranslationBulkExporter.Build(matrix, codes);
        var bodyLines = bundle.Split("\r\n")[2..^1];
        var row1Line = bodyLines.Single(l => l.StartsWith(key1 + ',', System.StringComparison.Ordinal));
        var cells = row1Line.Split(',');
        Assert.Equal(KnownTranslationKeys.EnValues[key1], cells[1]); // source column always present
        Assert.Equal(string.Empty, cells[2]);                       // en → empty cell
        Assert.Equal(string.Empty, cells[3]);                       // pl → empty cell
    }

    // ── Bulk_Export_ColumnsFollowCatalogSortOrder ────────────────────────────
    // A reordered catalog reorders the columns (disabled languages included —
    // D1/D2/F5: the bundle carries the whole set).
    [Fact]
    public async Task Bulk_Export_ColumnsFollowCatalogSortOrder()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);

        // en (SortOrder 0) + two custom codes, seeded in reverse display
        // order; then the catalog is reordered so the header must follow
        // SortOrder, not insertion order.
        await AddLanguage(store, "zz", "Zed");
        await AddLanguage(store, "qq", "Qee");
        var svc = new LocalizationService(store);

        var key0 = KnownTranslationKeys.AllKeys.First();
        await UpsertTranslation(store, key0, "en", "E");
        await UpsertTranslation(store, key0, "zz", "Z");
        await UpsertTranslation(store, key0, "qq", "Q");

        // Reorder: en, qq, zz (the default seed order is en, zz, qq).
        await svc.ReorderLanguagesAsync(new[] { "en", "qq", "zz" }, "admin-seed");

        var matrix = await svc.GetBulkTranslationMatrixAsync();
        var codes = (await svc.ListLanguagesAsync()).OrderBy(c => c.SortOrder).Select(c => c.Id).ToList();
        Assert.Equal(new[] { "en", "qq", "zz" }, codes);

        var bundle = TranslationBulkExporter.Build(matrix, codes);
        var lines = bundle.Split("\r\n");

        // The header follows SortOrder: key,source,en,qq,zz
        Assert.Equal("key,source,en,qq,zz", lines[1]);

        // And the body row's cells follow the same order.
        var row0Line = lines[2];
        var cells = row0Line.Split(',');
        Assert.Equal(key0, cells[0]);
        Assert.Equal("E", cells[2]); // en
        Assert.Equal("Q", cells[3]); // qq
        Assert.Equal("Z", cells[4]); // zz
    }

    // ── Bulk_Export_Pure_NoAuditRow ──────────────────────────────────────────
    // The matrix read emits **zero** <c>AccessAudit</c> rows (C-M15·7; the
    // §bundle "a read" pin) — and the exporter is pure (no store, no
    // session — it cannot emit one).
    [Fact]
    public async Task Bulk_Export_Pure_NoAuditRow()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        var key0 = KnownTranslationKeys.AllKeys.First();
        await UpsertTranslation(store, key0, "en", "E");
        await UpsertTranslation(store, key0, "pl", "P");

        var svc = new LocalizationService(store);

        // The pure half: the exporter runs against a hand-built matrix with
        // no store at all — by construction it can write no audit row.
        var handBuilt = new List<TranslationBulkRow>
        {
            new()
            {
                Key = key0,
                SourceText = KnownTranslationKeys.EnValues[key0],
                Stored = new Dictionary<string, string?> { ["en"] = "E", ["pl"] = "P" },
            },
        };
        var bundle = TranslationBulkExporter.Build(handBuilt, new[] { "en", "pl" });
        Assert.StartsWith(TranslationBulkExporter.BundleMarker, bundle);

        // The read seam: one call, zero audit rows. The assertion scopes to
        // the **translation** surface (TargetKind "translation") — the bulk
        // read may emit none; the `language.add` rows above are from this
        // test's own `AddLanguage` seeding (a different, already-audited seam)
        // and are intentionally out of scope for this pin.
        var matrix = await svc.GetBulkTranslationMatrixAsync();
        Assert.Equal(KnownTranslationKeys.AllKeys.Count, matrix.Count);
        Assert.Empty(await AuditRows(store, targetKind: "translation"));

        // Re-reading is equally audit-free (a repeated read is still a read).
        await svc.GetBulkTranslationMatrixAsync();
        Assert.Empty(await AuditRows(store, targetKind: "translation"));
    }

    // ── Shared helpers (the LocalizationServiceTests template) ───────────────

    private async Task<IDocumentStore> BootStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    private static async Task SeedM1RowAsync(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.OpenSession(new SessionOptions());

        var existingEn = await session.LoadAsync<LanguageCatalog>("en", ct);
        session.Store(existingEn ?? new LanguageCatalog
        {
            Id = "en",
            NativeName = "English",
            Enabled = true,
            SortOrder = 0
        });

        var existingSettings = await session.LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, ct);
        session.Store(existingSettings ?? new LocaleSettings
        {
            Id = LocaleSettings.SingletonId,
            DefaultLanguageCode = "en"
        });

        await session.SaveChangesAsync(ct);
    }

    private static async Task AddLanguage(IDocumentStore store, string code, string nativeName)
    {
        var svc = new LocalizationService(store);
        await svc.AddLanguageAsync(code, nativeName, "admin-seed");
    }

    private static async Task UpsertTranslation(
        IDocumentStore store, string key, string languageCode, string text)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.OpenSession(new SessionOptions());
        var existing = await session
            .Query<TranslationResource>()
            .Where(t => t.Key == key && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync(ct);
        if (existing is null)
            session.Store(new TranslationResource
            {
                Id = Guid.NewGuid().ToString("N"),
                Key = key,
                LanguageCode = languageCode,
                Text = text
            });
        else
        {
            existing.Text = text;
            session.Store(existing);
        }
        await session.SaveChangesAsync(ct);
    }

    private static async Task<IReadOnlyList<AccessAudit>> AuditRows(
        IDocumentStore store, string? targetKind = null)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.QuerySession();
        System.Linq.IQueryable<AccessAudit> query = session.Query<AccessAudit>();
        if (targetKind is not null)
            query = query.Where(a => a.TargetKind == targetKind);
        return await query.OrderBy(a => a.At).ToListAsync(ct);
    }
}
