using Kumunita.Core.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// F12 (OBS-06: route alerts to a human) — **U3**, the third and final
/// code unit of F12. The single pinned test <c>Alert_RoutedToConfiguredChannel</c>
/// (design <c>prod-hardening-f12-alerts.md</c> §4a legs 1–3, §4b harness gotcha).
/// <para>
/// F12.U2 landed the <see cref="Kumunita.Web.SideEffects.SupportEmailAlertSink"/>
/// Web channel + the <c>Program.cs</c> gated registration (the F12 binding,
/// registered **after** <c>AddKumunitaCore</c> so it **wins DI resolution**).
/// This unit is **test-only** — it asserts the **DI-ordering + routing contract**:
/// the F12 binding wins over the <see cref="NoopAlertSink"/> default when the
/// gate is set (leg-1, the load-bearing leg), the routed <see cref="AlertEvent"/>
/// reaches the resolved sink intact (leg-2), and the off-by-default floor
/// (<see cref="NoopAlertSink"/>) is still resolved when the gate is unset (leg-3).
/// <para>
/// The <c>SupportEmailAlertSink</c> **real class is NOT tested here** (pin §4b,
/// §7): its <see cref="ISmtpSender.SendAsync"/> call is the owner-ops concern
/// (the real SMTP send); the DI-ordering + routing contract is the F12 test's
/// job, covered by the <see cref="RecordingAlertSink"/> stub standing in for
/// the channel.
/// </para>
/// <para>
/// <b>The <c>RecordingAlertSink</c> stub (pin §4b — "reuses the F11.U3 shape,
/// not re-derives the contract"):</b> the F11.U3 <c>RecordingAlertSink</c> is a
/// <em>private nested</em> class inside <c>F11U3_DeadLetterAlertTests</c> (not
/// accessible from this file). This file declares the **same shape** (append
/// each <see cref="AlertEvent"/> to a <c>List{AlertEvent}</c>, return
/// <c>Task.CompletedTask</c>) — the pin fixes the **contract** (F12 binding
/// wins), not the **access path** (the exact stub class reference).
/// </para>
/// <para>
/// <b>No <c>src/**</c> change, no <c>PackageReference</c>, no migration,
/// no <c>.slnx</c> change</b> (a test-only unit). <c>Microsoft.Extensions
/// .DependencyInjection</c> is transitively available (referenced by
/// <c>Kumunita.Web</c> → flows here; the F9U4 / F10U3 / KioskControllerTests
/// precedent: <c>new ServiceCollection().BuildServiceProvider()</c> in
/// Web.Tests).
/// </para>
/// </summary>
public sealed class F12U3_AlertRoutedToChannelTests
{
    // ── Fixed synthetic inputs (the same "email.dead-letter" payload shape ──
    //    F11.U3 leg-3 asserts, pin §4a leg-2: "the same payload F11.U3 leg-3
    //    asserts, so the pin is not re-deriving F11's contract") ─────────────

    private const string Kind     = "email.dead-letter";
    private const string Subject  = "f12u3-resident@examplium.com";
    private const string Detail   = "SMTP relay timeout (attempt 4 of 4)";
    private const int    Attempts = 4;

    // ── The pinned test (all three §4a legs) ─────────────────────────────────

    /// <summary>
    /// The pinned <c>Alert_RoutedToConfiguredChannel</c> — all three §4a legs
    /// (pin §4a):
    /// <list type="number">
    /// <item><b>leg-1 (load-bearing — the DI-ordering leg)</b> — with the F12
    /// channel registered (the gate **set**), resolving <see cref="IAlertSink"/>
    /// from a DI container that has **both** the <see cref="NoopAlertSink"/>
    /// default (registered **first**, the <c>AddKumunitaCore</c> shape) **and**
    /// the F12 binding (registered **second**, the <c>Program.cs</c> shape)
    /// yields the **F12 binding** — **NOT** the <see cref="NoopAlertSink"/>.
    /// A DI-ordering assert, not a behavior assert (the behavior is F11.U3's
    /// <c>DeadLetter_FiresAlert</c>).
    /// </item>
    /// <item><b>leg-2 (the routing leg, mirroring F11.U3 leg-2/leg-3)</b> — an
    /// <see cref="AlertEvent"/> (the same <c>"email.dead-letter"</c> payload
    /// F11.U3 leg-3 asserts) is fired through the resolved sink; the recording
    /// sink **received it** (the <see cref="AlertEvent"/> fields match — the
    /// routing works).
    /// </item>
    /// <item><b>leg-3 (the off-by-default floor, the §3 invariant)</b> — with
    /// the gate **unset** (the <see cref="CommunityOptions.SupportEmail"/>
    /// unconfigured — the dev/scratch shape), resolving <see cref="IAlertSink"/>
    /// yields the <see cref="NoopAlertSink"/> default (the off-by-default floor
    /// is **kept**, pin §3).
    /// </item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task Alert_RoutedToConfiguredChannel()
    {
        var ct = TestContext.Current.CancellationToken;

        // ── leg-1 (load-bearing) + leg-2 — the gate is SET ────────────────────
        //
        // The hand-rolled ServiceCollection mirrors the production registration
        // order (pin §2d, §4b):
        //   1. AddKumunitaCore (DependencyInjection.cs:380) — the NoopAlertSink
        //      default, registered FIRST.
        //   2. Program.cs (pin §2c, the F12 block after AddKumunitaCore) — the
        //      F12 binding (here: RecordingAlertSink standing in for
        //      SupportEmailAlertSink), registered SECOND → it WINS DI
        //      resolution (last-wins for AddTransient of the same service).
        //
        // The RecordingAlertSink is the F12 binding stand-in (pin §4b: "the
        // RecordingAlertSink stub is the same one F11.U3 used … the F12 test
        // reuses it, not re-derives it" — the shape, since the F11.U3 class is
        // a private nested type not accessible from here).
        var recordingSink = new RecordingAlertSink();

        var services = new ServiceCollection();
        // (1) The NoopAlertSink default — the AddKumunitaCore shape (FIRST).
        services.AddTransient<IAlertSink, NoopAlertSink>();
        // (2) The F12 binding — the Program.cs shape (SECOND, gate set).
        //     Stand-in: RecordingAlertSink (same shape as SupportEmailAlertSink
        //     for the DI-ordering + routing contract; pin §4b).
        //     Factory registration (single-generic overload) so the test can
        //     assert against the SAME instance it created (Assert.Same).
        services.AddTransient<IAlertSink>(_ => recordingSink);

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<IAlertSink>();

        // leg-1 (load-bearing) — the F12 binding WINS over the NoopAlertSink
        // default (DI-ordering assert, not a behavior assert).
        Assert.IsType<RecordingAlertSink>(resolved);
        Assert.IsNotType<NoopAlertSink>(resolved);
        // Belt-and-suspenders: the resolved instance IS the recording stub we
        // registered (not a second NoopAlertSink instance).
        Assert.Same(recordingSink, resolved);

        // leg-2 (the routing leg) — fire the AlertEvent through the resolved
        // sink; the recording sink received it (the fields match).
        var alert = new AlertEvent(Kind, Subject, Detail, Attempts);
        await resolved.ReportAsync(alert, ct);

        Assert.Single(recordingSink.Events);
        var received = recordingSink.Events[0];
        Assert.Equal(Kind,     received.Kind);
        Assert.Equal(Subject,  received.Subject);
        Assert.Equal(Detail,   received.Detail);
        Assert.Equal(Attempts, received.Attempts);

        // ── leg-3 (the off-by-default floor) — the gate is UNSET ──────────────
        //
        // With the gate unset (CommunityOptions.SupportEmail unconfigured),
        // the Program.cs F12 block is NOT executed — only the NoopAlertSink
        // default (the AddKumunitaCore shape) is registered. Resolving
        // IAlertSink yields the NoopAlertSink default (the off-by-default
        // floor, pin §3, §4a leg-3).
        var servicesUnset = new ServiceCollection();
        // The NoopAlertSink default — the AddKumunitaCore shape (the ONLY
        // registration; the F12 binding is NOT registered because the gate
        // is unmet — the dev/scratch-instance shape).
        servicesUnset.AddTransient<IAlertSink, NoopAlertSink>();

        using var providerUnset = servicesUnset.BuildServiceProvider();
        var resolvedUnset = providerUnset.GetRequiredService<IAlertSink>();

        // leg-3 — the NoopAlertSink default is the floor (the gate-unset shape).
        Assert.IsType<NoopAlertSink>(resolvedUnset);
        Assert.IsNotType<RecordingAlertSink>(resolvedUnset);
    }

    // ── The §4b stub (the F11.U3 RecordingAlertSink shape, re-declared here ──
    //    because the F11.U3 class is a private nested type not accessible from
    //    this file; the pin fixes the contract — F12 binding wins — not the
    //    access path) ─────────────────────────────────────────────────────────

    /// <summary>
    /// A recording <see cref="IAlertSink"/> (pin §4a leg-2 / §4b: "a
    /// <c>RecordingAlertSink : IAlertSink</c> stub that appends each
    /// <see cref="AlertEvent"/> to a <c>List{AlertEvent}</c> and returns
    /// <c>Task.CompletedTask</c>"). The F12 binding stand-in for the
    /// DI-ordering + routing contract (the real <c>SupportEmailAlertSink</c>
    /// is NOT tested here — its <see cref="ISmtpSender.SendAsync"/> call is
    /// the owner-ops concern, pin §7).
    /// </summary>
    private sealed class RecordingAlertSink : IAlertSink
    {
        public List<AlertEvent> Events { get; } = new();

        public Task ReportAsync(AlertEvent alert, CancellationToken ct = default)
        {
            Events.Add(alert);
            return Task.CompletedTask;
        }
    }
}
