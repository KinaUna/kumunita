using System.IO;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Media;
using Kumunita.Core.Notifications;
using Kumunita.Core.Sessions;
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
/// S2/E2 (IMPROVE-SEC IS22): account deletion <b>deletes</b> the resident's
/// identity-adjacent rows — the per-resident state the F1 §2C "decision" rows
/// left undecided and the [DataClass] rows (IS20b/IS20c) now settle to
/// <see cref="Kumunita.Core.Privacy.Erasure.Delete"/>: <see cref="SessionDoc"/>
/// (a session credential — IP + user agent, one per login), <see cref="IdentityToken"/>
/// (verify/setup tokens), <see cref="GuestAccess"/> (a bounded guest standing),
/// <see cref="DelegationGrant"/> (either side), the four <c>Notification*</c>
/// types (per-recipient state), and <see cref="OutboxEmail"/> (matched on the
/// recipient email the composer passes in). The rows the resident's <i>own</i>
/// deletion touches are gone; rows belonging to <i>other</i> residents (a
/// delegation between two non-target residents, an outbox row to a different
/// recipient) survive — the deletes are owner-scoped, not a blanket wipe.
/// <para>
/// Harness mirrors <see cref="IdentityServiceDeleteLaneGdprTests"/> and
/// <see cref="DeleteAccountThreadTests"/> (the <see cref="PostgresFixture"/>
/// shape + <c>BootAsync</c> + <c>SeedAccountAsync</c> +
/// <c>boot.Identity.DeleteAccountAsync</c>). The four test names are design
/// §2.3 verbatim. Core stays HTTP-free — the seam runs inside the composer.
/// <para>
/// <b>PII gotcha (the IS08 gate): the seeded <see cref="SessionDoc"/> carries
/// an <c>Ip</c> + <c>UserAgent</c>. They are stored fixture values, not a log
/// template — the IS08 gate scans log calls in <c>src/**/*.cs</c>, not test
/// data. They are kept clearly synthetic (<c>10.0.0.42</c>, <c>test-agent</c>)
/// and are never logged.</para>
/// </para>
/// </summary>
public sealed class DeleteAccountRowsTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── S2/E2 pinned tests (design §2.3, verbatim names) ───────────────────

    [Fact]
    public async Task DeleteAccount_RemovesSessions()
    {
        var boot = await BootAsync();
        var actor  = await SeedAccountAsync(boot, "ga-actor@examplium.com", globalAdmin: true);
        var target = await SeedAccountAsync(boot, "rows-sess@examplium.com", globalAdmin: false);

        var sessionId = Guid.NewGuid().ToString("N");
        await using (var session = boot.Store.LightweightSession())
        {
            // Synthetic PII (the IS08 gate scans log calls, not test data):
            session.Store(new SessionDoc
            {
                Id = sessionId,
                UserId = target,
                UserAgent = "test-agent",
                Ip = "10.0.0.42",
                CreatedAt = DateTime.UtcNow,
                LastActive = DateTime.UtcNow,
                RevokedAt = DateTime.UtcNow
            });
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await boot.Identity.DeleteAccountAsync(target, adminSubjectId: actor);

        // The resident's session row is gone in a fresh query session.
        Assert.Null(await LoadAsync<SessionDoc>(boot.Store, sessionId));
    }

    [Fact]
    public async Task DeleteAccount_RemovesTokensAndGuestAccess()
    {
        var boot = await BootAsync();
        var actor  = await SeedAccountAsync(boot, "ga-actor@examplium.com", globalAdmin: true);
        var target = await SeedAccountAsync(boot, "rows-tok@examplium.com", globalAdmin: false);

        var tokenId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        await using (var session = boot.Store.LightweightSession())
        {
            session.Store(new IdentityToken
            {
                Id = tokenId,
                Kind = IdentityToken.KindVerify,
                UserId = target,
                Token = "sup3r-secret-token-value",
                Attempt = 1,
                CreatedAt = now,
                ExpiresAt = now.AddDays(1),
                ConsumedAt = now
            });
            // GuestAccess's identity is SubjectId (pinned by CoreDocTypes).
            session.Store(new GuestAccess
            {
                SubjectId = target,
                ValidFrom = now,
                ValidUntil = now.AddDays(30),
                SetByAdmin = actor
            });
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await boot.Identity.DeleteAccountAsync(target, adminSubjectId: actor);

        // Both the token + the guest standing are gone in a fresh query session.
        Assert.Null(await LoadAsync<IdentityToken>(boot.Store, tokenId));
        Assert.Null(await LoadAsync<GuestAccess>(boot.Store, target));
    }

    [Fact]
    public async Task DeleteAccount_RemovesDelegations_BothSides()
    {
        var boot = await BootAsync();
        var actor  = await SeedAccountAsync(boot, "ga-actor@examplium.com", globalAdmin: true);
        var target = await SeedAccountAsync(boot, "rows-deleg-a@examplium.com", globalAdmin: false);
        var other1 = await SeedAccountAsync(boot, "rows-deleg-b@examplium.com", globalAdmin: false);
        var other2 = await SeedAccountAsync(boot, "rows-deleg-c@examplium.com", globalAdmin: false);

        var grantA = Guid.NewGuid().ToString("N"); // target owns → deleted
        var grantB = Guid.NewGuid().ToString("N"); // target is delegate → deleted
        var grantC = Guid.NewGuid().ToString("N"); // between two OTHERS → survives
        var now = DateTimeOffset.UtcNow;
        await using (var session = boot.Store.LightweightSession())
        {
            session.Store(new DelegationGrant
            {
                Id = grantA,
                OwnerId = target, DelegateId = other1,
                Scope = ["posts"], From = now
            });
            session.Store(new DelegationGrant
            {
                Id = grantB,
                OwnerId = other1, DelegateId = target,
                Scope = ["posts"], From = now
            });
            session.Store(new DelegationGrant
            {
                Id = grantC,
                OwnerId = other1, DelegateId = other2,
                Scope = ["posts"], From = now
            });
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await boot.Identity.DeleteAccountAsync(target, adminSubjectId: actor);

        // A grant where target is the Owner AND one where target is the
        // Delegate are both gone; a grant between two other residents survives
        // (the delete is scoped to the resident on either side).
        Assert.Null(await LoadAsync<DelegationGrant>(boot.Store, grantA));
        Assert.Null(await LoadAsync<DelegationGrant>(boot.Store, grantB));
        Assert.NotNull(await LoadAsync<DelegationGrant>(boot.Store, grantC));
    }

    [Fact]
    public async Task DeleteAccount_RemovesNotificationsAndOutbox()
    {
        var boot = await BootAsync();
        var actor  = await SeedAccountAsync(boot, "ga-actor@examplium.com", globalAdmin: true);
        var target = await SeedAccountAsync(boot, "rows-notif@examplium.com", globalAdmin: false);
        var other  = await SeedAccountAsync(boot, "rows-notif-other@examplium.com", globalAdmin: false);

        var targetEmail = "rows-notif@examplium.com";
        var otherEmail  = "rows-notif-other@examplium.com";

        var notifId   = Guid.NewGuid().ToString("N");
        var subId     = Guid.NewGuid().ToString("N");
        var targetBox = Guid.NewGuid().ToString("N");
        var otherBox  = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        await using (var session = boot.Store.LightweightSession())
        {
            session.Store(new Notification
            {
                Id = notifId,
                RecipientId = target,
                Kind = "post.new",
                IdempotencyKey = "notification:post.new:" + notifId,
                Subject = "A new post",
                Body = "snippet",
                Created = now
            });
            // Identity = RecipientId (pinned): no separate Id.
            session.Store(new NotificationPreference
            {
                RecipientId = target,
                KindsEnabled = ["post.new"]
            });
            session.Store(new NotificationSubscription
            {
                Id = subId,
                RecipientId = target,
                Kind = "post.new",
                TargetId = "community-1"
            });
            // Identity = RecipientId (pinned).
            session.Store(new NotificationQuietSchedule
            {
                RecipientId = target,
                Enabled = true,
                Hours = [8, 9],
                DaysOfWeek = [1, 2]
            });
            // OutboxEmail: the recipient is the EMAIL string (not an id).
            session.Store(new OutboxEmail
            {
                Id = targetBox,
                IdempotencyKey = "verify:" + target + ":1",
                Recipient = targetEmail,
                Subject = "verify",
                Body = "verify body",
                QueuedAt = now
            });
            session.Store(new OutboxEmail
            {
                Id = otherBox,
                IdempotencyKey = "verify:" + other + ":1",
                Recipient = otherEmail,
                Subject = "verify",
                Body = "verify body",
                QueuedAt = now
            });
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await boot.Identity.DeleteAccountAsync(target, adminSubjectId: actor);

        // All four of the resident's notification rows are gone…
        Assert.Null(await LoadAsync<Notification>(boot.Store, notifId));
        Assert.Null(await LoadAsync<NotificationPreference>(boot.Store, target));
        Assert.Null(await LoadAsync<NotificationSubscription>(boot.Store, subId));
        Assert.Null(await LoadAsync<NotificationQuietSchedule>(boot.Store, target));
        // …the resident's outbox row (matched on the resident's email) is
        // gone, while an outbox row to a DIFFERENT recipient survives.
        Assert.Null(await LoadAsync<OutboxEmail>(boot.Store, targetBox));
        Assert.NotNull(await LoadAsync<OutboxEmail>(boot.Store, otherBox));
    }

    // ── Harness (mirrors DeleteAccountThreadTests / IdentityServiceDeleteLaneGdprTests) ──

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

        // CoreDocTypes covers the identity-adjacent set (IdentityToken,
        // GuestAccess, DelegationGrant, OutboxEmail); the IS22 additions are
        // SessionDocTypes (SessionDoc) + NotificationDocTypes (the 4
        // Notification* types). The Messaging/GroupMessaging/Media surfaces are
        // the IS21 base (DeleteCoreAsync runs ResidentContentEraser →
        // ResidentThreadPseudonymizer + the media byte-delete).
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            CoreDocTypes.Configure(opts);
            MessagingDocTypes.Configure(opts);
            GroupMessagingDocTypes.Configure(opts);
            MediaDocTypes.Configure(opts);
            SessionDocTypes.Configure(opts);
            NotificationDocTypes.Configure(opts);
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
        var mediaRoot = Path.Combine(Path.GetTempPath(), "kumunita-rows-" + Guid.NewGuid().ToString("n"));
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

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
