using System.Collections.Generic;
using System.Linq;

namespace Kumunita.Core.Localization;

/// <summary>
/// M15 (ADR 0116, D5) — the translation-bundle **parser**: a **pure**
/// function that maps a §bundle CSV string onto either the validated
/// per-language upsert rows (<see cref="TranslationBulkImport"/>) or a
/// refusal naming the **first offending row** (<see cref
/// "TranslationBulkImportRefused"/>, C-M15·3).
/// <para>
/// <b>Pure (C-M15·8, the <see cref="Events.IcsWriter"/> "BCL-only, pure,
/// closed subset" discipline):</b> no store, no <c>IDocumentSession</c>, no
/// <c>AccessAudit</c> row, no HTTP, no CSV package — the parse is plain BCL
/// string code. The caller owns the write (the additive
/// <c>UpsertManyTranslationsAsync</c> seam); the parser re-derives nothing
/// and leaks nothing (C-M15·1 — the bundle is a **view of the store**, the
/// parse is a **view of the file**; the store is the single source of truth
/// before, during, and after a round-trip).
/// </para>
/// <para>
/// <b>Validation is complete before any row is returned (C-M15·3)</b> — the
/// §bundle spec's locked validation order (D5):
/// <list type="number">
///   <item>The exact marker (<see cref="TranslationBulkExporter.BundleMarker"/>
///     ) on line 1.</item>
///   <item>A header row on line 2: <c>key,source,en,&lt;code1&gt;,…</c> —
///     the first three cells must be exactly <c>key</c>, <c>source</c>,
///     <c>en</c>.</item>
///   <item>Every language column (positions 3+) ∈ <paramref
///     name="catalogCodes"/> (an unknown column ⇒ refused, C-M15·3).</item>
///   <item>A non-empty body (≥ 1 body row).</item>
///   <item>Every body row's key (cell 0) ∈ <see
///     cref="KnownTranslationKeys.AllKeys"/> (an unknown key ⇒ refused,
///     C-M15·3).</item>
/// </list>
/// The <c>source</c> column (cell 1) is **ignored** — it is never written
/// back (D2 §bundle: read-only reference, the code's floor is the
/// authority). A **blank cell** (empty string) is **dropped** from the
/// upsert set (C-M15·4 — never a deletion).
/// </para>
/// <para>
/// <b>RFC 4180 cell parsing:</b> a field containing a comma, a double
/// quote, or a CR/LF is wrapped in double quotes; embedded double quotes
/// are doubled; plain fields (including empty cells) are unquoted. The
/// parser inverts this: a cell starting with <c>"</c> is read as a quoted
/// cell (terminated by a single unescaped <c>"</c>), otherwise it is read
/// up to the next comma or end of line.
/// </para>
/// </summary>
public static class TranslationBulkImporter
{
    /// <summary>
    /// Parses a §bundle CSV string into either the validated per-language
    /// upsert rows (<see cref="TranslationBulkImport"/>) or a refusal
    /// naming the **first offending row** (<see cref
    /// "TranslationBulkImportRefused"/>, C-M15·3).
    /// <para>
    /// **No session, no audit, no store** (the <see cref
    /// "Events.IcsWriter"/> posture, C-M15·8). The caller inspects the
    /// result type: <see cref="TranslationBulkImport"/> → drive the
    /// additive <c>UpsertManyTranslationsAsync</c> seam; <see cref
    /// "TranslationBulkImportRefused"/> → surface the 422 shape, write
    /// nothing, emit no audit row.
    /// </para>
    /// </summary>
    /// <param name="bundleText">
    /// The §bundle CSV text (marker + header + body, CRLF, RFC 4180
    /// quoted). Must be the exact shape emitted by <see cref
    /// "TranslationBulkExporter.Build"/> (C-M15·2 round-trip pair).
    /// </param>
    /// <param name="catalogCodes">
    /// The catalog's language codes (enabled **and** disabled — the
    /// catalog's set, D1/D2). A header language column not in this set is
    /// refused (unknown language, C-M15·3).
    /// </param>
    /// <returns>
    /// A <see cref="TranslationBulkImport"/> (the validated rows, blank
    /// cells dropped — C-M15·4) **or** a <see cref
    /// "TranslationBulkImportRefused"/> (the first offending row + the
    /// reason).
    /// </returns>
    public static TranslationBulkImportResult Parse(
        string bundleText,
        IReadOnlySet<string> catalogCodes)
    {
        if (string.IsNullOrEmpty(bundleText))
            return Refuse(string.Empty, "empty bundle — no marker row");

        // Split on CRLF. Per §bundle, every line (including the last) is
        // CRLF-terminated, so the final split element is an empty string —
        // strip it.
        var lines = bundleText.Split("\r\n");
        while (lines.Length > 0 && lines[^1].Length == 0)
            lines = lines[..^1];

        if (lines.Length == 0)
            return Refuse(string.Empty, "empty bundle — no marker row");

        if (lines.Length < 2)
            return Refuse(lines[0], "bundle has no header row");

        // 1. Marker (line 1) — the format authority (D2 §bundle).
        if (!string.Equals(lines[0], TranslationBulkExporter.BundleMarker,
                System.StringComparison.Ordinal))
            return Refuse(lines[0], "marker mismatch — expected " +
                TranslationBulkExporter.BundleMarker);

        // 2. Header (line 2).
        string[] headerCells;
        try { headerCells = SplitCsvRow(lines[1]); }
        catch (System.FormatException ex)
        { return Refuse(lines[1], "malformed header: " + ex.Message); }

        if (headerCells.Length < 3)
            return Refuse(lines[1],
                "header must be key,source,en,<codes…>");

        if (!string.Equals(headerCells[0], "key", System.StringComparison.Ordinal) ||
            !string.Equals(headerCells[1], "source", System.StringComparison.Ordinal) ||
            !string.Equals(headerCells[2], "en", System.StringComparison.Ordinal))
            return Refuse(lines[1], "header must start with key,source,en");

        var headerCodes = headerCells[3..].ToList();

        // Every language column must be in the catalog (C-M15·3 — unknown
        // language ⇒ refused). A missing catalog column is a no-op (no rows
        // for that language), not a refusal.
        foreach (var code in headerCodes)
        {
            if (!catalogCodes.Contains(code))
                return Refuse(lines[1], "unknown language column " + code);
        }

        // 3. Body: at least one row (D5 — "a non-empty body").
        if (lines.Length < 3)
            return Refuse(lines[1], "bundle has no body rows");

        // 4. Parse body rows.
        var rowsByLanguage = new Dictionary<string, Dictionary<string, string>>(
            StringComparer.Ordinal);
        var expectedCount = headerCells.Length;

        for (var i = 2; i < lines.Length; i++)
        {
            var line = lines[i];
            string[] cells;
            try { cells = SplitCsvRow(line); }
            catch (System.FormatException ex)
            { return Refuse(line, "malformed body row: " + ex.Message); }

            if (cells.Length != expectedCount)
                return Refuse(line, "cell count mismatch (expected " +
                    expectedCount + ", got " + cells.Length + ")");

            var key = cells[0];
            if (key.Length == 0)
                return Refuse(line, "empty key cell");

            if (!KnownTranslationKeys.AllKeys.Contains(key))
                return Refuse(line, "unknown key " + key);

            // cells[1] = source — **ignored** (D2 §bundle: read-only
            // reference, never written back).

            // cells[2] = en (the stored en row).
            UpsertCell(rowsByLanguage, "en", key, cells[2]);

            // cells[3..] = catalog codes in header order.
            for (var j = 3; j < cells.Length; j++)
                UpsertCell(rowsByLanguage, headerCodes[j - 3], key, cells[j]);
        }

        return new TranslationBulkImport(rowsByLanguage);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static void UpsertCell(
        Dictionary<string, Dictionary<string, string>> rows,
        string code, string key, string text)
    {
        if (string.IsNullOrEmpty(text)) return; // blank = no-op (C-M15·4)
        if (!rows.TryGetValue(code, out var dict))
        {
            dict = new Dictionary<string, string>(StringComparer.Ordinal);
            rows[code] = dict;
        }
        dict[key] = text;
    }

    private static TranslationBulkImportRefused Refuse(
        string line, string reason)
        => new() { OffendingRow = line, Reason = reason };

    /// <summary>
    /// RFC 4180 cell splitter (the inverse of the exporter's <c>Quote</c>):
    /// a cell starting with <c>"</c> is a quoted cell (terminated by a single
    /// unescaped <c>"</c>; embedded <c>""</c> → a literal <c>"</c>); an
    /// unquoted cell is read up to the next comma or end of line. Empty
    /// cells yield the empty string.
    /// </summary>
    private static string[] SplitCsvRow(string line)
    {
        var cells = new List<string>();
        var i = 0;

        while (true)
        {
            if (i < line.Length && line[i] == '"')
            {
                // Quoted cell: read until an unescaped closing quote.
                i++; // skip opening quote
                var sb = new System.Text.StringBuilder();
                while (i < line.Length)
                {
                    if (line[i] == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"'); // embedded doubled quote
                            i += 2;
                        }
                        else
                        {
                            i++; // skip closing quote
                            break;
                        }
                    }
                    else
                    {
                        sb.Append(line[i]);
                        i++;
                    }
                }
                cells.Add(sb.ToString());
            }
            else
            {
                // Unquoted cell: read up to the next comma or end of line.
                var start = i;
                while (i < line.Length && line[i] != ',')
                    i++;
                cells.Add(line[start..i]);
            }

            // After a cell: either end of line, or a comma separator.
            if (i >= line.Length)
                break;
            if (line[i] != ',')
                throw new System.FormatException(
                    "expected comma or end-of-line at position " + i);
            i++; // skip the comma
        }

        return cells.ToArray();
    }
}

// ── Result types ────────────────────────────────────────────────────────

/// <summary>
/// M15 (ADR 0116, D5) — the **result** of a translation-bundle parse:
/// either the validated per-language upsert rows (<see cref
/// "TranslationBulkImport"/>) or a refusal naming the **first offending
/// row** (<see cref "TranslationBulkImportRefused"/>, C-M15·3).
/// </summary>
public abstract class TranslationBulkImportResult
{
    protected TranslationBulkImportResult() { }
}

/// <summary>
/// The **Ok** result — the validated per-language upsert rows (blank
/// cells dropped — C-M15·4; the <c>source</c> column ignored — D2 §bundle).
/// A catalog code with **no** non-blank cell for any key is **absent**
/// from <see cref="RowsByLanguage"/> (a blank cell is a no-op, never an
/// empty row — C-M15·4).
/// </summary>
public sealed class TranslationBulkImport : TranslationBulkImportResult
{
    internal TranslationBulkImport(
        Dictionary<string, Dictionary<string, string>> rowsByLanguage)
    {
        RowsByLanguage = rowsByLanguage.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyDictionary<string, string>)
                kvp.Value.ToDictionary(
                    kv => kv.Key, kv => kv.Value, StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// The catalog's codes → the present non-blank upsert rows
    /// (<c>key → text</c>). A code with no non-blank cells is absent.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> RowsByLanguage { get; }
}

/// <summary>
/// The **Refused** result (C-M15·3, the 422 shape) — the **first offending
/// row** (its text as it appeared in the bundle) + the reason. The Web
/// route surfaces this to the caller; the service writes **no** audit row
/// (the M11 D4 "no audit for the blocked attempt" pin).
/// </summary>
public sealed class TranslationBulkImportRefused : TranslationBulkImportResult
{
    /// <summary>The text of the first offending row (its line content).</summary>
    public string OffendingRow { get; init; } = string.Empty;

    /// <summary>The reason (a human-readable diagnostic).</summary>
    public string Reason { get; init; } = string.Empty;
}
