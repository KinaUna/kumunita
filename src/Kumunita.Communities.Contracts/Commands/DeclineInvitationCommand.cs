namespace Kumunita.Communities.Contracts.Commands;

/// <summary>
/// Called when an invited user clicks the decline invitation link.
/// Token identifies the invitation.
public record DeclineInvitationCommand(string Token);