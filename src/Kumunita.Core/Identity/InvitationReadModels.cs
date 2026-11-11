namespace Kumunita.Core.Identity;

/// <summary>
/// The read-model + result shapes for the <see cref="IInvitationService"/>
/// read + delivery seams (ADR 0304, IMPROVE-UX A09) — the
/// <c>/admin/invitations</c> surface's Core models.
/// </summary>

/// <summary>
/// One invitation row as surfaced by
/// <see cref="IInvitationService.ListAsync"/> (the ADR 0304 D1/D6 read
/// shape — the <see cref="Invitation"/> row's members the
/// <c>/admin/invitations</c> list needs, plus the **virtual-Expired**
/// state computed from <see cref="Invitation.ExpiresAt"/> — a
/// <see cref="EmailInvitationStatus.Pending"/> row past its expiry
/// surface's <see cref="EmailInvitationStatus.Expired"/> in
/// <see cref="DisplayStatus"/> without the enum ever being written, the
/// <see cref="IInvitationService.ValidateAsync"/> post-load comparison
/// precedent). A pure read projection: the token hash is NOT carried
/// (the list never needs the credential — the hash-at-rest floor, ADR
/// 0304 D2).
/// </summary>
/// <param name="InvitationId">The <see cref="Invitation"/> row's id
/// (the revoke / resend action target).</param>
/// <param name="Email">The invited address (lowercased, trimmed — ADR
/// 0304 D1).</param>
/// <param name="InviterSubjectId">The resident or admin who created the
/// invitation (the audit principal).</param>
/// <param name="CreatedAt">When the invitation was created (UTC).</param>
/// <param name="ExpiresAt">The expiry instant (UTC) — the
/// virtual-Expired check.</param>
/// <param name="Status">The stored state (<see cref
/// "EmailInvitationStatus.Pending"/> / <see cref
/// "EmailInvitationStatus.Accepted"/> / <see cref
/// "EmailInvitationStatus.Revoked"/>) — never explicitly
/// <see cref="EmailInvitationStatus.Expired"/> (the virtual state,
/// ADR 0304 D1/D6).</param>
/// <param name="AcceptedAt">Set when the row was accepted (null while
/// Pending / Revoked).</param>
/// <param name="AcceptedSubjectId">The subject id of the account created
/// by the accept lane (null while Pending / Revoked).</param>
/// <param name="DisplayStatus">The list's surface state: the stored
/// <see cref="Status"/> except that a <see cref
/// "EmailInvitationStatus.Pending"/> row past <see cref
/// "Invitation.ExpiresAt"/> reads <see cref
/// "EmailInvitationStatus.Expired"/> (the virtual-Expired pin — the
/// list shows the state the validate/accept seams would refuse, without
/// a write).</param>
public sealed record InvitationListItem(
    string InvitationId,
    string Email,
    string InviterSubjectId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    EmailInvitationStatus Status,
    DateTimeOffset? AcceptedAt,
    string? AcceptedSubjectId,
    EmailInvitationStatus DisplayStatus);

/// <summary>
/// The <see cref="IInvitationService.InviteAsync"/> result (IMPROVE-UX
/// A09) — the invitation's delivery seam: create + stage the invitation
/// email in **one** transaction (the <see cref="IMailerStage"/> C3
/// guarantee — the outbox row commits with the domain write; a failed
/// commit leaves no queued email behind). The raw token is carried in
/// the <see cref="Invitation"/> (returned once for the caller to show —
/// the ADR 0304 D2 shown-once rule); the email itself carries the
/// <c>?invite=</c> link, so an admin surface typically does not show the
/// raw token at all.
/// </summary>
/// <param name="Token">The raw token (shown once, then unrecoverable —
/// the store holds only the SHA-256 hex digest).</param>
/// <param name="Invitation">The stored <see cref="Invitation"/> row
/// (hash only, never the raw token).</param>
public sealed record InvitationInviteResult(string Token, Invitation Invitation);
