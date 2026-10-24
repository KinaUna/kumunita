namespace Kumunita.Core.Notifications;

/// <summary>
/// The recurring message shape for the F18 (MAIL-06) in-repo bounce/complaint
/// ingest-rate alarm (the T4 code leg, pin
/// <c>docs/design/prod-hardening-f18-mailalarms.md</c> §6; the
/// <see cref="Kumunita.Core.Media.MediaLifecycleTick"/> /
/// <see cref="Kumunita.Core.Usage.UsagePurgeTick"/> shape verbatim). One class,
/// one baked-in schedule (<b>1 hour</b> — the pin §2 T4 cadence; the F15 1-day
/// ticks serve low-frequency retention sweeps, F18's cross-check is hourly),
/// re-yielded by the <see cref="Kumunita.Web.SideEffects.MailAlarmsHandler"/>
/// after each run. The <see cref="Wolverine.TimeoutMessage"/> 1-hour delay is
/// baked into the type, so every re-publish carries the same schedule — no
/// per-callsite <c>DelayedFor</c> needed. Postgres-backed durability (the
/// <c>AuditPurgeHandler</c> "a Coolify redeploy mid-hour does not silently
/// drop a pending run" precedent).
/// </summary>
public sealed record MailAlarmsTick() : Wolverine.TimeoutMessage(TimeSpan.FromHours(1));
