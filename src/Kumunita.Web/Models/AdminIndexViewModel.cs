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
        public bool Blocked { get; init; }
        public IReadOnlyList<string> Roles { get; init; } = [];
        /// <summary>The component *scopes* this account governs (moderator
        /// standing — ADR 0003). Distinct from <see cref="CommunityIds"/>
        /// (the communities this account may **post to**): a moderator can
        /// govern a community they cannot post to, and a member can post to a
        /// community they do not moderate; the two overlap by coincidence,
        /// not by definition.</summary>
        public IReadOnlyList<string> ComponentIds { get; init; } = [];
        /// <summary>The communities this account may **post to** (the new
        /// <c>ComponentMembership</c> posting right, distinct from the
        /// moderator scope above; <see cref="Kumunita.Core.Identity
        /// .Roles.GlobalAdmin"/> bypass this gate at post-time so an admin's
        /// own rows here are not strictly meaningful, but the UI renders
        /// them for consistency).</summary>
        public IReadOnlyList<string> CommunityIds { get; init; } = [];
    }

    public IReadOnlyList<AccountRow> Accounts { get; init; } = [];
    public int UnverifiedCount => Accounts.Count(a => !a.Verified);
    public int BlockedCount => Accounts.Count(a => a.Blocked);

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
        /// <summary>ADR 0012 — the community is mandatory: every verified
        /// resident is an implicit member (nobody may be removed or leave it;
        /// toggled on the community's manage page or the moderator lane, read
        /// only here).</summary>
        public bool Mandatory { get; init; }
    }

    public IReadOnlyList<CommunityRow> Communities { get; init; } = [];
    public int DisabledCommunityCount => Communities.Count(c => !c.Enabled);
}
