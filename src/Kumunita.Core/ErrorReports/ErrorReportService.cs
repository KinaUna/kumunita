using Kumunita.Core.Authorization;
using Kumunita.Core.Notifications;
using Marten;

namespace Kumunita.Core.ErrorReports;

/// <summary>
/// The read + two audited-write lanes for the ErrorReport doc (ADR 0154).
/// Composes the host-registered Marten IDocumentStore (the
/// AdminOnboardingService / SiteContentService shape) — reads open their
/// own QuerySession; each audited write opens one write IDocumentSession
/// and commits the doc + its single AccessAudit row together (C3).
/// </summary>
public sealed class ErrorReportService : IErrorReportService
{
    private readonly IDocumentStore _store;

    /// <summary>
    /// The escalation-authorization seam (ADR 0159, U04) — present on an
    /// instance with the ESC lane wired (the DI line), <c>null</c> on a
    /// test-construction site that builds the service with only the store
    /// (the M31 localization / translationProvider floor precedent). When
    /// <c>null</c>, <see cref="AcceptInboundAsync"/> short-circuits to
    /// <c>Error: "token service absent"</c> so those sites keep compiling.
    /// </summary>
    private readonly IEscalationTokenService? _tokenService;

    /// <summary>
    /// IMPROVE-UX R08 (ADR 0300) — the notification-emitter seam (the
    /// C03 <see cref="INotificationEmitter"/>): present on a production
    /// wiring (the DI line, the <c>ModerationService</c> /
    /// <c>EventReminderService</c> idiom), <c>null</c> on a test-construction
    /// site that builds the service with only the store (the
    /// <c>tokenService</c> optional-seam precedent). When <c>null</c>,
    /// <see cref="MarkResolvedAsync"/> simply skips the emission (the
    /// pre-R08 behaviour) so those sites keep compiling + passing.
    /// </summary>
    private readonly INotificationEmitter? _notifications;

    /// <summary>
    /// IMPROVE-UX R08 (ADR 0300) — the profile-read seam
    /// (<see cref="UserInfo.IUserInfoService"/>) the R08 body's recipient
    /// language comes from (the <c>ModerationService</c> R07 _userInfo
    /// precedent). Optional: <c>null</c> on a test harness that omits it —
    /// the body then degrades to the <c>en</c> floor (the ADR 0005
    /// provider guarantee), never throws.
    /// </summary>
    private readonly UserInfo.IUserInfoService? _userInfo;

    /// <summary>
    /// IMPROVE-UX R08 (ADR 0300) — the translation seam
    /// (<see cref="Localization.ITranslationProvider"/>) the R08 body's
    /// localized sentence comes from (the <c>ModerationService</c> R07
    /// <c>_translationProvider</c> precedent). Optional: <c>null</c> on a
    /// test harness that omits it — the body degrades to the plain
    /// <c>en</c> sentence (the <c>KnownTranslationKeys.EnValues</c> floor),
    /// never throws.
    /// </summary>
    private readonly Localization.ITranslationProvider? _translationProvider;

    public ErrorReportService(
        IDocumentStore store,
        IEscalationTokenService? tokenService = null,
        INotificationEmitter? notifications = null,
        UserInfo.IUserInfoService? userInfo = null,
        Localization.ITranslationProvider? translationProvider = null)
    {
        _store             = store;
        _tokenService      = tokenService;
        _notifications     = notifications;
        _userInfo          = userInfo;
        _translationProvider = translationProvider;
    }

    /// <summary>Store one ErrorReport row (new) + exactly one AccessAudit row (one session).</summary>
    public async Task<ErrorReport> CreateAsync(ErrorReportDraft draft, CancellationToken ct = default)
    {
        var report = new ErrorReport
        {
            Id            = Guid.NewGuid().ToString("N"),
            SubjectId     = draft.SubjectId,
            Description   = draft.Description,
            ContactEmail  = draft.ContactEmail,
            RequestId     = draft.RequestId,
            ExceptionType = draft.ExceptionType,
            UserAgent     = draft.UserAgent,
            Created       = DateTimeOffset.UtcNow,
            TriageStatus  = "new",            // the M31 floor; TriagedAt/TriagedBy stay null
            // M32 additive projection (M32·4) — draft.Origin is "error-page"
            // (the M31 500 form default, the M32-3 FACE) or "general" (the
            // M32 /issues/new form). ResolvedAt/ResolvedBy/ResolutionNote
            // stay null until MarkResolvedAsync (M32·8).
            Origin        = draft.Origin
        };

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(report);
        // Exactly one AccessAudit row (the §2.1 Via pin) — the SiteContentService
        // / AdminOnboardingService field set. Via = Resident (non-blank
        // SubjectId) or Anonymous (blank SubjectId, the new additive value).
        session.Store(new AccessAudit
        {
            Id                   = Guid.NewGuid().ToString("N"),
            At                   = DateTimeOffset.UtcNow,
            ActorId              = draft.SubjectId,
            EffectivePrincipalId = draft.SubjectId,
            Action               = "errorreport.create",
            TargetKind           = "error-report",
            TargetId             = report.Id,
            Via                  = string.IsNullOrEmpty(draft.SubjectId)
                                       ? AccessVia.Anonymous   // §2.1 pin (new additive value)
                                       : AccessVia.Resident,    // §2.1 pin (ADR 0041)
            Outcome              = AccessOutcome.Allow
        });
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return report;
    }

    /// <summary>
    /// Stamp a new report triaged + exactly one AccessAudit row (one
    /// session). Returns null (a no-op — no audit row, no state change)
    /// when the report is missing or already triaged (M31·6).
    /// </summary>
    public async Task<ErrorReport?> MarkTriagedAsync(string reportId, string actorId, CancellationToken ct = default)
    {
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var stored = await session.LoadAsync<ErrorReport>(reportId, ct).ConfigureAwait(false);
        if (stored is null || stored.TriageStatus == "triaged")
        {
            return null; // no-op — a missing or already-triaged report (M31·6)
        }
        stored.TriageStatus = "triaged";
        stored.TriagedAt    = DateTimeOffset.UtcNow;
        stored.TriagedBy    = actorId;
        session.Store(stored);
        // Exactly one AccessAudit row (Via = Admin — the §2.1 pin).
        session.Store(new AccessAudit
        {
            Id                   = Guid.NewGuid().ToString("N"),
            At                   = DateTimeOffset.UtcNow,
            ActorId              = actorId,
            EffectivePrincipalId = actorId,
            Action               = "errorreport.triage",
            TargetKind           = "error-report",
            TargetId             = reportId,
            Via                  = AccessVia.Admin,
            Outcome              = AccessOutcome.Allow
        });
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return stored;
    }

    /// <summary>
    /// Stamp a new/triaged report resolved + exactly one AccessAudit row
    /// (one session). Returns null (a no-op — no audit row, no state
    /// change) when the report is missing or already resolved (M32·8
    /// idempotency pin, the M31·6 MarkTriagedAsync shape verbatim).
    /// </summary>
    public async Task<ErrorReport?> MarkResolvedAsync(string reportId, string actorId, string? resolutionNote, CancellationToken ct = default)
    {
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var stored = await session.LoadAsync<ErrorReport>(reportId, ct).ConfigureAwait(false);
        if (stored is null || stored.TriageStatus == "resolved")
        {
            return null; // no-op — a missing or already-resolved report (M32·8)
        }
        stored.TriageStatus   = "resolved";
        stored.ResolvedAt     = DateTimeOffset.UtcNow;
        stored.ResolvedBy     = actorId;
        stored.ResolutionNote = resolutionNote;
        session.Store(stored);
        // Exactly one AccessAudit row (Via = Admin — the M32·8 pin).
        session.Store(new AccessAudit
        {
            Id                   = Guid.NewGuid().ToString("N"),
            At                   = DateTimeOffset.UtcNow,
            ActorId              = actorId,
            EffectivePrincipalId = actorId,
            Action               = "errorreport.resolve",
            TargetKind           = "error-report",
            TargetId             = reportId,
            Via                  = AccessVia.Admin,
            Outcome              = AccessOutcome.Allow
        });
        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        // IMPROVE-UX R08 (ADR 0300) — the filer hears back (E5): the
        // previously-empty "resolved" loop closes. Only a **signed-in**
        // filer (a non-blank <see cref="ErrorReport.SubjectId"/>) gets an
        // inbox row — an **anonymous** filer has no recipient and gets
        // nothing. The <c>ContactEmail</c> is **never** mailed here
        // (SEC: "never mailed" by design — mailing it is a held SEC
        // decision, the // TODO ❓ on this unit). The emission is staged
        // into the **same** write session as the stamp + audit row (the
        // R07 <c>ModerationService.ReportIntake</c> session-owning
        // precedent — the <c>INotificationEmitter.EmitAsync</c> call takes
        // the caller's <c>session</c>; it is a no-op when the emitter seam
        // is absent or the filer is anonymous, so the pre-R08 behaviour is
        // the exact degrade path).
        // // TODO ❓ anonymous filers: mail the ContactEmail? (SEC: currently
        // // "never mailed" — a held decision, NOT made by this unit.)
        if (!string.IsNullOrWhiteSpace(stored.SubjectId) && _notifications is not null)
        {
            var body = await BuildIssueResolvedBodyAsync(stored).ConfigureAwait(false);
            await _notifications.EmitAsync(
                session,
                recipientId: stored.SubjectId,
                kind: NotificationKinds.IssueResolved,
                idempotencyKey: $"notification:issue.resolved:{stored.Id}",
                body: body,
                targetId: stored.Id,
                linkPath: $"/reports/mine#i-{stored.Id}",
                ct: ct).ConfigureAwait(false);
        }

        return stored;
    }

    /// <summary>
    /// IMPROVE-UX R08 (ADR 0300) — build the <c>issue.resolved</c>
    /// notification body: a localized "your issue was resolved" sentence
    /// (the <c>notifications.issue.resolved_body</c> key), with the
    /// moderator's <see cref="ErrorReport.ResolutionNote"/> appended
    /// **verbatim** when present (the free-text note is **never**
    /// translated). Localizes to the filer's
    /// <c>Profile.EmailLanguage</c> (the <see cref="UserInfo.IUserInfoService"/>
    /// read seam; the <c>ModerationService.BuildReportResolvedBodyAsync</c>
    /// / <c>EventReminderService.BuildLocalizedReminder</c> precedent).
    /// Degrades gracefully: no <see cref="Localization.ITranslationProvider"/>
    /// or no <see cref="UserInfo.IUserInfoService"/> → the plain <c>en</c>
    /// sentence (the <see cref="Localization.KnownTranslationKeys.EnValues"/>
    /// floor), never throws.
    /// </summary>
    private async Task<string> BuildIssueResolvedBodyAsync(ErrorReport stored)
    {
        const string key = "notifications.issue.resolved_body";
        string sentence;
        if (_translationProvider is null)
        {
            // No provider wired (a test harness that omits the seam): the
            // plain <c>en</c> floor the ADR 0005 registry guarantees.
            sentence = Localization.KnownTranslationKeys.EnValues.TryGetValue(key, out var en)
                ? en
                : "Your issue report has been resolved.";
        }
        else
        {
            string? lang = null;
            if (_userInfo is not null && !string.IsNullOrWhiteSpace(stored.SubjectId))
            {
                try
                {
                    var profile = await _userInfo.GetProfileAsync(stored.SubjectId).ConfigureAwait(false);
                    lang = profile?.EmailLanguage;
                }
                catch
                {
                    // The read seam is best-effort here — the emission
                    // itself must never be blocked by a profile lookup
                    // failure (the filer still gets the row).
                }
            }
            sentence = await _translationProvider.GetAsync(key, lang).ConfigureAwait(false);
        }

        // The moderator's free-text note: appended verbatim (never
        // translated), on a second sentence when present.
        var note = stored.ResolutionNote;
        return string.IsNullOrWhiteSpace(note)
            ? sentence
            : sentence + " " + note;
    }

    /// <summary>Newest-first listing (Created DESC); a read, never audited (M31·4).</summary>
    public async Task<IReadOnlyList<ErrorReport>> ListAsync(int maxCount = 100, CancellationToken ct = default)
    {
        using var session = _store.QuerySession();
        return await session.Query<ErrorReport>()
            .OrderByDescending(x => x.Created)   // the repo's pinned ordering shape (AnnouncementService/MessagingService/UserInfoService .OrderByDescending(.Created))
            .Take(maxCount)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The filer's issues (IMPROVE-UX R05 — the "My reports" D1 shape's second
    /// read seam): the <see cref="ErrorReport"/> rows where
    /// <see cref="ErrorReport.SubjectId"/> == the subject, newest-first
    /// (<c>Created</c> desc). A **read, not a decision** (the
    /// <see cref="ListAsync"/> / M31·4 precedent) — the filer is the subject,
    /// the fields are their own, so no <see cref="IAuthorizationService"/>
    /// call, no <c>AccessAudit</c> row, and no visibility gate (the subject
    /// is the owner). Returns an empty list when the filer has no issues.
    /// </summary>
    public async Task<IReadOnlyList<ErrorReport>> ListMineAsync(string subjectId, CancellationToken ct = default)
    {
        if (subjectId is null) throw new ArgumentNullException(nameof(subjectId));

        using var session = _store.QuerySession();
        return await session.Query<ErrorReport>()
            .Where(x => x.SubjectId == subjectId)
            .OrderByDescending(x => x.Created)   // the repo's pinned ordering shape (.OrderByDescending(.Created))
            .ToListAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Accept an inbound (escalated) report (ADR 0159, ESC·5 / ESC·7) — the
    /// M32 <c>MarkResolvedAsync</c> idempotent-write-lane shape (the ADR 0006
    /// C3 single-write-lane): re-validate the token → the idempotent lookup →
    /// the blank-description guard → store the received row + **one**
    /// <c>AccessAudit</c> row (<c>Via = Escalation</c>) in one write session.
    /// The M31 3 + the M32 1 methods are **unchanged** (ESC·1). Core stays
    /// HTTP-free (ESC·6 / ADR 0006-D): the Web inbound endpoint resolved the
    /// presented <c>Bearer</c> plaintext to <paramref name="draft"/>.
    /// <c>TokenId</c> / <c>FromInstance</c> before this call.
    /// </summary>
    public async Task<InboundResult> AcceptInboundAsync(InboundReport draft, CancellationToken ct = default)
    {
        if (_tokenService is null)
        {
            return new InboundResult(Created: false, Row: null!, Error: "token service absent");
        }

        // ESC·5 — confirm the token row still exists and is non-revoked before
        // creating a row (the Web endpoint resolved the Bearer plaintext to
        // draft.TokenId via ValidateAsync; a revoked / unknown token →
        // "invalid token", which the Web endpoint maps to a 401 — no row, no
        // audit row). ESC·7 — the idempotent lookup on (EscalationTokenId,
        // EscalationSourceId): a re-delivery of the same origin report under
        // the same token returns the existing row, not a duplicate.
        using (var session = _store.QuerySession())
        {
            var token = await session.LoadAsync<EscalationToken>(draft.TokenId, ct).ConfigureAwait(false);
            if (token is null || token.RevokedAt is not null)
            {
                return new InboundResult(Created: false, Row: null!, Error: "invalid token");
            }
            var existing = await session.Query<ErrorReport>()
                .Where(r => r.EscalationTokenId == draft.TokenId && r.EscalationSourceId == draft.SourceReportId)
                .FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (existing is not null)
            {
                return new InboundResult(Created: false, Row: existing, Error: null);
            }
        }

        // ESC·5 / M32·4 — the blank-description guard (no row, no audit row).
        if (string.IsNullOrWhiteSpace(draft.Description))
        {
            return new InboundResult(Created: false, Row: null!, Error: "description required");
        }

        // ESC·5 — store the received row + exactly one AccessAudit row in ONE
        // write session (the ADR 0006 C3 single-write-lane, the M32·8
        // precedent).
        var now = DateTimeOffset.UtcNow;
        var report = new ErrorReport
        {
            Id                   = Guid.NewGuid().ToString("N"),
            SubjectId            = draft.OriginSubjectId,
            Description          = draft.Description,
            ContactEmail         = draft.ContactEmail,
            RequestId            = draft.RequestId,
            ExceptionType        = draft.ExceptionType,
            // UserAgent stays null — the origin's payload doesn't carry a UA
            // (the M32 8-field forward shape); the triage / resolve fields
            // stay null until the local admin acts on the row.
            Created              = draft.OriginCreated,
            TriageStatus         = "new",              // the M31 default
            Origin               = "escalated",        // the ESC·2 additive value
            FromInstance         = draft.FromInstance, // the accepting token's Label (ESC·8 display)
            EscalationReceivedAt = now,
            EscalationSourceId   = draft.SourceReportId,
            EscalationTokenId    = draft.TokenId
        };

        await using var writeSession = _store.OpenSession(new Marten.Services.SessionOptions());
        writeSession.Store(report);
        // Exactly one AccessAudit row (Via = Escalation — the ESC·10 pin; the
        // M2M standing: the receiving admin is not the actor, the origin
        // platform is).
        writeSession.Store(new AccessAudit
        {
            Id                   = Guid.NewGuid().ToString("N"),
            At                   = now,
            ActorId              = draft.OriginSubjectId,
            EffectivePrincipalId = draft.OriginSubjectId,
            Action               = "errorreport.inbound",
            TargetKind           = "error-report",
            TargetId             = report.Id,
            Via                  = AccessVia.Escalation,
            Outcome              = AccessOutcome.Allow
        });
        await writeSession.SaveChangesAsync(ct).ConfigureAwait(false);
        return new InboundResult(Created: true, Row: report, Error: null);
    }
}
