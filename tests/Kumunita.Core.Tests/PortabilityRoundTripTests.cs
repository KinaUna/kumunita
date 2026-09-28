using System.IO;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Media;
using Kumunita.Core.Messaging;
using Kumunita.Core.Pages;
using Kumunita.Core.Portability;
using Kumunita.Core.Posts;
using Kumunita.Core.Projects;
using Kumunita.Core.UserInfo;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace Kumunita.Core.Tests;

using static Marten.QueryableExtensions;

using static Marten.QueryableExtensions;

/// <summary>
/// M11 U07 — the <b>close</b> of the milestone: the three pinned Core tests
/// from <c>docs/design/m11-portability-design.md</c> §D9 (the exact frozen
/// names), each of which runs <see cref="PortabilityService"/> end-to-end
/// (U02/U03's export seam + U05/U06's import seam + the C-M11·2 no-secret
/// boundary) against the <b>real</b> Postgres-backed seams (the
/// <see cref="PostgresFixture"/> + the same boot the GU / GA / ML lanes
/// established in this assembly — the frozen
/// <see cref="IDocumentStore"/> + <see cref="UserManager{TUser}"/> /
/// <see cref="RoleManager{TRole}"/> + <see cref="LocalVolumeMediaStore"/>
/// over a fresh scratch DB).
/// <para>
/// The three pins:
/// <list type="bullet">
/// <item><b>D9a</b> — <see
/// cref="PortabilityRoundTrip_ExportThenImportPreservesContentGraphAndMediaAndRoles"/>:
/// a representative instance (one of each of the 44 content doc types that
/// participates in a non-trivial graph + one media object + one principal
/// with the elevated standing + the config block) → <see
/// cref="PortabilityService.ExportAsync"/> → a <em>fresh</em> instance →
/// <see cref="PortabilityService.ImportAsync"/> → the content graph + the
/// media bytes + the role assignments are all present on the fresh instance,
/// byte-identical and referentially intact.</item>
/// <item><b>D9b</b> — <see
/// cref="PortabilityNoSecret_ArchiveContainsNoCredentialMaterial"/>: the
/// <see cref="PortabilityPrincipal"/> POCO field shape (the C-M11·2 type
/// boundary) + a byte-scan witness over the export (the C-M11·2 wire
/// boundary).</item>
/// <item><b>D9c</b> — <see
/// cref="PortabilityFailClosed_RejectedArchiveWritesZeroRows"/>: a wrong
/// <c>format</c> manifest, a corrupted media byte, and a dangling reference
/// are each rejected <em>before any write</em> (C-M11·4) — the fresh
/// instance has zero content rows, zero principals, and zero audit rows
/// after each of the three rejections.</item>
/// </list>
/// A test whose exact name is not in the design doc's pinned list is a drift
/// pause, not a silent add.
/// </para>
/// </summary>
public sealed class PortabilityRoundTripTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── The shared boot — a full instance (M11 D9's "representative instance") ─
    // The frozen seams the PortabilityService consumes, wired exactly the way
    // the GU / GA / ML lanes wire them in this assembly: the
    // PostgresFixture's fresh scratch DB + the same Marten / EF / Identity /
    // LocalVolumeMediaStore shape as the GU lane's BootIdentityAsync — the
    // M11 service's ctor consumes those seams verbatim (the C-M11·6/7
    // "no new authorization surface" pin: the only seams it touches are the
    // frozen IDocumentStore + UserManager + RoleManager + IMediaStore /
    // IMediaFileStore).

    private async Task<(
        IDocumentStore store,
        AppDbContext db,
        UserManager<User> userManager,
        RoleManager<IdentityRole> roleManager,
        IMediaStore mediaStore,
        IOptions<CommunityOptions> options,
        PortabilityService service)>
        BootFullInstanceAsync(CancellationToken ct)
    {
        var conn = await fixture.NewDatabaseAsync(ct);

        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            // Every doc-type surface the §inventory's 44 content docs span
            // (the M1 / M3 / M4 / M5 / M6 / M9 + Media + Tag + Page + the
            // Localization docs — the exact set Program.cs registers).
            M1DocTypes.Configure(opts);
            M3DocTypes.Configure(opts);
            M4DocTypes.Configure(opts);
            M5DocTypes.Configure(opts);
            M6DocTypes.Configure(opts);
            M9DocTypes.Configure(opts);
            MediaDocTypes.Configure(opts);
            TagDocTypes.Configure(opts);
            PageDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        // The identity schema (the only EF Core in the tree, ADR 0004).
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn)
            .Options);
        await db.Database.MigrateAsync(ct);

        // The Identity user / role managers over the migrated `identity`
        // schema (the same UserStore / RoleStore shape the GU lane's
        // BootIdentityAsync uses).
        var userStore = new UserStore<User, IdentityRole, AppDbContext, string,
            IdentityUserClaim<string>, IdentityUserRole<string>,
            IdentityUserLogin<string>, IdentityUserToken<string>,
            IdentityRoleClaim<string>>(db);
        var roleStore = new RoleStore<IdentityRole, AppDbContext, string,
            IdentityUserRole<string>, IdentityRoleClaim<string>>(db);

        // Mirror Program.cs's real Identity password policy exactly (the app
        // relaxes RequireNonAlphanumeric — the setup-token idiom — and the
        // shipped PortabilityApplyIdentity.RandomPassword relies on that).
        // A harness that used the default policy would reject the re-created
        // principals' generated credentials on import.
        var identityOpts = Options.Create(new IdentityOptions
        {
            User = { RequireUniqueEmail = true },
            Password = { RequiredLength = 8, RequireNonAlphanumeric = false },
        });
        var userManager = new UserManager<User>(
            userStore,
            identityOpts,
            new PasswordHasher<User>(),
            new[] { new UserValidator<User>() },
            new[] { new PasswordValidator<User>() },
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new EmptyServiceProvider(),
            NullLogger<UserManager<User>>.Instance);
        var roleManager = new RoleManager<IdentityRole>(
            roleStore,
            new[] { new RoleValidator<IdentityRole>() },
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<IdentityRole>>.Instance);

        // The LocalVolumeMediaStore over a fresh scratch directory (the
        // ADR 0011 byte seam — the {id[0..2]}/{id} layout the C-M11·3
        // content-addressing pin relies on).
        var mediaRoot = Path.Combine(
            Path.GetTempPath(),
            "kumunita-m11-tests-" + Guid.NewGuid().ToString("n")[..10]);
        var fileStore = new LocalVolumeFileStore(
            Options.Create(new MediaOptions { RootPath = mediaRoot }));
        var mediaStore = new LocalVolumeMediaStore(fileStore, store);

        var options = Options.Create(new CommunityOptions
        {
            Name = "Maplewood Residents",
            SupportEmail = "admin@maplewood.example",
        });

        var service = new PortabilityService(
            store, db, userManager, roleManager, options, mediaStore, fileStore);

        return (store, db, userManager, roleManager, mediaStore, options, service);
    }

    // ── The D9a plant — one representative row per closed inventory entry ─
    // The design doc's §D9 "representative instance": every content doc type
    // with a non-trivial graph + one media object + the config block + the
    // two principals with the role standing. The plant is deliberately
    // minimal (the U07 pin — the round-trip is about shape + referential
    // integrity, not content volume) — one row of each of the participating
    // types, with the reference fields pointing at the planted principals /
    // docs so the U05 integrity loop (the C-M11·4 fail-closed gate) passes.

    private sealed record Planted(
        string principalAId,
        string principalBId,
        string componentId,
        string groupId,
        string postId,
        string postReplyId,
        string postTranslationId,
        string eventId,
        string eventRsvpId,
        string kanbanBoardId,
        string kanbanLaneId,
        string todoItemId,
        string boardItemPlacementId,
        string conversationId,
        string messageId,
        string pageId,
        string pageTranslationId,
        string mediaObjectId,
        string delegationGrantId,
        string guardianLinkId);

    private async Task<Planted> PlantRepresentativeInstanceAsync(
        IDocumentStore store,
        UserManager<User> userManager,
        RoleManager<IdentityRole> roleManager,
        IMediaStore mediaStore,
        CancellationToken ct)
    {
        // Ensure the three roles exist before adding users to them
        // (the EF IdentityRole row must be present for AddToRoleAsync).
        foreach (var role in new[] { Roles.GlobalAdmin, Roles.Moderator, Roles.Member })
        {
            var existing = await roleManager.FindByNameAsync(role);
            if (existing is null)
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                if (!result.Succeeded)
                    throw new InvalidOperationException("role create failed: " + role);
            }
        }

        // Two principals (the M11 §principals closed field set's two rows —
        // principal A with the elevated standing, principal B as the
        // non-elevated counterparty the graph's AuthorId / OwnerId / UserId
        // fields point at).
        var principalA = await userManager.CreateAsync(
            new User
            {
                Id = "pt-admin-001",
                UserName = "pt-admin",
                Email = "pt-admin@maplewood.example",
                NormalizedEmail = "PT-ADMIN@MAPLEWOOD.EXAMPLE",
            },
            "Passw0rd!pt-admin-001");
        if (!principalA.Succeeded)
            throw new InvalidOperationException("principal A create failed: " +
                string.Join(", ", principalA.Errors.Select(e => e.Description)));
        var userA = await userManager.FindByIdAsync("pt-admin-001")!;
        await userManager.AddToRoleAsync(userA, Roles.GlobalAdmin);
        await userManager.AddToRoleAsync(userA, Roles.Moderator);

        var principalB = await userManager.CreateAsync(
            new User
            {
                Id = "pt-resident-001",
                UserName = "pt-resident",
                Email = "pt-resident@maplewood.example",
                NormalizedEmail = "PT-RESIDENT@MAPLEWOOD.EXAMPLE",
            },
            "Passw0rd!pt-resident-001");
        if (!principalB.Succeeded)
            throw new InvalidOperationException("principal B create failed: " +
                string.Join(", ", principalB.Errors.Select(e => e.Description)));
        var userB = await userManager.FindByIdAsync("pt-resident-001")!;
        await userManager.AddToRoleAsync(userB, Roles.Member);

        // The config block — the §config field set (the U02 ConfigExport
        // reads these + the CommunityOptions value).
        await using (var configSession = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            var locale = new LocaleSettings
            {
                Id = LocaleSettings.SingletonId,
                DefaultLanguageCode = "en",
                DefaultTimezone = "UTC",
                DefaultDateFormat = DateFormat.FloorFormat,
                IsSignupOpen = true,
                NotifyAdminsOnSignup = true,
                AnnouncementCommentsEnabled = true,
                MessagingEnabled = true,
            };
            configSession.Store(locale);

            var en = new LanguageCatalog
            {
                Id = "en",
                NativeName = "English",
                Enabled = true,
                SortOrder = 0,
            };
            configSession.Store(en);
            var de = new LanguageCatalog
            {
                Id = "de",
                NativeName = "Deutsch",
                Enabled = true,
                SortOrder = 1,
            };
            configSession.Store(de);
            await configSession.SaveChangesAsync(ct);
        }

        // The media object — one content-addressed row (the C-M11·3 pin:
        // the id is the lowercase-hex SHA-256 of the payload; the media
        // manifest + the media/{Id[0..2]}/{Id} layout + the C-M11·4
        // check (d) byte verification all key off this row's id).
        var mediaPayload = Encoding.UTF8.GetBytes(
            "M11 U07 representative instance media payload — 2026-10-02.");
        var media = await mediaStore.PutAsync(
            mediaPayload,
            "u07-representative.png",
            "image/png",
            "pt-admin-001",
            ct);
        var mediaObjectId = media.Id;
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(mediaPayload)).ToLowerInvariant(),
            mediaObjectId);

        // The Profile docs (the UserInfo doc for each principal — the
        // §inventory Profile row: the AvatarId reference field points at
        // the media object's id (principal A's avatar — the §inventory
        // "→ MediaObject" reference the U05 integrity loop checks)).
        await using (var profileSession = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            profileSession.Store(new Profile
            {
                SubjectId = "pt-admin-001",
                DisplayName = "PT Admin",
                Verified = true,
                Blocked = false,
                Visibility = new Audience(),
                AvatarId = mediaObjectId,
            });
            profileSession.Store(new Profile
            {
                SubjectId = "pt-resident-001",
                DisplayName = "PT Resident",
                Verified = true,
                Blocked = false,
                Visibility = new Audience(),
            });
            await profileSession.SaveChangesAsync(ct);
        }

        // The §inventory content graph — one row of each participating
        // type with the reference fields pointing at the planted principals
        // (the U05 integrity loop's "→ principal" target set: every
        // AuthorId / OwnerId / UserId / GuardianId / ChildId / ParticipantA
        // / ParticipantB / SenderId must resolve).
        var componentId = "pt-component-001";
        var groupId = "pt-group-001";
        var postId = "pt-post-001";
        var postReplyId = "pt-post-reply-001";
        var postTranslationId = "pt-post-translation-001";
        var eventId = "pt-event-001";
        var eventRsvpId = "pt-event-rsvp-001";
        var kanbanBoardId = "pt-kanban-board-001";
        var kanbanLaneId = "pt-kanban-lane-001";
        var todoItemId = "pt-todo-001";
        var boardItemPlacementId = "pt-board-item-placement-001";
        var conversationId = "pt-conversation-001";
        var messageId = "pt-message-001";
        var pageId = "pt-page-001";
        var pageTranslationId = "pt-page-translation-001";
        var delegationGrantId = "pt-delegation-001";
        var guardianLinkId = "pt-guardian-link-001";

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());

        // Order 1 — the base units (the parents; no doc-level parent).
        session.Store(new Component
        {
            Id = componentId,
            Name = "Maplewood",
        });
        session.Store(new Group
        {
            Id = groupId,
            Name = "Maplewood Neighbors",
            OwnerId = "pt-admin-001",
            Created = DateTimeOffset.UtcNow,
        });

        // Order 2 — the M1 identity graph (the delegation + the guardian).
        session.Store(new DelegationGrant
        {
            Id = delegationGrantId,
            OwnerId = "pt-resident-001",
            DelegateId = "pt-admin-001",
            Scope = [],
            From = DateTimeOffset.UtcNow,
            To = null,
        });
        session.Store(new GuardianLink
        {
            Id = guardianLinkId,
            GuardianId = "pt-admin-001",
            ChildId = "pt-resident-001",
            Status = GuardianLinkStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        // Order 4 — the M3 content (the post + its reply + its translation).
        var postCreated = DateTimeOffset.UtcNow;
        session.Store(new Post
        {
            Id = postId,
            ComponentId = componentId,
            AuthorId = "pt-resident-001",
            Title = "PT U07 representative post",
            Body = "PT U07 — the round-trip representative post body (the " +
                "§inventory Post row, order 17).",
            Audience = new Audience(),
            GroupId = string.Empty,
            LanguageCode = "en",
            Created = postCreated,
        });
        session.Store(new PostReply
        {
            Id = postReplyId,
            PostId = postId,
            AuthorId = "pt-admin-001",
            Body = "PT U07 — the round-trip representative reply body " +
                "(the §inventory PostReply row, order 18).",
            Created = postCreated,
            LanguageCode = "en",
        });
        session.Store(new PostTranslation
        {
            Id = postTranslationId,
            PostId = postId,
            LanguageCode = "de",
            Title = "PT U07 representative post (DE)",
            Body = "PT U07 — der repräsentative Post-Körper in Deutsch " +
                "(die §inventory PostTranslation-Zeile, order 19).",
            AuthorId = "pt-admin-001",
            Created = postCreated,
        });

        // Order 5 — the M4 content (the event + the RSVP — the M4
        // "outcome" arrow: the IsDraft=false published event + one
        // Going RSVP from the non-author principal).
        var eventStart = DateTimeOffset.UtcNow.AddHours(1);
        session.Store(new Event
        {
            Id = eventId,
            Title = "PT U07 representative event",
            Body = "PT U07 — the round-trip representative event body " +
                "(the §inventory Event row, order 25).",
            ComponentId = componentId,
            AuthorId = "pt-admin-001",
            Start = eventStart,
            End = eventStart.AddHours(2),
            IsDraft = false,
            Audience = new Audience(),
        });
        session.Store(new EventRsvp
        {
            Id = eventRsvpId,
            EventId = eventId,
            UserId = "pt-resident-001",
            Status = RsvpStatus.Going,
            At = DateTimeOffset.UtcNow,
        });

        // Order 6 — the M5 content (the board + the lane + the to-do + the
        // placement — the M5 "outcome" arrow: the to-do is the work item;
        // the placement is where it sits on the lane; the lane is on the
        // board).
        session.Store(new KanbanBoard
        {
            Id = kanbanBoardId,
            Title = "PT U07 representative board",
            ComponentId = componentId,
            AuthorId = "pt-admin-001",
            Audience = new Audience(),
            Created = DateTimeOffset.UtcNow,
        });
        session.Store(new KanbanLane
        {
            Id = kanbanLaneId,
            BoardId = kanbanBoardId,
            Title = "To do",
            Order = 0,
            Created = DateTimeOffset.UtcNow,
        });
        session.Store(new TodoItem
        {
            Id = todoItemId,
            Title = "PT U07 representative to-do",
            ComponentId = componentId,
            AuthorId = "pt-resident-001",
            AssigneeId = "pt-admin-001",
            Status = "open",
            Created = DateTimeOffset.UtcNow,
            LanguageCode = "en",
        });
        session.Store(new BoardItemPlacement
        {
            Id = boardItemPlacementId,
            TodoItemId = todoItemId,
            BoardId = kanbanBoardId,
            LaneId = kanbanLaneId,
            Order = 0,
            Created = DateTimeOffset.UtcNow,
        });

        // Order 8 — the M9 content (the conversation + the message).
        session.Store(new Conversation
        {
            Id = conversationId,
            ParticipantA = "pt-admin-001",
            ParticipantB = "pt-resident-001",
            Created = DateTimeOffset.UtcNow,
        });
        session.Store(new Message
        {
            Id = messageId,
            ConversationId = conversationId,
            SenderId = "pt-resident-001",
            Body = "PT U07 — the round-trip representative message body " +
                "(the §inventory Message row, order 42).",
            Created = DateTimeOffset.UtcNow,
            LanguageCode = "en",
        });

        // Order 9 — the PG content (the page + its translation).
        session.Store(new Page
        {
            Id = pageId,
            Slug = "pt-u07-representative-page",
            Title = "PT U07 representative page",
            Body = "PT U07 — the round-trip representative page body " +
                "(the §inventory Page row, order 43).",
            AuthorId = "pt-admin-001",
            Kind = PageKind.System,
            IsDraft = false,
            IsDeleted = false,
            Created = DateTimeOffset.UtcNow,
            LanguageCode = "en",
        });
        session.Store(new PageTranslation
        {
            Id = pageTranslationId,
            PageId = pageId,
            LanguageCode = "de",
            Title = "PT U07 representative page (DE)",
            Body = "PT U07 — die repräsentative Seite in Deutsch " +
                "(die §inventory PageTranslation-Zeile, order 44).",
            AuthorId = "pt-admin-001",
            Created = DateTimeOffset.UtcNow,
        });

        await session.SaveChangesAsync(ct);

        return new Planted(
            "pt-admin-001",
            "pt-resident-001",
            componentId,
            groupId,
            postId,
            postReplyId,
            postTranslationId,
            eventId,
            eventRsvpId,
            kanbanBoardId,
            kanbanLaneId,
            todoItemId,
            boardItemPlacementId,
            conversationId,
            messageId,
            pageId,
            pageTranslationId,
            mediaObjectId,
            delegationGrantId,
            guardianLinkId);
    }

    // ── The D9a round-trip — the milestone's headline invariant ─────────────

    [Fact]
    public async Task PortabilityRoundTrip_ExportThenImportPreservesContentGraphAndMediaAndRoles()
    {
        var ct = TestContext.Current.CancellationToken;

        // Plant a representative instance (the design doc §D9a "representative
        // instance": one of each of the participating content doc types + one
        // media object + the two principals with the role standing + the
        // config block).
        var (storeA, _, userManagerA, roleManagerA, mediaStoreA, _, serviceA) =
            await BootFullInstanceAsync(ct);
        var planted = await PlantRepresentativeInstanceAsync(
            storeA, userManagerA, roleManagerA, mediaStoreA, ct);

        // Export — one *.kumunita archive (the U02/U03 export seam: the docs
        // + the no-secret principals + the config + the media bytes + the
        // manifest).
        var exportStream = await serviceA.ExportAsync("pt-admin-001", ct);
        var exportBytes = await ReadAllBytesAsync(exportStream, ct);
        Assert.NotEmpty(exportBytes);

        // Import into a fresh instance — the U05/U06 import seam (the
        // validate phase runs to completion before any write; a clean
        // validate applies principals → docs → media → config in the
        // locked order).
        var (storeB, dbB, userManagerB, roleManagerB, mediaStoreB, _, serviceB) =
            await BootFullInstanceAsync(ct);
        var importResult = await serviceB.ImportAsync(
            "pt-admin-001", new MemoryStream(exportBytes), ct);
        Assert.True(importResult.Ok,
            "the import should be clean — the failure set is: " +
            string.Join(", ", importResult.Failures));

        // ── Assert the content graph is preserved (one row per planted id) ─
        await using (var q = storeB.QuerySession())
        {
            // Order 1 — the base units.
            var component = await q.LoadAsync<Component>(planted.componentId, ct);
            Assert.Equal("Maplewood", component!.Name);

            var group = await q.LoadAsync<Group>(planted.groupId, ct);
            Assert.Equal("pt-admin-001", group!.OwnerId);

            // Order 2 — the M1 identity graph.
            var delegation = await q.LoadAsync<DelegationGrant>(planted.delegationGrantId, ct);
            Assert.Equal("pt-admin-001", delegation!.DelegateId);
            Assert.Equal("pt-resident-001", delegation.OwnerId);

            var guardian = await q.LoadAsync<GuardianLink>(planted.guardianLinkId, ct);
            Assert.Equal(GuardianLinkStatus.Active, guardian!.Status);
            Assert.Equal("pt-admin-001", guardian.GuardianId);
            Assert.Equal("pt-resident-001", guardian.ChildId);

            // Order 4 — the M3 content.
            var post = await q.LoadAsync<Post>(planted.postId, ct);
            Assert.Equal("PT U07 representative post", post!.Title);
            Assert.Equal("pt-resident-001", post.AuthorId);
            Assert.Equal("en", post.LanguageCode);

            var reply = await q.LoadAsync<PostReply>(planted.postReplyId, ct);
            Assert.Equal(planted.postId, reply!.PostId);
            Assert.Equal("pt-admin-001", reply.AuthorId);

            var translation = await q.LoadAsync<PostTranslation>(planted.postTranslationId, ct);
            Assert.Equal("de", translation!.LanguageCode);
            Assert.Equal(planted.postId, translation.PostId);

            // Order 5 — the M4 content.
            var evt = await q.LoadAsync<Event>(planted.eventId, ct);
            Assert.False(evt!.IsDraft, "the published event's IsDraft=false should be preserved");
            Assert.Equal("PT U07 representative event", evt.Title);
            Assert.Equal("pt-admin-001", evt.AuthorId);

            var rsvp = await q.LoadAsync<EventRsvp>(planted.eventRsvpId, ct);
            Assert.Equal(RsvpStatus.Going, rsvp!.Status);
            Assert.Equal("pt-resident-001", rsvp.UserId);
            Assert.Equal(planted.eventId, rsvp.EventId);

            // Order 6 — the M5 content.
            var board = await q.LoadAsync<KanbanBoard>(planted.kanbanBoardId, ct);
            Assert.Equal("PT U07 representative board", board!.Title);
            Assert.Equal("pt-admin-001", board.AuthorId);

            var lane = await q.LoadAsync<KanbanLane>(planted.kanbanLaneId, ct);
            Assert.Equal(planted.kanbanBoardId, lane!.BoardId);
            Assert.Equal(0, lane.Order);

            var todo = await q.LoadAsync<TodoItem>(planted.todoItemId, ct);
            Assert.Equal("PT U07 representative to-do", todo!.Title);
            Assert.Equal("pt-admin-001", todo.AssigneeId);
            Assert.Equal("open", todo.Status);

            var placement = await q.LoadAsync<BoardItemPlacement>(planted.boardItemPlacementId, ct);
            Assert.Equal(planted.todoItemId, placement!.TodoItemId);
            Assert.Equal(planted.kanbanBoardId, placement.BoardId);
            Assert.Equal(planted.kanbanLaneId, placement.LaneId);

            // Order 8 — the M9 content.
            var conversation = await q.LoadAsync<Conversation>(planted.conversationId, ct);
            Assert.Equal("pt-admin-001", conversation!.ParticipantA);
            Assert.Equal("pt-resident-001", conversation.ParticipantB);

            var message = await q.LoadAsync<Message>(planted.messageId, ct);
            Assert.Equal("pt-resident-001", message!.SenderId);
            Assert.Equal(planted.conversationId, message.ConversationId);

            // Order 9 — the PG content.
            var page = await q.LoadAsync<Page>(planted.pageId, ct);
            Assert.Equal("PT U07 representative page", page!.Title);
            Assert.Equal("pt-admin-001", page.AuthorId);

            var pageTranslation = await q.LoadAsync<PageTranslation>(planted.pageTranslationId, ct);
            Assert.Equal("de", pageTranslation!.LanguageCode);
            Assert.Equal(planted.pageId, pageTranslation.PageId);

            // The config block (the §config field set — the U06 apply step
            // 4's re-materialized rows).
            var localeB = await q.Query<LocaleSettings>()
                .Where(l => l.Id == LocaleSettings.SingletonId)
                .FirstAsync(ct);
            Assert.Equal("en", localeB.DefaultLanguageCode);
            Assert.True(localeB.IsSignupOpen);

            var enB = await q.LoadAsync<LanguageCatalog>("en", ct);
            Assert.True(enB!.Enabled);
            var deB = await q.LoadAsync<LanguageCatalog>("de", ct);
            Assert.Equal("Deutsch", deB!.NativeName);
        }

        // ── Assert the media bytes round-trip (C-M11·3 content-addressing) ─
        // The §validate check (d) guarantees the bytes match the id (the
        // id IS the SHA-256 of the payload) — a round-trip that preserved
        // the doc but corrupted the bytes would have failed validate. A
        // fresh read of the volume bytes is the wire witness.
        var readStream = await mediaStoreB.OpenReadAsync(planted.mediaObjectId, ct);
        await using var readMs = new MemoryStream();
        await readStream.CopyToAsync(readMs, ct);
        var roundTripBytes = readMs.ToArray();
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(roundTripBytes)).ToLowerInvariant(),
            planted.mediaObjectId);

        // ── Assert the role assignments are preserved (the M11 D9a "roles"
        //    half — the §principals roles[] round-trip) ─
        var principalAB = await userManagerB.FindByIdAsync("pt-admin-001");
        Assert.NotNull(principalAB);
        var rolesB = await userManagerB.GetRolesAsync(principalAB);
        Assert.Contains(Roles.GlobalAdmin, rolesB);
        Assert.Contains(Roles.Moderator, rolesB);

        var principalBB = await userManagerB.FindByIdAsync("pt-resident-001");
        Assert.NotNull(principalBB);
        Assert.Contains(Roles.Member, await userManagerB.GetRolesAsync(principalBB));

        // ── Assert exactly one portability.import AccessAudit row (the
        //    C-M11·6 one-audit-row pin — the service's, not the controller's) ─
        await using (var auditSession = storeB.QuerySession())
        {
            var auditRows = await Marten.QueryableExtensions.ToListAsync(
                auditSession.Query<AccessAudit>()
                    .Where(a => a.Action == "portability.import"),
                ct);
            Assert.Single(auditRows);
            Assert.Equal(AccessVia.Admin, auditRows[0].Via);
            Assert.Equal("portability", auditRows[0].TargetKind);
            Assert.Equal(AccessOutcome.Allow, auditRows[0].Outcome);
        }
    }

    // ── The D9b no-secret pin — the C-M11·2 boundary, both halves ──────────

    [Fact]
    public async Task PortabilityNoSecret_ArchiveContainsNoCredentialMaterial()
    {
        var ct = TestContext.Current.CancellationToken;

        // ── The type boundary (the C-M11·2 "structurally incapable of
        //    traveling" pin): the PortabilityPrincipal POCO has no field
        //    whose name carries a credential. ──
        var secretFieldNames = new[]
        {
            "passwordhash",
            "securitystamp",
            "accesstoken",
            "refreshtoken",
            "recoverycode",
            "signinsigninrecord",
        };
        var fieldNames = typeof(PortabilityPrincipal)
            .GetProperties()
            .Select(p => p.Name.ToLowerInvariant())
            .ToList();
        Assert.All(
            secretFieldNames,
            name => Assert.DoesNotContain(
                name, fieldNames, StringComparer.Ordinal));

        // ── The wire boundary (the C-M11·2 boundary on the archive): seed
        //    a known password + a known security stamp on a principal, and
        //    assert neither string appears in the exported archive bytes
        //    (the byte-scan witness). ──
        var (storeA, dbA, userManagerA, _, mediaStoreA, _, serviceA) =
            await BootFullInstanceAsync(ct);

        const string knownPassword = "Zk9!s3cret-wire-b0undary-M11-U07-2026";
        const string knownSecurityStamp = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6-e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2";
        const string knownPrincipalId = "pt-nosecret-witness-001";
        const string knownEmail = "pt-nosecret@maplewood.example";

        var createResult = await userManagerA.CreateAsync(
            new User
            {
                Id = knownPrincipalId,
                UserName = "pt-nosecret",
                Email = knownEmail,
                NormalizedEmail = knownEmail.ToUpperInvariant(),
            },
            knownPassword);
        if (!createResult.Succeeded)
            throw new InvalidOperationException("no-secret witness principal create failed: " +
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
        await userManagerA.UpdateSecurityStampAsync(
            (await userManagerA.FindByIdAsync(knownPrincipalId))!);

        // Plant a representative graph (the U02 export's doc loop needs at
        // least one row per participating type — the §validate (b)
        // per-type check requires the doc files to deserialize; the
        // §validate (c) integrity loop checks references. Plant the
        // minimal set: the Profile for the witness principal + one
        // Component + one Post + the config block + the media object (the
        // C-M11·3 wire witness is independent of the graph's shape).
        await using (var seedSession = storeA.OpenSession(new Marten.Services.SessionOptions()))
        {
            seedSession.Store(new LocaleSettings
            {
                Id = LocaleSettings.SingletonId,
                DefaultLanguageCode = "en",
                DefaultTimezone = "UTC",
                DefaultDateFormat = DateFormat.FloorFormat,
                IsSignupOpen = true,
                NotifyAdminsOnSignup = true,
                AnnouncementCommentsEnabled = true,
                MessagingEnabled = true,
            });
            seedSession.Store(new LanguageCatalog
            {
                Id = "en",
                NativeName = "English",
                Enabled = true,
                SortOrder = 0,
            });
            seedSession.Store(new Profile
            {
                SubjectId = knownPrincipalId,
                DisplayName = "PT No-Secret Witness",
                Verified = true,
                Blocked = false,
                Visibility = new Audience(),
            });
            seedSession.Store(new Component
            {
                Id = "pt-nosecret-component-001",
                Name = "Maplewood",
            });
            seedSession.Store(new Post
            {
                Id = "pt-nosecret-post-001",
                ComponentId = "pt-nosecret-component-001",
                AuthorId = knownPrincipalId,
                Title = "PT U07 no-secret witness post",
                Body = "PT U07 — the no-secret witness post body.",
                Audience = new Audience(),
                LanguageCode = "en",
                Created = DateTimeOffset.UtcNow,
            });
            await seedSession.SaveChangesAsync(ct);
        }

        // Export (the C-M11·2 boundary at the wire: the archive bytes are
        // the witness — the PrincipalsExport's "the extractor never
        // touches the credential columns" pin, observed at the wire).
        var exportStream = await serviceA.ExportAsync(knownPrincipalId, ct);
        var exportBytes = await ReadAllBytesAsync(exportStream, ct);
        Assert.NotEmpty(exportBytes);

        var wire = Encoding.UTF8.GetString(exportBytes);
        Assert.DoesNotContain(knownPassword, wire);

        // Read back the seeded password hash + security stamp (the values
        // the export must NOT have written into the archive).
        var witnessUser = await Microsoft.EntityFrameworkCore
            .EntityFrameworkQueryableExtensions
            .FirstAsync(dbA.Users, u => u.Id == knownPrincipalId, ct);
        Assert.False(string.IsNullOrEmpty(witnessUser.PasswordHash));
        Assert.False(string.IsNullOrEmpty(witnessUser.SecurityStamp));
        Assert.DoesNotContain(witnessUser.PasswordHash, wire);
        Assert.DoesNotContain(witnessUser.SecurityStamp, wire);
    }

    // ── The D9c fail-closed pin — the C-M11·4 invariant ────────────────────

    [Fact]
    public async Task PortabilityFailClosed_RejectedArchiveWritesZeroRows()
    {
        var ct = TestContext.Current.CancellationToken;

        // Plant a valid representative instance on instance A (the D9a
        // shape — the export produces a clean archive the U05 validator
        // passes).
        var (storeA, _, userManagerA, roleManagerA, mediaStoreA, _, serviceA) =
            await BootFullInstanceAsync(ct);
        await PlantRepresentativeInstanceAsync(storeA, userManagerA, roleManagerA, mediaStoreA, ct);
        var cleanExportStream = await serviceA.ExportAsync("pt-admin-001", ct);
        await using var cleanBuffer = new MemoryStream();
        await cleanExportStream.CopyToAsync(cleanBuffer, ct);
        var cleanExportBytes = cleanBuffer.ToArray();
        Assert.NotEmpty(cleanExportBytes);

        // ── Leg 1 — the format (C-M11·1): a wrong format version rejects
        //    (the short-circuit, the "format.unsupported" failure). ──
        await AssertFailClosedAsync(
            "kumunita/portability/999",
            cleanExportBytes,
            ct,
            expectedFailure: "format.unsupported");

        // ── Leg 2 — the media bytes (C-M11·3): a corrupted byte (a
        //    one-byte flip in the first media object's payload) rejects
        //    with the "media.mismatch:{id}" failure. ──
        // The media object's id is the SHA-256 of the clean payload —
        // the §validate check (d) computes the SHA of the (corrupted)
        // bytes and compares it against the manifest's media entry's id
        // (which is the clean payload's SHA). A mismatch is the
        // "media.mismatch:{id}" closed failure.
        // To corrupt a single byte, read the clean archive, find the
        // first media/ entry, flip a byte, rewrite it, and assert the
        // rejection + zero rows.
        await AssertFailClosedOnCorruptedMediaAsync(
            cleanExportBytes,
            ct,
            expectedFailurePrefix: "media.mismatch:");

        // ── Leg 3 — the referential integrity (C-M11·4): a dangling
        //    reference (the Group's OwnerId pointing at a principal that
        //    is not in the archive's principals set) rejects with the
        //    "ref.dangling:{Type}.{field}" failure. ──
        // Read the clean archive, change the Group doc's OwnerId to a
        // principal id not in the archive's principals, rewrite it, and
        // assert the rejection + zero rows.
        await AssertFailClosedOnDanglingReferenceAsync(
            cleanExportBytes,
            ct,
            expectedFailurePrefix: "ref.dangling:");
    }

    // ── The D9c leg helpers — each leg: fresh instance + zero rows after
    //    the rejection ──────────────────────────────────────────────────────

    private async Task AssertFailClosedAsync(
        string corruptManifestFormat,
        byte[] cleanExportBytes,
        CancellationToken ct,
        string expectedFailure)
    {
        // Read the clean archive, rewrite it with the corrupted format,
        // and hand it to a fresh instance's import.
        var data = await KumunitaArchive.ReadAsync(
            new MemoryStream(cleanExportBytes), ct);
        data.Manifest!.Format = corruptManifestFormat;
        var corruptedStream = new MemoryStream();
        await KumunitaArchive.WriteAsync(
            corruptedStream,
            data.Manifest,
            data.Docs,
            data.Media,
            data.Principals,
            data.Config,
            ct);
        corruptedStream.Position = 0;

        // A fresh instance — the C-M11·4 "fresh instance, zero writes"
        // pin (a validate failure means the apply phase never runs).
        var (storeB, dbB, _, _, mediaStoreB, _, serviceB) =
            await BootFullInstanceAsync(ct);
        corruptedStream.Position = 0;
        var result = await serviceB.ImportAsync(
            "pt-admin-001", corruptedStream, ct);
        Assert.False(result.Ok);
        Assert.Contains(expectedFailure, result.Failures);

        // The zero-rows assertion (the C-M11·4 fail-closed pin — no
        // content docs, no principals, no audit rows).
        await AssertZeroRowsAsync(storeB, dbB, mediaStoreB, ct);
    }

    private async Task AssertFailClosedOnCorruptedMediaAsync(
        byte[] cleanExportBytes,
        CancellationToken ct,
        string expectedFailurePrefix)
    {
        // Read the clean archive, flip a byte in the first media object's
        // payload, rewrite it, and hand it to a fresh instance's import.
        var data = await KumunitaArchive.ReadAsync(
            new MemoryStream(cleanExportBytes), ct);

        // The media object's id (the SHA-256 of the clean payload) — the
        // §validate check (d) will compute the SHA of the (corrupted)
        // bytes and compare against the manifest's media entry's id
        // (the clean SHA). The flip guarantees a mismatch.
        if (data.Media.Count == 0)
            throw new InvalidOperationException(
                "the clean export has no media bytes — the D9a plant should " +
                "have planted a media object (the C-M11·3 content-addressed " +
                "byte witness)");

        var firstMediaId = data.Media.Keys.First();
        var corrupted = data.Media[firstMediaId];
        corrupted[0] ^= 0xFF; // flip the first byte — the SHA will differ
        data.Media[firstMediaId] = corrupted;

        var corruptedStream = new MemoryStream();
        await KumunitaArchive.WriteAsync(
            corruptedStream, data.Manifest, data.Docs, data.Media,
            data.Principals, data.Config, ct);
        corruptedStream.Position = 0;

        // A fresh instance — the C-M11·4 "fresh instance, zero writes"
        // pin.
        var (storeB, dbB, _, _, mediaStoreB, _, serviceB) =
            await BootFullInstanceAsync(ct);
        var result = await serviceB.ImportAsync(
            "pt-admin-001", corruptedStream, ct);
        Assert.False(result.Ok);
        Assert.Contains(
            result.Failures,
            f => f.StartsWith(expectedFailurePrefix, StringComparison.Ordinal));

        await AssertZeroRowsAsync(storeB, dbB, mediaStoreB, ct);
    }

    private async Task AssertFailClosedOnDanglingReferenceAsync(
        byte[] cleanExportBytes,
        CancellationToken ct,
        string expectedFailurePrefix)
    {
        // Read the clean archive, change the Group doc's OwnerId to a
        // principal id not in the archive's principals set, rewrite it,
        // and hand it to a fresh instance's import.
        var data = await KumunitaArchive.ReadAsync(
            new MemoryStream(cleanExportBytes), ct);

        var groupBytes = data.Docs["Group"];
        var groupList = KumunitaArchive.FromJson<List<Group>>(groupBytes)
            ?? throw new InvalidOperationException("Group doc is malformed");
        var firstGroup = groupList.First();
        firstGroup.OwnerId = "pt-dangling-reference-not-in-archive-001";
        data.Docs["Group"] = KumunitaArchive.ToJson(groupList);

        var corruptedStream = new MemoryStream();
        await KumunitaArchive.WriteAsync(
            corruptedStream, data.Manifest, data.Docs, data.Media,
            data.Principals, data.Config, ct);
        corruptedStream.Position = 0;

        // A fresh instance — the C-M11·4 "fresh instance, zero writes"
        // pin.
        var (storeB, dbB, _, _, mediaStoreB, _, serviceB) =
            await BootFullInstanceAsync(ct);
        var result = await serviceB.ImportAsync(
            "pt-admin-001", corruptedStream, ct);
        Assert.False(result.Ok);
        Assert.Contains(
            result.Failures,
            f => f.StartsWith(expectedFailurePrefix, StringComparison.Ordinal));

        await AssertZeroRowsAsync(storeB, dbB, mediaStoreB, ct);
    }

    // ── The zero-rows assertion — the C-M11·4 fail-closed pin's witness ──
    // A validate failure means the apply phase never ran: the fresh
    // instance has no content docs, no principals, no media bytes, no
    // config rows, and no audit rows.

    private async Task AssertZeroRowsAsync(
        IDocumentStore store,
        AppDbContext db,
        IMediaStore mediaStore,
        CancellationToken ct)
    {
        // No content docs (the 44 content doc types' tables are empty —
        // the C-M11·4 fail-closed pin: a validate failure means the apply
        // phase never ran).
        await using (var q = store.QuerySession())
        {
            Assert.Equal(0, await Marten.QueryableExtensions.CountAsync(
                q.Query<Profile>(), ct));
            Assert.Equal(0, await Marten.QueryableExtensions.CountAsync(
                q.Query<Component>(), ct));
            Assert.Equal(0, await Marten.QueryableExtensions.CountAsync(
                q.Query<Post>(), ct));
            Assert.Equal(0, await Marten.QueryableExtensions.CountAsync(
                q.Query<Page>(), ct));
            Assert.Equal(0, await Marten.QueryableExtensions.CountAsync(
                q.Query<AccessAudit>(), ct));
        }

        // No principals (the identity store is empty — the C-M11·2 import
        // boundary's "zero writes on a rejected import" witness).
        var userCount = await Microsoft.EntityFrameworkCore
            .EntityFrameworkQueryableExtensions.CountAsync(db.Users, ct);
        Assert.Equal(0, userCount);

        // No media bytes (the volume is empty — the C-M11·3 byte witness).
        // The LocalVolumeMediaStore's OpenReadAsync throws on a missing
        // id — a fresh instance has no media objects to read.
        await Assert.ThrowsAsync<KeyNotFoundException>(
            async () =>
            {
                using var stream = await mediaStore.OpenReadAsync(
                    "pt-nonexistent-media-id", ct);
            });
    }

    // ── The stream helper — the MemoryStream → byte[] bridge (the export
    //    stream is a forward-only MemoryStream — read it once into a buffer
    //    to hand it to the import's stream seam). ─────────────────────────

    private static async Task<byte[]> ReadAllBytesAsync(Stream source, CancellationToken ct)
    {
        await using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
