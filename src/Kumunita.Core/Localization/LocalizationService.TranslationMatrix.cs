using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kumunita.Core.Bootstrap;
using Kumunita.Core.Pages;
using Marten;
using Marten.Services;

namespace Kumunita.Core.Localization;

/// <summary>
/// The translation-matrix surface — the bulk-translation matrix read
/// and the bundled-baseline read (ADR 0005 multilingual, ADR 0015
/// UI-view localization). A partial class of
/// <see cref="LocalizationService"/> extracted so the main file stays
/// under the 600 <c>cs_file</c> ceiling (C3-04).
/// </summary>
public partial class LocalizationService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<TranslationBulkRow>> GetBulkTranslationMatrixAsync(
        CancellationToken ct = default)
    {
        // M15 bulk-read seam (ADR 0116, D1; U01): the closed-set matrix the
        // TranslationBulkExporter projects onto the §bundle CSV. A read —
        // **no audit row** (C-M15·7), composed over the frozen seams only:
        // the catalog's codes (SortOrder, enabled + disabled) and one
        // GetTranslationsForAsync round-trip per code. A missing row → a
        // missing cell (a null value in Stored), never null in Key/SourceText
        // and never a synthetic row (the M·12 floor, C-M15·1).
        var catalog = await ListLanguagesAsync().ConfigureAwait(false);
        var codes = catalog
            .OrderBy(c => c.SortOrder)
            .Select(c => c.Id)
            .Distinct()
            .ToList();

        // One round-trip per catalog code, over the frozen batch read.
        var textByCode = new Dictionary<string, IReadOnlyDictionary<string, string>>(codes.Count, StringComparer.Ordinal);
        foreach (var code in codes)
            textByCode[code] = await GetTranslationsForAsync(code).ConfigureAwait(false);

        var rows = new List<TranslationBulkRow>(KnownTranslationKeys.AllKeys.Count);
        foreach (var key in KnownTranslationKeys.AllKeys)
        {
            var stored = new Dictionary<string, string?>(codes.Count, StringComparer.Ordinal);
            foreach (var code in codes)
            {
                textByCode[code].TryGetValue(key, out var text);
                stored[code] = text; // a missing row → a null cell (D1; the M·12 floor)
            }

            rows.Add(new TranslationBulkRow
            {
                Key = key,
                SourceText = KnownTranslationKeys.EnValues[key],
                Stored = stored,
            });
        }

        return rows;
    }

    /// <inheritdoc />
    public async Task<LanguageCompleteness> GetCompletenessAsync(string languageCode)
    {
        // M·9: the "known" universe of keys / slugs is the instance's seeded `en`
        // set — a key is *missing* for a language iff it has an `en` row but no
        // row in this language. One query session, three live-row reads.
        await using var session = _store.QuerySession();
        var ct = System.Threading.CancellationToken.None;

        // UI strings: the `en` universe (M·9) vs. the language's present set.
        var allStringRows = await session
            .Query<TranslationResource>()
            .Where(t => t.LanguageCode == "en" || t.LanguageCode == languageCode)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var enKeys = allStringRows
            .Where(t => t.LanguageCode == "en")
            .Select(t => t.Key)
            .Distinct()
            .ToHashSet();

        var presentStringKeys = allStringRows
            .Where(t => t.LanguageCode == languageCode)
            .Select(t => t.Key)
            .ToHashSet();

        var missingStringKeys = enKeys.Except(presentStringKeys).OrderBy(k => k).ToList();

        return new LanguageCompleteness(
            languageCode,
            presentStringKeys.OrderBy(k => k).ToList(),
            missingStringKeys);
    }

    // ── ADR 0044: the bundled baseline read + the create-if-missing seed ──

    /// <inheritdoc />
    public Task<BundledLanguageBaseline?> GetBundledBaselineAsync(string code)
    {
        // A pure read of the code's per-language sources — no database access.
        // Only the bundled baseline languages (de / fr / da) have one; en is the
        // source language (a row, not a baseline) and any other code is
        // admin-authored (no code baseline) — both return null. `da` ships
        // disabled in the catalog (the pre-seeded lane) but still carries a full
        // baseline, so adding it back after a remove re-seeds from this source.
        IReadOnlyDictionary<string, string> uiStrings;
        (string Slug, string Title, string Body)[] pages;
        switch (code)
        {
            case "de":
                uiStrings = KnownTranslationKeys.DeValues;
                pages = SeededContentCatalog.DeDefaultPages();
                break;
            case "fr":
                uiStrings = KnownTranslationKeys.FrValues;
                pages = SeededContentCatalog.FrDefaultPages();
                break;
            case "da":
                uiStrings = KnownTranslationKeys.DaValues;
                pages = SeededContentCatalog.DaDefaultPages();
                break;
            default:
                return Task.FromResult<BundledLanguageBaseline?>(null);
        }

        var pageBaselines = pages
            .Select(p => (p.Slug, p.Title, p.Body))
            .ToList();
        return Task.FromResult<BundledLanguageBaseline?>(
            new BundledLanguageBaseline(code, uiStrings, pageBaselines));
    }


}
