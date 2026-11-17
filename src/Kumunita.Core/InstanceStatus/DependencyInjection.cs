using Kumunita.Core.Identity;
using Kumunita.Core.Usage;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Kumunita.Core.InstanceStatus;

/// <summary>
/// The <c>InstanceStatus</c> module's self-registration seam (the
/// <see cref="Kumunita.Core.Usage.UsageModule.AddUsage"/> /
/// <see cref="Kumunita.Core.Identity.IdentityModule"/> shape — each module folder
/// owns its own wiring; <see cref="Kumunita.Core.ServiceCollectionExtensions.AddKumunitaCore"/>
/// lists the modules). One new **transient read seam**:
/// <see cref="IInstanceStatusService"/> → <see cref="InstanceStatusService"/>,
/// composed from the seams the host already registers (the Marten
/// <see cref="IDocumentStore"/>, the <see cref="IEmailDeadLetterCounter"/> /
/// <see cref="ISmtpHealthCheck"/> Identity seams, the
/// <see cref="IStorageMetricsService"/> Usage seam, the
/// <see cref="UserManager{User}"/> Identity seam, and the host-bound
/// <c>IOptions&lt;T&gt;</c> for the four options POCOs). Read-only, zero writes,
/// no new doc type, no new authorization surface (ADR 0006-D — Core stays
/// HTTP-free; the <c>/admin/status</c> page in O02 is the only place this
/// becomes HTTP, and the GlobalAdmin gate there is the Web's, not this seam's).
/// </summary>
public static class InstanceStatusModule
{
    /// <summary>Registers the <c>InstanceStatus</c> operator-plane read seam.</summary>
    public static IServiceCollection AddInstanceStatus(this IServiceCollection services)
    {
        // IMPROVE-UX O01 (D8) — the operator's read-only instance-status seam.
        // Transient (a fresh <see cref="InstanceStatusService"/> per resolution
        // reads the current process uptime — the <see cref="MailAlarmsService"/>
        // static / transient read-seam shape). It reuses the host-registered
        // seams (the IDocumentStore, the IEmailDeadLetterCounter /
        // ISmtpHealthCheck / IStorageMetricsService / UserManager&lt;User&gt;
        // seams, and the IOptions&lt;T&gt; for the four options POCOs); it
        // composes them, it does not re-derive any read, and it changes none
        // of them.
        // IMPROVE-UX O01 — the F18 T4 mail-alarm state read seam (the
        // IMailAlarmStateReader / MailAlarmStateReader — reuses the
        // IEmailDeadLetterCounter + the MailAlarmThresholdPolicy decision +
        // the SuppressedAddress window query shape). Transient, read-only,
        // no new doc type.
        services.AddTransient<IMailAlarmStateReader>(sp => new MailAlarmStateReader(
            sp.GetRequiredService<IDocumentStore>(),
            sp.GetRequiredService<IEmailDeadLetterCounter>()));

        // The operator's read-only instance-status seam. Transient (a fresh
        // InstanceStatusService per resolution reads the current process
        // uptime — the MailAlarmsService static / transient read-seam shape).
        // It reuses the host-registered seams (the IDocumentStore, the
        // IMailAlarmStateReader / ISmtpHealthCheck / IStorageMetricsService /
        // UserManager&lt;User&gt; seams, and the IOptions&lt;T&gt; for the four
        // options POCOs); it composes them, it does not re-derive any read,
        // and it changes none of them.
        services.AddTransient<IInstanceStatusService>(sp => new InstanceStatusService(
            sp.GetRequiredService<IDocumentStore>(),
            sp.GetRequiredService<IMailAlarmStateReader>(),
            sp.GetRequiredService<ISmtpHealthCheck>(),
            sp.GetRequiredService<IStorageMetricsService>(),
            sp.GetRequiredService<UserManager<User>>(),
            sp.GetRequiredService<IOptions<SeedAdminOptions>>(),
            sp.GetRequiredService<IOptions<SampleDataOptions>>(),
            sp.GetRequiredService<IOptions<HealthOptions>>(),
            sp.GetRequiredService<IOptions<InstanceStatusOptions>>()));

        return services;
    }
}
