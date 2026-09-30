using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M19 (ADR 0120, D2) — the guest standing seam. The guest is a *standing on
/// the account* (D1), not a new entity: a real <see cref="Profile"/> gains the
/// additive <see cref="Profile.IsGuest"/> flag, and the admin-settled,
/// time-bounded allowance lives in the <see cref="GuestAccess"/> document
/// (id = the guest's <c>SubjectId</c>). The <b>single</b> audited write lane is
/// <see cref="IdentityService.SetGuestAccessAsync"/> (exactly one
/// <see cref="AccessAudit"/> row, <c>Via = Admin</c>, action
/// <c>guest.set-standing</c>, <c>TargetKind</c> "guest", <c>TargetId</c>
/// "guest:{subjectId}" — C-M19·5); the read seam
/// <see cref="IdentityService.GetGuestAccessAsync"/> returns the document or
/// <c>null</c> (the C-M19·4 empty floor — a guest with no settled standing is
/// a shell, not an error).
/// <para>
/// Pinned the same way the ADR 0050 signup-gate lane is (via
/// <see cref="AccessAudit"/> shape + the live-on-next-read invariant), over a
/// fresh scratch Postgres (the <see cref="PostgresFixture"/> harness).
/// <c>IdentityService</c> is constructed with substitutes for the seams the
/// guest lane does not touch (only <c>documentStore</c> is the real one).
/// </para>
/// </summary>
public class GuestAccessSeamTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── GATE-3 — the allowance write lane is single + audited ─────────────

    [Fact]
    public async Task SetGuestAccess_Stores_One_Document_And_One_Audit_Row()
    {
        var (svc, store) = await BuildAsync();

        const string guestId = "guest-19";
        const string adminId = "admin-19";

        // The guest is a real account: seed its Profile so the write lane's
        // D1 flag flip (Profile.IsGuest = true) has a row to set.
        await using (var seed = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            seed.Store(new Profile
            {
                SubjectId = guestId,
                DisplayName = "A Consultant",
                Verified = true
            });
            await seed.SaveChangesAsync();
        }

        // One call to the single write lane (GATE-3) settles the standing.
        var now = DateTimeOffset.UtcNow;
        await svc.SetGuestAccessAsync(new GuestAccess
        {
            SubjectId = guestId,
            ValidFrom = now.AddDays(-1),
            ValidUntil = now.AddDays(1),
            AllowedSurfaces = GuestSurface.Announcements | GuestSurface.Directory
        }, adminId);

        // The document is stored (id = the guest's SubjectId) and is live on the
        // very next read (D3: data, not config).
        var reloaded = await svc.GetGuestAccessAsync(guestId);
        Assert.NotNull(reloaded);
        Assert.Equal(guestId, reloaded!.SubjectId);
        Assert.Equal(GuestSurface.Announcements | GuestSurface.Directory, reloaded.AllowedSurfaces);
        Assert.Equal(adminId, reloaded.SetByAdmin);
        Assert.Equal(now.AddDays(-1), reloaded.ValidFrom);
        Assert.Equal(now.AddDays(1), reloaded.ValidUntil);

        // The D1 flag flipped on the same row (the guest is a standing, C-M19·1).
        using var read = store.QuerySession();
        var profile = await read.LoadAsync<Profile>(guestId, TestContext.Current.CancellationToken);
        Assert.NotNull(profile);
        Assert.True(profile!.IsGuest);

        // Exactly one audit row (the single write), the ADR 0050 shape
        // (Via Admin, Outcome Allow), the guest-standing target (TargetKind
        // "guest", TargetId "guest:{subjectId}", action "guest.set-standing").
        var audits = await AuditRows(store, action: "guest.set-standing");
        Assert.Single(audits);
        var row = audits[0];
        Assert.Equal("guest.set-standing", row.Action);
        Assert.Equal("guest", row.TargetKind);
        Assert.Equal($"guest:{guestId}", row.TargetId);
        Assert.Equal(AccessVia.Admin, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
        Assert.Equal(adminId, row.ActorId);
        // A guest-standing write is not an access change — no VisibilityCount
        // (the single-target shape, ADR 0006 §B).
        Assert.Null(row.VisibleCount);
        Assert.Null(row.HiddenCount);

        // No stray audit rows from the seed — the lane is exactly one row.
        var allAudits = await AuditRows(store);
        Assert.Single(allAudits);
    }

    // ── C-M19·4 — the closed surface set floors to nothing ────────────────

    [Fact]
    public async Task GetGuestAccess_With_No_Allowance_Returns_Null()
    {
        var (svc, _) = await BuildAsync();

        // A subject with no settled standing yet: the read seam returns null —
        // the empty floor is a valid, least-privileged state, not an error
        // (C-M19·4, GATE-2 floor).
        Assert.Null(await svc.GetGuestAccessAsync("no-standing-guest"));
    }

    [Fact]
    public void GuestSurface_Closed_Set_Typo_Grants_Nothing()
    {
        // The named surfaces are distinct, non-zero, mutually exclusive bits.
        Assert.Equal(GuestSurface.None, (GuestSurface)0);
        Assert.Equal(GuestSurface.Announcements, (GuestSurface)1);
        Assert.Equal(GuestSurface.Events, (GuestSurface)2);
        Assert.Equal(GuestSurface.Directory, (GuestSurface)4);

        // The closed union (D4) is exactly the defined bits {1, 2, 4} — the full
        // set composes to 7, and a *new* surface is a new, higher bit value (the
        // ADR 0030 append precedent), never a free-form string.
        const int DefinedUnion = 1 | 2 | 4;   // == (int)(Announcements | Events | Directory)
        Assert.Equal(DefinedUnion, (int)(GuestSurface.Announcements | GuestSurface.Events | GuestSurface.Directory));

        // The closed-set guarantee (C-M19·4 / D4): a value made up ONLY of bits
        // outside the defined union grants no named surface. Bit 3 (8) and bit
        // 7 (0x80) are undefined — a "typo" / unknown surface. Neither includes
        // any of the named surfaces (HasFlag is a pure bit test, so an
        // out-of-union bit cannot silently carry a named surface).
        var unknownSurface = (GuestSurface)8;            // bit 3, undefined
        Assert.False(unknownSurface.HasFlag(GuestSurface.Announcements));
        Assert.False(unknownSurface.HasFlag(GuestSurface.Events));
        Assert.False(unknownSurface.HasFlag(GuestSurface.Directory));

        var farUnknown = (GuestSurface)0x80;            // bit 7, undefined
        Assert.False(farUnknown.HasFlag(GuestSurface.Announcements));
        Assert.False(farUnknown.HasFlag(GuestSurface.Events));
        Assert.False(farUnknown.HasFlag(GuestSurface.Directory));

        // None is the empty floor (C-M19·4): a guest with GuestSurface.None signs
        // in to a shell and grants nothing — a valid, least-privileged state,
        // not an error.
        Assert.False(GuestSurface.None.HasFlag(GuestSurface.Announcements));
        Assert.False(GuestSurface.None.HasFlag(GuestSurface.Events));
        Assert.False(GuestSurface.None.HasFlag(GuestSurface.Directory));
    }

    // ── Harness ───────────────────────────────────────────────────────────

    /// <summary>
    /// A real Marten store over a fresh scratch Postgres (the
    /// <see cref="PostgresFixture"/> harness) + an <see cref="IdentityService"/>
    /// wired with substitutes for the seams the guest lane does not touch (only
    /// <c>documentStore</c> is the real one).
    /// </summary>
    private async Task<(IdentityService svc, IDocumentStore store)> BuildAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
            M3DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);

        // The guest lane's two seams touch only `documentStore`; the other ctor
        // seams are unused (UserManager / IUserInfoService / IClaimsSource /
        // IMailerStage are not invoked by GetGuestAccessAsync /
        // SetGuestAccessAsync). UserManager<T> is a concrete class (NSubstitute's
        // Castle proxy needs a parameterless ctor it lacks), so pass null — the
        // null reference is never dereferenced.
        var svc = new IdentityService(
            userManager:          null!,
            documentStore:        store,
            userInfo:             Substitute.For<IUserInfoService>(),
            claimsSource:         Substitute.For<IClaimsSource>(),
            mailer:               Substitute.For<IMailerStage>(),
            verificationOptions:  Options.Create(new VerificationOptions()),
            logger:               Substitute.For<ILogger<IdentityService>>());

        return (svc, store);
    }

    private static async Task<IReadOnlyList<AccessAudit>> AuditRows(
        IDocumentStore store, string? action = null)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.QuerySession();
        System.Linq.IQueryable<AccessAudit> query = session.Query<AccessAudit>();
        if (action is not null)
            query = query.Where(a => a.Action == action);
        return await query.OrderBy(a => a.At).ToListAsync(ct);
    }
}
