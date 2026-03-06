namespace Kumunita.Localization.Domain;

public class LocalizationSettings
{
    public string DefaultLanguageCode { get; set; } = "en"; // default to English
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedAt { get; set;} = DateTime.UtcNow;
}