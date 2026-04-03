namespace Kumunita.Identity.Contracts.Queries;

public record PlatformUserRow(
    Guid Id,
    string Email,
    bool IsSuspended,
    bool MustChangePassword,
    IList<string> Roles,
    DateTimeOffset CreatedAt);
