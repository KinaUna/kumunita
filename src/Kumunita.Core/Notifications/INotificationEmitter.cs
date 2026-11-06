using Kumunita.Core.Identity;
using Marten;

namespace Kumunita.Core.Notifications;

/// <summary>
/// The C03 seam (IMPROVE-CODE lane — plan §C03, D6) — the **emission**
/// contract for cross-module callers. A module reaches another only through
/// an interface (ADR 0006 §D); this is that interface for the
/// Notifications module's emission path.
/// <para>
/// The inbox / preferences / quiet-schedule / subscription read-write surface
/// is the **Notifications module's own** API (consumed by the owner
/// <c>NotificationsController</c> in the Web assembly) — **not** a
/// cross-module seam, so it is **not** on this interface. Cross-module
/// callers (the ~10 Core modules + the Web side-effects that emit) resolve
/// this interface only, never the concrete <see cref="NotificationService"/>.
/// </para>
/// </summary>
public interface INotificationEmitter
{
    /// <summary>
    /// The C-M6 writer (D4, D5, D7) — the emission contract with no
    /// per-target scope (see
    /// <see cref="NotificationService.EmitAsync(IDocumentSession, string, string, string, string, string, string, string, string, CancellationToken)"/>
    /// for the full contract). Returns the stored (or pre-existing, dedup)
    /// <see cref="Notification"/> row, or <c>null</c> when the recipient is
    /// a suppressed sample account in a production environment (ADR 0078).
    /// </summary>
    Task<Notification?> EmitAsync(
        IDocumentSession session,
        string recipientId,
        string kind,
        string idempotencyKey,
        string? body,
        CancellationToken ct = default);

    /// <summary>
    /// The ADR 0084 / 0095 writer (the per-target subscription gate, the
    /// <paramref name="linkPath"/>, <paramref name="acceptPath"/> /
    /// <paramref name="declinePath"/> absolute-link shape) — the full
    /// emission contract (see
    /// <see cref="NotificationService.EmitAsync(IDocumentSession, string, string, string, string, string, string, string, string, CancellationToken)"/>).
    /// </summary>
    Task<Notification?> EmitAsync(
        IDocumentSession session,
        string recipientId,
        string kind,
        string idempotencyKey,
        string? body,
        string? targetId = null,
        string? linkPath = null,
        string? acceptPath = null,
        string? declinePath = null,
        CancellationToken ct = default);
}
