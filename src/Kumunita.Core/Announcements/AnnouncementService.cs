using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Marten;
using Marten.Services;

namespace Kumunita.Core.Announcements;

/// <summary>
/// Composition service for the <see cref="Announcement"/> bounded context
/// (the "platform announcements" lane — as opposed to a community
/// <see cref="Posts.Post"/>'s audience-restricted lane).
/// <para>
/// Session shape mirrors <see cref="Posts.PostService"/> (invariant C3 —
/// same transaction for writes): reads open their own
/// <c>QuerySession</c> (a plain read; announcements are not
/// audience-restricted, so there is no <c>AccessAudit</c> lane on this
/// bounded context and this service composes only the store seam);
/// writes go through the caller's in-flight <see cref="IDocumentSession"/>
/// (the Web layer's <c>DocumentStore.LightweightSession()</c>) so the
/// write and any other in-session state commit or roll back atomically.
/// </para>
/// <para>
/// <b>The "public vs community" split</b> (see <see cref="AnnouncementScope"/>):
/// a <see cref="AnnouncementScope.Public"/> announcement is visible to
/// <em>every</em> visitor — authenticated or not — and is authorable only
/// by a <see cref="Roles.GlobalAdmin"/>; a
/// <see cref="AnnouncementScope.Community"/> announcement is visible to
/// every signed-in user and is authorable by a GlobalAdmin or a
/// <see cref="Roles.Moderator"/>; it may also <em>target</em> a specific
/// community (a <c>CommunityId</c>), which then makes it visible only to
/// that community's members or moderators and a <see cref="Roles.GlobalAdmin"/>
/// and is authorable by that community's moderator or a
/// <see cref="Roles.GlobalAdmin"/>. <see cref="CreateAsync"/> enforces that
/// split at the Core layer (defense-in-depth — the ASP.NET gate already
/// narrows the author's role, but the Web layer cannot narrow the
/// <em>scope</em> choice by itself: a Moderator could POST a
/// <c>Scope=&"Public"</c> body regardless of the form they were served,
/// so the service is what guarantees the split is real).
/// </para>
/// </summary>
public sealed class AnnouncementService : IAnnouncementService
{
    private readonly IDocumentStore _store;
    private readonly IUserInfoService _userInfo;

    public AnnouncementService(IDocumentStore store, IUserInfoService userInfo)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
    }

    /// <summary>
    /// The set of <see cref="Announcement"/>s visible at the caller's
    /// authentication state:
    /// <list type="bullet">
    /// <item><see cref="AnnouncementScope.Public"/> — always, authed or not;</item>
    /// <item><see cref="AnnouncementScope.Community"/> — only when <paramref name="roles"/>.</item>
    /// </list>
    /// Sorted by <c>Created</c> descending (latest first). No audit
    /// <see cref="Authorization.AccessAudit"/> row (announcements are not
    /// audience-restricted, so there's no per-item decision to log — the
    /// coarse role gate is the whole decision, and it's a view-model
    /// filter, not a call into <c>IAuthorizationService</c>).
    /// </summary>
    public async Task<IReadOnlyList<Announcement>> ListVisibleAsync(string? actorId, IReadOnlySet<string> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        var (authed, admin, communities) = await ResolveReadVisibilityAsync(actorId, roles).ConfigureAwait(false);

        await using var session = _store.QuerySession();
        return await session
            .Query<Announcement>()
            .Where(a => !a.IsDraft &&
                        ((a.CommunityId == null &&
                          (a.Scope == AnnouncementScope.Public ||
                           (authed && a.Scope == AnnouncementScope.Community)))
                         || (a.CommunityId != null &&
                             (admin || communities.Contains(a.CommunityId!)))))
            .OrderByDescending(a => a.Created)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The single announcement to render at <c>/announcements/{id}</c>
    /// (the detail view): loaded by id, passed through the <em>same</em>
    /// visibility gate as <see cref="ListVisibleAsync"/>. Returns null —
    /// not an exception — when the id is missing <em>or</em> not visible
    /// to the caller (the two are deliberately indistinguishable:
    /// <see cref="PinnedAsync"/> returns null the same way, and there is no
    /// <c>AccessAudit</c> lane on this bounded context to log the "denied"
    /// side — the Web layer maps null to a 404).
    /// </summary>
    public async Task<Announcement?> GetAsync(string id, string? actorId, IReadOnlySet<string> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        if (string.IsNullOrEmpty(id)) return null;

        await using var session = _store.QuerySession();
        var announcement = await session.LoadAsync<Announcement>(id).ConfigureAwait(false);
        if (announcement is null)
            // Missing id ⇒ null (the Web layer's 404), the same fail-closed shape
            // as before the draft lane existed.
            return null;

        // ADR 0037 — draft gate (author-only, no audit row, no roles): a draft
        // announcement is invisible to **everyone except its author** — even a
        // GlobalAdmin is denied (the author-only pin is deliberately stronger
        // than the announcement lane's usual role split). No
        // <c>AccessAudit</c> row (announcements have no audit lane at all), and
        // the role set is <b>not</b> consulted: the sole decision is a pure
        // <c>AuthorId == actorId</c> ordinal check. A non-author (including a
        // GlobalAdmin) is denied — the Web layer maps both "missing" and
        // "not visible" to the same 404 (the non-leaky pin, ADR 0037).
        if (announcement.IsDraft)
            return (string.IsNullOrEmpty(actorId) ||
                    !string.Equals(announcement.AuthorId, actorId, StringComparison.Ordinal))
                ? null
                : announcement;

        // Live announcement — the normal scope-vs-role visibility gate (the
        // ADR 0017 / flat-two-way split): Public always; Community when signed
        // in; a community-targeted row only for that community's moderator /
        // member / a GlobalAdmin. Evaluated in C# against the loaded doc (the
        // same predicate ListVisibleAsync / PinnedAsync apply as a query filter).
        var (authed, admin, communities) = await ResolveReadVisibilityAsync(actorId, roles).ConfigureAwait(false);
        var visible = (announcement.CommunityId == null &&
                       (announcement.Scope == AnnouncementScope.Public ||
                        (authed && announcement.Scope == AnnouncementScope.Community)))
            || (announcement.CommunityId != null &&
                (admin || communities.Contains(announcement.CommunityId!)));

        return visible ? announcement : null;
    }

    /// <summary>
    /// The single announcement to render as a site-wide banner:
    /// the most-recently-created <see cref="Announcement"/> with
    /// <see cref="Announcement.Pinned" /> true that passes the caller's
    /// authentication state (the same gate as <see cref="ListVisibleAsync"/>:
    /// <see cref="AnnouncementScope.Public" /> always;
    /// <see cref="AnnouncementScope.Community" /> only when
    /// <paramref name="roles"/>). Returns null when no pinned
    /// announcement passes (the Web layer skips the banner in that case).
    /// No <c>AccessAudit</c> row (same reasoning as
    /// <see cref="ListVisibleAsync"/> — announcements are not
    /// audience-restricted; the scope-vs-role split is the whole decision).
    /// </summary>
    public async Task<Announcement?> PinnedAsync(string? actorId, IReadOnlySet<string> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        var (authed, admin, communities) = await ResolveReadVisibilityAsync(actorId, roles).ConfigureAwait(false);

        await using var session = _store.QuerySession();
        return await session
            .Query<Announcement>()
            .Where(a => !a.IsDraft && a.Pinned == true &&
                        ((a.CommunityId == null &&
                          (a.Scope == AnnouncementScope.Public ||
                           (authed && a.Scope == AnnouncementScope.Community)))
                         || (a.CommunityId != null &&
                             (admin || communities.Contains(a.CommunityId!)))))
            .OrderByDescending(a => a.Created)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Creates an <see cref="Announcement"/> in the <b>caller's</b> in-flight
    /// session (invariant C3 — the same-transaction lane, see
    /// <see cref="Posts.PostService.CreatePostAsync"/> for the shape).
    /// Enforces the scope-vs-role split:
    /// <list type="bullet">
    /// <item><see cref="AnnouncementScope.Public"/> — the author must hold <see cref="Roles.GlobalAdmin"/>;</item>
    /// <item><see cref="AnnouncementScope.Community"/> — the author must hold <see cref="Roles.GlobalAdmin"/>
    /// <b>or</b> <see cref="Roles.Moderator"/>.</item>
    /// </list>
    /// A denied split is a hard <see cref="UnauthorizedAccessException"/>
    /// (the Web layer maps that to a 403) — NOT a silent no-op, and NOT
    /// the <c>AccessAudit</c> decision lane (announcements are not
    /// audience-restricted; the audit lane is for per-user decisions,
    /// which this flat split is not). <paramref name="authorRoles"/> is
    /// the caller's role claim set (the Web layer's principal, not a DB
    /// read — same claim-set-as-principal seam the ASP.NET gate reads).
    /// </summary>
    public async Task<Announcement> CreateAsync(
        Announcement announcement,
        string actorId,
        IReadOnlySet<string> authorRoles,
        IDocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(announcement);
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An authoring actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(authorRoles);
        ArgumentNullException.ThrowIfNull(session);

        await EnsureCreatePermissionAsync(announcement, authorRoles).ConfigureAwait(false);

        if (string.IsNullOrEmpty(announcement.Id))
            announcement.Id = Guid.NewGuid().ToString("N");
        announcement.AuthorId = actorId;
        announcement.Created  = DateTimeOffset.UtcNow;
        announcement.LanguageCode = await ResolveLanguageCodeAsync(announcement.LanguageCode, session).ConfigureAwait(false); // ADR 0018

        session.Store(announcement);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return announcement;
    }

    /// <summary>
    /// ADR 0018 — resolves the authored-in <c>LanguageCode</c> for a new
    /// announcement: a non-empty authored code is used verbatim (BCP-47 tag,
    /// ADR 0005 B); a null/empty code is materialized from the instance
    /// default (<see cref="LocaleSettings.DefaultLanguageCode"/>, loaded from
    /// the caller's in-flight session) with <c>en</c> as the floor when the
    /// singleton row is absent. The result is **always** a concrete BCP-47
    /// code — no stored row is left empty. A tag, not a translation (ADR 0005 C
    /// unchanged).
    /// </summary>
    private async Task<string> ResolveLanguageCodeAsync(string? languageCode, IDocumentSession session)
    {
        if (!string.IsNullOrWhiteSpace(languageCode))
            return languageCode;

        var settings = await session.LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, CancellationToken.None).ConfigureAwait(false);
        if (settings is not null && !string.IsNullOrWhiteSpace(settings.DefaultLanguageCode))
            return settings.DefaultLanguageCode;

        return "en";
    }

    /// <summary>
    /// Edits an existing <see cref="Announcement"/> in the <b>caller's</b>
    /// in-flight session (invariant C3, mirroring <see cref="CreateAsync"/>
    /// and <see cref="DeleteAsync"/>'s write lane). The edit gate is
    /// <em>distinct</em> from the create gate (see
    /// <see cref="EnsureEditPermissionAsync"/>) and is evaluated against the
    /// <em>stored</em> announcement (loaded first — a missing id is a
    /// <see cref="KeyNotFoundException"/>, the Web layer's 404):
    /// <list type="bullet">
    /// <item><see cref="AnnouncementScope.Public"/> — the actor must hold <see cref="Roles.GlobalAdmin"/></item>
    /// <item><see cref="AnnouncementScope.Community"/> with no <see cref="Announcement.CommunityId"/>
    /// (the flat "all residents" target) — the actor must be the stored
    /// <see cref="Announcement.AuthorId"/> or hold <see cref="Roles.GlobalAdmin"/>; a community
    /// moderator who did not author it is <b>denied</b> (ADR 0017).</item>
    /// <item><see cref="AnnouncementScope.Community"/> with a <see cref="Announcement.CommunityId"/>
    /// — the actor must hold <see cref="Roles.GlobalAdmin"/> or the
    /// <c>moderator:{CommunityId}</c> standing claim.</item>
    /// </list>
    /// A denied actor is a hard <see cref="UnauthorizedAccessException"/>
    /// (the Web layer maps that to a 403); a missing id is a
    /// <see cref="KeyNotFoundException"/> (the Web layer maps that to a 404).
    /// <see cref="Announcement.AuthorId"/> and <see cref="Announcement.Created"/>
    /// are deliberately <b>not</b> reassigned here (unlike
    /// <see cref="CreateAsync"/>, which mints a brand-new doc): the author of
    /// record is whoever created it, not whoever last edited it, and
    /// <see cref="Announcement.Modified"/> is stamped — <see cref="DateTimeOffset.UtcNow"/>
    /// — only when at least one of Title/Body/Scope/Pinned/CommunityId/
    /// <see cref="Announcement.LanguageCode"/> (ADR 0018) actually changed,
    /// so a no-op re-save of an unchanged doc does not bump the stamp.
    /// </summary>
    public async Task<Announcement> UpdateAsync(
        Announcement updated,
        string actorId,
        IReadOnlySet<string> actorRoles,
        IDocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(updated);
        if (string.IsNullOrEmpty(updated.Id)) throw new ArgumentException("An announcement id is required.", nameof(updated.Id));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(session);

        // Load the stored doc first: a missing id is a 404 (KeyNotFound), not a 403 —
        // the same contract the delete lane and the Update_ToPublic pin pin for a
        // nonexistent id. The edit gate (EnsureEditPermissionAsync) needs the stored
        // AuthorId, so it runs against the stored row after the load.
        var existing = await session.LoadAsync<Announcement>(updated.Id).ConfigureAwait(false);
        if (existing is null)
            throw new KeyNotFoundException($"Announcement '{updated.Id}' was not found in the session; nothing to edit.");

        await EnsureEditPermissionAsync(existing, actorId, actorRoles);

        // ADR 0018 — materialize the authored-in tag the same way CreateAsync
        // does (a null/empty value ⇒ the instance default, <c>en</c> floor) on
        // BOTH sides before comparing. The stored side is normalized too: a
        // pre-ADR-0018 row has an empty <c>LanguageCode</c>, and a no-op
        // re-save that leaves the picker at the instance default must compare
        // as "unchanged" (resolving <c>""</c> → the default on the stored side
        // and the default → the default on the updated side makes the two
        // equal) — otherwise every no-op edit would falsely stamp
        // <c>Modified</c>. A genuine language change (e.g. default → <c>pl</c>)
        // still registers as changed.
        var existingLanguageCode = await ResolveLanguageCodeAsync(existing.LanguageCode, session).ConfigureAwait(false);
        var updatedLanguageCode  = await ResolveLanguageCodeAsync(updated.LanguageCode,  session).ConfigureAwait(false);

        var changed = existing.Title != updated.Title
            || existing.Body != updated.Body
            || existing.Scope != updated.Scope
            || existing.Pinned != updated.Pinned
            || existing.CommunityId != updated.CommunityId
            || existingLanguageCode != updatedLanguageCode; // ADR 0018 — the announcement edit surface is not ADR-frozen (unlike the post/reply edit lanes), so the authored-in tag is editable here too.

        existing.Title = updated.Title;
        existing.Body  = updated.Body;
        existing.Scope = updated.Scope;
        existing.Pinned = updated.Pinned;
        existing.CommunityId = updated.CommunityId;
        existing.LanguageCode = updatedLanguageCode; // ADR 0018
        existing.ImageIds = updated.ImageIds ?? []; // RC U05 (R·3) — the announcement edit lane persists the server-side-parsed content-image ids (POCO-direct: the Web layer parses the body, Core writes it verbatim — the same field-copy shape as the six above; null-coalesce to the POCO's non-null empty list).
        existing.AttachmentIds = updated.AttachmentIds ?? []; // ATT U5 (C-ATT·5) — the announcement edit lane persists the server-side-parsed attachment ids (POCO-direct: the Web layer parses the body, Core writes it verbatim — the same field-copy shape as the `ImageIds` line above; null-coalesce to the POCO's non-null empty list).
        if (changed)
            existing.Modified = DateTimeOffset.UtcNow;

        session.Store(existing);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return existing;
    }

    /// <summary>
    /// Deletes an <see cref="Announcement"/> in the <b>caller's</b> in-flight
    /// session (invariant C3). A hard delete (no soft-hidden state —
    /// announcements are a flat public surface, not audience-restricted
    /// content, so there's no <see cref="Posts.PostStatus"/>-shaped
    /// surface to preserve). The Web layer's
    /// <c>[Authorize(Roles = GlobalAdmin)]</c> gate is the sole role
    /// check (a single valid author role means there's no split to
    /// re-check at the Core layer the way <see cref="CreateAsync"/>'s
    /// scope-vs-role split does). A missing id is a
    /// <see cref="KeyNotFoundException"/> (the Web layer maps that to a 404).
    /// </summary>
    public async Task DeleteAsync(string announcementId, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(announcementId))
            throw new ArgumentException("An announcement id is required.", nameof(announcementId));
        ArgumentNullException.ThrowIfNull(session);

        var announcement = await session.LoadAsync<Announcement>(announcementId).ConfigureAwait(false);
        if (announcement is null)
            throw new KeyNotFoundException($"Announcement '{announcementId}' was not found in the session; nothing to delete.");

        session.Delete(announcement);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    // ─── ADR 0037 — author-only publish lane (draft → live) ─────────────────

    /// <summary>
    /// Publish a draft announcement — **author-only** (ADR 0037). Clears
    /// <see cref="Announcement.IsDraft"/> so the announcement becomes visible
    /// under its normal <see cref="AnnouncementScope"/> split (Public always /
    /// Community when signed in / community-targeted to that community's
    /// members and moderator).
    /// <para>
    /// The sole decision is <c>announcement.AuthorId == actorId</c> (ordinal
    /// comparison) — a non-author is denied (<see cref="UnauthorizedAccessException"/>,
    /// the Web layer's 403) <b>even at GlobalAdmin</b> (ADR 0037's author-only
    /// pin: publishing an announcement is the author's choice, not an admin's
    /// lever — contrast ADR 0017's edit lane, where a GlobalAdmin may edit any
    /// row). A missing id is a <see cref="KeyNotFoundException"/> (the Web
    /// layer's 404), the same fail-closed shape the delete lane pins.
    /// </para>
    /// <para>
    /// <b>Idempotent</b> (the ADR 0024 / no-op-re-save pin): a second publish
    /// on an already-live announcement is a no-op — it does not stamp
    /// <see cref="Announcement.Modified"/> when nothing changed. It does
    /// <b>not</b> touch <see cref="Announcement.Pinned"/> (a draft may or may
    /// not be pinned; publishing changes visibility, not pin state) and does
    /// <b>not</b> re-evaluate the scope-vs-role split (the author already holds
    /// the standing that created it). One <c>SaveChangesAsync</c> (invariant
    /// C3). No <c>AccessAudit</c> row (announcements have no audit lane — the
    /// flat split is not a per-user decision, the ADR 0017 precedent).
    /// </para>
    /// </summary>
    /// <exception cref="KeyNotFoundException">The announcement id is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor is not the announcement's author.</exception>
    public async Task<Announcement> PublishAsync(string announcementId, string actorId, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(announcementId)) throw new ArgumentException("An announcement id is required.", nameof(announcementId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting author is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(session);

        var announcement = await session.LoadAsync<Announcement>(announcementId).ConfigureAwait(false);
        if (announcement is null)
            throw new KeyNotFoundException($"Announcement '{announcementId}' was not found in the session; nothing to publish.");

        // Author-only gate (ADR 0037): only the author may publish. A non-author
        // is a denial — the Web layer maps it to its 403 shape (the ADR 0017
        // edit-lane precedent, minus the GlobalAdmin branch that ADR 0017 allows).
        if (!string.Equals(announcement.AuthorId, actorId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Only the author of an announcement may publish it.");

        // Idempotent (the no-op-re-save pin): a second publish on an
        // already-live announcement does not stamp Modified.
        if (announcement.IsDraft)
        {
            announcement.IsDraft = false;
            announcement.Modified = DateTimeOffset.UtcNow;
        }

        session.Store(announcement);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return announcement;
    }

    /// <summary>
    /// The **author's own** draft announcements (ADR 0037) — the
    /// <see cref="Announcement"/>s with <see cref="Announcement.IsDraft"/> true
    /// and <see cref="Announcement.AuthorId"/> == <paramref name="actorId"/>
    /// (ordinal comparison), sorted by <see cref="Announcement.Created"/>
    /// descending. This is the "My drafts" list the Web layer's
    /// <c>GET /announcements/drafts</c> renders — the discoverability surface
    /// for draft announcements, since <see cref="ListVisibleAsync"/> deliberately
    /// excludes them.
    /// <para>
    /// <b>Not an authorization surface (the ADR 0037 author-lane precedent):</b>
    /// the only decision is the pure <c>AuthorId == actorId</c> match in the
    /// query itself — there is <b>no</b> <c>AccessAudit</c> row (announcements
    /// have no audit lane at all) and no <c>roles</c> parameter (the author-only
    /// gate is the sole decision, ADR 0037). Opens its own <c>QuerySession</c>
    /// (the C3 read-lane shape — reads never touch the caller's write session).
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<Announcement>> ListMyDraftsAsync(string actorId)
    {
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting author is required.", nameof(actorId));

        await using var session = _store.QuerySession();
        return await session
            .Query<Announcement>()
            .Where(a => a.IsDraft && a.AuthorId == actorId)
            .OrderByDescending(a => a.Created)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    // ─── ADR 0029 — user-added announcement translations lane ───────────────

    /// <summary>
    /// The **read** seam for an announcement's user-added translations (ADR
    /// 0029, mirroring the ADR 0022
    /// <see cref="Kumunita.Core.Posts.PostService.GetPostTranslationsAsync"/>
    /// shape): the <see cref="AnnouncementTranslation"/> rows under
    /// <paramref name="announcementId"/>. Opens its own <c>QuerySession</c>
    /// (the C3 read-lane shape — reads never touch the caller's write
    /// session).
    /// <para>
    /// <b>Not an authorization surface (the ADR 0022 read-pin carried over):</b>
    /// a translation has no own audience — its visibility inherits the
    /// announcement's flat two-way <see cref="AnnouncementScope"/> split,
    /// which the caller has already made (the Web reads this only after
    /// <see cref="GetAsync"/> returned the announcement). So this method does
    /// <b>not</b> gate and writes <b>no</b> <c>AccessAudit</c> row — the same
    /// "a read, not a decision" pin as the list's as-is return.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<AnnouncementTranslation>> GetAnnouncementTranslationsAsync(string announcementId)
    {
        if (string.IsNullOrEmpty(announcementId))
            throw new ArgumentException("An announcement id is required.", nameof(announcementId));

        await using var session = _store.QuerySession();
        return await session
            .Query<AnnouncementTranslation>()
            .Where(t => t.AnnouncementId == announcementId)
            .OrderBy(t => t.LanguageCode)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Adds a **user-added translation** of an announcement in the
    /// <b>caller's</b> in-flight session (invariant C3 — the
    /// <see cref="IDocumentSession"/> is the caller's, so the write and the
    /// in-session <c>AccessAudit</c> row commit or roll back atomically).
    /// <para>
    /// <b>Standing (ADR 0029):</b> a <see cref="Roles.GlobalAdmin"/> or a
    /// <see cref="Roles.Translator"/> (both <see cref="AccessVia.Admin"/> —
    /// instance-wide, the ADR 0021/0026 Translator standing); and — for a
    /// <see cref="AnnouncementScope.Community"/> announcement
    /// <em>targeted</em> at one community — a
    /// <see cref="Roles.Moderator"/> scoped to that community
    /// (<see cref="AccessVia.Moderator"/>). A flat community-scope
    /// announcement (no <c>CommunityId</c>) and a
    /// <see cref="AnnouncementScope.Public"/> announcement have no community
    /// to moderate, so the component-moderator standing does not qualify for
    /// them. A denied actor throws <see cref="UnauthorizedAccessException"/>
    /// <b>before</b> anything is stored.
    /// </para>
    /// <para>
    /// <paramref name="languageCode"/> is the **target** language (a
    /// <see cref="Kumunita.Core.Localization.LanguageCatalog.Id"/> the Web
    /// offers from the enabled catalog); it is written **verbatim** (never
    /// floored to the instance default — a blank target is a caller error).
    /// One <c>SaveChangesAsync</c>.
    /// </para>
    /// </summary>
    /// <exception cref="KeyNotFoundException">The announcement id is not
    /// found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor holds none of
    /// the GlobalAdmin / Translator / (targeted community) component-moderator
    /// standings.</exception>
    public async Task<AnnouncementTranslation> AddAnnouncementTranslationAsync(
        string announcementId,
        string languageCode,
        string? title,
        string body,
        string actorId,
        IReadOnlySet<string> actorRoles,
        IDocumentSession session)
    {
        if (string.IsNullOrEmpty(announcementId))
            throw new ArgumentException("An announcement id is required.", nameof(announcementId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("A translation requires a non-empty body.", nameof(body));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(session);

        var announcement = await session.LoadAsync<Announcement>(announcementId).ConfigureAwait(false);
        if (announcement is null)
            throw new KeyNotFoundException($"Announcement '{announcementId}' was not found in the session; nothing to translate.");

        var via = ResolveTranslationStanding(announcement.Scope, announcement.CommunityId, actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                "Only an admin, a translator, or a moderator of the targeted community " +
                "may add a translation of this announcement.");

        var now = DateTimeOffset.UtcNow;
        var translation = new AnnouncementTranslation
        {
            Id = Guid.NewGuid().ToString("N"),
            AnnouncementId = announcementId,
            LanguageCode = languageCode,
            Title = title,
            Body = body,
            AuthorId = actorId,
            Created = now
        };

        // Audit row (the ADR 0022 write-lane precedent — a hand-written audit
        // row with no CanAsync decision call): the Via tag records the
        // standing the actor used (Admin for GlobalAdmin/Translator,
        // Moderator for a targeted community's moderator).
        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "announcementtranslation.add",
            TargetKind = "announcement",
            TargetId = announcementId,
            Via = via.Value,
            Outcome = Authorization.AccessOutcome.Allow
        };

        session.Store(translation);
        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return translation;
    }

    /// <summary>
    /// The public ADR 0029 standing probe the Web layer calls to decide
    /// whether to render the "add a translation" affordance (a
    /// <b>display</b> pin, not a gate — the real deny is the
    /// <see cref="AddAnnouncementTranslationAsync"/> standing check, which
    /// re-runs the same rule server-side). It delegates to the same
    /// <see cref="ResolveTranslationStanding"/> the write lane uses, so the
    /// display and the gate can never drift apart (the
    /// <see cref="Kumunita.Core.Posts.PostService.CanAddTranslation"/> shape).
    /// </summary>
    public static bool CanTranslateAnnouncement(
        AnnouncementScope scope, string? communityId, string actorId, IReadOnlySet<string> actorRoles)
        => ResolveTranslationStanding(scope, communityId, actorId, actorRoles) is not null;

    /// <summary>
    /// The ADR 0029 announcement-translation standing resolver (shared by
    /// <see cref="AddAnnouncementTranslationAsync"/> and
    /// <see cref="CanTranslateAnnouncement"/>). Returns the
    /// <see cref="Authorization.AccessVia"/> the actor qualifies under, or
    /// <c>null</c> to deny. Precedence (most specific standing first, so the
    /// audit row records the narrowest right that applied): a
    /// <see cref="Roles.Translator"/> / <see cref="Roles.GlobalAdmin"/>
    /// (<see cref="Authorization.AccessVia.Admin"/> — instance-wide, the ADR
    /// 0021/0026 standing), and — only when the announcement is
    /// <see cref="AnnouncementScope.Community"/> <em>targeted</em> at one
    /// community — a <see cref="Roles.Moderator"/> scoped to that community
    /// (<see cref="Authorization.AccessVia.Moderator"/>). A flat community
    /// (no target) or a <see cref="AnnouncementScope.Public"/> announcement
    /// has no community to moderate, so the component-moderator branch is
    /// excluded for it.
    /// </summary>
    private static Authorization.AccessVia? ResolveTranslationStanding(
        AnnouncementScope scope, string? communityId, string actorId, IReadOnlySet<string> actorRoles)
    {
        if (actorRoles.Contains(Roles.Translator))
            return Authorization.AccessVia.Admin;

        if (actorRoles.Contains(Roles.GlobalAdmin))
            return Authorization.AccessVia.Admin;

        if (scope == AnnouncementScope.Community
            && communityId is not null
            && actorRoles.Contains(Roles.ModeratorComponent(communityId)))
            return Authorization.AccessVia.Moderator;

        return null;
    }

    /// <summary>
    /// Resolves the actor's read-visibility for announcements: whether they are signed in
    /// (<c>authed</c>), a GlobalAdmin (<c>admin</c> — sees every target), and the set of
    /// communities in which they may see a targeted announcement (their membership set from
    /// <see cref="IUserInfoService.GetCommunityIdsAsync"/> unioned with the communities they
    /// moderate, read from their <c>moderator:{id}</c> standing claims). An anonymous or
    /// GlobalAdmin caller gets an empty community set (the admin flag already authorizes all
    /// targets for them, so the list is irrelevant).
    /// </summary>
    private async Task<(bool authed, bool admin, IReadOnlyList<string> communities)> ResolveReadVisibilityAsync(
        string? actorId,
        IReadOnlySet<string> roles)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return (false, false, new List<string>());

        var admin = roles.Contains(Roles.GlobalAdmin);
        if (admin)
            return (true, admin, new List<string>());

        var communities = new List<string>();
        foreach (var role in roles)
        {
            if (role.StartsWith("moderator:", StringComparison.Ordinal))
                communities.Add(role[Roles.ModeratorComponent("").Length..]);
        }

        var membership = await _userInfo.GetCommunityIdsAsync(actorId).ConfigureAwait(false);
        if (membership is not null)
        {
            foreach (var communityId in membership)
                communities.Add(communityId);
        }

        return (true, false, communities);
    }

    /// <summary>
    /// The <b>create</b> write gate (<see cref="CreateAsync"/> only — the edit lane has its own
    /// <see cref="EnsureEditPermissionAsync"/>). A <see cref="AnnouncementScope.Public"/>
    /// announcement is always platform-wide so it cannot carry a <c>CommunityId</c>; a
    /// <see cref="AnnouncementScope.Community"/> announcement with no <c>CommunityId</c> is the
    /// flat "all residents" target (a GlobalAdmin or Moderator); one with a <c>CommunityId</c>
    /// targets that community and requires a GlobalAdmin or the <c>moderator:{CommunityId}</c>
    /// standing claim, and the target must name a real component. A denied author is
    /// <see cref="UnauthorizedAccessException"/> (mapped to a 403); an invalid shape or unknown
    /// target is an <see cref="ArgumentException"/> (mapped to a 400).
    /// </summary>
    private async Task EnsureCreatePermissionAsync(Announcement announcement, IReadOnlySet<string> authorRoles)
    {
        ArgumentNullException.ThrowIfNull(announcement);
        ArgumentNullException.ThrowIfNull(authorRoles);

        var hasGlobalAdmin = authorRoles.Contains(Roles.GlobalAdmin);
        var hasModerator   = authorRoles.Contains(Roles.Moderator);

        if (announcement.CommunityId is not null && announcement.Scope == AnnouncementScope.Public)
            throw new ArgumentException(
                "A public-scope announcement cannot target a community; clear CommunityId or use the Community scope.",
                nameof(announcement));

        if (announcement.CommunityId is null)
        {
            switch (announcement.Scope)
            {
                case AnnouncementScope.Public when !hasGlobalAdmin:
                    throw new UnauthorizedAccessException("Only a GlobalAdmin may create a public-scope announcement.");

                case AnnouncementScope.Community when !hasGlobalAdmin && !hasModerator:
                    throw new UnauthorizedAccessException("Only a GlobalAdmin or Moderator may create a community-scope announcement.");

                default:
                    break;
            }
            return;
        }

        var target = announcement.CommunityId!;
        var moderatesTarget = authorRoles.Contains(Roles.ModeratorComponent(target));
        if (!hasGlobalAdmin && !moderatesTarget)
            throw new UnauthorizedAccessException(
                $"Only a GlobalAdmin or a moderator of community '{target}' may target that community.");

        var components = await _userInfo.GetComponentsAsync(enabledOnly: true).ConfigureAwait(false);
        if (components is null || !components.Any(c => c.Id == target))
            throw new ArgumentException(
                $"Unknown community '{target}' for the announcement target.",
                nameof(announcement));
    }

    /// <summary>
    /// The <b>edit</b> write gate (<see cref="UpdateAsync"/> only — distinct from
    /// <see cref="EnsureCreatePermissionAsync"/>). Evaluated against the <em>stored</em>
    /// announcement (loaded before the gate, so its <see cref="Announcement.AuthorId"/>
    /// is available):
    /// <list type="bullet">
    /// <item><see cref="AnnouncementScope.Public"/> — the actor must hold
    /// <see cref="Roles.GlobalAdmin"/> (a public notice is platform-wide; no moderator,
    /// however scoped, rewrites one);</item>
    /// <item><see cref="AnnouncementScope.Community"/> with no
    /// <see cref="Announcement.CommunityId"/> (the flat "all residents" target) — the actor
    /// must be the stored <see cref="Announcement.AuthorId"/> or hold
    /// <see cref="Roles.GlobalAdmin"/>. A community moderator who did not author it is
    /// <b>denied</b> — their lever over an announcement they did not write for all
    /// residents is moderation (the delete lane), not rewriting the author's words
    /// (ADR 0017);</item>
    /// <item><see cref="AnnouncementScope.Community"/> with a
    /// <see cref="Announcement.CommunityId"/> — the actor must hold
    /// <see cref="Roles.GlobalAdmin"/> or the <c>moderator:{CommunityId}</c> standing
    /// claim (a community moderator may edit that community's announcement even if a
    /// GlobalAdmin authored it; the author-of-record rule applies to the all-residents
    /// lane only).</item>
    /// </list>
    /// A denied actor is <see cref="UnauthorizedAccessException"/> (mapped to a 403).
    /// Deliberately <em>narrower</em> than the create gate: the base <see cref="Roles.Moderator"/>
    /// claim grants the flat all-residents <em>create</em> lane but never the flat
    /// all-residents <em>edit</em> lane.
    /// </summary>
    private static Task EnsureEditPermissionAsync(Announcement existing, string actorId, IReadOnlySet<string> actorRoles)
    {
        ArgumentNullException.ThrowIfNull(existing);
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);

        var isGlobalAdmin = actorRoles.Contains(Roles.GlobalAdmin);

        if (existing.CommunityId is null)
        {
            // Flat "all residents": Public → GlobalAdmin only; Community → author ∪ GlobalAdmin.
            if (existing.Scope == AnnouncementScope.Public)
            {
                if (!isGlobalAdmin)
                    throw new UnauthorizedAccessException("Only a GlobalAdmin may edit a public-scope announcement.");
            }
            else if (!isGlobalAdmin && existing.AuthorId != actorId)
            {
                throw new UnauthorizedAccessException(
                    "Only the author or a GlobalAdmin may edit an announcement for all residents.");
            }
            return Task.CompletedTask;
        }

        // Community-targeted: GlobalAdmin or that community's moderator.
        if (!isGlobalAdmin && !actorRoles.Contains(Roles.ModeratorComponent(existing.CommunityId)))
            throw new UnauthorizedAccessException(
                $"Only a GlobalAdmin or a moderator of community '{existing.CommunityId}' may edit that community's announcement.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// <b>Reverse-lookup</b> read seam (RC U03, R·5): the first announcement
    /// (by <c>Created</c> ascending) whose <see cref="Announcement.ImageIds"/>
    /// contains <paramref name="mediaId"/> — the serving route's owner
    /// resolution. **Un-audited** (RC R·5); announcements are not
    /// audience-restricted, so there is no <c>AccessAudit</c> lane on this
    /// bounded context (the same "no audit row" reasoning as
    /// <see cref="ListVisibleAsync"/>). Null when no announcement references
    /// the id (the route 404s). Read-only — no write lane, no <c>actorId</c>.
    /// </summary>
    public async Task<Announcement?> FindByImageIdAsync(string mediaId)
    {
        ArgumentNullException.ThrowIfNull(mediaId);
        if (mediaId.Length == 0) return null;

        await using var session = _store.QuerySession();
        return await session
            .Query<Announcement>()
            .Where(a => a.ImageIds.Contains(mediaId))
            .OrderBy(a => a.Created)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// <b>Reverse-lookup</b> read seam (ATT U3, C-ATT·4): the first announcement
    /// (by <c>Created</c> ascending) whose <see cref="Announcement.AttachmentIds"/>
    /// contains <paramref name="mediaId"/> — the attachment serving route's owner
    /// resolution. **Un-audited**; announcements are not audience-restricted,
    /// so there is no <c>AccessAudit</c> lane on this bounded context (the same
    /// "no audit row" reasoning as <see cref="ListVisibleAsync"/>). Null when no
    /// announcement references the id (the route 404s). Read-only — no write
    /// lane, no <c>actorId</c>.
    /// </summary>
    public async Task<Announcement?> FindByAttachmentIdAsync(string mediaId)
    {
        ArgumentNullException.ThrowIfNull(mediaId);
        if (mediaId.Length == 0) return null;

        await using var session = _store.QuerySession();
        return await session
            .Query<Announcement>()
            .Where(a => a.AttachmentIds.Contains(mediaId))
            .OrderBy(a => a.Created)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
    }
}
