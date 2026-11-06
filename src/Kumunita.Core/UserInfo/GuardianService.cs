using Kumunita.Core.Notifications;
using Kumunita.Core.Translations;
using Marten;
using Marten.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core.UserInfo;

/// <summary>
/// Concrete <see cref="IGuardianService"/> (C07, ADR 0286): the guardian
/// cluster split from <see cref="UserInfoService"/>.
/// </summary>

public partial class GuardianService(IDocumentStore store, IServiceProvider? services = null) : IGuardianService
{
    private readonly UserInfoStore _shared = new(store, services);

    /// <inheritdoc />
    public async Task<GuardianLink> CreateGuardianLinkAsync(string childId, string guardianId)
    {
        if (string.IsNullOrWhiteSpace(childId))
            throw new ArgumentException("Child id is required.", nameof(childId));
        if (string.IsNullOrWhiteSpace(guardianId))
            throw new ArgumentException("Guardian id is required.", nameof(guardianId));

        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        // G·4 idempotent formation: a duplicate (GuardianId, ChildId) Active row
        // is a no-op — the row is left as-is and returned (not a throw).
        var existing = await session.Query<GuardianLink>()
            .Where(l => l.GuardianId == guardianId && l.ChildId == childId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (existing is not null)
        {
            // No mutation, no audit (a no-op is a no-op — the contract, not an
            // error). Return the existing row as-is.
            return existing;
        }

        var link = new GuardianLink
        {
            Id = Guid.NewGuid().ToString("N"),
            GuardianId = guardianId,
            ChildId = childId,
            Status = GuardianLinkStatus.Active,
            CreatedAt = now
        };
        session.Store(link);

        // Formation audit: the guardian's own standing (all three identities the
        // guardian; the target is the guardian-link row itself, keyed to the
        // child). One SaveChangesAsync (invariant C3).
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = guardianId,
            EffectivePrincipalId = guardianId,
            Action = "guardian.create",
            TargetKind = "guardian-link",
            TargetId = link.Id,
            Via = Authorization.AccessVia.Guardian,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
        return link;
    }

    /// <inheritdoc />
    public async Task<GuardianLink> AssignGuardianLinkAsync(
        string childId, string guardianId, string assignedById)
    {
        if (string.IsNullOrWhiteSpace(childId))
            throw new ArgumentException("Child id is required.", nameof(childId));
        if (string.IsNullOrWhiteSpace(guardianId))
            throw new ArgumentException("Guardian id is required.", nameof(guardianId));
        if (string.IsNullOrWhiteSpace(assignedById))
            throw new ArgumentException("Assigning guardian id is required.", nameof(assignedById));

        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        // One row per (GuardianId, ChildId) pair — the business key (the
        // CoreDocTypes unique index). The ADR 0038 §F acceptance lane reworks
        // this seam: the assigned guardian's standing is now CONFIRMED, not
        // conferred — the row lands PENDING and the standing is minted only
        // on Accept (the guardian.create row is deferred to the accept seam).
        //
        // Idempotency (the G-A·4 precedent, re-shaped for the state machine):
        //   · Pending   → no-op (a fresh request is already awaiting this
        //                 guardian; nothing changes, no new audit row).
        //   · Declined  → the conferrer re-acts: the row flips back to
        //                 Pending (a deliberate new act — the row's
        //                 ResolvedAt/ResolvedBy are cleared, a fresh
        //                 guardian.assign audit row + notification are
        //                 emitted).
        //   · Active /  → no-op (the pair already holds, or has already
        //     Dissolved   left, standing; re-assignment is not a lane).
        var existing = await session.Query<GuardianLink>()
            .Where(l => l.GuardianId == guardianId && l.ChildId == childId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (existing is not null &&
            existing.Status is GuardianLinkStatus.Pending or
                           GuardianLinkStatus.Active or
                           GuardianLinkStatus.Dissolved)
        {
            // No mutation, no audit (a no-op is a no-op — the contract, not an
            // error). Return the existing row as-is.
            return existing;
        }

        GuardianLink link;
        if (existing is not null)
        {
            // The Declined case — the conferrer re-acts over the existing row.
            // The unique index keeps this ONE row (not a second one); the
            // state machine re-enters Pending.
            link = existing;
            link.Status = GuardianLinkStatus.Pending;
            link.ResolvedAt = null;
            link.ResolvedBy = null;
            link.AssignedById = assignedById;
        }
        else
        {
            link = new GuardianLink
            {
                Id = Guid.NewGuid().ToString("N"),
                GuardianId = guardianId,
                ChildId = childId,
                Status = GuardianLinkStatus.Pending,
                CreatedAt = now,
                AssignedById = assignedById
            };
        }
        session.Store(link);

        // The conferral audit row (the GA-AR verb): ActorId = the ASSIGNING
        // guardian (the conferrer). The standing-holder's guardian.create row
        // is deliberately deferred to AcceptGuardianLinkAsync (ADR 0038 §F) —
        // the standing is conferred by the assignee's consent, not by the
        // conferrer's act.
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = assignedById,
            EffectivePrincipalId = assignedById,
            Action = "guardian.assign",
            TargetKind = "guardian-link",
            TargetId = link.Id,
            Via = Authorization.AccessVia.Guardian,
            Outcome = Authorization.AccessOutcome.Allow
        });

        // The assignee's notification (the ADR 0038 §F "no email notification"
        // deferral is superseded: the accept/decline lane needs a reachable
        // surface). Silent when services is null (the test-harness precedent).
        await EmitGuardianAssignNotificationAsync(
            session, childId, guardianId, assignedById).ConfigureAwait(false);

        await session.SaveChangesAsync().ConfigureAwait(false);
        return link;
    }

    /// <inheritdoc />
    public async Task<GuardianLink> AcceptGuardianLinkAsync(
        string childId, string guardianId)
    {
        if (string.IsNullOrWhiteSpace(childId))
            throw new ArgumentException("Child id is required.", nameof(childId));
        if (string.IsNullOrWhiteSpace(guardianId))
            throw new ArgumentException("Guardian id is required.", nameof(guardianId));

        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        // The acceptee identifies with the row by the (guardianId, childId)
        // pair — the row's GuardianId field IS the assignee's identity (it was
        // written by the conferrer, not the assignee; the assignee is simply
        // the account the conferrer named). A standing gate is not possible:
        // the assignee holds no standing YET — that is exactly what the accept
        // confers. The precondition is the row's PENDING state.
        var link = await session.Query<GuardianLink>()
            .Where(l => l.GuardianId == guardianId && l.ChildId == childId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (link is null || link.Status != GuardianLinkStatus.Pending)
        {
            // Missing / already-resolved / already-active / dissolved — in
            // every case the accept is refused, not a no-op (the lane's
            // honesty: a second accept over an Active row is a bug the caller
            // must surface, the GroupInvitation.AcceptAsync C-M2b·3 shape).
            throw new InvalidOperationException(
                $"No pending guardian request for ({guardianId}, {childId}).");
        }

        link.Status = GuardianLinkStatus.Active;
        link.ResolvedAt = now;
        link.ResolvedBy = guardianId;
        session.Store(link);

        // The standing is minted NOW — the two complementary audit rows, in
        // the SAME session (C3, no partial write):
        //
        // (1) guardian.create — the GU seam's shape, byte-identical to what
        //     CreateGuardianLinkAsync writes: ActorId = the ASSIGNED guardian
        //     (the standing-holder).
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = guardianId,
            EffectivePrincipalId = guardianId,
            Action = "guardian.create",
            TargetKind = "guardian-link",
            TargetId = link.Id,
            Via = Authorization.AccessVia.Guardian,
            Outcome = Authorization.AccessOutcome.Allow
        });

        // (2) guardian.accept — the ADR 0038 §F consent event: ActorId = the
        //     ASSIGNED guardian (the one who consented).
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = guardianId,
            EffectivePrincipalId = guardianId,
            Action = "guardian.accept",
            TargetKind = "guardian-link",
            TargetId = link.Id,
            Via = Authorization.AccessVia.Guardian,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
        return link;
    }

    /// <inheritdoc />
    public async Task<GuardianLink> DeclineGuardianLinkAsync(
        string childId, string guardianId)
    {
        if (string.IsNullOrWhiteSpace(childId))
            throw new ArgumentException("Child id is required.", nameof(childId));
        if (string.IsNullOrWhiteSpace(guardianId))
            throw new ArgumentException("Guardian id is required.", nameof(guardianId));

        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        // Same identity-with-the-row shape as the accept seam (the assignee
        // names the (guardianId, childId) pair; the precondition is PENDING).
        var link = await session.Query<GuardianLink>()
            .Where(l => l.GuardianId == guardianId && l.ChildId == childId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (link is null || link.Status != GuardianLinkStatus.Pending)
        {
            throw new InvalidOperationException(
                $"No pending guardian request for ({guardianId}, {childId}).");
        }

        link.Status = GuardianLinkStatus.Declined;
        link.ResolvedAt = now;
        link.ResolvedBy = guardianId;
        session.Store(link);

        // The decline audit row: ActorId = the assigned guardian (the one who
        // refused). No guardian.create row — the standing was never minted.
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = guardianId,
            EffectivePrincipalId = guardianId,
            Action = "guardian.decline",
            TargetKind = "guardian-link",
            TargetId = link.Id,
            Via = Authorization.AccessVia.Guardian,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
        return link;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GuardianLink>> GetPendingGuardianRequestsForAssigneeAsync(
        string guardianId)
    {
        if (string.IsNullOrWhiteSpace(guardianId))
            throw new ArgumentException("Guardian id is required.", nameof(guardianId));

        await using var session = store.QuerySession();
        var rows = await session
            .Query<GuardianLink>()
            .Where(l => l.GuardianId == guardianId
                        && l.Status == GuardianLinkStatus.Pending)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync()
            .ConfigureAwait(false);
        return rows;
    }

    /// <inheritdoc />
    public async Task SuspendChildAsync(string childId, string guardianId)
    {
        if (string.IsNullOrWhiteSpace(childId))
            throw new ArgumentException("Child id is required.", nameof(childId));
        if (string.IsNullOrWhiteSpace(guardianId))
            throw new ArgumentException("Guardian id is required.", nameof(guardianId));

        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        // Standing gate first (G·2/G·3): an ACTIVE link for this exact pair.
        await GuardActiveLinkAsync(session, guardianId, childId).ConfigureAwait(false);

        // Load the child's profile (missing → bad state, not a no-op).
        var profile = await session.Query<Profile>()
            .Where(p => p.SubjectId == childId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (profile is null)
            throw new InvalidOperationException($"No profile for child {childId}.");

        // Set the SAME flag BlockedAccountMiddleware + the directory already read
        // (enforcement parity — the U01 pin).
        profile.Blocked = true;
        session.Store(profile);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = guardianId,
            EffectivePrincipalId = guardianId,
            Action = "guardian.suspend",
            TargetKind = "profile",
            TargetId = childId,
            Via = Authorization.AccessVia.Guardian,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UnsuspendChildAsync(string childId, string guardianId)
    {
        if (string.IsNullOrWhiteSpace(childId))
            throw new ArgumentException("Child id is required.", nameof(childId));
        if (string.IsNullOrWhiteSpace(guardianId))
            throw new ArgumentException("Guardian id is required.", nameof(guardianId));

        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        // Standing gate first (G·2/G·3): an ACTIVE link for this exact pair.
        await GuardActiveLinkAsync(session, guardianId, childId).ConfigureAwait(false);

        var profile = await session.Query<Profile>()
            .Where(p => p.SubjectId == childId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (profile is null)
            throw new InvalidOperationException($"No profile for child {childId}.");

        // Restore standing — live on the next read (G·2).
        profile.Blocked = false;
        session.Store(profile);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = guardianId,
            EffectivePrincipalId = guardianId,
            Action = "guardian.unsuspend",
            TargetKind = "profile",
            TargetId = childId,
            Via = Authorization.AccessVia.Guardian,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DissolveGuardianLinkAsync(string linkId, string actorId, bool viaAdmin)
    {
        if (string.IsNullOrWhiteSpace(linkId))
            throw new ArgumentException("Link id is required.", nameof(linkId));
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("Actor id is required.", nameof(actorId));

        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        var link = await session.LoadAsync<GuardianLink>(linkId).ConfigureAwait(false);
        if (link is null)
            throw new InvalidOperationException($"No guardian link: {linkId}");

        // G·4: on the guardian's own lane the actor must BE the GuardianId
        // (deny-by-default). The G·5 safety valve (viaAdmin) skips the check.
        if (!viaAdmin && link.GuardianId != actorId)
            throw new UnauthorizedAccessException(
                $"Account {actorId} is not the guardian on link {linkId}.");

        link.Status = GuardianLinkStatus.Dissolved;
        link.DissolvedAt = now;
        link.DissolvedBy = actorId;
        session.Store(link);

        // A dissolve writes NOTHING to membership and does NOT set
        // Profile.Blocked — the self-lanes restore on the next read (G·2/C4);
        // un-suspend is a separate act (the G·5 valve or UnsuspendChildAsync).
        var via = viaAdmin ? Authorization.AccessVia.Admin : Authorization.AccessVia.Guardian;

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "guardian.dissolve",
            TargetKind = "guardian-link",
            TargetId = linkId,
            Via = via,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetChildMessagingRestrictionAsync(string childId, bool restricted, string guardianId)
    {
        if (string.IsNullOrWhiteSpace(childId))
            throw new ArgumentException("Child id is required.", nameof(childId));
        if (string.IsNullOrWhiteSpace(guardianId))
            throw new ArgumentException("Guardian id is required.", nameof(guardianId));

        // M9 amendment — the guardian's messaging restriction over a
        // supervised child. Mirrors SuspendChildAsync exactly (the guardian
        // write-lane shape): standing gate first (G·2/G·3), then the flag
        // write, then one audit row — all in one session / one
        // SaveChangesAsync (C3).
        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        // Standing gate first (G·2/G·3): an ACTIVE link for this exact pair.
        await GuardActiveLinkAsync(session, guardianId, childId).ConfigureAwait(false);

        // Load the child's profile (missing → bad state, not a no-op — the
        // SuspendChildAsync pin).
        var profile = await session.Query<Profile>()
            .Where(p => p.SubjectId == childId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (profile is null)
            throw new InvalidOperationException($"No profile for child {childId}.");

        // Flip the ceiling flag (enforcement parity — the
        // MessagingService per-actor gate reads this exact field).
        profile.MessagingRestricted = restricted;
        session.Store(profile);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = guardianId,
            EffectivePrincipalId = guardianId,
            Action = "guardian.messaging_restrict",
            TargetKind = "profile",
            TargetId = childId,
            Via = Authorization.AccessVia.Guardian,
            Outcome = Authorization.AccessOutcome.Allow
        });
        await session.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetChildEventRsvpModeAsync(string childId, EventRsvpMode mode, string guardianId)
    {
        if (string.IsNullOrWhiteSpace(childId))
            throw new ArgumentException("Child id is required.", nameof(childId));
        if (string.IsNullOrWhiteSpace(guardianId))
            throw new ArgumentException("Guardian id is required.", nameof(guardianId));

        // The lane — the guardian's event-attendance policy over a supervised
        // child. Mirrors SetChildMessagingRestrictionAsync exactly (the
        // guardian write-lane shape): standing gate first (G·2/G·3), then the
        // mode write, then one audit row — all in one session / one
        // SaveChangesAsync (C3).
        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        // Standing gate first (G·2/G·3): an ACTIVE link for this exact pair.
        await GuardActiveLinkAsync(session, guardianId, childId).ConfigureAwait(false);

        // Load the child's profile (missing → bad state, not a no-op — the
        // SuspendChildAsync pin).
        var profile = await session.Query<Profile>()
            .Where(p => p.SubjectId == childId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (profile is null)
            throw new InvalidOperationException($"No profile for child {childId}.");

        // Set the mode (enforcement parity — the EventService RSVP gate reads
        // this exact field to decide how the child's own RsvpAsync write is
        // treated: refused / allowed-and-notified / allowed outright).
        profile.EventRsvpMode = mode;
        session.Store(profile);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = guardianId,
            EffectivePrincipalId = guardianId,
            Action = "guardian.event_rsvp_mode",
            TargetKind = "profile",
            TargetId = childId,
            Via = Authorization.AccessVia.Guardian,
            Outcome = Authorization.AccessOutcome.Allow
        });
        await session.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetChildCommunityBlockAsync(string childId, string communityId, bool blocked, string guardianId)
    {
        if (string.IsNullOrWhiteSpace(childId))
            throw new ArgumentException("Child id is required.", nameof(childId));
        if (string.IsNullOrWhiteSpace(communityId))
            throw new ArgumentException("Community id is required.", nameof(communityId));
        if (string.IsNullOrWhiteSpace(guardianId))
            throw new ArgumentException("Guardian id is required.", nameof(guardianId));

        // A guardian's per-community block-and-hide over a supervised child —
        // the SetChildMessagingRestrictionAsync guardian-ceiling shape,
        // per-community rather than a single flag: standing gate first
        // (G·2/G·3), then the list write, then one audit row — all in one
        // session / one SaveChangesAsync (C3). The block works even for a
        // MANDATORY community (ADR 0012's "cannot remove" — the membership is
        // implicit and the removal lanes refuse it) because it does not touch
        // the membership at all: it writes Profile.BlockedCommunityIds, which
        // the child's access surfaces (GetEffectiveCommunityIdsAsync) exclude.
        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        // Standing gate first (G·2/G·3): an ACTIVE link for this exact pair.
        await GuardActiveLinkAsync(session, guardianId, childId).ConfigureAwait(false);

        // The component must exist (a block on a missing community is a data
        // bug, not a no-op — the ClearCommunityMembershipAsync pin).
        var component = await session.LoadAsync<Component>(communityId).ConfigureAwait(false);
        if (component is null)
            throw new InvalidOperationException($"Community not found: {communityId}");

        // Load the child's profile (missing → bad state, not a no-op — the
        // SetChildMessagingRestrictionAsync pin).
        var profile = await session.Query<Profile>()
            .Where(p => p.SubjectId == childId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (profile is null)
            throw new InvalidOperationException($"No profile for child {childId}.");

        // Toggle the id in the block list (idempotent — a re-block / re-unblock
        // never duplicates or mis-removes).
        var current = profile.BlockedCommunityIds is null
            ? new List<string>()
            : profile.BlockedCommunityIds.ToList();
        if (blocked)
        {
            if (!current.Contains(communityId))
                current.Add(communityId);
        }
        else
        {
            current.RemoveAll(c => c == communityId);
        }
        profile.BlockedCommunityIds = current;
        session.Store(profile);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = guardianId,
            EffectivePrincipalId = guardianId,
            Action = blocked ? "guardian.community_block" : "guardian.community_unblock",
            TargetKind = "community",
            TargetId = communityId,
            Via = Authorization.AccessVia.Guardian,
            Outcome = Authorization.AccessOutcome.Allow
        });
        await session.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
    }


    /// <inheritdoc />
    public async Task<IReadOnlyList<CommunityMembershipRequest>>
        GetPendingCommunityMembershipRequestsForChildAsync(string childId)
    {
        if (string.IsNullOrEmpty(childId))
            return Array.Empty<CommunityMembershipRequest>();

        await using var session = store.QuerySession();
        return await session
            .Query<CommunityMembershipRequest>()
            .Where(r => r.UserId == childId && r.Status == CommunityMembershipRequestStatus.Pending)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<GuardianTimeLimitSchedule?> GetChildTimeLimitAsync(
        string guardianId, string childId)
    {
        if (string.IsNullOrWhiteSpace(guardianId))
            throw new ArgumentException("Guardian id is required.", nameof(guardianId));
        if (string.IsNullOrWhiteSpace(childId))
            throw new ArgumentException("Child id is required.", nameof(childId));

        // Standing gate first (C-M28·4, deny-by-default) — no audit row (a read).
        await GuardTimeLimitStandingAsync(guardianId, childId).ConfigureAwait(false);

        // Single-row read by the child's identity (the M20 GetQuietScheduleAsync
        // shape); null = "never restricted" (the floor, C-M28·3).
        await using var session = store.QuerySession();
        return await session
            .LoadAsync<GuardianTimeLimitSchedule>(childId)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetChildTimeLimitAsync(
        string guardianId, string childId, GuardianTimeLimitSchedule? schedule)
    {
        if (string.IsNullOrWhiteSpace(guardianId))
            throw new ArgumentException("Guardian id is required.", nameof(guardianId));
        if (string.IsNullOrWhiteSpace(childId))
            throw new ArgumentException("Child id is required.", nameof(childId));

        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        // Standing gate (C-M28·4, deny-by-default) — returns the audit Via
        // (Guardian on the link lane, Admin on the G·5 valve).
        var via = await GuardTimeLimitStandingAsync(guardianId, childId).ConfigureAwait(false);

        if (schedule is null)
        {
            // Clear = never restricted (the floor, C-M28·3): delete the child's
            // row, if any (the M20 SetQuietScheduleAsync "clear" idiom — the
            // session.Delete shape; the read seam then returns null).
            session.Delete<GuardianTimeLimitSchedule>(childId);
        }
        else
        {
            schedule.ChildId = childId;    // pin the identity (singleton-per-child row)
            schedule.Updated = now;
            session.Store(schedule);
        }

        // The GU write appends exactly ONE audit row (verb guardian.time-limit.set,
        // TargetKind the child account, the guardian.suspend / guardian.unsuspend
        // shape), in the same transaction (invariant C3). Strong consistency
        // (C-M28·4): live on the very next read (no projection, no cache).
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = guardianId,
            EffectivePrincipalId = guardianId,
            Action = "guardian.time-limit.set",
            TargetKind = "profile",
            TargetId = childId,
            Via = via,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<GuardianTimeLimitSchedule?> GetActiveTimeLimitAsync(string childId)
    {
        if (string.IsNullOrWhiteSpace(childId))
            throw new ArgumentException("Child id is required.", nameof(childId));

        // The ENFORCEMENT read (C-M28·2): child-keyed, NO guardian gate, NO audit
        // row — the TimeLimitMiddleware's single-row load (the
        // BlockedAccountMiddleware GetProfileAsync shape). null = "never
        // restricted" (the floor, C-M28·3).
        await using var session = store.QuerySession();
        return await session
            .LoadAsync<GuardianTimeLimitSchedule>(childId)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// M28 (ADR 0151, D4) — the M28 standing gate (G·2 live / G·5 valve,
    /// C-M28·4/C-M28·7): an <b>active</b> <see cref="GuardianLink"/> for the
    /// exact (guardian, child) pair confers <see cref="Authorization
    /// .AccessVia.Guardian"/>; else, if <paramref name="guardianId"/> is a
    /// GlobalAdmin, the G·5 safety valve confers <see cref="Authorization
    /// .AccessVia.Admin"/>; else <see cref="UnauthorizedAccessException"/>
    /// (the Web's 404 — the GU deny-by-default pin). A child subject used as
    /// <paramref name="guardianId"/> on their own <paramref name="childId"/>
    /// holds neither an active link (a child is never a GuardianId) nor the
    /// GlobalAdmin role, so they are denied (C-M28·7).
    /// </summary>
    private async Task<Authorization.AccessVia> GuardTimeLimitStandingAsync(
        string guardianId, string childId)
    {
        await using var probe = store.QuerySession();
        var link = await probe.Query<GuardianLink>()
            .Where(l => l.GuardianId == guardianId && l.ChildId == childId
                        && l.Status == GuardianLinkStatus.Active)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (link is not null)
            return Authorization.AccessVia.Guardian;

        if (await IsGlobalAdminAsync(guardianId).ConfigureAwait(false))
            return Authorization.AccessVia.Admin;

        throw new UnauthorizedAccessException(
            $"No active guardian link for ({guardianId}, {childId}) and the " +
            $"guardian is not a GlobalAdmin — no standing to manage the child's " +
            $"time limits (M28, C-M28·4/C-M28·7).");
    }

    /// <summary>
    /// M28 (ADR 0151, D4) — resolves whether <paramref name="subjectId"/> is a
    /// GlobalAdmin (the G·5 safety valve), via the <see cref="Kumunita.Core
    /// .Identity.IIdentityService"/> resolved lazily off the
    /// <c>IServiceProvider</c> (the <see cref="Notifications
    /// .NotificationService"/> emission seam shape — always resolvable, no
    /// construction cycle, the 181 direct-construction test harnesses that
    /// build <c>UserInfoService(store)</c> positionally get <c>services ==
    /// null</c> and therefore no admin standing, exactly as they get no
    /// emission). Fails closed: no provider / no principal / no role ⇒ not a
    /// GlobalAdmin.
    /// </summary>
    private async Task<bool> IsGlobalAdminAsync(string subjectId)
    {
        if (services is null)
            return false;
        var identity = services
            .GetService<Kumunita.Core.Identity.IIdentityService>();
        if (identity is null)
            return false;
        var principal = await identity.GetBySubjectAsync(subjectId).ConfigureAwait(false);
        return principal is not null
            && principal.Roles.Contains(Kumunita.Core.Identity.Roles.GlobalAdmin);
    }

    /// <summary>
    /// The ADR 0038 §F acceptance-lane emission: the assigned guardian (the
    /// recipient) gets an inbox row + (best-effort) staged email with
    /// <see cref="Notifications.Notification.LinkPath"/> =
    /// <c>/me/children</c> (their Index page, where the pending-requests
    /// card shows the Accept / Decline buttons). The <paramref name
    /// "conferrerId"/> is the UGC snippet (the conferring guardian's display
    /// name — a curation fact, not content the G·1 rule hides from the
    /// assignee) appended to the localized template. Silent when
    /// <paramref name="services"/> is null (the 181 pre-ADR 0083
    /// direct-construction test harnesses).
    /// </summary>
    private async Task EmitGuardianAssignNotificationAsync(
        IDocumentSession session,
        string childId,
        string assigneeId,
        string conferrerId)
    {
        var ns = services?.GetService<INotificationEmitter>();
        if (ns is null) return;

        var conferrerProfile = await session.Query<Profile>()
            .Where(p => p.SubjectId == conferrerId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        var body = string.IsNullOrWhiteSpace(conferrerProfile?.DisplayName)
            ? conferrerId
            : conferrerProfile!.DisplayName;

        var linkPath = "/me/children";
        await ns.EmitAsync(
            session,
            assigneeId,
            Notifications.NotificationKinds.GuardianAssign,
            $"notification:guardian.assign:{childId}:{assigneeId}",
            body,
            linkPath: linkPath,
            ct: System.Threading.CancellationToken.None)
            .ConfigureAwait(false);
    }

    private static async Task GuardActiveLinkAsync(
        IDocumentSession session, string guardianId, string childId)
    {
        var link = await session.Query<GuardianLink>()
            .Where(l => l.GuardianId == guardianId && l.ChildId == childId
                        && l.Status == GuardianLinkStatus.Active)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (link is null)
            throw new UnauthorizedAccessException(
                $"No active guardian link for ({guardianId}, {childId}).");
    }

}
