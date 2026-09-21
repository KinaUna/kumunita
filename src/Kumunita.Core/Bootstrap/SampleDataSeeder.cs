using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
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
/// <c>docker compose down -v && docker compose up --build</c> comes up immediately
/// populated with accounts (a password-bearing admin, a scoped moderator, a translator,
/// several verified residents), groups, announcements, community + group posts with
/// replies, published events with RSVPs, tags, and a resident blog. It is gated to the
/// <c>Development</c> environment at the call site (see <c>Program.cs</c>) so it can
/// never run on a real deployment.
/// <para>
/// <b>Idempotent + pristine-gated.</b> Like <see cref="FirstBootSeeder"/>, every step is
/// a create-if-missing no-op (accounts keyed by e-mail, content keyed by its own ids) and
/// the pristine outer gate keeps it from touching a warm database. <b>No e-mail</b> is
/// staged (the seeder is not a user-facing sign-up — there is no <see cref="IMailerStage"/>
/// dependency, which also sidesteps the Wolverine <c>IMessageContext</c> requirement the
/// outbox staging needs).
/// <para>
/// <b>Session discipline (invariant C3):</b> the EF / <c>identity</c>-side writes (accounts,
/// roles, passwords) commit per <see cref="UserManager"/> call; every <c>mt</c>-side
/// document (profiles, groups, content, translations) is stored in a <b>single</b>
/// <see cref="IDocumentSession"/> and committed once. The component-mandatory flags ride
/// the <see cref="IUserInfoService.SetCommunityMandatoryAsync"/> write lane (its own
/// session + audit row) because that is the single sanctioned writer for
/// <c>Component.Mandatory</c> (ADR 0012).
/// <para>
/// <b>Visibility model.</b> The four seeded communities are marked <b>mandatory</b>
/// (ADR 0012), so <b>every verified resident is a member of every board</b> and the
/// default community-visible posts (<see cref="Audience.Community"/> = true) are visible
/// to all of them without per-account grant rows. A content item's audience therefore only
/// needs the flat <c>Community = true</c> shape — no per-user grants.
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

    private static readonly IReadOnlySet<string> GlobalAdminRoles =
        new HashSet<string> { Roles.GlobalAdmin };

    private static string Id() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// Runs the sample-data steps. Called once by <see cref="SchemaBootstrap"/> on a
    /// pristine DB, only when the host is in the Development environment.
    /// </summary>
    public static async Task SeedAsync(
        AppDbContext identity,
        IDocumentStore mt,
        UserManager<User> userManager,
        RoleManager<IdentityRole> roleManager,
        IUserInfoService userInfo,
        ILogger logger,
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "Development sample data: seeding the mock neighborhood (Development-only, pristine DB).");

        // ── 1. Accounts ────────────────────────────────────────────────────────────────
        // The seeded admin (FirstBootSeeder) already exists with a setup token but no
        // password — EnsureUserAsync finds it and adds the demo password. The rest are
        // created fresh. All are verified residents (Member standing is implicit).
        var admin   = await EnsureUserAsync(userManager, roleManager, mt,
            AdminEmail, AdminPassword, "Alex Admin",
            elevatedRole: Roles.GlobalAdmin, logger: logger, ct: ct);

        var maria   = await EnsureUserAsync(userManager, roleManager, mt,
            ModeratorEmail, ModeratorPassword, "Maria Moderator",
            elevatedRole: Roles.Moderator, logger: logger, ct: ct);

        var sophie    = await EnsureUserAsync(userManager, roleManager, mt,
            TranslatorEmail, TranslatorPassword, "Sophie Translate",
            elevatedRole: Roles.Translator, logger: logger, ct: ct);

        var anna    = await EnsureUserAsync(userManager, roleManager, mt,
            "anna@examplium.com", ResidentPassword, "Anna Kowalska",
            contactVisibility: true, timeZone: "Europe/Warsaw", logger: logger, ct: ct);

        var ben     = await EnsureUserAsync(userManager, roleManager, mt,
            "ben@examplium.com", ResidentPassword, "Ben Nowak", logger: logger, ct: ct);

        var carla   = await EnsureUserAsync(userManager, roleManager, mt,
            "carla@examplium.com", ResidentPassword, "Carla Kubiak",
            contactVisibility: true, logger: logger, ct: ct);

        var david   = await EnsureUserAsync(userManager, roleManager, mt,
            "david@examplium.com", ResidentPassword, "David Lis",
            timeZone: "Europe/Prague", logger: logger, ct: ct);

        // ── 2. Components are mandatory (ADR 0012) — every verified resident is a member
        //    of every board, so the flat `Community = true` posts below are visible to all.
        //    Rides the sanctioned write lane (own session + audit row). ───────────────
        foreach (var componentId in new[] { "safety", "maintenance", "social", "governance" })
        {
            await userInfo.SetCommunityMandatoryAsync(componentId, true, admin.Id, GlobalAdminRoles);
        }

        // Maria's component standing (ADR 0003) — the `Moderator` EF role is already on her
        // account (above); the assignment rows are what mint her `moderator:{id}` claims at
        // sign-in (IdentityService.GetBySubjectAsync). Stored directly (bootstrap writer,
        // the FirstBootSeeder posture) in the single content session below.

        // ── 3. All `mt`-side sample content — one session, one commit (invariant C3). ──
        var now = DateTimeOffset.UtcNow;
        await using var session = mt.OpenSession(new SessionOptions());

        // Groups (a private family group + a public street group) + memberships.
        var family = new Group
        {
            Id = Id(), Name = "Kowalski Family",
            Description = "Anna and Ben's household — a private organizing group (ADR 0010).",
            IsPrivate = true, OwnerId = anna.Id, Created = now.AddDays(-40)
        };
        var green = new Group
        {
            Id = Id(), Name = "Street Green",
            Description = "Neighbors working on the shared green by the playground.",
            IsPrivate = false, OwnerId = carla.Id, Created = now.AddDays(-35)
        };
        session.Store(family);
        session.Store(green);
        session.Store(new GroupMembership { Id = Id(), GroupId = family.Id, UserId = anna.Id, AddedBy = anna.Id, At = now.AddDays(-40) });
        session.Store(new GroupMembership { Id = Id(), GroupId = family.Id, UserId = ben.Id, AddedBy = anna.Id, At = now.AddDays(-40) });
        session.Store(new GroupMembership { Id = Id(), GroupId = green.Id, UserId = carla.Id, AddedBy = carla.Id, At = now.AddDays(-35) });
        session.Store(new GroupMembership { Id = Id(), GroupId = green.Id, UserId = anna.Id, AddedBy = carla.Id, At = now.AddDays(-34) });
        session.Store(new GroupMembership { Id = Id(), GroupId = green.Id, UserId = david.Id, AddedBy = carla.Id, At = now.AddDays(-33) });

        // Maria's moderator scope (safety + social) — what her `moderator:{id}` claims are
        // minted from at sign-in (she holds the `Moderator` EF role).
        session.Store(new ModeratorAssignment { Id = Id(), UserId = maria.Id, ComponentId = "safety", GrantedBy = admin.Id, At = now.AddDays(-30) });
        session.Store(new ModeratorAssignment { Id = Id(), UserId = maria.Id, ComponentId = "social", GrantedBy = admin.Id, At = now.AddDays(-30) });

        // ── Tags (+ a couple of translations) — labels, never gates (C-TG·1). ───────────
        var tagCleanup  = new Tag { Id = Id(), Slug = "cleanup", Name = "Cleanup", LanguageCode = "en", CreatedBy = anna.Id, Created = now.AddDays(-30) };
        var tagNotice   = new Tag { Id = Id(), Slug = "notice", Name = "Notice", LanguageCode = "en", CreatedBy = admin.Id, Created = now.AddDays(-30) };
        var tagRecipe   = new Tag { Id = Id(), Slug = "recipe", Name = "Recipe", LanguageCode = "en", CreatedBy = carla.Id, Created = now.AddDays(-20) };
        session.Store(tagCleanup);
        session.Store(tagNotice);
        session.Store(tagRecipe);
        session.Store(new TagTranslation { Id = Id(), TagId = tagCleanup.Id, LanguageCode = "de", Name = "Säuberung", AuthorId = admin.Id, Created = now.AddDays(-28) });
        session.Store(new TagTranslation { Id = Id(), TagId = tagRecipe.Id, LanguageCode = "fr", Name = "Recette", AuthorId = admin.Id, Created = now.AddDays(-18) });

        // ── Announcements (Public pinned / Community flat / Community targeted) ─────────
        var welcome = new Announcement
        {
            Id = Id(), AuthorId = admin.Id,
            Title = "Welcome to the neighborhood board",
            Body = "This is a demo instance of **Kumunita**, a self-hosted platform for one\n\nneighborhood. Everything you see here is sample data — free to edit, hide, or delete while you explore.",
            Scope = AnnouncementScope.Public, CommunityId = null, Pinned = true,
            LanguageCode = "en", Created = now.AddDays(-14)
        };
        var volunteers = new Announcement
        {
            Id = Id(), AuthorId = maria.Id,
            Title = "Volunteers needed for the Saturday cleanup",
            Body = "We're clearing the back lane on **Saturday**. Bring gloves; we supply the bags.",
            Scope = AnnouncementScope.Community, CommunityId = null, Pinned = false,
            LanguageCode = "en", Created = now.AddDays(-3)
        };
        var alarm = new Announcement
        {
            Id = Id(), AuthorId = admin.Id,
            Title = "Fire-alarm test this week",
            Body = "Expect the building alarm to sound on **Thursday 09:00–09:30**. It is a test — please do not use the fire stairs unless they are actually in use.",
            Scope = AnnouncementScope.Community, CommunityId = "safety", Pinned = false,
            LanguageCode = "en", Created = now.AddDays(-1)
        };
        // A leading pinned, admin-authored test-platform notice (the most visible
        // thing on a demo instance — it tells visitors this is not real and to keep
        // private data off it). Pinned like `welcome`, Public scope (everyone), newest
        // so it sorts to the top of the pinned set.
        var testPlatform = new Announcement
        {
            Id = Id(), AuthorId = admin.Id,
            Title = "Test Platform",
            Body = "This is a test platform - not intended for real use.\n\nServices may stop working at any time, data may be deleted at any time, and changes may happen at any time.\n\nThis test platform is currently open for new users to sign-up, so anyone can try it out, so don't share any real or private information here.",
            Scope = AnnouncementScope.Public, CommunityId = null, Pinned = true,
            LanguageCode = "en", Created = now
        };
        session.Store(welcome);
        session.Store(testPlatform);
        session.Store(volunteers);
        session.Store(alarm);
        // Translation lane demos (ADR 0029 — a `GlobalAdmin`/`Translator` standing).
        session.Store(new AnnouncementTranslation
        {
            Id = Id(), AnnouncementId = welcome.Id, LanguageCode = "de",
            Title = "Willkommen im Nachbarschaftsbrett",
            Body = "Dies ist eine Demo-Instanz von **Kumunita**. Alle Inhalte hier sind Beispieldaten — frei zum Bearbeiten, Verbergen oder Löschen.",
            AuthorId = sophie.Id, Created = now.AddDays(-13)
        });
        session.Store(new AnnouncementTranslation
        {
            Id = Id(), AnnouncementId = alarm.Id, LanguageCode = "fr",
            Title = "Essai des alarmes incendie cette semaine",
            Body = "Le système d'alarme doit retentir **jeudi de 09:00 à 09:30**. C'est un essai — n'utilisez les escaliers de secours que s'ils sont réellement en usage.",
            AuthorId = sophie.Id, Created = now
        });

        // ── Community posts (+ replies + a tag + a translation) ─────────────────────────
        var communityAudience = () => new Audience(AudienceMode.Any, Array.Empty<AudienceGrant>()) { Community = true };

        var postRecycling = new Post
        {
            Id = Id(), ComponentId = "safety", AuthorId = anna.Id,
            Title = "New recycling schedule from next month",
            Body = "The city is moving glass to **Tuesdays** and paper to **Fridays** starting the 1st.\n\nDoes anyone have the new leaflet? I can print copies for the lobby.",
            Audience = communityAudience(),
            Created = now.AddDays(-5), Modified = now.AddDays(-4),
            LanguageCode = "en", TagIds = [tagNotice.Id]
        };
        var postPotluck = new Post
        {
            Id = Id(), ComponentId = "social", AuthorId = carla.Id,
            Title = "Potluck in the green this weekend?",
            Body = "Anyone up for a simple potluck under the tree on **Sunday**? No pressure — one dish each, drinks on the house (mine).",
            Audience = communityAudience(),
            Created = now.AddDays(-2), LanguageCode = "en", TagIds = [tagRecipe.Id]
        };
        var postMinutes = new Post
        {
            Id = Id(), ComponentId = "governance", AuthorId = david.Id,
            Title = "Monthly meeting minutes (draft for comment)",
            Body = "Summary of last week's building meeting:\n\n- Approved the garden-bed plan\n- Deferred the fence repaint to next season\n- Collected 3 € for the shared toolbox\n\nFlag anything you disagree with before it is finalized.",
            Audience = communityAudience(),
            Created = now.AddDays(-1), LanguageCode = "en", TagIds = [tagNotice.Id]
        };
        session.Store(postRecycling);
        session.Store(postPotluck);
        session.Store(postMinutes);

        var reply1 = new PostReply
        {
            Id = Id(), PostId = postRecycling.Id, AuthorId = ben.Id,
            Body = "I have the leaflet — it's on the notice board, page 2. Glass *and* the bottle bank both moved.",
            Created = now.AddDays(-4), LanguageCode = "en"
        };
        var reply2 = new PostReply
        {
            Id = Id(), PostId = postRecycling.Id, AuthorId = anna.Id,
            Body = "Perfect, thanks Ben — I'll grab it and print the copies today.",
            Created = now.AddDays(-4).AddHours(1), LanguageCode = "en"
        };
        var reply3 = new PostReply
        {
            Id = Id(), PostId = postPotluck.Id, AuthorId = david.Id,
            Body = "In! I'll bring a big salad. What about 14:00?",
            Created = now.AddDays(-1), LanguageCode = "en"
        };
        session.Store(reply1);
        session.Store(reply2);
        session.Store(reply3);

        // A post + reply translation (ADR 0022 — user-added, not machine-translated).
        session.Store(new PostTranslation
        {
            Id = Id(), PostId = postRecycling.Id, LanguageCode = "de",
            Title = "Neue Recyclingsch abende ab nächstem Monat",
            Body = "Die Stadt verschiebt Glas auf **Dienstag** und Papier auf **Freitag**, ab dem 1.\n\nHat jemand das neue Faltblatt? Ich drucke gerne Kopien für die Lobby.",
            AuthorId = sophie.Id, Created = now.AddDays(-4)
        });
        session.Store(new ReplyTranslation
        {
            Id = Id(), ReplyId = reply1.Id, LanguageCode = "de",
            Body = "Ich habe das Faltblatt — es ist am Schwarzen Brett, Seite 2. Glas *und* die Flaschensammlung wurden beide verlegt.",
            AuthorId = sophie.Id, Created = now.AddDays(-4)
        });

        // ── A group post (the GP lane — GroupId set, ComponentId empty, empty audience) ─
        var groupPost = new Post
        {
            Id = Id(), ComponentId = string.Empty, GroupId = green.Id, AuthorId = carla.Id,
            Title = "Green-plot plan for spring",
            Body = "Rough plan for the shared beds:\n\n- North bed: herbs (basil, parsley)\n- South bed: tomatoes\n\nVote in the thread and I'll finalize the seed list.",
            Audience = new Audience(),   // group-lane post: owner ∪ member, no audience grants
            Created = now.AddDays(-2), LanguageCode = "en"
        };
        session.Store(groupPost);
        var groupReply = new PostReply
        {
            Id = Id(), PostId = groupPost.Id, AuthorId = anna.Id,
            Body = "Herbs in the north bed sounds right — it's shadier. I can bring basil starts.",
            Created = now.AddDays(-2).AddHours(3), LanguageCode = "en"
        };
        session.Store(groupReply);

        // A group name translation (ADR 0026 — a `GlobalAdmin`/`Translator` standing).
        session.Store(new GroupTranslation
        {
            Id = Id(), GroupId = green.Id, LanguageCode = "de",
            Name = "Straßengrün", Description = "Nachbarn, die das gemeinsame Grün neben dem Spielplatz pflegen.",
            AuthorId = sophie.Id, Created = now.AddDays(-30)
        });

        // ── Published events (+ RSVPs) — IsDraft=false so they appear in the feed ───────
        var eventAudience = () => new Audience(AudienceMode.Any, Array.Empty<AudienceGrant>()) { AllResidents = true };

        var cleanup = new Event
        {
            Id = Id(), Title = "Community Cleanup Day",
            Body = "Gloves and bags provided. Meet at the gate **09:30**, done by **12:00**.\n\nCoffee and pastries afterwards at the community room.",
            ComponentId = "safety", AuthorId = maria.Id,
            Start = now.AddDays(5), End = now.AddDays(5).AddHours(3),
            Location = "Back lane, near the gate", Capacity = 20,
            Audience = eventAudience(),
            ReminderEnabled = true, IsDraft = false, IsDeleted = false,
            LanguageCode = "en", TagIds = [tagCleanup.Id],
            Created = now.AddDays(-2), Modified = now
        };
        var potluck = new Event
        {
            Id = Id(), Title = "Potluck in the Green",
            Body = "One dish each, arrive from **14:00**. Bring a chair if you have one.",
            ComponentId = "social", AuthorId = carla.Id,
            Start = now.AddDays(12), End = now.AddDays(12).AddHours(3),
            Location = "The shared green, under the tree", Capacity = null,
            Audience = eventAudience(),
            ReminderEnabled = true, IsDraft = false, IsDeleted = false,
            LanguageCode = "en", Created = now.AddDays(-1)
        };
        session.Store(cleanup);
        session.Store(potluck);

        // RSVPs — one row per (event, resident); a mix of Going / Maybe / No.
        session.Store(new EventRsvp { Id = Id(), EventId = cleanup.Id, UserId = anna.Id, Status = RsvpStatus.Going, At = now.AddDays(-1) });
        session.Store(new EventRsvp { Id = Id(), EventId = cleanup.Id, UserId = ben.Id, Status = RsvpStatus.Going, At = now.AddDays(-1).AddHours(2) });
        session.Store(new EventRsvp { Id = Id(), EventId = cleanup.Id, UserId = carla.Id, Status = RsvpStatus.Maybe, At = now });
        session.Store(new EventRsvp { Id = Id(), EventId = cleanup.Id, UserId = david.Id, Status = RsvpStatus.No, At = now });
        session.Store(new EventRsvp { Id = Id(), EventId = potluck.Id, UserId = carla.Id, Status = RsvpStatus.Going, At = now });
        session.Store(new EventRsvp { Id = Id(), EventId = potluck.Id, UserId = anna.Id, Status = RsvpStatus.Going, At = now.AddHours(1) });
        session.Store(new EventRsvp { Id = Id(), EventId = potluck.Id, UserId = ben.Id, Status = RsvpStatus.Maybe, At = now.AddHours(2) });

        // ── A resident blog (PageKind.User) — the /blog feed surface (ADR 0040) ─────────
        // Root page = Kind=User, ParentId=null, AuthorId=anna (GetBlogRootAsync shape).
        var blogRoot = new Page
        {
            Id = Id(), ParentId = null, Slug = "my-corner",
            Title = "Anna's corner of the street",
            Body = "A little space for notes about the block — the kind of thing you'd otherwise post on the group chat and then no one finds again.",
            Audience = null,           // public (world-readable) — a blog page may be public
            AuthorId = anna.Id, Kind = PageKind.User,
            ComponentId = null, LanguageCode = "en",
            Created = now.AddDays(-6), Modified = now.AddDays(-6),
            IsDraft = false, IsDeleted = false
        };
        var blogPost = new Page
        {
            Id = Id(), ParentId = blogRoot.Id, Slug = "the-bench-by-the-gate",
            Title = "The bench by the gate",
            Body = "There's a bench most of us have stopped noticing. It gets the sun first in the morning and the pigeons claim it by nine.\n\nI keep meaning to wipe it down for the people who use it to read. Small thing, but it's ours.",
            Audience = null,
            AuthorId = anna.Id, Kind = PageKind.User,
            ComponentId = null, LanguageCode = "en", TagIds = [tagRecipe.Id],
            Created = now.AddDays(-3), Modified = now.AddDays(-3),
            IsDraft = false, IsDeleted = false
        };
        session.Store(blogRoot);
        session.Store(blogPost);
        // A page translation (ADR 0039 lane — a `GlobalAdmin`/`Translator` standing).
        session.Store(new PageTranslation
        {
            Id = Id(), PageId = blogRoot.Id, LanguageCode = "de",
            Title = "Annas Ecke der Straße",
            Body = "Ein kleiner Raum für Notizen über den Block — genau die Dinge, die man sonst in den Gruppenchat schreibt und dann niemand wieder findet.",
            AuthorId = sophie.Id, Created = now.AddDays(-5)
        });

        // ── Sample-content expansion (doubling the neighborhood) ─────────────────────

        // Enable every catalog language for the demo. Danish ships DISABLED
        // (FirstBootSeeder seeds it awaiting an admin's enable); the sample wants
        // the full selector, so flip it on here. Dev-only: this session only ever
        // runs under the Development ∧ first-boot gate, so a real deployment's
        // catalog is left to its own admin. Load-then-Store (idempotent shape).
        var danish = await session.LoadAsync<LanguageCatalog>("da", ct);
        if (danish is not null && !danish.Enabled)
        {
            danish.Enabled = true;
            session.Store(danish);
        }

        // Three more tags — each with a full de/fr/da translation matrix so the
        // now-enabled Danish lane is visibly exercised in the demo.
        var tagRepair   = new Tag { Id = Id(), Slug = "repair",   Name = "Repair",   LanguageCode = "en", CreatedBy = ben.Id,   Created = now.AddDays(-25) };
        var tagQuestion = new Tag { Id = Id(), Slug = "question", Name = "Question", LanguageCode = "en", CreatedBy = david.Id, Created = now.AddDays(-25) };
        var tagGarden   = new Tag { Id = Id(), Slug = "garden",   Name = "Garden",   LanguageCode = "en", CreatedBy = carla.Id, Created = now.AddDays(-25) };
        session.Store(tagRepair);
        session.Store(tagQuestion);
        session.Store(tagGarden);
        session.Store(new TagTranslation { Id = Id(), TagId = tagRepair.Id,   LanguageCode = "de", Name = "Reparatur", AuthorId = sophie.Id, Created = now.AddDays(-24) });
        session.Store(new TagTranslation { Id = Id(), TagId = tagRepair.Id,   LanguageCode = "fr", Name = "Réparation", AuthorId = sophie.Id, Created = now.AddDays(-24) });
        session.Store(new TagTranslation { Id = Id(), TagId = tagRepair.Id,   LanguageCode = "da", Name = "Reparation", AuthorId = sophie.Id, Created = now.AddDays(-24) });
        session.Store(new TagTranslation { Id = Id(), TagId = tagQuestion.Id, LanguageCode = "de", Name = "Frage",     AuthorId = sophie.Id, Created = now.AddDays(-24) });
        session.Store(new TagTranslation { Id = Id(), TagId = tagQuestion.Id, LanguageCode = "fr", Name = "Question",  AuthorId = sophie.Id, Created = now.AddDays(-24) });
        session.Store(new TagTranslation { Id = Id(), TagId = tagQuestion.Id, LanguageCode = "da", Name = "Spørgsmål", AuthorId = sophie.Id, Created = now.AddDays(-24) });
        session.Store(new TagTranslation { Id = Id(), TagId = tagGarden.Id,   LanguageCode = "de", Name = "Garten",    AuthorId = sophie.Id, Created = now.AddDays(-24) });
        session.Store(new TagTranslation { Id = Id(), TagId = tagGarden.Id,   LanguageCode = "fr", Name = "Jardin",    AuthorId = sophie.Id, Created = now.AddDays(-24) });
        session.Store(new TagTranslation { Id = Id(), TagId = tagGarden.Id,   LanguageCode = "da", Name = "Have",      AuthorId = sophie.Id, Created = now.AddDays(-24) });

        // Three more announcements — the first carries a full de/fr/da translation
        // set so every enabled language shows up in the demo, not just de/fr.
        var waterDrop = new Announcement
        {
            Id = Id(), AuthorId = ben.Id,
            Title = "Water pressure drop on Friday morning",
            Body = "The supply will be interrupted **Friday 07:00–11:00** for main-line work. Keep a jug of water on hand.",
            Scope = AnnouncementScope.Community, CommunityId = null, Pinned = false,
            LanguageCode = "en", Created = now.AddDays(-2)
        };
        var roomHours = new Announcement
        {
            Id = Id(), AuthorId = admin.Id,
            Title = "Community room now open evenings",
            Body = "From next week the room is open **weekdays 18:00–22:00** for anyone who wants a quiet place to work or read.",
            Scope = AnnouncementScope.Community, CommunityId = null, Pinned = false,
            LanguageCode = "en", Created = now.AddDays(-1)
        };
        var umbrella = new Announcement
        {
            Id = Id(), AuthorId = carla.Id,
            Title = "Lost & found — a black umbrella",
            Body = "Someone left a black umbrella by the notice board. It's safe with me — claim it at the green.",
            Scope = AnnouncementScope.Community, CommunityId = "social", Pinned = false,
            LanguageCode = "en", Created = now
        };
        session.Store(waterDrop);
        session.Store(roomHours);
        session.Store(umbrella);
        session.Store(new AnnouncementTranslation
        {
            Id = Id(), AnnouncementId = waterDrop.Id, LanguageCode = "de",
            Title = "Wasserausfall am Freitagmorgen",
            Body = "Die Wasserversorgung wird **freitags von 07:00 bis 11:00** wegen Arbeiten an der Hauptleitung unterbrochen. Halten Sie eine Flasche Wasser bereit.",
            AuthorId = sophie.Id, Created = now.AddDays(-2)
        });
        session.Store(new AnnouncementTranslation
        {
            Id = Id(), AnnouncementId = waterDrop.Id, LanguageCode = "fr",
            Title = "Baisse de pression le vendredi matin",
            Body = "La distribution sera interrompue **vendredi de 07:00 à 11:00** pour des travaux sur la conduite principale. Gardez une bouteille d'eau à portée de main.",
            AuthorId = sophie.Id, Created = now.AddDays(-2)
        });
        session.Store(new AnnouncementTranslation
        {
            Id = Id(), AnnouncementId = waterDrop.Id, LanguageCode = "da",
            Title = "Vandafspærring fredag morgen",
            Body = "Vandforsyningen er afbrudt **fredag kl. 07:00–11:00** pga. hovedledningsarbejde. Hold en flaske vand klar.",
            AuthorId = sophie.Id, Created = now.AddDays(-2)
        });
        session.Store(new AnnouncementTranslation
        {
            Id = Id(), AnnouncementId = roomHours.Id, LanguageCode = "de",
            Title = "Gemeinschaftsraum jetzt auch abends offen",
            Body = "Ab nächster Woche ist der Raum **werktags von 18:00 bis 22:00** für alle offen, die einen ruhigen Ort zum Arbeiten oder Lesen suchen.",
            AuthorId = sophie.Id, Created = now
        });
        session.Store(new AnnouncementTranslation
        {
            Id = Id(), AnnouncementId = roomHours.Id, LanguageCode = "fr",
            Title = "La salle communautaire ouverte en soirée",
            Body = "À partir de la semaine prochaine, la salle est ouverte **en semaine de 18:00 à 22:00** pour quiconque cherche un endroit calme pour travailler ou lire.",
            AuthorId = sophie.Id, Created = now
        });
        session.Store(new AnnouncementTranslation
        {
            Id = Id(), AnnouncementId = umbrella.Id, LanguageCode = "de",
            Title = "Fundsachen — ein schwarzer Schirm",
            Body = "Jemand hat einen schwarzen Schirm neben dem Schwarzen Brett liegen gelassen. Er ist bei mir sicher — hol ihn am Grün ab.",
            AuthorId = sophie.Id, Created = now
        });
        session.Store(new AnnouncementTranslation
        {
            Id = Id(), AnnouncementId = umbrella.Id, LanguageCode = "fr",
            Title = "Objets trouvés — un parapluie noir",
            Body = "Quelqu'un a laissé un parapluie noir près du panneau d'affichage. Il est en sécurité chez moi — venez le récupérer au jardin.",
            AuthorId = sophie.Id, Created = now
        });

        // Three more community posts (+ replies) across the boards.
        var postElectrician = new Post
        {
            Id = Id(), ComponentId = "maintenance", AuthorId = ben.Id,
            Title = "Know a good local electrician?",
            Body = "Our kitchen fuse keeps tripping. If anyone has used a local electrician this year, I'd be grateful for a recommendation.",
            Audience = communityAudience(),
            Created = now.AddDays(-3), LanguageCode = "en", TagIds = [tagRepair.Id, tagQuestion.Id]
        };
        var postSeedlings = new Post
        {
            Id = Id(), ComponentId = "maintenance", AuthorId = carla.Id,
            Title = "Free seedlings in the recycling corner",
            Body = "I've got a tray of tomato and basil seedlings on the shelf by the bins — first come, first served.",
            Audience = communityAudience(),
            Created = now.AddDays(-1), LanguageCode = "en", TagIds = [tagGarden.Id]
        };
        var postStreetName = new Post
        {
            Id = Id(), ComponentId = "governance", AuthorId = david.Id,
            Title = "Where does the street name come from?",
            Body = "Half the block calls it one thing, the map says another. Does anyone know the story behind it?",
            Audience = communityAudience(),
            Created = now, LanguageCode = "en", TagIds = [tagQuestion.Id]
        };
        session.Store(postElectrician);
        session.Store(postSeedlings);
        session.Store(postStreetName);
        session.Store(new PostReply { Id = Id(), PostId = postElectrician.Id, AuthorId = anna.Id, Body = "The one on the corner — he fixed my boiler last spring, fair price.", Created = now.AddDays(-2), LanguageCode = "en" });
        session.Store(new PostReply { Id = Id(), PostId = postSeedlings.Id, AuthorId = david.Id, Body = "Just took two! Thanks Carla.", Created = now.AddHours(-6), LanguageCode = "en" });
        session.Store(new PostReply { Id = Id(), PostId = postStreetName.Id, AuthorId = carla.Id, Body = "It's named after the old mill at the river bend, I'm fairly sure.", Created = now.AddHours(-3), LanguageCode = "en" });
        session.Store(new PostReply { Id = Id(), PostId = postStreetName.Id, AuthorId = anna.Id, Body = "That matches the plaque at the gate — nice.", Created = now.AddHours(-1), LanguageCode = "en" });

        // A post + reply translation so the extra posts carry a non-English view too.
        session.Store(new PostTranslation
        {
            Id = Id(), PostId = postElectrician.Id, LanguageCode = "de",
            Title = "Kennt ihr einen guten lokalen Elektriker?",
            Body = "Unsere Küchen-Sicherung springt ständig raus. Wenn jemand dieses Jahr einen Elektriker in der Nähe genutzt hat, wäre ich für eine Empfehlung dankbar.",
            AuthorId = sophie.Id, Created = now.AddDays(-2)
        });

        // A second group post (the GP lane) under Street Green + a reply.
        var groupPostCompost = new Post
        {
            Id = Id(), ComponentId = string.Empty, GroupId = green.Id, AuthorId = david.Id,
            Title = "Compost bin — who's on this week?",
            Body = "The bin is ready to swap. Can anyone take a turn turning it this week? It's due on Monday.",
            Audience = new Audience(),   // group-lane post: owner ∪ member, no audience grants
            Created = now.AddDays(-1), LanguageCode = "en"
        };
        session.Store(groupPostCompost);
        session.Store(new PostReply { Id = Id(), PostId = groupPostCompost.Id, AuthorId = carla.Id, Body = "I'll do the turn on Monday morning.", Created = now, LanguageCode = "en" });

        // Two more published events (+ RSVPs) so the calendar has a fuller arc.
        var toolLibrary = new Event
        {
            Id = Id(), Title = "Tool Library Launch",
            Body = "Drills, ladders, pressure washers — borrow instead of buying. Meet in the community room to set up the shelf.",
            ComponentId = "maintenance", AuthorId = maria.Id,
            Start = now.AddDays(9), End = now.AddDays(9).AddHours(2),
            Location = "Community room", Capacity = 15,
            Audience = eventAudience(),
            ReminderEnabled = true, IsDraft = false, IsDeleted = false,
            LanguageCode = "en", TagIds = [tagRepair.Id],
            Created = now.AddDays(-1), Modified = now
        };
        var walk = new Event
        {
            Id = Id(), Title = "Neighborhood Walk",
            Body = "A slow loop of the block, coffee at the end. Strollers welcome — it's flat the whole way.",
            ComponentId = "social", AuthorId = carla.Id,
            Start = now.AddDays(18), End = now.AddDays(18).AddHours(1),
            Location = "Meeting at the green", Capacity = null,
            Audience = eventAudience(),
            ReminderEnabled = true, IsDraft = false, IsDeleted = false,
            LanguageCode = "en", Created = now
        };
        session.Store(toolLibrary);
        session.Store(walk);
        session.Store(new EventRsvp { Id = Id(), EventId = toolLibrary.Id, UserId = ben.Id,   Status = RsvpStatus.Going, At = now });
        session.Store(new EventRsvp { Id = Id(), EventId = toolLibrary.Id, UserId = anna.Id,  Status = RsvpStatus.Going, At = now.AddHours(1) });
        session.Store(new EventRsvp { Id = Id(), EventId = toolLibrary.Id, UserId = david.Id, Status = RsvpStatus.Maybe, At = now });
        session.Store(new EventRsvp { Id = Id(), EventId = walk.Id, UserId = anna.Id,  Status = RsvpStatus.Going, At = now });
        session.Store(new EventRsvp { Id = Id(), EventId = walk.Id, UserId = carla.Id, Status = RsvpStatus.Going, At = now.AddHours(1) });
        session.Store(new EventRsvp { Id = Id(), EventId = walk.Id, UserId = maria.Id, Status = RsvpStatus.Maybe, At = now.AddHours(2) });

        // A second resident blog post (+ a German translation) under Anna's root.
        var blogPost2 = new Page
        {
            Id = Id(), ParentId = blogRoot.Id, Slug = "the-corner-shops-bell",
            Title = "The corner shop's old bell",
            Body = "The little bell above the old corner shop still rings when someone opens the door. No one runs it now, but the sound has stayed — a small reminder the street used to be busier.",
            Audience = null,
            AuthorId = anna.Id, Kind = PageKind.User,
            ComponentId = null, LanguageCode = "en",
            Created = now.AddDays(-1), Modified = now.AddDays(-1),
            IsDraft = false, IsDeleted = false
        };
        session.Store(blogPost2);
        session.Store(new PageTranslation
        {
            Id = Id(), PageId = blogPost2.Id, LanguageCode = "de",
            Title = "Die alte Glocke des Eckladens",
            Body = "Die kleine Glocke über dem alten Eckladen klingelt immer noch, wenn jemand die Tür öffnet. Niemand führt den Laden mehr, aber der Klang ist geblieben — eine kleine Erinnerung daran, dass die Straße einmal geschäftiger war.",
            AuthorId = sophie.Id, Created = now
        });

        await session.SaveChangesAsync();

        logger.LogInformation(
            "Development sample data: complete. Accounts — {Admin} ({AdminPassword}), {Mod} ({ModPassword}), {Trans} ({TransPassword}), " +
            "anna/ben/carla/david@examplium.com (all {ResPassword}).",
            AdminEmail, AdminPassword, ModeratorEmail, ModeratorPassword, TranslatorEmail, TranslatorPassword, ResidentPassword);
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
        string password,
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
            var pwResult = await userManager.AddPasswordAsync(existing, password);
            if (!pwResult.Succeeded)
                throw new InvalidOperationException(
                    $"Failed to set the demo password for '{email}': {string.Join(", ", pwResult.Errors.Select(e => e.Description))}");
            if (elevatedRole is not null)
                await userManager.AddToRoleAsync(existing, elevatedRole);
        }
        else
        {
            // Idempotent: add the password only if the account has none (the seeded
            // admin is created without a password — the setup-token lane).
            if (string.IsNullOrEmpty(existing.PasswordHash))
            {
                if (string.IsNullOrEmpty(password))
                    throw new InvalidOperationException($"No password supplied for existing account '{email}'.");
                var pwResult = await userManager.AddPasswordAsync(existing, password);
                if (!pwResult.Succeeded)
                    throw new InvalidOperationException(
                        $"Failed to set the demo password for '{email}': {string.Join(", ", pwResult.Errors.Select(e => e.Description))}");
            }
            if (elevatedRole is not null)
                await userManager.AddToRoleAsync(existing, elevatedRole);
        }

        // The mt-side Profile — verified resident, self-only visibility default, optional
        // opt-in contact block + tz/format overrides.
        await using var session = mt.OpenSession(new SessionOptions());
        var profile = await session.LoadAsync<Profile>(existing.Id, ct);
        profile ??= new Profile { SubjectId = existing.Id };
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
}
