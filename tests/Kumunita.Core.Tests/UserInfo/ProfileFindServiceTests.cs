using System.Reflection;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests.UserInfo;

/// <summary>
/// <b>Find-people read</b> tests (M23, ADR 0123 D4/D6 / C-M23·2/4/6 / GATE-2 /
/// GATE-4) for the new <see cref="IProfileFindService"/> composition service
/// (the <see cref="DirectoryService"/> shape — ADR 0006-D "composes only the
/// frozen seams" pin) and its two paged reads: by-profile-tag + bio-substring.
/// <para>
/// Each test hands itself a fresh scratch Postgres DB
/// (<see cref="PostgresFixture.NewDatabaseAsync"/>) — the
/// <see cref="ProfileBioTagsWriteLaneTests"/> / <see cref="TagServiceTests"/>
/// idiom in this assembly — with <see cref="TagDocTypes"/> registered (the
/// by-tag read resolves a <see cref="Kumunita.Core.Tags.Tag"/> doc the M1-only
/// boot would not know). The two frozen seams the service composes are
/// composed directly here: <see cref="UserInfoService"/> (candidate set) +
/// <see cref="AuthorizationService"/> (the per-profile <c>Visibility</c>
/// decision via the existing <see cref="ProfileToAuditableResource"/>) — the
/// <see cref="DirectoryService"/> composition shape, ADR 0006-D.
/// </para>
/// <para>
/// The <b>gate</b> is the frozen <c>CanSeeAsync</c> (the M3
/// <c>ListFeedAsync</c> lane, C1): one call over the whole candidate set,
/// one <b>aggregate</b> <see cref="AccessAudit"/> row (<c>TargetId = null</c>,
/// <c>VisibleCount</c>/<c>HiddenCount</c> set, <c>TargetKind = "directory"</c>),
/// the owner branch resolved internally (the author always sees their own —
/// F3), and one per-item row for each candidate whose <c>Audience</c> is
/// non-null (the existing lane's behavior — M23 does not change it). A blank
/// query / a missing tag early-returns <b>before</b> any decision, so no row
/// at all (the M3 0-candidate / M8 "no decision, no row" shape).
/// </para>
/// </summary>
public sealed class ProfileFindServiceTests(PostgresFixture fixture)
    : IClassFixture<PostgresFixture>
{
    // ── Test 1 — by-tag over visible profiles only (GATE-2) ───────────────
    //
    // Two profiles share the "gardening" tag: one whose <c>Visibility</c>
    // includes the viewer (visible), one whose <c>Visibility</c> excludes the
    // viewer (denied). The by-tag read returns <b>only</b> the visible one;
    // the denied profile never surfaces and its tag is not a match artifact
    // (C-M23·4, D6). The tag resolves (the <see cref="ProfileTagPage.Tag"/>
    // is non-null and is the "gardening" tag — the display-name resolution
    // seam for the Web layer).
    [Fact]
    public async Task FindPeopleByTag_over_visible_profiles_only_denied_never_surfaces()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var svc = new ProfileFindService(userInfo, authz, store);

        const string viewer = "u-m23-find-t1-viewer";
        const string deniedSubject = "u-m23-find-t1-denied";
        const string visibleSubject = "u-m23-find-t1-visible";

        // Profile A (denied) — self-only visibility; the viewer is not granted.
        var denied = new Profile { SubjectId = deniedSubject, DisplayName = "Denied", Verified = true };
        await userInfo.UpsertProfileAsync(
            denied,
            new ProfileUpdate(null, null, null,
                new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, deniedSubject)]),
                null, null, null, new[] { "gardening" }));   // creates the "gardening" tag

        // Profile B (visible) — the viewer is granted.
        var visible = new Profile { SubjectId = visibleSubject, DisplayName = "Visible", Verified = true };
        await userInfo.UpsertProfileAsync(
            visible,
            new ProfileUpdate(null, null, null,
                new Audience(AudienceMode.Any, [
                    new AudienceGrant(GrantKind.User, viewer),
                    new AudienceGrant(GrantKind.User, visibleSubject)]),
                null, null, null, new[] { "GARDENING" }));   // reuse (no new tag, C-TG·4)

        var ct = TestContext.Current.CancellationToken;

        var page = await svc.FindPeopleByTagAsync("gardening", viewer, 1);

        // Only the visible profile surfaces — the denied one never leaks (D6).
        Assert.Single(page.Profiles);
        Assert.Equal(visibleSubject, page.Profiles[0].SubjectId);
        Assert.False(page.HasMore);

        // The tag resolved (the display-name resolution seam, ADR 0044 /
        // ADR 0005 preference order — the Web layer renders the name).
        Assert.NotNull(page.Tag);
        Assert.Equal("gardening", page.Tag!.Slug);
        Assert.Equal("gardening", page.Tag.Name);

        // GATE-2 — exactly one <b>aggregate</b> AccessAudit row, shaped to
        // the frozen C-M23·4 lane: TargetKind "directory", TargetId null,
        // VisibleCount/HiddenCount set (visible=1, hidden=1 — the denied
        // profile). (The two per-item rows the frozen lane also writes for
        // the two non-null-audience candidates are the lane's own behavior,
        // not a M23 addition.)
        await using var s = store.QuerySession();
        var aggregate = await s.Query<AccessAudit>()
            .Where(a => a.TargetKind == "directory" && a.TargetId == null)
            .ToListAsync(ct);
        Assert.Single(aggregate);
        Assert.Equal(1, aggregate[0].VisibleCount);
        Assert.Equal(1, aggregate[0].HiddenCount);
    }

    // ── Test 2 — bio-substring over visible profiles only (GATE-2) ─────────
    //
    // Two profiles' Bio contain "garden": one <c>Visibility</c>-visible, one
    // <c>Visibility</c>-denied. The bio read returns only the visible one —
    // the denied profile's bio is never a match artifact (D6). The match is
    // case-insensitive (the "Garden" vs. "garden" case-insensitivity check
    // happens implicitly here: both bios contain "garden"; the query is
    // "garden").
    [Fact]
    public async Task FindPeopleByBio_over_visible_profiles_only_denied_never_surfaces()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var svc = new ProfileFindService(userInfo, authz, store);

        const string viewer = "u-m23-find-t2-viewer";
        const string deniedSubject = "u-m23-find-t2-denied";
        const string visibleSubject = "u-m23-find-t2-visible";

        var denied = new Profile
        {
            SubjectId = deniedSubject,
            DisplayName = "Denied",
            Verified = true,
            Bio = "Keeps a **garden** and composts.",
        };
        await userInfo.UpsertProfileAsync(
            denied,
            new ProfileUpdate(null, null, null,
                new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, deniedSubject)]),
                null, null, null, null));

        var visible = new Profile
        {
            SubjectId = visibleSubject,
            DisplayName = "Visible",
            Verified = true,
            Bio = "Tends a small Garden in the backyard.",
        };
        await userInfo.UpsertProfileAsync(
            visible,
            new ProfileUpdate(null, null, null,
                new Audience(AudienceMode.Any, [
                    new AudienceGrant(GrantKind.User, viewer),
                    new AudienceGrant(GrantKind.User, visibleSubject)]),
                null, null, null, null));

        var ct = TestContext.Current.CancellationToken;

        // Case-insensitive substring (M8 D4 floor) — the query "garden"
        // matches both Bios (one "garden", the other "Garden").
        var page = await svc.FindPeopleByBioAsync("garden", viewer, 1);

        Assert.Single(page.Profiles);
        Assert.Equal(visibleSubject, page.Profiles[0].SubjectId);
        Assert.False(page.HasMore);

        // GATE-2 — one aggregate AccessAudit row (visible=1, hidden=1,
        // TargetKind "directory", TargetId null).
        await using var s = store.QuerySession();
        var aggregate = await s.Query<AccessAudit>()
            .Where(a => a.TargetKind == "directory" && a.TargetId == null)
            .ToListAsync(ct);
        Assert.Single(aggregate);
        Assert.Equal(1, aggregate[0].VisibleCount);
        Assert.Equal(1, aggregate[0].HiddenCount);
    }

    // ── Test 3 — one aggregate row per non-empty read (GATE-2) ─────────────
    //
    // After a non-empty by-tag read, assert exactly <b>one</b> aggregate
    // AccessAudit row — the C-M23·4 lane shape (TargetKind "directory",
    // TargetId null, VisibleCount/HiddenCount set) — emitted by the frozen
    // CanSeeAsync (the M3 ListFeedAsync idiom), not hand-written. (The
    // per-item rows the frozen lane also writes for non-null-audience
    // candidates are the lane's own behavior, not a M23 addition.)
    [Fact]
    public async Task FindPeopleByTag_emits_exactly_one_aggregate_audit_row()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var svc = new ProfileFindService(userInfo, authz, store);

        const string viewer = "u-m23-find-t3-viewer";
        const string subject = "u-m23-find-t3-subject";

        // One visible profile tagged "solar panels".
        var profile = new Profile { SubjectId = subject, DisplayName = "Resident", Verified = true };
        await userInfo.UpsertProfileAsync(
            profile,
            new ProfileUpdate(null, null, null,
                new Audience(AudienceMode.Any, [
                    new AudienceGrant(GrantKind.User, viewer),
                    new AudienceGrant(GrantKind.User, subject)]),
                null, null, null, new[] { "solar panels" }));

        await svc.FindPeopleByTagAsync("solar panels", viewer, 1);

        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();

        // Exactly one <b>aggregate</b> row (TargetId null, counts set,
        // TargetKind "directory") — the C-M23·4 lane.
        var aggregate = await s.Query<AccessAudit>()
            .Where(a => a.TargetKind == "directory" && a.TargetId == null)
            .ToListAsync(ct);
        Assert.Single(aggregate);
        Assert.NotNull(aggregate[0].VisibleCount);
        Assert.NotNull(aggregate[0].HiddenCount);
        Assert.Equal(1, aggregate[0].VisibleCount);   // the one visible candidate
        Assert.Equal(0, aggregate[0].HiddenCount);    // no hidden candidate
        Assert.Equal(viewer, aggregate[0].ActorId);
    }

    // ── Test 4 — blank query / missing tag ⇒ empty page, no row (GATE-2) ──
    //
    // A blank bio query and a tag slug that resolves to no Tag doc both
    // return an empty page and emit <b>no</b> AccessAudit row — the M3
    // 0-candidate / M8 "no decision, no row" shape (the service's early
    // return runs before CanSeeAsync, so the frozen seam is never called).
    [Fact]
    public async Task Blank_or_missing_input_empty_page_no_audit_row()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var svc = new ProfileFindService(userInfo, authz, store);

        const string actor = "u-m23-find-t4-actor";

        var ct = TestContext.Current.CancellationToken;

        // (a) Blank bio query — empty page, no row.
        var blankBio = await svc.FindPeopleByBioAsync("   ", actor, 1);
        Assert.Empty(blankBio.Profiles);
        Assert.False(blankBio.HasMore);
        Assert.Equal(0, await AuditCount(store, ct));

        // (b) Blank tag slug — empty page, no row.
        var blankTag = await svc.FindPeopleByTagAsync("  ", actor, 1);
        Assert.Empty(blankTag.Profiles);
        Assert.Null(blankTag.Tag);
        Assert.False(blankTag.HasMore);
        Assert.Equal(0, await AuditCount(store, ct));

        // (c) A tag slug that resolves to no Tag doc — empty page, no row
        // (the not-found shape — the M3 0-candidate early return).
        var missingTag = await svc.FindPeopleByTagAsync("definitely-not-a-tag-123", actor, 1);
        Assert.Empty(missingTag.Profiles);
        Assert.Null(missingTag.Tag);
        Assert.False(missingTag.HasMore);
        Assert.Equal(0, await AuditCount(store, ct));
    }

    // ── Test 5 — the owner always sees their own (F3) ──────────────────────
    //
    // A profile the actor <b>owns</b> (a <c>Visibility</c> that excludes
    // everyone else) still surfaces in the actor's own find-people read —
    // the owner branch of the frozen Decide() short-circuits (branch 1)
    // before the audience evaluation (the F3 pin, C-M23·1, D2). A different
    // viewer, granted the same bio-substring, does <b>not</b> see it.
    [Fact]
    public async Task Owner_always_sees_own_profile_even_when_visibility_denies_others()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var svc = new ProfileFindService(userInfo, authz, store);

        const string owner = "u-m23-find-t5-owner";
        const string other = "u-m23-find-t5-other";

        // The owner's profile: Bio matches "solar"; Visibility self-only
        // (the owner, no one else). The owner branch of Decide() allows the
        // owner (branch 1 — before the audience evaluation), denies the
        // other.
        var ownerProfile = new Profile
        {
            SubjectId = owner,
            DisplayName = "Owner",
            Verified = true,
            Bio = "Installs solar panels on the roof.",
        };
        await userInfo.UpsertProfileAsync(
            ownerProfile,
            new ProfileUpdate(null, null, null,
                new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, owner)]),
                null, null, null, null));

        // A second profile the owner AND the other can see (the other is
        // granted) — Bio matches "solar". This gives the other's result a
        // non-empty set (so the "empty vs. owner-only" distinction is
        // visible, not masked by a 0-result edge).
        var otherVisible = new Profile
        {
            SubjectId = "u-m23-find-t5-shared",
            DisplayName = "Shared",
            Verified = true,
            Bio = "Sells solar panel kits.",
        };
        await userInfo.UpsertProfileAsync(
            otherVisible,
            new ProfileUpdate(null, null, null,
                new Audience(AudienceMode.Any, [
                    new AudienceGrant(GrantKind.User, owner),
                    new AudienceGrant(GrantKind.User, other),
                    new AudienceGrant(GrantKind.User, "u-m23-find-t5-shared")]),
                null, null, null, null));

        // Owner's read: sees BOTH their own (owner branch) + the shared
        // one (granted). The owner's own profile is the one the
        // other-viewer read cannot surface.
        var ownerPage = await svc.FindPeopleByBioAsync("solar", owner, 1);
        var ownerIds = ownerPage.Profiles.Select(p => p.SubjectId).ToHashSet();
        Assert.Contains(owner, ownerIds);   // the owner branch (F3)
        Assert.Contains("u-m23-find-t5-shared", ownerIds);

        // Other viewer's read: sees only the shared one — the owner's
        // self-only profile never surfaces to them (D6 / C-M23·1).
        var otherPage = await svc.FindPeopleByBioAsync("solar", other, 1);
        var otherIds = otherPage.Profiles.Select(p => p.SubjectId).ToHashSet();
        Assert.Contains("u-m23-find-t5-shared", otherIds);
        Assert.DoesNotContain(owner, otherIds);
    }

    // ── Test 6 — HasMore paging (ADR 0090 D6 idiom) ────────────────────────
    //
    // Seed 31 visible profiles whose Bio all match the query, plus one
    // <c>Blocked</c> profile that also matches (the candidate filter —
    // the M2 <c>DirectoryService.ListAsync</c> shape — excludes it, a
    // suspended account has no public presence). Page 1 returns 30 with
    // <c>HasMore = true</c>; page 2 returns the remaining 1 with
    // <c>HasMore = false</c>.
    [Fact]
    public async Task Paging_page1_full_then_page2_remainder()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var svc = new ProfileFindService(userInfo, authz, store);

        const string viewer = "u-m23-find-t6-viewer";

        // 31 visible profiles, all Bio-matching "gardening", all granted to
        // the viewer. Stored directly (the shape round-trips — U01's proof)
        // to avoid 31 UpsertProfileAsync calls.
        for (var i = 0; i < 31; i++)
        {
            var subject = $"u-m23-find-t6-p{i:00}";
            await using var session = store.OpenSession(new Marten.Services.SessionOptions());
            session.Store(new Profile
            {
                SubjectId = subject,
                DisplayName = "Resident " + i,
                Verified = true,
                Bio = "Keeps a gardening shed at home.",
                Visibility = new Audience(AudienceMode.Any, [
                    new AudienceGrant(GrantKind.User, viewer),
                    new AudienceGrant(GrantKind.User, subject)]),
            });
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // One <b>blocked</b> profile whose Bio also matches — the candidate
        // filter (the M2 DirectoryService.ListAsync shape) must exclude it
        // from the candidate set (a suspended account has no public presence).
        await using (var blockedSession = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            blockedSession.Store(new Profile
            {
                SubjectId = "u-m23-find-t6-blocked",
                DisplayName = "Blocked",
                Verified = true,
                Blocked = true,
                Bio = "Also gardening.",
            });
            await blockedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Page 1: 30 visible, HasMore true (the 31st is on page 2; the
        // blocked one is filtered out of the candidate set).
        var page1 = await svc.FindPeopleByBioAsync("gardening", viewer, 1);
        Assert.Equal(30, page1.Profiles.Count);
        Assert.True(page1.HasMore);

        // Page 2: the remaining 1, HasMore false.
        var page2 = await svc.FindPeopleByBioAsync("gardening", viewer, 2);
        Assert.Single(page2.Profiles);
        Assert.False(page2.HasMore);

        // The blocked profile never surfaces (the M2 candidate-filter pin).
        var allIds = page1.Profiles.Concat(page2.Profiles)
            .Select(p => p.SubjectId).ToHashSet();
        Assert.DoesNotContain("u-m23-find-t6-blocked", allIds);
    }

    // ── Test 7 — zero new authorization surface (GATE-4) ───────────────────
    //
    // Pin that the authorization seam's surface is <b>unchanged</b> by M23:
    // the IAuthorizationService method set is the exact frozen ADR 0006 set
    // (CanAsync ×2, CanSeeAsync ×2, CanSeeGroupAsync ×2,
    // CanSeeGroupFeedAsync ×2 = 8 methods), and the
    // <see cref="ProfileToAuditableResource"/> adapter still projects the
    // frozen six-member shape (Id/Name/OwnerId/Audience/ComponentId/
    // TargetKind) with <c>TargetKind = "directory"</c> (the C-M23·2 / D7
    // pin — a new method / adapter / TargetKind would be a §drift-guard
    // stop). Pure in-memory; no store.
    [Fact]
    public void Authorization_seam_surface_unchanged()
    {
        // (a) IAuthorizationService's method set is the exact frozen ADR 0006
        // surface — M23 added zero methods (the C-M23·2 / D7 pin).
        var methodNames = typeof(IAuthorizationService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(
            new[]
            {
                "CanAsync", "CanAsync",
                "CanSeeAsync", "CanSeeAsync",
                "CanSeeGroupAsync", "CanSeeGroupAsync",
                "CanSeeGroupFeedAsync", "CanSeeGroupFeedAsync",
            },
            methodNames);

        // (b) The existing ProfileToAuditableResource (the adapter the
        //     find-people gate + the detail gate both reuse) still projects
        //     exactly the frozen six-member shape — nothing new was added to
        //     the adapter by M23 (C-M23·2).
        var visibility = new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, "u-member")]);
        var profile = new Profile
        {
            SubjectId = "u-m23-find-t7",
            DisplayName = "Seam",
            Verified = true,
            Visibility = visibility,
            ContactVisibility = new Audience(),
        };
        var adapter = new ProfileToAuditableResource(profile);
        Assert.Equal("u-m23-find-t7", adapter.Id);
        Assert.Equal("Seam", adapter.Name);
        Assert.Equal("u-m23-find-t7", adapter.OwnerId);
        Assert.Same(visibility, adapter.Audience);   // exactly Visibility, not ContactVisibility
        Assert.Null(adapter.ComponentId);
        Assert.Equal("directory", adapter.TargetKind);
    }

    // ── Shared helpers ─────────────────────────────────────────────────────

    /// <summary>Count of <see cref="AccessAudit"/> rows in the store (the
    /// "no row" shape assertions — the M3 0-candidate / M8 "no decision, no
    /// row" pin).</summary>
    private static Task<int> AuditCount(IDocumentStore store, CancellationToken ct)
        => store.QuerySession().Query<AccessAudit>().CountAsync(ct);

    /// <summary>One scratch Postgres DB + a <c>mt</c> schema over the M1 docs
    /// (the <see cref="Profile"/> doc) <b>plus</b> <see cref="TagDocTypes"/>
    /// (the by-tag read resolves a <see cref="Kumunita.Core.Tags.Tag"/> doc
    /// the M1-only boot would not know). Mirrors
    /// <see cref="ProfileBioTagsWriteLaneTests.BootStoreAsync"/> in this
    /// assembly.</summary>
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
            TagDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }
}
