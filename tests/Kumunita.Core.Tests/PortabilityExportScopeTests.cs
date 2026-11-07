using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.GroupMessaging;
using Kumunita.Core.Messaging;
using Kumunita.Core.Pages;
using Kumunita.Core.Portability;
using Kumunita.Core.Posts;
using Kumunita.Core.Projects;
using Kumunita.Core.Tags;
using Kumunita.Core.UserInfo;
using Marten;
using Npgsql;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// IS27 — the <see cref="PortabilityExportScope"/> content-free filter (ADR 0299,
/// design §2.6). These tests drive <see cref="PortabilityExportDocuments.ExportAsync"/>
/// directly (the docs-only seam, no Identity/Media dependencies) and verify:
/// <list type="bullet">
///   <item><b>Rule 1</b> — <see cref="Privacy.DataClassKind.PrivateContent"/> types
///         (without an Audience property) are omitted (the 23 of the 30,
///         including <c>Conversation</c>/<c>Message</c> — the 1:1 messaging pair,
///         which are the only messaging types in the M11 44-type inventory;
///         <c>GroupConversation</c>/<c>GroupMessage</c> are not in the M11 inventory
///         and therefore not in the export at all).</item>
///   <item><b>Rule 2</b> — the 7 audience-bearing <see cref="Privacy.DataClassKind.PrivateContent"/>
///         types keep public rows (<c>Audience</c> empty / <c>null</c>, no
///         <c>Community</c>/<c>AllResidents</c> flags) and drop restricted ones.</item>
///   <item><b>Rule 3</b> — <c>Profile</c> PII fields (<c>Email</c>/<c>Phone</c>/<c>Address</c>)
///         are blanked.</item>
///   <item><b>Full</b> — all private rows are present (the pre-IS27 behaviour, unchanged).</item>
/// </list>
/// The 8 <c>TODO ❓</c> types (design §2.6, IS27 default: skipped) are
/// <see cref="Privacy.DataClassKind.AuditModeration"/> or
/// <see cref="Privacy.DataClassKind.Operational"/> with
/// <see cref="Privacy.ExportScope.Never"/> — they are covered by the same
/// skip logic as the PrivateContent types. The 5 always-present types
/// (<see cref="Privacy.DataClassKind.Platform"/>,
/// <see cref="Privacy.DataClassKind.Media"/>, and
/// <see cref="Privacy.DataClassKind.AuditModeration"/> with
/// <see cref="Privacy.ExportScope.InstanceOnly"/>) are kept.
/// </summary>
public sealed class PortabilityExportScopeTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── The minimal boot — the docs-only store (no Identity/Media needed) ──

    private async Task<IDocumentStore> BootAsync(CancellationToken ct)
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
            NotificationDocTypes.Configure(opts);
            MessagingDocTypes.Configure(opts);
            MediaDocTypes.Configure(opts);
            TagDocTypes.Configure(opts);
            PageDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);
        return store;
    }

    // ── The minimal plant — one row per type IS27's filter exercises ──

    private async Task PlantTestRowsAsync(IDocumentStore store, CancellationToken ct)
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());

        // A component (the Post's parent).
        session.Store(new Component
        {
            Id = "pf-component-001",
            Name = "Test Community",
        });

        // A Profile with PII (IS27 Rule 3).
        session.Store(new Profile
        {
            SubjectId = "pf-user-001",
            DisplayName = "pf user one",
            Email = "user-a@example.test",
            Phone = "+15550001",
            Address = "123 Test St",
        });

        // A public Post (empty Audience → IS27 Rule 2: stays; the codebase's
        // "no specific audience restriction" shape — Audience.IsEmpty, no
        // Community/AllResidents flags; the design §2.6 "null/empty" rule).
        session.Store(new Post
        {
            Id = "pf-post-public",
            ComponentId = "pf-component-001",
            AuthorId = "pf-user-001",
            Title = "public post",
            Body = "public body",
            Audience = new Audience(),
            GroupId = string.Empty,
            LanguageCode = "en",
            Created = DateTimeOffset.UtcNow,
        });

        // A restricted Post (Audience non-null → IS27 Rule 2: dropped).
        session.Store(new Post
        {
            Id = "pf-post-restricted",
            ComponentId = "pf-component-001",
            AuthorId = "pf-user-001",
            Title = "restricted post",
            Body = "restricted body",
            Audience = new Audience(AudienceMode.All,
                [new AudienceGrant(GrantKind.User, "pf-user-002")]),
            GroupId = string.Empty,
            LanguageCode = "en",
            Created = DateTimeOffset.UtcNow,
        });

        // A 1:1 Conversation + Message (IS27 Rule 1: both omitted in ContentFree).
        session.Store(new Conversation
        {
            Id = "pf-conv-001",
            ParticipantA = "pf-user-001",
            ParticipantB = "pf-user-002",
            Created = DateTimeOffset.UtcNow,
        });
        session.Store(new Message
        {
            Id = "pf-msg-001",
            ConversationId = "pf-conv-001",
            SenderId = "pf-user-001",
            Body = "1:1 body",
            Created = DateTimeOffset.UtcNow,
        });

        // A GroupConversation + GroupMessage (IS27 Rule 1: both omitted in ContentFree).
        session.Store(new GroupConversation
        {
            Id = "pf-gconv-001",
            CreatorId = "pf-user-001",
            Title = "group thread",
            Created = DateTimeOffset.UtcNow,
            Participants = ["pf-user-001", "pf-user-002"],
        });
        session.Store(new GroupMessage
        {
            Id = "pf-gmsg-001",
            ConversationId = "pf-gconv-001",
            SenderId = "pf-user-001",
            Body = "group body",
            Created = DateTimeOffset.UtcNow,
        });

        await session.SaveChangesAsync(ct);
    }

    // ── The four IS27 tests ──────────────────────────────────────────────

    [Fact]
    public async Task ContentFree_HasNoConversationsOrMessages()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await BootAsync(ct);
        await PlantTestRowsAsync(store, ct);

        var (docs, docCounts) = await PortabilityExportDocuments.ExportAsync(
            store, PortabilityExportScope.ContentFree, ct);

        // Rule 1: the 1:1 messaging types are PrivateContent without Audience
        // → the entire type is omitted (empty array in the closed 44-entry map).
        Assert.Equal(0, docCounts["Conversation"]);
        Assert.Equal(0, docCounts["Message"]);

        // The type is still in the closed 44-entry map (the manifest's doc_counts
        // stays a closed inventory — an empty array, not a missing key).
        Assert.True(docs.ContainsKey("Conversation"));
        Assert.True(docs.ContainsKey("Message"));

        // Rule 1 sanity: the Post type (audience-bearing PrivateContent) is NOT
        // skipped by Rule 1 — it is filtered by Rule 2 (row-level).
        Assert.True(docCounts.ContainsKey("Post"));
        Assert.True(docs.ContainsKey("Post"));
    }

    [Fact]
    public async Task ContentFree_HasNoRestrictedPosts_HasPublicOnes()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await BootAsync(ct);
        await PlantTestRowsAsync(store, ct);

        var (docs, docCounts) = await PortabilityExportDocuments.ExportAsync(
            store, PortabilityExportScope.ContentFree, ct);

        // Rule 2: the Post type is present (audience-bearing PrivateContent),
        // but only the public rows (Audience = null) survive.
        Assert.Equal(1, docCounts["Post"]);

        // Deserialize and verify it is the public post, not the restricted one.
        var rows = KumunitaArchive.FromJson<List<Post>>(docs["Post"])!;
        Assert.Single(rows);
        Assert.Equal("pf-post-public", rows[0].Id);
        Assert.True(rows[0].Audience!.IsEmpty);
        Assert.False(rows[0].Audience!.Community);
        Assert.False(rows[0].Audience!.AllResidents);
    }

    [Fact]
    public async Task ContentFree_BlanksProfileContactFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await BootAsync(ct);
        await PlantTestRowsAsync(store, ct);

        var (docs, docCounts) = await PortabilityExportDocuments.ExportAsync(
            store, PortabilityExportScope.ContentFree, ct);

        // Rule 3: the Profile row is present (the resident's own data)
        // but the PII fields are blanked.
        Assert.Equal(1, docCounts["Profile"]);

        var rows = KumunitaArchive.FromJson<List<Profile>>(docs["Profile"])!;
        Assert.Single(rows);
        Assert.Equal("pf-user-001", rows[0].SubjectId);
        Assert.Equal("pf user one", rows[0].DisplayName);

        // Rule 3: the three PII fields are blanked.
        Assert.Null(rows[0].Email);
        Assert.Null(rows[0].Phone);
        Assert.Null(rows[0].Address);
    }

    [Fact]
    public async Task Full_RoundTripsAsBefore()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await BootAsync(ct);
        await PlantTestRowsAsync(store, ct);

        var (docs, docCounts) = await PortabilityExportDocuments.ExportAsync(
            store, PortabilityExportScope.Full, ct);

        // Full: all private rows are present (the pre-IS27 behaviour).
        Assert.Equal(1, docCounts["Conversation"]);
        Assert.Equal(1, docCounts["Message"]);

        // Full: both posts (public + restricted) are present.
        Assert.Equal(2, docCounts["Post"]);

        // Full: the Profile PII fields are intact (no blanking).
        Assert.Equal(1, docCounts["Profile"]);
        var rows = KumunitaArchive.FromJson<List<Profile>>(docs["Profile"])!;
        Assert.Single(rows);
        Assert.Equal("user-a@example.test", rows[0].Email);
        Assert.Equal("+15550001", rows[0].Phone);
        Assert.Equal("123 Test St", rows[0].Address);
    }
}
