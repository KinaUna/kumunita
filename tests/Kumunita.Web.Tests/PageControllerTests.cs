using System.Security.Claims;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// Unit tests for <see cref="PageController"/> — the <c>PG</c> lane's
/// Web surface (ADR 0039 §3.8). The pins this harness owns (the
/// <see cref="IPageService"/> seams + the <see cref="Kumunita.Core.Authorization
/// .IAuthorizationService"/> decision the Web layer runs):
/// <list type="number">
/// <item><b>404 on absent / 403 on denied are DISTINCT</b> (ADR 0039 §3.8,
///       the page-specific split, MUST NOT be collapsed): <see
///       cref="PageController.Show"/> on a path the service does not know is
///       a <see cref="NotFound"/> (the <see cref="KeyNotFoundException"/>
///       from <see cref="IPageService.GetByPathAsync"/>); the same action on
///       a path the service <em>does</em> know but the caller is denied to
///       (a <see cref="Decision"/> with <c>Allowed == false</c>) is a
///       <see cref="ForbidResult"/>. The two are different HTTP semantics
///       (the page does not exist vs. the page exists and is hidden from
///       this actor) — collapsing them (posts / announcements'
///       both-404 shape) would mislead both the user and the audit trail
///       (a 403 implies a standing decision ran; a 404 implies none did).</item>
/// <item><b>Draft gate (author-only)</b> (ADR 0037): a non-author caller
///       hitting a draft page gets a <see cref="ForbidResult"/> — even if
///       the <see cref="IAuthorizationService"/> <c>Read</c> decision would
///       allow them (a draft is its author's, until published; the draft
///       gate is the Web-layer pin, the <see cref="IAuthorizationService"/>
///       decision is the audience pin). An author sees their own draft (the
///       badge + publish affordance).</item>
/// <item><b>Tree browse filter (C6 aggregate)</b>: <see
///       cref="PageController.Index"/> calls
///       <see cref="IAuthorizationService.CanSeeAsync"/> ONCE over the whole
///       candidate set (the C6 aggregate row, not one per page), and a page
///       hidden from the <c>Visible</c> set is <b>absent</b> from the
///       rendered <see cref="PageTreeViewModel"/> — not blanked.</item>
/// <item><b>Mount-point resolver</b> (ADR 0039 §3.8, display not access):
///       <see cref="PageMountResolver.ResolveAsync"/> on a slot with a
///       mounted page returns that page's <c>/pages/…</c> href + its
///       title; on an unmounted slot (the service returns <c>null</c>) it
///       returns <c>null</c> (the layout omits the link); on a slot whose
///       mounted page has since been soft-deleted (not in the live tree)
///       it returns <c>null</c> (no valid path to offer). The resolver runs
///       NO access decision — the <c>Read</c> decision is the page's own
///       <see cref="PageController.Show"/>.</item>
/// <item><b>Composer audience round-trip (ADR 0036 single source)</b>:
///       <see cref="PageController.New"/> (POST) builds the
///       <see cref="Kumunita.Core.Authorization.Audience"/> via
///       <see cref="AudienceEditorModel.BuildAudience"/> and passes it
///       verbatim to <see cref="IPageService.CreateAsync"/>; when
///       <see cref="PageComposeViewModel.IsPublic"/> is <c>true</c>, the
///       page is written with <c>Audience = null</c> + <c>ComponentId =
///       null</c> (the public capability); when <c>false</c> (the default),
///       the editor's <c>BuildAudience()</c> + the <see
///       cref="PageComposeViewModel.CommunityId"/> are the stored values.
///       <see cref="PageController.Edit"/> (POST) round-trips the same way
///       through <see cref="IPageService.UpdateAsync"/>.</item>
/// <item><b>403 on a standing re-check (C3)</b>: <see
///       cref="PageController.Delete"/> / <see cref="PageController.Move"/>
///       on a page the service's <c>actorRoles</c> gate rejects (a
///       <see cref="UnauthorizedAccessException"/> from
///       <see cref="IPageService.DeleteAsync"/> /
///       <see cref="IPageService.MoveAsync"/>) surface as a
///       <see cref="ForbidResult"/> — the service is the real gate (C3),
///       the Web <c>[Authorize(Roles=…)]</c> is a pre-gate.</item>
/// </list>
/// <para>
/// The harness mirrors <see cref="AnnouncementControllerTests"/>: a
/// NSubstitute <see cref="IPageService"/> +
/// <see cref="IAuthorizationService"/> + <see cref="ILocalizationService"/>
/// + <see cref="IUserInfoService"/> + <see cref="IDocumentStore"/> (the
/// write lanes' <c>LightweightSession()</c>; the <c>Edit</c> GET lane's
/// <c>QuerySession()</c>) — no live Postgres, no host. A
/// <see cref="KumunitaPrincipal"/>-shaped principal is built with the
/// <see cref="Kumunita.Core.Identity.ClaimTypes.Subject"/> +
/// <see cref="Kumunita.Core.Identity.ClaimTypes.Role"/> claims (the
/// repo's role-claim convention).
/// </para>
/// </summary>
public class PageControllerTests
{
    // ── 404 vs 403 split (the page-specific pin) ─────────────────────────

    /// <summary>
    /// <see cref="PageController.Show"/> on a path the service does not
    /// know is a <see cref="NotFound"/> — the <see
    /// cref="KeyNotFoundException"/> from
    /// <see cref="IPageService.GetByPathAsync"/> surfaces as a clean 404,
    /// NOT a 403 (the page-specific split, ADR 0039 §3.8). This is the
    /// pin that the Web layer does not collapse absent/denied (the posts /
    /// announcements both-404 shape is NOT the page's shape).
    /// </summary>
    [Fact]
    public async Task Show_When_PathAbsent_ReturnsNotFound_NotForbid()
    {
        var pages = Substitute.For<IPageService>();
        pages.GetByPathAsync("about").Returns(Task.FromException<Page>(
            new KeyNotFoundException("no page at 'about'")));
        var controller = Build(pages, IsAuthenticated: true, subjectId: "subj-resident-001", roles: new[] { Roles.Member });

        var result = await controller.Show("about");

        Assert.IsType<NotFoundResult>(result);
        await pages.DidNotReceive().GetByMountPointAsync(Arg.Any<string>());
    }

    /// <summary>
    /// <see cref="PageController.Show"/> on a path the service <em>does</em>
    /// know but the caller is denied to (a <see cref="Decision"/> with
    /// <c>Allowed == false</c>) is a <see cref="ForbidResult"/> — a
    /// distinct HTTP semantic from the absent case (a standing decision ran
    /// here; none did in the 404 case). The pin: the Web layer does not
    /// collapse denied→404 (the posts / announcements both-404 shape is NOT
    /// the page's shape, ADR 0039 §3.8).
    /// </summary>
    [Fact]
    public async Task Show_When_PageDenied_ReturnsForbid_NotNotFound()
    {
        const string pageId = "page-denied-001";
        var page = new Page
        {
            Id = pageId,
            Slug = "denied",
            Title = "A denied page",
            Body = "you shall not read",
            AuthorId = "someone-else",
            Audience = new Kumunita.Core.Authorization.Audience { Mode = AudienceMode.Any, Grants = new List<AudienceGrant> { new AudienceGrant(GrantKind.User, "some-grant") } },
            IsDraft = false,
        };
        var pages = Substitute.For<IPageService>();
        pages.GetByPathAsync("denied").Returns(page);

        var authz = Substitute.For<IAuthorizationService>();
        authz.CanAsync("subj-resident-001", Arg.Any<AccessAction>(), Arg.Any<IAuditableResource>())
            .Returns(new Decision(Allowed: false, Via: AccessVia.Audience, EffectivePrincipalId: "subj-resident-001"));

        var controller = Build(pages, authz, IsAuthenticated: true, subjectId: "subj-resident-001", roles: new[] { Roles.Member });

        var result = await controller.Show("denied");

        Assert.IsType<ForbidResult>(result);
    }

    // ── PG U05 (ADR 0039 §3.9) — reference-from-UGC: a link in an authorized
    //    post does NOT imply the target page is authorized ─────────────────
    //
    // The link is free (MarkdownRenderer + IsSafeUrl render [About](/pages/about)
    // as an <a>), but opening /pages/about is a separate
    // IAuthorizationService.CanAsync(Read) decision — this test pins that the
    // "link present" half and the "target allowed/denied" half are decoupled.

    /// <summary>
    /// A <c>[About](/pages/about)</c> link in an **authorized** post's body
    /// renders as a live <c>&lt;a href="/pages/about"&gt;</c> (the frozen
    /// <see cref="Kumunita.Web.Security.MarkdownRenderer"/> +
    /// <see cref="IsSafeUrl"/> allow relative paths) — the link's PRESENCE is
    /// free and does not depend on the target's <c>Audience</c>. This is the
    /// "reference-from-UGC" half of PG U05: confirm the existing rendering,
    /// don't re-implement it.
    /// </summary>
    [Fact]
    public async Task PG5_ReferenceFromUgc_LinkInAuthorizedPost_RendersFree()
    {
        // The frozen renderer: a relative target is a safe URL → a live link.
        // This is exactly what an authorized post body containing the link
        // produces (the posts view renders via this same single renderer).
        var html = MarkdownRenderer.RenderHtml("See [About](/pages/about) for more.");

        Assert.Contains("<a href=\"/pages/about\">About</a>", html);
        // The surrounding text is preserved (the link is inline, not a
        // whole-block rewrite) — the post body is otherwise untouched.
        Assert.Contains("See ", html);
        Assert.Contains("for more.", html);
    }

    /// <summary>
    /// The **target** page being denied is a SEPARATE <c>Read</c> decision from
    /// the link's presence: a caller who can read an authorized post containing
    /// a link to a page they are denied opens <c>/pages/about</c> and gets a
    /// <see cref="ForbidResult"/> (403) — NOT the link silently 404-ing, and
    /// NOT the link's presence leaking the target. The pin: "I can see a link
    /// to it" ≠ "I am allowed to open it" (ADR 0039 §3.8's 404-vs-403 split,
    /// applied to the from-UGC reference path).
    /// </summary>
    [Fact]
    public async Task PG5_ReferenceFromUgc_LinkPresentButTargetDenied_Forbid_NotAllowed()
    {
        var page = new Page
        {
            Id = "page-target-001",
            Slug = "about",
            Title = "About (denied)",
            Body = "member-only about page",
            AuthorId = "someone-else",
            Audience = new Kumunita.Core.Authorization.Audience
            {
                Mode = AudienceMode.Any,
                Grants = new List<AudienceGrant> { new AudienceGrant(GrantKind.User, "some-grant") }
            },
            IsDraft = false,
        };
        var pages = Substitute.For<IPageService>();
        pages.GetByPathAsync("about").Returns(page);

        // The target's Read decision denies THIS actor (the separate decision
        // the link's presence never makes).
        var authz = Substitute.For<IAuthorizationService>();
        authz.CanAsync("subj-resident-001", Arg.Any<AccessAction>(), Arg.Any<IAuditableResource>())
            .Returns(new Decision(Allowed: false, Via: AccessVia.Audience, EffectivePrincipalId: "subj-resident-001"));

        var controller = Build(pages, authz, IsAuthenticated: true, subjectId: "subj-resident-001", roles: new[] { Roles.Member });

        var result = await controller.Show("about");

        // Denied → 403 (the page exists and a decision ran; distinct from the
        // absent 404, and from "the link rendered" which is a different surface).
        Assert.IsType<ForbidResult>(result);
    }

    /// <summary>
    /// <see cref="PageController.Show"/> on a <em>draft</em> page the caller
    /// did NOT author is a <see cref="ForbidResult"/> — the draft gate is
    /// the Web-layer pin (a draft is its author's, until published; ADR
    /// 0037), and it runs BEFORE the audience <see
    /// cref="IAuthorizationService.CanAsync"/> decision (a non-author
    /// hitting a draft never reaches the audience decision — the draft is
    /// simply not theirs). The pin: a non-author's 403 on a draft is NOT
    /// because the audience said no, it's because the draft is the author's.
    /// </summary>
    [Fact]
    public async Task Show_When_DraftAndNotAuthor_ReturnsForbid()
    {
        var page = new Page
        {
            Id = "page-draft-001",
            Slug = "my-draft",
            Title = "A draft page",
            Body = "work in progress",
            AuthorId = "the-author",
            Audience = null,    // even public, a non-author cannot see a draft
            IsDraft = true,
        };
        var pages = Substitute.For<IPageService>();
        pages.GetByPathAsync("my-draft").Returns(page);
        var authz = Substitute.For<IAuthorizationService>();

        // A non-author (subject ≠ AuthorId) hits the draft gate → 403, and
        // the CanAsync decision is never even consulted (the draft gate
        // short-circuits first).
        var controller = Build(pages, authz, IsAuthenticated: true, subjectId: "subj-resident-001", roles: new[] { Roles.Member });

        var result = await controller.Show("my-draft");

        Assert.IsType<ForbidResult>(result);
        await authz.DidNotReceive().CanAsync(Arg.Any<string>(), Arg.Any<AccessAction>(), Arg.Any<IAuditableResource>());
    }

    // ── Tree browse (C6 aggregate filter) ─────────────────────────────────

    /// <summary>
    /// <see cref="PageController.Index"/> calls
    /// <see cref="IAuthorizationService.CanSeeAsync"/> ONCE over the whole
    /// candidate set (the C6 aggregate, not one per page), and a page whose
    /// id is not in the <c>Visible</c> set is <b>absent</b> from the
    /// rendered <see cref="PageTreeViewModel"/> — not blanked. The pin: the
    /// tree browse's filter is the aggregate row, and a denied page does not
    /// render as an empty node.
    /// </summary>
    [Fact]
    public async Task Index_When_CanSeeAsync_HidesPage_DeniedPageIsAbsent()
    {
        const string visibleId = "page-visible-001";
        const string hiddenId  = "page-hidden-001";
        var visible = new Page { Id = visibleId, Slug = "visible", Title = "Visible page", AuthorId = "someone", IsDraft = false, IsDeleted = false };
        var hidden  = new Page { Id = hiddenId,  Slug = "hidden",  Title = "Hidden page",  AuthorId = "someone", IsDraft = false, IsDeleted = false };
        var pages = Substitute.For<IPageService>();
        pages.GetTreeAsync().Returns(new List<Page> { visible, hidden });

        var authz = Substitute.For<IAuthorizationService>();
        // Only the visible page is in the Visible set.
        authz.CanSeeAsync("subj-resident-001", Arg.Any<AccessAction>(), Arg.Any<IEnumerable<IAuditableResource>>())
            .Returns(new VisibleSet(
                Visible: new List<(string Id, AccessVia Via)> { (visibleId, AccessVia.Audience) },
                HiddenCount: 1));

        var controller = Build(pages, authz, IsAuthenticated: true, subjectId: "subj-resident-001", roles: new[] { Roles.Member });

        var view = (await controller.Index()) as ViewResult;
        Assert.NotNull(view);

        var model = view!.ViewData.Model as PageTreeViewModel;
        Assert.NotNull(model);
        // Exactly one node (the visible one) — the hidden one is absent, not blanked.
        Assert.Single(model!.Roots);
        Assert.Equal(visibleId, model.Roots[0].Id);
        Assert.Equal("Visible page", model.Roots[0].Title);
        Assert.Equal("/pages/visible", model.Roots[0].Path);

        // And the aggregate was called ONCE over the whole set (the C6
        // pin — not one per page).
        await authz.Received(1).CanSeeAsync(Arg.Is<string>(s => s == "subj-resident-001"), Arg.Any<AccessAction>(), Arg.Any<IEnumerable<IAuditableResource>>());
    }

    /// <summary>
    /// A non-author's <see cref="PageController.Index"/> never sees a
    /// <see cref="Page.IsDraft"/> page — the draft gate (author-only, ADR
    /// 0037) ran BEFORE the <see
    /// cref="IAuthorizationService.CanSeeAsync"/> decision (a draft is its
    /// author's; a non-author's candidate set does not include their own
    /// author's drafts). The pin: a non-author never sees a draft in the
    /// tree browse, even if the audience decision would have allowed it.
    /// </summary>
    [Fact]
    public async Task Index_When_NonAuthor_DraftPagesAreAbsentFromTree()
    {
        const string draftId   = "page-draft-001";
        const string liveId    = "page-live-001";
        var draft = new Page { Id = draftId, Slug = "draft", Title = "A draft",  AuthorId = "the-author", IsDraft = true,  IsDeleted = false };
        var live  = new Page { Id = liveId,  Slug = "live",  Title = "A live",   AuthorId = "the-author", IsDraft = false, IsDeleted = false };
        var pages = Substitute.For<IPageService>();
        pages.GetTreeAsync().Returns(new List<Page> { draft, live });

        var authz = Substitute.For<IAuthorizationService>();
        // The CanSeeAsync decision runs over the post-draft-gate candidate
        // set (live only) and allows it.
        authz.CanSeeAsync("subj-resident-001", Arg.Any<AccessAction>(), Arg.Any<IEnumerable<IAuditableResource>>())
            .Returns(new VisibleSet(
                Visible: new List<(string Id, AccessVia Via)> { (liveId, AccessVia.Audience) },
                HiddenCount: 0));

        // A non-author (subject ≠ "the-author") hits the index.
        var controller = Build(pages, authz, IsAuthenticated: true, subjectId: "subj-resident-001", roles: new[] { Roles.Member });

        var view = (await controller.Index()) as ViewResult;
        Assert.NotNull(view);

        var model = view!.ViewData.Model as PageTreeViewModel;
        Assert.NotNull(model);
        Assert.Single(model!.Roots);
        Assert.Equal(liveId, model.Roots[0].Id);
        Assert.False(model.Roots[0].IsDraft);
    }

    // ── Mount-point resolver (display, not access) ────────────────────────

    /// <summary>
    /// <see cref="PageMountResolver.ResolveAsync"/> on a slot with a
    /// mounted page returns that page's <c>/pages/…</c> href + its title.
    /// The pin: the resolver is the layout's "about slot → about page"
    /// seam (ADR 0039 §3.8), and it is display-only (no access decision).
    /// </summary>
    [Fact]
    public async Task MountResolver_When_Mounted_ReturnsHrefAndTitle()
    {
        const string aboutId = "page-about-001";
        var about = new Page { Id = aboutId, Slug = "about", Title = "About the community", MountPoint = "footer/community", IsDeleted = false };
        var pages = Substitute.For<IPageService>();
        pages.GetByMountPointAsync("footer/community").Returns(about);
        pages.GetTreeAsync().Returns(new List<Page> { about });

        var resolved = await PageMountResolver.ResolveAsync(pages, "footer/community");

        Assert.NotNull(resolved);
        Assert.Equal("/pages/about", resolved!.Value.Href);
        Assert.Equal("About the community", resolved.Value.Title);
    }

    /// <summary>
    /// <see cref="PageMountResolver.ResolveAsync"/> on an unmounted slot
    /// (the service returns <c>null</c>) returns <c>null</c> — the layout
    /// omits the link. The pin: an unmounted slot is NOT an error (the
    /// <see cref="IPageService.GetByMountPointAsync"/> contract is
    /// <c>null</c> for unmounted, not a <see cref="KeyNotFoundException"/>),
    /// and the resolver does not invent a link.
    /// </summary>
    [Fact]
    public async Task MountResolver_When_Unmounted_ReturnsNull()
    {
        var pages = Substitute.For<IPageService>();
        pages.GetByMountPointAsync("footer/community").Returns((Page?)null);

        var resolved = await PageMountResolver.ResolveAsync(pages, "footer/community");

        Assert.Null(resolved);
    }

    /// <summary>
    /// <see cref="PageMountResolver.ResolveAsync"/> on a slot whose mounted
    /// page has since been soft-deleted (not in the live tree) returns
    /// <c>null</c> — there is no valid <c>/pages/…</c> href to offer (the
    /// path is the ancestor-slug chain; a page not in the tree has no
    /// resolvable chain). The pin: a stale mount (a page deleted after it
    /// was mounted) does not produce a dangling link.
    /// </summary>
    [Fact]
    public async Task MountResolver_When_PageSoftDeleted_ReturnsNull()
    {
        var about = new Page { Id = "page-about-001", Slug = "about", Title = "About", MountPoint = "footer/community", IsDeleted = true };
        var pages = Substitute.For<IPageService>();
        pages.GetByMountPointAsync("footer/community").Returns(about);
        // The tree (the live, non-deleted set) does NOT contain the about
        // page (it's soft-deleted) — the resolver cannot derive a path for
        // it.
        pages.GetTreeAsync().Returns(new List<Page>());

        var resolved = await PageMountResolver.ResolveAsync(pages, "footer/community");

        Assert.Null(resolved);
    }

    // ── Composer audience round-trip (ADR 0036 single source) ─────────────

    /// <summary>
    /// <see cref="PageController.New"/> (POST, <see
    /// cref="PageComposeViewModel.IsPublic"/> = <c>true</c>) writes the page
    /// with <c>Audience = null</c> + <c>ComponentId = null</c> (the "pages
    /// default public" shape — the one place pages differ from posts, ADR
    /// 0039 §3.7). The pin: a public page is NOT written with a non-null
    /// audience (the editor's <see cref="AudienceEditorModel.BuildAudience"/>
    /// output is inert when <see cref="PageComposeViewModel.IsPublic"/> is
    /// <c>true</c>).
    /// </summary>
    [Fact]
    public async Task New_Post_IsPublic_WritesNullAudienceAndNullComponent()
    {
        var pages = Substitute.For<IPageService>();
        pages.CreateAsync(Arg.Any<Page>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(call => { var p = call.ArgAt<Page>(0); p.Id = "page-new-001"; p.AuthorId = call.ArgAt<string>(1); return Task.FromResult(p); });
        var controller = Build(pages, IsAuthenticated: true, subjectId: "subj-admin-001", roles: new[] { Roles.GlobalAdmin });

        var model = new PageComposeViewModel
        {
            Title = "About",
            Body = "The community's about page.",
            IsPublic = true,
            // A non-null editor (it would be inert — IsPublic=true) — the
            // pin is that the editor's output is NOT what gets stored.
            Audience = new AudienceEditorModel { Mode = "All", Grants = "[\"some-grant\"]", CommunityVisible = false },
            CommunityId = "community-001",
            LanguageCode = "en",
        };

        var result = await controller.New(model);

        Assert.IsType<RedirectToActionResult>(result);
        // The CreateAsync call's Page argument must have Audience = null and
        // ComponentId = null (the IsPublic=true shape).
        await pages.Received(1).CreateAsync(
            Arg.Is<Page>(p => p.Audience == null && p.ComponentId == null && p.Slug == "about" && p.Title == "About"),
            Arg.Is<string>(s => s == "subj-admin-001"),
            Arg.Any<IReadOnlySet<string>>(),
            Arg.Any<IDocumentSession>());
    }

    /// <summary>
    /// <see cref="PageController.New"/> (GET) seeds the composer's default as
    /// **non-public, community-visible** — consistent with posts (ADR 0039
    /// §3.4, amended 2026-09-17; originally "pages default public, the one
    /// place pages differ from posts"). The pin: a fresh composer is NOT
    /// public by default — <see cref="PageComposeViewModel.IsPublic"/> is
    /// <c>false</c>, the audience editor is <c>Community</c>-visible with
    /// empty grants, and the first reachable community is seeded as the
    /// scope (the community branch, <c>Decide()</c> branch 4, requires a
    /// non-null <see cref="Kumunita.Core.Pages.Page.ComponentId"/> or
    /// residents would be denied — Invariant C1).
    /// </summary>
    [Fact]
    public async Task New_Get_DefaultsToNonPublicCommunityVisible()
    {
        var pages = Substitute.For<IPageService>();
        pages.GetTreeAsync().Returns(new List<Page>());
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetComponentsAsync(true).Returns(new List<Component>
        {
            new Component { Id = "comp-default", Name = "Safety", Enabled = true },
        });
        userInfo.GetProfilesAsync(true).Returns(new List<Profile>());
        userInfo.GetPublicGroupsAsync().Returns(new List<Group>());
        var store = Substitute.For<IDocumentStore>();
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        var controller = new PageController(
            pages, Substitute.For<IAuthorizationService>(), DefaultLocalization(), userInfo, store);
        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());

        var result = Assert.IsType<ViewResult>(await controller.New());
        var model = Assert.IsType<PageComposeViewModel>(result.ViewData.Model);

        // Non-public + community-visible, with a community scope seeded so
        // residents aren't denied by the otherwise-empty audience.
        Assert.False(model.IsPublic);
        Assert.True(model.Audience.CommunityVisible);
        Assert.Equal("[]", model.Audience.Grants);
        Assert.Equal("comp-default", model.CommunityId);
    }

    /// <summary>
    /// <see cref="PageController.New"/> (POST, <see
    /// cref="PageComposeViewModel.IsPublic"/> = <c>false</c>) writes the
    /// page with the editor's <see cref="AudienceEditorModel.BuildAudience"/>
    /// output + the <see cref="PageComposeViewModel.CommunityId"/> (the
    /// non-public shape — the ADR 0036 single-source pin: the editor is the
    /// only deserialization site, the <c>BuildAudience()</c> output is the
    /// stored <see cref="Kumunita.Core.Authorization.Audience"/>).
    /// </summary>
    [Fact]
    public async Task New_Post_IsNotPublic_WritesEditorAudienceAndCommunity()
    {
        var pages = Substitute.For<IPageService>();
        pages.CreateAsync(Arg.Any<Page>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(call => { var p = call.ArgAt<Page>(0); p.Id = "page-new-002"; p.AuthorId = call.ArgAt<string>(1); return Task.FromResult(p); });
        var controller = Build(pages, IsAuthenticated: true, subjectId: "subj-admin-001", roles: new[] { Roles.GlobalAdmin });

        var model = new PageComposeViewModel
        {
            Title = "A restricted page",
            Body = "Restricted content.",
            IsPublic = false,
            Audience = new AudienceEditorModel { Mode = "Any", Grants = "[]", CommunityVisible = true },
            CommunityId = "community-002",
            LanguageCode = "en",
        };

        var result = await controller.New(model);

        Assert.IsType<RedirectToActionResult>(result);
        // Assert value-level equivalence (the controller constructs its own Audience
        // instance via BuildAudience, so ReferenceEquals would never hold).
        await pages.Received(1).CreateAsync(
            Arg.Is<Page>(p => p.Audience != null
                             && p.Audience.Mode == AudienceMode.Any
                             && p.Audience.Community
                             && p.Audience.Grants.Count == 0
                             && p.ComponentId == "community-002"
                             && p.Slug == "a-restricted-page" && p.Title == "A restricted page"),
            Arg.Is<string>(s => s == "subj-admin-001"),
            Arg.Any<IReadOnlySet<string>>(),
            Arg.Any<IDocumentSession>());
    }

    /// <summary>
    /// <see cref="PageController.Edit"/> (POST) round-trips the stored
    /// page's <see cref="Kumunita.Core.Authorization.Audience"/> through
    /// <see cref="AudienceEditorModel.FromAudience"/> (the GET lane) and
    /// back through <see cref="AudienceEditorModel.BuildAudience"/> (the
    /// POST lane) — the ADR 0036 single-source pin (the editor is the ONLY
    /// deserialization site). The pin: an edit that does not change the
    /// audience round-trips to an equivalent stored shape (a
    /// <see cref="Kumunita.Core.Authorization.Audience"/> with the same
    /// <c>Mode</c> / <c>Grants</c> / <c>Community</c> as the editor
    /// re-emits).
    /// </summary>
    [Fact]
    public async Task Edit_Post_RoundTripsAudienceThroughFromAudienceAndBuildAudience()
    {
        const string pageId = "page-edit-001";
        var existing = new Page
        {
            Id = pageId,
            Slug = "existing",
            Title = "Existing title",
            Body = "Existing body.",
            Audience = new Kumunita.Core.Authorization.Audience { Mode = AudienceMode.Any, Grants = new List<AudienceGrant> { new AudienceGrant(GrantKind.User, "grant-1") }, Community = true },
            ComponentId = "community-003",
            LanguageCode = "en",
            AuthorId = "subj-admin-001",
            IsDraft = false,
            IsDeleted = false,
        };

        var store = Substitute.For<IDocumentStore>();
        // The controller's Edit GET reads via store.QuerySession().LoadAsync<Page>(id)
        // and the Edit POST reads via store.LightweightSession().LoadAsync<Page>(id)
        // (Marten's LoadAsync has an optional CancellationToken — the same single
        // method; Arg.Any<CancellationToken>() matches both the no-CT controller
        // call and satisfies xUnit1051). Stub both sessions.
        var readSession = Substitute.For<IQuerySession>();
        readSession.LoadAsync<Page>(pageId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Page?>(existing));
        store.QuerySession().Returns(readSession);

        var writeSession = Substitute.For<IDocumentSession>();
        writeSession.LoadAsync<Page>(pageId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Page?>(existing));
        store.LightweightSession().Returns(writeSession);

        var pages = Substitute.For<IPageService>();
        pages.UpdateAsync(Arg.Any<Page>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(call => { var p = call.ArgAt<Page>(0); p.Id = pageId; p.AuthorId = call.ArgAt<string>(1); return Task.FromResult(p); });
        pages.GetTreeAsync().Returns(new List<Page> { existing });

        var controller = Build(pages, store: store, IsAuthenticated: true, subjectId: "subj-admin-001", roles: new[] { Roles.GlobalAdmin });

        // The GET lane's model is the round-trip start: FromAudience(existing.Audience).
        var get = (await controller.Edit(pageId)) as ViewResult;
        Assert.NotNull(get);
        var getModel = get!.ViewData.Model as PageComposeViewModel;
        Assert.NotNull(getModel);
        Assert.False(getModel!.IsPublic);    // existing.Audience is non-null
        Assert.Equal("Any", getModel.Audience.Mode);
        Assert.True(getModel.Audience.CommunityVisible);

        // A no-op edit (the model is re-posted unchanged) round-trips to a
        // BuildAudience() output equal in Mode/Community (the Grants are the
        // JSON array the editor's hidden textarea posts — the same shape the
        // FromAudience call produced, re-emitted on POST).
        var post = (await controller.Edit(pageId, getModel)) as RedirectToActionResult;
        Assert.NotNull(post);

        await pages.Received(1).UpdateAsync(
            Arg.Is<Page>(p => p.Audience != null
                             && p.Audience.Mode == AudienceMode.Any
                             && p.Audience.Community == true
                             && p.ComponentId == "community-003"
                             && p.Slug == "existing"
                             && p.ParentId == existing.ParentId
                             && p.IsDraft == existing.IsDraft),
            Arg.Is<string>(s => s == "subj-admin-001"),
            Arg.Any<IReadOnlySet<string>>(),
            Arg.Any<IDocumentSession>());
    }

    /// <summary>
    /// <see cref="PageController.New"/> (POST) sets the <see
    /// cref="Page.ImageIds"/> + <see cref="Page.AttachmentIds"/> from the
    /// body (via <see cref="ContentImageIds.ExtractContentImageIds"/> +
    /// <see cref="AttachmentIds.ExtractAttachmentIds"/>) <b>before</b>
    /// calling <see cref="IPageService.CreateAsync"/> — the U03 invariant
    /// (the Web layer owns the extraction, Core normalizes <c>?? []</c>
    /// and never parses the body).
    /// </summary>
    [Fact]
    public async Task New_Post_SetsImageIdsAndAttachmentIdsFromBody_BeforeCallingCreate()
    {
        var pages = Substitute.For<IPageService>();
        pages.CreateAsync(Arg.Any<Page>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(call => { var p = call.ArgAt<Page>(0); p.Id = "page-new-003"; p.AuthorId = call.ArgAt<string>(1); return Task.FromResult(p); });
        var controller = Build(pages, IsAuthenticated: true, subjectId: "subj-admin-001", roles: new[] { Roles.GlobalAdmin });

        var model = new PageComposeViewModel
        {
            Title = "A page with media",
            Body = "![alt](/content-image/a1b2c3d4)\nSome body.\n[Download](/attachment/e5f6a7b8)",
            IsPublic = true,
            LanguageCode = "en",
        };

        await controller.New(model);

        await pages.Received(1).CreateAsync(
            Arg.Is<Page>(p =>
                // The body's content-image + attachment references are
                // extracted server-side and set on the Page before the
                // CreateAsync call (the U03 invariant).
                p.ImageIds.Count >= 1 && p.ImageIds.Contains("a1b2c3d4")
                && p.AttachmentIds.Count >= 1 && p.AttachmentIds.Contains("e5f6a7b8")
                && p.Body == model.Body),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>());
    }

    // ── 403 on a standing re-check (C3) ────────────────────────────────────

    /// <summary>
    /// <see cref="PageController.Delete"/> on a page the service's
    /// <c>actorRoles</c> gate rejects (a <see cref="UnauthorizedAccessException"/>
    /// from <see cref="IPageService.DeleteAsync"/>) surfaces as a
    /// <see cref="ForbidResult"/> — the service is the real gate (C3), the
    /// Web <c>[Authorize(Roles=…)]</c> is a pre-gate. The pin: a standing
    /// re-check denial is NOT folded into a 404 (the page-specific split,
    /// ADR 0039 §3.8).
    /// </summary>
    [Fact]
    public async Task Delete_When_ServiceDeniesStanding_ReturnsForbid_NotNotFound()
    {
        const string pageId = "page-del-001";
        var pages = Substitute.For<IPageService>();
        pages.DeleteAsync(pageId, Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(Task.FromException(new UnauthorizedAccessException("only a Moderator/GlobalAdmin may delete")));
        var controller = Build(pages, IsAuthenticated: true, subjectId: "subj-admin-001", roles: new[] { Roles.Member });

        var result = await controller.Delete(pageId);

        Assert.IsType<ForbidResult>(result);
    }

    /// <summary>
    /// <see cref="PageController.Delete"/> on an absent page (a
    /// <see cref="KeyNotFoundException"/> from
    /// <see cref="IPageService.DeleteAsync"/>) surfaces as a
    /// <see cref="NotFoundResult"/> — the absent/denied split holds on the
    /// write lanes too (a 404 = the page does not exist; a 403 = the page
    /// exists but the caller may not delete it; ADR 0039 §3.8).
    /// </summary>
    [Fact]
    public async Task Delete_When_ServiceReturnsKeyNotFound_ReturnsNotFound()
    {
        const string pageId = "page-del-002";
        var pages = Substitute.For<IPageService>();
        pages.DeleteAsync(pageId, Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(Task.FromException(new KeyNotFoundException("no such page")));
        var controller = Build(pages, IsAuthenticated: true, subjectId: "subj-admin-001", roles: new[] { Roles.GlobalAdmin });

        var result = await controller.Delete(pageId);

        Assert.IsType<NotFoundResult>(result);
    }

    /// <summary>
    /// <see cref="PageController.Publish"/> on a page the service's
    /// author-only gate rejects (a <see cref="UnauthorizedAccessException"/>
    /// from <see cref="IPageService.PublishAsync"/>) surfaces as a
    /// <see cref="ForbidResult"/> — ADR 0037 author-only (a GlobalAdmin who
    /// is not the author is still denied; the service is the real gate, C3).
    /// </summary>
    [Fact]
    public async Task Publish_When_ServiceDeniesStanding_ReturnsForbid()
    {
        const string pageId = "page-pub-001";
        var pages = Substitute.For<IPageService>();
        pages.PublishAsync(pageId, Arg.Any<string>(), Arg.Any<IDocumentSession>())
            .Returns(Task.FromException<Page>(new UnauthorizedAccessException("only the author may publish")));
        var controller = Build(pages, IsAuthenticated: true, subjectId: "subj-admin-001", roles: new[] { Roles.GlobalAdmin });

        var result = await controller.Publish(pageId);

        Assert.IsType<ForbidResult>(result);
    }

    // ── PG6 — the user-added-translation lane (ADR 0029 carried to pages) ──
    //
    // The POST route is the Web plumbing over the IPageService.AddTranslationAsync
    // seam (C3 — the service is the standing authority; the controller re-runs
    // the Read gate first and surfaces 403/404). The standing decision itself
    // is pinned by the Core family (PageServiceTests.PG6_CanTranslatePage_* +
    // the U03 PG3_Translate_* write-lane tests). The (PageId, LanguageCode)
    // duplicate rejection is DB-side (the unique index) and is NOT re-tested
    // here (the service is substituted).

    /// <summary>
    /// (a) A <b>Translator</b> POSTs an <c>fr</c> translation on an
    /// authorized page → <see cref="IPageService.AddTranslationAsync"/> is
    /// called with the right args and the result is a
    /// <see cref="RedirectToActionResult"/> (NOT a 403) — the Read gate ran
    /// first and the success is surfaced. The <c>title</c> (a null-able) and
    /// <c>body</c> are passed through verbatim.
    /// </summary>
    [Fact]
    public async Task PG6_AddTranslation_Translator_Allowed_CallsService_AndRedirects()
    {
        const string pageId = "page-pg6-tr";
        var page = new Page
        {
            Id = pageId, Slug = "about", Title = "About", Body = "body",
            AuthorId = "someone-else", Audience = null, ComponentId = null,
            IsDraft = false, IsDeleted = false,
        };
        var (store, pages, authz) = WireStoreAndAllow(page);

        var controller = Build(pages, authz, store: store, IsAuthenticated: true,
            subjectId: "subj-translator", roles: new[] { Roles.Translator });

        var result = await controller.AddTranslation(pageId, "fr", "À propos", "Corps du texte");

        Assert.IsType<RedirectToActionResult>(result);
        await pages.Received(1).AddTranslationAsync(
            pageId, "fr", "À propos", "Corps du texte", "subj-translator",
            Arg.Is<IReadOnlySet<string>>(r => r.Contains(Roles.Translator)),
            Arg.Any<IDocumentSession>());
    }

    /// <summary>
    /// (b) ADR 0029 matrix — a <b>community Moderator</b> is <b>allowed</b> on
    /// a page whose <see cref="Page.ComponentId"/> is a community they moderate
    /// (the service's standing passes, the controller surfaces the success).
    /// The same actor is <b>denied</b> (403) on a flat/public page
    /// (<c>ComponentId == null</c> — no community to moderate), pinned by the
    /// sibling test <see cref="PG6_AddTranslation_CommunityModerator_FlatPage_Denied"/>.
    /// </summary>
    [Fact]
    public async Task PG6_AddTranslation_CommunityModerator_ScopedPage_Allowed()
    {
        const string pageId = "page-pg6-mod-s";
        var page = new Page
        {
            Id = pageId, Slug = "community-about", Title = "About", Body = "body",
            AuthorId = "someone-else",
            Audience = new Audience { Mode = AudienceMode.Any, Community = true, Grants = new List<AudienceGrant>() },
            ComponentId = "community-001",   // scoped — the moderator's standing key
            IsDraft = false, IsDeleted = false,
        };
        var (store, pages, authz) = WireStoreAndAllow(page);

        var controller = Build(pages, authz, store: store, IsAuthenticated: true,
            subjectId: "subj-mod", roles: new[] { Roles.Moderator, Roles.ModeratorComponent("community-001") });

        var result = await controller.AddTranslation(pageId, "de", "Über uns", "Körper");

        Assert.IsType<RedirectToActionResult>(result);
        await pages.Received(1).AddTranslationAsync(
            pageId, "de", "Über uns", "Körper", "subj-mod",
            Arg.Is<IReadOnlySet<string>>(r => r.Contains(Roles.ModeratorComponent("community-001"))),
            Arg.Any<IDocumentSession>());
    }

    /// <summary>
    /// (b) ADR 0029 matrix — the <b>flat/public page</b> branch: a community
    /// Moderator has no community to moderate on a page whose
    /// <see cref="Page.ComponentId"/> is <c>null</c>, so the standing
    /// <see cref="IPageService.AddTranslationAsync"/> re-check denies — the
    /// <see cref="UnauthorizedAccessException"/> surfaces as a
    /// <see cref="ForbidResult"/> (403), NOT a 404 (the page-specific split).
    /// </summary>
    [Fact]
    public async Task PG6_AddTranslation_CommunityModerator_FlatPage_Denied()
    {
        const string pageId = "page-pg6-mod-f";
        var page = new Page
        {
            Id = pageId, Slug = "about", Title = "About", Body = "body",
            AuthorId = "someone-else", Audience = null, ComponentId = null,   // flat/public
            IsDraft = false, IsDeleted = false,
        };
        var store = Substitute.For<IDocumentStore>();
        var readSession = Substitute.For<IQuerySession>();
        readSession.LoadAsync<Page>(pageId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Page?>(page));
        store.QuerySession().Returns(readSession);
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        var pages = Substitute.For<IPageService>();
        // The service's standing re-check (C3) denies — the component-moderator
        // branch does not qualify on a flat/public page.
        pages.AddTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(Task.FromException<PageTranslation>(new UnauthorizedAccessException(
                "only a GlobalAdmin, a Translator, or a moderator of the page's community may add a translation")));

        var authz = Substitute.For<IAuthorizationService>();
        authz.CanAsync(Arg.Any<string>(), Arg.Any<AccessAction>(), Arg.Any<IAuditableResource>())
            .Returns(new Decision(Allowed: true, Via: AccessVia.Audience, EffectivePrincipalId: "subj-mod"));

        var controller = Build(pages, authz, store: store, IsAuthenticated: true,
            subjectId: "subj-mod", roles: new[] { Roles.Moderator, Roles.ModeratorComponent("community-001") });

        var result = await controller.AddTranslation(pageId, "fr", "À propos", "Corps");

        Assert.IsType<ForbidResult>(result);
    }

    /// <summary>
    /// (c) A <b>plain Member</b> (no standing claim) POSTs a translation →
    /// the service's standing re-check denies → a <see cref="ForbidResult"/>
    /// (403), not a 404.
    /// </summary>
    [Fact]
    public async Task PG6_AddTranslation_PlainMember_Denied()
    {
        const string pageId = "page-pg6-mem";
        var page = new Page
        {
            Id = pageId, Slug = "about", Title = "About", Body = "body",
            AuthorId = "someone-else", Audience = null, ComponentId = null,
            IsDraft = false, IsDeleted = false,
        };
        var store = Substitute.For<IDocumentStore>();
        var readSession = Substitute.For<IQuerySession>();
        readSession.LoadAsync<Page>(pageId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Page?>(page));
        store.QuerySession().Returns(readSession);
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        var pages = Substitute.For<IPageService>();
        pages.AddTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(Task.FromException<PageTranslation>(new UnauthorizedAccessException("only a standing-holder may add a translation")));

        var authz = Substitute.For<IAuthorizationService>();
        authz.CanAsync(Arg.Any<string>(), Arg.Any<AccessAction>(), Arg.Any<IAuditableResource>())
            .Returns(new Decision(Allowed: true, Via: AccessVia.Audience, EffectivePrincipalId: "subj-member"));

        var controller = Build(pages, authz, store: store, IsAuthenticated: true,
            subjectId: "subj-member", roles: new[] { Roles.Member });

        var result = await controller.AddTranslation(pageId, "fr", "À propos", "Corps");

        Assert.IsType<ForbidResult>(result);
    }

    /// <summary>
    /// (d) <see cref="PageController.AddTranslation"/> on an absent page
    /// (the store's <c>LoadAsync&lt;Page&gt;</c> returns <c>null</c>) is a
    /// <see cref="NotFoundResult"/> (404) — the page-specific split holds on
    /// the translation lane too.
    /// </summary>
    [Fact]
    public async Task PG6_AddTranslation_AbsentPage_ReturnsNotFound()
    {
        // The default Build store's QuerySession().LoadAsync<Page> returns
        // null (the absent shape) — the actor is authenticated so the actorId
        // gate passes and the page load is what 404s.
        var pages = Substitute.For<IPageService>();
        var controller = Build(pages, IsAuthenticated: true, subjectId: "subj-translator", roles: new[] { Roles.Translator });

        var result = await controller.AddTranslation("page-pg6-absent", "fr", "À propos", "Corps");

        Assert.IsType<NotFoundResult>(result);
        await pages.DidNotReceive().AddTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>());
    }

    /// <summary>
    /// (e) The <see cref="PageShowViewModel.CanTranslate"/> affordance flag:
    /// <b>set</b> for a <b>Translator</b> on a public page (the display pin —
    /// <see cref="PageService.CanTranslatePage"/>), so the "add a
    /// translation" form renders. The real gate is the service (C3); the flag
    /// is the button's visibility.
    /// </summary>
    [Fact]
    public async Task PG6_Show_CanTranslateFlag_SetForTranslator()
    {
        const string path = "about";
        var page = new Page
        {
            Id = "page-pg6-flag-tr", Slug = "about", Title = "About", Body = "body",
            AuthorId = "someone-else", Audience = null, ComponentId = null,
            IsDraft = false, IsDeleted = false,
        };
        var pages = Substitute.For<IPageService>();
        pages.GetByPathAsync(path).Returns(page);
        pages.GetTranslationsAsync(page.Id).Returns(new List<PageTranslation>());
        pages.GetTreeAsync().Returns(new List<Page> { page });
        var authz = Substitute.For<IAuthorizationService>();
        authz.CanAsync(Arg.Any<string>(), Arg.Any<AccessAction>(), Arg.Any<IAuditableResource>())
            .Returns(new Decision(Allowed: true, Via: AccessVia.Audience, EffectivePrincipalId: "subj-translator"));

        var controller = Build(pages, authz, IsAuthenticated: true,
            subjectId: "subj-translator", roles: new[] { Roles.Translator });

        var view = (await controller.Show(path)) as ViewResult;
        Assert.NotNull(view);
        var model = view!.ViewData.Model as PageShowViewModel;
        Assert.NotNull(model);
        Assert.True(model!.CanTranslate);
    }

    /// <summary>
    /// (e) The <see cref="PageShowViewModel.CanTranslate"/> affordance flag:
    /// <b>unset</b> for a <b>plain Member</b> on a public page (the display
    /// pin — <see cref="PageService.CanTranslatePage"/> is false for a
    /// resident with no standing), so the "add a translation" form does not
    /// render.
    /// </summary>
    [Fact]
    public async Task PG6_Show_CanTranslateFlag_UnsetForPlainMember()
    {
        const string path = "about";
        var page = new Page
        {
            Id = "page-pg6-flag-mem", Slug = "about", Title = "About", Body = "body",
            AuthorId = "someone-else", Audience = null, ComponentId = null,
            IsDraft = false, IsDeleted = false,
        };
        var pages = Substitute.For<IPageService>();
        pages.GetByPathAsync(path).Returns(page);
        pages.GetTranslationsAsync(page.Id).Returns(new List<PageTranslation>());
        pages.GetTreeAsync().Returns(new List<Page> { page });
        var authz = Substitute.For<IAuthorizationService>();
        authz.CanAsync(Arg.Any<string>(), Arg.Any<AccessAction>(), Arg.Any<IAuditableResource>())
            .Returns(new Decision(Allowed: true, Via: AccessVia.Audience, EffectivePrincipalId: "subj-member"));

        var controller = Build(pages, authz, IsAuthenticated: true,
            subjectId: "subj-member", roles: new[] { Roles.Member });

        var view = (await controller.Show(path)) as ViewResult;
        Assert.NotNull(view);
        var model = view!.ViewData.Model as PageShowViewModel;
        Assert.NotNull(model);
        Assert.False(model!.CanTranslate);
    }

    // ── PG6 harness helper ─────────────────────────────────────────────────

    /// <summary>
    /// Wires the store (the page is loadable by id from
    /// <c>QuerySession().LoadAsync&lt;Page&gt;</c>) + the service
    /// (<see cref="IPageService.AddTranslationAsync"/> returns a fresh
    /// <see cref="PageTranslation"/> — the allow shape) + the Read decision
    /// (allowed). The deny shapes are wired inline in the sibling tests.
    /// </summary>
    private static (IDocumentStore store, IPageService pages, IAuthorizationService authz)
        WireStoreAndAllow(Page page)
    {
        var store = Substitute.For<IDocumentStore>();
        var readSession = Substitute.For<IQuerySession>();
        readSession.LoadAsync<Page>(page.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Page?>(page));
        store.QuerySession().Returns(readSession);
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        var pages = Substitute.For<IPageService>();
        pages.AddTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(call => Task.FromResult(new PageTranslation
            {
                Id = "tr-new",
                PageId = call.ArgAt<string>(0),
                LanguageCode = call.ArgAt<string>(1),
            }));

        var authz = Substitute.For<IAuthorizationService>();
        authz.CanAsync(Arg.Any<string>(), Arg.Any<AccessAction>(), Arg.Any<IAuditableResource>())
            .Returns(new Decision(Allowed: true, Via: AccessVia.Audience, EffectivePrincipalId: "subj"));

        return (store, pages, authz);
    }

    // ── harness ────────────────────────────────────────────────────────────

    private static ILocalizationService DefaultLocalization()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.ListLanguagesAsync().Returns(new List<LanguageCatalog>
        {
            new() { Id = "en", NativeName = "English", Enabled = true, SortOrder = 1 },
        });
        localization.GetDefaultLanguageCodeAsync().Returns("en");
        return localization;
    }

    private static PageController Build(
        IPageService pages,
        IAuthorizationService? authz = null,
        IUserInfoService? userInfo = null,
        IDocumentStore? store = null,
        string[]? roles = null,
        bool IsAuthenticated = false,
        string? subjectId = null)
    {
        var authzImpl = authz ?? Substitute.For<IAuthorizationService>();
        var userInfoImpl = userInfo ?? Substitute.For<IUserInfoService>();
        if (userInfo is null)
        {
            userInfoImpl.GetComponentsAsync(true).Returns(new List<Component>());
            userInfoImpl.GetProfilesAsync(true).Returns(new List<Profile>());
            userInfoImpl.GetPublicGroupsAsync().Returns(new List<Group>());
        }

        var storeImpl = store ?? Substitute.For<IDocumentStore>();
        if (store is null)
        {
            storeImpl.LightweightSession().Returns(Substitute.For<IDocumentSession>());
            var readSession = Substitute.For<IQuerySession>();
            readSession.LoadAsync<Page>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<Page?>(null));
            storeImpl.QuerySession().Returns(readSession);
        }

        var localization = DefaultLocalization();

        var controller = new PageController(pages, authzImpl, localization, userInfoImpl, storeImpl);
        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        if (IsAuthenticated || (roles is { Length: > 0 }))
        {
            var claims = new List<Claim>();
            if (subjectId is not null)
                claims.Add(new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, subjectId));
            if (roles is { Length: > 0 })
                claims.AddRange(roles.Select(r => new Claim(Kumunita.Core.Identity.ClaimTypes.Role, r)));

            controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(claims, authenticationType: "test"));
        }

        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());
        return controller;
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect / the call log, not the bag
        }
    }
}
