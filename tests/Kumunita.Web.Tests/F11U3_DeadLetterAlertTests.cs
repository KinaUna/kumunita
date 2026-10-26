using Kumunita.Core;
using Kumunita.Core.Identity;
using Kumunita.Web.SideEffects;
using Marten;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// F11 (JOB-02: alert on dead-letter exhaustion) — **U3**, the third and final
/// unit of F11. The single pinned test <c>DeadLetter_FiresAlert</c> (design
/// <c>prod-hardening-f11-dlq-alert.md</c> §4a legs 1–4, §4b harness gotcha).
/// <para>
/// F11.U2 landed the load-bearing contract — the <see cref="IAlertSink"/> Core
/// seam + the closed <see cref="AlertEvent"/> payload + the
/// <see cref="NoopAlertSink"/> default binding + the
/// <see cref="OutboxEmailHandler.HandleFault"/> trigger (the <c>ReportAsync</c>
/// call added AFTER the <see cref="EmailDeadLetter"/> row commit, wrapped in
/// the best-effort <c>try/catch</c> swallow). This unit drives
/// <see cref="OutboxEmailHandler.HandleFault"/> end-to-end with a **real**
/// <see cref="PostgresFixture"/> <see cref="IDocumentSession"/> + a synthetic
/// <c>Wolverine.Fault{OutboxEmail}</c> envelope + a recording / throwing
/// <see cref="IAlertSink"/> stub, and asserts the four §4a legs. **No
/// <c>src/**</c> file is touched** (a test-only unit); no
/// <c>PackageReference</c> is added (<c>WolverineFx</c> is already referenced
/// by <c>Kumunita.Web</c> and flows transitively to this assembly).
/// </para>
/// <para>
/// <b>The <c>Wolverine.Fault{OutboxEmail}</c> construction shape — the §4b
/// "the pin fixes the contract, not the access path" judgment call
/// (confirmed by reflection against the 6.50.2 assembly, not guessed):</b>
/// a single public constructor,
/// <c>ctor(T Message, ExceptionInfo Exception, int Attempts,
/// DateTimeOffset FailedAt, string CorrelationId, Guid ConversationId,
/// string TenantId, string Source, IReadOnlyDictionary{string,string} Headers)</c>.
/// The handler reads only <c>fault.Message</c> (the origin
/// <see cref="OutboxEmail"/>), <c>fault.Exception?.Message</c> (the last
/// delivery failure → the row's <c>LastError</c>), and <c>fault.Attempts</c>
/// (→ the row's <c>Attempts</c>).
/// </para>
/// <para>
/// <b>The load-bearing <c>ExceptionInfo</c> flag (confirmed by reflection):</b>
/// <see cref="ExceptionInfo.From(Exception, bool, bool)"/> — the <em>first</em>
/// boolean is the include-<c>Message</c> flag. <c>From(ex, false, false)</c>
/// yields an <em>empty</em> <c>.Message</c> (the <c>ExceptionInfo</c> is
/// type/stack-trace only); <c>From(ex, true, true)</c> preserves it. Leg-3
/// asserts <c>AlertEvent.Detail == fault.Exception.Message</c>, and the
/// <see cref="EmailDeadLetterWriter"/> records <c>row.LastError =
/// fault.Exception?.Message</c> — so the envelope's <c>ExceptionInfo</c> is
/// built with <c>ExceptionInfo.From(ex, true, true)</c> to keep the message
/// observable end-to-end (the exact last-delivery-failure string the operator
/// re-queues against, OPS §7).
/// </para>
/// <para>
/// <b>The <see cref="PostgresFixture"/> wiring (mirrors
/// <see cref="F5U4_KillAllTests"/> — Web.Tests carries its own
/// byte-for-byte <see cref="PostgresFixture"/>):</b> a fresh scratch database
/// per test class, a real <see cref="DocumentStore"/> over the
/// <c>mt</c> schema with <see cref="CoreDocTypes.Configure"/> (which registers
/// the <see cref="EmailDeadLetter"/> doc — <c>CoreDocTypes.cs:109</c>),
/// <c>ApplyAllConfiguredChangesToDatabaseAsync</c>, then a live
/// <see cref="IDocumentSession"/>. <see cref="F5U4_KillAllTests"/> also boots
/// the EF <c>Identity</c> store, but <see cref="OutboxEmailHandler.HandleFault"/>
/// is Wolverine-free / Marten-only (it writes exactly one
/// <see cref="EmailDeadLetter"/> row), so the EF/Identity surface is not needed
/// here and is deliberately omitted (the smallest correct harness — the F5U4
/// EF boot is load-bearing for <c>UserManager</c> stamp rotation, which has no
/// analog in this test).
/// </para>
public sealed class F11U3_DeadLetterAlertTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // Fixed synthetic inputs (pin §4a leg-3 — direct-property asserts against
    // these exact values, not a self-referential "matches what I passed in").
    private const string Recipient = "f11u3-resident@examplium.com";
    private const string IdemKey   = "verify:u-f11u3:7";
    private const string Subject   = "Confirm your email address";
    private const string LastError = "SMTP timeout talking to mail.example:587 (attempt 6 of 6)";
    private const int    Attempts  = 6;

    // ── The pinned test (all four §4a legs) ─────────────────────────────────

    /// <summary>
    /// The pinned <c>DeadLetter_FiresAlert</c> — all four §4a legs (pin §4a):
    /// <list type="number">
    /// <item><b>leg-1</b> — the <see cref="EmailDeadLetter"/> row is STILL
    /// written (F11 is additive, not a re-shape) — the row count went up by 1.
    /// </item>
    /// <item><b>leg-2</b> — <see cref="IAlertSink.ReportAsync"/> was called
    /// EXACTLY ONCE (the "one alert per dead-letter" contract) via a
    /// <see cref="RecordingAlertSink"/> stub.
    /// </item>
    /// <item><b>leg-3</b> — the recorded <see cref="AlertEvent"/> matches pin
    /// §2b exactly (a direct-property assert on <c>Kind</c> / <c>Subject</c> /
    /// <c>Detail</c> / <c>Attempts</c>).
    /// </item>
    /// <item><b>leg-4</b> (load-bearing) — a <see cref="ThrowingAlertSink"/>
    /// does NOT break the write: the row is STILL written AND
    /// <see cref="OutboxEmailHandler.HandleFault"/> returns normally (the
    /// <c>try/catch</c> swallow — a sink failure must not turn a dead-letter
    /// into a handler failure).
    /// </item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task DeadLetter_FiresAlert()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await BootStoreAsync(ct);

        // ── leg-1 + leg-2 + leg-3 (a recording sink) ────────────────────────
        await using (var session = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            var before = await session.Query<EmailDeadLetter>().CountAsync(ct);
            var sink   = new RecordingAlertSink();

            await OutboxEmailHandler.HandleFault(BuildFault(), session, sink);

            // leg-1 — the EmailDeadLetter row is STILL written (the count went
            // up by exactly 1 — F11 is additive; the row is the load-bearing
            // /health degraded signal, unchanged).
            var after = await session.Query<EmailDeadLetter>().CountAsync(ct);
            Assert.Equal(before + 1, after);

            // leg-2 — ReportAsync was called EXACTLY ONCE (one alert per
            // dead-letter). Assert.Single is the xunit-analyzers-preferred
            // "exactly one" check (xUnit2013).
            Assert.Single(sink.Events);

            // leg-3 — the AlertEvent matches pin §2b exactly (a direct-property
            // assert on the closed payload — Kind / Subject / Detail / Attempts).
            var e = sink.Events[0];
            Assert.Equal("email.dead-letter", e.Kind);
            Assert.Equal(Recipient,  e.Subject);
            Assert.Equal(LastError,  e.Detail);
            Assert.Equal(Attempts,   e.Attempts);
        }

        // ── leg-4 (load-bearing) — a throwing sink must not break the write ─
        await using (var session = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            var before = await session.Query<EmailDeadLetter>().CountAsync(ct);
            var sink   = new ThrowingAlertSink();

            // HandleFault must RETURN NORMALLY (no exception propagates) even
            // though the sink's ReportAsync throws — the try/catch swallow
            // (pin §2a / §3.1: a sink failure never turns a dead-letter into a
            // handler failure).
            await OutboxEmailHandler.HandleFault(BuildFault(), session, sink);

            // leg-1 holds even with a failing sink — the row is STILL written.
            var after = await session.Query<EmailDeadLetter>().CountAsync(ct);
            Assert.Equal(before + 1, after);

            // The sink DID receive the alert (it threw AFTER the payload
            // arrived — confirming the firing path reached it); the throw was
            // swallowed upstream (HandleFault returned normally above).
            Assert.Equal(1, sink.AttemptCount);
        }
    }

    // ── A synthetic Fault<OutboxEmail> envelope (the §4b access-path) ───────

    /// <summary>
    /// Build the synthetic <c>Wolverine.Fault{OutboxEmail}</c> envelope. The
    /// origin <see cref="OutboxEmail"/> carries the <c>Recipient</c> /
    /// <c>IdempotencyKey</c> / <c>Subject</c> the writer copies onto the row;
    /// the <see cref="ExceptionInfo"/> carries the last delivery failure
    /// (<c>.Message</c> == <see cref="LastError"/>). <see cref="Attempts"/> is
    /// the 6-over-~24h count (§6.2). Built with
    /// <c>ExceptionInfo.From(ex, true, true)</c> — the first boolean preserves
    /// <c>.Message</c> (the load-bearing leg-3 input; <c>(ex, false, false)</c>
    /// would yield an empty <c>.Message</c> and the <c>Detail</c> assert would
    /// not exercise the real last-failure string).
    /// </summary>
    private static Wolverine.Fault<OutboxEmail> BuildFault()
    {
        var origin = new OutboxEmail
        {
            Id             = Guid.NewGuid().ToString("N"),
            IdempotencyKey = IdemKey,
            Recipient      = Recipient,
            Subject        = Subject,
            Body           = "Please confirm your email address.",
            QueuedAt       = DateTimeOffset.UtcNow,
        };

        var exceptionInfo = Wolverine.ExceptionInfo.From(new InvalidOperationException(LastError), true, true);
        var headers       = new System.Collections.Generic.Dictionary<string, string?>();

        return new Wolverine.Fault<OutboxEmail>(
            origin,          // Message — the origin OutboxEmail (the writer's source)
            exceptionInfo,   // Exception — the last delivery failure (.Message preserved)
            Attempts,        // Attempts — the 6-over-~24h count (§6.2)
            DateTimeOffset.UtcNow, // FailedAt
            Guid.NewGuid().ToString("N"), // CorrelationId
            Guid.NewGuid(),  // ConversationId
            null,            // TenantId — single-tenant (no multi-tenant model)
            "test",          // Source
            headers);        // Headers
    }

    // ── Harness (the F5U4_KillAllTests.BootAsync shape, trimmed to Marten) ─

    /// <summary>
    /// Boot a real <see cref="DocumentStore"/> over a fresh scratch database
    /// (the <see cref="PostgresFixture"/> seam) with the <c>mt</c> schema +
    /// <see cref="CoreDocTypes"/> (which registers <see cref="EmailDeadLetter"/>),
    /// with the DDL applied. Mirrors <see cref="F5U4_KillAllTests"/> — minus the
    /// EF <c>Identity</c> boot, which <see cref="OutboxEmailHandler.HandleFault"/>
    /// does not touch (it is Wolverine-free / Marten-only). An instance method so
    /// it can read the primary-constructor <see cref="PostgresFixture"/> fixture
    /// (a static method cannot — the CS9105 the repo's C#12 surface enforces).
    /// </summary>
    private async Task<DocumentStore> BootStoreAsync(CancellationToken ct)
    {
        var conn = await fixture.NewDatabaseAsync(ct);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            CoreDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);
        return store;
    }

    // ── The two §4a stubs ───────────────────────────────────────────────────

    /// <summary>
    /// leg-2 / leg-3 — a recording <see cref="IAlertSink"/> (pin §4a leg-2:
    /// "a <c>RecordingAlertSink : IAlertSink</c> stub that appends each
    /// <c>AlertEvent</c> to a <c>List{AlertEvent}</c> and returns
    /// <c>Task.CompletedTask</c>").
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

    /// <summary>
    /// leg-4 (load-bearing) — a <see cref="ThrowingAlertSink"/> (pin §4a
    /// leg-4: "a <c>ThrowingAlertSink</c> (a stub whose <c>ReportAsync</c>
    /// throws)"). <see cref="AttemptCount"/> records that the firing path
    /// reached the sink before it threw.
    /// </summary>
    private sealed class ThrowingAlertSink : IAlertSink
    {
        public int AttemptCount { get; private set; }

        public Task ReportAsync(AlertEvent alert, CancellationToken ct = default)
        {
            AttemptCount++;
            throw new InvalidOperationException("synthetic alert-channel failure (leg-4)");
        }
    }
}
