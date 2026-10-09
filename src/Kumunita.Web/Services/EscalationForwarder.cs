using Kumunita.Core.ErrorReports;

namespace Kumunita.Web.Services;

/// <summary>
/// The M32 escalation forwarding impl (ADR 0155, M32·5 / M32·6 / M32·7) — the
/// Web-layer HTTP service, the **first outbound HTTP** in the codebase
/// (M32·5; Core stays HTTP-free, ADR 0006-D).
/// <para>
/// Holds no per-request state → registered as a **singleton** (the
/// <see cref="IHttpClientFactory"/> is a singleton; the
/// <see cref="Kumunita.Core.ErrorReports.IErrorReportService"/> read seam is
/// resolved per call via the factory).
/// </para>
/// <para>
/// **The endpoint is read from configuration, never persisted** (M32·6):
/// <c>configuration["KUMUNITA:ESCALATION_ENDPOINT"]</c> is the
/// <c>IConfiguration</c> mapping of the <c>KUMUNITA_ESCALATION_ENDPOINT</c>
/// env var (the <c>SmtpProbe</c> <c>KUMUNITA_SMTP_HEALTH_TIMEOUT_MS</c>
/// env-var read precedent for the "operator config from the environment"
/// shape). When absent (null/blank) the forward is a no-op
/// (<c>Configured == false</c>, FACES M32-8).
/// </para>
/// </summary>
public sealed class EscalationForwarder(
    IConfiguration configuration,
    IErrorReportService errorReports,
    IHttpClientFactory httpClientFactory) : IEscalationForwarder
{
    /// <summary>
    /// The forward timeout (the <c>SmtpProbe</c>
    /// <c>KUMUNITA_SMTP_HEALTH_TIMEOUT_MS</c> timeout precedent, a fixed
    /// 10 s). Bounded so a hung endpoint cannot wedge the Escalate action.
    /// </summary>
    private static readonly TimeSpan ForwardTimeout = TimeSpan.FromSeconds(10);

    public async Task<EscalationResult> ForwardAsync(string reportId, CancellationToken ct = default)
    {
        // 1. Read the endpoint (M32·6 — configuration, never persisted).
        var endpoint = configuration["KUMUNITA:ESCALATION_ENDPOINT"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            // Absent → a no-op forward (FACES M32-8). The caller (the Escalate
            // action) surfaces the "not configured" message; no state change.
            return new EscalationResult(Configured: false, Success: false, StatusCode: null, Error: null);
        }

        // 2. Load the report (a read — no audit, the M31·4 pin). The service's
        //    only read seam is ListAsync; list and filter to reportId.
        var reports = await errorReports.ListAsync(100, ct).ConfigureAwait(false);
        var report = reports.FirstOrDefault(r => r.Id == reportId);
        if (report is null)
        {
            return new EscalationResult(Configured: true, Success: false, StatusCode: null, Error: "report not found");
        }

        // 3. POST the closed payload { id, subjectId, description, contactEmail,
        //    requestId, exceptionType, origin, created } — NOT the resolution
        //    fields, NOT the endpoint (the §2.1 pin).
        var payload = new
        {
            id = report.Id,
            subjectId = report.SubjectId,
            description = report.Description,
            contactEmail = report.ContactEmail,
            requestId = report.RequestId,
            exceptionType = report.ExceptionType,
            origin = report.Origin,
            created = report.Created
        };

        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = ForwardTimeout;
            var response = await client.PostAsJsonAsync(endpoint, payload, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? new EscalationResult(Configured: true, Success: true, StatusCode: (int)response.StatusCode, Error: null)
                : new EscalationResult(Configured: true, Success: false, StatusCode: (int)response.StatusCode, Error: response.ReasonPhrase);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // A forward timeout (not a caller cancellation) is a failed forward
            // (M32·7 — the report is NOT stamped resolved; FACES M32-9).
            return new EscalationResult(Configured: true, Success: false, StatusCode: null, Error: ex.Message);
        }
        catch (HttpRequestException ex)
        {
            // A transport error is a failed forward (M32·7; FACES M32-9).
            return new EscalationResult(Configured: true, Success: false, StatusCode: null, Error: ex.Message);
        }
    }
}
