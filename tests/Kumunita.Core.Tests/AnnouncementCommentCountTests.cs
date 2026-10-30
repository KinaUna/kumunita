using Kumunita.Core;
using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// ADR 0248 — <see cref="AnnouncementService.CountAnnouncementCommentsAsync"/>, the batched read
/// behind the announcement card footer's comment count. Its own file, like
/// <c>PostReplyCountTests</c>, so the big <c>AnnouncementServiceTests</c> stays untouched.
/// </summary>
public class AnnouncementCommentCountTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task CountAnnouncementCommentsAsync_EmptyInput_ReturnsEmpty()
    {
        var (_, svc) = await BootAsync();
        Assert.Empty(await svc.CountAnnouncementCommentsAsync([]));
    }

    [Fact]
    public async Task CountAnnouncementCommentsAsync_CountsPerAnnouncement_IncludingSoftDeleted_AndOmitsOthers()
    {
        var (store, svc) = await BootAsync();
        await Plant(store, Comment("c1", "a-1"));
        await Plant(store, Comment("c2", "a-1"));
        await Plant(store, new AnnouncementComment
        {
            Id = "c3", AnnouncementId = "a-1", AuthorId = "u1", Body = "gone", LanguageCode = "en",
            Created = DateTimeOffset.UtcNow, DeletedAt = DateTimeOffset.UtcNow,
        });
        await Plant(store, Comment("c4", "a-2"));
        await Plant(store, Comment("c5", "a-other"));

        var counts = await svc.CountAnnouncementCommentsAsync(["a-1", "a-2", "a-none"]);

        Assert.Equal(3, counts["a-1"]);   // soft-deleted still counts (it shows as a placeholder in the detail list)
        Assert.Equal(1, counts["a-2"]);
        Assert.False(counts.ContainsKey("a-none"));   // absent = 0
        Assert.False(counts.ContainsKey("a-other"));  // not requested
    }

    private static AnnouncementComment Comment(string id, string announcementId) => new()
    {
        Id = id, AnnouncementId = announcementId, AuthorId = "u1", Body = "hi", LanguageCode = "en",
        Created = DateTimeOffset.UtcNow,
    };

    private async Task<(IDocumentStore Store, AnnouncementService Svc)> BootAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            CoreDocTypes.Configure(opts);
            PostDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return (store, new AnnouncementService(store, new UserInfoService(store)));
    }

    private static async Task Plant(IDocumentStore store, object document)
    {
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
