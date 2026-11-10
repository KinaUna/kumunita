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
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// ADR 0301 (IMPROVE-UX R11) — the self-service deletion SCHEDULING lane
/// (<see cref="DeletionScheduler"/>): the grace-period doc + the
/// schedule/cancel/due-query seams above the
/// <see cref="IdentityService.DeleteAccountAsync"/> seam (which stays
/// unchanged — ADR 0301 D4).
/// </summary>
/// <list type="bullet">
/// <item><b>Schedule</b> writes one <see cref="DeletionRequest"/> row
/// (<c>Pending</c>, <c>DueAt</c> = <c>ScheduledAt</c> + the 14-day grace
/// constant) + exactly one <c>"account.delete.scheduled"</c> audit row
/// (<c>Via: Owner</c>). A re-schedule overwrites the same row (one row per
/// resident).</item>
/// <item><b>Last-GlobalAdmin</b> (ADR 0142 D3 / 0301 D2): a lone GlobalAdmin
/// is refused with <see cref="InvalidOperationException"/> BEFORE any write
/// (no doc row, no audit row).</item>
/// <item><b>Child rule</b> (ADR 0301 D3): an ACTIVE <see cref
/// "GuardianLink"/> for the subject → <see cref
/// "UnauthorizedAccessException"/> before any write. A <see cref
/// "GuardianLinkStatus.Dissolved"/> link does NOT block (only Active rows
/// confer standing, ADR 0028).</item>
/// <item><b>Cancel</b> (ADR 0301 D2 — "cancel = signing in") moves the row
/// to <c>Cancelled</c>; a second cancel with no pending row is a no-op (the
/// idempotent-resident-return shape).</item>
/// <item><b>Due seam</b> (R12's entry point): <see cref
/// "IDeletionScheduler.ListDueAsync"/> returns only <c>Pending</c> rows past
/// their <c>DueAt</c>; <see cref
/// "IDeletionScheduler.MarkExecutedAsync"/> marks the row
/// <c>Executed</c>.</item>
/// </list>
/// <para>
/// Pinned the same way <c>IdentityServiceDeleteAccountTests</c> is (via the
/// <see cref="AccessAudit"/> shape + the fail-closed pin + the doc row),
/// over a fresh scratch Postgres (the <see cref="PostgresFixture"/> harness).
/// </para>
/// </summary>
public sealed class DeletionSchedulerTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── Schedule (the happy path) ────────────────────────────────────────

    [Fact]
    public async Task ScheduleDeletionAsync_Resident_WritesPendingRowAndAuditRow()
    {
        var boot = await BootAsync();
        var resident = await SeedAccountAsync(boot, "resident@examplium.com", globalAdmin: false);

        var before = DateTimeOffset.UtcNow;
        await boot.Scheduler.ScheduleDeletionAsync(resident);

        // The doc row: Pending, one row per resident (SubjectId identity),
        // DueAt = ScheduledAt + the 14-day grace constant (ADR 0301 D2, the
        // IDeletionScheduler.GracePeriodDays interface const).
        var row = await DeletionRequestRowAsync(boot.Store, resident);
        Assert.NotNull(row);
        Assert.Equal(DeletionRequestStatus.Pending, row!.Status);
        Assert.True(row.ScheduledAt >= before, "ScheduledAt must be the schedule instant");
        var grace = (row.DueAt - row.ScheduledAt).Days;
        Assert.Equal(IDeletionScheduler.GracePeriodDays, grace);
        Assert.Equal(14, IDeletionScheduler.GracePeriodDays); // the ADR's proposed N

        // Exactly one "account.delete.scheduled" audit row — Via: Owner,
        // the ADR 0301 D1 "one summary" shape (distinct from the
        // execution "account.delete" verb).
        var audits = await AuditRows(boot.Store, action: "account.delete.scheduled");
        Assert.Single(audits);
        Assert.Equal(AccessVia.Owner, audits[0].Via);
        Assert.Equal(resident, audits[0].ActorId);
        Assert.Equal("account", audits[0].TargetKind);
        Assert.Equal(resident, audits[0].TargetId);
        Assert.Equal(AccessOutcome.Allow, audits[0].Outcome);

        // The execution verb was NOT written (the seam ran nothing).
        Assert.Empty(await AuditRows(boot.Store, action: "account.delete"));
    }

    [Fact]
    public async Task ScheduleDeletionAsync_Reschedule_OverwritesTheSameRow()
    {
        var boot = await BootAsync();
        var resident = await SeedAccountAsync(boot, "resident@examplium.com", globalAdmin: false);

        await boot.Scheduler.ScheduleDeletionAsync(resident);
        var first = await DeletionRequestRowAsync(boot.Store, resident);

        // A re-schedule (the resident changed their mind twice) resets the
        // SAME row — one row per resident (the GroupInvitation
        // "reset the existing row" business-key convention), with a fresh
        // DueAt (the grace window restarts).
        await boot.Scheduler.ScheduleDeletionAsync(resident);
        var second = await DeletionRequestRowAsync(boot.Store, resident);

        Assert.NotNull(second);
        Assert.Equal(DeletionRequestStatus.Pending, second!.Status);
        Assert.True(second.ScheduledAt >= first!.ScheduledAt, "re-schedule must not move the clock backwards");
        Assert.True(second.DueAt >= first.DueAt, "the grace window restarts on re-schedule");
        Assert.Equal(IDeletionScheduler.GracePeriodDays, (second.DueAt - second.ScheduledAt).Days);
    }

    [Fact]
    public async Task ScheduleDeletionAsync_MissingAccount_ThrowsAndDoesNotWrite()
    {
        var boot = await BootAsync();
        const string unknown = "no-such-account-id";

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => boot.Scheduler.ScheduleDeletionAsync(unknown));

        Assert.Null(await DeletionRequestRowAsync(boot.Store, unknown));
        Assert.Empty(await AuditRows(boot.Store, action: "account.delete.scheduled"));
    }

    // ── Last-GlobalAdmin pin (ADR 0142 D3 / 0301 D2) ─────────────────────

    [Fact]
    public async Task ScheduleDeletionAsync_LastGlobalAdmin_ThrowsAndDoesNotWrite()
    {
        var boot = await BootAsync();
        // A lone GlobalAdmin scheduling their own deletion — the last-GA
        // lockout pin refuses before any write (the OPS.md §9 "Hand over
        // admin" precedent: promote a second GA first).
        var self = await SeedAccountAsync(boot, "last-ga@examplium.com", globalAdmin: true);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => boot.Scheduler.ScheduleDeletionAsync(self));

        Assert.Null(await DeletionRequestRowAsync(boot.Store, self));
        Assert.Empty(await AuditRows(boot.Store, action: "account.delete.scheduled"));
        Assert.NotNull(await boot.UserManager.FindByIdAsync(self));
    }

    [Fact]
    public async Task ScheduleDeletionAsync_NotLastGlobalAdmin_Schedules()
    {
        var boot = await BootAsync();
        // Two GlobalAdmins exist — the pin is satisfied, the GlobalAdmin
        // may schedule (the self-serve lane is open to every resident; ADR
        // 0301 D1 amends 0142 D5).
        var self = await SeedAccountAsync(boot, "ga-1@examplium.com", globalAdmin: true);
        await SeedAccountAsync(boot, "ga-2@examplium.com", globalAdmin: true);

        await boot.Scheduler.ScheduleDeletionAsync(self);

        var row = await DeletionRequestRowAsync(boot.Store, self);
        Assert.NotNull(row);
        Assert.Equal(DeletionRequestStatus.Pending, row!.Status);
    }

    // ── Child rule (ADR 0301 D3) ─────────────────────────────────────────

    [Fact]
    public async Task ScheduleDeletionAsync_ActiveGuardianLink_ThrowsAndDoesNotWrite()
    {
        var boot = await BootAsync();
        var guardian = await SeedAccountAsync(boot, "guardian@examplium.com", globalAdmin: false);
        var child = await SeedAccountAsync(boot, "child@examplium.com", globalAdmin: false);
        await PlantGuardianLinkAsync(boot.Store, guardianId: guardian, childId: child,
            status: GuardianLinkStatus.Active);

        // A child under an active GuardianLink cannot self-delete (ADR 0301
        // D3) — refused with UnauthorizedAccessException BEFORE any write
        // (the distinct case R13 renders with the "contact a guardian"
        // notice).
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => boot.Scheduler.ScheduleDeletionAsync(child));

        Assert.Null(await DeletionRequestRowAsync(boot.Store, child));
        Assert.Empty(await AuditRows(boot.Store, action: "account.delete.scheduled"));
    }

    [Fact]
    public async Task ScheduleDeletionAsync_DissolvedGuardianLink_DoesNotBlock()
    {
        var boot = await BootAsync();
        var guardian = await SeedAccountAsync(boot, "guardian@examplium.com", globalAdmin: false);
        var child = await SeedAccountAsync(boot, "child@examplium.com", globalAdmin: false);
        await PlantGuardianLinkAsync(boot.Store, guardianId: guardian, childId: child,
            status: GuardianLinkStatus.Dissolved);

        // A Dissolved (or Declined / absent) link does NOT confer standing
        // (ADR 0028: only Active rows do) — the child may self-delete
        // (ADR 0301 D3).
        await boot.Scheduler.ScheduleDeletionAsync(child);

        var row = await DeletionRequestRowAsync(boot.Store, child);
        Assert.NotNull(row);
        Assert.Equal(DeletionRequestStatus.Pending, row!.Status);
    }

    // ── Cancel (ADR 0301 D2 — "cancel = signing in") ─────────────────────

    [Fact]
    public async Task CancelDeletionAsync_Pending_MovesToCancelled()
    {
        var boot = await BootAsync();
        var resident = await SeedAccountAsync(boot, "resident@examplium.com", globalAdmin: false);
        await boot.Scheduler.ScheduleDeletionAsync(resident);

        await boot.Scheduler.CancelDeletionAsync(resident);

        var row = await DeletionRequestRowAsync(boot.Store, resident);
        Assert.NotNull(row);
        Assert.Equal(DeletionRequestStatus.Cancelled, row!.Status);
        Assert.NotNull(row.CancelledAt);
    }

    [Fact]
    public async Task CancelDeletionAsync_NoPendingRow_IsANoop()
    {
        var boot = await BootAsync();
        var resident = await SeedAccountAsync(boot, "resident@examplium.com", globalAdmin: false);

        // Never scheduled: a no-op, not an error (the idempotent
        // resident-return shape).
        await boot.Scheduler.CancelDeletionAsync(resident);
        Assert.Null(await DeletionRequestRowAsync(boot.Store, resident));

        // A second cancel after a real one is also a no-op (the row is
        // already Cancelled — the resident returning twice is not an error).
        await boot.Scheduler.ScheduleDeletionAsync(resident);
        await boot.Scheduler.CancelDeletionAsync(resident);
        await boot.Scheduler.CancelDeletionAsync(resident);

        var row = await DeletionRequestRowAsync(boot.Store, resident);
        Assert.NotNull(row);
        Assert.Equal(DeletionRequestStatus.Cancelled, row!.Status);
    }

    // ── The due seam (R12's entry point) ─────────────────────────────────

    [Fact]
    public async Task ListDueAsync_ReturnsOnlyPendingPastDue_MarkExecutedMarksDone()
    {
        var boot = await BootAsync();
        var due = await SeedAccountAsync(boot, "due@examplium.com", globalAdmin: false);
        var notDue = await SeedAccountAsync(boot, "notdue@examplium.com", globalAdmin: false);
        var cancelled = await SeedAccountAsync(boot, "cancelled@examplium.com", globalAdmin: false);

        // due: pending past its DueAt. notDue: pending, future DueAt.
        // cancelled: scheduled then cancelled (never due).
        await boot.Scheduler.ScheduleDeletionAsync(due);
        await boot.Scheduler.ScheduleDeletionAsync(notDue);
        await boot.Scheduler.ScheduleDeletionAsync(cancelled);
        await boot.Scheduler.CancelDeletionAsync(cancelled);

        var now = DateTimeOffset.UtcNow;

        // A pending row whose DueAt is in the future is NOT due.
        var dueRows = await boot.Scheduler.ListDueAsync(now);
        Assert.Empty(dueRows);

        // Force the due row past its DueAt (the test shortcut for the
        // 14-day window — R12's job would run days later). A 5-second margin
        // so the Marten DB round-trip (timestamptz microsecond rounding) can
        // never tip DueAt past `now` — the filter is `DueAt <= now`.
        await ForceDueAsync(boot.Store, due, now);
        dueRows = await boot.Scheduler.ListDueAsync(now);
        var ids = dueRows.Select(r => r.SubjectId).ToList();
        Assert.Single(dueRows);
        Assert.Equal(due, ids[0]);

        // MarkExecutedAsync (the R12 due-job calls this after the
        // DeleteAccountAsync lane ran — R12 owns the lane call; R11 owns
        // the mark-done seam) moves the row to Executed.
        await boot.Scheduler.MarkExecutedAsync(due);
        var executed = await DeletionRequestRowAsync(boot.Store, due);
        Assert.NotNull(executed);
        Assert.Equal(DeletionRequestStatus.Executed, executed!.Status);
        Assert.NotNull(executed.ExecutedAt);

        // The due list no longer returns the executed row (it is no longer
        // Pending).
        Assert.Empty(await boot.Scheduler.ListDueAsync(now));

        // The cancelled row is untouched (it never ran).
        Assert.NotNull(await DeletionRequestRowAsync(boot.Store, cancelled));
        Assert.Equal(DeletionRequestStatus.Cancelled, (await DeletionRequestRowAsync(boot.Store, cancelled))!.Status);
    }

    [Fact]
    public async Task MarkExecutedAsync_NoPendingRow_IsANoop()
    {
        var boot = await BootAsync();
        var resident = await SeedAccountAsync(boot, "resident@examplium.com", globalAdmin: false);

        // No pending row (never scheduled) — a no-op, not an error (the
        // R12 idempotent shape: a double-run of the due-job must not throw).
        await boot.Scheduler.MarkExecutedAsync(resident);
        Assert.Null(await DeletionRequestRowAsync(boot.Store, resident));
    }

    // ── Harness ─────────────────────────────────────────────────────────

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
            CoreDocTypes.Configure(opts);
            PostDocTypes.Configure(opts);
            // IS22 gotcha (same as IdentityServiceDeleteAccountTests): the
            // DeleteAccountAsync lane runs ResidentAccountRowsEraser, which
            // now also queries DeletionRequest — the NotificationDocTypes
            // surface must be registered or the query throws.
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

    private static async Task PlantGuardianLinkAsync(
        IDocumentStore store, string guardianId, string childId, GuardianLinkStatus status)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.LightweightSession();
        session.Store(new GuardianLink
        {
            Id = Guid.NewGuid().ToString("N"),
            GuardianId = guardianId,
            ChildId = childId,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            DissolvedAt = status is GuardianLinkStatus.Dissolved ? DateTimeOffset.UtcNow : null,
            DissolvedBy = status is GuardianLinkStatus.Dissolved ? guardianId : null
        });
        await session.SaveChangesAsync(ct);
    }

    private static async Task ForceDueAsync(IDocumentStore store, string subjectId, DateTimeOffset dueBefore)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.LightweightSession();
        var row = await session.LoadAsync<DeletionRequest>(subjectId, ct);
        // Pull the DueAt unambiguously into the past (a 5-second margin) so the
        // `DueAt <= now` filter holds even after the DB's microsecond rounding.
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
