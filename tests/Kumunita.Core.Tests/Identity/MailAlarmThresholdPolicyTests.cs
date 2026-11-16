using Kumunita.Core.Identity;
using Kumunita.Core.Notifications;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// C2-06 unit test for the <see cref="MailAlarmThresholdPolicy"/> decision
/// surface (F18 T4 in-repo alarm, the DoD #7 "the two untested services
/// covered" line — the pure decision extracted out of
/// <see cref="MailAlarmsService.EvaluateAsync"/>'s I/O). A **pure** unit — no
/// <c>PostgresFixture</c>, no <see cref="Marten.IDocumentStore"/>: just the
/// three window counts + the platform constants
/// (<see cref="MailAlarmsService.AlertThreshold"/>,
/// <see cref="MailAlarmsService.WindowHours"/>,
/// <see cref="MailAlarmsService.AlertKind"/>).
/// Pins the boundary (one under / at / over the threshold), the pin §3
/// payload mapping (the exact <c>Kind</c> / <c>Subject</c> / <c>Detail</c>
/// text, <c>Attempts</c> null), and the no-PII shape (pin §5.8 — only the
/// counts + the <c>Reason</c> breakdown, never an e-mail address). The I/O
/// legs (the <c>QuerySession</c> reads + the best-effort
/// <see cref="IAlertSink.ReportAsync"/> fire) stay covered by
/// <see cref="MailAlarmsServiceTests"/> (Testcontainers).
/// </summary>
public class MailAlarmThresholdPolicyTests
{
    [Fact]
    public void Build_OneUnderThreshold_ReturnsNull_NoAlert()
    {
        // 4 total (3 bounce + 1 complaint) — one under the threshold of 5.
        Assert.Null(MailAlarmThresholdPolicy.Build(3, 1, 0));
    }

    [Fact]
    public void Build_AtThreshold_ReturnsAlert()
    {
        // Exactly the threshold (5 total) — the "≥ 5" gate trips.
        Assert.NotNull(MailAlarmThresholdPolicy.Build(3, 2, 0));
    }

    [Fact]
    public void Build_OverThreshold_ReturnsAlert()
    {
        Assert.NotNull(MailAlarmThresholdPolicy.Build(6, 0, 4));
    }

    [Fact]
    public void Build_ZeroBounceZeroComplaint_ReturnsNull_EvenWithDeadLetters()
    {
        // The dead-letter count is a pin §1 second signal carried in Detail —
        // it does not trip the gate itself (the gate is the bounce/complaint
        // ingest, pin §2).
        Assert.Null(MailAlarmThresholdPolicy.Build(0, 0, 50));
    }

    [Fact]
    public void Build_AtThreshold_PayloadIsPinSection3_Mapping_NoPii()
    {
        var alert = MailAlarmThresholdPolicy.Build(3, 2, 7)!;

        // Pin §3 mapping — the F11 pre-reserved kind (a string, not an enum).
        Assert.Equal(MailAlarmsService.AlertKind, alert.Kind);
        Assert.Equal("mail.bounce-alarm", alert.Kind);

        // The exact subject / detail text (pin §3, U2) — counts + the Reason
        // breakdown + the dead-letter count. No PII anywhere (pin §5.8).
        Assert.Equal(
            $"SuppressedAddress ingest: 5 new bounce/complaint rows in {MailAlarmsService.WindowHours}h (threshold {MailAlarmsService.AlertThreshold})",
            alert.Subject);
        Assert.Equal("bounce: 3, complaint: 2; EmailDeadLetter count: 7", alert.Detail);

        // A non-retry alert carries no attempt count (the F11 pin).
        Assert.Null(alert.Attempts);
    }

    [Fact]
    public void Build_PayloadText_IsCountsOnly_NeverAnEmailAddress()
    {
        var alert = MailAlarmThresholdPolicy.Build(4, 1, 3)!;
        Assert.DoesNotContain("@", alert.Subject);
        Assert.DoesNotContain("@", alert.Detail);
    }
}
