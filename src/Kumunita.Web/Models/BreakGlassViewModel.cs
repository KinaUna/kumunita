namespace Kumunita.Web.Models;

public sealed class BreakGlassViewModel
{
    public bool HasOverride { get; init; }
    public bool Consumed { get; init; }
    public DateTimeOffset? GrantedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}

public sealed class BreakGlassConsumeViewModel
{
    public string Token { get; set; } = string.Empty;
}
