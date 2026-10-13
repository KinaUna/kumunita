using System.Text;
using Kumunita.Core.Mentions;
using Kumunita.Core.UserInfo;

namespace Kumunita.Web.Security;

/// <summary>
/// M36 (ADR 0165 D6, Part 2.4) — the <b>@mention render pass</b>: a
/// <b>post-render</b> linkification that runs <i>after</i> the frozen
/// <see cref="MarkdownRenderer"/> (the one-renderer pin — <see cref="MarkdownRenderer.RenderHtml"/>
/// is <b>unchanged</b>, M36·6). It is a <b>display transform</b>, not a
/// Markdown construct: it rewrites <b>only</b> a resolved
/// <c>@&lt;DisplayName&gt;</c> text node to
/// <c>&lt;a class="mention" href="/directory/{id}"&gt;@&lt;DisplayName&gt;&lt;/a&gt;</c>.
/// <para>
/// <b>Shape resolution (drift note, U07).</b> The register's U07 deliverable and
/// the design doc Part 2.4 prose name a client-side <c>mention-renderer.ts</c>
/// IIFE; but the design doc Part 2.7's <c>MentionRendererTests</c> is a
/// <b>C#</b> xunit class in <c>tests/Kumunita.Web.Tests</c> and Part 2.8's
/// acceptance gate is C# (<c>dotnet exec</c>) — a C# test cannot exercise a TS
/// IIFE. ADR 0165 D6 Consequences explicitly permit "a server-side pass in the
/// <c>MarkdownRenderer</c> boundary" as one of the two legitimate shapes, and the
/// pinned <c>MentionRendererTests</c> + the <see cref="MarkdownRenderer"/>
/// precedent (C#, <c>Security/</c>, tested in <c>tests/Kumunita.Web.Tests</c>)
/// make the <b>server-side C# pass</b> the only shape that satisfies the
/// milestone's acceptance gate. This class is that shape (not a third shape).
/// </para>
/// <para>
/// <b>Reuses, does not re-derive (M36·5).</b> <see cref="BuildMapAsync"/> calls
/// the frozen <see cref="IMentionService.ResolveAsync"/> (the U03 inert rule —
/// self / ambiguous / unknown / blocked are already excluded from the resolved
/// id set) and the frozen M2 <see cref="DirectoryService.ListAsync"/> catalog
/// read for the id→display-name projection. It does <b>not</b> re-implement the
/// inert rule, make an <c>IAuthorizationService</c> call, or write an
/// <c>AccessAudit</c> row — the mention link "grants no access"; the frozen M2
/// directory gate runs on click (D5).
/// </para>
/// </summary>
public static class MentionRenderer
{
    /// <summary>
    /// The tag names whose interior is <b>never</b> linkified (the M36-6 skip
    /// rule): an anchor, inline code, or a code block. <see cref="Rewrite"/> only
    /// runs on text at skip-depth 0, so a resolved <c>@name</c> inside any of
    /// these stays plain text. (<c>img</c> / <c>br</c> are void and their
    /// <c>alt</c> text lives inside the tag, not in a text node, so they need no
    /// depth accounting.)
    /// </summary>
    private static bool IsSkipTag(string local) => local is "a" or "code" or "pre";

    /// <summary>
    /// A Markdown structural delimiter that ends a mention token run (an open
    /// <c>(</c>, an open <c>[</c>, or a backtick) — the <see cref="MentionTokens"/>
    /// parser's <c>IsTokenTerminator</c>, mirrored here so the render side's token
    /// boundary matches the emitter's (the Part 2.1 token-shape pin).
    /// </summary>
    private static bool IsTokenTerminator(char c) => c is '(' or '[' or '`';

    /// <summary>
    /// The render pass (Part 2.4 / D6). A <b>pure</b> string transform:
    /// <paramref name="html"/> is the <b>rendered</b> body (the output of the
    /// frozen <see cref="MarkdownRenderer.RenderHtml"/>), and
    /// <paramref name="mentionMap"/> is the resolved mention map (display-name →
    /// resident <c>SubjectId</c>, the wire-in's <see cref="BuildMapAsync"/>
    /// output). Returns the body with each resolved <c>@&lt;DisplayName&gt;</c>
    /// text node rewritten to a <c class="mention">anchor</c>; every other
    /// <c>@</c> (unresolved / ambiguous / blocked / self, or inside
    /// <c>&lt;a&gt;</c>/<c>&lt;code&gt;</c>/<c>&lt;pre&gt;</c>) stays plain text.
    /// <para>
    /// <b>Idempotent</b> (D6 / M36-6): a rewritten mention's text is inside an
    /// <c>&lt;a&gt;</c>, so a second pass is at skip-depth &ge; 1 and skips it —
    /// running twice yields the same output.
    /// </para>
    /// <para>
    /// No new dependency, no mention-parsing library (M36·6 / D6) — a small,
    /// auditable token scan over the MarkdownRenderer's controlled output
    /// subset.
    /// </para>
    /// </summary>
    /// <param name="html">The rendered HTML body (the MarkdownRenderer output).</param>
    /// <param name="mentionMap">
    /// Resolved mention map (display-name → resident id). The keys are the
    /// <b>exact</b> display names that resolved (the inert rule is applied
    /// upstream by <see cref="IMentionService.ResolveAsync"/>).
    /// </param>
    public static string Render(string? html, IReadOnlyDictionary<string, string>? mentionMap)
    {
        if (string.IsNullOrEmpty(html))
            return html ?? string.Empty;

        // A no-op where there is nothing to resolve (an empty / null map holds
        // only-resolved names — if there are none, every `@` stays plain text).
        if (mentionMap is null || mentionMap.Count == 0)
            return html;

        // Case-insensitive display-name → resident id (the resolver is
        // case-insensitive, so matching is too — M36·2).
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in mentionMap)
        {
            if (!string.IsNullOrWhiteSpace(kv.Key))
                names[kv.Key.Trim()] = kv.Value;
        }

        var sb = new StringBuilder(html.Length + 64);
        var skipDepth = 0;
        var i = 0;
        var n = html.Length;

        while (i < n)
        {
            if (html[i] != '<')
            {
                // TEXT segment: run to the next '<' (or end). Only text at
                // skip-depth 0 is candidate for linkification.
                var end = html.IndexOf('<', i);
                if (end < 0)
                {
                    sb.Append(Rewrite(html[i..], names, skipDepth));
                    break;
                }
                sb.Append(Rewrite(html[i..end], names, skipDepth));
                i = end;
                continue;
            }

            // TAG segment: run to the next '>' (the MarkdownRenderer output has
            // no '>' inside an attribute — URLs are scheme-validated + escaped).
            var close = html.IndexOf('>', i);
            if (close < 0)
            {
                sb.Append(html[i..]);
                break;
            }

            var tag = html[i..(close + 1)];
            var (local, isClose) = ParseTag(tag);

            if (isClose)
            {
                if (IsSkipTag(local) && skipDepth > 0) skipDepth--;
            }
            else
            {
                var selfClosing =
                    tag.TrimEnd().EndsWith("/", StringComparison.Ordinal)
                    || local is "img" or "br";
                if (!selfClosing && IsSkipTag(local)) skipDepth++;
            }

            sb.Append(tag);
            i = close + 1;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Rewrite a single <b>text segment</b> (depth 0) — a resolved
    /// <c>@&lt;DisplayName&gt;</c> becomes a mention anchor; everything else is
    /// copied verbatim. At skip-depth &ge; 1 (inside <c>&lt;a&gt;</c> /
    /// <c>&lt;code&gt;</c> / <c>&lt;pre&gt;</c>) the text is returned unchanged
    /// (the M36-6 skip rule / the idempotency guarantee).
    /// </summary>
    private static string Rewrite(string text, IReadOnlyDictionary<string, string> names, int skipDepth)
    {
        if (skipDepth > 0)
            return text;
        if (text.IndexOf('@') < 0)
            return text;

        var sb = new StringBuilder(text.Length + 32);
        var i = 0;
        var n = text.Length;

        while (i < n)
        {
            if (text[i] != '@')
            {
                sb.Append(text[i]);
                i++;
                continue;
            }

            // The mention token = `@` + the maximal run, stopping at whitespace
            // OR the same Markdown structural delimiters the U03
            // <see cref="MentionTokens"/> parser uses (`(`, `[`, a backtick — the
            // Part 2.1 token-shape pin). Matching the <b>exact same token run</b>
            // as the parser keeps the render pass and the notification emitter in
            // agreement (a token that resolves ⇔ links; a token that is inert ⇔
            // stays plain text — no "notified but not linked" drift).
            var j = i + 1;
            while (j < n && !char.IsWhiteSpace(text[j]) && !IsTokenTerminator(text[j]))
                j++;

            var run = text[(i + 1)..j];

            if (run.Length > 0 && names.TryGetValue(run, out var residentId))
            {
                sb.Append("<a class=\"mention\" href=\"/directory/")
                  .Append(residentId)
                  .Append("\">@")
                  .Append(run)
                  .Append("</a>");
                i = j;
            }
            else
            {
                // Unresolved / ambiguous / blocked / self (not in the map), or a
                // lone `@`: keep the plain text.
                sb.Append('@');
                i++;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Extract the tag name (first word) + whether it is a closing tag. Handles
    /// <c>&lt;a&gt;</c> / <c>&lt;/a&gt;</c> / <c>&lt;img …/&gt;</c> /
    /// <c>&lt;h1&gt;</c> / <c>&lt;li class="…"&gt;</c> by reading the leading
    /// identifier; attributes are ignored.
    /// </summary>
    private static (string Local, bool IsClose) ParseTag(string tag)
    {
        var t = tag.Trim();
        var isClose = t.StartsWith("</", StringComparison.Ordinal);
        if (isClose) t = t[2..].Trim();

        var local = new string(t.TakeWhile(ch => char.IsLetterOrDigit(ch) || ch == '-').ToArray());
        return (local.Length == 0 ? string.Empty : local.ToLowerInvariant(), isClose);
    }

    /// <summary>
    /// Build the <b>resolved mention map</b> (display-name → resident
    /// <c>SubjectId</c>) for a body's <c>@</c>-tokens — the wire-in's entry
    /// point. It <b>reuses</b> the frozen
    /// <see cref="IMentionService.ResolveAsync"/> (the U03 inert rule: self /
    /// ambiguous / unknown / blocked are already excluded from the resolved id
    /// set — M36-5) for the id set, then projects id → display name off the
    /// frozen M2 <see cref="DirectoryService.ListAsync"/> catalog read (a pure
    /// read — no <c>IAuthorizationService</c> call, no <c>AccessAudit</c> row,
    /// M36·5). It does <b>not</b> re-derive the inert rule.
    /// <para>
    /// <b>A read, not a decision (M36·5).</b> Zero <c>AccessAudit</c> rows; the
    /// mention link grants no access — the directory gate runs on click (D5).
    /// </para>
    /// </summary>
    /// <param name="mentions">The frozen U03 read seam (the inert rule).</param>
    /// <param name="directory">The frozen M2 catalog read (id → display name).</param>
    /// <param name="body">The content's Markdown body (the <c>@</c>-tokens' source).</param>
    /// <param name="authorId">The content's author id (the self-exclusion input).</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>
    /// The resolved map (display-name → id). Empty when the body has no
    /// resolvable mention — in which case <see cref="Render"/> is a no-op and
    /// every <c>@</c> stays plain text.
    /// </returns>
    public static async Task<IReadOnlyDictionary<string, string>> BuildMapAsync(
        IMentionService mentions,
        DirectoryService directory,
        string? body,
        string authorId,
        CancellationToken ct = default)
    {
        var empty = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(body))
            return empty;

        var tokens = MentionTokens.ExtractTokens(body);
        if (tokens.Count == 0)
            return empty;

        // Reuse the frozen resolver (the inert rule — self / ambiguous / unknown
        // / blocked excluded, M36-5) for the resolved id set; do NOT re-derive it.
        var resolvedIds = await mentions.ResolveAsync(tokens, authorId, ct).ConfigureAwait(false);
        if (resolvedIds is null || resolvedIds.Count == 0)
            return empty;

        var idSet = resolvedIds.ToHashSet(StringComparer.Ordinal);
        // Project the resolved ids to their directory display names (a pure
        // catalog read — no authorization call, no audit row, M36·5).
        var list = await directory.ListAsync(authorId).ConfigureAwait(false);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in list.Visible)
        {
            if (idSet.Contains(p.SubjectId) && !string.IsNullOrWhiteSpace(p.DisplayName))
                map[p.DisplayName] = p.SubjectId;
        }
        return map;
    }
}
