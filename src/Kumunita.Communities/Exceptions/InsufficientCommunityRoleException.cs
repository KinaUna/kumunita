namespace Kumunita.Communities.Exceptions;

public class InsufficientCommunityRoleException(string slug, string requiredRole)
    : Exception($"This action requires the '{requiredRole}' role in community '{slug}'.");