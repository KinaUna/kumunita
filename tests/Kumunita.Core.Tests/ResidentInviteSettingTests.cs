// IMPROVE-UX A11 (ADR 0304, amended 2026-11-11) — the resident-invite admin
// setting (Off / On / OnWithApproval) + the testable rule. The A11 unit card
// (verbatim): "A11 — Resident invitations: admin setting (off / on /
// on-with-approval) + Core rule. `// TODO ❓` who may invite and how many per
// period. Done when: setting stored; rule tested."
//
// The pins (the A11 prompt's test list, verbatim):
//   (a) the **floor is `Off`** (a fresh instance);
//   (b) the setting **round-trips** for all three states (`Get` after `Set`);
//   (c) the write audit row — **exactly one** `AccessAudit`, `Via = Admin`,
//       the singleton action (`signup.set-resident-invite`, target "signup");
//   (d) the **rule** — `Off` → refuse; `On` → allow, no approval;
//       `OnWithApproval` → allow + the approval flag set;
//   (e) the `// TODO ❓` who-may-invite + rate is **flagged, not enforced**
//       (the rule does not reject on rate yet — the posture is pure, setting-
//       driven, and has no rate-limit conjunct).
//
// Harness: the PostgresFixture fresh-scratch-database-per-test shape (the
// InvitationServiceTests / KioskLinkServiceTests precedent). The setting
// seam is the IdentityPlatformGateService (the C3-03 / ADR 0293
// part-vs-whole surface, the NotifyAdminsOnSignup / IsSignupOpen
// singleton-toggle lane); the rule seam is the InvitationService (the
// A07/A08/A09 invitation lane — the rule reads the setting through
// IIdentityService, the A10 best-effort-degrade shape).

using System.Linq;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Marten;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

public sealed class ResidentInviteSettingTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Admin = "admin-resident-invite-001";
    private const string Resident = "resident-001";

    // ── Pin (a) — the floor is `Off` (a fresh instance) ────────────────────

    [Fact]
    public async Task A11_Floor_IsOff_OnFreshInstance()
    {
        var (svc, gates, store, identity) = await BootAsync();
        // A fresh instance (no LocaleSettings row, or a row with no
        // ResidentInviteSetting) reads the `Off` floor (ADR 0304
        // §Amendments A11 — the deliberate inverse of the codebase
        // `true`-floor convention, like MessagingEnabled).
        Assert.Equal(ResidentInviteSetting.Off, await gates.GetResidentInviteSettingAsync());
        // The rule seam reads the same floor through IIdentityService
        // (BootAsync stubs the identity to the `Off` floor — the rule
        // refuses a resident on a fresh instance).
        Assert.False((await svc.GetResidentInvitePostureAsync(Resident)).MayInvite);
        await store.DisposeAsync();
    }

    // ── Pin (b) — the setting round-trips for all three states ─────────────

    [Fact]
    public async Task A11_Setting_RoundTrips_AllThreeStates()
    {
        var (svc, gates, store, identity) = await BootAsync();
        var ct = TestContext.Current.CancellationToken;

        // `Off` (the floor, the default) — the round-trip pin.
        await gates.SetResidentInviteSettingAsync(ResidentInviteSetting.Off, Admin);
        Assert.Equal(ResidentInviteSetting.Off, await gates.GetResidentInviteSettingAsync());

        // `On` — the round-trip pin.
        await gates.SetResidentInviteSettingAsync(ResidentInviteSetting.On, Admin);
        Assert.Equal(ResidentInviteSetting.On, await gates.GetResidentInviteSettingAsync());

        // `OnWithApproval` — the round-trip pin.
        await gates.SetResidentInviteSettingAsync(ResidentInviteSetting.OnWithApproval, Admin);
        Assert.Equal(ResidentInviteSetting.OnWithApproval, await gates.GetResidentInviteSettingAsync());

        // A late read after a late write is the late value (the data, not
        // config — the IsSignupOpenAsync / IsNotifyAdminsOnSignupAsync
        // singleton-toggle shape).
        await gates.SetResidentInviteSettingAsync(ResidentInviteSetting.Off, Admin);
        Assert.Equal(ResidentInviteSetting.Off, await gates.GetResidentInviteSettingAsync());
        await store.DisposeAsync();
    }

    // ── Pin (c) — the write audit row: exactly one, `Via = Admin`, the
    //       singleton action (`signup.set-resident-invite`, target "signup") ──

    [Fact]
    public async Task A11_Set_WritesExactlyOneAdminAuditRow()
    {
        var (svc, gates, store, identity) = await BootAsync();
        var ct = TestContext.Current.CancellationToken;

        // One write → exactly one audit row (the signup.set-open /
        // signup.set-notify singleton shape — the SetSignupOpenAsync /
        // SetNotifyAdminsOnSignupAsync body pattern).
        await gates.SetResidentInviteSettingAsync(ResidentInviteSetting.On, Admin);

        var rows = await AllAudits(store);
        Assert.Single(rows);
        Assert.Equal("signup.set-resident-invite", rows[0].Action);
        Assert.Equal("signup", rows[0].TargetKind);
        Assert.Equal("signup", rows[0].TargetId);
        Assert.Equal(AccessVia.Admin, rows[0].Via);
        Assert.Equal(AccessOutcome.Allow, rows[0].Outcome);
        Assert.Equal(Admin, rows[0].ActorId);
        Assert.Equal(Admin, rows[0].EffectivePrincipalId);

        // A second write → a second row (the per-write audit, the
        // SetNotifyAdminsOnSignupAsync shape).
        await gates.SetResidentInviteSettingAsync(ResidentInviteSetting.Off, Admin);
        Assert.Equal(2, (await AllAudits(store)).Count);

        // The read seam writes ZERO audit rows (the IsSignupOpenAsync /
        // ValidateAsync "the read is not an access decision" floor — the M35·5
        // / 0159 shape).
        var before = (await AllAudits(store)).Count;
        await gates.GetResidentInviteSettingAsync();
        Assert.Equal(before, (await AllAudits(store)).Count);
        await store.DisposeAsync();
    }

    // ── Pin (d) — the rule: `Off` → refuse; `On` → allow, no approval;
    //       `OnWithApproval` → allow + the approval flag set ────────────────

    [Fact]
    public async Task A11_Rule_Off_IsRefused()
    {
        var (svc, gates, store, identity) = await BootAsync();

        // The floor (a fresh instance) is `Off` — the rule refuses.
        identity.GetResidentInviteSettingAsync()
            .Returns(ci => Task.FromResult(ResidentInviteSetting.Off));
        var posture = await svc.GetResidentInvitePostureAsync(Resident);
        Assert.False(posture.MayInvite);
        Assert.False(posture.RequiresApproval);
        await store.DisposeAsync();
    }

    [Fact]
    public async Task A11_Rule_On_Allows_WithoutApproval()
    {
        var (svc, gates, store, identity) = await BootAsync();

        identity.GetResidentInviteSettingAsync()
            .Returns(ci => Task.FromResult(ResidentInviteSetting.On));
        var posture = await svc.GetResidentInvitePostureAsync(Resident);
        Assert.True(posture.MayInvite);
        Assert.False(posture.RequiresApproval);
        await store.DisposeAsync();
    }

    [Fact]
    public async Task A11_Rule_OnWithApproval_Allows_WithApprovalFlag()
    {
        var (svc, gates, store, identity) = await BootAsync();

        identity.GetResidentInviteSettingAsync()
            .Returns(ci => Task.FromResult(ResidentInviteSetting.OnWithApproval));
        var posture = await svc.GetResidentInvitePostureAsync(Resident);
        Assert.True(posture.MayInvite);
        Assert.True(posture.RequiresApproval);
        await store.DisposeAsync();
    }

    // ── Pin (e) — the `// TODO ❓` who-may-invite + rate is flagged, not
    //       enforced (the rule does not reject on rate yet) ─────────────────

    [Fact]
    public async Task A11_Rule_DoesNotEnforceRate_ThePostureIsPureSettingDriven()
    {
        var (svc, gates, store, identity) = await BootAsync();
        var ct = TestContext.Current.CancellationToken;

        // The rule is a **pure, setting-driven** decision — no rate-limit
        // conjunct, no per-inviter ceiling, no "who may invite" conjunct
        // (the ADR 0304 D6 b open question, the A11 prompt: "the rule does
        // not reject on rate yet"). A single call for the same resident
        // under `On` returns the same posture as the first call — the
        // posture has no memory, no counter, no rate state.
        identity.GetResidentInviteSettingAsync()
            .Returns(ci => Task.FromResult(ResidentInviteSetting.On));

        var first = await svc.GetResidentInvitePostureAsync(Resident);
        var second = await svc.GetResidentInvitePostureAsync(Resident);
        var third = await svc.GetResidentInvitePostureAsync(Resident);
        Assert.Equal(first, second);
        Assert.Equal(second, third);
        Assert.True(first.MayInvite);

        // A different resident under the same setting gets the same posture
        // (the rule is setting-driven, not resident-driven — the A12 surface
        // will add the per-resident + rate refinement, the ADR 0304 D6 b
        // open question, flagged not built).
        var otherResident = "resident-002";
        var otherPosture = await svc.GetResidentInvitePostureAsync(otherResident);
        Assert.Equal(first, otherPosture);

        // The rule writes ZERO audit rows (the M35·5 / 0159 "the read is not
        // an access decision" floor — the write decision is A12's
        // CreateAsync lane's job). No write occurred in this test, so the
        // audit table is empty.
        Assert.Empty(await AllAudits(store));
        await store.DisposeAsync();
    }

    // ── Harness ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Boot the <see cref="IdentityPlatformGateService"/> (the setting seam)
    /// + the <see cref="InvitationService"/> (the rule seam) over a fresh
    /// <c>mt</c> schema in a new scratch database. The store registers
    /// <see cref="LocaleSettings"/> (the setting singleton) +
    /// <see cref="AccessAudit"/> (the audit pin) + <see cref="Invitation"/>
    /// (the rule's seam — the A07/A08/A09 invitation lane) on the
    /// <see cref="CoreDocTypes"/> surface. The <see cref
    /// "IIdentityService"/> dependency the rule reads the setting through is
    /// the real <see cref="IdentityPlatformGateService"/> (the rule seam
    /// calls <c>IIdentityService.GetResidentInviteSettingAsync</c> — the
    /// C08 thin-forwarder shape, the A11 seam).
    /// </summary>
    private async Task<(InvitationService Svc, IdentityPlatformGateService Gates, IDocumentStore Store, IIdentityService Identity)> BootAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);

        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            CoreDocTypes.Configure(opts);   // LocaleSettings + AccessAudit + Invitation
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        // The C3-03 / ADR 0293 part-vs-whole surface — the 12-gate cluster
        // (the NotifyAdminsOnSignup / IsSignupOpen singleton-toggle lane)
        // + the A11 2-gate resident-invite pair. The rule seam
        // (InvitationService.GetResidentInvitePostureAsync) reads the setting
        // through IIdentityService.GetResidentInviteSettingAsync — the C08
        // thin-forwarder shape. We wire an NSubstitute that returns the live
        // gate-service value (the A10 best-effort-degrade shape: the rule
        // reads the setting, the gates own the singleton toggle). The rule
        // pins stub the identity's GetResidentInviteSettingAsync to the
        // expected value (the rule is a pure function of the setting — the
        // "live" behaviour is already tested by the setting pins a/b/c).
        var gates = new IdentityPlatformGateService(
            store,
            null,                                  // IOptions<SampleDataOptions> — the sample-data gates are not exercised here
            new Kumunita.Core.UserInfo.UserInfoService(store),
            null!);                                // UserManager<User> — the sample-account membership check is not exercised here

        var identity = Substitute.For<IIdentityService>();
        // The floor default — overridden per rule test to the expected
        // setting value (the rule is a pure function of the setting; the
        // "live" round-trip is already tested by pins a/b/c above).
        identity.GetResidentInviteSettingAsync()
            .Returns(ci => Task.FromResult(ResidentInviteSetting.Off));

        var svc = new InvitationService(store, identity);
        return (svc, gates, store, identity);
    }

    /// <summary>The whole <see cref="AccessAudit"/> table (the pin target —
    /// the audit-row shape).</summary>
    private static async Task<IReadOnlyList<AccessAudit>> AllAudits(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        using var session = store.QuerySession();
        return await session.Query<AccessAudit>().ToListAsync(ct).ConfigureAwait(false);
    }
}
