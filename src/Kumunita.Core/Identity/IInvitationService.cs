namespace Kumunita.Core.Identity;

/// <summary>
/// The create / revoke / validate / accept seams for the <see
/// cref="Invitation"/> (ADR 0304, IMPROVE-UX A07) — the email invitation:
/// a single-use, expiring, revocable credential that opens sign-up for one
/// email address while the platform gate
/// (<see cref="IIdentityPlatformGateService.IsSignupOpenAsync"/>) is
/// closed (the ADR 0304 D4 invariants).
/// <para>
/// **Hash only at rest (ADR 0304 D2):** <see cref="CreateAsync"/>
/// generates the token (CSPRNG, <c>kinv_</c> + 32 random bytes base64url —
/// the 0159 <c>kesc_</c> / ADR 0302 <c>kskl_</c> shape), stores only the
/// SHA-256 hex digest, and returns the raw token **exactly once** for the
/// caller to show — the store holds only the hash, so the token is
/// unrecoverable by design (the <see cref
/// "Kumunita.Core.Kiosk.KioskLinkService"/> / <see cref
/// "Kumunita.Core.ErrorReports.EscalationTokenService"/> / <see cref
/// "IdentityService"/> F2 precedent).
/// <para>
/// **Audit (the <see cref="KioskLinkService"/> one-row convention):**
/// exactly one <c>invitation.create</c> row on create, one
/// <c>invitation.revoke</c> row on revoke, and one
/// <c>invitation.accept</c> row on accept (all <c>Via = Owner</c>,
/// <c>TargetKind = "invitation"</c>). <see cref="ValidateAsync"/> writes
/// **zero** audit rows (the M35·5 / 0159 "the read is a config load, not an
/// access decision" floor — the access decision is A08's sign-up gate's
/// job).
/// <para>
/// **Pure Core — no Web coupling (ADR 0304 D3):** A08's <c>?invite=</c>
/// sign-up wiring, A09's <c>/admin/invitations</c> surface, and A12's
/// resident "Invite a neighbour" all call this seam; the Web host
/// precomputes the token hash with the one shared
/// <c>IdentityService.Sha256Hex</c> digest (the
/// <c>FindTokenAsync</c> local-constant pattern) and hands it to
/// <see cref="ValidateAsync"/> / <see cref="AcceptAsync"/> — the digest is
/// never inside a <c>.Where(...)</c> expression.
/// </para>
/// </summary>
public interface IInvitationService
{
    /// <summary>
    /// Creates an invitation for <paramref name="email"/> (lowercased,
    /// trimmed at storage) issued by <paramref name="inviterSubjectId"/>:
    /// generates the long random token, stores only its SHA-256 hex digest
    /// (<see cref="Invitation.TokenHash"/>), commits exactly one
    /// <c>invitation.create</c> <c>AccessAudit</c> row
    /// (<c>Via: Owner</c>, <c>TargetKind = "invitation"</c>), and returns
    /// the raw token **exactly once** (the caller shows it — the store
    /// cannot recover it, the 0159 ESC·3 / ADR 0302 D2 rule).
    /// </summary>
    /// <param name="email">The invited address (lowercased + trimmed).</param>
    /// <param name="inviterSubjectId">The resident or admin creating the
    /// invitation (the audit principal for all lifecycle rows).</param>
    /// <param name="expiresAt">The expiry instant (UTC). A
    /// <see cref="EmailInvitationStatus.Pending"/> row past this instant is
    /// refused by <see cref="ValidateAsync"/> / <see cref="AcceptAsync"/>
    /// — the virtual <see cref="EmailInvitationStatus.Expired"/> state. The
    /// ADR 0304 D6 open question settles the proposed default (7 days); the
    /// caller passes the concrete value.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The raw token (shown once) + the stored
    /// <see cref="Invitation"/> row (which carries only the hash).</returns>
    Task<(string Token, Invitation Invitation)> CreateAsync(
        string email, string inviterSubjectId, DateTimeOffset expiresAt,
        CancellationToken ct = default);

    /// <summary>
    /// Revokes <paramref name="invitationId"/>: sets <see cref
    /// "Invitation.Status"/> to <see cref="EmailInvitationStatus.Revoked"/>
    /// (immediate — the next <see cref="ValidateAsync"/> /
    /// <see cref="AcceptAsync"/> refuses it) + exactly one
    /// <c>invitation.revoke</c> <c>AccessAudit</c> row (same shape as
    /// create). **Inviter-scoped** (ADR 0304 D3): an invitation the actor
    /// does not own is a no-op. Idempotent: a missing, already-revoked, or
    /// already-accepted invitation is a no-op returning <c>false</c> (the
    /// 0159 ESC·9 / ADR 0302 D3 precedent — no audit row, no state change).
    /// </summary>
    /// <param name="invitationId">The <see cref="Invitation"/> row's id.</param>
    /// <param name="inviterSubjectId">The inviter revoking their own
    /// invitation.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns><c>true</c> when an invitation was revoked; <c>false</c>
    /// when it is missing, already revoked, already accepted, or not the
    /// actor's.</returns>
    Task<bool> RevokeAsync(
        string invitationId, string inviterSubjectId, CancellationToken ct = default);

    /// <summary>
    /// Validates a presented token (ADR 0304 D3): returns the
    /// <see cref="Invitation"/> row when its hash matches
    /// <paramref name="tokenHash"/>, <see cref
    /// "Invitation.Status"/> is <see cref="EmailInvitationStatus.Pending"/>,
    /// and <see cref="Invitation.ExpiresAt"/> has not passed — else
    /// <b><c>null</c></b> (the caller renders the denied shape). Writes
    /// **zero** audit rows (the M35·5 / 0159 "the read is not an access
    /// decision" floor).
    /// </summary>
    /// <param name="tokenHash">The SHA-256 hex digest of the presented
    /// token (the caller precomputes it — the
    /// <c>IdentityService.FindTokenAsync</c> local-constant pattern, so the
    /// digest is never inside a <c>.Where(...)</c> expression).</param>
    /// <param name="ct">Cancellation.</param>
    Task<Invitation?> ValidateAsync(
        string tokenHash, CancellationToken ct = default);

    /// <summary>
    /// Accepts the invitation: verifies the token (row exists,
    /// <see cref="EmailInvitationStatus.Pending"/>, not past
    /// <see cref="Invitation.ExpiresAt"/>), **and** that the signing-up
    /// <paramref name="email"/> matches <see cref
    /// "Invitation.Email"/> (the token is only good for the invited
    /// address, ADR 0304 D4), runs the existing <see cref
    /// "IIdentityService.RegisterAsync"/> lane (the account-creation is the
    /// primary domain op), marks the row <see cref
    /// "EmailInvitationStatus.Accepted"/> with the new account's
    /// <see cref="Invitation.AcceptedSubjectId"/>, commits exactly one
    /// <c>invitation.accept</c> <c>AccessAudit</c> row, and returns the
    /// <see cref="ThinPrincipal"/>.
    /// <para>
    /// **Single-use (ADR 0304 D4):** a second <see cref="AcceptAsync"/>
    /// for the same token is refused — the row is <see cref
    /// "EmailInvitationStatus.Accepted"/>, so <see cref="ValidateAsync"/>
    /// returns <c>null</c>.
    /// </para>
    /// <para>
    /// **Cross-store ordering (the ADR 0142 D6 precedent):** if
    /// <see cref="IIdentityService.RegisterAsync"/> throws (e.g. email
    /// already exists), the exception propagates **and** the invitation
    /// row is **not** marked <see cref
    /// "EmailInvitationStatus.Accepted"/> — the domain write succeeds first,
    /// the derivative (the invitation mark) commits after.
    /// </para>
    /// </summary>
    /// <param name="tokenHash">The SHA-256 hex digest of the presented
    /// token (the caller precomputes it — the same local-constant pattern
    /// as <see cref="ValidateAsync"/>).</param>
    /// <param name="email">The signing-up email (must match
    /// <see cref="Invitation.Email"/>).</param>
    /// <param name="displayName">The new account's display name (passed to
    /// <see cref="IIdentityService.RegisterAsync"/>).</param>
    /// <param name="password">The new account's password (passed to
    /// <see cref="IIdentityService.RegisterAsync"/>).</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The <see cref="ThinPrincipal"/> for the newly created
    /// account (the <see cref="IIdentityService.RegisterAsync"/> return
    /// value).</returns>
    Task<ThinPrincipal> AcceptAsync(
        string tokenHash, string email, string displayName, string password,
        CancellationToken ct = default);
}
