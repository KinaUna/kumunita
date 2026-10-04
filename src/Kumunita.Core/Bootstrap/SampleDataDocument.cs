namespace Kumunita.Core.Bootstrap;

/// <summary>
/// The JSON document shape for the development sample data (ADR 0129). This is the single
/// source of truth <see cref="SampleDataSeeder"/> reads: the developer grows the mock
/// neighborhood — more accounts, posts, replies, events, translations, groups, tags, blog
/// pages — by editing <c>Data/sample-data.json</c> (embedded in the Kumunita.Core assembly),
/// with **no C# change required**. That is the point of the lane: a large, easily-extended
/// corpus to exercise search (ADR 0091 / ADR 0124), translation display, feeds, RSVPs, and
/// the directory, without the data bloating the seeder's source.
/// <para>
/// All cross-references are by the entities' stable, human-readable keys: accounts by
/// e-mail, tags by slug, groups by slug, and a blog child page by its parent's slug.
/// <see cref="SampleDataSeeder"/> resolves these to Marten document ids at seed time and
/// verifies every reference is known (fail-fast, so a typo in the JSON is a loud error, not
/// a silently-orphaned row).
/// <para>
/// <b>Timestamps</b> are expressed relative to seed time (<see cref="SampleDataSeeder"/>
/// anchors on <c>DateTimeOffset.UtcNow</c>): a non-negative <c>DaysAgo</c> offsets back from
/// "now", and an optional signed <c>HoursAfter</c> nudges the instant forward/back. Event
/// start/end use <c>StartDaysAhead</c> + <c>EndHoursAfterStart</c>. This keeps the JSON
/// self-describing (the demo always reads as "recent") and trivial to extend.
/// </summary>
internal sealed class SampleDataDocument
{
    /// <summary>Schema version (1 = the ADR 0129 shape). Bumped only if a field's meaning
    /// changes — additive field changes are not a bump.</summary>
    public int Version { get; set; } = 1;

    /// <summary>The component (community) ids marked <b>mandatory</b> (ADR 0012) so every
    /// verified resident is a member of every board and the flat Community-visible posts are
    /// visible to all of them. Rides the sanctioned <c>IUserInfoService.SetCommunityMandatoryAsync</c>
    /// write lane, exactly as the pre-ADR-0129 seeder did.</summary>
    public IReadOnlyList<string> MandatoryComponents { get; set; } = [];

    public IReadOnlyList<SampleAccount> Accounts { get; set; } = [];
    public IReadOnlyList<SampleModeratorAssignment> ModeratorAssignments { get; set; } = [];

    /// <summary>The <c>GuardianLink</c> rows (ADR 0028 §B / ADR 0038) — a guardian account
    /// supervising a child account (one or two guardians per child). Each row keys its two
    /// accounts by <c>e-mail</c> (<see cref="SampleGuardianLink.GuardianEmail"/> /
    /// <see cref="SampleGuardianLink.ChildEmail"/>), resolved to the <c>SubjectId</c>s at
    /// seed time; the seeder stores the <c>Active</c> row (a created-but-unlinked account can
    /// never exist — the <c>AddChild</c> formation-lane guarantee, G·4).</summary>
    public IReadOnlyList<SampleGuardianLink> GuardianLinks { get; set; } = [];
    public IReadOnlyList<SampleGroup> Groups { get; set; } = [];
    public IReadOnlyList<SampleTag> Tags { get; set; } = [];
    public IReadOnlyList<SampleAnnouncement> Announcements { get; set; } = [];
    public IReadOnlyList<SamplePost> Posts { get; set; } = [];
    public IReadOnlyList<SampleEvent> Events { get; set; } = [];
    public IReadOnlyList<SamplePage> Pages { get; set; } = [];
    public IReadOnlyList<SampleCommunityTranslation> CommunityTranslations { get; set; } = [];

    /// <summary>The <see cref="Kumunita.Core.Projects.ProjectGoal"/> goals (the <c>PL</c>
    /// "goals &amp; projects" lane, ADR 0086 D2) — the higher-level direction a
    /// <see cref="SampleProject"/> hangs off. Goals are authored in English (en-only; there
    /// is no <c>GoalTranslation</c> type). <see cref="SampleGoal.Title"/> is the stable
    /// cross-reference key (the "events keyed by English title" precedent) that
    /// <see cref="SampleProject.GoalTitle"/> resolves to a goal id at seed time.</summary>
    public IReadOnlyList<SampleGoal> Goals { get; set; } = [];

    /// <summary>The <see cref="Kumunita.Core.Projects.Project"/> projects (ADR 0086 D3) +
    /// their nested <see cref="Kumunita.Core.Projects.TodoItem"/> to-dos (ADR 0067). A
    /// project references its goal by <see cref="SampleProject.GoalTitle"/> (resolved to a
    /// goal id) and carries its to-dos inline (each a nested <see cref="SampleTodo"/>).</summary>
    public IReadOnlyList<SampleProject> Projects { get; set; } = [];

    /// <summary>The <see cref="Kumunita.Core.Projects.KanbanBoard"/> boards (ADR 0067 D1 /
    /// ADR 0031 WYSIWYG-board lane) + their nested <see cref="Kumunita.Core.Projects.KanbanLane"/>
    /// lanes (columns) and the <see cref="Kumunita.Core.Projects.TodoItem"/> cards placed on
    /// them (the <see cref="Kumunita.Core.Projects.BoardItemPlacement"/> rows). Each board keys
    /// its lanes by title and its cards by the to-do's stable <c>(projectTitle, todoTitle)</c>
    /// cross-reference (the "projects keyed by English title" precedent, extended to the
    /// to-do it sits on) so <see cref="SampleDataSeeder"/> resolves them to ids without a
    /// generated id. Authored in English; a board's title/description translations (the
    /// <see cref="Kumunita.Core.Projects.BoardTranslation"/> lane, ADR 0088) ride
    /// <see cref="SampleKanbanBoard.Translations"/>.</summary>
    public IReadOnlyList<SampleKanbanBoard> Boards { get; set; } = [];

    /// <summary>The <see cref="Kumunita.Core.Inventory.InventoryItem"/> inventory (ADR 0117)
    /// + its nested <see cref="Kumunita.Core.Inventory.InventoryCheckout"/> usage records
    /// (the append-only check-out / check-in history). Each item keys its checkouts by the
    /// borrower's <c>e-mail</c> + the checkout's <c>note</c> (create-if-missing on warm boot),
    /// and <see cref="SampleDataSeeder"/> resolves the borrower to a user id. The
    /// <see cref="SampleInventoryItem.CurrentHolderEmail"/> (the in-flight holder, the F1
    /// state) is set to the latest open checkout's borrower when present.</summary>
    public IReadOnlyList<SampleInventoryItem> InventoryItems { get; set; } = [];

    /// <summary>When <c>true</c>, the seeder enables the <c>LanguageCatalog</c> "da" (Danish)
    /// entry (it ships DISABLED, awaiting an admin's enable) so the demo's language selector
    /// carries the full en/de/fr/da set. Dev-only — this document only ever materializes under
    /// the Development ∧ first-boot gate.</summary>
    public bool EnableDanish { get; set; } = true;
}

/// <summary>A demo resident account (the EF/<c>identity</c> side + the <c>mt</c>
/// <c>Profile</c> side, including the M23 extended profile — ADR 0123). <see cref="Email"/>
/// is the business key (idempotent, create-if-missing).</summary>
internal sealed class SampleAccount
{
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>The elevated role to grant: <c>"GlobalAdmin"</c> / <c>"Moderator"</c> /
    /// <c>"Translator"</c>, or null for a plain resident. Resolved against
    /// <c>Roles.*</c> constants; the seeder verifies it is a known role.</summary>
    public string? Role { get; set; }

    /// <summary>The opt-in contact-block audience (ADR — e-mail visible to every signed-in
    /// resident). The <c>mt</c>-side write only.</summary>
    public bool ContactVisibility { get; set; }

    /// <summary>The resident's timezone override (ADR 0019); null = the platform default.</summary>
    public string? TimeZone { get; set; }

    /// <summary>The resident's date-format override (ADR 0020); null = the platform default.</summary>
    public string? DateFormat { get; set; }

    /// <summary>The M23 extended-profile bio (ADR 00123; rich Markdown, rendered read-only by
    /// the single <c>MarkdownRenderer</c> on the directory detail).</summary>
    public string? Bio { get; set; }

    /// <summary>The M23 author-set profile tags (ADR 00123 D1) — the <c>Tag</c> slugs this
    /// resident claims. Resolved to <c>Tag</c> ids at seed time.</summary>
    public IReadOnlyList<string> TagSlugs { get; set; } = [];

    /// <summary>The profile's *Visibility* audience (the M23 "audience for the detailed
    /// non-contact fields" shape). <c>"allResidents"</c> = every signed-in resident (the demo
    /// default, so bios/tags are discoverable); <c>"none"</c> = the empty self-only default;
    /// null = leave the seeder's default. Independent of <see cref="ContactVisibility"/>.</summary>
    public string? Visibility { get; set; }
}

/// <summary>A scoped-moderator governing scope (ADR 0003): which components
/// <see cref="SampleAccount"/> <c>UserEmail</c> moderates — the rows that mint the
/// <c>moderator:{id}</c> claims at sign-in.</summary>
internal sealed class SampleModeratorAssignment
{
    public string UserEmail { get; set; } = string.Empty;
    public string ComponentId { get; set; } = string.Empty;
    public int DaysAgo { get; set; }

    /// <summary>The grantor (defaults to the instance's admin when omitted — every seeded
    /// scope is admin-granted).</summary>
    public string? GrantedByEmail { get; set; }
}

/// <summary>A <c>GuardianLink</c> (ADR 0028 §B): <see cref="GuardianEmail"/> is the
/// guardian (the account that supervises) and <see cref="ChildEmail"/> the child account it
/// manages. Both are resolved to <c>SubjectId</c>s at seed time; the seeder stores the
/// <c>Active</c> row + its <c>guardian.create</c> audit (the <c>CreateGuardianLinkAsync</c>
/// formation shape, G·4). <see cref="DaysAgo"/> offsets <see cref="Kumunita.Core.UserInfo.GuardianLink.CreatedAt"/>
/// back from seed time (the established "recent" convention).</summary>
internal sealed class SampleGuardianLink
{
    public string GuardianEmail { get; set; } = string.Empty;
    public string ChildEmail { get; set; } = string.Empty;
    public int DaysAgo { get; set; }
}

/// <summary>A <c>Group</c> + its memberships + its name/description translations (ADR 0026).
/// <see cref="Slug"/> is the cross-reference key (used by
/// <see cref="SamplePost.GroupSlug"/>); it is not stored on the <c>Group</c> entity.</summary>
internal sealed class SampleGroup
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPrivate { get; set; }
    public string OwnerEmail { get; set; } = string.Empty;
    public IReadOnlyList<string> MemberEmails { get; set; } = [];
    public int DaysAgo { get; set; }
    public IReadOnlyList<SampleNameTranslation> Translations { get; set; } = [];
}

/// <summary>A <c>Tag</c> + its per-language display names (ADR 0044). <see cref="Slug"/> is the
/// language-neutral business key and the cross-reference key (used by
/// <see cref="SamplePost.TagSlugs"/> / <see cref="SampleAccount.TagSlugs"/>).</summary>
internal sealed class SampleTag
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CreatedByEmail { get; set; } = string.Empty;
    public int DaysAgo { get; set; }
    public IReadOnlyList<SampleNameTranslation> Translations { get; set; } = [];
}

/// <summary>A name (+ optional description) translation for a group or a community (ADR 0026)
/// and a tag's display name (ADR 0044 — <see cref="Description"/> is ignored for tags).</summary>
internal sealed class SampleNameTranslation
{
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>The translated name (optional — the base name is the fallback).</summary>
    public string? Name { get; set; }

    /// <summary>The translated description (optional — the base description is the fallback;
    /// ignored for tags).</summary>
    public string? Description { get; set; }

    public int? DaysAgo { get; set; }

    /// <summary>The actor who added the translation. Defaults to the instance's Translator
    /// account (the established translation-lane author) when omitted.</summary>
    public string? AuthorEmail { get; set; }
}

/// <summary>A <c>Announcement</c> (ADR 0029) + its translations. <see cref="Scope"/> is
/// <c>"public"</c> (world-visible, <see cref="AnnouncementScope.Public"/>) or
/// <c>"community"</c> (community-scoped, <see cref="AnnouncementScope.Community"/>);
/// <see cref="ComponentId"/> null = "all communities". Translations carry a title + body
/// (the <see cref="SampleContentTranslation"/> shape).</summary>
internal sealed class SampleAnnouncement
{
    public string AuthorEmail { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary><c>"public"</c> / <c>"community"</c> (see <see cref="AnnouncementScope"/>).</summary>
    public string Scope { get; set; } = "community";

    /// <summary>The community board this is scoped to; null = "all communities".</summary>
    public string? ComponentId { get; set; }

    /// <summary>Pinned to the top of the feed (the "Test Platform" / "Welcome" shape).</summary>
    public bool Pinned { get; set; }

    public int DaysAgo { get; set; }

    public IReadOnlyList<SampleContentTranslation> Translations { get; set; } = [];
}

/// <summary>A <c>Post</c> (community or group lane) + its replies + its translations (ADR
/// 0022/0027). A **group-lane** post names a <see cref="GroupSlug"/> (and leaves
/// <see cref="ComponentId"/> null); a **community** post names a <see cref="ComponentId"/>
/// (and leaves <see cref="GroupSlug"/> null) — the ADR 0013 lane exclusivity.</summary>
internal sealed class SamplePost
{
    public string? ComponentId { get; set; }
    public string? GroupSlug { get; set; }
    public string AuthorEmail { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>The audience shape: <c>"community"</c> (a Community-visible post — the M3
    /// default), <c>"empty"</c> (a group-lane post — owner ∪ member, no audience grants), or
    /// <c>"allResidents"</c>.</summary>
    public string Audience { get; set; } = "community";

    public IReadOnlyList<string> TagSlugs { get; set; } = [];
    public int DaysAgo { get; set; }
    public int? ModifiedDaysAgo { get; set; }

    public IReadOnlyList<SampleReply> Replies { get; set; } = [];
    public IReadOnlyList<SampleContentTranslation> Translations { get; set; } = [];
}

/// <summary>A <c>PostReply</c> + its translations (ADR 0022).</summary>
internal sealed class SampleReply
{
    public string AuthorEmail { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int? DaysAgo { get; set; }
    public int? HoursAfter { get; set; }
    public IReadOnlyList<SampleBodyTranslation> Translations { get; set; } = [];
}

/// <summary>A title+body translation of a post, announcement, event, or page (ADR 0022/0027/
/// 0029/0059). The authored-in base text is the parent's own <c>Title</c>/<c>Body</c> and is
/// never written here — only the non-English variants.</summary>
internal sealed class SampleContentTranslation
{
    public string LanguageCode { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string Body { get; set; } = string.Empty;
    public int? DaysAgo { get; set; }

    /// <summary>The actor who added the translation. Defaults to the instance's Translator
    /// account when omitted.</summary>
    public string? AuthorEmail { get; set; }
}

/// <summary>A body-only translation of a reply (ADR 0022) or a page (ADR 0039).</summary>
internal sealed class SampleBodyTranslation
{
    public string LanguageCode { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int? DaysAgo { get; set; }

    /// <summary>The actor who added the translation. Defaults to the instance's Translator
    /// account when omitted.</summary>
    public string? AuthorEmail { get; set; }
}

/// <summary>A published <c>Event</c> (IsDraft=false) + its RSVPs + its translations (ADR 0054/
/// 0059/0060). The <see cref="Translations"/> are exactly the ADR 0060 de/fr/da baselines the
/// warm-boot backfill re-applies (the "one registry, two lanes" shape).</summary>
internal sealed class SampleEvent
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ComponentId { get; set; }
    public string AuthorEmail { get; set; } = string.Empty;
    public int StartDaysAhead { get; set; }
    public int EndHoursAfterStart { get; set; }
    public string? Location { get; set; }
    public int? Capacity { get; set; }
    public string? Color { get; set; }
    public bool ReminderEnabled { get; set; } = true;
    public int? DaysAgo { get; set; }
    public bool ModifiedNow { get; set; }

    /// <summary>Display tag slugs (ADR 0125) — resolved to the <see cref="Tag"/> docs by slug.</summary>
    public IReadOnlyList<string> TagSlugs { get; set; } = [];

    public IReadOnlyList<SampleRsvp> Rsvps { get; set; } = [];
    public IReadOnlyList<SampleContentTranslation> Translations { get; set; } = [];
}

/// <summary>A resident's RSVP to an event (ADR 0054 §3.2). Last-write-wins, keyed per
/// (event, user); <see cref="Status"/> is <c>"Going"</c> / <c>"Maybe"</c> / <c>"No"</c>.</summary>
internal sealed class SampleRsvp
{
    public string UserEmail { get; set; } = string.Empty;

    /// <summary><c>"Going"</c> / <c>"Maybe"</c> / <c>"No"</c> (<see cref="RsvpStatus"/>).</summary>
    public string Status { get; set; } = "Going";

    /// <summary>Days back from seed time the RSVP was placed (optional — defaults to
    /// "now" when omitted; mirrors the original's <c>At = now</c> shape).</summary>
    public int? DaysAgo { get; set; }

    /// <summary>Optional signed hour nudge (a few hours before/after the <c>now</c> anchor).</summary>
    public int? HoursAfter { get; set; }
}

/// <summary>A <c>Page</c> (a resident blog node, <c>PageKind.User</c>) + its translations
/// (ADR 0039/0040). <see cref="Slug"/> is the path segment; <see cref="ParentSlug"/> names the
/// parent blog page (null = a root). <see cref="Audience"/> null = public (world-readable).</summary>
internal sealed class SamplePage
{
    public string Slug { get; set; } = string.Empty;
    public string? ParentSlug { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Kind { get; set; } = "User";
    public string AuthorEmail { get; set; } = string.Empty;
    public IReadOnlyList<string> TagSlugs { get; set; } = [];
    public int DaysAgo { get; set; }

    /// <summary>A title+body translation (ADR 0039 lane) — pages carry both, so the
    /// <see cref="SampleContentTranslation"/> shape (not the body-only one) is used.</summary>
    public IReadOnlyList<SampleContentTranslation> Translations { get; set; } = [];
}

/// <summary>A <c>Community</c> (component) name/description translation (ADR 0026) — the
/// sidebar/board label in a non-English language.</summary>
internal sealed class SampleCommunityTranslation
{
    public string ComponentId { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? DaysAgo { get; set; }
}

/// <summary>A <c>ProjectGoal</c> (the <c>PL</c> "goals &amp; projects" lane, ADR 0086 D2). A
/// goal is the organizing container a project hangs off — a direction, not a scheduled
/// thing (no dates, no draft state, no project back-pointer on the goal side). Authored in
/// English (en-only; there is no <c>GoalTranslation</c> type). <see cref="Title"/> is the
/// stable cross-reference key that <see cref="SampleProject.GoalTitle"/> resolves to a goal
/// id at seed time (the "events keyed by English title" precedent).</summary>
internal sealed class SampleGoal
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>The community board (a feed filter, never a gate); null = all communities.</summary>
    public string? ComponentId { get; set; }

    public string AuthorEmail { get; set; } = string.Empty;
    public int DaysAgo { get; set; }
}

/// <summary>A <c>Project</c> (ADR 0086 D3) + its nested to-dos (ADR 0067 D1). A project is
/// the unit of *managed* work — the goal it hangs off (<see cref="GoalTitle"/>, resolved to
/// a goal id; null = a standalone project) and the to-dos associated to it (each a nested
/// <see cref="SampleTodo"/>). <see cref="Status"/> is a plain state-label string (the
/// <c>KanbanStatuses</c> vocabulary, <c>null</c> = none — never an enum).</summary>
internal sealed class SampleProject
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>The goal this hangs off, named by its title (the <c>SampleGoal.Title</c>
    /// cross-reference key). Null / empty = a standalone project (no goal).</summary>
    public string? GoalTitle { get; set; }

    /// <summary>The community board (a feed filter, never a gate); null = all communities.</summary>
    public string? ComponentId { get; set; }

    public string AuthorEmail { get; set; } = string.Empty;

    /// <summary>The state label (<c>not-started</c> / <c>in-progress</c> / <c>done</c> /
    /// <c>cancelled</c>); null / empty = none.</summary>
    public string? Status { get; set; }

    /// <summary>Days back from seed time the project started (optional).</summary>
    public int? StartDaysAgo { get; set; }

    /// <summary>Days from seed time the project is due (optional; negative = overdue).</summary>
    public int? DueInDays { get; set; }

    public int DaysAgo { get; set; }

    /// <summary>Project title/description translations (the ADR 0088 lane). The current sample
    /// corpus is English-only, so this is empty — declared for generality (a no-op loop when
    /// absent).</summary>
    public IReadOnlyList<SampleContentTranslation> Translations { get; set; } = [];

    /// <summary>The to-dos associated to this project (each a nested <see cref="SampleTodo"/>).</summary>
    public IReadOnlyList<SampleTodo> Todos { get; set; } = [];
}

/// <summary>A <c>TodoItem</c> nested under a <see cref="SampleProject"/> (ADR 0067 D1). The
/// to-do is the work item — status, assignee, due date. <see cref="Status"/> is a plain
/// state-label string (the <c>KanbanStatuses</c> vocabulary, <c>null</c> = none).</summary>
internal sealed class SampleTodo
{
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }

    /// <summary>The state label (<c>not-started</c> / <c>in-progress</c> / <c>done</c> /
    /// <c>cancelled</c>); null / empty = none.</summary>
    public string? Status { get; set; }

    /// <summary>The assignee (a SubjectId — display + standing, never a gate); null =
    /// unassigned. Resolved to a user id by e-mail at seed time.</summary>
    public string? AssigneeEmail { get; set; }

    /// <summary>Days from seed time the to-do is due (negative = overdue).</summary>
    public int? DueInDays { get; set; }

    /// <summary>Days back from seed time the to-do was created.</summary>
    public int DaysAgo { get; set; }
}

/// <summary>A <see cref="Kumunita.Core.Projects.KanbanBoard"/> (ADR 0067 D1) + its nested
/// <see cref="SampleKanbanLane"/> columns (the <see cref="Kumunita.Core.Projects.KanbanLane"/>
/// rows) and the to-dos placed on it (the <see cref="Kumunita.Core.Projects.BoardItemPlacement"/>
/// rows, each a nested <see cref="SampleBoardCard"/>). A board is a container with its own
/// standing owner (<see cref="AuthorEmail"/>, the C-M5·6 shape) and a feed filter
/// (<see cref="ComponentId"/> — a filter, never a gate). Authored in English; the
/// <see cref="Kumunita.Core.Projects.BoardTranslation"/> title/description translations (the
/// ADR 0088 lane) ride <see cref="Translations"/> (the <see cref="SampleContentTranslation"/>
/// shape, body-optional — boards are title-usable without a body).</summary>
internal sealed class SampleKanbanBoard
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>The community board (a feed filter, never a gate); null = all communities.</summary>
    public string? ComponentId { get; set; }

    /// <summary>The project this board is associated with (a feed filter, never a gate — the
    /// ADR 0086 D4 <c>projectId</c> shape), named by its <c>SampleProject.Title</c>. Null = a
    /// standalone board (no project).</summary>
    public string? ProjectTitle { get; set; }

    public string AuthorEmail { get; set; } = string.Empty;
    public int DaysAgo { get; set; }

    /// <summary>The board's columns (the <c>KanbanLane</c> rows) — each a nested
    /// <see cref="SampleKanbanLane"/>, ordered by <see cref="SampleKanbanLane.Order"/>.</summary>
    public IReadOnlyList<SampleKanbanLane> Lanes { get; set; } = [];

    /// <summary>The to-dos placed on this board (the <c>BoardItemPlacement</c> rows) — each a
    /// nested <see cref="SampleBoardCard"/> carrying the to-do's <c>(projectTitle, todoTitle)</c>
    /// key + its lane title + its position within that lane.</summary>
    public IReadOnlyList<SampleBoardCard> Cards { get; set; } = [];

    /// <summary>The board's title/description translations (the ADR 0088 lane). The current
    /// sample corpus is English-only, so this is empty — declared for generality (a no-op loop
    /// when absent), mirroring the post/announcement/event/page/project translation lanes.</summary>
    public IReadOnlyList<SampleContentTranslation> Translations { get; set; } = [];
}

/// <summary>A column (lane) on a <see cref="SampleKanbanBoard"/> (the
/// <see cref="Kumunita.Core.Projects.KanbanLane"/> row). <see cref="Status"/> is the status a
/// to-do moved into the lane imparts (the <see cref="Kumunita.Core.Projects.KanbanStatuses"/>
/// vocabulary, <c>null</c> = imparts none — a plain string, never an enum);
/// <see cref="MaxItems"/> is an advisory capacity (<c>null</c> = no limit); <see cref="Order"/>
/// is the column's 0-based position within the board.</summary>
internal sealed class SampleKanbanLane
{
    public string Title { get; set; } = string.Empty;

    /// <summary>The status the lane imparts (<c>not-started</c> / <c>in-progress</c> /
    /// <c>done</c> / <c>cancelled</c>); null / empty = imparts none.</summary>
    public string? Status { get; set; }

    /// <summary>The lane's advisory capacity; null = no limit (refused not dropped, C-M5·5).</summary>
    public int? MaxItems { get; set; }

    /// <summary>The lane's 0-based column order within the board.</summary>
    public int Order { get; set; }
}

/// <summary>The placement of a <see cref="Kumunita.Core.Projects.TodoItem"/> on a
/// <see cref="SampleKanbanBoard"/> lane (the <see cref="Kumunita.Core.Projects.BoardItemPlacement"/>
/// row). The card is keyed by the to-do's stable <c>(ProjectTitle, TodoTitle)</c> cross-reference
/// (resolved to a <c>TodoItem</c> id at seed time) and its lane by <see cref="LaneTitle"/>
/// (matched against the board's <see cref="SampleKanbanLane"/> titles). A to-do may appear on a
/// board at most once (the <c>(TodoItemId, BoardId)</c> unique index) and sits at
/// <see cref="Order"/> within its lane.</summary>
internal sealed class SampleBoardCard
{
    /// <summary>The project the to-do belongs to (a <see cref="SampleProject.Title"/> — the
    /// to-do's <c>ProjectId</c> is inherited from its project).</summary>
    public string ProjectTitle { get; set; } = string.Empty;

    /// <summary>The to-do's title (a <see cref="SampleTodo.Title"/> within that project).</summary>
    public string TodoTitle { get; set; } = string.Empty;

    /// <summary>The lane title the card sits in (a <see cref="SampleKanbanLane.Title"/> on this
    /// board).</summary>
    public string LaneTitle { get; set; } = string.Empty;

    /// <summary>The card's 0-based order within its lane.</summary>
    public int Order { get; set; }
}

/// <summary>A <see cref="Kumunita.Core.Inventory.InventoryItem"/> (ADR 0117 D1) + its nested
/// <see cref="SampleInventoryCheckout"/> usage records (the append-only
/// <see cref="Kumunita.Core.Inventory.InventoryCheckout"/> set, the M16 "usage history"). The
/// item is the thing that can be checked out: a <see cref="Name"/>, an <see cref="OwnerKind"/>
/// (<c>shared</c> / <c>community</c> / <c>private</c> — a UI grouping + write-standing breadth,
/// never a read gate), an optional description, and its standing owner
/// (<see cref="AuthorEmail"/>). Authored in English (the M16 translation lane is deferred);
/// <see cref="CurrentHolderEmail"/> is the in-flight holder (the F1 state) — set to the latest
/// open checkout's borrower when one exists, otherwise the item is in the pool.</summary>
internal sealed class SampleInventoryItem
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The ownership kind: <c>shared</c> / <c>community</c> / <c>private</c> (ADR 0117
    /// D3 — a string label, a filter + standing-breadth, never a read gate).</summary>
    public string OwnerKind { get; set; } = "community";

    public string? Description { get; set; }

    /// <summary>The community board (a feed filter, never a gate — the Post.ComponentId shape);
    /// null = all communities.</summary>
    public string? ComponentId { get; set; }

    public string AuthorEmail { get; set; } = string.Empty;

    /// <summary>The in-flight holder (a SubjectId — the F1 state); null = in the pool / at
    /// home. Resolved to a user id by e-mail at seed time. When omitted, the seeder infers it
    /// from the latest open checkout (if any).</summary>
    public string? CurrentHolderEmail { get; set; }

    public int DaysAgo { get; set; }

    /// <summary>The usage records (the <c>InventoryCheckout</c> rows) — each a nested
    /// <see cref="SampleInventoryCheckout"/>; the append-only check-out / check-in history.</summary>
    public IReadOnlyList<SampleInventoryCheckout> Checkouts { get; set; } = [];
}

/// <summary>One <see cref="Kumunita.Core.Inventory.InventoryCheckout"/> usage record nested
/// under a <see cref="SampleInventoryItem"/> (ADR 0117 D4). The record says which borrower
/// (<see cref="BorrowerEmail"/>, resolved to a user id), when it went out
/// (<see cref="CheckedOutDaysAgo"/> back from seed time), and — when closed — when it came back
/// (<see cref="CheckedInDaysAgo"/> back; null = still open, the F1 witness). At most one open
/// record per item (the unique partial index on <c>(ItemId)</c> where <c>CheckedInAt IS NULL</c>),
/// so the sample item's records must carry at most one open row.</summary>
internal sealed class SampleInventoryCheckout
{
    /// <summary>The borrower (a <see cref="SampleAccount.Email"/> — display + standing, never a
    /// gate). Resolved to a user id at seed time.</summary>
    public string BorrowerEmail { get; set; } = string.Empty;

    /// <summary>Days back from seed time the item went out.</summary>
    public int CheckedOutDaysAgo { get; set; }

    /// <summary>Days back from seed time the item came back; null = still open (the F1 witness).</summary>
    public int? CheckedInDaysAgo { get; set; }

    /// <summary>An optional free-text note on the checkout.</summary>
    public string? Note { get; set; }
}
