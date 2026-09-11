using System.IO;

namespace Kumunita.Core.Media;

/// <summary>
/// The media module seam (C-MED·6 HTTP-free): content-addressed byte store +
/// catalog doc.
/// </summary>
public interface IMediaStore
{
    /// <summary>Store (dedup by content hash) + catalog doc; returns the stored <see cref="MediaObject"/>. Idempotent (C-MED·4).</summary>
    Task<MediaObject> PutAsync(byte[] content, string? filename, string contentType, string? actorId, CancellationToken ct = default);

    Task<MediaObject?> GetAsync(string id, CancellationToken ct = default);

    /// <summary>Open the payload for serving. Throws <see cref="KeyNotFoundException"/> if the doc is missing.</summary>
    Task<Stream> OpenReadAsync(string id, CancellationToken ct = default);

    /// <summary>Delete the payload + doc (orphan-safe; C-MED·7).</summary>
    Task RemoveAsync(string id, string actorId, CancellationToken ct = default);
}
