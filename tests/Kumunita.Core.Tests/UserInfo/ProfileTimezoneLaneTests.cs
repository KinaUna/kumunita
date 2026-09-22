using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests.UserInfo;

/// <summary>
/// Lane tests (ADR 0019) for <see cref="UserInfoService.SetProfileTimezoneAsync"/>
/// — the single write lane on the UserInfo side for a resident's personal time
/// zone override, pinned at the service layer (owner scope is the Web
/// boundary's job; the lane writes <see cref="Profile.TimeZone"/> only).
/// Each test hands itself a fresh scratch Postgres DB (<see cref="PostgresFixture.NewDatabaseAsync"/>),
/// mirroring the <see cref="ProfileAvatarLaneTests"/> idiom established in
/// this assembly.
/// <para>
/// The invariants pinned here:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>Set (strong consistency)</b> — the chosen IANA id is live on the very
/// next <c>GetProfileAsync</c> (the fresh read is the persistence proof).
/// </item>
/// <item>
/// <b>Null clears, and the clear persists</b> — a fresh <c>GetProfileAsync</c>
/// after the null call still sees <c>TimeZone == null</c> (fall through to the
/// platform default).
/// </item>
/// <item>
/// <b>Missing profile fails closed</b> — a throw (<see cref="KeyNotFoundException"/>
/// — the same pinned type as the avatar lane) and *no* profile created (never
/// load-or-create on this lane).
/// </item>
/// </list>
/// </summary>
public sealed class ProfileTimezoneLaneTests(PostgresFixture fixture)
    : IClassFixture<PostgresFixture>
{
    // Pick a well-formed non-UTC IANA id that is guaranteed to exist on whatever
    // OS runs this test (Windows ids, IANA ids, or both, depending on platform)
    // — so the test is platform-agnostic rather than hard-coding one family of
    // ids that the other OS would not recognize.
    private static string PickNonUtcZone()
    {
        foreach (var z in System.TimeZoneInfo.GetSystemTimeZones())
        {
            if (z.Id.StartsWith("*", StringComparison.Ordinal)) continue; // "Standard" pseudo-zones
            if (string.Equals(z.Id, "UTC", StringComparison.OrdinalIgnoreCase)) continue;
            return z.Id;
        }
        return "UTC";
    }

    // ── Test 1 — set: the id is live on the very next read ───────────────
    [Fact]
    public async Task UserInfoService_SetProfileTimezone_sets_id()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string subject = "u-tz-set";
        await SeedProfileAsync(svc, subject);
        var zone = PickNonUtcZone();

        // Fresh seed ⇒ no personal time zone yet (the additive field defaults to null).
        Assert.Null((await svc.GetProfileAsync(subject))?.TimeZone);

        await svc.SetProfileTimezoneAsync(subject, zone, subject);

        // The write is live on the very next single-profile read; the fresh
        // read through the service's own read lane is what proves the save
        // landed — not an in-memory assertion.
        var read = await svc.GetProfileAsync(subject);
        Assert.NotNull(read);
        Assert.Equal(zone, read!.TimeZone);

        // The write touched no other field of the profile.
        Assert.Equal("Resident " + subject, read.DisplayName);
    }

    // ── Test 2 — null clears, and the clear persists ─────────────────────
    [Fact]
    public async Task UserInfoService_SetProfileTimezone_null_clears()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string subject = "u-tz-clear";
        await SeedProfileAsync(svc, subject);
        var zone = PickNonUtcZone();

        // Set a zone first (test 1 proves the set; this test proves the
        // explicit clear over a set value).
        await svc.SetProfileTimezoneAsync(subject, zone, subject);
        Assert.Equal(zone, (await svc.GetProfileAsync(subject))!.TimeZone);

        // null ⇒ clear (the lane still stores + saves, so the clear persists
        // — a fresh read must still see null afterwards, falling through to
        // the platform default).
        await svc.SetProfileTimezoneAsync(subject, null, subject);

        Assert.Null((await svc.GetProfileAsync(subject))?.TimeZone);

        // …and the clear is re-settable (the lane is a plain field write,
        // not a tombstone).
        await svc.SetProfileTimezoneAsync(subject, zone, subject);
        Assert.Equal(zone, (await svc.GetProfileAsync(subject))!.TimeZone);
    }

    // ── Test 3 — missing profile fails closed (throw, never create) ──────
    [Fact]
    public async Task UserInfoService_SetProfileTimezone_missing_profile_throws_KeyNotFound()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        // The ADR 0019 pin: KeyNotFoundException for this lane (the same
        // pinned type as the avatar lane — load-throw-set-save, never
        // load-or-create).
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.SetProfileTimezoneAsync("u-tz-missing", "UTC", "u-tz-any"));

        // Fail closed = fail *without side effects*: no profile row was
        // created (the lane never loads or creates on this path).
        Assert.Null(await svc.GetProfileAsync("u-tz-missing"));
    }

    // ── Shared helpers ────────────────────────────────────────────────────

    /// <summary>Seed a bootstrap profile through the service's own
    /// <c>UpsertProfileAsync</c> (all-null patch ⇒ the record supplies every
    /// field — the exact idiom at <c>ProfileAvatarLaneTests</c>).</summary>
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

    /// <summary>One scratch Postgres DB + a <c>mt</c> schema over the M1 docs
    /// (the <see cref="Profile"/> doc — the lane touches nothing else). Mirrors
    /// <see cref="ProfileAvatarLaneTests.BootStoreAsync"/> in this assembly.</summary>
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
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }
}
