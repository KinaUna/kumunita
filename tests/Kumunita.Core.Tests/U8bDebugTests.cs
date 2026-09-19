using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Posts;
using Kumunita.Core.Tags;
using Kumunita.Core.UserInfo;
using Marten;
using Marten.Services;
using Xunit;

namespace Kumunita.Core.Tests;

public class U8bDebugTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Debug_U8b_PostTagIdsInDb()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
            M3DocTypes.Configure(opts);
            PageDocTypes.Configure(opts);
            TagDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        // Seed fixtures
        await using (var w = store.OpenSession(new SessionOptions()))
        {
            w.Store(new LanguageCatalog { Id = "en", NativeName = "en", Enabled = true, SortOrder = 0 });
            w.Store(new LocaleSettings { DefaultLanguageCode = "en" });
            w.Store(new Component { Id = "c1", Name = "C1", Enabled = true });
            await w.SaveChangesAsync(ct);
        }

        var userInfo = new UserInfoService(store);
        await userInfo.SetCommunityMembershipAsync("c1", "u1", "admin");

        var authz = new AuthorizationService(store, userInfo);
        var translations = new TranslationProvider(store);
        var tagSvc = new TagService(store, authz, translations);
        var postsSvc = new PostService(userInfo, authz, store, tagSvc);

        var draft = new PostDraft("c1", "T", "B",
            new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, "u1")]),
            LanguageCode: "en", TagSlugs: ["sanitation"]);

        Post created;
        await using (var session = store.OpenSession(new SessionOptions()))
        {
            created = await postsSvc.CreatePostAsync(draft, "u1", new HashSet<string> { Roles.Member }, session);
        }

        // Check the post's TagIds on the returned object
        System.Console.WriteLine($"RETURNED POST: TagIds.Count={(created.TagIds?.Count ?? -1)}");
        if (created.TagIds is { Count: > 0 })
            System.Console.WriteLine($"  TagId[0]={created.TagIds[0]}");

        // Check the post's TagIds as loaded from DB
        await using (var q = store.QuerySession())
        {
            var loaded = await q.LoadAsync<Post>(created.Id, ct);
            System.Console.WriteLine($"LOADED POST: TagIds.Count={(loaded?.TagIds?.Count ?? -1)}");
            if (loaded?.TagIds is { Count: > 0 })
                System.Console.WriteLine($"  TagId[0]={loaded.TagIds[0]}");

            var tags = await q.Query<Tag>().ToListAsync(ct);
            System.Console.WriteLine($"TAGS: {tags.Count}");
            foreach (var t in tags)
                System.Console.WriteLine($"  Tag Id={t.Id} Slug={t.Slug} CreatedBy={t.CreatedBy}");

            var audits = await q.Query<Authorization.AccessAudit>().ToListAsync(ct);
            System.Console.WriteLine($"AUDITS: {audits.Count}");
            foreach (var a in audits)
                System.Console.WriteLine($"  Audit Action={a.Action} TargetId={a.TargetId} ActorId={a.ActorId}");
        }

        // Now try ListForActorAsync
        var items = await tagSvc.ListForActorAsync("u1");
        System.Console.WriteLine($"LISTFORACTOR: {items.Count} items");
        foreach (var i in items)
            System.Console.WriteLine($"  Item Slug={i.Tag.Slug} UseCount={i.UseCount}");

        // Also check the authorization decision directly
        await using (var q2 = store.QuerySession())
        {
            var post = await q2.LoadAsync<Post>(created.Id, ct);
            var decision = await authz.CanAsync("u1", AccessAction.Read, new PostToAuditableResource(post));
            System.Console.WriteLine($"CANASYNC: Allowed={decision.Allowed} Via={decision.Via}");
        }
    }
}
