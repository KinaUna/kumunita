using Kumunita.Core;
using Kumunita.Core.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kumunita.Web.SideEffects;

/// <summary>
/// The F12 human alert channel (OBS-06, pin §2a): the <see cref="IAlertSink"/>
/// binding that reaches a human over the community's own SMTP relay. It composes
/// one <see cref="OutboxEmail"/> from the <see cref="AlertEvent"/> (pin §2b) and
/// sends it through the existing Core <see cref="ISmtpSender"/> — the F10
/// "Core owns the seam, Web owns the channel" split, NOT a new seam (ADR 0006).
/// <para>
/// <b>Best-effort at the boundary</b> (the <see cref="IAlertSink"/> contract,
/// <c>AlertSink.cs</c> doc): the <see cref="ISmtpSender.SendAsync"/> call is
/// wrapped in <c>try/catch</c>; a send failure is <em>swallowed</em>, never
/// rethrown (a rethrow would turn an alert-failure into the caller's write-path
/// failure — the <c>OutboxEmailHandler.HandleFault</c> <c>try/catch</c> swallow
/// precedent). The <c>/health</c> degraded gate (OPS §8) is the load-bearing
/// operator signal; this alert is the additive push.
/// </para>
/// <para>
/// <b>Kind-agnostic</b> (pin §3): it composes the <see cref="OutboxEmail"/> from
/// the <see cref="AlertEvent"/> fields and does NOT switch on
/// <see cref="AlertEvent.Kind"/> — F11's <c>"email.dead-letter"</c> rides it
/// today; F18's future <c>"mail.bounce-alarm"</c> rides the same binding.
/// </para>
/// </summary>
public sealed class SupportEmailAlertSink : IAlertSink
{
    private readonly ISmtpSender _sender;
    private readonly IOptions<CommunityOptions> _community;
    private readonly ILogger<SupportEmailAlertSink> _logger;

    /// <param name="sender">The Core SMTP seam (MailKit-backed) — the transport split (ADR 0006).</param>
    /// <param name="community">The per-instance <see cref="CommunityOptions"/>; <see cref="CommunityOptions.SupportEmail"/> is the human destination.</param>
    /// <param name="logger">A warning sink for swallowed send failures (pin §2a).</param>
    public SupportEmailAlertSink(
        ISmtpSender sender,
        IOptions<CommunityOptions> community,
        ILogger<SupportEmailAlertSink> logger)
    {
        _sender = sender;
        _community = community;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ReportAsync(AlertEvent alert, CancellationToken ct = default)
    {
        // The off-by-default floor (pin §2a, belt-and-suspenders over the
        // Program.cs registration gate): no SupportEmail configured → a no-op.
        var recipient = _community.Value.SupportEmail;
        if (string.IsNullOrWhiteSpace(recipient))
            return;

        var email = new OutboxEmail
        {
            Recipient = recipient,
            Subject = BuildSubject(alert),
            Body = BuildBody(alert),
            IdempotencyKey = $"alert:{alert.Kind}",
            Id = Guid.NewGuid().ToString("N"),
            QueuedAt = DateTimeOffset.UtcNow,
        };

        try
        {
            await _sender.SendAsync(email, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Best-effort at the boundary: a send failure is SWALLOWED, never
            // rethrown (pin §2a / §8; the IAlertSink contract). The /health
            // degraded gate is the load-bearing signal; this line is the audit
            // trail for the swallowed push.
            _logger.LogWarning(
                ex,
                "F12 alert channel: failed to deliver AlertEvent (Kind={Kind}, Subject={Subject}); swallowed per the IAlertSink boundary contract.",
                alert.Kind, alert.Subject);
        }
    }

    /// <summary>
    /// The operator-readable one-liner (pin §2b): the <see cref="AlertEvent.Kind"/>
    /// + <see cref="AlertEvent.Subject"/>. <c>"Kumunita alert: email.dead-letter — bob@example.com"</c>.
    /// </summary>
    private static string BuildSubject(AlertEvent alert)
        => $"Kumunita alert: {alert.Kind} — {alert.Subject}";

    /// <summary>
    /// The operator-readable detail (pin §2b): the <see cref="AlertEvent.Detail"/>
    /// + <see cref="AlertEvent.Attempts"/> — the OPS §7 re-queue input.
    /// </summary>
    private static string BuildBody(AlertEvent alert)
    {
        var detail = string.IsNullOrWhiteSpace(alert.Detail) ? "(none)" : alert.Detail;
        var attempts = alert.Attempts is null ? "(n/a)" : alert.Attempts.Value.ToString();
        return
            "Kumunita alert — operator detail (re-queue / triage per OPS §7).\n" +
            $"Kind: {alert.Kind}\n" +
            $"Subject: {alert.Subject}\n" +
            $"Detail: {detail}\n" +
            $"Attempts: {attempts}";
    }
}
