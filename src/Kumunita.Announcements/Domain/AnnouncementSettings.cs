using Kumunita.Shared.Kernel;

namespace Kumunita.Announcements.Domain;

public class AnnouncementSettings
{
    // Singleton document — only one instance ever exists
    public static readonly Guid SingletonId = Guid.Parse(
        "00000000-0000-0000-0000-000000000001");

    public Guid Id { get; private set; } = SingletonId;

    /// <summary>
    /// Master toggle — when false, only admins and moderators
    /// can create announcements. Member submissions are disabled.
    /// </summary>
    public bool MemberSubmissionsEnabled { get; private set; } = false;

    /// <summary>
    /// Maximum number of pending submissions a single member
    /// can have in the queue at one time.
    /// Prevents queue flooding.
    /// </summary>
    public int MaxPendingSubmissionsPerMember { get; private set; } = 3;

    public DateTimeOffset UpdatedAt { get; private set; }
        = DateTimeOffset.UtcNow;
    public UserId? LastUpdatedBy { get; private set; }

    // Needs to be public, for JsonSerializer and Marten — but we don't want it called directly from outside the class, so we'll make it parameterless and use a factory method for creation
    public AnnouncementSettings() { }

    public static AnnouncementSettings CreateDefaults()
        => new();

    public void Update(
        bool? memberSubmissionsEnabled,
        int? maxPendingSubmissionsPerMember,
        UserId updatedBy)
    {
        if (memberSubmissionsEnabled is not null)
            MemberSubmissionsEnabled = memberSubmissionsEnabled.Value;
        if (maxPendingSubmissionsPerMember is not null)
            MaxPendingSubmissionsPerMember = maxPendingSubmissionsPerMember.Value;
        LastUpdatedBy = updatedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}