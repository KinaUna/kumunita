using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Notifications;
using Kumunita.Core.UserInfo;
using Marten;

namespace Kumunita.Web.SideEffects;

/// <summary>
/// M20 (ADR 0121, D5) — the §6.4 <c>NotificationFlush</c> job's Web adapter
/// (the <see cref="EventReminderHandler"/> / <see cref="AuditPurgeHandler"/>
/// shape verbatim: a self-rescheduling <see cref="Wolverine.TimeoutMessage"/>
/// whose static <c>Handle</c> injects the live services into the Wolverine-free
/// Core service, then re-yields the next tick).
/// <para>
/// The flush's <em>business logic</em> — load the held
/// (<c>EmailDeferred == true</c>) <see cref="Notification"/> rows, re-resolve
/// each recipient's ADR 0019 effective zone, re-evaluate the pure
/// <see cref="QuietScheduleEvaluator.IsQuietNow"/> (D3) at the <b>run</b>
/// instant, stage the now-clear emails via the <b>frozen</b>
/// <see cref="IMailerStage"/> under the deferred idempotency key
/// (<see cref="NotificationService.DeferredKey"/>, D4), flip them
/// <c>EmailDeferred = false</c>, and leave the still-quiet ones deferred — is
/// the Wolverine-free <see cref="NotificationFlushService"/> in
/// <c>Kumunita.Core</c> (U04). The harness tests prove the release /
/// idempotency shape without a message host; this handler is just a thin
/// adapter that injects the live <see cref="IDocumentStore"/> + the frozen
/// <see cref="IMailerStage"/> stager + <see cref="IUserInfoService"/> +
/// <see cref="ILocalizationService"/> + <see cref="ITranslationProvider"/>
/// into that service, then re-schedules the next tick.
/// </para>
/// <para>
/// **The one sanctioned divergence from the three fixed-cadence §6.4 jobs**
/// (AuditPurge 1-day / EventReminder 1-day / UsagePurge 365-day): their
/// cadence is a <em>constant</em>, so re-yielding the same message type carries
/// the same baked-in schedule. M20's cadence is the **admin-set**
/// <see cref="LocaleSettings.QuietCheckMinutes"/> (D6) — a runtime value an
/// admin tightens or loosens — so the handler re-yields a <b>fresh</b>
/// <see cref="NotificationFlushTick"/> carrying the <b>resolved</b> delay each
/// run (the <see cref="NotificationFlushTick"/> record is a *parameterized*
/// <c>TimeoutMessage</c> rather than a fixed-baked one).
/// </para>
/// <para>
/// The cadence is read through U04's frozen read seam
/// <see cref="ILocalizationService.GetQuietCheckMinutesAsync"/> (no audit row)
/// — which floors a missing <see cref="LocaleSettings"/> row to <c>60</c>
/// (C-M20·6) — so this handler does <b>not</b> load the singleton itself or
/// invent a floor constant (single read path, the D6 shape). It is **not** a
/// config POCO and **not** an <c>IOptions</c> (unlike the M4
/// <see cref="Kumunita.Core.Events.EventReminderService"/> which takes
/// <c>EventReminderOptions</c>).
/// </para>
/// <para>
/// **The handler does NOT re-run <see cref="NotificationService.EmitAsync"/>
/// (D5's *Forbids*)** — re-emitting would double-store the inbox row (ADR 0076
/// D7, C-M20·1). It only *stages the held email* for rows whose quiet has
/// lifted. No new authorization surface (C-M20·5): the flush is a side effect,
/// not an access decision — no <c>AccessAction</c> / <c>AccessVia</c> /
/// <c>Decide()</c> branch is added.
/// </para>
/// </summary>
public static class NotificationFlushHandler
{
    /// <summary>
    /// Durable recurring tick: flushes the held (deferred) notification emails
    /// and re-schedules the next run at the **resolved** admin cadence.
    /// <para>
    /// Durability: <c>IntegrateWithWolverine()</c> (Program.cs) backs the
    /// scheduled message with Postgres storage, so a Coolify redeploy between
    /// runs does not silently drop a pending run (the
    /// <see cref="AuditPurgeHandler"/> / <see cref="EventReminderHandler"/>
    /// precedent — treat the §6.4 jobs as needing durable scheduling, not an
    /// in-memory timer).
    /// </para>
    /// <para>
    /// <see cref="Program"/> seeds the first tick with the baked default
    /// (60 min, matching the <see cref="LocaleSettings.QuietCheckMinutes"/>
    /// default); from the second run onward the handler reads the admin's
    /// live value each time.
    /// </para>
    /// </summary>
    public static async Task<IEnumerable<object>> Handle(
        NotificationFlushTick tick,
        IDocumentStore store,
        IMailerStage mailer,
        IUserInfoService userInfo,
        ILocalizationService localization,
        ITranslationProvider translationProvider)
    {
        // `now` is passed explicitly to the service so the quiet verdict is
        // evaluated at the RUN instant (quiet may have lifted since the row was
        // deferred — ADR 0019 run-instant, the EventReminderService shape). The
        // mailer is the frozen IMailerStage seam (the host resolves the real
        // OutboxEmailStager in production); userInfo + localization carry the
        // ADR 0019 zone chain + the D6 cadence read; translationProvider is the
        // ADR 0061 outbound-channel language (accepted by the Core service for
        // call-site parity, unused there because the held email's subject/body
        // are the row's already-localized values).
        //
        // The flush does NOT re-run EmitAsync (D5) — it only stages the held
        // email for the now-clear rows and flips them EmailDeferred = false
        // (the still-quiet rows are left for the next run; C-M20·4).
        await NotificationFlushService.FlushDeferredAsync(
            store,
            DateTimeOffset.UtcNow,
            mailer,
            userInfo,
            localization,
            translationProvider);

        // Self-reschedule at the RESOLVED admin cadence (D6) — NOT the baked
        // default. Read through U04's frozen read seam (no audit row); it floors
        // a missing LocaleSettings row to 60 (C-M20·6), so we do not load the
        // singleton or invent a floor constant. Constructing a fresh
        // NotificationFlushTick with the resolved minutes makes Wolverine
        // deliver the re-publish at that runtime delay (the §6.4 self-reschedule,
        // but with a *resolved* cadence — the one sanctioned divergence from the
        // three fixed-cadence §6.4 jobs).
        var minutes = await localization.GetQuietCheckMinutesAsync();
        return new[] { new NotificationFlushTick(TimeSpan.FromMinutes(minutes)) };
    }
}

/// <summary>
/// M20 (ADR 0121, D5) — the recurring message shape for the
/// <c>NotificationFlush</c> job (see <see cref="NotificationFlushHandler"/>).
/// <para>
/// <c>TimeoutMessage</c> is a <em>record</em> whose base constructor takes a
/// <see cref="TimeSpan"/> schedule (the <see cref="AuditPurgeTick"/> /
/// <see cref="EventReminderTick"/> precedent verbatim). M20 diverges in ONE
/// way: the delay is <b>resolvable at construction time</b> so the handler
/// can construct a **fresh tick with the resolved admin cadence** on each
/// re-schedule. The other three §6.4 jobs re-yield a fixed-baked tick because
/// their cadence is a constant (1 day / 1 day / 365 days); M20's cadence is
/// the **admin-set** <see cref="LocaleSettings.QuietCheckMinutes"/> (D6), a
/// runtime value, so the re-yield must carry the <b>resolved</b> delay.
/// </para>
/// <para>
/// **Two constructors** (rather than a single default-parameter one) because
/// C# requires a default parameter value to be a compile-time constant and
/// <c>TimeSpan.FromMinutes(60)</c> is not one. The parameterless form is the
/// <b>first-boot seed</b> in <see cref="Program"/> (the standard 60-minute
/// cadence — the <see cref="LocaleSettings.QuietCheckMinutes"/> default); the
/// <c>TimeSpan</c> form is the <b>steady-state re-schedule</b> the handler
/// builds at the admin's live cadence. Both are deliberately non-zero (a
/// zero-delay <c>TimeoutMessage</c> would be a fire-immediately, not a
/// scheduled, run).
/// </para>
/// </summary>
public sealed record NotificationFlushTick : Wolverine.TimeoutMessage
{
    /// <summary>
    /// First-boot seed — the standard 60-minute cadence (matching the
    /// <see cref="LocaleSettings.QuietCheckMinutes"/> default). Used by
    /// <see cref="Program"/> to schedule the first run; from the second run
    /// onward the handler re-schedules at the admin's live cadence.
    /// </summary>
    public NotificationFlushTick()
        : base(TimeSpan.FromMinutes(60))
    {
    }

    /// <summary>
    /// Steady-state re-schedule — the handler constructs a fresh tick at the
    /// resolved admin cadence (D6) each run, so the next delivery waits that
    /// many minutes (the §6.4 self-reschedule with a *resolved* delay).
    /// </summary>
    public NotificationFlushTick(TimeSpan delay)
        : base(delay)
    {
    }
}
