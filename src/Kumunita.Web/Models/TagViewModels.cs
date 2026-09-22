using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
using Kumunita.Core.Tags;

namespace Kumunita.Web.Models;

/// <summary>
/// The <c>TG</c> lane's Web surface view models (ADR 0044 D5; the
/// <see cref="Kumunita.Core.Tags.ITagService"/> read lane's projection —
/// the design doc §2.1 <c>TagListViewModel</c> / <c>TagByTagViewModel</c>
/// / <c>TagSuggestViewModel</c> shapes, verbatim).
/// <para>
/// **Read-only by construction (C-TG·1 / C-TG·8):** every row here was
/// returned by the access-scoped read seam
/// (<see cref="Kumunita.Core.Tags.ITagService.ListForActorAsync"/> /
/// <see cref="Kumunita.Core.Tags.ITagService.ListPostsByTagAsync"/> /
/// <see cref="Kumunita.Core.Tags.ITagService.ListPagesByTagAsync"/> /
/// <see cref="Kumunita.Core.Tags.ITagService.SuggestAsync"/>) — the
/// content's own <c>Read</c> decision already ran in Core (the ADR 0035
/// <c>PostReadDecision</c> routing), so a denied post's or page's fields
/// never reach these models. The tag grants nothing (C-TG·1); the read
/// emits no <c>AccessAudit</c> row of its own (C-TG·8). The controller does
/// **no** re-check and the view renders **only** what the seam returned —
/// no blanking, no "hidden" sentinel (the M3
/// <see cref="Kumunita.Web.Models.FeedViewModel"/> analog).
/// </para>
/// </summary>

/// <summary>
/// The <c>GET /tags</c> tag list (F12 — empty on a fresh instance,
/// C-TG·7): the <see cref="Kumunita.Core.Tags.TagItem"/> rows (the tag,
/// its use-count over the actor's readable content, and the display name
/// resolved to the actor's language — ADR 0005 preference order, D5), in
/// the seam's order (display name, then <c>Slug</c>).
/// </summary>
public sealed class TagListViewModel
{
    /// <summary>The readable tags (name + use-count, viewer language).</summary>
    public IReadOnlyList<TagItem> Tags { get; set; } = [];
}

/// <summary>
/// The <c>GET /tags/{slug}</c> by-tag view: the actor-readable
/// <see cref="Post"/>s (<c>Posts</c>) and <c>PageKind.User</c> blog
/// pages (<c>Pages</c>) whose <c>TagIds</c> contains the tag resolved from
/// the slug — each row links to its own detail route (a group post to
/// <c>/groups/{groupId}/posts/{id}</c>, a community post to
/// <c>/posts/{id}</c>, a blog page to its derived <c>/pages/…</c> path
/// via <see cref="Kumunita.Web.Security.PagePaths"/>). <see cref="PageById"/>
/// is the id → page map the view uses to render those
/// <c>/pages/…</c> hrefs (the derived ancestor-slug chain, ADR 0039 §3.3) —
/// a naming convenience, never an access surface.
/// <para>
/// **404-floor (the register's U7 pin):** when the slug is unknown, or the
/// tag is used only on content this viewer cannot read, both lists are
/// empty — the <see cref="Kumunita.Web.Controllers.TagController.ByTag"/>
/// action maps that shape to a 404 *before* this model is rendered (the
/// view never receives an empty by-tag model; the empty state is a
/// controller response, the M3 "403 on denied, not a blank page" analog —
/// here a 404, because a tag behind unread content must not confirm its
/// existence, C-TG·1 / C-TG·2).
/// </para>
/// </summary>
public sealed class TagByTagViewModel
{
    /// <summary>The slug the view was requested for (the <c>Slug</c>
    /// business key, C-TG·4).</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>The tag's display name resolved in the viewer's language
    /// (the ADR 0005 preference order — the
    /// <see cref="Kumunita.Core.Tags.ITagService.ListForActorAsync"/> row's
    /// <see cref="Kumunita.Core.Tags.TagItem.DisplayedName"/>, falling back
    /// to the slug when the tag has no readable use).</summary>
    public string DisplayedName { get; set; } = string.Empty;

    /// <summary>The actor-readable posts tagged with it (community + group;
    /// each row carries its own <c>GroupId</c> for the detail link).</summary>
    public IReadOnlyList<Post> Posts { get; set; } = [];

    /// <summary>The actor-readable <c>PageKind.User</c> blog pages tagged
    /// with it (a <c>PageKind.System</c> page can never appear here —
    /// its <c>TagIds</c> are always empty, C-TG·6).</summary>
    public IReadOnlyList<Page> Pages { get; set; } = [];

    /// <summary>The id → <see cref="Page"/> map for
    /// <see cref="Kumunita.Web.Security.PagePaths.Href"/> (the derived
    /// <c>/pages/…</c> paths; the full non-deleted tree, so an ancestor
    /// chain is always resolvable — the <see cref="BlogController"/>
    /// precedent).</summary>
    public IReadOnlyDictionary<string, Page> PageById { get; set; }
        = new Dictionary<string, Page>(StringComparer.Ordinal);

    /// <summary>The U8c reword surface (ADR 0044 D4, C-TG·5): the tag's
    /// per-language display name, pre-filled with the current translation
    /// (or the base name) and editable only when <c>CanTranslate</c> is
    /// true (the tag's <see cref="Kumunita.Core.Tags.Tag.CreatedBy"/> ∪
    /// GlobalAdmin — the C-TG·5 standing split; a non-creator attacher sees
    /// the form rendered **disabled** and a POST to
    /// <c>POST /tags/{slug}/translate</c> is a 403 from the service's
    /// standing re-check, C3). The register's U8c "no new seam" pin holds
    /// (the seam is exactly what U5's <c>AddTagTranslationAsync</c> already
    /// froze; this is a web-wiring unit, not a seam change). <c>null</c>
    /// when the tag is not readable to this actor (the 404-floor shape —
    /// the controller returns a <see cref="Microsoft.AspNetCore.Mvc.NotFoundResult"/>
    /// before this model is rendered, so this property is only set when a
    /// readable tag exists).</summary>
    public TagTranslationForm? Translation { get; set; }
}

/// <summary>
/// The <c>POST /api/tags/suggest</c> autocomplete response (F9 / F10 — the
/// viewer-language display name + the ≤ 10 cap, both enforced by the
/// <see cref="Kumunita.Core.Tags.ITagService.SuggestAsync"/> seam): the
/// matched <see cref="Kumunita.Core.Tags.TagItem"/> rows, capped at 10.
/// The client (the <c>client/lib</c> tag-suggest dropdown, U8) renders
/// <see cref="Kumunita.Core.Tags.TagItem.DisplayedName"/> and submits the
/// <see cref="Kumunita.Core.Tags.Tag.Slug"/> (the write seam derives the
/// <c>Slug</c> from the typed string, C-TG·4).
/// </summary>
public sealed class TagSuggestViewModel
{
    /// <summary>The matched suggestions (≤ 10, the C-TG·2 cap).</summary>
    public IReadOnlyList<TagItem> Suggestions { get; set; } = [];
}

/// <summary>
/// One row of the <see cref="TagTranslationForm"/> (the U8c reword surface,
/// ADR 0044 D4, C-TG·5): a single enabled <see cref="LanguageCatalog"/>
/// row the actor may set (or overwrite) on the tag. <see cref="LanguageCode"/>
/// is the row's identity (the <c>(TagId, LanguageCode)</c> unique index);
/// <see cref="NativeName"/> is the label displayed in the form (the
/// language's own name — "English", "Deutsch", "Français"), and
/// <see cref="CurrentName"/> is the value pre-filled into the <c>&lt;input&gt;</c>
/// — the current <see cref="Kumunita.Core.Tags.TagTranslation.Name"/> for that
/// language when a translation row exists, otherwise the tag's base
/// <see cref="Kumunita.Core.Tags.Tag.Name"/> (the creator's own spelling —
/// the ADR 0005 preference order, the display fallback when no translation
/// is set). The row is a **form seed** (a dumb shape carrier); the standing
/// decision is the <see cref="Kumunita.Core.Tags.ITagService
/// .CanTranslateTag"/> probe (a display pin, the ADR 0009 / 0026 "the name
/// is the creator's artifact" rule carried to tags) and the real deny is
/// the <see cref="Kumunita.Core.Tags.ITagService
/// .AddTagTranslationAsync"/> standing re-check (C-TG·5, C3).
/// </summary>
public sealed class TagTranslationRow
{
    /// <summary>The BCP-47 language code (the <c>(TagId, LanguageCode)</c>
    /// unique index's language half — the row's identity, posted back to the
    /// controller as the <c>LanguageCode</c> field of the form).
    /// Read-only on the form (the identity is fixed by the catalog, not
    /// user-editable).</summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>The language's own name ("English", "Deutsch", "Français")
    /// — the row's label in the form. Read-only (a display value, not
    /// user-editable).</summary>
    public string NativeName { get; set; } = string.Empty;

    /// <summary>The value to pre-fill into the <c>&lt;input&gt;</c> — the
    /// current <see cref="Kumunita.Core.Tags.TagTranslation.Name"/> for this
    /// language when a translation row exists, otherwise the tag's base
    /// <see cref="Kumunita.Core.Tags.Tag.Name"/> (the creator's own
    /// spelling, the ADR 0005 preference-order fallback). The form posts
    /// this field back to the controller, which passes it verbatim to
    /// <see cref="Kumunita.Core.Tags.ITagService.AddTagTranslationAsync"/>
    /// (the C-TG·9 one-audit-row write).</summary>
    public string CurrentName { get; set; } = string.Empty;
}

/// <summary>
/// The U8c reword surface's form state (ADR 0044 D4, C-TG·5): the tag's
/// display name per enabled language, pre-filled with the current translation
/// (or the base name) and editable **only** when
/// <see cref="CanTranslate"/> is true (the tag's <c>CreatedBy</c> ∪
/// GlobalAdmin — the C-TG·5 standing split; a non-creator attacher sees the
/// form rendered **disabled** and a <c>POST</c> to the translate route is a
/// 403 from the service's standing re-check). The form is rendered on the
/// <c>ByTag</c> view (<c>Views/Tag/ByTag.cshtml</c>), one row per enabled
/// <see cref="LanguageCatalog"/> (the instance catalog, the ADR 0005 B
/// shape — the U7/U8 read lane already resolves the display name through
/// this same catalog, so no new catalog surface is opened). The POST lane is
/// <c>POST /tags/{slug}/translate</c> (the <see cref="Kumunita
/// .Web.Controllers.TagController"/> action), the single write seam — the
/// register's U8c "no new seam" pin (the seam is exactly what U5's
/// <c>AddTagTranslationAsync</c> already froze; this is a web-wiring unit,
/// not a seam change).
/// </summary>
public sealed class TagTranslationForm
{
    /// <summary>The tag's base <c>Slug</c> (C-TG·4 — the business key; the
    /// form's <c>POST</c> route is keyed on it).</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>The tag's base display name (the creator's own spelling —
    /// the ADR 0018 authored-in fallback the form uses for a language with
    /// no translation row).</summary>
    public string BaseName { get; set; } = string.Empty;

    /// <summary>The tag's authored-in <c>LanguageCode</c> (the ADR 0018
    /// authored-in idiom — the base name's language; display-only on the
    /// form, never a standing decision).</summary>
    public string BaseLanguageCode { get; set; } = string.Empty;

    /// <summary>The standing probe (C-TG·5, D4 — creator ∪ GlobalAdmin).
    /// <see langword="true"/> when the actor is the tag's <c>CreatedBy</c> or
    /// a GlobalAdmin (the form is rendered editable); <see langword="false"/>
    /// for a non-creator attacher (the form is rendered **disabled** — the
    /// view's affordance pin; the real deny is the POST lane's standing
    /// re-check, the <see cref="Kumunita.Core.Tags.ITagService
    /// .AddTagTranslationAsync"/> <c>UnauthorizedAccessException</c> →
    /// <c>403</c>). A display pin (the ADR 0009 / 0026 "the name is the
    /// creator's artifact" rule carried to tags), not a gate.</summary>
    public bool CanTranslate { get; set; }

    /// <summary>The per-language rows (one per enabled
    /// <see cref="LanguageCatalog"/>, sorted by <c>SortOrder</c> then
    /// <c>Id</c> — the U7/U8 catalog order convention). Each row's
    /// <c>CurrentName</c> is pre-filled with the current translation or the
    /// base name (the ADR 0005 preference order).</summary>
    public IReadOnlyList<TagTranslationRow> Rows { get; set; } = [];
}
