namespace Kumunita.Web.Services;

/// <summary>
/// The M32 escalation forwarding lane (ADR 0155, M32·5 / M32·6 / M32·7).
/// Web-layer HTTP — Core stays HTTP-free (ADR 0006-D; unit-series rule 7
/// forbids an outbound <c>HttpClient</c> call in <c>Kumunita.Core</c>). This is
/// the **first outbound HTTP** in the codebase (the M32·5 pin).
/// <para>
/// Reads the endpoint from the <c>KUMUNITA_ESCALATION_ENDPOINT</c> env var
/// (M32·6 — **never** a DB column, never a per-row field, never a Marten
/// config row; the fork/multi-instance operator's environment config), loads
/// the report via <see cref="Kumunita.Core.ErrorReports.IErrorReportService"/>
/// (a read — no audit, the M31·4 pin), and POSTs the JSON payload to the
/// endpoint.
/// </para>
/// <para>
/// **Caller's contract** (the <c>ErrorReportAdminController.Escalate</c>
/// action): **only** on <c>Success == true</c> does the caller call
/// <c>IErrorReportService.MarkResolvedAsync</c> (the status transition + the
/// <c>errorreport.escalate</c> audit row are Core's; the HTTP is Web's, M32·7).
/// <c>Success == false</c> or <c>Configured == false</c> → **no**
/// <c>MarkResolvedAsync</c> call — a failed or unconfigured forward is a no-op
/// (M32·7 / M32·6; FACES M32-8 / M32-9).
/// </para>
/// </summary>
public interface IEscalationForwarder
{
    /// <summary>
    /// Forward the report named by <paramref name="reportId"/> to the
    /// configured endpoint. Returns <c>Configured == false</c> when the
    /// <c>KUMUNITA_ESCALATION_ENDPOINT</c> env var is absent (the M32·6 pin —
    /// the Escalate action is then a no-op, FACES M32-8). <c>Success == true</c>
    /// only on a 2xx response (the M32·7 pin). A non-2xx response or a
    /// transport error (<c>HttpRequestException</c> / <c>TaskCanceledException</c>
    /// timeout) returns <c>Success == false</c> with the <c>Error</c> message —
    /// the report is NOT stamped resolved in that case (M32·7).
    /// </summary>
    Task<EscalationResult> ForwardAsync(string reportId, CancellationToken ct = default);
}

/// <summary>
/// The outcome of one <see cref="IEscalationForwarder.ForwardAsync"/> call.
/// <c>Configured</c> is false when <c>KUMUNITA_ESCALATION_ENDPOINT</c> is
/// absent; <c>Success</c> is true only on a 2xx response; <c>StatusCode</c> is
/// null when not Configured (or on a transport error); <c>Error</c> is a short
/// transport-error message, null on success.
/// <para>
/// <b>ESC·4 additive (ADR 0159, ESC·4).</b> The M32 four members above are
/// **unchanged** (the M32·1 / M32·3 additive-only pin, ESC·6 — the
/// <see cref="ForwardAsync"/> signature is untouched). ESC adds exactly one
/// additive member, <see cref="ConfiguredSource"/> (the M32·3 additive
/// precedent), so the admin flash can name the channel the endpoint came
/// from: <c>"db"</c> when the endpoint was read from the
/// <see cref="Kumunita.Core.ErrorReports.EscalationOutboundConfig"/>
/// singleton (the admin-managed, token-authenticated channel — the token is
/// sent as a <c>Bearer</c> header, ESC·6), <c>"env"</c> when it fell back to
/// the <c>KUMUNITA_ESCALATION_ENDPOINT</c> env var (the unauthenticated M32-
/// style forward, endpoint only, no token, the ESC·4 fallback), and
/// <c>null</c> when neither is present (the M32 no-op, FACES ESC-6).
/// </para>
/// </summary>
public sealed record EscalationResult(
    bool Configured,
    bool Success,
    int? StatusCode,
    string? Error,
    string? ConfiguredSource = null);
