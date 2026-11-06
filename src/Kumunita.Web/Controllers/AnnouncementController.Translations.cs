using Kumunita.Core.Announcements;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/announcements/{id}/translations</c> translation lane (ADR 0029
/// add-only, ADR 0048 edit + remove) — the three user-added-translation
/// intake actions. A partial class of <see cref="AnnouncementController"/>
/// extracted so the main file stays under the 600 <c>cs_file</c> ceiling
/// (C3-04, the still-large-file tail). The shared helpers
/// (<c>SubjectId</c>, <c>RoleSet</c>, <c>SeedLanguageName</c>,
/// <c>announcements</c>, <c>store</c>) are resolved through the same
/// partial-class scope.
/// </summary>
public partial class AnnouncementController
{
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
    /// is a shape error (the unique index is the DB-layer backstop). The
    /// ADR 0048 edit / remove lanes are the sibling routes below this one.
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
            TempData["error"] = await this.FlashAsync("flash.translation_choose_language", "Choose a language for the translation.");
            return RedirectToAction("Detail", "Announcement", new { id });
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            TempData["error"] = await this.FlashAsync("flash.translation_needs_text", "A translation needs some text.");
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

        var name = await SeedLanguageName(languageCode);
        TempData["info"] = await this.FlashAsync("flash.translation_added", "Translation added ({0}).", name);
        return RedirectToAction("Detail", "Announcement", new { id });
    }

    // ── ADR 0048 — edit + delete lanes for announcement translations ─────
    // ADR 0029 was add-only; ADR 0048 lifts the "add-only" pin on the same
    // standing matrix (GlobalAdmin ∪ Translator ∪ targeted-community
    // Moderator). Failure shapes mirror the add lane (denied → 403; missing →
    // 404).

    /// <summary>
    /// **Updates** the existing user-added translation of the announcement in
    /// <paramref name="languageCode"/> (ADR 0048):
    /// <c>POST /announcements/{id}/translations/update</c>. Thin Web lane;
    /// delegates to <see cref="IAnnouncementService
    /// .UpdateAnnouncementTranslationAsync"/>. Failure shapes mirror
    /// <see cref="AddTranslation"/> (denied → 403; missing → 404).
    /// </summary>
    [HttpPost("/announcements/{id}/translations/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTranslation(
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
            TempData["error"] = await this.FlashAsync("flash.translation_choose_language", "Choose a language for the translation.");
            return RedirectToAction("Detail", "Announcement", new { id });
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            TempData["error"] = await this.FlashAsync("flash.translation_needs_text", "A translation needs some text.");
            return RedirectToAction("Detail", "Announcement", new { id });
        }

        var a = await announcements.GetAsync(id, actorId, RoleSet(User));
        if (a is null)
            return NotFound();

        await using var session = store.LightweightSession();
        try
        {
            await announcements.UpdateAnnouncementTranslationAsync(
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

        var name = await SeedLanguageName(languageCode);
        TempData["info"] = await this.FlashAsync("flash.translation_updated", "Translation updated ({0}).", name);
        return RedirectToAction("Detail", "Announcement", new { id });
    }

    /// <summary>
    /// **Removes** the existing user-added translation of the announcement in
    /// <paramref name="languageCode"/> (ADR 0048):
    /// <c>POST /announcements/{id}/translations/remove</c>. Thin Web lane;
    /// delegates to <see cref="IAnnouncementService
    /// .RemoveAnnouncementTranslationAsync"/>. Failure shapes mirror
    /// <see cref="AddTranslation"/> (denied → 403; missing → 404).
    /// </summary>
    [HttpPost("/announcements/{id}/translations/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveTranslation(
        [FromRoute] string id, [FromForm] string? languageCode)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
            return new ForbidResult();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = await this.FlashAsync("flash.translation_choose_language", "Choose a language for the translation.");
            return RedirectToAction("Detail", "Announcement", new { id });
        }

        var a = await announcements.GetAsync(id, actorId, RoleSet(User));
        if (a is null)
            return NotFound();

        await using var session = store.LightweightSession();
        try
        {
            await announcements.RemoveAnnouncementTranslationAsync(
                id, languageCode, actorId, RoleSet(User), session);
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var name = await SeedLanguageName(languageCode);
        TempData["info"] = await this.FlashAsync("flash.translation_removed", "Translation removed ({0}).", name);
        return RedirectToAction("Detail", "Announcement", new { id });
    }

    // U05, IMPROVE lane — the composer trio is defined once in
    // <see cref="ComposerSeedOptions"/>; the per-controller stub that used to
    // live here is deleted (C11 — the call site calls the helper directly).

    /// <summary>
    /// The create-lane composer's <b>default authored-in language</b> (the
    /// picker's pre-selection): the actor's per-request **effective**
    /// language (ADR 0049 — the <c>kumunita.locale</c> cookie → first enabled
    /// <c>Accept-Language</c> match → instance default → <c>en</c> floor, the
    /// exact <see cref="EffectiveLanguageCode.ResolveAsync"/> chain the
    /// <c>&lt;kw-l&gt;</c> TagHelper resolves UI strings through) — "write in
    /// the language you're reading in", the ADR 0018 "instance default"
    /// pre-selection generalized. A null <see cref="ITranslationProvider"/>
    /// (test-construction site) falls back to the instance default — the
    /// legacy behavior, so the existing mock sites keep passing.
    /// </summary>

}
