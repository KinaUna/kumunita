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
}

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
    bool CanTranslate);
