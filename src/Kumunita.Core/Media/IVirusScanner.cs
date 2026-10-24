using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Kumunita.Core.Media;

/// <summary>
/// F16 / UPL-02 — the scan verdict for one stored media byte. A signature
/// match is a <em>normal return value</em> (<see cref="VirusScanStatus.Infected"/>),
/// not an exception — the serve lane branches on it exactly like it branches
/// on a store miss.
/// </summary>
public enum VirusScanStatus
{
    /// <summary>No signature match → the serve lane proceeds (the unchanged path).</summary>
    Clean,
    /// <summary>A signature match → quarantine + 404 (not served).</summary>
    Infected,
}

/// <summary>
/// F16 / UPL-02 — the byte-level antivirus seam. <b>Core, HTTP-free (ADR
/// 0006-D)</b>: the contract is "bytes in → verdict out", with no
/// <see cref="System.Net.Http.HttpClient"/> and no Web types. The default
/// implementation (<see cref="ClamAvScanner"/>) drives a <em>local</em>
/// ClamAV daemon over a loopback socket — the <see cref="Kumunita.Core.Identity.ISmtpSender"/>
/// "Core I/O-seam / outside transport" split (the seam is Core; the concrete
/// daemon is the outside-touching part). Stubbable: F16.U3's test injects a
/// stub that returns <see cref="VirusScanStatus.Infected"/> and asserts the
/// serve lane 404s (it does not need a real daemon).
/// </summary>
public interface IVirusScanner
{
    /// <summary>
    /// Scan the payload stream and return the verdict. A signature match
    /// returns <see cref="VirusScanStatus.Infected"/>; a clean byte returns
    /// <see cref="VirusScanStatus.Clean"/>. The caller owns and disposes
    /// <paramref name="content"/> — the scanner must not leave it open.
    /// <see cref="ClamAvScanner"/> is <b>fail-open</b> on an unavailable /
    /// errored daemon (it returns <see cref="VirusScanStatus.Clean"/>) so a box
    /// without ClamAV still serves (the §2.1 pin — a missing optional daemon
    /// must not DoS the serve lane).
    /// </summary>
    Task<VirusScanStatus> ScanAsync(Stream content, CancellationToken ct = default);
}

/// <summary>
/// Per-instance ClamAV daemon config (the host binds <c>VirusScan__*</c>, the
/// <see cref="MediaOptions"/> / <see cref="Kumunita.Core.Identity.SmtpOptions"/>
/// "Core owns the class, the host binds the values" pattern). <see cref="Host"/>
/// + <see cref="Port"/> point at the local <c>clamd</c> INET listener.
/// </summary>
public sealed class VirusScanOptions
{
    public const string SectionName = "VirusScan";

    /// <summary>The ClamAV daemon host (loopback by default — it is a local process, not a provider).</summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>The ClamAV daemon INET port (3310, the <c>clamd.conf</c> <c>TCPSocket</c> default).</summary>
    public int Port { get; set; } = 3310;

    /// <summary>
    /// A ceiling on the bytes scanned per file (mirrors the <c>clamd</c>
    /// <c>StreamMaxLen</c> directive). A payload above this is not scanned and
    /// the verdict is <see cref="VirusScanStatus.Clean"/> (fail-open) — a daemon
    /// that would reject an oversize stream must not become a DoS on the serve
    /// lane (the §2.1 "a missing / errored daemon degrades to Clean" shape).
    /// </summary>
    public long MaxScanBytes { get; set; } = 100L * 1024 * 1024; // 100 MiB
}

/// <summary>
/// The <b>default</b> <see cref="IVirusScanner"/>: a <em>local</em> ClamAV
/// daemon driver (the <see cref="Kumunita.Core.Identity.SmtpSender"/> BCL-I/O
/// precedent — no package, no <c>HttpClient</c>; a loopback
/// <see cref="TcpClient"/> speaks the <c>clamd</c> <c>INSTREAM</c> line
/// protocol). <b>Fail-open by construction</b> (the §2.1 pin): a missing
/// daemon, a refused connection, a malformed reply, an oversize payload, or
/// any transport fault degrades to <see cref="VirusScanStatus.Clean"/> and
/// never throws into the serve lane — so a box without ClamAV still serves,
/// and the gate becomes "real" only once the owner runs the F16 ops step
/// ("run ClamAV on the prod volume"). The gate's <em>enforcement</em> is the
/// serve lane (the §4/§5 three controllers), not this scanner.
/// </summary>
public sealed class ClamAvScanner : IVirusScanner
{
    private readonly VirusScanOptions _opts;

    public ClamAvScanner(IOptions<VirusScanOptions> options)
    {
        _opts = options.Value;
    }

    public async Task<VirusScanStatus> ScanAsync(Stream content, CancellationToken ct = default)
    {
        // Fail-open: read the payload to a buffer. Any read fault degrades to
        // Clean (a scanner fault is not a serve-lane fault — §2.1). The caller
        // owns `content`; we only read it (no new byte-read seam — the serve
        // lane's own IMediaStore.OpenReadAsync produced the stream).
        byte[] payload;
        try
        {
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, ct).ConfigureAwait(false);
            payload = ms.ToArray();
        }
        catch
        {
            return VirusScanStatus.Clean;
        }

        if (payload.Length == 0)
            return VirusScanStatus.Clean; // an empty byte is clean (nothing to match)
        if (payload.Length > _opts.MaxScanBytes)
            return VirusScanStatus.Clean; // oversize: fail-open (the StreamMaxLen mirror)

        // The INSTREAM line protocol (no package): INSTREAM → "200 Streaming
        // file upload enabled" → <4-byte big-endian length> → <payload> →
        // "OK" | "STREAM ERROR …" | "FOUND <signature>". (TcpClient is
        // IDisposable, not IAsyncDisposable — a plain `using`.)
        using var socket = new TcpClient();
        try
        {
            await socket.ConnectAsync(_opts.Host, _opts.Port, ct).ConfigureAwait(false);
            using var net = socket.GetStream();
            using var reader = new StreamReader(net, Encoding.ASCII, leaveOpen: true);
            using var writer = new StreamWriter(net, new UTF8Encoding(false), leaveOpen: true) { NewLine = "\r\n" };

            writer.WriteLine("INSTREAM");
            await writer.FlushAsync(ct).ConfigureAwait(false);

            var ack = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (ack is null || !ack.StartsWith("200", StringComparison.Ordinal))
                return VirusScanStatus.Clean; // the daemon did not accept the stream → fail-open

            var len = BitConverter.GetBytes((uint)payload.Length);
            Array.Reverse(len); // little-endian → big-endian (the INSTREAM length header)
            await net.WriteAsync(len, ct).ConfigureAwait(false);
            await net.WriteAsync(payload, 0, payload.Length, ct).ConfigureAwait(false);

            var verdict = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (verdict is null)
                return VirusScanStatus.Clean; // no reply → fail-open
            // Only a signature match (the "FOUND …" reply) is Infected; "OK",
            // "STREAM ERROR", and any other reply are all treated as Clean
            // (fail-open — the §2.1 default, never a false-positive quarantine).
            return verdict.StartsWith("FOUND", StringComparison.Ordinal)
                ? VirusScanStatus.Infected
                : VirusScanStatus.Clean;
        }
        catch
        {
            // Fail-open (§2.1): connection refused, a timeout, a socket fault,
            // or a cancellation — NONE of these abort the serve lane.
            return VirusScanStatus.Clean;
        }
    }
}
