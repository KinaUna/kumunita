using Kumunita.Core.Usage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Web.Security;

/// <summary>
/// The shared upload **guard pipeline** for the media upload lanes
/// (IMPROVE-CODE C11 — the one <c>UploadHandler</c> for
/// <c>ContentImageController</c> / <c>AttachmentController</c> /
/// <c>DocumentController</c>).
/// <para>
/// Before C11 each of those upload actions carried its **own** copy of the
/// same two guards (a silent-coupling clone — the
/// <c>anti-patterns.md</c> seam the IMPROVE-CODE E7 row names: "upload
/// handling in <c>ContentImage</c> / <c>Attachment</c> / <c>Document</c>
/// controllers"):
/// </para>
/// <list type="number">
/// <item><b>the size / over-quota guard</b> (M25 C-UP·3 — the single 413
/// producer): resolve <see cref="IStorageSettingsService"/> +
/// <see cref="IUploadGate"/> from the request, read the settings doc, and run
/// <c>CheckUpload</c> over the admin doc + the subject's usage, running
/// <b>BEFORE</b> any <c>PutAsync</c> so a reject writes no byte (C-UP·2).</item>
/// <item><b>the magic-byte sniffer wire</b> (SEC U21/U22): read the leading 16
/// bytes via <c>OpenReadStream</c> (bounded, so the payload is not read twice
/// and cannot leak into memory) and reject on
/// <see cref="UploadSignatureSniffer.SniffVerdict.Mismatch"/> — a declared
/// type whose known signature the bytes contradict.</item>
/// </list>
/// <para>
/// Both are surface-independent and were byte-for-byte identical at every
/// call site, so they live here, exactly once. The **surface-specific** bits
/// stay in each controller (the public surface — routes, the empty-flash
/// key, the <c>Is*Allowed</c> allowlist, the 415 rejection shape, and the
/// post-<c>PutAsync</c> action — is unchanged, per the C11 constraint): the
/// empty-file → 400 flash, the content-type allowlist check
/// (<see cref="Kumunita.Core.Media.MediaOptions.IsAllowed"/> /
/// <c>IsAttachmentAllowed</c> / <c>IsDocumentAllowed</c>), the
/// <c>IFormFile</c> → bytes copy, and the <c>IMediaStore.PutAsync</c> write.
/// </para>
/// <para>
/// Part-vs-whole: <see cref="UploadGateHandler"/> is named by
/// <c>tests/Kumunita.Web.Tests/UploadGateHandlerTests.cs</c> (a decision — the
/// sniff verdict routing — that gets its own unit test, not only its four
/// callers). The methods are <c>static</c> and take the controller + the two
/// limits (the env per-file max and the platform limit — the caller extracts
/// them from its <c>MediaOptions</c>) so they compose with each surface's
/// existing <c>[Authorize]</c> / <c>[ValidateAntiForgeryToken]</c> /
/// <c>[EnableRateLimiting]</c> attributes (no behavior change). Keeping
/// <c>MediaOptions</c> out of the public surface is deliberate: it keeps the
/// Web→Media boundary edge at the ratchet ceiling (the controllers are the
/// edge; this handler is not).
/// </para>
/// </summary>
public static class UploadGateHandler
{
    /// <summary>
    /// The one-line 413 producer (M25 C-UP·3): resolve the
    /// <see cref="IUploadGate"/> + settings doc from the request and run the
    /// pure <c>StorageLimits.Decide</c> over the admin doc + the subject's
    /// usage. Runs <b>BEFORE</b> any <c>PutAsync</c> so a reject writes no
    /// byte (C-UP·2). Returns the gate's rejection result (the single 413
    /// producer, or a quota <c>IActionResult</c>), or <c>null</c> when the
    /// upload is within limit and quota — in which case the caller proceeds to
    /// its own allowlist / sniff / <c>PutAsync</c>.
    /// </summary>
    public static async Task<IActionResult?> CheckGateAsync(
        Controller controller,
        long fileLength,
        string subject,
        long envMaxBytes,
        long platformLimitBytes)
    {
        var requestServices = controller.HttpContext.RequestServices;
        var settingsSvc = requestServices.GetRequiredService<IStorageSettingsService>();
        var uploadGate  = requestServices.GetRequiredService<IUploadGate>();
        var settings = await settingsSvc.GetOrCreateAsync(CancellationToken.None);
        return await uploadGate.CheckUpload(fileLength, subject, settings, envMaxBytes, platformLimitBytes);
    }

    /// <summary>
    /// The SEC U21/U22 magic-byte sniffer wire. Reads the leading 16 bytes of
    /// <paramref name="file"/> via <c>OpenReadStream</c> (the read is bounded
    /// to 16 bytes, so it works for every <c>IFormFile</c> implementation
    /// without materialising the payload twice and cannot leak a full upload
    /// into memory), then classifies against the declared
    /// <paramref name="contentType"/>.
    /// </summary>
    /// <returns>
    /// <see cref="UploadSignatureSniffer.SniffVerdict.Mismatch"/> — the
    /// declared type has a known signature that the bytes contradict (the
    /// caller rejects, 415); <see cref="UploadSignatureSniffer.SniffVerdict.Match"/>
    /// or <see cref="UploadSignatureSniffer.SniffVerdict.Unknown"/> — the
    /// caller proceeds (a mismatch is the only reject).
    /// </returns>
    public static async Task<UploadSignatureSniffer.SniffVerdict> SniffAsync(
        IFormFile file, string? contentType)
    {
        var head = new byte[Math.Min(16L, file.Length)];
        if (head.Length > 0)
        {
            await using var rs = file.OpenReadStream();
            var n = 0;
            while (n < head.Length)
            {
                var got = await rs.ReadAsync(head.AsMemory(n));
                if (got == 0) break;
                n += got;
            }
            if (n < head.Length) Array.Resize(ref head, n);
        }
        return UploadSignatureSniffer.Classify(contentType, head);
    }
}
