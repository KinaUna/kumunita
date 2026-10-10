using Kumunita.Core.ErrorReports;

namespace Kumunita.Web.Models;

/// <summary>
/// The bound form model for the <c>/admin/escalation</c> outbound-config
/// surface (ADR 0159, ESC·4 / ESC·12). The origin GlobalAdmin's own outgoing
/// settings: the receiving platform's inbound <see cref="Endpoint"/>, the
/// plaintext access <see cref="Token"/> the origin admin was handed by the
/// receiving admin out-of-band, and an <see cref="Enabled"/> switch.
/// <para>
/// <b>ESC·4 — the <see cref="Token"/> is the *outgoing* secret</b> (stored on
/// the <see cref="EscalationOutboundConfig"/> doc because it is a secret the
/// origin admin chose to keep, **not** a secret of the receiving platform —
/// contrast the receiving-side <c>EscalationToken.TokenHash</c>, which is
/// hash-only-at-rest, ESC·3). When <see cref="Enabled"/> is
/// <c>false</c> the M32·6 env var <c>KUMUNITA_ESCALATION_ENDPOINT</c> remains
/// the unauthenticated operator-override / fallback (the
/// <c>EscalationForwarder</c>, U06).
/// </para>
/// <para>
/// <b>Validation</b> is explicit in the controller (the
/// <see cref="Controllers.AdminStorageController.Save"/> precedent): when
/// <see cref="Enabled"/> is <c>true</c> the <see cref="Endpoint"/> is required
/// + a valid <c>http://</c> or <c>https://</c> URL; when <c>false</c> both are
/// optional. The <see cref="Saved"/> flag (controller-set, never bound) tells
/// the view to render the <c>escalation.outbound.saved</c> confirmation (the
/// U07 kw-l key set, ESC·11).
/// </para>
/// </summary>
public sealed class EscalationConfigFormModel
{
    /// <summary>
    /// The receiving platform's inbound URL (the
    /// <c>escalation.outbound.endpoint.label</c> field). Required + a valid
    /// URL when <see cref="Enabled"/> is <c>true</c>; optional when disabled.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// The plaintext access token the origin admin was handed (the
    /// <c>escalation.outbound.token.label</c> field, rendered as a password
    /// input — the ESC·4 outgoing secret). Optional when <see cref="Enabled"/>
    /// is <c>false</c>.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// The escalation on/off switch (the
    /// <c>escalation.outbound.enabled.label</c> field). Default <c>false</c>.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// True after a successful save (the controller sets it on re-render so
    /// the view renders the <c>escalation.outbound.saved</c> confirmation).
    /// **Never** bound from the form.
    /// </summary>
    public bool Saved { get; set; }
}
