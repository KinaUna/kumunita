using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.ErrorReports;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M32 U07 (ADR 0155) — the <see cref="ErrorReportService"/> M32 additive
/// pins (M32·3 / M32·4 / M32·8 — the design doc §2.4 test 1–6):
/// <list type="number">
/// <item><b>M32_8_MarkResolved_New_Updates_TriageStatus_And_AuditRow</b> —
/// a <c>new</c> report is stamped <c>resolved</c> +
/// <c>ResolvedAt</c>/<c>ResolvedBy</c>/<c>ResolutionNote</c> + exactly one
/// <c>AccessAudit</c> row (<c>Via = Admin</c>, action
/// <c>errorreport.resolve</c>, <c>TargetKind</c> "error-report") (M32·8).</item>
/// <item><b>M32_8_MarkResolved_Triaged_Updates_TriageStatus_And_AuditRow</b>
/// — a <c>triaged</c> report is stamped <c>resolved</c> the same way (the
/// <c>triaged</c> → <c>resolved</c> transition) (M32·8).</item>
/// <item><b>M32_8_MarkResolved_AlreadyResolved_Is_NoOp</b> — re-stamping an
/// already-<c>resolved</c> report returns <c>null</c> (no second audit row,
/// no state change — the M32·8 idempotency pin, the M31·6
/// <see cref="ErrorReportServiceTests"/> precedent) (M32·8).</item>
/// <item><b>M32_8_MarkResolved_Missing_Returns_Null</b> — a missing report
/// returns <c>null</c> (no audit row) (M32·8).</item>
/// <item><b>M32_3_ErrorReport_Doc_FieldSet_M32_Ceiling</b> — the
/// <see cref="ErrorReport"/> doc carries exactly the **15-member M32
/// ceiling** field set (the M31 11 unchanged + the M32 additive 4:
/// <c>Origin</c> / <c>ResolvedAt</c> / <c>ResolvedBy</c> /
/// <c>ResolutionNote</c>) — no field outside the set, no field dropped
/// (the M32·3 / ADR 0155 D1 ceiling; this is the successor to the M31
/// <c>M31_3_ErrorReport_Doc_FieldSet_Ceiling</c> 11-member pin, which U07
/// retired per the U03 flag) (M32·3 / M32·10).</item>
/// <item><b>M32_4_CreateAsync_Origin_General_Stores_ErrorReport</b> — a
/// <see cref="ErrorReportDraft"/> with <c>Origin = "general"</c> (the M32
/// <c>/issues/new</c> general-issue lane, M32·4) stores an
/// <c>ErrorReport</c> row with <c>Origin = "general"</c> + the rest of the
/// M32·4 shape (<c>ExceptionType</c> null, the closed
/// <c>{"error-page","general"}</c> Origin set) (M32·4).</item>
/// </list>
/// </summary>
/// <para>
/// <b>Harness (the U03 idiom, verbatim from the M31
/// <see cref="ErrorReportServiceTests"/>):</b> every <c>*DocTypes.Configure</c>
/// call in this repo lives in the host's <c>AddMarten</c> lambda, not in
/// <c>DependencyInjection.cs</c>. This harness wires the SAME
/// <see cref="ErrorReportDocTypes.Configure"/> call in its own
/// <c>DocumentStore.For</c> lambda (the
/// <see cref="AdminOnboardingServiceTests.BootStoreAsync"/> idiom) — without
/// it the <c>ErrorReport</c> doc is invisible to Marten.
/// <see cref="Kumunita.Core.M1DocTypes.Configure"/> is included because it
/// registers the <c>AccessAudit</c> doc (the audit-row half of every write).
/// </para>
public class ErrorReportResolveTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── 1 — mark a new report resolved: stamp + audit (Via = Admin) (M32·8) ─
    // FACES M32-5: a "new" report is stamped TriageStatus "resolved" +
    // ResolvedAt/ResolvedBy/ResolutionNote + exactly one AccessAudit row
    // (Via = Admin, action errorreport.resolve, TargetKind "error-report").

    [Fact(DisplayName = "M32_8_MarkResolved_New_Updates_TriageStatus_And_AuditRow")]
    public async Task M32_8_MarkResolved_New_Updates_TriageStatus_And_AuditRow()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new ErrorReportService(store);

        var created = await svc.CreateAsync(new ErrorReportDraft(
            SubjectId: "sub-resident-m32-8-1",
            Description: "The group calendar is down",
            ContactEmail: null,
            RequestId: "req-m32-8-1",
            ExceptionType: null,
            UserAgent: null,
            Origin: "general"), ct);
        Assert.Equal("new", created.TriageStatus);

        const string admin = "admin-m32-8-1";
        const string note = "Restarted the calendar worker";
        var before = DateTimeOffset.UtcNow;
        var stamped = await svc.MarkResolvedAsync(created.Id, admin, note, ct);

        // The stamp is applied + returned.
        Assert.NotNull(stamped);
        Assert.Equal("resolved", stamped!.TriageStatus);
        Assert.NotNull(stamped.ResolvedAt);
        Assert.True(stamped.ResolvedAt >= before,
            $"Expected ResolvedAt ({stamped.ResolvedAt}) >= the pre-stamp clock ({before}).");
        Assert.Equal(admin, stamped.ResolvedBy);
        Assert.Equal(note, stamped.ResolutionNote);

        // The audit row: Via = Admin (the M32·8 pin), action errorreport.resolve.
        await using var q = store.QuerySession();
        var resolveAudit = (await q.Query<AccessAudit>()
            .Where(a => a.Action == "errorreport.resolve" && a.TargetKind == "error-report" && a.TargetId == created.Id)
            .ToListAsync(ct)).Single();
        Assert.Equal(AccessVia.Admin, resolveAudit.Via);
        Assert.Equal(admin, resolveAudit.ActorId);
        Assert.Equal(AccessOutcome.Allow, resolveAudit.Outcome);
    }

    // ── 2 — mark a triaged report resolved: the triaged → resolved
    //      transition (M32·8) ──────────────────────────────────────────────
    // The register's Assumptions: "(b) MarkResolvedAsync on a triaged report
    // is the same (the transition triaged → resolved)". A report that is
    // already triaged is still eligible for resolution — MarkResolvedAsync
    // only no-ops on a report already "resolved".

    [Fact(DisplayName = "M32_8_MarkResolved_Triaged_Updates_TriageStatus_And_AuditRow")]
    public async Task M32_8_MarkResolved_Triaged_Updates_TriageStatus_And_AuditRow()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new ErrorReportService(store);

        var created = await svc.CreateAsync(new ErrorReportDraft(
            SubjectId: "sub-resident-m32-8-2",
            Description: "The search returned an error",
            ContactEmail: null,
            RequestId: "req-m32-8-2",
            ExceptionType: "TimeoutException",
            UserAgent: null,
            Origin: "error-page"), ct);

        // Triage it first (the M31 triage lane — the transition source).
        const string triageAdmin = "admin-m32-8-2-triage";
        var triaged = await svc.MarkTriagedAsync(created.Id, triageAdmin, ct);
        Assert.NotNull(triaged);
        Assert.Equal("triaged", triaged!.TriageStatus);

        // Now resolve it (the triaged → resolved transition).
        const string resolveAdmin = "admin-m32-8-2-resolve";
        const string note = "Escalated to the upstream team";
        var stamped = await svc.MarkResolvedAsync(created.Id, resolveAdmin, note, ct);

        Assert.NotNull(stamped);
        Assert.Equal("resolved", stamped!.TriageStatus);
        Assert.NotNull(stamped.ResolvedAt);
        Assert.Equal(resolveAdmin, stamped.ResolvedBy);
        Assert.Equal(note, stamped.ResolutionNote);
        // The triage stamp survives (M32 does not re-shape the triage fields).
        Assert.Equal(triageAdmin, stamped.TriagedBy);

        // The resolve audit row: Via = Admin, action errorreport.resolve.
        await using var q = store.QuerySession();
        var resolveAudit = (await q.Query<AccessAudit>()
            .Where(a => a.Action == "errorreport.resolve" && a.TargetId == created.Id)
            .ToListAsync(ct)).Single();
        Assert.Equal(AccessVia.Admin, resolveAudit.Via);
        Assert.Equal(resolveAdmin, resolveAudit.ActorId);
    }

    // ── 3 — re-resolving an already-resolved report is a no-op (M32·8) ────
    // FACES M32-6: MarkResolvedAsync on an already-"resolved" report returns
    // null — no second audit row, no state change (the M32·8 idempotency pin,
    // the M31·6 MarkTriagedAsync precedent).

    [Fact(DisplayName = "M32_8_MarkResolved_AlreadyResolved_Is_NoOp")]
    public async Task M32_8_MarkResolved_AlreadyResolved_Is_NoOp()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new ErrorReportService(store);

        var created = await svc.CreateAsync(new ErrorReportDraft(
            SubjectId: "sub-resident-m32-8-3",
            Description: "The profile save failed",
            ContactEmail: null,
            RequestId: "req-m32-8-3",
            ExceptionType: null,
            UserAgent: null,
            Origin: "general"), ct);

        const string admin1 = "admin-m32-8-3-first";
        var first = await svc.MarkResolvedAsync(created.Id, admin1, "First resolution", ct);
        Assert.NotNull(first);
        Assert.Equal("resolved", first!.TriageStatus);
        var firstResolvedAt = first.ResolvedAt;
        var firstResolvedBy = first.ResolvedBy;
        var firstNote = first.ResolutionNote;

        // A second resolve (a different actor, a different note) is a no-op.
        const string admin2 = "admin-m32-8-3-second";
        var second = await svc.MarkResolvedAsync(created.Id, admin2, "Second resolution", ct);
        Assert.Null(second); // no-op — M32·8 idempotency

        // No state change: the stored row still carries the FIRST stamp.
        await using var q = store.QuerySession();
        var stored = await q.LoadAsync<ErrorReport>(created.Id, ct);
        Assert.NotNull(stored);
        Assert.Equal("resolved", stored!.TriageStatus);
        Assert.Equal(firstResolvedAt, stored.ResolvedAt);
        Assert.Equal(firstResolvedBy, stored.ResolvedBy);
        Assert.Equal(firstNote, stored.ResolutionNote);
        Assert.NotEqual(admin2, stored.ResolvedBy);
        Assert.NotEqual("Second resolution", stored.ResolutionNote);

        // And no SECOND resolve audit row was written (still exactly one).
        var resolveAudits = await q.Query<AccessAudit>()
            .Where(a => a.Action == "errorreport.resolve" && a.TargetId == created.Id)
            .ToListAsync(ct);
        Assert.Single(resolveAudits);
        Assert.Equal(admin1, resolveAudits[0].ActorId);
    }

    // ── 4 — resolving a missing report returns null (M32·8) ───────────────
    // A report id that was never stored → MarkResolvedAsync returns null,
    // writes no audit row (the register's "(d) on a missing report returns
    // null (no audit row)").

    [Fact(DisplayName = "M32_8_MarkResolved_Missing_Returns_Null")]
    public async Task M32_8_MarkResolved_Missing_Returns_Null()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new ErrorReportService(store);

        const string missingId = "er-m32-8-4-missing";
        const string admin = "admin-m32-8-4";
        var result = await svc.MarkResolvedAsync(missingId, admin, "note", ct);

        Assert.Null(result);

        // No audit row was written for the missing report (the no-op).
        await using var q = store.QuerySession();
        var audits = await q.Query<AccessAudit>()
            .Where(a => a.Action == "errorreport.resolve" && a.TargetId == missingId)
            .ToListAsync(ct);
        Assert.Empty(audits);
    }

    // ── 5 — the ErrorReport doc field set is the 15-member M32 ceiling
    //      (M32·3) ────────────────────────────────────────────────────────
    // ADR 0155 D1 (the M32·3 pin): the doc carries exactly the 15-member
    // field set — the M31 11 (ADR 0154 D1) unchanged + the M32 additive 4.
    // No field outside the set, no field in the set dropped. This is the
    // successor to the M31 11-member pin (M31_3_…_FieldSet_Ceiling, retired
    // by U07 per the U03 flag) — the drift-guard §2.6 pin.

    [Fact(DisplayName = "M32_3_ErrorReport_Doc_FieldSet_M32_Ceiling")]
    public void M32_3_ErrorReport_Doc_FieldSet_M32_Ceiling()
    {
        var actual = typeof(ErrorReport).GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var expected = new[]
        {
            // M31's 11 (ADR 0154 D1) — unchanged (M32·1 / M32·3).
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
            // M32's additive 4 (ADR 0155 D1) — the 15-member M32 ceiling.
            "Origin",
            "ResolvedAt",
            "ResolvedBy",
            "ResolutionNote",
        }.OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.Equal(expected, actual);
        Assert.Equal(15, actual.Count);
    }

    // ── 6 — CreateAsync with Origin = "general" stores the row (M32·4) ────
    // The M32 /issues/new general-issue lane (M32·4): the draft's additive
    // Origin member (closed set {"error-page","general"}) is projected onto
    // the stored ErrorReport row; a general issue is not tied to an error
    // page (ExceptionType null).

    [Fact(DisplayName = "M32_4_CreateAsync_Origin_General_Stores_ErrorReport")]
    public async Task M32_4_CreateAsync_Origin_General_Stores_ErrorReport()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new ErrorReportService(store);

        var draft = new ErrorReportDraft(
            SubjectId: "sub-resident-m32-4",
            Description: "The group calendar is down",
            ContactEmail: "resident@example.com",
            RequestId: "req-m32-4",
            ExceptionType: null,          // a general issue is not tied to an error page (M32·4)
            UserAgent: null,
            Origin: "general");           // the M32·4 pin

        var stored = await svc.CreateAsync(draft, ct);

        // The additive Origin is stored ("general" — the closed set member).
        Assert.Equal("general", stored.Origin);
        // The rest of the M32·4 shape: a general issue carries no exception
        // type; the report is in the "new" triage state with the resolution
        // fields still null (M32·8 — they are stamped only by MarkResolved).
        Assert.Null(stored.ExceptionType);
        Assert.Equal("new", stored.TriageStatus);
        Assert.Null(stored.ResolvedAt);
        Assert.Null(stored.ResolvedBy);
        Assert.Null(stored.ResolutionNote);
        // The closed Origin set — the M32·4 pin ({"error-page","general"}).
        Assert.Contains(stored.Origin, new[] { "error-page", "general" });

        // And the row is persisted (a fresh read returns the same Origin).
        await using var q = store.QuerySession();
        var reloaded = await q.LoadAsync<ErrorReport>(stored.Id, ct);
        Assert.NotNull(reloaded);
        Assert.Equal("general", reloaded!.Origin);
    }

    // ─── Shared helper ────────────────────────────────────────────────────

    /// <summary>
    /// Boot a fresh scratch store with the <c>ErrorReport</c> doc type
    /// (the <see cref="Kumunita.Core.ErrorReportDocTypes.Configure"/>
    /// surface) + the <c>AccessAudit</c> doc (via
    /// <see cref="Kumunita.Core.M1DocTypes.Configure"/>) registered in the
    /// <c>DocumentStore.For</c> lambda — the
    /// <see cref="AdminOnboardingServiceTests.BootStoreAsync"/> idiom
    /// (verbatim from the M31 <see cref="ErrorReportServiceTests"/>).
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
