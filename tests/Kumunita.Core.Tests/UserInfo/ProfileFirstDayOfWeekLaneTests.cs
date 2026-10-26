using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests.UserInfo;

/// <summary>
/// Lane tests for <see cref="UserInfoService.SetProfileFirstDayOfWeekAsync"/>
/// (ADR 0169) — the single write lane on the UserInfo side for the resident's
/// first-day-of-week preference (the Profile.FirstDayOfWeek additive field),
/// pinned at the service layer (owner scope is the Web boundary's job; the
/// lane writes Profile.FirstDayOfWeek only). Mirrors
/// <see cref="ProfileHomeShowUpcomingLaneTests"/> exactly (the fresh-scratch
/// Postgres-per-test harness idiom).
/// </summary>
public sealed class ProfileFirstDayOfWeekLaneTests(PostgresFixture fixture)
    : IClassFixture<PostgresFixture>
{
    // ── 1 — default: a fresh profile has no preference (null = unset) ─────
    [Fact]
    public async Task FreshProfile_FirstDayOfWeek_Defaults_Null()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string subject = "u-fdw-default";
        await SeedProfileAsync(svc, subject);

        var read = await svc.GetProfileAsync(subject);
        Assert.NotNull(read);
        Assert.Null(read!.FirstDayOfWeek); // the floor: unset → the Monday default applies
    }

    // ── 2 — set a day: the value is live on the very next read ────────────
    [Fact]
    public async Task SetProfileFirstDayOfWeek_Value_LiveOnNextRead()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string subject = "u-fdw-set";
        await SeedProfileAsync(svc, subject);
        Assert.Null((await svc.GetProfileAsync(subject))!.FirstDayOfWeek);

        await svc.SetProfileFirstDayOfWeekAsync(subject, 0, subject); // Sunday (the opt-in)

        var read = await svc.GetProfileAsync(subject);
        Assert.Equal(0, read!.FirstDayOfWeek);

        // The write touched no other field of the profile.
        Assert.Equal("Resident " + subject, read.DisplayName);
    }

    // ── 3 — set the Monday default over a Sunday: the change persists ─────
    [Fact]
    public async Task SetProfileFirstDayOfWeek_Change_Persists()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string subject = "u-fdw-change";
        await SeedProfileAsync(svc, subject);

        await svc.SetProfileFirstDayOfWeekAsync(subject, 0, subject); // Sunday
        Assert.Equal(0, (await svc.GetProfileAsync(subject))!.FirstDayOfWeek);

        await svc.SetProfileFirstDayOfWeekAsync(subject, 1, subject); // back to Monday
        Assert.Equal(1, (await svc.GetProfileAsync(subject))!.FirstDayOfWeek);

        // Re-settable (the lane is a plain field write, not a tombstone).
        await svc.SetProfileFirstDayOfWeekAsync(subject, 0, subject);
        Assert.Equal(0, (await svc.GetProfileAsync(subject))!.FirstDayOfWeek);
    }

    // ── 4 — missing profile fails closed (throw, never create) ───────────
    [Fact]
    public async Task SetProfileFirstDayOfWeek_MissingProfile_ThrowsKeyNotFound()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.SetProfileFirstDayOfWeekAsync("u-fdw-missing", 0, "u-fdw-any"));

        // Fail closed = no profile row created.
        Assert.Null(await svc.GetProfileAsync("u-fdw-missing"));
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
