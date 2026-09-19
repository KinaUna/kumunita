using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
using Marten;

namespace Kumunita.Core.Tags;

/// <summary>
/// The <c>TG</c> lane's write service (U5: the attach / create / translate
/// lane + the three standing probes + the <see cref="DeriveSlug"/> helper).
/// <para>
/// **U5/U6 split pin:** the read-lane methods (<see cref="ListForActorAsync"/>,
/// <see cref="ListPostsByTagAsync"/>, <see cref="ListPagesByTagAsync"/>,
/// <see cref="SuggestAsync"/>) are declared on <see cref="ITagService"/>
/// (the §2.1 11-member contract) but **not yet implemented** here — the
/// <see cref="NotImplementedException"/> stubs are the U5→U6 boundary
/// (the register's U5/U6 split note: U5 declares the full interface +
/// implements the 6 write-lane members; U6 implements the 4 read-lane members).
/// </para>
/// <para>
/// **C3 idiom:** every write takes the **caller's**
/// <see cref="IDocumentSession"/> (the write and the in-session
/// <c>AccessAudit</c> row commit or roll back atomically — the
/// <see cref="PostService.AddPostTranslationAsync"/> shape). One
/// <c>SaveChangesAsync</c> per write.
/// </para>
/// <para>
/// **Standing (C-TG·5, D4):** attach = the object's existing edit standing
/// (author ∪ GlobalAdmin — ADR 0014 / 0016 for posts, ADR 0040 for pages;
/// the ADR 0013 group lane has no component-moderator standing, ADR 0007);
/// translate = the tag's <c>CreatedBy</c> ∪ GlobalAdmin only. A denied actor
/// throws <see cref="UnauthorizedAccessException"/> **before** anything is
/// stored (C-TG·9).
/// </para>
/// <para>
/// **Via (ADR 0006-D lane pin):** only <see cref="AccessVia.Owner"/> (the
/// actor is the author / creator) or <see cref="AccessVia.Admin"/> (the
/// actor is a GlobalAdmin). Never <c>Moderator</c> / <c>BreakGlass</c> /
/// <c>Group</c> / any other value — the tag lane composes only the frozen
/// seams (the ADR 0006-D pin).
/// </para>
/// </summary>
public sealed class TagService : ITagService
{
    private readonly IDocumentStore _store;

    /// <summary>
    /// The only constructor dependency is the host-registered
    /// <see cref="IDocumentStore"/> (the <see cref="Pages.PageService"/> shape —
    /// U6's read lane needs it for the C-TG·2 base query; the write lane
    /// (U5) uses the caller's <see cref="IDocumentSession"/> per C3). **No**
    /// <see cref="IAuthorizationService"/> / <see cref="IUserInfoService"/> /
    /// <see cref="ITranslationProvider"/> is injected here — the write lane
    /// resolves standing inline (the <see cref="CanAttachToPost"/> /
    /// <see cref="CanAttachToPage"/> / <see cref="CanTranslateTag"/> probes +
    /// the <see cref="ResolveAttachVia"/> / <see cref="ResolveTranslateVia"/>
    /// helpers, the ADR 0006-D lane pin: composes only the frozen seams,
    /// never a new <c>AccessVia</c> value beyond <c>Owner</c> / <c>Admin</c>).
    /// The DI registration (<see cref="DependencyInjection.ServiceCollectionExtensions"/>)
    /// injects the store; tests construct <see cref="TagService"/> directly
    /// with a live scratch store (the <see cref="PostService"/> harness idiom
    /// — not DI).
    /// </summary>
    public TagService(IDocumentStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    // ── Write lane ──────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<Tag>> AttachToPostAsync(
        string postId, IReadOnlyList<string> slugs,
        string actorId, IReadOnlySet<string> roles, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(postId))
            throw new ArgumentException("A post id is required.", nameof(postId));
        if (slugs is null) throw new ArgumentNullException(nameof(slugs));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(session);

        var post = await session.LoadAsync<Post>(postId);
        if (post is null)
            throw new KeyNotFoundException(
                $"Post '{postId}' was not found in the session; nothing to attach.");

        // Standing check (C-TG·5, D4; ADR 0014/0016 community lane,
        // ADR 0013 group lane — author ∪ GlobalAdmin, no component-moderator):
        if (!CanAttachToPost(post, actorId, roles))
            throw new UnauthorizedAccessException(
                "Only the post's author or a GlobalAdmin may attach tags to it.");

        var now = DateTimeOffset.UtcNow;
        var via = ResolveAttachVia(post.AuthorId, actorId, roles);
        var resolvedTags = new List<Tag>(slugs.Count);
        var tagIds = new List<string>(slugs.Count);

        foreach (var slug in slugs)
        {
            var derivedSlug = DeriveSlug(slug);

            // C-TG·4: create-or-reuse (the Slug is the business key —
            // a second author attaching the same Slug reuses the Tag doc,
            // and does NOT become its CreatedBy, F2).
            var existing = await session.Query<Tag>()
                .Where(t => t.Slug == derivedSlug)
                .FirstOrDefaultAsync();

            if (existing is not null)
            {
                resolvedTags.Add(existing);
                tagIds.Add(existing.Id);
            }
            else
            {
                var tag = new Tag
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Slug = derivedSlug,
                    Name = derivedSlug,
                    LanguageCode = string.IsNullOrEmpty(post.LanguageCode) ? "en" : post.LanguageCode,
                    CreatedBy = actorId,
                    Created = now,
                };

                // One tag.create audit row per newly created tag (C-TG·9, F1).
                var createAudit = new Authorization.AccessAudit
                {
                    Id = Guid.NewGuid().ToString("N"),
                    At = now,
                    ActorId = actorId,
                    EffectivePrincipalId = actorId,
                    Action = "tag.create",
                    TargetKind = "tag",
                    TargetId = tag.Id,
                    Via = via,
                    Outcome = Authorization.AccessOutcome.Allow,
                };

                session.Store(tag);
                session.Store(createAudit);
                resolvedTags.Add(tag);
                tagIds.Add(tag.Id);
            }
        }

        post.TagIds = tagIds;

        // One tag.attach audit row (C-TG·9, F1).
        var attachAudit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "tag.attach",
            TargetKind = "post",
            TargetId = postId,
            Via = via,
            Outcome = Authorization.AccessOutcome.Allow,
        };
        session.Store(attachAudit);

        await session.SaveChangesAsync();
        return resolvedTags;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Tag>> AttachToPageAsync(
        string pageId, IReadOnlyList<string> slugs,
        string actorId, IReadOnlySet<string> roles, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(pageId))
            throw new ArgumentException("A page id is required.", nameof(pageId));
        if (slugs is null) throw new ArgumentNullException(nameof(slugs));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(session);

        var page = await session.LoadAsync<Page>(pageId);
        if (page is null)
            throw new KeyNotFoundException(
                $"Page '{pageId}' was not found in the session; nothing to attach.");

        // C-TG·6, D6: a System page with a non-empty slugs set is refused
        // (a shape violation, not a standing denial — consistent with the
        // U4 PageService refusal). A no-op detach (empty slugs) on a System
        // page proceeds (it simply re-saves an already-empty TagIds).
        if (page.Kind == PageKind.System && slugs.Count > 0)
            throw new ArgumentException(
                "A PageKind.System page cannot carry tags (C-TG·6, D6).", nameof(slugs));

        // Standing check (C-TG·5, D4; ADR 0040 — author ∪ GlobalAdmin):
        if (!CanAttachToPage(page, actorId, roles))
            throw new UnauthorizedAccessException(
                "Only the page's author or a GlobalAdmin may attach tags to it.");

        var now = DateTimeOffset.UtcNow;
        var via = ResolveAttachVia(page.AuthorId, actorId, roles);
        var resolvedTags = new List<Tag>(slugs.Count);
        var tagIds = new List<string>(slugs.Count);

        foreach (var slug in slugs)
        {
            var derivedSlug = DeriveSlug(slug);

            var existing = await session.Query<Tag>()
                .Where(t => t.Slug == derivedSlug)
                .FirstOrDefaultAsync();

            if (existing is not null)
            {
                resolvedTags.Add(existing);
                tagIds.Add(existing.Id);
            }
            else
            {
                var tag = new Tag
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Slug = derivedSlug,
                    Name = derivedSlug,
                    LanguageCode = string.IsNullOrEmpty(page.LanguageCode) ? "en" : page.LanguageCode,
                    CreatedBy = actorId,
                    Created = now,
                };

                var createAudit = new Authorization.AccessAudit
                {
                    Id = Guid.NewGuid().ToString("N"),
                    At = now,
                    ActorId = actorId,
                    EffectivePrincipalId = actorId,
                    Action = "tag.create",
                    TargetKind = "tag",
                    TargetId = tag.Id,
                    Via = via,
                    Outcome = Authorization.AccessOutcome.Allow,
                };

                session.Store(tag);
                session.Store(createAudit);
                resolvedTags.Add(tag);
                tagIds.Add(tag.Id);
            }
        }

        page.TagIds = tagIds;

        var attachAudit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "tag.attach",
            TargetKind = "page",
            TargetId = pageId,
            Via = via,
            Outcome = Authorization.AccessOutcome.Allow,
        };
        session.Store(attachAudit);

        await session.SaveChangesAsync();
        return resolvedTags;
    }

    /// <inheritdoc />
    public async Task<TagTranslation> AddTagTranslationAsync(
        string tagId, string languageCode, string name,
        string actorId, IReadOnlySet<string> roles, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(tagId))
            throw new ArgumentException("A tag id is required.", nameof(tagId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException(
                "A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "A translation requires a non-empty display name.", nameof(name));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(session);

        var tag = await session.LoadAsync<Tag>(tagId);
        if (tag is null)
            throw new KeyNotFoundException(
                $"Tag '{tagId}' was not found in the session; nothing to translate.");

        // Standing check (C-TG·5, D4 — creator ∪ GlobalAdmin only; a
        // non-creator attacher cannot reword the tag, F7):
        if (!CanTranslateTag(tag, actorId, roles))
            throw new UnauthorizedAccessException(
                "Only the tag's creator or a GlobalAdmin may set its translations.");

        var now = DateTimeOffset.UtcNow;
        var via = ResolveTranslateVia(tag.CreatedBy, actorId, roles);

        // Create-or-overwrite the (tagId, languageCode) row (the D4 translate
        // lane; the (TagId, LanguageCode) unique index enforces one row per
        // pair — re-adding overwrites).
        var existing = await session.Query<TagTranslation>()
            .Where(t => t.TagId == tagId && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync();

        TagTranslation translation;
        if (existing is not null)
        {
            existing.Name = name;
            existing.AuthorId = actorId;
            existing.Created = now;
            translation = existing;
        }
        else
        {
            translation = new TagTranslation
            {
                Id = Guid.NewGuid().ToString("N"),
                TagId = tagId,
                LanguageCode = languageCode,
                Name = name,
                AuthorId = actorId,
                Created = now,
            };
            session.Store(translation);
        }

        // One tagtranslation.add audit row (C-TG·9, F6/F8).
        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "tagtranslation.add",
            TargetKind = "tag",
            TargetId = tagId,
            Via = via,
            Outcome = Authorization.AccessOutcome.Allow,
        };
        session.Store(audit);

        await session.SaveChangesAsync();
        return translation;
    }

    // ── Read lane (U6 — not yet implemented) ────────────────────────────────
    // The register's U5/U6 split note: U5 declares the full 11-member
    // ITagService interface + implements the 6 write-lane members + the
    // TagItem record; U6 implements the 4 read-lane members. These stubs
    // are the boundary marker — they throw to make any accidental call
    // fail loudly (not a silent no-op) until U6 lands.

    /// <inheritdoc />
    public Task<IReadOnlyList<TagItem>> ListForActorAsync(string actorId)
        => throw new NotImplementedException(
            "U6 — the read lane (ListForActorAsync) is not yet implemented; see the register's U6 unit.");

    /// <inheritdoc />
    public Task<IReadOnlyList<Post>> ListPostsByTagAsync(string slug, string actorId)
        => throw new NotImplementedException(
            "U6 — the read lane (ListPostsByTagAsync) is not yet implemented; see the register's U6 unit.");

    /// <inheritdoc />
    public Task<IReadOnlyList<Page>> ListPagesByTagAsync(string slug, string actorId)
        => throw new NotImplementedException(
            "U6 — the read lane (ListPagesByTagAsync) is not yet implemented; see the register's U6 unit.");

    /// <inheritdoc />
    public Task<IReadOnlyList<TagItem>> SuggestAsync(string prefix, string actorId)
        => throw new NotImplementedException(
            "U6 — the read lane (SuggestAsync) is not yet implemented; see the register's U6 unit.");

    // ── Standing probes (C-TG·5; a display pin, not a gate — the real deny
    //    is the write-lane re-check, the PostService.CanAddTranslation /
    //    PageService.CanTranslatePage idiom) ──

    /// <inheritdoc />
    public bool CanAttachToPost(Post post, string actorId, IReadOnlySet<string> roles)
    {
        ArgumentNullException.ThrowIfNull(post);
        ArgumentNullException.ThrowIfNull(roles);
        return string.Equals(post.AuthorId, actorId, StringComparison.Ordinal)
               || roles.Contains(Roles.GlobalAdmin);
    }

    /// <inheritdoc />
    public bool CanAttachToPage(Page page, string actorId, IReadOnlySet<string> roles)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(roles);
        if (page.Kind == PageKind.System)
            return false;
        return string.Equals(page.AuthorId, actorId, StringComparison.Ordinal)
               || roles.Contains(Roles.GlobalAdmin);
    }

    /// <inheritdoc />
    public bool CanTranslateTag(Tag tag, string actorId, IReadOnlySet<string> roles)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(roles);
        return string.Equals(tag.CreatedBy, actorId, StringComparison.Ordinal)
               || roles.Contains(Roles.GlobalAdmin);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the <see cref="AccessVia"/> for an attach standing (the
    /// ADR 0006-D lane pin — only <see cref="AccessVia.Owner"/> or
    /// <see cref="AccessVia.Admin"/>; never <c>Moderator</c> /
    /// <c>BreakGlass</c> / <c>Group</c> or any other value). Precedence:
    /// author first (the narrowest right), then GlobalAdmin. Throws
    /// <see cref="UnauthorizedAccessException"/> if the actor holds neither
    /// (defensive — the standing probe is checked before this is called).
    /// </summary>
    private static AccessVia ResolveAttachVia(
        string authorId, string actorId, IReadOnlySet<string> roles)
    {
        if (string.Equals(authorId, actorId, StringComparison.Ordinal))
            return AccessVia.Owner;
        if (roles.Contains(Roles.GlobalAdmin))
            return AccessVia.Admin;
        throw new UnauthorizedAccessException(
            "The actor holds neither the author nor the GlobalAdmin standing.");
    }

    /// <summary>
    /// Resolves the <see cref="AccessVia"/> for a translate standing (the
    /// C-TG·5 pin — the tag's <c>CreatedBy</c> ∪ GlobalAdmin only).
    /// Precedence: creator first, then GlobalAdmin. Throws
    /// <see cref="UnauthorizedAccessException"/> if the actor holds neither.
    /// </summary>
    private static AccessVia ResolveTranslateVia(
        string createdBy, string actorId, IReadOnlySet<string> roles)
    {
        if (string.Equals(createdBy, actorId, StringComparison.Ordinal))
            return AccessVia.Owner;
        if (roles.Contains(Roles.GlobalAdmin))
            return AccessVia.Admin;
        throw new UnauthorizedAccessException(
            "The actor holds neither the creator nor the GlobalAdmin standing.");
    }

    /// <summary>
    /// Derives the normalized <c>Slug</c> from the typed input (C-TG·4, D3):
    /// lowercase + trim; validate the charset (letters — Unicode-aware, so
    /// accented letters like <c>é</c> are valid; digits; hyphens; underscores;
    /// ≤ 64 chars). <c>sanitation</c> and <c>Hygiène</c> are **two** different
    /// slugs (no accent-folding, no cross-language merge — D3). Throws
    /// <see cref="ArgumentException"/> on a violation.
    /// <para>
    /// The derived slug is the business key (C-TG·4) — the write lane's
    /// create-or-reuse query keys on it, so the normalization here is the
    /// identity guard (the <see cref="TagDocTypes"/> surface has no slug
    /// unique index; the write lane is the guard, the ADR 0011
    /// content-hash-Id dedup analogue).
    /// </para>
    /// </summary>
    private static string DeriveSlug(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException(
                "A non-blank slug is required.", nameof(input));

        var slug = input.Trim().ToLowerInvariant();

        if (slug.Length > 64)
            throw new ArgumentException(
                "A slug must be at most 64 characters.", nameof(input));

        for (var i = 0; i < slug.Length; i++)
        {
            var c = slug[i];
            if (!char.IsLetter(c) && !char.IsDigit(c) && c != '-' && c != '_')
                throw new ArgumentException(
                    "A slug may contain only letters, digits, hyphens, and underscores.",
                    nameof(input));
        }

        return slug;
    }
}
