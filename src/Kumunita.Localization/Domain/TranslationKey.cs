namespace Kumunita.Localization.Domain
{
    /// <summary>
    /// Represents a localization key used to identify and manage translatable strings within the application.
    /// </summary>
    /// <remarks>The TranslationKey class provides metadata for each translatable string, including its
    /// associated module, feature, and a description to assist translators. It also tracks creation and last update
    /// timestamps to facilitate translation management and auditing across different parts of the
    /// application.</remarks>
    public class TranslationKey
    {
        public int Id { get; set; }
        public string Module { get; set; } = string.Empty; // e.g. "UserManagement", "Dashboard"
        public string Feature { get; set; } = string.Empty; // e.g. "Login", "Profile", "Notifications"
        public string Key { get; set; } = string.Empty; // e.g. "UsernameLabel", "WelcomeMessage"
        public string Description { get; set; } = string.Empty; // optional description for translators
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
