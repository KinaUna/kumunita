// ATT U11 — the 5 <b>serving</b> seam tests pinned by the design doc
// (<c>file-attachments-design.md</c> §2.9, items 1–5), against
// <see cref="Kumunita.Web.Controllers.AttachmentController.Serve"/>
// (<c>GET /attachment/{id}</c>).
//
// ═══════════════════════════════════════════════════════════════════════════
// DRIFT PAUSE — all 5 pinned names below are recorded, not executed.
//
// The unit-series rule for a test that cannot be written as-pinned against
// the existing seam is a <b>drift pause</b> (record + stop), not a production
// edit and not a forced rewrite. This class is that record: it is the
// executable-<i>absence</i> — the file exists (the 3-file deliverable is
// complete), it names the 5 pinned tests, and it documents exactly why each
// one cannot be driven against the existing seam and what the seam gap is.
// It intentionally carries <b>zero <see cref="Xunit.Fact"/></b> methods:
// authoring any of them would require either a production edit (to make a
// seam substitutable) or a new test-infrastructure file (both forbidden by
// this unit's hard rules), and authoring a <b>non-pinned</b> probe test would
// violate the "never introduce a test whose exact name isn't in the design
// doc's seam list" rule. Zero tests here is the faithful state.
//
// The seam gap (the one root cause behind all 5) — identical in shape to the
// image lane's <see cref="ContentImageServingTests"/>:
//
//   The serving route's owner resolution calls the concrete, <b>sealed</b>
//   <see cref="Kumunita.Core.Posts.PostService"/> reverse-lookup seams
//   (<see cref="Kumunita.Core.Posts.PostService.FindPostByAttachmentIdAsync"/>
//   and <see cref="Kumunita.Core.Posts.PostService.FindReplyByAttachmentIdAsync"/>)
//   — and <see cref="Kumunita.Web.Controllers.AttachmentController"/> takes
//   <c>PostService</c> as a <b>concrete-type ctor argument</b>, not an
//   interface. Those methods are Postgres-backed (Marten
//   <c>IDocumentStore.QuerySession()</c> + async LINQ over
//   <c>Post.AttachmentIds</c> / <c>PostReply.AttachmentIds</c>), so driving
//   the route end-to-end requires a real Postgres.
//
//   But <see cref="Kumunita.Web.Tests"/> has <b>no Testcontainers /
//   PostgresFixture</b> infrastructure — that lives in
//   <see cref="Kumunita.Core.Tests"/> only (the
//   <see cref="Kumunita.Core.Tests.PostgresFixture"/> class), and
//   <see cref="Kumunita.Web.Tests"/> references
//   <c>Kumunita.Web</c> (not <c>Kumunita.Core.Tests</c>). <see cref="PostService"/>
//   is <b>sealed</b>, so NSubstitute cannot proxy it. The 3-file-diff hard
//   rule (the entire deliverable is the 3 new test files — no csproj edit,
//   no new fixture file) forbids adding the Testcontainers infra to
//   <c>Kumunita.Web.Tests</c> to make this work.
//
//   → The 5 serving tests are therefore <b>unwritable</b> against the
//   existing seam, and are recorded here as drift pauses. The route's
//   <b>behavior</b> they would pin is already pinned <b>elsewhere</b>:
//
//   • The ATT lane's <b>un-audited reverse lookup</b> (the read half) is
//     pinned by the <see cref="Kumunita.Core.Tests.AttachmentOwnershipTests"/>
//     <c>FindPostByAttachmentId_ReturnsOwningPost</c> /
//     <c>FindReplyByAttachmentId_ReturnsOwningReply</c> /
//     <c>FindAnnouncementByAttachmentId_ReturnsOwningAnnouncement</c>
//     tests (the Core seam, driven against a real Postgres).
//
//   • The ATT lane's <b>verbatim AttachmentIds write</b> (the data the lookup
//     reads) is pinned by the same file's
//     <c>PostCreate_PersistsAttachmentIds</c> /
//     <c>ReplyCreate_PersistsAttachmentIds</c> /
//     <c>AnnouncementCreate_PersistsAttachmentIds</c> (+ the reply/announcement
//     edit re-copy tests) tests.
//
//   What these 5 <b>Web</b> tests would additionally pin (the route's
//   <b>decision + HTTP</b> layer, which the Core tests cannot reach) is
//   documented per-name below, so a future unit that adds a substitutable
//   seam (e.g. an interface over the reverse lookup, or Testcontainers in
//   Web.Tests) can lift each one out of this pause verbatim.
// ═══════════════════════════════════════════════════════════════════════════

namespace Kumunita.Web.Tests;

/// <summary>
/// ATT U11 — the 5 serving seam tests (design doc §2.9, items 1–5) for
/// <see cref="Kumunita.Web.Controllers.AttachmentController.Serve"/>.
/// <b>Drift pause</b> — see the file header for the seam gap (the concrete
/// sealed <see cref="Kumunita.Core.Posts.PostService"/> reverse-lookup is
/// undrivable from this NSubstitute-only Web harness, and the 3-file-diff
/// rule forbids the infra that would make it drivable). This class is the
/// record: it names the 5 pinned tests, documents each one's intended
/// assertion, and carries <b>zero <see cref="Xunit.Fact"/></b> methods
/// (authoring any would require a forbidden production edit or a forbidden
/// new infra file; authoring a non-pinned probe would violate the
/// "exact-name-only" rule).
/// </summary>
public class AttachmentServingTests
{
    // ── 1 — AttachServe_F1_AudienceMemberDownloads ────────────────────────
    //
    // Intended (drift-paused): the store's <c>GetAsync</c> returns a
    // <see cref="Kumunita.Core.Media.MediaObject"/>; the post-owner lookup
    // (<c>posts.FindPostByAttachmentIdAsync</c>) returns a post;
    // <c>authz.CanAsync(actor, Read, PostToAuditableResource)</c> →
    // <b>Allow</b> → the result is a <c>FileResult</c> with the stored
    // content type, the response carries <c>X-Content-Type-Options:
    // nosniff</c> + <c>Content-Disposition: attachment; filename=…</c>
    // (C-ATT·2 — the download difference), and <c>authz</c> received
    // <c>CanAsync</c> <b>exactly once</b> (one Allow audit row — the route
    // performs the decision, it is not re-implemented).
    // <para>
    // Why it is paused: driving <c>Serve</c> to the post-owner branch
    // requires <c>posts.FindPostByAttachmentIdAsync(id)</c> to return a real
    // <c>Post</c> — which needs the sealed <c>PostService</c> over a real
    // Postgres (unavailable here).
    // </para>

    // ── 2 — AttachServe_F2_NonMember404 ────────────────────────────────────
    //
    // Intended (drift-paused): same setup, <c>CanAsync</c> → <b>Deny</b> →
    // assert <b>404</b> (not 403 — C-ATT·2's no-existence-leak rule) +
    // <c>CanAsync</c> received exactly once + <b>no</b> <c>OpenReadAsync</c>
    // call (the bytes never stream on Deny) + exactly one <c>Deny</c> audit
    // row (emitted by the single <c>CanAsync</c>, not by the action).
    // <para>
    // Why it is paused: identical seam gap to item 1 — the post-owner branch
    // is unreachable without the sealed <c>PostService</c> over a real
    // Postgres.
    // </para>

    // ── 3 — AttachServe_F3_Orphan404 ───────────────────────────────────────
    //
    // Intended (drift-paused): the store returns a <see cref="Kumunita.Core.Media.MediaObject"/>
    // (the bytes <b>exist</b>), <b>all</b> owner lookups (post → reply →
    // announcement) return null (orphan — C-ATT·7's inert posture) → assert
    // <b>404</b> even though the actor is a GlobalAdmin (there is no branch
    // that serves an orphan — no GlobalAdmin find branch) +
    // <c>authz.CanAsync</c> received <b>zero</b> times (no owner ⇒ no
    // decision ⇒ no row — the audit belongs to the decision, not the fetch).
    // <para>
    // Why it is paused: driving <c>Serve</c> to the orphan branch requires
    // <b>all three</b> owner lookups to return null — including the sealed
    // <c>PostService</c>'s <c>FindPostByAttachmentIdAsync</c> /
    // <c>FindReplyByAttachmentIdAsync</c> (unsubstitutable here).
    // </para>
    // <para>
    // Note (F8, folded into F3 per the design doc §2.9 note): an invalid-id
    // 400/404 with no store round-trip is the same serve-route 404 posture
    // this test exercises — step 1 returns 400 for a bad id *before* any
    // store access, step 2 a 404 for a well-formed-but-unowned id. A future
    // lift may split it into a distinct <c>AttachServe_F8_InvalidId404</c>,
    // but it is not required by the pin.
    // </para>

    // ── 4 — AttachServe_F4_ReplyParentDeny404 ──────────────────────────────
    //
    // Intended (drift-paused) — the <b>ATT-lane-specific</b> one (C-ATT·8):
    // the reply-owner lookup (<c>posts.FindReplyByAttachmentIdAsync</c>)
    // returns a reply → <b>the parent post is resolved</b> (the
    // <c>store.QuerySession()</c> raw <c>LoadAsync&lt;Post&gt;</c> the action
    // performs — no <c>CanAsync</c>, no audit row) →
    // <c>authz.CanAsync(actor, Read, PostToAuditableResource(parent))</c> →
    // <b>Deny</b> → assert <b>404</b> (not 403) + <c>CanAsync</c> received
    // exactly once (against the <b>parent</b>, not the reply) + exactly one
    // <c>Deny</c> audit row + <b>no</b> <c>OpenReadAsync</c> call.
    // <para>
    // Why it is paused: identical seam gap to items 1–2 (the reply-owner
    // branch needs the sealed <c>PostService</c>'s
    // <c>FindReplyByAttachmentIdAsync</c> over a real Postgres) <b>plus</b>
    // the parent-resolution step U9 added (the raw <c>LoadAsync&lt;Post&gt;</c>
    // through <c>IDocumentStore.QuerySession()</c>) — a future lift would
    // drive that parent load and assert the single <c>CanAsync</c> lands on
    // the parent, not the reply.
    // </para>

    // ── 5 — AttachServe_F5_AnnouncementPublicServes ────────────────────────
    //
    // Intended (drift-paused): the announcement-owner lookup
    // (<c>announcements.FindByAttachmentIdAsync</c>) returns an announcement
    // (the post/reply lookups return null first — owner order is post →
    // reply → announcement) → the flat scope gate
    // (<c>announcements.GetAsync</c>) passes (public announcement, or a
    // visible-scope one) → assert a <c>FileResult</c> served with
    // <c>X-Content-Type-Options: nosniff</c> + <c>Content-Disposition:
    // attachment; filename=…</c> + <c>authz.CanAsync</c> received <b>zero</b>
    // times (announcements are not audience-restricted — no <c>CanAsync</c>,
    // no <c>AccessAudit</c> row — the same reasoning as the image lane's
    // announcement branch).
    // <para>
    // Why it is paused: driving <c>Serve</c> to the announcement branch
    // requires the <b>post</b> and <b>reply</b> lookups to return null first
    // (owner order is post → reply → announcement) — which again needs the
    // sealed <c>PostService</c> over a real Postgres.
    // </para>
}
