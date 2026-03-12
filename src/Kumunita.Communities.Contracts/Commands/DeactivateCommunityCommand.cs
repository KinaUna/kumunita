namespace Kumunita.Communities.Contracts.Commands;

/// <summary>Only platform admins may call this endpoint.</summary>
public record DeactivateCommunityCommand(string Slug);