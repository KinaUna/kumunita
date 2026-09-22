namespace Kumunita.Core.UserInfo;

// ── ADR 0026 — group & community name/description translations ─────────────
//
// The "separate feature" ADR 0018 (posts/replies, shipped as ADR 0022) and
// ADR 0021 (the Translator role's scope boundary) both deferred: a human
// authoring a translation of a *group's* or a *community's* name + description
// into another supported language. These two documents are that lane, in the
// UserInfo bounded context (the context that owns Group and Component).
//
// They mirror Kumunita.Core.Posts.PostTranslation exactly (the ADR 0022 shape),
// with the one documented difference: both fields are optional-but-at-least-one
// required (a post/reply translation requires a body; a name/description
// translation may carry just the name, just the description, or both — the
// "at least one non-blank" rule is enforced by the UserInfoService write seam).

/// <summary>
/// A **user-added translation** of a <see cref="Group"/>'s name and/or
/// description into a language other than the one it was authored in (ADR
/// 0026; the "separate, later feature" ADR 0021's scope boundary deferred, and
/// the same user-authored-not-machine-translated lane ADR 0022 shipped for
/// posts and replies).
/// <para>
/// One row per (group, language) pair — the <c>(GroupId, LanguageCode)</c>
/// unique index (<see cref="M1DocTypes.Configure"/>) enforces that at the
/// database layer (Marten's document identity is the surrogate <see cref="Id"/>,
/// the same <see cref="GroupMembership"/> business-key convention as M1). The
/// translation carries its own optional <see cref="Name"/> and optional
/// <see cref="Description"/> (at least one non-blank — a translation with
/// nothing in it is rejected by the write seam, not the shape).
/// <para>
/// **Standing (ADR 0026):** a translation is added by the group's
/// <b>owner</b> (an <see cref="Authorization.AccessVia.Owner"/> audit tag) or a
/// <b>GlobalAdmin</b> / <b>Translator</b> (an <see cref="Authorization
/// .AccessVia.Admin"/> audit tag). A group **member** is not a standing
/// (membership is not the right to rename the group for others); a
/// component-moderator claim does not qualify (the group lane has no
/// component-moderator standing, ADR 0007). The decision + its
/// <see cref="Authorization.AccessAudit"/> row are written by
/// <see cref="UserInfoService.AddGroupTranslationAsync"/> in the caller's
/// transaction (C3).
/// <para>
/// **Not an authorization surface (the ADR 0022 read-pin carried over):** like
/// its parent group, a translation has <b>no own audience</b> — its visibility
/// inherits the group's owner∪member reach. A translation row that is not
/// under a group the viewer may reach is simply unreachable (the Web reads it
/// only after the group's detail gate passed).
/// <para>
/// Add-only lane (ADR 0026, mirroring ADR 0022): there is no edit/delete here —
/// re-adding a language overwrites that row; the lane today is "add a
/// translation of a language the group does not yet have."
/// </para>
/// </summary>
public sealed class GroupTranslation
{
    public string Id { get; set; } = string.Empty;

    /// <summary>The parent <see cref="Group"/> this translation renders.</summary>
    public string GroupId { get; set; } = string.Empty;

    /// <summary>
    /// The BCP-47 code of the language this translation is <b>in</b> (a
    /// <see cref="Kumunita.Core.Localization.LanguageCatalog.Id"/> — a
    /// supported, enabled language).
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>The translated name (optional — the group's
    /// <see cref="Group.Name"/> is the fallback when this is absent).</summary>
    public string? Name { get; set; }

    /// <summary>The translated description (optional — the group's
    /// <see cref="Group.Description"/> is the fallback when this is absent).
    /// At least one of <see cref="Name"/> / <see cref="Description"/> must be
    /// non-blank (the write seam enforces it, not the shape).</summary>
    public string? Description { get; set; }

    /// <summary>The subject id of the actor who added the translation.</summary>
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>When the translation was added (the initial — and, on this
    /// add-only lane, only) timestamp.</summary>
    public DateTimeOffset Created { get; set; }
}

/// <summary>
/// A **user-added translation** of a <see cref="Component"/> (community)'s
/// name and/or description into a language other than the one it was authored
/// in (ADR 0026; the same user-authored-not-machine-translated lane ADR 0022
/// shipped for posts and replies, extended to the community name/description
/// that ADR 0021's scope boundary deferred).
/// <para>
/// One row per (community, language) pair — the
/// <c>(ComponentId, LanguageCode)</c> unique index
/// (<see cref="M1DocTypes.Configure"/>) enforces that at the database layer
/// (Marten's document identity is the surrogate <see cref="Id"/>, the same
/// <see cref="ComponentMembership"/> business-key convention as M1). The
/// translation carries its own optional <see cref="Name"/> and optional
/// <see cref="Description"/> (at least one non-blank — a translation with
/// nothing in it is rejected by the write seam, not the shape).
/// <para>
/// **Standing (ADR 0026):** a translation is added by a <b>GlobalAdmin</b> or
/// a <b>Translator</b> (both an <see cref="Authorization.AccessVia.Admin"/>
/// audit tag). A community has **no owner** — its name/description is written
/// by a GlobalAdmin (the <c>/admin</c> create/update lane, <c>Via: Admin</c>),
/// so the component-moderator standing does not qualify (a moderator governs a
/// community's *members*, ADR 0012, not its name). The decision + its
/// <see cref="Authorization.AccessAudit"/> row are written by
/// <see cref="UserInfoService.AddCommunityTranslationAsync"/> in the caller's
/// transaction (C3).
/// <para>
/// **Not an authorization surface (the ADR 0022 read-pin carried over):** like
/// its parent community, a translation has <b>no own audience</b> — its
/// visibility inherits the community's enabled visibility. A translation row
/// that is not under an enabled community is simply unreachable (the Web reads
/// it only after the manage-page standing gate passed).
/// <para>
/// Add-only lane (ADR 0026, mirroring ADR 0022): there is no edit/delete here —
/// re-adding a language overwrites that row; the lane today is "add a
/// translation of a language the community does not yet have."
/// </para>
/// </summary>
public sealed class CommunityTranslation
{
    public string Id { get; set; } = string.Empty;

    /// <summary>The parent <see cref="Component"/> this translation renders.</summary>
    public string ComponentId { get; set; } = string.Empty;

    /// <summary>
    /// The BCP-47 code of the language this translation is <b>in</b> (a
    /// <see cref="Kumunita.Core.Localization.LanguageCatalog.Id"/> — a
    /// supported, enabled language).
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>The translated name (optional — the community's
    /// <see cref="Component.Name"/> is the fallback when this is absent).</summary>
    public string? Name { get; set; }

    /// <summary>The translated description (optional — the community's
    /// <see cref="Component.Description"/> is the fallback when this is absent).
    /// At least one of <see cref="Name"/> / <see cref="Description"/> must be
    /// non-blank (the write seam enforces it, not the shape).</summary>
    public string? Description { get; set; }

    /// <summary>The subject id of the actor who added the translation.</summary>
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>When the translation was added (the initial — and, on this
    /// add-only lane, only) timestamp.</summary>
    public DateTimeOffset Created { get; set; }
}
