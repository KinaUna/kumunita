using Kumunita.Announcements.Domain;
using Kumunita.Announcements.Exceptions;
using Kumunita.Announcements.Features.Authoring;
using Kumunita.Announcements.Features.Moderation;
using Kumunita.Announcements.Infrastructure;
using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Announcements;
using Kumunita.Shared.Kernel.ValueObjects;
using Kumunita.Tests.Infrastructure;
using Marten;
using Marten.Services;

namespace Kumunita.Tests.Announcements;

[Collection("Integration")]
public class AnnouncementHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static IDocumentStore? SharedStore;

    private IDocumentStore _store = null!;

    // Each test instance gets a unique tenant so concurrent tests never share data
    private readonly string _tenantId = $"t{Guid.NewGuid():N}"[..12];
    private readonly UserId _staffId = new(Guid.NewGuid());

    public async ValueTask InitializeAsync()
    {
        await SchemaLock.WaitAsync();
        try
        {
            if (SharedStore is null)
            {
                SharedStore = DocumentStore.For(opts =>
                {
                    opts.Connection(db.ConnectionString);
                    opts.UseSystemTextJsonForSerialization(configure: PrivateSetterTypeInfoResolver.Configure);
                    opts.Policies.AllDocumentsAreMultiTenanted();
                    opts.AddAnnouncementsMartenSchema();
                    opts.RegisterValueType(typeof(UserId));
                    opts.RegisterValueType(typeof(AnnouncementId));
                });
                await SharedStore.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
            }
        }
        finally
        {
            SchemaLock.Release();
        }
        _store = SharedStore;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask; // store is shared; disposed by GC / test run teardown

    // ── SubmitAnnouncement ─────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitAnnouncement_WhenEnabled_StoresPendingReview()
    {
        await EnableMemberSubmissions();

        UserId memberId = new(Guid.NewGuid());
        SubmitAnnouncement cmd = new(
            memberId,
            new LocalizedContent("en", "Community event"),
            new LocalizedContent("en", "Join us this weekend"),
            ExpiresAt: null);

        await using IDocumentSession session = TenantSession();
        (AnnouncementId id, _) = await SubmitAnnouncementHandler.Handle(cmd, session, default);
        await session.SaveChangesAsync();

        await using IQuerySession readSession = TenantQuerySession();
        Announcement? saved = await readSession.LoadAsync<Announcement>(id);

        Assert.NotNull(saved);
        Assert.Equal(AnnouncementStatus.PendingReview, saved.Status);
        Assert.Equal(memberId, saved.OwnerId);
        Assert.True(saved.RequiresModeration);
    }

    [Fact]
    public async Task SubmitAnnouncement_WhenDisabled_ThrowsMemberSubmissionsDisabled()
    {
        // Default settings have MemberSubmissionsEnabled = false
        SubmitAnnouncement cmd = new(
            new UserId(Guid.NewGuid()),
            new LocalizedContent("en", "Test"),
            new LocalizedContent("en", "Body"),
            ExpiresAt: null);

        await using IDocumentSession session = TenantSession();

        await Assert.ThrowsAsync<MemberSubmissionsDisabledException>(
            () => SubmitAnnouncementHandler.Handle(cmd, session, default));
    }

    [Fact]
    public async Task SubmitAnnouncement_AtLimit_ThrowsPendingSubmissionLimitExceeded()
    {
        await EnableMemberSubmissions(maxPending: 1);

        UserId memberId = new(Guid.NewGuid());

        // First submission — should succeed
        SubmitAnnouncement cmd = new(memberId,
            new LocalizedContent("en", "First"),
            new LocalizedContent("en", "Body"), null);

        await using (IDocumentSession s1 = TenantSession())
        {
            await SubmitAnnouncementHandler.Handle(cmd, s1, default);
            await s1.SaveChangesAsync();
        }

        // Second submission — should hit the limit
        await using IDocumentSession s2 = TenantSession();

        await Assert.ThrowsAsync<PendingSubmissionLimitExceededException>(
            () => SubmitAnnouncementHandler.Handle(cmd, s2, default));
    }

    // ── CreateAnnouncement ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAnnouncement_WithPublishImmediately_StoresPublished()
    {
        CreateAnnouncement cmd = new(
            _staffId,
            new LocalizedContent("en", "Staff notice"),
            new LocalizedContent("en", "Important update"),
            AnnouncementTarget.All,
            ExpiresAt: null,
            PublishImmediately: true);

        await using IDocumentSession session = TenantSession();
        (AnnouncementId id, _) = await CreateAnnouncementHandler.Handle(cmd, session, default);
        await session.SaveChangesAsync();

        await using IQuerySession readSession = TenantQuerySession();
        Announcement? saved = await readSession.LoadAsync<Announcement>(id);

        Assert.NotNull(saved);
        Assert.Equal(AnnouncementStatus.Published, saved.Status);
        Assert.NotNull(saved.PublishedAt);
    }

    [Fact]
    public async Task CreateAnnouncement_AsDraft_StoresDraft()
    {
        CreateAnnouncement cmd = new(
            _staffId,
            new LocalizedContent("en", "Draft notice"),
            new LocalizedContent("en", "Coming soon"),
            AnnouncementTarget.All,
            ExpiresAt: null,
            PublishImmediately: false);

        await using IDocumentSession session = TenantSession();
        (AnnouncementId id, _) = await CreateAnnouncementHandler.Handle(cmd, session, default);
        await session.SaveChangesAsync();

        await using IQuerySession readSession = TenantQuerySession();
        Announcement? saved = await readSession.LoadAsync<Announcement>(id);

        Assert.NotNull(saved);
        Assert.Equal(AnnouncementStatus.Draft, saved.Status);
        Assert.Null(saved.PublishedAt);
    }

    // ── ApproveAnnouncement ────────────────────────────────────────────────────

    [Fact]
    public async Task ApproveAnnouncement_TransitionsToPublished()
    {
        await EnableMemberSubmissions();
        AnnouncementId announcementId = await SubmitOneAnnouncement();

        ApproveAnnouncement cmd = new(announcementId, _staffId);

        await using IDocumentSession session = TenantSession();
        await ApproveAnnouncementHandler.Handle(cmd, session, default);
        await session.SaveChangesAsync();

        await using IQuerySession readSession = TenantQuerySession();
        Announcement? saved = await readSession.LoadAsync<Announcement>(announcementId);

        Assert.NotNull(saved);
        Assert.Equal(AnnouncementStatus.Published, saved.Status);
        Assert.NotNull(saved.ReviewedBy);
        Assert.Equal(_staffId, saved.ReviewedBy);
    }

    [Fact]
    public async Task ApproveAnnouncement_UnknownId_ThrowsAnnouncementNotFound()
    {
        ApproveAnnouncement cmd = new(new AnnouncementId(Guid.NewGuid()), _staffId);

        await using IDocumentSession session = TenantSession();

        await Assert.ThrowsAsync<AnnouncementNotFoundException>(
            () => ApproveAnnouncementHandler.Handle(cmd, session, default));
    }

    // ── RejectAnnouncement ─────────────────────────────────────────────────────

    [Fact]
    public async Task RejectAnnouncement_TransitionsToRejected()
    {
        await EnableMemberSubmissions();
        AnnouncementId announcementId = await SubmitOneAnnouncement();

        RejectAnnouncement cmd = new(announcementId, _staffId, "Off topic");

        await using IDocumentSession session = TenantSession();
        await RejectAnnouncementHandler.Handle(cmd, session, default);
        await session.SaveChangesAsync();

        await using IQuerySession readSession = TenantQuerySession();
        Announcement? saved = await readSession.LoadAsync<Announcement>(announcementId);

        Assert.NotNull(saved);
        Assert.Equal(AnnouncementStatus.Rejected, saved.Status);
        Assert.Equal("Off topic", saved.RejectionReason);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private IDocumentSession TenantSession()
        => _store.LightweightSession(new SessionOptions { TenantId = _tenantId });

    private IQuerySession TenantQuerySession()
        => _store.QuerySession(new SessionOptions { TenantId = _tenantId });

    private async Task EnableMemberSubmissions(int maxPending = 10)
    {
        AnnouncementSettings settings = AnnouncementSettings.CreateDefaults();
        settings.Update(
            memberSubmissionsEnabled: true,
            maxPendingSubmissionsPerMember: maxPending,
            updatedBy: _staffId);

        // Use the same tenant session as the handler so Marten's
        // identity map can find the settings regardless of tenancy style
        await using IDocumentSession session = TenantSession();
        session.Store(settings);
        await session.SaveChangesAsync();
    }

    private async Task<AnnouncementId> SubmitOneAnnouncement()
    {
        SubmitAnnouncement cmd = new(
            new UserId(Guid.NewGuid()),
            new LocalizedContent("en", "Event"),
            new LocalizedContent("en", "Details"),
            ExpiresAt: null);

        await using IDocumentSession session = TenantSession();
        (AnnouncementId id, _) = await SubmitAnnouncementHandler.Handle(cmd, session, default);
        await session.SaveChangesAsync();
        return id;
    }
}
