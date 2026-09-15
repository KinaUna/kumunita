using System.Text.RegularExpressions;
using Kumunita.Web.Security;

namespace Kumunita.Web.Tests;

/// <summary>
/// RE U07 — the lane's <b>acceptance gate</b>. The 10 pinned RE behaviors
/// (design doc §Pinned seam tests, <b>exact names</b>) are anchored here as a
/// small <b>C# spec mirror</b> of the pure functions in
/// <c>src/Kumunita.Web/client/lib/rich-editor.ts</c> (U03), plus one
/// <b>artifact pin</b> on the compiled <c>wwwroot/js/lib/rich-editor.js</c>.
/// <para>
/// <b>Harness reality (RE·3's <c>tsc</c>-only + no TS test runner):</b> the repo
/// has no <c>vitest</c>/<c>jest</c>/<c>node --test</c>; this file's
/// <see cref="RichEditorSpec"/> mirror and the TS module encode the
/// <b>same</b> pinned contract from the design doc, so the tests anchor the
/// client behavior without a JS runner. The mirror is the <b>executable
/// spec</b>, not a second product renderer (RE·2's "one renderer" stance
/// refers to the <b>read</b> path — <see cref="MarkdownRenderer"/> — which is
/// untouched; the preview is still U03's TS <c>renderPreview</c>).
/// </para>
/// <para>
/// <b>Drift note (recorded 2026-09-15, U07):</b> the design doc pins
/// <see cref="ApplyToggle_Bold_WrapsSelection_AndPreservesCaret"/> with the
/// selection <c>[7,12]</c>, but the frozen module (and the RE2 FACES'
/// "selection preserved" intent) produce <c>[8,13]</c> — the arithmetically
/// correct selection of <c>world</c> in <c>"hello **world**"</c>. The
/// <c>[7,12]</c> doc value is an off-by-one typo; this test asserts the
/// module-faithful <c>[8,13]</c> (the module is the reference of record, RE·2).
/// See <c>## U7 — Drift pause</c> in the handoff note.
/// </para>
/// </summary>
public class RichEditorTests
{
    // ── RenderPreview parity (RE·2) ────────────────────────────────────────

    /// <summary>
    /// <b>#1</b> — <c>**b** *i* `c`</c> renders <c>&lt;strong&gt;</c>,
    /// <c>&lt;em&gt;</c>, <c>&lt;code&gt;</c> (the RC subset parity, RE·2).
    /// </summary>
    [Fact]
    public void RenderPreview_BoldItalicCode_StillRender()
    {
        var output = RichEditorSpec.RenderPreview("**b** *i* `c`");

        Assert.Contains("<strong>", output);
        Assert.Contains("<em>", output);
        Assert.Contains("<code>", output);
    }

    /// <summary>
    /// <b>#2</b> — <c># H</c>, <c>- a</c>, <c>1. b</c> render <c>&lt;h1&gt;</c>,
    /// <c>&lt;ul&gt;&lt;li&gt;</c>, <c>&lt;ol&gt;&lt;li&gt;</c> (parity).
    /// </summary>
    [Fact]
    public void RenderPreview_HeadingsLists_StillRender()
    {
        var output = RichEditorSpec.RenderPreview("# H\n- a\n1. b");

        Assert.Contains("<h1>", output);
        Assert.Contains("<ul><li>", output);
        Assert.Contains("<ol><li>", output);
    }

    /// <summary>
    /// <b>#3</b> — <c>![x](/content-image/deadbeef)</c> renders exactly one
    /// <c>&lt;img class="rc-image"&gt;</c> (parity + the <c>src</c> allowlist
    /// accept, the client <c>isSafeImageSrc</c> mirror).
    /// </summary>
    [Fact]
    public void RenderPreview_ImagePlatformRoute_RendersImgTag()
    {
        var output = RichEditorSpec.RenderPreview("![x](/content-image/deadbeef)");

        Assert.Equal(1, CountOccurrences(output, "<img"));
        Assert.Contains("class=\"rc-image\"", output);
        Assert.Contains("src=\"/content-image/deadbeef\"", output);
    }

    /// <summary>
    /// <b>#4</b> — a remote <c>![x](https://evil/i.png)</c> renders as escaped
    /// plain text: <b>no</b> <c>&lt;img&gt;</c> (the client
    /// <c>isSafeImageSrc</c> mirror rejects, RE·2 — mirrors RC R·2).
    /// </summary>
    [Fact]
    public void RenderPreview_ImageRemoteSrc_RendersAsPlainText()
    {
        var output = RichEditorSpec.RenderPreview("![x](https://evil/i.png)");

        Assert.DoesNotContain("<img", output);
        Assert.DoesNotContain("src=", output);
        Assert.Contains("https://evil/i.png", output);
    }

    /// <summary>
    /// <b>#5</b> — hostile <c>&lt;script&gt;</c> markup is entity-escaped; no
    /// raw tag survives (client R·2, the same escape-first construction as
    /// <see cref="MarkdownRenderer"/>).
    /// </summary>
    [Fact]
    public void RenderPreview_HostileMarkup_StillEscaped()
    {
        var output = RichEditorSpec.RenderPreview("<script>alert(1)</script>");

        Assert.DoesNotContain("<script", output);
        Assert.Contains("&lt;script", output);
    }

    // ── Toolbar splices (RE·1) ─────────────────────────────────────────────

    /// <summary>
    /// <b>#6</b> — <c>applyToggle("hello world", (6,11), "bold")</c> wraps the
    /// selection in <c>**</c> → <c>"hello **world**"</c> with the selection
    /// preserved on the content → <c>(8,13)</c>. (The RE1/RE2 FACES; the toggle
    /// is a pure, caret-preserving splice. See the class doc's drift note on
    /// the design doc's <c>[7,12]</c> typo.)
    /// </summary>
    [Fact]
    public void ApplyToggle_Bold_WrapsSelection_AndPreservesCaret()
    {
        var (text, sel) = RichEditorSpec.ApplyToggle("hello world", (6, 11), "bold");

        Assert.Equal("hello **world**", text);
        Assert.Equal((8, 13), sel);
    }

    /// <summary>
    /// <b>#7</b> — selecting an already-bolded word and toggling <b>removes</b>
    /// the markers (the RE2 "clicking B again toggles it back" FACES).
    /// </summary>
    [Fact]
    public void ApplyToggle_Bold_TogglesOff_WhenAlreadyBold()
    {
        var (text, sel) = RichEditorSpec.ApplyToggle("**world**", (0, 9), "bold");

        Assert.Equal("world", text);
        Assert.Equal((0, 5), sel);
    }

    /// <summary>
    /// <b>#8</b> — <c>applyBlock("", 0, "h1")</c> inserts the marker + a
    /// trailing space at the caret's line → <c>"# "</c> (a block kind inserts a
    /// line marker at the caret; the new caret sits right after it).
    /// </summary>
    [Fact]
    public void ApplyBlock_H1_InsertsHeading_AtCaret()
    {
        var (text, caret) = RichEditorSpec.ApplyBlock("", 0, "h1");

        Assert.Equal("# ", text);
        Assert.Equal(2, caret);
    }

    /// <summary>
    /// <b>#9</b> — <c>applyLink("hello", (0,5), "https://x")</c> wraps the
    /// selection → <c>"[hello](https://x)"</c> (the RE3 link FACES).
    /// </summary>
    [Fact]
    public void ApplyLink_WrapsSelection_WithUrl()
    {
        var (text, sel) = RichEditorSpec.ApplyLink("hello", (0, 5), "https://x");

        Assert.Equal("[hello](https://x)", text);
        Assert.Equal((0, 5), sel);
    }

    // ── RC R·3 byte-identity ───────────────────────────────────────────────

    /// <summary>
    /// <b>#10</b> — <c>imageLink("fence","deadbeef")</c> is byte-identical to
    /// <c>"![fence](/content-image/deadbeef)"</c>, and RC's server-side
    /// <see cref="ContentImageIds.ExtractContentImageIds"/> picks the id up
    /// unchanged (RC R·3 — zero server change).
    /// </summary>
    [Fact]
    public void ImageLink_Is_RcByteIdentical()
    {
        var link = RichEditorSpec.ImageLink("fence", "deadbeef");

        Assert.Equal("![fence](/content-image/deadbeef)", link);

        // The RC parse (the RC R·3 enforcement point) is unchanged by the toolbar.
        var ids = ContentImageIds.ExtractContentImageIds(link);
        Assert.Equal(new[] { "deadbeef" }, ids);
    }

    // ── Artifact pin (RE·3: the client artifact is built + exported) ───────

    /// <summary>
    /// <b>Artifact pin</b> — the <c>tsc</c> build output
    /// <c>wwwroot/js/lib/rich-editor.js</c> exists on disk and exports the
    /// pinned surface (<c>renderPreview</c> / <c>applyToggle</c> /
    /// <c>applyBlock</c> / <c>applyLink</c> / <c>imageLink</c> /
    /// <c>isSafeImageSrc</c> / <c>bindRichEditor</c>). Fails loud if the
    /// artifact is absent (do not soften to a try/catch).
    /// </summary>
    [Fact]
    public void CompiledRichEditorJs_Exists_And_Exports()
    {
        var candidates = CandidateArtifactPaths();
        var artifact = candidates.FirstOrDefault(p => File.Exists(p));
        Assert.True(
            artifact is not null,
            $"rich-editor.js build artifact not found; searched:\n{string.Join("\n", candidates)}");

        var content = File.ReadAllText(artifact);
        foreach (var name in new[]
            {
                "renderPreview", "applyToggle", "applyBlock",
                "applyLink", "imageLink", "isSafeImageSrc", "bindRichEditor",
            })
        {
            Assert.Contains($"export function {name}", content, StringComparison.Ordinal);
        }
    }

    // ── Test helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Non-overlapping ordinal occurrences of <paramref name="needle"/> in
    /// <paramref name="haystack"/> (the <c>MarkdownRendererTests.Count</c>
    /// convention).
    /// </summary>
    private static int CountOccurrences(string haystack, string needle)
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

    /// <summary>
    /// Candidate paths to the compiled client artifact, resolved from both the
    /// current working directory and <see cref="AppContext.BaseDirectory"/>,
    /// walking up the directory tree (the bin layout / invocation CWD vary).
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
}

/// <summary>
/// A <b>small internal static C# spec mirror</b> of the pure functions in
/// <c>src/Kumunita.Web/client/lib/rich-editor.ts</c> (U03). It
/// <b>verbatim-encodes</b> the design doc's pinned behavior — the same
/// escape-first construction, the same single-pass image/link extraction,
/// the same <c>isSafeImageSrc</c>/<c>isSafeUrl</c> mirrors, and the same
/// caret/selection math. It is the <b>executable spec</b>, not a second
/// product renderer: the only shipped preview is the TS
/// <c>renderPreview</c>, and the only shipped read path is
/// <see cref="MarkdownRenderer"/> (both frozen, RC R·1).
/// </summary>
internal static class RichEditorSpec
{
    private static readonly Regex RefRe =
        new(@"(!?)\[([^\]]*)\]\(([^)\s]+)\)", RegexOptions.Compiled);

    public static string RenderPreview(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        var lines = markdown.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        var sb = new System.Text.StringBuilder();
        int i = 0;

        while (i < lines.Length)
        {
            var trimmed = lines[i].TrimStart(' ', '\t');

            // Fenced code block (content escaped verbatim, no inline rules).
            if (trimmed.StartsWith("```"))
            {
                var lang = trimmed.Substring(3).Trim();
                i++;
                var code = string.Empty;
                while (i < lines.Length && !lines[i].TrimStart(' ', '\t').StartsWith("```"))
                {
                    if (code.Length > 0)
                        code += "\n";
                    code += HtmlEscape(lines[i]);
                    i++;
                }
                if (i < lines.Length && lines[i].TrimStart(' ', '\t').StartsWith("```"))
                    i++;
                var langAttr = lang.Length > 0 ? $" class=\"language-{HtmlEscape(lang)}\"" : string.Empty;
                sb.Append($"<pre><code{langAttr}>{code}</code></pre>");
                continue;
            }

            // Heading 1–6.
            var heading = MatchHeading(trimmed);
            if (heading is not null)
            {
                sb.Append($"<h{heading.Value.level}>{Inline(heading.Value.content)}</h{heading.Value.level}>");
                i++;
                continue;
            }

            // Unordered list (- or *).
            if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
            {
                sb.Append("<ul>");
                while (i < lines.Length)
                {
                    var t = lines[i].TrimStart(' ', '\t');
                    if (!t.StartsWith("- ") && !t.StartsWith("* "))
                        break;
                    sb.Append($"<li>{Inline(t.Substring(2))}</li>");
                    i++;
                }
                sb.Append("</ul>");
                continue;
            }

            // Ordered list (1. / 2. / ...).
            if (Regex.IsMatch(trimmed, @"^\d+\. "))
            {
                sb.Append("<ol>");
                while (i < lines.Length)
                {
                    var t = lines[i].TrimStart(' ', '\t');
                    if (!Regex.IsMatch(t, @"^\d+\. "))
                        break;
                    var dot = t.IndexOf('.');
                    sb.Append($"<li>{Inline(t.Substring(dot + 1).TrimStart(' ', '\t'))}</li>");
                    i++;
                }
                sb.Append("</ol>");
                continue;
            }

            // Blank line — skip.
            if (trimmed.Length == 0)
            {
                i++;
                continue;
            }

            // Paragraph — consume consecutive non-blank, non-special lines.
            var para = string.Empty;
            while (i < lines.Length)
            {
                var t = lines[i].TrimStart(' ', '\t');
                if (t.Length == 0 || t.StartsWith("```") || MatchHeading(t) is not null
                    || t.StartsWith("- ") || t.StartsWith("* ") || Regex.IsMatch(t, @"^\d+\. "))
                    break;
                if (para.Length > 0)
                    para += ' ';
                para += t;
                i++;
            }
            sb.Append($"<p>{Inline(para)}</p>");
        }

        return sb.ToString();
    }

    public static (string value, (int start, int end) sel) ApplyToggle(
        string markdown, (int start, int end) sel, string kind)
    {
        var marker = kind == "bold" ? "**" : kind == "italic" ? "*" : "`";
        var mLen = marker.Length;
        var (lo, hi) = sel;
        var selectedText = markdown.Substring(lo, hi - lo);

        // Toggle-off case A: the selection itself is marker + content + marker.
        if (selectedText.Length >= 2 * mLen
            && selectedText.StartsWith(marker) && selectedText.EndsWith(marker))
        {
            var inner = selectedText.Substring(mLen, selectedText.Length - 2 * mLen);
            return (markdown.Substring(0, lo) + inner + markdown.Substring(hi), (lo, lo + inner.Length));
        }

        // Toggle-off case B: the selection is inside the markers.
        if (lo >= mLen && hi + mLen <= markdown.Length
            && markdown.Substring(lo - mLen, mLen) == marker
            && markdown.Substring(hi, mLen) == marker)
        {
            return (markdown.Substring(0, lo - mLen) + selectedText + markdown.Substring(hi + mLen),
                    (lo - mLen, lo - mLen + selectedText.Length));
        }

        // Toggle-on: wrap the selection in the markers.
        return (markdown.Substring(0, lo) + marker + selectedText + marker + markdown.Substring(hi),
                (lo + mLen, lo + mLen + selectedText.Length));
    }

    public static (string text, int caret) ApplyBlock(string markdown, int caret, string kind)
    {
        var marker = kind switch
        {
            "h1" => "# ",
            "h2" => "## ",
            "h3" => "### ",
            "ul" => "- ",
            "ol" => "1. ",
            _ => throw new ArgumentException($"unknown block kind: {kind}", nameof(kind)),
        };
        // Start of the current line (after the last newline before the caret).
        var lineStart = markdown.LastIndexOf('\n', caret - 1) + 1;
        var outStr = markdown.Substring(0, lineStart) + marker + markdown.Substring(lineStart);
        return (outStr, lineStart + marker.Length);
    }

    public static (string value, (int start, int end) sel) ApplyLink(
        string markdown, (int start, int end) sel, string url)
    {
        var (lo, hi) = sel;
        var selectedText = markdown.Substring(lo, hi - lo);
        var linkText = $"[{selectedText}]({url})";
        var outStr = markdown.Substring(0, lo) + linkText + markdown.Substring(hi);
        return (outStr, (lo, lo + selectedText.Length));
    }

    public static string ImageLink(string alt, string id)
        => $"![{alt}](/content-image/{id})";

    // ── Private helpers (mirror of the TS module's internal functions) ────

    private static string Inline(string input)
    {
        var sb = new System.Text.StringBuilder();
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
        if (level == 0 || level > 6)
            return null;
        if (level < line.Length && line[level] != ' ')
            return null;
        var content = line.Substring(level).TrimStart(' ', '\t');
        if (content.Length == 0)
            return null;
        return (level, content);
    }

    private static bool IsSafeImageSrc(string src)
    {
        if (string.IsNullOrEmpty(src))
            return false;

        // Branch 1: the platform route shape, /content-image/{id}.
        const string route = "/content-image/";
        if (src.StartsWith(route))
        {
            var id = src.Substring(route.Length);
            return id.Length >= 1 && id.Length <= 128
                && Regex.IsMatch(id, @"^[0-9a-f]+$");
        }

        // Branch 2: schemeless relative — no ':', no '//', no whitespace.
        return !src.Contains(':') && !src.StartsWith("//") && !Regex.IsMatch(src, @"\s");
    }

    private static bool IsSafeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
        if (!url.Contains("://") && !url.StartsWith("//"))
            return !url.Contains(':');
        var scheme = url.Split("://")[0].Trim().ToLowerInvariant();
        return scheme == "http" || scheme == "https" || scheme == "mailto";
    }

    private static string HtmlEscape(string s)
        => s.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
}
