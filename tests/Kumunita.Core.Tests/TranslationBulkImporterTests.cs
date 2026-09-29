using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Marten;
using Marten.Services;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M15 · U02 — the 5 pinned <see cref="LocalizationService"/> /
/// <see cref="TranslationBulkImporter"/> facts (design doc
/// <c>m15-translation-bulk-design.md</c> §pinned tests, U02 group, names
/// verbatim; unit plan <c>m15-u02.md</c>).
/// <para>
/// The tests cover both halves of the U02 deliverable:
/// <list type="bullet">
///   <item>The <b>pure</b> <see cref="TranslationBulkImporter"/> (no store,
///     no session — the <c>IcsWriter</c> posture, C-M15·8) — the refusal
///     paths (tests 3 and 4) drive it directly and assert that it names
///     the first offending row and that no writes happened (zero
///     <c>AccessAudit</c> rows, C-M15·3).</item>
///   <item>The <b>additive</b> <c>UpsertManyTranslationsAsync</c> seam
///     (tests 1, 2, 5) — one session, one <c>AccessAudit</c> row
///     (<c>translation.import</c>, C-M15·6), the frozen
///     <see cref="TranslationResource"/> row store (C-M15·7).</item>
/// </list>
/// </para>
/// <para>
/// The round-trip pair (C-M15·2): test 1 exports via
/// <see cref="TranslationBulkExporter"/> and re-imports via
/// <see cref="TranslationBulkImporter"/> + <c>UpsertManyTranslationsAsync</c>,
/// verifying the stored matrix is identical after a clean round-trip.
/// </para>
/// </summary>
public class TranslationBulkImporterTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── Bulk_Import_UpsertsPresentRows_Only ─────────────────────────────────
    // Present non-blank rows upsert; a blank cell (no row for that language)
    // leaves the stored row **unchanged** (C-M15·4). The exporter → importer
    // round-trip is lossless for the closed set (C-M15·2).
    [Fact]
    public async Task Bulk_Import_UpsertsPresentRows_Only()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        var key0 = KnownTranslationKeys.AllKeys.First();
        var key1 = KnownTranslationKeys.AllKeys.Skip(1).First();
        var key2 = KnownTranslationKeys.AllKeys.Skip(2).First();

        // Seed two rows for `pl` (the "present" set); key2 has no `pl` row
        // (the blank cell).
        await UpsertTranslation(store, key0, "pl", "PL::" + key0);
        await UpsertTranslation(store, key1, "pl", "PL::" + key1);
        // key2 has NO pl row.

        // Build the export bundle (the C-M15·2 round-trip partner, U01).
        var svc = new LocalizationService(store);
        var matrix = await svc.GetBulkTranslationMatrixAsync();
        var codes = (await svc.ListLanguagesAsync())
            .OrderBy(c => c.SortOrder).Select(c => c.Id).ToList();
        var bundle = TranslationBulkExporter.Build(matrix, codes);

        // Parse the bundle (pure — no store, no audit).
        var parseResult = TranslationBulkImporter.Parse(bundle, codes.ToHashSet(StringComparer.Ordinal));
        Assert.IsType<TranslationBulkImport>(parseResult);
        var import = (TranslationBulkImport)parseResult;

        // The `pl` column should have key0 and key1 (present), but NOT key2
        // (the blank cell was dropped — C-M15·4).
        Assert.True(import.RowsByLanguage.ContainsKey("pl"));
        var plRows = import.RowsByLanguage["pl"];
        Assert.Equal("PL::" + key0, plRows[key0]);
        Assert.Equal("PL::" + key1, plRows[key1]);
        Assert.False(plRows.ContainsKey(key2), "key2 has no stored pl row — should be absent from the import (blank = no-op, C-M15·4)");

        // Apply the import for `pl`.
        var written = await svc.UpsertManyTranslationsAsync("pl", plRows, "admin-seed");
        Assert.Equal(2, written);

        // Verify the store: key0 and key1 are present and correct; key2 is
        // still absent (never a synthetic row, C-M15·1).
        var plTexts = await svc.GetTranslationsForAsync("pl");
        Assert.Equal("PL::" + key0, plTexts[key0]);
        Assert.Equal("PL::" + key1, plTexts[key1]);
        Assert.False(plTexts.ContainsKey(key2), "key2 has no pl row — the blank cell was a no-op (C-M15·4)");

        // The round-trip is lossless: re-export and verify the matrix is
        // identical (C-M15·2).
        var matrixAfter = await svc.GetBulkTranslationMatrixAsync();
        var key0Row = matrixAfter.Single(r => r.Key == key0);
        var key1Row = matrixAfter.Single(r => r.Key == key1);
        var key2Row = matrixAfter.Single(r => r.Key == key2);
        Assert.Equal("PL::" + key0, key0Row.Stored["pl"]);
        Assert.Equal("PL::" + key1, key1Row.Stored["pl"]);
        Assert.Equal((string?)null, key2Row.Stored["pl"]); // still no row
    }

    // ── Bulk_Import_BlankCellIsANoOp ────────────────────────────────────────
    // A blank cell in the bundle leaves the stored row **byte-identical**
    // (C-M15·4 — a file never erases a translation; removal is a standing
    // decision on the per-surface lanes, ADR 0048).
    [Fact]
    public async Task Bulk_Import_BlankCellIsANoOp()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        var key0 = KnownTranslationKeys.AllKeys.First();

        // Seed a stored `pl` row for key0.
        const string originalText = "original-pl-text";
        await UpsertTranslation(store, key0, "pl", originalText);

        // Hand-build a bundle where key0's `pl` cell is **blank** (the
        // translator left it empty — they did not intend to erase it).
        var bundle = BuildBundle(
            codes: new[] { "en", "pl" },
            bodyRows: new[]
            {
                new[] { key0, "source-text", "en-text", string.Empty },  // pl cell blank
            });

        // Parse (pure).
        var parseResult = TranslationBulkImporter.Parse(bundle, new HashSet<string>(StringComparer.Ordinal) { "en", "pl" });
        Assert.IsType<TranslationBulkImport>(parseResult);
        var import = (TranslationBulkImport)parseResult;

        // The `pl` column should have NO entry for key0 (blank cell dropped).
        if (import.RowsByLanguage.ContainsKey("pl"))
            Assert.False(import.RowsByLanguage["pl"].ContainsKey(key0),
                "key0's blank pl cell should be absent from the import (C-M15·4)");

        // Apply the import for `pl` (the caller may or may not call it for
        // an empty set — both are correct).
        if (import.RowsByLanguage.TryGetValue("pl", out var plRows) && plRows.Count > 0)
            await new LocalizationService(store).UpsertManyTranslationsAsync("pl", plRows, "admin-seed");

        // The stored row is **byte-identical** — the blank cell did not
        // erase it (C-M15·4).
        var plTexts = await new LocalizationService(store).GetTranslationsForAsync("pl");
        Assert.Equal(originalText, plTexts[key0]);

        // No `translation.import` audit row was written (nothing was
        // upserted — a blank cell is a no-op, C-M15·4).
        Assert.Empty(await AuditRows(store, targetKind: "translation"));
    }

    // ── Bulk_Import_UnknownKeyRefused_ZeroWrites_NoAudit ────────────────────
    // A body row with a key not in <see cref="KnownTranslationKeys.AllKeys"/>
    // is **refused before any write** (C-M15·3, D5): zero rows touched,
    // **no** <c>AccessAudit</c> row, the first offending row named (the
    // M11 validate-then-apply posture, ADR 0108 D4).
    [Fact]
    public async Task Bulk_Import_UnknownKeyRefused_ZeroWrites_NoAudit()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        var key0 = KnownTranslationKeys.AllKeys.First();
        const string unknownKey = "this.key.does.not.exist";

        // Seed a stored `pl` row (to prove it survives the refusal).
        const string originalText = "pl-original";
        await UpsertTranslation(store, key0, "pl", originalText);

        // Build a bundle with one known key + one **unknown** key.
        var bundle = BuildBundle(
            codes: new[] { "en", "pl" },
            bodyRows: new[]
            {
                new[] { key0, "source", "en-val", "pl-val" },
                new[] { unknownKey, "source", "en-val", "pl-val" },  // unknown key
            });

        var catalogCodes = new HashSet<string>(StringComparer.Ordinal) { "en", "pl" };

        // Parse (pure — no store, no audit).
        var parseResult = TranslationBulkImporter.Parse(bundle, catalogCodes);
        var refused = Assert.IsType<TranslationBulkImportRefused>(parseResult);

        // The first offending row is named (C-M15·3 — the 422 shape).
        Assert.Contains(unknownKey, refused.OffendingRow);
        Assert.Contains("unknown key", refused.Reason);

        // Zero audit rows (C-M15·3 — no audit for the blocked attempt).
        Assert.Empty(await AuditRows(store, targetKind: "translation"));

        // The stored row is **untouched** (zero writes, C-M15·3).
        var plTexts = await new LocalizationService(store).GetTranslationsForAsync("pl");
        Assert.Equal(originalText, plTexts[key0]);
    }

    // ── Bulk_Import_WrongMarkerRefused_ZeroWrites_NoAudit ───────────────────
    // A bundle whose line 1 is not the exact marker
    // (<see cref="TranslationBulkExporter.BundleMarker"/>) is **refused
    // before any write** (C-M15·3, D2 — the M11 <c>manifest.json</c>
    // <c>format</c> pin applied to a file a resident can read): zero rows
    // touched, **no** <c>AccessAudit</c> row.
    [Fact]
    public async Task Bulk_Import_WrongMarkerRefused_ZeroWrites_NoAudit()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        var key0 = KnownTranslationKeys.AllKeys.First();

        // Seed a stored `pl` row (to prove it survives the refusal).
        const string originalText = "pl-original";
        await UpsertTranslation(store, key0, "pl", originalText);

        // Build a bundle with a **wrong** marker (a plausible but incorrect
        // first line).
        var wrongMarker = "# kumunita-translation-bundle/2";
        var bundle = wrongMarker + "\r\n" +
                     "key,source,en,pl\r\n" +
                     key0 + ",source,en-val,pl-val\r\n";

        var catalogCodes = new HashSet<string>(StringComparer.Ordinal) { "en", "pl" };

        // Parse (pure — no store, no audit).
        var parseResult = TranslationBulkImporter.Parse(bundle, catalogCodes);
        var refused = Assert.IsType<TranslationBulkImportRefused>(parseResult);

        // The wrong marker is named (C-M15·3 — the 422 shape).
        Assert.Equal(wrongMarker, refused.OffendingRow);
        Assert.Contains("marker", refused.Reason);

        // Zero audit rows (C-M15·3 — no audit for the blocked attempt).
        Assert.Empty(await AuditRows(store, targetKind: "translation"));

        // The stored row is **untouched** (zero writes, C-M15·3).
        var plTexts = await new LocalizationService(store).GetTranslationsForAsync("pl");
        Assert.Equal(originalText, plTexts[key0]);
    }

    // ── Bulk_Import_AuditRowShape_TranslationImport ─────────────────────────
    // The <c>UpsertManyTranslationsAsync</c> seam writes **exactly one**
    // <c>AccessAudit</c> row — <c>Action = "translation.import"</c>,
    // <c>TargetKind = "translation"</c>, <c>TargetId</c> = the count of
    // upserted rows (as a string), <c>Via = Admin</c>,
    // <c>Outcome = Allow</c> — committed in the same session as the writes
    // (C3; C-M15·6: one audit row per bulk action, never N rows for N
    // upserts).
    [Fact]
    public async Task Bulk_Import_AuditRowShape_TranslationImport()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        var key0 = KnownTranslationKeys.AllKeys.First();
        var key1 = KnownTranslationKeys.AllKeys.Skip(1).First();

        const string actorId = "admin-audit-seed";

        // Build a valid bundle with two `pl` rows.
        var bundle = BuildBundle(
            codes: new[] { "en", "pl" },
            bodyRows: new[]
            {
                new[] { key0, "src0", "en0", "PL::" + key0 },
                new[] { key1, "src1", "en1", "PL::" + key1 },
            });

        var catalogCodes = new HashSet<string>(StringComparer.Ordinal) { "en", "pl" };
        var parseResult = TranslationBulkImporter.Parse(bundle, catalogCodes);
        var import = Assert.IsType<TranslationBulkImport>(parseResult);
        var plRows = import.RowsByLanguage["pl"];
        Assert.Equal(2, plRows.Count);

        // Apply the import for `pl`.
        var svc = new LocalizationService(store);
        var written = await svc.UpsertManyTranslationsAsync("pl", plRows, actorId);
        Assert.Equal(2, written);

        // Exactly ONE audit row for the whole batch (C-M15·6).
        var auditRows = await AuditRows(store, targetKind: "translation");
        Assert.Single(auditRows);

        var row = auditRows[0];
        Assert.Equal("translation.import", row.Action);
        Assert.Equal("translation", row.TargetKind);
        Assert.Equal("2", row.TargetId);           // the count of upserted rows
        Assert.Equal(AccessVia.Admin, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
        Assert.Equal(actorId, row.ActorId);
        Assert.Equal(actorId, row.EffectivePrincipalId);

        // The store carries both rows.
        var plTexts = await svc.GetTranslationsForAsync("pl");
        Assert.Equal("PL::" + key0, plTexts[key0]);
        Assert.Equal("PL::" + key1, plTexts[key1]);
    }

    // ── Shared helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Hand-builds a §bundle CSV string with the given codes and body rows.
    /// Each body row is a string array: <c>[key, source, en, code1, …]</c>.
    /// Blank cells (empty strings) are emitted as empty CSV cells (RFC 4180:
    /// empty fields are unquoted).
    /// </summary>
    private static string BuildBundle(string[] codes, string[][] bodyRows)
    {
        // The header: key,source,en,<codes…> (en is the dedicated third
        // column; a catalog `en` maps onto it — the exporter's rule).
        var headerCodes = codes.Where(c => !string.Equals(c, "en", StringComparison.Ordinal)).ToList();
        var header = "key,source,en";
        foreach (var code in headerCodes)
            header += "," + code;

        var sb = new System.Text.StringBuilder();
        sb.Append(TranslationBulkExporter.BundleMarker).Append("\r\n");
        sb.Append(header).Append("\r\n");

        foreach (var row in bodyRows)
        {
            sb.Append(QuoteCsv(row[0]))
              .Append(',')
              .Append(QuoteCsv(row[1]))
              .Append(',')
              .Append(QuoteCsv(row[2]));
            for (var i = 3; i < row.Length; i++)
                sb.Append(',').Append(QuoteCsv(row[i]));
            sb.Append("\r\n");
        }

        return sb.ToString();
    }

    private static string QuoteCsv(string field)
    {
        if (field.Length == 0) return string.Empty;
        if (field.IndexOf(',') < 0 && field.IndexOf('"') < 0
            && field.IndexOf('\r') < 0 && field.IndexOf('\n') < 0)
            return field;
        return '"' + field.Replace("\"", "\"\"") + '"';
    }

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
