using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Moderation;
using Kumunita.Core.Notifications;
using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Marten;
using NSubstitute;
using Npgsql;
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
                .Where(r => r.PostId == "f2-post")
                .ToListAsync(TestContext.Current.CancellationToken);
            createdReportId = Assert.Single(filed).Id;
        });

        // Re-load in a fresh session — the "filed" literal must be there.
        await using (var s2 = store.QuerySession())
        {
            var report = await s2.LoadAsync<Report>(createdReportId!, TestContext.Current.CancellationToken);
            Assert.NotNull(report);
            Assert.Equal("filed", report!.Status);
        }
    }

    // ── R1 — FileReplyReportAsync_Filing_WritesReportWithReplyId ────────
    //
    // ADR 0023 (the reply-report-target lane): the reply-targeted report
    // carries Report.ReplyId = the reply's id (the target discriminator),
    // Report.PostId = the parent post's id (C-M3·1 reply-inherits — the
    // reply's own PostId), Report.Status = "filed" (the same §2.3 item-2
    // literal pin as the post-report filing lane), and
    // Report.ComponentId = the parent post's ComponentId.

    [Fact]
    public async Task FileReplyReportAsync_Filing_WritesReportWithReplyId()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string reporter = "u-adr0023-r1-reporter";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        await Plant(store, new Post
        {
            Id = "r1-post", ComponentId = ComponentId, AuthorId = "u-adr0023-r1-owner",
            Body = "body r1", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, reporter),
        });
        await Plant(store, new PostReply
        {
            Id = "r1-reply", PostId = "r1-post", AuthorId = "u-adr0023-r1-replier",
            Body = "reply body r1", Created = DateTimeOffset.UtcNow, LanguageCode = "en",
        });

        string? createdReportId = null;
        await RunInSession(store, async session =>
        {
            await svc.FileReplyReportAsync("r1-reply", reporter, "abuse in reply", session);
            var filed = await session.Query<Report>()
                .Where(r => r.ReplyId == "r1-reply")
                .ToListAsync(TestContext.Current.CancellationToken);
            createdReportId = Assert.Single(filed).Id;
        });

        // Re-load in a fresh session — the target discriminator + parent
        // post id + "filed" literal must all be there (the ADR 0023 shape).
        await using (var s2 = store.QuerySession())
        {
            var report = await s2.LoadAsync<Report>(createdReportId!, TestContext.Current.CancellationToken);
            Assert.NotNull(report);
            Assert.Equal("r1-reply", report!.ReplyId);     // the target discriminator
            Assert.Equal("r1-post", report.PostId);        // the parent post (C-M3·1)
            Assert.Equal(ComponentId, report.ComponentId); // carried from the parent
            Assert.Equal("filed", report.Status);          // the §2.3 item-2 literal pin
            Assert.Equal("abuse in reply", report.Reason);
        }
    }

    // ── R2 — FileReplyReportAsync_Filing_ViaTagIsAdmin_NotReport_NotOwner ─
    //
    // ADR 0023 — the filing lane's audit row carries the same pinned
    // AccessVia.Admin literal as the post-report lane (C-M3b·1; two
    // negatives: NOT AccessVia.Report — reserved for the read branch,
    // C-M3b·2; NOT AccessVia.Owner — C1 owner-branch). The action +
    // target-kind distinguish the reply lane from the post lane
    // ("report.reply.file" / "reply" vs "report.file" / "post").

    [Fact]
    public async Task FileReplyReportAsync_Filing_ViaTagIsAdmin_NotReport_NotOwner()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string reporter = "u-adr0023-r2-reporter";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        await Plant(store, new Post
        {
            Id = "r2-post", ComponentId = ComponentId, AuthorId = "u-adr0023-r2-owner",
            Body = "body r2", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, reporter),
        });
        await Plant(store, new PostReply
        {
            Id = "r2-reply", PostId = "r2-post", AuthorId = "u-adr0023-r2-replier",
            Body = "reply body r2", Created = DateTimeOffset.UtcNow, LanguageCode = "en",
        });

        await RunInSession(store, async session =>
            await svc.FileReplyReportAsync("r2-reply", reporter, "harassment in reply", session));

        var rows = await ReplyAudits(store, actor: reporter);
        var filingRow = Assert.Single(rows, a => a.Action == "report.reply.file");
        Assert.Equal(AccessVia.Admin, filingRow.Via);
        Assert.NotEqual(AccessVia.Report, filingRow.Via);
        Assert.NotEqual(AccessVia.Owner, filingRow.Via);
        Assert.Equal(AccessOutcome.Allow, filingRow.Outcome);
        Assert.Equal("r2-reply", filingRow.TargetId);
        Assert.Equal(reporter, filingRow.ActorId);
    }

    // ── R3 — FileReplyReportAsync_MissingReply_ThrowsKeyNotFound ─────────
    //
    // ADR 0023 — the reply-targeted lane's fail-closed shape: a missing
    // reply is a failed call (no report row, no audit row — no partial
    // write). Mirrors the post-report lane's "missing post" branch, but
    // keyed on the reply id.

    [Fact]
    public async Task FileReplyReportAsync_MissingReply_ThrowsKeyNotFound()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string reporter = "u-adr0023-r3-reporter";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        // A parent post exists — but the reply id we pass does not.
        await Plant(store, new Post
        {
            Id = "r3-post", ComponentId = ComponentId, AuthorId = "u-adr0023-r3-owner",
            Body = "body r3", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, reporter),
        });

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => RunInSession(store, async s =>
                await svc.FileReplyReportAsync("r3-missing-reply", reporter, "x", s)));
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
            var report = await s2.LoadAsync<Report>("f5-report", TestContext.Current.CancellationToken);
            Assert.Equal("filed", report!.Status);
        }

        // No new ModeratorAssignment for the target in this component.
        await using (var s3 = store.QuerySession())
        {
            var assignments = await s3.Query<ModeratorAssignment>()
                .Where(a => a.UserId == target && a.ComponentId == ComponentId)
                .ToListAsync(TestContext.Current.CancellationToken);
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
            var report = await s2.LoadAsync<Report>("f6-report", TestContext.Current.CancellationToken);
            Assert.Equal("assigned", report!.Status);
        }

        // A new ModeratorAssignment row exists for (newMod, ComponentId)
        // with GrantedBy = the GlobalAdmin caller (the SoD audit trail).
        await using (var s3 = store.QuerySession())
        {
            var assignments = await s3.Query<ModeratorAssignment>()
                .Where(a => a.UserId == newMod && a.ComponentId == ComponentId)
                .ToListAsync(TestContext.Current.CancellationToken);
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
            var report = await s2.LoadAsync<Report>("f7-report", TestContext.Current.CancellationToken);
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
            var comp = await s3.LoadAsync<Component>(ComponentId, TestContext.Current.CancellationToken);
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
            var report = await s2.LoadAsync<Report>("f8-report", TestContext.Current.CancellationToken);
            Assert.Equal("filed", report!.Status);
        }

        // The component's ModeratorAccess flag is unchanged (the lane's
        // if (decision.Allowed) guard did not fire; the M1 seam's
        // flag-flip was never reached).
        await using (var s3 = store.QuerySession())
        {
            var comp = await s3.LoadAsync<Component>(ComponentId, TestContext.Current.CancellationToken);
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

    // ── T1 — FileReportTargetAsync_Filing_WritesReportWithTargetKind ─────
    //
    // ADR 0173 (the report-targets lane) — the filing shape for the new
    // target kinds: the Report row carries TargetKind + TargetId,
    // PostId stays empty (the original discriminators stay untouched),
    // Status is the same "filed" literal pin, and the intake audit row
    // carries the per-kind Action tag ("report.event.file"),
    // TargetKind = the target kind, Via = AccessVia.Admin (the
    // §2.3 item-1 pin, same as the post lane).

    [Fact]
    public async Task FileReportTargetAsync_Filing_WritesReportWithTargetKind()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string reporter = "u-t1-reporter";

        await Plant(store, new Kumunita.Core.Events.Event
        {
            Id = "t1-event", Title = "t1 event", AuthorId = "u-t1-author",
            Start = DateTimeOffset.UtcNow, End = DateTimeOffset.UtcNow,
        });

        string? createdReportId = null;
        await svc.FileReportTargetAsync(
            "event", "t1-event", ComponentId, "u-t1-author", reporter, "spam");

        await using (var s1 = store.QuerySession())
        {
            var filed = await s1.Query<Report>()
                .Where(r => r.TargetId == "t1-event")
                .ToListAsync(TestContext.Current.CancellationToken);
            createdReportId = Assert.Single(filed).Id;
        }

        await using (var s2 = store.QuerySession())
        {
            var report = await s2.LoadAsync<Report>(createdReportId!, TestContext.Current.CancellationToken);
            Assert.NotNull(report);
            Assert.Equal("filed", report!.Status);
            Assert.Equal("event", report.TargetKind);
            Assert.Equal("t1-event", report.TargetId);
            Assert.Equal(string.Empty, report.PostId);
            Assert.Equal("u-t1-reporter", report.ReporterId);
        }

        // The intake audit row: the per-kind Action tag + the Admin
        // Via pin (the two-negatives shape, same as the post lane).
        var rows = await AllAudits(store);
        var filingRow = Assert.Single(rows, a => a.Action == "report.event.file");
        Assert.Equal(AccessVia.Admin, filingRow.Via);
        Assert.NotEqual(AccessVia.Report, filingRow.Via);
        Assert.Equal("event", filingRow.TargetKind);
        Assert.Equal("t1-event", filingRow.TargetId);
        Assert.Equal(reporter, filingRow.ActorId);
        Assert.Equal(AccessOutcome.Allow, filingRow.Outcome);
    }

    // ── T2 — FileReportTargetAsync_AllFiveKinds_WritesRows ───────────────
    //
    // ADR 0173 — the closed vocabulary end-to-end: each of the five
    // target kinds files a report row with the right TargetKind and the
    // per-kind audit Action tag.

    [Theory]
    [InlineData("event", "t2-event")]
    [InlineData("project", "t2-project")]
    [InlineData("board", "t2-board")]
    [InlineData("todo", "t2-todo")]
    [InlineData("grouppost", "t2-grouppost")]
    public async Task FileReportTargetAsync_AllFiveKinds_WritesRows(string kind, string targetId)
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string reporter = "u-t2-reporter";

        await svc.FileReportTargetAsync(
            kind, targetId, ComponentId, "u-t2-author", reporter, "reason");

        await using (var s2 = store.QuerySession())
        {
            var filed = await s2.Query<Report>()
                .Where(r => r.TargetId == targetId)
                .ToListAsync(TestContext.Current.CancellationToken);
            var report = Assert.Single(filed);
            Assert.Equal(kind, report.TargetKind);
            Assert.Equal("filed", report.Status);
        }

        var rows = await AllAudits(store);
        Assert.Contains(rows, a => a.Action == $"report.{kind}.file"
                                        && a.TargetKind == kind
                                        && a.Outcome == AccessOutcome.Allow);
    }

    // ── T3 — FileReportTargetAsync_SelfReport_NoNotificationEmission ─────
    //
    // ADR 0173 (the FileReportAsync self-report pin carried over) — a
    // resident reporting their **own** event emits no ReportFiled
    // notification (the authorId == actorId skip); the report row and
    // audit row still commit (the emission skip is not a write skip).

    [Fact]
    public async Task FileReportTargetAsync_SelfReport_NoNotificationEmission()
    {
        var (store, svc, staged) = await BootTargetStoreAsync();
        const string author = "u-t3-author";
        await PlantProfile(store, author, "t3-author@kumunita");

        await Plant(store, new Kumunita.Core.Events.Event
        {
            Id = "t3-event", Title = "t3 event", AuthorId = author,
            Start = DateTimeOffset.UtcNow, End = DateTimeOffset.UtcNow,
        });

        await svc.FileReportTargetAsync(
            "event", "t3-event", ComponentId, author, author, "self");

        Assert.Empty(staged);

        await using (var s2 = store.QuerySession())
        {
            var filed = await s2.Query<Report>()
                .Where(r => r.TargetId == "t3-event")
                .ToListAsync(TestContext.Current.CancellationToken);
            Assert.Single(filed);
        }
    }

    // ── T4 — FileReportTargetAsync_ThirdPartyReport_EmitsNotification ────
    //
    // ADR 0173 (the FileReportAsync notification pin carried over) — a
    // report against another resident's event stages the ReportFiled
    // emission for the author with the §6.3 key shape
    // notification:report.filed:{reportId}.

    [Fact]
    public async Task FileReportTargetAsync_ThirdPartyReport_EmitsNotification()
    {
        var (store, svc, staged) = await BootTargetStoreAsync();
        const string author = "u-t4-author";
        const string reporter = "u-t4-reporter";
        await PlantProfile(store, author, "t4-author@kumunita");

        await Plant(store, new Kumunita.Core.Events.Event
        {
            Id = "t4-event", Title = "t4 event", AuthorId = author,
            Start = DateTimeOffset.UtcNow, End = DateTimeOffset.UtcNow,
        });

        await svc.FileReportTargetAsync(
            "event", "t4-event", ComponentId, author, reporter, "harassment");

        var email = Assert.Single(staged);
        Assert.StartsWith("notification:report.filed:", email.Key);
        Assert.Equal("t4-author@kumunita", email.Recipient);
    }

    // ── T5 — FileReportTargetAsync_UnknownKind_Throws_NoPartialWrite ─────
    //
    // ADR 0173 — the closed vocabulary is enforced at the service
    // boundary: an unknown kind throws ArgumentException **before** any
    // row is written (no report row, no audit row — no partial write).

    [Fact]
    public async Task FileReportTargetAsync_UnknownKind_Throws_NoPartialWrite()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.FileReportTargetAsync(
                "page", "t5-page", ComponentId, "u-t5-author", "u-t5-reporter", "x"));

        await using (var s2 = store.QuerySession())
        {
            var reports = await s2.Query<Report>().ToListAsync(TestContext.Current.CancellationToken);
            Assert.Empty(reports);
        }
        Assert.Empty(await AllAudits(store));
    }

    // ── R03-1 — ResolveReportAsync_WithOutcome_WritesFullResolutionShape ──
    //
    // IMPROVE-UX R03 (the D1 "My reports" shape; the lane-enforces-shape
    // second line of defense): the R03 seam carries the optional
    // (outcome, resolutionNote) pair; when both are supplied, the lane
    // writes the **full** resolution shape onto the Report row —
    // Outcome / ResolutionNote / ResolvedAt (UTC) / ResolvedBy
    // (the caller's SubjectId) — in the same transaction as the
    // lane's existing AccessAudit row. The four fields were
    // null before the call; after, all four are populated (the
    // "filed → resolved-with-outcome" transition, the ADR 0004 §B.1
    // additive pin: pre-R03 rows keep their nulls, the R03 lane's
    // write is the one that fills them).

    [Fact]
    public async Task ResolveReportAsync_WithOutcome_WritesFullResolutionShape()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string globalAdmin = "u-r03-1-admin";
        const string note = "removed — see the ADR 0142 discussion";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = "r03-1-assign", UserId = globalAdmin, ComponentId = ComponentId,
            GrantedBy = "u-r03-1-root", At = DateTimeOffset.UtcNow
        });
        await Plant(store, new Post
        {
            Id = "r03-1-post", ComponentId = ComponentId, AuthorId = "u-r03-1-author",
            Body = "body r03-1", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, "u-r03-1-member"),
        });
        await Plant(store, new Report
        {
            Id = "r03-1-report", PostId = "r03-1-post", ReporterId = "u-r03-1-reporter",
            ComponentId = ComponentId, Reason = "r03-1-reason", Status = "filed",
            At = DateTimeOffset.UtcNow,
            // The four resolution fields are null on a filed row (the
            // "filed → resolved" transition invariant).
            Outcome = null, ResolutionNote = null, ResolvedAt = null, ResolvedBy = null,
        });

        var before = DateTimeOffset.UtcNow;
        await RunInSession(store, async session =>
            await svc.ResolveReportAsync(
                "r03-1-report", globalAdmin, session,
                outcome: ReportOutcome.Removed,
                resolutionNote: note));
        var after = DateTimeOffset.UtcNow;

        // The four resolution fields are populated with the caller's
        // exact values (the D1 closed vocabulary + the verbatim note).
        await using (var s2 = store.QuerySession())
        {
            var report = await s2.LoadAsync<Report>("r03-1-report", TestContext.Current.CancellationToken);
            Assert.NotNull(report);
            Assert.Equal("resolved", report!.Status);          // pre-R03 literal pin unchanged
            Assert.Equal(ReportOutcome.Removed, report.Outcome);
            Assert.Equal(note, report.ResolutionNote);
            Assert.Equal(globalAdmin, report.ResolvedBy);
            Assert.NotNull(report.ResolvedAt);
            // ResolvedAt is the lane's own UTC instant — between the
            // test's before/after ticks (the "written in this call" pin).
            Assert.InRange(report.ResolvedAt!.Value, before, after);
        }
    }

    // ── R03-2 — ResolveReportAsync_WithOutcome_AuditRowUnchanged ─────────
    //
    // IMPROVE-UX R03 (the unit's pin, verbatim from the plan): the
    // existing <c>AccessAudit</c> row is **unchanged** — same shape,
    // same action/target, same outcome. The test asserts the
    // full field set of the lane's own row (the
    // TargetKind = "report" lane row, distinct from the M1-seam's
    // TargetKind = "post" row) against the pre-R03 shape — the
    // audit row's shape is the frozen M3b shape (C-M3b·4), not
    // something R03 reshapes.

    [Fact]
    public async Task ResolveReportAsync_WithOutcome_AuditRowUnchanged()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string globalAdmin = "u-r03-2-admin";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = "r03-2-assign", UserId = globalAdmin, ComponentId = ComponentId,
            GrantedBy = "u-r03-2-root", At = DateTimeOffset.UtcNow
        });
        await Plant(store, new Post
        {
            Id = "r03-2-post", ComponentId = ComponentId, AuthorId = "u-r03-2-author",
            Body = "body r03-2", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, "u-r03-2-member"),
        });
        await Plant(store, new Report
        {
            Id = "r03-2-report", PostId = "r03-2-post", ReporterId = "u-r03-2-reporter",
            ComponentId = ComponentId, Reason = "r03-2-reason", Status = "filed",
            At = DateTimeOffset.UtcNow
        });

        await RunInSession(store, async session =>
            await svc.ResolveReportAsync(
                "r03-2-report", globalAdmin, session,
                outcome: ReportOutcome.Hidden,
                resolutionNote: "hidden — see ADR 0142"));

        // The lane's own audit row (TargetKind = "report") is the
        // pre-R03 shape, byte-for-byte — the R03 pin.
        var rows = await ReportAudits(store, actor: globalAdmin);
        var row = Assert.Single(rows, a => a.Action == "report.resolve");
        Assert.Equal("report", row.TargetKind);
        Assert.Equal("r03-2-report", row.TargetId);
        Assert.Equal(globalAdmin, row.ActorId);
        Assert.Equal(globalAdmin, row.EffectivePrincipalId);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
        Assert.NotEqual(AccessOutcome.Deny, row.Outcome);
    }

    // ── R03-3 — ResolveReportAsync_NoteOnly_WritesResolutionShape ────────
    //
    // IMPROVE-UX R03 — the note-without-outcome shape (the moderator
    // closes the report with a free-text note but no closed-vocabulary
    // action, e.g. the "we've reviewed it" shape the D1
    // <see cref="ReportOutcome.Other"/> lane would use): the lane
    // still writes the full four-field shape, with Outcome null
    // (the "no outcome recorded" state) and the note verbatim.

    [Fact]
    public async Task ResolveReportAsync_NoteOnly_WritesResolutionShape()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string globalAdmin = "u-r03-3-admin";
        const string note = "reviewed — no action taken, see the discussion";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = "r03-3-assign", UserId = globalAdmin, ComponentId = ComponentId,
            GrantedBy = "u-r03-3-root", At = DateTimeOffset.UtcNow
        });
        await Plant(store, new Post
        {
            Id = "r03-3-post", ComponentId = ComponentId, AuthorId = "u-r03-3-author",
            Body = "body r03-3", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, "u-r03-3-member"),
        });
        await Plant(store, new Report
        {
            Id = "r03-3-report", PostId = "r03-3-post", ReporterId = "u-r03-3-reporter",
            ComponentId = ComponentId, Reason = "r03-3-reason", Status = "filed",
            At = DateTimeOffset.UtcNow
        });

        await RunInSession(store, async session =>
            await svc.ResolveReportAsync(
                "r03-3-report", globalAdmin, session,
                outcome: null,
                resolutionNote: note));

        await using (var s2 = store.QuerySession())
        {
            var report = await s2.LoadAsync<Report>("r03-3-report", TestContext.Current.CancellationToken);
            Assert.NotNull(report);
            Assert.Equal("resolved", report!.Status);
            Assert.Null(report.Outcome);           // note-only shape — Outcome stays null
            Assert.Equal(note, report.ResolutionNote);
            Assert.Equal(globalAdmin, report.ResolvedBy);
            Assert.NotNull(report.ResolvedAt);
        }
    }

    // ── R03-4 — ResolveReportAsync_PreR03Shape_KeepsResolutionFieldsNull ─
    //
    // IMPROVE-UX R03 (the backward-compat pin): the pre-R03 two-
    // parameter call shape (no outcome, no note) keeps compiling and
    // behaving unchanged — the lane flips Status to "resolved" but
    // the four resolution fields stay null (the "resolved with no
    // outcome recorded" legacy shape, the ModerationController's
    // ResolvePost action's shape).

    [Fact]
    public async Task ResolveReportAsync_PreR03Shape_KeepsResolutionFieldsNull()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string globalAdmin = "u-r03-4-admin";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = "r03-4-assign", UserId = globalAdmin, ComponentId = ComponentId,
            GrantedBy = "u-r03-4-root", At = DateTimeOffset.UtcNow
        });
        await Plant(store, new Post
        {
            Id = "r03-4-post", ComponentId = ComponentId, AuthorId = "u-r03-4-author",
            Body = "body r03-4", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, "u-r03-4-member"),
        });
        await Plant(store, new Report
        {
            Id = "r03-4-report", PostId = "r03-4-post", ReporterId = "u-r03-4-reporter",
            ComponentId = ComponentId, Reason = "r03-4-reason", Status = "filed",
            At = DateTimeOffset.UtcNow
        });

        // The pre-R03 call shape (no outcome, no note) — the
        // ModerationController's ResolvePost action's shape.
        await RunInSession(store, async session =>
            await svc.ResolveReportAsync("r03-4-report", globalAdmin, session));

        await using (var s2 = store.QuerySession())
        {
            var report = await s2.LoadAsync<Report>("r03-4-report", TestContext.Current.CancellationToken);
            Assert.NotNull(report);
            Assert.Equal("resolved", report!.Status);       // the pre-R03 literal pin (unchanged)
            Assert.Null(report.Outcome);                    // the R03 pin — no outcome recorded
            Assert.Null(report.ResolutionNote);
            Assert.Null(report.ResolvedAt);
            Assert.Null(report.ResolvedBy);
        }

        // The pre-R03 audit row is unchanged (the same "unchanged" pin
        // as R03-2 — this is the pre-R03 shape the R04/R05 read paths
        // treat as the "legacy just closed" shape).
        var rows = await ReportAudits(store, actor: globalAdmin);
        Assert.Contains(rows, a => a.Action == "report.resolve"
                                   && a.TargetId == "r03-4-report"
                                   && a.Outcome == AccessOutcome.Allow);
    }

    // ── R03-5 — ResolveReportAsync_NoteOverCap_Throws_NoPartialWrite ─────
    //
    // IMPROVE-UX R03 (the lane-enforces-shape second line of defense):
    // a ResolutionNote over the D1 ≤500-char cap is a caller contract
    // violation — the lane throws ArgumentException **before** any
    // row is written (no Status flip, no audit row, no resolution
    // shape — no partial write). The R04 form's own maxlength
    // validation is the first line; this is the lane's.

    [Fact]
    public async Task ResolveReportAsync_NoteOverCap_Throws_NoPartialWrite()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string globalAdmin = "u-r03-5-admin";
        var tooLong = new string('x', 501);   // 501 — one over the 500-char cap

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = "r03-5-assign", UserId = globalAdmin, ComponentId = ComponentId,
            GrantedBy = "u-r03-5-root", At = DateTimeOffset.UtcNow
        });
        await Plant(store, new Post
        {
            Id = "r03-5-post", ComponentId = ComponentId, AuthorId = "u-r03-5-author",
            Body = "body r03-5", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, "u-r03-5-member"),
        });
        await Plant(store, new Report
        {
            Id = "r03-5-report", PostId = "r03-5-post", ReporterId = "u-r03-5-reporter",
            ComponentId = ComponentId, Reason = "r03-5-reason", Status = "filed",
            At = DateTimeOffset.UtcNow
        });

        // The lane throws before any row is written (the
        // ResolveReportNote normalization runs before the session
        // load — the lane-enforces-shape invariant).
        await RunInSession(store, async session =>
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                svc.ResolveReportAsync(
                    "r03-5-report", globalAdmin, session,
                    outcome: ReportOutcome.Other,
                    resolutionNote: tooLong));
        });

        // No partial write — the Status is still "filed" (not
        // flipped to "resolved"), the four resolution fields are
        // still null, and no lane-owned audit row was committed.
        await using (var s2 = store.QuerySession())
        {
            var report = await s2.LoadAsync<Report>("r03-5-report", TestContext.Current.CancellationToken);
            Assert.NotNull(report);
            Assert.Equal("filed", report!.Status);
            Assert.Null(report.Outcome);
            Assert.Null(report.ResolutionNote);
            Assert.Null(report.ResolvedAt);
            Assert.Null(report.ResolvedBy);
        }
        Assert.Empty(await ReportAudits(store, actor: globalAdmin));
    }

    // ── R03-6 — ResolveReportAsync_NoteAtCap_WritesVerbatim ──────────────
    //
    // IMPROVE-UX R03 (the cap boundary pin): a ResolutionNote of
    // exactly 500 chars is **accepted** (the cap is ≤500, not
    // <500) and stored verbatim (no trim, no truncation — the
    // "lane-enforces-shape" invariant allows the boundary value,
    // the "R04 form's own maxlength" invariant is the first line,
    // this test pins the lane's second line accepts the boundary).

    [Fact]
    public async Task ResolveReportAsync_NoteAtCap_WritesVerbatim()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string globalAdmin = "u-r03-6-admin";
        var atCap = new string('a', 500);     // exactly 500 — the boundary

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = "r03-6-assign", UserId = globalAdmin, ComponentId = ComponentId,
            GrantedBy = "u-r03-6-root", At = DateTimeOffset.UtcNow
        });
        await Plant(store, new Post
        {
            Id = "r03-6-post", ComponentId = ComponentId, AuthorId = "u-r03-6-author",
            Body = "body r03-6", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, "u-r03-6-member"),
        });
        await Plant(store, new Report
        {
            Id = "r03-6-report", PostId = "r03-6-post", ReporterId = "u-r03-6-reporter",
            ComponentId = ComponentId, Reason = "r03-6-reason", Status = "filed",
            At = DateTimeOffset.UtcNow
        });

        await RunInSession(store, async session =>
            await svc.ResolveReportAsync(
                "r03-6-report", globalAdmin, session,
                outcome: ReportOutcome.NoAction,
                resolutionNote: atCap));

        await using (var s2 = store.QuerySession())
        {
            var report = await s2.LoadAsync<Report>("r03-6-report", TestContext.Current.CancellationToken);
            Assert.NotNull(report);
            Assert.Equal("resolved", report!.Status);
            Assert.Equal(ReportOutcome.NoAction, report.Outcome);
            Assert.Equal(500, report.ResolutionNote!.Length);   // the boundary pin — verbatim
            Assert.Equal(atCap, report.ResolutionNote);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // IMPROVE-UX A02 — the outcome-execution lane (the D6 "the outcome
    // picker performs the action" promise; the E1/E2 S-severity loop
    // closer). R03 records the outcome on the Report row; A02 makes
    // that record **change something visible** — it re-derives the same
    // action on the target post and runs it in the **caller's session**
    // (the C3 same-transaction lane, ADR 0006-C). The A01 read branch is
    // what actually makes the post disappear from feed/detail; A02
    // writes the PostStatus the reads honour.
    //
    // The "post targets only" guard (the plan card's scope + the A02
    // trap): the hook requires a non-empty `Report.PostId` — the original
    // M3b / ADR 0023 post + reply shapes carry their id there, but the new
    // ADR 0173 lanes (event / project / board / todo) set
    // `PostId = string.Empty` and carry their id in `TargetId`, so a
    // non-post target records the outcome + note and executes **nothing**
    // (per-target-kind execution is A04's scope).
    // ═════════════════════════════════════════════════════════════════════

    // ── A02-1 — ResolveReportAsync_OutcomeHidden_PostTarget_FlipsPostHidden ─
    //
    // A02 DoD, verbatim from the plan card ("resolving with Hidden hides
    // the post"): resolving a **post-target** report (the original M3b
    // shape — `Report.PostId` set) with `ReportOutcome.Hidden` re-derives
    // `PostService.HidePostAsync` in the **caller's session**, so the
    // post's `Status` flips to the exact `PostStatus.Hidden` literal. The
    // write-lane audit row (`Action = "moderate"`, `TargetId = the post
    // id`, `Outcome = Allow`) is committed in the **same** transaction as
    // the lane's own `report.resolve` row (the R03 pin — that row is
    // unchanged; these are additional M3b write-lane rows, not a reshape
    // of the resolve row).

    [Fact]
    public async Task A02_ResolveReport_OutcomeHidden_PostTarget_FlipsPostHidden()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = ServicesWithPosts(store);
        const string admin = "u-a02-1-admin";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = "a02-1-assign", UserId = admin, ComponentId = ComponentId,
            GrantedBy = "u-a02-1-root", At = DateTimeOffset.UtcNow
        });
        await Plant(store, new Post
        {
            Id = "a02-1-post", ComponentId = ComponentId, AuthorId = "u-a02-1-author",
            Body = "body a02-1", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, "u-a02-1-member"),
            Status = PostStatus.Active,
        });
        await Plant(store, new Report
        {
            Id = "a02-1-report", PostId = "a02-1-post", ReporterId = "u-a02-1-reporter",
            ComponentId = ComponentId, Reason = "a02-1-reason", Status = "filed",
            At = DateTimeOffset.UtcNow,
        });

        await RunInSession(store, async session =>
            await svc.ResolveReportAsync(
                "a02-1-report", admin, session,
                outcome: ReportOutcome.Hidden,
                resolutionNote: "hidden per community policy"));

        // The outcome is recorded on the Report row (the R03 shape)…
        await using var s1 = store.QuerySession();
        var report = await s1.LoadAsync<Report>("a02-1-report", TestContext.Current.CancellationToken);
        Assert.NotNull(report);
        Assert.Equal("resolved", report!.Status);
        Assert.Equal(ReportOutcome.Hidden, report.Outcome);

        // …AND the post actually flips to Hidden (the A02 DoD pin).
        await using var s2 = store.QuerySession();
        var post = await s2.LoadAsync<Post>("a02-1-post", TestContext.Current.CancellationToken);
        Assert.Equal(PostStatus.Hidden, post!.Status);

        // The M3b write-lane audit row (Action = "moderate", the post id,
        // Allow) — an **additional** row in the same transaction (the A02
        // same-transaction pin).
        var postRows = await PostAudits(store, actor: admin);
        Assert.Contains(postRows, a => a.Action == AccessAction.Moderate.Id
                                       && a.TargetId == "a02-1-post"
                                       && a.Outcome == AccessOutcome.Allow);

        // The R03 pin — the resolve row is the pre-A02 shape, unchanged.
        var reportRows = await ReportAudits(store, actor: admin);
        var resolveRow = Assert.Single(reportRows, a => a.Action == "report.resolve");
        Assert.Equal("report", resolveRow.TargetKind);
        Assert.Equal("a02-1-report", resolveRow.TargetId);
        Assert.Equal(AccessOutcome.Allow, resolveRow.Outcome);
    }

    // ── A02-2 — ResolveReportAsync_OutcomeRemoved_PostTarget_FlipsPostRemoved ─
    //
    // A02 — same shape as A02-1 (the hide lane) but the hard lane:
    // resolving a **post-target** report with `ReportOutcome.Removed`
    // re-derives `PostService.RemovePostAsync`, so the post's `Status`
    // flips to the exact `PostStatus.Removed` literal. The write-lane
    // audit row (`Action = "moderate"`, `TargetId = the post id`,
    // `Outcome = Allow`) is committed in the same transaction (the A02
    // same-transaction pin; the resolve row is unchanged — the R03 pin).

    [Fact]
    public async Task A02_ResolveReport_OutcomeRemoved_PostTarget_FlipsPostRemoved()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = ServicesWithPosts(store);
        const string admin = "u-a02-2-admin";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = "a02-2-assign", UserId = admin, ComponentId = ComponentId,
            GrantedBy = "u-a02-2-root", At = DateTimeOffset.UtcNow
        });
        await Plant(store, new Post
        {
            Id = "a02-2-post", ComponentId = ComponentId, AuthorId = "u-a02-2-author",
            Body = "body a02-2", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, "u-a02-2-member"),
            Status = PostStatus.Active,
        });
        await Plant(store, new Report
        {
            Id = "a02-2-report", PostId = "a02-2-post", ReporterId = "u-a02-2-reporter",
            ComponentId = ComponentId, Reason = "a02-2-reason", Status = "filed",
            At = DateTimeOffset.UtcNow,
        });

        await RunInSession(store, async session =>
            await svc.ResolveReportAsync(
                "a02-2-report", admin, session,
                outcome: ReportOutcome.Removed,
                resolutionNote: "removed — content policy"));

        await using var s1 = store.QuerySession();
        var report = await s1.LoadAsync<Report>("a02-2-report", TestContext.Current.CancellationToken);
        Assert.Equal(ReportOutcome.Removed, report!.Outcome);

        // The post flips to the exact PostStatus.Removed literal (the A02
        // hard-lane pin).
        await using var s2 = store.QuerySession();
        var post = await s2.LoadAsync<Post>("a02-2-post", TestContext.Current.CancellationToken);
        Assert.Equal(PostStatus.Removed, post!.Status);

        var postRows = await PostAudits(store, actor: admin);
        Assert.Contains(postRows, a => a.Action == AccessAction.Moderate.Id
                                       && a.TargetId == "a02-2-post"
                                       && a.Outcome == AccessOutcome.Allow);
    }

    // ── A02-3 — ResolveReportAsync_OutcomeNoAction_PostTarget_PostUnchanged ─
    //
    // A02 (the no-action pin): resolving a **post-target** report with
    // `ReportOutcome.NoAction` records the outcome on the Report row but
    // executes **nothing** on the post (the `NoAction` / `Other`
    // no-action shape — the note is the explanation). The post's `Status`
    // stays `PostStatus.Active` (unchanged) — that is the pin. (The
    // resolve lane's own `CanAsync(Moderate)` decision also writes a
    // `moderate` audit row against the post's resource, so the write-lane
    // is discriminated by the post's Status, not the presence of a
    // `moderate` row.) The resolve row is still written (the R03 pin).

    [Fact]
    public async Task A02_ResolveReport_OutcomeNoAction_PostTarget_PostUnchanged()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = ServicesWithPosts(store);
        const string admin = "u-a02-3-admin";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = "a02-3-assign", UserId = admin, ComponentId = ComponentId,
            GrantedBy = "u-a02-3-root", At = DateTimeOffset.UtcNow
        });
        await Plant(store, new Post
        {
            Id = "a02-3-post", ComponentId = ComponentId, AuthorId = "u-a02-3-author",
            Body = "body a02-3", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, "u-a02-3-member"),
            Status = PostStatus.Active,
        });
        await Plant(store, new Report
        {
            Id = "a02-3-report", PostId = "a02-3-post", ReporterId = "u-a02-3-reporter",
            ComponentId = ComponentId, Reason = "a02-3-reason", Status = "filed",
            At = DateTimeOffset.UtcNow,
        });

        await RunInSession(store, async session =>
            await svc.ResolveReportAsync(
                "a02-3-report", admin, session,
                outcome: ReportOutcome.NoAction,
                resolutionNote: "reviewed — no action taken"));

        // The outcome is recorded (NoAction)…
        await using var s1 = store.QuerySession();
        var report = await s1.LoadAsync<Report>("a02-3-report", TestContext.Current.CancellationToken);
        Assert.Equal(ReportOutcome.NoAction, report!.Outcome);

        // …but the post is untouched (still Active) — the no-action pin
        // (HidePostAsync / RemovePostAsync never ran, so no Status write).
        await using var s2 = store.QuerySession();
        var post = await s2.LoadAsync<Post>("a02-3-post", TestContext.Current.CancellationToken);
        Assert.Equal(PostStatus.Active, post!.Status);

        // The resolve row is still written (the lane's own row).
        var reportRows = await ReportAudits(store, actor: admin);
        Assert.Contains(reportRows, a => a.Action == "report.resolve"
                                         && a.Outcome == AccessOutcome.Allow);
    }

    // ── A02-4 — ResolveReportAsync_OutcomeHidden_EventTarget_RecordsOutcomeOnly ─
    //
    // A02 (the "post targets only" trap, the plan card's explicit
    // guard): resolving a **non-post** target (an `event` — the new ADR
    // 0173 shape where `Report.PostId` is `string.Empty` and the target's
    // id lives in `Report.TargetId`) with `ReportOutcome.Hidden` records
    // the outcome on the Report row but executes **nothing** — the
    // `!string.IsNullOrEmpty(report.PostId)` guard blocks the
    // `HidePostAsync` call (there is no post to hide; per-target-kind
    // execution is A04's scope). To prove the guard is on `PostId` (not
    // "is there a post in the store"), a **decoy post** is planted and
    // must stay `Active` — the event-target resolve must not touch it.

    [Fact]
    public async Task A02_ResolveReport_OutcomeHidden_EventTarget_RecordsOutcomeOnly()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = ServicesWithPosts(store);
        const string admin = "u-a02-4-admin";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = "a02-4-assign", UserId = admin, ComponentId = ComponentId,
            GrantedBy = "u-a02-4-root", At = DateTimeOffset.UtcNow
        });
        // The event target (the ADR 0173 shape: TargetKind = "event",
        // TargetId = the event's id). The event's ComponentId is the
        // moderation gate (the Decide branch-2 shape).
        await Plant(store, new Kumunita.Core.Events.Event
        {
            Id = "a02-4-event", ComponentId = ComponentId, AuthorId = "u-a02-4-author",
            Title = "event a02-4", Body = "body a02-4",
            Start = DateTimeOffset.UtcNow, End = DateTimeOffset.UtcNow.AddHours(1),
            Created = DateTimeOffset.UtcNow,
        });
        // A DECOY post — planted to prove the guard is on Report.PostId
        // (not "is there a post in the store"). It must stay Active.
        await Plant(store, new Post
        {
            Id = "a02-4-decoy-post", ComponentId = ComponentId, AuthorId = "u-a02-4-decoy-author",
            Body = "decoy a02-4", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, "u-a02-4-decoy-member"),
            Status = PostStatus.Active,
        });
        // The event-target report: PostId is EMPTY (the ADR 0173 new-lane
        // shape — FileReportTargetAsync sets it string.Empty), the
        // target's id is in TargetId.
        await Plant(store, new Report
        {
            Id = "a02-4-report", PostId = string.Empty, ReporterId = "u-a02-4-reporter",
            ComponentId = ComponentId, Reason = "a02-4-reason", Status = "filed",
            At = DateTimeOffset.UtcNow,
            TargetKind = "event", TargetId = "a02-4-event",
        });

        await RunInSession(store, async session =>
            await svc.ResolveReportAsync(
                "a02-4-report", admin, session,
                outcome: ReportOutcome.Hidden,
                resolutionNote: "event — hide is A04's scope"));

        // The outcome is recorded on the Report row (the lane recorded it)…
        await using var s1 = store.QuerySession();
        var report = await s1.LoadAsync<Report>("a02-4-report", TestContext.Current.CancellationToken);
        Assert.Equal(ReportOutcome.Hidden, report!.Outcome);
        Assert.Equal("resolved", report.Status);
        Assert.Equal(string.Empty, report.PostId);   // the "post targets only" guard key

        // …but the decoy post is untouched (still Active) — the guard
        // blocked the HidePostAsync call (the A02 trap pin).
        await using var s2 = store.QuerySession();
        var decoy = await s2.LoadAsync<Post>("a02-4-decoy-post", TestContext.Current.CancellationToken);
        Assert.Equal(PostStatus.Active, decoy!.Status);

        // No write-lane (moderate) row committed for the decoy post.
        var postRows = await PostAudits(store, actor: admin);
        Assert.DoesNotContain(postRows, a => a.TargetId == "a02-4-decoy-post");

        // The resolve row is still written (the lane's own row).
        var reportRows = await ReportAudits(store, actor: admin);
        Assert.Contains(reportRows, a => a.Action == "report.resolve"
                                         && a.Outcome == AccessOutcome.Allow);
    }

    // ── A02-5 — ResolveReportAsync_OutcomeHidden_ReplyTarget_HidesParentPost ─
    //
    // A02 (the reply-report shape, the plan card's TODO ❓ read): a
    // **reply-targeted** report (ADR 0023 — `Report.ReplyId` set,
    // `Report.PostId` = the parent post's id, the C-M3·1 reply-inherits
    // rule) with `ReportOutcome.Hidden` resolves the parent post — the
    // `PostId` is set (the reply's parent, the existing comment's lever),
    // so the guard passes and `HidePostAsync` runs on the **parent post**
    // (not the reply — a reply-level hide would be A04's scope). The
    // parent post's `Status` flips to `PostStatus.Hidden`; the reply is
    // itself untouched (it has no own Status field to flip).

    [Fact]
    public async Task A02_ResolveReport_OutcomeHidden_ReplyTarget_HidesParentPost()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = ServicesWithPosts(store);
        const string admin = "u-a02-5-admin";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = "a02-5-assign", UserId = admin, ComponentId = ComponentId,
            GrantedBy = "u-a02-5-root", At = DateTimeOffset.UtcNow
        });
        await Plant(store, new Post
        {
            Id = "a02-5-post", ComponentId = ComponentId, AuthorId = "u-a02-5-author",
            Body = "body a02-5", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, "u-a02-5-member"),
            Status = PostStatus.Active,
        });
        await Plant(store, new PostReply
        {
            Id = "a02-5-reply", PostId = "a02-5-post", AuthorId = "u-a02-5-replier",
            Body = "reply body a02-5", Created = DateTimeOffset.UtcNow, LanguageCode = "en",
        });
        // A reply-targeted report (ADR 0023): ReplyId set, PostId = the
        // parent post (C-M3·1 reply-inherits).
        await Plant(store, new Report
        {
            Id = "a02-5-report", PostId = "a02-5-post", ReplyId = "a02-5-reply",
            ReporterId = "u-a02-5-reporter",
            ComponentId = ComponentId, Reason = "a02-5-reason", Status = "filed",
            At = DateTimeOffset.UtcNow,
        });

        await RunInSession(store, async session =>
            await svc.ResolveReportAsync(
                "a02-5-report", admin, session,
                outcome: ReportOutcome.Hidden,
                resolutionNote: "hidden — reply report resolves the parent post"));

        // The outcome is recorded…
        await using var s1 = store.QuerySession();
        var report = await s1.LoadAsync<Report>("a02-5-report", TestContext.Current.CancellationToken);
        Assert.Equal(ReportOutcome.Hidden, report!.Outcome);
        Assert.Equal("a02-5-reply", report.ReplyId);   // the reply discriminator is intact

        // …and the parent post (Report.PostId) flips to Hidden — the
        // existing comment's lever (the reply-level hide is A04's scope).
        await using var s2 = store.QuerySession();
        var post = await s2.LoadAsync<Post>("a02-5-post", TestContext.Current.CancellationToken);
        Assert.Equal(PostStatus.Hidden, post!.Status);

        var postRows = await PostAudits(store, actor: admin);
        Assert.Contains(postRows, a => a.Action == AccessAction.Moderate.Id
                                       && a.TargetId == "a02-5-post"
                                       && a.Outcome == AccessOutcome.Allow);
    }

    // ═════════════════════════════════════════════════════════════════════
    // IMPROVE-UX A04 — grouppost-target outcome-execution (the A02 guard
    // flipped for the "closest copy" — a `Post` doc with `GroupId` set, the
    // ADR 0013 shape). A02 recorded the outcome on the Report row but
    // executed **nothing** for grouppost targets (the `PostId` guard blocked
    // it; the target's id lives in `TargetId`, not `PostId`). A04 adds the
    // `grouppost` branch in the execution block: when
    // `Report.TargetKind == "grouppost"` and `Report.TargetId` is non-empty,
    // the same `HidePostAsync` / `RemovePostAsync` write lanes run on that
    // id. The post / reply shape (the `PostId` branch) is unchanged; the
    // `event` / `project` / `board` / `todo` shapes remain records-only (the
    // plan card's "the rest listed as follow-ups" line).
    //
    // **Gate reality (the `// TODO ❓` documented in the execution block):**
    // a group post's `Post.ComponentId` is `string.Empty` (ADR 0013 G·2 lane
    // exclusivity) and its `Audience` is empty (G·8). The
    // `AuthorizationService.Decide` function has no group-scope moderation
    // branch and no `GlobalAdmin` branch (the role is Web-layer-only, not in
    // Core's `Decide`). A regular GlobalAdmin resolving a grouppost report
    // is **denied** at the Core's `CanAsync(Moderate)` gate. Only a break-
    // glass `AdminOverride` row (branch 3) or the author (branch 1) passes.
    // These tests seed the break-glass row to prove the execution block works
    // when the gate passes; the `// TODO ❓` documents the design gap for
    // the standing-moderator shape (Option B, ADR 0003 SoD) as a follow-up.
    // ═════════════════════════════════════════════════════════════════════

    // ── A04-1 — ResolveReportAsync_OutcomeHidden_GroupPostTarget_FlipsGroupPostHidden ─
    //
    // A04 DoD pin ("one more kind hideable"): resolving a **grouppost-
    // targeted** report (`Report.PostId = string.Empty`,
    // `Report.TargetKind = "grouppost"`, `Report.TargetId = the group post
    // id`) with `ReportOutcome.Hidden` executes `HidePostAsync` on the
    // **group post's id** (via the A04 `grouppost` branch in the execution
    // block), so the group post's `Post.Status` flips to `PostStatus.Hidden`.
    // The break-glass `AdminOverride` row is seeded so the Core's
    // `CanAsync(Moderate)` gate passes (the A04 `// TODO ❓` — a regular
    // GlobalAdmin is denied; only break-glass or the author passes for a
    // `ComponentId = string.Empty` / empty-audience resource). The `report.
    // resolve` audit row is also committed (the lane's own row, the R03 pin
    // unchanged).

    [Fact]
    public async Task A04_ResolveReport_OutcomeHidden_GroupPostTarget_FlipsGroupPostHidden()
    {
        var (store, conn) = await BootStoreAsyncWithConn();
        var (userInfo, _, svc) = ServicesWithPosts(store);
        const string admin = "u-a04-1-admin";
        const string author = "u-a04-1-author";
        var now = DateTimeOffset.UtcNow;

        // Break-glass row (the Core-layer equivalent of the Web GlobalAdmin
        // role — the A04 `// TODO ❓` documents that a regular GlobalAdmin
        // is denied at the Core gate for a ComponentId=string.Empty resource;
        // only break-glass or the author passes).
        await SeedAdminOverrideAsync(conn, admin, "tok-a04-1",
            grantedAt: now.AddHours(-2), expiresAt: now.AddHours(+2),
            consumedAt: now.AddHours(-1));

        // The group post (ADR 0013 G·2/G·8 shape: ComponentId=empty,
        // GroupId set, Audience non-null empty).
        var group = await userInfo.CreateGroupAsync(author, "A04 test group", null);
        await Plant(store, new Post
        {
            Id = "a04-1-gp", GroupId = group.Id, ComponentId = string.Empty,
            AuthorId = author,
            Body = "body a04-1", Created = DateTimeOffset.UtcNow,
            Audience = new Authorization.Audience(),   // G·8 — non-null empty
            Status = PostStatus.Active,
        });
        // The grouppost-targeted report (ADR 0173 new-lane shape:
        // PostId = string.Empty, TargetKind = "grouppost", TargetId = post id).
        await Plant(store, new Report
        {
            Id = "a04-1-report", PostId = string.Empty,
            ReporterId = "u-a04-1-reporter",
            Reason = "a04-1-reason", Status = "filed",
            At = DateTimeOffset.UtcNow,
            TargetKind = "grouppost", TargetId = "a04-1-gp",
        });

        await RunInSession(store, async session =>
            await svc.ResolveReportAsync(
                "a04-1-report", admin, session,
                outcome: ReportOutcome.Hidden,
                resolutionNote: "hidden per group policy"));

        // The outcome is recorded on the Report row (the R03 shape, intact).
        await using var s1 = store.QuerySession();
        var report = await s1.LoadAsync<Report>("a04-1-report", TestContext.Current.CancellationToken);
        Assert.NotNull(report);
        Assert.Equal("resolved", report!.Status);
        Assert.Equal(ReportOutcome.Hidden, report.Outcome);
        Assert.Equal(string.Empty, report.PostId);        // the ADR 0173 new-lane shape
        Assert.Equal("grouppost", report.TargetKind);
        Assert.Equal("a04-1-gp", report.TargetId);

        // **A04 DoD pin**: the group post actually flips to Hidden.
        await using var s2 = store.QuerySession();
        var gp = await s2.LoadAsync<Post>("a04-1-gp", TestContext.Current.CancellationToken);
        Assert.Equal(PostStatus.Hidden, gp!.Status);

        // The M3b write-lane audit row (Action = "moderate", TargetId = the
        // group post id, Outcome = Allow) — an **additional** row in the same
        // transaction (the A02 same-transaction pin, extended to A04).
        var postRows = await PostAudits(store, actor: admin);
        Assert.Contains(postRows, a => a.Action == AccessAction.Moderate.Id
                                       && a.TargetId == "a04-1-gp"
                                       && a.Outcome == AccessOutcome.Allow);

        // The lane's own resolve row (the R03 pin — unchanged shape).
        var reportRows = await ReportAudits(store, actor: admin);
        var resolveRow = Assert.Single(reportRows, a => a.Action == "report.resolve");
        Assert.Equal("a04-1-report", resolveRow.TargetId);
        Assert.Equal(AccessOutcome.Allow, resolveRow.Outcome);
    }

    // ── A04-3 — ResolveReportAsync_OutcomeRemoved_GroupPostTarget_FlipsGroupPostRemoved ─
    //
    // A04 (the `Removed` symmetry, the F4 hard lane): resolving a
    // **grouppost-targeted** report with `ReportOutcome.Removed` executes
    // `RemovePostAsync` on the group post's id, so the group post's
    // `Post.Status` flips to `PostStatus.Removed` (the A04 "hideable" pin's
    // `Removed` sibling; the F4 hard lane is the same shape — irreversible
    // by design, the A03 `UnhidePostAsync` does not undo `Removed`).

    [Fact]
    public async Task A04_ResolveReport_OutcomeRemoved_GroupPostTarget_FlipsGroupPostRemoved()
    {
        var (store, conn) = await BootStoreAsyncWithConn();
        var (userInfo, _, svc) = ServicesWithPosts(store);
        const string admin = "u-a04-3-admin";
        const string author = "u-a04-3-author";
        var now = DateTimeOffset.UtcNow;

        await SeedAdminOverrideAsync(conn, admin, "tok-a04-3",
            grantedAt: now.AddHours(-2), expiresAt: now.AddHours(+2),
            consumedAt: now.AddHours(-1));

        var group = await userInfo.CreateGroupAsync(author, "A04-3 test group", null);
        await Plant(store, new Post
        {
            Id = "a04-3-gp", GroupId = group.Id, ComponentId = string.Empty,
            AuthorId = author,
            Body = "body a04-3", Created = DateTimeOffset.UtcNow,
            Audience = new Authorization.Audience(),
            Status = PostStatus.Active,
        });
        await Plant(store, new Report
        {
            Id = "a04-3-report", PostId = string.Empty,
            ReporterId = "u-a04-3-reporter",
            Reason = "a04-3-reason", Status = "filed",
            At = DateTimeOffset.UtcNow,
            TargetKind = "grouppost", TargetId = "a04-3-gp",
        });

        await RunInSession(store, async session =>
            await svc.ResolveReportAsync(
                "a04-3-report", admin, session,
                outcome: ReportOutcome.Removed,
                resolutionNote: "removed — content policy"));

        await using var s1 = store.QuerySession();
        var report = await s1.LoadAsync<Report>("a04-3-report", TestContext.Current.CancellationToken);
        Assert.Equal(ReportOutcome.Removed, report!.Outcome);

        // **A04 hard-lane pin**: the group post flips to Removed.
        await using var s2 = store.QuerySession();
        var gp = await s2.LoadAsync<Post>("a04-3-gp", TestContext.Current.CancellationToken);
        Assert.Equal(PostStatus.Removed, gp!.Status);

        var postRows = await PostAudits(store, actor: admin);
        Assert.Contains(postRows, a => a.Action == AccessAction.Moderate.Id
                                       && a.TargetId == "a04-3-gp"
                                       && a.Outcome == AccessOutcome.Allow);
    }

    // ── A04-2 — ResolveReportAsync_OutcomeHidden_EventTarget_StillRecordsOnly ─
    //
    // A04 (the "the rest listed as follow-ups" regression pin): an
    // **event-targeted** report with `ReportOutcome.Hidden` still records
    // the outcome on the Report row but executes **nothing** (the `event`
    // branch is **not** in A04's execution block — only `grouppost` was
    // added). The A02 `A02_…_EventTarget_RecordsOutcomeOnly` test already
    // pins this; A04-2 re-pins it here to confirm A04 did not accidentally
    // extend execution to `event` (the guard is `TargetKind == "grouppost"`,
    // not "any non-post target"). A decoy post must stay `Active` (the
    // A02 trap shape, unchanged).

    [Fact]
    public async Task A04_ResolveReport_OutcomeHidden_EventTarget_StillRecordsOnly()
    {
        var (store, _) = await BootStoreAsyncWithConn();
        var (_, _, svc) = ServicesWithPosts(store);
        const string admin = "u-a04-2-admin";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = "a04-2-assign", UserId = admin, ComponentId = ComponentId,
            GrantedBy = "u-a04-2-root", At = DateTimeOffset.UtcNow
        });
        await Plant(store, new Kumunita.Core.Events.Event
        {
            Id = "a04-2-event", ComponentId = ComponentId, AuthorId = "u-a04-2-author",
            Title = "event a04-2", Body = "body a04-2",
            Start = DateTimeOffset.UtcNow, End = DateTimeOffset.UtcNow.AddHours(1),
            Created = DateTimeOffset.UtcNow,
        });
        // A DECOY post — must stay Active (proves the A04 guard is on
        // TargetKind == "grouppost", not "any non-post target").
        await Plant(store, new Post
        {
            Id = "a04-2-decoy", ComponentId = ComponentId, AuthorId = "u-a04-2-decoy-author",
            Body = "decoy a04-2", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, "u-a04-2-decoy-member"),
            Status = PostStatus.Active,
        });
        // Event-target report: PostId = string.Empty, TargetKind = "event".
        await Plant(store, new Report
        {
            Id = "a04-2-report", PostId = string.Empty,
            ReporterId = "u-a04-2-reporter",
            ComponentId = ComponentId, Reason = "a04-2-reason", Status = "filed",
            At = DateTimeOffset.UtcNow,
            TargetKind = "event", TargetId = "a04-2-event",
        });

        await RunInSession(store, async session =>
            await svc.ResolveReportAsync(
                "a04-2-report", admin, session,
                outcome: ReportOutcome.Hidden,
                resolutionNote: "event — hide is a follow-up (A04 'the rest listed as follow-ups')"));

        // The outcome is recorded…
        await using var s1 = store.QuerySession();
        var report = await s1.LoadAsync<Report>("a04-2-report", TestContext.Current.CancellationToken);
        Assert.Equal(ReportOutcome.Hidden, report!.Outcome);
        Assert.Equal("resolved", report.Status);
        Assert.Equal(string.Empty, report.PostId);

        // …but the decoy post is untouched (still Active) — the A04 guard
        // is on TargetKind == "grouppost", not "any non-post target".
        await using var s2 = store.QuerySession();
        var decoy = await s2.LoadAsync<Post>("a04-2-decoy", TestContext.Current.CancellationToken);
        Assert.Equal(PostStatus.Active, decoy!.Status);

        // No write-lane (moderate) row for the decoy post.
        var postRows = await PostAudits(store, actor: admin);
        Assert.DoesNotContain(postRows, a => a.TargetId == "a04-2-decoy");

        // The resolve row is still written (the lane's own row).
        var reportRows = await ReportAudits(store, actor: admin);
        Assert.Contains(reportRows, a => a.Action == "report.resolve"
                                         && a.Outcome == AccessOutcome.Allow);
    }

    // ── R05-1 — ListMyReportsAsync_ReturnsReportersRows_WithResolutionShape ─
    //
    // IMPROVE-UX R05 (the D1 "My reports" read shape; the R05 DoD a pin): the
    // reporter's content reports (where Report.ReporterId == the reporter) are
    // listed newest-first, each row carrying the report's context
    // (PostId / Reason / At) + status + the R03 resolution shape
    // (Outcome / ResolutionNote / ResolvedAt / ResolvedBy). For a **visible**
    // post target (the reporter can still read it — in the audience) the
    // TargetTitle is shown. A report filed by another reporter is NOT in the
    // list (the ReporterId filter, the reporter's-own pin).

    [Fact]
    public async Task ListMyReportsAsync_ReturnsReportersRows_WithResolutionShape()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = ServicesWithPosts(store);
        const string reporter = "u-r05-1-reporter";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        // The reporter is in the post's audience (a User grant) so
        // PostService.GetPostAsync allows the read → the title is visible.
        await Plant(store, new Post
        {
            Id = "r05-1-post", ComponentId = ComponentId, AuthorId = "u-r05-1-author",
            Title = "visible post title", Body = "body r05-1", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, reporter),
        });
        await Plant(store, new Report
        {
            Id = "r05-1-report", PostId = "r05-1-post", ReporterId = reporter,
            ComponentId = ComponentId, Reason = "r05-1-reason", Status = "resolved",
            At = DateTimeOffset.UtcNow,
            Outcome = ReportOutcome.Hidden, ResolutionNote = "hidden per policy",
            ResolvedAt = DateTimeOffset.UtcNow, ResolvedBy = "u-r05-1-admin",
        });
        // A report filed by ANOTHER reporter must NOT appear in this reporter's list.
        await Plant(store, new Report
        {
            Id = "r05-1-other", PostId = "r05-1-post", ReporterId = "u-r05-1-other-reporter",
            ComponentId = ComponentId, Reason = "r05-1-other-reason", Status = "filed",
            At = DateTimeOffset.UtcNow,
        });

        var rows = await svc.ListMyReportsAsync(reporter, TestContext.Current.CancellationToken);

        var row = Assert.Single(rows);
        Assert.Equal("r05-1-report", row.ReportId);
        Assert.Equal("r05-1-post", row.PostId);
        Assert.Equal("r05-1-reason", row.Reason);
        Assert.Equal("resolved", row.Status);
        Assert.Null(row.TargetKind);                       // the original M3b post shape
        Assert.Equal("visible post title", row.TargetTitle); // visible → the title is shown
        Assert.Equal(ReportOutcome.Hidden, row.Outcome);    // the R03 resolution shape
        Assert.Equal("hidden per policy", row.ResolutionNote);
        Assert.NotNull(row.ResolvedAt);
        Assert.Equal("u-r05-1-admin", row.ResolvedBy);
    }

    // ── R05-2 — ListMyReportsAsync_TargetNoLongerVisible_NoTitle ──────────
    //
    // IMPROVE-UX R05 (the DoD pin, verbatim from the plan): a post target the
    // reporter can no longer read yields TargetTitle == null. Two shapes —
    // (a) the post was DELETED (no Post doc → GetPostAsync returns a null Post),
    // (b) the post is live but the reporter is DENIED (not the author, not in
    // the audience → CanAsync denies → GetPostAsync returns a null Post). Both
    // are "no longer visible" — the row still appears (the reporter's own
    // report) but with no title (the R05 DoD "target no longer visible → no
    // title" pin).

    [Fact]
    public async Task ListMyReportsAsync_TargetNoLongerVisible_NoTitle()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = ServicesWithPosts(store);
        const string reporter = "u-r05-2-reporter";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        // (a) A report against a DELETED post (no Post doc planted).
        await Plant(store, new Report
        {
            Id = "r05-2-deleted", PostId = "r05-2-gone-post", ReporterId = reporter,
            ComponentId = ComponentId, Reason = "r05-2-reason", Status = "filed",
            At = DateTimeOffset.UtcNow,
        });
        // (b) A report against a live post the reporter is DENIED on (the
        //     reporter is neither the author nor in the audience → CanAsync
        //     denies → GetPostAsync returns a null Post).
        await Plant(store, new Post
        {
            Id = "r05-2-denied", ComponentId = ComponentId, AuthorId = "u-r05-2-author",
            Title = "denied post title", Body = "body r05-2", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, "u-r05-2-someone-else"),
        });
        await Plant(store, new Report
        {
            Id = "r05-2-report", PostId = "r05-2-denied", ReporterId = reporter,
            ComponentId = ComponentId, Reason = "r05-2-reason", Status = "filed",
            At = DateTimeOffset.UtcNow,
        });

        var rows = await svc.ListMyReportsAsync(reporter, TestContext.Current.CancellationToken);

        var byId = rows.ToDictionary(r => r.ReportId);
        Assert.Equal(2, byId.Count);
        // (a) deleted → no title (the reporter can no longer read the target)
        Assert.Null(byId["r05-2-deleted"].TargetTitle);
        Assert.Null(byId["r05-2-deleted"].TargetKind);
        // (b) denied → no title (the live post is no longer readable by the reporter)
        Assert.Null(byId["r05-2-report"].TargetTitle);
    }

    // ── R05-3 — ListMyReportsAsync_NoReports_ReturnsEmpty ──────────────────
    //
    // IMPROVE-UX R05 (the empty-list pin; the GetQueueAsync / ListAsync
    // empty-list precedent): a reporter with no filed reports gets an empty
    // list (not null, not a throw) — and reports filed by others do not leak
    // in (the ReporterId filter).

    [Fact]
    public async Task ListMyReportsAsync_NoReports_ReturnsEmpty()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = ServicesWithPosts(store);

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        // A report filed by someone else must not leak into this reporter's list.
        await Plant(store, new Report
        {
            Id = "r05-3-other", PostId = "r05-3-post", ReporterId = "u-r05-3-other",
            ComponentId = ComponentId, Reason = "r05-3-reason", Status = "filed",
            At = DateTimeOffset.UtcNow,
        });

        var rows = await svc.ListMyReportsAsync("u-r05-3-empty", TestContext.Current.CancellationToken);
        Assert.NotNull(rows);
        Assert.Empty(rows);
    }

    // ── R07 — the ReportResolved emission (the E4 "the notification repeats
    //       the reporter's own reason" fix) ──────────────────────────────
    //
    // IMPROVE-UX R07 (the R03/R04/R05/R06 close-out unit): the
    // <c>ResolveReportAsync</c> lane's <see cref="NotificationKinds.ReportResolved"/>
    // emission now carries the **outcome** (a localized sentence from
    // <see cref="Report.Outcome"/> + the moderator's
    // <see cref="Report.ResolutionNote"/> verbatim) as its body — not the
    // reporter's own <see cref="Report.Reason"/> — and a
    // <c>linkPath</c> that lands on the reporter's row on "My reports"
    // (<c>/reports/mine#r-{id}</c>, the R06 row anchor
    // <c>id="r-{ReportId}"</c>). These tests wire the
    // <see cref="INotificationEmitter"/> as an NSubstitute seam (the
    // <c>C03_NotificationEmitterContractTests</c> shape) and assert the
    // emission's kind / recipient / linkPath / targetId / body. The kind,
    // recipient, and idempotency-key pins are unchanged (the F10 re-emit
    // idempotency anchor); the body + linkPath / targetId are the R07 ADDs.

    /// <summary>
    /// Compose the <see cref="ModerationService"/> over the real M1/M2
    /// seams (the <see cref="Services"/> shape) **plus** an NSubstitute
    /// <see cref="INotificationEmitter"/> seam (the R07 unit under test) and
    /// a real <see cref="ITranslationProvider"/>-shaped substitute that
    /// resolves each key to its <see cref="KnownTranslationKeys.EnValues"/>
    /// floor (the ADR 0005 en-floor the provider guarantees, so the asserted
    /// body is the exact <c>reports.body_*</c> English sentence). The
    /// reporter's <see cref="Profile"/> is planted (the
    /// <see cref="PlantProfile"/> shape) so
    /// <see cref="IUserInfoService.GetProfileAsync"/> resolves their
    /// <c>EmailLanguage</c> (null here — the en floor).
    /// </summary>
    private static async Task<(IDocumentStore store, ModerationService svc,
        INotificationEmitter emitter)> ServicesWithNotifications(IDocumentStore store, string reporterId)
    {
        // (a) The translation seam — the ADR 0005 en-floor the provider
        //     guarantees: every key resolves to its KnownTranslationKeys
        //     en value (the closed registry the seeder materializes).
        var translator = Substitute.For<ITranslationProvider>();
        translator.GetAsync(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(ci =>
            {
                var key = (string)ci[0]!;
                return Task.FromResult(
                    KnownTranslationKeys.EnValues.TryGetValue(key, out var en) ? en : key);
            });

        // (b) The real M1/M2 trio (the Services(store) shape) + the emitter
        //     substitute the R07 emission fires onto.
        var (userInfo, authz, _) = Services(store);
        var emitter = Substitute.For<INotificationEmitter>();
        var svc = new ModerationService(
            new ProfileSettingsService(store), authz, store,
            notifications: emitter, userInfo: userInfo, translationProvider: translator);

        // (c) Plant the reporter's Profile so GetProfileAsync resolves their
        //     EmailLanguage (null here — the en floor; the R07 body asserts
        //     the exact en sentence, not a language-specific one).
        await PlantProfile(store, reporterId, "r07-reporter@example.com");
        return (store, svc, emitter);
    }

    private static async Task PlantReportResolvedFixture(
        IDocumentStore store, string prefix, string reporterId)
    {
        const string globalAdmin = "u-r07-admin";
        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = $"{prefix}-assign", UserId = globalAdmin, ComponentId = ComponentId,
            GrantedBy = $"{prefix}-root", At = DateTimeOffset.UtcNow
        });
        await Plant(store, new Post
        {
            Id = $"{prefix}-post", ComponentId = ComponentId, AuthorId = $"{prefix}-author",
            Body = $"body {prefix}", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, $"{prefix}-member"),
        });
        await Plant(store, new Report
        {
            Id = $"{prefix}-report", PostId = $"{prefix}-post", ReporterId = reporterId,
            ComponentId = ComponentId, Reason = $"r07-reason-{prefix} (the reporter's own words)",
            Status = "filed", At = DateTimeOffset.UtcNow,
            Outcome = null, ResolutionNote = null, ResolvedAt = null, ResolvedBy = null,
        });
    }

    [Theory]
    [InlineData(ReportOutcome.NoAction)]
    [InlineData(ReportOutcome.Hidden)]
    [InlineData(ReportOutcome.Removed)]
    [InlineData(ReportOutcome.Other)]
    public async Task R07_ResolveReport_EmitsOutcomeBody_AndLinkPathToMyReportsRow(ReportOutcome outcome)
    {
        const string reporter = "u-r07-reporter";
        var suffix = outcome.ToString().ToLowerInvariant();  // noaction / hidden / removed / other
        var prefix = $"r07-{suffix}";
        var reportId = $"{prefix}-report";                   // e.g. r07-noaction-report

        var store = await BootStoreAsync();
        var (_, svc, emitter) = await ServicesWithNotifications(store, reporter);

        await PlantReportResolvedFixture(store, prefix, reporter);
        const string globalAdmin = "u-r07-admin";

        await RunInSession(store, async session =>
            await svc.ResolveReportAsync(reportId, globalAdmin, session, outcome: outcome));

        // (a) kind + recipient unchanged from the M6 F7 emission.
        // (b) linkPath exactly /reports/mine#r-{reportId}; targetId == reportId.
        // (c) body is the outcome sentence (reports.body_{suffix}) — NOT the
        //     reporter's own reason (the E4 finding). No note → no suffix.
        await emitter.Received(1).EmitAsync(
            Arg.Any<IDocumentSession>(),
            recipientId: reporter,
            kind: NotificationKinds.ReportResolved,
            idempotencyKey: $"notification:report.resolved:{reportId}:filed",
            body: Arg.Any<string?>(),
            targetId: reportId,
            linkPath: $"/reports/mine#r-{reportId}",
            acceptPath: (string?)null,
            declinePath: (string?)null,
            ct: Arg.Any<CancellationToken>());

        var call = emitter.ReceivedCalls()
            .First(c => c.GetMethodInfo().Name == nameof(INotificationEmitter.EmitAsync));
        var body = (string)call.GetArguments()[4]!;
        Assert.Equal(
            KnownTranslationKeys.EnValues[$"reports.body_{suffix}"],
            body);
        Assert.DoesNotContain("r07-reason", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task R07_ResolveReport_WithNote_BodyCarriesNoteVerbatim()
    {
        var store = await BootStoreAsync();
        const string reporter = "u-r07-note-reporter";
        var (_, svc, emitter) = await ServicesWithNotifications(store, reporter);

        await PlantReportResolvedFixture(store, "r07-note", reporter);
        const string globalAdmin = "u-r07-admin";
        const string reportId = "r07-note-report";
        const string note = "Moderator free-text note — verbatim, never translated (Umlaut ä, French é, Danish å).";

        await RunInSession(store, async session =>
            await svc.ResolveReportAsync(reportId, globalAdmin, session,
                outcome: ReportOutcome.Other, resolutionNote: note));

        await emitter.Received(1).EmitAsync(
            Arg.Any<IDocumentSession>(),
            recipientId: reporter,
            kind: NotificationKinds.ReportResolved,
            idempotencyKey: Arg.Any<string>(),
            body: Arg.Any<string?>(),
            targetId: reportId,
            linkPath: $"/reports/mine#r-{reportId}",
            acceptPath: (string?)null,
            declinePath: (string?)null,
            ct: Arg.Any<CancellationToken>());

        var call = emitter.ReceivedCalls()
            .First(c => c.GetMethodInfo().Name == nameof(INotificationEmitter.EmitAsync));
        var body = (string)call.GetArguments()[4]!;
        // The outcome sentence + a space + the verbatim note (never translated).
        Assert.StartsWith(KnownTranslationKeys.EnValues["reports.body_other"], body);
        Assert.EndsWith(note, body);
        Assert.Contains(note, body);
    }

    [Fact]
    public async Task R07_ResolveReport_NoOutcome_BodyIsPlainResolvedSentence()
    {
        var store = await BootStoreAsync();
        const string reporter = "u-r07-noout-reporter";
        var (_, svc, emitter) = await ServicesWithNotifications(store, reporter);

        await PlantReportResolvedFixture(store, "r07-noout", reporter);
        const string globalAdmin = "u-r07-admin";
        const string reportId = "r07-noout-report";

        // The pre-R03 shape: no outcome, no note (the "resolved with no
        // outcome recorded" state R05 reads) → the plain "resolved" sentence.
        await RunInSession(store, async session =>
            await svc.ResolveReportAsync(reportId, globalAdmin, session));

        await emitter.Received(1).EmitAsync(
            Arg.Any<IDocumentSession>(),
            recipientId: reporter,
            kind: NotificationKinds.ReportResolved,
            idempotencyKey: Arg.Any<string>(),
            body: Arg.Any<string?>(),
            targetId: reportId,
            linkPath: $"/reports/mine#r-{reportId}",
            acceptPath: (string?)null,
            declinePath: (string?)null,
            ct: Arg.Any<CancellationToken>());

        var call = emitter.ReceivedCalls()
            .First(c => c.GetMethodInfo().Name == nameof(INotificationEmitter.EmitAsync));
        var body = (string)call.GetArguments()[4]!;
        Assert.Equal(KnownTranslationKeys.EnValues["reports.body_resolved"], body);
        Assert.DoesNotContain("r07-reason", body);   // the E4 pin: not the reporter's own reason
    }

    // ── A05 — the report.queued routing emission (the E3 "new reports reach
    //       no moderator" loop closer) ────────────────────────────────────────
    //
    // IMPROVE-UX A05 (ADR 0303): a *filed* report is routed to the component's
    // standing moderators ∪ the GlobalAdmins (deduped), with **no target
    // content in the body** (the 2026-09-25 disclosure-fix shape) and a
    // linkPath of /moderation/{reportId} (the scoped-read route — the
    // receiving moderator's Read decision on open still applies). These tests
    // wire the INotificationEmitter as an NSubstitute seam (the R07 shape)
    // **and** the IIdentityService.ListGlobalAdminsAsync seam (the ADR 0303
    // GlobalAdmin-fan-out) — both optional on the ModerationService ctor,
    // wired here so the full recipient set resolves. The report.filed-to-
    // author emission (the 2026-09-25 shape) is asserted **unchanged** — the
    // author still gets report.filed, never report.queued.
    //
    /// <summary>
    /// Compose the M3b trio **plus** the NSubstitute
    /// <see cref="INotificationEmitter"/> (the A05 emission seam) **and** the
    /// <see cref="IIdentityService"/> (the ADR 0303
    /// <see cref="IIdentityService.ListGlobalAdminsAsync"/> GlobalAdmin
    /// fan-out). The standing-moderator half resolves from the live store's
    /// <see cref="ModeratorAssignment"/> rows (the same-transaction read); the
    /// GlobalAdmin half resolves from the <paramref name="globalAdminIds"/>
    /// the substitute returns.
    /// </summary>
    private static (IDocumentStore store, ModerationService svc, INotificationEmitter emitter)
        ServicesWithRouting(IDocumentStore store, IReadOnlyList<string> globalAdminIds)
    {
        var (userInfo, authz, _) = Services(store);
        var emitter = Substitute.For<INotificationEmitter>();
        var identity = Substitute.For<IIdentityService>();
        identity.ListGlobalAdminsAsync()
            .Returns(Task.FromResult<IReadOnlyList<string>>(globalAdminIds.ToList()));
        var svc = new ModerationService(
            new ProfileSettingsService(store), authz, store,
            notifications: emitter, userInfo: userInfo, identity: identity);
        return (store, svc, emitter);
    }

    /// <summary>The id of the report filed against a post (the post-lane's
    /// internal Guid, read back from the live store for the linkPath /
    /// idempotency-key pins).</summary>
    private static async Task<string> ReportIdForPostAsync(IDocumentStore store, string postId)
    {
        await using var q = store.QuerySession();
        var r = await q.Query<Report>()
            .Where(x => x.PostId == postId)
            .SingleAsync(TestContext.Current.CancellationToken);
        return r.Id;
    }

    /// <summary>The id of the report filed against a new-lane target
    /// (ADR 0173 — <see cref="Report.TargetId"/> carries the target's id; the
    /// post-lane's <see cref="Report.PostId"/> is empty), read back from the
    /// live store for the linkPath / idempotency-key pins.</summary>
    private static async Task<string> ReportIdForTargetAsync(IDocumentStore store, string targetId)
    {
        await using var q = store.QuerySession();
        var r = await q.Query<Report>()
            .Where(x => x.TargetId == targetId)
            .SingleAsync(TestContext.Current.CancellationToken);
        return r.Id;
    }

    // ── A05-1 — filed report routes to standing moderator ∪ GlobalAdmin
    //       (deduped), author still gets report.filed ──────────────────────────

    [Fact]
    public async Task A05_FileReport_Queued_StandingModeratorAndGlobalAdmin_InboxRows()
    {
        var store = await BootStoreAsync();
        // The standing moderator "a05-1-mod" is ALSO a GlobalAdmin (dedupe
        // pin: one row, not two); "a05-1-admin" is a GlobalAdmin only.
        var (_, svc, emitter) = ServicesWithRouting(
            store, new[] { "a05-1-mod", "a05-1-admin" });

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = "a05-1-assign", UserId = "a05-1-mod", ComponentId = ComponentId,
            GrantedBy = "a05-1-root", At = DateTimeOffset.UtcNow
        });
        const string reporter = "a05-1-reporter";
        const string author = "a05-1-author";
        await Plant(store, new Post
        {
            Id = "a05-1-post", ComponentId = ComponentId, AuthorId = author,
            Body = "body a05-1", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, reporter),
        });

        await RunInSession(store, async session =>
            await svc.FileReportAsync("a05-1-post", reporter, "a05-1-reason", session));

        var reportId = await ReportIdForPostAsync(store, "a05-1-post");
        var link = $"/moderation/{reportId}";

        // The standing moderator (who is also a GlobalAdmin) gets ONE row.
        await emitter.Received(1).EmitAsync(
            Arg.Any<IDocumentSession>(),
            recipientId: "a05-1-mod",
            kind: NotificationKinds.ReportQueued,
            idempotencyKey: $"notification:report.queued:{reportId}:a05-1-mod",
            body: (string?)null,
            targetId: reportId,
            linkPath: link,
            acceptPath: (string?)null,
            declinePath: (string?)null,
            ct: Arg.Any<CancellationToken>());

        // The GlobalAdmin-only recipient gets one row.
        await emitter.Received(1).EmitAsync(
            Arg.Any<IDocumentSession>(),
            recipientId: "a05-1-admin",
            kind: NotificationKinds.ReportQueued,
            idempotencyKey: $"notification:report.queued:{reportId}:a05-1-admin",
            body: (string?)null,
            targetId: reportId,
            linkPath: link,
            acceptPath: (string?)null,
            declinePath: (string?)null,
            ct: Arg.Any<CancellationToken>());

        // The reporter is NOT routed a nudge about their own filing.
        await emitter.DidNotReceive().EmitAsync(
            Arg.Any<IDocumentSession>(),
            recipientId: reporter,
            kind: NotificationKinds.ReportQueued,
            idempotencyKey: $"notification:report.queued:{reportId}:{reporter}",
            body: (string?)null,
            targetId: reportId,
            linkPath: link,
            acceptPath: (string?)null,
            declinePath: (string?)null,
            ct: Arg.Any<CancellationToken>());

        // The author's report.filed emission is UNCHANGED (one, not queued).
        await emitter.Received(1).EmitAsync(
            Arg.Any<IDocumentSession>(),
            recipientId: author,
            kind: NotificationKinds.ReportFiled,
            idempotencyKey: $"notification:report.filed:{reportId}",
            body: (string?)null,
            ct: Arg.Any<CancellationToken>());
        await emitter.DidNotReceive().EmitAsync(
            Arg.Any<IDocumentSession>(),
            recipientId: author,
            kind: NotificationKinds.ReportQueued,
            idempotencyKey: $"notification:report.queued:{reportId}:{author}",
            body: (string?)null,
            targetId: reportId,
            linkPath: link,
            acceptPath: (string?)null,
            declinePath: (string?)null,
            ct: Arg.Any<CancellationToken>());
    }

    // ── A05-2 — self-report: the reporter (their own standing moderator)
    //       gets no queued row; the GlobalAdmin still does ─────────────────────

    [Fact]
    public async Task A05_FileReport_Queued_SelfReport_NoQueuedRowForReporter()
    {
        var store = await BootStoreAsync();
        // The reporter IS the standing moderator (degenerate); a GlobalAdmin
        // is the fallback.
        var (_, svc, emitter) = ServicesWithRouting(
            store, new[] { "a05-2-admin" });

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = "a05-2-assign", UserId = "a05-2-reporter", ComponentId = ComponentId,
            GrantedBy = "a05-2-root", At = DateTimeOffset.UtcNow
        });
        const string reporter = "a05-2-reporter";
        const string author = "a05-2-author";
        await Plant(store, new Post
        {
            Id = "a05-2-post", ComponentId = ComponentId, AuthorId = author,
            Body = "body a05-2", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, reporter),
        });

        await RunInSession(store, async session =>
            await svc.FileReportAsync("a05-2-post", reporter, "a05-2-reason", session));

        var reportId = await ReportIdForPostAsync(store, "a05-2-post");

        // The reporter (their own standing moderator) is skipped (self-report
        // guard, mirrored from the report.filed-to-author lane). All string
        // args are literals (the A05-3 consistent-matchers shape — mixing a
        // matcher with string literals raises NSubstitute's
        // AmbiguousArgumentsException).
        await emitter.DidNotReceive().EmitAsync(
            Arg.Any<IDocumentSession>(),
            recipientId: reporter,
            kind: NotificationKinds.ReportQueued,
            idempotencyKey: $"notification:report.queued:{reportId}:{reporter}",
            body: (string?)null,
            targetId: reportId,
            linkPath: $"/moderation/{reportId}",
            acceptPath: (string?)null,
            declinePath: (string?)null,
            ct: Arg.Any<CancellationToken>());

        // The GlobalAdmin fallback still gets the row (the A05 / E3 loop
        // closer — the standing half is empty for the reporter, the
        // GlobalAdmin half is not).
        await emitter.Received(1).EmitAsync(
            Arg.Any<IDocumentSession>(),
            recipientId: "a05-2-admin",
            kind: NotificationKinds.ReportQueued,
            idempotencyKey: $"notification:report.queued:{reportId}:a05-2-admin",
            body: (string?)null,
            targetId: reportId,
            linkPath: $"/moderation/{reportId}",
            acceptPath: (string?)null,
            declinePath: (string?)null,
            ct: Arg.Any<CancellationToken>());
    }

    // ── A05-3 — the new-lane shape (FileReportTargetAsync): component set →
    //       standing moderators; component null → only the GlobalAdmins ────────

    [Fact]
    public async Task A05_FileReportTarget_Queued_NewLaneShape_RoutesToo()
    {
        var store = await BootStoreAsync();
        var (_, svc, emitter) = ServicesWithRouting(
            store, new[] { "a05-3-admin" });

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = "a05-3-assign", UserId = "a05-3-mod", ComponentId = ComponentId,
            GrantedBy = "a05-3-root", At = DateTimeOffset.UtcNow
        });
        const string reporter = "a05-3-reporter";
        const string author = "a05-3-author";

        // Filing 1 — a group post INSIDE a component (componentId set): the
        // standing moderators + the GlobalAdmins both get a row.
        await svc.FileReportTargetAsync(
            "grouppost", "a05-3-gp-1", ComponentId, author, reporter, "a05-3-reason-1");
        // Filing 2 — a group post OUTSIDE a component (componentId null): the
        // C-M3b·4 "no fabricated component" shape — only the GlobalAdmins
        // reach it (no standing set for a null component).
        await svc.FileReportTargetAsync(
            "grouppost", "a05-3-gp-2", null, author, reporter, "a05-3-reason-2");

        var r1 = await ReportIdForTargetAsync(store, "a05-3-gp-1");
        var r2 = await ReportIdForTargetAsync(store, "a05-3-gp-2");

        // The standing moderator gets a row for filing 1 only (filing 2 has
        // no component → no standing set).
        await emitter.Received(1).EmitAsync(
            Arg.Any<IDocumentSession>(),
            recipientId: "a05-3-mod",
            kind: NotificationKinds.ReportQueued,
            idempotencyKey: $"notification:report.queued:{r1}:a05-3-mod",
            body: (string?)null,
            targetId: r1,
            linkPath: $"/moderation/{r1}",
            acceptPath: (string?)null,
            declinePath: (string?)null,
            ct: Arg.Any<CancellationToken>());
        await emitter.DidNotReceive().EmitAsync(
            Arg.Any<IDocumentSession>(),
            recipientId: "a05-3-mod",
            kind: NotificationKinds.ReportQueued,
            idempotencyKey: $"notification:report.queued:{r2}:a05-3-mod",
            body: (string?)null,
            targetId: r2,
            linkPath: $"/moderation/{r2}",
            acceptPath: (string?)null,
            declinePath: (string?)null,
            ct: Arg.Any<CancellationToken>());

        // The GlobalAdmin gets a row for BOTH filings (the fallback recipient
        // set — the E3 loop closer for component-less targets).
        await emitter.Received(1).EmitAsync(
            Arg.Any<IDocumentSession>(),
            recipientId: "a05-3-admin",
            kind: NotificationKinds.ReportQueued,
            idempotencyKey: $"notification:report.queued:{r1}:a05-3-admin",
            body: (string?)null,
            targetId: r1,
            linkPath: $"/moderation/{r1}",
            acceptPath: (string?)null,
            declinePath: (string?)null,
            ct: Arg.Any<CancellationToken>());
        await emitter.Received(1).EmitAsync(
            Arg.Any<IDocumentSession>(),
            recipientId: "a05-3-admin",
            kind: NotificationKinds.ReportQueued,
            idempotencyKey: $"notification:report.queued:{r2}:a05-3-admin",
            body: (string?)null,
            targetId: r2,
            linkPath: $"/moderation/{r2}",
            acceptPath: (string?)null,
            declinePath: (string?)null,
            ct: Arg.Any<CancellationToken>());

        // The author's report.filed emission is UNCHANGED (one per filing).
        await emitter.Received(1).EmitAsync(
            Arg.Any<IDocumentSession>(),
            recipientId: author,
            kind: NotificationKinds.ReportFiled,
            idempotencyKey: $"notification:report.filed:{r1}",
            body: (string?)null,
            ct: Arg.Any<CancellationToken>());
        await emitter.Received(1).EmitAsync(
            Arg.Any<IDocumentSession>(),
            recipientId: author,
            kind: NotificationKinds.ReportFiled,
            idempotencyKey: $"notification:report.filed:{r2}",
            body: (string?)null,
            ct: Arg.Any<CancellationToken>());
    }

    // ── A05-4 — the report.queued body carries NO target content (the A05
    //       "no content in body" pin) ─────────────────────────────────────────

    [Fact]
    public async Task A05_FileReport_Queued_BodyHasNoContent()
    {
        var store = await BootStoreAsync();
        var (_, svc, emitter) = ServicesWithRouting(store, new[] { "a05-4-admin" });

        const string reason = "a05-4-secret-reason (the reporter's free-text, never forwarded)";
        const string postBody = "a05-4-secret-post-body";
        const string reporter = "a05-4-reporter";
        const string author = "a05-4-author";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = "a05-4-assign", UserId = "a05-4-admin", ComponentId = ComponentId,
            GrantedBy = "a05-4-root", At = DateTimeOffset.UtcNow
        });
        await Plant(store, new Post
        {
            Id = "a05-4-post", ComponentId = ComponentId, AuthorId = author,
            Body = postBody, Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, reporter),
        });

        await RunInSession(store, async session =>
            await svc.FileReportAsync("a05-4-post", reporter, reason, session));

        var reportId = await ReportIdForPostAsync(store, "a05-4-post");

        // The queued emission's body is null — the localized
        // notification.report.queued.subject is the row's text. No title, no
        // body, no reporter's reason (the 2026-09-25 disclosure-fix shape).
        await emitter.Received(1).EmitAsync(
            Arg.Any<IDocumentSession>(),
            recipientId: "a05-4-admin",
            kind: NotificationKinds.ReportQueued,
            idempotencyKey: $"notification:report.queued:{reportId}:a05-4-admin",
            body: (string?)null,
            targetId: reportId,
            linkPath: $"/moderation/{reportId}",
            acceptPath: (string?)null,
            declinePath: (string?)null,
            ct: Arg.Any<CancellationToken>());

        // The pin: none of the report.queued emissions carry the reason or
        // the post body in their body argument (body is null — the strongest
        // form of the "no content" guarantee).
        var queuedBodies = emitter.ReceivedCalls()
            .Where(c => (string?)c.GetArguments()[2] == NotificationKinds.ReportQueued)
            .Select(c => (string?)c.GetArguments()[4])
            .ToList();
        Assert.All(queuedBodies, b => Assert.True(b is null || (
            !b.Contains(reason, StringComparison.Ordinal) &&
            !b.Contains(postBody, StringComparison.Ordinal)),
            $"report.queued body carries target content: {b}"));
    }

    // ── A06 — the moderation nav badge's open-report count ────────────────────
    //
    // IMPROVE-UX A06 (the E3 "new reports reach no moderator" finding's
    // visible loop-closer, the badge half — A05 shipped the routing):
    // ModerationService.CountOpenReportsAsync is a read seam (the
    // GetQueueAsync / GetDetailAsync shape — no CanAsync, no AccessAudit row)
    // that returns the actor's scoped open-report count, where "open" is
    // Status ∈ {filed, assigned, unlocked} (a resolved report is done). The
    // scoped query is the SAME one GetQueueAsync uses (the C-M3b·4 SoD shape:
    // GlobalAdmin → every report; standing-moderator → their components with
    // ModeratorAccess ON; plain resident → no reports) — a test pins this
    // (A06-1). The nav badge's 60 s cache is Web-layer (ADR 0006-D holds:
    // the Core seam is pure, the IMemoryCache lives in Kumunita.Web).

    [Fact]
    public async Task A06_CountOpenReports_GlobalAdmin_SameScopedQueryAsQueue()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);

        const string moderator = "a06-1-mod";
        const string reporter = "a06-1-reporter";

        // Component A — the moderator's scoped component (ModeratorAccess ON,
        // the U6 handoff's flag-ON gate). Component B — a different component
        // the moderator has no standing on (out of scope for the standing
        // call, in scope for the GlobalAdmin call).
        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = "a06-1-assign", UserId = moderator, ComponentId = ComponentId,
            GrantedBy = "a06-1-root", At = DateTimeOffset.UtcNow
        });
        const string compB = "a06-1-comp-b";
        await Plant(store, new Component { Id = compB, Name = "Social", Enabled = true,
                                           ModeratorAccess = true });

        await Plant(store, new Post
        {
            Id = "a06-1-post", ComponentId = ComponentId, AuthorId = "a06-1-author",
            Body = "body a06-1", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, reporter),
        });

        // Three reports: one filed on Component A (in scope), one resolved on
        // Component A (out of scope — not open), one filed on Component B
        // (out of scope for the standing call, in scope for the GlobalAdmin
        // call).
        await Plant(store, new Report
        {
            Id = "a06-1-r-filed-a", PostId = "a06-1-post", ReporterId = reporter,
            ComponentId = ComponentId, Reason = "flagged", Status = "filed",
            At = DateTimeOffset.UtcNow
        });
        await Plant(store, new Report
        {
            Id = "a06-1-r-resolved-a", PostId = "a06-1-post", ReporterId = reporter,
            ComponentId = ComponentId, Reason = "flagged", Status = "resolved",
            At = DateTimeOffset.UtcNow
        });
        await Plant(store, new Report
        {
            Id = "a06-1-r-filed-b", PostId = "a06-1-post", ReporterId = reporter,
            ComponentId = compB, Reason = "flagged", Status = "filed",
            At = DateTimeOffset.UtcNow
        });

        // The standing-moderator call: in-scope (Component A) open reports
        // only — the filed A report. The resolved A report is not open; the
        // filed B report is out of scope (the moderator has no
        // ModeratorAssignment on Component B).
        var standingCount = await svc.CountOpenReportsAsync(
            moderator, isGlobalAdmin: false, isStanding: true, TestContext.Current.CancellationToken);
        Assert.Equal(1, standingCount);

        // The GlobalAdmin call: every open report (both filed reports — A
        // and B; the resolved A report still excluded — not open). This is
        // the "same scoped query as the queue" pin: the GlobalAdmin's
        // GetQueueAsync would return all three rows, and its open count is
        // Rows.Count - ByStatus["resolved"] = 2.
        var globalAdminCount = await svc.CountOpenReportsAsync(
            moderator, isGlobalAdmin: true, isStanding: true, TestContext.Current.CancellationToken);
        Assert.Equal(2, globalAdminCount);

        // The pin — the count is provably equal to the queue's open count
        // (the A06 "the badge count is provably equal to the queue's open
        // count for the same actor in the same window" invariant): for the
        // GlobalAdmin, GetQueueAsync returns all three rows and its
        // ByStatus["resolved"] is 1, so its open count is
        // Rows.Count - ByStatus["resolved"] = 2 — the same as
        // CountOpenReportsAsync. (The ServicesWithPosts composition is
        // used here — the queue read seam resolves display names via
        // IUserInfoService, which the plain Services trio omits; the
        // scoped query itself is unchanged.)
        // Compose the moderation service with the IUserInfoService read seam
        // wired (the GetQueueAsync display-name resolution requires it — the
        // A06 pin is about the scoped query, not the display work, so a real
        // UserInfoService over the same live store is the honest shape).
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var profileSettings = new ProfileSettingsService(store);
        var svc2 = new ModerationService(profileSettings, authz, store, userInfo: userInfo);
        var queue = await svc2.GetQueueAsync(moderator, isGlobalAdmin: true, isStanding: true, TestContext.Current.CancellationToken);
        Assert.Equal(3, queue.Rows.Count);
        Assert.Equal(1, queue.ByStatus.GetValueOrDefault("resolved", 0));
        Assert.Equal(
            queue.Rows.Count - queue.ByStatus.GetValueOrDefault("resolved", 0),
            globalAdminCount);
    }

    [Fact]
    public async Task A06_CountOpenReports_PlainResident_Zero()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);

        const string resident = "a06-2-resident";
        const string reporter = "a06-2-reporter";

        // A filed report on a component the resident has no standing on —
        // the C-M3b·4 SoD display-level projection (a plain resident sees
        // no reports, the badge's count is 0 by construction).
        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = true });
        await Plant(store, new Post
        {
            Id = "a06-2-post", ComponentId = ComponentId, AuthorId = "a06-2-author",
            Body = "body a06-2", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, reporter),
        });
        await Plant(store, new Report
        {
            Id = "a06-2-r-filed", PostId = "a06-2-post", ReporterId = reporter,
            ComponentId = ComponentId, Reason = "flagged", Status = "filed",
            At = DateTimeOffset.UtcNow
        });

        var count = await svc.CountOpenReportsAsync(
            resident, isGlobalAdmin: false, isStanding: false, TestContext.Current.CancellationToken);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task A06_CountOpenReports_ResolvedReport_NotOpen()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);

        const string moderator = "a06-3-mod";
        const string reporter = "a06-3-reporter";

        // One filed + one resolved report on the same component the
        // moderator is standing on — the resolved one is not "open" (the
        // A06 "a resolved report is done" pin).
        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = "a06-3-assign", UserId = moderator, ComponentId = ComponentId,
            GrantedBy = "a06-3-root", At = DateTimeOffset.UtcNow
        });
        await Plant(store, new Post
        {
            Id = "a06-3-post", ComponentId = ComponentId, AuthorId = "a06-3-author",
            Body = "body a06-3", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, reporter),
        });
        await Plant(store, new Report
        {
            Id = "a06-3-r-filed", PostId = "a06-3-post", ReporterId = reporter,
            ComponentId = ComponentId, Reason = "flagged", Status = "filed",
            At = DateTimeOffset.UtcNow
        });
        await Plant(store, new Report
        {
            Id = "a06-3-r-resolved", PostId = "a06-3-post", ReporterId = reporter,
            ComponentId = ComponentId, Reason = "flagged", Status = "resolved",
            At = DateTimeOffset.UtcNow
        });

        var count = await svc.CountOpenReportsAsync(
            moderator, isGlobalAdmin: false, isStanding: true, TestContext.Current.CancellationToken);
        Assert.Equal(1, count);
    }

    // ── Shared helpers (mirror the PostServiceTests shape) ──────────────

    private async Task<IDocumentStore> BootStoreAsync()
    {
        var (store, _) = await BootStoreAsyncWithConn();
        return store;
    }

    /// <summary>
    /// <see cref="BootStoreAsync"/> + the raw connection string (needed by the
    /// A04 tests to seed a break-glass <c>AdminOverride</c> row via the raw-SQL
    /// path — the <c>GroupPostServiceTests.SeedAdminOverrideAsync</c> shape).
    /// </summary>
    private async Task<(IDocumentStore store, string conn)> BootStoreAsyncWithConn()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            CoreDocTypes.Configure(opts);
            PostDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return (store, conn);
    }

    /// <summary>
    /// Seed a consumed/unexpired <c>AdminOverride</c> row (the
    /// <c>AuthorizationServiceTests.SeedAdminOverrideAsync</c> / <c>GroupPost-
    /// ServiceTests.SeedAdminOverrideAsync</c> precedent — the hand-rolled
    /// <c>mt</c> table, operator-written via psql; the test mirrors that path
    /// with a raw Npgsql insert). Needed by the A04 tests to pass the Core's
    /// <c>CanAsync(Moderate)</c> gate for a <c>ComponentId = string.Empty</c>
    /// resource (a group post) where a regular GlobalAdmin is denied.
    /// </summary>
    private static async Task SeedAdminOverrideAsync(
        string connString, string userId, string token,
        DateTimeOffset grantedAt, DateTimeOffset expiresAt,
        DateTimeOffset consumedAt)
    {
        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO \"mt\".\"AdminOverride\" " +
            "  (\"id\", \"userId\", \"token\", \"grantedAt\", \"expiresAt\", \"consumedAt\") " +
            "VALUES (@id, @userId, @token, @grantedAt, @expiresAt, @consumedAt)";

        static void Add(NpgsqlCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }

        Add(cmd, "@id", Guid.NewGuid().ToString("N"));
        Add(cmd, "@userId", userId);
        Add(cmd, "@token", token);
        Add(cmd, "@grantedAt", grantedAt);
        Add(cmd, "@expiresAt", expiresAt);
        Add(cmd, "@consumedAt", consumedAt);

        await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Compose the service trio + a <see cref="NotificationService"/> wired
    /// with the recording mailer (the <c>NotificationServiceTests</c>
    /// <c>RecordingMailer</c> shape, reused verbatim) — the T3/T4 tests need
    /// the ReportFiled emission observable as staged outbox rows.
    /// </summary>
    private async Task<(IDocumentStore store, ModerationService svc,
        List<(string Key, string Recipient, string Subject, string Body)> staged)>
        BootTargetStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            CoreDocTypes.Configure(opts);
            PostDocTypes.Configure(opts);
            NotificationDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);

        var staged = new List<(string Key, string Recipient, string Subject, string Body)>();
        var mailer = Substitute.For<IMailerStage>();
        mailer.StageAsync(
                Arg.Any<IDocumentSession>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                staged.Add(((string)callInfo[1], (string)callInfo[2],
                            (string)callInfo[3], (string)callInfo[4]));
                return Task.CompletedTask;
            });

        var translator = Substitute.For<ITranslationProvider>();
        translator.GetAsync(
                Arg.Any<string>(), Arg.Any<string?>())
            .Returns(ci => Task.FromResult($"{(string)ci[0]}-{(string?)ci[1] ?? "en"}"));

        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync(Arg.Any<string>())
            .Returns(ci =>
            {
                var subjectId = (string)ci[0];
                return LoadProfileAsync(store, subjectId);
            });

        var userInfoReal = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfoReal);
        var profileSettings = new ProfileSettingsService(store);
        var notifications = new NotificationService(
            store, userInfo, translator, mailer, null, null, null);
        var moderation = new ModerationService(profileSettings, authz, store, notifications);

        return (store, moderation, staged);
    }

    /// <summary>Plant a minimal <see cref="Profile"/> row for the notification
    /// emission to resolve the recipient's email (the
    /// <c>NotificationServiceTests.PlantProfile</c> shape, reused).</summary>
    private static async Task PlantProfile(IDocumentStore store, string subjectId, string email)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(new Profile
        {
            SubjectId   = subjectId,
            DisplayName = subjectId,
            Verified    = true,
            Email       = email,
        });
        await w.SaveChangesAsync(ct);
    }

    /// <summary>The <see cref="IUserInfoService.GetProfileAsync"/> read lane
    /// (the ADR 0061 seam) — reads the planted <see cref="Profile"/> from the
    /// live store; <c>null</c> when no profile exists for the subject (the
    /// <c>NotificationServiceTests.PlantProfileReadAsync</c> shape, reused).</summary>
    private static async Task<Profile?> LoadProfileAsync(IDocumentStore store, string subjectId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var q = store.QuerySession();
        return await q.LoadAsync<Profile>(subjectId, ct);
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
        var profileSettings = new ProfileSettingsService(store);
        var moderation = new ModerationService(profileSettings, authz, store);
        return (userInfo, authz, moderation);
    }

    /// <summary>
    /// IMPROVE-UX R05 — compose the same trio but wire the
    /// <see cref="PostService"/> into the <see cref="ModerationService"/>
    /// (the optional R05 posts read seam the <c>ListMyReportsAsync</c>
    /// post-title visibility gate depends on): a real <see cref="PostService"/>
    /// (userInfo / authz / store, the <c>PostServiceTests</c>
    /// <c>new PostService(userInfo, authz, store)</c> shape) + the same
    /// <see cref="UserInfoService"/> / <see cref="AuthorizationService"/> /
    /// <see cref="ProfileSettingsService"/> trio. The
    /// <c>ServicesWithPosts(store).Moderation</c> shape matches
    /// <c>Services(store).Moderation</c> so the R05 tests read identically.
    /// </summary>
    private static (UserInfoService User, AuthorizationService Authz, ModerationService Moderation)
        ServicesWithPosts(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var posts = new PostService(userInfo, authz, store);
        var profileSettings = new ProfileSettingsService(store);
        var moderation = new ModerationService(profileSettings, authz, store, posts: posts);
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

    /// <summary>
    /// ADR 0023 — the reply-report-target lane's own audit rows, scoped to
    /// the reply lane's <c>TargetKind = "reply"</c> (the
    /// <see cref="ModerationService.FileReplyReportAsync"/> filing lane's
    /// hand-written row, mirroring <see cref="PostAudits"/> for the
    /// post-lane's <c>TargetKind = "post"</c> rows).
    /// </summary>
    private static async Task<IReadOnlyList<AccessAudit>> ReplyAudits(IDocumentStore store, string? actor = null)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        var q = s.Query<AccessAudit>().Where(a => a.TargetKind == "reply");
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
