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

    // ── IMPROVE-UX A15 — the per-resident storage quota override card ────────
    // The admin half of the storage loop (A14's "request more space" nudge
    // points here): the effective per-user quota for this resident (override
    // ?? global, the C-UP·5 sentinel resolved), their current usage, and a
    // set/clear control. The effective quota + the override are the two values
    // the view renders; the form is a blank-clears / 0-unlimited / MiB-cap
    // input (the AdminStorageController.Save value semantics).

    /// <summary>A15 — the resident's current per-user-quota override in bytes,
    /// or <c>null</c> when no override is set (the <c>override ?? global</c>
    /// fallback signal — distinct from <c>0</c>, the per-resident
    /// unlimited). Drives the view's "no override (uses the community
    /// default)" note and the form's pre-fill.</summary>
    public long? QuotaOverrideBytes { get; init; }

    /// <summary>A15 — the resident's **effective** per-user quota in bytes
    /// (override ?? global). <c>0</c> = unlimited for this resident (the
    /// C-UP·5 sentinel preserved per-resident).</summary>
    public long EffectiveQuotaBytes { get; init; }

    /// <summary>A15 — true when the resident's effective per-user quota is
    /// <c>0</c> (unlimited). Drives the "Unlimited" label in the card.</summary>
    public bool EffectiveQuotaUnlimited { get; init; }

    /// <summary>A15 — the resident's current used bytes (the C-SM·7
    /// <c>Σ SizeBytes WHERE CreatedById == subject</c> read).</summary>
    public long UsedBytes { get; init; }

    /// <summary>A15 — the community-wide per-user quota in bytes (the global
    /// the override falls back to; <c>0</c> = unlimited). Shown so the admin
    /// sees what "clear the override" means for this resident.</summary>
    public long GlobalQuotaBytes { get; init; }

    /// <summary>A15 — the override pre-filled as a MiB string for the form
    /// input (blank when no override is set — clearing returns the resident to
    /// the community default; <c>"0"</c> when the override is the per-resident
    /// unlimited sentinel). Pure display; the controller parses on save.</summary>
    public string QuotaOverrideInput { get; init; } = string.Empty;

    /// <summary>A15 — human-readable (IEC units) of <see cref="UsedBytes"/>.</summary>
    public string UsedHuman { get; init; } = string.Empty;

    /// <summary>A15 — human-readable of the effective quota — "Unlimited" when
    /// <see cref="EffectiveQuotaUnlimited"/>.</summary>
    public string EffectiveQuotaHuman { get; init; } = string.Empty;

    /// <summary>A15 — human-readable of the community-wide quota — "Unlimited"
    /// when it is <c>0</c>.</summary>
    public string GlobalQuotaHuman { get; init; } = string.Empty;
}
