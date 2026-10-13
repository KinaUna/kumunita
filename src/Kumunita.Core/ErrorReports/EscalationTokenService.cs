using System.Security.Cryptography;
using System.Text;
using Kumunita.Core.Authorization;
using Marten;

namespace Kumunita.Core.ErrorReports;

/// <summary>
/// The read + two audited-write lanes for the <see cref="EscalationToken"/>
/// doc (ADR 0159, ESC·3 / ESC·9). Composes the host-registered Marten
/// <c>IDocumentStore</c> (the <c>ErrorReportService</c> /
/// <c>SiteContentService</c> shape) — reads open their own
/// <c>QuerySession</c>; each audited write opens one write
/// <c>IDocumentSession</c> and commits the doc + its single
/// <c>AccessAudit</c> row together (invariant C3, strong consistency).
/// <para>
/// <c>GenerateAsync</c> / <c>RevokeAsync</c> are **single-write-lane** (the
/// ADR 0006 C3 shape — the M32 <c>MarkResolvedAsync</c> precedent: one write
/// session storing the doc + exactly one <c>AccessAudit</c> row).
/// <c>ValidateAsync</c> is a constant-time hash compare that stamps
/// <c>LastUsedAt</c> on a match (a read-then-write, **no** audit row — the
/// M31·4 precedent). <c>ListAsync</c> is a read that projects to the
/// <see cref="EscalationTokenSummary"/> (no plaintext / hash — ESC·3). Core
/// stays HTTP-free (ADR 0006-D / ESC·6): the Web inbound endpoint is the only
/// place the <c>Bearer</c> token is parsed.
/// </para>
/// </summary>
public sealed class EscalationTokenService : IEscalationTokenService
{
    private readonly IDocumentStore _store;

    public EscalationTokenService(IDocumentStore store) => _store = store;

    /// <summary>
    /// Newest-first listing (Created DESC); a read, never audited (M31·4).
    /// Projects each row to <see cref="EscalationTokenSummary"/> — no
    /// plaintext, no <c>TokenHash</c> (ESC·3).
    /// </summary>
    public async Task<IReadOnlyList<EscalationTokenSummary>> ListAsync(CancellationToken ct = default)
    {
        using var session = _store.QuerySession();
        var rows = await session.Query<EscalationToken>()
            .OrderByDescending(t => t.Created)   // the repo's pinned ordering shape (ErrorReportService .OrderByDescending(.Created))
            .ToListAsync(ct).ConfigureAwait(false);
        return rows.Select(Summary).ToList();
    }

    /// <summary>
    /// Generate a new labeled token: the CSPRNG plaintext (shown once), the
    /// SHA-256 hash row, and exactly one <c>AccessAudit</c> row
    /// (action "escalation.token.generate") in one write session (ESC·3 /
    /// ESC·9, the M32·8 single-write-lane precedent).
    /// </summary>
    public async Task<(string Plaintext, EscalationTokenSummary Stored)> GenerateAsync(string label, string actorId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var plaintext = NewToken();
        var token = new EscalationToken
        {
            Id            = Guid.NewGuid().ToString("N"),
            Label         = label,
            TokenHash     = Sha256Hex(plaintext),
            TokenPrefix   = plaintext.Length >= 12 ? plaintext[..12] + "…" : plaintext + "…",
            Created       = now,
            CreatedBy     = actorId
            // LastUsedAt / RevokedAt / RevokedBy stay null until first use / revoke (ESC·9)
        };

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(token);
        // Exactly one AccessAudit row (Via = Admin — the ESC·9 pin).
        session.Store(AuditRow(now, actorId, "escalation.token.generate", token.Id));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return (plaintext, Summary(token));
    }

    /// <summary>
    /// Revoke a token (set <c>RevokedAt</c> / <c>RevokedBy</c>) + exactly one
    /// <c>AccessAudit</c> row (action "escalation.token.revoke") in one write
    /// session. Returns false (a no-op — no audit row, no state change) when
    /// the token is missing or already revoked (the M32·8 idempotent precedent,
    /// the M31·6 <c>MarkTriagedAsync</c> shape verbatim). ESC·9.
    /// </summary>
    public async Task<bool> RevokeAsync(string tokenId, string actorId, CancellationToken ct = default)
    {
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var stored = await session.LoadAsync<EscalationToken>(tokenId, ct).ConfigureAwait(false);
        if (stored is null || stored.RevokedAt is not null)
        {
            return false; // no-op — a missing or already-revoked token (ESC·9, M32·8)
        }
        var now = DateTimeOffset.UtcNow;
        stored.RevokedAt = now;
        stored.RevokedBy = actorId;
        session.Store(stored);
        // Exactly one AccessAudit row (Via = Admin — the ESC·9 pin).
        session.Store(AuditRow(now, actorId, "escalation.token.revoke", tokenId));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Validate a presented plaintext token: SHA-256-hash it, match by the
    /// stored <c>TokenHash</c> (constant-time floor — the plaintext is never
    /// compared char-by-char), restricted to non-revoked rows. On a match,
    /// stamp <c>LastUsedAt</c> (a read-then-write, **no** audit row — the M31·4
    /// precedent) and return the token; otherwise return null (ESC·5 / ESC·9).
    /// </summary>
    public async Task<EscalationToken?> ValidateAsync(string plaintextToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(plaintextToken))
        {
            return null; // a blank token cannot validate (ESC·5)
        }
        var tokenHash = Sha256Hex(plaintextToken);
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var match = await session.Query<EscalationToken>()
            .Where(t => t.TokenHash == tokenHash && t.RevokedAt == null)   // the validate lookup (ESC·3 / ESC·9)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (match is null)
        {
            return null; // absent or revoked — no audit row (the M31·4 read lane)
        }
        match.LastUsedAt = DateTimeOffset.UtcNow;   // stamp on use (ESC·9) — no audit row
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return match;
    }

    // ── private helpers ────────────────────────────────────────────────────

    private static EscalationTokenSummary Summary(EscalationToken t) => new(
        t.Id, t.Label, t.TokenPrefix, t.Created, t.LastUsedAt, t.RevokedAt, t.RevokedBy);

    private static AccessAudit AuditRow(
        DateTimeOffset at, string actorId, string action, string targetId) => new()
    {
        Id                   = Guid.NewGuid().ToString("N"),
        At                   = at,
        ActorId              = actorId,
        EffectivePrincipalId = actorId,
        Action               = action,
        TargetKind           = "escalation-token",   // the ESC·9 pin
        TargetId             = targetId,
        Via                  = AccessVia.Admin,      // the ESC·9 pin
        Outcome              = AccessOutcome.Allow
    };

    /// <summary>The token format: <c>kesc_</c> + 32 CSPRNG bytes base64url (256 bits, ESC·3).</summary>
    private static string NewToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return "kesc_" + ToBase64Url(bytes);
    }

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
