using Kumunita.Core.ErrorReports;
using Kumunita.Core.Localization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>POST /escalations/inbound</c> token-gated acceptance endpoint (ADR
/// 0159, ESC·5 / ESC·6 / ESC·7) — the **receiving** platform's entry point for
/// an origin platform's escalated issue. This is a **machine-to-machine**
/// surface: the credential is the <c>Authorization: Bearer &lt;token&gt;</c>
/// header (the plaintext <see cref="EscalationToken"/> the receiving admin
/// issued, presented by the origin's <see cref="Services.EscalationForwarder"/>
/// when it escalates), **not** a session. So there is deliberately **no**
/// <c>[Authorize]</c> gate (the ESC·5 pin — the M31·2 / M32·4 "public,
/// anonymous-safe" precedent, applied to the M2M surface). Core stays
/// HTTP-free (ADR 0006-D / ESC·6): this endpoint parses the <c>Bearer</c>
/// credential + the JSON body (Web-layer HTTP) and delegates the token
/// **validation** (hash compare) to
/// <see cref="IEscalationTokenService.ValidateAsync"/> and the received-row
/// write to <see cref="IErrorReportService.AcceptInboundAsync"/> (Core).
/// <para>
/// **The acceptance contract (ESC·5 / ESC·7).** (1) Parse the
/// <c>Bearer</c> plaintext (ESC·6). (2) Read the M32 8-field JSON payload
/// (the origin's <see cref="InboundEscalationPayload"/>). (3) A blank
/// <c>Description</c> is a **400** (a bad request — never a row, the M32·4
/// "never a 500" pin, applied to the M2M surface). (4) Validate the token —
/// a missing / invalid / revoked token is a **401**, **no row, no audit row**
/// (ESC·5 / ESC·9). (5) Build the <see cref="InboundReport"/> (the
/// <c>TokenId</c> is the validated token's id, the <c>FromInstance</c> is its
/// <c>Label</c>, the rest are the origin's fields — the U04 Core record,
/// called not re-shaped, ESC·1) and call
/// <see cref="IErrorReportService.AcceptInboundAsync"/> — the idempotent
/// write lane (ESC·7: a re-delivery of the same
/// <c>(TokenId, SourceReportId)</c> returns the **existing** row, no
/// duplicate, no second audit row). (6) Map the result:
/// <c>result.Error != null</c> → **401** (a defensive re-validation — e.g. a
/// token revoked in the race window, ESC·5); <c>result.Created == true</c> →
/// **200** <c>{ id, received: true }</c> (a new <c>Origin = "escalated"</c>
/// row, ESC·5); <c>result.Created == false</c> (idempotent) → **200**
/// <c>{ id, received: false }</c> (ESC·7).
/// </para>
/// </summary>
[Route("escalations")]
public sealed class EscalationInboundController(
    // The Core write seam — the AcceptInboundAsync idempotent inbound lane
    // (U04's 5th method on IErrorReportService, ESC·1 / ESC·5 / ESC·7).
    IErrorReportService errorReports,
    // The Core token seam — ValidateAsync is the only endpoint-level token
    // read (U03's IEscalationTokenService, ESC·3 / ESC·9). Called, never
    // re-shaped (ESC·1).
    IEscalationTokenService tokenService) : Controller
{
    /// <summary>
    /// <c>POST /escalations/inbound</c> — the token-gated acceptance of an
    /// origin platform's escalated issue (the ESC·5 / ESC·6 / ESC·7 contract in
    /// the type summary). Public (no session gate — the token is the
    /// credential, the ESC·5 pin); the only credential is the
    /// <c>Authorization: Bearer &lt;token&gt;</c> header. A successful
    /// acceptance writes an <c>Origin = "escalated"</c>
    /// <see cref="ErrorReport"/> row + one <c>AccessAudit</c> row
    /// (<c>Via = Escalation</c>) in Core (the ADR 0006 C3 single-write-lane,
    /// the M2M standing — the origin platform, not a receiving admin, is the
    /// actor, ESC·10); a re-delivery under the same token + report id is
    /// idempotent (ESC·7).
    /// </summary>
    [HttpPost("inbound")]
    public async Task<IActionResult> Inbound([FromBody] InboundEscalationPayload? body)
    {
        // 1. Parse the Bearer credential (ESC·6 — the token is the plaintext the
        //    receiving admin issued; the forwarder presents it as the outgoing
        //    secret). Absent / malformed → 401 (no row, ESC·5).
        var plaintext = ParseBearerToken();
        if (string.IsNullOrWhiteSpace(plaintext))
        {
            return Unauthorized(new { error = "invalid_token", message = Msg("escalation.inbound.invalid_token") });
        }

        // 2. Validate the description (the M32·4 "never a 500" pin, applied to
        //    the M2M surface — a blank description is a 400 bad request, never
        //    a row). The body is also null-safe here (a missing JSON body reads
        //    as a blank description).
        if (body is null || string.IsNullOrWhiteSpace(body.Description))
        {
            return BadRequest(new { error = "description_required" });
        }

        // 3. Validate the token (Core — the constant-time SHA-256 hash compare
        //    + the LastUsedAt stamp, U04's reconciliation note: the endpoint
        //    resolves the Bearer plaintext → TokenId via ValidateAsync *before*
        //    calling AcceptInboundAsync, keeping Core HTTP-free, ESC·6). A
        //    missing / invalid / revoked token → 401, no row (ESC·5 / ESC·9).
        var token = await tokenService.ValidateAsync(plaintext);
        if (token is null)
        {
            return Unauthorized(new { error = "invalid_token", message = Msg("escalation.inbound.invalid_token") });
        }

        // 4. Build the InboundReport (the U04 Core record — called, not re-
        //    shaped, ESC·1): the TokenId is the validated token's id (the ESC·7
        //    idempotency key), the FromInstance is its Label (the ESC·8 display
        //    — which origin platform this came from), the rest are the
        //    origin's fields from the M32 8-field payload.
        var report = new InboundReport(
            TokenId:          token.Id,
            SourceReportId:   body.Id ?? string.Empty,
            FromInstance:     token.Label,
            Description:      body.Description,
            ContactEmail:     string.IsNullOrWhiteSpace(body.ContactEmail) ? null : body.ContactEmail,
            OriginSubjectId:  body.SubjectId ?? string.Empty,
            RequestId:        body.RequestId ?? string.Empty,
            ExceptionType:    string.IsNullOrWhiteSpace(body.ExceptionType) ? null : body.ExceptionType,
            OriginCreated:    body.Created == default ? DateTimeOffset.UtcNow : body.Created);

        // 5. Accept (Core — the AcceptInboundAsync idempotent write lane,
        //    ESC·5 / ESC·7): re-validates the token by id (a race-window
        //    revocation → Error), does the idempotent lookup on
        //    (TokenId, SourceReportId), and on a new delivery stores the
        //    Origin = "escalated" row + one AccessAudit row (Via = Escalation)
        //    in one write session.
        var result = await errorReports.AcceptInboundAsync(report);

        // 6. Map the result (ESC·5 / ESC·7). Error non-null → 401 (a defensive
        //    re-validation — no row, no audit row); Created → 200 received:true;
        //    idempotent → 200 received:false (the existing row, no duplicate).
        if (result.Error is not null)
        {
            return Unauthorized(new { error = "invalid_token", message = Msg("escalation.inbound.invalid_token") });
        }

        return Ok(new
        {
            id       = result.Row.Id,
            received = result.Created,
            message  = Msg("escalation.inbound.received")
        });
    }

    /// <summary>
    /// Parse the <c>Authorization: Bearer &lt;token&gt;</c> header into the
    /// plaintext token (the ESC·6 pin — the token is the credential in both
    /// directions; the forwarder presents it as <c>Bearer</c>, the endpoint
    /// reads it). Returns <c>null</c> when the header is absent, blank, or not
    /// in the <c>Bearer</c> scheme. Web-layer only (Core stays HTTP-free,
    /// ESC·6).
    /// </summary>
    private string? ParseBearerToken()
    {
        var header = (Request?.Headers["Authorization"].FirstOrDefault())?.Trim();
        if (string.IsNullOrWhiteSpace(header))
            return null;
        const string BearerPrefix = "Bearer ";
        if (header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var token = header[BearerPrefix.Length..].Trim();
            return token.Length == 0 ? null : token;
        }
        return null;
    }

    /// <summary>
    /// Resolve an <c>escalation.inbound.*</c> kw-l key to the en source text
    /// (the ADR 0015 D1 <c>kw-l</c> provider-floor discipline — the
    /// <see cref="KnownTranslationKeys.EnValues"/> source text is the floor, so
    /// a M2M caller never sees a raw key before U07 authors the values; at U06
    /// the value is not yet registered, so the key itself is the floor). This
    /// is a machine-to-machine JSON message (not a browser flash), so there is
    /// no effective-language resolution — the en source text is the correct
    /// floor for the origin platform's log. Consumes the §2.3 key names (the
    /// U06 "inbound flashes" — the values are U07's to author, ESC·11).
    /// </summary>
    private static string Msg(string key) =>
        KnownTranslationKeys.EnValues.GetValueOrDefault(key) ?? key;
}

/// <summary>
/// The M2M inbound payload — the **closed M32 8-field shape**
/// (<c>{ id, subjectId, description, contactEmail, requestId, exceptionType,
/// origin, created }</c>, the M32·1 / M32·5 forwarder payload) that the origin
/// platform's <see cref="Services.EscalationForwarder"/> POSTs to
/// <c>/escalations/inbound</c>. A **Web-layer binding record** (Core stays
/// HTTP-free, ADR 0006-D / ESC·6) — it is **not** a Core type and **not** a
/// re-shape of the U04 <see cref="InboundReport"/> Core record (the inbound
/// record carries the *receiving* platform's identity — the token's id +
/// label — which the raw payload does not; the controller maps this payload
/// into the Core record, ESC·1). ASP.NET Core's default case-insensitive JSON
/// binding maps the camelCase fields (<c>id</c>, <c>subjectId</c>, …) onto the
/// PascalCase properties.
/// </summary>
public sealed record InboundEscalationPayload
{
    /// <summary>The origin's report id (the <see cref="InboundReport.SourceReportId"/> ESC·7 idempotency key).</summary>
    public string? Id { get; init; }

    /// <summary>The origin's SubjectId (<c>""</c> for an anonymous reporter — the <see cref="InboundReport.OriginSubjectId"/>).</summary>
    public string? SubjectId { get; init; }

    /// <summary>The origin's description (required, non-blank — a blank value is a 400).</summary>
    public string? Description { get; init; }

    /// <summary>The origin's contact email (optional).</summary>
    public string? ContactEmail { get; init; }

    /// <summary>The origin's request id (the log-correlation handle).</summary>
    public string? RequestId { get; init; }

    /// <summary>The origin's exception type (null for a general issue).</summary>
    public string? ExceptionType { get; init; }

    /// <summary>The origin's <c>Origin</c> field (bound for payload fidelity; the receiving side sets <c>Origin = "escalated"</c> itself, so this is not mapped onto the Core record).</summary>
    public string? Origin { get; init; }

    /// <summary>The origin's creation instant (the <see cref="InboundReport.OriginCreated"/>).</summary>
    public DateTimeOffset Created { get; init; }
}
