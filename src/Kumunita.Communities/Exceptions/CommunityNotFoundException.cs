namespace Kumunita.Communities.Exceptions;

public class CommunityNotFoundException(string slug)
    : Exception($"Community '{slug}' was not found.");