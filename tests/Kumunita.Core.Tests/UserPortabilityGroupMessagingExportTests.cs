using System.IO;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.GroupMessaging;
using Kumunita.Core.Identity;
using Kumunita.Core.Media;
using Kumunita.Core.Portability;
using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// IS32 (E6, second half) — the resident export now carries the group-messaging
/// families (<see cref="GroupConversation"/> + <see cref="GroupMessage"/>),
/// which shipped after F1 (ADR 0278) and were missed by the closed
/// <see cref="UserScopeInventory"/>. These are <em>union</em> (the resident is
/// in the <c>Participants</c> set) and <em>indirect</em> (the message's
/// <c>ConversationId</c> → <c>GroupConversation</c>) scope kinds — neither is
/// single-owner, so the generic owner rule cannot see them.
/// <list type="bullet">
/// <item><b>union + indirect export</b> —
///     <c>Export_IncludesGroupThreadsActorIsIn</c>: a group thread the resident
///     is in (with a message) is present in their archive; a foreign thread
///     they are not in is absent.</item>
/// <item><b>guard, fail-closed indirect</b> —
///     <c>Import_GroupMessage_ForeignConversation_Rejected</c>: a
///     <c>GroupMessage</c> whose <c>ConversationId</c> is not in the actor's
///     group-conversation set is rejected (mirrors the <c>Message</c> rule).</item>
/// <item><b>guard, empty-set fail-closed</b> —
///     <c>Guard_GroupMessage_RequiresActorConversation</c>: <c>Check</c> with a
///     <c>null</c> <c>actorGroupConversationIds</c> rejects the
///     <c>GroupMessage</c> (fail-closed).</item>
/// </list>
/// </summary>
/// <para>
/// Synthetic ids only (unit-series rule 7): residents are
/// <c>m32-resident-a</c> / <c>m32-resident-b</c>; emails
/// <c>a@x.com</c> / <c>b@x.com</c>. No real PII.
/// </para>
public sealed class UserPortabilityGroupMessagingExportTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private sealed record Booted(
        IDocumentStore store,
        AppDbContext db,
        UserManager<User> userManager,
        UserPortabilityService service);

    private async Task<Booted> BootAsync(CancellationToken ct)
    {
        var conn = await fixture.NewDatabaseAsync(ct);

        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            CoreDocTypes.Configure(opts);
            PostDocTypes.Configure(opts);
            EventDocTypes.Configure(opts);
            ProjectDocTypes.Configure(opts);
            MessagingDocTypes.Configure(opts);
            MediaDocTypes.Configure(opts);
            TagDocTypes.Configure(opts);
            PageDocTypes.Configure(opts);
            InventoryDocTypes.Configure(opts);
            DocumentDocTypes.Configure(opts);
            BookmarkDocTypes.Configure(opts);
            PersonalNotesDocTypes.Configure(opts);
            // IS32 (E6) — the GroupMessaging surface: the two new closed-set
            // rows (GroupConversation + GroupMessage) must be queryable by
            // the export loop (the GroupConversation seat-set pre-query +
            // the GroupMessage indirect filter).
            GroupMessagingDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn)
            .Options);
        await db.Database.MigrateAsync(ct);

        var userStore = new UserStore<User, IdentityRole, AppDbContext, string,
            IdentityUserClaim<string>, IdentityUserRole<string>,
            IdentityUserLogin<string>, IdentityUserToken<string>,
            IdentityRoleClaim<string>>(db);
        var identityOpts = Options.Create(new IdentityOptions
        {
            User = { RequireUniqueEmail = true },
            Password = { RequiredLength = 8, RequireNonAlphanumeric = false },
        });
        var userManager = new UserManager<User>(
            userStore, identityOpts, new PasswordHasher<User>(),
            [new UserValidator<User>()], [new PasswordValidator<User>()],
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(),
            new EmptyServiceProvider(),
            NullLogger<UserManager<User>>.Instance);

        var mediaRoot = Path.Combine(
            Path.GetTempPath(),
            "kumunita-m32-" + Guid.NewGuid().ToString("n")[..10]);
        var fileStore = new LocalVolumeFileStore(
            Options.Create(new MediaOptions { RootPath = mediaRoot }));
        var mediaStore = new LocalVolumeMediaStore(fileStore, store);

        var userInfo = Substitute.For<IUserInfoService>();

        var service = new UserPortabilityService(store, userInfo, mediaStore, userManager);
        return new Booted(store, db, userManager, service);
    }

    /// <summary>Creates an Identity user + Profile for a given subject id.</summary>
    private async Task CreateResidentAsync(
        Booted boot, string subjectId, string username, string email, CancellationToken ct)
    {
        var result = await boot.userManager.CreateAsync(
            new User
            {
                Id = subjectId,
                UserName = username,
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
            },
            "Passw0rd!" + username);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"user create failed for {subjectId}: " +
                string.Join(", ", result.Errors.Select(e => e.Description)));

        await using var session = boot.store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(new Profile
        {
            SubjectId = subjectId,
            DisplayName = username,
            Verified = true,
            Blocked = false,
        });
        await session.SaveChangesAsync(ct);
    }

    // ── Test 1: union + indirect export — A's threads in, B's threads out ──

    [Fact]
    public async Task Export_IncludesGroupThreadsActorIsIn()
    {
        var boot = await BootAsync(CancellationToken.None);
        const string residentA = "m32-resident-a";
        const string residentB = "m32-resident-b";
        await CreateResidentAsync(boot, residentA, "resident-a", "a@x.com", CancellationToken.None);
        await CreateResidentAsync(boot, residentB, "resident-b", "b@x.com", CancellationToken.None);

        // Plant: thread "gconv-a" — A is a participant (union seat). It holds
        // one GroupMessage (indirect seat, via ConversationId).
        //       thread "gconv-b" — B is the only participant (a foreign thread
        //       A is NOT in). It holds one GroupMessage (foreign too).
        await using (var session = boot.store.OpenSession(new Marten.Services.SessionOptions()))
        {
            session.Store(new GroupConversation
            {
                Id = "gconv-a",
                CreatorId = residentA,
                Title = "A's thread",
                Created = DateTimeOffset.UtcNow,
                Participants = [residentA],
            });
            session.Store(new GroupMessage
            {
                Id = "gmsg-a",
                ConversationId = "gconv-a",
                SenderId = residentA,
                Body = "hi from A",
                Created = DateTimeOffset.UtcNow,
                LanguageCode = "en",
            });
            session.Store(new GroupConversation
            {
                Id = "gconv-b",
                CreatorId = residentB,
                Title = "B's thread",
                Created = DateTimeOffset.UtcNow,
                Participants = [residentB],
            });
            session.Store(new GroupMessage
            {
                Id = "gmsg-b",
                ConversationId = "gconv-b",
                SenderId = residentB,
                Body = "hi from B",
                Created = DateTimeOffset.UtcNow,
                LanguageCode = "en",
            });
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Export as resident A.
        using var stream = await boot.service.ExportAsync(residentA, TestContext.Current.CancellationToken);
        var data = await KumunitaArchive.ReadAsync(stream, TestContext.Current.CancellationToken);

        // The GroupConversation array — exactly one (A's thread), not B's.
        // (This also proves the nameToType entries resolve — no
        // KeyNotFoundException was thrown reaching here.)
        var conversations = KumunitaArchive.FromJson<List<GroupConversation>>(data.Docs["GroupConversation"])!;
        Assert.Single(conversations);
        Assert.Equal("gconv-a", conversations[0].Id);
        Assert.DoesNotContain(conversations, c => c.Id == "gconv-b");

        // The GroupMessage array — exactly A's (the indirect seat), not B's.
        var messages = KumunitaArchive.FromJson<List<GroupMessage>>(data.Docs["GroupMessage"])!;
        Assert.Single(messages);
        Assert.Equal("gmsg-a", messages[0].Id);
        Assert.Equal("gconv-a", messages[0].ConversationId);
        Assert.DoesNotContain(messages, m => m.Id == "gmsg-b");

        // The manifest doc counts agree.
        Assert.Equal(1, data.Manifest!.DocCounts["GroupConversation"]);
        Assert.Equal(1, data.Manifest!.DocCounts["GroupMessage"]);
    }

    // ── Test 2: guard — a foreign GroupMessage (ConversationId not in the ──
    // ── actor's group-conversation set) is rejected (mirrors the Message ──
    // ── rule; the set is non-empty but does not contain the row's conv). ──

    [Fact]
    public void Import_GroupMessage_ForeignConversation_Rejected()
    {
        const string actor = "user-a";
        // The actor is in group thread "gconv-actor" only.
        var actorGroupConversationIds =
            new HashSet<string>(StringComparer.Ordinal) { "gconv-actor" };

        // A GroupMessage whose ConversationId is a thread the actor is NOT in.
        var foreignMessage = new
        {
            Id = "gmsg-foreign",
            ConversationId = "gconv-foreign",
            SenderId = actor,
        };

        var verdict = ImportOwnershipGuard.Check(
            "GroupMessage", foreignMessage, actor, _ => null,
            actorGroupConversationIds: actorGroupConversationIds);

        Assert.False(verdict.Allowed);
        Assert.Equal(ImportOwnershipGuard.ReasonConversationNotActor, verdict.RejectionReason);

        // The positive control: a GroupMessage in the actor's own thread is
        // allowed (the same branch, the accepted case).
        var ownMessage = new
        {
            Id = "gmsg-own",
            ConversationId = "gconv-actor",
            SenderId = actor,
        };
        var allowed = ImportOwnershipGuard.Check(
            "GroupMessage", ownMessage, actor, _ => null,
            actorGroupConversationIds: actorGroupConversationIds);
        Assert.True(allowed.Allowed);
        Assert.Null(allowed.RejectionReason);
    }

    // ── Test 3: guard — null actorGroupConversationIds is fail-closed ────
    // ── (every GroupMessage rejected, mirroring Message's null-set rule). ──

    [Fact]
    public void Guard_GroupMessage_RequiresActorConversation()
    {
        const string actor = "user-a";

        // A GroupMessage that WOULD be the actor's, but the caller supplies a
        // null group-conversation set → the guard must still reject it
        // (fail-closed, exactly like the Message branch does with a null
        // actorConversationIds).
        var message = new
        {
            Id = "gmsg-x",
            ConversationId = "gconv-x",
            SenderId = actor,
        };

        var nullSet = ImportOwnershipGuard.Check(
            "GroupMessage", message, actor, _ => null); // actorGroupConversationIds omitted (null)

        Assert.False(nullSet.Allowed);
        Assert.Equal(ImportOwnershipGuard.ReasonConversationNotActor, nullSet.RejectionReason);

        // An empty set is also fail-closed (the same branch, the empty case).
        var emptySet = ImportOwnershipGuard.Check(
            "GroupMessage", message, actor, _ => null,
            actorGroupConversationIds: new HashSet<string>(StringComparer.Ordinal));
        Assert.False(emptySet.Allowed);
        Assert.Equal(ImportOwnershipGuard.ReasonConversationNotActor, emptySet.RejectionReason);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
