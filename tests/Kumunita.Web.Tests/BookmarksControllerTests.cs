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
/// The <see cref="BookmarksController"/> Web-boundary seam tests (M17 U03 —
/// Web surface part 1: the <c>/bookmarks</c> list + the per-row unbookmark
/// control, ADR 0118). Mirrors the <see cref="InventoryControllerTests"/> /
/// <see cref="TagControllerTests"/> harness shape: the **frozen**
/// <see cref="IBookmarkService"/> seam is substituted with NSubstitute (the
/// controller never re-derives access — C-M17·1: the seam is the sole reader
/// / writer; the 404 non-leaky split and the redirect-after-write mapping
/// are **this** layer's pins). The <see cref="Marten.IDocumentStore"/> is a
/// plain substitute for the write lane's <c>LightweightSession()</c> (the C3
/// same-transaction lane — <see cref="BookmarkService"/> never commits
/// internally; the caller owns the single write).
/// <para>
/// The five M17-U03 pinned tests (the design doc §gate + the U03 plan,
/// locked verbatim):
/// </para>
/// <list type="number">
/// <item><b>F3_OwnerList_Loads_Their_Rows_And_NoAuditRow_Commits</b> —
/// <c>GET /bookmarks</c>: the seam's <c>ListAsync(ownerId)</c> is called
/// exactly once with the actor's <c>SubjectId</c>, the view receives the
/// <see cref="BookmarkListResult"/> verbatim, and the controller does not
/// call <c>IAuthorizationService.CanSeeAsync</c> over the bookmark rows
/// (the C-M17·2 pin — the owner's list is a personal read, zero audit).</item>
/// <item><b>F3_NonOwner_Gets_404_And_NoAuditRow_Commits</b> —
/// <c>GET /bookmarks</c>: a caller with no read standing gets a non-leaky
/// <see cref="NotFoundResult"/>, and the controller does not call the seam's
/// <c>ListAsync</c> (the "operator has no read standing" precedent — the
/// ADR 0105 pin).</item>
/// <item><b>F5_Degraded_Row_Renders_BmListDegrading_Key_NoTitle_NoLink</b> —
/// the <c>GET /bookmarks</c> list view: a <see cref="BookmarkRow"/> with
/// <c>Degraded: true</c> renders as a <c>&lt;span&gt;</c> carrying the
/// <c>bm.list.degraded</c> key, <b>no</b> <c>&lt;a&gt;</c> tag, <b>no</b>
/// title text, <b>no</b> <c>TargetId</c> text (the D5 / C-M17·5 pin — the
/// row's entire leak surface is exactly that one static string).</item>
/// <item><b>F5_Unbookmark_On_Degraded_Row_Posts_To_Remove_Endpoint</b> —
/// the per-row unbookmark control: a <c>POST</c> to
/// <c>/bookmarks/{targetKind}/{targetId}/remove</c> on a **degraded** row
/// calls the seam's <c>RemoveAsync</c> (the D5 pin — the unbookmark needs no
/// target read at all, the row is keyed on the owner's own
/// <see cref="Bookmark"/> row).</item>
/// <item><b>BmKeys_AreTheClosedFourteenKeySet</b> — the §kw-l pin: the
/// <see cref="Kumunita.Core.Localization.KnownTranslationKeys.EnValues"/>
/// registry contains **exactly** the 14 <c>bm.*</c> keys (the closed set,
/// the parity-test shape the U04 / U05 units depend on; the two
/// <c>bm.toggle.*</c> flash keys were added by the obs-2 ADR 0118
/// amendment, 2026-09-30).</item>
/// </list>
/// <para>
/// **No database, no Testcontainers** — a pure NSubstitute seam test (the
/// seam's read / write decisions are the seam's; this layer pins the
/// controller's mapping + the view / registry structural shape, not the
/// seam's gate).
/// </para>
/// </summary>
public sealed class BookmarksControllerTests
{
    /// <summary>
    /// The closed 14-key <c>bm.*</c> set (design doc §kw-l, the exact names
    /// the parity tests pin — the U04 / U05 units consume this set, never
    /// re-register a subset; the two <c>bm.toggle.*</c> flash keys are the
    /// obs-2 ADR 0118 amendment, 2026-09-30).
    /// </summary>
    private static readonly string[] BmKeys =
    [
        "bm.nav",
        "bm.list.title",
        "bm.list.empty",
        "bm.list.degraded",
        "bm.list.kind.post",
        "bm.list.kind.event",
        "bm.list.kind.todo",
        "bm.list.kind.announcement",
        "bm.list.kind.page",
        "bm.list.unbookmark",
        "bm.button.bookmark",
        "bm.button.bookmarked",
        "bm.toggle.bookmarked",
        "bm.toggle.removed",
    ];

    // ── 1 — F3_OwnerList_Loads_Their_Rows_And_NoAuditRow_Commits ─────────

    /// <summary>
    /// <c>GET /bookmarks</c> (the owner's list, the D2 personal read —
    /// C-M17·2: zero <c>AccessAudit</c> rows, zero <c>CanSeeAsync</c>
    /// passes, the ADR 0105 / M6-inbox "operator has no read standing"
    /// precedent): the seam's <see cref="IBookmarkService.ListAsync"/> is
    /// called **exactly once** with the actor's <c>SubjectId</c> (the
    /// <c>ownerId</c>), and the view receives the
    /// <see cref="BookmarkListResult"/> **verbatim** (the grouped rows, the
    /// D5 degraded flag — the controller never re-derives access, C-M17·1).
    /// The controller composes **only** the <see cref="IBookmarkService"/>
    /// seam (no <c>IAuthorizationService</c> is injected — C-M17·1 / C-M17·2:
    /// the owner's list is a personal read, and no
    /// <c>CanSeeAsync</c> pass over the bookmark rows is possible).
    /// </summary>
    [Fact]
    public async Task F3_OwnerList_Loads_Their_Rows_And_NoAuditRow_Commits()
    {
        const string actor = "subj-bm-owner";

        var bookmarks = Substitute.For<IBookmarkService>();
        var result = new BookmarkListResult(
        [
            new BookmarkGroup("post",
            [
                new BookmarkRow("post", "p-visible", "A visible post", "/posts/p-visible", false, new DateTimeOffset(2026, 9, 20, 9, 0, 0, TimeSpan.Zero)),
                new BookmarkRow("post", "p-gone", null, null, true, new DateTimeOffset(2026, 9, 19, 9, 0, 0, TimeSpan.Zero)),
            ]),
            new BookmarkGroup("event",
            [
                new BookmarkRow("event", "e-1", "A visible event", "/events/e-1", false, new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.Zero)),
            ]),
        ]);
        bookmarks.ListAsync(Arg.Any<string>()).Returns(result);

        var controller = Build(bookmarks, subjectId: actor);
        var iresult = await controller.Index();

        var view = Assert.IsType<ViewResult>(iresult);
        var vm = Assert.IsType<BookmarkListResult>(view.ViewData.Model);

        // The view receives the BookmarkListResult verbatim (the grouped
        // rows, the D5 degraded flag — the controller never reshapes the
        // result, C-M17·1: the seam is the sole reader).
        Assert.Same(result, vm);
        Assert.Equal(2, vm.Groups.Count);
        Assert.Equal("post", vm.Groups[0].Kind);
        Assert.Equal(2, vm.Groups[0].Items.Count);
        Assert.False(vm.Groups[0].Items[0].Degraded);
        Assert.True(vm.Groups[0].Items[1].Degraded);

        // The seam's ListAsync was called exactly once, with the actor's
        // SubjectId as the ownerId (the personal read — the actor's own
        // rows, C-M17·2: zero audit, zero CanSeeAsync — the controller
        // composes only IBookmarkService, no IAuthorizationService).
        await bookmarks.Received(1).ListAsync(actor);
    }

    // ── 2 — F3_NonOwner_Gets_404_And_NoAuditRow_Commits ──────────────────

    /// <summary>
    /// <c>GET /bookmarks</c> (the "operator has no read standing" gate —
    /// C-M17·2, the ADR 0105 precedent): a caller with no read standing
    /// (no <c>SubjectId</c>) gets a non-leaky <see cref="NotFoundResult"/>,
    /// and the controller does **not** call the seam's
    /// <see cref="IBookmarkService.ListAsync"/> (the sole-reader pin —
    /// C-M17·1: the controller never re-derives access, and a caller with no
    /// read standing has no read to delegate). That 404 commits
    /// <b>no</b> <c>AccessAudit</c> row (C-M17·2: the owner's list is a
    /// personal read, zero audit — the ADR 0105 "operator has no read
    /// standing" shape).
    /// </summary>
    [Fact]
    public async Task F3_NonOwner_Gets_404_And_NoAuditRow_Commits()
    {
        var bookmarks = Substitute.For<IBookmarkService>();
        // ListAsync is **not** stubbed — the non-owner path returns
        // NotFound before reaching it. NSubstitute's auto-substitution of
        // the un-stubbed method is inert (no call is logged for a method
        // the controller never invokes), and the DidNotReceive assertion
        // below is the pin.
        var controller = Build(bookmarks, subjectId: null);
        var result = await controller.Index();

        // C-M17·2 — the non-leaky 404 (a non-owner, a GlobalAdmin included,
        // has no read standing over any bookmark list; that 404 commits no
        // AccessAudit row).
        Assert.IsType<NotFoundResult>(result);

        // The seam's ListAsync was **not** called (the "operator has no
        // read standing" precedent — the controller returns the 404 before
        // delegating any read to the seam).
        await bookmarks.DidNotReceive().ListAsync(Arg.Any<string>());
    }

    // ── 3 — F5_Degraded_Row_Renders_BmListDegrading_Key_NoTitle_NoLink ───

    /// <summary>
    /// The <c>GET /bookmarks</c> list view (the D5 / C-M17·5 pin — a
    /// bookmark whose target is absent, soft-deleted, or no longer visible
    /// renders the **generic** <c>bm.list.degraded</c> label — **no**
    /// title, **no** link, **no** id): a <see cref="BookmarkRow"/> with
    /// <c>Degraded: true</c> renders as a <c>&lt;span&gt;</c> carrying the
    /// <c>bm.list.degraded</c> key, with **no** <c>&lt;a&gt;</c> tag,
    /// **no** title text, **no** <c>TargetId</c> text in that branch (the
    /// row's entire leak surface is exactly that one static string). The
    /// per-row unbookmark form (the D5 pin — the unbookmark control works
    /// on a degraded row) is present in the view and posts to
    /// <c>/bookmarks/{targetKind}/{targetId}/remove</c> with the
    /// <c>bm.list.unbookmark</c> button label. The house structural-pin
    /// idiom (the <see cref="InventoryControllerTests"/>
    /// <c>Nav_Entry_Present</c> "string pin, no TestServer" precedent).
    /// </summary>
    [Fact]
    public void F5_Degraded_Row_Renders_BmListDegrading_Key_NoTitle_NoLink()
    {
        var path = Path.Combine(RepoRoot, "src", "Kumunita.Web", "Views", "Bookmarks", "Index.cshtml");
        Assert.True(File.Exists(path), $"Views/Bookmarks/Index.cshtml not found at {path}.");

        // Strip Razor comments — they are author documentation, not rendered
        // markup: the house structural-pin idiom asserts on the shape the
        // browser sees, and the degraded row's "no <a> / no title / no
        // TargetId" pin is a statement about the *rendered* branch, not the
        // inline doc-comment describing that pin.
        var html = System.Text.RegularExpressions.Regex.Replace(
            File.ReadAllText(path),
            @"\@\*.*?\*\@",
            "",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        // (1) The closed degraded label key is present (the D5 / C-M17·5
        // pin — the degraded row renders exactly this one static string).
        Assert.Contains("key=\"bm.list.degraded\"", html);

        // (2) The degraded branch — the <h3> heading that wraps the
        // bm.list.degraded key — is a closed <span> carrying the
        // bm.list.degraded kw-l key, and that branch carries NO <a> tag
        // (no link), NO title binding, NO link binding (the <a> /
        // row.Title / row.Link bindings live in the *else* branch, outside
        // this heading — the D5 / C-M17·5 pin: the row's entire leak
        // surface is exactly that one static string).
        int degKeyIdx = html.IndexOf("key=\"bm.list.degraded\"", StringComparison.Ordinal);
        Assert.True(degKeyIdx >= 0, "bm.list.degraded key not found in the view.");
        int h3OpenIdx = html.LastIndexOf("<h3", degKeyIdx, StringComparison.Ordinal);
        int h3CloseIdx = html.IndexOf("</h3>", degKeyIdx, StringComparison.Ordinal);
        Assert.True(h3OpenIdx >= 0 && h3CloseIdx > degKeyIdx,
            "The degraded label is not wrapped in an <h3> heading.");
        string degBranch = html.Substring(h3OpenIdx, h3CloseIdx - h3OpenIdx + "</h3>".Length);
        Assert.Contains("<span", degBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("<a ", degBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("row.Title", degBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("row.Link", degBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("TargetId", degBranch, StringComparison.Ordinal);

        // (3) The per-row unbookmark form is present (the D5 pin — the
        // unbookmark control works on both degraded and non-degraded rows),
        // posts to the /bookmarks/{kind}/{id}/remove endpoint (the dynamic
        // {row.TargetKind}/{row.TargetId} shape), and carries the
        // bm.list.unbookmark button label.
        Assert.Contains("/bookmarks/", html);
        Assert.Contains("/remove\"", html);
        Assert.Contains("method=\"post\"", html);
        Assert.Contains("key=\"bm.list.unbookmark\"", html);
        Assert.Contains("row.TargetKind", html);
        Assert.Contains("row.TargetId", html);
        Assert.Contains("@Html.AntiForgeryToken()", html);
    }

    // ── 4 — F5_Unbookmark_On_Degraded_Row_Posts_To_Remove_Endpoint ────────

    /// <summary>
    /// <c>POST /bookmarks/{targetKind}/{targetId}/remove</c> (the D5 pin —
    /// unbookmark on a **degraded** row still works: the row is keyed on the
    /// owner's own <see cref="Bookmark"/> row, not the target's id, so the
    /// unbookmark needs **no target read at all** — D5 / F4): the seam's
    /// <see cref="IBookmarkService.RemoveAsync"/> is called with the actor's
    /// <c>SubjectId</c> as the <c>ownerId</c> + the
    /// <c>targetKind</c> / <c>targetId</c> verbatim + the caller's
    /// <see cref="Marten.IDocumentSession"/> (the C3 same-transaction lane —
    /// <see cref="BookmarkService"/> never commits internally; the caller
    /// owns the single write — the U02 <c>RunInSession</c> shape), and the
    /// result redirects back to <c>/bookmarks</c> (the "redirect after
    /// write" precedent — the M16 <c>InventoryController</c> / M5
    /// <c>PageController</c> shape).
    /// </summary>
    [Fact]
    public async Task F5_Unbookmark_On_Degraded_Row_Posts_To_Remove_Endpoint()
    {
        const string actor = "subj-bm-remove-owner";
        const string targetKind = "post";
        const string targetId = "p-degraded-gone";   // a DEGRADED target (absent — D5)

        var bookmarks = Substitute.For<IBookmarkService>();
        bookmarks.RemoveAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Marten.IDocumentSession>())
            .Returns(new BookmarkToggleResult(BookmarkToggleStatus.Removed));

        var store = Substitute.For<Marten.IDocumentStore>();
        var session = Substitute.For<Marten.IDocumentSession>();
        store.LightweightSession().Returns(session);

        var controller = Build(bookmarks, store, subjectId: actor);
        var result = await controller.Remove(targetKind, targetId);

        // The "redirect after write" precedent (the M16 InventoryController
        // / M5 PageController shape): the unbookmark commits + redirects to
        // the /bookmarks list (the row is gone — the D4 physical removal).
        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/bookmarks", redirect.Url);

        // Obs-2 (ADR 0118 amendment) — the flash toast is set on the
        // localized bm.toggle.removed key (the raw key here — the test
        // floor: the translation seam is absent in the harness, so the
        // controller's T() helper falls back to the key itself).
        Assert.Equal("bm.toggle.removed", controller.TempData["info"]);

        // The seam's RemoveAsync was called with the actor's SubjectId as
        // the ownerId + the targetKind / targetId verbatim + the caller's
        // session (the C3 same-transaction lane — the D5 pin: no target
        // read at all, the row is keyed on the owner's own Bookmark row).
        await bookmarks.Received(1).RemoveAsync(
            actor, targetKind, targetId, Arg.Any<Marten.IDocumentSession>());

        // The caller owned the session (the C3 commit — the U02
        // RunInSession shape: the service stores/deletes into the
        // caller's session, the caller commits).
        await session.Received(1).SaveChangesAsync();
    }

    // ── 5 — BmKeys_AreTheClosedTwelveKeySet ───────────────────────────────

    /// <summary>
    /// The §kw-l pin (the design doc §kw-l, the closed 12-key set): the
    /// <see cref="Kumunita.Core.Localization.KnownTranslationKeys.EnValues"/>
    /// registry contains **exactly** the 12 <c>bm.*</c> keys (no more, no
    /// less — the parity-test shape the U04 / U05 units depend on; the U04
    /// <c>_BookmarkButton</c> partial + the U05 gate tests consume
    /// <c>bm.button.bookmark</c> / <c>bm.button.bookmarked</c>, so the
    /// closed set is registered in U03, never a per-unit subset). All four
    /// registries (en / de / fr / da) carry the same closed set.
    /// </summary>
    [Fact]
    public void BmKeys_AreTheClosedFourteenKeySet()
    {
        var en = Kumunita.Core.Localization.KnownTranslationKeys.EnValues;

        // (1) All twelve of the closed set are registered (each key
        // present + a non-empty value — the parity-test shape).
        foreach (var key in BmKeys)
        {
            Assert.True(en.ContainsKey(key), $"Expected the closed set to contain '{key}'.");
            Assert.False(string.IsNullOrWhiteSpace(en[key]), $"The '{key}' value must be non-empty.");
        }

        // (2) The closed set is EXACTLY the fourteen keys — no more (a
        // fifteenth bm.* key is a drift event the U04 / U05 units depend
        // on), no less (missing one breaks the parity tests).
        var actualBmKeys = en.Keys
            .Where(k => k.StartsWith("bm.", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(BmKeys.ToHashSet(StringComparer.Ordinal), actualBmKeys);
        Assert.Equal(14, actualBmKeys.Count);

        // (3) All four registries (en / de / fr / da) carry the same closed
        // set (the §kw-l parity pin — the U04 / U05 units consume the full
        // set in every language).
        foreach (var key in BmKeys)
        {
            Assert.True(Kumunita.Core.Localization.KnownTranslationKeys.DeValues.ContainsKey(key), $"de registry missing '{key}'.");
            Assert.True(Kumunita.Core.Localization.KnownTranslationKeys.FrValues.ContainsKey(key), $"fr registry missing '{key}'.");
            Assert.True(Kumunita.Core.Localization.KnownTranslationKeys.DaValues.ContainsKey(key), $"da registry missing '{key}'.");
        }
    }

    // ── Harness ──────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="BookmarksController"/> over NSubstitute seams.
    /// The <see cref="IBookmarkService"/> is the sole seam (C-M17·1 / C-M17·2
    /// — the controller composes **only** the bookmark service; no
    /// <c>IAuthorizationService</c>, no <c>IUserInfoService</c> — the
    /// owner's list is a personal read, zero audit). The
    /// <see cref="Marten.IDocumentStore"/> is a plain substitute for the
    /// write lane's <c>LightweightSession()</c> (the C3 same-transaction
    /// lane — <see cref="BookmarkService"/> never commits internally; the
    /// caller owns the single write). A no-op
    /// <see cref="ITempDataProvider"/> closes the <c>TempData</c> bag so the
    /// write lane's success branch doesn't NRE
    /// (<c>DefaultHttpContext</c> leaves <c>Session</c> null by default).
    /// </summary>
    private static BookmarksController Build(
        IBookmarkService bookmarks,
        Marten.IDocumentStore? store = null,
        string? subjectId = null,
        string[]? roles = null)
    {
        store ??= Substitute.For<Marten.IDocumentStore>();

        var controller = new BookmarksController(bookmarks, store);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var claims = new List<Claim>();
        if (subjectId is not null)
            claims.Add(new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, subjectId));
        if (roles is { Length: > 0 })
            claims.AddRange(roles.Select(r => new Claim(Kumunita.Core.Identity.ClaimTypes.Role, r)));

        if (claims.Count > 0)
            controller.ControllerContext.HttpContext.User =
                new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));

        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());
        return controller;
    }

    /// <summary>
    /// The repo root (the house structural-pin idiom — the
    /// <see cref="InventoryControllerTests"/> / <see cref="NavMoreFoldTests"/>
    /// shape: walk up from the test assembly's output dir to the dir
    /// holding <c>Kumunita.slnx</c>).
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
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect / the call log, not the bag
        }
    }
}
