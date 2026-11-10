namespace Kumunita.Core.Identity;

/// <summary>
/// ADR 0301 (IMPROVE-UX R12) — the recurring message shape for the
/// scheduled-account-deletion due-job: the tick that finds the
/// <see cref="DeletionRequest"/> rows past their <c>DueAt</c> and runs each
/// through the EXISTING <see cref="IIdentityService.DeleteAccountAsync"/> lane
/// (the self-serve branch — ADR 0301 D4 keeps that lane's contract unchanged;
/// R12 does not re-implement deletion).
/// <para>
/// One class, one baked-in schedule — the
/// <see cref="Kumunita.Core.Notifications.MailAlarmsTick"/> (1-hour) /
/// <see cref="Kumunita.Core.Usage.UsagePurgeTick"/> (1-day) shape verbatim, with
/// a <b>1-hour</b> cadence (a fine default for a 14-day grace window:
/// <see cref="IDeletionScheduler.ListDueAsync"/> is a cheap query and a no-op
/// when nothing is due, so an hourly check is the lowest-cost cadence that keeps
/// execution latency under a day). The <see cref="Wolverine.TimeoutMessage"/>
/// delay is baked into the type, so every re-publish carries the same schedule —
/// no per-callsite <c>DelayedFor</c> needed. The cadence is a knob, not a
/// decision the ADR freezes (the <see cref="IDeletionScheduler.GracePeriodDays"/>
/// constant is the one place the *grace* length lives; this is the *check*
/// cadence). Postgres-backed durability (the <c>AuditPurgeHandler</c> "a
/// Coolify redeploy mid-hour does not silently drop a pending run" precedent).
/// </summary>
public sealed record DeletionDueTick() : Wolverine.TimeoutMessage(TimeSpan.FromHours(1));
