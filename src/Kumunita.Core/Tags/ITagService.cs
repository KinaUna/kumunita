using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
using Marten;

namespace Kumunita.Core.Tags;

/// <summary>
/// The single feature service for the <c>TG</c> lane (ADR 0006-D: the service
/// owns the decision, not the Web). Composes **only** the frozen modules —
/// <see cref="Kumunita.Core.Authorization.IAuthorizationService"/> (the
/// content's existing <c>Read</c> decision, never a new branch), the frozen
/// <see cref="Kumunita.Core.UserInfo.IUserInfoService"/> read seams, and
/// <see cref="Kumunita.Core.Localization.ITranslationProvider"/> (display-name
/// resolution, ADR 0005) — plus its own <c>IDocumentStore</c> for the write
/// lanes' C3 audit rows. Never opens a new seam on any frozen interface
/// (the ADR 0006-D lane pin — the tag lane composes only the frozen seams,
/// never a new <c>AccessVia</c> value beyond <c>Owner</c> / <c>Admin</c>).
/// </summary>
public interface ITagService
{
    // ── Write lane (C-TG·9: one AccessAudit row per write; C-TG·5 standing) ──

    /// <summary>
    /// Attach / detach the <paramref name="slugs"/> set on a post in the
    /// caller's in-flight session (C3). Standing = the post's existing edit
    /// lane (ADR 0014 / 0016 author-only; ADR 0013 group lane — D4). A
    /// not-yet-existing <c>Slug</c> is created: the actor becomes
    /// <c>CreatedBy</c>, the typed string (lowercased + trimmed, C-TG·4) the
    /// base <c>Name</c>. Writes one <c>tag.attach</c> row (and one
    /// <c>tag.create</c> row per newly created tag) — nothing else.
    /// </summary>
    Task<IReadOnlyList<Tag>> AttachToPostAsync(
        string postId, IReadOnlyList<string> slugs,
        string actorId, IReadOnlySet<string> roles, IDocumentSession session);

    /// <summary>
    /// Same shape for a <c>PageKind.User</c> blog page (the ADR 0040 author ∪
    /// GlobalAdmin standing). A <c>PageKind.System</c> page with a
    /// non-empty <paramref name="slugs"/> set is **refused**
    /// (<c>ArgumentException</c>) — C-TG·6, D6.
    /// </summary>
    Task<IReadOnlyList<Tag>> AttachToPageAsync(
        string pageId, IReadOnlyList<string> slugs,
        string actorId, IReadOnlySet<string> roles, IDocumentSession session);

    /// <summary>
    /// Add / overwrite the <paramref name="languageCode"/> translation row of
    /// tag <paramref name="tagId"/> (the <c>(TagId, LanguageCode)</c>
    /// business key — overwrites on conflict). Standing: the tag's
    /// <c>CreatedBy</c> ∪ GlobalAdmin only (C-TG·5, D4). One
    /// <c>tagtranslation.add</c> row, <c>Via</c> = <c>Owner</c> / <c>Admin</c>.
    /// </summary>
    Task<TagTranslation> AddTagTranslationAsync(
        string tagId, string languageCode, string name,
        string actorId, IReadOnlySet<string> roles, IDocumentSession session);

    // ── Read lane (C-TG·8: plain reads, no audit row; C-TG·2 base query) ──
    // U6 implements these; U5 declares them (the §2.1 11-member pin).

    /// <summary>
    /// The tag list (F12 empty on a fresh instance): distinct
    /// <see cref="Tag"/> rows used on ≥ 1 post / <c>PageKind.User</c> page
    /// the actor may already read (the C-TG·2 base query), each with a
    /// use-count and the display name resolved to the actor's language
    /// (ADR 0005 preference order, D5). No <c>AccessAudit</c> row (C-TG·8).

    /// </summary>
    Task<IReadOnlyList<TagItem>> ListForActorAsync(string actorId);

    /// <summary>
    /// The by-tag post results, **paged** (ADR 0090 D6, M7 U01 — the design
    /// doc §7.6 lock): the actor-readable posts (F3 / F4) whose <c>TagIds</c>
    /// contains the tag resolved from <paramref name="slug"/>, in
    /// <c>Created</c> ascending order — the post's own <c>Read</c> decision
    /// is applied **before** the post is returned (C-TG·3, D5) — then a
    /// <c>Skip((page - 1) * PageSize).Take(PageSize)</c> window
    /// (<c>PageSize = 30</c> — the D4 shape).
    /// <see cref="TagPostPage.HasMore"/> is the sole paging signal (D1 —
    /// <c>pageCount == PageSize</c>); an out-of-range page returns an empty
    /// page with <c>HasMore: false</c>. **No** <c>AccessAudit</c> row
    /// (C-TG·8 — the tag lane is a plain read). See
    /// <see cref="TagService.ListPostsByTagPagedAsync"/> for the full
    /// contract.
    /// </summary>
    Task<TagPostPage> ListPostsByTagPagedAsync(string slug, string actorId, int page, CancellationToken ct = default);

    /// <summary>
    /// The by-tag blog-page results, **paged** (ADR 0090 D6, M7 U01 — the
    /// design doc §7.6 lock): the actor-readable <c>PageKind.User</c> pages
    /// whose <c>TagIds</c> contains the tag, in <c>Created</c> ascending
    /// order, then a <c>Skip((page - 1) * PageSize).Take(PageSize)</c> window
    /// (<c>PageSize = 30</c> — the D4 shape).
    /// <see cref="TagPagePage.HasMore"/> is the sole paging signal (D1 —
    /// <c>pageCount == PageSize</c>); an out-of-range page returns an empty
    /// page with <c>HasMore: false</c>. **No** <c>AccessAudit</c> row
    /// (C-TG·8 — the tag lane is a plain read). See
    /// <see cref="TagService.ListPagesByTagPagedAsync"/> for the full
    /// contract.
    /// </summary>
    Task<TagPagePage> ListPagesByTagPagedAsync(string slug, string actorId, int page, CancellationToken ct = default);

    /// <summary>
    /// Autocomplete (F9 / F10): the C-TG·2 base query filtered by
    /// <c>starts_with(displayName, prefix) OR starts_with(slug, prefix)</c>,
    /// where <c>displayName</c> is the name resolved in the viewer's language;
    /// **capped at ≤ 10**. No <c>AccessAudit</c> row (C-TG·8).
    /// </summary>
    Task<IReadOnlyList<TagItem>> SuggestAsync(string prefix, string actorId);

    // ── Standing probes (C-TG·5; a display pin, not a gate — the real deny
    //    is the write-lane re-check, the <c>PostService.CanAddTranslation</c>
    //    / <c>PageService.CanTranslatePage</c> idiom) ──

    /// <summary>
    /// True if the actor is the post's author or a GlobalAdmin
    /// (the ADR 0014 / 0016 community lane ∪ the ADR 0013 group lane —
    /// author ∪ GlobalAdmin, no component-moderator standing, ADR 0007).
    /// A display pin (the Web renders the affordance); the real deny is the
    /// <see cref="AttachToPostAsync"/> standing re-check.
    /// </summary>
    bool CanAttachToPost(Post post, string actorId, IReadOnlySet<string> roles);

    /// <summary>
    /// False for a <c>PageKind.System</c> page (C-TG·6, D6). Otherwise true
    /// if the actor is the page's author or a GlobalAdmin (the ADR 0040
    /// lane). A display pin; the real deny is the
    /// <see cref="AttachToPageAsync"/> standing re-check.
    /// </summary>
    bool CanAttachToPage(Page page, string actorId, IReadOnlySet<string> roles);

    /// <summary>
    /// True if the actor is the tag's <c>CreatedBy</c> or a GlobalAdmin
    /// (C-TG·5, D4 — the name is the creator's artifact, the ADR 0009 / 0026
    /// rule carried to tags). A display pin; the real deny is the
    /// <see cref="AddTagTranslationAsync"/> standing re-check.
    /// </summary>
    bool CanTranslateTag(Tag tag, string actorId, IReadOnlySet<string> roles);
}

/// <summary>
/// The read-lane result row (the tag list / autocomplete shape, D5): the tag,
/// its use-count over the actor's readable content, and the display name
/// resolved to the actor's language (the ADR 0005 preference order).
/// U6's read-lane methods return <see cref="IReadOnlyList{T}">IReadOnlyList</see>
/// of this type; U5 declares it (the §2.1 11-member pin — the record type
/// must exist in U5 because the interface is the single declaration site).
/// </summary>
public sealed record TagItem(Tag Tag, int UseCount, string DisplayedName);
