using System.Security.Claims;
using Kumunita.Core.Media;
using Kumunita.Core.Usage;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Tests;

/// <summary>
/// IMPROVE-CODE C11 — the one <see cref="UploadGateHandler"/> for the media
/// upload lanes (ContentImage / Attachment / Document). Part-vs-whole: the
/// handler is named by <b>this</b> test — it owns two decisions that were
/// byte-for-byte clone-copied across three controllers before C11:
/// <list type="number">
/// <item><see cref="UploadGateHandler.CheckGateAsync"/> — the single 413
/// producer (M25 C-UP·3). Returns <c>null</c> within limit/quota, a 413
/// <see cref="StatusCodeResult"/> when oversize, over-quota, or platform-full.</item>
/// <item><see cref="UploadGateHandler.SniffAsync"/> — the SEC U21/U22
/// magic-byte wire, returning a <see cref="UploadSignatureSniffer.SniffVerdict"/>:
/// <c>Mismatch</c> (declared type the bytes contradict), <c>Match</c>
/// (the signature supports the declared type), <c>Unknown</c> (the wire
/// allows the type's discretion — no known signature to contradict).</item>
/// </list>
/// Both are pure (no media store, no write) — the <b>surface-specific</b>
/// bits (the empty-flash, the allowlist, the <c>PutAsync</c> write, the
/// 415 shape) stay in each controller, unchanged. The harness mirrors
/// <see cref="UploadSniffAttachmentImageTests"/> — the
/// <see cref="UploadGateTestSupport.ServicesWith"/> provider is the same
/// one the production controllers resolve from <c>RequestServices</c>.
/// </summary>
public class UploadGateHandlerTests
{
    // ── CheckGateAsync ──────────────────────────────────────────────────

    /// <summary>
    /// Within limit and quota (the M24/M25 baseline — <c>MaxFileBytes =
    /// null</c> → env fallback, <c>PerUserQuotaBytes = 0</c> → quota
    /// disabled, <c>platformIsFull = false</c>) — the gate returns
    /// <c>null</c>, i.e. the caller proceeds to its own allowlist / sniff /
    /// <c>PutAsync</c>.
    /// </summary>
    [Fact]
    public async Task CheckGateAsync_WithinLimitAndQuota_ReturnsNull()
    {
        var controller = ControllerWith(UploadGateTestSupport.ServicesWith(
            new CommunityStorageSettings { MaxFileBytes = null, PerUserQuotaBytes = 0 },
            0,
            platformIsFull: false));

        var opts = new MediaOptions();
        var result = await UploadGateHandler.CheckGateAsync(
            controller, 1024, "user-a", opts.MaxBytes, opts.MaxPlatformBytes);

        Assert.Null(result);
    }

    /// <summary>
    /// Oversize (F2 — <c>incoming &gt; effectiveMax</c>) — the gate
    /// produces the single 413 <see cref="StatusCodeResult"/> (C-UP·3).
    /// The <c>MaxFileBytes = 1024</c> doc cap is the env fallback's
    /// override (the lane's settings doc wins over
    /// <see cref="MediaOptions.MaxBytes"/>); a 2048-byte upload exceeds it.
    /// </summary>
    [Fact]
    public async Task CheckGateAsync_Oversize_Returns413()
    {
        var controller = ControllerWith(UploadGateTestSupport.ServicesWith(
            new CommunityStorageSettings { MaxFileBytes = 1024, PerUserQuotaBytes = 0 },
            0,
            platformIsFull: false));

        var opts = new MediaOptions();
        var result = await UploadGateHandler.CheckGateAsync(
            controller, 2048, "user-a", opts.MaxBytes, opts.MaxPlatformBytes);

        var sc = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status413RequestEntityTooLarge, sc.StatusCode);
    }

    /// <summary>
    /// Over-quota (F3 — <c>usage + incoming &gt; PerUserQuotaBytes</c> with
    /// the file itself within the per-file cap) — the gate produces the
    /// single 413 (C-UP·3). <c>MaxFileBytes = 10 MiB</c> (the file fits),
    /// <c>PerUserQuotaBytes = 1024</c>, current usage <c>512</c>, incoming
    /// <c>513</c> → <c>1025 &gt; 1024</c> → <c>OverQuota</c>.
    /// </summary>
    [Fact]
    public async Task CheckGateAsync_OverQuota_Returns413()
    {
        var controller = ControllerWith(UploadGateTestSupport.ServicesWith(
            new CommunityStorageSettings { MaxFileBytes = 10 * 1024 * 1024, PerUserQuotaBytes = 1024 },
            currentUsageBytes: 512,
            platformIsFull: false));

        var opts = new MediaOptions();
        var result = await UploadGateHandler.CheckGateAsync(
            controller, 513, "user-a", opts.MaxBytes, opts.MaxPlatformBytes);

        var sc = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status413RequestEntityTooLarge, sc.StatusCode);
    }

    /// <summary>
    /// Platform full (the platform-limit lane, C-UP·3) — the gate blocks
    /// every upload with a 413 <b>before</b> the per-file/per-user limits
    /// are consulted. The file itself is within limit and quota; the
    /// <c>IStorageMetricsService.IsFull = true</c> seam is the reject
    /// authority.
    /// </summary>
    [Fact]
    public async Task CheckGateAsync_PlatformFull_Returns413()
    {
        var controller = ControllerWith(UploadGateTestSupport.ServicesWith(
            new CommunityStorageSettings { MaxFileBytes = null, PerUserQuotaBytes = 0 },
            0,
            platformIsFull: true));

        var opts = new MediaOptions();
        var result = await UploadGateHandler.CheckGateAsync(
            controller, 1024, "user-a", opts.MaxBytes, opts.MaxPlatformBytes);

        var sc = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status413RequestEntityTooLarge, sc.StatusCode);
    }

    // ── SniffAsync ──────────────────────────────────────────────────────

    /// <summary>
    /// Declared <c>image/png</c>, PNG-signature bytes in the head →
    /// <see cref="UploadSignatureSniffer.SniffVerdict.Match"/> — the wire
    /// proceeds (the caller's <c>IsAllowed</c> is the allowlist control,
    /// the sniffer's verdict here is informational; no reject).
    /// </summary>
    [Fact]
    public async Task SniffAsync_PngDeclaredWithPngHead_ReturnsMatch()
    {
        var file = TestFile("a.png", "image/png", PngHead);

        var verdict = await UploadGateHandler.SniffAsync(file, "image/png");

        Assert.Equal(UploadSignatureSniffer.SniffVerdict.Match, verdict);
    }

    /// <summary>
    /// Declared <c>image/png</c>, HTML bytes in the head →
    /// <see cref="UploadSignatureSniffer.SniffVerdict.Mismatch"/> — the
    /// wire rejects (each controller maps this to a 415, unchanged).
    /// </summary>
    [Fact]
    public async Task SniffAsync_PngDeclaredWithHtmlHead_ReturnsMismatch()
    {
        var file = TestFile("evil.png", "image/png", HtmlHead);

        var verdict = await UploadGateHandler.SniffAsync(file, "image/png");

        Assert.Equal(UploadSignatureSniffer.SniffVerdict.Mismatch, verdict);
    }

    /// <summary>
    /// Declared <c>application/pdf</c>, <c>%PDF</c> bytes in the head →
    /// <see cref="UploadSignatureSniffer.SniffVerdict.Match"/>.
    /// </summary>
    [Fact]
    public async Task SniffAsync_PdfDeclaredWithPdfHead_ReturnsMatch()
    {
        var file = TestFile("a.pdf", "application/pdf", PdfHead);

        var verdict = await UploadGateHandler.SniffAsync(file, "application/pdf");

        Assert.Equal(UploadSignatureSniffer.SniffVerdict.Match, verdict);
    }

    /// <summary>
    /// Declared <c>text/plain</c>, plain-text bytes in the head (no NUL) →
    /// <see cref="UploadSignatureSniffer.SniffVerdict.Unknown"/> — the
    /// sniffer cannot prove a text claim from bytes alone; the wire allows
    /// (the caller's <c>IsAllowed</c> is the control).
    /// </summary>
    [Fact]
    public async Task SniffAsync_TextDeclaredWithTextHead_ReturnsUnknown()
    {
        var file = TestFile("a.txt", "text/plain",
            System.Text.Encoding.UTF8.GetBytes("hello, world\nline two\n"));

        var verdict = await UploadGateHandler.SniffAsync(file, "text/plain");

        Assert.Equal(UploadSignatureSniffer.SniffVerdict.Unknown, verdict);
    }

    /// <summary>
    /// Declared <c>text/plain</c>, a NUL byte in the first 16 bytes →
    /// <see cref="UploadSignatureSniffer.SniffVerdict.Mismatch"/> — a
    /// binary payload contradicts the text claim.
    /// </summary>
    [Fact]
    public async Task SniffAsync_TextDeclaredWithNulHead_ReturnsMismatch()
    {
        var head = new byte[] { 0x61, 0x62, 0x00, 0x64 };   // "ab\0d"
        var file = TestFile("binary.txt", "text/plain", head);

        var verdict = await UploadGateHandler.SniffAsync(file, "text/plain");

        Assert.Equal(UploadSignatureSniffer.SniffVerdict.Mismatch, verdict);
    }

    /// <summary>
    /// Declared <c>application/octet-stream</c> (an allowed-but-unknown
    /// type — no known signature to contradict) →
    /// <see cref="UploadSignatureSniffer.SniffVerdict.Unknown"/> — the
    /// wire allows; the caller's <c>IsAllowed</c> is the control.
    /// </summary>
    [Fact]
    public async Task SniffAsync_OctetStreamDeclared_ReturnsUnknown()
    {
        var file = TestFile("blob.bin", "application/octet-stream", PngHead);

        var verdict = await UploadGateHandler.SniffAsync(file, "application/octet-stream");

        Assert.Equal(UploadSignatureSniffer.SniffVerdict.Unknown, verdict);
    }

    // ── fixtures ────────────────────────────────────────────────────────

    // Synthetic leading bytes (16-byte heads, matching U21's 16-byte read).
    private static readonly byte[] PngHead = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
                                               0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52 };
    private static readonly byte[] HtmlHead = System.Text.Encoding.UTF8.GetBytes("<script>alert(1)</script>");
    private static readonly byte[] PdfHead  = System.Text.Encoding.UTF8.GetBytes("%PDF-1.4\n%binary\n");

    /// <summary>
    /// A bare <see cref="Microsoft.AspNetCore.Mvc.Controller"/> with a
    /// <see cref="ControllerContext"/> whose <c>HttpContext.RequestServices</c>
    /// is the <see cref="UploadGateTestSupport.ServicesWith"/> provider —
    /// the same seam the production upload actions resolve their
    /// <c>IUploadGate</c> / <c>IStorageSettingsService</c> from.
    /// </summary>
    private static Controller ControllerWith(IServiceProvider services)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = services;
        var controller = new GateController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
        return controller;
    }

    /// <summary>
    /// <see cref="Microsoft.AspNetCore.Mvc.Controller"/> is abstract, so the
    /// handler's <c>controller.HttpContext.RequestServices</c> read needs a
    /// concrete carrier. No action is invoked here — only the
    /// <see cref="ControllerContext"/> is consumed.
    /// </summary>
    private sealed class GateController : Controller { }

    /// <summary>
    /// A minimal <see cref="IFormFile"/> carrier backed by a byte array —
    /// the sniffer wire reads <c>Length</c> / <c>OpenReadStream</c>; no
    /// multipart parsing at this seam. The <see cref="UploadSniffAttachmentImageTests"/>
    /// idiom (private there; replicated here).
    /// </summary>
    private sealed class TestFormFile : IFormFile
    {
        private readonly byte[] _content;

        public TestFormFile(string fileName, string? contentType, byte[] content)
        {
            _content = content ?? Array.Empty<byte>();
            FileName = fileName;
            Name = "file";
            ContentType = contentType ?? string.Empty;
            ContentDisposition = string.Empty;
            Length = _content.Length;
        }

        public long Length { get; }
        public string FileName { get; }
        public string Name { get; }
        public string ContentType { get; set; }
        public string ContentDisposition { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

        public Stream Open() => new MemoryStream(_content, false);
        public Stream OpenReadStream() => new MemoryStream(_content, false);
        public Stream OpenReadStream(long startingOffset)
            => startingOffset switch
            {
                0 => new MemoryStream(_content, false),
                _ => new MemoryStream(_content[(int)startingOffset..], false),
            };

        public void CopyTo(Stream target)
            => target.Write(_content, 0, _content.Length);

        public void CopyTo(Stream target, CancellationToken cancellationToken)
            => target.Write(_content, 0, _content.Length);

        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
        {
            target.Write(_content, 0, _content.Length);
            return Task.CompletedTask;
        }

        public void Dispose() { }
    }

    private static IFormFile TestFile(string fileName, string? contentType, byte[] content)
        => new TestFormFile(fileName, contentType, content);
}
