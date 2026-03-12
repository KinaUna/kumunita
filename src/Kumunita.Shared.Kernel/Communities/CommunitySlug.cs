namespace Kumunita.Shared.Kernel.Communities;

/// <summary>
/// URL-friendly community identifier used as the Marten tenant key.
/// Slugs are lowercase, hyphen-separated, e.g. "old-town-lausanne".
/// </summary>
public sealed record CommunitySlug
{
    public string Value { get; }

    public CommunitySlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Community slug cannot be empty.", nameof(value));

        string normalized = value.Trim().ToLowerInvariant();

        if (!IsValid(normalized))
            throw new ArgumentException(
                "Community slug must contain only lowercase letters, digits, and hyphens, " +
                "must start and end with a letter or digit, and be between 3 and 64 characters.",
                nameof(value));

        Value = normalized;
    }

    public static bool IsValid(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length is >= 3 and <= 64 &&
        System.Text.RegularExpressions.Regex.IsMatch(value, @"^[a-z0-9][a-z0-9\-]*[a-z0-9]$");

    public static implicit operator string(CommunitySlug slug) => slug.Value;
    public override string ToString() => Value;
}