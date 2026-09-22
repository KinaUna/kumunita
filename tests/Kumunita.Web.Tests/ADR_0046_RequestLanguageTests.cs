using Kumunita.Core.Localization;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Http;
using Xunit;

/// <summary>
/// ADR 0046 — the default-language step: when a resident has not selected a
/// language, the page tries to match the browser's <c>Accept-Language</c>
/// settings (against the **enabled** catalog), and only if that is not
/// possible falls back to the platform default (the provider's frozen
/// chain — pinned on the Core side in
/// <c>LocalizationServiceTests.ADR0046_*</c>).
/// <para>
/// <see cref="RequestLanguage"/> is the Web-layer signal: the preference
/// cookie stays the **first** and only **write** path; the browser match is
/// a per-request suggestion (never persisted, never a claim, never
/// authorization-relevant). The parse is permissive (RFC 9110 grammar,
/// quality factors honored, invalid tags dropped) — a malformed header
/// degrades to "no browser signal", never a fault.
/// </para>
/// </summary>
public class ADR_0046_RequestLanguageTests
{
    private static LanguageCatalog[] Catalog(
        (string Id, bool Enabled)[] rows)
        => rows
            .Select((r, i) => new LanguageCatalog
            {
                Id = r.Id,
                NativeName = r.Id,
                Enabled = r.Enabled,
                SortOrder = i,
            })
            .ToArray();

    private static HttpRequest RequestWithAcceptLanguage(string? header)
    {
        var context = new DefaultHttpContext();
        if (header is not null)
            context.Request.Headers.AcceptLanguage = header;
        return context.Request;
    }

    private static HttpRequest RequestWithCookie(string cookie)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"kumunita.locale={cookie}";
        if (string.Equals(cookie, "", StringComparison.Ordinal))
            context.Request.Headers.Cookie = "kumunita.locale=";
        return context.Request;
    }

    // ── Browser match ───────────────────────────────────────────────────────

    [Fact]
    public void Browser_ExactTag_MatchesEnabledCatalog()
    {
        var request = RequestWithAcceptLanguage("pl, en;q=0.8");
        var match = RequestLanguage.Browser(request, Catalog(
            new[] { ("en", true), ("pl", true) }));
        Assert.Equal("pl", match);
    }

    [Fact]
    public void Browser_PriorityOrder_HonoredByQualityFactor()
    {
        // de is enabled, fr is the higher-priority tag → fr wins.
        var request = RequestWithAcceptLanguage("fr-CA,de;q=0.5,en;q=0.3");
        var match = RequestLanguage.Browser(request, Catalog(
            new[] { ("en", true), ("de", true), ("fr", true) }));
        Assert.Equal("fr", match);
    }

    [Fact]
    public void Browser_PriorityOrder_WhenAllEqualFirstTagWins()
    {
        var request = RequestWithAcceptLanguage("de,fr");
        var match = RequestLanguage.Browser(request, Catalog(
            new[] { ("en", true), ("de", true), ("fr", true) }));
        Assert.Equal("de", match);
    }

    [Fact]
    public void Browser_PartialTag_MatchesByPrimarySubtag()
    {
        // fr-CA is not a catalog row; the primary subtag fr is.
        var request = RequestWithAcceptLanguage("fr-CA");
        var match = RequestLanguage.Browser(request, Catalog(
            new[] { ("en", true), ("fr", true) }));
        Assert.Equal("fr", match);
    }

    [Fact]
    public void Browser_CaseInsensitive_Match()
    {
        var request = RequestWithAcceptLanguage("PL");
        var match = RequestLanguage.Browser(request, Catalog(
            new[] { ("en", true), ("pl", true) }));
        Assert.Equal("pl", match);
    }

    [Fact]
    public void Browser_MatchIsLimitedToEnabledLanguages()
    {
        // da is in the catalog but disabled (ADR 0045's shape) → skipped.
        var request = RequestWithAcceptLanguage("da,de");
        var match = RequestLanguage.Browser(request, Catalog(
            new[] { ("en", true), ("de", true), ("da", false) }));
        Assert.Equal("de", match);

        // All tags disabled / unmatched → null (the provider's default/en
        // chain then applies).
        var request2 = RequestWithAcceptLanguage("da");
        Assert.Null(RequestLanguage.Browser(request2, Catalog(
            new[] { ("en", true), ("da", false) })));
    }

    [Fact]
    public void Browser_NoHeaderOrNoMatch_ReturnsNull()
    {
        Assert.Null(RequestLanguage.Browser(
            RequestWithAcceptLanguage(null),
            Catalog(new[] { ("en", true) })));
        Assert.Null(RequestLanguage.Browser(
            RequestWithAcceptLanguage(""),
            Catalog(new[] { ("en", true) })));
        Assert.Null(RequestLanguage.Browser(
            RequestWithAcceptLanguage("xx,yy"),
            Catalog(new[] { ("en", true) })));
    }

    [Fact]
    public void Browser_MalformedHeader_DegradesToNoSignal()
    {
        // Invalid tags are dropped; valid ones still match.
        Assert.Equal("en", RequestLanguage.Browser(
            RequestWithAcceptLanguage("!!!,en;q=0.9"),
            Catalog(new[] { ("en", true) })));

        // All invalid → no signal, no throw.
        Assert.Null(RequestLanguage.Browser(
            RequestWithAcceptLanguage("!!!,;q=0.5"),
            Catalog(new[] { ("en", true) })));

        // q=0 is an explicit "not acceptable" → dropped.
        Assert.Null(RequestLanguage.Browser(
            RequestWithAcceptLanguage("en;q=0"),
            Catalog(new[] { ("en", true) })));
    }

    [Fact]
    public void Browser_DuplicateTag_KeepsHighestQuality()
    {
        // A repeated tag keeps its **highest** quality: here en first appears
        // at q=0.3 then q=0.9; the 0.9 must win over de's 0.5. (Keeping the
        // first 0.3 would let de win — so this pins the "highest wins" rule.)
        Assert.Equal("en", RequestLanguage.Browser(
            RequestWithAcceptLanguage("en;q=0.3,en;q=0.9,de;q=0.5"),
            Catalog(new[] { ("en", true), ("de", true) })));
    }

    [Fact]
    public void BrowserCandidates_ReturnsAllEnabledMatchesInPriorityOrder()
    {
        // The full ordered list (not just the first) — the ADR 0046 per-string
        // chain input. de and pl both match; fr-CA folds to fr; xx is dropped.
        var request = RequestWithAcceptLanguage("de,fr-CA,pl,xx");
        var candidates = RequestLanguage.BrowserCandidates(request, Catalog(
            new[] { ("en", true), ("de", true), ("fr", true), ("pl", true) }));
        Assert.Equal(new[] { "de", "fr", "pl" }, candidates.ToArray());
    }

    [Fact]
    public void BrowserCandidates_ExcludesDisabledAndUnmatched()
    {
        // da disabled → dropped; only de remains.
        Assert.Equal(new[] { "de" }, RequestLanguage.BrowserCandidates(
            RequestWithAcceptLanguage("da,de"),
            Catalog(new[] { ("en", true), ("de", true), ("da", false) }))
            .ToArray());

        // Nothing matches → empty list (the caller degrades to the frozen
        // instance-default → en chain).
        Assert.Empty(RequestLanguage.BrowserCandidates(
            RequestWithAcceptLanguage("xx,yy"),
            Catalog(new[] { ("en", true) })));
    }

    // ── Preference (the per-request effective signal) ──────────────────────

    [Fact]
    public void Preference_CookieWinsOverBrowser()
    {
        var request = RequestWithAcceptLanguage("de");
        request.Headers.Cookie = "kumunita.locale=pl";
        var catalog = Catalog(new[] { ("en", true), ("pl", true), ("de", true) });
        Assert.Equal("pl", RequestLanguage.Preference(request, catalog));
    }

    [Fact]
    public void Preference_NoCookie_FallsBackToBrowserMatch()
    {
        var request = RequestWithAcceptLanguage("de");
        var catalog = Catalog(new[] { ("en", true), ("de", true) });
        Assert.Equal("de", RequestLanguage.Preference(request, catalog));
    }

    [Fact]
    public void Preference_NoCookieNoBrowser_ReturnsNull_DefaultChainApplies()
    {
        var request = RequestWithAcceptLanguage("xx");
        var catalog = Catalog(new[] { ("en", true) });
        // null = "no signal" — the provider then resolves the instance
        // default → en (the frozen chain, M·1/M·9).
        Assert.Null(RequestLanguage.Preference(request, catalog));
    }

    [Fact]
    public void Preference_BlankCookie_IsNoPreference()
    {
        var request = RequestWithCookie("");
        request.Headers.AcceptLanguage = "de";
        var catalog = Catalog(new[] { ("en", true), ("de", true) });
        Assert.Equal("de", RequestLanguage.Preference(request, catalog));
    }
}
