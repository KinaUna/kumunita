using Kumunita.Core.Logging;
using Kumunita.Core.Privacy;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Kumunita.Web.SideEffects;

/// <summary>
/// The recurring <see cref="RetentionTick"/> job (IMPROVE-SEC D6 / E5 — the
/// Web-side leg of the retention sweep IS33/IS34a made pure + I/O in Core).
/// <para>
/// The <b>work</b> lives in the Wolverine-free, HTTP-free
/// <see cref="RetentionService"/> in <c>Kumunita.Core.Privacy</c> (IS34a,
/// frozen) — this handler is a thin adapter that injects a live
/// <see cref="IDocumentStore"/> + the config-bound
/// <see cref="RetentionOptions"/> (IS33) into that static service, and
/// re-yields a fresh <see cref="RetentionTick"/> so the recurring schedule
/// carries forward. The tick's 1-day delay is baked into the
/// <see cref="Wolverine.TimeoutMessage"/> base type, so every re-publish
/// carries the same cadence — no per-callsite <c>DelayedFor</c> needed
/// (the <c>MediaLifecycleHandler</c> / <c>AuditPurgeHandler</c> shape
/// verbatim). Postgres-backed durability: a Coolify redeploy mid-day does
/// not silently drop a pending run (the <c>AuditPurgeHandler</c> precedent).
/// </para>
/// <para>
/// There is **no DI registration** of <see cref="RetentionService"/> — it is
/// a <c>static</c> class (the <c>UsagePurgeService</c> /
/// <c>MediaLifecycleService</c> shape), and <see cref="RetentionOptions"/>
/// is bound in <c>Program.cs</c> (IS34b) the way
/// <c>AuditPurgeOptions</c> / <c>MediaOptions</c> are.
/// </para>
/// <para>
/// IS35 (D6, the log-file leg): the handler also passes the file-sink
/// directory + retention days to <see cref="RetentionService.RunAsync"/>'s two
/// optional log parameters, so the daily <see cref="RollingFileSink.Retain"/>
/// pass runs every tick (D6: the file-log <c>Retain</c> runs daily, not only
/// at boot). It reads <see cref="FileSinkOptions"/> (the <c>Logging__File</c>
/// section — the same source <c>Program.cs</c> binds at boot) via the
/// <c>IConfiguration</c> Wolverine injects; a missing/unset section falls back
/// to the POCO defaults (the <c>Program.cs</c> <c>?? new FileSinkOptions()</c>
/// shape), so a log-less test harness still resolves. The log leg is the one
/// D6 piece with no <see cref="RetentionOptions"/> key — it is driven by
/// <see cref="FileSinkOptions"/>, not the retention key surface.
/// </para>
/// </summary>
public static class RetentionTickHandler
{
    /// <summary>
    /// Durable recurring tick: self-schedules 1 day ahead. Because the message
    /// type bakes in the delay (<see cref="Wolverine.TimeoutMessage"/>'s
    /// schedule constructor), re-yielding a fresh <see cref="RetentionTick"/>
    /// carries the same schedule every run — no per-callsite
    /// <c>DelayedFor</c> needed.
    /// <para>
    /// <c>Task&lt;IEnumerable&lt;object&gt;&gt;</c> is the async-eligible
    /// cascade shape in Wolverine (an <c>IEnumerable&lt;object&gt;</c>
    /// iterator can't <c>await</c>); we return the array rather than
    /// <c>yield return</c> (which is invalid inside an async method).
    /// </para>
    /// </summary>
    public static async Task<IEnumerable<object>> Handle(
        RetentionTick tick,
        IDocumentStore store,
        IOptions<RetentionOptions> options,
        IConfiguration configuration)
    {
        // `now` is passed explicitly to the service (UtcNow here keeps
        // production correct; the harness pins its own — the
        // AuditPurgeHandler precedent). RetentionService logs counts (no PII);
        // nothing logged here.
        //
        // IS35 (D6): pass the file-sink directory + retention days to the log
        // leg so RollingFileSink.Retain runs daily (not only at boot). The
        // FileSinkOptions read mirrors Program.cs's boot read (the same
        // Logging__File section); a missing/unset section falls back to the
        // POCO defaults (the `?? new FileSinkOptions()` shape).
        var fileSinkOptions =
            configuration?.GetSection(FileSinkOptions.SectionName).Get<FileSinkOptions>()
            ?? new FileSinkOptions();
        await RetentionService.RunAsync(
            store, options.Value, DateTimeOffset.UtcNow,
            logDir: fileSinkOptions.Directory,
            logRetentionDays: fileSinkOptions.RetentionDays);

        // Self-reschedule: return a fresh RetentionTick so the recurring
        // schedule carries forward (the TimeoutMessage's 1-day delay is baked
        // into the type, so this re-publish picks up the same 1-day cadence —
        // the pattern from scheduled-and-delayed-messages.md's Recurring-jobs
        // section). Without this line the tick would fire exactly once and the
        // daily sweep would silently stop after first boot.
        return new[] { new RetentionTick() };
    }
}
