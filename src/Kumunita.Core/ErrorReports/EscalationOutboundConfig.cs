namespace Kumunita.Core.ErrorReports;

/// <summary>
/// The origin-side escalation outbound config (ADR 0159, ESC·4). A **singleton**
/// row (one per instance, fixed <c>Id</c>) the origin GlobalAdmin manages on
/// the <c>/admin/escalation</c> surface (ESC·12): the receiving platform's
/// inbound <c>Endpoint</c>, the plaintext access <c>Token</c> the origin admin
/// was handed by the receiving admin out-of-band, and an <c>Enabled</c> switch.
/// The field set below is the **6-member ESC·4 ceiling** (ADR 0159 D2) — no
/// field outside this set may appear in the doc.
/// <para>
/// The <c>Token</c> here is the **plaintext** the origin admin was handed —
/// stored because it is the *outgoing* secret the origin admin chose to keep,
/// **not** a secret of the *receiving* platform (contrast
/// <see cref="EscalationToken.TokenHash"/>, which is hash-only-at-rest,
/// ESC·3). The M32·6 env var <c>KUMUNITA_ESCALATION_ENDPOINT</c> remains the
/// **unauthenticated operator-override / fallback** (ESC·4): when this row is
/// absent or disabled the Web-layer <c>EscalationForwarder</c> (U06) falls
/// back to the env-var endpoint (endpoint only, **no** token); when this row
/// is present + enabled it is primary and the token is sent as a <c>Bearer</c>
/// header (ESC·6).
/// </para>
/// </summary>
public sealed class EscalationOutboundConfig
{
    public string Id { get; set; } = default!;       // conventional — a fixed singleton id (the Web layer sets it, e.g. "outbound-config")
    public string Endpoint { get; set; } = default!; // the receiving platform's inbound URL
    public string Token { get; set; } = default!;    // the plaintext token the origin admin was handed (the OUTGOING secret)
    public bool Enabled { get; set; }                // default false
    public DateTimeOffset Updated { get; set; }
    public string UpdatedBy { get; set; } = default!; // the admin's subject
}
