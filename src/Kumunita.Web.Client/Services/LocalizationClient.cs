namespace Kumunita.Web.Client.Services;

/// <summary>
/// Fetches translations from the backend Localization module and
/// manages the client-side preferred language state.
/// </summary>
public interface ILocalizationClient
{
    /// <summary>
    /// Returns the translation for the given key in the current language,
    /// falling back through the platform's fallback chain if needed.
    /// Returns the key itself if no translation is found.
    /// </summary>
    ValueTask<string> GetAsync(string module, string feature, string key);

    /// <summary>
    /// Updates the user's preferred language and reloads translations.
    /// Persists the preference to the backend.
    /// </summary>
    Task SetPreferredLanguageAsync(string languageCode);

    /// <summary>The currently active language code.</summary>
    string CurrentLanguageCode { get; }

    /// <summary>Fired when the active language changes.</summary>
    event Action? OnLanguageChanged;
}

public class LocalizationClient : ILocalizationClient
{
    private readonly IApiClient _api;
    private readonly Dictionary<string, string> _cache = [];

    public string CurrentLanguageCode { get; private set; } = "en";

    public event Action? OnLanguageChanged;

    public LocalizationClient(IApiClient api) => _api = api;

    public async ValueTask<string> GetAsync(string module, string feature, string key)
    {
        var cacheKey = $"{module}:{feature}:{key}:{CurrentLanguageCode}";

        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        // TODO: batch translation fetches rather than one per key
        var result = await _api.GetAsync<TranslationResult>(
            $"/localization/{module}/{feature}/{key}?lang={CurrentLanguageCode}");

        var value = result?.Value ?? key;
        _cache[cacheKey] = value;
        return value;
    }

    public async Task SetPreferredLanguageAsync(string languageCode)
    {
        CurrentLanguageCode = languageCode;
        _cache.Clear(); // invalidate all cached translations

        // TODO: PUT /users/me/preferred-language
        await Task.CompletedTask;

        OnLanguageChanged?.Invoke();
    }

    private record TranslationResult(string Value);
}