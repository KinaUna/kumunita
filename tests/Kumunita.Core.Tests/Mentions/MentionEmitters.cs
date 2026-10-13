using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Mentions;
using Kumunita.Core.Notifications;
using Kumunita.Core.UserInfo;
using Marten;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M36 (ADR 0165, Part 2.7) — the shared harness for the **per-surface mention
/// emitter** tests (the nine content lanes, additive to the existing per-surface
/// test classes). Boots a fresh scratch store with the M1 surface (<c>Profile</c> +
/// <c>AccessAudit</c>) + the M6 surface (<c>Notification</c>) + the caller's
/// surface doc types, and wires the **real** <see cref="NotificationService"/>
/// (recording mailer + <see cref="ITranslationProvider"/> substitute) + the **real**
/// <see cref="MentionService"/> (over the frozen M2 <see cref="DirectoryService"/>
/// catalog read, M36·2 — reused, not re-shaped).
/// <para>
/// The <see cref="NotificationService"/> / <see cref="MentionService"/> are the
/// **frozen** U03/U05 seams — the per-surface services (U04/U05) consume them
/// unchanged; these pins drive the surface's own write lane and assert the
/// mention <c>Notification</c> row it emits (the per-recipient key
/// <c>notification:{kind}:{contentId}:{recipientId}</c>, M36·4) + the staged
/// best-effort email (the M6 D7 email gate). The inert cases (author / unknown /
/// ambiguous — M36-5) resolve to **no** recipient, so the surface emits **no**
/// mention row. The draft-capable lanes (community post / group post /
/// announcement — M36·8) run the mention emitter **only** on the publishing lane,
/// so a draft save emits no mention row.
/// </para>
/// </summary>
internal static class MentionEmitters
{
    /// <summary>The booted scratch store + the two frozen seams the surface
    /// services consume, + the recording mailer's staged envelopes.</summary>
    public sealed class Boot
    {
        public required IDocumentStore Store { get; init; }
        public required UserInfoService UserInfo { get; init; }
        public required NotificationService Notifications { get; init; }
        public required MentionService Mentions { get; init; }
        public required List<(string Key, string Recipient, string Subject, string Body)> Staged { get; init; }
    }

    /// <summary>Boot a fresh scratch DB (M1 + M6 + the caller's surface doc types)
    /// and wire the real <see cref="NotificationService"/> +
    /// <see cref="MentionService"/> over the frozen M2 directory read.</summary>
    public static async Task<Boot> BootAsync(PostgresFixture fixture, params Action<Marten.StoreOptions>[] surfaceDocTypes)
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);   // Profile + AccessAudit
            M6DocTypes.Configure(opts);   // Notification (the mention row the surface emits)
            foreach (var cfg in surfaceDocTypes) cfg(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        var userInfo = new UserInfoService(store);
        // The frozen M2 directory read (M36·2) — a read, not a decision (M36·5),
        // so a bare IAuthorizationService substitute satisfies the ctor guard.
        var directory = new DirectoryService(userInfo, Substitute.For<IAuthorizationService>());
        var mentions = new MentionService(directory);

        var translator = Substitute.For<ITranslationProvider>();
        translator.GetAsync(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(ci => Task.FromResult((string)ci[0]));

        var staged = new List<(string Key, string Recipient, string Subject, string Body)>();
        var mailer = Substitute.For<IMailerStage>();
        mailer.StageAsync(
                Arg.Any<IDocumentSession>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                staged.Add(((string)callInfo[1], (string)callInfo[2], (string)callInfo[3], (string)callInfo[4]));
                return Task.CompletedTask;
            });

        var notifications = new NotificationService(store, userInfo, translator, mailer);

        return new Boot
        {
            Store = store,
            UserInfo = userInfo,
            Notifications = notifications,
            Mentions = mentions,
            Staged = staged,
        };
    }

    /// <summary>Plant non-blocked <see cref="Profile"/> rows (the directory
    /// <see cref="MentionService"/> resolves against). Each tuple is a
    /// (subject id, display name); the recipient's <c>Email</c> is set so the
    /// M6 D7 email gate stages (a best-effort nudge).</summary>
    public static async Task PlantResidents(IDocumentStore store, params (string Id, string Name)[] residents)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        foreach (var (id, name) in residents)
        {
            w.Store(new Profile { SubjectId = id, DisplayName = name, Verified = true, Blocked = false, Email = id + "@ex.net" });
        }
        await w.SaveChangesAsync(ct);
    }

    /// <summary>All <see cref="Notification"/> rows (unsorted — the test
    /// inspects by <c>Kind</c> / <c>RecipientId</c>).</summary>
    public static async Task<IReadOnlyList<Notification>> AllNotifications(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var q = store.QuerySession();
        return await q.Query<Notification>().ToListAsync(ct);
    }
}
