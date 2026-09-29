using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Marten;
using Marten.Services;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M15 · U03 — the 2 pinned <see cref="LocalizationService"/>
/// <c>SaveAllTranslationsAsync</c> facts (design doc
/// <c>m15-translation-bulk-design.md</c> §pinned tests, U03 group, names
/// verbatim; unit plan <c>m15-u03.md</c>).
/// <para>
/// The batch editor's write seam (D4/D6): the same one-session +
/// one-audit-row idiom as U02's <c>UpsertManyTranslationsAsync</c>, with
/// the audit action <c>translation.save_all</c> and
/// <c>TargetId</c> = the language code (not the count). Blank values are
/// dropped, never erased (C-M15·4); the frozen
/// <see cref="LocalizationService.UpsertTranslationAsync"/> stays the
/// per-row lane, byte-identical (C-M15·7).
/// </para>
/// </summary>
public class TranslationBulkSaveAllTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── Bulk_SaveAll_UpsertsPresentRows_Only ───────────────────────────────
    // The present non-blank rows upsert (insert-or-update, same row store
    // as the frozen one-row lane — C-M15·7); a **blank** input is
    // **dropped, not erased** (C-M15·4 — the same no-op rule as the
    // import; the stored row for a blank key survives byte-identical) and
    // is not counted in the return value.
    [Fact]
    public async Task Bulk_SaveAll_UpsertsPresentRows_Only()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        var key0 = KnownTranslationKeys.AllKeys.First();
        var key1 = KnownTranslationKeys.AllKeys.Skip(1).First();

        var svc = new LocalizationService(store);

        // Seed one stored `pl` row (key0) that the batch save must
        // *update*, and leave key1 unseeded (the batch save must *insert*
        // it).
        await UpsertTranslation(store, key0, "pl", "pl-original");

        // The batch editor's form: key0 present (updated), key1 present
        // (inserted), key1's *sibling* is left blank — the blank entry is
        // a no-op (C-M15·4).
        var key2 = KnownTranslationKeys.AllKeys.Skip(2).First();
        await UpsertTranslation(store, key2, "pl", "pl-must-survive");

        var written = await svc.SaveAllTranslationsAsync("pl", new Dictionary<string, string>
        {
            [key0] = "PL::" + key0,
            [key1] = "PL::" + key1,
            [key2] = string.Empty,   // blank — dropped, not erased (C-M15·4)
        }, "admin-seed");

        // The blank row is not counted — only the two present non-blank
        // rows upserted.
        Assert.Equal(2, written);

        // key0 updated, key1 inserted, key2 **byte-identical** (the blank
        // input did not erase it — C-M15·4).
        var plTexts = await svc.GetTranslationsForAsync("pl");
        Assert.Equal("PL::" + key0, plTexts[key0]);
        Assert.Equal("PL::" + key1, plTexts[key1]);
        Assert.Equal("pl-must-survive", plTexts[key2]);
    }

    // ── Bulk_SaveAll_AuditRowShape_TranslationSaveAll ──────────────────────
    // The <c>SaveAllTranslationsAsync</c> seam writes **exactly one**
    // <c>AccessAudit</c> row — <c>Action = "translation.save_all"</c>,
    // <c>TargetKind = "translation"</c>, <c>TargetId</c> = the **language
    // code**, <c>Via = Admin</c>, <c>Outcome = Allow</c> — committed in
    // the same session as the writes (C3; C-M15·6: one audit row per bulk
    // action, never N rows for N upserts).
    [Fact]
    public async Task Bulk_SaveAll_AuditRowShape_TranslationSaveAll()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        var key0 = KnownTranslationKeys.AllKeys.First();
        var key1 = KnownTranslationKeys.AllKeys.Skip(1).First();

        const string actorId = "admin-audit-seed";
        var svc = new LocalizationService(store);

        var written = await svc.SaveAllTranslationsAsync("pl", new Dictionary<string, string>
        {
            [key0] = "PL::" + key0,
            [key1] = "PL::" + key1,
        }, actorId);
        Assert.Equal(2, written);

        // Exactly ONE audit row for the whole batch (C-M15·6).
        var auditRows = await AuditRows(store, targetKind: "translation");
        Assert.Single(auditRows);

        var row = auditRows[0];
        Assert.Equal("translation.save_all", row.Action);
        Assert.Equal("translation", row.TargetKind);
        Assert.Equal("pl", row.TargetId);           // the language code (D4)
        Assert.Equal(AccessVia.Admin, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
        Assert.Equal(actorId, row.ActorId);
        Assert.Equal(actorId, row.EffectivePrincipalId);

        // The store carries both rows.
        var plTexts = await svc.GetTranslationsForAsync("pl");
        Assert.Equal("PL::" + key0, plTexts[key0]);
        Assert.Equal("PL::" + key1, plTexts[key1]);
    }

    // ── Shared helpers (mirrors TranslationBulkImporterTests) ─────────────

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
