namespace Kumunita.Web.Models;

/// <summary>
/// The <c>/admin/platform</c> page (ADR 0062). The platform links
/// (languages, timezone, date format, sign-up, audit, break-glass) and
/// the platform pages table (the five shipped surfaces with preview /
/// edit links). Reuses the <see cref="AdminIndexViewModel.PlatformPageRow"/>
/// shape (the shared row type the <c>/admin</c> dashboard used to render).
/// </summary>
public sealed class AdminPlatformViewModel
{
    public IReadOnlyList<AdminIndexViewModel.PlatformPageRow> PlatformPages { get; init; } = [];

    /// <summary>
    /// ADR 0138 — whether the <b>sample-data</b> surface (<c>/admin/sample</c>,
    /// the change-password lock toggle) is reachable on this instance. Set by
    /// <c>AdminController.Platform</c> from
    /// <see cref="Kumunita.Core.Identity.IIdentityService.IsSampleDataEnabledAsync"/>
    /// (the <c>SampleData__Enabled</c> flag); the view renders the
    /// <c>/admin/sample</c> link only when this is <c>true</c>. A real
    /// deployment never carries the flag, so the link is absent there — the
    /// surface is unreachable by construction (the ADR 0056 shape). Defaults to
    /// <c>false</c> (hidden) so a missing read floors to "not a demo instance."
    /// </summary>
    public bool ShowSampleData { get; init; }
}
