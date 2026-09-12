namespace Kumunita.Web.Security;

/// <summary>
/// A minimal, escape-first Markdown → HTML renderer for
/// <see cref="Kumunita.Core.Localization.LocalizedPage.Body"/> (ADR 0005 A —
/// the "single page engine" the ADR promises for static pages).
/// <para>
/// <b>XSS-safe by construction:</b> every input character is escaped
/// (HtmlEncode) <i>before</i> any inline rules are applied, so a hostile
/// admin-typed <c>&lt;script&gt;</c> / <c>onerror=</c> / <c>javascript:</c>
/// cannot survive the round-trip. URL values are then re-validated against a
/// scheme whitelist (<c>http</c> / <c>https</c> / <c>mailto</c> / relative) —
/// <c>javascript:</c>, <c>data:</c>, and any other scheme are rejected and the
/// link is rendered as plain text.
/// </para>
/// <para>
/// Supported subset (the common static-page needs): headings 1–6, paragraphs,
/// unordered lists (<c>-</c> / <c>*</c>), ordered lists (<c>1.</c>), fenced
/// code blocks (```), <c>**bold**</c>, <c>*italic*</c>, `` `code` ``, and
/// <c>[label](url)</c> links. Tables, images, and raw HTML are intentionally
/// out of scope — the ADR's static pages (terms, about, help) do not need
/// them, and omitting them keeps the renderer small and auditable.
/// </para>
/// <para>
/// This is the **only** place Markdown is rendered in the Web project — the
/// U5-pinned <c>PreviewPage</c> editor view and the U6 static-page route
/// (<c>/terms</c> / <c>/help</c>) both call <see cref="RenderHtml"/>.
/// </para>
/// </summary>
public static class MarkdownRenderer
{
    /// <summary>
    /// Renders a Markdown string to an HTML fragment. Safe to embed in a
    /// <c>&lt;div class="markdown"&gt;</c> container (the U6 static-page view
    /// wraps the output this way). Returns an empty string for null/empty
    /// input.
    /// </summary>
    public static string RenderHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var sb = new System.Text.StringBuilder();
        var i = 0;

        while (i < lines.Length)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            // Fenced code block — ``` ... ``` (the content is escaped verbatim,
            // no inline rules applied).
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                var lang = trimmed[3..].Trim();
                i++;
                var code = new System.Text.StringBuilder();
                while (i < lines.Length && !lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    if (code.Length > 0) code.Append('\n');
                    code.Append(HtmlEscape(lines[i]));
                    i++;
                }
                // Skip the closing fence (if present).
                if (i < lines.Length && lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
                    i++;

                var langAttr = string.IsNullOrEmpty(lang) ? string.Empty : $" class=\"language-{HtmlEscape(lang)}\"";
                sb.Append("<pre><code").Append(langAttr).Append('>').Append(code).Append("</code></pre>");
                continue;
            }

            // Heading 1–6.
            var heading = MatchHeading(trimmed);
            if (heading is not null)
            {
                var level = heading.Value.level;
                var content = heading.Value.content;
                sb.Append($"<h{level}>").Append(Inline(content)).Append($"</h{level}>");
                i++;
                continue;
            }

            // Unordered list (- or *).
            if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
            {
                sb.Append("<ul>");
                while (i < lines.Length)
                {
                    var t = lines[i].TrimStart();
                    if (!(t.StartsWith("- ", StringComparison.Ordinal) || t.StartsWith("* ", StringComparison.Ordinal)))
                        break;
                    sb.Append("<li>").Append(Inline(t[2..])).Append("</li>");
                    i++;
                }
                sb.Append("</ul>");
                continue;
            }

            // Ordered list (1. / 2. / ...).
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d+\. "))
            {
                sb.Append("<ol>");
                while (i < lines.Length)
                {
                    var t = lines[i].TrimStart();
                    if (!System.Text.RegularExpressions.Regex.IsMatch(t, @"^\d+\. "))
                        break;
                    var dot = t.IndexOf('.');
                    sb.Append("<li>").Append(Inline(t[(dot + 1)..].TrimStart())).Append("</li>");
                    i++;
                }
                sb.Append("</ol>");
                continue;
            }

            // Blank line — skip (paragraph breaks are handled by consecutive
            // non-blank lines collapsing into one <p>).
            if (trimmed.Length == 0)
            {
                i++;
                continue;
            }

            // Paragraph — consume consecutive non-blank, non-special lines.
            var para = new System.Text.StringBuilder();
            while (i < lines.Length)
            {
                var t = lines[i].TrimStart();
                if (t.Length == 0
                    || t.StartsWith("```", StringComparison.Ordinal)
                    || MatchHeading(t) is not null
                    || t.StartsWith("- ", StringComparison.Ordinal)
                    || t.StartsWith("* ", StringComparison.Ordinal)
                    || System.Text.RegularExpressions.Regex.IsMatch(t, @"^\d+\. "))
                    break;
                if (para.Length > 0) para.Append(' ');
                para.Append(t);
                i++;
            }
            sb.Append("<p>").Append(Inline(para.ToString())).Append("</p>");
        }

        return sb.ToString();
    }

    // ── Private helpers ──────────────────────────────────────────────────

    private static (int level, string content)? MatchHeading(string line)
    {
        var level = 0;
        while (level < line.Length && level < 6 && line[level] == '#')
            level++;
        if (level == 0 || level > 6)
            return null;
        if (level < line.Length && line[level] != ' ')
            return null;
        var content = line[level..].TrimStart();
        if (content.Length == 0)
            return null;
        return (level, content);
    }

    /// <summary>
    /// Inline Markdown → HTML. Links are extracted first (on the raw text,
    /// before any escaping) so their URLs survive the scheme check intact.
    /// All non-link text is HTML-escaped before the inline rules (bold,
    /// italic, code) are applied — a hostile <c>&lt;script&gt;</c> /
    /// <c>onerror=</c> cannot survive the round-trip.
    /// </summary>
    private static string Inline(string input)
    {
        var result = new System.Text.StringBuilder();
        var lastEnd = 0;
        var linkRe = new System.Text.RegularExpressions.Regex(
            @"\[([^\]]+)\]\(([^)\s]+)\)");

        foreach (System.Text.RegularExpressions.Match m in linkRe.Matches(input))
        {
            // Text before this link → escape + inline rules.
            if (m.Index > lastEnd)
                result.Append(InlineText(input[lastEnd..m.Index]));

            var label = m.Groups[1].Value;
            var url   = m.Groups[2].Value;
            var escLabel = HtmlEscape(label);

            if (IsSafeUrl(url))
            {
                // Attribute-escape the URL: & → &amp;, " → &quot;
                // (a literal & in a query string must be entity-encoded in
                // an HTML attribute; the browser decodes it on navigation).
                var escUrl = url.Replace("&", "&amp;").Replace("\"", "&quot;");
                result.Append("<a href=\"").Append(escUrl)
                       .Append("\">").Append(escLabel).Append("</a>");
            }
            else
            {
                // Unsafe URL — render the label as plain escaped text.
                result.Append(escLabel);
            }

            lastEnd = m.Index + m.Length;
        }

        // Text after the last link.
        if (lastEnd < input.Length)
            result.Append(InlineText(input[lastEnd..]));

        return result.ToString();
    }

    /// <summary>
    /// HTML-escape a text segment, then apply the inline rules
    /// (code, bold, italic) on the already-escaped result.
    /// </summary>
    private static string InlineText(string text)
    {
        var s = HtmlEscape(text);
        // `code` first (so a backtick inside bold is not re-processed).
        s = System.Text.RegularExpressions.Regex.Replace(s,
            @"`([^`]+)`", m => "<code>" + m.Groups[1].Value + "</code>");
        // **bold** (before *italic* so a double-asterisk isn't eaten as two italics).
        s = System.Text.RegularExpressions.Regex.Replace(s,
            @"\*\*([^*]+)\*\*", m => "<strong>" + m.Groups[1].Value + "</strong>");
        // *italic*.
        s = System.Text.RegularExpressions.Regex.Replace(s,
            @"\*([^*]+)\*", m => "<em>" + m.Groups[1].Value + "</em>");
        return s;
    }

    /// <summary>
    /// Escapes the five HTML-significant characters (&amp;, &lt;, &gt;, ", ').
    /// Applied to every input character <i>before</i> any inline rules run,
    /// so a hostile <c>&lt;script&gt;</c> / <c>onerror=</c> cannot survive the
    /// round-trip. (HTML entities in the resulting <c>href</c> attribute —
    /// e.g. <c>&amp;</c> for a literal <c>&amp;</c> in a URL query string — are
    /// decoded by the browser when it navigates, so this is correct HTML.)
    /// </summary>
    private static string HtmlEscape(string s)
    {
        return s
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }

    /// <summary>
    /// Scheme whitelist for links. <c>http</c>, <c>https</c>, <c>mailto</c>,
    /// and relative paths are allowed; <c>javascript:</c>, <c>data:</c>, and
    /// any other scheme are rejected (the link renders as plain text).
    /// </summary>
    private static bool IsSafeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
        // Relative URLs (no scheme, no protocol-relative //) are safe.
        if (!url.Contains("://") && !url.StartsWith("//", StringComparison.Ordinal))
            return !url.Contains(":"); // a bare "foo:bar" is not a relative path
        var scheme = url.Split("://")[0].Trim().ToLowerInvariant();
        return scheme is "http" or "https" or "mailto";
    }
}
