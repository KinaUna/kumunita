using System.Text;

namespace Kumunita.Core.Localization;

/// <summary>
/// M15 (ADR 0116, D2) — the translation-bundle **emitter**: a **pure**
/// function over the closed-set matrix (<see cref="TranslationBulkRow"/> rows)
/// that maps it onto the §bundle CSV shape — the exact table locked in
/// <c>docs/design/m15-translation-bulk-design.md</c> §bundle (the LOCKED
/// primary tier, the U01 unit of <c>m15-u01.md</c>). The <see cref
/// "TranslationBulkImporter"/> (U02) parses **exactly** this shape — the two
/// are a round-trip pair (C-M15·2).
/// <para>
/// **Pure (C-M15·8, the <see cref="Events.IcsWriter"/> "BCL-only, pure, closed
/// subset" discipline):** no store, no <c>IDocumentSession</c>, no
/// <c>AccessAudit</c> row, no HTTP, no CSV package — the output is built with
/// plain BCL string code. The caller owns the matrix (the frozen
/// <c>GetBulkTranslationMatrixAsync</c> read seam); the emitter re-derives
/// nothing and leaks nothing (C-M15·1 — the bundle is a **view** of the
/// <see cref="TranslationResource"/> store, never a store).
/// </para>
/// <para>
/// **The §bundle shape (verbatim):** line 1 the marker row
/// <see cref="BundleMarker"/>; line 2 the column header
/// <c>key,source,en,&lt;code1&gt;,…</c> — <c>key</c>, <c>source</c>
/// (<see cref="KnownTranslationKeys.EnValues"/> — the read-only reference),
/// <c>en</c> (the **stored** <c>en</c> row), then each catalog language
/// (enabled **and** disabled) in <c>SortOrder</c> — the column set is the
/// catalog's set; body rows = the matrix in order (declaration order for the
/// service's read seam). A <c>null</c> stored cell renders as an **empty
/// cell** (never a synthetic row, D1). RFC 4180 quoting: a field containing
/// a comma, a double quote, or a CR/LF is wrapped in double quotes; embedded
/// double quotes are doubled; plain fields are unquoted. CRLF on **every**
/// line, including the last. UTF-8, no BOM (the caller's encoding choice —
/// this method returns text).
/// </para>
/// <para>
/// <b>The <c>en</c> column</b> is the dedicated third column in **every**
/// bundle: a catalog code equal to <c>"en"</c> in <paramref name="columnOrder"/>
/// maps onto that column and is not emitted twice (a disabled or reordered
/// <c>en</c> catalog row still fills the <c>en</c> cell — F5, D1/D2).
/// </para>
/// </summary>
public static class TranslationBulkExporter
{
    /// <summary>
    /// The bundle's **format authority** (D2, §bundle): line 1 of every
    /// bundle, verbatim. The import (U02) **requires** this exact marker on
    /// line 1 — any other first line is refused (C-M15·3, the M11
    /// <c>manifest.json</c> <c>format</c> pin applied to a file a resident
    /// can read).
    /// </summary>
    public const string BundleMarker = "# kumunita-translation-bundle/1";

    /// <summary>
    /// The one public method (D9 — the pure-emitter surface, verbatim). Maps
    /// the closed-set matrix + the catalog's column order onto the **entire**
    /// bundle: the marker row, the <c>key,source,en,&lt;codes…&gt;</c> header,
    /// the body rows in <paramref name="matrix"/> order — RFC 4180 quoted,
    /// CRLF-terminated (every line, including the last).
    /// </summary>
    /// <param name="matrix">
    /// One <see cref="TranslationBulkRow"/> per closed registry key (the
    /// service read seam emits them in <see cref="KnownTranslationKeys.AllKeys"/>
    /// declaration order). A <c>null</c> <see cref="TranslationBulkRow.Stored"/>
    /// cell renders as an **empty cell** (D1 — never a synthetic row).
    /// </param>
    /// <param name="columnOrder">
    /// The catalog's codes in <c>SortOrder</c> — enabled **and** disabled
    /// (D1/D2: the bundle carries the whole set; a disabled language's column
    /// still appears, F5). A code equal to <c>"en"</c> maps onto the
    /// dedicated <c>en</c> column (never a duplicate column).
    /// </param>
    /// <returns>
    /// The entire bundle text — marker + header + body, RFC 4180 quoted,
    /// CRLF line endings on every line including the last, UTF-8-safe text
    /// (the caller encodes it BOM-free).
    /// </returns>
    public static string Build(IReadOnlyList<TranslationBulkRow> matrix,
                               IReadOnlyList<string> columnOrder)
    {
        // The dedicated `en` column is always third; a catalog `en` maps onto
        // it, so the per-code columns skip the `en` entry (no duplicate).
        var codes = columnOrder
            .Where(c => !string.Equals(c, "en", System.StringComparison.Ordinal))
            .ToList();

        var sb = new StringBuilder();

        // Line 1 — the marker (the format authority, §bundle).
        sb.Append(BundleMarker).Append("\r\n");

        // Line 2 — the column header, verbatim order: key,source,en,<codes…>.
        sb.Append("key").Append(",source").Append(",en");
        foreach (var code in codes)
            sb.Append(',').Append(code);
        sb.Append("\r\n");

        // Body rows — in matrix order (declaration order), one row per key.
        foreach (var row in matrix)
        {
            sb.Append(Quote(row.Key))
              .Append(',')
              .Append(Quote(row.SourceText))
              .Append(',')
              .Append(Quote(Cell(row, "en")));
            foreach (var code in codes)
                sb.Append(',').Append(Quote(Cell(row, code)));
            sb.Append("\r\n");
        }

        return sb.ToString();
    }

    /// <summary>A missing stored row renders as the empty cell (D1 — never
    /// null in the output, never a synthetic row).</summary>
    private static string Cell(TranslationBulkRow row, string code) =>
        row.Stored.TryGetValue(code, out var text) ? text ?? string.Empty : string.Empty;

    /// <summary>
    /// RFC 4180 (D2 §bundle): a field containing a comma, a double quote, or
    /// a CR/LF is wrapped in double quotes; embedded double quotes are
    /// doubled; plain fields (including the empty cell) are unquoted.
    /// </summary>
    private static string Quote(string field)
    {
        if (field.Length == 0)
            return string.Empty;

        if (field.IndexOf(',') < 0 && field.IndexOf('"') < 0
            && field.IndexOf('\r') < 0 && field.IndexOf('\n') < 0)
            return field;

        return '"' + field.Replace("\"", "\"\"") + '"';
    }
}
