namespace Kumunita.Core.ErrorReports;

/// <summary>
/// The inbound escalation payload (ADR 0159, ESC·5 / ESC·7). A **separate**
/// Core record — it is **not** a re-shape of the M31 / M32
/// <see cref="ErrorReportDraft"/> (ESC·1): the inbound payload carries the
/// *origin's* fields + the *receiving* platform's identity (the accepting
/// token's id + label), which the locally-filed draft does not. The
/// <c>TokenId</c> is the resolved id of the <see cref="EscalationToken"/> the
/// Web inbound endpoint validated (it presents the <c>Bearer</c> plaintext to
/// <see cref="IEscalationTokenService.ValidateAsync"/> before calling
/// <see cref="IErrorReportService.AcceptInboundAsync"/> — Core stays
/// HTTP-free, ESC·6 / ADR 0006-D). The two idempotency-key members
/// (<c>TokenId</c> + <c>SourceReportId</c>) are the ESC·7 pair a re-delivery
/// is deduplicated on.
/// </summary>
public sealed record InboundReport(
    string TokenId,             // which token accepted it (ESC·7 idempotency key)
    string SourceReportId,      // the origin's report id (ESC·7 idempotency key)
    string FromInstance,        // the accepting token's Label (ESC·8 display)
    string Description,         // the origin's description (required)
    string? ContactEmail,       // the origin's contact email (optional)
    string OriginSubjectId,     // the origin's SubjectId ("" for anonymous)
    string RequestId,           // the origin's request id
    string? ExceptionType,      // the origin's exception type (null for a general issue)
    DateTimeOffset OriginCreated); // the origin's Created

/// <summary>
/// The acceptance result of <see cref="IErrorReportService.AcceptInboundAsync"/>
/// (ESC·5 / ESC·7).
/// <list type="bullet">
///   <item><c>Created == true</c> → a new <c>Origin = "escalated"</c>
///       <see cref="ErrorReport"/> row was written (+ exactly one
///       <c>AccessAudit</c> row, <c>Via = Escalation</c>, action
///       "errorreport.inbound" — the M2M standing, ESC·10).</item>
///   <item><c>Created == false</c> (<c>Error is null</c>) → the idempotent
///       path (ESC·7): the existing row is returned, no duplicate, no second
///       audit row.</item>
///   <item><c>Error non-null</c> → a validation failure (invalid / revoked
///       token, or a blank description) — no row, no audit row. The Web
///       endpoint maps the invalid-token case to a 401 (ESC·5).</item>
/// </list>
/// </summary>
public sealed record InboundResult(bool Created, ErrorReport Row, string? Error);
