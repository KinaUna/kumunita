using Kumunita.Shared.Kernel;

namespace Kumunita.Communities.Contracts.Queries;

public record CommunityResult(
    CommunityId Id,
    string Slug,
    string Name,            // resolved for user's preferred language
    string Description,
    string? City,
    string? Country,
    bool IsActive,
    int MemberCount);