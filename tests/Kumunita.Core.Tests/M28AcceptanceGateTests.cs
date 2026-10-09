using System.Reflection;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M28 (ADR 0151) — the **three** acceptance-gate tests (design doc §2.8),
/// authored by U07 and **recorded** by U08. Each is a pinned test over the
/// 20-test seam list (§2.6):
///
/// <list type="bullet">
/// <item><c>Gate1</c> — closed-loop: a guardian sets a <c>Blocked</c>-mode
/// window containing "now" for their child → <c>IsAllowedNow</c> returns
/// <c>false</c> (restricted) → <c>GetActiveTimeLimitAsync</c> sees the
/// schedule (the middleware would sign the child out) — F1 + C-M28·4.</item>
/// <item><c>Gate2</c> — handoff: a guardian sets an <c>Allowed</c>-mode
/// window containing "now" → allowed; then clears (<c>null</c>) → always
/// allowed (the floor) — F2-in-window + F3 + C-M28·4 strong consistency.</item>
/// <item><c>Gate3</c> — part-vs-whole: the full 20-test seam list passes
/// together — C-M28·5 (zero new authorization surface).</item>
/// </list>
///
/// <para>
/// **Gate1 / Gate2** exercise the U03 seams on the GU surface (an active
/// <see cref="GuardianLink"/> is created first, exactly as the U03 standing
/// tests do) + the U02 pure <see cref="GuardianTimeLimitEvaluator"/> + the
/// U03 enforcement read <c>GetActiveTimeLimitAsync</c> (strong consistency,
/// C-M28·4). **Gate3** is the in-project structural pin: the
/// <see cref="IAuthorizationService"/> frozen 4-method surface is unchanged —
/// the same assertion the U03 standing test
/// <c>C_M28_5_IAuthorizationService_Surface_Count_Unchanged</c> carries.
/// </para>
///
/// <para>
/// **Note (Gate3 "part-vs-whole" is the suites running green, not this
/// assembly reflecting over the 20 classes):** the 20 seam tests are split
/// across **two** test projects — <c>Core.Tests</c> (U02's 7 pure + U03's 7
/// standing/audit) and <c>Web.Tests</c> (U04's 3 middleware + U06's 3
/// surface). A <c>Core.Tests</c> assembly cannot reference <c>Web.Tests</c>
/// types, so the **in-project** evidence is the C-M28·5 structural pin (the
/// frozen authorization surface is unchanged); the **whole** evidence is
/// U07 running **both** suites green (recorded in the handoff note for
/// U08's §2.8 gate record). This comment is the drift-guard record that
/// Gate3 is the in-project half of that.
/// </para>
///
/// <para>
/// Authority: design doc §2.6 (the 20 seam names), §2.8 (the three gate
/// names — the names are the contract, a different name is a drift pause),
/// §2.9 (the drift-guard), C-M28·3/4/5, ADR 0151, ADR 0028 §E (the audit
/// verbs), and the U03/U02 harness shape (the U03 standing tests'
/// <c>BootStoreAsync</c> + <c>CreateGuardianLinkAsync</c> lane).
/// </para>
/// </summary>
public class M28AcceptanceGateTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── GATE-1 — Closed-loop: a Blocked window containing "now" restricts ──
    // A guardian with an active link sets a Blocked-mode window whose hour
    // contains the CURRENT instant (in the child's effective zone = UTC, so
    // the wall-clock hour is the UTC hour). IsAllowedNow must return false
    // (restricted) — the middleware would sign out — and the enforcement read
    // GetActiveTimeLimitAsync must see the schedule (C-M28·4 strong
    // consistency). (F1 + C-M28·4, §2.8.)

    [Fact]
    public async Task Gate1_ClosedLoop_BlockedWindow_ContainingNow_RestrictsChild()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);   // services null ⇒ no admin standing

        const string guardian = "u-m28-gate1-guardian";
        const string child = "u-m28-gate1-child";

        await svc.CreateGuardianLinkAsync(child, guardian);

        // "Now" — the CURRENT instant (the gate is about the child being
        // restricted at the moment the guardian sets the window, not a
        // pinned historical moment). UTC = the child's effective zone (the
        // wall-clock hour is the UTC hour).
        var now = DateTimeOffset.UtcNow;
        var zone = TimeZoneInfo.Utc;

        // A Blocked-mode window containing the current hour (empty DaysOfWeek
        // = all days — the hour alone pins the window). The child is
        // restricted DURING the window (F1 — the lean-default polarity).
        await svc.SetChildTimeLimitAsync(guardian, child,
            new GuardianTimeLimitSchedule { Enabled = true, Mode = TimeLimitMode.Blocked, Hours = [now.Hour] });

        // C-M28·4 — the ENFORCEMENT read (no guardian gate) sees the schedule
        // live on the very next call (no projection, no cache).
        var read = await svc.GetActiveTimeLimitAsync(child);
        Assert.NotNull(read);
        Assert.True(read!.Enabled);
        Assert.Equal(TimeLimitMode.Blocked, read.Mode);

        // F1 — Blocked + in-window → RESTRICTED (IsAllowedNow == false). The
        // TimeLimitMiddleware (U04) reads exactly this schedule + verdict and
        // would sign the child out (C-M28·1 — a sign-out, not a 403).
        Assert.False(GuardianTimeLimitEvaluator.IsAllowedNow(read, now, zone));
    }

    // ── GATE-2 — Handoff: an Allowed window permitting "now" → allowed; then ─
    // cleared → always allowed. A guardian sets an Allowed-mode window whose
    // hour contains "now" → IsAllowedNow returns true (allowed); then clears
    // (null) → GetActiveTimeLimitAsync returns null → IsAllowedNow(null, ...)
    // returns true (always allowed, the floor). (F2-in-window + F3 + C-M28·4,
    // §2.8.)

    [Fact]
    public async Task Gate2_Handoff_AllowedWindow_Then_Clear_Is_AlwaysAllowed()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);   // services null ⇒ no admin standing

        const string guardian = "u-m28-gate2-guardian";
        const string child = "u-m28-gate2-child";

        await svc.CreateGuardianLinkAsync(child, guardian);

        var now = DateTimeOffset.UtcNow;
        var zone = TimeZoneInfo.Utc;

        // BEFORE — no schedule (the floor, C-M28·3): always allowed.
        Assert.Null(await svc.GetActiveTimeLimitAsync(child));
        Assert.True(GuardianTimeLimitEvaluator.IsAllowedNow(null, now, zone));

        // An Allowed-mode window containing the current hour (empty DaysOfWeek
        // = all days). The child may use the platform DURING the window (F2 —
        // the allow-list polarity) → "now" (in-window) → allowed.
        await svc.SetChildTimeLimitAsync(guardian, child,
            new GuardianTimeLimitSchedule { Enabled = true, Mode = TimeLimitMode.Allowed, Hours = [now.Hour] });

        // C-M28·4 — the read sees the schedule live on the next call.
        var read = await svc.GetActiveTimeLimitAsync(child);
        Assert.NotNull(read);
        Assert.True(read!.Enabled);
        Assert.Equal(TimeLimitMode.Allowed, read.Mode);

        // F2 — Allowed + in-window → ALLOWED (IsAllowedNow == true).
        Assert.True(GuardianTimeLimitEvaluator.IsAllowedNow(read, now, zone));

        // CLEAR (null) — the row is deleted (the M20 "clear" idiom).
        await svc.SetChildTimeLimitAsync(guardian, child, null);

        // F3 — the floor: the enforcement read returns null = "never
        // restricted", and IsAllowedNow(null, ...) is always true (C-M28·3).
        // Strong consistency (C-M28·4): the clear is live on the very next
        // read.
        var cleared = await svc.GetActiveTimeLimitAsync(child);
        Assert.Null(cleared);
        Assert.True(GuardianTimeLimitEvaluator.IsAllowedNow(cleared, now, zone));
    }

    // ── GATE-3 — Part-vs-whole: the 20 seam tests pass together ─────────────
    // In-project structural pin (C-M28·5 — zero new authorization surface):
    // reflect over IAuthorizationService and assert the frozen 4-method
    // surface is unchanged (the U03 standing test
    // C_M28_5_IAuthorizationService_Surface_Count_Unchanged is the same
    // assertion). The 20 seam tests (U02 7 pure + U03 7 standing/audit +
    // U04 3 middleware + U06 3 surface) are the WHOLE evidence — they are
    // split across Core.Tests + Web.Tests (a Core.Tests assembly cannot
    // reference Web.Tests types), so the part-vs-whole is satisfied by BOTH
    // suites running green (U07 runs them + records the pass/red status in
    // the handoff note for U08's §2.8 gate record), NOT by one test here
    // reflecting over all 20 classes. See the class doc-comment.

    [Fact]
    public void Gate3_PartVsWhole_AllTwentySeamTestsPassTogether()
    {
        var names = typeof(IAuthorizationService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .Distinct()
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        // The frozen 4-method surface (the ADR 0006-D four signatures): no
        // M28 method may have been added (C-M28·5, the drift-guard).
        Assert.Equal(
            new[] { "CanAsync", "CanSeeAsync", "CanSeeGroupAsync", "CanSeeGroupFeedAsync" },
            names);
    }

    // ── Shared helpers (the U03 standing tests' BootStoreAsync, verbatim) ───

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
