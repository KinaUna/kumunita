using Kumunita.Core;
using Kumunita.Core.Identity;
using Kumunita.Core.Notifications;
using Kumunita.Web.SideEffects;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// IMPROVE-UX **O04** (D8) — the T4 mail-alarm breach also reaches the
/// GlobalAdmins' inbox (the second delivery channel over the existing
/// e-mail channel).
/// <para>
/// O04 is thin: the existing T4 gate
/// (<see cref="MailAlarmsService.EvaluateAsync"/>) now returns the
/// composed <see cref="AlertEvent"/> (or <c>null</c> under threshold)
/// instead of a <c>bool</c>, so the handler can read
/// <see cref="AlertEvent.Subject"/> for its inbox body. The handler
/// (<see cref="MailAlarmsHandler.Handle"/>) gains two DI parameters
/// (<see cref="INotificationEmitter"/>, <see cref="IIdentityService"/>)
/// and, when the alarm fired, emits one
/// <see cref="NotificationKinds.OpsAlarm"/> row per GlobalAdmin
/// (the A14 fan-out shape, <c>LinkPath = /admin/status</c>).
/// </para>
/// <para>
/// <b>Two invariants are load-bearing (the O04 prompt card):</b>
/// (1) the T4 gate is unchanged — only the *delivery* gains a second
/// channel; (2) the inbox emission is best-effort (the A14 / ADR 0077 D5
/// posture) — a failure is swallowed, never rethrown, and the tick still
/// self-schedules.
/// </para>
/// <para>
/// <b>Test model:</b> <see cref="MailAlarmsService.EvaluateAsync"/> is a
/// <b>static</b> read that executes real Marten SQL (Marten 9's
/// <c>CountAsync</c> runs a live query — it cannot be NSubstituted, the
/// <c>M15AcceptanceTests</c> precedent), so each behavioural test boots a
/// scratch <see cref="PostgresFixture"/> store with breach rows planted,
/// and the handler's emission seams (<see cref="INotificationEmitter"/>,
/// <see cref="IIdentityService"/>) are pure NSubstitutes — the emission is
/// fully captured, no Notification row is written.
/// </para>
/// </summary>
public sealed class O04MailAlarmInboxTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Admin1 = "admin-001";
    private const string Admin2 = "admin-002";

    // ── Structural pins: the kind is in the closed set, not in OptInKinds ──

    [Fact(DisplayName = "OpsAlarm kind value is ops.alarm")]
    public void Kind_OpsAlarm_Value()
        => Assert.Equal("ops.alarm", NotificationKinds.OpsAlarm);

    [Fact(DisplayName = "OpsAlarm is in the Known closed set")]
    public void Kind_OpsAlarm_InKnown()
        => Assert.Contains(NotificationKinds.OpsAlarm, NotificationKinds.Known);

    [Fact(DisplayName = "OpsAlarm is NOT in OptInKinds (admin-lane, always-on)")]
    public void Kind_OpsAlarm_NotInOptInKinds()
        => Assert.DoesNotContain(NotificationKinds.OpsAlarm, NotificationKinds.OptInKinds);

    // ── Behavioural: the handler emits one row per GlobalAdmin on breach ────

    [Fact(DisplayName = "O04 handler — alarm fires → one inbox row per GlobalAdmin, linked to /admin/status")]
    public async Task AlarmFired_EmitsOneRowPerGlobalAdmin_LinkedToAdminStatus()
    {
        var store = await BootStoreAsync(breached: true);
        try
        {
            var emitter  = Substitute.For<INotificationEmitter>();
            var identity = Substitute.For<IIdentityService>();
            identity.ListGlobalAdminsAsync().Returns(new[] { Admin1, Admin2 });
            emitter.EmitAsync(Arg.Any<IDocumentSession>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<Notification?>(null));

            var result = await RunHandler(store, emitter, identity);

            // The tick self-schedules (one MailAlarmsTick in the cascade).
            Assert.Single(result);
            Assert.IsType<MailAlarmsTick>(result.First());

            // Two emission calls (one per admin), each linked to /admin/status.
            await emitter.Received(2).EmitAsync(
                Arg.Any<IDocumentSession>(),
                Arg.Any<string>(),
                NotificationKinds.OpsAlarm,
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),      // targetId (null)
                "/admin/status",         // linkPath
                Arg.Any<string?>(),      // acceptPath (null)
                Arg.Any<string?>(),      // declinePath (null)
                Arg.Any<CancellationToken>());
        }
        finally { await store.DisposeAsync(); }
    }

    [Fact(DisplayName = "O04 handler — no GlobalAdmin (fresh instance) → emitter never called")]
    public async Task NoGlobalAdmins_EmitterNeverCalled()
    {
        var store = await BootStoreAsync(breached: true);
        try
        {
            var emitter  = Substitute.For<INotificationEmitter>();
            var identity = Substitute.For<IIdentityService>();
            identity.ListGlobalAdminsAsync().Returns(Array.Empty<string>());

            // The best-effort no-op (the A14 precedent): a fresh instance
            // (no GlobalAdmin) is nothing-to-notify, not a noise path.
            var result = await RunHandler(store, emitter, identity);
            Assert.Single(result);
            await emitter.DidNotReceiveWithAnyArgs().EmitAsync(
                default!, default!, default!, default!,
                default!, default!, default!, default!, default!,
                default);
        }
        finally { await store.DisposeAsync(); }
    }

    [Fact(DisplayName = "O04 handler — emit throws → swallowed, tick still self-schedules")]
    public async Task EmitThrows_Swallows_TickStillSchedules()
    {
        var store = await BootStoreAsync(breached: true);
        try
        {
            var emitter  = Substitute.For<INotificationEmitter>();
            var identity = Substitute.For<IIdentityService>();
            identity.ListGlobalAdminsAsync().Returns(new[] { Admin1 });
            emitter.When(x => x.EmitAsync(
                Arg.Any<IDocumentSession>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>()))
                .Do(_ => throw new InvalidOperationException("boom"));

            // Must NOT throw — the best-effort contract (the A14 / ADR 0077 D5
            // posture). The tick still self-schedules.
            var result = await RunHandler(store, emitter, identity);
            Assert.Single(result);
        }
        finally { await store.DisposeAsync(); }
    }

    [Fact(DisplayName = "O04 handler — under threshold → emitter never called (gate unchanged)")]
    public async Task UnderThreshold_NoEmission()
    {
        var store = await BootStoreAsync(breached: false);   // 0 breach rows → gate returns null
        try
        {
            var emitter  = Substitute.For<INotificationEmitter>();
            var identity = Substitute.For<IIdentityService>();
            identity.ListGlobalAdminsAsync().Returns(new[] { Admin1, Admin2 });

            var result = await RunHandler(store, emitter, identity);
            Assert.Single(result);

            // The gate did not breach → no inbox leg (the e-mail leg likewise
            // fired no alert; the delivery gains a channel only on breach).
            await emitter.DidNotReceiveWithAnyArgs().EmitAsync(
                default!, default!, default!, default!,
                default!, default!, default!, default!, default!,
                default);
        }
        finally { await store.DisposeAsync(); }
    }

    [Fact(DisplayName = "O04 handler — the body carries the AlertEvent.Subject (counts + threshold, no PII)")]
    public async Task Body_CarriesSubject_NoPii()
    {
        var store = await BootStoreAsync(breached: true);
        try
        {
            var emitter  = Substitute.For<INotificationEmitter>();
            var identity = Substitute.For<IIdentityService>();
            identity.ListGlobalAdminsAsync().Returns(new[] { Admin1 });

            string? capturedBody = null;
            emitter.EmitAsync(Arg.Any<IDocumentSession>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    capturedBody = callInfo.ArgAt<string?>(4);   // index 4 = body
                    return Task.FromResult<Notification?>(null);
                });

            await RunHandler(store, emitter, identity);

            // The body is the AlertEvent.Subject: "SuppressedAddress ingest: 5
            // new bounce/complaint rows in 24h (threshold 5)" — counts +
            // threshold, no e-mail addresses, no resident names.
            Assert.NotNull(capturedBody);
            Assert.Contains("SuppressedAddress ingest", capturedBody!);
            Assert.Contains("new bounce/complaint rows", capturedBody!);
            Assert.Contains("threshold", capturedBody!);
            // No PII (no "@" = no e-mail address).
            Assert.DoesNotContain("@", capturedBody!);
        }
        finally { await store.DisposeAsync(); }
    }

    // ── Harness ──────────────────────────────────────────────────────────────

    private async Task<IDocumentStore> BootStoreAsync(bool breached)
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            CoreDocTypes.Configure(opts);
            NotificationDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        if (breached)
        {
            // Plant 5 bounce rows — exactly the T4 threshold (AlertThreshold =
            // 5) — so the static EvaluateAsync returns a non-null AlertEvent.
            var now = DateTimeOffset.UtcNow;
            await using var s = store.OpenSession(new Marten.Services.SessionOptions());
            for (int i = 0; i < 5; i++)
            {
                s.Store(new SuppressedAddress
                {
                    Id = $"breach{i}@test.example",
                    Reason = "bounce",
                    Created = now.AddHours(-1),
                    Source = "ses",
                });
            }
            await s.SaveChangesAsync(ct);
        }
        return store;
    }

    private static Task<IEnumerable<object>> RunHandler(
        IDocumentStore store,
        INotificationEmitter emitter,
        IIdentityService identity)
        => MailAlarmsHandler.Handle(
            new MailAlarmsTick(), store,
            new NoopAlertSink(),   // the e-mail leg (a no-op — we test the inbox leg)
            emitter, identity,
            NullLogger.Instance);   // non-generic (the static handler can't be a generic type arg, CS0718)
}
