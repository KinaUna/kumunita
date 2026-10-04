using Kumunita.Core.Localization;
using Kumunita.Core.Media;
using Kumunita.Core.Usage;
using Kumunita.Web.Models;
using Microsoft.Extensions.Options;

namespace Kumunita.Web.Security;

/// <summary>
/// Resolves the per-file upload limit the upload gate enforces
/// (<see cref="StorageLimits.EffectiveMaxFileBytes"/>: the admin override, else
/// the <c>Media__MaxBytes</c> env fallback) as a localized hint for upload
/// forms and modals. Read-only; <c>0</c> (unlimited) yields no hint.
/// </summary>
public interface IUploadLimitHint
{
    /// <summary>The effective per-file limit in bytes (<c>0</c> = unlimited).</summary>
    Task<long> GetMaxBytesAsync();

    /// <summary>The localized "Maximum file size: X" text, or <c>null</c> when unlimited.</summary>
    Task<string?> GetTextAsync();
}

/// <inheritdoc/>
public sealed class UploadLimitHint(
    IStorageSettingsService storageSettings,
    IOptions<MediaOptions> mediaOpts,
    ITranslationProvider provider,
    ILocalizationService localization,
    IHttpContextAccessor httpContextAccessor) : IUploadLimitHint
{
    /// <summary>The translation key; its <c>{0}</c> is the formatted size.</summary>
    public const string Key = "upload.max_size";

    /// <inheritdoc/>
    public async Task<long> GetMaxBytesAsync()
    {
        var settings = await storageSettings.GetOrCreateAsync(CancellationToken.None);
        return StorageLimits.EffectiveMaxFileBytes(settings, mediaOpts.Value.MaxBytes);
    }

    /// <inheritdoc/>
    public async Task<string?> GetTextAsync()
    {
        var max = await GetMaxBytesAsync();
        if (max <= 0) return null;

        // Same language chain as the kw-l TagHelper: cookie pick, else the
        // browser's Accept-Language against the enabled catalog.
        var request = httpContextAccessor.HttpContext?.Request;
        var pref = request is null ? null : LocaleCookie.Read(request);
        string template;
        if (!string.IsNullOrWhiteSpace(pref))
            template = await provider.GetAsync(Key, pref);
        else if (request is not null)
        {
            var enabled = await localization.ListLanguagesAsync();
            template = await provider.GetAsync(Key, RequestLanguage.BrowserCandidates(request, enabled));
        }
        else
            template = await provider.GetAsync(Key, (string?)null);

        return template.Replace("{0}", ResidentStorageViewModel.FormatBytes(max));
    }
}
