using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Marten;
using Marten.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// ADR 0050 — the admin-managed sign-up gate (open vs. invitation-only). The
/// gate is an additive field on the <see cref="LocaleSettings"/> singleton
/// (the same doc the ADR 0019 / ADR 0020 platform-default lanes use), read via
/// <see cref="IdentityService.IsSignupOpenAsync"/> (the <c>true</c> floor — a
/// fresh instance ships with sign-up open) and written via
/// <see cref="IdentityService.SetSignupOpenAsync"/> (exactly one
/// <see cref="AccessAudit"/> row, <c>Via = Admin</c>, action
/// <c>signup.set-open</c>, <c>TargetKind</c> "signup").
/// <para>
/// Pinned the same way the ADR 0019 / ADR 0020 timezone / date-format lanes are
/// (via <c>AccessAudit</c> shape + the live-on-next-read invariant), over a
/// fresh scratch Postgres (the <see cref="PostgresFixture"/> harness).
/// <c>IdentityService</c> is constructed with substitutes for the seams the
/// gate does not touch (UserManager / IUserInfoService / IClaimsSource /
/// IMailerStage / IOptions / ILogger) — only the <c>documentStore</c> is the
/// real one.
/// </para>
/// </summary>
public class IdentityServiceSignupPolicyTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── The `true` floor — a fresh instance ships with sign-up open ───────

    [Fact]
    public async Task IsSignupOpen_FreshInstance_FloorsToTrue_NoAuditRow()
    {
        var (svc, store) = await BuildAsync();

        // A fresh instance has no LocaleSettings row at all (the seeder has not
        // run in this scratch DB); the floor is `true`.
        Assert.True(await svc.IsSignupOpenAsync());

        // A read is a read — no audit row is committed for the floor probe.
        var audits = await AuditRows(store);
        Assert.Empty(audits);
    }

    // ── Invitation-only (closed) — the admin flips the gate ───────────────

    [Fact]
    public async Task SetSignupOpen_Closed_AuditRowShape_ViaAdmin_AndLiveOnNextRead()
    {
        var (svc, store) = await BuildAsync();

        const string actor = "admin-s50";
        await svc.SetSignupOpenAsync(false, actor);

        // The value is live on the very next read (M·4: data, not config).
        Assert.False(await svc.IsSignupOpenAsync());

        // Exactly one audit row (the single write), the ADR 0019 / ADR 0020
        // shape (Via Admin, Outcome Allow), the singleton-toggle target
        // (TargetKind "signup", TargetId "signup", action "signup.set-open").
        var audits = await AuditRows(store, action: "signup.set-open");
        Assert.Single(audits);
        var row = audits[0];
        Assert.Equal("signup.set-open", row.Action);
        Assert.Equal("signup", row.TargetKind);
        Assert.Equal("signup", row.TargetId);
        Assert.Equal(AccessVia.Admin, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
        Assert.Equal(actor, row.ActorId);
        // A singleton toggle is not an access change — no VisibilityCount
        // (the single-target shape, ADR 0006 §B).
        Assert.Null(row.VisibleCount);
        Assert.Null(row.HiddenCount);
    }

    // ── Re-open — the admin flips it back ─────────────────────────────────

    [Fact]
    public async Task SetSignupOpen_Reopen_SetsTrue_AndSecondAuditRow()
    {
        var (svc, store) = await BuildAsync();

        const string actor = "admin-s50";
        await svc.SetSignupOpenAsync(false, actor);
        Assert.False(await svc.IsSignupOpenAsync());

        // Flip back open: the value is live, and a *second* audit row is
        // appended (the lane is additive — each flip is one row).
        await svc.SetSignupOpenAsync(true, actor);
        Assert.True(await svc.IsSignupOpenAsync());

        var audits = await AuditRows(store, action: "signup.set-open");
        Assert.Equal(2, audits.Count);
        Assert.All(audits, r =>
        {
            Assert.Equal(AccessVia.Admin, r.Via);
            Assert.Equal(actor, r.ActorId);
        });
    }

    // ── Harness ───────────────────────────────────────────────────────────

    /// <summary>
    /// A real Marten store over a fresh scratch Postgres (the
    /// <see cref="PostgresFixture"/> harness) + an <see cref="IdentityService"/>
    /// wired with substitutes for the seams the gate does not touch (only
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

        // The gate's two lanes touch only `documentStore`; the other ctor seams
        // are unused (UserManager / IUserInfoService / IClaimsSource / IMailerStage
        // are not invoked by IsSignupOpenAsync / SetSignupOpenAsync). UserManager<T>
        // is a concrete class (NSubstitute's Castle proxy needs a parameterless
        // ctor it lacks), so pass null — the null reference is never dereferenced.
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
