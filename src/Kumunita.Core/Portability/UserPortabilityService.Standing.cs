using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kumunita.Core.Identity;
using Kumunita.Core.Media;
using Kumunita.Core.Usage;
using Kumunita.Core.UserInfo;

namespace Kumunita.Core.Portability;

/// <summary>
/// The <c>/portability</c> standing-check helpers — the
/// per-module standing checks (group/tag/component/page)
/// that the import-lane uses to verify the actor may
/// resolve into that module (ADR 0006 module-boundary,
/// the portability import standing gate). A partial class of
/// <see cref="UserPortabilityService"/> extracted so the main
/// file stays under the 600 <c>cs_file</c> ceiling (C3-04).
/// </summary>
public partial class UserPortabilityService
{
    // ── U06 (resolve) helpers — the resident standing check ─────────────

    /// <summary>
    /// The resident standing check (the §2.5 (3) fail-closed rejection —
    /// the resident must have standing over the <c>PickedTargetId</c>: a
    /// group they are a member of (or own), a tag they created, a
    /// component they are a member of, a page they authored). A business
    /// standing read, not an <c>AccessAction</c> (C-M27·7 — the frozen
    /// <c>IAuthorizationService</c> surface is unchanged).
    /// </summary>
    private async Task<bool> HasStandingAsync(
        Marten.IQuerySession session,
        string? targetKind, string targetId, string residentSubjectId,
        CancellationToken ct)
    {
        if (targetKind is null)
            return false;

        return targetKind switch
        {
            "Group" => await StandingGroupAsync(session, targetId, residentSubjectId, ct).ConfigureAwait(false),
            "Tag" => await StandingTagAsync(session, targetId, residentSubjectId, ct).ConfigureAwait(false),
            "Component" => await StandingComponentAsync(session, targetId, residentSubjectId, ct).ConfigureAwait(false),
            "Page" => await StandingPageAsync(session, targetId, residentSubjectId, ct).ConfigureAwait(false),
            _ => false, // an unknown target kind is a fail-closed rejection
        };
    }

    /// <summary>
    /// Group standing: the resident is a member (a <c>GroupMembership</c>
    /// row with <c>UserId</c> = the resident + <c>GroupId</c> = the target)
    /// or the owner (the <c>Group.OwnerId</c> = the resident).
    /// </summary>
    private static async Task<bool> StandingGroupAsync(
        Marten.IQuerySession session, string groupId, string residentSubjectId, CancellationToken ct)
    {
        // The GroupMembership row (the resident is a member).
        var memberships = await QueryAllRowsAsync(session, typeof(Kumunita.Core.UserInfo.GroupMembership), ct);
        foreach (var m in memberships)
        {
            var t = m.GetType();
            var gid = (string?)t.GetProperty("GroupId", BindingFlags.Public | BindingFlags.Instance)?.GetValue(m);
            var uid = (string?)t.GetProperty("UserId", BindingFlags.Public | BindingFlags.Instance)?.GetValue(m);
            if (gid == groupId && uid == residentSubjectId)
                return true;
        }
        // The Group.OwnerId (the resident is the owner).
        var groups = await QueryAllRowsAsync(session, typeof(Kumunita.Core.UserInfo.Group), ct);
        foreach (var g in groups)
        {
            var t = g.GetType();
            var gid = (string?)t.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)?.GetValue(g);
            var oid = (string?)t.GetProperty("OwnerId", BindingFlags.Public | BindingFlags.Instance)?.GetValue(g);
            if (gid == groupId && oid == residentSubjectId)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Tag standing: the resident created the tag (the <c>Tag.CreatedBy</c>
    /// = the resident — the ADR 0044 translate-standing owner).
    /// </summary>
    private static async Task<bool> StandingTagAsync(
        Marten.IQuerySession session, string tagId, string residentSubjectId, CancellationToken ct)
    {
        var tags = await QueryAllRowsAsync(session, typeof(Kumunita.Core.Tags.Tag), ct);
        foreach (var tag in tags)
        {
            var t = tag.GetType();
            var tid = (string?)t.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)?.GetValue(tag);
            var cb = (string?)t.GetProperty("CreatedBy", BindingFlags.Public | BindingFlags.Instance)?.GetValue(tag);
            if (tid == tagId && cb == residentSubjectId)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Component standing: the resident is a member (a
    /// <c>ComponentMembership</c> row with <c>UserId</c> = the resident +
    /// <c>ComponentId</c> = the target) or the component is mandatory
    /// (the ADR 0012 implicit membership).
    /// </summary>
    private static async Task<bool> StandingComponentAsync(
        Marten.IQuerySession session, string componentId, string residentSubjectId, CancellationToken ct)
    {
        // The ComponentMembership row (the resident is a member).
        var memberships = await QueryAllRowsAsync(session, typeof(Kumunita.Core.UserInfo.ComponentMembership), ct);
        foreach (var m in memberships)
        {
            var t = m.GetType();
            var cid = (string?)t.GetProperty("ComponentId", BindingFlags.Public | BindingFlags.Instance)?.GetValue(m);
            var uid = (string?)t.GetProperty("UserId", BindingFlags.Public | BindingFlags.Instance)?.GetValue(m);
            if (cid == componentId && uid == residentSubjectId)
                return true;
        }
        // The Component.Mandatory flag (the ADR 0012 implicit membership).
        var components = await QueryAllRowsAsync(session, typeof(Kumunita.Core.UserInfo.Component), ct);
        foreach (var c in components)
        {
            var t = c.GetType();
            var cid = (string?)t.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)?.GetValue(c);
            var mandatory = (bool)(t.GetProperty("Mandatory", BindingFlags.Public | BindingFlags.Instance)?.GetValue(c) ?? false);
            var enabled = (bool)(t.GetProperty("Enabled", BindingFlags.Public | BindingFlags.Instance)?.GetValue(c) ?? true);
            if (cid == componentId && mandatory && enabled)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Page standing: the resident authored the page (the
    /// <c>Page.AuthorId</c> = the resident).
    /// </summary>
    private static async Task<bool> StandingPageAsync(
        Marten.IQuerySession session, string pageId, string residentSubjectId, CancellationToken ct)
    {
        var pages = await QueryAllRowsAsync(session, typeof(Kumunita.Core.Pages.Page), ct);
        foreach (var p in pages)
        {
            var t = p.GetType();
            var pid = (string?)t.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)?.GetValue(p);
            var aid = (string?)t.GetProperty("AuthorId", BindingFlags.Public | BindingFlags.Instance)?.GetValue(p);
            if (pid == pageId && aid == residentSubjectId)
                return true;
        }
        return false;
    }
}
