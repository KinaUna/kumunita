using System.Security.Claims;
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
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// ADR 0048 — Web-side shape for the community translation edit/delete
/// lanes. Standing is GlobalAdmin ∪ Translator (via
/// <see cref="IUserInfoService.UpdateCommunityTranslationAsync"/> /
/// <see cref="IUserInfoService.RemoveCommunityTranslationAsync"/>); the Web
/// layer is thin:
/// <list type="bullet">
/// <item>Anonymous → 403 <c>Forbid</c> (no subject id).</item>
/// <item>Service throws <see cref="UnauthorizedAccessException"/> → 403
///       (denied standing).</item>
/// <item>Service throws <see cref="KeyNotFoundException"/> → 404
///       (missing row).</item>
/// <item>Happy path → 302 <c>Redirect</c> to the manage page.</item>
/// </list>
/// </summary>
public class CommunityControllerTranslationTests
{
    private const string CompId = "comp-001";

    [Fact]
    public async Task UpdateTranslation_GlobalAdmin_HappyPath_Redirects()
    {
        var userInfo = Substitute.For<IUserInfoService>();

        var controller = Build(userInfo, roles: new[] { Roles.GlobalAdmin }, subjectId: "subj-admin");

        var result = await controller.UpdateTranslation(CompId, "fr", "Nouveau", "Desc");

        Assert.IsType<RedirectToActionResult>(result);
        await userInfo.Received(1).UpdateCommunityTranslationAsync(
            CompId, "fr", "Nouveau", "Desc", "subj-admin",
            Arg.Is<IReadOnlySet<string>>(s => s.Contains(Roles.GlobalAdmin)), Arg.Any<IDocumentSession>());
    }

    [Fact]
    public async Task UpdateTranslation_Translator_HappyPath_Redirects()
    {
        var userInfo = Substitute.For<IUserInfoService>();

        var controller = Build(userInfo, roles: new[] { Roles.Translator }, subjectId: "subj-translator");

        var result = await controller.UpdateTranslation(CompId, "fr", "Nouveau", "Desc");

        Assert.IsType<RedirectToActionResult>(result);
        await userInfo.Received(1).UpdateCommunityTranslationAsync(
            CompId, "fr", "Nouveau", "Desc", "subj-translator",
            Arg.Is<IReadOnlySet<string>>(s => s.Contains(Roles.Translator)), Arg.Any<IDocumentSession>());
    }

    [Fact]
    public async Task UpdateTranslation_Member_Forbid()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.UpdateCommunityTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(Task.FromException<CommunityTranslation>(new UnauthorizedAccessException("Denied.")));

        var controller = Build(userInfo, roles: new[] { Roles.Member }, subjectId: "subj-member");

        var result = await controller.UpdateTranslation(CompId, "fr", "X", "Desc");

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task UpdateTranslation_MissingRow_NotFound()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.UpdateCommunityTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(Task.FromException<CommunityTranslation>(new KeyNotFoundException("No row.")));

        var controller = Build(userInfo, roles: new[] { Roles.GlobalAdmin }, subjectId: "subj-admin");

        var result = await controller.UpdateTranslation(CompId, "fr", "X", "Desc");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateTranslation_Anonymous_Forbid_NoWrite()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var controller = Build(userInfo, IsAuthenticated: false);

        var result = await controller.UpdateTranslation(CompId, "fr", "X", "Desc");

        Assert.IsType<ForbidResult>(result);
        await userInfo.DidNotReceive().UpdateCommunityTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>());
    }

    [Fact]
    public async Task RemoveTranslation_GlobalAdmin_HappyPath_Redirects()
    {
        var userInfo = Substitute.For<IUserInfoService>();

        var controller = Build(userInfo, roles: new[] { Roles.GlobalAdmin }, subjectId: "subj-admin");

        var result = await controller.RemoveTranslation(CompId, "fr");

        Assert.IsType<RedirectToActionResult>(result);
        await userInfo.Received(1).RemoveCommunityTranslationAsync(
            CompId, "fr", "subj-admin",
            Arg.Is<IReadOnlySet<string>>(s => s.Contains(Roles.GlobalAdmin)), Arg.Any<IDocumentSession>());
    }

    [Fact]
    public async Task RemoveTranslation_Translator_HappyPath_Redirects()
    {
        var userInfo = Substitute.For<IUserInfoService>();

        var controller = Build(userInfo, roles: new[] { Roles.Translator }, subjectId: "subj-translator");

        var result = await controller.RemoveTranslation(CompId, "fr");

        Assert.IsType<RedirectToActionResult>(result);
        await userInfo.Received(1).RemoveCommunityTranslationAsync(
            CompId, "fr", "subj-translator",
            Arg.Is<IReadOnlySet<string>>(s => s.Contains(Roles.Translator)), Arg.Any<IDocumentSession>());
    }

    [Fact]
    public async Task RemoveTranslation_Member_Forbid()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.RemoveCommunityTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(Task.FromException(new UnauthorizedAccessException("Denied.")));

        var controller = Build(userInfo, roles: new[] { Roles.Member }, subjectId: "subj-member");

        var result = await controller.RemoveTranslation(CompId, "fr");

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task RemoveTranslation_MissingRow_NotFound()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.RemoveCommunityTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(Task.FromException(new KeyNotFoundException("No row.")));

        var controller = Build(userInfo, roles: new[] { Roles.GlobalAdmin }, subjectId: "subj-admin");

        var result = await controller.RemoveTranslation(CompId, "fr");

        Assert.IsType<NotFoundResult>(result);
    }

    // ── GET Translations (ADR 0053) — the dedicated surface ────────────────

    [Fact]
    public async Task Translations_Translator_CanTranslateTrue_View()
    {
        var userInfo = BuildForGet(canTranslate: true, component: true);

        var controller = Build(userInfo, roles: new[] { Roles.Translator }, subjectId: "subj-translator");

        var result = await controller.Translations(CompId, TestContext.Current.CancellationToken);

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<CommunityTranslationViewModel>(view.Model);
        Assert.True(((CommunityTranslationViewModel)view.Model!).CanTranslate);
    }

    [Fact]
    public async Task Translations_GlobalAdmin_CanTranslateTrue_View()
    {
        var userInfo = BuildForGet(canTranslate: true, component: true);

        var controller = Build(userInfo, roles: new[] { Roles.GlobalAdmin }, subjectId: "subj-admin");

        var result = await controller.Translations(CompId, TestContext.Current.CancellationToken);

        var view = Assert.IsType<ViewResult>(result);
        Assert.True(((CommunityTranslationViewModel)view.Model!).CanTranslate);
    }

    [Fact]
    public async Task Translations_ComponentModerator_ViewOnly_CanTranslateFalse()
    {
        // The manage standing (moderator) admits the page, but the translation
        // standing is denied — CanTranslate must be false (view-only).
        var userInfo = BuildForGet(canTranslate: false, component: true);

        var controller = Build(userInfo,
            roles: new[] { Roles.ModeratorComponent(CompId) }, subjectId: "subj-mod");

        var result = await controller.Translations(CompId, TestContext.Current.CancellationToken);

        var view = Assert.IsType<ViewResult>(result);
        var vm = (CommunityTranslationViewModel)view.Model!;
        Assert.False(vm.CanTranslate);
        Assert.Equal(CompId, vm.ComponentId);
    }

    [Fact]
    public async Task Translations_PlaneMember_NoStanding_NotFound()
    {
        // Neither manage nor translation standing — fail-closed 404, and the
        // read seam is never reached (the page gate is the reach decision).
        var userInfo = BuildForGet(canTranslate: false, component: true);

        var controller = Build(userInfo, roles: new[] { Roles.Member }, subjectId: "subj-member");

        var result = await controller.Translations(CompId, TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
        await userInfo.DidNotReceive().GetCommunityTranslationsAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Translations_UnknownCommunity_NotFound()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetComponentsAsync(enabledOnly: false).Returns(new List<Component>());

        var controller = Build(userInfo, roles: new[] { Roles.GlobalAdmin }, subjectId: "subj-admin");

        var result = await controller.Translations(CompId, TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
    }

    // ── Harness ─────────────────────────────────────────────────────────────

    private static IUserInfoService BuildForGet(bool canTranslate, bool component)
    {
        var userInfo = Substitute.For<IUserInfoService>();
        if (component)
        {
            userInfo.GetComponentsAsync(enabledOnly: false).Returns(new List<Component>
            {
                new() { Id = CompId, Name = "The Club", Enabled = true },
            });
        }
        userInfo.CanTranslateCommunity(Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>()).Returns(canTranslate);
        userInfo.GetCommunityTranslationsAsync(CompId).Returns(new List<CommunityTranslation>());
        return userInfo;
    }

    private static CommunityController Build(
        IUserInfoService userInfo,
        string[]? roles = null,
        bool IsAuthenticated = false,
        string? subjectId = null)
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.ListLanguagesAsync().Returns(new List<LanguageCatalog>());

        var store = Substitute.For<IDocumentStore>();
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        var controller = new CommunityController(userInfo, localization, store);
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
