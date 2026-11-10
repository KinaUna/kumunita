using Marten;

namespace Kumunita.Core.Kiosk;

/// <summary>
/// The create / revoke / validate seams for the <see cref="KioskLink"/>
/// (ADR 0302, IMPROVE-UX R15) — the shared kiosk link: a per-playlist,
/// revocable, long-random-token read-only credential that (from R16 on)
/// authenticates **only** on <c>/kiosk</c> + its slides, shows
/// community-visible items only, and is audited under the owner.
/// <para>
/// **Hash only at rest (ADR 0302 D2):** <see cref="CreateAsync"/>
/// generates the token (CSPRNG, <c>kskl_</c> + 32 random bytes base64url —
/// the 0159 <c>kesc_</c> shape), stores only the SHA-256 hex digest, and
/// returns the raw token **exactly once** for the caller to show — the
/// store holds only the hash, so the token is unrecoverable by design
/// (the <see cref="Kumunita.Core.ErrorReports
/// .EscalationTokenService"/> / <see cref="Kumunita.Core.Identity
/// .IdentityService"/> F2 precedent).
/// <para>
/// **Audit (the <see cref="KioskService"/> one-row convention):** exactly
/// one <c>kiosk.link.create</c> row on create and one
/// <c>kiosk.link.revoke</c> row on revoke (<c>Via = Owner</c>,
/// <c>TargetKind = "kiosk"</c>). <see cref="ValidateAsync"/> writes
/// **zero** audit rows (the M35·5 "the read is a config load, not an access
/// decision" floor — the owner-scope read decision is the R16
/// middleware's job, and every read the link authenticates is audited by
/// the item's own read lane under the owner).
/// </para>
/// <para>
/// **Pure Core — no Web coupling (ADR 0302 D3):** the
/// <see cref="ValidateAsync"/> seam takes the token's **hash** (the
/// caller — the R16 middleware — hashes the presented token with the same
/// <c>IdentityService.Sha256Hex</c> digest it shares, and precomputes it
/// as a local constant — the <c>FindTokenAsync</c> pattern — so the query
/// stays translatable).
/// </para>
/// </summary>
public interface IKioskLinkService
{
    /// <summary>
    /// Creates a link for <paramref name="playlistId"/> owned by
    /// <paramref name="ownerSubjectId"/>: generates the long random token,
    /// stores only its SHA-256 hex digest (<see cref="KioskLink
    /// .TokenHash"/>), commits exactly one <c>kiosk.link.create</c>
    /// <c>AccessAudit</c> row (<c>Via: Owner</c>, <c>TargetKind = "kiosk"</c>),
    /// and returns the raw token **exactly once** (the caller shows it —
    /// the store cannot recover it, the 0159 ESC·3 rule).
    /// </summary>
    /// <param name="playlistId">The <see cref="KioskPlaylist"/> the link
    /// plays.</param>
    /// <param name="ownerSubjectId">The resident creating the link (the
    /// audit principal for everything the link authenticates — ADR 0302
    /// D4).</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The raw token (shown once) + the stored
    /// <see cref="KioskLink"/> row (which carries only the hash).</returns>
    Task<(string Token, KioskLink Link)> CreateAsync(
        string playlistId, string ownerSubjectId, CancellationToken ct = default);

    /// <summary>
    /// Revokes <paramref name="linkId"/>: sets <see cref="KioskLink
    /// .RevokedAt"/> (immediate — the next <see cref="ValidateAsync"/>
    /// refuses it) + exactly one <c>kiosk.link.revoke</c>
    /// <c>AccessAudit</c> row (same shape as create). **Owner-scoped**
    /// (ADR 0302 D3): a link the actor does not own is a no-op.
    /// Idempotent: a missing or already-revoked link is a no-op returning
    /// <c>false</c> (the 0159 ESC·9 precedent — no audit row, no state
    /// change).
    /// </summary>
    /// <param name="linkId">The <see cref="KioskLink"/> row's id.</param>
    /// <param name="ownerSubjectId">The resident revoking their own link.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns><c>true</c> when a link was revoked; <c>false</c> when it
    /// is missing, already revoked, or not the actor's.</returns>
    Task<bool> RevokeAsync(
        string linkId, string ownerSubjectId, CancellationToken ct = default);

    /// <summary>
    /// Validates a presented token for the owner's scope (ADR 0302 D3):
    /// returns the <see cref="KioskLink"/> row when its hash matches
    /// <paramref name="tokenHash"/>, it is not revoked, and it belongs to
    /// <paramref name="ownerSubjectId"/> — else <b><c>null</c></b> (the
    /// caller renders the denied shape). Stamps <see cref="KioskLink
    /// .LastUsedAt"/> on a match (a read-then-write, **no** audit row —
    /// the M35·5 / 0159 "the read is not an access decision" floor).
    /// </summary>
    /// <param name="tokenHash">The SHA-256 hex digest of the presented
    /// token (the caller precomputes it — the
    /// <c>IdentityService.FindTokenAsync</c> local-constant pattern, so the
    /// digest is never inside a <c>.Where(...)</c> expression).</param>
    /// <param name="ownerSubjectId">The owner whose links may validate —
    /// a hash match for **another** owner's link is refused (the
    /// owner-scope pin).</param>
    /// <param name="ct">Cancellation.</param>
    Task<KioskLink?> ValidateAsync(
        string tokenHash, string ownerSubjectId, CancellationToken ct = default);
}
