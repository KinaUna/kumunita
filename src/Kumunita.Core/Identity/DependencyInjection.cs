using Kumunita.Core.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>Identity</c> bounded context's DI registration,
/// extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The module owns:
/// the identity account lanes (<see cref="IIdentityService"/>), the step-6/7
/// mail seams (<see cref="IMailerStage"/> → <see cref="OutboxEmailStager"/>,
/// <see cref="IEmailDeadLetterCounter"/> → <see cref="EmailDeadLetterCounter"/>,
/// <see cref="ISmtpHealthCheck"/> → <see cref="SmtpHealthCheck"/>,
/// <see cref="ISmtpSender"/> → <see cref="SmtpSender"/>), and the F11 alert
/// seam's off-by-default <see cref="NoopAlertSink"/> binding (a channel sink is
/// registered LATER in the host's Program.cs so it wins DI resolution).
/// <para>
/// ADR 0077: the <see cref="IIdentityService"/>'s optional
/// <c>NotificationService?</c> ctor seam resolves automatically from the
/// Notifications module (the default value is only used when the type is
/// unregistered — the two direct-construction Core test harnesses omit it). No
/// factory needed here.
/// </para>
/// </summary>
public static class IdentityModule
{
    /// <summary>Registers the <c>Identity</c> bounded context's domain services + mail seams.</summary>
    public static IServiceCollection AddIdentity(this IServiceCollection services)
    {
        services.AddTransient<IIdentityService, IdentityService>();

        // Step-7 (C3 fix, plan U2): OutboxEmailStager now also enqueues the
        // durable message envelope via Wolverine IMessageContext (Core's new
        // direct WolverineFx dependency — see Kumunita.Core.csproj +
        // IMailerStage.cs), so it needs the per-scope context injected
        // (factory form, same idiom as DirectoryService / Posts.PostService /
        // Moderation.ModerationService).
        services.AddTransient<IMailerStage>(sp =>
            new OutboxEmailStager(sp.GetRequiredService<Wolverine.IMessageContext>()));

        services.AddTransient<IEmailDeadLetterCounter, EmailDeadLetterCounter>();

        // The /health mail-reachability seam (OPS §8): a sockets-level SMTP
        // handshake against the bound SmtpOptions — reports false rather than
        // throwing when the relay is unreachable, so HealthController can map
        // it to "mail": "unreachable".
        services.AddTransient<ISmtpHealthCheck, SmtpHealthCheck>();

        // Step-7 (M1 plan): the per-attempt SMTP seam. The durable policy
        // (6 attempts / ~24h / dead-letter) is configured by the host's
        // Wolverine handler; the harness overrides this registration with a
        // fake to drive failure/retry/dead-letter assertions without a live
        // relay.
        services.AddTransient<ISmtpSender, SmtpSender>();

        // F11 (JOB-02, prod-hardening): the alert seam's DEFAULT binding.
        // NoopAlertSink is off-by-default (the F10 DSN-gated no-op idiom): an
        // unconfigured instance sends nothing anywhere — the /health degraded
        // gate is the always-on signal, the alert is the additive push (pin
        // §3.0). A channel binding (the Sentry-backed SentryAlertSink) is
        // registered LATER in Program.cs so it wins DI resolution.
        services.AddTransient<IAlertSink, NoopAlertSink>();

        return services;
    }
}
