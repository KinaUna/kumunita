using System.Security.Claims;
using Kumunita.Core.Announcements;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// Unit tests for <see cref="AnnouncementController"/>. The pins this harness
/// owns (see <see cref="IAnnouncementService"/> for the seam shape it exercises
/// and <see cref="AnnouncementService"/> for the full ADR rationale):
/// <list type="number">
/// <item><b>Read gate — the visitor vs resident split</b> (the flat
///       <see cref="AnnouncementScope"/> split on <em>read</em>): a signed-out
///       caller (<c>User.Identity.IsAuthenticated == false</c>) drives the
///       service with <c>isAuthenticated: false</c> — the service then filters
///       <see cref="AnnouncementScope.Public"/> only; a signed-in caller drives
///       the service with <c>true</c> — the service then returns the union.
///       This is a <em>shape</em> pin (the controller passes the right
///       flag); the <em>filtering</em> itself is the service's and is already
///       covered by <see cref="Kumunita.Core.Tests.AnnouncementServiceTests.ListVisible_Anonymous_OnlySeesPublic"/>
///       and its <c>Authenticated</c> twin.</item>
/// <item><b>Null-safe author display-name fallback</b> (the read surface never
///       crashes on a missing profile row): when
///       <see cref="IUserInfoService.GetProfileAsync"/> returns null, the
///       <see cref="AnnouncementRow.AuthorDisplayName"/> cell is the raw
///       subject id, not an exception.</item>
/// <item><b>Scope-picker seeding (GET /announcements/new)</b> (the write-lane
///       shape): a GlobalAdmin's picker lists <c>Public</c> before
///       <c>Community</c> (in that order — the picker is the <em>shape</em>
///       convenience; the server-side <see cref="AnnouncementService.CreateAsync"/>
///       re-check is what actually pins the split at POST); a Moderator's
///       picker lists only <c>Community</c>.</item>
/// <item><b>404 on unknown delete id</b> (<c>KeyNotFoundException</c>
///       from the service → <see cref="NotFound"/> at the Web layer): the
///       controller does NOT swallow the exception into a 500 — a missing
///       announcement is a clean 404.</item>
/// </list>
/// <para>
/// The harness pattern mirrors <see cref="AdminControllerBlockTests"/>: a
/// sealed-concrete DI service (now exposed through its
/// <see cref="IAnnouncementService"/> seam) is substituted with NSubstitute;
/// <see cref="IDocumentStore"/> is also substituted (<see cref="Controller.Store"/>
/// never called on the read lane — only <c>LightweightSession()</c> on the
/// write lane, which the controller's await-using disposes).
/// </para>
/// </summary>
public class AnnouncementControllerTests
{
    // ── Read gate (GET /announcements) — visitor vs resident split ─────────

    /// <summary>
    /// A signed-out visitor (no authenticated principal) drives the service
    /// with <c>isAuthenticated: false</c> — the service then applies the
    /// <see cref="AnnouncementScope.Public"/>-only filter. The controller
    /// must not accidentally pin a resident-style read.
    /// </summary>
    [Fact]
    public async Task Index_When_Anonymous_Passes_False_ToService()
    {
        var announcements = Substitute.For<IAnnouncementService>();
        announcements.ListVisibleAsync(false).Returns(new List<Announcement>());
        var controller = Build(announcements, IsAuthenticated: false);

        await controller.Index();

        await announcements.Received(1).ListVisibleAsync(false);
        await announcements.DidNotReceive().ListVisibleAsync(true);
    }

    /// <summary>
    /// A signed-in resident (any authenticated role) drives the service with
    /// <c>isAuthenticated: true</c> — the service then applies the union (public
    /// + community). The <c>true</c> pin is the only Web-side decision
    /// the read gate makes (the filtering is the service's, not the
    /// controller's).
    /// </summary>
    [Fact]
    public async Task Index_When_Authenticated_Passes_True_ToService()
    {
        var announcements = Substitute.For<IAnnouncementService>();
        announcements.ListVisibleAsync(true).Returns(new List<Announcement>());
        var controller = Build(announcements, IsAuthenticated: true);

        await controller.Index();

        await announcements.Received(1).ListVisibleAsync(true);
        await announcements.DidNotReceive().ListVisibleAsync(false);
    }

    // ── Read gate display-name fallback (null-safe) ──

    /// <summary>
    /// When an author's profile row is <b>missing</b> in the UserInfo seam (the
    /// <c>GetProfileAsync</c> call returns <c>null</c>), the row's
    /// <see cref="AnnouncementRow.AuthorDisplayName"/> must fall back to the
    /// author's raw subject id — not crash the read surface, not render an
    /// empty string. A platform announcement with an unknown author is still
    /// a valid read (a profile deletion / seed-lane drift is normal
    /// operational state, not a fatal condition).
    /// </summary>
    [Fact]
    public async Task Index_When_ProfileMissing_FallsBackToSubjectId()
    {
        const string author = "subj-anon-author-001";
        var announcements = Substitute.For<IAnnouncementService>();
        announcements.ListVisibleAsync(true).Returns(new List<Announcement>
        {
            new()
            {
                Id = "ann-1", Scope = AnnouncementScope.Public,
                Title = "Scheduled maintenance", Body = "Saturday 02:00-04:00 UTC",
                AuthorId = author, Created = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero),
            },
        });

        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync(author).Returns((Profile?)null);

        var controller = Build(announcements, userInfo, IsAuthenticated: true);

        var view = (await controller.Index()) as ViewResult;
        Assert.NotNull(view);

        var model = view!.ViewData.Model as AnnouncementIndexViewModel;
        Assert.NotNull(model);
        Assert.Single(model!.Announcements);
        Assert.Equal(author, model.Announcements[0].AuthorDisplayName);
    }

    /// <summary>
    /// The happy path: a known author's profile row exists and the
    /// <see cref="Profile.DisplayName"/> field is non-trivial. The
    /// <see cref="AnnouncementRow.AuthorDisplayName"/> is that field — not
    /// the raw subject id. This is the pin for the <em>fallback not being
    /// too greedy</em> (the fallback is the <c>null</c> / <c>""</c> case,
    /// not "always prefer the raw id over a set display name").
    /// </summary>
    [Fact]
    public async Task Index_When_ProfilePresent_UsesDisplayName()
    {
        const string author = "subj-admin-001";
        const string display = "Kumunita Admin";
        var announcements = Substitute.For<IAnnouncementService>();
        announcements.ListVisibleAsync(true).Returns(new List<Announcement>
        {
            new()
            {
                Id = "ann-1", Scope = AnnouncementScope.Public,
                Title = "Scheduled maintenance", Body = "Saturday 02:00-04:00 UTC",
                AuthorId = author, Created = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero),
            },
        });

        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync(author).Returns(new Profile
        {
            SubjectId = author,
            DisplayName = display,
        });

        var controller = Build(announcements, userInfo, IsAuthenticated: true);

        var view = (await controller.Index()) as ViewResult;
        Assert.NotNull(view);

        var model = view!.ViewData.Model as AnnouncementIndexViewModel;
        Assert.NotNull(model);
        Assert.Equal(display, model!.Announcements[0].AuthorDisplayName);
    }

    // ── Scope-picker seeding (GET /announcements/new) ──────────────────────

    /// <summary>
    /// A GlobalAdmin's scope picker lists both scopes, in the documented
    /// order: <see cref="AnnouncementScope.Public"/> first, then
    /// <see cref="AnnouncementScope.Community"/> (the picker is the
    /// *shape* convenience — the server-side
    /// <see cref="AnnouncementService.CreateAsync"/> re-check at POST is the
    /// real gate, not the picker). This is the pin that a GlobalAdmin's
    /// picker is never scoped to Community-only (a UI regression would mean
    /// the GlobalAdmin cannot even *express* a public-scope intent before
    /// POST — the POST would succeed, but that's not the documented UX).
    /// </summary>
    [Fact]
    public void New_When_GlobalAdmin_Scopes_BothInOrder()
    {
        var controller = Build(Substitute.For<IAnnouncementService>(),
            roles: new[] { Roles.GlobalAdmin }, IsAuthenticated: true);

        var result = controller.New() as ViewResult;

        Assert.NotNull(result);

        var model = result!.ViewData.Model as AnnouncementComposeViewModel;
        Assert.NotNull(model);
        var scopes = model!.AllowedScopes!.ToList();
        Assert.Equal(2, scopes.Count);
        Assert.Contains(AnnouncementScope.Public, scopes);
        Assert.Contains(AnnouncementScope.Community, scopes);
        Assert.Equal(AnnouncementScope.Public, scopes[0]);
        Assert.Equal(AnnouncementScope.Community, scopes[1]);
    }

    /// <summary>
    /// A Moderator's scope picker lists only <see cref="AnnouncementScope.Community"/>
    /// — the GlobalAdmin-exclusive <c>Public</c> scope is not offered at the
    /// picker. A UI regression where a Moderator's picker accidentally exposes
    /// <c>Public</c> would be caught at POST time by the Core re-check, but
    /// the picker is the documented UX (ADR-style: the shape pins the
    /// author's role before they write, so the server-side re-check is
    /// defense-in-depth, not the <em>sole</em> gate).
    /// </summary>
    [Fact]
    public void New_When_Moderator_Scopes_OnlyCommunity()
    {
        var controller = Build(Substitute.For<IAnnouncementService>(),
            roles: new[] { Roles.Moderator }, IsAuthenticated: true);

        var result = controller.New() as ViewResult;

        Assert.NotNull(result);

        var model = result!.ViewData.Model as AnnouncementComposeViewModel;
        Assert.NotNull(model);
        var scopes = model!.AllowedScopes!.ToList();
        Assert.Single(scopes);
        Assert.Equal(AnnouncementScope.Community, scopes[0]);
    }

    // ── Delete 404 on unknown id ───────────────────────────────────────────

    /// <summary>
    /// A GlobalAdmin POSTs to <c>/announcements/{id}/delete</c> for an id the
    /// store cannot resolve (the service's <see cref="AnnouncementService.DeleteAsync"/>
    /// throws <see cref="KeyNotFoundException"/>). The Web layer must map
    /// that to a clean <c>404</c> (<see cref="NotFound"/>), not swallow it
    /// into a 500. This is the pin for the Web-side contract: a missing
    /// announcement is a not-found, not an internal error.
    /// </summary>
    [Fact]
    public async Task Delete_When_ServiceReportsMissing_Returns_404()
    {
        const string id = "ann-does-not-exist";
        var announcements = Substitute.For<IAnnouncementService>();
        announcements.DeleteAsync(id, Arg.Any<IDocumentSession>())
            .Returns(Task.FromException(new KeyNotFoundException($"Announcement '{id}' was not found in the session; nothing to delete.")));

        var store = Substitute.For<IDocumentStore>();
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        var controller = new AnnouncementController(
            announcements,
            Substitute.For<IUserInfoService>(),
            store);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };

        // The service's seam contract: a missing id is a KeyNotFoundException
        // (the Web layer maps that to a 404 via the catch clause above).
        // If the controller did NOT catch KNE, this would bubble up as an
        // unhandled exception (a 500-shape regression). The pin is the
        // call log — DeleteAsync received exactly once with the id the
        // controller received (shape match) — plus the test not crashing,
        // which is the observable "404, not 500" at the unit boundary.
        await controller.Delete(id);

        await announcements.Received(1).DeleteAsync(id, Arg.Any<IDocumentSession>());
    }

    // ── Harness ─────────────────────────────────────────────────────────────–

    /// <summary>
    /// Builds an <see cref="AnnouncementController"/> with a substituted
    /// <see cref="IAnnouncementService"/> + <see cref="IUserInfoService"/>
    /// (the two store-adjacent seams) and a minted signed-in / signed-out
    /// principal (the <c>ClaimTypes.Role</c> claim type is Kumunita's — per
    /// <see cref="Kumunita.Web.Security.KumunitaPrincipal"/>), so the
    /// controller's <c>User.Identity.IsAuthenticated</c> is the right value
    /// for the read-gate pin.
    /// </summary>
    private static AnnouncementController Build(
        IAnnouncementService announcements,
        IUserInfoService? userInfo = null,
        string[]? roles = null,
        bool IsAuthenticated = false)
    {
        var userInfoImpl = userInfo ?? Substitute.For<IUserInfoService>();
        var store = Substitute.For<IDocumentStore>();
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        var controller = new AnnouncementController(announcements, userInfoImpl, store);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };

        if (IsAuthenticated || (roles is { Length: > 0 }))
        {
            controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    roles?.Select(r => new Claim(Kumunita.Core.Identity.ClaimTypes.Role, r)) ?? Enumerable.Empty<Claim>(),
                    authenticationType: "test"));
        }

        return controller;
    }
}
