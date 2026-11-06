using System.Reflection;
using System.Text.Json;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Messaging;
using Kumunita.Core.UserInfo;
using Marten;
using Microsoft.AspNetCore.Identity;
using static Kumunita.Core.Portability.UserPortabilityService;

namespace Kumunita.Core.Portability;


/// <summary>
/// C3-03 (ADR 0293) — the classify <b>prepass</b> shape (the U13/M27
/// validate pre-pass over the target store — read-only), extracted out of
/// <see cref="UserPortabilityService"/> into a shared top-level record: it is
/// the seam's <c>ClassifyTargetPrepassAsync</c> result and the
/// <see cref="UserPortabilityService"/> classifier leg's input. Moved to a
/// top-level record (was a <c>private sealed record</c> nested in the
/// god-service) because it crosses the <see cref="ImportOwnershipGuardService"/>
/// seam boundary — the C08 shared-shape shape.
/// </summary>
public sealed record ClassifyPrepass(
    Dictionary<string, IReadOnlyList<object>> TargetRowsByType,
    Dictionary<string, HashSet<string>> TypeSetById,
    HashSet<string> PrincipalIds,
    HashSet<string> LanguageCodes);

/// <summary>
/// C3-03 (ADR 0293) — the <b>SEC-lane H1 ownership-guard inputs</b> (the U4/U6
/// pure decision's derived inputs), extracted out of <see
/// cref="UserPortabilityService"/> into a shared top-level record: (a) the
/// <c>existingOwnerOf</c> probe — per-type <c>row.Id → owner</c> map read off
/// the type's first <see cref="UserScopeEntry.OwnershipFields"/> entry (the
/// ownership anchor); a null result means "not owned by another" to the probe;
/// (b) the actor's conversation-id set — the union of the target's
/// <c>Conversation</c> rows the actor holds a seat in and the archive's own
/// <c>Conversation</c> rows the actor holds a seat in (a <c>Message</c> whose
/// conversation ships in the same archive is the actor's own). Fail-closed: an
/// empty union rejects every <c>Message</c>. Moved to a top-level record (was a
/// <c>private sealed record</c> nested in the god-service) because it crosses
/// the <see cref="ImportOwnershipGuardService"/> seam boundary.
/// </summary>
public sealed record OwnershipGuardInputs(
    Dictionary<string, Dictionary<string, string>> OwnerByIdByType,
    HashSet<string> ActorConversationIds);

/// <summary>
/// C3-03 (ADR 0293) — the <b>SEC-lane H1 ownership-guard</b> derivation surface
/// (the U4/U6 pure decision's inputs), extracted out of <see
/// cref="UserPortabilityService"/> as a real part-vs-whole seam (the C3-00
/// <c>EventRsvpGuardianService</c> pattern). The three guard methods are a
/// <b>named domain concept</b> — the classify/resolve ownership-guard pre-pass
/// (the <see cref="ClassifyPrepass"/> / <see cref="OwnershipGuardInputs"/>
/// shared shapes, design doc §2.2) — with their own target-store reads
/// (<c>QueryAllRowsAsync</c> over the in-scope + referenced doc types), the
/// target principal set (the <c>UserManager</c> users), the language set, and
/// the fail-closed conversation-seat union. A C13 cold read of
/// <see cref="ImportOwnershipGuardService"/> passes on its own.
/// </summary>
public interface IImportOwnershipGuardService
{
    /// <summary>Derive the classify <see cref="ClassifyPrepass"/> (the target
    /// rows + id sets, the target principal set, the language set) over the
    /// target store (read-only).</summary>
    Task<ClassifyPrepass> ClassifyTargetPrepassAsync(KumunitaArchiveData data, JsonSerializerOptions jsonOpts, string residentSubjectId, CancellationToken ct);

    /// <summary>Derive the <see cref="OwnershipGuardInputs"/> ownership-guard
    /// inputs over a classify prepass (the owner probe + the fail-closed
    /// conversation-seat union).</summary>
    OwnershipGuardInputs DeriveOwnershipGuardInputs(ClassifyPrepass prepass, KumunitaArchiveData data, JsonSerializerOptions jsonOpts, string residentSubjectId);

    /// <summary>Derive the <see cref="OwnershipGuardInputs"/> ownership-guard
    /// inputs over the target store (the resolve path — reads the owner rows
    /// + the conversation seat set directly).</summary>
    Task<OwnershipGuardInputs> ResolveGuardInputsAsync(KumunitaArchiveData data, Dictionary<string, Type> nameToType, string residentSubjectId, CancellationToken ct);
}

public sealed class ImportOwnershipGuardService : IImportOwnershipGuardService
{
    private readonly Marten.IDocumentStore documentStore;
    private readonly UserManager<User> userManager;

    public ImportOwnershipGuardService(Marten.IDocumentStore documentStore, UserManager<User> userManager)
    {
        this.documentStore = documentStore;
        this.userManager = userManager;
    }

    /// <summary>The C3-03 (ADR 0293) <see cref="ClassifyPrepass"/> derivation (verbatim from <see cref="UserPortabilityService"/>).</summary>
    public async Task<ClassifyPrepass> ClassifyTargetPrepassAsync(
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

    /// <summary>The C3-03 (ADR 0293) <see cref="OwnershipGuardInputs"/> derivation over a classify prepass (verbatim from <see cref="UserPortabilityService"/>).</summary>
    public OwnershipGuardInputs DeriveOwnershipGuardInputs(
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

    /// <summary>The C3-03 (ADR 0293) <see cref="OwnershipGuardInputs"/> derivation over the target store (verbatim from <see cref="UserPortabilityService"/>).</summary>
    public async Task<OwnershipGuardInputs> ResolveGuardInputsAsync(
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
}
