using Kumunita.Core.Events;
using Marten;

namespace Kumunita.Core;

/// <summary>
/// The <c>M4</c> (Events) bounded context's Marten-native document registration
/// surface (ADR 0004 §B.1 / ADR 0054) — the parallel surface to
/// <see cref="M1DocTypes"/> / <see cref="M3DocTypes"/> / <see cref="MediaDocTypes"/> /
/// <see cref="PageDocTypes"/> for the new <c>Kumunita.Core.Events</c> context. Both
/// documents are POCOs with the conventional <c>string</c> <c>Id</c> identity (the
/// M3 "string Id" convention), so only the two business/feed indexes need pinning;
/// everything else is Marten's default.
/// <para>
/// **The two docs are new, not additive** (ADR 0054 §3.1 / §3.2 — a new bounded
/// context, its own POCOs, its own surface). The **surface** is additive:
/// <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> delta-detects and applies the
/// new tables idempotently at boot; the existing <c>Post</c> / <c>Announcement</c> /
/// <c>Page</c> surfaces are untouched. **Zero migrations for existing surfaces.**
/// </para>
/// </summary>
public static class M4DocTypes
{
    /// <summary>
    /// Registers the M4 (Events) domain documents. Idempotent: calling twice is
    /// safe — Marten's <c>Schema.For&lt;T&gt;()</c> returns the same document mapping
    /// each time, and the delta is applied idempotently by
    /// <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> at boot (the same
    /// dev-only loop / versioned-boot path as M1/M3/Media/Page).
    /// </summary>
    public static void Configure(StoreOptions opts)
    {
        // Event (ADR 0054 §3.1 — the M4 field set): one row per event. Conventional
        // string Id (the M3 "string Id" convention) — Marten's default Id-unique
        // mapping is the document identity.
        //
        // The (ComponentId, Start) **feed ordering** index (design doc §4 — "a
        // (ComponentId, Start) index (the feed ordering shape)"): the
        // ListUpcomingAsync feed (U03) orders survivors by Start (ascending), so the
        // (ComponentId, Start) composite index lets Postgres range-scan a
        // component's upcoming events without a sort. Note: in Postgres a regular
        // (non-unique) composite index treats NULL ComponentId values as a valid
        // leading key (a component-less event still sorts by Start under the NULL
        // ComponentId bucket) — the feed's ComponentId is a *filter*, never a gate
        // (C-M3·2), so the index is an optimization, not an integrity constraint.
        opts.Schema.For<Event>()
               .Index(e => new { e.ComponentId, e.Start });

        // EventRsvp (ADR 0054 §3.2 — the §3.2 last-write-wins exception): one row
        // per (event, resident) pair. The (EventId, UserId) **unique** index enforces
        // exactly one RSVP per resident per event at the DB layer (upsert semantics —
        // a conflicting RSVP write is a no-op or self-converging; the resident's
        // latest status is simply the truth). This is the ARCHITECTURE.md §5
        // concurrency-token exception, the GroupMembership / ComponentMembership /
        // PostTranslation business-key convention: the surrogate Id is the document
        // identity, the (EventId, UserId) pair is the DB-enforced business key.
        //
        // The auto-derived name (mt_doc_eventrsvp_uidx_event_iduser_id, ~40 chars)
        // is under Postgres's 64-char NAMEDATALEN limit, so no explicit name is
        // required (unlike M3DocTypes' AnnouncementTranslation edge case).
        opts.Schema.For<EventRsvp>()
               .UniqueIndex(r => r.EventId, r => r.UserId);
    }
}
