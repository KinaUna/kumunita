using Kumunita.Core.Identity;

namespace Kumunita.Web.Models;

public sealed class AdminIndexViewModel
{
    public sealed class AccountRow
    {
        public string SubjectId { get; init; } = string.Empty;
        public string? Email { get; init; }
        public string? DisplayName { get; init; }
        public bool Verified { get; init; }
        public IReadOnlyList<string> Roles { get; init; } = [];
        public IReadOnlyList<string> ComponentIds { get; init; } = [];
    }

    public IReadOnlyList<AccountRow> Accounts { get; init; } = [];
    public int UnverifiedCount => Accounts.Count(a => !a.Verified);

    public sealed class ComponentOption
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public bool ModeratorAccess { get; init; }
    }

    public IReadOnlyList<ComponentOption> Components { get; init; } = [];
}
