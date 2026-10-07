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

    /// <summary>    /// M19 (ADR 0120, D1) — the guest standing flag. <c>true</c> means this
    /// account is a guest (a limited-privilege, outside-the-resident-circle
    /// account — a consultant/coach/teacher/speaker/entertainer). A guest is
    /// still a real Identity account (real <c>User</c>, real <c>Profile</c>,
    /// real <c>SubjectId</c>) so the frozen authorization seams and the audit
    /// log treat it exactly like any other actor (C-M19·1). A guest **never**
    /// holds <c>Member</c> (C-M19·2) and its access is limited to the
    /// <c>GuestAccess.AllowedSurfaces</c> set within the bounded window
    /// (D3/D4). The flag is additive (ADR 0004 §B.1) — a <c>false</c> default
    /// means every existing account is non-guest, no migration.
    /// </summary>
    public bool IsGuest { get; set; }

    /// <summary>    /// The author-controlled audience (ADR 0003: the author always controls their own
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

    // M23 ADD (ADR 0123 D1/D5, ADR 0004 §B.1 additive — the 9th/10th additive
    // Profile fields after AvatarId, TimeZone, DateFormat, EmailLanguage):
    /// <summary>
    /// The resident's **biography** (M23, ADR 0123 D1/D5) — the rich-content body
    /// (Markdown + optional in-content images, the ADR 0025/0031 shape), rendered
    /// on the directory detail by the single <c>MarkdownRenderer.RenderHtml</c>
    /// helper. Stored as the authored Markdown <c>string</c>; <c>null</c>/empty
    /// means "no bio" (the detail renders no bio block, C-M23·5). Gated by
    /// <see cref="Visibility"/> (C-M23·1, D2 — the M2 "kept for the detailed
    /// fields" audience finally takes over). An **additive** field (ADR 0004
    /// §B.1), like <see cref="EmailLanguage"/>: delta-detected, idempotent, no
    /// re-seed, no EF migration.
    /// </summary>
    public string? Bio { get; set; }

    /// <summary>
    /// The resident's **author-set tags** (M23, ADR 0123 D1/D3) — the
    /// <c>string</c> <see cref="Kumunita.Core.Tags.Tag"/> ids the resident picked
    /// (skills, interests, knowledge, expertise). References the **frozen ADR 0044
    /// shared <c>Tag</c> doc** by <c>Tag.Id</c> — the
    /// <see cref="Kumunita.Core.Posts.Post.TagIds"/> /
    /// <see cref="Kumunita.Core.Pages.Page.TagIds"/> idiom verbatim (a third
    /// referencer of the same doc, **not** a new tag type, C-M23·6/D1). Default
    /// <see cref="IReadOnlyList{T}">empty</see> — a profile with no tags reads
    /// back with an empty list (the ADR 0004 §B.1 additive no-reseed pin). A tag
    /// is a **label, never a gate** (C-TG·1 carried to profiles, C-M23·6). An
    /// **additive** field (ADR 0004 §B.1), like <see cref="EmailLanguage"/>.
    /// </summary>
    public IReadOnlyList<string> TagIds { get; set; } = [];

    // M22 ADD (ADR 0132 D1; ADR 0004 §B.1 additive — the 11th additive Profile
    // field after AvatarId, TimeZone, DateFormat, EmailLanguage, Bio, TagIds):
    /// <summary>
    /// M22 (ADR 0132, D1) — the onboarding completion stamp. <c>null</c> =
    /// the resident has not finished the guided walk-through (the floor: the
    /// banner shows, the nav entry is present). A non-null value = finished /
    /// skipped; the banner clears. Written only by the owner-scope
    /// <see cref="IUserInfoService.CompleteOnboardingAsync"/> lane (D2); read
    /// through the existing <see cref="IUserInfoService.GetProfileAsync"/>
    /// read (never a claim, D6). Additive per ADR 0004 §B.1: delta-detected,
    /// idempotent, no re-seed, no EF migration, no new <c>*DocTypes</c> surface.
    /// </summary>
    public DateTimeOffset? OnboardingCompletedAt { get; set; }

    // M9-amendment (per-resident messaging opt-in; ADR 0004 §B.1 additive — the
    // 12th additive Profile field after AvatarId, TimeZone, DateFormat,
    // EmailLanguage, Bio, TagIds, OnboardingCompletedAt):
    /// <summary>
    /// The resident's own <b>messaging opt-in</b> (M9 amendment on top of ADR
    /// 0105 — the per-user control the instance-level
    /// <see cref="Localization.LocaleSettings.MessagingEnabled"/> toggle
    /// gates over). <c>true</c> = this resident participates in 1:1 direct
    /// messaging (may open/send); <c>false</c> = the resident has opted out
    /// (cannot open a conversation or send a message; the Messages surface is
    /// hidden for them). Read by the
    /// <see cref="Kumunita.Core.Messaging.IMessagingService"/> per-actor gate
    /// alongside the instance toggle (the master gate) and, for a supervised
    /// child, the guardian's restriction ceiling (the guardian lane reads this
    /// child's active <see cref="GuardianLink"/> set to decide the ceiling; a
    /// restriction always wins over the child's own choice). An *additive*
    /// field (ADR 0004 §B.1), like <see cref="OnboardingCompletedAt"/>:
    /// delta-detected, idempotent, no re-seed, no EF migration, no new
    /// <c>*DocTypes</c> surface. Defaults to <c>false</c> (the ADR 0105
    /// opt-in convention — a privacy-sensitive capability is off until the
    /// resident chooses it on).
    /// </summary>
    public bool MessagingOptIn { get; set; } = false;

    /// <summary>
    /// The <b>guardian's messaging restriction</b> over a supervised child
    /// (M9 amendment — the parent/guardian control). A <b>ceiling</b> (the
    /// normal parental-restriction model): <c>true</c> = a guardian has
    /// <b>forced messaging OFF</b> for this child, and it stays OFF no matter
    /// what the child's own <see cref="MessagingOptIn"/> says; <c>false</c> =
    /// the guardian has <b>allowed</b> messaging, deferring the decision to
    /// the child's own opt-in. Read by the
    /// <see cref="Kumunita.Core.Messaging.IMessagingService"/> per-actor gate
    /// as a hard veto over <see cref="MessagingOptIn"/> (the restriction always
    /// wins; the allowance merely lifts the veto). Written only by the
    /// <see cref="IUserInfoService.SetChildMessagingRestrictionAsync"/> lane
    /// (a guardian with an active <see cref="GuardianLink"/> — the
    /// <see cref="IUserInfoService.SuspendChildAsync"/> / <see
    /// cref="Profile.Blocked"/> precedent: a guardian action that writes a
    /// single flag on the profile, one active-link standing gate). For an
    /// unsupervised resident this stays <c>false</c> (no guardian to set it),
    /// so the gate reduces to the resident's own opt-in. An *additive* field
    /// (ADR 0004 §B.1), like <see cref="Blocked"/>: delta-detected, idempotent,
    /// no re-seed, no EF migration.
    /// </summary>
    public bool MessagingRestricted { get; set; } = false;

    /// <summary>
    /// The <b>guardian's community block</b> over a supervised child — the set of
    /// community (component) ids a guardian has <b>blocked access to and hidden</b>
    /// for the child. A <b>ceiling</b>, like <see cref="MessagingRestricted"/> (the
    /// parental-restriction model), but per-community rather than a single flag:
    /// an id in this list is excluded from the child's effective community set on
    /// every access surface (the community directory/sidebar, the community feed,
    /// the posting gate, and the audience visibility of community-scoped posts) —
    /// which is exactly why it works for a <b>mandatory</b> community, whose
    /// membership is implicit and cannot be removed (ADR 0012: the removal lanes
    /// refuse / skip it, so "removing the child" is impossible there). Removing an
    /// id restores full access (the child's own membership — explicit or mandatory —
    /// stands again). Written only by the
    /// <see cref="IUserInfoService.SetChildCommunityBlockAsync"/> lane (a guardian
    /// with an active <see cref="GuardianLink"/> — the
    /// <see cref="IUserInfoService.SetChildMessagingRestrictionAsync"/> precedent:
    /// a guardian action over the child, one active-link standing gate, one audit
    /// row). For an unsupervised resident this stays empty (no guardian to set it),
    /// so the effective set reduces to the ordinary membership. An *additive* field
    /// (ADR 0004 §B.1), like <see cref="MessagingRestricted"/> / <see cref="TagIds"/>:
    /// delta-detected, idempotent, no re-seed, no EF migration.
    /// </summary>
    public IReadOnlyList<string> BlockedCommunityIds { get; set; } = [];

    /// <summary>
    /// The <b>guardian's event-attendance policy</b> over a supervised child
    /// (the lane's <see cref="EventRsvpMode"/> — one of three guardian
    /// postures the guardian may select). This is a <b>per-child preference
    /// the guardian sets</b>, not a per-resident field. The stored default is
    /// <see cref="EventRsvpMode.GuardianApproves"/> (the lane's
    /// most-protective posture); an <b>unsupervised</b> resident is
    /// effectively <see cref="EventRsvpMode.ChildDecides"/> — the gate only
    /// consults this field once an active <see cref="GuardianLink"/> exists,
    /// so a resident with no guardian RSVPs freely regardless of the stored
    /// value. Written only
    /// by the <see cref="IUserInfoService.SetChildEventRsvpModeAsync"/> lane
    /// (a guardian with an active <see cref="GuardianLink"/> — the
    /// <see cref="IUserInfoService.SetChildMessagingRestrictionAsync"/> /
    /// <see cref="Profile.MessagingRestricted"/> precedent: a guardian action
    /// that writes a single value on the profile, one active-link standing
    /// gate, one audit row). Read by the
    /// <see cref="Kumunita.Core.Events.EventService"/> RSVP gate to decide
    /// whether a supervised child's own <c>RsvpAsync</c> write is refused
    /// (the guardian must call the approval lane), allowed-and-notified, or
    /// allowed outright. An *additive* field (ADR 0004 §B.1), like
    /// <see cref="MessagingRestricted"/> / <see cref="BlockedCommunityIds"/>:
    /// delta-detected, idempotent, no re-seed, no EF migration.
    /// </summary>
    public EventRsvpMode EventRsvpMode { get; set; } = EventRsvpMode.GuardianApproves;

    /// <summary>
    /// The resident's <b>items-per-page preference</b> for every paged list
    /// surface (the community / all-sections / group post feeds, events,
    /// projects, boards, to-dos, goals, announcements, tag-by-tag posts &amp;
    /// pages, search, find-people, inventory, documents, and messaging).
    /// Nullable: <c>null</c> means the resident has not set a value, so the
    /// platform default (<see cref="Kumunita.Core.Query.PageSizer.Default"/> =
    /// 10) applies — the "preference if present" shape, the same resolution
    /// order as <see cref="TimeZone"/> / <see cref="DateFormat"/>. Written
    /// only by the owner-scope
    /// <see cref="IUserInfoService.SetProfilePageSizeAsync"/> lane (the
    /// <see cref="SetProfileTimezoneAsync"/> single-write-lane shape — the
    /// self-scope check happens at the Web boundary); read by every feed
    /// controller per request, clamped to
    /// <see cref="Kumunita.Core.Query.PageSizer.Min"/>…
    /// <see cref="Kumunita.Core.Query.PageSizer.Max"/> before being passed to
    /// the paged Core seam. An *additive* field (ADR 0004 §B.1), like
    /// <see cref="TimeZone"/>: delta-detected, idempotent, no re-seed, no EF
    /// migration.
    /// </summary>
    public int? PageSize { get; set; }

    // ADD (ADR 0149 D1; ADR 0004 §B.1 additive — the 13th additive Profile
    // field after AvatarId, TimeZone, DateFormat, EmailLanguage, Bio, TagIds,
    // OnboardingCompletedAt, MessagingOptIn, MessagingRestricted,
    // BlockedCommunityIds, EventRsvpMode, PageSize):
    /// <summary>
    /// The resident's **hide-home-intro preference** (ADR 0149, D1) — the
    /// home page's two top sections: the "Intro: what Kumunita is and does"
    /// hero and the "What Kumunita does" three-surfaces band. <c>true</c> =
    /// hide both for this resident, so a signed-in resident lands straight on
    /// the "What's new" feed; <c>false</c> (the default) = show them, exactly
    /// as before. This is a pure *display* preference — it changes what this
    /// resident sees on <c>/</c>, never any authorization or data. Written only
    /// by the owner-scope
    /// <see cref="IUserInfoService.SetProfileHideHomeIntroAsync"/> lane (the
    /// <see cref="IUserInfoService.SetProfilePageSizeAsync"/> single-write-lane
    /// shape — the self-scope check happens at the Web boundary); read by the
    /// <see cref="Kumunita.Web.Controllers.HomeController"/> per request. An
    /// *additive* field (ADR 0004 §B.1), like <see cref="PageSize"/>:
    /// delta-detected, idempotent, no re-seed, no EF migration.
    /// </summary>
    public bool HideHomeIntro { get; set; } = false;
}

/// <summary>
/// The <b>guardian's event-attendance policy</b> over a supervised child's
/// RSVPs — the lane's <see cref="Profile.EventRsvpMode"/> field (one of three
/// mutually exclusive postures a guardian may select, the <see cref
/// "Kumunita.Core.UserInfo.Profile.MessagingRestricted"/> /
/// <see cref="Kumunita.Core.Authorization.AccessVia"/> closed-vocabulary
/// shape). The <see cref="Kumunita.Core.Events.EventService"/> RSVP gate
/// reads this to decide how the child's own <c>RsvpAsync</c> write is
/// treated, and whether the guardian holds a standing to approve / deny /
/// veto the child's attendance on their behalf.
/// </summary>
public enum EventRsvpMode
{
    /// <summary>
    /// The most-protective posture and the <b>default for a supervised
    /// child</b> (the lane's stated default — the consent wording the
    /// creating / accepting guardian already agreed to: "you must approve or
    /// deny all group and event invitations"): the child's own <see
    /// cref="Kumunita.Core.Events.IEventService.RsvpAsync"/> self-lane is
    /// <b>refused</b> (a supervised child with an active <see cref
    /// "GuardianLink"/> gets a user-presentable refusal, the GU group-invitation
    /// self-lane precedent); the child's only path to an RSVP is for their
    /// guardian to call the approval lane (the
    /// <see cref="IUserInfoService"/>'s sibling
    /// <see cref="Kumunita.Core.Events.IEventService
    /// .GuardianApproveEventRsvpAsync"/>). The guardian is <b>notified</b>
    /// when the child asks (via the
    /// <see cref="Kumunita.Core.Events.GuardianEventRequest"/> row the child's
    /// refused self-RSVP creates) so they can act.
    /// </summary>
    GuardianApproves,

    /// <summary>
    /// A middle posture: the child's own <see
    /// cref="Kumunita.Core.Events.IEventService.RsvpAsync"/> self-lane is
    /// <b>allowed</b> (the RSVP lands exactly as an unsupervised resident's
    /// would); every such write is <b>notified</b> to the child's active
    /// guardian(s) (the
    /// <see cref="Kumunita.Core.Notifications.NotificationKinds
    /// .GuardianEventRsvp"/> kind — inbox + email), and the guardian retains a
    /// standing to <b>veto</b> (remove) any of the child's existing RSVPs via
    /// the <see cref="Kumunita.Core.Events.IEventService
    /// .GuardianVetoEventRsvpAsync"/> lane (a hard ceiling over the child's
    /// own choice, the <see cref="MessagingRestricted"/> shape).
    /// </summary>
    GuardianNotifies,

    /// <summary>
    /// The least-restrictive posture (and the <b>effective</b> posture for
    /// any resident with <b>no</b> active guardian — the gate only consults
    /// this field once an active <see cref="GuardianLink"/> exists, so an
    /// unsupervised resident always RSVPs freely regardless of the stored
    /// value): the child's own <see
    /// cref="Kumunita.Core.Events.IEventService.RsvpAsync"/> self-lane is
    /// <b>allowed outright</b>, with <b>no</b> notification to any guardian and
    /// <b>no</b> standing for a guardian to veto (a guardian who wants a
    /// ceiling can switch this child to <see cref="GuardianApproves"/> or
    /// <see cref="GuardianNotifies"/> via
    /// <see cref="IUserInfoService.SetChildEventRsvpModeAsync"/>).
    /// </summary>
    ChildDecides
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
    string? Address = null,
    /// <summary>The resident's biography (see <see cref="Profile.Bio"/>). M23 (ADR 0123 D3)
    /// — the rich-content body. Appended with a default after the frozen six-field shape so
    /// existing positional call sites compile unchanged; null leaves the current value
    /// untouched (the "null ⇒ don't touch" patch rule).</summary>
    string? Bio = null,
    /// <summary>The resident's author-set tags (see <see cref="Profile.TagIds"/>). M23
    /// (ADR 0123 D3) — the list of <see cref="Kumunita.Core.Tags.Tag"/> ids (the create-or-get
    /// resolve is U02's). Appended with a default after the frozen seven-field shape so
    /// existing positional call sites compile unchanged; null leaves the current value
    /// untouched.</summary>
    IReadOnlyCollection<string>? TagIds = null);
