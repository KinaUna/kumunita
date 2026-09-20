using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Media;
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

        // Step-7 (C3 fix, plan U2): OutboxEmailStager now also enqueues the durable
        // message envelope via Wolverine IMessageContext (Core's new direct WolverineFx
        // dependency — see Kumunita.Core.csproj + IMailerStage.cs), so it needs the
        // per-scope context injected (factory form, same idiom as DirectoryService /
        // Posts.PostService / Moderation.ModerationService below).
        services.AddTransient<IMailerStage>(sp =>
            new OutboxEmailStager(sp.GetRequiredService<Wolverine.IMessageContext>()));

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
            sp.GetRequiredService<Marten.IDocumentStore>(),
            // TG (ADR 0044, U8b) — the tag-lane write seam (the
            // AttachToPostAsync / AttachToPageAsync / AddTagTranslationAsync
            // lanes). The registration composes the frozen seams only (the
            // ADR 0006-D lane pin) — the PostService's new ctor param is a
            // new dependency on PostService, not a new seam on a frozen
            // interface (§2.6).
            sp.GetRequiredService<Tags.ITagService>()));

        // M3b (the "platform announcements" lane, bounded context
        // Kumunita.Core.Announcements — part of M3's roadmap scope): the service seam — a store-composing
        // service kept behind an interface so the Web-side consumer (the
        // AnnouncementController) can be tested without a live Postgres
        // (mirrors IEmailDeadLetterCounter's registration pattern; the
        // scope-vs-role split lives inside CreateAsync, not in a
        // separate IUserInfoService / IAuthorizationService pairing).
        services.AddTransient<Announcements.IAnnouncementService, Announcements.AnnouncementService>();

        // PG (ADR 0039, plan U01): the pages-side service seam (bounded
        // context Kumunita.Core.Pages — the "hierarchical, audience-restricted,
        // translatable knowledge tree" lane that absorbs the static-page lane
        // in U07) — a store-composing service kept behind an interface so the
        // Web-side consumer (the U04 PageController) can be tested without a
        // live Postgres (the same "AddTransient with the store injected" shape
        // as IAnnouncementService above; U02/U03 add the read/write methods —
        // this unit is the seam + registration only).
        services.AddTransient<Pages.IPageService>(sp => new Pages.PageService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<Tags.ITagService>()));

        // TG (ADR 0044, plan U5/U6): the tags-side service (bounded context
        // Kumunita.Core.Tags — the ADR 0011 shared-id-doc lane that composes
        // only the frozen seams: IAuthorizationService + ITranslationProvider,
        // never a new AccessVia value beyond Owner/Admin). The write lanes
        // (AttachToPostAsync / AttachToPageAsync / AddTagTranslationAsync) take
        // the caller's IDocumentSession (C3) and the standing probes are pure;
        // U6's read lane (ListForActorAsync / ListPostsByTagAsync /
        // ListPagesByTagAsync / SuggestAsync) composes IAuthorizationService (the
        // content's own Read decision — C-TG·3 / C-TG·2) + ITranslationProvider
        // (display-name resolution, ADR 0005 / D5) over the host-registered
        // Marten IDocumentStore. The registration composes the frozen seams only
        // (the ADR 0006-D lane pin) — the ADR 0006-D seam is never a new seam.
        services.AddTransient<Tags.ITagService>(sp => new Tags.TagService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<IAuthorizationService>(),
            sp.GetRequiredService<Localization.ITranslationProvider>()));

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

        // The /health mail-reachability seam (OPS §8): a sockets-level SMTP
        // handshake against the bound SmtpOptions — resolves IOptions<SmtpOptions>
        // (which the host binds from the "SMTP" section in Program.cs) and
        // reports false rather than throwing when the relay is unreachable or
        // unconfigured, so HealthController can always map it to "mail": "unreachable".
        services.AddTransient<ISmtpHealthCheck, SmtpHealthCheck>();

        // Step-7 (M1 plan): the per-attempt SMTP seam. The durable policy (6 attempts
        // / ~24h / dead-letter) is configured by the host's Wolverine handler against
        // this implementation; the harness overrides this registration with a fake
        // to drive failure/retry/dead-letter assertions without a live relay.
        services.AddTransient<ISmtpSender, SmtpSender>();

        // Step-7 (M1 plan §6.4): the AuditPurge tiering is per-instance config,
        // not improvised (§5: "the purge decision ... is set in the job's config").
        services.AddOptions<Authorization.AuditPurgeOptions>();

        // Media (plan U1): the byte-I/O seam's per-instance config + the
        // local-volume implementation. Core stays HTTP-free (ADR 0006-D):
        // LocalVolumeFileStore is a BCL file-store behind IMediaFileStore.
        services.AddOptions<MediaOptions>();
        services.AddTransient<IMediaFileStore, LocalVolumeFileStore>();

        // Media (plan U2): the single decision-path module seam (C-MED·1). It
        // composes U1's byte seam (IMediaFileStore) with the host-registered
        // Marten IDocumentStore (registered by the host's AddMarten call — the
        // same "concrete service in the Core composition root" shape as M3 U6's
        // PostService above). Resolved by the Web serving lane (U7) + upload
        // lane (U6); never touched by the raw volume directly (C-MED·6).
        services.AddTransient<IMediaStore, LocalVolumeMediaStore>();

        // Multilingual (ML, ADR 0005; plan U4): the two Core seams U2/U3 shipped —
        // the per-request read path (ITranslationProvider, U2) and the admin
        // management seam (ILocalizationService, U3). Both take the host-registered
        // Marten IDocumentStore (the same "AddTransient with the store injected"
        // shape as IMediaStore above). Core stays HTTP-free (M·8): neither touches
        // an HttpRequest or a cookie — the Web layer passes the preferred language
        // as a plain BCP-47 string (M·5). Languages are data, not config (M·4):
        // an admin edit takes effect on the next request, no rebuild.
        services.AddTransient<Localization.ITranslationProvider, Localization.TranslationProvider>();
        services.AddTransient<Localization.ILocalizationService, Localization.LocalizationService>();

        // M4 (ADR 0054, plan U01): the Events bounded context's service seam (bounded
        // context Kumunita.Core.Events — the "coordination" arrow: the Event + EventRsvp
        // documents, the IEventService read/write surface, and the §6.4 reminder job).
        // A store-composing service kept behind an interface so the Web-side consumer
        // (the EventController, U05) can be tested without a live Postgres (the same
        // "AddTransient with the store injected" shape as IAnnouncementService /
        // IPageService above). U03/U04 land the read + write lanes; this unit is the
        // seam + registration only (the EventService skeleton throws
        // NotImplementedException until then — the "logic lands in U03/U04" pin).
        services.AddTransient<Events.IEventService>(sp => new Events.EventService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<IAuthorizationService>(),
            sp.GetRequiredService<IUserInfoService>()));
        return services;
    }
}

