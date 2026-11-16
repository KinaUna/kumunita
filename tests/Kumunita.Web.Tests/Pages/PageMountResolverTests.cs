using Kumunita.Core.Pages;
using Kumunita.Web.Security;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// C12 — a **pure unit test** for <see cref="PageMountResolver"/> (ADR 0039 §3.8).
/// The resolver is display-only (no access decision): "a page mounted at
/// <paramref name="slot"/> → its <c>/pages/…</c> href + title", and
/// <c>null</c> for an unmounted slot or a soft-deleted (not-in-tree) page.
/// <para>
/// The equivalent assertions also live in <see cref="PageControllerTests"/>,
/// but that class is Testcontainers-based (<c>IClassFixture&lt;PostgresFixture&gt;</c>),
/// so this file is the non-Testcontainers unit seam the C12 gate names:
/// every public <c>*Resolver</c> must be covered by a pure unit test.
/// </para>
/// <para>
/// **Part-vs-whole (D1/D6):** the test class is a test, not a production type —
/// the part-vs-whole rule does not apply; the seam it covers
/// (<see cref="PageMountResolver"/>) has a real consumer (the layout partials)
/// and its own unit test (this file).
/// </para>
/// </summary>
public sealed class PageMountResolverTests
{
    [Fact]
    public async Task Resolve_MountedPage_ReturnsHrefAndTitle()
    {
        const string aboutId = "page-about-001";
        var about = new Page
        {
            Id = aboutId,
            Slug = "about",
            Title = "About the community",
            MountPoint = "footer/community",
            IsDeleted = false
        };
        var pages = Substitute.For<IPageService>();
        pages.GetByMountPointAsync("footer/community").Returns(about);
        pages.GetTreeAsync().Returns(new List<Page> { about });

        var resolved = await PageMountResolver.ResolveAsync(pages, "footer/community");

        Assert.NotNull(resolved);
        Assert.Equal("/pages/about", resolved!.Value.Href);
        Assert.Equal("About the community", resolved.Value.Title);
    }

    [Fact]
    public async Task Resolve_UnmountedSlot_ReturnsNull()
    {
        var pages = Substitute.For<IPageService>();
        pages.GetByMountPointAsync("footer/community").Returns((Page?)null);

        var resolved = await PageMountResolver.ResolveAsync(pages, "footer/community");

        Assert.Null(resolved);
    }

    [Fact]
    public async Task Resolve_SoftDeletedPage_ReturnsNull()
    {
        // The mounted page has since been soft-deleted — it is not in the
        // live tree, so the resolver cannot derive a valid /pages/… href.
        var about = new Page
        {
            Id = "page-about-001",
            Slug = "about",
            Title = "About",
            MountPoint = "footer/community",
            IsDeleted = true
        };
        var pages = Substitute.For<IPageService>();
        pages.GetByMountPointAsync("footer/community").Returns(about);
        pages.GetTreeAsync().Returns(new List<Page>());   // not in the live tree

        var resolved = await PageMountResolver.ResolveAsync(pages, "footer/community");

        Assert.Null(resolved);
    }

    [Fact]
    public async Task Resolve_NullOrEmptySlot_ReturnsNull()
    {
        var pages = Substitute.For<IPageService>();

        Assert.Null(await PageMountResolver.ResolveAsync(pages, null!));
        Assert.Null(await PageMountResolver.ResolveAsync(pages, ""));
        Assert.Null(await PageMountResolver.ResolveAsync(pages, "   "));
    }
}
