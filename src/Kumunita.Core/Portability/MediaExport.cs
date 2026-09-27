using System.IO;
using Kumunita.Core.Media;
using Marten;

namespace Kumunita.Core.Portability;

/// <summary>
/// The U03 media export (D6 / C-M11·3) — for every <c>MediaObject</c> in the
/// catalog (the <c>MediaObject</c> entry in the D7 registry, one of the
/// <c>docs/{Type}.json</c> rows), reads the payload bytes and accumulates:
/// <list type="bullet">
/// <item>the <c>media</c> dict (content id → payload bytes) — the
/// <c>media/</c> section the <see cref="KumunitaArchive"/> writer lays out
/// at <c>media/{Id[0..2]}/{Id}</c> (the <b>same</b> content-addressed layout
/// as the local volume — the C-M11·3 byte-identity pin), and</item>
/// <item>the <c>media_manifest</c> list — one
/// <see cref="PortabilityMediaEntry"/>
/// per object, the exact locked field set
/// <c>{ id, size_bytes, content_type }</c> (§manifest) read from the
/// catalog — the set U05's §validate (d) checks byte-for-byte.</item>
/// </list>
/// <para>
/// Fail-closed at the source: a catalog row whose payload is absent, or
/// whose <c>ContentType</c> is missing, throws
/// <see cref="InvalidOperationException"/> before any bytes are written —
/// the export never ships a catalog doc whose bytes it cannot carry
/// (the C-M11·3 "both present or rejected" pin, on the export side).
/// </para>
/// </summary>
public static class MediaExport
{
    /// <summary>
    /// Reads every catalog payload + builds the media-manifest list. The
    /// catalog is read directly from the Marten store (a single
    /// <c>session.Query&lt;MediaObject&gt;()</c> over a
    /// <c>LightweightSession</c> — the same seam the <c>IMediaStore</c>
    /// implementation uses), not derived from the
    /// <c>docs/MediaObject.json</c> bytes.
    /// </summary>
    /// <param name="documentStore">The Marten store (the catalog source).</param>
    /// <param name="mediaStore">
    /// The media seam — the payload source (<see cref="IMediaStore.OpenReadAsync"/>).
    /// </param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>
    /// The <c>media</c> dict (id → bytes) + the ordered
    /// <c>media_manifest</c> entries (manifest order = catalog order).
    /// </returns>
    public static async Task<(Dictionary<string, byte[]> Media, List<PortabilityMediaEntry> Manifest)> ExportAsync(
        IDocumentStore documentStore,
        IMediaStore mediaStore,
        CancellationToken ct)
    {
        await using var session = documentStore.LightweightSession();
        var catalog = await session.Query<MediaObject>().ToListAsync(ct);

        var media = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var manifest = new List<PortabilityMediaEntry>(catalog.Count);

        foreach (var obj in catalog)
        {
            // Fail-closed: a catalog row with missing metadata would silently
            // round-trip a null into the manifest — reject at the source.
            if (string.IsNullOrWhiteSpace(obj.ContentType))
                throw new InvalidOperationException(
                    $"M11 media export — MediaObject '{obj.Id}' has no content type; the manifest field set {{ id, size_bytes, content_type }} is closed (C-M11·3).");

            var stream = await mediaStore.OpenReadAsync(obj.Id, ct);
            using (stream)
            {
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, ct);
                var bytes = buffer.ToArray();

                if (bytes.LongLength != obj.SizeBytes)
                    throw new InvalidOperationException(
                        $"M11 media export — MediaObject '{obj.Id}' payload is {bytes.LongLength} bytes but the catalog records {obj.SizeBytes}; refusing to ship a mismatched size_bytes (C-M11·3).");

                media[obj.Id] = bytes;
                manifest.Add(new PortabilityMediaEntry
                {
                    Id = obj.Id,
                    SizeBytes = obj.SizeBytes,
                    ContentType = obj.ContentType,
                });
            }
        }

        return (media, manifest);
    }
}
