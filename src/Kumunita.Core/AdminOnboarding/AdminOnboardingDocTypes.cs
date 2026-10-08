using Marten;

namespace Kumunita.Core;

/// <summary>
/// The <c>AdminOnboarding</c> bounded context's Marten-native document
/// registration surface (ADR 0153, ADR 0004 §B.1) — the parallel surface to
/// <see cref="SiteContentDocTypes"/> / <see cref="SurfaceLabelsDocTypes"/>
/// for the new <c>Kumunita.Core.AdminOnboarding</c> context. The <see
/// cref="AdminOnboarding.AdminOnboarding"/> singleton has the conventional
/// <c>string</c> <c>Id</c> identity (fixed to the sentinel <c>singleton</c>),
/// so no non-default convention or business-key index is needed — only the
/// explicit <c>Schema.For</c> makes the doc visible to Marten (the M3/Media/
/// Page/SITE precedent: without the call the doc is invisible to the schema).
/// Idempotent: calling twice is safe — Marten's <c>Schema.For&lt;T&gt;()</c>
/// returns the same document mapping each time, and the delta is applied
/// idempotently by <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> at boot
/// (the same dev-only loop / versioned-boot path as M1/M3/Media/SITE).
/// </summary>
public static class AdminOnboardingDocTypes
{
    /// <summary>
    /// Registers the <see cref="AdminOnboarding.AdminOnboarding"/> singleton
    /// document. See the class summary for the ADR 0004 §B.1 additive /
    /// no-migration posture.
    /// </summary>
    public static void Configure(StoreOptions opts)
    {
        // `AdminOnboarding` here is the doc type in the
        // `Kumunita.Core.AdminOnboarding` namespace; it is fully qualified as
        // `AdminOnboarding.AdminOnboarding` because the unqualified name in
        // this parent namespace resolves to the *namespace* (the type and
        // namespace share the name `AdminOnboarding`).
        opts.Schema.For<AdminOnboarding.AdminOnboarding>();
    }
}
