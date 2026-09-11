using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Kumunita.Core.Media;

/// <summary>Local-volume byte store. Path = <c>{RootPath}/{id[0..2]}/{id}</c> (hex-only id).</summary>
public sealed class LocalVolumeFileStore : IMediaFileStore
{
    private readonly MediaOptions _opts;

    public LocalVolumeFileStore(IOptions<MediaOptions> options)
    {
        _opts = options.Value;
        RootPath = _opts.RootPath;
        Directory.CreateDirectory(RootPath); // idempotent
    }

    public string RootPath { get; }

    private string PathFor(string contentId)
    {
        var safe = string.Concat(contentId.Where(c => (c >= 'a' && c <= 'f') || (c >= '0' && c <= '9')));
        var dir = safe.Length >= 2 ? safe[..2] : "_";
        return Path.Combine(RootPath, dir, safe);
    }

    public Task<bool> ExistsAsync(string contentId, CancellationToken ct = default) =>
        Task.FromResult(File.Exists(PathFor(contentId)));

    public Task<Stream> OpenReadAsync(string contentId, CancellationToken ct = default)
    {
        var p = PathFor(contentId);
        if (!File.Exists(p))
            throw new FileNotFoundException("media file not found (C-MED·7 orphan-safe: nothing references a bare hash)", p);
        return Task.FromResult<Stream>(new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.Read));
    }

    public Task DeleteFileAsync(string contentId, CancellationToken ct = default)
    {
        var p = PathFor(contentId);
        if (!File.Exists(p)) throw new FileNotFoundException("media file not found (C-MED·7 orphan-safe: nothing references a bare hash)", p);
        File.Delete(p);
        return Task.CompletedTask;
    }

    public Task PutAsync(string contentId, byte[] content, CancellationToken ct = default)
    {
        var final = PathFor(contentId);
        Directory.CreateDirectory(Path.GetDirectoryName(final)!);
        var tmp = final + ".tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            fs.Write(content, 0, content.Length);
            fs.Flush();
            fs.Flush(true); // fsync — durability before rename (C-MED·4)
        }
        File.Move(tmp, final, overwrite: true); // atomic replace on both Win/POSIX
        return Task.CompletedTask;
    }
}
