using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

    // ── M24 volume-stat reads (ADR 0134 C-SM·2) ─────────────────────────────
    // Read-only, zero writes (C-SM·2): a filesystem stat of the configured
    // RootPath's partition — NOT a Postgres query, so it stays out of any
    // QuerySession (C-SM·4 / F7). No I/O side-effect beyond the stat call.

    public Task<long> GetTotalSpaceBytesAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (OperatingSystem.IsWindows())
            return Task.FromResult(new DriveInfo(Path.GetPathRoot(RootPath)!).TotalSize);
        var buf = Statvfs(RootPath); // Linux: one statvfs syscall (C-SM·2)
        return Task.FromResult((long)(buf.f_blocks * buf.f_frsize));
    }

    public Task<long> GetFreeSpaceBytesAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (OperatingSystem.IsWindows())
            return Task.FromResult(new DriveInfo(Path.GetPathRoot(RootPath)!).AvailableFreeSpace);
        var buf = Statvfs(RootPath); // Linux: one statvfs syscall (C-SM·2)
        return Task.FromResult((long)(buf.f_bavail * buf.f_frsize));
    }

    /// <summary>
    /// Linux <c>statvfs</c> for <paramref name="path"/> (Windows uses
    /// <see cref="DriveInfo"/>). Throws <see cref="IOException"/> on failure.
    /// </summary>
    private static statvfs_t Statvfs(string path)
    {
        if (statvfs(path, out var buf) != 0)
            throw new IOException($"statvfs failed for '{path}' (errno {Marshal.GetLastPInvokeError()})");
        return buf;
    }

    /// <summary>The <c>struct statvfs</c> layout (64-bit): 10 × 8-byte fields.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct statvfs_t
    {
        public ulong f_bsize, f_frsize, f_blocks, f_bfree, f_bavail,
                     f_files, f_ffree, f_favail, f_flag, f_namemax;
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int statvfs(string path, out statvfs_t buf);
}
