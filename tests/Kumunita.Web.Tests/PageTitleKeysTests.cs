using Kumunita.Core.Localization;
using Kumunita.Web.Security;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>ADR 0273 — the page-title map stays in step with the translation registry.</summary>
public class PageTitleKeysTests
{
    [Fact]
    public void EveryMappedKeyIsRegisteredAndItsEnglishValueIsTheMappedTitle()
    {
        foreach (var (english, key) in PageTitleKeys.All)
        {
            Assert.True(KnownTranslationKeys.EnValues.TryGetValue(key, out var value), $"{key} is not registered");
            Assert.Equal(english, value);
        }
    }

    [Fact]
    public void TryGetKey_UnknownOrNullTitle_PassesThrough()
    {
        Assert.False(PageTitleKeys.TryGetKey("Alice's garden club", out _));
        Assert.False(PageTitleKeys.TryGetKey(null, out _));
        Assert.True(PageTitleKeys.TryGetKey("Admin — Accounts", out var key));
        Assert.Equal("title.admin_accounts", key);
    }

    [Fact]
    public void Keys_AreUnique()
    {
        Assert.Equal(PageTitleKeys.All.Count, PageTitleKeys.All.Values.Distinct().Count());
    }
}
