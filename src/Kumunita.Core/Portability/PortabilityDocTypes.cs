using System.Collections;

namespace Kumunita.Core.Portability;

/// <summary>
/// One reference field on a content document (the §inventory "Reference
/// fields" column, the D7 reference map). The <see cref="Target"/> is the
/// target set the U05 integrity loop resolves the field's value(s) against:
/// <see cref="PrincipalTarget"/> (the <c>identity/principals.json</c>
/// <c>subjectId</c> set), <see cref="LanguageCatalogTarget"/> (the
/// <c>config.json</c> <c>languages[]</c> id set — read <em>before</em> the
/// docs integrity loop), <see cref="MultiKindTarget"/> (the kind-dependent
/// <c>TargetId</c> fields — Notification / NotificationSubscription — the
/// closed kind set), or a content doc type name (that type's
/// <c>docs/{Type}.json</c> id set — <c>MediaObject</c> included, its ids
/// also present in the manifest's media manifest). <see cref="IsArray"/>
/// marks the list-valued fields (<c>ImageIds</c> / <c>TagIds</c>).
/// </summary>
public sealed record PortabilityReferenceField(
    string Field,
    string Target,
    bool IsArray = false)
{
    /// <summary>A <c>→ principal</c> reference (a <c>subjectId</c>).</summary>
    public const string PrincipalTarget = "principal";

    /// <summary>A <c>→ LanguageCatalog</c> reference (a <c>LanguageCode</c>).</summary>
    public const string LanguageCatalogTarget = "LanguageCatalog";

    /// <summary>
    /// A kind-dependent <c>TargetId</c> reference (the closed kind set,
    /// per §inventory order 7 — the U05 loop resolves the value against
    /// whichever of the named types holds it).
    /// </summary>
    public const string MultiKindTarget = "Component|Group|Page|Announcement";
}

/// <summary>
/// One §inventory entry — the closed content inventory as <em>data</em>
/// (D7): the document <see cref="Type"/>, its archive <see cref="FileName"/>
/// (<c>docs/{Type}.json</c>), its import <see cref="Order"/> (parents
/// before children, 1-based), and its <see cref="ReferenceFields"/> (the
/// D7 reference map the U05 integrity loop drives). U01 copied the table
/// verbatim from <c>docs/design/m11-portability-design.md</c> §inventory
/// (LOCKED) — the <see cref="Entries"/> list is the 44-content-doc closed
/// set; the five excluded docs (<c>IdentityToken</c> / <c>OutboxEmail</c> /
/// <c>EmailDeadLetter</c> / <c>AccessAudit</c> / <c>AuditPurgeSummary</c>)
/// and the <c>config.json</c>-carried state (<c>LocaleSettings</c> /
/// <c>LanguageCatalog</c>) are deliberately <em>not</em> entries
/// (§inventory "Excluded" + drift guard entries 1–4).
/// </summary>
public sealed record PortabilityDocEntry(
    string Type,
    int Order,
    IReadOnlyList<PortabilityReferenceField> ReferenceFields)
{
    /// <summary>
    /// The archive file name — derived, <c>docs/{Type}.json</c> (the
    /// §layout pin: one JSON-array file per content doc type).
    /// </summary>
    public string FileName => DocsFileName(Type);

    /// <summary>The <c>docs/{Type}.json</c> archive file name (§layout).</summary>
    public static string DocsFileName(string type) => $"docs/{type}.json";
}

/// <summary>
/// The D7 closed inventory registry (M11, C-M11·4/5) — the ordered
/// <c>{ Type, FileName, Order, ReferenceFields }</c> list, a <see
/// cref="ByType"/> lookup, and the ordered iteration the U02 export loop +
/// the U05 apply / integrity loop consume <em>generically</em> (not
/// per-type code). Pure data — no service registration is needed
/// (referenced statically; the only DI registration in the context is the
/// <see cref="PortabilityService"/> shell, U01).
/// </summary>
public static class PortabilityDocTypes
{
    private static PortabilityReferenceField P(string field, bool isArray = false) =>
        new(field, PortabilityReferenceField.PrincipalTarget, isArray);

    private static PortabilityReferenceField L(string field) =>
        new(field, PortabilityReferenceField.LanguageCatalogTarget);

    private static PortabilityReferenceField R(string field, string target) =>
        new(field, target);

    private static PortabilityReferenceField A(string field, string target) =>
        new(field, target, IsArray: true);

    /// <summary>
    /// The exact closed inventory — 44 content docs in the 9 locked
    /// order-groups, copied verbatim from the design doc §inventory
    /// (the table rows are in the same order; the "Reference fields"
    /// column is the per-row mapping below).
    /// </summary>
    public static IReadOnlyList<PortabilityDocEntry> Entries { get; } = new List<PortabilityDocEntry>
    {
        // ── Order 1 — the base units (parents; no doc-level parent) ──
        new PortabilityDocEntry("Group", 1, new[] { P("OwnerId") }),
        new PortabilityDocEntry("Component", 2, Array.Empty<PortabilityReferenceField>()),
        new PortabilityDocEntry("MediaObject", 3, new[] { P("CreatedById") }),
        new PortabilityDocEntry("Tag", 4, Array.Empty<PortabilityReferenceField>()),

        // ── Order 2 — the identity graph (the M1 delegation/guardian/membership/moderator docs) ──
        new PortabilityDocEntry("Profile", 5, new[] { R("AvatarId", "MediaObject") }),
        new PortabilityDocEntry("DelegationGrant", 6, new[] { P("OwnerId"), P("DelegateId"), P("RevokedBy") }),
        new PortabilityDocEntry("GuardianLink", 7, new[] { P("GuardianId"), P("ChildId") }),
        new PortabilityDocEntry("GroupMembership", 8, new[] { R("GroupId", "Group"), P("UserId"), P("AddedBy") }),
        new PortabilityDocEntry("GroupInvitation", 9, new[] { R("GroupId", "Group"), P("UserId"), P("InvitedBy") }),
        new PortabilityDocEntry("GroupJoinRequest", 10, new[] { R("GroupId", "Group"), P("UserId") }),
        new PortabilityDocEntry("ModeratorAssignment", 11, new[] { R("ComponentId", "Component"), P("UserId"), P("GrantedBy") }),
        new PortabilityDocEntry("ComponentMembership", 12, new[] { R("ComponentId", "Component"), P("UserId") }),

        // ── Order 3 — the M1 translations (one row per (parent, language)) ──
        new PortabilityDocEntry("TranslationResource", 13, new[] { L("LanguageCode") }),
        new PortabilityDocEntry("GroupTranslation", 14, new[] { R("GroupId", "Group"), L("LanguageCode") }),
        new PortabilityDocEntry("CommunityTranslation", 15, new[] { R("ComponentId", "Component"), L("LanguageCode") }),
        new PortabilityDocEntry("TagTranslation", 16, new[] { R("TagId", "Tag"), L("LanguageCode") }),

        // ── Order 4 — the M3 content (posts / announcements + their translations/comments) ──
        new PortabilityDocEntry("Post", 17, new[] { R("ComponentId", "Component"), R("GroupId", "Group"), P("AuthorId"), A("ImageIds", "MediaObject"), A("TagIds", "Tag") }),
        new PortabilityDocEntry("PostReply", 18, new[] { R("PostId", "Post"), P("AuthorId"), A("ImageIds", "MediaObject") }),
        new PortabilityDocEntry("PostTranslation", 19, new[] { R("PostId", "Post"), L("LanguageCode"), P("AuthorId") }),
        new PortabilityDocEntry("ReplyTranslation", 20, new[] { R("ReplyId", "PostReply"), L("LanguageCode"), P("AuthorId") }),
        new PortabilityDocEntry("Report", 21, new[] { R("PostId", "Post"), P("ReporterId"), R("ComponentId", "Component") }),
        new PortabilityDocEntry("Announcement", 22, new[] { P("AuthorId"), R("CommunityId", "Component"), A("ImageIds", "MediaObject") }),
        new PortabilityDocEntry("AnnouncementTranslation", 23, new[] { R("AnnouncementId", "Announcement"), L("LanguageCode"), P("AuthorId") }),
        new PortabilityDocEntry("AnnouncementComment", 24, new[] { R("AnnouncementId", "Announcement"), P("AuthorId") }),

        // ── Order 5 — the M4 content (events + RSVPs + translations) ──
        new PortabilityDocEntry("Event", 25, new[] { R("ComponentId", "Component"), R("GroupId", "Group"), P("AuthorId") }),
        new PortabilityDocEntry("EventRsvp", 26, new[] { R("EventId", "Event"), P("UserId") }),
        new PortabilityDocEntry("EventTranslation", 27, new[] { R("EventId", "Event"), L("LanguageCode"), P("AuthorId") }),

        // ── Order 6 — the M5/PL content (projects + boards + to-dos + their translations/comments) ──
        new PortabilityDocEntry("ProjectGoal", 28, new[] { R("ComponentId", "Component"), P("AuthorId") }),
        new PortabilityDocEntry("Project", 29, new[] { R("GoalId", "ProjectGoal"), R("ComponentId", "Component"), P("AuthorId") }),
        new PortabilityDocEntry("TodoItem", 30, new[] { R("ComponentId", "Component"), R("ProjectId", "Project"), P("AuthorId"), P("AssigneeId"), R("ParentId", "TodoItem"), R("BlockedByTodoId", "TodoItem"), A("TagIds", "Tag"), A("ImageIds", "MediaObject") }),
        new PortabilityDocEntry("KanbanBoard", 31, new[] { R("ComponentId", "Component"), R("ProjectId", "Project"), P("AuthorId"), A("TagIds", "Tag") }),
        new PortabilityDocEntry("KanbanLane", 32, new[] { R("BoardId", "KanbanBoard") }),
        new PortabilityDocEntry("BoardItemPlacement", 33, new[] { R("TodoItemId", "TodoItem"), R("BoardId", "KanbanBoard"), R("LaneId", "KanbanLane") }),
        new PortabilityDocEntry("TodoTranslation", 34, new[] { R("TodoItemId", "TodoItem"), L("LanguageCode"), P("AuthorId") }),
        new PortabilityDocEntry("BoardTranslation", 35, new[] { R("BoardId", "KanbanBoard"), L("LanguageCode"), P("AuthorId") }),
        new PortabilityDocEntry("ProjectTranslation", 36, new[] { R("ProjectId", "Project"), L("LanguageCode"), P("AuthorId") }),
        new PortabilityDocEntry("TodoComment", 37, new[] { R("TodoId", "TodoItem"), R("ParentId", "TodoComment"), P("AuthorId") }),

        // ── Order 7 — the M6 content (notifications + the per-resident read state) ──
        new PortabilityDocEntry("Notification", 38, new[] { P("RecipientId"), new PortabilityReferenceField("TargetId", PortabilityReferenceField.MultiKindTarget) }),
        new PortabilityDocEntry("NotificationPreference", 39, new[] { P("RecipientId") }),
        new PortabilityDocEntry("NotificationSubscription", 40, new[] { P("RecipientId"), new PortabilityReferenceField("TargetId", PortabilityReferenceField.MultiKindTarget) }),

        // ── Order 8 — the M9 content (messaging) ──
        new PortabilityDocEntry("Conversation", 41, new[] { P("ParticipantA"), P("ParticipantB") }),
        new PortabilityDocEntry("Message", 42, new[] { R("ConversationId", "Conversation"), P("SenderId"), L("LanguageCode") }),

        // ── Order 9 — the PG content (pages + their translations) ──
        new PortabilityDocEntry("Page", 43, new[] { R("ParentId", "Page"), P("AuthorId"), R("ComponentId", "Component"), A("ImageIds", "MediaObject"), A("TagIds", "Tag") }),
        new PortabilityDocEntry("PageTranslation", 44, new[] { R("PageId", "Page"), L("LanguageCode"), P("AuthorId") }),
    };

    /// <summary>The closed inventory's entry count (44 — the U02/U05 loop size).</summary>
    public static int Count => Entries.Count;

    /// <summary>
    /// The <c>ByType</c> lookup (doc type name → entry). Case-sensitive,
    /// exact names — the names are the frozen §inventory pin.
    /// </summary>
    public static IReadOnlyDictionary<string, PortabilityDocEntry> ByType { get; } =
        Entries.ToDictionary(e => e.Type);

    /// <summary>Ordered iteration in import order (parents before children).</summary>
    public static IEnumerable<PortabilityDocEntry> InOrder() =>
        Entries.OrderBy(e => e.Order);

    /// <summary>Looks up one entry by doc type name (null if not in the closed set).</summary>
    public static PortabilityDocEntry? TryGet(string type) =>
        ByType.TryGetValue(type, out var entry) ? entry : null;
}
