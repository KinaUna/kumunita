using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Kumunita.Core.Media;

/// <summary>
/// Byte I/O on the volume — swappable seam (C-MED·6); testable against a
/// temp dir. Paths are derived from the content id only (never a filename).
/// </summary>
public interface IMediaFileStore
{
    /// <summary>Atomically write the payload for this content id (C-MED·4); idempotent (dedup by id).</summary>
    Task PutAsync(string contentId, byte[] content, CancellationToken ct = default);

    Task<bool> ExistsAsync(string contentId, CancellationToken ct = default);

    /// <summary>Open the payload read-only (does not copy to memory).</summary>
    Task<Stream> OpenReadAsync(string contentId, CancellationToken ct = default);

    /// <summary>Delete the payload file (not the document). Fails closed if the file is missing.</summary>
    Task DeleteFileAsync(string contentId, CancellationToken ct = default);

    string RootPath { get; }
}
