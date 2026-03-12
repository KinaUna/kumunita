namespace Kumunita.Communities.Contracts.Commands;

/// <summary>
/// Called when an invited user clicks the invitation link.
/// Token identifies the invitation.
/// </summary>
public record AcceptInvitationCommand(string Token);