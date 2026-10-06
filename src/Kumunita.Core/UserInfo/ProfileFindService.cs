using Kumunita.Core.Authorization;
using Kumunita.Core.Query;
using Kumunita.Core.Tags;
using Marten;

namespace Kumunita.Core.UserInfo;

/// <summary>
/// M23 (ADR 0123 D4/D6) — the by-tag + bio-substring find-people read. A
/// <see cref="DirectoryService"/>-shaped composition service that mirrors the
/// M3 <c>PostService.ListFeedAsync</c> lane: it composes only the frozen seams
/// (<see cref="IUserInfoService"/> for the candidate set,
/// <see cref="IAuthorizationService"/>.CanSeeAsync for the per-profile
/// <c>Visibility</c> decision via the existing
/// <see cref="ProfileToAuditableResource"/>, the host-registered
/// <see cref="IDocumentStore"/> for the tag resolve) — <b>zero new
/// authorization surface</b> (C-M23·2, D7). A match is a feed organizer, never
/// a gate (D6): a profile the viewer cannot see never surfaces and its bio /
/// tag never leaks.
/// <para>
/// The gate + its one aggregate audit row are the frozen <c>CanSeeAsync</c>
/// (the M3 C-M3·3 lane — one call, one aggregate row, the owner branch resolved
/// internally): it is <b>not</b> a per-profile <c>CanAsync</c> loop and it is
/// <b>not</b> a hand-written aggregate row (a second row would be a §drift-guard
/// violation). A blank query / a missing tag early-returns before any decision,
/// so no row is written (the M3 0-candidate / M8 "no decision, no row" shape).
/// </para>
/// </summary>
public sealed class ProfileFindService : IProfileFindService
{
    // ADR 0090 D6 — the paged-feed page size (the PostService / TagService
    // `PageSize = 30` precedent).
    private const int PageSize = 30;

    private readonly IUserInfoService _userInfo;
    private readonly IAuthorizationService _authz;
    private readonly IDocumentStore _store;

    public ProfileFindService(
        IUserInfoService userInfo,
        IAuthorizationService authz,
        IDocumentStore store)
    {
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
        _authz = authz ?? throw new ArgumentNullException(nameof(authz));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<ProfileTagPage> FindPeopleByTagAsync(
        string slug, string actorId, int page, SortSpec? sort = null, int? pageSize = null)
    {
        // Blank slug ⇒ empty page, no decision, no row (the M3 0-candidate shape).
        if (string.IsNullOrWhiteSpace(slug))
            return new ProfileTagPage(Array.Empty<Profile>(), null, false);
        if (page < 1) page = 1;
        int ps = PageSizer.ResolveOverride(pageSize, PageSize);

        // Resolve the tag by its C-TG·4 business key (lowercase + trimmed — the
        // TagService.DeriveSlug idiom). A missing tag ⇒ empty page, no row
        // (the not-found shape, the M3 0-candidate early return).
        var derivedSlug = slug.Trim().ToLowerInvariant();
        await using var session = _store.QuerySession();
        var tag = await session.Query<Tag>()
            .Where(t => t.Slug == derivedSlug)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (tag is null)
            return new ProfileTagPage(Array.Empty<Profile>(), null, false);

        // Candidate set (the GetProfilesAsync(false) shape) filtered by the tag
        // (C-M23·6) — the match is a feed organizer (D6), never a gate.
        var candidates = (await _userInfo.GetProfilesAsync(false).ConfigureAwait(false))
            .Where(p => !p.Blocked && p.TagIds.Contains(tag.Id))
            .ToList();
        // C-M3·2 / M3 0-candidate shape: the 0-candidate early return runs
        // BEFORE any decision and BEFORE the gate — no CanSeeAsync call, no row.
        if (candidates.Count == 0)
            return new ProfileTagPage(Array.Empty<Profile>(), tag, false);

        // The gate (D6 / C-M23·4): one frozen CanSeeAsync pass — it writes the
        // one aggregate audit row itself (the M3 ListFeedAsync shape) and
        // resolves the owner branch internally (the author always sees their
        // own). Returns only the survivors whose id the VisibleSet surfaced.
        var visible = await GateAsync(candidates, actorId).ConfigureAwait(false);
        if (visible.Count == 0)
            return new ProfileTagPage(Array.Empty<Profile>(), tag, false);

        // M26 U8 (design doc §2.2 row 16, correction C-2 — the people
        // allowlist is <b>name</b> only, <c>Profile</c> has no
        // <c>Created</c>): null keeps the current order exactly (the
        // unsorted storage order — C-SORT·2); non-null applies the
        // allowlist over <see cref="Profile.DisplayName"/>
        // (OrdinalIgnoreCase) + the ThenBy(SubjectId) tie-breaker
        // (C-SORT·5) via the shared TagPeopleSortSupport helper.
        // In-memory LINQ-to-objects (not a Marten query). The gate above
        // is frozen (C-SORT·4).
        var pageSlice = Paged(
            TagPeopleSortSupport.OrderByPeopleSort(visible, sort,
                new HashSet<string> { "name" },
                defaultDir: false, // the pinned default (correction C-2): name, asc
                p => p.DisplayName, p => p.SubjectId).ToList(), page, ps);
        // ADR 0090 D6 / the M3 ListFeedAsync idiom: HasMore is "the page is
        // full" (the paged slice filled the ps window) — the same
        // `pagedSlice.Count == ps` shape as PostService.ListFeedAsync
        // (`candidates.Count == ps`, where `candidates` is its paged
        // slice) and TagService.ListPostsByTagPagedAsync (`items.Count ==
        // ps`, where `items` is the page). Not the total candidate count.
        return new ProfileTagPage(pageSlice, tag, pageSlice.Count == ps);
    }

    public async Task<ProfileBioPage> FindPeopleByBioAsync(
        string q, string actorId, int page, SortSpec? sort = null, int? pageSize = null)
    {
        // Blank query ⇒ empty page, no decision, no row (the M3 0-candidate shape).
        if (string.IsNullOrWhiteSpace(q))
            return new ProfileBioPage(Array.Empty<Profile>(), false);
        if (page < 1) page = 1;
        int ps = PageSizer.ResolveOverride(pageSize, PageSize);

        // The bio substring (M8 D4 floor): a case-insensitive substring over a
        // non-null Bio (the M8 D4 engine; D8·2 defers full-text/semantic).
        var needle = q.Trim();
        var candidates = (await _userInfo.GetProfilesAsync(false).ConfigureAwait(false))
            .Where(p => !p.Blocked
                && p.Bio is not null
                && p.Bio.Contains(needle, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // C-M3·2 / M3 0-candidate shape: 0 candidates ⇒ no CanSeeAsync, no row.
        if (candidates.Count == 0)
            return new ProfileBioPage(Array.Empty<Profile>(), false);

        // The gate (D6 / C-M23·4): one frozen CanSeeAsync pass (the
        // ListFeedAsync shape) — one aggregate audit row, owner branch
        // resolved internally. Returns only the survivors whose id the
        // VisibleSet surfaced.
        var visible = await GateAsync(candidates, actorId).ConfigureAwait(false);
        if (visible.Count == 0)
            return new ProfileBioPage(Array.Empty<Profile>(), false);

        // M26 U8 (design doc §2.2 row 17, correction C-2 — the people
        // allowlist is <b>name</b> only): null keeps the current order
        // exactly (the unsorted storage order — C-SORT·2); non-null applies
        // the allowlist over <see cref="Profile.DisplayName"/>
        // (OrdinalIgnoreCase) + the ThenBy(SubjectId) tie-breaker
        // (C-SORT·5) via the shared TagPeopleSortSupport helper
        // (in-memory LINQ-to-objects). The gate above is frozen
        // (C-SORT·4).
        var pageSlice = Paged(
            TagPeopleSortSupport.OrderByPeopleSort(visible, sort,
                new HashSet<string> { "name" },
                defaultDir: false, // the pinned default (correction C-2): name, asc
                p => p.DisplayName, p => p.SubjectId).ToList(), page, ps);
        // ADR 0090 D6 / the M3 ListFeedAsync idiom: HasMore is "the page is
        // full" (the paged slice filled the ps window) — not the total
        // candidate count (see the by-tag read's comment).
        return new ProfileBioPage(pageSlice, pageSlice.Count == ps);
    }

    // ── The gate (D6 / C-M23·4): one frozen CanSeeAsync pass over the whole
    //    candidate set (the M3 ListFeedAsync shape, C1) — it emits the one
    //    aggregate AccessAudit row itself (the M3 C-M3·3 lane) and resolves
    //    the owner branch internally (the author always sees their own, F3).
    //    Returns the source docs whose id the VisibleSet surfaced (D6 — never
    //    a hidden profile's fields). Profile's identity is its SubjectId (not
    //    an `Id` member), so the VisibleSet projection filters on SubjectId.
    private async Task<List<Profile>> GateAsync(List<Profile> candidates, string actorId)
    {
        var visibleSet = await _authz.CanSeeAsync(
                actorId,
                AccessAction.Read,
                candidates.Select(p => new ProfileToAuditableResource(p)))
            .ConfigureAwait(false);

        var visibleIds = new HashSet<string>(visibleSet.Visible.Select(v => v.Id));
        return candidates.Where(p => visibleIds.Contains(p.SubjectId)).ToList();
    }

    private static List<Profile> Paged(List<Profile> ordered, int page, int size)
    {
        var zeroBased = Math.Max(0, page - 1);
        return ordered.Skip(zeroBased * size).Take(size).ToList();
    }
}
