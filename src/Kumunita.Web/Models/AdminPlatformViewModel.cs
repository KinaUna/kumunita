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
    /// ADR 0161 — the per-<see cref="PlatformPage"/> translation data for the
    /// platform pages that resolved to a <see cref="Kumunita.Core.Pages.Page"/>
    /// doc (the four <c>Page</c>-backed surfaces; the <c>about</c> view is
    /// preview-only, so it has none). One row per such page, in the same
    /// footer order as <see cref="PlatformPages"/>. The view renders a
    /// translations ⋮ dropdown per row (add / edit / remove — the
    /// posts/announcements pattern) + the shared
    /// <c>Views/Shared/_PageTranslationModals</c> partial (id-prefixed by
    /// <see cref="PlatformPage.Slug"/> so multiple rows on one document never
    /// collide). Standing is the GlobalAdmin gate on <c>/admin</c> (the
    /// controller's <c>[Authorize(Roles = GlobalAdmin)]</c>); the
    /// <c>PageController</c> write lanes re-check standing server-side (C3).
    /// </summary>
    public IReadOnlyList<PlatformPage> PlatformPageTranslations { get; init; } = [];

    /// <summary>
    /// ADR 0161 — one platform page's translation surface: its id, its
    /// authored-in <see cref="Title"/> (pre-filled into the "add a
    /// translation" modal's title field), its stored
    /// <see cref="Kumunita.Core.Pages.PageTranslation"/> rows (one "edit" modal
    /// each), the candidate <see cref="LanguageOption"/> list (enabled catalog
    /// languages with no stored row and not the authored-in language — one
    /// "add" modal each), and the <see cref="LangNames"/> code → native-name
    /// map (the modal-title label). <see cref="Slug"/> is the id-prefix for
    /// the shared partial's element ids (e.g. <c>terms-trans-add-de-…</c>).
    /// </summary>
    public sealed class PlatformPage
    {
        public string Slug { get; init; } = string.Empty;
        public string PageId { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public IReadOnlyList<Kumunita.Core.Pages.PageTranslation> Translations { get; init; } = [];
        public IReadOnlyList<LanguageOption> MissingLanguages { get; init; } = [];
        public IReadOnlyDictionary<string, string> LangNames { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

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
