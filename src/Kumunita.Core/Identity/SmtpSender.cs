using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kumunita.Core.Identity;

/// <summary>
/// Per-attempt SMTP seam (ARCHITECTURE.md §5/§6.2). The interface is the only
/// world-facing surface the durable <c>OutboxEmail</c> handler talks to — the
/// handler's *policy* (durability, retry schedule, dead-letter) is configured
/// once at host startup and not per-call here, matching the reference
/// <c>OutboxEmailHandler</c> template for §6.2 (retry count is defined by the
/// number of cooldowns, not by a retry counter).
/// <para>
/// The <see cref="SendAsync"/> contract is a single attempt: it <em>throws</em>
/// on any SMTP failure. The host's durable handler (Wolverine) decides "retry
/// with cooldown" based on that throw; when the retry schedule is exhausted the
/// <c>Fault&lt;OutboxEmail&gt;</c> handler appends the <see cref="EmailDeadLetter"/>
/// domain document. <c>EmailDeadLetter</c> is the operator's re-queue signal
/// (OPS §7) — NOT Wolverine's own low-level dead-letter-queue envelopes, which
/// are for inspection only.
/// </para>
/// </summary>
public interface ISmtpSender
{
    /// <summary>
    /// Send one <see cref="OutboxEmail"/> over SMTP. Implementation detail:
    /// the <paramref name="email"/>'s body is pre-rendered Markdown (§6.2:
    /// "the durable state the handler needs when SMTP is down"), so this
    /// method does no localization or template work — it only transmits.
    /// Throws <see cref="SmtpException"/> / <see cref="MailAddressException"/> /
    /// <see cref="ArgumentNullException"/> as appropriate; the host's retry
    /// policy inspects the exception type, not any envelope.
    /// </summary>
    Task SendAsync(OutboxEmail email, CancellationToken ct = default);
}

/// <summary>
/// SmtpClient configuration bound per-instance from <c>SMTP__*</c> options
/// (see <see cref="SectionName"/>; the host binds the section, following the
/// <see cref="VerificationOptions"/> / <see cref="SeedAdminOptions"/> pattern —
/// Core owns the class, the Web host binds the values).
/// <para>
/// Defaults are "no host, no port" — an unconfigured <see cref="SmtpSender"/>
/// throws <see cref="InvalidOperationException"/> on the first <see cref="SendAsync"/>
/// call with an actionable message. This is deliberate: a silent "send to
/// nowhere" would hide the real signal the <c>EmailDeadLetter</c> row is
/// designed to surface (the §8 degraded /health gate depends on the operator
/// seeing *some* failure, not zero).
/// </para>
/// </summary>
public sealed class SmtpOptions
{
    /// <summary>Configuration section name (bound from the host's <c>appsettings</c> in the Web project).</summary>
    public const string SectionName = "SMTP";

    /// <summary>The SMTP relay host (e.g. <c>mail.kumunita.example</c>). Empty = unconfigured.</summary>
    public string? Host { get; set; }

    /// <summary>The SMTP relay port (default 587 per <c>SmtpClient</c> when null/0; explicit here to be reviewed).</summary>
    public int Port { get; set; } = 587;

    /// <summary>
    /// If true, the sender authenticates with the process's Windows/OS credentials.
    /// If false (default) no credentials are sent — a relay that requires
    /// authentication will fail the first send (surfaced as an
    /// <c>EmailDeadLetter</c> per §6).
    /// </summary>
    public bool UseDefaultCredentials { get; set; } = false;

    /// <summary>
    /// The <c>From</c> header. Empty = the relay's default (which may be rejected
    /// by stricter relays; in production instances, set this to the resident-facing
    /// support mailbox per the seeder's <c>CommunityOptions.SupportEmail</c> —
    /// the two are distinct values even if they often match).
    /// </summary>
    public string? From { get; set; }
}

/// <summary>
/// BCL <c>System.Net.Mail</c> implementation of <see cref="ISmtpSender"/>. One
/// <c>SmtpClient</c> instance per call (cheap to construct; avoids holding a
/// network socket across many email dispatches and keeps each attempt
/// independent in the presence of relay connection resets).
/// <para>
/// The <c>OutboxEmail</c>'s <see cref="OutboxEmail.IdempotencyKey"/> is set as
/// the X-Message-Id so the relay can deduplicate if (and only if) it honors
/// SMTP's message-id semantics — this is *not* the idempotency guarantee for
/// the domain path (that's the <c>OutboxEmail</c> row's own key, the §6.2
/// <c>verify:{userId}:{attempt}</c> shape), just a belt for relays that log
/// by message-id.
/// </para>
/// </summary>
public sealed class SmtpSender(
    Microsoft.Extensions.Options.IOptions<SmtpOptions> options,
    Microsoft.Extensions.Logging.ILogger<SmtpSender> logger) : ISmtpSender
{
    private readonly SmtpOptions _cfg = options.Value;

    /// <inheritdoc />
    public async Task SendAsync(OutboxEmail email, CancellationToken ct = default)
    {
        if (email is null)
            throw new ArgumentNullException(nameof(email));
        if (string.IsNullOrWhiteSpace(email.Recipient))
            throw new ArgumentException("Recipient must be set on OutboxEmail (the §6.2 per-email row is the contract).", nameof(email));
        if (string.IsNullOrEmpty(_cfg.Host))
            throw new InvalidOperationException(
                "SMTP is not configured (SMTP__Host is empty). Set it in the host's configuration " +
                "(appsettings.Development.json for dev, SMTP__Host env var in production per OPS.md §2). " +
                "No email was sent; the durable handler will retry / dead-letter per the configured policy.");

        using var client = new SmtpClient
        {
            Host = _cfg.Host,
            Port = _cfg.Port,
            UseDefaultCredentials = _cfg.UseDefaultCredentials
        };

        var msg = new MailMessage
        {
            To = { new MailAddress(email.Recipient) },
            Subject = email.Subject,
            Body = email.Body,
            IsBodyHtml = false               // the body is Markdown (§6.2: "rendered Markdown"); relays don't re-render
        };

        if (!string.IsNullOrWhiteSpace(_cfg.From))
            msg.From = new MailAddress(_cfg.From);

        // X-Message-Id carries the per-email idempotency key (§6.2) — a relay-side
        // duplicate signal, not a delivery guarantee (that belongs to the caller's
        // committed OutboxEmail row, not to this transmission).
        msg.Headers.Add("X-Message-Id", email.IdempotencyKey);
        msg.Headers.Add("X-Kumunita-Recipient", email.Recipient);

        try
        {
            await client.SendMailAsync(msg, ct);
            logger.LogInformation("Delivered email {Idp} to {Recipient} (attempt sent).", email.IdempotencyKey, email.Recipient);
        }
        catch (TimeoutException)
        {
            // Re-throw as-is; the host's retry policy (RetryWithCooldown) inspects
            // the concrete type (SmtpException/Timeout/MailAddressException) and
            // the cooldown list drives the 6-attempt / ~24h window (§6.2).
            throw;
        }
    }
}
