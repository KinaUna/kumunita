using Kumunita.Core.Targets;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The C05 <see cref="ContentTargetKinds"/> truth table — the closed
/// <c>TargetKind</c> sets (ADR 0118 D1 / ADR 0167 D1), the static
/// <c>page-*</c> rows (the ADR 0167 amendment), and the link templates —
/// asserted against the pure kind table directly. A **pure** unit — no
/// <c>PostgresFixture</c>, no <see cref="Marten.IDocumentStore"/> (the
/// table has no session, no DI, no I/O).
/// <para>
/// The session-touching loader calls live in
/// <see cref="ContentTargetResolver"/> (integration-tested at the seam —
/// the <c>BookmarkServiceTests</c> / <c>PersonalNoteServiceTests</c>
/// families) and are NOT covered here.
/// </para>
/// </summary>
public class ContentTargetKindsTests
{
    // ── Closed sets ──────────────────────────────────────────────────────

    [Fact]
    public void BookmarksSet_IsTheNineKindsFromA0118()
    {
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "post", "event", "todo", "announcement", "page",
            "project", "board", "inventory", "directory",
        };
        Assert.Equal(expected, new HashSet<string>(ContentTargetKinds.BookmarksKinds, StringComparer.Ordinal));
        Assert.Equal(9, ContentTargetKinds.BookmarksKinds.Count);
    }

    [Fact]
    public void PersonalNotesSet_IsBookmarksPlusTheAmendmentKinds()
    {
        // Every Bookmarks kind is a PersonalNotes kind (ADR 0118 amendment
        // — the sets were unified in one direction).
        foreach (var k in ContentTargetKinds.BookmarksKinds)
        {
            Assert.Contains(k, ContentTargetKinds.PersonalNotesKinds);
        }
        // The ADR 0167 amendment adds grouppost + document + the 11 page-*
        // kinds.
        Assert.Contains("grouppost", ContentTargetKinds.PersonalNotesKinds);
        Assert.Contains("document", ContentTargetKinds.PersonalNotesKinds);
        Assert.Equal(21, ContentTargetKinds.PersonalNotesKinds.Count);
    }

    [Fact]
    public void OrderedSets_MatchTheClosedSetAndAreConsistent()
    {
        Assert.Equal(
            new HashSet<string>(ContentTargetKinds.BookmarksKinds, StringComparer.Ordinal),
            new HashSet<string>(ContentTargetKinds.BookmarksOrdered, StringComparer.Ordinal));
        Assert.Equal(
            new HashSet<string>(ContentTargetKinds.PersonalNotesKinds, StringComparer.Ordinal),
            new HashSet<string>(ContentTargetKinds.PersonalNotesOrdered, StringComparer.Ordinal));
    }

    // ── Kind-set membership (IsValid) ────────────────────────────────────

    [Theory]
    [InlineData("post", true)]
    [InlineData("grouppost", false)]
    [InlineData("document", false)]
    [InlineData("page-group", false)]
    [InlineData("not-a-kind", false)]
    public void IsValid_BookmarksSurface(string kind, bool expected)
    {
        Assert.Equal(expected, ContentTargetKinds.IsValid(kind, ContentTargetKinds.Bookmarks));
    }

    [Theory]
    [InlineData("post", true)]
    [InlineData("grouppost", true)]
    [InlineData("document", true)]
    [InlineData("page-group", true)]
    [InlineData("not-a-kind", false)]
    public void IsValid_PersonalNotesSurface(string kind, bool expected)
    {
        Assert.Equal(expected, ContentTargetKinds.IsValid(kind, ContentTargetKinds.PersonalNotes));
    }

    [Fact]
    public void IsValid_UnknownSurface_IsAlwaysFalse()
    {
        Assert.False(ContentTargetKinds.IsValid("post", "unknown-surface"));
    }

    // ── Static page-* rows (the ADR 0167 amendment) ──────────────────────

    [Fact]
    public void TryStaticRow_EveryStaticPageKind_ResolvesFixedTitleAndLink()
    {
        // The eight static page-* kinds: the page itself is the surface,
        // always visible, fixed title + link.
        Assert.True(ContentTargetKinds.TryStaticRow("page-announcements", out var a));
        Assert.Equal(("Announcements", "/announcements"), a);

        Assert.True(ContentTargetKinds.TryStaticRow("page-community", out var c));
        Assert.Equal(("Community", "/community"), c);

        Assert.True(ContentTargetKinds.TryStaticRow("page-events", out var e));
        Assert.Equal(("Events", "/events"), e);

        Assert.True(ContentTargetKinds.TryStaticRow("page-projects", out var p));
        Assert.Equal(("Projects", "/projects"), p);

        Assert.True(ContentTargetKinds.TryStaticRow("page-todos", out var t));
        Assert.Equal(("To-dos", "/projects/todos"), t);

        Assert.True(ContentTargetKinds.TryStaticRow("page-boards", out var b));
        Assert.Equal(("Boards", "/projects/boards"), b);

        Assert.True(ContentTargetKinds.TryStaticRow("page-inventory", out var i));
        Assert.Equal(("Inventory", "/inventory"), i);

        Assert.True(ContentTargetKinds.TryStaticRow("page-documents", out var d));
        Assert.Equal(("Documents", "/documents"), d);

        Assert.Equal(8, ContentTargetKinds.AllStaticRows.Count);
    }

    [Fact]
    public void TryStaticRow_NonStaticKinds_ReturnFalse()
    {
        // The two *dynamic* page-* kinds are NOT static rows — they re-run
        // the owning surface's frozen read (the loader, not the table).
        Assert.False(ContentTargetKinds.TryStaticRow("page-group", out _));
        Assert.False(ContentTargetKinds.TryStaticRow("page-project", out _));
        // And no ordinary kind is a static row.
        Assert.False(ContentTargetKinds.TryStaticRow("post", out _));
        Assert.False(ContentTargetKinds.TryStaticRow("event", out _));
    }

    // ── Link templates (the simple kinds — no session needed) ─────────────

    [Fact]
    public void LinkTemplates_MatchTheDetailRoutes()
    {
        Assert.Equal("/posts/p1", ContentTargetKinds.PostLink("p1"));
        Assert.Equal("/groups/g1/posts/p1", ContentTargetKinds.GrouppostLink("g1", "p1"));
        Assert.Equal("/events/e1", ContentTargetKinds.EventLink("e1"));
        Assert.Equal("/projects/todos/t1", ContentTargetKinds.TodoLink("t1"));
        Assert.Equal("/projects/boards/b1", ContentTargetKinds.BoardLink("b1"));
        Assert.Equal("/announcements/a1", ContentTargetKinds.AnnouncementLink("a1"));
        Assert.Equal("/inventory/i1", ContentTargetKinds.InventoryLink("i1"));
        Assert.Equal("/directory/u1", ContentTargetKinds.DirectoryLink("u1"));
        Assert.Equal("/documents/d1", ContentTargetKinds.DocumentLink("d1"));
        Assert.Equal("/groups/g1", ContentTargetKinds.GroupLink("g1"));
        Assert.Equal("/projects/projects/pr1", ContentTargetKinds.PageProjectLink("pr1"));
    }

    [Fact]
    public void ProjectLink_PreservesTheSurfaceDivergence()
    {
        // Byte-identical behaviour: the M17 surface links the project detail
        // page (/projects/projects/{id}); the M37 surface links the projects
        // page (/projects/{id}) — the ADR 0118 amendment shape re-applied.
        // The divergence is recorded here, not re-derived per surface.
        Assert.Equal("/projects/projects/pr1", ContentTargetKinds.ProjectLink("pr1", ContentTargetKinds.Bookmarks));
        Assert.Equal("/projects/pr1", ContentTargetKinds.ProjectLink("pr1", ContentTargetKinds.PersonalNotes));
    }

    [Fact]
    public void PageGroupStaticId_IsTheIndexPageId()
    {
        // The page-group "static" id is the /groups index page (the note is
        // about the index itself, not a real group).
        Assert.Equal("static", ContentTargetKinds.PageGroupStaticId);
    }
}
