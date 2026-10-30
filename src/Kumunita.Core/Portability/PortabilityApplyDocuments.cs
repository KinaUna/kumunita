using System.Text.Json;
using Marten;

namespace Kumunita.Core.Portability;

/// <summary>
/// The §apply step 2 — store the domain documents in the registry's
/// <b>import order</b> (parents before children, the D7 order) as
/// <b>one commit</b> (a single <c>SaveChangesAsync</c> — the C-M11·4
/// "one commit" pin). The loop is uniform (the
/// <see cref="PortabilityExportDocuments.NameToType"/> table lookup +
/// <c>List&lt;T&gt;</c> deserialization + <c>session.Store(row)</c> —
/// <em>not</em> per-type code).
/// <para>
/// The input is the <see cref="KumunitaArchiveData.Docs"/> map (the
/// <c>docs/{Type}.json</c> array bytes, the archive's single source of
/// truth). A clean validate guarantees the deserialization succeeds
/// (check (b)) — this unit's apply is fail-open on a pre-validated
/// archive (a mid-apply failure is the documented restore path,
/// C-M11·4, never a silently-accepted half-import).
/// </para>
/// <para>
/// The non-generic <c>session.Store(object)</c> call is the same seam
/// U04's <see cref="PortabilityService.ExportAsync"/> uses for the
/// <c>AccessAudit</c> row — no reflection needed.
/// </para>
/// </summary>
public static class PortabilityApplyDocuments
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Stores every content doc in the closed inventory in the
    /// <see cref="PortabilityDocTypes.InOrder()"/> import order (parents
    /// before children — the D7 order) as one commit (one
    /// <c>SaveChangesAsync</c>). The loop is uniform (the
    /// <see cref="PortabilityExportDocuments.NameToType"/> table lookup
    /// + reflection-dispatched <c>List&lt;T&gt;</c> deserialization +
    /// <c>ISession.Store&lt;T&gt;</c> — <em>not</em> per-type code).
    /// </summary>
    /// <param name="documentStore">The frozen Marten seam.</param>
    /// <param name="data">The <see cref="KumunitaArchiveData"/> — the
    /// <see cref="KumunitaArchiveData.Docs"/> map (doc type name →
    /// <c>docs/{Type}.json</c> array bytes) is the single source of
    /// truth for the apply.</param>
    /// <param name="mode">The import mode (ADR 0193).
    /// <see cref="PortabilityImportMode.CleanSlate"/> — the D1 restore
    /// semantic: every archived row is stored (the fresh instance has no
    /// conflicting rows — a principal collision was refused before this
    /// unit ran, so the content graph's author references are safe).
    /// <see cref="PortabilityImportMode.KeepExisting"/> — merge mode: a
    /// row whose id already exists in the target store is <b>skipped</b>
    /// (the existing content is kept intact — never overwritten); only
    /// new rows are added.</param>
    /// <param name="ct">Cancellation.</param>
    public static async Task ApplyAsync(
        Marten.IDocumentStore documentStore,
        KumunitaArchiveData data,
        PortabilityImportMode mode,
        CancellationToken ct)
    {
        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());

        foreach (var entry in PortabilityDocTypes.InOrder())
        {
            ct.ThrowIfCancellationRequested();

            if (!data.Docs.TryGetValue(entry.Type, out var json))
                continue; // a clean validate guaranteed presence (check
                          // (b)); a missing type is a caller error — skip
                          // (the archive was pre-validated; the apply is
                          // fail-open on the pre-validated shape).

            var docType = PortabilityExportDocuments.NameToType[entry.Type];
            var listType = typeof(List<>).MakeGenericType(docType);
            var rows = (System.Collections.IList)JsonSerializer.Deserialize(json, listType, JsonOpts)!;

            // KeepExisting (ADR 0193) — the existing-row skip, keyed on the
            // type's <b>natural identity</b>, NOT just the surrogate id.
            // A document's unique business-key index (e.g. TranslationResource
            // (Key, LanguageCode), GroupMembership (GroupId, UserId), the
            // *Translation (ParentId, LanguageCode) rows) is the real
            // identity, and it is the DB-enforced uniqueness. The archive's
            // row and the target's row for the same logical entity carry
            // <b>different</b> surrogate ids (two independent instances), so an
            // id-only skip misses them and the Store collides on the unique
            // index (23505). The skip key is therefore the union of the
            // surrogate id + every unique index's member set, derived from the
            // Marten mapping (the single source of truth for the index) so the
            // skip key always matches the actual constraint.
            HashSet<string>? existingSkipKeys = null;
            List<string[]>? keySets = null;
            if (mode == PortabilityImportMode.KeepExisting)
            {
                keySets = GetIdentityKeySets(documentStore, docType);
                existingSkipKeys = new HashSet<string>(StringComparer.Ordinal);
                var queryAll = typeof(PortabilityApplyDocuments)
                    .GetMethod(nameof(QueryAllAsync),
                        System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(docType);
                var existing = (System.Collections.IEnumerable)
                    (await (Task<object>)queryAll.Invoke(null, [documentStore, (object)ct])!)!;
                foreach (var exRow in existing)
                    AddSkipKeys(exRow, docType.Name, keySets, existingSkipKeys);
            }

            foreach (var row in rows)
            {
                // KeepExisting — skip the row the target already holds (the
                // existing content is kept intact, never overwritten). The
                // check is on the natural identity (surrogate id + business-
                // key member set), not just the surrogate id.
                if (existingSkipKeys is not null && keySets is not null
                    && IsSkipped(row, docType.Name, keySets, existingSkipKeys))
                    continue;

                session.Store(row); // the non-generic Store(object) —
                                    // the same seam U04's ExportAsync
                                    // uses for the AccessAudit row (no
                                    // reflection needed).
            }
        }

        // One commit (the C-M11·4 "one commit" pin) — every doc type's
        // rows are staged in a single session; one SaveChangesAsync
        // commits them atomically (a mid-apply failure is the
        // documented restore path, never a silently-accepted
        // half-import).
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // ── The KeepExisting natural-identity skip helpers (ADR 0193) ────────────
    // A row's natural identity is its surrogate id PLUS the member set of
    // every unique index on its table (the DB-enforced business key). The
    // member names are read from the Marten mapping so the skip key is always
    // in sync with the actual unique constraint; a business-key index whose
    // columns are not POCO members (a stored / duplicated column) is skipped
    // for the skip key and the surrogate id remains the guard (no worse than
    // the pre-fix behaviour).

    /// <summary>
    /// The type's identity key sets: the surrogate id first (<c>Id</c>, or
    /// <c>SubjectId</c> for the <c>Profile</c> shape), then one entry per
    /// unique index whose columns are all POCO members. Each entry is the
    /// closed member-name tuple that constitutes that identity.
    /// </summary>
    private static List<string[]> GetIdentityKeySets(
        Marten.IDocumentStore store, Type docType)
    {
        var sets = new List<string[]>();
        var idProp = docType.GetProperty("Id") ?? docType.GetProperty("SubjectId");
        if (idProp is not null)
            sets.Add(new[] { idProp.Name });

        var idType = store.Options.FindOrResolveDocumentType(docType);
        if (idType is null)
            return sets; // an unknown type has no business-key indexes —
                         // the surrogate-id key set above is the guard.
        var indexes = idType.Indexes;
        if (indexes is null)
            return sets;
        foreach (var index in indexes)
        {
            if (index is null || !index.IsUnique)
                continue;
            var columns = index.Columns;
            if (columns is null)
                continue;
            var members = new List<string>();
            var allMembers = true;
            foreach (var column in columns)
            {
                var member = ExtractMember(column);
                if (member is null) { allMembers = false; break; }
                members.Add(member);
            }
            if (allMembers && members.Count > 0)
                sets.Add(members.ToArray());
        }
        return sets;
    }

    /// <summary>
    /// Adds every computable identity signature of <paramref name="row"/>
    /// to <paramref name="sink"/>. A signature that cannot be built (a null
    /// member — nulls are distinct in a unique index, so never a duplicate;
    /// or a non-member column) is omitted, never a false positive.
    /// </summary>
    private static void AddSkipKeys(
        object row, string typeName, List<string[]> keySets, HashSet<string> sink)
    {
        for (var i = 0; i < keySets.Count; i++)
        {
            var signature = IdentitySignature(row.GetType(), keySets[i], row, typeName + "#" + i);
            if (signature is not null)
                sink.Add(signature);
        }
    }

    /// <summary>
    /// <c>true</c> iff <paramref name="row"/> shares any identity with an
    /// already-existing row (its surrogate id or a business-key member set
    /// matches) — i.e. the target already holds this logical entity.
    /// </summary>
    private static bool IsSkipped(
        object row, string typeName, List<string[]> keySets, HashSet<string> existing)
    {
        for (var i = 0; i < keySets.Count; i++)
        {
            var signature = IdentitySignature(row.GetType(), keySets[i], row, typeName + "#" + i);
            if (signature is not null && existing.Contains(signature))
                return true;
        }
        return false;
    }

    /// <summary>
    /// A canonical signature of <paramref name="row"/> over one key set
    /// (the type name + key-set ordinal + the member values). Returns
    /// <c>null</c> when any member is null (nulls are distinct in a unique
    /// index — not a duplicate) or when a member cannot be resolved.
    /// </summary>
    private static string? IdentitySignature(
        Type type, string[] members, object row, string prefix)
    {
        var sb = new System.Text.StringBuilder(prefix);
        sb.Append('\u0001');
        foreach (var member in members)
        {
            var value = type.GetProperty(member)?.GetValue(row);
            if (value is null)
                return null;
            sb.Append(value.ToString()).Append('\u0001');
        }
        return sb.ToString();
    }

    /// <summary>
    /// Extracts the POCO member name from a Marten index column. A POCO-member
    /// index column is rendered as <c>(data --&gt; 'MemberName')</c>; any other
    /// shape (a stored / duplicated column, an expression) is not reflectable
    /// here and returns <c>null</c>.
    /// </summary>
    private static string? ExtractMember(string? columnSql)
    {
        if (string.IsNullOrEmpty(columnSql))
            return null;
        const string prefix = "(data ->> '";
        if (columnSql.StartsWith(prefix, StringComparison.Ordinal)
            && columnSql.EndsWith("')", StringComparison.Ordinal))
            return columnSql.Substring(prefix.Length, columnSql.Length - prefix.Length - 2);
        return null;
    }

    /// <summary>
    /// The generic helper the KeepExisting keep-skip + the CleanSlate
    /// wipe (ADR 0193) drive off — loads <em>all</em> rows of a doc type
    /// from the target store. Invoked with <c>MakeGenericMethod(docType)</c>
    /// since the type is only known at runtime (the registry's data-driven
    /// loop).
    /// </summary>
    internal static async Task<object> QueryAllAsync<T>(
        Marten.IDocumentStore documentStore, CancellationToken ct)
        where T : notnull
    {
        await using var session = documentStore.QuerySession();
        return await session.Query<T>().ToListAsync(ct);
    }

    /// <summary>
    /// The CleanSlate wipe helper (ADR 0193) — loads <em>all</em> rows of
    /// a doc type, deletes them, and commits. Invoked with
    /// <c>MakeGenericMethod(docType)</c> since the type is only known at
    /// runtime (the registry's data-driven loop).
    /// </summary>
    internal static async Task DeleteAllAsync<T>(
        Marten.IDocumentStore documentStore, CancellationToken ct)
        where T : notnull
    {
        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        var rows = await session.Query<T>().ToListAsync(ct);
        foreach (var row in rows)
            session.Delete(row);
        if (rows.Count > 0)
            await session.SaveChangesAsync(ct);
    }
}
