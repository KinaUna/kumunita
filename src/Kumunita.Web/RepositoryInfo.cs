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

    /// <summary>
    /// A repository link. <see cref="Key"/> is the <c>KnownTranslationKeys</c>
    /// key for the label (the views emit it through a dynamic <c>kw-l</c> so the
    /// label resolves per the resident's language); <see cref="Label"/> is the
    /// <c>en</c> reference text (and the M·1 floor if the provider is ever absent).
    /// </summary>
    public sealed record Link(string Key, string Label, string Url);

    public static IReadOnlyList<Link> Links { get; } = new List<Link>
    {
        new("repo.source_code", "Source code", BaseUrl),
        new("repo.documentation", "Documentation", $"{BaseUrl}/tree/{DefaultBranch}/docs"),
        new("repo.non_technical", "For non-technical residents", $"{BaseUrl}/blob/{DefaultBranch}/docs/philosophy/how-it-works.md"),
    };
}
