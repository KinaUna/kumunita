namespace Kumunita.Core.Pages;

/// <summary>
/// The <b>kind</b> of a <see cref="Page"/> (ADR 0040; the <c>PG-B</c> extension
/// to the <c>PG</c> lane, ADR 0039): the standing namespace a page lives in.
/// Two kinds, closed set — no third kind is introduced by a lane that needs
/// one (a new kind is a new ADR, not a silent addition).
/// <para>
/// <see cref="System"/> — a **platform** page: the canonical
/// <c>system/about</c> / <c>system/terms</c> / <c>system/help</c> tree. The
/// standing lane is **GlobalAdmin only** (a community Moderator has *no*
/// standing on a system page — the ADR 0040 amendment to the ADR 0039 §3.7
/// matrix; the ADR 0029 translation lane is retained for a system page, so a
/// Translator may add a translation of a system page). A system page is
/// never nested under a <see cref="User"/> page (the namespace guard in
/// <see cref="PageService.EnsureCreateNamespace"/> /
/// <see cref="PageService.EnsureMoveNamespace"/> enforces this).
/// </para>
/// <para>
/// <see cref="User"/> — a **resident's blog** page: a page the resident
/// authored for their <c>blog/{subjectId}/…</c> subtree (the per-user blog
/// feed, ADR 0040). The standing lane is **author ∪ GlobalAdmin ∪
/// ModeratorComponent(<c>ComponentId</c>)** (the ADR 0039 §3.7 shape,
/// unchanged for this kind) — the author may move / delete their own blog
/// pages (a behavioral change vs. the ADR 0039 move/delete lane, which was
/// admin/mod-only; ADR 0040 enables the author lane for a <see cref="User"/>
/// page). A user page is never nested under a <see cref="System"/> page
/// (the namespace guard).
/// </para>
/// <para>
/// <b>Additive</b> (ADR 0004 §B.1 — delta-detected, idempotent, no seed
/// reset, zero migrations for the new field): the existing <see cref="Page"/>
/// POCO gains a <c>PageKind</c> property; the <see cref="PageDocTypes"/>
/// registration is unchanged (a non-indexed scalar field on a document — no
/// business-key / unique-index change).
/// </para>
/// <para>
/// **Default:** <see cref="System"/> — the existing canonical pages (the
/// seeder's <c>terms</c> / <c>help</c>) and every page created before this
/// field existed are system pages by default; a resident's blog page is
/// explicitly set to <see cref="User"/> by the owning write lane (the
/// <c>PG-B</c> composer sets it for the user page, the seeder sets it for
/// the canonical pages).
/// </para>
/// </summary>
public enum PageKind
{
    /// <summary>A **platform** page (the <c>system/…</c> subtree) — the standing
    /// lane is <b>GlobalAdmin only</b> (a community Moderator has no standing;
    /// a Translator may still add a translation, the ADR 0029 lane retained).</summary>
    System,

    /// <summary>A **resident's blog** page (the <c>blog/{subjectId}/…</c>
    /// subtree) — the standing lane is <b>author ∪ GlobalAdmin ∪
    /// ModeratorComponent(<c>ComponentId</c>)</b> (the ADR 0039 §3.7 shape
    /// unchanged for this kind); the author may move / delete their own blog
    /// pages (the ADR 0040 behavioral change for this kind).</summary>
    User,
}
