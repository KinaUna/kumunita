namespace Kumunita.Identity.Infrastructure;

/// <summary>
/// Bound from the <c>Kumunita:InitialAdmin</c> configuration section.
/// Supply the password via User Secrets (dev) or an environment variable /
/// key vault (production) — never commit it to source control.
/// </summary>
public sealed class AdminSeedOptions
{
    public const string Section = "Kumunita:InitialAdmin";

    public string Email    { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;

    /// <summary>
    /// Must satisfy the password policy: ≥ 10 chars, at least one non-alphanumeric.
    /// </summary>
    public string Password { get; init; } = string.Empty;

    internal bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Email) &&
        !string.IsNullOrWhiteSpace(Password);
}