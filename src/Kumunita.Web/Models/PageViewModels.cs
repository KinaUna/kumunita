using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Kumunita.Web.Models;

/// <summary>
/// The <c>/pages</c> Web surface's view models (the Pages lane <c>PG</c>,
/// ADR 0039 §3.8). Three shapes, mirroring the repo's existing surfaces:
/// <list type="bullet">
/// <item><see cref="PageTreeViewModel"/> — the <c>GET /pages</c> tree
///       browse (the C6 <see cref="Kumunita.Core.Authorization.IAuthorizationService
///       .CanSeeAsync"/>(Read)-filtered forest, rendered as a nested list).</item>
/// <item><see cref="PageShowViewModel"/> — the <c>GET /pages/{**path}</c>
///       post view (the single <c>Page</c> + its <see
///       cref="Kumunita.Core.Pages.PageTranslation"/> rows + the ADR 0027
///       chip-swap language set + the draft/edit affordances).</item>
/// <item><see cref="PageComposeViewModel"/> — the <c>GET/POST
///       /pages/new</c> composer <em>and</em> the <c>GET/POST
///       /pages/{id}/edit</c> lane (title, WYSIWYG body, parent picker,
///       the <see cref="AudienceEditorModel"/> verbatim + the
///       <see cref="IsPublic"/> null-audience flag, the ADR 0018 language
///       picker). The same editor + renderer as posts/announcements (the
///       lane's "one editor, one renderer" pin).</item>
/// </list>
/// <para>
/// **Pages default public** (<see cref="Kumunita.Core.Pages.Page.Audience"/>
/// <c>null</c>) — the one place pages differ from posts (ADR 0036 defaults
/// posts to community-visible). <see cref="PageComposeViewModel.IsPublic"/>
/// is the form's expression of that: when <c>true</c> the page is written
/// with <c>Audience = null</c> (world-readable, <c>ComponentId = null</c>);
/// when <c>false</c> the <see cref="PageComposeViewModel.Audience"/> editor
/// (a <see cref="AudienceEditorModel"/>) + <see cref="PageComposeViewModel
/// .CommunityId"/> are what the service stores (the non-null, explicit
/// grant-list / community shape). The composer/edit round-trips it through
/// <see cref="AudienceEditorModel.FromAudience"/> /
/// <see cref="AudienceEditorModel.BuildAudience"/> unchanged (the ADR 0036
/// single-source pin).
/// </para>
/// </summary>

/// <summary>
/// One node of the <c>GET /pages</c> tree browse (the C6
/// <c>CanSeeAsync</c>(Read)-filtered forest). A page the actor may not read
/// is <b>absent</b> (not rendered blanked) — the filter ran at the
/// controller over the whole candidate set, so the model only ever carries
/// visible nodes. <see cref="Path"/> is the derived path (ADR 0039 §3.3)
/// the node's <c>&lt;a href="/pages/…"&gt;</c> uses.
/// </summary>
public sealed record PageNode(
    string Id,
    string Title,
    /// <summary>The derived path (ADR 0039 §3.3) — the anchor href is
    /// <c>/pages/</c> + this. Root <c>about</c> → <c>"about"</c>.</summary>
    string Path,
    /// <summary>Whether the node is a draft (ADR 0037) — visible to the
    /// author only; the tree badge marks it. A non-author never sees a
    /// draft node (the author-only draft gate ran before this node was
    /// offered).</summary>
    bool IsDraft,
    IReadOnlyList<PageNode> Children);

/// <summary>
/// The <c>GET /pages</c> read surface (the tree browse): the caller-visible
/// <see cref="Kumunita.Core.Pages.Page"/> forest as a nested
/// <see cref="PageNode"/> list (a <c>CanSeeAsync</c>(Read)-denied page is
/// absent from <see cref="Roots"/> — the C6 aggregate row, the single
/// <c>CanSeeAsync</c> call over the whole candidate set, not one per node).
/// An empty forest (no pages, or all denied) is a valid shape — the view
/// renders a "no pages yet" note, not a 404.
/// </summary>
public sealed record PageTreeViewModel(IReadOnlyList<PageNode> Roots);

/// <summary>
/// The <c>GET /pages/{**path}</c> post view (the full-body read surface —
/// the tree browse shows a title + path and links here). The page's own
/// shape plus the ADR 0027 chip-swap data (the authored-in
/// <see cref="PageShowViewModel.OriginalLanguageCode"/> as the first
/// default-visible variant, one hidden variant per
/// <see cref="Kumunita.Core.Pages.PageTranslation"/>, and the enabled-catalog
/// <see cref="PageShowViewModel.Languages"/> the chips render from) and the
/// author-only draft + edit affordances (the service's write-lane standing
/// is the real gate — the flags are the buttons' visibility, the ADR 0037
/// author-only <c>IsAuthor</c> / <c>IsDraft</c> pins carried over from
/// announcements). A denied page never reaches this model (the controller
/// 403s on deny / 404s on absent before building it).
/// </summary>
public sealed record PageShowViewModel(
    string Id,
    string Title,
    string Body,
    string Path,
    string OriginalLanguageCode,
    IReadOnlyList<Kumunita.Core.Pages.PageTranslation> Translations,
    IReadOnlyList<LanguageOption> Languages,
    bool IsAuthor,
    bool IsDraft,
    bool CanEdit,
    string? AuthorDisplayName,
    string? CommunityDisplayName,
    /// <summary>
    /// The ADR 0029 "add a translation" affordance flag (the display pin —
    /// <see cref="Kumunita.Core.Pages.PageService.CanTranslatePage"/>, the
    /// same rule the <c>AddTranslationAsync</c> write-lane gate re-checks
    /// server-side): a GlobalAdmin / a Translator may add a translation of
    /// any page; a community Moderator only of a page scoped to their
    /// community (a flat/public page has no community to moderate). <c>true</c>
    /// ⇒ the post view renders the "add a translation" form (gated on this
    /// flag), the candidate list drawn from <see cref="Languages"/> excluding
    /// the authored-in language + any already-translated code (the ADR 0027
    /// "candidate list excludes the item's own language" rule).
    /// </summary>
    bool CanTranslate);

/// <summary>
/// The composer form-bound model — <c>GET/POST /pages/new</c> and
/// <c>GET/POST /pages/{id}/edit</c> (with <see cref="PageId"/> set on the
/// edit lane). Title + WYSIWYG body + parent picker + the
/// <see cref="AudienceEditorModel"/> verbatim + the
/// <see cref="IsPublic"/> null-audience flag + the ADR 0018 language picker
/// + an optional mount point. Reuses the <see cref="AudienceEditorModel"/>
/// and the same WYSIWYG binding as posts/announcements (the lane's "one
/// editor, one renderer" pin) — no second editor, no second audience shape
/// (the M2 U11 / ADR 0036 single-source pin applies verbatim: the
/// <see cref="Audience"/> editor's hidden <c>Grants</c> textarea is the only
/// form-bound grant field; <see cref="AudienceEditorModel.BuildAudience"/>
/// is the only deserialization site).
/// <para>
/// <b>Slug (ADR 0039 §3.2):</b> the derived path is built from the
/// <c>(ParentId, Slug)</c> chain, so a new page needs a slug. The composer
/// keeps the closed set the U04 plan specifies (title / body / parent /
/// audience / language — no slug field) and the controller
/// <b>slugifies the title</b> server-side (a display-label → path-segment
/// derivation; an explicit slug field would be a second, hand-maintained
/// naming surface the plan does not call for). The edit lane carries the
/// stored slug (read-only, not re-posted — reparenting + re-slug is the
/// <c>POST /pages/{id}/move</c> lane, not the editor).
/// </para>
/// </summary>
public sealed class PageComposeViewModel
{
    /// <summary>The page id (set only for the edit lane; null for create).
    /// The edit form posts to <c>/pages/</c> + this value + <c>/edit</c>.</summary>
    public string? PageId { get; set; }

    public string? Title { get; set; }

    /// <summary>The WYSIWYG body (Markdown) — the single
    /// <c>textarea[data-rich-editor]</c> the shared
    /// <c>bindRichEditor</c> wires. The client never sends the image /
    /// attachment ids; the controller parses them server-side from the body
    /// (<c>ContentImageIds.ExtractContentImageIds</c> /
    /// <c>AttachmentIds.ExtractAttachmentIds</c>) before calling the write
    /// lane (the U03 invariant: Core normalizes <c>?? []</c>, never parses
    /// the body).</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>The parent page this page nests under (a <c>Page</c> id) —
    /// <c>null</c>/empty = a root node (the forest shape, ADR 0039 §3.3).
    /// Binds from a <c>&lt;select&gt;</c> of <see cref="ParentPages"/>.</summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// The <b>public</b> toggle (the ADR 0039 "pages default public" shape —
    /// the one place pages differ from posts). <c>true</c> ⇒ the page is
    /// written with <see cref="Kumunita.Core.Pages.Page.Audience"/> <c>null</c>
    /// (world-readable, unauthenticated included) and
    /// <see cref="Kumunita.Core.Pages.Page.ComponentId"/> <c>null</c>;
    /// <c>false</c> ⇒ the <see cref="Audience"/> editor +
    /// <see cref="CommunityId"/> are stored verbatim. Round-trips from a
    /// stored page as <c>page.Audience is null</c>. Defaulted
    /// <c>true</c> — a fresh page is public (the lane's default), matching
    /// the seeded <c>about</c>/<c>terms</c>/<c>help</c> pages.
    /// </summary>
    public bool IsPublic { get; set; } = true;

    /// <summary>
    /// The page's <b>audience</b> editor — the M2 reusable
    /// <see cref="AudienceEditorModel"/> (the
    /// <see cref="Kumunita.Core.Authorization.Audience"/> form-bound shape),
    /// reused verbatim (the "one audience, one binder" pin). Inert (not
    /// stored) while <see cref="IsPublic"/> is <c>true</c> — a public page
    /// has a <c>null</c> audience; consulted only when <see cref="IsPublic"/>
    /// is <c>false</c>.
    /// </summary>
    public AudienceEditorModel Audience { get; set; } = new();

    /// <summary>
    /// The community this page is scoped to (<see cref="Component"/> id) —
    /// the target of the audience's <c>Community</c> flag (all members of
    /// that component may read it) and the scoping key a community
    /// <c>Moderator</c>'s create/edit/move/delete standing checks against
    /// (<see cref="Kumunita.Core.Identity.Roles.ModeratorComponent"/>, ADR
    /// 0039 §3.7). A <c>null</c>/empty value is a flat/public page (no
    /// community to moderate). Binds from a <c>&lt;select&gt;</c> of
    /// <see cref="Components"/>. Inert while <see cref="IsPublic"/> is
    /// <c>true</c> (a public page has no component scope).
    /// </summary>
    public string? CommunityId { get; set; }

    /// <summary>
    /// The UI slot this page is mounted at (ADR 0039 §3.8 — e.g.
    /// <c>"footer/community"</c>, <c>"help/account"</c>) — a <b>display</b>
    /// concern (where to surface a link), <b>not</b> an access boundary. A
    /// free-text field (the slot names are an open set); empty = not
    /// mounted. The layout's mount-point resolver reads this (the U04
    /// deliverable).
    /// </summary>
    public string? MountPoint { get; set; }

    /// <summary>
    /// The page's <b>authored-in language</b> (ADR 0018) — the BCP-47 code
    /// the author is writing this page in. A form-bound <c>&lt;select&gt;</c>
    /// posting <see cref="LanguageCode"/>; empty/unset is materialized from
    /// the instance default server-side at write time (the announcement /
    /// post composer idiom). Editable on the edit lane (ADR 0018).
    /// </summary>
    public string? LanguageCode { get; set; }

    /// <summary>The compose form's language *picker* options — the instance's
    /// **enabled** language catalog, ordered by <c>SortOrder</c>, reseeded by
    /// the controller on every render. <b>[BindNever]</b> — the form POSTs a
    /// <see cref="LanguageCode"/>, not a catalog-list shape.</summary>
    [BindNever]
    public IReadOnlyList<(string Code, string NativeName)> Languages { get; set; } = [];

    /// <summary>The parent *picker* options — the caller-visible page set the
    /// new page may nest under (an empty candidate set is a valid shape: the
    /// form offers a "Top level" root option). <b>[BindNever]</b> — the form
    /// POSTs a <see cref="ParentId"/>, not a page-list shape.</summary>
    [BindNever]
    public IReadOnlyList<(string Id, string Label)> ParentPages { get; set; } = [];

    /// <summary>The community *picker* options — the enabled
    /// <see cref="Kumunita.Core.UserInfo.Component"/> set (a plain
    /// resident sees their reachable communities; a GlobalAdmin sees every
    /// enabled one) the <see cref="CommunityId"/> <c>&lt;select&gt;</c>
    /// renders. <b>[BindNever]</b> — the form POSTs a
    /// <see cref="CommunityId"/>, not a component-list shape.</summary>
    [BindNever]
    public IReadOnlyList<(string Id, string Name)> Components { get; set; } = [];

    /// <summary>
    /// The composer's shape is well-formed for a <c>POST</c>. <see
    /// cref="Title"/> must be non-empty (a page with no title cannot derive
    /// a slug — the ADR 0039 §3.2 path is built from the title-slug). <see
    /// cref="Body"/> is optional (a folder node may have no body). When
    /// <see cref="IsPublic"/> is <c>false</c> the <see cref="Audience"/>
    /// editor must be well-formed (<see cref="AudienceEditorModel.IsValid"/>).
    /// </summary>
    public bool IsValid
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Title)) return false;
            if (!IsPublic && (Audience is null || !Audience.IsValid)) return false;
            return true;
        }
    }
}
