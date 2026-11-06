using Kumunita.Core.Authorization;
using Marten;

namespace Kumunita.Core.Translations;

/// <summary>
/// The **one shared translation-variant write lane** (ADR [0288](../../../docs/adr/0288-one-translation-variant-lane.md)) —
/// the three write operations every translatable surface shares: <see cref="AddAsync"/> /
/// <see cref="UpdateAsync"/> / <see cref="RemoveAsync"/> over the <c>(parent, language)</c> pair the
/// per-surface unique index enforces. Each does the same six steps, defined once here: validate the
/// inputs → load the parent (missing → <see cref="KeyNotFoundException"/>) → re-run the standing
/// server-side (a denied actor throws <see cref="UnauthorizedAccessException"/> before anything is
/// stored) → create / update / delete the row (the descriptor's surface-specific shape) → write the
/// hand-written <see cref="AccessAudit"/> row (<see cref="AccessAuditFactory.SingleTarget"/>) →
/// commit atomically (one <see cref="IDocumentSession.SaveChangesAsync"/>).
/// <para>
/// <b>Shape (ADR [0288](../../../docs/adr/0288-one-translation-variant-lane.md)).</b> The lane is a
/// <see langword="sealed"/> <c>record</c> — a D6-legal cross-module target (a <c>record</c>, not a
/// concrete <c>class</c>, so a surface referencing it is not a module-boundary violation; the
/// C05 <c>ContentTargetResolver</c> precedent). It is generic over
/// <typeparamref name="TParent"/> (the parent aggregate) and
/// <typeparamref name="TVar"/> (the <c>Translation</c> document row) and carries a
/// <see cref="TranslationDescriptor{TParent,TVar}"/> — a small record of plain functions + strings
/// that names the surface-specific data (target kind, audit action prefix, the standing matrix,
/// and the four document operations). A new translatable surface adds a descriptor (a few lines),
/// not a copied day of work.
/// </para>
/// <para>
/// <b>What the lane owns (ADR [0288](../../../docs/adr/0288-one-translation-variant-lane.md)).</b> The shared
/// invariants, defined once here: the <c>(parent, language)</c> key shape; the
/// <c>KeyNotFoundException</c> on a missing parent or row; the
/// <c>UnauthorizedAccessException</c> on a denied actor (the standing is re-run
/// server-side — the C3 single-source pin, so the Web display pin and this gate can never
/// drift); the hand-written <see cref="AccessAudit"/> row via
/// <see cref="AccessAuditFactory.SingleTarget"/> (C3 — the audit commits atomically with the
/// domain write, one <c>SaveChangesAsync</c>); and the language validation (a blank target is a
/// caller error). The surface-specific quirks — the standing matrix, the
/// blank-title-normalize on add (ADR [0059](../../../docs/adr/0059-event-translations.md) events) vs. verbatim
/// (ADR [0022](../../../docs/adr/0022-user-added-post-reply-translations.md) posts), and the update restamp
/// (events re-record <c>AuthorId</c> / <c>Created</c>) — live in the descriptor's functions,
/// verbatim per surface.
/// </para>
/// <para>
/// <b>Session ownership.</b> The lane operates on the <see cref="IDocumentSession"/> the service
/// passes it — the service owns the session (its own C3 session, the <see cref="EventService"/> shape,
/// or the caller's, the <see cref="PostService"/> shape) and the lane's <c>SaveChangesAsync</c> is
/// the single write. The lane never opens a session itself and never knows about an
/// <see cref="IDocumentStore"/>.
/// </para>
/// <para>
/// <b>What the lane does NOT own.</b> The read seams (<c>Get*TranslationsAsync</c>) stay in each
/// service: their shape is surface-specific (the <see cref="Posts.PostService"/> reply read is a
/// batched <c>(replyIds)</c> query, the others a single-parent self-<c>QuerySession</c>), so forcing
/// them through a per-parent lane would manufacture uniformity the data does not have (the part-vs-
/// whole test — a type that meets none of the part criteria is ceremony). The lane is the
/// <b>write</b> seam, which is the shape E6's "copied per surface" names.
/// </para>
/// </summary>
/// <typeparam name="TParent">The parent aggregate the translation renders
/// (<see cref="Posts.Post"/>, <see cref="Events.Event"/>, <see cref="Announcements.Announcement"/>,
/// <see cref="Pages.Page"/>, <see cref="UserInfo.Group"/>, …).</typeparam>
/// <typeparam name="TVar">The <c>Translation</c> document row
/// (<see cref="Posts.PostTranslation"/>, <see cref="Events.EventTranslation"/>, …).</typeparam>
public sealed record TranslationLane<TParent, TVar>
    where TVar : class
{
    private readonly TranslationDescriptor<TParent, TVar> _d;

    public TranslationLane(TranslationDescriptor<TParent, TVar> descriptor)
    {
        _d = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }

    /// <summary>
    /// **Adds** a translation of <paramref name="parentId"/> into
    /// <paramref name="languageCode"/> in the <paramref name="session"/> (the surface's
    /// <c>Add*TranslationAsync</c>; ADR [0288](../../../docs/adr/0288-one-translation-variant-lane.md)).
    /// Loads the parent, re-runs the standing server-side (a denied actor throws
    /// <see cref="UnauthorizedAccessException"/> before anything is stored), creates the row
    /// (the descriptor's <c>Create</c> applies the surface's title/body shape), writes the
    /// <see cref="AccessAudit"/> row (<c>{ActionPrefix}.add</c>), and commits atomically (one
    /// <see cref="IDocumentSession.SaveChangesAsync"/>).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="parentId"/> /
    /// <paramref name="languageCode"/> / <paramref name="body"/> / <paramref name="actorId"/> is
    /// blank (a translation with no body is not a translation).</exception>
    /// <exception cref="KeyNotFoundException">The parent id is not found in the session.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor holds none of the surface's
    /// standing (its descriptor's <c>ResolveStanding</c> returned <c>null</c>).</exception>
    public async Task<TVar> AddAsync(
        IDocumentSession session,
        string parentId,
        string languageCode,
        string? title,
        string? body,
        string actorId,
        IReadOnlySet<string> actorRoles)
    {
        if (session is null) throw new ArgumentNullException(nameof(session));
        if (string.IsNullOrEmpty(parentId)) throw new ArgumentException("A parent id is required.", nameof(parentId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (_d.ValidateContent is null)
            ValidateContentDefault(title, body);
        else
            _d.ValidateContent(title, body);
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);

        var parent = await _d.LoadParent(session, parentId).ConfigureAwait(false);
        if (parent is null)
            throw new KeyNotFoundException($"{_d.Noun} '{parentId}' was not found in the session; nothing to translate.");

        var via = _d.ResolveStanding(parent, await _d.LoadStandingExtra(session, parent).ConfigureAwait(false), actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException($"Only a standing-holder of the {_d.Noun} may add a translation of it.");

        var row = _d.Create(parentId, languageCode, title, body, actorId, DateTimeOffset.UtcNow);
        session.Store(row);
        session.Store(AccessAuditFactory.SingleTarget(actorId, _d.ActionPrefix + ".add", _d.TargetKind, parentId, via.Value));
        await session.SaveChangesAsync().ConfigureAwait(false);
        return row;
    }

    /// <summary>
    /// The default content validation (applied when the descriptor does not
    /// supply its own <see cref="TranslationDescriptor{TParent,TVar}.ValidateContent"/>):
    /// at least one of <paramref name="title"/> or <paramref name="body"/> must
    /// be non-blank — a translation with neither is not a translation.
    /// </summary>
    private static void ValidateContentDefault(string? title, string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("A translation requires a non-empty body.", nameof(body));
    }

    /// <summary>
    /// **Updates** the existing row for (<paramref name="parentId"/>,
    /// <paramref name="languageCode"/>) in the <paramref name="session"/> (the surface's
    /// <c>Update*TranslationAsync</c>; ADR [0048](../../../docs/adr/0048-translation-edit-and-remove-lanes.md)
    /// edit lane, ADR [0288](../../../docs/adr/0288-one-translation-variant-lane.md)). Loads the parent (missing
    /// → <see cref="KeyNotFoundException"/>), re-runs the standing, loads the
    /// <c>(parentId, languageCode)</c> row (missing → <see cref="KeyNotFoundException"/> — a
    /// shape error for this route; the Web offers it only for languages that have a row),
    /// applies the surface's <c>ApplyUpdate</c> (the title/body shape + any restamp), writes the
    /// <see cref="AccessAudit"/> row (<c>{ActionPrefix}.update</c>), and commits atomically.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The parent or the <c>(parentId, languageCode)</c>
    /// row is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor holds none of the surface's
    /// standing.</exception>
    public async Task<TVar> UpdateAsync(
        IDocumentSession session,
        string parentId,
        string languageCode,
        string? title,
        string? body,
        string actorId,
        IReadOnlySet<string> actorRoles)
    {
        if (session is null) throw new ArgumentNullException(nameof(session));
        if (string.IsNullOrEmpty(parentId)) throw new ArgumentException("A parent id is required.", nameof(parentId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (_d.ValidateContent is null)
            ValidateContentDefault(title, body);
        else
            _d.ValidateContent(title, body);
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);

        var parent = await _d.LoadParent(session, parentId).ConfigureAwait(false);
        if (parent is null)
            throw new KeyNotFoundException($"{_d.Noun} '{parentId}' was not found in the session; nothing to update.");

        var via = _d.ResolveStanding(parent, await _d.LoadStandingExtra(session, parent).ConfigureAwait(false), actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException($"Only a standing-holder of the {_d.Noun} may edit a translation of it.");

        var row = await _d.Find(session, parentId, languageCode).ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException($"{_d.Noun} '{parentId}' has no translation in '{languageCode}'; nothing to update.");

        _d.ApplyUpdate(row, title, body, actorId, DateTimeOffset.UtcNow);
        session.Store(row);
        session.Store(AccessAuditFactory.SingleTarget(actorId, _d.ActionPrefix + ".update", _d.TargetKind, parentId, via.Value));
        await session.SaveChangesAsync().ConfigureAwait(false);
        return row;
    }

    /// <summary>
    /// **Removes** the existing row for (<paramref name="parentId"/>,
    /// <paramref name="languageCode"/>) in the <paramref name="session"/> (the surface's
    /// <c>Remove*TranslationAsync</c>; ADR [0048](../../../docs/adr/0048-translation-edit-and-remove-lanes.md)
    /// remove lane, ADR [0288](../../../docs/adr/0288-one-translation-variant-lane.md)). Loads the parent (missing
    /// → <see cref="KeyNotFoundException"/>), re-runs the standing, loads the row (missing →
    /// <see cref="KeyNotFoundException"/>), hard-deletes it (the trail is the
    /// <see cref="AccessAudit"/> row written in the same session — ADR [0024](../../../docs/adr/0024-author-soft-delete-lane.md)'s
    /// soft-delete does not apply: a translation has no children to keep visible, and a
    /// soft-delete would block re-adding the same language via the unique index), writes the
    /// audit row (<c>{ActionPrefix}.remove</c>), and commits atomically.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The parent or the <c>(parentId, languageCode)</c>
    /// row is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor holds none of the surface's
    /// standing.</exception>
    public async Task RemoveAsync(
        IDocumentSession session,
        string parentId,
        string languageCode,
        string actorId,
        IReadOnlySet<string> actorRoles)
    {
        if (session is null) throw new ArgumentNullException(nameof(session));
        if (string.IsNullOrEmpty(parentId)) throw new ArgumentException("A parent id is required.", nameof(parentId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);

        var parent = await _d.LoadParent(session, parentId).ConfigureAwait(false);
        if (parent is null)
            throw new KeyNotFoundException($"{_d.Noun} '{parentId}' was not found in the session; nothing to remove.");

        var via = _d.ResolveStanding(parent, await _d.LoadStandingExtra(session, parent).ConfigureAwait(false), actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException($"Only a standing-holder of the {_d.Noun} may remove a translation of it.");

        var row = await _d.Find(session, parentId, languageCode).ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException($"{_d.Noun} '{parentId}' has no translation in '{languageCode}'; nothing to remove.");

        session.Delete(row);
        session.Store(AccessAuditFactory.SingleTarget(actorId, _d.ActionPrefix + ".remove", _d.TargetKind, parentId, via.Value));
        await session.SaveChangesAsync().ConfigureAwait(false);
    }
}

/// <summary>
/// The **per-surface descriptor** for the shared <see cref="TranslationLane{TParent,TVar}"/>
/// (ADR [0288](../../../docs/adr/0288-one-translation-variant-lane.md)) — data, not a class hierarchy.
/// Names the surface-specific pieces the lane composes: the audit <see cref="TargetKind"/> and
/// <see cref="ActionPrefix"/>, the <see cref="Noun"/> (the error-message noun), and the four
/// document operations + the standing matrix as plain functions. Every function is defined in the
/// surface's module (where <typeparamref name="TParent"/> / <typeparamref name="TVar"/> are
/// concrete), so the surface keeps its own document access and standing decision; the lane supplies
/// the shared shape (validation, the audit row, the atomic commit) once.
/// <para>
/// <b>The surface quirks live here, explicitly (ADR [0288](../../../docs/adr/0288-one-translation-variant-lane.md)):</b>
/// <see cref="ResolveStanding"/> is the surface's standing matrix (posts'
/// author / (community) component-moderator / GlobalAdmin — ADR [0022](../../../docs/adr/0022-user-added-post-reply-translations.md);
/// events' author / Translator / GlobalAdmin — ADR [0059](../../../docs/adr/0059-event-translations.md); groups'
/// owner / Translator / GlobalAdmin — ADR [0026](../../../docs/adr/0026-group-community-name-description-translations.md));
/// <see cref="Create"/> applies the surface's title/body shape (events normalize a blank title —
/// ADR [0059](../../../docs/adr/0059-event-translations.md) — posts write it verbatim —
/// ADR [0022](../../../docs/adr/0022-user-added-post-reply-translations.md)); and <see cref="ApplyUpdate"/>
/// applies the surface's update shape (events re-record <c>AuthorId</c> / <c>Created</c> —
/// ADR [0059](../../../docs/adr/0059-event-translations.md)).
/// </para>
/// </summary>
public sealed record TranslationDescriptor<TParent, TVar>
    where TVar : class
{
    /// <summary>The <see cref="AccessAudit"/> <c>TargetKind</c> string the surface's rows already
    /// emit (the exact string the surface's <c>*ToAuditableResource</c> / audit rows use, e.g.
    /// "post", "event", "announcement").</summary>
    public required string TargetKind { get; init; }

    /// <summary>The <see cref="AccessAudit"/> <c>Action</c> prefix the surface uses
    /// (the lane appends ".add" / ".update" / ".remove"; e.g. "posttranslation" →
    /// "posttranslation.add").</summary>
    public required string ActionPrefix { get; init; }

    /// <summary>The error-message noun for the surface ("post", "event", "announcement", …).</summary>
    public required string Noun { get; init; }

    /// <summary>
    /// Loads the extra document the standing needs, if any (the surface's own document
    /// access). The single-document surfaces return <c>null</c>; the
    /// <see cref="Posts.PostReply"/> lane loads the parent <see cref="Posts.Post"/> (the reply's
    /// standing reads the parent's group/component scope — C-M3·1 — the reply itself has no
    /// component) and throws <see cref="KeyNotFoundException"/> when it is absent, mirroring the
    /// reply's original standing re-check.
    /// </summary>
    public required Func<IDocumentSession, TParent, Task<object?>> LoadStandingExtra { get; init; }

    /// <summary>
    /// The surface's standing matrix (ADR [0288](../../../docs/adr/0288-one-translation-variant-lane.md)) — a pure function of
    /// the loaded <paramref name="parent"/> (and the optional <paramref name="extra"/> document),
    /// returning the <see cref="AccessVia"/> the actor qualifies under, or <c>null</c> to deny.
    /// Defined in the surface's module (it may delegate to the surface's
    /// <c>*StandingPolicy</c> / <c>*TranslationPolicy</c> static — the C3 single-source pin, so
    /// the display and the gate can never drift). The reply lane reads the
    /// <paramref name="extra"/> (parent post) scope and the <paramref name="parent"/> (reply)
    /// <c>AuthorId</c>; the single-document surfaces read the <paramref name="parent"/>.
    /// </summary>
    public required Func<TParent, object?, string, IReadOnlySet<string>, AccessVia?> ResolveStanding { get; init; }

    /// <summary>Loads the parent by id in the given session (<c>null</c> when absent).</summary>
    public required Func<IDocumentSession, string, Task<TParent?>> LoadParent { get; init; }

    /// <summary>The <c>(parentId, languageCode)</c> row, or <c>null</c> when absent.</summary>
    public required Func<IDocumentSession, string, string, Task<TVar?>> Find { get; init; }

    /// <summary>
    /// Creates the new <typeparamref name="TVar"/> row (the surface's <c>new *Translation{…}</c>
    /// shape — its title/body normalization, its <c>Id</c> = <c>Guid.NewGuid().ToString("N")</c>).
    /// <paramref name="created"/> is the lane's <c>DateTimeOffset.UtcNow</c>.
    /// </summary>
    public required Func<string, string, string?, string?, string, DateTimeOffset, TVar> Create { get; init; }

    /// <summary>
    /// Applies the update to an existing row (the surface's field writes + restamp quirk;
    /// <paramref name="now"/> is the lane's <c>DateTimeOffset.UtcNow</c>).
    /// </summary>
    public required Action<TVar, string?, string?, string, DateTimeOffset> ApplyUpdate { get; init; }

    /// <summary>
    /// The surface's content validation for add/update (called by the lane before
    /// the standing check; throws <see cref="ArgumentException"/> on a content
    /// violation). When <c>null</c> (the default for the four surfaces with a
    /// non-null <c>Body</c> — posts / events / announcements / pages), the lane
    /// uses its built-in "a translation requires a non-empty body" check.
    /// Surfaces with a different content contract (the projects family's
    /// "a translation requires a title <i>or</i> a body", both nullable)
    /// supply their own validation here.
    /// </summary>
    public Action<string?, string?>? ValidateContent { get; init; }
}
