namespace Kumunita.Core.ErrorReports;

/// <summary>
/// The receiving-side escalation-token read + audited-write lanes (ADR 0159,
/// ESC·3 / ESC·9). A **new** Core seam (namespace <c>Kumunita.Core.ErrorReports</c>)
/// — it is a *separate* seam from the M31 / M32 <see cref="IErrorReportService"/>
/// 4-method surface, which it does **not** re-shape (ESC·1). Core stays
/// HTTP-free (ADR 0006-D / ESC·6): the Web layer is the only place the
/// <c>Bearer</c> token is parsed from the request. Each audited write (generate
/// / revoke) commits the <see cref="EscalationToken"/> doc + exactly one
/// <c>AccessAudit</c> row in one write session (the ADR 0006 C3 single-write-
/// lane, the M32·8 precedent); reads are never audited (the M31·4 "read is not
/// an access decision" precedent).
/// </summary>
public interface IEscalationTokenService
{
    /// <summary>
    /// The receiving admin's token list (newest first). A read — no audit row
    /// (the M31·4 "read is not an access decision" precedent). Never exposes
    /// the plaintext (the <c>TokenPrefix</c> + <c>Label</c> + status only,
    /// ESC·3 / ESC·9). Returns an empty list when there are no tokens.
    /// </summary>
    Task<IReadOnlyList<EscalationTokenSummary>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// Generate a new labeled token. Returns the plaintext (shown ONCE by the
    /// Web layer, never re-rendered) + the stored row summary. Writes the
    /// <see cref="EscalationToken"/> row (hash only — ESC·3) + exactly ONE
    /// <c>AccessAudit</c> row (<c>Via = Admin</c>, action
    /// "escalation.token.generate", <c>TargetKind</c> "escalation-token") in
    /// one write session (the ADR 0006 C3 single-write-lane shape, the M32·8
    /// precedent). The plaintext is CSPRNG-generated
    /// (<c>kesc_</c> + 32 random bytes base64url, 256 bits).
    /// </summary>
    Task<(string Plaintext, EscalationTokenSummary Stored)> GenerateAsync(
        string label, string actorId, CancellationToken ct = default);

    /// <summary>
    /// Revoke a token (set <c>RevokedAt</c> / <c>RevokedBy</c>). Idempotent (a
    /// no-op — no audit row, no state change — when the token is missing or
    /// already revoked, the M32·8 idempotency precedent). On an effective
    /// revoke it writes exactly ONE <c>AccessAudit</c> row (<c>Via = Admin</c>,
    /// action "escalation.token.revoke", <c>TargetKind</c> "escalation-token")
    /// in one write session and returns <c>true</c>; on a no-op it returns
    /// <c>false</c>. After revocation the token **no longer validates**
    /// (immediate, ESC·9).
    /// </summary>
    Task<bool> RevokeAsync(string tokenId, string actorId, CancellationToken ct = default);

    /// <summary>
    /// Validate a presented plaintext token. Returns the matching
    /// non-revoked token (or <c>null</c> when absent / revoked — ESC·5, ESC·9).
    /// Stamps <c>LastUsedAt</c> on a match (a read-then-write on the token row,
    /// NO audit row — the M31·4 "read is not an access decision" precedent).
    /// The plaintext is SHA-256-hashed and matched by the stored
    /// <c>TokenHash</c> (constant-time floor — a DoS / timing floor; the
    /// plaintext is never compared character-by-character in app code).
    /// </summary>
    Task<EscalationToken?> ValidateAsync(string plaintextToken, CancellationToken ct = default);
}

/// <summary>
/// The read-model a token row projects to for the admin list (ESC·3 / ESC·9).
/// It is **never** the doc, and it exposes **no** plaintext and **no**
/// <c>TokenHash</c> (ESC·3 — the plaintext is shown once at generation, never
/// persisted; the <see cref="TokenPrefix"/> is the masked display form).
/// </summary>
public sealed record EscalationTokenSummary(
    string Id,
    string Label,
    string TokenPrefix,          // the masked display form, e.g. "kesc_ab12…"
    DateTimeOffset Created,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt,
    string? RevokedBy);
