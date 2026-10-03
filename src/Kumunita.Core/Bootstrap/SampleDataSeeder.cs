using System.Text.Json;
using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.Inventory;
using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
using Kumunita.Core.Projects;
using Kumunita.Core.Tags;
using Kumunita.Core.UserInfo;
using Marten;
using Marten.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Kumunita.Core.Bootstrap;

/// <summary>
/// <b>Development-only sample data</b> (a mock neighborhood). Runs on a <b>pristine</b>
/// database — the same outer <see cref="DbBootstrap.IsPristineAsync"/> gate that
/// <see cref="FirstBootSeeder"/> runs under — so a fresh
/// <c>docker compose down -v && docker compose up --build</c> (dev) or a fresh
/// deployed demo instance (ADR 0056) comes up immediately populated with accounts
/// (a scoped moderator, a translator, several verified residents), groups,
/// announcements, community + group posts with replies, published events with RSVPs,
/// tags, and a resident blog. It is gated on <c>SampleData__Enabled</c> <b>and</b> the
/// pristine-DB check (see <c>Program.cs</c>, ADR 0056): a real deployment never carries
/// the flag, so the seeder is unreachable by construction. In the deploy posture (a
/// <c>Production</c> instance with the flag set) the demo accounts get random
/// high-entropy passwords handed to the seed admin by e-mail through the durable
/// outbox, and the admin keeps its <c>SeedAdmin__</c> token lane (no weak credential
/// is stored on a public instance).
/// <para>
/// <b>Data lives in an embedded JSON document (ADR 0129).</b> The mock neighborhood is
/// described by the single <see cref="SampleDataDocument"/> in
/// <c>Data/sample-data.json</c> (embedded in this assembly, <see cref="LoadDocument"/>).
/// A developer grows the corpus — more accounts, posts, replies, events, translations,
/// groups, tags, blog pages — by editing that one file, with **no C# change**; this class
/// is a generic materializer that resolves the file's human-readable cross-references
/// (accounts by e-mail, tags/groups by slug, blog pages by parent slug) to Marten
/// document ids and stores the documents. The <see cref="EventTranslationBaselines"/>
/// the warm-boot backfill (<see cref="BackfillEventTranslationsAsync"/>) reads are sourced
/// from the same file, so a fresh and a backfilled instance agree on the de / fr / da
/// rows (the ADR 0060 D1 "one registry, two lanes" shape).
/// <para>
/// <b>Idempotent + pristine-gated.</b> Every step is a create-if-missing no-op and the
/// pristine outer gate keeps it from touching a warm database. No e-mail is staged except
/// in the deploy posture (a credentials summary to the seed admin).
/// <para>
/// <b>Session discipline (invariant C3):</b> the EF / <c>identity</c>-side writes
/// (accounts, roles, passwords, base profiles) commit per <see cref="UserManager"/>
/// call; every <c>mt</c>-side document (extended profiles, groups, content, translations)
/// is stored in a <b>single</b> <see cref="IDocumentSession"/> and committed once. The
/// component-mandatory flags ride the <see cref="IUserInfoService.SetCommunityMandatoryAsync"/>
/// write lane (its own session + audit row) because that is the single sanctioned writer
/// for <c>Component.Mandatory</c> (ADR 0012).
/// <para>
/// <b>Visibility model.</b> The seeded communities are marked <b>mandatory</b>
/// (ADR 0012), so <b>every verified resident is a member of every board</b> and the
/// default community-visible posts (<see cref="Audience.Community"/> = true) are visible
/// to all of them without per-account grant rows.
/// </summary>
public static class SampleDataSeeder
{
    // ── Demo credentials (Development-only; printed in the log on first boot so a
    //    developer can pick one up). All meet the app's password policy (Program.cs:
    //    RequiredLength = 8, RequireNonAlphanumeric = false). ────────────────────────
    public const string AdminEmail = "admin@examplium.com";
    public const string AdminPassword = "Admin123!";
    public const string ModeratorEmail = "moderator@examplium.com";
    public const string ModeratorPassword = "Mod1234!";
    public const string TranslatorEmail = "translator@examplium.com";
    public const string TranslatorPassword = "Trans123!";
    public const string ResidentPassword = "Resident123!";

    /// <summary>
    /// The code-owned, closed set of sample-account e-mail addresses (ADR 0078).
    /// Every account <see cref="SeedAsync"/> creates is identified by one of these
    /// addresses; the set is the single source of truth the notification
    /// suppression gate (<c>NotificationService.EmitAsync</c>,
    /// <c>NotificationOptions.SuppressForSampleAccountsInProduction</c>) compares a
    /// recipient's profile e-mail against in production. A recipient is a sample
    /// account when — and only when — their e-mail (case-insensitive, trimmed) is a
    /// member of this set. Sourced from the <c>accounts</c> list in the embedded
    /// sample-data document (ADR 0129) — the same file the seeder materializes, so the
    /// closed set and the seeded corpus cannot drift apart.
    /// </summary>
    public static readonly IReadOnlySet<string> SampleAccountEmails =
        new HashSet<string>(
            LoadDocument().Accounts.Select(a => a.Email),
            StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlySet<string> GlobalAdminRoles =
        new HashSet<string> { Roles.GlobalAdmin };

    private static string Id() => Guid.NewGuid().ToString("N");

    // ── The embedded sample-data document (ADR 0129) ─────────────────────────────────
    // A single, lazily-cached read of the embedded JSON. Every consumer (the closed
    // e-mail set, the event baselines, and SeedAsync itself) reads the SAME instance,
    // so the data is loaded once and the cross-references resolve identically.
    private static SampleDataDocument? _document;

    /// <summary>
    /// Load the sample-data document from the embedded resource
    /// (<c>Kumunita.Core.sample-data.json</c>, see <c>Kumunita.Core.csproj</c>).
    /// Cached after first read; a missing/undecodable resource is a hard, loud error —
    /// the seeder is a Development-only surface, so a broken file must fail fast rather
    /// than seed a silent partial neighborhood.
    /// </summary>
    private static SampleDataDocument LoadDocument()
    {
        if (_document is not null)
            return _document;

        var assembly = typeof(SampleDataSeeder).Assembly;
        using var stream = assembly.GetManifestResourceStream("Kumunita.Core.sample-data.json")
            ?? throw new InvalidOperationException(
                "The embedded sample-data resource 'Kumunita.Core.sample-data.json' is missing " +
                "from the Kumunita.Core assembly — check the EmbeddedResource item in Kumunita.Core.csproj.");

        // The JSON is camelCase; the POCOs are PascalCase — bind case-insensitively. Inlined
        // (not a static field) so this method is fully self-contained and correct regardless
        // of the static-field initialization order of its callers (e.g. SampleAccountEmails).
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var doc = JsonSerializer.Deserialize<SampleDataDocument>(stream, options)
            ?? throw new InvalidOperationException("The embedded sample-data document failed to deserialize.");

        // Fail-fast on an unknown elevated role (a typo in the JSON is a loud error, not a
        // silently-created bogus EF role). Inlined set — same reason as the options above.
        var knownElevated = new[] { Roles.GlobalAdmin, Roles.Moderator, Roles.Translator };
        foreach (var account in doc.Accounts)
        {
            if (account.Role is not null && !knownElevated.Contains(account.Role))
                throw new InvalidOperationException(
                    $"sample-data.json: account '{account.Email}' has an unknown elevated role " +
                    $"'{account.Role}' (expected one of GlobalAdmin, Moderator, Translator, or none).");
        }
        _document = doc;
        return doc;
    }

    /// <summary>
    /// The (language → title / body) baseline for one sample event — the de / fr / da
    /// text the sample neighborhood ships as <see cref="Kumunita.Core.Events.EventTranslation"/>
    /// rows (ADR 0059), seeded by <see cref="SeedAsync"/> and re-applied, create-if-missing,
    /// by <see cref="BackfillEventTranslationsAsync"/> (ADR 0060).
    /// </summary>
    public readonly record struct EventTranslationBaseline(string Code, string? Title, string Body);

    /// <summary>
    /// The de / fr / da translation baselines for the sample events, keyed by the event's
    /// **English title** (the seeder's stable, human-readable key — within the sample,
    /// events are identified by their content, not by id). Sourced from each event's
    /// <c>translations</c> in the embedded sample-data document (ADR 0129);
    /// <see cref="SeedAsync"/> and <see cref="BackfillEventTranslationsAsync"/> read the
    /// **same** set, so a fresh instance and a backfilled one carry identical de / fr / da
    /// rows (the ADR 0060 D1 "one registry, two lanes" shape).
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<EventTranslationBaseline>> EventTranslationBaselines { get; }
        = LoadDocument().Events
            .Where(e => e.Translations.Count > 0)
            .ToDictionary(
                e => e.Title,
                e => (IReadOnlyList<EventTranslationBaseline>)e.Translations
                    .Select(t => new EventTranslationBaseline(t.LanguageCode, t.Title, t.Body))
                    .ToList());

    /// <summary>
    /// Runs the sample-data steps. Called once by <c>Program.cs</c> on a pristine DB,
    /// only when <c>SampleData__Enabled</c> is set (ADR 0056). In the Development
    /// environment it takes the weak-credential posture; otherwise (a deployed demo
    /// site) it takes the deploy posture — random passwords + a credentials e-mail to
    /// the seed admin. The neighborhood's content is read from the embedded sample-data
    /// document (ADR 0129).
    /// </summary>
    public static async Task SeedAsync(
        AppDbContext identity,
        IDocumentStore mt,
        UserManager<User> userManager,
        RoleManager<IdentityRole> roleManager,
        IUserInfoService userInfo,
        IMailerStage? mailer = null,
        string? adminEmail = null,
        ILogger logger = default!,
        CancellationToken ct = default)
    {
        var doc = LoadDocument();

        // Two postures, one seeder (ADR 0056):
        //  · Development (mailer == null) — the documented weak demo credentials
        //    (README table), printed to the log; the seed admin also gets a weak demo
        //    password on top of its setup-token lane.
        //  · Deploy (mailer != null) — the seed admin stays on its <c>SeedAdmin__</c>
        //    setup-token lane (no weak password), the other demo accounts get random
        //    high-entropy passwords, and a single credentials summary is staged to the
        //    seed admin's e-mail through the durable outbox (below).
        bool deployPosture = mailer is not null;
        string adminAccountEmail = (deployPosture && adminEmail is not null) ? adminEmail : AdminEmail;

        logger.LogInformation(
            deployPosture
                ? "Sample data: seeding the mock neighborhood (deploy posture, pristine DB)."
                : "Development sample data: seeding the mock neighborhood (Development-only, pristine DB).");

        // ── 1. Accounts (the EF / identity side + the base mt Profile) ──────────────────
        // The seeded admin (FirstBootSeeder) already exists with a setup token but no
        // password — in Development EnsureUserAsync adds the demo password; in the deploy
        // posture a null password is a no-op, so the account keeps its token lane (no weak
        // credential). Every other demo account is created fresh. All are verified
        // residents (Member standing is implicit). Cross-references throughout the rest
        // of the seed key off the document's e-mail (stable business key), so the admin is
        // keyed by its *document* e-mail even when its real e-mail differs (deploy).
        var usersByEmail = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);
        var deployCredentials = new List<(string Email, string Password)>();
        foreach (var account in doc.Accounts)
        {
            bool isAdmin = account.Role == Roles.GlobalAdmin;
            string email = (isAdmin && deployPosture) ? adminAccountEmail : account.Email;
            string? password =
                deployPosture
                    ? (isAdmin ? null : RandomPassword())
                    : (isAdmin ? AdminPassword
                      : account.Role == Roles.Moderator ? ModeratorPassword
                      : account.Role == Roles.Translator ? TranslatorPassword
                      : ResidentPassword);
            if (!isAdmin && deployPosture && password is not null)
                deployCredentials.Add((account.Email, password));

            var user = await EnsureUserAsync(
                userManager, roleManager, mt,
                email, password, account.Name,
                contactVisibility: account.ContactVisibility,
                timeZone: account.TimeZone,
                dateFormat: account.DateFormat,
                elevatedRole: account.Role,
                logger: logger, ct: ct);
            usersByEmail[account.Email] = user;
        }

        var admin = usersByEmail[doc.Accounts.Single(a => a.Role == Roles.GlobalAdmin).Email];
        // The established translation-lane author (default author for every translation).
        var translator = doc.Accounts.FirstOrDefault(a => a.Role == Roles.Translator);
        string translationAuthorId = translator is not null ? usersByEmail[translator.Email].Id : string.Empty;

        // ── 2. Components are mandatory (ADR 0012) — every verified resident is a member
        //    of every board, so the flat `Community = true` posts are visible to all.
        //    Rides the sanctioned write lane (own session + audit row). ───────────────
        foreach (var componentId in doc.MandatoryComponents)
        {
            await userInfo.SetCommunityMandatoryAsync(componentId, true, admin.Id, GlobalAdminRoles);
        }

        // ── 3. All `mt`-side sample content — one session, one commit (invariant C3). ──
        var now = DateTimeOffset.UtcNow;
        string TranslateAuthorId(string? authorEmail)
            => authorEmail is not null ? usersByEmail[authorEmail].Id : translationAuthorId;

        await using var session = mt.OpenSession(new SessionOptions());

        // ── Tags (+ per-language display names) — labels, never gates (C-TG·1). ─────────
        // Tags come first: posts, profiles, and events all reference them by slug.
        var tagsBySlug = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in doc.Tags)
        {
            var id = Id();
            session.Store(new Tag
            {
                Id = id,
                Slug = tag.Slug,
                Name = tag.Name,
                LanguageCode = "en",
                CreatedBy = usersByEmail[tag.CreatedByEmail].Id,
                Created = now.AddDays(-tag.DaysAgo),
            });
            tagsBySlug[tag.Slug] = id;
            foreach (var tr in tag.Translations)
            {
                session.Store(new TagTranslation
                {
                    Id = Id(),
                    TagId = id,
                    LanguageCode = tr.LanguageCode,
                    Name = tr.Name ?? string.Empty,
                    AuthorId = TranslateAuthorId(tr.AuthorEmail),
                    Created = now.AddDays(-(tr.DaysAgo ?? 0)),
                });
            }
        }

        // ── Groups (+ memberships + name/description translations) ──────────────────────
        var groupsBySlug = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in doc.Groups)
        {
            var id = Id();
            var owner = usersByEmail[group.OwnerEmail];
            var groupCreated = now.AddDays(-group.DaysAgo);
            session.Store(new Group
            {
                Id = id,
                Name = group.Name,
                Description = group.Description,
                IsPrivate = group.IsPrivate,
                OwnerId = owner.Id,
                Created = groupCreated,
            });
            groupsBySlug[group.Slug] = id;
            foreach (var memberEmail in group.MemberEmails)
            {
                var member = usersByEmail[memberEmail];
                session.Store(new GroupMembership
                {
                    Id = Id(),
                    GroupId = id,
                    UserId = member.Id,
                    AddedBy = owner.Id,
                    At = groupCreated,
                });
            }
            foreach (var tr in group.Translations)
            {
                session.Store(new GroupTranslation
                {
                    Id = Id(),
                    GroupId = id,
                    LanguageCode = tr.LanguageCode,
                    Name = tr.Name,
                    Description = tr.Description,
                    AuthorId = TranslateAuthorId(tr.AuthorEmail),
                    Created = now.AddDays(-(tr.DaysAgo ?? 0)),
                });
            }
        }

        // ── The scoped moderator's governing scope (ADR 0003) — the rows that mint the
        //    `moderator:{id}` claims at sign-in (bootstrap writer posture). ─────────────
        foreach (var assignment in doc.ModeratorAssignments)
        {
            var moderator = usersByEmail[assignment.UserEmail];
            var grantedBy = assignment.GrantedByEmail is not null
                ? usersByEmail[assignment.GrantedByEmail].Id
                : admin.Id;
            session.Store(new ModeratorAssignment
            {
                Id = Id(),
                UserId = moderator.Id,
                ComponentId = assignment.ComponentId,
                GrantedBy = grantedBy,
                At = now.AddDays(-assignment.DaysAgo),
            });
        }

        // ── Extended profiles (bio + author-set tags + resident-visible audience, ADR
        //    0123) — loaded in this session over the base profile EnsureUserAsync stored.
        foreach (var account in doc.Accounts)
        {
            var subjectId = usersByEmail[account.Email].Id;
            var profile = await session.LoadAsync<Profile>(subjectId, ct);
            if (profile is null)
                continue; // pristine gate means all accounts exist; be safe.
            if (account.Bio is not null)
                profile.Bio = account.Bio;
            if (account.TagSlugs.Count > 0)
                profile.TagIds = [.. account.TagSlugs.Select(slug => tagsBySlug[slug])];
            // Open the *Visibility* audience to every signed-in resident so the
            // bio/tags are discoverable on the directory detail (the M23 demo shape —
            // independent of the ContactVisibility gate).
            profile.Visibility = new Audience(AudienceMode.Any, Array.Empty<AudienceGrant>()) { AllResidents = true };
            session.Store(profile);
        }

        // ── Announcements (+ translations) ───────────────────────────────────────────────
        foreach (var announcement in doc.Announcements)
        {
            var id = Id();
            session.Store(new Announcement
            {
                Id = id,
                AuthorId = usersByEmail[announcement.AuthorEmail].Id,
                Title = announcement.Title,
                Body = announcement.Body,
                Scope = ParseScope(announcement.Scope),
                CommunityId = announcement.ComponentId,
                Pinned = announcement.Pinned,
                LanguageCode = "en",
                Created = now.AddDays(-announcement.DaysAgo),
            });
            foreach (var tr in announcement.Translations)
            {
                session.Store(new AnnouncementTranslation
                {
                    Id = Id(),
                    AnnouncementId = id,
                    LanguageCode = tr.LanguageCode,
                    Title = tr.Title ?? string.Empty,
                    Body = tr.Body,
                    AuthorId = TranslateAuthorId(tr.AuthorEmail),
                    Created = now.AddDays(-(tr.DaysAgo ?? 0)),
                });
            }
        }

        // ── Community + group posts (+ replies + translations) ──────────────────────────
        foreach (var samplePost in doc.Posts)
        {
            var id = Id();
            bool groupLane = samplePost.GroupSlug is not null;
            session.Store(new Post
            {
                Id = id,
                ComponentId = groupLane ? string.Empty : (samplePost.ComponentId ?? string.Empty),
                GroupId = groupLane ? groupsBySlug[samplePost.GroupSlug!] : string.Empty,
                AuthorId = usersByEmail[samplePost.AuthorEmail].Id,
                Title = samplePost.Title,
                Body = samplePost.Body,
                Audience = ParsePostAudience(samplePost.Audience),
                Created = now.AddDays(-samplePost.DaysAgo),
                Modified = samplePost.ModifiedDaysAgo.HasValue ? now.AddDays(-samplePost.ModifiedDaysAgo.Value) : null,
                LanguageCode = "en",
                TagIds = [.. samplePost.TagSlugs.Select(slug => tagsBySlug[slug])],
            });
            foreach (var reply in samplePost.Replies)
            {
                var replyId = Id();
                session.Store(new PostReply
                {
                    Id = replyId,
                    PostId = id,
                    AuthorId = usersByEmail[reply.AuthorEmail].Id,
                    Body = reply.Body,
                    Created = now.AddDays(-(reply.DaysAgo ?? 0)).AddHours(reply.HoursAfter ?? 0),
                    LanguageCode = "en",
                });
                foreach (var tr in reply.Translations)
                {
                    session.Store(new ReplyTranslation
                    {
                        Id = Id(),
                        ReplyId = replyId,
                        LanguageCode = tr.LanguageCode,
                        Body = tr.Body,
                        AuthorId = TranslateAuthorId(tr.AuthorEmail),
                        Created = now.AddDays(-(tr.DaysAgo ?? 0)),
                    });
                }
            }
            foreach (var tr in samplePost.Translations)
            {
                session.Store(new PostTranslation
                {
                    Id = Id(),
                    PostId = id,
                    LanguageCode = tr.LanguageCode,
                    Title = tr.Title ?? string.Empty,
                    Body = tr.Body,
                    AuthorId = TranslateAuthorId(tr.AuthorEmail),
                    Created = now.AddDays(-(tr.DaysAgo ?? 0)),
                });
            }
        }

        // ── Published events (+ RSVPs + the ADR 0060 de/fr/da baselines) ─────────────────
        foreach (var sampleEvent in doc.Events)
        {
            var id = Id();
            var start = now.AddDays(sampleEvent.StartDaysAhead);
            session.Store(new Event
            {
                Id = id,
                Title = sampleEvent.Title,
                Body = sampleEvent.Body,
                ComponentId = sampleEvent.ComponentId,
                AuthorId = usersByEmail[sampleEvent.AuthorEmail].Id,
                Start = start,
                End = start.AddHours(sampleEvent.EndHoursAfterStart),
                Location = sampleEvent.Location,
                Capacity = sampleEvent.Capacity,
                Color = sampleEvent.Color,
                Audience = new Audience(AudienceMode.Any, Array.Empty<AudienceGrant>()) { AllResidents = true },
                ReminderEnabled = sampleEvent.ReminderEnabled,
                IsDraft = false,
                IsDeleted = false,
                LanguageCode = "en",
                TagIds = [.. sampleEvent.TagSlugs.Select(slug => tagsBySlug[slug])],
                Created = sampleEvent.DaysAgo.HasValue ? now.AddDays(-sampleEvent.DaysAgo.Value) : now,
                Modified = sampleEvent.ModifiedNow ? now : null,
            });
            foreach (var rsvp in sampleEvent.Rsvps)
            {
                session.Store(new EventRsvp
                {
                    Id = Id(),
                    EventId = id,
                    UserId = usersByEmail[rsvp.UserEmail].Id,
                    Status = ParseRsvpStatus(rsvp.Status),
                    At = now.AddDays(-(rsvp.DaysAgo ?? 0)).AddHours(rsvp.HoursAfter ?? 0),
                });
            }
            foreach (var tr in sampleEvent.Translations)
            {
                session.Store(new EventTranslation
                {
                    Id = Id(),
                    EventId = id,
                    LanguageCode = tr.LanguageCode,
                    Title = tr.Title,
                    Body = tr.Body,
                    AuthorId = TranslateAuthorId(tr.AuthorEmail),
                    Created = now.AddDays(-(tr.DaysAgo ?? 0)),
                });
            }
        }

        // ── A resident blog (PageKind.User) — the /blog feed surface (ADR 0040) ──────────
        var pagesBySlug = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var page in doc.Pages)
        {
            var id = Id();
            var parent = page.ParentSlug is not null ? pagesBySlug[page.ParentSlug] : null;
            var created = now.AddDays(-page.DaysAgo);
            session.Store(new Page
            {
                Id = id,
                ParentId = parent,
                Slug = page.Slug,
                Title = page.Title,
                Body = page.Body,
                Audience = null, // public (world-readable) — a blog page may be public
                Kind = ParsePageKind(page.Kind),
                AuthorId = usersByEmail[page.AuthorEmail].Id,
                ComponentId = null,
                LanguageCode = "en",
                Created = created,
                Modified = created,
                IsDraft = false,
                IsDeleted = false,
                TagIds = [.. page.TagSlugs.Select(slug => tagsBySlug[slug])],
            });
            pagesBySlug[page.Slug] = id;
            foreach (var tr in page.Translations)
            {
                session.Store(new PageTranslation
                {
                    Id = Id(),
                    PageId = id,
                    LanguageCode = tr.LanguageCode,
                    Title = tr.Title ?? string.Empty,
                    Body = tr.Body,
                    AuthorId = TranslateAuthorId(tr.AuthorEmail),
                    Created = now.AddDays(-(tr.DaysAgo ?? 0)),
                });
            }
        }

        // ── Goals → Projects → To-dos (the `PL` lane, ADR 0086; to-dos ADR 0067) ─────────
        // Materialized in this same session (invariant C3) so a single commit covers the
        // whole corpus. Cross-references resolve by the entities' stable keys — a goal is
        // keyed by its <c>Title</c> (the "events keyed by English title" precedent); a
        // project references its goal by <c>goalTitle</c>; to-dos are nested under their
        // project, so there is no cross-document id to resolve beyond goal → project.
        var goalsByTitle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var goal in doc.Goals)
        {
            var id = Id();
            session.Store(new ProjectGoal
            {
                Id = id,
                Title = goal.Title,
                Description = goal.Description,
                ComponentId = goal.ComponentId,
                AuthorId = usersByEmail[goal.AuthorEmail].Id,
                Audience = null, // public (a goal is world-readable in the demo — the `Audience = null` shape)
                IsDeleted = false,
                LanguageCode = "en",
                Created = now.AddDays(-goal.DaysAgo),
            });
            goalsByTitle[goal.Title] = id;
        }

        // Project / to-do id maps for the board placements below (the boards key their
        // cards by (projectTitle, todoTitle) — resolved against these).
        var projectsByTitle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var todosByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sampleProject in doc.Projects)
        {
            var id = Id();
            var goalId = sampleProject.GoalTitle is not null && goalsByTitle.TryGetValue(sampleProject.GoalTitle, out var g)
                ? g
                : null;
            var created = now.AddDays(-sampleProject.DaysAgo);
            session.Store(new Project
            {
                Id = id,
                Title = sampleProject.Title,
                Description = sampleProject.Description,
                GoalId = goalId,
                Status = sampleProject.Status,
                StartAt = sampleProject.StartDaysAgo.HasValue ? now.AddDays(-sampleProject.StartDaysAgo.Value) : null,
                DueAt = sampleProject.DueInDays.HasValue ? now.AddDays(sampleProject.DueInDays.Value) : null,
                ComponentId = sampleProject.ComponentId,
                AuthorId = usersByEmail[sampleProject.AuthorEmail].Id,
                Audience = null, // public (world-readable — the `Audience = null` shape)
                IsDeleted = false,
                LanguageCode = "en",
                Created = created,
                Modified = created,
            });
            projectsByTitle[sampleProject.Title] = id;
            // Project title/description translations (ADR 0088). The current corpus is
            // English-only (empty), so this is a no-op loop when absent — declared for
            // generality, mirroring the post/announcement/event/page translation lanes.
            foreach (var tr in sampleProject.Translations)
            {
                session.Store(new ProjectTranslation
                {
                    Id = Id(),
                    ProjectId = id,
                    LanguageCode = tr.LanguageCode,
                    Title = tr.Title,
                    Body = tr.Body,
                    AuthorId = TranslateAuthorId(tr.AuthorEmail),
                    Created = now.AddDays(-(tr.DaysAgo ?? 0)),
                });
            }
            // Nested to-dos (ADR 0067 D1) — the unit of *managed* work under the project.
            foreach (var todo in sampleProject.Todos)
            {
                var todoId = Id();
                session.Store(new TodoItem
                {
                    Id = todoId,
                    Title = todo.Title,
                    Body = todo.Body,
                    ComponentId = sampleProject.ComponentId,
                    ProjectId = id,
                    AuthorId = usersByEmail[sampleProject.AuthorEmail].Id,
                    AssigneeId = todo.AssigneeEmail is not null ? usersByEmail[todo.AssigneeEmail].Id : null,
                    Status = todo.Status,
                    StartAt = null,
                    DueAt = todo.DueInDays.HasValue ? now.AddDays(todo.DueInDays.Value) : null,
                    Audience = null, // public (world-readable — the `Audience = null` shape)
                    IsDeleted = false,
                    LanguageCode = "en",
                    TagIds = [],
                    ImageIds = [],
                    AttachmentIds = [],
                    Created = now.AddDays(-todo.DaysAgo),
                    Modified = null,
                });
                todosByKey[sampleProject.Title + "|" + todo.Title] = todoId;
            }
        }

        // ── Kanban boards (ADR 0067 D1) + lanes + to-do placements ──────────────────────
        // Boards are a container with their own standing owner + feed filter; their lanes
        // (columns) impart a status, and their cards are the BoardItemPlacement rows that
        // place an existing to-do on a lane. Cross-references resolve by stable keys: a
        // lane by its title, a card by (projectTitle, todoTitle) — the projectsByTitle /
        // todosByKey maps built above. A card naming an unknown to-do or lane is a loud
        // error (the D3 fail-fast), never a silently-orphaned placement.
        foreach (var sampleBoard in doc.Boards)
        {
            var boardId = Id();
            var projectId = sampleBoard.ProjectTitle is not null && projectsByTitle.TryGetValue(sampleBoard.ProjectTitle, out var pid)
                ? pid
                : null;
            var boardCreated = now.AddDays(-sampleBoard.DaysAgo);
            session.Store(new KanbanBoard
            {
                Id = boardId,
                Title = sampleBoard.Title,
                Description = sampleBoard.Description,
                ComponentId = sampleBoard.ComponentId,
                ProjectId = projectId,
                AuthorId = usersByEmail[sampleBoard.AuthorEmail].Id,
                Audience = null, // public (world-readable — the `Audience = null` shape)
                IsDeleted = false,
                LanguageCode = "en",
                Created = boardCreated,
                Modified = boardCreated,
            });
            // Lanes (columns) — ordered; a lane's visibility is the board's (C-M5·3).
            var lanesByTitle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var lane in sampleBoard.Lanes)
            {
                var laneId = Id();
                session.Store(new KanbanLane
                {
                    Id = laneId,
                    BoardId = boardId,
                    Title = lane.Title,
                    Status = lane.Status,
                    MaxItems = lane.MaxItems,
                    Order = lane.Order,
                    Created = boardCreated,
                    Modified = boardCreated,
                });
                lanesByTitle[lane.Title] = laneId;
            }
            // Board title/description translations (ADR 0088) — a no-op loop when the
            // corpus is English-only (the current shape), declared for generality.
            foreach (var tr in sampleBoard.Translations)
            {
                session.Store(new BoardTranslation
                {
                    Id = Id(),
                    BoardId = boardId,
                    LanguageCode = tr.LanguageCode,
                    Title = tr.Title,
                    Body = tr.Body,
                    AuthorId = TranslateAuthorId(tr.AuthorEmail),
                    Created = now.AddDays(-(tr.DaysAgo ?? 0)),
                });
            }
            // Cards — the to-do → lane placements (the BoardItemPlacement rows).
            foreach (var card in sampleBoard.Cards)
            {
                var cardKey = card.ProjectTitle + "|" + card.TodoTitle;
                if (!todosByKey.TryGetValue(cardKey, out var todoItemId))
                    throw new InvalidOperationException(
                        $"sample-data.json: board '{sampleBoard.Title}' card " +
                        $"({cardKey}) names a to-do that is not seeded under the project " +
                        $"'{card.ProjectTitle}'.");
                if (!lanesByTitle.TryGetValue(card.LaneTitle, out var laneId))
                    throw new InvalidOperationException(
                        $"sample-data.json: board '{sampleBoard.Title}' card " +
                        $"({cardKey}) names an unknown lane '{card.LaneTitle}'.");
                session.Store(new BoardItemPlacement
                {
                    Id = Id(),
                    TodoItemId = todoItemId,
                    BoardId = boardId,
                    LaneId = laneId,
                    Order = card.Order,
                    Created = boardCreated,
                    Modified = boardCreated,
                });
            }
        }

        // ── Inventory (ADR 0117) + check-out / check-in usage records ───────────────────
        // An item is the check-out-able thing (name + ownership kind + standing owner); its
        // usage history is the append-only InventoryCheckout record set (never a field on the
        // item). The item's CurrentHolderId (the F1 in-flight state) is the borrower of its
        // latest open checkout (CheckedInAt null), else null = in the pool. A record naming
        // an unknown borrower is a loud error (the D3 fail-fast), never an orphaned row.
        foreach (var sampleItem in doc.InventoryItems)
        {
            var itemId = Id();
            var itemCreated = now.AddDays(-sampleItem.DaysAgo);
            // Resolve the in-flight holder: the latest open checkout's borrower, or the
            // explicit override (the item is 'at home' when neither is present).
            string? currentHolder =
                sampleItem.CurrentHolderEmail is not null
                    ? usersByEmail[sampleItem.CurrentHolderEmail].Id
                    : sampleItem.Checkouts
                        .Where(c => c.CheckedInDaysAgo is null)
                        .OrderByDescending(c => c.CheckedOutDaysAgo)
                        .Select(c => usersByEmail[c.BorrowerEmail].Id)
                        .FirstOrDefault();
            session.Store(new InventoryItem
            {
                Id = itemId,
                Name = sampleItem.Name,
                OwnerKind = sampleItem.OwnerKind,
                Description = sampleItem.Description,
                ComponentId = sampleItem.ComponentId,
                AuthorId = usersByEmail[sampleItem.AuthorEmail].Id,
                Audience = null, // public (world-readable — the `Audience = null` shape)
                CurrentHolderId = currentHolder,
                IsDeleted = false,
                LanguageCode = "en",
                Created = itemCreated,
                Modified = itemCreated,
            });
            // The usage records (the append-only InventoryCheckout set).
            foreach (var co in sampleItem.Checkouts)
            {
                session.Store(new InventoryCheckout
                {
                    Id = Id(),
                    ItemId = itemId,
                    BorrowerId = usersByEmail[co.BorrowerEmail].Id,
                    CheckedOutAt = now.AddDays(-co.CheckedOutDaysAgo),
                    CheckedInAt = co.CheckedInDaysAgo.HasValue ? now.AddDays(-co.CheckedInDaysAgo.Value) : null,
                    Note = co.Note,
                });
            }
        }

        // ── Community (board) name/description translations (ADR 0026) ──────────────────
        foreach (var community in doc.CommunityTranslations)
        {
            session.Store(new CommunityTranslation
            {
                Id = Id(),
                ComponentId = community.ComponentId,
                LanguageCode = community.LanguageCode,
                Name = community.Name,
                AuthorId = translationAuthorId,
                Created = now.AddDays(-(community.DaysAgo ?? 0)),
            });
        }

        // ── Enable every catalog language for the demo (ADR 0005) ────────────────────────
        // Danish ships DISABLED (FirstBootSeeder seeds it awaiting an admin's enable);
        // the sample wants the full selector, so flip it on here. Dev-only: this session
        // only ever runs under the Development ∧ first-boot gate. Load-then-Store (idempotent).
        if (doc.EnableDanish)
        {
            var danish = await session.LoadAsync<LanguageCatalog>("da", ct);
            if (danish is not null && !danish.Enabled)
            {
                danish.Enabled = true;
                session.Store(danish);
            }
        }

        await session.SaveChangesAsync();

        // Deploy posture (ADR 0056): hand the demo credentials to the instance's admin
        // through the durable outbox (one OutboxEmail, idempotency-keyed) — the only place
        // they exist beyond the hashed EF store. No-op in Development (mailer is null).
        if (deployPosture && mailer is not null)
        {
            await using var emailSession = mt.OpenSession(new SessionOptions());
            await mailer.StageAsync(emailSession,
                idempotencyKey: $"sampledata:{admin.Id}",
                recipient: adminAccountEmail,
                subject: "Kumunita: demo-instance credentials",
                body: SampleDataCredentialsBody(deployCredentials),
                ct: ct);
            await emailSession.SaveChangesAsync();
            logger.LogInformation(
                "Sample data (deploy): credentials staged to the seed admin's outbox ({Admin}).",
                adminAccountEmail);
        }

        if (deployPosture)
        {
            logger.LogInformation(
                "Sample data (deploy posture): complete. Demo credentials were emailed to the " +
                "seed admin ({Admin}); the admin account keeps its SeedAdmin__ setup-token lane " +
                "(no weak credential stored).", adminAccountEmail);
        }
        else
        {
            logger.LogInformation(
                "Development sample data: complete. Demo accounts use the documented weak " +
                "credentials (admin {AdminPassword}, moderator {ModPassword}, translator {TransPassword}, " +
                "residents {ResPassword}).",
                AdminPassword, ModeratorPassword, TranslatorPassword, ResidentPassword);
        }
    }

    // ── Shape parsers (string → domain enum / audience) ─────────────────────────────────
    private static AnnouncementScope ParseScope(string scope)
        => scope.Equals("public", StringComparison.OrdinalIgnoreCase)
            ? AnnouncementScope.Public
            : AnnouncementScope.Community;

    private static Audience ParsePostAudience(string audience)
    {
        var grants = Array.Empty<AudienceGrant>();
        if (audience.Equals("allResidents", StringComparison.OrdinalIgnoreCase))
            return new Audience(AudienceMode.Any, grants) { AllResidents = true };
        if (audience.Equals("empty", StringComparison.OrdinalIgnoreCase))
            return new Audience(); // group-lane post: owner ∪ member, no audience grants
        return new Audience(AudienceMode.Any, grants) { Community = true }; // "community" (default)
    }

    private static PageKind ParsePageKind(string kind)
        => kind.Equals("user", StringComparison.OrdinalIgnoreCase) ? PageKind.User : PageKind.System;

    private static RsvpStatus ParseRsvpStatus(string status)
        => status switch
        {
            "Maybe" => RsvpStatus.Maybe,
            "No" => RsvpStatus.No,
            _ => RsvpStatus.Going,
        };

    /// <summary>
    /// **Warm-boot backfill** of the sample events' de / fr / da
    /// <see cref="Kumunita.Core.Events.EventTranslation"/> rows (ADR 0060). An instance whose
    /// first boot predates the sample event translations has the sample events (authored in
    /// <c>en</c>) but no de / fr / da rows, so a German / French / Danish-speaking resident
    /// sees only the English variant on the events feed and detail. This lane closes that gap
    /// the same way ADR 0047 D2 / ADR 0052 do for the canonical pages and the UI-string catalog.
    /// <para>
    /// **Create-if-missing only** (the ADR 0042 D1 invariant): an existing
    /// <c>(EventId, LanguageCode)</c> row is skipped, never refreshed — the Translator's
    /// in-app edit of a sample event's translation (the ADR 0059 lane) is never clobbered by a
    /// later deploy. The <c>en</c> authored-in row is the event's own title / body and is never
    /// read or written here.
    /// </para>
    /// <para>
    /// **Scope guard.** It runs only when <c>SampleData__Enabled</c> is set (the caller's gate,
    /// ADR 0056) and only touches events whose title is one of the sample events (the
    /// <see cref="EventTranslationBaselines"/> registry). On a real neighborhood — which never
    /// carries the flag — this method is unreachable by construction, exactly like
    /// <see cref="SeedAsync"/>. Idempotent: a second run finds every row it created on the first
    /// and skips (the ADR 0042 D1 skip path).
    /// </para>
    /// </summary>
    public static async Task BackfillEventTranslationsAsync(
        IDocumentSession session,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var allEvents = await session.Query<Event>()
            .Where(e => e.IsDeleted == false)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var (englishTitle, baselines) in EventTranslationBaselines)
        {
            // Match by the sample event's stable English title (the registry's key) —
            // within a sample neighborhood the events are identified by their content,
            // not by id. On a real neighborhood no sample event exists, so this is a
            // no-op for every key.
            var eventRow = allEvents.FirstOrDefault(e => e.Title == englishTitle);
            if (eventRow is null)
                continue;   // this sample event is absent — nothing to backfill for it.

            foreach (var baseline in baselines)
            {
                var existing = await session
                    .Query<EventTranslation>()
                    .Where(t => t.EventId == eventRow.Id && t.LanguageCode == baseline.Code)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);

                if (existing is null)
                {
                    session.Store(new EventTranslation
                    {
                        Id = Id(),
                        EventId = eventRow.Id,
                        LanguageCode = baseline.Code,
                        Title = baseline.Title,
                        Body = baseline.Body,
                        AuthorId = string.Empty,   // sample / platform content — no resident author
                        Created = now,
                    });
                }
                // else: skip — create-if-missing (never overwrite; ADR 0042 D1).
            }
        }

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// **Warm-boot backfill of the whole sample corpus** (ADR 0130 — the ADR 0060
    /// event-translation lane generalized to every sample surface). A sample instance
    /// whose first boot predates a later growth of the embedded <c>sample-data.json</c>
    /// (ADR 0129) has the *old* corpus but not the entries added since; this method adds
    /// the **missing** ones on every warm boot so a live demo instance catches up to the
    /// current file **without a destructive wipe**.
    /// <para>
    /// **Create-if-missing, never clobber (the ADR 0042 D1 invariant, carried verbatim
    /// from the ADR 0060 lane).** Every sample entity is matched by a stable natural key
    /// (Option A — no new column, no schema change, ADR 0086 / 0067 surface already
    /// registered): accounts by **e-mail**, tags by **slug**, groups / announcements /
    /// events / goals / projects by **title**, pages by **(parent, slug)**, posts by
    /// **(title, lane)** — the lane being <c>GroupId</c> for a group-lane post and
    /// <c>ComponentId</c> for a community post. Child rows (translations, RSVPs,
    /// memberships, nested to-dos) are matched by their parent id + their own natural
    /// key. A match is **skipped**; a miss is **created** with exactly the same field
    /// values <see cref="SeedAsync"/> would have used, so a fresh instance and a
    /// backfilled one converge to the same corpus.
    /// </para>
    /// <para>
    /// **Never touches a real neighborhood.** It runs only when <c>SampleData__Enabled</c>
    /// is set (the caller's gate, ADR 0056) and only creates rows for entities whose
    /// natural key is in the embedded document; on a real neighborhood — which never
    /// carries the flag, and even if it did, whose content has different titles — this is
    /// a no-op. Idempotent: a second boot finds every row the first created and skips.
    /// </para>
    /// <para>
    /// **Scope / caveat (the ADR 0130 D3 pin).** Natural keys are display strings, not
    /// DB-enforced business keys (the content entities carry no unique index — only the
    /// child/translation tables do). For the hand-authored hero content this is exact;
    /// for the generated bulk corpus, a *renamed* title reads as a new entry (it will be
    /// added, not matched) and a *title collision* with a non-sample row reads as
    /// "present" (it will be skipped). Both are acceptable on a closed demo corpus, and
    /// neither overwrites — the create-if-missing invariant holds in every case.
    /// </para>
    /// </summary>
    public static async Task BackfillSampleCorpusAsync(
        AppDbContext identity,
        IDocumentStore mt,
        UserManager<User> userManager,
        RoleManager<IdentityRole> roleManager,
        IUserInfoService userInfo,
        IMailerStage? mailer = null,
        string? adminEmail = null,
        ILogger logger = default!,
        CancellationToken ct = default)
    {
        var doc = LoadDocument();
        var now = DateTimeOffset.UtcNow;

        // ── 1. Accounts (create-if-missing, keyed by e-mail) ──────────────────────────
        // EnsureUserAsync is idempotent (FindByEmailAsync) and only adds a password to
        // an account that has none — so a warm re-run never overwrites an existing
        // credential, and it creates a *new* account only if the corpus grew past first
        // boot. The two postures (ADR 0056) mirror SeedAsync exactly: in Development a
        // newly-added account gets the documented weak demo password; in the deploy
        // posture the seed admin stays on its setup-token lane (a null password is a
        // no-op, so NO weak credential is ever written) and a newly-added non-admin
        // account gets a random high-entropy password.
        bool deployPosture = mailer is not null;
        var usersByEmail = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);
        foreach (var account in doc.Accounts)
        {
            bool isAdmin = account.Role == Roles.GlobalAdmin;
            string email = (isAdmin && deployPosture && adminEmail is not null) ? adminEmail : account.Email;
            string? password =
                deployPosture
                    ? (isAdmin ? null : RandomPassword())
                    : (isAdmin ? AdminPassword
                      : account.Role == Roles.Moderator ? ModeratorPassword
                      : account.Role == Roles.Translator ? TranslatorPassword
                      : ResidentPassword);
            var user = await EnsureUserAsync(
                userManager, roleManager, mt,
                email, password, account.Name,
                contactVisibility: account.ContactVisibility,
                timeZone: account.TimeZone,
                dateFormat: account.DateFormat,
                elevatedRole: account.Role,
                logger: logger, ct: ct);
            usersByEmail[account.Email] = user;
        }
        var admin = usersByEmail[doc.Accounts.Single(a => a.Role == Roles.GlobalAdmin).Email];
        var translator = doc.Accounts.FirstOrDefault(a => a.Role == Roles.Translator);
        string translationAuthorId = translator is not null ? usersByEmail[translator.Email].Id : string.Empty;
        string TranslateAuthorId(string? authorEmail)
            => authorEmail is not null ? usersByEmail[authorEmail].Id : translationAuthorId;

        // ── 2. Components mandatory (idempotent write lane; load-then-store) ───────────
        // SetCommunityMandatoryAsync is the sanctioned writer and is itself
        // create-if-missing / no-op-on-no-change, so re-asserting it on a warm boot is
        // safe (the pristine gate normally keeps SeedAsync off a warm DB; here we want
        // the warm path to be self-sufficient).
        foreach (var componentId in doc.MandatoryComponents)
        {
            await userInfo.SetCommunityMandatoryAsync(componentId, true, admin.Id, GlobalAdminRoles);
        }

        // ── 3. All `mt`-side sample content — one session, one commit (invariant C3) ───
        await using var session = mt.OpenSession(new SessionOptions());

        // Preload the current corpus once per type (the create-if-missing match set).
        var existingTags     = await session.Query<Tag>().ToListAsync(ct);
        var existingGroups   = await session.Query<Group>().ToListAsync(ct);
        var existingGroupsBy = await session.Query<GroupMembership>().ToListAsync(ct);
        var existingAnn      = await session.Query<Announcement>().ToListAsync(ct);
        var existingPosts    = await session.Query<Post>().ToListAsync(ct);
        var existingReplies  = await session.Query<PostReply>().ToListAsync(ct);
        var existingEvents   = await session.Query<Event>().ToListAsync(ct);
        var existingRsvps    = await session.Query<EventRsvp>().ToListAsync(ct);
        var existingPages    = await session.Query<Page>().ToListAsync(ct);
        var existingGoals    = await session.Query<ProjectGoal>().ToListAsync(ct);
        var existingProjects = await session.Query<Project>().ToListAsync(ct);
        var existingTodos    = await session.Query<TodoItem>().ToListAsync(ct);
        var existingTagTrans  = await session.Query<TagTranslation>().ToListAsync(ct);
        var existingGrpTrans  = await session.Query<GroupTranslation>().ToListAsync(ct);
        var existingAnnTrans  = await session.Query<AnnouncementTranslation>().ToListAsync(ct);
        var existingPostTrans = await session.Query<PostTranslation>().ToListAsync(ct);
        var existingReplTrans = await session.Query<ReplyTranslation>().ToListAsync(ct);
        var existingEvtTrans  = await session.Query<EventTranslation>().ToListAsync(ct);
        var existingPageTrans = await session.Query<PageTranslation>().ToListAsync(ct);
        var existingPrjTrans  = await session.Query<ProjectTranslation>().ToListAsync(ct);
        var existingCommTrans = await session.Query<CommunityTranslation>().ToListAsync(ct);
        var existingBoards    = await session.Query<KanbanBoard>().ToListAsync(ct);
        var existingLanes     = await session.Query<KanbanLane>().ToListAsync(ct);
        var existingPlacements = await session.Query<BoardItemPlacement>().ToListAsync(ct);
        var existingBoardTrans = await session.Query<BoardTranslation>().ToListAsync(ct);
        var existingInventory = await session.Query<InventoryItem>().ToListAsync(ct);
        var existingCheckouts = await session.Query<InventoryCheckout>().ToListAsync(ct);

        // ── Tags (by slug) + per-language display names ────────────────────────────────
        var tagsBySlug = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in existingTags) tagsBySlug[existing.Slug] = existing.Id;
        foreach (var tag in doc.Tags)
        {
            string id;
            if (!tagsBySlug.TryGetValue(tag.Slug, out id))
            {
                id = Id();
                session.Store(new Tag
                {
                    Id = id,
                    Slug = tag.Slug,
                    Name = tag.Name,
                    LanguageCode = "en",
                    CreatedBy = usersByEmail[tag.CreatedByEmail].Id,
                    Created = now.AddDays(-tag.DaysAgo),
                });
                tagsBySlug[tag.Slug] = id;
            }
            // Top-up the tag's translation rows (create-if-missing by (tagId, lang)).
            var haveTagTrans = existingTagTrans
                .Where(t => t.TagId == id)
                .Select(t => t.LanguageCode)
                .ToHashSet();
            foreach (var tr in tag.Translations)
            {
                if (haveTagTrans.Contains(tr.LanguageCode)) continue;
                session.Store(new TagTranslation
                {
                    Id = Id(),
                    TagId = id,
                    LanguageCode = tr.LanguageCode,
                    Name = tr.Name ?? string.Empty,
                    AuthorId = TranslateAuthorId(tr.AuthorEmail),
                    Created = now.AddDays(-(tr.DaysAgo ?? 0)),
                });
                haveTagTrans.Add(tr.LanguageCode);
            }
        }

        // ── Groups (by title) + memberships + name/description translations ────────────
        var groupsByTitle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in existingGroups) groupsByTitle[existing.Name] = existing.Id;
        foreach (var group in doc.Groups)
        {
            string id;
            if (!groupsByTitle.TryGetValue(group.Name, out id))
            {
                id = Id();
                var seedOwner = usersByEmail[group.OwnerEmail];
                var seedCreated = now.AddDays(-group.DaysAgo);
                session.Store(new Group
                {
                    Id = id,
                    Name = group.Name,
                    Description = group.Description,
                    IsPrivate = group.IsPrivate,
                    OwnerId = seedOwner.Id,
                    Created = seedCreated,
                });
                groupsByTitle[group.Name] = id;
            }
            // Memberships — one row per (group, user) unique index; skip existing.
            // (GroupMembership has no IsDeleted — removal is a hard delete, ADR 0008.)
            var haveMembers = existingGroupsBy
                .Where(m => m.GroupId == id)
                .Select(m => m.UserId)
                .ToHashSet();
            var owner = usersByEmail[group.OwnerEmail];
            var groupCreated = now.AddDays(-group.DaysAgo);
            foreach (var memberEmail in group.MemberEmails)
            {
                var member = usersByEmail[memberEmail];
                if (haveMembers.Contains(member.Id)) continue;
                session.Store(new GroupMembership
                {
                    Id = Id(),
                    GroupId = id,
                    UserId = member.Id,
                    AddedBy = owner.Id,
                    At = groupCreated,
                });
                haveMembers.Add(member.Id);
            }
            // Group translations — top-up by (groupId, lang).
            var haveGrpTrans = existingGrpTrans
                .Where(t => t.GroupId == id)
                .Select(t => t.LanguageCode)
                .ToHashSet();
            foreach (var tr in group.Translations)
            {
                if (haveGrpTrans.Contains(tr.LanguageCode)) continue;
                session.Store(new GroupTranslation
                {
                    Id = Id(),
                    GroupId = id,
                    LanguageCode = tr.LanguageCode,
                    Name = tr.Name,
                    Description = tr.Description,
                    AuthorId = TranslateAuthorId(tr.AuthorEmail),
                    Created = now.AddDays(-(tr.DaysAgo ?? 0)),
                });
                haveGrpTrans.Add(tr.LanguageCode);
            }
        }

        // ── Moderator assignments (ADR 0003) — the rows that mint `moderator:{id}` ─────
        // There is no natural-key unique index on (UserId, ComponentId); match the pair
        // in memory. A missing pair is created; an existing one is skipped.
        var existingModAssign = await session.Query<ModeratorAssignment>().ToListAsync(ct);
        foreach (var assignment in doc.ModeratorAssignments)
        {
            var moderator = usersByEmail[assignment.UserEmail];
            var grantedBy = assignment.GrantedByEmail is not null
                ? usersByEmail[assignment.GrantedByEmail].Id
                : admin.Id;
            bool have = existingModAssign.Any(m =>
                m.UserId == moderator.Id && m.ComponentId == assignment.ComponentId);
            if (have) continue;
            session.Store(new ModeratorAssignment
            {
                Id = Id(),
                UserId = moderator.Id,
                ComponentId = assignment.ComponentId,
                GrantedBy = grantedBy,
                At = now.AddDays(-assignment.DaysAgo),
            });
        }

        // ── Announcements (by title, component-scoped) + translations ──────────────────
        var announcementsByTitle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in existingAnn)
        {
            // Composite key: title + component (null = platform-wide) + scope, so two
            // announcements with the same title in different scopes are distinct.
            announcementsByTitle[existing.Title + "|" + (existing.CommunityId ?? "_")] = existing.Id;
        }
        foreach (var announcement in doc.Announcements)
        {
            var key = announcement.Title + "|" + (announcement.ComponentId ?? "_");
            string id;
            if (!announcementsByTitle.TryGetValue(key, out id))
            {
                id = Id();
                session.Store(new Announcement
                {
                    Id = id,
                    AuthorId = usersByEmail[announcement.AuthorEmail].Id,
                    Title = announcement.Title,
                    Body = announcement.Body,
                    Scope = ParseScope(announcement.Scope),
                    CommunityId = announcement.ComponentId,
                    Pinned = announcement.Pinned,
                    LanguageCode = "en",
                    Created = now.AddDays(-announcement.DaysAgo),
                });
                announcementsByTitle[key] = id;
            }
            // Top-up the announcement's translation rows.
            var haveAnnTrans = existingAnnTrans
                .Where(t => t.AnnouncementId == id)
                .Select(t => t.LanguageCode)
                .ToHashSet();
            foreach (var tr in announcement.Translations)
            {
                if (haveAnnTrans.Contains(tr.LanguageCode)) continue;
                session.Store(new AnnouncementTranslation
                {
                    Id = Id(),
                    AnnouncementId = id,
                    LanguageCode = tr.LanguageCode,
                    Title = tr.Title ?? string.Empty,
                    Body = tr.Body,
                    AuthorId = TranslateAuthorId(tr.AuthorEmail),
                    Created = now.AddDays(-(tr.DaysAgo ?? 0)),
                });
                haveAnnTrans.Add(tr.LanguageCode);
            }
        }

        // ── Posts (by title + lane) + replies + translations ───────────────────────────
        // The lane is `GroupId` for a group-lane post, `ComponentId` for a community
        // post (the ADR 0013 lane exclusivity). The composite key encodes which lane.
        // The Group entity carries no slug (the ADR 0010 shape), so the sample
        // document's slug → group-id mapping is derived once (matching the
        // document's group names against the groups created/seeded above) — posts
        // reference their group by slug, so this is the lookup they need.
        var docGroupSlugToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sampleGroup in doc.Groups)
            if (groupsByTitle.TryGetValue(sampleGroup.Name, out var sampleGroupId))
                docGroupSlugToId[sampleGroup.Slug] = sampleGroupId;
        var postsByTitle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in existingPosts)
        {
            var lane = string.IsNullOrEmpty(existing.GroupId) ? "C:" + (existing.ComponentId ?? "_")
                                                               : "G:" + existing.GroupId;
            postsByTitle[existing.Title + "|" + lane] = existing.Id;
        }
        // Same-title posts collapse to one (PostId, lane) row (ADR 0130 D5). When
        // two or more sample posts share a title within the same lane, the
        // translation top-up runs once per sample post against the *same* PostId.
        // Track the languages already stored for each PostId *this run* so the
        // second/third duplicate doesn't re-store the same (PostId, LanguageCode)
        // and trip the PostTranslation unique constraint.
        var postTransLangsThisRun = new Dictionary<string, HashSet<string>>();
        foreach (var samplePost in doc.Posts)
        {
            bool groupLane = samplePost.GroupSlug is not null;
            var groupPostId = groupLane && docGroupSlugToId.TryGetValue(samplePost.GroupSlug!, out var g) ? g : string.Empty;
            var lane = groupLane ? "G:" + groupPostId
                                 : "C:" + (samplePost.ComponentId ?? "_");
            var key = samplePost.Title + "|" + lane;
            string id;
            if (!postsByTitle.TryGetValue(key, out id))
            {
                id = Id();
                session.Store(new Post
                {
                    Id = id,
                    ComponentId = groupLane ? string.Empty : (samplePost.ComponentId ?? string.Empty),
                    GroupId = groupPostId,
                    AuthorId = usersByEmail[samplePost.AuthorEmail].Id,
                    Title = samplePost.Title,
                    Body = samplePost.Body,
                    Audience = ParsePostAudience(samplePost.Audience),
                    Created = now.AddDays(-samplePost.DaysAgo),
                    Modified = samplePost.ModifiedDaysAgo.HasValue ? now.AddDays(-samplePost.ModifiedDaysAgo.Value) : null,
                    LanguageCode = "en",
                    TagIds = [.. samplePost.TagSlugs.Select(slug => tagsBySlug[slug])],
                });
                postsByTitle[key] = id;
            }
            // Replies — there is no natural-key unique index on (PostId, Body); match
            // in-memory by (PostId, Body) and skip an existing one (create-if-missing).
            var haveReplies = existingReplies
                .Where(r => r.PostId == id)
                .Select(r => r.Body)
                .ToHashSet();
            foreach (var reply in samplePost.Replies)
            {
                if (haveReplies.Contains(reply.Body)) continue;
                var replyId = Id();
                session.Store(new PostReply
                {
                    Id = replyId,
                    PostId = id,
                    AuthorId = usersByEmail[reply.AuthorEmail].Id,
                    Body = reply.Body,
                    Created = now.AddDays(-(reply.DaysAgo ?? 0)).AddHours(reply.HoursAfter ?? 0),
                    LanguageCode = "en",
                });
                haveReplies.Add(reply.Body);
                // Top-up the reply's translation rows.
                var haveReplTrans = existingReplTrans
                    .Where(t => t.ReplyId == replyId)
                    .Select(t => t.LanguageCode)
                    .ToHashSet();
                foreach (var tr in reply.Translations)
                {
                    if (haveReplTrans.Contains(tr.LanguageCode)) continue;
                    session.Store(new ReplyTranslation
                    {
                        Id = Id(),
                        ReplyId = replyId,
                        LanguageCode = tr.LanguageCode,
                        Body = tr.Body,
                        AuthorId = TranslateAuthorId(tr.AuthorEmail),
                        Created = now.AddDays(-(tr.DaysAgo ?? 0)),
                    });
                    haveReplTrans.Add(tr.LanguageCode);
                }
            }
            // Top-up the post's translation rows.
            var havePostTrans = existingPostTrans
                .Where(t => t.PostId == id)
                .Select(t => t.LanguageCode)
                .ToHashSet();
            // A same-title duplicate stored translations for this PostId earlier in
            // this run; those aren't in the pre-run snapshot, so fold them in.
            if (postTransLangsThisRun.TryGetValue(id, out var alreadyAdded))
                havePostTrans.UnionWith(alreadyAdded);
            if (!postTransLangsThisRun.TryGetValue(id, out var addedThisRun))
                postTransLangsThisRun[id] = addedThisRun = new HashSet<string>();
            foreach (var tr in samplePost.Translations)
            {
                if (havePostTrans.Contains(tr.LanguageCode)) continue;
                session.Store(new PostTranslation
                {
                    Id = Id(),
                    PostId = id,
                    LanguageCode = tr.LanguageCode,
                    Title = tr.Title ?? string.Empty,
                    Body = tr.Body,
                    AuthorId = TranslateAuthorId(tr.AuthorEmail),
                    Created = now.AddDays(-(tr.DaysAgo ?? 0)),
                });
                havePostTrans.Add(tr.LanguageCode);
                addedThisRun.Add(tr.LanguageCode);
            }
        }

        // ── Events (by title) + RSVPs + translations ───────────────────────────────────
        var eventsByTitle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in existingEvents) eventsByTitle[existing.Title] = existing.Id;
        foreach (var sampleEvent in doc.Events)
        {
            string id;
            if (!eventsByTitle.TryGetValue(sampleEvent.Title, out id))
            {
                id = Id();
                var start = now.AddDays(sampleEvent.StartDaysAhead);
                session.Store(new Event
                {
                    Id = id,
                    Title = sampleEvent.Title,
                    Body = sampleEvent.Body,
                    ComponentId = sampleEvent.ComponentId,
                    AuthorId = usersByEmail[sampleEvent.AuthorEmail].Id,
                    Start = start,
                    End = start.AddHours(sampleEvent.EndHoursAfterStart),
                    Location = sampleEvent.Location,
                    Capacity = sampleEvent.Capacity,
                    Color = sampleEvent.Color,
                    Audience = new Audience(AudienceMode.Any, Array.Empty<AudienceGrant>()) { AllResidents = true },
                    ReminderEnabled = sampleEvent.ReminderEnabled,
                    IsDraft = false,
                    IsDeleted = false,
                    LanguageCode = "en",
                    TagIds = [.. sampleEvent.TagSlugs.Select(slug => tagsBySlug[slug])],
                    Created = sampleEvent.DaysAgo.HasValue ? now.AddDays(-sampleEvent.DaysAgo.Value) : now,
                    Modified = sampleEvent.ModifiedNow ? now : null,
                });
                eventsByTitle[sampleEvent.Title] = id;
            }
            // RSVPs — (EventId, UserId) unique index; skip existing.
            var haveRsvps = existingRsvps
                .Where(r => r.EventId == id)
                .Select(r => r.UserId)
                .ToHashSet();
            foreach (var rsvp in sampleEvent.Rsvps)
            {
                var userId = usersByEmail[rsvp.UserEmail].Id;
                if (haveRsvps.Contains(userId)) continue;
                session.Store(new EventRsvp
                {
                    Id = Id(),
                    EventId = id,
                    UserId = userId,
                    Status = ParseRsvpStatus(rsvp.Status),
                    At = now.AddDays(-(rsvp.DaysAgo ?? 0)).AddHours(rsvp.HoursAfter ?? 0),
                });
                haveRsvps.Add(userId);
            }
            // Top-up the event's translation rows.
            var haveEvtTrans = existingEvtTrans
                .Where(t => t.EventId == id)
                .Select(t => t.LanguageCode)
                .ToHashSet();
            foreach (var tr in sampleEvent.Translations)
            {
                if (haveEvtTrans.Contains(tr.LanguageCode)) continue;
                session.Store(new EventTranslation
                {
                    Id = Id(),
                    EventId = id,
                    LanguageCode = tr.LanguageCode,
                    Title = tr.Title,
                    Body = tr.Body,
                    AuthorId = TranslateAuthorId(tr.AuthorEmail),
                    Created = now.AddDays(-(tr.DaysAgo ?? 0)),
                });
                haveEvtTrans.Add(tr.LanguageCode);
            }
        }

        // ── Pages (by slug — globally unique in the sample corpus) + translations ──────
        // The ADR 0040 blog lane: parents are always listed before their sub-pages, and
        // the slug is the stable cross-reference key (SeedAsync keys off it the same way).
        var pagesBySlug = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in existingPages)
            pagesBySlug[existing.Slug] = existing.Id;
        foreach (var page in doc.Pages)
        {
            string id;
            if (!pagesBySlug.TryGetValue(page.Slug, out id))
            {
                id = Id();
                var parent = page.ParentSlug is not null ? pagesBySlug[page.ParentSlug] : null;
                var created = now.AddDays(-page.DaysAgo);
                session.Store(new Page
                {
                    Id = id,
                    ParentId = parent,
                    Slug = page.Slug,
                    Title = page.Title,
                    Body = page.Body,
                    Audience = null, // public (world-readable) — a blog page may be public
                    Kind = ParsePageKind(page.Kind),
                    AuthorId = usersByEmail[page.AuthorEmail].Id,
                    ComponentId = null,
                    LanguageCode = "en",
                    Created = created,
                    Modified = created,
                    IsDraft = false,
                    IsDeleted = false,
                    TagIds = [.. page.TagSlugs.Select(slug => tagsBySlug[slug])],
                });
                pagesBySlug[page.Slug] = id;
            }
            // Top-up the page's translation rows (create-if-missing by (pageId, lang)).
            var havePageTrans = existingPageTrans
                .Where(t => t.PageId == id)
                .Select(t => t.LanguageCode)
                .ToHashSet();
            foreach (var tr in page.Translations)
            {
                if (havePageTrans.Contains(tr.LanguageCode)) continue;
                session.Store(new PageTranslation
                {
                    Id = Id(),
                    PageId = id,
                    LanguageCode = tr.LanguageCode,
                    Title = tr.Title ?? string.Empty,
                    Body = tr.Body,
                    AuthorId = TranslateAuthorId(tr.AuthorEmail),
                    Created = now.AddDays(-(tr.DaysAgo ?? 0)),
                });
                havePageTrans.Add(tr.LanguageCode);
            }
        }

        // ── Goals (by title) — the PL lane, ADR 0086 D2 ────────────────────────────────
        var goalsByTitle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in existingGoals) goalsByTitle[existing.Title] = existing.Id;
        foreach (var goal in doc.Goals)
        {
            if (goalsByTitle.TryGetValue(goal.Title, out var existingGoalId))
            {
                goalsByTitle[goal.Title] = existingGoalId;
                continue;
            }
            var id = Id();
            session.Store(new ProjectGoal
            {
                Id = id,
                Title = goal.Title,
                Description = goal.Description,
                ComponentId = goal.ComponentId,
                AuthorId = usersByEmail[goal.AuthorEmail].Id,
                Audience = null,
                IsDeleted = false,
                LanguageCode = "en",
                Created = now.AddDays(-goal.DaysAgo),
            });
            goalsByTitle[goal.Title] = id;
        }

        // ── Projects (by title) + translations + nested to-dos ─────────────────────────
        // Board-card resolution maps (the boards key their cards by (projectTitle,
        // todoTitle)): project title → id, and (projectTitle|todoTitle) → to-do id, both
        // seeded from the existing corpus; to-dos newly created in this run register their
        // id as the loop runs so the boards loop (below) can resolve them.
        var projectsByTitle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in existingProjects) projectsByTitle[p.Title] = p.Id;
        var projIdToTitle = existingProjects.ToDictionary(p => p.Id, p => p.Title);
        var todosByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in existingTodos)
            if (t.ProjectId is not null && !t.IsDeleted && projIdToTitle.TryGetValue(t.ProjectId, out var pt))
                todosByKey[pt + "|" + t.Title] = t.Id;
        foreach (var sampleProject in doc.Projects)
        {
            string id;
            if (!existingProjects.Any(p => p.Title == sampleProject.Title))
            {
                id = Id();
                var goalId = sampleProject.GoalTitle is not null && goalsByTitle.TryGetValue(sampleProject.GoalTitle, out var g)
                    ? g
                    : null;
                var created = now.AddDays(-sampleProject.DaysAgo);
                session.Store(new Project
                {
                    Id = id,
                    Title = sampleProject.Title,
                    Description = sampleProject.Description,
                    GoalId = goalId,
                    Status = sampleProject.Status,
                    StartAt = sampleProject.StartDaysAgo.HasValue ? now.AddDays(-sampleProject.StartDaysAgo.Value) : null,
                    DueAt = sampleProject.DueInDays.HasValue ? now.AddDays(sampleProject.DueInDays.Value) : null,
                    ComponentId = sampleProject.ComponentId,
                    AuthorId = usersByEmail[sampleProject.AuthorEmail].Id,
                    Audience = null,
                    IsDeleted = false,
                    LanguageCode = "en",
                    Created = created,
                    Modified = created,
                });
            }
            else
            {
                // Resolve the existing id (we matched by title above).
                id = existingProjects.Single(p => p.Title == sampleProject.Title).Id;
            }
            // Top-up the project's translation rows.
            var havePrjTrans = existingPrjTrans
                .Where(t => t.ProjectId == id)
                .Select(t => t.LanguageCode)
                .ToHashSet();
            foreach (var tr in sampleProject.Translations)
            {
                if (havePrjTrans.Contains(tr.LanguageCode)) continue;
                session.Store(new ProjectTranslation
                {
                    Id = Id(),
                    ProjectId = id,
                    LanguageCode = tr.LanguageCode,
                    Title = tr.Title,
                    Body = tr.Body,
                    AuthorId = TranslateAuthorId(tr.AuthorEmail),
                    Created = now.AddDays(-(tr.DaysAgo ?? 0)),
                });
                havePrjTrans.Add(tr.LanguageCode);
            }
            // Nested to-dos — match by (ProjectId, Title); create if missing.
            var haveTodos = existingTodos
                .Where(t => t.ProjectId == id && !t.IsDeleted)
                .Select(t => t.Title)
                .ToHashSet();
            foreach (var todo in sampleProject.Todos)
            {
                if (haveTodos.Contains(todo.Title)) continue;
                var todoId = Id();
                session.Store(new TodoItem
                {
                    Id = todoId,
                    Title = todo.Title,
                    Body = todo.Body,
                    ComponentId = sampleProject.ComponentId,
                    ProjectId = id,
                    AuthorId = usersByEmail[sampleProject.AuthorEmail].Id,
                    AssigneeId = todo.AssigneeEmail is not null ? usersByEmail[todo.AssigneeEmail].Id : null,
                    Status = todo.Status,
                    StartAt = null,
                    DueAt = todo.DueInDays.HasValue ? now.AddDays(todo.DueInDays.Value) : null,
                    Audience = null,
                    IsDeleted = false,
                    LanguageCode = "en",
                    TagIds = [],
                    ImageIds = [],
                    AttachmentIds = [],
                    Created = now.AddDays(-todo.DaysAgo),
                    Modified = null,
                });
                haveTodos.Add(todo.Title);
                // Register for the board-card resolution below (boards key cards by
                // (projectTitle, todoTitle)).
                todosByKey[sampleProject.Title + "|" + todo.Title] = todoId;
            }
        }

        // ── Kanban boards (ADR 0067 D1) + lanes + placements (create-if-missing) ────────
        // A board is matched by title (the closed sample corpus — the ADR 0130 D3 caveat
        // about display-string keys applies); its lanes by (boardId, order) and its card
        // placements by (todoId, boardId) — both unique-index-backed. A card naming a to-do
        // or a lane absent on a warm DB is skipped (create-if-missing) — the D3 fail-fast is
        // the pristine SeedAsync path only.
        var boardsByTitle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in existingBoards) boardsByTitle[b.Title] = b.Id;
        foreach (var sampleBoard in doc.Boards)
        {
            string boardId;
            if (!boardsByTitle.TryGetValue(sampleBoard.Title, out boardId))
            {
                boardId = Id();
                var projectId = sampleBoard.ProjectTitle is not null
                    && projectsByTitle.TryGetValue(sampleBoard.ProjectTitle, out var pid) ? pid : null;
                var boardCreated = now.AddDays(-sampleBoard.DaysAgo);
                session.Store(new KanbanBoard
                {
                    Id = boardId,
                    Title = sampleBoard.Title,
                    Description = sampleBoard.Description,
                    ComponentId = sampleBoard.ComponentId,
                    ProjectId = projectId,
                    AuthorId = usersByEmail[sampleBoard.AuthorEmail].Id,
                    Audience = null,
                    IsDeleted = false,
                    LanguageCode = "en",
                    Created = boardCreated,
                    Modified = boardCreated,
                });
                boardsByTitle[sampleBoard.Title] = boardId;
            }
            // Lanes — (boardId, order) unique; skip an existing one by (boardId, order).
            var existingLaneOrders = existingLanes.Where(l => l.BoardId == boardId).Select(l => l.Order).ToHashSet();
            var lanesByTitle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var existingLane in existingLanes.Where(l => l.BoardId == boardId))
                lanesByTitle[existingLane.Title] = existingLane.Id;
            foreach (var lane in sampleBoard.Lanes)
            {
                string laneId;
                if (existingLaneOrders.Contains(lane.Order))
                    laneId = existingLanes.Single(l => l.BoardId == boardId && l.Order == lane.Order).Id;
                else
                {
                    laneId = Id();
                    var boardCreated = now.AddDays(-sampleBoard.DaysAgo);
                    session.Store(new KanbanLane
                    {
                        Id = laneId,
                        BoardId = boardId,
                        Title = lane.Title,
                        Status = lane.Status,
                        MaxItems = lane.MaxItems,
                        Order = lane.Order,
                        Created = boardCreated,
                        Modified = boardCreated,
                    });
                    existingLaneOrders.Add(lane.Order);
                }
                lanesByTitle[lane.Title] = laneId;
            }
            // Board title/description translations (ADR 0088) — top-up by (boardId, lang).
            var haveBoardTrans = existingBoardTrans.Where(t => t.BoardId == boardId)
                .Select(t => t.LanguageCode).ToHashSet();
            foreach (var tr in sampleBoard.Translations)
            {
                if (haveBoardTrans.Contains(tr.LanguageCode)) continue;
                session.Store(new BoardTranslation
                {
                    Id = Id(),
                    BoardId = boardId,
                    LanguageCode = tr.LanguageCode,
                    Title = tr.Title,
                    Body = tr.Body,
                    AuthorId = TranslateAuthorId(tr.AuthorEmail),
                    Created = now.AddDays(-(tr.DaysAgo ?? 0)),
                });
                haveBoardTrans.Add(tr.LanguageCode);
            }
            // Cards — (todoId, boardId) unique; skip an existing one by todo id.
            var existingCardTodoIds = existingPlacements.Where(pl => pl.BoardId == boardId)
                .Select(pl => pl.TodoItemId).ToHashSet();
            foreach (var card in sampleBoard.Cards)
            {
                var cardKey = card.ProjectTitle + "|" + card.TodoTitle;
                if (!todosByKey.TryGetValue(cardKey, out var todoItemId)) continue; // to-do absent — skip.
                if (existingCardTodoIds.Contains(todoItemId)) continue;            // already placed.
                if (!lanesByTitle.TryGetValue(card.LaneTitle, out var laneId)) continue; // lane absent — skip.
                var boardCreated = now.AddDays(-sampleBoard.DaysAgo);
                session.Store(new BoardItemPlacement
                {
                    Id = Id(),
                    TodoItemId = todoItemId,
                    BoardId = boardId,
                    LaneId = laneId,
                    Order = card.Order,
                    Created = boardCreated,
                    Modified = boardCreated,
                });
                existingCardTodoIds.Add(todoItemId);
            }
        }

        // ── Inventory (ADR 0117) + usage records (create-if-missing) ────────────────────
        // An item is matched by name (the closed sample corpus); its usage records by
        // (itemId, borrowerId, note). A record naming an unknown borrower is impossible on a
        // real neighborhood (unreachable by the SampleData__Enabled gate) and is skipped
        // here (create-if-missing) rather than the D3 fail-fast (the pristine path only).
        var inventoryByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var i in existingInventory) inventoryByName[i.Name] = i.Id;
        foreach (var sampleItem in doc.InventoryItems)
        {
            string itemId;
            if (!inventoryByName.TryGetValue(sampleItem.Name, out itemId))
            {
                itemId = Id();
                var itemCreated = now.AddDays(-sampleItem.DaysAgo);
                string? currentHolder =
                    sampleItem.CurrentHolderEmail is not null
                        ? usersByEmail[sampleItem.CurrentHolderEmail].Id
                        : sampleItem.Checkouts
                            .Where(c => c.CheckedInDaysAgo is null)
                            .OrderByDescending(c => c.CheckedOutDaysAgo)
                            .Select(c => usersByEmail[c.BorrowerEmail].Id)
                            .FirstOrDefault();
                session.Store(new InventoryItem
                {
                    Id = itemId,
                    Name = sampleItem.Name,
                    OwnerKind = sampleItem.OwnerKind,
                    Description = sampleItem.Description,
                    ComponentId = sampleItem.ComponentId,
                    AuthorId = usersByEmail[sampleItem.AuthorEmail].Id,
                    Audience = null,
                    CurrentHolderId = currentHolder,
                    IsDeleted = false,
                    LanguageCode = "en",
                    Created = itemCreated,
                    Modified = itemCreated,
                });
                inventoryByName[sampleItem.Name] = itemId;
            }
            // Usage records — (itemId, borrowerId, note) match; skip an existing one.
            var haveCheckouts = existingCheckouts.Where(c => c.ItemId == itemId)
                .Select(c => (c.BorrowerId, c.Note)).ToHashSet();
            foreach (var co in sampleItem.Checkouts)
            {
                var borrowerId = usersByEmail[co.BorrowerEmail].Id;
                if (haveCheckouts.Contains((borrowerId, co.Note))) continue;
                session.Store(new InventoryCheckout
                {
                    Id = Id(),
                    ItemId = itemId,
                    BorrowerId = borrowerId,
                    CheckedOutAt = now.AddDays(-co.CheckedOutDaysAgo),
                    CheckedInAt = co.CheckedInDaysAgo.HasValue ? now.AddDays(-co.CheckedInDaysAgo.Value) : null,
                    Note = co.Note,
                });
                haveCheckouts.Add((borrowerId, co.Note));
            }
        }

        // ── Community (board) name/description translations (ADR 0026) ─────────────────
        // Match by (ComponentId, LanguageCode).
        var haveCommTrans = existingCommTrans
            .Select(t => (t.ComponentId, t.LanguageCode))
            .ToHashSet();
        foreach (var community in doc.CommunityTranslations)
        {
            var key = (community.ComponentId, community.LanguageCode);
            if (haveCommTrans.Contains(key)) continue;
            session.Store(new CommunityTranslation
            {
                Id = Id(),
                ComponentId = community.ComponentId,
                LanguageCode = community.LanguageCode,
                Name = community.Name,
                Description = community.Description,
                AuthorId = translationAuthorId,
                Created = now.AddDays(-(community.DaysAgo ?? 0)),
            });
            haveCommTrans.Add(key);
        }

        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Warm-boot: backfilled the sample corpus (create-if-missing, idempotent). " +
            "Accounts: {Accounts}, Tags: {Tags}, Groups: {Groups}, Announcements: {Ann}, " +
            "Posts: {Posts}, Events: {Events}, Pages: {Pages}, Goals: {Goals}, Projects: {Projects}.",
            doc.Accounts.Count, doc.Tags.Count, doc.Groups.Count, doc.Announcements.Count,
            doc.Posts.Count, doc.Events.Count, doc.Pages.Count, doc.Goals.Count, doc.Projects.Count);
    }

    /// <summary>
    /// Find-or-create a verified-resident account: the EF/<c>identity</c>-side write
    /// (account + password + roles) commits per <see cref="UserManager"/> call; the
    /// <c>mt</c>-side <see cref="Profile"/> is stored in its own session. Idempotent —
    /// keyed by e-mail — so a warm re-run (the pristine gate normally prevents one) is a
    /// no-or-refresh.
    /// <para>
    /// <paramref name="contactVisibility"/> sets the opt-in contact-block audience
    /// (any signed-in resident may see the e-mail — the <see cref="Audience.AllResidents"/>
    /// branch, which needs no component target). <paramref name="timeZone"/> /
    /// <paramref name="dateFormat"/> are the resident overrides of the platform defaults
    /// (ADR 0019 / ADR 0020); left null the instance default applies.
    /// </para>
    /// </summary>
    private static async Task<User> EnsureUserAsync(
        UserManager<User> userManager,
        RoleManager<IdentityRole> roleManager,
        IDocumentStore mt,
        string email,
        string? password,
        string displayName,
        bool contactVisibility = false,
        string? timeZone = null,
        string? dateFormat = null,
        string? elevatedRole = null,
        ILogger logger = default!,
        CancellationToken ct = default)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is null)
        {
            // Ensure the elevated role row exists (FirstBootSeeder already creates
            // GlobalAdmin/Moderator/Translator; be explicit so the seeder is self-contained).
            if (elevatedRole is not null && await roleManager.FindByNameAsync(elevatedRole) is null)
                await roleManager.CreateAsync(new IdentityRole(elevatedRole));

            existing = new User { Id = Id(), Email = email, UserName = email };
            await userManager.CreateAsync(existing);
            if (!string.IsNullOrEmpty(password))
            {
                var pwResult = await userManager.AddPasswordAsync(existing, password);
                if (!pwResult.Succeeded)
                    throw new InvalidOperationException(
                        $"Failed to set the demo password for '{email}': {string.Join(", ", pwResult.Errors.Select(e => e.Description))}");
            }
            if (elevatedRole is not null)
                await userManager.AddToRoleAsync(existing, elevatedRole);
        }
        else
        {
            // Idempotent: add the password only if the account has none (the seeded
            // admin is created without a password — the setup-token lane). A null
            // password (the deploy-posture admin, ADR 0056) is a no-op: the account
            // keeps its token lane and gets no weak credential.
            if (string.IsNullOrEmpty(existing.PasswordHash) && !string.IsNullOrEmpty(password))
            {
                var pwResult = await userManager.AddPasswordAsync(existing, password);
                if (!pwResult.Succeeded)
                    throw new InvalidOperationException(
                        $"Failed to set the demo password for '{email}': {string.Join(", ", pwResult.Errors.Select(e => e.Description))}");
            }
            if (elevatedRole is not null)
                await userManager.AddToRoleAsync(existing, elevatedRole);
        }

        // The mt-side Profile — verified resident, self-only visibility default, optional
        // opt-in contact block + tz/format overrides. (The extended bio/tags/visibility
        // are applied later, in the single content session — ADR 0123.)
        await using var session = mt.OpenSession(new SessionOptions());
        var profile = await session.LoadAsync<Profile>(existing.Id, ct);
        profile ??= new Profile { SubjectId = existing.Id };
        // Create-if-missing, never clobber (ADR 0042 D1): a DisplayName the resident set
        // in-app must survive a reboot — the seeder is a development convenience, not the
        // owner of the user's profile. Only an unset name, or FirstBootSeeder's e-mail
        // placeholder (which is the seed admin's name before the sample name lands on a
        // pristine boot), is replaced by the sample name.
        if (string.IsNullOrEmpty(profile.DisplayName)
            || string.Equals(profile.DisplayName, email, StringComparison.OrdinalIgnoreCase))
            profile.DisplayName = displayName;
        profile.Email = email;
        profile.Verified = true;
        if (profile.Visibility is null)
            profile.Visibility = new Audience();
        profile.ContactVisibility = contactVisibility
            ? new Audience(AudienceMode.Any, Array.Empty<AudienceGrant>()) { AllResidents = true }
            : profile.ContactVisibility;
        if (timeZone is not null)
            profile.TimeZone = timeZone;
        if (dateFormat is not null)
            profile.DateFormat = dateFormat;
        session.Store(profile);
        await session.SaveChangesAsync();

        if (logger is not null)
            logger.LogDebug("Sample data: ensured account {Email} ({Name}).", email, displayName);
        return existing;
    }

    /// <summary>
    /// A random 32-character high-entropy password for a deploy-posture demo account.
    /// CSPRNG-backed (<see cref="System.Security.Cryptography.RandomNumberGenerator"/>)
    /// over the full alphanumeric alphabet, and guaranteed to contain at least one
    /// uppercase, one lowercase, and one digit — the app's Identity password policy
    /// requires all three (only the *non-alphanumeric* requirement is relaxed by
    /// <c>Program.cs</c>; <c>RequireUppercase</c> / <c>RequireLowercase</c> /
    /// <c>RequireDigit</c> keep their ASP.NET Core defaults). Distinct per account,
    /// and never derived from, or printed alongside, the seed admin's own credential
    /// (ADR 0056).
    /// </summary>
    private static string RandomPassword()
    {
        const int length = 32;
        const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lower = "abcdefghijklmnopqrstuvwxyz";
        const string digit = "0123456789";
        const string alphabet = upper + lower + digit;

        var chars = new char[length];
        // Seed one guaranteed member of each required class, fill the rest from the
        // full alphabet, then shuffle (CSPRNG Fisher–Yates) so no position is predictable.
        chars[0] = upper[System.Security.Cryptography.RandomNumberGenerator.GetInt32(upper.Length)];
        chars[1] = lower[System.Security.Cryptography.RandomNumberGenerator.GetInt32(lower.Length)];
        chars[2] = digit[System.Security.Cryptography.RandomNumberGenerator.GetInt32(digit.Length)];
        for (var i = 3; i < length; i++)
            chars[i] = alphabet[System.Security.Cryptography.RandomNumberGenerator.GetInt32(alphabet.Length)];
        for (var i = length - 1; i > 0; i--)
        {
            var j = System.Security.Cryptography.RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars);
    }

    /// <summary>
    /// The deploy-posture credentials e-mail body (ADR 0056): the demo accounts' e-mail +
    /// generated password, one per line (sourced from the sample-data document), plus the
    /// note that the seed-admin account keeps its <c>SeedAdmin__</c> setup-token lane.
    /// Staged once through the durable outbox to the seed admin; the operator is told to
    /// delete it once captured.
    /// </summary>
    private static string SampleDataCredentialsBody(IEnumerable<(string Email, string Password)> credentials)
    {
        var lines = new List<string>
        {
            "A Kumunita instance has seeded a demo neighborhood (sample data).",
            "Demo account credentials (e-mail → password):",
            "",
        };
        lines.AddRange(credentials.Select(c => $"{c.Email}  →  {c.Password}"));
        lines.AddRange(new[]
        {
            "",
            "Your own admin account keeps its one-time setup-token lane; use the setup link " +
            "from the first-boot setup e-mail to set your admin password.",
            "",
            "Treat this e-mail as sensitive and delete it once you have the credentials.",
        });
        return string.Join("\n", lines);
    }
}
