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
    /// If true, the sender authenticates with the process's Windows/OS credentials
    /// (BCL <c>SmtpClient.UseDefaultCredentials</c>). Mutually exclusive with
    /// <see cref="User"/> — if both are set <see cref="User"/> wins (per the BCL
    /// precedence: explicitly-set credentials take effect over the default-credential
    /// flag). Left at the default (false) everywhere except the rare case of a
    /// Windows-service relay with the process already in the right domain.
    /// </summary>
    public bool UseDefaultCredentials { get; set; } = false;

    /// <summary>
    /// The SMTP relay username for authentication. Empty + <see cref="Pass"/> empty
    /// = no AUTH sent at all (Mailpit / local-only relay — the dev compose shape).
    /// Exactly one of <c>User</c> / <c>Pass</c> set is a configuration error —
    /// <see cref="SmtpSender.SendAsync"/> throws a clear <see cref="InvalidOperationException"/>
    /// before it opens a connection, instead of failing opaquely at the <c>AUTH</c>
    /// handshake once on the wire (which would otherwise surface only as a dead-
    /// lettered <c>OutboxEmail</c> row — see <c>OPS.md</c> §6 / §7).
    /// </summary>
    public string? User { get; set; }

    /// <summary>
    /// The SMTP relay password; required iff <see cref="User"/> is set. See
    /// <see cref="User"/> for the "exactly one or zero" invariant.
    /// </summary>
    public string? Pass { get; set; }

    /// <summary>
    /// Encryption policy for the relay handshake. Recognized values (case-insensitive):
    /// <see cref="SecureTls"/> (<c>Tls</c>) → <c>STARTTLS</c> (the conventional 587
    /// shape; the default); <see cref="SecureNone"/> (<c>None</c>) → no encryption,
    /// plain SMTP (Mailpit / a loopback-only relay — **not** a production shape).
    /// <para>
    /// <b>Why only these two:</b> the BCL <see cref="System.Net.Mail.SmtpClient"/>
    /// supports only STARTTLS (RFC 3207). <c>EnableSsl</c> is a boolean; there is no
    /// separate "implicit TLS" flag, and the .NET API reference is explicit that
    /// the alternate "SSL session established up front" model (port 465, a.k.a.
    /// SMTPS) is <i>not</i> supported by <c>SmtpClient</c>. Offering an <c>Ssl</c>
    /// value here would mislead the operator into thinking 465 works — it does not.
    /// If a relay exposes <b>only</b> 465, swap out this implementation for a
    /// hand-rolled <c>Sockets</c>/<c>SslStream</c> client before setting
    /// <c>SMTP__Port=465</c> in env; do not configure it on this code path.
    /// </para>
    /// <para>
    /// Anything else is a configuration error and <see cref="SmtpSender.SendAsync"/>
    /// throws before opening the client, so a typo'd value fails fast instead of
    /// silently talking plain to a relay that expects TLS.
    /// </para>
    /// </summary>
    public string Secure { get; set; } = SecureTls;

    /// <summary>STARTTLS (conventional 587). The default — the only encrypted shape the BCL supports.</summary>
    public const string SecureTls = "Tls";

    /// <summary>No encryption — plain SMTP. Local-only (Mailpit, localhost relay).</summary>
    public const string SecureNone = "None";

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

    /// <summary>
    /// Maps <see cref="SmtpOptions.Secure"/> onto the BCL's single
    /// <see cref="System.Net.Mail.SmtpClient.EnableSsl"/> switch. Only two shapes
    /// are legitimate: <c>Tls</c> → <c>EnableSsl = true</c> (STARTTLS),
    /// <c>None</c> → <c>EnableSsl = false</c> (plain). The BCL's <c>EnableSsl</c>
    /// is STARTTLS-only (the .NET reference document is explicit that the implicit
    /// TLS / SMTPS model — port 465 — is <b>not</b> supported by <c>SmtpClient</c>);
    /// an <c>Ssl</c> / 465 relay therefore does not work on this code path and the
    /// operator needs to either swap the relay to one that exposes a STARTTLS
    /// port (most do — 587 is the standard) or replace this implementation before
    /// attempting it.
    /// </summary>
    private static bool ResolveEnableSsl(string? secure)
    {
        if (string.IsNullOrWhiteSpace(secure)) return true;     // default: Tls
        return secure.Trim().ToLowerInvariant() switch
        {
            "tls"  => true,
            "none" => false,
            // Fail fast: a typo'd value (e.g. "starttls", "STARTTLS", "Ssl")
            // should not silently fall through to a default the operator did
            // not intend — the .NET API reference is explicit that the BCL
            // does not support the port-465 implicit TLS shape that "Ssl"
            // might suggest, so the guard message needs to say so.
            _ => throw new InvalidOperationException(
                $"SMTP__Secure value '{secure}' is not supported. " +
                $"Recognized values: {SmtpOptions.SecureTls} (STARTTLS, the BCL's only TLS mode; " +
                "use the relay's STARTTLS port, conventionally 587), or " +
                $"{SmtpOptions.SecureNone} (plain SMTP, local-only). " +
                "Note: the BCL SmtpClient does not support implicit TLS (port 465 / SMTPS) — " +
                "pick a relay that exposes a STARTTLS port instead.")
        };
    }

    /// <summary>
    /// The "exactly one or zero" invariant on credentials (SmtpOptions.User /
    /// SmtpOptions.Pass): both set → AUTH, neither set → no AUTH sent — and
    /// exactly one set is a configuration error, not a silent relay handshake
    /// failure. Throwing here (before the <c>SmtpClient</c> is even constructed)
    /// keeps the failure in the "SMTP is not configured" message shape the
    /// durable handler's retry policy already inspects (SmtpException /
    /// TimeoutException / MailAddressException / ArgumentNullException), not a
    /// half-open connection failure that shows up as something new.
    /// </summary>
    private static SmtpClient CreateClient(SmtpOptions cfg)
    {
        bool hasUser  = !string.IsNullOrWhiteSpace(cfg.User);
        bool hasPass  = !string.IsNullOrWhiteSpace(cfg.Pass);
        if (hasUser != hasPass)
            throw new InvalidOperationException(
                "SMTP__User and SMTP__Pass must be set together (or neither). " +
                "Current shape: " +
                $"User={(hasUser ? "set" : "unset")}, Pass={(hasPass ? "set" : "unset")}. " +
                "No email was sent; the durable handler will retry / dead-letter per the configured policy.");

        var client = new SmtpClient
        {
            Host = cfg.Host!,
            Port = cfg.Port,
            EnableSsl = ResolveEnableSsl(cfg.Secure),
            // BCL precedence (when both are set, the explicit credentials win —
            // UseDefaultCredentials is only used as a fallback). We set it only
            // when explicitly requested; the default (false) is the common case.
            UseDefaultCredentials = cfg.UseDefaultCredentials
        };

        if (hasUser && hasPass)
        {
            // The BCL exposes SMTP auth via the `Credentials` property (a
            // NetworkCredential) — assign the pair rather than calling a method.
            // This is the shape Mailgun / Resend / MailerSend / Postmark /
            // corporate SMTP expect.
            client.Credentials = new System.Net.NetworkCredential(cfg.User, cfg.Pass);
        }

        return client;
    }

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

        using var client = CreateClient(_cfg);

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
