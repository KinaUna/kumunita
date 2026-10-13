using Marten;

namespace Kumunita.Core;

/// <summary>
/// The ESC escalation-authorization bounded context's Marten-native document
/// registration surface (ADR 0159, ESC·1; ADR 0004 §B.1, the
/// <c>ErrorReportDocTypes</c> / <c>UsageDocTypes</c> parallel-surface shape).
/// This is a **new** parallel surface for the **two new** ESC docs (the
/// <c>EscalationToken</c> + <c>EscalationOutboundConfig</c>); the M32
/// <c>ErrorReportDocTypes</c> surface is **untouched** (ESC·1), and the four
/// additive <c>ErrorReport</c> fields (ESC·2) ride its existing
/// <c>.Schema.For&lt;ErrorReport&gt;()</c> (ADR 0004 §B.1 idempotent delta at
/// boot — no new surface needed for the additive fields). Both new docs use
/// the conventional <c>string Id</c> identity (Marten default), so only the
/// <c>EscalationToken</c> <c>TokenHash</c> (the validate lookup) +
/// (RevokedAt, Created) (the token-list ordering) indexes need pinning.
/// </summary>
public static class EscalationDocTypes
{
    public static void Configure(StoreOptions opts)
    {
        // EscalationToken — the TokenHash index (the ValidateAsync lookup,
        // ESC·3) + the (RevokedAt, Created) token-list-ordering index (the
        // ListAsync feed orders newest-first; the M17 Bookmark (OwnerId,
        // Created) / M6 Notification (RecipientId, Created) feed-ordering-
        // index shape).
        // NOTE (U03 deviation): the design-doc §2.2.6 pin writes the
        // multi-column index as the tuple form
        // `.Index((t) => (t.RevokedAt, t.Created))`, but the only Index
        // overloads in Marten 9.31.2 / Weasel 9.29.0 are Index(expr) and
        // Index(expr, Action<ComputedIndex>), and ComputedIndex exposes no
        // Name property (only Casing / TenancyScope). A computed index
        // therefore cannot be expressed in the tuple form in this stack — so
        // this uses the anonymous-object form (the M5/M6/M17 `new { ... }`
        // shape), letting Marten auto-derive the name. The semantic intent
        // (index TokenHash + the (RevokedAt, Created) ordering) is unchanged.
        opts.Schema.For<ErrorReports.EscalationToken>()
               .Index(t => t.TokenHash)
               .Index(t => new { t.RevokedAt, t.Created });

        // EscalationOutboundConfig — a singleton (one row per instance, the
        // ESC·4 pin); the conventional string Id identity needs no extra
        // convention (the UsageDocTypes shape).
        opts.Schema.For<ErrorReports.EscalationOutboundConfig>();
    }
}
