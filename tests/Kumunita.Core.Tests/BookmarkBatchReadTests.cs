using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Bookmarks;
using Marten;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// ADR 0227 — <see cref="IBookmarkService.ListBookmarkedIdsAsync"/>, the batched
/// personal read behind the feed card footer's bookmark state. Own file:
/// <c>BookmarkServiceTests</c> pins its test names. The read touches only the
/// store, so the other dependencies are substituted.
/// </summary>
public class BookmarkBatchReadTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task ListBookmarkedIdsAsync_ReturnsOnlyOwnersRowsOfThatKindAmongTheRequestedIds()
    {
        var (store, bm) = await BootAsync();
        await Plant(store, Row("b1", "u-a", "post", "p1"));
        await Plant(store, Row("b2", "u-a", "post", "p2"));
        await Plant(store, Row("b3", "u-a", "event", "p3"));    // other kind
        await Plant(store, Row("b4", "u-b", "post", "p3"));     // other owner
        await Plant(store, Row("b5", "u-a", "post", "p-not-asked"));

        var ids = await bm.ListBookmarkedIdsAsync("u-a", "post", ["p1", "p2", "p3"]);

        Assert.Equal(new[] { "p1", "p2" }, ids.OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task ListBookmarkedIdsAsync_NoOwnerUnknownKindOrNoIds_ReturnsEmpty()
    {
        var (_, bm) = await BootAsync();
        Assert.Empty(await bm.ListBookmarkedIdsAsync("", "post", ["p1"]));
        Assert.Empty(await bm.ListBookmarkedIdsAsync("u-a", "not-a-kind", ["p1"]));
        Assert.Empty(await bm.ListBookmarkedIdsAsync("u-a", "post", []));
    }

    private static Bookmark Row(string id, string owner, string kind, string target) => new()
    {
        Id = id, OwnerId = owner, TargetKind = kind, TargetId = target, Created = DateTimeOffset.UtcNow,
    };

    private async Task<(IDocumentStore Store, BookmarkService Svc)> BootAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            CoreDocTypes.Configure(opts);
            BookmarkDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        var svc = new BookmarkService(
            Substitute.For<Kumunita.Core.UserInfo.IUserInfoService>(),
            Substitute.For<Kumunita.Core.Authorization.IAuthorizationService>(),
            store,
            Substitute.For<Kumunita.Core.Announcements.IAnnouncementService>(),
            Substitute.For<Kumunita.Core.Pages.IPageService>(),
            Substitute.For<Kumunita.Core.Events.IEventService>(),
            Substitute.For<Kumunita.Core.Projects.IProjectService>(),
            Substitute.For<Kumunita.Core.Inventory.IInventoryService>(),
            Substitute.For<Kumunita.Core.Identity.IIdentityService>());
        return (store, svc);
    }

    private static async Task Plant(IDocumentStore store, object document)
    {
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
