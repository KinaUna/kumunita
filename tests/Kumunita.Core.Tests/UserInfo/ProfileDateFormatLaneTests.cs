using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests.UserInfo;

/// <summary>
/// Lane tests (ADR 0020) for <see cref="UserInfoService.SetProfileDateFormatAsync"/>
/// — the single write lane on the UserInfo side for a resident's personal date-time
/// format override, pinned at the service layer (owner scope is the Web
/// boundary's job; the lane writes <see cref="Profile.DateFormat"/> only).
/// Mirrors <see cref="ProfileTimezoneLaneTests"/> exactly (same invariants,
/// same scratch-DB idiom):
/// <list type="bullet">
/// <item><b>Set (strong consistency)</b> — the chosen format string is live on
/// the very next <c>GetProfileAsync</c> (the fresh read is the persistence
/// proof).</item>
/// <item><b>Null clears, and the clear persists</b> — a fresh <c>GetProfileAsync</c>
/// after the null call still sees <c>DateFormat == null</c> (fall through to the
/// platform default).</item>
/// <item><b>Missing profile fails closed</b> — a throw
/// (<see cref="KeyNotFoundException"/> — the same pinned type as the timezone
/// lane) and *no* profile created (never load-or-create on this lane).</item>
/// </list>
/// </summary>
public sealed class ProfileDateFormatLaneTests(PostgresFixture fixture)
    : IClassFixture<PostgresFixture>
{
    // ── Test 1 — set: the format string is live on the very next read ─────
    [Fact]
    public async Task UserInfoService_SetProfileDateFormat_sets_format()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string subject = "u-df-set";
        await SeedProfileAsync(svc, subject);
        const string fmt = DateFormat.IsoFormat; // a well-formed, usable format

        // Fresh seed ⇒ no personal date format yet (the additive field defaults to null).
        Assert.Null((await svc.GetProfileAsync(subject))?.DateFormat);

        await svc.SetProfileDateFormatAsync(subject, fmt, subject);

        // The write is live on the very next single-profile read; the fresh
        // read through the service's own read lane is what proves the save
        // landed — not an in-memory assertion.
        var read = await svc.GetProfileAsync(subject);
        Assert.NotNull(read);
        Assert.Equal(fmt, read!.DateFormat);

        // The write touched no other field of the profile.
        Assert.Equal("Resident " + subject, read.DisplayName);
    }

    // ── Test 2 — a custom format round-trips (the "not a preset" value) ───
    [Fact]
    public async Task UserInfoService_SetProfileDateFormat_custom_format_roundTrips()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string subject = "u-df-custom";
        await SeedProfileAsync(svc, subject);
        const string custom = "dd.MM.yyyy HH:mm"; // valid, but not one of the presets

        Assert.Null(DateFormat.Match(custom)); // proof it is a custom value
        await svc.SetProfileDateFormatAsync(subject, custom, subject);

        Assert.Equal(custom, (await svc.GetProfileAsync(subject))!.DateFormat);
    }

    // ── Test 3 — null clears, and the clear persists ───────────────────────
    [Fact]
    public async Task UserInfoService_SetProfileDateFormat_null_clears()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string subject = "u-df-clear";
        await SeedProfileAsync(svc, subject);

        // Set a format first (test 1 proves the set; this test proves the
        // explicit clear over a set value).
        await svc.SetProfileDateFormatAsync(subject, DateFormat.IsoFormat, subject);
        Assert.Equal(DateFormat.IsoFormat, (await svc.GetProfileAsync(subject))!.DateFormat);

        // null ⇒ clear (the lane still stores + saves, so the clear persists
        // — a fresh read must still see null afterwards, falling through to the
        // platform default).
        await svc.SetProfileDateFormatAsync(subject, null, subject);
        Assert.Null((await svc.GetProfileAsync(subject))?.DateFormat);

        // …and the clear is re-settable (the lane is a plain field write,
        // not a tombstone).
        await svc.SetProfileDateFormatAsync(subject, DateFormat.IsoFormat, subject);
        Assert.Equal(DateFormat.IsoFormat, (await svc.GetProfileAsync(subject))!.DateFormat);
    }

    // ── Test 4 — missing profile fails closed (throw, never create) ────────
    [Fact]
    public async Task UserInfoService_SetProfileDateFormat_missing_profile_throws_KeyNotFound()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        // The ADR 0020 pin: KeyNotFoundException for this lane (the same
        // pinned type as the timezone lane — load-throw-set-save, never
        // load-or-create).
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.SetProfileDateFormatAsync("u-df-missing", DateFormat.IsoFormat, "u-df-any"));

        // Fail closed = fail *without side effects*: no profile row was
        // created (the lane never loads or creates on this path).
        Assert.Null(await svc.GetProfileAsync("u-df-missing"));
    }

    // ── Shared helpers ────────────────────────────────────────────────────

    /// <summary>Seed a bootstrap profile through the service's own
    /// <c>UpsertProfileAsync</c> (all-null patch ⇒ the record supplies every
    /// field — the exact idiom at <c>ProfileTimezoneLaneTests</c>).</summary>
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
    /// <see cref="ProfileTimezoneLaneTests.BootStoreAsync"/> in this assembly.</summary>
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
