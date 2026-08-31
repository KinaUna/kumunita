namespace Kumunita.Core;

/// <summary>
/// Per-instance community identity. Same image everywhere, different config
/// (ADR 0002); in production the values come from <c>Community__*</c>
/// environment variables (OPS.md).
/// </summary>
public sealed class CommunityOptions
{
    public const string SectionName = "Community";

    /// <summary>Display name shown to residents (e.g. "Maplewood Residents").</summary>
    public string Name { get; set; } = "Kumunita";

    /// <summary>Contact shown in the footer / reports.</summary>
    public string? SupportEmail { get; set; }
}
