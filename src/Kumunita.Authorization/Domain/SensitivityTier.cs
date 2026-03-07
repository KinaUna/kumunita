namespace Kumunita.Authorization.Domain;

public enum SensitivityTier
{
    /// <summary>
    /// Public data — display names, avatars, public directory entries.
    /// No capability token required — freely accessible to authenticated members.
    /// </summary>
    Public = 0,

    /// <summary>
    /// Standard data — profile bios, visible group memberships.
    /// Session-scoped token — valid for the duration of the user's session.
    /// </summary>
    Standard = 1,

    /// <summary>
    /// Sensitive data — phone numbers, alternative emails, addresses.
    /// Per-request token — single use, 30 second TTL.
    /// </summary>
    Sensitive = 2,

    /// <summary>
    /// Restricted data — admin-only fields, suspension history.
    /// Per-request token — single use, 30 second TTL, admin role required.
    /// </summary>
    Restricted = 3
}