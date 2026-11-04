using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kumunita.Core.Portability;

/// <summary>
/// C10-3b (IMPROVE-CODE) — the <b>per-entity-kind strategy table</b> for the
/// classify (<see cref="UserPortabilityService.ClassifyAsync"/>) and resolve
/// (<see cref="UserPortabilityService.ResolveAsync"/>) lanes, extracted from the
/// pre-split monoliths as a <c>partial</c> companion to
/// <see cref="UserPortabilityService"/>. Every member here is a pure, stateless
/// static leg (no field access) over a single entity kind or reference — the
/// strategy-table the orchestrators iterate (design doc
/// <c>docs/design/m27-user-scoped-portability-design.md</c> §2.4–§2.5). Splitting
/// the legs out keeps the orchestrator file under its ratchet ceiling (the
/// <c>partial</c> split is the repo idiom, cf.
/// <c>Kumunita.Web/Security/RequestLanguage</c>) while the locked public seam
/// and the baseline-pinned legs stay in the primary file.
/// </summary>
public sealed partial class UserPortabilityService
{

    /// <summary>
    /// C10-3b (IMPROVE-CODE) — the <b>per-entity classify leg</b> (the §2.4
    /// contract row-by-row), extracted from <see cref="ClassifyAsync"/>'s
    /// inner loop: the SEC-lane H1 ownership guard runs first (a hostile row
    /// is classified <c>Rejected</c> and never reaches the importable set —
    /// S-1); then the U02 business-key <c>duplicate</c> match; then the
    /// M11 referential-integrity <c>conflict</c> loop over the target (an
    /// empty absent-reference set is <c>Clean</c>). The three M27 status
    /// members keep their order (the <see cref="UserPortabilityEntityStatus"/>
    /// closed set); <c>Rejected</c> carries the guard's verbatim
    /// <see cref="ImportOwnershipGuard"/> reason code.
    /// </summary>
    private static UserPortabilityEntityClassification ClassifyEntityRow(
        string type,
        object archiveRow,
        ClassifyPrepass prepass,
        OwnershipGuardInputs guard,
        string residentSubjectId)
    {
        var entityId = ReadId(archiveRow) ?? "";

        // (a) SEC-lane H1 (U5) — the ownership guard (the U4 pure decision)
        // runs BEFORE a row is marked importable: a hostile row (forged
        // ownership, an <c>Id</c> collision with a row another resident
        // owns, or an access-granting row) is classified <c>Rejected</c> and
        // never reaches the <c>Clean</c>/<c>Duplicate</c>/<c>Conflict</c> set
        // (S-1). The reason code is the user-visible <c>RejectedReason</c>
        // (U6 records it as <c>import.rejected:&lt;reason&gt;</c>).
        var ownerProbe = guard.OwnerByIdByType.GetValueOrDefault(type);
        var verdict = ImportOwnershipGuard.Check(
            type,
            archiveRow,
            residentSubjectId,
            id => ownerProbe is not null ? ownerProbe.GetValueOrDefault(id) : null,
            type == "Message" ? guard.ActorConversationIds : null);
        if (!verdict.Allowed)
        {
            return new UserPortabilityEntityClassification(
                Kind: type,
                EntityId: entityId,
                Status: UserPortabilityEntityStatus.Rejected,
                AbsentReferences: [],
                DuplicateId: null,
                RejectedReason: verdict.RejectionReason);
        }

        // (b) duplicate — the U02 business-key match.
        var targetRows = prepass.TargetRowsByType[type];
        var duplicate = targetRows
            .FirstOrDefault(t => t is not null && MatchBusinessKey(type, archiveRow, t));
        if (duplicate is not null)
        {
            return new UserPortabilityEntityClassification(
                Kind: type,
                EntityId: entityId,
                Status: UserPortabilityEntityStatus.Duplicate,
                AbsentReferences: [],
                DuplicateId: ReadId(duplicate));
        }

        // (c) conflict — the M11 referential-integrity loop over
        //     the target (the §2.4 conflict reason shape).
        var absent = ResolveAbsentReferences(
            type, archiveRow, prepass.TypeSetById, prepass.PrincipalIds, prepass.LanguageCodes);
        return new UserPortabilityEntityClassification(
            Kind: type,
            EntityId: entityId,
            Status: absent.Count > 0
                ? UserPortabilityEntityStatus.Conflict
                : UserPortabilityEntityStatus.Clean,
            AbsentReferences: absent,
            DuplicateId: null);
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

    /// <summary>Does <paramref name="conversation"/> carry the actor in a
    /// seat (<c>ParticipantA</c> or <c>ParticipantB</c>)? (The SEC-lane H1
    /// <c>Message</c> guard input derivation — the U4
    /// <see cref="ImportOwnershipGuard"/> <c>Conversation</c> rule.)</summary>
    private static bool IsActorConversation(object conversation, string actorSubjectId)
    {
        var t = conversation.GetType();
        var a = (string?)ReadFieldValue(t, "ParticipantA", conversation);
        var b = (string?)ReadFieldValue(t, "ParticipantB", conversation);
        return string.Equals(a, actorSubjectId, StringComparison.Ordinal)
            || string.Equals(b, actorSubjectId, StringComparison.Ordinal);
    }
}
