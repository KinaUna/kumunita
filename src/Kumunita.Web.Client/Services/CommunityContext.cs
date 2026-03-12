using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace Kumunita.Web.Client.Services;

/// <summary>
/// Tracks which community the user is currently focused on.
/// This is a UI-layer concept — it does NOT affect backend tenancy
/// (that is always determined by the {slug} in the URL).
///
/// The active community drives the community switcher highlight and
/// the default navigation targets in NavMenu.
/// </summary>
public class CommunityContext
{
    private readonly NavigationManager _nav;
    private readonly AuthenticationStateProvider _auth;

    public CommunityContext(NavigationManager nav, AuthenticationStateProvider auth)
    {
        _nav = nav;
        _auth = auth;
    }

    /// <summary>
    /// The currently focused community slug, or null for the combined view.
    /// </summary>
    public string? ActiveSlug { get; private set; }

    public bool IsInCombinedView => ActiveSlug is null;

    public event Action? OnChange;

    /// <summary>
    /// Sets the active community and navigates to the community landing page.
    /// </summary>
    public void SetActive(string slug)
    {
        ActiveSlug = slug.ToLowerInvariant();
        OnChange?.Invoke();
        _nav.NavigateTo($"/communities/{ActiveSlug}");
    }

    /// <summary>
    /// Clears the active community and returns to the combined feed.
    /// </summary>
    public void ClearActive()
    {
        ActiveSlug = null;
        OnChange?.Invoke();
        _nav.NavigateTo("/announcements");
    }

    /// <summary>
    /// Sets the active community without navigating (e.g. when the user
    /// navigates directly to a community URL via the address bar).
    /// Called by community-scoped pages in OnInitialized.
    /// </summary>
    public void SetActiveWithoutNavigate(string slug)
    {
        string normalized = slug.ToLowerInvariant();

        if (ActiveSlug == normalized) return;

        ActiveSlug = normalized;
        OnChange?.Invoke();
    }
}