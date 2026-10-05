using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

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
    /// Throws <see cref="MailKit.Net.Smtp.SmtpCommandException"/> (AUTH / mailbox /
    /// delivery rejections), <see cref="MailKit.Net.Smtp.SmtpProtocolException"/>
    /// or <see cref="MailKit.ProtocolException"/> (connection / TLS / protocol-level
    /// failures incl. connection drops), <see cref="MimeKit.ParseException"/>
    /// (malformed address), <see cref="ArgumentNullException"/> as appropriate;
    /// the host's retry policy inspects the exception type, not any envelope.
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
    /// Retained for environment compatibility: the BCL
    /// <c>System.Net.Mail.SmtpClient.UseDefaultCredentials</c> let a relay rely
    /// on the process's OS credentials (a rare Windows-service shape). MailKit
    /// has no equivalent — it authenticates via AUTH PLAIN / LOGIN / XOAUTH2
    /// against an explicit username — so this flag is a **no-op** under the
    /// MailKit transport (ADR 0131). Leave it at the default (false); a relay
    /// that relies on this shape predates the swap and needs the explicit
    /// <see cref="User"/> / <see cref="Pass"/> pair instead.
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

    /// <summary>STARTTLS (conventional 587). The default encrypted shape.</summary>
    public const string SecureTls = "Tls";

    /// <summary>Implicit TLS / SMTPS (conventional 465 — the "TLS up front" shape).</summary>
    public const string SecureSsl = "Ssl";

    /// <summary>No enforced encryption — take TLS if the relay offers it, else plain. Local-only (Mailpit, localhost relay).</summary>
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
/// <see cref="MailKit.Net.Smtp.SmtpClient"/> (MailKit) implementation of
/// <see cref="ISmtpSender"/>. One client instance per call (cheap to construct;
/// avoids holding a network socket across many email dispatches and keeps each
/// attempt independent in the presence of relay connection resets).
/// <para>
/// <b>Transport choice (ADR 0131):</b> the original implementation used the BCL
/// <c>System.Net.Mail.SmtpClient</c>. In the 2026-10-02 VPS deployment (Proton
/// Mail SMTP submission, <c>smtp.protonmail.ch:587</c>), the BCL client reached
/// <c>MAIL FROM</c> without a live AUTH session even though the *same* credentials
/// authenticated cleanly from the same container (curl), from the VPS host
/// (openssl), and from the app's own <see cref="SmtpProbe"/> handshake inside the
/// same process (/health <c>mail: "ok"</c>) — a live, reproducible BCL regression
/// on .NET 10. MailKit drives the identical
/// EHLO → STARTTLS → AUTH (PLAIN/LOGIN) sequence correctly and also adds implicit
/// TLS (465) support the BCL never had (the limitation documented in COOLIFY.md
/// §5.1B), which is why <see cref="SmtpOptions.SecureSsl"/> is now a first-class
/// value.
/// </para>
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
    /// Maps <see cref="SmtpOptions.Secure"/> onto MailKit's
    /// <see cref="SecureSocketOptions"/>. Three shapes:
    /// <c>Tls</c> → <see cref="SecureSocketOptions.StartTls"/> (STARTTLS, the
    /// conventional 587 shape; the default), <c>Ssl</c> →
    /// <see cref="SecureSocketOptions.SslOnConnect"/> (implicit TLS / SMTPS,
    /// port 465), <c>None</c> →
    /// <see cref="SecureSocketOptions.StartTlsWhenAvailable"/> (take TLS if
    /// the relay offers it, plain otherwise — the Mailpit / loopback-only
    /// shape). Anything unrecognized throws before the client is constructed,
    /// so a typo'd value fails fast instead of silently talking plain to a
    /// relay that expects TLS.
    /// </summary>
    private static SecureSocketOptions ResolveSecurity(string? secure)
    {
        if (string.IsNullOrWhiteSpace(secure)) return SecureSocketOptions.StartTls;  // default: Tls
        return secure.Trim().ToLowerInvariant() switch
        {
            "tls"  => SecureSocketOptions.StartTls,
            "ssl"  => SecureSocketOptions.SslOnConnect,
            "none" => SecureSocketOptions.StartTlsWhenAvailable,
            _ => throw new InvalidOperationException(
                $"SMTP__Secure value '{secure}' is not supported. " +
                $"Recognized values: {SmtpOptions.SecureTls} (STARTTLS — the conventional 587 shape), " +
                $"{SmtpOptions.SecureSsl} (implicit TLS / SMTPS — port 465), or " +
                $"{SmtpOptions.SecureNone} (no enforced encryption; local-only).")
        };
    }

    /// <summary>
    /// The "exactly one or zero" invariant on credentials (SmtpOptions.User /
    /// SmtpOptions.Pass): both set → AUTH, neither set → no AUTH sent — and
    /// exactly one set is a configuration error, not a silent relay handshake
    /// failure. Throwing here (before the client is even constructed) keeps the
    /// failure in the "SMTP is not configured" message shape the durable
    /// handler's retry policy already inspects (MailKit.SmtpCommandException /
    /// MailKit.ProtocolException / MailAddressException / ArgumentNullException),
    /// not a half-open connection failure that shows up as something new.
    /// </summary>
    private static MailKit.Net.Smtp.SmtpClient CreateClient(SmtpOptions cfg)
    {
        bool hasUser  = !string.IsNullOrWhiteSpace(cfg.User);
        bool hasPass  = !string.IsNullOrWhiteSpace(cfg.Pass);
        if (hasUser != hasPass)
            throw new InvalidOperationException(
                "SMTP__User and SMTP__Pass must be set together (or neither). " +
                "Current shape: " +
                $"User={(hasUser ? "set" : "unset")}, Pass={(hasPass ? "set" : "unset")}. " +
                "No email was sent; the durable handler will retry / dead-letter per the configured policy.");

        var client = new MailKit.Net.Smtp.SmtpClient
        {
            // Parity with the BCL default X.509 policy check (chain + name + CRL)
            // — the .NET reference for the original implementation documented the
            // CRL check explicitly, so we keep it opt-in-true rather than
            // inheriting MailKit's default (which is also true, but pinning it
            // here keeps the intent visible for future readers).
            CheckCertificateRevocation = true
        };
        // cfg.UseDefaultCredentials is a documented no-op under MailKit (see the
        // property doc) — nothing to configure here.

        return client;
    }

    /// <summary>
    /// Connect (STARTTLS or implicit TLS per <see cref="SmtpOptions.Secure"/>)
    /// + AUTH when the credential pair is set. Split from <see cref="CreateClient"/>
    /// so the invariant guard stays a pure configuration check and the network
    /// handshake is exercised in one place — the same EHLO → STARTTLS → AUTH
    /// sequence <see cref="SmtpProbe"/> validates on the /health path, driven
    /// through the same MailKit API a real send uses.
    /// </summary>
    private static async Task ConnectAndAuthenticateAsync(
        MailKit.Net.Smtp.SmtpClient client, SmtpOptions cfg, CancellationToken ct)
    {
        await client.ConnectAsync(cfg.Host!, cfg.Port, ResolveSecurity(cfg.Secure), ct).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(cfg.User) && !string.IsNullOrWhiteSpace(cfg.Pass))
        {
            // Mailgun / Resend / MailerSend / Postmark / Proton Mail all expect
            // AUTH PLAIN or LOGIN — the mechanisms MailKit advertises by default.
            await client.AuthenticateAsync(cfg.User, cfg.Pass, ct).ConfigureAwait(false);
        }
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

        // MimeMessage.From is read-only in MailKit 4.x — set it through the
        // 4-arg constructor (from, to, subject, body) rather than patching
        // after the fact. The body is Markdown (§6.2: "rendered Markdown");
        // relays don't re-render it, so a plain-text MIME part is the honest
        // shape.
        IEnumerable<InternetAddress>? fromList = string.IsNullOrWhiteSpace(_cfg.From)
            ? null
            : new InternetAddress[] { new MailboxAddress(null, _cfg.From) };
        var toList = new InternetAddress[] { new MailboxAddress(null, email.Recipient) };
        var msg = new MimeMessage(fromList, toList, email.Subject, new TextPart("plain") { Text = email.Body });

        // X-Message-Id carries the per-email idempotency key (§6.2) — a relay-side
        // duplicate signal, not a delivery guarantee (that belongs to the caller's
        // committed OutboxEmail row, not to this transmission).
        msg.Headers.Add("X-Message-Id", email.IdempotencyKey);
        msg.Headers.Add("X-Kumunita-Recipient", email.Recipient);

        try
        {
            await ConnectAndAuthenticateAsync(client, _cfg, ct).ConfigureAwait(false);
            await client.SendAsync(msg, ct).ConfigureAwait(false);
            logger.LogInformation("Delivered email {Idp} to {Recipient} (attempt sent).", email.IdempotencyKey, email.Recipient);
        }
        catch (MailKit.Net.Smtp.SmtpCommandException)
        {
            // Re-throw as-is — the host's retry policy (Program.cs) inspects the
            // concrete type (MailKit.Net.Smtp.SmtpCommandException for AUTH /
            // mailbox / delivery rejections; MailKit.ProtocolException for
            // connection / TLS failures) and the cooldown list drives the
            // 6-attempt / ~24h window (§6.2). The SmtpCommandException.Message
            // carries the relay's verbatim status line (e.g. "5.7.1 ... Sender
            // address rejected"), which is the operator signal the dead-letter
            // row is designed to surface.
            throw;
        }
        catch (MailKit.Net.Smtp.SmtpProtocolException)
        {
            // Connection reset mid-send, TLS handshake failure, relay dropping
            // the socket — the "relay is down / network path broken" shape the
            // retry policy is designed to absorb on cooldown. (The base
            // MailKit.ProtocolException covers the same surface for connection
            // drops at the socket level; both are retried per the host's policy.)
            throw;
        }
        catch (MailKit.ProtocolException)
        {
            // Base-class catch for any other connection-level failure (DNS,
            // socket reset, protocol parse) — same retry semantics.
            throw;
        }
        finally
        {
            // Best-effort teardown: a relay that reset the connection mid-send
            // (the shape that motivated the BCL-era try/catch) can leave the
            // local side in a limbo state; the next attempt should start clean.
            if (client.IsConnected)
            {
                try { client.Disconnect(true); } catch { /* best-effort */ }
            }
        }
    }
}
