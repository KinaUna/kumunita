using System.Security.Claims;
using Kumunita.Core;
using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Bookmarks;
using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
using Kumunita.Core.Projects;
using Kumunita.Core.UserInfo;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M17 / U02 — the 14 pinned <see cref="BookmarkService"/> seam tests
/// (design doc <c>m17-bookmarks-design.md</c> §2.5, names locked verbatim).
/// <para>
/// The harness follows the M3 <c>PostServiceTests</c> shape: a shared
/// <see cref="PostgresFixture"/> (Testcontainers <c>postgres:18</c>), a
/// fresh scratch database per test method, the full DDL applied
/// (<c>CoreDocTypes</c> + <c>PostDocTypes</c> + <c>BookmarkDocTypes</c>), and the
/// seven-seam <see cref="BookmarkService"/> composition over the scratch
/// store. No NSubstitute — the frozen seams are real (the M2
/// <c>DirectoryServiceTests</c> / M3 <c>PostServiceTests</c> precedent).
/// </para>
/// <para>
/// **F2 tests** drive the D3 write-lane visibility check with a
/// <c>post</c> target (the most representative surface): a visible post
/// (author-branch Allow) passes; an invisible post (neither author nor in
/// the audience) is Refused. The other four kinds follow the same pattern.
/// </para>
/// </summary>
public class BookmarkServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string ComponentId = "c-u9-comp";
    private string? _conn;   // set by BootStoreAsync; used by Services() to build the EF identity schema

    // ── 1 — F1_ToggleIsIdempotentOneRow (F1, the unique-index witness) ──

    [Fact]
    public async Task F1_ToggleIsIdempotentOneRow()
    {
        var store = await BootStoreAsync();
        var (_, _, bm) = Services(store);
        const string owner = "u-f1-owner";
        const string postId = "f1-post";

        await PlantVisiblePost(store, postId, owner);

        // First toggle → Bookmarked.
        var first = await RunInSession(store, s => bm.ToggleAsync(owner, "post", postId, s));
        Assert.Equal(BookmarkToggleStatus.Bookmarked, first.Status);

        // Second toggle → AlreadyBookmarked (the F1 no-op).
        var second = await RunInSession(store, s => bm.ToggleAsync(owner, "post", postId, s));
        Assert.Equal(BookmarkToggleStatus.AlreadyBookmarked, second.Status);

        // The BookmarkDocTypes unique index is the witness: exactly ONE row.
        var rows = await Bookmarks(store, owner);
        Assert.Single(rows, b => b.TargetKind == "post" && b.TargetId == postId);
    }

    // ── 2 — F1_ToggleRemoveIsPhysicalNoIsDeleted (D4) ──────────────────

    [Fact]
    public async Task F1_ToggleRemoveIsPhysicalNoIsDeleted()
    {
        var store = await BootStoreAsync();
        var (_, _, bm) = Services(store);
        const string owner = "u-f1b-owner";
        const string postId = "f1b-post";

        await PlantVisiblePost(store, postId, owner);

        // Bookmark → Bookmarked.
        await RunInSession(store, s => bm.ToggleAsync(owner, "post", postId, s));

        // Remove → Removed (D4 — physical removal, no IsDeleted to flip).
        var removed = await RunInSession(store, s => bm.RemoveAsync(owner, "post", postId, s));
        Assert.Equal(BookmarkToggleStatus.Removed, removed.Status);

        // Physical: zero rows for this owner/kind/id.
        var rows = await Bookmarks(store, owner);
        Assert.DoesNotContain(rows, b => b.TargetKind == "post" && b.TargetId == postId);

        // Re-bookmark after removal is a fresh Bookmarked (D4).
        var reBooked = await RunInSession(store, s => bm.ToggleAsync(owner, "post", postId, s));
        Assert.Equal(BookmarkToggleStatus.Bookmarked, reBooked.Status);
    }

    // ── 3 — F2_WriteLaneRequiresTargetVisibility (D3, positive) ────────

    [Fact]
    public async Task F2_WriteLaneRequiresTargetVisibility()
    {
        var store = await BootStoreAsync();
        var (_, _, bm) = Services(store);
        const string owner = "u-f2-owner";
        const string postId = "f2-post";

        await PlantVisiblePost(store, postId, owner);

        // The D3 check passes (the author branch): a visible target is
        // bookmarkable, the row is created.
        var result = await RunInSession(store, s => bm.ToggleAsync(owner, "post", postId, s));
        Assert.Equal(BookmarkToggleStatus.Bookmarked, result.Status);

        var rows = await Bookmarks(store, owner);
        Assert.Single(rows, b => b.TargetKind == "post" && b.TargetId == postId);
    }

    // ── 4 — F2_InvisibleTargetRefused404NoRow (D3, negative) ──────────

    [Fact]
    public async Task F2_InvisibleTargetRefused404NoRow()
    {
        var store = await BootStoreAsync();
        var (_, _, bm) = Services(store);
        const string owner = "u-f2b-owner";   // the one trying to bookmark
        const string poster = "u-f2b-poster"; // the post's author
        const string postId = "f2b-post";

        // A post authored by "poster" with an audience that grants only
        // "poster" — the owner is neither the author nor in the audience.
        await Plant(store, new Component { Id = ComponentId, Name = "Test", Enabled = true });
        await Plant(store, new Post
        {
            Id = postId,
            ComponentId = ComponentId,
            AuthorId = poster,
            Body = "f2b body",
            Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, poster),
        });

        // D3: the target's own CanAsync(Read) → Deny ⇒ Refused (404),
        // no row created, no surviving bookmark audit row.
        var result = await RunInSession(store, s => bm.ToggleAsync(owner, "post", postId, s));
        Assert.Equal(BookmarkToggleStatus.Refused, result.Status);

        // No Bookmark row survives.
        var rows = await Bookmarks(store, owner);
        Assert.DoesNotContain(rows, b => b.TargetId == postId);
    }

    // ── 5 — F2_DuplicateIsNoOp (F1, the AlreadyBookmarked status) ─────

    [Fact]
    public async Task F2_DuplicateIsNoOp()
    {
        var store = await BootStoreAsync();
        var (_, _, bm) = Services(store);
        const string owner = "u-f2c-owner";
        const string postId = "f2c-post";

        await PlantVisiblePost(store, postId, owner);

        // First toggle → Bookmarked (a fresh row).
        var first = await RunInSession(store, s => bm.ToggleAsync(owner, "post", postId, s));
        Assert.Equal(BookmarkToggleStatus.Bookmarked, first.Status);

        // Capture the Created timestamp.
        var row1 = (await Bookmarks(store, owner)).First(b => b.TargetId == postId);

        // Second toggle → AlreadyBookmarked (the no-op; one row, one Created).
        var second = await RunInSession(store, s => bm.ToggleAsync(owner, "post", postId, s));
        Assert.Equal(BookmarkToggleStatus.AlreadyBookmarked, second.Status);

        // The row's Created is unchanged (no mutation — D4).
        var row2 = (await Bookmarks(store, owner)).First(b => b.TargetId == postId);
        Assert.Equal(row1.Created, row2.Created);
        Assert.Equal(row1.Id, row2.Id); // same row, not a new one
    }

    // ── 6 — F3_OwnerListLoadsZeroAuditRows (C-M17·2) ──────────────────

    [Fact]
    public async Task F3_OwnerListLoadsZeroAuditRows()
    {
        var store = await BootStoreAsync();
        var (_, _, bm) = Services(store);
        const string owner = "u-f3-owner";
        const string postId = "f3-post";

        await PlantVisiblePost(store, postId, owner);

        // Bookmark (creates the target's own audit row via CanAsync(Read)).
        await RunInSession(store, s => bm.ToggleAsync(owner, "post", postId, s));

        // Snapshot the audit count BEFORE the list load.
        var before = (await AllAudits(store)).Count;

        // ListAsync — the owner's personal read (C-M17·2: zero audit rows).
        var list = await bm.ListAsync(owner);
        Assert.Single(list.Groups, g => g.Kind == "post");
        Assert.Single(list.Groups.First(g => g.Kind == "post").Items);

        // The list load itself committed ZERO new audit rows.
        var after = (await AllAudits(store)).Count;
        Assert.Equal(before, after);
    }

    // ── 7 — F3_NonOwnerGets404NoAuditRow (C-M17·2, non-owner) ─────────

    [Fact]
    public async Task F3_NonOwnerGets404NoAuditRow()
    {
        var store = await BootStoreAsync();
        var (_, _, bm) = Services(store);
        const string owner = "u-f3b-owner";
        const string stranger = "u-f3b-stranger";
        const string postId = "f3b-post";

        await PlantVisiblePost(store, postId, owner);
        await RunInSession(store, s => bm.ToggleAsync(owner, "post", postId, s));

        // A non-owner (a GlobalAdmin included) gets an empty list and
        // zero new audit rows (the Web layer maps to 404; the Core
        // seam just returns nothing for a non-owner — the ADR 0105
        // "operator has no read standing" precedent).
        var before = (await AllAudits(store)).Count;
        var list = await bm.ListAsync(stranger);
        Assert.Empty(list.Groups);
        var after = (await AllAudits(store)).Count;
        Assert.Equal(before, after);
    }

    // ── 8 — F4_DanglingRowDegradesNoTitleNoLink (D5, C-M17·5) ─────────

    [Fact]
    public async Task F4_DanglingRowDegradesNoTitleNoLink()
    {
        var store = await BootStoreAsync();
        var (_, _, bm) = Services(store);
        const string owner = "u-f4-owner";
        const string ghostId = "f4-ghost-post"; // does not exist

        // Plant the bookmark row directly (no toggle — the target is
        // absent, so the D3 check would refuse).
        await Plant(store, new Bookmark
        {
            Id = Guid.NewGuid().ToString(),
            OwnerId = owner,
            TargetKind = "post",
            TargetId = ghostId,
            Created = DateTimeOffset.UtcNow,
        });

        // The list resolves the dangling row → Degraded, no Title, no Link.
        var list = await bm.ListAsync(owner);
        var group = Assert.Single(list.Groups, g => g.Kind == "post");
        var row = Assert.Single(group.Items);
        Assert.True(row.Degraded);
        Assert.Null(row.Title);
        Assert.Null(row.Link);
        Assert.Equal(ghostId, row.TargetId);
    }

    // ── 9 — F4_UnbookmarkOnDanglingRowStillWorks (D5 / F4) ────────────

    [Fact]
    public async Task F4_UnbookmarkOnDanglingRowStillWorks()
    {
        var store = await BootStoreAsync();
        var (_, _, bm) = Services(store);
        const string owner = "u-f4b-owner";
        const string ghostId = "f4b-ghost-post";

        await Plant(store, new Bookmark
        {
            Id = Guid.NewGuid().ToString(),
            OwnerId = owner,
            TargetKind = "post",
            TargetId = ghostId,
            Created = DateTimeOffset.UtcNow,
        });

        // RemoveAsync does not read the target — a dangling row is removable.
        var result = await RunInSession(store, s => bm.RemoveAsync(owner, "post", ghostId, s));
        Assert.Equal(BookmarkToggleStatus.Removed, result.Status);

        var rows = await Bookmarks(store, owner);
        Assert.DoesNotContain(rows, b => b.TargetId == ghostId);
    }

    // ── 10 — F4_VisibleRowHasTitleAndLink (D5, positive) ──────────────

    [Fact]
    public async Task F4_VisibleRowHasTitleAndLink()
    {
        var store = await BootStoreAsync();
        var (_, _, bm) = Services(store);
        const string owner = "u-f4c-owner";
        const string postId = "f4c-post";

        await PlantVisiblePost(store, postId, owner);
        await RunInSession(store, s => bm.ToggleAsync(owner, "post", postId, s));

        var list = await bm.ListAsync(owner);
        var group = Assert.Single(list.Groups, g => g.Kind == "post");
        var row = Assert.Single(group.Items);
        Assert.False(row.Degraded);
        Assert.NotNull(row.Title);
        Assert.Equal("/posts/" + postId, row.Link);
    }

    // ── 11 — Bookmark_NoAudienceNoIsDeletedNoModified (§2.7) ─────────

    [Fact]
    public void Bookmark_NoAudienceNoIsDeletedNoModified()
    {
        var props = typeof(Bookmark).GetProperties()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        // The exact field set (D1/D2/D4, §2.1 — the frozen pin):
        Assert.Contains("Id", props);
        Assert.Contains("OwnerId", props);
        Assert.Contains("TargetKind", props);
        Assert.Contains("TargetId", props);
        Assert.Contains("Created", props);

        // What is FORBIDDEN (D1/D2/D4 — the anti-shape):
        Assert.DoesNotContain("Audience", props);
        Assert.DoesNotContain("IsDeleted", props);
        Assert.DoesNotContain("Modified", props);
        Assert.DoesNotContain("ComponentId", props);
        Assert.DoesNotContain("LanguageCode", props);
    }

    // ── 12 — BookmarkService_MakesNoCanSeeAsyncCallOnList (C-M17·2) ──

    [Fact]
    public async Task BookmarkService_MakesNoCanSeeAsyncCallOnList()
    {
        var store = await BootStoreAsync();
        var (_, _, bm) = Services(store);
        const string owner = "u-f4d-owner";
        const string postId = "f4d-post";

        await PlantVisiblePost(store, postId, owner);
        await RunInSession(store, s => bm.ToggleAsync(owner, "post", postId, s));

        // The list resolution uses CanAsync (single-target) for each
        // visible row, but it NEVER calls CanSeeAsync (bulk) over the
        // bookmark rows themselves — C-M17·2. The observable effect:
        // no aggregate audit row (TargetKind = "bookmark" or similar).
        var list = await bm.ListAsync(owner);
        Assert.Single(list.Groups);

        // No new audit rows — CanSeeAsync would have created at least
        // one aggregate row. The resolution uses the target's own
        // CanAsync (single-target), which IS allowed and expected.
        // Assert no row with a "bookmark"-like TargetKind was created.
        var audits = await AllAudits(store);
        Assert.DoesNotContain(audits, a => a.TargetKind == "bookmark");
    }

    // ── 13 — BookmarkService_MakesNoModerateCall (C-M17·1) ────────────

    [Fact]
    public async Task BookmarkService_MakesNoModerateCall()
    {
        var store = await BootStoreAsync();
        var (_, _, bm) = Services(store);
        const string owner = "u-f4e-owner";
        const string postId = "f4e-post";

        await PlantVisiblePost(store, postId, owner);

        // Toggle → the D3 check uses CanAsync(Read), never Moderate.
        await RunInSession(store, s => bm.ToggleAsync(owner, "post", postId, s));

        // Assert no audit row with a "moderate" action was created.
        var audits = await AllAudits(store);
        Assert.DoesNotContain(audits, a =>
            string.Equals(a.Action, "moderate", StringComparison.OrdinalIgnoreCase));
    }

    // ── 14 — TargetKind_IsTheClosedStringSet (D1) ─────────────────────

    [Fact]
    public void TargetKind_IsTheClosedStringSet()
    {
        // The ADR 0118 amendment (2026-10-16) added the project / board
        // kinds — the M37 PersonalNotes closed-set vocabulary already
        // carried them; the M17 surface now matches. The set is now
        // SEVEN kinds (was five at M17 ship, the M5 KanbanStatuses shape
        // pin still holds: string, not enum, closed, drift-guarded).
        var expected = new HashSet<string>(StringComparer.Ordinal)
            { "post", "event", "todo", "announcement", "page", "project", "board" };

        Assert.Equal(expected, new HashSet<string>(BookmarkService.ClosedTargetKinds, StringComparer.Ordinal));
        Assert.Equal(7, BookmarkService.ClosedTargetKinds.Count);
    }

    // ── ADR 0118 amendment (2026-10-16) — the project / board bookmark
    //    lanes (the M37 PersonalNotes closed-set vocabulary re-applied to
    //    the M17 surface) ───────────────────────────────────────────────
    //
    // The ADR 0118 amendment (2026-10-16) added the project / board kinds
    // to the closed set. The D3 write-lane check re-runs the owning
    // surface's own frozen IProjectService.GetProjectAsync / GetBoardAsync
    // read — the same call the /projects/{id} / /projects/boards/{id}
    // detail page uses (the M37 PersonalNotes "project" / "board" D3 shape
    // re-applied). These tests pin that the new kinds behave identically
    // to the existing five (F1 idempotency, D3 write-lane check, D4
    // physical removal, D5 list resolution).

    // ── 16 — F2_ProjectBookmark_WriteLanePositive (D3, positive) ───────

    [Fact]
    public async Task F2_ProjectBookmark_WriteLanePositive()
    {
        var store = await BootStoreAsync();
        var (_, _, bm) = Services(store);
        const string owner = "u-16-owner";
        const string projectId = "p-16-project";

        // Plant a visible project (author = owner — the author branch of
        // the frozen Decide() passes CanAsync(Read)).
        await Plant(store, new Project
        {
            Id = projectId,
            AuthorId = owner,
            Title = "16 test project",
            Created = DateTimeOffset.UtcNow,
        });

        // The D3 check passes (the author branch): a visible target is
        // bookmarkable, the row is created.
        var result = await RunInSession(store, s => bm.ToggleAsync(owner, "project", projectId, s));
        Assert.Equal(BookmarkToggleStatus.Bookmarked, result.Status);

        // The row is keyed on the (owner, "project", projectId) tuple.
        var rows = await Bookmarks(store, owner);
        Assert.Single(rows, b => b.TargetKind == "project" && b.TargetId == projectId);

        // Idempotency (F1 — the unique index witness):
        var second = await RunInSession(store, s => bm.ToggleAsync(owner, "project", projectId, s));
        Assert.Equal(BookmarkToggleStatus.AlreadyBookmarked, second.Status);
        Assert.Single(await Bookmarks(store, owner), b => b.TargetKind == "project");
    }

    // ── 17 — F2_ProjectBookmark_InvisibleRefused (D3, negative) ────────

    [Fact]
    public async Task F2_ProjectBookmark_InvisibleRefused()
    {
        var store = await BootStoreAsync();
        var (_, _, bm) = Services(store);
        const string owner = "u-17-owner";
        const string author = "u-17-author";
        const string projectId = "p-17-project";

        // A project authored by a *different* resident, with an audience that
        // grants only the author — the owner is neither the author nor in the
        // audience, has no role standing: the frozen CanAsync(Read) denies →
        // Refused (the M16 create-gate posture, the 404-not-403 Web mapping).
        // (Audience = null would be community-visible, ADR 0036 — so the
        // restricted audience is what makes the target invisible.)
        await Plant(store, new Project
        {
            Id = projectId,
            AuthorId = author,
            Title = "17 invisible project",
            Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, author),
        });

        var result = await RunInSession(store, s => bm.ToggleAsync(owner, "project", projectId, s));
        Assert.Equal(BookmarkToggleStatus.Refused, result.Status);

        // No Bookmark row survives (the D3 table: a denied / absent /
        // soft-deleted target is Refused, no row is created).
        var rows = await Bookmarks(store, owner);
        Assert.DoesNotContain(rows, b => b.TargetKind == "project" && b.TargetId == projectId);
    }

    // ── 18 — F2_BoardBookmark_WriteLanePositive (D3, positive) ─────────

    [Fact]
    public async Task F2_BoardBookmark_WriteLanePositive()
    {
        var store = await BootStoreAsync();
        var (_, _, bm) = Services(store);
        const string owner = "u-18-owner";
        const string boardId = "b-18-board";

        // Plant a visible board (author = owner — the author branch of
        // the frozen Decide() passes CanAsync(Read)).
        await Plant(store, new KanbanBoard
        {
            Id = boardId,
            AuthorId = owner,
            Title = "18 test board",
            Created = DateTimeOffset.UtcNow,
        });

        var result = await RunInSession(store, s => bm.ToggleAsync(owner, "board", boardId, s));
        Assert.Equal(BookmarkToggleStatus.Bookmarked, result.Status);

        var rows = await Bookmarks(store, owner);
        Assert.Single(rows, b => b.TargetKind == "board" && b.TargetId == boardId);

        // Idempotency (F1):
        var second = await RunInSession(store, s => bm.ToggleAsync(owner, "board", boardId, s));
        Assert.Equal(BookmarkToggleStatus.AlreadyBookmarked, second.Status);
        Assert.Single(await Bookmarks(store, owner), b => b.TargetKind == "board");
    }

    // ── 19 — F2_BoardBookmark_InvisibleRefused (D3, negative) ──────────

    [Fact]
    public async Task F2_BoardBookmark_InvisibleRefused()
    {
        var store = await BootStoreAsync();
        var (_, _, bm) = Services(store);
        const string owner = "u-19-owner";
        const string author = "u-19-author";
        const string boardId = "b-19-board";

        // A board authored by a *different* resident, with an audience that
        // grants only the author — the owner has no standing: the frozen
        // CanAsync(Read) denies → Refused. (Audience = null would be
        // community-visible, ADR 0036 — so the restricted audience is what
        // makes the target invisible.)
        await Plant(store, new KanbanBoard
        {
            Id = boardId,
            AuthorId = author,
            Title = "19 invisible board",
            Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, author),
        });

        var result = await RunInSession(store, s => bm.ToggleAsync(owner, "board", boardId, s));
        Assert.Equal(BookmarkToggleStatus.Refused, result.Status);

        var rows = await Bookmarks(store, owner);
        Assert.DoesNotContain(rows, b => b.TargetKind == "board" && b.TargetId == boardId);
    }

    // ── 20 — F4_ProjectBoardRowsResolveTitleAndLink (D5, positive) ─────

    [Fact]
    public async Task F4_ProjectBoardRowsResolveTitleAndLink()
    {
        var store = await BootStoreAsync();
        var (_, _, bm) = Services(store);
        const string owner = "u-20-owner";
        const string projectId = "p-20-project";
        const string boardId = "b-20-board";

        await Plant(store, new Project
        {
            Id = projectId,
            AuthorId = owner,
            Title = "20 test project",
            Created = DateTimeOffset.UtcNow,
        });
        await Plant(store, new KanbanBoard
        {
            Id = boardId,
            AuthorId = owner,
            Title = "20 test board",
            Created = DateTimeOffset.UtcNow,
        });

        await RunInSession(store, s => bm.ToggleAsync(owner, "project", projectId, s));
        await RunInSession(store, s => bm.ToggleAsync(owner, "board", boardId, s));

        var list = await bm.ListAsync(owner);

        // The project group (the "project" kind, the canonical order puts
        // it after "page" in the ClosedTargetKindsOrdered array).
        var projectGroup = Assert.Single(list.Groups, g => g.Kind == "project");
        var projectRow = Assert.Single(projectGroup.Items);
        Assert.False(projectRow.Degraded);
        Assert.Equal("20 test project", projectRow.Title);
        Assert.Equal($"/projects/projects/{projectId}", projectRow.Link);

        // The board group (the "board" kind, last in the canonical order).
        var boardGroup = Assert.Single(list.Groups, g => g.Kind == "board");
        var boardRow = Assert.Single(boardGroup.Items);
        Assert.False(boardRow.Degraded);
        Assert.Equal("20 test board", boardRow.Title);
        Assert.Equal($"/projects/boards/{boardId}", boardRow.Link);
    }

    // ── 21 — F4_ProjectBoardDanglingRowsDegrade (D5, negative) ─────────

    [Fact]
    public async Task F4_ProjectBoardDanglingRowsDegrade()
    {
        var store = await BootStoreAsync();
        var (_, _, bm) = Services(store);
        const string owner = "u-21-owner";
        const string ghostProjectId = "p-21-ghost-project";
        const string ghostBoardId = "b-21-ghost-board";

        // Plant the bookmark rows directly (no toggle — the targets are
        // absent, so the D3 check would refuse).
        await Plant(store, new Bookmark
        {
            Id = Guid.NewGuid().ToString(),
            OwnerId = owner,
            TargetKind = "project",
            TargetId = ghostProjectId,
            Created = DateTimeOffset.UtcNow,
        });
        await Plant(store, new Bookmark
        {
            Id = Guid.NewGuid().ToString(),
            OwnerId = owner,
            TargetKind = "board",
            TargetId = ghostBoardId,
            Created = DateTimeOffset.UtcNow,
        });

        var list = await bm.ListAsync(owner);

        // The project group — the dangling row degrades to (null, null),
        // Degraded = true (the D5 / C-M17·5 pin — exactly one static
        // bm.list.degraded label, no <a> tag, no title, no TargetId).
        var projectGroup = Assert.Single(list.Groups, g => g.Kind == "project");
        var projectRow = Assert.Single(projectGroup.Items);
        Assert.True(projectRow.Degraded);
        Assert.Null(projectRow.Title);
        Assert.Null(projectRow.Link);
        Assert.Equal(ghostProjectId, projectRow.TargetId);

        // The board group — same D5 degrade shape.
        var boardGroup = Assert.Single(list.Groups, g => g.Kind == "board");
        var boardRow = Assert.Single(boardGroup.Items);
        Assert.True(boardRow.Degraded);
        Assert.Null(boardRow.Title);
        Assert.Null(boardRow.Link);
        Assert.Equal(ghostBoardId, boardRow.TargetId);
    }

    // ── 22 — F4_SoftDeletedProjectBoardRefused (D3, soft-delete) ───────

    [Fact]
    public async Task F4_SoftDeletedProjectBoardRefused()
    {
        var store = await BootStoreAsync();
        var (_, _, bm) = Services(store);
        const string owner = "u-22-owner";
        const string projectId = "p-22-project";
        const string boardId = "b-22-board";

        // A soft-deleted project (IsDeleted = true, the ADR 0024 flag) —
        // the owning surface's frozen GetProjectAsync throws
        // KeyNotFoundException on absent / soft-deleted (the C3 split),
        // which the D3 table maps to Refused.
        await Plant(store, new Project
        {
            Id = projectId,
            AuthorId = owner,
            Title = "22 soft-deleted project",
            Created = DateTimeOffset.UtcNow,
            IsDeleted = true,
        });
        await Plant(store, new KanbanBoard
        {
            Id = boardId,
            AuthorId = owner,
            Title = "22 soft-deleted board",
            Created = DateTimeOffset.UtcNow,
            IsDeleted = true,
        });

        var projectResult = await RunInSession(store, s => bm.ToggleAsync(owner, "project", projectId, s));
        Assert.Equal(BookmarkToggleStatus.Refused, projectResult.Status);

        var boardResult = await RunInSession(store, s => bm.ToggleAsync(owner, "board", boardId, s));
        Assert.Equal(BookmarkToggleStatus.Refused, boardResult.Status);

        // No rows survive (the D3 table: absent / soft-deleted → Refused,
        // no row is created).
        var rows = await Bookmarks(store, owner);
        Assert.DoesNotContain(rows, b => b.TargetKind == "project" && b.TargetId == projectId);
        Assert.DoesNotContain(rows, b => b.TargetKind == "board" && b.TargetId == boardId);
    }

    // ── Shared helpers ──────────────────────────────────────────────────

    private async Task<IDocumentStore> BootStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            CoreDocTypes.Configure(opts);
            PostDocTypes.Configure(opts);
            ProjectDocTypes.Configure(opts);
            BookmarkDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);

        // EF identity schema (ADR 0004): the same MigrateAsync the production boot
        // does — required by the obs-4 IdentityService (IIdentityService.GetBySubjectAsync
        // reads the EF `identity` schema for the user/role rows).
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn)
            .Options);
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);

        _conn = conn;
        return store;
    }

    private (UserInfoService User, AuthorizationService Authz, BookmarkService Bm)
        Services(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);

        // obs 4 (ADR 0118 D3/F2): the real IdentityService over the migrated EF
        // identity schema (the GuardianAssignmentTests shape) — so GetBySubjectAsync
        // resolves the owner's real standing when the bookmark target is an
        // announcement. The no-op NoClaimsSource/NoMail stand in for the two
        // Web-side seams (not under test here).
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_conn!)
            .Options);
        var userStore = new UserStore<User, IdentityRole, AppDbContext, string,
            IdentityUserClaim<string>, IdentityUserRole<string>,
            IdentityUserLogin<string>, IdentityUserToken<string>,
            IdentityRoleClaim<string>>(db);
        var userManager = new UserManager<User>(
            userStore,
            Options.Create(new IdentityOptions { User = { RequireUniqueEmail = true } }),
            new PasswordHasher<User>(),
            new[] { new UserValidator<User>() },
            new[] { new PasswordValidator<User>() },
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            EmptyServiceProvider.Instance,
            NullLogger<UserManager<User>>.Instance);
        var identity = new IdentityService(
            userManager,
            store,
            userInfo,
            NoClaimsSource.Instance,
            NoMail.Instance,
            Options.Create(new VerificationOptions()),
            NullLogger<IdentityService>.Instance);

        var authz = new AuthorizationService(store, userInfo);
        var announcements = new AnnouncementService(store, userInfo);
        var pages = new PageService(store);
        var events = new EventService(store, authz, userInfo);
        var projects = new ProjectService(store, authz, userInfo);
        var bm = new BookmarkService(userInfo, authz, store, announcements, pages, events, projects, identity);
        return (userInfo, authz, bm);
    }

    /// <summary>
    /// Plant a post that is visible to its author (the owner branch of
    /// the frozen Decide() — no audience restriction needed for the
    /// author to pass CanAsync(Read)).
    /// </summary>
    private static async Task PlantVisiblePost(IDocumentStore store, string postId, string author)
    {
        await Plant(store, new Component { Id = ComponentId, Name = "Test", Enabled = true });
        await Plant(store, new Post
        {
            Id = postId,
            ComponentId = ComponentId,
            AuthorId = author,
            Title = "Bookmark test post",
            Body = "bookmark test post",
            Created = DateTimeOffset.UtcNow,
            Audience = new Audience(), // empty — author branch passes
        });
    }

    private static Audience Audience(GrantKind kind, string id)
        => new(AudienceMode.Any, [new AudienceGrant(kind, id)]);

    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }

    private static async Task<T> RunInSession<T>(IDocumentStore store, Func<IDocumentSession, Task<T>> action)
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var result = await action(session);
        await session.SaveChangesAsync();
        return result;
    }

    private static async Task<IReadOnlyList<Bookmark>> Bookmarks(IDocumentStore store, string owner)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await Marten.QueryableExtensions.ToListAsync(
            s.Query<Bookmark>()
                .Where(b => b.OwnerId == owner),
            ct);
    }

    private static async Task<IReadOnlyList<BookmarkFolder>> Folders(IDocumentStore store, string owner)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await Marten.QueryableExtensions.ToListAsync(
            s.Query<BookmarkFolder>()
                .Where(f => f.OwnerId == owner),
            ct);
    }

    private static async Task<IReadOnlyList<AccessAudit>> AllAudits(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await Marten.QueryableExtensions.ToListAsync(s.Query<AccessAudit>(), ct);
    }

    // ── Test doubles (the two Web-side seams the IdentityService takes; minimal
    //    substitutes — the mailer's durable-envelope path and the claims source
    //    are not under test here; the same shape as GuardianAssignmentTests) ──

    private sealed class NoClaimsSource : IClaimsSource
    {
        public static readonly NoClaimsSource Instance = new();
        public ClaimsPrincipal? Current => null;   // not request-driven in a Core test
    }

    private sealed class NoMail : IMailerStage
    {
        public static readonly NoMail Instance = new();
        public Task StageAsync(
            IDocumentSession session,
            string idempotencyKey,
            string recipient,
            string subject,
            string body,
            CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static readonly EmptyServiceProvider Instance = new();
        public object? GetService(Type serviceType) => null;
    }

    // ── 15 — GlobalAdmin_Beats_CommunityScope_On_Bookmark_Toggle (obs 4 / D3/F2) ──

    [Fact]
    public async Task GlobalAdmin_Beats_CommunityScope_On_Bookmark_Toggle()
    {
        var store = await BootStoreAsync();
        var (_, _, bm) = Services(store);
        const string adminId = "u-obs4-admin";
        const string communityId = "u-obs4-comm";
        const string annId = "u-obs4-ann";

        // Seed the EF side: the account, the GlobalAdmin role row, the link
        // (the same shape IdentityServiceAccountNotificationTests.SeedGlobalAdminAsync uses).
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_conn!)
            .Options);
        var ct = TestContext.Current.CancellationToken;
        if (!await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .AnyAsync(db.Roles.Where(r => r.Name == Roles.GlobalAdmin), ct))
        {
            db.Roles.Add(new IdentityRole
            {
                Id = Roles.GlobalAdmin,
                Name = Roles.GlobalAdmin,
                NormalizedName = Roles.GlobalAdmin.ToUpperInvariant()
            });
        }
        if (await db.Users.FindAsync(new object[] { adminId }, ct) is null)
        {
            db.Users.Add(new User { Id = adminId, UserName = adminId, Email = adminId + "@ex.net" });
        }
        if (!await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .AnyAsync(db.UserRoles.Where(ur => ur.UserId == adminId && ur.RoleId == Roles.GlobalAdmin), ct))
        {
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = adminId, RoleId = Roles.GlobalAdmin });
        }
        await db.SaveChangesAsync(ct);

        // The mt side: a verified profile (GetBySubjectAsync checks Profile.Verified → Member role;
        // the GlobalAdmin role comes from the EF identity role link above).
        await Plant(store, new Profile
        {
            SubjectId = adminId,
            DisplayName = adminId,
            Verified = true,
            Email = adminId + "@ex.net"
        });

        // A community-targeted announcement (Scope = Community, CommunityId set).
        // A plain non-member resident with no GlobalAdmin standing would be DENIED
        // by ResolveReadVisibilityAsync — the obs-4 bug was that BookmarkService
        // passed an empty role set, so even a GlobalAdmin was denied.
        await Plant(store, new Announcement
        {
            Id = annId,
            AuthorId = adminId,
            Title = "Community-targeted notice",
            Body = "Visible to community members and GlobalAdmins",
            Scope = AnnouncementScope.Community,
            CommunityId = communityId,
            Created = DateTimeOffset.UtcNow,
        });

        // obs 4 fix: the owner's real standing is resolved via IIdentityService.GetBySubjectAsync.
        // A GlobalAdmin (role from the EF identity link) sees a community-targeted
        // announcement (ResolveReadVisibilityAsync: admin = roles.Contains(GlobalAdmin) → true).
        var result = await RunInSession(store, s => bm.ToggleAsync(adminId, "announcement", annId, s));
        Assert.Equal(BookmarkToggleStatus.Bookmarked, result.Status);

        // Verify the row was created.
        var rows = await Bookmarks(store, adminId);
        Assert.Single(rows, b => b.TargetKind == "announcement" && b.TargetId == annId);
    }

    // ── Folder-tree tests (ADR 0168 D2 — the M37 C_M37_6_* pin re-applied to
    //    the bookmark surface) ─────────────────────────────────────────────

    /// <summary>
    /// C_M17_6_FolderTree_DepthBounded_UniqueName (ADR 0168 D2 — the M37
    /// C_M37_6_FolderTree_DepthBounded_UniqueName pin re-applied): a folder
    /// tree is a personal tree, bounded in depth (≤ 2: a new folder's parent
    /// must be a root), unique in name within the owner's tree (the
    /// (OwnerId, ParentId, Name) unique index is the witness), and a foreign
    /// parent / a non-root parent is a non-leaky 404 / depth violation.
    /// </summary>
    [Fact]
    public async Task C_M17_6_FolderTree_DepthBounded_UniqueName()
    {
        var store = await BootStoreAsync();
        var (_, _, bm) = Services(store);
        const string owner = "u-cm176-owner";

        // Root folder, then a child under it (depth ≤ 2 — both allowed).
        var root = await RunInSession(store, s => bm.CreateFolderAsync(owner, "Work", null, s));
        var child = await RunInSession(store, s => bm.CreateFolderAsync(owner, "Deep", root.Id, s));
        Assert.NotNull(root.Id);
        Assert.NotNull(child.Id);

        // A depth violation: a new folder whose parent is a non-root (a child)
        // would open the tree past the bound (the ADR 0168 "arbitrary-depth
        // folder tree" forbid) — throws InvalidOperationException.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => RunInSession(store, s => bm.CreateFolderAsync(owner, "TooDeep", child.Id, s)));

        // A foreign parent is a non-leaky 404 (KeyNotFoundException).
        const string other = "u-cm176-other";
        var otherRoot = await RunInSession(store, s => bm.CreateFolderAsync(other, "Other", null, s));
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => RunInSession(store, s => bm.CreateFolderAsync(owner, "X", otherRoot.Id, s)));

        // The (OwnerId, ParentId, Name) unique index is the witness: a
        // same-name, same-parent folder is the no-op (returns the existing
        // row — one row, not a new one).
        var dupRoot = await RunInSession(store, s => bm.CreateFolderAsync(owner, "Work", null, s));
        Assert.Equal(root.Id, dupRoot.Id);

        // A different parent with the same name is allowed (the index is
        // keyed on all three).
        var sameNameUnderChild = await RunInSession(store, s => bm.CreateFolderAsync(owner, "Work", root.Id, s));
        Assert.NotEqual(root.Id, sameNameUnderChild.Id);
    }

    /// <summary>
    /// C_M17_6_FolderDeletion_MovesBookmarksToRoot_BookmarksSurvive (ADR
    /// 0168 D2 — the M37 C_M37_6_FolderDeletion_MovesNotesToRoot_BodiesSurvive
    /// pin re-applied): a folder's deletion MOVES its bookmarks to the root
    /// (FolderId = null) — the bookmark rows are never lost (re-parented, not
    /// deleted); child folders are recursively deleted (their bookmarks move
    /// to the root too).
    /// </summary>
    [Fact]
    public async Task C_M17_6_FolderDeletion_MovesBookmarksToRoot_BookmarksSurvive()
    {
        var store = await BootStoreAsync();
        var (_, _, bm) = Services(store);
        const string owner = "u-cm176b-owner";
        const string postId = "cm176b-post";
        const string otherPostId = "cm176b-post2";

        await PlantVisiblePost(store, postId, owner);
        await PlantVisiblePost(store, otherPostId, owner);

        // Plant two root folders + one child.
        var root = await RunInSession(store, s => bm.CreateFolderAsync(owner, "Archive", null, s));
        var child = await RunInSession(store, s => bm.CreateFolderAsync(owner, "Old", root.Id, s));

        // Plant a bookmark in the root folder and one in the child.
        var rootBm = await RunInSession(store, s => bm.ToggleAsync(owner, "post", postId, s));
        Assert.Equal(BookmarkToggleStatus.Bookmarked, rootBm.Status);
        var childBm = await RunInSession(store, s => bm.ToggleAsync(owner, "post", otherPostId, s));
        Assert.Equal(BookmarkToggleStatus.Bookmarked, childBm.Status);
        await RunInSession(store, s => bm.MoveBookmarkToFolderAsync(owner, "post", postId, root.Id, s));
        await RunInSession(store, s => bm.MoveBookmarkToFolderAsync(owner, "post", otherPostId, child.Id, s));

        // Delete the root folder — it has a child, so the outcome is
        // FoldersDeleted (child recursively deleted).
        var result = await RunInSession(store, s => bm.DeleteFolderAsync(owner, root.Id, s));
        Assert.Equal(BookmarkFolderDeleteResult.FoldersDeleted, result);

        // Both folders (root + child) are physically removed.
        var folders = await Folders(store, owner);
        Assert.DoesNotContain(folders, f => f.Id == root.Id);
        Assert.DoesNotContain(folders, f => f.Id == child.Id);

        // Both bookmarks are moved to the root (FolderId = null) — the bookmark
        // rows are never lost (the D2 pin: folder deletion is a re-parent, not
        // a bookmark removal).
        var bookmarks = await Bookmarks(store, owner);
        Assert.Equal(2, bookmarks.Count);
        foreach (var b in bookmarks)
        {
            Assert.Null(b.FolderId);   // moved to the root
        }
        Assert.Equal("post", bookmarks.First(x => x.TargetId == postId).TargetKind);
        Assert.Equal("post", bookmarks.First(x => x.TargetId == otherPostId).TargetKind);
    }

    /// <summary>
    /// C_M17_6_BookmarkCannotLiveInAnotherResidentFolder (ADR 0168 D2 — the
    /// M37 C_M37_6_NoteCannotLiveInAnotherResidentFolder pin re-applied): a
    /// bookmark cannot live in another resident's folder (a foreign folder is
    /// a non-leaky 404 on the move).
    /// </summary>
    [Fact]
    public async Task C_M17_6_BookmarkCannotLiveInAnotherResidentFolder()
    {
        var store = await BootStoreAsync();
        var (_, _, bm) = Services(store);
        const string ownerA = "u-cm176c-ownerA";
        const string ownerB = "u-cm176c-ownerB";
        const string postId = "cm176c-post";

        await PlantVisiblePost(store, postId, ownerA);
        var bmRow = await RunInSession(store, s => bm.ToggleAsync(ownerA, "post", postId, s));
        Assert.Equal(BookmarkToggleStatus.Bookmarked, bmRow.Status);

        // Owner B's folder.
        var folderB = await RunInSession(store, s => bm.CreateFolderAsync(ownerB, "B's folder", null, s));
        Assert.NotNull(folderB.Id);

        // Owner A cannot move their bookmark into Owner B's folder (non-leaky 404).
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => RunInSession(store, s => bm.MoveBookmarkToFolderAsync(ownerA, "post", postId, folderB.Id, s)));

        // And a non-owner cannot move Owner A's bookmark at all (non-leaky 404).
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => RunInSession(store, s => bm.MoveBookmarkToFolderAsync(ownerB, "post", postId, null, s)));
    }

    /// <summary>
    /// F1_ListGroupsUnderFolderTree (ADR 0168 D2 — the M37 ListNotesAsync
    /// shape re-applied): the owner's list groups bookmarks by (TargetKind,
    /// FolderId), with the unfiled bucket (FolderId = null) and the owner's
    /// folder tree carried in the result (the Folders list, with the
    /// BookmarkCount per folder).
    /// </summary>
    [Fact]
    public async Task F1_ListGroupsUnderFolderTree()
    {
        var store = await BootStoreAsync();
        var (_, _, bm) = Services(store);
        const string owner = "u-cm17f1-owner";
        const string post1 = "cm17f1-post1";
        const string post2 = "cm17f1-post2";

        await PlantVisiblePost(store, post1, owner);
        await PlantVisiblePost(store, post2, owner);
        await RunInSession(store, s => bm.ToggleAsync(owner, "post", post1, s));
        await RunInSession(store, s => bm.ToggleAsync(owner, "post", post2, s));

        var folder = await RunInSession(store, s => bm.CreateFolderAsync(owner, "Read later", null, s));
        await RunInSession(store, s => bm.MoveBookmarkToFolderAsync(owner, "post", post1, folder.Id, s));
        // post2 stays unfiled (root).

        var result = await bm.ListAsync(owner);

        // The "post" kind has TWO groups: one for the folder, one unfiled.
        var postGroups = result.Groups.Where(g => g.Kind == "post").ToList();
        Assert.Equal(2, postGroups.Count);

        // The folder group carries one item (post1) + the folder id.
        var folderGroup = postGroups.First(g => g.FolderId == folder.Id);
        Assert.Single(folderGroup.Items);
        Assert.Equal(post1, folderGroup.Items[0].TargetId);
        Assert.Equal(folder.Id, folderGroup.Items[0].FolderId);

        // The unfiled group carries one item (post2), FolderId = null.
        var unfiledGroup = postGroups.First(g => g.FolderId is null);
        Assert.Single(unfiledGroup.Items);
        Assert.Equal(post2, unfiledGroup.Items[0].TargetId);
        Assert.Null(unfiledGroup.Items[0].FolderId);

        // The owner's folder tree is carried in the result, with the correct
        // BookmarkCount (1 — post1).
        var folderRow = result.Folders.First(f => f.Id == folder.Id);
        Assert.Equal("Read later", folderRow.Name);
        Assert.Equal(1, folderRow.BookmarkCount);
    }
}
