namespace Kumunita.Web.Models;

/// <summary>
/// The <c>/admin/accounts/{subjectId}</c> detail page — the account's
/// standing management surface (roles, moderator scope, posting
/// membership, block/unblock). Extracted out of the <c>/admin</c> shell's
/// accounts table (which previously inlined three checkbox forms per row):
/// the shell keeps the read-only overview + a <c>Manage</c> link, this page
/// owns the writes. The row data is the same
/// <see cref="AdminIndexViewModel.AccountRow"/> shape the shell uses (no
/// duplication — the detail action composes it from the same
/// <see cref="AdminIndexViewModel"/> the shell builds, resolved by
/// SubjectId).
/// </summary>
public sealed class AdminAccountManageViewModel
{
    public AdminIndexViewModel.AccountRow Account { get; init; } = null!;

    /// <summary>The enabled component set for the role-assignment "scope"
    /// checkbox list (a disabled component is not a valid moderator scope —
    /// same rule as the shell's inline form).</summary>
    public IReadOnlyList<AdminIndexViewModel.ComponentOption> Components { get; init; } = [];

    /// <summary>All components (enabled + disabled) for the posting-membership
    /// list: an account may still carry a membership row on a disabled
    /// community (the feed 404s, the row stays), and the admin needs to see
    /// it to remove it. Mandatory components render locked (ADR 0012 —
    /// everyone is a member; unchecking is a server-side no-op).</summary>
    public IReadOnlyList<AdminIndexViewModel.CommunityRow> Communities { get; init; } = [];

    /// <summary>True when the signed-in admin is managing their own account —
    /// the self-block guard then hides the Block button (Unblock still shows,
    /// so an admin blocked by another admin can self-restore). The controller
    /// enforces this too (defense in depth); the view just doesn't offer the
    /// action it would refuse.</summary>
    public bool IsSelf { get; init; }

    /// <summary>The disabled-communities-only view of <see cref="Communities"/>
    /// (membership rows that currently 404 on the feed). A convenience for the
    /// view's "disabled communities this account still posts to" note.</summary>
    public IReadOnlyList<string> DisabledCommunityIds { get; init; } = [];
}
