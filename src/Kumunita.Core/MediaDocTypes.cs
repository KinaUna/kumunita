using Kumunita.Core.Media;
using Marten;

namespace Kumunita.Core;

/// <summary>
/// Media module's Marten-native document surface (ADR 0004 §B.1) — the parallel
/// surface to <see cref="M1DocTypes"/> / <see cref="M3DocTypes"/>. <see cref="MediaObject"/>
/// is a POCO with the conventional <c>string</c> <c>Id</c> identity — its <c>Id</c>
/// *is* the content hash (C-MED·4), so Marten's default Id-unique mapping is the
/// dedup; no business-key index required (M3's "string Id" convention).
/// </summary>
public static class MediaDocTypes
{
    /// <summary>
    /// Registers the media catalog document. Idempotent: calling twice is safe —
    /// Marten's <c>Schema.For&lt;T&gt;()</c> returns the same document mapping each
    /// time, and the delta is applied idempotently by
    /// <c>ApplyAllConfiguredChangesToDatabaseAsync</c> at boot (the same dev-only
    /// loop / versioned-boot path as M1/M3).
    /// </summary>
    public static void Configure(StoreOptions opts) =>
        opts.Schema.For<MediaObject>();
}
