using Kumunita.Core.Localization;
using Kumunita.Core.Portability;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/account/portability</c> surface (M27, ADR 0148 D5) — the
/// <b>resident's</b> thin control plane over moving <b>their own authored
/// data</b> in and out, as data (the resident-plane shape, the
/// ADR 0105/0118 personal-by-id house pattern). Mirrors
/// <see cref="AdminPortabilityController"/> verbatim in shape but at the
/// resident scope: <see cref="Microsoft.AspNetCore.Authorization.AuthorizeAttribute"/>
/// gated (the verified-resident self-lane — the resident's own data only, no
/// GlobalAdmin break-glass, no audience decision), a thin wrapper over the
/// <see cref="IUserPortabilityService"/> seam, and the audit rows are the
/// <b>service's</b> (exactly one <c>AccessAudit</c> per write,
/// <c>Via = Owner</c>, <c>TargetKind "portability"</c> — the controller adds
/// none, the ADR 0105 <c>messaging.toggle</c> one-audit-row shape).
/// <para>
/// U07 ships the surface's index + export + import halves: the <c>Index</c>
/// action (the export button + the import upload form + the status area),
/// the <c>Export</c> action (the <c>GET /account/portability/export</c>
/// stream, the <see cref="IUserPortabilityService.ExportAsync"/> delegation +
/// the <c>Content-Disposition: attachment</c> serve idiom), and the
/// <c>Import</c> action (the <c>POST /account/portability/import</c> upload,
/// the <see cref="IUserPortabilityService.ClassifyAsync"/> delegation + the
/// fail-closed render of the <c>clean</c>/<c>duplicate</c>/<c>conflict</c>
/// report). The resolve-review UI + the
/// <c>POST /account/portability/import/resolve</c> action are U08.
/// </para>
/// <para>
/// **Zero new authorization surface (C-M27·7):** no <c>AccessAction</c> /
/// <c>AccessVia</c> / <c>IAuthorizationService</c> branch / <c>Audience</c>
/// — the gate is the <c>[Authorize]</c> verified-resident self-lane; the
/// per-entity conflict decision is a <b>business decision made by the
/// resident in the UI</b> (U08), never an authorization decision. The
/// <c>myportability.*</c> kw-l namespace is deliberately distinct from M11's
/// admin <c>portability.*</c> keys so the resident surface never collides
/// with the operator surface (D9).
/// </para>
/// </summary>
[Route("account/portability")]
[Authorize]
public sealed class UserPortabilityController(
    IUserPortabilityService portability,
    ILocalizationService localization,
    ITranslationProvider? translationProvider = null,
    // U08 — the "add elsewhere" target picker's option source (the
    // resident's member-of groups, read via the UserInfo read seam — a
    // business read, NOT the authorization engine; C-M27·7's zero-new-
    // authorization-surface pin is about IAuthorizationService, which U08
    // adds no branch of. Optional so the test-construction site compiles
    // without it; null → an empty option list (the fail-closed picker).
    Kumunita.Core.UserInfo.IUserInfoService? userInfo = null) : Controller
{
    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// <c>GET /account/portability</c> — the index (the export button + the
    /// import upload form + the status area). A read — no audit row (the
    /// design doc §surface: the index read is un-audited, C-M27·6 "reads emit
    /// none"). The import upload form (rendered in the view) targets
    /// <c>POST /account/portability/import</c> — the
    /// <see cref="Import(IFormFile)"/> action (the surface's import half).
    /// </summary>
    [HttpGet]
    public IActionResult Index() =>
        // The view lives at the mandated Views/Account/Portability/ path (the
        // resident account surface, the ADR 0105/0118 personal-by-id house
        // pattern) — outside this controller's default Views/UserPortability/
        // convention, so it is referenced by explicit virtual path.
        View("~/Views/Account/Portability/Index.cshtml");

    /// <summary>
    /// <c>GET /account/portability/export</c> — streams the resident's own
    /// <c>*.kumunita</c> archive (the design doc §surface:
    /// <c>Content-Disposition: attachment</c>, the ADR 0034 attachment
    /// lane's shape — the same serve headers the M11 operator export uses).
    /// Delegates to <see cref="IUserPortabilityService.ExportAsync"/> — which
    /// emits exactly one <c>portability.export</c> <c>AccessAudit</c> row
    /// (<c>TargetKind "portability"</c>, <c>Via = Owner</c>, verb
    /// <c>export</c>; the controller adds none — C-M27·6). The actor is the
    /// resident's own <c>subjectId</c> (the archive's scope anchor + the audit
    /// row's actor).
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export()
    {
        var actor = ActorId(User) ?? string.Empty;
        var stream = await portability.ExportAsync(actor);

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Disposition"] =
            "attachment; filename=\"my-data.kumunita\"";
        return File(stream, "application/octet-stream");
    }

    /// <summary>
    /// <c>POST /account/portability/import</c> — classifies the uploaded
    /// <c>*.kumunita</c> archive (the design doc §surface: the D5 import
    /// half, part 1 of 2 — U08 ships the resolve-review UI + the
    /// <c>POST /account/portability/import/resolve</c> apply). Delegates to
    /// <see cref="IUserPortabilityService.ClassifyAsync"/> — the
    /// <c>clean</c>/<c>duplicate</c>/<c>conflict</c> classification + the
    /// per-entity reference-availability report, run to completion before any
    /// write (C-M27·5, the classify phase is read-only). The actor is the
    /// resident's own <c>subjectId</c>.
    /// <para>
    /// **Fail-closed render (C-M27·4):** a classify failure (a malformed /
    /// out-of-scope / unsupported archive) returns the
    /// <see cref="UserPortabilityImportPlan"/> closed failure set — this
    /// action renders it into <c>TempData["error"]</c> (the
    /// <c>myportability.status</c> kw-l key + the failure list) and the
    /// instance is unchanged (zero writes). A clean classification renders
    /// <c>TempData["info"]</c> — the status-area line (the
    /// <c>myportability.status</c> kw-l key + the
    /// <c>clean</c>/<c>duplicate</c>/<c>conflict</c> summary the resident
    /// then resolves, entity by entity, on the U08 resolve-review page).
    /// The <c>_FlashToast</c> partial (the one, uniform flash surface)
    /// renders either, the house
    /// <c>AdminPortabilityController</c> <c>TempData["info"]</c> +
    /// redirect idiom.
    /// </para>
    /// </summary>
    [HttpPost("import")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile archive)
    {
        var actor = ActorId(User) ?? string.Empty;

        if (archive is null || archive.Length == 0)
        {
            TempData["error"] = await T("myportability.status");
            return RedirectToAction(nameof(Index));
        }

        await using var stream = archive.OpenReadStream();
        var plan = await portability.ClassifyAsync(actor, stream);

        if (!plan.Ok)
        {
            // The closed failure set (the C-M27·4 fail-closed pin — the
            // classify phase refused before any write): the
            // myportability.status kw-l key + the failure list, the instance
            // unchanged (zero writes on a classify failure).
            var failures = string.Join("\n", plan.Failures);
            TempData["error"] = $"{(await T("myportability.status"))}\n{failures}";
            return RedirectToAction(nameof(Index));
        }

        // The clean classification: the status-area line (the
        // myportability.status kw-l key) + the clean/duplicate/conflict
        // summary. The per-entity resolve-review (the resident's add-elsewhere
        // / discard choice, entity by entity) is U08's surface — U07 renders
        // only the report the resident is about to resolve.
        var clean = plan.Entities.Count(e => e.Status == UserPortabilityEntityStatus.Clean);
        var duplicates = plan.Entities.Count(e => e.Status == UserPortabilityEntityStatus.Duplicate);
        var conflicts = plan.Entities.Count(e => e.Status == UserPortabilityEntityStatus.Conflict);
        TempData["info"] =
            $"{(await T("myportability.status"))} — {clean} ready, " +
            $"{duplicates} duplicate, {conflicts} to resolve.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// <c>GET /account/portability/import/resolve</c> — the resolve-review
    /// read (the design doc §surface: the resident's per-entity
    /// <c>conflict</c> review, D5 import half part 2 of 2 — U08). The archive
    /// is <b>re-uploaded</b> with the form (the stateless, server-authoritative
    /// shape — the classification is re-derived from the uploaded bytes, never
    /// trusted from the client, the C-M27·5 read-only posture). A
    /// <b>read</b> — <see cref="IUserPortabilityService.ClassifyAsync"/> writes
    /// nothing and emits no <c>AccessAudit</c> row (C-M27·6 "reads emit none").
    /// Renders <see cref="ResolveReviewModel"/> (one row per <c>conflict</c>
    /// entity, the <c>UserPortabilityAbsentReference</c> {Kind, Field, Value}
    /// the §2.4 reason text renders + the form-bound add-elsewhere / discard
    /// choice). The actor is the resident's own <c>subjectId</c>.
    /// </summary>
    [HttpGet("import/resolve")]
    public async Task<IActionResult> ResolveReview(IFormFile archive)
    {
        if (archive is null || archive.Length == 0)
            return RedirectToAction(nameof(Index));

        var actor = ActorId(User) ?? string.Empty;
        await using var stream = archive.OpenReadStream();
        var plan = await portability.ClassifyAsync(actor, stream);

        if (!plan.Ok)
        {
            var failures = string.Join("\n", plan.Failures);
            TempData["error"] = $"{(await T("myportability.status"))}\n{failures}";
            return RedirectToAction(nameof(Index));
        }

        var model = BuildReviewModel(plan);

        // The "add elsewhere" target options — the resident's member-of groups
        // (the §2.4 verbatim: "Add it to a group you are a member of"). Read via
        // the UserInfo read seam (a business read, not the authorization engine
        // — C-M27·7); an absent seam (the test floor) or a lookup failure yields
        // an empty option list (the fail-closed picker — no standing, no target).
        if (userInfo is not null)
        {
            try
            {
                var groups = await userInfo.GetGroupsForUserAsync(actor);
                foreach (var g in groups)
                    model.Targets.Add(new TargetOption
                    {
                        Id = g.Id,
                        Label = string.IsNullOrWhiteSpace(g.Name) ? g.Id : g.Name,
                    });
            }
            catch
            {
                // A standing read that fails leaves the picker empty (the
                // resident can still discard — the §2.4 "or discard it").
            }
        }

        return View("~/Views/Account/Portability/ResolveReview.cshtml", model);
    }

    /// <summary>
    /// <c>POST /account/portability/import/resolve</c> — applies the resident's
    /// per-entity decisions (the design doc §surface: the D5 import half part 2
    /// of 2 — U08, the lane's headline mechanic, C-M27·4). The archive is
    /// <b>re-uploaded</b> with the form: the classification is re-derived from
    /// the uploaded bytes (server-authoritative, never trusted from the client)
    /// and the
    /// <see cref="IUserPortabilityService.ResolveAsync"/> apply reads the entity
    /// rows <b>from the re-uploaded archive stream</b> (the U06 seam refinement
    /// — the <c>archive</c> parameter is the one added so the apply reads the
    /// <c>docs/{Type}.json</c> bytes). The one <c>portability.import.resolve</c>
    /// <c>AccessAudit</c> row (<c>Via = Owner</c>, <c>TargetKind "portability"</c>)
    /// is the <b>service's</b> — the controller adds none (C-M27·6). The actor
    /// is the resident's own <c>subjectId</c>.
    /// <para>
    /// **No auto-merge (C-M27·4):** a <c>conflict</c> entity the resident left
    /// unchosen posts no <see cref="UserPortabilityEntityResolution"/> — it is
    /// not applied, never defaulted. Only an explicit <c>Discard</c> or an
    /// explicit <c>AddElsewhere</c> + target is applied, and an
    /// <c>AddElsewhere</c> whose target the resident has no standing over is a
    /// fail-closed rejection (<see cref="IUserPortabilityService.ResolveAsync"/>
    /// standing check).
    /// </para>
    /// </summary>
    [HttpPost("import/resolve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(ResolveReviewModel model, IFormFile archive)
    {
        if (archive is null || archive.Length == 0 || model?.Conflicts is null)
        {
            TempData["error"] = await T("myportability.status");
            return RedirectToAction(nameof(Index));
        }

        var actor = ActorId(User) ?? string.Empty;

        // The classification is re-derived from the re-uploaded bytes
        // (server-authoritative — the plan the resident saw on the review read
        // is the same, but is never trusted from the client).
        await using var classifyStream = archive.OpenReadStream();
        var plan = await portability.ClassifyAsync(actor, classifyStream);

        if (!plan.Ok)
        {
            var failures = string.Join("\n", plan.Failures);
            TempData["error"] = $"{(await T("myportability.status"))}\n{failures}";
            return RedirectToAction(nameof(Index));
        }

        // The resident's per-entity choices → the locked
        // UserPortabilityEntityResolution set. A posted row must match a real
        // conflict in this archive (a forged / stale row is ignored); an
        // unchosen conflict posts no resolution (the C-M27·4 no-auto-merge pin);
        // an AddElsewhere with no target is not applied (a broken-reference
        // store is worse than no write — the fail-closed posture).
        var validConflicts = new HashSet<string>(
            plan.Entities
                .Where(e => e.Status == UserPortabilityEntityStatus.Conflict)
                .Select(e => $"{e.Kind}|{e.EntityId}"),
            StringComparer.Ordinal);
        var resolutions = new List<UserPortabilityEntityResolution>();
        foreach (var row in model.Conflicts)
        {
            if (string.IsNullOrWhiteSpace(row.Resolution))
                continue; // unresolved → not applied (no auto-merge).
            if (!validConflicts.Contains($"{row.Kind}|{row.EntityId}"))
                continue; // not a conflict in this archive → ignore the row.

            if (row.Resolution == UserPortabilityResolutionKind.Discard.ToString())
            {
                resolutions.Add(new UserPortabilityEntityResolution(
                    row.Kind, row.EntityId, UserPortabilityResolutionKind.Discard,
                    null, null, null));
                continue;
            }

            if (row.Resolution == UserPortabilityResolutionKind.AddElsewhere.ToString()
                && !string.IsNullOrWhiteSpace(row.PickedTargetId))
            {
                resolutions.Add(new UserPortabilityEntityResolution(
                    row.Kind, row.EntityId, UserPortabilityResolutionKind.AddElsewhere,
                    row.PickedTargetId, row.AbsentKind, row.AbsentField));
            }
        }

        // The apply reads the entity rows from the re-uploaded archive (the U06
        // seam refinement — the `archive` parameter). The service emits the one
        // portability.import.resolve audit row (Via = Owner); the controller
        // adds none.
        await using var applyStream = archive.OpenReadStream();
        var result = await portability.ResolveAsync(actor, plan, resolutions, applyStream);

        if (!result.Ok)
        {
            var failures = string.Join("\n", result.Failures);
            TempData["error"] = $"{(await T("myportability.status"))}\n{failures}";
            return RedirectToAction(nameof(Index));
        }

        TempData["info"] =
            $"{(await T("myportability.status"))} — {result.AppliedCount} applied, " +
            $"{result.DiscardedCount} discarded.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Maps a <see cref="UserPortabilityImportPlan"/> to the resolve-review
    /// <see cref="ResolveReviewModel"/> — one <see cref="ResolveConflict"/> row
    /// per <c>conflict</c> entity (the first <c>UserPortabilityAbsentReference</c>
    /// {Kind, Field, Value} the §2.4 reason text renders + the empty form-bound
    /// choice), the clean + duplicate counts as the informational summary.
    /// <c>Clean</c> / <c>duplicate</c> entities are applied/skipped by
    /// <see cref="IUserPortabilityService.ResolveAsync"/> without a resident
    /// decision — only <c>conflict</c> entities are rendered (C-M27·4).
    /// </summary>
    private static ResolveReviewModel BuildReviewModel(UserPortabilityImportPlan plan)
    {
        var model = new ResolveReviewModel
        {
            CleanCount = plan.Entities.Count(e => e.Status == UserPortabilityEntityStatus.Clean),
            DuplicateCount = plan.Entities.Count(e => e.Status == UserPortabilityEntityStatus.Duplicate),
        };
        foreach (var c in plan.Entities.Where(e => e.Status == UserPortabilityEntityStatus.Conflict))
        {
            var firstRef = c.AbsentReferences.FirstOrDefault();
            model.Conflicts.Add(new ResolveConflict
            {
                Kind = c.Kind,
                EntityId = c.EntityId,
                AbsentKind = firstRef?.Kind,
                AbsentField = firstRef?.Field,
                AbsentValue = firstRef?.Value,
            });
        }
        return model;
    }

    /// <summary>
    /// Resolves a <c>myportability.*</c> kw-l key to the resident's effective
    /// language (the house <c>EffectiveLanguageCode.ResolveAsync</c> +
    /// <c>ITranslationProvider.GetAsync</c> seam — the same as the view's
    /// resolution, so the TempData strings render in the resident's language).
    /// Falls back to the <c>en</c> floor when the translation seam is absent
    /// (the test-construction site).
    /// </summary>
    private async Task<string> T(string key)
    {
        if (translationProvider is null)
            return key; // the test floor (no context — the raw key)
        var lang = await EffectiveLanguageCode.ResolveAsync(
            HttpContext?.Request, localization, translationProvider);
        return await translationProvider.GetAsync(key, lang);
    }

    /// <summary>
    /// The resolve-review view model (U08) — the one form-bound shape the
    /// <c>ResolveReview.cshtml</c> renders + the <c>POST /import/resolve</c>
    /// action binds. A settable class (NOT the locked
    /// <see cref="UserPortabilityEntityResolution"/> record — that is a
    /// get-only positional record the default model binder cannot land on).
    /// <see cref="Conflicts"/> holds one <see cref="ResolveConflict"/> row per
    /// <c>conflict</c> entity the resident must decide; <see cref="CleanCount"/>
    /// + <see cref="DuplicateCount"/> are the informational summary (applied /
    /// skipped by <see cref="IUserPortabilityService.ResolveAsync"/> with no
    /// resident decision — C-M27·4). The form field names are
    /// <c>Conflicts[i].*</c> — the default complex binder lands them on
    /// <see cref="Conflicts"/> on the POST.
    /// </summary>
    public sealed class ResolveReviewModel
    {
        /// <summary>The clean-entity count (the informational summary — applied
        /// by <see cref="IUserPortabilityService.ResolveAsync"/> with no
        /// resident decision; not form-bound on the apply POST).</summary>
        public int CleanCount { get; set; }

        /// <summary>The duplicate-entity count (the informational summary —
        /// skipped by <see cref="IUserPortabilityService.ResolveAsync"/>; not
        /// form-bound on the apply POST).</summary>
        public int DuplicateCount { get; set; }

        /// <summary>One row per <c>conflict</c> entity (the resident's
        /// per-entity choice, C-M27·4). The form-bound shape — the
        /// <see cref="UserPortabilityEntityResolution"/> set the apply POST maps
        /// to is derived from this.</summary>
        public List<ResolveConflict> Conflicts { get; } = new();

        /// <summary>The "add elsewhere" target options (the resident's
        /// member-of groups — the §2.4 verbatim "Add it to a group you are a
        /// member of"). Rendered in the view's per-entity target picker; read
        /// data (seeded on the GET, not form-bound on the POST).</summary>
        public List<TargetOption> Targets { get; } = new();
    }

    /// <summary>
    /// One "add elsewhere" target option (U08) — the resident's member-of
    /// group (the <see cref="Kumunita.Core.UserInfo.Group"/> the
    /// <see cref="ResolveConflict.PickedTargetId"/> re-points to). Read data
    /// seeded on the GET (the <c>ViewData</c>-equivalent, carried on the model
    /// so the view renders it without a second lookup seam).
    /// </summary>
    public sealed class TargetOption
    {
        /// <summary>The group id (the <see cref="Kumunita.Core.UserInfo.Group.Id"/>).</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>The group name (the <see cref="Kumunita.Core.UserInfo.Group.Name"/>;
        /// falls back to the id when the name is missing).</summary>
        public string Label { get; init; } = string.Empty;
    }

    /// <summary>
    /// One <c>conflict</c> entity's resolve-review row (U08) — the
    /// <see cref="UserPortabilityAbsentReference"/> {Kind, Field, Value} the
    /// §2.4 reason text renders ("This post references group
    /// <c>{Value}</c>, which does not exist here. Add it to a group you are a
    /// member of, or discard it.") + the form-bound
    /// <see cref="Resolution"/> (empty / "AddElsewhere" / "Discard" — the
    /// default binder posts empty for an unchosen conflict, the C-M27·4
    /// no-auto-merge shape) + the <see cref="PickedTargetId"/> (the AddElsewhere
    /// re-point target the resident chose).
    /// </summary>
    public sealed class ResolveConflict
    {
        /// <summary>The entity kind (e.g. "Post", "Event") — must match a
        /// <c>conflict</c> entry in the archive's classification.</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>The entity id (the id in the archive) — must match a
        /// <c>conflict</c> entry in the archive's classification.</summary>
        public string EntityId { get; set; } = string.Empty;

        /// <summary>The first absent reference's kind (e.g. "Group") — the
        /// §2.4 reason text + the <see cref="UserPortabilityEntityResolution"/>
        /// <c>AbsentRefKind</c>.</summary>
        public string? AbsentKind { get; set; }

        /// <summary>The first absent reference's field (e.g. "GroupId") — the
        /// <see cref="UserPortabilityEntityResolution"/> <c>AbsentRefField</c>.</summary>
        public string? AbsentField { get; set; }

        /// <summary>The first absent reference's value (the id in the archive
        /// that is absent in the target) — the §2.4 reason text.</summary>
        public string? AbsentValue { get; set; }

        /// <summary>The resident's choice — empty (unresolved, not applied),
        /// <c>"AddElsewhere"</c>, or <c>"Discard"</c> (the
        /// <see cref="Kumunita.Core.Portability.UserPortabilityResolutionKind"/>
        /// value names, bound as a plain string so an unchosen conflict posts
        /// empty — never a silent default, the C-M27·4 no-auto-merge pin).</summary>
        public string? Resolution { get; set; }

        /// <summary>The AddElsewhere re-point target (the group/component/page/
        /// tag the resident chose) — required for an AddElsewhere to be applied;
        /// absent for a Discard.</summary>
        public string? PickedTargetId { get; set; }
    }
}
