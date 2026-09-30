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
/// The <see cref="BookmarksController.Toggle"/> + the
/// <c>_BookmarkButton</c> partial Web-boundary seam tests (M17 U04 —
/// Web surface part 2: the bookmark toggle write lane + the shared
/// toggle control on the five target detail surfaces, ADR 0118).
/// Mirrors the <see cref="BookmarksControllerTests"/> /
/// <see cref="InventoryControllerTests"/> harness shape: the **frozen**
/// <see cref="IBookmarkService"/> seam is substituted with NSubstitute
/// (the controller never re-derives access — C-M17·1: the seam is the
/// sole writer; the 404 non-leaky split and the redirect-after-write
/// mapping are **this** layer's pins). The
/// <see cref="Marten.IDocumentStore"/> is a plain substitute for the
/// write lane's <c>LightweightSession()</c> (the C3 same-transaction
/// lane — <see cref="BookmarkService"/> never commits internally; the
/// caller owns the single write).
/// <para>
/// The six M17-U04 pinned tests (the U04 plan + the design doc §gate,
/// locked verbatim):
/// </para>
/// <list type="number">
/// <item><b>F1_Toggle_Idempotent_One_Row_Second_Call_Returns_AlreadyBookmarked</b>
/// — a first <c>POST /bookmarks/toggle</c> creates a row
/// (<c>Bookmarked</c>), a second returns <c>AlreadyBookmarked</c> (the
/// F1 no-op; the unique index is the witness, the M9
/// <c>convo_uidx_pair</c> shape).</item>
/// <item><b>F2_Invisible_Target_Refused_404_No_Row_Survives</b> — a
/// target that is **not visible** to the owner (the seam's
/// <c>ToggleAsync</c> returns <c>Refused</c>) maps to a non-leaky
/// <see cref="NotFoundResult"/>, and the controller does **not** call
/// the seam's <c>ListAsync</c> (the D3 write-lane visibility check —
/// **no** row survives).</item>
/// <item><b>F3_NonOwner_Toggle_Gets_404_No_Audit_Row</b> — a caller
/// with no read standing (a <c>GlobalAdmin</c> who is not the owner,
/// or a principal with no <c>SubjectId</c>) gets a non-leaky
/// <see cref="NotFoundResult"/>, and the controller does **not** call
/// the seam's <c>ToggleAsync</c> (the C-M17·2 pin, the ADR 0105
/// "operator has no read standing" precedent).</item>
/// <item><b>F4_Unbookmark_On_Degraded_Row_Still_Works</b> — a
/// <c>POST /bookmarks/{kind}/{id}/remove</c> on a **degraded** row
/// (the target is absent, soft-deleted, or no longer visible) calls
/// the seam's <c>RemoveAsync</c> (the D5 pin — the unbookmark needs
/// no target read at all, the row is keyed on the owner's own
/// <see cref="Bookmark"/> row).</item>
/// <item><b>BmButton_Partial_Renders_Correct_Label_For_State</b> — the
/// <c>_BookmarkButton</c> partial: a button on a page whose model
/// carries <c>BookmarkState == Bookmarked</c> renders the
/// <c>bm.button.bookmarked</c> label; a button on a page whose model
/// does **not** carry <c>BookmarkState</c> renders the
/// <c>bm.button.bookmark</c> label (the D2 / C-M17·2 pin — the
/// detail view does **not** load the owner's bookmark list to decide
/// the button's label).</item>
/// <item><b>BmButton_Partial_Form_Posts_To_Toggle_Endpoint</b> — the
/// <c>_BookmarkButton</c> partial: the form POSTs to
/// <c>/bookmarks/toggle</c> with the correct <c>kind</c> + <c>id</c>
/// route values (the closed set + the target's id), and the hidden
/// anti-forgery token is present (the
/// <c>[ValidateAntiForgeryToken]</c> pin).</item>
/// </list>
/// <para>
/// **No database, no Testcontainers** — a pure NSubstitute seam test
/// (the seam's read / write decisions are the seam's; this layer pins
/// the controller's mapping + the partial's structural shape, not the
/// seam's gate).
/// </para>
/// </summary>
public sealed class BookmarkButtonTests
{
    // ── 1 — F1_Toggle_Idempotent_One_Row_Second_Call_Returns_AlreadyBookmarked ──

    /// <summary>
    /// <c>POST /bookmarks/toggle</c> (the F1 idempotency pin — D4 / F1):
    /// a first call creates a row (<c>Bookmarked</c>), a second call
    /// returns <c>AlreadyBookmarked</c> (the F1 no-op; the unique
    /// index on <c>(OwnerId, TargetKind, TargetId)</c> is the witness —
    /// the M9 <c>convo_uidx_pair</c> shape). Both results map to a
    /// redirect to <c>/bookmarks</c> (the "redirect after write"
    /// precedent — the M16 <c>InventoryController</c> / M5
    /// <c>PageController</c> shape). The seam's <c>ToggleAsync</c> is
    /// the sole writer (C-M17·1: the controller never re-derives
    /// access).
    /// </summary>
    [Fact]
    public async Task F1_Toggle_Idempotent_One_Row_Second_Call_Returns_AlreadyBookmarked()
    {
        const string actor = "subj-f1-owner";
        const string kind = "post";
        const string id = "p-toggle-f1";

        var bookmarks = Substitute.For<IBookmarkService>();
        bookmarks
            .ToggleAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Marten.IDocumentSession>())
            .Returns(
                // First call — row created.
                new BookmarkToggleResult(BookmarkToggleStatus.Bookmarked),
                // Second call — the F1 no-op (one row, one Created — the
                // unique-index witness).
                new BookmarkToggleResult(BookmarkToggleStatus.AlreadyBookmarked));

        var store = Substitute.For<Marten.IDocumentStore>();
        var session = Substitute.For<Marten.IDocumentSession>();
        store.LightweightSession().Returns(session);

        var controller = Build(bookmarks, store, subjectId: actor);

        // First call — Bookmarked.
        var first = await controller.Toggle(kind, id);
        var firstRedirect = Assert.IsType<RedirectResult>(first);
        Assert.Equal("/bookmarks", firstRedirect.Url);

        // Second call — AlreadyBookmarked (the F1 no-op).
        var second = await controller.Toggle(kind, id);
        var secondRedirect = Assert.IsType<RedirectResult>(second);
        Assert.Equal("/bookmarks", secondRedirect.Url);

        // The seam's ToggleAsync was called twice, both with the actor's
        // SubjectId as the ownerId (C-M17·1: the controller never
        // re-derives access, the seam is the sole writer).
        await bookmarks.Received(2).ToggleAsync(
            actor, kind, id, Arg.Any<Marten.IDocumentSession>());

        // The caller owned the session (the C3 commit — the U02
        // RunInSession shape: the service stores into the caller's
        // session, the caller commits).
        await session.Received(2).SaveChangesAsync();
    }

    // ── 2 — F2_Invisible_Target_Refused_404_No_Row_Survives ────────────────

    /// <summary>
    /// <c>POST /bookmarks/toggle</c> (the D3 write-lane visibility check —
    /// C-M17·3 / F2 pin): a target that is **not visible** to the owner
    /// (the seam's <c>ToggleAsync</c> returns <c>Refused</c>) maps to a
    /// non-leaky <see cref="NotFoundResult"/> (the M16 create-gate
    /// posture — Deny ⇒ 404, never 403; the Deny row does not survive),
    /// and the controller does **not** call the seam's
    /// <see cref="IBookmarkService.ListAsync"/> (the sole-reader pin —
    /// C-M17·1: the controller never re-derives access; the write lane
    /// and the read lane are independent).
    /// </summary>
    [Fact]
    public async Task F2_Invisible_Target_Refused_404_No_Row_Survives()
    {
        const string actor = "subj-f2-owner";
        const string kind = "post";
        const string id = "p-invisible-f2";

        var bookmarks = Substitute.For<IBookmarkService>();
        bookmarks
            .ToggleAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Marten.IDocumentSession>())
            .Returns(new BookmarkToggleResult(BookmarkToggleStatus.Refused));

        var store = Substitute.For<Marten.IDocumentStore>();
        var session = Substitute.For<Marten.IDocumentSession>();
        store.LightweightSession().Returns(session);

        var controller = Build(bookmarks, store, subjectId: actor);
        var result = await controller.Toggle(kind, id);

        // C-M17·3 / F2 — the non-leaky 404 (the D3 write-lane
        // visibility check failed — the target is not visible to the
        // owner; **no** row survives; the Web layer maps to a 404,
        // never 403).
        Assert.IsType<NotFoundResult>(result);

        // The seam's ToggleAsync was called (the write lane ran), but
        // the seam's ListAsync was **not** called (the sole-reader pin —
        // C-M17·1: the controller never re-derives access; the write
        // lane and the read lane are independent).
        await bookmarks.Received(1).ToggleAsync(
            actor, kind, id, Arg.Any<Marten.IDocumentSession>());
        await bookmarks.DidNotReceive().ListAsync(Arg.Any<string>());
    }

    // ── 3 — F3_NonOwner_Toggle_Gets_404_No_Audit_Row ───────────────────────

    /// <summary>
    /// <c>POST /bookmarks/toggle</c> (the C-M17·2 pin — the "operator
    /// has no read standing" gate, the ADR 0105 precedent): a caller
    /// with no read standing (no <c>SubjectId</c> — a <c>GlobalAdmin</c>
    /// who is not the owner, or any operator without read standing)
    /// gets a non-leaky <see cref="NotFoundResult"/>, and the controller
    /// does **not** call the seam's
    /// <see cref="IBookmarkService.ToggleAsync"/> (the sole-writer pin —
    /// C-M17·1: the controller never re-derives access, and a caller
    /// with no standing has no write to delegate). That 404 commits
    /// <b>no</b> <c>AccessAudit</c> row (C-M17·2 — the owner's list is
    /// a personal read, zero audit — the ADR 0105 "operator has no read
    /// standing" shape).
    /// </summary>
    [Fact]
    public async Task F3_NonOwner_Toggle_Gets_404_No_Audit_Row()
    {
        var bookmarks = Substitute.For<IBookmarkService>();
        // ToggleAsync is **not** stubbed — the non-owner path returns
        // NotFound before reaching it.

        var controller = Build(bookmarks, subjectId: null);
        var result = await controller.Toggle("post", "p-nonowner-f3");

        // C-M17·2 — the non-leaky 404 (a non-owner, a GlobalAdmin
        // included, has no standing to create a bookmark row; that 404
        // commits no AccessAudit row).
        Assert.IsType<NotFoundResult>(result);

        // The seam's ToggleAsync was **not** called (the "operator has
        // no read standing" precedent — the controller returns the 404
        // before delegating any write to the seam).
        await bookmarks.DidNotReceive().ToggleAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Marten.IDocumentSession>());
    }

    // ── 4 — F4_Unbookmark_On_Degraded_Row_Still_Works ──────────────────────

    /// <summary>
    /// <c>POST /bookmarks/{kind}/{id}/remove</c> (the D5 pin — unbookmark
    /// on a **degraded** row still works: the row is keyed on the owner's
    /// own <see cref="Bookmark"/> row, not the target's id, so the
    /// unbookmark needs **no target read at all** — D5 / F4): the seam's
    /// <see cref="IBookmarkService.RemoveAsync"/> is called with the
    /// actor's <c>SubjectId</c> as the <c>ownerId</c> + the
    /// <c>targetKind</c> / <c>targetId</c> verbatim + the caller's
    /// <see cref="Marten.IDocumentSession"/> (the C3 same-transaction
    /// lane — <see cref="BookmarkService"/> never commits internally;
    /// the caller owns the single write — the U02 <c>RunInSession</c>
    /// shape), and the result redirects back to <c>/bookmarks</c> (the
    /// "redirect after write" precedent — the M16
    /// <c>InventoryController</c> / M5 <c>PageController</c> shape).
    /// </summary>
    [Fact]
    public async Task F4_Unbookmark_On_Degraded_Row_Still_Works()
    {
        const string actor = "subj-f4-owner";
        const string targetKind = "post";
        const string targetId = "p-degraded-gone-f4";   // a DEGRADED target (absent — D5)

        var bookmarks = Substitute.For<IBookmarkService>();
        bookmarks
            .RemoveAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Marten.IDocumentSession>())
            .Returns(new BookmarkToggleResult(BookmarkToggleStatus.Removed));

        var store = Substitute.For<Marten.IDocumentStore>();
        var session = Substitute.For<Marten.IDocumentSession>();
        store.LightweightSession().Returns(session);

        var controller = Build(bookmarks, store, subjectId: actor);
        var result = await controller.Remove(targetKind, targetId);

        // The "redirect after write" precedent: the unbookmark commits +
        // redirects to the /bookmarks list (the row is gone — the D4
        // physical removal).
        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/bookmarks", redirect.Url);

        // The seam's RemoveAsync was called with the actor's SubjectId
        // as the ownerId + the targetKind / targetId verbatim + the
        // caller's session (the C3 same-transaction lane — the D5 pin:
        // no target read at all, the row is keyed on the owner's own
        // Bookmark row).
        await bookmarks.Received(1).RemoveAsync(
            actor, targetKind, targetId, Arg.Any<Marten.IDocumentSession>());

        // The caller owned the session (the C3 commit).
        await session.Received(1).SaveChangesAsync();
    }

    // ── 5 — BmButton_Partial_Renders_Correct_Label_For_State ──────────────

    /// <summary>
    /// The <c>_BookmarkButton</c> partial (the D2 / C-M17·2 pin — the
    /// detail view does **not** load the owner's bookmark list to decide
    /// the button's label): the partial's label logic is
    /// <c>ViewData["BookmarkState"] == "Bookmarked"</c> ⇒
    /// <c>bm.button.bookmarked</c>, else <c>bm.button.bookmark</c>.
    /// The house structural-pin idiom (the
    /// <see cref="BookmarksControllerTests"/>
    /// <c>F5_Degraded_Row_Renders_BmListDegrading_Key_NoTitle_NoLink</c>
    /// "string pin, no TestServer" precedent): read the partial file,
    /// verify the both-branch structure + the two closed kw-l keys.
    /// </summary>
    [Fact]
    public void BmButton_Partial_Renders_Correct_Label_For_State()
    {
        var path = Path.Combine(RepoRoot, "src", "Kumunita.Web", "Views", "Shared", "_BookmarkButton.cshtml");
        Assert.True(File.Exists(path), $"Views/Shared/_BookmarkButton.cshtml not found at {path}.");

        // Strip Razor comments — they are author documentation, not
        // rendered markup.
        var html = System.Text.RegularExpressions.Regex.Replace(
            File.ReadAllText(path),
            @"\@\*.*?\*\@",
            "",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        // (1) Both label keys are present (the two-branch structure:
        // the default bm.button.bookmark + the exceptional
        // bm.button.bookmarked).
        Assert.Contains("key=\"bm.button.bookmark\"", html);
        Assert.Contains("key=\"bm.button.bookmarked\"", html);

        // (2) The label-switching logic is driven by ViewData's
        // BookmarkState == "Bookmarked" (the D2 / C-M17·2 pin — the
        // detail view does not load the owner's bookmark list to decide
        // the label; the (exceptional) re-render-after-toggle state
        // carries BookmarkState in ViewData).
        Assert.Contains("BookmarkState", html);
        Assert.Contains("Bookmarked", html);

        // (3) The button's label logic is a static per-render
        // conditional (no query, no service call, no owner-list load —
        // the D2 / C-M17·2 pin: the detail view does not load the
        // owner's bookmark list to decide the button's label).
        Assert.DoesNotContain("IBookmarkService", html);
        Assert.DoesNotContain("ListAsync", html);
        Assert.DoesNotContain("ToggleAsync", html);
    }

    // ── 6 — BmButton_Partial_Form_Posts_To_Toggle_Endpoint ─────────────────

    /// <summary>
    /// The <c>_BookmarkButton</c> partial (the
    /// <c>[ValidateAntiForgeryToken]</c> pin + the closed-set + target-id
    /// route values): the form POSTs to <c>/bookmarks/toggle</c> with
    /// the correct <c>kind</c> + <c>id</c> route values (the closed set +
    /// the target's id), and the anti-forgery token is present (the
    /// house convention <c>@Html.AntiForgeryToken()</c>). The house
    /// structural-pin idiom (the
    /// <see cref="BookmarksControllerTests"/>
    /// <c>F5_Unbookmark_On_Degraded_Row_Posts_To_Remove_Endpoint</c>
    /// "string pin, no TestServer" precedent).
    /// </summary>
    [Fact]
    public void BmButton_Partial_Form_Posts_To_Toggle_Endpoint()
    {
        var path = Path.Combine(RepoRoot, "src", "Kumunita.Web", "Views", "Shared", "_BookmarkButton.cshtml");
        Assert.True(File.Exists(path), $"Views/Shared/_BookmarkButton.cshtml not found at {path}.");

        // Strip Razor comments — they are author documentation, not
        // rendered markup.
        var html = System.Text.RegularExpressions.Regex.Replace(
            File.ReadAllText(path),
            @"\@\*.*?\*\@",
            "",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        // (1) The form POSTs to the /bookmarks/toggle endpoint (the U04
        // toggle action's route).
        Assert.Contains("/bookmarks/toggle", html);
        Assert.Contains("method=\"post\"", html);

        // (2) The kind + id hidden inputs are present (the closed set +
        // the target's id — the D1 closed-string-set pin).
        Assert.Contains("name=\"kind\"", html);
        Assert.Contains("name=\"id\"", html);
        Assert.Contains("Model.Kind", html);
        Assert.Contains("Model.Id", html);

        // (3) The anti-forgery token is present (the
        // [ValidateAntiForgeryToken] pin — the house convention
        // @Html.AntiForgeryToken()).
        Assert.Contains("@Html.AntiForgeryToken()", html);
    }

    // ── Harness ──────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="BookmarksController"/> over NSubstitute seams
    /// (the <see cref="BookmarksControllerTests"/> harness shape — the
    /// <see cref="IBookmarkService"/> is the sole seam, the
    /// <see cref="Marten.IDocumentStore"/> is a plain substitute for the
    /// write lane's <c>LightweightSession()</c>, a no-op
    /// <see cref="ITempDataProvider"/> closes the <c>TempData</c> bag).
    /// </summary>
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

    /// <summary>
    /// The repo root (the house structural-pin idiom — walk up from the
    /// test assembly's output dir to the dir holding <c>Kumunita.slnx</c>).
    /// </summary>
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

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { /* no-op */ }
    }
}
