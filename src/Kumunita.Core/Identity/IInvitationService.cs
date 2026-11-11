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

    /// <summary>
    /// Lists the invitations created by <paramref name="inviterSubjectId"/>
    /// (IMPROVE-UX A09 — the <c>/admin/invitations</c> list seam; ADR 0304
    /// D3's list-read the A07/A08 seam shipped without): the inviter's rows,
    /// newest-first, each an <see cref="InvitationListItem"/> carrying
    /// <see cref="InvitationListItem.DisplayStatus"/> — the stored
    /// <see cref="Invitation.Status"/> except that a
    /// <see cref="EmailInvitationStatus.Pending"/> row past
    /// <see cref="Invitation.ExpiresAt"/> reads <see cref
    /// "EmailInvitationStatus.Expired"/> (the virtual-Expired state, ADR
    /// 0304 D1/D6 — computed in the read, the enum is never written, the
    /// <see cref="ValidateAsync"/> post-load comparison precedent).
    /// <para>
    /// **A read lane — zero audit rows** (the M35·5 / 0159 "the read is a
    /// config load, not an access decision" floor — the access decision is
    /// the Web controller's <c>[Authorize(Roles = Roles.GlobalAdmin)]</c>
    /// boundary's job, the ADR 0050 split; the service does not re-check
    /// <c>User</c>). The token hash is **not** carried (the list never needs
    /// the credential — the hash-at-rest floor, ADR 0304 D2).
    /// </para>
    /// </summary>
    /// <param name="inviterSubjectId">The inviter whose rows are listed
    /// (owner-scoped — another inviter's rows are not returned).</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The inviter's invitations, newest-first (empty when the
    /// inviter has none — the C-M19·4 empty floor, not an error).</returns>
    Task<IReadOnlyList<InvitationListItem>> ListAsync(
        string inviterSubjectId, CancellationToken ct = default);

    /// <summary>
    /// Invites <paramref name="email"/> (lowercased, trimmed) issued by
    /// <paramref name="inviterSubjectId"/> **and stages the invitation
    /// email** in the same transaction (IMPROVE-UX A09 — the delivery seam):
    /// creates the <see cref="Invitation"/> row (the
    /// <see cref="CreateAsync"/> contract — exactly one
    /// <c>invitation.create</c> <c>AccessAudit</c> row,
    /// <c>Via: Owner</c>, <c>TargetKind = "invitation"</c>), builds the
    /// invitation email (<see cref="Email.Invitation"/> keys — the
    /// <c>email.invitation_subject</c> / <c>email.invitation_body</c> pair,
    /// the <see cref="IdentityService.BuildVerificationEmailAsync"/>
    /// template shape), and stages it via <see cref="IMailerStage
    /// .StageAsync"/> with the idempotency key <c>invite:{invitationId}</c>
    /// (the §6.2 per-email key scheme). One <c>SaveChangesAsync</c> = the
    /// domain write + the audit row + the outbox row + the durable envelope
    /// commit atomically (the C3 guarantee — a caller whose commit never
    /// lands leaves no queued email behind).
    /// <para>
    /// The email's <c>?invite=</c> link is
    /// <c>{BaseUrl}/account/signup?invite={token}</c> — <see cref
    /// "IInstanceBaseUrlReader.BaseUrl"/> (the host-bound
    /// <c>VerificationOptions.BaseUrl</c>), with the same-origin relative
    /// path fallback when unset (the dev-only shape — a human can still copy
    /// it into the browser bar, the <c>VerificationLink</c> precedent).
    /// <paramref name="inviteLinkOverride"/> (nullable) replaces the
    /// assembled link (a test shape: an absolute link without a host-bound
    /// base URL). The raw token is returned **exactly once** (the ADR 0304
    /// D2 shown-once rule); the store holds only the SHA-256 hex digest.
    /// </para>
    /// </summary>
    /// <param name="email">The invited address (lowercased + trimmed).</param>
    /// <param name="inviterSubjectId">The resident or admin issuing the
    /// invitation (the audit principal + the email's name).</param>
    /// <param name="expiresAt">The expiry instant (UTC) — ADR 0304 D6(a)
    /// proposed default 7 days, settled by the A09 call site (the
    /// <c>AdminInvitationsController</c> passes
    /// <c>DateTimeOffset.UtcNow.AddDays(7)</c>).</param>
    /// <param name="inviterDisplayName">The inviter's display name
    /// (the email's <c>{0}</c> — names the inviter + their community for
    /// the invitee; null/blank floors to "there").</param>
    /// <param name="inviteLinkOverride">Optional absolute invite link
    /// override (a test shape); null assembles
    /// <c>{BaseUrl}/account/signup?invite={token}</c>.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The raw token (shown once) + the stored row (hash only) —
    /// <see cref="InvitationInviteResult"/>.</returns>
    Task<InvitationInviteResult> InviteAsync(
        string email, string inviterSubjectId, string inviterDisplayName,
        DateTimeOffset expiresAt, string? inviteLinkOverride = null,
        CancellationToken ct = default);
}
