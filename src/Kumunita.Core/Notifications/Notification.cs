namespace Kumunita.Core.Notifications;

/// <summary>
/// A notification row — a **personal** inbound record for one recipient
/// (ADR 0076 D3: the <c>RecipientId</c> is the whole access story; no
/// <c>Audience</c>, no <c>AccessAction</c>, no <c>IAuditableResource</c>
/// adapter, no audit row on read). The inbox is the **durable** record
/// (D5); the email is the best-effort nudge staged alongside it.
/// </summary>
public sealed class Notification
{
    public string Id { get; set; } = string.Empty;             // surrogate (Marten default)

    public string RecipientId { get; set; } = string.Empty;    // the SubjectId the row is FOR (D3 — the whole access story)
    public string Kind { get; set; } = string.Empty;           // one of <see cref="NotificationKinds"/> (D2 — code-owned closed set)
    public string IdempotencyKey { get; set; } = string.Empty; // the <c>notification:{kind}:{stable-source-id}</c> key (D4, §6.3) — the service-side dedup anchor; a re-emission with the same key is a no-op (F10)
    public string? SourceId { get; set; }                      // the stable source id from the idempotency key (the §6.3 table) — display/debug, never a gate
    public string? Subject { get; set; }                       // the localized subject used for the email (the recipient's language, D6); stored for the inbox row's display
    public string? Body { get; set; }                          // the localized body (the recipient's language, D6) with the UGC snippet (the sender's authored language, ADR 0018)
    public string? LinkPath { get; set; }                      // the same-origin relative path to the item this notification is about (e.g. /posts/{id}#reply-{replyId}); the inbox renders it as a link and the email carries it BaseUrl-prefixed (the VerificationOptions.BaseUrl absolute-link precedent). `null` = no link (non-content kinds, or emitters that predate the field).
    public string? AcceptPath { get; set; }                    // ADR 0095 — the same-origin relative path to the ACCEPT action this notification offers (e.g. /groups/{id}/invitations/accept); the inbox renders it as a clickable action and the email carries it BaseUrl-prefixed (the same absolute-link precedent as LinkPath). `null` = no accept action (kinds that aren't actionable — the overwhelming majority, and every emitter that predates the field).
    public string? DeclinePath { get; set; }                   // ADR 0095 — the same-origin relative path to the DECLINE action this notification offers (e.g. /groups/{id}/invitations/decline); rendered/linked exactly like AcceptPath. `null` = no decline action.

    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? ReadAt { get; set; }                // `null` = unread (D8 — the "mark all read" set is one bulk update)
}
