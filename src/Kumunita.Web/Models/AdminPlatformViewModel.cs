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
}
