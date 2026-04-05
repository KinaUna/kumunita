using Kumunita.Announcements.Domain;
using Kumunita.Announcements.Exceptions;
using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Announcements;
using Kumunita.Shared.Kernel.ValueObjects;

namespace Kumunita.Tests.Announcements;

public class AnnouncementDomainTests
{
    private static readonly UserId AnyUser = new(Guid.NewGuid());
    private static readonly LocalizedContent AnyTitle = new("en", "Test title");
    private static readonly LocalizedContent AnyBody = new("en", "Test body");

    // ── CreateByStaff ──────────────────────────────────────────────────────────

    [Fact]
    public void CreateByStaff_ProducesDraftWithCorrectProperties()
    {
        Announcement ann = Announcement.CreateByStaff(AnyUser, AnyTitle, AnyBody,
            AnnouncementTarget.All, expiresAt: null);

        Assert.Equal(AnnouncementStatus.Draft, ann.Status);
        Assert.False(ann.RequiresModeration);
        Assert.Equal(AnyUser, ann.OwnerId);
    }

    // ── SubmitByMember ─────────────────────────────────────────────────────────

    [Fact]
    public void SubmitByMember_ProducesPendingReviewWithModerationFlag()
    {
        Announcement ann = Announcement.SubmitByMember(AnyUser, AnyTitle, AnyBody,
            expiresAt: null);

        Assert.Equal(AnnouncementStatus.PendingReview, ann.Status);
        Assert.True(ann.RequiresModeration);
    }

    // ── Publish ────────────────────────────────────────────────────────────────

    [Fact]
    public void Publish_FromDraft_TransitionsToPublished()
    {
        Announcement ann = Announcement.CreateByStaff(AnyUser, AnyTitle, AnyBody,
            AnnouncementTarget.All, null);

        ann.Publish(AnyUser);

        Assert.Equal(AnnouncementStatus.Published, ann.Status);
        Assert.NotNull(ann.PublishedAt);
    }

    [Fact]
    public void Publish_FromPendingReview_TransitionsToPublished()
    {
        Announcement ann = Announcement.SubmitByMember(AnyUser, AnyTitle, AnyBody, null);

        ann.Publish(AnyUser);

        Assert.Equal(AnnouncementStatus.Published, ann.Status);
    }

    [Fact]
    public void Publish_FromRetracted_Throws()
    {
        Announcement ann = Announcement.CreateByStaff(AnyUser, AnyTitle, AnyBody,
            AnnouncementTarget.All, null);
        ann.Publish(AnyUser);
        ann.Retract(AnyUser, "no longer relevant");

        Assert.Throws<InvalidAnnouncementStatusException>(() => ann.Publish(AnyUser));
    }

    // ── Reject ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Reject_FromPendingReview_TransitionsToRejected()
    {
        Announcement ann = Announcement.SubmitByMember(AnyUser, AnyTitle, AnyBody, null);

        ann.Reject(AnyUser, "inappropriate content");

        Assert.Equal(AnnouncementStatus.Rejected, ann.Status);
        Assert.Equal("inappropriate content", ann.RejectionReason);
    }

    [Fact]
    public void Reject_FromPublished_Throws()
    {
        Announcement ann = Announcement.CreateByStaff(AnyUser, AnyTitle, AnyBody,
            AnnouncementTarget.All, null);
        ann.Publish(AnyUser);

        Assert.Throws<InvalidAnnouncementStatusException>(() =>
            ann.Reject(AnyUser, "reason"));
    }

    // ── Retract ────────────────────────────────────────────────────────────────

    [Fact]
    public void Retract_FromPublished_TransitionsToRetracted()
    {
        Announcement ann = Announcement.CreateByStaff(AnyUser, AnyTitle, AnyBody,
            AnnouncementTarget.All, null);
        ann.Publish(AnyUser);

        ann.Retract(AnyUser, "outdated");

        Assert.Equal(AnnouncementStatus.Retracted, ann.Status);
        Assert.Equal("outdated", ann.RetractionReason);
    }

    [Fact]
    public void Retract_FromDraft_Throws()
    {
        Announcement ann = Announcement.CreateByStaff(AnyUser, AnyTitle, AnyBody,
            AnnouncementTarget.All, null);

        Assert.Throws<InvalidAnnouncementStatusException>(() =>
            ann.Retract(AnyUser, "reason"));
    }

    // ── Update ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Update_FromDraft_MutatesFields()
    {
        Announcement ann = Announcement.CreateByStaff(AnyUser, AnyTitle, AnyBody,
            AnnouncementTarget.All, null);
        LocalizedContent newTitle = new("en", "Updated title");

        AnnouncementTarget staffOnly = new(targetRoles: ["Manager"], targetGroupIds: null);
        ann.Update(newTitle, null, staffOnly, null);

        Assert.Equal("Updated title", ann.Title.Get("en"));
        Assert.False(ann.Target.IsUniversal);
    }

    [Fact]
    public void Update_FromPublished_Throws()
    {
        Announcement ann = Announcement.CreateByStaff(AnyUser, AnyTitle, AnyBody,
            AnnouncementTarget.All, null);
        ann.Publish(AnyUser);

        Assert.Throws<InvalidAnnouncementStatusException>(() =>
            ann.Update(AnyTitle, null, null, null));
    }

    // ── IsCurrentlyVisible ─────────────────────────────────────────────────────

    [Fact]
    public void IsCurrentlyVisible_WhenRetracted_ReturnsFalse()
    {
        Announcement ann = Announcement.CreateByStaff(AnyUser, AnyTitle, AnyBody,
            AnnouncementTarget.All, null);
        ann.Publish(AnyUser);
        ann.Retract(AnyUser, "done");

        Assert.False(ann.IsCurrentlyVisible);
    }

    [Fact]
    public void IsCurrentlyVisible_WhenExpired_ReturnsFalse()
    {
        Announcement ann = Announcement.CreateByStaff(AnyUser, AnyTitle, AnyBody,
            AnnouncementTarget.All, expiresAt: DateTimeOffset.UtcNow.AddSeconds(-1));
        ann.Publish(AnyUser);

        Assert.False(ann.IsCurrentlyVisible);
    }

    [Fact]
    public void IsCurrentlyVisible_WhenPublishedAndNotExpired_ReturnsTrue()
    {
        Announcement ann = Announcement.CreateByStaff(AnyUser, AnyTitle, AnyBody,
            AnnouncementTarget.All, expiresAt: DateTimeOffset.UtcNow.AddDays(1));
        ann.Publish(AnyUser);

        Assert.True(ann.IsCurrentlyVisible);
    }
}
