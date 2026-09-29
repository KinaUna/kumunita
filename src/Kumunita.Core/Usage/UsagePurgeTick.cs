namespace Kumunita.Core.Usage;

/// <summary>
/// The recurring message shape for the M13 usage-purge job (ADR 0114 D5, the
/// <see cref="Kumunita.Core.Authorization.AuditPurgeTick"/> shape verbatim).
/// One class, one baked-in schedule (1 day), re-yielded by the
/// <see cref="Kumunita.Web.SideEffects.UsagePurgeHandler"/> after each run.
/// The <see cref="Wolverine.TimeoutMessage"/> 1-day delay is baked into the
/// type, so every re-publish carries the same schedule — no per-callsite
/// <c>DelayedFor</c> needed. Postgres-backed durability (the
/// <c>AuditPurgeHandler</c> "a Coolify redeploy mid-day does not silently drop
/// a pending run" precedent).
/// </summary>
public sealed record UsagePurgeTick() : Wolverine.TimeoutMessage(TimeSpan.FromDays(1));
