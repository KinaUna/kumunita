using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M12 iCal composition pin (ADR 0112): the feed seam + the pure emitter
/// compose to exactly the visible upcoming set (C-M12·1). Shape follows
/// <see cref="EventServiceTests"/> verbatim — same <see cref="PostgresFixture"/>,
/// same <c>BootStoreAsync</c>, same <c>Plant</c> helper, same <c>Services</c>.
/// Pin: plant a visible community event + an audience-restricted event + a
/// draft + a deleted + a group-channel event; call
/// <see cref="IEventService.ListUpcomingAsync"/> with
/// <c>componentId = null</c>, page 0 (the <c>CalendarFeed</c> action's exact
/// call shape); call <see cref="IcsWriter.Build"/>; assert the ICS contains
/// exactly one <c>VEVENT</c> (the visible community event).
/// </summary>
public class IcsFeedTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task IcsFeed_ContainsExactlyTheVisibleUpcomingSet()
    {
        var ct = TestContext.Current.CancellationToken;
        using var store = await BootStoreAsync();
        var (userInfo, _, events) = Services(store);

        // Visible: community event, published, public, non-group-channel.
        await Plant(store, new Event
        {
            Id = "feed-visible",
            AuthorId = "author-1",
            Title = "Visible cleanup",
            Body = "Visible body",
            Start = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero),
            IsDraft = false,
            IsDeleted = false,
            Audience = null
        });

        // Audience-restricted to someone else: passes the candidate filter,
        // must be excluded by CanSeeAsync(Read).
        await Plant(store, new Event
        {
            Id = "feed-restricted",
            AuthorId = "author-2",
            Title = "Restricted",
            Body = "Restricted body",
            Start = new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero),
            IsDraft = false,
            IsDeleted = false,
            Audience = Audience(GrantKind.User, "someone-else")
        });

        // Draft: filtered by the candidate filter.
        await Plant(store, new Event
        {
            Id = "feed-draft",
            AuthorId = "author-3",
            Title = "Draft",
            Body = "Draft body",
            Start = new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero),
            IsDraft = true,
            IsDeleted = false,
            Audience = null
        });

        // Deleted: filtered by the candidate filter.
        await Plant(store, new Event
        {
            Id = "feed-deleted",
            AuthorId = "author-4",
            Title = "Deleted",
            Body = "Deleted body",
            Start = new DateTimeOffset(2026, 10, 4, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero),
            IsDraft = false,
            IsDeleted = true,
            Audience = null
        });

        // Group-channel: filtered by the candidate filter (GroupId non-empty).
        await Plant(store, new Event
        {
            Id = "feed-group",
            AuthorId = "author-5",
            Title = "Group event",
            Body = "Group body",
            GroupId = "g-m12-feed",
            Start = new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero),
            IsDraft = false,
            IsDeleted = false,
            Audience = null
        });

        // The feed seam, called exactly the way CalendarFeed calls it.
        var actorId = "feed-caller";
        var page = await events.ListUpcomingAsync(null, actorId, 0, ct);

        // Exactly one VEVENT: the visible community event.
        Assert.Single(page.Items);
        Assert.Equal("feed-visible", page.Items[0].Id);

        // Resolve CATEGORIES via the §field-map idiom (no-op here: no TagIds).
        var ics = IcsWriter.Build(page.Items, DateTimeOffset.UtcNow, categoriesByEventId: null);
        Assert.Contains("BEGIN:VEVENT", ics);
        Assert.Contains("kw-eve-feed-visible@kumunita", ics);
        Assert.DoesNotContain("kw-eve-feed-restricted@kumunita", ics);
        Assert.DoesNotContain("kw-eve-feed-draft@kumunita", ics);
        Assert.DoesNotContain("kw-eve-feed-deleted@kumunita", ics);
        Assert.DoesNotContain("kw-eve-feed-group@kumunita", ics);
        // Exactly one BEGIN:VEVENT — not two, not zero.
        Assert.Equal(1, ics.Split("BEGIN:VEVENT", StringSplitOptions.None).Length - 1);
    }

    // ── helpers (copied verbatim from EventServiceTests) ───────────────

    private async Task<IDocumentStore> BootStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
            M3DocTypes.Configure(opts);
            M4DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, TestContext.Current.CancellationToken);
        return store;
    }

    private static (UserInfoService User, AuthorizationService Authz, EventService Events) Services(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var events = new EventService(store, authz, userInfo);
        return (userInfo, authz, events);
    }

    private static Audience Audience(GrantKind kind, string id) => new(AudienceMode.Any, [new AudienceGrant(kind, id)]);

    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }
}
