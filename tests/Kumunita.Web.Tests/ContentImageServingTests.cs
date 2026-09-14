// RC U07 — the 4 <b>serving</b> seam tests pinned by the design doc
// (<c>rich-content-design.md</c> §Pinned seam tests, items 13–16), against
// <see cref="Kumunita.Web.Controllers.ContentImageController.Serve"/>
// (<c>GET /content-image/{id}</c>).
//
// ═══════════════════════════════════════════════════════════════════════════
// DRIFT PAUSE — all 4 pinned names below are recorded, not executed.
//
// The unit-series rule for a test that cannot be written as-pinned against
// the existing seam is a <b>drift pause</b> (record + stop), not a production
// edit and not a forced rewrite. This class is that record: it is the
// executable-<i>absence</i> — the file exists (the 3-file deliverable is
// complete), it names the 4 pinned tests, and it documents exactly why each
// one cannot be driven against the existing seam and what the seam gap is.
// It intentionally carries <b>zero <see cref="Xunit.Fact"/></b> methods:
// authoring any of them would require either a production edit (to make a
// seam substitutable) or a new test-infrastructure file (both forbidden by
// this unit's hard rules), and authoring a <b>non-pinned</b> probe test would
// violate the "never introduce a test whose exact name isn't in the design
// doc's seam list" rule. Zero tests here is the faithful state.
//
// The seam gap (the one root cause behind all 4):
//
//   The serving route's owner resolution calls the concrete, <b>sealed</b>
//   <see cref="Kumunita.Core.Posts.PostService"/> reverse-lookup seams
//   (<see cref="Kumunita.Core.Posts.PostService.FindPostByImageIdAsync"/>
//   and <see cref="Kumunita.Core.Posts.PostService.FindReplyByImageIdAsync"/>)
//   — and <see cref="Kumunita.Web.Controllers.ContentImageController"/>
//   takes <c>PostService</c> as a <b>concrete-type ctor argument</b>, not an
//   interface. Those two methods are Postgres-backed (Marten
//   <c>IDocumentStore.QuerySession()</c> + EF-async LINQ over
//   <c>Post.ImageIds</c> / <c>PostReply.ImageIds</c>), so driving the route
//   end-to-end requires a real Postgres.
//
//   But <see cref="Kumunita.Web.Tests"/> has <b>no Testcontainers /
//   PostgresFixture</b> infrastructure — that lives in
//   <see cref="Kumunita.Core.Tests"/> only (the <see cref="Kumunita.Core.Tests.PostgresFixture"/>
//   class), and <see cref="Kumunita.Web.Tests"/> references
//   <c>Kumunita.Web</c> (not <c>Kumunita.Core.Tests</c>). <see cref="PostService"/>
//   is <b>sealed</b>, so NSubstitute cannot proxy it. The 3-file-diff hard
//   rule (the entire deliverable is the 3 new test files — no csproj edit,
//   no new fixture file) forbids adding the Testcontainers infra to
//   <c>Kumunita.Web.Tests</c> to make this work.
//
//   → The 4 serving tests are therefore <b>unwritable</b> against the
//   existing seam, and are recorded here as drift pauses. The route's
//   <b>behavior</b> they would pin is already pinned <b>elsewhere</b>:
//
//   • R·5's <b>un-audited reverse lookup</b> (the read half) is pinned by the
//     <see cref="Kumunita.Core.Tests.ContentImageOwnershipTests"/>
//     <c>R5_ReverseLookup_FindsOwningPost</c> /
//     <c>R5_ReverseLookup_FindsOwningReply</c> tests (the Core seam, driven
//     against a real Postgres).
//
//   • R·3's <b>verbatim ImageIds write</b> (the data the lookup reads) is
//     pinned by the same file's <c>R3_ImageIdsPopulatedFromBodyLinks</c> /
//     <c>R3_ImageIdsEmpty_WhenNoLinks</c> tests.
//
//   • R·7's <b>zero-migration field pin</b> is pinned by
//     <c>R7_PostPoco_FieldSetUnmodifiedExceptImageIds</c>.
//
//   What these 4 <b>Web</b> tests would additionally pin (the route's
//   <b>decision + HTTP</b> layer, which the Core tests cannot reach) is
//   documented per-name below, so a future unit that adds a substitutable
//   seam (e.g. an interface over the reverse lookup, or Testcontainers in
//   Web.Tests) can lift each one out of this pause verbatim.
// ═══════════════════════════════════════════════════════════════════════════

namespace Kumunita.Web.Tests;

/// <summary>
/// RC U07 — the 4 serving seam tests (design doc §Pinned seam tests, items
/// 13–16) for <see cref="Kumunita.Web.Controllers.ContentImageController.Serve"/>.
/// <b>Drift pause</b> — see the file header for the seam gap (the concrete
/// sealed <see cref="Kumunita.Core.Posts.PostService"/> reverse-lookup is
/// undrivable from this NSubstitute-only Web harness, and the 3-file-diff
/// rule forbids the infra that would make it drivable). This class is the
/// record: it names the 4 pinned tests, documents each one's intended
/// assertion, and carries <b>zero <see cref="Xunit.Fact"/></b> methods
/// (authoring any would require a forbidden production edit or a forbidden
/// new infra file; authoring a non-pinned probe would violate the
/// "exact-name-only" rule).
/// </summary>
public class ContentImageServingTests
{
    // ── 13 — R4_Member_Allows_200_OneAllowAuditRow ────────────────────────
    //
    // Intended (drift-paused): the store's <c>GetAsync</c> returns a
    // <see cref="Kumunita.Core.Media.MediaObject"/>; the post-owner lookup
    // (<c>posts.FindPostByImageIdAsync</c>) returns a post;
    // <c>authz.CanAsync(actor, Read, PostToAuditableResource)</c> →
    // <b>Allow</b> → the result is a <c>FileResult</c> with the stored
    // content type, the response carries <c>X-Content-Type-Options:
    // nosniff</c>, and <c>authz</c> received <c>CanAsync</c> <b>exactly
    // once</b> (no double-audit — the route performs it, per U03's note).
    // <para>
    // Why it is paused: driving <c>Serve</c> to the post-owner branch
    // requires <c>posts.FindPostByImageIdAsync(id)</c> to return a real
    // <c>Post</c> — which needs the sealed <c>PostService</c> over a real
    // Postgres (unavailable here).
    // </para>

    // ── 14 — R4_NonMember_Denies_404_OneDenyAuditRow ──────────────────────
    //
    // Intended (drift-paused): same setup, <c>CanAsync</c> → <b>Deny</b> →
    // assert <b>404</b> (not 403 — R·4's no-leak rule) + <c>CanAsync</c>
    // received exactly once + <b>no</b> <c>OpenReadAsync</c> call (the bytes
    // never stream on Deny).
    // <para>
    // Why it is paused: identical seam gap to item 13 — the post-owner
    // branch is unreachable without the sealed <c>PostService</c> over a
    // real Postgres.
    // </para>

    // ── 15 — R5_Orphan_404_ForGlobalAdmin_ZeroAuditRows ───────────────────
    //
    // Intended (drift-paused): the store returns a <see cref="Kumunita.Core.Media.MediaObject"/>
    // (the bytes <b>exist</b>), <b>all</b> owner lookups (post → reply →
    // announcement → page) return null (orphan — R·4's inert posture) →
    // assert <b>404</b> even though the actor is a GlobalAdmin (the
    // "including GlobalAdmin" case — there is no branch that serves an
    // orphan, FACES R5) + <c>authz.CanAsync</c> received <b>zero</b> times
    // (no owner ⇒ no decision ⇒ no row — the audit belongs to the decision,
    // not the fetch).
    // <para>
    // Why it is paused: driving <c>Serve</c> to the orphan branch requires
    // <b>all four</b> owner lookups to return null — including the sealed
    // <c>PostService</c>'s <c>FindPostByImageIdAsync</c> /
    // <c>FindReplyByImageIdAsync</c> (unsubstitutable here).
    // </para>

    // ── 16 — R4_PlatformPageOwner_ZeroCanAsyncCalls ───────────────────────
    //
    // Intended (drift-paused): the page-owner lookup
    // (<c>pages.FindPageByImageIdAsync</c>) returns a
    // <see cref="Kumunita.Core.Localization.LocalizedPage"/> (the
    // post/reply/announcement lookups return null) → assert a
    // <c>FileResult</c> served + <c>authz.CanAsync</c> received <b>zero</b>
    // times (the platform branch — public by construction, FACES R6's
    // serving half; the R6 render half is U06's grep proof).
    // <para>
    // Why it is paused: driving <c>Serve</c> to the platform branch
    // requires the <b>post</b> and <b>reply</b> lookups to return null
    // first (owner order is post → reply → announcement → page) — which
    // again needs the sealed <c>PostService</c> over a real Postgres.
    // </para>
}
