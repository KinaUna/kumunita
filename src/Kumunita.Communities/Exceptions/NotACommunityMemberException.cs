namespace Kumunita.Communities.Exceptions;

public class NotACommunityMemberException(string slug)
    : Exception($"User is not a member of community '{slug}'.");