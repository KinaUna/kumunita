using System.Text.RegularExpressions;

namespace Kumunita.Web.Security;

/// <summary>
/// RC R·3 — the server-side <c>ImageIds</c> population. The owning doc's
/// <c>ImageIds</c> is derived **from the body**, never from a form field (a
/// form field would be spoofable): this helper scans a Markdown body for the
/// route-shaped content-image links (<c>![alt](/content-image/{id})</c>) and
/// returns the <c>{id}</c> values.
/// <para>
/// <b>Home choice (recorded, U04):</b> a <c>public static class</c> in
/// <c>Kumunita.Web.Security</c> — the same home as <see cref="MarkdownRenderer"/>
/// (the convention anchor the plan names). A single shared seam is the point:
/// U05's three other surfaces (announcement, group-post, static-page) reuse
/// this instead of each re-implementing the regex.
/// </para>
/// <para>
/// <b>Ordering:</b> deduplicated, **first-occurrence order preserved** — the
/// serving route's reverse lookup returns the first owner, so a deterministic
/// order keeps the field stable (a body referencing the same image twice
/// renders both but stores one id).
/// </para>
/// </summary>
public static class ContentImageIds
{
    /// <summary>
    /// Matches the full <c>/content-image/{id}</c> token wherever it occurs in
    /// the body (the renderer's accept-branch-1 shape, verbatim), capturing
    /// the 1–128 hex <c>{id}</c>.
    /// </summary>
    private static readonly Regex FullSrcRe =
        new(@"/content-image/([0-9a-f]{1,128})(?![0-9a-f])", RegexOptions.Compiled);

    /// <summary>
    /// Extract the distinct content-image ids from a Markdown body, in
    /// first-occurrence order. A null/empty body, or a body with no
    /// route-shaped image links, returns an empty list (never null).
    /// </summary>
    public static IReadOnlyList<string> ExtractContentImageIds(string? body)
    {
        if (string.IsNullOrEmpty(body))
            return [];

        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in FullSrcRe.Matches(body))
        {
            var id = m.Groups[1].Value;
            if (seen.Add(id))
                ordered.Add(id);
        }

        return ordered;
    }
}
