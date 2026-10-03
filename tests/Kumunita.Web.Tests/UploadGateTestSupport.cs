using Kumunita.Core.Usage;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// M25 U8 — test-infrastructure helper (not a test; adds no test case). U8
/// adopted the Web-only <see cref="IUploadGate"/> in the four upload lanes,
/// and each lane now resolves <see cref="IStorageSettingsService"/> +
/// <see cref="IUploadGate"/> from <c>HttpContext.RequestServices</c>. The
/// pre-existing per-lane harnesses built a bare <see cref="DefaultHttpContext"/>
/// (whose <c>RequestServices</c> is <c>null</c>), which would NRE on the new
/// gate adoption. This wires a provider into that <c>RequestServices</c> so
/// the existing suite keeps running against the real gate.
/// <para>
/// <b>Why the two MVC factory stubs are required (probe-verified):</b>
/// <see cref="DefaultHttpContext.RequestServices"/> defaults to <c>null</c>,
/// and MVC's <c>ControllerBase.get_Url()</c> / <c>get_TempData()</c> are
/// guarded with <c>if (RequestServices is not null)</c> — so with a null
/// provider they fall back to <c>NullUrlHelper</c> / <c>NullTempDataDictionary</c>
/// and <c>RedirectToAction</c> / <c>View(...)</c> succeed. The moment a
/// harness is given a <em>non-null</em> provider (which U8's gate resolution
/// needs), that null-guard is bypassed and
/// <c>GetRequiredService&lt;IUrlHelperFactory&gt;()</c> /
/// <c>GetRequiredService&lt;ITempDataDictionaryFactory&gt;()</c> <em>throws</em>
/// unless the provider satisfies them. So this provider registers the two gate
/// seams <em>and</em> faithful no-op versions of those two factories (their
/// <c>Action</c> returns <see cref="string.Empty"/>, exactly what
/// <c>NullUrlHelper</c> produced in the baseline), keeping the success-path
/// <c>RedirectToActionResult</c> / <c>ViewResult</c> assertions unchanged.
/// </para>
/// <para>
/// The stub <see cref="IStorageSettingsService"/> returns a settings doc with
/// <see cref="CommunityStorageSettings.MaxFileBytes"/> = <c>null</c> (→ fall
/// back to the <c>envMaxBytes</c> the lane passes = the harness
/// <see cref="MediaOptions.MaxBytes"/>) and <see cref
/// "CommunityStorageSettings.PerUserQuotaBytes"/> = <c>0</c> (→ quota disabled,
/// the C-UP·5 sentinel). <see cref
/// "IStorageSettingsService.GetPerUserUsageBytesAsync"/> returns 0 (no prior
/// usage). The <b>real</b> <see cref="UploadGate"/> then runs the <b>pure</b>
/// Core <see cref="StorageLimits.Decide"/> — so a harness that sets
/// <c>MaxBytes = 16</c> and a 32-byte payload still gets <see
/// cref="StorageDecision.Oversize"/> (→ the gate's 413), and a sub-limit
/// payload still gets <see cref="StorageDecision.Allowed"/> (→ null → proceed).
/// The existing oversize/success/wrong-type/empty assertions are therefore
/// unchanged — only the 413's producer moved from an inline guard to the gate.
/// </para>
/// </summary>
internal static class UploadGateTestSupport
{
    /// <summary>
    /// Builds an <see cref="IServiceProvider"/> carrying the stub
    /// <see cref="IStorageSettingsService"/> and the real <see
    /// cref="UploadGate"/>, ready to assign to
    /// <see cref="HttpContext.RequestServices"/>.
    /// </summary>
    /// <param name="settings">The community settings doc the stub returns
    /// (default: <c>MaxFileBytes = null</c> → the lane's
    /// <c>MediaOptions.MaxBytes</c> is the env fallback;
    /// <c>PerUserQuotaBytes = 0</c> → quota disabled, the C-UP·5 sentinel).</param>
    /// <param name="currentUsageBytes">The subject's pre-existing usage in
    /// bytes (the C-SM·7 read <see cref
    /// "IStorageSettingsService.GetPerUserUsageBytesAsync"/> returns). Default
    /// <c>0</c> (no prior usage) — the U8 baseline. The U9 over-quota fixture
    /// passes a positive value so <c>usage + incoming &gt; PerUserQuotaBytes</c>
    /// (F3) while <c>MaxFileBytes</c> stays large (not oversize).</param>
    public static IServiceProvider ServicesWith(
        CommunityStorageSettings? settings = null,
        long currentUsageBytes = 0)
    {
        settings ??= new CommunityStorageSettings { MaxFileBytes = null, PerUserQuotaBytes = 0 };

        var settingsSvc = Substitute.For<IStorageSettingsService>();
        settingsSvc.GetOrCreateAsync(Arg.Any<CancellationToken>())
            .Returns(settings);
        settingsSvc.GetPerUserUsageBytesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(currentUsageBytes);

        return new ServiceCollection()
            .AddSingleton<IStorageSettingsService>(settingsSvc)
            .AddSingleton<IUploadGate>(new UploadGate(settingsSvc))
            // The two success-path MVC lookups the non-null provider must
            // satisfy (see class doc). No-op versions mirroring the baseline's
            // NullUrlHelper / NullTempDataDictionary fallback:
            .AddSingleton<IUrlHelperFactory>(EmptyUrlHelperFactory())
            .AddSingleton<ITempDataDictionaryFactory>(StubTempDataFactory())
            .BuildServiceProvider();
    }

    /// <summary>
    /// A no-op <see cref="IUrlHelperFactory"/> (NSubstitute, so the full
    /// interface surface is covered) whose <c>GetUrlHelper</c> returns a no-op
    /// <see cref="IUrlHelper"/> — the baseline's null-RequestServices path fell
    /// back to <c>NullUrlHelper</c> (whose <c>Action</c> returns
    /// <see cref="string.Empty"/>). A stub returning <c>null</c>/<c>""</c> from
    /// <c>Action</c> is an equivalent no-op: <see cref="RedirectToActionResult
    /// .ActionName"/> (the assertion target) is set directly, and <c>Url</c>
    /// (not asserted here) is empty.
    /// </summary>
    private static IUrlHelperFactory EmptyUrlHelperFactory()
    {
        var urlHelper = Substitute.For<IUrlHelper>();
        urlHelper.Action(Arg.Any<UrlActionContext>()).Returns(string.Empty);
        var factory = Substitute.For<IUrlHelperFactory>();
        factory.GetUrlHelper(Arg.Any<ActionContext>()).Returns(urlHelper);
        return factory;
    }

    /// <summary>
    /// A no-op <see cref="ITempDataDictionaryFactory"/> (NSubstitute) whose
    /// <c>GetTempData</c> returns a stub <see cref="ITempDataDictionary"/> —
    /// the baseline's null-RequestServices path fell back to
    /// <c>NullTempDataDictionary</c>; a non-null stub is an equivalent no-op for
    /// the <c>View</c> / <c>TempData</c> reads these tests make.
    /// </summary>
    private static ITempDataDictionaryFactory StubTempDataFactory()
    {
        var factory = Substitute.For<ITempDataDictionaryFactory>();
        factory.GetTempData(Arg.Any<HttpContext>())
            .Returns(Substitute.For<ITempDataDictionary>());
        return factory;
    }
}
