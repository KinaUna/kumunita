namespace Kumunita.Communities.Contracts.Commands;

/// <summary>Only platform admins may call this endpoint.</summary>
public record CreateCommunityCommand(
    string Slug,
    Dictionary<string, string> Name,         // LanguageCode → display name
    Dictionary<string, string> Description,
    string? Street,
    string? City,
    string? PostalCode,
    string? Country);