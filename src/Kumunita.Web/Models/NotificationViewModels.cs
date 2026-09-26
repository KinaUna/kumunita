using Kumunita.Core.Notifications;
using Kumunita.Core.Projects;

namespace Kumunita.Web.Models;

/// <summary>
/// The <c>GET /notifications</c> inbox view model (design doc §6.4 route 1;
/// the M4 / M5 view-model precedent — a plain shape handed to the view).
/// <para>
/// **<see cref="Notifications"/>** (C-M6·8) is the caller's **most recent
/// <see cref="NotificationService.InboxCap"/> (50)** rows, newest-first
/// (<c>Created</c> descending) — no pagination, no per-kind filter in M6.
/// **<see cref="Count"/>** mirrors <see cref="Notifications"/>.Count (the
/// rendered row count) — never a re-query.
/// </para>
/// <para>
/// **<see cref="TodoCards"/>** (the <c>todo.assign</c> enhancement) maps a
/// <see cref="Notification"/> id to its live <see cref="TodoCard"/> — the
/// to-do the resident was assigned, rendered rich (title + link, description,
/// status, start/due, the boards the resident may read that carry it, and its
/// subtasks the resident may read). Present only for the <c>todo.assign</c>
/// rows the controller could enrich (a to-do later soft-deleted, or one the
/// resident has lost read access to, is absent from the map — the row falls
/// back to its stored subject/body). The access decisions are the
/// <c>IProjectService</c>'s (the <c>IAuthorizationService</c> path, ADR
/// 0006-D) — the inbox view model is a plain shape, it re-derives nothing
/// (C-M6·11). Empty when no row is a <c>todo.assign</c> or the projects
/// seam is absent (the test harness).
/// </para>
/// <para>
/// A **personal read** (C-M6·3): the recipient reads their own rows — the
/// <c>RecipientId</c> is the whole access story; no <c>IAuthorizationService</c>
/// call, no audit row (F11).
/// </para>
/// </summary>
public sealed record NotificationsInboxViewModel(
    IReadOnlyList<Notification> Notifications,
    int Count,
    IReadOnlyDictionary<string, TodoCard> TodoCards);

/// <summary>
/// The live shape of a <c>todo.assign</c> notification's to-do, resolved at
/// inbox-read time (not stored — the <see cref="Notification"/> row keeps
/// only its stable <c>SourceId</c> + the recipient's localized subject/body).
/// <para>
/// **<see cref="Todo"/>** is the to-do itself (title, body, status, start,
/// due — the <c>GetTodoAsync</c> read, which is already <c>Read</c>-gated for
/// the recipient, so its presence means the resident may see it).
/// **<see cref="Boards"/>** are the <see cref="KanbanBoard"/>s the recipient
/// may <c>Read</c> that this to-do is placed on (the <c>
/// ListBoardsForTodoAsync</c> read — a board the resident lacks access to is
/// **not** listed, not merely hidden). **<see cref="Subtasks"/>** are the
/// to-do's subtasks the recipient may <c>Read</c> (the <c>GetTodoAsync</c>
/// subtask set — each is a full to-do with its own <c>Audience</c>, C-M5·7;
/// a denied subtask is not returned). All three are value shapes handed to
/// the view; the view renders the links (a link is a presentation concern,
/// not an access decision).
/// </para>
/// </summary>
public sealed record TodoCard(
    TodoItem Todo,
    IReadOnlyList<KanbanBoard> Boards,
    IReadOnlyList<TodoItem> Subtasks);

/// <summary>
/// The <c>GET/POST /notifications/preferences</c> view model (design doc
/// §6.4 routes 4–5; the C-M6·9 lean-default pin).
/// <para>
/// **<see cref="KindsEnabled"/>** (C-M6·9) is the subset of the
/// <see cref="NotificationKinds.Known"/> closed set the resident has **enabled**;
/// <c>null</c> / empty = **all enabled** (the lean-default — the service's
/// <c>EmailEnabledForAsync</c> gate reads exactly this). Nullable so a
/// resident who unchecks every toggle posts an **empty** list, not <c>null</c>,
/// and the two shapes (no preference yet vs. all disabled) round-trip
/// distinctly. **<see cref="AllKinds"/>** is the closed, code-owned kind
/// vocabulary (<see cref="NotificationKinds.Known"/>, the C-M6·2 pin —
/// ordered, the settings page's canonical display order; a resident cannot
/// choose their own kind string).
/// </para>
/// </summary>
public sealed record NotificationPreferencesViewModel(
    IReadOnlyList<string>? KindsEnabled,
    IReadOnlyList<string> AllKinds);

/// <summary>
/// ADR 0084 — one row of the <c>GET /notifications/subscriptions</c> settings
/// list (the per-target subscriptions surface, as distinct from the
/// per-kind <see cref="NotificationPreferencesViewModel"/>): the target's
/// stable id, its display name (a community's / group's / page's name, or
/// the section label for a sentinel target), and the resident's effective
/// choice for that (kind, target) — the stored row's <c>Enabled</c> value
/// when one exists, else the kind's default from
/// <see cref="Kumunita.Core.Notifications.NotificationKinds.OptInKinds"/>
/// (opt-IN kinds default to disabled, opt-OUT kinds default to enabled).
/// </summary>
public sealed record SubscriptionRow(
    string Kind,
    string TargetId,
    string Name,
    bool Enabled);

/// <summary>
/// The <c>GET/POST /notifications/subscriptions</c> view model (ADR 0084 —
/// the per-target subscriptions settings surface): the ordered
/// <see cref="SubscriptionRow"/> list the view renders, one toggle per
/// (kind, target) pair (announcements per community + the flat/public
/// sentinel, community posts per community, group posts per group, and new
/// sub-pages per parent page the resident has subscribed to).
/// </summary>
public sealed record NotificationSubscriptionsViewModel(
    IReadOnlyList<SubscriptionRow> Rows);
