namespace Kumunita.Announcements.Domain;

public enum AnnouncementStatus
{
    /// <summary>
    /// Created by admin/moderator but not yet published.
    /// Admin/moderator can edit freely.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Submitted by a member, awaiting moderator review.
    /// Cannot be edited while under review.
    /// </summary>
    PendingReview = 1,

    /// <summary>
    /// Approved and visible to targeted members.
    /// </summary>
    Published = 2,

    /// <summary>
    /// Rejected by moderator — visible only to submitter.
    /// </summary>
    Rejected = 3,

    /// <summary>
    /// Retracted after publication — no longer visible.
    /// </summary>
    Retracted = 4
}