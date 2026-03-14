using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Communities;

namespace Kumunita.Communities.Contracts.Queries;

/// <summary>
/// Lightweight community entry used in the community switcher / user community list.
/// Includes the authenticated user's role in that community.
/// </summary>
public record UserCommunityResult(
    CommunityId Id,
    string Slug,
    string Name,            // resolved for the user's preferred language
    string? City,
    string? Country,
    CommunityRole Role);
