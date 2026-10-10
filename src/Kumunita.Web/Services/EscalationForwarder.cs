using Kumunita.Core.ErrorReports;
using Marten;

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
/// <para>
/// **ESC extension (ADR 0159, ESC·4 / ESC·6).** The endpoint is now resolved
/// from the admin-managed <see cref="EscalationOutboundConfig"/> singleton
/// **first** (the token-authenticated forward — the token is sent as an
/// <c>Authorization: Bearer</c> header, <c>ConfiguredSource == "db"</c>), and
/// the <c>KUMUNITA_ESCALATION_ENDPOINT</c> env var is preserved as the
/// **unauthenticated fallback** (endpoint only, no token,
/// <c>ConfiguredSource == "env"</c>) when that row is absent or disabled
/// (the ESC·4 pin — a deliberate evolution of the M32·6 "env var, never
/// persisted" pin, which was the M32-era operator-config channel). When
/// neither channel is present the forward is the M32 no-op (FACES ESC-6,
/// <c>ConfiguredSource == null</c>). The <see cref="ForwardAsync"/> signature
/// and the 8-field payload are **unchanged** (the M32·1 / ESC·6 pins —
/// additive-only); the only additive member is
/// <see cref="EscalationResult.ConfiguredSource"/> (ESC·4). Core stays
/// HTTP-free (ADR 0006-D) — this is still the Web-layer outbound POST.
/// </para>
/// </summary>
public sealed class EscalationForwarder(
    IConfiguration configuration,
    IErrorReportService errorReports,
    IHttpClientFactory httpClientFactory,
    // ESC·4 (ADR 0159) — the host-registered Marten store (the AddMarten
    // singleton, Program.cs — the same store U05's EscalationConfigController
    // reads/writes the EscalationOutboundConfig singleton with). **Optional**
    // (default null — the M31 optional-ctor-param floor precedent the register
    // cites for this exact ctor, the same idiom as the IEscalationForwarder
    // param on ErrorReportAdminController and the localization /
    // translationProvider seams) so any test-construction site that builds the
    // forwarder without a store keeps compiling: when null, the DB-config
    // read is skipped and the forward degrades to the M32·6 env-var channel
    // (the ESC·4 fallback, ConfiguredSource == "env") or the M32 no-op
    // (FACES ESC-6) — exactly the U08 ESC_4_Forwarder_NoDB_EnvVar_Fallback +
    // ESC_6_Forwarder_NoConfig_NoHTTP shape. It is the single read seam for
    // the admin-managed EscalationOutboundConfig singleton (the design doc
    // §2.1.3 read-seam resolution — U05 recorded that U03 added the
    // IEscalationTokenService token seam but NOT an outbound-config read/write
    // seam, so the Web layer owns the read directly, the AdminStorageController
    // precedent).
    IDocumentStore? store = null) : IEscalationForwarder
{
    /// <summary>
    /// The forward timeout (the <c>SmtpProbe</c>
    /// <c>KUMUNITA_SMTP_HEALTH_TIMEOUT_MS</c> timeout precedent, a fixed
    /// 10 s). Bounded so a hung endpoint cannot wedge the Escalate action.
    /// </summary>
    private static readonly TimeSpan ForwardTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The fixed singleton id for the <see cref="EscalationOutboundConfig"/>
    /// row (the ESC·4 pin — one row per instance; the same constant U05's
    /// <c>EscalationConfigController</c> writes it under). Read here to decide
    /// the forward channel (the design doc §2.1.3 read-seam resolution — the
    /// Web layer owns the singleton read directly, the AdminStorageController
    /// precedent; U03 added the token seam but not an outbound-config seam).
    /// </summary>
    private const string OutboundConfigSingletonId = "outbound-config";

    /// <summary>
    /// Resolve the outbound forward channel (ESC·4 / ESC·6). Reads the
    /// admin-managed <see cref="EscalationOutboundConfig"/> singleton **first**
    /// (the ESC·4 primary channel — the token-authenticated forward; the
    /// plaintext <c>Token</c> is the *outgoing* secret the origin admin was
    /// handed, presented as a <c>Bearer</c> header, ESC·6). When that row is
    /// **absent or disabled** it falls back to the M32·6 env var
    /// <c>KUMUNITA:ESCALATION_ENDPOINT</c> (the unauthenticated M32-style
    /// forward, endpoint only, **no** token — the ESC·4 fallback). Returns
    /// <c>null</c> when **neither** channel is configured (the M32 no-op,
    /// FACES ESC-6). The direct <see cref="IDocumentStore"/> read mirrors U05's
    /// <c>EscalationConfigController</c> singleton idiom (the design doc §2.1.3
    /// read-seam resolution — U03 added the token seam but not an outbound-
    /// config seam, so the Web layer owns the read).
    /// <para>
    /// The tuple is <c>(Endpoint, Token?, Source)</c>: <c>Source</c> is
    /// <c>"db"</c> (the admin-managed channel) or <c>"env"</c> (the fallback);
    /// <c>Token</c> is non-null only for the <c>"db"</c> channel (the
    /// <c>"env"</c> fallback is endpoint-only, no token, the ESC·4 pin).
    /// </para>
    /// </summary>
    private async Task<(string Endpoint, string? Token, string Source)?> ResolveOutboundTargetAsync(CancellationToken ct)
    {
        // 1) The admin-managed DB config (the ESC·4 primary — present + enabled
        //    + a non-blank endpoint; U05's save validation requires a valid
        //    endpoint when Enabled, so a blank one is only reachable by a row
        //    written directly to the store). Skipped when the store seam is
        //    absent (the test-construction floor — the forward degrades to the
        //    M32·6 env-var fallback below, the ESC·4 pin).
        EscalationOutboundConfig? config = null;
        if (store is not null)
        {
            using (var session = store.QuerySession())
            {
                config = await session.LoadAsync<EscalationOutboundConfig>(OutboundConfigSingletonId, ct);
            }
        }
        if (config is not null && config.Enabled && !string.IsNullOrWhiteSpace(config.Endpoint))
        {
            // The token-authenticated forward. The Token is the outgoing secret
            // (non-blank for a config the admin actually handed over — the Bearer
            // header carries it, ESC·6); a blank token still selects the "db"
            // channel (the endpoint came from the admin config) but presents no
            // Bearer credential.
            return (config.Endpoint,
                    string.IsNullOrWhiteSpace(config.Token) ? null : config.Token,
                    "db");
        }

        // 2) The M32·6 env-var fallback (the unauthenticated M32-style forward —
        //    endpoint only, NO token, the ESC·4 pin).
        var envEndpoint = configuration["KUMUNITA:ESCALATION_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(envEndpoint))
        {
            return (envEndpoint, null, "env");
        }

        // 3) Neither channel is present → the M32 no-op (FACES ESC-6).
        return null;
    }

    public async Task<EscalationResult> ForwardAsync(string reportId, CancellationToken ct = default)
    {
        // 1. Resolve the outbound channel (ESC·4 / ESC·6): the admin-managed DB
        //    config (the token-authenticated forward) first, the M32·6 env var
        //    (the unauthenticated fallback) second, a no-op when neither. The
        //    ForwardAsync signature is **unchanged** (the ESC·6 pin — no new
        //    parameter; the forwarder reads its own config).
        var target = await ResolveOutboundTargetAsync(ct).ConfigureAwait(false);
        if (target is null)
        {
            // Absent → a no-op forward (FACES M32-8 / ESC-6). The caller (the
            // Escalate action) surfaces the "not configured" message; no state
            // change. ConfiguredSource is null (the M32 no-op, ESC·4).
            return new EscalationResult(Configured: false, Success: false, StatusCode: null, Error: null, ConfiguredSource: null);
        }
        var (endpoint, token, source) = target.Value;

        // 2. Load the report (a read — no audit, the M31·4 pin). The service's
        //    only read seam is ListAsync; list and filter to reportId.
        var reports = await errorReports.ListAsync(100, ct).ConfigureAwait(false);
        var report = reports.FirstOrDefault(r => r.Id == reportId);
        if (report is null)
        {
            return new EscalationResult(Configured: true, Success: false, StatusCode: null, Error: "report not found", ConfiguredSource: source);
        }

        // 3. POST the closed payload { id, subjectId, description, contactEmail,
        //    requestId, exceptionType, origin, created } — the M32 8-field
        //    shape is **unchanged** (the M32·1 pin — additive-only), NOT the
        //    resolution fields, NOT the endpoint (the §2.1 pin).
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
            // ESC·6 — when the channel is the admin-managed DB config and the
            // origin admin recorded a token, present it as a Bearer credential
            // (the receiving inbound endpoint, U06's, validates it — the
            // token-gated acceptance, ESC·5). The "env" fallback is
            // endpoint-only (no token, the ESC·4 pin).
            if (token is not null)
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            var response = await client.PostAsJsonAsync(endpoint, payload, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? new EscalationResult(Configured: true, Success: true, StatusCode: (int)response.StatusCode, Error: null, ConfiguredSource: source)
                : new EscalationResult(Configured: true, Success: false, StatusCode: (int)response.StatusCode, Error: response.ReasonPhrase, ConfiguredSource: source);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // A forward timeout (not a caller cancellation) is a failed forward
            // (M32·7 — the report is NOT stamped resolved; FACES M32-9).
            return new EscalationResult(Configured: true, Success: false, StatusCode: null, Error: ex.Message, ConfiguredSource: source);
        }
        catch (HttpRequestException ex)
        {
            // A transport error is a failed forward (M32·7; FACES M32-9).
            return new EscalationResult(Configured: true, Success: false, StatusCode: null, Error: ex.Message, ConfiguredSource: source);
        }
    }
}
