using Kumunita.Shared.Kernel.Communities;
using Kumunita.Shared.Kernel.ValueObjects;
using System.Security.Claims;
using System.Text.Json;

namespace Kumunita.Shared.Kernel.Auth;

/// <summary>
/// Extension methods on ClaimsPrincipal for community-aware authorization checks.
/// Usable from both backend handlers and Blazor WASM components.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    private const string CommunitiesClaim = "communities";
    private const string PlatformAdminClaim = "platform_admin";
    private const string PreferredLanguageClaim = "preferred_language";

    private static string? GetClaimValue(this ClaimsPrincipal user, string claimType) =>
        user.FindFirst(claimType)?.Value;

    // ── Identity ──────────────────────────────────────────────────────────────

    public static UserId GetUserId(this ClaimsPrincipal user)
    {
        string sub = user.GetClaimValue(ClaimTypes.NameIdentifier)
                     ?? user.GetClaimValue("sub")
                     ?? throw new InvalidOperationException("User has no subject claim.");

        return new UserId(Guid.Parse(sub));
    }

    public static bool IsPlatformAdmin(this ClaimsPrincipal user) =>
        user.GetClaimValue(PlatformAdminClaim) == "true";

    public static string? GetEmail(this ClaimsPrincipal user) =>
        user.GetClaimValue(ClaimTypes.Email)
        ?? user.GetClaimValue("email");

    // ── Language ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the user's preferred language codes in fallback order.
    /// Falls back to English if no preference is set.
    /// </summary>
    public static IReadOnlyList<string> GetPreferredLanguage(this ClaimsPrincipal user)
    {
        string? pref = user.GetClaimValue(PreferredLanguageClaim);
        return pref is not null
            ? [pref, LanguageCode.English.Value]
            : [LanguageCode.English.Value];
    }

    // ── Community membership ──────────────────────────────────────────────────

    /// <summary>
    /// Returns all community slugs the user is an active member of.
    /// </summary>
    public static IReadOnlyList<string> GetCommunitySlugs(this ClaimsPrincipal user) =>
        GetCommunityEntries(user).Select(c => c.Slug).ToList();

    public static bool IsMemberOf(this ClaimsPrincipal user, string slug) =>
        GetCommunitySlugs(user).Contains(slug, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the user's role in the given community, or null if not a member.
    /// </summary>
    public static CommunityRole? GetCommunityRole(this ClaimsPrincipal user, string slug)
    {
        CommunityClaim? entry = GetCommunityEntries(user)
            .FirstOrDefault(c => c.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

        return entry is null ? null : Enum.Parse<CommunityRole>(entry.Role, ignoreCase: true);
    }

    public static bool IsManagerOf(this ClaimsPrincipal user, string slug) =>
        user.GetCommunityRole(slug) == CommunityRole.Manager;

    public static bool IsModeratorOrAbove(this ClaimsPrincipal user, string slug) =>
        user.GetCommunityRole(slug) >= CommunityRole.Moderator;

    // ── Internal helpers ──────────────────────────────────────────────────────

    private static IReadOnlyList<CommunityClaim> GetCommunityEntries(ClaimsPrincipal user)
    {
        string? raw = user.GetClaimValue(CommunitiesClaim);

        if (string.IsNullOrWhiteSpace(raw))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<CommunityClaim>>(raw,
                JsonSerializerOptions.Web) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private record CommunityClaim(string Slug, string Role);
}