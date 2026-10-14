using Kumunita.Core;
using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.PersonalNotes;
using Kumunita.Core.Posts;
using Kumunita.Core.Projects;
using Kumunita.Core.UserInfo;
using Marten;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M37 / U02 — the 15 pinned <see cref="PersonalNoteService"/> seam tests
/// (design doc <c>m37-personal-notes-design.md</c> §2.5, names locked
/// verbatim). U02 writes tests 1–12 (note-related) + 16–18 (no-call
/// invariants); the 3 folder-tree tests (13–15, <c>C_M37_6_*</c>) are
/// U03's.
/// <para>
/// The harness follows the M17 <c>BookmarkServiceTests</c> shape: a
/// shared <see cref="PostgresFixture"/> (Testcontainers
/// <c>postgres:18</c>), a fresh scratch database per test method, the
/// full DDL applied (<c>CoreDocTypes</c> + <c>PostDocTypes</c> +
/// <c>PersonalNotesDocTypes</c>), and the
/// <see cref="PersonalNoteService"/> composition over the scratch store.
/// No NSubstitute — the frozen seams are real (the M2
/// <c>DirectoryServiceTests</c> / M3 <c>PostServiceTests</c> precedent).
/// </para>
/// <para>
/// **F2 tests** drive the D3 write-lane visibility check with a
/// <c>post</c> target (the most representative surface): a visible post
/// (author-branch Allow) passes; an invisible post (neither author nor
/// in the audience) is Refused (KeyNotFoundException). The other eight
/// kinds follow the same pattern.
/// </para>
/// </summary>
public class PersonalNoteServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string ComponentId = "c-u9-comp";

    // ── 1 — F1_AttachIdempotentOneRowOneCreated (F1, the unique-index witness) ──

    [Fact]
    public async Task F1_AttachIdempotentOneRowOneCreated()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-f1-owner";
        const string postId = "f1-post";

        await PlantVisiblePost(store, postId, owner);

        // First attach → Created.
        var first = await RunInSession(store, s => svc.CreateNoteAsync(owner, "post", postId, "my note", s));
        Assert.Equal("my note", first.Body);

        // Second attach → AlreadyNoted (the F1 no-op).
        var second = await RunInSession(store, s => svc.CreateNoteAsync(owner, "post", postId, "my note", s));
        Assert.Equal(first.Id, second.Id); // same row, not a new one

        // The PersonalNotesDocTypes unique index is the witness: exactly ONE row.
        var rows = await Notes(store, owner);
        Assert.Single(rows, n => n.TargetKind == "post" && n.TargetId == postId);
    }

    // ── 2 — F1_ReAttachAfterDelete_FreshCreated (D4 / F1) ─────────────────

    [Fact]
    public async Task F1_ReAttachAfterDelete_FreshCreated()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-f1b-owner";
        const string postId = "f1b-post";

        await PlantVisiblePost(store, postId, owner);

        // Attach → Created.
        var first = await RunInSession(store, s => svc.CreateNoteAsync(owner, "post", postId, "body", s));
        var firstCreated = (await Notes(store, owner)).First(n => n.TargetId == postId).Created;

        // Delete.
        await RunInSession(store, s => svc.DeleteNoteAsync(owner, first.Id, s));
        Assert.Empty((await Notes(store, owner)).Where(n => n.TargetId == postId));

        // Re-attach after removal is a fresh Created (D4 / F1).
        var reAttached = await RunInSession(store, s => svc.CreateNoteAsync(owner, "post", postId, "body again", s));
        var secondCreated = (await Notes(store, owner)).First(n => n.TargetId == postId).Created;

        // Fresh row, fresh Created timestamp.
        Assert.NotEqual(first.Id, reAttached.Id);
        Assert.True(secondCreated >= firstCreated); // fresh (may be equal if in the same ms)
        var rows = await Notes(store, owner);
        Assert.Single(rows, n => n.TargetKind == "post" && n.TargetId == postId);
    }

    // ── 3 — F2_AttachInvisibleTarget_Refused_NoRow_NoDenyRow (D3, negative) ──

    [Fact]
    public async Task F2_AttachInvisibleTarget_Refused_NoRow_NoDenyRow()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-f2-owner";      // the one trying to attach
        const string poster = "u-f2-poster";    // the post's author
        const string postId = "f2-post";

        // A post authored by "poster" with an audience that grants only
        // "poster" — the owner is neither the author nor in the audience.
        await Plant(store, new Component { Id = ComponentId, Name = "Test", Enabled = true });
        await Plant(store, new Post
        {
            Id = postId,
            ComponentId = ComponentId,
            AuthorId = poster,
            Body = "f2 body",
            Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, poster),
        });

        // D3: the target's own CanAsync(Read) → Deny ⇒ Refused (404),
        // no row created.
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => RunInSession(store, s => svc.CreateNoteAsync(owner, "post", postId, "body", s)));

        // No PersonalNote row survives.
        var rows = await Notes(store, owner);
        Assert.DoesNotContain(rows, n => n.TargetId == postId);

        // No M37 Deny row survives (the M16 create-gate posture — M37 adds
        // no audit row of its own; the target's own CanAsync(Read) decision
        // row is the target's, not M37's — design doc §2.3).
        var audits = await AllAudits(store);
        Assert.DoesNotContain(audits, a =>
            string.Equals(a.TargetKind, "personalnote", StringComparison.OrdinalIgnoreCase));
    }

    // ── 4 — F2_AttachAbsentTarget_Refused_NoRow (D3, absent target) ──────

    [Fact]
    public async Task F2_AttachAbsentTarget_Refused_NoRow()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-f2b-owner";
        const string ghostId = "f2b-ghost-post"; // does not exist

        // D3: the target is absent ⇒ Refused (404), no row.
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => RunInSession(store, s => svc.CreateNoteAsync(owner, "post", ghostId, "body", s)));

        // No PersonalNote row survives.
        var rows = await Notes(store, owner);
        Assert.DoesNotContain(rows, n => n.TargetId == ghostId);
    }

    // ── 5 — F3_OwnerList_ZeroAuditRows_ZeroCanSeeAsyncPasses (C-M37·2) ──

    [Fact]
    public async Task F3_OwnerList_ZeroAuditRows_ZeroCanSeeAsyncPasses()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-f3-owner";
        const string postId = "f3-post";

        await PlantVisiblePost(store, postId, owner);

        // Attach (creates the target's own audit row via CanAsync(Read)).
        await RunInSession(store, s => svc.CreateNoteAsync(owner, "post", postId, "body", s));

        // Snapshot the audit count BEFORE the list load.
        var before = (await AllAudits(store)).Count;

        // ListNotesAsync — the owner's personal read (C-M37·2: zero audit rows).
        var list = await svc.ListNotesAsync(owner);
        Assert.Single(list.Groups, g => g.Kind == "post");
        Assert.Single(list.Groups.First(g => g.Kind == "post").Items);

        // The list load itself committed ZERO new audit rows.
        var after = (await AllAudits(store)).Count;
        Assert.Equal(before, after);
    }

    // ── 6 — F3_NonOwnerList_404_NoAuditRow (C-M37·2, non-owner) ──────────

    [Fact]
    public async Task F3_NonOwnerList_404_NoAuditRow()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-f3b-owner";
        const string stranger = "u-f3b-stranger";
        const string postId = "f3b-post";

        await PlantVisiblePost(store, postId, owner);
        await RunInSession(store, s => svc.CreateNoteAsync(owner, "post", postId, "body", s));

        // A non-owner (a GlobalAdmin included) gets an empty list and
        // zero new audit rows (the Web layer maps to 404; the Core
        // seam just returns nothing for a non-owner — the ADR 0105
        // "operator has no read standing" precedent).
        var before = (await AllAudits(store)).Count;
        var list = await svc.ListNotesAsync(stranger);
        Assert.Empty(list.Groups);
        var after = (await AllAudits(store)).Count;
        Assert.Equal(before, after);
    }

    // ── 7 — F3_NonOwnerGetNote_404_NoAuditRow (C-M37·2, non-owner) ───────

    [Fact]
    public async Task F3_NonOwnerGetNote_404_NoAuditRow()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-f3c-owner";
        const string stranger = "u-f3c-stranger";
        const string postId = "f3c-post";

        await PlantVisiblePost(store, postId, owner);
        var note = await RunInSession(store, s => svc.CreateNoteAsync(owner, "post", postId, "body", s));

        // A non-owner gets a non-leaky 404 (KeyNotFoundException) and
        // zero new audit rows (the ADR 0105 "operator has no read
        // standing" precedent).
        var before = (await AllAudits(store)).Count;
        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.GetNoteAsync(stranger, note.Id));
        var after = (await AllAudits(store)).Count;
        Assert.Equal(before, after);
    }

    // ── 8 — F4_DegradedNote_BodyStillVisible_NoTitle_NoLink_NoId (D7) ────

    [Fact]
    public async Task F4_DegradedNote_BodyStillVisible_NoTitle_NoLink_NoId()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-f4-owner";
        const string ghostId = "f4-ghost-post"; // does not exist

        // Plant the note row directly (no attach — the target is absent,
        // so the D3 check would refuse).
        var now = DateTimeOffset.UtcNow;
        await Plant(store, new PersonalNote
        {
            Id = "f4-note-1",
            OwnerId = owner,
            TargetKind = "post",
            TargetId = ghostId,
            Body = "my precious body text",
            Created = now,
        });

        // The list resolves the dangling row → Degraded, no Title, no Link,
        // **and the body remains visible** to the owner (D4 / D7).
        var list = await svc.ListNotesAsync(owner);
        var group = Assert.Single(list.Groups, g => g.Kind == "post");
        var row = Assert.Single(group.Items);
        Assert.True(row.Degraded);
        Assert.Null(row.Title);
        Assert.Null(row.Link);
        Assert.Equal(ghostId, row.TargetId);
        Assert.Equal("my precious body text", row.Body); // body survives (D4)
    }

    // ── 9 — F4_DegradedNote_ControlsStillWork (D7 / F4) ──────────────────

    [Fact]
    public async Task F4_DegradedNote_ControlsStillWork()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-f4b-owner";
        const string ghostId = "f4b-ghost-post";

        await Plant(store, new PersonalNote
        {
            Id = "f4b-note-1",
            OwnerId = owner,
            TargetKind = "post",
            TargetId = ghostId,
            Body = "dangling body",
            Created = DateTimeOffset.UtcNow,
        });

        // The note controls (edit / delete) work on the degraded row —
        // none of them require the target's own read seam (they are keyed
        // on the note's own id + the owner's own tree).

        // Edit (UpdateNoteBodyAsync) works on the dangling row.
        var edited = await RunInSession(store, s => svc.UpdateNoteBodyAsync(owner, "f4b-note-1", "edited body", s));
        Assert.Equal("edited body", edited.Body);
        Assert.NotNull(edited.Modified); // the Modified witness (D4)

        // Delete (DeleteNoteAsync) works on the dangling row (no target read).
        var deleteResult = await RunInSession(store, s => svc.DeleteNoteAsync(owner, "f4b-note-1", s));
        Assert.Equal(NoteDeleteStatus.Deleted, deleteResult);

        var rows = await Notes(store, owner);
        Assert.DoesNotContain(rows, n => n.Id == "f4b-note-1");
    }

    // ── 10 — C_M37_4_BodyIsContent_NotPointer (D4 / C-M37·4) ─────────────

    [Fact]
    public void C_M37_4_BodyIsContent_NotPointer()
    {
        var props = typeof(PersonalNote).GetProperties()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        // The exact field set (D1/D2/D4, §2.1 — the frozen pin):
        Assert.Contains("Id", props);
        Assert.Contains("OwnerId", props);
        Assert.Contains("TargetKind", props);
        Assert.Contains("TargetId", props);
        Assert.Contains("Body", props); // D4 — a note IS content
        Assert.Contains("FolderId", props);
        Assert.Contains("Tags", props);
        Assert.Contains("Created", props);
        Assert.Contains("Modified", props);

        // What is FORBIDDEN (D1/D2/D4 — the anti-shape):
        Assert.DoesNotContain("Audience", props);
        Assert.DoesNotContain("IsDeleted", props);
        Assert.DoesNotContain("ComponentId", props);
        Assert.DoesNotContain("LanguageCode", props);
    }

    // ── 11 — C_M37_5_TagNormalization_Lowercase_Trimmed_Deduped_Sorted ──

    [Fact]
    public async Task C_M37_5_TagNormalization_Lowercase_Trimmed_Deduped_Sorted()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-f5-owner";
        const string postId = "f5-post";

        await PlantVisiblePost(store, postId, owner);
        var note = await RunInSession(store, s => svc.CreateNoteAsync(owner, "post", postId, "body", s));

        // Add tags in mixed case / with whitespace.
        await RunInSession(store, s => svc.AddTagAsync(owner, note.Id, "  Beta  ", s));
        await RunInSession(store, s => svc.AddTagAsync(owner, note.Id, "ALPHA", s));
        await RunInSession(store, s => svc.AddTagAsync(owner, note.Id, "beta", s)); // duplicate (case-insensitive)

        var result = await svc.GetNoteAsync(owner, note.Id);
        Assert.Equal(["alpha", "beta"], result.Tags.ToList()); // lowercase, trimmed, deduped, sorted
    }

    // ── 12 — C_M37_5_TagIsNotATagDoc (D5 / C-M37·5) ──────────────────────

    [Fact]
    public void C_M37_5_TagIsNotATagDoc()
    {
        // M37's tags are *personal* labels (IReadOnlyList<string>), not
        // references to the M26 Tag doc. The PersonalNote doc has a Tags
        // field of type IReadOnlyList<string>? — not a list of TagIds.
        var tagsProp = typeof(PersonalNote).GetProperty("Tags");
        Assert.NotNull(tagsProp);
        // IReadOnlyList<string>? is a nullable reference type annotation —
        // the runtime type is still IReadOnlyList<string> (the interface),
        // NOT a list of Tag doc ids.
        Assert.Equal(typeof(IReadOnlyList<string>), tagsProp.PropertyType);
    }

    // ── 13 — PersonalNoteService_MakesNoModerateCall (C-M37·1) ───────────

    [Fact]
    public async Task PersonalNoteService_MakesNoModerateCall()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-f4e-owner";
        const string postId = "f4e-post";

        await PlantVisiblePost(store, postId, owner);

        // Attach → the D3 check uses CanAsync(Read), never Moderate.
        await RunInSession(store, s => svc.CreateNoteAsync(owner, "post", postId, "body", s));

        // Assert no audit row with a "moderate" action was created.
        var audits = await AllAudits(store);
        Assert.DoesNotContain(audits, a =>
            string.Equals(a.Action, "moderate", StringComparison.OrdinalIgnoreCase));
    }

    // ── 14 — PersonalNoteService_MakesNoCanSeeAsyncCallOnList (C-M37·2) ──

    [Fact]
    public async Task PersonalNoteService_MakesNoCanSeeAsyncCallOnList()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-f4f-owner";
        const string postId = "f4f-post";

        await PlantVisiblePost(store, postId, owner);
        await RunInSession(store, s => svc.CreateNoteAsync(owner, "post", postId, "body", s));

        // The list resolution uses CanAsync (single-target) for each
        // visible row, but it NEVER calls CanSeeAsync (bulk) over the
        // note rows themselves — C-M37·2. The observable effect:
        // no aggregate audit row (TargetKind = "personalnote" or similar).
        var list = await svc.ListNotesAsync(owner);
        Assert.Single(list.Groups);

        // No new audit rows with a "personalnote"-like TargetKind were created.
        var audits = await AllAudits(store);
        Assert.DoesNotContain(audits, a =>
            string.Equals(a.TargetKind, "personalnote", StringComparison.OrdinalIgnoreCase));
    }

    // ── 15 — PersonalNoteService_MakesNoAccessAuditRowOnOwnerList (C-M37·2) ──

    [Fact]
    public async Task PersonalNoteService_MakesNoAccessAuditRowOnOwnerList()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-f4g-owner";
        const string postId = "f4g-post";

        await PlantVisiblePost(store, postId, owner);
        await RunInSession(store, s => svc.CreateNoteAsync(owner, "post", postId, "body", s));

        // The owner's own list load commits ZERO AccessAudit rows — C-M37·2.
        var before = (await AllAudits(store)).Count;
        var list = await svc.ListNotesAsync(owner);
        Assert.Single(list.Groups, g => g.Kind == "post");
        Assert.Single(list.Groups.First(g => g.Kind == "post").Items);
        var after = (await AllAudits(store)).Count;
        Assert.Equal(before, after);
    }

    // ── 13 — C_M37_6_FolderTree_DepthBounded_UniqueName (D6 / C-M37·6) ───

    [Fact]
    public async Task C_M37_6_FolderTree_DepthBounded_UniqueName()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-c6-owner";

        // Depth bound (D6): root → child is the whole tree.
        var root = await RunInSession(store, s => svc.CreateFolderAsync(owner, "Work", null, s));
        var child = await RunInSession(store, s => svc.CreateFolderAsync(owner, "Deep", root.Id, s));
        Assert.Equal(root.Id, child.ParentId); // the child hangs under the root

        // A grandchild would open the tree past the depth ≤ 2 bound — refused
        // (the Web layer maps InvalidOperationException to 400 — the depth is
        // violated), and nothing is committed.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => RunInSession(store, s => svc.CreateFolderAsync(owner, "TooDeep", child.Id, s)));
        var foldersAfterReject = await Folders(store, owner);
        Assert.DoesNotContain(foldersAfterReject, f => f.Name == "TooDeep");

        // Unique name within the owner's tree (D6): the (OwnerId, ParentId,
        // Name) unique index is the witness. A same-name, same-parent folder
        // is a no-op (one row — returns the existing row); the same name
        // under a *different* parent is a fresh row.
        var dupRoot = await RunInSession(store, s => svc.CreateFolderAsync(owner, "Work", null, s));
        Assert.Equal(root.Id, dupRoot.Id); // no-op — the existing root, not a new one

        var sameNameUnderRoot =
            await RunInSession(store, s => svc.CreateFolderAsync(owner, "Work", root.Id, s));
        Assert.NotEqual(root.Id, sameNameUnderRoot.Id); // same name, different parent — a new row
        Assert.Equal(root.Id, sameNameUnderRoot.ParentId);

        // The (OwnerId, ParentId, Name) unique index is the DB witness: the
        // two "Work" rows differ only by ParentId, so both are allowed.
        var folders = await Folders(store, owner);
        Assert.Equal(3, folders.Count); // Work(root) + Deep(child) + Work(under root)
    }

    // ── 14 — C_M37_6_FolderDeletion_MovesNotesToRoot_BodiesSurvive (D6) ──

    [Fact]
    public async Task C_M37_6_FolderDeletion_MovesNotesToRoot_BodiesSurvive()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-c6b-owner";

        var root = await RunInSession(store, s => svc.CreateFolderAsync(owner, "Archive", null, s));
        var child = await RunInSession(store, s => svc.CreateFolderAsync(owner, "Old", root.Id, s));

        // Plant two notes — one in the root folder, one in the child — each
        // with its own body.
        await Plant(store, new PersonalNote
        {
            Id = "c6b-note-root", OwnerId = owner, TargetKind = "post",
            TargetId = "c6b-p1", Body = "body in root folder",
            FolderId = root.Id, Created = DateTimeOffset.UtcNow,
        });
        await Plant(store, new PersonalNote
        {
            Id = "c6b-note-child", OwnerId = owner, TargetKind = "post",
            TargetId = "c6b-p2", Body = "body in child folder",
            FolderId = child.Id, Created = DateTimeOffset.UtcNow,
        });

        // Delete the root folder — it has a child, so the outcome is
        // FoldersDeleted (child recursively deleted).
        var result = await RunInSession(store, s => svc.DeleteFolderAsync(owner, root.Id, s));
        Assert.Equal(FolderDeleteResult.FoldersDeleted, result);

        // Both folders (root + child) are physically removed.
        var folders = await Folders(store, owner);
        Assert.DoesNotContain(folders, f => f.Id == root.Id);
        Assert.DoesNotContain(folders, f => f.Id == child.Id);

        // BOTH notes survive at the root — the note bodies are never lost
        // (the D6 pin: folder deletion is a re-parent, not a note removal).
        var notes = await Notes(store, owner);
        var rootNote = Assert.Single(notes, n => n.Id == "c6b-note-root");
        var childNote = Assert.Single(notes, n => n.Id == "c6b-note-child");
        Assert.Null(rootNote.FolderId);    // moved to the root
        Assert.Null(childNote.FolderId);   // moved to the root (from the child)
        Assert.Equal("body in root folder", rootNote.Body);   // body survives
        Assert.Equal("body in child folder", childNote.Body); // body survives
    }

    // ── 15 — C_M37_6_NoteCannotLiveInAnotherResidentFolder (D6 / C-M37·6) ──

    [Fact]
    public async Task C_M37_6_NoteCannotLiveInAnotherResidentFolder()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string ownerA = "u-c6c-ownerA";
        const string ownerB = "u-c6c-ownerB";

        // Owner A's folder.
        var folderA = await RunInSession(store, s => svc.CreateFolderAsync(ownerA, "A's folder", null, s));

        // Owner B's note, sitting at the root of B's tree.
        await Plant(store, new PersonalNote
        {
            Id = "c6c-noteB", OwnerId = ownerB, TargetKind = "post",
            TargetId = "c6c-p", Body = "b's body",
            FolderId = null, Created = DateTimeOffset.UtcNow,
        });

        // Owner B cannot move their note into owner A's folder (D6 / C-M37·6
        // — a note cannot live in another resident's folder). A non-leaky
        // 404 (KeyNotFoundException).
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => RunInSession(store, s => svc.MoveNoteToFolderAsync(ownerB, "c6c-noteB", folderA.Id, s)));

        // The note is unchanged — still at the root of B's tree, body intact.
        var after = (await Notes(store, ownerB)).First(n => n.Id == "c6c-noteB");
        Assert.Null(after.FolderId);
        Assert.Equal("b's body", after.Body);

        // Owner A's folder is intact — no leak, no cross-owner mutation.
        var aFolders = await Folders(store, ownerA);
        Assert.Contains(aFolders, f => f.Id == folderA.Id);
    }

    // ── Announcement lane (ADR 0167 D1 extension, M17 obs-4 shape) ─────

    [Fact]
    public async Task F2_AttachAnnouncement_VisiblePublicTarget_Created()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-ann-owner";
        const string annId = "ann-visible-public";

        // ADR 0037 — IsDraft = false. Scope = Public, CommunityId = null
        // → visible to every viewer regardless of roles (the GetAsync path
        // the detail page uses).
        await Plant(store, new Announcement
        {
            Id = annId,
            AuthorId = "u-ann-author",
            Title = "Visible public announcement",
            Body = "body",
            Scope = AnnouncementScope.Public,
            CommunityId = null,
            IsDraft = false,
            LanguageCode = "en",
            Created = DateTimeOffset.UtcNow,
        });

        // D3 write-lane check passes: CreateNoteAsync returns the fresh row.
        var row = await RunInSession(store, s => svc.CreateNoteAsync(
            owner, "announcement", annId, "my note", s));
        Assert.Equal(annId, row.TargetId);
        Assert.Equal("/announcements/" + annId, row.Link);
        Assert.False(row.Degraded);

        // The owner's list resolves the row (title + link present, body intact).
        var list = await svc.ListNotesAsync(owner);
        var group = Assert.Single(list.Groups, g => g.Kind == "announcement");
        var noteRow = Assert.Single(group.Items);
        Assert.Equal(annId, noteRow.TargetId);
        Assert.Equal("my note", noteRow.Body);
    }

    [Fact]
    public async Task F2_AttachAnnouncement_Draft_NonAuthor_Refused()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-annb-owner";    // not the author
        const string annId = "ann-draft";

        // ADR 0037 — the draft announcement's author is NOT the owner. The
        // detail page's own GetAsync returns null for a non-author, so the
        // D3 write-lane check must refuse (KeyNotFoundException → 404).
        await Plant(store, new Announcement
        {
            Id = annId,
            AuthorId = "u-annb-author",
            Title = "Draft announcement",
            Body = "body",
            Scope = AnnouncementScope.Public,
            CommunityId = null,
            IsDraft = true,
            LanguageCode = "en",
            Created = DateTimeOffset.UtcNow,
        });

        // D3: the target's own read returned null ⇒ Refused (404), no row.
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => RunInSession(store, s => svc.CreateNoteAsync(
                owner, "announcement", annId, "body", s)));

        // No PersonalNote row survives.
        var rows = await Notes(store, owner);
        Assert.DoesNotContain(rows, n => n.TargetId == annId);
    }

    // ── Shared helpers ─────────────────────────────────────────────────────

    private async Task<IDocumentStore> BootStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            CoreDocTypes.Configure(opts);
            PostDocTypes.Configure(opts);
            PersonalNotesDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    private (UserInfoService User, AuthorizationService Authz, PersonalNoteService Svc)
        Services(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var events = new EventService(store, authz, userInfo);
        var projects = new ProjectService(store, authz, userInfo);
        // ADR 0167 D1 (extension) — the announcement lane (the M17
        // BookmarkService shape, ADR 0118 D3/F2 obs-4): real
        // AnnouncementService over the same store (PostDocTypes is
        // already configured by BootStoreAsync); NSubstitute for the
        // IIdentityService (the role set is empty for the test actors
        // — GetBySubjectAsync returns null for the synthetic subject
        // ids, so the announcement read degrades to the no-role path).
        var announcements = new AnnouncementService(store, userInfo);
        var identity = Substitute.For<IIdentityService>();
        identity.GetBySubjectAsync(Arg.Any<string>()).Returns((ThinPrincipal?)null);
        var svc = new PersonalNoteService(userInfo, authz, store, events, projects, announcements, identity);
        return (userInfo, authz, svc);
    }

    private static async Task PlantVisiblePost(IDocumentStore store, string postId, string author)
    {
        await Plant(store, new Component { Id = ComponentId, Name = "Test", Enabled = true });
        await Plant(store, new Post
        {
            Id = postId,
            ComponentId = ComponentId,
            AuthorId = author,
            Title = "Note test post",
            Body = "note test post",
            Created = DateTimeOffset.UtcNow,
            Audience = new Audience(), // empty — author branch passes
        });
    }

    private static Audience Audience(GrantKind kind, string id)
        => new(AudienceMode.Any, [new AudienceGrant(kind, id)]);

    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }

    private static async Task<T> RunInSession<T>(IDocumentStore store, Func<IDocumentSession, Task<T>> action)
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var result = await action(session);
        await session.SaveChangesAsync();
        return result;
    }

    private static async Task<IReadOnlyList<PersonalNote>> Notes(IDocumentStore store, string owner)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await Marten.QueryableExtensions.ToListAsync(
            s.Query<PersonalNote>().Where(n => n.OwnerId == owner), ct);
    }

    private static async Task<IReadOnlyList<NoteFolder>> Folders(IDocumentStore store, string owner)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await Marten.QueryableExtensions.ToListAsync(
            s.Query<NoteFolder>().Where(f => f.OwnerId == owner), ct);
    }

    private static async Task<IReadOnlyList<AccessAudit>> AllAudits(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await Marten.QueryableExtensions.ToListAsync(s.Query<AccessAudit>(), ct);
    }
}
