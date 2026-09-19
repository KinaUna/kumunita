using System.Security.Claims;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// ADR 0048 — Web-side shape for the page translation edit/delete lanes.
/// Standing is GlobalAdmin ∪ Translator (via
/// <see cref="IPageService.UpdateTranslationAsync"/> /
/// <see cref="IPageService.RemoveTranslationAsync"/>); the Web layer
/// is thin (C3 — the service is the authority):
/// <list type="bullet">
/// <item>Anonymous → 403 <see cref="ForbidResult"/>.</item>
/// <item>Missing page / row → 404 <c>NotFound</c>.</item>
/// <item>Denied standing → 403 <see cref="ForbidResult"/>.</item>
/// <item>Happy path → 302 <c>Redirect</c> to the page's post-view path.</item>
/// </list>
/// </summary>
public class PageControllerTranslationTests
{
    private static readonly Guid PageId = Guid.NewGuid();

    [Fact]
    public async Task UpdateTranslation_GlobalAdmin_HappyPath_Redirects()
    {
        var pages = Substitute.For<IPageService>();
        var authz = Substitute.For<IAuthorizationService>();
        authz.CanAsync(Arg.Any<string>(), Arg.Any<AccessAction>(), Arg.Any<IAuditableResource>()).Returns(new Decision(true, AccessVia.Admin, "subj-admin"));

        var store = Substitute.For<IDocumentStore>();
        var readSession = Substitute.For<IQuerySession>();
        readSession.LoadAsync<Page>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Page?>(new Page
            {
                Id = PageId.ToString(),
                Slug = "about",
                Title = "About",
                Body = "b",
                AuthorId = "subj-admin",
                IsDraft = false,
            }));
        store.QuerySession().Returns(readSession);
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        pages.GetTreeAsync().Returns(new List<Page>
        {
            new() { Id = PageId.ToString(), Slug = "about", Title = "About", Body = "b", AuthorId = "subj-admin" },
        });

        var controller = Build(pages, authz, store, roles: new[] { Roles.GlobalAdmin }, subjectId: "subj-admin");

        var result = await controller.UpdateTranslation(PageId.ToString(), "fr", "À propos", "Corps");

        Assert.IsType<RedirectToActionResult>(result);
        await pages.Received(1).UpdateTranslationAsync(
            PageId.ToString(), "fr", "À propos", "Corps", "subj-admin",
            Arg.Is<IReadOnlySet<string>>(s => s.Contains(Roles.GlobalAdmin)), Arg.Any<IDocumentSession>());
    }

    [Fact]
    public async Task UpdateTranslation_Translator_HappyPath_Redirects()
    {
        var pages = Substitute.For<IPageService>();
        var authz = Substitute.For<IAuthorizationService>();
        authz.CanAsync(Arg.Any<string>(), Arg.Any<AccessAction>(), Arg.Any<IAuditableResource>()).Returns(new Decision(true, AccessVia.Admin, "subj-translator"));

        var store = Substitute.For<IDocumentStore>();
        var readSession = Substitute.For<IQuerySession>();
        readSession.LoadAsync<Page>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Page?>(new Page
            {
                Id = PageId.ToString(),
                Slug = "about",
                Title = "About",
                Body = "b",
                AuthorId = "subj-admin",
                IsDraft = false,
            }));
        store.QuerySession().Returns(readSession);
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        pages.GetTreeAsync().Returns(new List<Page>
        {
            new() { Id = PageId.ToString(), Slug = "about", Title = "About", Body = "b", AuthorId = "subj-admin" },
        });

        var controller = Build(pages, authz, store, roles: new[] { Roles.Translator }, subjectId: "subj-translator");

        var result = await controller.UpdateTranslation(PageId.ToString(), "fr", "À propos", "Corps");

        Assert.IsType<RedirectToActionResult>(result);
        await pages.Received(1).UpdateTranslationAsync(
            PageId.ToString(), "fr", "À propos", "Corps", "subj-translator",
            Arg.Is<IReadOnlySet<string>>(s => s.Contains(Roles.Translator)), Arg.Any<IDocumentSession>());
    }

    [Fact]
    public async Task UpdateTranslation_MissingPage_NotFound_NoWrite()
    {
        var pages = Substitute.For<IPageService>();
        var authz = Substitute.For<IAuthorizationService>();

        var store = Substitute.For<IDocumentStore>();
        var readSession = Substitute.For<IQuerySession>();
        readSession.LoadAsync<Page>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Page?>(new KeyNotFoundException("No page.")));
        store.QuerySession().Returns(readSession);
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        var controller = Build(pages, authz, store, roles: new[] { Roles.GlobalAdmin }, subjectId: "subj-admin");

        var result = await controller.UpdateTranslation(Guid.NewGuid().ToString(), "fr", "T", "Corps");

        Assert.IsType<NotFoundResult>(result);
        await pages.DidNotReceive().UpdateTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>());
    }

    [Fact]
    public async Task UpdateTranslation_Denied_Forbid()
    {
        var pages = Substitute.For<IPageService>();
        pages.UpdateTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(Task.FromException<PageTranslation>(new UnauthorizedAccessException("Denied.")));

        var authz = Substitute.For<IAuthorizationService>();
        authz.CanAsync(Arg.Any<string>(), Arg.Any<AccessAction>(), Arg.Any<IAuditableResource>()).Returns(new Decision(true, AccessVia.Admin, "subj-member"));

        var store = Substitute.For<IDocumentStore>();
        var readSession = Substitute.For<IQuerySession>();
        readSession.LoadAsync<Page>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Page?>(new Page
            {
                Id = PageId.ToString(),
                Slug = "about",
                Title = "About",
                Body = "b",
                AuthorId = "subj-admin",
                IsDraft = false,
            }));
        store.QuerySession().Returns(readSession);
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        pages.GetTreeAsync().Returns(new List<Page>
        {
            new() { Id = PageId.ToString(), Slug = "about", Title = "About", Body = "b", AuthorId = "subj-admin" },
        });

        var controller = Build(pages, authz, store, roles: new[] { Roles.Member }, subjectId: "subj-member");

        var result = await controller.UpdateTranslation(PageId.ToString(), "fr", "T", "Corps");

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task UpdateTranslation_MissingRow_NotFound()
    {
        var pages = Substitute.For<IPageService>();
        pages.UpdateTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(Task.FromException<PageTranslation>(new KeyNotFoundException("No row.")));

        var authz = Substitute.For<IAuthorizationService>();
        authz.CanAsync(Arg.Any<string>(), Arg.Any<AccessAction>(), Arg.Any<IAuditableResource>()).Returns(new Decision(true, AccessVia.Admin, "subj-admin"));

        var store = Substitute.For<IDocumentStore>();
        var readSession = Substitute.For<IQuerySession>();
        readSession.LoadAsync<Page>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Page?>(new Page
            {
                Id = PageId.ToString(),
                Slug = "about",
                Title = "About",
                Body = "b",
                AuthorId = "subj-admin",
                IsDraft = false,
            }));
        store.QuerySession().Returns(readSession);
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        pages.GetTreeAsync().Returns(new List<Page>
        {
            new() { Id = PageId.ToString(), Slug = "about", Title = "About", Body = "b", AuthorId = "subj-admin" },
        });

        var controller = Build(pages, authz, store, roles: new[] { Roles.GlobalAdmin }, subjectId: "subj-admin");

        var result = await controller.UpdateTranslation(PageId.ToString(), "fr", "T", "Corps");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateTranslation_Anonymous_Forbid_NoWrite()
    {
        var pages = Substitute.For<IPageService>();
        var authz = Substitute.For<IAuthorizationService>();
        var store = Substitute.For<IDocumentStore>();
        store.QuerySession().Returns(Substitute.For<IQuerySession>());
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        var controller = Build(pages, authz, store, IsAuthenticated: false);

        var result = await controller.UpdateTranslation(PageId.ToString(), "fr", "T", "Corps");

        Assert.IsType<ForbidResult>(result);
        await pages.DidNotReceive().UpdateTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>());
    }

    [Fact]
    public async Task RemoveTranslation_GlobalAdmin_HappyPath_Redirects()
    {
        var pages = Substitute.For<IPageService>();
        var authz = Substitute.For<IAuthorizationService>();
        authz.CanAsync(Arg.Any<string>(), Arg.Any<AccessAction>(), Arg.Any<IAuditableResource>()).Returns(new Decision(true, AccessVia.Admin, "subj-admin"));

        var store = Substitute.For<IDocumentStore>();
        var readSession = Substitute.For<IQuerySession>();
        readSession.LoadAsync<Page>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Page?>(new Page
            {
                Id = PageId.ToString(),
                Slug = "about",
                Title = "About",
                Body = "b",
                AuthorId = "subj-admin",
                IsDraft = false,
            }));
        store.QuerySession().Returns(readSession);
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        pages.GetTreeAsync().Returns(new List<Page>
        {
            new() { Id = PageId.ToString(), Slug = "about", Title = "About", Body = "b", AuthorId = "subj-admin" },
        });

        var controller = Build(pages, authz, store, roles: new[] { Roles.GlobalAdmin }, subjectId: "subj-admin");

        var result = await controller.RemoveTranslation(PageId.ToString(), "fr");

        Assert.IsType<RedirectToActionResult>(result);
        await pages.Received(1).RemoveTranslationAsync(
            PageId.ToString(), "fr", "subj-admin",
            Arg.Is<IReadOnlySet<string>>(s => s.Contains(Roles.GlobalAdmin)), Arg.Any<IDocumentSession>());
    }

    [Fact]
    public async Task RemoveTranslation_Translator_HappyPath_Redirects()
    {
        var pages = Substitute.For<IPageService>();
        var authz = Substitute.For<IAuthorizationService>();
        authz.CanAsync(Arg.Any<string>(), Arg.Any<AccessAction>(), Arg.Any<IAuditableResource>()).Returns(new Decision(true, AccessVia.Admin, "subj-translator"));

        var store = Substitute.For<IDocumentStore>();
        var readSession = Substitute.For<IQuerySession>();
        readSession.LoadAsync<Page>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Page?>(new Page
            {
                Id = PageId.ToString(),
                Slug = "about",
                Title = "About",
                Body = "b",
                AuthorId = "subj-admin",
                IsDraft = false,
            }));
        store.QuerySession().Returns(readSession);
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        pages.GetTreeAsync().Returns(new List<Page>
        {
            new() { Id = PageId.ToString(), Slug = "about", Title = "About", Body = "b", AuthorId = "subj-admin" },
        });

        var controller = Build(pages, authz, store, roles: new[] { Roles.Translator }, subjectId: "subj-translator");

        var result = await controller.RemoveTranslation(PageId.ToString(), "fr");

        Assert.IsType<RedirectToActionResult>(result);
        await pages.Received(1).RemoveTranslationAsync(
            PageId.ToString(), "fr", "subj-translator",
            Arg.Is<IReadOnlySet<string>>(s => s.Contains(Roles.Translator)), Arg.Any<IDocumentSession>());
    }

    [Fact]
    public async Task RemoveTranslation_Denied_Forbid()
    {
        var pages = Substitute.For<IPageService>();
        pages.RemoveTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(Task.FromException(new UnauthorizedAccessException("Denied.")));

        var authz = Substitute.For<IAuthorizationService>();
        authz.CanAsync(Arg.Any<string>(), Arg.Any<AccessAction>(), Arg.Any<IAuditableResource>()).Returns(new Decision(true, AccessVia.Admin, "subj-member"));

        var store = Substitute.For<IDocumentStore>();
        var readSession = Substitute.For<IQuerySession>();
        readSession.LoadAsync<Page>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Page?>(new Page
            {
                Id = PageId.ToString(),
                Slug = "about",
                Title = "About",
                Body = "b",
                AuthorId = "subj-admin",
                IsDraft = false,
            }));
        store.QuerySession().Returns(readSession);
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        pages.GetTreeAsync().Returns(new List<Page>
        {
            new() { Id = PageId.ToString(), Slug = "about", Title = "About", Body = "b", AuthorId = "subj-admin" },
        });

        var controller = Build(pages, authz, store, roles: new[] { Roles.Member }, subjectId: "subj-member");

        var result = await controller.RemoveTranslation(PageId.ToString(), "fr");

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task RemoveTranslation_MissingPage_NotFound_NoWrite()
    {
        var pages = Substitute.For<IPageService>();
        var authz = Substitute.For<IAuthorizationService>();

        var store = Substitute.For<IDocumentStore>();
        var readSession = Substitute.For<IQuerySession>();
        readSession.LoadAsync<Page>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Page?>(new KeyNotFoundException("No page.")));
        store.QuerySession().Returns(readSession);
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        var controller = Build(pages, authz, store, roles: new[] { Roles.GlobalAdmin }, subjectId: "subj-admin");

        var result = await controller.RemoveTranslation(Guid.NewGuid().ToString(), "fr");

        Assert.IsType<NotFoundResult>(result);
        await pages.DidNotReceive().RemoveTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>());
    }

    // ── Harness ─────────────────────────────────────────────────────────────

    private static PageController Build(
        IPageService pages,
        IAuthorizationService? authz = null,
        IDocumentStore? store = null,
        string[]? roles = null,
        bool IsAuthenticated = false,
        string? subjectId = null)
    {
        var authzImpl = authz ?? Substitute.For<IAuthorizationService>();
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetComponentsAsync(true).Returns(new List<Component>());
        userInfo.GetProfilesAsync(true).Returns(new List<Profile>());
        userInfo.GetPublicGroupsAsync().Returns(new List<Group>());

        var storeImpl = store ?? Substitute.For<IDocumentStore>();
        var localization = Substitute.For<ILocalizationService>();
        localization.ListLanguagesAsync().Returns(new List<LanguageCatalog>());

        var controller = new PageController(pages, authzImpl, localization, userInfo, storeImpl);
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

        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());
        return controller;
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }
}
