namespace Kumunita.Shared.Kernel.ValueObjects;

public readonly record struct LanguageCode
{
    public string Value { get; }

    public LanguageCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Language code cannot be empty.", nameof(value));

        if (value.Length > 10)
            throw new ArgumentException("Language code is too long.", nameof(value));

        Value = value.Trim().ToLowerInvariant();
    }

    // Implicit conversion for ergonomic usage in LINQ and comparisons
    public static implicit operator string(LanguageCode code) => code.Value;
    public static implicit operator LanguageCode(string value) => new(value);

    public override string ToString() => Value;

    // Well-known codes as constants to avoid magic strings
    public static readonly LanguageCode English = new("en");
    public static readonly LanguageCode French = new("fr");
    public static readonly LanguageCode German = new("de");
    public static readonly LanguageCode Italian = new("it");
}
