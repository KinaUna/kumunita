using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests.UserInfo;

/// <summary>
/// Lane tests for <see cref="UserInfoService.SetProfileHomeShowUpcomingAsync"/>
/// — the single write lane on the UserInfo side for the resident's home
/// "upcoming" display preference (the Profile.HomeShowUpcoming additive
/// field), pinned at the service layer (owner scope is the Web boundary's
/// job; the lane writes Profile.HomeShowUpcoming only). Mirrors
/// <see cref="ProfileTimezoneLaneTests"/> exactly.
/// </summary>
public sealed class ProfileHomeShowUpcomingLaneTests(PostgresFixture fixture)
    : IClassFixture<PostgresFixture>
{
    // ── 1 — default: a fresh profile has the flag OFF (the floor) ─────────
    [Fact]
    public async Task FreshProfile_HomeShowUpcoming_Defaults_False()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string subject = "u-hsu-default";
        await SeedProfileAsync(svc, subject);

        var read = await svc.GetProfileAsync(subject);
        Assert.NotNull(read);
        Assert.False(read!.HomeShowUpcoming); // the floor: hide the section
    }

    // ── 2 — set true: the flag is live on the very next read ─────────────
    [Fact]
    public async Task SetProfileHomeShowUpcoming_True_LiveOnNextRead()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string subject = "u-hsu-true";
        await SeedProfileAsync(svc, subject);
        Assert.False((await svc.GetProfileAsync(subject))!.HomeShowUpcoming);

        await svc.SetProfileHomeShowUpcomingAsync(subject, true, subject);

        var read = await svc.GetProfileAsync(subject);
        Assert.True(read!.HomeShowUpcoming);

        // The write touched no other field of the profile.
        Assert.Equal("Resident " + subject, read.DisplayName);
    }

    // ── 3 — set false over a true value: the clear persists ───────────────
    [Fact]
    public async Task SetProfileHomeShowUpcoming_False_ClearsAndPersists()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string subject = "u-hsu-clear";
        await SeedProfileAsync(svc, subject);

        await svc.SetProfileHomeShowUpcomingAsync(subject, true, subject);
        Assert.True((await svc.GetProfileAsync(subject))!.HomeShowUpcoming);

        await svc.SetProfileHomeShowUpcomingAsync(subject, false, subject);
        Assert.False((await svc.GetProfileAsync(subject))!.HomeShowUpcoming);

        // Re-settable (the lane is a plain field write, not a tombstone).
        await svc.SetProfileHomeShowUpcomingAsync(subject, true, subject);
        Assert.True((await svc.GetProfileAsync(subject))!.HomeShowUpcoming);
    }

    // ── 4 — missing profile fails closed (throw, never create) ───────────
    [Fact]
    public async Task SetProfileHomeShowUpcoming_MissingProfile_ThrowsKeyNotFound()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.SetProfileHomeShowUpcomingAsync("u-hsu-missing", true, "u-hsu-any"));

        // Fail closed = no profile row created.
        Assert.Null(await svc.GetProfileAsync("u-hsu-missing"));
    }

    // ── 5 — scope: a fresh profile defaults to the community-wide floor ──
    [Fact]
    public async Task FreshProfile_HomeUpcomingMineOnly_Defaults_False()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string subject = "u-huo-default";
        await SeedProfileAsync(svc, subject);

        var read = await svc.GetProfileAsync(subject);
        Assert.NotNull(read);
        Assert.False(read!.HomeUpcomingMineOnly); // the floor: community-wide
    }

    // ── 6 — set true: the scope flag is live on the very next read ───────
    [Fact]
    public async Task SetProfileHomeUpcomingMineOnly_True_LiveOnNextRead()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string subject = "u-huo-true";
        await SeedProfileAsync(svc, subject);
        Assert.False((await svc.GetProfileAsync(subject))!.HomeUpcomingMineOnly);

        await svc.SetProfileHomeUpcomingMineOnlyAsync(subject, true, subject);

        var read = await svc.GetProfileAsync(subject);
        Assert.True(read!.HomeUpcomingMineOnly);

        // The write touched no other field of the profile.
        Assert.Equal("Resident " + subject, read.DisplayName);
        Assert.False(read.HomeShowUpcoming); // the sibling flag is untouched
    }

    // ── 7 — set false over a true value: the clear persists ───────────────
    [Fact]
    public async Task SetProfileHomeUpcomingMineOnly_False_ClearsAndPersists()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string subject = "u-huo-clear";
        await SeedProfileAsync(svc, subject);

        await svc.SetProfileHomeUpcomingMineOnlyAsync(subject, true, subject);
        Assert.True((await svc.GetProfileAsync(subject))!.HomeUpcomingMineOnly);

        await svc.SetProfileHomeUpcomingMineOnlyAsync(subject, false, subject);
        Assert.False((await svc.GetProfileAsync(subject))!.HomeUpcomingMineOnly);

        // Re-settable (the lane is a plain field write, not a tombstone).
        await svc.SetProfileHomeUpcomingMineOnlyAsync(subject, true, subject);
        Assert.True((await svc.GetProfileAsync(subject))!.HomeUpcomingMineOnly);
    }

    // ── 8 — missing profile fails closed (throw, never create) ───────────
    [Fact]
    public async Task SetProfileHomeUpcomingMineOnly_MissingProfile_ThrowsKeyNotFound()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.SetProfileHomeUpcomingMineOnlyAsync("u-huo-missing", true, "u-huo-any"));

        // Fail closed = no profile row created.
        Assert.Null(await svc.GetProfileAsync("u-huo-missing"));
    }

    // ── Shared helpers ────────────────────────────────────────────────────

    private static async Task SeedProfileAsync(IUserInfoService svc, string subject)
    {
        var profile = new Profile
        {
            SubjectId = subject,
            DisplayName = "Resident " + subject,
            Verified = true,
        };
        await svc.UpsertProfileAsync(profile, new ProfileUpdate(null, null, null, null, null));
    }

    private async Task<IDocumentStore> BootStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            CoreDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }
}
