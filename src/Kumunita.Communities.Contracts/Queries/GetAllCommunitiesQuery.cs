namespace Kumunita.Communities.Contracts.Queries;

/// <summary>Platform admins only — returns all communities.</summary>
public record GetAllCommunitiesQuery(bool IncludeInactive = false);