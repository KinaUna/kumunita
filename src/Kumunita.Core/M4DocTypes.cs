using Kumunita.Core.Announcements;
using Marten;

namespace Kumunita.Core;

/// <summary>
/// The announcements bounded context's Marten-native document registration
/// surface (ADR 0004 §B.1) — the parallel surface to <see cref="M3DocTypes"/>
/// for <c>Kumunita.Core.Announcements</c>. <see cref="Announcement"/> is a POCO
/// with the conventional <c>string</c> <c>Id</c> identity, so no non-default
/// convention (identity, business-key index) needs pinning here: Marten's
/// defaults apply.
/// </summary>
public static class M4DocTypes
{
    /// <summary>
    /// Registers the announcements bounded context's documents. Idempotent:
    /// calling twice is safe — Marten's <c>Schema.For&lt;T&gt;()</c> returns the
    /// same document mapping each time, and the delta is applied idempotently
    /// by <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> at boot.
    /// </summary>
    public static void Configure(StoreOptions opts)
    {
        // Announcements (the "platform announcements" lane): public-scope
        // (every visitor) and community-scope (every signed-in resident).
        // Conventional string Id, so no non-default convention needed.
        opts.Schema.For<Announcement>();
    }
}
