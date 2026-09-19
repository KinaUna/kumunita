using System.Text.Json;

namespace Kumunita.Web.Security;

/// <summary>
/// TG (ADR 0044, U8b register patch) — the server-side <c>TagSlugs</c>
/// population. The composer's <c>TagIds</c> form field (posted by
/// <c>client/lib/tag-suggest.ts</c> as a JSON array of label strings, e.g.
/// <c>["sanitation", "budget"]</c> or <c>[]</c> for the empty state) is
/// parsed here into a clean <c>IReadOnlyList&lt;string&gt;</c> of slugs —
/// trimmed, blank entries dropped, deduplicated (first-occurrence order
/// preserved). The <c>Slug</c> is the business key (C-TG·4); the
/// <c>TagService.DeriveSlug</c> lane re-derives + charset-validates each
/// slug (a bad slug is an <c>ArgumentException</c> — the Web layer maps it
/// to a form error, the M3 "a form is a shape" precedent).
/// <para>
/// <b>Why a helper (not a model-binding <c>string[]</c>):</b> ASP.NET
/// Core's default form binder does <b>not</b> deserialize a single form
/// field whose value is a JSON array into a <c>string[]</c> — it binds by
/// repeated field names (e.g. <c>TagIds=a&amp;TagIds=b</c>), but the
/// client posts <b>one</b> field whose <b>value</b> is the JSON array
/// (<c>TagIds=["sanitation","budget"]</c>). A raw <c>string</c> form field
/// + this parse helper is the source-compatible shape (the U8b register's
/// "the simplest source-compatible shape" note); it also lets the Web layer
/// normalize (trim / dedup / drop-blank) <b>before</b> the Core write lane
/// sees the values, so the <c>TagService.DeriveSlug</c> charset check sees
/// a clean input (a blank entry would otherwise be an
/// <c>ArgumentException</c> at the lane, not a form error at the Web).
/// <para>
/// <b>Ordering:</b> deduplicated, first-occurrence order preserved — the
/// same convention as <see cref="ContentImageIds.ExtractContentImageIds"/> /
/// <see cref="AttachmentIds.ExtractAttachmentIds"/> (the body-parsed id
/// idiom, U4 precedent).
/// </summary>
public static class TagSlugs
{
    /// <summary>
    /// Parse the composer's <c>TagIds</c> form field (a JSON array of label
    /// strings, or a raw CSV string as a defensive fallback) into a clean
    /// <c>IReadOnlyList&lt;string&gt;</c> of slugs — trimmed, blank entries
    /// dropped, deduplicated (first-occurrence order preserved). Null /
    /// empty / malformed input returns an empty list (never null) — the
    /// "no tags" state (the U4 additive default-empty pin).
    /// </summary>
    public static IReadOnlyList<string> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        // The client (tag-suggest.ts) posts a JSON array of strings:
        //   ["sanitation", "budget"]   (or [] for the empty state)
        // Try JSON first; fall back to CSV split for a raw value.
        List<string> labels;
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
        {
            try
            {
                labels = JsonSerializer.Deserialize<List<string>>(trimmed)
                         ?? new List<string>();
            }
            catch (JsonException)
            {
                // Malformed JSON — fall through to the CSV split.
                labels = SplitCsv(raw);
            }
        }
        else
        {
            labels = SplitCsv(raw);
        }

        var ordered = new List<string>(labels.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var label in labels)
        {
            if (string.IsNullOrWhiteSpace(label)) continue;
            var slug = label.Trim();
            if (seen.Add(slug))
                ordered.Add(slug);
        }

        return ordered;
    }

    private static List<string> SplitCsv(string raw) =>
        raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
           .ToList();
}
