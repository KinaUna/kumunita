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

    /// <summary>
    /// The instance's <b>default</b> IANA time zone id (ADR 0019) — the
    /// fallback a resident's timestamps render in when they have set no
    /// <c>Profile.TimeZone</c> override (the "preference if present → instance
    /// default → <c>UTC</c> floor" resolution order, the same shape as
    /// <see cref="DefaultLanguageCode"/>). An *additive* field on the singleton
    /// (ADR 0004 §B.1); the first-boot seeder materializes it to <c>UTC</c>.
    /// </summary>
    public string DefaultTimezone { get; set; } = "UTC";

    /// <summary>
    /// The instance's <b>default</b> date-time <i>format</i> — a .NET custom
    /// datetime format string (ADR 0020) — the fallback a resident's
    /// timestamps are <b>formatted</b> in when they have set no
    /// <c>Profile.DateFormat</c> override (the same "preference if present →
    /// instance default → floor" resolution order as
    /// <see cref="DefaultTimezone"/>). An *additive* field on the singleton
    /// (ADR 0004 §B.1); the first-boot seeder materializes it to the floor
    /// (the "Long" preset, <see cref="DateFormat.FloorFormat"/>).
    /// </summary>
    public string DefaultDateFormat { get; set; } = DateFormat.FloorFormat;

    /// <summary>
    /// Whether self-service sign-up is currently open on this instance
    /// (ADR 0050). The <see cref="Kumunita.Core.Identity"/> signup lane is the
    /// one that reads this: when <c>true</c> the public <c>Sign up</c> surface
    /// is live and a new resident may create an account; when <c>false</c> the
    /// surface is hidden and the signup write is denied (the admin-managed
    /// "invitation-only" state the README's deferred "Invitation-only sign-up"
    /// item describes as the long-term default). An *additive* field on the
    /// singleton (ADR 0004 §B.1), the same shape as <see
    /// cref="DefaultTimezone"/> / <see cref="DefaultDateFormat"/>. Defaults to
    /// <c>true</c> so a fresh instance ships with sign-up open (the development
    /// circle keeps working; an admin tightens it to invitation-only before the
    /// community widens — SECURITY.md §6, adversary A2).
    /// </summary>
    public bool IsSignupOpen { get; set; } = true;
}
