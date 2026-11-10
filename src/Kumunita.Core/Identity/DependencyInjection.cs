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
        // ADR 0301 (IMPROVE-UX R11) — the self-service deletion SCHEDULING
        // lane (the grace period above the IIdentityService.DeleteAccountAsync
        // seam — ADR 0301 D4 keeps that contract unchanged; the scheduler is a
        // sibling service, not a change to IIdentityService). The R12 due-job
        // resolves IDeletionScheduler for the ListDueAsync / MarkExecutedAsync
        // seams.
        services.AddTransient<IDeletionScheduler, DeletionScheduler>();
        // C3-07 (ADR 0297, D5 contract extraction) — the IInstanceBaseUrlReader
        // read contract over the host-bound VerificationOptions.BaseUrl value.
        // The Identity module owns both the source options and the contract, so
        // the binding stays here (the source module). This retires the
        // Notifications→Identity:VerificationOptions boundary edge — the
        // Notifications e-mail lane now codes against the interface, not the
        // concrete VerificationOptions policy type.
        // GetService (not GetRequiredService) — preserve the pre-C3-07 shape:
        // the host configures VerificationOptions, but a test harness that
        // composes the Core DI without that section resolves null (the
        // InstanceBaseUrlReader reports null → the relative-path fallback).
        services.AddTransient<IInstanceBaseUrlReader>(sp => new InstanceBaseUrlReader(
            sp.GetService<Microsoft.Extensions.Options.IOptions<VerificationOptions>>()));
        // C3-07 (ADR 0297, D5 contract extraction) — the IIdentityPrincipalStore
        // read + wipe contract over the EF Core identity-schema context (ADR 0004
        // §B — the only EF surface in the tree). The Identity module owns both
        // the AppDbContext and the contract, so the binding stays here; the
        // Portability module now codes against the interface, retiring the
        // Portability→Identity:AppDbContext boundary edge (the EF Core context
        // stays a private detail of the source module).
        services.AddTransient<IIdentityPrincipalStore>(sp => new IdentityPrincipalStore(
            sp.GetRequiredService<AppDbContext>()));
        // C3-03 (ADR 0293) — the platform-gate seam (the C08 test-compat facade
        // shape; the IIdentityService gate methods are thin forwarders to it and
        // keep compiling unchanged).
        services.AddTransient<IIdentityPlatformGateService>(sp => new IdentityPlatformGateService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetService<Microsoft.Extensions.Options.IOptions<SampleDataOptions>>(),
            sp.GetRequiredService<Kumunita.Core.UserInfo.IUserInfoService>(),
            sp.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<User>>()));

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
