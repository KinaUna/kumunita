namespace Kumunita.Core.Notifications;

/// <summary>
/// A suppressed recipient e-mail address (F17 / MAIL-03 — bounce/complaint
/// suppression; pin: <c>docs/design/prod-hardening-f17-bounce.md</c> §1).
/// One row per suppressed address: the <see cref="Id"/> is the
/// **lowercased** recipient e-mail (the document identity — the
/// <c>MediaObject</c> / <c>Notification</c> externally-assigned-string-key
/// precedent; Marten does not auto-generate string Ids). The row is written
/// by the provider-callback ingest (or a future manual lane) — the read-only
/// gate in <see cref="NotificationService.EmitAsync"/> never writes it.
/// Re-suppression of the same address is a no-op write: Marten's
/// <c>Store</c> semantics overwrite <see cref="Reason"/> /
/// <see cref="Created"/> / <see cref="Source"/> with the latest values
/// (last-write-wins on a same-Id document; pin §3.4).
/// </summary>
public sealed class SuppressedAddress
{
    public string Id { get; set; } = string.Empty;          // the lowercased recipient e-mail — the document identity (pin §1)

    public string Reason { get; set; } = string.Empty;      // closed set: "bounce" | "complaint" | "manual" (pin §1; the gate does not read it)

    public DateTimeOffset Created { get; set; }             // write time (pin §1)

    public string Source { get; set; } = string.Empty;      // the provider name (e.g. "ses" / "postal" / "mailpit") or "manual" (pin §1)
}
