using Kumunita.Core.UserInfo;

namespace Kumunita.Core.Identity;

/// <summary>
/// The IdentityModule's public surface — ADR 0006 §A (frozen; changes are breaking).
/// <para>
/// Only the IdentityModule knows the identity source (ASP.NET Core Identity,
/// <c>identity</c> schema; cookie claims now, the later OIDC <c>sub</c> swap is mechanical
/// and confined to this module) and only it issues <see cref="ThinPrincipal"/>.
/// </para>
/// <para>
/// **M1 lifecycle methods** (the ADR 0006-E *compatible* lane — added to the owning
/// module's public surface, named here): the M1 design's identity lifecycle
/// (signup, the verification token + admin manual-verify valve, the seed-admin setup
/// token) reaches the module through these. Each such action appends its own
/// <see cref="Authorization.AccessAudit"/> admin-action row (<c>via: Admin</c> /
/// <c>via: BreakGlass</c>) in the same commit as its own writes.
/// </para>
/// </summary>
public interface IIdentityService
{
    /// <summary>The thin principal for the current request, or null when unauthenticated
    /// (built from <see cref="IClaimsSource.Current"/>'s claim set — the claim shape is
    /// the whole principal).</summary>
    Task<ThinPrincipal?> GetCurrentAsync();

    /// <summary>The principal for a given subject, or null (identity lookup for decisions
    /// made on someone else's behalf — e.g. resolving a moderator's scope).</summary>
    Task<ThinPrincipal?> GetBySubjectAsync(string subjectId);

    // ── M1 lifecycle (ADR 0006-E compatible lane) ──────────────────────────────────────

    /// <summary>
    /// Signup (OPS §2/§7, the design's "the first resident touch"): create an
    /// *unverified* account, bootstrap its <see cref="Profile"/>, create a fresh
    /// <see cref="IdentityToken"/> (kind
    /// <see cref="IdentityToken.KindVerify"/>, idempotency <c>verify:{userId}:1</c>), and
    /// stage the one verification email (<see cref="OutboxEmail"/>) — the only world seam,
    /// the single-send designed handoff. The account cannot sign in until verified.
    /// </summary>
    Task<ThinPrincipal> RegisterAsync(string displayName, string email, string password);

    /// <summary>
    /// Resend signup verification for an existing *unverified* account (the resident
    /// retries signup and gets the "account already exists" error — their email was
    /// lost or never arrived): mints a fresh <see cref="IdentityToken"/> (kind
    /// <see cref="IdentityToken.KindVerify"/>, idempotency <c>verify:{userId}:{attempt}</c>)
    /// and stages a new verification email (<see cref="OutboxEmail"/>) in one commit;
    /// prior attempts stay valid until their own expiry (VerificationOptions §6.2).
    /// Bound by <see cref="VerificationOptions.MaxVerifyAttempts"/> — a resident who
    /// exhausts the attempts gets a result directing them to the admin manual-verify
    /// valve, not an exception. A <c>false</c> result always carries a
    /// user-presentable <see cref="ResendVerificationResult.Reason"/>.
    /// </summary>
    Task<ResendVerificationResult> ResendVerificationEmailAsync(string email);

    /// <summary>
    /// GA (ADR 0038): resolve an email to a subject id (the assign
    /// form's one external identifier). A read — no audit row, no
    /// mutation. Returns the subject id, or null if the email has no
    /// account (the Web's user-presentable error surface — the ADR
    /// 0008 "a non-guardian learns nothing" shape: a null return, not
    /// an exception that names the email). ADR 0006-E compatible
    /// ADD — the M1 lifecycle ADD precedent (the
    /// <c>ResendVerificationEmailAsync</c> shape, a read over
    /// <c>userManager.FindByEmailAsync</c>).
    /// </summary>
    Task<string?> FindSubjectByEmailAsync(string email);

    /// <summary>
    /// Consume a verification link (the resident clicked the link — the handoff ends
    /// on-platform): set <see cref="Profile.Verified"/>, mark the token consumed, audit
    /// (<c>via: Owner</c> — the resident verifying their own account).
    /// </summary>
    Task<Profile> VerifyWithTokenAsync(string tokenValue);

    /// <summary>
    /// The admin manual-verify valve (OPS §7 safety valve — the unverified-signup pile-up
    /// signal when verification emails dead-letter): a GlobalAdmin verifies an unverified
    /// account in-app; the account becomes usable immediately. Audited
    /// (<c>via: Admin</c>, target the account).
    /// </summary>
    Task<Profile> ManuallyVerifyAsync(string targetSubjectId, string adminSubjectId);

    /// <summary>
    /// Block a resident (the admin suspension lane): a GlobalAdmin marks the target
    /// <see cref="Profile.Blocked"/>; the account immediately loses all role standing
    /// (no <c>Member</c>/<c>Moderator</c>/<c>GlobalAdmin</c>, so it cannot act or be
    /// granted standing) until unblocked. The account and its documents are preserved
    /// (a reversible suspension, not a delete). Rotates the security stamp (existing
    /// sessions invalidate on their next re-mint) and appends an audit row
    /// (<c>via: Admin</c>, action <c>"block"</c>, target the account). Only a GlobalAdmin
    /// may call this.
    /// </summary>
    Task BlockAsync(string targetSubjectId, string adminSubjectId);

    /// <summary>
    /// Unblock a resident (the inverse of <see cref="BlockAsync"/>): restores the target's
    /// <see cref="Profile.Blocked"/> to false, so its standing (roles) is available again at
    /// next sign-in / re-mint. Rotates the security stamp and appends an audit row
    /// (<c>via: Admin</c>, action <c>"unblock"</c>, target the account). Only a GlobalAdmin
    /// may call this.
    /// </summary>
    Task UnblockAsync(string targetSubjectId, string adminSubjectId);

    /// <summary>
    /// Seed-admin bootstrap (OPS §2, FirstBootSeeder): the one-time setup token is
    /// consumed, invalidating it; the account is sign-in-ready (verified), a password set;
    /// a duplicate token use is rejected (single-use). Audited (<c>via: Admin</c>).
    /// </summary>
    Task<ThinPrincipal> CompleteSeedAdminSetupAsync(string email, string setupTokenValue, string newPassword);

    /// <summary>
    /// Whether the first-boot seed-admin setup (OPS §2) has been completed: <c>true</c>
    /// once the one-time setup <see cref="IdentityToken"/> (kind
    /// <see cref="IdentityToken.KindSetup"/>) has been consumed by
    /// <see cref="CompleteSeedAdminSetupAsync"/> (or has since expired). The Web's
    /// login page uses this to hide the "Received a first-boot setup token?" hint once
    /// there is no longer a live setup lane to complete — the account already signs in
    /// with a real password.
    /// </summary>
    Task<bool> IsFirstBootSetupCompleteAsync();

    /// <summary>
    /// Consume a break-glass <see cref="Authorization.AdminOverride"/> token (§4.5, OPS §9):
    /// the target account, in-app at <c>/admin/break-glass</c>, presents the token exactly
    /// once; the row's <see cref="Authorization.AdminOverride.ConsumedAt"/> is set (single-use);
    /// the elevation lasts until <see cref="Authorization.AdminOverride.ExpiresAt"/> — every
    /// subsequent privileged decision under it records <c>via: BreakGlass</c> (the
    /// AuthorizationModule's inline read, not an identity state).
    /// </summary>
    Task ConsumeBreakGlassAsync(string subjectId, string token);

    /// <summary>
    /// Role promote/demote + component-scope assignment (ADR 0003; the <c>Translator</c>
    /// lane is ADR 0021; **role independence** — any combination of elevated roles — is
    /// ADR 0030): a GlobalAdmin sets the target's **set** of elevated roles, <paramref
    /// name="roles"/> — any subset of <c>GlobalAdmin</c>, <c>Moderator</c>, and
    /// <c>Translator</c> (an account may hold more than one at once; e.g. a GlobalAdmin
    /// who also holds <c>Translator</c> to stand in for the community's translators).
    /// <c>Member</c> is the implicit verified-resident standing and is never carried in
    /// the set; an **empty** set means "no elevated role" (a plain Member). For a
    /// <c>Moderator</c>, <paramref name="componentIds"/> is the complete scope
    /// (null/empty clears it — the only standing-moderator path is
    /// <c>moderatorAccess</c>, invariant C5); a <c>Translator</c> holds no component
    /// scope, so <paramref name="componentIds"/> applies only when <c>Moderator</c> is
    /// in the set. Rotates the security stamp (invalidates existing sessions — a demoted
    /// account loses the standing immediately, not at cookie expiry, OPS §10). Appends
    /// an audit row <c>(via: Admin, action: "role")</c>. Only a GlobalAdmin may call
    /// this.
    /// </summary>
    Task SetRoleAsync(string targetSubjectId, string adminSubjectId,
        IReadOnlyCollection<string> roles, IReadOnlyList<string>? componentIds);

    /// <summary>
    /// Change password (self-serve, or a GlobalAdmin reset): rotates the security stamp
    /// so the account's existing sessions invalidate. Appends an audit row
    /// <c>(via: Owner | Admin)</c>.
    /// <para>
    /// <b>ADR 0138 — the sample-account guard.</b> The self-serve lane
    /// (<c>byAdmin: false</c>) is additionally subject to
    /// <see cref="IsChangePasswordLockedForAsync"/>: when a
    /// <c>SampleData__Enabled</c> instance has opted in to the lock (an admin
    /// flipped <see cref="SetSamplePasswordChangeLockedAsync"/>) and the subject
    /// is a non-admin sample account, this throws
    /// <see cref="UnauthorizedAccessException"/> <b>before</b> any write (no
    /// audit row for the blocked attempt — the fail-closed pin). The admin
    /// reset lane (<c>byAdmin: true</c>) is always allowed — an admin must be
    /// able to recover a demo credential they set.
    /// </para>
    /// </summary>
    Task ChangePasswordAsync(string subjectId, string newPassword, bool byAdmin);

    // ── Signup policy (ADR 0050 — the admin-managed open / invitation-only gate) ──

    /// <summary>
    /// Whether self-service sign-up is currently open on this instance (ADR 0050).
    /// The Web's <c>AccountController</c> reads this to hide the public <c>Sign up</c>
    /// surface and to deny the signup write when it is closed (the README's deferred
    /// "Invitation-only sign-up" item — the long-term default the community should
    /// run under once it widens beyond the development circle). A read (no audit
    /// row); the <c>true</c> floor — a missing singleton or a null value both yield
    /// <c>true</c>, so a fresh instance ships with sign-up open.
    /// </summary>
    Task<bool> IsSignupOpenAsync();

    /// <summary>
    /// Set whether self-service sign-up is open (ADR 0050): a GlobalAdmin flips the
    /// instance-wide gate — <c>true</c> opens the public <c>Sign up</c> surface
    /// (the development-circle default), <c>false</c> closes it (the invitation-only
    /// state: existing residents are unaffected; only new self-service accounts are
    /// gated). Writes the <see cref="Kumunita.Core.Localization.LocaleSettings.IsSignupOpen"/>
    /// singleton and appends exactly one <c>AccessAudit</c> row
    /// (<c>via: Admin</c>, action <c>"signup.set-open"</c>, target "signup") in the
    /// same session (C3 — no silent, unaudited access). Only a GlobalAdmin may call
    /// this; the Web's <c>AdminSignupController</c> enforces the gate.
    /// </summary>
    Task SetSignupOpenAsync(bool open, string adminSubjectId);

    // ── Sample-data change-password lock (ADR 0138) ────────────────────────
    // A demo-instance guard: when the SampleData__Enabled instance is opted in
    // (an admin flips the LocaleSettings.SamplePasswordChangeLocked gate at
    // /admin/sample), a non-admin sample account (the closed
    // SampleDataSeeder.SampleAccountEmails set) is denied the self-serve
    // password change — so a visitor testing the demo can't break the shared
    // credentials. The sample GlobalAdmin and every real account are exempt.

    /// <summary>
    /// Whether the <c>SampleData__Enabled</c> flag is set on this instance
    /// (ADR 0138) — the gate that decides whether the sample-data surfaces
    /// (the <c>/admin/sample</c> change-password lock and the locked-account
    /// notice) exist at all. A read (no audit row); a real deployment never
    /// carries the flag, so this is <c>false</c> there and the surfaces are
    /// unreachable by construction (the ADR 0056 "unreachable by construction"
    /// shape).
    /// </summary>
    Task<bool> IsSampleDataEnabledAsync();

    /// <summary>
    /// Whether sample accounts are currently <b>locked out of changing their
    /// own password</b> (ADR 0138). The <c>false</c> floor — a missing
    /// singleton or an unset value both yield <c>false</c>, so a fresh or real
    /// instance never blocks a password change (the deliberate inverse of the
    /// codebase <c>true</c>-floor convention, the <see
    /// cref="Localization.LocaleSettings.MessagingEnabled"/> shape). A read
    /// (no audit row); the <see cref="IsSampleDataEnabledAsync"/> flag is NOT
    /// consulted here — the lock is a pure instance value, and the
    /// <see cref="IsChangePasswordLockedForAsync"/> decision combines it with
    /// the sample-account membership.
    /// </summary>
    Task<bool> IsSamplePasswordChangeLockedAsync();

    /// <summary>
    /// Set whether sample accounts are locked out of changing their own
    /// password (ADR 0138): a GlobalAdmin flips the instance-wide gate at
    /// <c>/admin/sample</c> — <c>true</c> locks the non-admin sample accounts
    /// (so a demo visitor can't break the shared credentials), <c>false</c>
    /// unlocks them. Writes the
    /// <see cref="Localization.LocaleSettings.SamplePasswordChangeLocked"/>
    /// singleton and appends exactly one <c>AccessAudit</c> row
    /// (<c>via: Admin</c>, action <c>"sample.set-password-lock"</c>, target
    /// "sample") in the same session (C3 — no silent, unaudited access). Only
    /// a GlobalAdmin may call this; the Web's <c>AdminSampleDataController</c>
    /// enforces the gate (and 404s when <c>SampleData__Enabled</c> is unset).
    /// </summary>
    Task SetSamplePasswordChangeLockedAsync(bool locked, string adminSubjectId);

    /// <summary>
    /// Whether the given <paramref name="subjectId"/>'s self-serve password
    /// change is currently <b>locked</b> (ADR 0138). Combines the three
    /// conditions into one decision (the single place the rule lives, so the
    /// resident <c>/account/password</c> surface and the
    /// <see cref="ChangePasswordAsync"/> enforcement guard agree):
    /// <see cref="IsSampleDataEnabledAsync"/> (the instance carries the closed
    /// set) <b>AND</b> <see cref="IsSamplePasswordChangeLockedAsync"/> (the
    /// admin opted in) <b>AND</b> the subject is a member of
    /// <see cref="Bootstrap.SampleDataSeeder.SampleAccountEmails"/> (a sample
    /// account) <b>AND NOT</b> a <c>GlobalAdmin</c> (the sample admin is
    /// exempt). Returns <c>false</c> for every real account and for the sample
    /// admin regardless of the gate.
    /// </summary>
    Task<bool> IsChangePasswordLockedForAsync(string subjectId);

    // ── Admin account notifications (ADR 0077 — the admin-lane signup/verify
    //    notify gate) ──

    /// <summary>
    /// Whether the account lane notifies the GlobalAdmins when a resident signs
    /// up and when a resident verifies their account (ADR 0077). A read (no audit
    /// row); the <c>true</c> floor — a missing singleton or an unset value both
    /// yield <c>true</c>, so a fresh instance ships with the admin notification
    /// on (the M6 lean-default posture, the <see cref="IsSignupOpenAsync"/>
    /// <c>true</c> floor).
    /// </summary>
    Task<bool> IsNotifyAdminsOnSignupAsync();

    /// <summary>
    /// Set whether the account lane notifies the GlobalAdmins on sign-up and
    /// verification (ADR 0077): a GlobalAdmin flips the instance-wide gate —
    /// <c>true</c> enables the <c>account.signup</c> / <c>account.verified</c>
    /// emitters, <c>false</c> disables them. Writes the
    /// <see cref="Kumunita.Core.Localization.LocaleSettings.NotifyAdminsOnSignup"/>
    /// singleton and appends exactly one <c>AccessAudit</c> row
    /// (<c>via: Admin</c>, action <c>"signup.set-notify"</c>, target "signup") in
    /// the same session (C3 — no silent, unaudited access). Only a GlobalAdmin
    /// may call this; the Web's <c>AdminSignupController</c> enforces the gate.
    /// </summary>
    Task SetNotifyAdminsOnSignupAsync(bool notify, string adminSubjectId);

    // ── M19 guest standing (ADR 0120, D2 — the single audited write lane) ────

    /// <summary>
    /// The guest standing for a subject (ADR 0120, D2): the bounded window
    /// (D3) + the closed surface set (D4). A **read** (no audit row — the
    /// <see cref="IsSignupOpenAsync"/> plain-read shape). Returns <c>null</c> when the
    /// account has no settled standing yet — a guest with no allowance is a
    /// shell (C-M19·4, the empty floor). U02's claim mint and U04's admin
    /// surface both ride this read.
    /// </summary>
    Task<GuestAccess?> GetGuestAccessAsync(string subjectId);

    /// <summary>
    /// Set the guest standing for a subject (ADR 0120, D2): a GlobalAdmin
    /// settles the bounded window (D3) + the closed surface set (D4) + marks
    /// the account <see cref="Kumunita.Core.UserInfo.Profile.IsGuest"/>. Writes
    /// the <see cref="GuestAccess"/> document and appends exactly one
    /// <c>AccessAudit</c> row (<c>via: Admin</c>, action
    /// <c>"guest.set-standing"</c>, <c>TargetKind</c> "guest",
    /// <c>TargetId</c> "guest:{subjectId}") in the same session (C-M19·5 — no
    /// silent, unaudited access). This is the **single** write lane: the Web's
    /// <c>AdminGuestsController</c> (U04) enforces the GlobalAdmin standing and
    /// calls only this — a controller may not <c>IDocumentSession.Store</c> the
    /// document directly (C-M19·5, D2's *Forbids*).
    /// </summary>
    Task SetGuestAccessAsync(GuestAccess access, string adminSubjectId);

    // ── Account deletion (ADR 0142 — the resident-leave / admin-removal lane) ──

    /// <summary>
    /// ADR 0142 — <b>delete a resident's account</b> (the privacy lane the
    /// resident self-serve surface <c>POST /account/delete</c> and the
    /// GlobalAdmin surface <c>POST /admin/delete</c> both reach). The target
    /// loses every standing immediately: their
    /// <see cref="Kumunita.Core.Authorization.AccessAudit"/> rows are
    /// **pseudonymized** (the actor id replaced by a tombstone), their
    /// group/community memberships are removed (they leave the group and
    /// component membership rows), their
    /// <see cref="Kumunita.Core.UserInfo.Profile"/> row is removed, and the
    /// underlying ASP.NET Identity account is deleted (their password hash
    /// and roles are gone). The audit trail itself is preserved: the
    /// rows that were written *by* the target before this moment remain —
    /// their <c>ActorId</c> is rewritten to a deterministic
    /// <c>"deleted:{subjectId}"</c> tombstone, so an operator can prove
    /// what happened without retaining the identity (OPS.md §9 /
    /// ARCHITECTURE.md §5 "Deletion-of-account interaction").
    /// <para>
    /// **Two branches, one seam (the ADR 0138 self-serve/admin
    /// distinction):**
    /// </para>
    /// <list type="bullet">
    /// <item><b>Self-deletion</b> — <paramref name="adminSubjectId"/>
    /// equals <paramref name="targetSubjectId"/>: the resident is deleting
    /// *their own* account (the Web self-serve surface
    /// <c>POST /account/delete</c> invokes this shape, after verifying the
    /// resident's current password). **ADR 0142 D5 gate:** the self-serve
    /// lane is reachable only by a <c>GlobalAdmin</c> — a non-GlobalAdmin
    /// invoking this branch throws <c>UnauthorizedAccessException</c>
    /// (the fail-closed pin, mirroring the admin-initiated branch). The
    /// Web surface renders the ADR 0138 "surface-replaced-by-notice" shape
    /// for a non-GlobalAdmin, so the resident sees the gate before
    /// submitting. The audit row is <c>Via: Owner</c>.</item>
    /// <item><b>Admin-initiated</b> — <paramref name="adminSubjectId"/>
    /// differs from <paramref name="targetSubjectId"/>: a GlobalAdmin is
    /// removing *another* resident (the Web admin surface
    /// <c>POST /admin/delete</c>). The <c>GlobalAdmin</c> gate applies
    /// (<see cref="BlockAsync"/> / <see cref="UnblockAsync"/> shape — the
    /// fail-closed pin); a non-admin invoking this branch throws
    /// <c>UnauthorizedAccessException</c>. The audit row is
    /// <c>Via: Admin</c>.</item>
    /// </list>
    /// <para>
    /// **Last-GlobalAdmin guard (the lockout pin, the ADR 0006-E
    /// precedent):** if the target holds the <c>GlobalAdmin</c> role and
    /// they are the *only* GlobalAdmin on the instance, the lane throws
    /// <see cref="InvalidOperationException"/> before any write — a
    /// single-admin instance cannot self-erase its last admin (the
    /// OPS.md §9 "Hand over admin" procedure is the recovery path).
    /// </para>
    /// <para>
    /// **Audit:** exactly one <c>AccessAudit</c> row (action
    /// <c>"account.delete"</c>, <c>TargetKind</c> "account",
    /// <c>TargetId</c> the subject, <c>Via: Admin</c>) written in the
    /// *same Marten session* as the pseudonymization + membership removal
    /// (C3 — no silent, unaudited access). The pseudonymization itself
    /// is not a per-row audit — the single <c>"account.delete"</c> row
    /// is the summary row, the way the purge job's
    /// <c>AuditPurgeSummary</c> summarizes the bulk delete (the same
    /// "one summary, many rows" shape).
    /// </para>
    /// <para>
    /// **Fail-closed / idempotency:** if the target account does not
    /// exist (already deleted, or never created), the lane throws
    /// <see cref="InvalidOperationException"/> before any write. A
    /// *second* call for the same subject (after a successful first
    /// call) throws the same <c>InvalidOperationException</c> (the
    /// Identity row is gone) — the lane is not a silent no-op.
    /// </para>
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Either branch was
    /// invoked by a non-<c>GlobalAdmin</c> (the self-serve lane's ADR 0142
    /// D5 gate, or the admin-initiated branch's fail-closed pin).</exception>
    /// <exception cref="InvalidOperationException">The target account does
    /// not exist, or the target is the last <c>GlobalAdmin</c> on the
    /// instance (the lockout pin — applies to <i>both</i> branches: a lone
    /// GlobalAdmin cannot self-delete).</exception>
    Task DeleteAccountAsync(string targetSubjectId, string adminSubjectId);

    // ── ADR 0143 — guardian delete-child (the GU standing over the ADR 0142 core) ──

    /// <summary>
    /// ADR 0143 — a **guardian** deletes a **child** account: the 6th GU
    /// supervisory action (ADR 0028's five + this), reusing the ADR 0142
    /// deletion core (<see cref="DeleteAccountAsync"/>'s shared body).
    /// <para>
    /// **Standing gate (G·2/G·3, live):** before any write, an <b>active</b>
    /// <see cref="Kumunita.Core.UserInfo.GuardianLink"/> with
    /// <c>GuardianId == <paramref name="guardianId"/></c> and
    /// <c>ChildId == <paramref name="childId"/></c> must exist, else
    /// <see cref="UnauthorizedAccessException"/> (the Web's 404 — the
    /// <c>GuardActiveLinkAsync</c> shape every other GU seam runs). No
    /// projection, no cache — a dissolve is live on the next read.
    /// </para>
    /// <para>
    /// **On success:** every <see cref="Kumunita.Core.UserInfo.GuardianLink"/>
    /// row for the child (any status) is dissolved in the same session (C·5 —
    /// no dangling standing; a co-guardian's row ends here, C4 strong
    /// consistency); then the ADR 0142 core runs — the child's audit rows are
    /// pseudonymized to a <c>deleted:{childId}</c> tombstone, their
    /// memberships + <see cref="Kumunita.Core.UserInfo.Profile"/> are removed,
    /// the Identity account is deleted, and exactly one
    /// <c>"account.delete"</c> summary audit row is written
    /// (<see cref="Authorization.AccessVia.Guardian"/>).
    /// </para>
    /// <para>
    /// **Not a silent no-op:** a second call for the same child after a
    /// successful first is refused — the standing gate runs first and the
    /// successful delete dissolved the link (so the gate now fails), surfacing
    /// <see cref="UnauthorizedAccessException"/> (the Web's 404). A repeat
    /// attempt cannot re-delete, and it errors rather than succeeding.
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="childId"/> or
    /// <paramref name="guardianId"/> is null/whitespace.</exception>
    /// <exception cref="UnauthorizedAccessException">No active
    /// <see cref="Kumunita.Core.UserInfo.GuardianLink"/> for this
    /// (guardian, child) pair — the actor has no standing (deny-by-default,
    /// G·3; the Web surfaces a 404). This is also the failure for a second
    /// call after a successful delete (the link was dissolved).</exception>
    /// <exception cref="InvalidOperationException">The child account does
    /// not exist (defensive, after the standing gate passed), or the child is
    /// the last <c>GlobalAdmin</c> on the instance (the lockout pin — a child
    /// is never a GlobalAdmin, so this is a no-op in practice).</exception>
    Task DeleteChildAccountAsync(string childId, string guardianId);
}
