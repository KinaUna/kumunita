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
