using Kumunita.Web.Security;

namespace Kumunita.Web.Tests;

/// <summary>
/// Pure-function tests for <see cref="MarkdownRenderer"/> — the single
/// Markdown → HTML engine (RC R·1). The pins this harness owns (see
/// <see cref="MarkdownRenderer.RenderHtml"/> for the escape-first
/// construction and the RC lane's design doc for the pinned <c>IsSafeImageSrc</c>
/// allowlist + the exact <c>&lt;img&gt;</c> emission):
/// <list type="number">
/// <item><b>Platform-route image → <c>&lt;img&gt;</c></b>: a
///       <c>![alt](/content-image/{id})</c> whose <c>id</c> is 1–128
///       lowercase hex renders exactly one <c>&lt;img&gt;</c> with the pinned
///       attribute set (<c>src</c>, <c>alt</c>, <c>class="rc-image"</c>,
///       <c>loading="lazy"</c>). The <c>alt</c> is escaped verbatim — no
///       inline rules inside the attribute.</item>
/// <item><b>Remote / data-URI / malformed-id images → plain text</b>:
///       any <c>src</c> that is not the platform route shape (or a relative
///       path) rejects — the whole <c>![alt](src)</c> renders as escaped
///       plain text, <b>no</b> <c>&lt;img&gt;</c>, <b>no</b> <c>src=</c>
///       attribute (RC R·2, the <c>IsSafeUrl</c>-reject precedent).</item>
/// <item><b>Inline image stays in its paragraph</b>: an image on a text line
///       renders inside the surrounding single <c>&lt;p&gt;</c>, not a new
///       block element.</item>
/// <item><b>Regression pin (R·1/R·8)</b>: the pre-existing subset —
///       <c>**bold**</c>, <c>*italic*</c>, a <c>-</c> list, a <c>##</c>
///       heading, a `` `code` `` span — still renders. Proves the image
///       extension did not regress the engine.</item>
/// <item><b>Hostile markup stays escaped (R·2)</b>: a raw <c>&lt;script&gt;</c>
///       is entity-escaped (no raw tag survives), and a hostile
///       <c>onerror="x"</c> in an <c>alt</c> is escaped verbatim — no raw
///       <c>onerror=</c> attribute is emitted on the <c>&lt;img&gt;</c>.</item>
/// </list>
/// <para>
/// No DI, no fixture: each test calls
/// <see cref="MarkdownRenderer.RenderHtml"/> on a literal and asserts on the
/// output string (contains / not-contains), the escape-first contract made
/// observable end to end.
/// </para>
/// </summary>
public class MarkdownRendererTests
{
    /// <summary>
    /// A platform-route image — <c>![alt](/content-image/{id})</c> with a
    /// well-formed lowercase-hex <c>id</c> — renders exactly one
    /// <c>&lt;img&gt;</c> carrying the pinned attribute set. The <c>alt</c>
    /// (<c>fence</c>) is emitted verbatim (it carries no Markdown), and the
    /// source line wraps in a single <c>&lt;p&gt;</c>.
    /// </summary>
    [Fact]
    public void Image_PlatformRouteSrc_RendersImgTag()
    {
        var output = MarkdownRenderer.RenderHtml("![fence](/content-image/deadbeefcafe)");

        Assert.Equal(1, Count(output, "<img"));
        Assert.Contains("src=\"/content-image/deadbeefcafe\"", output);
        Assert.Contains("alt=\"fence\"", output);
        Assert.Contains("class=\"rc-image\"", output);
        Assert.Contains("loading=\"lazy\"", output);
    }

    /// <summary>
    /// A remote <c>src</c> (<c>https://…</c>) is outside the
    /// <c>IsSafeImageSrc</c> allowlist — the whole <c>![alt](src)</c> renders
    /// as escaped plain text. No <c>&lt;img&gt;</c>, no <c>src=</c> attribute;
    /// the URL text survives as plain (escaped) content.
    /// </summary>
    [Fact]
    public void Image_RemoteSrc_RendersAsPlainText()
    {
        var output = MarkdownRenderer.RenderHtml("![x](https://evil.example/img.png)");

        Assert.DoesNotContain("<img", output);
        Assert.DoesNotContain("src=", output);
        Assert.Contains("https://evil.example/img.png", output);
    }

    /// <summary>
    /// A <c>data:</c>-URI <c>src</c> (an XSS vector — inline image bytes) is
    /// rejected like any remote <c>src</c>: plain escaped text, no
    /// <c>&lt;img&gt;</c>, no <c>src=</c>.
    /// </summary>
    [Fact]
    public void Image_DataUriSrc_RendersAsPlainText()
    {
        var output = MarkdownRenderer.RenderHtml("![x](data:image/png;base64,AAA)");

        Assert.DoesNotContain("<img", output);
        Assert.DoesNotContain("src=", output);
        Assert.Contains("data:image/png;base64,AAA", output);
    }

    /// <summary>
    /// A <c>/content-image/</c> path whose <c>id</c> is not 1–128 lowercase
    /// hex — a non-hex id and an empty id — both reject: plain escaped text,
    /// no <c>&lt;img&gt;</c>. (The route shape is exact; a malformed id is not
    /// a relative-path fallback.)
    /// </summary>
    [Fact]
    public void Image_MalformedHexId_RendersAsPlainText()
    {
        var nonHex = MarkdownRenderer.RenderHtml("![x](/content-image/notahexid)");
        Assert.DoesNotContain("<img", nonHex);

        var emptyId = MarkdownRenderer.RenderHtml("![x](/content-image/)");
        Assert.DoesNotContain("<img", emptyId);
    }

    /// <summary>
    /// An image in the middle of a text line stays inside that single
    /// <c>&lt;p&gt;</c> — the paragraph's open tag precedes the
    /// <c>&lt;img&gt;</c> and its close tag follows; it is not emitted as a
    /// separate block element.
    /// </summary>
    [Fact]
    public void Image_InlineInParagraph_StaysInParagraph()
    {
        var output = MarkdownRenderer.RenderHtml("Hello ![pic](/content-image/ab) world");

        Assert.Contains("<img", output);

        var pOpen = output.IndexOf("<p>", StringComparison.Ordinal);
        var img = output.IndexOf("<img", StringComparison.Ordinal);
        var pClose = output.IndexOf("</p>", StringComparison.Ordinal);
        Assert.True(pOpen >= 0 && img >= 0 && pClose >= 0,
            $"expected <p>, <img>, </p>; got: {output}");
        Assert.True(pOpen < img && img < pClose,
            $"expected <p>…<img>…</p> order; got: {output}");
        Assert.Equal(1, Count(output, "<p>"));
        Assert.Equal(1, Count(output, "</p>"));
    }

    /// <summary>
    /// Regression pin (R·1/R·8): the pre-existing Markdown subset still
    /// renders after the image extension — <c>**bold**</c>, <c>*italic*</c>,
    /// a <c>-</c> list item, a <c>##</c> heading, and a `` `code` `` span each
    /// produce their tags. One input exercises all five.
    /// </summary>
    [Fact]
    public void Bold_Italic_List_Heading_StillRender()
    {
        var markdown = "## Heading\n"
                     + "Some **bold** *italic* `code` text\n"
                     + "- a list item";
        var output = MarkdownRenderer.RenderHtml(markdown);

        Assert.Contains("<h2>", output);
        Assert.Contains("<strong>", output);
        Assert.Contains("<em>", output);
        Assert.Contains("<code>", output);
        Assert.Contains("<li>", output);
    }

    /// <summary>
    /// Hostile markup stays escaped (R·2). A raw <c>&lt;script&gt;</c> tag in
    /// a paragraph is entity-escaped (no raw <c>&lt;script</c> survives), and
    /// a hostile <c>onerror="x"</c> placed in an image's <c>alt</c> is escaped
    /// verbatim — so no raw <c>onerror=</c> followed by a quote (an attribute
    /// position) is emitted on the <c>&lt;img&gt;</c>; the <c>alt</c> keeps it
    /// as <c>onerror=&quot;…&quot;</c> text only.
    /// </summary>
    [Fact]
    public void HostileMarkup_StillEscaped()
    {
        // A raw <script> tag → entity-escaped, never a live tag.
        var script = MarkdownRenderer.RenderHtml("<script>alert(1)</script>");
        Assert.DoesNotContain("<script", script);
        Assert.Contains("&lt;script", script);

        // A hostile alt on a *valid* platform image: the <img> renders, but
        // the alt is escaped verbatim — no raw onerror=" attribute survives.
        var img = MarkdownRenderer.RenderHtml("![onerror=\"x\"](/content-image/ab)");
        Assert.Contains("<img", img);
        Assert.DoesNotContain("onerror=\"", img);
    }

    /// <summary>
    /// Counts non-overlapping occurrences of <paramref name="needle"/> in
    /// <paramref name="haystack"/> (ordinal).
    /// </summary>
    private static int Count(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }
}
