using Kumunita.Core.Posts;

namespace Kumunita.Web.Models;

/// <summary>
/// The post detail surface (M3, plan U7) — <c>/posts/{id}</c> + its
/// one-level reply list. A *projection* of
/// <c>PostService.GetPostAsync</c>'s <see cref="PostDetailResult"/> — the
/// single decision row (invariant C-M3·3; one <c>CanAsync</c> call per visit,
/// C6) has already been written at the Core layer, and the
/// <see cref="Replies"/> list is the **already-authorized** one-level set
/// returned *as-is* under the parent post's single <c>Read</c> decision
/// (invariant C-M3·1). The controller does **no** re-check on a reply (the
/// C-M3·1 "no second <c>Can*Async</c> on the reply" pin) and surfaces
/// <see cref="Post"/>/ <see cref="AuthorDisplayName"/>/ <see
/// cref="ReplyItem.AuthorDisplayName"/> as display names (each a
/// <see cref="Kumunita.Core.UserInfo.IUserInfoService.GetProfileAsync"/> read,
/// not a decision — the M2 <see cref="Kumunita.Web.Controllers.GroupsController"/>
/// display-name precedent).
/// <para>
/// A <b>Denied</b> or <b>missing</b> post is mapped by the controller to a
/// 403 / 404 (the §2.3 candidate-filter shape) before this model is built;
/// the view never receives a <see cref="PostDetailViewModel"/> for a post the
/// viewer may not read. The model itself therefore never carries a
/// "hidden" sentinel — the two fail-closed cases are controller responses,
/// not view-model states (the M2 <see cref="Kumunita.Web.Models.DirectoryViewModel.Detail"/>
/// §9 analog: the view has *no channel* to render a denied post).
/// </para>
/// </summary>
/// <summary>
/// One <b>supported language</b> on the detail surface (ADR 0022): a
/// <see cref="Kumunita.Core.Localization.LanguageCatalog"/> enabled row
/// (ordered by <c>SortOrder</c>) plus whether the item already
/// <b>has</b> a user-added translation into it. <see cref="HasTranslation"/>
/// true ⇒ the language renders as an "available" chip and offers the
/// translation's title/body for selection; false ⇒ the language is a
/// candidate for the "add a translation" lane (offered only when
/// <see cref="PostDetailViewModel.CanTranslate"/> is set).
/// </summary>
public sealed record LanguageOption(string Code, string NativeName, bool HasTranslation);

public sealed class PostDetailViewModel
{
    public Post Post { get; set; } = null!;
    public string AuthorDisplayName { get; set; } = string.Empty;

    /// <summary>The author's subject id (the <see cref="Post"/>'s
    /// <c>AuthorId</c>) — a display convenience: the author's avatar links the
    /// audited serving lane <c>GET /profile/avatar/{subjectId}</c> (the same
    /// "a read, not a decision" pin as <see cref="AuthorDisplayName"/>).</summary>
    public string AuthorSubjectId { get; set; } = string.Empty;
    public IReadOnlyList<ReplyItem> Replies { get; set; } = [];
    public bool IsAuthor { get; set; }

    // ── TG (ADR 0044) — the post's tags on the detail surface ──

    /// <summary>
    /// The post's **tags** (the <c>TG</c> lane, ADR 0044) as a
    /// <b>(Slug, DisplayedName)</b> projection for the detail surface — one
    /// row per id in <see cref="Post.TagIds"/> that resolves to a stored
    /// <c>Tag</c> (a dangling id is dropped silently — the tag was deleted
    /// out from under the post, and the C-TG·1 "labels, never a gate" pin
    /// says a tag grants nothing, so a broken reference renders as nothing,
    /// not a 404 or an error). A post with no tags renders an empty list
    /// (the detail page simply shows no tag row — the F12 "no tags yet"
    /// shape, the tag list's empty-list precedent carried to the post). The
    /// <c>Slug</c> is the link target (<c>/tags/{slug}</c>,
    /// the by-tag browse surface — discoverability); the <c>DisplayedName</c>
    /// is resolved in the **viewer's language** (the ADR 0005 preference
    /// order: the effective language → the <c>TagTranslation</c> for it → the
    /// base <c>Tag.Name</c>), the exact idiom the tag list's
    /// <c>BuildTagItemsAsync</c> uses. <b>[BindNever]</b>-equivalent: this is
    /// a projection on a projection model (the detail page is GET-only), so
    /// there is no form round-trip to guard. Populated by the
    /// <see cref="PostsController.Detail"/> / group-detail controllers from
    /// the post's <c>Tag</c> / <c>TagTranslation</c> docs (a read, not a
    /// decision — the post's single <c>Read</c> decision already ran in
    /// <c>GetPostAsync</c>, C-TG·1).
    /// </summary>
    public IReadOnlyList<(string Slug, string DisplayedName)> Tags { get; set; } = [];

    /// <summary>
    /// The post's **community's name** (the <see cref="Post"/>'s
    /// <c>ComponentId</c> resolved to its stored <c>Component.Name</c>),
    /// shown in the **viewer's language** when a user-added name translation
    /// exists for it (the ADR 0026 floor, exactly the feed's
    /// <c>ResolveCommunityNameAsync</c> idiom), feeding the "back to {community}"
    /// link's label on the detail page. A display pin, not a gate: the
    /// post's single <c>Read</c> decision already ran in <c>GetPostAsync</c>.
    /// The link's <b>target</b> still uses <see cref="Post"/>'s
    /// <c>ComponentId</c> — only the label changes. A display gap, not an
    /// error: when the component cannot be resolved the raw id is the
    /// fallback (the page still renders).
    /// </summary>
    public string CommunityDisplayName { get; set; } = string.Empty;

    // ── ADR 0022 — user-added post translations ──

    /// <summary>The post's user-added translations, keyed by their target
    /// language code (an <see cref="IReadOnlyList{Kumunita.Core.Posts.PostTranslation}"/>
    /// projection — a "a read, not a decision" surface; visibility already
    /// inherited the post's single <c>Read</c> decision).</summary>
    public IReadOnlyList<Kumunita.Core.Posts.PostTranslation> PostTranslations { get; set; } = [];

    /// <summary>Every enabled <see cref="LanguageCatalog"/> language (in
    /// <c>SortOrder</c>) with its <see cref="LanguageOption.HasTranslation"/>
    /// flag — the set the "available translations" chips and the
    /// "add a translation" candidate list render from.</summary>
    public IReadOnlyList<LanguageOption> Languages { get; set; } = [];

    /// <summary>Whether the signed-in actor holds standing to
    /// <b>add</b> a translation of this post (ADR 0022 — the author, a
    /// community moderator, or a GlobalAdmin). A display pin, not a gate: the
    /// real deny is the <c>PostService.AddPostTranslationAsync</c> standing
    /// check. When false, no "add a translation" affordance renders.</summary>
    public bool CanTranslate { get; set; }
    // ── TD lane (ADR 0027) — the authored-in language as a first-class variant ──

    /// <summary>The language the post was **authored in** (ADR 0018,
    /// <see cref="Post"/>'s <c>LanguageCode</c>) — carried **additively** on
    /// this model (TD·6: the shared <see cref="LanguageOption"/> record is
    /// untouched). TD·1: the detail surface renders this code as the
    /// **first, default-visible** variant chip, and TD·4: the "Add a …"
    /// candidate list excludes it. Populated by the detail controller from
    /// <c>Post.LanguageCode</c> — a read, never a write (TD·7: no Core or
    /// schema change).</summary>
    public string OriginalLanguageCode { get; set; } = string.Empty;}

/// <summary>
/// One visible reply row. The low-entropy projection: the
/// <see cref="Kumunita.Core.Posts.PostReply"/>'s <c>Id</c>, a
/// <see cref="AuthorDisplayName"/> (a <c>GetProfileAsync</c> read — the same
/// "a read, not a decision" pin as the parent post's), the reply's
/// <see cref="Kumunita.Core.Posts.PostReply.Body"/> (verbatim — no
/// truncation for the detail surface; the list's preview truncation is a
/// list-surface concern), and the <see cref="Kumunita.Core.Posts.PostReply.Created"/>
/// timestamp. No <c>Audience</c> of its own (C-M3·1: a reply has *no* own
/// audience field); a <c>Reply</c> inherits visibility from its parent
/// post's single <c>Read</c> decision, not from a second authorization call.
/// </summary>
public sealed record ReplyItem(
    string Id,
    string AuthorDisplayName,
    /// <summary>The replier's subject id (the <see cref="PostReply"/>'s
    /// <c>AuthorId</c>) — a display convenience: the reply's avatar links the
    /// audited serving lane <c>GET /profile/avatar/{subjectId}</c> (the same
    /// "a read, not a decision" pin as <see cref="AuthorDisplayName"/>).</summary>
    string AuthorSubjectId,
    string Body,
    DateTimeOffset Created,
    /// <summary>The reply's last edit time (ADR 0016) — a display pin, not a
    /// gate. <c>null</c> until the reply is first edited (the reply's
    /// <c>Created</c> is the initial timestamp); after an edit it carries the
    /// last-edit time and drives the "edited" badge on the row. Written only
    /// by the author-only <c>PostService.UpdateReplyAsync</c> lane, mirroring
    /// <c>Post.Modified</c>.</summary>
    DateTimeOffset? Modified,
    /// <summary>Whether the signed-in actor authored **this reply** (a
    /// display pin, not a gate — ADR 0016's reply-edit lane renders the
    /// per-reply "Edit" affordance on this row). Mirrors the parent
    /// <see cref="PostDetailViewModel.IsAuthor"/> / the group lane's
    /// <see cref="Kumunita.Web.Models.GroupViewModel.GroupPostDetailViewModel.IsAuthor"/>
    /// post-level analog; computed per-reply from the reply's own
    /// <c>AuthorId</c>, not the parent post's author.</summary>
    bool IsAuthor,
    /// <summary>The reply's user-added translations (ADR 0022; a "a read, not a
    /// decision" surface — inherits the reply's visibility from the parent
    /// post's single <c>Read</c> decision). Empty when none have been added.</summary>
    IReadOnlyList<Kumunita.Core.Posts.ReplyTranslation> Translations,
    /// <summary>Whether the signed-in actor holds standing to
    /// <b>add</b> a translation of this reply (ADR 0022 — the author, a
    /// community moderator, or a GlobalAdmin). A display pin, not a gate; the
    /// real deny is <c>PostService.AddReplyTranslationAsync</c>.</summary>
    bool CanTranslate,
    /// <summary>When the author soft-deleted this reply (ADR 0024); <c>null</c>
    /// while it is live. A display pin: the detail view renders a placeholder
    /// in place of the body when set, hides the per-reply Edit/translate
    /// affordances, but still counts the reply toward the parent's reply count.</summary>
    DateTimeOffset? DeletedAt,
    /// <summary>The language the reply was **authored in** (ADR 0018,
    /// <see cref="Kumunita.Core.Posts.PostReply"/>'s <c>LanguageCode</c>) —
    /// carried **additively** as a trailing positional (TD·6: the shared
    /// <see cref="LanguageOption"/> record is untouched). TD·1: the detail
    /// surface renders this code as the reply's **first, default-visible**
    /// variant chip, and TD·4: the reply's "Add a …" candidate list excludes
    /// it. Populated from <c>PostReply.LanguageCode</c> — a read, never a
    /// write (TD·7: no Core or schema change).</summary>
    string OriginalLanguageCode);
