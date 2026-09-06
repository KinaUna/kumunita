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

    /// <summary>
    /// The full community list (enabled + disabled) rendered in the
    /// <c>/admin</c> "Communities" section. A distinct shape from
    /// <see cref="ComponentOption"/> because the admin needs to see the
    /// disabled rows (so they can re-enable) and the full editable fields
    /// (description, sort order, enabled flag). <see cref="Icon"/> is a
    /// reserved slot for a future picker UI — the Core API already carries
    /// it, so a later form can surface it without a schema change.
    /// </summary>
    public sealed class CommunityRow
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public int SortOrder { get; init; }
        public bool Enabled { get; init; } = true;
        public bool ModeratorAccess { get; init; }
    }

    public IReadOnlyList<CommunityRow> Communities { get; init; } = [];
    public int DisabledCommunityCount => Communities.Count(c => !c.Enabled);
}
