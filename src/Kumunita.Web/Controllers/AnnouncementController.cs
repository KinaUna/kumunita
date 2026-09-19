using System.Collections.Generic;
using Kumunita.Core.Announcements;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/announcements</c> surface (the "platform announcements" lane —
/// distinct from the per-community <see cref="PostsController"/>'s
/// audience-restricted <see cref="Posts.Post"/> lanes):
/// <list type="bullet">
/// <item><c>GET /announcements/{id}</c> — the detail view: the full body
/// of one announcement. <b>Open</b> to unauthenticated visitors, like the
/// list; the
/// <see cref="Kumunita.Core.Announcements.IAnnouncementService.GetAsync"/>
/// visibility gate is the sole reader (public scope always, community
/// scope when signed in, a community-targeted row visible to that
/// community's moderator/members or a GlobalAdmin). A missing or not
/// visible id is a 404 (announcements are not audience-restricted
/// content, so there is no 403/audit lane — see the service's seam).</item>
/// <item><c>GET /announcements</c> — the read surface, <b>open</b> to
/// unauthenticated visitors (a public-scope announcement is by definition
/// visible whether or not the visitor is signed in — the maintenance-notice
/// case). The controller's <see cref="ListVisibleAsync"/> filter is the
/// sole visibility gate: public scope always, community scope when
/// signed in.</item>
/// <item><c>GET /announcements/new</c> + <c>POST /announcements/new</c> —
/// the write lane, <b>[Authorize(Roles = GlobalAdmin, Moderator)]</b>. A
/// GlobalAdmin may author <see cref="AnnouncementScope.Public"/> or
/// <see cref="AnnouncementScope.Community"/>; a Moderator may author
/// <see cref="AnnouncementScope.Community"/> only — the same split
/// <see cref="AnnouncementService.CreateAsync"/> re-checks server-side at
/// POST (defense-in-depth: the ASP.NET gate narrows the author, the service
/// narrows the scope, together they pin the two-way split).</item>
/// <item><c>GET /announcements/{id}/edit</c> + <c>POST
/// /announcements/{id}/edit</c> — the edit write lane,
/// <b>[Authorize(Roles = GlobalAdmin, Moderator)]</b>. The edit gate is
/// <em>distinct</em> from the create split and is re-checked by
/// <see cref="AnnouncementService.UpdateAsync"/> server-side at POST,
/// against the <em>stored</em> row: a GlobalAdmin may edit any row; a
/// community moderator may edit a <em>community-targeted</em> row they
/// moderate; but the flat "all residents" lane (Community scope, no target)
/// is editable only by its <em>author</em> or a GlobalAdmin — a moderator
/// who did not author it is denied (ADR 0017). Defense-in-depth: the ASP.NET
/// gate narrows the role, the service pins the per-row rule, and the
/// <c>CanEdit</c> affordance on the index/detail surfaces keeps the button's
/// rule in step with both.</item>
/// <item><c>POST /announcements/{id}/delete</c> — <b>[Authorize(Roles =
/// GlobalAdmin)]</b> (delete the lane is GlobalAdmin-only by design; the
/// <see cref="AnnouncementService"/> delete lane is the single write surface).</item>
/// </list>
/// <para>
/// <b>ADR 0006-D:</b> a thin HTTP layer — routes + authz + shape; the
/// visibility split is the <see cref="AnnouncementService"/>'s, never
/// re-derived here. The principal's subject id + role set is minted from the
/// signed-in cookie (<see cref="KumunitaPrincipal"/>), passed to the service
/// as the <c>actorId</c> + <c>authorRoles</c> — no DB re-read for role
/// shape (the claim set is the principal, ADR 0006-B); the author's
/// <em>display name</em> (a read surface only, never an access decision) is
/// a plain <c>IUserInfoService.GetProfileAsync</c> read.
/// </para>
/// </summary>
public sealed class AnnouncementController(
    IAnnouncementService announcements,
    IUserInfoService userInfo,
    ILocalizationService localization,
    IDocumentStore store) : Controller
{
    private static string? SubjectId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    private static IReadOnlySet<string> RoleSet(System.Security.Claims.ClaimsPrincipal user)
        => user?.Claims
            .Where(c => c.Type == Kumunita.Core.Identity.ClaimTypes.Role)
            .Select(c => c.Value)
            .ToHashSet()
            ?? new HashSet<string>();

    /// <summary>The caller's role-dependent scope options (GlobalAdmin: both
    /// scopes; Moderator: community only) — a shape convenience, never the gate.</summary>
    private static IReadOnlyCollection<AnnouncementScope> RoleAllowedScopes(IReadOnlySet<string> roles)
    {
        var allowed = new List<AnnouncementScope> { AnnouncementScope.Community };
        if (roles.Contains(Roles.GlobalAdmin))
            allowed.Insert(0, AnnouncementScope.Public);
        return allowed;
    }

    /// <summary>
    /// Seeds a compose form's <c>AllowedScopes</c>, <c>TargetCommunities</c>,
    /// and <c>Languages</c> (ADR 0018) — a GlobalAdmin may target any
    /// community, a Moderator only the ones they moderate (read from their
    /// <c>moderator:{id}</c> standing claims). A shape convenience only; the
    /// service pins the whole split server-side at POST.
    /// <para>
    /// <b>Languages (ADR 0018, ADR 0005 B):</b> the instance's **enabled**
    /// <see cref="LanguageCatalog"/>, ordered by <c>SortOrder</c>, read via
    /// the controller's existing <c>IDocumentStore</c> (the
    /// <see cref="LocaleController.Index"/> catalog read pattern) — no new
    /// constructor dependency (the 10 test-construction sites of this
    /// controller stay untouched). The form leaves the selection empty by
    /// default so the *instance default* is what the service materializes
    /// server-side at write time; on the edit lane the stored row's
    /// <c>LanguageCode</c> pre-selects it.
    /// </para>
    /// </summary>
    private async Task SeedComposeOptionsAsync(AnnouncementComposeViewModel model, IReadOnlySet<string> roles)
    {
        model.AllowedScopes = RoleAllowedScopes(roles);
        var components = await userInfo.GetComponentsAsync(enabledOnly: true).ConfigureAwait(false);
        model.TargetCommunities = components
            .Where(c => roles.Contains(Roles.GlobalAdmin) || roles.Contains(Roles.ModeratorComponent(c.Id)))
            .ToList();

        // ADR 0018 — the authored-in language picker options (the enabled
        // catalog, the SeedComposeOptionsAsync single seed site for both the
        // create and edit compose forms). Read through the HTTP-free
        // ILocalizationService seam (the same seam the Posts/Groups controllers
        // use) rather than the raw store — a plain substitute in the unit tests.
        var catalog = await localization.ListLanguagesAsync().ConfigureAwait(false);
        model.Languages = catalog
            .Where(l => l.Enabled)
            .OrderBy(l => l.SortOrder)
            .Select(l => (l.Id, l.NativeName))
            .ToList();

        // ADR 0018 — pre-select the instance default so the picker highlights
        // the right option and a no-change submit is a concrete BCP-47 code
        // (never an empty row). The edit lane sets model.LanguageCode from the
        // stored row *before* calling this, and that value is preserved here
        // (the guard only fills an unset/blank selection).
        if (string.IsNullOrWhiteSpace(model.LanguageCode))
            model.LanguageCode = await localization.GetDefaultLanguageCodeAsync().ConfigureAwait(false);
    }

    // ── Read (GET /announcements) ─

    /// <summary>
    /// The read surface: the caller-visible announcements (public scope always,
    /// community scope when signed in) — latest first. No [Authorize]: visitors
    /// see the public-scope set; residents see the union.
    /// </summary>
    [HttpGet("/announcements")]
    public async Task<IActionResult> Index()
    {
        var subjectId = SubjectId(User);
        var roles     = RoleSet(User);
        var visible   = await announcements.ListVisibleAsync(subjectId, roles);

        var authorIds = visible.Select(a => a.AuthorId).Distinct().ToHashSet();

        // Resolve each distinct author's display name once (a *display* lookup,
        // never an access decision — the public/community split gate is the
        // service's, not the display-name's). Missing profile row: fall back
        // to the raw subject id (null-safe: no null-coalescing exception).
        var authorNames = new Dictionary<string, string>(authorIds.Count);
        foreach (var id in authorIds)
        {
            string name = id;
            var profile = await userInfo.GetProfileAsync(id);
            if (profile?.DisplayName is not null && profile.DisplayName.Length > 0)
                name = profile.DisplayName;
            authorNames[id] = name;
        }

        var components = await userInfo.GetComponentsAsync(enabledOnly: true);
        var componentNames = components.ToDictionary(c => c.Id, c => c.Name);

        var rows = visible
            .Select(a => new AnnouncementRow(a.Id, a.Scope, a.Title ?? string.Empty, a.Body, a.Created,
                                             authorNames[a.AuthorId], a.AuthorId, a.Pinned, a.CommunityId, a.CommunityId is not null && componentNames.TryGetValue(a.CommunityId, out var cn) ? cn : null))
            .ToList();

        return View(new AnnouncementIndexViewModel(rows));
    }

    // ── Detail (GET /announcements/{id}) ───────────────────────────────────

    /// <summary>
    /// The detail view: one announcement's full body (the /announcements list
    /// shows a preview link per row). No [Authorize]: the same as the list,
    /// the service's <see cref="Kumunita.Core.Announcements.IAnnouncementService.GetAsync"/>
    /// gate is the reader (public scope always, community scope when signed
    /// in, a community-targeted row visible to that community's
    /// moderator/members or a GlobalAdmin). A missing or not-visible id both
    /// return null and map to <see cref="NotFound"/> (announcements are not
    /// audience-restricted content — no 403/audit lane on this bounded
    /// context, unlike the <see cref="PostsController"/>'s post lane).
    /// </summary>
    [HttpGet("/announcements/{id}")]
    public async Task<IActionResult> Detail(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        var a = await announcements.GetAsync(id, SubjectId(User), RoleSet(User));
        if (a is null)
            return NotFound();

        // The author's display name — a *display* read (GetProfileAsync),
        // never an access decision (the visibility gate already ran inside
        // the service). Missing profile row: fall back to the raw subject id.
        var profile = await userInfo.GetProfileAsync(a.AuthorId);
        string authorName = profile?.DisplayName is not null && profile.DisplayName.Length > 0
            ? profile.DisplayName
            : a.AuthorId;

        string? communityName = null;
        if (a.CommunityId is not null)
        {
            var components = await userInfo.GetComponentsAsync(enabledOnly: true);
            communityName = components.FirstOrDefault(c => c.Id == a.CommunityId)?.Name;
        }

        // The Edit button's affordance flag — the same edit rule the service's
        // EnsureEditPermissionAsync re-checks server-side at POST, evaluated
        // against the *stored* row (the /announcements index's canEdit rule,
        // carried to the detail page). A shape convenience only: the real gate
        // is the service, so a non-authorized viewer never sees the button and
        // never has to hit the 403.
        bool canEdit = CanEditAnnouncement(a, RoleSet(User), SubjectId(User));

        // ADR 0029 — the announcement's user-added translations (a "a read,
        // not a decision" surface; the announcement's flat scope gate already
        // ran in GetAsync above) and the enabled-catalog language set the
        // chips / "add a translation" candidate list render from (the
        // ADR 0022 post-detail shape).
        var translations = await announcements.GetAnnouncementTranslationsAsync(a.Id) ?? [];
        var enabledLanguages = await SeedLanguagePickerAsync();
        var translationCodes = translations.Select(t => t.LanguageCode).ToHashSet();
        var languages = enabledLanguages
            .Select(l => new LanguageOption(l.Code, l.NativeName, translationCodes.Contains(l.Code)))
            .ToList();
        var actor = SubjectId(User) ?? string.Empty;
        var canTranslate = AnnouncementService.CanTranslateAnnouncement(
            a.Scope, a.CommunityId, actor, RoleSet(User));

        return View(new AnnouncementDetailViewModel(
            a.Id, a.Scope, a.Title, a.Body, a.Created, a.Modified,
            authorName, a.AuthorId, a.Pinned, communityName, canEdit,
            translations, languages, canTranslate, a.LanguageCode,
            // ADR 0037 — the author-only draft surface: GetAsync returns a
            // draft to the author only, so IsAuthor ⇔ IsDraft here; both feed
            // the detail page's draft badge + Publish button (author-only).
            IsAuthor: actor == a.AuthorId,
            IsDraft:  a.IsDraft));
    }

    // ── Add a translation (POST /announcements/{id}/translations) ───────────

    /// <summary>
    /// A **user-added-translation intake** action (ADR 0029) — the
    /// ADR 0022 post-translation lane carried to the Announcements bounded
    /// context. The standing rule (re-checked server-side by
    /// <see cref="AnnouncementService.AddAnnouncementTranslationAsync"/>): a
    /// <c>GlobalAdmin</c> or a <c>Translator</c> for any announcement, plus a
    /// community <c>Moderator</c> of an announcement targeted at a community
    /// they moderate (a flat community announcement has no such moderator
    /// lane). The <c>[Authorize(Roles)]</c> gate is coarse — it keeps
    /// anonymous / plain residents off the route and lets any of the three
    /// standing-holder roles reach the service; the service is the authority.
    ///
    /// The add is **add-only**: a (announcement, language) already translated
    /// is a shape error (the unique index is the DB-layer backstop) — there is
    /// no edit / replace lane (the ADR 0022 contract).
    /// </summary>
    [HttpPost("/announcements/{id}/translations")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTranslation(
        [FromRoute] string id,
        [FromForm] string? languageCode,
        [FromForm] string? title,
        [FromForm] string? body)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
            return new ForbidResult();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return RedirectToAction("Detail", "Announcement", new { id });
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            TempData["error"] = "A translation needs some text.";
            return RedirectToAction("Detail", "Announcement", new { id });
        }

        // The viewer must be able to see the announcement (re-run the flat
        // scope gate — a null row is a 404, the announcement lane's non-leaky
        // posture, unlike the audience-restricted posts lane's 403).
        var a = await announcements.GetAsync(id, actorId, RoleSet(User));
        if (a is null)
            return NotFound();

        await using var session = store.LightweightSession();
        try
        {
            await announcements.AddAnnouncementTranslationAsync(
                id,
                languageCode,
                string.IsNullOrWhiteSpace(title) ? null : title,
                body,
                actorId,
                RoleSet(User),
                session);
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var name = SeedLanguageName(languageCode);
        TempData["info"] = $"Translation added ({name}).";
        return RedirectToAction("Detail", "Announcement", new { id });
    }

    /// <summary>
    /// Seeds the detail page's language chip / "add a translation" candidate
    /// list (ADR 0005 B) — the instance's **enabled**
    /// <see cref="Kumunita.Core.Localization.LanguageCatalog"/>, ordered by
    /// <c>SortOrder</c>. Read through the HTTP-free
    /// <see cref="ILocalizationService.ListLanguagesAsync"/> seam (the exact
    /// catalog read the <see cref="PostsController"/>'s detail lane uses), so
    /// the announcement surface mirrors an established lane rather than
    /// re-deriving the catalog from the store.
    /// </summary>
    private async Task<IReadOnlyList<(string Code, string NativeName)>> SeedLanguagePickerAsync()
    {
        var catalog = await localization.ListLanguagesAsync().ConfigureAwait(false);
        return catalog
            .Where(l => l.Enabled)
            .OrderBy(l => l.SortOrder)
            .Select(l => (l.Id, l.NativeName))
            .ToList();
    }

    /// <summary>
    /// Resolves a language code to its native name for a confirmation banner
    /// (the <see cref="PostsController.SeedLanguageName"/> pattern). A
    /// "a read, not a decision" catalog lookup; falls back to the raw code
    /// when the language is not in the catalog (a never-blank shape).
    /// </summary>
    private async Task<string> SeedLanguageName(string code)
    {
        var catalog = await localization.ListLanguagesAsync().ConfigureAwait(false);
        return catalog.FirstOrDefault(l => l.Id == code)?.NativeName ?? code;
    }

    /// <summary>
    /// The edit rule for a **stored** announcement (the <c>CanEdit</c>
    /// affordance flag on the index + detail surfaces): a
    /// <see cref="AnnouncementScope.Public"/> row requires GlobalAdmin; a
    /// <see cref="AnnouncementScope.Community"/> row with no target (the flat
    /// "all residents" audience) requires the row's author
    /// (<paramref name="actorId"/> == <c>AuthorId</c>) or GlobalAdmin — a
    /// community moderator who did not author it is <em>denied</em> (ADR 0017);
    /// a <c>Community</c> row with a target requires GlobalAdmin or the
    /// <c>moderator:{CommunityId}</c> standing claim. This mirrors the service's
    /// <see cref="Kumunita.Core.Announcements.AnnouncementService"/>
    /// <c>EnsureEditPermissionAsync</c> gate exactly (the service is the real
    /// gate at POST; this is only the button's visibility decision).
    /// </summary>
    private static bool CanEditAnnouncement(Announcement a, IReadOnlySet<string> roles, string? actorId)
    {
        var isGlobalAdmin = roles.Contains(Roles.GlobalAdmin);
        var isAuthor = !string.IsNullOrEmpty(actorId) && a.AuthorId == actorId;

        if (a.Scope == AnnouncementScope.Public)
            return isGlobalAdmin;

        // Community scope: flat (no target) needs the author or a GlobalAdmin;
        // targeted needs that community's moderator (or a GlobalAdmin).
        if (a.CommunityId is null)
            return isGlobalAdmin || isAuthor;

        return isGlobalAdmin || roles.Contains(Roles.ModeratorComponent(a.CommunityId));
    }

    // ── Create (GET + POST /announcements/new) ─────────────────────────────

    /// <summary>
    /// <c>GET /announcements/new</c> — the write lane's shape. The scope
    /// picker options are the caller's role-dependent set (a GlobalAdmin: both
    /// scopes; a Moderator: community only) — a shape convenience, the
    /// service pins the split server-side at POST.
    /// </summary>
    [HttpGet("/announcements/new")]
    [Authorize(Roles = "GlobalAdmin,Moderator")]
    public async Task<IActionResult> New()
    {
        var model = new AnnouncementComposeViewModel { Scope = "Community" };
        await SeedComposeOptionsAsync(model, RoleSet(User));
        return View(model);
    }

    /// <summary>
    /// <c>POST /announcements/new</c> — the write lane. A <b>live</b> save
    /// (save-as-draft off) redirects to the read page (the new announcement is
    /// visible to the visitor immediately — the split is the gate, not a
    /// re-render). A <b>draft</b> save (ADR 0037, save-as-draft on) persists
    /// and stays on the composer: the first save creates the draft, and each
    /// later save <b>updates the same draft</b> (the <see
    /// cref="Kumunita.Web.Models.AnnouncementComposeViewModel.DraftId"/>
    /// round-trips via a hidden field) so continued work never mints a
    /// duplicate; the author keeps working where they left off. A failed save
    /// (invalid, unauthorized, or missing actor) re-renders <c>New</c> with the
    /// caller's <c>AllowedScopes</c> restored to their role-dependent set (a
    /// POST that 500s into a fresh GET with a different AllowedScopes would
    /// mis-seed the scope picker).
    /// </summary>
    [HttpPost("/announcements/new")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "GlobalAdmin,Moderator")]
    public async Task<IActionResult> New(AnnouncementComposeViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Body))
            ModelState.AddModelError(nameof(model.Body), "Body is required.");
        if (!Enum.TryParse<AnnouncementScope>(model.Scope, out var scope))
            ModelState.AddModelError(nameof(model.Scope), "Scope is required.");

        if (!ModelState.IsValid)
        {
            await SeedComposeOptionsAsync(model, RoleSet(User));
            return View(model);
        }

        // C3 same-transaction lane: the service's SaveChangesAsync is the single
        // write; the caller's store.LightweightSession() is the in-flight
        // transaction (mirrors the PostsController write-lane shape).
        await using var session = store.LightweightSession();
        var authorId = SubjectId(User) ?? string.Empty;
        if (string.IsNullOrEmpty(authorId))
        {
            ModelState.AddModelError(string.Empty, "Not signed in.");
            return View(model);
        }

        try
        {
            var toSave = new Announcement
                {
                    Title  = string.IsNullOrWhiteSpace(model.Title) ? string.Empty : model.Title.Trim(),
                    Body   = model.Body!,
                    Scope  = scope,
                    Pinned = model.Pinned,
                    CommunityId = string.IsNullOrWhiteSpace(model.CommunityId) ? null : model.CommunityId,
                    // ADR 0018 — empty ⇒ the service's ResolveLanguageCodeAsync materializes the instance default at write time (Announcement.LanguageCode is a non-nullable string).
                    LanguageCode = string.IsNullOrWhiteSpace(model.LanguageCode) ? string.Empty : model.LanguageCode,
                    // RC R·3 (U05) — server-side parse of the body's /content-image/{id} links; the client never sends the ids (a form field would be spoofable).
                    ImageIds = ContentImageIds.ExtractContentImageIds(model.Body),
                    // ATT U7 (C-ATT·4) — server-side parse of the body's /attachment/{id} links; the client never sends the ids (a form field would be spoofable).
                    AttachmentIds = AttachmentIds.ExtractAttachmentIds(model.Body),
                    // ADR 0037 — draft mode: saved but invisible to all but the author until published.
                    IsDraft = model.SaveAsDraft,
                };

            Announcement result;
            if (model.SaveAsDraft && !string.IsNullOrWhiteSpace(model.DraftId))
            {
                // ADR 0037 — continuing an already-saved draft: UPDATE the same
                // document (the DraftId round-trips via a hidden field, so the
                // stateless re-render keeps pointing at one draft) instead of
                // minting a duplicate. UpdateAsync is author-gated (the author is
                // always the caller here) and never touches IsDraft — editing a
                // draft never publishes it (ADR 0037).
                toSave.Id = model.DraftId;
                result = await announcements.UpdateAsync(
                    toSave,
                    actorId:    authorId,
                    actorRoles: RoleSet(User),
                    session);
            }
            else
            {
                result = await announcements.CreateAsync(
                    toSave,
                    actorId:     authorId,
                    authorRoles: RoleSet(User),
                    session);
            }

            if (model.SaveAsDraft)
            {
                // ADR 0037 — draft save: persist and let the author keep working.
                // Do NOT navigate away (a redirect to the feed would look like a
                // loss — the draft is invisible there anyway). Re-render the
                // compose form with the author's content still in the editor (the
                // model round-trips Title/Body/Scope/etc.) and a confirmation, so
                // they continue where they left off. Remember the draft's id so
                // the next save updates this same draft (no duplicates). The
                // draft is reachable any time via "My drafts".
                model.DraftId = result.Id;
                await SeedComposeOptionsAsync(model, RoleSet(User));
                TempData["info"] = "Saved as draft — it's hidden from everyone (including admins) until you publish it. Find it under “My drafts”.";
                return View(model);
            }

            TempData["info"] = "Announcement created.";
            return RedirectToAction("Index");
        }
        catch (UnauthorizedAccessException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await SeedComposeOptionsAsync(model, RoleSet(User));
            return View(model);
        }
    }

    // ── Edit (GET + POST /announcements/{id}/edit) ─────────────────────────

    /// <summary>
    /// <c>GET /announcements/{id}/edit</c> — the edit write lane's shape,
    /// seeded from the existing announcement (Title/Body/Scope preserved).
    /// The scope picker options are the caller's role-dependent set (a
    /// GlobalAdmin: both scopes; a Moderator: community only). The edit
    /// affordance gate — the same rule the service's
    /// <see cref="IAnnouncementService.UpdateAsync"/> re-checks server-side
    /// at POST — is applied up front (a form a user can't submit shouldn't be
    /// rendered in the first place): a Moderator viewing a public-scope
    /// announcement, or a flat all-residents announcement they did not author,
    /// gets a 403 here (the service's re-check is still the real gate at
    /// POST; this only keeps the button's rule and the form's rule in step).
    /// </summary>
    [HttpGet("/announcements/{id}/edit")]
    [Authorize(Roles = "GlobalAdmin,Moderator")]
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        await using var session = store.QuerySession();
        var existing = await session.LoadAsync<Announcement>(id);
        if (existing is null)
            return NotFound();

        var roles = RoleSet(User);
        if (!CanEditAnnouncement(existing, roles, SubjectId(User)))
        {
            return new ForbidResult();
        }

        // ADR 0018 — pre-select the stored authored-in tag (the announcement
        // edit lane is not ADR-frozen, so the tag is editable here). A blank
        // value on a pre-ADR-0018 row is left as-is; the SeedComposeOptionsAsync
        // ?? guard fills it with the instance default so the picker still
        // highlights a concrete option.
        var model = new AnnouncementComposeViewModel
        {
            Id = id,
            Title = existing.Title,
            Body  = existing.Body,
            Scope = existing.Scope.ToString(),
            Pinned = existing.Pinned,
            CommunityId = existing.CommunityId,
            LanguageCode = existing.LanguageCode,
            // ADR 0037 — mirror the stored draft state into the form (read-only
            // surface on the edit lane; <c>AnnouncementService.UpdateAsync</c>
            // deliberately does not touch <c>IsDraft</c>, so editing a draft
            // never publishes it — only the author's PublishAsync does, the
            // ADR 0037 author-only pin). Seeded for display parity; the
            // checkbox on the edit view is informational.
            SaveAsDraft = existing.IsDraft,
        };
        await SeedComposeOptionsAsync(model, roles);
        return View(model);
    }

    /// <summary>
    /// <c>POST /announcements/{id}/edit</c> — the edit write lane. On
    /// success, redirects to the read page (the edit is visible to the
    /// visitor immediately — the split is the gate, not a re-render). A
    /// denied scope-vs-role split (e.g. a Moderator editing a public-scope
    /// announcement) is a 403; a missing id is a 404.
    /// </summary>
    [HttpPost("/announcements/{id}/edit")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "GlobalAdmin,Moderator")]
    public async Task<IActionResult> Edit(string id, AnnouncementComposeViewModel model)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();
        if (string.IsNullOrWhiteSpace(model.Body))
            ModelState.AddModelError(nameof(model.Body), "Body is required.");
        if (!Enum.TryParse<AnnouncementScope>(model.Scope, out var scope))
            ModelState.AddModelError(nameof(model.Scope), "Scope is required.");

        if (!ModelState.IsValid)
        {
            // Re-seed the picker (the invalid-POST path has no existing doc to
            // seed from — the edit target isn't loadable without a write
            // session; fall back to the role-dependent shape, the same way the
            // create lane does).
            model.Id = id;
            await SeedComposeOptionsAsync(model, RoleSet(User));
            return View(model);
        }

        await using var session = store.LightweightSession();
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
        {
            TempData["error"] = "Not signed in.";
            return new UnauthorizedResult();
        }

        try
        {
            await announcements.UpdateAsync(
                new Announcement
                {
                    Id     = id,
                    Title  = model.Title ?? string.Empty,
                    Body   = model.Body!,
                    Scope  = scope,
                    Pinned = model.Pinned,
                    CommunityId = string.IsNullOrWhiteSpace(model.CommunityId) ? null : model.CommunityId,
                    // ADR 0018 — empty ⇒ the service's ResolveLanguageCodeAsync materializes the instance default; the announcement edit lane is not ADR-frozen, so the tag is editable here.
                    LanguageCode = string.IsNullOrWhiteSpace(model.LanguageCode) ? string.Empty : model.LanguageCode,
                    // RC R·3 (U05) — server-side parse of the body's /content-image/{id} links; the client never sends the ids.
                    ImageIds = ContentImageIds.ExtractContentImageIds(model.Body),
                    // ATT U7 (C-ATT·4) — server-side parse of the body's /attachment/{id} links; the client never sends the ids.
                    AttachmentIds = AttachmentIds.ExtractAttachmentIds(model.Body),
                },
                actorId:    actorId,
                actorRoles: RoleSet(User),
                session);
            TempData["info"] = "Announcement updated.";
            return RedirectToAction("Index");
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // ── Delete (POST /announcements/{id}/delete) ───────────────────────────

    /// <summary>
    /// <c>POST /announcements/{id}/delete</c> — the GlobalAdmin delete lane.
    /// A missing announcement is a 404 (the service's
    /// <see cref="KeyNotFoundException"/> maps to <see cref="NotFound"/>);
    /// on success, the caller returns to the read page (the announcement is
    /// gone for everyone — a hard delete, not a soft-hidden state).
    /// </summary>
    [HttpPost("/announcements/{id}/delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "GlobalAdmin")]
    public async Task<IActionResult> Delete(string id)
    {
        await using var session = store.LightweightSession();
        try
        {
            await announcements.DeleteAsync(id, session);
            TempData["info"] = "Announcement deleted.";
            return RedirectToAction("Index");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // ── Publish (POST /announcements/{id}/publish) — author-only (ADR 0037) ─

    /// <summary>
    /// Publish a draft announcement (ADR 0037): <c>POST
    /// /announcements/{id}/publish</c>. <b>Author-only</b> — the sole lever
    /// that clears <see cref="Announcement.IsDraft"/> is the author's own
    /// choice. A non-author (even a GlobalAdmin) is a 403: a draft is
    /// invisible to them (<see
    /// cref="Kumunita.Core.Announcements.AnnouncementService.GetAsync"/>
    /// returns a draft to the author only), so they have no affordance to
    /// reach this, and the service re-pins the author gate server-side. A
    /// missing id is a 404; on success, redirect back to the detail page (now
    /// visible under its scope).
    /// </summary>
    [HttpPost("/announcements/{id}/publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        var actor = SubjectId(User) ?? string.Empty;
        if (string.IsNullOrEmpty(actor))
            return new UnauthorizedResult();

        // Pre-write gate (the ADR 0037 author-only draft gate): GetAsync
        // returns a draft to the author only, so a null here covers both
        // "missing" and "a draft the actor is not the author of" → 403
        // (non-leaky, the post-lane "403 on denied" shape).
        var a = await announcements.GetAsync(id, actor, RoleSet(User));
        if (a is null)
            return new ForbidResult();

        await using var session = store.LightweightSession();
        try
        {
            await announcements.PublishAsync(id, actor, session);
        }
        catch (UnauthorizedAccessException)
        {
            // Non-author (even a GlobalAdmin) — the 403 shape (the ADR 0037
            // author-only pin; a re-render would leak the draft's content).
            return new ForbidResult();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        TempData["info"] = "Announcement published.";
        return RedirectToAction("Detail", new { id });
    }
}
