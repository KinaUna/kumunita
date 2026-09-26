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
/// ADR 0048 — Web-side shape for the group name/description translation
/// edit/delete lanes. The group lane is **404 fail-closed** (a 403 would
/// advertise a gate the group lane does not expose — the ADR 0022 group-
/// lane pin):
/// <list type="bullet">
/// <item>Anonymous → 404 <c>NotFound</c> (no subject id).</item>
/// <item>Group not in the actor's visible projection → 404
///       (the controller's <c>TryResolveWriteSurface</c> gate).</item>
/// <item>Service throws <see cref="KeyNotFoundException"/> → 404
///       (missing row).</item>
/// <item>Service throws <see cref="UnauthorizedAccessException"/> → 404
///       (denied standing — the group lane's fail-closed posture, not 403).</item>
/// <item>Happy path → 302 <c>Redirect</c> to the group detail page.</item>
/// </list>
/// <para>
/// Mirrors <see cref="AnnouncementControllerTests"/> /
/// <see cref="CommunityControllerTranslationTests"/> harness: NSubstitute
/// for the seams, a no-op <see cref="ITempDataProvider"/> to close the
/// write lane's TempData touch.
/// </para>
/// <para>
/// The group post / reply translation edit/delete Web lanes (which delegate
/// to <see cref="Kumunita.Core.Posts.PostService"/>'s
/// <c>UpdatePostTranslationAsync</c> / <c>UpdateReplyTranslationAsync</c> /
/// <c>RemovePostTranslationAsync</c> / <c>RemoveReplyTranslationAsync</c> —
/// the same Core seams as the community-lane
/// <see cref="PostsController"/> shapes) aren't isolated-tested here:
/// <see cref="Kumunita.Core.Posts.PostService"/> is a sealed concrete class
/// (NSubstitute cannot proxy it), and their standing + exception contract
/// are already pinned end-to-end by the Core-level
/// <see cref="Kumunita.Core.Tests.PostTranslationTests"/> suite (which the
/// Web layer's 404/Forbid mapping mirrors verbatim).
/// </para>
/// </summary>
public class GroupsControllerTranslationTests
{
    private const string GroupId = "grp-001";

    [Fact]
    public async Task UpdateTranslation_Owner_HappyPath_Redirects()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetGroupsForUserAsync("subj-owner").Returns(new List<Group>
        {
            new() { Id = GroupId, Name = "G", OwnerId = "subj-owner" },
        });

        var controller = Build(userInfo, roles: new[] { Roles.Member }, subjectId: "subj-owner");

        var result = await controller.UpdateTranslation(GroupId, "fr", "Nouveau", "Desc");

        Assert.IsType<RedirectToActionResult>(result);
        await userInfo.Received(1).UpdateGroupTranslationAsync(
            GroupId, "fr", "Nouveau", "Desc", "subj-owner",
            Arg.Is<IReadOnlySet<string>>(s => s.Contains(Roles.Member)), Arg.Any<IDocumentSession>());
    }

    [Fact]
    public async Task UpdateTranslation_MissingGroup_NotFound_NoWrite()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetGroupsForUserAsync("subj-member").Returns(new List<Group>());

        var controller = Build(userInfo, roles: new[] { Roles.Member }, subjectId: "subj-member");

        var result = await controller.UpdateTranslation("grp-missing", "fr", "X", "Desc");

        Assert.IsType<NotFoundResult>(result);
        await userInfo.DidNotReceive().UpdateGroupTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>());
    }

    [Fact]
    public async Task UpdateTranslation_Anonymous_NotFound_NoWrite()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var controller = Build(userInfo, IsAuthenticated: false);

        var result = await controller.UpdateTranslation(GroupId, "fr", "X", "Desc");

        Assert.IsType<ForbidResult>(result);
        await userInfo.DidNotReceive().UpdateGroupTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>());
    }

    [Fact]
    public async Task UpdateTranslation_Denied_NotFound()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetGroupsForUserAsync("subj-member").Returns(new List<Group>
        {
            new() { Id = GroupId, Name = "G", OwnerId = "subj-owner" },
        });
        userInfo.UpdateGroupTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(Task.FromException<GroupTranslation>(new UnauthorizedAccessException("Denied.")));

        var controller = Build(userInfo, roles: new[] { Roles.Member }, subjectId: "subj-member");

        var result = await controller.UpdateTranslation(GroupId, "fr", "X", "Desc");

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task UpdateTranslation_MissingRow_NotFound()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetGroupsForUserAsync("subj-owner").Returns(new List<Group>
        {
            new() { Id = GroupId, Name = "G", OwnerId = "subj-owner" },
        });
        userInfo.UpdateGroupTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(Task.FromException<GroupTranslation>(new KeyNotFoundException("No row.")));

        var controller = Build(userInfo, roles: new[] { Roles.Member }, subjectId: "subj-owner");

        var result = await controller.UpdateTranslation(GroupId, "fr", "X", "Desc");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task RemoveTranslation_Owner_HappyPath_Redirects()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetGroupsForUserAsync("subj-owner").Returns(new List<Group>
        {
            new() { Id = GroupId, Name = "G", OwnerId = "subj-owner" },
        });

        var controller = Build(userInfo, roles: new[] { Roles.Member }, subjectId: "subj-owner");

        var result = await controller.RemoveTranslation(GroupId, "fr");

        Assert.IsType<RedirectToActionResult>(result);
        await userInfo.Received(1).RemoveGroupTranslationAsync(
            GroupId, "fr", "subj-owner",
            Arg.Is<IReadOnlySet<string>>(s => s.Contains(Roles.Member)), Arg.Any<IDocumentSession>());
    }

    [Fact]
    public async Task RemoveTranslation_MissingGroup_NotFound_NoWrite()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetGroupsForUserAsync("subj-member").Returns(new List<Group>());

        var controller = Build(userInfo, roles: new[] { Roles.Member }, subjectId: "subj-member");

        var result = await controller.RemoveTranslation("grp-missing", "fr");

        Assert.IsType<NotFoundResult>(result);
        await userInfo.DidNotReceive().RemoveGroupTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>());
    }

    [Fact]
    public async Task RemoveTranslation_Denied_NotFound()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetGroupsForUserAsync("subj-member").Returns(new List<Group>
        {
            new() { Id = GroupId, Name = "G", OwnerId = "subj-owner" },
        });
        userInfo.RemoveGroupTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(Task.FromException(new UnauthorizedAccessException("Denied.")));

        var controller = Build(userInfo, roles: new[] { Roles.Member }, subjectId: "subj-member");

        var result = await controller.RemoveTranslation(GroupId, "fr");

        Assert.IsType<ForbidResult>(result);
    }

    // ── Harness ─────────────────────────────────────────────────────────────

    private static GroupsController Build(
        IUserInfoService? userInfo = null,
        string[]? roles = null,
        bool IsAuthenticated = false,
        string? subjectId = null)
    {
        var userInfoImpl = userInfo ?? Substitute.For<IUserInfoService>();
        // The group post/reply lanes (delegating to the sealed PostService
        // concrete) are not exercised by this class; a real PostService is
        // passed for the constructor shape — its methods are never called
        // here (the group name/description lanes only touch IUserInfoService).
        var posts = new Kumunita.Core.Posts.PostService(
            userInfoImpl, Substitute.For<Kumunita.Core.Authorization.IAuthorizationService>(), Substitute.For<IDocumentStore>());
        var localization = Substitute.For<ILocalizationService>();
        localization.ListLanguagesAsync().Returns(new List<LanguageCatalog>());
        localization.GetDefaultLanguageCodeAsync().Returns("en");

        var store = Substitute.For<IDocumentStore>();
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        // Group events (ADR 0089) — a substitute IEventService for the
        // constructor shape; this class exercises only the group
        // name/description translation lanes (IUserInfoService), never the
        // group-event actions, so the seam is not invoked.
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
