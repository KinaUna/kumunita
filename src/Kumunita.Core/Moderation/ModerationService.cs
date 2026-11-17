using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Kumunita.Core.Notifications;
using Kumunita.Core.Posts;
using Kumunita.Core.Projects;
using Kumunita.Core.UserInfo;
using Marten;

namespace Kumunita.Core.Moderation;

/// <summary>
/// The report-workflow composition service (M3b U4; bounded context
/// <c>Kumunita.Core.Moderation</c>, ADR 0006-D lane). The M3b analog of
/// M2's <see cref="Kumunita.Core.UserInfo.DirectoryService"/> and M3's
/// <see cref="Kumunita.Core.Posts.PostService"/>. A pure caller of the
/// two frozen M1/M2 modules — <see cref="IUserInfoService"/> read seams
/// (including the flag-flip seam
/// <see cref="IProfileSettingsService.SetComponentModeratorAccessAsync"/>,
/// used by U5's F6 lane) and <see cref="IAuthorizationService"/> (the
/// single decision path) — plus its own <see cref="IDocumentStore"/>
/// for the write lanes.
/// <para>
/// M3b's **only** new authorization-surface addition (ADR 0006-E
/// compatible lane) is U5's <c>CanReadWithReportAsync</c>. U4's
/// <see cref="FileReportAsync"/> is a **resident-facing intake action**
/// (C-M3b·1, F1) — it is **not** an access decision and makes **no**
/// <see cref="IAuthorizationService"/> call; it composes only the
/// caller-provided <see cref="IDocumentStore"/> and writes two rows
/// (the <see cref="Report"/> domain row and the
/// <see cref="AccessAudit"/> row) into the **one** caller-owned
/// transaction (C3 / ADR 0006-C — one <c>SaveChangesAsync</c>). The
/// audit row carries the pinned tag <see cref="AccessVia.Admin"/>
/// (NOT <see cref="AccessVia.Report"/> — reserved for the read branch,
/// C-M3b·2; NOT <see cref="AccessVia.Owner"/> — the C1 owner-branch
/// negative). <see cref="Report.Status"/> is set to the exact literal
/// <c>"filed"</c> (the first of the four Status-literal pins, §2.3
/// item 2).
/// </para>
/// <para>
/// U5 appends <c>AssignReportAsync</c> (F5, C-M3b·4, GlobalAdmin-gated),
/// <c>UnlockAsync</c> / <c>ResolveReportAsync</c> (F6, C-M3b·4, C5
/// flag-flip), and <c>CanReadWithReportAsync</c> (F2, C-M3b·2, the
/// <c>Via = Report</c> read branch) to this same file — U4's
/// deliverable is the ctor + this one write lane only.
/// </para>
/// </summary>
public partial class ModerationService : IModerationService
{
    // C07 (ADR 0286) — SetComponentModeratorAccessAsync moved to
    // IProfileSettingsService; this service now holds only the sibling.
    private readonly IProfileSettingsService _profileSettings;
    private readonly IAuthorizationService _authz;
    private readonly IDocumentStore _store;

    // M6 (U04) — the notification emitter (frozen surface, U03). Optional so the
    // existing (pre-M6) positional call sites keep compiling (CS1736, the TG-lane
    // precedent); production wiring passes the DI-registered instance.
    private readonly INotificationEmitter? _notifications;

    // IMPROVE-CODE C09 — the profile read seam (the queue / detail's reporter,
    // reply-author, target-author and standing-moderator display names, moved
    // off the controller). Optional so the existing positional call sites keep
    // compiling (CS1736, the TG-lane precedent); production wiring passes the
    // DI-registered instance. The read seam's methods throw when it is absent
    // (the write lanes are unaffected — they never read profiles).
    private readonly IUserInfoService? _userInfo;

    // IMPROVE-UX R05 — the posts read seam (the "My reports" read's post-title
    // visibility gate: the post target's title is shown only while the
    // reporter can still read the post, resolved via PostService.GetPostAsync).
    // Optional so the existing positional test call sites (the Services store,
    // BootTargetStoreAsync) keep compiling (CS1736, the _userInfo precedent);
    // production wiring (the DI factory) passes the DI-registered instance.
    // The read seam throws when it is absent (the other read lanes never read
    // a post's visibility, so they are unaffected).
    private readonly Posts.PostService? _posts;

    // IMPROVE-UX R07 — the translation seam (the ReportResolved body's
    // localized outcome sentence; ADR 0005 the en-floor provider). Optional
    // so the existing positional test-construction call sites keep compiling
    // (CS1736, the _userInfo / _posts precedent); production wiring passes
    // the DI-registered instance. When null (a test harness that omits it)
    // the body degrades to the plain outcome word — never throws.
    private readonly ITranslationProvider? _translationProvider;

    // IMPROVE-UX A05 (ADR 0303) — the GlobalAdmin enumeration read seam (the
    // report.queued routing's GlobalAdmin fallback recipient set: when a
    // component has no standing moderator, the filed report still reaches the
    // GlobalAdmins). ADR 0006-E *compatible ADD* on the Identity module
    // (IIdentityService.ListGlobalAdminsAsync) — role enumeration is an
    // Identity concern, not a frozen profile/community seam, so
    // IUserInfoService is untouched. Optional (CS1736, the _userInfo / _posts
    // / _translationProvider precedent) so the existing positional test
    // call sites keep compiling; production wiring (the DI factory) passes
    // the DI-registered instance. When null (a test harness that omits it)
    // the GlobalAdmin half of the recipient set is empty — the standing-
    // moderator half still routes (the A05 "never ship only the standing
    // half silently" pin is enforced by a test, not by this degrade).
    private readonly Kumunita.Core.Identity.IIdentityService? _identity;

    public ModerationService(
        IProfileSettingsService profileSettings,
        IAuthorizationService authz,
        IDocumentStore store,
        INotificationEmitter? notifications = null,
        IUserInfoService? userInfo = null,
        Posts.PostService? posts = null,
        ITranslationProvider? translationProvider = null,
        // IMPROVE-UX A05 (ADR 0303) — the GlobalAdmin enumeration read seam
        // (the report.queued routing's fallback recipient set). Optional
        // (CS1736) — a test harness that omits it routes the standing
        // moderators only (the GlobalAdmin half is empty).
        Kumunita.Core.Identity.IIdentityService? identity = null)
    {
        _profileSettings = profileSettings ?? throw new ArgumentNullException(nameof(profileSettings));
        _authz    = authz    ?? throw new ArgumentNullException(nameof(authz));
        _store    = store    ?? throw new ArgumentNullException(nameof(store));
        _notifications = notifications;
        _userInfo = userInfo;
        _posts = posts;
        _translationProvider = translationProvider;
        _identity = identity;
    }

    private IUserInfoService UserInfo =>
        _userInfo ?? throw new InvalidOperationException(
            "The profile read seam (IUserInfoService) is not wired — the " +
            "ModerationService read seam (IMPROVE-CODE C09) requires it.");

    /// <summary>
    /// M6 (U04) — the emitter-body truncation helper (design doc §6.3
    /// "the report's reason, truncated"; ADR 0018 the sender-language
    /// pin). Caps the stored snippet at 200 chars + a trailing ellipsis
    /// so an inbox row never carries a full report body inline.
    /// </summary>
    private static string? Truncate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        var trimmed = text.Trim();
        return trimmed.Length <= 200 ? trimmed : trimmed[..200] + "…";
    }

    /// <summary>
    /// IMPROVE-UX R03 — the <see cref="Report.ResolutionNote"/>
    /// normalization (the D1 ≤500-char pin, lane-enforced so the Web
    /// form cannot bypass it): trim the note, normalize
    /// whitespace-only to <c>null</c> (the "no note" shape), and
    /// reject a note over 500 chars with an
    /// <see cref="ArgumentException"/> (the caller-contract violation
    /// — the R04 form's own maxlength validation is the first line,
    /// this the lane-enforces-shape second). Unlike
    /// <see cref="Truncate"/> (which caps at 200 + ellipsis, the
    /// emitter-snippet shape), this **throws** on over-cap input — the
    /// resolution note is data the reporter sees verbatim on the
    /// "My reports" page, not an inbox snippet; truncating would
    /// silently lose the moderator's note.
    /// </summary>
    private static string? ResolveReportNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note)) return null;
        var trimmed = note.Trim();
        if (trimmed.Length > 500)
            throw new ArgumentException(
                $"The resolution note is {trimmed.Length} chars; the R03 cap is 500.",
                nameof(note));
        return trimmed;
    }

    /// <summary>
    /// IMPROVE-UX R07 — build the <c>ReportResolved</c> notification body
    /// (the "the report-resolved notification is the reporter's own reason"
    /// E4 fix): a localized outcome sentence (the <see cref="ReportOutcome"/>
    /// closed four — the four <c>reports.body.*</c> keys; the pre-R03
    /// <c>Outcome == null</c> shape falls back to the plain "resolved"
    /// sentence, the <c>reports.body.resolved</c> key), with the
    /// moderator's <see cref="Report.ResolutionNote"/> appended verbatim
    /// when present (the free-text note is **never** translated).
    /// Localizes to the *reporter's* <c>Profile.EmailLanguage</c> (the
    /// <see cref="IUserInfoService"/> read seam; the
    /// <c>EventReminderService.BuildLocalizedReminder</c> precedent).
    /// Degrades gracefully: no <c>ITranslationProvider</c> or no
    /// <c>IUserInfoService</c> → plain outcome word (or "Resolved"), never throws.
    /// </summary>
    private async Task<string> BuildReportResolvedBodyAsync(Report report)
    {
        // The closed-four outcome key (the R03 D1 vocabulary — the four
        // `ReportOutcome` values each map to exactly one reports.body.*
        // key); the pre-R03 null shape maps to reports.body.resolved.
        var outcomeKey = report.Outcome switch
        {
            ReportOutcome.NoAction  => "reports.body_noaction",
            ReportOutcome.Hidden    => "reports.body_hidden",
            ReportOutcome.Removed   => "reports.body_removed",
            ReportOutcome.Other     => "reports.body_other",
            _                       => "reports.body_resolved"
        };

        string outcomeWord;
        if (_translationProvider is null)
        {
            // No provider wired (a test harness that omits the seam):
            // the plain English fallback — the outcome word the R06
            // `reports.outcome_*` labels already carry (the en floor).
            outcomeWord = KnownTranslationKeys.EnValues.TryGetValue(outcomeKey, out var en)
                ? en
                : (report.Outcome?.ToString() ?? "Resolved");
        }
        else
        {
            string? lang = null;
            if (_userInfo is not null && !string.IsNullOrWhiteSpace(report.ReporterId))
            {
                try
                {
                    var profile = await _userInfo.GetProfileAsync(report.ReporterId).ConfigureAwait(false);
                    lang = profile?.EmailLanguage;
                }
                catch
                {
                    // The read seam is best-effort here — the emission
                    // itself must never be blocked by a profile lookup
                    // failure (the reporter still gets the row).
                }
            }
            outcomeWord = await _translationProvider.GetAsync(outcomeKey, lang).ConfigureAwait(false);
        }

        // The moderator's free-text note: appended verbatim (never
        // translated), on a second sentence when present.
        var note = report.ResolutionNote;
        return string.IsNullOrWhiteSpace(note)
            ? outcomeWord
            : outcomeWord + " " + note;
    }

    /// <summary>
    /// ADR 0173 — the report-targets lane's shared helper: resolve the
    /// report's target (post OR one of the new surfaces) to the single
    /// <see cref="IAuditableResource"/> the SoD <c>Moderate</c>
    /// decision (F5 / F6) runs against. The original M3b shape
    /// (a <see cref="Post"/>) uses <c>PostToAuditableResource</c>;
    /// the new surfaces use their own ToAuditableResource adapters
    /// (the ADR 0006-E shape — the module reads only its own
    /// <c>OwnerId</c> / <c>Audience</c> / <c>ComponentId</c>, never
    /// module-specific fields).
    /// <para>
    /// <b>Fail-closed:</b> a missing target is a failed call (no
    /// status flip, no audit row — no partial write; the same
    /// invariant the original <c>Post</c> load enforced).
    /// </para>
    /// </summary>
    private async Task<IAuditableResource> LoadReportTargetResourceAsync(
        Report report, IDocumentSession session)
    {
        if (report.TargetKind is not null && report.TargetId is not null)
        {
            switch (report.TargetKind)
            {
                case "event":
                {
                    var ev = await session.LoadAsync<Kumunita.Core.Events.Event>(report.TargetId).ConfigureAwait(false);
                    if (ev is null)
                        throw new KeyNotFoundException($"Event '{report.TargetId}' (referenced by report '{report.Id}') was not found in the session.");
                    return new Kumunita.Core.Events.EventToAuditableResource(ev);
                }
                case "project":
                {
                    var pr = await session.LoadAsync<Kumunita.Core.Projects.Project>(report.TargetId).ConfigureAwait(false);
                    if (pr is null)
                        throw new KeyNotFoundException($"Project '{report.TargetId}' (referenced by report '{report.Id}') was not found in the session.");
                    return new Kumunita.Core.Projects.ProjectToAuditableResource(pr);
                }
                case "board":
                {
                    var bd = await session.LoadAsync<Kumunita.Core.Projects.KanbanBoard>(report.TargetId).ConfigureAwait(false);
                    if (bd is null)
                        throw new KeyNotFoundException($"Board '{report.TargetId}' (referenced by report '{report.Id}') was not found in the session.");
                    return new Kumunita.Core.Projects.KanbanBoardToAuditableResource(bd);
                }
                case "todo":
                {
                    var td = await session.LoadAsync<Kumunita.Core.Projects.TodoItem>(report.TargetId).ConfigureAwait(false);
                    if (td is null)
                        throw new KeyNotFoundException($"To-do '{report.TargetId}' (referenced by report '{report.Id}') was not found in the session.");
                    return new Kumunita.Core.Projects.TodoItemToAuditableResource(td);
                }
                case "grouppost":
                {
                    var gp = await session.LoadAsync<Post>(report.TargetId).ConfigureAwait(false);
                    if (gp is null)
                        throw new KeyNotFoundException($"Group post '{report.TargetId}' (referenced by report '{report.Id}') was not found in the session.");
                    return new PostToAuditableResource(gp);
                }
            }
        }

        // The original M3b / ADR 0023 shape: a post (possibly with a
        // reply discriminator on Report.ReplyId — the post is still
        // the auditable resource, the C-M3·1 reply-inherits rule).
        var post = await session.LoadAsync<Post>(report.PostId).ConfigureAwait(false);
        if (post is null)
            throw new KeyNotFoundException($"Post '{report.PostId}' (referenced by report '{report.Id}') was not found in the session.");
        return new PostToAuditableResource(post);
    }


    // ── Committed write overloads (IMPROVE-CODE C09 — the controller's
    //    write lanes moved off the controller's own session) ───────────

    /// <summary>
    /// The committed <c>Assign</c> lane (IMPROVE-CODE C09 — the
    /// <see cref="Kumunita.Web.Controllers.ModerationController"/>'s
    /// <c>Assign</c> write lane moved behind this service, ADR 0006 §D
    /// "route + authz + shape"). Delegates to the frozen
    /// <see cref="AssignReportAsync"/> in the service's own session
    /// (the SoD <c>CanAsync(Moderate)</c> gate inside that method is
    /// unchanged); the web-side validation reads (the report's
    /// <c>ComponentId</c> + the standing-moderator <see
    /// cref="ModeratorAssignment"/> check) stay in the controller over
    /// the <see cref="GetDetailAsync"/> /
    /// <see cref="IsStandingModeratorOnComponentAsync"/> read seams
    /// (route + authz + shape — ADR 0006 §D). The controller's
    /// <c>TempData</c> / <c>Redirect</c> / <c>NotFound</c> shape is
    /// unchanged (the served HTML is unchanged; only the Marten reads
    /// moved).
    /// </summary>
    public async Task AssignReportAsync(
        string reportId,
        string assignedToModeratorId,
        string globalAdminId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(reportId))              throw new ArgumentException("A report id is required.",             nameof(reportId));
        if (string.IsNullOrEmpty(assignedToModeratorId)) throw new ArgumentException("An assigned moderator id is required.", nameof(assignedToModeratorId));
        if (string.IsNullOrEmpty(globalAdminId))         throw new ArgumentException("A GlobalAdmin actor id is required.",   nameof(globalAdminId));

        await using var session = _store.LightweightSession();
        await AssignReportAsync(
            reportId, assignedToModeratorId, globalAdminId, session)
            .ConfigureAwait(false);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// The committed <c>Unlock</c> lane (IMPROVE-CODE C09 — the
    /// <see cref="Kumunita.Web.Controllers.ModerationController"/>'s
    /// <c>Unlock</c> moved behind this service, ADR 0006 §D). Delegates
    /// to the frozen <see cref="UnlockAsync"/> in the service's own
    /// session (the C5 <c>SetComponentModeratorAccessAsync</c> flag-flip
    /// inside that method is unchanged); the controller's
    /// <c>TempData</c> / <c>Redirect</c> / <c>NotFound</c> shape is
    /// unchanged.
    /// </summary>
    public async Task UnlockAsync(
        string reportId,
        string globalAdminId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(reportId))      throw new ArgumentException("A report id is required.",         nameof(reportId));
        if (string.IsNullOrEmpty(globalAdminId)) throw new ArgumentException("A GlobalAdmin actor id is required.", nameof(globalAdminId));

        await using var session = _store.LightweightSession();
        await UnlockAsync(reportId, globalAdminId, session).ConfigureAwait(false);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// The committed <c>Resolve</c> lane (IMPROVE-CODE C09 — the
    /// <see cref="Kumunita.Web.Controllers.ModerationController"/>'s
    /// <c>ResolvePost</c> moved behind this service, ADR 0006 §D).
    /// Delegates to the frozen <see cref="ResolveReportAsync"/> in the
    /// service's own session (the C5 <c>SetComponentModeratorAccessAsync</c>
    /// flag-flip inside that method is unchanged); the controller's
    /// <c>TempData</c> / <c>Redirect</c> / <c>NotFound</c> shape is
    /// unchanged.
    /// <para>
    /// IMPROVE-UX R03 — the optional <paramref name="outcome"/> /
    /// <paramref name="resolutionNote"/> pair (both <c>null</c> by
    /// default, so the pre-R03 two-parameter call shape keeps
    /// compiling unchanged). When supplied, the lane writes them onto
    /// the <see cref="Report"/> row in the same transaction as the
    /// lane's existing <c>AccessAudit</c> row — which is **unchanged**
    /// (the R03 pin).
    /// </para>
    /// </summary>
    public async Task ResolveReportAsync(
        string reportId,
        string globalAdminId,
        ReportOutcome? outcome = null,
        string? resolutionNote = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(reportId))      throw new ArgumentException("A report id is required.",         nameof(reportId));
        if (string.IsNullOrEmpty(globalAdminId)) throw new ArgumentException("A GlobalAdmin actor id is required.", nameof(globalAdminId));

        await using var session = _store.LightweightSession();
        await ResolveReportAsync(reportId, globalAdminId, session, outcome, resolutionNote).ConfigureAwait(false);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// The committed <c>Unhide</c> lane (IMPROVE-UX A03 — the reversible
    /// lever; the <see cref="ResolveReportAsync"/> precedent's
    /// committed-overload shape: opens its own
    /// <see cref="Marten.IDocumentSession"/> (the C3
    /// same-transaction lane), loads the <see cref="Report"/> row for
    /// the <c>PostId</c>, delegates to the Posts context's
    /// <c>PostService.UnhidePostAsync</c> (the
    /// <see cref="Kumunita.Core.Authorization.AccessAction.Moderate"/>
    /// -gated write lane — the <c>HidePostAsync</c> inverse), one
    /// <c>SaveChangesAsync</c>). The <see cref="KeyNotFoundException"/>
    /// the report read throws (an absent report) is the controller's 404
    /// pin (the non-leaky shape — the <c>ResolveReportAsync</c> precedent).
    /// </summary>
    public async Task UnhidePostAsync(
        string reportId,
        string actorId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(reportId)) throw new ArgumentException("A report id is required.", nameof(reportId));
        if (string.IsNullOrEmpty(actorId))  throw new ArgumentException("A moderating actor is required.", nameof(actorId));
        if (_posts is null)
            throw new InvalidOperationException(
                "The posts write seam (Posts.PostService) is not wired — the " +
                "ModerationService UnhidePostAsync seam (IMPROVE-UX A03) requires it.");

        await using var session = _store.LightweightSession();

        // The report read runs first: the report's KeyNotFoundException is
        // the controller's 404 pin (non-leaky, the ResolveReportAsync
        // precedent); the PostId is resolved here so the Posts seam does not
        // have to re-derive it (the C-M3b·4 "no fabricated component" shape
        // — the report is the source of the target's identity, not the
        // caller).
        var report = await session.LoadAsync<Report>(reportId).ConfigureAwait(false);
        if (report is null)
            throw new KeyNotFoundException($"Report '{reportId}' was not found.");

        // Delegate to the Posts context's UnhidePostAsync (the
        // HidePostAsync inverse — the Moderate-gated write lane, the same
        // C3 same-transaction shape as HidePostAsync / RemovePostAsync).
        // PostService.UnhidePostAsync commits in this session (the C3
        // same-transaction lane, ADR 0006-E) — the audit row + the
        // post.Status write commit atomically. The session is disposed
        // by the await using (a second SaveChangesAsync here would be a
        // no-op at best, a second round-trip at worst).
        await _posts.UnhidePostAsync(report.PostId, actorId, session).ConfigureAwait(false);
    }

    /// <summary>
    /// The standing-moderator read for the <c>Assign</c> web-side
    /// defense-in-depth (IMPROVE-CODE C09 — the controller's previous
    /// <c>AnyAsync</c> read moved behind this service, ADR 0006 §D): a
    /// <c>true</c> when <paramref name="moderatorId"/> holds a
    /// <see cref="ModeratorAssignment"/> row on <paramref
    /// name="componentId"/>, a <c>false</c> otherwise (the controller's
    /// previous <c>flash_not_moderator</c> redirect shape — the form
    /// was out of sync, not a 404).
    /// </summary>
    public async Task<bool> IsStandingModeratorOnComponentAsync(
        string componentId,
        string moderatorId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(componentId)) return false;
        if (string.IsNullOrEmpty(moderatorId)) return false;

        await using var session = _store.LightweightSession();
        return await session
            .Query<ModeratorAssignment>()
            .AnyAsync(a => a.ComponentId == componentId && a.UserId == moderatorId)
            .ConfigureAwait(false);
    }

    // ── Read seam (IMPROVE-CODE C09 — the controller's raw Marten reads) ──

    /// <inheritdoc />
    public async Task<ModerationQueueReadModel> GetQueueAsync(
        string actorId,
        bool isGlobalAdmin,
        bool isStanding,
        CancellationToken ct = default)
    {
        // SEC-READ: report-gated (ADR 0003) — the queue self-filters: a standing-moderator sees only reports on their assigned components with ModeratorAccess ON; a plain resident sees none; a GlobalAdmin sees all
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());

        // The raw queue — sorted At desc + Status desc (the plan's line-254
        // pin; the controller's previous <c>Index</c> shape, verbatim).
        var reports = await session
            .Query<Report>()
            .OrderByDescending(r => r.At)
            .ThenByDescending(r => r.Status)
            .ToListAsync()
            .ConfigureAwait(false);

        // The scoped-read filter (the controller's <c>Index</c> block,
        // verbatim): a GlobalAdmin sees every report; a standing-moderator
        // sees only the reports where (a) they hold a
        // <see cref="ModeratorAssignment"/> row on the report's
        // <c>ComponentId</c>, AND (b) that component's
        // <see cref="Kumunita.Core.UserInfo.Component.ModeratorAccess"/>
        // flag is ON; a plain resident sees no reports. A read lane, not a
        // decision lane (no <c>CanAsync</c> call, no <c>AccessAudit</c> row).
        if (!isGlobalAdmin && isStanding)
        {
            var myComponents = (await _profileSettings
                .GetAssignmentsAsync(actorId).ConfigureAwait(false))
                .Where(a => a.ComponentId is not null)
                .Select(a => a.ComponentId)
                .ToHashSet();
            var components = (await session.Query<Kumunita.Core.UserInfo.Component>()
                .ToListAsync().ConfigureAwait(false)).ToDictionary(c => c.Id);
            var scopedComponentIds = new HashSet<string>();
            foreach (var componentName in components.Keys)
            {
                if (!myComponents.Contains(componentName))
                    continue;
                if (components[componentName].ModeratorAccess == true)
                    scopedComponentIds.Add(componentName);
            }
            reports = reports.Where(r =>
                r.ComponentId is not null && scopedComponentIds.Contains(r.ComponentId))
                .ToList();
        }
        else if (!isGlobalAdmin)
        {
            // A plain resident (Member, no standing-moderator row) sees NO
            // reports (the C-M3b·4 SoD pin projects at the display level on
            // the read lanes).
            reports = [];
        }

        var componentNames = (await session.Query<Kumunita.Core.UserInfo.Component>()
            .ToListAsync().ConfigureAwait(false))
            .ToDictionary(c => c.Id, c => c.Name);

        var rows = new List<ModerationQueueRow>(reports.Count);
        var byStatus = new Dictionary<string, int>
        {
            ["filed"]    = 0,
            ["assigned"] = 0,
            ["unlocked"] = 0,
            ["resolved"] = 0,
        };

        foreach (var r in reports)
        {
            var reporter = await UserInfo.GetProfileAsync(r.ReporterId).ConfigureAwait(false);

            // ADR 0023 — the reply-report-target lane (the controller's
            // previous reply block, verbatim).
            string? replyAuthorName = null;
            if (r.ReplyId is not null)
            {
                var reply = await session.LoadAsync<PostReply>(r.ReplyId).ConfigureAwait(false);
                if (reply is not null)
                    replyAuthorName =
                        (await UserInfo.GetProfileAsync(reply.AuthorId).ConfigureAwait(false))?.DisplayName;
            }

            // ADR 0173 — the report-targets lane (the controller's previous
            // target block, verbatim).
            string? targetKind = r.TargetKind, targetId = r.TargetId;
            string? targetTitle = null, targetAuthorName = null;
            if (targetKind is not null && targetId is not null)
            {
                (string? title, string? authorId) = (null, null);
                switch (targetKind)
                {
                    case "event":
                        var ev = await session.LoadAsync<Kumunita.Core.Events.Event>(targetId).ConfigureAwait(false);
                        title = ev?.Title; authorId = ev?.AuthorId; break;
                    case "project":
                        var pr = await session.LoadAsync<Kumunita.Core.Projects.Project>(targetId).ConfigureAwait(false);
                        title = pr?.Title; authorId = pr?.AuthorId; break;
                    case "board":
                        var bd = await session.LoadAsync<KanbanBoard>(targetId).ConfigureAwait(false);
                        title = bd?.Title; authorId = bd?.AuthorId; break;
                    case "todo":
                        var td = await session.LoadAsync<TodoItem>(targetId).ConfigureAwait(false);
                        title = td?.Title; authorId = td?.AuthorId; break;
                    case "grouppost":
                        var gp = await session.LoadAsync<Post>(targetId).ConfigureAwait(false);
                        title = gp?.Title; authorId = gp?.AuthorId; break;
                }
                targetTitle = title;
                targetAuthorName = authorId is null
                    ? null
                    : (await UserInfo.GetProfileAsync(authorId).ConfigureAwait(false))?.DisplayName;
            }

            var statusKey = r.Status ?? "filed";   // "filed" as the M3-registered POCO default
            if (!byStatus.ContainsKey(statusKey))
                byStatus[statusKey] = 0;
            byStatus[statusKey] += 1;

            rows.Add(new ModerationQueueRow
            {
                Id          = r.Id,
                PostId      = r.PostId,
                PostTitle   = r.PostId.Length > 0 ? (await session.LoadAsync<Post>(r.PostId).ConfigureAwait(false))?.Title : null,
                ComponentId = r.ComponentId,
                ComponentName = r.ComponentId is not null ? componentNames.GetValueOrDefault(r.ComponentId) : null,
                ReporterName = reporter?.DisplayName,
                Reason      = r.Reason,
                Status      = statusKey,
                At          = r.At,
                ReplyId     = r.ReplyId,
                ReplyAuthorName = replyAuthorName,
                TargetKind  = targetKind,
                TargetId    = targetId,
                TargetTitle = targetTitle,
                TargetAuthorName = targetAuthorName,
            });
        }

        return new ModerationQueueReadModel
        {
            Rows     = rows,
            ByStatus = byStatus,
        };
    }

    /// <summary>
    /// The scoped-component-id set for <paramref name="actorId"/> when they
    /// are a standing-moderator (the <see cref="GetQueueAsync"/> scoped-read
    /// filter, extracted verbatim): the intersection of the actor's
    /// <see cref="ModeratorAssignment"/> component ids and the set of
    /// components whose <see cref="Kumunita.Core.UserInfo.Component.ModeratorAccess"/>
    /// flag is ON (the U6 handoff's flag-ON gate). An empty set when the
    /// actor has no <see cref="ModeratorAssignment"/> row on any
    /// <c>ModeratorAccess</c>-ON component — the badge's count is then 0.
    /// </summary>
    private async Task<HashSet<string>> ScopedComponentIdsAsync(
        string actorId,
        IDocumentSession session)
    {
        var myComponents = (await _profileSettings
            .GetAssignmentsAsync(actorId).ConfigureAwait(false))
            .Where(a => a.ComponentId is not null)
            .Select(a => a.ComponentId)
            .ToHashSet();
        var components = (await session.Query<Kumunita.Core.UserInfo.Component>()
            .ToListAsync().ConfigureAwait(false)).ToDictionary(c => c.Id);
        var scoped = new HashSet<string>();
        foreach (var componentName in components.Keys)
        {
            if (!myComponents.Contains(componentName))
                continue;
            if (components[componentName].ModeratorAccess == true)
                scoped.Add(componentName);
        }
        return scoped;
    }

    /// <inheritdoc />
    public async Task<int> CountOpenReportsAsync(
        string actorId,
        bool isGlobalAdmin,
        bool isStanding,
        CancellationToken ct = default)
    {
        // SEC-READ: report-gated (ADR 0003) — the badge self-filters the same
        // way the queue does (C-M3b·4 SoD): a standing-moderator sees only
        // reports on their assigned components with ModeratorAccess ON; a
        // plain resident sees none; a GlobalAdmin sees all.
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());

        if (!isGlobalAdmin && isStanding)
        {
            var scopedComponentIds = await ScopedComponentIdsAsync(actorId, session).ConfigureAwait(false);
            if (scopedComponentIds.Count == 0)
                return 0;
            return await session.Query<Report>()
                .CountAsync(r => (r.Status == "filed" || r.Status == "assigned"
                                 || r.Status == "unlocked" || r.Status == null)
                    && r.ComponentId != null
                    && scopedComponentIds.Contains(r.ComponentId))
                .ConfigureAwait(false);
        }

        if (!isGlobalAdmin)
        {
            // A plain resident (Member, no standing-moderator row) sees NO
            // reports (the C-M3b·4 SoD pin projects at the display level on
            // the read lanes) — the badge's count is 0 by construction.
            return 0;
        }

        return await session.Query<Report>()
            .CountAsync(r => r.Status == "filed" || r.Status == "assigned"
                || r.Status == "unlocked" || r.Status == null)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ModerationDetailReadModel> GetDetailAsync(
        string reportId,
        CancellationToken ct = default)
    {
        // SEC-READ: report-gated (ADR 0003) — ModerationController.Resolve scopes the read to a GlobalAdmin or a standing-moderator on the report's component (IsInActorScopeAsync, C-M3b·4 SoD)
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());

        var report = await session.LoadAsync<Report>(reportId).ConfigureAwait(false);
        if (report is null)
            throw new KeyNotFoundException($"Report '{reportId}' was not found.");

        // ADR 0173 — the report-targets lane (the controller's previous
        // <c>Resolve</c> target block, verbatim): the report's target is
        // EITHER a post (PostId set) OR one of the new surfaces
        // (TargetKind / TargetId set). A missing target is a 404 (the
        // controller's previous <c>return NotFound()</c> shape — the
        // <c>KeyNotFoundException</c> the Web layer maps).
        string? postTitle = null, postBody = null, postAuthorId = null;
        string? targetKind = report.TargetKind, targetId = report.TargetId;
        string? targetTitle = null, targetAuthorId = null;

        if (targetKind is not null && targetId is not null)
        {
            switch (targetKind)
            {
                case "event":
                {
                    var ev = await session.LoadAsync<Kumunita.Core.Events.Event>(targetId).ConfigureAwait(false);
                    if (ev is null)
                        throw new KeyNotFoundException($"Event '{targetId}' (referenced by report '{report.Id}') was not found.");
                    targetTitle = ev.Title; postBody = ev.Body; targetAuthorId = ev.AuthorId;
                    break;
                }
                case "project":
                {
                    var pr = await session.LoadAsync<Kumunita.Core.Projects.Project>(targetId).ConfigureAwait(false);
                    if (pr is null)
                        throw new KeyNotFoundException($"Project '{targetId}' (referenced by report '{report.Id}') was not found.");
                    targetTitle = pr.Title; postBody = pr.Description; targetAuthorId = pr.AuthorId;
                    break;
                }
                case "board":
                {
                    var bd = await session.LoadAsync<KanbanBoard>(targetId).ConfigureAwait(false);
                    if (bd is null)
                        throw new KeyNotFoundException($"Board '{targetId}' (referenced by report '{report.Id}') was not found.");
                    targetTitle = bd.Title; postBody = bd.Description; targetAuthorId = bd.AuthorId;
                    break;
                }
                case "todo":
                {
                    var td = await session.LoadAsync<TodoItem>(targetId).ConfigureAwait(false);
                    if (td is null)
                        throw new KeyNotFoundException($"To-do '{targetId}' (referenced by report '{report.Id}') was not found.");
                    targetTitle = td.Title; targetAuthorId = td.AuthorId;
                    break;
                }
                case "grouppost":
                {
                    var gp = await session.LoadAsync<Post>(targetId).ConfigureAwait(false);
                    if (gp is null)
                        throw new KeyNotFoundException($"Group post '{targetId}' (referenced by report '{report.Id}') was not found.");
                    targetTitle = gp.Title; postBody = gp.Body; targetAuthorId = gp.AuthorId;
                    break;
                }
            }
        }
        else
        {
            var post = await session.LoadAsync<Post>(report.PostId).ConfigureAwait(false);
            if (post is null)
                throw new KeyNotFoundException($"Post '{report.PostId}' (referenced by report '{report.Id}') was not found.");
            postTitle = post.Title; postBody = post.Body; postAuthorId = post.AuthorId;
        }

        var reporter  = await UserInfo.GetProfileAsync(report.ReporterId).ConfigureAwait(false);
        var postAuthor = await UserInfo.GetProfileAsync(
            (postAuthorId ?? targetAuthorId) ?? string.Empty).ConfigureAwait(false);

        var componentNames = (await session.Query<Kumunita.Core.UserInfo.Component>()
            .ToListAsync().ConfigureAwait(false))
            .ToDictionary(c => c.Id, c => c.Name);

        // ADR 0023 — the reply-report-target lane (the controller's previous
        // reply block, verbatim).
        string? replyId = report.ReplyId;
        string? replyBody = null;
        string? replyAuthorName = null;
        if (replyId is not null)
        {
            var reply = await session.LoadAsync<PostReply>(replyId).ConfigureAwait(false);
            if (reply is not null)
            {
                replyBody       = reply.Body;
                replyAuthorName =
                    (await UserInfo.GetProfileAsync(reply.AuthorId).ConfigureAwait(false))?.DisplayName;
            }
        }

        var moderators = await GetStandingModeratorsAsync(report.ComponentId).ConfigureAwait(false);

        var statusKey = report.Status ?? "filed";
        return new ModerationDetailReadModel
        {
            ReportId         = report.Id,
            PostId           = report.PostId,
            ComponentId      = report.ComponentId,
            ComponentName    = report.ComponentId is not null ? componentNames.GetValueOrDefault(report.ComponentId) : null,
            Reason           = report.Reason,
            Status           = statusKey,
            Outcome          = report.Outcome,   // IMPROVE-UX A03 — the R03 resolution outcome (the Web layer's Unhide-form predicate)
            At               = report.At,
            ReporterName     = reporter?.DisplayName,
            PostTitle        = postTitle,
            PostBody         = postBody,
            PostAuthorName   = postAuthor?.DisplayName,
            ReplyId          = replyId,
            ReplyBody        = replyBody,
            ReplyAuthorName  = replyAuthorName,
            TargetKind       = targetKind,
            TargetId         = targetId,
            TargetTitle      = targetTitle,
            TargetAuthorName = targetAuthorId is null
                ? null
                : (await UserInfo.GetProfileAsync(targetAuthorId).ConfigureAwait(false))?.DisplayName,
            Moderators       = moderators,
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MyReportRow>> ListMyReportsAsync(
        string reporterId,
        CancellationToken ct = default)
    {
        // SEC-READ: owner-only — the reporter's own reports (a read, not a
        // decision — the GetQueueAsync / GetDetailAsync precedent: no
        // IAuthorizationService call, no AccessAudit row; the report is the
        // reporter's own, the read is a list, not a scoped-read gate).
        if (string.IsNullOrEmpty(reporterId))
            throw new ArgumentException("A reporter id is required.", nameof(reporterId));
        if (_posts is null)
            throw new InvalidOperationException(
                "The posts read seam (Posts.PostService) is not wired — the " +
                "ModerationService ListMyReportsAsync read seam (IMPROVE-UX R05) requires it.");

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var reports = await session
            .Query<Report>()
            .Where(r => r.ReporterId == reporterId)
            .OrderByDescending(r => r.At)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var rows = new List<MyReportRow>(reports.Count);
        foreach (var r in reports)
        {
            string? targetKind = r.TargetKind;
            string? targetTitle = null;

            if (targetKind is not null && r.TargetId is not null)
            {
                // The other target kinds (ADR 0173) — resolved directly (the
                // GetDetailAsync target switch); their visibility rules are out
                // of scope for R05 (the plan is posts-first, A02).
                string? title = null;
                switch (targetKind)
                {
                    case "event":
                        title = (await session.LoadAsync<Kumunita.Core.Events.Event>(r.TargetId).ConfigureAwait(false))?.Title;
                        break;
                    case "project":
                        title = (await session.LoadAsync<Project>(r.TargetId).ConfigureAwait(false))?.Title;
                        break;
                    case "board":
                        title = (await session.LoadAsync<KanbanBoard>(r.TargetId).ConfigureAwait(false))?.Title;
                        break;
                    case "todo":
                        title = (await session.LoadAsync<TodoItem>(r.TargetId).ConfigureAwait(false))?.Title;
                        break;
                    case "grouppost":
                        title = (await session.LoadAsync<Post>(r.TargetId).ConfigureAwait(false))?.Title;
                        break;
                }
                targetTitle = title;
            }
            else
            {
                // The original M3b shape (a post target): the title is gated on
                // the reporter's visibility — a null Post in the
                // PostDetailResult (denied / hidden / removed / deleted) means
                // the reporter can no longer read it → no title (the R05 DoD
                // "target no longer visible → no title" pin).
                if (r.PostId.Length > 0)
                {
                    var detail = await _posts.GetPostAsync(r.PostId, reporterId).ConfigureAwait(false);
                    targetTitle = detail.Post?.Title;
                }
            }

            rows.Add(new MyReportRow
            {
                ReportId        = r.Id,
                PostId          = r.PostId,
                ComponentId     = r.ComponentId,
                Reason          = r.Reason,
                Status          = r.Status ?? "filed",
                At              = r.At,
                TargetKind      = targetKind,
                TargetTitle     = targetTitle,
                Outcome         = r.Outcome,
                ResolutionNote  = r.ResolutionNote,
                ResolvedAt      = r.ResolvedAt,
                ResolvedBy      = r.ResolvedBy,
            });
        }
        return rows;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModerationStandingModerator>> GetStandingModeratorsAsync(
        string? componentId,
        CancellationToken ct = default)
    {
        // SEC-READ: report-gated (ADR 0003) — the standing-moderator list for the report's component is surfaced only from GetDetailAsync (which is report-gated)
        if (componentId is null)
            return [];   // C-M3b·4 "no fabricated component"

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var standingModeratorIds = await session
            .Query<ModeratorAssignment>()
            .Where(a => a.ComponentId == componentId)
            .Select(a => a.UserId)
            .ToListAsync()
            .ConfigureAwait(false);

        var moderators = new List<ModerationStandingModerator>(standingModeratorIds.Count);
        foreach (var uid in standingModeratorIds)
        {
            var profile = await UserInfo.GetProfileAsync(uid).ConfigureAwait(false);
            moderators.Add(new ModerationStandingModerator(uid, profile?.DisplayName));
        }
        return moderators;
    }

    /// <inheritdoc />
    public async Task<bool> IsInActorScopeAsync(
        string actor,
        string? componentId,
        CancellationToken ct = default)
    {
        if (componentId is null) return false;   // C-M3b·4 "no fabricated component"
        var assignments = await _profileSettings.GetAssignmentsAsync(actor).ConfigureAwait(false);
        if (!assignments.Any(a => a.ComponentId == componentId))
            return false;
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var component = await session.LoadAsync<Kumunita.Core.UserInfo.Component>(componentId).ConfigureAwait(false);
        return component?.ModeratorAccess == true;
    }
}
