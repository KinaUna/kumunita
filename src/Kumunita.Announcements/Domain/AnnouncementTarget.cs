using Kumunita.Shared.Kernel;

namespace Kumunita.Announcements.Domain;

public class AnnouncementTarget
{
    /// <summary>
    /// Empty target means visible to all active members.
    /// </summary>
    public static readonly AnnouncementTarget All = new();

    // Role names this announcement is targeted at
    // Empty means all roles
    public IReadOnlyList<string> TargetRoles { get; private set; } = [];

    // Group IDs this announcement is targeted at
    // Empty means all groups
    public IReadOnlyList<GroupId> TargetGroupIds { get; private set; } = [];

    // Parameterless constructor required by Marten
    public AnnouncementTarget() { }

    public AnnouncementTarget(
        IEnumerable<string>? targetRoles,
        IEnumerable<GroupId>? targetGroupIds)
    {
        TargetRoles = targetRoles?.ToList() ?? [];
        TargetGroupIds = targetGroupIds?.ToList() ?? [];
    }

    /// <summary>
    /// Returns true if the announcement is visible to all members
    /// with no targeting restrictions.
    /// </summary>
    public bool IsUniversal =>
        TargetRoles.Count == 0 && TargetGroupIds.Count == 0;

    /// <summary>
    /// Determines whether a member with the given roles and groups
    /// is in the target audience for this announcement.
    /// </summary>
    public bool Includes(
        IEnumerable<string> memberRoles,
        IEnumerable<GroupId> memberGroupIds)
    {
        if (IsUniversal) return true;

        List<string> roles = memberRoles.ToList();
        List<GroupId> groups = memberGroupIds.ToList();

        bool roleMatch = TargetRoles.Count == 0
                         || TargetRoles.Any(r => roles.Contains(r));

        bool groupMatch = TargetGroupIds.Count == 0
                          || TargetGroupIds.Any(g => groups.Contains(g));

        // Must match both role and group criteria if both are specified
        return roleMatch && groupMatch;
    }
}