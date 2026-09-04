namespace Kumunita.Web;

/// <summary>
/// Where residents and collaborators can find this project's code and
/// documentation. A single shared repository (one codebase for every
/// neighborhood deployment), so unlike <see cref="Kumunita.Core.CommunityOptions"/>
/// (per-instance config) these links are the same everywhere — static in code,
/// kept in sync with the git remote, not overridden per deployment.
/// </summary>
public static class RepositoryInfo
{
    public const string BaseUrl = "https://github.com/KinaUna/kumunita";
    public const string DefaultBranch = "main";

    public sealed record Link(string Label, string Url);

    public static IReadOnlyList<Link> Links { get; } = new List<Link>
    {
        new("Source code", BaseUrl),
        new("Documentation", $"{BaseUrl}/tree/{DefaultBranch}/docs"),
        new("For non-technical residents", $"{BaseUrl}/blob/{DefaultBranch}/docs/philosophy/how-it-works.md"),
    };
}
