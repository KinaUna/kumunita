using System.Globalization;
using Kumunita.Core.Localization;
using Microsoft.AspNetCore.Http;

namespace Kumunita.Web.Security;

/// <summary>
/// The per-request **language signal** the platform renders in (ADR 0046 —
/// the default-language chain when a resident has not picked one). Two
/// inputs, one priority order:
/// </summary>
/// <list type="number">
/// <item>The <c>kumunita.locale</c> preference cookie — an **explicit** pick
/// (the settings page / the language switcher wrote it). Present and enabled
/// → the platform renders in it; the chain below is not consulted for the
/// effective language (it only supplies the picker's "matched from your
/// browser" marker).</item>
/// <item>The browser's <c>Accept-Language</c> request header — matched
/// **case-insensitively against the enabled** <see cref="Kumunita.Core.Localization.LanguageCatalog"/>
/// set (an exact match first, then a primary-subtag match, e.g.
/// <c>fr-CA</c> → <c>fr</c>). The first enabled match becomes the effective
/// language. A match is **suggested, never persisted**: the cookie is still
/// the only write path (an explicit pick always wins over the browser and
/// pins the choice; the browser signal is re-read per request and is
/// privacy-honest — it is never stored, never a claim, and never used for
/// authorization, the thin-token rule, ADR 0001-B).</item>
/// </list>
/// <para>
/// If neither yields an enabled catalog language, <see cref="Preference"/>
/// returns <c>null</c> and the provider's frozen chain (instance default →
/// <c>en</c>, M·1/M·9) applies unchanged. <see cref="Browser"/> returns the
/// matched catalog code for the picker's marker, or <c>null</c>. The
/// <c>Accept-Language</c> parse is permissive (RFC 9110 grammar, quality
/// factors honored, invalid tags dropped) — a malformed header degrades to
/// "no browser signal", never to a fault.
/// </para>
/// <para>
/// **HTTP-free Core (M·8) holds:** this class lives in the Web layer and
/// passes plain BCP-47 strings to
/// <see cref="Kumunita.Core.Localization.ITranslationProvider"/> — the
/// provider never reads a header or a cookie.
/// </para>
public static partial class RequestLanguage
{
    /// <summary>
    /// The per-request **effective** language signal: the preference cookie
    /// if present, else the first enabled <c>Accept-Language</c> match, else
    /// <c>null</c> (the provider then resolves the instance default →
    /// <c>en</c>).
    /// </summary>
    public static string? Preference(HttpRequest request, IEnumerable<LanguageCatalog> enabledCatalog)
    {
        var cookie = LocaleCookie.Read(request);
        if (!string.IsNullOrWhiteSpace(cookie))
            return cookie;

        return Browser(request, enabledCatalog);
    }

    /// <summary>
    /// The browser's <c>Accept-Language</c> matched against the enabled
    /// catalog, in the header's priority order — the **first** enabled match
    /// (exact, then primary-subtag, case-insensitive), or <c>null</c> when
    /// the header is absent, unparseable, or no tag matches an enabled
    /// catalog language. Used for the picker's "matched from your browser"
    /// marker (ADR 0046) and as <see cref="Preference"/>'s fallback step.
    /// </summary>
    public static string? Browser(HttpRequest request, IEnumerable<LanguageCatalog> enabledCatalog)
    {
        var header = request.Headers.AcceptLanguage.ToString();
        var tags = ParseAcceptLanguage(header);
        if (tags.Count == 0)
            return null;

        var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in enabledCatalog)
        {
            if (row.Enabled && !string.IsNullOrWhiteSpace(row.Id))
                enabled.Add(row.Id);
        }

        foreach (var tag in tags)
        {
            if (enabled.Contains(tag))
                return tag;

            var dash = tag.IndexOf('-');
            if (dash > 0 && enabled.Contains(tag[..dash]))
                return tag[..dash];
        }

        return null;
    }

    /// <summary>
    /// The browser's <c>Accept-Language</c> tags matched against the enabled
    /// catalog, **in the header's priority order** — every tag that has an
    /// enabled catalog match (exact, then primary-subtag, case-insensitive),
    /// de-duplicated, as an ordered candidate list. This is the ADR 0046
    /// per-string chain input: the provider walks the list so a key the top
    /// candidate lacks but a later one has resolves to that later row, not the
    /// <c>en</c> floor (M·2). An empty list means "no browser signal" — the
    /// caller then falls back to the frozen instance-default → <c>en</c> chain.
    /// </summary>
    public static IReadOnlyList<string> BrowserCandidates(
        HttpRequest request, IEnumerable<LanguageCatalog> enabledCatalog)
    {
        var tags = ParseAcceptLanguage(request.Headers.AcceptLanguage.ToString());
        if (tags.Count == 0)
            return Array.Empty<string>();

        var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in enabledCatalog)
        {
            if (row.Enabled && !string.IsNullOrWhiteSpace(row.Id))
                enabled.Add(row.Id);
        }

        var matched = new List<string>(tags.Count);
        foreach (var tag in tags)
        {
            string? code = null;
            if (enabled.Contains(tag))
            {
                code = tag;
            }
            else
            {
                var dash = tag.IndexOf('-');
                if (dash > 0 && enabled.Contains(tag[..dash]))
                    code = tag[..dash];
            }
            if (code is not null && !matched.Any(m =>
                string.Equals(m, code, StringComparison.OrdinalIgnoreCase)))
            {
                matched.Add(code);
            }
        }

        return matched;
    }

    /// <summary>
    /// Parses an <c>Accept-Language</c> header (RFC 9110) into a
    /// quality-ordered, deduplicated list of tags. Invalid tags are dropped;
    /// a blank / absent header yields an empty list — the caller treats that
    /// as "no browser signal", never as a fault.
    /// </summary>
    private static List<string> ParseAcceptLanguage(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return new List<string>();

        // (normalized tag, quality, original position). A duplicate tag keeps
        // its highest quality (the later, higher-q one wins — the browser is
        // expected to send the list q-ordered anyway).
        var byTag = new Dictionary<string, (double Q, int Order)>();
        var order = 0;
        foreach (var rawPart in header.Split(','))
        {
            var part = rawPart.Trim();
            if (part.Length == 0)
                continue;

            var q = 1.0;
            string? tag = null;
            foreach (var param in part.Split(';'))
            {
                var token = param.Trim();
                if (token.Length == 0)
                    continue;
                if (token.Length > 2
                    && token[..2].Equals("q=", StringComparison.OrdinalIgnoreCase))
                {
                    if (double.TryParse(token[2..], NumberStyles.Float,
                            CultureInfo.InvariantCulture, out var value))
                    {
                        q = value;
                    }
                }
                else
                {
                    tag = token;
                }
            }

            if (q <= 0)
                continue; // a q=0 tag is an explicit "not acceptable"

            if (tag is null || !IsValidTag(tag))
                continue;

            var normalized = tag.ToLowerInvariant();
            if (!byTag.TryGetValue(normalized, out var existing)
                || q > existing.Q)
            {
                byTag[normalized] = (q, order);
            }
            order++;
        }

        return byTag
            .OrderByDescending(kv => kv.Value.Q)
            .ThenBy(kv => kv.Value.Order) // stable for equal q
            .Select(kv => kv.Key)
            .ToList();
    }

    /// <summary>BCP-47-shaped sanity check (language tag, RFC 9110
    /// <c>1*3ALPHA *( "-1*8ALPHA" )</c>): letters and hyphens only, at least
    /// one letter, no leading/trailing/double hyphen. Deliberately lax — the
    /// enabled-catalog match is the real gate.</summary>
    private static bool IsValidTag(string tag)
    {
        if (tag.Length is 0 or > 35)
            return false;
        if (tag[0] is '-' || tag[^1] is '-')
            return false;
        if (tag.Contains("--"))
            return false;
        return tag.All(ch => char.IsLetter(ch) || ch is '-');
    }
}
