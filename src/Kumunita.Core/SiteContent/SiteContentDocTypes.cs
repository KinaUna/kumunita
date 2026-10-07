using Kumunita.Core.SiteContent;
using Marten;

namespace Kumunita.Core;

/// <summary>
/// The <c>SITE</c> (site content) bounded context's Marten-native document
/// registration surface (ADR 0150, ADR 0004 §B.1) — the parallel surface to
/// <see cref="M1DocTypes"/> / <see cref="PageDocTypes"/> /
/// <see cref="MediaDocTypes"/> for the new <c>Kumunita.Core.SiteContent</c>
/// context. The <see cref="SiteContent"/> singleton has the conventional
/// <c>string</c> <c>Id</c> identity (fixed to the sentinel <c>singleton</c>),
/// so no non-default convention or business-key index is needed — only the
/// explicit <c>Schema.For</c> makes the doc visible to Marten (the M3/Media/
/// Page precedent: without the call the doc is invisible to the schema).
/// Idempotent: calling twice is safe — Marten's <c>Schema.For&lt;T&gt;()</c>
/// returns the same document mapping each time, and the delta is applied
/// idempotently by <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> at boot
/// (the same dev-only loop / versioned-boot path as M1/M3/Media).
/// </summary>
public static class SiteContentDocTypes
{
    /// <summary>
    /// Registers the <see cref="SiteContent"/> singleton document. See the
    /// class summary for the ADR 0004 §B.1 additive / no-migration posture.
    /// </summary>
    public static void Configure(StoreOptions opts)
    {
        // `SiteContent` here is the doc type in the `Kumunita.Core.SiteContent`
        // namespace; it is fully qualified as `SiteContent.SiteContent` because
        // the unqualified name in this parent namespace resolves to the *namespace*
        // (the type and namespace share the name `SiteContent`).
        opts.Schema.For<SiteContent.SiteContent>();
    }
}
