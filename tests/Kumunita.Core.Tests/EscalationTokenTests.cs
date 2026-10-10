using System.Security.Cryptography;
using System.Text;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.ErrorReports;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// ESC · U08 (ADR 0159) — the <see cref="EscalationTokenService"/> receiving-
/// token lane pins (ESC·3 / ESC·9 — design doc §2.4 tests 1–5, names
/// **verbatim**):
/// <list type="number">
/// <item><b>ESC_3_Token_Generate_Stores_Hash_Not_Plaintext</b> —
/// <see cref="IEscalationTokenService.GenerateAsync"/> stores a row whose
/// <c>TokenHash</c> is the SHA-256 of the returned plaintext, and the
/// <c>EscalationToken</c> doc carries **no plaintext member** (ESC·3 — the
/// plaintext is shown exactly once, never persisted; hash-only-at-rest) (FACES
/// ESC-1).</item>
/// <item><b>ESC_3_Token_Validate_Valid_Returns_Token</b> —
/// <see cref="IEscalationTokenService.ValidateAsync"/> with the correct
/// plaintext returns the matching non-revoked token (ESC·3) (FACES ESC-3).</item>
/// <item><b>ESC_3_Token_Validate_Invalid_Returns_Null</b> — a wrong plaintext
/// returns <c>null</c> (ESC·3 / ESC·5) (FACES ESC-4).</item>
/// <item><b>ESC_9_Token_Revoke_Immediate_NoValidate</b> — after
/// <see cref="IEscalationTokenService.RevokeAsync"/> the token **no longer
/// validates** (immediate, ESC·9) (FACES ESC-7).</item>
/// <item><b>ESC_9_Token_List_Excludes_Plaintext_And_Hash</b> —
/// <see cref="IEscalationTokenService.ListAsync"/> exposes only the
/// <see cref="EscalationTokenSummary"/> (the <c>TokenPrefix</c> masked form +
/// <c>Label</c> + status) — **no** plaintext, **no** <c>TokenHash</c>
/// (ESC·3 / ESC·9) (FACES ESC-1).</item>
/// </list>
/// </summary>
/// <para>
/// <b>Harness (the M32 <see cref="ErrorReportResolveTests"/> idiom):</b> every
/// <c>*DocTypes.Configure</c> call in this repo lives in the host's
/// <c>AddMarten</c> lambda, not in <c>DependencyInjection.cs</c>. This harness
/// wires the SAME <see cref="EscalationDocTypes.Configure"/> +
/// <see cref="Kumunita.Core.M1DocTypes.Configure"/> calls in its own
/// <c>DocumentStore.For</c> lambda — without it the <c>EscalationToken</c> doc
/// is invisible to Marten. <c>M1DocTypes.Configure</c> registers the
/// <c>AccessAudit</c> doc (the audit-row half of the
/// <see cref="EscalationTokenService"/> generate / revoke write lanes, the
/// ESC·9 pin).
/// </para>
public class EscalationTokenTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── 1 — generate stores the SHA-256 hash, never the plaintext (ESC·3) ──
    // FACES ESC-1: the receiving admin generates a labeled token → the
    // plaintext is shown **once** (the return value), the <c>EscalationToken</c>
    // row (hash) is stored, and the doc itself carries **no plaintext member**
    // (the hash is the only form the receiving side keeps, ESC·3).

    [Fact(DisplayName = "ESC_3_Token_Generate_Stores_Hash_Not_Plaintext")]
    public async Task ESC_3_Token_Generate_Stores_Hash_Not_Plaintext()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new EscalationTokenService(store);

        const string admin = "admin-esc-1";
        var (plaintext, stored) = await svc.GenerateAsync("Origin platform A", admin, ct);

        // The plaintext is returned (shown once — the ESC·3 pin) and is the
        // kesc_-prefixed CSPRNG format (the U03 NewToken shape).
        Assert.StartsWith("kesc_", plaintext);
        Assert.Equal("Origin platform A", stored.Label);

        // The stored row's TokenHash is exactly the SHA-256 of the plaintext
        // (the ESC·3 pin — the hash, not the plaintext, is at rest), and the
        // doc carries the generating admin's CreatedBy (the ESC·9 pin). The
        // summary read-model does NOT expose CreatedBy (the 7-member pin) —
        // the doc row does.
        await using var q = store.QuerySession();
        var row = await q.LoadAsync<EscalationToken>(stored.Id, ct);
        Assert.NotNull(row);
        Assert.Equal(Sha256Hex(plaintext), row!.TokenHash);
        Assert.Equal(admin, row.CreatedBy);
        // The masked prefix is the first 12 chars + the ellipsis (the U03
        // Summary shape) — the only token material the receiving side shows.
        Assert.StartsWith("kesc_", row.TokenPrefix);

        // The plaintext is NOT a doc member (ESC·3 — the plaintext is shown
        // once, never persisted). No property named "Plaintext" exists on the
        // doc; the only token-bearing members are the hash + the prefix.
        var propNames = typeof(EscalationToken).GetProperties()
            .Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("Plaintext", propNames);
        Assert.Contains("TokenHash", propNames);
        Assert.Contains("TokenPrefix", propNames);
    }

    // ── 2 — validate with the correct plaintext returns the token (ESC·3) ──
    // FACES ESC-3: a valid plaintext (the one the receiving admin issued)
    // resolves to the non-revoked token — the inbound endpoint's credential
    // check (ESC·5). A valid token is a non-revoked row; this test asserts the
    // match + the LastUsedAt stamp (the ESC·9 read-then-write, no audit row).

    [Fact(DisplayName = "ESC_3_Token_Validate_Valid_Returns_Token")]
    public async Task ESC_3_Token_Validate_Valid_Returns_Token()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new EscalationTokenService(store);

        var (plaintext, stored) = await svc.GenerateAsync("Origin platform B", "admin-esc-2", ct);

        var validated = await svc.ValidateAsync(plaintext, ct);

        Assert.NotNull(validated);
        Assert.Equal(stored.Id, validated!.Id);
        Assert.Equal("Origin platform B", validated.Label);
        // A valid token is non-revoked (the ValidateAsync RevokedAt == null
        // guard, ESC·9).
        Assert.Null(validated.RevokedAt);
        // The LastUsedAt stamp is applied on the matched token on a match (the
        // ESC·9 read-then-write, no audit row — the impl sets it on the
        // returned object and persists it in the same session).
        Assert.NotNull(validated.LastUsedAt);
    }

    // ── 3 — validate with a wrong plaintext returns null (ESC·3 / ESC·5) ──
    // FACES ESC-4: a token the receiving admin never issued (a wrong plaintext)
    // does not resolve — the inbound endpoint maps this to a 401, no row
    // (ESC·5). The hash compare (the SHA-256 of the wrong plaintext) matches no
    // stored row.

    [Fact(DisplayName = "ESC_3_Token_Validate_Invalid_Returns_Null")]
    public async Task ESC_3_Token_Validate_Invalid_Returns_Null()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new EscalationTokenService(store);

        // A known token exists so the lookup is a *mismatch*, not an empty
        // table (a blank token short-circuits to null in the impl, ESC·5 —
        // both paths return null; this exercises the hash-mismatch path).
        await svc.GenerateAsync("Origin platform C", "admin-esc-3", ct);

        var validated = await svc.ValidateAsync("kesc_totally-wrong-plaintext", ct);
        Assert.Null(validated);
    }

    // ── 4 — revoke is immediate: the token no longer validates (ESC·9) ────
    // FACES ESC-7: the receiving admin revokes a token (immediate, one
    // AccessAudit row escalation.token.revoke) → a subsequent inbound with
    // that token is invalid (the handoff gate's second half, ESC·9). The
    // ValidateAsync RevokedAt == null guard means a revoked row never matches.

    [Fact(DisplayName = "ESC_9_Token_Revoke_Immediate_NoValidate")]
    public async Task ESC_9_Token_Revoke_Immediate_NoValidate()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new EscalationTokenService(store);

        var (plaintext, stored) = await svc.GenerateAsync("Origin platform D", "admin-esc-4", ct);

        // A valid token validates before revocation (the pre-revoke state).
        var before = await svc.ValidateAsync(plaintext, ct);
        Assert.NotNull(before);

        // The effective revoke (ESC·9) — sets RevokedAt / RevokedBy + exactly
        // one AccessAudit row (escalation.token.revoke, Via = Admin).
        const string revokeAdmin = "admin-esc-4-revoker";
        var effective = await svc.RevokeAsync(stored.Id, revokeAdmin, ct);
        Assert.True(effective);

        // After revocation the token no longer validates (immediate, ESC·9).
        var after = await svc.ValidateAsync(plaintext, ct);
        Assert.Null(after);

        // The stored row carries the revoke stamp + exactly one
        // escalation.token.revoke audit row (the ESC·9 single-write-lane).
        await using var q = store.QuerySession();
        var row = await q.LoadAsync<EscalationToken>(stored.Id, ct);
        Assert.NotNull(row);
        Assert.NotNull(row!.RevokedAt);
        Assert.Equal(revokeAdmin, row.RevokedBy);
        var revokeAudits = (await q.Query<AccessAudit>()
            .Where(a => a.Action == "escalation.token.revoke" && a.TargetId == stored.Id)
            .ToListAsync(ct)).ToList();
        Assert.Single(revokeAudits);
        Assert.Equal(AccessVia.Admin, revokeAudits[0].Via);
    }

    // ── 5 — the list exposes no plaintext / hash (ESC·3 / ESC·9) ──────────
    // FACES ESC-1: the receiving admin's token list is a read-model
    // (EscalationTokenSummary) — each row exposes the Label + the masked
    // TokenPrefix + the Created / LastUsedAt / RevokedAt status, but **no**
    // plaintext and **no** TokenHash (ESC·3). The summary type is the pin —
    // it simply has no plaintext / hash member to expose.

    [Fact(DisplayName = "ESC_9_Token_List_Excludes_Plaintext_And_Hash")]
    public async Task ESC_9_Token_List_Excludes_Plaintext_And_Hash()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new EscalationTokenService(store);

        // Two labeled tokens (the multiple-coexisting, ESC·3 pin) — the list
        // must return both, each masked.
        var (p1, s1) = await svc.GenerateAsync("Origin platform E", "admin-esc-5", ct);
        await svc.GenerateAsync("Origin platform F", "admin-esc-5", ct);

        var list = await svc.ListAsync(ct);

        Assert.Equal(2, list.Count);
        // The generated token's row is present (by id) and exposes only the
        // masked prefix + label + status — the plaintext never appears.
        var thisRow = Assert.Single(list, s => s.Id == s1.Id);
        Assert.Equal("Origin platform E", thisRow.Label);
        Assert.StartsWith("kesc_", thisRow.TokenPrefix);
        Assert.DoesNotContain(p1, thisRow.TokenPrefix, StringComparison.Ordinal);
        // Newest-first ordering (the ListAsync OrderByDescending(Created) shape,
        // the M32·8 precedent).
        Assert.True(list[0].Created >= list[1].Created);

        // The summary read-model has **no** plaintext member and **no**
        // TokenHash member (ESC·3 — the only form the receiving side shows is
        // the TokenPrefix).
        var propNames = typeof(EscalationTokenSummary).GetProperties()
            .Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("Plaintext", propNames);
        Assert.DoesNotContain("TokenHash", propNames);
        Assert.Contains("TokenPrefix", propNames);
    }

    // ─── Shared helpers ────────────────────────────────────────────────────

    /// <summary>The U03 SHA-256 hex digest (the impl's private helper mirrored
    /// here so the test asserts the exact stored value).</summary>
    private static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    /// <summary>
    /// Boot a fresh scratch store with the two ESC doc types (the
    /// <see cref="Kumunita.Core.EscalationDocTypes.Configure"/> surface — the
    /// <c>EscalationToken</c> + the <c>EscalationOutboundConfig</c>) + the
    /// <c>AccessAudit</c> doc (via <see cref="Kumunita.Core.M1DocTypes.Configure"/>)
    /// registered in the <c>DocumentStore.For</c> lambda — the
    /// <see cref="ErrorReportResolveTests.BootStoreAsync"/> idiom.
    /// </summary>
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
            EscalationDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }
}
