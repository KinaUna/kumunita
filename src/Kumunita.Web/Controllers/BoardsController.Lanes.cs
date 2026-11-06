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
/// The <c>/boards</c> lane-management + todo-movement surface — the
/// lane CRUD (update/create/delete/move) and the todo-card
/// reordering actions (ADR 0067/0068/0069/0070 boards,
/// ADR 0074 board-lane, ADR 0100 todo-lane). A partial class of
/// <see cref="BoardsController"/> extracted so the main file stays
/// under the 600 <c>cs_file</c> ceiling (C3-04).
/// </summary>
public partial class BoardsController
{

    /// <summary>
    /// <c>POST /projects/boards/{id}/lanes/{laneId}</c> — the lane-update
    /// write lane (sets the lane's <c>Title</c> / <c>Status</c> /
    /// <c>MaxItems</c> / <c>Order</c> — the F4 / F5 / F6 FACES the lane's
    /// <c>Status</c> / <c>MaxItems</c> fields are the input to — the
    /// service's pins, the §2.3 pin). **Creator ∪ GlobalAdmin** over the
    /// board (C-M5·6) — the service's server-side standing gate; a missing
    /// id is 404, a denied actor 403 (the C3 split).
    /// </summary>
    [HttpPost("/projects/boards/{id}/lanes/{laneId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LaneUpdatePost(string id, string laneId, [FromForm] LaneEditorModel model)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            // The board Razor views land in U10 (no dedicated lane-edit view
            // in U08) — a malformed shape redirects back to the board with a
            // message (the M4 "a form is a shape" precedent).
            TempData["error"] = await this.FlashAsync("projects.flash_lane_title_required", "A lane title is required.");
            return Redirect($"/projects/boards/{id}");
        }

        var request = new UpdateLaneRequest
        {
            Title = string.IsNullOrWhiteSpace(model.Title) ? null : model.Title,
            Status = model.Status,
            MaxItems = model.MaxItems,
            Order = model.Order,
        };

        try
        {
            await this.boards.UpdateLaneAsync(laneId, actorId, RoleSet(User), request, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        TempData["info"] = await this.FlashAsync("projects.flash_lane_updated", "Lane updated.");
        return Redirect($"/projects/boards/{id}");
    }


    /// <summary>
    /// <c>POST /projects/boards/{id}/lanes/{laneId}/move-left</c> — the
    /// lane-reorder lane (ADR 0068 — the service's
    /// <see cref="IProjectService.MoveLaneAsync"/> <c>"left"</c> pin).
    /// **Creator ∪ GlobalAdmin** over the board (C-M5·6) — the service's
    /// server-side standing gate; a missing id is 404, a denied actor 403
    /// (the C3 split); an edge lane (no lane to the left) is a service no-op
    /// (nothing written — the redirect is a harmless round-trip).
    /// </summary>
    [HttpPost("/projects/boards/{id}/lanes/{laneId}/move-left")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveLaneLeftPost(string id, string laneId)
    {
        return await MoveLaneAsync(id, laneId, "left");
    }


    /// <summary>
    /// <c>POST /projects/boards/{id}/lanes/{laneId}/move-right</c> — the
    /// lane-reorder lane (ADR 0068 — the service's
    /// <see cref="IProjectService.MoveLaneAsync"/> <c>"right"</c> pin).
    /// </summary>
    [HttpPost("/projects/boards/{id}/lanes/{laneId}/move-right")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveLaneRightPost(string id, string laneId)
    {
        return await MoveLaneAsync(id, laneId, "right");
    }


    /// <summary>
    /// <c>POST /projects/boards/{id}/lanes</c> — the add-lane write lane
    /// (ADR 0069 — the service's
    /// <see cref="IProjectService.CreateLaneAsync"/>: a new lane appended at
    /// the end of the board, no status imparted, no limit). **Creator ∪
    /// GlobalAdmin** over the board (C-M5·6) — the service's server-side
    /// standing gate; a missing id is 404, a denied actor 403 (the C3
    /// split); a blank title is a form error (the M4 "a form is a shape"
    /// precedent).
    /// </summary>
    [HttpPost("/projects/boards/{id}/lanes")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LaneCreatePost(string id, [FromForm] string Title)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(Title))
        {
            TempData["error"] = await this.FlashAsync("projects.flash_lane_title_required", "A lane title is required.");
            return Redirect($"/projects/boards/{id}");
        }

        try
        {
            await this.boards.CreateLaneAsync(id, Title, actorId, RoleSet(User), HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (ArgumentException ex)
        {
            // A blank title (the service's 400) — a form error, not a 500.
            TempData["error"] = ex.Message;
            return Redirect($"/projects/boards/{id}");
        }

        TempData["info"] = await this.FlashAsync("projects.flash_lane_added", "Lane added.");
        return Redirect($"/projects/boards/{id}");
    }


    /// <summary>
    /// <c>POST /projects/boards/{id}/lanes/{laneId}/move</c> — the
    /// lane-reorder lane (ADR 0069 — the service's
    /// <see cref="IProjectService.MoveLaneToPositionAsync"/>: move the lane
    /// to a 0-based index, renumbering the board's lanes 0..n-1). **Creator
    /// ∪ GlobalAdmin** over the board (C-M5·6) — the service's server-side
    /// standing gate; a missing id is 404, a denied actor 403 (the C3 split);
    /// an out-of-range index is a service no-op (the clamp).
    /// </summary>
    [HttpPost("/projects/boards/{id}/lanes/{laneId}/move")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveLanePost(string id, string laneId, [FromForm] int index)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await this.boards.MoveLaneToPositionAsync(laneId, index, actorId, RoleSet(User), HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        TempData["info"] = await this.FlashAsync("projects.flash_lane_moved", "Lane moved.");
        return Redirect($"/projects/boards/{id}");
    }


    /// <summary>
    /// <c>POST /projects/boards/{id}/lanes/{laneId}/delete</c> — the
    /// lane-delete write lane (ADR 0097 — the service's
    /// <see cref="IProjectService.DeleteLaneAsync"/>: the lane + its card
    /// placements are removed, the to-dos are kept standalone (C-M5·2), and
    /// the board's remaining lanes are renumbered 0..n-1). **Creator ∪
    /// GlobalAdmin** over the board (C-M5·6) — the service's server-side
    /// standing gate; a missing id is 404, a denied actor 403 (the C3
    /// split).
    /// </summary>
    [HttpPost("/projects/boards/{id}/lanes/{laneId}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LaneDeletePost(string id, string laneId)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await this.boards.DeleteLaneAsync(laneId, actorId, RoleSet(User), HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        TempData["info"] = await this.FlashAsync("projects.flash_lane_deleted", "Lane deleted.");
        return Redirect($"/projects/boards/{id}");
    }


    /// <summary>
    /// <c>POST /projects/boards/{id}/lanes/{laneId}/todos</c> — the
    /// add-to-do-to-lane write lane (ADR 0068 — the service's
    /// <see cref="IProjectService.AddTodoToLaneAsync"/>: a new to-do placed
    /// on the lane at the end, the lane's <c>Status</c> imparted, the lane's
    /// <c>MaxItems</c> limit the refusal gate). **Creator ∪ GlobalAdmin**
    /// over the board (C-M5·6) — the service's server-side standing gate; a
    /// missing id is 404, a denied actor 403 (the C3 split); a lane-limit
    /// refusal or a blank title is a form error (the M4 "a form is a shape"
    /// precedent).
    /// </summary>
    [HttpPost("/projects/boards/{id}/lanes/{laneId}/todos")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTodoToLanePost(string id, string laneId, [FromForm] string Title)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await this.boards.AddTodoToLaneAsync(id, laneId, Title, actorId, RoleSet(User), HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (ArgumentException ex)
        {
            // A blank title (the service's 400) — a form error, not a 500
            // (the M4 "a form is a shape" precedent).
            TempData["error"] = ex.Message;
            return Redirect($"/projects/boards/{id}");
        }
        catch (InvalidOperationException ex)
        {
            // The lane-limit refusal (C-M5·5, F6) — a form error, not a 500.
            TempData["error"] = ex.Message;
            return Redirect($"/projects/boards/{id}");
        }

        TempData["info"] = await this.FlashAsync("events.flash_todo_added", "To-do added.");
        return Redirect($"/projects/boards/{id}");
    }


    /// <summary>
    /// <c>POST /projects/boards/{id}/delete</c> — the board soft-delete
    /// write lane (the cascade to the board's <see cref="KanbanLane"/> rows
    /// + <see cref="BoardItemPlacement"/> rows is the service's — the §2.3
    /// pin; F2: a to-do on a deleted board is still standalone — the
    /// placement rows are deleted, the to-do is untouched). **Creator ∪
    /// GlobalAdmin** over the board (C-M5·6) — the service's server-side
    /// standing gate; a missing id is 404, a denied actor 403 (the C3
    /// split).
    /// </summary>
    [HttpPost("/projects/boards/{id}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BoardDeletePost(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await this.boards.DeleteBoardAsync(id, actorId, RoleSet(User), HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        TempData["info"] = await this.FlashAsync("projects.flash_board_deleted", "Board deleted.");
        return Redirect("/projects/boards");
    }


    /// <summary>
    /// <c>POST /projects/boards/{boardId}/lanes/{laneId}/cards/{placementId}/
    /// move-up</c> — the within-lane reorder (F4 / F5 / F6 — the service's
    /// <see cref="IProjectService.MoveTodoWithinLaneAsync"/>
    /// <c>"up"</c> pin). **Creator ∪ assignee ∪ GlobalAdmin** over the
    /// to-do (C-M5·6) — the service's server-side standing gate; a missing
    /// placement is 404, a denied actor 403 (the C3 split); a lane-limit
    /// refusal is a form error (the M4 "a form is a shape" precedent).
    /// </summary>
    [HttpPost("/projects/boards/{boardId}/lanes/{laneId}/cards/{placementId}/move-up")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveUpPost(string boardId, string laneId, string placementId)
    {
        return await ReorderCardAsync(boardId, placementId, "up");
    }


    /// <summary>
    /// <c>POST /projects/boards/{boardId}/lanes/{laneId}/cards/{placementId}/
    /// move-down</c> — the within-lane reorder (F4 / F5 / F6 — the service's
    /// <see cref="IProjectService.MoveTodoWithinLaneAsync"/>
    /// <c>"down"</c> pin).
    /// </summary>
    [HttpPost("/projects/boards/{boardId}/lanes/{laneId}/cards/{placementId}/move-down")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveDownPost(string boardId, string laneId, string placementId)
    {
        return await ReorderCardAsync(boardId, placementId, "down");
    }


    /// <summary>
    /// <c>POST /projects/boards/{boardId}/lanes/{laneId}/cards/{placementId}/
    /// move-left</c> — the adjacent-lane change (F4 / F5 / F6 — the service's
    /// <see cref="IProjectService.MoveTodoToAdjacentLaneAsync"/>
    /// <c>"left"</c> pin).
    /// </summary>
    [HttpPost("/projects/boards/{boardId}/lanes/{laneId}/cards/{placementId}/move-left")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveLeftPost(string boardId, string laneId, string placementId)
    {
        return await AdjacentLaneAsync(boardId, placementId, "left");
    }


    /// <summary>
    /// <c>POST /projects/boards/{boardId}/lanes/{laneId}/cards/{placementId}/
    /// move-right</c> — the adjacent-lane change (F4 / F5 / F6 — the
    /// service's <see cref="IProjectService.MoveTodoToAdjacentLaneAsync"/>
    /// <c>"right"</c> pin).
    /// </summary>
    [HttpPost("/projects/boards/{boardId}/lanes/{laneId}/cards/{placementId}/move-right")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveRightPost(string boardId, string laneId, string placementId)
    {
        return await AdjacentLaneAsync(boardId, placementId, "right");
    }


    /// <summary>
    /// <c>POST /projects/boards/{id}/lanes/{targetLaneId}/cards/{placementId}/
    /// move</c> — the drag-drop move-to-position lane (ADR 0069 — the
    /// service's <see cref="IProjectService.MoveTodoToLanePositionAsync"/>:
    /// move the card into <c>targetLaneId</c> at a 0-based index, imparting
    /// the lane's <c>Status</c> when set (C-M5·4) and gate-limiting on a
    /// cross-lane move (C-M5·5); same-lane moves skip both). **Creator ∪
    /// assignee ∪ GlobalAdmin** over the to-do (C-M5·6) — the service's
    /// server-side standing gate; a missing placement/lane is 404, a denied
    /// actor 403 (the C3 split); a lane-limit refusal is a form error (the M4
    /// "a form is a shape" precedent).
    /// </summary>
    [HttpPost("/projects/boards/{id}/lanes/{targetLaneId}/cards/{placementId}/move")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveCardPost(string id, string targetLaneId, string placementId, [FromForm] int index)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await this.boards.MoveTodoToLanePositionAsync(placementId, targetLaneId, index, actorId, RoleSet(User), HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (InvalidOperationException ex)
        {
            // The lane-limit refusal (C-M5·5, F6) on a cross-lane move — a
            // form error, not a 500.
            TempData["error"] = ex.Message;
            return Redirect($"/projects/boards/{id}");
        }

        TempData["info"] = await this.FlashAsync("projects.flash_card_moved", "Card moved.");
        return Redirect($"/projects/boards/{id}");
    }
}
