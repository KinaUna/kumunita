using System.Text;
using System.Text.RegularExpressions;
using Kumunita.Web.Security;

namespace Kumunita.Web.Tests;

/// <summary>
/// WY U03 — the lane's <b>pure-function tests</b> (design doc §2.7 items
/// 1–9, <b>exact names</b> — frozen). The tests anchor the contract of
/// the two pure functions in
/// <c>src/Kumunita.Web/client/lib/dom-to-markdown.ts</c> (the WY lane's
/// load-bearing artifact, ADR 0033 D2 + D3):
/// <see cref="WysiwygSpec.ToMarkdown"/> (the inverse of
/// <c>renderPreview</c>, the WY·3 ceiling) and
/// <see cref="WysiwygSpec.SanitizeHtml"/> (the DOM-shape normalizer,
/// WY·6). The artifacts 10–17 (the artifact-string pins) are authored
/// in U4–U7 and are <b>not</b> in this file (unit-series rule 3).
/// <para>
/// <b>Harness reality (WY·8 / RE·3 <c>tsc</c>-only + no TS test
/// runner):</b> the repo has no <c>vitest</c>/<c>jest</c>/<c>node
/// --test</c>; the <see cref="WysiwygSpec"/> mirror and the TS module
/// encode the <b>same</b> pinned contract from the design doc, so the
/// tests anchor the client behavior without a JS runner. The mirror is
/// the <b>executable spec</b>, not a second product renderer (RE·2's
/// "one renderer" stance refers to the <b>read</b> path —
/// <see cref="MarkdownRenderer"/> — which is untouched). The round-trip
/// property <c>toMarkdown(renderPreview(md)) === md</c> (WY·10 / WY5)
/// is the load-bearing invariant: the serializer is the exact inverse
/// of <c>renderPreview</c> for the pinned corpus (the adjacent-block,
/// no-blank-line shape — <c>renderPreview</c> is frozen, RE·2, and
/// collapses blank lines between blocks).
/// </para>
/// </summary>
public class WysiwygEditorTests
{
    // ── WY10 / WY5 — the round-trip property (the key invariant) ─────────

    /// <summary>
    /// <b>#1</b> — <c>toMarkdown(renderPreview(md)) === md</c> for a
    /// <c>md</c> exercising bold + a heading + a list + a link + an
    /// image + a code block (WY·3, WY·5). The image's
    /// <c>![alt](/content-image/{id})</c> form is also verified to be
    /// byte-picked-up by RC's <see cref="ContentImageIds"/> parse (RC
    /// R·3 — zero server change).
    /// </summary>
    [Fact]
    public void WY10_RoundTrip_BoldHeadingListLinkImageCode()
    {
        const string md =
            "# H\n" +
            "World **bold** *italic* `code`\n" +
            "- a\n- b\n" +
            "[t](https://example.com) ![fence](/content-image/deadbeef)\n" +
            "```js\nlet a = 1;\n```";
        var html = WysiwygSpec.RenderPreview(md);
        var back = WysiwygSpec.ToMarkdown(html);

        Assert.Equal(md, back);
        // RC R·3 byte-identity — the serializer's image form is the exact
        // shape ContentImageIds.FullSrcRe already parses (zero server
        // change).
        var ids = ContentImageIds.ExtractContentImageIds(back);
        Assert.Equal(new[] { "deadbeef" }, ids);
    }

    // ── WY3 — the serializer emits exactly the WY·3 subset ───────────────

    /// <summary>
    /// <b>#2</b> — <c>toMarkdown</c> emits only the WY·3 subset; a
    /// non-subset input (a <c>&lt;span&gt;</c> / a <c>&lt;table&gt;</c>)
    /// is rendered as plain text, <b>never</b> re-emitted as a tag
    /// (WY·3).
    /// </summary>
    [Fact]
    public void WY3_Serializer_EmitsOnlyThePinnedSubset()
    {
        // A <span> wrapper (a common paste shape) — the inner text
        // survives, the tag does not.
        var span = WysiwygSpec.ToMarkdown("<span style=\"color:red\">x</span>");
        Assert.Equal("x", span);
        Assert.DoesNotContain("<span", span);

        // A <table> (a non-subset tag) — the inner text survives, the
        // tag does not.
        var table = WysiwygSpec.ToMarkdown("<table><tr><td>x</td></tr></table>");
        Assert.Equal("x", table);
        Assert.DoesNotContain("<table", table);
        Assert.DoesNotContain("<td", table);

        // A heading (a WY·3 subset tag) — the tag IS re-emitted as its
        // Markdown form (the inverse of renderPreview).
        var h1 = WysiwygSpec.ToMarkdown("<h1>Heading</h1>");
        Assert.Equal("# Heading", h1);
    }

    /// <summary>
    /// <b>#3</b> — blank <c>&lt;p&gt;</c> / <c>&lt;h1&gt;</c> /
    /// <c>&lt;ul&gt;</c> are skipped (design doc §2.3 edge cases a/b/c).
    /// </summary>
    [Fact]
    public void WY3_Serializer_SkipsBlankElements()
    {
        Assert.Equal(string.Empty, WysiwygSpec.ToMarkdown("<p></p>"));
        Assert.Equal(string.Empty, WysiwygSpec.ToMarkdown("<h1></h1>"));
        Assert.Equal(string.Empty, WysiwygSpec.ToMarkdown("<ul></ul>"));
        Assert.Equal(string.Empty, WysiwygSpec.ToMarkdown("<ol></ol>"));
    }

    /// <summary>
    /// <b>#4</b> — an <c>&lt;img&gt;</c> with an unsafe <c>src</c> (the
    /// <c>isSafeImageSrc</c> reject) renders as plain escaped text —
    /// which, for an <c>&lt;img&gt;</c>, is the empty string (the
    /// <c>isSafeImageSrc</c> reject precedent; the
    /// <c>renderPreview</c> mirror renders the unsafe image as plain
    /// escaped text, and an <c>&lt;img&gt;</c> has no text to fall back
    /// on) (design doc §2.3 edge case e).
    /// </summary>
    [Fact]
    public void WY3_Serializer_RejectsUnsafeImageSrc()
    {
        Assert.Equal(string.Empty, WysiwygSpec.ToMarkdown(
            "<img src=\"javascript:alert(1)\" alt=\"x\"/>"));
        Assert.Equal(string.Empty, WysiwygSpec.ToMarkdown(
            "<img src=\"https://evil.example/i.png\" alt=\"x\"/>"));
    }

    /// <summary>
    /// <b>#5</b> — an <c>&lt;a&gt;</c> with an unsafe <c>href</c> (the
    /// <c>isSafeUrl</c> reject) renders the label as plain text (design
    /// doc §2.3 edge case f; the <c>isSafeUrl</c> reject precedent).
    /// </summary>
    [Fact]
    public void WY3_Serializer_RejectsUnsafeLinkHref()
    {
        Assert.Equal("label", WysiwygSpec.ToMarkdown(
            "<a href=\"javascript:alert(1)\">label</a>"));
        Assert.Equal("label", WysiwygSpec.ToMarkdown(
            "<a href=\"data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==\">label</a>"));
    }

    // ── WY5 — the saved body is byte-identical ────────────────────────────

    /// <summary>
    /// <b>#6</b> — the serialized body for a hand-built pane HTML is
    /// byte-identical to the Markdown a resident could hand-type (RC R·3
    /// — the <see cref="ContentImageIds"/> parse +
    /// <see cref="MarkdownRenderer"/> render are untouched).
    /// </summary>
    [Fact]
    public void WY5_SavedBodyIsByteIdentical()
    {
        // The exact pane HTML renderPreview emits for the pinned corpus.
        var paneHtml =
            "<h1>H</h1>" +
            "<p>World <strong>bold</strong></p>" +
            "<ul><li>a</li><li>b</li></ul>" +
            "<p><a href=\"https://example.com\">t</a> " +
            "<img src=\"/content-image/deadbeef\" alt=\"fence\" class=\"rc-image\" loading=\"lazy\" /></p>" +
            "<pre><code class=\"language-js\">let a = 1;</code></pre>";
        var expectedMd =
            "# H\n" +
            "World **bold**\n" +
            "- a\n- b\n" +
            "[t](https://example.com) ![fence](/content-image/deadbeef)\n" +
            "```js\nlet a = 1;\n```";

        var got = WysiwygSpec.ToMarkdown(paneHtml);
        Assert.Equal(expectedMd, got);

        // RC R·3 — the image form is the exact shape ContentImageIds
        // already parses (zero server change).
        var ids = ContentImageIds.ExtractContentImageIds(got);
        Assert.Equal(new[] { "deadbeef" }, ids);

        // RC R·1 — the read path (MarkdownRenderer) renders the saved
        // body back to the same pane HTML (the round-trip is the
        // closed-loop, pinned by WY10).
        var rendered = MarkdownRenderer.RenderHtml(got);
        Assert.Contains("src=\"/content-image/deadbeef\"", rendered);
        Assert.Contains("class=\"rc-image\"", rendered);
        Assert.Contains("<strong>bold</strong>", rendered);
    }

    // ── WY6 — the sanitizer strips disallowed elements/attrs/urls ─────────

    /// <summary>
    /// <b>#7</b> — <c>sanitizeHtml</c> strips every non-subset element
    /// (design doc §2.4 reject list — <c>&lt;div&gt;</c>,
    /// <c>&lt;span&gt;</c>, <c>&lt;table&gt;</c>, …), keeping the
    /// inner content.
    /// </summary>
    [Fact]
    public void WY6_Sanitizer_StripsDisallowedElements()
    {
        Assert.Equal("<p>keep</p>", WysiwygSpec.SanitizeHtml("<div><p>keep</p></div>"));
        Assert.Equal("x", WysiwygSpec.SanitizeHtml("<span style=\"color:red\">x</span>"));
        Assert.Equal("keep", WysiwygSpec.SanitizeHtml("<table><tr><td>keep</td></tr></table>"));
        Assert.Equal("<p>ok</p>", WysiwygSpec.SanitizeHtml("<script>alert(1)</script><p>ok</p>"));
    }

    /// <summary>
    /// <b>#8</b> — <c>sanitizeHtml</c> strips every <c>on*</c> handler,
    /// every <c>style</c>, every non-<c>language-{lang}</c>
    /// <c>class</c>, every <c>id</c> (design doc §2.4 reject list).
    /// </summary>
    [Fact]
    public void WY6_Sanitizer_StripsDisallowedAttributes()
    {
        // on* + style + id — all stripped.
        Assert.Equal("<p>t</p>", WysiwygSpec.SanitizeHtml(
            "<p onclick=\"evil()\" style=\"color:red\" id=\"x\">t</p>"));
        // class — stripped from <p> (not on the allowlist for <p>).
        Assert.Equal("<p>t</p>", WysiwygSpec.SanitizeHtml("<p class=\"custom\">t</p>"));
        // class="language-{lang}" — KEPT (the one WY·3-subset class).
        Assert.Equal(
            "<pre><code class=\"language-js\">c</code></pre>",
            WysiwygSpec.SanitizeHtml("<pre><code class=\"language-js\">c</code></pre>"));
        // id — stripped from <code> (not on the allowlist for <code>).
        Assert.Equal(
            "<code class=\"language-js\">c</code>",
            WysiwygSpec.SanitizeHtml("<code id=\"x\" class=\"language-js\">c</code>"));
    }

    /// <summary>
    /// <b>#9</b> — <c>sanitizeHtml</c> drops every unsafe <c>href</c>
    /// (the <c>isSafeUrl</c> reject — the <c>&lt;a&gt;</c> tag is
    /// dropped, the text is kept) + every unsafe <c>src</c> (the
    /// <c>isSafeImageSrc</c> reject — the <c>&lt;img&gt;</c> is dropped
    /// entirely, it has no text content to keep).
    /// </summary>
    [Fact]
    public void WY6_Sanitizer_StripsUnsafeHrefs()
    {
        // Unsafe href — the tag is dropped, the text survives.
        Assert.Equal("x", WysiwygSpec.SanitizeHtml(
            "<a href=\"javascript:alert(1)\">x</a>"));
        // Unsafe src — the <img> is dropped entirely.
        Assert.Equal(string.Empty, WysiwygSpec.SanitizeHtml(
            "<img src=\"javascript:alert(1)\" alt=\"x\"/>"));
        // Safe href + safe src — kept verbatim (the WY·3 subset).
        Assert.Equal("<a href=\"https://x\">y</a>", WysiwygSpec.SanitizeHtml(
            "<a href=\"https://x\">y</a>"));
        Assert.Equal(
            "<img src=\"/content-image/deadbeef\" alt=\"x\" />",
            WysiwygSpec.SanitizeHtml(
                "<img src=\"/content-image/deadbeef\" alt=\"x\" class=\"rc-image\" loading=\"lazy\" />"));
    }

    // ── WY U4 — artifact-string pins (§2.7 items 13–14, verbatim names) ──

    /// <summary>
    /// <b>#13</b> — the compiled <c>wwwroot/js/lib/rich-editor.js</c>
    /// contains <c>contentEditable</c> (WY·1 — the pane is the editing
    /// surface, set at runtime by the WY block's
    /// <c>previewPane.contentEditable = 'true'</c>). Proves the U4 WY
    /// block shipped in the compiled artifact.
    /// </summary>
    [Fact]
    public void CompiledRichEditorJs_ContainsContentEditable()
    {
        var content = ReadCompiledRichEditor();
        Assert.Contains("contentEditable", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>#14</b> — the compiled <c>wwwroot/js/lib/rich-editor.js</c>
    /// contains <c>toMarkdown</c> (WY·3 / WY·5 — the serializer import +
    /// the <c>input</c> handler's <c>textarea.value = toMarkdown(…)</c>
    /// call both land in the compiled artifact). Proves the U4 WY block's
    /// serializer wiring shipped.
    /// </summary>
    [Fact]
    public void CompiledRichEditorJs_ContainsToMarkdown()
    {
        var content = ReadCompiledRichEditor();
        Assert.Contains("toMarkdown", content, StringComparison.Ordinal);
    }

    // ── WY U5 — the toolbar rework artifact-string pins (WY·4 / WY·8) ────

    /// <summary>
    /// U5 — the compiled <c>wwwroot/js/lib/rich-editor.js</c> carries the
    /// <b>DOM-splice</b> toolbar wiring (WY·4 — the toolbar splices DOM,
    /// not Markdown, into the pane via the Selection / Range API). The
    /// <c>range.surroundContents</c> / <c>range.extractContents</c> /
    /// <c>range.insertNode</c> trio is the §2.5e per-button construction;
    /// their presence proves the U5 rework shipped (the pre-U5 loop
    /// spliced Markdown into the textarea and referenced none of these).
    /// The <c>export function {name}</c> RE surface is asserted verbatim
    /// by <c>CompiledRichEditorJs_StillExportsRePureFunctions</c>
    /// (<c>InlineEditorTests</c>) — untouched by U5.
    /// </summary>
    [Fact]
    public void CompiledRichEditorJs_ContainsDomSplice()
    {
        var content = ReadCompiledRichEditor();
        // The Selection / Range API splice trio (design doc §2.5e).
        Assert.Contains("surroundContents", content, StringComparison.Ordinal);
        Assert.Contains("extractContents", content, StringComparison.Ordinal);
        Assert.Contains("insertNode", content, StringComparison.Ordinal);
        // After every splice the binder keeps the textarea in sync (WY·2).
        Assert.Contains("toMarkdown", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>#11</b> — <c>package.json</c> is still <c>typescript</c>-only
    /// (WY·8 — the <c>tsc</c>-only / no-editor-dependency constraint stands
    /// unchanged). The U5 toolbar rework splices DOM via the browser's
    /// Selection / Range API (built-in) and adds <b>no</b> editor package.
    /// The needle is the <c>typescript</c> devDependency plus the
    /// <b>absence</b> of a known editor package name.
    /// </summary>
    [Fact]
    public void WY8_TscOnly_NoEditorDependency()
    {
        var pkg = ReadPackageJson();
        // The tsc-only toolchain is the sole dependency (RE·3 / WY·8).
        Assert.Contains("typescript", pkg, StringComparison.Ordinal);
        // No known editor / rich-text package name appears anywhere in the
        // manifest (a WY·8 / RE·3 regression — a WY-2 lane that adopts an
        // editor dependency would fail here on purpose).
        foreach (var name in new[]
            {
                "quill", "prosemirror", "prose-mirror", "tiptap",
                "slate", "codemirror", "CodeMirror", "tinymce",
                "ckeditor", "froala", "lexical",
            })
        {
            Assert.DoesNotContain(name, pkg, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── WY U7 — the code view rework (WY·7) ───────────────────────────────

    /// <summary>
    /// <b>#10</b> — the compiled <c>wwwroot/js/lib/rich-editor.js</c>
    /// sets <c>textarea.readOnly</c> (WY·7 — the code view is a
    /// <b>read-only</b> mirror of the pane; the resident never types into
    /// the sink — WY·2: the textarea stays the live form field the server
    /// binds on submit, never disabled / removed / re-shaped, RC R·3 /
    /// RE·1 / IE·1) <b>and</b> still carries the
    /// <c>rc-editor-source-hidden</c> class toggle (the IE·1 frozen base
    /// — the textarea is revealed / hidden by that CSS class, unchanged
    /// from IE). The two needles together prove the U7 <c>setView</c>
    /// rework shipped (the pre-U7 block toggled only the class — an
    /// editable source — and referenced no <c>readOnly</c>).
    /// </summary>
    [Fact]
    public void WY7_CodeViewIsReadOnlyMirror()
    {
        var content = ReadCompiledRichEditor();
        // WY·7 / WY·2 — the textarea is the read-only sink (the binder
        // sets `textarea.readOnly = true` in the U7 setView block).
        Assert.Contains("readOnly", content, StringComparison.Ordinal);
        // IE·1 frozen base — the source-hidden class is still the
        // reveal/hide mechanism (the code view is a mirror, not a mode
        // switch — WY·1: the pane stays editable + visible in both states).
        Assert.Contains("rc-editor-source-hidden", content, StringComparison.Ordinal);
    }

    // ── WY·9 — the a11y artifact-string pin (§2.7 #12) ───────────────────

    /// <summary>
    /// <b>#12</b> — the compiled <c>wwwroot/js/lib/rich-editor.js</c> sets
    /// <c>role="textbox"</c> + <c>aria-multiline</c> on the pane (WY·9 — the
    /// pane is keyboard-operable and announced as a multiline textbox; the
    /// binder sets both at runtime, never in the Razor — design doc §2.5(a)).
    /// Closes the §2.7 pin-list drift for this name (U8 recorded it as
    /// absent / indirectly covered).
    /// </summary>
    [Fact]
    public void WY9_PaneIsKeyboardOperable()
    {
        var content = ReadCompiledRichEditor();
        // WY·9 — the a11y label pair is set by the WY block at runtime
        // (the exact `setAttribute` calls the design doc §2.5(a) pins).
        Assert.Contains(
            "setAttribute('role', 'textbox')", content, StringComparison.Ordinal);
        Assert.Contains(
            "setAttribute('aria-multiline', '')", content, StringComparison.Ordinal);
    }

    // ── Test helpers (mirrors <c>InlineEditorTests</c> idiom) ────────────

    /// <summary>
    /// Resolve the compiled <c>rich-editor.js</c> artifact and read it,
    /// failing loud if it is absent.
    /// </summary>
    private static string ReadCompiledRichEditor()
    {
        var candidates = CandidateArtifactPaths();
        var artifact = candidates.FirstOrDefault(p => File.Exists(p));
        Assert.True(
            artifact is not null,
            $"rich-editor.js build artifact not found; searched:\n{string.Join("\n", candidates)}");
        return File.ReadAllText(artifact);
    }

    /// <summary>
    /// Candidate paths to the compiled client artifact (walk up ≤ 8 levels
    /// from both CWD and <see cref="AppContext.BaseDirectory"/>).
    /// </summary>
    private static IReadOnlyList<string> CandidateArtifactPaths()
    {
        const string rel = "src/Kumunita.Web/wwwroot/js/lib/rich-editor.js";
        var candidates = new List<string>();
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(start);
            for (int i = 0; i < 8 && dir != null; i++)
            {
                candidates.Add(Path.Combine(dir.FullName, rel));
                dir = dir.Parent;
            }
        }
        return candidates.Distinct().ToArray();
    }

    /// <summary>
    /// Resolve <c>src/Kumunita.Web/package.json</c> on disk and read it,
    /// failing loud if it is absent (the <c>ReadCompiledRichEditor</c>
    /// idiom — the same walk-up ≤ 8 levels from CWD +
    /// <see cref="AppContext.BaseDirectory"/>).
    /// </summary>
    private static string ReadPackageJson()
    {
        const string rel = "src/Kumunita.Web/package.json";
        var candidates = new List<string>();
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(start);
            for (int i = 0; i < 8 && dir != null; i++)
            {
                candidates.Add(Path.Combine(dir.FullName, rel));
                dir = dir.Parent;
            }
        }
        candidates = candidates.Distinct().ToList();
        var artifact = candidates.FirstOrDefault(p => File.Exists(p));
        Assert.True(
            artifact is not null,
            $"package.json not found; searched:\n{string.Join("\n", candidates)}");
        return File.ReadAllText(artifact);
    }
}

/// <summary>
/// A <b>small internal static C# spec mirror</b> of the two pure
/// functions in <c>src/Kumunita.Web/client/lib/dom-to-markdown.ts</c>
/// (WY U03). It <b>verbatim-encodes</b> the design doc's pinned
/// behavior — the same WY·3-subset tag allowlist, the same
/// attribute allowlist, the same <c>isSafeUrl</c>/<c>isSafeImageSrc</c>
/// mirrors, the same inline-sibling convention (text nodes trimmed;
/// siblings joined by a single space), and the same block-join
/// convention (<c>\n</c> between blocks). It is the <b>executable
/// spec</b>, not a second product renderer: the only shipped
/// serializer is the TS <c>toMarkdown</c>, and the only shipped read
/// path is <see cref="MarkdownRenderer"/> (both frozen, RC R·1).
/// </summary>
internal static class WysiwygSpec
{
    // ── Pure functions (the mirror of the TS module's exports) ───────────

    public static string ToMarkdown(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var root = Parse(html);
        var s = SerializeNode(root);
        return s ?? string.Empty;
    }

    public static string SanitizeHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        var root = Parse(html);
        return SanitizeNode(root);
    }

    // ── The WY·3 tag allowlists + the attribute allowlist ─────────────────

    private static readonly HashSet<string> BlockTags = new()
    { "p", "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "pre" };

    private static readonly HashSet<string> SanitizeSubset = new()
    {
        "p", "h1", "h2", "h3", "h4", "h5", "h6",
        "ul", "ol", "li",
        "strong", "em", "code",
        "a", "img",
        "pre",
    };

    private static readonly HashSet<string> SanitizeDropContent = new()
    {
        "script", "style", "noscript", "template", "iframe", "object",
        "embed", "form", "input", "button", "select", "textarea",
        "video", "audio", "canvas", "source", "track", "base", "link", "meta",
    };

    private static readonly HashSet<string> VoidTags = new()
    { "br", "img", "hr", "input", "meta", "link" };

    private static readonly Regex TagRe =
        new(@"<\/?([a-zA-Z][a-zA-Z0-9-]*)((?:\s+[^\u003c\u003e]*?)?)(\/?)>", RegexOptions.Compiled);

    private static readonly Regex AttrRe =
        new(@"([a-zA-Z][a-zA-Z0-9_-]*)\s*=\s*""([^""]*)""|([a-zA-Z][a-zA-Z0-9_-]*)", RegexOptions.Compiled);

    // ── A minimal HTML tree (mirror of the TS parser) ─────────────────────

    private abstract class HtmlNode { }
    private sealed class HtmlTextNode : HtmlNode { public string Text { get; init; } = ""; }
    private sealed class HtmlElementNode : HtmlNode
    {
        public string Tag { get; init; } = "";
        public Dictionary<string, string> Attrs { get; init; } = new(StringComparer.Ordinal);
        public List<HtmlNode> Children { get; init; } = new();
    }

    private sealed record Token
    {
        public bool IsText { get; init; }
        public string Text { get; init; } = "";
        public bool IsEnd { get; init; }
        public string Tag { get; init; } = "";
        public bool IsElement { get; init; }
        public bool SelfClosing { get; init; }
        public Dictionary<string, string> Attrs { get; init; } = new(StringComparer.Ordinal);
    }

    private static List<Token> Tokenize(string html)
    {
        var tokens = new List<Token>();
        var last = 0;
        foreach (Match m in TagRe.Matches(html))
        {
            if (m.Index > last)
            {
                var t = html.Substring(last, m.Index - last);
                if (t.Length > 0) tokens.Add(new Token { IsText = true, Text = t });
            }
            var isClose = m.Value[1] == '/';
            var tag = m.Groups[1].Value.ToLowerInvariant();
            if (isClose)
            {
                tokens.Add(new Token { IsEnd = true, Tag = tag });
            }
            else
            {
                var selfClosing = m.Groups[3].Value == "/" || VoidTags.Contains(tag);
                var attrs = new Dictionary<string, string>(StringComparer.Ordinal);
                var attrStr = (m.Groups[2].Value ?? "").Trim();
                if (attrStr.Length > 0)
                {
                    foreach (Match am in AttrRe.Matches(attrStr))
                    {
                        if (am.Groups[1].Success)
                        {
                            attrs[am.Groups[1].Value.ToLowerInvariant()] = am.Groups[2].Value;
                        }
                        else if (am.Groups[3].Success)
                        {
                            attrs[am.Groups[3].Value.ToLowerInvariant()] = "";
                        }
                    }
                }
                tokens.Add(new Token { IsElement = true, Tag = tag, SelfClosing = selfClosing, Attrs = attrs });
            }
            last = m.Index + m.Length;
        }
        if (last < html.Length)
        {
            var t = html.Substring(last);
            if (t.Length > 0) tokens.Add(new Token { IsText = true, Text = t });
        }
        return tokens;
    }

    private static HtmlElementNode Parse(string html)
    {
        var root = new HtmlElementNode { Tag = "root", Children = new List<HtmlNode>() };
        var stack = new List<HtmlElementNode> { root };
        foreach (var tok in Tokenize(html))
        {
            if (tok.IsEnd)
            {
                for (var i = stack.Count - 1; i > 0; i--)
                {
                    if (stack[i].Tag == tok.Tag) { stack.RemoveRange(i, stack.Count - i); break; }
                }
                continue;
            }
            if (tok.IsText)
            {
                stack[stack.Count - 1].Children.Add(new HtmlTextNode { Text = tok.Text });
                continue;
            }
            var node = new HtmlElementNode
            {
                Tag = tok.Tag,
                Attrs = tok.Attrs,
                Children = new List<HtmlNode>(),
            };
            stack[stack.Count - 1].Children.Add(node);
            if (!tok.SelfClosing) stack.Add(node);
        }
        return root;
    }

    // ── Inline serializer (the inverse of renderPreview's inline) ─────────

    private static string InlineChildren(HtmlElementNode node)
    {
        var sb = new StringBuilder();
        var first = true;
        void Push(string s)
        {
            if (s.Length == 0) return;
            if (!first) sb.Append(' ');
            sb.Append(s);
            first = false;
        }
        foreach (var c in node.Children)
        {
            if (c is HtmlTextNode tn)
            {
                Push(UnescapeHtml(tn.Text).Trim());
                continue;
            }
            var e = (HtmlElementNode)c;
            switch (e.Tag)
            {
                case "strong" or "b":
                    Push($"**{InlineChildren(e)}**");
                    break;
                case "em" or "i":
                    Push($"*{InlineChildren(e)}*");
                    break;
                case "code":
                    Push($"`{InlineChildren(e)}`");
                    break;
                case "a":
                    Push(SerializeLink(e));
                    break;
                case "img":
                    Push(SerializeImage(e));
                    break;
                case "br":
                    Push(" ");
                    break;
                default:
                    // Non-subset inline tag — serialize the content, never
                    // the tag (WY·3).
                    Push(InlineChildren(e));
                    break;
            }
        }
        return sb.ToString();
    }

    private static string SerializeLink(HtmlElementNode a)
    {
        var inline = InlineChildren(a);
        var href = a.Attrs.TryGetValue("href", out var h) ? h : null;
        if (href is not null && href.Length > 0 && IsSafeUrl(UnescapeHtml(href)))
        {
            return $"[{inline}]({UnescapeHtml(href)})";
        }
        return inline; // (d)/(f)
    }

    private static string SerializeImage(HtmlElementNode img)
    {
        if (!img.Attrs.TryGetValue("src", out var src)) return string.Empty;
        var alt = img.Attrs.TryGetValue("alt", out var a) ? a : "";
        if (IsSafeImageSrc(UnescapeHtml(src)))
        {
            return $"![{UnescapeHtml(alt)}]({UnescapeHtml(src)})";
        }
        return string.Empty; // (e)
    }

    private static string? SerializeCodeBlock(HtmlElementNode pre)
    {
        HtmlElementNode? code = null;
        foreach (var c in pre.Children)
        {
            if (c is HtmlElementNode e && e.Tag == "code") { code = e; break; }
        }
        if (code is null) return null;
        var langAttr = code.Attrs.TryGetValue("class", out var cl) ? cl : "";
        var m = Regex.Match(langAttr, @"^language-(.+)$");
        var lang = m.Success ? UnescapeHtml(m.Groups[1].Value) : "";
        var codeText = InlineChildren(code);
        var fence = lang.Length > 0 ? "```" + lang : "```";
        return $"{fence}\n{codeText}\n```";
    }

    private static string? SerializeList(HtmlElementNode list)
    {
        var items = new List<string>();
        var n = 0;
        foreach (var c in list.Children)
        {
            if (c is not HtmlElementNode e || e.Tag != "li") continue;
            var inline = InlineChildren(e);
            if (inline.Length == 0) continue;
            n++;
            items.Add(list.Tag == "ol" ? $"{n}. {inline}" : $"- {inline}");
        }
        if (items.Count == 0) return null; // (c)
        return string.Join("\n", items);
    }

    private static string? SerializeNode(HtmlElementNode node)
    {
        var tag = node.Tag;
        if (tag is "root" or "div" or "span" or "body" or "html")
        {
            var parts = new List<string>();
            foreach (var c in node.Children)
            {
                if (c is HtmlTextNode tn)
                {
                    var t = UnescapeHtml(tn.Text).Trim();
                    if (t.Length > 0) parts.Add(t);
                    continue;
                }
                var s = SerializeNode((HtmlElementNode)c);
                if (s is not null && s.Length > 0) parts.Add(s);
            }
            return parts.Count > 0 ? string.Join("\n", parts) : null;
        }
        if (tag == "p")
        {
            var inline = InlineChildren(node);
            return inline.Length > 0 ? inline : null; // (a)
        }
        if (tag.Length == 2 && tag[0] == 'h' && tag[1] is >= '1' and <= '6')
        {
            var inline = InlineChildren(node);
            if (inline.Length == 0) return null; // (b)
            var level = tag[1] - '0';
            return new string('#', level) + " " + inline;
        }
        if (tag is "ul" or "ol") return SerializeList(node);
        if (tag == "li")
        {
            var inline = InlineChildren(node);
            return inline.Length > 0 ? inline : null;
        }
        if (tag == "pre") return SerializeCodeBlock(node);
        if (tag is "strong" or "b")
        {
            var inline = InlineChildren(node);
            return inline.Length > 0 ? $"**{inline}**" : null;
        }
        if (tag is "em" or "i")
        {
            var inline = InlineChildren(node);
            return inline.Length > 0 ? $"*{inline}*" : null;
        }
        if (tag == "code")
        {
            var inline = InlineChildren(node);
            return inline.Length > 0 ? $"`{inline}`" : null;
        }
        if (tag == "a")
        {
            var s = SerializeLink(node);
            return s.Length > 0 ? s : null;
        }
        if (tag == "img")
        {
            var s = SerializeImage(node);
            return s.Length > 0 ? s : null;
        }
        // Non-subset tag — serialize the inline content, never the tag
        // (WY·3).
        var fallback = InlineChildren(node);
        return fallback.Length > 0 ? fallback : null;
    }

    // ── Sanitizer (mirror of the TS sanitizeHtml) ─────────────────────────

    private static string SanitizeNode(HtmlNode node)
    {
        if (node is HtmlTextNode tn) return tn.Text;
        var e = (HtmlElementNode)node;
        var tag = e.Tag;
        if (tag is "root" or "body" or "html" or "div" or "span")
        {
            var sb = new StringBuilder();
            foreach (var c in e.Children) sb.Append(SanitizeNode(c));
            return sb.ToString();
        }
        if (SanitizeDropContent.Contains(tag)) return string.Empty;
        if (!SanitizeSubset.Contains(tag))
        {
            var sb = new StringBuilder();
            foreach (var c in e.Children) sb.Append(SanitizeNode(c));
            return sb.ToString();
        }
        if (tag == "img")
        {
            if (!e.Attrs.TryGetValue("src", out var src) || !IsSafeImageSrc(UnescapeHtml(src)))
                return string.Empty;
            var alt = e.Attrs.TryGetValue("alt", out var a) ? a : "";
            return $"<img src=\"{EscAttr(src)}\" alt=\"{EscAttr(alt)}\" />";
        }
        if (tag == "a")
        {
            var inner = string.Concat(e.Children.Select(SanitizeNode));
            if (!e.Attrs.TryGetValue("href", out var href) || !IsSafeUrl(UnescapeHtml(href)))
                return inner;
            return $"<a href=\"{EscAttr(href)}\">{inner}</a>";
        }
        if (tag == "pre")
            return $"<pre>{string.Concat(e.Children.Select(SanitizeNode))}</pre>";
        if (tag == "code")
        {
            var cls = e.Attrs.TryGetValue("class", out var c) ? c : null;
            var clsOk = cls is not null && Regex.IsMatch(cls, @"^language-[a-z0-9_-]+$", RegexOptions.IgnoreCase);
            var inner = string.Concat(e.Children.Select(SanitizeNode));
            return clsOk
                ? $"<code class=\"{EscAttr(cls)}\">{inner}</code>"
                : $"<code>{inner}</code>";
        }
        var finalInner = string.Concat(e.Children.Select(SanitizeNode));
        return $"<{tag}>{finalInner}</{tag}>";
    }

    private static string EscAttr(string s)
        => s.Replace("&", "&amp;").Replace("\"", "&quot;");

    // ── Pure predicates + escape helpers ──────────────────────────────────

    private static string UnescapeHtml(string s)
        => s.Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&quot;", "\"")
            .Replace("&#39;", "'");

    private static bool IsSafeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!url.Contains("://") && !url.StartsWith("//"))
            return !url.Contains(':');
        var scheme = url.Split("://")[0].Trim().ToLowerInvariant();
        return scheme == "http" || scheme == "https" || scheme == "mailto";
    }

    private static bool IsSafeImageSrc(string src)
    {
        if (string.IsNullOrEmpty(src)) return false;
        const string route = "/content-image/";
        if (src.StartsWith(route))
        {
            var id = src.Substring(route.Length);
            return id.Length >= 1 && id.Length <= 128 && Regex.IsMatch(id, @"^[0-9a-f]+$");
        }
        return !src.Contains(':') && !src.StartsWith("//") && !Regex.IsMatch(src, @"\s");
    }

    // ── The renderPreview mirror (verbatim copy of RichEditorSpec.RenderPreview) ─

    private static readonly Regex RefRe =
        new(@"(!?)\[([^\]]*)\]\(([^)\s]+)\)", RegexOptions.Compiled);

    public static string RenderPreview(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var sb = new StringBuilder();
        int i = 0;

        while (i < lines.Length)
        {
            var trimmed = lines[i].TrimStart(' ', '\t');

            if (trimmed.StartsWith("```"))
            {
                var lang = trimmed.Substring(3).Trim();
                i++;
                var code = string.Empty;
                while (i < lines.Length && !lines[i].TrimStart(' ', '\t').StartsWith("```"))
                {
                    if (code.Length > 0) code += "\n";
                    code += HtmlEscape(lines[i]);
                    i++;
                }
                if (i < lines.Length && lines[i].TrimStart(' ', '\t').StartsWith("```"))
                    i++;
                var langAttr = lang.Length > 0 ? $" class=\"language-{HtmlEscape(lang)}\"" : string.Empty;
                sb.Append($"<pre><code{langAttr}>{code}</code></pre>");
                continue;
            }

            var heading = MatchHeading(trimmed);
            if (heading is not null)
            {
                sb.Append($"<h{heading.Value.level}>{Inline(heading.Value.content)}</h{heading.Value.level}>");
                i++;
                continue;
            }

            if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
            {
                sb.Append("<ul>");
                while (i < lines.Length)
                {
                    var t = lines[i].TrimStart(' ', '\t');
                    if (!t.StartsWith("- ") && !t.StartsWith("* ")) break;
                    sb.Append($"<li>{Inline(t.Substring(2))}</li>");
                    i++;
                }
                sb.Append("</ul>");
                continue;
            }

            if (Regex.IsMatch(trimmed, @"^\d+\. "))
            {
                sb.Append("<ol>");
                while (i < lines.Length)
                {
                    var t = lines[i].TrimStart(' ', '\t');
                    if (!Regex.IsMatch(t, @"^\d+\. ")) break;
                    var dot = t.IndexOf('.');
                    sb.Append($"<li>{Inline(t.Substring(dot + 1).TrimStart(' ', '\t'))}</li>");
                    i++;
                }
                sb.Append("</ol>");
                continue;
            }

            if (trimmed.Length == 0) { i++; continue; }

            var para = string.Empty;
            while (i < lines.Length)
            {
                var t = lines[i].TrimStart(' ', '\t');
                if (t.Length == 0 || t.StartsWith("```") || MatchHeading(t) is not null
                    || t.StartsWith("- ") || t.StartsWith("* ") || Regex.IsMatch(t, @"^\d+\. "))
                    break;
                if (para.Length > 0) para += ' ';
                para += t;
                i++;
            }
            sb.Append($"<p>{Inline(para)}</p>");
        }

        return sb.ToString();
    }

    private static string Inline(string input)
    {
        var sb = new StringBuilder();
        var lastEnd = 0;
        foreach (Match m in RefRe.Matches(input))
        {
            if (m.Index > lastEnd)
                sb.Append(InlineText(input.Substring(lastEnd, m.Index - lastEnd)));
            var isImage = m.Groups[1].Value == "!";
            var label = m.Groups[2].Value;
            var target = m.Groups[3].Value;
            if (isImage)
            {
                if (IsSafeImageSrc(target))
                {
                    var escSrc = target.Replace("&", "&amp;").Replace("\"", "&quot;");
                    sb.Append($"<img src=\"{escSrc}\" alt=\"{HtmlEscape(label)}\" class=\"rc-image\" loading=\"lazy\" />");
                }
                else
                {
                    sb.Append(InlineText($"![{label}]({target})"));
                }
            }
            else
            {
                var escLabel = HtmlEscape(label);
                if (IsSafeUrl(target))
                {
                    var escUrl = target.Replace("&", "&amp;").Replace("\"", "&quot;");
                    sb.Append($"<a href=\"{escUrl}\">{escLabel}</a>");
                }
                else
                {
                    sb.Append(escLabel);
                }
            }
            lastEnd = m.Index + m.Length;
        }
        if (lastEnd < input.Length)
            sb.Append(InlineText(input.Substring(lastEnd)));
        return sb.ToString();
    }

    private static string InlineText(string text)
    {
        var s = HtmlEscape(text);
        s = Regex.Replace(s, @"`([^`]+)`", "<code>$1</code>");
        s = Regex.Replace(s, @"\*\*([^*]+)\*\*", "<strong>$1</strong>");
        s = Regex.Replace(s, @"\*([^*]+)\*", "<em>$1</em>");
        return s;
    }

    private static (int level, string content)? MatchHeading(string line)
    {
        var level = 0;
        while (level < line.Length && level < 6 && line[level] == '#')
            level++;
        if (level == 0 || level > 6) return null;
        if (level < line.Length && line[level] != ' ') return null;
        var content = line.Substring(level).TrimStart(' ', '\t');
        if (content.Length == 0) return null;
        return (level, content);
    }

    private static string HtmlEscape(string s)
        => s.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
}
