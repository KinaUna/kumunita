using Kumunita.Core.Authorization;
using Marten;

namespace Kumunita.Core.Kiosk;

/// <summary>
/// The <see cref="IKioskLinkService"/> impl (ADR 0302, IMPROVE-UX R15) —
/// the shared kiosk link's Core seam. A plain store-composing service (the
/// <see cref="KioskService"/> structural template): each operation owns its
/// own session (the <c>EscalationTokenService</c> committed-lane shape —
/// the link's writes are its own lane, not part of a caller's
/// transaction; the R18 settings surface calls one seam per action).
/// <para>
/// **Hash only at rest (ADR 0302 D2):** <see cref="CreateAsync"/> generates
/// the token (<c>kskl_</c> + 32 CSPRNG bytes base64url — the
/// <see cref="Kumunita.Core.ErrorReports.EscalationTokenService"/>
/// <c>kesc_</c> shape), stores only the SHA-256 hex digest
/// (<see cref="KioskLink.TokenHash"/> — the
/// <see cref="Kumunita.Core.Identity.IdentityService.Sha256Hex"/>
/// implementation, shared <c>internal</c> so the sibling hashes identically
/// to the rest of the tree), returns the raw token **once**, and discards
/// it — the store cannot recover it (the 0159 ESC·3 / F2 rule).
/// </para>
/// <para>
/// **Audit (the <see cref="KioskService"/> one-row convention):** exactly
/// one <c>kiosk.link.create</c> / <c>kiosk.link.revoke</c>
/// <c>AccessAudit</c> row per operation (<c>Via = Owner</c>,
/// <c>TargetKind = "kiosk"</c>, <c>TargetId</c> = the link's id).
/// <see cref="ValidateAsync"/> writes **zero** rows (the M35·5 "the read is
/// a config load, not an access decision" floor — the owner-scope read
/// decision is the R16 middleware's job, and every read the link
/// authenticates is audited by the item's own read lane under the owner,
/// ADR 0302 D4).
/// </para>
/// </summary>
public sealed class KioskLinkService(IDocumentStore store) : IKioskLinkService
{
    /// <inheritdoc />
    public async Task<(string Token, KioskLink Link)> CreateAsync(
        string playlistId, string ownerSubjectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playlistId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerSubjectId);

        var now = DateTimeOffset.UtcNow;
        // ADR 0302 D2 — the token is 32 CSPRNG bytes base64url under a
        // kskl_ prefix (256 bits, the EscalationTokenService `kesc_` shape;
        // the R19 SEC review may adjust the length — the only place the
        // generation lives). Shown once, discarded after hashing.
        var token = NewToken();
        var link = new KioskLink
        {
            Id             = Guid.NewGuid().ToString("N"),
            PlaylistId     = playlistId,
            OwnerSubjectId = ownerSubjectId,
            TokenHash      = Kumunita.Core.Identity.IdentityService.Sha256Hex(token),
            CreatedAt      = now,
            // LastUsedAt / RevokedAt stay null until first use / revoke.
        };

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(link);
        // Exactly one AccessAudit row (the KioskService kiosk.save one-row
        // shape, TargetId = the link's id — the EscalationTokenService
        // token-row precedent).
        session.Store(AuditRow(now, ownerSubjectId, "kiosk.link.create", link.Id));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return (token, link);
    }

    /// <inheritdoc />
    public async Task<bool> RevokeAsync(
        string linkId, string ownerSubjectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(linkId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerSubjectId);

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var stored = await session.LoadAsync<KioskLink>(linkId, ct).ConfigureAwait(false);

        // ADR 0302 D3 — owner-scoped + idempotent: a missing link, an
        // already-revoked one, or a link owned by someone else is a no-op
        // (no audit row, no state change — the EscalationTokenService
        // RevokeAsync no-op shape).
        if (stored is null
            || stored.RevokedAt is not null
            || stored.OwnerSubjectId != ownerSubjectId)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        stored.RevokedAt = now;
        session.Store(stored);
        session.Store(AuditRow(now, ownerSubjectId, "kiosk.link.revoke", linkId));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<KioskLink?> ValidateAsync(
        string tokenHash, string ownerSubjectId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(tokenHash) || string.IsNullOrEmpty(ownerSubjectId))
        {
            return null; // a blank hash or owner cannot validate (the ESC·5 shape)
        }

        // The hash arrives precomputed (the IdentityService.FindTokenAsync
        // local-constant pattern — the digest is a query constant, never
        // inside the .Where expression). Owner scope is part of the match:
        // a valid hash for ANOTHER owner's link is refused (ADR 0302 D3
        // pin, test (d)).
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var match = await session.Query<KioskLink>()
            .Where(l => l.TokenHash == tokenHash
                && l.OwnerSubjectId == ownerSubjectId
                && l.RevokedAt == null)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (match is null)
        {
            return null; // absent, revoked, or another owner's — no audit row
        }

        // Stamp LastUsedAt on use (the ESC·9 read-then-write, no audit row
        // — the M35·5 "the read is not an access decision" floor).
        match.LastUsedAt = DateTimeOffset.UtcNow;
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return match;
    }

    // ── private helpers ────────────────────────────────────────────────────

    /// <summary>The one <c>AccessAudit</c> row shape the kiosk-link lane
    /// writes (the <see cref="KioskService"/> <c>kiosk.save</c> field
    /// mapping — the IdentityService.AuditRow "internal so the sibling
    /// shares it" precedent is not reachable across the module boundary,
    /// so the shape is duplicated here, pinned by the tests).</summary>
    private static AccessAudit AuditRow(
        DateTimeOffset at, string ownerSubjectId, string action, string linkId) => new()
    {
        Id                   = Guid.NewGuid().ToString("N"),
        At                   = at,
        ActorId              = ownerSubjectId,
        EffectivePrincipalId = ownerSubjectId,
        Action               = action,
        TargetKind           = "kiosk",
        TargetId             = linkId,
        Via                  = AccessVia.Owner,
        Outcome              = AccessOutcome.Allow
    };

    /// <summary>The token format (ADR 0302 D2): <c>kskl_</c> + 32 CSPRNG
    /// bytes base64url (256 bits — the EscalationTokenService.NewToken
    /// shape, prefix distinguishing the lane).</summary>
    private static string NewToken()
    {
        var bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return "kskl_" + Convert.ToBase64String(bytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
