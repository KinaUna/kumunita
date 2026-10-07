using System.Reflection;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M28 (ADR 0151, D4) — the three <see cref="IUserInfoService"/> guardian
/// time-limit seams' <b>7 pinned standing/audit tests</b> (design doc §2.6,
/// items 8–14). These assert the U03 seams on the GU surface (ADR 0028):
/// the standing gate (an active <see cref="GuardianLink"/> ∪ GlobalAdmin,
/// G·2/G·5), the <b>one</b> <see cref="AccessAudit"/> row on the write
/// (verb <c>guardian.time-limit.set</c>, <c>Via</c> Guardian/Admin), the
/// floor (no schedule / null = never restricted, C-M28·3), the strong
/// consistency (a save is live on the next read, C-M28·4), the no-self-lane
/// denial (a child cannot set their own, C-M28·7), and the C-M28·5 structural
/// pin (the frozen <see cref="IAuthorizationService"/> 4-method surface is
/// unchanged — zero new authorization surface).
/// <para>
/// The gate is **deny-by-default** (G·2): a non-guardian, non-admin —
/// including a child acting on their own <c>childId</c> (C-M28·7) — is denied
/// with <see cref="UnauthorizedAccessException"/> (the Web's 404). The GlobalAdmin
/// safety valve (G·5) is exercised by resolving the <see
/// cref="IIdentityService"/> lazily off the <see cref="IServiceProvider"/>
/// seam (the <c>NotificationService</c> emission-shape, always resolvable, no
/// construction cycle) — the 7 non-admin tests build
/// <c>UserInfoService(store)</c> positionally (services null ⇒ no admin
/// standing, exactly as they get no emission).
/// </para>
/// <para>
/// Authority: design doc §2.1 (the three-seam pin), §2.6 (the 7 test names),
/// C-M28·3/4/5/7, ADR 0028 §E (the audit-verb list), and the M20
/// <c>SetQuietScheduleAsync</c> "clear" idiom (ADR 0121 D2). The names are the
/// contract; a different name is a drift pause, not a silent rename.
/// </para>
/// </summary>
public class GuardianTimeLimitStandingTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── F5 / C-M28·4 · C-M28·7 — the gate is deny-by-default ─────────────────
    // A non-guardian, non-admin (no active link, no GlobalAdmin standing)
    // calling SetChildTimeLimitAsync is DENIED (UnauthorizedAccessException,
    // the Web's 404). The no-standing gate fires before any row read/write.

    [Fact]
    public async Task F5_NonGuardian_Set_Is_Denied()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);   // services null ⇒ no admin standing

        const string stranger = "u-m28-ng-stranger";
        const string child = "u-m28-ng-child";

        // The (stranger, child) pair has NO active link; stranger is not a
        // GlobalAdmin (services null). The gate denies before any write.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.SetChildTimeLimitAsync(stranger, child,
                new GuardianTimeLimitSchedule { Enabled = true, Mode = TimeLimitMode.Blocked, Hours = [22, 23] }));
        // The read seam denies on the same gate.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.GetChildTimeLimitAsync(stranger, child));

        // Nothing was written (the gate fires before the row touch).
        Assert.Null(await svc.GetActiveTimeLimitAsync(child));
    }

    // ── F5 / C-M28·4 — a guardian with an active link writes; one audit row ──
    // SetChildTimeLimitAsync (guardian lane) appends EXACTLY ONE AccessAudit
    // row (verb guardian.time-limit.set, Via: Guardian, TargetKind the child
    // account, TargetId the child) in the same transaction (C3). Strong
    // consistency: the row is live on the next read (C-M28·4).

    [Fact]
    public async Task F5_Guardian_Set_EmitsOneAuditRow_ViaGuardian()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string guardian = "u-m28-gs-guardian";
        const string child = "u-m28-gs-child";

        await svc.CreateGuardianLinkAsync(child, guardian);

        await svc.SetChildTimeLimitAsync(guardian, child,
            new GuardianTimeLimitSchedule { Enabled = true, Mode = TimeLimitMode.Blocked, Hours = [22, 23] });

        // The row is live on the very next read (C-M28·4).
        var read = await svc.GetActiveTimeLimitAsync(child);
        Assert.NotNull(read);
        Assert.True(read!.Enabled);
        Assert.Equal(TimeLimitMode.Blocked, read.Mode);
        Assert.Equal([22, 23], read.Hours);

        // EXACTLY ONE audit row for the write (the GU "one audited row" pin).
        var count = await CountAuditAsync(store, "guardian.time-limit.set", child);
        Assert.Equal(1, count);

        // The row: Via: Guardian (the guardian's own standing), targeting the child.
        var row = await LastAuditAsync(store, "guardian.time-limit.set", child);
        Assert.Equal(AccessVia.Guardian, row.Via);
        Assert.Equal(guardian, row.ActorId);
        Assert.Equal("profile", row.TargetKind);
        Assert.Equal(child, row.TargetId);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
    }

    // ── F5 / C-M28·4, G·5 — the GlobalAdmin safety valve (no link) ───────────
    // A GlobalAdmin who holds NO active GuardianLink over the child still
    // writes: the G·5 valve confers Via: Admin. This is the D4 "active link
    // ∪ GlobalAdmin" union — the admin is the ceiling the guardian lane
    // cannot reach.

    [Fact]
    public async Task F5_GlobalAdmin_Set_EmitsOneAuditRow_ViaAdmin()
    {
        var store = await BootStoreAsync();

        const string admin = "u-m28-ga-admin";     // a GlobalAdmin — NOT the GuardianId
        const string child = "u-m28-ga-child";

        // No active link: the standing must come from the GlobalAdmin valve.
        // Resolve the IIdentityService lazily off the IServiceProvider seam
        // (the NotificationService emission-shape), so IsGlobalAdminAsync sees
        // the admin's role.
        var identity = Substitute.For<IIdentityService>();
        identity.GetBySubjectAsync(admin).Returns(Task.FromResult<ThinPrincipal?>(
            new ThinPrincipal(admin, null, true, new[] { Roles.GlobalAdmin })));
        var sp = new ServiceCollection().AddSingleton(identity).BuildServiceProvider();

        var svc = new UserInfoService(store, sp);

        await svc.SetChildTimeLimitAsync(admin, child,
            new GuardianTimeLimitSchedule { Enabled = true, Mode = TimeLimitMode.Allowed, Hours = [8, 17] });

        var row = await LastAuditAsync(store, "guardian.time-limit.set", child);
        Assert.Equal(AccessVia.Admin, row.Via);      // the G·5 valve
        Assert.Equal(admin, row.ActorId);
        Assert.Equal(child, row.TargetId);

        // Strong consistency (C-M28·4): the admin's write is live on the read.
        var read = await svc.GetActiveTimeLimitAsync(child);
        Assert.Equal(TimeLimitMode.Allowed, read!.Mode);
    }

    // ── C-M28·4 — strong consistency: a save is live on the very next read ───
    // A guardian sets a schedule; GetActiveTimeLimitAsync (the ENFORCEMENT
    // read, child-keyed, no guardian gate) sees the exact value on the next
    // call — no projection, no cache (the ADR 0050 IsSignupOpen shape).

    [Fact]
    public async Task F5_Set_Is_StronglyConsistent_NextReadSeesNewValue()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string guardian = "u-m28-sc-guardian";
        const string child = "u-m28-sc-child";

        await svc.CreateGuardianLinkAsync(child, guardian);

        // Before: no schedule (the floor).
        Assert.Null(await svc.GetActiveTimeLimitAsync(child));

        // Set a distinct, recognizable schedule.
        await svc.SetChildTimeLimitAsync(guardian, child,
            new GuardianTimeLimitSchedule { Enabled = true, Mode = TimeLimitMode.Blocked, Hours = [0, 1, 2], DaysOfWeek = [1, 2] });

        // The ENFORCEMENT read (no guardian gate) sees the exact value — live
        // on the very next call (C-M28·4).
        var read = await svc.GetActiveTimeLimitAsync(child);
        Assert.NotNull(read);
        Assert.True(read!.Enabled);
        Assert.Equal([0, 1, 2], read.Hours);
        Assert.Equal([1, 2], read.DaysOfWeek);

        // A second save overwrites the same singleton-per-child row (upsert) —
        // still one row, the new value.
        await svc.SetChildTimeLimitAsync(guardian, child,
            new GuardianTimeLimitSchedule { Enabled = true, Mode = TimeLimitMode.Allowed, Hours = [9, 10] });
        var read2 = await svc.GetActiveTimeLimitAsync(child);
        Assert.Equal(TimeLimitMode.Allowed, read2!.Mode);
        Assert.Equal([9, 10], read2.Hours);
    }

    // ── C-M28·7 — no self-lane: a child cannot set their own time limit ──────
    // The child's OWN subject id used as guardianId on their own childId is
    // DENIED (deny-by-default — a child holds no active link as GuardianId and
    // is never a GlobalAdmin). The only write is the guardian-gated (∪
    // GlobalAdmin) seam; there is no owner-scope self-service lane.

    [Fact]
    public async Task C_M28_7_Child_Cannot_Set_Own_TimeLimit()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);   // services null ⇒ no admin standing

        const string child = "u-m28-self-child";

        // The child acting on THEMSELVES (guardianId == childId) is denied.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.SetChildTimeLimitAsync(child, child,
                new GuardianTimeLimitSchedule { Enabled = true, Mode = TimeLimitMode.Blocked, Hours = [22] }));
        // …and the read seam denies the self-read too.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.GetChildTimeLimitAsync(child, child));

        // Nothing written (the gate fires before the row touch).
        Assert.Null(await svc.GetActiveTimeLimitAsync(child));
    }

    // ── C-M28·3 / D4 — clear (null) deletes the row ──────────────────────────
    // SetChildTimeLimitAsync(guardianId, childId, null) DELETES the child's
    // row (the M20 SetQuietScheduleAsync "clear" idiom, session.Delete) —
    // GetActiveTimeLimitAsync then returns null = "never restricted" (the
    // floor). The clear still appends its one audit row (the write is audited
    // regardless of set-vs-clear).

    [Fact]
    public async Task F5_Clear_SetsNull_DeletesRow()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string guardian = "u-m28-cl-guardian";
        const string child = "u-m28-cl-child";

        await svc.CreateGuardianLinkAsync(child, guardian);

        // Set a schedule first (a row exists).
        await svc.SetChildTimeLimitAsync(guardian, child,
            new GuardianTimeLimitSchedule { Enabled = true, Mode = TimeLimitMode.Blocked, Hours = [22, 23] });
        Assert.NotNull(await svc.GetActiveTimeLimitAsync(child));

        // Clear (null) — the row is deleted (the floor, C-M28·3).
        await svc.SetChildTimeLimitAsync(guardian, child, null);

        // GetActiveTimeLimitAsync returns null = "never restricted".
        Assert.Null(await svc.GetActiveTimeLimitAsync(child));

        // The clear is still an audited write (one row for each set-and-clear).
        var count = await CountAuditAsync(store, "guardian.time-limit.set", child);
        Assert.Equal(2, count);   // one for the set, one for the clear
    }

    // ── C-M28·5 — the structural pin: zero new authorization surface ─────────
    // M28 must not add a method to the frozen IAuthorizationService surface.
    // Reflect over the interface and assert the distinct public instance method
    // names are EXACTLY the frozen four — no new signature, no new AccessAction
    // / AccessVia, no Decide() branch (C-M28·5, the drift-guard).

    [Fact]
    public void C_M28_5_IAuthorizationService_Surface_Count_Unchanged()
    {
        var names = typeof(IAuthorizationService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .Distinct()
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        // The frozen 4-method surface (the ADR 0006-D four signatures): no
        // M28 method may have been added.
        Assert.Equal(
            new[] { "CanAsync", "CanSeeAsync", "CanSeeGroupAsync", "CanSeeGroupFeedAsync" },
            names);
    }

    // ── Shared helpers ──────────────────────────────────────────────────────

    private static async Task<AccessAudit> LastAuditAsync(IDocumentStore store, string action, string targetId)
    {
        await using var session = store.QuerySession();
        var row = await session.Query<AccessAudit>()
            .Where(a => a.Action == action && a.TargetId == targetId)
            .OrderByDescending(a => a.At)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(row);
        return row!;
    }

    private static async Task<int> CountAuditAsync(IDocumentStore store, string action, string targetId)
    {
        await using var session = store.QuerySession();
        return await session.Query<AccessAudit>()
            .Where(a => a.Action == action && a.TargetId == targetId)
            .CountAsync(TestContext.Current.CancellationToken);
    }

    private async Task<IDocumentStore> BootStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }
}
