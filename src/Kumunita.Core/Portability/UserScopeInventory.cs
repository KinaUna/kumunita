namespace Kumunita.Core.Portability;

/// <summary>
/// One D1 in-scope doc family's ownership mapping (the design doc
/// <c>docs/design/m27-user-scoped-portability-design.md</c> §2.2 table row,
/// copied verbatim by M27 U02). <see cref="OwnershipFields"/> are the
/// closed field names whose value(s) scope the entity:
/// <em>direct</em> rows (the vast majority) carry exactly one
/// principal-valued field (<c>AuthorId</c> / <c>OwnerId</c> /
/// <c>UserId</c> / <c>SubjectId</c> / <c>CreatedBy</c> — the field's
/// value must equal the resident's <c>subjectId</c> for the entity to be
/// in scope); <c>Conversation</c> carries the two-<c>principal</c> union
/// (<c>ParticipantA</c> ∪ <c>ParticipantB</c> — the resident is in scope
/// when they hold either seat); <c>Group</c> carries the owner-or-member
/// union (<c>OwnerId</c> ∪ the <c>GroupMembership.UserId</c> row of the
/// set, a <em>row-based</em> field); <c>Message</c> carries an empty
/// direct set — its scope is <em>indirect</em>, via its
/// <c>ConversationId</c> → <c>Conversation</c> (the <see
/// cref="ScopeBasis"/> says so). <see cref="Note"/> is the §2.2 table's
/// "Note" column verbatim (the ADR cross-references included); <see
/// cref="ScopeBasis"/> is the §2.2 "Source" column (M11 reference map vs.
/// the doc's own definition — the §2.8 drift-guard entries 1–2).
/// </summary>
public sealed record UserScopeEntry(
    string Type,
    IReadOnlyList<string> OwnershipFields,
    string ScopeBasis,
    string Note);

/// <summary>
/// The D1 <b>closed resident-scope inventory</b> as data (M27 U02) — the
/// exact closed set of content doc types M27 exports/imports at the
/// resident scope, the per-doc ownership field (or union / indirect
/// basis), the C-M27·3 <em>excluded</em> list, and the pure <see
/// cref="IsInScope"/> membership test the U03 export filter + the U04
/// classify loop drive off.
/// <para>
/// **The copy source is the design doc §2.2, verbatim** (U00 locked it;
/// the U00 handoff §(b) restates the same table) — this registry does
/// not re-derive it from the M11 <see cref="PortabilityDocTypes"/>
/// reference map, and it does not re-invent the reference-field names:
/// the in-M11-set docs' <c>→ principal</c> field names match the M11
/// reference map exactly (e.g. <c>Post</c>'s <c>AuthorId</c>), and the
/// four non-M11 docs (<c>InventoryItem</c> / <c>Document</c> /
/// <c>DocumentFolder</c> / <c>Bookmark</c>) + <c>Tag</c> +
/// <c>Profile</c> are sourced from their own definitions (the §2.8
/// drift-guard entries 1–2, recorded in each row's <see
/// cref="UserScopeEntry.ScopeBasis"/>).
/// </para>
/// <para>
/// **No service registration, no document, no <c>*DocTypes</c> surface,
/// no migration (D7):** referenced statically, like the M11
/// <see cref="PortabilityDocTypes"/> registry; <see
/// cref="PortabilityDocTypes"/> remains the M11 source of truth for the
/// 44-entry reference map (this registry names the same field names, it
/// does not redefine them).
/// </para>
/// </summary>
public static class UserScopeInventory
{
    /// <summary>
    /// The closed in-scope inventory (the design doc §2.2 table, verbatim
    /// — 29 doc families; <see cref="Entries"/> is the closed lookup by
    /// type name, <see cref="IsInScope"/> the membership test).
    /// </summary>
    public static IReadOnlyDictionary<string, UserScopeEntry> Entries { get; } =
        new Dictionary<string, UserScopeEntry>(StringComparer.Ordinal)
        {
            ["Post"]                 = new UserScopeEntry("Post", ["AuthorId"], "M11 ref map", "the resident's authored posts"),
            ["PostReply"]            = new UserScopeEntry("PostReply", ["AuthorId"], "M11 ref map", "the resident's authored replies"),
            ["PostTranslation"]      = new UserScopeEntry("PostTranslation", ["AuthorId"], "M11 ref map", "the resident's user-added translations (ADR 0022) — the ownership field is nullable"),
            ["ReplyTranslation"]     = new UserScopeEntry("ReplyTranslation", ["AuthorId"], "M11 ref map", "the resident's user-added translations (ADR 0022) — the ownership field is nullable"),
            ["AnnouncementComment"]  = new UserScopeEntry("AnnouncementComment", ["AuthorId"], "M11 ref map", "the resident's comments on announcements"),
            ["Event"]                = new UserScopeEntry("Event", ["AuthorId"], "M11 ref map", "the resident's authored events"),
            ["EventRsvp"]            = new UserScopeEntry("EventRsvp", ["UserId"], "M11 ref map", "the resident's own RSVPs"),
            ["EventTranslation"]     = new UserScopeEntry("EventTranslation", ["AuthorId"], "M11 ref map", "the resident's user-added event translations (ADR 0059) — the ownership field is nullable"),
            ["ProjectGoal"]          = new UserScopeEntry("ProjectGoal", ["AuthorId"], "M11 ref map", "the resident's authored goals"),
            ["Project"]              = new UserScopeEntry("Project", ["AuthorId"], "M11 ref map", "the resident's authored projects"),
            ["TodoItem"]             = new UserScopeEntry("TodoItem", ["AuthorId"], "M11 ref map", "the resident's authored to-dos"),
            ["KanbanBoard"]          = new UserScopeEntry("KanbanBoard", ["AuthorId"], "M11 ref map", "the resident's authored boards"),
            ["TodoTranslation"]      = new UserScopeEntry("TodoTranslation", ["AuthorId"], "M11 ref map", "the resident's user-added to-do translations (ADR 0088) — the ownership field is nullable"),
            ["BoardTranslation"]     = new UserScopeEntry("BoardTranslation", ["AuthorId"], "M11 ref map", "the resident's user-added board translations (ADR 0088) — the ownership field is nullable"),
            ["ProjectTranslation"]   = new UserScopeEntry("ProjectTranslation", ["AuthorId"], "M11 ref map", "the resident's user-added project translations (ADR 0088) — the ownership field is nullable"),
            ["TodoComment"]          = new UserScopeEntry("TodoComment", ["AuthorId"], "M11 ref map", "the resident's comments on to-dos"),
            ["Conversation"]         = new UserScopeEntry("Conversation", ["ParticipantA", "ParticipantB"], "M11 ref map", "conversations the resident participates in — the ownership is the two-principal union (ParticipantA ∪ ParticipantB)"),
            ["Message"]              = new UserScopeEntry("Message", [], "M11 ref map (via Conversation)", "messages in the resident's conversations — indirect scope: ConversationId → Conversation, the resident is a participant"),
            ["Page"]                 = new UserScopeEntry("Page", ["AuthorId"], "M11 ref map", "the resident's authored pages (blog)"),
            ["Tag"]                  = new UserScopeEntry("Tag", ["CreatedBy"], "Tag.cs (drift guard entry 1)", "the tags the resident created"),
            ["InventoryItem"]        = new UserScopeEntry("InventoryItem", ["AuthorId"], "InventoryItem.cs (drift guard entry 1)", "the inventory items the resident created"),
            ["Document"]             = new UserScopeEntry("Document", ["OwnerId"], "Document.cs (drift guard entry 1)", "the documents the resident uploaded"),
            ["DocumentFolder"]       = new UserScopeEntry("DocumentFolder", ["OwnerId"], "DocumentFolder.cs (drift guard entry 1)", "the document folders the resident created"),
            ["Bookmark"]             = new UserScopeEntry("Bookmark", ["OwnerId"], "Bookmark.cs (drift guard entry 1)", "the resident's bookmarks"),
            ["Profile"]              = new UserScopeEntry("Profile", ["SubjectId"], "Profile.cs", "the resident's own bio + tags (M23) + display identity"),
            ["Group"]                = new UserScopeEntry("Group", ["OwnerId", "GroupMembership.UserId"], "M11 ref map (drift guard entry 2)", "the groups the resident created or is a member of — needed to make their content resolvable on import; the union is OwnerId ∪ member (a GroupMembership row in the set with UserId = the resident)"),
            ["GroupMembership"]      = new UserScopeEntry("GroupMembership", ["UserId"], "M11 ref map", "the resident's own membership rows"),
            ["GroupInvitation"]      = new UserScopeEntry("GroupInvitation", ["UserId"], "M11 ref map", "invitations the resident received"),
            ["GroupJoinRequest"]     = new UserScopeEntry("GroupJoinRequest", ["UserId"], "M11 ref map", "join-requests the resident submitted"),
        };

    /// <summary>
    /// The C-M27·3 <b>excluded</b> set — the doc types that do NOT travel
    /// at the resident scope (the design doc §2.2 "Excluded" table,
    /// verbatim): the M11 "Excluded" operational/credential set
    /// (unchanged — C-M11·2), the operator instance-identity (travels via
    /// M11's <c>config.json</c>; the resident does not own it — D1 "out of
    /// scope"), the per-resident read state (system-generated, not
    /// authored), the identity-graph / authorization state (the M11
    /// identity graph — the M27 scope is the resident's <em>content</em>),
    /// the moderation state, the operator/GlobalAdmin content, the
    /// operator/Translator-managed translations, and the board
    /// sub-entities (they travel with the in-scope board via the M11
    /// apply order, not as independently-scoped resident content).
    /// <para>
    /// **Not a type row, but the C-M27·3 pin itself:** the <em>other
    /// residents'</em> authored rows (their <c>Post</c> / <c>Message</c> /
    /// <c>Document</c> / <c>Bookmark</c> / any other in-scope-type
    /// content) are excluded by the <see cref="IsInScope"/> ownership
    /// test, not by a type row — the in-scope types travel, scoped to
    /// this resident's rows. The U09 two-resident fixture pins exactly
    /// that: the same type, one resident's rows in, the other's out.
    /// </para>
    /// </summary>
    public static IReadOnlyList<(string Type, string Reason)> Excluded { get; } =
    [
        ("IdentityToken", "Credential material (C-M11·2 / C-M27·2 — the M11 \"Excluded\" set, unchanged)"),
        ("OutboxEmail", "Operational state (a fresh instance re-derives; the M11 \"Excluded\" set, unchanged)"),
        ("EmailDeadLetter", "Operational state (the M11 \"Excluded\" set, unchanged)"),
        ("AccessAudit", "Operational state (a log, not resident content; the M11 \"Excluded\" set, unchanged)"),
        ("AuditPurgeSummary", "Operational state (the M11 \"Excluded\" set, unchanged)"),
        ("LocaleSettings / LanguageCatalog", "Operator instance-identity (travels via config.json in M11; the resident does not own it — D1 \"out of scope\")"),
        ("Notification", "Per-resident read state (the M6 operational state; system-generated, not authored by the resident)"),
        ("NotificationPreference", "Per-resident read state (the M6 operational state; system-generated, not authored by the resident)"),
        ("NotificationSubscription", "Per-resident read state (the M6 operational state; system-generated, not authored by the resident)"),
        ("DelegationGrant", "Identity-graph / authorization state (the M11 identity graph; not the resident's authored content)"),
        ("GuardianLink", "Identity-graph / authorization state (the M11 identity graph; not the resident's authored content)"),
        ("ModeratorAssignment", "Identity-graph / authorization state (the M11 identity graph; not the resident's authored content)"),
        ("ComponentMembership", "Identity-graph / authorization state (the M11 identity graph; not the resident's authored content)"),
        ("Report", "Moderation state (a report is a moderation action, not the resident's authored content)"),
        ("Announcement", "Operator/GlobalAdmin content (authored by GlobalAdmin/Translator, not by a plain resident — the resident's AnnouncementComment is in scope, the Announcement itself is not)"),
        ("AnnouncementTranslation", "Operator/GlobalAdmin content (authored by GlobalAdmin/Translator, not by a plain resident)"),
        ("CommunityTranslation", "Operator/Translator-managed translations (the UI-string + operator-managed translation rows; not the resident's authored content)"),
        ("TranslationResource", "Operator/Translator-managed translations (the UI-string + operator-managed translation rows; not the resident's authored content)"),
        ("GroupTranslation", "Operator/Translator-managed translations (the UI-string + operator-managed translation rows; not the resident's authored content)"),
        ("TagTranslation", "Operator/Translator-managed translations (the UI-string + operator-managed translation rows; not the resident's authored content)"),
        ("KanbanLane", "Sub-entities of the resident's board (the KanbanBoard is in scope; its lanes + placements travel with the board via the M11 apply order, not as independently-scoped resident content)"),
        ("BoardItemPlacement", "Sub-entities of the resident's board (the KanbanBoard is in scope; its lanes + placements travel with the board via the M11 apply order, not as independently-scoped resident content)"),
    ];

    /// <summary>
    /// The closed-membership test (C-M27·3) — <c>true</c> only for the
    /// 29 <see cref="Entries"/> in-scope doc types; <c>false</c> for
    /// every <see cref="Excluded"/> row (including the five M11
    /// "Excluded" docs) and for any type name outside the closed set
    /// (an out-of-closed-set doc type is <em>never</em> in scope — the
    /// fail-closed default, the C-M27·5 pre-write posture at the
    /// inventory level).
    /// </summary>
    public static bool IsInScope(string type) =>
        Entries.ContainsKey(type);
}