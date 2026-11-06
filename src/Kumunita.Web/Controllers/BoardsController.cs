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
/// The /projects/boards actions (C08, ADR 0287): board CRUD, lane
/// CRUD/reorder, card moves, cross-board todo moves/copy, and board
/// translations. Inherited from
/// <see cref="ProjectsBaseController"/> (shared fields + helpers).
/// </summary>
[Authorize]
public partial class BoardsController : ProjectsBaseController
{

    // C08 (ADR 0287) — the child forwards the full seam set to the
    // <see cref="ProjectsBaseController"/> base. The boards / todos sibling
    // seams are supplied by DI (all three are registered in
    // <c>DependencyInjection</c>); the board / todo actions in this controller
    // resolve against them. A test site may pass <c>null</c> for a seam it does
    // not exercise.
    public BoardsController(
        IProjectService projects,
        IUserInfoService userInfo, IGroupService groupService, IProfileSettingsService profileSettings,
        ILocalizationService localization,
        IDocumentStore store,
        IBoardService boards,
        ITodoService todos,
        ITranslationProvider? translationProvider = null,
        EffectiveTimezoneResolver? timezone = null,
        IEventService? events = null,
        ITagService? tags = null) : base(projects, userInfo, groupService, profileSettings, localization, store, boards, todos, translationProvider, timezone, events, tags)
    {
    }




    /// <summary>
    /// <c>GET /projects/boards</c> — the board feed (F2 — a to-do can be
    /// placed on several boards / appears on each; the service's
    /// <see cref="IProjectService.ListBoardsAsync"/>
    /// <c>CanSeeAsync(Read)</c> gate is the sole reader — FACES are the
    /// service's; the controller's <c>ForbidResult</c> / <c>NotFound</c>
    /// split is the C3 pin only). The <paramref name="componentId"/> query
    /// is a *filter, never a gate* (C-M3·2). Author + component display
    /// names are *read* lookups (never access decisions).
    /// </summary>
    [HttpGet("/projects/boards")]
    public async Task<IActionResult> BoardsIndex(string? componentId, int page = 1, string? sort = null, string? dir = null)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        // M26 U12 (C-SORT·3) — the boards surface's closed allowlist
        // (row 8: created/modified/title).
        var feedSort = ParseSort(sort, dir, ProjectFeedAllowedKeys);

        IReadOnlyList<KanbanBoard> boards;
        BoardPage boardsPage;
        // The resident's items-per-page preference (FeedPaging resolves
        // Profile.PageSize → PageSizer default/clamp); a no-actor read
        // uses the platform default. Passed to the seam (the Skip/Take
        // window) and the pager's PageSize.
        int pageSize = await FeedPaging.PageSizeAsync(userInfo, actorId);
        try
        {
            boardsPage = await this.boards.ListBoardsAsync(componentId, actorId, page, null, ct: HttpContext.RequestAborted, sort: feedSort, pageSize: pageSize); // ADR 0090 D1/D3 — the paging signal is the page's .HasMore.
            boards = boardsPage.Items;
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        // ADR 0088 / ADR 0051 — the feed translation swap: the viewer's
        // current-language translation of each board (title + description)
        // swaps in over the authored-in value (the Event feed idiom). A read
        // convenience (the audience decision already ran in ListBoardsAsync);
        // a no-op when no provider is wired.
        if (translationProvider is not null)
        {
            string effLang = await EffectiveLanguageCode.ResolveAsync(HttpContext?.Request, localization, translationProvider);
            foreach (var b in boards)
                await ApplyBoardTranslationAsync(b, effLang);
        }

        var authorIds = boards
            .Select(b => b.AuthorId)
            .Where(a => a is not null && a.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var subjectId in authorIds)
            names[subjectId] = await ResolveDisplayNameAsync(subjectId);

        var componentNames = await ResolveComponentNamesAsync();
        var rows = boards.Select(b => ProjectBoardRow(b, names, componentNames)).ToList();

        var vm = new BoardIndexViewModel(
            Boards: rows,
            Components: await ComposerSeedOptions.SeedComponentPickerAsync(profileSettings),
            CurrentComponentId: componentId,
            CurrentPage: page,
            // ADR 0086 D9 — the row dropdown's "Add to Project…" modal
            // picker (a display surface, never a gate — C-PL·3; empty ⇒
            // the item + modal hide, the BoardEdit F10 rule).
            Projects: await SeedProjectPickerAsync(),
            // M7 (ADR 0090 D5) — the pager (F2 one-page no-render pin): null on a
            // single page so the _Pager partial renders nothing. The community
            // filter is carried across prev/next (D7) as a FilterParams pair.
            // The M26 U12 (C-SORT·8) sort/dir pairs join the same set only when
            // the request carried them (C-SORT·3); an unsorted read stays
            // byte-identical to pre-M26 (C-SORT·2).
            Pager: (boardsPage.HasMore || page > 1)
                ? PagedViewModel.ForRoute("/projects/boards", page, pageSize, boardsPage.HasMore,
                    BuildBoardFilterParams(componentId, sort, dir))
                : null,
            // M26 U12 (D-SORT·5) — the one shared sort control (the U10
            // _Sort reference, reused verbatim — C-SORT·1). The boards
            // surface's closed allowlist (row 8: created/modified/title).
            Sort: SortViewModel.ForRoute(
                "/projects/boards",
                currentKey: feedSort?.Key,
                currentDir: feedSort is { } s ? (s.Descending ? "desc" : "asc") : null,
                options: [
                    ("created", "desc"),
                    ("modified", "desc"),
                    ("title", "asc"),
                ]));

        return View("BoardIndex", vm);
    }


    /// <summary>
    /// <c>GET /projects/boards/{id}</c> — the board detail (the **two-level
    /// decision**, C-M5·3 — F3: a board is gated by its own <c>Audience</c>;
    /// a to-do on the board is visible iff **both** are visible — the
    /// service's <see cref="IProjectService.GetBoardAsync"/> already ran the
    /// board's single <c>CanAsync(Read)</c> entry gate + each card's own
    /// <c>CanAsync(Read)</c>; a denied card is **not returned**, not just
    /// hidden). The card <see cref="BoardItemPlacement"/> rows are resolved
    /// via a read-only <see cref="IDocumentStore"/> query (a display
    /// convenience — the M4 <c>EventController</c> / U07
    /// <c>TodoDetail</c> idiom; never a gate). A missing board is 404, a
    /// denied actor 403 (the C3 split).
    /// </summary>
    [HttpGet("/projects/boards/{id}")]
    public async Task<IActionResult> BoardDetail(string id, string? view = null)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        BoardDetailResult result;
        try
        {
            result = await this.boards.GetBoardAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        // The card placements (BoardItemPlacement rows for this board) — a
        // read convenience, never a gate (the audience decision already ran
        // in GetBoardAsync's two-level decision, C-M5·3).
        IReadOnlyList<BoardItemPlacement> placements = [];
        await using (var session = store.QuerySession())
        {
            placements = await session.Query<BoardItemPlacement>()
                .Where(p => p.BoardId == id)
                .ToListAsync(HttpContext.RequestAborted);
        }
        var placementByKey = placements
            .ToDictionary(p => (p.LaneId, p.TodoItemId));

        // Collect all the subject ids (board author + all card assignees)
        // for display-name resolution (a read, never a decision).
        var allCards = result.Lanes.SelectMany(l => l.Cards).ToList();
        var subjectIds = new[] { result.Board.AuthorId }
            .Concat(allCards.Select(c => c.AssigneeId))
            .Where(a => a is not null && a.Length > 0)
            .Select(a => a!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var subjectId in subjectIds)
            names[subjectId] = await ResolveDisplayNameAsync(subjectId);

        var componentNames = await ResolveComponentNamesAsync();
        var groupNames = await ResolveGroupNamesAsync();
        var boardRow = ProjectBoardRow(result.Board, names, componentNames);

        var laneRows = new List<LaneDetailRow>();
        foreach (var laneDetail in result.Lanes)
        {
            var cards = laneDetail.Cards.Select(card =>
            {
                placementByKey.TryGetValue((laneDetail.Lane.Id, card.Id), out var p);
                // ADR 0087 D4 — the card's waiting-on chip: the access-scoped
                // `BlockerChip` the service resolved (the `CardBlockers` map —
                // an unreadable / absent / soft-deleted blocker degrades to
                // `Generic`, C-TBD·4); absent key = no blocker = `null`.
                result.CardBlockers.TryGetValue(card.Id, out var blocker);
                return new TodoCardRow(
                    PlacementId: p?.Id ?? string.Empty,
                    TodoId: card.Id,
                    Title: card.Title,
                    Status: card.Status,
                    AssigneeId: card.AssigneeId,
                    AssigneeDisplayName: ResolveAssigneeDisplayName(
                        card.AssigneeId, names, groupNames, componentNames),
                    Order: p?.Order ?? 0,
                    // ADR 0079 — the optional dates pass through verbatim
                    // (`null` = no date; the card gates the line on non-null).
                    StartAt: card.StartAt,
                    DueAt: card.DueAt,
                    Blocker: blocker,
                    // ADR 0106 — the "Details" expander surface: the body
                    // verbatim (the view renders it as markdown; a title-only
                    // to-do leaves it empty → the "no body" hint), and the
                    // community display name resolved from `ComponentId` via
                    // the enabled-component `componentNames` map (`null` when
                    // unscoped / unresolvable → the view omits that line).
                    Body: card.Body,
                    CommunityDisplayName: card.ComponentId is not null
                        && componentNames.TryGetValue(card.ComponentId, out var cn) ? cn : null);
            }).ToList();

            laneRows.Add(new LaneDetailRow(
                LaneId: laneDetail.Lane.Id,
                Title: laneDetail.Lane.Title,
                Status: laneDetail.Lane.Status,
                MaxItems: laneDetail.Lane.MaxItems,
                Order: laneDetail.Lane.Order,
                Cards: cards));
        }

        // ADR 0086 D9 — the **project link** (the D6 dangling-association
        // rule): resolve the board's `ProjectId` to a readable, non-deleted
        // project title for the detail view. Only when `ProjectId` is
        // non-empty is `GetProjectAsync` called (a `null` association is the
        // common case — no extra read); a soft-deleted target (404) or an
        // unreadable one (403) leaves both fields `null`, and the link is
        // omitted entirely — **not** a 404/403 for the board itself (C-PL·6 /
        // the display-surface rule: a read convenience, never an error).
        string? projectLinkId = null;
        string? projectLinkTitle = null;
        if (result.Board.ProjectId is { Length: > 0 })
        {
            try
            {
                var linked = await projects.GetProjectAsync(result.Board.ProjectId, actorId, HttpContext.RequestAborted);
                projectLinkId = linked.Id;
                projectLinkTitle = linked.Title;
            }
            catch (KeyNotFoundException)
            {
                // Soft-deleted project — the association dangles; the link is
                // hidden (C-PL·6), the board itself is unaffected.
            }
            catch (UnauthorizedAccessException)
            {
                // A project the actor may not read — a hidden link, not a 403
                // for the board (C-PL·3).
            }
        }

        // ADR 0115 D2 (M14 interlock carried to the board surface) — the
        // **event link**: resolve the board's `EventId` to a readable,
        // non-deleted event title for the detail view, through the **frozen**
        // <see cref="Kumunita.Core.Events.IEventService.GetAsync"/> (a
        // single <c>CanAsync(Read)</c> decision). The D2 dangling-safe rule:
        // a soft-deleted target (404) or an unreadable one (403) leaves all
        // three fields `null`, and the chip is omitted entirely — **not** a
        // 404/403 for the board itself (C-M14·2 — a read convenience, never an
        // error; the target's title / id are not leaked). Only when `EventId`
        // is non-empty is <c>GetAsync</c> called (a `null` association is the
        // common case — no extra read).
        string? eventLinkId = null;
        string? eventLinkTitle = null;
        if (events is not null && result.Board.EventId is { Length: > 0 })
        {
            try
            {
                var linked = await events.GetAsync(result.Board.EventId, actorId, HttpContext.RequestAborted);
                eventLinkId = linked.Id;
                eventLinkTitle = linked.Title;
            }
            catch (KeyNotFoundException)
            {
                // Soft-deleted event — the association dangles; the chip is
                // hidden (C-M14·2), the board itself is unaffected.
            }
            catch (UnauthorizedAccessException)
            {
                // An event the actor may not read — a hidden chip, not a 403
                // for the board (C-M14·2).
            }
        }

        // ADR 0088 — the board's user-added translations (the ADR 0027
        // chip-swap + ADR 0049 default-visible-variant + ADR 0022 add-form
        // shape). A "a read, not a decision" surface; the parent board's
        // single Read decision already ran in GetBoardAsync (C-M3·1).
        var boardTranslations = await this.boards.GetBoardTranslationsAsync(id);

        // ADR 0044 / 0147 — the board's tags, resolved to display names in
        // the viewer's language (the <see cref="PostsController"/> detail
        // idiom — <c>ListForActorAsync</c> already does the TagTranslation
        // lookup). A "read, not a decision" surface: the board's single Read
        // decision ran in GetBoardAsync above; a dangling TagId simply drops
        // (renders as nothing, not a 404). No <c>ITagService</c> (test
        // construction site) ⇒ empty list (no-op).
        var boardTagRows = new List<(string Slug, string DisplayedName)>();
        if (tags is not null && result.Board.TagIds.Count > 0)
        {
            var boardTagIds = result.Board.TagIds.ToHashSet(StringComparer.Ordinal);
            // The viewer's per-request effective language (ADR 0005 / ADR 0049)
            // so a tag's <c>TagTranslation</c> in that language resolves; null
            // (test-construction site) falls to the instance-default → en floor.
            var boardTagPreferred = await EffectiveLanguageCode.ResolvePreferredAsync(HttpContext?.Request, localization, translationProvider);
            var readable = await tags.ListForActorAsync(actorId, boardTagPreferred);
            boardTagRows = readable
                .Where(t => boardTagIds.Contains(t.Tag.Id))
                .Select(t => (Slug: t.Tag.Slug, DisplayedName: t.DisplayedName))
                .OrderBy(x => x.DisplayedName, StringComparer.Ordinal)
                .ThenBy(x => x.Slug, StringComparer.Ordinal)
                .ToList();
        }

        var vm = new BoardDetailViewModel(
            Board: boardRow,
            Lanes: laneRows,
            // ADR 0070 — the board-head ⋮ menu's "Edit board" item (creator
            // ∪ GlobalAdmin — the same standing the service's
            // UpdateBoardAsync server-enforces; a non-signed-in viewer is
            // never an author and never carries the role).
            CanEdit: !string.IsNullOrEmpty(actorId)
                     && (string.Equals(result.Board.AuthorId, actorId, StringComparison.Ordinal)
                         || RoleSet(User).Contains(Kumunita.Core.Identity.Roles.GlobalAdmin)),
            // ADR 0086 D9 — the project link (the D6 dangling rule above).
            ProjectId: projectLinkId,
            ProjectTitle: projectLinkTitle,
            // ADR 0115 D2 (M14 interlock carried to the board surface) — the
            // event link (the D2 dangling rule above).
            EventId: eventLinkId,
            EventTitle: eventLinkTitle,
            EventLinkPath: eventLinkId is not null ? $"/events/{eventLinkId}" : null,
            // ADR 0115 D3 (M14 interlock carried to the board surface) — the
            // "Link to event…" picker (a display surface, never a gate —
            // C-M14·4; empty ⇒ the card hides, the ADR 0086 D9 F10 rule). The
            // <see cref="SeedEventPickerAsync"/> seed offers the actor's own
            // events; the board's stored <c>EventId</c> prefills it.
            EventPicker: new BoardEventPicker(
                Options: await SeedEventPickerAsync(),
                CurrentEventId: result.Board.EventId),
            // ADR 0086 D9 — the ⋮ menu's "Add to Project…" modal picker
            // (a display surface, never a gate — C-PL·3; empty ⇒ the item
            // + modal hide, the BoardEdit F10 rule).
            Projects: await SeedProjectPickerAsync(),
            // ADR 0088 — the board's user-added translations (the ADR 0027
            // chip-swap + ADR 0049 default-visible-variant + ADR 0022 add-form
            // shape). A "a read, not a decision" surface; the parent board's
            // single Read decision already ran in GetBoardAsync (C-M3·1).
            // CanTranslate is the ADR 0088 display pin (creator ∪ Translator ∪
            // GlobalAdmin) — the real gate is the server-side re-check in the
            // Add/Update/Remove lanes at POST.
            Translations: boardTranslations,
            Languages: LanguageOptionsFrom(boardTranslations, await ComposerSeedOptions.SeedLanguagePickerAsync(localization), t => t.LanguageCode),
            CanTranslate: !string.IsNullOrEmpty(actorId)
                          && BoardTranslationPolicy.CanAddBoardTranslation(result.Board.AuthorId, actorId, RoleSet(User)),
            OriginalLanguageCode: result.Board.LanguageCode,
            Tags: boardTagRows,
            // ADR 0285 D6 — "list" is the only other value; anything else is
            // the board view (a display selector, never an error).
            ListView: string.Equals(view?.Trim(), "list", StringComparison.OrdinalIgnoreCase));
        // ADR 0071 (amendment) — the "Add subtask" modal offers an optional
        // assignee picker. Seed the standing assignee options (verified,
        // non-self profiles) the way the Create / BoardNew views do, so the
        // modal's <select> can read them from ViewData.
        // ADR 0074 — the card "Assign to…" modal reuses the same seeded
        // Audience_Users (people) + Audience_Groups (public groups) lists
        // and adds the instance's enabled components as the community
        // choices; all three are display options, never a gate (the
        // standing decision is the service's on write).
        await ComposerSeedOptions.SeedGrantPickerOptionsAsync(this, userInfo, groupService, profileSettings, includeAssignUsers: true);
        var communityOptions = (await profileSettings.GetComponentsAsync(enabledOnly: true))
            .Select(c => new GrantOption
            {
                Id = c.Id,
                Label = string.IsNullOrWhiteSpace(c.Name) ? c.Id : c.Name,
                Kind = "Community",
            })
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
        ViewData["Assign_Communities"] = communityOptions;

        // ADR 0195 — the card edit-modal surface. Two read surfaces (both
        // "a read, never a decision" — C-M5·3: every card's own Read decision
        // already ran in GetBoardAsync's two-level decision; the standing
        // write gate is the service's on the POST /projects/todos/{id} lane):
        //   (a) per-card prefill — each card's stored values (Title / Body /
        //       Status / AssigneeId / ParentId / BlockedByTodoId / StartAt /
        //       DueAt (wall-clock in the actor's zone, the TodoEditGet idiom)
        //       / ComponentId / LanguageCode + the current tag slugs, the
        //       data-tag-suggest-initial prefill shape);
        //   (b) board-level picker — the option lists shared by every card's
        //       modal (parent / blocker / community / language + the
        //       grant-picker users + groups seeded into ViewData above).
        // Both are display conveniences: a missing / unreadable value simply
        // yields an empty picker option (the same F10-hides-the-picker
        // rule the BoardEdit / BoardNew views apply).
        var actorId0 = SubjectId(User) ?? string.Empty;
        var zone0 = await ResolveZoneAsync();
        var prefills = new Dictionary<string, CardEditPrefill>(StringComparer.Ordinal);
        if (actorId0.Length > 0)
        {
            foreach (var laneDetail in result.Lanes)
            {
                foreach (var card in laneDetail.Cards)
                {
                    // ADR 0079 / ADR 0019 — the stored StartAt / DueAt are UTC
                    // instants; convert to wall-clock in the actor's effective
                    // zone so the modal's <c>datetime-local</c> shows the local
                    // time the author chose (a blank field on POST is the clear
                    // intent — the UpdateTodoRequest partial shape).
                    var slugs = await SeedExistingTagSlugsAsync(card.TagIds);
                    prefills[card.Id] = new CardEditPrefill(
                        TodoId: card.Id,
                        Title: card.Title,
                        Body: card.Body,
                        Status: card.Status,
                        AssigneeId: card.AssigneeId,
                        ParentId: card.ParentId,
                        BlockedByTodoId: card.BlockedByTodoId,
                        StartAt: card.StartAt is not null ? TimeZoneInfo.ConvertTime(card.StartAt.Value, zone0) : null,
                        DueAt:   card.DueAt   is not null ? TimeZoneInfo.ConvertTime(card.DueAt.Value,   zone0) : null,
                        ComponentId: card.ComponentId,
                        LanguageCode: card.LanguageCode ?? string.Empty,
                        TagSlugs: slugs);
                }
            }
        }
        vm = vm with
        {
            CardEditPrefills = prefills,
            CardEditPicker = new CardEditPicker(
                ParentOptions: await SeedTodoParentOptionsAsync(actorId0),
                BlockerOptions: await SeedBlockerPickerAsync(),
                Components: await ComposerSeedOptions.SeedComponentPickerAsync(profileSettings),
                Languages: await ComposerSeedOptions.SeedLanguagePickerAsync(localization),
                GrantUsers: (ViewData["Audience_Users"] as List<GrantOption>) ?? new List<GrantOption>(),
                GrantGroups: (ViewData["Audience_Groups"] as List<GrantOption>) ?? new List<GrantOption>()),
        };
        return View("BoardDetail", vm);
    }


    /// <summary>
    /// <c>GET /projects/boards/{id}/edit</c> — the board edit page (ADR 0070,
    /// amended by ADR 0098) — the board's own <c>Title</c> +
    /// <c>Description</c> + <c>Audience</c> editor (the
    /// <see cref="BoardUpdateModel"/> shape; the component / language are
    /// creation-time choices, not editable here). The board is
    /// loaded through the frozen seam's <see
    /// cref="IProjectService.GetBoardAsync"/> (the audience gate is the
    /// service's single entry <c>CanAsync(Read)</c>, C-M5·3) — a missing
    /// board is 404, a denied actor 403 (the C3 split). The standing
    /// decision (creator ∪ GlobalAdmin) is the service's on write; the view
    /// renders the form (the board-head ⋮ menu's "Edit board" item is
    /// <see cref="BoardDetailViewModel.CanEdit"/>-gated, the same matrix).
    /// The audience editor prefills from the stored
    /// <see cref="KanbanBoard.Audience"/> (ADR 0098 — a <c>null</c> stored
    /// audience is the default "everyone" shape) and the grant-picker option
    /// lists seed alongside the create lane's shape.
    /// </summary>
    [HttpGet("/projects/boards/{id}/edit")]
    public async Task<IActionResult> BoardEditGet(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        KanbanBoard board;
        try
        {
            board = (await this.boards.GetBoardAsync(id, actorId, HttpContext.RequestAborted)).Board;
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        var model = new BoardUpdateModel
        {
            Title = board.Title,
            Description = board.Description,
            // ADR 0098 — the board's **audience** editor prefilled from the
            // stored value (a `null` stored audience is the default "everyone"
            // shape — the <c>FromAudience(null)</c> default).
            Audience = AudienceEditorModel.FromAudience(board.Audience),
            // ADR 0086 D9 — the **project picker** (display surface, never a
            // gate — C-PL·3): prefill the current association + seed the
            // actor's readable, non-deleted project options (a `null`
            // association prefill posts blank = clear).
            ProjectId = board.ProjectId,
            Projects = await SeedProjectPickerAsync(),
            // ADR 0044 / 0147 — the tag-suggest input self-wires its own
            // `name="TagIds"` hidden field (a JSON array of the author's typed
            // labels); the edit prefill re-seeds the chips from the stored
            // tags' **slugs** (the <see cref="SeedExistingTagSlugsAsync"/>
            // → <c>ExistingTagSlugs</c> → <c>data-tag-suggest-initial</c>
            // shape). The bound <c>TagIds</c> stays null — the component's
            // hidden field is the form field on POST.
            TagIds = null,
            ExistingTagSlugs = await SeedExistingTagSlugsAsync(board.TagIds),
        };
        // The _GrantPickers partial reads these from ViewData (the M2/M3/M4
        // shared shape — the create lane's precedent).
        await ComposerSeedOptions.SeedGrantPickerOptionsAsync(this, userInfo, groupService, profileSettings, includeAssignUsers: true);
        ViewData["boardId"] = id; // the edit form's POST action (POST /projects/boards/{id}).
        return View("BoardEdit", model);
    }


    /// <summary>
    /// <c>POST /projects/boards/{id}</c> — the board update write lane (ADR
    /// 0070, amended by ADR 0098 — the service's <see
    /// cref="IProjectService.UpdateBoardAsync"/>: a full update of the
    /// board's <c>Title</c> + <c>Description</c>, a blank description
    /// clearing it to <c>null</c>, + the posted audience editor written
    /// verbatim). **Creator ∪
    /// GlobalAdmin** over the board (C-M5·6) — the service's server-side
    /// standing gate; a missing id is 404, a denied actor 403 (the C3
    /// split); a blank title is a form error (the M4 "a form is a shape"
    /// precedent — re-render the edit view with the error). Redirect-after-
    /// POST back to the board.
    /// </summary>
    [HttpPost("/projects/boards/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BoardEditPost(string id, [FromForm] BoardUpdateModel model)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        // Re-seed the pickers so a failed-shape re-render below still shows
        // the project + grant options (the create lane's precedent).
        model.Projects = await SeedProjectPickerAsync();
        await ComposerSeedOptions.SeedGrantPickerOptionsAsync(this, userInfo, groupService, profileSettings, includeAssignUsers: true);

        if (!model.IsValid)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                ModelState.AddModelError(nameof(model.Title), "A title is required.");
            if (model.Audience is null || !model.Audience.IsValid)
                ModelState.AddModelError("Audience.Mode", "Audience mode is required (Any or All).");
            return View("BoardEdit", model);
        }

        var request = new UpdateBoardRequest
        {
            Title = model.Title!,
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description,
            // ADR 0098 — the posted audience is the actor's complete choice,
            // written verbatim (ADR 0001-B — the single deserialization site).
            Audience = model.Audience.BuildAudience(),
            // ADR 0044 / 0147 — the posted tag slugs (the tag-suggest hidden
            // field, a JSON array of the author's typed labels). `null` =
            // leave stored tags untouched (the tag-suggest form always posts
            // the field, so `null` is the "field absent" edge); `[]` = detach
            // all; non-empty = replace.
            TagSlugs = model.TagIds is null ? null : TagSlugParser.Parse(model.TagIds),
        };

        try
        {
            await this.boards.UpdateBoardAsync(id, actorId, RoleSet(User), request, HttpContext.RequestAborted);
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
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("BoardEdit", model);
        }

        TempData["info"] = await this.FlashAsync("projects.flash_board_updated", "Board updated.");
        return Redirect($"/projects/boards/{id}");
    }


    /// <summary>
    /// <c>POST /projects/boards/{id}/set-project</c> — the **project
    /// association** write lane (ADR 0086 D9 / C-PL·2). A **standalone** form
    /// POST (its own small form on the edit page — **not** the main board
    /// update form, whose frozen <see cref="Kumunita.Core.Projects
    /// .UpdateBoardRequest"/> is untouched). Sets the board's
    /// <see cref="Kumunita.Core.Projects.KanbanBoard.ProjectId"/> through the
    /// U04 <see cref="IProjectService.SetBoardProjectAsync"/> seam; a blank
    /// choice posts <c>null</c> = **clear** the association. **Creator ∪
    /// GlobalAdmin** over the board — the service's server-side standing
    /// re-check + project guard (C3 split) is the enforcement; the controller
    /// does no standing math (the ADR 0006-D split). A missing board is 404,
    /// a denied actor 403, a missing / unreadable project 404 / 403.
    /// </summary>
    [HttpPost("/projects/boards/{id}/set-project")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BoardSetProject(string id, [FromForm] string? projectId)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await this.boards.SetBoardProjectAsync(
                id,
                actorId,
                RoleSet(User),
                string.IsNullOrWhiteSpace(projectId) ? null : projectId,
                HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        TempData["info"] = string.IsNullOrWhiteSpace(projectId)
            ? await this.FlashAsync("projects.flash_project_cleared", "Project cleared.")
            : await this.FlashAsync("projects.flash_project_set", "Project set.");
        return Redirect($"/projects/boards/{id}");
    }


    /// <summary>
    /// <c>POST /projects/boards/{id}/set-event</c> — the **event
    /// association** write lane (ADR 0115 D3 / C-M14·3, the M14 interlock
    /// carried to the board surface — the <c>TodoSetEvent</c> shape verbatim,
    /// the <c>BoardSetProject</c> board-standing sibling). A **standalone**
    /// form POST (its own small form on the board detail — **not** the main
    /// board update form, whose frozen <see
    /// cref="Kumunita.Core.Projects.UpdateBoardRequest"/> is untouched).
    /// Sets the board's <see cref="Kumunita.Core.Projects.KanbanBoard
    /// .EventId"/> through the <see
    /// cref="IProjectService.SetBoardEventAsync"/> seam; a blank / <c>null</c>
    /// choice posts <c>null</c> = **clear** the association. **Creator ∪
    /// GlobalAdmin** over the board — the service's server-side standing
    /// re-check + event guard (C-M14·3) is the enforcement; the controller
    /// does no standing math (the ADR 0006-D split). A missing board is 404,
    /// a denied actor 403, a missing / unreadable event 404 (the seam's
    /// decision as truth, C-M14·3).
    /// </summary>
    [HttpPost("/projects/boards/{id}/set-event")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BoardSetEvent(string id, [FromForm] string? eventId)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await this.boards.SetBoardEventAsync(
                id,
                actorId,
                RoleSet(User),
                string.IsNullOrWhiteSpace(eventId) ? null : eventId,
                HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        TempData["info"] = string.IsNullOrWhiteSpace(eventId)
            ? await this.FlashAsync("projects.flash_event_link_cleared", "Event link cleared.")
            : await this.FlashAsync("projects.flash_event_linked", "Event linked.");
        return Redirect($"/projects/boards/{id}");
    }


    /// <summary>
    /// <c>GET /projects/boards/new</c> — the board composer (any signed-in
    /// resident becomes the author — §2.5 <c>Owner</c>). Seeds the audience
    /// editor (the M2 single-source pin, ADR 0001-B — the sole access
    /// boundary), the authored-in language picker (ADR 0018), the component
    /// feed-organizer picker (C-M3·2), and the grant-picker option lists
    /// (the M2/M3/M4 shared <c>_GrantPickers</c> partial).
    /// </summary>
    [HttpGet("/projects/boards/new")]
    public async Task<IActionResult> BoardCreateGet()
    {
        var model = new BoardEditorModel
        {
            // ADR 0106 — the new-board default audience is "Everyone in this
            // community" (the ADR 0036 community-visible flag, the Posts
            // precedent): checked by default so a board is visible to every
            // signed-in resident out of the box. A `null`/empty
            // `ComponentId` makes the ADR 0036 branch the whole decision
            // (any signed-in actor sees it — the resident-only standing), and
            // the "granular" audience panel (the _GrantPickers block) hides
            // while the flag is on, exactly as the view's copy promises ("By
            // default the board is visible to everyone; turn that off … only
            // if you want to narrow who can see it."). Without this seed the
            // flag is `false` + empty grants, which under `EvaluateAudience`
            // (an empty audience always denies) left a fresh board
            // author-only — contradicting the view's own "default is
            // everyone" text. The view's checkbox already renders from
            // `Model.Audience?.CommunityVisible ?? true`; seeding `true`
            // makes the rendered state and the submitted state agree.
            Audience = new AudienceEditorModel
            {
                Mode = "Any",
                Grants = "[]",
                CommunityVisible = true,
            },
            Languages = await ComposerSeedOptions.SeedLanguagePickerAsync(localization),
            // ADR 0018 / ADR 0049 — pre-select the actor's current effective
            // language so the picker highlights the right option and a
            // no-change submit is a concrete BCP-47 code (never an empty row).
            LanguageCode = await ResolveComposeDefaultLanguageAsync(),
            Components = await ComposerSeedOptions.SeedComponentPickerAsync(profileSettings),
            // ADR 0086 D9 — the **project picker** on the new form (a
            // display surface, never a gate — C-PL·3); a new board has no
            // association yet, so `ProjectId` stays `null` and the picker
            // offers a leading "no project" option.
            Projects = await SeedProjectPickerAsync(),
        };
        await ComposerSeedOptions.SeedGrantPickerOptionsAsync(this, userInfo, groupService, profileSettings, includeAssignUsers: true);
        return View("BoardNew", model);
    }


    /// <summary>
    /// <c>POST /projects/boards</c> — the board create write lane (any
    /// signed-in resident becomes the author; F8 — a refused write is a
    /// 403). Validates the shape (<see cref="BoardEditorModel.IsValid"/>),
    /// writes through <see cref="IProjectService.CreateBoardAsync"/> (the
    /// service opens its own write session + audit row, C3), redirects to
    /// the new board's <c>/projects/boards/{id}</c> (the M4 redirect-after-
    /// POST pattern). The audience's <see
    /// cref="AudienceEditorModel.BuildAudience()"/> is the single
    /// deserialization site (the M2 single-source pin, ADR 0001-B).
    /// </summary>
    [HttpPost("/projects/boards")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BoardCreatePost([FromForm] BoardEditorModel model)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
        {
            ModelState.AddModelError(string.Empty, "You must sign in to create a board.");
            return View("BoardNew", model);
        }

        // Re-seed the pickers so a failed-shape re-render below still shows
        // the language + component + grant + project options.
        model.Languages = await ComposerSeedOptions.SeedLanguagePickerAsync(localization);
        model.Components = await ComposerSeedOptions.SeedComponentPickerAsync(profileSettings);
        model.Projects = await SeedProjectPickerAsync();
        await ComposerSeedOptions.SeedGrantPickerOptionsAsync(this, userInfo, groupService, profileSettings, includeAssignUsers: true);

        if (!model.IsValid)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                ModelState.AddModelError(nameof(model.Title), "A title is required.");
            if (model.Audience is null || !model.Audience.IsValid)
                ModelState.AddModelError("Audience.Mode", "Audience mode is required (Any or All).");
            return View("BoardNew", model);
        }

        var request = new CreateBoardRequest
        {
            Title = model.Title!,
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description,
            ComponentId = string.IsNullOrWhiteSpace(model.ComponentId) ? null : model.ComponentId,
            Audience = model.Audience.BuildAudience(), // ADR 0001-B — the single deserialization site.
            LanguageCode = string.IsNullOrWhiteSpace(model.LanguageCode) ? null : model.LanguageCode,
            // ADR 0044 / 0147 — the author's typed tag labels (the tag-suggest
            // hidden field, a JSON array of labels) parsed to clean slugs
            // (the <see cref="Kumunita.Web.Security.TagSlugs"/> parse — null /
            // empty ⇒ no attach; non-empty ⇒ resolved to <c>Tag</c> ids by
            // the <c>AttachToBoardAsync</c> lane).
            TagSlugs = TagSlugParser.Parse(model.TagIds),
            Lanes = model.Lanes
                .Where(l => !string.IsNullOrWhiteSpace(l.Title))
                .Select((l, i) => new CreateLaneRequest
                {
                    Title = l.Title!,
                    Status = string.IsNullOrWhiteSpace(l.Status) ? null : l.Status,
                    MaxItems = l.MaxItems,
                    Order = l.Order ?? i,
                })
                .ToList(),
        };

        KanbanBoard board;
        try
        {
            board = await this.boards.CreateBoardAsync(actorId, RoleSet(User), request, HttpContext.RequestAborted);
        }
        catch (UnauthorizedAccessException)
        {
            ModelState.AddModelError(string.Empty, "You do not have permission to create a board.");
            return View("BoardNew", model);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        // ADR 0086 D9 — the **project association** on the new form. The
        // frozen `CreateBoardRequest` carries no `ProjectId` (Core is frozen
        // — C-PL·8 additive-only), so the association is applied **after**
        // the board exists, through the U04 <c>SetBoardProjectAsync</c> seam
        // (the same seam the edit form's standalone set-project form uses).
        // The picker is a **display surface, never a gate** (C-PL·3): the
        // actor is the creator (the standing re-check passes), and a project
        // that is soft-deleted (404) or unreadable (403) at write time — a
        // race past the seeded list — simply leaves the board created and
        // unassociated (no error is surfaced over an already-successful
        // create).
        if (!string.IsNullOrWhiteSpace(model.ProjectId))
        {
            try
            {
                await this.boards.SetBoardProjectAsync(
                    board.Id, actorId, RoleSet(User), model.ProjectId, HttpContext.RequestAborted);
            }
            catch (KeyNotFoundException)
            {
                // The chosen project was soft-deleted after the picker seeded
                // — the association dangles; the board is still created.
            }
            catch (UnauthorizedAccessException)
            {
                // The chosen project is unreadable at write time — the
                // association is not applied; the board is still created
                // (C-PL·3 — the picker is a display surface, never a gate).
            }
        }

        TempData["info"] = await this.FlashAsync("projects.flash_board_created", "Board created.");
        return Redirect($"/projects/boards/{board.Id}");
    }
}
