using Marten;

namespace Kumunita.Core.Media;

/// <summary>
/// The F15 (UPL-04) byte-store lifecycle/retention sweep. A
/// <b>platform constant</b>, not a config knob (the
/// <see cref="Kumunita.Core.Usage.UsagePurgeService"/> /
/// <see cref="Kumunita.Core.Usage.StorageMetricsCaptureService"/> precedent:
/// the retention has no per-instance reason to vary — the operator who wants
/// a different value edits the constant and redeploys; the per-instance knob is
/// a named deferral). A <b>Wolverine-free</b>, <b>HTTP-free</b> static service
/// (ADR 0006-D: Core stays HTTP-free — no <c>IHttpContextAccessor</c>, no Web
/// types); the Web handler is a thin adapter that injects a live
/// <see cref="IDocumentStore"/> and <see cref="IMediaFileStore"/>.
/// </summary>
public static class MediaLifecycleService
{
    /// <summary>
    /// The F15 (UPL-04) media-byte retention: 365 days (a platform constant,
    /// matching <see cref="UsagePurgeService.RetentionDays"/> and
    /// <see cref="StorageMetricsCaptureService.RetentionDays"/> by convention).
    /// </summary>
    public const int RetentionDays = 365;

    /// <summary>
    /// Deletes <see cref="MediaObject"/> catalog rows older than
    /// <see cref="RetentionDays"/> days from <paramref name="now"/> (the
    /// <see cref="MediaObject.Created"/> time-based sweep — <b>not</b>
    /// owner-scoped), in one session (batched id-collection + delete, the
    /// <see cref="UsagePurgeService"/> shape), and — <b>after</b> the commit —
    /// deletes each row's content-addressed byte on the volume via the
    /// idempotent <see cref="IMediaFileStore.DeleteIfExistsAsync"/> seam (the
    /// F1 byte-delete seam, re-used as-is). Byte deletes are post-commit
    /// filesystem ops, not in the session; a single byte-delete failure is
    /// logged and swallowed, never rethrown (the C-MED·7 orphan-safe
    /// invariant). No advisory lock — idempotency is the natural no-op (a
    /// double-run selects the already-gone set) + the
    /// <see cref="IMediaFileStore.DeleteIfExistsAsync"/> no-op-on-missing.
    /// </summary>
    /// <param name="store">The shared document store (the host injects the same <c>IDocumentStore</c> the domain services use).</param>
    /// <param name="media">The byte store; <see cref="IMediaFileStore.DeleteIfExistsAsync"/> is the idempotent byte-delete seam (re-used, not reshaped).</param>
    /// <param name="now">Injection point for tests (pin "now" so the 365-day boundary is deterministic).</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The number of <see cref="MediaObject"/> catalog rows deleted.</returns>
    public static async Task<int> SweepAsync(
        IDocumentStore store,
        IMediaFileStore media,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(media);
        var cutoff = now.AddDays(-RetentionDays);

        List<string> expired;
        await using (var session = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            // Batched id-collection + delete in ONE session (the
            // UsagePurgeService / StorageMetricsCaptureService house shape):
            // one SaveChangesAsync for the whole batch, never a per-row
            // round-trip. C3 single-write-lane, ADR 0006.
            expired = (await session.Query<MediaObject>()
                                  .Where(o => o.Created < cutoff)
                                  .Select(o => o.Id)
                                  .ToListAsync(ct))
                          .Distinct().ToList();
            foreach (var id in expired) session.Delete<MediaObject>(id);
            await session.SaveChangesAsync(ct);   // commit the catalog deletes FIRST
        }

        // Post-commit byte deletes (filesystem ops, not in the session — the
        // F1.U4 "post-commit" ordering). DeleteIfExistsAsync is idempotent
        // (missing file is a no-op, never an error), so a double-run never
        // faults. A single unexpected failure is logged + swallowed (a
        // reclaimable orphan), never rethrown (the C-MED·7 invariant).
        foreach (var id in expired)
        {
            try
            {
                await media.DeleteIfExistsAsync(id, ct);
            }
            catch
            {
                // Log + continue — never let a byte-delete failure abort the
                // sweep; a failed byte-delete leaves a reclaimable orphan.
            }
        }

        return expired.Count;
    }
}
