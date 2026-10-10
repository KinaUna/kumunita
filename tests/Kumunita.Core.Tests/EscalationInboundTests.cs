using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.ErrorReports;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// ESC · U08 (ADR 0159) — the <see cref="ErrorReportService.AcceptInboundAsync"/>
/// receiving-inbox lane pins (ESC·2 / ESC·5 / ESC·7 / ESC·10 — design doc §2.4
/// tests 6–10, names **verbatim**):
/// <list type="number">
/// <item><b>ESC_5_AcceptInbound_ValidToken_Creates_Escalated_Origin</b> — a
/// valid (non-revoked) token + a non-blank description stores a new
/// <c>ErrorReport</c> row with <c>Origin = "escalated"</c> + the four ESC·2
/// additive fields set + the origin's fields projected (ESC·5 / ESC·2) (FACES
/// ESC-3).</item>
/// <item><b>ESC_5_AcceptInbound_InvalidToken_Returns_Error_NoRow</b> — an
/// invalid / revoked / unknown token returns <c>Created == false</c>,
/// <c>Error</c> non-null, **no row, no audit row** (ESC·5 — the Web endpoint
/// maps this to a 401) (FACES ESC-4 / ESC-5).</item>
/// <item><b>ESC_7_AcceptInbound_ReDelivery_Idempotent_NoDuplicate</b> — a
/// re-delivery of the **same** origin report under the **same** token returns
/// the **existing** row (<c>Created == false</c>, <c>Error is null</c>), not a
/// duplicate, and writes **no second** audit row (ESC·7 — the
/// <c>(EscalationTokenId, EscalationSourceId)</c> idempotency key) (FACES
/// ESC-9).</item>
/// <item><b>ESC_2_ErrorReport_Doc_FieldSet_ESC_Ceiling</b> — the
/// <see cref="ErrorReport"/> doc carries exactly the **19-member ESC ceiling**
/// field set (the M32 15 unchanged + the ESC additive 4:
/// <c>FromInstance</c> / <c>EscalationReceivedAt</c> /
/// <c>EscalationSourceId</c> / <c>EscalationTokenId</c>) — no field outside
/// the set, no field dropped (the ESC·2 / ADR 0159 D1 ceiling; this is the
/// successor to the M32 <c>M32_3_ErrorReport_Doc_FieldSet_M32_Ceiling</c>
/// 15-member pin, which U08 retires per the U07 flag) (ESC·2 / ESC·10).</item>
/// <item><b>ESC_10_AcceptInbound_AuditRow_Via_Escalation</b> — a successful
/// inbound writes exactly **one** <c>AccessAudit</c> row with
/// <c>Via = Escalation</c> (action <c>errorreport.inbound</c>,
/// <c>TargetKind</c> "error-report") — the M2M standing (the origin platform
/// is the actor, not a local admin) — and <c>AccessVia.Escalation</c> is
/// present while the 12 frozen values are unchanged (ESC·10) (FACES ESC-3).</item>
/// </list>
/// </summary>
/// <para>
/// <b>Harness (the M32 <see cref="ErrorReportResolveTests"/> idiom):</b> the
/// same <c>DocumentStore.For</c> lambda wiring
/// <see cref="Kumunita.Core.M1DocTypes.Configure"/> (the
/// <c>AccessAudit</c> doc) + <see cref="Kumunita.Core.ErrorReportDocTypes.Configure"/>
/// (the <c>ErrorReport</c> doc, which the four ESC·2 additive fields ride) +
/// <see cref="Kumunita.Core.EscalationDocTypes.Configure"/> (the
/// <c>EscalationToken</c> doc the <see cref="ErrorReportService.AcceptInboundAsync"/>
/// re-validates by id). The <see cref="ErrorReportService"/> is constructed
/// with its **optional** <c>IEscalationTokenService</c> seam (U04's
/// optional-ctor-param precedent) supplied as a live
/// <see cref="EscalationTokenService"/> over the same store — so the
/// re-validation + the idempotent lookup + the received-row write all hit the
/// same live Postgres.
/// </para>
public class EscalationInboundTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── 6 — a valid token + description creates an escalated row (ESC·5) ──
    // FACES ESC-3: the origin admin escalates (valid token + configured
    // endpoint) → the receiving inbound creates an <c>Origin = "escalated"</c>
    // <c>ErrorReport</c> row (+ the four ESC·2 additive fields + one
    // AccessAudit row, Via = Escalation) → the origin marks it resolved.

    [Fact(DisplayName = "ESC_5_AcceptInbound_ValidToken_Creates_Escalated_Origin")]
    public async Task ESC_5_AcceptInbound_ValidToken_Creates_Escalated_Origin()
    {
        var (store, svc) = await BootAsync();
        var ct = TestContext.Current.CancellationToken;
        var tokens = new EscalationTokenService(store);

        // The receiving admin issues the token (the ESC·3 / ESC·9 lane — the
        // plaintext is the Web endpoint's Bearer credential, resolved to the
        // token id before AcceptInboundAsync — Core stays HTTP-free, ESC·6).
        var (plaintext, storedToken) = await tokens.GenerateAsync("Origin platform X", "admin-esc-6", ct);
        _ = plaintext; // the Web endpoint resolved it; AcceptInboundAsync re-validates by id

        var originCreated = DateTimeOffset.UtcNow;
        var draft = new InboundReport(
            TokenId: storedToken.Id,
            SourceReportId: "origin-report-6",
            FromInstance: "Origin platform X",
            Description: "The calendar is down on our instance",
            ContactEmail: "ops@example.com",
            OriginSubjectId: "origin-subject-6",
            RequestId: "origin-req-6",
            ExceptionType: null,
            OriginCreated: originCreated);

        var result = await svc.AcceptInboundAsync(draft, ct);

        Assert.True(result.Created);
        Assert.Null(result.Error);
        var row = result.Row;
        Assert.Equal("escalated", row.Origin);                 // the ESC·2 additive value
        Assert.Equal("Origin platform X", row.FromInstance);    // the accepting token's Label (ESC·8)
        Assert.Equal(draft.SourceReportId, row.EscalationSourceId); // the ESC·7 idempotency key
        Assert.Equal(storedToken.Id, row.EscalationTokenId);     // which token accepted it (ESC·2)
        Assert.NotNull(row.EscalationReceivedAt);               // the receive instant (ESC·2)
        // The origin's fields are projected onto the row (the M32 8-field payload).
        Assert.Equal(draft.OriginSubjectId, row.SubjectId);
        Assert.Equal(draft.Description, row.Description);
        Assert.Equal(draft.ContactEmail, row.ContactEmail);
        Assert.Equal(draft.RequestId, row.RequestId);
        Assert.Null(row.ExceptionType);
        Assert.Equal(originCreated, row.Created);
        Assert.Equal("new", row.TriageStatus);                  // the M31 default
        // The row is persisted (a fresh read returns the same Origin).
        await using var q = store.QuerySession();
        var reloaded = await q.LoadAsync<ErrorReport>(row.Id, ct);
        Assert.NotNull(reloaded);
        Assert.Equal("escalated", reloaded!.Origin);
    }

    // ── 7 — an invalid token returns Error + no row (ESC·5) ───────────────
    // FACES ESC-4 / ESC-5: a token the receiving admin never issued (unknown)
    // or one that has been revoked (the handoff gate) is invalid →
    // AcceptInboundAsync returns Created == false + Error non-null, writes NO
    // row and NO audit row. The Web endpoint maps the invalid-token case to a
    // 401 (ESC·5). This test exercises the unknown-token path; the revoked-
    // token path is the same code branch (the token row's RevokedAt != null).

    [Fact(DisplayName = "ESC_5_AcceptInbound_InvalidToken_Returns_Error_NoRow")]
    public async Task ESC_5_AcceptInbound_InvalidToken_Returns_Error_NoRow()
    {
        var (store, svc) = await BootAsync();
        var ct = TestContext.Current.CancellationToken;

        // A real token exists so the failure is a *mismatch*, not an empty
        // table — the unknown-token draft references a token id that was never
        // stored (the ValidateAsync / re-validation returns no row, ESC·5).
        var (_, storedToken) = await new EscalationTokenService(store)
            .GenerateAsync("Origin platform Y", "admin-esc-7", ct);
        _ = storedToken;

        var auditCountBefore = await CountInboundAuditsAsync(store, ct);

        var draft = new InboundReport(
            TokenId: "nonexistent-token-id-7",   // unknown — never stored
            SourceReportId: "origin-report-7",
            FromInstance: "Origin platform Y",
            Description: "A report that must not be accepted",
            ContactEmail: null,
            OriginSubjectId: "origin-subject-7",
            RequestId: "origin-req-7",
            ExceptionType: null,
            OriginCreated: DateTimeOffset.UtcNow);

        var result = await svc.AcceptInboundAsync(draft, ct);

        Assert.False(result.Created);
        Assert.NotNull(result.Error);
        // No row was written (the invalid token never reaches the write lane).
        await using var q = store.QuerySession();
        var rows = (await q.Query<ErrorReport>()
            .Where(r => r.EscalationSourceId == "origin-report-7")
            .ToListAsync(ct)).ToList();
        Assert.Empty(rows);
        // No audit row was written (the no-op, the M31·4 "read is not an
        // access decision" precedent).
        var auditCountAfter = await CountInboundAuditsAsync(store, ct);
        Assert.Equal(auditCountBefore, auditCountAfter);
    }

    // ── 8 — a re-delivery under the same token is idempotent (ESC·7) ──────
    // FACES ESC-9: a re-delivery of the **same** origin report under the
    // **same** token returns the **existing** row (Created == false, Error is
    // null — the idempotent path, NOT the error path) — no duplicate, no
    // second audit row. The key is (EscalationTokenId, EscalationSourceId).

    [Fact(DisplayName = "ESC_7_AcceptInbound_ReDelivery_Idempotent_NoDuplicate")]
    public async Task ESC_7_AcceptInbound_ReDelivery_Idempotent_NoDuplicate()
    {
        var (store, svc) = await BootAsync();
        var ct = TestContext.Current.CancellationToken;
        var tokens = new EscalationTokenService(store);

        var (_, storedToken) = await tokens.GenerateAsync("Origin platform Z", "admin-esc-8", ct);

        InboundReport draft = new InboundReport(
            TokenId: storedToken.Id,
            SourceReportId: "origin-report-8",
            FromInstance: "Origin platform Z",
            Description: "The first delivery of a dedup'd report",
            ContactEmail: null,
            OriginSubjectId: "origin-subject-8",
            RequestId: "origin-req-8",
            ExceptionType: null,
            OriginCreated: DateTimeOffset.UtcNow);

        var first = await svc.AcceptInboundAsync(draft, ct);
        Assert.True(first.Created);
        Assert.Null(first.Error);
        var firstRowId = first.Row.Id;

        var inboundAuditsAfterFirst = await CountInboundAuditsAsync(store, ct);
        Assert.Equal(1, inboundAuditsAfterFirst); // exactly one audit row on the first delivery

        // The re-delivery: same token + same origin report id → idempotent.
        var second = await svc.AcceptInboundAsync(draft, ct);
        Assert.False(second.Created); // the idempotent path (NOT the error path)
        Assert.Null(second.Error);
        Assert.Equal(firstRowId, second.Row.Id); // the EXISTING row, not a duplicate

        // No duplicate row on the (token, source) key.
        await using var q = store.QuerySession();
        var rows = (await q.Query<ErrorReport>()
            .Where(r => r.EscalationTokenId == storedToken.Id && r.EscalationSourceId == "origin-report-8")
            .ToListAsync(ct)).ToList();
        Assert.Single(rows);
        Assert.Equal(firstRowId, rows[0].Id);

        // No second audit row (the idempotent path writes none).
        var inboundAuditsAfterSecond = await CountInboundAuditsAsync(store, ct);
        Assert.Equal(inboundAuditsAfterFirst, inboundAuditsAfterSecond);
    }

    // ── 9 — the ErrorReport doc field set is the 19-member ESC ceiling
    //      (ESC·2) ────────────────────────────────────────────────────────
    // ADR 0159 D1 (the ESC·2 pin): the doc carries exactly the 19-member field
    // set — the M32 15 (ADR 0155 D1) unchanged + the ESC additive 4
    // (FromInstance / EscalationReceivedAt / EscalationSourceId /
    // EscalationTokenId). No field outside the set, no field dropped. This is
    // the successor to the M32 15-member pin (M32_3_ErrorReport_Doc_FieldSet_
    // M32_Ceiling, retired by U08 per the U07 flag) — the drift-guard §2.6 pin.

    [Fact(DisplayName = "ESC_2_ErrorReport_Doc_FieldSet_ESC_Ceiling")]
    public void ESC_2_ErrorReport_Doc_FieldSet_ESC_Ceiling()
    {
        var actual = typeof(ErrorReport).GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var expected = new[]
        {
            // M31's 11 (ADR 0154 D1) — unchanged (M32·1 / M32·3 / ESC·2).
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
            // M32's additive 4 (ADR 0155 D1) — unchanged (ESC·2).
            "Origin",
            "ResolvedAt",
            "ResolvedBy",
            "ResolutionNote",
            // ESC's additive 4 (ADR 0159 D1) — the 19-member ESC ceiling.
            "FromInstance",
            "EscalationReceivedAt",
            "EscalationSourceId",
            "EscalationTokenId",
        }.OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.Equal(expected, actual);
        Assert.Equal(19, actual.Count);
    }

    // ── 10 — a successful inbound audits Via = Escalation (ESC·10) ────────
    // FACES ESC-3: the successful inbound writes exactly **one**
    // AccessAudit row with Via = Escalation (action errorreport.inbound,
    // TargetKind "error-report") — the M2M standing (the origin platform, not
    // a local admin, is the actor). AccessVia.Escalation is present while the
    // 12 frozen values are unchanged (ESC·10 — no re-shape of the frozen set).

    [Fact(DisplayName = "ESC_10_AcceptInbound_AuditRow_Via_Escalation")]
    public async Task ESC_10_AcceptInbound_AuditRow_Via_Escalation()
    {
        var (store, svc) = await BootAsync();
        var ct = TestContext.Current.CancellationToken;
        var tokens = new EscalationTokenService(store);

        var (_, storedToken) = await tokens.GenerateAsync("Origin platform W", "admin-esc-10", ct);

        var draft = new InboundReport(
            TokenId: storedToken.Id,
            SourceReportId: "origin-report-10",
            FromInstance: "Origin platform W",
            Description: "An escalated issue",
            ContactEmail: null,
            OriginSubjectId: "origin-subject-10",
            RequestId: "origin-req-10",
            ExceptionType: null,
            OriginCreated: DateTimeOffset.UtcNow);

        var result = await svc.AcceptInboundAsync(draft, ct);
        Assert.True(result.Created);
        var reportId = result.Row.Id;

        // Exactly one AccessAudit row for the inbound, Via = Escalation
        // (the ESC·10 pin — the M2M standing).
        await using var q = store.QuerySession();
        var audits = (await q.Query<AccessAudit>()
            .Where(a => a.Action == "errorreport.inbound" && a.TargetKind == "error-report" && a.TargetId == reportId)
            .ToListAsync(ct)).ToList();
        Assert.Single(audits);
        Assert.Equal(AccessVia.Escalation, audits[0].Via);
        Assert.Equal(AccessOutcome.Allow, audits[0].Outcome);
        // The actor is the origin platform's subject (the M2M standing — the
        // receiving admin is not the actor, ESC·10).
        Assert.Equal("origin-subject-10", audits[0].ActorId);

        // AccessVia.Escalation is a real value + the 12 frozen values are
        // unchanged (ESC·10 — the 13-value set is the 12 frozen + the ESC 1).
        var values = Enum.GetValues<AccessVia>()
            .OrderBy(v => v.ToString(), StringComparer.Ordinal)
            .ToList();
        Assert.Contains(AccessVia.Escalation, values);
        Assert.Equal(13, values.Count);
        foreach (var frozen in new[]
        {
            AccessVia.Owner, AccessVia.Audience, AccessVia.Delegation,
            AccessVia.Moderator, AccessVia.Report, AccessVia.BreakGlass,
            AccessVia.Admin, AccessVia.Group, AccessVia.Guardian,
            AccessVia.Community, AccessVia.Resident, AccessVia.Anonymous,
        })
        {
            Assert.Contains(frozen, values);
        }
    }

    // ─── Shared helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Boot a fresh scratch store with the <c>AccessAudit</c> doc
    /// (<see cref="Kumunita.Core.M1DocTypes.Configure"/>), the
    /// <c>ErrorReport</c> doc (<see cref="Kumunita.Core.ErrorReportDocTypes.Configure"/>,
    /// which the four ESC·2 additive fields ride), and the
    /// <c>EscalationToken</c> + <c>EscalationOutboundConfig</c> docs (the
    /// <see cref="Kumunita.Core.EscalationDocTypes.Configure"/> surface) — the
    /// <see cref="ErrorReportResolveTests.BootStoreAsync"/> idiom, extended for
    /// the ESC surface. Returns the live store + a <see cref="ErrorReportService"/>
    /// wired with the optional <see cref="IEscalationTokenService"/> seam
    /// (U04's optional-ctor-param precedent) over the same store.
    /// </summary>
    private async Task<(IDocumentStore store, ErrorReportService svc)> BootAsync()
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
            EscalationDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        var svc = new ErrorReportService(store, new EscalationTokenService(store));
        return (store, svc);
    }

    /// <summary>Count the <c>errorreport.inbound</c> audit rows (the
    /// TargetKind "error-report" + action filter) — the idempotency + no-audit
    /// discriminator (ESC·7).</summary>
    private static async Task<int> CountInboundAuditsAsync(IDocumentStore store, CancellationToken ct)
    {
        await using var q = store.QuerySession();
        return await q.Query<AccessAudit>()
            .Where(a => a.Action == "errorreport.inbound" && a.TargetKind == "error-report")
            .CountAsync(ct);
    }
}
