using Kumunita.Core.Notifications;

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
/// A **personal read** (C-M6·3): the recipient reads their own rows — the
/// <c>RecipientId</c> is the whole access story; no <c>IAuthorizationService</c>
/// call, no audit row (F11).
/// </para>
/// </summary>
public sealed record NotificationsInboxViewModel(
    IReadOnlyList<Notification> Notifications,
    int Count);

/// <summary>
/// The <c>GET/POST /notifications/preferences</c> view model (design doc
/// §6.4 routes 4–5; the C-M6·9 lean-default pin).
/// <para>
/// **<see cref="KindsEnabled"/>** (C-M6·9) is the subset of the nine
/// <see cref="NotificationKinds"/> constants the resident has **enabled**;
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
