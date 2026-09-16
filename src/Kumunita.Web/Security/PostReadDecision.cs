using Kumunita.Core.Authorization;
using Kumunita.Core.Posts;

namespace Kumunita.Web.Security;

/// <summary>
/// The single, shared <c>Read</c>-decision router for a post that owns a
/// user-uploaded media object (an inline content image on
/// <see cref="Post.ImageIds"/>, or a download attachment on
/// <see cref="Post.AttachmentIds"/>).
/// <para>
/// <b>Why this exists:</b> the serving routes
/// (<see cref="Kumunita.Web.Controllers.ContentImageController.Serve"/>,
/// <see cref="Kumunita.Web.Controllers.AttachmentController.Serve"/>) and the
/// reply-parent branch of each need to pick <em>which</em> of the two
/// authorization lanes applies to a given <see cref="Post"/>:
/// <list type="bullet">
/// <item><b>Group-lane post</b> (ADR 0013 G·2 — <see cref="Post.GroupId"/>
/// non-empty): the <b>membership</b> lane
/// (<see cref="IAuthorizationService.CanSeeGroupAsync"/>, G·1 — membership is
/// the sole decision; G·8 — the audience is written non-null empty, so the
/// audience lane would deny everyone except the author).
/// </item>
/// <item><b>Component post</b> (ADR 0001/0006 — <see cref="Post.GroupId"/>
/// empty): the <b>audience</b> lane
/// (<see cref="IAuthorizationService.CanAsync"/> with
/// <see cref="PostToAuditableResource"/>, the M3/M3b read decision).
/// </item>
/// </list>
/// Before this helper, the branch was triplicated inline in the two
/// controllers (post branch + reply-parent branch of each); a group post's
/// media was served to its non-author members only when the routing was
/// written correctly in <em>every</em> copy. Concentrating the routing in
/// one pure, <c>public static</c> seam makes it testable in
/// <c>Kumunita.Web.Tests</c> (NSubstitute — no store, no Postgres) and
/// removes the duplication.
/// </para>
/// <para>
/// <b>Web-only:</b> the routing is a <c>Web</c>-layer concern (it calls
/// <c>IAuthorizationService</c>, whose audit row is the route's, not the
/// Core's); Core stays HTTP-free and body-parse-free (ADR 0006-D). This
/// helper is the same home as <see cref="AttachmentIds"/> /
/// <see cref="ContentImageIds"/> / <see cref="MarkdownRenderer"/>.
/// </para>
/// <para>
/// <b>Decision contract:</b> the returned
/// <see cref="Kumunita.Core.Authorization.Decision"/> is the one the route
/// acts on: <c>Allowed</c> ⇒ serve; <c>!Allowed</c> ⇒ 404 (not 403 — the
/// no-existence-leak rule, C-ATT·2 / R·4) and exactly one audit row
/// (Allow <em>or</em> Deny — the <c>IAuthorizationService</c> seam emits it;
/// this helper does not). The <see cref="Kumunita.Core.Authorization.AccessVia"/>
/// on the row is <see cref="Kumunita.Core.Authorization.AccessVia.Group"/>
/// (or <c>Delegation</c> for an in-scope delegate acting with the owner's
/// membership) on the group lane, and <see cref="Kumunita.Core.Authorization.AccessVia.Owner"/>
/// / <c>Audience</c> / <c>Moderator</c> / <c>BreakGlass</c> / <c>Delegation</c>
/// on the component lane — the audit log's "who did this, by what right"
/// query is preserved by the correct lane being chosen, not by a second
/// call.
/// </para>
/// </summary>
public static class PostReadDecision
{
    /// <summary>
    /// Resolve the single <c>Read</c> decision for <paramref name="post"/>
    /// on behalf of <paramref name="actorId"/>. The lane is chosen by the
    /// post's own shape — <see cref="Post.GroupId"/> non-empty ⇒ group lane
    /// (ADR 0013 G·1/G·2/G·8); <see cref="Post.GroupId"/> empty ⇒ component
    /// lane (ADR 0001/0006) — and <em>exactly one</em> decision call is made
    /// (one audit row).
    /// </summary>
    /// <param name="post">
    /// The owning post (resolved by the serve route's reverse lookup).
    /// Non-null by contract — the route 404s before calling when the
    /// reverse lookup is null.
    /// </param>
    /// <param name="actorId">
    /// The acting subject (the route's
    /// <c>KumunitaPrincipal.SubjectId(User) ?? ""</c>). A non-member to a
    /// group post and a non-audience-member to a component post both return
    /// <c>Allowed == false</c>; the route maps that to a 404.
    /// </param>
    /// <param name="authz">
    /// The <see cref="IAuthorizationService"/> seam (the single decision
    /// path, ADR 0006-D) — the audit row is its, not this helper's.
    /// </param>
    /// <returns>
    /// The <see cref="Kumunita.Core.Authorization.Decision"/> the route
    /// acts on (serve on <c>Allowed</c>; 404 on <c>!Allowed</c>).
    /// </returns>
    public static async Task<Decision> ResolveAsync(
        Post post, string actorId, IAuthorizationService authz)
    {
        ArgumentNullException.ThrowIfNull(post);
        ArgumentNullException.ThrowIfNull(authz);

        if (!string.IsNullOrEmpty(post.GroupId))
        {
            // Group-lane post (ADR 0013 G·2): the membership lane is the
            // sole decision (G·1). The post's <see cref="Post.Audience"/>
            // is non-null empty (G·8 — the audience lane is never
            // evaluated on the group lane), and
            // <see cref="PostToAuditableResource"/> would project it as-is
            // into <see cref="IAuthorizationService.CanAsync"/>'s
            // <c>MatchGroups</c> branch, where the empty-audience-denies
            // invariant (ADR 0006-C1) would deny everyone except the
            // owner branch. The membership lane is the correct decision.
            return await authz.CanSeeGroupAsync(actorId, post.GroupId, post.Id);
        }

        // Component post (ADR 0001/0006): the audience lane (the M3/M3b
        // read decision — owner / audience / moderator / break-glass, in
        // the §4.4 order). The <see cref="PostToAuditableResource"/>
        // adapter is the frozen projection (C-M3·3).
        return await authz.CanAsync(
            actorId, AccessAction.Read, new PostToAuditableResource(post));
    }
}
