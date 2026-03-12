namespace Kumunita.Communities.Exceptions;

public class CommunityInvitationNotFoundException(string token)
    : Exception($"Invitation with token '{token}' was not found.");