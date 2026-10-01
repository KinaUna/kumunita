using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests.UserInfo;

/// <summary>
/// <b>Shape</b> tests (M23, ADR 0123 D1/D3) for the two additive
/// <see cref="Profile"/> fields (<see cref="Profile.Bio"/>,
/// <see cref="Profile.TagIds"/>) and the <see cref="ProfileUpdate"/> patch
/// record extension (the two additive optional trailing fields
/// <see cref="ProfileUpdate.Bio"/> / <see cref="ProfileUpdate.TagIds"/>).
/// <para>
/// This is the **U01** unit — the data-model shape only. It proves:
/// </para>
/// <list type="bullet">
/// <item>
/// The two new <see cref="Profile"/> columns **round-trip** (store a
/// <see cref="Profile"/> with a <see cref="Profile.Bio"/> + a
/// <see cref="Profile.TagIds"/> array; reload; assert verbatim) and default
/// to <c>null</c> / empty on a fresh profile (the ADR 0004 §B.1 additive
/// no-reseed pin — the new columns are delta-detected, idempotent, no
/// re-seed).
/// </item>
/// <item>
/// <see cref="ProfileUpdate"/> **carries** the two new fields: the frozen
/// six-field positional shape still compiles (the <c>Address = null</c>
/// appended-with-default precedent), a new
/// <see cref="ProfileUpdate"/> with <c>Bio</c> + <c>TagIds</c> set reflects
/// them, and the all-default shape has both <c>null</c>.
/// </item>
/// <item>
/// A <see cref="ProfileUpdate"/> with <c>Bio = null</c> / <c>TagIds = null</c>
/// is **distinguishable** from one with them set (the "null ⇒ don't touch"
/// patch rule every other field follows).
/// </item>
/// </list>
/// <para>
/// These are <b>shape</b> tests. The <b>write-lane</b> behavior (bio written
/// verbatim, tag create-or-get resolve, the <c>tag.create</c> audit row, the
/// no-profile-write audit row) is <b>U02's</b> — it is deliberately <b>not</b>
/// tested here. Mirrors the <see cref="ProfileTimezoneLaneTests"/> harness
/// idiom in this assembly (fresh scratch Postgres DB per test).
/// </para>
/// </summary>
public sealed class ProfileBioTagsShapeTests(PostgresFixture fixture)
    : IClassFixture<PostgresFixture>
{
    // ── Test 1 — the two new Profile fields round-trip ────────────────────
    [Fact]
    public async Task Profile_round_trips_bio_and_tag_ids()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string subject = "u-m23-roundtrip";
        var bio = "**A resident** who keeps the community garden.\n\nMore.";
        var tagIds = new List<string> { "solar-panels", "gardening" };

        // Store the Profile document directly through the document store (the
        // pure persistence proof — the ADR 0004 §B.1 additive round-trip). The
        // write-lane wiring of these two fields (bio verbatim + tag
        // create-or-get) is U02's, so we store the shape here, not through
        // UpsertProfileAsync.
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(new Profile
        {
            SubjectId = subject,
            DisplayName = "Resident " + subject,
            Verified = true,
            Bio = bio,
            TagIds = tagIds,
        });
        await session.SaveChangesAsync();

        // The fresh read through the service's own read lane is the persistence
        // proof: the two new columns exist and come back verbatim.
        var read = await svc.GetProfileAsync(subject);
        Assert.NotNull(read);
        Assert.Equal(bio, read!.Bio);
        Assert.Equal(tagIds, read.TagIds);
    }

    // ── Test 2 — fresh profile defaults to no bio / empty tags ────────────
    [Fact]
    public async Task Profile_fresh_defaults_bio_null_tag_ids_empty()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string subject = "u-m23-fresh";

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(new Profile
        {
            SubjectId = subject,
            DisplayName = "Resident " + subject,
            Verified = true,
        });
        await session.SaveChangesAsync();

        // The additive no-reseed pin (ADR 0004 §B.1): a profile created before
        // / without the two new fields reads back with <c>null</c> bio and an
        // <c>empty</c> tag list (not null, not an exception) — the same shape
        // <see cref="Posts.Post.TagIds"/> reads back with on a pre-tag post.
        var read = await svc.GetProfileAsync(subject);
        Assert.NotNull(read);
        Assert.Null(read!.Bio);
        Assert.NotNull(read.TagIds);
        Assert.Empty(read.TagIds);
    }

    // ── Test 3 — ProfileUpdate carries the two new fields with defaults ───
    [Fact]
    public void ProfileUpdate_carries_bio_and_tag_ids_with_defaults()
    {
        // The frozen six-field positional shape (M1 five + Address) still
        // compiles and leaves the two new fields at their default (null ⇒
        // don't touch) — the appended-with-default precedent.
        var frozen = new ProfileUpdate("Name", "a@b.c", "555", null, null, "12 Main St");
        Assert.Equal("12 Main St", frozen.Address);
        Assert.Null(frozen.Bio);
        Assert.Null(frozen.TagIds);

        // A new ProfileUpdate with Bio + TagIds set (positional, all eight
        // fields) reflects them.
        var tagIds = new List<string> { "solar-panels" };
        var full = new ProfileUpdate(
            "Name", "a@b.c", "555", null, null,
            "12 Main St", "My bio.", tagIds);
        Assert.Equal("My bio.", full.Bio);
        Assert.NotNull(full.TagIds);
        Assert.Equal(tagIds, full.TagIds!.ToArray());

        // The all-default shape (positional six) has both new fields null.
        var def = new ProfileUpdate(null, null, null, null, null, null);
        Assert.Null(def.Bio);
        Assert.Null(def.TagIds);
    }

    // ── Test 4 — null ⇒ don't-touch patch shape is distinguishable ────────
    [Fact]
    public void ProfileUpdate_null_distinguishes_from_set()
    {
        // A patch with Bio = null / TagIds = null (the default ⇒ "don't touch")
        // is distinguishable from a patch that sets them (the write lane reads
        // null as "leave the current value untouched" — U02's job to honor).
        var leaveAlone = new ProfileUpdate(null, null, null, null, null, null);
        Assert.Null(leaveAlone.Bio);
        Assert.Null(leaveAlone.TagIds);

        var setBoth = new ProfileUpdate(null, null, null, null, null, null, "Bio set.", new List<string> { "gardening" });
        Assert.Equal("Bio set.", setBoth.Bio);
        Assert.NotNull(setBoth.TagIds);
        Assert.Contains("gardening", setBoth.TagIds!);

        // An empty (non-null) tag list is also distinguishable from null: it
        // means "clear the tags", not "don't touch" (the same null-vs-empty
        // distinction Post/Page tag patches rely on).
        var clear = new ProfileUpdate(null, null, null, null, null, null, null, Array.Empty<string>());
        Assert.Null(clear.Bio);
        Assert.NotNull(clear.TagIds);
        Assert.Empty(clear.TagIds!);
    }

    // ── Shared helpers ────────────────────────────────────────────────────

    /// <summary>One scratch Postgres DB + a <c>mt</c> schema over the M1 docs
    /// (the <see cref="Profile"/> doc — the shape touches nothing else). Mirrors
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
