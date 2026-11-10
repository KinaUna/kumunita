using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.SideEffects;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// ADR 0301 (IMPROVE-UX R12) — the scheduled-account-deletion due-job
/// (<see cref="DeletionDueHandler"/>). A **store** unit (the
/// <see cref="Kumunita.Core.Tests.DeletionSchedulerTests"/> harness shape —
/// a real Marten <c>DocumentStore</c> over the
/// <see cref="PostgresFixture"/>), proving the handler is a thin adapter over
/// the R11 seams: it calls <see cref="IDeletionScheduler.ListDueAsync"/>,
/// then, per due row, the EXISTING <see cref="IIdentityService
/// .DeleteAccountAsync"/> self-serve lane (the "executes once" invariant is
/// the <see cref="Kumunita.Core.Identity.ResidentAccountRowsEraser"/> row
/// erasure, not a flag), then re-yields a fresh <see cref="DeletionDueTick"/>
/// so the recurring schedule carries forward.
/// <para>
/// Covers the three paths the R12 prompt names: (a) the happy path (a due
/// non-GlobalAdmin row is deleted, the account is gone, the
/// <c>DeletionRequest</c> row is erased, the due list is now empty);
/// (b) the no-due case (no rows past <c>DueAt</c> → no-op, re-yields);
/// (c) the refused-at-execution case (a last-GlobalAdmin promoted mid-window
/// is refused at execution, the row moves to
/// <see cref="DeletionRequestStatus.Cancelled"/>, and the next tick does NOT
/// retry — the "no-infinite-retry" invariant, ADR 0301 D2).
/// </para>
/// <para>
/// Synthetic fixture ids only — no real PII (the IS08 log gate).
/// </para>
/// </summary>
public sealed class DeletionDueHandlerTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── Happy path (the R12 headline) ─────────────────────────────────────

    [Fact(DisplayName = "Handler_DueNonGlobalAdminRow_DeletesAccountAndErasesRow")]
    public async Task Handler_DueNonGlobalAdminRow_DeletesAccountAndErasesRow()
    {
        var boot = await BootAsync();
        var resident = await SeedAccountAsync(boot, "resident@examplium.com", globalAdmin: false);
        await boot.Scheduler.ScheduleDeletionAsync(resident);
        var now = DateTimeOffset.UtcNow;
        await ForceDueAsync(boot.Store, resident, now);

        // Drive the handler (not the lane directly — the seam R12 delivers).
        var results = await DeletionDueHandler.Handle(
            new DeletionDueTick(), boot.Scheduler, boot.Identity, NullLogger.Instance);

        // (1) The handler re-yields a fresh DeletionDueTick (the schedule
        //     self-perpetuates).
        Assert.IsType<DeletionDueTick>(Assert.Single(results));

        // (2) The account is gone (the lane ran: the Identity row is deleted).
        Assert.Null(await boot.UserManager.FindByIdAsync(resident));

        // (3) The DeletionRequest row is erased (the "executes once" invariant
        //     is the ResidentAccountRowsEraser block 13, not a flag).
        Assert.Null(await DeletionRequestRowAsync(boot.Store, resident));

        // (4) The due list no longer returns the executed row (it is no longer
        //     Pending — it is gone).
        Assert.Empty(await boot.Scheduler.ListDueAsync(now));

        // (5) A "account.delete" audit row was written (the lane's summary
        //     row — the audit-of-the-deletion).
        var deletes = await AuditRows(boot.Store, action: "account.delete");
        Assert.Single(deletes);
        Assert.Equal(AccessVia.Owner, deletes[0].Via);
        Assert.Equal(resident, deletes[0].ActorId);
    }

    // ── No-due case (the idempotent-sweep shape) ───────────────────────────

    [Fact(DisplayName = "Handler_NoDueRows_NoOpAndReschedules")]
    public async Task Handler_NoDueRows_NoOpAndReschedules()
    {
        var boot = await BootAsync();
        var notDue = await SeedAccountAsync(boot, "notdue@examplium.com", globalAdmin: false);
        await boot.Scheduler.ScheduleDeletionAsync(notDue); // future DueAt — NOT due

        // Drive the handler — no due rows, so no lane call, no write.
        var results = await DeletionDueHandler.Handle(
            new DeletionDueTick(), boot.Scheduler, boot.Identity, NullLogger.Instance);

        // Re-yields (the schedule self-perpetuates) but the account is intact
        // and the pending row is still there (the sweep is a no-op).
        Assert.IsType<DeletionDueTick>(Assert.Single(results));
        Assert.NotNull(await boot.UserManager.FindByIdAsync(notDue));
        var row = await DeletionRequestRowAsync(boot.Store, notDue);
        Assert.NotNull(row);
        Assert.Equal(DeletionRequestStatus.Pending, row!.Status);
    }

    // ── Cancelled rows are never re-executed (ADR 0301 D2) ─────────────────

    [Fact(DisplayName = "Handler_CancelledRow_NotReExecuted")]
    public async Task Handler_CancelledRow_NotReExecuted()
    {
        var boot = await BootAsync();
        var resident = await SeedAccountAsync(boot, "cancelled@examplium.com", globalAdmin: false);
        await boot.Scheduler.ScheduleDeletionAsync(resident);
        await boot.Scheduler.CancelDeletionAsync(resident); // the resident signed in

        // Drive the handler — the cancelled row is NOT in the due list (it is
        // not Pending), so the lane is never called for it.
        await DeletionDueHandler.Handle(
            new DeletionDueTick(), boot.Scheduler, boot.Identity, NullLogger.Instance);

        // The account is intact (the lane never ran for this row) and the row
        // is still Cancelled (a Cancelled / already-erased row is never
        // re-executed — the R12 invariant).
        Assert.NotNull(await boot.UserManager.FindByIdAsync(resident));
        var row = await DeletionRequestRowAsync(boot.Store, resident);
        Assert.NotNull(row);
        Assert.Equal(DeletionRequestStatus.Cancelled, row!.Status);
    }

    // ── The "executes once" invariant under a double-run ───────────────────

    [Fact(DisplayName = "Handler_SecondRun_NoDoubleExecution")]
    public async Task Handler_SecondRun_NoDoubleExecution()
    {
        var boot = await BootAsync();
        var resident = await SeedAccountAsync(boot, "once@examplium.com", globalAdmin: false);
        await boot.Scheduler.ScheduleDeletionAsync(resident);
        var now = DateTimeOffset.UtcNow;
        await ForceDueAsync(boot.Store, resident, now);

        // First run: the account is deleted, the row is erased.
        await DeletionDueHandler.Handle(
            new DeletionDueTick(), boot.Scheduler, boot.Identity, NullLogger.Instance);
        Assert.Null(await boot.UserManager.FindByIdAsync(resident));
        Assert.Null(await DeletionRequestRowAsync(boot.Store, resident));

        // Second run: a no-op (no due rows left — the row was erased by the
        // first run's lane), NOT a re-execution (the lane is not called a
        // second time for a row that is already gone). The re-yield still
        // happens (the schedule self-perpetuates).
        var results = await DeletionDueHandler.Handle(
            new DeletionDueTick(), boot.Scheduler, boot.Identity, NullLogger.Instance);
        Assert.IsType<DeletionDueTick>(Assert.Single(results));
        Assert.Null(await boot.UserManager.FindByIdAsync(resident));
    }

    // ── Refused-at-execution: last-GlobalAdmin promoted mid-window ─────────

    [Fact(DisplayName = "Handler_LastGlobalAdminAtExecution_RefusesAndStopsRetrying")]
    public async Task Handler_LastGlobalAdminAtExecution_RefusesAndStopsRetrying()
    {
        var boot = await BootAsync();
        // Two GlobalAdmins exist, so the scheduling-time last-GA pin (ADR 0301
        // D2 / 0142 D3) is satisfied — the schedule writes the pending row.
        var ga1 = await SeedAccountAsync(boot, "ga-1@examplium.com", globalAdmin: true);
        var ga2 = await SeedAccountAsync(boot, "ga-2@examplium.com", globalAdmin: true);
        await boot.Scheduler.ScheduleDeletionAsync(ga1);
        var now = DateTimeOffset.UtcNow;
        await ForceDueAsync(boot.Store, ga1, now);

        // The resident is promoted to LAST GlobalAdmin mid-grace-window:
        // ga2 is removed by the admin path (the lane's guard passes — two GAs
        // exist at that instant). After this, ga1 is the only GlobalAdmin
        // left, so the handler's lane call hits the 0142 D3 pin.
        await boot.Identity.DeleteAccountAsync(ga2, adminSubjectId: ga1);
        Assert.Null(await boot.UserManager.FindByIdAsync(ga2));

        // Drive the handler — the lane's last-GA guard (DeleteCoreAsync) throws
        // InvalidOperationException BEFORE any write, so the handler's catch
        // branch moves the row out of Pending (Cancelled, the honest "the
        // system refused at execution" state) and logs it (no PII — the IS08
        // gate: {ResidentId} is id-shaped, {Reason} is a fixed label).
        await DeletionDueHandler.Handle(
            new DeletionDueTick(), boot.Scheduler, boot.Identity, NullLogger.Instance);

        // The account is intact (the lane refused before any write — the fail-
        // closed pin) and the row moved to Cancelled (out of Pending).
        Assert.NotNull(await boot.UserManager.FindByIdAsync(ga1));
        var row = await DeletionRequestRowAsync(boot.Store, ga1);
        Assert.NotNull(row);
        Assert.Equal(DeletionRequestStatus.Cancelled, row!.Status);

        // The no-infinite-retry invariant: a second run does NOT re-execute
        // the cancelled row (it is no longer in the due list — ListDueAsync
        // filters on Pending). The handler re-yields but does nothing for it.
        await DeletionDueHandler.Handle(
            new DeletionDueTick(), boot.Scheduler, boot.Identity, NullLogger.Instance);
        Assert.NotNull(await boot.UserManager.FindByIdAsync(ga1));
        Assert.Equal(DeletionRequestStatus.Cancelled,
            (await DeletionRequestRowAsync(boot.Store, ga1))!.Status);
        Assert.Empty(await boot.Scheduler.ListDueAsync(now));
    }

    // ── Harness (the DeletionSchedulerTests boot shape, copied) ────────────

    private sealed record Boot
    {
        public required DeletionScheduler Scheduler { get; init; }
        public required IdentityService Identity { get; init; }
        public required UserManager<User> UserManager { get; init; }
        public required IDocumentStore Store { get; init; }
        public required DbContextOptions<AppDbContext> DbOptions { get; init; }
    }

    private async Task<Boot> BootAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);

        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            CoreDocTypes.Configure(opts);      // DeletionRequest, Profile, AccessAudit, …
            PostDocTypes.Configure(opts);      // GroupMembership, ComponentMembership (the lane's step 6/7)
            // The DeleteAccountAsync lane runs ResidentAccountRowsEraser,
            // which queries DeletionRequest — the NotificationDocTypes
            // surface must be registered or the query throws (the R11
            // DeletionSchedulerTests note, verbatim).
            NotificationDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(conn).Options;
        await using (var db = new AppDbContext(dbOptions))
        {
            await db.Database.MigrateAsync(ct);
        }

        var userStore = new UserStore<User, IdentityRole, AppDbContext, string,
            IdentityUserClaim<string>, IdentityUserRole<string>,
            IdentityUserLogin<string>, IdentityUserToken<string>,
            IdentityRoleClaim<string>>(new AppDbContext(dbOptions));

        var userManager = new UserManager<User>(
            userStore,
            Options.Create(new IdentityOptions { User = { RequireUniqueEmail = true } }),
            new PasswordHasher<User>(),
            new[] { new UserValidator<User>() },
            new[] { new PasswordValidator<User>() },
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new EmptyServiceProvider(),
            NullLogger<UserManager<User>>.Instance);

        var userInfo = new UserInfoService(store);

        var identity = new IdentityService(
            userManager:          userManager,
            documentStore:        store,
            userInfo:             userInfo,
            claimsSource:         Substitute.For<IClaimsSource>(),
            mailer:               Substitute.For<IMailerStage>(),
            verificationOptions:  Options.Create(new VerificationOptions()),
            logger:               NullLogger<IdentityService>.Instance,
            translationProvider:  null,
            notifications:        null,
            sampleDataOptions:    Options.Create(new SampleDataOptions { Enabled = false }));

        var scheduler = new DeletionScheduler(
            userManager:    userManager,
            documentStore:  store,
            logger:         NullLogger<DeletionScheduler>.Instance);

        return new Boot
        {
            Scheduler = scheduler,
            Identity = identity,
            UserManager = userManager,
            Store = store,
            DbOptions = dbOptions
        };
    }

    private async Task<string> SeedAccountAsync(Boot boot, string email, bool globalAdmin)
    {
        var ct = TestContext.Current.CancellationToken;
        var mgr = boot.UserManager;

        var user = new User { Id = Guid.NewGuid().ToString("N"), Email = email, UserName = email };
        await mgr.CreateAsync(user, "Sup3rSecret!");

        if (globalAdmin)
        {
            await using var db = new AppDbContext(boot.DbOptions);
            if (!await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                    .AnyAsync(db.Roles.Where(r => r.Name == Roles.GlobalAdmin), ct))
            {
                db.Roles.Add(new IdentityRole
                {
                    Name = Roles.GlobalAdmin,
                    NormalizedName = Roles.GlobalAdmin.ToUpperInvariant()
                });
                await db.SaveChangesAsync(ct);
            }
            await mgr.AddToRoleAsync(user, Roles.GlobalAdmin);
        }

        await using var session = boot.Store.LightweightSession();
        session.Store(new Profile
        {
            SubjectId = user.Id,
            DisplayName = "Test Resident",
            Verified = true,
            Email = email
        });
        await session.SaveChangesAsync(ct);

        return user.Id ?? string.Empty;
    }

    /// <summary>Pulls a pending row unambiguously past its <c>DueAt</c>
    /// (a 5-second margin) so the <c>DueAt &lt;= now</c> filter holds even
    /// after the DB's microsecond rounding — the test shortcut for the
    /// 14-day window (the <c>DeletionSchedulerTests</c> <c>ForceDueAsync</c>
    /// shape, verbatim).</summary>
    private static async Task ForceDueAsync(IDocumentStore store, string subjectId, DateTimeOffset dueBefore)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.LightweightSession();
        var row = await session.LoadAsync<DeletionRequest>(subjectId, ct);
        row.DueAt = dueBefore.AddSeconds(-5);
        session.Store(row);
        await session.SaveChangesAsync(ct);
    }

    private static async Task<DeletionRequest?> DeletionRequestRowAsync(IDocumentStore store, string subjectId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.QuerySession();
        return await session.LoadAsync<DeletionRequest>(subjectId, ct);
    }

    private static async Task<IReadOnlyList<AccessAudit>> AuditRows(
        IDocumentStore store, string? action = null)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.QuerySession();
        System.Linq.IQueryable<AccessAudit> query = session.Query<AccessAudit>();
        if (action is not null)
            query = query.Where(a => a.Action == action);
        return await Marten.QueryableExtensions.ToListAsync(query.OrderBy(a => a.At), ct);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
