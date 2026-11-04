using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kumunita.Core.Identity;
using Kumunita.Core.Media;
using Kumunita.Core.Usage;
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
/// entity's id in the target) + the
/// <see cref="RejectedReason"/> (<c>rejected</c> only — the SEC-lane H1
/// <see cref="ImportOwnershipGuard"/> reason code: the row is hostile for
/// this actor — forged ownership, an <c>Id</c> collision with a row another
/// resident owns, or an access-granting row — so it is never importable).
/// </summary>
public sealed record UserPortabilityEntityClassification(
    string Kind,              // e.g. "Post", "Event", "Message"
    string EntityId,          // the id in the archive
    UserPortabilityEntityStatus Status,
    IReadOnlyList<UserPortabilityAbsentReference> AbsentReferences,
    string? DuplicateId,      // for Duplicate: the existing entity's id in the target
    string? RejectedReason = null); // for Rejected: the ImportOwnershipGuard reason code (user-visible); null otherwise

/// <summary>
/// The closed classification status (the design doc §2.1 verbatim).
/// <see cref="Rejected"/> is the SEC-lane (H1) append (2026-10-30): an
/// archive row the <see cref="ImportOwnershipGuard"/> found hostile for the
/// importing actor. Appended after <see cref="Conflict"/> — the three
/// M27 members keep their order (no renumbering).
/// </summary>
public enum UserPortabilityEntityStatus
{
    Clean,
    Duplicate,
    Conflict,
    Rejected
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
public sealed partial class UserPortabilityService(
    Marten.IDocumentStore documentStore,
    IUserInfoService userInfoService,
    IMediaStore mediaStore,
    Microsoft.AspNetCore.Identity.UserManager<User> userManager,
    IStorageSettingsService? storageSettings = null,
    IStorageMetricsService? storageMetrics = null,
    Microsoft.Extensions.Options.IOptions<MediaOptions>? mediaOpts = null) : IUserPortabilityService
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
    /// <summary>
    /// C10-3b (IMPROVE-CODE) — the <b>orchestrator</b> over the per-phase
    /// strategy seams extracted from the pre-split monolith: the §inventory
    /// per-type scope-filter + media-id collection (<see cref="ExportDocsByTypeAsync"/>
    /// — the per-entity-kind loop), the resident-media payload leg
    /// (<see cref="ExportMediaAsync"/>), the no-secret principal leg
    /// (<see cref="ExportPrincipalAsync"/>), the manifest + D2 marker leg
    /// (<see cref="ExportManifest"/>), and the one <c>portability.export</c>
    /// <c>AccessAudit</c> row (<see cref="EmitPortabilityAuditRowAsync"/>
    /// — shared with the resolve lane). The public signature is the frozen
    /// C08/C09 seam; only the body changed.
    /// </summary>
    public async Task<Stream> ExportAsync(string residentSubjectId, CancellationToken ct = default)
    {
        // ── JSON options (the M11 PortabilityExportDocuments pattern) ─────
        var jsonOpts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        // ── The name → Type map (the M11 NameToType + the 4 non-M11 types) ─
        var nameToType = BuildNameToTypeMap();

        await using var session = documentStore.QuerySession();

        // ── Per-phase strategy legs (the per-entity-kind extraction) ──────
        var (docs, docCounts, mediaIds) = await ExportDocsByTypeAsync(session, nameToType, jsonOpts, residentSubjectId, ct);
        var (media, mediaManifest, catalogDict) = await ExportMediaAsync(session, mediaIds, ct);
        if (mediaIds.Count > 0)
        {
            var mediaObjs = mediaIds
                .Where(id => catalogDict.TryGetValue(id, out var o))
                .Select(id => catalogDict[id])
                .ToList();
            docs["MediaObject"] = JsonSerializer.SerializeToUtf8Bytes(mediaObjs, typeof(List<MediaObject>), jsonOpts);
            docCounts["MediaObject"] = mediaObjs.Count;
        }
        var principals = await ExportPrincipalAsync(residentSubjectId);
        var config = KumunitaArchive.ToJson(new PortabilityConfig());
        var manifest = ExportManifest(docCounts, mediaManifest, residentSubjectId);
        await EmitPortabilityAuditRowAsync("portability.export", residentSubjectId, ct);

        // ── Write the archive ────────────────────────────────────────────
        var stream = new MemoryStream();
        await KumunitaArchive.WriteAsync(stream, manifest, docs, media, principals, config, ct);
        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// C10-3b (IMPROVE-CODE) — the per-type <b>name → Type map</b> strategy
    /// leg, extracted from <see cref="ExportAsync"/> / <see cref="ClassifyAsync"/>
    /// / <see cref="ResolveAsync"/> (the three monoliths each duplicated the
    /// M11 <see cref="PortabilityExportDocuments.NameToType"/> clone + the 4
    /// non-M11 drift-guard entry 1 types). One shared map — the 4 non-M11
    /// types are resolved locally, NOT added to the frozen
    /// <see cref="PortabilityDocTypes"/> table (the C-M11·5 closed-set pin).
    /// </summary>
    private static Dictionary<string, Type> BuildNameToTypeMap()
    {
        var nameToType = new Dictionary<string, Type>(PortabilityExportDocuments.NameToType, StringComparer.Ordinal);
        nameToType["InventoryItem"]    = typeof(Kumunita.Core.Inventory.InventoryItem);
        nameToType["Document"]         = typeof(Kumunita.Core.Documents.Document);
        nameToType["DocumentFolder"]   = typeof(Kumunita.Core.Documents.DocumentFolder);
        nameToType["Bookmark"]         = typeof(Kumunita.Core.Bookmarks.Bookmark);
        return nameToType;
    }

    /// <summary>
    /// C10-3b (IMPROVE-CODE) — the <b>per-entity-kind export loop</b>
    /// (the design doc §2.2 closed §inventory, the per-type strategy seam)
    /// extracted from <see cref="ExportAsync"/>: the two-principal seat sets
    /// (Conversation in-scope id set + Group owner-or-member set, the
    /// Message indirect-basis / Group union seats the <see cref="ScopeFilter"/>
    /// doc-comment defines) are resolved once, then every in-scope
    /// <c>UserScopeInventory.Entries</c> kind is scope-filtered to the
    /// resident's own rows (the C-M27·3 ownership-not-read pin), serialized
    /// to the <c>docs/{Type}.json</c> bytes, and its media-reference ids
    /// (ImageIds / AttachmentIds / AvatarId / MediaId — the §2.2 "minimal
    /// identity reference set") are collected for the media leg. Ordering +
    /// identity of the in-scope rows are preserved (no copy, no clone — the
    /// C-M27·1 format pin). Returns the docs-by-type bytes, the doc-count
    /// map, and the referenced media-id set.
    /// </summary>
    private async Task<(Dictionary<string, byte[]> Docs,
        Dictionary<string, int> DocCounts,
        HashSet<string> MediaIds)>
        ExportDocsByTypeAsync(
            Marten.IQuerySession session,
            Dictionary<string, Type> nameToType,
            JsonSerializerOptions jsonOpts,
            string residentSubjectId,
            CancellationToken ct)
    {
        // ── Pre-query Conversation + GroupMembership (the seat sets) ──────
        // Message (indirect basis) needs inScopeConversationIds; Group
        // (owner-or-member union) needs residentGroupIds. Both are derived
        // from the resident's own rows before the main loop.
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

            foreach (var row in inScopeRows)
                CollectMediaReferenceIds(row, mediaIds);
        }

        return (docs, docCounts, mediaIds);
    }

    /// <summary>
    /// C10-3b (IMPROVE-CODE) — one in-scope row's media-reference id
    /// collection (the design doc §2.2 "minimal identity reference set":
    /// the <c>ImageIds</c> / <c>AttachmentIds</c> array seats + the
    /// <c>AvatarId</c> / <c>MediaId</c> single seats), extracted from the
    /// <see cref="ExportDocsByTypeAsync"/> per-type loop body as the
    /// per-row strategy leg. Reflection over the locked property names
    /// (the M11 <c>ReadFieldValue</c> pattern — no per-type code); a
    /// missing property or null value contributes no id (fail-closed
    /// default).
    /// </summary>
    private static void CollectMediaReferenceIds(object row, HashSet<string> mediaIds)
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

    /// <summary>
    /// C10-3b (IMPROVE-CODE) — the <b>resident media payload leg</b>
    /// (the M11 <see cref="MediaExport"/> pattern, filtered), extracted
    /// from <see cref="ExportAsync"/>: the MediaObject catalog rows are
    /// read once, then each referenced media id (the
    /// <see cref="ExportDocsByTypeAsync"/> media-id set) is opened via
    /// <see cref="IMediaStore.OpenReadAsync"/>, its payload byte-length is
    /// checked against the catalog's <c>size_bytes</c> (a mismatch throws —
    /// refusing to ship a corrupt byte), and the bytes + manifest entry are
    /// recorded. Returns the media-by-id bytes, the manifest entries, and
    /// the catalog dictionary (reused by the <c>docs/MediaObject.json</c>
    /// leg).
    /// </summary>
    private async Task<(Dictionary<string, byte[]> Media,
        List<PortabilityMediaEntry> MediaManifest,
        Dictionary<string, MediaObject> CatalogDict)>
        ExportMediaAsync(
            Marten.IQuerySession session,
            HashSet<string> mediaIds,
            CancellationToken ct)
    {
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

        return (media, mediaManifest, catalogDict);
    }

    /// <summary>
    /// C10-3b (IMPROVE-CODE) — the <b>no-secret principal leg</b> (the M11
    /// <see cref="PrincipalsExport"/> shape), extracted from
    /// <see cref="ExportAsync"/>: the resident's Identity account is
    /// resolved, their profile read, and exactly the eight
    /// <c>PortabilityPrincipal</c> allowed fields are read (the C-M27·2
    /// boundary: PasswordHash / SecurityStamp / AccessToken / RefreshToken /
    /// RecoveryCode are never touched). Throws <see cref="InvalidOperationException"/>
    /// when no Identity account exists for the resident (a fail-closed
    /// export refusal — no audit row for a refused export).
    /// </summary>
    private async Task<byte[]> ExportPrincipalAsync(string residentSubjectId)
    {
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
        return KumunitaArchive.ToJson(new[] { principal });
    }

    /// <summary>
    /// C10-3b (IMPROVE-CODE) — the <b>manifest + D2 resident-scope marker
    /// leg</b> (the M11 <see cref="ManifestFinalize"/> shape + the D2
    /// <c>Scope = "resident"</c> marker), extracted from
    /// <see cref="ExportAsync"/>: the doc-count + media-manifest maps are
    /// folded into the manifest, then the resident-scope marker fields are
    /// set (the design doc §2.2 "Excluded" table: the resident does not own
    /// the community name — <c>null</c>; LocaleSettings / LanguageCatalog /
    /// CommunityName are operator instance-identity, the config leg writes
    /// the empty <c>PortabilityConfig</c>).
    /// </summary>
    private static PortabilityManifest ExportManifest(
        Dictionary<string, int> docCounts,
        List<PortabilityMediaEntry> mediaManifest,
        string residentSubjectId)
    {
        var manifest = ManifestFinalize.Build(
            null, // the resident does not own the community name
            docCounts,
            mediaManifest,
            DateTimeOffset.UtcNow);
        manifest.Scope = "resident";
        manifest.ResidentSubjectId = residentSubjectId;
        return manifest;
    }

    /// <summary>
    /// C10-3b (IMPROVE-CODE) — the <b>one AccessAudit row leg</b> (the ADR
    /// 0105 <c>messaging.toggle</c> shape: the service emits it, the
    /// controller adds none), extracted from <see cref="ExportAsync"/> and
    /// shared with <see cref="ResolveAsync"/> (the <c>portability.export</c>
    /// / <c>portability.import.resolve</c> verbs parameterized). A failed
    /// build / apply throws before this point, so no audit row for a
    /// refused action (the "no audit for a refused action" posture).
    /// </summary>
    private async Task EmitPortabilityAuditRowAsync(string action, string residentSubjectId, CancellationToken ct)
    {
        await using var auditSession = documentStore.OpenSession(new Marten.Services.SessionOptions());
        auditSession.Store(new Authorization.AccessAudit
        {
            Id = System.Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = residentSubjectId,
            EffectivePrincipalId = residentSubjectId,
            Action = action,
            TargetKind = "portability",
            TargetId = "portability",
            Via = Authorization.AccessVia.Owner,
            Outcome = Authorization.AccessOutcome.Allow,
        });
        await auditSession.SaveChangesAsync(ct).ConfigureAwait(false);
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
    /// C10-3b (IMPROVE-CODE) — the <b>orchestrator</b> over the per-phase
    /// strategy seams extracted from the pre-split monolith: the archive is
    /// read (a <see cref="InvalidDataException"/> is surfaced as a
    /// fail-closed plan, the SEC-lane U14 zip-bomb pin), the target pre-pass
    /// (<see cref="ClassifyTargetPrepassAsync"/> — the target rows + id sets
    /// + the principal / language code sets the per-entity leg resolves
    /// against), the SEC-lane H1 ownership-guard inputs
    /// (<see cref="DeriveOwnershipGuardInputs"/>), then every in-scope
    /// archive entity is classified (<see cref="ClassifyEntityRow"/> — the
    /// per-entity-kind leg: <c>Rejected</c> → <c>Duplicate</c> →
    /// <c>Clean</c>/<c>Conflict</c>). **Read-only** (C-M27·5): a
    /// <c>QuerySession</c> only, no <c>session.Store</c> / <c>SaveChanges</c>.
    /// The public signature is the frozen C08/C09 seam; only the body
    /// changed.
    /// </summary>
    public async Task<UserPortabilityImportPlan> ClassifyAsync(
        string residentSubjectId, Stream archive, CancellationToken ct = default)
    {
        // ── JSON options (the M11 PortabilityValidate pattern) ────────────
        var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // ── Read the archive (the M11 KumunitaArchive reader) ─────────────
        // SEC-lane U14 (M2 / U13 carry-forward): a zip-bomb rejection (U13's
        // <see cref="InvalidDataException"/>) is surfaced as a user-facing
        // fail-closed plan, not a 500.
        KumunitaArchiveData data;
        try
        {
            data = await KumunitaArchive.ReadAsync(archive, ct).ConfigureAwait(false);
        }
        catch (InvalidDataException ex)
        {
            return new UserPortabilityImportPlan(false, [],
                [$"archive.invalid:{ex.Message}"]);
        }

        // ── Per-phase strategy legs (the per-entity-kind extraction) ──────
        var prepass = await ClassifyTargetPrepassAsync(data, jsonOpts, residentSubjectId, ct);
        var guard = DeriveOwnershipGuardInputs(prepass, data, jsonOpts, residentSubjectId);

        var nameToType = BuildNameToTypeMap();
        var entities = new List<UserPortabilityEntityClassification>();
        foreach (var (type, _) in UserScopeInventory.Entries)
        {
            ct.ThrowIfCancellationRequested();
            if (!data.Docs.TryGetValue(type, out var json))
                continue; // a type not in the archive contributes no entities

            var docType = nameToType[type];
            var listType = typeof(List<>).MakeGenericType(docType);
            if (JsonSerializer.Deserialize(json, listType, jsonOpts) is not System.Collections.IEnumerable archiveRows)
                continue;

            foreach (var archiveRow in archiveRows)
            {
                if (archiveRow is null)
                    continue;
                entities.Add(ClassifyEntityRow(
                    type, archiveRow, prepass, guard, residentSubjectId));
            }
        }

        // ── The no-write pin (C-M27·5) — no session.Store / SaveChanges ───
        // was called; the classification is read-only (the QuerySession +
        // the userManager.Users read are the only I/O, both read-only).
        return new UserPortabilityImportPlan(Ok: true, Entities: entities, Failures: []);
    }

    /// <summary>
    /// C10-3b (IMPROVE-CODE) — the <b>classify target pre-pass</b>
    /// (the M11 validate pre-pass, over the target store — read-only),
    /// extracted from <see cref="ClassifyAsync"/>: for every in-scope kind
    /// the target rows (the <c>duplicate</c> match set) + their id set (the
    /// <c>conflict</c> reference-resolution target) are read, plus the
    /// referenced out-of-scope doc types' id sets (e.g. <c>Announcement</c>
    /// — the <c>Bookmark.TargetId</c> multi-kind union), the target
    /// principal id set (the <c>→ principal</c> references always resolve
    /// for the resident — they are the anchor), and the <c>LanguageCode</c>
    /// set (the <c>→ LanguageCatalog</c> references). Bundled in the
    /// <see cref="ClassifyPrepass"/> shape the <see cref="ClassifyEntityRow"/>
    /// leg resolves against.
    /// </summary>
    private sealed record ClassifyPrepass(
        Dictionary<string, IReadOnlyList<object>> TargetRowsByType,
        Dictionary<string, HashSet<string>> TypeSetById,
        HashSet<string> PrincipalIds,
        HashSet<string> LanguageCodes);

    private async Task<ClassifyPrepass> ClassifyTargetPrepassAsync(
        KumunitaArchiveData data,
        JsonSerializerOptions jsonOpts,
        string residentSubjectId,
        CancellationToken ct)
    {
        await using var session = documentStore.QuerySession();
        var nameToType = BuildNameToTypeMap();

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

        return new ClassifyPrepass(targetRowsByType, typeSetById, principalIds, languageCodes);
    }

    /// <summary>
    /// C10-3b (IMPROVE-CODE) — the <b>SEC-lane H1 ownership-guard inputs</b>
    /// (the U4/U6 pure decision's derived inputs), extracted from
    /// <see cref="ClassifyAsync"/> and shared with <see cref="ResolveAsync"/>
    /// (the same two shapes, the exact contract in design doc §2.2): (a) the
    /// <c>existingOwnerOf</c> probe — per-type <c>row.Id → owner</c> map read
    /// off the type's first <see cref="UserScopeEntry.OwnershipFields"/> entry
    /// (the ownership anchor); a null result means "not owned by another" to
    /// the probe; (b) the actor's conversation-id set — the union of the
    /// target's <c>Conversation</c> rows the actor holds a seat in and the
    /// archive's own <c>Conversation</c> rows the actor holds a seat in (a
    /// <c>Message</c> whose conversation ships in the same archive is the
    /// actor's own). Fail-closed: an empty union rejects every
    /// <c>Message</c>.
    /// </summary>
    private sealed record OwnershipGuardInputs(
        Dictionary<string, Dictionary<string, string>> OwnerByIdByType,
        HashSet<string> ActorConversationIds);

    private OwnershipGuardInputs DeriveOwnershipGuardInputs(
        ClassifyPrepass prepass,
        KumunitaArchiveData data,
        JsonSerializerOptions jsonOpts,
        string residentSubjectId)
    {
        var ownerByIdByType = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var (t, entry) in UserScopeInventory.Entries)
        {
            var ownerField = entry.OwnershipFields.Count > 0 ? entry.OwnershipFields[0] : null;
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (ownerField is not null)
            {
                foreach (var row in prepass.TargetRowsByType.GetValueOrDefault(t) ?? [])
                {
                    if (ReadId(row) is string rowId && rowId.Length > 0
                        && (string?)ReadFieldValue(row.GetType(), ownerField, row) is { Length: > 0 } owner)
                        map[rowId] = owner;
                }
            }
            ownerByIdByType[t] = map;
        }

        var actorConversationIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in prepass.TargetRowsByType.GetValueOrDefault("Conversation") ?? [])
            if (IsActorConversation(row, residentSubjectId))
                if (ReadId(row) is string idT && idT.Length > 0)
                    actorConversationIds.Add(idT);
        if (data.Docs.TryGetValue("Conversation", out var convJson))
        {
            var convListType = typeof(List<>).MakeGenericType(typeof(Kumunita.Core.Messaging.Conversation));
            if (JsonSerializer.Deserialize(convJson, convListType, jsonOpts) is System.Collections.IEnumerable archiveConversations)
                foreach (var row in archiveConversations)
                    if (row is not null && IsActorConversation(row, residentSubjectId))
                        if (ReadId(row) is string idA && idA.Length > 0)
                            actorConversationIds.Add(idA);
        }

        return new OwnershipGuardInputs(ownerByIdByType, actorConversationIds);
    }


    /// <inheritdoc />
    /// <summary>
    /// C10-3b (IMPROVE-CODE) — the <b>orchestrator</b> over the per-phase
    /// strategy seams extracted from the pre-split monolith (the design doc
    /// §2.5 contract, verbatim). The public signature is the frozen C08/C09
    /// seam; only the body changed. Legs (in order): archive read (a
    /// <see cref="InvalidDataException"/> is surfaced as a fail-closed
    /// result, the SEC-lane U14 zip-bomb pin), the §2.5 (3) standing pre-pass
    /// (<see cref="ResolveStandingPrepassAsync"/>), the archive row read
    /// (<see cref="ResolveArchiveRowsAsync"/>), the per-entity apply loop
    /// (<see cref="ApplyPlanEntitiesAsync"/> — the M11 dependency-order
    /// uniform loop, one commit), the media leg (<see cref="ApplyMediaAsync"/>
    /// — the SEC-lane U14 size/quota/type gate), and the one
    /// <c>portability.import.resolve</c> <c>AccessAudit</c> row
    /// (<see cref="EmitPortabilityAuditRowAsync"/> — shared with the export
    /// lane).
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
        // SEC-lane U14 (M2 / U13 carry-forward): a zip-bomb rejection (U13's
        // <see cref="InvalidDataException"/>) is surfaced as a user-facing
        // fail-closed result, not a 500.
        KumunitaArchiveData data;
        try
        {
            data = await KumunitaArchive.ReadAsync(archive, ct).ConfigureAwait(false);
        }
        catch (InvalidDataException ex)
        {
            return new UserPortabilityImportResult(false, 0, 0,
                [$"archive.invalid:{ex.Message}"]);
        }

        var nameToType = BuildNameToTypeMap();

        // ── Per-phase strategy legs (the per-entity-kind extraction) ──────
        // (1) standing pre-pass (the §2.5 (3) fail-closed rejection) — the
        // transaction has not started yet, so no rows are written (C-M27·4).
        var standingRejection = await ResolveStandingPrepassAsync(resolutions, residentSubjectId, ct);
        if (standingRejection is not null)
            return new UserPortabilityImportResult(false, 0, 0, standingRejection);

        // (2) archive row read (the M11 PortabilityApplyDocuments pattern —
        // the docs/{Type}.json array bytes are the single source of truth
        // for the apply).
        var archiveRowsByType = ResolveArchiveRowsAsync(data, nameToType, plan);
        var resolutionByEntity = BuildResolutionLookup(resolutions);

        // (3) per-entity apply (the M11 dependency order, one commit) — the
        // SEC-lane H1 (U6) ownership-guard inputs are derived read-only,
        // and the guard is re-run per <c>session.Store(row)</c> (defense in
        // depth: a plan tampered between classify and apply, or a forged
        // resolve form, cannot write a foreign row — invariant S-1).
        var apply = await ApplyPlanEntitiesAsync(
            data, nameToType, plan, archiveRowsByType, resolutionByEntity,
            residentSubjectId, ct);
        if (apply.Ok is false)
            return new UserPortabilityImportResult(false, 0, 0, apply.Failures);

        var (appliedCount, discardedCount, rejectedReasons) = (
            apply.AppliedCount, apply.DiscardedCount, apply.RejectedReasons);

        // (4) media leg (the SEC-lane U14 size/quota/type gate) — a skipped
        // media appends a <c>media.skipped:<reason></c> entry; the
        // import stays Ok (a partial skip is not a fail-closed rollback).
        await ApplyMediaAsync(data, residentSubjectId, rejectedReasons, ct);

        // (5) the one AccessAudit row (shared leg with the export lane).
        await EmitPortabilityAuditRowAsync("portability.import.resolve", residentSubjectId, ct);

        // SEC-lane H1 (U6): the resolve-time guard's rejections are surfaced
        // in the result's message list — one <c>import.rejected:<reason></c>
        // entry per rejected row, so the rejection is counted and the reason
        // is recorded (the guard's verbatim code). The import is still
        // <c>Ok</c> = <c>true</c> (the actor's genuine rows imported; the
        // hostile rows were skipped, not written) — a partial rejection is
        // not a fail-closed rollback (the C-M27·4 rollback is for a
        // <em>mid-apply exception</em>, not for a guard rejection).
        return new UserPortabilityImportResult(true, appliedCount, discardedCount, rejectedReasons);
    }

    /// <summary>
    /// C10-3b (IMPROVE-CODE) — the <b>§2.5 (3) standing pre-pass</b>
    /// (the fail-closed rejection), extracted from
    /// <see cref="ResolveAsync"/>: for every <c>AddElsewhere</c> resolution
    /// the resident must have standing over <c>PickedTargetId</c> (a group
    /// they are a member of, a tag they own, a component they are a member
    /// of, a page they authored — the <see cref="HasStandingAsync"/> per-kind
    /// leg). A <c>PickedTargetId</c> the resident has no standing over is a
    /// fail-closed rejection (the entity is not applied; the failure is
    /// recorded in <c>UserPortabilityImportResult.Failures</c>). Returns
    /// <c>null</c> when every standing check passed (the apply may proceed),
    /// or the closed <c>["standing.denied:..."]</c> failure list on the
    /// first rejection.
    /// </summary>
    private async Task<IReadOnlyList<string>?> ResolveStandingPrepassAsync(
        IReadOnlyList<UserPortabilityEntityResolution> resolutions,
        string residentSubjectId,
        CancellationToken ct)
    {
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
                return [$"standing.denied:{res.Kind}:{res.EntityId}:{res.AbsentRefKind}:{res.PickedTargetId}"];
            }
        }
        return null;
    }

    /// <summary>
    /// C10-3b (IMPROVE-CODE) — the <b>archive row read leg</b> (the M11
    /// <see cref="PortabilityApplyDocuments"/> pattern), extracted from
    /// <see cref="ResolveAsync"/>: for every distinct kind in the plan the
    /// archive's <c>docs/{Type}.json</c> bytes are deserialized into their
    /// declared POCO row set (the <c>Kind</c> is the strategy-table key —
    /// the M11 "string key" convention). A kind not in the archive
    /// contributes no rows (the fail-closed default).
    /// </summary>
    private static Dictionary<string, List<object>> ResolveArchiveRowsAsync(
        KumunitaArchiveData data,
        Dictionary<string, Type> nameToType,
        UserPortabilityImportPlan plan)
    {
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
        return archiveRowsByType;
    }

    /// <summary>
    /// C10-3b (IMPROVE-CODE) — the <b>per-entity resolution lookup</b>
    /// (the M11 "string key" convention), extracted from
    /// <see cref="ResolveAsync"/>: the resident's per-entity decisions are
    /// keyed by <c>"Kind|EntityId"</c> so the apply loop can resolve them
    /// without a tuple comparer.
    /// </summary>
    private static Dictionary<string, UserPortabilityEntityResolution> BuildResolutionLookup(
        IReadOnlyList<UserPortabilityEntityResolution> resolutions)
    {
        var resolutionByEntity = new Dictionary<string, UserPortabilityEntityResolution>(
            StringComparer.Ordinal);
        foreach (var r in resolutions)
            resolutionByEntity[$"{r.Kind}|{r.EntityId}"] = r;
        return resolutionByEntity;
    }

    /// <summary>
    /// C10-3b (IMPROVE-CODE) — the per-entity apply leg (§2.5 (1)–(3), the
    /// per-entity-kind strategy loop). Docs apply in M11 dependency order;
    /// the SEC-lane H1 (U6) ownership guard re-runs per
    /// <c>session.Store(row)</c> (defense in depth — invariant S-1). One
    /// commit (C-M27·4). Returns the apply outcome (a mid-apply exception
    /// is the documented rollback path — <c>Ok = false</c> + the closed
    /// failure set; a guard rejection appends an
    /// <c>import.rejected:&lt;reason&gt;</c> entry but is not a fail-closed
    /// rollback — the C-M27·4 rollback is for a mid-apply exception, not a
    /// guard rejection).
    /// </summary>
    private sealed record ApplyOutcome(
        bool Ok,
        int AppliedCount,
        int DiscardedCount,
        List<string> RejectedReasons,
        IReadOnlyList<string> Failures);

    /// <summary>
    /// The per-row guard decision (the U4 pure decision) — the delegate the
    /// <see cref="ApplyTypeRows"/> leg calls per <c>session.Store(row)</c>
    /// (the <c>out rejectionReason</c> carries the guard's verbatim reason
    /// code for the <c>import.rejected:&lt;reason&gt;</c> tally).
    /// </summary>
    private delegate bool GuardRow(string type, object row, out string? rejectionReason);

    private async Task<ApplyOutcome> ApplyPlanEntitiesAsync(
        KumunitaArchiveData data,
        Dictionary<string, Type> nameToType,
        UserPortabilityImportPlan plan,
        Dictionary<string, List<object>> archiveRowsByType,
        Dictionary<string, UserPortabilityEntityResolution> resolutionByEntity,
        string residentSubjectId,
        CancellationToken ct)
    {
        var guard = await ResolveGuardInputsAsync(data, nameToType, residentSubjectId, ct);
        bool TryImportRow(string type, object row, out string? rejectionReason)
        {
            var ownerProbe = guard.OwnerByIdByType.GetValueOrDefault(type);
            var verdict = ImportOwnershipGuard.Check(
                type, row, residentSubjectId,
                id => ownerProbe is not null ? ownerProbe.GetValueOrDefault(id) : null,
                type == "Message" ? guard.ActorConversationIds : null);
            rejectionReason = verdict.RejectionReason;
            return verdict.Allowed;
        }

        var appliedCount = 0;
        var discardedCount = 0;
        var rejectedReasons = new List<string>();
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
                var (a, d, r) = ApplyTypeRows(
                    entry.Type, rows, plan, resolutionByEntity, rejectedReasons,
                    session, TryImportRow);
                appliedCount += a;
                discardedCount += d;
            }

            // One commit (the C-M27·4 "one commit" pin) — every doc type's rows
            // are staged in a single session; one SaveChangesAsync commits them
            // atomically (a mid-apply failure is the documented rollback path).
            await session.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // C-M27·4 fail-closed pin: a mid-apply failure is the documented
            // rollback path (the transaction rolls back, no rows are written).
            return new ApplyOutcome(false, 0, 0, [], Failures: [$"apply.failed:{ex.Message}"]);
        }
        return new ApplyOutcome(true, appliedCount, discardedCount, rejectedReasons, []);
    }

    /// <summary>
    /// C10-3b (IMPROVE-CODE) — the per-type row apply leg (the inner loop of
    /// <see cref="ApplyPlanEntitiesAsync"/>, extracted to keep it under the
    /// 80-line / CC-15 ceiling): for every archive row in the type's set, the
    /// plan's classification drives the §2.5 (1)–(3) decision —
    /// <c>Clean</c> → apply (after the per-row guard), <c>Duplicate</c> →
    /// discard, <c>Conflict</c> → the resident's decision (the no-auto-merge
    /// pin C-M27·4: an unresolved conflict is not applied). Returns the
    /// per-type (applied, discarded, rejected) tallies; the guard rejections
    /// are appended to the shared <paramref name="rejectedReasons"/> list.
    /// </summary>
    private static (int Applied, int Discarded, int Rejected) ApplyTypeRows(
        string type,
        List<object> rows,
        UserPortabilityImportPlan plan,
        Dictionary<string, UserPortabilityEntityResolution> resolutionByEntity,
        List<string> rejectedReasons,
        Marten.IDocumentSession session,
        GuardRow tryImportRow)
    {
        var applied = 0;
        var discarded = 0;
        var rejected = 0;
        foreach (var row in rows)
        {
            if (row is null)
                continue;
            var entityId = ReadId(row) ?? "";
            var cls = plan.Entities
                .FirstOrDefault(e => e.Kind == type && e.EntityId == entityId);
            if (cls is null)
                continue; // not in the plan (shouldn't happen)

            // (1) clean → apply (the M11 import order, the §2.5 (1) rule).
            if (cls.Status == UserPortabilityEntityStatus.Clean)
            {
                // SEC-lane H1 (U6): the resolve-time ownership guard (defense
                // in depth — the plan's <c>Clean</c> mark was computed at
                // classify; a plan tampered with in between, or a forged
                // resolve form, must not be trusted). A rejected row is
                // <em>skipped</em> (never stored, S-1) and recorded as
                // <c>import.rejected:&lt;reason&gt;</c>.
                if (!tryImportRow(type, row, out string? cleanReason))
                {
                    rejectedReasons.Add($"import.rejected:{cleanReason}");
                    rejected++;
                    continue;
                }
                session.Store(row);
                applied++;
                continue;
            }

            // (2) duplicate → not applied (the §2.5 (2) rule).
            if (cls.Status == UserPortabilityEntityStatus.Duplicate)
            {
                discarded++;
                continue;
            }

            // (3) conflict → the resident's decision.
            if (!resolutionByEntity.TryGetValue($"{type}|{entityId}", out var res))
            {
                // C-M27·4 no-auto-merge pin: a conflict entity the resident
                // did not resolve is not applied (no default, no fallback,
                // no auto-re-point).
                continue;
            }

            if (res.Resolution == UserPortabilityResolutionKind.Discard)
            {
                // §2.5 (3) Discard rule: not applied (no write).
                discarded++;
                continue;
            }

            if (res.Resolution == UserPortabilityResolutionKind.AddElsewhere)
            {
                // §2.5 (3) AddElsewhere rule: apply with the absent reference
                // re-pointed to PickedTargetId (the target the resident chose).
                RepointAbsentReference(row, res);
                // SEC-lane H1 (U6): the resolve-time ownership guard — same
                // defense-in-depth re-check as the <c>Clean</c> site: a
                // conflict row that is hostile for the actor must not be
                // written, even if the resident resolved it (S-1).
                if (!tryImportRow(type, row, out string? addElseReason))
                {
                    rejectedReasons.Add($"import.rejected:{addElseReason}");
                    rejected++;
                    continue;
                }
                session.Store(row);
                applied++;
            }
        }
        return (applied, discarded, rejected);
    }

    /// <summary>
    /// C10-3b (IMPROVE-CODE) — the AddElsewhere re-point leg (the §2.5 (3)
    /// rule), extracted from the per-type apply loop: the absent reference
    /// field is re-pointed to <c>PickedTargetId</c> (the target the resident
    /// chose). A missing / non-writable property is a no-op (the fail-closed
    /// default — the row is stored as-is, the M11 re-point pattern).
    /// </summary>
    private static void RepointAbsentReference(
        object row, UserPortabilityEntityResolution res)
    {
        if (res.AbsentRefField is null || res.PickedTargetId is null)
            return;
        var prop = row.GetType().GetProperty(
            res.AbsentRefField, BindingFlags.Public | BindingFlags.Instance);
        if (prop is not null && prop.CanWrite)
            prop.SetValue(row, res.PickedTargetId);
    }

    /// <summary>
    /// C10-3b (IMPROVE-CODE) — the <b>resolve-time ownership-guard inputs</b>
    /// leg (the same two shapes U5 derives in the classify loop — the
    /// <see cref="OwnershipGuardInputs"/> shared shape), extracted from
    /// <see cref="ResolveAsync"/>: (a) the <c>existingOwnerOf</c> probe —
    /// per-type <c>row.Id → owner</c> map read off the type's first
    /// <see cref="UserScopeEntry.OwnershipFields"/> entry (the ownership
    /// anchor); a null result means "not owned by another" to the probe; (b)
    /// the actor's conversation-id set — the union of the target's
    /// <c>Conversation</c> rows the actor holds a seat in and the archive's
    /// own <c>Conversation</c> rows the actor holds a seat in (a
    /// <c>Message</c> whose conversation ships in the same archive is the
    /// actor's own). Fail-closed: an empty union rejects every
    /// <c>Message</c>. The resolve-time guard re-runs
    /// <see cref="ImportOwnershipGuard.Check"/> immediately before each
    /// <c>session.Store(row)</c> so a plan tampered with between classify
    /// and apply (or a forged resolve form) cannot write a foreign row
    /// (invariant S-1).
    /// </summary>
    private async Task<OwnershipGuardInputs> ResolveGuardInputsAsync(
        KumunitaArchiveData data,
        Dictionary<string, Type> nameToType,
        string residentSubjectId,
        CancellationToken ct)
    {
        var ownerByIdByType = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var (t, entry) in UserScopeInventory.Entries)
        {
            var ownerField = entry.OwnershipFields.Count > 0 ? entry.OwnershipFields[0] : null;
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (ownerField is not null)
            {
                var targetRows = await QueryAllRowsAsync(
                    documentStore.QuerySession(), nameToType[t], ct).ConfigureAwait(false);
                foreach (var row in targetRows)
                {
                    if (ReadId(row) is string rowId && rowId.Length > 0
                        && (string?)ReadFieldValue(row.GetType(), ownerField, row) is { Length: > 0 } owner)
                        map[rowId] = owner;
                }
            }
            ownerByIdByType[t] = map;
        }

        var actorConversationIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in await QueryAllRowsAsync(
                documentStore.QuerySession(), nameToType["Conversation"], ct).ConfigureAwait(false))
            if (IsActorConversation(row, residentSubjectId))
                if (ReadId(row) is string idT && idT.Length > 0)
                    actorConversationIds.Add(idT);
        if (data.Docs.TryGetValue("Conversation", out var convJsonGuard))
        {
            var convListType = typeof(List<>).MakeGenericType(nameToType["Conversation"]);
            var guardJsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            if (JsonSerializer.Deserialize(convJsonGuard, convListType, guardJsonOpts)
                    is System.Collections.IEnumerable archiveConversations)
                foreach (var row in archiveConversations)
                    if (row is not null && IsActorConversation(row, residentSubjectId))
                        if (ReadId(row) is string idA && idA.Length > 0)
                            actorConversationIds.Add(idA);
        }

        return new OwnershipGuardInputs(ownerByIdByType, actorConversationIds);
    }

    /// <summary>
    /// C10-3b (IMPROVE-CODE) — the <b>media leg</b> (the M11
    /// <see cref="PortabilityApplyMedia"/> pattern), extracted from
    /// <see cref="ResolveAsync"/>: for each media byte in the archive,
    /// <see cref="IMediaStore.PutAsync"/> (idempotent — the C-MED·4 dedup:
    /// identical bytes → same id → no second volume file). SEC-lane U14
    /// (M2 / S-5): each imported media runs the <b>same</b> size/quota/type
    /// decision as an upload (the <see cref="StorageLimits.Decide"/> pure
    /// decision the Web <c>IUploadGate</c> composes + the C-SM·7 usage read
    /// + the platform-space read + the <see cref="MediaOptions"/> allowlist
    /// — all registered by <c>AddKumunitaCore</c>; Core stays HTTP-free,
    /// ADR 0006-D). A media that fails the gate is <b>skipped</b> (no byte
    /// written) and recorded as <c>media.skipped:<reason></c> in the
    /// result's message list (the U6 <c>import.rejected:</c> shape) — the
    /// import stays <c>Ok</c> (a partial skip is not a fail-closed rollback;
    /// the actor's genuine rows + allowed media still import). When the
    /// policy seams are absent (a unit boot that constructs the service
    /// without the Core DI registrations) the legacy no-gate behaviour
    /// applies — the production registration (Core
    /// <c>DependencyInjection.cs</c>) always resolves them.
    /// </summary>
    private async Task ApplyMediaAsync(
        KumunitaArchiveData data,
        string residentSubjectId,
        List<string> rejectedReasons,
        CancellationToken ct)
    {
        var mediaGateEnabled = storageSettings is not null && mediaOpts is not null;
        CommunityStorageSettings? mediaSettings = null;
        long mediaUsage = 0;
        if (mediaGateEnabled)
        {
            mediaSettings = await storageSettings.GetOrCreateAsync(ct).ConfigureAwait(false);
            mediaUsage = await storageSettings
                .GetPerUserUsageBytesAsync(residentSubjectId, ct).ConfigureAwait(false);
        }

        var mediaManifestById = data.Manifest?.MediaManifest is { Count: > 0 } entries
            ? entries
                .Where(e => e is not null && !string.IsNullOrEmpty(e.Id))
                .GroupBy(e => e.Id, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal)
            : null;

        long mediaImportedBytes = 0;
        foreach (var (mediaId, bytes) in data.Media)
        {
            ct.ThrowIfCancellationRequested();

            if (mediaGateEnabled)
            {
                // The same decision order the Web IUploadGate runs
                // (platform-space first, then the pure size/quota
                // decision — the C-UP·2 size-first ordering) + the
                // allowlist check every upload lane runs. The resident's
                // export ships image/attachment/document-lane media, so
                // the union of the three allowlists is the allowed set.
                if (storageMetrics is not null
                    && (await storageMetrics.GetPlatformSpaceAsync(
                        mediaOpts.Value.MaxPlatformBytes, ct).ConfigureAwait(false)).IsFull)
                {
                    rejectedReasons.Add("media.skipped:platform-full");
                    continue;
                }

                var declaredType = mediaManifestById is not null
                    ? mediaManifestById.GetValueOrDefault(mediaId)?.ContentType
                    : null;
                if (string.IsNullOrWhiteSpace(declaredType)
                    || !(mediaOpts.Value.IsAllowed(declaredType)
                        || mediaOpts.Value.IsAttachmentAllowed(declaredType)
                        || mediaOpts.Value.IsDocumentAllowed(declaredType)))
                {
                    // A media with no manifest entry (hence no declared
                    // type) is fail-closed — it never round-tripped from
                    // a genuine export (the manifest field set is
                    // locked, C-M11·3).
                    rejectedReasons.Add("media.skipped:disallowed-type");
                    continue;
                }

                var decision = StorageLimits.Decide(
                    bytes.LongLength,
                    mediaUsage + mediaImportedBytes,
                    mediaSettings,
                    mediaOpts.Value.MaxBytes);
                if (decision is StorageDecision.Oversize)
                {
                    rejectedReasons.Add("media.skipped:oversize");
                    continue;
                }
                if (decision is StorageDecision.OverQuota)
                {
                    rejectedReasons.Add("media.skipped:over-quota");
                    continue;
                }

                mediaImportedBytes += bytes.LongLength;
            }

            await mediaStore.PutAsync(bytes, null, "application/octet-stream",
                residentSubjectId, ct).ConfigureAwait(false);
        }
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

