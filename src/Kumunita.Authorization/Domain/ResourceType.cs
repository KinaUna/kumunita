namespace Kumunita.Authorization.Domain;

public static class ResourceType
{
    // ── UserProfile resources ─────────────────────────────────────────────
    public static readonly ResourceDescriptor ProfileDisplayName = new(
        "UserProfile.DisplayName", SensitivityTier.Public);

    public static readonly ResourceDescriptor ProfileBio = new(
        "UserProfile.Bio", SensitivityTier.Standard);

    public static readonly ResourceDescriptor ProfilePhoneNumber = new(
        "UserProfile.PhoneNumber", SensitivityTier.Sensitive);

    public static readonly ResourceDescriptor ProfileAlternativeEmail = new(
        "UserProfile.AlternativeEmail", SensitivityTier.Sensitive);

    public static readonly ResourceDescriptor ProfileAddress = new(
        "UserProfile.Address", SensitivityTier.Sensitive);

    public static readonly ResourceDescriptor ProfilePreferredLanguage = new(
        "UserProfile.PreferredLanguage", SensitivityTier.Standard);

    // ── DirectoryEntry resources ──────────────────────────────────────────
    public static readonly ResourceDescriptor DirectoryEntryPublic = new(
        "DirectoryEntry.Public", SensitivityTier.Public);

    public static readonly ResourceDescriptor DirectoryEntryLocation = new(
        "DirectoryEntry.Location", SensitivityTier.Standard);

    // ── Group resources ───────────────────────────────────────────────────
    public static readonly ResourceDescriptor GroupMembership = new(
        "Group.Membership", SensitivityTier.Standard);

    // ── Admin resources ───────────────────────────────────────────────────
    public static readonly ResourceDescriptor UserSuspensionHistory = new(
        "User.SuspensionHistory", SensitivityTier.Restricted);

    public static readonly ResourceDescriptor FullAuditLog = new(
        "AuditLog.Full", SensitivityTier.Restricted);

    // ── Future modules register their own resource types here ─────────────

    // Lookup by name — used during token validation
    private static readonly Dictionary<string, ResourceDescriptor> _all = new[]
    {
        ProfileDisplayName, ProfileBio, ProfilePhoneNumber,
        ProfileAlternativeEmail, ProfileAddress, ProfilePreferredLanguage,
        DirectoryEntryPublic, DirectoryEntryLocation,
        GroupMembership, UserSuspensionHistory, FullAuditLog
    }.ToDictionary(r => r.Name);

    public static ResourceDescriptor? Find(string name)
        => _all.TryGetValue(name, out var descriptor) ? descriptor : null;
}

public record ResourceDescriptor(string Name, SensitivityTier SensitivityTier);