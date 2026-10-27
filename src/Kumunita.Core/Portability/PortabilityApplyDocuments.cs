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

            // KeepExisting (ADR 0193) — the existing-row skip. The id
            // property is the type's identity field (the U05 loop's
            // convention: <c>Id</c>, except <c>Profile</c> whose identity
            // is <c>SubjectId</c>). The existing rows are read via a
            // generic helper (the Marten 9 <c>IQuerySession.Query&lt;T&gt;()</c>
            // seam, invoked with <c>MakeGenericMethod</c> since the type
            // is only known at runtime), then the id set is extracted
            // via reflection (the same id-property convention the U05
            // <see cref="PortabilityValidate"/> integrity loop uses).
            HashSet<string>? existingIds = null;
            if (mode == PortabilityImportMode.KeepExisting)
            {
                var idProp = docType.GetProperty("Id") ?? docType.GetProperty("SubjectId");
                existingIds = new HashSet<string>(StringComparer.Ordinal);
                if (idProp is not null)
                {
                    var queryAll = typeof(PortabilityApplyDocuments)
                        .GetMethod(nameof(QueryAllAsync),
                            System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.Static)!
                        .MakeGenericMethod(docType);
                    var existing = (System.Collections.IEnumerable)
                        (await (Task<object>)queryAll.Invoke(null, [documentStore, (object)ct])!)!;
                    foreach (var exRow in existing)
                    {
                        var v = idProp.GetValue(exRow);
                        if (v is string s && !string.IsNullOrEmpty(s))
                            existingIds.Add(s);
                    }
                }
            }

            foreach (var row in rows)
            {
                // KeepExisting — skip the row the target already holds
                // (the existing content is kept intact, never overwritten).
                if (existingIds is not null)
                {
                    var idProp = row.GetType().GetProperty("Id") ?? row.GetType().GetProperty("SubjectId");
                    var idValue = idProp?.GetValue(row);
                    if (idValue is string s && !string.IsNullOrEmpty(s) && existingIds.Contains(s))
                        continue;
                }

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
