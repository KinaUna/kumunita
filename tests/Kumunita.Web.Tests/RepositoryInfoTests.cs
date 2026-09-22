using Kumunita.Web;

namespace Kumunita.Web.Tests;

/// <summary>
/// Pins the public GitHub links on the home page so a typo'd branch name
/// (e.g. someone renames the default branch and these links stop resolving)
/// or a dropped/renamed entry is caught at build time instead of silently
/// 404-ing for visitors.
/// </summary>
public class RepositoryInfoTests
{
    private static readonly string Base = RepositoryInfo.BaseUrl;
    private static readonly IEnumerable<string> Urls = RepositoryInfo.Links.Select(l => l.Url);

    [Fact]
    public void All_Links_Point_At_The_Kumunita_Repository()
    {
        Assert.All(Urls, url =>
        {
            Assert.StartsWith(Base, url);
            Assert.DoesNotContain("//localhost", url);
            Assert.False(url.EndsWith('/'), $"{url} should not have a trailing slash");
        });
    }

    [Fact]
    public void Includes_SourcesCode_And_Docs_Links()
    {
        var labels = RepositoryInfo.Links.Select(l => l.Label).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Source code", labels);
        Assert.Contains("Documentation", labels);
    }

    [Fact]
    public void Documentation_Link_Is_Pinned_To_DefaultBranch()
    {
        var docs = RepositoryInfo.Links.Single(l =>
            string.Equals(l.Label, "Documentation", StringComparison.OrdinalIgnoreCase));
        Assert.Equal($"{Base}/tree/{RepositoryInfo.DefaultBranch}/docs", docs.Url);
    }

    [Fact]
    public void No_Link_Has_Blank_Label_Or_Url()
    {
        Assert.All(RepositoryInfo.Links, l =>
        {
            Assert.False(string.IsNullOrWhiteSpace(l.Label));
            Assert.False(string.IsNullOrWhiteSpace(l.Url));
        });
    }

    /// <summary>
    /// Each link carries a <see cref="RepositoryInfo.Link.Key"/> that is a
    /// registered <c>KnownTranslationKeys</c> key with a non-empty <c>en</c>
    /// value — the counterpart of the KwLRegistryConsistency static-literal scan
    /// for the dynamic <c>key="@link.Key"</c> path the home / about / footer
    /// views emit (a text scan can't check a Razor expression). Pins that the
    /// labels actually resolve per-language rather than degrading to the raw key.
    /// </summary>
    [Fact]
    public void Repository_Link_Keys_Are_Registered_With_NonEmpty_En_Value()
    {
        foreach (var link in RepositoryInfo.Links)
        {
            Assert.False(string.IsNullOrWhiteSpace(link.Key),
                $"link '{link.Url}' has no translation key");
            Assert.True(Kumunita.Core.Localization.KnownTranslationKeys.EnValues.ContainsKey(link.Key),
                $"link key '{link.Key}' is not in KnownTranslationKeys");
            Assert.False(
                string.IsNullOrWhiteSpace(Kumunita.Core.Localization.KnownTranslationKeys.EnValues[link.Key]),
                $"link key '{link.Key}' has an empty en value");
            // The en registry text is the label's reference floor — keep it in
            // step with the label so a fresh en instance renders identically.
            Assert.Equal(link.Label, Kumunita.Core.Localization.KnownTranslationKeys.EnValues[link.Key]);
        }
    }
}
