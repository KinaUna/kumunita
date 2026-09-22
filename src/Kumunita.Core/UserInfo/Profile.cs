using Kumunita.Core.Authorization;

namespace Kumunita.Core.UserInfo;

/// <summary>
/// A resident's profile (ARCHITECTURE.md §5 — UserInfoModule documents, <c>mt</c> schema).
/// Stored from M1 so M2 never patches visibility onto a legacy document (the M1 design's
/// "emergent impact"). Document identity is <see cref="SubjectId"/> (the thin principal's
/// subject).
/// <para>
/// The directory lists every non-blocked resident's *basic* info (name + verified badge)
/// to every signed-in viewer — the platform is invitation-only and limited to residents,
/// so "who is here" is not a gated surface. <see cref="ContactVisibility"/> is the <b>single</b>
/// audience that gates a profile's *opt-in* contact block (email/phone) on the directory
/// detail and preview: <c>null</c> means the author opted out (never shown), a non-null
/// audience runs one <c>CanAsync</c> decision. <see cref="Visibility"/> is kept (document +
/// editor, author-controlled, ADR 0003) as the audience for the *detailed* non-contact
/// fields that will take it over once they exist — it does not hide a profile today.
/// </para>
/// <para>
/// <see cref="ExternalId"/> is reserved for federation (ADR 0001): IdentityModule is the only
/// component that knows the identity source, so the later OIDC <c>sub</c> swap is additive.
/// <see cref="HouseholdId"/> is display/metadata ONLY — the authorization path never reads
/// it; household-based visibility is expressed as a household *group* the owner grants
/// (ADR 0001-B).
/// </para>
/// </summary>
public sealed class Profile
{
    /// <summary>Document identity — one profile per account. Mapped in
    /// <c>M1DocTypes.Configure</c> via <see cref="Marten.StoreOptions.Schema"/>.Identity
    /// because Marten's default identity convention (a property named <c>Id</c>) won't
    /// pick this up. ADR 0004 §B.1.</summary>
    public string SubjectId { get; set; } = string.Empty;

    /// <summary>Reserved for federation (later OIDC <c>sub</c>); null until then.</summary>
    public string? ExternalId { get; set; }

    /// <summary>Display/metadata only — never consulted by the authorization path.</summary>
    public string? HouseholdId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Verified resident (the claim's <see cref="Identity.ThinPrincipal.IsVerifiedResident"/>
    /// is minted from here at sign-in). Unverified accounts cannot sign in.</summary>
    public bool Verified { get; set; }

    /// <summary>Blocked resident (a GlobalAdmin's suspension; ADR-style admin lane). A blocked
    /// account loses all role standing (no <c>Member</c>/<c>Moderator</c>/<c>GlobalAdmin</c>,
    /// so it cannot act or be granted standing) until unblocked — a reversible suspension that
    /// preserves the account and its documents. Evaluated at the Identity↔cookie seam
    /// (<see cref="Identity.ClaimShaping"/>'s Web factory) exactly as <see cref="Verified"/> is;
    /// no claim type is minted for it (the no-relational-data invariant holds: the effect is
    /// "no roles", read from <c>mt</c> at sign-in).</summary>
    public bool Blocked { get; set; }

    /// <summary>
    /// The author-controlled audience (ADR 0003: the author always controls their own
    /// content's audience), kept on the document + editor for the *detailed* non-contact
    /// profile fields (the audience that would gate them once such fields ship). At the
    /// directory/detail presentation layer the profile's <b>basic</b> info
    /// (<see cref="DisplayName"/> + <see cref="Verified"/>) is <b>not</b> gated: the
    /// platform is invitation-only and limited to residents, so "who is here" is not a
    /// gated surface — <see cref="Kumunita.Core.UserInfo.DirectoryService"/> lists every
    /// non-blocked resident to every signed-in viewer. Bootstrap default: <c>new
    /// Audience()</c> (an empty audience; see <see cref="ContactVisibility"/> for the
    /// short-circuit rule that applies to the actually-gated contact block).</summary>
    public Audience Visibility { get; set; } = new();

    /// <summary>
    /// The <b>single</b> audience gate on the directory/detail surface (M2 §2.4, invariant
    /// C-M2·1): who may see the contact block (<see cref="Address"/>/<see cref="Email"/>/<see cref="Phone"/>).
    /// Evaluated by <see cref="Kumunita.Core.UserInfo.DirectoryService"/> through the
    /// frozen <c>IAuthorizationService.CanAsync</c>: <c>null</c> short-circuits to "no
    /// contact block" with no decision and no audit row (the author opted out); a
    /// non-null audience runs one decision and one <c>AccessAudit</c> row. The profile's
    /// basic info renders regardless of this gate — only the contact block is gated.
    /// </summary>
    public Audience? ContactVisibility { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    /// <summary>
    /// The resident's street address, shown to neighbors in the directory. Like
    /// <see cref="Email"/>/<see cref="Phone"/>, it is an opt-in field: gated by the same
    /// <see cref="ContactVisibility"/> audience so it only renders on the directory
    /// list/detail when the author has opted in <i>and</i> the viewer's decision allows.
    /// Free-text (a single street line) — the repo has no structured address sub-model.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// The profile's avatar media object id (ADR 0011; C-MED·8) →
    /// <c>MediaObject.Id</c> (a content hash). Nullable: no avatar set. This is
    /// an *additive* field (ADR 0004 §B.1), like M3's `Post.Status`.
    /// </summary>
    public string? AvatarId { get; set; }

    /// <summary>
    /// The resident's IANA time zone id (e.g. <c>Europe/Warsaw</c>) — the user's
    /// <b>override</b> of the platform default (ADR 0019; <see
    /// cref="Localization.LocaleSettings.DefaultTimezone"/> is the fallback when
    /// this is null). Stored as an IANA id so it is unambiguous and round-trips
    /// with <see cref="System.TimeZoneInfo"/>. Nullable: <c>null</c> means the
    /// resident uses the instance default (the "preference if present" shape,
    /// the same resolution order as the language preference). An *additive*
    /// field (ADR 0004 §B.1), like <see cref="AvatarId"/>.
    /// </summary>
    public string? TimeZone { get; set; }

    /// <summary>
    /// The resident's date-time <i>format</i> — a .NET custom datetime format
    /// string (e.g. <c>yyyy-MM-dd HH:mm</c>) — the user's <b>override</b> of
    /// the platform default (ADR 0020; <see
    /// cref="Localization.LocaleSettings.DefaultDateFormat"/> is the fallback
    /// when this is null). Stored as the format string itself (not a preset id)
    /// so a resident may pick a curated preset <i>or</i> a custom format; a
    /// preset is simply a well-known format string. Nullable: <c>null</c> means
    /// the resident uses the instance default (the "preference if present"
    /// shape, the same resolution order as <see cref="TimeZone"/>). An
    /// *additive* field (ADR 0004 §B.1), like <see cref="TimeZone"/>.
    /// </summary>
    public string? DateFormat { get; set; }

    /// <summary>
    /// The resident's <b>email &amp; notification language</b> — a BCP-47
    /// language code (e.g. <c>de</c>, <c>fr</c>, <c>da</c>) — the user's
    /// <b>preference</b> for the language the platform writes to them in:
    /// outbound account emails (verification) and event reminders resolve their
    /// body/subject through this code first, then the instance default
    /// (<see cref="Localization.LocaleSettings.DefaultLanguageCode"/>), then the
    /// <c>en</c> registry floor (ADR 0005 / the provider floor). Unlike the
    /// other overrides above, this does not change the resident's own UI or
    /// rendering — it is strictly the *outbound channel's* language, so a
    /// resident who browses in one language can still choose to receive their
    /// emails in another. Stored as the BCP-47 code itself (not a preset id) so
    /// it round-trips with <see cref="System.Globalization.CultureInfo"/> and the
    /// <c>TranslationProvider</c>'s HTTP-free resolution path. Nullable:
    /// <c>null</c> means the resident uses the instance default (the "preference
    /// if present" shape, the same resolution order as the UI language
    /// preference). An *additive* field (ADR 0004 §B.1), like
    /// <see cref="TimeZone"/> and <see cref="DateFormat"/>.
    /// </summary>
    public string? EmailLanguage { get; set; }
}

/// <summary>A profile contact-surface update (the M1 bootstrap surface — the author's own
/// profile; the profile *editing* UI and directory visibility rules are M2). Null fields
/// leave the current value untouched.</summary>
public sealed record ProfileUpdate(
    string? DisplayName,
    string? Email,
    string? Phone,
    Audience? Visibility,
    Audience? ContactVisibility,
    /// <summary>The resident's address (see <see cref="Profile.Address"/>). Appended with a
    /// default after the frozen M1/M2 five-field shape so existing positional call sites
    /// compile unchanged; null leaves the current value untouched (the "null ⇒ don't touch"
    /// patch rule every other field follows).</summary>
    string? Address = null);
