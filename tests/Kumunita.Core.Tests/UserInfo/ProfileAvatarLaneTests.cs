using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests.UserInfo;

/// <summary>
/// Lane tests (media U5; design doc §2.5 pinned names) for
/// <see cref="UserInfoService.SetProfileAvatarAsync"/> — the single
/// C-MED·8 write lane on the UserInfo side, pinned at the service layer
/// (owner scope is the Web boundary's job; the lane writes
/// <see cref="Profile.AvatarId"/> only). Each test hands itself a fresh
/// scratch Postgres DB (<see cref="PostgresFixture.NewDatabaseAsync"/>),
/// mirroring the <c>BootStoreAsync</c> shape
/// <see cref="UserInfoServiceGroupDescriptionTests"/> established in this
/// assembly.
/// <para>
/// The invariants pinned here:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>Set (C4 strong consistency)</b> — the new avatar id is live on the
/// very next <c>GetProfileAsync</c> (the fresh read is the persistence
/// proof; C-MED·4: the id is a content hash, the lane stores it as a plain
/// string and never loads the <c>MediaObject</c> catalog).
/// </item>
/// <item>
/// <b>Null clears, and the clear persists</b> (C-MED·8) — a fresh
/// <c>GetProfileAsync</c> after the null call still sees
/// <c>AvatarId == null</c>.
/// </item>
/// <item>
/// <b>Missing profile fails closed</b> — a throw
/// (<see cref="KeyNotFoundException"/>, the design doc §2.2 pinned type)
/// and *no* profile created (never load-or-create on this lane).
/// </item>
/// </list>
/// </summary>
public sealed class ProfileAvatarLaneTests(PostgresFixture fixture)
    : IClassFixture<PostgresFixture>
{
    // A well-formed avatar id (lowercase-hex, the MediaObject.Id shape,
    // C-MED·4) — the lane references the catalog by id only; no hashing or
    // volume is involved in this test (the U3 KnownHash idiom).
    private const string KnownHash =
        "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90";

    // ── Test 1 — set: the id is live on the very next read (C4) ──────────
    [Fact]
    public async Task UserInfoService_SetProfileAvatar_sets_id()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string subject = "u-avatar-set";
        await SeedProfileAsync(svc, subject);

        // Fresh seed ⇒ no avatar yet (the additive field defaults to null).
        Assert.Null((await svc.GetProfileAsync(subject))?.AvatarId);

        await svc.SetProfileAvatarAsync(subject, KnownHash, subject);

        // The write is live on the very next single-profile read (C4); the
        // fresh read through the service's own read lane is what proves the
        // save landed — not an in-memory assertion.
        var read = await svc.GetProfileAsync(subject);
        Assert.NotNull(read);
        Assert.Equal(KnownHash, read!.AvatarId);

        // The write touched no other field of the profile.
        Assert.Equal("Resident " + subject, read.DisplayName);
    }

    // ── Test 2 — null clears, and the clear persists (C-MED·8) ───────────
    [Fact]
    public async Task UserInfoService_SetProfileAvatar_null_clears()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string subject = "u-avatar-clear";
        await SeedProfileAsync(svc, subject);

        // Set an avatar first (test 1 proves the set; this test proves the
        // explicit clear over a set value).
        await svc.SetProfileAvatarAsync(subject, KnownHash, subject);
        Assert.Equal(KnownHash, (await svc.GetProfileAsync(subject))!.AvatarId);

        // null ⇒ clear (the lane still stores + saves, so the clear persists
        // — a fresh read must still see null afterwards).
        await svc.SetProfileAvatarAsync(subject, null, subject);

        Assert.Null((await svc.GetProfileAsync(subject))?.AvatarId);

        // …and the clear is re-settable (the lane is a plain field write,
        // not a tombstone).
        await svc.SetProfileAvatarAsync(subject, KnownHash, subject);
        Assert.Equal(KnownHash, (await svc.GetProfileAsync(subject))!.AvatarId);
    }

    // ── Test 3 — missing profile fails closed (throw, never create) ──────
    [Fact]
    public async Task UserInfoService_SetProfileAvatar_missing_profile_throws_KeyNotFound()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        // The design doc §2.2 pins KeyNotFoundException for this lane (U4's
        // doc-wins note over the group lanes' InvalidOperationException).
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.SetProfileAvatarAsync("u-avatar-missing", KnownHash, "u-avatar-any"));

        // Fail closed = fail *without side effects*: no profile row was
        // created (the lane is load-throw-set-save, never load-or-create).
        Assert.Null(await svc.GetProfileAsync("u-avatar-missing"));
    }

    // ── Shared helpers ────────────────────────────────────────────────────

    /// <summary>Seed a bootstrap profile through the service's own
    /// <c>UpsertProfileAsync</c> (all-null patch ⇒ the record supplies every
    /// field — the exact idiom at <c>UserInfoServiceTests</c> L62).</summary>
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
    /// (the <see cref="Profile"/> doc — the lane touches nothing else; no
    /// <c>MediaDocTypes</c> surface needed). Mirrors
    /// <see cref="UserInfoServiceGroupDescriptionTests.BootStoreAsync"/>
    /// in this assembly.</summary>
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
