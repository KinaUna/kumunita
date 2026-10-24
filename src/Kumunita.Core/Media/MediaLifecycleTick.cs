namespace Kumunita.Core.Media;

/// <summary>
/// The recurring message shape for the F15 (UPL-04) byte-store lifecycle/
/// retention sweep (the <see cref="Kumunita.Core.Usage.UsagePurgeTick"/> /
/// <see cref="Kumunita.Core.Usage.StorageMetricsCaptureTick"/> shape verbatim).
/// One class, one baked-in schedule (1 day), re-yielded by the
/// <see cref="Kumunita.Web.SideEffects.MediaLifecycleHandler"/> after each run.
/// The <see cref="Wolverine.TimeoutMessage"/> 1-day delay is baked into the
/// type, so every re-publish carries the same schedule — no per-callsite
/// <c>DelayedFor</c> needed. Postgres-backed durability (the
/// <c>AuditPurgeHandler</c> "a Coolify redeploy mid-day does not silently drop
/// a pending run" precedent).
/// </summary>
public sealed record MediaLifecycleTick() : Wolverine.TimeoutMessage(TimeSpan.FromDays(1));
