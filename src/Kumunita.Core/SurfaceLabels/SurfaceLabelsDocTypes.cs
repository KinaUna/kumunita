using Kumunita.Core.SurfaceLabels;
using Marten;

namespace Kumunita.Core;

/// <summary>
/// The <c>M29</c> (admin surface labels) bounded context's Marten-native
/// document registration surface (ADR 0152, ADR 0004 §B.1) — the parallel
/// surface to <see cref="SiteContentDocTypes"/> / <see cref="PageDocTypes"/> /
/// <see cref="MediaDocTypes"/> for the new <c>Kumunita.Core.SurfaceLabels</c>
/// context. The <see cref="SurfaceLabels"/> singleton has the conventional
/// <c>string</c> <c>Id</c> identity (fixed to the sentinel <c>singleton</c>,
/// the <c>SiteContent</c> / <c>LocaleSettings</c> shape, ADR 0005 B), so no
/// non-default convention or business-key index is needed — only the explicit
/// <c>Schema.For</c> makes the doc visible to Marten (the SITE/M3/Media/Page
/// precedent: without the call the doc is invisible to the schema).
/// Idempotent: calling twice is safe — Marten's <c>Schema.For&lt;T&gt;()</c>
/// returns the same document mapping each time, and the delta is applied
/// idempotently by <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> at boot
/// (the same dev-only loop / versioned-boot path as SITE/M1/M3/Media/Page).
/// No EF migration (a new Marten doc type is additive per ADR 0004 §B.1); the
/// <c>SiteContent</c> and <c>LocaleSettings</c> docs are untouched (ADR 0150
/// D6 / ADR 0006 module-boundary pin, M29·6).
/// </summary>
public static class SurfaceLabelsDocTypes
{
    /// <summary>
    /// Registers the <see cref="SurfaceLabels"/> singleton document. See the
    /// class summary for the ADR 0004 §B.1 additive / no-migration posture.
    /// </summary>
    public static void Configure(StoreOptions opts)
    {
        // `SurfaceLabels` here is the doc type in the `Kumunita.Core.Surface
        // Labels` namespace; it is fully qualified as
        // `SurfaceLabels.SurfaceLabels` because the unqualified name in this
        // parent namespace resolves to the *namespace* (the type and namespace
        // share the name `SurfaceLabels`) — the exact SiteContent.SiteContent
        // idiom.
        opts.Schema.For<SurfaceLabels.SurfaceLabels>();

        // ADR 0158 — the LBL-2 translation lane (the "future LBL-2 lane" ADR
        // 0152 §D8 named). One row per language of the singleton's 13 surface
        // labels — the (LanguageCode) unique index enforces that at the DB
        // layer (the PostTranslation (PostId, LanguageCode) convention, minus
        // the parent key: the surface-labels singleton is a singleton, so its
        // single identity is implicit and the row is keyed only on its target
        // language). Additive per ADR 0004 §B.1; the SurfaceLabels singleton
        // above is untouched (the exact SiteContentDocTypes ADR 0157 idiom).
        opts.Schema.For<SurfaceLabelTranslation>()
               .UniqueIndex(t => t.LanguageCode);
    }
}
