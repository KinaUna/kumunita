using System.Security.Claims;
using Kumunita.Core.Moderation;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// The <see cref="ModerationController"/> Web-boundary seam tests
/// (IMPROVE-CODE C09 — the <c>/moderation</c> surface's raw Marten
/// reads moved behind the Core read seam <see cref="IModerationService"/>,
/// ADR 0006 §D "route + authz + shape"; the controller is the thin HTTP
/// layer that maps the seam's read models onto its view-models + the
/// <see cref="KeyNotFoundException"/> → non-leaky-404 split + the
/// scoped-read 404 (a standing-moderator out of scope, or a plain
/// resident, never sees a report they are not scoped on — C-M3b·4
/// display-level projection)).
/// <para>
/// This is the <b>first</b> test the <see cref="ModerationController"/>
/// has ever had (E12 — the IMPROVE-CODE plan's "add the first xunit test
/// for a controller as its seam moves" requirement). The
/// <b>frozen</b> <see cref="IModerationService"/> seam is substituted
/// with NSubstitute (the controller never re-derives access — the seam
/// is the sole reader; the display-level scope gate is the C-M3b·4
/// projection, not a decision).
/// </para>
/// <para>
/// <b>Pinned tests (the unit plan's Deliverables):</b>
/// </para>
/// <list type="number">
/// <item><b>ModerationController_Resolve_AbsentReport_404</b> —
/// <c>GET /moderation/{id}</c>: the seam's <c>GetDetailAsync</c> throws
/// <see cref="KeyNotFoundException"/> (the report is absent — the "stale
/// form" shape) and the controller maps it to a non-leaky
/// <see cref="NotFoundResult"/> (never 403 — the M16 create-gate posture,
/// mirroring the <see cref="NotesControllerTests"/> precedent).
/// </item>
/// <item><b>ModerationController_Resolve_NonScopedStandingModerator_404</b>
/// — <c>GET /moderation/{id}</c>: a <b>standing-moderator</b> (a
/// <c>Moderator</c> role claim, not a <c>GlobalAdmin</c>) who is
/// <b>not</b> in scope on the report's component (the seam's
/// <c>IsInActorScopeAsync</c> returns <c>false</c>) gets a non-leaky
/// <see cref="NotFoundResult"/> — the C-M3b·4 SoD display-level
/// projection (a standing-moderator sees only the reports on their
/// components with <c>ModeratorAccess</c> ON; the actual write-lane gate
/// is the action-level <c>[Authorize(Roles = GlobalAdmin)]</c> + the
/// Core's <c>CanAsync(Moderate)</c> — the two independent gates).
/// </item>
/// </list>
/// <para>
/// <b>No database, no Testcontainers</b> — a pure NSubstitute seam test
/// (the seam's read / scope decisions are the seam's; this layer pins
/// the controller's 404 mapping + the scoped-read 404, not the seam's
/// gate).
/// </para>
/// </summary>
public sealed class ModerationControllerTests
{
    // ── 1 — ModerationController_Resolve_AbsentReport_404 ─────────────

    /// <summary>
    /// <c>GET /moderation/{id}</c> (the <b>absent-report</b> branch —
    /// the "stale form" shape: the report was deleted between the GET
    /// that rendered the queue and this GET): the seam's
    /// <see cref="IModerationService.GetDetailAsync"/> throws
    /// <see cref="KeyNotFoundException"/> and the controller maps it to
    /// a non-leaky <see cref="NotFoundResult"/> (never 403 — the M16
    /// create-gate posture; the report is absent, not denied). The
    /// <c>IsInActorScopeAsync</c> call is <b>not</b> reached (the 404
    /// fires before the scope check) — the witness below.
    /// </summary>
    [Fact]
    public async Task ModerationController_Resolve_AbsentReport_404()
    {
        const string reportId = "r-does-not-exist";   // absent — the "stale form" shape

        var moderation = Substitute.For<IModerationService>();
        moderation.When(x => x.GetDetailAsync(reportId, Arg.Any<CancellationToken>()))
            .Throws(new KeyNotFoundException($"Report '{reportId}' was not found."));

        var controller = Build(moderation, subjectId: "subj-admin", roles: ["GlobalAdmin"]);
        var result = await controller.Resolve(reportId);

        // M16 create-gate posture — the non-leaky 404 (never 403 — the
        // report is absent, not denied; the controller maps the seam's
        // KeyNotFoundException to a 404, the Web layer's mapping).
        Assert.IsType<NotFoundResult>(result);

        // The scope check is not reached (the 404 fires first) — the
        // witness (the seam is the sole reader; the controller does not
        // re-derive the scope decision).
        await moderation.DidNotReceive().IsInActorScopeAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ── 2 — ModerationController_Resolve_NonScopedStandingModerator_404 ─

    /// <summary>
    /// <c>GET /moderation/{id}</c> (the <b>scoped-read 404</b> — a
    /// standing-moderator who is <b>not</b> in scope on the report's
    /// component, the C-M3b·4 SoD display-level projection): the seam's
    /// <see cref="IModerationService.GetDetailAsync"/> returns the
    /// detail (the report exists), but the seam's
    /// <see cref="IModerationService.IsInActorScopeAsync"/> returns
    /// <c>false</c> (the actor has no <c>ModeratorAssignment</c> row on
    /// the report's <c>ComponentId</c>, or the component's
    /// <c>ModeratorAccess</c> flag is OFF) and the controller maps that
    /// to a non-leaky <see cref="NotFoundResult"/> (never 403 — the M16
    /// create-gate posture; the report is out of the actor's scope, not
    /// denied). The display-level projection is this layer's pin (the
    /// actual write-lane gate is the action-level
    /// <c>[Authorize(Roles = GlobalAdmin)]</c> + the Core's
    /// <c>CanAsync(Moderate)</c> — the two independent gates).
    /// </summary>
    [Fact]
    public async Task ModerationController_Resolve_NonScopedStandingModerator_404()
    {
        const string reportId   = "r-scoped";
        const string actor      = "subj-standing-moderator";
        const string component  = "c-elsewhere";   // a component this actor is not a standing-moderator on

        var moderation = Substitute.For<IModerationService>();
        // The report exists — the detail resolves (the scoped-read gate
        // below is the pin, not the absence).
        moderation.GetDetailAsync(reportId, Arg.Any<CancellationToken>())
            .Returns(new ModerationDetailReadModel
            {
                ReportId    = reportId,
                PostId      = "p-any",
                ComponentId = component,
                Status      = "filed",
                At          = DateTimeOffset.UtcNow,
                Moderators  = [],
            });
        // The actor is a standing-moderator (the <c>Moderator</c> role
        // claim) but is NOT in scope on this report's component — the
        // seam's scope check returns false.
        moderation.IsInActorScopeAsync(actor, component, Arg.Any<CancellationToken>())
            .Returns(false);

        var controller = Build(moderation, subjectId: actor, roles: ["Moderator"]);
        var result = await controller.Resolve(reportId);

        // C-M3b·4 SoD display-level projection — the non-leaky 404 (never
        // 403 — the report is out of the actor's scope, not denied; the
        // controller maps the seam's false scope check to a 404).
        Assert.IsType<NotFoundResult>(result);

        // The scope check was the decision (the seam is the sole reader
        // of the <c>ModeratorAssignment</c> / <c>Component.ModeratorAccess</c>
        // reads — the controller does not re-derive access).
        await moderation.Received(1).IsInActorScopeAsync(
            actor, component, Arg.Any<CancellationToken>());
    }

    // ── Harness ────────────────────────────────────────────────────────

    /// <summary>
    /// Build a <see cref="ModerationController"/> over the
    /// <see cref="IModerationService"/> seam substitute + the actor's
    /// claim set (the <c>subjectId</c> + the <c>roles</c> — the
    /// <c>Kumunita.Sub</c> / <c>Kumunita.Role</c> claims the
    /// <see cref="Kumunita.Web.Security.KumunitaPrincipal"/> helpers read).
    /// A no-op <see cref="ITempDataProvider"/> closes the <c>TempData</c>
    /// bag so the write lanes' redirect-after-write branch doesn't NRE
    /// (<c>DefaultHttpContext</c> leaves <c>Session</c> null by default).
    /// </summary>
    private static ModerationController Build(
        IModerationService moderation,
        string? subjectId = null,
        string[]? roles = null)
    {
        var controller = new ModerationController(moderation);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var claims = new List<Claim>();
        if (subjectId is not null)
            claims.Add(new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, subjectId));
        if (roles is { Length: > 0 })
            claims.AddRange(roles.Select(r => new Claim(Kumunita.Core.Identity.ClaimTypes.Role, r)));

        if (claims.Count > 0)
            controller.ControllerContext.HttpContext.User =
                new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));

        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());
        return controller;
    }

    /// <summary>
    /// A no-op <see cref="ITempDataProvider"/> — the house idiom for closing
    /// the <c>TempData</c> bag in controller tests (the
    /// <see cref="NotesControllerTests"/> / <see cref="BookmarksControllerTests"/>
    /// precedent: <c>DefaultHttpContext</c> leaves <c>Session</c> null by
    /// default, so the <c>TempData</c> bag NREs on the redirect-after-write
    /// branch without a provider; this no-op keeps the assertion target
    /// (the call log / the redirect shape) intact).
    /// </summary>
    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the call log / the redirect shape
        }
    }
}
