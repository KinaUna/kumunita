using System.Text.RegularExpressions;

namespace Kumunita.Web.Tests.SecGate;

/// <summary>
/// IS38 (IMPROVE-SEC) — the P-8 **CSP-discipline gate**. Pins the
/// "no inline script in Razor views" rule (SECURITY.md §6, OPS §10
/// "code discipline") so the next drift is a **red build**, not a review
/// comment. <c>script-src 'self'</c> (no <c>'unsafe-inline'</c>) silently
/// blocks any inline <c>on*</c> handler or inline executable
/// <c>&lt;script&gt;</c> in a view — the defect is *invisible at runtime*
/// (the handler just never fires), which is exactly what made the 8 finding
/// E9 (and the 2 finding E14) latent.
///
/// Two tests, two invariants (P-8, per the plan's IS38/IS38b split):
///
///   <see cref="NoView_HasAnInlineEventHandler"/> — no inline
///   <c>on*="…"</c> event handler in any view. Baseline **8 → 0**: IS37
///   moved the 6 <c>onsubmit="return confirm(…)"</c> handlers to
///   <c>data-confirm</c>, IS38 moved the last 2 (the AdminAnalytics
///   <c>onchange</c> and the Escalation/Tokens <c>onclick</c>) to
///   <c>data-autosubmit</c> / <c>data-copy-target</c>. Green.
///
///   <see cref="NoView_HasAnInlineScriptBlock"/> — no inline executable
///   <c>&lt;script&gt;</c> block (a <c>&lt;script&gt;</c> /
///   <c>&lt;script type="module"&gt;</c> /
///   <c>&lt;script type="text/javascript"&gt;</c> tag **with a non-empty
///   body** and no <c>src</c>). <c>&lt;script type="application/json"&gt;</c>
///   data islands are excluded (they are not executable; the two in this
///   repo are legitimate). Baseline **2** (the Document/New + Document/Edit
///   folder-picker blocks, finding **E14**) — **skipped in IS38** (so the
///   committed suite stayed green) and **landed green in IS38c** (2 -> 0) once
///   the 2 blocks are extracted to the tsc-built document-folder-picker.ts
///   module.
///
/// Both scans run on **comment-stripped** source: the 2
/// <c>_LayoutScripts.cshtml</c> hits are in HTML comments, and several views
/// carry Razor comments that mention <c>&lt;script&gt;</c> / <c>on*</c> in
/// prose — none is a live attribute or a live block. Stripping both comment
/// kinds is what keeps them out of scope.
///
/// The allowlist is **empty and stays empty** (the IS08 doctrine): a view
/// that genuinely needs an exception is a *pause and record* (a question in
/// the handoff), never a silent allowance.
/// </summary>
public class CspInlineDisciplineGateTests
{
    /// <summary>
    /// The allowlist of <c>file:line</c> sites that may carry an inline
    /// handler or inline script block. **Empty, and it stays empty**: the
    /// fix is always a real tsc module + a <c>data-*</c> attribute, not an
    /// exception. A site that cannot be moved is a recorded pause, not a
    /// silent allowance.
    /// </summary>
    private static readonly IReadOnlySet<string> Allowlist = new HashSet<string>();

    /// <summary>
    /// Inline event handler — a whitespace-anchored <c>on&lt;verb&gt;="</c>
    /// (so <c>font="</c>, <c>data-confirm="</c>, etc. do not match).
    /// Case-sensitive on the leading <c>on</c>, matching how the views are
    /// written and the plan's E9 baseline.
    /// </summary>
    private static readonly Regex InlineHandler = new(
        @"\son[a-z]+=""",
        RegexOptions.Compiled);


    /// <summary>
    /// An inline executable <c>&lt;script&gt;</c> opening tag — a
    /// <c>&lt;script&gt;</c> with no <c>src=</c> (external modules are fine)
    /// and not an <c>application/json</c> / <c>application/ld+json</c> data
    /// island. Matched case-insensitively on the tag.
    /// </summary>
    private static readonly Regex InlineScriptTag = new(
        @"<script\b(?![^>]*\bsrc\s*=)[^>]*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// A <c>&lt;script type="…json…"&gt;</c> data island (e.g.
    /// <c>application/json</c>, <c>application/ld+json</c>) — not
    /// executable, so excluded from the script-block gate.
    /// </summary>
    private static readonly Regex JsonDataIsland = new(
        @"type\s*=\s*""[^""]*json""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// The 2 Document folder-picker blocks (finding **E14**) that the
    /// script-block gate would flag. IS38 does **not** fix them (out of
    /// scope, D8 file limit) and does **not** allowlist them; they are
    /// recorded in the handoff and land green in IS38c after the shared
    /// logic is extracted to a tsc module. This set is the pin that proves
    /// the skip is *about* these 2 blocks (if the set drifts, the test
    /// message names the actual blocks).
    /// </summary>
    private static readonly string[] E14DocumentBlocks =
    {
        "src/Kumunita.Web/Views/Document/New.cshtml",
        "src/Kumunita.Web/Views/Document/Edit.cshtml",
    };

    [Fact(DisplayName = "No view carries an inline event handler (E9: 8 -> 0)")]
    public void NoView_HasAnInlineEventHandler()
    {
        var violations = new List<string>();
        foreach (string file in InlineCshtmlFiles())
        {
            string rel = ToRel(file);
            string src = StripComments(SecGateSource.ReadSrc(rel));
            foreach (Match m in InlineHandler.Matches(src))
            {
                int line = 1;
                for (int i = 0; i < m.Index; i++)
                    if (src[i] == '\n')
                        line++;
                violations.Add($"{rel}:{line}");
            }
        }

        var flagged = violations.Where(v => !Allowlist.Contains(v)).Distinct().ToList();
        Assert.True(
            flagged.Count == 0,
            "View(s) carrying an inline on*=\" handler — replace with a data-* "
            + "attribute wired by a tsc module (confirm.ts / reset-form.ts / "
            + "inline-free.ts precedent); the inline form is silently blocked by "
            + "script-src 'self'.\n  " + string.Join("\n  ", flagged));
    }

    [Fact(DisplayName = "No view carries an inline executable script block (E14: 2 -> 0, IS38c)")]
    public void NoView_HasAnInlineScriptBlock()
    {
        var violations = new List<string>();
        foreach (string file in InlineCshtmlFiles())
        {
            string rel = ToRel(file);
            string stripped = StripComments(SecGateSource.ReadSrc(rel));
            foreach (Match tag in InlineScriptTag.Matches(stripped))
            {
                // A data island (application/json / ld+json) is not executable.
                if (JsonDataIsland.IsMatch(tag.Value))
                    continue;
                // A self-closing <script/> or an empty <script></script> has no
                // body — not an executable block.
                int bodyStart = tag.Index + tag.Length;
                int close = stripped.IndexOf("</script>", bodyStart, StringComparison.OrdinalIgnoreCase);
                if (close < 0)
                    continue;
                string body = stripped[bodyStart..close].Trim();
                if (body.Length == 0)
                    continue;

                int line = 1;
                for (int i = 0; i < tag.Index; i++)
                    if (stripped[i] == '\n')
                        line++;
                violations.Add($"{rel}:{line}");
            }
        }

        var flagged = violations.Where(v => !Allowlist.Contains(v)).Distinct().ToList();

        // IS38c landed: the 2 E14 Document folder-picker blocks are extracted
        // to the tsc-built document-folder-picker.ts module, so the gate sees
        // 0 inline blocks and returns here. (The E14DocumentBlocks pin below
        // stays as the drift detector if a block reappears.)
        if (flagged.Count == 0)
            return;

        bool isOnlyE14 = flagged.Count == E14DocumentBlocks.Length
            && E14DocumentBlocks.All(rel => flagged.Any(f => f.StartsWith(rel + ':', StringComparison.Ordinal)));
        Assert.Fail(
            isOnlyE14
                ? "The 2 E14 Document folder-picker inline <script type=\"module\"> "
                + "blocks are still present (Document/New, Document/Edit). Extract "
                + "the shared fetch-and-populate logic to a tsc module and replace "
                + "each inline block with a data-* attribute (IS38c); then this "
                + "gate is green (2 -> 0). Do NOT allowlist them."
                : "View(s) carrying an inline executable <script> block — extract "
                + "to a tsc module + a data-* attribute; the inline form is "
                + "silently blocked by script-src 'self'.\n  "
                + string.Join("\n  ", flagged));
    }

    // ── helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Every <c>.cshtml</c> under <c>src/Kumunita.Web/Views/</c> (absolute
    /// paths). The gate's scope is the rendered view tree — the only surface
    /// a browser would parse as a document.
    /// </summary>
    private static string[] InlineCshtmlFiles()
        => SecGateSource.AllSrcFiles("*.cshtml")
            .Where(p => ToRel(p).StartsWith("src/Kumunita.Web/Views/", StringComparison.Ordinal))
            .OrderBy(p => ToRel(p), StringComparer.Ordinal)
            .ToArray();

    /// <summary>Repo-relative, <c>/</c>-separated path (for messages + allowlist keys).</summary>
    private static string ToRel(string absPath)
    {
        string root = SecGateSource.RepoRoot.Replace('\\', '/') + "/";
        string rel = absPath.Replace('\\', '/');
        return rel.StartsWith(root, StringComparison.Ordinal) ? rel[root.Length..] : rel;
    }

    /// <summary>
    /// Removes Razor block comments (<c>@* … *@</c>) and HTML comments
    /// (<c>&lt;!-- … --&gt;</c>) so a <c>&lt;script&gt;</c> / <c>on*</c>
    /// mention inside a comment is not mistaken for a live attribute or
    /// block. Razor comments are stripped first: several view comments
    /// reference <c>&lt;script&gt;</c> in prose, and an HTML comment that
    /// appears inside one would otherwise cut it short. Nesting is not
    /// needed for the views this gate pins (no nested comment spans a
    /// live attribute).
    /// </summary>
    private static string StripComments(string src)
    {
        src = RazorComment.Replace(src, "");
        return HtmlComment.Replace(src, "");
    }

    private static readonly Regex RazorComment = new(
        @"@\*[\s\S]*?\*@",
        RegexOptions.Compiled);

    private static readonly Regex HtmlComment = new(
        @"<!--[\s\S]*?-->",
        RegexOptions.Compiled);
}
