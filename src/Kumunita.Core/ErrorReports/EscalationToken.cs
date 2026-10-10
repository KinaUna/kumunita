namespace Kumunita.Core.ErrorReports;

/// <summary>
/// The receiving-side escalation-authorization token (ADR 0159, ESC·3). A
/// receiving GlobalAdmin generates a labeled token per authorized origin
/// platform and hands the plaintext out-of-band; the origin admin presents it
/// as a <c>Bearer</c> credential when escalating. The field set below is the
/// **9-member ESC·3 ceiling** (ADR 0159 D1) — no field outside this set may
/// appear in the doc, and the **plaintext is never a member** (ESC·3: only the
/// SHA-256 hash is stored; the plaintext is shown exactly once at generation,
/// never persisted). Tokens are CSPRNG-generated (<c>RandomNumberGenerator</c>),
/// 32 random bytes base64url, <c>kesc_</c>-prefixed (a <c>ghp_</c>-style
/// high-entropy format — 256 bits, so brute-force validation is infeasible).
/// Multiple tokens coexist; each is individually revocable (ESC·9). This is a
/// **new** bounded-context doc on the M31 / M32 <c>ErrorReports</c> surface
/// (ESC·1 — the M32 <see cref="ErrorReport"/> 15-member doc is untouched; the
/// two new docs ride a **new** <c>EscalationDocTypes</c> parallel surface).
/// The conventional <c>string Id</c> identity is Marten-generated.
/// </summary>
public sealed class EscalationToken
{
    public string Id { get; set; } = default!;        // conventional — Marten-generated
    public string Label { get; set; } = default!;     // the admin's name for the origin platform
    public string TokenHash { get; set; } = default!; // the SHA-256 hex digest of the plaintext — NEVER the plaintext
    public string TokenPrefix { get; set; } = default!; // the masked display prefix, e.g. "kesc_ab12…"
    public DateTimeOffset Created { get; set; }
    public string CreatedBy { get; set; } = default!; // the admin's subject
    public DateTimeOffset? LastUsedAt { get; set; }   // stamped on each successful inbound (a read-then-write, no audit row — ESC·9)
    public DateTimeOffset? RevokedAt { get; set; }    // set on revoke (immediate, ESC·9); null while valid
    public string? RevokedBy { get; set; }            // the admin's subject who revoked
}
