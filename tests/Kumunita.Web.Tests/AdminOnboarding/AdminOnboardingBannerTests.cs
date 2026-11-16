using System.Text.RegularExpressions;
using Kumunita.Core.AdminOnboarding;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// M30 U06 (ADR 0153) — the admin onboarding banner pins (M30·5): the
/// banner renders <b>iff</b> the signed-in actor is a <c>GlobalAdmin</c> AND
/// <c>CompletedAt</c> is <c>null</c> (the M22 <c>bannerEligible</c> read +
/// the M29 <c>GlobalAdmin</c>-gated scope), is <b>non-blocking</b> (sign-in
/// never gated, always dismissible via a <c>sessionStorage</c> flag — never
/// a write to <c>CompletedAt</c>, the route always reachable), and its CTA
/// link points to <c>/admin/onboarding</c> (the M22
/// <c>onboarding.banner.action</c> idiom, admin-scope).
/// </summary>
/// <para>
/// <b>Test model (the house idiom):</b> the M22 / M30 banner is a Razor
/// partial (<see cref="_AdminOnboardingBanner.cshtml"/>) that gates itself
/// on two reads — <c>KumunitaPrincipal.IsGlobalAdmin(User)</c> (the
/// <c>Kumunita.Role</c> claim read, the thin-token rule ADR 0001-B) and the
/// <c>IAdminOnboardingService.GetAsync()</c> best-effort completion read
/// (the SAME read-seam the
/// <see cref="AdminOnboardingController.Index"/> action calls, M30·5 — the
/// banner + the view resolve to the same value). The banner's whole contract
/// is "the eligibility reads exist in the markup, the gate is the two
/// conditions ANDed, the CTA link points at the walk-through route, and the
/// dismissal is a client-side flag" — none of which need a running server.
/// So these are <b>structural string pins</b> on the partial source (the
/// <see cref="NavMoreFoldTests"/> /
/// <see cref="BookmarkButtonTests.BmButton_Partial_Renders_Correct_Lane_For_State"/>
/// "string pin, no TestServer" house idiom — the same shape the M22
/// <see cref="OnboardingControllerTests"/> / M22
/// <see cref="Onboarding_KwL_Set_Is_Parity_Pinned_In_Four_Languages"/> used
/// to pin the resident banner's eligibility + closed-<c>kw-l</c>-set shape):
/// read the partial file (located by walking up to the repo root), strip the
/// Razor comment block (author documentation, not rendered markup), and
/// verify the two eligibility reads + the <c>@if (bannerEligible)</c> gate +
/// the <c>/admin/onboarding</c> CTA link + the non-blocking dismissal flag.
/// </para>
/// <para>
/// <b>Why not a TestServer render:</b> the M22 / M30 banner has no
/// model (it <c>@inject</c>s the seams itself), and the house Web.Tests
/// suite has no <c>RazorPage</c> / <c>RenderViewToString</c> harness — the
/// established house idiom for a model-less, <c>@inject</c>-driven partial
/// is the structural pin (the <c>_BookmarkButton</c> precedent, the
/// <c>_OnboardingBanner</c> precedent). The behavioural guarantee (the
/// banner actually rendering at runtime) is the Playwright e2e surface, not
/// xUnit — the ADR 0107/0111 e2e precedent.
/// </para>
public sealed class AdminOnboardingBannerTests
{
    // ── 1 — renders iff GlobalAdmin AND CompletedAt is null (M30·5) ───────
    // The partial's eligibility reads: isGlobalAdmin (the GlobalAdmin role
    // read, the thin-token rule ADR 0001-B) + bannerEligible (the
    // GetAsync read's null check, the SAME seam the controller calls —
    // M30·5). The two are ANDed (the GlobalAdmin gate + the null
    // completion read), and the @if (bannerEligible) gate is the single
    // render branch.

    [Fact(DisplayName = "U06 admin onboarding banner renders iff GlobalAdmin AND CompletedAt is null (M30·5)")]
    public void BannerRenders_ForGlobalAdmin_WhenNotCompleted()
    {
        var src = LoadBannerSource();

        // (a) The GlobalAdmin eligibility read (the thin-token rule, ADR
        // 0001-B — the Kumunita.Role claim, never a claim re-derived here):
        // the standard role read (KumunitaPrincipal.IsGlobalAdmin) is the
        // gate.
        Assert.Contains("KumunitaPrincipal.IsGlobalAdmin", src);
        Assert.Contains("User.Identity?.IsAuthenticated == true", src);

        // (b) The completion eligibility read (M30·5 — the SAME GetAsync
        // seam the AdminOnboardingController.Index action calls, so the
        // banner + the view resolve to the same value): the best-effort
        // read, a missing store / row / read failure degrades to null =
        // not-yet-guided, the floor.
        Assert.Contains("AdminOnboarding.GetAsync", src);
        Assert.Contains("completedAt is null", src);

        // (c) The two are ANDed (isGlobalAdmin gates the completion read —
        // a non-GlobalAdmin never even resolves the completion read) and the
        // @if (bannerEligible) is the single render branch (the M22
        // bannerEligible idiom, admin-scope).
        Assert.Contains("bool isGlobalAdmin", src);
        Assert.Contains("bool bannerEligible", src);
        Assert.Contains("@if (bannerEligible)", src);
    }

    // ── 2 — does NOT render for a non-GlobalAdmin (M30·3 / M30·5) ─────────
    // Even if CompletedAt is null, a non-GlobalAdmin never sees the banner:
    // the isGlobalAdmin read is the gate, and it is ANDed with the
    // completion read (the M29 GlobalAdmin-gated scope — the thin-token
    // rule, ADR 0001-B; the banner is admin-scope, not resident-scope).

    [Fact(DisplayName = "U06 admin onboarding banner does NOT render for a non-GlobalAdmin (even if CompletedAt is null)")]
    public void BannerDoesNotRender_ForNonGlobalAdmin()
    {
        var src = LoadBannerSource();

        // The GlobalAdmin read is present (the gate) — a non-GlobalAdmin
        // (no Kumunita.Role claim of GlobalAdmin, or unauthenticated) fails
        // it, and isGlobalAdmin is ANDed with the completion read before
        // bannerEligible is computed.
        Assert.Contains("KumunitaPrincipal.IsGlobalAdmin", src);
        Assert.Contains("User.Identity?.IsAuthenticated == true", src);

        // The banner is admin-scope (GlobalAdmin-gated) — it does NOT fall
        // back to the resident-scope onboarding read (the M22 / M30
        // distinction — M22 is resident-scope, M30 is admin-scope; the two
        // completion flags are independent, the M22
        // Profile.OnboardingCompletedAt field is untouched).
        Assert.DoesNotContain("OnboardingCompletedAt", src);
        Assert.DoesNotContain("IUserInfoService", src);

        // The isGlobalAdmin gate is ANDed with the completion read (a
        // non-GlobalAdmin never sees the banner, even if CompletedAt is
        // null).
        Assert.Contains("if (isGlobalAdmin)", src);
    }

    // ── 3 — does NOT render when CompletedAt is non-null (M30·5) ──────────
    // Even if the actor is a GlobalAdmin, a completed admin never sees the
    // banner: the bannerEligible read is (await GetAsync()) is null — a
    // non-null CompletedAt makes it false, and the @if (bannerEligible)
    // gate clears.

    [Fact(DisplayName = "U06 admin onboarding banner does NOT render when CompletedAt is non-null (even if the actor is a GlobalAdmin)")]
    public void BannerDoesNotRender_WhenCompleted()
    {
        var src = LoadBannerSource();

        // The completion read is (await GetAsync()) is null (M30·5 — the
        // floor: null = the banner shows; non-null = the banner clears).
        // The same GetAsync seam the AdminOnboardingController.Index action
        // calls (M30·5 — the banner + the view resolve to the same value).
        Assert.Contains("AdminOnboarding.GetAsync", src);
        Assert.Contains("completedAt is null", src);

        // The @if (bannerEligible) is the single render branch (a non-null
        // CompletedAt makes bannerEligible false → the banner clears).
        Assert.Contains("@if (bannerEligible)", src);

        // The banner is admin-scope (the IAdminOnboardingService seam — the
        // M30 AdminOnboarding singleton, the ADR 0153 D1 pin; NOT the M22
        // resident onboarding — the M22 / M30 distinction).
        Assert.Contains("IAdminOnboardingService", src);
    }

    // ── 4 — the banner link points to /admin/onboarding (M30·5) ───────────
    // The CTA link (the M22 onboarding.banner.action idiom, admin-scope)
    // points at the /admin/onboarding walk-through route. The copy is the
    // closed adminonboarding.banner.* kw-l set (M30·6 — the 21-key set U05
    // authored, the ADR 0153 D3 pin).

    [Fact(DisplayName = "U06 admin onboarding banner link points to /admin/onboarding (the M22 onboarding.banner.action idiom, admin-scope)")]
    public void BannerLinkPointsToAdminOnboarding()
    {
        var src = LoadBannerSource();

        // (a) The CTA link points at /admin/onboarding (the walk-through
        // route — the M22 onboarding.banner.action idiom, admin-scope;
        // M30·5 — the route is always reachable for a GlobalAdmin).
        Assert.Contains("href=\"/admin/onboarding\"", src);

        // (b) The copy is the closed adminonboarding.banner.* kw-l set
        // (M30·6 — the 21-key set U05 authored, the ADR 0153 D3 pin — the
        // floor: never an inline string, the kw-l TagHelper auto-escapes).
        Assert.Contains("key=\"adminonboarding.banner.text\"", src);
        Assert.Contains("key=\"adminonboarding.banner.action\"", src);

        // (c) Non-blocking (M30·5): the close button is a *client-side*
        // dismiss only (the M22 onboarding-banner.js sessionStorage flag —
        // never a write to CompletedAt; the only way to clear the banner
        // for real is the explicit "mark as complete" POST — M30·4).
        Assert.Contains("data-dismiss-key", src);
        Assert.Contains("kumunita-onboarding-banner-dismissed", src);

        // (d) The dismissal module is the shared M22 onboarding-banner.js
        // (one dismissal mechanism, two banners — the resident's and the
        // admin's — the shared .kumunita-onboarding-banner class is
        // intentional).
        Assert.Contains("kumunita-onboarding-banner", src);
        Assert.Contains("~/js/lib/onboarding-banner.js", src);
    }

    // ─── Shared helper ──────────────────────────────────────────────────────

    /// <summary>
    /// Load the <c>_AdminOnboardingBanner.cshtml</c> partial source, with the
    /// Razor comment block stripped (author documentation, not rendered
    /// markup — the <see cref="BookmarkButtonTests"/> /
    /// <see cref="NavMoreFoldTests"/> house idiom).
    /// </summary>
    private static string LoadBannerSource()
    {
        var viewsDir = ResolveViewsDir();
        var path = Path.Combine(
            viewsDir.FullName, "Shared", "_AdminOnboardingBanner.cshtml");
        Assert.True(File.Exists(path),
            $"_AdminOnboardingBanner.cshtml not found at {path}.");

        // Strip Razor comments — they are author documentation, not
        // rendered markup.
        return Regex.Replace(
            File.ReadAllText(path),
            @"\@\*.*?\*\@",
            "",
            RegexOptions.Singleline);
    }

    /// <summary>
    /// Locates the repo's <c>src/Kumunita.Web/Views</c> folder by walking up
    /// from the test assembly's output directory to the repo root (the dir
    /// that holds <c>Kumunita.slnx</c>) — the
    /// <see cref="KwLRegistryConsistencyTests.ResolveViewsDir"/> /
    /// <see cref="NavMoreFoldTests.RepoRoot"/> house idiom, correct
    /// regardless of the output depth (Debug/Release, xunit.v3, etc.).
    /// </summary>
    private static DirectoryInfo ResolveViewsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Kumunita.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null,
            "Could not locate the repo root (Kumunita.slnx) above the test output directory.");

        var views = new DirectoryInfo(Path.Combine(dir!.FullName, "src", "Kumunita.Web", "Views"));
        Assert.True(views.Exists, $"Views directory not found at {views.FullName}.");
        return views;
    }
}
