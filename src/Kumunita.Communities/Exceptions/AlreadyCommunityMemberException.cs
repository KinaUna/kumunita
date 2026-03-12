namespace Kumunita.Communities.Exceptions;

public class AlreadyCommunityMemberException(string slug)
    : Exception($"User is already a member of community '{slug}'.");