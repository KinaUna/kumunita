namespace Kumunita.Core.Localization;

/// <summary>
/// One UI-string translation (ADR 0005 B; M·2/M·4). The business key is
/// (Key, LanguageCode) — the unique index in M1DocTypes enforces one text per key
/// per language (the GroupMembership / ComponentMembership pair idiom). A resident
/// never sees a blank label (M·1): a missing (Key, preferred) falls back to
/// (Key, default) → (Key, "en") → the Key itself (M·9's floor is "en", always
/// seeded by the first-run seeder).
/// </summary>
public sealed class TranslationResource
{
    public string Id { get; set; } = string.Empty;            // surrogate (Marten default) — the pair idiom
    public string Key { get; set; } = string.Empty;           // the code key (e.g. "nav.home", "feed.reply")
    public string LanguageCode { get; set; } = string.Empty;  // BCP-47 — a LanguageCatalog.Id
    public string Text { get; set; } = string.Empty;          // the rendered string
}
