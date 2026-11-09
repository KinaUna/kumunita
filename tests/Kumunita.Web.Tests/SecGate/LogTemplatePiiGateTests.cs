using System.Text.RegularExpressions;

namespace Kumunita.Web.Tests.SecGate;

/// <summary>
/// IS08 (IMPROVE-SEC) — the P-3 **source-side log-PII gate**. Retires the M4
/// baseline (7 → 0) and makes the defect class unrepeatable: a future
/// <c>logger.LogInformation("{Email}", e)</c> is a **red build**, not a review
/// comment (the lane's doctrine in one sentence: *the parts are cheap, the
/// linkage is rare* — this is the linkage).
///
/// The scan is line-wrap-immune and deliberately *not* a whole-file regex: it
/// walks <c>src/**/*.cs</c>, finds every structured-logging call, walks each
/// call to its matching close paren (respecting string literals and line
/// comments), and checks the **placeholders** inside that call's template
/// literal for a bare PII word. Position-based, string-aware matching is what
/// keeps a template split across lines (both <c>FirstBootSeeder</c> sites) in
/// scope while a PII-looking token on an *argument*, an *exception message*
/// (<c>throw new …("…{…}…")</c>), or a *comment* stays out.
///
/// The id rule (design §2.2): a placeholder is PII **only if it is exactly a
/// PII word** — <c>{Email}</c>, <c>{Ip}</c>, <c>{Phone}</c>, <c>{Address}</c>,
/// <c>{Recipient}</c>, <c>{Body}</c> (case-insensitive). Id-shaped
/// placeholders are excluded by construction: <c>{UserId}</c>,
/// <c>{RecipientId}</c>, <c>{Idp}</c>, <c>{IdempotencyKey}</c>,
/// <c>{EmailDomain}</c>, <c>{RecipientHash}</c>, <c>{MaxBodyChars}</c> — none
/// equals a bare word, so none is flagged. Redacted forms
/// (<c>LogRedact.EmailDomain</c> / <c>Hash8</c>) log a domain/hash, not PII.
///
/// The allowlist is **empty and stays that way** (see field): a site that
/// genuinely cannot be redacted is a *pause and record* (a question in the
/// handoff), never a silent exception.
/// </summary>
public class LogTemplatePiiGateTests
{
    /// <summary>
    /// The PII word set (design §2.2). Matched **case-insensitively and
    /// exactly** — a placeholder is PII iff its text between <c>{</c> and
    /// <c>}</c> is one of these words. This is what implements the id rule:
    /// anything with a prefix or suffix (<c>…Id</c>, <c>…Key</c>, <c>…Domain</c>,
    /// <c>…Chars</c>) is an id-shaped identifier, not a PII field.
    /// </summary>
    private static readonly HashSet<string> PiiWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "Email",
        "Ip",
        "Phone",
        "Address",
        "Recipient",
        "Body",
    };

    /// <summary>
    /// The allowlist of <c>file:line</c> sites that may carry a PII
    /// placeholder. **Empty, and it stays empty**: a redacted-at-the-source
    /// site needs no exception, and a site that cannot be redacted is a
    /// recorded pause, not a silent allowance.
    /// </summary>
    private static readonly IReadOnlySet<string> Allowlist = new HashSet<string>();

    /// <summary>
    /// A structured-logging entry point — the six level methods, or the
    /// generic <c>Log(LogLevel, …)</c>. Anchored on <c>.</c> so a local type
    /// named, e.g., <c>LogInformation</c> (if one ever existed) is not
    /// mistaken for a call.
    /// </summary>
    private static readonly Regex LogCall = new(
        @"\.\s*Log(?:Information|Warning|Error|Debug|Critical|Trace|)\s*\(",
        RegexOptions.Compiled);

    [Fact(DisplayName = "No log template carries a PII placeholder (M4: 7 -> 0)")]
    public void NoLogTemplate_CarriesAPiiPlaceholder()
    {
        // Scope is **src/** only (the IS08 spec): the gate pins production
        // log templates. tests/ legitimately carries the *probes* the sink and
        // this gate are proven with (e.g. SentryBreadcrumbPiiTests logs a
        // "{Email}" Information line to prove the floor) — those are out of
        // scope by design.
        string rootPrefix = SecGateSource.RepoRoot.Replace('\\', '/') + "/";
        var violations = new List<string>();
        foreach (string file in SecGateSource.AllSrcFiles("*.cs"))
        {
            string rel = file.Replace('\\', '/').Replace(rootPrefix, "");
            if (!rel.StartsWith("src/", StringComparison.Ordinal))
                continue;
            string src = SecGateSource.ReadSrc(rel);
            int baseLine = 1;
            foreach (Match m in LogCall.Matches(src))
            {
                int line = baseLine;
                for (int i = 0; i < m.Index; i++)
                    if (src[i] == '\n')
                        line++;

                // m's match ends at the call's opening '(' — start the span exactly there.
                int openParen = m.Index + m.Length - 1;
                string spanText = CallSpan(src, openParen);   // the call, up to its matching ')'
                foreach (Match p in Regex.Matches(spanText, @"\{([^{}]+)\}"))
                {
                    string name = p.Groups[1].Value.Trim();
                    if (PiiWords.Contains(name))
                        violations.Add($"{rel}:{line}  {{{name}}}");
                }
            }
        }

        var flagged = violations.Where(v => !Allowlist.Contains(v)).Distinct().ToList();
        Assert.True(
            flagged.Count == 0,
            "Log template(s) carrying a bare PII placeholder — redact at the source"
            + " (LogRedact.EmailDomain / Hash8, or pass an id) instead of the raw value.\n  "
            + string.Join("\n  ", flagged));
    }

    /// <summary>
    /// The text of one log call, from the opening <c>(</c> at
    /// <paramref name="openIdx"/> up to (and including) the matching close
    /// paren. String literals and line comments are respected: a <c>)</c>
    /// inside a template string or inside a <c>//</c> comment does **not**
    /// close the call, and a template split across 2–3 lines stays a single
    /// span. This is what makes the gate immune to line-wrapping.
    /// </summary>
    private static string CallSpan(string src, int openParen)
    {
        int n = src.Length;
        int i = openParen + 1;           // scan from just after the opening '('
        int depth = 1;                   // armed for that '('; the matching ')' brings it to 0
        bool inStr = false;
        bool inComment = false;
        while (i < n && depth > 0)
        {
            char c = src[i];
            char next = i + 1 < n ? src[i + 1] : '\0';
            if (c == '\n')
            {
                inComment = false;       // a line comment ends at the newline
            }
            else if (inComment)
            {
                // consume to end-of-line (the '\n' case resets inComment)
            }
            else if (inStr)
            {
                if (c == '\\')
                    i++;                 // skip the escaped char (e.g. \" )
                else if (c == '"')
                    inStr = false;
            }
            else if (c == '/' && next == '/')
            {
                inComment = true;
                i++;
            }
            else if (c == '"')
            {
                inStr = true;
            }
            else if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
            }
            i++;
        }

        return src.Substring(openParen, i - openParen);
    }
}
