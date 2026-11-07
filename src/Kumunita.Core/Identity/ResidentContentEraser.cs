using Marten;

namespace Kumunita.Core.Identity;

/// <summary>
/// C10 (IMPROVE-CODE) — the cross-module <b>content-doc erasure</b> of
/// <see cref="IdentityService"/>, extracted along the F1 (DB-13 GDPR) §2A
/// "residential content" closed-list seam the method already comments: the one
/// place the §2A set (posts, announcements, events, projects, pages, inventory,
/// documents, bookmarks, tags, personal notes, and the
/// <c>MediaObject</c> catalog) is queried-by-owner and deleted in a single
/// Marten session, and the resident's <b>messaging threads are
/// pseudonymized</b> (D3, IS21) rather than deleted — the 1:1 / group
/// <c>Conversation</c> + <c>Message</c> + <c>GroupConversation</c> +
/// <c>GroupMessage</c> + <c>GroupMessagingInvitation</c> set routes through
/// <see cref="ResidentThreadPseudonymizer"/> (the resident's seats and sent
/// messages are tombstoned, the rows stay for the other participants).
/// <para>
/// <b>Behaviour:</b> the same §2A doc set (only the resident's
/// own rows, owner field == <paramref name="residentSubjectId"/>), the same
/// <see cref="Kumunita.Core.Projects.KanbanBoard"/> cascade (placements + lanes
/// first, then the board — the <c>ProjectService.DeleteBoardAsync</c> shape),
/// and the same <c>MediaObject</c> catalog read. The one deliberate exception
/// is the messaging set — D3 (IMPROVE-SEC IS21) changed it from *delete the
/// conversation* to *pseudonymize the thread* (see
/// <see cref="ResidentThreadPseudonymizer"/>). Only the <b>byte</b> delete
/// is the composer's (it runs after the commit, the §5 ordering pin).
/// </para>
/// <para>
/// Runs on the caller's in-flight <paramref name="session"/> (C3 — the
/// composer's single <c>SaveChangesAsync</c> commits it; this method does not
/// commit). Returns the resident content-doc count (the log summary's
/// <c>{ContentDocs}</c>) + the resident's <c>MediaObject</c> catalog ids
/// (the post-commit byte-delete loop's input, the §4 catalog pin).
/// </para>
/// <para>
/// <b>Part-vs-whole:</b> a public seam with two callers —
/// <see cref="IdentityService"/>.DeleteCoreAsync (the composer) and the
/// <c>IdentityServiceDeleteLaneGdprTests</c> erasure test (the direct proof of
/// full erasure across the module set). It is the §2A closed list as data,
/// not a second copy of the delete.
/// </para>
/// </summary>
public static class ResidentContentEraser
{
    /// <summary>
    /// Deletes the resident's §2A residential content docs (and the
    /// <c>MediaObject</c> catalog rows) in the caller's <paramref
    /// name="session"/> (C3 — no commit here). See the class doc for the
    /// unchanged-behaviour + part-vs-whole contract (F1 / DB-13 GDPR, §2A / §4
    /// / §5 of docs/design/prod-hardening-f1-gdpr-deletion.md).
    /// </summary>
    public static async Task<(int ContentDocs, List<string> MediaIds)> EraseAsync(
        IDocumentSession session, string residentSubjectId, CancellationToken ct)
    {
        int contentDocs = 0;
        async Task<int> DeleteResidentRows<T>(
            System.Linq.Expressions.Expression<System.Func<T, bool>> predicate)
            where T : notnull
        {
            var rows = await session.Query<T>().Where(predicate)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var row in rows)
                session.Delete(row);
            return rows.Count;
        }

        // §2A primary set — one query per doc type, owner field == resident.
        contentDocs += await DeleteResidentRows<Kumunita.Core.Posts.Post>(p => p.AuthorId == residentSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Posts.PostReply>(p => p.AuthorId == residentSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Posts.PostTranslation>(p => p.AuthorId == residentSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Posts.ReplyTranslation>(p => p.AuthorId == residentSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Announcements.Announcement>(a => a.AuthorId == residentSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Announcements.AnnouncementTranslation>(a => a.AuthorId == residentSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Announcements.AnnouncementComment>(a => a.AuthorId == residentSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Events.Event>(e => e.AuthorId == residentSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Events.EventTranslation>(e => e.AuthorId == residentSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Events.EventRsvp>(r => r.UserId == residentSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Projects.Project>(p => p.AuthorId == residentSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Projects.ProjectGoal>(p => p.AuthorId == residentSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Projects.TodoItem>(t => t.AuthorId == residentSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Projects.TodoComment>(t => t.AuthorId == residentSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Projects.TodoTranslation>(t => t.AuthorId == residentSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Projects.BoardTranslation>(b => b.AuthorId == residentSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Projects.ProjectTranslation>(p => p.AuthorId == residentSubjectId);
        // KanbanBoard + its owner-field-less sub-entities (cascade — the
        // DeleteBoardAsync shape: the board's placements + lanes go first,
        // then the board itself). The to-dos themselves are deleted above by
        // their own AuthorId (a to-do is a standalone row, not a board child).
        {
            var boardIds = await session.Query<Kumunita.Core.Projects.KanbanBoard>()
                .Where(b => b.AuthorId == residentSubjectId)
                .Select(b => b.Id)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var boardId in boardIds)
            {
                var placements = await session.Query<Kumunita.Core.Projects.BoardItemPlacement>()
                    .Where(p => p.BoardId == boardId)
                    .ToListAsync(ct).ConfigureAwait(false);
                foreach (var placement in placements)
                    session.Delete(placement);

                var lanes = await session.Query<Kumunita.Core.Projects.KanbanLane>()
                    .Where(l => l.BoardId == boardId)
                    .ToListAsync(ct).ConfigureAwait(false);
                foreach (var lane in lanes)
                    session.Delete(lane);
            }
            contentDocs += await DeleteResidentRows<Kumunita.Core.Projects.KanbanBoard>(b => b.AuthorId == residentSubjectId);
        }
        contentDocs += await DeleteResidentRows<Kumunita.Core.Pages.Page>(p => p.AuthorId == residentSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Pages.PageTranslation>(p => p.AuthorId == residentSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Inventory.InventoryItem>(i => i.AuthorId == residentSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Documents.Document>(d => d.OwnerId == residentSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Documents.DocumentFolder>(d => d.OwnerId == residentSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Bookmarks.Bookmark>(b => b.OwnerId == residentSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Bookmarks.BookmarkFolder>(b => b.OwnerId == residentSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Tags.Tag>(t => t.CreatedBy == residentSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Tags.TagTranslation>(t => t.AuthorId == residentSubjectId);
        // Conversation / Message / group threads — D3 (IMPROVE-SEC IS21):
        // pseudonymize, don't delete. The F1-era *delete* of the resident's
        // 1:1 Conversation + Message rows (which also destroyed the other
        // party's copy) is replaced by the D3 rule in
        // <see cref="ResidentThreadPseudonymizer"/>: the resident's message
        // bodies are erased and their sender becomes the tombstone, their
        // conversation seat is tombstoned, they leave the group thread, and
        // their pending invites are dropped — the rows STAY so the other
        // participants' threads remain coherent. The group types are the
        // pseudonymizer's alone (one fact lives in one place; IS24's
        // HandledTypes gate depends on this split).
        contentDocs += await ResidentThreadPseudonymizer.PseudonymizeAsync(
            session, residentSubjectId, null, ct).ConfigureAwait(false);
        contentDocs += await DeleteResidentRows<Kumunita.Core.PersonalNotes.PersonalNote>(n => n.OwnerId == residentSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.PersonalNotes.NoteFolder>(n => n.OwnerId == residentSubjectId);

        // F1.U4 (DB-13 GDPR) — the resident's MediaObject catalog rows, in this
        // SAME session / SAME commit as the content-doc deletes above (C3
        // single-write-lane, ADR 0006; the §4 catalog pin in
        // docs/design/prod-hardening-f1-gdpr-deletion.md). The owner field is
        // <c>CreatedById</c> (the resident who first stored the unique file).
        // We collect each row's <c>Id</c> (the lowercase-hex SHA-256 content id
        // — the exact byte-store key for F1.U2's DeleteIfExistsAsync) into
        // <c>mediaIds</c> so the composer's post-commit byte-delete loop can
        // run; the catalog rows themselves are session-deleted here and
        // committed by the composer's single SaveChangesAsync. Orphan policy
        // (pinned §4, owner-accepted): a dedup'd byte referenced by another
        // resident's doc is deleted anyway — the dangling ref degrades to a
        // 404 (C-MED·7), not a crash — so NO reference check is performed.
        var mediaRows = await session.Query<Kumunita.Core.Media.MediaObject>()
            .Where(o => o.CreatedById == residentSubjectId)
            .ToListAsync(ct).ConfigureAwait(false);
        var mediaIds = mediaRows.Select(o => o.Id).ToList();
        foreach (var mediaRow in mediaRows)
            session.Delete(mediaRow);
        contentDocs += mediaRows.Count;

        return (contentDocs, mediaIds);
    }
}
