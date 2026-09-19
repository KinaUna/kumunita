using Kumunita.Core;
using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// Shared direct-construction harness for <see cref="StaticPagesController"/>
/// tests (the controller's constructor grew in the ADR 0043 D7 localized-body
/// lane: it now also takes an <see cref="ITranslationProvider"/> — the
/// effective-language read seam — an <see cref="ILocalizationService"/> —
/// the enabled-catalog read the <c>Accept-Language</c> match is validated
/// against — and an <see cref="IHttpContextAccessor"/> — the cookie /
/// <c>Accept-Language</c> signal reader). A test supplies its own
/// <see cref="IPageService"/> mock (the <see cref="IPageService
/// .ResolvePageAsync"/> seam, the ADR 0043 D7 read) and gets back a fully
/// wired controller over a <see cref="DefaultHttpContext"/>.
/// <para>
/// The harness's <see cref="ITranslationProvider"/> stub resolves the
/// effective language to <c>en</c> (the floor) by default — the
/// <c>cookie-less</c> path: no <c>kumunita.locale</c> cookie on the
/// <see cref="DefaultHttpContext"/>, so the controller's
/// <c>else if (request is not null)</c> branch runs, the
/// <c>Accept-Language</c> header is absent (empty candidate list), and the
/// provider resolves the frozen instance-default → <c>en</c> floor. A test
/// that wants a different effective language (to pin the
/// <see cref="PageTranslation"/> pick) passes it through
/// <paramref name="effectiveLanguage"/>.
/// </para>
/// </summary>
internal static class StaticPagesControllerHarness
{
    /// <summary>
    /// Builds a <see cref="StaticPagesController"/> over a
    /// <see cref="DefaultHttpContext"/> with the given <paramref name="pages"/>
    /// mock, the translation/localization/http seams stubbed to the
    /// cookie-less <c>en</c> floor, and the given
    /// <paramref name="community"/> (a sensible default when null).
    /// </summary>
    public static StaticPagesController Build(
        IPageService pages,
        CommunityOptions? community = null,
        string effectiveLanguage = "en")
    {
        var translations = Substitute.For<ITranslationProvider>();
        // The two effective-language overloads (the string? preferred-code
        // and the IReadOnlyCollection<string>? candidates) both resolve to
        // the requested effective language — the controller picks whichever
        // the cookie / Accept-Language branch selects.
        translations.ResolveEffectiveLanguageAsync(Arg.Any<string?>())
            .Returns(Task.FromResult(effectiveLanguage));
        translations.ResolveEffectiveLanguageAsync(Arg.Any<IReadOnlyCollection<string>?>())
            .Returns(Task.FromResult(effectiveLanguage));

        var localization = Substitute.For<ILocalizationService>();
        localization.ListLanguagesAsync().Returns<IReadOnlyList<LanguageCatalog>>(
        [
            new LanguageCatalog { Id = "en", NativeName = "English",  Enabled = true, SortOrder = 0 },
            new LanguageCatalog { Id = "de", NativeName = "Deutsch",  Enabled = true, SortOrder = 1 },
            new LanguageCatalog { Id = "fr", NativeName = "Français", Enabled = true, SortOrder = 2 },
            new LanguageCatalog { Id = "da", NativeName = "Dansk",    Enabled = true, SortOrder = 3 },
        ]);

        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContextAccessor.HttpContext.Returns(httpContext);

        var controller = new StaticPagesController(
            pages,
            translations,
            localization,
            httpContextAccessor,
            Options.Create(community ?? new CommunityOptions
            {
                Name = "Maplewood",
                SupportEmail = "maps@example.com",
            }));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }
}
