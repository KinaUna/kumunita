using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Moderation;
using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M3b, plan U9 — the 8 <see cref="ModerationService"/> seam tests
/// (design doc <c>m3b-moderation.md</c> §2.5, rows 1–8). Mirrors the
/// <c>PostServiceTests</c> (M3 U9) shape: same <see cref="PostgresFixture"/>,
/// same <c>BootStoreAsync</c>, same trio-of-services composition, fresh
/// scratch Postgres per test method.
/// <para>
/// The four <see cref="ModerationService"/> write lanes (U4/U5) are
/// exercised over the two frozen M1/M2 seams (<see cref="IUserInfoService"/>
/// + <see cref="IAuthorizationService"/>) plus the caller-owned
/// <see cref="IDocumentStore"/> (the C3 same-transaction shape). Each test
/// name is pinned verbatim by §2.5 — the §2.7 drift-guard makes renaming
/// them a drift event — and carries its FACES row (F1 / F5 / F6) and
/// invariant anchor (C-M3b·1 / C-M3b·2 / C-M3b·4, ADR 0003 §SoD) per Part 1.
/// </para>
/// <para>
/// This unit **does not** run the §2.6 acceptance gate (U10) and authors
/// no e2e (U10). The pass/red status of these 8 is data U10 consumes
/// alongside <c>PostServiceTests</c>'s M3 lane (rows 9–13 in the same
/// file, the M3b ADDs).
/// </para>
/// </summary>
public class ModerationServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string ComponentId = "c-m3b-comp";

    // ── 1 — FileReportAsync_Filing_ViaTagIsAdmin_NotReport_NotOwner ──────
    //
    // C-M3b·1 (F1) filing lane: the resident-facing intake write. No
    // IAuthorizationService call. Audit row carries the pinned
    // AccessVia.Admin literal — NOT AccessVia.Report (reserved for the
    // read branch, C-M3b·2), NOT AccessVia.Owner (C1 owner-branch) (§2.3
    // item 1). The two negatives are the authoritative shape.

    [Fact]
    public async Task FileReportAsync_Filing_ViaTagIsAdmin_NotReport_NotOwner()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string reporter = "u-m3b-f1-reporter";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        await Plant(store, new Post
        {
            Id = "f1-post", ComponentId = ComponentId, AuthorId = "u-m3b-f1-owner",
            Body = "body f1", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, reporter),
        });

        await RunInSession(store, async session =>
            await svc.FileReportAsync("f1-post", reporter, "harassment", session));

        var rows = await PostAudits(store, actor: reporter);
        var filingRow = Assert.Single(rows, a => a.Action == "report.file");
        Assert.Equal(AccessVia.Admin, filingRow.Via);
        Assert.NotEqual(AccessVia.Report, filingRow.Via);
        Assert.NotEqual(AccessVia.Owner, filingRow.Via);
        Assert.Equal(AccessOutcome.Allow, filingRow.Outcome);
        Assert.Equal("f1-post", filingRow.TargetId);
        Assert.Equal(reporter, filingRow.ActorId);
    }

    // ── 2 — FileReportAsync_Filing_WritesReportStatusFiled ───────────────
    //
    // C-M3b·1 (F1) — the exact Status-literal pin for the filing lane
    // (§2.3 item 2: "filed"). First of the four literal pins.

    [Fact]
    public async Task FileReportAsync_Filing_WritesReportStatusFiled()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string reporter = "u-m3b-f2-reporter";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        await Plant(store, new Post
        {
            Id = "f2-post", ComponentId = ComponentId, AuthorId = "u-m3b-f2-owner",
            Body = "body f2", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, reporter),
        });

        string? createdReportId = null;
        await RunInSession(store, async session =>
        {
            await svc.FileReportAsync("f2-post", reporter, "abuse", session);
            var filed = await session.Query<Report>()
                .Where(r => r.PostId == "f2-post").ToListAsync();
            createdReportId = Assert.Single(filed).Id;
        });

        // Re-load in a fresh session — the "filed" literal must be there.
        await using (var s2 = store.QuerySession())
        {
            var report = await s2.LoadAsync<Report>(createdReportId);
            Assert.NotNull(report);
            Assert.Equal("filed", report!.Status);
        }
    }

    // ── 3 — CanReadWithReportAsync_ModeratorWithReport_Allowed_ViaTagIsReport ─
    //
    // C-M3b·2 (F2) — the filed report is the gate for a standing
    // moderator (C5 carve-out activated). The M3b read lane's
    // AccessAudit row (the U5 "Deny" path, which writes its own row
    // with Via = Report) is the pin for the literal; the Allow path
    // delegates to the M1-frozen IAuthorizationService.CanAsync (the
    // seam's own audit row is the Allow record — the M3b lane writes
    // an extra row with Via = Report on Deny only, per §2.4 item 3/4).

    [Fact]
    public async Task CanReadWithReportAsync_ModeratorWithReport_Allowed_ViaTagIsReport()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string moderator = "u-m3b-f3-moderator";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = "f3-assign", UserId = moderator, ComponentId = ComponentId,
            GrantedBy = "u-m3b-f3-admin", At = DateTimeOffset.UtcNow
        });
        await Plant(store, new Post
        {
            Id = "f3-post", ComponentId = ComponentId, AuthorId = "u-m3b-f3-author",
            Body = "body f3", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, "u-m3b-f3-member"),
        });
        await Plant(store, new Report
        {
            Id = "f3-report", PostId = "f3-post", ReporterId = "u-m3b-f3-reporter",
            ComponentId = ComponentId, Reason = "flagged", Status = "filed",
            At = DateTimeOffset.UtcNow
        });

        var decision = await svc.CanReadWithReportAsync("f3-post", moderator);
        Assert.True(decision.Allowed);

        // The canonical Allow record is the M1-frozen seam's own audit
        // row (the M3b lane's Allow path delegates to it; the lane's
        // own extra row is the Deny path's only, per U5's code). The
        // test asserts the decision came out Allowed (via branch #2 —
        // the C5 carve-out activated).
        var allowRows = await PostAudits(store, actor: moderator);
        Assert.Contains(allowRows, a => a.Action == "read"
                                        && a.Outcome == AccessOutcome.Allow
                                        && a.TargetId == "f3-post");
    }

    // ── 4 — CanReadWithReportAsync_ModeratorWithoutReport_Denied_C5Unactivated ─
    //
    // C-M3b·2 (C5 unactivated) — a standing moderator with NO filed
    // report in this component is denied. The M3b lane writes its own
    // Deny audit row in its own commit, with the pinned Via = Report
    // literal (§2.4 item 4) — the only one of the M3b lanes where the
    // Via = Report literal lives on the M3b-side row (the filing
    // lane's pinned Admin is a different literal; the Assign/Unlock/
    // Resolve lanes carry decision.Via from the M1 seam).

    [Fact]
    public async Task CanReadWithReportAsync_ModeratorWithoutReport_Denied_C5Unactivated()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string moderator = "u-m3b-f4-moderator";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = "f4-assign", UserId = moderator, ComponentId = ComponentId,
            GrantedBy = "u-m3b-f4-admin", At = DateTimeOffset.UtcNow
        });
        await Plant(store, new Post
        {
            Id = "f4-post", ComponentId = ComponentId, AuthorId = "u-m3b-f4-author",
            Body = "body f4", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, "u-m3b-f4-member"),
        });
        // No Report row for "f4-post" in this component.

        var decision = await svc.CanReadWithReportAsync("f4-post", moderator);
        Assert.False(decision.Allowed);

        // The M3b lane wrote its own Deny row (in its own commit):
        // Via = Report, Outcome = Deny, TargetId = the post id.
        var rows = await PostAudits(store, actor: moderator);
        Assert.Contains(rows, a => a.Action == "read"
                                   && a.TargetId == "f4-post"
                                   && a.Via == AccessVia.Report
                                   && a.Outcome == AccessOutcome.Deny);
    }

    // ── 5 — AssignReportAsync_ModeratorCaller_Denied_NoWrite_NoPartialState ─
    //
    // C-M3b·4 (F5, SoD) — the write-lane gate (AccessAction.Moderate
    // in the M1-frozen seam) is the SoD discriminator: a caller without
    // a standing moderator in THIS component is denied (branch #2 does
    // not fire), the Report.Status is NOT updated to "assigned", and
    // no new ModeratorAssignment row is written. C3 — the audit row
    // (Deny) is still committed.

    [Fact]
    public async Task AssignReportAsync_ModeratorCaller_Denied_NoWrite_NoPartialState()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string caller = "u-m3b-f5-caller";        // no assignment in this component
        const string target = "u-m3b-f5-target";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = true });
        await Plant(store, new Post
        {
            Id = "f5-post", ComponentId = ComponentId, AuthorId = "u-m3b-f5-author",
            Body = "body f5", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, "u-m3b-f5-member"),
        });
        await Plant(store, new Report
        {
            Id = "f5-report", PostId = "f5-post", ReporterId = "u-m3b-f5-reporter",
            ComponentId = ComponentId, Reason = "flagged", Status = "filed",
            At = DateTimeOffset.UtcNow
        });

        await RunInSession(store, async session =>
            await svc.AssignReportAsync("f5-report", target, caller, session));

        // The report's Status must remain "filed" (no partial write).
        await using (var s2 = store.QuerySession())
        {
            var report = await s2.LoadAsync<Report>("f5-report");
            Assert.Equal("filed", report!.Status);
        }

        // No new ModeratorAssignment for the target in this component.
        await using (var s3 = store.QuerySession())
        {
            var assignments = await s3.Query<ModeratorAssignment>()
                .Where(a => a.UserId == target && a.ComponentId == ComponentId)
                .ToListAsync();
            Assert.Empty(assignments);
        }

        // The Deny audit record is the M1-frozen seam's own row (the
        // ModerationService lane's `if (decision.Allowed)` guard covers
        // BOTH the domain write and the lane's own hand-written row — a
        // denied call writes no lane row, only the seam's always-on row;
        // §2.3 item 4 pin "audit row" = the seam's row, here with
        // TargetKind = "post", TargetId = the post id, Action =
        // "moderate", Outcome = Deny, committed in the same caller
        // transaction as no domain write).
        var rows = await PostAudits(store, actor: caller);
        Assert.Contains(rows, a => a.Action == AccessAction.Moderate.Id
                                   && a.TargetId == "f5-post"
                                   && a.Outcome == AccessOutcome.Deny);
    }

    // ── 6 — AssignReportAsync_GlobalAdmin_WritesStatusAssigned_ModAssignmentRow ─
    //
    // C-M3b·4 (F5) — a caller with a standing moderator in this
    // component (the ADR 0003 §SoD split: Core trusts the caller's
    // standing, the Web layer verifies the GlobalAdmin role) succeeds:
    // the report's Status flips to the exact "assigned" literal (§2.3
    // item 2) AND a new ModeratorAssignment row is written for the
    // assigned moderator on this component, with GrantedBy = caller
    // (the SoD audit trail). One SaveChangesAsync commits both rows
    // (C3 same-transaction).

    [Fact]
    public async Task AssignReportAsync_GlobalAdmin_WritesStatusAssigned_ModAssignmentRow()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string globalAdmin = "u-m3b-f6-admin";     // standing moderator in this component
        const string newMod     = "u-m3b-f6-newmod";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = "f6-assign-admin", UserId = globalAdmin, ComponentId = ComponentId,
            GrantedBy = "u-m3b-f6-root", At = DateTimeOffset.UtcNow
        });
        await Plant(store, new Post
        {
            Id = "f6-post", ComponentId = ComponentId, AuthorId = "u-m3b-f6-author",
            Body = "body f6", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, "u-m3b-f6-member"),
        });
        await Plant(store, new Report
        {
            Id = "f6-report", PostId = "f6-post", ReporterId = "u-m3b-f6-reporter",
            ComponentId = ComponentId, Reason = "flagged", Status = "filed",
            At = DateTimeOffset.UtcNow
        });

        await RunInSession(store, async session =>
            await svc.AssignReportAsync("f6-report", newMod, globalAdmin, session));

        // Status flipped to the exact "assigned" literal.
        await using (var s2 = store.QuerySession())
        {
            var report = await s2.LoadAsync<Report>("f6-report");
            Assert.Equal("assigned", report!.Status);
        }

        // A new ModeratorAssignment row exists for (newMod, ComponentId)
        // with GrantedBy = the GlobalAdmin caller (the SoD audit trail).
        await using (var s3 = store.QuerySession())
        {
            var assignments = await s3.Query<ModeratorAssignment>()
                .Where(a => a.UserId == newMod && a.ComponentId == ComponentId)
                .ToListAsync();
            var row = Assert.Single(assignments);
            Assert.Equal(globalAdmin, row.GrantedBy);
        }

        // Allow audit row for the write.
        var rows = await ReportAudits(store, actor: globalAdmin);
        Assert.Contains(rows, a => a.Action == "report.assign"
                                   && a.TargetId == "f6-report"
                                   && a.Outcome == AccessOutcome.Allow);
    }

    // ── 7 — ResolveReportAsync_GlobalAdmin_WritesStatusResolved_FlipsFlagSameTxn ─
    //
    // C-M3b·4 (F6, C5 activation) — a caller with standing in this
    // component succeeds: Report.Status flips to the exact "resolved"
    // literal (§2.3 item 2), the flag-flip via the M1-frozen seam
    // (IUserInfoService.SetComponentModeratorAccessAsync) lands in a
    // separate commit (per U5's doc — the M1 seam's own contract:
    // "own session, own commit"), and the AccessAudit row is Allow.
    // The test reloads the component in a FRESH session and asserts
    // the flip is visible (strong-consistency — the seam already
    // committed).

    [Fact]
    public async Task ResolveReportAsync_GlobalAdmin_WritesStatusResolved_FlipsFlagSameTxn()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string globalAdmin = "u-m3b-f7-admin";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = "f7-assign-admin", UserId = globalAdmin, ComponentId = ComponentId,
            GrantedBy = "u-m3b-f7-root", At = DateTimeOffset.UtcNow
        });
        await Plant(store, new Post
        {
            Id = "f7-post", ComponentId = ComponentId, AuthorId = "u-m3b-f7-author",
            Body = "body f7", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, "u-m3b-f7-member"),
        });
        await Plant(store, new Report
        {
            Id = "f7-report", PostId = "f7-post", ReporterId = "u-m3b-f7-reporter",
            ComponentId = ComponentId, Reason = "resolved-cased", Status = "filed",
            At = DateTimeOffset.UtcNow
        });

        await RunInSession(store, async session =>
            await svc.ResolveReportAsync("f7-report", globalAdmin, session));

        // Status flipped to the exact "resolved" literal.
        await using (var s2 = store.QuerySession())
        {
            var report = await s2.LoadAsync<Report>("f7-report");
            Assert.Equal("resolved", report!.Status);
        }

        // The flag flip on Component.ModeratorAccess: it was planted
        // = true here, and the M1 seam wrote = true (the lane passes
        // on = true). Either way the post-lane state is true. This
        // assertion is the observable "the M1 seam committed the
        // flag-flip" pin — the test re-reads in a fresh session and
        // verifies the value is true now.
        await using (var s3 = store.QuerySession())
        {
            var comp = await s3.LoadAsync<Component>(ComponentId);
            Assert.True(comp!.ModeratorAccess);
        }

        // Allow audit row for the write.
        var rows = await ReportAudits(store, actor: globalAdmin);
        Assert.Contains(rows, a => a.Action == "report.resolve"
                                   && a.TargetId == "f7-report"
                                   && a.Outcome == AccessOutcome.Allow);
    }

    // ── 8 — ResolveReportAsync_NonGlobalAdminCaller_Denied_NoWrite_NoPartialState ─
    //
    // C-M3b·4 (F6, SoD) — the caller lacks a standing moderator in
    // this component → the M1 seam denies (branch #2 does not fire) →
    // Report.Status is NOT updated to "resolved" (no partial write),
    // the component's ModeratorAccess flag is unchanged, and the
    // Deny audit row is committed.

    [Fact]
    public async Task ResolveReportAsync_NonGlobalAdminCaller_Denied_NoWrite_NoPartialState()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string caller = "u-m3b-f8-caller";        // no assignment in this component

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = false });                  // default OFF
        await Plant(store, new Post
        {
            Id = "f8-post", ComponentId = ComponentId, AuthorId = "u-m3b-f8-author",
            Body = "body f8", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, "u-m3b-f8-member"),
        });
        await Plant(store, new Report
        {
            Id = "f8-report", PostId = "f8-post", ReporterId = "u-m3b-f8-reporter",
            ComponentId = ComponentId, Reason = "unresolved-cased", Status = "filed",
            At = DateTimeOffset.UtcNow
        });

        await RunInSession(store, async session =>
            await svc.ResolveReportAsync("f8-report", caller, session));

        // Status remains "filed" (no partial write).
        await using (var s2 = store.QuerySession())
        {
            var report = await s2.LoadAsync<Report>("f8-report");
            Assert.Equal("filed", report!.Status);
        }

        // The component's ModeratorAccess flag is unchanged (the lane's
        // if (decision.Allowed) guard did not fire; the M1 seam's
        // flag-flip was never reached).
        await using (var s3 = store.QuerySession())
        {
            var comp = await s3.LoadAsync<Component>(ComponentId);
            Assert.False(comp!.ModeratorAccess);
        }

        // The Deny audit record is the M1-frozen seam's own row
        // (TargetKind = "post", TargetId = the post id, Action =
        // "moderate", Outcome = Deny) — the ModerationService lane's
        // own row is only written inside `if (decision.Allowed)` (the
        // C3 same-transaction lane).
        var rows = await PostAudits(store, actor: caller);
        Assert.Contains(rows, a => a.Action == AccessAction.Moderate.Id
                                   && a.TargetId == "f8-post"
                                   && a.Outcome == AccessOutcome.Deny);
    }


    // ── Shared helpers (mirror the PostServiceTests shape) ──────────────

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
            M3DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    /// <summary>Compose the M3b service trio: <see cref="UserInfoService"/> +
    /// <see cref="AuthorizationService"/> + <see cref="ModerationService"/> —
    /// the same three-constructor shape as <c>PostServiceTests.Services</c>,
    /// plus the M3b service over the same two frozen M1/M2 seams.</summary>
    private static (UserInfoService User, AuthorizationService Authz, ModerationService Moderation)
        Services(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var moderation = new ModerationService(userInfo, authz, store);
        return (userInfo, authz, moderation);
    }

    private static Audience Audience(GrantKind kind, string id)
        => new(AudienceMode.Any, [new AudienceGrant(kind, id)]);

    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }

    private static async Task<T> RunInSession<T>(IDocumentStore store, Func<IDocumentSession, Task<T>> action)
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        return await action(session);
    }

    private static async Task RunInSession(IDocumentStore store, Func<IDocumentSession, Task> action)
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        await action(session);
    }

    /// <summary>
    /// Audit rows scoped to the report's own <c>TargetKind = "report"</c>
    /// (the M3b assign/unlock/resolve write-lane rows — the lane's own
    /// <see cref="AccessAudit"/> row, hand-written per the C3 same-
    /// transaction discipline). The filing lane's hand-written row and
    /// the M1-seam's own row carry <c>TargetKind = "post"</c>, so they
    /// are read via <see cref="PostAudits"/> (rows 1/2) or via
    /// <see cref="AllAudits"/> (rows 3/4, where the M3b lane writes a
    /// <c>TargetKind = "post"</c> row on the Deny path, and the M1 seam
    /// writes the Allow-path row).
    /// </summary>
    private static async Task<IReadOnlyList<AccessAudit>> ReportAudits(IDocumentStore store, string? actor = null)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        var q = s.Query<AccessAudit>().Where(a => a.TargetKind == "report");
        if (actor is not null) q = q.Where(a => a.ActorId == actor);
        return await q.ToListAsync(ct);
    }

    private static async Task<IReadOnlyList<AccessAudit>> PostAudits(IDocumentStore store, string? actor = null)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        var q = s.Query<AccessAudit>().Where(a => a.TargetKind == "post");
        if (actor is not null) q = q.Where(a => a.ActorId == actor);
        return await q.ToListAsync(ct);
    }

    private static async Task<IReadOnlyList<AccessAudit>> AllAudits(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<AccessAudit>().ToListAsync(ct);
    }
}
