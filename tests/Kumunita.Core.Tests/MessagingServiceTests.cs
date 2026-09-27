using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Kumunita.Core.Messaging;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The <see cref="MessagingService"/> toggle-seam pins (M9, ADR 0105 — U02's
/// 2 of the lane's 14). The shape follows <see cref="AnnouncementServiceTests"/>
/// — same <see cref="PostgresFixture"/>, same <c>BootStoreAsync</c>, same
/// <c>AuditRows</c> helper, fresh scratch Postgres per test method.
/// <para>
/// The toggle is the ADR 0101 admin-toggle shape with the floor <b>inverted</b>
/// (design doc §D2): a missing <see cref="LocaleSettings"/> row reads as
/// **off** (the <c>false</c> floor — messaging is a privacy-sensitive
/// opt-in, not a public-surface default), and the admin write commits exactly
/// one <c>messaging.toggle</c> <see cref="AccessAudit"/> row with
/// <c>Via = Admin</c> / <c>Outcome = Allow</c> (C-M9·3).
/// </para>
/// <para>
/// U03 extends this class with the 12 conversation / message behavior pins —
/// these two toggle pins are U02's and are not rescopeable (the design doc's
/// pinned-test list).
/// </para>
/// </summary>
public class MessagingServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── The `false` floor — a fresh instance ships with messaging off ────

    /// <summary>
    /// A missing row is the <c>false</c> floor (the deliberate inverse of the
    /// <c>true</c>-floor convention — the design doc §D2 text), and the probe
    /// is a read: no <see cref="AccessAudit"/> row is committed.
    /// </summary>
    [Fact]
    public async Task IsMessagingEnabled_FreshInstance_FloorsToFalse()
    {
        var store = await BootStoreAsync();
        var svc = new MessagingService(store);

        // A fresh instance has no LocaleSettings row at all (the seeder has
        // not run in this scratch DB); the floor is `false`.
        Assert.False(await svc.IsMessagingEnabledAsync());

        // A read is a read — no audit row is committed for the floor probe
        // (the IsSignupOpen_FreshInstance_FloorsToTrue_NoAuditRow shape, the
        // false floor).
        var audits = await AuditRows(store);
        Assert.Empty(audits);
    }

    // ── The admin flips the gate on ──────────────────────────────────────

    /// <summary>
    /// Enabling the gate persists the flag **and** commits a
    /// <c>messaging.toggle</c> / <c>Via = Admin</c> audit row (the ADR 0101
    /// <c>announcementcomments.set-enabled</c> singleton-toggle shape, flat
    /// sentinel <c>TargetId</c>). A subsequent read sees the flag as **on**.
    /// </summary>
    [Fact]
    public async Task SetMessagingEnabled_Toggle_StoresFlagAndAuditRow()
    {
        var store = await BootStoreAsync();
        var svc = new MessagingService(store);

        await svc.SetMessagingEnabledAsync(true, "u-admin");

        Assert.True(await svc.IsMessagingEnabledAsync());

        var audits = await AuditRows(store);
        var row = Assert.Single(audits, a => a.Action == "messaging.toggle");
        Assert.Equal(Authorization.AccessVia.Admin, row.Via);
        Assert.Equal("messaging.toggle", row.TargetKind);
        Assert.Equal("messaging.toggle", row.TargetId);
        Assert.Equal(Authorization.AccessOutcome.Allow, row.Outcome);
        Assert.Equal("u-admin", row.ActorId);
        // A singleton toggle is not an access change — no VisibleCount /
        // HiddenCount (the single-target shape, the ADR 0019/0020 pin).
        Assert.Null(row.VisibleCount);
        Assert.Null(row.HiddenCount);
    }

    // ── Harness ──────────────────────────────────────────────────────────

    private async Task<IDocumentStore> BootStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            // M1DocTypes registers LocaleSettings (the toggle's home) and
            // AccessAudit (the audit-row pin needs it in the schema).
            M1DocTypes.Configure(opts);
            // M9DocTypes registers Conversation / Message (the M9 surface
            // the service writes to; the U02 toggle pins only touch
            // LocaleSettings, but the schema is the full lane's).
            M9DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    /// <summary>Query the AccessAudit lane for the row-shape pins.</summary>
    private static async Task<IReadOnlyList<AccessAudit>> AuditRows(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<AccessAudit>().ToListAsync(ct);
    }
}
