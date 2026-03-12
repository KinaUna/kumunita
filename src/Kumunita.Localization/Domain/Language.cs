namespace Kumunita.Localization.Domain;

/// <summary>
/// Represents a language supported by the application.
/// </summary>
public record Language
{
    public Language() { }

    public Language(string code, string name, string nativeName, bool IsDefault = false, bool IsActive = true)
    {
        Code = code;
        Name = name;
        NativeName = nativeName;
        this.IsDefault = IsDefault;
        this.IsActive = IsActive;
        CreatedAt = DateTime.UtcNow;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public string Code { get; set; } = null!; // e.g. "en", "fr", "es"
    public string Name { get; set; } = string.Empty; // e.g. "English", "French", "Spanish"
    public string NativeName { get; set; } = string.Empty; // e.g. "English", "Français", "Español"
    public bool IsDefault { get; set; } // whether this is the default language for the application
    public bool IsActive { get; set; } // whether this language is currently active and should be offered to users
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}