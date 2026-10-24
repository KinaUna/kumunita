using Marten;

namespace Kumunita.Core.Notifications;

/// <summary>
/// F17 / MAIL-03 — the <see cref="ISuppressionService"/> implementation (pin:
/// <c>docs/design/prod-hardening-f17-bounce.md</c> §3.2). A store-composing
/// service over the host-registered <see cref="IDocumentStore"/> (the same
/// "AddTransient with the store injected" shape as <see cref="NotificationService"/>):
/// the write lane opens its own write session (one <c>SaveChangesAsync</c>,
/// invariant C3), the lookup opens a query session (a pure read).
/// <para>
/// Core stays HTTP-free (ADR 0006-D): no HTTP types, no <c>IActionResult</c>,
/// no <c>HttpContext</c>. The provider-callback route (pin §3.3) is the Web
/// <c>MailSuppressionController</c>.
/// </para>
/// </summary>
public sealed class SuppressionService : ISuppressionService
{
    private readonly IDocumentStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="SuppressionService"/> class.
    /// </summary>
    /// <param name="store">The host-registered Marten <see cref="IDocumentStore"/>
    /// (reads open a query session, the write opens a write session).</param>
    public SuppressionService(IDocumentStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public async Task SuppressAsync(
        string email,
        string reason,
        string source,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("An e-mail is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reason is required.", nameof(reason));
        if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("A source is required.", nameof(source));

        var id = email.Trim().ToLowerInvariant();
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        // Pin §1 / §3.4 — the <c>Id</c> is the lowercased e-mail (the document
        // identity). <c>Store</c> on an existing <c>Id</c> overwrites
        // <see cref="SuppressedAddress.Reason"/> / <see cref="SuppressedAddress.Created"/>
        // / <see cref="SuppressedAddress.Source"/> with the latest values
        // (last-write-wins; re-suppression is a no-op write, one row, no
        // duplicate). One <c>SaveChangesAsync</c> (C3).
        session.Store(new SuppressedAddress
        {
            Id = id,
            Reason = reason,
            Created = DateTimeOffset.UtcNow,
            Source = source.Trim().ToLowerInvariant(),
        });
        await session.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<bool> IsSuppressedAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var id = email.Trim().ToLowerInvariant();
        await using var session = _store.QuerySession();
        // A single <c>LoadAsync</c> by the document <c>Id</c> (the lowercased
        // e-mail) — O(1), no index needed (pin §1.2).
        return await session.LoadAsync<SuppressedAddress>(id, ct) is not null;
    }
}
