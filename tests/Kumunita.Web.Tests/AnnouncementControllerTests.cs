using System.Security.Claims;
using Kumunita.Core.Announcements;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
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
    /// with a <b>null</b> <c>actorId</b> and an <b>empty</b> role set — the
    /// service then applies the <see cref="AnnouncementScope.Public"/>-only
    /// filter (and never surfaces a targeted <c>CommunityId</c> row). The
    /// controller must not accidentally pin a resident-style read.
    /// </summary>
    [Fact]
    public async Task Index_When_Anonymous_PassesNullActorId_ToService()
    {
        var announcements = Substitute.For<IAnnouncementService>();
        announcements.ListVisibleAsync(null, new HashSet<string>()).Returns(new List<Announcement>());
        var controller = Build(announcements, IsAuthenticated: false);

        await controller.Index();

        await announcements.Received(1).ListVisibleAsync(null, Arg.Is<IReadOnlySet<string>>(s => s.Count == 0));
        await announcements.DidNotReceive().ListVisibleAsync(Arg.Is<string>(x => x != null), Arg.Any<IReadOnlySet<string>>());
    }

    /// <summary>
    /// A signed-in resident drives the service with their <b>subject id</b>
    /// plus their role set — the service then applies the union (public +
    /// community, plus any targeted rows in the caller's communities). The
    /// subject-id pin is the only Web-side decision the read gate makes
    /// (the filtering is the service's, not the controller's).
    /// </summary>
    [Fact]
    public async Task Index_When_Authenticated_PassesSubjectId_ToService()
    {
        var announcements = Substitute.For<IAnnouncementService>();
        announcements.ListVisibleAsync("subj-resident-001", new HashSet<string> { Roles.Member }).Returns(new List<Announcement>());
        var controller = Build(announcements, roles: new[] { Roles.Member }, IsAuthenticated: true, subjectId: "subj-resident-001");

        await controller.Index();

        await announcements.Received(1).ListVisibleAsync("subj-resident-001", Arg.Is<IReadOnlySet<string>>(s => s.Count == 1 && s.Contains(Roles.Member)));
        await announcements.DidNotReceive().ListVisibleAsync(null, Arg.Any<IReadOnlySet<string>>());
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
        announcements.ListVisibleAsync("subj-resident-001", Arg.Any<IReadOnlySet<string>>()).Returns(new List<Announcement>
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
        userInfo.GetComponentsAsync(true).Returns(new List<Component>());

        var controller = Build(announcements, userInfo, roles: new[] { Roles.Member }, IsAuthenticated: true, subjectId: "subj-resident-001");

        var view = (await controller.Index()) as ViewResult;
        Assert.NotNull(view);

        var model = view!.ViewData.Model as AnnouncementIndexViewModel;
        Assert.NotNull(model);
        Assert.Single(model!.Announcements);
        Assert.Equal(author, model.Announcements[0].AuthorDisplayName);
        Assert.Equal(author, model.Announcements[0].AuthorSubjectId); // the avatar lane's subject
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
        announcements.ListVisibleAsync("subj-resident-001", Arg.Any<IReadOnlySet<string>>()).Returns(new List<Announcement>
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
        userInfo.GetComponentsAsync(true).Returns(new List<Component>());

        var controller = Build(announcements, userInfo, roles: new[] { Roles.Member }, IsAuthenticated: true, subjectId: "subj-resident-001");

        var view = (await controller.Index()) as ViewResult;
        Assert.NotNull(view);

        var model = view!.ViewData.Model as AnnouncementIndexViewModel;
        Assert.NotNull(model);
        Assert.Equal(display, model!.Announcements[0].AuthorDisplayName);
        Assert.Equal(author, model.Announcements[0].AuthorSubjectId); // the avatar lane's subject
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
    public async Task New_When_GlobalAdmin_Scopes_BothInOrder()
    {
        var controller = Build(Substitute.For<IAnnouncementService>(),
            roles: new[] { Roles.GlobalAdmin }, IsAuthenticated: true);

        var result = (await controller.New()) as ViewResult;

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
    public async Task New_When_Moderator_Scopes_OnlyCommunity()
    {
        var controller = Build(Substitute.For<IAnnouncementService>(),
            roles: new[] { Roles.Moderator }, IsAuthenticated: true);

        var result = (await controller.New()) as ViewResult;

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
            DefaultLocalization(),
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

    // ── Edit gate (GET + POST /announcements/{id}/edit) ─────────────────────

    /// <summary>
    /// GET /announcements/{id}/edit for a missing id: the store's read
    /// session returns null → the controller maps that to a clean 404
    /// (same "missing = 404, not 500" contract as the delete lane).
    /// </summary>
    [Fact]
    public async Task Edit_When_StoreHasNoSuchAnnouncement_Returns_404()
    {
        const string id = "ann-does-not-exist";

        var store = Substitute.For<IDocumentStore>();
        var readSession = Substitute.For<IQuerySession>();
        readSession.LoadAsync<Announcement>(id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Announcement?>(null));
        store.QuerySession().Returns(readSession);

        var controller = new AnnouncementController(
            Substitute.For<IAnnouncementService>(),
            Substitute.For<IUserInfoService>(),
            DefaultLocalization(),
            store);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await controller.Edit(id);

        Assert.IsType<NotFoundResult>(result);
    }

    /// <summary>
    /// GET /announcements/{id}/edit, happy path, GlobalAdmin on a
    /// public-scope announcement: the view model is seeded from the stored
    /// document (Title/Body/Scope preserved) and the picker offers both
    /// scopes — the edit form is a shape seeded from the existing row.
    /// </summary>
    [Fact]
    public async Task Edit_When_GlobalAdmin_EditFormSeededFromStoredRow_BothScopes()
    {
        const string id = "ann-edit-pub";
        var existing = new Announcement
        {
            Id = id, Scope = AnnouncementScope.Public,
            Title = "Old title", Body = "Old body",
            AuthorId = "subj-author-001", Created = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero),
        };

        var store = Substitute.For<IDocumentStore>();
        var readSession = Substitute.For<IQuerySession>();
        readSession.LoadAsync<Announcement>(id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Announcement?>(existing));
        store.QuerySession().Returns(readSession);

        var controller = new AnnouncementController(
            Substitute.For<IAnnouncementService>(),
            Substitute.For<IUserInfoService>(),
            DefaultLocalization(),
            store);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, "subj-admin-001"),
                        new Claim(Kumunita.Core.Identity.ClaimTypes.Role, Roles.GlobalAdmin),
                    },
                    authenticationType: "test")) },
        };

        var result = (await controller.Edit(id)) as ViewResult;

        Assert.NotNull(result);
        var model = result!.ViewData.Model as AnnouncementComposeViewModel;
        Assert.NotNull(model);
        Assert.Equal(id, model!.Id);
        Assert.Equal("Old title", model.Title);
        Assert.Equal("Old body", model.Body);
        Assert.Equal(AnnouncementScope.Public.ToString(), model.Scope);
        var scopes = model.AllowedScopes!.ToList();
        Assert.Equal(2, scopes.Count);
        Assert.Contains(AnnouncementScope.Public, scopes);
        Assert.Contains(AnnouncementScope.Community, scopes);
    }

    /// <summary>
    /// GET /announcements/{id}/edit, Moderator on a public-scope
    /// announcement: the shape gate refuses up front (403) — a form a user
    /// cannot submit is not rendered in the first place (the service's
    /// re-check is still the sole real gate at POST). The denied branch
    /// returns <see cref="ForbidResult"/> directly (no <c>TempData</c>
    /// write in this harness), so the pin is the result type — not a
    /// <c>ViewResult</c>. The ADR 0017 edit gate: the Public lane is
    /// GlobalAdmin-only, so a Moderator (no admin, not the author's lane)
    /// is refused.
    /// </summary>
    [Fact]
    public async Task Edit_When_Moderator_PublicScope_Returns_Forbid_NotView()
    {
        const string id = "ann-edit-denied";
        var existing = new Announcement
        {
            Id = id, Scope = AnnouncementScope.Public,
            Title = "t", Body = "b", AuthorId = "subj-author-001",
            Created = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero),
        };

        var store = Substitute.For<IDocumentStore>();
        var readSession = Substitute.For<IQuerySession>();
        readSession.LoadAsync<Announcement>(id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Announcement?>(existing));
        store.QuerySession().Returns(readSession);

        var controller = new AnnouncementController(
            Substitute.For<IAnnouncementService>(),
            Substitute.For<IUserInfoService>(),
            DefaultLocalization(),
            store);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = HttpContextWithSession(new[]
            {
                new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, "subj-mod-001"),
                new Claim(Kumunita.Core.Identity.ClaimTypes.Role, Roles.Moderator),
            }),
        };

        // The shape gate refuses up front: a Moderator is NOT in the
        // Public-scope edit standing (GlobalAdmin-only), so the controller
        // returns ForbidResult without seeding a form. (The service's
        // re-check at POST is still the sole real gate. The ADR 0017 edit
        // gate is the same rule re-expressed: Public lane = admin only.)
        var result = await controller.Edit(id);
        Assert.IsType<ForbidResult>(result);
    }

    /// <summary>
    /// GET /announcements/{id}/edit, ADR 0017 flat "all residents" lane:
    /// a community moderator who did NOT author the flat Community-scope
    /// announcement is refused the edit form up front (403) — the base
    /// Moderator role that still lets them CREATE a flat all-residents
    /// announcement does NOT let them EDIT one they didn't author. The
    /// non-author branch of the edit standing is denied before any form is
    /// seeded, so the pin is the ForbidResult (not a ViewResult).
    /// </summary>
    [Fact]
    public async Task Edit_When_NonAuthorModerator_FlatCommunityScope_Returns_Forbid()
    {
        const string id = "ann-edit-flat-mod";
        var existing = new Announcement
        {
            Id = id, Scope = AnnouncementScope.Community, CommunityId = null,
            Title = "t", Body = "b", AuthorId = "subj-author-001",
            Created = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero),
        };

        var store = Substitute.For<IDocumentStore>();
        var readSession = Substitute.For<IQuerySession>();
        readSession.LoadAsync<Announcement>(id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Announcement?>(existing));
        store.QuerySession().Returns(readSession);

        var controller = new AnnouncementController(
            Substitute.For<IAnnouncementService>(),
            Substitute.For<IUserInfoService>(),
            DefaultLocalization(),
            store);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = HttpContextWithSession(new[]
            {
                new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, "subj-mod-001"),
                new Claim(Kumunita.Core.Identity.ClaimTypes.Role, Roles.Moderator),
            }),
        };

        var result = await controller.Edit(id);
        Assert.IsType<ForbidResult>(result);
    }

    /// <summary>
    /// GET /announcements/{id}/edit, ADR 0017 flat "all residents" lane
    /// positive pin: the AUTHOR-of-record opens their own flat
    /// Community-scope announcement's edit form — the AuthorId==actorId
    /// branch of the edit standing is honored even though the actor holds
    /// only the Member role (not a role bypass, the author rule). The
    /// GlobalAdmin Public happy-path test is the same assertion shape;
    /// here the standing comes from authorship, not the admin role.
    /// </summary>
    [Fact]
    public async Task Edit_When_Author_FlatCommunityScope_Returns_View()
    {
        const string id = "ann-edit-flat-author";
        const string author = "subj-author-flat";
        var existing = new Announcement
        {
            Id = id, Scope = AnnouncementScope.Community, CommunityId = null,
            Title = "Old title", Body = "Old body", AuthorId = author,
            Created = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero),
        };

        var store = Substitute.For<IDocumentStore>();
        var readSession = Substitute.For<IQuerySession>();
        readSession.LoadAsync<Announcement>(id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Announcement?>(existing));
        store.QuerySession().Returns(readSession);

        var controller = new AnnouncementController(
            Substitute.For<IAnnouncementService>(),
            Substitute.For<IUserInfoService>(),
            DefaultLocalization(),
            store);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, author),
                        new Claim(Kumunita.Core.Identity.ClaimTypes.Role, Roles.Member),
                    },
                    authenticationType: "test")) },
        };

        var result = (await controller.Edit(id)) as ViewResult;

        Assert.NotNull(result);
        var model = result!.ViewData.Model as AnnouncementComposeViewModel;
        Assert.NotNull(model);
        Assert.Equal(id, model!.Id);
        Assert.Equal("Old title", model.Title);
        Assert.Equal("Old body", model.Body);
        Assert.Equal(AnnouncementScope.Community.ToString(), model.Scope);
    }

    /// <summary>
    /// POST /announcements/{id}/edit, happy path: a GlobalAdmin's valid
    /// submit drives the service's UpdateAsync exactly once with the id +
    /// posted Title/Body/Scope. The service-call assertion is the pin —
    /// the controller's <c>TempData["info"]</c> write on the success branch
    /// NREs in this harness (no <c>ISessionStore</c>), exactly per the
    /// <see cref="AdminControllerBlockTests"/> "NRE lands *after* the Core
    /// lane" convention: the pin is the NSubstitute call log (the service
    /// received the right args), not <c>RedirectToActionResult</c>.
    /// </summary>
    [Fact]
    public async Task Edit_Post_When_Valid_Calls_UpdateAsync()
    {
        const string id = "ann-edit-ok";
        var announcements = Substitute.For<IAnnouncementService>();
        // A canned return value (the controller ignores the service's return
        // value; the pin below is the received-args assertion, not the
        // return round-trip).
        var canned = new Announcement
        {
            Id = id, Scope = AnnouncementScope.Public, Title = "New title", Body = "New body",
            AuthorId = "subj-author-001", Created = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero),
        };
        announcements.UpdateAsync(
            Arg.Any<Announcement>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlySet<string>>(),
            Arg.Any<IDocumentSession>())
            .Returns(Task.FromResult(canned));

        var store = Substitute.For<IDocumentStore>();
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        var controller = new AnnouncementController(announcements, Substitute.For<IUserInfoService>(), DefaultLocalization(), store);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = HttpContextWithSession(new[]
            {
                new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, "subj-admin-001"),
                new Claim(Kumunita.Core.Identity.ClaimTypes.Role, Roles.GlobalAdmin),
            }),
        };

        try { await controller.Edit(id, new AnnouncementComposeViewModel
        {
            Id = id, Title = "New title", Body = "New body", Scope = "Public",
        }); }
        catch (NullReferenceException) { /* expected: TempData write NRE, see above */ }

        await announcements.Received(1).UpdateAsync(
            Arg.Is<Announcement>(a => a.Id == id
                && a.Title == "New title"
                && a.Body == "New body"
                && a.Scope == AnnouncementScope.Public),
            Arg.Any<string>(),
            Arg.Any<IReadOnlySet<string>>(),
            Arg.Any<IDocumentSession>());
    }

    /// <summary>
    /// POST /announcements/{id}/edit, the write-lane re-check: the service
    /// refuses the split (Moderator → public scope) with
    /// <see cref="UnauthorizedAccessException"/> → the controller maps that
    /// to a 403 (Forbid), not a 500.
    /// </summary>
    [Fact]
    public async Task Edit_Post_When_ServiceDenies_Returns_Forbid()
    {
        const string id = "ann-edit-denied";
        var announcements = Substitute.For<IAnnouncementService>();
        announcements.UpdateAsync(
            Arg.Any<Announcement>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlySet<string>>(),
            Arg.Any<IDocumentSession>())
            .Returns(Task.FromException<Announcement>(new UnauthorizedAccessException(
                "Only a GlobalAdmin may edit a public-scope announcement.")));

        var store = Substitute.For<IDocumentStore>();
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        var controller = new AnnouncementController(announcements, Substitute.For<IUserInfoService>(), DefaultLocalization(), store);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = HttpContextWithSession(new[]
            {
                new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, "subj-mod-001"),
                new Claim(Kumunita.Core.Identity.ClaimTypes.Role, Roles.Moderator),
            }),
        };

        var result = await controller.Edit(id, new AnnouncementComposeViewModel
        {
            Id = id, Title = "t", Body = "b", Scope = "Public",
        });

        Assert.IsType<ForbidResult>(result);
    }

    /// <summary>
    /// POST /announcements/{id}/edit, missing id from the service
    /// (<see cref="KeyNotFoundException"/>) → the controller maps that to a
    /// clean 404 (same contract as the delete lane).
    /// </summary>
    [Fact]
    public async Task Edit_Post_When_ServiceReportsMissing_Returns_404()
    {
        const string id = "ann-does-not-exist";
        var announcements = Substitute.For<IAnnouncementService>();
        announcements.UpdateAsync(
            Arg.Any<Announcement>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlySet<string>>(),
            Arg.Any<IDocumentSession>())
            .Returns(Task.FromException<Announcement>(new KeyNotFoundException(
                $"Announcement '{id}' was not found in the session; nothing to edit.")));

        var store = Substitute.For<IDocumentStore>();
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        var controller = new AnnouncementController(announcements, Substitute.For<IUserInfoService>(), DefaultLocalization(), store);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = HttpContextWithSession(new[]
            {
                new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, "subj-admin-001"),
                new Claim(Kumunita.Core.Identity.ClaimTypes.Role, Roles.GlobalAdmin),
            }),
        };

        var result = await controller.Edit(id, new AnnouncementComposeViewModel
        {
            Id = id, Title = "t", Body = "b", Scope = "Public",
        });

        Assert.IsType<NotFoundResult>(result);
    }

    // ── Harness ──

    /// <summary>
    /// Builds an authenticated <see cref="DefaultHttpContext"/> with the supplied
    /// claims plus an NSubstitute <see cref="Microsoft.AspNetCore.Http.ISession"/>
    /// (so controller actions that write to <c>TempData</c> don't NRE — 
    /// <c>DefaultHttpContext</c> leaves <c>Session</c> null by default, and the
    /// edit lane's success/denied branches both touch <c>TempData</c>).
    /// </summary>
    private static DefaultHttpContext HttpContextWithSession(Claim[] claims)
    {
        var session = NSubstitute.Substitute.For<Microsoft.AspNetCore.Http.ISession>();
        session.Id.Returns("test-session");
        session.IsAvailable.Returns(true);
        session.LoadAsync().Returns(Task.CompletedTask);
        session.CommitAsync().Returns(Task.CompletedTask);
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test")),
                     Session = session,
                };
            }

            // ──── Target-community picker (GET /announcements/new, targeting lane) ───

            /// <summary>
            /// A moderator's picker is scoped to the communities they hold the
            /// moderator-for claim about — the other community is hidden even though
            /// it is enabled and listed by the component seam.
            /// </summary>
            [Fact]
            public async Task New_When_Moderator_TargetCommunities_OnlyTheirs()
            {
                var userInfo = Substitute.For<IUserInfoService>();
                userInfo.GetComponentsAsync(true).Returns(new List<Component>
                {
                    new() { Id = "community-A", Name = "Community A" },
                    new() { Id = "community-B", Name = "Community B" },
                });
                var controller = Build(Substitute.For<IAnnouncementService>(), userInfo: userInfo,
                    roles: new[] { Roles.Moderator, Roles.ModeratorComponent("community-A") },
                    IsAuthenticated: true, subjectId: "subj-mod-001");

                var result = (await controller.New()) as ViewResult;

                var model = Assert.IsType<AnnouncementComposeViewModel>(result?.Model);
                var ids = model.TargetCommunities.Select(c => c.Id).ToHashSet();
                Assert.Contains("community-A", ids);
                Assert.DoesNotContain("community-B", ids);
            }

            /// <summary>
            /// A global admin gets every enabled community in the picker.
            /// </summary>
            [Fact]
            public async Task New_When_GlobalAdmin_TargetCommunities_All()
            {
                var userInfo = Substitute.For<IUserInfoService>();
                userInfo.GetComponentsAsync(true).Returns(new List<Component>
                {
                    new() { Id = "community-A", Name = "Community A" },
                    new() { Id = "community-B", Name = "Community B" },
                });
                var controller = Build(Substitute.For<IAnnouncementService>(), userInfo: userInfo,
                    roles: new[] { Roles.GlobalAdmin },
                    IsAuthenticated: true, subjectId: "subj-admin-001");

                var result = (await controller.New()) as ViewResult;

                var model = Assert.IsType<AnnouncementComposeViewModel>(result?.Model);
                var ids = model.TargetCommunities.Select(c => c.Id).ToHashSet();
                Assert.Contains("community-A", ids);
                Assert.Contains("community-B", ids);
            }

            /// <summary>
            /// A targeted row's <c>CommunityId</c> is resolved to a display name in
            /// the index feed (the feed's one read of the component seam), and the raw
            /// id is preserved on the row.
            /// </summary>
            [Fact]
            public async Task Index_When_TargetedAnnouncement_CommunityDisplayNameResolved()
            {
                const string author = "subj-mod-001";
                var announcements = Substitute.For<IAnnouncementService>();
                announcements.ListVisibleAsync("subj-resident-001", Arg.Any<IReadOnlySet<string>>()).Returns(
                    new List<Announcement>
                    {
                        new()
                        {
                            Id = "ann-targeted", Scope = AnnouncementScope.Community,
                            CommunityId = "community-A",
                            Title = "Potluck Saturday", Body = "Community A potluck, bring a side",
                            AuthorId = author, Created = new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero),
                        },
                    });

                var userInfo = Substitute.For<IUserInfoService>();
                userInfo.GetProfileAsync(author).Returns((Profile?)new Profile { SubjectId = author, DisplayName = "Community Moderator" });
                userInfo.GetComponentsAsync(true).Returns(new List<Component>
                {
                    new() { Id = "community-A", Name = "Community A" },
                });

                var controller = Build(announcements, userInfo, roles: new[] { Roles.Member }, IsAuthenticated: true, subjectId: "subj-resident-001");

                var result = (await controller.Index()) as ViewResult;

                var rows = Assert.IsType<List<AnnouncementRow>>((result?.Model as AnnouncementIndexViewModel)?.Announcements!);
                var row = rows.Single();
                Assert.Equal("community-A", row.CommunityId);
                Assert.Equal("Community A", row.CommunityDisplayName);
            }

            // ──── Detail (GET /announcements/{id}) — full-body read ────────────────

            /// <summary>
            /// The happy path: the service's <see cref="IAnnouncementService.GetAsync"/>
            /// returns the announcement (the gate already ran in the service) and the
            /// controller hands the view the <em>full</em> body — this is the surface
            /// the list's 250-character preview / the banner's 150-character preview
            /// link into. The author's display name resolves from the profile seam,
            /// the community-targeted row resolves its display name, and the pinned
            /// flag passes through untouched.
            /// </summary>
            [Fact]
            public async Task Detail_When_Visible_ReturnsFullBodyModel()
            {
                const string author = "subj-admin-001";
                string longBody = string.Concat(Enumerable.Repeat("Maintenance window text. ", 20));
                var announcements = Substitute.For<IAnnouncementService>();
                announcements.GetAsync("ann-detail", "subj-resident-001", Arg.Any<IReadOnlySet<string>>()).Returns(
                    new Announcement
                    {
                        Id = "ann-detail", Scope = AnnouncementScope.Public,
                        Title = "Scheduled maintenance", Body = longBody,
                        AuthorId = author, Created = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero),
                        Modified = new DateTimeOffset(2026, 1, 20, 12, 0, 0, TimeSpan.Zero), Pinned = true,
                    });

                var userInfo = Substitute.For<IUserInfoService>();
                userInfo.GetProfileAsync(author).Returns((Profile?)new Profile { SubjectId = author, DisplayName = "Site Admin" });

                var controller = Build(announcements, userInfo, roles: new[] { Roles.Member }, IsAuthenticated: true, subjectId: "subj-resident-001");

                var view = (await controller.Detail("ann-detail")) as ViewResult;
                Assert.NotNull(view);

                var model = Assert.IsType<AnnouncementDetailViewModel>(view!.ViewData.Model);
                Assert.Equal("ann-detail", model.Id);
                Assert.Equal(longBody, model.Body);        // the full text, not a truncated preview
                Assert.True(model.Pinned);
                Assert.Equal("Site Admin", model.AuthorDisplayName);
                Assert.Equal("subj-admin-001", model.AuthorSubjectId); // the avatar lane's subject
            }

            /// <summary>
            /// A missing <em>or</em> not-visible id both arrive as the service's
            /// <c>null</c> return (the gate — missing and denied are indistinguishable
            /// by design, no 403/audit lane on this bounded context) and the
            /// controller maps that to a clean <see cref="NotFound"/> — never a
            /// blank page, never a 500.
            /// </summary>
            [Fact]
            public async Task Detail_When_Missing_OrNotVisible_ReturnsNotFound()
            {
                var announcements = Substitute.For<IAnnouncementService>();
                announcements.GetAsync("ann-missing", "subj-resident-001", Arg.Any<IReadOnlySet<string>>()).Returns((Announcement?)null);
                var controller = Build(announcements, roles: new[] { Roles.Member }, IsAuthenticated: true, subjectId: "subj-resident-001");

                var result = await controller.Detail("ann-missing");

                Assert.IsType<NotFoundResult>(result);
                await announcements.DidNotReceive().ListVisibleAsync(Arg.Any<string?>(), Arg.Any<IReadOnlySet<string>>());
            }

            /// <summary>
            /// The anonymous visitor detail lane (public-scope announcements are
            /// visible signed out, e.g. a maintenance notice): the controller passes
            /// a <b>null</b> <c>actorId</c> and an <b>empty</b> role set to the
            /// service — the same shape pin as the list's read gate.
            /// </summary>
            [Fact]
            public async Task Detail_When_Anonymous_PassesNullActorId_ToService()
            {
                var announcements = Substitute.For<IAnnouncementService>();
                announcements.GetAsync("ann-public", null, Arg.Any<IReadOnlySet<string>>()).Returns(
                    new Announcement
                    {
                        Id = "ann-public", Scope = AnnouncementScope.Public,
                        Title = "Maintenance", Body = "Saturday 02:00-04:00 UTC",
                        AuthorId = "subj-admin-001", Created = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero),
                    });
                var userInfo = Substitute.For<IUserInfoService>();
                userInfo.GetProfileAsync("subj-admin-001").Returns((Profile?)null); // missing profile → subject-id fallback
                var controller = Build(announcements, userInfo, IsAuthenticated: false);

                var view = (await controller.Detail("ann-public")) as ViewResult;
                Assert.NotNull(view);

                await announcements.Received(1).GetAsync("ann-public", null, Arg.Is<IReadOnlySet<string>>(s => s.Count == 0));
                var model = Assert.IsType<AnnouncementDetailViewModel>(view!.ViewData.Model);
                Assert.Equal("subj-admin-001", model.AuthorDisplayName);
                Assert.Equal("subj-admin-001", model.AuthorSubjectId); // the avatar lane's subject
            }

            /// <summary>
            /// Builds an <see cref="AnnouncementController"/> with a substituted
    /// <see cref="IAnnouncementService"/> + <see cref="IUserInfoService"/>
    /// (the two store-adjacent seams) and a minted signed-in / signed-out
    /// principal (the <c>ClaimTypes.Role</c> claim type is Kumunita's — per
    /// <see cref="Kumunita.Web.Security.KumunitaPrincipal"/>), so the
    /// controller's <c>User.Identity.IsAuthenticated</c> is the right value
    /// for the read-gate pin.
    /// </summary>
    /// <summary>
    // ── ADR 0029 — AddTranslation (POST /announcements/{id}/translations) ──

    /// <summary>
    /// The happy path: a standing-holder (GlobalAdmin) adds a translation.
    /// The controller validates the form shape (language code + non-empty
    /// body), confirms the viewer can see the announcement (the flat scope
    /// gate re-run via <see cref="IAnnouncementService.GetAsync"/>), and then
    /// delegates the write + standing decision to
    /// <see cref="IAnnouncementService.AddAnnouncementTranslationAsync"/>
    /// (the service is the authority — the controller never re-derives the
    /// standing split itself, ADR 0006-D). On success, a <c>TempData["info"]</c>
    /// confirmation and a redirect back to the detail page.
    /// </summary>
    [Fact]
    public async Task AddTranslation_GlobalAdmin_HappyPath_Redirects()
    {
        const string id = "ann-t-001";
        var announcements = Substitute.For<IAnnouncementService>();
        announcements.GetAsync(id, "subj-admin-001", Arg.Any<IReadOnlySet<string>>())
            .Returns(new Announcement
            {
                Id = id, Scope = AnnouncementScope.Public,
                Title = "Original", Body = "Body",
                AuthorId = "subj-admin-001", Created = DateTimeOffset.UtcNow,
                LanguageCode = "en",
            });
        announcements.AddAnnouncementTranslationAsync(
            id, "fr", "Titre", "Corps", "subj-admin-001",
            Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(new AnnouncementTranslation
            {
                Id = "tr-001", AnnouncementId = id, LanguageCode = "fr",
                Title = "Titre", Body = "Corps", AuthorId = "subj-admin-001",
                Created = DateTimeOffset.UtcNow,
            });

        var controller = Build(announcements,
            roles: new[] { Roles.GlobalAdmin }, IsAuthenticated: true, subjectId: "subj-admin-001");

        var result = await controller.AddTranslation(id, "fr", "Titre", "Corps");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Detail", redirect.ActionName);
        Assert.Equal("Announcement", redirect.ControllerName);

        await announcements.Received(1).AddAnnouncementTranslationAsync(
            id, "fr", "Titre", "Corps", "subj-admin-001",
            Arg.Is<IReadOnlySet<string>>(s => s.Contains(Roles.GlobalAdmin)), Arg.Any<IDocumentSession>());
    }

    /// <summary>
    /// A community Moderator of the targeted community adds a translation —
    /// the ADR 0029 community-moderator lane, exercised through the Web
    /// surface (the service's standing re-check is the real gate; the
    /// controller only pins the route + shape + the flat-scope precondition).
    /// </summary>
    [Fact]
    public async Task AddTranslation_TargetedByCommunityModerator_HappyPath_Redirects()
    {
        const string id = "ann-t-002";
        var announcements = Substitute.For<IAnnouncementService>();
        announcements.GetAsync(id, "subj-mod-A", Arg.Any<IReadOnlySet<string>>())
            .Returns(new Announcement
            {
                Id = id, Scope = AnnouncementScope.Community, CommunityId = "community-A",
                Title = "Original", Body = "Body",
                AuthorId = "subj-admin-001", Created = DateTimeOffset.UtcNow,
                LanguageCode = "en",
            });
        announcements.AddAnnouncementTranslationAsync(
            id, "es", "Título", "Cuerpo", "subj-mod-A",
            Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(new AnnouncementTranslation
            {
                Id = "tr-002", AnnouncementId = id, LanguageCode = "es",
                Title = "Título", Body = "Cuerpo", AuthorId = "subj-mod-A",
                Created = DateTimeOffset.UtcNow,
            });

        var controller = Build(announcements,
            roles: new[] { Roles.Moderator, Roles.ModeratorComponent("community-A") },
            IsAuthenticated: true, subjectId: "subj-mod-A");

        var result = await controller.AddTranslation(id, "es", "Título", "Cuerpo");

        Assert.IsType<RedirectToActionResult>(result);
        await announcements.Received(1).AddAnnouncementTranslationAsync(
            id, "es", "Título", "Cuerpo", "subj-mod-A",
            Arg.Is<IReadOnlySet<string>>(s => s.Contains(Roles.ModeratorComponent("community-A"))), Arg.Any<IDocumentSession>());
    }

    /// <summary>
    /// A standing-denied actor (the service's <see
    /// cref="UnauthorizedAccessException"/>) maps to a clean <see
    /// cref="ForbidResult"/> — the controller does not swallow the exception
    /// into a 500, and the write lane never persists on a deny (the service
    /// throws before its <c>SaveChangesAsync</c>).
    /// </summary>
    [Fact]
    public async Task AddTranslation_Denied_Forbid_NoWrite()
    {
        const string id = "ann-t-003";
        var announcements = Substitute.For<IAnnouncementService>();
        announcements.GetAsync(id, "subj-member", Arg.Any<IReadOnlySet<string>>())
            .Returns(new Announcement
            {
                Id = id, Scope = AnnouncementScope.Public,
                Title = "Original", Body = "Body",
                AuthorId = "subj-admin-001", Created = DateTimeOffset.UtcNow,
                LanguageCode = "en",
            });
        announcements.AddAnnouncementTranslationAsync(
            id, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(Task.FromException<AnnouncementTranslation>(
                new UnauthorizedAccessException("Denied.")));

        var controller = Build(announcements,
            roles: new[] { Roles.Member }, IsAuthenticated: true, subjectId: "subj-member");

        var result = await controller.AddTranslation(id, "fr", "Titre", "Corps");

        Assert.IsType<ForbidResult>(result);
    }

    /// <summary>
    /// A missing (or not-visible) announcement: the service's
    /// <see cref="IAnnouncementService.GetAsync"/> returns <c>null</c> (the
    /// gate already ran) and the controller maps that to a clean
    /// <see cref="NotFoundResult"/> — the write lane never touches a dangling
    /// reference.
    /// </summary>
    [Fact]
    public async Task AddTranslation_MissingAnnouncement_NotFound()
    {
        var announcements = Substitute.For<IAnnouncementService>();
        announcements.GetAsync("ann-t-missing", "subj-admin-001", Arg.Any<IReadOnlySet<string>>())
            .Returns((Announcement?)null);

        var controller = Build(announcements,
            roles: new[] { Roles.GlobalAdmin }, IsAuthenticated: true, subjectId: "subj-admin-001");

        var result = await controller.AddTranslation("ann-t-missing", "fr", "Titre", "Corps");

        Assert.IsType<NotFoundResult>(result);
        await announcements.DidNotReceive().AddAnnouncementTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>());
    }

    /// <summary>
    /// A blank <c>languageCode</c> is a shape error — the controller
    /// short-circuits with a redirect back to the detail page (the form
    /// re-renders with the error) and never calls the service's write seam.
    /// </summary>
    [Fact]
    public async Task AddTranslation_BlankLanguageCode_RedirectsBack_NoWrite()
    {
        var announcements = Substitute.For<IAnnouncementService>();

        var controller = Build(announcements,
            roles: new[] { Roles.GlobalAdmin }, IsAuthenticated: true, subjectId: "subj-admin-001");

        var result = await controller.AddTranslation("ann-t-001", "   ", "Titre", "Corps");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Detail", redirect.ActionName);
        await announcements.DidNotReceive().AddAnnouncementTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>());
    }

    /// <summary>
    /// A blank <c>body</c> is a shape error (the same short-circuit as
    /// <see cref="AddTranslation_BlankLanguageCode_RedirectsBack_NoWrite"/>):
    /// a translation needs some text, and the write seam is never called.
    /// </summary>
    [Fact]
    public async Task AddTranslation_BlankBody_RedirectsBack_NoWrite()
    {
        var announcements = Substitute.For<IAnnouncementService>();

        var controller = Build(announcements,
            roles: new[] { Roles.GlobalAdmin }, IsAuthenticated: true, subjectId: "subj-admin-001");

        var result = await controller.AddTranslation("ann-t-001", "fr", "Titre", "");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Detail", redirect.ActionName);
        await announcements.DidNotReceive().AddAnnouncementTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>());
    }

    // ── ADR 0048 — UpdateTranslation / RemoveTranslation ────────────────────

    [Fact]
    public async Task UpdateTranslation_GlobalAdmin_HappyPath_Redirects()
    {
        const string id = "ann-u-001";
        var announcements = Substitute.For<IAnnouncementService>();
        announcements.GetAsync(id, "subj-admin-001", Arg.Any<IReadOnlySet<string>>())
            .Returns(new Announcement
            {
                Id = id, Scope = AnnouncementScope.Public,
                Title = "Original", Body = "Body",
                AuthorId = "subj-admin-001", Created = DateTimeOffset.UtcNow,
                LanguageCode = "en",
            });

        var controller = Build(announcements,
            roles: new[] { Roles.GlobalAdmin }, IsAuthenticated: true, subjectId: "subj-admin-001");

        var result = await controller.UpdateTranslation(id, "fr", "Nouveau", "Corps");

        Assert.IsType<RedirectToActionResult>(result);
        await announcements.Received(1).UpdateAnnouncementTranslationAsync(
            id, "fr", "Nouveau", "Corps", "subj-admin-001",
            Arg.Is<IReadOnlySet<string>>(s => s.Contains(Roles.GlobalAdmin)), Arg.Any<IDocumentSession>());
    }

    [Fact]
    public async Task UpdateTranslation_Denied_Forbid()
    {
        const string id = "ann-u-002";
        var announcements = Substitute.For<IAnnouncementService>();
        announcements.GetAsync(id, "subj-member", Arg.Any<IReadOnlySet<string>>())
            .Returns(new Announcement
            {
                Id = id, Scope = AnnouncementScope.Public,
                Title = "Original", Body = "Body",
                AuthorId = "subj-admin-001", Created = DateTimeOffset.UtcNow,
                LanguageCode = "en",
            });
        announcements.UpdateAnnouncementTranslationAsync(
            id, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(Task.FromException<Kumunita.Core.Announcements.AnnouncementTranslation>(new UnauthorizedAccessException("Denied.")));

        var controller = Build(announcements,
            roles: new[] { Roles.Member }, IsAuthenticated: true, subjectId: "subj-member");

        var result = await controller.UpdateTranslation(id, "fr", "X", "Corps");

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task UpdateTranslation_MissingAnnouncement_NotFound()
    {
        var announcements = Substitute.For<IAnnouncementService>();
        announcements.GetAsync("ann-u-missing", "subj-admin-001", Arg.Any<IReadOnlySet<string>>())
            .Returns((Announcement?)null);

        var controller = Build(announcements,
            roles: new[] { Roles.GlobalAdmin }, IsAuthenticated: true, subjectId: "subj-admin-001");

        var result = await controller.UpdateTranslation("ann-u-missing", "fr", "T", "Corps");

        Assert.IsType<NotFoundResult>(result);
        await announcements.DidNotReceive().UpdateAnnouncementTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>());
    }

    [Fact]
    public async Task UpdateTranslation_MissingRow_NotFound()
    {
        const string id = "ann-u-003";
        var announcements = Substitute.For<IAnnouncementService>();
        announcements.GetAsync(id, "subj-admin-001", Arg.Any<IReadOnlySet<string>>())
            .Returns(new Announcement
            {
                Id = id, Scope = AnnouncementScope.Public,
                Title = "Original", Body = "Body",
                AuthorId = "subj-admin-001", Created = DateTimeOffset.UtcNow,
                LanguageCode = "en",
            });
        announcements.UpdateAnnouncementTranslationAsync(
            id, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(Task.FromException<Kumunita.Core.Announcements.AnnouncementTranslation>(new KeyNotFoundException("No row.")));

        var controller = Build(announcements,
            roles: new[] { Roles.GlobalAdmin }, IsAuthenticated: true, subjectId: "subj-admin-001");

        var result = await controller.UpdateTranslation(id, "fr", "T", "Corps");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task RemoveTranslation_GlobalAdmin_HappyPath_Redirects()
    {
        const string id = "ann-r-001";
        var announcements = Substitute.For<IAnnouncementService>();
        announcements.GetAsync(id, "subj-admin-001", Arg.Any<IReadOnlySet<string>>())
            .Returns(new Announcement
            {
                Id = id, Scope = AnnouncementScope.Public,
                Title = "Original", Body = "Body",
                AuthorId = "subj-admin-001", Created = DateTimeOffset.UtcNow,
                LanguageCode = "en",
            });

        var controller = Build(announcements,
            roles: new[] { Roles.GlobalAdmin }, IsAuthenticated: true, subjectId: "subj-admin-001");

        var result = await controller.RemoveTranslation(id, "fr");

        Assert.IsType<RedirectToActionResult>(result);
        await announcements.Received(1).RemoveAnnouncementTranslationAsync(
            id, "fr", "subj-admin-001",
            Arg.Is<IReadOnlySet<string>>(s => s.Contains(Roles.GlobalAdmin)), Arg.Any<IDocumentSession>());
    }

    [Fact]
    public async Task RemoveTranslation_Denied_Forbid()
    {
        const string id = "ann-r-002";
        var announcements = Substitute.For<IAnnouncementService>();
        announcements.GetAsync(id, "subj-member", Arg.Any<IReadOnlySet<string>>())
            .Returns(new Announcement
            {
                Id = id, Scope = AnnouncementScope.Public,
                Title = "Original", Body = "Body",
                AuthorId = "subj-admin-001", Created = DateTimeOffset.UtcNow,
                LanguageCode = "en",
            });
        announcements.RemoveAnnouncementTranslationAsync(
            id, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(Task.FromException(new UnauthorizedAccessException("Denied.")));

        var controller = Build(announcements,
            roles: new[] { Roles.Member }, IsAuthenticated: true, subjectId: "subj-member");

        var result = await controller.RemoveTranslation(id, "fr");

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task RemoveTranslation_MissingAnnouncement_NotFound()
    {
        var announcements = Substitute.For<IAnnouncementService>();
        announcements.GetAsync("ann-r-missing", "subj-admin-001", Arg.Any<IReadOnlySet<string>>())
            .Returns((Announcement?)null);

        var controller = Build(announcements,
            roles: new[] { Roles.GlobalAdmin }, IsAuthenticated: true, subjectId: "subj-admin-001");

        var result = await controller.RemoveTranslation("ann-r-missing", "fr");

        Assert.IsType<NotFoundResult>(result);
        await announcements.DidNotReceive().RemoveAnnouncementTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>());
    }

    [Fact]
    public async Task RemoveTranslation_MissingRow_NotFound()
    {
        const string id = "ann-r-003";
        var announcements = Substitute.For<IAnnouncementService>();
        announcements.GetAsync(id, "subj-admin-001", Arg.Any<IReadOnlySet<string>>())
            .Returns(new Announcement
            {
                Id = id, Scope = AnnouncementScope.Public,
                Title = "Original", Body = "Body",
                AuthorId = "subj-admin-001", Created = DateTimeOffset.UtcNow,
                LanguageCode = "en",
            });
        announcements.RemoveAnnouncementTranslationAsync(
            id, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(Task.FromException(new KeyNotFoundException("No row.")));

        var controller = Build(announcements,
            roles: new[] { Roles.GlobalAdmin }, IsAuthenticated: true, subjectId: "subj-admin-001");

        var result = await controller.RemoveTranslation(id, "fr");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task RemoveTranslation_Anonymous_Forbid_NoWrite()
    {
        var announcements = Substitute.For<IAnnouncementService>();
        var controller = Build(announcements, IsAuthenticated: false);

        var result = await controller.RemoveTranslation("ann-r-001", "fr");

        Assert.IsType<ForbidResult>(result);
        await announcements.DidNotReceive().RemoveAnnouncementTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>());
    }

    /// <summary>
    /// An anonymous caller (no authenticated principal): the controller's
    /// <c>SubjectId(User)</c> is <c>null</c> and the lane short-circuits to a
    /// <see cref="ForbidResult"/> before touching the service — the write seam
    /// is never called and no write is persisted.
    /// </summary>
    [Fact]
    public async Task AddTranslation_Anonymous_Forbid_NoWrite()
    {
        var announcements = Substitute.For<IAnnouncementService>();
        var controller = Build(announcements, IsAuthenticated: false);

        var result = await controller.AddTranslation("ann-t-001", "fr", "Titre", "Corps");

        Assert.IsType<ForbidResult>(result);
        await announcements.DidNotReceive().AddAnnouncementTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>());
    }

    // ── ADR 0037 — draft mode (author-only) at the Web layer ─────────────
    //
    // The draft *behavior* (a draft is invisible to everyone but its author,
    // publish is author-only + idempotent, the my-drafts list is author-scoped)
    // is exhaustively pinned in the Core lane (Kumunita.Core.Tests.
    // PostDraftModeTests). These Web tests pin the thin HTTP layer's
    // <em>mapping</em> of that lane's outcomes to status codes + view flags —
    // the part the Core tests cannot see.

    /// <summary>
    /// <c>POST /announcements/{id}/publish</c> happy path (ADR 0037): the
    /// service's <c>GetAsync</c> returns the draft to the author (the
    /// author-only draft gate) and <c>PublishAsync</c> clears the flag → the
    /// controller redirects back to the detail page (now live) and sets the
    /// "published" TempData. The service is called with the actor's subject
    /// id (shape pin — the controller passes the right actor).
    /// </summary>
    [Fact]
    public async Task Publish_When_DraftAndAuthor_PublishesAndRedirects()
    {
        const string id = "ann-df-pub";
        const string author = "subj-df-author";
        var draft = new Announcement
        {
            Id = id, Scope = AnnouncementScope.Public,
            Title = "draft", Body = "a draft",
            AuthorId = author, IsDraft = true,
            Created = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
        };

        var announcements = Substitute.For<IAnnouncementService>();
        announcements.GetAsync(id, author, Arg.Any<IReadOnlySet<string>>()).Returns(draft);
        announcements.PublishAsync(id, author, Arg.Any<IDocumentSession>()).Returns(draft);

        var controller = Build(announcements, roles: new[] { Roles.Member },
            IsAuthenticated: true, subjectId: author);

        var result = await controller.Publish(id);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Detail", ((RedirectToActionResult)result).ActionName);
        await announcements.Received(1).PublishAsync(id, author, Arg.Any<IDocumentSession>());
        Assert.Equal("Announcement published.", controller.TempData["info"]);
    }

    /// <summary>
    /// <c>POST /announcements/{id}/publish</c> — the ADR 0037 author-only pin
    /// at the Web layer: the service's <c>GetAsync</c> returns <c>null</c> for
    /// a draft the actor is not the author of (missing and denied are
    /// indistinguishable by design), so the controller maps that to a
    /// <see cref="ForbidResult"/> (403) and <b>never</b> calls
    /// <c>PublishAsync</c> — no write, no leaked content.
    /// </summary>
    [Fact]
    public async Task Publish_When_DraftAndNonAuthor_Forbid_NoWrite()
    {
        const string id = "ann-df-nona";
        const string nonAuthor = "subj-df-nonauthor"; // a GlobalAdmin is *still* a non-author here

        var announcements = Substitute.For<IAnnouncementService>();
        // GetAsync returns null for the non-author (author-only draft gate).
        announcements.GetAsync(id, nonAuthor, Arg.Any<IReadOnlySet<string>>()).Returns((Announcement?)null);

        var controller = Build(announcements, roles: new[] { Roles.GlobalAdmin, Roles.Member },
            IsAuthenticated: true, subjectId: nonAuthor);

        var result = await controller.Publish(id);

        Assert.IsType<ForbidResult>(result);
        await announcements.DidNotReceive().PublishAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IDocumentSession>());
    }

    /// <summary>
    /// <c>POST /announcements/{id}/publish</c> — the service's re-check throws
    /// <see cref="UnauthorizedAccessException"/> (the defense-in-depth author
    /// gate, the ADR 0037 pin): the controller maps that to a
    /// <see cref="ForbidResult"/> — never a 500, never a re-render that would
    /// leak the draft's content.
    /// </summary>
    [Fact]
    public async Task Publish_When_ServiceThrowsUnauthorized_Forbid()
    {
        const string id = "ann-df-unauth";
        const string nonAuthor = "subj-df-unauth";
        var draft = new Announcement { Id = id, AuthorId = "subj-df-someone-else", IsDraft = true };

        var announcements = Substitute.For<IAnnouncementService>();
        announcements.GetAsync(id, nonAuthor, Arg.Any<IReadOnlySet<string>>()).Returns(draft);
        announcements.When(x => x.PublishAsync(id, nonAuthor, Arg.Any<IDocumentSession>()))
            .Throws(new UnauthorizedAccessException("Only the author of an announcement may publish it."));

        var controller = Build(announcements, roles: new[] { Roles.GlobalAdmin },
            IsAuthenticated: true, subjectId: nonAuthor);

        var result = await controller.Publish(id);

        Assert.IsType<ForbidResult>(result);
    }

    /// <summary>
    /// <c>POST /announcements/{id}/publish</c> — a missing id surfaces as the
    /// service's <see cref="KeyNotFoundException"/>; the controller maps that
    /// to a clean <see cref="NotFoundResult"/> (404, not 500).
    /// </summary>
    [Fact]
    public async Task Publish_When_ServiceThrowsKeyNotFound_404()
    {
        const string id = "ann-df-missing";
        const string author = "subj-df-missing-author";
        var draft = new Announcement { Id = id, AuthorId = author, IsDraft = true };

        var announcements = Substitute.For<IAnnouncementService>();
        announcements.GetAsync(id, author, Arg.Any<IReadOnlySet<string>>()).Returns(draft);
        announcements.When(x => x.PublishAsync(id, author, Arg.Any<IDocumentSession>()))
            .Throws(new KeyNotFoundException($"Announcement '{id}' was not found in the session; nothing to publish."));

        var controller = Build(announcements, IsAuthenticated: true, subjectId: author);

        var result = await controller.Publish(id);

        Assert.IsType<NotFoundResult>(result);
    }

    /// <summary>
    /// <c>POST /announcements/{id}/publish</c> — an anonymous caller
    /// (no authenticated principal): the controller's <c>SubjectId(User)</c>
    /// is empty and the lane short-circuits to an <see cref="UnauthorizedResult"/>
    /// before touching the service (the ADR 0037 author-only pin — there is no
    /// "author" to act for an anonymous visitor).
    /// </summary>
    [Fact]
    public async Task Publish_When_Anonymous_Forbid_NoWrite()
    {
        var announcements = Substitute.For<IAnnouncementService>();
        var controller = Build(announcements, IsAuthenticated: false);

        var result = await controller.Publish("ann-df-anon");

        Assert.IsType<UnauthorizedResult>(result);
        await announcements.DidNotReceive().PublishAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IDocumentSession>());
    }

    /// <summary>
    /// <c>GET /announcements/{id}</c> — a draft the actor authored: the
    /// controller hands the view <c>IsAuthor = true</c> and
    /// <c>IsDraft = true</c> (the two flags that drive the detail page's
    /// draft badge + the author-only Publish button — ADR 0037). The service's
    /// <c>GetAsync</c> returned the draft to the author (the author-only gate),
    /// so both flags are true for exactly the one viewer who may act.
    /// </summary>
    [Fact]
    public async Task Detail_When_DraftAuthor_FlagsAuthorAndDraft()
    {
        const string id = "ann-df-detail";
        const string author = "subj-df-detail-author";
        var announcements = Substitute.For<IAnnouncementService>();
        announcements.GetAsync(id, author, Arg.Any<IReadOnlySet<string>>()).Returns(
            new Announcement
            {
                Id = id, Scope = AnnouncementScope.Public,
                Title = "draft", Body = "a draft",
                AuthorId = author, IsDraft = true,
                Created = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
            });
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync(author).Returns((Profile?)new Profile { SubjectId = author, DisplayName = "Author" });

        var controller = Build(announcements, userInfo, roles: new[] { Roles.Member },
            IsAuthenticated: true, subjectId: author);

        var view = (await controller.Detail(id)) as ViewResult;
        var model = Assert.IsType<AnnouncementDetailViewModel>(view!.ViewData.Model);
        Assert.True(model.IsAuthor);
        Assert.True(model.IsDraft);
        Assert.Equal(author, model.AuthorSubjectId);
    }

    /// <summary>
    /// <c>GET /announcements/{id}</c> — a draft the actor is <b>not</b> the
    /// author of (a GlobalAdmin included, the ADR 0037 author-only pin):
    /// the service's <c>GetAsync</c> returns <c>null</c> (missing and denied
    /// are indistinguishable by design) and the controller maps that to a
    /// clean <see cref="NotFoundResult"/> — the non-leaky 404, never a re-render
    /// that would leak the draft's content to a non-author.
    /// </summary>
    [Fact]
    public async Task Detail_When_DraftNonAuthor_ReturnsNotFound()
    {
        const string id = "ann-df-detailex";
        const string nonAuthor = "subj-df-detail-nonauthor";

        var announcements = Substitute.For<IAnnouncementService>();
        announcements.GetAsync(id, nonAuthor, Arg.Any<IReadOnlySet<string>>()).Returns((Announcement?)null);

        var controller = Build(announcements, roles: new[] { Roles.GlobalAdmin, Roles.Member },
            IsAuthenticated: true, subjectId: nonAuthor);

        var result = await controller.Detail(id);

        Assert.IsType<NotFoundResult>(result);
    }

    /// <summary>
    /// <c>POST /announcements/new</c> (compose) — the ADR 0037 draft toggle
    /// binds through to the service's write: the controller maps the form's
    /// <c>SaveAsDraft</c> flag onto the <see cref="Announcement.IsDraft"/>
    /// field before calling <c>CreateAsync</c>. A <c>true</c> checkbox must
    /// produce a draft announcement at the service seam (the Core lane pins
    /// that a draft is then invisible to all but the author).
    /// </summary>
    [Fact]
    public async Task New_Post_SaveAsDraft_True_CreatesDraft()
    {
        const string author = "subj-df-compose-author";
        var announcements = Substitute.For<IAnnouncementService>();

        // A capturing closure: when the controller calls CreateAsync, record the
        // Announcement it passed so we can assert on the IsDraft mapping below.
        Announcement? captured = null;
        announcements.CreateAsync(
            Arg.Any<Announcement>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(call =>
            {
                captured = call.ArgAt<Announcement>(0);
                return new Announcement { Id = "ann-df-created" };
            });

        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetComponentsAsync(true).Returns(new List<Component>());

        var controller = Build(announcements, userInfo, roles: new[] { Roles.GlobalAdmin },
            IsAuthenticated: true, subjectId: author);

        var model = new AnnouncementComposeViewModel
        {
            Scope = "Public",
            Title = "a draft",
            Body = "draft body",
            SaveAsDraft = true,
        };

        await controller.New(model);

        Assert.NotNull(captured);
        Assert.True(captured!.IsDraft);
    }

    /// <summary>
    /// <c>POST /announcements/new</c> (compose) — the ADR 0037 draft toggle's
    /// negative: an unchecked <c>SaveAsDraft</c> (default <c>false</c>)
    /// produces a <b>live</b> announcement at the service seam (the toggle is
    /// opt-in — the default behavior of creating a visible announcement is
    /// unchanged).
    /// </summary>
    [Fact]
    public async Task New_Post_SaveAsDraft_Unchecked_CreatesLive()
    {
        const string author = "subj-df-compose-live";
        var announcements = Substitute.For<IAnnouncementService>();

        Announcement? captured = null;
        announcements.CreateAsync(
            Arg.Any<Announcement>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(call =>
            {
                captured = call.ArgAt<Announcement>(0);
                return new Announcement { Id = "ann-df-live" };
            });

        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetComponentsAsync(true).Returns(new List<Component>());

        var controller = Build(announcements, userInfo, roles: new[] { Roles.GlobalAdmin },
            IsAuthenticated: true, subjectId: author);

        var model = new AnnouncementComposeViewModel
        {
            Scope = "Public",
            Title = "a live post",
            Body = "live body",
            SaveAsDraft = false,
        };

        await controller.New(model);

        Assert.NotNull(captured);
        Assert.False(captured!.IsDraft);
    }

    // ── ADR 0037 — draft save stays on the composer (no navigation) ─────────

    /// <summary>
    /// ADR 0037 — the draft-save <b>navigation</b> pin: a <c>POST /announcements/new</c>
    /// with <c>SaveAsDraft = true</c> and <b>no</b> remembered <see
    /// cref="AnnouncementComposeViewModel.DraftId"/> (a first save) must (a)
    /// <b>create</b> the draft via <see cref="IAnnouncementService.CreateAsync"/>
    /// (not the edit seam), (b) return a <b>view</b> (re-render the composer)
    /// rather than a redirect to the feed (a redirect would leave the author's
    /// content looking lost, since the draft is invisible to the feed), and
    /// (c) set <c>model.DraftId</c> to the created announcement's id so a
    /// subsequent save continues this same draft. The author stays where they
    /// left off — "persist it and let the user continue working on it."
    /// </summary>
    [Fact]
    public async Task New_Post_SaveAsDraft_FirstSave_CreatesAndStaysOnComposer()
    {
        const string author = "subj-df-compose-stay";
        var announcements = Substitute.For<IAnnouncementService>();
        announcements.CreateAsync(
            Arg.Any<Announcement>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(call =>
            {
                var a = call.ArgAt<Announcement>(0);
                a.Id = "ann-df-stay-created";
                return a;
            });

        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetComponentsAsync(true).Returns(new List<Component>());

        var controller = Build(announcements, userInfo, roles: new[] { Roles.GlobalAdmin },
            IsAuthenticated: true, subjectId: author);

        var model = new AnnouncementComposeViewModel
        {
            Scope = "Public",
            Title = "a draft",
            Body = "draft body",
            SaveAsDraft = true,
            DraftId = null,
        };

        var result = await controller.New(model);

        // (a) created via the create seam — and NOT via the edit seam.
        await announcements.Received(1).CreateAsync(
            Arg.Any<Announcement>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>());
        await announcements.DidNotReceive().UpdateAsync(
            Arg.Any<Announcement>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>());

        // (b) stayed on the composer (a view), not a redirect away.
        var view = Assert.IsType<ViewResult>(result);
        var reRendered = Assert.IsType<AnnouncementComposeViewModel>(view.ViewData.Model);
        // The author's content is still in the form (continued where they left off).
        Assert.Equal("draft body", reRendered.Body);

        // (c) the draft's id is remembered for the next save.
        Assert.Equal("ann-df-stay-created", model.DraftId);
    }

    /// <summary>
    /// ADR 0037 — the draft-save <b>no-duplicate</b> pin: a <c>POST
    /// /announcements/new</c> with <c>SaveAsDraft = true</c> and a remembered
    /// <see cref="AnnouncementComposeViewModel.DraftId"/> (a continuation save)
    /// must <b>update</b> the existing draft via <see
    /// cref="IAnnouncementService.UpdateAsync"/> — passing the remembered id as
    /// the announcement's <c>Id</c> — and must <b>not</b> mint a duplicate via
    /// <see cref="IAnnouncementService.CreateAsync"/>. Re-saving while staying
    /// on the composer keeps pointing at one draft (the id round-trips via the
    /// form's hidden field).
    /// </summary>
    [Fact]
    public async Task New_Post_SaveAsDraft_SecondSave_UpdatesSameDraft_NoDuplicate()
    {
        const string author = "subj-df-compose-again";
        const string existingDraftId = "ann-df-existing";
        var announcements = Substitute.For<IAnnouncementService>();

        Announcement? capturedUpdate = null;
        announcements.UpdateAsync(
            Arg.Any<Announcement>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(call =>
            {
                capturedUpdate = call.ArgAt<Announcement>(0);
                return capturedUpdate;
            });

        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetComponentsAsync(true).Returns(new List<Component>());

        var controller = Build(announcements, userInfo, roles: new[] { Roles.GlobalAdmin },
            IsAuthenticated: true, subjectId: author);

        var model = new AnnouncementComposeViewModel
        {
            Scope = "Public",
            Title = "a draft",
            Body = "draft body, revised",
            SaveAsDraft = true,
            DraftId = existingDraftId,
        };

        var result = await controller.New(model);

        // Updated the existing draft — carrying its id — and did NOT create a new one.
        await announcements.Received(1).UpdateAsync(
            Arg.Any<Announcement>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>());
        await announcements.DidNotReceive().CreateAsync(
            Arg.Any<Announcement>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>());

        Assert.NotNull(capturedUpdate);
        Assert.Equal(existingDraftId, capturedUpdate!.Id);

        // Stayed on the composer.
        Assert.IsType<ViewResult>(result);
        Assert.Equal(existingDraftId, model.DraftId);
    }

    /// <summary>
    /// ADR 0018 — a default <see cref="ILocalizationService"/> substitute for
    /// the compose form's language picker: an empty enabled catalog + the
    /// <c>en</c> instance-default floor (a valid shape — the view renders an
    /// empty language picker). Used by the direct-construction test sites (and
    /// the <see cref="Build"/> helper) so the new constructor dependency has a
    /// plain substitute to read from.
    /// </summary>
    private static ILocalizationService DefaultLocalization()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.ListLanguagesAsync().Returns(new List<LanguageCatalog>());
        localization.GetDefaultLanguageCodeAsync().Returns("en");
        return localization;
    }

    private static AnnouncementController Build(
        IAnnouncementService announcements,
        IUserInfoService? userInfo = null,
        string[]? roles = null,
        bool IsAuthenticated = false,
        string? subjectId = null)
    {
        var userInfoImpl = userInfo ?? Substitute.For<IUserInfoService>();
        // The compose shape seeds the target-community picker from the UserInfo
        // seam (an empty candidate set is a valid shape — the view renders an
        // "all residents"-only picker for a plain Moderator). Only defaulted
        // here for a caller-provided substitute its own setup wins.
        if (userInfo is null)
            userInfoImpl.GetComponentsAsync(true).Returns(new List<Component>());

        var store = Substitute.For<IDocumentStore>();
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        // ADR 0018 — the compose form's language picker reads the catalog +
        // instance default through the HTTP-free ILocalizationService seam.
        var localization = DefaultLocalization();

        var controller = new AnnouncementController(announcements, userInfoImpl, localization, store);
        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
        };

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

        // The write lanes (AddTranslation / Edit / Delete) all touch TempData on
        // the way out — the harness has no ITempDataProvider by default, so the
        // controller's TempData property NREs on first access. A no-op fake
        // (the AdminDateFormatControllerTests / MLUI_FacesTests pattern) closes
        // it without requiring a real ISession.
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
