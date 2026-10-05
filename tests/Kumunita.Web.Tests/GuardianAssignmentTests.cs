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

    // ── 8 — happy path (ADR 0038 §F) — AssignGuardianLinkAsync is called, new
    //      row (PENDING) + ONE assign audit row ─
    // A guardian assigns a known, non-self, non-duplicate email → the
    // AssignGuardianLinkAsync seam (ADR 0038 §F) creates a new GuardianLink
    // row in PENDING state + ONE audit row in one commit (C3 — S·1):
    // guardian.assign [ActorId = the ASSIGNING guardian / conferrer] (S·5).
    // The action redirects to Detail. The standing is NOT minted yet — the
    // guardian.create row is deferred to the Accept action (the assignee's
    // consent is what mints the standing, the ADR 0038 §F supersession of
    // the GA-AR "two audit rows" contract).

    [Fact]
    public async Task Assign_KnownEmail_WritesPendingRow_AndAssignAuditRow()
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

        // A new GuardianLink row for (newGuardian, child) with PENDING status
        // (the ADR 0038 §F acceptance lane: the standing is not minted until
        // the assignee accepts with consent).
        await using var session = store.QuerySession();
        var newLink = await session.Query<GuardianLink>()
            .Where(l => l.GuardianId == NewGuardian && l.ChildId == Child)
            .FirstOrDefaultAsync(ct);
        Assert.NotNull(newLink);
        Assert.Equal(GuardianLinkStatus.Pending, newLink!.Status);

        // The conferrer is persisted on the row (ADR 0038 §F — the S·3
        // byte-identical-POCO invariant is superseded: the "who conferred the
        // standing" legibility is now on the row itself).
        Assert.Equal(Guardian, newLink.AssignedById);
        // The assignee's ResolvedAt/ResolvedBy are null (the row is still
        // Pending — they have not yet acted).
        Assert.Null(newLink.ResolvedAt);
        Assert.Null(newLink.ResolvedBy);

        // ONE audit row targets the new link (S·1 — one commit, one row):
        // guardian.assign — ActorId = the ASSIGNING guardian (the conferrer,
        // S·5).
        var assignRow = await LastAuditAsync(store, "guardian.assign", newLink.Id, ct);
        Assert.Equal(Guardian, assignRow.ActorId);             // the assigning guardian (the test's actor)
        Assert.Equal(AccessVia.Guardian, assignRow.Via);
        Assert.Equal("guardian-link", assignRow.TargetKind);

        // NO guardian.create row (the standing is not minted until the
        // assignee accepts — the ADR 0038 §F supersession of the GA-AR
        // "two audit rows" contract).
        Assert.Equal(0, await CountAuditAsync(store, "guardian.create", newLink.Id, ct));
    }

    // ── 8a — ADR 0038 §F — the assignee's ACCEPT action: consent gate +
    //      seam call + redirect + audit rows ─
    // The assignee (the NewGuardian's subject id is the actor) accepts a
    // pending request. The consent gate is the form's GuardianConsent
    // checkbox: unchecked → the action refuses (redirects to Index with
    // TempData["error"]); checked → the accept seam runs (Pending → Active,
    // guardian.create + guardian.accept audit rows commit), redirect to
    // Index with TempData["info"].

    [Fact]
    public async Task Accept_NoConsent_Refused_RedirectsToIndex()
    {
        // The actor is the NEW GUARDIAN (the assignee — their subject id is
        // the NewGuardian constant), NOT the conferring Guardian. The
        // assignee holds no standing yet — the standing gate is not
        // applicable (the accept is what mints the standing).
        var (controller, identity, store) = await BuildAsync(
            actorSubjectId: NewGuardian, seedGuardianLink: false);
        var ct = TestContext.Current.CancellationToken;

        // Seed the pending request: the Guardian (the conferrer) assigns the
        // NewGuardian (the assignee) as a co-guardian for the child.
        await using (var seedSession = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            // The Guardian's own active link (their standing basis).
            await SeedGuardianLinkAsync(store, Guardian, Child, ct);
            // The Pending row (the assignee's request) — write it directly
            // (the seam is not called here — the test is about the ACCEPT
            // action's consent gate, not the seam's write).
            seedSession.Store(new GuardianLink
            {
                Id = Guid.NewGuid().ToString("N"),
                GuardianId = NewGuardian,
                ChildId = Child,
                Status = GuardianLinkStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                AssignedById = Guardian
            });
            await seedSession.SaveChangesAsync(ct);
        }

        // The consent gate: unchecked → refused.
        var result = await controller.Accept(
            Child,
            new AcceptGuardianForm { GuardianConsent = false });

        // The action redirects to Index (the consent gate's error surface —
        // the TempData["error"] the _FlashToast surfaces; the Suspend /
        // Unsuspend / Dissolve precedent in this controller).
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);

        // The row is STILL Pending (the accept did not run — the consent
        // gate refused it).
        await using var session = store.QuerySession();
        var link = await session.Query<GuardianLink>()
            .Where(l => l.GuardianId == NewGuardian && l.ChildId == Child)
            .FirstOrDefaultAsync(ct);
        Assert.NotNull(link);
        Assert.Equal(GuardianLinkStatus.Pending, link!.Status);
    }

    [Fact]
    public async Task Accept_WithConsent_MintsStanding_RedirectsToIndex()
    {
        var (controller, identity, store) = await BuildAsync(
            actorSubjectId: NewGuardian, seedGuardianLink: false);
        var ct = TestContext.Current.CancellationToken;

        // Seed the pending request (same shape as the consent-gate test).
        await SeedGuardianLinkAsync(store, Guardian, Child, ct);
        await using (var seedSession = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            seedSession.Store(new GuardianLink
            {
                Id = Guid.NewGuid().ToString("N"),
                GuardianId = NewGuardian,
                ChildId = Child,
                Status = GuardianLinkStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                AssignedById = Guardian
            });
            await seedSession.SaveChangesAsync(ct);
        }

        // The consent gate: checked → the accept runs.
        var result = await controller.Accept(
            Child,
            new AcceptGuardianForm { GuardianConsent = true });

        // The action redirects to Index (the success path — the TempData
        // ["info"] "You are now this child's guardian." the _FlashToast
        // surfaces).
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);

        // The row is now ACTIVE (the standing is minted).
        await using var session = store.QuerySession();
        var link = await session.Query<GuardianLink>()
            .Where(l => l.GuardianId == NewGuardian && l.ChildId == Child)
            .FirstOrDefaultAsync(ct);
        Assert.NotNull(link);
        Assert.Equal(GuardianLinkStatus.Active, link!.Status);
        Assert.Equal(NewGuardian, link.ResolvedBy);
        Assert.NotNull(link.ResolvedAt);

        // TWO audit rows target the link (C3 — one commit, two rows):
        // (1) guardian.create — ActorId = the ASSIGNED guardian (the
        //     standing-holder, the GU seam's shape — byte-identical to what
        //     CreateGuardianLinkAsync writes).
        var createRow = await LastAuditAsync(store, "guardian.create", link.Id, ct);
        Assert.Equal(NewGuardian, createRow.ActorId);
        Assert.Equal(AccessVia.Guardian, createRow.Via);

        // (2) guardian.accept — ActorId = the ASSIGNED guardian (the one who
        //     consented — the ADR 0038 §F consent event).
        var acceptRow = await LastAuditAsync(store, "guardian.accept", link.Id, ct);
        Assert.Equal(NewGuardian, acceptRow.ActorId);
        Assert.Equal(AccessVia.Guardian, acceptRow.Via);

        // The conferral legibility (ADR 0038 §F — the S·3
        // byte-identical-POCO invariant is superseded) is now on the row
        // itself: the conferrer's SubjectId is persisted in AssignedById
        // (the row's "who conferred the standing" legibility, the ADR 0038
        // §F amendment). The Web test harness seeds the row directly (not
        // via the seam), so the guardian.assign audit row is not written
        // here — the row's AssignedById field is the legibility surface.
        Assert.Equal(Guardian, link.AssignedById);
    }

    // ── 8b — ADR 0038 §F — the assignee's DECLINE action: the row flips
    //      Pending → Declined, the guardian.decline audit row commits ─

    [Fact]
    public async Task Decline_ByAssignee_FlipsRowToDeclined_WritesDeclineAuditRow()
    {
        var (controller, identity, store) = await BuildAsync(
            actorSubjectId: NewGuardian, seedGuardianLink: false);
        var ct = TestContext.Current.CancellationToken;

        // Seed the pending request (same shape as the accept tests).
        await SeedGuardianLinkAsync(store, Guardian, Child, ct);
        await using (var seedSession = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            seedSession.Store(new GuardianLink
            {
                Id = Guid.NewGuid().ToString("N"),
                GuardianId = NewGuardian,
                ChildId = Child,
                Status = GuardianLinkStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                AssignedById = Guardian
            });
            await seedSession.SaveChangesAsync(ct);
        }

        // The decline: no consent gate (the decline is a refusal, not an
        // acceptance).
        var result = await controller.Decline(Child);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);

        // The row is now DECLINED (the assignee refused the standing).
        await using var session = store.QuerySession();
        var link = await session.Query<GuardianLink>()
            .Where(l => l.GuardianId == NewGuardian && l.ChildId == Child)
            .FirstOrDefaultAsync(ct);
        Assert.NotNull(link);
        Assert.Equal(GuardianLinkStatus.Declined, link!.Status);
        Assert.Equal(NewGuardian, link.ResolvedBy);
        Assert.NotNull(link.ResolvedAt);

        // ONE audit row targets the link: guardian.decline — ActorId = the
        // ASSIGNED guardian (the one who refused).
        var declineRow = await LastAuditAsync(store, "guardian.decline", link.Id, ct);
        Assert.Equal(NewGuardian, declineRow.ActorId);
        Assert.Equal(AccessVia.Guardian, declineRow.Via);

        // NO guardian.create row (the standing was never minted).
        Assert.Equal(0, await CountAuditAsync(store, "guardian.create", link.Id, ct));
    }

    // ── 8c — ADR 0038 §F — a non-assignee's accept is refused (the
    //      ADR 0012/0013 "a non-guardian learns nothing" shape applied to
    //      the acceptance lane) ─

    [Fact]
    public async Task Accept_ByNonAssignee_Refused_RedirectsToIndexWithError()
    {
        // The actor is the Guardian (the CONFERING guardian — NOT the
        // assignee of the pending request). The assignee is the NewGuardian.
        var (controller, identity, store) = await BuildAsync(
            actorSubjectId: Guardian, seedGuardianLink: false);
        var ct = TestContext.Current.CancellationToken;

        // Seed the conferrer's active link + the assignee's pending request.
        await SeedGuardianLinkAsync(store, Guardian, Child, ct);
        await using (var seedSession = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            seedSession.Store(new GuardianLink
            {
                Id = Guid.NewGuid().ToString("N"),
                GuardianId = NewGuardian,
                ChildId = Child,
                Status = GuardianLinkStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                AssignedById = Guardian
            });
            await seedSession.SaveChangesAsync(ct);
        }

        // The conferring guardian tries to accept the NewGuardian's request
        // — refused: the row's GuardianId field is the NewGuardian's
        // identity, not the Guardian's. The seam throws
        // InvalidOperationException ("no pending row for this pair"); the
        // action catches it and redirects to Index with TempData["error"]
        // (the Suspend / Unsuspend / Dissolve precedent).
        var result = await controller.Accept(
            Child,
            new AcceptGuardianForm { GuardianConsent = true });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);

        // The row is STILL Pending (the accept did not run — the identity
        // gate refused it).
        await using var session = store.QuerySession();
        var link = await session.Query<GuardianLink>()
            .Where(l => l.GuardianId == NewGuardian && l.ChildId == Child)
            .FirstOrDefaultAsync(ct);
        Assert.NotNull(link);
        Assert.Equal(GuardianLinkStatus.Pending, link!.Status);
    }

    // ── 8d — ADR 0038 §F — the Index action returns the assignee's pending
    //      requests (the pending-requests card's source data) ─

    [Fact]
    public async Task Index_ReturnsAssigneesPendingRequests()
    {
        // The actor is the NewGuardian (the assignee — their pending request
        // is what the Index page's pending-requests card shows).
        var (controller, identity, store) = await BuildAsync(
            actorSubjectId: NewGuardian, seedGuardianLink: false);
        var ct = TestContext.Current.CancellationToken;

        // Seed the conferrer's active link + the assignee's pending request.
        await SeedGuardianLinkAsync(store, Guardian, Child, ct);
        await using (var seedSession = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            seedSession.Store(new GuardianLink
            {
                Id = Guid.NewGuid().ToString("N"),
                GuardianId = NewGuardian,
                ChildId = Child,
                Status = GuardianLinkStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                AssignedById = Guardian
            });
            await seedSession.SaveChangesAsync(ct);
        }

        var result = await controller.Index();
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<Kumunita.Web.Models.GuardianIndexModel>(view.Model);

        // The pending-requests list has exactly one row (the assignee's
        // pending request for this child).
        Assert.Single(model.PendingRequests);
        Assert.Equal(Child, model.PendingRequests[0].ChildId);
        Assert.Equal(Guardian, model.PendingRequests[0].ConferrerDisplayName);
        // The children list is empty (the assignee holds no active link yet
        // — the standing is not minted until they accept).
        Assert.Empty(model.Children);
    }

    // ── 8e — ADR 0038 §F — a non-assignee's Index shows no pending
    //      requests (the ADR 0012/0013 "a non-guardian learns nothing"
    //      shape applied to the identity axis) ─

    [Fact]
    public async Task Index_NonAssignee_SeesNoPendingRequests()
    {
        // The actor is the Guardian (the CONFERING guardian — NOT the
        // assignee of the pending request). The assignee is the NewGuardian.
        var (controller, identity, store) = await BuildAsync(
            actorSubjectId: Guardian, seedGuardianLink: false);
        var ct = TestContext.Current.CancellationToken;

        // Seed the conferrer's active link + the assignee's pending request.
        await SeedGuardianLinkAsync(store, Guardian, Child, ct);
        await using (var seedSession = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            seedSession.Store(new GuardianLink
            {
                Id = Guid.NewGuid().ToString("N"),
                GuardianId = NewGuardian,
                ChildId = Child,
                Status = GuardianLinkStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                AssignedById = Guardian
            });
            await seedSession.SaveChangesAsync(ct);
        }

        var result = await controller.Index();
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<Kumunita.Web.Models.GuardianIndexModel>(view.Model);

        // The Guardian is NOT the assignee of the pending request (their
        // row is Active from SeedGuardianLinkAsync, not Pending) — their
        // Index page's pending-requests list is empty.
        Assert.Empty(model.PendingRequests);
        // But their children list shows the child (their active link).
        Assert.Single(model.Children);
        Assert.Equal(Child, model.Children[0].ChildId);
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
