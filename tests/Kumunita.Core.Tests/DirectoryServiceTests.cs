using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M2, plan U5 — <see cref="DirectoryService"/> self-check (the *one* test U5 owns; the
/// full 5-test seam list §2.5 is U6's).
/// <para>
/// <see cref="ListAsync_Hides_Unverified"/> pins invariant C-M2·2 (candidate filter ≠
/// access decision, §4.3/§2.3) at the unit level: an unverified viewer's
/// <see cref="DirectoryService.ListAsync"/> result must be exactly their own
/// <see cref="Profile"/> (via the Owner branch, the sole Allow) and nothing else — a
/// verified resident's profile, even if planted in the same store, never appears in the
/// result's <c>Visible</c> *or* <c>HiddenCount</c> (it was excluded by the §2.3 filter
/// before any <see cref="IAuthorizationService"/> call ran), and no <see cref="AccessAudit"/>
/// row anywhere names that other resident (their <c>SubjectId</c>, as actor, as
/// effective principal, or as target).
/// </para>
/// </summary>
public class DirectoryServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task ListAsync_Hides_Unverified()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var svc = new DirectoryService(userInfo, authz);

        // Two residents in the same store: one verified, one unverified (the viewer).
        const string verifiedOther = "u-dir-verified-other";
        const string unverifiedViewer = "u-dir-unverified-viewer";

        var verifiedProfile = new Profile
        {
            SubjectId = verifiedOther,
            DisplayName = "Verified Other",
            Verified = true,
            Visibility = new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, verifiedOther)]),
        };
        var unverifiedProfile = new Profile
        {
            SubjectId = unverifiedViewer,
            DisplayName = "Unverified Viewer",
            Verified = false,
            // Deliberately self-granted (an audience *that* would Allow the owner):
            // if the test asserted on Visibility evaluation alone (not the §2.3 filter),
            // this shape still lands on the Owner branch (which wins first, §4.4
            // branch 1) — the test's assertion is about *which* profile is even
            // *present* to be evaluated, not about this audience's shape.
            Visibility = new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, unverifiedViewer)]),
        };

        // Bootstrap both through the service's own single write seam (C3's write lane),
        // then reset to zero rows for the audit assertion below (UpsertProfileAsync
        // itself appends no Audit row — M1's bootstrap surface — so this is a
        // well-defined starting count of 0 for the DirectoryService decision rows).
        await userInfo.UpsertProfileAsync(verifiedProfile, new ProfileUpdate(null, null, null, null, null));
        await userInfo.UpsertProfileAsync(unverifiedProfile, new ProfileUpdate(null, null, null, null, null));

        // The §2.3 unverified row: exactly one candidate — the viewer themself —
        // survives the filter in DirectoryService.ListAsync before CanSeeAsync runs.
        var result = await svc.ListAsync(unverifiedViewer, viewerVerified: false);

        // Exactly the viewer's own profile; the verified resident never appears — not
        // in Visible, not in HiddenCount (excluded *before* any decision ran, C-M2·2).
        Assert.Single(result.Visible);
        Assert.Equal(unverifiedViewer, result.Visible[0].SubjectId);
        Assert.Equal(0, result.HiddenCount);

        // The single decision (Owner branch, Read on the viewer's own profile) is
        // audited: one aggregate row + one per-item (audience-restricted) row for that
        // same profile — C3's two-row shape applied to a 1-candidate set — and *nothing
        // else* anywhere names the verified resident (C-M2·2's "never logged as an
        // access decision" pin, at the DB level, not just the return shape).
        await using var session = store.QuerySession();
        var allAudits = await session.Query<AccessAudit>()
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, allAudits.Count);

        var aggregate = allAudits.Single(a => a.TargetId is null);
        Assert.Equal("directory", aggregate.TargetKind);
        Assert.Equal(AccessAction.Read.Id, aggregate.Action);
        Assert.Equal(unverifiedViewer, aggregate.ActorId);
        Assert.Equal(1, aggregate.VisibleCount);
        Assert.Equal(0, aggregate.HiddenCount);
        Assert.Equal(AccessVia.Owner, aggregate.Via);
        Assert.Equal(AccessOutcome.Allow, aggregate.Outcome);

        var perItem = allAudits.Single(a => a.TargetId is not null);
        Assert.Equal(unverifiedViewer, perItem.TargetId);
        Assert.Equal(unverifiedViewer, perItem.ActorId);
        Assert.Equal(AccessVia.Owner, perItem.Via);
        Assert.Equal(AccessOutcome.Allow, perItem.Outcome);

        // C-M2·2 — the verified resident's SubjectId appears nowhere: not as actor,
        // not as effective principal, not as target — evidence the §2.3 filter excluded
        // them before CanSeeAsync could name them in any row.
        Assert.All(allAudits, a =>
        {
            Assert.NotEqual(verifiedOther, a.ActorId);
            Assert.NotEqual(verifiedOther, a.EffectivePrincipalId);
            Assert.NotEqual(verifiedOther, a.TargetId);
        });
    }

    // ── Shared bootstrap: store + connection string + services ──────────
    // Same shape as AuthorizationServiceTests.BootAsync / UserInfoServiceTests.BootStoreAsync.

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
