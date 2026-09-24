using Kumunita.Core.Notifications;
using Marten;

namespace Kumunita.Core;

/// <summary>
/// The <c>M6</c> (Notifications) bounded context's Marten-native document
/// registration surface (ADR 0004 §B.1 / ADR 0076 D1) — the parallel surface
/// to <see cref="M5DocTypes"/> for the new <c>Kumunita.Core.Notifications</c>
/// context. Both documents are POCOs with the conventional <c>string</c>
/// <c>Id</c> identity (the M5 convention), so only the feed/index needs
/// pinning. The docs are new, not additive; the surface is additive —
/// <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> delta-detects and
/// applies the new tables idempotently at boot. **Zero migrations for
/// existing surfaces.**
/// </summary>
public static class M6DocTypes
{
    public static void Configure(StoreOptions opts)
    {
        // Notification — conventional string Id (Marten's default); the
        // (RecipientId, Created) **feed-ordering** index (the
        // <c>ListInboxAsync</c> feed orders survivors by `Created` descending —
        // the M5 <c>TodoItem</c> (ComponentId, Created) index shape); the
        // <c>IdempotencyKey</c> index (the F10 re-emission dedup anchor — the
        // <c>EmitAsync</c> look-up that makes a same-key re-emission a no-op).
        opts.Schema.For<Notification>()
               .Index(n => new { n.RecipientId, n.Created })
               .Index(n => n.IdempotencyKey);

        // NotificationPreference — the RecipientId is the document id (one
        // row per recipient); no additional indexes needed.
        opts.Schema.For<NotificationPreference>();
    }
}
