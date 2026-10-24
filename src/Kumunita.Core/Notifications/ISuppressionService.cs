namespace Kumunita.Core.Notifications;

/// <summary>
/// F17 / MAIL-03 — the bounce/complaint **suppression** write + lookup seam
/// (pin: <c>docs/design/prod-hardening-f17-bounce.md</c> §3.2).
/// <para>
/// Core stays HTTP-free (ADR 0006-D): this seam carries no HTTP types — the
/// provider-callback **route** (pin §3.3) is the Web <c>MailSuppressionController</c>,
/// the only place an <c>IActionResult</c> / <c>HttpContext</c> appears. The
/// read-only gate in <see cref="NotificationService.EmitAsync"/> is a separate
/// concern (pin §2) — it inlines its own <c>LoadAsync</c>; it does **not** call
/// <see cref="IsSuppressedAsync"/> (the pin §3.2 note: the pin pins the shape,
/// not the implementation detail).
/// </para>
/// </summary>
public interface ISuppressionService
{
    /// <summary>
    /// Suppress a recipient's e-mail (pin §1 idempotency rule — re-suppression
    /// of the same address is a no-op write: <c>Store</c> on an existing
    /// <see cref="SuppressedAddress.Id"/> overwrites <c>Reason</c> /
    /// <c>Created</c> / <c>Source</c> with the latest values, last-write-wins).
    /// One <c>SaveChangesAsync</c> (invariant C3 — single write lane).
    /// </summary>
    /// <param name="email">The recipient's e-mail (stored lowercased).</param>
    /// <param name="reason">"bounce" | "complaint" (the ingest's closed set).</param>
    /// <param name="source">The provider name (stored lowercased) or "manual".</param>
    /// <param name="ct">Cancellation.</param>
    Task SuppressAsync(
        string email,
        string reason,
        string source,
        CancellationToken ct = default);

    /// <summary>
    /// Read-only lookup (pin §3.2): is this e-mail on the suppression list?
    /// A single <c>LoadAsync</c> by the lowercased e-mail — <c>true</c> when a
    /// <see cref="SuppressedAddress"/> row exists, <c>false</c> when not.
    /// </summary>
    Task<bool> IsSuppressedAsync(string email, CancellationToken ct = default);
}
