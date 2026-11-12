using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.InstanceStatus;
using Kumunita.Core.Media;
using Kumunita.Core.Portability;
using Kumunita.Core.Targets;
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
        // C10 (IMPROVE-CODE) — the thin composer (the module self-registration
        // seam, FACES: Adaptive). Each module folder owns its own wiring;
        // this method lists the modules instead of the ~50 registrations.
        // The order of calls is irrelevant to DI resolution (the container
        // resolves by type at resolution time, not by registration order);
        // the grouping is by bounded context for readability.

        // Identity + mail + audit (step-7 / M1 plan)
        services.AddIdentity();

        // UserInfo cluster (M1/M2/M13/M23)
        services.AddUserInfo();

        // Authorization + AuditPurge (step-7). Named AddKumunitaAuthorization
        // to avoid colliding with ASP.NET's AddAuthorization at the host site.
        services.AddKumunitaAuthorization();

        // Site content + admin surfaces (SITE / M29 / M30 / M31 / ESC)
        services.AddSiteContent();
        services.AddSurfaceLabels();
        services.AddAdminOnboarding();
        services.AddErrorReports();
        services.AddKiosk();

        // Mentions (M36)
        services.AddMentions();

        // Content surfaces (M3 / M5 / M9 / M17 / M21 / PG / TG)
        services.AddPosts();
        services.AddProjects();
        services.AddEvents();
        services.AddAnnouncements();
        services.AddPages();
        services.AddTags();
        services.AddDocuments();
        services.AddBookmarks();
        services.AddPersonalNotes();
        services.AddMessaging();
        services.AddGroupMessaging();
        services.AddInventory();
        services.AddTargets();

        // Cross-cutting (M6 / M8 / M16 / ML)
        services.AddNotifications();
        services.AddSearch();
        services.AddModeration();
        services.AddLocalization();
        services.AddSessions();

        // Operator plane (M11 / M27)
        services.AddPortability();

        // Byte I/O (Media / F16)
        services.AddMedia();

        // Operator analytics (M13 / M24 / M25)
        services.AddUsage();

        // IMPROVE-UX O01 (D8) — the operator's read-only instance-status seam
        // (the /admin/status page in O02, the dashboard notice in O03, the
        // inbox alarm in O04). Read-only, no new doc type, no new auth surface.
        services.AddInstanceStatus();

        return services;
    }
}

