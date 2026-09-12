using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Marten;

namespace Kumunita.Core.Localization;

/// <summary>
/// The per-request read seam's implementation (M·1–M·3, M·8, M·9).
/// <para>
/// <b>HTTP-free</b> (M·8): the preferred language arrives as a plain
/// <see cref="string"/> — the provider never touches a cookie or a claim.
/// It reads <b>only</b> the two content documents (<see cref="TranslationResource"/>
/// / <see cref="LocalizedPage"/>) plus the M1 seed (<see cref="LanguageCatalog"/>
/// / <see cref="LocaleSettings"/>) through a Marten <c>IQuerySession</c>; it
/// <b>never</b> reads a <c>Post</c> / <c>PostReply</c> / <c>Group</c> body
/// (M·3 — UGC is authored and rendered as written).
/// </para>
/// <para>
/// <b>Resolution order (M·1, M·9):</b> preferred (if enabled in the catalog)
/// → instance default (if enabled) → <c>"en"</c> (the source-language floor —
/// always seeded by the first-run seeder). Never returns null or empty.
/// </para>
/// <para>
/// <b>Per-string / per-page fallback (M·2):</b> a partially translated language
/// degrades gracefully per key (UI strings) and per slug (static pages) —
/// never as a whole view flipping to <c>"en"</c>. The last-resort floor for a
/// UI string is the key itself (a resident never sees a blank label); for a
/// static page it is <c>null</c> (the Web renders a 404).
/// </para>
/// </summary>
public sealed class TranslationProvider : ITranslationProvider
{
    private readonly IDocumentStore _store;

    public TranslationProvider(IDocumentStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Resolves the effective language (M·1) and builds the deduplicated
    /// fallback chain (M·2): effective → default → <c>"en"</c>.
    /// </summary>
    private async Task<(string effective, string[] chain)> ResolveChainAsync(
        string? preferredLanguageCode)
    {
        await using var session = _store.QuerySession();
        var ct = CancellationToken.None;

        var settings = await session
            .LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, ct)
            .ConfigureAwait(false);
        var defaultCode = settings?.DefaultLanguageCode ?? "en";

        var catalog = await session
            .Query<LanguageCatalog>()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // M·1: preferred (if enabled) → default (if enabled) → "en" (M·9 floor).
        string effective;
        if (!string.IsNullOrWhiteSpace(preferredLanguageCode)
            && catalog.Any(c => c.Id == preferredLanguageCode && c.Enabled))
        {
            effective = preferredLanguageCode;
        }
        else if (catalog.Any(c => c.Id == defaultCode && c.Enabled))
        {
            effective = defaultCode;
        }
        else
        {
            effective = "en";
        }

        // M·2: the per-string / per-page fallback chain (deduplicated, priority order).
        var chain = new List<string> { effective };
        if (defaultCode != effective)
            chain.Add(defaultCode);
        if (!chain.Contains("en"))
            chain.Add("en");

        return (effective, chain.ToArray());
    }

    /// <inheritdoc />
    public async Task<string> ResolveEffectiveLanguageAsync(string? preferredLanguageCode)
        => (await ResolveChainAsync(preferredLanguageCode).ConfigureAwait(false)).effective;

    /// <inheritdoc />
    public async Task<string> GetAsync(string key, string? preferredLanguageCode)
    {
        var (_, chain) = await ResolveChainAsync(preferredLanguageCode).ConfigureAwait(false);
        await using var session = _store.QuerySession();
        var ct = CancellationToken.None;

        // One query: all candidate rows for this key (M·2: per-string fallback).
        var rows = await session
            .Query<TranslationResource>()
            .Where(t => t.Key == key && chain.Contains(t.LanguageCode))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var lang in chain)
        {
            var match = rows.FirstOrDefault(r => r.LanguageCode == lang);
            if (match != null)
                return match.Text;
        }
        return key; // M·1: the key itself is the floor — a resident never sees a blank label.
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetManyAsync(
        IReadOnlyCollection<string> keys, string? preferredLanguageCode)
    {
        if (keys.Count == 0)
            return new Dictionary<string, string>();

        var (_, chain) = await ResolveChainAsync(preferredLanguageCode).ConfigureAwait(false);
        var keySet = keys.ToHashSet();

        // One query for all requested keys (M·2: no N round-trips).
        await using var session = _store.QuerySession();
        var ct = CancellationToken.None;
        var rows = await session
            .Query<TranslationResource>()
            .Where(t => keySet.Contains(t.Key) && chain.Contains(t.LanguageCode))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Per-string fallback (M·2): each key resolves independently.
        var result = new Dictionary<string, string>(keySet.Count);
        foreach (var key in keySet)
        {
            string? text = null;
            foreach (var lang in chain)
            {
                var match = rows.FirstOrDefault(r => r.Key == key && r.LanguageCode == lang);
                if (match != null)
                {
                    text = match.Text;
                    break;
                }
            }
            result[key] = text ?? key; // M·1: the key itself is the floor.
        }
        return result;
    }

    /// <inheritdoc />
    public async Task<LocalizedPage?> GetPageAsync(string slug, string? preferredLanguageCode)
    {
        var (_, chain) = await ResolveChainAsync(preferredLanguageCode).ConfigureAwait(false);
        await using var session = _store.QuerySession();
        var ct = CancellationToken.None;

        // One query: all candidate rows for this slug (M·2: per-page fallback).
        var rows = await session
            .Query<LocalizedPage>()
            .Where(p => p.Slug == slug && chain.Contains(p.LanguageCode))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var lang in chain)
        {
            var match = rows.FirstOrDefault(p => p.LanguageCode == lang);
            if (match != null)
                return match;
        }
        return null; // M·2: truly absent in every language → the Web renders a 404.
    }
}
