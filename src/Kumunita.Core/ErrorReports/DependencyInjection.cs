using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>ErrorReports</c> bounded context's DI
/// registration, extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The error-report
/// lane (client-side error reporting + triage). Core stays HTTP-free (ADR
/// 0006-D).
/// </summary>
public static class ErrorReportsModule
{
    /// <summary>Registers the <c>ErrorReports</c> bounded context's seam.</summary>
    public static IServiceCollection AddErrorReports(this IServiceCollection services)
    {
        services.AddTransient<ErrorReports.IErrorReportService>(sp => new ErrorReports.ErrorReportService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<ErrorReports.IEscalationTokenService>()));
        services.AddTransient<ErrorReports.IEscalationTokenService>(sp => new ErrorReports.EscalationTokenService(
            sp.GetRequiredService<Marten.IDocumentStore>()));
        services.AddTransient<ErrorReports.IEscalationOutboundConfigService>(sp => new ErrorReports.EscalationOutboundConfigService(
            sp.GetRequiredService<Marten.IDocumentStore>()));
        return services;
    }
}
