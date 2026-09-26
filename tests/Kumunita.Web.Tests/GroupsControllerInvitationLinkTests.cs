using System.Security.Claims;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
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
/// ADR 0095 — Web-side shape for the **link-clickable** group-invite accept /
/// decline lane (the group-invite email's links and the inbox's buttons all
/// land on the same two [Authorize] GET actions).
///
/// The gate is the identical **self-lane** shape as the POST self-lane
/// (C-M2b·2): the actor must be in their **own**
/// <see cref="IUserInfoService.GetPendingInvitationsForUserAsync"/> list for
/// the group id, and the seam (<see cref="IUserInfoService.
/// AcceptGroupInvitationAsync"/> / <see cref="IUserInfoService.
/// DeclineGroupInvitationAsync"/>) is the one that resolves the transition.
/// A non-invitee (or a resolved row) 404s (no seam call); a row resolved in
/// the gap (the Core's C-M2b·3 <c>InvalidOperationException</c> wall) maps to
/// the error <see cref="TempData"/> message and a redirect to
/// <see cref="GroupsController.Index"/> — never a 500. The accept happy path
/// redirects into the group's <see cref="GroupsController.Detail"/> (the
/// invitee is now a member); the decline happy path redirects to
/// <see cref="GroupsController.Index"/> (the invitee never becomes a member).
///
/// Mirrors the <see cref="GroupsControllerDeleteTests"/> harness: NSubstitute
/// for the seams, a no-op <see cref="ITempDataProvider"/> to close the
/// redirect's TempData touch.
/// </summary>
public class GroupsControllerInvitationLinkTests
{
    private const string GroupId = "grp-0095";

    [Fact]
    public async Task AcceptInvitationLink_HappyPath_RedirectsToDetail_AndAccepts()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetPendingInvitationsForUserAsync("subj-invitee").Returns(new List<GroupInvitation>
        {
            new() { Id = "inv-1", GroupId = GroupId, Status = InvitationStatus.Pending },
        });

        var controller = Build(userInfo, subjectId: "subj-invitee");

        var result = await controller.AcceptInvitationLink(GroupId);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(GroupsController.Detail), redirect.ActionName);
        Assert.Equal(GroupId, redirect.RouteValues?["id"]);
        await userInfo.Received(1).AcceptGroupInvitationAsync(GroupId, "subj-invitee");
    }

    [Fact]
    public async Task DeclineInvitationLink_HappyPath_RedirectsToIndex_AndDeclines()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetPendingInvitationsForUserAsync("subj-invitee").Returns(new List<GroupInvitation>
        {
            new() { Id = "inv-1", GroupId = GroupId, Status = InvitationStatus.Pending },
        });

        var controller = Build(userInfo, subjectId: "subj-invitee");

        var result = await controller.DeclineInvitationLink(GroupId);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(GroupsController.Index), redirect.ActionName);
        await userInfo.Received(1).DeclineGroupInvitationAsync(GroupId, "subj-invitee");
    }

    [Fact]
    public async Task AcceptInvitationLink_NotInMyPendingList_NotFound_NoSeamCall()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        // In the pending list for a DIFFERENT group — the self-lane gate fails.
        userInfo.GetPendingInvitationsForUserAsync("subj-other").Returns(new List<GroupInvitation>
        {
            new() { Id = "inv-2", GroupId = "grp-other", Status = InvitationStatus.Pending },
        });

        var controller = Build(userInfo, subjectId: "subj-other");

        var result = await controller.AcceptInvitationLink(GroupId);

        Assert.IsType<NotFoundResult>(result);
        await userInfo.DidNotReceiveWithAnyArgs().AcceptGroupInvitationAsync(GroupId, "subj-other");
    }

    [Fact]
    public async Task DeclineInvitationLink_EmptyPendingList_NotFound_NoSeamCall()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetPendingInvitationsForUserAsync("subj-none").Returns(new List<GroupInvitation>());

        var controller = Build(userInfo, subjectId: "subj-none");

        var result = await controller.DeclineInvitationLink(GroupId);

        Assert.IsType<NotFoundResult>(result);
        await userInfo.DidNotReceiveWithAnyArgs().DeclineGroupInvitationAsync(GroupId, "subj-none");
    }

    [Fact]
    public async Task AcceptInvitationLink_ResolvedInGap_MapsToError_AndRedirectsToIndex()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetPendingInvitationsForUserAsync("subj-invitee").Returns(new List<GroupInvitation>
        {
            new() { Id = "inv-1", GroupId = GroupId, Status = InvitationStatus.Pending },
        });
        // The row was resolved in the gap between the gate and the commit —
        // the Core's C-M2b·3 invalid-transition wall.
        userInfo.AcceptGroupInvitationAsync(GroupId, "subj-invitee")
            .Returns(Task.FromException(new InvalidOperationException("resolved")));

        var controller = Build(userInfo, subjectId: "subj-invitee");

        var result = await controller.AcceptInvitationLink(GroupId);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(GroupsController.Index), redirect.ActionName);
        Assert.Equal("That invitation is no longer pending.", controller.TempData["error"]);
    }

    [Fact]
    public async Task DeclineInvitationLink_ResolvedInGap_MapsToError_AndRedirectsToIndex()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetPendingInvitationsForUserAsync("subj-invitee").Returns(new List<GroupInvitation>
        {
            new() { Id = "inv-1", GroupId = GroupId, Status = InvitationStatus.Pending },
        });
        userInfo.DeclineGroupInvitationAsync(GroupId, "subj-invitee")
            .Returns(Task.FromException(new InvalidOperationException("resolved")));

        var controller = Build(userInfo, subjectId: "subj-invitee");

        var result = await controller.DeclineInvitationLink(GroupId);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(GroupsController.Index), redirect.ActionName);
        Assert.Equal("That invitation is no longer pending.", controller.TempData["error"]);
    }

    [Fact]
    public async Task AcceptInvitationLink_Unauthenticated_Unauthorized()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var controller = Build(userInfo, subjectId: null);

        var result = await controller.AcceptInvitationLink(GroupId);

        Assert.IsType<UnauthorizedResult>(result);
        await userInfo.DidNotReceiveWithAnyArgs().AcceptGroupInvitationAsync(GroupId, "subj-x");
    }

    // ── Harness (mirrors GroupsControllerDeleteTests.Build) ────────────────

    private static GroupsController Build(
        IUserInfoService? userInfo = null,
        string[]? roles = null,
        bool IsAuthenticated = false,
        string? subjectId = null)
    {
        var userInfoImpl = userInfo ?? Substitute.For<IUserInfoService>();
        var posts = new Kumunita.Core.Posts.PostService(
            userInfoImpl, Substitute.For<Kumunita.Core.Authorization.IAuthorizationService>(), Substitute.For<IDocumentStore>());
        var localization = Substitute.For<ILocalizationService>();
        localization.ListLanguagesAsync().Returns(new List<LanguageCatalog>());
        localization.GetDefaultLanguageCodeAsync().Returns("en");

        var store = Substitute.For<IDocumentStore>();
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        var events = Substitute.For<Kumunita.Core.Events.IEventService>();

        var controller = new GroupsController(userInfoImpl, posts, localization, store, events);
        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        if (IsAuthenticated || (roles is { Length: > 0 }) || subjectId is not null)
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
