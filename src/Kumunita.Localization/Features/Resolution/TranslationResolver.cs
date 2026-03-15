using Kumunita.Localization.Domain;
using Marten;

namespace Kumunita.Localization.Features.Resolution;

public class TranslationResolver
{
    public async Task<ResolvedTranslation> ResolveAsync(
        ResolveTranslation query,
        IQuerySession session,
        CancellationToken ct)
    {
        // Step 1 — build candidate language list
        IEnumerable<string> candidates = BuildCandidateChain(
            query.PreferredLanguage,
            query.BrowserLanguages);

        // Step 2 — find the TranslationKey by module/feature/key
        TranslationKey? key = await session
            .Query<TranslationKey>()
            .FirstOrDefaultAsync(k =>
                k.Module == query.Module &&
                k.Feature == query.Feature &&
                k.Key == query.Key, ct);

        if (key is null)
            return new ResolvedTranslation(
                $"{query.Module}.{query.Feature}.{query.Key}", "none");

        // Step 3 — load all translations for this key
        IReadOnlyList<Translation> translations = await session
            .Query<Translation>()
            .Where(t => t.TranslationKeyId == key.Id)
            .ToListAsync(ct);

        // Step 4 — walk the candidate chain, prefer Approved
        foreach (string lang in candidates)
        {
            Translation? match = translations.FirstOrDefault(t =>
                t.LanguageCode == lang &&
                t.Status == TranslationStatus.Approved);

            if (match is not null)
                return new ResolvedTranslation(match.Value, lang);
        }

        // Step 5 — Draft English fallback
        Translation? draftEn = translations.FirstOrDefault(t =>
            t.LanguageCode == "en");

        if (draftEn is not null)
            return new ResolvedTranslation(draftEn.Value, "en-draft");

        // Step 6 — return key itself as final fallback
        return new ResolvedTranslation(
            $"{query.Module}.{query.Feature}.{query.Key}", "none");
    }

    private static IEnumerable<string> BuildCandidateChain(
        string? preferredLanguage,
        IEnumerable<string> browserLanguages)
    {
        List<string> candidates = new List<string>();

        if (!string.IsNullOrEmpty(preferredLanguage))
            candidates.Add(Normalize(preferredLanguage));

        foreach (string lang in browserLanguages)
        {
            string normalized = Normalize(lang);
            // Add both region variant and base language
            // e.g. "fr-BE" → adds "fr-BE" then "fr"
            if (!candidates.Contains(normalized))
                candidates.Add(normalized);

            string baseLang = normalized.Split('-')[0];
            if (!candidates.Contains(baseLang))
                candidates.Add(baseLang);
        }

        // English is always last resort
        if (!candidates.Contains("en"))
            candidates.Add("en");

        return candidates;
    }

    private static string Normalize(string code) =>
        code.Trim().ToLowerInvariant();
}