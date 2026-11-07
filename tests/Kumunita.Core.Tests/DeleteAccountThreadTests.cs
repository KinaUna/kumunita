using System.IO;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.GroupMessaging;
using Kumunita.Core.Identity;
using Kumunita.Core.Media;
using Kumunita.Core.Messaging;
using Kumunita.Core.UserInfo;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// D3 (IMPROVE-SEC IS21, plan decision D3): account deletion <b>pseudonymizes</b>
/// the resident's messaging threads instead of deleting them — the resident's
/// <c>Message</c> / <c>GroupMessage</c> bodies are erased and their
/// <c>SenderId</c> becomes the <c>$"deleted:{id}"</c> tombstone (the
/// <c>PseudonymizeAuditRowsAsync</c> precedent), the resident's
/// <c>Conversation</c> seat is tombstoned (the conversation itself stays, D3:
/// the other participant's copy must remain), the resident leaves the
/// <c>GroupConversation.Participants</c> list, and a pending invite <i>to</i>
/// the resident is deleted (stale once the account is gone). The F1-era
/// *delete-the-conversation* behaviour is replaced — the other participant's
/// thread must stay coherent.
/// <para>
/// Harness mirrors <see cref="IdentityServiceDeleteLaneGdprTests"/> (the
/// <see cref="PostgresFixture"/> shape + <c>BootAsync</c> +
/// <c>SeedAccountAsync</c> + <c>boot.Identity.DeleteAccountAsync</c>). The
/// four test names are design §2.3 verbatim. Core stays HTTP-free — the seam
/// is constructed directly, no Web types.
/// </para>
/// </summary>
public sealed class DeleteAccountThreadTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── D3 pinned tests (design §2.3, verbatim names) ──────────────────────

    [Fact]
    public async Task DeleteAccount_DirectMessages_BodyErased_SenderTombstoned()
    {
        var boot = await BootAsync();
        var actor  = await SeedAccountAsync(boot, "ga-actor@examplium.com", globalAdmin: true);
        var target = await SeedAccountAsync(boot, "thread-a@examplium.com", globalAdmin: false);
        var other  = await SeedAccountAsync(boot, "thread-b@examplium.com", globalAdmin: false);

        // The thread: one conversation, the resident's sent message + the
        // other's reply (both live in the same 1:1 thread).
        var convoId = Guid.NewGuid().ToString("N");
        var msgId   = Guid.NewGuid().ToString("N");
        await using (var session = boot.Store.LightweightSession())
        {
            session.Store(new Conversation
            {
                Id = convoId,
                ParticipantA = string.CompareOrdinal(target, other) <= 0 ? target : other,
                ParticipantB = string.CompareOrdinal(target, other) <= 0 ? other : target,
                Created = DateTimeOffset.UtcNow
            });
            session.Store(new Message
            {
                Id = msgId, ConversationId = convoId, SenderId = target,
                Body = "d3 direct message body", Created = DateTimeOffset.UtcNow
            });
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await boot.Identity.DeleteAccountAsync(target, adminSubjectId: actor);

        // The resident's message survives (the row STAYS — D3) with the body
        // erased and the sender tombstoned; the conversation still exists.
        var msg = await LoadAsync<Message>(boot.Store, msgId);
        Assert.NotNull(msg);
        Assert.Equal(string.Empty, msg!.Body);
        Assert.Equal($"deleted:{target}", msg.SenderId);

        var convo = await LoadAsync<Conversation>(boot.Store, convoId);
        Assert.NotNull(convo);
    }

    [Fact]
    public async Task DeleteAccount_OtherPartyThread_Survives()
    {
        var boot = await BootAsync();
        var actor  = await SeedAccountAsync(boot, "ga-actor@examplium.com", globalAdmin: true);
        var target = await SeedAccountAsync(boot, "thread-a@examplium.com", globalAdmin: false);
        var other  = await SeedAccountAsync(boot, "thread-b@examplium.com", globalAdmin: false);

        var convoId = Guid.NewGuid().ToString("N");
        var otherMsgId = Guid.NewGuid().ToString("N");
        await using (var session = boot.Store.LightweightSession())
        {
            session.Store(new Conversation
            {
                Id = convoId,
                ParticipantA = string.CompareOrdinal(target, other) <= 0 ? target : other,
                ParticipantB = string.CompareOrdinal(target, other) <= 0 ? other : target,
                Created = DateTimeOffset.UtcNow
            });
            session.Store(new Message
            {
                Id = otherMsgId, ConversationId = convoId, SenderId = other,
                Body = "the other party's reply", Created = DateTimeOffset.UtcNow
            });
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await boot.Identity.DeleteAccountAsync(target, adminSubjectId: actor);

        // The other participant's message in the SAME thread is untouched
        // (body + sender intact) and their seat is NOT tombstoned — the
        // thread remains coherent for them (D3: 1:1 changes from
        // *delete the conversation* to this rule).
        var msg = await LoadAsync<Message>(boot.Store, otherMsgId);
        Assert.NotNull(msg);
        Assert.Equal("the other party's reply", msg!.Body);
        Assert.Equal(other, msg.SenderId);

        var convo = await LoadAsync<Conversation>(boot.Store, convoId);
        Assert.NotNull(convo);
        // The other seat is intact; the resident's seat is the tombstone.
        var seats = new[] { convo!.ParticipantA, convo.ParticipantB };
        Assert.Contains(other, seats);
        Assert.Contains($"deleted:{target}", seats);
    }

    [Fact]
    public async Task DeleteAccount_GroupMessages_Pseudonymized()
    {
        var boot = await BootAsync();
        var actor  = await SeedAccountAsync(boot, "ga-actor@examplium.com", globalAdmin: true);
        var target = await SeedAccountAsync(boot, "group-a@examplium.com", globalAdmin: false);
        var other  = await SeedAccountAsync(boot, "group-b@examplium.com", globalAdmin: false);

        var convoId = Guid.NewGuid().ToString("N");
        var msgId   = Guid.NewGuid().ToString("N");
        await using (var session = boot.Store.LightweightSession())
        {
            session.Store(new GroupConversation
            {
                Id = convoId, CreatorId = other, Title = "d3 group thread",
                Created = DateTimeOffset.UtcNow,
                Participants = new List<string> { other, target }.OrderBy(x => x).ToList()
            });
            session.Store(new GroupMessage
            {
                Id = msgId, ConversationId = convoId, SenderId = target,
                Body = "d3 group message body", Created = DateTimeOffset.UtcNow
            });
            // A pending invite TO the resident (stale once the account is
            // gone — D3 deletes it; the group thread's own participant list
            // is the pseudonymized surface).
            session.Store(new GroupMessagingInvitation
            {
                Id = Guid.NewGuid().ToString("N"), ConversationId = convoId,
                InvitedId = target, InvitedBy = other,
                Status = GroupMessagingInvitationStatus.Pending,
                InvitedAt = DateTimeOffset.UtcNow
            });
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await boot.Identity.DeleteAccountAsync(target, adminSubjectId: actor);

        // The resident's group message survives with the body erased and the
        // sender tombstoned (same rule as 1:1).
        var msg = await LoadAsync<GroupMessage>(boot.Store, msgId);
        Assert.NotNull(msg);
        Assert.Equal(string.Empty, msg!.Body);
        Assert.Equal($"deleted:{target}", msg.SenderId);

        // The resident is out of the participant list; the conversation stays.
        var convo = await LoadAsync<GroupConversation>(boot.Store, convoId);
        Assert.NotNull(convo);
        Assert.DoesNotContain(target, convo!.Participants);
        Assert.Contains(other, convo.Participants);

        // The pending invite to the resident is gone (a fresh query finds no
        // GroupMessagingInvitation with InvitedId == target).
        var invites = await ListAsync<GroupMessagingInvitation>(
            boot.Store, i => i.InvitedId == target);
        Assert.Empty(invites);
    }

    [Fact]
    public async Task DeleteAccount_RemovedFromGroupConversationParticipants()
    {
        var boot = await BootAsync();
        var actor  = await SeedAccountAsync(boot, "ga-actor@examplium.com", globalAdmin: true);
        var target = await SeedAccountAsync(boot, "group-a@examplium.com", globalAdmin: false);
        var other  = await SeedAccountAsync(boot, "group-b@examplium.com", globalAdmin: false);

        var convoId = Guid.NewGuid().ToString("N");
        var otherMsgId = Guid.NewGuid().ToString("N");
        await using (var session = boot.Store.LightweightSession())
        {
            session.Store(new GroupConversation
            {
                Id = convoId, CreatorId = other, Title = "d3 group thread",
                Created = DateTimeOffset.UtcNow,
                Participants = new List<string> { other, target }.OrderBy(x => x).ToList()
            });
            session.Store(new GroupMessage
            {
                Id = otherMsgId, ConversationId = convoId, SenderId = other,
                Body = "the other participant's message", Created = DateTimeOffset.UtcNow
            });
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await boot.Identity.DeleteAccountAsync(target, adminSubjectId: actor);

        // The resident's id is gone from the participant list, but the
        // conversation and the other participant's message remain (D3: the
        // row stays for the other participants; only the resident's seat +
        // sent rows are erased).
        var convo = await LoadAsync<GroupConversation>(boot.Store, convoId);
        Assert.NotNull(convo);
        Assert.DoesNotContain(target, convo!.Participants);
        Assert.Contains(other, convo.Participants);

        var msg = await LoadAsync<GroupMessage>(boot.Store, otherMsgId);
        Assert.NotNull(msg);
        Assert.Equal("the other participant's message", msg!.Body);
        Assert.Equal(other, msg.SenderId);
    }

    // ── Harness (mirrors IdentityServiceDeleteLaneGdprTests) ───────────────

    private sealed record Boot
    {
        public required IdentityService Identity { get; init; }
        public required UserManager<User> UserManager { get; init; }
        public required IDocumentStore Store { get; init; }
        public required DbContextOptions<AppDbContext> DbOptions { get; init; }
        public required IMediaFileStore MediaStore { get; init; }
    }

    private async Task<Boot> BootAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);

        // GOTCHA (mirrors the reference harness): MediaObject is registered
        // on the MediaDocTypes surface, so it must be added here; the
        // messaging + group-messaging surfaces are IS21's additions.
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            CoreDocTypes.Configure(opts);
            MessagingDocTypes.Configure(opts);
            GroupMessagingDocTypes.Configure(opts);
            MediaDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(conn).Options;
        await using (var db = new AppDbContext(dbOptions))
        {
            await db.Database.MigrateAsync(ct);
        }

        var userStore = new UserStore<User, IdentityRole, AppDbContext, string,
            IdentityUserClaim<string>, IdentityUserRole<string>,
            IdentityUserLogin<string>, IdentityUserToken<string>,
            IdentityRoleClaim<string>>(new AppDbContext(dbOptions));

        var userManager = new UserManager<User>(
            userStore,
            Options.Create(new IdentityOptions { User = { RequireUniqueEmail = true } }),
            new PasswordHasher<User>(),
            new[] { new UserValidator<User>() },
            new[] { new PasswordValidator<User>() },
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new EmptyServiceProvider(),
            NullLogger<UserManager<User>>.Instance);

        var userInfo = new UserInfoService(store);

        // A real byte store (unique temp dir) — DeleteAccountAsync runs the
        // post-commit byte-delete against it (no bytes are planted here, but
        // the harness must supply one or the composer would NPE).
        var mediaRoot = Path.Combine(Path.GetTempPath(), "kumunita-thread-" + Guid.NewGuid().ToString("n"));
        var mediaStore = new LocalVolumeFileStore(Options.Create(new MediaOptions { RootPath = mediaRoot }));

        var identity = new IdentityService(
            userManager:          userManager,
            documentStore:        store,
            userInfo:             userInfo,
            claimsSource:         Substitute.For<IClaimsSource>(),
            mailer:               Substitute.For<IMailerStage>(),
            verificationOptions:  Options.Create(new VerificationOptions()),
            logger:               NullLogger<IdentityService>.Instance,
            translationProvider:  null,
            notifications:        null,
            sampleDataOptions:    Options.Create(new SampleDataOptions { Enabled = false }),
            mediaStore:           mediaStore);

        return new Boot
        {
            Identity = identity,
            UserManager = userManager,
            Store = store,
            DbOptions = dbOptions,
            MediaStore = mediaStore
        };
    }

    private async Task<string> SeedAccountAsync(Boot boot, string email, bool globalAdmin)
    {
        var ct = TestContext.Current.CancellationToken;
        var mgr = boot.UserManager;

        var user = new User { Id = Guid.NewGuid().ToString("N"), Email = email, UserName = email };
        await mgr.CreateAsync(user, "Sup3rSecret!");

        if (globalAdmin)
        {
            await using var db = new AppDbContext(boot.DbOptions);
            if (!await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                    .AnyAsync(db.Roles.Where(r => r.Name == Roles.GlobalAdmin), ct))
            {
                db.Roles.Add(new IdentityRole
                {
                    Name = Roles.GlobalAdmin,
                    NormalizedName = Roles.GlobalAdmin.ToUpperInvariant()
                });
                await db.SaveChangesAsync(ct);
            }
            await mgr.AddToRoleAsync(user, Roles.GlobalAdmin);
        }

        await using var session = boot.Store.LightweightSession();
        session.Store(new Profile
        {
            SubjectId = user.Id,
            DisplayName = "Test Resident",
            Verified = true,
            Email = email
        });
        await session.SaveChangesAsync(ct);

        return user.Id ?? string.Empty;
    }

    private static async Task<T?> LoadAsync<T>(IDocumentStore store, string id)
        where T : notnull, new()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.QuerySession();
        return await session.LoadAsync<T>(id, ct);
    }

    private static async Task<IReadOnlyList<T>> ListAsync<T>(
        IDocumentStore store, System.Linq.Expressions.Expression<System.Func<T, bool>> predicate)
        where T : notnull
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.QuerySession();
        return await Marten.QueryableExtensions.ToListAsync(session.Query<T>().Where(predicate), ct);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
