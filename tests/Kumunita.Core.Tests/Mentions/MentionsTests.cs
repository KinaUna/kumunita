using Kumunita.Core.Authorization;
using Kumunita.Core.Mentions;
using Kumunita.Core.UserInfo;
using Marten;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M36 (ADR 0165, design doc Part 2.7) — the 8 pinned <see cref="MentionService"/> /
/// <see cref="MentionTokens"/> tests. Three pin the <see cref="MentionTokens"/>
/// pure parser (M36-1 — the de-dup + code-span-exclude + lone-<c>@</c> pins);
/// five pin <see cref="IMentionService.ResolveAsync"/> (the M36·2 exactly-one
/// resolution + the M36-5 inert rule: zero-match / two-or-more / author-self /
/// zero-<c>AccessAudit</c>-row).
/// <para>
/// Harness: the <see cref="PostgresFixture"/> fresh-scratch-database-per-test
/// shape (the <c>KioskServiceTests</c> precedent). The store registers
/// <see cref="M1DocTypes"/> (the <c>Profile</c> + <c>AccessAudit</c> tables these
/// pins read). The <see cref="MentionService"/> composes the frozen M2
/// <see cref="DirectoryService"/> catalog read (M36·2 — reused, not re-shaped);
/// the parser is pure (no DI, no I/O), so the three parser pins need no store.
/// </para>
/// </summary>
public sealed class MentionsTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── Pin 1 ─────────────────────────────────────────────────────────────
    // M36-1 — the parser returns the ordered, de-duplicated candidate tokens
    // (case-insensitive, order-preserving). `@Ada @Ada @Bob` → [Ada, Bob]: the
    // second `@Ada` is dropped, and `Ada` precedes `Bob` (first-occurrence
    // order). Pure: no store.

    [Fact]
    public void MentionTokens_ExtractTokens_DeDuplicatesAndOrders()
    {
        var tokens = MentionTokens.ExtractTokens("@Ada @Ada @Bob");

        Assert.Equal(new[] { "Ada", "Bob" }, tokens);
    }

    // ── Pin 2 ─────────────────────────────────────────────────────────────
    // M36-1 / M36·2 — a `@`-token inside an inline code span, a link text, or
    // an image alt is markup, not a mention: it is never extracted. The three
    // `@codeN` candidates (inline code / link / image) are all excluded, while
    // the bare `@ok` token still resolves as a candidate.

    [Fact]
    public void MentionTokens_ExtractTokens_ExcludesCodeSpansAndLinks()
    {
        var body = "see `@code1` and [t @code2](/x) and ![@code3](/y) and @ok";

        var tokens = MentionTokens.ExtractTokens(body);

        Assert.DoesNotContain("code1", tokens);
        Assert.DoesNotContain("code2", tokens);
        Assert.DoesNotContain("code3", tokens);
        Assert.Contains("ok", tokens);
    }

    // ── Pin 3 ─────────────────────────────────────────────────────────────
    // M36-1 — a lone `@` (whitespace right after) or a trailing `@` (end of
    // string, nothing token-eligible after) yields no token. The middle `@hi`
    // is a real candidate; the two inert `@`s are dropped.

    [Fact]
    public void MentionTokens_ExtractTokens_LoneAndTrailingAtYieldNoToken()
    {
        Assert.Empty(MentionTokens.ExtractTokens("@ "));      // lone @
        Assert.Empty(MentionTokens.ExtractTokens("hi @"));    // trailing @
        Assert.Equal(new[] { "hi" }, MentionTokens.ExtractTokens("@ @hi @"));
    }

    // ── Pin 4 ─────────────────────────────────────────────────────────────
    // M36·2 — a token matching exactly one non-blocked, non-author resident
    // resolves to that resident's id. Ada matches the token `Ada`; the author
    // is a distinct resident, so Ada is not excluded.

    [Fact]
    public async Task ResolveAsync_ExactlyOneNonBlockedNonAuthor_Resolves()
    {
        var (store, svc) = await BootAsync();
        await PlantResidents(store,
            ("ada", "Ada"),
            ("bob", "Bob"),
            ("author-1", "Author One"));

        var resolved = await svc.ResolveAsync(["Ada"], "author-1");

        Assert.Equal(new[] { "ada" }, resolved);
        await store.DisposeAsync();
    }

    // ── Pin 5 ─────────────────────────────────────────────────────────────
    // M36-5 (inert) — a token matching zero residents (an unknown name) is
    // dropped: no id, never a guess.

    [Fact]
    public async Task ResolveAsync_ZeroMatches_YieldsNoId()
    {
        var (store, svc) = await BootAsync();
        await PlantResidents(store, ("ada", "Ada"));

        var resolved = await svc.ResolveAsync(["Ghost"], "author-1");

        Assert.Empty(resolved);
        await store.DisposeAsync();
    }

    // ── Pin 6 ─────────────────────────────────────────────────────────────
    // M36-5 (inert) — a token matching two or more residents (ambiguous) is
    // dropped: no id, never a guess. Two residents share the display name
    // `Twin`; neither is the author; the token resolves to nothing.

    [Fact]
    public async Task ResolveAsync_TwoOrMoreMatches_YieldsNoId()
    {
        var (store, svc) = await BootAsync();
        await PlantResidents(store,
            ("twin-a", "Twin"),
            ("twin-b", "Twin"));

        var resolved = await svc.ResolveAsync(["Twin"], "author-1");

        Assert.Empty(resolved);
        await store.DisposeAsync();
    }

    // ── Pin 7 ─────────────────────────────────────────────────────────────
    // M36-5 (self) — a token matching the author's own display name resolves
    // to nothing: the author is excluded by SubjectId even though a resident
    // with that display name exists (the author is never notified of their
    // own mention).

    [Fact]
    public async Task ResolveAsync_AuthorsOwnName_YieldsNoId()
    {
        var (store, svc) = await BootAsync();
        await PlantResidents(store, ("carol", "Carol"));

        var resolved = await svc.ResolveAsync(["Carol"], "carol");

        Assert.Empty(resolved);
        await store.DisposeAsync();
    }

    // ── Pin 8 ─────────────────────────────────────────────────────────────
    // M36-5 — a mention is a *read*, not an access decision: ResolveAsync
    // writes zero AccessAudit rows (a fresh scratch store has none, and a
    // resolving call must not add any).

    [Fact]
    public async Task ResolveAsync_WritesZeroAccessAuditRows()
    {
        var (store, svc) = await BootAsync();
        await PlantResidents(store, ("ada", "Ada"));

        await svc.ResolveAsync(["Ada"], "author-1");

        await using var q = store.QuerySession();
        Assert.Equal(0, await q.Query<AccessAudit>().CountAsync(TestContext.Current.CancellationToken));
        await store.DisposeAsync();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Boot a fresh scratch store with the M1 surface (the
    /// <see cref="Profile"/> + <see cref="AccessAudit"/> tables these pins read),
    /// and a <see cref="MentionService"/> over the frozen M2
    /// <see cref="DirectoryService"/> (the <c>ListAsync</c> pure catalog read —
    /// M36·2). The <see cref="IAuthorizationService"/> is carried but never
    /// invoked by <c>ListAsync</c> (a read, not a decision — M36·5), so a bare
    /// substitute satisfies the ctor's non-null guard.</summary>
    private async Task<(IDocumentStore store, MentionService svc)> BootAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        var userInfo = new UserInfoService(store);
        var directory = new DirectoryService(userInfo, Substitute.For<IAuthorizationService>());
        var svc = new MentionService(directory);
        return (store, svc);
    }

    /// <summary>Plant non-blocked <see cref="Profile"/> rows directly (test
    /// fixture seeding, not a service write seam) — each tuple is a
    /// (subject id, display name) pair.</summary>
    private static async Task PlantResidents(IDocumentStore store, params (string Id, string Name)[] residents)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        foreach (var (id, name) in residents)
        {
            w.Store(new Profile { SubjectId = id, DisplayName = name, Verified = true, Blocked = false });
        }
        await w.SaveChangesAsync(ct);
    }
}
