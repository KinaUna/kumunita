using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;

namespace Kumunita.Core.UserInfo;

/// <summary>
/// The UserInfoModule's community-management surface (ADR 0286): community
/// CRUD, member lanes, and community translations.
/// Split from <see cref="IUserInfoService"/> (C07, IMPROVE-CODE E4).
/// </summary>

public interface ICommunityMembershipService
{
    /// <summary>
    /// Create a new community (a <see cref="Component"/> row) for the
    /// <c>/admin</c> "add community" form. The <c>id</c> is derived from
    /// <paramref name="name"/> (slug + short suffix) — the controller
    /// doesn't mint ids itself. Defaults: <see cref="Component.Enabled"/>
    /// <c>true</c>, <see cref="Component.ModeratorAccess"/>
    /// <c>false</c> (invariant C5's OFF-by-default), <see cref="Component.
    /// SortOrder"/> = one past the current maximum (or 0 when there are
    /// none yet) so the new row lands at the end of the existing list.
    /// Appends an <see cref="Authorization.AccessAudit"/> row (action
    /// "community.add", targetKind "component", via Admin, outcome Allow)
    /// in the same session/transaction as the row (invariant C3).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <paramref name="actorId"/> is null/whitespace.</exception>
    Task<Component> CreateCommunityAsync(string name, string? description, string actorId);

    /// <summary>
    /// Patch an existing community (a <see cref="Component"/> row) from
    /// the <c>/admin</c> "edit community" form. **Null arguments are
    /// "keep as-is"** (the form's contract: an untouched field doesn't
    /// erase the current value). <see cref="Component.Id"/> itself is
    /// never changed here — identity is identity. Appends an audit row
    /// (action "community.update", targetKind "component", via Admin,
    /// outcome Allow) in the same transaction as the row (invariant C3).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="componentId"/> or <paramref name="actorId"/> is null/whitespace.</exception>
    /// <exception cref="InvalidOperationException">No component with that id exists.</exception>
    Task UpdateCommunityAsync(string componentId,
        string? name, string? description,
        int? sortOrder, bool? moderatorAccess, bool? enabled,
        string actorId);

    /// <summary>
    /// Enable or disable an existing community (the <c>/admin</c>
    /// "remove a community" form — the user-chosen hide, not
    /// a delete: this sets <see cref="Component.Enabled"/> so the
    /// row, its posts, and any moderator assignments remain intact;
    /// the <c>/community/{id}</c> feed 404s for disabled rows
    /// (the read path's <c>enabledOnly</c> filter handles this)).
    /// Appends an audit row (action "community.toggle-enabled",
    /// targetKind "component", via Admin, outcome Allow) in the
    /// same transaction as the flag change (invariant C3).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="componentId"/> is null/whitespace.</exception>
    /// <exception cref="InvalidOperationException">No component with that id exists.</exception>
    Task SetCommunityEnabledAsync(string componentId, bool enabled, string actorId);

    /// <summary>
    /// The <b>membership rows</b> of a single community (a
    /// <see cref="Component"/> row) — the manage-page member list (ADR
    /// 0012) and the candidate source for the add picker. The
    /// <see cref="GetGroupMembersAsync"/> analog on the component axis: a
    /// *candidate projection*, not an access decision — no
    /// <see cref="Authorization.AccessAudit"/> row. Strong-consistency live
    /// rows (C4): an add/remove in the same commit is live on the very next
    /// call. Note this returns the **explicit rows only** — a mandatory
    /// community's implicit members are *all* residents, which no list
    /// enumerates; the manage surface shows the flag instead (the view's
    /// job, not this read's).
    /// </summary>
    Task<IReadOnlyList<ComponentMembership>> GetCommunityMembersAsync(string componentId);

    /// <summary>
    /// The <b>read</b> seam for a community's user-added name/description
    /// translations (ADR 0026): the <see cref="CommunityTranslation"/> rows
    /// under <paramref name="componentId"/>. Owns its <c>QuerySession</c> (the
    /// C3 read-lane shape). **Not an authorization surface** (the ADR 0022
    /// "a read, not a decision" pin carried over): the row has no own audience
    /// — its visibility inherits the community's enabled visibility, which the
    /// caller has already made — so this writes **no** audit row.
    /// </summary>
    Task<IReadOnlyList<CommunityTranslation>> GetCommunityTranslationsAsync(string componentId);

    /// <summary>
    /// Adds a **user-added translation** of a community's name and/or
    /// description into <paramref name="languageCode"/> (ADR 0026). In the
    /// caller's in-flight session (C3). **At least one** of
    /// <paramref name="name"/> / <paramref name="description"/> must be
    /// non-blank (an <see cref="ArgumentException"/> otherwise).
    /// <paramref name="languageCode"/> is the target language, written verbatim.
    /// <b>Standing:</b> a <b>GlobalAdmin</b> or a <b>Translator</b> (both
    /// <see cref="Authorization.AccessVia.Admin"/>); a community has no owner,
    /// so no <see cref="Authorization.AccessVia.Owner"/> branch, and a
    /// component-moderator is denied (a moderator governs a community's
    /// *members*, ADR 0012, not its name). A denied actor throws
    /// <see cref="UnauthorizedAccessException"/> before anything is stored.
    /// One <c>SaveChangesAsync</c>.
    /// </summary>
    /// <exception cref="ArgumentException">A blank target language, a blank
    /// actor, or both name and description blank.</exception>
    /// <exception cref="KeyNotFoundException">The component id is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor holds neither
    /// the GlobalAdmin nor the Translator standing.</exception>
    Task<CommunityTranslation> AddCommunityTranslationAsync(
        string componentId, string languageCode, string? name, string? description,
        string actorId, IReadOnlySet<string> actorRoles, Marten.IDocumentSession session);

    /// <summary>
    /// Session-owning overload (C2-04 — the Web controller stops opening its own
    /// session; the service owns the write + the single <c>SaveChangesAsync</c>).
    /// Identical behaviour to the <c>IDocumentSession</c> overload except the
    /// service opens the <c>LightweightSession</c> internally (the C3 single-
    /// write-lane is unchanged).
    /// </summary>
    Task<CommunityTranslation> AddCommunityTranslationAsync(
        string componentId, string languageCode, string? name, string? description,
        string actorId, IReadOnlySet<string> actorRoles);

    /// <summary>
    /// **Updates** an existing user-added translation of a community's name
    /// and/or description (ADR 0048). Standing is the same as
    /// <see cref="AddCommunityTranslationAsync"/> (a GlobalAdmin or a
    /// Translator); a denied actor throws <see cref="UnauthorizedAccessException"/>;
    /// a missing component or row is a <see cref="KeyNotFoundException"/>. At
    /// least one of name / description must be non-blank. One
    /// <c>SaveChangesAsync</c>; a hand-written <see cref="Authorization
    /// .AccessAudit"/> row (action <c>communitytranslation.update</c>) is
    /// stored in the caller's session.
    /// </summary>
    Task<CommunityTranslation> UpdateCommunityTranslationAsync(
        string componentId, string languageCode, string? name, string? description,
        string actorId, IReadOnlySet<string> actorRoles, Marten.IDocumentSession session);

    /// <summary>
    /// Session-owning overload (C2-04 — the Web controller stops opening its own
    /// session; the service owns the write + the single <c>SaveChangesAsync</c>).
    /// Identical behaviour to the <c>IDocumentSession</c> overload except the
    /// service opens the <c>LightweightSession</c> internally.
    /// </summary>
    Task<CommunityTranslation> UpdateCommunityTranslationAsync(
        string componentId, string languageCode, string? name, string? description,
        string actorId, IReadOnlySet<string> actorRoles);

    /// <summary>
    /// **Removes** an existing user-added translation of a community's name
    /// and/or description (ADR 0048). Standing is the same as
    /// <see cref="AddCommunityTranslationAsync"/>; a denied actor throws
    /// <see cref="UnauthorizedAccessException"/>; a missing component or row is
    /// a <see cref="KeyNotFoundException"/>. One <c>SaveChangesAsync</c>; a
    /// hand-written <see cref="Authorization.AccessAudit"/> row (action
    /// <c>communitytranslation.remove</c>) is stored in the caller's session.
    /// </summary>
    Task RemoveCommunityTranslationAsync(
        string componentId, string languageCode,
        string actorId, IReadOnlySet<string> actorRoles, Marten.IDocumentSession session);

    /// <summary>
    /// Session-owning overload (C2-04 — the Web controller stops opening its own
    /// session; the service owns the write + the single <c>SaveChangesAsync</c>).
    /// Identical behaviour to the <c>IDocumentSession</c> overload except the
    /// service opens the <c>LightweightSession</c> internally.
    /// </summary>
    Task RemoveCommunityTranslationAsync(
        string componentId, string languageCode,
        string actorId, IReadOnlySet<string> actorRoles);

    /// <summary>
    /// The public ADR 0026 standing probe the Web layer calls to decide
    /// whether to render the community "add a translation" affordance (a
    /// <b>display</b> pin, not a gate — the real deny is
    /// <see cref="AddCommunityTranslationAsync"/> standing check, which re-runs
    /// the same rule server-side). Delegates to the same resolver the write
    /// lane uses, so the display and the gate can never drift (ADR 0006-D).
    /// <b>Standing:</b> a GlobalAdmin or a Translator — a community has no
    /// owner, so no Owner branch, and a component-moderator is not.
    /// </summary>
    bool CanTranslateCommunity(string actorId, IReadOnlySet<string> actorRoles);

}
