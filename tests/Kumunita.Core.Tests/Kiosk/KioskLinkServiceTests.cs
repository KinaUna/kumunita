using Kumunita.Core.Authorization;
using Kumunita.Core.Kiosk;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// ADR 0302 (IMPROVE-UX R15) — the <see cref="KioskLinkService"/> pins: the
/// shared kiosk link's Core seam. The unit card's "Done when" (verbatim):
/// "tests: revoked/unknown token refused, hash only at rest."
/// <list type="bullet">
/// <item>(a) a **revoked / unknown** token is refused —
/// <see cref="KioskLinkService.ValidateAsync"/> returns <c>null</c>.</item>
/// <item>(b) the **stored value is a hash, never the raw token** (the
/// "hash only at rest" pin — the 0159 ESC·3 / F2 rule).</item>
/// <item>(c) <see cref="KioskLinkService.CreateAsync"/> returns the raw
/// token **exactly once** — the store holds only the digest, so the raw
/// token is unrecoverable from the store.</item>
/// <item>(d) an owner's <see cref="KioskLinkService.ValidateAsync"/> for
/// **another owner's** link is refused (the owner-scope pin — the hash
/// match alone is not sufficient).</item>
/// </list>
/// Plus the audit pins: exactly one <c>kiosk.link.create</c> row per create
/// and one <c>kiosk.link.revoke</c> row per revoke (<c>Via = Owner</c>,
/// <c>TargetKind = "kiosk"</c> — the <see cref="KioskService"/>
/// <c>kiosk.save</c> one-row convention), and **zero** rows on a validate
/// (the M35·5 "the read is a config load, not an access decision" floor)
/// with <see cref="KioskLink.LastUsedAt"/> stamped on a match.
/// <para>
/// Harness: the <see cref="PostgresFixture"/> (Testcontainers
/// <c>postgres:18</c>) fresh-scratch-database-per-test shape (the
/// <see cref="KioskServiceTests"/> precedent). The store registers the
/// <see cref="KioskLink"/> + <see cref="KioskPlaylist"/> (the
/// <see cref="KioskDocTypes"/> / <see cref="Kumunita.Core
/// .CoreDocTypes"/> surfaces) + <see cref="AccessAudit"/> (the
/// <see cref="Kumunita.Core.CoreDocTypes"/> surface).
/// </para>
/// </summary>
public sealed class KioskLinkServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "kiosk-link-owner-001";
    private const string Other = "kiosk-link-other-002";

    // ── Pin (c) — CreateAsync returns the raw token exactly once ─────────

    [Fact]
    public async Task ADR0302_Create_ReturnsRawTokenOnce_AndStoresRow()
    {
        var store = await BootStoreAsync();
        var svc = new KioskLinkService(store);
        var ct = TestContext.Current.CancellationToken;

        var (token, row) = await svc.CreateAsync(Owner, Owner, ct);

        // The raw token is returned (the caller shows it, once).
        Assert.False(string.IsNullOrEmpty(token));
        Assert.StartsWith("kskl_", token);   // the ADR 0302 D2 format prefix
        // The row is returned and is the same row the store now holds.
        Assert.False(string.IsNullOrEmpty(row.Id));
        Assert.Equal(Owner, row.OwnerSubjectId);
        Assert.Equal(Owner, row.PlaylistId);

        // Re-created links are distinct rows (one row per link, ADR 0302 D1).
        var (token2, row2) = await svc.CreateAsync(Owner, Owner, ct);
        Assert.NotEqual(row.Id, row2.Id);
        await store.DisposeAsync();
    }

    // ── Pin (b) — hash only at rest ───────────────────────────────────────

    [Fact]
    public async Task ADR0302_Store_HoldsHashNotRawToken()
    {
        var store = await BootStoreAsync();
        var svc = new KioskLinkService(store);
        var ct = TestContext.Current.CancellationToken;

        var (token, row) = await svc.CreateAsync(Owner, Owner, ct);

        // The stored value is the SHA-256 hex digest (64 lowercase hex
        // chars — the IdentityService.Sha256Hex shape), NEVER the raw token
        // (the 0159 ESC·3 / F2 hash-at-rest rule, ADR 0302 D2).
        Assert.NotEqual(token, row.TokenHash);
        Assert.Equal(64, row.TokenHash.Length);
        Assert.Matches("^[0-9a-f]{64}$", row.TokenHash);
        // The stored digest is exactly the SHA-256 hex of the raw token
        // (the IdentityService.Sha256Hex algorithm — the test-local helper
        // mirrors it: same input, same output).
        Assert.Equal(TestSha256Hex(token), row.TokenHash);

        // And the whole row's persisted shape carries no raw token: the
        // raw token appears in NONE of the row's members (the unrecoverable
        // pin — a DB dump of this row cannot yield the credential).
        Assert.DoesNotContain(token, row.Id);
        Assert.DoesNotContain(token, row.PlaylistId);
        Assert.DoesNotContain(token, row.OwnerSubjectId);
        Assert.DoesNotContain(token, row.TokenHash);
        await store.DisposeAsync();
    }

    // ── Pin (d) — owner scope: another owner's link is refused ────────────

    [Fact]
    public async Task ADR0302_Validate_AnotherOwnersLink_IsRefused()
    {
        var store = await BootStoreAsync();
        var svc = new KioskLinkService(store);
        var ct = TestContext.Current.CancellationToken;

        var (token, _) = await svc.CreateAsync(Owner, Owner, ct);
        var hash = TestSha256Hex(token);

        // The same (valid) hash presented against ANOTHER owner's scope is
        // refused — the hash match alone is not sufficient (ADR 0302 D3
        // pin, test (d)).
        Assert.Null(await svc.ValidateAsync(hash, Other, ct));

        // ...while the OWNING scope validates (the control half).
        Assert.NotNull(await svc.ValidateAsync(hash, Owner, ct));
        await store.DisposeAsync();
    }

    // ── Pin (a) — unknown token refused ───────────────────────────────────

    [Fact]
    public async Task ADR0302_Validate_UnknownToken_IsRefused()
    {
        var store = await BootStoreAsync();
        var svc = new KioskLinkService(store);
        var ct = TestContext.Current.CancellationToken;

        // A hash that matches no stored row (a random 256-bit token) is
        // refused for any owner (the absent-row case).
        var unknown = TestSha256Hex("never-created-token");
        Assert.Null(await svc.ValidateAsync(unknown, Owner, ct));
        await store.DisposeAsync();
    }

    // ── Pin (a) — revoked token refused ───────────────────────────────────

    [Fact]
    public async Task ADR0302_Validate_RevokedToken_IsRefused()
    {
        var store = await BootStoreAsync();
        var svc = new KioskLinkService(store);
        var ct = TestContext.Current.CancellationToken;

        var (token, row) = await svc.CreateAsync(Owner, Owner, ct);
        var hash = TestSha256Hex(token);

        // Control: it validates while live.
        Assert.NotNull(await svc.ValidateAsync(hash, Owner, ct));

        // Revoke (the owner's own link — allowed).
        Assert.True(await svc.RevokeAsync(row.Id, Owner, ct));

        // ...and is refused from now on (the revoked case).
        Assert.Null(await svc.ValidateAsync(hash, Owner, ct));
        await store.DisposeAsync();
    }

    // ── Pin (a)+(d) — the full matrix on one created link ─────────────────

    [Fact]
    public async Task ADR0302_Validate_MatchesOnlyValidOwnLink()
    {
        var store = await BootStoreAsync();
        var svc = new KioskLinkService(store);
        var ct = TestContext.Current.CancellationToken;

        // Two links coexist for the same owner (the R18 list surface will
        // show both); each validates on its own hash and scope only.
        var (a, rowA) = await svc.CreateAsync(Owner, Owner, ct);
        var (b, rowB) = await svc.CreateAsync(Owner, Owner, ct);
        var hashA = TestSha256Hex(a);
        var hashB = TestSha256Hex(b);

        Assert.Equal(rowA.Id, (await svc.ValidateAsync(hashA, Owner, ct))!.Id);
        Assert.Equal(rowB.Id, (await svc.ValidateAsync(hashB, Owner, ct))!.Id);

        // Cross-scope refusals hold for every combination.
        Assert.Null(await svc.ValidateAsync(hashA, Other, ct));
        Assert.Null(await svc.ValidateAsync(hashB, Other, ct));
        // Revoking one leaves the other valid (per-link revocation, the
        // EscalationTokenService ESC·9 shape).
        Assert.True(await svc.RevokeAsync(rowA.Id, Owner, ct));
        Assert.Null(await svc.ValidateAsync(hashA, Owner, ct));
        Assert.NotNull(await svc.ValidateAsync(hashB, Owner, ct));
        await store.DisposeAsync();
    }

    // ── Revoke — owner-scoped, idempotent, audited ────────────────────────

    [Fact]
    public async Task ADR0302_Revoke_OwnerScoped_Idempotent_Audited()
    {
        var store = await BootStoreAsync();
        var svc = new KioskLinkService(store);
        var ct = TestContext.Current.CancellationToken;

        var (_, row) = await svc.CreateAsync(Owner, Owner, ct);

        // A non-owner cannot revoke (owner-scope pin, ADR 0302 D3).
        Assert.False(await svc.RevokeAsync(row.Id, Other, ct));

        // The owner's revoke succeeds once...
        Assert.True(await svc.RevokeAsync(row.Id, Owner, ct));
        // ...and is idempotent (already revoked → no-op, the 0159 ESC·9
        // shape).
        Assert.False(await svc.RevokeAsync(row.Id, Owner, ct));
        // An unknown link is a no-op (the missing-row case).
        Assert.False(await svc.RevokeAsync("no-such-link", Owner, ct));

        // Audit: exactly ONE create row + exactly ONE revoke row (the
        // no-op revokes wrote nothing — the ESC·9 no-audit-on-no-op pin).
        var rows = await AllAudits(store);
        Assert.Equal(2, rows.Count);
        Assert.Equal("kiosk.link.create", rows[0].Action);
        Assert.Equal("kiosk.link.revoke", rows[1].Action);
        foreach (var r in rows)
        {
            Assert.Equal(Owner, r.ActorId);
            Assert.Equal("kiosk", r.TargetKind);
            Assert.Equal(row.Id, r.TargetId);
            Assert.Equal(AccessVia.Owner, r.Via);
            Assert.Equal(AccessOutcome.Allow, r.Outcome);
        }
        await store.DisposeAsync();
    }

    // ── Validate — zero audit rows, LastUsedAt stamped ────────────────────

    [Fact]
    public async Task ADR0302_Validate_WritesNoAuditRow_StampedLastUsed()
    {
        var store = await BootStoreAsync();
        var svc = new KioskLinkService(store);
        var ct = TestContext.Current.CancellationToken;

        var (token, row) = await svc.CreateAsync(Owner, Owner, ct);
        var hash = TestSha256Hex(token);

        Assert.Null(row.LastUsedAt);   // null until first use (the D1 shape)

        // The validate is the M35·5 "zero audit on the read" lane...
        var result = await svc.ValidateAsync(hash, Owner, ct);
        Assert.NotNull(result);
        var all = await AllAudits(store);
        Assert.Single(all);                    // only the create row
        Assert.Equal("kiosk.link.create", all[0].Action);

        // ...and stamps LastUsedAt (the ESC·9 read-then-write, no audit
        // row — the "last used shown" invariant, ADR 0302 D4). It is
        // stamped at the validate instant, which is >= the CreatedAt.
        Assert.NotNull(result!.LastUsedAt);
        Assert.True(result.LastUsedAt! >= row.CreatedAt);

        // A refused validate (unknown token) stamps nothing on anything.
        await svc.ValidateAsync(TestSha256Hex("never-created-token"), Owner, ct);
        Assert.Equal(1, (await AllAudits(store)).Count);
        await store.DisposeAsync();
    }

    // ── R16 — the presented-token overload and the hash-based seam agree ──
    // Trap A: the hashing lives in ONE place (IdentityService.Sha256Hex,
    // internal to Core). The R16 middleware hashes the presented token via
    // ValidatePresentedAsync and must agree with a hash-based ValidateAsync
    // lookup on the SAME token — a drift between the two would be a
    // divergence risk the "the hash is one fact" invariant forbids (the
    // EscalationTokenService precedent this ADR names as its template).

    [Fact]
    public async Task ADR0302_R16_PresentedOverload_AgreesWithHashBasedValidate()
    {
        var store = await BootStoreAsync();
        var svc = new KioskLinkService(store);
        var ct = TestContext.Current.CancellationToken;

        var (token, row) = await svc.CreateAsync(Owner, Owner, ct);
        var hash = TestSha256Hex(token);   // the test-local mirror of Sha256Hex

        // The presented-token overload (owner-less — the wall-screen flow)
        // returns the SAME row the hash-based seam returns for the OWNING
        // scope (the two paths must not diverge — the one-fact invariant).
        var presented = await svc.ValidatePresentedAsync(token, ct);
        Assert.NotNull(presented);
        Assert.Equal(row.Id, presented!.Id);
        Assert.Equal(Owner, presented.OwnerSubjectId);
        Assert.Equal(row.Id, (await svc.ValidateAsync(hash, Owner, ct))!.Id);
        await store.DisposeAsync();
    }

    [Fact]
    public async Task ADR0302_R16_PresentedOverload_RefusesBlankRevokedAndUnknown()
    {
        var store = await BootStoreAsync();
        var svc = new KioskLinkService(store);
        var ct = TestContext.Current.CancellationToken;

        // Blank / empty presented tokens refuse (the ESC·5 guard — no session,
        // no store hit).
        Assert.Null(await svc.ValidatePresentedAsync(string.Empty, ct));
        Assert.Null(await svc.ValidatePresentedAsync("   ", ct));

        // Unknown token refuses (no matching row).
        Assert.Null(await svc.ValidatePresentedAsync("never-created-token", ct));

        // Revoked token refuses (the revoked case, owner-less).
        var (token, row) = await svc.CreateAsync(Owner, Owner, ct);
        Assert.NotNull(await svc.ValidatePresentedAsync(token, ct));   // valid while live
        Assert.True(await svc.RevokeAsync(row.Id, Owner, ct));
        Assert.Null(await svc.ValidatePresentedAsync(token, ct));      // refused after revoke
        await store.DisposeAsync();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Boot a fresh <c>mt</c> schema in a new scratch database — the
    /// doc types these pins read: <see cref="KioskLink"/> +
    /// <see cref="KioskPlaylist"/> (the kiosk surfaces) +
    /// <see cref="AccessAudit"/> (the audit-count assertions query it).</summary>
    private async Task<IDocumentStore> BootStoreAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            Kumunita.Core.KioskDocTypes.Configure(opts);   // KioskPlaylist
            Kumunita.Core.CoreDocTypes.Configure(opts);          // KioskLink + AccessAudit
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);
        return store;
    }

    /// <summary>The whole <see cref="AccessAudit"/> table (unsorted — the
    /// test inspects).</summary>
    private static async Task<IReadOnlyList<AccessAudit>> AllAudits(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        using var session = store.QuerySession();
        return await session.Query<AccessAudit>().ToListAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Test-local SHA-256 hex helper (the <see cref="Kumunita.Core
    /// .Identity.IdentityService.Sha256Hex"/> algorithm — same input, same
    /// output). <c>IdentityService.Sha256Hex</c> is <c>internal</c> to the
    /// Core assembly; the test project cannot see it. The helper mirrors the
    /// repo's <c>TestLocalSha256Hex</c> precedent in
    /// <c>IdentityServiceTokenHashTests</c>.
    /// </summary>
    private static string TestSha256Hex(string value) =>
        Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(value)))
        .ToLowerInvariant();   // exactly 64 lowercase hex chars
}
