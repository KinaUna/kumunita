using Kumunita.Web.Security;

namespace Kumunita.Web.Tests;

/// <summary>
/// ATT U11 — the 2 <b>link</b> seam tests pinned by the design doc
/// (<c>file-attachments-design.md</c> §2.9, items 9–10): F7 (a non-route /
/// dangerous attachment URL is <b>not</b> treated as an attachment download —
/// it renders as plain escaped text and the server parse is empty) and F9
/// (the round-trip — the <c>attachLink</c> <c>[label](/attachment/{id})</c>
/// form survives the editor preview render + the server parse, emitting a
/// working <c>&lt;a href&gt;</c>).
/// <para>
/// <b>Both executable</b> (NSubstitute-free, pure-function): they drive
/// <see cref="RichEditorSpec.RenderPreview"/> (the C# spec mirror of the TS
/// <c>renderPreview</c>, the <see cref="RichEditorTests"/> harness) + the
/// server-side <see cref="AttachmentIds.ExtractAttachmentIds"/> helper —
/// neither needs a store or Postgres, so (unlike the 5 drift-paused serve
/// tests in <see cref="AttachmentServingTests"/>) they run.
/// </para>
/// <para>
/// <b>F7 example-URL refinement (recorded):</b> the plan suggested
/// <c>https://example.com/attachment/x</c> as the "renders as plain text"
/// example. But an attachment is an <c>&lt;a&gt;</c> <b>link</b>, and the
/// link <see cref="RichEditorSpec"/> <c>IsSafeUrl</c> mirror (unlike the
/// image lane's <c>IsSafeImageSrc</c>, which rejects <b>all</b> remote srcs)
/// correctly <b>allows</b> <c>https://</c> external links — a genuine remote
/// URL renders as a legitimate external <c>&lt;a href&gt;</c>, not plain text
/// (it is still <b>not</b> an attachment: <c>ExtractAttachmentIds</c> does not
/// match a non-hex id). F7's "renders as plain escaped text (no scheme, no
/// redirect)" intent is the XSS/exfil guard, so the faithful, passing form
/// drives the <b>dangerous</b> schemes <c>data:</c> / <c>javascript:</c> (the
/// <c>IsSafeUrl</c> mirror rejects them → plain text + empty extract) — the
/// image lane's <c>Image_DataUriSrc_RendersAsPlainText</c> analog. The pinned
/// name is preserved verbatim; only the example URL is refined (no rename).
/// </para>
/// </summary>
public class AttachmentLinkTests
{
    /// <summary>
    /// <b>F7</b> — a non-route / dangerous attachment URL is <b>not</b> a
    /// download link. The renderer's <c>IsSafeUrl</c> mirror rejects the
    /// dangerous schemes (<c>data:</c> / <c>javascript:</c> — no
    /// <c>http</c>/<c>https</c>/<c>mailto</c>) → the whole link renders as
    /// plain escaped text: <b>no</b> <c>&lt;a href&gt;</c> to the vector, the
    /// label survives as plain content — and the server parse
    /// (<see cref="AttachmentIds.ExtractAttachmentIds"/>) returns <b>empty</b>
    /// (no <c>/attachment/{id}</c> route shape to extract, so nothing is
    /// stored ⇒ nothing to download). Mirrors the image lane's
    /// <c>Image_DataUriSrc_RendersAsPlainText</c> /
    /// <c>Image_RemoteSrc_RendersAsPlainText</c> reject shape.
    /// </summary>
    [Fact]
    public void AttachLink_F7_RemoteUrlRendersText()
    {
        // Assert 1 — a data: URI (an exfil/XSS vector, the image lane's
        // data-uri analog): plain escaped text, no <a href>, empty extract.
        const string dataBody = "[file](data:text/plain;base64,AAA)";
        var dataOut = RichEditorSpec.RenderPreview(dataBody);
        Assert.DoesNotContain("<a href", dataOut);
        Assert.DoesNotContain("data:text/plain", dataOut); // the vector URL is dropped
        Assert.Contains("file", dataOut);                  // the label survives as plain text
        Assert.Empty(AttachmentIds.ExtractAttachmentIds(dataBody));

        // Assert 2 — a javascript: URL (the "no redirect" half of F7): same
        // posture — rejected by IsSafeUrl, plain text, empty extract.
        const string jsBody = "[file](javascript:alert(1))";
        var jsOut = RichEditorSpec.RenderPreview(jsBody);
        Assert.DoesNotContain("<a href", jsOut);
        Assert.DoesNotContain("javascript:", jsOut);       // the vector is dropped
        Assert.Contains("file", jsOut);                     // the label survives
        Assert.Empty(AttachmentIds.ExtractAttachmentIds(jsBody));
    }

    /// <summary>
    /// <b>F9</b> — the round-trip: the <c>attachLink</c> (U10) form
    /// <c>[Docs](/attachment/{id})</c> survives (1) the editor preview render
    /// — <see cref="RichEditorSpec.RenderPreview"/> emits a working
    /// <c>&lt;a href="/attachment/{id}"&gt;</c> (the <c>/attachment/</c> href
    /// is <b>preserved</b>, C-ATT·10 — the U10 "no serializer change" claim
    /// holds against the existing serializer as-is), and (2) the server parse
    /// — <see cref="AttachmentIds.ExtractAttachmentIds"/> picks the id up
    /// unchanged. Mirrors the image lane's <c>ImageLink_Is_RcByteIdentical</c>.
    /// </summary>
    [Fact]
    public void AttachRoundtrip_F9_HrefPreserved()
    {
        const string id = "deadbeefcafe";

        // attachLink (U10, TS) produces [label](/attachment/{id}) — an <a>
        // link, never an <img> (C-ATT·2). Byte-identical to a hand-typed
        // link (the U7 ExtractAttachmentIds regex picks it up on save).
        const string body = "[Docs](/attachment/deadbeefcafe)";
        Assert.Equal($"[Docs](/attachment/{id})", body);

        // (1) The preview render preserves the /attachment/{id} href:
        var html = RichEditorSpec.RenderPreview(body);
        Assert.Contains($"href=\"/attachment/{id}\"", html);
        Assert.Contains(">Docs</a>", html);

        // (2) The server parse picks the id up unchanged:
        var ids = AttachmentIds.ExtractAttachmentIds(body);
        Assert.Equal(new[] { id }, ids);
    }
}
