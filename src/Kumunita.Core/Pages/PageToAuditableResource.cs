using Kumunita.Core.Authorization;

namespace Kumunita.Core.Pages;

/// <summary>
/// Adapter (ADR 0039 / PG U02): presents a <see cref="Page"/> to the frozen
/// <see cref="IAuthorizationService"/> as an <see cref="IAuditableResource"/>.
/// It mirrors <see cref="Posts.PostToAuditableResource"/> verbatim — the
/// **only** differences are that <see cref="Page.Audience"/> is <b>null
/// allowed</b> (pages default public, the one place pages differ from posts,
/// ADR 0039 §3.4) and <see cref="TargetKind"/> is <c>"page"</c>.
/// <para>
/// Mapping (ADR 0039 §3.4 / §3.2 provenance):
/// <para>
/// <c>Id</c> = <see cref="Page.Id"/>; <c>Name</c> = <see cref="Page.Title"/>
/// or a 60-char-truncated <see cref="Page.Body"/> (the audit row's
/// human-facing label — the <c>PostToAuditableResource</c> shape);
/// <c>OwnerId</c> = <see cref="Page.AuthorId"/> — the owner branch of the
/// decision algorithm is the *only* lane that lets the author see their own
/// page (the ADR 0037 author-pin, ADR 0039 §3.7);
/// <c>Audience</c> = <see cref="Page.Audience"/> (**null allowed** — the
/// frozen <c>Decide()</c> branch 5 treats <c>null</c> as public, so a
/// null-audience page is world-readable; the adapter projects it as-is and
/// never mutates it, per ADR 0001-B); <c>ComponentId</c> =
/// <see cref="Page.ComponentId"/> — a feed organizer / moderation scope,
/// *never* an access boundary (ADR 0039 §4), so projecting it here is safe
/// and carries no decision weight; <c>TargetKind</c> = <c>"page"</c> (the
/// <see cref="AccessAudit.TargetKind"/> discriminator for this line of
/// decisions).
/// </para>
/// <para>
/// The adapter is the **only** new authorization surface (ADR 0039): it
/// plugs into the **frozen** <see cref="IAuthorizationService"/> (ADR 0006 §A
/// — <c>CanAsync</c> / <c>CanSeeAsync</c>) with **no** signature change, **no**
/// new <c>AccessAction</c>, **no** new <c>AccessVia</c>, and **no** new branch
/// in <c>Decide()</c> — <c>PG</c> adds an *adapter*, not a *branch*. The
/// adapter does not *own* the <see cref="Page"/>: a single instance is safe to
/// pass into either overload (<c>CanAsync</c> detail, <c>CanSeeAsync</c>
/// tree) — each call is a value-level projection, not a shared-mutable-state
/// hazard. <c>sealed</c> keeps the surface closed (ADR 0006-D's
/// single-decision-path is what matters, not subclassability).
/// </para>
/// </summary>
public sealed class PageToAuditableResource : IAuditableResource
{
    /// <summary>
    /// Create an adapter for <paramref name="page"/>.
    /// </summary>
    public PageToAuditableResource(Page page) => Page = page;

    /// <summary>The page this adapter presents. The adapter does not own it.</summary>
    public Page Page { get; }

    /// <summary>Resource id = the page's document identity.</summary>
    public string Id => Page.Id;

    /// <summary>
    /// Display name for the audit row — the title, or the body truncated to
    /// 60 chars (57 + "...") when there is no title (the
    /// <see cref="Posts.PostToAuditableResource.Name"/> shape).
    /// </summary>
    public string Name =>
        string.IsNullOrEmpty(Page.Title)
            ? (Page.Body.Length < 60 ? Page.Body : Page.Body[..57] + "...")
            : Page.Title;

    /// <summary>Absolute owner = the author (the owner branch of the
    /// <c>Decide()</c> algorithm; the ADR 0037 author pin, ADR 0039 §3.7).</summary>
    public string? OwnerId => Page.AuthorId;

    /// <summary>
    /// The page's audience, projected verbatim (ADR 0001-B — the adapter
    /// never mutates it). **Null allowed** (unlike
    /// <see cref="Posts.PostToAuditableResource.Audience"/>, which is
    /// non-null by construction): a null audience is public (the frozen
    /// <c>Decide()</c> branch 5) — the one place pages differ from posts
    /// (ADR 0039 §3.4).
    /// </summary>
    public Audience? Audience => Page.Audience;

    /// <summary>
    /// Component scope — a feed organizer / moderation scope (ADR 0039 §4),
    /// never an access boundary; carrying it here gives the moderator-scoped
    /// standing (ADR 0039 §3.7) its scoping key without <c>PG</c> gaining a
    /// moderator read branch.
    /// </summary>
    public string? ComponentId => Page.ComponentId;

    /// <summary>
    /// Resource target kind — <c>"page"</c> (the
    /// <see cref="AccessAudit.TargetKind"/> discriminator for this line of
    /// decisions).
    /// </summary>
    public string TargetKind => "page";
}
