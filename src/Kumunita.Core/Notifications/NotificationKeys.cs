namespace Kumunita.Core.Notifications;

/// <summary>
/// The C03 named static (D6 — the allowlist permits a static whose name ends
/// in <c>Keys</c>) — the deferred-email idempotency key form. Moved here from
/// the <see cref="NotificationService"/> class (the former
/// <c>NotificationService.DeferredKey</c> static) so cross-assembly consumers
/// (the flush service, the Web flush handler, and their tests) depend on a
/// named static rather than the concrete service (the D6 module-boundary
/// rule).
/// <para>
/// **M20 (ADR 0121, D4) — the deferred-email idempotency key form.**
/// Distinct from the emit-time key
/// (<c>notification:{kind}:{source-id}</c>) so the outbox dedup (F10) does
/// **not** collide with an earlier emit-time stage; the flush job (D5, U04)
/// stages the held email under this key, so a cleared email is delivered
/// **exactly once** (C-M20·4). <paramref name="sourceId"/> is the row's
/// stable source id (the <see cref="Notification.SourceId"/> field). This
/// form is **recorded (U03)** and **consumed by U04 / U05** — the flush job
/// reuses this exact shape; it must not be re-derived (a different form
/// would break the once-delivered guarantee).
/// </para>
/// </summary>
public static class NotificationKeys
{
    /// <summary>
    /// The deferred-email idempotency key form — see the type summary.
    /// </summary>
    public static string DeferredKey(string kind, string sourceId) =>
        $"notification:{kind}:{sourceId}:deferred";
}
