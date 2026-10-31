using Kumunita.Web.Security;

namespace Kumunita.Web.Tests;

/// <summary>
/// SEC remediation lane — U20 (finding **L2**): the pinned regression tests
/// for the pure <see cref="UploadSignatureSniffer.Matches"/> magic-byte sniffer.
/// Each test builds synthetic bytes (no fixtures, no I/O) and asserts the
/// declared-type → signature match contract (S-7: an upload's bytes must match
/// its declared type). The three §2.3-pinned names are the closed set this
/// class owns — no others.
/// </summary>
public class UploadSignatureSnifferTests
{
    // Synthetic leading bytes (16-byte heads, matching U21/U22's 16-byte read).
    private static readonly byte[] PngHead  = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
                                                0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52 };
    private static readonly byte[] JpegHead = { 0xFF, 0xD8, 0xFF, 0xE0,
                                                0x00, 0x10, 0x4A, 0x46,
                                                0x49, 0x46, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01 };
    private static readonly byte[] GifHead  = { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61,
                                                0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00 };
    private static readonly byte[] WebpHead = { 0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00,
                                                0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38, 0x20 };
    private static readonly byte[] PdfHead  = { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37,
                                                0x0A, 0x31, 0x30, 0x30, 0x20, 0x30, 0x20, 0x72 };
    private static readonly byte[] TextHead = System.Text.Encoding.UTF8.GetBytes("# note\nhello, world\n");
    private static readonly byte[] HtmlHead = System.Text.Encoding.UTF8.GetBytes("<script>alert(1)</script>");
    private static readonly byte[] WordHead = { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00,
                                                0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

    /// <summary>
    /// Each of the five binary-signature types (PNG / JPEG / GIF / WebP / PDF)
    /// accepts its own leading bytes. (Plain-text and zip-based office are
    /// covered by the sniffer's contract too, but the §2.3-pinned acceptance
    /// name is the binary-signature set.)
    /// </summary>
    [Fact]
    public void Png_Jpeg_Gif_WebP_Pdf_Accepted()
    {
        Assert.True(UploadSignatureSniffer.Matches("image/png", PngHead));
        Assert.True(UploadSignatureSniffer.Matches("image/jpeg", JpegHead));
        Assert.True(UploadSignatureSniffer.Matches("image/gif", GifHead));
        Assert.True(UploadSignatureSniffer.Matches("image/webp", WebpHead));
        Assert.True(UploadSignatureSniffer.Matches("application/pdf", PdfHead));

        // Parameter-insensitive: the browser-appended charset must not break the match.
        Assert.True(UploadSignatureSniffer.Matches("image/png; charset=utf-8", PngHead));
    }

    /// <summary>
    /// A file declared <c>image/png</c> whose head is HTML bytes is rejected —
    /// the declared type does not match the signature, so an HTML-in-a-png
    /// upload cannot slip through (S-7).
    /// </summary>
    [Fact]
    public void DeclaredPng_ButHtmlBytes_Rejected()
    {
        Assert.False(UploadSignatureSniffer.Matches("image/png", HtmlHead));
        // And a genuine PNG head under a hostile declaration is also rejected
        // (the bytes must match the declared type, not just be a recognised image).
        Assert.False(UploadSignatureSniffer.Matches("text/plain", PngHead));
    }

    /// <summary>
    /// A declared type with no known signature returns <c>false</c> (fail-
    /// closed): a legacy binary office mime type and an empty/missing declared
    /// type are all rejected, regardless of the bytes.
    /// </summary>
    [Fact]
    public void UnknownDeclaredType_Rejected()
    {
        Assert.False(UploadSignatureSniffer.Matches("application/vnd.ms-powerpoint", WordHead));
        Assert.False(UploadSignatureSniffer.Matches("application/octet-stream", PngHead));
        Assert.False(UploadSignatureSniffer.Matches(null, PngHead));
        Assert.False(UploadSignatureSniffer.Matches("", PngHead));
        Assert.False(UploadSignatureSniffer.Matches("   ", PngHead));
    }
}
