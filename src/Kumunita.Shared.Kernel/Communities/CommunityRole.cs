namespace Kumunita.Shared.Kernel.Communities;

/// <summary>
/// A user's role within a specific community.
/// Roles are community-local — a Manager in one community has no elevated
/// access in any other community.
/// </summary>
public enum CommunityRole
{
    /// <summary>
    /// Standard community participant. Can view content, submit announcements
    /// (if enabled), and manage their own profile and visibility settings.
    /// </summary>
    Member = 0,

    /// <summary>
    /// Can moderate announcements (approve/reject/retract) and invite new
    /// members at Member level. Has no elevated access to personal member data.
    /// </summary>
    Moderator = 1,

    /// <summary>
    /// Full community administration: invite members at any role level,
    /// change member roles (up to Moderator), suspend members, and manage
    /// community settings. Has no elevated access to personal member data —
    /// data access is still governed by capability tokens and visibility policies.
    /// </summary>
    Manager = 2
}