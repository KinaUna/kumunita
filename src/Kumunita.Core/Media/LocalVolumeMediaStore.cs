using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Marten;

namespace Kumunita.Core.Media;

/// <summary>
/// Composes the byte seam (<see cref="IMediaFileStore"/>) with the host
/// <see cref="IDocumentStore"/> (C-MED·6: store-first, doc-second, orphan-safe,
/// C-MED·7). The <c>Put</c> path is the dedup lane (C-MED·4): hash →
/// <c>LoadAsync</c> → return the existing doc if present, else write the volume
/// file (bytes first) and commit the catalog doc — a single
/// <c>SaveChangesAsync</c>.
/// </summary>
public sealed class LocalVolumeMediaStore : IMediaStore
{
    private readonly IMediaFileStore _files;
    private readonly IDocumentStore _store;

    public LocalVolumeMediaStore(IMediaFileStore files, IDocumentStore store)
    {
        _files = files;
        _store = store;
    }

    public async Task<MediaObject> PutAsync(byte[] content, string? filename, string contentType, string? actorId, CancellationToken ct = default)
    {
        var id = Sha256Hex(content);
        await using var session = _store.LightweightSession();
        var existing = await session.LoadAsync<MediaObject>(id, ct).ConfigureAwait(false);
        if (existing is not null)
            return existing; // dedup: identical bytes → same id → no second volume file (C-MED·4)

        var doc = new MediaObject
        {
            Id = id,
            Filename = filename,
            ContentType = contentType,
            SizeBytes = content.Length,
            Created = DateTimeOffset.Now,
            CreatedById = actorId
        };
        await _files.PutAsync(id, content, ct).ConfigureAwait(false); // bytes first (orphan-safe, C-MED·7)
        session.Store(doc);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return doc;
    }

    public async Task<MediaObject?> GetAsync(string id, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        return await session.LoadAsync<MediaObject>(id, ct).ConfigureAwait(false);
    }

    public async Task<Stream> OpenReadAsync(string id, CancellationToken ct = default)
    {
        if (await GetAsync(id, ct).ConfigureAwait(false) is null)
            throw new KeyNotFoundException($"media object missing: {id} (C-MED·7)");
        return await _files.OpenReadAsync(id, ct).ConfigureAwait(false);
    }

    public async Task RemoveAsync(string id, string actorId, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        var doc = await session.LoadAsync<MediaObject>(id, ct).ConfigureAwait(false);
        if (doc is not null)
        {
            session.Delete(doc);
            await session.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        if (await _files.ExistsAsync(id, ct).ConfigureAwait(false))
            await _files.DeleteFileAsync(id, ct).ConfigureAwait(false); // orphan file: self-cleaning (C-MED·7)
    }

    private static string Sha256Hex(byte[] b) =>
        Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
}
