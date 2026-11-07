using System.Security.Cryptography;
using System.Text;

namespace Kumunita.Core.Logging;

/// <summary>
/// The P-3 source-side redaction helper (IMPROVE-SEC IS06, design §2.2). The
/// invariant it serves: **the log never carries the local part of an address**
/// — it carries the domain (or a stable hash) instead, and **nothing is
/// retained**. A caller redacts *at the template site* (IS07/IS08) so that no
/// sink — the JSON-lines file sink, the Sentry breadcrumb floor set in IS05,
/// or a copy-pasted runbook — can ever see the raw address. This is a **pure**
/// helper: no state, no DI, BCL-only (hand-rolled, the <see cref="LogLine"/>
/// precedent). The two members return a total <see cref="string"/> — the caller
/// never sees a <c>null</c>.
/// </summary>
public static class LogRedact
{
    /// <summary>The sentinel returned for null / empty / malformed input.</summary>
    private const string None = "(none)";

    /// <summary>
    /// The **lower-cased domain** of a syntactically valid email — the text
    /// after the <c>@</c> (e.g. a <c>local@domain</c> address yields the
    /// lower-cased <c>domain</c>).
    /// The local part is **never** returned: it is discarded after confirming
    /// it is non-empty. <see cref="None"/> for null / empty / no <c>@</c> /
    /// empty local part / empty domain. No regex: a <c>LastIndexOf('@')</c>
    /// split is the whole rule, keeping this total and allocation-light.
    /// </summary>
    public static string EmailDomain(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return None;

        int at = value.LastIndexOf('@');
        if (at <= 0)          // no '@', or empty local part ("@example.test")
            return None;

        string domain = value[(at + 1)..];
        return string.IsNullOrEmpty(domain)   // empty domain ("a@")
            ? None
            : domain.ToLowerInvariant();
    }

    /// <summary>
    /// The **first 8 lowercase-hex characters of the SHA-256 digest** over the
    /// UTF-8 bytes of <paramref name="value"/>, for correlating log lines
    /// without carrying the address. <see cref="None"/> for null / empty input,
    /// so the helper is total (the <c>_IsNone</c> half is pinned by
    /// <c>Hash8_IsStableAndShort</c>). The digest is computed and returned —
    /// **nothing is retained** here.
    /// </summary>
    public static string Hash8(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return None;

        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        // First 4 bytes == the first 8 hex characters of the 32-byte digest.
        return Convert.ToHexString(digest).Substring(0, 8).ToLowerInvariant();
    }
}
