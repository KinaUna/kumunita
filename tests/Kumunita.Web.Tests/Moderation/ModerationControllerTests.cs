using System.Security.Claims;
using Kumunita.Core.Moderation;
using Kumunita.Core.Posts;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Primitives;
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
/// <item><b>ModerationController_ResolvePost_OutcomeAndNote_PassThrough</b>
/// — <c>POST /moderation/{id}/resolve</c> (IMPROVE-UX R04): the form
/// carries <c>outcome=Hidden</c> + a <c>resolutionNote</c> and the
/// controller passes the parsed <see cref="ReportOutcome.Hidden"/> +
/// the note verbatim to the R03 seam (the pass-through, not the seam's
/// behavior — the R03 Core tests pin the seam).
/// </item>
/// <item><b>ModerationController_ResolvePost_EmptyForm_PreR04Shape</b>
/// — <c>POST /moderation/{id}/resolve</c> (IMPROVE-UX R04): an empty
/// form (no outcome, no note) calls the seam with both
/// <c>null</c> — the pre-R04 "no outcome recorded" shape.
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

    // ── 3 — ModerationController_ResolvePost_OutcomeAndNote_PassThrough ─

    /// <summary>
    /// <c>POST /moderation/{id}/resolve</c> (IMPROVE-UX R04 — the
    /// resolve form's outcome + note pass-through): the form carries
    /// <c>outcome=Hidden</c> + a non-empty <c>resolutionNote</c> and
    /// the controller passes the parsed <see cref="ReportOutcome.Hidden"/>
    /// + the note verbatim to the R03 seam's
    /// <see cref="IModerationService.ResolveReportAsync"/>
    /// (the pass-through, not the seam's behavior — the R03 Core
    /// tests pin what the seam does with those values). A successful
    /// resolve redirects to the queue (the write-lane
    /// redirect-after-write shape).
    /// </summary>
    [Fact]
    public async Task ModerationController_ResolvePost_OutcomeAndNote_PassThrough()
    {
        const string reportId      = "r-resolve-outcome";
        const string admin         = "subj-admin";
        const string note          = "The post was hidden at the author's request.";

        var moderation = Substitute.For<IModerationService>();
        var controller = Build(moderation, subjectId: admin, roles: ["GlobalAdmin"]);

        // The form fields the resolve form posts (the R04 shape:
        // outcome is the enum's string name, resolutionNote is the
        // free-text note).
        controller.HttpContext.Request.Form = new FormCollection(
            new Dictionary<string, StringValues>
            {
                ["outcome"]        = new StringValues("Hidden"),
                ["resolutionNote"] = new StringValues(note),
            });

        var result = await controller.ResolvePost(reportId);

        // The successful resolve redirects to the queue (the write-lane
        // redirect-after-write shape — the same Redirect("/moderation")
        // the pre-R04 shape used).
        Assert.IsType<RedirectResult>(result);

        // The pass-through: the outcome is parsed to ReportOutcome.Hidden,
        // the note is passed verbatim (the R03 lane's ≤500-cap helper is
        // the second line of defense — this note is within the cap, so
        // the controller's job is to forward it unchanged).
        await moderation.Received(1).ResolveReportAsync(
            reportId:       reportId,
            globalAdminId:  admin,
            outcome:        ReportOutcome.Hidden,
            resolutionNote: note,
            ct:             Arg.Any<CancellationToken>());
    }

    // ── 4 — ModerationController_ResolvePost_EmptyForm_PreR04Shape ────

    /// <summary>
    /// <c>POST /moderation/{id}/resolve</c> (IMPROVE-UX R04 — the
    /// pre-R04 "no outcome recorded" shape): an empty form (no
    /// <c>outcome</c>, no <c>resolutionNote</c> — the user left both
    /// blank) calls the R03 seam with both optional parameters
    /// <c>null</c> — the same call the seam received before R04
    /// (the two-argument shape still compiles because the R03
    /// parameters are optional; R04's controller change is what
    /// populates them when the form supplies them).
    /// </summary>
    [Fact]
    public async Task ModerationController_ResolvePost_EmptyForm_PreR04Shape()
    {
        const string reportId = "r-resolve-empty";
        const string admin    = "subj-admin";

        var moderation = Substitute.For<IModerationService>();
        var controller = Build(moderation, subjectId: admin, roles: ["GlobalAdmin"]);

        // An empty form — no outcome, no note (the user left both blank).
        controller.HttpContext.Request.Form = new FormCollection(
            new Dictionary<string, StringValues>());

        var result = await controller.ResolvePost(reportId);

        // The successful resolve redirects to the queue.
        Assert.IsType<RedirectResult>(result);

        // The pre-R04 shape: both optional parameters are null (the
        // controller maps the blank form fields to null — the outcome's
        // unrecognized/empty parse → null, the note's empty/whitespace →
        // null — the R03 seam's "no outcome recorded" path).
        await moderation.Received(1).ResolveReportAsync(
            reportId:       reportId,
            globalAdminId:  admin,
            outcome:        null,
            resolutionNote: null,
            ct:             Arg.Any<CancellationToken>());
    }

    // ── A06 — the moderation nav badge render (view-source pin) ────────────

    /// <summary>
    /// IMPROVE-UX A06 (the E3 "new reports reach no moderator" finding's
    /// visible loop-closer, the badge half): the <c>_AccountNav</c> partial
    /// carries the moderation nav badge's count span
    /// (<c>class="kmb-nav-badge"</c>) on the Moderation menu item — rendered
    /// <b>only when</b> <c>navModerationOpenCount &gt; 0</c> (the A06
    /// "0 hides" pin — a 0-badge is a noise path, the <c>@if</c> guard
    /// keeps it out). The count comes from
    /// <c>IModerationService.CountOpenReportsAsync</c> (the Core read seam —
    /// ADR 0006-D holds: the cache is Web-layer, the seam stays pure),
    /// read through the instance-level <c>IMemoryCache</c> keyed by
    /// (actorId, isGlobalAdmin, isStanding) with a 60 s sliding window
    /// (the A06 "cached ≤60 s" pin — the badge does not double the queue's
    /// DB load). Both Moderation menu items (the GlobalAdmin branch at
    /// the <c>/admin/error-reports</c> sibling + the standing-moderator
    /// branch) carry the same badge span — the scope is by construction
    /// (a plain resident sees neither row, so no extra guard is needed).
    /// The queue's friendly empty state is a separate view pin
    /// (<see cref="A06_Moderation_Index_EmptyState_KmbEmptyShape"/>) — the
    /// R29–R33 batch's last plain empty state (the R33 "Moderation/Index
    /// is A06's" pin).
    /// </summary>
    [Fact]
    public void A06_AccountNav_ModerationBadge_GuardedOnCountGreaterThanZero()
    {
        var nav = ReadViewSource("Shared", "_AccountNav.cshtml");

        // The badge span is on the Moderation menu item (both branches —
        // the GlobalAdmin branch and the standing-moderator branch carry
        // the same href + the same badge shape).
        Assert.Contains("href=\"/moderation\"", nav, StringComparison.Ordinal);
        Assert.Contains("kmb-nav-badge", nav, StringComparison.Ordinal);
        Assert.Contains("data-nav-badge=\"moderation\"", nav, StringComparison.Ordinal);

        // The A06 "0 hides" pin: the badge span is wrapped in an
        // @if (navModerationOpenCount > 0) guard — a 0-badge is a noise
        // path. The guard is present before the span (the Razor conditional
        // wraps the span, not the other way round).
        Assert.Contains(
            "navModerationOpenCount > 0",
            nav, StringComparison.Ordinal);

        // The count comes from the Core read seam
        // (IModerationService.CountOpenReportsAsync — the A06 count-seam
        // shape, the same scoped query the queue uses) read through the
        // Web-layer IMemoryCache (the 60 s sliding window — the A06
        // "cached ≤60 s" pin).
        Assert.Contains(
            "Moderation.CountOpenReportsAsync",
            nav, StringComparison.Ordinal);
        Assert.Contains(
            "TimeSpan.FromSeconds(60)",
            nav, StringComparison.Ordinal);
        Assert.Contains(
            "SlidingExpiration",
            nav, StringComparison.Ordinal);

        // The cache key is scoped per (actorId, isGlobalAdmin, isStanding)
        // — the A06 "per (actor, standing)" pin.
        Assert.Contains(
            "moderation.open-badge:",
            nav, StringComparison.Ordinal);
    }

    /// <summary>
    /// IMPROVE-UX A06 (the queue's friendly empty state — the A06 DoD's
    /// second half, the R33 "Moderation/Index is A06's" pin):
    /// <c>Views/Moderation/Index.cshtml</c>'s
    /// <c>@if (Model.TotalCount == 0)</c> branch renders the
    /// <c>.kmb-empty</c> shape (the R29–R33 batch's reference, the ADR 0177
    /// shape) — a <c>kmb-empty-title</c> wrapping the existing
    /// <c>moderation.empty</c> key + a <c>kmb-empty-body</c> wrapping the
    /// new <c>moderation.empty_body</c> key (the one new translation key ×
    /// en/de/fr/da, the parity pin). The bare
    /// <c>alert alert-secondary</c> it previously rendered is gone (the
    /// <c>ux-measure.py</c> <c>empty_plain</c> count drops by 1).
    /// </summary>
    [Fact]
    public void A06_Moderation_Index_EmptyState_KmbEmptyShape()
    {
        var view = ReadViewSource("Moderation", "Index.cshtml");

        // The .kmb-empty shape (the ADR 0177 reference — the R29–R33
        // batch's friendly empty state).
        Assert.Contains("kmb-empty", view, StringComparison.Ordinal);
        Assert.Contains("kmb-empty-title", view, StringComparison.Ordinal);
        Assert.Contains("kmb-empty-body", view, StringComparison.Ordinal);

        // The existing moderation.empty title key (reused, not new) + the
        // new moderation.empty_body key (the one new translation key).
        Assert.Contains("moderation.empty", view, StringComparison.Ordinal);
        Assert.Contains("moderation.empty_body", view, StringComparison.Ordinal);

        // The bare alert-secondary it previously rendered is gone (the
        // ux-measure.py empty_plain count drops by 1 — the R33 pin).
        Assert.DoesNotContain(
            "alert alert-secondary",
            view, StringComparison.Ordinal);
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
    /// Reads a view's source by walking up from the test's working
    /// directory to the repo root (the <c>Kumunita.slnx</c> file) — the
    /// same pattern <see cref="GroupMessagesControllerTests.ReadViewSource"/>
    /// / <see cref="MessagesControllerTests.ReadViewSource"/> use for their
    /// <c>_AccountNav.cshtml</c> pin.
    /// </summary>
    private static string ReadViewSource(string dir, string file)
    {
        var dirInfo = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dirInfo is not null && !File.Exists(Path.Combine(dirInfo.FullName, "Kumunita.slnx")))
            dirInfo = dirInfo.Parent;
        Assert.NotNull(dirInfo);
        var path = Path.Combine(dirInfo!.FullName, "src", "Kumunita.Web", "Views", dir, file);
        Assert.True(File.Exists(path), $"view {path} not found");
        return File.ReadAllText(path);
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
