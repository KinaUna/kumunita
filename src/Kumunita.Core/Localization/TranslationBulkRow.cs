namespace Kumunita.Core.Localization;

/// <summary>
/// M15 (ADR 0116, D1) — one row of the translation-bulk **closed-set matrix**:
/// the projection a <see cref="TranslationBulkExporter"/> /
/// <see cref="TranslationBulkImporter"/> round-trip pair (C-M15·2) maps onto
/// the bundle CSV.
/// <para>
/// <b>Not a Marten document</b> (C-M15·8): it is a read projection — it
/// creates no <c>mt</c> table, owns no <c>Id</c>, and is never stored. The
/// <see cref="TranslationResource"/> rows (+ the <c>(Key, LanguageCode)</c>
/// unique index) remain the single source of truth before, during, and after
/// a round-trip (C-M15·1) — the bundle is a **view of the store, never a
/// store**.
/// </para>
/// <para>
/// <b>The M·12 floor:</b> <see cref="Key"/> and <see cref="SourceText"/> are
/// never null (the M·12 "never null, never a synthetic row" floor — the
/// *cells* carry the missing state). In <see cref="Stored"/>, a
/// catalog language with **no** stored <see cref="TranslationResource"/> row
/// is present with a <c>null</c> value — an **empty cell** on export
/// (D1/D2 §bundle), never an absent entry and never a synthetic row.
/// </para>
/// </summary>
public sealed class TranslationBulkRow
{
    /// <summary>The closed registry key — one of <see cref="KnownTranslationKeys.AllKeys"/>.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// The registry's <c>en</c> floor text — <see cref="KnownTranslationKeys.EnValues"/>[Key].
    /// A **read-only reference** (the bundle's <c>source</c> column, D2 §bundle):
    /// the import ignores it — a file's <c>source</c> text can never overwrite
    /// the code's floor.
    /// </summary>
    public string SourceText { get; init; } = string.Empty;

    /// <summary>
    /// The catalog's codes → the **stored** text for (Key, code). A code with
    /// no stored row is present with a <c>null</c> value (the missing cell —
    /// D1; the M·12 floor). The key set is the **catalog's** set (enabled
    /// **and** disabled, D1/D2), so a disabled language's cell still round-trips.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Stored { get; init; }
        = new Dictionary<string, string?>(System.StringComparer.Ordinal);
}
