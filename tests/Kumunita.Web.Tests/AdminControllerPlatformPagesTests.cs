using Kumunita.Core.Pages;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// SP U03 (ADR 0043 D4) — the <c>/admin</c> shell's "Platform pages"
/// affordance: <see cref="AdminController.BuildPlatformPagesAsync"/> composes
/// the five shipped platform surfaces (the <c>about</c> view + the four
/// <c>Page</c> docs) into the <see cref="AdminIndexViewModel
/// .PlatformPageRow"/> rows the view renders, in footer order, with the four
/// <c>Page</c>-backed surfaces carrying their edit target
/// (<c>/pages/{id}/edit</c>) and <c>about</c> preview-only (ADR 0043 D1 — it
/// is a view, not a seeded page).
///
/// <para>
/// This pins the **pure composition seam** directly — <c>Index()</c> calls
/// it, but the suite does not invoke <c>Index()</c> itself: the shell's
/// account list round-trips through EF Core
/// (<c>identities.Users.ToListAsync()</c>), which this harness cannot reach
/// without a live Postgres. The other admin tests
/// (<c>AdminControllerBlockTests</c> / <c>SetRoleTests</c> /
/// <c>MandatoryTests</c>) target the narrow lanes for exactly this reason;
/// this one targets the new pure lane the same way.
/// </para>
///
/// <para>
/// The footer column itself is a static layout render (no controller, no
/// service in the layout — the ADR 0043 D4 unconditional-render invariant),
/// so its pin is the registry-shape test
/// (<c>KnownTranslationKeys_ParityTests.Footer_Platform_Keys_From_Adr_0043_D4_…</c>)
/// plus the <c>_Layout.cshtml</c> markup itself; there is no view-rendering
/// harness for the shared layout in this suite.
/// </para>
/// </summary>
public class AdminControllerPlatformPagesTests
{
    private static readonly string[] Slugs = { "about", "terms", "help", "privacy", "conduct" };
    private static readonly string[] Routes = { "/about", "/terms", "/help", "/privacy", "/conduct" };

    [Fact(DisplayName = "SP U03: the four Page-backed surfaces resolve to edit ids; about is preview-only (ADR 0043 D1)")]
    public async Task SeededTree_FiveRowsInOrder_FourEditTargets_AboutPreviewOnly()
    {
        var pages = SeedPages(seeded: new[] { "terms", "help", "privacy", "conduct" });

        var rows = await AdminController.BuildPlatformPagesAsync(pages);

        // Five rows, in the footer-column order (about first — ADR 0043 D4).
        Assert.Equal(Slugs, rows.Select(r => r.Slug).ToArray());
        Assert.Equal(Routes, rows.Select(r => r.Route).ToArray());

        // about: a view, not a Page doc (ADR 0043 D1) → no edit target.
        Assert.Null(rows[0].PageId);

        // The four Page-backed surfaces resolved to page ids → each is an
        // edit target (/pages/{id}/edit), the seeded page's own id.
        foreach (var row in rows.Skip(1))
        {
            Assert.False(string.IsNullOrWhiteSpace(row.PageId),
                $"surface '{row.Slug}' did not resolve to a page id");
            Assert.Equal($"id-{row.Slug}", row.PageId);
        }
    }

    [Fact(DisplayName = "SP U03: an empty tree yields five preview-only rows (absence-tolerant, no error)")]
    public async Task EmptyTree_FiveRows_AllPreviewOnly()
    {
        var pages = Substitute.For<IPageService>();
        pages.GetByPathAsync(Arg.Any<string>())
            .Returns(Task.FromException<Page>(new KeyNotFoundException()));

        var rows = await AdminController.BuildPlatformPagesAsync(pages);

        Assert.Equal(Slugs, rows.Select(r => r.Slug).ToArray());
        Assert.Equal(Routes, rows.Select(r => r.Route).ToArray());
        Assert.All(rows, r => Assert.Null(r.PageId));
    }

    [Fact(DisplayName = "SP U03: the system/{slug} primary and the bare-slug legacy fallback both resolve (ADR 0040 re-parent seam)")]
    public async Task BareSlug_Fallback_Resolves_AbsentSystemPath()
    {
        // `privacy` is under system/ (the canonical path); `conduct` is only
        // present at the bare slug (a pre-ADR-0040 instance shape); both must
        // resolve. about / terms / help are absent → preview-only rows.
        var pages = Substitute.For<IPageService>();
        pages.GetByPathAsync(Arg.Any<string>()).Returns(ci =>
        {
            var path = ci.ArgAt<string>(0);
            return path switch
            {
                "system/privacy" => Task.FromResult(new Page { Id = "id-privacy", Slug = "privacy" }),
                "conduct"        => Task.FromResult(new Page { Id = "id-conduct", Slug = "conduct" }),
                _                => Task.FromException<Page>(new KeyNotFoundException()),
            };
        });

        var rows = await AdminController.BuildPlatformPagesAsync(pages);

        var bySlug = rows.ToDictionary(r => r.Slug);
        Assert.Equal("id-privacy", bySlug["privacy"].PageId);
        Assert.Equal("id-conduct", bySlug["conduct"].PageId);
        Assert.Null(bySlug["about"].PageId);
        Assert.Null(bySlug["terms"].PageId);
        Assert.Null(bySlug["help"].PageId);
    }

    // ── Harness ──────────────────────────────────────────────────────────────

    /// <summary>
    /// A seeded <see cref="IPageService"/>: <paramref name="seeded"/> slugs
    /// resolve under <c>system/{slug}</c> (the ADR 0040 canonical path) with a
    /// deterministic id (<c>id-{slug}</c>); everything else is absent
    /// (<see cref="KeyNotFoundException"/>, the ADR 0039 §3.3 "absent" shape).
    /// </summary>
    private static IPageService SeedPages(string[] seeded)
    {
        var pages = Substitute.For<IPageService>();
        pages.GetByPathAsync(Arg.Any<string>()).Returns(ci =>
        {
            var path = ci.ArgAt<string>(0);
            var slug = path.StartsWith("system/") ? path["system/".Length..] : path;
            return seeded.Contains(slug)
                ? Task.FromResult(new Page { Id = $"id-{slug}", Slug = slug })
                : Task.FromException<Page>(new KeyNotFoundException());
        });
        return pages;
    }
}
