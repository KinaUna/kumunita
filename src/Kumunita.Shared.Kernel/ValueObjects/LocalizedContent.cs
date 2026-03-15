namespace Kumunita.Shared.Kernel.ValueObjects;

public class LocalizedContent
{
    private readonly Dictionary<string, string> _translations = new();

    // Parameterless constructor required by Marten's deserializer
    public LocalizedContent() { }

    public LocalizedContent(string languageCode, string content)
    {
        Set(languageCode, content);
    }

    public LocalizedContent(Dictionary<string, string> name)
    {
        foreach ((string key, string value) in name)
        {
            Set(key, value);
        }
    }

    // Marten serializes this dictionary directly to JSON
    public IReadOnlyDictionary<string, string> Translations
        => _translations.AsReadOnly();

    public LocalizedContent Set(string languageCode, string content)
    {
        LanguageCode code = new LanguageCode(languageCode);
        _translations[code.Value] = content;
        return this; // fluent for builder-style usage
    }

    public string? Get(string languageCode)
        => _translations.TryGetValue(
            new LanguageCode(languageCode).Value, out string? value)
            ? value : null;

    public string Resolve(IEnumerable<string> candidateLanguages)
    {
        foreach (string lang in candidateLanguages)
        {
            string? value = Get(lang);
            if (value is not null) return value;
        }

        // Fall back to English, then first available, then empty
        return Get(LanguageCode.English)
               ?? _translations.Values.FirstOrDefault()
               ?? string.Empty;
    }

    public bool HasLanguage(string languageCode)
        => _translations.ContainsKey(new LanguageCode(languageCode).Value);
}