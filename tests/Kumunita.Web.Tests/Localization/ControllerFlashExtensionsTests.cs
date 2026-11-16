using Kumunita.Core.Localization;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>ADR 0261 — the shared <c>FlashAsync</c> extension: English floor without services, provider text with them.</summary>
public class ControllerFlashExtensionsTests
{
    private sealed class Probe : Controller { }

    [Fact]
    public async Task NoRequestServices_ReturnsEnglishFloor()
    {
        var c = new Probe { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        Assert.Equal("Event updated.", await c.FlashAsync("events.flash_updated", "Event updated."));
    }

    [Fact]
    public async Task NoHttpContext_FormatsPlaceholdersOnTheFloor()
    {
        var c = new Probe();
        Assert.Equal("Translation added (Dansk).", await c.FlashAsync("flash.translation_added", "Translation added ({0}).", "Dansk"));
    }

    [Fact]
    public async Task WithProvider_UsesResolvedTemplateAndFormats()
    {
        var provider = Substitute.For<ITranslationProvider>();
        provider.ResolveEffectiveLanguageAsync(Arg.Any<string?>()).Returns("de");
        provider.GetAsync("flash.translation_added", Arg.Any<string?>()).Returns("Übersetzung hinzugefügt ({0}).");
        var localization = Substitute.For<ILocalizationService>();
        var services = new ServiceCollection().AddSingleton(provider).AddSingleton(localization).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        var c = new Probe { ControllerContext = new ControllerContext { HttpContext = http } };

        Assert.Equal("Übersetzung hinzugefügt (Dansk).",
            await c.FlashAsync("flash.translation_added", "Translation added ({0}).", "Dansk"));
    }

    [Fact]
    public async Task ProviderReturningTheKey_FallsBackToEnglish()
    {
        var provider = Substitute.For<ITranslationProvider>();
        provider.ResolveEffectiveLanguageAsync(Arg.Any<string?>()).Returns("en");
        provider.GetAsync("events.flash_deleted", Arg.Any<string?>()).Returns("events.flash_deleted");
        var localization = Substitute.For<ILocalizationService>();
        var services = new ServiceCollection().AddSingleton(provider).AddSingleton(localization).BuildServiceProvider();
        var c = new Probe { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = services } } };

        Assert.Equal("Event deleted.", await c.FlashAsync("events.flash_deleted", "Event deleted."));
    }
}
