using Kumunita.Web.Security;

namespace Kumunita.Web.Tests;

/// <summary>
/// M36 (ADR 0165, design doc Part 2.7) — the 4 pinned
/// <see cref="MentionRenderer"/> render-pass tests (the M36·6 pins). The pass is
/// a **pure string transform** (U07's shape resolution: the C# server-side pass
/// at the <see cref="MarkdownRenderer"/> boundary, not a TS IIFE): each test
/// calls <see cref="MentionRenderer.Render"/> on a rendered HTML body + a
/// resolved mention map and asserts on the linkified output.
/// <list type="number">
/// <item><b>Rewrite</b> (M36-3 / M36·6): a resolved <c>@Ada</c> text node becomes
///       exactly <c>&lt;a class="mention" href="/directory/{id}"&gt;@Ada&lt;/a&gt;</c>;
///       an unresolved sibling stays plain text.</item>
/// <item><b>Unresolved = no-op</b> (M36·6): a <c>@</c> token not in the map is
///       returned unchanged (plain text — a mention is a display transform, not a
///       guess).</item>
/// <item><b>Idempotent</b> (M36·6): a second pass over the output is a no-op (the
///       rewritten mention's text is inside an <c>&lt;a&gt;</c>, so it is at skip-
///       depth ≥ 1 and skipped).</item>
/// <item><b>Skips inside anchor / code</b> (M36·6): a resolved <c>@name</c> inside
///       an existing <c>&lt;a&gt;</c> or <c>&lt;code&gt;</c> is never rewritten.</item>
/// </list>
/// No DI, no fixture: pure functions over strings (the
/// <see cref="MarkdownRendererTests"/> precedent).
/// </summary>
public class MentionRendererTests
{
    private static readonly IReadOnlyDictionary<string, string> Map =
        new Dictionary<string, string> { ["Ada"] = "ada-001" };

    // ── Pin 1 ─────────────────────────────────────────────────────────────
    // M36-3 / M36·6 — the resolved `@Ada` text node rewrites to the pinned
    // mention anchor; the unresolved `@Bob` sibling (not in the map) stays
    // plain text.

    [Fact]
    public void Render_Pass_RewritesResolvedMentionToDirectoryLink()
    {
        var html = "<p>hi @Ada @Bob</p>";

        var output = MentionRenderer.Render(html, Map);

        Assert.Contains("<a class=\"mention\" href=\"/directory/ada-001\">@Ada</a>", output);
        // The unresolved sibling stays plain text (no mention anchor for it).
        Assert.Contains(" @Bob", output);
        Assert.DoesNotContain("href=\"/directory/\"@Bob", output);
        // The surrounding paragraph is preserved.
        Assert.StartsWith("<p>hi ", output);
        Assert.EndsWith("</p>", output);
    }

    // ── Pin 2 ─────────────────────────────────────────────────────────────
    // M36·6 — a token not in the resolved map is a no-op: the body returns
    // byte-for-byte unchanged (a mention is never a guess — M36-5's inert
    // rule at the render layer).

    [Fact]
    public void Render_Pass_UnresolvedTokenIsPlainText()
    {
        const string html = "<p>ping @Unknown here</p>";

        // A non-empty map that does NOT contain `Unknown` (the realistic
        // "resolved someone else" shape): the `@Unknown` token is inert.
        var output = MentionRenderer.Render(html, Map);

        Assert.Equal(html, output);
    }

    // ── Pin 3 ─────────────────────────────────────────────────────────────
    // M36·6 — idempotent: running the pass twice over the same body yields the
    // same output (the first pass's mention anchor is inside an `<a>`, so the
    // second pass is at skip-depth ≥ 1 and skips it).

    [Fact]
    public void Render_Pass_IsIdempotent()
    {
        const string html = "<p>hi @Ada</p>";

        var once = MentionRenderer.Render(html, Map);
        var twice = MentionRenderer.Render(once, Map);

        Assert.Equal(once, twice);
        // Exactly one mention anchor (not two).
        Assert.Equal(1, Count(once, "<a class=\"mention\""));
    }

    // ── Pin 4 ─────────────────────────────────────────────────────────────
    // M36·6 — a resolved `@Ada` inside an existing `<a>` or `<code>` element is
    // never rewritten (the skip rule). Both cases return the body unchanged.

    [Fact]
    public void Render_Pass_SkipsInsideAnchorAndCode()
    {
        const string insideAnchor = "<a href=\"/directory/other\">@Ada</a>";
        const string insideCode = "<code>@Ada</code>";

        var anchorOut = MentionRenderer.Render(insideAnchor, Map);
        var codeOut = MentionRenderer.Render(insideCode, Map);

        // Neither gains a mention anchor (the token is inside a skip-tag).
        Assert.DoesNotContain("class=\"mention\"", anchorOut);
        Assert.DoesNotContain("class=\"mention\"", codeOut);
        Assert.Equal(insideAnchor, anchorOut);
        Assert.Equal(insideCode, codeOut);
    }

    private static int Count(string haystack, string needle)
    {
        var count = 0;
        var idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }
}
