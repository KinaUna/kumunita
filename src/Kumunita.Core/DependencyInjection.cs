using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// <c>Kumunita.Core</c>'s composition-root surface (ADR 0006-D: Core carries no HTTP
/// types) — the host (Web) calls <see cref="AddKumunitaCore"/> at startup to register
/// the domain services it resolves.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <c>Kumunita.Core</c>'s domain services.
    /// <see cref="IUserInfoService"/> → <see cref="UserInfoService"/>,
    /// <see cref="IAuthorizationService"/> → <see cref="AuthorizationService"/>,
    /// <see cref="IIdentityService"/> → <see cref="IdentityService"/>,
    /// <see cref="IMailerStage"/> → <see cref="OutboxEmailStager"/> (step-6 staging: the
    /// domain write and the staged <c>OutboxEmail</c> row commit atomically in the
    /// caller's session — invariant C3).
    /// <para>
    /// <b>Step-7 surface (Wolverine side effects):</b>
    /// <see cref="ISmtpSender"/> → <see cref="SmtpSender"/> is the per-attempt SMTP
    /// seam that the durable <c>OutboxEmail</c> handler (in the host — see
    /// <c>Kumunita.Web/SideEffects/OutboxEmailHandler</c>) calls for each delivery
    /// attempt. It is a Core-agnostic BCL implementation (no Wolverine, no HTTP) so
    /// the ADR 0006-D "no HTTP types / testable" boundary holds and the harness can
    /// substitute a fake <see cref="ISmtpSender"/> to drive the failure/retry/
    /// dead-letter assertions without a live relay.
    /// <para>
    /// Their dependencies are the host-registered <see cref="Marten.IDocumentStore"/>
    /// (Marten's <c>AddMarten</c>, already called in the host startup path) and — for
    /// <see cref="IIdentityService"/> — the <c>identity</c>-schema
    /// <c>UserManager</c>/<c>RoleManager</c>, <see cref="IClaimsSource"/>, and the
    /// <c>Verification</c>/<c>SeedAdmin</c>/<c>SMTP</c> options, all of which the
    /// host also registers.
    /// </para>
    /// </summary>
    public static IServiceCollection AddKumunitaCore(this IServiceCollection services)
    {
        services.AddTransient<IUserInfoService, UserInfoService>();
        services.AddTransient<IAuthorizationService, AuthorizationService>();
        services.AddTransient<IIdentityService, IdentityService>();
        services.AddTransient<IMailerStage, OutboxEmailStager>();

        // M2 (plan U5): the directory-side composition root — a concrete class (it
        // composes two *seams*, not itself a seam: no interface, ADR 0006-D's
        // "single seam" rule applies to the *modules* it calls, not to the caller).
        services.AddTransient<DirectoryService>(sp =>
            new DirectoryService(
                sp.GetRequiredService<IUserInfoService>(),
                sp.GetRequiredService<IAuthorizationService>()));

        // Step-8 (M1 plan): the /health degraded seam (OPS §8) — counts
        // EmailDeadLetter rows through a Marten IQuerySession. Web-side consumers
        // (HealthController) resolve this, so tests can substitute a canned count
        // without a live Postgres.
        // M3 (plan U6): the posts-side composition root — a concrete class pairing
        // the two frozen seams with the host-registered Marten IDocumentStore
        // (mirrors the M2 DirectoryService registration shape above).
        services.AddTransient<Posts.PostService>(sp => new Posts.PostService(
            sp.GetRequiredService<IUserInfoService>(),
            sp.GetRequiredService<IAuthorizationService>(),
            sp.GetRequiredService<Marten.IDocumentStore>()));

        // M4 (the "platform announcements" lane, bounded context
        // Kumunita.Core.Announcements): the service seam — a store-composing
        // service kept behind an interface so the Web-side consumer (the
        // AnnouncementController) can be tested without a live Postgres
        // (mirrors IEmailDeadLetterCounter's registration pattern; the
        // scope-vs-role split lives inside CreateAsync, not in a
        // separate IUserInfoService / IAuthorizationService pairing).
        services.AddTransient<Announcements.IAnnouncementService, Announcements.AnnouncementService>();

        // M3b (plan U7):
        // (bounded context Kumunita.Core.Moderation) pairing the two frozen M1/M2
        // seams with the host-registered Marten IDocumentStore (the same
        // "concrete service in the Core composition root" shape as M2 U5's
        // DirectoryService above + M3 U6's PostService). U7's ModerationController
        // resolves this directly; U3/U4/U5 never needed the registration because
        // they tested with a manual-instance (the M3 PostServiceTests harness shape).
        services.AddTransient<Moderation.ModerationService>(sp => new Moderation.ModerationService(
            sp.GetRequiredService<IUserInfoService>(),
            sp.GetRequiredService<IAuthorizationService>(),
            sp.GetRequiredService<Marten.IDocumentStore>()));

        services.AddTransient<IEmailDeadLetterCounter, EmailDeadLetterCounter>();

        // Step-7 (M1 plan): the per-attempt SMTP seam. The durable policy (6 attempts
        // / ~24h / dead-letter) is configured by the host's Wolverine handler against
        // this implementation; the harness overrides this registration with a fake
        // to drive failure/retry/dead-letter assertions without a live relay.
        services.AddTransient<ISmtpSender, SmtpSender>();

        // Step-7 (M1 plan §6.4): the AuditPurge tiering is per-instance config,
        // not improvised (§5: "the purge decision ... is set in the job's config").
        services.AddOptions<Authorization.AuditPurgeOptions>();
        return services;
    }
}

