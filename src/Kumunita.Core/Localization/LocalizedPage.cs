namespace Kumunita.Core.Localization;

/// <summary>
/// One static-page translation (ADR 0005 B; M·2/M·4). The business key is
/// (Slug, LanguageCode) — one page body per slug per language (terms/about/help),
/// rendered by the single page engine. Markdown body; per-page fallback (M·2).
/// Rows are **retained** when a language is removed (M·7) so re-adding restores
/// them.
/// </summary>
public sealed class LocalizedPage
{
    public string Id { get; set; } = string.Empty;            // surrogate (Marten default) — the pair idiom
    public string Slug { get; set; } = string.Empty;          // "terms" | "about" | "help"
    public string LanguageCode { get; set; } = string.Empty;  // BCP-47 — a LanguageCatalog.Id
    public string Title { get; set; } = string.Empty;         // the page's rendered title
    public string Body { get; set; } = string.Empty;          // Markdown (the single page engine)
    public DateTimeOffset Updated { get; set; }               // last admin edit
}
