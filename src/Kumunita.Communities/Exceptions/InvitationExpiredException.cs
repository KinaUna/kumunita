namespace Kumunita.Communities.Exceptions;

public class InvitationExpiredException(string token)
    : Exception($"Invitation '{token}' has expired.");