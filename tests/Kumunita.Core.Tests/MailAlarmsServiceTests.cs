using Kumunita.Core.Identity;
using Kumunita.Core.Notifications;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// C12 seam test for <see cref="MailAlarmsService"/> (F18 T4 in-repo alarm).
/// Pure integration over a scratch Postgres (no fake needed — the service is
/// a read-only count over <see cref="SuppressedAddress"/> rows in the window).
/// Two legs: under-threshold → false, no alert; at-threshold → true, exactly
/// one alert with the pin §3 payload mapping (Kind = "mail.bounce-alarm").
/// </summary>
public sealed class MailAlarmsServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Evaluate_UnderThreshold_ReturnsFalse_NoAlert()
    {
        var (store, sink) = await BootAsync();
        var now = DateTimeOffset.UtcNow;

        // Plant 4 bounce rows (one under the threshold of 5).
        await using (var session = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            for (int i = 0; i < 4; i++)
            {
                session.Store(new SuppressedAddress
                {
                    Id = $"user{i}@test.example",
                    Reason = "bounce",
                    Created = now.AddHours(-1),
                    Source = "ses"
                });
            }
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var result = await MailAlarmsService.EvaluateAsync(store, sink, now, TestContext.Current.CancellationToken);
        Assert.Null(result);
        Assert.Equal(0, sink.Count);
    }

    [Fact]
    public async Task Evaluate_AtThreshold_ReturnsTrue_OneAlertWithCorrectKind()
    {
        var (store, sink) = await BootAsync();
        var now = DateTimeOffset.UtcNow;

        // Plant exactly 5 (3 bounce + 2 complaint) — the threshold.
        await using (var session = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            for (int i = 0; i < 3; i++)
            {
                session.Store(new SuppressedAddress
                {
                    Id = $"b{i}@test.example", Reason = "bounce",
                    Created = now.AddHours(-2), Source = "ses"
                });
            }
            for (int i = 0; i < 2; i++)
            {
                session.Store(new SuppressedAddress
                {
                    Id = $"c{i}@test.example", Reason = "complaint",
                    Created = now.AddHours(-3), Source = "postal"
                });
            }
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var result = await MailAlarmsService.EvaluateAsync(store, sink, now, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(1, sink.Count);
        // O04: the returned event IS the one delivered to the sink (the handler
        // reads result.Subject for its inbox body — the same payload the e-mail
        // leg sent).
        Assert.Same(sink.LastAlert, result);
        Assert.Equal(MailAlarmsService.AlertKind, sink.LastAlert!.Kind);
        // No PII in the subject (pin §5.8) — only counts.
        Assert.Contains("5 new bounce/complaint rows", sink.LastAlert.Subject);
        Assert.Contains("bounce: 3, complaint: 2", sink.LastAlert.Detail);
    }

    [Fact]
    public async Task Evaluate_OutsideWindow_ReturnsFalse()
    {
        var (store, sink) = await BootAsync();
        var now = DateTimeOffset.UtcNow;

        // Plant 6 rows — above threshold — but all outside the 24h window.
        await using (var session = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            for (int i = 0; i < 6; i++)
            {
                session.Store(new SuppressedAddress
                {
                    Id = $"old{i}@test.example", Reason = "bounce",
                    Created = now.AddDays(-3), Source = "ses"
                });
            }
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var result = await MailAlarmsService.EvaluateAsync(store, sink, now, TestContext.Current.CancellationToken);
        Assert.Null(result);
        Assert.Equal(0, sink.Count);
    }

    private async Task<(IDocumentStore store, RecordingAlertSink sink)> BootAsync()
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
        return (store, new RecordingAlertSink());
    }

    private sealed class RecordingAlertSink : IAlertSink
    {
        public int Count { get; private set; }
        public AlertEvent? LastAlert { get; private set; }
        public Task ReportAsync(AlertEvent alert, CancellationToken ct = default)
        {
            Count++;
            LastAlert = alert;
            return Task.CompletedTask;
        }
    }
}
