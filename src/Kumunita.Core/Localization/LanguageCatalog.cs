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

    /// <summary>
    /// Whether the <see cref="Kumunita.Core.Identity"/> account lane notifies the
    /// <b>GlobalAdmins</b> (inbox + best-effort email, the M6 lean-default posture)
    /// when a new resident <b>signs up</b> (<c>RegisterAsync</c>) and when a
    /// resident <b>verifies</b> their account (<c>VerifyWithTokenAsync</c>) — the
    /// two <c>account.signup</c> / <c>account.verified</c> emitters (ADR 0077).
    /// An *additive* field on the singleton (ADR 0004 §B.1), the same shape as
    /// <see cref="IsSignupOpen"/> / <see cref="DefaultTimezone"/> /
    /// <see cref="DefaultDateFormat"/>. Defaults to <c>true</c> so a fresh
    /// instance ships with the admin notification on — a small neighborhood's
    /// admins want to know who is joining out of the box (the M6
    /// "null / empty = all enabled" lean-default and the <see cref="IsSignupOpen"/>
    /// <c>true</c> floor, ADR 0077 D1). An admin tightens it to <c>false</c> only
    /// if the signal becomes noise.
    /// </summary>
    public bool NotifyAdminsOnSignup { get; set; } = true;

    /// <summary>
    /// Whether signed-in residents may **comment on platform announcements**
    /// (ADR 0101). The <see cref="Kumunita.Core.Announcements"/> comment lane
    /// is the one that reads this: when <c>true</c> a signed-in user who can
    /// see an announcement may comment on it (and the comment list is shown
    /// on the detail page); when <c>false</c> the composer is hidden and no
    /// signed-in user may add a comment. A visitor (not signed in) can never
    /// comment regardless of this flag, and comments are always visible to
    /// signed-in users only — even on a public-scope announcement — under the
    /// announcement's own flat scope split (ADR 0101). An *additive* field on
    /// the singleton (ADR 0004 §B.1), the same shape as
    /// <see cref="IsSignupOpen"/> / <see cref="NotifyAdminsOnSignup"/>.
    /// Defaults to <c>true</c> so a fresh instance ships with the discussion
    /// lane open out of the box (the M6 lean-default <c>true</c> floor); an
    /// admin tightens it to <c>false</c> to silence the surface.
    /// </summary>
    public bool AnnouncementCommentsEnabled { get; set; } = true;
}
