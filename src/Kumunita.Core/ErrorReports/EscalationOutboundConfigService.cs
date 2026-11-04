using Kumunita.Core.Authorization;
using Marten;

namespace Kumunita.Core.ErrorReports;

/// <summary>
/// The <see cref="IEscalationOutboundConfig"/> read + single-write-lane
/// implementation (ADR 0159, ESC·4 / ESC·12). The
/// <see cref="EscalationTokenService"/> sibling shape (the same
/// <c>ErrorReports</c> bounded context, ADR 0006-D): reads open their own
/// <c>QuerySession</c>; the audited write opens one write
/// <c>IDocumentSession</c> and commits the doc + its single
/// <c>AccessAudit</c> row together (invariant C3, strong consistency — the
/// M32 <c>MarkResolvedAsync</c> single-write-lane precedent).
/// <para>
/// IMPROVE-CODE C09 — the <see cref="Kumunita.Web.Controllers.EscalationConfigController"/>'s
/// raw Marten read/write moved behind <see cref="IEscalationOutboundConfigService"/>
/// (ADR 0006 §D "route + authz + shape"); the served HTML is unchanged.
/// </para>
/// </summary>
public sealed class EscalationOutboundConfigService : IEscalationOutboundConfigService
{
    /// <summary>
    /// The fixed singleton id for the <see cref="EscalationOutboundConfig"/>
    /// row (the ESC·4 pin — one row per instance; the U03 doc comment "a
    /// fixed singleton id, e.g. outbound-config" — moved off the controller).
    /// </summary>
    private const string SingletonId = "outbound-config";

    private readonly IDocumentStore _store;

    public EscalationOutboundConfigService(IDocumentStore store) => _store = store;

    /// <inheritdoc />
    public async Task<EscalationOutboundConfig?> GetAsync(CancellationToken ct = default)
    {
        using var session = _store.QuerySession();
        return await session.LoadAsync<EscalationOutboundConfig>(SingletonId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<EscalationOutboundConfig> SaveAsync(
        string endpoint,
        string token,
        bool enabled,
        string actorId,
        CancellationToken ct = default)
    {
        // Load-or-create the singleton (the AdminStorageController create-if-
        // missing sentinel shape — a missing row is written as a fresh doc).
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var existing = await session.LoadAsync<EscalationOutboundConfig>(SingletonId, ct).ConfigureAwait(false);
        var config = existing ?? new EscalationOutboundConfig { Id = SingletonId };

        var now = DateTimeOffset.UtcNow;
        config.Endpoint  = endpoint;
        config.Token     = token;
        config.Enabled   = enabled;
        config.Updated   = now;
        config.UpdatedBy = actorId;

        // The single-write-lane (ESC·4 / the ADR 0006 C3 shape): the
        // EscalationOutboundConfig doc + exactly one AccessAudit row commit
        // in one write session (the EscalationTokenService.GenerateAsync /
        // RevokeAsync idiom — the M32 MarkResolvedAsync precedent).
        session.Store(config);
        session.Store(new AccessAudit
        {
            Id                   = Guid.NewGuid().ToString("N"),
            At                   = now,
            ActorId              = actorId,
            EffectivePrincipalId = actorId,
            Action               = "escalation.outbound.save",
            TargetKind           = "escalation-outbound-config",
            TargetId             = SingletonId,
            Via                  = AccessVia.Admin,
            Outcome              = AccessOutcome.Allow
        });
        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        return config;
    }
}
