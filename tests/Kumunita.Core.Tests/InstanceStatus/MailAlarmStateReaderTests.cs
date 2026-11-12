using Kumunita.Core.Identity;
using Kumunita.Core.InstanceStatus;
using Kumunita.Core.Notifications;
using Marten;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests.InstanceStatus;

/// <summary>
/// Unit tests for the <see cref="MailAlarmStateReader"/> (IMPROVE-UX O01, the
/// F18 T4 mail-alarm state read seam). The reader reuses the
/// <see cref="IEmailDeadLetterCounter"/> dead-letter seam + the pure
/// <see cref="MailAlarmThresholdPolicy.Build"/> firing decision + the window's
/// <see cref="SuppressedAddress"/> bounce / complaint count (the
/// <see cref="MailAlarmsService"/> T4 query shape, the
/// <see cref="MailAlarmsService.WindowHours"/> rolling window).
/// <para>
/// The "happy" test uses a real Postgres scratch store (the
/// <see cref="Kumunita.Core.Tests.MailAlarmsServiceTests"/> idiom) to verify
/// the window-count query shape against real Marten + real Postgres. The
/// "degraded" test uses a null store (the
/// <see cref="Kumunita.Core.Tests.AdminOnboardingServiceTests"/> "missing
/// store" idiom) — the reader's <c>QuerySession()</c> NREs, and the
/// <see cref="InstanceStatusService"/> composer's try/catch degrades the
/// section to <see cref="MailAlarmStatus.Unavailable"/> (not a throw).
/// </para>
/// </summary>
public sealed class MailAlarmStateReaderTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task ReadAsync_WindowCountsAndFiringDecision_AreCorrect()
    {
        var (store, deadLetters) = await BootAsync();
        var now = DateTimeOffset.UtcNow;

        // Plant 6 bounce rows in the window (≥ the threshold of 5 → firing).
        await using (var session = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            for (int i = 0; i < 6; i++)
            {
                session.Store(new SuppressedAddress
                {
                    Id = $"b{i}@test.example",
                    Reason = "bounce",
                    Created = now.AddHours(-1),
                    Source = "ses"
                });
            }
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var reader = new MailAlarmStateReader(store, deadLetters);
        var result = await reader.ReadAsync(now, TestContext.Current.CancellationToken);

        Assert.Equal(6, result.Bounces);
        Assert.Equal(0, result.Complaints);
        Assert.Equal(3, result.DeadLetters);   // the substituted counter returns 3
        Assert.True(result.Firing);             // 6 >= 5 (the AlertThreshold)
    }

    [Fact]
    public async Task ReadAsync_UnderThreshold_FiringFalse()
    {
        var (store, deadLetters) = await BootAsync();
        var now = DateTimeOffset.UtcNow;

        // Plant 4 bounce rows (one under the threshold of 5).
        await using (var session = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            for (int i = 0; i < 4; i++)
            {
                session.Store(new SuppressedAddress
                {
                    Id = $"b{i}@test.example",
                    Reason = "bounce",
                    Created = now.AddHours(-1),
                    Source = "ses"
                });
            }
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var reader = new MailAlarmStateReader(store, deadLetters);
        var result = await reader.ReadAsync(now, TestContext.Current.CancellationToken);

        Assert.Equal(4, result.Bounces);
        Assert.Equal(0, result.Complaints);
        Assert.Equal(3, result.DeadLetters);
        Assert.False(result.Firing);            // 4 < 5 (the AlertThreshold)
    }

    [Fact]
    public async Task ReadAsync_OutsideWindow_Excluded()
    {
        var (store, deadLetters) = await BootAsync();
        var now = DateTimeOffset.UtcNow;

        // Plant 6 rows — above threshold — but all outside the 24h window.
        await using (var session = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            for (int i = 0; i < 6; i++)
            {
                session.Store(new SuppressedAddress
                {
                    Id = $"old{i}@test.example",
                    Reason = "bounce",
                    Created = now.AddDays(-3),
                    Source = "ses"
                });
            }
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var reader = new MailAlarmStateReader(store, deadLetters);
        var result = await reader.ReadAsync(now, TestContext.Current.CancellationToken);

        Assert.Equal(0, result.Bounces);
        Assert.Equal(0, result.Complaints);
        Assert.Equal(3, result.DeadLetters);
        Assert.False(result.Firing);            // 0 < 5 (outside the window)
    }

    [Fact]
    public async Task ReadAsync_MissingStore_Throws_TheServiceDegrades()
    {
        // A test construction with no usable IDocumentStore (null! — the
        // QuerySession call NREs). The reader throws; the
        // InstanceStatusService composer's try/catch degrades the section to
        // MailAlarmStatus.Unavailable (not a throw). This test just pins that
        // the reader does NOT swallow the exception (the degradation is the
        // composer's job, not the reader's).
        var deadLetters = Substitute.For<IEmailDeadLetterCounter>();
        deadLetters.GetCountAsync(Arg.Any<CancellationToken>()).Returns(3);

        var reader = new MailAlarmStateReader(null!, deadLetters);
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            reader.ReadAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken));
    }

    private async Task<(IDocumentStore store, IEmailDeadLetterCounter deadLetters)> BootAsync()
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

        var deadLetters = Substitute.For<IEmailDeadLetterCounter>();
        deadLetters.GetCountAsync(Arg.Any<CancellationToken>()).Returns(3);
        return (store, deadLetters);
    }
}
