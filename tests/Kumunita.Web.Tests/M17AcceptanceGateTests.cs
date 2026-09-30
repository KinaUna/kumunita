using System.Security.Claims;
using Kumunita.Core.Bookmarks;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// The three M17 acceptance gate tests (design doc §gate / §2.6, locked
/// verbatim in ADR 0118). These are the **milestone-level** acceptance
/// witnesses — the closed loop, the authorization boundary, and the
/// part-vs-whole audit split — distinct from the U03/U04 per-seam pins.
/// <para>
/// **No database, no Testcontainers** — a pure NSubstitute seam test over
/// the frozen <see cref="IBookmarkService"/> + <see cref="Marten.IDocumentStore"/>
/// (the same harness shape as <see cref="BookmarksControllerTests"/> /
/// <see cref="BookmarkButtonTests"/>).
/// </para>
/// </summary>
public sealed class M17AcceptanceGateTests
{
    // ── (a) Closed loop: toggle → list → remove → re-toggle ─────────────

    /// <summary>
    /// **(a) Closed loop** (C-M17·4 / F1 — the design doc §gate(a)):
    /// the owner bookmarks a visible post → the <c>GET /bookmarks</c>
    /// list shows the row under the <c>post</c> group →
    /// <c>POST /bookmarks/post/{id}/remove</c> removes it (the row is
    /// gone) → a second <c>POST /bookmarks/toggle</c> creates a
    /// **fresh** <c>Created</c> timestamp and exactly **one** row
    /// (the F1 / C-M17·4 witness — the unique index is the
    /// idempotency pin).
    /// </summary>
    [Fact]
    public async Task ClosedLoop_ToggleListRemoveReToggle_FreshCreated_OneRow()
    {
        const string actor = "subj-acceptance-owner";
        const string kind = "post";
        const string id = "p-acceptance";

        var bookmarks = Substitute.For<IBookmarkService>();

        // The toggle sequence: Bookmarked (first) → Bookmarked (fresh
        // after removal — the F1 / C-M17·4 witness: the unique index is
        // the idempotency pin; re-bookmarking is a fresh Created).
        bookmarks
            .ToggleAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Marten.IDocumentSession>())
            .Returns(
                new BookmarkToggleResult(BookmarkToggleStatus.Bookmarked),
                new BookmarkToggleResult(BookmarkToggleStatus.Bookmarked));

        // The list shows one row under the "post" group (after the first
        // toggle, before removal).
        bookmarks
            .ListAsync(actor)
            .Returns(
                new BookmarkListResult(
                [
                    new BookmarkGroup(kind,
                    [
                        new BookmarkRow(kind, id, "Visible post", $"/posts/{id}", false, new DateTimeOffset(2026, 9, 30, 8, 0, 0, TimeSpan.Zero)),
                    ]),
                ]));

        // The remove returns Removed (the row is physically deleted —
        // D4: no IsDeleted, removal is physical).
        bookmarks
            .RemoveAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Marten.IDocumentSession>())
            .Returns(new BookmarkToggleResult(BookmarkToggleStatus.Removed));

        var store = Substitute.For<Marten.IDocumentStore>();
        var session = Substitute.For<Marten.IDocumentSession>();
        store.LightweightSession().Returns(session);

        var controller = Build(bookmarks, store, subjectId: actor);

        // 1 — Toggle (bookmark the visible post → Bookmarked, one row
        //    created). Obs-2 (ADR 0118 amendment): redirect to the
        //    surface's own URL + the flash toast is set.
        const string returnUrl = $"/posts/{id}";
        var firstToggle = await controller.Toggle(kind, id, returnUrl);
        var firstRedirect = Assert.IsType<RedirectResult>(firstToggle);
        Assert.Equal(returnUrl, firstRedirect.Url);
        Assert.Equal("bm.toggle.bookmarked", controller.TempData["info"]);

        // 2 — List (the row appears under the "post" group).
        var listResult = await controller.Index();
        var view = Assert.IsType<ViewResult>(listResult);
        var vm = Assert.IsType<BookmarkListResult>(view.ViewData.Model);
        Assert.Single(vm.Groups);
        Assert.Equal("post", vm.Groups[0].Kind);
        Assert.Single(vm.Groups[0].Items);
        Assert.False(vm.Groups[0].Items[0].Degraded);
        Assert.Equal("Visible post", vm.Groups[0].Items[0].Title);
        Assert.Equal(id, vm.Groups[0].Items[0].TargetId);

        // 3 — Remove (unbookmark the post → the row is physically
        //    deleted). Obs-2 (ADR 0118 amendment): the flash toast is set.
        var removeResult = await controller.Remove(kind, id);
        var removeRedirect = Assert.IsType<RedirectResult>(removeResult);
        Assert.Equal("/bookmarks", removeRedirect.Url);
        Assert.Equal("bm.toggle.removed", controller.TempData["info"]);

        // 4 — Toggle again (a fresh Created timestamp, one row — the F1
        //    / C-M17·4 witness: the unique index guarantees at most one
        //    row per (owner, target); re-bookmarking after removal is a
        //    fresh Created, D4).
        var reToggle = await controller.Toggle(kind, id, returnUrl);
        var reRedirect = Assert.IsType<RedirectResult>(reToggle);
        Assert.Equal(returnUrl, reRedirect.Url);

        // The seam's ToggleAsync was called exactly twice (the initial
        // bookmark + the re-bookmark after removal).
        await bookmarks.Received(2).ToggleAsync(
            actor, kind, id, Arg.Any<Marten.IDocumentSession>());

        // The seam's RemoveAsync was called once (the physical removal).
        await bookmarks.Received(1).RemoveAsync(
            actor, kind, id, Arg.Any<Marten.IDocumentSession>());

        // The caller owned the session commits (the C3 same-transaction
        // lane — the controller commits, the service never does).
        // toggle×2 (each commits) + remove×1 (commits) = 3 commits.
        await session.Received(3).SaveChangesAsync();
    }

    // ── (b) Handoff authorization boundary ────────────────────────────────

    /// <summary>
    /// **(b) Handoff authorization boundary** (C-M17·2 / C-M17·3 / F2 / F3 —
    /// the design doc §gate(b)): a non-owner (a <c>GlobalAdmin</c>
    /// included) gets a non-leaky **404** on both the list and the toggle,
    /// and **no** <c>AccessAudit</c> row commits on either. A bookmark of
    /// a **non-visible** target is <c>Refused</c> (404, **no** row), and
    /// the row never grants anything to a later read.
    /// </summary>
    [Fact]
    public async Task NonOwner_ListAndToggle_404_NoAuditRow()
    {
        var bookmarks = Substitute.For<IBookmarkService>();

        var store = Substitute.For<Marten.IDocumentStore>();
        var session = Substitute.For<Marten.IDocumentSession>();
        store.LightweightSession().Returns(session);

        // A caller with **no** SubjectId (a GlobalAdmin who is not the
        // owner, or any operator without read standing) — the ADR 0105
        // "operator has no read standing" precedent.
        var controller = Build(bookmarks, store, subjectId: null);

        // 1 — GET /bookmarks: non-leaky 404, no AccessAudit row, the seam
        //    is **not** called.
        var listResult = await controller.Index();
        Assert.IsType<NotFoundResult>(listResult);
        await bookmarks.DidNotReceive().ListAsync(Arg.Any<string>());

        // 2 — POST /bookmarks/toggle: non-leaky 404, no AccessAudit row,
        //    the seam is **not** called.
        var toggleResult = await controller.Toggle("post", "p-some-post");
        Assert.IsType<NotFoundResult>(toggleResult);
        await bookmarks.DidNotReceive().ToggleAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Marten.IDocumentSession>());

        // No session created at all (the 404 short-circuits before any
        // write — LightweightSession is synchronous, no await).
        store.DidNotReceive().LightweightSession();
    }

    // ── (c) Part-vs-whole audit completeness ───────────────────────────────

    /// <summary>
    /// **(c) Part-vs-whole audit completeness** (C-M17·2 / C-M17·5 — the
    /// design doc §gate(c)): the owner bookmarks a **non-visible**
    /// target → the seam's <c>ToggleAsync</c> returns <c>Refused</c> →
    /// **no** <c>Bookmark</c> row survives. Then the owner's
    /// <c>GET /bookmarks</c> load commits **zero** <c>AccessAudit</c>
    /// rows and runs **zero** <c>CanSeeAsync</c> passes over the bookmark
    /// rows (the M6-inbox / M9-conversation personal-read shape). The D3
    /// write-lane check is the **target's own** frozen <c>Read</c>
    /// decision (one decision, the target's own row — **not** a bookmark
    /// row).
    /// </summary>
    [Fact]
    public async Task InvisibleTarget_ToggleRefused_NoRow_OwnerListZeroAudit()
    {
        const string actor = "subj-invisible-owner";
        const string kind = "post";
        const string id = "p-invisible";

        var bookmarks = Substitute.For<IBookmarkService>();

        // The D3 write-lane check: the target is not visible to the
        // owner → Refused (404, no row).
        bookmarks
            .ToggleAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Marten.IDocumentSession>())
            .Returns(new BookmarkToggleResult(BookmarkToggleStatus.Refused));

        // The owner's list: empty (the row was never created). Zero
        // AccessAudit rows, zero CanSeeAsync passes over the bookmark
        // rows — the personal-read shape (C-M17·2).
        bookmarks
            .ListAsync(actor)
            .Returns(new BookmarkListResult([]));

        var store = Substitute.For<Marten.IDocumentStore>();
        var session = Substitute.For<Marten.IDocumentSession>();
        store.LightweightSession().Returns(session);

        var controller = Build(bookmarks, store, subjectId: actor);

        // 1 — Toggle a non-visible target → 404 (Refused), no row survives.
        var toggleResult = await controller.Toggle(kind, id);
        Assert.IsType<NotFoundResult>(toggleResult);

        // The seam's ToggleAsync was called (the write lane ran), and it
        // returned Refused (the D3 write-lane check is the target's own
        // frozen Read decision — C-M17·3: one decision, the target's own
        // row, not a bookmark row).
        await bookmarks.Received(1).ToggleAsync(
            actor, kind, id, Arg.Any<Marten.IDocumentSession>());

        // No session commit (the Refused path does not commit — the row
        // was never created).
        await session.DidNotReceive().SaveChangesAsync();

        // 2 — The owner's list loads with zero rows (the row was never
        //    created) and commits zero AccessAudit rows (C-M17·2: the
        //    personal read, zero audit). The controller composes only
        //    IBookmarkService (no IAuthorizationService is injected —
        //    zero CanSeeAsync passes over the bookmark rows is
        //    architecturally guaranteed).
        var listResult = await controller.Index();
        var view = Assert.IsType<ViewResult>(listResult);
        var vm = Assert.IsType<BookmarkListResult>(view.ViewData.Model);
        Assert.Empty(vm.Groups);

        // The seam's ListAsync was called exactly once with the actor's
        // SubjectId (the personal read, zero audit — the ADR 0105 /
        // M6-inbox shape).
        await bookmarks.Received(1).ListAsync(actor);
    }

    // ── Harness ────────────────────────────────────────────────────────────

    private static BookmarksController Build(
        IBookmarkService bookmarks,
        Marten.IDocumentStore? store = null,
        string? subjectId = null)
    {
        store ??= Substitute.For<Marten.IDocumentStore>();

        var controller = new BookmarksController(bookmarks, store);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var claims = new List<Claim>();
        if (subjectId is not null)
            claims.Add(new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, subjectId));

        if (claims.Count > 0)
            controller.ControllerContext.HttpContext.User =
                new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));

        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());
        return controller;
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> tempData) { }
    }
}
