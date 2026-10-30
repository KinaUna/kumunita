using System.Globalization;
using Kumunita.Core.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Web.Security;

/// <summary>
/// Localized flash (<c>TempData["info"]</c> / <c>["error"]</c>) text for a controller (ADR 0261).
/// </summary>
/// <remarks>
/// Resolves <see cref="ITranslationProvider"/> / <see cref="ILocalizationService"/> from
/// <c>HttpContext.RequestServices</c>, so a controller needs **no constructor change**. Fail-open
/// to the English floor passed by the caller: no <c>HttpContext</c>, no request services (a unit
/// test with a bare <c>DefaultHttpContext</c>), no provider, or a provider that returns nothing
/// usable all yield <paramref name="english"/> — the same floor discipline as the per-controller
/// <c>FlashAsync</c> copies. <c>{0}</c>/<c>{1}</c> placeholders use the invariant culture.
/// </remarks>
public static class ControllerFlashExtensions
{
    public static async Task<string> FlashAsync(this ControllerBase controller, string key, string english, params object?[] args)
    {
        var template = english;
        var services = controller.HttpContext?.RequestServices;
        var provider = services?.GetService<ITranslationProvider>();
        if (provider is not null)
        {
            var localization = services!.GetService<ILocalizationService>();
            if (localization is not null)
            {
                var lang = await EffectiveLanguageCode.ResolveAsync(controller.HttpContext!.Request, localization, provider);
                var resolved = await provider.GetAsync(key, lang);
                if (!string.IsNullOrWhiteSpace(resolved) && resolved != key)
                    template = resolved;
            }
        }
        return args.Length > 0 ? string.Format(CultureInfo.InvariantCulture, template, args) : template;
    }
}
