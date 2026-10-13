namespace Kumunita.Core.Usage;

/// <summary>
/// The recurring message shape for the M33 storage-metrics-capture job
/// (ADR 0156, M33·3 — the <see cref="UsagePurgeTick"/> shape verbatim: one
/// class, one baked-in schedule (1 day), re-yielded by the
/// <see cref="Kumunita.Web.SideEffects.StorageMetricsCaptureHandler"/> after
/// each run. The <see cref="Wolverine.TimeoutMessage"/> 1-day delay is baked
/// into the type, so every re-publish carries the same cadence — no
/// per-callsite <c>DelayedFor</c> needed. Postgres-backed durability (a
/// Coolify redeploy mid-day does not silently drop a pending run, the
/// <see cref="UsagePurgeTick"/> precedent).
/// </summary>
public sealed record StorageMetricsCaptureTick()
    : Wolverine.TimeoutMessage(TimeSpan.FromDays(1));
