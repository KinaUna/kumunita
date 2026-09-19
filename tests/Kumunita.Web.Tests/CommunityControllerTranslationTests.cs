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

    // ── Harness ─────────────────────────────────────────────────────────────

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
