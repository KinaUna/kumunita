namespace Kumunita.Communities.Exceptions;

public class CommunitySlugAlreadyExistsException(string slug)
    : Exception($"A community with slug '{slug}' already exists.");