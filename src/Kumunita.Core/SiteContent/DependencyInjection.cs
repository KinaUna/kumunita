using Kumunita.Core.SiteContent;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>SiteContent</c> bounded context's DI
/// registration, extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. SITE (ADR 0150,
/// plan U03): the landing-surface content seam — the SiteContent singleton's
/// read + audited write lanes (the ADR 0050 IsSignupOpen / SetSignupOpen
/// single-write-lane shape, the LocaleSettings-parallel bounded context).
/// Composes the host-registered Marten IDocumentStore (reads open a
/// QuerySession, the audited write opens a write session — invariant C3). Core
/// stays HTTP-free (ADR 0006-D): the Web gate (U07) is the only place the
/// GlobalAdmin authorization is produced.
/// </summary>
public static class SiteContentModule
{
    /// <summary>Registers the <c>SiteContent</c> bounded context's read + audited-write seams.</summary>
    public static IServiceCollection AddSiteContent(this IServiceCollection services)
    {
        services.AddTransient<ISiteContentService>(sp => new SiteContentService(
            sp.GetRequiredService<Marten.IDocumentStore>()));

        return services;
    }
}
