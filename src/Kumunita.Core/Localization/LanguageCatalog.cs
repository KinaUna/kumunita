namespace Kumunita.Core.Localization;

/// <summary>
/// One row in the per-instance language catalog (ADR 0005 B): the languages the
/// instance supports. Admins edit it in-app (/admin/languages, M6's surface) —
/// the first-boot seeder materializes the source-language (<c>en</c>) row.
/// </summary>
public sealed class LanguageCatalog
{
    /// <summary>BCP-47 code — the row's identity (ADR 0005 B: <c>{ code }</c>).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The language's own name as displayed to the user ("English", "Polski", ...).</summary>
    public string NativeName { get; set; } = string.Empty;

    /// <summary>Whether residents can pick and admins can use this language right now.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Display order in the language selector (lower = first).</summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Per-instance locale settings singleton (ADR 0005 B): the instance-level default
/// language. One row per instance; <see cref="Id"/> fixed to the sentinel
/// <c>singleton</c> so re-resolving it is a plain identity-keyed load.
/// </summary>
public sealed class LocaleSettings
{
    public const string SingletonId = "singleton";

    public string Id { get; set; } = SingletonId;

    /// <summary>The BCP-47 code of the instance's default language (a
    /// <see cref="LanguageCatalog.Id"/> that exists and is enabled).</summary>
    public string DefaultLanguageCode { get; set; } = "en";
}
