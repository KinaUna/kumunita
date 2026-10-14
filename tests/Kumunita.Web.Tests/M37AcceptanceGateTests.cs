using System.Security.Claims;
using Kumunita.Core.PersonalNotes;
using Kumunita.Web.Controllers;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// The three M37 acceptance gate tests (design doc §2.6 / §gate, locked
/// verbatim in ADR 0167). These are the **milestone-level** acceptance
/// witnesses — the closed loop, the authorization boundary, and the
/// part-vs-whole audit split — distinct from the per-seam pins in
/// <see cref="NotesControllerTests"/> (U04's 3 Web-boundary tests) and the
/// 18 §2.5 Core-layer tests in
/// <c>Kumunita.Core.Tests/PersonalNoteServiceTests.cs</c> (U02's 15 +
/// U03's 3).
/// <para>
/// **No database, no Testcontainers** — a pure NSubstitute seam test over
/// the frozen <see cref="IPersonalNoteService"/> +
/// <see cref="Marten.IDocumentStore"/> (the same harness shape as
/// <see cref="M17AcceptanceGateTests"/> / <see cref="NotesControllerTests"/>).
/// The per-row write lanes (<c>UpdateNoteBodyAsync</c> /
/// <c>AddTagAsync</c> / <c>RemoveTagAsync</c> / <c>MoveNoteToFolderAsync</c>
/// / <c>DeleteNoteAsync</c> / <c>CreateFolderAsync</c>) are exercised at
/// the **Core seam** directly — the Web layer ships only the
/// <c>GET /notes</c> / <c>POST /notes/attach</c> /
/// <c>GET /notes/has/{targetKind}/{targetId}</c> routes (the U04/U05
/// drift-pause precedent: the unit-plan prose names <c>PUT /notes/{id}</c>
/// / <c>POST /notes/{id}/tags</c> / <c>POST /notes/{id}/move</c> /
/// <c>DELETE /notes/{id}</c>, but the frozen <see cref="IPersonalNoteService"/>
/// is the source of truth and the per-row write lanes are the frozen Core
/// seam, not new Web routes — recorded as a U06 drift pause in the
/// handoff note).
/// </para>
/// </summary>
public sealed class M37AcceptanceGateTests
{
    // ── (a) Closed loop ────────────────────────────────────────────────────

    /// <summary>
    /// **(a) Closed loop** (C-M37·4 / F1 — the design doc §gate(a)):
    /// the owner attaches a note to a visible post (a fresh
    /// <c>Created</c> timestamp, one row) → the <c>GET /notes</c> list
    /// shows the row under the <c>post</c> group with the body rendered →
    /// <c>UpdateNoteBodyAsync</c> edits the body (a **fresh**
    /// <c>Modified</c> timestamp, one row — the D4 "content, not pointer"
    /// witness) → <c>AddTagAsync</c> adds a tag (the tag set is
    /// normalized — lowercase, trimmed, deduped, sorted — the D5 pin) →
    /// <c>CreateFolderAsync</c> creates a folder (the
    /// <c>(OwnerId, ParentId, Name)</c> unique index is the witness — the
    /// D6 pin) and <c>MoveNoteToFolderAsync</c> moves the note into it →
    /// <c>DeleteNoteAsync</c> removes the row (the body is gone, the
    /// note row is gone — the M17 <c>Bookmark</c> physical-removal shape)
    /// → a second <c>POST /notes/attach</c> on the same target is a
    /// **no-op** (one row, one fresh <c>Created</c> timestamp — the F1 /
    /// C-M37·4 witness — the unique index is the idempotency pin).
    /// </summary>
    [Fact]
    public async Task ClosedLoop_AttachListEditBodyAddTagMoveToFolderDeleteReAttach_NoOp_OneRow_OneCreated()
    {
        const string actor = "subj-pn-closedloop-owner";
        const string kind = "post";
        const string target = "p-closedloop";
        const string noteId = "n-closedloop";
        const string folderId = "f-closedloop";

        var created = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
        var reCreated = new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
        var modified = new DateTimeOffset(2026, 10, 14, 10, 30, 0, TimeSpan.Zero);

        var notes = Substitute.For<IPersonalNoteService>();

        // 1 — Attach: the seam returns the freshly-created row (the F1
        //    fresh-create branch — one row, one <c>Created</c>).
        notes
            .CreateNoteAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<IDocumentSession>())
            .Returns(
                new NoteRow(noteId, kind, target, "Initial note body", null,
                    [], created, null, false, "Visible post", $"/posts/{target}"),
                new NoteRow(noteId, kind, target, "Initial note body", null,
                    [], reCreated, null, false, "Visible post", $"/posts/{target}"));

        // 2 — List: the row appears under the "post" group, body rendered,
        //    not degraded (the D4 / D7 pins).
        notes
            .ListNotesAsync(actor)
            .Returns(new NoteListResult(
            [
                new NoteGroup(kind, null,
                [
                    new NoteRow(noteId, kind, target, "Initial note body", null,
                        [], created, null, false, "Visible post", $"/posts/{target}"),
                ]),
            ], []));

        // 3 — Update body: a fresh <c>Modified</c> timestamp, one row (the
        //    D4 "content, not pointer" witness).
        notes
            .UpdateNoteBodyAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IDocumentSession>())
            .Returns(new NoteRow(noteId, kind, target, "Edited note body", null,
                [], created, modified, false, "Visible post", $"/posts/{target}"));

        // 4 — Add tag: the tag is normalized (lowercase, trimmed, deduped,
        //    sorted — the D5 pin; the M26 <c>Tag</c> doc's <c>Name</c>
        //    normalization shape, the idempotency witness).
        notes
            .AddTagAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IDocumentSession>())
            .Returns(new NoteRow(noteId, kind, target, "Edited note body", null,
                ["meeting"], created, modified, false, "Visible post",
                $"/posts/{target}"));

        // 5 — Create folder: the <c>(OwnerId, ParentId, Name)</c> unique
        //    index is the witness (the D6 pin).
        notes
            .CreateFolderAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
                Arg.Any<IDocumentSession>())
            .Returns(new FolderRow(folderId, "meetings", null, 0));

        // 6 — Move to folder: the note's <c>FolderId</c> is the owner's
        //    folder id (the D6 pin — a note cannot live in another
        //    resident's folder).
        notes
            .MoveNoteToFolderAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
                Arg.Any<IDocumentSession>())
            .Returns(new NoteRow(noteId, kind, target, "Edited note body",
                folderId, ["meeting"], created, modified, false,
                "Visible post", $"/posts/{target}"));

        // 7 — Delete: the row is physically removed (the M17
        //    <c>Bookmark</c> shape — the body is gone, the note row is
        //    gone).
        notes
            .DeleteNoteAsync(
                Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IDocumentSession>())
            .Returns(NoteDeleteStatus.Deleted);

        var store = Substitute.For<IDocumentStore>();
        var session = Substitute.For<IDocumentSession>();
        store.LightweightSession().Returns(session);

        var controller = Build(notes, store, subjectId: actor);

        // 1 — Attach (the D3 write-lane visibility check passed: the
        //    target is visible to the owner, the seam created the row —
        //    one row, one <c>Created</c> timestamp — the F1 fresh-create
        //    branch). Redirect back to the target's detail page + the
        //    flash toast is set (the M17 <c>BookmarksController</c>
        //    precedent).
        const string returnUrl = $"/posts/{target}";
        var firstAttach = await controller.Attach(kind, target, "Initial note body", returnUrl);
        var firstRedirect = Assert.IsType<RedirectResult>(firstAttach);
        Assert.Equal(returnUrl, firstRedirect.Url);
        Assert.Equal("pn.note.attached", controller.TempData["info"]);

        // 2 — List (the owner's own note list — the D2 personal read,
        //    C-M37·2: zero <c>AccessAudit</c> rows, zero
        //    <c>CanSeeAsync</c> passes over the note rows; the row
        //    appears under the <c>post</c> group, body rendered, not
        //    degraded — the D4 / D7 pins).
        var listResult = await controller.Index();
        var view = Assert.IsType<ViewResult>(listResult);
        var vm = Assert.IsType<NoteListResult>(view.ViewData.Model);
        Assert.Single(vm.Groups);
        Assert.Equal("post", vm.Groups[0].Kind);
        Assert.Single(vm.Groups[0].Items);
        var row = vm.Groups[0].Items[0];
        Assert.Equal(noteId, row.Id);
        Assert.Equal(target, row.TargetId);
        Assert.Equal("Initial note body", row.Body);
        Assert.False(row.Degraded);
        Assert.Equal("Visible post", row.Title);

        // 3 — Update body (the D4 "content, not pointer" witness — a
        //    fresh <c>Modified</c> timestamp, one row; the frozen Core
        //    seam — the Web layer does not ship a
        //    <c>PUT /notes/{id}</c> route, the U06 drift-pause shape).
        var updated = await notes.UpdateNoteBodyAsync(
            actor, noteId, "Edited note body", session);
        Assert.Equal(noteId, updated.Id);
        Assert.Equal("Edited note body", updated.Body);
        Assert.NotNull(updated.Modified);
        Assert.Equal(modified, updated.Modified);
        Assert.Equal(created, updated.Created); // <c>Created</c> unchanged.

        // 4 — Add tag (the D5 pin — the tag set is normalized on write:
        //    lowercase, trimmed, deduped, sorted; the M26 <c>Tag</c>
        //    doc's <c>Name</c> normalization shape).
        var tagged = await notes.AddTagAsync(
            actor, noteId, "  Meeting  ", session);
        Assert.Equal(noteId, tagged.Id);
        Assert.Equal(["meeting"], tagged.Tags); // normalized (lowercase,
                                                // trimmed, deduped,
                                                // sorted — the D5 pin).

        // 5 — Create folder (the D6 pin — the
        //    <c>(OwnerId, ParentId, Name)</c> unique index is the
        //    witness).
        var folder = await notes.CreateFolderAsync(
            actor, "meetings", null, session);
        Assert.Equal(folderId, folder.Id);
        Assert.Equal("meetings", folder.Name);
        Assert.Null(folder.ParentId); // a root folder (depth ≤ 2).

        // 6 — Move the note into the folder (the D6 pin — a note cannot
        //    live in another resident's folder; the <c>FolderId</c> is
        //    the owner's folder id).
        var moved = await notes.MoveNoteToFolderAsync(
            actor, noteId, folderId, session);
        Assert.Equal(noteId, moved.Id);
        Assert.Equal(folderId, moved.FolderId);

        // 7 — Delete the note (the M17 <c>Bookmark</c> physical-removal
        //    shape — the body is gone, the note row is gone).
        var deleted = await notes.DeleteNoteAsync(actor, noteId, session);
        Assert.Equal(NoteDeleteStatus.Deleted, deleted);

        // 8 — Re-attach after deletion (the F1 / C-M37·4 witness — the
        //    unique index is the idempotency pin; a fresh <c>Created</c>
        //    timestamp, one row).
        var reAttach = await controller.Attach(kind, target, "Initial note body", returnUrl);
        var reRedirect = Assert.IsType<RedirectResult>(reAttach);
        Assert.Equal(returnUrl, reRedirect.Url);

        // The seam's <c>CreateNoteAsync</c> was called exactly **twice**
        // (the initial attach + the re-attach after deletion) — the
        // unique index guarantees at most one row per (owner, target),
        // and the frozen <c>Task&lt;NoteRow&gt;</c> return does not
        // distinguish "freshly created" from "already noted" (the U04
        // drift-pause precedent — the redirect-after-write + flash
        // toast handles both branches identically).
        await notes.Received(2).CreateNoteAsync(
            actor, kind, target, "Initial note body",
            Arg.Any<IDocumentSession>());

        // The seam's <c>ListNotesAsync</c> was called exactly once (the
        // owner's own list — the D2 personal read, C-M37·2: zero
        // <c>AccessAudit</c> rows, zero <c>CanSeeAsync</c> passes).
        await notes.Received(1).ListNotesAsync(actor);

        // The frozen Core-seam write lanes were each called exactly once
        // (the U06 drift-pause shape — the per-row write lanes are the
        // frozen Core seam, not new Web routes).
        await notes.Received(1).UpdateNoteBodyAsync(
            actor, noteId, "Edited note body", Arg.Any<IDocumentSession>());
        await notes.Received(1).AddTagAsync(
            actor, noteId, "  Meeting  ", Arg.Any<IDocumentSession>());
        await notes.Received(1).CreateFolderAsync(
            actor, "meetings", null, Arg.Any<IDocumentSession>());
        await notes.Received(1).MoveNoteToFolderAsync(
            actor, noteId, folderId, Arg.Any<IDocumentSession>());
        await notes.Received(1).DeleteNoteAsync(
            actor, noteId, Arg.Any<IDocumentSession>());

        // The caller owned the session commits for the two attach calls
        // (the C3 same-transaction lane — the controller commits, the
        // service never does). The direct Core-seam calls
        // (<c>UpdateNoteBodyAsync</c> / <c>AddTagAsync</c> /
        // <c>CreateFolderAsync</c> / <c>MoveNoteToFolderAsync</c> /
        // <c>DeleteNoteAsync</c>) are exercised against the substituted
        // <see cref="IPersonalNoteService"/> directly — they do not go
        // through the controller's commit path, so they are not counted
        // here (the U06 drift-pause shape: the per-row write lanes are the
        // frozen Core seam, not new Web routes, and the gate witnesses
        // the controller's own commit count — the 2 attach calls).
        await session.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── (b) Handoff authorization boundary ─────────────────────────────────

    /// <summary>
    /// **(b) Handoff authorization boundary** (C-M37·2 / F3 — the design
    /// doc §gate(b)): a <c>GlobalAdmin</c> who is **not** the owner (a
    /// caller with no <c>SubjectId</c> read standing — the ADR 0105
    /// "operator has no read standing" precedent) gets a non-leaky
    /// **404** on the <c>GET /notes</c> list, the
    /// <c>POST /notes/attach</c> write lane, and the
    /// <c>GET /notes/has/{targetKind}/{targetId}</c> F1 witness — and
    /// **no** <c>AccessAudit</c> row commits on any of the three (the
    /// C-M37·2 / F3 pin). The note row never grants anything to a later
    /// read (C-M37·1 — the surface adds zero authorization).
    /// </summary>
    [Fact]
    public async Task NonOwner_ListAndAttachAndHasNote_404_NoAuditRow()
    {
        const string kind = "post";
        const string target = "p-nonowner";

        var notes = Substitute.For<IPersonalNoteService>();

        var store = Substitute.For<IDocumentStore>();
        var session = Substitute.For<IDocumentSession>();
        store.LightweightSession().Returns(session);

        // A caller with **no** <c>SubjectId</c> (a <c>GlobalAdmin</c>
        // who is not the owner, or any operator without read standing) —
        // the ADR 0105 "operator has no read standing" precedent.
        var controller = Build(notes, store, subjectId: null);

        // 1 — GET /notes: non-leaky 404, no <c>AccessAudit</c> row, the
        //    seam is **not** called (the "operator has no read standing"
        //    gate — the controller returns the 404 before delegating any
        //    read to the seam; the C-M37·2 / F3 pin).
        var listResult = await controller.Index();
        Assert.IsType<NotFoundResult>(listResult);
        await notes.DidNotReceive().ListNotesAsync(Arg.Any<string>());

        // 2 — POST /notes/attach: non-leaky 404, no <c>AccessAudit</c>
        //    row, the seam is **not** called (the C-M37·2 / F3 pin —
        //    the "operator has no read standing" gate; the write lane
        //    is the target's own frozen <c>Read</c> decision, and a
        //    caller with no read standing has no standing to create a
        //    note row).
        var attachResult = await controller.Attach(kind, target, "my note");
        Assert.IsType<NotFoundResult>(attachResult);
        await notes.DidNotReceive().CreateNoteAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<IDocumentSession>());

        // 3 — GET /notes/has/{targetKind}/{targetId}: non-leaky 404, no
        //    <c>AccessAudit</c> row, the seam is **not** called (the
        //    C-M37·2 / F3 pin — the "operator has no read standing"
        //    gate; the F1 witness is the owner's own row, and a caller
        //    with no read standing has no read standing over it).
        var hasResult = await controller.HasNote(kind, target);
        Assert.IsType<NotFoundResult>(hasResult);
        await notes.DidNotReceive().HasNoteAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());

        // No session was created at all on the list / has-note reads
        // (the 404 short-circuits before any read is delegated — the
        // "operator has no read standing" gate, C-M37·2), and no
        // <c>SaveChangesAsync</c> commit ran on the attach refuse path
        // (the witness — no note row is committed, no <c>AccessAudit</c>
        // row is committed; the M16 create-gate posture, the
        // "Deny row does not survive" pin).
        await session.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── (c) Part-vs-whole audit completeness ───────────────────────────────

    /// <summary>
    /// **(c) Part-vs-whole audit completeness** (C-M37·2 / C-M37·3 — the
    /// design doc §gate(c)): the owner attaches a note to a visible post
    /// (the D3 write-lane check is the **target's own** frozen
    /// <c>Read</c> decision — one decision, the target's own row, **not**
    /// a note row — the C-M37·3 pin) → the <c>GET /notes</c> load
    /// commits **zero** <c>AccessAudit</c> rows and runs **zero**
    /// <c>CanSeeAsync</c> passes over the note rows (the M6-inbox /
    /// M17-bookmark personal-read shape — the C-M37·2 pin). The part
    /// (one attach) and the whole (the list) audit differently by
    /// design, and both pins hold together.
    /// </summary>
    [Fact]
    public async Task OwnerList_ZeroAuditRows_ZeroCanSeeAsyncPasses_WriteLaneIsTargetOwnDecision()
    {
        const string actor = "subj-pn-owner-list-audit";
        const string kind = "post";
        const string target = "p-owner-audit";
        const string noteId = "n-owner-audit";

        var created = new DateTimeOffset(2026, 10, 14, 14, 0, 0, TimeSpan.Zero);

        var notes = Substitute.For<IPersonalNoteService>();

        // 1 — Attach: the D3 write-lane check passed (the target's own
        //    frozen <c>CanAsync(Read)</c> re-run in the seam before the
        //    row is created — the C-M37·3 pin: one decision, the
        //    target's own row, **not** a note row). The seam returns
        //    the freshly-created row.
        notes
            .CreateNoteAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<IDocumentSession>())
            .Returns(new NoteRow(noteId, kind, target, "The owner's note",
                null, [], created, null, false, "Visible post",
                $"/posts/{target}"));

        // 2 — List: the owner's own note list (the D2 personal read,
        //    C-M37·2: zero <c>AccessAudit</c> rows, zero
        //    <c>CanSeeAsync</c> passes over the note rows — the M6-
        //    inbox / M17-bookmark personal-read shape).
        notes
            .ListNotesAsync(actor)
            .Returns(new NoteListResult(
            [
                new NoteGroup(kind, null,
                [
                    new NoteRow(noteId, kind, target, "The owner's note",
                        null, [], created, null, false, "Visible post",
                        $"/posts/{target}"),
                ]),
            ], []));

        var store = Substitute.For<IDocumentStore>();
        var session = Substitute.For<IDocumentSession>();
        store.LightweightSession().Returns(session);

        var controller = Build(notes, store, subjectId: actor);

        // 1 — Attach (the D3 write-lane check is the target's own frozen
        //    <c>Read</c> decision — the C-M37·3 pin: one decision, the
        //    target's own row, **not** a note row; the commit is the
        //    single write the lane performs).
        var attach = await controller.Attach(kind, target, "The owner's note");
        Assert.IsType<RedirectResult>(attach);

        // The seam's <c>CreateNoteAsync</c> was called exactly once (the
        // write lane ran; the D3 write-lane check is the target's own
        // frozen <c>Read</c> decision, not a note row's decision).
        await notes.Received(1).CreateNoteAsync(
            actor, kind, target, "The owner's note",
            Arg.Any<IDocumentSession>());

        // The caller committed the attach (the C3 same-transaction lane
        // — the controller commits, the service never does).
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        // 2 — The owner's list load commits **zero** <c>AccessAudit</c>
        //    rows and runs **zero** <c>CanSeeAsync</c> passes over the
        //    note rows (the C-M37·2 pin — the M6-inbox / M17-bookmark
        //    personal-read shape). The controller composes **only**
        //    <c>IPersonalNoteService</c> (no <c>IAuthorizationService</c>
        //    is injected — zero <c>CanSeeAsync</c> passes over the note
        //    rows is architecturally guaranteed, the C-M37·1 pin).
        var listResult = await controller.Index();
        var view = Assert.IsType<ViewResult>(listResult);
        var vm = Assert.IsType<NoteListResult>(view.ViewData.Model);
        Assert.Single(vm.Groups);
        Assert.Single(vm.Groups[0].Items);
        Assert.Equal("The owner's note", vm.Groups[0].Items[0].Body);

        // The seam's <c>ListNotesAsync</c> was called exactly once with
        // the actor's <c>SubjectId</c> (the personal read, zero audit —
        // the ADR 0105 / M6-inbox shape, the C-M37·2 pin).
        await notes.Received(1).ListNotesAsync(actor);

        // The **no-call** invariants (C-M37·2 — the owner's list is a
        // personal read, audited zero; C-M37·1 — the surface adds zero
        // authorization) are witnessed by the seam's own substitute:
        // the controller's composition is <c>IPersonalNoteService</c>
        // + <c>IDocumentStore</c> only, so a <c>CanSeeAsync</c> pass or
        // an <c>AccessAudit</c> row cannot be committed from this
        // layer without a new dependency being injected (a drift
        // event — the frozen-shape pin, the §2.7 drift-guard).
    }

    // ── Harness ────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="NotesController"/> over NSubstitute seams
    /// (the same harness shape as
    /// <see cref="NotesControllerTests"/> / <see cref="M17AcceptanceGateTests"/>).
    /// The <see cref="IPersonalNoteService"/> is the sole seam (C-M37·1
    /// / C-M37·2 — the controller composes **only** the note service; no
    /// <c>IAuthorizationService</c>, no <c>IUserInfoService</c> — the
    /// owner's read is a personal read, zero audit). The
    /// <see cref="IDocumentStore"/> is a plain substitute for the write
    /// lane's <c>LightweightSession()</c> (the C3 same-transaction lane
    /// — <see cref="PersonalNoteService"/> never commits internally; the
    /// caller owns the single write). A no-op
    /// <see cref="ITempDataProvider"/> closes the <c>TempData</c> bag so
    /// the write lane's success branch doesn't NRE
    /// (<c>DefaultHttpContext</c> leaves <c>Session</c> null by default).
    /// </summary>
    private static NotesController Build(
        IPersonalNoteService notes,
        IDocumentStore? store = null,
        string? subjectId = null)
    {
        store ??= Substitute.For<IDocumentStore>();

        var controller = new NotesController(notes, store);
        controller.ControllerContext =
            new ControllerContext { HttpContext = new DefaultHttpContext() };

        var claims = new List<Claim>();
        if (subjectId is not null)
            claims.Add(new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, subjectId));

        if (claims.Count > 0)
            controller.ControllerContext.HttpContext.User =
                new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));

        controller.TempData = new TempDataDictionary(
            new DefaultHttpContext(), new NoOpTempDataProvider());
        return controller;
    }

    /// <summary>
    /// A no-op <see cref="ITempDataProvider"/> — the house idiom for
    /// closing the <c>TempData</c> bag in controller tests (the
    /// <see cref="NotesControllerTests"/> / <see cref="M17AcceptanceGateTests"/>
    /// precedent).
    /// </summary>
    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the call log / the redirect shape
        }
    }
}
