using Kumunita.Core.Notifications;
using Kumunita.Core.UserInfo;
using Marten;
using Marten.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kumunita.Core.Identity;

/// <summary>
/// The <c>/identity</c> account-ops lane — the block/
/// unblock + role-set + password-change + session-kill
/// + break-glass actions (ADR 0001 core-stack-and-identity,
/// ADR 0003 roles-and-moderator). A partial class of
/// <see cref="IdentityService"/> extracted so the main file
/// stays under the 600 <c>cs_file</c> ceiling (C3-04).
/// </summary>
public partial class IdentityService
{
    // ── Block / Unblock (the admin suspension lane — mirrors the verify/role lanes) ────

    /// <inheritdoc />
    public async Task BlockAsync(string targetSubjectId, string adminSubjectId)
    {
        var target = await SetBlockedAsync(targetSubjectId, adminSubjectId, blocked: true);
        logger.LogInformation("Admin {Admin} blocked {Target}.", adminSubjectId, target.Id);
    }

    /// <inheritdoc />
    public async Task UnblockAsync(string targetSubjectId, string adminSubjectId)
    {
        var target = await SetBlockedAsync(targetSubjectId, adminSubjectId, blocked: false);
        logger.LogInformation("Admin {Admin} unblocked {Target}.", adminSubjectId, target.Id);
    }

    private async Task<User> SetBlockedAsync(string targetSubjectId, string adminSubjectId, bool blocked)
    {
        var admin = await RequireGlobalAdminAsync(adminSubjectId);
        _ = admin;
        var target = await userManager.FindByIdAsync(targetSubjectId)
            ?? throw new InvalidOperationException($"No account '{targetSubjectId}'.");

        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        var profile = (await session.LoadAsync<Profile>(targetSubjectId)) ?? new Profile
        {
            SubjectId = targetSubjectId,
            Email = target.Email,
            DisplayName = target.UserName ?? target.Email ?? string.Empty
        };
        profile.Blocked = blocked;
        session.Store(profile);

        var now = DateTimeOffset.UtcNow;
        session.Store(AuditRow(now, adminSubjectId, adminSubjectId,
            blocked ? "block" : "unblock", AccountKind, targetSubjectId,
            Authorization.AccessVia.Admin, Authorization.AccessOutcome.Allow));
        await session.SaveChangesAsync();

        // The block takes effect at the Identity↔cookie seam: a blocked account mints no
        // roles, so it has no standing (no Member/Moderator/GlobalAdmin). Rotate the
        // security stamp — the same invalidation the demotion/password lanes rely on (OPS §10)
        // — so existing sessions must re-mint to reflect the change.
        await userManager.UpdateSecurityStampAsync(target);

        return target;
    }

    public async Task<bool> IsSignupOpenAsync()
        => await _gates.IsSignupOpenAsync();

    public async Task SetSignupOpenAsync(bool open, string adminSubjectId)
        => await _gates.SetSignupOpenAsync(open, adminSubjectId);

    public async Task<bool> IsSampleDataEnabledAsync()
        => await _gates.IsSampleDataEnabledAsync();

    public async Task<bool> IsSamplePasswordChangeLockedAsync()
        => await _gates.IsSamplePasswordChangeLockedAsync();

    public async Task SetSamplePasswordChangeLockedAsync(bool locked, string adminSubjectId)
        => await _gates.SetSamplePasswordChangeLockedAsync(locked, adminSubjectId);

    public async Task<bool> IsChangePasswordLockedForAsync(string subjectId)
        => await _gates.IsChangePasswordLockedForAsync(subjectId);



    public async Task<bool> IsSampleMfaLockedAsync()
        => await _gates.IsSampleMfaLockedAsync();

    public async Task SetSampleMfaLockedAsync(bool locked, string adminSubjectId)
        => await _gates.SetSampleMfaLockedAsync(locked, adminSubjectId);

    public async Task<bool> IsMfaLockedForAsync(string subjectId)
        => await _gates.IsMfaLockedForAsync(subjectId);

    public async Task<bool> IsSampleDataAccountAsync(string subjectId)
        => await _gates.IsSampleDataAccountAsync(subjectId);

    public async Task<bool> IsNotifyAdminsOnSignupAsync()
        => await _gates.IsNotifyAdminsOnSignupAsync();

    public async Task SetNotifyAdminsOnSignupAsync(bool notify, string adminSubjectId)
        => await _gates.SetNotifyAdminsOnSignupAsync(notify, adminSubjectId);

    // ── Resident-invite mode (ADR 0304, amended 2026-11-11 for IMPROVE-UX A11) ──
    // The 3-state resident-invite posture (the EventRsvpMode 3-state shape,
    // the NotifyAdminsOnSignup / IsSignupOpen singleton-toggle lane). A11
    // settles the setting + the rule; A12's resident "Invite a neighbour"
    // surface will consume the posture via IInvitationService
    // .GetResidentInvitePostureAsync (the A12 seam reads MayInvite /
    // RequiresApproval before calling CreateAsync / InviteAsync). The
    // C08 thin-forwarder shape — IdentityService delegates to the
    // IdentityPlatformGateService (the C3-03 / ADR 0293 part-vs-whole
    // surface).

    public async Task<ResidentInviteSetting> GetResidentInviteSettingAsync()
        => await _gates.GetResidentInviteSettingAsync();

    public async Task SetResidentInviteSettingAsync(ResidentInviteSetting mode, string adminSubjectId)
        => await _gates.SetResidentInviteSettingAsync(mode, adminSubjectId);

    // ── M19 guest standing (ADR 0120, D2 — the single audited write lane) ──

    /// <inheritdoc />
    public async Task<GuestAccess?> GetGuestAccessAsync(string subjectId)
    {
        using var session = documentStore.QuerySession();
        // The C-M19·4 empty floor: null is a valid state (a guest with no
        // settled standing), not an error.
        return await session.LoadAsync<GuestAccess>(subjectId, CancellationToken.None);
    }

    /// <inheritdoc />
    public async Task SetGuestAccessAsync(GuestAccess access, string adminSubjectId)
    {
        // ADR 0120 write seam: the admin-settled allowance (the GuestAccess
        // document, id = the guest's SubjectId) + exactly one audit row (via:
        // Admin, action "guest.set-standing", target "guest:{subjectId}") in
        // the same session (C-M19·5 — no silent, unaudited access). The
        // SetSignupOpenAsync body pattern: one write session, store the
        // document + set Profile.IsGuest (the D1 flag) + one audit row, commit.
        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        var ct = System.Threading.CancellationToken.None;

        // Settle the settling admin (the authoritative ActorId) and store the
        // allowance. access.SubjectId is the guest's subject id (the document
        // identity) — the caller owns it; we do not re-derive it.
        access.SetByAdmin = adminSubjectId;
        session.Store(access);

        // Mark the account a guest (the D1 flag, C-M19·1) in the same session.
        // A guest is still a real account — the Profile row exists for a real
        // Identity account; load-or-create so a guest standing can be settled
        // before the profile row (defensively) rather than failing.
        var profile = await session.LoadAsync<Kumunita.Core.UserInfo.Profile>(
            access.SubjectId, ct).ConfigureAwait(false);
        if (profile is not null)
        {
            profile.IsGuest = true;
            session.Store(profile);
        }

        // Exactly one audit row (C-M19·5, D2) — the singleton-toggle shape
        // (the signup.set-open precedent).
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = adminSubjectId,
            EffectivePrincipalId = adminSubjectId,
            Action = "guest.set-standing",
            TargetKind = "guest",
            TargetId = $"guest:{access.SubjectId}",
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // ── ADR 0142 — account deletion (the resident-leave / admin-removal lane) ──

    /// <inheritdoc />
    public async Task DeleteAccountAsync(string targetSubjectId, string adminSubjectId)
    {
        // ADR 0142 — the self-serve vs. admin distinction.
        //   * Self-deletion (adminSubjectId == targetSubjectId): the resident
        //     is deleting *their own* account. No GlobalAdmin gate — any
        //     resident may leave the platform. The audit row is Via: Owner.
        //   * Admin-initiated (adminSubjectId != targetSubjectId): a
        //     GlobalAdmin is removing *another* resident. The GlobalAdmin
        //     gate applies (the RequireGlobalAdminAsync shape — the
        //     BlockAsync / UnblockAsync precedent; the fail-closed pin). The
        //     audit row is Via: Admin.
        var isSelfDeletion = string.Equals(
            targetSubjectId, adminSubjectId, StringComparison.Ordinal);

        if (isSelfDeletion)
        {
            // Self-deletion — the resident acts on their own account.
            // ADR 0301 D1 (amends 0142 D5): the self-serve lane is open to
            // EVERY resident — the 0142 D5 GlobalAdmin gate is removed, so a
            // non-GlobalAdmin resident may self-delete (the IMPROVE-UX E7
            // dead-end is closed; the R12 due-job runs exactly this branch
            // for a due DeletionRequest). The only standing refusal that
            // survives is the last-GlobalAdmin lockout pin (0142 D3), which
            // DeleteCoreAsync re-checks. The resident's current password was
            // already verified by the Web controller (R13) before reaching
            // here, so the lane itself performs no standing check.
            var self = await userManager.FindByIdAsync(targetSubjectId).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"No account '{targetSubjectId}'.");
            await DeleteCoreAsync(
                targetSubjectId, self,
                actorId: targetSubjectId,
                via: Authorization.AccessVia.Owner);
            return;
        }

        // Admin-initiated deletion of a *different* account.
        // 1. Admin gate (the RequireGlobalAdminAsync shape).
        var admin = await RequireGlobalAdminAsync(adminSubjectId).ConfigureAwait(false);
        _ = admin;

        // 2. The target must exist (the BlockAsync / SetBlockedAsync shape).
        var target = await userManager.FindByIdAsync(targetSubjectId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No account '{targetSubjectId}'.");

        await DeleteCoreAsync(
            targetSubjectId, target,
            actorId: adminSubjectId,
            via: Authorization.AccessVia.Admin);
    }

    // ── ADR 0143 — guardian delete-child (the GU standing over the ADR 0142 core) ──

    /// <inheritdoc />
    public async Task DeleteChildAccountAsync(string childId, string guardianId)
    {
        if (string.IsNullOrWhiteSpace(childId))
            throw new ArgumentException("Child id is required.", nameof(childId));
        if (string.IsNullOrWhiteSpace(guardianId))
            throw new ArgumentException("Guardian id is required.", nameof(guardianId));

        // Standing gate FIRST (G·2/G·3, live — the GuardActiveLinkAsync
        // shape every other GU seam runs, and the house convention: gate
        // before existence). An ACTIVE GuardianLink for this exact
        // (guardian, child) pair must exist, else refuse before any write.
        // A child is never a GlobalAdmin, so there is no GlobalAdmin gate
        // here (C·2 deny-by-default: the lane is reachable only through an
        // active link); a non-guardian's crafted call is refused with
        // UnauthorizedAccessException (the Web's 404). Checking the standing
        // first also keeps the response uniform for an existing vs. a
        // non-existing target (both a 404 — no account-existence oracle),
        // the ADR 0012/0013 "a non-guardian learns nothing" shape.
        await using (var gateSession = documentStore.OpenSession(new Marten.Services.SessionOptions()))
        {
            var link = await gateSession.Query<Kumunita.Core.UserInfo.GuardianLink>()
                .Where(l => l.GuardianId == guardianId && l.ChildId == childId
                            && l.Status == GuardianLinkStatus.Active)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
            if (link is null)
                throw new UnauthorizedAccessException(
                    $"No active guardian link for ({guardianId}, {childId}).");
        }

        // The target must exist (a defensive check AFTER the standing gate —
        // the SuspendChildAsync "load the profile, missing → InvalidOperationException"
        // shape). A second call for an already-deleted child reaches the
        // standing gate first (the successful delete dissolved the link, C·5)
        // and is refused with the same 404 — not a silent no-op.
        var target = await userManager.FindByIdAsync(childId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No account '{childId}'.");

        await DeleteCoreAsync(
            childId, target,
            actorId: guardianId,
            via: Authorization.AccessVia.Guardian,
            // ADR 0143 C·5 — no dangling standing: dissolve every
            // GuardianLink row for the deleted child (any status) in the
            // same session as the core's writes, so a co-guardian's row
            // does not dangle pointing at a deleted account.
            dissolveGuardianLinksForChildAsync: async (session, now) =>
            {
                var rows = await session.Query<Kumunita.Core.UserInfo.GuardianLink>()
                    .Where(l => l.ChildId == childId)
                    .ToListAsync()
                    .ConfigureAwait(false);
                foreach (var row in rows)
                {
                    if (row.Status != GuardianLinkStatus.Dissolved)
                    {
                        row.Status = GuardianLinkStatus.Dissolved;
                        row.DissolvedAt = now;
                        row.DissolvedBy = guardianId;
                    }
                    session.Store(row);
                }
            });
    }

    /// <summary>
    /// ADR 0142 — the shared body of <see cref="DeleteAccountAsync"/> for both
    /// the self-deletion and admin-initiated branches: the last-GlobalAdmin
    /// guard, the pseudonymization, the membership/profile removal, the audit
    /// row, and the Identity account deletion. The branch-specific actor +
    /// <c>Via</c> are passed in (the self-serve row is <c>Via: Owner</c>; the
    /// admin row is <c>Via: Admin</c>).
    /// </summary>
    private async Task DeleteCoreAsync(
        string targetSubjectId, User target, string actorId, Authorization.AccessVia via,
        System.Func<IDocumentSession, DateTimeOffset, Task>? dissolveGuardianLinksForChildAsync = null)
    {
        // Last-GlobalAdmin guard (the lockout pin, the OPS.md §9 "Hand over
        // admin" precedent). If the target is a GlobalAdmin and they are the
        // only one on the instance, refuse before any write — this applies to
        // *both* branches (a lone GlobalAdmin cannot self-delete, and no
        // admin may delete the last GlobalAdmin). The recovery path is to
        // promote a second GlobalAdmin first (the "Hand over admin" procedure).
        var targetRoles = (await userManager.GetRolesAsync(target).ConfigureAwait(false)).ToList();
        if (targetRoles.Contains(Roles.GlobalAdmin))
        {
            var allGAs = (await userManager
                .GetUsersInRoleAsync(Roles.GlobalAdmin).ConfigureAwait(false)).ToList();
            var otherGAs = allGAs.Where(u => u?.Id is string uid && uid != targetSubjectId).ToList();
            if (otherGAs.Count == 0)
                throw new InvalidOperationException(
                    "This is the last GlobalAdmin on the instance. Promote a " +
                    "second GlobalAdmin before deleting this account (the " +
                    "OPS.md §9 'Hand over admin' procedure).");
        }

        // 4. Open the Marten session for the pseudonymization + membership
        //    removal + audit row (the C3 single-commit shape — all in one
        //    SaveChangesAsync).
        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        var ct = System.Threading.CancellationToken.None;
        var now = DateTimeOffset.UtcNow;

        // 5. Pseudonymize the target's audit rows (the OPS.md §9 /
        //    ARCHITECTURE.md §5 "Deletion-of-account interaction" — the
        //    rows remain, the actor id is rewritten to a tombstone). The
        //    summary row (step 9 below) is the audit-of-the-deletion; the
        //    per-row rewrites are the pseudonymization itself.
        var auditRowCount =
            await PseudonymizeAuditRowsAsync(session, targetSubjectId, ct).ConfigureAwait(false);

        // 6 + 7. Remove the target's group memberships, component memberships
        //       (posting rights), and moderator assignments (governing scope)
        //       — they leave every group / lose their posting + governing
        //       standing (the strong-consistency C4 invariant: the next
        //       GetGroupIdsAsync call will not find them). The audit rows for
        //       the *individual* removals are subsumed by the single
        //       "account.delete" summary row (step 9) — the same "one summary,
        //       many rows" shape the purge job's AuditPurgeSummary follows.
        var (groupCount, compCount, modCount) =
            await RemoveAccountMembershipsAsync(session, targetSubjectId, ct).ConfigureAwait(false);

        // 8. Remove the target's Profile row (their PII — name, email, phone,
        //    address, bio, preferences — is gone). The directory's
        //    non-blocked filter (DirectoryService's `!p.Blocked`) would hide
        //    a Blocked profile, but deletion is the *stronger* signal: the
        //    row is simply absent, so every read path (the directory, the
        //    profile detail, the author-name resolution) falls back to the
        //    raw subject id (the existing null-safe `profile?.DisplayName ??
        //    authorId` idiom).
        var profile = await session.LoadAsync<Kumunita.Core.UserInfo.Profile>(
            targetSubjectId, ct).ConfigureAwait(false);
        if (profile is not null)
        {
            session.Delete(profile);
        }

        // 8c. F1 (DB-13 GDPR) — delete the target's resident-authored content
        //     docs in this SAME session / SAME commit (C3 single-write-lane,
        //     ADR 0006; the §2A closed list in
        //     docs/design/prod-hardening-f1-gdpr-deletion.md). Only the
        //     resident's own rows are removed (the listed owner field ==
        //     targetSubjectId); platform announcements (AuthorId == "") and
        //     every other resident's content are untouched.
        //
        //     Owner-field-less sub-entities travel with their parent and have
        //     no resident field to query by: KanbanLane / BoardItemPlacement
        //     are cascade-deleted off the KanbanBoard rows below (the exact
        //     ProjectService.DeleteBoardAsync cascade shape). GroupTranslation
        //     / CommunityTranslation / TranslationResource are NOT removed here
        //     — they sit under Group / Component parents this fix does not
        //     delete (§2C — those parents are out of scope), and
        //     TranslationResource is a global UI-string resource with no
        //     principal field at all. MediaObject rows + their bytes are U4's
        //     set — not touched here (only IdentityService.cs, this one commit).
        var (contentDocs, mediaIds) =
            await ResidentContentEraser.EraseAsync(session, targetSubjectId, ct)
                .ConfigureAwait(false);

        // 8d. S2/E2 (IMPROVE-SEC IS22) — delete the target's identity-adjacent
        //     rows (the F1 §2C "decision" rows now decided [DataClass] Delete in
        //     IS20b/IS20c): SessionDoc, IdentityToken, GuestAccess,
        //     DelegationGrant (either side), the 4 Notification* types, and
        //     OutboxEmail where Recipient == the target's email. Same session /
        //     same commit (C3 — the composer's single SaveChangesAsync below
        //     commits it; the eraser does not commit). target.Email is passed in
        //     (the OutboxEmail match is on the email string, not an id); it is
        //     never logged (the IS08 gate — the eraser's log line carries only
        //     {Resident} = the subject id and {AccountRows} = an int).
        await ResidentAccountRowsEraser.EraseAsync(
                session, targetSubjectId, target.Email, null, ct)
            .ConfigureAwait(false);

        // 8b. ADR 0143 — dissolve every GuardianLink row for the deleted
        //     child (any status) in this same session (C·5 — no dangling
        //     standing; C4 strong-consistency). The ADR 0142 self/admin
        //     branches pass no callback (a GlobalAdmin or a self-deleting
        //     resident is never a child in a GuardianLink); the guardian
        //     delete-child branch supplies one. The per-row dissolution
        //     writes nothing of their own — the single "account.delete"
        //     summary row (step 9) subsumes them, the ADR 0142 "one
        //     summary, many rows" shape.
        if (dissolveGuardianLinksForChildAsync is not null)
        {
            await dissolveGuardianLinksForChildAsync(session, now).ConfigureAwait(false);
        }

        // 9. Exactly one audit row — the "account.delete" summary (the
        //    AuditPurgeSummary precedent: one summary row for a bulk
        //    operation). The actor is the one who performed the deletion
        //    (the resident themselves on self-deletion, the GlobalAdmin on
        //    admin-initiated); the target is the deleted account. The
        //    <c>Via</c> tag carries the branch (Owner / Admin).
        session.Store(AuditRow(now, actorId, actorId,
            "account.delete", AccountKind, targetSubjectId,
            via, Authorization.AccessOutcome.Allow));

        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        // 9b. F1.U4 (DB-13 GDPR) — delete the resident's media BYTES on the
        //     local volume, AFTER the Marten commit and BEFORE the EF Identity
        //     delete (the §5 ordering pin: Marten commit → byte deletes → EF
        //     Identity delete). These are filesystem ops, NOT in the Marten
        //     session — the catalog rows were committed in step 9's single
        //     SaveChangesAsync; only the bytes are removed here. Each
        //     <c>DeleteIfExistsAsync</c> (F1.U2) is idempotent and never throws
        //     for a missing file; we additionally swallow + log any unexpected
        //     failure (a byte-delete failure after the commit leaves a
        //     reclaimable orphan — orphan-safe per C-MED·7 — the §5 "accepted
        //     cross-store window" for this store) so the account deletion
        //     ALWAYS completes. Skipped entirely when the store seam was not
        //     supplied (the two direct-construction test harnesses), which
        //     leaves only the already-committed catalog-row deletes in place.
        var mediaDeleted =
            await DeleteResidentMediaBytesAsync(mediaIds, targetSubjectId, ct).ConfigureAwait(false);

        // 10. Delete the Identity account (EF Core, the identity schema).
        //     This removes the password hash, the role assignments, and the
        //     user row itself. Done AFTER the Marten commit (the
        //     "the account exists before its derivative mt rows" ordering
        //     the class doc's "two stores, one Postgres" paragraph
        //     describes, inverted: the mt cleanup commits first, then the
        //     identity row is removed). A rare failure between the two is
        //     the accepted cross-store window (the same "accepted cross-
        //     store window" the class doc already acknowledges for the
        //     register/verify lanes).
        var result = await userManager.DeleteAsync(target).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Failed to delete the identity account: " +
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        logger.LogInformation(
            "Actor {Actor} (via {Via}) deleted account {Target}. {AuditRows} audit rows pseudonymized, {GroupMemberships} group memberships removed, {CompMemberships} component memberships removed, {ModAssignments} moderator assignments removed, {ContentDocs} resident content docs removed (incl. {MediaCatalog} MediaObject catalog rows), {MediaBytes} media bytes deleted.",
            actorId, via, targetSubjectId,
            auditRowCount, groupCount,
            compCount, modCount, contentDocs,
            mediaIds.Count, mediaDeleted);
    }

    /// <summary>
    /// C10 (IMPROVE-CODE) — step 5 of <see cref="DeleteCoreAsync"/> (extracted
    /// along the phase seam it already comments): pseudonymize the target's
    /// <c>AccessAudit</c> rows in the caller's <paramref name="session"/> —
    /// the rows remain (the OPS.md §9 / ARCHITECTURE.md §5 "Deletion-of-account
    /// interaction"), only the actor id is rewritten to a tombstone. Returns
    /// the number of rows touched (the log summary's <c>{AuditRows}</c>). The
    /// caller's single <c>SaveChangesAsync</c> commits the re-stores (C3).
    /// </summary>
    private async Task<int> PseudonymizeAuditRowsAsync(
        IDocumentSession session, string targetSubjectId, CancellationToken ct)
    {
        var tombstone = $"deleted:{targetSubjectId}";
        var rows = await session.Query<Authorization.AccessAudit>()
            .Where(a => a.ActorId == targetSubjectId)
            .ToListAsync(ct).ConfigureAwait(false);
        foreach (var row in rows)
        {
            row.ActorId = tombstone;
            if (row.EffectivePrincipalId == targetSubjectId)
                row.EffectivePrincipalId = tombstone;
            session.Store(row);
        }
        return rows.Count;
    }

    /// <summary>
    /// C10 (IMPROVE-CODE) — steps 6 + 7 of <see cref="DeleteCoreAsync"/>
    /// (extracted along the phase seam): remove the target's <c>GroupMembership</c>
    /// (they leave every group — the strong-consistency C4 invariant),
    /// <c>ComponentMembership</c> (posting rights), and <c>ModeratorAssignment</c>
    /// (governing scope) rows in the caller's <paramref name="session"/>. The
    /// per-row removal audit is subsumed by the single "account.delete" summary
    /// row (the purge job's AuditPurgeSummary "one summary, many rows" shape).
    /// Returns the three row counts (the log summary's three placeholders).
    /// The caller's single <c>SaveChangesAsync</c> commits the deletes (C3).
    /// </summary>
    private async Task<(int Group, int Component, int Moderator)> RemoveAccountMembershipsAsync(
        IDocumentSession session, string targetSubjectId, CancellationToken ct)
    {
        // The query-then-delete shape is preserved exactly (a query, then
        // session.Delete of each row) — the row set is the same, so we
        // query, delete each, and return the count (the three calls below).
        var group = await session.Query<Kumunita.Core.UserInfo.GroupMembership>()
            .Where(m => m.UserId == targetSubjectId).ToListAsync(ct).ConfigureAwait(false);
        foreach (var gm in group) session.Delete(gm);

        var comp = await session.Query<Kumunita.Core.UserInfo.ComponentMembership>()
            .Where(m => m.UserId == targetSubjectId).ToListAsync(ct).ConfigureAwait(false);
        foreach (var cm in comp) session.Delete(cm);

        var mod = await session.Query<Kumunita.Core.UserInfo.ModeratorAssignment>()
            .Where(m => m.UserId == targetSubjectId).ToListAsync(ct).ConfigureAwait(false);
        foreach (var ma in mod) session.Delete(ma);

        return (group.Count, comp.Count, mod.Count);
    }

    /// <summary>
    /// C10 (IMPROVE-CODE) — step 9b of <see cref="DeleteCoreAsync"/> (extracted
    /// along the phase seam): delete the resident's media BYTES on the local
    /// volume, AFTER the Marten commit and BEFORE the EF Identity delete (the
    /// §5 ordering pin). These are filesystem ops, NOT in the Marten session —
    /// the catalog rows were committed in the caller's single
    /// <c>SaveChangesAsync</c>; only the bytes are removed here. Each
    /// <c>DeleteIfExistsAsync</c> (F1.U2) is idempotent and never throws for a
    /// missing file; we additionally swallow + log any unexpected failure (a
    /// byte-delete failure after the commit leaves a reclaimable orphan —
    /// orphan-safe per C-MED·7 — the §5 "accepted cross-store window" for this
    /// store) so the account deletion ALWAYS completes. Returns the number of
    /// bytes deleted (the log summary's <c>{MediaBytes}</c>). Skipped entirely
    /// when the store seam was not supplied (the two direct-construction test
    /// harnesses), which leaves only the already-committed catalog-row deletes
    /// in place.
    /// </summary>
    private async Task<int> DeleteResidentMediaBytesAsync(
        List<string> mediaIds, string targetSubjectId, CancellationToken ct)
    {
        if (mediaStore is null)
            return 0;
        var deleted = 0;
        foreach (var mediaId in mediaIds)
        {
            try
            {
                if (await mediaStore.DeleteIfExistsAsync(mediaId, ct).ConfigureAwait(false))
                    deleted++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "F1.U4 — byte-delete failed for media id {MediaId} during account {Target} deletion; leaving a reclaimable orphan (orphan-safe per C-MED·7) and continuing.",
                    mediaId, targetSubjectId);
            }
        }
        return deleted;
    }

    // ── ADR 0077 — the account-lane GlobalAdmin emitters ──────────────────

    /// <summary>
    /// ADR 0077 — notify every <c>GlobalAdmin</c> (via the frozen
    /// <see cref="Notifications.NotificationService"/> emitter) of an account
    /// event (the <c>account.signup</c> or <c>account.verified</c> kind). Called
    /// on the caller's open <paramref name="session"/> (the C3 single-commit
    /// shape — the caller commits after). **Best-effort:** any failure (no
    /// emitter wired, no GlobalAdmin, an emit error) is swallowed and logged —
    /// the account operation (register / verify) is the primary domain op and
    /// must succeed even if the notification lane is unavailable (the M6 "email
    /// is best-effort, the inbox is the durable record" posture, D5). Gated by
    /// the instance <see cref="Localization.LocaleSettings.NotifyAdminsOnSignup"/>
    /// flag (the <c>true</c> floor). A fresh instance's single seed-admin
    /// account is not notified about itself (no other admin exists, so the
    /// recipient set is empty — a no-op, not a noise path).
    /// </summary>
    private async Task EmitAccountNotificationAsync(
        IDocumentSession session,
        string kind,
        string accountId,
        string snippet)
    {
        try
        {
            if (notifications is null)
                return;                                          // not wired (test harness) — the account op still succeeds

            if (!await IsNotifyAdminsOnSignupAsync().ConfigureAwait(false))
                return;                                          // the admin disabled the notify — a no-op

            var admins = (await userManager
                .GetUsersInRoleAsync(Roles.GlobalAdmin).ConfigureAwait(false)).ToList();
            if (admins.Count == 0)
                return;                                          // no GlobalAdmin (a fresh instance) — nothing to notify

            // D4 / F10 — a stable, content-derived key per recipient: the same
            // logical event is one inbox row + one email per admin (a re-emission
            // of the same (event, admin) is a no-op, a different admin is a
            // distinct key). The recipient id is part of the key because
            // EmitAsync dedups by key alone (the recipient is a parameter, not part
            // of the key — the §6.3 shape + the per-admin fan-out).
            foreach (var admin in admins)
            {
                var adminId = admin?.Id ?? string.Empty;
                if (adminId.Length == 0)
                    continue;
                await notifications.EmitAsync(
                    session,
                    recipientId: adminId,
                    kind: kind,
                    idempotencyKey: $"notification:{kind}:{accountId}:{adminId}",
                    body: snippet,
                    ct: default).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            // Best-effort (D5): the account operation must not fail because the
            // notification lane is unavailable — log and let the caller commit the
            // domain write. The inbox row / email are the nudge, not the record.
            logger.LogWarning(ex, "Account-lane notification ({Kind}) for {AccountId} was not emitted.",
                kind, accountId);
        }
    }

    /// <inheritdoc />
    public async Task<ThinPrincipal> CompleteSeedAdminSetupAsync(string email, string setupTokenValue, string newPassword)
    {
        var user = await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"No seed-admin account for email '{email}'.");

        var token = await FindTokenAsync(setupTokenValue, kind: IdentityToken.KindSetup, subjectId: user.Id);
        if (token is null)
            throw new InvalidOperationException("Setup token is invalid, already used, or expired (single-use).");

        // Password is a single-use credential: replace it with the resident's chosen one.
        await userManager.RemovePasswordAsync(user);
        await userManager.AddPasswordAsync(user, newPassword);

        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        var profile = (await session.LoadAsync<Profile>(user.Id)) ?? new Profile
        {
            SubjectId = user.Id,
            Email = email,
            DisplayName = email
        };
        profile.Verified = true;
        session.Store(profile);

        var now = DateTimeOffset.UtcNow;
        token.ConsumedAt = now;
        session.Store(token);
        session.Store(AuditRow(now, user.Id, user.Id, "seed-admin.setup", AccountKind, user.Id,
            Authorization.AccessVia.Admin, Authorization.AccessOutcome.Allow));
        await session.SaveChangesAsync();

        // Invalidate any pre-existing session (the seed token is one-time, but the stamp
        // rotation is the belt-and-braces for "the account is brand new").
        await userManager.UpdateSecurityStampAsync(user);

        var identityRoles = (await userManager.GetRolesAsync(user)).ToList();
        return new ThinPrincipal(user.Id, user.ExternalId, IsVerifiedResident: true, identityRoles);
    }

    /// <inheritdoc />
    public async Task<bool> IsFirstBootSetupCompleteAsync()
    {
        // The first-boot setup lane is "still open" exactly while an unconsumed,
        // unexpired KindSetup token exists. ConsumedAt is set by
        // CompleteSeedAdminSetupAsync in its single commit, so its presence is the
        // completion signal; an unconsumed-but-expired token is dead setup material
        // too — the link must not linger advertising an unusable token either.
        await using var session = documentStore.OpenSession(new SessionOptions());
        var now = DateTimeOffset.UtcNow;
        var liveSetupToken = await session
            .Query<IdentityToken>()
            .Where(t => t.Kind == IdentityToken.KindSetup
                && t.ConsumedAt == null
                && t.ExpiresAt > now)
            .FirstOrDefaultAsync();

        return liveSetupToken is null;
    }

    /// <inheritdoc />
    public async Task ConsumeBreakGlassAsync(string subjectId, string token)
    {
        // AdminOverride is the hand-rolled operator-written row (ADR 0004 §B.1): the app
        // writes back ONLY the consumed flag on first presentation (single-use); every
        // subsequent privileged DECISION under it records via:BreakGlass in the
        // AuthorizationModule's inline read.
        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());

        // Two parameterised commands on the same session connection — the UPDATE sets
        // the consumed flag (single-use; the WHERE guards against re-presentation), then
        // a fresh SELECT confirms the row exists and is now consumable (otherwise no
        // UPDATE bound). Both commands run on the same physical connection the session
        // owns, so they see a consistent view.
        var conn = (Npgsql.NpgsqlConnection)session.Connection!;

        await using var update = conn.CreateCommand();
        update.CommandText =
            "UPDATE \"mt\".\"AdminOverride\" " +
            "SET \"consumedAt\" = @now " +
            "WHERE \"userId\" = @userId " +
            "  AND \"token\" = @token " +
            "  AND \"consumedAt\" IS NULL";
        void AddU(string name, object value)
        {
            var p = update.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            update.Parameters.Add(p);
        }
        AddU("@now", DateTimeOffset.UtcNow);
        AddU("@userId", subjectId);
        AddU("@token", token);
        await update.ExecuteNonQueryAsync().ConfigureAwait(false);

        // Confirm the row exists and is now consumed (otherwise the UPDATE bound 0 rows:
        // no such (userId, token) row, or already consumed by someone else).
        await using var verify = conn.CreateCommand();
        verify.CommandText =
            "SELECT (\"consumedAt\" IS NOT NULL) " +
            "FROM \"mt\".\"AdminOverride\" WHERE \"userId\" = @userId AND \"token\" = @token";
        void AddV(string name, object value)
        {
            var p = verify.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            verify.Parameters.Add(p);
        }
        AddV("@userId", subjectId);
        AddV("@token", token);
        var consumed = await verify.ExecuteScalarAsync().ConfigureAwait(false);
        if (consumed is not bool b || !b)
            throw new InvalidOperationException(
                "Break-glass token is not recognized, was already consumed, or expired.");

        // The elevation is in effect from here until ExpiresAt; the decision-time check
        // (AuthorizationService.HasBreakGlassAsync) is the live gate. No row delete — the
        // row stays (history + the consumed flag).
        session.Store(AuditRow(DateTimeOffset.UtcNow, subjectId, subjectId, "break-glass.consume",
            "admin_override", subjectId, Authorization.AccessVia.BreakGlass, Authorization.AccessOutcome.Allow));
        await session.SaveChangesAsync();

        logger.LogInformation("Account {UserId} consumed break-glass elevation.", subjectId);
    }

    // ── Role promote/demote + component scope (ADR 0003) ──────────────────

    /// <inheritdoc />
    public async Task SetRoleAsync(string targetSubjectId, string adminSubjectId,
        IReadOnlyCollection<string> roles, IReadOnlyList<string>? componentIds)
    {
        var admin = await RequireGlobalAdminAsync(adminSubjectId);
        _ = admin;
        var target = await userManager.FindByIdAsync(targetSubjectId)
            ?? throw new InvalidOperationException($"No account '{targetSubjectId}'.");

        // ADR 0030 — roles are independent: `roles` is the *set* of elevated roles the
        // target should hold (any subset of the three). `Member` is the implicit
        // verified-resident standing and never appears here, so it is filtered out
        // defensively (a stray "Member" in the set is a no-op either way).
        var wants = (roles ?? [])
            .Where(r => r is Roles.Moderator or Roles.Translator or Roles.GlobalAdmin)
            .ToHashSet();
        var wantsGlobalAdmin = wants.Contains(Roles.GlobalAdmin);
        var wantsModerator = wants.Contains(Roles.Moderator);
        var wantsTranslator = wants.Contains(Roles.Translator);

        var targetRoles = (await userManager.GetRolesAsync(target)).ToHashSet();
        bool rolesChanged =
            wantsGlobalAdmin != targetRoles.Contains(Roles.GlobalAdmin) ||
            wantsModerator   != targetRoles.Contains(Roles.Moderator) ||
            wantsTranslator  != targetRoles.Contains(Roles.Translator);

        // Apply the GlobalAdmin/Moderator/Translator identity roles (Member is the implicit
        // verified standing — no EF role for it). AddTo/RemoveFromRole manage the role
        // membership; the role row itself is created by the host's seed (FirstBootSeeder)
        // when the account is granted the role for the first time.
        if (wantsGlobalAdmin)  await userManager.AddToRoleAsync(target, Roles.GlobalAdmin);
        else                   await userManager.RemoveFromRoleAsync(target, Roles.GlobalAdmin);
        if (wantsModerator)    await userManager.AddToRoleAsync(target, Roles.Moderator);
        else                   await userManager.RemoveFromRoleAsync(target, Roles.Moderator);
        if (wantsTranslator)   await userManager.AddToRoleAsync(target, Roles.Translator);
        else                   await userManager.RemoveFromRoleAsync(target, Roles.Translator);

        // Security stamp: demoted accounts lose elevated access on the NEXT request, not at
        // cookie expiry (OPS §10). Rotate regardless to be safe (a re-signin is required to
        // pick up new claims).
        if (rolesChanged)
            await userManager.UpdateSecurityStampAsync(target);

        // Component-scope assignments (mt): upsert on promote-to-Moderator, delete on demote
        // (strong consistency — the next GetAssignmentsAsync reflects it; the "who cleared,
        // when" history is in the access_audit admin-action row, not in the assignment).
        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        if (wantsModerator && componentIds is { Count: > 0 })
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var componentId in componentIds)
                session.Store(new ModeratorAssignment
                {
                    Id = Guid.NewGuid().ToString("N"),
                    UserId = targetSubjectId,
                    ComponentId = componentId,
                    GrantedBy = adminSubjectId,
                    At = now
                });
        }
        else
        {
            // Delete every assignment for the target (strong consistency, invariant C4).
            var existing = await session
                .Query<ModeratorAssignment>()
                .Where(a => a.UserId == targetSubjectId)
                .ToListAsync()
                .ConfigureAwait(false);
            foreach (var a in existing)
                session.Delete<ModeratorAssignment>(a.Id);
        }

        session.Store(AuditRow(DateTimeOffset.UtcNow, adminSubjectId, adminSubjectId,
            "role", "role", targetSubjectId, Authorization.AccessVia.Admin, Authorization.AccessOutcome.Allow));
        await session.SaveChangesAsync();

        logger.LogInformation("Admin {Admin} set {Target}'s roles to {Roles}/{Components}.",
            adminSubjectId, targetSubjectId, string.Join(",", wants), string.Join(",", componentIds ?? []));
    }

    // ── Password (self-serve or admin reset) ───────────────────────────────

    /// <inheritdoc />
    public async Task ChangePasswordAsync(string subjectId, string newPassword, bool byAdmin)
    {
        // ADR 0138 — the enforcement guard (the single place the lock bites,
        // so the resident's self-serve change-password surface (now on the
        // Security tab, ADR 0280) and this write path can never disagree). It applies to the self-serve lane only (byAdmin
        // false): an admin's reset of a sample account is always allowed (an
        // admin must be able to recover a demo credential they set), so the
        // guard is a no-op for the byAdmin path. A locked, non-admin sample
        // account is denied *before* any write (no audit row for the blocked
        // attempt — the RemoveLanguageAsync / M·7 fail-closed pin) and before
        // any security-stamp rotation (so the account stays usable).
        if (!byAdmin && await IsChangePasswordLockedForAsync(subjectId))
        {
            throw new UnauthorizedAccessException(
                "This is a demo (sample) account and password changes are locked by the " +
                "administrator. Use a different account if you need to test a password change.");
        }

        var user = await userManager.FindByIdAsync(subjectId)
            ?? throw new InvalidOperationException($"No account '{subjectId}'.");

        await userManager.RemovePasswordAsync(user);
        await userManager.AddPasswordAsync(user, newPassword);
        await userManager.UpdateSecurityStampAsync(user);      // invalidate every existing session

        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        session.Store(AuditRow(DateTimeOffset.UtcNow, subjectId, subjectId,
            "password.change", AccountKind, subjectId,
            byAdmin ? Authorization.AccessVia.Admin : Authorization.AccessVia.Owner,
            Authorization.AccessOutcome.Allow));
        await session.SaveChangesAsync();

        logger.LogInformation("Password changed for {UserId} ({ByAdmin}).", subjectId, byAdmin ? "admin" : "self");
    }

    // ── Session kill-all (F5, AUTH-04) ──────────────────────────────────────

    /// <inheritdoc />
    public async Task KillAllSessionsAsync(string subjectId)
    {
        var user = await userManager.FindByIdAsync(subjectId)
            ?? throw new InvalidOperationException($"No account '{subjectId}'.");

        // Rotate the security stamp — the SAME seam ChangePasswordAsync uses at the
        // password-change lane (IdentityService.cs:1477). No migration: User already
        // carries the SecurityStamp column (inherited from IdentityUser, the InitialIdentity
        // migration). The gate that makes this rotation operative (RevocationStampMiddleware
        // in the Web) is the repo's actual invalidation idiom (ADR 0006-B "fat authorization,
        // per request"; the SecurityStampValidator is intentionally neutered by the 14-day
        // ValidationInterval — see Program.cs + PrivilegedStampMiddleware). The stamp
        // rotation is the server-side revocation mark; the Web gate reads it per-request
        // and signs out any session whose AuthenticationProperties.IssuedUtc (the
        // session's birth time, set by the cookie handler at sign-in and NOT refreshed by
        // sliding expiration) predates the stamp.
        await userManager.UpdateSecurityStampAsync(user);

        // Audit (C3 single-commit, same Marten session as the EF write — the same
        // shape ChangePasswordAsync uses for its audit row; ADR 0006 "audit-by-default").
        await using var session = documentStore.OpenSession(new SessionOptions());
        session.Store(AuditRow(DateTimeOffset.UtcNow, subjectId, subjectId,
            "session.kill_all", AccountKind, subjectId,
            Authorization.AccessVia.Owner,
            Authorization.AccessOutcome.Allow));
        await session.SaveChangesAsync();

        logger.LogInformation("Kill-all: security stamp rotated for {UserId}.", subjectId);
    }
}
