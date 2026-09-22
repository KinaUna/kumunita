// M1 step 7 (C3 fix) — U1 compile-truth probe.
// See docs/plans-milestones/done/plan-m1-step-7-outbox-email-c3.md, unit U1 —
// the deliverable of this probe file is proving the exact Wolverine
// `IMessageContext` member the `OutboxEmailStager` will call, by actually
// compiling it against the WolverineFx 6.33.0 assembly the repo pins.
// The reference docs (docs/wolverine/marten-integration-outbox.md) describe
// the *transactional outbox* guarantee prose-level but do not name the
// method; U2 will inline the chosen call into `IMailerStage.cs` and
// delete this file. Do not call these methods from production code.

namespace Kumunita.Core.Identity;

using Wolverine;

/// <summary>
/// Compilation surface for the pinned API. The single public static
/// method <see cref="ProbePublish"/> proves, by compiling against
/// WolverineFx 6.33.0, the exact member the durable
/// <see cref="OutboxEmailStager"/> will call:
/// <c>ValueTask PublishAsync&lt;T&gt;(T message)</c> on
/// <c>Wolverine.IMessageContext</c>.
/// <para>
/// The plan's U1 spec (plan line ~112) offered <c>EnqueueAsync</c> as
/// the canonical name ("e.g. <c>IMessageContext.EnqueueAsync
/// &lt;T&gt;(T message)</c>, or a <c>Publish</c>/<c>Enqueue</c>/<c>
/// Schedule</c>-named method that actually exists on the installed
/// assembly"). U1's deliverable is to pin the *exact* name, not assume
/// one — and the compiler's verdict is that <c>EnqueueAsync</c>
/// <strong>does not exist</strong> on <c>IMessageContext</c> in 6.33.0
/// (<c>CS1061: 'IMessageContext' does not contain a definition for
/// 'EnqueueAsync'</c>). The surface <c>PublishAsync&lt;T&gt;(T)
/// </c> does exist, returns a <c>ValueTask</c>, and is the correct
/// call to use.
/// </para>
/// <para>
/// Why this lives in <c>Kumunita.Core</c> (and not a Web-only probe): the
/// package prerequisite lives in Core's <c>.csproj</c>, so the probe
/// must be compiled where Core's own compile unit already resolves the
/// dependency. This file is <c>internal</c> — it does not widen any of
 /// the public surface; <see cref="IMailerStage"/>'s public contract
/// stays byte-for-byte unchanged (U1's Exit is "no public API change
/// yet").
/// </para>
/// </summary>
internal static class U1PinnedApiProbe
{
    /// <summary>
    /// <b>Pinned surface — U2 will use this call shape.</b>
    /// Verbatim, from the WolverineFx 6.33.0 assembly:
    /// <code>public System.Threading.Tasks.ValueTask PublishAsync&lt;T&gt;(T message)</code>
    /// on <c>Wolverine.IMessageContext</c> (namespace <c>Wolverine</c>,
    /// type <c>IMessageContext</c>).
    /// <para>
    /// This is the per-scope, method-injectable handler-context API
    /// (Wolverine 6.x), the type the plan's own prose names in U1's
    /// Exit ("e.g. <c>IMessageContext.EnqueueAsync&lt;T&gt;(T message)</c>,
    /// or a <c>Publish</c>/<c>Enqueue</c>/<c>Schedule</c>-named method
    /// that actually exists on the installed assembly"). The name is NOT
    /// <c>EnqueueAsync</c> — that member does not exist on
    /// <c>IMessageContext</c> in 6.33.0 (verified: the compiler reports
    /// <c>CS1061: 'IMessageContext' does not contain a definition for
    /// 'EnqueueAsync'</c>). And it returns a <c>ValueTask</c> (not an
    /// <c>Envelope</c> reference — fire-and-participate-in-ambient-
    /// transaction; there's nothing to inspect on the return).
    /// </para>
    /// <para>
    /// Why <c>PublishAsync</c> here is still the only correct surface
    /// (not a fallback to <c>IMessageBus.PublishAsync</c> as the plan
    /// risk R1 warns against): <c>IMessageContext</c> IS the
    /// per-scope/message-bus primitive, and its <c>PublishAsync</c>
    /// routes through Wolverine's own transactional-outbox middleware
    /// (registered by <c>.IntegrateWithWolverine()</c> in
    /// <c>Kumunita.Web/Program.cs</c>) — the same middleware that holds
    /// a handler-cascaded message's envelope until the ambient Marten
    /// session's <c>SaveChangesAsync</c> commits (the
    /// <c>docs/wolverine/marten-integration-outbox.md</c> guarantee).
    /// <c>IMessageBus.PublishAsync</c> (host-level) additionally asserts
    /// <c>WolverineRuntime.AssertHasStarted</c> (see the
    /// <c>AuditPurgeTick</c> comment in <c>Program.cs</c> ~line 324);
    /// <c>IMessageContext.PublishAsync</c> does not.
    /// </para>
    /// <para>
    /// U2 will call it exactly as:
    /// <code>await context.PublishAsync&lt;OutboxEmail&gt;(email);</code>
    /// inside <c>OutboxEmailStager.StageAsync</c>, after
    /// <c>session.Store(email)</c>, before the caller's
    /// <c>await session.SaveChangesAsync()</c>.
    /// </para>
    /// </summary>
    public static ValueTask ProbePublish(IMessageContext context, OutboxEmail email)
        => context.PublishAsync(email);
}
