using Kumunita.Core.UserInfo;

namespace Kumunita.Core.Mentions;

/// <summary>
/// M36 (ADR 0165, Part 2.1) — the shared mention-token parser. A **static,
/// pure, no-dep** helper (the <c>UgcSnippets</c> / <c>KanbanStatuses</c>
/// shape): a string transform, trivially unit-testable in isolation. It
/// extracts the candidate <c>@&lt;token&gt;</c> occurrences from a Markdown
/// body for the <see cref="IMentionService"/> resolver to match against the
/// directory. No <c>IDocumentSession</c>, no service, no I/O, no
/// localization — the M36·1 / M36·9 pins.
/// </summary>
public static class MentionTokens
{
    /// <summary>
    /// Scan a Markdown body for <c>@&lt;token&gt;</c> occurrence strings.
    /// Returns the **ordered, de-duplicated** list of candidate tokens (a
    /// token = <c>@</c> + a maximal run of non-whitespace characters, the
    /// <c>@</c> itself **not** included — the resolver matches against
    /// <c>Profile.DisplayName</c>).
    /// <para>
    /// A token is **not** extracted from inside a Markdown code span
    /// (``` fenced), inline code (<c>`…`</c>), a link (<c>[text](url)</c>),
    /// or an image (<c>![alt](url)</c>). A lone <c>@</c> or a trailing
    /// <c>@</c> (no following non-whitespace character) yields **no** token.
    /// A null / whitespace body yields an **empty** list. De-duplication is
    /// case-insensitive and order-preserving (first occurrence's spelling is
    /// kept; the resolver is case-insensitive, so the spelling is not
    /// load-bearing) — the M36-1 pin.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> ExtractTokens(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return Array.Empty<string>();

        var excluded = BuildExcludedMask(body);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        for (var i = 0; i < body.Length; i++)
        {
            if (body[i] != '@' || excluded[i])
                continue;

            // A lone / trailing `@` (nothing token-eligible after it) is inert.
            var start = i + 1;
            if (start >= body.Length)
                continue;
            if (char.IsWhiteSpace(body[start]) || IsTokenTerminator(body[start]))
                continue;

            // The maximal run of non-whitespace after the `@`, greedy to the
            // next whitespace / end-of-string / a Markdown structural
            // delimiter that ends a token (an open `(`, an open `[`, a
            // backtick — the Part 2.1 pin).
            var end = start;
            while (end < body.Length
                   && !char.IsWhiteSpace(body[end])
                   && !IsTokenTerminator(body[end]))
            {
                end++;
            }

            var token = body[start..end];
            if (seen.Add(token))
                result.Add(token);
        }

        return result;
    }

    /// <summary>
    /// A Markdown structural delimiter that ends a token run (an open <c>(</c>,
    /// an open <c>[</c>, or a backtick — the Part 2.1 token-shape pin). A fence
    /// line is never reached here: a newline (whitespace) always precedes it, so
    /// the run stops at the whitespace first.
    /// </summary>
    private static bool IsTokenTerminator(char c) => c is '(' or '[' or '`';

    /// <summary>
    /// Mark the character indices that fall inside an **excluded** Markdown
    /// region — a fenced code block, an inline code span, or a link / image
    /// text. A <c>@</c>-token whose start index is marked is code / markup, not
    /// a mention, and is never extracted (the M36·2 / M36-1 code-span-exclude
    /// pin). Pure: operates on the string only, no I/O.
    /// </summary>
    private static bool[] BuildExcludedMask(string body)
    {
        var excluded = new bool[body.Length];

        // 1. Fenced code blocks (``` … ```): a line whose trimmed content starts
        //    with three backticks toggles the fence state; both delimiter lines
        //    and every line in between are excluded.
        var inFence = false;
        for (var i = 0; i < body.Length; )
        {
            var lineStart = i;
            while (i < body.Length && body[i] != '\n')
                i++;
            var lineEnd = i;              // exclusive (body[i] is '\n' or end)
            if (i < body.Length) i++;     // consume the '\n'

            var trimmed = body.AsSpan(lineStart, lineEnd - lineStart).Trim();
            var isFence = trimmed.StartsWith("```", StringComparison.Ordinal);

            if (isFence || inFence)
                for (var k = lineStart; k < lineEnd; k++) excluded[k] = true;

            if (isFence) inFence = !inFence;
        }

        // 2. Inline code spans (`…`): a backtick-delimited region in the
        //    non-fenced text. On the closing backtick the interior (back to the
        //    opening backtick) is marked excluded, so a `@word` between them is
        //    code, not a mention.
        var inInline = false;
        for (var i = 0; i < body.Length; i++)
        {
            if (excluded[i])
                continue; // already inside a fence
            if (body[i] != '`')
                continue;

            if (inInline)
            {
                // Close: mark the interior **back to, but excluding, the opening
                // backtick** so an `@word` between the two backticks is code, not a
                // mention (the M36-1 / M36·2 code-span-exclude pin). The opening
                // backtick was already marked in the open branch, and the close
                // backtick itself is marked below — so the loop starts one step
                // before the close and stops *before* the open (leaving both
                // backtick delimiters already-excluded, not re-marked).
                for (var k = i - 1; k >= 0; k--)
                {
                    if (body[k] == '`') break;
                    if (!excluded[k]) excluded[k] = true;
                }
                excluded[i] = true;
                inInline = false;
            }
            else
            {
                excluded[i] = true;
                inInline = true;
            }
        }

        // 3. Link / image text ([text] / [alt]): the region from an open `[`
        //    to its matching `]` is excluded (a `@` inside the link / image
        //    text is markup, not a mention). Both link and image alt use the
        //    same `[…]` shape.
        for (var i = 0; i < body.Length; i++)
        {
            if (excluded[i] || body[i] != '[')
                continue;
            for (var k = i + 1; k < body.Length; k++)
            {
                if (body[k] == ']')
                {
                    for (var m = i; m <= k; m++) excluded[m] = true;
                    break;
                }
            }
        }

        return excluded;
    }
}

/// <summary>
/// M36 (ADR 0165, Part 2.2) — the mention-resolution read seam. Resolves
/// <c>@</c>-tokens (the <see cref="MentionTokens.ExtractTokens"/> output) to
/// the resident ids they mention, composing the frozen M2
/// <see cref="DirectoryService"/> catalog read.
/// <para>
/// **A read, not a decision (M36·5):** it does **no** <c>IAuthorizationService</c>
/// call and writes **zero** <c>AccessAudit</c> rows — the F7 "a mention emission
/// emits no audit row" pin is preserved at the resolver level.
/// <para>
/// It composes the **concrete** <see cref="DirectoryService"/> (M2 Deviation 1,
/// Part 2.0) — it does **not** invent an <c>IDirectoryService</c> interface.
/// **HTTP-free + Wolverine-free** (M36·9, ADR 0006-D): a plain store-composing
/// read; the only outbound channel in the milestone is the frozen M6 email lane
/// (the <c>EmitAsync</c> caller, not the resolver).
/// </para>
/// </summary>
public interface IMentionService
{
    /// <summary>
    /// Resolve <c>@</c>-tokens to the resident ids they mention. A token
    /// resolves to a resident's <c>SubjectId</c> **iff** exactly one
    /// non-blocked resident (as listed by the frozen
    /// <c>DirectoryService.ListAsync</c>) has a display name equal to the
    /// token (case-insensitive, ordinal) AND that resident is **not** the
    /// <paramref name="authorId"/>. A token matching **zero** residents
    /// (unknown) or **two or more** (ambiguous) is **inert** — dropped, never
    /// a guess (M36-5). The result is the **de-duplicated, order-preserving**
    /// set of resolved recipient ids (order = first token occurrence order).
    /// </summary>
    Task<IReadOnlyCollection<string>> ResolveAsync(
        IReadOnlyCollection<string> tokens,
        string authorId,
        CancellationToken ct = default);
}

/// <summary>
/// The <see cref="IMentionService"/> implementation (M36, Part 2.2). A plain
/// store-composing read over the frozen M2 <see cref="DirectoryService"/>
/// catalog seam (M36·2 — reused, not re-shaped): it calls
/// <c>ListAsync</c> once for the full non-blocked catalog and maps tokens to
/// resident ids. No <c>HttpClient</c>, no Wolverine handler (M36·9, ADR
/// 0006-D).
/// </summary>
public sealed class MentionService : IMentionService
{
    private readonly DirectoryService _directory;

    public MentionService(DirectoryService directory)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
    }

    public async Task<IReadOnlyCollection<string>> ResolveAsync(
        IReadOnlyCollection<string> tokens,
        string authorId,
        CancellationToken ct = default)
    {
        if (tokens is null)
            throw new ArgumentNullException(nameof(tokens));

        // A mention is a read, not a decision (M36·5): compose the frozen M2
        // catalog read — every non-blocked resident, no IAuthorizationService
        // call, no AccessAudit row — and map tokens to resident ids.
        // Blocked residents are already excluded by ListAsync (M36·4); the
        // author is excluded by SubjectId (M36-5) — the author is never
        // notified of their own mention, even if another resident shares the
        // author's display name.
        var list = await _directory.ListAsync(authorId).ConfigureAwait(false);
        var candidates = list.Visible
            .Where(p => !string.IsNullOrEmpty(p.SubjectId) && p.SubjectId != authorId)
            .ToList();

        var resolved = new List<string>();
        var seen = new HashSet<string>();
        foreach (var token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token))
                continue;

            var matches = candidates
                .Where(p => string.Equals(p.DisplayName, token, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.SubjectId)
                .ToList();

            // Inert rule (M36-5): exactly one non-blocked, non-author resident
            // matches → the token resolves to that resident; zero (unknown) or
            // two or more (ambiguous) → dropped, never a guess, never notify the
            // wrong resident (M36·2 / M36-5).
            if (matches.Count == 1 && seen.Add(matches[0]))
                resolved.Add(matches[0]);
        }

        return resolved;
    }
}
