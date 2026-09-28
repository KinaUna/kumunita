using Kumunita.Core.Media;

namespace Kumunita.Core.Portability;

/// <summary>
/// The §apply step 3 — copy the media bytes into the volume at the
/// <c>{Id[0..2]}/{Id}</c> content-addressed layout (the C-M11·3 pin —
/// the <b>same</b> layout as the local volume, so import is a byte-copy
/// and the dedup-by-content-hash is preserved; re-importing the same
/// file is a no-op at the byte layer).
/// <para>
/// The <c>MediaObject</c> catalog doc is <em>not</em> stored here — it
/// is one of the 44 content docs already stored by <see
/// cref="PortabilityApplyDocuments.ApplyAsync"/> (order 3 in the D7
/// registry). This unit's scope is the <b>bytes</b> only: for every
/// <c>MediaObject</c> listed in the archive's <see
/// cref="KumunitaArchiveData.Media"/> map, <see
/// cref="IMediaFileStore.PutAsync"/> the payload (idempotent — the
/// C-MED·4 dedup: identical bytes → same id → no second volume file).
/// </para>
/// <para>
/// Fail-closed at the source (the complement of U03's <see
/// cref="MediaExport"/> guards): a missing / mismatched byte is a
/// <b>validate-phase</b> failure (C-M11·3 / §validate (d)) — the
/// archive is rejected <em>before</em> this unit runs. This unit is
/// fail-open on a pre-validated archive (a mid-apply failure is the
/// documented restore path, C-M11·4, never a silently-accepted
/// half-import).
/// </para>
/// </summary>
public static class PortabilityApplyMedia
{
    /// <summary>
    /// Copies every media payload in <paramref name="data"/> into the
    /// volume at the <c>{Id[0..2]}/{Id}</c> content-addressed layout
    /// (the C-M11·3 pin — the <see cref="IMediaFileStore.PutAsync"/>
    /// seam applies the layout, the same as <see
    /// cref="KumunitaArchive.MediaEntryName"/>). Idempotent (the
    /// C-MED·4 dedup): a re-import of the same file is a no-op at the
    /// byte layer.
    /// </summary>
    /// <param name="mediaFileStore">The byte seam (C-MED·6) — the
    /// <c>{Id[0..2]}/{Id}</c> layout is applied by the store, not
    /// here.</param>
    /// <param name="data">The <see cref="KumunitaArchiveData"/> — the
    /// <see cref="KumunitaArchiveData.Media"/> map (content id → payload
    /// bytes) is the single source of truth for the apply.</param>
    /// <param name="ct">Cancellation.</param>
    public static async Task ApplyAsync(
        IMediaFileStore mediaFileStore, KumunitaArchiveData data, CancellationToken ct)
    {
        foreach (var (contentId, bytes) in data.Media)
        {
            ct.ThrowIfCancellationRequested();
            // The {Id[0..2]}/{Id} layout is applied by the store (the
            // C-MED·3/4/7 convention — the C-M11·3 pin: the <b>same</b>
            // content-addressed layout as the local volume, so import is
            // a byte-copy and the dedup-by-content-hash is preserved).
            // Idempotent (C-MED·4): identical bytes → same id → no
            // second volume file.
            await mediaFileStore.PutAsync(contentId, bytes, ct).ConfigureAwait(false);
        }
    }
}
