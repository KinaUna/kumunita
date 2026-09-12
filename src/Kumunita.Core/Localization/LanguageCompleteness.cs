using System.Collections.Generic;

namespace Kumunita.Core.Localization;

/// <summary>
/// The per-language completeness view (ADR 0005 D; M·12 FACES). Which UI keys and
/// static pages are present vs. missing for a language — the admin sees the gap a
/// resident would hit via the per-string fallback (M·2) **before** they hit it.
/// The "known" universe of keys/slugs is the instance's seeded `en` set (M·9): a
/// key is *missing* for a language iff it has an `en` row but no row in this
/// language.
/// </summary>
public sealed record LanguageCompleteness(
    string LanguageCode,
    IReadOnlyList<string> PresentKeys,
    IReadOnlyList<string> MissingKeys,
    IReadOnlyList<string> PresentPageSlugs,
    IReadOnlyList<string> MissingPageSlugs);
