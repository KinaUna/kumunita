using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Communities;

namespace Kumunita.Communities.Contracts.Queries;

public record CommunityMemberResult(
    UserId UserId,
    string DisplayName,
    CommunityRole Role,
    MembershipStatus Status,
    DateTimeOffset JoinedAt);