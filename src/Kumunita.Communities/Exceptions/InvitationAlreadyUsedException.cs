namespace Kumunita.Communities.Exceptions;

public class InvitationAlreadyUsedException(string token)
    : Exception($"Invitation '{token}' has already been used.");