namespace Kumunita.Core.Pages;

/// <summary>
/// A **page** — the body/structure node of the platform's knowledge tree
/// (ADR 0039; the Pages lane `PG`, which absorbed the static-page lane ADR
/// 0005 A — the legacy per-slug static-page doc was retired in U07, and this
/// <see cref="Page"/> is now the single source). A page is one node in a
/// **forest**: nested by <see cref="ParentId"/> (null = a root node),
/// identified within its parent by <see cref="Slug"/> — the
/// <c>(ParentId, Slug)</c> unique index (<see cref="PageDocTypes.Configure"/>)
/// enforces one page per slug per parent (the
/// <see cref="UserInfo.GroupMembership"/> business-key convention). The full
/// path is **derived** (the chain of ancestor slugs — `/pages/a/b/c`), not
/// stored, so a subtree moves in one column write.
/// <para>
/// A page is either a *folder* (has children, <see cref="Body"/> may be
/// empty), a *leaf* (has a body), or both ("folder-with-index") — "folder"
/// is not a type, it is "has ≥1 child".
/// </para>
/// <para>
/// **Audience (reused verbatim, ADR 0001-B / 0036):** <see cref="Audience"/>
/// is **exactly** a post's <see cref="Posts.Post.Audience"/> —
/// <b>not</b> a new scope. <c>null</c> = **public** (everyone, including
/// unauthenticated — the frozen <c>Decide()</c> branch 5); non-null =
/// <c>Mode</c> + <c>Grants</c> (users/groups) + the <c>Community</c> flag.
/// Pages **default public** (<c>Audience = null</c>) — the one place pages
/// deliberately differ from posts (ADR 0036 seeds posts *community-visible*
/// by default; a page is meant to be *read*, so the author narrows when they
/// want private). ADR 0039.
/// </para>
/// <para>
/// **Translatable UGC (ADR 0018 authored-in tag; ADR 0022/0026/0029 row
/// shape):** the authored-in body is stored once, tagged with
/// <see cref="LanguageCode"/>; user-added translations are separate
/// <see cref="PageTranslation"/> rows (never machine-translated — ADR 0005 C
/// stands). Display is the ADR 0027 chip-swap, reused verbatim.
/// </para>
/// <para>
/// **Authorization (ADR 0039):** the decision path is the **frozen**
/// <see cref="Authorization.IAuthorizationService.CanAsync"/> /
/// <see cref="Authorization.IAuthorizationService.CanSeeAsync"/> through a
/// <c>PageToAuditableResource</c> adapter (U02 — mirrors
/// <c>PostToAuditableResource</c>: <c>Id</c>/<c>Name</c>/<c>OwnerId =
/// AuthorId</c>/<c>Audience</c> (null allowed)/<c>ComponentId</c>/<c>
/// TargetKind = "page"</c>). No new <c>AccessAction</c>, no new
/// <c>AccessVia</c>, no new branch — <c>PG</c> adds an *adapter*, not a
/// *branch*.
/// </para>
/// <para>
/// **Draft + delete (ADR 0037 idiom; ADR 0024 soft-delete shape):**
/// <see cref="IsDraft"/> is the author-only draft pin (a draft is invisible
/// to everyone except its author until <c>PublishAsync</c> — U03);
/// <see cref="IsDeleted"/> is the soft-delete flag (U03 sets it; U02's read
/// lanes filter it out via <c>CanSeeAsync</c>) — an accidental delete of a
/// seeded page is recoverable and a page's children are not orphaned
/// mid-tree.
/// </para>
/// <para>
/// **Mount points (ADR 0039):** <see cref="MountPoint"/> is a nullable
/// string tag naming *where a page appears in the UI* (e.g.
/// <c>"footer/community"</c> for the about page, <c>"help/account"</c> for
/// the change-password help page). It is a **display** concern — a UI slot
/// resolves "the page whose <c>MountPoint == slot</c>" and renders/links it.
/// It is **not** an access boundary: access is always
/// <see cref="Audience"/> + <c>CanAsync(Read)</c>.
/// </para>
/// </summary>
public sealed class Page
{
    public string Id { get; set; } = string.Empty;

    // Hierarchy (the "folders and files" shape — ADR 0039 §3.3).
    /// <summary>
    /// The parent page this page is nested under. <c>null</c> = a root node
    /// (the tree is a **forest** — any number of roots: <c>about</c>,
    /// <c>help</c>, <c>blog</c>, …; there is no single mandatory root).
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// The page's path segment, **unique per parent** (the
    /// <c>(ParentId, Slug)</c> unique index, <see cref="PageDocTypes
    /// .Configure"/>). The full path is derived from the ancestor chain, not
    /// stored.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>The authored-in title (the display label).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The authored-in body (Markdown) — rendered by the **single**
    /// <c>MarkdownRenderer</c> (ADR 0025) and edited by the **single**
    /// <c>bindRichEditor</c> (ADR 0031). May be empty on a folder node.
    /// </summary>
    public string Body { get; set; } = string.Empty;

    // Audience (ADR 0001-B / 0036 — REUSED verbatim, not a new scope; ADR 0039).
    /// <summary>
    /// The page's audience — **exactly** a post's
    /// <see cref="Posts.Post.Audience"/> (ADR 0001-B / 0036), **not** a new
    /// scope. <c>null</c> = **public** (everyone, including unauthenticated —
    /// the frozen <c>Decide()</c> branch 5); non-null = an explicit grant
    /// list (<c>Mode</c> + <c>Grants</c>) and/or the <c>Community</c> flag
    /// (all members of the target component, <see cref="ComponentId"/>).
    /// Pages **default public** (<c>null</c>) — the one place pages differ
    /// from posts (ADR 0036 defaults posts to community-visible).
    /// </summary>
    public Authorization.Audience? Audience { get; set; }

    // Authorship + moderation scoping (ADR 0039 §3.7 standing matrix).
    /// <summary>
    /// The subject id of the author (the <c>Owner</c> branch of the frozen
    /// <c>Decide()</c> — edit/publish standing; ADR 0039 §3.7).
    /// </summary>
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>
    /// The community a <see cref="Authorization.AccessVia.Moderator"/> is
    /// scoped to (a <see cref="UserInfo.Component"/> by id) — the
    /// community-scope standing branches (ADR 0036's <c>Community</c>
    /// audience flag + the ADR 0029 translation lane, carried over). A
    /// flat/public page has no community to moderate, so a non-null
    /// <c>Community</c> audience must have this set.
    /// </summary>
    public string? ComponentId { get; set; }

    /// <summary>
    /// The BCP-47 code of the language this page was **authored in** (ADR
    /// 0018 authored-in tag; the <see cref="Posts.PostTranslation"/> /
    /// <see cref="Announcements.AnnouncementTranslation"/> lane's base
    /// language). A <see cref="PageTranslation"/> row's
    /// <see cref="PageTranslation.LanguageCode"/> is the language it
    /// translates *into* — a distinct concept.
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    // Mount point (ADR 0039 §3.3/§3.8).
    /// <summary>
    /// The UI slot this page is mounted at (e.g. <c>"footer/community"</c>,
    /// <c>"help/account"</c>) — a **display** concern (where to surface a
    /// link), **not** an access boundary (access is always
    /// <see cref="Audience"/> + <c>CanAsync(Read)</c>).
    /// </summary>
    public string? MountPoint { get; set; }

    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Modified { get; set; }

    // Draft idiom (ADR 0037, reused — the author-only pin).
    /// <summary>
    /// True while this page is an unsaved draft (ADR 0037, the
    /// <see cref="Announcements.Announcement.IsDraft"/> idiom reused): a
    /// draft is **invisible to everyone except its author** — the
    /// <see cref="Audience"/> decision is bypassed entirely. Set to
    /// <c>false</c> by <c>PublishAsync</c> (U03) when the author is ready to
    /// share. Default <c>false</c>.
    /// </summary>
    public bool IsDraft { get; set; } = false;

    // Soft-delete flag (ADR 0024 author-lane shape; ADR 0039 §3.7 — declared
    // now so the doc shape is final at U01; U03's delete lane sets it and
    // U02's read lanes filter it out via CanSeeAsync).
    /// <summary>
    /// True after the author delete lane (U03, the ADR 0024 soft-delete shape)
    /// hides the page: the read lanes (U02's <c>GetTreeAsync</c> /
    /// <c>GetByPathAsync</c> / <c>GetByMountPointAsync</c>) exclude
    /// <c>IsDeleted</c> pages (the <c>CanSeeAsync</c> filter) — an
    /// accidental delete of a seeded page is recoverable and the page's
    /// children are not orphaned mid-tree (ADR 0039 §3.7).
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    // RC content-image + ATT attachment idiom (ADR 0025 / 0034), reused.
    /// <summary>
    /// The content images referenced by <see cref="Body"/> — the
    /// <c>MediaObject</c> ids appearing as <c>/content-image/{id}</c> links
    /// in the rendered body (the RC ADR 0025 idiom, byte-store +
    /// reverse-lookup serving reused). Populated server-side by the owning
    /// write lane (U03); the client never sends them.
    /// </summary>
    public IReadOnlyList<string> ImageIds { get; set; } = [];
    /// <summary>
    /// The attachment file ids referenced by <see cref="Body"/> — the
    /// <c>MediaObject</c> ids appearing as <c>/attachment/{id}</c> links in
    /// the rendered body (the ATT ADR 0034 idiom). Populated server-side by
    /// the owning write lane (U03); the client never sends them. Separate
    /// from <see cref="ImageIds"/> (a page's images stay in
    /// <see cref="ImageIds"/>, its files in <see cref="AttachmentIds"/>).
    /// </summary>
    public IReadOnlyList<string> AttachmentIds { get; set; } = [];
}
