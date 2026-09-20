using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;

namespace Kumunita.Core.Events;

/// <summary>
/// The <c>M4</c> (Events) composition service (ADR 0054). A store-composing
/// service kept behind <see cref="IEventService"/> so the Web-side consumer (the
/// <c>EventController</c>, U05) can be tested without a live Postgres (NSubstitute),
/// mirroring the <see cref="Announcements.AnnouncementService"/> /
/// <see cref="Posts.PostService"/> shape.
/// <para>
/// **U01 — this unit — declares the seam + this skeleton and registers it** (the
/// design doc §4 / plan U01). **The read + write lanes are implemented in U03 / U04**;
/// every method body here is a <see cref="NotImplementedException"/> placeholder
/// (the "skeleton, logic lands in U03/U04" pin from the user's brief). The
/// constructor composes the **frozen** seams the lanes will need:
/// <see cref="IDocumentStore"/> (reads open their own <c>QuerySession</c>, writes go
/// through the caller's in-flight <c>IDocumentSession</c> — the C3 same-transaction
/// shape, mirroring <see cref="Announcements.AnnouncementService"/>),
/// <see cref="IAuthorizationService"/> (the frozen read/write decision path, ADR
/// 0006, via the <c>EventToAuditableResource</c> adapter U02), and
/// <see cref="IUserInfoService"/> (standing — role / membership probes).
/// </para>
/// <para>
/// **No new seam on a frozen interface** is opened here (ADR 0054 §3.9 / ADR 0006
/// §A) — the constructor only *consumes* the existing <c>IAuthorizationService</c> /
/// <c>IUserInfoService</c> surfaces. The <c>EventService</c> is the *adapter*
/// (bounded context), not a *branch*.
/// </para>
/// </summary>
public sealed class EventService : IEventService
{
    private readonly IDocumentStore _store;
    private readonly IAuthorizationService _authorization;
    private readonly IUserInfoService _userInfo;

    public EventService(IDocumentStore store, IAuthorizationService authorization, IUserInfoService userInfo)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
    }

    // --- Read lanes (U03) -------------------------------------------------------

    /// <inheritdoc cref="IEventService.ListUpcomingAsync"/> — **U03** implements.
    public Task<IReadOnlyList<Event>> ListUpcomingAsync(string? componentId, string actorId, int page, CancellationToken ct = default)
        => throw new NotImplementedException("M4 U01 skeleton — the feed (U03) has not landed yet.");

    /// <inheritdoc cref="IEventService.GetAsync"/> — **U03** implements.
    public Task<Event> GetAsync(string eventId, string actorId, CancellationToken ct = default)
        => throw new NotImplementedException("M4 U01 skeleton — the detail (U03) has not landed yet.");

    /// <inheritdoc cref="IEventService.GetRsvpsAsync"/> — **U03** implements.
    public Task<IReadOnlyList<EventRsvp>> GetRsvpsAsync(string eventId, CancellationToken ct = default)
        => throw new NotImplementedException("M4 U01 skeleton — the owner-only RSVP list (U03) has not landed yet.");

    /// <inheritdoc cref="IEventService.GetMyRsvpAsync"/> — **U03** implements.
    public Task<EventRsvp?> GetMyRsvpAsync(string eventId, string actorId, CancellationToken ct = default)
        => throw new NotImplementedException("M4 U01 skeleton — the own-RSVP read (U03) has not landed yet.");

    // --- Write lanes (U04) — standing re-checked server-side (§3.4, C3) --------

    /// <inheritdoc cref="IEventService.CreateAsync"/> — **U04** implements.
    public Task<Event> CreateAsync(string actorId, CreateEventRequest request, CancellationToken ct = default)
        => throw new NotImplementedException("M4 U01 skeleton — the create lane (U04) has not landed yet.");

    /// <inheritdoc cref="IEventService.UpdateAsync"/> — **U04** implements.
    public Task<Event> UpdateAsync(string eventId, string actorId, UpdateEventRequest request, CancellationToken ct = default)
        => throw new NotImplementedException("M4 U01 skeleton — the edit lane (U04) has not landed yet.");

    /// <inheritdoc cref="IEventService.PublishAsync"/> — **U04** implements.
    public Task<Event> PublishAsync(string eventId, string actorId, CancellationToken ct = default)
        => throw new NotImplementedException("M4 U01 skeleton — the publish lane (U04) has not landed yet.");

    /// <inheritdoc cref="IEventService.DeleteAsync"/> — **U04** implements.
    public Task DeleteAsync(string eventId, string actorId, CancellationToken ct = default)
        => throw new NotImplementedException("M4 U01 skeleton — the soft-delete lane (U04) has not landed yet.");

    /// <inheritdoc cref="IEventService.RsvpAsync"/> — **U04** implements.
    public Task<EventRsvp> RsvpAsync(string eventId, string actorId, RsvpStatus status, CancellationToken ct = default)
        => throw new NotImplementedException("M4 U01 skeleton — the RSVP lane (U04) has not landed yet.");
}
