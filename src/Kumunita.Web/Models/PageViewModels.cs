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
/// The <c>GET /blog/{userId}</c> per-user blog feed (ADR 0040) — the
/// resident's own <see cref="Kumunita.Core.Pages.Page"/> set
/// (<see cref="Kumunita.Core.Pages.PageKind"/> = <c>User</c>, authored by
/// that resident), newest-first. Each row links to the page's
/// <c>/pages/…</c> href (the same <see cref="Kumunita.Web.Security
/// .PagePaths"/> the tree browse uses). The feed is a <b>read-only
/// listing</b> — the write lanes (create/edit/move/delete) are the
/// <c>/pages/…</c> controller's, and the feed itself carries no standing
/// affordances (a resident's own feed is theirs; another resident's feed is
/// a read-only list of their public + community-visible pages).
/// </summary>
public sealed record BlogViewModel(
    string AuthorId,
    string? AuthorDisplayName,
    IReadOnlyList<BlogPostRow> Posts,
    /// <summary>
    /// Whether the acting caller is the <b>author</b> of this feed (the
    /// affordance pin — the "New page" / "New blog page" button is shown on
    /// your own feed; another resident's feed is read-only).
    /// </summary>
    bool IsOwner);

/// <summary>
/// One row of the <see cref="BlogViewModel"/> feed: the page's title, the
/// <c>/pages/…</c> href, and the last-modified stamp. A <b>draft</b> page
/// (ADR 0037) is <b>absent</b> from another resident's feed (a draft is its
/// author's) — the controller filters it; the <see cref="IsDraft"/> flag is
/// present only so the author's own feed can badge it.
/// </summary>
public sealed record BlogPostRow(
    string Id,
    string Title,
    string Href,
    DateTimeOffset Modified,
    bool IsDraft);

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
    bool CanTranslate,
    /// <summary>
    /// ADR 0049 — the viewer's resolved current-language BCP-47 code for this
    /// request (the same chain the <c>&lt;kw-l&gt;</c> TagHelper resolves UI
    /// text through: <c>kumunita.locale</c> cookie → <c>Accept-Language</c>
    /// match → instance default → <c>en</c>). The view uses this to pick
    /// which of the page's <see cref="Kumunita.Core.Pages.PageTranslation"/>
    /// variants is <b>default-visible</b>: the matching translation (if any)
    /// is shown first, the authored-in variant is demoted to a one-click
    /// chip swap (ADR 0027 machinery untouched). <c>null</c> when the
    /// per-request read seam is absent (e.g. a test harness) — the view
    /// degrades to the ADR 0027 floor (authored-in variant default-visible).
    /// </summary>
    string? DefaultVariant);

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

    /// <summary>
    /// The <b>page kind</b> (ADR 0040) the composer is authoring —
    /// <see cref="Kumunita.Core.Pages.PageKind.System"/> (a system/platform
    /// page under the <c>system/</c> root, GlobalAdmin-only) or
    /// <see cref="Kumunita.Core.Pages.PageKind.User"/> (a blog page under the
    /// actor's own <c>blog/{uid}</c> root). The form binds a
    /// <c>"System"</c>/<c>"User"</c> string; the controller maps it to the
    /// enum. Default <c>System</c> — a plain <c>/pages/new</c> is a system
    /// page (the ADR 0039 shape); the blog lane posts <c>Kind = "User"</c>
    /// with the actor's blog root as the parent.
    /// </summary>
    public string? Kind { get; set; } = "System";

    /// <summary>
    /// Whether the acting caller is a <b>GlobalAdmin</b> — drives the
    /// parent-picker differentiation in <c>_PageForm.cshtml</c> (ADR 0040):
    /// a GlobalAdmin sees <em>both</em> the system page set and their own
    /// blog root as nesting candidates; a plain resident sees only their own
    /// blog root. <b>[BindNever]</b> — the server derives it from the
    /// principal (never bound from the form, so it cannot be spoofed).
    /// </summary>
    [BindNever]
    public bool IsAdmin { get; set; }

    /// <summary>
    /// ADR 0058 — whether the edit lane may offer the <b>"Reset to seeded
    /// text"</b> button. <c>true</c> exactly when the page's <see
    /// cref="Kumunita.Core.Pages.Page.Slug"/> is one of the seeded platform
    /// pages / guides (<see cref="Kumunita.Core.Bootstrap.FirstBootSeeder
    /// .HasSeededText"/> — the code-owned seed registries carry an
    /// <c>en</c> baseline for it) — a page with no seeded baseline (a
    /// resident-authored page, or a slug the seed never wrote) has nothing
    /// to reset to, so the button is hidden. The standing (edit standing —
    /// the button's POST is 403-denied by the service for a non-qualifier)
    /// is already enforced on the edit lane itself, so by the time this view
    /// renders the actor has standing; <see cref="CanReset"/> only reflects
    /// the <em>seeded-text availability</em>. <b>[BindNever]</b> — the
    /// server derives it from the stored page (never bound from the form).
    /// </summary>
    [BindNever]
    public bool CanReset { get; set; }

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
    /// The <b>public</b> toggle. <c>true</c> ⇒ the page is written with
    /// <see cref="Kumunita.Core.Pages.Page.Audience"/> <c>null</c> (world-readable,
    /// unauthenticated included) and <see cref="Kumunita.Core.Pages.Page.ComponentId"/>
    /// <c>null</c>; <c>false</c> ⇒ the <see cref="Scope"/> dropdown +
    /// <see cref="Audience"/> editor are stored (the controller's
    /// <c>ResolveAudience</c> mapping, ADR 0041). Round-trips from a stored
    /// page as <c>page.Audience is null</c>. Defaulted <c>false</c> — a fresh
    /// page is **non-public** (all-residents by default, ADR 0041); an admin
    /// can still opt a page into the public capability. (The seeded
    /// <c>about</c>/<c>terms</c>/<c>help</c> pages are written public by the
    /// seeder, not by this default.)
    /// </summary>
    public bool IsPublic { get; set; } = false;

    /// <summary>
    /// The <b>"All residents"</b> scope (ADR 0041) — the page's
    /// <c>who-can-see-this</c> dropdown value meaning *every signed-in
    /// resident* (any authenticated reader, no community required), written
    /// as <c>Audience.AllResidents = true</c> + <c>ComponentId = null</c>
    /// (the new frozen <c>Decide()</c> resident branch). The <b>default</b>
    /// for a fresh composer, and the analog of the announcement's flat
    /// <c>Scope = Community, CommunityId = null</c> shape.
    /// </summary>
    public const string ScopeAllResidents = "AllResidents";

    /// <summary>
    /// The <b>"Individual access"</b> scope (ADR 0041) — the dropdown value
    /// that reveals the detailed <see cref="Audience"/> editor (explicit
    /// user/group grants). Any other non-blank <see cref="Scope"/> value is a
    /// <see cref="Kumunita.Core.UserInfo.Component"/> id (a specific
    /// community, written as <c>Audience.Community = true</c> + that
    /// <c>ComponentId</c>).
    /// </summary>
    public const string ScopeIndividual = "Individual";

    /// <summary>
    /// The <b>"who can see this"</b> scope (ADR 0041) — one of
    /// <see cref="ScopeAllResidents"/> (the default), <see
    /// cref="ScopeIndividual"/> (reveal the editor), or a
    /// <see cref="Kumunita.Core.UserInfo.Component"/> id (a specific
    /// community). Binds from the <c>&lt;select&gt;</c> the view renders from
    /// <see cref="Components"/>. Inert while <see cref="IsPublic"/> is
    /// <c>true</c> (a public page is world-readable, no scope).
    /// </summary>
    public string? Scope { get; set; } = ScopeAllResidents;

    /// <summary>
    /// Whether the <see cref="Scope"/> is <see cref="ScopeIndividual"/> (the
    /// view's pin for revealing the detailed <see cref="Audience"/> editor —
    /// it is shown only when this is <c>true</c>, matching the announcement
    /// lane's audience visibility rules). A <c>null</c>/"AllResidents" / community scope is <c>false</c>.
    /// </summary>
    public bool IsIndividualAccess =>
        string.Equals(Scope, ScopeIndividual, StringComparison.Ordinal);

    /// <summary>
    /// The page's <b>audience</b> editor — the M2 reusable
    /// <see cref="AudienceEditorModel"/> (the
    /// <see cref="Kumunita.Core.Authorization.Audience"/> form-bound shape),
    /// reused verbatim (the "one audience, one binder" pin). Consulted only
    /// when <see cref="IsIndividualAccess"/> is <c>true</c> (the
    /// "Individual access" scope); inert (not stored) while <see
    /// cref="IsPublic"/> is <c>true</c> or the scope is a community / All
    /// residents (the controller writes those scopes directly, not through
    /// the editor).
    /// </summary>
    public AudienceEditorModel Audience { get; set; } = new();

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
    /// The composer's <b>tag</b> input (the <c>TG</c> lane, ADR 0044 — U8b
    /// register patch). A <b>plain form-bound field</b> (no <see
    /// cref="BindNever"/>): the client (<c>client/lib/tag-suggest.ts</c>
    /// L106–L110) posts a hidden <c>name="TagIds"</c> field whose value is
    /// a <b>JSON array of label strings</b> (the author's typed tags — the
    /// <c>Slug</c> is derived server-side, C-TG·4). The controller re-parses
    /// + normalizes (trim / dedup / drop-blank) on the <c>POST</c> and writes
    /// the resolved <c>Slug</c>s onto <c>Page.TagIds</c> before calling
    /// <c>IPageService.CreateAsync</c> / <c>UpdateAsync</c> — the service's
    /// write lane then calls <c>TagService.AttachToPageAsync</c> on the
    /// same session (C3 single-transaction idiom). A <b>bad slug</b> is an
    /// <c>ArgumentException</c> from <c>TagService.DeriveSlug</c> (C-TG·4)
    /// — the controller maps it to a form error (the M3 "a form is a
    /// shape" precedent). <b>Empty / null</b> is the "no tags" state (the U4
    /// additive default-empty pin). The <see cref="Kumunita.Core.Pages
    /// .PageKind.System"/> composer <b>does not render the field</b> (the
    /// <c>_PageForm.cshtml</c> <c>@if (Model.Kind == "User")</c> gate, U8
    /// note (c), C-TG·6) — and the service's <c>AttachToPageAsync</c>
    /// write-lane refusal is the second guard (the U4 <c>PageService</c>
    /// create/update refusal + the U5 <c>AttachToPageAsync</c> System-page
    /// refusal — two refusals that agree, the write lane is the gate, the
    /// Web form is the shape).
    /// </summary>
    public string? TagIds { get; set; }

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
            // ADR 0041 — the editor is only consulted for the "Individual
            // access" scope (the other scopes are written directly by the
            // controller), so only that scope requires a well-formed editor.
            if (!IsPublic && IsIndividualAccess && (Audience is null || !Audience.IsValid))
                return false;
            return true;
        }
    }
}
