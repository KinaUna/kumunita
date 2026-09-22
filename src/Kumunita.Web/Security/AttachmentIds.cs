using System.Text.RegularExpressions;

namespace Kumunita.Web.Security;

/// <summary>
/// ATT U7 (C-ATT·4) — the server-side <c>AttachmentIds</c> population. The
/// owning doc's <c>AttachmentIds</c> is derived **from the body**, never from
/// a form field (a form field would be spoofable): this helper scans a
/// Markdown body for the route-shaped attachment links
/// (<c>[label](/attachment/{id})</c>) and returns the <c>{id}</c> values.
/// <para>
/// <b>Home choice (recorded, U7):</b> a <c>public static class</c> in
/// <c>Kumunita.Web.Security</c> — the same home as <see cref="ContentImageIds"/>
/// and <see cref="MarkdownRenderer"/> (the convention anchor the design doc
/// §2.4 names). A single shared seam is the point: the post create, reply
/// create/edit, announcement create/edit, and group-post create call-sites
/// all reuse this instead of each re-implementing the regex.
/// </para>
/// <para>
/// <b>Ordering:</b> deduplicated, **first-occurrence order preserved** — the
/// serving route's reverse lookup returns the first owner, so a deterministic
/// order keeps the field stable (a body referencing the same attachment twice
/// renders both but stores one id).
/// </para>
/// <para>
/// <b>Read-only (C-ATT·6):</b> this helper only extracts ids for the write
/// lanes' <c>AttachmentIds</c> field; it does **not** check the upload
/// allowlist/size (that's U8's <c>MediaOptions.IsAttachmentAllowed</c> +
/// <c>MaxBytes</c>) and does **not** touch the store.
/// </para>
/// <para>
/// <b>Web-only (C-ATT·4):</b> Core stays body-parse-free (ADR 0006-D); this
/// class deliberately lives in <c>Kumunita.Web.Security</c>, never
/// <c>Kumunita.Core</c>.
/// </para>
/// </summary>
public static class AttachmentIds
{
    /// <summary>
    /// Matches the full <c>/attachment/{id}</c> token wherever it occurs in
    /// the body (the image lane's <c>FullSrcRe</c> shape, verbatim, route
    /// prefix swapped <c>/content-image/</c> → <c>/attachment/</c>),
    /// capturing the 1–128 hex <c>{id}</c>.
    /// </summary>
    private static readonly Regex FullIdRe =
        new(@"/attachment/([0-9a-f]{1,128})(?![0-9a-f])", RegexOptions.Compiled);

    /// <summary>
    /// Extract the distinct attachment ids from a Markdown body, in
    /// first-occurrence order. A null/empty body, or a body with no
    /// route-shaped attachment links, returns an empty list (never null).
    /// </summary>
    public static IReadOnlyList<string> ExtractAttachmentIds(string? body)
    {
        if (string.IsNullOrEmpty(body))
            return [];

        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in FullIdRe.Matches(body))
        {
            var id = m.Groups[1].Value;
            if (seen.Add(id))
                ordered.Add(id);
        }

        return ordered;
    }
}
