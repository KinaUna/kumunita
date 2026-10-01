using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests.UserInfo;

/// <summary>
/// <b>Write-lane</b> tests (M23, ADR 0123 D3 / C-M23·3 / C-M23·6 / GATE-3) for
/// the extended <see cref="IUserInfoService.UpsertProfileAsync"/> single write
/// lane (the F13 single-write-surface pin, extended by U02 — <b>no new method</b>).
/// <para>
/// U01 (the shape tests, <see cref="ProfileBioTagsShapeTests"/>) proved the two
/// additive <see cref="Profile"/> fields round-trip and the
/// <see cref="ProfileUpdate"/> patch carries them. This file pins the
/// <b>write-lane behavior</b>:
/// </para>
/// <list type="bullet">
/// <item><b>Bio verbatim</b> — <c>patch.Bio</c> is stored byte-for-byte (no
/// Core-side Markdown processing, D3; ADR 0001-B the author's choice is
/// absolute).</item>
/// <item><b>null ⇒ don't-touch</b> — a follow-up patch with <c>Bio = null</c> /
/// <c>TagIds = null</c> leaves both untouched (the "null ⇒ don't touch" patch
/// rule).</item>
/// <item><b>Tag create-or-get — new</b> — a missing <c>Slug</c> creates a
/// <see cref="Tags.Tag"/> (+ exactly one <c>tag.create</c>
/// <see cref="AccessAudit"/> row, C-TG·9); the <c>CreatedBy</c> is the acting
/// resident (the <c>actorBy</c> param, or the owner when <c>actorBy</c> is
/// <c>null</c>).</item>
/// <item><b>Tag create-or-get — reuse</b> — a present <c>Slug</c> reuses the
/// existing <see cref="Tags.Tag"/> (no new tag, no new row, C-TG·4).</item>
/// <item><b>No profile-write audit row</b> — the profile write itself emits
/// <b>no</b> audit row (a Profile field write, not an access decision — the
/// existing <c>UpsertProfileAsync</c> shape); the <b>only</b> rows are the
/// <c>tag.create</c> rows, one per newly created tag.</item>
/// <item><b>Zero new authorization surface</b> (C-M23·2) — the existing
/// <see cref="ProfileToAuditableResource"/> adapter is unchanged (the two
/// existing adapters + <c>AccessAction.Read</c> carry everything).</item>
/// </list>
/// <para>
/// Each test hands itself a fresh scratch Postgres DB
/// (<see cref="PostgresFixture.NewDatabaseAsync"/>) — the
/// <see cref="ProfileTimezoneLaneTests"/> / <see cref="TagServiceTests"/>
/// idiom in this assembly — with <see cref="TagDocTypes"/> registered (the
/// create-or-get stores <see cref="Tags.Tag"/> docs the M1-only boot would not
/// know).
/// </para>
/// </summary>
public sealed class ProfileBioTagsWriteLaneTests(PostgresFixture fixture)
    : IClassFixture<PostgresFixture>
{
    // ── Test 1 — bio written byte-for-byte verbatim (no Core Markdown) ───
    [Fact]
    public async Task UpsertProfile_writes_bio_verbatim()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);
        const string subject = "u-m23-bio-verbatim";

        // Rich content (Markdown) — stored verbatim; the MarkdownRenderer render
        // is a Web-side concern (U04/U05), never here (D5).
        const string richBio = "# A resident\n\nKeeps the **community garden**.\n\n> Solar panels on the roof.";

        var profile = new Profile { SubjectId = subject, Verified = true };
        await svc.UpsertProfileAsync(
            profile,
            new ProfileUpdate(null, null, null, null, null, null, richBio, null));

        var read = await svc.GetProfileAsync(subject);
        Assert.NotNull(read);
        Assert.Equal(richBio, read!.Bio);   // byte-for-byte — no Core-side processing
    }

    // ── Test 2 — null patch ⇒ don't-touch (Bio + TagIds both preserved) ────
    [Fact]
    public async Task UpsertProfile_null_bio_and_tags_leave_current_untouched()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);
        const string subject = "u-m23-null-dontouch";

        var profile = new Profile { SubjectId = subject, Verified = true };

        // First save: set a bio + a tag.
        await svc.UpsertProfileAsync(
            profile,
            new ProfileUpdate(null, null, null, null, null, null, "biography v1", new[] { "gardening" }));

        var afterFirst = await svc.GetProfileAsync(subject);
        Assert.Equal("biography v1", afterFirst!.Bio);
        Assert.Single(afterFirst.TagIds);

        // Second save: all-null patch (Bio = null / TagIds = null ⇒ "don't touch").
        await svc.UpsertProfileAsync(
            profile,
            new ProfileUpdate(null, null, null, null, null, null));

        var afterSecond = await svc.GetProfileAsync(subject);
        Assert.Equal("biography v1", afterSecond!.Bio);   // untouched
        Assert.Equal(afterFirst.TagIds, afterSecond.TagIds); // untouched (same single tag id)
    }

    // ── Test 3 — new slug creates a Tag + exactly one tag.create row ───────
    [Fact]
    public async Task UpsertProfile_new_tag_slug_creates_tag_and_one_audit_row()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);
        const string subject = "u-m23-tag-new";
        const string actor = "u-m23-tag-actor";

        var ct = TestContext.Current.CancellationToken;
        var profile = new Profile { SubjectId = subject, Verified = true };
        await svc.UpsertProfileAsync(
            profile,
            new ProfileUpdate(null, null, null, null, null, null, null, new[] { "SOLAR PANELS" }),
            actor);

        // The profile's TagIds carries the new tag's id.
        var read = await svc.GetProfileAsync(subject);
        Assert.NotNull(read);
        Assert.Single(read!.TagIds);
        var tagId = read.TagIds[0];

        // A Tag with the derived slug (lowercased + trimmed, the plan's C-TG·4 idiom)
        // and CreatedBy = the acting resident.
        await using var s = store.QuerySession();
        var tag = await s.Query<Tags.Tag>().Where(t => t.Id == tagId).FirstOrDefaultAsync(ct);
        Assert.NotNull(tag);
        Assert.Equal("solar panels", tag!.Slug);
        Assert.Equal("solar panels", tag.Name);
        Assert.Equal(actor, tag.CreatedBy);

        // Exactly one tag.create audit row, shaped to the TG lane (C-TG·9).
        var createRows = await s.Query<AccessAudit>()
            .Where(a => a.Action == "tag.create").ToListAsync(ct);
        Assert.Single(createRows);
        Assert.Equal(tagId, createRows[0].TargetId);
        Assert.Equal("tag", createRows[0].TargetKind);
        Assert.Equal(actor, createRows[0].ActorId);
        Assert.Equal(AccessVia.Owner, createRows[0].Via);
        Assert.Equal(AccessOutcome.Allow, createRows[0].Outcome);

        // And no other audit row at all — the profile write itself emits none
        // (a Profile field write, not an access decision).
        var total = await s.Query<AccessAudit>().CountAsync(ct);
        Assert.Equal(1, total);
    }

    // ── Test 4 — reusing a present slug creates no new tag / no new row ────
    [Fact]
    public async Task UpsertProfile_reuse_existing_slug_no_new_tag_no_row()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);
        const string subject = "u-m23-tag-reuse";

        var ct = TestContext.Current.CancellationToken;
        var profile = new Profile { SubjectId = subject, Verified = true };

        // First save: create the tag (one tag.create row).
        await svc.UpsertProfileAsync(
            profile,
            new ProfileUpdate(null, null, null, null, null, null, null, new[] { "gardening" }));

        await using var s = store.QuerySession();
        var tagsBefore = await s.Query<Tags.Tag>().CountAsync(ct);
        var createsBefore = await s.Query<AccessAudit>().Where(a => a.Action == "tag.create").CountAsync(ct);
        Assert.Equal(1, tagsBefore);
        Assert.Equal(1, createsBefore);

        // Second save: the same slug (now present) is reused, not re-created.
        await svc.UpsertProfileAsync(
            profile,
            new ProfileUpdate(null, null, null, null, null, null, null, new[] { "GARDENING" }));

        // No new tag, no new tag.create row (C-TG·4).
        Assert.Equal(tagsBefore, await s.Query<Tags.Tag>().CountAsync(ct));
        Assert.Equal(createsBefore, await s.Query<AccessAudit>().Where(a => a.Action == "tag.create").CountAsync(ct));

        // The profile still points at the single, original tag id (reused, not duplicated).
        var read = await svc.GetProfileAsync(subject);
        Assert.Single(read!.TagIds);
        Assert.Equal("gardening", await s.Query<Tags.Tag>().Where(t => t.Id == read.TagIds[0]).Select(t => t.Slug).FirstAsync(ct));
    }

    // ── Test 5 — a bio-only write (no tags) emits zero audit rows ──────────
    [Fact]
    public async Task UpsertProfile_bio_only_emits_no_audit_row()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);
        const string subject = "u-m23-bio-only-noaudit";

        var ct = TestContext.Current.CancellationToken;
        var profile = new Profile { SubjectId = subject, Verified = true };
        await svc.UpsertProfileAsync(
            profile,
            new ProfileUpdate(null, null, null, null, null, null, "A bio, no tags.", null));

        // The profile write is a field write, not an access decision — no audit
        // row of any kind (C-M23·3 / GATE-3).
        await using var s = store.QuerySession();
        Assert.Equal(0, await s.Query<AccessAudit>().CountAsync(ct));
    }

    // ── Test 6 — actorBy null falls back to the owner as CreatedBy ─────────
    [Fact]
    public async Task UpsertProfile_actor_by_null_falls_back_to_owner()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);
        const string subject = "u-m23-fallback-creator";

        var ct = TestContext.Current.CancellationToken;
        var profile = new Profile { SubjectId = subject, Verified = true };
        await svc.UpsertProfileAsync(
            profile,
            new ProfileUpdate(null, null, null, null, null, null, null, new[] { "solar panels" }),
            actorBy: null);   // the only standing is the author's own profile (D3)

        var read = await svc.GetProfileAsync(subject);
        Assert.Single(read!.TagIds);
        await using var s = store.QuerySession();
        var tag = await s.Query<Tags.Tag>().Where(t => t.Id == read.TagIds[0]).FirstOrDefaultAsync(ct);
        Assert.NotNull(tag);
        Assert.Equal(subject, tag!.CreatedBy);   // fell back to the owner
    }

    // ── Test 7 — zero new authorization surface (C-M23·2 pin) ──────────────
    //
    // The write lane must ride the existing authorization seams: the two
    // existing adapters + AccessAction.Read. Pin that the
    // ProfileToAuditableResource (the adapter the detail gate + find-people
    // read reuse) still projects exactly the frozen six-member shape and the
    // "directory" TargetKind — nothing new was added to the authorization
    // surface by M23 (D7). Pure in-memory; no store.
    [Fact]
    public void Authorization_seam_unchanged_profile_to_auditable_resource_pinned()
    {
        var visibility = new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, "u-member")]);
        var profile = new Profile
        {
            SubjectId = "u-seam",
            DisplayName = "Seam",
            Verified = true,
            Visibility = visibility,
            ContactVisibility = new Audience()   // distinct on purpose — never projected
        };

        var adapter = new ProfileToAuditableResource(profile);

        Assert.Equal("u-seam", adapter.Id);
        Assert.Equal("Seam", adapter.Name);
        Assert.Equal("u-seam", adapter.OwnerId);
        Assert.Same(visibility, adapter.Audience);   // exactly Visibility, not ContactVisibility
        Assert.Null(adapter.ComponentId);
        Assert.Equal("directory", adapter.TargetKind);
    }

    // ── Shared helpers ────────────────────────────────────────────────────

    /// <summary>One scratch Postgres DB + a <c>mt</c> schema over the M1 docs
    /// (the <see cref="Profile"/> doc) <b>plus</b> <see cref="TagDocTypes"/>
    /// (the inlined create-or-get stores <see cref="Tags.Tag"/> docs). Mirrors
    /// <see cref="ProfileTimezoneLaneTests.BootStoreAsync"/> with the
    /// <c>TagDocTypes.Configure</c> line the <see cref="TagServiceTests"/>
    /// boot path carries.</summary>
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
            TagDocTypes.Configure(opts);   // the two new Tag/TagTranslation docs (the create-or-get writes Tag)
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }
}
