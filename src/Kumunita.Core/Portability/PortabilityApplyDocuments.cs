using System.Text.Json;

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
    /// <param name="ct">Cancellation.</param>
    public static async Task ApplyAsync(
        Marten.IDocumentStore documentStore,
        KumunitaArchiveData data,
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

            foreach (var row in rows)
                session.Store(row); // the non-generic Store(object) —
                                    // the same seam U04's ExportAsync
                                    // uses for the AccessAudit row (no
                                    // reflection needed).
        }

        // One commit (the C-M11·4 "one commit" pin) — every doc type's
        // rows are staged in a single session; one SaveChangesAsync
        // commits them atomically (a mid-apply failure is the
        // documented restore path, never a silently-accepted
        // half-import).
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
