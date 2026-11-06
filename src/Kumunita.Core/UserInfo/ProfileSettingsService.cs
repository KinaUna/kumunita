using Kumunita.Core.Notifications;
using Kumunita.Core.Translations;
using Marten;
using Marten.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core.UserInfo;

/// <summary>
/// Concrete <see cref="IProfileSettingsService"/> (C07, ADR 0286): the
/// profile-settings cluster split from <see cref="UserInfoService"/>.
/// </summary>

public sealed class ProfileSettingsService(IDocumentStore store, IServiceProvider? services = null) : IProfileSettingsService
{
    /// <inheritdoc />
    public async Task SetProfileAvatarAsync(string subjectId, string? avatarId, string actorBy)
    {
        // C-MED·8 single write lane (design doc §2.2): point Profile.AvatarId at a
        // MediaObject (a content hash) or clear it when avatarId is null. The
        // owner-scope check happens at the Web boundary (the self-only upload
        // lane) — this lane writes Profile.AvatarId only. One session, one
        // SaveChangesAsync (C3); no audit row (a Profile field write — the
        // UpsertProfileAsync shape, "not an access decision"; the serving lane's
        // audit row is owned by the caller's CanAsync gate, C-MED·2). Fail closed
        // on a missing profile (design doc §2.2).
        await using var session = store.OpenSession(new SessionOptions());

        var profile = await session.LoadAsync<Profile>(subjectId).ConfigureAwait(false);
        if (profile is null)
            throw new KeyNotFoundException($"Profile not found: {subjectId}");

        profile.AvatarId = avatarId;
        session.Store(profile);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetProfileTimezoneAsync(string subjectId, string? timezone, string actorBy)
    {
        // ADR 0019 — the user's timezone override write lane. Mirrors
        // SetProfileAvatarAsync exactly (the C-MED·8 single write-lane shape):
        // the self-scope check happens at the Web boundary (the owner is the
        // actor); this lane writes Profile.TimeZone only. One session, one
        // SaveChangesAsync (C3); no audit row (a Profile field write — the
        // UpsertProfileAsync shape, "not an access decision"). Fail closed on a
        // missing profile (never load-or-create, the SetProfileAvatarAsync pin).
        // Strong consistency (C4): the new value is live on the very next
        // GetProfileAsync call.
        await using var session = store.OpenSession(new SessionOptions());

        var profile = await session.LoadAsync<Profile>(subjectId).ConfigureAwait(false);
        if (profile is null)
            throw new KeyNotFoundException($"Profile not found: {subjectId}");

        profile.TimeZone = timezone;
        session.Store(profile);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetProfilePageSizeAsync(string subjectId, int? pageSize, string actorBy)
    {
        // The resident's items-per-page override write lane. Mirrors
        // SetProfileTimezoneAsync exactly (the C-MED·8 single write-lane shape):
        // the self-scope check happens at the Web boundary (the owner is the
        // actor); this lane writes Profile.PageSize only. One session, one
        // SaveChangesAsync (C3); no audit row (a Profile field write — the
        // UpsertProfileAsync shape, "not an access decision"). Fail closed on a
        // missing profile (never load-or-create, the SetProfileTimezoneAsync pin).
        // Strong consistency (C4): the new value is live on the very next
        // GetProfileAsync call. The value is stored as-is (clamped on the read
        // path by PageSizer.Resolve — the ADR 0019 "store, resolve on read"
        // posture; a page size is never a fail-closed gate).
        await using var session = store.OpenSession(new SessionOptions());

        var profile = await session.LoadAsync<Profile>(subjectId).ConfigureAwait(false);
        if (profile is null)
            throw new KeyNotFoundException($"Profile not found: {subjectId}");

        profile.PageSize = pageSize;
        session.Store(profile);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetProfileFirstDayOfWeekAsync(string subjectId, int? firstDayOfWeek, string actorBy)
    {
        // ADR 0169 — the resident's first-day-of-week preference write lane.
        // Mirrors SetProfilePageSizeAsync exactly (the C-MED·8 single
        // write-lane shape): the self-scope check happens at the Web boundary
        // (the owner is the actor); this lane writes Profile.FirstDayOfWeek
        // only. One session, one SaveChangesAsync (C3); no audit row (a
        // Profile field write — the UpsertProfileAsync shape, "not an access
        // decision"). Fail closed on a missing profile (never load-or-create,
        // the SetProfilePageSizeAsync pin). Strong consistency (C4): the new
        // value is live on the very next GetProfileAsync call. Stored as-is
        // (resolved to the Monday default on the read path by
        // WeekAnchor.Resolve — the ADR 0019 "store, resolve on read" posture;
        // a display preference is never a fail-closed gate).
        await using var session = store.OpenSession(new SessionOptions());

        var profile = await session.LoadAsync<Profile>(subjectId).ConfigureAwait(false);
        if (profile is null)
            throw new KeyNotFoundException($"Profile not found: {subjectId}");

        profile.FirstDayOfWeek = firstDayOfWeek;
        session.Store(profile);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetProfileHideHomeIntroAsync(string subjectId, bool hideHomeIntro, string actorBy)
    {
        // ADR 0149 D1 — the resident's hide-home-intro preference write lane.
        // Mirrors SetProfilePageSizeAsync exactly (the C-MED·8 single
        // write-lane shape): the self-scope check happens at the Web boundary
        // (the owner is the actor); this lane writes Profile.HideHomeIntro
        // only. One session, one SaveChangesAsync (C3); no audit row (a
        // Profile field write — the UpsertProfileAsync shape, "not an access
        // decision"). Fail closed on a missing profile (never load-or-create,
        // the SetProfileTimezoneAsync pin). Strong consistency (C4): the new
        // value is live on the very next GetProfileAsync call.
        await using var session = store.OpenSession(new SessionOptions());

        var profile = await session.LoadAsync<Profile>(subjectId).ConfigureAwait(false);
        if (profile is null)
            throw new KeyNotFoundException($"Profile not found: {subjectId}");

        profile.HideHomeIntro = hideHomeIntro;
        session.Store(profile);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetProfileHomeShowUpcomingAsync(string subjectId, bool showUpcoming, string actorBy)
    {
        // The home "upcoming" display preference write lane. Mirrors
        // SetProfileHideHomeIntroAsync exactly (the C-MED·8 single write-lane
        // shape): the self-scope check happens at the Web boundary (the owner
        // is the actor); this lane writes Profile.HomeShowUpcoming only. One
        // session, one SaveChangesAsync (C3); no audit row (a Profile field
        // write — the UpsertProfileAsync shape, "not an access decision").
        // Fail closed on a missing profile (never load-or-create, the
        // SetProfileTimezoneAsync pin). Strong consistency (C4): the new
        // value is live on the very next GetProfileAsync call.
        await using var session = store.OpenSession(new SessionOptions());

        var profile = await session.LoadAsync<Profile>(subjectId).ConfigureAwait(false);
        if (profile is null)
            throw new KeyNotFoundException($"Profile not found: {subjectId}");

        profile.HomeShowUpcoming = showUpcoming;
        session.Store(profile);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetProfileHomeUpcomingMineOnlyAsync(string subjectId, bool mineOnly, string actorBy)
    {
        // The home "upcoming" scope write lane. Mirrors
        // SetProfileHomeShowUpcomingAsync exactly (the C-MED·8 single
        // write-lane shape): the self-scope check happens at the Web
        // boundary (the owner is the actor); this lane writes
        // Profile.HomeUpcomingMineOnly only. One session, one SaveChangesAsync
        // (C3); no audit row (a Profile field write — the UpsertProfileAsync
        // shape, "not an access decision"). Fail closed on a missing profile
        // (never load-or-create, the SetProfileTimezoneAsync pin). Strong
        // consistency (C4): the new value is live on the very next
        // GetProfileAsync call.
        await using var session = store.OpenSession(new SessionOptions());

        var profile = await session.LoadAsync<Profile>(subjectId).ConfigureAwait(false);
        if (profile is null)
            throw new KeyNotFoundException($"Profile not found: {subjectId}");

        profile.HomeUpcomingMineOnly = mineOnly;
        session.Store(profile);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetProfileDateFormatAsync(string subjectId, string? formatString, string actorBy)
    {
        // ADR 0020 — the user's date-format override write lane. Mirrors
        // SetProfileTimezoneAsync exactly (the C-MED·8 single write-lane shape):
        // the self-scope check happens at the Web boundary (the owner is the
        // actor); this lane writes Profile.DateFormat only. One session, one
        // SaveChangesAsync (C3); no audit row (a Profile field write — the
        // UpsertProfileAsync shape, "not an access decision"). Fail closed on a
        // missing profile (never load-or-create, the SetProfileTimezoneAsync
        // pin). Strong consistency (C4): the new value is live on the very next
        // GetProfileAsync call.
        await using var session = store.OpenSession(new SessionOptions());

        var profile = await session.LoadAsync<Profile>(subjectId).ConfigureAwait(false);
        if (profile is null)
            throw new KeyNotFoundException($"Profile not found: {subjectId}");

        profile.DateFormat = formatString;
        session.Store(profile);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetProfileEmailLanguageAsync(string subjectId, string? emailLanguage, string actorBy)
    {
        // ADR 0061 — the user's outbound-channel language write lane. Mirrors
        // SetProfileDateFormatAsync exactly (the C-MED·8 single write-lane
        // shape): the self-scope check happens at the Web boundary (the owner
        // is the actor); this lane writes Profile.EmailLanguage only. One
        // session, one SaveChangesAsync (C3); no audit row (a Profile field
        // write — the UpsertProfileAsync shape, "not an access decision").
        // Fail closed on a missing profile (never load-or-create, the
        // SetProfileDateFormatAsync pin). Strong consistency (C4): the new
        // value is live on the very next GetProfileAsync call.
        await using var session = store.OpenSession(new SessionOptions());

        var profile = await session.LoadAsync<Profile>(subjectId).ConfigureAwait(false);
        if (profile is null)
            throw new KeyNotFoundException($"Profile not found: {subjectId}");

        profile.EmailLanguage = emailLanguage;
        session.Store(profile);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CompleteOnboardingAsync(string subjectId, string actorBy)
    {
        // ADR 0132 (D2) — the onboarding-completion stamp write lane. Mirrors
        // SetProfileTimezoneAsync exactly (the C-MED·8 single write-lane shape):
        // the self-scope check happens at the Web boundary (the owner is the
        // actor); this lane writes Profile.OnboardingCompletedAt only. One
        // session, one SaveChangesAsync (C3); no audit row (a Profile field
        // write — the UpsertProfileAsync shape, "not an access decision").
        // Fail closed on a missing profile (never load-or-create, the
        // SetProfileTimezoneAsync pin). Strong consistency (C4): the stamp is
        // live on the very next GetProfileAsync call.
        await using var session = store.OpenSession(new SessionOptions());

        var profile = await session.LoadAsync<Profile>(subjectId).ConfigureAwait(false);
        if (profile is null)
            throw new KeyNotFoundException($"Profile not found: {subjectId}");

        profile.OnboardingCompletedAt = DateTimeOffset.UtcNow;
        session.Store(profile);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetOnboardingCompletedAsync(string subjectId)
    {
        // ADR 0132 (D2) — the onboarding-completion read seam. The
        // GetProfileAsync read re-projected (owner-scope, no audit row,
        // never load-or-creates — null is the floor, not a write).
        await using var session = store.OpenSession(new SessionOptions());

        var profile = await session.LoadAsync<Profile>(subjectId).ConfigureAwait(false);
        return profile?.OnboardingCompletedAt;
    }

    /// <inheritdoc />
    public async Task SetMessagingOptInAsync(string subjectId, bool optIn, string actorBy)
    {
        // M9 amendment — the resident's own messaging opt-in write lane.
        // Mirrors CompleteOnboardingAsync exactly (the C-MED·8 single
        // write-lane shape): the self-scope check happens at the Web boundary
        // (the owner is the actor); this lane writes
        // Profile.MessagingOptIn only. One session, one SaveChangesAsync
        // (C3); no audit row (a Profile field write — the UpsertProfileAsync
        // shape, "not an access decision"). Fail closed on a missing profile
        // (never load-or-create, the CompleteOnboardingAsync pin). Strong
        // consistency (C4): the value is live on the very next
        // GetProfileAsync call.
        await using var session = store.OpenSession(new SessionOptions());

        var profile = await session.LoadAsync<Profile>(subjectId).ConfigureAwait(false);
        if (profile is null)
            throw new KeyNotFoundException($"Profile not found: {subjectId}");

        profile.MessagingOptIn = optIn;
        session.Store(profile);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Component>> GetComponentsAsync(bool enabledOnly)
    {
        // M3 ADD (ADR 0006-E lane, mirrors M2's GetProfilesAsync U3): the composer's
        // component picker / /community/{id} grouping / the feed's candidate
        // filter (M3 design §2.3). A candidate set, NOT a visible set — no
        // AccessAudit row (C-M3·2); visibility is the caller's job via
        // IAuthorizationService (C3). Live rows, no projection, no cache (C4).
        await using var session = store.QuerySession();
        var components = enabledOnly
            ? await session.Query<Component>().Where(c => c.Enabled).ToListAsync().ConfigureAwait(false)
            : await session.Query<Component>().ToListAsync().ConfigureAwait(false);
        return components;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Profile>> GetProfilesAsync(bool verifiedOnly)
    {
        // Live rows (invariant C4); candidate set, NOT a visible set (§4.3 / C-M2·2)
        // — no audit row here (invariant C3), visibility is decided by the caller
        // via IAuthorizationService (C6 shared matching pass).
        await using var session = store.QuerySession();
        var profiles = verifiedOnly
            ? await session.Query<Profile>().Where(p => p.Verified).ToListAsync().ConfigureAwait(false)
            : await session.Query<Profile>().ToListAsync().ConfigureAwait(false);
        return profiles;
    }

    /// <inheritdoc />
    public async Task UpsertProfileAsync(Profile profile, ProfileUpdate patch, string? actorBy = null)
    {
        // Load-or-create by SubjectId (the pinned document identity). When creating, the
        // `profile` argument is the base record (its fields define the initial row); the
        // `patch`'s non-null fields then take priority over every source (the M1 bootstrap
        // surface). One SaveChangesAsync; no audit row (called by the Identity lifecycle /
        // bootstrap surface — not an access decision, so invariant C3's audit lane does not
        // apply here).
        var subjectId = profile.SubjectId;

        await using var session = store.OpenSession(new SessionOptions());
        var existing = await session.LoadAsync<Profile>(subjectId).ConfigureAwait(false);

        var doc = existing ?? new Profile
        {
            SubjectId = subjectId,
            ExternalId = profile.ExternalId,
            HouseholdId = profile.HouseholdId,
            DisplayName = profile.DisplayName,
            Verified = profile.Verified,
            Visibility = profile.Visibility,
            ContactVisibility = profile.ContactVisibility,
            Email = profile.Email,
            Phone = profile.Phone,
            Address = profile.Address,
            Bio = profile.Bio,
            TagIds = profile.TagIds
        };

        // Patch wins on every non-null field; a null field leaves the current value untouched.
        if (patch.DisplayName is not null) doc.DisplayName = patch.DisplayName;
        if (patch.Email is not null) doc.Email = patch.Email;
        if (patch.Phone is not null) doc.Phone = patch.Phone;
        if (patch.Visibility is not null) doc.Visibility = patch.Visibility;
        if (patch.ContactVisibility is not null) doc.ContactVisibility = patch.ContactVisibility;
        if (patch.Address is not null) doc.Address = patch.Address;
        if (patch.Bio is not null) doc.Bio = patch.Bio;   // M23 (D3): verbatim (ADR 0001-B); null ⇒ don't touch

        // M23 (D3): resolve the profile's author-set tags to Tag.Id values — the inlined
        // ADR 0044 create-or-get (TagService.AttachToPostAsync shape). A missing Slug creates
        // a Tag (+ one tag.create row, C-TG·9); a present Slug is reused (no row, C-TG·4).
        // The profile write itself emits no audit row (a Profile field write, not an access
        // decision — the existing shape above). The actor id for any new Tag.CreatedBy is the
        // acting resident (the self-edit standing, D3) — falls back to the owner.
        if (patch.TagIds is not null)
        {
            var now = DateTimeOffset.UtcNow;
            var creator = actorBy ?? subjectId;
            var resolved = new List<string>(patch.TagIds.Count);
            foreach (var raw in patch.TagIds)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var slug = raw.Trim().ToLowerInvariant();   // DeriveSlug (C-TG·4), the TagService idiom
                if (slug.Length == 0) continue;
                var existingTag = await session.Query<Kumunita.Core.Tags.Tag>()
                    .Where(t => t.Slug == slug)
                    .FirstOrDefaultAsync();
                if (existingTag is not null)
                {
                    resolved.Add(existingTag.Id);           // reuse — no row (C-TG·4)
                }
                else
                {
                    var tag = new Kumunita.Core.Tags.Tag
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Slug = slug,
                        Name = slug,
                        LanguageCode = "en",
                        CreatedBy = creator,
                        Created = now
                    };
                    session.Store(new Authorization.AccessAudit
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        At = now,
                        ActorId = creator,
                        EffectivePrincipalId = creator,
                        Action = "tag.create",
                        TargetKind = "tag",
                        TargetId = tag.Id,
                        Via = Authorization.AccessVia.Owner,
                        Outcome = Authorization.AccessOutcome.Allow
                    });
                    session.Store(tag);
                    resolved.Add(tag.Id);                   // one tag.create row (C-TG·9)
                }
            }
            doc.TagIds = resolved.ToArray();
        }

        session.Store(doc);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Component>> SeedComponentsAsync()
    {
        // Upsert the four defaults by their stable identity — for the known set,
        // Component.Id is the key ("safety"/"maintenance"/"social"/"governance"), so
        // "upsert by key" is a plain identity-keyed read-then-decide. Create absent
        // rows (their `ModeratorAccess` default of `false` pins invariant C5); leave
        // existing rows untouched — only SetComponentModeratorAccessAsync flips that
        // flag — so an idempotent re-run never resets a deliberate ON. One
        // SaveChangesAsync; no audit row (bootstrap, not an access decision).
        var seeds = new[]
        {
            new Component { Id = "safety", Name = "Safety" },
            new Component { Id = "maintenance", Name = "Maintenance" },
            new Component { Id = "social", Name = "Social" },
            new Component { Id = "governance", Name = "Governance" },
        };

        var result = new List<Component>(seeds.Length);

        await using var session = store.OpenSession(new SessionOptions());
        foreach (var seed in seeds)
        {
            var existing = await session.LoadAsync<Component>(seed.Id).ConfigureAwait(false);
            if (existing is not null)
            {
                // Present already: return it unchanged; only the flag path may touch it.
                result.Add(existing);
                continue;
            }
            session.Store(seed);
            result.Add(seed);
        }

        await session.SaveChangesAsync().ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public async Task SetComponentModeratorAccessAsync(string componentId, bool on, string actorId)
    {
        // The standing-moderator-scope path (ADR 0003, invariant C5 — the ONLY writer that
        // may flip Component.ModeratorAccess): load the component, set the flag, append the
        // audit row (Action "moderator-access", TargetKind "component", Via = Admin — the
        // interface doc pins this: only a GlobalAdmin reaches it — ActorId = actorId) in the
        // SAME session, one SaveChangesAsync (invariant C3 — flag flip and audit commit
        // atomically).
        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        var component = await session.LoadAsync<Component>(componentId).ConfigureAwait(false);
        if (component is null)
            throw new InvalidOperationException($"Component not found: {componentId}");

        component.ModeratorAccess = on;
        session.Store(component);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "moderator-access",
            TargetKind = "component",
            TargetId = componentId,
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModeratorAssignment>> GetAssignmentsAsync(string userId)
    {
        // Live-row read (invariant C4): the account's component-scope assignments (ADR 0003's
        // named-scope rows) at this instant — no projection, no cache.
        await using var session = store.QuerySession();
        return await session
            .Query<ModeratorAssignment>()
            .Where(a => a.UserId == userId)
            .ToListAsync()
            .ConfigureAwait(false);
    }

}
