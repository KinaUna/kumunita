using Kumunita.Core.Localization;
using Kumunita.Core.Messaging;
using Kumunita.Core.Notifications;
using Marten;
using Marten.Services;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// C03 (IMPROVE-CODE lane — plan §C03, D6) — the seam test for the new
/// <see cref="INotificationEmitter"/> emission contract.
/// <para>
/// The whole point of the C03 split: cross-module callers depend on the
/// <em>interface</em>, not the concrete <see cref="NotificationService"/>. This
/// test proves one such caller — the Messaging module's
/// <see cref="MessagingService.SendAsync"/> send lane — resolves the emitter
/// through <see cref="INotificationEmitter"/> and fires the expected
/// <c>message.new</c> emission, **without Postgres** (the seam is pure: the
/// recipient universe is a fixed participant pair, not a store read). A fake
/// emitter (NSubstitute) captures the emission and asserts the kind, recipient,
/// idempotency-key shape, body, and link path the lane produces.
/// </para>
/// <para>
/// This is a **unit** test (non-Testcontainers): it needs no store — the
/// <see cref="MessagingService"/> constructor takes the
/// <see cref="INotificationEmitter"/> as an optional seam, and the only store
/// reads on the path are the instance-enable toggle
/// (<see cref="LocaleSettings.MessagingEnabled"/>) and the
/// <see cref="Conversation"/> participant lookup, both stubbed.
/// </para>
/// </summary>
public class C03_NotificationEmitterContractTests
{
    private const string ConversationId = "conv-c03";
    private const string ActorId = "u-c03-actor";
    private const string RecipientId = "u-c03-other";
    private const string Body = "hi, saw the post?";

    /// <summary>
    /// Fakes the frozen seams a <see cref="MessagingService"/> needs to reach
    /// its send-lane emission: a store whose enablement toggle reads "on" and
    /// whose write session returns the two-participant conversation, plus the
    /// fake <see cref="INotificationEmitter"/> under test. Returns the service
    /// (wired to the fake emitter) and the emitter so the test can assert on it.
    /// </summary>
    private static (MessagingService svc, INotificationEmitter emitter) Build()
    {
        // (a) The instance-enable read — the master gate the send lane checks
        //     before it touches the conversation. Stubs the QuerySession the
        //     IsMessagingEnabledAsync toggle opens.
        var settings = new LocaleSettings { MessagingEnabled = true };
        var querySession = Substitute.For<IQuerySession>();
        querySession.LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, Arg.Any<CancellationToken>())
            .Returns(settings);

        // (b) The write session the send lane opens: returns the conversation
        //     the actor participates in (Store / SaveChangesAsync are no-ops —
        //     NSubstitute auto-returns for the void / Task members).
        var convo = new Conversation
        {
            Id = ConversationId,
            ParticipantA = ActorId,     // the acting actor
            ParticipantB = RecipientId, // the *other* participant = recipient
        };
        var writeSession = Substitute.For<IDocumentSession>();
        writeSession.LoadAsync<Conversation>(ConversationId, Arg.Any<CancellationToken>())
            .Returns(convo);

        // (c) The store composing the two above.
        var store = Substitute.For<IDocumentStore>();
        store.QuerySession().Returns(querySession);
        store.OpenSession(Arg.Any<SessionOptions>()).Returns(writeSession);

        // (d) The fake emitter — the unit under test. Captures the emission
        //     the send lane fires and satisfies the Task<Notification?> return.
        var emitter = Substitute.For<INotificationEmitter>();
        emitter.EmitAsync(
            Arg.Any<IDocumentSession>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            targetId: null,
            linkPath: Arg.Any<string?>(),
            acceptPath: (string?)null,
            declinePath: (string?)null,
            ct: Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Notification?>(null));

        // The whole seam: the caller resolves the emitter through the
        // INotificationEmitter interface (the C03 contract), not the concrete
        // NotificationService — that is the assertion's premise.
        var svc = new MessagingService(store, notifications: emitter);
        return (svc, emitter);
    }

    [Fact]
    public async Task C03_SendLaneEmitsMessageNewToOtherParticipant()
    {
        var (svc, emitter) = Build();

        // Drive the send lane — the actor (ParticipantA) sends to the
        // conversation; the recipient is the other participant (ParticipantB).
        await svc.SendAsync(ConversationId, ActorId, Body);

        // The lane fired the expected emission exactly once.
        await emitter.Received(1).EmitAsync(
            Arg.Any<IDocumentSession>(),
            recipientId: RecipientId,                                  // the other participant, never the sender
            kind: NotificationKinds.MessageNew,                        // the message.new kind (D6)
            idempotencyKey: Arg.Any<string>(),                         // the stable content-derived key (asserted below)
            body: Body,
            targetId: null,                                            // messaging has no per-target scope
            linkPath: $"/messages/{ConversationId}",                  // the item link (the email absolute-links it)
            acceptPath: (string?)null,
            declinePath: (string?)null,
            ct: Arg.Any<CancellationToken>());

        // The idempotency key is the stable, content-derived form the
        // NotificationService dedups on (ADR 0076 D4 / F10): the
        // notification:message.new:{messageId} shape.
        var call = emitter.ReceivedCalls()
            .First(c => c.GetMethodInfo().Name == nameof(INotificationEmitter.EmitAsync));
        var key = (string)call.GetArguments()[3];
        Assert.StartsWith($"notification:{NotificationKinds.MessageNew}:", key, StringComparison.Ordinal);
    }
}
