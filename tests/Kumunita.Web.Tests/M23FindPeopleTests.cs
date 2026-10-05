using System.Reflection;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Kumunita.Core.Localization;
using Kumunita.Core.Query;
using Kumunita.Core.Tags;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// M23 · U05 — the <c>/people</c> find-people Web-surface pin (the U05 exit
/// gate's Web.Tests-only half). The find is over the <b>actor-visible</b>
/// resident set: the U03 <see cref="IProfileFindService"/> composes the frozen
/// <c>ProfileToAuditableResource</c> + <c>CanSeeAsync</c> gate (the M3
/// <c>ListFeedAsync</c> shape) — <b>zero new authorization surface</b>
/// (C-M23·2, D7). A match is a <b>feed organizer, never a gate</b> (D6): a
/// profile the viewer cannot see never surfaces. U05 projects <b>exactly</b>
/// what the U03 service returned and never a field it did not surface.
/// <list type="number">
/// <item><b>The by-tag find (D4/D6, GATE-2).</b> A signed-in viewer's
///       <c>ByTag</c> action calls
///       <see cref="IProfileFindService.FindPeopleByTagAsync"/> with the slug,
///       the signed-in subject, and the page — and projects the returned
///       <see cref="ProfileTagPage"/> onto the view model verbatim (the
///       already-<c>Visibility</c>-gated survivors; a denied profile the
///       service omitted is never re-surfaced here).</item>
/// <item><b>The by-bio find (D4/D6, GATE-2).</b> A signed-in viewer's
///       <c>Index</c> action with a non-blank bio calls
///       <see cref="IProfileFindService.FindPeopleByBioAsync"/> with the
///       <b>trimmed</b> query, the signed-in subject, and the page — and
///       projects the returned <see cref="ProfileBioPage"/> verbatim.</item>
/// <item><b>The empty state (D4, F6) + the by-tag form's redirect.</b> A blank
///       bio renders the index forms (the U03 "no decision, no row" shape —
///       no 404, no error; D6: an empty result is a valid result); a
///       not-found tag renders the empty-state <c>Tag</c> view (not a 404).
///       The index's by-tag form posts the slug as a <c>tag</c> field (a form
///       GET cannot carry a field into the <c>/people/tag/{slug}</c> path
///       segment without inline script — SECURITY.md §6) and <c>Index</c>
///       302-redirects to the canonical <c>ByTag</c> route.</item>
/// <item><b>The paging (the ADR 0090 D6 idiom) + the nav entry (F6) + the six
///       U05 kw-l keys non-empty in en/de/fr/da.</b> The view models carry the
///       ADR 0090 D6 <c>HasMore</c> flag + page; the results views emit a
///       "Next page" link only when <c>HasMore</c>; the <c>_Layout.cshtml</c>
///       dropdown + rail carry the <c>nav.people</c> item linking to
///       <c>/people</c>; and each of the six U05 keys carries a non-empty
///       value in all four languages.</item>
/// <item><b>The find requires sign-in (F6, [Authorize]) + zero new
///       authorization surface (GATE-4, C-M23·2, D7).</b> A pin that
///       <see cref="FindPeopleController"/> carries <c>[Authorize]</c> (no
///       anonymous find), and a reflection pin that
///       <see cref="Kumunita.Core.Authorization.IAuthorizationService"/>'s
///       method set is unchanged and <see cref="ClaimTypes.All"/> is
///       unchanged.</item>
/// </list>
/// <para>
/// <b>Test-construction floor (transparency of the pin):</b> the controller is
/// driven by direct action invocation with an NSubstitute
/// <see cref="IProfileFindService"/> (the U04
/// <c>M23ProfileExtendedTests</c> / <c>AnnouncementControllerTests</c> idiom)
/// and a principal carrying the single <c>Kumunita.Sub</c> claim
/// <see cref="Kumunita.Web.Security.KumunitaPrincipal"/> mints. The
/// <c>Visibility</c> gate itself (the U03 <c>CanSeeAsync</c> pass, one
/// aggregate audit row, the no-leak pin) is the Core.Tests / e2e half of the
/// U05 gate (U03's <c>ProfileFindServiceTests</c>); these tests pin what the
/// <b>Web boundary</b> projects and never re-derives access.
/// </para>
/// </summary>
public sealed class M23FindPeopleTests
{
    private const string Viewer = "subj-m23-fp-viewer";
    private const string ResidentA = "subj-m23-fp-resident-a";
    private const string ResidentB = "subj-m23-fp-resident-b";

    // ── Face 1 — the by-tag find (D4/D6, GATE-2) ─────────────────────────

    /// <summary>
    /// D4/D6 (GATE-2) — a signed-in viewer's <c>ByTag(slug, page)</c> action
    /// calls <see cref="IProfileFindService.FindPeopleByTagAsync"/> with the
    /// slug, the <b>signed-in subject</b> (the F6 "no anonymous find" idiom —
    /// the viewer is never a form value), and the page — and projects the
    /// returned <see cref="ProfileTagPage"/> onto the view model verbatim:
    /// the already-<c>Visibility</c>-gated survivors (a profile the service
    /// omitted is never re-surfaced here — the D6 no-leak pin).
    /// </summary>
    [Fact]
    public async Task ByTag_Passes_SlugSubjectPage_AndProjects_TheGate_ReturnedPage()
    {
        var (controller, find) = BuildController();

        var a = Profile(ResidentA, "Alice", true);
        var b = Profile(ResidentB, "Bob", false);
        var tag = new Tag { Id = "tag-gardening", Slug = "gardening", Name = "Gardening", LanguageCode = "en", CreatedBy = ResidentA };
        var page = new ProfileTagPage(new[] { a, b }, tag, HasMore: true);
        find.FindPeopleByTagAsync("gardening", Viewer, 2, null).Returns(page);

        var result = await controller.ByTag("gardening", 2);

        // The Web boundary supplies the signed-in subject (never a form value).
        await find.Received(1).FindPeopleByTagAsync("gardening", Viewer, 2, null);

        var vm = Assert.IsType<FindPeopleTagViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("gardening", vm.Slug);
        Assert.Equal(2, vm.Page);
        Assert.True(vm.Result.HasMore);          // ADR 0090 D6 flag carried verbatim
        Assert.Equal(2, vm.Result.Profiles.Count);
        Assert.Equal(ResidentA, vm.Result.Profiles[0].SubjectId);
        Assert.Equal("Gardening", vm.TagDisplayName);   // the resolved tag's base name (a label, never a gate — C-M23·6)
    }

    // ── Face 2 — the by-bio find (D4/D6, GATE-2) ─────────────────────────

    /// <summary>
    /// D4/D6 (GATE-2) — a signed-in viewer's <c>Index(bio)</c> action with a
    /// non-blank bio calls
    /// <see cref="IProfileFindService.FindPeopleByBioAsync"/> with the
    /// <b>trimmed</b> query, the signed-in subject, and the page — and projects
    /// the returned <see cref="ProfileBioPage"/> onto the view model verbatim.
    /// </summary>
    [Fact]
    public async Task Index_Bio_Passes_TrimmedQuerySubjectPage_AndProjects_TheGate_ReturnedPage()
    {
        var (controller, find) = BuildController();

        var a = Profile(ResidentA, "Alice", true);
        var page = new ProfileBioPage(new[] { a }, HasMore: false);
        find.FindPeopleByBioAsync("baker", Viewer, 1, null).Returns(page);

        var result = await controller.Index(bio: "  baker  ", page: 1);

        await find.Received(1).FindPeopleByBioAsync("baker", Viewer, 1, null);

        var vm = Assert.IsType<FindPeopleBioViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("baker", vm.Query);       // trimmed (the M8 D4 substring engine)
        Assert.Equal(1, vm.Page);
        Assert.False(vm.Result.HasMore);
        Assert.Equal(ResidentA, vm.Result.Profiles[0].SubjectId);
    }

    // ── Face 3 — the empty state (D4, F6) + the by-tag form's redirect ────

    /// <summary>
    /// D4/F6 — a blank bio renders the <b>index forms</b> (the U03 "no
    /// decision, no row" shape — no 404, no error; D6: an empty result is a
    /// valid result), and <b>no</b> find call runs (the early return precedes
    /// any decision). The U03 service is never consulted for a blank query.
    /// </summary>
    [Fact]
    public async Task Index_BlankBio_Returns_TheIndexForms_Without_AnyFindCall()
    {
        var (controller, find) = BuildController();

        var result = await controller.Index(bio: "   ", page: 1);   // whitespace-only ⇒ blank

        var vm = Assert.IsType<FindPeopleIndexViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.NotNull(vm);
        // The U03 service is the gate; a blank query must not reach it (no
        // decision, no row — the M3 0-candidate shape).
        await find.DidNotReceiveWithAnyArgs().FindPeopleByBioAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SortSpec?>());
    }

    /// <summary>
    /// D4/D6 — a <b>not-found</b> tag (the U03 service returns an empty page
    /// with <c>Tag = null</c>) renders the empty-state <see cref="FindPeopleTagViewModel"/>
    /// (the <c>profile.find.empty</c> key, <c>Profiles.Count == 0</c>) —
    /// <b>not</b> a 404 and <b>not</b> an error (D6: a match is a feed
    /// organizer, never a gate; a miss is a valid empty result).
    /// </summary>
    [Fact]
    public async Task ByTag_MissingTag_Returns_EmptyStateView_NotNotFound()
    {
        var (controller, find) = BuildController();

        // The U03 "missing tag ⇒ empty page, no row" shape: empty profiles,
        // null tag, no more.
        find.FindPeopleByTagAsync("definitely-not-a-tag", Viewer, 1, null)
            .Returns(new ProfileTagPage(Array.Empty<Profile>(), null, false));

        var result = await controller.ByTag("definitely-not-a-tag", 1);

        var vm = Assert.IsType<FindPeopleTagViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Empty(vm.Result.Profiles);      // the empty-state shape (no 404)
        Assert.Null(vm.Result.Tag);            // a miss → the empty state
        Assert.Null(vm.TagDisplayName);
    }

    /// <summary>
    /// The by-tag form's redirect (the index's <c>GET /people?tag={slug}</c>):
    /// a form GET cannot carry a field into the <c>/people/tag/{slug}</c> path
    /// segment without inline script (SECURITY.md §6), so <c>Index</c> 302-
    /// redirects to the canonical <see cref="ByTag"/> route (the results URL +
    /// the paging link target). The slug is URI-escaped.
    /// </summary>
    [Fact]
    public async Task Index_TagForm_Redirects_To_TheCanonical_ByTagRoute()
    {
        var (controller, find) = BuildController();

        var result = await controller.Index(tag: "gardening", page: 1);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/people/tag/gardening", redirect.Url);
        // The redirect short-circuits before any find call (no decision, no
        // row on the index surface).
        await find.DidNotReceiveWithAnyArgs().FindPeopleByTagAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SortSpec?>());
    }

    // ── Face 4 — the paging idiom (ADR 0090 D6) ───────────────────────────

    /// <summary>
    /// ADR 0090 D6 — the view model carries the <c>HasMore</c> flag + page
    /// verbatim from the U03 page, so the results views can emit a "Next
    /// page" link (to <c>?page=N+1</c>) exactly when <c>HasMore</c> and none
    /// when it is the last page. Page 1 of a full page (30, HasMore true) vs.
    /// the final page (HasMore false).
    /// </summary>
    [Fact]
    public async Task Paging_TheViewModel_Carries_TheHasMoreFlag_AndPage()
    {
        var (controller, find) = BuildController();

        // A full first page (the U03 PageSize = 30 slice) → HasMore true.
        find.FindPeopleByBioAsync("a", Viewer, 1, null)
            .Returns(new ProfileBioPage(Enumerable.Range(0, 30).Select(i => Profile($"subj-{i}", $"P{i}", false)).ToList(), true));
        var first = await controller.Index(bio: "a", page: 1);
        var vm1 = Assert.IsType<FindPeopleBioViewModel>(Assert.IsType<ViewResult>(first).Model);
        Assert.True(vm1.Result.HasMore);
        Assert.Equal(1, vm1.Page);

        // The final page (the remainder, fewer than PageSize) → HasMore false.
        find.FindPeopleByBioAsync("a", Viewer, 2, null)
            .Returns(new ProfileBioPage(new[] { Profile(ResidentA, "Alice", true) }, false));
        var last = await controller.Index(bio: "a", page: 2);
        var vm2 = Assert.IsType<FindPeopleBioViewModel>(Assert.IsType<ViewResult>(last).Model);
        Assert.False(vm2.Result.HasMore);
        Assert.Equal(2, vm2.Page);
    }

    // ── Face 5 — the nav entry (F6) + the six U05 kw-l keys ───────────────

    /// <summary>
    /// F6 — the <c>_Layout.cshtml</c> carries a "People" nav item in <b>both</b>
    /// variants (the variant-B "More ▾" dropdown next to Directory, and the
    /// variant-C icon rail next to Directory), each linking to
    /// <c>FindPeople/Index</c> and carrying the U05 <c>nav.people</c> kw-l
    /// key (a non-empty value in the current language — the parity pin below).
    /// </summary>
    [Fact]
    public void Layout_Carries_APeopleNavEntry_InBothVariants()
    {
        var path = Path.Combine(RepoRoot, "src", "Kumunita.Web", "Views", "Shared", "_Layout.cshtml");
        Assert.True(File.Exists(path), $"_Layout.cshtml not found at {path}.");
        var html = File.ReadAllText(path);

        // The U05 nav key is referenced (both variants reuse the one key).
        Assert.Contains("key=\"nav.people\"", html);

        // Both variants link to the FindPeople controller (dropdown + rail).
        var findPeopleLinks = Regex.Matches(html, "asp-controller=\"FindPeople\"\\s+asp-action=\"Index\"");
        Assert.True(findPeopleLinks.Count >= 2,
            $"Expected ≥2 'FindPeople/Index' nav links (variant-B dropdown + variant-C rail); found {findPeopleLinks.Count}.");
    }

    /// <summary>
    /// The results views (the F6 surface) render the U05 empty state + the
    /// ADR 0090 D6 "Next page" link only when <c>HasMore</c>, and keep the
    /// existing <c>/directory/[subjectId]</c> profile-link target (the design
    /// doc §7 reuse pin — not re-keyed).
    /// </summary>
    [Fact]
    public void People_Views_Carry_TheEmptyState_ThePagingLink_AndTheDirectoryLinkTarget()
    {
        foreach (var view in new[] { "Tag", "Bio" })
        {
            // The FindPeopleController's views live in Views/FindPeople/ (the
            // controller-name folder — Razor's View("Tag") resolves against it).
            var path = Path.Combine(RepoRoot, "src", "Kumunita.Web", "Views", "FindPeople", view + ".cshtml");
            Assert.True(File.Exists(path), $"{view}.cshtml not found at {path}.");
            var html = File.ReadAllText(path);

            // The empty state (D6 — a valid empty result, the profile.find.empty key).
            Assert.Contains("key=\"profile.find.empty\"", html);
            // The ADR 0090 D6 paging link (the pagination.next reuse — the Documents idiom).
            Assert.Contains("HasMore", html);
            Assert.Contains("key=\"pagination.next\"", html);
            // The existing profile-link target (the §7 reuse pin — /directory/[subjectId]).
            Assert.Contains("Detail\", \"Directory\"", html);
        }
    }

    /// <summary>
    /// The six U05 <c>kw-l</c> keys (the plan's §key table) each carry a
    /// non-empty value in all four languages (the
    /// <see cref="KnownTranslationKeys"/> en/de/fr/da registry — the
    /// <c>KwLRegistryConsistencyTests</c> + <c>KnownTranslationKeys_ParityTests</c>
    /// gate, asserted from the Web assembly so the U05 Web exit pins its own
    /// surface). A blank value in any language would make the provider floor
    /// resolve to nothing for that key under that preference.
    /// </summary>
    [Fact]
    public void U05_KwLKeys_Are_NonEmpty_InAllFourLanguages()
    {
        var keys = new[]
        {
            "nav.people",
            "profile.find.title",
            "profile.find.by_tag",
            "profile.find.by_bio",
            "profile.find.results",
            "profile.find.empty",
        };
        Assert.Equal(6, keys.Length);

        var registry = new (string Name, IReadOnlyDictionary<string, string> Values)[]
        {
            ("en", KnownTranslationKeys.EnValues),
            ("de", KnownTranslationKeys.DeValues),
            ("fr", KnownTranslationKeys.FrValues),
            ("da", KnownTranslationKeys.DaValues),
        };

        foreach (var (lang, values) in registry)
        {
            foreach (var key in keys)
            {
                Assert.True(values.ContainsKey(key),
                    $"U05 kw-l key '{key}' is missing from the {lang} registry (KnownTranslationKeys.{lang}Values)");
                Assert.False(string.IsNullOrWhiteSpace(values[key]),
                    $"U05 kw-l key '{key}' has an empty {lang} value — the provider floor would resolve it to nothing");
            }
        }
    }

    // ── Face 6 — requires sign-in (F6, [Authorize]) + zero new authz surface (GATE-4) ──

    /// <summary>
    /// F6 ([Authorize]) — the find is over "who may this actor see," so it
    /// requires sign-in: an anonymous <c>GET /people</c> is redirected to the
    /// cookie login, never rendered. The pin is that
    /// <see cref="FindPeopleController"/> carries the <c>[Authorize]</c>
    /// attribute (a structural pin in the house idiom — driving an anonymous
    /// request through the full auth middleware needs a TestServer the house
    /// idiom avoids).
    /// </summary>
    [Fact]
    public void FindPeopleController_Requires_SignIn()
    {
        var authorize = typeof(FindPeopleController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false);
        Assert.True(authorize.Length > 0, "FindPeopleController is not [Authorize]-annotated — an anonymous /people would render results (F6 violation).");
    }

    /// <summary>
    /// GATE-4 / C-M23·2 / D7 — the find-people surface introduces <b>no</b>
    /// new authorization surface: <see cref="Kumunita.Core.Authorization.IAuthorizationService"/>
    /// exposes exactly the eight pre-existing method signatures and
    /// <see cref="ClaimTypes.All"/> is unchanged (the four admissible claims).
    /// A "U05 added a <c>CanSeeProfileFindAsync</c>" or a new claim pin would
    /// fail here — the whole point of M23 is that it rides the existing
    /// <c>directory</c> Read path (the U03 gate), it does not extend the seams.
    /// </summary>
    [Fact]
    public void AuthorizationSurface_Is_Unchanged_ZeroNewSurface()
    {
        var signatures = typeof(Kumunita.Core.Authorization.IAuthorizationService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName && !m.IsGenericMethod)
            .Select(m => (m.Name, m.GetParameters().Length))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ThenBy(t => t.Item2)
            .ToList();

        Assert.Equal(
            new (string, int)[]
            {
                ("CanAsync", 3), ("CanAsync", 4),
                ("CanSeeAsync", 3), ("CanSeeAsync", 4),
                ("CanSeeGroupAsync", 3), ("CanSeeGroupAsync", 4),
                ("CanSeeGroupFeedAsync", 3), ("CanSeeGroupFeedAsync", 4),
            },
            signatures);

        var claims = Kumunita.Core.Identity.ClaimTypes.All
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(
            new[] { "Kumunita.ExternalId", "Kumunita.Role", "Kumunita.Sub", "Kumunita.Verified" },
            claims);
    }

    // ── fixtures ────────────────────────────────────────────────────────────

    /// <summary>A minimal non-blocked <see cref="Profile"/> row for a results
    /// page (the already-<c>Visibility</c>-gated survivor shape the U03
    /// service returns).</summary>
    private static Profile Profile(string subjectId, string displayName, bool verified) => new()
    {
        SubjectId = subjectId,
        DisplayName = displayName,
        Verified = verified,
        Blocked = false,
    };

    /// <summary>Construct the <see cref="FindPeopleController"/> with an
    /// NSubstitute <see cref="IProfileFindService"/> and a principal carrying
    /// the single <c>Kumunita.Sub</c> claim (the U04
    /// <c>SetViewer</c> idiom).</summary>
    private static (FindPeopleController controller, IProfileFindService find) BuildController()
    {
        var find = Substitute.For<IProfileFindService>();
        var controller = new FindPeopleController(find);

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[] { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Viewer) },
                authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return (controller, find);
    }

    /// <summary>Walk up from the test output directory to the repo root (the
    /// <see cref="PwaManifestTests.RepoRoot"/> idiom) — correct regardless of
    /// the output depth (Debug/Release, xunit.v3, etc.).</summary>
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Kumunita.slnx")))
                dir = dir.Parent;
            Assert.True(dir is not null, "Could not locate the repo root (Kumunita.slnx).");
            return dir!.FullName;
        }
    }
}
