namespace Kumunita.Core.Privacy;

/// <summary>
/// The recurring message shape for the IMPROVE-SEC D6 / E5 retention sweep
/// (the <see cref="Kumunita.Core.Usage.UsagePurgeTick"/> /
/// <see cref="Kumunita.Core.Media.MediaLifecycleTick"/> shape verbatim). One
/// class, one baked-in schedule (1 day), re-yielded by the
/// <see cref="Kumunita.Web.SideEffects.RetentionTickHandler"/> (IS34b) after
/// each run.
/// <para>
/// The <see cref="Wolverine.TimeoutMessage"/> 1-day delay is baked into the
/// type, so every re-publish carries the same schedule — no per-callsite
/// <c>DelayedFor</c> needed. Postgres-backed durability (the
/// <c>AuditPurgeHandler</c> "a Coolify redeploy mid-day does not silently drop
/// a pending run" precedent).
/// </para>
/// </summary>
public sealed record RetentionTick() : Wolverine.TimeoutMessage(TimeSpan.FromDays(1));
