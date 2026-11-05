using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Moderation;
using Kumunita.Core.Posts;
using Kumunita.Core.Projects;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// C12 — the <see cref="ReportTargetsController"/> seam (ADR 0173). The
/// resident-facing report intake for the five target kinds (event / project /
/// board / to-do / group post). The testable seam is the empty-id guard: an
/// empty <c>id</c> route value maps to <c>404 NotFound</c> before any read
/// decision or the <c>ModerationService.FileReportTargetAsync</c> write lane is
/// reached.
/// <para>
/// <see cref="PostService"/> and <see cref="ModerationService"/> are <c>sealed</c>
/// (not NSubstitutable), so a real instance of each is constructed over a
/// substitute <c>IDocumentStore</c> — neither is touched on the empty-id path.
/// </para>
/// </summary>
public sealed class ReportTargetsControllerTests
{
    private static ReportTargetsController BuildController()
    {
        var store = NSubstitute.Substitute.For<Marten.IDocumentStore>();
        var moderation = new ModerationService(
            NSubstitute.Substitute.For<IProfileSettingsService>(),
            NSubstitute.Substitute.For<IAuthorizationService>(),
            store);
        var posts = new PostService(
            NSubstitute.Substitute.For<IUserInfoService>(),
            NSubstitute.Substitute.For<IAuthorizationService>(),
            store);

        var controller = new ReportTargetsController(
            store: store,
            moderation: moderation,
            events: NSubstitute.Substitute.For<IEventService>(),
            projects: NSubstitute.Substitute.For<IProjectService>(),
            posts: posts,
            boards: NSubstitute.Substitute.For<IBoardService>(),
            todos: NSubstitute.Substitute.For<ITodoService>());

        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim(Kumunita.Core.Identity.ClaimTypes.Subject, "actor-1") },
                "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    [Fact]
    public async Task ReportEvent_EmptyId_ReturnsNotFound()
    {
        // The empty-id guard fires before the event's Read decision or the
        // write lane — a uniform 404 for the missing-target case.
        var controller = BuildController();
        var action = await controller.ReportEvent(id: "", reason: "spam");
        Assert.IsType<NotFoundResult>(action);
    }
}
