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
/// ADR 0093 — Web-side shape for the owner ∪ GlobalAdmin group-delete
/// lane. The gate is <see cref="GroupsController"/>'s
/// <c>TryResolveOwnerSurface</c>: the actor must be in the owner ∪ member
/// projection (<c>TryResolveWriteSurface</c>) AND be either the owner or a
/// GlobalAdmin. A plain member or non-member POST 404s (no seam call), and
/// the happy path 302s to <see cref="GroupsController.Index"/> (the actor is
/// out of the projection on the very next read — the ADR 0008 redirect
/// shape). Mirrors <see cref="GroupsControllerTranslationTests"/> harness:
/// NSubstitute for the seams, a no-op <see cref="ITempDataProvider"/> to
/// close the write lane's TempData touch.
/// </summary>
public class GroupsControllerDeleteTests
{
    private const string GroupId = "grp-001";

    [Fact]
    public async Task DeleteGroup_Owner_HappyPath_RedirectsToIndex_AndDeletes()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetGroupsForUserAsync("subj-owner").Returns(new List<Group>
        {
            new() { Id = GroupId, Name = "G", OwnerId = "subj-owner" },
        });

        var controller = Build(userInfo, roles: new[] { Roles.Member }, subjectId: "subj-owner");

        var result = await controller.DeleteGroup(GroupId);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(GroupsController.Index), redirect.ActionName);
        await userInfo.Received(1).DeleteGroupAsync(GroupId, "subj-owner");
    }

    [Fact]
    public async Task DeleteGroup_GlobalAdmin_HappyPath_RedirectsToIndex_AndDeletes()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        // The actor is a member (in the projection) but NOT the owner; the
        // GlobalAdmin standing is what clears the owner ∪ GlobalAdmin gate.
        userInfo.GetGroupsForUserAsync("subj-admin").Returns(new List<Group>
        {
            new() { Id = GroupId, Name = "G", OwnerId = "subj-owner" },
        });

        var controller = Build(userInfo, roles: new[] { Roles.Member, Roles.GlobalAdmin }, subjectId: "subj-admin");

        var result = await controller.DeleteGroup(GroupId);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(GroupsController.Index), redirect.ActionName);
        await userInfo.Received(1).DeleteGroupAsync(GroupId, "subj-admin");
    }

    [Fact]
    public async Task DeleteGroup_PlainMember_NotFound_NoDelete()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        // In the projection (a member) but neither owner nor GlobalAdmin.
        userInfo.GetGroupsForUserAsync("subj-member").Returns(new List<Group>
        {
            new() { Id = GroupId, Name = "G", OwnerId = "subj-owner" },
        });

        var controller = Build(userInfo, roles: new[] { Roles.Member }, subjectId: "subj-member");

        var result = await controller.DeleteGroup(GroupId);

        Assert.IsType<NotFoundResult>(result);
        await userInfo.DidNotReceive().DeleteGroupAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task DeleteGroup_NonMember_NotFound_NoDelete()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        // Empty projection — the group is not in the actor's visible set.
        userInfo.GetGroupsForUserAsync("subj-outside").Returns(new List<Group>());

        var controller = Build(userInfo, roles: new[] { Roles.Member }, subjectId: "subj-outside");

        var result = await controller.DeleteGroup(GroupId);

        Assert.IsType<NotFoundResult>(result);
        await userInfo.DidNotReceive().DeleteGroupAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    // ── Harness ─────────────────────────────────────────────────────────────

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
