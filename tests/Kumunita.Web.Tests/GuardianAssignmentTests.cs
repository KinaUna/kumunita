using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// GA (ADR 0038 — guardian assignment: an existing guardian assigns a second
/// guardian to a child's account) + GA-AR (ADR 0038 §Amendment (2026-09-17,
/// second) — the second audit row) — the lane's <b>6 pinned Web controller
/// tests</b> for <see cref="GuardianController.Assign"/> (POST
/// <c>me/children/{childId}/assign</c>). These are the <c>Pinned contract →
/// Pinned seam tests (exact names)</c> #4–#9 frozen in
/// <c>docs/design/guardian-assignment-design.md</c> (U01, extended by the
/// GA-AR rename + add), asserting U05's action (the standing gate G-A·1, the
/// resolution G-A·2, the self-assignment refusal G-A·5, the idempotent no-op
/// G-A·4, the happy-path <c>AssignGuardianLinkAsync</c> call, and the
/// <c>guardian.assign</c> conferral row).
/// <para>
/// These are <b>integration</b> tests (not NSubstitute-only): the
/// <c>Assign</c> action's standing gate (<c>ActiveLinkAsync</c>) and the
/// happy-path <c>CreateGuardianLinkAsync</c> seam both run real Marten SQL —
/// Marten 9.x's <c>FirstOrDefaultAsync</c>/<c>ToListAsync</c> are extension
/// methods that cast to the internal <c>MartenLinqQueryable</c> and execute a
/// live query, so they cannot be NSubstituted. The pinned tests also assert
/// <b>real</b> <c>GuardianLink</c> rows + <b>real</b> <c>guardian.create</c>
/// audit rows, which only a live store can produce. Each test hands itself a
/// fresh scratch Postgres DB (<see cref="PostgresFixture.NewDatabaseAsync"/>),
/// the <c>BootStoreAsync</c> shape the GU lane established in
/// <c>Kumunita.Core.Tests</c>.
/// <para>
/// <b>Resolved (GA-AR, 2026-09-17):</b> the U06 drift pause (the pinned
/// contract §D / test #8 said <c>ActorId</c> = the <b>assigning</b> guardian,
/// but the byte-identical GU seam <c>CreateGuardianLinkAsync</c> set
/// <c>ActorId</c> = the <b>assigned</b> guardian) is now <b>met</b> by the
/// GA-AR seam <c>AssignGuardianLinkAsync</c>: it writes <b>two</b> audit rows
/// — <c>guardian.create</c> (<c>ActorId</c> = the assigned guardian, the GU
/// seam's shape) + <c>guardian.assign</c> (<c>ActorId</c> = the assigning
/// guardian / conferrer). The §D prose pin and the code pin are no longer
/// mutually unsatisfiable.
/// A test whose exact name is not in the design doc's pinned list is a drift
/// pause, not a silent add.
/// </para>
/// </summary>
public sealed class GuardianAssignmentTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Guardian = "ga-guardian-001";
    private const string Child = "ga-child-001";
    private const string SecondGuardian = "ga-second-guardian-001";
    private const string NewGuardian = "ga-new-guardian-001";
    private const string NonGuardian = "ga-non-guardian-001";

    // ── 4 — G-A·1 — a non-guardian cannot assign (404) ────────────────────
    // The standing gate (ActiveLinkAsync) runs first: no active GuardianLink
    // for (actor, child) → null → 404. The ADR 0012/0013 "a non-guardian
    // learns nothing" shape.

    [Fact]
    public async Task Assign_NonGuardian_Returns404()
    {
        // The actor is a NON-guardian (no active link over the child). The
        // standing gate (ActiveLinkAsync) returns null → 404, before the
        // email resolution even runs.
        var (controller, _, _) = await BuildAsync(
            actorSubjectId: NonGuardian,
            seedGuardianLink: false);

        var result = await controller.Assign(
            Child,
            new AssignGuardianForm { Email = "someone@example.com" });

        Assert.IsType<NotFoundResult>(result);
    }

    // ── 5 — G-A·2 — an unknown email is refused (user-presentable error) ──
    // The resolution (FindSubjectByEmailAsync) returns null for an unknown
    // email; the action surfaces "No account with that email." on the form's
    // ModelState, never a 500, never an auto-create (the GU lane's G·4
    // "formation is creation-based" precedent).

    [Fact]
    public async Task Assign_UnknownEmail_ReturnsValidationError()
    {
        // The actor is the guardian (an active link over the child is seeded),
        // so the standing gate passes. The email is unknown → the resolution
        // returns null → the form's error surface.
        var (controller, _, _) = await BuildAsync();

        var result = await controller.Assign(
            Child,
            new AssignGuardianForm { Email = "unknown@example.com" });

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Detail", view.ViewName);
        Assert.True(view.ViewData.ModelState.ContainsKey(string.Empty));
        var errors = view.ViewData.ModelState[string.Empty]!.Errors;
        Assert.Contains(errors, e => e.ErrorMessage == "No account with that email.");
    }

    // ── 6 — G-A·5 — self-assignment is refused (user-presentable error) ──
    // The resolution resolves the actor's own email back to their own subject
    // id; assignedId == subject → "You are already this child's guardian."
    // on the form's ModelState. A no-op, not a useful act — the GU formation
    // lane's territory.

    [Fact]
    public async Task Assign_SelfAssignment_ReturnsValidationError()
    {
        // The actor is the guardian (an active link over the child is seeded).
        // The email resolves to the guardian's OWN subject id (self-assignment).
        var (controller, identity, _) = await BuildAsync();
        identity.FindSubjectByEmailAsync("ga-guardian@example.com")
            .Returns(Task.FromResult<string?>(Guardian));

        var result = await controller.Assign(
            Child,
            new AssignGuardianForm { Email = "ga-guardian@example.com" });

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Detail", view.ViewName);
        Assert.True(view.ViewData.ModelState.ContainsKey(string.Empty));
        var errors = view.ViewData.ModelState[string.Empty]!.Errors;
        Assert.Contains(errors, e => e.ErrorMessage == "You are already this child's guardian.");
    }

    // ── 7 — G-A·4 — a duplicate assignment is a no-op (no second audit row) ─
    // The CreateGuardianLinkAsync seam is idempotent for the (guardianId,
    // childId) pair: a duplicate active row is returned as-is, no new row,
    // no second audit row. The action then sets TempData["info"] and
    // redirects (the "success" path — the no-op is not an error).

    [Fact]
    public async Task Assign_DuplicateAssignment_IsIdempotentNoOp()
    {
        // The actor is the guardian (an active link over the child is seeded).
        // A SECOND active GuardianLink for (secondGuardian, child) already
        // exists — the duplicate case.
        var (controller, identity, store) = await BuildAsync();
        var ct = TestContext.Current.CancellationToken;
        await SeedGuardianLinkAsync(store, SecondGuardian, Child, ct);

        // The email resolves to the second guardian's subject id (which
        // already has an active link over the child).
        identity.FindSubjectByEmailAsync("second@example.com")
            .Returns(Task.FromResult<string?>(SecondGuardian));

        var result = await controller.Assign(
            Child,
            new AssignGuardianForm { Email = "second@example.com" });

        // The no-op is a "success" — the action redirects (not a form re-render).
        Assert.IsType<RedirectToActionResult>(result);

        // The second guardian has exactly one link row for the child (the
        // pre-seeded one — CreateGuardianLinkAsync's no-op created no second
        // row), and no guardian.create audit row for that link (the no-op
        // wrote nothing). The guardian's own link (seeded by BuildAsync) was
        // also seeded directly, not via CreateGuardianLinkAsync, so the
        // total guardian.create count for this child is therefore 0.
        await using var session = store.QuerySession();
        var secondLinkCount = await session.Query<GuardianLink>()
            .Where(l => l.GuardianId == SecondGuardian && l.ChildId == Child)
            .CountAsync(ct);
        Assert.Equal(1, secondLinkCount);

        var auditCount = await CountAuditAsync(store, "guardian.create", Child, ct);
        Assert.Equal(0, auditCount);
    }

    // ── 8 — happy path — AssignGuardianLinkAsync is called, new row + 2 audit ─
    // A guardian assigns a known, non-self, non-duplicate email → the
    // AssignGuardianLinkAsync seam (GA-AR, ADR 0038 §Amendment
    // (2026-09-17, second)) creates a new GuardianLink row (Active) + TWO
    // audit rows in one commit (C3 — S·1): guardian.create [ActorId = the
    // ASSIGNED guardian, the GU seam's shape] + guardian.assign [ActorId =
    // the ASSIGNING guardian / conferrer] (S·5). The action redirects to
    // Detail. (Resolves the U06 drift pause: the §D prose pin "ActorId =
    // the assigning guardian" is now met by the guardian.assign row.)

    [Fact]
    public async Task Assign_KnownEmail_CallsAssignGuardianLinkAsync()
    {
        // The actor is the guardian (an active link over the child is seeded).
        // The email resolves to a NEW guardian (non-self, non-duplicate).
        var (controller, identity, store) = await BuildAsync();
        var ct = TestContext.Current.CancellationToken;
        identity.FindSubjectByEmailAsync("new@example.com")
            .Returns(Task.FromResult<string?>(NewGuardian));

        var result = await controller.Assign(
            Child,
            new AssignGuardianForm { Email = "new@example.com" });

        // The happy path redirects to Detail (not a form re-render).
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Detail", redirect.ActionName);

        // A new GuardianLink row for (newGuardian, child) with Active status.
        await using var session = store.QuerySession();
        var newLink = await session.Query<GuardianLink>()
            .Where(l => l.GuardianId == NewGuardian && l.ChildId == Child)
            .FirstOrDefaultAsync(ct);
        Assert.NotNull(newLink);
        Assert.Equal(GuardianLinkStatus.Active, newLink!.Status);

        // Two audit rows target the new link (S·1 — one commit, two rows):
        // (1) guardian.create — ActorId = the ASSIGNED guardian (S·5).
        var createRow = await LastAuditAsync(store, "guardian.create", newLink.Id, ct);
        Assert.Equal(NewGuardian, createRow.ActorId);          // the assigned guardian
        Assert.Equal(AccessVia.Guardian, createRow.Via);

        // (2) guardian.assign — ActorId = the ASSIGNING guardian (S·5).
        var assignRow = await LastAuditAsync(store, "guardian.assign", newLink.Id, ct);
        Assert.Equal(Guardian, assignRow.ActorId);             // the assigning guardian (the test's actor)
        Assert.Equal(AccessVia.Guardian, assignRow.Via);

        Assert.Equal(newLink.Id, createRow.TargetId);
        Assert.Equal(newLink.Id, assignRow.TargetId);
        Assert.Equal("guardian-link", assignRow.TargetKind);
    }

    // ── 8a — GA-AR (2026-09-17) — the guardian.assign row's ActorId is the
    //      ASSIGNING guardian (the conferrer), NOT the assigned guardian.
    //      This is the §D named legibility limitation (ADR 0038, first
    //      amendment) now resolved: the conferrer is on the audit trail.

    [Fact]
    public async Task Assign_KnownEmail_WritesAssignAuditRow()
    {
        // The actor is the guardian (an active link over the child is seeded).
        // The email resolves to a NEW guardian (non-self, non-duplicate).
        var (controller, identity, store) = await BuildAsync();
        var ct = TestContext.Current.CancellationToken;
        identity.FindSubjectByEmailAsync("new@example.com")
            .Returns(Task.FromResult<string?>(NewGuardian));

        var result = await controller.Assign(
            Child,
            new AssignGuardianForm { Email = "new@example.com" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Detail", redirect.ActionName);

        // The new link row exists (the seam's write, S·1).
        await using var session = store.QuerySession();
        var newLink = await session.Query<GuardianLink>()
            .Where(l => l.GuardianId == NewGuardian && l.ChildId == Child)
            .FirstOrDefaultAsync(ct);
        Assert.NotNull(newLink);

        // S·5 — the guardian.assign row's ActorId is the ASSIGNING guardian
        // (the actor, the test's Guardian constant), NOT the assigned
        // guardian (NewGuardian). This is the conferral legibility the
        // GA-AR lane adds (ADR 0038 §D's named limitation, resolved).
        var assignRow = await LastAuditAsync(store, "guardian.assign", newLink!.Id, ct);
        Assert.Equal(Guardian, assignRow.ActorId);             // the assigning guardian (the actor)
        Assert.Equal(Guardian, assignRow.EffectivePrincipalId);
        Assert.NotEqual(NewGuardian, assignRow.ActorId);       // NOT the assigned guardian
        Assert.Equal("guardian-link", assignRow.TargetKind);
        Assert.Equal(newLink.Id, assignRow.TargetId);
        Assert.Equal(AccessVia.Guardian, assignRow.Via);
        Assert.Equal(AccessOutcome.Allow, assignRow.Outcome);
    }

    // ── Shared harness ─────────────────────────────────────────────────────

    /// <summary>
    /// Boots the real Marten store (the GU lane's <c>BootStoreAsync</c> shape)
    /// + a real <see cref="UserInfoService"/> + an NSubstitute
    /// <see cref="IIdentityService"/> (only <c>FindSubjectByEmailAsync</c>
    /// is called by the <c>Assign</c> action — all other members are unused)
    /// + a <see cref="GuardianController"/> wired with the real store, the
    /// real <c>UserInfoService</c>, and the substitute <c>IIdentityService</c>.
    /// The actor's subject id is set via the <c>Kumunita.Sub</c> claim (the
    /// <see cref="KumunitaPrincipal.SubjectId"/> read). By default the
    /// guardian's own active <c>GuardianLink</c> over the child is seeded so
    /// the standing gate (G-A·1) passes for the guardian's tests (5–8). The
    /// non-guardian test (4) passes a different subject id with no link.
    /// </summary>
    private async Task<(GuardianController controller, IIdentityService identity, IDocumentStore store)> BuildAsync(
        string actorSubjectId = Guardian,
        bool seedGuardianLink = true)
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);

        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        // Seed the guardian's own active link (so the standing gate passes for
        // tests 5–8). The non-guardian test (4) passes seedGuardianLink: false.
        if (seedGuardianLink)
        {
            await SeedGuardianLinkAsync(store, Guardian, Child, ct);
        }

        // The real UserInfoService (the CreateGuardianLinkAsync seam runs
        // against the real store — the happy-path test asserts the real
        // GuardianLink row + the real guardian.create audit row).
        var userInfo = new UserInfoService(store);

        // The IIdentityService — NSubstitute (only FindSubjectByEmailAsync is
        // called by Assign; all other members are unused in these tests).
        var identity = Substitute.For<IIdentityService>();
        // Default: unknown email → null (the G-A·2 no-leak shape). Individual
        // tests override this for their specific email. NSubstitute wraps the
        // bare value in Task<T> automatically for Task-returning members.
        identity.FindSubjectByEmailAsync(Arg.Any<string>())
            .Returns((string?)null);

        var controller = new GuardianController(userInfo, identity, store);

        // The ClaimsPrincipal — the actor's subject id via the Kumunita.Sub
        // claim (KumunitaPrincipal.SubjectId reads this). Use the Kumunita
        // ClaimTypes.Subject constant (NOT System.Security.Claims.ClaimTypes —
        // that would be the BCL "http://schemas.microsoft.com/..." URI, which
        // KumunitaPrincipal.SubjectId does not read).
        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim(
                    Kumunita.Core.Identity.ClaimTypes.Subject,
                    actorSubjectId) },
                authenticationType: "test"));

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // TempData — the happy path writes TempData["info"] before the
        // redirect; a NoOpTempDataProvider prevents the NRE.
        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());

        return (controller, identity, store);
    }

    /// <summary>Seeds a minimal <see cref="GuardianLink"/> row (Active) for the
    /// (guardian, child) pair — the standing the <c>Assign</c> action's gate
    /// reads (G-A·1).</summary>
    private static async Task SeedGuardianLinkAsync(
        IDocumentStore store, string guardianId, string childId, CancellationToken ct)
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(new GuardianLink
        {
            Id = Guid.NewGuid().ToString("N"),
            GuardianId = guardianId,
            ChildId = childId,
            Status = GuardianLinkStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await session.SaveChangesAsync(ct);
    }

    private static async Task<int> CountAuditAsync(IDocumentStore store, string action, string targetId, CancellationToken ct)
    {
        await using var session = store.QuerySession();
        return await session.Query<AccessAudit>()
            .Where(a => a.Action == action && a.TargetId == targetId)
            .CountAsync(ct);
    }

    private static async Task<AccessAudit> LastAuditAsync(IDocumentStore store, string action, string targetId, CancellationToken ct)
    {
        await using var session = store.QuerySession();
        var row = await session.Query<AccessAudit>()
            .Where(a => a.Action == action && a.TargetId == targetId)
            .OrderByDescending(a => a.At)
            .FirstOrDefaultAsync(ct);
        Assert.NotNull(row);
        return row!;
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect / the call log, not the bag
        }
    }
}
