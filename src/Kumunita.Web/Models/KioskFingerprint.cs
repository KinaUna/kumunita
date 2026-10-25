namespace Kumunita.Web.Models;

/// <summary>
/// ADR 0164 amendment — the kiosk **content fingerprint** helper. A tiny,
/// dependency-free FNV-1a 32 hash rendered as 8 lowercase hex characters,
/// used by <c>KioskController.Classify*</c> to stamp each resolved slide with a
/// stable digest of its display-relevant content (board lanes + cards, todo
/// rows, or the in-range events).
/// <para>
/// <b>Why a hash and not the raw content:</b> the <c>GET /kiosk/slide</c>
/// response travels to the client on every REFRESH tick; shipping a short
/// opaque digest (8 chars) instead of the full content keeps the response
/// small while still changing whenever the underlying display content changes.
/// The client (<c>client/lib/kiosk.ts</c>) stores the last rendered
/// fingerprint per slide and **skips the iframe reload** when the server's
/// fingerprint matches — the fix for the "kiosk reload is too disruptive"
/// symptom (the M35 U04 unconditional re-render every
/// <c>data-refresh-interval</c> s). A digest, not the content, is returned:
/// the content is already what the iframe's own navigation re-fetches, and the
/// denied shape (no content) is fingerprinted off a fixed denied-shape string
/// so a visible→denied flip is still detected (the kind prefix prevents
/// collision with a real content digest).
/// </para>
/// <para>
/// <b>Stability, not security:</b> FNV-1a 32 is used for **change detection**
/// only, never for integrity or signatures. Deterministic across calls and
/// across processes (no salt, no randomness, no environment dependence) — the
/// same content string always yields the same 8-char digest, which is all the
/// client's equality check needs.
/// </para>
/// </summary>
public static class KioskFingerprint
{
    /// <summary>
    /// Computes the 8-char hex FNV-1a 32 digest of <paramref name="content"/>
    /// (the canonical string the caller built from the slide's
    /// display-relevant fields — lane order, card id/title/status, or the
    /// in-range event id/title/time). An empty or <c>null</c> input still
    /// yields a stable digest (the empty-string seed), so a content-free slide
    /// (an empty board, an empty window) is fingerprinted consistently.
    /// </summary>
    public static string Of(string? content)
    {
        // FNV-1a 32 (the offset basis + the prime are the canonical constants;
        // the 32-bit word width keeps the digest a stable 8-hex-char string
        // with no long-overflow edge cases on any runtime).
        const uint fnvOffsetBasis = 2166136261u;
        const uint fnvPrime = 16777619u;
        var hash = fnvOffsetBasis;
        var text = content ?? string.Empty;
        for (var i = 0; i < text.Length; i++)
        {
            hash ^= text[i];
            hash *= fnvPrime;
        }
        return hash.ToString("x8");
    }
}
