using Kumunita.Core.Posts;
using Marten;

namespace Kumunita.Core;

/// <summary>
/// M3's Marten-native document registration surface (ADR 0004 §B.1) — the parallel
/// surface to <see cref="M1DocTypes"/> for the posts bounded context
/// (<c>Kumunita.Core.Posts</c>). The three documents are POCOs with the
/// conventional <c>string</c> <c>Id</c> identity, so no non-default convention
/// (identity, business-key index) needs pinning in M3: Marten's defaults apply.
/// </summary>
public static class M3DocTypes
{
    /// <summary>
    /// Registers the M3 domain documents. Idempotent: calling twice is safe —
    /// Marten's <c>Schema.For&lt;T&gt;()</c> returns the same document mapping
    /// each time, and the delta is applied idempotently by
    /// <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> at boot.
    /// </summary>
    public static void Configure(StoreOptions opts)
    {
        // Posts (M3 — posts, component lists; the design doc §2.2 shapes).
        opts.Schema.For<Post>();
        opts.Schema.For<PostReply>();

        // Report: table-in-M3 / flow-in-M3b (design doc §2.2 + §2.6 flag). The
        // table is registered now for forward compatibility; the workflow
        // (file / assign / unlock / resolve) is M3b's, and M3b will add the
        // (PostId, Status) business-key index when it owns the write lane.
        opts.Schema.For<Report>();
    }
}
