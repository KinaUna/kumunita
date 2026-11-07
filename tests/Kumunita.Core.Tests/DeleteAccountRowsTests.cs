using System.IO;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.ErrorReports;
using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.Media;
using Kumunita.Core.Notifications;
using Kumunita.Core.Sessions;
using Kumunita.Core.Usage;
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

    // ── IS23: 3 child-scoped deletes (guardian, community) ──────────────────

    [Fact]
    public async Task DeleteAccount_ChildGuardianRows_Removed()
    {
        var boot = await BootAsync();
        var actor  = await SeedAccountAsync(boot, "ga-actor@examplium.com", globalAdmin: true);
        var target = await SeedAccountAsync(boot, "rows-is23-child@examplium.com", globalAdmin: false);
        var other  = await SeedAccountAsync(boot, "rows-is23-other@examplium.com", globalAdmin: false);

        // The resident (target) is the child in all three rows; the other
        // resident is the child in the "survives" rows.
        var targetEventId = Guid.NewGuid().ToString("N");
        var otherEventId  = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        await using (var session = boot.Store.LightweightSession())
        {
            // GuardianTimeLimitSchedule: identity = ChildId (pinned).
            session.Store(new GuardianTimeLimitSchedule
            {
                ChildId = target,
                Enabled = true,
                Mode = TimeLimitMode.Blocked,
                Hours = [8, 9],
                DaysOfWeek = [1, 2],
                Updated = now
            });
            session.Store(new GuardianTimeLimitSchedule
            {
                ChildId = other,
                Enabled = false,
                Mode = TimeLimitMode.Blocked,
                Hours = [],
                DaysOfWeek = [],
                Updated = now
            });
            // GuardianEventRequest: ChildId = the supervised child.
            session.Store(new GuardianEventRequest
            {
                Id = Guid.NewGuid().ToString("N"),
                EventId = targetEventId,
                ChildId = target,
                DesiredStatus = RsvpStatus.Going,
                Status = GuardianEventRequestStatus.Pending,
                RequestedAt = now
            });
            session.Store(new GuardianEventRequest
            {
                Id = Guid.NewGuid().ToString("N"),
                EventId = otherEventId,
                ChildId = other,
                DesiredStatus = RsvpStatus.Going,
                Status = GuardianEventRequestStatus.Pending,
                RequestedAt = now
            });
            // CommunityMembershipRequest: UserId = the supervised child.
            session.Store(new CommunityMembershipRequest
            {
                Id = Guid.NewGuid().ToString("N"),
                ComponentId = "community-1",
                UserId = target,
                RequestedBy = actor,
                Status = CommunityMembershipRequestStatus.Pending,
                RequestedAt = now
            });
            session.Store(new CommunityMembershipRequest
            {
                Id = Guid.NewGuid().ToString("N"),
                ComponentId = "community-1",
                UserId = other,
                RequestedBy = actor,
                Status = CommunityMembershipRequestStatus.Pending,
                RequestedAt = now
            });
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await boot.Identity.DeleteAccountAsync(target, adminSubjectId: actor);

        // All three child-scoped rows for the resident are gone (fresh
        // LoadAsync → null)…
        Assert.Null(await LoadAsync<GuardianTimeLimitSchedule>(boot.Store, target));
        // GuardianEventRequest: query by ChildId (no surrogate id handy —
        // load via a query session).
        await using (var qs = boot.Store.QuerySession())
        {
            var targetEventReq = await Marten.QueryableExtensions.ToListAsync(
                qs.Query<GuardianEventRequest>().Where(r => r.ChildId == target),
                TestContext.Current.CancellationToken);
            Assert.Empty(targetEventReq);
            var targetCommReq = await Marten.QueryableExtensions.ToListAsync(
                qs.Query<CommunityMembershipRequest>().Where(r => r.UserId == target),
                TestContext.Current.CancellationToken);
            Assert.Empty(targetCommReq);

            // …while the OTHER resident's rows survive (the deletes are
            // child-scoped, not a blanket wipe).
            var otherEventReq = await Marten.QueryableExtensions.ToListAsync(
                qs.Query<GuardianEventRequest>().Where(r => r.ChildId == other),
                TestContext.Current.CancellationToken);
            Assert.Single(otherEventReq);
            var otherCommReq = await Marten.QueryableExtensions.ToListAsync(
                qs.Query<CommunityMembershipRequest>().Where(r => r.UserId == other),
                TestContext.Current.CancellationToken);
            Assert.Single(otherCommReq);
        }
        Assert.NotNull(await LoadAsync<GuardianTimeLimitSchedule>(boot.Store, other));
    }

    // ── IS23: 3 pseudonymizations (error, escalation, usage) ────────────────

    [Fact]
    public async Task DeleteAccount_ErrorReport_Pseudonymized_ContactEmailCleared()
    {
        var boot = await BootAsync();
        var actor  = await SeedAccountAsync(boot, "ga-actor@examplium.com", globalAdmin: true);
        var target = await SeedAccountAsync(boot, "rows-is23-err@examplium.com", globalAdmin: false);
        var other  = await SeedAccountAsync(boot, "rows-is23-err-other@examplium.com", globalAdmin: false);

        var targetReportId = Guid.NewGuid().ToString("N");
        var otherReportId  = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        await using (var session = boot.Store.LightweightSession())
        {
            // PII gotcha (IS08): ContactEmail is stored fixture data, not a
            // log template. Kept synthetic; never logged by the eraser.
            session.Store(new ErrorReport
            {
                Id = targetReportId,
                SubjectId = target,
                Description = "Something went wrong (synthetic fixture)",
                ContactEmail = "a@example.test",
                RequestId = "req-001",
                Created = now,
                TriageStatus = "new"
            });
            session.Store(new ErrorReport
            {
                Id = otherReportId,
                SubjectId = other,
                Description = "Something else (synthetic fixture)",
                ContactEmail = "b@example.test",
                RequestId = "req-002",
                Created = now,
                TriageStatus = "new"
            });
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await boot.Identity.DeleteAccountAsync(target, adminSubjectId: actor);

        // The resident's ErrorReport row STAYS (the pseudonymize contract),
        // with the principal → tombstone and the PII (ContactEmail) → null.
        var targetReport = await LoadAsync<ErrorReport>(boot.Store, targetReportId);
        Assert.NotNull(targetReport);
        Assert.Equal($"deleted:{target}", targetReport.SubjectId);
        Assert.Null(targetReport.ContactEmail);

        // An ErrorReport for a different SubjectId is untouched.
        var otherReport = await LoadAsync<ErrorReport>(boot.Store, otherReportId);
        Assert.NotNull(otherReport);
        Assert.Equal(other, otherReport.SubjectId);
        Assert.Equal("b@example.test", otherReport.ContactEmail);
    }

    [Fact]
    public async Task DeleteAccount_UsageEvents_Pseudonymized()
    {
        var boot = await BootAsync();
        var actor  = await SeedAccountAsync(boot, "ga-actor@examplium.com", globalAdmin: true);
        var target = await SeedAccountAsync(boot, "rows-is23-usage@examplium.com", globalAdmin: false);
        var other  = await SeedAccountAsync(boot, "rows-is23-usage-other@examplium.com", globalAdmin: false);

        var targetUsageId = Guid.NewGuid().ToString("N");
        var otherUsageId  = Guid.NewGuid().ToString("N");
        var targetTokenId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        await using (var session = boot.Store.LightweightSession())
        {
            session.Store(new UsageEvent
            {
                Id = targetUsageId,
                At = now,
                ActorId = target,
                RouteTemplate = "GET /posts/{id}"
            });
            session.Store(new UsageEvent
            {
                Id = otherUsageId,
                At = now,
                ActorId = other,
                RouteTemplate = "GET /posts/{id}"
            });
            // EscalationToken: CreatedBy = the admin's subject. The token is
            // operator-scoped (an operator credential) but the creator's
            // identity is the resident's — pseudonymize the principal field,
            // the token hash itself is untouched (still verifiable by the
            // origin platform, the row STAYS).
            session.Store(new EscalationToken
            {
                Id = targetTokenId,
                Label = "origin-platform-x",
                TokenHash = "0000000000000000000000000000000000000000000000000000000000000000",
                TokenPrefix = "kesc_synthetic",
                Created = now,
                CreatedBy = target
            });
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await boot.Identity.DeleteAccountAsync(target, adminSubjectId: actor);

        // Both principal-tombstone rows STAY (the pseudonymize contract) with
        // the principal field → tombstone.
        var targetUsage = await LoadAsync<UsageEvent>(boot.Store, targetUsageId);
        Assert.NotNull(targetUsage);
        Assert.Equal($"deleted:{target}", targetUsage.ActorId);
        Assert.Equal("GET /posts/{id}", targetUsage.RouteTemplate);

        var targetToken = await LoadAsync<EscalationToken>(boot.Store, targetTokenId);
        Assert.NotNull(targetToken);
        Assert.Equal($"deleted:{target}", targetToken.CreatedBy);
        // The token hash (the operator credential) is untouched.
        Assert.Equal("0000000000000000000000000000000000000000000000000000000000000000",
                     targetToken.TokenHash);

        // A UsageEvent for a different ActorId is untouched.
        var otherUsage = await LoadAsync<UsageEvent>(boot.Store, otherUsageId);
        Assert.NotNull(otherUsage);
        Assert.Equal(other, otherUsage.ActorId);
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
        // GuestAccess, DelegationGrant, OutboxEmail) AND the IS23
        // child-scoped deletes (GuardianTimeLimitSchedule +
        // CommunityMembershipRequest — both in CoreDocTypes). The IS22
        // additions are SessionDocTypes (SessionDoc) +
        // NotificationDocTypes (the 4 Notification* types). The IS23
        // additions are EventDocTypes (GuardianEventRequest),
        // ErrorReportDocTypes (ErrorReport), EscalationDocTypes
        // (EscalationToken), and UsageDocTypes (UsageEvent). The
        // Messaging/GroupMessaging/Media surfaces are the IS21 base
        // (DeleteCoreAsync runs ResidentContentEraser →
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
            EventDocTypes.Configure(opts);
            ErrorReportDocTypes.Configure(opts);
            EscalationDocTypes.Configure(opts);
            UsageDocTypes.Configure(opts);
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
