using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kumunita.Core.Identity;
using Kumunita.Core.Media;
using Kumunita.Core.UserInfo;

namespace Kumunita.Core.Portability;

/// <summary>
/// The resident seam the Web layer + the tests target (M27 D7,
/// C-M27·7). The locked public surface (the design doc
/// <c>docs/design/m27-user-scoped-portability-design.md</c> §2.1, copied
/// verbatim by U01's shell; U03–U06 fill the bodies) — the resident
/// scope is a lane **on** the M11 operator portability surface (the
/// same <c>Kumunita.Core.Portability</c> context), not a new bounded
/// context.
/// <para>
/// **Resident lane, not an authorization lane (C-M27·7):** no
/// <c>AccessAction</c> / <c>AccessVia</c> / <c>IAuthorizationService</c>
/// branch / <c>Audience</c> — the <c>[Authorize]</c> verified-resident
/// gate (the Web <c>UserPortabilityController</c>) is the only decision,
/// and each **write** emits exactly one <c>AccessAudit</c> row
/// (<c>TargetKind "portability"</c>, <c>Via = Owner</c>, verb
/// <c>export</c> / <c>import</c> / <c>import.resolve</c>) — the
/// <em>service</em> emits it, the controller adds none (the ADR 0105
/// <c>messaging.toggle</c> shape).
/// </para>
/// </summary>
public interface IUserPortabilityService
{
    /// <summary>
    /// U03 (export) — one complete, valid <c>*.kumunita</c> archive at the
    /// resident scope (the D2 format: the same M11 archive + the
    /// <c>Scope = "resident"</c> manifest marker). Emits exactly one
    /// <c>portability.export</c> <c>AccessAudit</c> row (<c>Via = Owner</c>).
    /// </summary>
    /// <param name="residentSubjectId">The resident's own <c>subjectId</c> (the archive's scope anchor + the audit row's actor).</param>
    Task<Stream> ExportAsync(string residentSubjectId, CancellationToken ct = default);

    /// <summary>
    /// U04 (classify) — validate-then-classify, **no writes** (C-M27·5):
    /// the <c>clean</c>/<c>duplicate</c>/<c>conflict</c> classification
    /// (the §2.4 contract) + the per-entity reference-availability report
    /// runs to completion before any write. A <c>conflict</c> entity never
    /// auto-applies — it is surfaced for the resident's decision.
    /// </summary>
    /// <param name="residentSubjectId">The resident's own <c>subjectId</c> (the archive's scope anchor).</param>
    /// <param name="archive">The uploaded <c>*.kumunita</c> archive stream.</param>
    Task<UserPortabilityImportPlan> ClassifyAsync(
        string residentSubjectId, Stream archive, CancellationToken ct = default);

    /// <summary>
    /// U06 (resolve + apply) — apply **only** the resident-resolved
    /// entities, fail-closed (C-M27·4): a rejected / aborted resolve
    /// writes <b>nothing</b>; a <c>conflict</c> entity the resident did
    /// not resolve is <b>not applied</b> (no auto-merge). Emits exactly
    /// one <c>portability.import.resolve</c> <c>AccessAudit</c> row
    /// (<c>Via = Owner</c>).
    /// </summary>
    /// <param name="residentSubjectId">The resident's own <c>subjectId</c> (the audit row's actor).</param>
    /// <param name="plan">The U04 classification output (the <c>clean</c>/<c>duplicate</c>/<c>conflict</c> set).</param>
    /// <param name="resolutions">The resident's per-entity decisions (the §2.5 apply contract).</param>
    /// <param name="archive">The uploaded <c>*.kumunita</c> archive stream — the apply phase reads the entity rows from the archive (the M11 <c>PortabilityApplyDocuments</c> pattern: the <c>docs/{Type}.json</c> array bytes are the single source of truth for the apply). Optional for source compatibility with the U01 seam; the U08 Web resolve-POST (which re-uploads the archive on the resolve step) passes it. <c>null</c> → a fail-closed rejection (the apply reads from the archive, not the target store).</param>
    /// <param name="ct">Cancellation.</param>
    Task<UserPortabilityImportResult> ResolveAsync(
        string residentSubjectId,
        UserPortabilityImportPlan plan,
        IReadOnlyList<UserPortabilityEntityResolution> resolutions,
        Stream? archive = null,
        CancellationToken ct = default);
}

/// <summary>
/// The classify output (U04; the closed shapes, the design doc §2.1
/// verbatim) — the per-entity <c>clean</c>/<c>duplicate</c>/<c>conflict</c>
/// classification (the §2.4 contract) + the per-entity
/// reference-availability report (the <see cref="UserPortabilityAbsentReference"/>
/// list). A classify failure (a malformed / out-of-scope / unsupported
/// archive) returns <see cref="Ok"/> = <c>false</c> + the closed
/// <see cref="Failures"/> set and writes nothing.
/// </summary>
public sealed record UserPortabilityImportPlan(
    bool Ok,
    IReadOnlyList<UserPortabilityEntityClassification> Entities,
    IReadOnlyList<string> Failures);

/// <summary>
/// One in-scope entity's classification (the design doc §2.1 verbatim):
/// the <see cref="Status"/> (the closed
/// <see cref="UserPortabilityEntityStatus"/> set) + the
/// <see cref="AbsentReferences"/> report (<c>conflict</c> only) + the
/// <see cref="DuplicateId"/> (<c>duplicate</c> only — the existing
/// entity's id in the target).
/// </summary>
public sealed record UserPortabilityEntityClassification(
    string Kind,              // e.g. "Post", "Event", "Message"
    string EntityId,          // the id in the archive
    UserPortabilityEntityStatus Status,
    IReadOnlyList<UserPortabilityAbsentReference> AbsentReferences,
    string? DuplicateId);     // for Duplicate: the existing entity's id in the target

/// <summary>
/// The closed classification status (the design doc §2.1 verbatim).
/// </summary>
public enum UserPortabilityEntityStatus
{
    Clean,
    Duplicate,
    Conflict
}

/// <summary>
/// One absent reference in a <c>conflict</c> entity's report (the
/// design doc §2.1 verbatim) — the data the U07/U08 resolve-review UI
/// renders as the "add elsewhere → pick a target" picker.
/// </summary>
public sealed record UserPortabilityAbsentReference(
    string Kind,              // e.g. "Group", "Tag", "Component", "Page"
    string Field,             // e.g. "GroupId", "TagIds", "ComponentId"
    string Value);            // the id in the archive that is absent in the target

/// <summary>
/// The resident's per-entity decision (U05; the closed shapes, the
/// design doc §2.1 verbatim) — a <see cref="UserPortabilityResolutionKind"/>
/// (the closed <see cref="UserPortabilityResolutionKind"/> set) + the
/// <see cref="PickedTargetId"/> / <see cref="AbsentRefKind"/> /
/// <see cref="AbsentRefField"/> re-point (<c>AddElsewhere</c> only).
/// </summary>
public sealed record UserPortabilityEntityResolution(
    string Kind,              // the entity kind (must match a classification entry)
    string EntityId,          // the entity id (must match a classification entry)
    UserPortabilityResolutionKind Resolution,
    string? PickedTargetId,   // for AddElsewhere: the target the resident chose
    string? AbsentRefKind,    // for AddElsewhere: the absent-reference kind being re-pointed
    string? AbsentRefField);  // for AddElsewhere: the absent-reference field being re-pointed

/// <summary>
/// The closed per-entity decision kinds (the design doc §2.1 verbatim):
/// <see cref="AddElsewhere"/> (apply with the absent reference re-pointed
/// to <c>PickedTargetId</c>) or <see cref="Discard"/> (no write).
/// </summary>
public enum UserPortabilityResolutionKind
{
    AddElsewhere,
    Discard
}

/// <summary>
/// The resolve output (U06; the closed shapes, the design doc §2.1
/// verbatim) — the apply outcome (the §2.5 contract):
/// <see cref="Ok"/> + <see cref="AppliedCount"/> /
/// <see cref="DiscardedCount"/> + the closed <see cref="Failures"/> set
/// (a fail-closed rejection, e.g. a <c>PickedTargetId</c> the resident
/// has no standing over, leaves <see cref="Ok"/> = <c>false</c> + the
/// failure set and writes nothing).
/// </summary>
public sealed record UserPortabilityImportResult(
    bool Ok,
    int AppliedCount,
    int DiscardedCount,
    IReadOnlyList<string> Failures);

/// <summary>
/// The <see cref="IUserPortabilityService"/> shell (M27 U01) — the ctor
/// over the frozen seams (<see cref="Marten.IDocumentStore"/> for the
/// domain docs, <see cref="IUserInfoService"/> for the resident standing
/// reads, <see cref="IMediaStore"/> for the content-addressed bytes, the
/// Identity <see cref="UserManager{User}"/> for the resident's own
/// account) + the static registry / archive machinery (the
/// <see cref="PortabilityDocTypes"/> + <see cref="KumunitaArchive"/>
/// resolved from the frozen seams inside each method, as M11's
/// <see cref="PortabilityService"/> does).
/// <para>
/// <b>U01 ships signatures only</b> — all three members throw
/// <see cref="NotImplementedException"/>; U03 fills
/// <see cref="IUserPortabilityService.ExportAsync"/>, U04 fills
/// <see cref="IUserPortabilityService.ClassifyAsync"/>, U06 fills
/// <see cref="IUserPortabilityService.ResolveAsync"/> (the same "seam +
/// shell now, logic lands in the later units" posture as the M11
/// <c>PortabilityService</c> U01 pin). The U09–U11 tests + the U07/U08
/// Web surface target these members verbatim.
/// </para>
/// </summary>
public sealed class UserPortabilityService(
    Marten.IDocumentStore documentStore,
    IUserInfoService userInfoService,
    IMediaStore mediaStore,
    Microsoft.AspNetCore.Identity.UserManager<User> userManager) : IUserPortabilityService
{
    /// <summary>
    /// The U02 resident-scope filter (C-M27·3) — the <em>pure</em>
    /// composition over the U02 <see cref="UserScopeInventory"/>
    /// registry, driving off the POCO set directly. U03's
    /// <see cref="ExportAsync"/> calls this per in-scope doc-type array
    /// (the M11 <c>docs/{Type}.json</c> shape, filtered to this
    /// resident's rows only — the C-M27·3 ownership-not-read pin).
    /// <para>
    /// **The closed membership rule (the design doc §2.2, verbatim):**
    /// a doc type <c>T</c>'s rows are returned iff
    /// <see cref="UserScopeInventory.IsInScope"/> holds (an
    /// out-of-closed-set type — including every <see
    /// cref="UserScopeInventory.Excluded"/> row — contributes
    /// <em>zero</em> rows, the C-M27·5 fail-closed default at the
    /// filter level); and each row <c>r</c> of <c>T</c> is in scope iff
    /// the resident holds <em>at least one</em> of <c>T</c>'s locked
    /// ownership seats, resolved per <see cref="UserScopeEntry.
    /// OwnershipFields"/>:
    /// <list type="bullet">
    /// <item>a <em>direct</em> principal field (<c>AuthorId</c> /
    ///     <c>OwnerId</c> / <c>UserId</c> / <c>SubjectId</c> /
    ///     <c>CreatedBy</c>) — the property value must equal
    ///     <paramref name="residentSubjectId"/>; the nullable
    ///     translation variants (<c>PostTranslation</c> /
    ///     <c>ReplyTranslation</c> / <c>EventTranslation</c> /
    ///     <c>TodoTranslation</c> / <c>BoardTranslation</c> /
    ///     <c>ProjectTranslation</c>) — a <c>null</c> ownership value is
    ///     <em>not</em> in scope (the row is a system/operator-added
    ///     translation, not the resident's), so only a non-null value
    ///     equal to the resident qualifies;</item>
    /// <item><c>Conversation</c> — the two-principal union:
    ///     <c>ParticipantA</c> ∪ <c>ParticipantB</c> (either seat
    ///     held, <em>not</em> both — a row with the resident in either
    ///     seat is in scope);</item>
    /// <item><c>Message</c> — the <em>indirect</em> basis
    ///     (<see cref="UserScopeEntry.OwnershipFields"/> is empty,
    ///     <see cref="UserScopeEntry.ScopeBasis"/> says
    ///     "M11 ref map (via Conversation)"): the row's
    ///     <c>ConversationId</c> must be in
    ///     <paramref name="inScopeConversationIds"/> (the
    ///     <c>Conversation</c> rows the resident participates in,
    ///     resolved by U03 over the archive's own doc set — this method
    ///     takes it as a parameter so the filter stays a pure POCO
    ///     test, no doc-type cross-lookup of its own);</item>
    /// <item><c>Group</c> — the owner-or-member union:
    ///     <c>OwnerId</c> == the resident, <em>or</em> the resident
    ///     holds a <c>GroupMembership</c> row with
    ///     <c>UserId</c> == the resident for this <c>GroupId</c> (the
    ///     row-based seat, resolved by U03 over the archive's own
    ///     <c>GroupMembership</c> set — passed in as
    ///     <paramref name="residentGroupIds"/>).</item>
    /// </list>
    /// </para>
    /// <para>
    /// **Ordering + identity are preserved** — the returned list is a
    /// new list, in the input order, of the input's <em>own</em>
    /// instances (no copy, no clone — the archive's POCOs travel
    /// through the M11 archive writer as-is, the C-M27·1 format
    /// pin unchanged).
    /// </para>
    /// </summary>
    /// <param name="type">The doc type name (the <see
    ///     cref="UserScopeInventory.Entries"/> closed set).</param>
    /// <param name="docs">The input row set (the archive's
    ///     <c>docs/{Type}.json</c> array, already deserialized to POCOs
    ///     by the M11 <see cref="KumunitaArchive"/> reader — U03's
    ///     export loop feeds the target instance's rows here, per
    ///     type).</param>
    /// <param name="residentSubjectId">The resident's own
    ///     <c>subjectId</c> (the scope anchor — the C-M27·3
    ///     ownership test value).</param>
    /// <param name="inScopeConversationIds">The <c>ConversationId</c>
    ///     set the resident participates in (the
    ///     <c>Conversation</c> rows where the resident holds a
    ///     <c>ParticipantA</c>/<c>ParticipantB</c> seat) — <c>Message</c>
    ///     rows are in scope iff their <c>ConversationId</c> is in this
    ///     set. U03 resolves this over the archive's own
    ///     <c>Conversation</c> doc array before calling; <c>null</c>
    ///     (or empty) means "no conversations in scope", so
    ///     <c>Message</c> rows contribute nothing (the fail-closed
    ///     default).</param>
    /// <param name="residentGroupIds">The <c>GroupId</c> set where the
    ///     resident holds a <c>GroupMembership</c> row
    ///     (<c>UserId</c> == the resident) — a <c>Group</c> row is in
    ///     scope iff its <c>OwnerId</c> == the resident <em>or</em> its
    ///     <c>Id</c> is in this set. U03 resolves this over the
    ///     archive's own <c>GroupMembership</c> doc array before
    ///     calling; <c>null</c> (or empty) means "no membership rows",
    ///     so a <c>Group</c> row's only in-scope path is its
    ///     <c>OwnerId</c> (the fail-closed default).</param>
    /// <returns>A new list (the input order preserved) of the input's
    ///     own instances that are in the resident's scope — empty for
    ///     an out-of-closed-set <paramref name="type"/>, an empty
    ///     input, or no matching rows.</returns>
    public static IReadOnlyList<object> ScopeFilter(
        string type,
        IReadOnlyList<object> docs,
        string residentSubjectId,
        IReadOnlySet<string>? inScopeConversationIds = null,
        IReadOnlySet<string>? residentGroupIds = null)
    {
        if (!UserScopeInventory.IsInScope(type))
            return [];

        if (UserScopeInventory.Entries[type] is not { } entry)
            return [];

        var inScope = new List<object>(docs.Count);
        foreach (var doc in docs)
        {
            if (doc is null)
                continue;
            if (ScopeRowHoldsSeat(doc, entry, residentSubjectId,
                    inScopeConversationIds, residentGroupIds))
                inScope.Add(doc);
        }
        return inScope;
    }

    /// <summary>
    /// One row's seat test (the <see cref="ScopeFilter"/> loop body) —
    /// <c>true</c> iff the row holds at least one of its kind's locked
    /// ownership seats for the resident (the §2.2 rule, resolved per
    /// seat in the <see cref="UserScopeEntry.OwnershipFields"/> order,
    /// the two union kinds' row-based seats resolved via the
    /// <paramref name="inScopeConversationIds"/> /
    /// <paramref name="residentGroupIds"/> parameters the
    /// <see cref="ScopeFilter"/> doc-comment defines).
    /// </summary>
    private static bool ScopeRowHoldsSeat(
        object doc,
        UserScopeEntry entry,
        string residentSubjectId,
        IReadOnlySet<string>? inScopeConversationIds,
        IReadOnlySet<string>? residentGroupIds)
    {
        var type = doc.GetType();

        // The two union / indirect kinds, by their locked §2.2 seat
        // names (the <see cref="UserScopeEntry.OwnershipFields"/>
        // values are the exact locked names — the "ConversationId"
        // indirect-basis seat + the "GroupMembership.UserId" row-based
        // seat are the only two that are not plain principal fields on
        // the doc itself).
        if (entry.Type == "Message")
        {
            // The indirect basis — the ConversationId → Conversation
            // link (the resident is a participant), not a direct
            // field on the Message.
            var conversationId = (string?)type.GetProperty("ConversationId",
                BindingFlags.Public | BindingFlags.Instance)?.GetValue(doc);
            return conversationId is not null
                && inScopeConversationIds is not null
                && inScopeConversationIds.Contains(conversationId);
        }
        if (entry.Type == "Group")
        {
            // The owner-or-member union — OwnerId (direct) ∪
            // GroupMembership.UserId (the row-based seat, resolved by
            // the caller over the archive's GroupMembership array).
            var ownerId = (string?)type.GetProperty("OwnerId",
                BindingFlags.Public | BindingFlags.Instance)?.GetValue(doc);
            if (ownerId == residentSubjectId)
                return true;
            var groupId = (string?)type.GetProperty("Id",
                BindingFlags.Public | BindingFlags.Instance)?.GetValue(doc);
            return groupId is not null
                && residentGroupIds is not null
                && residentGroupIds.Contains(groupId);
        }

        // Every other in-scope kind — a direct principal field
        // (AuthorId / OwnerId / UserId / SubjectId / CreatedBy, or the
        // Conversation two-principal union's two seats). A null field
        // value (the nullable translation variants' AuthorId) is NOT
        // in scope — only a non-null value equal to the resident
        // qualifies.
        foreach (var field in entry.OwnershipFields)
        {
            var prop = type.GetProperty(field, BindingFlags.Public | BindingFlags.Instance);
            if (prop is null)
                continue;
            var value = prop.GetValue(doc);
            if (value is string s && s == residentSubjectId)
                return true;
        }
        return false;
    }

    /// <summary>
    /// The U02 business-key matcher (the D4 <c>duplicate</c> detector)
    /// — the <em>pure</em> composition over the U02 <see
    /// cref="UserBusinessKeys"/> registry, driving off the POCO set
    /// directly. U04's <see cref="ClassifyAsync"/> classifier calls this
    /// per in-scope entity (the design doc §2.4 rule (b): a
    /// <c>duplicate</c> entity is one whose business key matches an
    /// existing entity in the target — the §2.3 locked keys).
    /// <para>
    /// **The fail-closed default (C-M27·5):** <c>false</c> for any
    /// <paramref name="type"/> outside the closed §2.3 set, for a null
    /// argument, or for a candidate whose runtime type lacks the
    /// kind's locked field name (a malformed / out-of-scope candidate
    /// is <em>never</em> a duplicate — the §2.4 rule (a) <c>clean</c>
    /// classification proceeds on "no match"). No session, no write —
    /// a pure <c>bool</c> over the two POCOs, the plan's locked
    /// <c>bool Matches(string type, object a, object b)</c> shape.
    /// </para>
    /// </summary>
    /// <param name="type">The doc type name (the <see
    ///     cref="UserBusinessKeys.ByType"/> closed set — the 14 §2.3
    ///     kinds).</param>
    /// <param name="a">The candidate entity (the archive's row).</param>
    /// <param name="b">The existing entity in the target (the match
    ///     against which <paramref name="a"/> is compared).</param>
    /// <returns><c>true</c> iff every field in the kind's locked
    ///     business-key set is equal between the two candidates
    ///     (case-sensitive <see cref="object.Equals(object)"/>);
    ///     <c>false</c> otherwise (the fail-closed default).</returns>
    public static bool MatchBusinessKey(string type, object? a, object? b) =>
        UserBusinessKeys.Matches(type, a, b);

    /// <inheritdoc />
    public async Task<Stream> ExportAsync(string residentSubjectId, CancellationToken ct = default)
    {
        // ── JSON options (the M11 PortabilityExportDocuments pattern) ─────
        var jsonOpts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        // ── The name → Type map (the M11 NameToType + the 4 non-M11 types) ─
        // The 4 non-M11 types (drift-guard entry 1) are resolved locally;
        // they are NOT added to the frozen PortabilityDocTypes table.
        var nameToType = new Dictionary<string, Type>(PortabilityExportDocuments.NameToType, StringComparer.Ordinal);
        nameToType["InventoryItem"]    = typeof(Kumunita.Core.Inventory.InventoryItem);
        nameToType["Document"]         = typeof(Kumunita.Core.Documents.Document);
        nameToType["DocumentFolder"]   = typeof(Kumunita.Core.Documents.DocumentFolder);
        nameToType["Bookmark"]         = typeof(Kumunita.Core.Bookmarks.Bookmark);

        // ── Pre-query Conversation + GroupMembership (the seat sets) ──────
        // Message (indirect basis) needs inScopeConversationIds; Group
        // (owner-or-member union) needs residentGroupIds. Both are derived
        // from the resident's own rows before the main loop.
        await using var session = documentStore.QuerySession();

        var allConversations = await QueryAllRowsAsync(session, typeof(Kumunita.Core.Messaging.Conversation), ct);
        var inScopeConversations = ScopeFilter("Conversation", allConversations, residentSubjectId);
        var inScopeConversationIds = inScopeConversations
            .Select(r => (string?)r.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)?.GetValue(r))
            .Where(id => id is not null)
            .Select(id => id!)
            .ToHashSet(StringComparer.Ordinal);

        var allMemberships = await QueryAllRowsAsync(session, typeof(Kumunita.Core.UserInfo.GroupMembership), ct);
        var inScopeMemberships = ScopeFilter("GroupMembership", allMemberships, residentSubjectId);
        var residentGroupIds = inScopeMemberships
            .Select(r => (string?)r.GetType().GetProperty("GroupId", BindingFlags.Public | BindingFlags.Instance)?.GetValue(r))
            .Where(id => id is not null)
            .Select(id => id!)
            .ToHashSet(StringComparer.Ordinal);

        // ── Per-type doc export (the U02 scope filter over the M11 loop) ──
        var docs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var docCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var mediaIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (type, _) in UserScopeInventory.Entries)
        {
            ct.ThrowIfCancellationRequested();
            var docType = nameToType[type];

            // Conversation + GroupMembership were already queried above;
            // reuse the filtered results to avoid a double query.
            IReadOnlyList<object> inScopeRows;
            if (type == "Conversation")
            {
                inScopeRows = inScopeConversations;
            }
            else if (type == "GroupMembership")
            {
                inScopeRows = inScopeMemberships;
            }
            else
            {
                var allRows = await QueryAllRowsAsync(session, docType, ct);
                inScopeRows = ScopeFilter(type, allRows, residentSubjectId,
                    inScopeConversationIds, residentGroupIds);
            }

            // Serialize using the value's own runtime type — the ScopeFilter
            // output is a List<object> of the doc type's instances; the
            // declared List<T> input type must match the concrete list
            // element type (the M11 pattern serializes the raw List<T> from
            // ToListAsync; here the filtered list is List<object>, so the
            // runtime type is List<T> as well — GetType() resolves it).
            docs[type] = JsonSerializer.SerializeToUtf8Bytes(inScopeRows, inScopeRows.GetType(), jsonOpts);
            docCounts[type] = inScopeRows.Count;

            // Collect media references from the in-scope rows (the
            // resident's media scope: ImageIds / AttachmentIds /
            // AvatarId / MediaId — the design doc §2.2 "minimal
            // identity reference set").
            foreach (var row in inScopeRows)
            {
                var rowType = row.GetType();
                foreach (var propName in new[] { "ImageIds", "AttachmentIds" })
                {
                    if (rowType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(row)
                        is System.Collections.IEnumerable ids)
                    {
                        foreach (var id in ids)
                            if (id is string s && s.Length > 0)
                                mediaIds.Add(s);
                    }
                }
                foreach (var propName in new[] { "AvatarId", "MediaId" })
                {
                    if (rowType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(row)
                        is string singleId && singleId.Length > 0)
                        mediaIds.Add(singleId);
                }
            }
        }

        // ── Resident media (the M11 MediaExport pattern, filtered) ───────
        // Query the MediaObject catalog rows for the referenced media ids,
        // then read the payload bytes (the same OpenReadAsync + size-check
        // + manifest-entry pattern as the M11 MediaExport.ExportAsync).
        var catalogDict = (await QueryAllRowsAsync(session, typeof(MediaObject), ct))
            .Cast<MediaObject>()
            .ToDictionary(o => o.Id, StringComparer.Ordinal);

        var media = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var mediaManifest = new List<PortabilityMediaEntry>(mediaIds.Count);

        foreach (var mediaId in mediaIds)
        {
            ct.ThrowIfCancellationRequested();
            if (!catalogDict.TryGetValue(mediaId, out var obj))
                continue;
            if (string.IsNullOrWhiteSpace(obj.ContentType))
                continue;

            var byteStream = await mediaStore.OpenReadAsync(mediaId, ct);
            using (byteStream)
            {
                using var buffer = new MemoryStream();
                await byteStream.CopyToAsync(buffer, ct);
                var bytes = buffer.ToArray();

                if (bytes.LongLength != obj.SizeBytes)
                    throw new InvalidOperationException(
                        $"M27 media export — MediaObject '{mediaId}' payload is {bytes.LongLength} bytes " +
                        $"but the catalog records {obj.SizeBytes}; refusing to ship a mismatched size_bytes.");

                media[mediaId] = bytes;
                mediaManifest.Add(new PortabilityMediaEntry
                {
                    Id = mediaId,
                    SizeBytes = obj.SizeBytes,
                    ContentType = obj.ContentType,
                });
            }
        }

        // Include the resident's MediaObject catalog rows in the docs
        // section (the M11 docs/MediaObject.json shape, filtered to the
        // resident's media — the design doc §2.2 "minimal identity
        // reference set" pin).
        if (mediaIds.Count > 0)
        {
            var mediaObjs = mediaIds
                .Where(id => catalogDict.TryGetValue(id, out var o))
                .Select(id => catalogDict[id])
                .ToList();
            docs["MediaObject"] = JsonSerializer.SerializeToUtf8Bytes(mediaObjs, typeof(List<MediaObject>), jsonOpts);
            docCounts["MediaObject"] = mediaObjs.Count;
        }

        // ── No-secret principal (the M11 PortabilityPrincipal shape) ─────
        // The C-M27·2 boundary: only the eight allowed §principals fields
        // are read; PasswordHash / SecurityStamp / AccessToken / RefreshToken
        // / RecoveryCode are never touched.
        var user = await userManager.FindByIdAsync(residentSubjectId);
        if (user is null)
            throw new InvalidOperationException(
                $"M27 export — no Identity account for resident '{residentSubjectId}'.");
        var profile = await userInfoService.GetProfileAsync(residentSubjectId);
        var principal = new PortabilityPrincipal
        {
            SubjectId = user.Id,
            Username = user.UserName,
            Email = user.Email,
            NormalizedEmail = user.NormalizedEmail,
            DisplayName = profile?.DisplayName,
            Verified = profile?.Verified ?? false,
            Blocked = profile?.Blocked ?? false,
            Roles = (await userManager.GetRolesAsync(user)).ToList(),
        };
        var principals = KumunitaArchive.ToJson(new[] { principal });

        // ── Config (the resident does not own it — an empty block) ───────
        // The design doc §2.2 "Excluded" table: LocaleSettings /
        // LanguageCatalog / CommunityName are operator instance-identity
        // (travels via config.json in M11; the resident does not own it).
        // An empty PortabilityConfig keeps the archive structurally valid.
        var config = KumunitaArchive.ToJson(new PortabilityConfig());

        // ── Manifest finalize + the D2 resident-scope marker ─────────────
        var manifest = ManifestFinalize.Build(
            null, // the resident does not own the community name
            docCounts,
            mediaManifest,
            DateTimeOffset.UtcNow);
        manifest.Scope = "resident";
        manifest.ResidentSubjectId = residentSubjectId;

        // ── The one AccessAudit row (Via = Owner, verb export) ───────────
        // The service emits it; the controller adds none (the ADR 0105
        // messaging.toggle shape). Emitted after the archive is built
        // (a failed build throws before this point, so no audit row for a
        // refused export).
        await using var auditSession = documentStore.OpenSession(new Marten.Services.SessionOptions());
        auditSession.Store(new Authorization.AccessAudit
        {
            Id = System.Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = residentSubjectId,
            EffectivePrincipalId = residentSubjectId,
            Action = "portability.export",
            TargetKind = "portability",
            TargetId = "portability",
            Via = Authorization.AccessVia.Owner,
            Outcome = Authorization.AccessOutcome.Allow,
        });
        await auditSession.SaveChangesAsync(ct).ConfigureAwait(false);

        // ── Write the archive ────────────────────────────────────────────
        var stream = new MemoryStream();
        await KumunitaArchive.WriteAsync(stream, manifest, docs, media, principals, config, ct);
        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// Queries all rows of a doc type from the store (the M11
    /// <see cref="PortabilityExportDocuments"/> reflection-dispatched
    /// pattern: <c>IQuerySession.Query&lt;T&gt;()</c> →
    /// <c>Marten.QueryableExtensions.ToListAsync&lt;T&gt;</c>), returning
    /// the rows as a <c>List&lt;object&gt;</c> (the <see
    /// cref="ScopeFilter"/> input shape).
    /// </summary>
    private static async Task<List<object>> QueryAllRowsAsync(
        Marten.IQuerySession session, Type docType, CancellationToken ct)
    {
        var queryMethod = typeof(Marten.IQuerySession)
            .GetMethod(nameof(Marten.IQuerySession.Query), Type.EmptyTypes)!;
        var toListAsyncMethod = typeof(Marten.QueryableExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == nameof(Marten.QueryableExtensions.ToListAsync)
                        && m.IsGenericMethod
                        && m.GetGenericArguments().Length == 1
                        && m.GetParameters().Length >= 1
                        && m.GetParameters()[0].ParameterType.IsGenericType
                        && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition()
                           == typeof(System.Linq.IQueryable<>));

        var queryable = queryMethod.MakeGenericMethod(docType).Invoke(session, null)!;
        var listTask = toListAsyncMethod.MakeGenericMethod(docType)
            .Invoke(null, new object?[] { queryable, ct })!;
        await ((Task)listTask).ConfigureAwait(false);
        var list = (IList)listTask.GetType().GetProperty("Result")!.GetValue(listTask)!;
        return list.Cast<object>().ToList();
    }

    // ── U04 (classify) — the 4 non-M11 in-scope docs' reference fields ────
    // The drift-guard entry 1 docs (the design doc §2.3 "or the doc's own
    // definition for the four non-M11 docs") — the reference fields the
    // U04 classifier resolves against the target. The in-M11 docs use the
    // frozen <see cref="PortabilityDocTypes"/> reference map (verbatim).
    private static readonly Dictionary<string, IReadOnlyList<PortabilityReferenceField>> NonM11RefFields =
        new(StringComparer.Ordinal)
        {
            ["InventoryItem"]  = new[]
            {
                new PortabilityReferenceField("ComponentId", "Component"),
                new PortabilityReferenceField("AuthorId", PortabilityReferenceField.PrincipalTarget),
                new PortabilityReferenceField("CurrentHolderId", PortabilityReferenceField.PrincipalTarget),
            },
            ["Document"]       = new[]
            {
                new PortabilityReferenceField("MediaId", "MediaObject"),
                new PortabilityReferenceField("OwnerId", PortabilityReferenceField.PrincipalTarget),
                new PortabilityReferenceField("FolderId", "DocumentFolder"),
                new PortabilityReferenceField("TagIds", "Tag", IsArray: true),
            },
            ["DocumentFolder"] = new[]
            {
                new PortabilityReferenceField("ParentId", "DocumentFolder"),
            },
            ["Bookmark"]       = new[]
            {
                new PortabilityReferenceField("OwnerId", PortabilityReferenceField.PrincipalTarget),
                new PortabilityReferenceField("TargetId", "Post|Event|TodoItem|Announcement|Page"),
            },
        };

    /// <inheritdoc />
    /// <summary>
    /// U04 (classify) — the <c>clean</c>/<c>duplicate</c>/<c>conflict</c>
    /// classification over the uploaded resident archive (the design doc
    /// §2.4 contract, verbatim) + the per-entity reference-availability
    /// report. **Read-only** (C-M27·5 — the pre-write pin): the
    /// <see cref="Marten.IDocumentStore"/> is opened as a
    /// <c>QuerySession</c> (read-only), the <c>userManager.Users</c> read is
    /// the principal source (read-only), and **no**
    /// <c>session.Store</c> / <c>SaveChangesAsync</c> is called — a
    /// <c>conflict</c> entity is only ever <em>reported</em>, never
    /// applied (C-M27·4 — the resident's per-entity choice is the only
    /// apply path, owned by U06's <see cref="ResolveAsync"/>).
    /// </summary>
    public async Task<UserPortabilityImportPlan> ClassifyAsync(
        string residentSubjectId, Stream archive, CancellationToken ct = default)
    {
        // ── JSON options (the M11 PortabilityValidate pattern) ────────────
        var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // ── The name → Type map (the M11 NameToType + the 4 non-M11 types) ─
        var nameToType = new Dictionary<string, Type>(
            PortabilityExportDocuments.NameToType, StringComparer.Ordinal);
        nameToType["InventoryItem"]  = typeof(Kumunita.Core.Inventory.InventoryItem);
        nameToType["Document"]       = typeof(Kumunita.Core.Documents.Document);
        nameToType["DocumentFolder"] = typeof(Kumunita.Core.Documents.DocumentFolder);
        nameToType["Bookmark"]       = typeof(Kumunita.Core.Bookmarks.Bookmark);

        // ── Read the archive (the M11 KumunitaArchive reader) ─────────────
        var data = await KumunitaArchive.ReadAsync(archive, ct).ConfigureAwait(false);

        // ── Pre-pass: the target id sets (the M11 validate pre-pass, over
        //    the target store — read-only) ─────────────────────────────────
        await using var session = documentStore.QuerySession();

        // The in-scope types' target rows (the <c>duplicate</c> match set) +
        // their id sets (the <c>conflict</c> reference-resolution target).
        var targetRowsByType = new Dictionary<string, IReadOnlyList<object>>(StringComparer.Ordinal);
        var typeSetById = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var (type, _) in UserScopeInventory.Entries)
        {
            ct.ThrowIfCancellationRequested();
            var docType = nameToType[type];
            var targetRows = await QueryAllRowsAsync(session, docType, ct).ConfigureAwait(false);
            targetRowsByType[type] = targetRows;
            var idSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in targetRows)
                if (ReadId(row) is string id && id.Length > 0)
                    idSet.Add(id);
            typeSetById[type] = idSet;
        }

        // Referenced doc types that are not in-scope (e.g. <c>Announcement</c>
        // — the <c>Bookmark.TargetId</c> multi-kind union) — their id sets.
        foreach (var type in ReferencedDocTypes())
        {
            if (typeSetById.ContainsKey(type))
                continue;
            var docType = nameToType[type];
            var idSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in (await QueryAllRowsAsync(session, docType, ct).ConfigureAwait(false)))
                if (ReadId(row) is string id && id.Length > 0)
                    idSet.Add(id);
            typeSetById[type] = idSet;
        }

        // The principal set (the target's users — the resident is the anchor,
        // so their <c>→ principal</c> references always resolve).
        var principalIds = new HashSet<string>(StringComparer.Ordinal);
        var userList = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ToListAsync(userManager.Users, ct).ConfigureAwait(false);
        foreach (var u in userList)
            if (u.Id is string id && id.Length > 0)
                principalIds.Add(id);

        // The language set (the <c>→ LanguageCatalog</c> references).
        var languageCodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in (await QueryAllRowsAsync(
                session, typeof(Kumunita.Core.Localization.LanguageCatalog), ct).ConfigureAwait(false)))
            if (ReadId(row) is string lc && lc.Length > 0)
                languageCodes.Add(lc);

        // ── Classify each in-scope entity (the §2.4 contract) ─────────────
        var entities = new List<UserPortabilityEntityClassification>();
        foreach (var (type, _) in UserScopeInventory.Entries)
        {
            ct.ThrowIfCancellationRequested();

            // The archive's rows for this type (the in-scope entities to
            // classify). A type not in the archive contributes no entities.
            if (!data.Docs.TryGetValue(type, out var json))
                continue;
            var docType = nameToType[type];
            var listType = typeof(List<>).MakeGenericType(docType);
            if (JsonSerializer.Deserialize(json, listType, jsonOpts) is not System.Collections.IEnumerable archiveRows)
                continue;

            var targetRows = targetRowsByType[type];
            foreach (var archiveRow in archiveRows)
            {
                if (archiveRow is null)
                    continue;
                var entityId = ReadId(archiveRow) ?? "";

                // (b) duplicate — the U02 business-key match.
                var duplicate = targetRows
                    .FirstOrDefault(t => t is not null && MatchBusinessKey(type, archiveRow, t));
                if (duplicate is not null)
                {
                    entities.Add(new UserPortabilityEntityClassification(
                        Kind: type,
                        EntityId: entityId,
                        Status: UserPortabilityEntityStatus.Duplicate,
                        AbsentReferences: [],
                        DuplicateId: ReadId(duplicate)));
                    continue;
                }

                // (c) conflict — the M11 referential-integrity loop over
                //     the target (the §2.4 conflict reason shape).
                var absent = ResolveAbsentReferences(
                    type, archiveRow, typeSetById, principalIds, languageCodes);
                entities.Add(new UserPortabilityEntityClassification(
                    Kind: type,
                    EntityId: entityId,
                    Status: absent.Count > 0
                        ? UserPortabilityEntityStatus.Conflict
                        : UserPortabilityEntityStatus.Clean,
                    AbsentReferences: absent,
                    DuplicateId: null));
            }
        }

        // ── The no-write pin (C-M27·5) — no session.Store / SaveChanges ───
        // was called; the classification is read-only (the QuerySession +
        // the userManager.Users read are the only I/O, both read-only).
        return new UserPortabilityImportPlan(Ok: true, Entities: entities, Failures: []);
    }

    // ── U04 (classify) helpers (pure, over the POCO set) ──────────────────

    /// <summary>The doc types referenced by the in-scope types' reference
    /// fields (the M11 reference map + the 4 non-M11 docs' own fields) — the
    /// <c>conflict</c> reference-resolution target set.</summary>
    private static IEnumerable<string> ReferencedDocTypes()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (type, _) in UserScopeInventory.Entries)
        {
            foreach (var rf in GetReferenceFields(type))
            {
                if (rf.Target == PortabilityReferenceField.PrincipalTarget)
                    continue;
                if (rf.Target == PortabilityReferenceField.LanguageCatalogTarget)
                    continue;
                if (rf.Target.Contains('|', StringComparison.Ordinal))
                {
                    foreach (var part in rf.Target.Split('|'))
                        if (part.Length > 0)
                            set.Add(part);
                    continue;
                }
                set.Add(rf.Target);
            }
        }
        return set;
    }

    /// <summary>The reference fields for a type (the M11 reference map, or
    /// the 4 non-M11 docs' own fields).</summary>
    private static IReadOnlyList<PortabilityReferenceField> GetReferenceFields(string type)
    {
        if (PortabilityDocTypes.ByType.TryGetValue(type, out var entry))
            return entry.ReferenceFields;
        if (NonM11RefFields.TryGetValue(type, out var fields))
            return fields;
        return [];
    }

    /// <summary>The per-entity absent-reference report (the §2.4 (c)
    /// <c>conflict</c> reason — the M11 referential-integrity loop over the
    /// target, restructured into the
    /// <see cref="UserPortabilityAbsentReference"/> shape).</summary>
    private static List<UserPortabilityAbsentReference> ResolveAbsentReferences(
        string type, object row,
        Dictionary<string, HashSet<string>> typeSetById,
        HashSet<string> principalIds,
        HashSet<string> languageCodes)
    {
        var absent = new List<UserPortabilityAbsentReference>();
        var rowType = row.GetType();
        foreach (var rf in GetReferenceFields(type))
        {
            var value = ReadFieldValue(rowType, rf.Field, row);
            if (value is null)
                continue;  // a null field is a satisfied reference.

            HashSet<string>? targetSet = rf.Target switch
            {
                PortabilityReferenceField.PrincipalTarget => principalIds,
                PortabilityReferenceField.LanguageCatalogTarget => languageCodes,
                _ when rf.Target.Contains('|', StringComparison.Ordinal) => null,  // multi-kind
                _ when typeSetById.TryGetValue(rf.Target, out var ts) => ts,
                _ => null,
            };

            if (rf.IsArray)
            {
                if (value is not System.Collections.IList list)
                    continue;
                foreach (var item in list)
                {
                    if (item is not string id || id.Length == 0)
                        continue;
                    if (!RefResolves(id, rf, targetSet, typeSetById))
                        absent.Add(new UserPortabilityAbsentReference(rf.Target, rf.Field, id));
                }
            }
            else
            {
                if (value is not string id || id.Length == 0)
                    continue;
                if (!RefResolves(id, rf, targetSet, typeSetById))
                    absent.Add(new UserPortabilityAbsentReference(rf.Target, rf.Field, id));
            }
        }
        return absent;
    }

    /// <summary>Resolves one reference value against the field's target set
    /// (the M11 <c>ReferenceResolves</c> pattern; the multi-kind union for
    /// the 4 non-M11 docs' own fields).</summary>
    private static bool RefResolves(
        string id, PortabilityReferenceField rf,
        HashSet<string>? directTarget,
        Dictionary<string, HashSet<string>> typeSetById)
    {
        if (rf.Target.Contains('|', StringComparison.Ordinal))
        {
            foreach (var part in rf.Target.Split('|'))
            {
                if (part.Length == 0)
                    continue;
                if (typeSetById.TryGetValue(part, out var set) && set.Contains(id))
                    return true;
            }
            return true;  // a sentinel — treated as satisfied
        }
        return directTarget is not null && directTarget.Contains(id);
    }

    /// <summary>Reads one reference field's value off a row (the M11
    /// <c>ReadFieldValue</c> pattern — reflection on the property name,
    /// not per-type code).</summary>
    private static object? ReadFieldValue(Type rowType, string fieldName, object row)
    {
        var prop = rowType.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance);
        if (prop is null)
            return null;  // an absent field is a satisfied reference.
        return prop.GetValue(row);
    }

    /// <summary>The row's identity (the <c>Id</c> or <c>SubjectId</c> property,
    /// the M11 validate id-set convention — <c>Profile</c> is keyed by
    /// <c>SubjectId</c>, the rest by <c>Id</c>).</summary>
    private static string? ReadId(object row)
    {
        var t = row.GetType();
        var idProp = t.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)
            ?? t.GetProperty("SubjectId", BindingFlags.Public | BindingFlags.Instance);
        if (idProp is null)
            return null;
        if (idProp.PropertyType == typeof(string))
            return idProp.GetValue(row) as string;
        if (idProp.PropertyType == typeof(Guid))
            return (idProp.GetValue(row) as Guid?)?.ToString("N");
        return idProp.GetValue(row)?.ToString();
    }

    /// <inheritdoc />
    /// <summary>
    /// U06 (resolve + apply) — the <c>clean</c>/<c>duplicate</c>/<c>conflict</c>
    /// per-entity apply (the design doc §2.5 contract, verbatim) + the
    /// <c>AddElsewhere</c> re-point + the <c>Discard</c> no-write + the
    /// no-auto-merge pin (C-M27·4) + the fail-closed contract (C-M27·4).
    /// The apply reads the entity rows from the uploaded archive (the M11
    /// <see cref="PortabilityApplyDocuments"/> pattern: the
    /// <c>docs/{Type}.json</c> array bytes are the single source of truth
    /// for the apply). One commit (a single <c>SaveChangesAsync</c> — the
    /// C-M27·4 "one commit" pin). One <c>portability.import.resolve</c>
    /// <c>AccessAudit</c> row (<c>Via = Owner</c>).
    /// </summary>
    public async Task<UserPortabilityImportResult> ResolveAsync(
        string residentSubjectId,
        UserPortabilityImportPlan plan,
        IReadOnlyList<UserPortabilityEntityResolution> resolutions,
        Stream? archive = null,
        CancellationToken ct = default)
    {
        // ── Fail-closed: the apply reads from the archive ──────────────────
        // The §2.5 apply contract: the apply reads the entity rows from the
        // uploaded archive. A null archive is a fail-closed rejection (the
        // apply cannot proceed without the archive's rows).
        if (archive is null)
            return new UserPortabilityImportResult(false, 0, 0, ["archive.missing"]);

        // ── Read the archive (the M11 KumunitaArchive reader) ─────────────
        var data = await KumunitaArchive.ReadAsync(archive, ct).ConfigureAwait(false);

        // ── The name → Type map (the M11 NameToType + the 4 non-M11 types) ─
        var nameToType = new Dictionary<string, Type>(
            PortabilityExportDocuments.NameToType, StringComparer.Ordinal);
        nameToType["InventoryItem"]  = typeof(Kumunita.Core.Inventory.InventoryItem);
        nameToType["Document"]       = typeof(Kumunita.Core.Documents.Document);
        nameToType["DocumentFolder"] = typeof(Kumunita.Core.Documents.DocumentFolder);
        nameToType["Bookmark"]       = typeof(Kumunita.Core.Bookmarks.Bookmark);

        // ── Pre-pass: the standing check (the §2.5 (3) fail-closed rejection) ─
        // For every AddElsewhere resolution, the resident must have standing
        // over PickedTargetId (a group they are a member of, a tag they own,
        // a component they are a member of, a page they authored). A
        // PickedTargetId the resident has no standing over is a fail-closed
        // rejection (the entity is not applied; the failure is recorded in
        // UserPortabilityImportResult.Failures). The transaction has not
        // started yet, so no rows are written (the C-M27·4 fail-closed pin).
        await using var readSession = documentStore.QuerySession();
        foreach (var res in resolutions)
        {
            if (res.Resolution != UserPortabilityResolutionKind.AddElsewhere)
                continue;
            if (res.PickedTargetId is null)
                continue;
            if (!await HasStandingAsync(readSession, res.AbsentRefKind, res.PickedTargetId,
                    residentSubjectId, ct).ConfigureAwait(false))
            {
                return new UserPortabilityImportResult(
                    false, 0, 0,
                    [$"standing.denied:{res.Kind}:{res.EntityId}:{res.AbsentRefKind}:{res.PickedTargetId}"]);
            }
        }

        // ── Pre-pass: read the archive's rows for each entity type ────────
        // The apply reads the entity rows from the archive (the M11
        // PortabilityApplyDocuments pattern: the docs/{Type}.json array
        // bytes are the single source of truth for the apply).
        var archiveRowsByType = new Dictionary<string, List<object>>(StringComparer.Ordinal);
        foreach (var type in plan.Entities.Select(e => e.Kind).Distinct(StringComparer.Ordinal))
        {
            if (!data.Docs.TryGetValue(type, out var json))
                continue;
            var docType = nameToType[type];
            var listType = typeof(List<>).MakeGenericType(docType);
            var rows = (System.Collections.IList)JsonSerializer.Deserialize(json, listType,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            archiveRowsByType[type] = rows.Cast<object>().ToList();
        }

        // ── The resolution lookup (the per-entity decision) ────────────────
        // String key (the "Kind|EntityId" convention — no tuple comparer
        // needed, the M11 "string key" convention).
        var resolutionByEntity = new Dictionary<string, UserPortabilityEntityResolution>(
            StringComparer.Ordinal);
        foreach (var r in resolutions)
            resolutionByEntity[$"{r.Kind}|{r.EntityId}"] = r;

        // ── Apply the docs in the M11 dependency order (one commit) ────────
        // The locked order (the design doc §2.5 (1)): the clean entities are
        // applied in the M11 §inventory import order (parents before children
        // — the D7 order; the uniform loop, not per-type code). The
        // AddElsewhere entities are applied with the absent reference
        // re-pointed to PickedTargetId (the §2.5 (3) rule). The duplicate +
        // discarded + unresolved-conflict entities are not applied (the
        // C-M27·4 no-auto-merge pin).
        var appliedCount = 0;
        var discardedCount = 0;

        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        try
        {
            foreach (var entry in PortabilityDocTypes.InOrder())
            {
                ct.ThrowIfCancellationRequested();
                if (!UserScopeInventory.IsInScope(entry.Type))
                    continue;
                if (!archiveRowsByType.TryGetValue(entry.Type, out var rows))
                    continue;

                foreach (var row in rows)
                {
                    if (row is null)
                        continue;
                    var entityId = ReadId(row) ?? "";
                    var cls = plan.Entities
                        .FirstOrDefault(e => e.Kind == entry.Type && e.EntityId == entityId);
                    if (cls is null)
                        continue; // not in the plan (shouldn't happen)

                    // (1) clean → apply (the M11 import order, the §2.5 (1) rule).
                    if (cls.Status == UserPortabilityEntityStatus.Clean)
                    {
                        session.Store(row);
                        appliedCount++;
                        continue;
                    }

                    // (2) duplicate → not applied (the §2.5 (2) rule).
                    if (cls.Status == UserPortabilityEntityStatus.Duplicate)
                    {
                        discardedCount++;
                        continue;
                    }

                    // (3) conflict → the resident's decision.
                    if (!resolutionByEntity.TryGetValue($"{entry.Type}|{entityId}", out var res))
                    {
                        // C-M27·4 no-auto-merge pin: a conflict entity the
                        // resident did not resolve is not applied (there is
                        // no default, no fallback, no auto-re-point).
                        continue;
                    }

                    if (res.Resolution == UserPortabilityResolutionKind.Discard)
                    {
                        // §2.5 (3) Discard rule: not applied (no write).
                        discardedCount++;
                        continue;
                    }

                    if (res.Resolution == UserPortabilityResolutionKind.AddElsewhere)
                    {
                        // §2.5 (3) AddElsewhere rule: apply with the absent
                        // reference re-pointed to PickedTargetId (the target
                        // the resident chose). The re-point rule: the absent
                        // reference field is re-pointed to PickedTargetId.
                        if (res.AbsentRefField is not null && res.PickedTargetId is not null)
                        {
                            var prop = row.GetType().GetProperty(
                                res.AbsentRefField, BindingFlags.Public | BindingFlags.Instance);
                            if (prop is not null && prop.CanWrite)
                                prop.SetValue(row, res.PickedTargetId);
                        }
                        session.Store(row);
                        appliedCount++;
                        continue;
                    }
                }
            }

            // One commit (the C-M27·4 "one commit" pin) — every doc type's
            // rows are staged in a single session; one SaveChangesAsync
            // commits them atomically (a mid-apply failure is the
            // documented rollback path, never a silently-accepted
            // half-import).
            await session.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // C-M27·4 fail-closed pin: a mid-apply failure is the
            // documented rollback path (the transaction rolls back, no
            // rows are written).
            return new UserPortabilityImportResult(
                false, 0, 0, [$"apply.failed:{ex.Message}"]);
        }

        // ── Apply the media (the M11 PortabilityApplyMedia pattern) ────────
        // For each media byte in the archive, IMediaStore.PutAsync
        // (idempotent — the C-MED·4 dedup: identical bytes → same id →
        // no second volume file). The resident's own write lane for media
        // (not a new bulk importer).
        foreach (var (_, bytes) in data.Media)
        {
            ct.ThrowIfCancellationRequested();
            await mediaStore.PutAsync(bytes, null, "application/octet-stream",
                residentSubjectId, ct).ConfigureAwait(false);
        }

        // ── The one AccessAudit row (Via = Owner, verb import.resolve) ─────
        // The service emits it; the controller adds none (the ADR 0105
        // messaging.toggle shape). After a clean apply (a pre-pass failure
        // returned the closed failure set before this point, so no audit row
        // for a refused resolve — the same "no audit for a refused action"
        // posture as the export lane).
        await using var auditSession = documentStore.OpenSession(new Marten.Services.SessionOptions());
        auditSession.Store(new Authorization.AccessAudit
        {
            Id = System.Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = residentSubjectId,
            EffectivePrincipalId = residentSubjectId,
            Action = "portability.import.resolve",
            TargetKind = "portability",
            TargetId = "portability",
            Via = Authorization.AccessVia.Owner,
            Outcome = Authorization.AccessOutcome.Allow,
        });
        await auditSession.SaveChangesAsync(ct).ConfigureAwait(false);

        return new UserPortabilityImportResult(true, appliedCount, discardedCount, []);
    }

    // ── U06 (resolve) helpers — the resident standing check ─────────────

    /// <summary>
    /// The resident standing check (the §2.5 (3) fail-closed rejection —
    /// the resident must have standing over the <c>PickedTargetId</c>: a
    /// group they are a member of (or own), a tag they created, a
    /// component they are a member of, a page they authored). A business
    /// standing read, not an <c>AccessAction</c> (C-M27·7 — the frozen
    /// <c>IAuthorizationService</c> surface is unchanged).
    /// </summary>
    private async Task<bool> HasStandingAsync(
        Marten.IQuerySession session,
        string? targetKind, string targetId, string residentSubjectId,
        CancellationToken ct)
    {
        if (targetKind is null)
            return false;

        return targetKind switch
        {
            "Group" => await StandingGroupAsync(session, targetId, residentSubjectId, ct).ConfigureAwait(false),
            "Tag" => await StandingTagAsync(session, targetId, residentSubjectId, ct).ConfigureAwait(false),
            "Component" => await StandingComponentAsync(session, targetId, residentSubjectId, ct).ConfigureAwait(false),
            "Page" => await StandingPageAsync(session, targetId, residentSubjectId, ct).ConfigureAwait(false),
            _ => false, // an unknown target kind is a fail-closed rejection
        };
    }

    /// <summary>
    /// Group standing: the resident is a member (a <c>GroupMembership</c>
    /// row with <c>UserId</c> = the resident + <c>GroupId</c> = the target)
    /// or the owner (the <c>Group.OwnerId</c> = the resident).
    /// </summary>
    private static async Task<bool> StandingGroupAsync(
        Marten.IQuerySession session, string groupId, string residentSubjectId, CancellationToken ct)
    {
        // The GroupMembership row (the resident is a member).
        var memberships = await QueryAllRowsAsync(session, typeof(Kumunita.Core.UserInfo.GroupMembership), ct);
        foreach (var m in memberships)
        {
            var t = m.GetType();
            var gid = (string?)t.GetProperty("GroupId", BindingFlags.Public | BindingFlags.Instance)?.GetValue(m);
            var uid = (string?)t.GetProperty("UserId", BindingFlags.Public | BindingFlags.Instance)?.GetValue(m);
            if (gid == groupId && uid == residentSubjectId)
                return true;
        }
        // The Group.OwnerId (the resident is the owner).
        var groups = await QueryAllRowsAsync(session, typeof(Kumunita.Core.UserInfo.Group), ct);
        foreach (var g in groups)
        {
            var t = g.GetType();
            var gid = (string?)t.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)?.GetValue(g);
            var oid = (string?)t.GetProperty("OwnerId", BindingFlags.Public | BindingFlags.Instance)?.GetValue(g);
            if (gid == groupId && oid == residentSubjectId)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Tag standing: the resident created the tag (the <c>Tag.CreatedBy</c>
    /// = the resident — the ADR 0044 translate-standing owner).
    /// </summary>
    private static async Task<bool> StandingTagAsync(
        Marten.IQuerySession session, string tagId, string residentSubjectId, CancellationToken ct)
    {
        var tags = await QueryAllRowsAsync(session, typeof(Kumunita.Core.Tags.Tag), ct);
        foreach (var tag in tags)
        {
            var t = tag.GetType();
            var tid = (string?)t.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)?.GetValue(tag);
            var cb = (string?)t.GetProperty("CreatedBy", BindingFlags.Public | BindingFlags.Instance)?.GetValue(tag);
            if (tid == tagId && cb == residentSubjectId)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Component standing: the resident is a member (a
    /// <c>ComponentMembership</c> row with <c>UserId</c> = the resident +
    /// <c>ComponentId</c> = the target) or the component is mandatory
    /// (the ADR 0012 implicit membership).
    /// </summary>
    private static async Task<bool> StandingComponentAsync(
        Marten.IQuerySession session, string componentId, string residentSubjectId, CancellationToken ct)
    {
        // The ComponentMembership row (the resident is a member).
        var memberships = await QueryAllRowsAsync(session, typeof(Kumunita.Core.UserInfo.ComponentMembership), ct);
        foreach (var m in memberships)
        {
            var t = m.GetType();
            var cid = (string?)t.GetProperty("ComponentId", BindingFlags.Public | BindingFlags.Instance)?.GetValue(m);
            var uid = (string?)t.GetProperty("UserId", BindingFlags.Public | BindingFlags.Instance)?.GetValue(m);
            if (cid == componentId && uid == residentSubjectId)
                return true;
        }
        // The Component.Mandatory flag (the ADR 0012 implicit membership).
        var components = await QueryAllRowsAsync(session, typeof(Kumunita.Core.UserInfo.Component), ct);
        foreach (var c in components)
        {
            var t = c.GetType();
            var cid = (string?)t.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)?.GetValue(c);
            var mandatory = (bool)(t.GetProperty("Mandatory", BindingFlags.Public | BindingFlags.Instance)?.GetValue(c) ?? false);
            var enabled = (bool)(t.GetProperty("Enabled", BindingFlags.Public | BindingFlags.Instance)?.GetValue(c) ?? true);
            if (cid == componentId && mandatory && enabled)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Page standing: the resident authored the page (the
    /// <c>Page.AuthorId</c> = the resident).
    /// </summary>
    private static async Task<bool> StandingPageAsync(
        Marten.IQuerySession session, string pageId, string residentSubjectId, CancellationToken ct)
    {
        var pages = await QueryAllRowsAsync(session, typeof(Kumunita.Core.Pages.Page), ct);
        foreach (var p in pages)
        {
            var t = p.GetType();
            var pid = (string?)t.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)?.GetValue(p);
            var aid = (string?)t.GetProperty("AuthorId", BindingFlags.Public | BindingFlags.Instance)?.GetValue(p);
            if (pid == pageId && aid == residentSubjectId)
                return true;
        }
        return false;
    }
}
