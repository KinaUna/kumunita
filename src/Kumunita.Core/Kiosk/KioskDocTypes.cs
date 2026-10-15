using Kumunita.Core.Kiosk;
using Marten;

namespace Kumunita.Core;

/// <summary>
/// The <c>Kiosk</c> (kiosk mode) bounded context's Marten-native document
/// registration surface (ADR 0164, ADR 0004 §B.1) — the parallel surface to
/// <see cref="SiteContentDocTypes"/> / <see cref="SurfaceLabelsDocTypes"/> /
/// <see cref="EscalationDocTypes"/> for the new
/// <c>Kumunita.Core.Kiosk</c> context (M35·1). The
/// <see cref="KioskPlaylist"/> doc has the conventional <c>string</c>
/// <c>Id</c> identity (the acting resident's subject id — one row per
/// resident, the ADR 0164 amendment; the <c>LocaleSettings</c> per-user
/// shape, ADR 0005 B), so no
/// non-default convention or business-key index is needed — only the explicit
/// <c>Schema.For</c> makes the doc visible to Marten (the
/// SiteContentDocTypes / SurfaceLabelsDocTypes / EscalationDocTypes
/// precedent: without the call the doc is invisible to the schema).
/// <para>
/// The <see cref="KioskView"/> entry is a **value object** nested in the
/// <see cref="KioskPlaylist"/> (not a standalone doc — M35·9: the kiosk adds
/// **no** new doc type beyond <see cref="KioskPlaylist"/>; the M5
/// <c>Projects</c> + M4 <c>Events</c> contexts are **untouched**).
/// </para>
/// The <see cref="KioskPlaylist"/> is now a **per-resident** row (the ADR
/// 0164 amendment — one row per signed-in user, keyed on the user's subject
/// id, rather than the one shared GlobalAdmin singleton); the doc's identity
/// is still the conventional <c>string</c> <c>Id</c>, so the registration is
/// unchanged.
/// <para>
/// Idempotent: calling twice is safe — Marten's <c>Schema.For&lt;T&gt;()</c>
/// returns the same document mapping each time, and the delta is applied
/// idempotently by <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> at boot
/// (the same dev-only loop / versioned-boot path as M1/M3/Media/SiteContent/
/// SurfaceLabels/Escalation). No EF migration (a new Marten doc type is
/// additive per ADR 0004 §B.1); the <c>SiteContent</c> and
/// <c>LocaleSettings</c> docs are untouched (ADR 0006 module-boundary pin,
/// M35·9).
/// </summary>
public static class KioskDocTypes
{
    /// <summary>
    /// Registers the <see cref="KioskPlaylist"/> singleton document. See the
    /// class summary for the ADR 0004 §B.1 additive / no-migration posture.
    /// </summary>
    public static void Configure(StoreOptions opts)
    {
        // `KioskPlaylist` is the doc type in the `Kumunita.Core.Kiosk`
        // namespace; it is referenced via the using directive above (the
        // unqualified name resolves correctly — unlike the SiteContent /
        // SurfaceLabels collision idiom, there is no type/namespace name
        // clash here: `Kiosk` is the namespace, `KioskPlaylist` is the type).
        opts.Schema.For<KioskPlaylist>();
    }
}
