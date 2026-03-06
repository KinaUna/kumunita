namespace Kumunita.Localization.Domain
{
    public class Translation
    {
        public int Id { get; set; }
        public int TranslationKeyId { get; set; } = 0;
        public string LanguageCode { get; set; } = string.Empty; // e.g. "en", "fr", "es"
        public string Value { get; set; } = string.Empty; // the translated string value
        public TranslationStatus Status { get; set; } = TranslationStatus.Draft; // translation status
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
