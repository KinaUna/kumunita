using Kumunita.Core.Tags;

namespace Kumunita.Core.UserInfo;

/// <summary>
/// M23 (ADR 0123 D4) — the "find people with something in common" read (D6, the
/// ADR 0044 D5 privacy-pin carried to profiles): the by-profile-tag +
/// bio-substring finds over the <b>actor-visible</b> resident set. A
/// <b>composition service</b> in the <c>UserInfo</c> bounded context (the
/// <see cref="DirectoryService"/> shape, ADR 0006-D) — it composes <b>only</b>
/// the frozen seams (<see cref="IUserInfoService"/> for the candidate set,
/// <c>Kumunita.Core.Authorization.IAuthorizationService</c>.<c>CanSeeAsync</c>
/// for the per-profile <c>Visibility</c> decision via the existing
/// <see cref="ProfileToAuditableResource"/>, the host-registered
/// <c>Marten.IDocumentStore</c> for the tag resolve) — <b>zero new
/// authorization surface</b> (C-M23·2, D7). A match is a <b>feed organizer,
/// never a gate</b> (D6): a profile the viewer cannot see never surfaces and
/// its bio/tag never leaks. One aggregate <c>AccessAudit</c> row per non-empty
/// read (C-M23·4 — emitted by the frozen <c>CanSeeAsync</c> itself, the M3
/// <c>ListFeedAsync</c> lane); a blank query / a missing tag returns an empty
/// page with <b>no</b> row (the M3 0-candidate / M8 "no decision, no row"
/// shape).
/// </summary>
public interface IProfileFindService
{
    /// <summary>
    /// The by-profile-tag find: the actor-visible profiles whose
    /// <c>TagIds</c> contain the tag resolved from <paramref name="slug"/>
    /// (the C-TG·4 business-key lookup — the slug lowercased + trimmed), paged
    /// (the ADR 0090 D6 <c>HasMore</c> idiom, <c>PageSize = 30</c>). The gate is
    /// the frozen <c>CanSeeAsync</c> (one aggregate audit row, the M3 lane); a
    /// blank/missing slug returns an empty page with no row (the M3 0-candidate
    /// shape).
    /// </summary>
    Task<ProfileTagPage> FindPeopleByTagAsync(string slug, string actorId, int page);

    /// <summary>
    /// The bio-substring find: the actor-visible profiles whose <c>Bio</c>
    /// contains <paramref name="q"/> (case-insensitive substring, the M8 D4
    /// engine), paged (the ADR 0090 D6 <c>HasMore</c> idiom,
    /// <c>PageSize = 30</c>). The gate is the frozen <c>CanSeeAsync</c>; a
    /// blank query returns an empty page with no row (the M3 0-candidate / M8
    /// "no decision, no row" shape).
    /// </summary>
    Task<ProfileBioPage> FindPeopleByBioAsync(string q, string actorId, int page);
}

/// <summary>
/// M23 (ADR 0123 D4) — a paged by-tag find-people page: the already-
/// <c>Visibility</c>-gated <see cref="Profile"/> survivors, the resolved
/// <see cref="Tag"/> (for the display-name resolution; <c>null</c> on a
/// miss), and the ADR 0090 D6 <c>HasMore</c> flag.
/// </summary>
public sealed record ProfileTagPage(
    IReadOnlyList<Profile> Profiles,
    Tag? Tag,
    bool HasMore);

/// <summary>
/// M23 (ADR 0123 D4) — a paged bio-substring find-people page: the already-
/// <c>Visibility</c>-gated <see cref="Profile"/> survivors and the ADR 0090
/// D6 <c>HasMore</c> flag.
/// </summary>
public sealed record ProfileBioPage(
    IReadOnlyList<Profile> Profiles,
    bool HasMore);
