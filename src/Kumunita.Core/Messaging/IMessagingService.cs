using Kumunita.Core.Messaging;

namespace Kumunita.Core.Messaging;

/// <summary>
/// The seam over the <see cref="Kumunita.Core.Messaging"/> bounded context
/// (M9, ADR 0105 — direct 1:1 signed-in resident messaging, off by default).
/// The service composes the host-registered Marten <c>IDocumentStore</c>
/// (the <see cref="Announcements.IAnnouncementService"/> store-composing
/// shape — the Web-side consumer can be tested without a live Postgres).
/// <para>
/// U02 ships the two admin-toggle seams (design doc §D2 / §2.2); U03
/// appends the conversation / message read+write seams.
/// </para>
/// </summary>
public interface IMessagingService
{
    // Toggle (D2) — the ADR 0101 shape, floor inverted (a missing
    // LocaleSettings row reads as OFF).
    Task<bool> IsMessagingEnabledAsync();
    Task SetMessagingEnabledAsync(bool enabled, string actorId);
}
