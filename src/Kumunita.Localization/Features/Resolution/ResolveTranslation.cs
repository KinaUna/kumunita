using Marten;

namespace Kumunita.Localization.Features.Resolution;

public record ResolveTranslation(
    string Module,
    string Feature,
    string Key,
    string? PreferredLanguage,       // from user token claim
    IEnumerable<string> BrowserLanguages); // from Accept-Language header

public record ResolvedTranslation(string Value, string ResolvedLanguage);

public static class ResolveTranslationHandler
{
    public static async Task<ResolvedTranslation> Handle(
        ResolveTranslation query,
        IQuerySession session, // read-only — no transaction needed
        TranslationResolver resolver,
        CancellationToken ct)
    {
        return await resolver.ResolveAsync(query, session, ct);
    }
}