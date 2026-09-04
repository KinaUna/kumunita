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
}
