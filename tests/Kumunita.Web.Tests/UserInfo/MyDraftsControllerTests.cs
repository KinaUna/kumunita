using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using System.Security.Claims;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// C12 — the <see cref="MyDraftsController"/> seam (ADR 0037). The author's
/// own drafts across both post lanes + draft announcements + draft events.
/// A thin HTTP layer (ADR 0006-D): route + authz + shape. The empty-subject
/// path is the testable seam: an anonymous visitor (no <c>SubjectId</c> claim)
/// gets a <c>404</c> (there is no "whose drafts am I listing?" question).
/// </summary>
public sealed class MyDraftsControllerTests
{
    private static MyDraftsController BuildController(string? subjectId)
    {
        // PostService is sealed — construct a real one. The anonymous path the
        // test exercises returns NotFound() before PostService is touched, so
        // its backing store is a no-op substitute.
        var store = NSubstitute.Substitute.For<Marten.IDocumentStore>();
        var posts = new PostService(
            NSubstitute.Substitute.For<IUserInfoService>(),
            NSubstitute.Substitute.For<IAuthorizationService>(),
            store);

        var controller = new MyDraftsController(
            posts: posts,
            announcements: NSubstitute.Substitute.For<IAnnouncementService>(),
            userInfo: NSubstitute.Substitute.For<IUserInfoService>(),
            groupService: NSubstitute.Substitute.For<IGroupService>(),
            profileSettings: NSubstitute.Substitute.For<IProfileSettingsService>(),
            events: NSubstitute.Substitute.For<IEventService>());

        var httpContext = new DefaultHttpContext();
        if (subjectId is not null)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, subjectId) }, "test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    [Fact]
    public async Task Index_AnonymousVisitor_ReturnsNotFound()
    {
        // No SubjectId claim → the drafts are someone's own; an anonymous
        // caller has no drafts to list → 404 (the ADR 0037 "empty subject maps
        // to a 404" pin).
        var controller = BuildController(subjectId: null);
        var action = await controller.Index();
        Assert.IsType<NotFoundResult>(action);
    }
}
