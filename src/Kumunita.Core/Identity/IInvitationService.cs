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
/// resident "Invite a neighbour" all call this seam. The hash-based seams
/// (<see cref="ValidateAsync"/> / <see cref="AcceptAsync"/>) take a
/// precomputed digest (the <c>IdentityService.FindTokenAsync</c>
/// local-constant pattern — the digest is never inside a
/// <c>.Where(...)</c> expression); the **presented-token** overloads
/// (<see cref="ValidatePresentedAsync"/> / <see cref
/// "AcceptPresentedAsync"/>, IMPROVE-UX A08) accept the raw token and hash
/// it in **this** assembly with the one shared <c>IdentityService
/// .Sha256Hex</c> digest, then delegate to the hash-based seams (the ADR
/// 0302 <c>KioskLinkService.ValidatePresentedAsync</c> **Trap A** shape —
/// the Web host cannot hash, <c>Sha256Hex</c> is <c>internal</c>, so the
/// digest lives in exactly one place).
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

    /// <summary>
    /// Validates a <b>presented raw token</b> for the sign-up gate (ADR 0304
    /// D3, IMPROVE-UX A08) — the A08 <c>?invite=</c> gate's read seam.
    /// <para>
    /// **Trap A (hash in one place):** the Web host cannot hash the presented
    /// token — <see cref="IdentityService.Sha256Hex"/> is <c>internal</c> to
    /// the Core assembly (no <c>InternalsVisibleTo</c> to the Web host), and
    /// re-implementing the digest in the Web layer is a divergence risk that
    /// breaks the "the hash is one fact" invariant the
    /// <see cref="Kumunita.Core.Kiosk.KioskLinkService"/> / <see cref
    /// "Kumunita.Core.ErrorReports.EscalationTokenService"/> keep. This
    /// overload hashes the raw token with that one existing hasher and
    /// delegates to the hash-based <see cref="ValidateAsync"/>, so the digest
    /// lives in exactly one place (this assembly).
    /// </para>
    /// <para>
    /// **Owner-less by design** (the invitation has no owner session at the
    /// gate): the token is the credential, discovered by the lookup — so this
    /// overload takes **only** the presented token and returns the
    /// <see cref="Invitation"/> row (which carries
    /// <see cref="Invitation.Email"/> — the address the A08 gate prefills
    /// and must-match-pins) when the token is a live <see cref
    /// "EmailInvitationStatus.Pending"/> row, else <b><c>null</c></b> (the
    /// caller renders the closed shape). A blank token is refused (the
    /// <see cref="ValidateAsync"/> guard). No audit row is written (the M35·5
    /// "the read is not an access decision" floor — the access decision is
    /// A08's sign-up gate's job).
    /// </para>
    /// </summary>
    /// <param name="presentedToken">The raw token as presented by the viewer
    /// (the A08 <c>?invite=</c> query param / hidden form field) — hashed
    /// here, never compared raw.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The matching live <see cref="Invitation"/> row, or
    /// <c>null</c> (absent / accepted / revoked / past-expiry / blank — the
    /// caller renders the closed shape).</returns>
    Task<Invitation?> ValidatePresentedAsync(
        string presentedToken, CancellationToken ct = default);

    /// <summary>
    /// Accepts a <b>presented raw token</b> for the sign-up write path (ADR
    /// 0304 D3, IMPROVE-UX A08) — the A08 <c>?invite=</c> POST's write seam.
    /// <para>
    /// **Trap A (hash in one place):** the Web host cannot hash the presented
    /// token — <see cref="IdentityService.Sha256Hex"/> is <c>internal</c> to
    /// the Core assembly (no <c>InternalsVisibleTo</c> to the Web host). This
    /// overload hashes the raw token with that one existing hasher and
    /// delegates to the hash-based <see cref="AcceptAsync"/>, so the digest
    /// lives in exactly one place (this assembly).
    /// </para>
    /// <para>
    /// **Contract:** identical to <see cref="AcceptAsync"/> (the token is
    /// verified, the signing-up email must match
    /// <see cref="Invitation.Email"/>, the
    /// <see cref="IIdentityService.RegisterAsync"/> lane runs, the row is
    /// marked <see cref="EmailInvitationStatus.Accepted"/>, one
    /// <c>invitation.accept</c> audit row is written, the
    /// <see cref="ThinPrincipal"/> is returned). A blank token is refused
    /// (the <see cref="ValidatePresentedAsync"/> guard).
    /// </para>
    /// </summary>
    /// <param name="presentedToken">The raw token as presented by the viewer
    /// (the A08 <c>?invite=</c> hidden form field) — hashed here, never
    /// compared raw.</param>
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
    Task<ThinPrincipal> AcceptPresentedAsync(
        string presentedToken, string email, string displayName, string password,
        CancellationToken ct = default);
}
