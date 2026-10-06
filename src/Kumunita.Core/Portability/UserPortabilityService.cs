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
    Task<UserPortabilityImportResult> ResolveAsync(
        string residentSubjectId,
        UserPortabilityImportPlan plan,
        IReadOnlyList<UserPortabilityEntityResolution> resolutions,
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

    /// <inheritdoc />
    public Task<UserPortabilityImportPlan> ClassifyAsync(string residentSubjectId, Stream archive, CancellationToken ct = default)
        => throw new NotImplementedException("M27 U04 — the clean/duplicate/conflict classification (no writes, C-M27·5).");

    /// <inheritdoc />
    public Task<UserPortabilityImportResult> ResolveAsync(
        string residentSubjectId,
        UserPortabilityImportPlan plan,
        IReadOnlyList<UserPortabilityEntityResolution> resolutions,
        CancellationToken ct = default)
        => throw new NotImplementedException("M27 U06 — the fail-closed per-entity resolve apply (C-M27·4).");
}
