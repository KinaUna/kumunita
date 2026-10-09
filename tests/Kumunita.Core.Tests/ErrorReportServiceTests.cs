using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.ErrorReports;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M31 U06 (ADR 0154) — the <see cref="ErrorReportService"/> read + two
/// audited-write-lane pins (the ADR 0006 C3 single-write-lane shape,
/// M31·3 / M31·4 / M31·5 / M31·6 / M31·10):
/// <list type="number">
/// <item><b>M31_1_CreateAnonymous_Stores_ErrorReport_And_AuditRow</b> — a
/// blank <c>SubjectId</c> (an anonymous 500 submitter) stores one
/// <c>ErrorReport</c> row (<c>TriageStatus = "new"</c>,
/// <c>TriagedAt</c>/<c>TriagedBy</c> null) + exactly one <c>AccessAudit</c>
/// row (<c>Via = Anonymous</c>, action <c>errorreport.create</c>,
/// <c>TargetKind</c> "error-report" — the §2.1 pin) (M31·5).</item>
/// <item><b>M31_2_CreateSignedIn_Stores_SubjectId_And_AuditRow</b> — a
/// non-blank <c>SubjectId</c> (a signed-in resident) stores the row with the
/// <c>SubjectId</c> set + exactly one <c>AccessAudit</c> row
/// (<c>Via = Resident</c>, the §2.1 pin) (M31·5).</item>
/// <item><b>M31_5_Create_Writes_ExactlyOne_AccessAuditRow</b> — one write
/// session commits the doc + exactly one audit row (the ADR 0006 C3
/// single-write-lane shape; a double-submit is two rows by design — the
/// admin's dedup call, not a system constraint) (M31·5).</item>
/// <item><b>M31_6_MarkTriaged_New_Updates_TriageStatus_And_AuditRow</b> —
/// a <c>new</c> report is stamped <c>triaged</c> +
/// <c>TriagedAt</c>/<c>TriagedBy</c> + exactly one <c>AccessAudit</c> row
/// (<c>Via = Admin</c>, action <c>errorreport.triage</c>) (M31·6).</item>
/// <item><b>M31_6_MarkTriaged_AlreadyTriaged_Is_NoOp</b> — re-stamping an
/// already-<c>triaged</c> report returns <c>null</c> (no second audit row,
/// no state change — the M31·6 idempotency pin) (M31·6).</item>
/// <item><b>M31_6_MarkTriaged_Writes_ExactlyOne_AccessAuditRow</b> — the
/// effective triage writes exactly one <c>AccessAudit</c> row
/// (<c>Via = Admin</c>) (M31·6).</item>
/// <item><b>M31_4_List_Returns_All_Reports_NewestFirst</b> — the
/// <c>ListAsync</c> read returns every report ordered by
/// <c>Created DESC</c>, a read, not an access decision (no <c>AccessAudit</c>
/// row) (M31·4).</item>
/// <item><b>M31_3_ErrorReport_Doc_FieldSet_Ceiling</b> — the <c>ErrorReport</c>
/// doc carries exactly the 11-member field set pinned in §2.2 (D1) — no
/// field outside the set, no field in the set dropped (the M31·3 / ADR 0154
/// D1 ceiling, the drift-guard §2.6 pin) (M31·3 / M31·10).</item>
/// </list>
/// </summary>
/// <para>
/// <b>Harness (the U03 idiom):</b> every <c>*DocTypes.Configure</c> call in
/// this repo lives in the host's <c>AddMarten</c> lambda, not in
/// <c>DependencyInjection.cs</c>. This harness wires the SAME
/// <see cref="Kumunita.Core.ErrorReportDocTypes.Configure"/> call in its own
/// <c>DocumentStore.For</c> lambda (the <see cref="AdminOnboardingServiceTests.BootStoreAsync"/>
/// idiom) — without it the <c>ErrorReport</c> doc is invisible to Marten.
/// <see cref="Kumunita.Core.M1DocTypes.Configure"/> is included because it
/// registers the <c>AccessAudit</c> doc (the audit-row half of every write).
/// </para>
public class ErrorReportServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── 1 — anonymous create: row + audit (Via = Anonymous) (M31·5) ───────
    // A blank SubjectId (an anonymous 500 submitter, FACES M31-1) stores the
    // ErrorReport row (TriageStatus "new", TriagedAt/TriagedBy null) + exactly
    // one AccessAudit row with the §2.1 pin's Anonymous Via tag.

    [Fact(DisplayName = "M31_1_CreateAnonymous_Stores_ErrorReport_And_AuditRow")]
    public async Task M31_1_CreateAnonymous_Stores_ErrorReport_And_AuditRow()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new ErrorReportService(store);

        var draft = new ErrorReportDraft(
            SubjectId: string.Empty,        // anonymous (blank)
            Description: "I was trying to RSVP for the Saturday event and the page went blank",
            ContactEmail: null,
            RequestId: "req-anon-0001",
            ExceptionType: "NullReferenceException",
            UserAgent: "test-agent");

        var stored = await svc.CreateAsync(draft, ct);

        // The row is stored with the anonymous shape + the M31 floor.
        Assert.False(string.IsNullOrWhiteSpace(stored.Id));
        Assert.Equal(string.Empty, stored.SubjectId);
        Assert.Equal("I was trying to RSVP for the Saturday event and the page went blank", stored.Description);
        Assert.Equal("req-anon-0001", stored.RequestId);
        Assert.Equal("NullReferenceException", stored.ExceptionType);
        Assert.Equal("test-agent", stored.UserAgent);
        Assert.Equal("new", stored.TriageStatus);
        Assert.Null(stored.TriagedAt);
        Assert.Null(stored.TriagedBy);

        // The audit row: Via = Anonymous (the §2.1 pin for a blank SubjectId),
        // action errorreport.create, TargetKind "error-report", Allow.
        await using var q = store.QuerySession();
        var audit = (await q.Query<AccessAudit>()
            .Where(a => a.Action == "errorreport.create" && a.TargetKind == "error-report")
            .ToListAsync(ct)).Single();
        Assert.Equal(AccessVia.Anonymous, audit.Via);
        Assert.Equal(string.Empty, audit.ActorId);
        Assert.Equal(stored.Id, audit.TargetId);
        Assert.Equal(AccessOutcome.Allow, audit.Outcome);
    }

    // ── 2 — signed-in create: SubjectId set + audit (Via = Resident) (M31·5) ─
    // A non-blank SubjectId (a signed-in resident, FACES M31-2) stores the row
    // with the SubjectId set + exactly one AccessAudit row (Via = Resident,
    // the §2.1 pin / ADR 0041).

    [Fact(DisplayName = "M31_2_CreateSignedIn_Stores_SubjectId_And_AuditRow")]
    public async Task M31_2_CreateSignedIn_Stores_SubjectId_And_AuditRow()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new ErrorReportService(store);

        const string residentSubject = "sub-resident-m31-2";
        var draft = new ErrorReportDraft(
            SubjectId: residentSubject,   // signed-in (non-blank)
            Description: "The community page showed a spinner and then nothing",
            ContactEmail: "resident@example.com",
            RequestId: "req-res-0002",
            ExceptionType: null,          // IExceptionHandlerFeature absent
            UserAgent: null);

        var stored = await svc.CreateAsync(draft, ct);

        Assert.Equal(residentSubject, stored.SubjectId);
        Assert.Equal("resident@example.com", stored.ContactEmail);
        Assert.Null(stored.ExceptionType);
        Assert.Null(stored.UserAgent);
        Assert.Equal("new", stored.TriageStatus);

        // The audit row: Via = Resident (the §2.1 pin for a non-blank SubjectId).
        await using var q = store.QuerySession();
        var audit = (await q.Query<AccessAudit>()
            .Where(a => a.Action == "errorreport.create" && a.TargetKind == "error-report")
            .ToListAsync(ct)).Single();
        Assert.Equal(AccessVia.Resident, audit.Via);
        Assert.Equal(residentSubject, audit.ActorId);
        Assert.Equal(residentSubject, audit.EffectivePrincipalId);
        Assert.Equal(stored.Id, audit.TargetId);
    }

    // ── 3 — create writes EXACTLY ONE AccessAudit row (M31·5, C3) ──────────
    // One write session commits the doc + exactly one audit row. The
    // "exactly one" is the ADR 0006 C3 single-write-lane pin — the repo's
    // "exactly one AccessAudit row" assertion idiom (the
    // AdminOnboardingServiceTests.CompleteAsync_WritesOneAccessAuditRow shape).

    [Fact(DisplayName = "M31_5_Create_Writes_ExactlyOne_AccessAuditRow")]
    public async Task M31_5_Create_Writes_ExactlyOne_AccessAuditRow()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new ErrorReportService(store);

        var draft = new ErrorReportDraft(
            SubjectId: "sub-resident-m31-5",
            Description: "Posting a reply failed",
            ContactEmail: null,
            RequestId: "req-m31-5",
            ExceptionType: null,
            UserAgent: null);

        var stored = await svc.CreateAsync(draft, ct);

        // Exactly one audit row for this report (C3 — one write session).
        await using var q = store.QuerySession();
        var auditRows = await q.Query<AccessAudit>()
            .Where(a => a.TargetKind == "error-report" && a.TargetId == stored.Id)
            .ToListAsync(ct);
        Assert.Single(auditRows);
        Assert.Equal("errorreport.create", auditRows[0].Action);
        Assert.Equal(AccessVia.Resident, auditRows[0].Via);
        Assert.Equal(AccessOutcome.Allow, auditRows[0].Outcome);

        // And exactly one ErrorReport row for the same write.
        await using var q2 = store.QuerySession();
        var reports = await q2.Query<ErrorReport>()
            .Where(r => r.RequestId == "req-m31-5")
            .ToListAsync(ct);
        Assert.Single(reports);
    }

    // ── 4 — mark a new report triaged: stamp + audit (Via = Admin) (M31·6) ──
    // FACES M31-4: a new report is stamped TriageStatus "triaged" +
    // TriagedAt/TriagedBy + exactly one AccessAudit row (Via = Admin, action
    // errorreport.triage, TargetKind "error-report").

    [Fact(DisplayName = "M31_6_MarkTriaged_New_Updates_TriageStatus_And_AuditRow")]
    public async Task M31_6_MarkTriaged_New_Updates_TriageStatus_And_AuditRow()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new ErrorReportService(store);

        var created = await svc.CreateAsync(new ErrorReportDraft(
            SubjectId: "sub-resident-m31-6",
            Description: "The sign-in page timed out",
            ContactEmail: null,
            RequestId: "req-m31-6",
            ExceptionType: "TimeoutException",
            UserAgent: null), ct);

        var admin = "admin-m31-6";
        var before = DateTimeOffset.UtcNow;
        var stamped = await svc.MarkTriagedAsync(created.Id, admin, ct);

        // The stamp is applied + returned.
        Assert.NotNull(stamped);
        Assert.Equal("triaged", stamped!.TriageStatus);
        Assert.NotNull(stamped.TriagedAt);
        Assert.True(stamped.TriagedAt >= before,
            $"Expected TriagedAt ({stamped.TriagedAt}) >= the pre-stamp clock ({before}).");
        Assert.Equal(admin, stamped.TriagedBy);

        // The audit row: Via = Admin (the §2.1 pin), action errorreport.triage.
        await using var q = store.QuerySession();
        var triageAudit = (await q.Query<AccessAudit>()
            .Where(a => a.Action == "errorreport.triage" && a.TargetKind == "error-report" && a.TargetId == created.Id)
            .ToListAsync(ct)).Single();
        Assert.Equal(AccessVia.Admin, triageAudit.Via);
        Assert.Equal(admin, triageAudit.ActorId);
        Assert.Equal(AccessOutcome.Allow, triageAudit.Outcome);
    }

    // ── 5 — re-marking an already-triaged report is a no-op (M31·6) ────────
    // FACES M31-5: MarkTriagedAsync on an already-"triaged" report returns
    // null — no second audit row, no state change (the M31·6 idempotency pin).

    [Fact(DisplayName = "M31_6_MarkTriaged_AlreadyTriaged_Is_NoOp")]
    public async Task M31_6_MarkTriaged_AlreadyTriaged_Is_NoOp()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new ErrorReportService(store);

        var created = await svc.CreateAsync(new ErrorReportDraft(
            SubjectId: "sub-resident-m31-6b",
            Description: "The search returned an error",
            ContactEmail: null,
            RequestId: "req-m31-6b",
            ExceptionType: null,
            UserAgent: null), ct);

        const string admin1 = "admin-m31-6-first";
        var first = await svc.MarkTriagedAsync(created.Id, admin1, ct);
        Assert.NotNull(first);
        Assert.Equal("triaged", first!.TriageStatus);
        var firstTriagedAt = first.TriagedAt;
        var firstTriagedBy = first.TriagedBy;

        // A second triage (a different actor) is a no-op.
        const string admin2 = "admin-m31-6-second";
        var second = await svc.MarkTriagedAsync(created.Id, admin2, ct);
        Assert.Null(second); // no-op — M31·6 idempotency

        // No state change: the stored row still carries the FIRST stamp.
        await using var q = store.QuerySession();
        var stored = await q.LoadAsync<ErrorReport>(created.Id, ct);
        Assert.NotNull(stored);
        Assert.Equal("triaged", stored!.TriageStatus);
        Assert.Equal(firstTriagedAt, stored.TriagedAt);
        Assert.Equal(firstTriagedBy, stored.TriagedBy);
        Assert.NotEqual(admin2, stored.TriagedBy);

        // And no SECOND triage audit row was written (still exactly one).
        var triageAudits = await q.Query<AccessAudit>()
            .Where(a => a.Action == "errorreport.triage" && a.TargetId == created.Id)
            .ToListAsync(ct);
        Assert.Single(triageAudits);
        Assert.Equal(admin1, triageAudits[0].ActorId);
    }

    // ── 6 — mark triaged writes EXACTLY ONE AccessAudit row (M31·6, C3) ────
    // The effective triage commits the stamped doc + exactly one audit row in
    // one write session (the ADR 0006 C3 single-write-lane shape).

    [Fact(DisplayName = "M31_6_MarkTriaged_Writes_ExactlyOne_AccessAuditRow")]
    public async Task M31_6_MarkTriaged_Writes_ExactlyOne_AccessAuditRow()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new ErrorReportService(store);

        var created = await svc.CreateAsync(new ErrorReportDraft(
            SubjectId: "sub-resident-m31-6c",
            Description: "The profile save failed",
            ContactEmail: null,
            RequestId: "req-m31-6c",
            ExceptionType: null,
            UserAgent: null), ct);

        const string admin = "admin-m31-6c";
        await svc.MarkTriagedAsync(created.Id, admin, ct);

        // Exactly one errorreport.triage audit row for this report.
        await using var q = store.QuerySession();
        var triageAudits = await q.Query<AccessAudit>()
            .Where(a => a.Action == "errorreport.triage" && a.TargetKind == "error-report" && a.TargetId == created.Id)
            .ToListAsync(ct);
        Assert.Single(triageAudits);
        Assert.Equal(AccessVia.Admin, triageAudits[0].Via);
        Assert.Equal(admin, triageAudits[0].ActorId);
        Assert.Equal(created.Id, triageAudits[0].TargetId);
        Assert.Equal(AccessOutcome.Allow, triageAudits[0].Outcome);
    }

    // ── 7 — ListAsync returns all reports newest-first, a read (M31·4) ─────
    // FACES M31-3: the read returns every report ordered by Created DESC, and
    // writes NO AccessAudit row (a read, not an access decision — M31·4).
    // Seeded directly with distinct Created instants so the ordering is
    // deterministic (the service's CreateAsync stamps UtcNow, which could
    // collide under a fast clock).

    [Fact(DisplayName = "M31_4_List_Returns_All_Reports_NewestFirst")]
    public async Task M31_4_List_Returns_All_Reports_NewestFirst()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new ErrorReportService(store);

        // Seed three reports with distinct Created instants (oldest → newest).
        var t0 = DateTimeOffset.UtcNow.AddHours(-3);
        var t1 = DateTimeOffset.UtcNow.AddHours(-2);
        var t2 = DateTimeOffset.UtcNow.AddHours(-1);

        await using (var w = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            w.Store(new ErrorReport { Id = "er-list-oldest", SubjectId = "sub-a", Description = "oldest", RequestId = "r-oldest", Created = t0, TriageStatus = "new" });
            w.Store(new ErrorReport { Id = "er-list-mid", SubjectId = "sub-b", Description = "mid", RequestId = "r-mid", Created = t1, TriageStatus = "new" });
            w.Store(new ErrorReport { Id = "er-list-newest", SubjectId = "sub-c", Description = "newest", RequestId = "r-newest", Created = t2, TriageStatus = "new" });
            await w.SaveChangesAsync(ct);
        }

        var auditCountBefore = await CountErrorReportAuditsAsync(store, ct);

        var listed = await svc.ListAsync(100, ct);

        // All three, ordered Created DESC (newest first).
        Assert.Equal(3, listed.Count);
        Assert.Equal("er-list-newest", listed[0].Id);
        Assert.Equal("er-list-mid", listed[1].Id);
        Assert.Equal("er-list-oldest", listed[2].Id);

        // The read wrote NO AccessAudit row (M31·4 — a read, not an access
        // decision).
        var auditCountAfter = await CountErrorReportAuditsAsync(store, ct);
        Assert.Equal(auditCountBefore, auditCountAfter);
    }

    // ── 8 — the ErrorReport doc field set is the 11-member ceiling (M31·3) ─
    // D1 (ADR 0154, §2.2): the doc carries exactly the 11-member field set —
    // no field outside the set, no field in the set dropped. This is the
    // drift-guard §2.6 pin (a re-shape is a `## U<m> — Drift pause`).

    [Fact(DisplayName = "M31_3_ErrorReport_Doc_FieldSet_Ceiling")]
    public void M31_3_ErrorReport_Doc_FieldSet_Ceiling()
    {
        var actual = typeof(ErrorReport).GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var expected = new[]
        {
            "Id",
            "SubjectId",
            "Description",
            "ContactEmail",
            "RequestId",
            "ExceptionType",
            "UserAgent",
            "Created",
            "TriageStatus",
            "TriagedAt",
            "TriagedBy",
        }.OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.Equal(expected, actual);
    }

    // ─── Shared helpers ─────────────────────────────────────────────────────

    private static async Task<int> CountErrorReportAuditsAsync(IDocumentStore store, CancellationToken ct)
    {
        await using var q = store.QuerySession();
        return await q.Query<AccessAudit>()
            .Where(a => a.TargetKind == "error-report")
            .CountAsync(ct);
    }

    /// <summary>
    /// Boot a fresh scratch store with the <c>ErrorReport</c> doc type
    /// (the <see cref="Kumunita.Core.ErrorReportDocTypes.Configure"/> surface)
    /// + the <c>AccessAudit</c> doc (via <see cref="Kumunita.Core.M1DocTypes.Configure"/>)
    /// registered in the <c>DocumentStore.For</c> lambda — the
    /// <see cref="AdminOnboardingServiceTests.BootStoreAsync"/> idiom.
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
            ErrorReportDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }
}
