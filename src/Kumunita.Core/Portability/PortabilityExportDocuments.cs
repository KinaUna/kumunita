using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Marten;

namespace Kumunita.Core.Portability;

/// <summary>
/// The U02 docs export loop (D7 / C-M11·4/5) — iterates the
/// <see cref="PortabilityDocTypes"/> registry (D7's closed inventory, data)
/// and writes one <c>docs/{Type}.json</c> (a JSON array of the POCO rows)
/// per entry + the <c>docCounts</c> (type name → row count) the manifest
/// carries.
/// <para>
/// The <see cref="NameToType"/> map is data (parallel to the registry's
/// string names) — the 44 name → <see cref="Type"/> resolutions are a
/// uniform table lookup, not 44 branches. The <c>Query&lt;T&gt;()</c> /
/// <c>ToListAsync()</c> / <c>JsonSerializer</c> calls are uniform
/// (reflection-dispatched on the resolved <see cref="Type"/>), so the loop
/// body is identical for every entry — the "generic JSON round-trip over
/// the POCOs, not per-type code" pin.
/// </para>
/// </summary>
public static class PortabilityExportDocuments
{
    /// <summary>
    /// The same JSON options as <see cref="KumunitaArchive.ToJson{T}"/>
    /// (case-insensitive property names + null suppression) — the
    /// <c>docs/{Type}.json</c> payloads round-trip identically through
    /// U05's <c>FromJson&lt;T&gt;</c>.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// <c>IQuerySession.Query&lt;T&gt;()</c> — the no-arg generic method the
    /// uniform loop dispatches per resolved <see cref="Type"/> (not per-type
    /// code).
    /// </summary>
    private static readonly MethodInfo QueryMethod =
        typeof(IQuerySession).GetMethod(nameof(IQuerySession.Query), Type.EmptyTypes)!;

    /// <summary>
    /// <c>Marten.QueryableExtensions.ToListAsync&lt;T&gt;(IQueryable&lt;T&gt;, CancellationToken)</c>
    /// — the <c>IQueryable&lt;T&gt;</c> → <c>List&lt;T&gt;</c> terminal (the same
    /// extension the in-repo services use via <c>session.Query&lt;T&gt;()
    /// .ToListAsync()</c>). Resolved once; dispatched via
    /// <see cref="MethodInfo.MakeGenericMethod"/> per resolved <see
    /// cref="Type"/> so the loop body stays uniform (not per-type code).
    /// </summary>
    private static readonly MethodInfo ToListAsyncMethod =
        typeof(Marten.QueryableExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == nameof(Marten.QueryableExtensions.ToListAsync)
                        && m.IsGenericMethod
                        && m.GetGenericArguments().Length == 1
                        && m.GetParameters().Length >= 1
                        && m.GetParameters()[0].ParameterType.IsGenericType
                        && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IQueryable<>));

    /// <summary>
    /// The 44 registry name → POCO <see cref="Type"/> map (data, parallel to
    /// the <see cref="PortabilityDocTypes.Entries"/> string names — every
    /// registry entry has exactly one <see cref="Type"/> here).
    /// </summary>
    private static readonly Dictionary<string, Type> NameToType = new(StringComparer.Ordinal)
    {
        ["Group"]                    = typeof(Kumunita.Core.UserInfo.Group),
        ["Component"]                = typeof(Kumunita.Core.UserInfo.Component),
        ["MediaObject"]              = typeof(Kumunita.Core.Media.MediaObject),
        ["Tag"]                      = typeof(Kumunita.Core.Tags.Tag),
        ["Profile"]                  = typeof(Kumunita.Core.UserInfo.Profile),
        ["DelegationGrant"]          = typeof(Kumunita.Core.UserInfo.DelegationGrant),
        ["GuardianLink"]             = typeof(Kumunita.Core.UserInfo.GuardianLink),
        ["GroupMembership"]          = typeof(Kumunita.Core.UserInfo.GroupMembership),
        ["GroupInvitation"]          = typeof(Kumunita.Core.UserInfo.GroupInvitation),
        ["GroupJoinRequest"]         = typeof(Kumunita.Core.UserInfo.GroupJoinRequest),
        ["ModeratorAssignment"]      = typeof(Kumunita.Core.UserInfo.ModeratorAssignment),
        ["ComponentMembership"]      = typeof(Kumunita.Core.UserInfo.ComponentMembership),
        ["TranslationResource"]      = typeof(Kumunita.Core.Localization.TranslationResource),
        ["GroupTranslation"]         = typeof(Kumunita.Core.UserInfo.GroupTranslation),
        ["CommunityTranslation"]     = typeof(Kumunita.Core.UserInfo.CommunityTranslation),
        ["TagTranslation"]           = typeof(Kumunita.Core.Tags.TagTranslation),
        ["Post"]                     = typeof(Kumunita.Core.Posts.Post),
        ["PostReply"]                = typeof(Kumunita.Core.Posts.PostReply),
        ["PostTranslation"]          = typeof(Kumunita.Core.Posts.PostTranslation),
        ["ReplyTranslation"]         = typeof(Kumunita.Core.Posts.ReplyTranslation),
        ["Report"]                   = typeof(Kumunita.Core.Posts.Report),
        ["Announcement"]             = typeof(Kumunita.Core.Announcements.Announcement),
        ["AnnouncementTranslation"]  = typeof(Kumunita.Core.Announcements.AnnouncementTranslation),
        ["AnnouncementComment"]      = typeof(Kumunita.Core.Announcements.AnnouncementComment),
        ["Event"]                    = typeof(Kumunita.Core.Events.Event),
        ["EventRsvp"]                = typeof(Kumunita.Core.Events.EventRsvp),
        ["EventTranslation"]         = typeof(Kumunita.Core.Events.EventTranslation),
        ["ProjectGoal"]              = typeof(Kumunita.Core.Projects.ProjectGoal),
        ["Project"]                  = typeof(Kumunita.Core.Projects.Project),
        ["TodoItem"]                 = typeof(Kumunita.Core.Projects.TodoItem),
        ["KanbanBoard"]              = typeof(Kumunita.Core.Projects.KanbanBoard),
        ["KanbanLane"]               = typeof(Kumunita.Core.Projects.KanbanLane),
        ["BoardItemPlacement"]       = typeof(Kumunita.Core.Projects.BoardItemPlacement),
        ["TodoTranslation"]          = typeof(Kumunita.Core.Projects.TodoTranslation),
        ["BoardTranslation"]         = typeof(Kumunita.Core.Projects.BoardTranslation),
        ["ProjectTranslation"]       = typeof(Kumunita.Core.Projects.ProjectTranslation),
        ["TodoComment"]              = typeof(Kumunita.Core.Projects.TodoComment),
        ["Notification"]             = typeof(Kumunita.Core.Notifications.Notification),
        ["NotificationPreference"]   = typeof(Kumunita.Core.Notifications.NotificationPreference),
        ["NotificationSubscription"] = typeof(Kumunita.Core.Notifications.NotificationSubscription),
        ["Conversation"]             = typeof(Kumunita.Core.Messaging.Conversation),
        ["Message"]                  = typeof(Kumunita.Core.Messaging.Message),
        ["Page"]                     = typeof(Kumunita.Core.Pages.Page),
        ["PageTranslation"]          = typeof(Kumunita.Core.Pages.PageTranslation),
    };

    /// <summary>
    /// Exports every content doc in the closed inventory — one
    /// <c>docs/{Type}.json</c> (a JSON array of the POCO rows) per registry
    /// entry + the <c>docCounts</c> (type name → row count) the manifest
    /// carries. The loop is uniform (the <see cref="NameToType"/> table
    /// lookup + reflection-dispatched <c>Query&lt;T&gt;()</c> /
    /// <c>ToListAsync()</c> / <c>JsonSerializer</c> — not per-type code).
    /// </summary>
    /// <param name="documentStore">The frozen Marten seam.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>
    /// <c>Docs</c> — the type name → <c>docs/{Type}.json</c> payload bytes
    /// (one entry per registry entry); <c>DocCounts</c> — the type name →
    /// row count (the manifest's <c>doc_counts</c>).
    /// </returns>
    public static async Task<(Dictionary<string, byte[]> Docs, Dictionary<string, int> DocCounts)> ExportAsync(
        IDocumentStore documentStore, CancellationToken ct = default)
    {
        var docs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var docCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        await using var session = documentStore.QuerySession();

        foreach (var entry in PortabilityDocTypes.InOrder())
        {
            ct.ThrowIfCancellationRequested();

            var docType = NameToType[entry.Type];

            // Uniform query + terminal (reflection-dispatched on the
            // resolved Type — the "not per-type code" pin):
            //   IQuerySession.Query<T>() → IMartenQueryable<T> (a real IQueryable<T>)
            //   Marten.QueryableExtensions.ToListAsync<T>(IQueryable<T>, CancellationToken)
            //     → Task<IReadOnlyList<T>> (the concrete materialized value is a List<T>)
            //
            // Both dispatches are reflection-driven on the resolved Type (the
            // "not per-type code" pin). The terminal returns Task<IReadOnlyList<T>>
            // — a typed Task — so we keep the boxed object (not a cast to the
            // non-generic Task, which would drop the result) and read its Result
            // property off the concrete Task<IReadOnlyList<T>> after it completes.
            var queryable = QueryMethod.MakeGenericMethod(docType).Invoke(session, null)!;
            var listTask = ToListAsyncMethod.MakeGenericMethod(docType)
                .Invoke(null, new object?[] { queryable, ct })!;
            await ((Task)listTask).ConfigureAwait(false);
            var list = (System.Collections.IList)listTask.GetType().GetProperty("Result")!.GetValue(listTask)!;

            // Generic JSON round-trip: one Serialize over the List<T> (the
            // same options as KumunitaArchive.ToJson<T>).
            var listType = typeof(List<>).MakeGenericType(docType);
            docs[entry.Type] = JsonSerializer.SerializeToUtf8Bytes(list, listType, JsonOpts);

            docCounts[entry.Type] = list.Count;
        }

        return (docs, docCounts);
    }
}
