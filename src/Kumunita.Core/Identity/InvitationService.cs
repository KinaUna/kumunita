using Kumunita.Core.Authorization;
using Kumunita.Core.Notifications;
using Marten;

namespace Kumunita.Core.Identity;

/// <summary>
/// The <see cref="IInvitationService"/> impl (ADR 0304, IMPROVE-UX A07) —
/// the email invitation's Core seam. A plain store-composing service (the
/// <see cref="KioskLinkService"/> structural template): each operation owns
/// its own session (the <c>EscalationTokenService</c> committed-lane shape —
/// the invitation's writes are its own lane, not part of a caller's
/// transaction; the A08–A12 surfaces call one seam per action).
/// <para>
/// **Hash only at rest (ADR 0304 D2):** <see cref="CreateAsync"/> generates
/// the token (<c>kinv_</c> + 32 CSPRNG bytes base64url — the
/// <see cref="KioskLinkService"/> <c>kskl_</c> /
/// <see cref="Kumunita.Core.ErrorReports.EscalationTokenService"/>
/// <c>kesc_</c> shape), stores only the SHA-256 hex digest
/// (<see cref="Invitation.TokenHash"/> — the
/// <see cref="IdentityService.Sha256Hex"/> implementation, shared
/// <c>internal</c> so the sibling hashes identically to the rest of the
/// tree), returns the raw token **once**, and discards it — the store
/// cannot recover it (the 0159 ESC·3 / F2 rule).
/// </para>
/// <para>
/// **Audit (the <see cref="KioskLinkService"/> one-row convention):**
/// exactly one <c>invitation.create</c> / <c>invitation.revoke</c> /
/// <c>invitation.accept</c> <c>AccessAudit</c> row per operation
/// (<c>Via = Owner</c>, <c>TargetKind = "invitation"</c>,
/// <c>TargetId</c> = the invitation's id).
/// <see cref="ValidateAsync"/> writes **zero** rows (the M35·5 / 0159
/// "the read is not an access decision" floor — the access decision is
/// A08's sign-up gate's job).
/// </para>
/// <para>
/// **Cross-store ordering on <see cref="AcceptAsync"/> (the ADR 0142 D6
/// precedent):** the <see cref="IIdentityService.RegisterAsync"/> lane
/// commits the account-creation first (the primary domain op — EF Identity
/// + Marten Profile/Token/OutboxEmail in its own session); then the
/// invitation mark (the derivative) is committed in a fresh session. A
/// failure between the two leaves the account present with the invitation
/// still <c>Pending</c> — a recoverable state (the inviter can revoke and
/// re-issue, or the admin can delete the account), never the inverse.
/// </para>
/// </summary>
public sealed class InvitationService(
    IDocumentStore store,
    IIdentityService identity,
    // IMPROVE-UX A09 — the delivery seam (InviteAsync) + the read seam
    // (ListAsync). All optional (default null) so the A07/A08 test harness
    // (`new InvitationService(store, identity)` in
    // InvitationServiceTests) keeps compiling: a null mailer / base-URL
    // reader skips the email-staging lane (the A09 InviteAsync test harness
    // supplies them positionally); a null translation provider / community
    // name floors to the raw-English literals (the
    // BuildVerificationEmailAsync precedent). DI always supplies the live
    // values.
    IMailerStage? mailer = null,
    IInstanceBaseUrlReader? baseUrlReader = null,
    Kumunita.Core.Localization.ITranslationProvider? translationProvider = null,
    Microsoft.Extensions.Options.IOptions<Kumunita.Core.CommunityOptions>? communityOptions = null,
    // IMPROVE-UX A10 — the inviter-acceptance nudge (AcceptAsync step 4).
    // Optional (default null) so the A07/A08 test harness
    // (`new InvitationService(store, identity)`) and the A09 BootA09Async
    // positional call (six args) keep compiling: a null emitter skips the
    // notify lane entirely (the best-effort degrade path — the
    // EmitAccountNotificationAsync D5 posture). DI always supplies the live
    // INotificationEmitter.
    Kumunita.Core.Notifications.INotificationEmitter? notifications = null) : IInvitationService
{
    /// <inheritdoc />
    public async Task<(string Token, Invitation Invitation)> CreateAsync(
        string email, string inviterSubjectId, DateTimeOffset expiresAt,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(inviterSubjectId);

        var now = DateTimeOffset.UtcNow;
        // ADR 0304 D2 — the token is 32 CSPRNG bytes base64url under a
        // kinv_ prefix (256 bits, the KioskLinkService `kskl_` shape; the
        // prefix distinguishes the lane). Shown once, discarded after
        // hashing.
        var token = NewToken();
        var invitation = new Invitation
        {
            Id               = Guid.NewGuid().ToString("N"),
            Email            = email.Trim().ToLowerInvariant(),
            InviterSubjectId = inviterSubjectId,
            TokenHash        = IdentityService.Sha256Hex(token),
            CreatedAt        = now,
            ExpiresAt        = expiresAt,
            Status           = EmailInvitationStatus.Pending,
            // AcceptedAt / AcceptedSubjectId stay null until AcceptAsync.
        };

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(invitation);
        // Exactly one AccessAudit row (the KioskLinkService kiosk.link.create
        // one-row shape, TargetId = the invitation's id — the
        // EscalationTokenService token-row precedent).
        session.Store(AuditRow(now, inviterSubjectId, "invitation.create", invitation.Id));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return (token, invitation);
    }

    /// <inheritdoc />
    public async Task<bool> RevokeAsync(
        string invitationId, string inviterSubjectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invitationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(inviterSubjectId);

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var stored = await session.LoadAsync<Invitation>(invitationId, ct).ConfigureAwait(false);

        // ADR 0304 D3 — inviter-scoped + idempotent: a missing invitation,
        // an already-revoked/accepted one, or one owned by someone else is a
        // no-op (no audit row, no state change — the EscalationTokenService
        // RevokeAsync no-op shape, the ADR 0302 D3 precedent).
        if (stored is null
            || stored.Status is not EmailInvitationStatus.Pending
            || stored.InviterSubjectId != inviterSubjectId)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        stored.Status = EmailInvitationStatus.Revoked;
        session.Store(stored);
        session.Store(AuditRow(now, inviterSubjectId, "invitation.revoke", invitationId));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<Invitation?> ValidateAsync(
        string tokenHash, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(tokenHash))
        {
            return null; // a blank hash cannot validate (the ESC·5 shape)
        }

        // The hash arrives precomputed (the IdentityService.FindTokenAsync
        // local-constant pattern — the digest is a query constant, never
        // inside the .Where expression). Owner scope is not part of the
        // match: a valid hash for ANOTHER inviter's invitation is still a
        // valid token (the ADR 0304 D4 single-use pin is on the
        // invitation's status, not on the inviter's scope).
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var match = await session.Query<Invitation>()
            .Where(i => i.TokenHash == tokenHash
                && i.Status == EmailInvitationStatus.Pending)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (match is null)
        {
            return null; // absent, accepted, or revoked — no audit row
        }

        // The expiry check is a post-load comparison (the ADR 0304 D1
        // virtual-Expired state — the ExpiresAt field is the check, not
        // the enum value). A past-expiry Pending row is refused (the
        // virtual Expired case).
        if (match.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return null; // past expiry — refused (the virtual Expired state)
        }

        return match;
    }

    /// <inheritdoc />
    public async Task<ThinPrincipal> AcceptAsync(
        string tokenHash, string email, string displayName, string password,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        // 1. Validate the token (the same seam as ValidateAsync — the
        //    single-source-of-truth for the refusal cases).
        var invitation = await ValidateAsync(tokenHash).ConfigureAwait(false);
        if (invitation is null)
        {
            throw new InvalidOperationException(
                "The invitation token is invalid, expired, or already used.");
        }

        // 2. The signing-up email must match the invitation's email (the
        //    ADR 0304 D4 single-address pin — the token is only good for
        //    the invited address).
        if (invitation.Email != email.Trim().ToLowerInvariant())
        {
            throw new InvalidOperationException(
                "The signing-up email does not match the invited address.");
        }

        // 3. Run the existing IIdentityService.RegisterAsync lane first
        //    (the ADR 0142 D6 cross-store ordering precedent — the
        //    account-creation is the primary domain op; the invitation
        //    mark is the derivative, committed after). A RegisterAsync
        //    failure (e.g. email already exists) propagates — the
        //    invitation row is NOT marked Accepted in that case (the
        //    recoverable state: the inviter can revoke and re-issue).
        var principal = await identity.RegisterAsync(
            displayName, email, password).ConfigureAwait(false);

        // 4. Mark the invitation Accepted + one audit row (the derivative
        //    commit, in a fresh session — the ADR 0142 D6 ordering).
        var now = DateTimeOffset.UtcNow;
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var stored = await session.LoadAsync<Invitation>(invitation.Id, ct).ConfigureAwait(false);
        if (stored is null || stored.Status is not EmailInvitationStatus.Pending)
        {
            // Race: another AcceptAsync won between step 1 and step 4.
            // The account was created (step 3); the invitation mark is
            // already done by the winner. This is the rare cross-session
            // window the ADR 0142 D6 accepts (the domain write succeeded;
            // the derivative state is consistent — the account exists).
            return principal;
        }
        stored.Status        = EmailInvitationStatus.Accepted;
        stored.AcceptedAt    = now;
        stored.AcceptedSubjectId = principal.SubjectId;
        session.Store(stored);
        session.Store(AuditRow(now, invitation.InviterSubjectId, "invitation.accept", invitation.Id));

        // IMPROVE-UX A10 (ADR 0304) — the inviter hears back: a best-effort
        // notification to the inviter that their invitee accepted and an
        // account was created. Staged into the SAME committed session
        // (the IdentityService.EmitAccountNotificationAsync D5 posture —
        // the notify row is part of this write). **Not** a re-emit of
        // account.signup (that already fires on IIdentityService.RegisterAsync,
        // step 3 — re-emitting it here would double-notify the GlobalAdmin
        // set). Best-effort: a null emitter or an emit error is swallowed —
        // the account creation (step 3) + the invitation.accept audit row
        // (above) are the durable record; the inbox row is the nudge.
        // // TODO ❓ linkPath: /admin/invitations (A09's surface) for now;
        // // the resident self-invite surface when A11/A12 ship. Flag for
        // // SEC review: an inviter who is also a GlobalAdmin gets two
        // // distinct rows (invitation.accepted + account.signup) — two
        // // distinct kinds, two distinct meanings; I believe that is
        // // acceptable, but confirm in the SEC lane.
        if (notifications is not null)
        {
            try
            {
                await notifications.EmitAsync(
                    session,
                    recipientId: invitation.InviterSubjectId,
                    kind: NotificationKinds.InvitationAccepted,
                    idempotencyKey: $"notification:{NotificationKinds.InvitationAccepted}:{invitation.Id}",
                    body: displayName,
                    linkPath: "/admin/invitations",
                    ct: ct).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort (D5): the domain write + audit row are durable;
                // a notify-lane failure must not fail the accept.
            }
        }

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return principal;
    }

    /// <inheritdoc />
    public Task<Invitation?> ValidatePresentedAsync(
        string presentedToken, CancellationToken ct = default)
    {
        // A blank presented token cannot match any stored hash (the ESC·5
        // guard — and it keeps the blank case from opening a session).
        if (string.IsNullOrWhiteSpace(presentedToken))
        {
            return Task.FromResult<Invitation?>(null);
        }

        // Trap A (ADR 0304 D3, IMPROVE-UX A08) — the hash lives in ONE place:
        // the shared internal hasher (the IdentityService.Sha256Hex
        // implementation — the sibling in KioskLinkService hashes identically,
        // pinned by the A08 "the presented-token overload and the hash-based
        // seam agree" test). The Web host cannot call it (internal, no
        // InternalsVisibleTo), so the hashing is in this assembly; the
        // presented token never enters a .Where(...) expression (the
        // FindTokenAsync precomputed-constant pattern).
        var tokenHash = IdentityService.Sha256Hex(presentedToken);
        return ValidateAsync(tokenHash, ct);
    }

    /// <inheritdoc />
    public Task<ThinPrincipal> AcceptPresentedAsync(
        string presentedToken, string email, string displayName, string password,
        CancellationToken ct = default)
    {
        // A blank presented token cannot match any stored hash (the ESC·5
        // guard — refuse before hashing, the ValidatePresentedAsync shape).
        if (string.IsNullOrWhiteSpace(presentedToken))
        {
            throw new InvalidOperationException(
                "The invitation token is invalid, expired, or already used.");
        }

        // Trap A (ADR 0304 D3, IMPROVE-UX A08) — the hash lives in ONE place
        // (the IdentityService.Sha256Hex shared hasher — the Web host cannot
        // call it, internal, no InternalsVisibleTo); the presented token never
        // enters a .Where(...) expression. Delegate to the hash-based seam so
        // the contract (email match, RegisterAsync, the Accepted mark, the one
        // invitation.accept audit row, the ThinPrincipal return) is shared
        // verbatim — the two paths must not diverge (the one-fact invariant).
        var tokenHash = IdentityService.Sha256Hex(presentedToken);
        return AcceptAsync(tokenHash, email, displayName, password, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InvitationListItem>> ListAsync(
        string inviterSubjectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inviterSubjectId);

        // A read lane (the M35·5 / 0159 "the read is a config load, not an
        // access decision" floor) — zero audit rows (the ValidateAsync
        // precedent). Owner-scoped: only the inviter's rows (the list view's
        // access decision is the Web controller's [Authorize(Roles =
        // Roles.GlobalAdmin)] boundary's job — the ADR 0050 split; the
        // service does not re-check User). The token hash is NOT carried
        // (the list never needs the credential — the ADR 0304 D2
        // hash-at-rest floor).
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var rows = await session.Query<Invitation>()
            .Where(i => i.InviterSubjectId == inviterSubjectId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        return rows
            .Select(i => new InvitationListItem(
                InvitationId: i.Id,
                Email: i.Email,
                InviterSubjectId: i.InviterSubjectId,
                CreatedAt: i.CreatedAt,
                ExpiresAt: i.ExpiresAt,
                Status: i.Status,
                AcceptedAt: i.AcceptedAt,
                AcceptedSubjectId: i.AcceptedSubjectId,
                // The virtual-Expired state (ADR 0304 D1/D6): a Pending row
                // past ExpiresAt reads Expired in the list — computed here,
                // never written (the ValidateAsync post-load comparison
                // precedent).
                DisplayStatus: i.Status == EmailInvitationStatus.Pending
                    && i.ExpiresAt <= now
                    ? EmailInvitationStatus.Expired
                    : i.Status))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<InvitationInviteResult> InviteAsync(
        string email, string inviterSubjectId, string inviterDisplayName,
        DateTimeOffset expiresAt, string? inviteLinkOverride = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(inviterSubjectId);
        if (mailer is null)
        {
            // A09 contract: InviteAsync is the delivery seam — it needs the
            // mailer stage. A null mailer is a test shape that did not
            // supply it (the A07/A08 CreateAsync-only harness) — refuse
            // loudly rather than create an invitation that is never
            // delivered (the C3 no-silent-missing-email floor).
            throw new InvalidOperationException(
                "The invitation delivery seam requires a mailer stage (IMailerStage).");
        }

        var now = DateTimeOffset.UtcNow;
        // ADR 0304 D2 — the token is 32 CSPRNG bytes base64url under a
        // kinv_ prefix (the CreateAsync shape, shared); shown once (the
        // result carries it), then unrecoverable (the store holds only the
        // hash).
        var token = NewToken();
        var invitation = new Invitation
        {
            Id               = Guid.NewGuid().ToString("N"),
            Email            = email.Trim().ToLowerInvariant(),
            InviterSubjectId = inviterSubjectId,
            TokenHash        = IdentityService.Sha256Hex(token),
            CreatedAt        = now,
            ExpiresAt        = expiresAt,
            Status           = EmailInvitationStatus.Pending,
        };

        // The ?invite= link the A08 receiving gate resolves (the
        // VerificationLink shape: BaseUrl prefix + the same-origin relative
        // path fallback when unset). inviteLinkOverride (a test shape)
        // replaces the assembly.
        var inviteLink = inviteLinkOverride
            ?? ((baseUrlReader?.BaseUrl is string root and not ""
                    ? root.TrimEnd('/')
                    : string.Empty)
                + $"/account/signup?invite={token}");

        // The email (the BuildVerificationEmailAsync template shape —
        // translation keys + a raw-English fallback floor; the body template's
        // {0} = the inviter's name, {1} = the invite link, {2} = the community
        // name, {3} = the expiry date — the A09 prompt: "the body must name the
        // inviter's community and the expiry so the resident knows the window").
        var community = communityOptions?.Value.Name ?? "Kumunita";
        var (subject, body) = await BuildInvitationEmailAsync(
            string.IsNullOrWhiteSpace(inviterDisplayName) ? "there" : inviterDisplayName,
            inviteLink, community, invitation.ExpiresAt);

        // ONE session, ONE commit: the domain write (the Invitation row) +
        // the exactly-one invitation.create AccessAudit row + the outbox
        // row + the durable envelope — atomically (the IMailerStage C3
        // guarantee: a failed commit leaves no queued email behind).
        // Idempotency key invite:{invitationId} (the §6.2 per-email key
        // scheme, the A09 prompt).
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(invitation);
        session.Store(AuditRow(now, inviterSubjectId, "invitation.create", invitation.Id));
        await mailer.StageAsync(
            session,
            idempotencyKey: $"invite:{invitation.Id}",
            recipient: invitation.Email,
            subject: subject,
            body: body,
            ct: ct).ConfigureAwait(false);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return new InvitationInviteResult(token, invitation);
    }

    // ── private helpers ────────────────────────────────────────────────────

    /// <summary>
    /// The invitation email's subject + body (IMPROVE-UX A09) — the
    /// <see cref="IdentityService.BuildVerificationEmailAsync"/> template
    /// shape: the <c>email.invitation_subject</c> / <c>email.invitation_body</c>
    /// registry entries through the <see cref
    /// "Kumunita.Core.Localization.ITranslationProvider"/> (the
    /// preferred-language → en floor), with the raw-English literals as the
    /// fallback for the two test sites that construct <see
    /// cref="InvitationService"/> directly without a provider. The body
    /// template's <c>{0}</c>/<c>{1}</c>/<c>{2}</c>/<c>{3}</c> placeholders
    /// are the inviter's display name, the <c>?invite=</c> link, the
    /// community name, and the expiry date (the <c>email.invitation_body</c>
    /// registry entry). The body names the inviter's community + the expiry
    /// window (the A09 prompt: "the body must name the inviter's community
    /// and the expiry so the resident knows the window").
    /// </summary>
    private async Task<(string Subject, string Body)> BuildInvitationEmailAsync(
        string inviterDisplayName, string inviteLink, string community, DateTimeOffset expiresAt)
    {
        // The subject + body templates resolve the {0}/{1}/{2}/{3} placeholders
        // as the inviter's name, the invite link, the community name, and the
        // expiry date (the email.invitation_subject / email.invitation_body
        // registry entries — the four-language C02 parity set). The expiry is
        // pre-formatted (a readable date) so the template carries {3}, not a
        // raw DateTimeOffset.
        var name = inviterDisplayName;
        var expiryText = expiresAt.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        object[] args = { name, inviteLink, community, expiryText };

        string bodyTemplate = "Hi {0},\n\n{0} has invited you to join {2} on Kumunita. Open the one-time link below to create your account — the invitation stays valid until {3}:\n\n{1}\n\nIf you weren\u2019t expecting this, you can ignore this message: it only opens sign-up for your address and then expires.";
        string subjectTemplate = "You're invited to {2}";

        if (translationProvider is not null)
        {
            // The invitation lane has no per-recipient preferred language at
            // creation (the inviter may not have one on file) — the provider
            // resolves the instance default → the en floor.
            var resolvedSubject = await translationProvider.GetAsync(
                "email.invitation_subject", (string?)null).ConfigureAwait(false);
            var resolvedBody = await translationProvider.GetAsync(
                "email.invitation_body", (string?)null).ConfigureAwait(false);
            // A missing entry (a provider without the key) would surface the raw
            // key — fall back to the English literal (the BuildVerificationEmailAsync
            // floor).
            if (!string.IsNullOrWhiteSpace(resolvedSubject) && resolvedSubject != "email.invitation_subject")
                subjectTemplate = resolvedSubject;
            if (!string.IsNullOrWhiteSpace(resolvedBody) && resolvedBody != "email.invitation_body")
                bodyTemplate = resolvedBody;
        }

        string subject = string.Format(subjectTemplate, args);
        string body = string.Format(bodyTemplate, args);
        return (subject, body);
    }

    /// <summary>The one <c>AccessAudit</c> row shape the invitation lane
    /// writes (the <see cref="KioskLinkService"/> <c>kiosk.link.create</c>
    /// field mapping — the KioskLink "the read is not an access decision"
    /// floor applies to ValidateAsync; the three write lanes each emit
    /// exactly one row).</summary>
    private static AccessAudit AuditRow(
        DateTimeOffset at, string inviterSubjectId, string action, string invitationId) => new()
    {
        Id                   = Guid.NewGuid().ToString("N"),
        At                   = at,
        ActorId              = inviterSubjectId,
        EffectivePrincipalId = inviterSubjectId,
        Action               = action,
        TargetKind           = "invitation",
        TargetId             = invitationId,
        Via                  = AccessVia.Owner,
        Outcome              = AccessOutcome.Allow
    };

    /// <summary>The token format (ADR 0304 D2): <c>kinv_</c> + 32 CSPRNG
    /// bytes base64url (256 bits — the KioskLinkService.NewToken shape,
    /// the prefix distinguishing the invitation lane from the kiosk link
    /// lane).</summary>
    private static string NewToken()
    {
        var bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return "kinv_" + Convert.ToBase64String(bytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
