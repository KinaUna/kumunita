using Kumunita.Core.Documents;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// C12 — the <see cref="DocumentFolderController"/> seam (ADR 0122 / ADR 0125).
/// The documents-organization folder CRUD + document-move routes. The
/// <c>Index</c> action is the testable seam: an anonymous visitor (no
/// <c>SubjectId</c> claim) gets <c>401 Unauthorized</c> before the folder tree
/// is read — the ADR 0122 D7 posture (the form's existence is not leaked to
/// an unauthenticated caller).
/// </summary>
public sealed class DocumentFolderControllerTests
{
    private static DocumentFolderController BuildController(bool signedIn)
    {
        // DocumentFolderService is sealed (not substitutable) — construct a
        // real one over a substitute store. It is never touched on the
        // anonymous 401 path the test exercises, so the backing store is a no-op.
        var store = NSubstitute.Substitute.For<Marten.IDocumentStore>();
        var folders = new DocumentFolderService(store);
        var controller = new DocumentFolderController(
            folders: folders,
            store: store);

        var httpContext = new DefaultHttpContext();
        if (signedIn)
        {
            httpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    new[] { new System.Security.Claims.Claim(Kumunita.Core.Identity.ClaimTypes.Subject, "actor-1") },
                    "test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    [Fact]
    public async Task Index_AnonymousVisitor_ReturnsUnauthorized()
    {
        // The folder tree is a signed-in surface; an anonymous caller is
        // rejected before the service is touched.
        var controller = BuildController(signedIn: false);
        var action = await controller.Index();
        Assert.IsType<UnauthorizedResult>(action);
    }
}
