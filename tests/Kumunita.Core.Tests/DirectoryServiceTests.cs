using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M2 — <see cref="DirectoryService"/> self-check (the "list" side; the detail/preview
/// gates are <c>DirectoryServiceTests_U6</c>).
/// <para>
/// <see cref="ListAsync_Shows_All_NonBlock_Residents_to_Any_SignedIn_Viewer"/> pins the
/// directory's <b>show-everyone</b> product rule at the unit level: the platform is
/// invitation-only and limited to residents, so <c>ListAsync</c> returns <b>every</b>
/// non-blocked <see cref="Profile"/> for <b>any</b> signed-in viewer — an unverified
/// viewer's result is not narrowed to their own row (the old §2.3 candidate filter is
/// gone), and a verified resident's profile appears for them too. <c>Profile.Blocked</c>
/// remains the only account-level exclusion (a suspended resident is not listed). The
/// list is a <b>pure catalog read</b>: <c>ListAsync</c> runs no
/// <see cref="IAuthorizationService"/> decision, so <b>no</b> <see cref="AccessAudit"/>
/// row is written at all. An unauthenticated (empty) subject short-circuits to an empty
/// list (F8 boundary).
/// </para>
/// </summary>
public class DirectoryServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task ListAsync_Shows_All_NonBlock_Residents_to_Any_SignedIn_Viewer()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var svc = new DirectoryService(userInfo, authz);

        var verifiedOther = "u-dir-verified-other";
        var unverifiedViewer = "u-dir-unverified-viewer";
        var blockedResident = "u-dir-blocked-resident";

        // Three residents in the same store: two listed, one suspended. Note the listed
        // profiles use a Visibility that would have *denied* the other viewer under the
        // old two-gate design — the point of the test is that the listing no longer
        // consults that audience at all.
        var verifiedProfile = new Profile
        {
            SubjectId = verifiedOther,
            DisplayName = "Verified Other",
            Verified = true,
            // A self-only audience would have hidden this row from the unverified viewer
            // before the change; the new rule ignores it for the listing.
            Visibility = new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, verifiedOther)]),
        };
        var unverifiedProfile = new Profile
        {
            SubjectId = unverifiedViewer,
            DisplayName = "Unverified Viewer",
            Verified = false,
            Visibility = new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, unverifiedViewer)]),
        };
        var blockedProfile = new Profile
        {
            SubjectId = blockedResident,
            DisplayName = "Blocked Resident",
            Verified = true,
            Blocked = true, // suspended — never listed
        };

        // Bootstrap the two listed residents through the service's own single write
        // seam (UpsertProfileAsync appends no Audit row — M1's bootstrap surface — so
        // the audit count below starts well-defined at 0 for any DirectoryService
        // decision rows).
        await userInfo.UpsertProfileAsync(verifiedProfile, new ProfileUpdate(null, null, null, null, null));
        await userInfo.UpsertProfileAsync(unverifiedProfile, new ProfileUpdate(null, null, null, null, null));

        // The suspended resident: BlockAsync is the real admin lane (it needs
        // UserManager; pulling Identity infra into this test would obscure the pin).
        // Persist Profile.Blocked via a raw document-session set — the same
        // single-Store the admin lane itself does (SetBlockedAsync's core line:
        // profile.Blocked = true; session.Store(profile)).
        await using (var blockedSession = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            blockedSession.Store(blockedProfile);
            await blockedSession.SaveChangesAsync();
        }

        // (a) Unverified viewer: sees BOTH non-blocked residents (not just themselves),
        // never the blocked one.
        var unverifiedList = await svc.ListAsync(unverifiedViewer);
        var unverifiedIds = unverifiedList.Visible.Select(p => p.SubjectId).ToHashSet();
        Assert.Contains(verifiedOther, unverifiedIds);
        Assert.Contains(unverifiedViewer, unverifiedIds);
        Assert.DoesNotContain(blockedResident, unverifiedIds);
        Assert.Equal(2, unverifiedList.Visible.Count);

        // (b) Verified viewer: the same full non-blocked set (no narrowing either way).
        var verifiedList = await svc.ListAsync(verifiedOther);
        var verifiedIds = verifiedList.Visible.Select(p => p.SubjectId).ToHashSet();
        Assert.Contains(verifiedOther, verifiedIds);
        Assert.Contains(unverifiedViewer, verifiedIds);
        Assert.DoesNotContain(blockedResident, verifiedIds);
        Assert.Equal(2, verifiedList.Visible.Count);

        // (c) Unauthenticated (empty subject): F8 boundary — empty list, fail closed.
        var anonymousList = await svc.ListAsync(string.Empty);
        Assert.Empty(anonymousList.Visible);

        // (d) The list ran NO IAuthorizationService decision: zero AccessAudit rows
        // anywhere (nothing to name any resident as actor/principal/target).
        await using var session = store.QuerySession();
        var allAudits = await session.Query<AccessAudit>()
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(allAudits);
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
