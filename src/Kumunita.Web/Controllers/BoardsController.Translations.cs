using System.Security.Claims;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Localization;
using Kumunita.Core.Projects;
using Kumunita.Core.Query;
using Kumunita.Core.Tags;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Localization;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/boards</c> board-translation lane — the three
/// user-added-translation actions (ADR 0026 group-community
/// name/description translations applied to a board). A partial
/// class of <see cref="BoardsController"/> extracted so the main
/// file stays under the 600 <c>cs_file</c> ceiling (C3-04).
/// </summary>
public partial class BoardsController
{
    public async Task<IActionResult> AddBoardTranslation(
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
            return Redirect($"/projects/boards/{id}");
        }
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
        {
            TempData["error"] = await this.FlashAsync("flash.translation_needs_title_or_text", "A translation needs a title or some text.");
            return Redirect($"/projects/boards/{id}");
        }

        try
        {
            await this.boards.GetBoardAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        // C2-02 (ADR 0288) — the shared write lane (M3: controller owns the
        // session, C3 one SaveChangesAsync); the descriptor normalizes title/body.
        await using var session = store!.LightweightSession();
        try
        {
            await this.boards.BoardTranslationLane.AddAsync(session, id, languageCode, title, body, actorId, RoleSet(User));
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
        return Redirect($"/projects/boards/{id}");
    }


    /// <summary>
    /// **Updates** the existing user-added translation of the board in
    /// <paramref name="languageCode"/> (ADR 0088, the ADR 0048 edit-lane
    /// shape): <c>POST /projects/boards/{id}/translations/update</c>. Thin Web
    /// lane; delegates to
    /// <see cref="IBoardService.BoardTranslationLane"/> (ADR 0288). Precondition +
    /// failure shapes mirror <see cref="AddBoardTranslation"/>.
    /// </summary>
    [HttpPost("/projects/boards/{id}/translations/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBoardTranslation(
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
            return Redirect($"/projects/boards/{id}");
        }
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
        {
            TempData["error"] = await this.FlashAsync("flash.translation_needs_title_or_text", "A translation needs a title or some text.");
            return Redirect($"/projects/boards/{id}");
        }

        try
        {
            await this.boards.GetBoardAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        // C2-02 (ADR 0288) — the shared write lane (M3: controller owns the
        // session, C3 one SaveChangesAsync); the descriptor normalizes title/body.
        await using var session = store!.LightweightSession();
        try
        {
            await this.boards.BoardTranslationLane.UpdateAsync(session, id, languageCode, title, body, actorId, RoleSet(User));
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
        return Redirect($"/projects/boards/{id}");
    }


    /// <summary>
    /// **Removes** the existing user-added translation of the board in
    /// <paramref name="languageCode"/> (ADR 0088, the ADR 0048 remove-lane
    /// shape): <c>POST /projects/boards/{id}/translations/remove</c>. Thin Web
    /// lane; delegates to
    /// <see cref="IBoardService.BoardTranslationLane"/> (ADR 0288). Failure
    /// shapes mirror <see cref="AddBoardTranslation"/>.
    /// </summary>
    [HttpPost("/projects/boards/{id}/translations/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveBoardTranslation(
        [FromRoute] string id,
        [FromForm] string? languageCode)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
            return new ForbidResult();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = await this.FlashAsync("flash.translation_choose_language", "Choose a language for the translation.");
            return Redirect($"/projects/boards/{id}");
        }

        try
        {
            await this.boards.GetBoardAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        // C2-02 (ADR 0288) — the shared write lane (M3 shape: the controller
        // owns the in-flight session; one SaveChangesAsync, C3).
        await using var session = store!.LightweightSession();
        try
        {
            await this.boards.BoardTranslationLane.RemoveAsync(session, id, languageCode, actorId, RoleSet(User));
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
        return Redirect($"/projects/boards/{id}");
    }
}
