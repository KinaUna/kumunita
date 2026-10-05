using System.Security.Claims;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// GA (ADR 0038 — guardian assignment: an existing guardian assigns a second
/// guardian to a child's account) — the lane's <b>3 pinned Core seam tests</b> for
/// the <see cref="IIdentityService.FindSubjectByEmailAsync"/> ADD (the email →
/// subject id resolution; invariant G-A·2's no-leak shape: an unknown email
/// returns <c>null</c>, never an exception that names the email). These are the
/// <c>Pinned contract → Pinned seam tests (exact names)</c> frozen in
/// <c>docs/design/guardian-assignment-design.md</c> (U01), asserting U02's seam +
/// impl.
/// <para>
/// The account is seeded through the <b>real M1 lifecycle</b> —
/// <see cref="IIdentityService.RegisterAsync"/> +
/// <see cref="IIdentityService.VerifyWithTokenAsync"/> — driven by the real EF
/// <see cref="UserStore{TUser,TRole,TContext,TKey,TUserClaim,TUserRole,TUserLogin,TUserToken,TRoleClaim}"/>
/// over a migrated <c>identity</c> schema (the same <c>identity</c>-schema store
/// production uses) and the real <see cref="PasswordHasher{TUser}"/> /
/// <see cref="EmailAddressNormalizer"/>. A no-op <see cref="IMailerStage"/> stands
/// in for the outbox stage (the durable email lane is not under test here); the
/// verification token is read back from the seeded <see cref="IdentityToken"/> row
/// and consumed exactly as the Web's <c>/account/verify</c> handoff would. Each
/// test hands itself a fresh scratch Postgres DB (the
/// <see cref="GuardianControlsTests"/> harness shape — the
/// <see cref="PostgresFixture"/> plus the <c>BootStoreAsync</c> boot the GU lane
/// established in this assembly).
/// <para>
/// These are Core tests: no <c>Kumunita.Web</c> type anywhere; the 5 Web
/// controller tests are U06's, and the acceptance gate is U07's. A test whose
/// exact name is not in the design doc's pinned list is a drift pause, not a
/// silent add.
/// </para>
/// </summary>
public class GuardianAssignmentTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── The known-email resolution — the seam's happy path ───────────────────
    // Seed a verified account through the M1 lifecycle (RegisterAsync +
    // VerifyWithTokenAsync); FindSubjectByEmailAsync resolves the typed email to
    // the account's subject id (the same value RegisterAsync's principal carried).

    [Fact]
    public async Task FindSubjectByEmail_ReturnsSubjectIdForKnownEmail()
    {
        var (identity, _, store) = await BootIdentityAsync();

        const string email = "ga-u03-known@example.com";
        var principal = await identity.RegisterAsync("GA U03 Known", email, "Passw0rd!u03known");
        await VerifySeededAccountAsync(identity, store);

        Assert.Equal(principal.SubjectId, await identity.FindSubjectByEmailAsync(email));
    }

    // ── G-A·2 — no-leak shape: an unknown email returns null, not an exception ─
    // The Web's Assign action turns the null into the form's "No account with
    // that email." surface; a 500/exception naming the email would leak account
    // existence the way the ADR 0008 "a non-guardian learns nothing" shape forbids.

    [Fact]
    public async Task FindSubjectByEmail_ReturnsNullForUnknownEmail()
    {
        var (identity, _, _) = await BootIdentityAsync();

        Assert.Null(await identity.FindSubjectByEmailAsync("no-account@example.com"));
    }

    // ── The ASP.NET Identity FindByEmailAsync case-insensitive precedent ───────
    // Seed with a mixed-case email; the lowercase lookup still resolves to the
    // same subject id (the store's NormalizedEmail column — set by the real
    // UpperInvariantLookupNormalizer on Create — is what FindByEmailAsync compares).

    [Fact]
    public async Task FindSubjectByEmail_IsCaseInsensitiveOnEmail()
    {
        var (identity, _, store) = await BootIdentityAsync();

        const string email = "User@Example.com";
        var principal = await identity.RegisterAsync("GA U03 Case", email, "Passw0rd!u03case");
        await VerifySeededAccountAsync(identity, store);

        Assert.Equal(principal.SubjectId, await identity.FindSubjectByEmailAsync("user@example.com"));
    }

    // ── 9 (2026-09-17) — the gate's HANDOFF leg, promoted from inference to a test ──
    // ADR 0038 §F — the acceptance lane: after the GA lane writes a PENDING
    // row, the assigned guardian must ACCEPT (with consent) before their
    // standing is minted. This test drives the full acceptance flow:
    // (a) the assigning guardian assigns → Pending row;
    // (b) the assigned guardian accepts → Active row + guardian.create +
    //     guardian.accept audit rows;
    // (c) the assigned guardian, now a full guardian, drives the GU
    //     supervisory lanes over the child (SuspendChildAsync /
    //     UnsuspendChildAsync) — G-A·3 held (identical in kind to the
    //     creator's).
    // This is the acceptance gate's second leg (closed loop / HANDOFF /
    // part-vs-whole), now proven, not inferred.

    [Fact]
    public async Task Handoff_AssignedGuardian_CanSuspendAndUnsuspendChild_AfterAccept()
    {
        var (_, userInfo, store) = await BootIdentityAsync();
        var ct = TestContext.Current.CancellationToken;

        const string assigningGuardian = "ga-handoff-assigning";
        const string assignedGuardian = "ga-handoff-assigned";
        const string child = "ga-handoff-child";

        // Seed a minimal child profile (SuspendChildAsync loads the child's
        // Profile by SubjectId) + the assigning guardian's active link (their
        // standing basis, G-A·1).
        await SeedChildProfileAsync(store, child, ct);
        await userInfo.CreateGuardianLinkAsync(child, assigningGuardian);

        // The GA lane: the assigning guardian assigns the second guardian —
        // exactly the seam GuardianController.Assign calls (ADR 0038 §F:
        // the seam now writes a PENDING row; the standing is not minted
        // until the assignee accepts).
        var link = await userInfo.AssignGuardianLinkAsync(
            child, assignedGuardian, assigningGuardian);
        Assert.Equal(GuardianLinkStatus.Pending, link.Status);

        // The assignee's identity gate: BEFORE the accept, the assignee
        // holds no standing over the child (the standing gates all query
        // Active exclusively — the GU G·3 deny-by-default shape applied to
        // the acceptance lane).
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => userInfo.SuspendChildAsync(child, assignedGuardian));

        // The assignee ACCEPTS (the ADR 0038 §F supersession of the
        // "no acceptance step" deferral; the consent gate is the Web's
        // AcceptGuardianForm.GuardianConsent — the Core seam is the
        // state-machine transition, the consent check is the Web layer's
        // job). The row flips Pending → Active; the guardian.create +
        // guardian.accept audit rows commit (C3).
        var accepted = await userInfo.AcceptGuardianLinkAsync(child, assignedGuardian);
        Assert.Equal(GuardianLinkStatus.Active, accepted.Status);
        Assert.Equal(assignedGuardian, accepted.ResolvedBy);

        // HANDOFF: the assigned guardian, now a full guardian, suspends +
        // un-suspends the child — the GU lanes resolve the new row (G-A·3 —
        // identical in kind to the creator's, no content read).
        await userInfo.SuspendChildAsync(child, assignedGuardian);
        Assert.True((await userInfo.GetProfileAsync(child))!.Blocked);

        await userInfo.UnsuspendChildAsync(child, assignedGuardian);
        Assert.False((await userInfo.GetProfileAsync(child))!.Blocked);

        // The suspension audit row records the ASSIGNED guardian as the actor
        // (the GU seam's standing-holder-as-actor shape; ADR 0038 §D, as
        // reconciled 2026-09-17).
        var row = await LastAuditAsync(store, "guardian.suspend", child, ct);
        Assert.Equal(assignedGuardian, row.ActorId);
        Assert.Equal(AccessVia.Guardian, row.Via);
    }

    // ── ADR 0038 §F — the assignee's identity gate: a different assignee's
    // accept is refused (the ADR 0012/0013 "a non-guardian learns nothing"
    // shape applied to the acceptance lane) ─────────────────────────────────

    [Fact]
    public async Task Accept_ByNonAssignee_Refused()
    {
        var (_, userInfo, store) = await BootIdentityAsync();
        var ct = TestContext.Current.CancellationToken;

        const string assigningGuardian = "ga-accept-nonassigning";
        const string assignedGuardian = "ga-accept-assigned";
        const string otherAssignee  = "ga-accept-other";
        const string child          = "ga-accept-child";

        await SeedChildProfileAsync(store, child, ct);
        await userInfo.CreateGuardianLinkAsync(child, assigningGuardian);
        await userInfo.AssignGuardianLinkAsync(child, assignedGuardian, assigningGuardian);

        // A different account (the otherAssignee) tries to accept the
        // assignedGuardian's request — refused: the row's GuardianId field
        // is the assignedGuardian's identity, not the otherAssignee's. The
        // seam throws InvalidOperationException (the "no Pending row for
        // this pair" shape); the Web maps this to the Index page's
        // TempData["error"] surface (the Suspend / Unsuspend / Dissolve
        // precedent).
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => userInfo.AcceptGuardianLinkAsync(child, otherAssignee));
    }

    // ── ADR 0038 §F — the accept over an already-Active row is refused ──
    // A second accept over the same (guardianId, childId) pair is refused,
    // not a no-op (the GroupInvitation.AcceptAsync C-M2b·3 shape: a
    // re-resolved invitation is a bug the caller must surface, not a
    // silent idempotent).

    [Fact]
    public async Task Accept_AlreadyActive_Refused()
    {
        var (_, userInfo, store) = await BootIdentityAsync();
        var ct = TestContext.Current.CancellationToken;

        const string assigningGuardian = "ga-accept-already-active-assigning";
        const string assignedGuardian = "ga-accept-already-active-assigned";
        const string child          = "ga-accept-already-active-child";

        await SeedChildProfileAsync(store, child, ct);
        await userInfo.CreateGuardianLinkAsync(child, assigningGuardian);
        await userInfo.AssignGuardianLinkAsync(child, assignedGuardian, assigningGuardian);

        // First accept: Pending → Active (the happy path).
        await userInfo.AcceptGuardianLinkAsync(child, assignedGuardian);

        // Second accept over the same pair: refused (the row is no longer
        // Pending — the lane's honesty invariant, the C-M2b·3 shape).
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => userInfo.AcceptGuardianLinkAsync(child, assignedGuardian));
    }

    // ── ADR 0038 §F — the decline seam: Pending → Declined + audit row ──

    [Fact]
    public async Task Decline_PendingToDeclined_WritesDeclineAuditRow()
    {
        var (_, userInfo, store) = await BootIdentityAsync();
        var ct = TestContext.Current.CancellationToken;

        const string assigningGuardian = "ga-decline-assigning";
        const string assignedGuardian = "ga-decline-assigned";
        const string child          = "ga-decline-child";

        await SeedChildProfileAsync(store, child, ct);
        await userInfo.CreateGuardianLinkAsync(child, assigningGuardian);
        var pending = await userInfo.AssignGuardianLinkAsync(
            child, assignedGuardian, assigningGuardian);
        Assert.Equal(GuardianLinkStatus.Pending, pending.Status);

        // The decline: Pending → Declined + the guardian.decline audit row.
        var declined = await userInfo.DeclineGuardianLinkAsync(child, assignedGuardian);
        Assert.Equal(GuardianLinkStatus.Declined, declined.Status);
        Assert.Equal(assignedGuardian, declined.ResolvedBy);
        Assert.NotNull(declined.ResolvedAt);

        // The guardian.decline row: ActorId = the assigned guardian (the
        // one who refused). No guardian.create row (the standing was never
        // minted).
        var declineRow = await LastAuditAsync(store, "guardian.decline", pending.Id, ct);
        Assert.Equal(assignedGuardian, declineRow.ActorId);
        Assert.Equal(assignedGuardian, declineRow.EffectivePrincipalId);
        Assert.Equal(AccessVia.Guardian, declineRow.Via);
        Assert.Equal(AccessOutcome.Allow, declineRow.Outcome);

        // No guardian.create row (the standing was never minted).
        Assert.Equal(0, await CountAuditAsync(store, "guardian.create", pending.Id, ct));

        // The decline is one-way for the lane (the ADR 0038 §F
        // "Declined is terminal for the lane" shape): a re-accept over a
        // Declined row is refused (the row is no longer Pending).
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => userInfo.AcceptGuardianLinkAsync(child, assignedGuardian));
    }

    // ── ADR 0038 §F — the conferrer's re-act over a Declined row: the row
    // flips back to Pending (a deliberate new act, the ADR 0038 §F
    // "re-assignment is a deliberate new act, not an undo" shape) ──

    [Fact]
    public async Task Assign_ReActOverDeclined_FlipsBackToPending()
    {
        var (_, userInfo, store) = await BootIdentityAsync();
        var ct = TestContext.Current.CancellationToken;

        const string assigningGuardian = "ga-react-assigning";
        const string assignedGuardian = "ga-react-assigned";
        const string child          = "ga-react-child";

        await SeedChildProfileAsync(store, child, ct);
        await userInfo.CreateGuardianLinkAsync(child, assigningGuardian);

        // First assign: Pending.
        var first = await userInfo.AssignGuardianLinkAsync(
            child, assignedGuardian, assigningGuardian);
        Assert.Equal(GuardianLinkStatus.Pending, first.Status);

        // The assignee declines: Pending → Declined.
        await userInfo.DeclineGuardianLinkAsync(child, assignedGuardian);

        // The conferrer re-acts (the ADR 0038 §F re-act lane): the row flips
        // back to Pending. The unique index keeps this ONE row (not a
        // second); the state machine re-enters Pending. The assignee's
        // ResolvedAt/ResolvedBy are cleared; a fresh guardian.assign audit
        // row is written.
        var second = await userInfo.AssignGuardianLinkAsync(
            child, assignedGuardian, assigningGuardian);
        Assert.Equal(GuardianLinkStatus.Pending, second.Status);
        Assert.Equal(first.Id, second.Id);   // the same row (the unique index)
        Assert.Null(second.ResolvedAt);
        Assert.Null(second.ResolvedBy);

        // Two guardian.assign rows (the first assign + the re-act).
        Assert.Equal(2, await CountAuditAsync(store, "guardian.assign", first.Id, ct));
    }

    // ── 10 (ADR 0038 §F) — the conferral seam writes a PENDING row + ONE
    // guardian.assign audit row in one commit ──
    // S·1 (C3 atomicity) + S·5 (audit shape) + S·6 (idempotency). The
    // ADR 0038 §F amendment supersedes the GA-AR "two audit rows" contract:
    // the standing is CONFIRMED, not conferred — the row lands Pending, and
    // the guardian.create row (the standing-holder's GU seam's shape) is
    // deferred to AcceptGuardianLinkAsync (the assignee's consent is what
    // mints the standing). The guardian.assign row (the conferrer) is the
    // commit's audit.
    // The "who holds standing" legibility is now on the row itself (the
    // GuardianLink.AssignedById field, ADR 0038 §F supersedes the
    // "byte-identical POCO" invariant S·3): the conferrer's SubjectId is
    // persisted on the row, not only recoverable from the audit trail.

    [Fact]
    public async Task AssignGuardianLink_WritesPendingRow_AndAssignAuditRow()
    {
        var (_, userInfo, store) = await BootIdentityAsync();
        var ct = TestContext.Current.CancellationToken;

        const string assigningGuardian = "ga-f-assigning";
        const string assignedGuardian = "ga-f-assigned";
        const string child = "ga-f-child";

        await SeedChildProfileAsync(store, child, ct);

        // Call the seam — one commit, the Pending row + one audit row (S·1).
        var link = await userInfo.AssignGuardianLinkAsync(
            child, assignedGuardian, assigningGuardian);
        Assert.Equal(GuardianLinkStatus.Pending, link.Status);

        // (a) one GuardianLink row for the pair, Pending.
        await using var q1 = store.QuerySession();
        var linkCount = await Marten.QueryableExtensions.CountAsync(
            q1.Query<GuardianLink>()
                .Where(l => l.GuardianId == assignedGuardian && l.ChildId == child),
            ct);
        Assert.Equal(1, linkCount);

        // (b) the conferrer is persisted on the row (ADR 0038 §F — the
        //     "who conferred the standing" legibility, the S·3
        //     byte-identical-POCO invariant is superseded).
        Assert.Equal(assigningGuardian, link.AssignedById);
        // The assignee's ResolvedAt/ResolvedBy are null (the row is still
        // Pending — they have not yet acted).
        Assert.Null(link.ResolvedAt);
        Assert.Null(link.ResolvedBy);

        // (c) one guardian.assign row, ActorId = the ASSIGNING guardian (the
        //     conferrer, S·5).
        var assignRow = await LastAuditAsync(store, "guardian.assign", link.Id, ct);
        Assert.Equal(assigningGuardian, assignRow.ActorId);
        Assert.Equal(assigningGuardian, assignRow.EffectivePrincipalId);
        Assert.Equal(AccessVia.Guardian, assignRow.Via);
        Assert.Equal(AccessOutcome.Allow, assignRow.Outcome);

        // (d) the row targets the link id, TargetKind = "guardian-link".
        Assert.Equal(link.Id, assignRow.TargetId);
        Assert.Equal("guardian-link", assignRow.TargetKind);

        // (e) NO guardian.create row (the standing is not minted until the
        //     assignee accepts — the ADR 0038 §F supersession of the GA-AR
        //     "two audit rows" contract).
        Assert.Equal(0, await CountAuditAsync(store, "guardian.create", link.Id, ct));

        // (f) S·6 — idempotency: a second call with the same pair (while the
        //     row is still Pending) is a no-op — the row count stays 1, the
        //     audit-row count stays 1.
        await userInfo.AssignGuardianLinkAsync(
            child, assignedGuardian, assigningGuardian);

        await using var q2 = store.QuerySession();
        var linkCount2 = await Marten.QueryableExtensions.CountAsync(
            q2.Query<GuardianLink>()
                .Where(l => l.GuardianId == assignedGuardian && l.ChildId == child),
            ct);
        Assert.Equal(1, linkCount2);

        var assignCount2 = await CountAuditAsync(store, "guardian.assign", link.Id, ct);
        Assert.Equal(1, assignCount2);
        // Still no guardian.create row.
        Assert.Equal(0, await CountAuditAsync(store, "guardian.create", link.Id, ct));
    }

    // ── ADR 0038 §F — the accept seam writes TWO audit rows in one commit ──
    // The standing is minted on the accept: the guardian.create row (the
    // standing-holder's GU seam's shape, byte-identical to what
    // CreateGuardianLinkAsync writes) + the guardian.accept row (the
    // consent event). Together they answer "who holds standing" AND "when
    // was the standing minted."

    [Fact]
    public async Task AcceptGuardianLink_WritesCreateAndAcceptAuditRows()
    {
        var (_, userInfo, store) = await BootIdentityAsync();
        var ct = TestContext.Current.CancellationToken;

        const string assigningGuardian = "ga-accept-audit-assigning";
        const string assignedGuardian = "ga-accept-audit-assigned";
        const string child = "ga-accept-audit-child";

        await SeedChildProfileAsync(store, child, ct);
        await userInfo.CreateGuardianLinkAsync(child, assigningGuardian);
        var pending = await userInfo.AssignGuardianLinkAsync(
            child, assignedGuardian, assigningGuardian);
        Assert.Equal(GuardianLinkStatus.Pending, pending.Status);

        // The accept: the standing is minted — two audit rows in one commit.
        var accepted = await userInfo.AcceptGuardianLinkAsync(child, assignedGuardian);
        Assert.Equal(GuardianLinkStatus.Active, accepted.Status);
        Assert.Equal(assignedGuardian, accepted.ResolvedBy);
        Assert.NotNull(accepted.ResolvedAt);

        // (a) one guardian.create row, ActorId = the ASSIGNED guardian (the
        //     standing-holder, the GU seam's shape — byte-identical to what
        //     CreateGuardianLinkAsync writes).
        var createRow = await LastAuditAsync(store, "guardian.create", pending.Id, ct);
        Assert.Equal(assignedGuardian, createRow.ActorId);
        Assert.Equal(assignedGuardian, createRow.EffectivePrincipalId);
        Assert.Equal(AccessVia.Guardian, createRow.Via);
        Assert.Equal(AccessOutcome.Allow, createRow.Outcome);
        Assert.Equal(pending.Id, createRow.TargetId);
        Assert.Equal("guardian-link", createRow.TargetKind);

        // (b) one guardian.accept row, ActorId = the ASSIGNED guardian (the
        //     one who consented — the ADR 0038 §F consent event).
        var acceptRow = await LastAuditAsync(store, "guardian.accept", pending.Id, ct);
        Assert.Equal(assignedGuardian, acceptRow.ActorId);
        Assert.Equal(assignedGuardian, acceptRow.EffectivePrincipalId);
        Assert.Equal(AccessVia.Guardian, acceptRow.Via);
        Assert.Equal(AccessOutcome.Allow, acceptRow.Outcome);
        Assert.Equal(pending.Id, acceptRow.TargetId);
        Assert.Equal("guardian-link", acceptRow.TargetKind);

        // (c) the guardian.assign row (the conferral event) is still on the
        //     row from the earlier assign — the full audit trail is
        //     legible: who conferred (the conferrer), who holds standing
        //     (the assignee), when the standing was minted (the accept row).
        var assignRow = await LastAuditAsync(store, "guardian.assign", pending.Id, ct);
        Assert.Equal(assigningGuardian, assignRow.ActorId);
    }

    // ── ADR 0038 §F — the read seam: the assignee's pending requests ──

    [Fact]
    public async Task GetPendingGuardianRequestsForAssignee_ReturnsOnlyAssigneesPendingRows()
    {
        var (_, userInfo, store) = await BootIdentityAsync();
        var ct = TestContext.Current.CancellationToken;

        const string assigningGuardian = "ga-read-assigning";
        const string assignedGuardian = "ga-read-assigned";
        const string otherGuardian  = "ga-read-other";
        const string child          = "ga-read-child";

        await SeedChildProfileAsync(store, child, ct);
        await userInfo.CreateGuardianLinkAsync(child, assigningGuardian);
        await userInfo.AssignGuardianLinkAsync(child, assignedGuardian, assigningGuardian);

        // The assigned guardian's read: exactly one row (their pending
        // request for this child).
        var rows = await userInfo.GetPendingGuardianRequestsForAssigneeAsync(assignedGuardian);
        Assert.Single(rows);
        Assert.Equal(child, rows[0].ChildId);
        Assert.Equal(assigningGuardian, rows[0].AssignedById);
        Assert.Equal(GuardianLinkStatus.Pending, rows[0].Status);

        // The assigning guardian's read: zero rows (they are not the
        // assignee of this request — they are the conferrer; their row is
        // Active from CreateGuardianLinkAsync, not Pending).
        var otherRows = await userInfo.GetPendingGuardianRequestsForAssigneeAsync(assigningGuardian);
        Assert.Empty(otherRows);

        // A third account's read: zero rows (the ADR 0012/0013 "a
        // non-guardian learns nothing" shape applied to the identity axis).
        var strangerRows = await userInfo.GetPendingGuardianRequestsForAssigneeAsync("stranger");
        Assert.Empty(strangerRows);

        // After the assignee accepts: the read returns zero rows (the row is
        // no longer Pending).
        await userInfo.AcceptGuardianLinkAsync(child, assignedGuardian);
        var afterAccept = await userInfo.GetPendingGuardianRequestsForAssigneeAsync(assignedGuardian);
        Assert.Empty(afterAccept);
    }

    // ── Shared harness ────────────────────────────────────────────────────────

    /// <summary>
    /// Boots the two-store shape the seam reads from: the <c>mt</c> document
    /// schema (the <see cref="GuardianControlsTests"/> boot, byte-identical)
    /// plus the <c>identity</c> schema (EF Core migrations applied — the same
    /// call <c>SchemaBootstrap.ApplyAsync</c> makes at boot), and hands back the
    /// real <see cref="IdentityService"/> wired with the real EF
    /// <see cref="UserStore{TUser,TRole,TContext,TKey,TUserClaim,TUserRole,TUserLogin,TUserToken,TRoleClaim}"/>
    /// (the <c>identity</c>-schema store production uses), the real
    /// <see cref="PasswordHasher{TUser}"/> + <see cref="EmailKeyNormalizer"/>,
    /// and a no-op <see cref="IMailerStage"/> (the outbox stage
    /// <c>RegisterAsync</c> touches; the seam under test never reads one).
    /// </summary>
    private async Task<(IdentityService identity, UserInfoService userInfo, IDocumentStore store)> BootIdentityAsync()
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

        // The identity schema — the only EF Core in the tree (ADR 0004), migrated
        // exactly as SchemaBootstrap.ApplyAsync does at boot.
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn)
            .Options);
        await db.Database.MigrateAsync(ct);

        // The real EF identity store over the migrated `identity` schema — the same
        // UserStore<AppUser, IdentityRole, AppDbContext, …> the Web host's
        // AddEntityFrameworkStores<AppDbContext>() registers in production.
        var userStore = new UserStore<User, IdentityRole, AppDbContext, string,
            IdentityUserClaim<string>, IdentityUserRole<string>,
            IdentityUserLogin<string>, IdentityUserToken<string>,
            IdentityRoleClaim<string>>(db);

        var options = new IdentityOptions { User = { RequireUniqueEmail = true } };
        var userManager = new UserManager<User>(
            userStore,
            Options.Create(options),
            new PasswordHasher<User>(),
            new[] { new UserValidator<User>() },
            new[] { new PasswordValidator<User>() },
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            EmptyServiceProvider.Instance,
            NullLogger<UserManager<User>>.Instance);

        var userInfo = new UserInfoService(store);
        var identity = new IdentityService(
            userManager,
            store,
            userInfo,
            NoClaimsSource.Instance,
            NoMail.Instance,
            Options.Create(new VerificationOptions()),
            NullLogger<IdentityService>.Instance);

        return (identity, userInfo, store);
    }

    /// <summary>
    /// Completes the M1 verification handoff exactly as the Web's
    /// <c>/account/verify</c> action would: <c>RegisterAsync</c> staged the
    /// single-use token (row Id = the URL's <c>?id=</c> value; the high-entropy
    /// secret is what <c>VerifyWithTokenAsync</c> compares). Each test owns a
    /// fresh scratch DB, so the single <see cref="IdentityToken"/> KindVerify
    /// row IS the just-registered account's token — read it back and consume it.
    /// </summary>
    private static async Task VerifySeededAccountAsync(IIdentityService identity, IDocumentStore store)
    {
        await using var session = store.QuerySession();
        // Fully-qualified: both Marten and EF Core expose a FirstAsync(IQueryable<T>)
        // extension here; this is the Marten (mt-schema) query.
        var token = await Marten.QueryableExtensions
            .FirstAsync(
                session.Query<IdentityToken>()
                    .Where(t => t.Kind == IdentityToken.KindVerify)
                    .OrderByDescending(t => t.CreatedAt),
                TestContext.Current.CancellationToken);
        await identity.VerifyWithTokenAsync(token.Token);
    }

    // ── Test doubles (the two Web-side seams the IdentityService takes; the
    //    Identity shared framework ships no public Null* test doubles in .NET 10,
    //    and the mailer's durable-envelope path needs a started Wolverine host —
    //    neither is under test here, so minimal substitutes are honest) ──

    private sealed class NoClaimsSource : IClaimsSource
    {
        public static readonly NoClaimsSource Instance = new();
        public ClaimsPrincipal? Current => null;   // not request-driven in a Core test
    }

    private sealed class NoMail : IMailerStage
    {
        public static readonly NoMail Instance = new();
        public Task StageAsync(
            Marten.IDocumentSession session,
            string idempotencyKey,
            string recipient,
            string subject,
            string body,
            CancellationToken ct = default) => Task.CompletedTask;   // no outbox row — the seam under test never reads one
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static readonly EmptyServiceProvider Instance = new();
        public object? GetService(Type serviceType) => null;   // unregistered → null (the IServiceProvider contract); ProtectPersonalData is off, so no manager path resolves anything
    }

    // ── Handoff-leg helpers (2026-09-17) ──────────────────────────────────────

    /// <summary>
    /// Seeds a minimal verified, un-blocked <see cref="Profile"/> for the child so
    /// <c>SuspendChildAsync</c> (which loads the child's profile by
    /// <c>SubjectId</c>) has something to read (the GU lane's
    /// <c>SeedChildProfileAsync</c> shape, for the handoff leg).
    /// </summary>
    private static async Task SeedChildProfileAsync(IDocumentStore store, string childId, CancellationToken ct)
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(new Profile
        {
            SubjectId = childId,
            DisplayName = "Child",
            Verified = true,
            Blocked = false,
            Visibility = new Audience(),
        });
        await session.SaveChangesAsync(ct);
    }

    /// <summary>The most recent <see cref="AccessAudit"/> row for an action + target
    /// (the GU lane's <c>LastAuditAsync</c> shape) — the handoff leg reads the
    /// suspension row's <c>ActorId</c> + <c>Via</c>.</summary>
    private static async Task<AccessAudit> LastAuditAsync(
        IDocumentStore store, string action, string targetId, CancellationToken ct)
    {
        await using var session = store.QuerySession();
        // Fully-qualified: both Marten and EF Core expose a FirstOrDefaultAsync(IQueryable<T>)
        // extension here; this is the Marten (mt-schema) query (the existing
        // VerifySeededAccountAsync precedent).
        var row = await Marten.QueryableExtensions
            .FirstOrDefaultAsync(
                session.Query<AccessAudit>()
                    .Where(a => a.Action == action && a.TargetId == targetId)
                    .OrderByDescending(a => a.At),
                ct);
        Assert.NotNull(row);
        return row!;
    }

    /// <summary>The count of <see cref="AccessAudit"/> rows for an action +
    /// target (the GU lane's <c>CountAuditAsync</c> shape) — the idempotency
    /// sub-assertion reads the audit-row counts.</summary>
    private static async Task<int> CountAuditAsync(
        IDocumentStore store, string action, string targetId, CancellationToken ct)
    {
        await using var session = store.QuerySession();
        return await Marten.QueryableExtensions.CountAsync(
            session.Query<AccessAudit>()
                .Where(a => a.Action == action && a.TargetId == targetId),
            ct);
    }
}
