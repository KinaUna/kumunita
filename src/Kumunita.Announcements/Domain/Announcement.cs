using Kumunita.Announcements.Exceptions;
using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Announcements;
using Kumunita.Shared.Kernel.Domain;
using Kumunita.Shared.Kernel.ValueObjects;

namespace Kumunita.Announcements.Domain;

public class Announcement : IAuditableEntity, IUserOwned
{
    public AnnouncementId Id { get; private set; }

    // Who created it — member or admin/moderator
    public UserId OwnerId { get; private set; }

    // Multilingual content — uses LocalizedContent from Shared Kernel
    public LocalizedContent Title { get; private set; } = new();
    public LocalizedContent Body { get; private set; } = new();

    public AnnouncementStatus Status { get; private set; }
    public AnnouncementTarget Target { get; private set; } = AnnouncementTarget.All;

    // Whether this was created by a member (true) or admin/moderator (false)
    public bool RequiresModeration { get; private set; }

    // Moderation details
    public UserId? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? RejectionReason { get; private set; }

    // Publication details
    public DateTimeOffset? PublishedAt { get; private set; }

    // Optional expiry — announcement stops appearing after this date
    public DateTimeOffset? ExpiresAt { get; private set; }

    // Retraction details
    public UserId? RetractedBy { get; private set; }
    public DateTimeOffset? RetractedAt { get; private set; }
    public string? RetractionReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
        = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; }
        = DateTimeOffset.UtcNow;

    // Required by Marten
    private Announcement() { }

    /// <summary>
    /// Creates an announcement by an admin or moderator.
    /// Goes directly to Draft status — can be published immediately.
    /// </summary>
    public static Announcement CreateByStaff(
        UserId authorId,
        LocalizedContent title,
        LocalizedContent body,
        AnnouncementTarget target,
        DateTimeOffset? expiresAt)
    {
        return new Announcement
        {
            Id = new AnnouncementId(Guid.NewGuid()),
            OwnerId = authorId,
            Title = title,
            Body = body,
            Target = target,
            Status = AnnouncementStatus.Draft,
            RequiresModeration = false,
            ExpiresAt = expiresAt
        };
    }

    /// <summary>
    /// Creates an announcement submitted by a member.
    /// Goes directly to PendingReview status.
    /// </summary>
    public static Announcement SubmitByMember(
        UserId memberId,
        LocalizedContent title,
        LocalizedContent body,
        DateTimeOffset? expiresAt)
    {
        return new Announcement
        {
            Id = new AnnouncementId(Guid.NewGuid()),
            OwnerId = memberId,
            Title = title,
            Body = body,
            // Member submissions are always universal —
            // only staff can set targeting
            Target = AnnouncementTarget.All,
            Status = AnnouncementStatus.PendingReview,
            RequiresModeration = true,
            ExpiresAt = expiresAt
        };
    }

    public void Publish(UserId publishedBy)
    {
        if (Status != AnnouncementStatus.Draft
            && Status != AnnouncementStatus.PendingReview)
            throw new InvalidAnnouncementStatusException(
                Id, Status, "publish");

        Status = AnnouncementStatus.Published;
        PublishedAt = DateTimeOffset.UtcNow;
        ReviewedBy = publishedBy;
        ReviewedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Reject(UserId rejectedBy, string reason)
    {
        if (Status != AnnouncementStatus.PendingReview)
            throw new InvalidAnnouncementStatusException(
                Id, Status, "reject");

        Status = AnnouncementStatus.Rejected;
        ReviewedBy = rejectedBy;
        ReviewedAt = DateTimeOffset.UtcNow;
        RejectionReason = reason;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Retract(UserId retractedBy, string reason)
    {
        if (Status != AnnouncementStatus.Published)
            throw new InvalidAnnouncementStatusException(
                Id, Status, "retract");

        Status = AnnouncementStatus.Retracted;
        RetractedBy = retractedBy;
        RetractedAt = DateTimeOffset.UtcNow;
        RetractionReason = reason;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Update(
        LocalizedContent? title,
        LocalizedContent? body,
        AnnouncementTarget? target,
        DateTimeOffset? expiresAt)
    {
        if (Status != AnnouncementStatus.Draft)
            throw new InvalidAnnouncementStatusException(
                Id, Status, "update");

        if (title is not null) Title = title;
        if (body is not null) Body = body;
        if (target is not null) Target = target;
        if (expiresAt is not null) ExpiresAt = expiresAt;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Determines whether this announcement is currently visible —
    /// published, not expired, not retracted.
    /// </summary>
    public bool IsCurrentlyVisible =>
        Status == AnnouncementStatus.Published
        && (ExpiresAt is null || DateTimeOffset.UtcNow < ExpiresAt);
}
