namespace Kumunita.Core.ErrorReports;

/// <summary>
/// The read + single-write-lane for the <see cref="EscalationOutboundConfig"/>
/// singleton (ADR 0159, ESC·4 / ESC·12). The <see cref="EscalationTokenService"/>
/// sibling shape (the same <c>ErrorReports</c> bounded context, ADR 0006-D):
/// reads open their own <c>QuerySession</c>; the audited write opens one write
/// <c>IDocumentSession</c> and commits the doc + its single <c>AccessAudit</c>
/// row together (invariant C3, strong consistency — the M32
/// <c>MarkResolvedAsync</c> single-write-lane precedent).
/// <para>
/// IMPROVE-CODE C09 — the <see cref="Kumunita.Web.Controllers.EscalationConfigController"/>'s
/// raw Marten read/write (the design doc §2.1.3 read-seam resolution that U05
/// left in the Web layer, "no new Core file") moved behind this interface,
/// ADR 0006 §D "route + authz + shape": the controller validates the bound
/// form (route + shape) and delegates the read + the single-write-lane to this
/// seam. The served HTML is unchanged.
/// </para>
/// <para>
/// <b>ESC·12 — no new <c>AccessAction</c>, <c>Decide()</c> branch, or
/// <c>IAuthorizationService</c> surface</b> (the admin gate is the standard
/// <c>[Authorize(Roles = GlobalAdmin)]</c> role check, not a decision lane).
/// Core stays HTTP-free (ADR 0006-D / ESC·6): the Web layer is the only place
/// the subject / request context are produced.
/// </para>
/// </summary>
public interface IEscalationOutboundConfigService
{
    /// <summary>
    /// The read lane (ESC·4): load the <see cref="EscalationOutboundConfig"/>
    /// singleton (a read — no audit row, the M31·4 "read is not an access
    /// decision" precedent). Returns <c>null</c> when the row is absent (the
    /// <see cref="Kumunita.Web.Controllers.AdminStorageController"/>
    /// create-if-missing sentinel shape — the Web layer renders an empty form).
    /// </summary>
    Task<EscalationOutboundConfig?> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// The single-write-lane (ESC·4 / the ADR 0006 C3 shape): load-or-create
    /// the <see cref="EscalationOutboundConfig"/> singleton (a missing row is
    /// written as a fresh doc — the AdminStorageController create-if-missing
    /// sentinel shape), set <paramref name="endpoint"/> /
    /// <paramref name="token"/> / <paramref name="enabled"/> + the
    /// <c>Updated</c> / <c>UpdatedBy</c> stamps, and commit the doc + exactly
    /// one <c>AccessAudit</c> row (<c>Via = Admin</c>, action
    /// <c>escalation.outbound.save</c>, <c>TargetKind</c>
    /// <c>escalation-outbound-config</c>) in **one** write session (the
    /// <see cref="EscalationTokenService"/> <c>GenerateAsync</c> /
    /// <c>RevokeAsync</c> idiom — the M32 <c>MarkResolvedAsync</c> precedent).
    /// Returns the stored row.
    /// </summary>
    Task<EscalationOutboundConfig> SaveAsync(
        string endpoint,
        string token,
        bool enabled,
        string actorId,
        CancellationToken ct = default);
}
