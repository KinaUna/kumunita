using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Kumunita.Core.Messaging;
using Kumunita.Core.UserInfo;
using Marten;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M9 amendment — the per-resident + guardian messaging controls, the
/// <b>Core-seam</b> half (the Web-half pins live in
/// <c>Kumunita.Web.Tests/MessagesControllerTests.cs</c> + a new
/// <c>MessagingSettingsControllerTests.cs</c>). The shape follows
/// <see cref="MessagingServiceTests"/> (the M9 toggle pins, U02/U03) and
/// <see cref="GuardianControlsTests"/> (the GU guardian-lane pins, U02–U06):
/// the same <see cref="PostgresFixture"/> / <c>BootStoreAsync</c> shape, the
/// same <c>AuditRows</c> / <c>LastAuditAsync</c> helper idiom, fresh scratch
/// Postgres per test method.
/// <para>
/// The amendment adds <b>three</b> Core seams over M9's instance-level
/// toggle (ADR 0105):
/// <list type="number">
/// <item><see cref="IUserInfoService.SetMessagingOptInAsync"/> — the
/// resident's own opt-in write lane (the <c>CompleteOnboardingAsync</c>
/// owner-scope shape; no audit row — "not an access decision").</item>
/// <item><see cref="IUserInfoService.SetChildMessagingRestrictionAsync"/> —
/// the guardian's ceiling write lane (the <c>SuspendChildAsync</c>
/// guardian-scope shape; a <c>guardian.messaging_restrict</c> audit row,
/// <c>Via: Guardian</c>).</item>
/// <item><see cref="IMessagingService.IsMessagingAllowedForAsync"/> — the
/// per-actor composite gate read (instance master gate ∧ the resident's
/// own opt-in ∧ ¬the guardian's ceiling; fail-closed on a missing
/// profile or an absent directory seam).</item>
/// </list>
/// The composition rule (the user's chosen model) is the load-bearing
/// invariant pinned here: the instance toggle is the <b>master gate</b>
/// (off ⇒ no one, regardless of opt-in), the resident's opt-in is the
/// <b>individual choice</b> (on = may message; the ADR 0105 privacy-
/// sensitive default-<c>false</c> convention), and the guardian's
/// restriction is the <b>ceiling</b> (a hard veto over the child's own
/// opt-in; the allowance merely lifts the veto and defers to the child).
/// </para>
/// </summary>
public class MessagingPerActorGateTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── 1 — the resident's own opt-in write lane (no audit row, C3) ─────

    /// <summary>
    /// <see cref="IUserInfoService.SetMessagingOptInAsync"/> flips
    /// <see cref="Profile.MessagingOptIn"/> and is live on the very next
    /// <see cref="IUserInfoService.GetProfileAsync"/> read (C4 strong
    /// consistency). No <see cref="AccessAudit"/> row (a profile-field
    /// write — the <c>UpsertProfileAsync</c> "not an access decision"
    /// shape, the <c>CompleteOnboardingAsync</c> pin verbatim). A missing
    /// profile is a fail-closed <c>KeyNotFoundException</c> (never
    /// load-or-create, the <c>CompleteOnboardingAsync</c> pin).
    /// </summary>
    [Fact]
    public async Task SetMessagingOptIn_FlipsFlag_NoAuditRow_LiveOnNextRead()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);
        const string resident = "u-ma1-resident";

        await SeedProfileAsync(store, resident);

        // Default floor (the ADR 0105 opt-in default-<c>false</c>
        // convention): a fresh profile has messaging opted out.
        Assert.False((await svc.GetProfileAsync(resident))!.MessagingOptIn);

        // Opt in — live on the very next read.
        await svc.SetMessagingOptInAsync(resident, true, resident);
        Assert.True((await svc.GetProfileAsync(resident))!.MessagingOptIn);

        // Opt back out — the value round-trips.
        await svc.SetMessagingOptInAsync(resident, false, resident);
        Assert.False((await svc.GetProfileAsync(resident))!.MessagingOptIn);

        // No audit row (a profile-field write — "not an access decision").
        Assert.Equal(0, await CountAuditsAsync(store));
    }

    /// <summary>
    /// <see cref="IUserInfoService.SetMessagingOptInAsync"/> on a subject
    /// with no <see cref="Profile"/> row is a fail-closed
    /// <c>KeyNotFoundException</c> — the lane never load-or-creates (the
    /// <c>CompleteOnboardingAsync</c> pin).
    /// </summary>
    [Fact]
    public async Task SetMessagingOptIn_MissingProfile_FailsClosed_NoCreate()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);
        const string stranger = "u-ma1-stranger";   // no profile planted

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.SetMessagingOptInAsync(stranger, true, stranger));

        // Never load-or-create: no profile row appeared.
        Assert.Null(await svc.GetProfileAsync(stranger));
    }

    // ── 2 — the guardian's ceiling write lane (audit row, Via: Guardian) ──

    /// <summary>
    /// <see cref="IUserInfoService.SetChildMessagingRestrictionAsync"/>
    /// flips <see cref="Profile.MessagingRestricted"/> and is live on the
    /// very next read (C4). The audit row is
    /// <c>guardian.messaging_restrict</c> / <c>Via: Guardian</c> /
    /// <c>Outcome: Allow</c> (the <c>guardian.suspend</c> /
    /// <c>guardian.unsuspend</c> shape, the GU G·2 pin carried to the
    /// messaging lane). The ceiling round-trips (restrict → allow →
    /// restrict).
    /// </summary>
    [Fact]
    public async Task SetChildMessagingRestriction_FlipsCeiling_AuditedViaGuardian()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);
        const string guardian = "u-ma2-guardian";
        const string child = "u-ma2-child";

        await SeedProfileAsync(store, child);
        await svc.CreateGuardianLinkAsync(child, guardian);

        // Default floor: a fresh child is not restricted (the ceiling is
        // off; the child's own opt-in decides).
        Assert.False((await svc.GetProfileAsync(child))!.MessagingRestricted);

        // Restrict — the ceiling is live on the very next read.
        await svc.SetChildMessagingRestrictionAsync(child, true, guardian);
        Assert.True((await svc.GetProfileAsync(child))!.MessagingRestricted);

        // Allow — the ceiling lifts (the child's own opt-in now decides).
        await svc.SetChildMessagingRestrictionAsync(child, false, guardian);
        Assert.False((await svc.GetProfileAsync(child))!.MessagingRestricted);

        // The audit row (guardian.messaging_restrict, Via: Guardian).
        var row = await LastAuditAsync(store, "guardian.messaging_restrict", child);
        Assert.Equal(AccessVia.Guardian, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
        Assert.Equal(guardian, row.ActorId);
    }

    /// <summary>
    /// <see cref="IUserInfoService.SetChildMessagingRestrictionAsync"/> on
    /// a (guardian, child) pair with <b>no active link</b> is a fail-closed
    /// <see cref="UnauthorizedAccessException"/> (the GU G·3 deny-by-default
    /// pin — the Web surfaces a 404). The ceiling is <b>not</b> written.
    /// </summary>
    [Fact]
    public async Task SetChildMessagingRestriction_NoActiveLink_Refused_NoWrite()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);
        const string stranger = "u-ma2-stranger";   // no link over the child
        const string child = "u-ma2-child";

        await SeedProfileAsync(store, child);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.SetChildMessagingRestrictionAsync(child, true, stranger));

        // The ceiling is untouched (the refuse fired before the write).
        Assert.False((await svc.GetProfileAsync(child))!.MessagingRestricted);
    }

    // ── 3 — the per-actor composite gate (the load-bearing invariant) ────

    /// <summary>
    /// <see cref="IMessagingService.IsMessagingAllowedForAsync"/> is the
    /// <b>composite</b> of the three M9-amendment halves. The load-bearing
    /// case: <b>instance on ∧ the resident opted in ∧ not restricted ⇒
    /// allowed</b>. A resident who has opted in is messaging-allowed when
    /// the instance master gate is on.
    /// </summary>
    [Fact]
    public async Task IsMessagingAllowedFor_InstanceOn_OptIn_Allowed()
    {
        var (store, userInfo, messaging) = await BootWithUserInfoAsync();
        const string resident = "u-ma3-resident";

        await SeedProfileAsync(store, resident, optIn: true);
        await messaging.SetMessagingEnabledAsync(true, "u-admin");   // instance master gate on

        Assert.True(await messaging.IsMessagingAllowedForAsync(resident));
    }

    /// <summary>
    /// The <b>master-gate veto</b> (the user's chosen combination rule,
    /// case 1): instance off ⇒ <b>no one</b> is allowed, even a resident
    /// who has opted in. The per-user opt-in is a choice made <i>under</i>
    /// the instance's umbrella, not an override of it.
    /// </summary>
    [Fact]
    public async Task IsMessagingAllowedFor_InstanceOff_OptIn_NotAllowed()
    {
        var (store, userInfo, messaging) = await BootWithUserInfoAsync();
        const string resident = "u-ma3-resident";

        await SeedProfileAsync(store, resident, optIn: true);
        // The instance master gate stays at its default-<c>false</c> floor
        // (a fresh instance ships with messaging off, the ADR 0105 pin).
        Assert.False(await messaging.IsMessagingEnabledAsync());

        Assert.False(await messaging.IsMessagingAllowedForAsync(resident));
    }

    /// <summary>
    /// The <b>opt-in floor</b> (the ADR 0105 privacy-sensitive
    /// default-<c>false</c> convention): instance on but the resident has
    /// <b>not</b> opted in ⇒ not allowed. The master gate opens the
    /// surface; the individual choice decides participation.
    /// </summary>
    [Fact]
    public async Task IsMessagingAllowedFor_InstanceOn_NotOptedIn_NotAllowed()
    {
        var (store, userInfo, messaging) = await BootWithUserInfoAsync();
        const string resident = "u-ma3-resident";

        await SeedProfileAsync(store, resident, optIn: false);   // the default floor
        await messaging.SetMessagingEnabledAsync(true, "u-admin");

        Assert.False(await messaging.IsMessagingAllowedForAsync(resident));
    }

    /// <summary>
    /// The <b>guardian's ceiling veto</b> (the user's chosen combination
    /// rule, case 3): instance on ∧ the child opted in ∧ the guardian has
    /// restricted ⇒ <b>not</b> allowed. The restriction always wins over
    /// the child's own opt-in (a hard ceiling, the normal parental-
    /// restriction model).
    /// </summary>
    [Fact]
    public async Task IsMessagingAllowedFor_ChildOptedIn_GuardianRestricted_NotAllowed()
    {
        var (store, userInfo, messaging) = await BootWithUserInfoAsync();
        const string guardian = "u-ma3-guardian";
        const string child = "u-ma3-child";

        await SeedProfileAsync(store, child, optIn: true);
        await messaging.SetMessagingEnabledAsync(true, "u-admin");   // instance on
        await userInfo.CreateGuardianLinkAsync(child, guardian);     // the standing the ceiling write needs
        await userInfo.SetChildMessagingRestrictionAsync(child, true, guardian);   // ceiling on

        // The child's own opt-in is on, but the guardian's ceiling wins.
        Assert.True((await userInfo.GetProfileAsync(child))!.MessagingOptIn);
        Assert.False(await messaging.IsMessagingAllowedForAsync(child));
    }

    /// <summary>
    /// The <b>allowance lifts the veto</b> (the user's chosen combination
    /// rule, case 3 converse): instance on ∧ the child opted in ∧ the
    /// guardian allows ⇒ allowed. The ceiling is <c>false</c>, so the
    /// child's own opt-in decides (allowed).
    /// </summary>
    [Fact]
    public async Task IsMessagingAllowedFor_ChildOptedIn_GuardianAllows_Allowed()
    {
        var (store, userInfo, messaging) = await BootWithUserInfoAsync();
        const string guardian = "u-ma3-guardian";
        const string child = "u-ma3-child";

        await SeedProfileAsync(store, child, optIn: true);
        await messaging.SetMessagingEnabledAsync(true, "u-admin");   // instance on
        await userInfo.CreateGuardianLinkAsync(child, guardian);     // the standing the ceiling write needs
        await userInfo.SetChildMessagingRestrictionAsync(child, false, guardian);   // ceiling off

        Assert.True(await messaging.IsMessagingAllowedForAsync(child));
    }

    /// <summary>
    /// The <b>fail-closed floor</b>: a signed-in resident with <b>no
    /// profile row</b> (or a harness without the directory seam) is
    /// messaging-not-allowed, even with the instance on — the floor is
    /// "no messaging", the ADR 0105 opt-in default-<c>false</c>
    /// convention carried to the per-actor half. This is the load-bearing
    /// pin against a "default-open" drift.
    /// </summary>
    [Fact]
    public async Task IsMessagingAllowedFor_MissingProfile_FailsClosed()
    {
        var (store, userInfo, messaging) = await BootWithUserInfoAsync();
        const string stranger = "u-ma3-stranger";   // no profile planted

        await messaging.SetMessagingEnabledAsync(true, "u-admin");   // instance on

        // No profile ⇒ the opt-in cannot be verified ⇒ not allowed.
        Assert.Null(await userInfo.GetProfileAsync(stranger));
        Assert.False(await messaging.IsMessagingAllowedForAsync(stranger));
    }

    // ── Shared helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a minimal verified <see cref="Profile"/> for the resident with
    /// a chosen <see cref="Profile.MessagingOptIn"/> value (the
    /// <c>SeedChildProfileAsync</c> shape in <see cref="GuardianControlsTests"/>,
    /// the M9-amendment half).
    /// </summary>
    private static async Task SeedProfileAsync(IDocumentStore store, string subjectId, bool optIn = false)
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(new Profile
        {
            SubjectId = subjectId,
            DisplayName = "Resident",
            Verified = true,
            Blocked = false,
            Visibility = new Audience(),
            MessagingOptIn = optIn,
        });
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Boots a store + a live <see cref="IUserInfoService"/> (the
    /// directory seam the gate reads) + a <see cref="MessagingService"/>
    /// composed over both (the <c>BootStoreWithNotificationsAsync</c> shape
    /// in <see cref="MessagingServiceTests"/>, minus the nudge lane — the
    /// gate read does not touch the notification surface).
    /// </summary>
    private async Task<(IDocumentStore store, IUserInfoService userInfo, IMessagingService messaging)>
        BootWithUserInfoAsync()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var messaging = new MessagingService(store, userInfo);
        return (store, userInfo, messaging);
    }

    private static async Task<int> CountAuditsAsync(IDocumentStore store)
    {
        await using var session = store.QuerySession();
        return await session.Query<AccessAudit>()
            .CountAsync(TestContext.Current.CancellationToken);
    }

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

    private async Task<IDocumentStore> BootStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);   // LocaleSettings + AccessAudit
            M6DocTypes.Configure(opts);   // Notification (the M9 nudge table)
            M9DocTypes.Configure(opts);   // Conversation + Message
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }
}
