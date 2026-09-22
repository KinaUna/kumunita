namespace Kumunita.Web.Models;

/// <summary>
/// The <c>/admin</c> overview dashboard (ADR 0062). Counts + the
/// verify-queue shortcut. The per-section details (accounts, communities,
/// platform pages) live on their own pages; this is the "where am I"
/// landing surface.
/// </summary>
public sealed class AdminDashboardViewModel
{
    public int AccountsCount { get; init; }
    public int UnverifiedCount { get; init; }
    public int BlockedCount { get; init; }
    public int CommunitiesCount { get; init; }
    public int DisabledCommunityCount { get; init; }

    /// <summary>
    /// The unverified accounts, for the verify-queue shortcut. Rendered
    /// only when <see cref="UnverifiedCount"/>&nbsp;&gt;&nbsp;0.
    /// </summary>
    public IReadOnlyList<UnverifiedAccountRow> Unverified { get; init; } = [];

    public sealed class UnverifiedAccountRow
    {
        public string SubjectId { get; init; } = string.Empty;
        public string? Email { get; init; }
        public string? DisplayName { get; init; }
    }
}
